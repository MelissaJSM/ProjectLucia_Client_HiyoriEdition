using UnityEngine;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// 도움말 및 정보(About Me) 패널을 제어하고 외부 웹사이트 연결 기능을 제공하는 컨트롤러입니다.
    /// </summary>
    public class InstructionController : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Header("Instruction Panels")]
        [Tooltip("도움말 화면의 배경 디머(Dimmer) 패널")]
        [SerializeField] private GameObject instructionDimmer;

        [Tooltip("개발자 정보(About Me) 패널")]
        [SerializeField] private GameObject instructionAboutMe;

        #endregion

        #region Panel Control (패널 제어)

        /// <summary>
        /// 도움말 패널(디머)을 활성화합니다.
        /// </summary>
        public void OnInstruction()
        {
            if (instructionDimmer != null)
                instructionDimmer.SetActive(true);
        }

        /// <summary>
        /// 도움말 패널(디머)을 비활성화합니다.
        /// </summary>
        public void OffInstruction()
        {
            if (instructionDimmer != null)
                instructionDimmer.SetActive(false);
        }

        /// <summary>
        /// 개발자 정보(About Me) 패널을 활성화하고, 도움말 패널은 닫습니다.
        /// </summary>
        public void OnInstructionAboutMe()
        {
            if (instructionAboutMe != null)
                instructionAboutMe.SetActive(true);
            OffInstruction();
        }

        /// <summary>
        /// 개발자 정보(About Me) 패널을 비활성화합니다.
        /// </summary>
        public void OffInstructionAboutMe()
        {
            if (instructionAboutMe != null)
                instructionAboutMe.SetActive(false);
        }

        #endregion

        #region External Links (외부 링크)

        /// <summary>
        /// 개발자 웹사이트(GitHub)를 기본 브라우저로 엽니다.
        /// </summary>
        public void OnInstructionWebSite()
        {
            Application.OpenURL("https://melissa-industry.gitbook.io/projectlucia_client");
        }

        #endregion
    }
}
