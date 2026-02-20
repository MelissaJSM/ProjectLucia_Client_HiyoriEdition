using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using ProjectLucia.Status;
using ProjectLucia.ThirdParty.VAD;
using ProjectLucia.ThirdParty.Whisper; // SpeakerVerifierORT 네임스페이스 추가
using TMPro;
using UnityEngine;
using UnityEngine.UI;
// VAD 네임스페이스
// 프로젝트별 네임스페이스 (환경에 맞게 유지)
using Debug = UnityEngine.Debug;

// ReSharper disable RedundantCast

namespace ProjectLucia.ThirdParty.Whisper.Runtime.Utils
{
    /// <summary>
    /// Portion of recorded audio clip.
    /// </summary>
    public struct AudioChunk
    {
        public float[] Data;
        public int Frequency;
        public int Channels;
        public float Length;
        public bool IsVoiceDetected;
    }

    public delegate void OnVadChangedDelegate(bool isSpeechDetected);
    public delegate void OnChunkReadyDelegate(AudioChunk chunk);
    public delegate void OnRecordStopDelegate(AudioChunk recordedAudio);
    
    // [ASV 추가] 화자 인증 성공 시 이벤트 (필요 시 UI 표시 등에 사용)
    public delegate void OnSpeakerVerifiedDelegate(float score);
    
    /// <summary>
    /// Controls microphone input settings and recording. 
    /// </summary>
    public class MicrophoneRecord : MonoBehaviour
    {
        #region Inspector Settings

        [Header("Modules")] private SileroVadDetector _detector;
        public WhisperManager whisperManager;

        // =========================================================
        // 🚀 [ASV 통합] 화자 인식 설정
        // =========================================================
        [Header("Speaker Verification (ASV)")]
        [Tooltip("Speaker Verifier ORT instance. Must be assigned in Inspector.")]
        public SpeakerVerifierORT speakerVerifier;  // 인스펙터에서 할당 필수
        
        [Tooltip("Enable or disable Speaker Verification.")]
        public bool useSpeakerVerification = true;  // 기능 On/Off
        
        [Tooltip("화자 인식을 위해 최소한 모아야 하는 시간 (초) - 1.5초 권장")]
        public float minAsvDuration = 1f; 

        [Header("Recording Settings")]
        [Tooltip("Max length of recorded audio from microphone in seconds")]
        public int maxLengthSec = 60;

        [Tooltip("After reaching max length microphone record will continue")]
        public bool loop = true;

        [Header("Voice Activity Detection (VAD)")]
        public string sileroModelFileName = "silero_vad.onnx";
        public string voxcelebFileName = "voxceleb.onnx";

        [Tooltip("How often VAD checks if current audio chunk has speech")]
        public float vadUpdateRateSec = 0.1f;

        [Tooltip("Seconds of audio record that VAD uses to check if chunk has speech")]
        public float vadContextSec = 1.5f;

        [Tooltip("Threshold of VAD energy activation")]
        public float vadThresold = 0.5f;

        [Tooltip("Length of audio chunks in seconds, useful for streaming")]
        public float chunksLengthSec = 0.5f;

        [Tooltip("Should microphone play echo when recording is complete?")]
        public bool echo = true;

        [Tooltip("Min Speech Duration Ms")] public int minSpeechDurationMs = 250;
        [Tooltip("Min Silence Duration Ms")] public int minSilenceDurationMs = 500; // 문장 끊김 판단

        [Tooltip("Max Speech Duration Seconds")]
        public float maxnSpeechDurationSeconds = Single.PositiveInfinity;

        [Tooltip("Speech Pad Ms")] public int speechPadMs = 30;

        [Header("UI References")]
        [Tooltip("Optional indicator that changes color when speech detected")] [CanBeNull]
        public Image vadIndicatorImage;

        public Button vadButton;
        
        [Header("Microphone selection (optional)")]
        [Tooltip("Optional UI dropdown with all available microphone inputs")]
        [CanBeNull]
        public TMP_Dropdown microphoneDropdown;

        [Tooltip("The label of default microphone input in dropdown")]
        public string microphoneDefaultLabel = "Default microphone";

