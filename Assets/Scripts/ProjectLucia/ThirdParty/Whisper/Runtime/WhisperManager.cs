using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using ProjectLucia.Server;
using ProjectLucia.Status;
using ProjectLucia.ThirdParty.Whisper.Runtime.Native;
using ProjectLucia.ThirdParty.Whisper.Runtime.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLucia.ThirdParty.Whisper.Runtime
{
    /// <summary>
    /// Manages Whisper model lifecycle in Unity scene.
    /// </summary>
    public class WhisperManager : MonoBehaviour
    {
        #region Inspector Settings

        [Header("General")]
        [Tooltip("Log level for whisper loading and inference")]
        public LogLevel logLevel = LogLevel.Log;

        [Header("Model")]
        [SerializeField, Tooltip("Path to model weights file (relative). e.g. \"Whisper/ggml-base.bin\"")]
        private string modelPath;

        [Header("Language")]
        [Tooltip("Output text language. Use empty or \"auto\" for auto-detection.")]
        public string language = "en";

        [Tooltip("Force output text to English translation. Improves translation quality.")]
        public bool translateToEnglish;

        [Header("Advanced settings")]
        [SerializeField, Tooltip("Sampling strategy to use.")]
        private WhisperSamplingStrategy strategy = WhisperSamplingStrategy.WHISPER_SAMPLING_GREEDY;

        [Tooltip("Do not use past transcription (if any) as initial prompt for the decoder.")]
        public bool noContext = true;

        [Tooltip("Force single segment output (useful for streaming).")]
        public bool singleSegment;

        [Tooltip("Output tokens with their confidence in each segment.")]
        public bool enableTokens;

        [Tooltip("Initial prompt as a string variable. It may improve transcription quality or guide it.")]
        [TextArea] public string initialPrompt;

        [Header("Streaming settings")]
        [Tooltip("Minimal portions of audio per whisper stream step, in seconds.")]
        public float stepSec = 1f;

        [Tooltip("How many seconds of previous segment will be used for current segment.")]
        public float keepSec = 0.2f;

        [Tooltip("How many seconds of audio will be recurrently transcribed until context update.")]
        public float lengthSec = 10f;

        [Tooltip("Should stream modify whisper prompt for better context handling?")]
        public bool updatePrompt = true;

        [Tooltip("If false stream will use all information from previous iteration.")]
        public bool dropOldBuffer;

        [Header("Experimental settings")]
        [Tooltip("[EXPERIMENTAL] Output timestamps for each token. Need enabled tokens to work.")]
        public bool tokensTimestamps;

        [Tooltip("[EXPERIMENTAL] Overwrite the audio context size (0 = use default). These may reduce quality.")]
        public int audioCtx;

        [Header("UI References")]
        [SerializeField, Tooltip("Image component to visualize VAD detection state.")]
        private Image VadDetector;

        #endregion

        #region Internal Constants & Fields

        [Header("Inference"), Tooltip("Try to load whisper in GPU for faster inference")]
        private const bool UseGpu = true;

        [Tooltip("Use the Flash Attention algorithm for faster inference")]
        private const bool FlashAttention = false;

        // 중요! 버튼 재시작할때 방어코드. 재시작 시 충돌 방지.
        [HideInInspector] public bool isRecording;

        [HideInInspector] public List<int> availableModelIndices = new List<int>();

        [Tooltip("If true stream will ignore audio chunks with no detected speech.")]
        [HideInInspector] public bool useVad = true;

        private WhisperWrapper _whisper;
        private WhisperParams _params;
        private readonly MainThreadDispatcher _dispatcher = new MainThreadDispatcher();

        public Dictionary<int, string> WhisperModelList = new Dictionary<int, string>()
        {
            { 0, "None" },
            { 1, "ggml-tiny.bin" },
            { 2, "ggml-small.bin" },
            { 3, "ggml-base.bin" },
            { 4, "ggml-medium.bin" },
            { 5, "ggml-large-v3.bin" },
            { 6, "ggml-large-v3-turbo.bin" },
        };

        #endregion

        #region Properties

        public string ModelPath
        {
            get => modelPath;
            set => modelPath = value;
        }

        /// <summary>Checks if whisper weights are loaded and ready to be used.</summary>
        public bool IsLoaded => _whisper != null;

        /// <summary>Checks if whisper weights are still loading and not ready.</summary>
        public bool IsLoading { get; private set; }

        #endregion

        #region Events

        /// <summary>Raised when whisper transcribed a new text segment from audio.</summary>
        public event OnNewSegmentDelegate OnNewSegment;

        /// <summary>Raised when whisper made some progress in transcribing audio (0..100).</summary>
        public event OnProgressDelegate OnProgress;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            LogUtils.Level = logLevel;
        }

        private void OnValidate()
        {
            LogUtils.Level = logLevel;
        }

        private void Update()
        {
            _dispatcher.Update();
        }

        private void OnDestroy()
        {
            // [VRAM Leak Fix] 앱 종료/객체 파괴 시 명시적으로 네이티브 리소스 해제
            if (_whisper != null)
            {
                if(SettingData.IsDebug) Debug.Log("[WhisperManager] Force disposing WhisperWrapper to free VRAM...");
                _whisper.Dispose();
                _whisper = null;
            }
            
            Resources.UnloadUnusedAssets();
        }

        #endregion

        #region Model Management

        /// <summary>
        /// Load model and default parameters. Prepare it for text transcription.
        /// </summary>
        public async Task InitModel()
        {
            // check if model is already loaded or actively loading
            if (IsLoaded)
            {
                LogUtils.Warning("Whisper model is already loaded and ready for use!");
                return;
            }

            if (IsLoading)
            {
                LogUtils.Warning("Whisper model is already loading!");
                return;
            }

            // load model and default params
            IsLoading = true;
            try
            {
                // ✅ 경로 자동 해석 (persistent → StreamingAssets)
                // ModelPath: "Whisper/ggml-base.bin"
                // SettingData.whisperQuantization: "q5" (예시)
                // 목표: "Whisper/ggml-base-q5.bin"
                
                string relative;
                if (string.IsNullOrEmpty(SettingData.WhisperQuantization) || SettingData.WhisperQuantization == "없음")
                {
                    relative = ModelPath;
                }
                else
                {
                    // 확장자(.bin) 앞에 양자화 접미사 삽입
                    // 예: "Whisper/ggml-base.bin" -> "Whisper/ggml-base-q5.bin"
                    relative = ModelPath.Replace(".bin", "-" + SettingData.WhisperQuantization + ".bin");
                }
                
                if(SettingData.IsDebug) Debug.Log($"양자화 포함 경로 결과 : {relative}");
                var absolute = ResolveModelAbsolutePath(relative);

                if (string.IsNullOrEmpty(absolute) || !File.Exists(absolute))
                    throw new FileNotFoundException($"Model file not found.\nrelative={relative}\nresolved={absolute}");

                var context = CreateContextParams();
                _whisper = await WhisperWrapper.InitFromFileAsync(absolute, context);
                _params = WhisperParams.GetDefaultParams(strategy);
                UpdateParams();

                _whisper.OnNewSegment += OnNewSegmentHandler;
                _whisper.OnProgress += OnProgressHandler;
            }
            catch (Exception e)
            {
                LogUtils.Exception(e);
            }

            IsLoading = false;
        }

        /// <summary>
        /// Whisper 모델을 언로드하고 메모리를 해제
        /// </summary>
        public async Task UnloadModel()
        {
            if (!IsLoaded)
            {
                LogUtils.Warning("Whisper model is not loaded. Nothing to unload.");
                return;
            }

            // 1) 이벤트 해제
            _whisper.OnNewSegment -= OnNewSegmentHandler;
            _whisper.OnProgress -= OnProgressHandler;

            // 2) 실행 중이면 중단 요청 & 완료 대기
            if (_whisper.IsRunning)
            {
                _whisper.Cancel();
                while (_whisper.IsRunning)
                    await Task.Yield();
            }

            // 3) 안전하게 해제
            _whisper.Dispose();
            _whisper = null;
            IsLoading = false;
            LogUtils.Log("Whisper model has been unloaded safely.");
        }

        /// <summary>
        /// Checks if the selected Whisper model files (original, q8, q5) exist and match their SHA1 hashes.
        /// Updates SettingData.IsExistedWhisper based on the result.
        /// </summary>
        public void CheckWhisperModel(int value, string selectedFile, string modelPaths)
        {
            if (value == 0)
            {
                SettingData.IsExistedWhisper = true;
                return;
            }

            // modelPaths 예: "Whisper/ggml-base.bin"
            // We need to check all 3 files (original, q8, q5)
            // The 'modelPaths' argument is typically just the base file name relative path.
            // Let's derive the other two from 'selectedFile' (which is the base file name).

            // 1. Original
            string path1 = TryGetExistingModelPath(modelPaths);
            if (path1 == null)
            {
                SettingData.IsExistedWhisper = false;
                return;
            }
            if (!CheckFileSHA1(path1, selectedFile, DownloadSha1.WhisperSha1))
            {
                SettingData.IsExistedWhisper = false;
                return;
            }

            // 2. Q8
            string nameQ8 = selectedFile.Replace(".bin", "-q8.bin");
            string pathQ8 = TryGetExistingModelPath(Path.Combine("Whisper", nameQ8));
            if (pathQ8 == null)
            {
                SettingData.IsExistedWhisper = false;
                return;
            }
            if (!CheckFileSHA1(pathQ8, nameQ8, DownloadSha1.WhisperSha1Q8))
            {
                SettingData.IsExistedWhisper = false;
                return;
            }

            // 3. Q5
            string nameQ5 = selectedFile.Replace(".bin", "-q5.bin");
            string pathQ5 = TryGetExistingModelPath(Path.Combine("Whisper", nameQ5));
            if (pathQ5 == null)
            {
                SettingData.IsExistedWhisper = false;
                return;
            }
            if (!CheckFileSHA1(pathQ5, nameQ5, DownloadSha1.WhisperSha1Q5))
            {
                SettingData.IsExistedWhisper = false;
                return;
            }

            // All passed
            SettingData.IsExistedWhisper = true;
        }

        #endregion

        #region Inference

        /// <summary>
        /// Start async transcription of audio clip.
        /// </summary>
        /// <returns>Full audio transcript. Null if transcription failed.</returns>
        public async Task<WhisperResult> GetTextAsync(AudioClip clip)
        {
            var isLoaded = await CheckIfLoaded();
            if (!isLoaded)
                return null;

            UpdateParams();
            var res = await _whisper.GetTextAsync(clip, _params);
            return res;
        }

        /// <summary>
        /// Start async transcription of audio buffer.
        /// </summary>
        /// <param name="samples">Raw audio buffer.</param>
        /// <param name="frequency">Audio sample rate.</param>
        /// <param name="channels">Audio channels count.</param>
        /// <returns>Full audio transcript. Null if transcription failed.</returns>
        public async Task<WhisperResult> GetTextAsync(float[] samples, int frequency, int channels)
        {
            var isLoaded = await CheckIfLoaded();
            if (!isLoaded)
                return null;

            UpdateParams();
            var res = await _whisper.GetTextAsync(samples, frequency, channels, _params);
            return res;
        }

        /// <summary>
        /// Create a new instance of Whisper streaming transcription.
        /// </summary>
        public async Task<WhisperStream> CreateStream(int frequency, int channels)
        {
            var isLoaded = await CheckIfLoaded();
            if (!isLoaded)
            {
                LogUtils.Error("Model weights aren't loaded! Load model first!");
                return null;
            }

            var param = new WhisperStreamParams(_params,
                frequency, channels, stepSec, keepSec, lengthSec, updatePrompt,
                dropOldBuffer, useVad);
            var stream = new WhisperStream(_whisper, param);
            return stream;
        }

        /// <summary>
        /// Create a new instance of Whisper streaming transcription from microphone input.
        /// </summary>
        public async Task<WhisperStream> CreateStream(MicrophoneRecord microphone)
        {
            if(SettingData.IsDebug) Debug.Log($"마이크로폰 로드 : {microphone}");
            var isLoaded = await CheckIfLoaded();
            if (!isLoaded)
            {
                LogUtils.Error("Model weights aren't loaded! Load model first!");
                return null;
            }

            // Unity는 마이크 단일 채널 사용
            var channels = 1;
            var frequency = microphone.Frequency;
            var param = new WhisperStreamParams(_params,
                frequency, channels, stepSec, keepSec, lengthSec, updatePrompt,
                dropOldBuffer, useVad);
            var stream = new WhisperStream(_whisper, param, microphone);
            return stream;
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// persistentDataPath 우선 -> (없으면) StreamingAssets 로 해석한 절대 경로 반환
        /// </summary>
        private static string ResolveModelAbsolutePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            // 1) persistent
            var p = Path.Combine(Application.persistentDataPath, relativePath);
            if (File.Exists(p))
                return p;

            // 2) StreamingAssets (Standalone/Editor 기준)
            var s = Path.Combine(Application.streamingAssetsPath, relativePath);
            return s;
        }

        /// <summary>
        /// 실제 존재하는 파일 경로 (persistent → StreamingAssets)
        /// </summary>
        private static string TryGetExistingModelPath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return null;

            var p = Path.Combine(Application.persistentDataPath, relativePath);
            if (File.Exists(p)) return p;

            var s = Path.Combine(Application.streamingAssetsPath, relativePath);
            if (File.Exists(s)) return s;

            return null;
        }

        /// <summary>
        /// Checks if currently loaded whisper model supports multilingual transcription.
        /// </summary>
        public bool IsMultilingual()
        {
            if (!IsLoaded)
            {
                LogUtils.Error("Whisper model isn't loaded! Init Whisper model first!");
                return false;
            }

            return _whisper.IsMultilingual;
        }

        private void UpdateParams()
        {
            _params.Language = language;
            _params.Translate = translateToEnglish;
            _params.NoContext = noContext;
            _params.SingleSegment = singleSegment;
            _params.AudioCtx = audioCtx;
            _params.EnableTokens = enableTokens;
            _params.TokenTimestamps = tokensTimestamps;

            if (SettingData.IsCallNow)
            {
                // 호출명(CallName)을 프롬프트에 반영
                string callName = SettingData.CallName;
                if (string.IsNullOrEmpty(callName))
                {
                    callName = "히요리";
                }
                _params.InitialPrompt = $"안녕 {callName}야? 우리 오타 없이 완벽한 문장으로 대화하자. {initialPrompt}";
            }
            else
            {
                _params.InitialPrompt = initialPrompt;
            }
            
        }

        private WhisperContextParams CreateContextParams()
        {
            var context = WhisperContextParams.GetDefaultParams();
            context.UseGpu = UseGpu;
            context.FlashAttn = FlashAttention;
            return context;
        }

        private async Task<bool> CheckIfLoaded()
        {
            if (!IsLoaded && !IsLoading)
            {
                LogUtils.Error("Whisper model isn't loaded! Init Whisper model first!");
                return false;
            }

            // wait while model still loading
            while (IsLoading)
            {
                // 최적화: Task.Yield() 대신 100ms 대기
                await Task.Delay(100);
            }

            return IsLoaded;
        }

        private bool CheckFileSHA1(string fullPath, string fileName, Dictionary<string, string> sha1Dict)
        {
            if (!sha1Dict.TryGetValue(fileName, out string expected))
                return false;

            string actual = AudioModelDownloader.SafeComputeSHA1(fullPath);
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resets the VAD detection UI indicator to its default color.
        /// </summary>
        public void ResetVadDetectUI()
        {
            if (VadDetector != null)
                VadDetector.color = Color.white;
        }

        #endregion

        #region Event Handlers

        private void OnNewSegmentHandler(WhisperSegment segment)
        {
            _dispatcher.Execute(() => { OnNewSegment?.Invoke(segment); });
        }

        private void OnProgressHandler(int progress)
        {
            _dispatcher.Execute(() => { OnProgress?.Invoke(progress); });
        }

        #endregion
    }
}
