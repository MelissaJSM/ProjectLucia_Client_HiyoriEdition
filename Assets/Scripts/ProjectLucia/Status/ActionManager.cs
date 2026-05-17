using System.Collections;
using System.Collections.Generic;
using ProjectLucia.GUI;
using ProjectLucia.Live2D;
using UnityEngine;
using Random = UnityEngine.Random;

namespace ProjectLucia.Status
{
    /// <summary>
    /// 캐릭터의 행동(Action) 및 상태 메시지, 대기 모션(Idle Motion)을 관리하는 매니저 클래스입니다.
    /// </summary>
    public class ActionManager : MonoBehaviour
    {
        #region Fields & Properties (필드 및 속성)

        private Coroutine _characterActionCoroutine;
        private Coroutine _idleMotionCoroutine;

        [Header("Idle Motion Settings (Inspector)")] [Tooltip("대기 모션 랜덤 재생 여부를 설정하는 UI 오브젝트")] [SerializeField]
        private GameObject isIdleMotionRandom;

        public GameObject IsIdleMotionRandom
        {
            get => isIdleMotionRandom;
            set => isIdleMotionRandom = value;
        }

        [Tooltip("대기 모션 랜덤 재생 최대 시간 간격을 설정하는 UI 오브젝트")] [SerializeField]
        private GameObject idleMotionRandomMax;

        public GameObject IdleMotionRandomMax
        {
            get => idleMotionRandomMax;
            set => idleMotionRandomMax = value;
        }

        [Tooltip("대기 모션 랜덤 재생 최소 시간 간격을 설정하는 UI 오브젝트")] [SerializeField]
        private GameObject idleMotionRandomMin;

        public GameObject IdleMotionRandomMin
        {
            get => idleMotionRandomMin;
            set => idleMotionRandomMin = value;
        }

        [Tooltip("대기 모션 고정 시간 간격을 설정하는 UI 오브젝트")] [SerializeField]
        private GameObject idleMotionFixed;

        public GameObject IdleMotionFixed
        {
            get => idleMotionFixed;
            set => idleMotionFixed = value;
        }

        private EmotialController _emotialController;
        private PanelManager _panelManager;

        // 최적화: WaitForSeconds 캐싱
        private readonly WaitForSeconds _wait3Sec = new WaitForSeconds(3f);
        private readonly WaitForSeconds _wait5Sec = new WaitForSeconds(5f);

        #endregion

        #region Constants & Messages (상수 및 메시지)

        // Live2D 표현 문자열(EmotialController API가 문자열을 받는 형태이므로 상수화)
        private const string ExpIdle = "Idle";
        private const string ExpHappy = "Happy";
        private const string ExpSad = "Sad";
        private const string ExpLoading = "Loading";

