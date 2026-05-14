// Assets/Scripts/Test/SnippingOverlay.cs
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Collections;
using ProjectLucia.Status;
using UnityEngine;
using System.Runtime.InteropServices; // 이 줄을 추가!

namespace ProjectLucia.Capture
{
    /// <summary>
    /// 화면 캡처를 위한 오버레이 UI 및 캡처 로직을 제어하는 클래스입니다.
    /// 마우스 드래그를 통한 영역 선택 캡처 및 전체 화면 캡처 기능을 제공합니다.
    /// </summary>
    public class SnippingOverlay : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Header("UI Overlay Settings (UI 오버레이 설정)")]
        [Tooltip("캡처 모드 시 전체 화면을 어둡게 덮는 패널 오브젝트")]
        public GameObject overlayRoot;

        [Tooltip("드래그하여 선택된 영역을 표시할 RectTransform (Image 컴포넌트 포함)")]
        public RectTransform selectionRect;

        [Header("Manager References (매니저 참조)")]
        [Tooltip("캡처된 이미지를 저장하고 관리할 갤러리 매니저")]
        public CaptureGalleryManager galleryManager;

        #endregion

        #region Private Fields (비공개 필드)

        /// <summary>
        /// 현재 영역 선택 모드인지 여부
        /// </summary>
        private bool _isSelecting;

        /// <summary>
        /// 드래그 시작 지점 (스크린 좌표)
        /// </summary>
        private Vector2 _dragStart;

        /// <summary>
        /// 현재 드래그 위치 (스크린 좌표)
        /// </summary>
        private Vector2 _dragCurrent;

        /// <summary>
        /// 부모 캔버스 참조 (좌표 변환용 캐싱)
        /// </summary>
        private Canvas _canvas;

        /// <summary>
        /// 선택 영역 UI의 부모 RectTransform (좌표 변환용 캐싱)
        /// </summary>
        private RectTransform _parentRect;
        
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        private const int VK_ESCAPE = 0x1B; // ESC 키의 가상 키 코드

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 시작 시 오버레이가 켜져있다면 강제로 끄기
            if (overlayRoot != null)
                overlayRoot.SetActive(false);

            // 선택 영역 크기 초기화 및 컴포넌트 캐싱
            if (selectionRect != null)
            {
                _canvas = selectionRect.GetComponentInParent<Canvas>();
                _parentRect = selectionRect.parent as RectTransform;
                selectionRect.sizeDelta = Vector2.zero;
            }
        }

        void Update()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // 글로벌 핫키 플래그 확인 (GlobalHotkey 스크립트 연동)
            if (GlobalHotkey.ShiftPausePressed && SettingData.CanPressedPicture)
            {
                GlobalHotkey.ShiftPausePressed = false; // 플래그 소비
                StartCoroutine(CaptureFullDisplayCoroutine());
            }
            else if (GlobalHotkey.PausePressed && SettingData.CanPressedPicture)
            {
                GlobalHotkey.PausePressed = false;      // 플래그 소비
                StartSelectionMode();
            }
#else
            // (에디터/기타 플랫폼 예비 코드) - Legacy Input 사용
            if (Input.GetKeyDown(KeyCode.Pause))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (shift) StartCoroutine(CaptureFullDisplayCoroutine());
                else       StartSelectionMode();
            }
