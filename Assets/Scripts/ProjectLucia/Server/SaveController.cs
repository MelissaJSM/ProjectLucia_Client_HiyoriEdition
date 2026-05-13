using UnityEngine;
using ProjectLucia.GUI;
using ProjectLucia.Live2D;
using ProjectLucia.Status;
using ProjectLucia.Windows;
using Debug = UnityEngine.Debug;

namespace ProjectLucia.Server
{
    /// <summary>
    /// 애플리케이션의 설정(Settings)을 저장하고 로드하는 컨트롤러입니다.
    /// PlayerPrefs를 사용하여 데이터를 영구 저장하며, UI 및 시스템 상태를 동기화합니다.
    /// </summary>
    public class SaveController : MonoBehaviour
    {
        #region Private Fields (비공개 필드)

        // 매니저 참조
        private SideBarManager _sideBarManager;
        private ToggleManager _toggleManager;
        private SettingController _settingController;
        private DropdownManager _dropdownManager;
        private TextManager _textManager;
        private GameManager _gameManager;
        private Live2DButtonPosition _live2DButtonPosition;
        private SystemInformation _systemInformation;
        private LockController _lockController;
        private InputHandler _inputHandler;
        private PanelManager _panelManager;

        // 메인 카메라 캐시
        private Camera _mainCam;

        // GPU UI 오브젝트 풀링
        private readonly System.Collections.Generic.List<GpuVramItemUI> _gpuPool
            = new System.Collections.Generic.List<GpuVramItemUI>();

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 매니저 참조 가져오기
            _sideBarManager       = GameManager.Instance.SideBarManager;
            _toggleManager        = GameManager.Instance.ToggleManager;
            _settingController    = GameManager.Instance.SettingController;
            _dropdownManager      = GameManager.Instance.DropdownManager;
            _textManager          = GameManager.Instance.TextManager;
            _gameManager          = GameManager.Instance;
            _live2DButtonPosition = GameManager.Instance.Live2DButtonPosition;
            _systemInformation    = GameManager.Instance.SystemInformation;
            _lockController       = GameManager.Instance.LockController;
            _inputHandler         = GameManager.Instance.InputHandler;
            _panelManager         = GameManager.Instance.PanelManager;
            
            _mainCam = Camera.main; 
        }

        #endregion

        #region Save & Load Settings (설정 저장 및 로드)

