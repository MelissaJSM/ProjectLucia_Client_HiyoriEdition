using ProjectLucia.GUI;
using ProjectLucia.Server;
using ProjectLucia.Status;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace ProjectLucia.Windows
{
    /// <summary>
    /// 프로그램의 재시작(Reboot) 및 초기화(Reset) 기능을 담당하는 클래스입니다.
    /// 관련 확인 팝업 UI를 제어하고, 실제 프로세스 재시작 로직을 수행합니다.
    /// </summary>
    public class ProgramRebooter : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("재시작 확인 팝업 오브젝트")]
        [SerializeField] private GameObject rebootAlert;

        [Tooltip("초기화 확인 팝업 오브젝트")]
        [SerializeField] private GameObject resetAlert;

        #endregion

        #region Private Fields (비공개 필드)

        private SaveController _saveController;
        private ServerClient _serverClient;
        private ActionManager _actionManager;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            _saveController = GameManager.Instance.SaveController;
            _serverClient = GameManager.Instance.ServerClient;
            _actionManager = GameManager.Instance.ActionManager;
        }

        #endregion

        #region UI Control (UI 제어)

        /// <summary>
        /// 재시작 확인 팝업을 표시합니다.
        /// </summary>
        public void AlertDisplayReboot()
        {
            if (rebootAlert != null)
                rebootAlert.SetActive(true);
        }

        /// <summary>
        /// 초기화 확인 팝업을 표시합니다.
        /// </summary>
        public void AlertDisplayReset()
        {
            if (resetAlert != null)
                resetAlert.SetActive(true);
        }

        /// <summary>
        /// 모든 알림 팝업을 닫습니다.
        /// </summary>
        public void OffAlertDisplay()
        {
            if (rebootAlert != null) rebootAlert.SetActive(false);
            if (resetAlert != null) resetAlert.SetActive(false);
        }

        #endregion

        #region System Operations (시스템 조작)

        /// <summary>
        /// 설정을 저장하고 IntroScene으로 이동하여 재시작 효과를 냅니다.
        /// </summary>
        public void OnReboot()
        {
            _saveController.SaveSettings();
            SceneManager.LoadScene("IntroScene");
        }

        /// <summary>
        /// 설정을 초기화하고 IntroScene으로 이동하여 재시작 효과를 냅니다.
        /// </summary>
        public void OnReset()
        {
            _saveController.ResetSettings();
            SceneManager.LoadScene("IntroScene");
        }

        /// <summary>
        /// 서버 재시작 요청을 보냅니다.
        /// </summary>
        public void OnServerRestart()
        {
            _actionManager.LoadingCharacterAction(0);
            _serverClient.SendRestartingToServer();
        }

        #endregion
    }
}
