using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ProjectLucia.Live2D;
using ProjectLucia.Server;
using ProjectLucia.Status;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// 대화 로그(Log) UI를 관리하는 컨트롤러입니다.
    /// 로그 목록 표시, 우클릭 메뉴, 피드백 전송, 로그 삭제 등의 기능을 담당합니다.
    /// </summary>
    public class LogController : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Header("Prefabs / Containers")]
        [Tooltip("로그 목록에 추가될 개별 로그 아이템 프리팹")]
        [SerializeField] private GameObject logData;

        [Tooltip("로그 아이템들이 생성될 부모 컨테이너 (Scroll View Content)")]
        [SerializeField] private Transform chatContainer;

        [Header("Alerts & Popups")]
        [Tooltip("전체 로그 삭제 확인 팝업")]
        [SerializeField] private GameObject alertDisplay;

        [Tooltip("로그 아이템 우클릭 시 나타나는 메뉴")]
        [SerializeField] private GameObject rightClickAlert;

        [Tooltip("피드백 입력을 위한 팝업 패널")]
        [SerializeField] private GameObject feedbackAlert;
        
        [Header("Right-Click (UI Raycast)")]
        [Tooltip("UI 클릭 감지를 위한 GraphicRaycaster")]
        public GraphicRaycaster graphicRaycaster;

        [Tooltip("이벤트 시스템 참조")]
        public EventSystem eventSystem;

        [Tooltip("로그 목록 스크롤 뷰")]
        public ScrollRect scrollRect;

        [Header("Sprites & Resources")]
        [Tooltip("감정별 프로필 스프라이트 리스트")]
        [SerializeField] private List<Sprite> emotionSprite;

        [Tooltip("피드백이 반영된 말풍선 스프라이트")]
        [SerializeField] private Sprite feedbackPanel;

        #endregion

        #region Public Properties (공개 속성)

        /// <summary>
        /// 현재 로그 패널이 열려있는지 여부
        /// </summary>
        public bool LogsOpen { get => _logsOpen; set => _logsOpen = value; }

        #endregion

        #region Private Fields (비공개 필드)

        private bool _logsOpen;

        // 매니저 참조
        private PanelManager _panelManager;
        private SettingController _settingController;
        private MySQLManager _mySQLManager;
        private ServerClient _serverClient;
        private ActionManager _actionManager;
        private InputHandler _inputHandler;
        private Live2DButtonPosition _live2DButtonPosition;

        // 코루틴 추적 (최적화용)
        private Coroutine _createPrefabsCoroutine;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            if (GameManager.Instance == null) return;

            // 매니저 참조 가져오기
            _panelManager          = GameManager.Instance.PanelManager;
            _settingController     = GameManager.Instance.SettingController;
            _mySQLManager          = GameManager.Instance.MySQLManager;
            _serverClient          = GameManager.Instance.ServerClient;
            _actionManager         = GameManager.Instance.ActionManager;
            _inputHandler          = GameManager.Instance.InputHandler;
            _live2DButtonPosition  = GameManager.Instance.Live2DButtonPosition;

            // 필수 컴포넌트 누락 경고
            if (!graphicRaycaster) if(SettingData.IsDebug) Debug.LogWarning("[LogController] GraphicRaycaster가 누락되었습니다.");
            if (!eventSystem)      if(SettingData.IsDebug) Debug.LogWarning("[LogController] EventSystem이 누락되었습니다.");
            if (!chatContainer)    if(SettingData.IsDebug) Debug.LogWarning("[LogController] chatContainer가 누락되었습니다.");
            if (!logData)          if(SettingData.IsDebug) Debug.LogWarning("[LogController] logData 프리팹이 누락되었습니다.");
        }

        private void Update()
        {
            // 로그창이 닫혀있거나, 이미 우클릭 메뉴가 떠있으면 패스
            if (!_logsOpen) return;
            if (rightClickAlert != null && rightClickAlert.activeSelf) return;
            if (graphicRaycaster == null || eventSystem == null) return;

            // 우클릭 감지 (Legacy Input 사용 - 투명 윈도우 호환성)
            if (Input.GetMouseButtonDown(1))
            {
                var pointerData = new PointerEventData(eventSystem) { position = Input.mousePosition };
                var results = new List<RaycastResult>();
                graphicRaycaster.Raycast(pointerData, results);

                foreach (var hit in results)
                {
                    // 클릭된 UI에서 LogID 컴포넌트 찾기
                    var logID = hit.gameObject.GetComponentInParent<LogID>();
                    if (logID == null) continue;

                    // 전역 변수에 선택된 로그 정보 저장
                    RightClickVariable.logObject = logID.gameObject;
                    RightClickVariable.logID     = logID.LogIDNumber;

                    // 피드백 가능 여부 확인 (AI 말풍선인지 체크)
                    CheckFeedbackAvailability(logID.transform);

                    // 우클릭 메뉴 표시
                    if (rightClickAlert != null)
                    {
                        rightClickAlert.SetActive(true);
                    }
                    break; 
                }
            }
        }

        private void OnDestroy()
        {
            DeleteAllprefabs();
            RightClickVariable.Reset();
            Resources.UnloadUnusedAssets();
        }

        #endregion

        #region Log Panel Control (로그 패널 제어)

        /// <summary>
        /// 로그 패널을 열거나 닫습니다.
        /// </summary>
        public void OnLogs()
        {
            _logsOpen = !_logsOpen;

            if (_logsOpen)
            {
                // [Open] 로그 패널 열기
                _panelManager.Panels[(int)UISettingEnums.PanelsEnum.LOGPanel].SetActive(true);

                // 설정창이 열려있었다면 닫기 및 정리
                if (_panelManager.Panels[(int)UISettingEnums.PanelsEnum.SettingPanel].activeSelf)
                {
                    _settingController.OnSettingsCancel(true);
                    _settingController.OnStopServerStatus();
                }
                else
                {
                    _settingController.VoiceRecordChecking(true); // 음성 인식 일시 중지
                    _live2DButtonPosition.DrawLive2dBound();
                }

                // 로그 데이터 불러오기 및 UI 생성
                DeleteAllprefabs(); 
                var list = _mySQLManager.InQuiryLogData(0, 0);
                
                if (list is { Count: > 0 }) 
                {
                    if (_createPrefabsCoroutine != null) StopCoroutine(_createPrefabsCoroutine);
                    _createPrefabsCoroutine = StartCoroutine(CreatePrefabsRoutine(list));
                }
                else 
                {
                    if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log("불러올 로그 데이터가 없습니다.");
                }

                // 로그 버튼 비활성화 (중복 클릭 방지) - 토글 기능을 위해 제거
                // if (_settingController.MainUiButtons.Count > (int)UISettingEnums.MainUiButtonEnum.LogsButton)
                //    _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.LogsButton].interactable = false;
                
                _settingController.CheckDimmer();
                _actionManager.AnnounceCharacterAction(3); // 로그 열림 모션
            }
            else
            {
                // [Close] 로그 패널 닫기
                _panelManager.Panels[(int)UISettingEnums.PanelsEnum.LOGPanel].SetActive(false);
                
                DeleteAllprefabs();
                _settingController.VoiceRecordChecking(false); // 음성 인식 재개
                
                // if (_settingController.MainUiButtons.Count > (int)UISettingEnums.MainUiButtonEnum.LogsButton)
                //    _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.LogsButton].interactable = true;
                
                _actionManager.AnnounceCharacterAction(4); // 로그 닫힘 모션
                _live2DButtonPosition.DrawLive2dBound();
            }
        }

        #endregion

        #region Log Item Creation (로그 아이템 생성)

        /// <summary>
        /// 로그 아이템들을 코루틴을 통해 분산 생성합니다. (프레임 드랍 방지)
        /// </summary>
        private IEnumerator CreatePrefabsRoutine(List<LogData> logDataList)
        {
            if (logData == null || chatContainer == null) yield break;

            // ID 오름차순 정렬 (과거 -> 최신)
            // 일반적인 메신저처럼 과거 대화가 위, 최신 대화가 아래에 위치하도록 함
            logDataList.Sort((a, b) => a.id.CompareTo(b.id));

            int count = 0;
            const int batchSize = 10; // 한 프레임당 생성 개수

            foreach (var log in logDataList)
            {
                CreateSingleLogItem(log);
                count++;

                if (count % batchSize == 0)
                {
                    yield return null; 
                }
            }

            // 스크롤 위치 초기화 (맨 아래로)
            if (scrollRect != null)
            {
                yield return null; 
                Canvas.ForceUpdateCanvases();
                scrollRect.verticalNormalizedPosition = 0f; 
            }
            
            _createPrefabsCoroutine = null;
        }

        /// <summary>
        /// 단일 로그 아이템을 생성하고 데이터를 설정합니다.
        /// </summary>
        private void CreateSingleLogItem(LogData log)
        {
            var item = Instantiate(logData, chatContainer);
            if (item == null) return;

            // 헬퍼 함수
            Transform GetTf(string path) => item.transform.Find(path);
            void SetTxt(string path, string val) 
            {
                var t = GetTf(path);
                if (t != null && t.TryGetComponent<TextMeshProUGUI>(out var txt)) 
                    txt.text = val ?? "";
            }
            void SetImg(string path, Sprite sp) 
            {
                var t = GetTf(path);
                if (t != null && sp != null && t.TryGetComponent<Image>(out var img)) 
                    img.sprite = sp;
            }

            // ID 설정
            if (item.TryGetComponent<LogID>(out var idComp)) 
                idComp.LogIDNumber = log.id;

            string userName;
            if (SettingData.UserName == null || SettingData.UserName.Trim() == "")
            {
                userName = "사용자";
            }
            else
            {
                userName = SettingData.UserName;
            }
            // 이름 설정
            SetTxt("UserContent/UserContentBackground/UserNamePanel/UserNameText", userName);
            
            string assistantName;
            if (SettingData.CallName == null || SettingData.CallName.Trim() == "")
            {
                assistantName = "모모세 히요리";
            }
            else
            {
                assistantName = SettingData.CallName;
            }
            SetTxt("AssistantContent/AssistantContentBackground/AssistantName/AssistantNameText", assistantName);

            // 내용 설정
            SetTxt("UserContent/UserContentBackground/UserContent/UserContentText", log.user);
            
            string assistantText = (log.isLearning || log.isFeedback) ? log.feedbackData : log.assistant;
            SetTxt("AssistantContent/AssistantContentBackground/AssistantContent/AssistantContentText", assistantText);

            // 시간 설정
            SetTxt("UserContent/UserContentBackground/UserTime/UserTimeText", log.userTime);
            SetTxt("AssistantContent/AssistantContentBackground/AssistantTime/AssistantTimeText", log.assistantTime);

            // 감정 아이콘 설정
            int profIndex = EmotionProfile(log.emotion);
            if (profIndex >= 0 && profIndex < emotionSprite.Count)
            {
                SetImg("AssistantContent/AssistantProfile", emotionSprite[profIndex]);
            }

            // 피드백 상태 반영
            if (log.isLearning || log.isFeedback)
            {
                SetImg("AssistantContent/AssistantContentBackground/AssistantContent", feedbackPanel);
            }

            // 피드백 입력창 설정
            var inputTf = GetTf("AssistantContent/AssistantContentBackground/FeedBackInput");
            if (inputTf != null && inputTf.TryGetComponent<TMP_InputField>(out var input))
            {
                input.text = log.feedbackData ?? "";
                input.interactable = !(log.isLearning || log.isFeedback);
            }
        }

        #endregion

        #region Feedback & Deletion (피드백 및 삭제)

        /// <summary>
        /// 피드백을 서버로 전송하고 UI 및 DB를 갱신합니다.
        /// </summary>
        public void FeedbackPrefabs(TMP_InputField input)
        {
            if (input == null) { EndRightAlertPanel(); return; }

            var logID = RightClickVariable.logID;
            var logGo = RightClickVariable.logObject;
            var feedbackText = input.text ?? string.Empty;

            if (logID <= 0 || logGo == null)
            {
                if(SettingData.IsDebug) Debug.LogWarning("피드백 대상이 올바르지 않습니다.");
                EndRightAlertPanel();
                return;
            }

            // 서버 전송
            _serverClient.SendFeedbackToServer(feedbackText, logID, (serverResponse) =>
            {
                if (string.IsNullOrEmpty(serverResponse))
                {
                    if(SettingData.IsDebug) Debug.LogError("❌ 서버 응답 실패");
                    EndRightAlertPanel();
                    return;
                }

                try
                {
                    string filtered = serverResponse; 

                    // 1. DB 업데이트 (filtered는 이제 순수 텍스트임)
                    _mySQLManager.UpdateFeedbackData(filtered, feedbackText, logID);

                    // 2. UI 즉시 반영
                    var bgPath = "AssistantContent/AssistantContentBackground";
                    var contentTf = logGo.transform.Find($"{bgPath}/AssistantContent/AssistantContentText");
                    var bubbleTf  = logGo.transform.Find($"{bgPath}/AssistantContent");
                    var inputTf   = logGo.transform.Find($"{bgPath}/FeedBackInput");

                    if (contentTf != null && contentTf.TryGetComponent<TMP_Text>(out var txt)) 
                        txt.text = filtered;

                    if (bubbleTf != null && bubbleTf.TryGetComponent<Image>(out var img) && feedbackPanel != null) 
                        img.sprite = feedbackPanel;

                    if (inputTf != null && inputTf.TryGetComponent<TMP_InputField>(out var feedInput)) 
                        feedInput.interactable = false; 

                }
                catch (Exception e)
                {
                    Debug.LogError($"피드백 처리 중 오류: {e.Message}");
                    _actionManager.ErrorCharacterAction(900, false);
                }
                finally
                {
                    RightClickVariable.Reset();
                    EndRightAlertPanel();
                }
            });
        }

        /// <summary>
        /// 선택된 로그를 삭제합니다.
        /// </summary>
        public void DeletePrefabs()
        {
            var logID = RightClickVariable.logID;
            var target = RightClickVariable.logObject;

            if (logID <= 0 || target == null)
            {
                EndRightAlertPanel();
                return;
            }

            if (_mySQLManager.DeleteLogData(logID))
            {
                Destroy(target);
            }
            else
            {
                _actionManager.ErrorCharacterAction(1003, false);
            }

            RightClickVariable.Reset();
            EndRightAlertPanel();
        }

        /// <summary>
        /// 모든 로그를 삭제합니다.
        /// </summary>
        public void DeleteAllLogs(bool confirm)
        {
            if (!confirm) { alertDisplay?.SetActive(false); return; }

            if (_mySQLManager.AllDeleteLogData())
            {
                DeleteAllprefabs();
            }
            else
            {
                _actionManager.ErrorCharacterAction(1004, false);
            }
            alertDisplay?.SetActive(false);
        }

        /// <summary>
        /// 생성된 모든 로그 아이템 프리팹을 제거합니다.
        /// </summary>
        public void DeleteAllprefabs()
        {
            if (_createPrefabsCoroutine != null)
            {
                StopCoroutine(_createPrefabsCoroutine);
                _createPrefabsCoroutine = null;
            }

            if (chatContainer == null) return;

            foreach (Transform child in chatContainer)
            {
                Destroy(child.gameObject);
            }
        }

        #endregion

        #region UI Helpers (UI 보조 메서드)

        /// <summary>
        /// 전체 삭제 확인 팝업을 표시합니다.
        /// </summary>
        public void AlertDisplay() => alertDisplay?.SetActive(true);

        /// <summary>
        /// 피드백 입력 패널을 엽니다.
        /// </summary>
        public void OnFeedbackPanel()
        {
            if (rightClickAlert) rightClickAlert.SetActive(false);
            if (feedbackAlert)   feedbackAlert.SetActive(true);
        }

        /// <summary>
        /// 우클릭 메뉴 및 피드백 패널을 닫고 상태를 초기화합니다.
        /// </summary>
        public void EndRightAlertPanel()
        {
            if (rightClickAlert) rightClickAlert.SetActive(false);
            if (feedbackAlert)   feedbackAlert.SetActive(false);

            if (_inputHandler != null && _inputHandler.Inputs.Count > (int)UISettingEnums.InputEnum.Feedback)
            {
                var field = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.Feedback];
                if (field) field.text = "";
            }
        }

        /// <summary>
        /// 우클릭 시 피드백 버튼 활성화 여부를 결정합니다. (AI 말풍선만 가능)
        /// </summary>
        private void CheckFeedbackAvailability(Transform logTransform)
        {
            if (rightClickAlert == null) return;

            var assistantBubble = logTransform.Find("AssistantContent/AssistantContentBackground/AssistantContent");
            var feedbackBtnTransform = rightClickAlert.transform.Find("Alert/Feedback");
            
            if (feedbackBtnTransform != null && feedbackBtnTransform.TryGetComponent<Button>(out var feedbackBtn))
            {
                bool isAssistant = false;
                if (assistantBubble != null && assistantBubble.TryGetComponent<Image>(out var img))
                {
                    isAssistant = (img.sprite != null && img.sprite.name == "chat_assistant");
                }
                feedbackBtn.interactable = isAssistant;
            }
        }

        /// <summary>
        /// 감정 문자열을 스프라이트 인덱스로 변환합니다.
        /// </summary>
        private int EmotionProfile(string emotion)
        {
            if (string.IsNullOrEmpty(emotion)) return (int)Live2DEnums.SpriteEnum.Idle;

            switch (emotion.Trim().ToLowerInvariant())
            {
                case "angry":   return (int)Live2DEnums.SpriteEnum.Angry;
                case "fear":    
                case "sad":     return (int)Live2DEnums.SpriteEnum.Sad;
                case "happy":   
                case "tender":  return (int)Live2DEnums.SpriteEnum.Happy;
                case "surprise": return (int)Live2DEnums.SpriteEnum.Idle; 
                default:        return (int)Live2DEnums.SpriteEnum.Idle;
            }
        }

        #endregion
    }
}
