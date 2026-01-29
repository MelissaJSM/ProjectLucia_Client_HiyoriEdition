using UnityEngine;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// FadeController를 더 쉽게 사용하기 위한 래퍼(Wrapper) 클래스입니다.
    /// 기본 지속 시간(defaultDuration)을 사용하여 간단하게 페이드 인/아웃을 호출할 수 있습니다.
    /// </summary>
    public class FadeControllerUser : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("기본 페이드 지속 시간 (초)")]
        [SerializeField] private float defaultDuration = 0.5f;

        #endregion

        #region Private Fields (비공개 필드)

        private FadeController _fade;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // GameManager를 통해 FadeController 참조 가져오기
            _fade = GameManager.Instance.FadeController;
        }

        #endregion

        #region Public Methods (공개 메서드)

        /// <summary>
        /// 대상 오브젝트를 기본 지속 시간 동안 페이드 인(나타나게) 합니다.
        /// </summary>
        /// <param name="target">대상 게임 오브젝트</param>
        public void StartFadeIn(GameObject target)
        {
            _fade?.FadeIn(target, defaultDuration);
        }

        /// <summary>
        /// 대상 오브젝트를 기본 지속 시간 동안 페이드 아웃(사라지게) 합니다.
        /// </summary>
        /// <param name="target">대상 게임 오브젝트</param>
        public void StartFadeOut(GameObject target)
        {
            _fade?.FadeOut(target, defaultDuration);
        }

        /// <summary>
        /// 대상 오브젝트를 페이드 인하면서 상호작용 가능 여부도 함께 설정합니다.
        /// </summary>
        /// <param name="target">대상 게임 오브젝트</param>
        /// <param name="interactable">상호작용 활성화 여부 (기본값: true)</param>
        public void StartFadeInInteractable(GameObject target, bool interactable = true)
        {
            _fade?.FadeIn(target, defaultDuration, onComplete: null, interactable: interactable, blocksRaycasts: interactable);
        }

        /// <summary>
        /// 대상 오브젝트를 페이드 아웃하면서 상호작용을 비활성화합니다.
        /// </summary>
        /// <param name="target">대상 게임 오브젝트</param>
        public void StartFadeOutNonInteractable(GameObject target)
        {
            _fade?.FadeOut(target, defaultDuration, onComplete: null, interactable: false, blocksRaycasts: false);
        }

        #endregion
    }
}
