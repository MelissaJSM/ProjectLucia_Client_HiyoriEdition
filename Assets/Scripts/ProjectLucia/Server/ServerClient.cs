using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Live2D.Cubism.Framework.Expression;
using ProjectLucia.Capture;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 프로젝트 매니저들 (기존 유지)
using ProjectLucia.GUI;
using ProjectLucia.Live2D;
using ProjectLucia.Status;
using ProjectLucia.Windows;

// Status의 GpuInfo를 직접 사용
using StatusGpuInfo = ProjectLucia.Status.GpuInfo;
// ReSharper disable EmptyGeneralCatchClause
// ReSharper disable InconsistentNaming
// ReSharper disable AsyncVoidMethod

namespace ProjectLucia.Server
{
    /// <summary>
    /// 서버와의 통신(WebSocket, HTTP)을 담당하는 클라이언트 클래스입니다.
    /// 채팅, RAG, 피드백, 오디오 재생, 화면 관찰(Observer) 기능을 통합 관리합니다.
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

        // UI 및 상태 제어
        private bool _hasActiveAnswer;
        private static bool _healthOnce;

        // 관찰 기능 제어 플래그
        private bool _isObservingBusy; // 이미지 처리 중 중복 방지

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
            
            if (!_healthOnce)
            {
                _healthOnce = true;
                StartCoroutine(CheckHealthAndLog());
            }

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

        #region Observer Logic (관찰 기능 로직)

        /// <summary>
        /// 화면 변화 감지 시 이미지를 업로드하고 관찰 패킷을 전송하는 코루틴입니다.
        /// </summary>
        private IEnumerator ProcessObserveRoutine(byte[] imageBytes)
        {
            // 방어 코드: 유저가 타이핑 중이거나, AI가 답변 중이거나, 이미 이미지 처리 중이면 스킵
            if (IsUserTyping || _hasActiveAnswer || _isObservingBusy)
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
            if (!string.IsNullOrEmpty(imageId) && !IsUserTyping && !_hasActiveAnswer)
            {
                if (desktopObserver.showDebugLog) if(SettingData.IsDebug) Debug.Log($"[Client] Sending Observe Packet ({imageId})");
                
                var pkt = new Packet<ObservePayload>
                {
                    op = "observe",
                    data = new ObservePayload { image_id = imageId }
                };
                SendJson(pkt);

                // 안전장치: 10초 뒤에는 무조건 잠금 해제 (서버 응답 누락 대비)
                StartCoroutine(ReleaseBusyFlagAfterDelay(10.0f));
            }
            else
            {
                _isObservingBusy = false;
            }
        }

