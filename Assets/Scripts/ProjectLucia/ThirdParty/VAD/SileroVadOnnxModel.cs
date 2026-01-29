using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

// First(), etc.

namespace ProjectLucia.ThirdParty.VAD
{
    /// <summary>
    /// Silero VAD ONNX 모델을 로드하고 추론을 수행하는 래퍼 클래스입니다.
    /// 오디오 데이터를 입력받아 음성 확률을 반환하며, 내부 상태(State)를 관리합니다.
    /// </summary>
    public sealed class SileroVadOnnxModel : IDisposable
    {
        #region Private Fields (비공개 필드)

        private readonly InferenceSession _session;
        private readonly object _lock = new object();

        // 모델 상태 및 컨텍스트
        private float[][][] _state;
        private float[][] _context;
        private int _lastSr;
        private int _lastBatch;

        // 평탄화된 입력/상태 버퍼 (GC 최적화)
        private float[] _flatInput;
        private float[] _flatState;

        // 지원하는 샘플링 레이트
        private static readonly HashSet<int> KSupportedRates = new HashSet<int> { 8000, 16000 };

        #endregion

        #region Constructor & Dispose (생성자 및 해제)

        /// <summary>
        /// ONNX 모델 파일 경로를 받아 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="modelPath">ONNX 모델 파일 경로</param>
        public SileroVadOnnxModel(string modelPath)
        {
            var so = new SessionOptions();
            try
            {
                // ONNX Runtime 세션 옵션 설정 (최적화 및 스레드 설정)
                so.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
                so.InterOpNumThreads = 1;
                so.IntraOpNumThreads = 1;
                so.EnableMemoryPattern = true;
                so.EnableCpuMemArena = true;

                _session = new InferenceSession(modelPath, so);
            }
            finally
            {
                so.Dispose();
            }

            ResetStates();
        }

        /// <summary>
        /// 리소스를 해제합니다.
        /// </summary>
        public void Dispose()
        {
            _session?.Dispose();
        }

        #endregion

        #region Public Methods (공개 메서드)

        /// <summary>
        /// 모델의 내부 상태(State)와 컨텍스트를 초기화합니다.
        /// </summary>
        public void ResetStates()
        {
            // 상태 텐서 초기화 [2, 1, 128]
            _state = new float[2][][];
            _state[0] = new float[1][]; _state[0][0] = new float[128];
            _state[1] = new float[1][]; _state[1][0] = new float[128];

            _context = Array.Empty<float[]>();
            _lastSr = 0;
            _lastBatch = 0;

            _flatInput = null;
            _flatState = null;
        }

        /// <summary>
        /// 오디오 데이터를 입력받아 음성 확률을 계산합니다.
        /// </summary>
        /// <param name="x">오디오 데이터 (배치 단위, [Batch][Samples])</param>
        /// <param name="sr">샘플링 레이트</param>
        /// <returns>각 배치 항목에 대한 음성 확률 배열</returns>
        public float[] Call(float[][] x, int sr)
        {
            lock (_lock)
            {
                // 입력 검증 및 정규화
                var v = ValidateAndNormalize(x, sr);
                x = v.X;
                sr = v.Sr;

                // 모델 요구사항에 따른 샘플 수 및 컨텍스트 크기 설정
                int numSamples = (sr == 16000) ? 512 : 256;
                int contextCols = (sr == 16000) ? 64 : 32;

                if (x[0].Length != numSamples)
                    throw new ArgumentException($"Provided number of samples is {x[0].Length}. Required: {numSamples} at {sr} Hz.");

                int batch = x.Length;

                // 배치 크기나 샘플링 레이트 변경 시 상태 초기화
                if (_lastBatch == 0 || _lastSr == 0) ResetStates();
                if (_lastSr != 0 && _lastSr != sr)   ResetStates();
                if (_lastBatch != 0 && _lastBatch != batch) ResetStates();

                // 컨텍스트 버퍼 초기화
                if (_context.Length == 0 || _context.Length != batch || _context[0].Length != contextCols)
                {
                    _context = new float[batch][];
                    for (int i = 0; i < batch; i++)
                        _context[i] = new float[contextCols];
                }

                // 컨텍스트와 입력 연결
                var withCtx = ConcatContext(_context, x);

                // 입력 데이터 평탄화 (Flatten)
                int flatInputLen = batch * (withCtx[0].Length);
                EnsureCapacity(ref _flatInput, flatInputLen);
                Flatten2D(withCtx, _flatInput);

                // 상태 데이터 평탄화
                int sDim = _state.Length;
                int hDim = _state[0].Length;
                int dDim = _state[0][0].Length;
                int flatStateLen = sDim * hDim * dDim;
                EnsureCapacity(ref _flatState, flatStateLen);
                Flatten3D(_state, _flatState);

                // ONNX 입력 텐서 생성
                var inputs = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor("input",
                        new DenseTensor<float>(_flatInput, new[] { batch, withCtx[0].Length })),
                    NamedOnnxValue.CreateFromTensor("sr",
                        new DenseTensor<long>(new[] { (long)sr }, new[] { 1 })),
                    NamedOnnxValue.CreateFromTensor("state",
                        new DenseTensor<float>(_flatState, new[] { sDim, hDim, dDim }))
                };

                // 추론 실행
                using var outputs = _session.Run(inputs);
                
                // 결과 텐서 추출
                var outputTensor = outputs.First(o => o.Name == "output").AsTensor<float>();
                var stateTensor  = outputs.First(o => o.Name == "stateN").AsTensor<float>();

                // 컨텍스트 업데이트 (다음 호출을 위해 마지막 부분 저장)
                _context = TailColumns(withCtx, contextCols);
                _lastSr = sr;
                _lastBatch = batch;

                // 상태 업데이트
                var dims = stateTensor.Dimensions;
                int s = dims[0], h = dims[1], d = dims[2];
                if (_state.Length != s || _state[0].Length != h || _state[0][0].Length != d)
                {
                    _state = new float[s][][];
                    for (int i = 0; i < s; i++)
                    {
                        _state[i] = new float[h][];
                        for (int j = 0; j < h; j++)
                            _state[i][j] = new float[d];
                    }
                }
                for (int i = 0; i < s; i++)
                for (int j = 0; j < h; j++)
                for (int k = 0; k < d; k++)
                    _state[i][j][k] = stateTensor[i, j, k];

                return outputTensor.ToArray();
            }
        }

