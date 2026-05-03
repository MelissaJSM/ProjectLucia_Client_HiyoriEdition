using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ProjectLucia.Server;
using ProjectLucia.Status;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ProjectLucia.Windows
{
    public class NotificationManager : MonoBehaviour
    {
        [Header("Process Settings")]
        public string subFolderName = "Alert";
        public string exeFileName = "ProjectLucia_Toast.exe";

        [Header("Network Settings")]
        public int port = 3982;

        [Header("Server Reference")]
        public ServerClient serverClient;

        private string _fullPath;

        // UDP 관련 변수
        private UdpClient _udpClient;
        private Thread _receiveThread;
        
        // 스레드 간 안전한 상태 공유를 위한 volatile 키워드
        private volatile bool _isRunningUDP; 
        
        private readonly Queue<string> _messageQueue = new Queue<string>();
        private readonly object _lockObject = new object();

        // 🔥 중복 종료 및 실행 방지용 플래그
        private bool _isShuttingDown = false;
        private bool _isProcessStarting = false;

        // ==========================================
        // [Win32 API] ShellExecute 선언
        // ==========================================
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr ShellExecute(IntPtr hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, int nShowCmd);

        private const int SW_HIDE = 0; // 창 숨김 속성

        private void Awake()
        {
            _fullPath = Path.Combine(Application.streamingAssetsPath, subFolderName, exeFileName);
            
            if (serverClient == null)
                serverClient = FindObjectOfType<ServerClient>();
        }

        // ==========================================
        // [프로세스 제어] 시작
        // ==========================================
        public async void StartToastProcess() 
        {
            if (!ValidateFile() || !SettingData.IsExistedAlert) return;

            // 🔥 중복 실행 방지: 이미 켜는 중이면 무시 (탭 연타, 설정창 무한 팝업 방어)
            if (_isProcessStarting) return;
            _isProcessStarting = true;

            // 1. 기존에 떠있는 프로세스를 OS 단에서 학살
            StopToastProcess();

            // 🔥 2. taskkill 명령이 먹히고 OS가 파일 락을 풀 때까지 1초간 부드럽게 대기 (크래시 방지)
            await Task.Delay(1000);

            try
            {
                string safePath = _fullPath.Replace("/", "\\");
                string workingDirectory = Path.GetDirectoryName(safePath);

                // 3. 깨끗한 상태에서 단 1개의 새 프로세스 실행
                IntPtr result = ShellExecute(IntPtr.Zero, "open", safePath, "", workingDirectory, SW_HIDE);

                int retVal = (int)result;
                if (retVal <= 32)
                {
                    Debug.LogError($"<color=red>[Win32 API Error]</color> 프로세스 실행 실패. Error Code: {retVal}");
                }
                else
                {
                    Debug.Log($"<color=cyan>[Success]</color> 알림 감지 엔진 백그라운드 실행 완료");
                    StartUDP();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"프로세스 실행 중 예외 발생: {e.Message}");
            }
            finally
            {
                // 🔥 실행이 성공하든 실패하든 락을 무조건 해제합니다.
                _isProcessStarting = false;
            }
        }

        private bool ValidateFile()
        {
            if (!File.Exists(_fullPath))
            {
                Debug.LogError($"<color=red>[Error]</color> 파일을 찾을 수 없습니다: {_fullPath}");
                return false;
            }
            return true;
        }

        // ==========================================
        // [프로세스 제어] 종료
        // ==========================================
        public void StopToastProcess()
        {
            StopUDP();

            try
            {
                // 🔥 멜리사님의 원본 방식: 유니티 Native Error 버그를 피하는 가장 안정적인 방법!
                // CMD의 taskkill로 이름이 일치하는 프로세스 싹쓸이 명령 전송 (Fire and Forget)
                string killCommand = $"/F /IM {exeFileName} /T";
                ShellExecute(IntPtr.Zero, "open", "taskkill.exe", killCommand, "", SW_HIDE);
                
                Debug.Log($"<color=yellow>[Stopped]</color> 기존 알림 감지 엔진 종료 명령 전송 완료.");
            }
            catch (Exception e)
            {
                Debug.LogError($"프로세스 강제 종료 중 오류 발생: {e.Message}");
            }
        }

        // ==========================================
        // [UDP 통신] 데이터 수신
        // ==========================================
        private void StartUDP()
        {
            if (_isRunningUDP) return;
            _isRunningUDP = true;
            _receiveThread = new Thread(ReceiveData) { IsBackground = true };
            _receiveThread.Start();
        }

        private void StopUDP()
        {
            _isRunningUDP = false;
            
            if (_udpClient != null) 
            { 
                try 
                { 
                    _udpClient.Close(); 
                } 
                catch { } // 소켓 닫을 때 나는 에러 무시
                
                _udpClient = null; 
            }

            // 유니티 메인 스레드 종료를 막지 않기 위해 _receiveThread.Join() 사용 안 함
            _receiveThread = null; 
        }

        private void ReceiveData()
        {
            try
            {
                _udpClient = new UdpClient(port);
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, port);
            
                while (_isRunningUDP)
                {
                    byte[] data = _udpClient.Receive(ref anyIP);
                    string message = Encoding.UTF8.GetString(data);
                
                    lock (_lockObject) 
                    { 
                        _messageQueue.Enqueue(message); 
                    }
                }
            }
            catch (SocketException)
            {
                // 앱 종료 시 UdpClient.Close()가 호출되어 생기는 자연스러운 끊김 현상이므로 무시합니다.
            }
            catch (Exception e) 
            { 
                // 시스템이 살아있을 때만 에러 출력 (종료 중 메모리 에러 방지)
                if (_isRunningUDP) 
                {
                    try { Debug.LogError($"UDP 수신 오류: {e.Message}"); } catch {}
                }
            }
        }

        // ==========================================
        // [메인 스레드] 데이터 파싱 및 처리
        // ==========================================
        void Update()
        {
            lock (_lockObject)
            {
                while (_messageQueue.Count > 0)
                {
                    string rawData = _messageQueue.Dequeue();
                    ProcessNotification(rawData);
                }
            }
        }

        private void ProcessNotification(string rawData)
        {
            string[] splitData = rawData.Split('|');
            if (splitData.Length < 2) return;

            string header = splitData[0];

            if (header == "Toast" && splitData.Length >= 3)
            {
                string appName = splitData[1];
                string content = splitData[2];
                Debug.Log($"<color=green>[일반 알림]</color> {appName}: {content}");
            
                if (serverClient != null)
                    serverClient.SendNotificationAlert(appName, content, null);
            }
            else if (header == "카카오톡_이미지")
            {
                string imagePath = splitData[1];
                Debug.Log($"<color=yellow>[카톡 캡처 감지]</color> 경로: {imagePath}");
            
                if (serverClient != null)
                    serverClient.SendNotificationAlert("카카오톡", "이미지 알림", imagePath);
            }
        }

        // ==========================================
        // [종료 이벤트] 앱 파괴 / 씬 전환 처리
        // ==========================================
        
        // 🔥 씬이 넘어갈 때 (Reboot 시) 무조건 호출되어 좀비 프로세스 방지
        void OnDestroy()
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;
            StopToastProcess();
        }

        // 앱이 완전히 꺼질 때
        void OnApplicationQuit()
        {
            if (_isShuttingDown) return;
            _isShuttingDown = true;
            StopToastProcess();
        }
    }
}