using UnityEngine;
// ReSharper disable InconsistentNaming

namespace ProjectLucia.Status
{
    /// <summary>
    /// 로그 항목의 고유 ID를 관리하는 컴포넌트입니다.
    /// UI 프리팹 등에 부착되어 개별 로그를 식별하는 데 사용됩니다.
    /// </summary>
    public class LogID : MonoBehaviour
    {
        #region Fields & Properties (필드 및 속성)

        [Tooltip("이 로그 항목의 고유 식별 번호입니다.")]
        [SerializeField] private int logIDNumber;

        /// <summary>
        /// 로그 ID 번호를 가져오거나 설정합니다.
        /// </summary>
        public int LogIDNumber
        {
            get => logIDNumber;
            set => logIDNumber = value;
        }

        #endregion
    }

    #region Data Structures (데이터 구조)

    /// <summary>
    /// 개별 대화 로그의 데이터를 담는 클래스입니다.
    /// 사용자 입력, AI 응답, 감정 상태, 시간 정보 등을 포함합니다.
    /// </summary>
    [System.Serializable]
    public class LogData
    {
        /// <summary>
        /// 로그의 고유 ID (데이터베이스 Primary Key 등)
        /// </summary>
        public int id;

        /// <summary>
        /// 사용자가 입력한 대화 내용
        /// </summary>
        public string user;

        /// <summary>
        /// AI(Assistant)가 응답한 대화 내용
        /// </summary>
        public string assistant;

        /// <summary>
        /// 당시 AI의 감정 상태
        /// </summary>
        public string emotion;

        /// <summary>
        /// 사용자가 대화를 입력한 시간
        /// </summary>
        public string userTime;

        /// <summary>
        /// AI가 응답한 시간
        /// </summary>
        public string assistantTime;

        /// <summary>
        /// 피드백이 존재하는지 여부
        /// </summary>
        public bool isFeedback;

        /// <summary>
        /// 학습이 완료되었는지 여부 (학습 완료 시 수정/삭제 제한 등에 사용 가능)
        /// </summary>
        public bool isLearning;

        /// <summary>
        /// 사용자가 남긴 피드백 데이터 내용
        /// </summary>
        public string feedbackData;
    }

    /// <summary>
    /// 서버 설정 관련 데이터를 담는 클래스입니다.
    /// </summary>
    public class ServerSettingData
    {
        // memoryLimit, memoryTime 제거됨
        // 추후 서버 관련 설정이 추가될 수 있습니다.
    }

    #endregion

    #region Static Helpers (정적 도우미)

    /// <summary>
    /// 우클릭 이벤트 발생 시 선택된 로그의 정보를 임시 저장하는 정적 클래스입니다.
    /// </summary>
    public static class RightClickVariable
    {
        /// <summary>
        /// 우클릭된 로그 UI 게임 오브젝트
        /// </summary>
        public static GameObject logObject;

        /// <summary>
        /// 우클릭된 로그의 ID 번호
        /// </summary>
        public static int logID;
        
        public static void Reset()
        {
            logObject = null;
            logID = 0;
        }
    }

    #endregion
}