        /// <summary>
        /// 현재 설정값들을 PlayerPrefs에 저장합니다.
        /// </summary>
        public void SaveSettings()
        {
            // Live2D 설정 저장
            PlayerPrefs.SetFloat("Live2DSizeValue", SettingData.Live2DSizeValue);
            PlayerPrefs.SetInt("IsInputKeyboard",   SettingData.IsInputKeyboard ? 1 : 0);
            PlayerPrefs.SetInt("IsMouseChaser",     SettingData.IsLookTarget ? 1 : 0);
            PlayerPrefs.SetInt("IsTouchMotion",     SettingData.L2dClicked ? 1 : 0);
            PlayerPrefs.SetInt("IsIdleMotion",      SettingData.IsIdleMotion ? 1 : 0);
            PlayerPrefs.SetInt("IsIdleMotionRandom",SettingData.IsIdleMotionRandom ? 1 : 0);
            PlayerPrefs.SetInt("IsThinkBalloon", SettingData.IsThinkBalloon ? 1 : 0);

            if (SettingData.IsIdleMotionRandom)
            {
                PlayerPrefs.SetFloat("IdleMotionRandomMax", SettingData.IdleMotionRandomMax);
                PlayerPrefs.SetFloat("IdleMotionRandomMin", SettingData.IdleMotionRandomMin);
            }
            else
            {
                PlayerPrefs.SetFloat("IdleMotionFixed", SettingData.IdleMotionFixed);
            }

            PlayerPrefs.SetFloat("talkingFontSize",  SettingData.TalkingFontSize);
            PlayerPrefs.SetInt("BubbleTextValue",    SettingData.BubbleTextValue);
            PlayerPrefs.SetString("RealTalkPixel",   SettingData.RealTalkPixel);

            // DesktopObserver 설정 저장
            PlayerPrefs.SetFloat("CheckInterval",      SettingData.CheckIntervalValue);
            PlayerPrefs.SetFloat("ChangeThreshold",    SettingData.ChangeThresholdValue);
            PlayerPrefs.SetFloat("StabilityDuration",  SettingData.StabilityDurationValue);
            PlayerPrefs.SetFloat("MinSendInterval",    SettingData.MinSendIntervalValue);

            // 사용자 정보 저장
            PlayerPrefs.SetString("UserBirthDate", SettingData.UserBirthDate);
            PlayerPrefs.SetInt("UserGender", SettingData.UserGender);

            // 그래픽 설정 저장 (커스텀 프리셋일 경우 세부 설정 저장)
            if (SettingData.PresetIndex == (int)UISettingEnums.PresetListEnum.Custom)
            {
                PlayerPrefs.SetInt("antiIndex",            SettingData.AntiIndex);
                PlayerPrefs.SetInt("Vsync",                SettingData.IsVsync);
                PlayerPrefs.SetInt("FXAAFastMode",         SettingData.IsFxaaFastMode ? 1 : 0);
                PlayerPrefs.SetInt("FXAAAlphaKeep",        SettingData.IsFxaaAlphaKeep ? 1 : 0);
                PlayerPrefs.SetFloat("JitterSpread",       SettingData.IsJitterSpread);
                PlayerPrefs.SetFloat("StaionaryBlending",  SettingData.IsStaionaryBlending);
                PlayerPrefs.SetFloat("MotionBlending",     SettingData.IsMotionBlending);
                PlayerPrefs.SetFloat("Sharpness",          SettingData.IsSharpness);
                PlayerPrefs.SetInt("MsaaQuality",          SettingData.IsMsaaQuality);
                PlayerPrefs.SetInt("SmaaQuality",          SettingData.IsSmaaQuality);
                PlayerPrefs.SetInt("AnisotropicFiltering", SettingData.IsAnisotropicFiltering);
                PlayerPrefs.SetInt("MipMap",               SettingData.IsMipMap);
            }

            PlayerPrefs.SetInt("presetIndex",       SettingData.PresetIndex);
            PlayerPrefs.SetFloat("FrameLimit",      SettingData.IsFrameLimit);
            PlayerPrefs.SetInt("DynamicResolution", SettingData.IsDynamicResolution ? 1 : 0);
            PlayerPrefs.SetInt("RenderingPath",     SettingData.IsRenderingPath);
            PlayerPrefs.SetInt("IconFade",          SettingData.IsIconFade);

            // 음성 설정 저장
            PlayerPrefs.SetInt("IsWhisperModel", SettingData.SetWhisperModel);
            PlayerPrefs.SetString("MicDevice",   SettingData.MicDevice);
            PlayerPrefs.SetInt("defaultModel",   SettingData.DefaultModel);
            PlayerPrefs.SetString("SavedGPUName",SettingData.ResultGPUName);
            PlayerPrefs.SetString("WhisperQuantization",SettingData.WhisperQuantization);
            PlayerPrefs.SetInt("IsCallNow", SettingData.IsCallNow ? 1 : 0);
            PlayerPrefs.SetFloat("SimilarityThreshold", SettingData.SimilarityThreshold);

            // 기타 설정 저장
            PlayerPrefs.SetInt("IsDebug",   SettingData.IsDebug ? 1 : 0);
            PlayerPrefs.SetInt("IsEmotion", SettingData.IsEmotion ? 1 : 0);
            PlayerPrefs.SetInt("IsAlertNotification", SettingData.AlertNotification);
            

            PlayerPrefs.Save();

            if(SettingData.IsDebug) Debug.Log("설정이 저장되었습니다.");
        }

        /// <summary>
        /// 저장된 설정을 로드하고 UI 및 시스템에 반영합니다.
        /// </summary>
        public void LoadSettings()
        {
            if (!SettingData.IsStartMode)
            {
                GetPlayerPref();
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log("start 가 아니라서 로드했습니다.");
            }

            // 각 파트별 설정 적용
            OnLive2DGUI();
            OnServer();
            OnGraphic(false);
            OnVoice();
            OnEtc();

            // 다운로드 동의 상태 초기화
            _toggleManager.IsSetDownloadAgree((int)UISettingEnums.DownloadEnums.All);
        }

