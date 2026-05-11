using System.Collections.Generic;
using UnityEngine;
using Live2D.Cubism.Core;
using ProjectLucia.GUI;
using ProjectLucia.Server;
using ProjectLucia.Status;

namespace ProjectLucia.Live2D
{
    /// <summary>
    /// Live2D 모델의 위치와 크기에 따라 UI 요소(버튼, 패널 등)의 위치를 동적으로 조정하는 클래스입니다.
    /// 모델의 바운드(Bounds)를 계산하고, 이를 기반으로 UI를 배치합니다.
    /// </summary>
    public class Live2DButtonPosition : MonoBehaviour
    {
        #region Private Fields (비공개 필드)

        private Bounds _live2dBounds; // Live2D 캐릭터의 바운드

        // 초기 UI 위치 저장용 변수
        private Vector2 _optionPanelAnchoredPosition;
        private Vector2 _inputButtonAnchoredPosition;
        private Vector2 _talkingPanelAnchoredPosition;
        private Vector2 _lockPanelAnchoredPosition;
        private Vector2 _statusPanelAnchoredPosition;
        private Vector2 _thinkPanelAnchoredPosition; // [추가됨] ThinkPanel 초기 위치

        // UI RectTransform 캐싱
        private RectTransform _optionPanelTransform;
        private RectTransform _inputRectTransform;
        private RectTransform _talkingPanelRectTransform;
        private RectTransform _lockPanelRectTransform;
        private RectTransform _statusPanelTransform;
        private RectTransform _thinkPanelRectTransform; // [추가됨] ThinkPanel Transform 캐싱

        // 바운드 비율 계산용 변수
        private float _resultBoundsRatioX;
        private float _resultBoundsRatioY;

        // 매니저 참조
        private PanelManager _panelManager;
        private InputHandler _inputHandler;
        private SettingController _settingController;
        private GameManager _gameManager;
        private SaveController _saveController;
        private SideBarManager _sideBarManager;

        #endregion

        #region Public Fields (공개 필드)

        /// <summary>
        /// Live2D 모델의 초기 로컬 스케일
        /// </summary>
        [HideInInspector] public Vector3 originalLocalScale;

        /// <summary>
        /// Live2D 모델의 초기 로컬 사이즈 (Bounds 크기)
        /// </summary>
        [HideInInspector] public Vector3 originalLocalSize;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 매니저 참조 가져오기
            _panelManager = GameManager.Instance.PanelManager;
            _inputHandler = GameManager.Instance.InputHandler;
            _settingController = GameManager.Instance.SettingController;
            _gameManager = GameManager.Instance;
            _saveController = GameManager.Instance.SaveController;
            _sideBarManager = GameManager.Instance.SideBarManager;

            // RectTransform 캐싱
            _optionPanelTransform = _panelManager
                .Panels[(int)UISettingEnums.PanelsEnum.OptionPanel]
                .GetComponent<RectTransform>();
            _inputRectTransform = _inputHandler
                .Inputs[(int)UISettingEnums.InputEnum.InputField]
                .GetComponent<RectTransform>();
            _talkingPanelRectTransform = _panelManager
                .Panels[(int)UISettingEnums.PanelsEnum.TalikngPanel]
                .GetComponent<RectTransform>();
            _lockPanelRectTransform = _panelManager
                .Panels[(int)UISettingEnums.PanelsEnum.LockPanel]
                .GetComponent<RectTransform>();
            _statusPanelTransform = _panelManager
                .Panels[(int)UISettingEnums.PanelsEnum.StatusPanel]
                .GetComponent<RectTransform>();
                
            // [추가됨] ThinkPanel 캐싱
            _thinkPanelRectTransform = _panelManager
                .Panels[(int)UISettingEnums.PanelsEnum.ThinkPanel]
                .GetComponent<RectTransform>();

            // 초기 위치 저장
            _optionPanelAnchoredPosition = _optionPanelTransform.anchoredPosition;
            _inputButtonAnchoredPosition = _inputRectTransform.anchoredPosition;
            _talkingPanelAnchoredPosition = _talkingPanelRectTransform.anchoredPosition;
            _lockPanelAnchoredPosition = _lockPanelRectTransform.anchoredPosition;
            _statusPanelAnchoredPosition = _statusPanelTransform.anchoredPosition;
            
            // [추가됨] ThinkPanel 초기 위치 저장
            _thinkPanelAnchoredPosition = _thinkPanelRectTransform.anchoredPosition;
        }

