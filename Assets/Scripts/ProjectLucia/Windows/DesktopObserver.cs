using System.Threading.Tasks;
using ProjectLucia.Capture;
using ProjectLucia.GUI;
using ProjectLucia.Server;
using ProjectLucia.Status;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectLucia.Windows
{
    /// <summary>
    /// 화면 변화를 감지하고 이미지를 전송하는 이벤트 델리게이트입니다.
    /// </summary>
    /// <param name="imageBytes">JPG로 인코딩된 이미지 데이터</param>
    public delegate void ScreenChangedHandler(byte[] imageBytes);

    /// <summary>
    /// 데스크톱 화면의 변화를 감지하여 서버로 전송하는 클래스입니다.
    /// 주기적으로 화면을 캡처하고, 이전 프레임과 비교하여 변화량이 임계값을 초과하면 이벤트를 발생시킵니다.
    /// </summary>
    public class DesktopObserver : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Header("Detection Settings (감지 설정)")]
        [Tooltip("화면 검사 주기 (초 단위)")]
        public float checkInterval = 1f;

        [Tooltip("변화 감지 임계값 (% 단위, 이 값 이상 변해야 감지됨)")]
        public float changeThreshold = 40.0f;

        [Tooltip("디버그 로그 출력 여부")]
        public bool showDebugLog = true;

        [Header("Timing Logic (타이밍 로직)")]
        [Tooltip("화면 변화 후 안정화되어야 하는 지속 시간 (초)")]
        public float stabilityDuration = 1.0f;

        [Tooltip("전송 후 다음 전송까지의 최소 대기 시간 (초)")]
        public float minSendInterval = 5.0f;

        #endregion

        #region Public Properties (공개 속성)

        /// <summary>
        /// 현재 사용자가 채팅 중인지 여부 (채팅 중에는 감지 중단)
        /// </summary>
        [HideInInspector] public bool isChatting;

        #endregion

        #region Events (이벤트)

        /// <summary>
        /// 화면 변화가 감지되고 안정화되었을 때 발생하는 이벤트
        /// </summary>
        public event ScreenChangedHandler OnScreenChanged;

        #endregion

        #region Private Fields (비공개 필드)

        // 내부 상태 변수
        private Color32[] _lastFrameLowRes; // 비교용 저해상도 픽셀 데이터
        private float _checkTimer;
        private float _stableTimer;
        private bool _wasChanging;
        private byte[] _pendingImageData;
        private float _lastSentTime = -999f;

        // 비교 해상도 (작을수록 연산 빠름)
        private int _lowResSize = 64;

        // 비동기 작업 중복 방지 플래그
        private bool _isProcessing;

        // 매니저 참조
        private InputHandler _inputHandler;
        private SettingController _settingController;
        private LogController _logController;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name == "IntroScene")
                return;
            
            // 매니저 참조 가져오기
            _inputHandler = GameManager.Instance.InputHandler;
            _settingController = GameManager.Instance.SettingController;
            _logController = GameManager.Instance.LogController;

            // RealTalkPixel 설정값 파싱 (저해상도 크기 설정)
            if (System.Enum.TryParse(SettingData.RealTalkPixel, out UISettingEnums.RealtimePixelEnums result))
            {
                _lowResSize = (int)result;
            }
            else
            {
                _lowResSize = (int)UISettingEnums.RealtimePixelEnums.Low; // 기본값
            }
        }

        void Update()
        {
            // 감지 조건 확인
            if (isChatting || !SettingData.IsRealTalk) return;
            
            // UI 조작 중에는 감지 중단
            if (_inputHandler.IsInputText || _settingController.SettingsOpen || _logController.LogsOpen) return;
            
            var scene = SceneManager.GetActiveScene();
            if (scene.name == "IntroScene")
                return;

            // 1. 주기적 검사 실행
            _checkTimer += Time.deltaTime;
            if (_checkTimer >= checkInterval)
            {
                _checkTimer = 0f;
                if (!_isProcessing) // 이전 작업 완료 대기
                {
                    CheckScreenChange(); 
                }
            }

            // 2. 안정화 및 전송 로직 처리
            ProcessStabilityLogic();
        }

        #endregion

        #region Screen Capture & Comparison (화면 캡처 및 비교)

        /// <summary>
        /// 화면을 캡처하고 변화를 감지하는 비동기 메서드입니다.
        /// </summary>
        async void CheckScreenChange()
        {
            _isProcessing = true;

            // 비동기 작업 시작 (캡처, 리사이징, 비교 모두 백그라운드 스레드에서 수행)
            await Task.Run(() =>
            {
                // 1. 화면 캡처 (백그라운드 스레드에서 실행 가능한 Raw 캡처 사용)
                int width, height;
                byte[] rawBytes = DesktopCapture.CaptureFullPrimaryDisplayRaw(out width, out height);

                if (rawBytes == null)
                {
                    return (false, 0f, null, null, 0, 0);
                }

                // 2. Raw Byte Array -> Color32[] 변환 (리사이징을 위해)
                //    (참고: Color32 구조체는 메인 스레드 제약 없음)
                Color32[] rawPixels = new Color32[width * height];
                for (int i = 0; i < rawPixels.Length; i++)
                {
                    int idx = i * 4;
                    rawPixels[i] = new Color32(rawBytes[idx], rawBytes[idx + 1], rawBytes[idx + 2], rawBytes[idx + 3]);
                }

                // 3. 리사이징 (비동기)
                Color32[] currentLowRes = ResizeToLowRes(rawPixels, width, height, _lowResSize, _lowResSize);

                // 4. 비교 로직 (비동기)
                if (_lastFrameLowRes != null)
                {
                    float diff = CalculateDifference(_lastFrameLowRes, currentLowRes);

                    if (diff >= changeThreshold)
                    {
                        // 변화 감지됨 -> 원본 데이터도 반환해야 함 (나중에 인코딩 위해)
                        return (true, diff, currentLowRes, rawBytes, width, height);
                    }
                }
                
                // 변화 없음 또는 첫 프레임
                return (false, 0f, currentLowRes, null, 0, 0);

            }).ContinueWith(task =>
            {
                var (changed, diff, currentLowRes, rawBytes, width, height) = task.Result;

                if (rawBytes == null && currentLowRes == null)
                {
                    // 캡처 실패
                    if (showDebugLog) if(SettingData.IsDebug) Debug.LogWarning("[Observer] 캡쳐 실패");
                    _isProcessing = false;
                    return;
                }

                if (changed)
                {
                    if (showDebugLog) if(SettingData.IsDebug) Debug.Log($"<color=yellow>[Observer] 화면 움직임 감지 ({diff:F1}%)</color>");

                    _wasChanging = true;
                    _stableTimer = 0f;

                    // 변화 감지 시 JPG 인코딩 (메인 스레드에서 Texture2D 생성 후 인코딩)
                    // Texture2D 생성은 메인 스레드 필수
                    Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                    tex.LoadRawTextureData(rawBytes);
                    tex.Apply();
                    
                    _pendingImageData = tex.EncodeToJPG(75);
                    
                    Destroy(tex); // 메모리 해제

                    // 기준 프레임 갱신
                    _lastFrameLowRes = currentLowRes;
                }
                else if (_lastFrameLowRes == null)
                {
                    // 첫 프레임 초기화
                    _lastFrameLowRes = currentLowRes;
                }

                _isProcessing = false;

            }, TaskScheduler.FromCurrentSynchronizationContext()); // 메인 스레드 복귀
        }

        /// <summary>
        /// 화면 안정화 여부를 확인하고 전송 조건을 체크합니다.
        /// </summary>
        void ProcessStabilityLogic()
        {
            if (_wasChanging)
            {
                _stableTimer += Time.deltaTime;

                // (A) 화면이 안정화됨 (설정 시간 경과)
                if (_stableTimer >= stabilityDuration)
                {
                    // (B) 쿨타임 체크
                    if (Time.time - _lastSentTime >= minSendInterval)
                    {
                        if (_pendingImageData != null && !isChatting)
                        {
                            if (showDebugLog) if(SettingData.IsDebug) Debug.Log("<color=green>[Observer] 조건 충족 -> 전송!</color>");
                        
                            // 이벤트 발생 (이미지 전송)
                            OnScreenChanged?.Invoke(_pendingImageData);
                        
                            _lastSentTime = Time.time;
                            _pendingImageData = null;
                        }
                    }
                    else
                    {
                        if (showDebugLog && _pendingImageData != null) 
                            if(SettingData.IsDebug) Debug.Log($"[Observer] 쿨타임 중... 스킵");
                    
                        _pendingImageData = null;
                    }

                    _wasChanging = false;
                    _stableTimer = 0f;
                }
            }
        }

        #endregion

        #region Helper Methods (보조 메서드)

        /// <summary>
        /// 픽셀 배열을 저해상도로 리사이징합니다. (Nearest Neighbor 방식)
        /// </summary>
        Color32[] ResizeToLowRes(Color32[] originalPixels, int srcW, int srcH, int targetW, int targetH)
        {
            Color32[] resultPixels = new Color32[targetW * targetH];

            float xStride = (float)srcW / targetW;
            float yStride = (float)srcH / targetH;

            for (int y = 0; y < targetH; y++)
            {
                for (int x = 0; x < targetW; x++)
                {
                    int px = Mathf.FloorToInt(x * xStride);
                    int py = Mathf.FloorToInt(y * yStride);
                
                    int index = py * srcW + px;
                    if (index < originalPixels.Length)
                    {
                        resultPixels[y * targetW + x] = originalPixels[index];
                    }
                }
            }
            return resultPixels;
        }

        /// <summary>
        /// 두 이미지(픽셀 배열) 간의 차이를 백분율로 계산합니다.
        /// </summary>
        float CalculateDifference(Color32[] img1, Color32[] img2)
        {
            if (img1.Length != img2.Length) return 100f;

            long diffSum = 0;
            for (int i = 0; i < img1.Length; i++)
            {
                diffSum += Mathf.Abs(img1[i].r - img2[i].r) +
                           Mathf.Abs(img1[i].g - img2[i].g) +
                           Mathf.Abs(img1[i].b - img2[i].b);
            }

            // 전체 픽셀 수 * 3채널 * 255(최대값) 으로 나누어 백분율 계산
            double totalMaxDiff = img1.Length * 3.0 * 255.0;
            return (float)((diffSum / totalMaxDiff) * 100.0);
        }

        #endregion
    }
}
