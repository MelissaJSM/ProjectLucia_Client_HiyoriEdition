using System.Collections.Generic;
using ProjectLucia.Status;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLucia.Capture
{
    /// <summary>
    /// 캡처된 이미지들을 갤러리 형태로 관리하고 표시하는 매니저 클래스입니다.
    /// 캡처 아이템 생성, 삭제, 미리보기 확대 등의 기능을 제공합니다.
    /// </summary>
    public class CaptureGalleryManager : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Header("Prefab & Parent Settings (프리팹 및 부모 설정)")]
        [Tooltip("갤러리에 추가될 개별 캡처 아이템의 프리팹 (CaptureItem 컴포넌트 포함)")]
        public GameObject captureItemPrefab;

        [Tooltip("캡처 아이템들이 생성될 부모 Transform (보통 ScrollView의 Content)")]
        public Transform itemsParent;

        [Header("Big Preview UI Settings (큰 미리보기 UI 설정)")]
        [Tooltip("이미지 확대 미리보기 UI의 최상위 루트 오브젝트")]
        public GameObject bigPreviewRoot;

        [Tooltip("확대된 이미지를 표시할 RawImage 컴포넌트")]
        public RawImage bigPreviewImage;

        [Tooltip("미리보기 창을 닫을 닫기 버튼")]
        public Button bigPreviewCloseButton;

        #endregion

        #region Private Fields (비공개 필드)

        /// <summary>
        /// 현재 관리 중인 캡처 아이템 리스트
        /// </summary>
        private readonly List<CaptureItem> _items = new List<CaptureItem>();

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 초기화 시 미리보기 창 숨김
            if (bigPreviewRoot != null)
                bigPreviewRoot.SetActive(false);

            // 닫기 버튼 이벤트 연결
            if (bigPreviewCloseButton != null)
            {
                bigPreviewCloseButton.onClick.RemoveAllListeners();
                bigPreviewCloseButton.onClick.AddListener(HideBigPreview);
            }
        }

        #endregion

        #region Gallery Management (갤러리 관리)

        /// <summary>
        /// 새로운 캡처 이미지를 갤러리에 추가합니다.
        /// </summary>
        /// <param name="tex">추가할 텍스처 이미지</param>
        public void AddCapture(Texture2D tex)
        {
            if (captureItemPrefab == null || itemsParent == null || tex == null)
            {
                if(SettingData.IsDebug) Debug.LogWarning("CaptureGalleryManager: 설정 또는 텍스처가 비어있어 아이템을 추가할 수 없습니다.");
                return;
            }

            // 아이템 생성 및 초기화
            var go = Instantiate(captureItemPrefab, itemsParent);
            var item = go.GetComponent<CaptureItem>();
            if (item == null)
            {
                if(SettingData.IsDebug) Debug.LogError("CaptureGalleryManager: 프리팹에 CaptureItem 컴포넌트가 없습니다.");
                Destroy(go);
                return;
            }

            item.Init(tex, this);
            _items.Add(item);
        }

        /// <summary>
        /// 특정 캡처 아이템을 갤러리에서 제거합니다.
        /// </summary>
        /// <param name="item">제거할 CaptureItem 객체</param>
        public void RemoveItem(CaptureItem item)
        {
            if (item == null) return;

            _items.Remove(item);
            Destroy(item.gameObject);
        }

        /// <summary>
        /// 갤러리의 모든 캡처 아이템을 제거합니다.
        /// </summary>
        public void ClearAll()
        {
            foreach (var item in _items)
            {
                if (item != null)
                    Destroy(item.gameObject);
            }
            _items.Clear();
        }

        #endregion

        #region Data Access (데이터 접근)

        /// <summary>
        /// 현재 갤러리에 있는 모든 캡처 이미지의 텍스처 리스트를 반환합니다.
        /// </summary>
        /// <returns>Texture2D 리스트</returns>
        public List<Texture2D> GetAllTextures()
        {
            var result = new List<Texture2D>();

            foreach (var item in _items)
            {
                if (item == null) continue;
                var tex = item.GetTexture();
                if (tex != null)
                    result.Add(tex);
            }

            return result;
        }

        /// <summary>
        /// 특정 인덱스의 캡처 이미지 텍스처를 반환합니다.
        /// </summary>
        /// <param name="index">가져올 이미지의 인덱스</param>
        /// <returns>해당 인덱스의 Texture2D (유효하지 않으면 null)</returns>
        public Texture2D GetTextureAt(int index)
        {
            if (index < 0 || index >= _items.Count)
                return null;

            var item = _items[index];
            return item != null ? item.GetTexture() : null;
        }

        #endregion

        #region Preview UI (미리보기 UI)

        /// <summary>
        /// 선택한 이미지를 큰 미리보기 창에 표시합니다.
        /// </summary>
        /// <param name="tex">표시할 텍스처</param>
        public void ShowBigPreview(Texture2D tex)
        {
            if (bigPreviewRoot == null || bigPreviewImage == null || tex == null)
                return;

            bigPreviewImage.texture = tex;
            // 필요 시 원본 비율로 조정: bigPreviewImage.SetNativeSize();

            bigPreviewRoot.SetActive(true);
        }

        /// <summary>
        /// 큰 미리보기 창을 닫습니다.
        /// </summary>
        private void HideBigPreview()
        {
            if (bigPreviewRoot != null)
                bigPreviewRoot.SetActive(false);
        }

        #endregion
    }
}
