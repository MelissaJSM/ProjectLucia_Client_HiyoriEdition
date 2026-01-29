using ProjectLucia.Live2D;
using ProjectLucia.Status;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// 스크롤바 조작이 끝났을 때(Pointer Up) 특정 동작을 수행하는 트리거 클래스입니다.
    /// 주로 Live2D 모델의 크기나 위치 변경 후 버튼 위치를 재계산하는 데 사용됩니다.
    /// </summary>
    public class ScrollbarTrigger : MonoBehaviour, IPointerUpHandler
    {
        #region Private Fields (비공개 필드)

        private Live2DButtonPosition _buttonPosition;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            _buttonPosition = GameManager.Instance.Live2DButtonPosition;
        }

        #endregion

        #region Event Handlers (이벤트 핸들러)

        /// <summary>
        /// 스크롤바에서 마우스 클릭(터치)을 뗐을 때 호출됩니다.
        /// Live2D 모델의 바운드 박스를 다시 계산하여 버튼 위치를 갱신합니다.
        /// </summary>
        /// <param name="eventData">입력 이벤트 데이터</param>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (_buttonPosition != null)
            {
                _buttonPosition.DrawLive2dBound();
                if(SettingData.IsDebug) Debug.Log("Scrollbar click released: Live2D Bound Updated.");
            }
        }

        #endregion
    }
}