        /// <summary>
        /// 오류 상황별 캐릭터 대사 (Key: Error Code, Value: Message)
        /// </summary>
        private static readonly Dictionary<int, string> Live2DStatus = new Dictionary<int, string>
        {
            // 서버 (연결 및 통신 관련)
            { 0, "오빠! 혹시 서버 설정 건드렸어? 정상이면 상관 없는데 잘못 설정한거면 다시점검해봐!" },
            { 1, "오빠, 서버 문을 두드렸는데 아무도 없는거야. 네트워크 문제인걸까? (시무룩)" },
            { 2, "서버 로딩 중" },
            { 400, "어라? 오빠, 요청한 모양이 조금 이상한 것 같은걸! (400 Bad Request)" },
            { 403, "오빠! 히요리 쫓겨난 것 같은거야... 들어갈 수가 없는걸! (403 Forbidden)" },
            { 404, "아무리 찾아봐도 그런 곳은 없는걸. 히요리 길을 잃어버린거야... (404 Not Found)" },
            { 500, "으앙! 서버 배가 아프대! 내부 오류가 난거야! (500 Internal Error)" },
            { 502, "서버가 기절해버린거야! 너무 무리했나 봐... (502 Bad Gateway)" },
            { 503, "서버가 너무 바쁘거나 아픈가 봐. 히요리가 조금 기다려볼까? (503 Service Unavailable)" },

            // AI 및 처리
            { 600, "히요리 머리... 아니, LLM 모델이 안 보이는걸! 어디 간거야?" },
            { 700, "오빠? 아무 말도 안 해줘서 히요리는 빈 종이만 받은거야." },
            { 800, "목소리가 안 나오는거야... 말하고 싶은데 실패한걸. (울먹)" },
            { 900, "열심히 배웠는데... 고치지 못한거야. 미안해, 오빠..." },

            // SQL (데이터베이스)
            { 1000, "오빠, 기억 상자(SQL)가 안 열리는거야! 들어갈 수가 없는걸." },
            { 1001, "기억을 떠올리려는데 안 되는거야. 머릿속이 새하얘진걸..." },
            { 1002, "오빠와의 추억을 적으려 했는데... 실패해버린거야. 흑흑." },
            { 1003, "지우라고 한 거 없는걸! 유령인가 봐, 오빠?" },
            { 1004, "지워버리려고 했는데 안 지워지는거야. 엄청 끈질긴 녀석들인걸!" },

            // 모델 (로컬 AI 모델 및 하드웨어)
            { 2000, "히요리 귀(Whisper)랑 눈치(VAD) 설정이 비어있는거야! 얼른 확인해줘, 오빠!" },
            { 2001, "오빠, 듣는 귀(Whisper)가 없거나 마이크가 꺼진 것 같은걸. 히요리 말 들려?" },
            { 2002, "언제 말을 끊어야 할지 모르겠는걸... 눈치(VAD) 모델이 없는거야." },
            { 2003, "Whisper 모델 가져오다가 넘어진거야... 실패인걸!" },
            { 2004, "VAD 모델을 못 가져온거야. 다운로드 실패인걸." },
            { 2005, "귀(Whisper)랑 눈치(VAD)가 말을 안 듣는거야! 오빠가 좀 봐줘!" },
            { 2100, "그래픽카드 깨워줘! 히요리 심장이 안 뛰는거야." },
            { 2101, "어라? 이 그래픽카드는 히요리가 알던 게 아닌걸! (장치 미일치)" },

            // 성능 (메모리)
            { 3001, "오빠... 히요리 머리가 깨질 것 같은걸! 로컬 메모리가 부족한거야! (OOM)" },
            { 3002, "서버 언니가 머리 아프대! 서버 메모리 부족인거야! (Server OOM)" },

            // 카메라 (비전)
            { 4000, "오빠 얼굴을 못 보낸거야! 보고 싶은데... (이미지 전송 실패)" },
            { 4001, "방금 본 게 뭔지 모르겠는걸... 히요리 눈이 이상한가 봐!" },
            { 4002, "히요리 눈(카메라)이 어디 갔지? 찾을 수가 없는거야!" },

            // 키워드
            { 5000, "부르는 말을 까먹었거나 망가진거야. 오빠, 히요리 불렀어?" }
        };

        /// <summary>
        /// 성공 상황별 캐릭터 대사
        /// </summary>
        private static readonly Dictionary<int, string> SuccessLive2DStatus = new Dictionary<int, string>
        {
            { 10, "헤헤, 오빠 말 입력 완료! 히요리 좀 똑똑해진거야? 칭찬해줘!" },
            { 20, "짠! 서버랑 연결된걸! 뭐든 말만 해, 오빠!" }
        };

        /// <summary>
        /// 로딩 상황별 캐릭터 대사
        /// </summary>
        private static readonly Dictionary<int, string> LoadingLive2DStatus = new Dictionary<int, string>
        {
            { 0, "잠깐만 기다려줘 오빠, 히요리 다시 일어나는 중인거야... (부팅)" },
            { 10, "오빠 말 열심히 공부하고 있는 중인걸... (피드백 반영 중)" },
        };

