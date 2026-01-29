using ProjectLucia.GUI;
using ProjectLucia.Server;
using ProjectLucia.Status;
using UnityEngine;

namespace ProjectLucia.Windows
{
    /// <summary>
    /// 시스템의 하드웨어 정보(GPU, VRAM 등)를 확인하고 관리하는 클래스입니다.
    /// 그래픽 카드 변경 감지 및 Whisper 모델 사용 가능 여부(VRAM 체크)를 판단합니다.
    /// </summary>
    public class SystemInformation : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("GPU 정보 확인 및 매칭을 위한 UI 패널")]
        [SerializeField] private GameObject gpuCheck;
        public GameObject GpuCheck => gpuCheck;

        #endregion

        #region Private Fields (비공개 필드)

        private DropdownManager _dropdownManager;
        private SettingController _settingController;
        private TextManager _textManager;
        private PanelManager _panelManager;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 매니저 참조 가져오기
            _dropdownManager = GameManager.Instance.DropdownManager;
            _settingController = GameManager.Instance.SettingController;
            _textManager = GameManager.Instance.TextManager;
            _panelManager = GameManager.Instance.PanelManager;
        }

        #endregion

        #region System Information Check (시스템 정보 확인)

        /// <summary>
        /// 시스템 정보를 확인하고 저장된 GPU 정보와 비교합니다.
        /// </summary>
        public void SystemInformationStart()
        {
            // 디버그 로그 출력
            if(SettingData.IsDebug) Debug.Log($"Graphics Device Name: {SystemInfo.graphicsDeviceName}");
            if(SettingData.IsDebug) Debug.Log($"Graphics Device Vendor: {SystemInfo.graphicsDeviceVendor}");
            if(SettingData.IsDebug) Debug.Log($"Graphics Device Version: {SystemInfo.graphicsDeviceVersion}");
            if(SettingData.IsDebug) Debug.Log($"Graphics Memory Size (MB): {SystemInfo.graphicsMemorySize}");
            if(SettingData.IsDebug) Debug.Log($"Graphics Device Type: {SystemInfo.graphicsDeviceType}");
            if(SettingData.IsDebug) Debug.Log($"Graphics MultiThreaded: {SystemInfo.graphicsMultiThreaded}");
            if(SettingData.IsDebug) Debug.Log($"Max Texture Size: {SystemInfo.maxTextureSize}");
            
            // 저장된 GPU 정보와 현재 GPU 정보 비교
            if (!string.IsNullOrEmpty(SettingData.SavedGPUName) && SettingData.SavedGPUName == SystemInfo.graphicsDeviceName)
            {
                // 일치함
                SettingData.IsMatchedGPU = true;
                SettingData.ResultGPUName = SettingData.SavedGPUName;
            }
            else if(string.IsNullOrEmpty(SettingData.SavedGPUName))
            {
                // 저장된 정보 없음 (최초 실행 등)
                if(SettingData.IsDebug) Debug.Log("그래픽카드 정보가 저장되어 있지 않습니다.");
                SettingData.IsMatchedGPU = false;
            }
            else
            {
                // 불일치함 (하드웨어 변경 등)
                SettingData.IsMatchedGPU = false;
                SettingData.ResultGPUName = SettingData.SavedGPUName;
            }
        }

        /// <summary>
        /// 현재 GPU 정보를 저장하고 설정을 갱신합니다. (매칭 버튼 클릭 시 호출)
        /// </summary>
        public void SystemInformationCheck()
        {
            SettingData.ResultGPUName = SystemInfo.graphicsDeviceName;
            SettingData.IsMatchedGPU = true;
            
            // Whisper 모델 선택 초기화 (안전장치)
            _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.WhisperModel].SetValueWithoutNotify(0);
            
            // Voice 탭 갱신
            _settingController.OnTabVoice();
        }

        /// <summary>
        /// 선택한 Whisper 모델이 현재 시스템의 VRAM 용량 내에서 구동 가능한지 확인합니다.
        /// </summary>
        /// <param name="value">선택한 Whisper 모델 인덱스</param>
        /// <returns>사용 가능 여부 (true: 가능, false: 불가능)</returns>
        public bool SystemModelVramCheck(int value)
        {
            if(SettingData.IsDebug) Debug.Log($"드롭다운 밸류 값 : {value}");

            int vram = SystemInfo.graphicsMemorySize;
            int limit70Per = vram / 100 * 70; // 70% 경고 기준
            int result = VramStatus.WhisperSize[value];

            // 양자화 옵션에 따른 VRAM 사용량 보정 (0: 없음, 1: Q8, 2: Q5)
            int quantValue = _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.WhisperQuantization].value;

            if (quantValue == 1) // Q8 (약 60%)
            {
                result = (int)(result * 0.6f);
            }
            else if(quantValue == 2) // Q5 (약 40%)
            {
                result = (int)(result * 0.4f);
            }

            // UI 정보 갱신
            _textManager.Texts[(int)UISettingEnums.TextsEnum.GPUName].text = SettingData.ResultGPUName;
            _textManager.Texts[(int)UISettingEnums.TextsEnum.GPUVram].text = $"{result}/{vram}MB";

            if (value == 0)
            {
                // 모델 미사용 선택 시 항상 통과
                return true;
            }

            // VRAM 초과 여부 확인
            if (result + VramStatus.SileroVad >= vram)
            {
                if(SettingData.IsDebug) Debug.Log("VRAM 용량 초과");
                _textManager.Texts[(int)UISettingEnums.TextsEnum.GPUStatus].color = Color.red;
                _textManager.Texts[(int)UISettingEnums.TextsEnum.GPUStatus].text = "금지";
                _panelManager.LockOfPanel(true); // 패널 잠금
                return false; 
            }
            
            // 경고 수준 확인 (70% 이상)
            if (result + VramStatus.SileroVad >= limit70Per)
            {
                if(SettingData.IsDebug) Debug.Log("VRAM 사용량 경고 수준");
                _textManager.Texts[(int)UISettingEnums.TextsEnum.GPUStatus].color = Color.yellow;
                _textManager.Texts[(int)UISettingEnums.TextsEnum.GPUStatus].text = "경고";
                _panelManager.LockOfPanel(false);
                return true; // 사용은 가능
            }
            
            // 정상 범위
            if(SettingData.IsDebug) Debug.Log("VRAM 사용량 정상");
            _textManager.Texts[(int)UISettingEnums.TextsEnum.GPUStatus].color = Color.green;
            _textManager.Texts[(int)UISettingEnums.TextsEnum.GPUStatus].text = "정상";
            _panelManager.LockOfPanel(false);
            return true; 
        }

        #endregion
    }
}
