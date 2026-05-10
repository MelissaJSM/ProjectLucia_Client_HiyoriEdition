using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Live2D.Cubism.Framework.Expression;
using ProjectLucia.Capture;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 프로젝트 매니저들
using ProjectLucia.GUI;
using ProjectLucia.Live2D;
using ProjectLucia.Status;
using ProjectLucia.ThirdParty.Whisper;
using ProjectLucia.Windows;

// Status의 GpuInfo를 직접 사용
// ReSharper disable EmptyGeneralCatchClause
// ReSharper disable InconsistentNaming
// ReSharper disable AsyncVoidMethod

namespace ProjectLucia.Server
{
    /// <summary>
    /// 서버와의 통신(WebSocket, HTTP)을 담당하는 클라이언트 클래스입니다.
    /// 채팅, 피드백, 오디오 재생, 화면 관찰(Observer) 및 알림(Notification) 기능을 통합 관리합니다.
    /// </summary>
    public class ServerClient : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Header("Endpoints (자동 설정됨)")]
        [Tooltip("웹소켓 연결 URL")]
        [SerializeField] private string wsUrl;
        [Tooltip("오디오 파일 다운로드 기본 URL")]
        [SerializeField] private string httpAudioBase;
        [Tooltip("서버 재시작 요청 URL")]
        [SerializeField] private string httpRestartUrl;
        [Tooltip("서버 상태 확인(Health Check) URL")]
        [SerializeField] private string healthUrl;
        [Tooltip("이미지 업로드 URL")]
        [SerializeField] private string uploadImageUrl;

        [Header("WebSocket Configuration")]
        [Tooltip("연결 타임아웃 (초)")]
        [SerializeField] private int connectTimeout = 10;
        [Tooltip("하트비트(Ping) 응답 대기 시간 (초)")]
        [SerializeField] private float heartbeatGrace = 60f;
        [Tooltip("자동 재연결 활성화 여부")]
        [SerializeField] private bool autoReconnect = true;
        [Tooltip("재연결 시도 초기 지연 시간 (초)")]
        [SerializeField] private float reconnectInitialDelay = 1f;
        [Tooltip("재연결 시도 최대 지연 시간 (초)")]
        [SerializeField] private float reconnectMaxDelay = 20f;

        [Header("HTTP Configuration")]
        [Tooltip("HTTP 요청 타임아웃 (초)")]
        [SerializeField] private int requestTimeout = 600;

        [Header("MySQL UI Sync")]
        [Tooltip("MySQL 상태 UI 동기화 활성화 여부")]
        [SerializeField] private bool enableMysqlSync = true;
        [Tooltip("MySQL 상태 동기화 주기 (초)")]
        [SerializeField] private float mysqlSyncCooldown = 1.0f;

        [Header("UI References")]
        [Tooltip("웹소켓 연결 상태를 표시할 이미지")]
        [SerializeField] private Image wsDetect;

        [Tooltip("연결 중 상태 스프라이트")]
        [SerializeField] private Sprite wsLoading;
        [Tooltip("연결 성공 상태 스프라이트")]
        [SerializeField] private Sprite wsConnect;
        [Tooltip("연결 실패/에러 상태 스프라이트")]
        [SerializeField] private Sprite wsError;

        [Header("Observer Integration")]
        [Tooltip("화면 관찰 기능을 담당하는 DesktopObserver 참조")]
        public DesktopObserver desktopObserver; 

        #endregion

        #region Public Properties (공개 속성)

        /// <summary>
        /// 현재 서버가 응답(오디오 재생 또는 텍스트 출력) 중인지 여부
        /// </summary>
        public bool HasActiveAnswer => _hasActiveAnswer;

        #endregion

        #region Private Fields (비공개 필드)

        // 웹소켓 및 통신 관련
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<Action> _main = new();
        private string _clientId;
        private float _lastServerPingAt = -999f;
        private bool _connecting;
        private bool _closing;
        private bool _run = true;
        private Coroutine _audioWaitCo;
        
        // 🔥 추가: 진행 중인 다운로드를 추적하기 위한 변수
        private Coroutine _downloadAudioCo; 
        private UnityWebRequest _activeAudioReq; 

        // UI 및 상태 제어
        private bool _hasActiveAnswer;

        // 락(Lock) 제어 변수들
        private bool _isWaitingForChatResult; // [1순위] 유저 채팅 결과를 기다리는 중인지 여부
        private bool _isObservingBusy;        // [2순위] 관찰 또는 알림 처리를 기다리는 중인지 여부 (이미지 처리 중 중복 방지)
        private Coroutine _observeTimeoutCo;  // 관찰/알림 타임아웃 코루틴 참조 (안전 해제용)

        // 외부 입력(InputField 등) 감지용 프로퍼티
        private bool IsUserTyping
        {
            get => desktopObserver != null && desktopObserver.isChatting;
            set { if (desktopObserver != null) desktopObserver.isChatting = value; }
        }

        // 상태 저장 (피드백/로그용)
        private string _lastUserMessage;
        private Action<string> _onFeedbackCompleted;
        private string _lastFeedbackMessage;
        private int _lastFeedbackId;

        // 로그용
        [HideInInspector] public string userDateTime;
        private string NowString() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 매니저 참조
        private TextManager _textManager;
        private MySQLManager _mySQLManager;
        private SaveController _saveController;
        private AudioSource _audioSource;
        private PanelManager _panelManager;
        private ActionManager _actionManager;
        private EmotialController _emotialController;
        private CaptureGalleryManager _captureGalleryManager;
        private SettingController _settingController;
        private ToggleManager _toggleManager;
        private StreamingSampleMic _streamingSampleMic;

        // 최적화: WaitForSeconds 캐싱
        private readonly WaitForSeconds _wait2Sec = new WaitForSeconds(2f);
        private readonly WaitForSeconds _wait3Sec = new WaitForSeconds(3f);
        private WaitForSeconds _waitMysqlSync; 

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            try
            {
                _textManager = GameManager.Instance.TextManager;
                _mySQLManager = GameManager.Instance.MySQLManager;
                _saveController = GameManager.Instance.SaveController;
                _audioSource = GameManager.Instance.AudioSource;
                _panelManager = GameManager.Instance.PanelManager;
                _actionManager = GameManager.Instance.ActionManager;
                _emotialController = GameManager.Instance.EmotialController;
                _captureGalleryManager = GameManager.Instance.CaptureGalleryManager;
                _settingController = GameManager.Instance.SettingController;
                _toggleManager = GameManager.Instance.ToggleManager;
                _streamingSampleMic = GameManager.Instance.StreamingSampleMic;
            }
            catch { /* IntroScene 방어 */ }
        }

        private void Start()
        {
            // 인스펙터 설정값 반영
            _waitMysqlSync = new WaitForSeconds(mysqlSyncCooldown);

            BuildUrlsFromSettingData();

            // DesktopObserver 이벤트 구독
            if (desktopObserver != null)
            {
                desktopObserver.OnScreenChanged += (imageBytes) =>
                {
                    // 백그라운드 스레드에서 이벤트가 발생하므로 메인 스레드 큐로 전달
                    _main.Enqueue(() =>
                    {
                        // 연결 상태이고, 인트로 씬이 아닐 때만 처리
                        if (IsOpen && !IsIntroScene())
                        {
                            StartCoroutine(ProcessObserveRoutine(imageBytes));
                        }
                    });
                };
            }

            if (IsIntroScene()) return;
            
            StartCoroutine(MySqlSyncLoop());
            _ = ConnectLoop();
        }

        private void Update()
        {
            while (_main.TryDequeue(out var a)) { try { a?.Invoke(); } catch (Exception e) { if(SettingData.IsDebug) Debug.LogError(e); } }
        }

        private async void OnApplicationQuit() { _run = false; await Close("quit", hard: true); }
        
        private async void OnDestroy()
        {
            _run = false; 
            try { _cts?.Cancel(); } catch { } 
            if (!IsIntroScene()) ForceStopResponse(); 
            await Close("destroy", hard: true);
        }

        #endregion

        #region Initialization (초기화)

        /// <summary>
        /// SettingData의 IP/Port 정보를 바탕으로 서버 URL들을 구성합니다.
        /// </summary>
        public void BuildUrlsFromSettingData()
        {
            var ip = SettingData.ServerIP;
            var pt = SettingData.ServerPort;
            _clientId = SystemInfo.deviceUniqueIdentifier ?? ("client-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            wsUrl = $"ws://{ip}:{pt}/ws?client_id={Uri.EscapeDataString(_clientId)}";
            try
            {
                httpAudioBase = $"http://{ip}:{(int.Parse(pt) + 1).ToString()}/audio/";
            }
            catch
            {
                httpAudioBase = "";
            }
            
            httpRestartUrl = $"http://{ip}:{pt}/restart";
            healthUrl = $"http://{ip}:{pt}/health";
            uploadImageUrl = $"http://{ip}:{pt}/upload/image";
        }

        #endregion

        #region Observer & Notification Logic (관찰 및 알림 로직)

        /// <summary>
        /// 화면 변화 감지 시 이미지를 업로드하고 관찰 패킷을 전송하는 코루틴입니다.
        /// </summary>
        private IEnumerator ProcessObserveRoutine(byte[] imageBytes)
        {
            // 방어 코드: 유저가 타이핑 중이거나, AI가 답변 중이거나, 채팅 응답 대기 중이거나, 이미 이미지 처리 중이면 스킵 (2순위)
            if (IsUserTyping || _hasActiveAnswer || _isObservingBusy || _isWaitingForChatResult)
            {
                yield break;
            }

            // [LOCK] 잠금 시작
            _isObservingBusy = true;
            if(desktopObserver.showDebugLog) if(SettingData.IsDebug) Debug.Log("[Client] 화면 변화 감지 -> 이미지 업로드 시작...");

            // 1. 이미지 HTTP 업로드
            string imageId = null;
            WWWForm form = new WWWForm();
            form.AddBinaryData("file", imageBytes, "screen.jpg", "image/jpeg");

            using (UnityWebRequest www = UnityWebRequest.Post(uploadImageUrl, form))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var json = www.downloadHandler.text;
                    var res = JsonUtility.FromJson<UploadImageResponse>(json);
                    if (res is { ok: true })
                    {
                        imageId = res.image_id;
                    }
                }
                else
                {
                    if(SettingData.IsDebug) Debug.LogError($"[Client] Observe Upload Fail: {www.error}");
                    _isObservingBusy = false; // 실패 시 잠금 해제
                    yield break;
                }
            }

            // 2. WS 패킷 전송 (업로드 성공 시)
            // 업로드 하는 동안 상태가 변했을 수 있으므로 다시 체크
            if (!string.IsNullOrEmpty(imageId) && !IsUserTyping && !_hasActiveAnswer && !_isWaitingForChatResult)
            {
                if (desktopObserver.showDebugLog) if(SettingData.IsDebug) Debug.Log($"[Client] Sending Observe Packet ({imageId})");
                
                var pkt = new Packet<ObservePayload>
                {
                    op = "observe",
                    data = new ObservePayload 
                    { 
                        image_id = imageId,
                        user_name = SettingData.UserName // <-- ObservePayload 안으로 이동
                    }
                };
                SendJson(pkt);

                // 안전장치: 응답 누락 대비 60초 타임아웃
                if (_observeTimeoutCo != null) StopCoroutine(_observeTimeoutCo);
                _observeTimeoutCo = StartCoroutine(ReleaseBusyFlagAfterDelay(60.0f));
            }
            else
            {
                _isObservingBusy = false;
            }
        }

        /// <summary>
        /// 외부(NotificationManager 등)에서 알림이 발생했을 때 호출되는 메서드입니다.
        /// </summary>
        public void SendNotificationAlert(string appName, string content, string imagePath)
        {
            // 2순위: 현재 유저가 채팅 중이거나 응답을 기다리거나, 다른 말을 하고 있으면 알림은 무시합니다.
            if (!IsOpen || IsUserTyping || _hasActiveAnswer || _isWaitingForChatResult || _isObservingBusy)
            {
                if(SettingData.IsDebug) Debug.LogWarning($"[알림 무시됨] 현재 히요리가 바쁩니다. ({appName}: {content})");
                return;
            }

            StartCoroutine(NotificationRoutine(appName, content, imagePath));
        }

        private IEnumerator NotificationRoutine(string appName, string content, string imagePath)
        {
            _isObservingBusy = true; // 관찰과 락을 공유 (중복 실행 방지)
            string imageId = null;

            // 이미지가 있다면 (카카오톡 캡처 등) 업로드 먼저 수행
            if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                byte[] imageBytes = File.ReadAllBytes(imagePath);
                
                // 파일 전송 준비가 끝났으므로 원본 삭제 (용량 관리)
                try { File.Delete(imagePath); } catch { }

                WWWForm form = new WWWForm();
                form.AddBinaryData("file", imageBytes, "noti.jpg", "image/jpeg");

                using UnityWebRequest www = UnityWebRequest.Post(uploadImageUrl, form);
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<UploadImageResponse>(www.downloadHandler.text);
                    if (res is { ok: true })
                    {
                        imageId = res.image_id;
                    }
                }
                else
                {
                    if(SettingData.IsDebug) Debug.LogError($"[Client] Notification Image Upload Fail: {www.error}");
                }
            }

            // 업로드 하는 동안 유저가 채팅을 쳤는지 다시 확인
            if (IsUserTyping || _hasActiveAnswer || _isWaitingForChatResult)
            {
                _isObservingBusy = false;
                yield break;
            }

            // 알림 패킷 생성 및 전송
            var pkt = new Packet<NotificationPayload>
            {
                op = "notification", // 서버 백엔드에서 해당 op 처리 필요
                data = new NotificationPayload
                {
                    app_name = appName,
                    content = content,
                    image_id = imageId,
                    user_name = SettingData.UserName
                }
            };
            SendJson(pkt);

            // 안전장치 타임아웃
            if (_observeTimeoutCo != null) StopCoroutine(_observeTimeoutCo);
            _observeTimeoutCo = StartCoroutine(ReleaseBusyFlagAfterDelay(60.0f));
        }

        private IEnumerator ReleaseBusyFlagAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_isObservingBusy)
            {
                if(SettingData.IsDebug) Debug.LogWarning("[Client] 관찰/알림 응답 타임아웃. 락을 강제 해제합니다.");
                _isObservingBusy = false;
            }
            _observeTimeoutCo = null;
        }

        #endregion

        #region Public API (공개 메서드)

        /// <summary>
        /// 채팅 메시지를 서버로 전송합니다. (이미지 포함 가능) - [우선순위 1순위]
        /// </summary>
        public void SendMessageToServer(string message, bool useThink = false)
        {
            if (!IsOpen)
            {
                if (SettingData.IsDebug) Debug.LogWarning("Server not connected. Message ignored.");
                _actionManager.ErrorCharacterAction(1,false);
                return;
            }

            // [1순위 인터럽트] 히요리가 말하고 있거나 관찰/알림 분석 중이면 즉시 끊어버림
            if (_hasActiveAnswer || _isObservingBusy)
            {
                if(SettingData.IsDebug) Debug.Log("[우선순위] 유저 채팅 감지! 진행 중인 대화 및 백그라운 작업을 강제 중단합니다.");
                ForceStopResponse();
            }

            // 채팅 대기 락(Lock) 설정
            IsUserTyping = true; 
            _isWaitingForChatResult = true; 
            
            if(SettingData.IsDebug) Debug.Log("Sending message to server : " + message);
            userDateTime = NowString();
            _lastUserMessage = message;
            _actionManager.LoadingCharacterAction(3545);

            // 캡처 갤러리 이미지 확인
            List<Texture2D> imagesToSend = new List<Texture2D>();
            if (_captureGalleryManager != null)
            {
                var captured = _captureGalleryManager.GetAllTextures();
                if (captured is { Count: > 0 }) imagesToSend.AddRange(captured);
            }

            if (imagesToSend.Count > 0)
                StartCoroutine(UploadImagesAndSendChat(message, imagesToSend, useThink));
            else
                SendChatPacket(message, null, useThink);
        }

        /// <summary>
        /// 피드백을 서버로 전송합니다.
        /// </summary>
        public void SendFeedbackToServer(string feedbackMessage, int feedbackID, Action<string> onCompleted = null)
        {
            if (!IsOpen)
            {
                if (SettingData.IsDebug) Debug.LogWarning("Server not connected. Feedback ignored.");
                _actionManager.ErrorCharacterAction(1,false);
                return;
            }

            _actionManager.LoadingCharacterAction(10);
            _onFeedbackCompleted = onCompleted;
            _lastFeedbackMessage = feedbackMessage;
            _lastFeedbackId = feedbackID;

            var pkt = new Packet<FeedbackPayload>
            {
                op = "feedback",
                data = new FeedbackPayload { feedback = feedbackMessage, number = feedbackID }
            };
            SendJson(pkt);
        }
        
        /// <summary>
        /// 서버 모니터링 요청을 전송합니다.
        /// </summary>
        public void SendMonitoringToServer()
        {
            var pkt = new Packet<EmptyPayload> { op = "monitoring", data = new EmptyPayload() };
            SendJson(pkt);
        }

        /// <summary>
        /// 서버 재시작 요청을 전송합니다.
        /// </summary>
        public void SendRestartingToServer() => StartCoroutine(PostRestart());

        /// <summary>
        /// 현재 진행 중인 응답(오디오, 텍스트 등)과 백그라운드 작업을 강제로 중단합니다.
        /// </summary>
        public void ForceStopResponse()
        {
            // 기존 오디오 재생 중지
            if (_audioWaitCo != null) { StopCoroutine(_audioWaitCo); _audioSource.Stop(); _audioWaitCo = null; }
            
            // 🔥 통신 추적: 진행중인 다운로드 코루틴 및 웹 통신 강제 취소
            if (_downloadAudioCo != null) { StopCoroutine(_downloadAudioCo); _downloadAudioCo = null; }
            if (_activeAudioReq != null) { _activeAudioReq.Abort(); _activeAudioReq = null; }

            _actionManager.ActionCoroutineCheck();
            _panelManager.ResponseTextEnd(true);
            SetLive2DIdle();
            
            // 강제 중단 시 관찰/알림 타임아웃 코루틴 정리
            if (_observeTimeoutCo != null)
            {
                StopCoroutine(_observeTimeoutCo);
                _observeTimeoutCo = null;
            }

            // 모든 락(Lock) 초기화
            _hasActiveAnswer = false;
            IsUserTyping = false; 
            _isObservingBusy = false;
            _isWaitingForChatResult = false; 
        }

        #endregion

        #region Private Helpers (내부 헬퍼)

        private IEnumerator UploadImagesAndSendChat(string message, List<Texture2D> images, bool useThink)
        {
            var uploadedIds = new List<string>();
            foreach (var img in images)
            {
                if (img == null) continue;
                byte[] bytes = img.EncodeToJPG();
                WWWForm form = new WWWForm();
                form.AddBinaryData("file", bytes, "image.jpg", "image/jpeg");

                using UnityWebRequest www = UnityWebRequest.Post(uploadImageUrl, form);
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    var res = JsonUtility.FromJson<UploadImageResponse>(www.downloadHandler.text);
                    if (res is { ok: true }) uploadedIds.Add(res.image_id);
                }
            }
            if (_captureGalleryManager != null) _captureGalleryManager.ClearAll();
            
            SendChatPacket(message, uploadedIds, useThink);
        }

        private void SendChatPacket(string message, List<string> imageIds, bool useThink)
        {
            var pkt = new Packet<ChatPayload>
            {
                op = "chat",
                data = new ChatPayload
                {
                    text = message,
                    emotion = SettingData.IsEmotion,
                    image_ids = imageIds,
                    think = useThink,
                    user_birth_date = SettingData.UserBirthDate,
                    user_gender = SettingData.UserGender == 0 ? "Man" : "Woman",
                    user_name = SettingData.UserName
                }
            };
            SendJson(pkt);
            
            // 전송 완료 후 타이핑 종료 (응답 대기 상태 _isWaitingForChatResult는 유지됨)
            IsUserTyping = false;
        }

        #endregion

        #region Transport & Lifecycle (통신 및 생명주기)
        
        private void ResetTransport()
        {
            try { _ws?.Abort(); } catch { }
            try { _ws?.Dispose(); } catch { }
            _ws = null;
            try { _cts?.Cancel(); } catch { }
            try { _cts?.Dispose(); } catch { }
            _cts = null;
            _lastServerPingAt = -999f;
        }

        private IEnumerator CheckHealthAndLog()
        {
            while (_run)
            {
                using (var req = UnityWebRequest.Get(healthUrl))
                {
                    req.timeout = 3;
                    yield return req.SendWebRequest();

                    // 1. 통신 실패 (503 에러 등)
                    if (req.result != UnityWebRequest.Result.Success) 
                    {
                        if(SettingData.IsDebug) 
                            Debug.LogWarning($"[WS] health check fail ({req.responseCode}): {req.error}. 서버 준비 대기중... (1초 후 재시도)");
                
                        yield return new WaitForSeconds(1f); // 1초 대기 후 다음 루프로
                        continue; 
                    }

                    // 2. 통신 성공 -> 파싱 시도
                    string jsonResponse = req.downloadHandler.text;
                    bool isSuccess = false; // 파싱 성공 여부를 저장할 변수

                    try
                    {
                        HealthResponse healthData = JsonUtility.FromJson<HealthResponse>(jsonResponse);
        
                        if(SettingData.IsDebug) 
                            Debug.Log($"[WS] health ok. 연결된 모델: {healthData.model_name}");
                
                        // 1단계: 먼저 gemma3 (또는 gemma-3) 모델인지 확인합니다.
                        if (Regex.IsMatch(healthData.model_name, "gemma-?3", RegexOptions.IgnoreCase))
                        {
                            // 2단계: gemma3 모델 중에서도 1b 또는 270m 사이즈인지 확인합니다.
                            if (Regex.IsMatch(healthData.model_name, "1b|270m", RegexOptions.IgnoreCase))
                            {
                                if(SettingData.IsDebug) 
                                    Debug.Log($"경고! 작은 사이즈의 모델({healthData.model_name})이 감지되었습니다. 멀티모달을 비활성화합니다.");
            
                                if (SettingData.IsRealTalk)
                                {
                                    _settingController.OnClickRealTalk();
                                }

                                SettingData.CanPressedPicture = false;
                                _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.RealTalkButton].interactable = false;
                            }
                            // 3단계: gemma3 모델이지만, 1b/270m이 아닌 경우 (예: 8b, 27b 등 일반/대형 모델)
                            else
                            {
                                if(SettingData.IsDebug) 
                                    Debug.Log($"Gemma 3 일반 모델({healthData.model_name})이 감지되었습니다. 정상 작동합니다.");
            
                                // 필요하다면 이곳에 일반 Gemma 3 전용 로직을 추가할 수 있습니다.
                                SettingData.CanPressedPicture = true;
                                _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.RealTalkButton].interactable = true;
                            }

                            if (SettingData.IsThink)
                            {
                                _toggleManager.IsThink();
                            }
                            _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.ThinkButton].interactable = false;
                        }
                        else
                        {
                            SettingData.CanPressedPicture = true;
                            _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.RealTalkButton].interactable = true;
                            _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.ThinkButton].interactable = true;
                        }
                        
                        _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.MicsButton].interactable = true;
                        

                        isSuccess = true; // 에러 없이 여기까지 왔다면 성공!
                    }
                    catch (Exception e)
                    {
                        if(SettingData.IsDebug) 
                            Debug.LogWarning($"[WS] health JSON 파싱 실패: {e.Message}. 1초 후 재시도...");
                    }

                    // 3. try-catch 블록 밖에서 yield 처리
                    if (isSuccess)
                    {
                        yield break; // 완벽하게 성공했으므로 코루틴(반복) 종료
                    }
                    else
                    {
                        yield return new WaitForSeconds(1f); // 파싱 실패 시 1초 대기 후 다음 루프로
                    }
                }
            }
        }

        private async Task ConnectLoop()
        {
            float backoff = reconnectInitialDelay;
            while (_run)
            {
                if (_connecting || IsOpen) { await Task.Delay(200); continue; }
                _connecting = true;
                try
                {
                    await ConnectOnce();
                    backoff = reconnectInitialDelay;
                    _ = ReceiveLoop();
                    _ = WatchdogLoop();
            
                    _main.Enqueue(() => 
                    {
                        wsDetect.sprite = wsConnect;
                        StartCoroutine(CheckHealthAndLog());
                    });
                }
                catch
                {
                    wsDetect.sprite = wsError;
                    ResetTransport();
                    if (!autoReconnect) break;
                    await Task.Delay(TimeSpan.FromSeconds(backoff));
                    backoff = Mathf.Min(backoff * 2f, reconnectMaxDelay);
                    
                    if (_streamingSampleMic.IsRunning)
                    {
                        _streamingSampleMic.OnButtonPressed();
                    }
                    _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.MicsButton].interactable = false;
                    
                    if (SettingData.IsRealTalk)
                    {
                        _settingController.OnClickRealTalk();
                    }
                    _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.RealTalkButton].interactable = false;
                    if (SettingData.IsThink)
                    {
                        _toggleManager.IsThink();
                    }
                    _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.ThinkButton].interactable = false;
                    SettingData.CanPressedPicture = false;
                }
                finally { _connecting = false; }
            }
        }

        private async Task ConnectOnce()
        {
            ResetTransport();
            _cts = new CancellationTokenSource();
            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(connectTimeout));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);
            await _ws.ConnectAsync(new Uri(wsUrl), linked.Token);
            _main.Enqueue(() => SetServerStatusText("정상", new Color32(50, 205, 50, 255)));
        }

        private async Task ReceiveLoop()
        {
            var buf = new byte[64 * 1024];
            try
            {
                while (IsOpen && _cts is { IsCancellationRequested: false })
                {
                    var ms = new System.IO.MemoryStream();
                    WebSocketReceiveResult res;
                    do
                    {
                        res = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), _cts.Token);
                        if (res.MessageType == WebSocketMessageType.Close) { await Close("server close", hard: true); return; }
                        ms.Write(buf, 0, res.Count);
                    } while (!res.EndOfMessage);

                    if (res.MessageType == WebSocketMessageType.Text) HandleIncoming(Encoding.UTF8.GetString(ms.ToArray()));
                }
            }
            catch { await Close("recv exception", hard: true); }
        }

        private async Task WatchdogLoop()
        {
            while (IsOpen && _cts is { IsCancellationRequested: false })
            {
                if (_lastServerPingAt > 0 && (Time.realtimeSinceStartup - _lastServerPingAt) > heartbeatGrace)
                {
                    await Close("server_ping timeout", hard: true);
                    break;
                }
                await Task.Delay(250);
            }
        }

        private bool IsOpen => _ws is { State: WebSocketState.Open };

        private async Task Close(string reason, bool hard = false)
        {
            if (_closing) return;
            _closing = true;
            try
            {
                if (_ws is { State: WebSocketState.Open } && !hard)
                {
                    using var t = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, t.Token);
                }
                else _ws?.Abort();
            }
            catch { }
            finally
            {
                ResetTransport();
                _closing = false;
                _main.Enqueue(() =>
                {
                    if (!IsIntroScene() && !_hasActiveAnswer && _audioWaitCo == null)
                    {
                        SetServerStatusText("연결 끊김", Color.red);
                        _actionManager.ErrorCharacterAction(0, false);
                    }
                });
            }
        }

        #endregion

        #region Incoming Handling (프로토콜 처리)

        

        private void HandleIncoming(string json)
        {
            var op = QuickOp(json);
            switch (op)
            {
                case "server_ping":
                    _lastServerPingAt = Time.realtimeSinceStartup;
                    var pong = new Packet<ClientPongPayload> { op = "client_pong", data = new ClientPongPayload { ts = Time.time } };
                    SendJson(pong);
                    break;

                // 관찰 결과와 알림 결과는 동일한 포맷(ObserveResult)으로 처리합니다.
                case "observe_result":
                case "notification_result":
                    {
                        var res = JsonUtility.FromJson<ObserveResult>(json);
                        _main.Enqueue(() => OnObserveResult(res));
                        break;
                    }

                case "chat_result":
                    {
                        var res = JsonUtility.FromJson<ChatResult>(json);
                        _main.Enqueue(() => OnChatResult(res));
                        break;
                    }
                
                case "feedback_result":
                    var fbRes = JsonUtility.FromJson<FeedbackResult>(json);
                    _main.Enqueue(() => OnFeedbackResult(fbRes));
                    break;
                case "monitoring":
                    var monRes = JsonUtility.FromJson<MonitoringPacket>(json);
                    _main.Enqueue(() => OnMonitoring(monRes));
                    break;
                case "error":
                    if(SettingData.IsDebug) Debug.LogError(json);
                    break;
            }
        }

        private void OnObserveResult(ObserveResult res)
        {
            // 서버 응답 도달 시 타임아웃 코루틴 해제 및 락 동기화
            if (_observeTimeoutCo != null)
            {
                StopCoroutine(_observeTimeoutCo);
                _observeTimeoutCo = null;
            }
            _isObservingBusy = false;

            if (res.should_speak)
            {
                // [2순위 설움] 유저가 채팅 응답을 기다리는 중이거나 이미 대화 중이면 무시
                if (_isWaitingForChatResult || _hasActiveAnswer)
                {
                    if(SettingData.IsDebug) Debug.LogWarning("[관찰/알림 무시] 현재 유저 채팅 처리 중이거나 대화 중입니다.");
                    return;
                }

                if(SettingData.IsDebug) Debug.Log($"<color=cyan>[System 선톡]: {res.llm_response}</color> (이유: {res.reason})");

                if (IsIntroScene()) return;

                _hasActiveAnswer = true;
                _lastUserMessage = "(System Trigger)"; 

                _emotialController.UpdateLive2DExpression(string.IsNullOrEmpty(res.emotion) ? "Neutral" : res.emotion);
                _panelManager.ResponseTextProcess(res.llm_response, true);
        
                // 🔥 수정된 부분: MySQL 에러가 터져도 아래 코드가 실행되도록 예외 처리 격리
                try
                {
                    _mySQLManager.InsertLogData("System:BackgroundEvent", res.llm_response, res.emotion ?? "Neutral");
                }
                catch (Exception e)
                {
                    if (SettingData.IsDebug) Debug.LogError($"[MySQL Error] 관찰 로그 저장 실패: {e.Message}");
                }

                // 정상/에러 상관없이 답변 처리 완료 흐름(오디오 재생 등)은 무조건 실행
                HandleResponseCompletion(res.llm_response, res.audio_filename);
            }
            else
            {
                if(desktopObserver.showDebugLog) if(SettingData.IsDebug) Debug.Log($"[관찰/알림]: 할 말 없음 ({res.reason})");
            }
        }

        private void OnChatResult(ChatResult res)
        {
            if (IsIntroScene()) return;

            _isWaitingForChatResult = false; 
            if (_hasActiveAnswer) ForceStopResponse(); 

            _hasActiveAnswer = true;
            _emotialController.UpdateLive2DExpression(string.IsNullOrEmpty(res.emotion) ? "Neutral" : res.emotion);
            _panelManager.ResponseTextProcess(res.llm_response, true);
    
            // 🔥 수정된 부분: MySQL 에러가 터져도 아래 코드가 실행되도록 격리
            try
            {
                _mySQLManager.InsertLogData(_lastUserMessage, res.llm_response, res.emotion);
            }
            catch (Exception e)
            {
                if (SettingData.IsDebug) Debug.LogError($"[MySQL Error] 챗 로그 저장 실패: {e.Message}");
            }

            HandleResponseCompletion(res.llm_response, res.audio_filename);
        }

        private void OnFeedbackResult(FeedbackResult res) {
            _mySQLManager.UpdateFeedbackData(res.result, _lastFeedbackMessage, _lastFeedbackId);
            _onFeedbackCompleted?.Invoke(res.result);
            _onFeedbackCompleted = null;
        }

        private void OnMonitoring(MonitoringPacket res) {
            if (IsIntroScene()) return;
            SetLive2DIdle();
            SetServerStatusText("정상", new Color32(50, 205, 50, 255));
            if (res?.gpus is { Length: > 0 }) _saveController.OnMonitoringGuiServer(res.status ?? "success", res.gpus);
        }

        #endregion

        #region Utils (유틸리티)

        private static string QuickOp(string json)
        {
            const string key = "\"op\"";
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return null;
            int c = json.IndexOf(':', i + key.Length);
            if (c < 0) return null;
            int q1 = json.IndexOf('"', c + 1);
            if (q1 < 0) return null;
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return null;
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }

        /// <summary>
        /// 응답 처리 완료 핸들러 (오디오 재생 또는 텍스트 읽기 대기)
        /// </summary>
        private void HandleResponseCompletion(string text, string audioFilename)
        {
            if (!string.IsNullOrEmpty(audioFilename))
            {
                var audioUrl = httpAudioBase + audioFilename;
                // 🔥 추가: 기존 다운로드 코루틴이 있다면 종료 후 시작
                if (_downloadAudioCo != null) StopCoroutine(_downloadAudioCo);
                _downloadAudioCo = StartCoroutine(DownloadAndPlayAudio(audioUrl, text));
            }
            else
            {
                // 오디오 파일이 없으면 텍스트 길이에 맞춰 대기
                float duration = CalculateReadingDuration(text);
                if (_audioWaitCo != null) StopCoroutine(_audioWaitCo);
                _audioWaitCo = StartCoroutine(WaitForTextReadRoutine(duration));
            }
        }

        /// <summary>
        /// 텍스트 길이에 따른 읽기 시간 계산 (평균 읽기 속도 기반)
        /// </summary>
        private float CalculateReadingDuration(string text)
        {
            if (string.IsNullOrEmpty(text)) return 3.0f;
            return Mathf.Max(3.0f, text.Length * 0.3f + 2.0f);
        }

        /// <summary>
        /// 텍스트 읽기 시간만큼 대기 후 말풍선 닫기
        /// </summary>
        private IEnumerator WaitForTextReadRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            _panelManager.ResponseTextEnd(true);
            SetLive2DIdle();
            _hasActiveAnswer = false;
            _audioWaitCo = null;
        }

        private IEnumerator DownloadAndPlayAudio(string audioUrl, string textContent)
        {
            using var req = new UnityWebRequest(audioUrl, UnityWebRequest.kHttpVerbGET);
            _activeAudioReq = req; // 🔥 통신 추적 시작

            // 🔥 수정: streamAudio를 false로 변경! (파일이 작으므로 한 번에 받는 것이 훨씬 안전함)
            var dh = new DownloadHandlerAudioClip(audioUrl, AudioType.WAV) { streamAudio = false };
            req.downloadHandler = dh;
            
            // 🔥 수정: 오디오 다운로드 타임아웃을 600초(10분)에서 15초로 대폭 단축하여 무한 멈춤 방지
            req.timeout = 15; 

            yield return req.SendWebRequest();

            _activeAudioReq = null; // 🔥 통신 추적 해제

            if (req.result != UnityWebRequest.Result.Success) 
            {
                if(SettingData.IsDebug) Debug.LogWarning($"Audio download failed: {req.error}. Fallback to text wait.");
                float duration = CalculateReadingDuration(textContent);
                if (_audioWaitCo != null) StopCoroutine(_audioWaitCo);
                _audioWaitCo = StartCoroutine(WaitForTextReadRoutine(duration));
                yield break;
            }
            
            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null) 
            {
                float duration = CalculateReadingDuration(textContent);
                if (_audioWaitCo != null) StopCoroutine(_audioWaitCo);
                _audioWaitCo = StartCoroutine(WaitForTextReadRoutine(duration));
                yield break;
            }

            if (_audioWaitCo != null) { StopCoroutine(_audioWaitCo); _audioSource.Stop(); _audioWaitCo = null; }
            _audioSource.clip = clip;
            _audioSource.Play();
            _audioWaitCo = StartCoroutine(WaitForAudioEndRoutine(_audioSource));
            yield return _audioWaitCo;
        }

        private IEnumerator WaitForAudioEndRoutine(AudioSource audioSource)
        {
            // ✨ 안전장치: 오디오 길이보다 조금 더 긴 '최대 대기 시간' 계산
            // 만약 사운드 장치 문제로 isPlaying이 안 꺼져도 이 시간이 지나면 강제 종료됩니다.
            float maxWaitTime = audioSource.clip.length + 5.0f; 
            float startTime = Time.realtimeSinceStartup;

            while (audioSource.isPlaying)
            {
                // 장치 문제로 무한 루프에 빠지는 것을 방지
                if (Time.realtimeSinceStartup - startTime > maxWaitTime)
                {
                    if (SettingData.IsDebug) Debug.LogWarning("[Client] 오디오 재생이 비정상적으로 길어짐. 강제 종료합니다.");
                    break; 
                }
                yield return null;
            }

            yield return _wait2Sec; 
            _panelManager.ResponseTextEnd(true);
            SetLive2DIdle();
            _hasActiveAnswer = false;
            _audioWaitCo = null;
        }

        private IEnumerator PostRestart()
        {
            using var req = new UnityWebRequest(httpRestartUrl, UnityWebRequest.kHttpVerbPOST);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 60; // 모델 리로드 시간 고려
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
            {
                _actionManager.LoadingCharacterAction(0);
                SetServerStatusText("재시작중..", Color.yellow);
            }
            else
            {
                _actionManager.ErrorCharacterAction((int)req.responseCode, false);
                SetServerStatusText("오류 발생", Color.red);
            }
        }

        private IEnumerator MySqlSyncLoop()
        {
            yield return _wait3Sec; 
            while (_run)
            {
                if (!IsIntroScene() && enableMysqlSync) yield return StartCoroutine(SyncMySqlStatusToUiAsync());
                yield return _waitMysqlSync; 
            }
        }
        
        private IEnumerator SyncMySqlStatusToUiAsync()
        {
            ServerSettingData serverSettingList = null;
            _mySQLManager.SuppressUnitySideEffects = true;
            try
            {
                var task = Task.Run(async () =>
                {
                    try { return await Task.Run(() => _mySQLManager.InQuiryServerSettingData()); }
                    catch { return null; }
                });
                while (!task.IsCompleted) yield return null;
                try { serverSettingList = task.Result; } catch {}
            }
            finally { _mySQLManager.SuppressUnitySideEffects = false; }
            if (serverSettingList == null) {
                _textManager.Texts[(int)UISettingEnums.TextsEnum.MySqlStatusText].color = Color.red;
                _textManager.Texts[(int)UISettingEnums.TextsEnum.MySqlStatusText].text = "연결 실패";
            } else {
                _textManager.Texts[(int)UISettingEnums.TextsEnum.MySqlStatusText].color = new Color32(50, 205, 50, 255);
                _textManager.Texts[(int)UISettingEnums.TextsEnum.MySqlStatusText].text = "연결 성공";
            }
        }

        private bool IsIntroScene() => SceneManager.GetActiveScene().name == "IntroScene";
        
        private void SetLive2DIdle()
        {
            if (GameManager.Instance?.Live2DGameObject)
                GameManager.Instance.Live2DGameObject.GetComponent<CubismExpressionController>().CurrentExpressionIndex = (int)Live2DEnums.Live2DList.Idle;
        }
        
        private void SetServerStatusText(string text, Color color)
        {
            if (_textManager == null) return;
            _textManager.Texts[(int)UISettingEnums.TextsEnum.ServerStatusText].text = text;
            _textManager.Texts[(int)UISettingEnums.TextsEnum.ServerStatusText].color = color;
        }

        private void SendJson<T>(Packet<T> pkt)
        {
            if (!IsOpen) { if(SettingData.IsDebug) Debug.LogWarning("WS not connected"); return; }
            try
            {
                var json = JsonUtility.ToJson(pkt);
                var bytes = Encoding.UTF8.GetBytes(json);
                _ = _ws.SendAsync(bytes, WebSocketMessageType.Text, true, _cts.Token);
            }
            catch (Exception ex)
            {
                if(SettingData.IsDebug) Debug.LogWarning($"SendJson failed: {ex.Message}");
                _ = Close("send failed", hard: true);
            }
        }

        #endregion
    }
}