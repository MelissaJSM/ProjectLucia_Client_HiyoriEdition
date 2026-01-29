using System;
using System.Security.Cryptography;
using System.Text;
using ProjectLucia.Server;
using ProjectLucia.Status;
using TMPro;
using UnityEngine;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// 시스템 잠금 기능을 제어하는 컨트롤러입니다.
    /// 비밀번호 설정, 잠금 화면 표시, 비밀번호 검증 및 변경 기능을 담당합니다.
    /// 보안을 위해 비밀번호는 해시(Hash)와 솔트(Salt)를 사용하여 저장됩니다.
    /// </summary>
    public class LockController : MonoBehaviour
    {
        #region Constants & Keys (상수 및 키)

        // PlayerPrefs 저장 키
        private const string KeyIsLockingOn     = "IsLockingOn";
        private const string KeyIsBootingLocked = "IsBootingLocked";

        // 레거시(구버전) 호환용 키
        private const string KeyLegacyPassword  = "LockPassword";

        // 보안 저장용 키 (해시 및 솔트)
        private const string KeyPwHash = "LockPasswordHash";
        private const string KeyPwSalt = "LockPasswordSalt";

        // 초기 비밀번호
        private const string DefaultPassword = "3545";

        #endregion

        #region Inspector Fields (인스펙터 설정)

        [Header("UI Panels")]
        [Tooltip("잠금 화면 배경 디머(Dimmer) 패널")]
        [SerializeField] private GameObject lockDimmer;

        [Tooltip("비밀번호 변경 팝업 패널")]
        [SerializeField] private GameObject lockPasswordChange;

        [Tooltip("잠금 알림 디머 패널")]
        [SerializeField] private GameObject lockAlertDimmer;

        [Header("UI Texts & Labels")]
        [Tooltip("잠금 상태 메시지를 표시하는 텍스트")]
        [SerializeField] private TMP_Text lockToggleText;

        [Tooltip("비밀번호 확인 결과 메시지를 표시하는 오브젝트 (내부에 Text 포함)")]
        [SerializeField] private GameObject passwordCheckAnnouncement; 
        public GameObject PasswordCheckAnnouncement { get => passwordCheckAnnouncement; set => passwordCheckAnnouncement = value; }

        [Tooltip("잠금 화면 메시지 텍스트")]
        [SerializeField] private TMP_Text lockMessageText;

        [Tooltip("부팅 잠금 설정 버튼 텍스트")]
        [SerializeField] private TMP_Text bootLockButtonText;

        [Tooltip("잠금 해제 실패 시 알림 메시지를 표시하는 오브젝트")]
        [SerializeField] private GameObject unlockAnnouncementObject; 
        public GameObject UnlockAnnouncementObject { get => unlockAnnouncementObject; set => unlockAnnouncementObject = value; }

        #endregion

        #region Private Fields (비공개 필드)

        // 캐싱된 텍스트 컴포넌트
        private TMP_Text _passwordCheckLabel;
        private TMP_Text _unlockAnnounceLabel;

        // 매니저 참조
        private SettingController _settingController;
        private PanelManager _panelManager;
        private InputHandler _inputHandler;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 매니저 참조 가져오기
            _settingController = GameManager.Instance.SettingController;
            _panelManager      = GameManager.Instance.PanelManager;
            _inputHandler      = GameManager.Instance.InputHandler;

            // 텍스트 컴포넌트 캐싱 (Null 체크 포함)
            _passwordCheckLabel  = passwordCheckAnnouncement ? passwordCheckAnnouncement.GetComponent<TMP_Text>() : null;
            _unlockAnnounceLabel = unlockAnnouncementObject ? unlockAnnouncementObject.GetComponent<TMP_Text>() : null;

            // 비밀번호 저장소 초기화 (마이그레이션 포함)
            EnsurePasswordInitialized();

            // 부팅 잠금 버튼 텍스트 초기화
            UpdateBootLockButtonLabel();
        }

        #endregion

        #region Panel Control (패널 제어)

        /// <summary>
        /// 잠금 화면 디머를 활성화합니다.
        /// </summary>
        public void OnLockDimmer()      => SetActiveSafe(lockDimmer, true);

        /// <summary>
        /// 잠금 화면 디머를 비활성화합니다.
        /// </summary>
        public void OffLockDimmer()     => SetActiveSafe(lockDimmer, false);

        /// <summary>
        /// 비밀번호 변경 팝업을 엽니다.
        /// </summary>
        public void OnLockPasswordChange()
        {
            OffLockDimmer();
            SetActiveSafe(lockPasswordChange, true);
        }

        /// <summary>
        /// 비밀번호 변경 팝업을 닫고 입력 필드를 초기화합니다.
        /// </summary>
        public void OffLockPasswordChange()
        {
            SetActiveSafe(passwordCheckAnnouncement, false);
            SetActiveSafe(lockPasswordChange, false);

            _inputHandler.IsLockIME = false;
            ClearInput(UISettingEnums.InputEnum.OldPassword);
            ClearInput(UISettingEnums.InputEnum.NewPassword);
            ClearInput(UISettingEnums.InputEnum.NewPasswordCheck);

            OnLockDimmer();
        }

        /// <summary>
        /// 잠금 알림 디머를 활성화합니다.
        /// </summary>
        public void OnLockAlertDimmer()
        {
            OffLockDimmer();
            SetActiveSafe(lockAlertDimmer, true);
        }

        /// <summary>
        /// 잠금 알림 디머를 비활성화합니다.
        /// </summary>
        public void OffLockAlertDimmer()
        {
            SetActiveSafe(lockAlertDimmer, false);
            // 잠금 패널이 닫혀있다면 기본 디머를 다시 켭니다.
            if (!_panelManager.Panels[(int)UISettingEnums.PanelsEnum.LockPanel].gameObject.activeSelf)
            {
                OnLockDimmer();
            }
        }

        #endregion

        #region Lock State Management (잠금 상태 관리)

        /// <summary>
        /// 시스템 잠금 상태를 설정합니다.
        /// </summary>
        /// <param name="isLocked">잠금 활성화 여부</param>
        public void LockManagement(bool isLocked)
        {
            if (isLocked)
            {
                // 잠금 활성화: 패널 표시, 설정창 닫기, 입력 포커스 이동
                _panelManager.Panels[(int)UISettingEnums.PanelsEnum.LockPanel].gameObject.SetActive(true);
                OffLockAlertDimmer();
                _settingController.OnSettingsCancel(false);
                UpdateLockToggleMessage();
                PlayerPrefs.SetInt(KeyIsLockingOn, 1);

                FocusInput(UISettingEnums.InputEnum.LockPasswordInput);
            }
            else
            {
                // 잠금 해제: 패널 숨김
                _panelManager.Panels[(int)UISettingEnums.PanelsEnum.LockPanel].gameObject.SetActive(false);
                PlayerPrefs.SetInt(KeyIsLockingOn, 0);
            }

            PlayerPrefs.Save();
            _settingController.CheckMainUiButton(isLocked);
        }

        /// <summary>
        /// 잠금 상태 메시지를 업데이트합니다. (부팅 잠금 여부에 따라 메시지 변경)
        /// </summary>
        private void UpdateLockToggleMessage()
        {
            if (!lockToggleText) return;

            bool bootLocked = PlayerPrefs.GetInt(KeyIsLockingOn, 0) == 1;
            lockToggleText.text = bootLocked
                ? "잠긴 상태로 강제 재시작 되었습니다\n암호를 입력해 주세요"
                : "시스템이 잠겨있습니다.\n암호를 입력해 주세요.";
        }

        /// <summary>
        /// 부팅 시 자동 잠금 기능을 토글합니다.
        /// </summary>
        /// <param name="isClick">UI 클릭 여부 (true: 값 변경, false: UI 갱신만)</param>
        public void LockBootController(bool isClick)
        {
            if (isClick)
            {
                int cur = PlayerPrefs.GetInt(KeyIsBootingLocked, 0);
                PlayerPrefs.SetInt(KeyIsBootingLocked, cur == 1 ? 0 : 1);
                PlayerPrefs.Save();
            }

            UpdateBootLockButtonLabel();
        }

        /// <summary>
        /// 부팅 잠금 버튼의 텍스트를 현재 설정에 맞게 업데이트합니다.
        /// </summary>
        private void UpdateBootLockButtonLabel()
        {
            if (!bootLockButtonText) return;

            bool on = PlayerPrefs.GetInt(KeyIsBootingLocked, 0) == 1;
            bootLockButtonText.text = on ? "부팅 잠금 해제" : "부팅 잠금 설정";
        }

        #endregion

        #region Password Operations (비밀번호 관리)

        /// <summary>
        /// 비밀번호 변경을 시도합니다.
        /// 기존 비밀번호 확인 및 새 비밀번호 일치 여부를 검사합니다.
        /// </summary>
        public void LockPasswordChanger()
        {
            string oldPw = GetInput(UISettingEnums.InputEnum.OldPassword);
            string newPw = GetInput(UISettingEnums.InputEnum.NewPassword);
            string newPwCheck = GetInput(UISettingEnums.InputEnum.NewPasswordCheck);

            if (string.IsNullOrWhiteSpace(oldPw) ||
                string.IsNullOrWhiteSpace(newPw) ||
                string.IsNullOrWhiteSpace(newPwCheck))
            {
                ShowPasswordCheckMsg("입력칸은 비어있으면 안됩니다.");
                return;
            }

            if (!VerifyPassword(oldPw.Trim()))
            {
                ShowPasswordCheckMsg("비밀번호가 틀렸습니다.");
                return;
            }

            if (!string.Equals(newPw.Trim(), newPwCheck.Trim(), StringComparison.Ordinal))
            {
                ShowPasswordCheckMsg("확인용 비밀번호가 일치하지 않습니다.");
                return;
            }

            // 새 비밀번호 저장 (해시 및 솔트 재생성)
            SavePassword(newPw.Trim());
            OffLockPasswordChange();
        }

        /// <summary>
        /// 잠금 해제를 시도합니다.
        /// 입력된 비밀번호를 검증하고 성공 시 잠금을 해제합니다.
        /// </summary>
        public void UnlockController()
        {
            string tryPw = GetInput(UISettingEnums.InputEnum.LockPasswordInput).Trim();

            if (VerifyPassword(tryPw))
            {
                LockManagement(false);
                SetActiveSafe(unlockAnnouncementObject, false);
                _inputHandler.IsLockIME = false;
            }
            else
            {
                if (_unlockAnnounceLabel) _unlockAnnounceLabel.text = "비밀번호가 틀렸습니다.";
                SetActiveSafe(unlockAnnouncementObject, true);

                FocusInput(UISettingEnums.InputEnum.LockPasswordInput);
            }

            SetTextWithoutNotify(UISettingEnums.InputEnum.LockPasswordInput, string.Empty);
        }

        #endregion

        #region UI Helpers (UI 보조 메서드)

        private void SetActiveSafe(GameObject go, bool active)
        {
            if (go && go.activeSelf != active) go.SetActive(active);
        }

        private void ShowPasswordCheckMsg(string msg)
        {
            if (_passwordCheckLabel) _passwordCheckLabel.text = msg;
            SetActiveSafe(passwordCheckAnnouncement, true);
        }

        private void FocusInput(UISettingEnums.InputEnum idx)
        {
            var input = _inputHandler.Inputs[(int)idx];
            if (input)
            {
                input.Select();
                input.ActivateInputField();
            }
        }

        private string GetInput(UISettingEnums.InputEnum idx)
        {
            var input = _inputHandler.Inputs[(int)idx];
            return input ? input.text ?? string.Empty : string.Empty;
        }

        private void ClearInput(UISettingEnums.InputEnum idx)
        {
            var input = _inputHandler.Inputs[(int)idx];
            if (input) input.text = string.Empty;
        }

        private void SetTextWithoutNotify(UISettingEnums.InputEnum idx, string value)
        {
            var input = _inputHandler.Inputs[(int)idx];
            if (input) input.SetTextWithoutNotify(value);
        }

        #endregion

        #region Security Helpers (보안 보조 메서드)

        /// <summary>
        /// 비밀번호 저장소가 초기화되어 있는지 확인하고, 없으면 초기화하거나 마이그레이션합니다.
        /// </summary>
        private void EnsurePasswordInitialized()
        {
            // 이미 해시/솔트가 있으면 통과
            if (!string.IsNullOrEmpty(PlayerPrefs.GetString(KeyPwHash, "")) &&
                !string.IsNullOrEmpty(PlayerPrefs.GetString(KeyPwSalt, "")))
            {
                return;
            }

            // 레거시 평문 비밀번호가 있으면 마이그레이션 수행
            var legacy = PlayerPrefs.GetString(KeyLegacyPassword, "");
            if (!string.IsNullOrEmpty(legacy))
            {
                SavePassword(legacy);
                PlayerPrefs.DeleteKey(KeyLegacyPassword);
                PlayerPrefs.Save();
                return;
            }

            // 저장된 비밀번호가 없으면 기본값으로 초기화
            SavePassword(DefaultPassword);
        }

        /// <summary>
        /// 비밀번호를 해시화하여 저장합니다. (Salt 사용)
        /// </summary>
        private void SavePassword(string plainPassword)
        {
            var salt = GenerateSalt(16); // 128bit Salt 생성
            var hash = ComputeHash(plainPassword, salt);

            PlayerPrefs.SetString(KeyPwSalt, Convert.ToBase64String(salt));
            PlayerPrefs.SetString(KeyPwHash, Convert.ToBase64String(hash));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 입력된 비밀번호가 저장된 해시와 일치하는지 검증합니다.
        /// </summary>
        private bool VerifyPassword(string plainPassword)
        {
            var salt64 = PlayerPrefs.GetString(KeyPwSalt, "");
            var hash64 = PlayerPrefs.GetString(KeyPwHash, "");

            // 해시 데이터가 없으면 레거시 방식으로 검증 (호환성)
            if (string.IsNullOrEmpty(salt64) || string.IsNullOrEmpty(hash64))
            {
                var legacy = PlayerPrefs.GetString(KeyLegacyPassword, DefaultPassword);
                return string.Equals(legacy, plainPassword, StringComparison.Ordinal);
            }

            try
            {
                var salt = Convert.FromBase64String(salt64);
                var storedHash = Convert.FromBase64String(hash64);
                var computed = ComputeHash(plainPassword, salt);

                // 타이밍 공격 방지를 위한 상수 시간 비교
                return FixedTimeEquals(storedHash, computed);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 암호화용 무작위 Salt를 생성합니다.
        /// </summary>
        private static byte[] GenerateSalt(int size)
        {
            var salt = new byte[size];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return salt;
        }

        /// <summary>
        /// 비밀번호와 Salt를 조합하여 SHA256 해시를 생성합니다.
        /// </summary>
        private static byte[] ComputeHash(string password, byte[] salt)
        {
            using var sha = SHA256.Create();
            var pwBytes = Encoding.UTF8.GetBytes(password);
            var buffer = new byte[salt.Length + pwBytes.Length];
            
            Buffer.BlockCopy(salt, 0, buffer, 0, salt.Length);
            Buffer.BlockCopy(pwBytes, 0, buffer, salt.Length, pwBytes.Length);
            
            return sha.ComputeHash(buffer);
        }

        /// <summary>
        /// 두 바이트 배열을 상수 시간(Constant Time)에 비교합니다.
        /// </summary>
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        #endregion
    }
}