        /// <summary>
        /// 안내 상황별 캐릭터 대사
        /// </summary>
        private static readonly Dictionary<int, string> AnnounceLive2DStatus = new Dictionary<int, string>
        {
            { 0, "설정 메뉴 열었어! 오빠, 뭐 바꿔줄까?" },
            { 1, "설정 끝! 히요리가 더 완벽해진거야!" },
            { 2, "안 바꾸는거야? 알겠어, 그대로 가는걸!" },
            { 3, "기록을 살펴보는 거야? 로그 메뉴 들어온걸!" },
            { 4, "로그 메뉴 닫을게. 지금 히요리한테 집중해줘, 오빠!" },
            // RAG 관련 대사 삭제됨
            { 13, "VAD 켜진거야! 오빠 목소리 놓치지 않으려고 귀 쫑긋하고 있는걸. (집중)" },
            { 14, "VAD 끈거야. 잠시 귀 좀 닫고 있을게, 오빠." },
        };

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            _emotialController = GameManager.Instance.EmotialController;
            _panelManager = GameManager.Instance.PanelManager;
        }

        private void OnDestroy()
        {
            ActionCoroutineCheck();
            StopIdleMotion();
            Resources.UnloadUnusedAssets();
        }

        #endregion

        #region Action Methods (행동 처리 메서드)

        /// <summary>
        /// 오류 발생 시 캐릭터의 행동 및 대사를 처리합니다.
        /// </summary>
        /// <param name="errorCode">오류 코드</param>
        /// <param name="isTalk">대화 중 발생 여부</param>
        public void ErrorCharacterAction(int errorCode, bool isTalk)
        {
            if (IsLockedPanelActive()) return;

            ActionCoroutineCheck();
            _emotialController.UpdateLive2DExpression(ExpSad);

            var msg = GetDictMessage(Live2DStatus, errorCode, "알 수 없는 오류");
            _characterActionCoroutine = StartCoroutine(ErrorTalking(msg, isTalk));
        }

        /// <summary>
        /// 성공 시 캐릭터의 행동 및 대사를 처리합니다.
        /// </summary>
        /// <param name="successCode">성공 코드</param>
        public void SuccessCharacterAction(int successCode)
        {
            if (IsLockedPanelActive()) return;

            ActionCoroutineCheck();
            _emotialController.UpdateLive2DExpression(ExpHappy);

            var msg = GetDictMessage(SuccessLive2DStatus, successCode, "완료");
            _characterActionCoroutine = StartCoroutine(SuccessTalking(msg));
        }

        /// <summary>
        /// 안내 메시지 출력 시 캐릭터의 행동 및 대사를 처리합니다.
        /// </summary>
        /// <param name="announceCode">안내 코드</param>
        public void AnnounceCharacterAction(int announceCode)
        {
            if (IsLockedPanelActive()) return;

            ActionCoroutineCheck();
            _emotialController.UpdateLive2DExpression(ExpIdle);

            var msg = GetDictMessage(AnnounceLive2DStatus, announceCode, "안내");
            _characterActionCoroutine = StartCoroutine(AnnounceTalking(msg));
        }

        /// <summary>
        /// 로딩 중일 때 캐릭터의 행동 및 대사를 처리합니다.
        /// </summary>
        /// <param name="loadingCode">로딩 코드</param>
        public void LoadingCharacterAction(int loadingCode)
        {
            if (IsLockedPanelActive()) return;

            ActionCoroutineCheck();
            _emotialController.UpdateLive2DExpression(ExpLoading);

            if (loadingCode != 3545)
            {
                var msg = GetDictMessage(LoadingLive2DStatus, loadingCode, "로딩 중…");
                _panelManager.ResponseTextProcess(msg, false);
            }
        }

        #endregion

        #region Idle Motion Logic (대기 모션 로직)

