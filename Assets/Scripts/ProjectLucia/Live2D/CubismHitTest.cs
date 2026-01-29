using System;
using System.Collections.Generic;
using Live2D.Cubism.Framework.Raycasting;
using UnityEngine;
using UnityEngine.EventSystems;
using ProjectLucia.GUI;
using ProjectLucia.Server;
using ProjectLucia.Status;
using Random = UnityEngine.Random;

namespace ProjectLucia.Live2D
{
    /// <summary>
    /// Live2D 모델의 클릭 및 드래그 상호작용을 처리하는 클래스입니다.
    /// 특정 파츠(Drawable)에 대한 터치 반응(감정 모션)과 모델 이동(드래그) 기능을 제공합니다.
    /// </summary>
    public class CubismHitTest : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Header("Whitelist Settings (상호작용 허용 목록)")]
        [Tooltip("터치 시 감정 모션 반응을 허용할 Live2D Drawable(파츠) 이름 목록")]
        [SerializeField] private string[] touchableDrawables;

        [Tooltip("드래그 시 모델 이동을 허용할 Live2D Drawable(파츠) 이름 목록")]
        [SerializeField] private string[] draggableDrawables;

        [Tooltip("터치 화이트리스트 사용 여부 (false면 모든 파츠 터치 가능)")]
        [SerializeField] private bool useTouchableWhitelist = true;

        [Tooltip("드래그 화이트리스트 사용 여부 (false면 모든 파츠 드래그 가능)")]
        [SerializeField] private bool useDraggableWhitelist = true;

        #endregion

        #region Private Fields (비공개 필드)

        // 화이트리스트 해시셋 (검색 최적화)
        private HashSet<string> _touchableSet;
        private HashSet<string> _draggableSet;

        // 드래그 및 클릭 상태 변수
        private bool _isDragging;                 
        private Vector3 _offset;                  
        private bool _isClicking;                 
        private Vector3 _initialMousePosition;    
        private const float DragThreshold = 0.1f;
        private bool _uiClicked;

        // 매니저 참조
        private TextManager _textManager;
        private CanvasBoundsChecker _canvasBoundsChecker;
        private Live2DButtonPosition _buttonPosition;
        private EmotialController _emotialController;
        private PanelManager _panelManager;
        private GameManager _gameManager;
        private SettingController _settingController;
        private LogController _logController;

        // 레이캐스트 재사용 버퍼 (GC 할당 최소화)
        private readonly CubismRaycastHit[] _hits = new CubismRaycastHit[8];

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 매니저 참조 가져오기
            _textManager          = GameManager.Instance.TextManager;
            _canvasBoundsChecker  = GameManager.Instance.CanvasBoundsChecker;
            _buttonPosition       = GameManager.Instance.Live2DButtonPosition;
            _emotialController    = GameManager.Instance.EmotialController;
            _panelManager         = GameManager.Instance.PanelManager;
            _gameManager          = GameManager.Instance;
            _settingController    = GameManager.Instance.SettingController;
            _logController        = GameManager.Instance.LogController;

            // 화이트리스트 초기화
            _touchableSet = new HashSet<string>(touchableDrawables ?? Array.Empty<string>());
            _draggableSet = new HashSet<string>(draggableDrawables ?? Array.Empty<string>());
        }

        private void Update()
        {
            // 마우스 좌표 가져오기 (Legacy Input 사용 - 투명 윈도우 호환성)
            var mousePosition = Input.mousePosition;

            // 디버그용 좌표 표시
            if (_textManager?.Texts != null)
            {
                _textManager.Texts[(int)UISettingEnums.TextsEnum.MouseCoordText].text =
                    $"X = {mousePosition.x}, Y = {mousePosition.y}";
            }

            // CanvasBoundsChecker에 마우스 좌표 전달
            _canvasBoundsChecker.MouseX = mousePosition.x;
            _canvasBoundsChecker.MouseY = mousePosition.y;

            // 실시간 히트 테스트 (디버그 텍스트 출력용)
            PerformClick(true);

            // 좌클릭 시작 (UI 버튼 위가 아닐 때만)
            if (Input.GetMouseButton(0) && !_canvasBoundsChecker.IsMouseOverButton)
            {
                if (!_isClicking)
                {
                    _initialMousePosition = mousePosition;
                    _isClicking = true;
                }
            }

            // 클릭 유지 중: 드래그 전환 판단
            if (_isClicking && !_isDragging && Input.GetMouseButton(0))
            {
                var delta = mousePosition - _initialMousePosition;
                if (delta.sqrMagnitude > (DragThreshold * DragThreshold))
                {
                    StartDragging();
                }
            }

            // 좌클릭 뗌
            if (Input.GetMouseButtonUp(0))
            {
                if (_isDragging && !_uiClicked)
                {
                    StopDragging();
                    _buttonPosition.DrawLive2dBound(); // 버튼 위치 재계산
                }
                else if (_isClicking && !_uiClicked)
                {
                    // 단순 클릭 처리 (모션 재생)
                    PerformClick(false);
                }

                // 상태 초기화
                _isClicking = false;
                _uiClicked = false;
            }

            // 드래그 중이면 위치 업데이트
            if (_isDragging) PerformDrag();
        }

        #endregion

        #region Public Methods (공개 메서드)

        /// <summary>
        /// UI 요소가 클릭되었음을 알립니다. (모델 클릭/드래그 방지용)
        /// </summary>
        public void UiClick()
        {
            _uiClicked = true;
        }

        #endregion

        #region Interaction Logic (상호작용 로직)

        /// <summary>
        /// 클릭 히트 테스트를 수행하고, 조건에 따라 모션을 재생하거나 디버그 정보를 출력합니다.
        /// </summary>
        /// <param name="realtime">true면 디버그 정보만 갱신, false면 실제 클릭 동작 수행</param>
        private void PerformClick(bool realtime)
        {
            if (Camera.main != null)
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                int hitCount = _canvasBoundsChecker.Raycaster.Raycast(ray, _hits);

                if (realtime)
                {
                    // 실시간 디버그 정보 출력
                    if (_textManager?.Texts != null)
                    {
                        var buf = $"Hit Count: {hitCount}\n";
                        for (int i = 0; i < hitCount; i++)
                        {
                            var d = _hits[i].Drawable;
                            if (d != null) buf += $"{d.name} / ";
                        }
                        _textManager.Texts[(int)UISettingEnums.TextsEnum.MouseCoordText].text += "\n" + buf;
                    }
                    return;
                }

                // 실제 클릭 처리 (조건 검사)
                if (IsPointerOverUIObject()) return;
                if (_settingController.SettingsOpen) return;
                if (_logController.LogsOpen) return;
                if (_panelManager.Panels[(int)UISettingEnums.PanelsEnum.LockPanel].gameObject.activeSelf) return;
                if (!SettingData.L2dClicked) return;

                // 히트된 파츠 확인 및 모션 재생
                for (int i = 0; i < hitCount; i++)
                {
                    var d = _hits[i].Drawable;
                    if (d == null) continue;

                    // 화이트리스트 체크
                    if (useTouchableWhitelist && _touchableSet.Count > 0)
                    {
                        if (!_touchableSet.Contains(d.name)) continue;
                    }

                    // 모션 재생
                    if (_emotialController.TouchAnimationClips is { Length: > 0 })
                    {
                        int randomIndex = Random.Range(0, _emotialController.TouchAnimationClips.Length);
                        _emotialController.PlayMotion(_emotialController.TouchAnimationClips[randomIndex]);
                    }
                    break;
                }

                // 히트 결과 디버그 텍스트 갱신
                if (_textManager?.Texts != null)
                {
                    var resultsText = "Hit:\n";
                    for (int i = 0; i < hitCount; i++)
                    {
                        var d = _hits[i].Drawable;
                        if (d != null) resultsText += $"{d.name} / ";
                    }
                    _textManager.Texts[(int)UISettingEnums.TextsEnum.HitText].text = resultsText;
                }
            }
        }

        /// <summary>
        /// 드래그를 시작합니다. (조건 검사 및 오프셋 계산)
        /// </summary>
        private void StartDragging()
        {
            if (_uiClicked) return;
            if (IsPointerOverUIObject()) return;
            if (_panelManager.Panels[(int)UISettingEnums.PanelsEnum.TalikngPanel].activeSelf) return;
            
            if (Camera.main != null)
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                int hitCount = _canvasBoundsChecker.Raycaster.Raycast(ray, _hits);

                if (hitCount <= 0) return;

                for (int i = 0; i < hitCount; i++)
                {
                    var d = _hits[i].Drawable;
                    if (d == null) continue;

                    // 드래그 화이트리스트 체크
                    if (useDraggableWhitelist && _draggableSet.Count > 0)
                    {
                        if (!_draggableSet.Contains(d.name)) continue;
                    }

                    // 드래그 시작
                    _isDragging = true;

                    var objPos = _gameManager.Live2DGameObject.transform.position;
                
                    // 마우스 월드 좌표 계산 (Z축 보정)
                    var mouseWorld = Camera.main.ScreenToWorldPoint(new Vector3(
                        Input.mousePosition.x, Input.mousePosition.y,
                        objPos.z - Camera.main.transform.position.z));

                    _offset = objPos - mouseWorld;
                    break;
                }
            }
        }

        /// <summary>
        /// 드래그 중 모델 위치를 업데이트합니다.
        /// </summary>
        private void PerformDrag()
        {
            var obj = _gameManager.Live2DGameObject;
            
            if (Camera.main != null)
            {
                var mouseWorld = Camera.main.ScreenToWorldPoint(new Vector3(
                    Input.mousePosition.x, Input.mousePosition.y,
                    obj.transform.position.z - Camera.main.transform.position.z));

                obj.transform.position = mouseWorld + _offset;
            }
        }

        /// <summary>
        /// 드래그를 종료합니다.
        /// </summary>
        private void StopDragging()
        {
            _isDragging = false;
        }

        /// <summary>
        /// 마우스 포인터가 UI 위에 있는지 확인합니다.
        /// </summary>
        private bool IsPointerOverUIObject()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        #endregion
    }
}
