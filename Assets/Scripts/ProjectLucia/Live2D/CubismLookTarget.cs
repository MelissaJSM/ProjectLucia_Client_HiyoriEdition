using Live2D.Cubism.Core;
using Live2D.Cubism.Framework.LookAt;
using UnityEngine;
using ProjectLucia.GUI;
using ProjectLucia.Status;

namespace ProjectLucia.Live2D
{
    /// <summary>
    /// Live2D 모델의 시선(LookAt) 목표 지점을 계산하는 클래스입니다.
    /// 마우스 위치를 추적하여 모델이 마우스를 바라보도록 합니다.
    /// </summary>
    public class CubismLookTarget : MonoBehaviour, ICubismLookTarget
    {
        #region Inspector Fields (인스펙터 설정)

        [Header("Anchor / Plane")]
        [Tooltip("시선 추적의 기준이 되는 앵커 Transform (보통 모델 앞쪽 평면)")]
        [SerializeField] private Transform live2dCorsor;

        [Tooltip("마우스 좌표를 월드 좌표로 변환할 때 사용할 Z축 오프셋")]
        [SerializeField] private float planeZOffset;

        [Header("Smoothing (부드러운 이동)")]
        [Tooltip("시선 이동 스무딩 사용 여부")]
        [SerializeField] private bool useSmoothing = true;

        [Tooltip("스무딩 강도 (0.0 ~ 1.0, 낮을수록 느리고 부드러움)")]
        [SerializeField, Range(0.0f, 1.0f)] private float smoothFactor = 0.2f;

        [Header("Clamp (시선 범위 제한)")]
        [Tooltip("시선 이동 범위를 제한할지 여부")]
        [SerializeField] private bool clampLook;

        [Tooltip("X축 시선 제한 범위 (로컬 좌표 기준)")]
        [SerializeField] private Vector2 clampX = new Vector2(-1.5f, 1.5f);

        [Tooltip("Y축 시선 제한 범위 (로컬 좌표 기준)")]
        [SerializeField] private Vector2 clampY = new Vector2(-1.0f, 1.0f);

        #endregion

        #region Private Fields (비공개 필드)

        private PanelManager _panelManager;
        private GameManager _gameManager;
        private Camera _cam;
        private Vector3 _smoothedWorldTarget;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 매니저 참조 가져오기
            _panelManager = GameManager.Instance.PanelManager;
            _gameManager  = GameManager.Instance;

            // 메인 카메라 캐싱
            _cam = Camera.main;
            if (_cam == null)
            {
                if(SettingData.IsDebug) Debug.LogWarning("CubismLookTarget: Camera.main 이 null 입니다. 현재 카메라를 가져옵니다.");
                _cam = Camera.current;
            }

            // 앵커 확인
            if (live2dCorsor == null)
            {
                if(SettingData.IsDebug) Debug.LogError("CubismLookTarget: live2dCorsor(시선 앵커)가 지정되지 않았습니다.");
            }
        }

        #endregion

        #region ICubismLookTarget Implementation (인터페이스 구현)

        /// <summary>
        /// Live2D 모델이 바라볼 목표 지점(월드 좌표)을 반환합니다.
        /// </summary>
        /// <returns>목표 지점 Vector3</returns>
        public Vector3 GetPosition()
        {
            // 시선 추적 비활성화 조건 체크
            if (!IsActive())
            {
                // 비활성 시 정면(앵커) 또는 모델 위치 반환
                return live2dCorsor != null ? live2dCorsor.position : _gameManager.Live2DGameObject.transform.position;
            }

            if (_cam == null || live2dCorsor == null)
            {
                return _gameManager.Live2DGameObject.transform.position;
            }

            // 마우스 좌표를 월드 좌표로 변환 (Legacy Input 사용)
            float targetZ = live2dCorsor.position.z + planeZOffset;
            Vector3 screenPos = Input.mousePosition; 
            
            // 카메라로부터의 거리 계산
            Vector3 mousePos = new Vector3(screenPos.x, screenPos.y, targetZ - _cam.transform.position.z);
            var worldTarget = _cam.ScreenToWorldPoint(mousePos);

            // 시선 범위 제한 (Clamp)
            if (clampLook)
            {
                var local = live2dCorsor.InverseTransformPoint(worldTarget);
                local.x = Mathf.Clamp(local.x, clampX.x, clampX.y);
                local.y = Mathf.Clamp(local.y, clampY.x, clampY.y);
                worldTarget = live2dCorsor.TransformPoint(local);
            }

            // 스무딩 적용 (Smoothing)
            if (useSmoothing)
            {
                _smoothedWorldTarget = Vector3.Lerp(
                    _smoothedWorldTarget == Vector3.zero ? worldTarget : _smoothedWorldTarget,
                    worldTarget,
                    smoothFactor
                );
                return _smoothedWorldTarget;
            }

            return worldTarget;
        }

        /// <summary>
        /// 시선 추적 활성화 여부를 확인합니다.
        /// 설정창, 로그창, 잠금 화면 등이 열려있거나 설정에서 꺼져있으면 false를 반환합니다.
        /// </summary>
        /// <returns>활성화 여부</returns>
        public bool IsActive()
        {
            bool uiBlocked =
                _panelManager.Panels[(int)UISettingEnums.PanelsEnum.SettingPanel].activeSelf ||
                _panelManager.Panels[(int)UISettingEnums.PanelsEnum.LOGPanel].activeSelf  ||
                _panelManager.Panels[(int)UISettingEnums.PanelsEnum.LockPanel].activeSelf;

            return !uiBlocked && SettingData.IsLookTarget;
        }

        #endregion

        #region Public Methods (공개 메서드)

        /// <summary>
        /// Live2D 모델의 파라미터를 초기값으로 리셋합니다.
        /// </summary>
        public void ResetParameters()
        {
            var cubismModel = _gameManager.Live2DGameObject.GetComponent<CubismModel>();
            if (cubismModel == null)
            {
                if(SettingData.IsDebug) Debug.LogError("CubismModel이 존재하지 않습니다.");
                return;
            }

            foreach (var parameter in cubismModel.Parameters)
                parameter.Value = parameter.DefaultValue;

            cubismModel.ForceUpdateNow();
            if(SettingData.IsDebug) Debug.Log("모든 CubismParameter 초기화 완료.");
        }

        #endregion
    }
}
