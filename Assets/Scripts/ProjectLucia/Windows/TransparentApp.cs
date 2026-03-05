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
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        [DllImport("user32.dll")]
        public static extern int BringWindowToTop(IntPtr hwnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy,
            uint uFlags);

        [DllImport("Dwmapi.dll")]
        public static extern uint DwmExtendFrameIntoClientArea(IntPtr hWnd, ref Margins margins);

        #endregion

        #region Constants & Fields (상수 및 필드)

        // 윈도우 핸들 및 플래그
        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private static readonly IntPtr HwndNotTopmost = new IntPtr(-2); // 최상위 해제용 핸들
        private IntPtr _hWnd;

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
                // 인트로 씬에서는 GameManager가 아직 초기화되지 않았을 수 있음
                if(SettingData.IsDebug) Debug.Log(scene.name == "IntroScene" ? $"[TransparentApp] IntroScene 예외 (무시 가능): {e.Message}" : e.StackTrace);
            }
        }

#if !UNITY_EDITOR
        void Start()
        {
            // 백그라운드 실행 허용
            Application.runInBackground = true;
        
            // 윈도우 핸들 가져오기
            _hWnd = GetActiveWindow();
        
            // 창 투명화 설정 (DWM 확장)
            Margins margins = new Margins { LeftWidth = -1 };
            DwmExtendFrameIntoClientArea(_hWnd, ref margins);
            
            // 레이어드 윈도우 설정
            SetWindowLong(_hWnd, GwlExstyle, WsExLayered);
        
            // 최상위 창으로 설정
            BringWindowToTop(_hWnd);
            SetWindowPos(_hWnd, HwndTopmost, 0, 0, 0, 0, SwpNosize);
        }
#endif

        #endregion

        #region Window Control (윈도우 제어)

        /// <summary>
        /// 현재 설정(_toggle)에 따라 윈도우의 클릭 투과 속성을 변경합니다.
        /// </summary>
        public void SetWindows()
        {
            // 창을 최상위로 유지
            BringWindowToTop(_hWnd);
            SetWindowPos(_hWnd, HwndTopmost, 0, 0, 0, 0, SwpNosize);

            // 플래그 설정 (투명 + 클릭 투과 여부)
            // _toggle이 true면 클릭 가능 (WsExLayered만)
            // _toggle이 false면 클릭 투과 (WsExLayered | WsExTransparent)
            uint flags = _toggle ? WsExLayered : (WsExLayered | WsExTransparent);
            SetWindowLong(_hWnd, GwlExstyle, flags);

            // 디버그 텍스트 업데이트 (인트로 씬 제외)
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

        /// <summary>
        /// 파일 다이얼로그 등을 띄우기 위해 윈도우 설정을 임시로 변경합니다.
        /// (최상위 속성 해제 및 클릭 가능 상태로 전환)
        /// </summary>
        public void EnableInteractionForDialog()
        {
#if !UNITY_EDITOR
            // 1. 클릭 가능하도록 투명 속성 제거 (WsExTransparent 제거)
            SetWindowLong(_hWnd, GwlExstyle, WsExLayered);

            // 2. 최상위 속성 해제 (다이얼로그가 뒤로 숨는 문제 방지)
            // HWND_NOTOPMOST = -2
            SetWindowPos(_hWnd, HwndNotTopmost, 0, 0, 0, 0, SwpNosize | SwpNomove);
#endif
        }

        /// <summary>
        /// 다이얼로그가 닫힌 후 원래 윈도우 설정으로 복구합니다.
        /// </summary>
        public void RestoreWindowSettings()
        {
#if !UNITY_EDITOR
            // 원래 설정대로 복구 (SetWindows 호출)
            SetWindows();
#endif
        }

        #endregion
    }
}