        #endregion

        #region Private Helper Classes & Methods (내부 헬퍼)

        private sealed class ValidInput
        {
            public float[][] X { get; }
            public int Sr { get; }
            public ValidInput(float[][] x, int sr) { X = x; Sr = sr; }
        }

        private static ValidInput ValidateAndNormalize(float[][] x, int sr)
        {
            if (x == null || x.Length == 0)
                throw new ArgumentException("Input audio is null or empty.");

            // 16k의 정수배는 16k로 등간격 다운샘플링
            if (sr != 16000 && (sr % 16000 == 0))
            {
                int step = sr / 16000;
                float[][] reduced = new float[x.Length][];
                for (int b = 0; b < x.Length; b++)
                {
                    var src = x[b];
                    int dstLen = (src.Length + step - 1) / step;
                    var dst = new float[dstLen];
                    for (int si = 0, di = 0; si < src.Length; si += step, di++)
                        dst[di] = src[si];
                    reduced[b] = dst;
                }
                x = reduced;
                sr = 16000;
            }

            if (!KSupportedRates.Contains(sr))
                throw new ArgumentException($"Unsupported sample rate {sr}. Supported: 8000, 16000, or multiples of 16000 (auto-downsampled).");

            int need = (sr == 16000) ? 512 : 256;
            if (x[0].Length < need)
                throw new ArgumentException($"Input audio too short. Need at least {need} samples per item at {sr} Hz.");

            return new ValidInput(x, sr);
        }

        private static float[][] ConcatContext(float[][] ctx, float[][] x)
        {
            if (ctx.Length == 0) return x;
            if (ctx.Length != x.Length) throw new ArgumentException("Context and input batch size mismatch.");

            int rows = x.Length;
            int colsA = ctx[0].Length;
            int colsB = x[0].Length;

            var result = new float[rows][];
            for (int r = 0; r < rows; r++)
            {
                var dst = new float[colsA + colsB];
                Array.Copy(ctx[r], 0, dst, 0, colsA);
                Array.Copy(x[r], 0, dst, colsA, colsB);
                result[r] = dst;
            }
            return result;
        }

        private static float[][] TailColumns(float[][] array, int tail)
        {
            int rows = array.Length;
            int cols = array[0].Length;
            if (tail > cols) tail = cols;

            var result = new float[rows][];
            int start = cols - tail;
            for (int r = 0; r < rows; r++)
            {
                var dst = new float[tail];
                Array.Copy(array[r], start, dst, 0, tail);
                result[r] = dst;
            }
            return result;
        }

        private static void EnsureCapacity(ref float[] buffer, int len)
        {
            if (buffer == null || buffer.Length < len)
                buffer = new float[len];
        }

        private static void Flatten2D(float[][] src, float[] dst)
        {
            int idx = 0;
            foreach (var row in src)
            {
                Array.Copy(row, 0, dst, idx, row.Length);
                idx += row.Length;
            }
        }

        private static void Flatten3D(float[][][] src, float[] dst)
        {
            int idx = 0;
            foreach (var a in src)
            {
                foreach (var b in a)
                {
                    Array.Copy(b, 0, dst, idx, b.Length);
                    idx += b.Length;
                }
            }
        }

        #endregion
    }
}