        /// <summary>
        /// 대기 모션(Idle Motion)을 시작합니다. 설정에 따라 랜덤 또는 고정 간격으로 재생됩니다.
        /// </summary>
        public void IdleCharacterAction()
        {
            // 설정창 열려 있으면 대기 모션 시작 안 함
            if (_panelManager.Panels[(int)UISettingEnums.PanelsEnum.SettingPanel].activeSelf)
            {
                StopIdleMotion();
                return;
            }
                
            //////////////////////////////////////////////////////////////////

            if (SettingData.IsIdleMotionRandom && SettingData.IsIdleMotion)
            {
                // SettingData에 저장된(정규화 해제된) 분 단위를 사용
                var minMin = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1, 60, SettingData.IdleMotionRandomMin)));
                var maxMin = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1, 60, SettingData.IdleMotionRandomMax)));

                // min/max 보정
                if (maxMin < minMin) { (minMin, maxMin) = (maxMin, minMin);
                }
                // 상한 포함을 원할 경우 +1 (현재 +1 포함)
                _idleMotionCoroutine = StartCoroutine(IdleMotionRandomStart(maxMin, minMin));
            }
            else if(SettingData.IsIdleMotion && !SettingData.IsIdleMotionRandom)
            {
                var fixedMin = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1, 60, SettingData.IdleMotionFixed)));
                _idleMotionCoroutine = StartCoroutine(IdleMotionFixedStart(fixedMin));
            }
            else
            {
                StopIdleMotion();
            }
        }

        /// <summary>
        /// 현재 실행 중인 대기 모션 코루틴을 중지합니다.
        /// </summary>
        public void StopIdleMotion()
        {
            if (_idleMotionCoroutine != null)
            {
                StopCoroutine(_idleMotionCoroutine);
                _idleMotionCoroutine = null;
                if (SettingData.IsDebug)
                    if (SettingData.IsDebug)
                        Debug.Log("대기모션 코루틴 정지/해제 완료");
            }
        }

        #endregion

        #region Coroutines (코루틴)

        private IEnumerator ErrorTalking(string errorText, bool isTalk)
        {
            _panelManager.ResponseTextProcess(errorText, false);
            yield return _wait5Sec; // 캐싱된 WaitForSeconds 사용
            
            _emotialController.UpdateLive2DExpression(ExpIdle);
            _panelManager.ResponseTextEnd(isTalk);
        }

        private IEnumerator SuccessTalking(string successText)
        {
            _panelManager.ResponseTextProcess(successText, false);
            yield return _wait3Sec; // 캐싱된 WaitForSeconds 사용
            
            _emotialController.UpdateLive2DExpression(ExpIdle);
            _panelManager.ResponseTextEnd(false);
        }

        private IEnumerator AnnounceTalking(string announceText)
        {
            _panelManager.ResponseTextProcess(announceText, false);
            yield return _wait3Sec; // 캐싱된 WaitForSeconds 사용
            
            _emotialController.UpdateLive2DExpression(ExpIdle);
            _panelManager.ResponseTextEnd(false);
        }

        private IEnumerator IdleMotionFixedStart(int minutes)
        {
            // 최소 1분
            var wait = new WaitForSeconds(minutes * 60f);
            while (true)
            {
                yield return wait;
                if (!IsLockedPanelActive())
                    _emotialController.RandomIdleMotion();
            }
            // ReSharper disable once IteratorNeverReturns
        }

        private IEnumerator IdleMotionRandomStart(int maxMinutes, int minMinutes)
        {
            while (true)
            {
                // 상한 포함(의도에 따라 maxMinutes+1 제거 가능)
                int chosenMinutes = Random.Range(minMinutes, maxMinutes + 1);
                chosenMinutes = Mathf.Max(1, chosenMinutes);

                yield return new WaitForSeconds(chosenMinutes * 60f);

                if (!IsLockedPanelActive())
                    _emotialController.RandomIdleMotion();
            }
            // ReSharper disable once IteratorNeverReturns
        }

        #endregion

        #region Helper Methods (보조 메서드)

        /// <summary>
        /// 딕셔너리에서 메시지를 안전하게 가져옵니다.
        /// </summary>
        private static string GetDictMessage(Dictionary<int, string> dict, int code, string fallback)
        {
            return dict != null && dict.TryGetValue(code, out var msg) ? msg : fallback;
        }

        /// <summary>
        /// 잠금 패널이 활성화되어 있는지 확인합니다.
        /// </summary>
        private bool IsLockedPanelActive()
        {
            return _panelManager.Panels[(int)UISettingEnums.PanelsEnum.LockPanel].activeSelf;
        }

        /// <summary>
        /// 실행 중인 캐릭터 액션 코루틴이 있다면 중지합니다.
        /// </summary>
        public void ActionCoroutineCheck()
        {
            if (_characterActionCoroutine != null)
            {
                StopCoroutine(_characterActionCoroutine);
                _characterActionCoroutine = null;
            }
        }

        #endregion
    }
}