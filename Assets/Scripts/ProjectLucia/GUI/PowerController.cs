using ProjectLucia.Windows;
using UnityEngine;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// 시스템 종료, 재시작, 초기화 등 전원 관련 메뉴를 제어하는 컨트롤러입니다.
    /// 각 기능에 대한 확인 팝업을 띄우는 역할을 합니다.
    /// </summary>
    public class PowerController : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("시스템 메뉴(종료, 재시작, 초기화 버튼이 있는) 패널")]
        [SerializeField] private GameObject systemMenu;

        #endregion

        #region Private Fields (비공개 필드)

        private ProgramShutdown _programShutdown;
        private ProgramRebooter _programRebooter;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // GameManager를 통해 필요한 컴포넌트 참조 가져오기
            _programShutdown = GameManager.Instance.ProgramShutdown;
            _programRebooter = GameManager.Instance.ProgramRebooter;
        }

        #endregion

        #region Menu Control (메뉴 제어)

        /// <summary>
        /// 시스템 메뉴를 활성화합니다.
        /// </summary>
        public void OnSystemMenu()
        {
            if (systemMenu != null)
                systemMenu.SetActive(true);
        }

        /// <summary>
        /// 시스템 메뉴를 비활성화합니다.
        /// </summary>
        public void OffSystemMenu()
        {
            if (systemMenu != null)
                systemMenu.SetActive(false);
        }

        /// <summary>
        /// 프로그램 종료 확인 팝업을 띄웁니다.
        /// </summary>
        public void TurnShutdownMenu()
        {
            _programShutdown.AlertDisplay();
            OffSystemMenu();
        }

        /// <summary>
        /// 프로그램 재시작 확인 팝업을 띄웁니다.
        /// </summary>
        public void TurnRebooterMenu()
        {
            _programRebooter.AlertDisplayReboot();
            OffSystemMenu();
        }

        /// <summary>
        /// 프로그램 설정 초기화 확인 팝업을 띄웁니다.
        /// </summary>
        public void TurnResetMenu()
        {
            _programRebooter.AlertDisplayReset();
            OffSystemMenu();
        }

        #endregion
    }
}
