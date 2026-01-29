using ProjectLucia.Status;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// 폰트 크기 조절 슬라이더 등을 조작할 때 텍스트 크기 미리보기를 제공하는 클래스입니다.
    /// 사용자가 UI를 누르고 있는 동안 예시 텍스트를 화면에 표시합니다.
    /// </summary>
    public class FontSizeTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        #region Private Fields (비공개 필드)

        private PanelManager _panelManager;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            _panelManager = GameManager.Instance.PanelManager;
        }

        #endregion

        #region Event Handlers (이벤트 핸들러)

        /// <summary>
        /// UI 요소를 클릭(터치)하기 시작했을 때 호출됩니다.
        /// 폰트 크기를 확인할 수 있는 다국어 예시 텍스트를 출력합니다.
        /// </summary>
        /// <param name="eventData">입력 이벤트 데이터</param>
        public void OnPointerDown(PointerEventData eventData)
        {
            if(SettingData.IsDebug) Debug.Log("포인터 다운 눌러짐");

            _panelManager.ResponseTextProcess("텍스트 사이즈를 확인 할 수 있습니다.\nYou can check the text size.\nテキストサイズを確認できます", false);
        }

        /// <summary>
        /// UI 요소에서 클릭(터치)을 뗐을 때 호출됩니다.
        /// 텍스트 출력을 종료하고 대화창을 닫거나 초기화합니다.
        /// </summary>
        /// <param name="eventData">입력 이벤트 데이터</param>
        public void OnPointerUp(PointerEventData eventData)
        {
            if(SettingData.IsDebug) Debug.Log("포인터 업 눌러짐");
            _panelManager.ResponseTextEnd(false);
        }

        #endregion
    }
}
