using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;
using ProjectLucia.Live2D;
using TMPro;
using ProjectLucia.Server;
using ProjectLucia.Status;
using ProjectLucia.ThirdParty.Whisper.Runtime;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// 사용자 입력(InputField)을 관리하고 처리하는 핸들러 클래스입니다.
    /// 텍스트 입력, 포커스 관리, IME 제어, 서버 설정 값 반영 등을 담당합니다.
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("관리할 TMP_InputField 리스트 (UISettingEnums.InputEnum 순서와 일치해야 함)")]
        [SerializeField] private List<TMP_InputField> inputs; 
        public List<TMP_InputField> Inputs => inputs;

        #endregion

        #region Public Properties (공개 속성)

        /// <summary>
        /// 현재 텍스트 입력 중인지 여부 (포커스 상태이거나 텍스트가 있는 경우)
        /// </summary>
        public bool IsInputText { get; private set; }

        /// <summary>
        /// IME(입력기) 잠금 여부 설정
        /// </summary>
        public bool IsLockIME
        {
            set => _isLockIme = value;
        }

        #endregion

        #region Private Fields (비공개 필드)

        private bool _isFocused;
        private string _currentText = "";
        private bool _isLockIme;

        // 정규식 (한글, 공백)
        private static readonly Regex KoreanRegex = new Regex(@"[가-힣ㄱ-ㅎㅏ-ㅣ]", RegexOptions.Compiled);
        private static readonly Regex SpaceRegex = new Regex(@"\s");
        
        // Enum 범위 캐싱 (탭 이동 로직용)
        private readonly int _pStart = (int)UISettingEnums.InputEnum.OldPassword;
        private readonly int _pEnd   = (int)UISettingEnums.InputEnum.NewPasswordCheck; 
        private readonly int _sStart = (int)UISettingEnums.InputEnum.ServerIP;
        private readonly int _sEnd   = (int)UISettingEnums.InputEnum.SqlPort;          

        // 매니저 참조
        private InputHandler _inputHandler;
        private WhisperManager _whisperManager;
        private SettingController _settingController;
        private ServerClient _serverClient;
        private PanelManager _panelManager;
        private LockController _lockController;
        private LogController _logController;

        private EmotialController _emotialController;
        // RAG 관련 필드 제거됨
        // private KeywordExtractorOnnx _keywordExtractorOnnx;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 매니저 참조 가져오기
            _inputHandler = GameManager.Instance.InputHandler;
            _whisperManager = GameManager.Instance.WhisperManager;
            _settingController = GameManager.Instance.SettingController;
            _serverClient = GameManager.Instance.ServerClient;
            _panelManager = GameManager.Instance.PanelManager;
            _lockController = GameManager.Instance.LockController;
            _logController = GameManager.Instance.LogController;
            _emotialController = GameManager.Instance.EmotialController;
            // RAG 관련 필드 제거됨
            // _keywordExtractorOnnx = GameManager.Instance.KeywordExtractorOnnx;
        }

        private void Start()
        {
            // 모든 InputField에 엔터키 이벤트 연결
            foreach (var field in _inputHandler.Inputs)
            {
                if (field != null)
                {
                    field.onEndEdit.RemoveAllListeners(); 
                    field.onEndEdit.AddListener(HandleInput);
                }
            }       
            
            // 메인 채팅 입력창 이벤트 연결
            var input = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.InputField];
            input.onSelect.AddListener(OnFocusIn);
            input.onDeselect.AddListener(OnFocusOut);
            input.onValueChanged.AddListener(OnTextChanged);

            // RAG 버튼 초기화 (삭제됨)
            // _settingController.OnClickRAG(false);
        }

        private void Update()
        {
            // 탭 키를 이용한 InputField 포커스 이동 처리
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                var inputField = EventSystem.current.currentSelectedGameObject?.GetComponent<TMP_InputField>();
                if (inputField == null) return;
                int index = inputs.IndexOf(inputField);
                
                // 비밀번호 입력 그룹 또는 서버 설정 그룹 내에서 탭 이동
                if (InRangeTab(index, _pStart, _pEnd) || InRangeTab(index, _sStart, _sEnd))
                {
                    int next = index + 1;
                    if (next >= _inputHandler.inputs.Count) return; 

                    var nextField = _inputHandler.inputs[next];
                    if (nextField != null)
                    {
                        nextField.Select();
                        nextField.ActivateInputField();
                    }
                }
            }
            else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"esc 키 감지");
            }

            // 채팅 입력창에서 숫자패드 엔터키가 눌렸을 때 처리
            if (Keyboard.current != null && Keyboard.current.numpadEnterKey.wasPressedThisFrame)
            {
                var currentObj = EventSystem.current.currentSelectedGameObject;
                var chatInput = inputs[(int)UISettingEnums.InputEnum.InputField];

                if (chatInput != null && currentObj == chatInput.gameObject)
                {
                    _whisperManager.useVad = false;
                    ProcessInput(chatInput.text);
                }
            }
        }
        
        void LateUpdate()
        {
            // IME 모드 제어 (Legacy Input System 사용)
            // _isLockIme가 true이면 IME를 꺼서 한글 입력을 막거나 영문 전용으로 설정
            Input.imeCompositionMode = _isLockIme ? IMECompositionMode.Off : IMECompositionMode.On;
            
            if(SettingData.IsDebug && inputs[(int)UISettingEnums.InputEnum.InputField].isFocused)
                if(SettingData.IsDebug) Debug.Log($"채팅창 값 변경 완료 : {inputs[(int)UISettingEnums.InputEnum.InputField].isFocused}");
        }

        private void OnDestroy()
        {
            // 이벤트 리스너 해제 및 리소스 정리
            var input = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.InputField];

            if (input != null)
            {
                input.onEndEdit.RemoveAllListeners();
                input.onSelect.RemoveListener(OnFocusIn);
                input.onDeselect.RemoveListener(OnFocusOut);
                input.onValueChanged.RemoveListener(OnTextChanged);
            }
            Resources.UnloadUnusedAssets();

            _isLockIme = false;
            Input.imeCompositionMode = IMECompositionMode.Auto;
        }

        #endregion

        #region Input Handling (입력 처리)
        
        
        /// <summary>
        /// 채팅을 수동으로 버튼전송할때 사용합니다.
        /// </summary>
        // 버튼 클릭 시 호출할 함수 (인스펙터 연결용)
        public void OnClickSendButton()
        {
            // 1. 우리가 관리하는 InputField 리스트에서 '채팅 입력창'을 가져옵니다.
            // (UISettingEnums.InputEnum.InputField는 0번 인덱스일 것입니다.)
            var chatInput = inputs[(int)UISettingEnums.InputEnum.InputField];

            // 2. 그 입력창의 현재 텍스트(.text)를 직접 꺼내옵니다.
            string textToSend = chatInput.text;

            // 3. 기존에 있던 전송 로직(ProcessInput)에 텍스트를 넘겨줍니다.
            ProcessInput(textToSend);
    
            // (선택사항) 전송 후 채팅창 비우기 등을 원하면 여기서 추가 처리
            // ProcessInput 내부에서 이미 초기화 로직이 있다면 생략 가능
        }
        
        

        /// <summary>
        /// InputField 입력 종료(엔터키 등) 시 호출되는 메서드입니다.
        /// 각 입력 필드의 역할에 따라 적절한 로직을 수행합니다.
        /// </summary>
        private void HandleInput(string inputValue)
        {
            var inputField = EventSystem.current.currentSelectedGameObject?.GetComponent<TMP_InputField>();
            if (inputField == null) return;
            int index = Inputs.IndexOf(inputField);
            
            // 엔터키 입력 시에만 동작
            if (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame))
            {
                if (index == (int)UISettingEnums.InputEnum.LockPasswordInput)
                {
                    _lockController.UnlockController(); // 잠금 해제 시도
                }
                else if (index == (int)UISettingEnums.InputEnum.InputField)
                {
                    _whisperManager.useVad = false; // 음성 인식 일시 중지
                    ProcessInput(inputValue);       // 채팅 메시지 전송
                }
                else if (InRangeEnter(index, UISettingEnums.InputEnum.OldPassword, UISettingEnums.InputEnum.NewPasswordCheck))
                {
                    _lockController.LockPasswordChanger(); // 비밀번호 변경 시도
                }
                else if (index == (int)UISettingEnums.InputEnum.Feedback)
                {
                    _logController.FeedbackPrefabs(inputField); // 피드백 전송
                }
            }
        }

        /// <summary>
        /// 채팅 입력값을 처리하여 서버로 전송합니다.
        /// RAG 기능이 서버 내부 로직으로 통합되어, 이제는 단순히 메시지만 전송합니다.
        /// </summary>
        public void ProcessInput(string input)
        {
            // 안전성 체크
            if (this == null || gameObject == null) return;
            if (_settingController == null || _panelManager == null || _serverClient == null) return;

            // 잠금 화면이 아니고 설정창이 닫혀있을 때만 처리
            var lockPanel = _panelManager.Panels[(int)UISettingEnums.PanelsEnum.LockPanel];
            if (lockPanel == null || lockPanel.gameObject == null) return;

            if (!_settingController.SettingsOpen && !lockPanel.gameObject.activeSelf)
            {
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"처리된 값: {input}");
                if (!string.IsNullOrEmpty(input))
                {
                    // RAG 분기 로직 제거됨 -> 무조건 일반 메시지 전송
                    _serverClient.SendMessageToServer(input, SettingData.IsThink);
                }
            }

            // 입력 필드 초기화
            if (_inputHandler != null && _inputHandler.Inputs is { Count: > (int)UISettingEnums.InputEnum.InputField })
            {
                var inputField = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.InputField];
                if (inputField != null)
                {
                    inputField.text = null;
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }
        }

        #endregion

        #region Focus & Condition Checks (포커스 및 상태 확인)

        private void OnFocusIn(string _)
        {
            _isFocused = true;
            CheckConditions();
        }

        private void OnFocusOut(string _)
        {
            _isFocused = false;
            CheckConditions();
        }

        void OnTextChanged(string text)
        {
            _currentText = text;
            CheckConditions();
        }

        /// <summary>
        /// 현재 입력 상태(IsInputText)를 갱신합니다.
        /// </summary>
        void CheckConditions()
        {
            if (!SettingData.IsDebug)
            {
                IsInputText = _isFocused || !string.IsNullOrEmpty(_currentText);
                return;
            }

            // 디버그 모드일 때 상세 로그 출력
            switch (_isFocused)
            {
                case true when !string.IsNullOrEmpty(_currentText):
                    if(SettingData.IsDebug) Debug.Log("포커스 O + 텍스트 있음");
                    IsInputText = true;
                    break;
                case false when !string.IsNullOrEmpty(_currentText):
                    if(SettingData.IsDebug) Debug.Log("포커스 X + 텍스트 있음");
                    IsInputText = true;
                    break;
                case true:
                    if(SettingData.IsDebug) Debug.Log("포커스 O + 텍스트 없음");
                    IsInputText = true;
                    break;
                default:
                    if(SettingData.IsDebug) Debug.Log("포커스 없음");
                    IsInputText = false;
                    break;
            }
        }

        #endregion

        #region Setting Data Binding (설정 데이터 바인딩)

        // 각 설정 항목별 UI 값 적용 및 로드 메서드들

        public void SetserverIP(bool isApply)
        {
            if (isApply)
            {
                SettingData.ServerIP = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.ServerIP].text;
                PlayerPrefs.SetString("SetserverIP", SettingData.ServerIP);
            }
            else
            {
                _inputHandler.Inputs[(int)UISettingEnums.InputEnum.ServerIP].text = SettingData.ServerIP;
            }
        }

        public void SetMysqlIP(bool isApply)
        {
            if (isApply)
            {
                SettingData.MySqlIp = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.SqlIP].text;
                PlayerPrefs.SetString("SetMySqlIP", SettingData.MySqlIp);
            }
            else
            {
                _inputHandler.Inputs[(int)UISettingEnums.InputEnum.SqlIP].text = SettingData.MySqlIp;
            }
        }

        public void SetdatabaseName(bool isApply)
        {
            if (isApply)
            {
                SettingData.DatabaseName = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.DatabaseName].text;
                PlayerPrefs.SetString("SetdatabaseName", SettingData.DatabaseName);
            }
            else
            {
                _inputHandler.Inputs[(int)UISettingEnums.InputEnum.DatabaseName].text = SettingData.DatabaseName;
            }
        }

        public void SetsqlUserName(bool isApply)
        {
            if (isApply)
            {
                SettingData.SqlUserName = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.SqlUserName].text;
                PlayerPrefs.SetString("SetsqlUserName", SettingData.SqlUserName);
            }
            else
            {
                _inputHandler.Inputs[(int)UISettingEnums.InputEnum.SqlUserName].text = SettingData.SqlUserName;
            }
        }

        public void SetsqlPassword(bool isApply)
        {
            if (isApply)
            {
                SettingData.SqlPassword = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.SqlPassword].text;
                PlayerPrefs.SetString("SetsqlPassword", SettingData.SqlPassword);
            }
            else
            {
                _inputHandler.Inputs[(int)UISettingEnums.InputEnum.SqlPassword].text = SettingData.SqlPassword;
            }
        }

        public void SetsqlPort(bool isApply)
        {
            if (isApply)
            {
                SettingData.SqlPort = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.SqlPort].text;
                PlayerPrefs.SetString("SetsqlPort", SettingData.SqlPort);
            }
            else
            {
                _inputHandler.Inputs[(int)UISettingEnums.InputEnum.SqlPort].text = SettingData.SqlPort;
            }
        }

        public void SetserverPort(bool isApply)
        {
            if (isApply)
            {
                SettingData.ServerPort = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.ServerPort].text;
                PlayerPrefs.SetString("SetserverPort", SettingData.ServerPort);
            }
            else
            {
                _inputHandler.Inputs[(int)UISettingEnums.InputEnum.ServerPort].text = SettingData.ServerPort;
            }
        }

        public void SetUserName(bool isApply)
        {
            if (isApply)
            {
                SettingData.UserName = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.UserName].text; 
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"입력창 유저 이름 : {_inputHandler.Inputs[(int)UISettingEnums.InputEnum.UserName].text}");
                PlayerPrefs.SetString("UserName",SettingData.UserName);
            }
            else
            {
                _inputHandler.Inputs[(int)UISettingEnums.InputEnum.UserName].text = SettingData.UserName;
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"입력창 유저 이름 : {_inputHandler.Inputs[(int)UISettingEnums.InputEnum.UserName].text}");
            }
        }

        
        #endregion

        #region Input Validation & Control (입력 검증 및 제어)

        /// <summary>
        /// 한글 입력 및 공백 입력을 제어합니다. (비밀번호 입력창 등에서 사용)
        /// </summary>
        public void LockOfKorean(TMP_InputField inputField)
        {
            string inputText = inputField.text;
            if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"받아오는 입력 데이터 필드 : {inputText}");

            // 공백 제거 처리
            SpaceLock(inputField);

            // 한글이나 공백이 없고 엔터키가 눌리지 않은 경우 경고 메시지 숨김
            if (!KoreanRegex.IsMatch(inputText) && !SpaceRegex.IsMatch(inputText) && !(Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame)))
            {
                if (_panelManager.Panels[(int)UISettingEnums.PanelsEnum.LockPanel].activeSelf)
                {
                    _lockController.UnlockAnnouncementObject.SetActive(false);
                    _lockController.UnlockAnnouncementObject.GetComponent<TMP_Text>().text = null;
                }
                else
                {
                    _lockController.PasswordCheckAnnouncement.SetActive(false);
                    _lockController.PasswordCheckAnnouncement.GetComponent<TMP_Text>().text = null;
                }
            }
        }

        /// <summary>
        /// 입력 필드에서 공백을 제거합니다.
        /// </summary>
        public void SpaceLock(TMP_InputField inputField)
        {
            string inputText = inputField.text;
            if (SpaceRegex.IsMatch(inputText))
            {
                if (inputField.text == " ")
                {
                    if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log("공백상태에서 띄어쓰기가 입력됨");
                    inputField.SetTextWithoutNotify(null); 
                }
                else if  (inputField.name != "FeedbackInput") // 피드백 입력창은 공백 허용
                {
                    if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log("텍스트 입력 상태에서 띄어쓰기가 입력됨");
                    inputField.SetTextWithoutNotify(SpaceRegex.Replace(inputText, ""));
                }
            }
        }

        /// <summary>
        /// 포커스 여부에 따라 IME 모드를 제어합니다.
        /// </summary>
        public void IMEController(bool isFocused)
        {
            if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"ime 차단 시작 : {isFocused}");
            _isLockIme = isFocused;
        }

        #endregion

        #region Helper Methods (보조 메서드)

        private bool InRangeTab(int i, int start, int endExclusive) => i >= start && i < endExclusive;
        
        private bool InRangeEnter(int value, UISettingEnums.InputEnum start, UISettingEnums.InputEnum endExclusive)
        {
            return value >= (int)start && value < (int)endExclusive;
        }

        #endregion
    }
}