        /// <summary>
        /// PlayerPrefs에서 값을 읽어와 SettingData에 저장합니다.
        /// </summary>
        public static void GetPlayerPref()
        {
            // Live2D
            SettingData.Live2DSizeValue   = PlayerPrefs.GetFloat("Live2DSizeValue", 0);
            SettingData.L2dClicked        = PlayerPrefs.GetInt("IsTouchMotion", 1) == 1;
            SettingData.IsIdleMotion      = PlayerPrefs.GetInt("IsIdleMotion", 1) == 1;
            SettingData.IsIdleMotionRandom= PlayerPrefs.GetInt("IsIdleMotionRandom", 1) == 1;
            SettingData.IdleMotionRandomMax = PlayerPrefs.GetFloat("IdleMotionRandomMax", 0);
            SettingData.IdleMotionRandomMin = PlayerPrefs.GetFloat("IdleMotionRandomMin", 0);
            SettingData.IdleMotionFixed     = PlayerPrefs.GetFloat("IdleMotionFixed", 0);
            SettingData.BubbleTextValue     = PlayerPrefs.GetInt("BubbleTextValue", 2);
            SettingData.RealTalkPixel       = PlayerPrefs.GetString("RealTalkPixel", "Low");
            SettingData.IsThinkBalloon = PlayerPrefs.GetInt("IsThinkBalloon", 1) == 1;

            // DesktopObserver (기본값 설정)
            SettingData.CheckIntervalValue     = PlayerPrefs.GetFloat("CheckInterval", 0f);
            SettingData.ChangeThresholdValue   = PlayerPrefs.GetFloat("ChangeThreshold", Mathf.InverseLerp(1f, 100f, 3f));
            SettingData.StabilityDurationValue = PlayerPrefs.GetFloat("StabilityDuration", 0f);
            SettingData.MinSendIntervalValue   = PlayerPrefs.GetFloat("MinSendInterval", Mathf.InverseLerp(1f, 60f, 10f));

            // 사용자 정보
            SettingData.UserBirthDate = PlayerPrefs.GetString("UserBirthDate", "2000-01-01");
            SettingData.UserGender = PlayerPrefs.GetInt("UserGender", 0);
            SettingData.UserName = PlayerPrefs.GetString("UserName", "");
            
            SettingData.IsInputKeyboard  = PlayerPrefs.GetInt("IsInputKeyboard", 1) == 1;
            SettingData.IsLookTarget     = PlayerPrefs.GetInt("IsMouseChaser", 1) == 1;
            SettingData.TalkingFontSize  = PlayerPrefs.GetFloat("talkingFontSize", 0.3f);

            // 그래픽
            SettingData.PresetIndex      = PlayerPrefs.GetInt("presetIndex", 3);
            SettingData.IsFrameLimit     = PlayerPrefs.GetFloat("FrameLimit", 0.3f);
            SettingData.IsDynamicResolution = PlayerPrefs.GetInt("DynamicResolution", 1) == 1;
            SettingData.IsRenderingPath     = PlayerPrefs.GetInt("RenderingPath", 1);

            SettingData.AntiIndex            = PlayerPrefs.GetInt("antiIndex", 0);
            SettingData.IsVsync              = PlayerPrefs.GetInt("Vsync", 1);
            SettingData.IsFxaaFastMode       = PlayerPrefs.GetInt("FXAAFastMode", 1) == 1;
            SettingData.IsFxaaAlphaKeep      = PlayerPrefs.GetInt("FXAAAlphaKeep", 1) == 1;
            SettingData.IsJitterSpread       = PlayerPrefs.GetFloat("JitterSpread", 1);
            SettingData.IsStaionaryBlending  = PlayerPrefs.GetFloat("StaionaryBlending", 1);
            SettingData.IsMotionBlending     = PlayerPrefs.GetFloat("MotionBlending", 1);
            SettingData.IsSharpness          = PlayerPrefs.GetFloat("Sharpness", 1);
            SettingData.IsMsaaQuality        = PlayerPrefs.GetInt("MsaaQuality", 1);
            SettingData.IsSmaaQuality        = PlayerPrefs.GetInt("SmaaQuality", 1);
            SettingData.IsAnisotropicFiltering = PlayerPrefs.GetInt("AnisotropicFiltering", 1);
            SettingData.IsMipMap             = PlayerPrefs.GetInt("MipMap", 1);
            SettingData.IsIconFade           = PlayerPrefs.GetInt("IconFade", 1);

            // 음성
            SettingData.SetWhisperModel = PlayerPrefs.GetInt("IsWhisperModel", 0);
            SettingData.MicDevice       = PlayerPrefs.GetString("MicDevice", "default");
            SettingData.DefaultModel    = PlayerPrefs.GetInt("defaultModel", 2);
            SettingData.SavedGPUName    = PlayerPrefs.GetString("SavedGPUName", "");
            SettingData.WhisperQuantization = PlayerPrefs.GetString("WhisperQuantization", "");
            SettingData.IsCallNow = PlayerPrefs.GetInt("IsCallNow", 0) == 1;
            SettingData.CallName = PlayerPrefs.GetString("CallName", "모모세 히요리");
            SettingData.SimilarityThreshold = PlayerPrefs.GetFloat("SimilarityThreshold", 0.7f);
            

            // 기타
            SettingData.IsDebug   = PlayerPrefs.GetInt("IsDebug", 0) == 1;
            SettingData.IsEmotion = PlayerPrefs.GetInt("IsEmotion", 0) == 1;
            SettingData.AlertNotification = PlayerPrefs.GetInt("IsAlertNotification", 0);

            // 서버 연결 정보
            SettingData.ServerIP     = PlayerPrefs.GetString("SetserverIP", "");
            SettingData.DatabaseName = PlayerPrefs.GetString("SetdatabaseName", "");
            SettingData.SqlUserName  = PlayerPrefs.GetString("SetsqlUserName", "");
            SettingData.SqlPassword  = PlayerPrefs.GetString("SetsqlPassword", "");
            SettingData.SqlPort      = PlayerPrefs.GetString("SetsqlPort", "");
            SettingData.ServerPort   = PlayerPrefs.GetString("SetserverPort", "");
            SettingData.MySqlIp      = PlayerPrefs.GetString("SetMySqlIP", "");
        }

