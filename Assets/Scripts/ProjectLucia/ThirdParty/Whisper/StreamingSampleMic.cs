using System;
using System.Threading;
using System.Threading.Tasks;
using ProjectLucia.GUI;
using ProjectLucia.Status;
using ProjectLucia.ThirdParty.Whisper.Runtime;
using ProjectLucia.ThirdParty.Whisper.Runtime.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// SpeakerVerifierORT 네임스페이스 추가

namespace ProjectLucia.ThirdParty.Whisper
{
    /// <summary>
    /// 마이크 입력을 실시간으로 Whisper 모델에 스트리밍하여 음성 인식을 수행하는 클래스입니다.
    /// VAD(음성 감지) 및 화자 인식(Speaker Verification) 기능과 연동됩니다.
    /// </summary>
    public class StreamingSampleMic : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Header("References (참조)")]
        [Tooltip("Whisper 모델 관리자")]
        public WhisperManager whisper;

        [Tooltip("마이크 녹음 및 VAD 처리")]
        public MicrophoneRecord microphoneRecord;

        [Tooltip("화자 인식(Speaker Verification) 모듈")]
        public SpeakerVerifierORT speakerVerifier; 

        [Header("UI (Optional)")]
        [Tooltip("인식된 텍스트를 표시할 UI (선택 사항)")]
        public TMP_InputField text;

        #endregion

        #region Private Fields (비공개 필드)

        private WhisperStream _stream;
        private Task _streamTask = Task.CompletedTask; // 백그라운드 실행 태스크
        private CancellationTokenSource _cts;     // StartStream 래핑용 토큰

        // 상태 및 동기화 변수
        private volatile bool _isRunning;         // 스트림+마이크 정상 동작 여부
        private bool _isStarting;
        private bool _isStopping;
        private bool _isDestroyed;                // OnDestroy 호출 여부
        private bool _isAppQuitting;              // OnApplicationQuit 호출 여부
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        // 매니저 참조
        private ActionManager _actionManager;
        private WhisperManager _whisperManager;
        private InputHandler _inputHandler;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 매니저 참조 가져오기
            _actionManager        = GameManager.Instance.ActionManager;
            _whisperManager       = GameManager.Instance.WhisperManager;
            _inputHandler         = GameManager.Instance.InputHandler;
            
            // SpeakerVerifierORT 자동 찾기 (없으면 인스펙터 할당 필요)
            if (speakerVerifier == null)
                speakerVerifier = FindFirstObjectByType<SpeakerVerifierORT>();
        }

        private void OnApplicationQuit()
        {
            _isAppQuitting = true;
            
            // 1. CTS 취소 (백그라운드 태스크 중단 신호)
            if (_cts != null)
            {
                try { _cts.Cancel(); }
                catch { /* ignored */ }

                try { _cts.Dispose(); }
                catch { /* ignored */ }

                _cts = null;
            }

            // 2. 마이크 강제 종료
            if (microphoneRecord != null)
            {
                try { microphoneRecord.StopRecord(); }
                catch { /* ignored */ }
            }
            
            // 3. 스트림 참조 해제
            if (_stream != null)
            {
                DetachStreamEvents(_stream);
                _stream = null;
            }
        }

        private void OnDestroy()
        {
            _isDestroyed = true;
            
            // 앱 종료 중이면 OnDestroy에서 추가 작업을 하지 않음
            if (_isAppQuitting) return;

            // 비동기 정리 작업 실행 (Fire-and-forget)
            _ = CleanupAsync();
        }

        #endregion

        #region Public Methods (공개 메서드)

        /// <summary>
        /// 마이크 버튼 클릭 시 호출됩니다. 스트리밍을 시작하거나 중지합니다.
        /// </summary>
        public async void OnButtonPressed()
        {
            try
            {
                // 환경 검증 (GPU 매칭/모델/음성 VAD 설치 여부)
                if (!SettingData.IsMatchedGPU || string.IsNullOrEmpty(SettingData.ResultGPUName))
                {
                    _actionManager.ErrorCharacterAction(
                        string.IsNullOrEmpty(SettingData.ResultGPUName) ? 2100 : 2101, false);
                    return;
                }
                if (!SettingData.IsExistedWhisper || !SettingData.IsExistedVad)
                {
                    _actionManager.ErrorCharacterAction(
                        !SettingData.IsExistedWhisper && !SettingData.IsExistedVad ? 2000 :
                        (!SettingData.IsExistedWhisper ? 2001 : 2002), false);
                    return;
                }

                await _gate.WaitAsync();
                if (!_isRunning)
                    await SafeStartAsync();
                else
                    await SafeStopAsync();
            }
            catch (Exception e)
            {
                if(SettingData.IsDebug) Debug.LogError("마이크 스트림 토글 중 예외");
                if(SettingData.IsDebug) Debug.LogException(e);

                // 예외 메시지에 따른 에러 처리
                var msg = e.ToString();
                if (msg.IndexOf("onnx", StringComparison.OrdinalIgnoreCase) >= 0)
                    _actionManager.ErrorCharacterAction(2002, false);
                else if (msg.IndexOf("null", StringComparison.OrdinalIgnoreCase) >= 0)
                    _actionManager.ErrorCharacterAction(2001, false);
                else
                {
                    _actionManager.ErrorCharacterAction(2005, false);
                    SettingData.IsExistedVad = false;
                    SettingData.IsExistedWhisper = false;
                }
            }
            finally
            {
                if (_gate.CurrentCount == 0) _gate.Release();
            }
        }

        /// <summary>
        /// 외부에서 강제로 스트리밍을 재시작할 때 호출합니다. (예: 설정 변경 시)
        /// </summary>
        public async void OnRestart()
        {
            try
            {
                await _gate.WaitAsync();
                await SafeStopAsync();
                await SafeStartAsync();
            }
            catch (Exception e)
            {
                if(SettingData.IsDebug) Debug.LogError("OnRestart 중 예외");
                if(SettingData.IsDebug) Debug.LogException(e);
                _actionManager.ErrorCharacterAction(2005, false);
                SettingData.IsExistedVad = false;
                SettingData.IsExistedWhisper = false;
            }
            finally
            {
                if (_gate.CurrentCount == 0) _gate.Release();
            }
        }

        #endregion

        #region Internal Logic (내부 로직)

        private async Task SafeStartAsync()
        {
            if (_isStarting || _isRunning || _isDestroyed || _isAppQuitting) return;
            _isStarting = true;

            try
            {
                // 1) 모델 준비 (Whisper & SpeakerVerifier)
                var whisperTask = whisper.InitModel();
                
                if (speakerVerifier != null)
                {
                    // 화자 인식 모델 초기화 (비동기)
                    StartCoroutine(speakerVerifier.InitModel());
                }

                await whisperTask; // Whisper 모델 로드 대기

                // 2) 스트림 준비 및 이벤트 연결
                _stream = await whisper.CreateStream(microphoneRecord);
                if (_stream == null)
                    throw new InvalidOperationException("WhisperStream 생성 실패");

                AttachStreamEvents(_stream);

                // 3) 스트림 실행 (백그라운드)
                _cts = new CancellationTokenSource();
                _streamTask = Task.Run(() =>
                {
                    try { _stream.StartStream(); }
                    catch (Exception ex) { if(SettingData.IsDebug) Debug.LogException(ex); }
                }, _cts.Token);

                // 4) 마이크 시작 (메인 스레드 비동기)
                await microphoneRecord.StartRecordAsync();

                _whisperManager.isRecording = true;
                _isRunning = true;
                if(SettingData.IsDebug) Debug.Log("🎤 스트림 시작 완료");
                
                // UI 업데이트 (녹음 중 상태)
                microphoneRecord.vadButton.image.sprite = microphoneRecord.VadImages[1];
                SpriteState newSpriteState = microphoneRecord.vadButton.spriteState;
                newSpriteState.highlightedSprite = microphoneRecord.VadImages[1];
                newSpriteState.pressedSprite = microphoneRecord.VadImages[1];

            }
            finally
            {
                _isStarting = false;
            }
        }

        private async Task SafeStopAsync()
        {
            // 앱 종료 중이면 복잡한 정리 절차 생략
            if (_isAppQuitting) return;

            if (_isStopping || !_isRunning) return;
            _isStopping = true;

            try
            {
                // 1) 마이크 중지
                if (microphoneRecord != null)
                {
                    try 
                    { 
                        if (microphoneRecord.isRecording) 
                            microphoneRecord.StopRecord(); 
                    }
                    catch (Exception e) { if(SettingData.IsDebug) Debug.LogException(e); }
                }

                // 2) 스트림 종료 신호
                if (_stream != null)
                {
                    try { _stream.StopStream(); } 
                    catch (Exception e) { if(SettingData.IsDebug) Debug.LogException(e); }
                }

                // 3) 스트림 처리 완전 종료 대기 (최대 2초)
                if (_streamTask != null)
                {
                    var completed = await Task.WhenAny(_streamTask, Task.Delay(2000));
                    if (completed != _streamTask)
                    {
                        if(SettingData.IsDebug) Debug.LogWarning("스트림 종료 대기 타임아웃 (모델 언로드 강제 진행)");
                    }
                }

                // 4) 이벤트 해제 및 정리
                if (_stream != null)
                {
                    DetachStreamEvents(_stream);
                    _stream = null;
                }
                _cts?.Dispose();
                _cts = null;
                _streamTask = Task.CompletedTask;

                if (_whisperManager != null) _whisperManager.isRecording = false;
                _isRunning = false;
                if(SettingData.IsDebug) Debug.Log("🛑 스트림 중지 완료");

                // UI 업데이트 (대기 상태)
                if (!_isDestroyed && !_isAppQuitting && this != null && microphoneRecord != null && 
                    microphoneRecord.vadButton != null && microphoneRecord.vadIndicatorImage != null)
                {
                    try
                    {
                        microphoneRecord.vadIndicatorImage.sprite = microphoneRecord.VadImages[2];
                        microphoneRecord.vadButton.image.sprite = microphoneRecord.VadImages[0];

                        SpriteState newSpriteState = microphoneRecord.vadButton.spriteState;
                        newSpriteState.highlightedSprite = microphoneRecord.VadImages[0];
                        newSpriteState.pressedSprite = microphoneRecord.VadImages[0];
                    }
                    catch (Exception e)
                    {
                        if(SettingData.IsDebug) Debug.LogWarning($"UI update failed during stop: {e.Message}");
                    }
                }

                // 5) 모델 언로드
                if (whisper != null)
                {
                    if(SettingData.IsDebug) Debug.Log("Whisper 모델 언로드 시작");
                    await whisper.UnloadModel();
                }
                
                if (speakerVerifier != null)
                {
                    if(SettingData.IsDebug) Debug.Log("SpeakerVerifier 모델 언로드 시작");
                    speakerVerifier.UnloadModel();
                }
            }
            finally
            {
                _isStopping = false;
            }
        }

        private async Task CleanupAsync()
        {
            try
            {
                if (_gate != null) await _gate.WaitAsync();
                await SafeStopAsync();
            }
            catch (Exception e)
            {
                if(SettingData.IsDebug) Debug.LogException(e);
            }
            finally
            {
                if (_gate is { CurrentCount: 0 }) _gate.Release();
            }
        }

        #endregion

        #region Whisper Event Handlers (Whisper 이벤트 핸들러)

        private void AttachStreamEvents(WhisperStream s)
        {
            s.OnResultUpdated   += OnResult;
            s.OnSegmentUpdated  += OnSegmentUpdated;
            s.OnSegmentFinished += OnSegmentFinished;
            s.OnStreamFinished  += OnFinished;
        }

        private void DetachStreamEvents(WhisperStream s)
        {
            try { s.OnResultUpdated   -= OnResult; } catch { /* ignore */ }
            try { s.OnSegmentUpdated  -= OnSegmentUpdated; } catch { /* ignore */ }
            try { s.OnSegmentFinished -= OnSegmentFinished; } catch { /* ignore */ }
            try { s.OnStreamFinished  -= OnFinished; } catch { /* ignore */ }
        }

        private void OnResult(string result)
        {
            // 실시간 중간 결과 처리 (필요 시 구현)
        }

        private void OnSegmentUpdated(WhisperResult segment)
        {
            if(SettingData.IsDebug) Debug.Log($"[Whisper] Segment updated: {segment.Result}");
        }

        private void OnSegmentFinished(WhisperResult segment)
        {
            if (_isDestroyed || _isAppQuitting || _isStopping || this == null) return;

            string trim = segment.Result.Trim();
            if(SettingData.IsDebug) Debug.Log($"[Whisper] Segment finished: {trim}");

            // [환각 필터링] Whisper 특유의 환각 텍스트 필터링
            if (string.IsNullOrEmpty(trim) ||
                trim.StartsWith("[") && trim.EndsWith("]") || 
                trim.StartsWith("(") && trim.EndsWith(")") || 
                trim.Equals("MBC 뉴스 이덕영입니다.", StringComparison.OrdinalIgnoreCase) || 
                trim.Equals("시청해 주셔서 감사합니다.", StringComparison.OrdinalIgnoreCase))
            {
                if(SettingData.IsDebug) Debug.LogWarning($"[Whisper] Hallucination detected and ignored: {trim}");
                return;
            }

            if (_whisperManager != null && _whisperManager.isRecording)
            {
                try
                {
#pragma warning disable CS4014
                    if (_inputHandler != null)
                        _inputHandler.ProcessInput(segment.Result);
#pragma warning restore CS4014
                }
                catch (Exception ex)
                {
                    if(SettingData.IsDebug) Debug.LogWarning($"Failed to process input: {ex.Message}");
                }

                // VAD 일시 중지 (서버 응답 대기)
                if (whisper != null)
                    whisper.useVad = false;
            }
        }

        private void OnFinished(string finalResult)
        {
            if(SettingData.IsDebug) Debug.Log("[Whisper] Stream finished");
        }

        #endregion
    }
}
