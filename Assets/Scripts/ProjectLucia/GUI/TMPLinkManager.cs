using ProjectLucia.Status;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// TextMeshPro 텍스트 내의 하이퍼링크 클릭 이벤트를 처리하는 클래스입니다.
    /// 텍스트에 포함된 <link> 태그를 감지하고, 클릭 시 해당 URL을 엽니다.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TMPLinkManager : MonoBehaviour, IPointerClickHandler
    {
        #region Private Fields (비공개 필드)

        private TMP_Text _textComponent;
        private Canvas _rootCanvas;
        private Camera _uiCamera; // Canvas 렌더 모드에 따라 설정됨

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        void Awake()
        {
            _textComponent = GetComponent<TMP_Text>();
            _rootCanvas = GetComponentInParent<Canvas>();

            // Canvas 렌더 모드에 따라 적절한 카메라 설정
            if (_rootCanvas != null)
            {
                // ScreenSpaceOverlay 모드일 경우 카메라는 null이어야 함
                // ScreenSpaceCamera 또는 WorldSpace 모드일 경우 worldCamera 사용
                _uiCamera = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera;
            }
        }

        #endregion

        #region Event Handlers (이벤트 핸들러)

        /// <summary>
        /// 텍스트 컴포넌트 클릭 시 호출됩니다.
        /// 클릭된 위치에 링크가 있는지 확인하고, 있다면 해당 URL을 엽니다.
        /// </summary>
        /// <param name="eventData">클릭 이벤트 데이터</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            if(SettingData.IsDebug) Debug.Log("OnPointerClick: 링크 클릭 감지 시도");

            // 터치/마우스 좌표를 기반으로 클릭된 링크 인덱스 찾기
            // eventData.position을 사용하여 플랫폼 독립적으로 처리
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(_textComponent, eventData.position, _uiCamera);
            
            // 링크가 클릭되지 않았으면 리턴
            if (linkIndex == -1) return;

            // 링크 정보 가져오기
            var linkInfo = _textComponent.textInfo.linkInfo[linkIndex];
            string linkId = linkInfo.GetLinkID();

            // URL 열기
            if (!string.IsNullOrEmpty(linkId))
            {
                if(SettingData.IsDebug) Debug.Log($"Opening URL: {linkId}");
                Application.OpenURL(linkId);
            }
        }

        #endregion
    }
}
