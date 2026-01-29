using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectLucia.ThirdParty.VAD
{
    /// <summary>
    /// Silero VAD(Voice Activity Detection) 모델을 사용하여 오디오 데이터에서 음성 구간을 감지하는 클래스입니다.
    /// ONNX 모델을 로드하고, 오디오 샘플을 분석하여 음성 시작/종료 지점을 식별합니다.
    /// </summary>
    public class SileroVadDetector : IDisposable
    {
        #region Private Fields (비공개 필드)

        private readonly SileroVadOnnxModel _model;
        private readonly float _threshold;       // 음성 감지 임계값 [0,1]
        private readonly float _negThreshold;    // 음성 종료 임계값 [0,1] (히스테리시스 적용)
        private readonly int _samplingRate;
        private readonly int _windowSizeSample;
        private readonly float _minSpeechSamples;
        private readonly float _speechPadSamples;
        private readonly float _maxSpeechSamples; 
        private readonly float _minSilenceSamples;
        private readonly float _minSilenceSamplesAtMaxSpeech;

        private int _audioLengthSamples;

        private const float ThresholdGap = 0.15f;
        private const int SamplingRate8K = 8000;
        private const int SamplingRate16K = 16000;

        #endregion

        #region Constructor (생성자)

        /// <summary>
        /// SileroVadDetector 인스턴스를 생성하고 초기화합니다.
        /// </summary>
        /// <param name="onnxModelPath">ONNX 모델 파일 경로</param>
        /// <param name="threshold">음성 감지 임계값 (0.0 ~ 1.0)</param>
        /// <param name="samplingRate">샘플링 레이트 (8000 또는 16000)</param>
        /// <param name="minSpeechDurationMs">최소 음성 지속 시간 (ms)</param>
        /// <param name="maxSpeechDurationSeconds">최대 음성 지속 시간 (초)</param>
        /// <param name="minSilenceDurationMs">최소 침묵 지속 시간 (ms)</param>
        /// <param name="speechPadMs">음성 구간 앞뒤 패딩 시간 (ms)</param>
        public SileroVadDetector(
            string onnxModelPath,
            float threshold,
            int samplingRate,
            int minSpeechDurationMs,
            float maxSpeechDurationSeconds,
            int minSilenceDurationMs,
            int speechPadMs)
        {
            if (samplingRate != SamplingRate8K && samplingRate != SamplingRate16K)
                throw new ArgumentException("Sampling rate not supported. Only 8000 or 16000 Hz are allowed.");

            // 임계값 보정 및 히스테리시스 설정
            float th = Mathf.Clamp01(threshold);
            float neg = Mathf.Clamp01(th - ThresholdGap);

            _model = new SileroVadOnnxModel(onnxModelPath);
            _samplingRate = samplingRate;
            _threshold = th;
            _negThreshold = neg;

            // 윈도우 크기 설정 (샘플링 레이트에 따라 다름)
            _windowSizeSample = (samplingRate == SamplingRate16K) ? 512 : 256;

            // 샘플 수 계산
            _minSpeechSamples = samplingRate * (minSpeechDurationMs / 1000f);
            _speechPadSamples = samplingRate * (speechPadMs / 1000f);

            // 최대 음성 길이 제한 (음수 방지 및 안전 여유분)
            float rawMax = samplingRate * maxSpeechDurationSeconds - _windowSizeSample - 2f * _speechPadSamples;
            _maxSpeechSamples = Mathf.Max(0f, rawMax);

            _minSilenceSamples = samplingRate * (minSilenceDurationMs / 1000f);
            _minSilenceSamplesAtMaxSpeech = samplingRate * (98 / 1000f);

            Reset();
        }

        #endregion

        #region Public Methods (공개 메서드)

        /// <summary>
        /// 내부 상태를 초기화합니다.
        /// </summary>
        private void Reset()
        {
            _model.ResetStates();
        }

        /// <summary>
        /// AudioClip에서 음성 구간을 감지하여 반환합니다.
        /// </summary>
        /// <param name="audioClip">분석할 AudioClip</param>
        /// <returns>감지된 음성 세그먼트 리스트</returns>
        public List<SileroSpeechSegment> GetSpeechSegmentListFromAudioClip(AudioClip audioClip)
        {
            if (audioClip == null) return new List<SileroSpeechSegment>();

            Reset();

            int totalSamples = audioClip.samples * audioClip.channels;
            var samples = new float[totalSamples];
            audioClip.GetData(samples, 0);

            return GetSpeechSegmentListFromPcm(samples, audioClip.channels);
        }

        /// <summary>
        /// PCM 데이터(float 배열)에서 음성 구간을 빠르게 감지합니다. (임계값 통과 시 즉시 true 반환)
        /// </summary>
        /// <param name="pcmData">PCM 오디오 데이터</param>
        /// <param name="channels">채널 수 (기본값 1)</param>
        /// <returns>음성이 감지되면 true</returns>
        public bool IsSpeechDetectedFast(float[] pcmData, int channels = 1)
        {
            if (pcmData == null || pcmData.Length == 0) return false;

            Reset();

            int hop = _windowSizeSample;

            // 다채널 처리: 채널 믹스다운(평균) 후 윈도우 단위 분석
            if (channels > 1)
            {
                int monoLen = pcmData.Length / channels;
                var buffer = new float[hop];
                for (int i = 0; i <= monoLen - hop; i += hop)
                {
                    for (int j = 0; j < hop; j++)
                    {
                        float acc = 0f;
                        int baseIdx = (i + j) * channels;
                        for (int ch = 0; ch < channels; ch++) acc += pcmData[baseIdx + ch];
                        buffer[j] = acc / channels;
                    }
                    float p = _model.Call(new[] { buffer }, _samplingRate)[0];
                    if (p >= _threshold) return true;
                }
            }
            else
            {
                var buffer = new float[hop];
                for (int i = 0; i <= pcmData.Length - hop; i += hop)
                {
                    Array.Copy(pcmData, i, buffer, 0, hop);
                    float p = _model.Call(new[] { buffer }, _samplingRate)[0];
                    if (p >= _threshold) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// PCM 데이터에서 음성이 감지되었는지 확인합니다. (전체 분석 후 결과 반환)
        /// </summary>
        /// <param name="pcmData">PCM 오디오 데이터</param>
        /// <param name="channels">채널 수</param>
        /// <returns>음성이 감지되면 true</returns>
        public bool IsSpeechDetected(float[] pcmData, int channels = 1)
        {
            var segments = GetSpeechSegmentListFromPcm(pcmData, channels);
            return segments is { Count: > 0 };
        }

        /// <summary>
        /// 리소스를 해제합니다.
        /// </summary>
        public void Dispose()
        {
            _model?.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private Methods (내부 로직)

        /// <summary>
        /// PCM 데이터로부터 음성 세그먼트 리스트를 추출합니다.
        /// </summary>
        private List<SileroSpeechSegment> GetSpeechSegmentListFromPcm(float[] pcmData, int channels = 1)
        {
            var segments = new List<SileroSpeechSegment>();
            if (pcmData == null || pcmData.Length == 0) return segments;

            Reset();

            // 채널 믹스다운 (스테레오 -> 모노)
            float[] monoSamples;
            if (channels > 1)
            {
                int monoLen = pcmData.Length / channels;
                monoSamples = new float[monoLen];
                for (int i = 0; i < monoLen; i++)
                {
                    float acc = 0f;
                    int baseIdx = i * channels;
                    for (int ch = 0; ch < channels; ch++) acc += pcmData[baseIdx + ch];
                    monoSamples[i] = acc / channels;
                }
            }
            else
            {
                monoSamples = pcmData;
            }

            _audioLengthSamples = monoSamples.Length;

            // 윈도우 단위로 확률 계산
            int hop = _windowSizeSample;
            int frameCount = (monoSamples.Length - hop) >= 0 ? ((monoSamples.Length - hop) / hop + 1) : 0;
            var speechProbList = new List<float>(Mathf.Max(0, frameCount));

            var buffer = new float[hop]; 
            for (int offset = 0; offset <= monoSamples.Length - hop; offset += hop)
            {
                Array.Copy(monoSamples, offset, buffer, 0, hop);
                float p = _model.Call(new[] { buffer }, _samplingRate)[0];
                speechProbList.Add(p);
            }

            return CalculateProb(speechProbList);
        }

        /// <summary>
        /// 확률 리스트를 기반으로 음성 세그먼트를 계산합니다.
        /// </summary>
        private List<SileroSpeechSegment> CalculateProb(List<float> speechProbList)
        {
            var result = new List<SileroSpeechSegment>();
            if (speechProbList == null || speechProbList.Count == 0) return result;

            bool triggered = false;
            int tempEnd = 0, prevEnd = 0, nextStart = 0;

            var segment = new SileroSpeechSegment(); 

            for (int i = 0; i < speechProbList.Count; i++)
            {
                float p = speechProbList[i];
                int pos = _windowSizeSample * i;

                // 임계값 초과 시 시작점 갱신
                if (p >= _threshold && tempEnd != 0)
                {
                    tempEnd = 0;
                    if (nextStart < prevEnd) nextStart = pos;
                }

                if (p >= _threshold && !triggered)
                {
                    triggered = true;
                    segment.StartOffset = pos;
                    continue;
                }

                // 최대 길이 초과 시 강제 분할
                if (triggered && segment.StartOffset.HasValue &&
                    (pos - segment.StartOffset.Value) > _maxSpeechSamples)
                {
                    if (prevEnd != 0)
                    {
                        segment.EndOffset = prevEnd;
                        TryAddIfLongEnough(result, segment);
                        segment = new SileroSpeechSegment();

                        triggered = (nextStart >= prevEnd);
                        if (triggered) segment.StartOffset = nextStart;

                        prevEnd = nextStart = tempEnd = 0;
                    }
                    else
                    {
                        segment.EndOffset = pos;
                        TryAddIfLongEnough(result, segment);
                        segment = new SileroSpeechSegment();
                        prevEnd = nextStart = tempEnd = 0;
                        triggered = false;
                    }
                }

                // 임계값 미만 (침묵) 처리
                if (p < _negThreshold && triggered)
                {
                    if (tempEnd == 0) tempEnd = pos;

                    if ((pos - tempEnd) > _minSilenceSamplesAtMaxSpeech)
                        prevEnd = tempEnd;

                    if ((pos - tempEnd) >= _minSilenceSamples)
                    {
                        segment.EndOffset = tempEnd;
                        TryAddIfLongEnough(result, segment);

                        segment = new SileroSpeechSegment();
                        prevEnd = nextStart = tempEnd = 0;
                        triggered = false;
                    }
                }
            }

            // 마지막 세그먼트 처리
            if (segment.StartOffset.HasValue &&
                (_audioLengthSamples - segment.StartOffset.Value) > _minSpeechSamples)
            {
                segment.EndOffset = _audioLengthSamples;
                TryAddIfLongEnough(result, segment);
            }

            // 병합 및 시간 계산
            return MergeListAndCalculateSecond(result, _samplingRate);
        }

        /// <summary>
        /// 세그먼트 길이가 최소 기준을 만족하면 리스트에 추가합니다.
        /// </summary>
        private void TryAddIfLongEnough(List<SileroSpeechSegment> list, SileroSpeechSegment seg)
        {
            if (!seg.StartOffset.HasValue || !seg.EndOffset.HasValue) return;

            if ((seg.EndOffset.Value - seg.StartOffset.Value) > _minSpeechSamples)
                list.Add(seg);
        }

        /// <summary>
        /// 세그먼트들을 병합하고 시간(초) 정보를 계산합니다.
        /// </summary>
        private List<SileroSpeechSegment> MergeListAndCalculateSecond(List<SileroSpeechSegment> original, int samplingRate)
        {
            var result = new List<SileroSpeechSegment>();
            if (original == null || original.Count == 0) return result;

            original.Sort((a, b) =>
            {
                int ax = a.StartOffset ?? 0;
                int bx = b.StartOffset ?? 0;
                return ax.CompareTo(bx);
            });

            int audioLen = _audioLengthSamples;

            int left = Mathf.Clamp(original[0].StartOffset ?? 0, 0, audioLen);
            int right = Mathf.Clamp(original[0].EndOffset ?? 0, 0, audioLen);

            for (int i = 1; i < original.Count; i++)
            {
                int s = Mathf.Clamp(original[i].StartOffset ?? 0, 0, audioLen);
                int e = Mathf.Clamp(original[i].EndOffset ?? 0, 0, audioLen);

                if (s > right)
                {
                    result.Add(new SileroSpeechSegment(
                        left, right,
                        CalculateSecondByOffset(left, samplingRate),
                        CalculateSecondByOffset(right, samplingRate)
                    ));
                    left = s;
                    right = e;
                }
                else
                {
                    right = Math.Max(right, e);
                }
            }

            result.Add(new SileroSpeechSegment(
                left, right,
                CalculateSecondByOffset(left, samplingRate),
                CalculateSecondByOffset(right, samplingRate)
            ));

            // 패딩 적용 및 겹침 보정
            for (int i = 0; i < result.Count; i++)
            {
                var item = result[i];

                if (i == 0)
                {
                    item.StartOffset = Math.Max(0, (item.StartOffset ?? 0) - (int)_speechPadSamples);
                }

                if (i != result.Count - 1)
                {
                    var nextItem = result[i + 1];
                    int aEnd = item.EndOffset ?? 0;
                    int bStart = nextItem.StartOffset ?? 0;

                    int silence = bStart - aEnd;
                    if (silence < 2 * _speechPadSamples)
                    {
                        int half = silence / 2;
                        item.EndOffset = Math.Min(audioLen, aEnd + half);
                        nextItem.StartOffset = Math.Max(0, bStart - half);
                    }
                    else
                    {
                        item.EndOffset = Math.Min(audioLen, aEnd + (int)_speechPadSamples);
                        nextItem.StartOffset = Math.Max(0, bStart - (int)_speechPadSamples);
                    }
                }
                else
                {
                    int end = item.EndOffset ?? 0;
                    item.EndOffset = Math.Min(audioLen, end + (int)_speechPadSamples);
                }

                // 시간 정보 갱신
                item.StartSecond = CalculateSecondByOffset(item.StartOffset ?? 0, samplingRate);
                item.EndSecond   = CalculateSecondByOffset(item.EndOffset   ?? 0, samplingRate);
            }

            return result;
        }

        /// <summary>
        /// 샘플 오프셋을 시간(초)으로 변환합니다.
        /// </summary>
        private float CalculateSecondByOffset(int offset, int samplingRate)
        {
            float secondValue = offset / (float)samplingRate;
            return Mathf.Floor(secondValue * 1000f) / 1000f; // ms 단위 내림
        }

        #endregion
    }
}
