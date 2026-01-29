using UnityEngine;

namespace ProjectLucia.Windows
{
    /// <summary>
    /// 프로그램 종료 기능을 담당하는 클래스입니다.
    /// 종료 확인 팝업을 띄우고, 실제 애플리케이션 종료를 처리합니다.
    /// </summary>
    public class ProgramShutdown : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("종료 확인 팝업 오브젝트")]
        [SerializeField] private GameObject alertDisplay;

        #endregion

        #region Public Methods (공개 메서드)

        /// <summary>
        /// 종료 확인 팝업을 화면에 표시합니다.
        /// </summary>
        public void AlertDisplay()
        {
            if (alertDisplay != null)
                alertDisplay.SetActive(true);
        }
        
        /// <summary>
        /// 게임을 종료하거나 팝업을 닫습니다.
        /// </summary>
        /// <param name="isTrue">true면 종료, false면 취소(팝업 닫기)</param>
        public void QuitGame(bool isTrue)
        {
            if (isTrue)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;  // 에디터 플레이 모드 종료
#else
                Application.Quit();  // 빌드된 게임 종료
#endif
            }
            else
            {
                if (alertDisplay != null)
                    alertDisplay.SetActive(false); 
            }
        }   

        #endregion
    }
}
