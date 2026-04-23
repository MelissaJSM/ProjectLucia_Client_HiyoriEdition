using ProjectLucia.Capture;
using ProjectLucia.Live2D;
using ProjectLucia.Server;
using ProjectLucia.Status;
using ProjectLucia.ThirdParty.Whisper;
using ProjectLucia.ThirdParty.Whisper.Runtime;
using ProjectLucia.ThirdParty.Whisper.Runtime.Utils;
using ProjectLucia.Windows;
using UnityEngine;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// 게임의 전반적인 상태와 주요 컴포넌트들을 관리하는 싱글톤 매니저 클래스입니다.
    /// 다른 클래스에서 필요한 매니저나 컨트롤러에 접근할 수 있는 중앙 허브 역할을 합니다.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        #region Singleton (싱글톤)

        /// <summary>
        /// GameManager의 싱글톤 인스턴스입니다.
        /// </summary>
        public static GameManager Instance { get; private set; }

        #endregion

        #region Live2D Components (Live2D 관련 컴포넌트)

        [Header("Live2D Objects & Settings")]
        [Tooltip("Live2D 캐릭터 모델 게임 오브젝트")]
        [SerializeField] private GameObject live2DGameObject;
        public GameObject Live2DGameObject => live2DGameObject;

        [Tooltip("Live2D 시선 추적 컴포넌트")]
        [SerializeField] private CubismLookTarget cubismLookTarget;

        [Tooltip("Live2D 히트 테스트(클릭 감지) 컴포넌트")]
        [SerializeField] private CubismHitTest cubismHitTest;

        [Tooltip("Live2D 감정 및 모션 제어 컨트롤러")]
        [SerializeField] private EmotialController emotialController;
        public EmotialController EmotialController => emotialController;

        [Tooltip("Live2D 버튼 위치 및 크기 계산 관리자")]
        [SerializeField] private Live2DButtonPosition live2DButtonPosition;
        public Live2DButtonPosition Live2DButtonPosition => live2DButtonPosition;

        #endregion

        #region UI & Input Components (UI 및 입력 관련 컴포넌트)

        [Header("UI & Input Controllers")]
        [Tooltip("사용자 입력을 처리하는 핸들러")]
        [SerializeField] InputHandler inputHandler;
        public InputHandler InputHandler => inputHandler;

        [Tooltip("UI 페이드 효과를 쉽게 사용하기 위한 래퍼 클래스")]
        [SerializeField] private FadeControllerUser fadeControllerUser;
        public FadeControllerUser FadeControllerUser => fadeControllerUser;

        [Tooltip("UI 페이드 효과를 제어하는 핵심 컨트롤러")]
        [SerializeField] private FadeController fadeController;
        public FadeController FadeController => fadeController;

        [Tooltip("마우스 위치에 따른 UI 상호작용 및 투명 윈도우 제어")]
        [SerializeField] CanvasBoundsChecker canvasBoundsChecker;
        public CanvasBoundsChecker CanvasBoundsChecker => canvasBoundsChecker;

        [Tooltip("사이드바 UI 관리자")]
        [SerializeField] private SideBarManager sideBarManager;
        public SideBarManager SideBarManager => sideBarManager;

        [Tooltip("패널 UI 관리자")]
        [SerializeField] PanelManager panelManager;
        public PanelManager PanelManager => panelManager;

        [Tooltip("텍스트 UI 관리자")]
        [SerializeField] TextManager textManager;
        public TextManager TextManager => textManager;

        [Tooltip("토글 UI 관리자")]
        [SerializeField] ToggleManager toggleManager;
        public ToggleManager ToggleManager => toggleManager;

        [Tooltip("드롭다운 UI 관리자")]
        [SerializeField] private DropdownManager dropdownManager;
        public DropdownManager DropdownManager => dropdownManager;

        [Tooltip("전반적인 설정 UI 컨트롤러")]
        [SerializeField] private SettingController settingController;
        public SettingController SettingController => settingController;

        #endregion

        #region System & Network Components (시스템 및 네트워크 관련 컴포넌트)

        [Header("System & Network")]
        [Tooltip("윈도우 투명화 및 창 관리")]
        [SerializeField] TransparentApp transparentApp;
        public TransparentApp TransparentApp => transparentApp;

        [Tooltip("서버 통신 클라이언트")]
        [SerializeField] private ServerClient serverClient;
        public ServerClient ServerClient => serverClient;

        [Tooltip("MySQL 데이터베이스 관리자")]
        [SerializeField] private MySQLManager mySQLManager;
        public MySQLManager MySQLManager => mySQLManager;

        [Tooltip("캐릭터 행동 및 상태 메시지 관리자")]
        [SerializeField] private ActionManager actionManager;
        public ActionManager ActionManager => actionManager;

        [Tooltip("설정 저장 및 로드 컨트롤러")]
        [SerializeField] private SaveController saveController;
        public SaveController SaveController => saveController;

        [Tooltip("오디오 소스 컴포넌트")]
        [SerializeField] private AudioSource audioSource;
        public AudioSource AudioSource => audioSource;

        [Tooltip("로그 기록 및 관리 컨트롤러")]
        [SerializeField] private LogController logController;
        public LogController LogController => logController;

        [Tooltip("시스템 전원 관리 컨트롤러")]
        [SerializeField] private PowerController powerController;

        [Tooltip("프로그램 종료 관리자")]
        [SerializeField] private ProgramShutdown programShutdown;
        public ProgramShutdown ProgramShutdown => programShutdown;

        [Tooltip("프로그램 재부팅 관리자")]
        [SerializeField] private ProgramRebooter programRebooter;
        public ProgramRebooter ProgramRebooter => programRebooter;

        [Tooltip("시스템 정보(GPU, VRAM 등) 확인")]
        [SerializeField] private SystemInformation systemInformation;
        public SystemInformation SystemInformation => systemInformation;

        [Tooltip("사용자 안내 및 지침 컨트롤러")]
        [SerializeField] private InstructionController instructionController;

        [Tooltip("프레임 카운터 (디버그용)")]
        [SerializeField] private FrameCounter frameCounter;
        public FrameCounter FrameCounter => frameCounter;

        [Tooltip("잠금 화면 및 보안 관리자")]
        [SerializeField] LockController lockController;
        public LockController LockController => lockController;

        [Tooltip("알림 프로그램")] [SerializeField] private AlertModelDownloader alertModelDownloader;
        public AlertModelDownloader AlertModelDownloader => alertModelDownloader;
        [SerializeField] private NotificationManager notificationManager;
        public NotificationManager NotificationManager => notificationManager;

        #endregion

        #region AI & Voice Components (AI 및 음성 관련 컴포넌트)

        [Header("AI & Voice Recognition")]
        [Tooltip("Whisper 음성 인식 관리자")]
        [SerializeField] private WhisperManager whisperManager;
        public WhisperManager WhisperManager => whisperManager;

        [Tooltip("오디오 모델 다운로더")]
        [SerializeField] private AudioModelDownloader audioModelDownloader;
        public AudioModelDownloader AudioModelDownloader => audioModelDownloader;

        [Tooltip("마이크 녹음 및 VAD 처리")]
        [SerializeField] private MicrophoneRecord microphoneRecord;
        public MicrophoneRecord MicrophoneRecord => microphoneRecord;

        [Tooltip("실시간 스트리밍 마이크 샘플 처리")]
        [SerializeField] private StreamingSampleMic streamingSampleMic;
        public StreamingSampleMic StreamingSampleMic => streamingSampleMic;
        
        
        #endregion

        #region Capture Components (캡처 관련 컴포넌트)

        [Header("Screen Capture")]
        [Tooltip("화면 캡처 오버레이 및 영역 선택")]
        [SerializeField] SnippingOverlay snippingOverlay;
        public SnippingOverlay SnippingOverlay => snippingOverlay;

        [Tooltip("캡처된 이미지 갤러리 관리자")]
        [SerializeField] CaptureGalleryManager captureGalleryManager;
        public CaptureGalleryManager CaptureGalleryManager => captureGalleryManager;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 싱글톤 패턴 구현
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // 중복된 인스턴스 제거
                return;
            }

            Instance = this; // 현재 인스턴스를 싱글톤으로 설정
            // DontDestroyOnLoad(gameObject); // 필요 시 주석 해제 (씬 전환 시 유지)
        }

        public void Start()
        {
            // 디버그용 코드 (필요 시 주석 해제하여 사용)
            // 1. 특정 키 삭제: PlayerPrefs.DeleteKey("SavedGPUName");
            // 2. 특정 값 강제 설정: PlayerPrefs.SetString("SavedGPUName", "errorData"); PlayerPrefs.Save();

            // 초기화 시작 플래그 설정
            SettingData.IsStartMode = true;

            // VAD 모델 확인 및 초기화
            microphoneRecord.CheckVadModel();

            // Live2D 모델 크기 및 위치 초기화
            live2DButtonPosition.originalLocalScale = live2DGameObject.transform.localScale;
            if(SettingData.IsDebug) Debug.Log($"로컬 사이즈 테스트 : {live2DButtonPosition.originalLocalScale}");

            // 기본 바운드 및 사이즈 계산
            Bounds originalBounds = live2DButtonPosition.CalculateBounds(live2DGameObject);
            live2DButtonPosition.originalLocalSize = originalBounds.size;
            if(SettingData.IsDebug) Debug.Log($"오리지널 사이즈 테스트 : {live2DButtonPosition.originalLocalSize}");

            // 오리지널 상태에서 비율 설정
            live2DButtonPosition.SetCharacterRatio();

            // 저장된 설정 로드 (UI, 서버 설정 등)
            saveController.LoadSettings();

            // Live2D 위치 로드 및 적용
            saveController.LoadLive2DLocation();
            live2DButtonPosition.DrawLive2dBound();

            // 잠금 상태 확인 및 적용
            // IsLockingOn: 잠금 기능 활성화 여부, IsBootingLocked: 부팅 시 잠금 여부
            lockController.LockManagement(PlayerPrefs.GetInt("IsLockingOn", 1) == 1 || PlayerPrefs.GetInt("IsBootingLocked", 0) == 1);

            // 초기화 완료 플래그 해제
            SettingData.IsStartMode = false;
        }

        #endregion
    }
}