        [SerializeField] private List<Sprite> vadImages;
        [SerializeField] private List<Sprite> vadFocusImages;
        [SerializeField] private List<Sprite> vadClickImages;

        #endregion

        #region Internal Constants & Fields

        // Whisper expects 16kHz sample rate
        public int Frequency => 16000;

        // 내부 상태 관리
        private enum AsvState { Pending, Verified, Rejected }
        private AsvState _currentAsvState = AsvState.Pending;
        private bool _isVerifying = false; // 검증 진행 중 여부

        // 데이터를 1.5초간 잡아둘 대기열
        private readonly Queue<AudioChunk> _pendingChunks = new Queue<AudioChunk>();
        private readonly List<float> _asvAudioBuffer = new List<float>(); // 검증용 Raw Data Accumulator

        private int _lastVadPos;
        private AudioClip _clip;
        private int _lastChunkPos;
        private int _chunksLength;
        private int _lastMicPos;
        private bool _madeLoopLap;

        private string _selectedMicDevice;

        #endregion

        #region Properties

        public List<Sprite> VadImages
        {
            get => vadImages;
            set => vadImages = value;
        }
        
        public List<Sprite> VadFocusImages
        {
            get => vadFocusImages;
            set => vadFocusImages = value;
        }
        
        public List<Sprite> VadClickImages
        {
            get => vadClickImages;
            set => vadClickImages = value;
        }

        public string SelectedMicDevice
        {
            get => _selectedMicDevice;
            set
            {
                if (value != null && !AvailableMicDevices.Contains(value))
                    throw new ArgumentException("Microphone device not found");
                _selectedMicDevice = value;
            }
        }

        public int ClipSamples => _clip.samples * _clip.channels;

        public string RecordStartMicDevice { get; private set; }
        public bool isRecording;
        public bool IsVoiceDetected { get; private set; }

        public IEnumerable<string> AvailableMicDevices => Microphone.devices;

        #endregion

        #region Events

        /// <summary>
        /// Raised when VAD status changed.
        /// </summary>
        public event OnVadChangedDelegate OnVadChanged;

        /// <summary>
        /// Raised when new audio chunk from microphone is ready.
        /// </summary>
        public event OnChunkReadyDelegate OnChunkReady;

        /// <summary>
        /// Raised when microphone record stopped.
        /// Returns <see cref="maxLengthSec"/> or less of recorded audio.
        /// </summary>
        public event OnRecordStopDelegate OnRecordStop;

        // 인증 성공 이벤트
        public event OnSpeakerVerifiedDelegate OnSpeakerVerified;

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (!isRecording)
                return;

            var micPos = Microphone.GetPosition(RecordStartMicDevice);
            if (micPos < _lastMicPos)
            {
                _madeLoopLap = true;
                if (!loop)
                {
                    LogUtils.Verbose($"Stopping recording, mic pos returned back to {micPos}");
                    StopRecord();
                    return;
                }
                LogUtils.Verbose($"Mic made a new loop lap, continue recording.");
            }

            // 순서: VAD 상태 먼저 갱신 -> 청크 처리
            UpdateVad(micPos);
            UpdateChunks(micPos);
            
            _lastMicPos = micPos;
        }

        private void OnDestroy()
        {
            if (isRecording) StopRecord();

            _detector?.Dispose();
            _detector = null;
            
            Resources.UnloadUnusedAssets();
        }

        #endregion

        #region VAD & Model Management

        // VAD 모델 초기화 및 로드 로직
        public void CheckVadModel()
        {
            if (!SettingData.IsStartMode)
            {
                SettingData.IsExistedVad = IsExistedVadFind();
                if(SettingData.IsDebug) Debug.Log("Checking VAD model existence...");
            }
            
            if (!SettingData.IsExistedVad) return;
            
            // 이미 존재하면 패스
            if (_detector != null) return;

            _detector = new SileroVadDetector(SettingData.VadModelPath, vadThresold, Frequency, minSpeechDurationMs,
                maxnSpeechDurationSeconds, minSilenceDurationMs, speechPadMs);

            if (microphoneDropdown != null)
            {
                microphoneDropdown.options = AvailableMicDevices
                    .Prepend(microphoneDefaultLabel)
                    .Select(text => new TMP_Dropdown.OptionData(text))
                    .ToList();

                microphoneDropdown.value = microphoneDropdown.options
                    .FindIndex(op => op.text == microphoneDefaultLabel);

                microphoneDropdown.onValueChanged.AddListener(OnMicrophoneChanged);
            }
        }