        /// <summary>
        /// 모든 설정을 초기화하고 기본값으로 로드합니다.
        /// </summary>
        public void ResetSettings()
        {
            PlayerPrefs.DeleteAll();
            LoadSettings();
            if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log("설정이 초기화되었습니다.");
        }

        #endregion

        #region Apply Settings (설정 적용)

        private void OnLive2DGUI()
        {
            _sideBarManager.ScrollbarController(false);

            _toggleManager.IsKeyboardInputController(false);
            _toggleManager.IsMouseChaserController(false);
            _toggleManager.IsTouchMotionController(false);
            _toggleManager.IsThinkBalloon(false);

            _sideBarManager.SetTalkFontSize(false);
            _sideBarManager.SetIdleMotionRandomMax(false);
            _sideBarManager.SetIdleMotionRandomMin(false);
            _sideBarManager.SetIdleMotionFixed(false);
            _toggleManager.IsIdleMotionRandomController(false);
            _toggleManager.IsIdleMotionController(false);

            _dropdownManager.SetBubbleText(false);
            _dropdownManager.SetRealtimePixel(false);
            _dropdownManager.SetUserGender(false);
            _inputHandler.SetUserName(false);
            _panelManager.SetTalkCharacterName();
            
            // DesktopObserver
            _sideBarManager.SetCheckInterval(false);
            _sideBarManager.SetChangeThreshold(false);
            _sideBarManager.SetStabilityDuration(false);
            _sideBarManager.SetMinSendInterval(false);

            // Calendar
            if (_settingController.BirthDateText != null)
            {
                _settingController.BirthDateText.text = SettingData.UserBirthDate;
            }
        }

        private void OnVoice()
        {
            _systemInformation.SystemInformationStart();
            _settingController.WhisperModelInquiry();
            _dropdownManager.SetWhisperModel(false);
            _dropdownManager.SetMicDevice(false);
            _dropdownManager.SetDefaultLanguage(false);
            _dropdownManager.SetWhisperQuantization(false);
            _toggleManager.IsWakeUpController(false);
            _sideBarManager.SetSimilarityThreshold(false);
        }

        private void OnServer()
        {
            _settingController.MySqlApplySettings(false);
            _settingController.ServerSettingApply(false);
        }

        private void OnEtc()
        {
            _toggleManager.IsDebugController(false);
            _toggleManager.IsEmotion(false);
            _lockController.LockBootController(false);
            _dropdownManager.SetAlertNotification(false);
        }

