#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
// ReSharper disable InconsistentNaming

namespace ProjectLucia.Capture
{
    /// <summary>
    /// Windows 전역 핫키(Global Hotkey)를 감지하는 클래스입니다.
    /// 별도의 스레드를 생성하여 Win32 메시지 루프를 돌며 키 입력을 감지합니다.
    /// (Pause 키, Shift + Pause 키 감지)
    /// </summary>
    public class GlobalHotkey : MonoBehaviour
    {
        #region Public Static Flags (공개 상태 플래그)

        /// <summary>
        /// Pause 키가 눌렸는지 여부 (다른 스크립트에서 확인 후 false로 초기화 필요)
        /// </summary>
        public static volatile bool PausePressed;

        /// <summary>
        /// Shift + Pause 키가 눌렸는지 여부 (다른 스크립트에서 확인 후 false로 초기화 필요)
        /// </summary>
        public static volatile bool ShiftPausePressed;

        #endregion

        #region Win32 Constants & Structures (Win32 상수 및 구조체)

        // Win32 상수
        const uint MOD_SHIFT = 0x0001;
        const uint VK_PAUSE  = 0x13;
        const uint WM_HOTKEY = 0x0312;
        const uint WM_QUIT   = 0x0012;

        // 핫키 ID
        const int ID_PAUSE       = 1;
        const int ID_SHIFT_PAUSE = 2;

        [StructLayout(LayoutKind.Sequential)]
        struct MSG
        {
            public IntPtr hwnd;
            public uint   message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint   time;
            public POINT  pt;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT
        {
            public int x;
            public int y;
        }

        #endregion

        #region Win32 P/Invoke Declarations (Win32 API 선언)

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage(ref MSG lpMsg);

        [DllImport("kernel32.dll")]
        static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);

        #endregion

        #region Private Fields (비공개 필드)

        /// <summary>
        /// 핫키 감지를 위한 백그라운드 스레드
        /// </summary>
        Thread _hotkeyThread;

        /// <summary>
        /// 스레드 실행 여부 플래그
        /// </summary>
        volatile bool _running;

        /// <summary>
        /// 생성된 스레드의 ID (메시지 전송용)
        /// </summary>
        uint _threadId;

        /// <summary>
        /// 스레드 초기화 완료 대기용 이벤트
        /// </summary>
        readonly AutoResetEvent _threadReady = new AutoResetEvent(false);

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        void Awake()
        {
            // 부모가 있다면 해제하여 루트 오브젝트로 만듦 (DontDestroyOnLoad 경고 방지)
            if (transform.parent != null)
            {
                transform.SetParent(null);
            }
            // 씬 전환 시에도 파괴되지 않도록 설정
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable()
        {
            // 이미 스레드가 실행 중이면 중복 실행 방지
            if (_hotkeyThread is { IsAlive: true })
                return;

            _running = true;
            _hotkeyThread = new Thread(HotkeyLoop)
            {
                IsBackground = true // 메인 프로세스 종료 시 함께 종료되도록 설정
            };
            _hotkeyThread.Start();

            // 스레드에서 threadId 세팅할 때까지 대기 (동기화)
            _threadReady.WaitOne();
        }

        void OnDisable()
        {
            StopHotkeyThread();
        }

        void OnApplicationQuit()
        {
            StopHotkeyThread();
        }

        #endregion

        #region Thread Management (스레드 관리)

        /// <summary>
        /// 핫키 감지 스레드를 안전하게 종료합니다.
        /// </summary>
        void StopHotkeyThread()
        {
            _running = false;

            if (_threadId != 0)
            {
                // GetMessage 루프를 깨우기 위해 WM_QUIT 메시지 전송
                PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            }

            try
            {
                // 스레드가 종료될 때까지 최대 200ms 대기
                if (_hotkeyThread is { IsAlive: true })
                    _hotkeyThread.Join(200);
            }
            catch (Exception)
            {
                // 스레드 종료 중 예외 발생 시 무시
            }

            _threadId = 0;
            _hotkeyThread = null;
        }

        /// <summary>
        /// 별도 스레드에서 실행되는 핫키 감지 루프입니다.
        /// Win32 메시지 펌프를 돌리며 WM_HOTKEY 메시지를 처리합니다.
        /// </summary>
        void HotkeyLoop()
        {
            // 현재 스레드 ID 저장 (종료 시 메시지 전송용)
            _threadId = GetCurrentThreadId();
            
            // 메인 스레드에 초기화 완료 신호 전송
            _threadReady.Set();

            // 주의: Unity API는 메인 스레드가 아니므로 여기서 호출 불가 (if(SettingData.IsDebug) Debug.Log 등 사용 금지)

            // 핫키 등록
            RegisterHotKey(IntPtr.Zero, ID_PAUSE,       0,        VK_PAUSE);
            RegisterHotKey(IntPtr.Zero, ID_SHIFT_PAUSE, MOD_SHIFT, VK_PAUSE);

            try
            {
                while (true)
                {
                    // 메시지 대기 (블로킹)
                    int ret = GetMessage(out var msg, IntPtr.Zero, 0, 0);
                    
                    // WM_QUIT 수신 또는 에러 시 루프 종료
                    if (ret == 0 || msg.message == WM_QUIT)
                    {
                        break;
                    }

                    if (!_running)
                        continue;

                    // 핫키 메시지 처리
                    if (msg.message == WM_HOTKEY)
                    {
                        int id = msg.wParam.ToInt32();
                        if (id == ID_PAUSE)
                        {
                            PausePressed = true;
                        }
                        else if (id == ID_SHIFT_PAUSE)
                        {
                            ShiftPausePressed = true;
                        }
                    }

                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            }
            finally
            {
                // 루프 종료 시 핫키 등록 해제
                UnregisterHotKey(IntPtr.Zero, ID_PAUSE);
                UnregisterHotKey(IntPtr.Zero, ID_SHIFT_PAUSE);
            }
        }

        #endregion
    }
}
#endif
