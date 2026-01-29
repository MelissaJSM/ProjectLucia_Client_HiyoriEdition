using System;
using System.Collections.Generic;
using System.Reflection;
using Live2D.Cubism.Framework.Expression;
using Live2D.Cubism.Framework.Motion;
using ProjectLucia.GUI;
using ProjectLucia.Status;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectLucia.Live2D
{
    /// <summary>
    /// Live2D 모델의 감정 표현(Expression)과 모션(Motion)을 제어하는 컨트롤러입니다.
    /// Cubism SDK의 버전 차이에 대응하기 위해 리플렉션을 사용하여 모션을 재생합니다.
    /// </summary>
    public class EmotialController : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Header("Motion Clips")]
        [Tooltip("대기(Idle) 상태에서 재생할 애니메이션 클립 배열")]
        [SerializeField] private AnimationClip[] idleAnimationClips;

        [Tooltip("터치 반응 시 재생할 애니메이션 클립 배열")]
        [SerializeField] private AnimationClip[] touchAnimationClips;
        public AnimationClip[] TouchAnimationClips => touchAnimationClips;

        [Header("Motion Defaults")]
        [Tooltip("기본 모션 우선순위 (SDK 호환성을 위해 내부 Enum 사용)")]
        [SerializeField] private EmotePriority defaultPriority = EmotePriority.Force;

        [Tooltip("모션을 재생할 레이어 인덱스")]
        [SerializeField] private int motionLayerIndex;

        [Tooltip("모션 루프(반복 재생) 여부")]
        [SerializeField] private bool defaultLoop;

        [Tooltip("모션 재생 속도 (0.01 이상)")]
        [SerializeField, Min(0.01f)] private float defaultSpeed = 1f;

        #endregion

        #region Constants & Enums (상수 및 열거형)

        /// <summary>
        /// Cubism SDK의 Priority와 호환되는 내부 우선순위 열거형
        /// </summary>
        public enum EmotePriority
        {
            None   = 0,
            Idle   = 1,
            Normal = 2,
            Force  = 3
        }

        // 감정 문자열과 Live2D 표현 인덱스 매핑
        private static readonly Dictionary<string, int> EmotionMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Angry", (int)Live2DEnums.Live2DList.Angry },
            { "Fear", (int)Live2DEnums.Live2DList.Sad },
            { "Sad", (int)Live2DEnums.Live2DList.Sad },
            { "Happy", (int)Live2DEnums.Live2DList.Happy },
            { "Tender", (int)Live2DEnums.Live2DList.Happy },
            { "Loading", (int)Live2DEnums.Live2DList.Loading },
            { "Idle", (int)Live2DEnums.Live2DList.Idle },
            { "Neutral", (int)Live2DEnums.Live2DList.Idle }
        };

        #endregion

        #region Private Fields (비공개 필드)

        private Component _motionController; // CubismMotionController (리플렉션용)
        private CubismExpressionController _expressionController;
        private GameManager _gameManager;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            _gameManager = GameManager.Instance;
        }

        private void Start()
        {
            if (_gameManager == null || _gameManager.Live2DGameObject == null)
            {
                if(SettingData.IsDebug) Debug.LogError("[EmotialController] GameManager 또는 Live2DGameObject가 없습니다.");
                return;
            }

            // 컴포넌트 가져오기
            _motionController = _gameManager.Live2DGameObject.GetComponent<CubismMotionController>();
            _expressionController = _gameManager.Live2DGameObject.GetComponent<CubismExpressionController>();

            if (_motionController == null)
            {
                if(SettingData.IsDebug) Debug.LogError("[EmotialController] CubismMotionController가 부착되어 있지 않습니다.");
                return;
            }

            // 애니메이션 종료 이벤트 구독 (리플렉션 사용)
            TrySubscribeAnimationEnd(_motionController, true);
        }

        private void OnDestroy()
        {
            // 이벤트 구독 해제
            TrySubscribeAnimationEnd(_motionController, false);
        }

        #endregion

        #region Public Methods (공개 메서드)

        /// <summary>
        /// 지정된 애니메이션 클립을 재생합니다.
        /// </summary>
        /// <param name="motionClip">재생할 애니메이션 클립</param>
        /// <param name="priorityOverride">우선순위 (null이면 기본값 사용)</param>
        /// <param name="isLoop">루프 여부 (null이면 기본값 사용)</param>
        /// <param name="speed">재생 속도 (null이면 기본값 사용)</param>
        /// <param name="layerIndex">레이어 인덱스 (null이면 기본값 사용)</param>
        public void PlayMotion(
            AnimationClip motionClip,
            EmotePriority? priorityOverride = null,
            bool? isLoop = null,
            float? speed = null,
            int? layerIndex = null
        )
        {
            if (_motionController == null)
            {
                if(SettingData.IsDebug) Debug.LogWarning("[EmotialController] MotionController가 없습니다.");
                return;
            }
            if (motionClip == null)
            {
                if(SettingData.IsDebug) Debug.LogWarning("[EmotialController] 재생할 MotionClip이 null입니다.");
                return;
            }

            // 파라미터 설정 (기본값 적용)
            int pr = (int)(priorityOverride ?? defaultPriority);
            bool lp = isLoop ?? defaultLoop;
            float sp = Mathf.Max(0.01f, speed ?? defaultSpeed);
            int li = layerIndex ?? motionLayerIndex;

            // 리플렉션을 통해 PlayAnimation 호출 (SDK 호환성)
            if (!InvokePlayAnimationCompat(_motionController, motionClip, li, pr, lp, sp))
            {
                if(SettingData.IsDebug) Debug.LogError("[EmotialController] PlayAnimation 메서드를 찾을 수 없습니다. (SDK 버전 확인 필요)");
            }
        }

        /// <summary>
        /// 등록된 Idle 애니메이션 중 하나를 무작위로 재생합니다.
        /// </summary>
        public void RandomIdleMotion()
        {
            if (idleAnimationClips == null || idleAnimationClips.Length == 0)
            {
                if(SettingData.IsDebug) Debug.LogWarning("[EmotialController] IdleAnimationClips가 비어있습니다.");
                return;
            }
            int idx = Random.Range(0, idleAnimationClips.Length);
            PlayMotion(idleAnimationClips[idx]);
        }

        /// <summary>
        /// 현재 재생 중인 모든 모션을 정지합니다.
        /// </summary>
        public void StopMotion()
        {
            if (_motionController == null) return;
            var mi = _motionController.GetType().GetMethod("StopAllAnimation", BindingFlags.Instance | BindingFlags.Public);
            mi?.Invoke(_motionController, null);
        }

        /// <summary>
        /// Live2D 모델의 표정(Expression)을 업데이트합니다.
        /// </summary>
        /// <param name="emotion">감정 문자열 (예: "Happy", "Sad")</param>
        public void UpdateLive2DExpression(string emotion)
        {
            if (_expressionController == null)
            {
                // 런타임 재탐색
                _expressionController = _gameManager?.Live2DGameObject?.GetComponent<CubismExpressionController>();
                if (_expressionController == null)
                {
                    if(SettingData.IsDebug) Debug.LogWarning("[EmotialController] CubismExpressionController가 없습니다.");
                    return;
                }
            }

            if (string.IsNullOrEmpty(emotion))
            {
                _expressionController.CurrentExpressionIndex = (int)Live2DEnums.Live2DList.Idle;
                return;
            }

            // 감정 매핑 확인 및 적용
            if (EmotionMap.TryGetValue(emotion, out int index))
            {
                _expressionController.CurrentExpressionIndex = index;
            }
            else
            {
                // 매핑되지 않은 감정은 기본값(Idle)으로 처리
                _expressionController.CurrentExpressionIndex = (int)Live2DEnums.Live2DList.Idle;
            }
        }

        #endregion

        #region Internal Reflection Logic (내부 리플렉션 로직)

        /// <summary>
        /// Cubism SDK 버전에 따라 다른 PlayAnimation 메서드 시그니처를 찾아 호출합니다.
        /// (int 우선순위 버전과 Enum 우선순위 버전을 모두 지원)
        /// </summary>
        private static bool InvokePlayAnimationCompat(
            Component motionController,
            AnimationClip clip,
            int layerIndex,
            int priorityInt,
            bool isLoop,
            float speed
        )
        {
            var t = motionController.GetType();

            // 1. int priority 버전 탐색
            var miInt = t.GetMethod(
                "PlayAnimation",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(AnimationClip), typeof(int), typeof(int), typeof(bool), typeof(float) },
                null);

            if (miInt != null)
            {
                miInt.Invoke(motionController, new object[] { clip, layerIndex, priorityInt, isLoop, speed });
                return true;
            }

            // 2. Enum priority 버전 탐색 (CubismMotionPriority)
            var cubismPriorityType = Type.GetType("Live2D.Cubism.Framework.Motion.CubismMotionPriority, Live2D.Cubism.Framework");
            if (cubismPriorityType is { IsEnum: true })
            {
                var miEnum = t.GetMethod(
                    "PlayAnimation",
                    BindingFlags.Instance | BindingFlags.Public,
                    null,
                    new[] { typeof(AnimationClip), typeof(int), cubismPriorityType, typeof(bool), typeof(float) },
                    null);

                if (miEnum != null)
                {
                    // int -> Enum 변환
                    object enumBoxed = Enum.ToObject(cubismPriorityType, priorityInt);
                    miEnum.Invoke(motionController, new[] { clip, layerIndex, enumBoxed, isLoop, speed });
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// AnimationEndHandler 이벤트를 리플렉션으로 찾아 구독하거나 해제합니다.
        /// </summary>
        private static void TrySubscribeAnimationEnd(Component motionController, bool subscribe)
        {
            if (motionController == null) return;

            var t = motionController.GetType();
            var evt = t.GetEvent("AnimationEndHandler", BindingFlags.Instance | BindingFlags.Public);
            if (evt == null) return;

            // 정적 메서드를 델리게이트로 변환하여 이벤트 핸들러로 등록
            var handler = Delegate.CreateDelegate(evt.EventHandlerType, typeof(EmotialController).GetMethod(nameof(StaticAnimationEndRelay), BindingFlags.NonPublic | BindingFlags.Static) ?? throw new InvalidOperationException());
            
            if (subscribe) evt.AddEventHandler(motionController, handler);
            else evt.RemoveEventHandler(motionController, handler);
        }

        /// <summary>
        /// 애니메이션 종료 시 호출되는 정적 릴레이 메서드입니다.
        /// </summary>
        // ReSharper disable once UnusedParameter.Local
        private static void StaticAnimationEndRelay(int instanceId)
        {
            // 필요 시 애니메이션 종료 후처리 로직 추가 가능
        }

        #endregion
    }
}