        private void OnDrawGizmos()
        {
            // 에디터 상에서 바운드 박스 시각화 (디버그용)
            if (!Application.isPlaying || _gameManager.Live2DGameObject == null) return;
            if (_live2dBounds.size == Vector3.zero)
                _live2dBounds = CalculateBounds(_gameManager.Live2DGameObject);

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_live2dBounds.center, _live2dBounds.size);
        }

        #endregion

        #region Public Methods (공개 메서드)

        /// <summary>
        /// Live2D 모델의 크기 변화에 따른 바운드 비율을 미리 계산합니다.
        /// (초기화 시 호출)
        /// </summary>
        public void SetCharacterRatio()
        {
            float[] sidebarValues = new float[]
            {
                _sideBarManager.minValue,
                (_sideBarManager.minValue + _sideBarManager.maxValue) / 2,
                _sideBarManager.maxValue
            };

            List<float> sizeXList = new List<float>();
            List<float> sizeYList = new List<float>();

            foreach (var value in sidebarValues)
            {
                Live2dSize(value);
                var bounds = CalculateBounds(_gameManager.Live2DGameObject);
                if (SettingData.IsDebug) Debug.Log($"사이즈 보정을 위한 바운드 사이즈 ({value}): {bounds.size}");
                sizeXList.Add(bounds.size.x);
                sizeYList.Add(bounds.size.y);
            }

            float deltaX1 = sizeXList[1] - sizeXList[0];
            float deltaX2 = sizeXList[2] - sizeXList[0];
            float deltaY1 = sizeYList[1] - sizeYList[0];
            float deltaY2 = sizeYList[2] - sizeYList[0];

            float avgDeltaX = (deltaX1 + deltaX2) / 2;
            float avgDeltaY = (deltaY1 + deltaY2) / 2;

            _resultBoundsRatioX = avgDeltaX / 7.5f; // 비율 상수 (경험적 값)
            _resultBoundsRatioY = avgDeltaY / 7.5f;
        }

        /// <summary>
        /// Live2D 모델의 스케일을 조정합니다.
        /// </summary>
        /// <param name="size">크기 조절 값 (슬라이더 값 등)</param>
        public void Live2dSize(float size)
        {
            _gameManager.Live2DGameObject.transform.localScale =
                new Vector3(originalLocalScale.x + size * 150, originalLocalScale.y + size * 150, 1000);
        }

        /// <summary>
        /// Live2D 캐릭터의 전체 Bounds(경계 박스)를 계산합니다.
        /// </summary>
        public Bounds CalculateBounds(GameObject live2dObject)
        {
            var drawables = live2dObject.GetComponentsInChildren<CubismDrawable>();
            if (drawables.Length == 0)
            {
                if (SettingData.IsDebug) Debug.LogError("No CubismDrawable components found.");
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            var firstRenderer = drawables[0].GetComponent<Renderer>();
            if (firstRenderer == null)
            {
                if (SettingData.IsDebug) Debug.LogError("First CubismDrawable has no Renderer.");
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            Bounds bounds = firstRenderer.bounds;
            foreach (var drawable in drawables)
            {
                var rend = drawable.GetComponent<Renderer>();
                if (rend != null)
                    bounds.Encapsulate(rend.bounds);
            }

            return bounds;
        }

        /// <summary>
        /// 바운드 중심에서 위쪽으로 1/4 높이 만큼 이동한 지점을 계산합니다.
        /// </summary>
        public Vector3 CalculateBoundsCenter(GameObject live2dObject)
        {
            Bounds bounds = CalculateBounds(live2dObject);
            // y축으로 높이의 1/4만큼 offset
            return bounds.center + Vector3.up * (bounds.size.y * 0.25f);
        }

        /// <summary>
        /// Live2D 모델의 바운드를 다시 계산하고 UI 위치를 업데이트합니다.
        /// </summary>
        public void DrawLive2dBound()
        {
            // 현재 피벗 위치 저장 (드리프트 방지)
            Vector3 pivotPos = _gameManager.Live2DGameObject.transform.position;
            _saveController.SaveLive2DLocation(pivotPos);

            if (SettingData.IsDebug) Debug.Log($"실질 좌표 테스트 시작 : {pivotPos}");

            // UI 배치 및 스케일링 업데이트
            UpdateUIElementPositionAndSize(pivotPos);
        }

        #endregion

        #region Private Methods (비공개 메서드)

        /// <summary>
        /// UI 요소를 Live2D 모델의 바운드에 맞게 배치하고 스케일을 조정합니다.
        /// </summary>
        private void UpdateUIElementPositionAndSize(Vector3 pivotPos)
        {
            if (_settingController.GuiCanvas == null)
            {
                if (SettingData.IsDebug) Debug.LogError("GUI Canvas is not assigned.");
                return;
            }

            RectTransform canvasRect = _settingController.GuiCanvas.GetComponent<RectTransform>();

            _live2dBounds.center = pivotPos;
            Vector3 calSize = new Vector3(
                originalLocalSize.x + (_sideBarManager.live2dSizeValue * _resultBoundsRatioX),
                originalLocalSize.y + (_sideBarManager.live2dSizeValue * _resultBoundsRatioY),
                originalLocalSize.z
            );
            _live2dBounds.size = calSize;

            if (SettingData.IsDebug)
                Debug.Log(
                    $"바운드 테스트 시작 / 사이즈 : {_live2dBounds.size} / 중심 좌표 : {_live2dBounds.center} / 스크롤바에 나오는 사이즈 밸류 : {_sideBarManager.live2dSizeValue}");

            // 설정창이나 로그창이 열려있는지 확인
            bool isSettingOrLogActive =
                _panelManager.Panels[(int)UISettingEnums.PanelsEnum.SettingPanel].activeSelf ||
                _panelManager.Panels[(int)UISettingEnums.PanelsEnum.LOGPanel].activeSelf;

            float scaleRight = isSettingOrLogActive ? 2.2f : 1.5f;
            float scaleLeft =  1.5f; 
            float scaleY = 1f;

            if(SettingData.IsDebug) Debug.Log(isSettingOrLogActive
                ? "설정 혹은 로그 패널이 켜져있습니다."
                : "설정 혹은 로그 패널이 안켜져있습니다.");

            // --- 계산 로직 ---
            float originalWidth = _live2dBounds.size.x;
            float widthAddedRight = originalWidth * (scaleRight - 1f); 
            float widthAddedLeft  = originalWidth * (scaleLeft - 1f);

            Vector3 newSize = new Vector3(
                originalWidth + widthAddedRight + widthAddedLeft, 
                _live2dBounds.size.y * scaleY,
                _live2dBounds.size.z
            );

            Vector3 newCenter = _live2dBounds.center;
            newCenter.x += (widthAddedRight / 2f) - (widthAddedLeft / 2f);
            newCenter.y += (newSize.y - _live2dBounds.size.y) / 2f;

            canvasRect.position = newCenter;
            canvasRect.sizeDelta = new Vector2(newSize.x, newSize.y);

            // 자식 UI 요소들의 스케일 조정
            Vector3 targetScale = _gameManager.Live2DGameObject.transform.localScale / 1000f;
            SetScaleRecursive(_settingController.GuiCanvas.transform, targetScale);

            // 부속 패널 위치 재조정
            _optionPanelTransform.anchoredPosition = isSettingOrLogActive
                ? _optionPanelAnchoredPosition * targetScale.x
                : new Vector2(-50f, -70f) * targetScale.x;

            _statusPanelTransform.anchoredPosition = isSettingOrLogActive
                ? _statusPanelAnchoredPosition * targetScale.x
                : Vector2.zero * targetScale.x;

            // 스케일 계산에 사용될 비율 캐싱
            float settingPanelScaleX = _panelManager.Panels[(int)UISettingEnums.PanelsEnum.SettingPanel].transform.localScale.x;

            _inputRectTransform.anchoredPosition = _inputButtonAnchoredPosition * settingPanelScaleX;
            _talkingPanelRectTransform.anchoredPosition = _talkingPanelAnchoredPosition * settingPanelScaleX;
            _lockPanelRectTransform.anchoredPosition = _lockPanelAnchoredPosition * settingPanelScaleX;
            
            // [추가됨] ThinkPanel 위치 재조정 (TalkingPanel과 동일한 방식 적용)
            _thinkPanelRectTransform.anchoredPosition = _thinkPanelAnchoredPosition * settingPanelScaleX;
        }

        /// <summary>
        /// Transform 하위의 모든 자식 요소들의 스케일을 재귀적으로 설정합니다.
        /// </summary>
        private void SetScaleRecursive(Transform parent, Vector3 scale)
        {
            foreach (Transform child in parent)
                child.localScale = scale;
        }

        #endregion
    }
}