        public bool IsExistedVadFind()
        {
            string vadFolderPath = Path.Combine(Application.streamingAssetsPath, "Vad");
            SettingData.VadModelPath = Path.Combine(vadFolderPath, sileroModelFileName);
            string voxcelebPath = Path.Combine(vadFolderPath, voxcelebFileName);
            
            // SHA1 체크 로직 등은 기존 유지
            if (!File.Exists(SettingData.VadModelPath))
            {
                if(SettingData.IsDebug) Debug.LogError($"[VAD Error] Model not found: {SettingData.VadModelPath}");
                return false; 
            }

            if (!File.Exists(voxcelebPath))
            {
                if(SettingData.IsDebug) Debug.LogError($"[VAD Error] Model not found: {voxcelebPath}");
                return false;
            }

            // (SHA1 체크 로직 생략 가능하면 생략, 필요하면 유지)
            return true;
        }

        private void UpdateVad(int micPos)
        {
            if (!whisperManager.useVad) return;

            var samplesCount = GetMicBufferLength(micPos);
            if (samplesCount <= 0) return;

            var vadUpdateRateSamples = vadUpdateRateSec * _clip.frequency;
            var dt = GetMicPosDist(_lastVadPos, micPos);
            if (dt < vadUpdateRateSamples) return;
            
            _lastVadPos = samplesCount;

            // VAD 판별
            var data = GetMicBufferLast(micPos, vadContextSec);
            var vad = _detector.IsSpeechDetected(data, _clip.channels);

            // 상태 변화 감지
            if (vad != IsVoiceDetected)
            {
                IsVoiceDetected = vad;
                OnVadChanged?.Invoke(vad);
            }

            if (vadIndicatorImage)
            {
                var sprite = vad ? vadImages[1] : vadImages[0];
                vadIndicatorImage.sprite = sprite;
            }
        }

        #endregion

        #region Recording Logic

        public async Task StartRecordAsync()
        {
            if (isRecording) return;

            // VAD Detector 비동기 초기화
            if (_detector == null)
            {
                await Task.Run(() =>
                {
                    string modelPaths = Path.Combine(Application.streamingAssetsPath, "Vad", sileroModelFileName);
                    _detector = new SileroVadDetector(modelPaths, vadThresold, Frequency, minSpeechDurationMs,
                        maxnSpeechDurationSeconds, minSilenceDurationMs, speechPadMs);
                });
            }

            RecordStartMicDevice = SelectedMicDevice;
            _clip = Microphone.Start(RecordStartMicDevice, loop, maxLengthSec, Frequency);
            isRecording = true;

            _lastMicPos = 0;
            _madeLoopLap = false;
            _lastChunkPos = 0;
            _lastVadPos = 0;
            
            // ASV 초기화
            ResetAsvState();

            _chunksLength = (int)(_clip.frequency * _clip.channels * chunksLengthSec);
        }

        public void StopRecord(float dropTimeSec = 0f)
        {
            if (!isRecording) return;

            // 종료 시 마지막 버퍼 처리 및 ASV 초기화
            ResetAsvState();

            // 기존 Stop 처리
            var data = GetMicBuffer(dropTimeSec);
            var finalAudio = new AudioChunk()
            {
                Data = data,
                Channels = _clip.channels,
                Frequency = _clip.frequency,
                IsVoiceDetected = IsVoiceDetected,
                Length = (float)data.Length / (_clip.frequency * _clip.channels)
            };

            Microphone.End(RecordStartMicDevice);
            isRecording = false;
            
            if (_clip != null)
            {
                Destroy(_clip);
                _clip = null;
            }
            
            LogUtils.Verbose($"Stopped microphone recording.");

            if (IsVoiceDetected)
            {
                IsVoiceDetected = false;
                OnVadChanged?.Invoke(false);
            }

            if (echo)
            {
                // [수정] _clip이 null일 때 기본값(1채널, 16000Hz) 사용
                int channels = (_clip != null) ? _clip.channels : 1;
                int frequency = (_clip != null) ? _clip.frequency : 16000;

                var echoClip = AudioClip.Create("echo", data.Length, channels, frequency, false);
                echoClip.SetData(data, 0);
                PlayAudioAndDestroy.Play(echoClip, Vector3.zero);
            }

            OnRecordStop?.Invoke(finalAudio);
            
            // Whisper 모델 해제 등
            if (whisperManager != null)
            {
                // _ = whisperManager.UnloadModel(); // <-- REMOVED: StreamingSampleMic will handle this
                whisperManager.ResetVadDetectUI();
                
                // [중요] 재시작 시 VAD가 다시 동작하도록 useVad를 true로 복구
                whisperManager.useVad = true;
            }
        }

