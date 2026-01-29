using System;
using System.Collections;
using System.Collections.Generic;
using ProjectLucia.Status;
using UnityEngine;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// UI 요소의 페이드 인/아웃 효과를 안전하고 부드럽게 제어하는 컨트롤러입니다.
    /// CanvasGroup을 우선적으로 사용하며, 필요 시 자동으로 추가합니다.
    /// 중복 실행 방지 및 현재 알파값에서의 자연스러운 전환을 지원합니다.
    /// </summary>
    public class FadeController : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("UI 페이드 시 Time.timeScale의 영향을 받지 않는 Unscaled Time 사용 여부")]
        [SerializeField] private bool useUnscaledTime = true; 

        [Tooltip("알파값 차이가 이 값 이하일 경우 즉시 완료 처리하는 임계값")]
        [SerializeField, Range(0f, 1f)] private float epsilon = 0.001f;

        #endregion

        #region Private Fields (비공개 필드)

        /// <summary>
        /// 각 타겟 오브젝트별로 현재 실행 중인 페이드 코루틴을 추적하는 딕셔너리
        /// </summary>
        private readonly Dictionary<GameObject, Coroutine> _running = new Dictionary<GameObject, Coroutine>();

        #endregion

        #region Public Methods (공개 메서드)

        /// <summary>
        /// 대상 오브젝트를 페이드 인(알파 1로 전환)합니다.
        /// </summary>
        /// <param name="target">대상 게임 오브젝트</param>
        /// <param name="duration">페이드 소요 시간 (초)</param>
        /// <param name="onComplete">완료 시 실행할 콜백 (선택)</param>
        /// <param name="interactable">CanvasGroup의 interactable 설정 (null이면 유지)</param>
        /// <param name="blocksRaycasts">CanvasGroup의 blocksRaycasts 설정 (null이면 유지)</param>
        public void FadeIn(GameObject target, float duration, Action onComplete = null, bool? interactable = null, bool? blocksRaycasts = null)
        {
            if (!ValidateTarget(target)) return;
            StartFade(target, toAlpha: 1f, duration, onComplete, interactable, blocksRaycasts);
        }

        /// <summary>
        /// 대상 오브젝트를 페이드 아웃(알파 0으로 전환)합니다.
        /// </summary>
        /// <param name="target">대상 게임 오브젝트</param>
        /// <param name="duration">페이드 소요 시간 (초)</param>
        /// <param name="onComplete">완료 시 실행할 콜백 (선택)</param>
        /// <param name="interactable">CanvasGroup의 interactable 설정 (null이면 유지)</param>
        /// <param name="blocksRaycasts">CanvasGroup의 blocksRaycasts 설정 (null이면 유지)</param>
        public void FadeOut(GameObject target, float duration, Action onComplete = null, bool? interactable = null, bool? blocksRaycasts = null)
        {
            if (!ValidateTarget(target)) return;
            StartFade(target, toAlpha: 0f, duration, onComplete, interactable, blocksRaycasts);
        }

        /// <summary>
        /// 대상 오브젝트의 알파값과 상호작용 상태를 즉시 설정합니다. (페이드 효과 없음)
        /// </summary>
        /// <param name="target">대상 게임 오브젝트</param>
        /// <param name="alpha">설정할 알파값 (0~1)</param>
        /// <param name="interactable">CanvasGroup의 interactable 설정 (null이면 유지)</param>
        /// <param name="blocksRaycasts">CanvasGroup의 blocksRaycasts 설정 (null이면 유지)</param>
        public void SetAlphaImmediate(GameObject target, float alpha, bool? interactable = null, bool? blocksRaycasts = null)
        {
            if (!ValidateTarget(target)) return;

            var (cg, cr) = GetDrivers(target, addCanvasGroupIfMissing: true);

            if (cg != null)
            {
                cg.alpha = Mathf.Clamp01(alpha);
                if (interactable.HasValue) cg.interactable = interactable.Value;
                if (blocksRaycasts.HasValue) cg.blocksRaycasts = blocksRaycasts.Value;
            }
            else if (cr != null)
            {
                cr.SetAlpha(Mathf.Clamp01(alpha));
            }
        }

        #endregion

        #region Internal Logic (내부 로직)

        /// <summary>
        /// 페이드 코루틴을 시작합니다. 이미 실행 중인 코루틴이 있다면 중지합니다.
        /// </summary>
        private void StartFade(GameObject target, float toAlpha, float duration, Action onComplete, bool? interactable, bool? blocksRaycasts)
        {
            // 기존 페이드가 있으면 중지하여 충돌 방지
            if (_running.TryGetValue(target, out var co) && co != null)
            {
                StopCoroutine(co);
                _running[target] = null;
            }

            var routine = StartCoroutine(CoFade(target, toAlpha, duration, onComplete, interactable, blocksRaycasts));
            _running[target] = routine;
        }

        /// <summary>
        /// 실제 페이드 애니메이션을 수행하는 코루틴입니다.
        /// </summary>
        private IEnumerator CoFade(GameObject target, float toAlpha, float duration, Action onComplete, bool? interactable, bool? blocksRaycasts)
        {
            var (cg, cr) = GetDrivers(target, addCanvasGroupIfMissing: true);
            if (cg == null && cr == null) yield break;

            // 현재 알파값 가져오기 헬퍼
            float GetAlpha() => cg != null ? cg.alpha : cr.GetAlpha();
            
            // 알파값 설정 헬퍼
            void SetAlpha(float a)
            {
                if (cg != null) cg.alpha = a;
                else cr.SetAlpha(a);
            }

            float from = GetAlpha();
            toAlpha = Mathf.Clamp01(toAlpha);
            duration = Mathf.Max(0f, duration);

            // 목표값과 현재값의 차이가 미세하거나 시간이 0이면 즉시 완료 처리
            if (duration <= 0f || Mathf.Abs(from - toAlpha) <= epsilon)
            {
                SetAlpha(toAlpha);
                if (cg != null)
                {
                    if (interactable.HasValue)   cg.interactable   = interactable.Value;
                    if (blocksRaycasts.HasValue) cg.blocksRaycasts = blocksRaycasts.Value;
                }
                onComplete?.Invoke();
                yield break;
            }

            // 페이드 시작 시 상호작용 설정 적용 (명시된 경우)
            if (cg != null)
            {
                if (interactable.HasValue)   cg.interactable   = interactable.Value;
                if (blocksRaycasts.HasValue) cg.blocksRaycasts = blocksRaycasts.Value;
            }

            // 페이드 루프
            float t = 0f;
            while (t < duration)
            {
                t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float lerp = Mathf.Clamp01(t / duration);
                SetAlpha(Mathf.Lerp(from, toAlpha, lerp));
                yield return null;
            }

            // 최종값 보정 및 완료 콜백
            SetAlpha(toAlpha);
            onComplete?.Invoke();
        }

        #endregion

        #region Helper Methods (보조 메서드)

        /// <summary>
        /// 타겟 오브젝트가 유효한지 검사합니다.
        /// </summary>
        private static bool ValidateTarget(GameObject target)
        {
            if (target != null) return true;
            if(SettingData.IsDebug) Debug.LogError("[FadeController] 대상 GameObject가 null 입니다.");
            return false;
        }

        /// <summary>
        /// 타겟에서 CanvasGroup 또는 CanvasRenderer를 가져옵니다.
        /// CanvasGroup이 없고 addCanvasGroupIfMissing이 true면 새로 추가합니다.
        /// </summary>
        private static (CanvasGroup cg, CanvasRenderer cr) GetDrivers(GameObject target, bool addCanvasGroupIfMissing)
        {
            var cg = target.GetComponent<CanvasGroup>();
            if (cg == null && addCanvasGroupIfMissing)
                cg = target.AddComponent<CanvasGroup>();

            CanvasRenderer cr = null;
            if (cg == null) // CanvasGroup이 전혀 없다면 CanvasRenderer로 폴백 (드문 경우)
                cr = target.GetComponent<CanvasRenderer>();

            return (cg, cr);
        }

        #endregion
    }
}
