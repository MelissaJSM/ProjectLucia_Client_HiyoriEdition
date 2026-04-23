using System;
using System.Runtime.InteropServices;
using UnityEngine;
using ProjectLucia.GUI;
using ProjectLucia.Status;
using UnityEngine.SceneManagement;

namespace ProjectLucia.Windows
{
    /// <summary>
    /// Windows API를 사용하여 Unity 창을 투명하게 만들고, 마우스 클릭 투과(Click-through) 기능을 제어하는 클래스입니다.
    /// </summary>
    public class TransparentApp : MonoBehaviour
    {
        #region Win32 API Definitions (Win32 API 정의)

        [StructLayout(LayoutKind.Sequential)]
        public struct Margins
        {
            public int LeftWidth;
            public int RightWidth;
            public int TopHeight;
            public int BottomHeight;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll")]
        public static extern int BringWindowToTop(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("Dwmapi.dll")]
        public static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins margins);

        // 🔥 [핵심 추가] 창을 숨기기 위한 ShowWindow API
        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        #endregion

        #region Constants & Fields (상수 및 필드)

        // 윈도우 핸들 및 플래그
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private static readonly IntPtr HwndNotTopmost = new IntPtr(-2); // 최상위 해제용 핸들
        private IntPtr _hWnd;
        private uint _originalStyle; // 앱 시작 전의 원래 윈도우 스타일 백업

        /// <summary>
        /// 현재 윈도우 핸들
        /// </summary>
        public IntPtr HWnd
        {
            get => _hWnd;
            set => _hWnd = value;
        }

        private const uint SwpNosize = 0x0001;
        private const uint SwpNomove = 0x0002;
        public const int GwlExstyle = -20;
        public const uint WsExLayered = 0x00080000;
        public const uint WsExTransparent = 0x00000020; 
        
        // 🔥 [핵심 추가] 창 숨김 명령어 상수
        private const int SwHide = 0; 

        // 상태 변수
        private bool _toggle = true;

        /// <summary>
        /// 클릭 투과 여부 (true: 클릭 가능, false: 클릭 투과)
        /// </summary>
        public bool Toggle
        {
            get => _toggle;
            set => _toggle = value;
        }

        private TextManager _textManager;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            try
            {
                _textManager = GameManager.Instance.TextManager;
            }
            catch (Exception e)
            {
                var scene = SceneManager.GetActiveScene();
                if(SettingData.IsDebug) Debug.Log(scene.name == "IntroScene" ? $"[TransparentApp] IntroScene 예외 (무시 가능): {e.Message}" : e.StackTrace);
            }
        }

#if !UNITY_EDITOR
        void Start()
        {
            Application.runInBackground = true;
        
            _hWnd = GetActiveWindow();
        
            // 창 속성을 변경하기 전에 원래 상태 백업
            _originalStyle = GetWindowLong(_hWnd, GwlExstyle);

            int screenWidth = Display.main.systemWidth; 
            int screenHeight = Display.main.systemHeight;

            Margins margins = new Margins { LeftWidth = -1 };
            DwmExtendFrameIntoClientArea(_hWnd, ref margins);
            
            SetWindowLong(_hWnd, GwlExstyle, WsExLayered);
        
            SetWindowPos(_hWnd, HwndTopmost, 0, 0, screenWidth, screenHeight, 0);

            Screen.SetResolution(screenWidth, screenHeight, FullScreenMode.FullScreenWindow);
        }

        // 🔥 [핵심 수정] 앱 종료 시 창을 먼저 숨긴 후 안전하게 스타일 복원
        void OnApplicationQuit()
        {
            if (_hWnd != IntPtr.Zero)
            {
                // 1. 유니티가 꺼지기 전에 화면에서 창을 먼저 지워버림 (검은 화면 깜빡임 차단)
                ShowWindow(_hWnd, SwHide);

                // 2. 보이지 않는 상태에서 안전하게 원래 윈도우 스타일로 복구 (투명 해제)
                SetWindowLong(_hWnd, GwlExstyle, _originalStyle);

                // 3. DWM 확장 프레임 초기화 (전체 영역 확장 취소)
                Margins margins = new Margins { LeftWidth = 0, RightWidth = 0, TopHeight = 0, BottomHeight = 0 };
                DwmExtendFrameIntoClientArea(_hWnd, ref margins);

                // 4. 최상위(Topmost) 속성 해제
                SetWindowPos(_hWnd, HwndNotTopmost, 0, 0, 0, 0, SwpNosize | SwpNomove);
            }
        }
#endif

        #endregion

        #region Window Control (윈도우 제어)

        public void SetWindows()
        {
            BringWindowToTop(_hWnd);
            SetWindowPos(_hWnd, HwndTopmost, 0, 0, 0, 0, SwpNosize);

            uint flags = _toggle ? WsExLayered : (WsExLayered | WsExTransparent);
            SetWindowLong(_hWnd, GwlExstyle, flags);

            if (SceneManager.GetActiveScene().name != "IntroScene" && _textManager != null)
            {
                string statusText = _toggle ? "프로그램 집중 모드" : "윈도우 집중 모드";
                if (_textManager.Texts != null && _textManager.Texts.Count > (int)UISettingEnums.TextsEnum.DebugText)
                {
                    _textManager.Texts[(int)UISettingEnums.TextsEnum.DebugText].text = statusText;
                }
            }
        }

        #endregion

        #region Dialog Interaction Helpers (다이얼로그 상호작용 헬퍼)

        public void EnableInteractionForDialog()
        {
#if !UNITY_EDITOR
            SetWindowLong(_hWnd, GwlExstyle, WsExLayered);
            SetWindowPos(_hWnd, HwndNotTopmost, 0, 0, 0, 0, SwpNosize | SwpNomove);
#endif
        }

        public void RestoreWindowSettings()
        {
#if !UNITY_EDITOR
            SetWindows();
#endif
        }

        #endregion
    }
}