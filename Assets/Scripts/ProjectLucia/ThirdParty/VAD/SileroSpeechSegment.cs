using UnityEngine;

namespace ProjectLucia.ThirdParty.VAD
{
    /// <summary>
    /// Silero VAD(Voice Activity Detection) 모델이 감지한 음성 세그먼트(구간) 정보를 담는 데이터 클래스입니다.
    /// 오디오 샘플 내에서의 시작/종료 오프셋과 시간(초) 정보를 포함합니다.
    /// </summary>
    [System.Serializable]
    public class SileroSpeechSegment
    {
        #region Properties & Fields (속성 및 필드)

        /// <summary>
        /// 음성 구간이 시작되는 샘플 인덱스(Offset)입니다.
        /// </summary>
        // 참고: Unity 인스펙터는 기본적으로 Nullable 타입(int?)을 표시하지 않습니다.
        [Tooltip("음성 구간 시작 샘플 인덱스")]
        public int? StartOffset { get; set; }

        /// <summary>
        /// 음성 구간이 끝나는 샘플 인덱스(Offset)입니다.
        /// </summary>
        [Tooltip("음성 구간 종료 샘플 인덱스")]
        public int? EndOffset { get; set; }

        /// <summary>
        /// 음성 구간 시작 시간(초 단위)입니다.
        /// </summary>
        [Tooltip("음성 구간 시작 시간 (초)")]
        public float? StartSecond;

        /// <summary>
        /// 음성 구간 종료 시간(초 단위)입니다.
        /// </summary>
        [Tooltip("음성 구간 종료 시간 (초)")]
        public float? EndSecond;

        #endregion

        #region Constructors (생성자)

        /// <summary>
        /// 기본 생성자
        /// </summary>
        public SileroSpeechSegment()
        {
        }

        /// <summary>
        /// 데이터를 초기화하는 생성자입니다.
        /// </summary>
        /// <param name="startOffset">시작 샘플 오프셋</param>
        /// <param name="endOffset">종료 샘플 오프셋</param>
        /// <param name="startSecond">시작 시간(초)</param>
        /// <param name="endSecond">종료 시간(초)</param>
        public SileroSpeechSegment(int startOffset, int? endOffset, float? startSecond, float? endSecond)
        {
            StartOffset = startOffset;
            EndOffset = endOffset;
            StartSecond = startSecond;
            EndSecond = endSecond;
        }

        #endregion
    }
}