        private void OnMicrophoneChanged(int ind)
        {
            if (microphoneDropdown == null) return;
            var opt = microphoneDropdown.options[ind];
            SelectedMicDevice = opt.text == microphoneDefaultLabel ? null : opt.text;
        }

        #endregion

        #region Audio Processing & ASV

        // ====================================================================
        // 🚀 [핵심 수정] UpdateChunks를 가로채서 ASV 검문소 적용
        // ====================================================================
        private void UpdateChunks(int micPos)
        {
            if (_chunksLength <= 0) return;

            var chunkDist = GetMicPosDist(_lastChunkPos, micPos);
            
            // 처리할 데이터가 Chunk 크기 이상일 때 반복
            while (chunkDist > _chunksLength)
            {
                var origData = new float[_chunksLength];
                _clip.GetData(origData, _lastChunkPos);

                var chunkStruct = new AudioChunk()
                {
                    Data = origData,
                    Frequency = _clip.frequency,
                    Channels = _clip.channels,
                    Length = chunksLengthSec,
                    IsVoiceDetected = IsVoiceDetected
                };

                // ASV 사용 여부에 따라 분기
                if (useSpeakerVerification && speakerVerifier != null)
                {
                    ProcessChunkWithAsv(chunkStruct);
                }
                else
                {
                    // ASV 안 쓰면 기존처럼 바로 WhisperStream으로 보냄
                    OnChunkReady?.Invoke(chunkStruct);
                }

                _lastChunkPos = (_lastChunkPos + _chunksLength) % ClipSamples;
                chunkDist = GetMicPosDist(_lastChunkPos, micPos);
            }
        }

        // 👮‍♂️ 화자 인식 검문소 로직
        private async void ProcessChunkWithAsv(AudioChunk chunk)
        {
            // 0. 검증 중이면 무조건 대기열에 넣고 리턴 (순서 보장)
            if (_isVerifying)
            {
                _pendingChunks.Enqueue(chunk);
                return;
            }

            // 1. 목소리가 감지되지 않음 (침묵)
            if (!IsVoiceDetected)
            {
                // 수집 중이었는데 침묵이 옴 -> 문장 끝 -> 즉시 검증 시도
                if (_currentAsvState == AsvState.Pending && _asvAudioBuffer.Count > 0)
                {
                    await CheckSpeakerIdentityAsync();
                    // 검증이 끝난 후, 현재 침묵 청크 처리
                    // 검증 결과에 따라 상태가 Verified/Rejected로 변했을 것임.
                    
                    if (_currentAsvState == AsvState.Verified)
                    {
                         OnChunkReady?.Invoke(chunk);
                         ResetAsvState(); // 문장 끝났으니 리셋
                    }
                    else if (_currentAsvState == AsvState.Rejected)
                    {
                         ResetAsvState(); // 리셋
                         // Rejected된 문장 뒤의 침묵은 Whisper 문맥 유지를 위해 보냄
                         OnChunkReady?.Invoke(chunk);
                    }
                    return;
                }

                // 이미 판정이 난 상태에서 침묵이 옴 -> 리셋
                if (_currentAsvState != AsvState.Pending)
                {
                    ResetAsvState();
                }

                // 침묵은 (Rejected 상태가 아니면) 항상 통과
                if (_currentAsvState != AsvState.Rejected)
                {
                    OnChunkReady?.Invoke(chunk);
                }
                return;
            }

            // 2. 목소리 감지됨
            switch (_currentAsvState)
            {
                case AsvState.Verified:
                    OnChunkReady?.Invoke(chunk);
                    break;

                case AsvState.Rejected:
                    // 무시
                    break;

                case AsvState.Pending:
                    _pendingChunks.Enqueue(chunk);
                    _asvAudioBuffer.AddRange(chunk.Data);

                    float currentDuration = (float)_asvAudioBuffer.Count / Frequency;
                    if (currentDuration >= minAsvDuration)
                    {
                        await CheckSpeakerIdentityAsync();
                    }
                    break;
            }
        }

