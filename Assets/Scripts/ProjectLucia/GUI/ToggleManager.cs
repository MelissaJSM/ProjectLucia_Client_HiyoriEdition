using System.Collections.Generic;
using ProjectLucia.Server;
using ProjectLucia.Status;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// UI의 토글(Toggle) 요소들을 관리하고, 선택된 값에 따라 설정을 변경하는 매니저 클래스입니다.
    /// Live2D 동작, 그래픽 옵션, 디버그 모드, 다운로드 동의 등의 설정을 처리합니다.
    /// </summary>
    public class ToggleManager : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("관리할 Toggle 리스트 (UISettingEnums.TogglesEnum 순서와 일치해야 함)")]
        [SerializeField] private List<Toggle> toggles; 
        public List<Toggle> Toggles => toggles;

        [Tooltip("추론용 버튼과 이미지")]
        public List<Sprite> thinkTextures;
        [SerializeField] private Image thinkDetector;

        [Tooltip("다운로드 동의 UI 오브젝트 리스트 (VAD, Whisper, Keyword 순)")]
        [SerializeField] private List<GameObject> downLoadObjects;

        #endregion

        #region Private Fields (비공개 필드)

        private PanelManager _panelManager;
        private SettingController _settingController;
        private ActionManager _actionManager;
        private FrameCounter _frameCounter;
        private InputHandler _inputHandler;

        // 캐싱: 카메라 및 포스트 프로세싱
        private Camera _mainCam;
        private UnityEngine.Rendering.PostProcessing.PostProcessLayer _ppl;
        private UnityEngine.Rendering.PostProcessing.FastApproximateAntialiasing _fxaa;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 매니저 참조 가져오기
            _panelManager      = GameManager.Instance.PanelManager;
            _settingController = GameManager.Instance.SettingController;
            _actionManager     = GameManager.Instance.ActionManager;
            _frameCounter      = GameManager.Instance.FrameCounter;
            _inputHandler = GameManager.Instance.InputHandler;

            _mainCam = Camera.main;
            _ppl = _settingController != null ? _settingController.PostProcessLayer : null;
            _fxaa = _ppl != null ? _ppl.fastApproximateAntialiasing : null;
        }

        #endregion

        #region Helper Methods (보조 메서드)

        private Camera GetMainCam()
        {
            if (_mainCam == null) _mainCam = Camera.main;
            return _mainCam;
        }

        #endregion

        #region Live2D & Input Settings (Live2D 및 입력 설정)

        /// <summary>
        /// 키보드 입력 활성화 여부를 설정합니다.
        /// </summary>
        public void IsKeyboardInputController(bool isClick)
        {
            bool value = isClick
                ? Toggles[(int)UISettingEnums.TogglesEnum.IsKeyboard].isOn
                : SettingData.IsInputKeyboard;

            if (!isClick)
                Toggles[(int)UISettingEnums.TogglesEnum.IsKeyboard].SetIsOnWithoutNotify(value);
            
            if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"[Toggle] Keyboard Input: {value}");
        }

        /// <summary>
        /// 마우스 추적(시선 처리) 활성화 여부를 설정합니다.
        /// </summary>
        public void IsMouseChaserController(bool isClick)
        {
            bool value = isClick
                ? Toggles[(int)UISettingEnums.TogglesEnum.IsMouseChaser].isOn
                : SettingData.IsLookTarget;

            if (!isClick)
                Toggles[(int)UISettingEnums.TogglesEnum.IsMouseChaser].SetIsOnWithoutNotify(value);
        }

        /// <summary>
        /// 터치 반응 모션 활성화 여부를 설정합니다.
        /// </summary>
        public void IsTouchMotionController(bool isClick)
        {
            bool value = isClick
                ? Toggles[(int)UISettingEnums.TogglesEnum.IsTouchMotion].isOn
                : SettingData.L2dClicked;

            if (!isClick)
                Toggles[(int)UISettingEnums.TogglesEnum.IsTouchMotion].SetIsOnWithoutNotify(value);
        }

        /// <summary>
        /// 호출 웨이크업 활성화 여부를 설정합니다.
        /// </summary>
        public void IsWakeUpController(bool isClick)
        {
            bool value = isClick
                ? Toggles[(int)UISettingEnums.TogglesEnum.IsCallNow].isOn
                : SettingData.IsCallNow;

            if (!isClick)
                Toggles[(int)UISettingEnums.TogglesEnum.IsCallNow].SetIsOnWithoutNotify(value);

            
            GameObject parentObject = _inputHandler.Inputs[(int)UISettingEnums.InputEnum.CallName].transform.parent.gameObject;

            parentObject.SetActive(value);
        }
        
        #endregion

        #region Idle Motion Settings (Idle 모션 설정)

        /// <summary>
        /// 대기 모션(Idle Motion) 활성화 여부를 설정합니다.
        /// </summary>
        public void IsIdleMotionController(bool isClick)
        {
            bool value = isClick
                ? Toggles[(int)UISettingEnums.TogglesEnum.IsIdleMotion].isOn
                : SettingData.IsIdleMotion;

            if (!isClick)
                Toggles[(int)UISettingEnums.TogglesEnum.IsIdleMotion].SetIsOnWithoutNotify(value);

            SetMotionCheck();
        }

        /// <summary>
        /// 대기 모션 랜덤 재생 활성화 여부를 설정합니다.
        /// </summary>
        public void IsIdleMotionRandomController(bool isClick)
        {
            bool value = isClick
                ? Toggles[(int)UISettingEnums.TogglesEnum.IsIdleMotionRandom].isOn
                : SettingData.IsIdleMotionRandom;

            if (!isClick)
                Toggles[(int)UISettingEnums.TogglesEnum.IsIdleMotionRandom].SetIsOnWithoutNotify(value);

            SetMotionCheck();
        }

        /// <summary>
        /// Idle 모션 설정에 따라 관련 UI(슬라이더 등)의 활성화 상태를 갱신합니다.
        /// </summary>
        private void SetMotionCheck()
        {
            bool isIdle   = Toggles[(int)UISettingEnums.TogglesEnum.IsIdleMotion].isOn;
            bool isRandom = Toggles[(int)UISettingEnums.TogglesEnum.IsIdleMotionRandom].isOn;

            if (_actionManager != null)
            {
                if (_actionManager.IsIdleMotionRandom != null)
                    _actionManager.IsIdleMotionRandom.SetActive(isIdle);
                if (_actionManager.IdleMotionRandomMax != null)
                    _actionManager.IdleMotionRandomMax.SetActive(isIdle && isRandom);
                if (_actionManager.IdleMotionRandomMin != null)
                    _actionManager.IdleMotionRandomMin.SetActive(isIdle && isRandom);
                if (_actionManager.IdleMotionFixed != null)
                    _actionManager.IdleMotionFixed.SetActive(isIdle && !isRandom);
            }
        }

        #endregion

        #region Debug Settings (디버그 설정)

        /// <summary>
        /// 디버그 모드 활성화 여부를 설정합니다.
        /// </summary>
        public void IsDebugController(bool isClick)
        {
            bool value = isClick
                ? Toggles[(int)UISettingEnums.TogglesEnum.IsDebug].isOn
                : SettingData.IsDebug;

            if (!isClick)
                Toggles[(int)UISettingEnums.TogglesEnum.IsDebug].SetIsOnWithoutNotify(value);
            
            if (_panelManager?.DebugCanvas != null)
                _panelManager.DebugCanvas.gameObject.SetActive(value);

            if (_frameCounter != null)
                _frameCounter.isDebug = value;

            if(SettingData.IsDebug) Debug.Log($"[Toggle] Debug: {value}");
        }

        #endregion

        #region Graphic Settings (그래픽 설정)

        /// <summary>
        /// 동적 해상도(Dynamic Resolution) 활성화 여부를 설정합니다.
        /// </summary>
        public void IsSetDynamicResolution(bool isClick)
        {
            bool value = isClick
                ? Toggles[(int)UISettingEnums.TogglesEnum.IsDynamicResolution].isOn
                : SettingData.IsDynamicResolution;

            if (!isClick)
                Toggles[(int)UISettingEnums.TogglesEnum.IsDynamicResolution].SetIsOnWithoutNotify(value);
            
            var cam = GetMainCam();
            if (cam != null)
                cam.allowDynamicResolution = value;

            if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"[Toggle] DynamicResolution: {value}");
        }

        /// <summary>
        /// FXAA Fast Mode 활성화 여부를 설정합니다.
        /// </summary>
        public void IsSetFXAAFastMode(bool isClick)
        {
            bool value = isClick
                ? Toggles[(int)UISettingEnums.TogglesEnum.IsFXAAFastMode].isOn
                : SettingData.IsFxaaFastMode;

            if (!isClick)
                Toggles[(int)UISettingEnums.TogglesEnum.IsFXAAFastMode].SetIsOnWithoutNotify(value);
            
            if (_fxaa != null)
                _fxaa.fastMode = value;

            if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"[Toggle] FXAA FastMode: {_fxaa?.fastMode}");
        }

        /// <summary>
        /// FXAA Keep Alpha 활성화 여부를 설정합니다.
        /// </summary>
        public void IsSetFXAAKeepAlpha(bool isClick)
        {
            bool value = isClick
                ? Toggles[(int)UISettingEnums.TogglesEnum.IsFXAAAlphaKeep].isOn
                : SettingData.IsFxaaAlphaKeep;

            if (!isClick)
                Toggles[(int)UISettingEnums.TogglesEnum.IsFXAAAlphaKeep].SetIsOnWithoutNotify(value);
            
            if (_fxaa != null)
                _fxaa.keepAlpha = value;

            if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"[Toggle] FXAA KeepAlpha: {_fxaa?.keepAlpha}");
        }

        #endregion

        #region Download Settings (다운로드 설정)

        /// <summary>
        /// 다운로드 동의 토글 상태에 따라 다운로드 버튼을 활성화/비활성화합니다.
        /// </summary>
        /// <param name="setObject">대상 인덱스 (0: VAD, 1: Whisper, 2: Alert, 4: 전체 초기화)</param>
        public void IsSetDownloadAgree(int setObject)
        {
            int start = setObject;
            int end   = setObject + 1;

            // 전체 초기화 모드
            if (setObject == 4) { start = 0; end = 3; }

            // 인덱스 범위 안전장치
            start = Mathf.Clamp(start, 0, downLoadObjects.Count);
            end   = Mathf.Clamp(end,   0, downLoadObjects.Count);

            for (int i = start; i < end; i++)
            {
                var container = downLoadObjects[i];
                if (container == null) continue;

                var toggle = container.GetComponentInChildren<Toggle>(true);
                var button = container.GetComponentInChildren<Button>(true);

                if (setObject == 4)
                {
                    // 전체 초기화 시 토글 해제 및 버튼 비활성화
                    if (toggle != null) toggle.SetIsOnWithoutNotify(false);
                    if (button != null) button.interactable = false;
                }
                else
                {
                    // 개별 설정 시 토글 상태에 따라 버튼 활성화
                    if (button != null) button.interactable = (toggle != null && toggle.isOn);
                }
            }
        }

        #endregion

        #region Other Settings (기타 설정)

        /// <summary>
        /// 감정 분석 기능 활성화 여부를 설정합니다.
        /// </summary>
        public void IsEmotion(bool isClick)
        {
            bool value = isClick
                ? Toggles[(int)UISettingEnums.TogglesEnum.IsEmotion].isOn
                : SettingData.IsEmotion;

            if (!isClick)
                Toggles[(int)UISettingEnums.TogglesEnum.IsEmotion].SetIsOnWithoutNotify(value);
        }

        /// <summary>
        /// 추론 기능 활성화 여부를 설정합니다.
        /// 다만 gemma3 는 활성화 하던 말던 동작 안합니다.
        /// </summary>
        public void IsThink()
        {
            SettingData.IsThink = !SettingData.IsThink;
            if(SettingData.IsDebug) Debug.Log($"추론모드 {(SettingData.IsThink ? "활성화" : "비활성화")}");

            if (SettingData.IsThink)
            {
                _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.ThinkButton].image.sprite = thinkTextures[(int)UISettingEnums.ThinkButtonEnums.yes_idle];
                SpriteState newSpriteState = _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.ThinkButton].spriteState;
                newSpriteState.highlightedSprite = thinkTextures[(int)UISettingEnums.ThinkButtonEnums.yes_focus];
                newSpriteState.pressedSprite = thinkTextures[(int)UISettingEnums.ThinkButtonEnums.yes_click];
                
                newSpriteState.selectedSprite = thinkTextures[(int)UISettingEnums.ThinkButtonEnums.yes_idle];
                _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.ThinkButton].spriteState = newSpriteState;
                
                thinkDetector.sprite = thinkTextures[(int)UISettingEnums.ThinkButtonEnums.yes_idle];
            }
            else
            {
                _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.ThinkButton].image.sprite = thinkTextures[(int)UISettingEnums.ThinkButtonEnums.no_idle];
                SpriteState newSpriteState = _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.ThinkButton].spriteState;
                newSpriteState.highlightedSprite = thinkTextures[(int)UISettingEnums.ThinkButtonEnums.no_focus];
                newSpriteState.pressedSprite = thinkTextures[(int)UISettingEnums.ThinkButtonEnums.no_click];
                
                newSpriteState.selectedSprite = thinkTextures[(int)UISettingEnums.ThinkButtonEnums.no_idle];
                _settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.ThinkButton].spriteState = newSpriteState;
                
                thinkDetector.sprite = thinkTextures[(int)UISettingEnums.ThinkButtonEnums.no_idle];
            }
        }
        
        /// <summary>
        /// 대화의 말풍선 기능을 제어합니다.
        /// </summary>
        public void IsThinkBalloon(bool isClick)
        {
            bool value = isClick
                ? Toggles[(int)UISettingEnums.TogglesEnum.IsThinkBalloon].isOn
                : SettingData.IsThinkBalloon;

            if (!isClick)
                Toggles[(int)UISettingEnums.TogglesEnum.IsThinkBalloon].SetIsOnWithoutNotify(value);

        }

        #endregion
    }
}
