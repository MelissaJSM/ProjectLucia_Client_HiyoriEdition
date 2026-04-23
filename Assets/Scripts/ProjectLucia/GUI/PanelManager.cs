using System;
using System.Collections.Generic;
using ProjectLucia.Server;
using ProjectLucia.Status;
using ProjectLucia.ThirdParty.Whisper.Runtime;
using UnityEngine;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// UI 패널, 탭, 버튼 등의 활성화 상태와 상호작용을 관리하는 매니저 클래스입니다.
    /// 설정창, 대화창, 디버그 UI 등을 제어하며, 다운로드 중 UI 잠금 기능도 제공합니다.
    /// </summary>
    public class PanelManager : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("설정창 캔버스의 RectTransform")]
        [SerializeField] private RectTransform canvasRect;
        public RectTransform CanvasRect => canvasRect;

        [Tooltip("관리할 패널 리스트 (UISettingEnums.PanelsEnum 순서)")]
        [SerializeField] private List<GameObject> panels;
        public List<GameObject> Panels => panels;

        [Tooltip("설정창 내부의 탭 패널 리스트")]
        [SerializeField] private List<GameObject> tabs;

        [Tooltip("캔버스 렌더러가 포함된 버튼 오브젝트 배열")]
        [SerializeField] private GameObject[] buttonCanvasRenderers;
        public GameObject[] ButtonCanvasRenderers => buttonCanvasRenderers;

        [Header("Debug UI")]
        [Tooltip("디버그 정보를 표시할 캔버스")]
        [SerializeField] private Canvas debugCanvas;
        public Canvas DebugCanvas => debugCanvas;

        #endregion

        #region Private Fields (비공개 필드)

        private TextManager _textManager;
        private WhisperManager _whisperManager;
        private AudioModelDownloader _audioModelDownloader;
        private SettingController _settingController;
        private DropdownManager _dropdownManager;


        // 다운로드 잠금 상태 캐싱 (최적화용)
        private bool? _lastDownloadInteractableState;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 매니저 참조 가져오기
            _textManager            = GameManager.Instance.TextManager;
            _whisperManager         = GameManager.Instance.WhisperManager;
            _audioModelDownloader   = GameManager.Instance.AudioModelDownloader;
            _settingController      = GameManager.Instance.SettingController;
            _dropdownManager        = GameManager.Instance.DropdownManager;

        }

        #endregion

        #region Panel & Tab Control (패널 및 탭 제어)

        /// <summary>
        /// 선택된 탭만 활성화하고 나머지는 비활성화합니다.
        /// </summary>
        /// <param name="selectedTab">활성화할 탭 인덱스</param>
        public void TabUpdate(int selectedTab)
        {
            if (tabs == null || tabs.Count == 0) return;
            if (selectedTab < 0 || selectedTab >= tabs.Count) return;

            for (int i = 0; i < tabs.Count; i++)
            {
                var tabGo = tabs[i];
                if (tabGo == null) continue;

                bool target = (i == selectedTab);
                if (tabGo.activeSelf != target)
                    tabGo.SetActive(target);
            }
        }

        /// <summary>
        /// 대화창(말풍선)에 텍스트를 표시합니다.
        /// 설정에 따라 말풍선을 띄우지 않을 수도 있습니다.
        /// </summary>
        /// <param name="responseText">표시할 텍스트</param>
        /// <param name="isTalk">어시스턴트 발화 여부 (true: 어시스턴트, false: 시스템/기타)</param>
        public void ResponseTextProcess(string responseText, bool isTalk)
        {
            if (SettingData.BubbleTextValue == 0) return; // 말풍선 끄기 설정 시 무시

            if (string.IsNullOrWhiteSpace(responseText)) return;

            int talkValue = Convert.ToInt32(isTalk);
            // 설정값과 발화 타입이 일치할 때만 표시 (예: 어시스턴트만 표시 등)
            if (SettingData.BubbleTextValue != talkValue)
            {
                var talkingPanelIndex = (int)UISettingEnums.PanelsEnum.TalikngPanel;
                if (!IsValid(panels, talkingPanelIndex)) return;

                var panel = panels[talkingPanelIndex];
                if (panel != null && !panel.activeSelf)
                    panel.SetActive(true);

                int respIdx = (int)UISettingEnums.TextsEnum.ResponseText;
                if (_textManager != null && IsValid(_textManager.Texts, respIdx))
                    _textManager.Texts[respIdx].text = responseText;
            }
        }

        /// <summary>
        /// 대화창(말풍선)을 닫고 텍스트를 초기화합니다.
        /// 어시스턴트 발화 종료 시 VAD(음성 감지)를 재개합니다.
        /// </summary>
        /// <param name="isTalk">어시스턴트 발화 여부</param>
        public void ResponseTextEnd(bool isTalk)
        {
            var talkingPanelIndex = (int)UISettingEnums.PanelsEnum.TalikngPanel;
            if (IsValid(panels, talkingPanelIndex))
            {
                var panel = panels[talkingPanelIndex];
                if (panel != null && panel.activeSelf)
                    panel.SetActive(false);
            }

            int respIdx = (int)UISettingEnums.TextsEnum.ResponseText;
            if (_textManager != null && IsValid(_textManager.Texts, respIdx))
                _textManager.Texts[respIdx].text = string.Empty;

            if (isTalk && _whisperManager != null)
                _whisperManager.useVad = true;
        }

        #endregion

        #region UI Locking (UI 잠금 제어)

        /// <summary>
        /// 설정창의 주요 버튼 상호작용을 잠그거나 해제합니다. (잠금 화면 등에서 사용)
        /// </summary>
        /// <param name="isLocked">잠금 여부</param>
        public void LockOfPanel(bool isLocked)
        {
            if (_settingController == null) return;

            SetInteractableSafe(_settingController.RightButton,       !isLocked);
            SetInteractableSafe(_settingController.CancelButton,      !isLocked);
            SetInteractableSafe(_settingController.ShutDownButton,    !isLocked);
            SetInteractableSafe(_settingController.InstructionButton, !isLocked);

            // Voice 탭은 예외적으로 잠금 해제 상태 유지 (기존 정책)
            if (_settingController.TabButtons != null)
            {
                for (int i = 0; i < _settingController.TabButtons.Count; i++)
                {
                    if (i == (int)UISettingEnums.TabsEnum.Voice) continue;
                    SetInteractableSafe(_settingController.TabButtons[i], !isLocked);
                }
            }

            if (_settingController.MainUiButtons != null)
            {
                SetInteractableSafe(_settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.SettingButton], !isLocked);
                SetInteractableSafe(_settingController.MainUiButtons[(int)UISettingEnums.MainUiButtonEnum.LogsButton],    !isLocked);
            }
        }

        /// <summary>
        /// 모델 다운로드 진행 중 UI 상호작용을 잠그거나 해제합니다.
        /// </summary>
        /// <param name="isAudio">오디오 모델 다운로드 여부 (false면 키워드 모델)</param>
        public void DataDownLoadLock(bool isAudio)
        {
            bool audioBusy = false;
            bool keywordBusy = false;

            // 오디오 모델 다운로드 상태 확인
            if (isAudio && _audioModelDownloader != null)
            {
                audioBusy =
                    _audioModelDownloader.VadRequest != null ||
                    _audioModelDownloader.WhisperRequest != null;
            }
            

            bool interactable = !(audioBusy || keywordBusy);

            // 상태 변경 시에만 UI 갱신 (최적화)
            if (_lastDownloadInteractableState.HasValue && _lastDownloadInteractableState.Value == interactable)
            {
                return;
            }
            _lastDownloadInteractableState = interactable;
            

            // 전체 UI 버튼 잠금/해제 적용
            SetInteractableForAll(interactable);

            // Whisper 모델 드롭다운도 함께 제어
            if (_dropdownManager?.Dropdowns != null &&
                IsValid(_dropdownManager.Dropdowns, (int)UISettingEnums.DropDownEnum.WhisperModel))
            {
                _dropdownManager.Dropdowns[(int)UISettingEnums.DropDownEnum.WhisperModel].interactable = interactable;
            }

            if (SettingData.IsDebug)
                if(SettingData.IsDebug) Debug.Log($"[DownloadLock] isAudio={isAudio}, audioBusy={audioBusy}, keywordBusy={keywordBusy}, interactable={interactable}");
        }

        #endregion

        #region Helper Methods (보조 메서드)

        /// <summary>
        /// 리스트 인덱스 유효성 검사
        /// </summary>
        private static bool IsValid<T>(IList<T> list, int index)
        {
            return list != null && index >= 0 && index < list.Count && list[index] != null;
        }

        /// <summary>
        /// Selectable UI 요소의 interactable 속성을 안전하게 설정합니다.
        /// </summary>
        private static void SetInteractableSafe(UnityEngine.UI.Selectable sel, bool state)
        {
            if (sel != null) sel.interactable = state;
        }

        /// <summary>
        /// 설정창 내 모든 주요 버튼의 상호작용 상태를 일괄 설정합니다.
        /// 현재 선택된 탭 버튼은 비활성화 상태를 유지합니다.
        /// </summary>
        private void SetInteractableForAll(bool state)
        {
            if (_settingController == null) return;

            SetInteractableSafe(_settingController.RightButton,       state);
            SetInteractableSafe(_settingController.CancelButton,      state);
            SetInteractableSafe(_settingController.ShutDownButton,    state);
            SetInteractableSafe(_settingController.InstructionButton, state);
            SetInteractableSafe(_settingController.LockButton,        state);

            if (_settingController.TabButtons != null)
            {
                // 현재 활성화된 탭 인덱스 찾기
                int currentTabIndex = -1;
                for (int i = 0; i < tabs.Count; i++)
                {
                    if (tabs[i].activeSelf)
                    {
                        currentTabIndex = i;
                        break;
                    }
                }

                for (int i = 0; i < _settingController.TabButtons.Count; i++)
                {
                    // 활성화(true) 시, 현재 선택된 탭 버튼은 비활성화(false) 유지 (중복 클릭 방지)
                    if (state && i == currentTabIndex)
                    {
                        SetInteractableSafe(_settingController.TabButtons[i], false);
                    }
                    else
                    {
                        SetInteractableSafe(_settingController.TabButtons[i], state);
                    }
                }
            }

            if (_settingController.MainUiButtons != null)
                foreach (var b in _settingController.MainUiButtons)
                    SetInteractableSafe(b, state);
        }

        #endregion
    }
}
