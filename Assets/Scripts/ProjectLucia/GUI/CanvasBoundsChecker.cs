using System.Collections.Generic;
using Live2D.Cubism.Framework.Raycasting;
using ProjectLucia.Capture;
using ProjectLucia.Status;
using ProjectLucia.Windows;
using TMPro;
using UnityEngine;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// 마우스 커서의 위치에 따라 UI 요소들의 표시 여부와 상호작용 가능 상태를 제어하는 클래스입니다.
    /// 투명 윈도우 처리, 버튼 페이드 효과, Live2D 레이캐스팅 등을 관리합니다.
    /// </summary>
    public class CanvasBoundsChecker : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("Live2D 모델과의 상호작용을 위한 레이캐스터")]
        [SerializeField] private CubismRaycaster raycaster; 
        
        /// <summary>
        /// 외부에서 접근 가능한 Raycaster 프로퍼티
        /// </summary>
        public CubismRaycaster Raycaster => raycaster;

        #endregion

        #region Public Properties (공개 속성)

        /// <summary>
        /// 현재 마우스 X 좌표 (외부 입력)
        /// </summary>
        public float MouseX { get; set; }

        /// <summary>
        /// 현재 마우스 Y 좌표 (외부 입력)
        /// </summary>
        public float MouseY { get; set; }

        /// <summary>
        /// 마우스가 버튼 위에 있는지 여부
        /// </summary>
        public bool IsMouseOverButton { get; private set; }

        #endregion

        #region Private Fields (비공개 필드)

        private PanelManager _panelManager;
        private TransparentApp _transparentApp;
        private FadeControllerUser _fadeUser;
        private SnippingOverlay _snippingOverlay;

        /// <summary>
        /// 각 버튼의 현재 표시 상태(Visible)를 캐싱하는 딕셔너리
        /// </summary>
        private readonly Dictionary<GameObject, bool> _visible = new Dictionary<GameObject, bool>();
        
        /// <summary>
        /// 최적화를 위한 RectTransform 캐싱
        /// </summary>
        private readonly Dictionary<GameObject, RectTransform> _rectTransforms = new Dictionary<GameObject, RectTransform>();
        
        /// <summary>
        /// 최적화를 위한 TMP_InputField 캐싱
        /// </summary>
        private readonly Dictionary<GameObject, TMP_InputField> _inputFields = new Dictionary<GameObject, TMP_InputField>();

        private Camera _cam;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 매니저 및 컴포넌트 참조 가져오기
            _panelManager   = GameManager.Instance.PanelManager;
            _transparentApp = GameManager.Instance.TransparentApp;
            _fadeUser       = GameManager.Instance.FadeControllerUser;
            _snippingOverlay = GameManager.Instance.SnippingOverlay;

            _cam = Camera.main;

            // 모든 버튼 초기화: CanvasGroup 추가, 초기 알파 0, 캐싱
            foreach (GameObject button in _panelManager.ButtonCanvasRenderers)
            {
                EnsureCanvasGroup(button, setInitialAlpha: 0f, blocksRaycasts: false, interactable: false);
                _visible[button] = false; // 초기 상태는 숨김

                // RectTransform 캐싱
                if (!_rectTransforms.ContainsKey(button))
                {
                    var rt = button.GetComponent<RectTransform>();
                    if (rt != null) _rectTransforms[button] = rt;
                }

                // TMP_InputField 캐싱 (입력 필드가 있는 경우)
                var input = button.GetComponent<TMP_InputField>();
                if (input != null)
                {
                    _inputFields[button] = input;
                }
            }
        }

        private void Start()
        {
            // 첫 프레임에 마우스 위치 기반으로 UI 상태 즉시 설정 (깜빡임 방지)
            var mouse = new Vector2(MouseX, MouseY);
            bool insideCanvas = RectTransformUtility.RectangleContainsScreenPoint(_panelManager.CanvasRect, mouse, _cam);
            _transparentApp.Toggle = insideCanvas;
            _transparentApp.SetWindows();

            foreach (GameObject button in _panelManager.ButtonCanvasRenderers)
            {
                bool shouldShow = ComputeShouldShow(button, mouse);
                SetAlphaImmediate(button, shouldShow ? 1f : 0f, shouldShow); // 즉시 반영
                _visible[button] = shouldShow;
            }
        }

        private void Update()
        {
            Vector2 mouse = new Vector2(MouseX, MouseY);

            // 1. 캔버스 영역 및 투명 윈도우 처리
            // 캡처 오버레이가 켜져 있으면 무조건 캔버스 내부로 간주하여 클릭 가능 상태 유지
            var insideCanvas = _snippingOverlay.overlayRoot.activeSelf || RectTransformUtility.RectangleContainsScreenPoint(_panelManager.CanvasRect, mouse, _cam);

            if (_transparentApp.Toggle != insideCanvas)
            {
                _transparentApp.Toggle = insideCanvas;
                _transparentApp.SetWindows();
            }

            // 2. 버튼별 표시 규칙 평가 및 페이드 처리
            foreach (GameObject button in _panelManager.ButtonCanvasRenderers)
            {
                bool shouldShow = ComputeShouldShow(button, mouse);

                // 마우스가 버튼 위에 있는지 확인 (마지막 평가된 버튼 기준)
                if (_rectTransforms.TryGetValue(button, out var buttonRect))
                {
                    IsMouseOverButton = RectTransformUtility.RectangleContainsScreenPoint(buttonRect, mouse, _cam);
                }
                else
                {
                    // 캐싱 실패 시 Fallback
                    var rt = button.GetComponent<RectTransform>();
                    if (rt) IsMouseOverButton = RectTransformUtility.RectangleContainsScreenPoint(rt, mouse, _cam);
                }

                // 상태 변경 감지 및 처리
                if (_visible.TryGetValue(button, out bool nowVisible))
                {
                    if (nowVisible == shouldShow)
                        continue; // 상태 변화 없음

                    // 상태 변경 -> 페이드 효과 적용
                    if (shouldShow)
                    {
                        _fadeUser.StartFadeIn(button); // 페이드 인
                        SetInteract(button, true);     // 상호작용 활성화
                    }
                    else
                    {
                        _fadeUser.StartFadeOut(button); // 페이드 아웃
                        SetInteract(button, false);     // 상호작용 비활성화
                    }

                    _visible[button] = shouldShow;
                }
                else
                {
                    // 예외 상황: 캐시에 없는 경우 즉시 동기화
                    SetAlphaImmediate(button, shouldShow ? 1f : 0f, shouldShow);
                    _visible[button] = shouldShow;
                }
            }
        }

        #endregion

        #region Logic Methods (로직 메서드)

        /// <summary>
        /// 특정 버튼이 현재 보여야 하는지 여부를 계산합니다.
        /// 설정(SettingData.IsIconFade) 및 마우스 위치, 입력 상태 등을 고려합니다.
        /// </summary>
        /// <param name="button">대상 버튼 게임 오브젝트</param>
        /// <param name="mouse">현재 마우스 위치</param>
        /// <returns>표시 여부 (true: 보임, false: 숨김)</returns>
        private bool ComputeShouldShow(GameObject button, Vector2 mouse)
        {
            string buttonName = button.name;

            // 항상 표시되어야 하는 핵심 아이콘들 (RagDetector 제거됨)
            bool alwaysShow = buttonName == "VADDetector" || buttonName == "WSDetector" || buttonName == "RealDetector" || buttonName == "ThinkDetector";

            // RectTransform 가져오기 (캐시 우선)
            if (!_rectTransforms.TryGetValue(button, out var buttonRect))
            {
                buttonRect = button.GetComponent<RectTransform>(); // Fallback
            }
            
            // 마우스 오버 여부 확인
            bool over = false;
            if (buttonRect != null)
            {
                over = RectTransformUtility.RectangleContainsScreenPoint(buttonRect, mouse, _cam);
            }

            // 아이콘 페이드 설정에 따른 분기
            switch (SettingData.IsIconFade)
            {
                case 0: // 항상 표시
                    return true;

                case 1: // 일부 표시 (핵심 버튼 상시 표시 + 마우스 오버 시 표시)
                    if (alwaysShow) return true;
                    if (over) return true;

                    // 입력 필드는 포커스 중이거나 텍스트가 있으면 유지
                    if (buttonName == "TextInput")
                    {
                        if (_inputFields.TryGetValue(button, out var input))
                        {
                            bool focused = input.isFocused;
                            bool hasText = !string.IsNullOrEmpty(input.text);
                            return focused || hasText;
                        }
                    }
                    return false;

                default: // 2: 전부 페이드 (마우스 오버 시에만 표시, 입력 중인 필드 제외)
                    if (buttonName == "TextInput")
                    {
                        if (_inputFields.TryGetValue(button, out var input2))
                        {
                            bool focused2 = input2.isFocused;
                            bool hasText2 = !string.IsNullOrEmpty(input2.text);
                            if (focused2 || hasText2) return true;
                        }
                    }
                    return over;
            }
        }

        #endregion

        #region Helper Methods (보조 메서드)

        /// <summary>
        /// CanvasGroup 컴포넌트가 있는지 확인하고 없으면 추가하며, 초기 상태를 설정합니다.
        /// </summary>
        private static void EnsureCanvasGroup(GameObject go, float setInitialAlpha, bool blocksRaycasts, bool interactable)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = Mathf.Clamp01(setInitialAlpha);
            cg.blocksRaycasts = blocksRaycasts;
            cg.interactable = interactable;
        }

        /// <summary>
        /// CanvasGroup의 알파값과 상호작용 상태를 즉시 설정합니다. (페이드 효과 없음)
        /// </summary>
        private static void SetAlphaImmediate(GameObject go, float a, bool interact)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = Mathf.Clamp01(a);
            cg.interactable = interact;
            cg.blocksRaycasts = interact;
        }

        /// <summary>
        /// CanvasGroup의 상호작용 가능 여부를 설정합니다.
        /// </summary>
        private static void SetInteract(GameObject go, bool on)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.interactable   = on;
                cg.blocksRaycasts = on;
            }
        }

        #endregion
    }
}