        // 🕵️‍♀️ 검증 수행 함수 (비동기)
        private async Task CheckSpeakerIdentityAsync()
        {
            if (_isVerifying) return;
            _isVerifying = true;

            try 
            {
                float[] audioToVerify = _asvAudioBuffer.ToArray();
                var (isVerified, score) = await speakerVerifier.VerifyUserAsync(audioToVerify);

                if (!isRecording) return; // 녹음 중지되었으면 중단

                if (isVerified)
                {
                    if(SettingData.IsDebug) Debug.Log($"<color=cyan>🚀 [ASV Success] 주인님 확인! (Score: {score:F2})</color>");
                    _currentAsvState = AsvState.Verified;
                    OnSpeakerVerified?.Invoke(score);
                    
                    // 대기열 방출
                    while (_pendingChunks.Count > 0)
                        OnChunkReady?.Invoke(_pendingChunks.Dequeue());
                }
                else
                {
                    if(SettingData.IsDebug) Debug.Log($"<color=gray>⛔ [ASV Reject] 타인 차단 (Score: {score:F2})</color>");
                    _currentAsvState = AsvState.Rejected;
                    _pendingChunks.Clear();
                }
                
                _asvAudioBuffer.Clear();
            }
            finally
            {
                _isVerifying = false;
                
                // 검증 중에 쌓인 큐 처리 (Verified 상태라면 방출, Rejected라면 삭제)
                if (_currentAsvState == AsvState.Verified)
                {
                    while (_pendingChunks.Count > 0)
                        OnChunkReady?.Invoke(_pendingChunks.Dequeue());
                }
                else if (_currentAsvState == AsvState.Rejected)
                {
                    _pendingChunks.Clear();
                }
            }
        }

        private void ResetAsvState()
        {
            _currentAsvState = AsvState.Pending;
            _pendingChunks.Clear();
            _asvAudioBuffer.Clear();
            _isVerifying = false;
        }

        #endregion

        #region Helper Methods

        private float[] GetMicBuffer(float dropTimeSec = 0f)
        {
            if (_clip == null) return Array.Empty<float>();
            
            var micPos = Microphone.GetPosition(RecordStartMicDevice);
            var len = GetMicBufferLength(micPos);
            if (len == 0) return Array.Empty<float>();

            var dropTimeSamples = (int)(_clip.frequency * dropTimeSec);
            len = Math.Max(0, len - dropTimeSamples);

            var data = new float[len];
            var offset = _madeLoopLap ? micPos : 0;
            _clip.GetData(data, offset);

            return data;
        }

        private float[] GetMicBufferLast(int micPos, float lastSec)
        {
            if (_clip == null) return Array.Empty<float>();
            
            var len = GetMicBufferLength(micPos);
            if (len == 0) return Array.Empty<float>();

            var lastSamples = (int)(_clip.frequency * lastSec);
            var dataLength = Math.Min(lastSamples, len);
            var offset = micPos - dataLength;
            if (offset < 0) offset = len + offset;

            var data = new float[dataLength];
            _clip.GetData(data, offset);
            return data;
        }

        private int GetMicBufferLength(int micPos)
        {
            if (micPos == 0 && !_madeLoopLap) return 0;
            var len = _madeLoopLap ? ClipSamples : micPos;
            return len;
        }

        private int GetMicPosDist(int prevPos, int newPos)
        {
            if (newPos >= prevPos) return newPos - prevPos;
            return ClipSamples - prevPos + newPos;
        }

        #endregion
    }
}