        /// <summary>
        /// 그래픽 설정을 적용합니다.
        /// </summary>
        /// <param name="isClick">UI 클릭 여부 (true: UI 값 적용, false: 저장된 값 적용)</param>
        public void OnGraphic(bool isClick)
        {
            bool isNotCustom;

            if (!isClick)
            {
                _dropdownManager.DropdownController(false);
            }

            _sideBarManager.SetFrameLimitSideBar(false);

            // 프리셋에 따른 그래픽 옵션 자동 설정
            if (_dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.PresetDropDown].value !=
                (int)UISettingEnums.PresetListEnum.Custom)
            {
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"퀄리티 레벨 : {QualitySettings.GetQualityLevel()}");

                _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.VSync].value = QualitySettings.vSyncCount;

                switch (QualitySettings.GetQualityLevel())
                {
                    case (int)UISettingEnums.PresetListEnum.VeryLow:
                        _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.AntiDropDown].value =
                            (int)UISettingEnums.AntiListEnum.None;
                        break;

                    case (int)UISettingEnums.PresetListEnum.Low:
                        _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.AntiDropDown].value =
                            (int)UISettingEnums.AntiListEnum.FXAA;
                        _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsFXAAFastMode].isOn = true;
                        _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsFXAAAlphaKeep].isOn = true;
                        _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsDynamicResolution].isOn = true;
                        _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.RenderingPath].value =
                            (int)UISettingEnums.RenderingPathEnum.VertexLit;
                        break;

                    case (int)UISettingEnums.PresetListEnum.Medium:
                        _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.AntiDropDown].value =
                            (int)UISettingEnums.AntiListEnum.SMAA;
                        _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.SmaaQuality].value =
                            (int)UISettingEnums.SMAAEnum.Medium;
                        _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsDynamicResolution].isOn = true;
                        _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.RenderingPath].value =
                            (int)UISettingEnums.RenderingPathEnum.Forward;
                        break;

                    case (int)UISettingEnums.PresetListEnum.High:
                        _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.AntiDropDown].value =
                            (int)UISettingEnums.AntiListEnum.MSAA;
                        _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsDynamicResolution].isOn = false;
                        _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.RenderingPath].value =
                            (int)UISettingEnums.RenderingPathEnum.DeferredShading;
                        break;

                    case (int)UISettingEnums.PresetListEnum.VeryHigh:
                        _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.AntiDropDown].value =
                            (int)UISettingEnums.AntiListEnum.SMAA;
                        _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.SmaaQuality].value =
                            (int)UISettingEnums.SMAAEnum.High;
                        _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsDynamicResolution].isOn = false;
                        _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.RenderingPath].value =
                            (int)UISettingEnums.RenderingPathEnum.DeferredShading;
                        break;

                    case (int)UISettingEnums.PresetListEnum.Ultra:
                        _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.AntiDropDown].value =
                            (int)UISettingEnums.AntiListEnum.MSAA;
                        _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsDynamicResolution].isOn = false;
                        _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.RenderingPath].value =
                            (int)UISettingEnums.RenderingPathEnum.DeferredShading;
                        break;
                }

                _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.AnisotropicFiltering].value =
                    (int)QualitySettings.anisotropicFiltering;
                _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.MipMap].value =
                    QualitySettings.globalTextureMipmapLimit;

                isNotCustom = true;
            }
            else
            {
                if (SettingData.IsDebug)
                {
                    if(SettingData.IsDebug) Debug.Log($"퀄리티 레벨 : {QualitySettings.GetQualityLevel()}");
                    if(SettingData.IsDebug) Debug.Log("커스텀 모드 진입");
                }
                isNotCustom = false;
            }

            // 세부 설정 UI 활성화/비활성화
            _dropdownManager.SetVsync(isNotCustom);
            _dropdownManager.SetAntiUI(isNotCustom);
            _dropdownManager.SetMsaaQuality(isNotCustom);
            _toggleManager.IsSetFXAAFastMode(isNotCustom);
            _toggleManager.IsSetFXAAKeepAlpha(isNotCustom);
            _dropdownManager.SetSMAAQuality(isNotCustom);
            _sideBarManager.SetTAAJitterSpreadSideBar(isNotCustom);
            _sideBarManager.SetTAAStationaryBlendingSideBar(isNotCustom);
            _sideBarManager.SetTAAMotionBlendingSideBar(isNotCustom);
            _sideBarManager.SetTAASharpnessSideBar(isNotCustom);
            _dropdownManager.SetAnisotropicFlitering(isNotCustom);
            _dropdownManager.SetMipMap(isNotCustom);
            _toggleManager.IsSetDynamicResolution(isNotCustom);
            _dropdownManager.SetRenderingPath(isNotCustom);

            _dropdownManager.SetIconFade(false);
        }

        #endregion

        #region Monitoring & Location (모니터링 및 위치)

        /// <summary>
        /// 서버 모니터링 정보를 UI에 업데이트합니다. (GPU 정보 포함)
        /// </summary>
        public void OnMonitoringGuiServer(string status, GpuInfo[] gpus)
        {
            _textManager.Texts[(int)UISettingEnums.TextsEnum.ServerStatusText].text = status;
            if (gpus == null) return;

            // GPU UI 풀링 처리
            for (int i = _gpuPool.Count; i < gpus.Length; i++)
            {
                var go = Instantiate(_settingController.VramStatus, _settingController.GpuInfoParent);
                var item = go.GetComponent<GpuVramItemUI>();
                if (item == null)
                {
                    item = go.AddComponent<GpuVramItemUI>();
                }
                _gpuPool.Add(item);
            }

            for (int i = 0; i < _gpuPool.Count; i++)
            {
                bool active = i < gpus.Length;
                var item = _gpuPool[i];
                if (item != null && item.gameObject.activeSelf != active)
                    item.gameObject.SetActive(active);
            }

            for (int i = gpus.Length - 1; i >= 0; i--)
            {
                int poolIndex = gpus.Length - 1 - i;
                var ui = _gpuPool[poolIndex];
                if (ui == null) continue;
                
                ui.transform.SetSiblingIndex(7);
                ui.UpdateView(gpus[i]);
            }
        }

        public void OnMonitoringGuiSql(int memory, bool memoryTime)
        {
            // SQL 모니터링 UI 업데이트 (현재 미사용)
        }

        /// <summary>
        /// Live2D 모델의 현재 위치를 저장합니다.
        /// </summary>
        public void SaveLive2DLocation(Vector3 live2dLimitBounds)
        {
            PlayerPrefs.SetFloat("Live2D_X", live2dLimitBounds.x);
            PlayerPrefs.SetFloat("Live2D_Y", live2dLimitBounds.y);
            PlayerPrefs.SetFloat("Live2D_Z", live2dLimitBounds.z);
            if (SettingData.IsDebug)
                if(SettingData.IsDebug) Debug.Log($"위치 저장완료 {live2dLimitBounds.x} : {live2dLimitBounds.y} : {live2dLimitBounds.z} ");
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 저장된 Live2D 모델의 위치를 로드하고 적용합니다.
        /// 화면 밖으로 나갔을 경우 중앙으로 복구합니다.
        /// </summary>
        public void LoadLive2DLocation()
        {
            var cam = _mainCam != null ? _mainCam : Camera.main;
            if (cam == null)
            {
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.LogWarning("Main Camera를 찾을 수 없습니다. 위치 로드를 건너뜁니다.");
                return;
            }

            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 2000f);
            Vector3 worldCenter  = cam.ScreenToWorldPoint(screenCenter);

            float x = PlayerPrefs.GetFloat("Live2D_X", worldCenter.x);
            float y = PlayerPrefs.GetFloat("Live2D_Y", worldCenter.y);
            float z = PlayerPrefs.GetFloat("Live2D_Z", worldCenter.z);

            if (SettingData.IsDebug)
                if(SettingData.IsDebug) Debug.Log($"위치 불러오기 완료 : {worldCenter} / {x} : {y} : {z} ");

            _gameManager.Live2DGameObject.transform.position = new Vector3(x, y, z);

            var boundsCenter = _live2DButtonPosition.CalculateBoundsCenter(_gameManager.Live2DGameObject);
            var screenResult = cam.WorldToScreenPoint(boundsCenter);

            bool outOfScreen = (screenResult.x < 0 || screenResult.x > Screen.width ||
                                screenResult.y < 0 || screenResult.y > Screen.height);

            if (outOfScreen)
            {
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log("화면 넘어감 → 중심으로 복구");
                _gameManager.Live2DGameObject.transform.position = worldCenter;
            }
        }

        #endregion
    }
}
