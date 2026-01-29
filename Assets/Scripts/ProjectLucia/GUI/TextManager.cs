using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// UI 텍스트 요소들을 중앙에서 관리하는 매니저 클래스입니다.
    /// 설정창, 상태 표시, 대화창 등의 텍스트 컴포넌트에 접근할 수 있는 참조를 제공합니다.
    /// </summary>
    public class TextManager : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("관리할 TMP_Text 리스트 (UISettingEnums.TextsEnum 순서와 일치해야 함)")]
        [SerializeField] private List<TMP_Text> texts;

        [Tooltip("대화창(말풍선)에 표시되는 텍스트 컴포넌트")]
        [SerializeField] private TMP_Text talkText;

        #endregion

        #region Public Properties (공개 속성)

        /// <summary>
        /// 등록된 모든 텍스트 컴포넌트 리스트를 반환합니다.
        /// </summary>
        public List<TMP_Text> Texts => texts;

        /// <summary>
        /// 대화창 텍스트 컴포넌트를 반환합니다.
        /// </summary>
        public TMP_Text TalkText => talkText;

        #endregion
    }
}
