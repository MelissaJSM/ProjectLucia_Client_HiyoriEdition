using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking; 
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;
using ProjectLucia.GUI;
using ProjectLucia.Live2D;
using ProjectLucia.Status;
using ProjectLucia.ThirdParty.Whisper;
using ProjectLucia.ThirdParty.Whisper.Runtime;
using ProjectLucia.ThirdParty.Whisper.Runtime.Utils;
using ProjectLucia.Windows;

namespace ProjectLucia.Server
{
    /// <summary>
    /// 설정(Settings) UI의 전반적인 동작을 제어하는 컨트롤러입니다.
    /// 패널 전환, 설정 값 적용/취소, 서버 연결 설정, RealTalk 모드 전환 등을 관리합니다.
    /// </summary>
    public class SettingController : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Header("UI Components")] 
        [Tooltip("GUI 캔버스")]
        [SerializeField] Canvas guiCanvas;
        public Canvas GuiCanvas => guiCanvas;
        
        [Tooltip("포스트 프로세싱 레이어")]
        [SerializeField] private PostProcessLayer postProcessLayer;
        public PostProcessLayer PostProcessLayer => postProcessLayer;

        [Tooltip("VRAM 상태 표시 UI 프리팹")]
        [SerializeField] private GameObject vramStatus;
        public GameObject VramStatus => vramStatus;

        [Tooltip("GPU 정보 UI가 생성될 부모 Transform")]
        [SerializeField] private Transform gpuInfoParent;
        public Transform GpuInfoParent => gpuInfoParent;

        // RAG 관련 UI 오브젝트 삭제됨
        
        [Header("Buttons")]
        [Tooltip("설정 적용(확인) 버튼")]
        [SerializeField] private Button rightButton;
        public Button RightButton => rightButton;

        [Tooltip("설정 취소 버튼")]
        [SerializeField] private Button cancelButton;
        public Button CancelButton => cancelButton;

        [Tooltip("시스템 종료 버튼")]
        [SerializeField] private Button shutdownButton;
        public Button ShutDownButton => shutdownButton;

        [Tooltip("추론 버튼")]
        [SerializeField] private Button instructionButton;
        public Button InstructionButton
        {
            get => instructionButton;
            set => instructionButton = value;
        }

        [Tooltip("잠금 버튼")]
        [SerializeField] private Button lockButton;
        public Button  LockButton => lockButton;

        [Tooltip("메인 UI 버튼 리스트")]
        [SerializeField] private List<Button> mainUiButtons;
        public List<Button> MainUiButtons => mainUiButtons;

        [Tooltip("설정 탭 버튼 리스트")]
        [SerializeField] private List<Button> tabButtons;
        public List<Button> TabButtons => tabButtons;

        [Tooltip("Dimmer 처리할 UI 오브젝트 리스트")]
        [SerializeField] private List<GameObject> dimmables;

        [Header("State")]
        [Tooltip("설정창 열림 상태")]
        [SerializeField] private bool settingsOpen;
        public bool SettingsOpen => settingsOpen;

        [Header("RealTalk UI")]
        [Tooltip("RealTalk 모드 버튼")]
        [SerializeField] private Button realTalkButton;
        public Button RealTalkButton
        {
            get => realTalkButton;
            set => realTalkButton = value;
        }

        private Image realTalkImage;
        
        // RAG 관련 필드 삭제됨

        [Tooltip("RealTalk 버튼 스프라이트 리스트 (Normal)")]
        [SerializeField] private List<Sprite> realTalkImageList;
        [Tooltip("RealTalk 버튼 스프라이트 리스트 (Highlighted)")]
        [SerializeField] private List<Sprite> realTalkImageFocusList;
        [Tooltip("RealTalk 버튼 스프라이트 리스트 (Pressed)")]
        [SerializeField] private List<Sprite> realTalkImageClickList;

        // RAG 관련 스프라이트 리스트 삭제됨
        
        [Header("Other UI")]
        [Tooltip("폰트 크기 조절 UI 오브젝트")]
        [SerializeField] private GameObject fontSizeObject;
        public GameObject FontSizeObject => fontSizeObject;

        [Tooltip("생년월일 표시 텍스트")]
        [SerializeField] private Text birthDateText; 
        public Text BirthDateText => birthDateText;

        #endregion

        #region Public Properties & Enums (공개 속성 및 열거형)

        public bool cycleStatus;

        // RAGEnum 및 SetRAG 삭제됨

        #endregion

        #region Private Fields (비공개 필드)

        private bool _viewPassword;
        private bool _isRecordingMemory;
        private bool _monitoringScheduled;

        // 매니저 참조
        private PanelManager _panelManager;
        private LogController _logController;
        private SaveController _saveController;
        private Live2DButtonPosition _live2DButtonPosition;
        private SideBarManager _sideBarManager;
        private ToggleManager _toggleManager;
        private DropdownManager _dropdownManager;
        private MicrophoneRecord _microphoneRecord;
        private StreamingSampleMic _streamingSampleMic;
        private ServerClient _serverClient;
        private AudioModelDownloader _audioModelDownloader;
        private WhisperManager _whisperManager;
        private InputHandler _inputHandler;
        private MySQLManager _mysqlManager;
        private TextManager _textManager;
        private ActionManager _actionManager;
        private EmotialController _emotialController;
        private SystemInformation _systemInformation;
        private AlertModelDownloader _alertModelDownloader;
        // RAG 관련 필드 삭제됨
        // private KeywordModelDownloader _keywordModelDownloader;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 매니저 참조 가져오기
            _panelManager = GameManager.Instance.PanelManager;
            _logController = GameManager.Instance.LogController;
            _saveController = GameManager.Instance.SaveController;
            _live2DButtonPosition = GameManager.Instance.Live2DButtonPosition;
            _sideBarManager = GameManager.Instance.SideBarManager;
            _toggleManager = GameManager.Instance.ToggleManager;
            _dropdownManager = GameManager.Instance.DropdownManager;
            _microphoneRecord = GameManager.Instance.MicrophoneRecord;
            _streamingSampleMic = GameManager.Instance.StreamingSampleMic;
            _serverClient = GameManager.Instance.ServerClient;
            _audioModelDownloader = GameManager.Instance.AudioModelDownloader;
            _whisperManager = GameManager.Instance.WhisperManager;
            _inputHandler = GameManager.Instance.InputHandler;
            _mysqlManager = GameManager.Instance.MySQLManager;
            _textManager = GameManager.Instance.TextManager;
            _actionManager = GameManager.Instance.ActionManager;
            _emotialController = GameManager.Instance.EmotialController;
            _systemInformation  = GameManager.Instance.SystemInformation;
            _alertModelDownloader = GameManager.Instance.AlertModelDownloader;
        }

        public void Update()
        {
            // 다운로드 중 UI 잠금 처리
            _panelManager?.DataDownLoadLock(true);
        }

        private void OnDestroy()
        {
            OnStopServerStatus();
            Resources.UnloadUnusedAssets();
        }

        #endregion

        #region Settings Panel Control (설정창 제어)

        /// <summary>
        /// 설정창을 열거나 닫습니다.
        /// </summary>
        public void OnSettings()
        {
            settingsOpen = !settingsOpen;

            if (settingsOpen)
            {
                // 로그 패널이 열려있으면 닫기
                if (_panelManager.Panels[(int)UISettingEnums.PanelsEnum.LOGPanel].activeSelf)
                {
                    _panelManager.Panels[(int)UISettingEnums.PanelsEnum.LOGPanel].gameObject.SetActive(false);
                    _logController.DeleteAllprefabs();
                    _logController.LogsOpen = false;
                }
                else
                {
                    VoiceRecordChecking(true); // 음성 녹음 일시 중지
                }
                
                // 설정창 열기 및 초기화
                _panelManager.Panels[(int)UISettingEnums.PanelsEnum.SettingPanel].SetActive(true);
                _live2DButtonPosition.DrawLive2dBound();
                _saveController.LoadSettings();
                _panelManager.TabUpdate((int)UISettingEnums.TabsEnum.Live2D);
                
                // mainUiButtons[(int)UISettingEnums.MainUiButtonEnum.LogsButton].interactable = false;
                RightButton.interactable = true;
                TabInteractable((int)UISettingEnums.TabsEnum.Live2D);

                // 대화 및 모션 중단
                _serverClient.ForceStopResponse();
                _actionManager.StopIdleMotion();
                _emotialController.StopMotion();

                CheckDimmer();
                _actionManager.AnnounceCharacterAction(0); // 설정 진입 모션
            }
            else
            {
                // 설정창 닫기 (저장 안 함)
                _panelManager.Panels[(int)UISettingEnums.PanelsEnum.SettingPanel].SetActive(false);
                CallSettingData(false); // 기존 값 복구
                mainUiButtons[(int)UISettingEnums.MainUiButtonEnum.LogsButton].interactable = true;

                if (_panelManager.Panels[(int)UISettingEnums.PanelsEnum.TalikngPanel].activeSelf)
                {
                    _panelManager.ResponseTextEnd(true);
                }

                VoiceRecordChecking(false); // 음성 녹음 재개
                OnStopServerStatus();
                _actionManager.AnnounceCharacterAction(2); // 설정 취소 모션
            }
        }

        /// <summary>
        /// 설정을 적용하고 창을 닫습니다.
        /// </summary>
        public void OnSettingsApply()
        {
            settingsOpen = false;
            _panelManager.Panels[(int)UISettingEnums.PanelsEnum.SettingPanel].SetActive(false);
            CallSettingData(true); // 현재 값 저장
            OnStopServerStatus();
            mainUiButtons[(int)UISettingEnums.MainUiButtonEnum.LogsButton].interactable = true;
            VoiceRecordChecking(false); 
            _inputHandler.SetUserName(true);
            if (_panelManager.Panels[(int)UISettingEnums.PanelsEnum.TalikngPanel].activeSelf)
            {
                _panelManager.ResponseTextEnd(true);
            }
            _actionManager.AnnounceCharacterAction(1); // 설정 완료 모션
        }

        /// <summary>
        /// 설정을 취소하고 창을 닫습니다.
        /// </summary>
        /// <param name="isLog">로그창에서 호출되었는지 여부</param>
        public void OnSettingsCancel(bool isLog)
        {
            settingsOpen = false;
            _panelManager.Panels[(int)UISettingEnums.PanelsEnum.SettingPanel].SetActive(false);
            CallSettingData(false); // 기존 값 복구
            OnStopServerStatus();
            mainUiButtons[(int)UISettingEnums.MainUiButtonEnum.LogsButton].interactable = true;
            if (!isLog)
            {
                VoiceRecordChecking(false); 
            }
            
            if (_panelManager.Panels[(int)UISettingEnums.PanelsEnum.TalikngPanel].activeSelf)
            {
                _panelManager.ResponseTextEnd(true);
            }

            if (!_panelManager.Panels[(int)UISettingEnums.PanelsEnum.LockPanel].gameObject.activeSelf)
            {
                _actionManager.AnnounceCharacterAction(2); // 설정 취소 모션
            }
        }

        #endregion

        #region Data Management (데이터 관리)

        /// <summary>
        /// 설정 데이터를 저장하거나 로드합니다.
        /// </summary>
        /// <param name="isTrue">true: 현재 UI 값을 저장, false: 저장된 값을 로드</param>
        private void CallSettingData(bool isTrue)
        {
            if (isTrue)
            {
                // --- 저장 로직 ---
                
                // Live2D
                SettingData.Live2DSizeValue = _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.Live2dSizeScrollbar].value;
                SettingData.IsInputKeyboard = _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsKeyboard].isOn;
                SettingData.IsLookTarget = _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsMouseChaser].isOn;
                SettingData.L2dClicked = _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsTouchMotion].isOn;
                SettingData.IsIdleMotion = _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsIdleMotion].isOn;
                SettingData.IsIdleMotionRandom = _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsIdleMotionRandom].isOn;

                // Idle Motion
                SettingData.IdleMotionRandomMax = _sideBarManager.GetIdleMotionRandomMax01();
                SettingData.IdleMotionRandomMin = _sideBarManager.GetIdleMotionRandomMin01();
                SettingData.IdleMotionFixed = _sideBarManager.GetIdleMotionFixed01();
                
                // Font & Pixel
                SettingData.TalkingFontSize = _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.TalkFontScrollbar].value;
                var pixelDropdown = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.RealtimePixel];
                SettingData.RealTalkPixel = pixelDropdown.options[pixelDropdown.value].text;

                // DesktopObserver
                SettingData.CheckIntervalValue = _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.CheckIntervalScrollbar].value;
                SettingData.ChangeThresholdValue = _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.ChangeThresholdScrollbar].value;
                SettingData.StabilityDurationValue = _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.StabilityDurationScrollbar].value;
                SettingData.MinSendIntervalValue = _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.MinSendIntervalScrollbar].value;

                // User Info
                if (birthDateText != null) SettingData.UserBirthDate = birthDateText.text;
                SettingData.BubbleTextValue = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.BubbleText].value;
                SettingData.UserGender = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.UserGender].value;

                // Graphic
                SettingData.PresetIndex = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.PresetDropDown].value;
                if (SettingData.PresetIndex == (int)UISettingEnums.PresetListEnum.Custom)
                {
                    // 커스텀 설정 저장
                    SettingData.IsVsync = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.VSync].value;
                    SettingData.AntiIndex = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.AntiDropDown].value;
                    SettingData.IsMsaaQuality = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.MSAAQuality].value;
                    SettingData.IsFxaaFastMode = _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsFXAAFastMode].isOn;
                    SettingData.IsFxaaAlphaKeep = _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsFXAAAlphaKeep].isOn;
                    SettingData.IsDynamicResolution = _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsDynamicResolution].isOn;
                    SettingData.IsSmaaQuality = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.SmaaQuality].value;
                    SettingData.IsJitterSpread = _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.JitterSpreadScrollbar].value;
                    SettingData.IsStaionaryBlending = _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.StaionaryBlendingScrollbar].value;
                    SettingData.IsMotionBlending = _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.MotionBlendingScrollbar].value;
                    SettingData.IsSharpness = _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.SharpnessScrollbar].value;
                    SettingData.IsAnisotropicFiltering = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.AnisotropicFiltering].value;
                    SettingData.IsMipMap = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.MipMap].value;
                }

                // 공용 그래픽 설정
                SettingData.IsFrameLimit = _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.FrameLimitScrollbar].value;
                SettingData.IsDynamicResolution = _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsDynamicResolution].isOn;
                SettingData.IsRenderingPath = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.RenderingPath].value;
                SettingData.IsIconFade = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.IconFade].value;

                // Voice 
                SettingData.SetWhisperModel = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.WhisperModel].value;
                if (SettingData.IsExistedVad)
                {
                    SettingData.MicDevice = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.MicDevice]
                        .options[_dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.MicDevice].value].text;
                }
                SettingData.DefaultModel = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.DefaultModel].value;
                SettingData.IsCallNow = _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsCallNow].isOn;
                SettingData.CallName = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.CallName].text;
                SettingData.SimilarityThreshold = _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.SimilarityThresholdScrollbar].value;

                // Etc
                SettingData.IsDebug = _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsDebug].isOn;
                SettingData.IsEmotion = _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsEmotion].isOn;
                SettingData.AlertNotification = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.Notification].value;
                
                _saveController.SaveSettings();
                
                // 상태 갱신
                _toggleManager.IsIdleMotionController(false);
                if (_microphoneRecord.isRecording)
                {
                    _streamingSampleMic.OnRestart();
                }
            }
            else
            {
                // --- 로드 로직 ---
                _saveController.LoadSettings();
                
                if (birthDateText != null)
                {
                    birthDateText.text = SettingData.UserBirthDate;
                }
            }
            
            _live2DButtonPosition.DrawLive2dBound();
        }

        #endregion

        #region Tab Control (탭 제어)

        public void OnTabLive2D()
        {
            OnStopServerStatus();
            _panelManager.TabUpdate((int)UISettingEnums.TabsEnum.Live2D);
            cycleStatus = false;
            OkButton(!cycleStatus);
            TabInteractable((int)UISettingEnums.TabsEnum.Live2D);
        }

        public void OnTabGrapic()
        {
            OnStopServerStatus();
            _panelManager.TabUpdate((int)UISettingEnums.TabsEnum.Graphic);
            cycleStatus = false;
            OkButton(!cycleStatus);
            TabInteractable((int)UISettingEnums.TabsEnum.Graphic);
        }

        public void OnTabServer()
        {
            _panelManager.TabUpdate((int)UISettingEnums.TabsEnum.Server);

            if (!_monitoringScheduled)
            {
                _monitoringScheduled = true;
                InvokeRepeating(nameof(SendMonitoringRequest), 0f, 5f);
            }

            cycleStatus = true;
            OkButton(!cycleStatus);
            TabInteractable((int)UISettingEnums.TabsEnum.Server);
        }

        public void OnTabVoice()
        {
            OnStopServerStatus();

            if (SettingData.IsMatchedGPU)
            {
                _audioModelDownloader.VoiceList.SetActive(true);
                _systemInformation.GpuCheck.SetActive(false);
                _systemInformation.SystemModelVramCheck(_dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.WhisperModel].value);
                _audioModelDownloader.VadDownloadScreen.SetActive(!SettingData.IsExistedVad);
                _audioModelDownloader.VadDownloadStatus.SetActive(SettingData.IsExistedVad);
            }
            else if (!SettingData.IsMatchedGPU && string.IsNullOrEmpty(SettingData.ResultGPUName))
            {
                _audioModelDownloader.VoiceList.SetActive(false);
                _systemInformation.GpuCheck.SetActive(true);
                _textManager.Texts[(int)UISettingEnums.TextsEnum.GPUCheckSubject].text = "GPU 초기 매칭";
                _textManager.Texts[(int)UISettingEnums.TextsEnum.GPUCheckAnnounce].text = "프로그램 초기 실행 시 그래픽카드 체크가 필요합니다.";
                _textManager.Texts[(int)UISettingEnums.TextsEnum.GPUCheckButton].text = "매칭 시작";
            }
            else
            {
                _audioModelDownloader.VoiceList.SetActive(false);
                _systemInformation.GpuCheck.SetActive(true);
                _textManager.Texts[(int)UISettingEnums.TextsEnum.GPUCheckSubject].text = "GPU 매칭 실패";
                _textManager.Texts[(int)UISettingEnums.TextsEnum.GPUCheckAnnounce].text = "그래픽카드의 변경이 일어난걸로 파악됩니다.\n다시체크 해주세요.";
                _textManager.Texts[(int)UISettingEnums.TextsEnum.GPUCheckButton].text = "다시 매칭";
            }

            _panelManager.TabUpdate((int)UISettingEnums.TabsEnum.Voice);
            cycleStatus = false;
            OkButton(!cycleStatus);
            TabInteractable((int)UISettingEnums.TabsEnum.Voice);
        }

        public void OnTabEtc()
        {
            OnStopServerStatus();
            // RAG 관련 다운로드 화면 로직 삭제됨
            // _keywordModelDownloader.KeywordDownloadScreen.SetActive(!SettingData.IsExistKeyword);
            
            SettingData.IsExistedAlert = _alertModelDownloader.IsExistedAlertFind();
            _alertModelDownloader.AlertDownloadScreen.SetActive(!SettingData.IsExistedAlert);
            _alertModelDownloader.AlertDownloadStatus.SetActive(SettingData.IsExistedAlert);
            
            _panelManager.TabUpdate((int)UISettingEnums.TabsEnum.Etc);
            cycleStatus = false; 
            
            
            OkButton(!cycleStatus);
            TabInteractable((int)UISettingEnums.TabsEnum.Etc);
        }

        #endregion

        #region Helper Methods (보조 메서드)

        public void SendMonitoringRequest()
        {
            _serverClient.SendMonitoringToServer();
        }

        private void OkButton(bool result)
        {
            _panelManager.Panels[(int)UISettingEnums.PanelsEnum.SettingPanel].transform.Find("Ok").gameObject
                .GetComponent<Button>().interactable = result;
        }

        private void TabInteractable(int value)
        {
            if(SettingData.IsDebug) Debug.Log($"탭 밸류값 : {value}");
            var list = TabButtons;
            for (int i = 0, n = list.Count; i < n; i++)
            {
                list[i].interactable = i != value;
            }
        }

        public void VoiceRecordChecking(bool isOpening)
        {
            if (isOpening)
            {
                if (_microphoneRecord.isRecording)
                {
                    _isRecordingMemory = true;
                    _streamingSampleMic.OnButtonPressed(); 
                }
                else
                {
                    _isRecordingMemory = false; 
                }
            }
            else
            {
                if (_isRecordingMemory)
                {
                    _streamingSampleMic.OnButtonPressed(); 
                    _isRecordingMemory = false; 
                }
            }
        }

        public void WhisperModelInquiry()
        {
            StartCoroutine(CoWhisperModelInquiry());
        }

        private IEnumerator CoWhisperModelInquiry()
        {
            string whisperFolder = Path.Combine(Application.streamingAssetsPath, "Whisper");
            if (!Directory.Exists(whisperFolder))
            {
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.LogWarning("📁 StreamingAssets/Whisper 폴더가 존재하지 않습니다.");
                yield break;
            }

            var task = Task.Run(() =>
            {
                var arr = Directory.GetFiles(whisperFolder);
                var hs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in arr)
                {
                    if (f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                    hs.Add(Path.GetFileName(f)); 
                }
                return hs;
            });

            while (!task.IsCompleted) yield return null;
            var files = task.Result;

            _whisperManager.availableModelIndices.Clear();

            var dropdown = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.WhisperModel];
            for (int i = 0; i < dropdown.options.Count; i++)
            {
                string optionText = dropdown.options[i].text;
                string file1 = optionText;
                string file2 = optionText.Replace(".bin", "-q8.bin");
                string file3 = optionText.Replace(".bin", "-q5.bin");

                if (files.Contains(file1) && files.Contains(file2) && files.Contains(file3))
                {
                    _whisperManager.availableModelIndices.Add(i);
                    if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"✅ 설치됨: {optionText}");
                }
                else
                {
                    if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"❌ 미설치됨: {optionText}");
                }
            }
        }

        public void OnClickMyIP(bool isServer)
        {
            StartCoroutine(CoFetchPublicIp(isServer));
        }

        private IEnumerator CoFetchPublicIp(bool isServer)
        {
            TMP_InputField inputIP = isServer
                ? _inputHandler.Inputs[(int)UISettingEnums.InputEnum.ServerIP]
                : _inputHandler.Inputs[(int)UISettingEnums.InputEnum.SqlIP];

            inputIP.text = "조회중..";

            using var req = UnityWebRequest.Get("https://api.ipify.org");
            req.timeout = 5; 
            yield return req.SendWebRequest();

            inputIP.text = req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : "조회실패";
        }

        public void OnClickViewPassword()
        {
            _viewPassword = !_viewPassword;

            var inputField = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.SqlPassword];
            inputField.inputType = _viewPassword ? TMP_InputField.InputType.Standard : TMP_InputField.InputType.Password;

            inputField.ForceLabelUpdate();
            inputField.ActivateInputField();
        }

        // OnClickRAG 메서드 삭제됨

        public void OnClickRealTalk()
        {
            SettingData.IsRealTalk = !SettingData.IsRealTalk;
            bool isReal = SettingData.IsRealTalk;

            if(SettingData.IsDebug) Debug.Log($"리얼톡 모드 {(isReal ? "활성화" : "비활성화")}");

            int index = isReal ? 1 : 0;

            realTalkButton.image.sprite = realTalkImageList[index];
            realTalkImage.sprite = realTalkImageList[index];

            SpriteState newSpriteState = realTalkButton.spriteState;
            newSpriteState.highlightedSprite = realTalkImageFocusList[index];
            newSpriteState.pressedSprite = realTalkImageClickList[index];
    
            realTalkButton.spriteState = newSpriteState;
        }

        public void OnStopServerStatus()
        {
            if (_monitoringScheduled)
            {
                CancelInvoke(nameof(SendMonitoringRequest));
                _monitoringScheduled = false;
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log("모니터링 반복작업 종료");
            }
        }

        public void MySqlApplySettings(bool isApply)
        {
            _inputHandler.SetMysqlIP(isApply);
            _inputHandler.SetdatabaseName(isApply);
            _inputHandler.SetsqlUserName(isApply);
            _inputHandler.SetsqlPassword(isApply);
            _inputHandler.SetsqlPort(isApply);
        }
        
        public void ServerSettingApply(bool isApply)
        {
            _inputHandler.SetserverIP(isApply);
            _inputHandler.SetserverPort(isApply);
            _serverClient.BuildUrlsFromSettingData();
        }

        public void ServerLlmSettingApply()
        {
            _mysqlManager.ReplaceServerSettingData(); 
            _serverClient.SendRestartingToServer();
        }

        public void CheckDimmer()
        {
            if (dimmables == null) return;
            foreach (var obj in dimmables)
            {
                if (obj != null && obj.activeSelf)
                {
                    obj.SetActive(false);
                }
            }
        }

        public void CheckMainUiButton(bool isLocked)
        {
            foreach (var uiData in mainUiButtons)
            {
                uiData.interactable = !isLocked;
            }
        }

        #endregion
    }
}