        private IEnumerator ReleaseBusyFlagAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_isObservingBusy)
            {
                _isObservingBusy = false;
            }
        }

        #endregion

        #region Public API (공개 메서드)

        /// <summary>
        /// 채팅 메시지를 서버로 전송합니다. (이미지 포함 가능)
        /// </summary>
        public void SendMessageToServer(string message)
        {
            // 채팅 시작 시 관찰 잠금 해제 및 타이핑 상태 설정
            IsUserTyping = true; 
            
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
                StartCoroutine(UploadImagesAndSendChat(message, imagesToSend));
            else
                SendChatPacket(message, null);
        }

        /// <summary>
        /// RAG(검색 증강 생성) 요청을 서버로 전송합니다.
        /// </summary>
        public void SendRAGToServer(string message, string keywords)
        {
            if(SettingData.IsDebug) Debug.Log($"rag sending: {message} / {keywords}");
            userDateTime = NowString();
            _lastUserMessage = message;
            _actionManager.LoadingCharacterAction(3545);

            var pkt = new Packet<RagPayload>
            {
                op = "rag",
                data = new RagPayload { text = message, keywords = keywords }
            };
            SendJson(pkt);
        }

        /// <summary>
        /// 피드백을 서버로 전송합니다.
        /// </summary>
        public void SendFeedbackToServer(string feedbackMessage, int feedbackID, Action<string> onCompleted = null)
        {
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
        /// 현재 진행 중인 응답(오디오, 텍스트 등)을 강제로 중단합니다.
        /// </summary>
        public void ForceStopResponse()
        {
            if (_audioWaitCo != null) { StopCoroutine(_audioWaitCo); _audioSource.Stop(); _audioWaitCo = null; }
            _actionManager.ActionCoroutineCheck();
            _panelManager.ResponseTextEnd(true);
            SetLive2DIdle();
            _hasActiveAnswer = false;
            
            // 강제 중단 시 채팅 상태 및 관찰 잠금 초기화
            IsUserTyping = false; 
            _isObservingBusy = false;
        }

        #endregion

        #region Private Helpers (내부 헬퍼)

        private IEnumerator UploadImagesAndSendChat(string message, List<Texture2D> images)
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
            SendChatPacket(message, uploadedIds);
        }

        private void SendChatPacket(string message, List<string> imageIds)
        {
            var pkt = new Packet<ChatPayload>
            {
                op = "chat",
                data = new ChatPayload
                {
                    text = message,
                    emotion = SettingData.IsEmotion,
                    image_ids = imageIds,
                    
                    // User Info
                    user_birth_date = SettingData.UserBirthDate,
                    user_gender = SettingData.UserGender == 0 ? "Man" : "Woman",
                    user_name = SettingData.UserName
                }
            };
            SendJson(pkt);
            
            // 전송 완료 후 타이핑 종료
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
            try
            {
                if (string.IsNullOrEmpty(wsUrl)) throw new UriFormatException("WS URL is empty");
                _ = new Uri(wsUrl); 

                wsDetect.sprite = wsLoading;
            }
            catch 
            {
                wsDetect.sprite = wsError;
                yield break;
            }

            using var req = UnityWebRequest.Get(healthUrl);
            req.timeout = 3;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) if(SettingData.IsDebug) Debug.LogWarning($"[WS] health check fail");
            else if(SettingData.IsDebug) Debug.Log("[WS] health ok");
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
                    wsDetect.sprite = wsConnect;
                }
                catch
                {
                    wsDetect.sprite = wsError;
                    ResetTransport();
                    if (!autoReconnect) break;
                    await Task.Delay(TimeSpan.FromSeconds(backoff));
                    backoff = Mathf.Min(backoff * 2f, reconnectMaxDelay);
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

        // [DTO Definition]
        [Serializable] private class ObservePayload { 
            // ReSharper disable once NotAccessedField.Local
            public string image_id; 
        }
        [Serializable] private class ObserveResult 
        { 
            public string op; 
            public bool should_speak; 
            public string llm_response; 
            public string reason;
            public string emotion;        
            public string audio_filename; 
        }
        [Serializable] private class ChatResult { public string op; public string llm_response; public string emotion; public string audio_filename; }
        [Serializable] private class RAGResult { public string op; public string keywords; public string answer; public string audio_filename; }
        [Serializable] private class FeedbackResult { public string op; public string result; }
        [Serializable] private class MonitoringPacket { public string op; public string status; public StatusGpuInfo[] gpus; }
        [Serializable] private class UploadImageResponse { public bool ok; public string image_id; } 

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

                case "observe_result":
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

                case "rag_result":
                    {
                        var res = JsonUtility.FromJson<RAGResult>(json);
                        _main.Enqueue(() => OnRagResult(res));
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
            _isObservingBusy = false;

            if (res.should_speak)
            {
                if(SettingData.IsDebug) Debug.Log($"<color=cyan>[선톡]: {res.llm_response}</color> (이유: {res.reason})");

                if (IsIntroScene()) return;

                _hasActiveAnswer = true;
                _lastUserMessage = "(Screen Trigger)"; 

                _emotialController.UpdateLive2DExpression(string.IsNullOrEmpty(res.emotion) ? "Neutral" : res.emotion);
                _panelManager.ResponseTextProcess(res.llm_response, true);
                _mySQLManager.InsertLogData("System:ScreenObservation", res.llm_response, res.emotion ?? "Neutral");

                HandleResponseCompletion(res.llm_response, res.audio_filename);
            }
            else
            {
                if(desktopObserver.showDebugLog) if(SettingData.IsDebug) Debug.Log($"[관찰]: 할 말 없음 ({res.reason})");
            }
        }

        private void OnChatResult(ChatResult res)
        {
            if (IsIntroScene()) return;
            _hasActiveAnswer = true;
            _emotialController.UpdateLive2DExpression(string.IsNullOrEmpty(res.emotion) ? "Neutral" : res.emotion);
            _panelManager.ResponseTextProcess(res.llm_response, true);
            _mySQLManager.InsertLogData(_lastUserMessage, res.llm_response, res.emotion);
            HandleResponseCompletion(res.llm_response, res.audio_filename);
        }

        private void OnRagResult(RAGResult res) {
            if (IsIntroScene()) return;
            _hasActiveAnswer = true;
            _emotialController.UpdateLive2DExpression("Neutral");
            _panelManager.ResponseTextProcess(res.answer, true);
            _mySQLManager.InsertLogData(_lastUserMessage, res.answer, "Neutral");
            HandleResponseCompletion(res.answer, res.audio_filename);
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
                StartCoroutine(DownloadAndPlayAudio(audioUrl, text));
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
            // 한글 평균 읽기 속도 고려 (초당 5~6글자) + 여유 시간
            // 0.2s per char + 2.0s base delay
            return Mathf.Max(3.0f, text.Length * 0.2f + 2.0f);
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
            var dh = new DownloadHandlerAudioClip(audioUrl, AudioType.WAV) { streamAudio = true };
            req.downloadHandler = dh;
            req.timeout = Mathf.Max(1, requestTimeout);
            yield return req.SendWebRequest();
            
            if (req.result != UnityWebRequest.Result.Success) 
            {
                // 오디오 다운로드 실패 시 텍스트 대기로 전환
                if(SettingData.IsDebug) Debug.LogWarning($"Audio download failed: {req.error}. Fallback to text wait.");
                float duration = CalculateReadingDuration(textContent);
                if (_audioWaitCo != null) StopCoroutine(_audioWaitCo);
                _audioWaitCo = StartCoroutine(WaitForTextReadRoutine(duration));
                yield break;
            }
            
            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null) 
            {
                // 오디오 클립 로드 실패 시 텍스트 대기로 전환
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
            while (audioSource.isPlaying) yield return null;
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
            req.timeout = 10;
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