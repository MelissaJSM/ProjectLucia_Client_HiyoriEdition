using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectLucia.Capture
{
    /// <summary>
    /// 개별 캡처 아이템을 관리하는 클래스입니다.
    /// 썸네일 표시, 클릭 시 확대 보기, 삭제 기능을 담당합니다.
    /// </summary>
    public class CaptureItem : MonoBehaviour, IPointerClickHandler
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("캡처 이미지를 표시할 RawImage 컴포넌트")]
        [SerializeField] private RawImage image;

        [Tooltip("해당 캡처 아이템을 삭제할 닫기 버튼")]
        [SerializeField] private Button closeButton;

        #endregion

        #region Private Fields (비공개 필드)

        /// <summary>
        /// 이 아이템이 보유한 캡처 텍스처 데이터
        /// </summary>
        private Texture2D _texture;

        /// <summary>
        /// 이 아이템을 관리하는 갤러리 매니저 참조
        /// </summary>
        private CaptureGalleryManager _manager;

        #endregion

        #region Initialization (초기화)

        /// <summary>
        /// 캡처 아이템을 초기화합니다. 갤러리 매니저에서 생성 시 호출됩니다.
        /// </summary>
        /// <param name="tex">표시할 텍스처 이미지</param>
        /// <param name="manager">부모 갤러리 매니저</param>
        public void Init(Texture2D tex, CaptureGalleryManager manager)
        {
            _texture = tex;
            _manager = manager;

            // 이미지 UI 설정
            if (image != null)
                image.texture = tex;

            // 닫기 버튼 이벤트 연결
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnClickClose);
            }
        }

        #endregion

        #region Public Methods (공개 메서드)

        /// <summary>
        /// 현재 아이템이 가지고 있는 텍스처를 반환합니다.
        /// </summary>
        /// <returns>Texture2D 객체</returns>
        public Texture2D GetTexture()
        {
            return _texture;
        }

        #endregion

        #region Event Handlers (이벤트 핸들러)

        /// <summary>
        /// 닫기(삭제) 버튼 클릭 시 호출됩니다.
        /// 텍스처 메모리를 해제하고 매니저에게 아이템 제거를 요청합니다.
        /// </summary>
        private void OnClickClose()
        {
            // 텍스처 메모리 해제
            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }

            // 매니저에게 제거 요청
            if (_manager != null)
                _manager.RemoveItem(this);
            else
                Destroy(gameObject);
        }

        /// <summary>
        /// 아이템(썸네일) 클릭 시 호출됩니다.
        /// 좌클릭 시 큰 미리보기 창을 띄웁니다.
        /// </summary>
        /// <param name="eventData">클릭 이벤트 데이터</param>
        public void OnPointerClick(PointerEventData eventData)
        {
            // 좌클릭만 처리
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            // 매니저를 통해 큰 미리보기 표시
            if (_texture != null && _manager != null)
            {
                _manager.ShowBigPreview(_texture);
            }
        }

        #endregion
    }
}