#endif

            // 선택 모드일 때 입력 처리
            if (_isSelecting)
                HandleSelectionInput();
        }

        #endregion

        #region Selection Mode Logic (선택 모드 로직)

        /// <summary>
        /// 영역 선택 모드를 시작합니다. 오버레이를 활성화하고 초기 좌표를 설정합니다.
        /// </summary>
        void StartSelectionMode()
        {
            _isSelecting = true;
            
            // 현재 마우스 위치를 시작점으로 설정
            _dragStart = Input.mousePosition;
            _dragCurrent = _dragStart;

            if (overlayRoot != null)
                overlayRoot.SetActive(true);

            UpdateSelectionRect();
        }

        /// <summary>
        /// 영역 선택을 완료하고 캡처를 수행합니다.
        /// </summary>
        void FinishSelection()
        {
            _isSelecting = false;

            if (overlayRoot != null)
                overlayRoot.SetActive(false);
            
            Rect selection = GetSelectionRect(_dragStart, _dragCurrent);

            // 너무 작은 영역(2x2 미만)은 무시
            if (selection.width < 2f || selection.height < 2f)
                return;

            StartCoroutine(CaptureSelectionCoroutine(selection));
        }

        /// <summary>
        /// 영역 선택을 취소합니다. (ESC 키 등)
        /// </summary>
        void CancelSelection()
        {
            _isSelecting = false;

            if (overlayRoot != null)
                overlayRoot.SetActive(false);
            
            if (selectionRect != null)
            {
                selectionRect.sizeDelta = Vector2.zero;
            }
        }

        #endregion

        #region Input Handling (입력 처리)

        /// <summary>
        /// 선택 모드 중 마우스 및 키보드 입력을 처리합니다.
        /// </summary>
        void HandleSelectionInput()
        {
            // 좌클릭 시작 (Down) - 드래그 시작점 재설정
            if (Input.GetMouseButtonDown(0))
            {
                _dragStart = Input.mousePosition;
                _dragCurrent = _dragStart;
                UpdateSelectionRect();
            }

            // 좌클릭 유지 (Press) - 드래그 중
            if (Input.GetMouseButton(0))
            {
                _dragCurrent = Input.mousePosition;
                UpdateSelectionRect();
            }

            // 좌클릭 뗌 (Up) - 선택 완료
            if (Input.GetMouseButtonUp(0))
            {
                _dragCurrent = Input.mousePosition;
                FinishSelection();
            }

            // ESC 키 - 취소 (Unity 포커스 + 백그라운드 글로벌 감지 모두 적용)
            // GetAsyncKeyState(VK_ESCAPE) & 0x8000 은 키가 현재 눌려있는지를 확인합니다.
            if (Input.GetKeyDown(KeyCode.Escape) || (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0)
            {
                CancelSelection();
            }
        }

        #endregion

        #region UI Update & Calculation (UI 업데이트 및 계산)

        /// <summary>
        /// 두 점(시작점, 끝점)을 기준으로 Rect 영역을 계산합니다.
        /// </summary>
        static Rect GetSelectionRect(Vector2 a, Vector2 b)
        {
            float xMin = Mathf.Min(a.x, b.x);
            float yMin = Mathf.Min(a.y, b.y);
            float width = Mathf.Abs(a.x - b.x);
            float height = Mathf.Abs(a.y - b.y);
            return new Rect(xMin, yMin, width, height);
        }

        /// <summary>
        /// 선택 영역 UI(RectTransform)의 위치와 크기를 업데이트합니다.
        /// </summary>
        void UpdateSelectionRect()
        {
            if (selectionRect == null || _parentRect == null)
                return;

            if (_canvas == null)
                _canvas = selectionRect.GetComponentInParent<Canvas>();

            Camera cam = null;
            // Screen Space - Overlay가 아닌 경우 카메라 필요
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = _canvas.worldCamera;

            // 스크린 좌표(마우스)를 부모 RectTransform의 로컬 좌표로 변환
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, _dragStart, cam, out var localStart);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRect, _dragCurrent, cam, out var localCurrent);

            Rect r = GetSelectionRect(localStart, localCurrent);

            // Pivot에 따른 위치 보정
            // r.position은 Rect의 좌하단(min) 좌표.
            // selectionRect.localPosition은 Pivot 위치 기준이므로 보정 필요.
            selectionRect.localPosition = r.position + Vector2.Scale(selectionRect.pivot, r.size);
            selectionRect.sizeDelta = r.size;
        }

        #endregion

        #region Capture Coroutines (캡처 코루틴)

        /// <summary>
        /// 선택된 영역을 캡처하는 코루틴입니다.
        /// Unity 좌표계를 Windows 스크린 좌표계로 변환하여 캡처를 수행합니다.
        /// </summary>
        /// <param name="selection">선택된 영역 (Unity Screen 좌표계)</param>
        IEnumerator CaptureSelectionCoroutine(Rect selection)
        {
            // UI 갱신 대기 (한 프레임)
            yield return null;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            // Unity 게임 뷰 해상도
            int unityW = Screen.width;
            int unityH = Screen.height;

            // 실제 윈도우(프라이머리 모니터) 해상도
            int sysW = DesktopCapture.PrimaryWidth;
            int sysH = DesktopCapture.PrimaryHeight;

            // 해상도 비율 보정 (DPI 스케일링 대응 등)
            float scaleX = (float)sysW / unityW;
            float scaleY = (float)sysH / unityH;

            // Unity 좌표계 (좌하단 원점)
            float xMinU = selection.xMin;
            float yMinU = selection.yMin;
            float xMaxU = selection.xMax;
            float yMaxU = selection.yMax;

            // 윈도우 좌표계 (좌상단 원점)로 변환
            int screenX      = Mathf.RoundToInt(xMinU * scaleX);
            int captureWidth = Mathf.RoundToInt((xMaxU - xMinU) * scaleX);

            // Unity의 위쪽 경계(yMaxU)가 윈도우에서의 시작 Y가 됨 (Y축 반전)
            int screenY       = Mathf.RoundToInt(sysH - (yMaxU * scaleY));
            int captureHeight = Mathf.RoundToInt((yMaxU - yMinU) * scaleY);

            if (captureWidth <= 0 || captureHeight <= 0)
                yield break;

            // 실제 화면 캡처 수행
            Texture2D tex = DesktopCapture.CaptureRegion(
                screenX,
                screenY,
                captureWidth,
                captureHeight
            );

            if (tex != null)
            {
                // 갤러리에 캡처 이미지 추가
                if (galleryManager != null)
                {
                    galleryManager.AddCapture(tex);
                }
                
                if(SettingData.IsDebug) Debug.Log(
                    $"Capture selection: UnityRect={selection} → " +
                    $"Screen({screenX},{screenY},{captureWidth},{captureHeight})"
                );
            }
#else
            yield break;
#endif
        }

        /// <summary>
        /// 전체 화면을 캡처하는 코루틴입니다.
        /// </summary>
        IEnumerator CaptureFullDisplayCoroutine()
        {
            yield return null;

            Texture2D tex = DesktopCapture.CaptureFullPrimaryDisplay();
            if (tex != null)
            {
                if (galleryManager != null)
                    galleryManager.AddCapture(tex);
            }
        }

        #endregion
    }
}
#endif
