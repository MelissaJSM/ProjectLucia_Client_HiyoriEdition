using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ProjectLucia.Server;
using ProjectLucia.Status;
using ProjectLucia.ThirdParty.Whisper.Runtime;
using ProjectLucia.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// UI의 Dropdown 요소들을 관리하고, 선택된 값에 따라 설정을 변경하는 매니저 클래스입니다.
    /// 그래픽, 오디오, 언어 모델 등 다양한 설정을 처리합니다.
    /// </summary>
    public class DropdownManager : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("관리할 TMP_Dropdown 리스트")]
        [SerializeField] private List<TMP_Dropdown> dropdowns;
        public List<TMP_Dropdown> Dropdowns => dropdowns;

        [Tooltip("안티앨리어싱 설정에 따라 활성화/비활성화할 UI 패널 리스트")]
        [SerializeField] private List<GameObject> antiList;

        #endregion

        #region Private Fields (비공개 필드)

        private SaveController _saveController;
        private ToggleManager _toggleManager;
        private SideBarManager _sideBarManager;
        private SettingController _settingController;
        private AudioModelDownloader _audioModelDownloader;
        private WhisperManager _whisperManager;
        private SystemInformation _systemInformation;
        private TextManager _textManager;

        // 메인 카메라 캐시
        private Camera _mainCam;
        private Camera GetMainCam()
        {
            if (_mainCam == null) _mainCam = Camera.main;
            return _mainCam;
        }

        // Anti UI 패널 토글 최적화용 인덱스
        private int _lastAntiIndex = -1;

        // 다운로드 상태 UI 캐시
        private GameObject _dlStatusGo;

        // Whisper 모델 설정 중복 방지용 토큰 및 인덱스
        private CancellationTokenSource _setWhisperCts;
        private int _lastWhisperIndex = int.MinValue;

        // 런타임 모델 루트 경로 (PersistentDataPath)
        private string RuntimeModelRoot =>
            Path.Combine(Application.persistentDataPath, "Whisper");

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 매니저 참조 가져오기
            _saveController       = GameManager.Instance.SaveController;
            _toggleManager        = GameManager.Instance.ToggleManager;
            _sideBarManager       = GameManager.Instance.SideBarManager;
            _settingController    = GameManager.Instance.SettingController;
            _audioModelDownloader = GameManager.Instance.AudioModelDownloader;
            _whisperManager       = GameManager.Instance.WhisperManager;
            _systemInformation    = GameManager.Instance.SystemInformation;
            _textManager          = GameManager.Instance.TextManager;

            _mainCam = Camera.main;

            // 다운로드 상태 UI 찾기
            if (_audioModelDownloader?.WhisperDownloadScreen != null)
            {
                var t = _audioModelDownloader.WhisperDownloadScreen.transform;
                var found = t.Find("DownLoadStatus");
                _dlStatusGo = found != null ? found.gameObject : null;
            }

            // 런타임 모델 루트 폴더 생성 보장
            try
            {
                if (!Directory.Exists(RuntimeModelRoot))
                    Directory.CreateDirectory(RuntimeModelRoot);
            }
            catch (Exception e)
            {
                if (SettingData.IsDebug)
                    if(SettingData.IsDebug) Debug.LogWarning($"[DropdownManager] 모델 루트 생성 실패: {e.Message}");
            }
        }

        #endregion

        #region Graphic Settings (그래픽 설정)

        /// <summary>
        /// 그래픽 품질 프리셋을 변경합니다.
        /// </summary>
        /// <param name="isClick">UI 클릭 여부 (true: UI 값 적용, false: 저장된 값 적용)</param>
        public void DropdownController(bool isClick)
        {
            int value = isClick
                ? Dropdowns[(int)UISettingEnums.DropDownEnum.PresetDropDown].value
                : SettingData.PresetIndex;

            if (!isClick)
                Dropdowns[(int)UISettingEnums.DropDownEnum.PresetDropDown].SetValueWithoutNotify(value);

            if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"프리셋 값 변경 : {value}");

            // 품질 수준 설정 (적용은 OnGraphic에서 수행)
            QualitySettings.SetQualityLevel(value, false);

            if (isClick)
                _saveController?.OnGraphic(true);

            // 커스텀 모드일 때만 세부 설정 활성화
            bool isInteractable = value == (int)UISettingEnums.PresetListEnum.Custom;

            Dropdowns[(int)UISettingEnums.DropDownEnum.VSync].interactable           = isInteractable;
            Dropdowns[(int)UISettingEnums.DropDownEnum.AntiDropDown].interactable    = isInteractable;
            Dropdowns[(int)UISettingEnums.DropDownEnum.MSAAQuality].interactable     = isInteractable;
            _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsFXAAFastMode].interactable  = isInteractable;
            _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsFXAAAlphaKeep].interactable = isInteractable;
            Dropdowns[(int)UISettingEnums.DropDownEnum.SmaaQuality].interactable     = isInteractable;

            _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.JitterSpreadScrollbar].interactable     = isInteractable;
            _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.StaionaryBlendingScrollbar].interactable= isInteractable;
            _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.MotionBlendingScrollbar].interactable   = isInteractable;
            _sideBarManager.Scrollbars[(int)UISettingEnums.ScrollbarEnum.SharpnessScrollbar].interactable        = isInteractable;

            Dropdowns[(int)UISettingEnums.DropDownEnum.AnisotropicFiltering].interactable = isInteractable;
            Dropdowns[(int)UISettingEnums.DropDownEnum.MipMap].interactable               = isInteractable;
            _toggleManager.Toggles[(int)UISettingEnums.TogglesEnum.IsDynamicResolution].interactable = isInteractable;
            Dropdowns[(int)UISettingEnums.DropDownEnum.RenderingPath].interactable        = isInteractable;
        }

        /// <summary>
        /// 안티앨리어싱 모드를 설정합니다.
        /// </summary>
        public void SetAntiUI(bool isClick)
        {
            int value = isClick
                ? Dropdowns[(int)UISettingEnums.DropDownEnum.AntiDropDown].value
                : SettingData.AntiIndex;

            if (!isClick)
                Dropdowns[(int)UISettingEnums.DropDownEnum.AntiDropDown].SetValueWithoutNotify(value);

            if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"Anti value: {value}");

            // UI 패널 전환 최적화
            if (_lastAntiIndex != value)
            {
                if (_lastAntiIndex >= 0 && _lastAntiIndex < antiList.Count)
                    antiList[_lastAntiIndex].SetActive(false);

                if (value >= 0 && value < antiList.Count)
                    antiList[value].SetActive(true);

                _lastAntiIndex = value;
            }

            var cam = GetMainCam();
            var ppl = _settingController?.PostProcessLayer;
            bool isMsaa = value == (int)UISettingEnums.AntiListEnum.MSAA;

            if (cam != null)
                cam.allowMSAA = isMsaa;

            // MSAA가 아니면 Unity 기본 AA 끄기
            if (!isMsaa)
                QualitySettings.antiAliasing = 0;

            // PostProcessLayer AA 설정
            if (ppl != null)
                ppl.antialiasingMode = isMsaa ? PostProcessLayer.Antialiasing.None
                                              : (PostProcessLayer.Antialiasing)value;

            // 동적 해상도 토글 상태 업데이트
            var dynRes = _toggleManager?.Toggles[(int)UISettingEnums.TogglesEnum.IsDynamicResolution];
            if (dynRes != null) dynRes.gameObject.SetActive(isMsaa);
        }

        /// <summary>
        /// MSAA 품질을 설정합니다. (2x, 4x, 8x)
        /// </summary>
        public void SetMsaaQuality(bool isClick)
        {
            int value = isClick
                ? Dropdowns[(int)UISettingEnums.DropDownEnum.MSAAQuality].value
                : SettingData.IsMsaaQuality;

            if (!isClick)
                Dropdowns[(int)UISettingEnums.DropDownEnum.MSAAQuality].SetValueWithoutNotify(value);

            int[] map = { 2, 4, 8 };
            int aa = (value >= 0 && value < map.Length) ? map[value] : 0;
            QualitySettings.antiAliasing = aa;

            if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log("Current MSAA: " + QualitySettings.antiAliasing);
        }

        /// <summary>
        /// SMAA 품질을 설정합니다.
        /// </summary>
        public void SetSMAAQuality(bool isClick)
        {
            int value = isClick
                ? Dropdowns[(int)UISettingEnums.DropDownEnum.SmaaQuality].value
                : SettingData.IsSmaaQuality;

            if (!isClick)
                Dropdowns[(int)UISettingEnums.DropDownEnum.SmaaQuality].SetValueWithoutNotify(value);

            var ppl = _settingController?.PostProcessLayer;
            if (ppl != null && ppl.subpixelMorphologicalAntialiasing != null)
            {
                ppl.subpixelMorphologicalAntialiasing.quality =
                    (SubpixelMorphologicalAntialiasing.Quality)value;

                if (SettingData.IsDebug)
                    if(SettingData.IsDebug) Debug.Log($"SMAA 품질: {ppl.subpixelMorphologicalAntialiasing.quality}");
            }
        }

        /// <summary>
        /// 이방성 필터링(Anisotropic Filtering)을 설정합니다.
        /// </summary>
        public void SetAnisotropicFlitering(bool isClick)
        {
            int value = isClick
                ? Dropdowns[(int)UISettingEnums.DropDownEnum.AnisotropicFiltering].value
                : SettingData.IsAnisotropicFiltering;

            if (!isClick)
                Dropdowns[(int)UISettingEnums.DropDownEnum.AnisotropicFiltering].SetValueWithoutNotify(value);

            QualitySettings.anisotropicFiltering = (AnisotropicFiltering)value;

            if (SettingData.IsDebug)
                if(SettingData.IsDebug) Debug.Log($"이방성 텍스쳐: {QualitySettings.anisotropicFiltering}");
        }

        /// <summary>
        /// 밉맵(MipMap) 제한을 설정합니다.
        /// </summary>
        public void SetMipMap(bool isClick)
        {
            int value = isClick
                ? Dropdowns[(int)UISettingEnums.DropDownEnum.MipMap].value
                : SettingData.IsMipMap;

            if (!isClick)
                Dropdowns[(int)UISettingEnums.DropDownEnum.MipMap].SetValueWithoutNotify(value);

            QualitySettings.globalTextureMipmapLimit = value;

            if (SettingData.IsDebug)
                if(SettingData.IsDebug) Debug.Log($"텍스처 품질(전역 밉맵 제한): {QualitySettings.globalTextureMipmapLimit}");
        }

        /// <summary>
        /// 렌더링 경로(Rendering Path)를 설정합니다.
        /// </summary>
        public void SetRenderingPath(bool isClick)
        {
            int value = isClick
                ? Dropdowns[(int)UISettingEnums.DropDownEnum.RenderingPath].value
                : SettingData.IsRenderingPath;

            if (!isClick)
                Dropdowns[(int)UISettingEnums.DropDownEnum.RenderingPath].SetValueWithoutNotify(value);

            var cam = GetMainCam();
            if (cam != null)
            {
                cam.renderingPath = (RenderingPath)value;

                if (SettingData.IsDebug)
                {
                    if(SettingData.IsDebug) Debug.Log($"렌더링 경로 : {cam.renderingPath}");
                    if(SettingData.IsDebug) Debug.Log($"렌더링 경로 수치 : {(int)cam.renderingPath}");
                }
            }
        }

        /// <summary>
        /// 수직 동기화(VSync)를 설정합니다.
        /// </summary>
        public void SetVsync(bool isClick)
        {
            int value = isClick
                ? Dropdowns[(int)UISettingEnums.DropDownEnum.VSync].value
                : SettingData.IsVsync;

            if (!isClick)
                Dropdowns[(int)UISettingEnums.DropDownEnum.VSync].SetValueWithoutNotify(value);

            QualitySettings.vSyncCount = value;

            // VSync가 꺼져 있을 때만 프레임 제한 슬라이더 표시
            var frameLimitParent = _sideBarManager?.Scrollbars[(int)UISettingEnums.ScrollbarEnum.FrameLimitScrollbar]?.transform.parent;
            if (frameLimitParent != null)
                frameLimitParent.gameObject.SetActive(value == (int)UISettingEnums.VsyncEnum.None);
        }

        /// <summary>
        /// 아이콘 페이드 설정을 변경합니다.
        /// </summary>
        public void SetIconFade(bool isClick)
        {
            int value = isClick
                ? Dropdowns[(int)UISettingEnums.DropDownEnum.IconFade].value
                : SettingData.IsIconFade;

            if (!isClick)
                Dropdowns[(int)UISettingEnums.DropDownEnum.IconFade].SetValueWithoutNotify(value);
        }

        #endregion

        #region Audio & Whisper Settings (오디오 및 Whisper 설정)

        /// <summary>
        /// 마이크 장치를 설정합니다.
        /// </summary>
        public void SetMicDevice(bool isClick)
        {
            if (isClick)
            {
                // UI에서 직접 변경 시 값만 읽음
                _ = Dropdowns[(int)UISettingEnums.DropDownEnum.MicDevice].value;
            }
            else
            {
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"마이크 디바이스: {SettingData.MicDevice}");
                
                // 저장된 이름으로 인덱스 찾기
                int idx = Dropdowns[(int)UISettingEnums.DropDownEnum.MicDevice]
                    .options.FindIndex(o => o.text == SettingData.MicDevice);
                
                Dropdowns[(int)UISettingEnums.DropDownEnum.MicDevice]
                    .SetValueWithoutNotify(idx == -1 ? 0 : idx);

                if (SettingData.IsDebug)
                {
                    if(SettingData.IsDebug) Debug.Log($"설정된 사운드 밸류값 : {SettingData.MicDevice}");
                    if(SettingData.IsDebug) Debug.Log($"설정된 사운드 드롭다운 값 : {Dropdowns[(int)UISettingEnums.DropDownEnum.MicDevice].value}");
                }
            }
        }

        /// <summary>
        /// Whisper 모델을 설정합니다. (비동기 처리)
        /// 모델 파일 존재 여부 및 VRAM 용량을 확인하고, 필요 시 다운로드 UI를 표시합니다.
        /// </summary>
        public async void SetWhisperModel(bool isClick)
        {
            try
            {
                int value = isClick
                    ? Dropdowns[(int)UISettingEnums.DropDownEnum.WhisperModel].value
                    : SettingData.SetWhisperModel;

                if (!isClick)
                    Dropdowns[(int)UISettingEnums.DropDownEnum.WhisperModel].SetValueWithoutNotify(value);

                // 중복 호출 방지
                if (_lastWhisperIndex == value)
                    return;
                _lastWhisperIndex = value;

                // 이전 작업 취소 및 새 토큰 생성
                _setWhisperCts?.Cancel();
                _setWhisperCts = new CancellationTokenSource();
                var ct = _setWhisperCts.Token;

                // 0번 인덱스(미사용) 처리
                if (value == 0)
                {
                    SafeSetActive(_audioModelDownloader?.WhisperDownloadScreen, false);
                    SetGpuStatus("미사용", Color.black);
                    if (_whisperManager != null) _whisperManager.ModelPath = null;
                    SettingData.IsExistedWhisper = true;
                    return;
                }

                // 모델 파일명 조회
                string selectedFile = (_whisperManager != null &&
                                       _whisperManager.WhisperModelList.TryGetValue(value, out var fname))
                                      ? fname : null;

                if (string.IsNullOrEmpty(selectedFile))
                {
                    if (SettingData.IsDebug)
                        if(SettingData.IsDebug) Debug.LogWarning("[DropdownManager] WhisperModelList 키/값이 유효하지 않습니다.");
                    return;
                }

                // 경로 설정 (상대 경로 및 절대 경로)
                string relativeModelPath = Path.Combine("Whisper", selectedFile);
                string saPhysicalPath = Path.Combine(Application.streamingAssetsPath, "Whisper", selectedFile);
                
                if(SettingData.IsDebug) Debug.Log($"***모델 설정용 : {relativeModelPath} \n 모델 실제 경로 : {saPhysicalPath}");

                // UI 업데이트
                SafeSetActive(_audioModelDownloader?.WhisperDownloadScreen, true);
                SafeSetActive(_dlStatusGo, false);

                // GPU VRAM 체크 (비동기)
                bool vramOk = true;
                if (SettingData.IsMatchedGPU && _systemInformation != null)
                {
                    vramOk = await Task.Factory.StartNew(() =>
                    {
                        ct.ThrowIfCancellationRequested();
                        try { return _systemInformation.SystemModelVramCheck(value); }
                        catch { return true; }
                    }, ct, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
                }
                ct.ThrowIfCancellationRequested();

                if (!vramOk)
                {
                    SetGpuStatus("부족", Color.red);
                    SafeSetActive(_audioModelDownloader?.WhisperDownloadScreen, false);
                    SafeSetActive(_dlStatusGo, false);
                    return;
                }

                // 파일 존재 여부 체크 (비동기)
                bool exists = await Task.Factory.StartNew(() =>
                {
                    ct.ThrowIfCancellationRequested();
                    // 원본, q8, q5 파일 모두 확인
                    string path1 = Path.Combine(Application.streamingAssetsPath, "Whisper", selectedFile);
                    string path2 = Path.Combine(Application.streamingAssetsPath, "Whisper", selectedFile.Replace(".bin", "-q8.bin"));
                    string path3 = Path.Combine(Application.streamingAssetsPath, "Whisper", selectedFile.Replace(".bin", "-q5.bin"));
                    return File.Exists(path1) && File.Exists(path2) && File.Exists(path3);
                }, ct, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);

                SettingData.IsExistedWhisper = exists;

                if (SettingData.IsExistedWhisper)
                {
                    // 모델 존재 시 경로 설정
                    if (_whisperManager != null)
                        _whisperManager.ModelPath = relativeModelPath;

                    SafeSetActive(_audioModelDownloader?.WhisperDownloadScreen, false);
                    SafeSetActive(_dlStatusGo, false);
                }
                else
                {
                    // 모델 미존재 시 다운로드 안내
                    var gpuText = _textManager?.Texts[(int)UISettingEnums.TextsEnum.GPUStatus];
                    bool isGpuRed = (gpuText != null && gpuText.color == Color.red);

                    SafeSetActive(_audioModelDownloader?.WhisperDownloadScreen, !isGpuRed);
                    SafeSetActive(_dlStatusGo, false);

                    // 다운로드 체크 루틴 호출
                    if (!SettingData.IsStartMode && _whisperManager != null)
                    {
                        _ = Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                _whisperManager.CheckWhisperModel(value, selectedFile, relativeModelPath);
                            }
                            catch (Exception ex)
                            {
                                if (SettingData.IsDebug)
                                    if(SettingData.IsDebug) Debug.LogWarning($"[DropdownManager] CheckWhisperModel 예외: {ex.Message}");
                            }
                        }, ct, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
                    }
                }
                if(SettingData.IsDebug) Debug.Log($"***모델 경로 최종 안내 : {_whisperManager.ModelPath}");
            }
            catch (OperationCanceledException)
            {
                if (SettingData.IsDebug)
                    if(SettingData.IsDebug) Debug.Log("[DropdownManager] SetWhisperModel 취소됨(디바운스).");
            }
            catch (Exception ex)
            {
                if (SettingData.IsDebug)
                    if(SettingData.IsDebug) Debug.LogError($"[DropdownManager] SetWhisperModel 예외: {ex}");
            }
        }

        /// <summary>
        /// Whisper 양자화(Quantization) 설정을 변경합니다.
        /// </summary>
        public void SetWhisperQuantization(bool isClick)
        {
            var dropdown = Dropdowns[(int)UISettingEnums.DropDownEnum.WhisperQuantization];

            if (isClick)
            {
                SettingData.WhisperQuantization = dropdown.options[dropdown.value].text;
                if (SettingData.IsDebug)
                    if(SettingData.IsDebug) Debug.Log($"Whisper Quantization: {SettingData.WhisperQuantization}");
            }
            else
            {
                int index = dropdown.options.FindIndex(o => o.text == SettingData.WhisperQuantization);
                if (index != -1)
                {
                    dropdown.SetValueWithoutNotify(index);
                }
                else if (dropdown.options.Count > 0)
                {
                    dropdown.SetValueWithoutNotify(0);
                }
            }
        }

        /// <summary>
        /// Whisper 모델 다운로드 완료 후 설정을 적용합니다.
        /// </summary>
        public void SetWhisperModelAfterDownload(int modelIndex, string relativeModelPath)
        {
            if (SettingData.IsDebug)
                if(SettingData.IsDebug) Debug.Log($"[DropdownManager] 모델 다운로드 완료 후 설정: {relativeModelPath}");

            _lastWhisperIndex = modelIndex;

            if (_whisperManager != null)
                _whisperManager.ModelPath = relativeModelPath;

            SettingData.IsExistedWhisper = true;

            SafeSetActive(_audioModelDownloader?.WhisperDownloadScreen, false);
            SafeSetActive(_dlStatusGo, false);
            
            if(SettingData.IsDebug) Debug.Log($"***모델 경로 최종 안내 : {_whisperManager.ModelPath}");
        }

        #endregion

        #region Other Settings (기타 설정)

        /// <summary>
        /// 기본 언어 모델을 설정합니다.
        /// </summary>
        public void SetDefaultLanguage(bool isClick)
        {
            int value = isClick
                ? Dropdowns[(int)UISettingEnums.DropDownEnum.DefaultModel].value
                : SettingData.DefaultModel;

            if (!isClick)
                Dropdowns[(int)UISettingEnums.DropDownEnum.DefaultModel].SetValueWithoutNotify(value);

            var lang = (UISettingEnums.DefaultLanguageEnum)value;
            string langStr = lang.ToString();
            if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"언어: {langStr}");

            if (_whisperManager != null)
                _whisperManager.language = langStr;
        }

        /// <summary>
        /// 말풍선 텍스트 표시 설정을 변경합니다.
        /// </summary>
        public void SetBubbleText(bool isClick)
        {
            int value;
            if (isClick)
            {
                value = Dropdowns[(int)UISettingEnums.DropDownEnum.BubbleText].value;
            }
            else
            {
                value = SettingData.BubbleTextValue;
                Dropdowns[(int)UISettingEnums.DropDownEnum.BubbleText]
                    .SetValueWithoutNotify(value);
            }

            if (_settingController?.FontSizeObject != null)
                _settingController.FontSizeObject.SetActive(value > 0);
        }

        /// <summary>
        /// 리얼타임 픽셀 품질을 설정합니다.
        /// </summary>
        public void SetRealtimePixel(bool isClick)
        {
            if (isClick)
            {
                // UI 조작 시에는 SettingData 변경 안 함 (별도 로직 처리)
            }
            else
            {
                var dropdown = Dropdowns[(int)UISettingEnums.DropDownEnum.RealtimePixel];
                int idx = dropdown.options.FindIndex(o => o.text == SettingData.RealTalkPixel);
                if (idx != -1)
                {
                    dropdown.SetValueWithoutNotify(idx);
                }
            }
        }

        /// <summary>
        /// 사용자 성별을 설정합니다.
        /// </summary>
        public void SetUserGender(bool isClick)
        {
            if (isClick)
            {
                // UI 조작 시에는 SettingData 변경 안 함
            }
            else
            {
                Dropdowns[(int)UISettingEnums.DropDownEnum.UserGender]
                    .SetValueWithoutNotify(SettingData.UserGender);
            }
        }

        /// <summary>
        /// 카메라 장치를 설정합니다. (미구현)
        /// </summary>
        public void SetCameraDevice(bool isClick)
        {
            // 추후 구현 예정
        }

        #endregion

        #region Helper Methods (보조 메서드)

        /// <summary>
        /// GPU 상태 텍스트와 색상을 업데이트합니다.
        /// </summary>
        private void SetGpuStatus(string text, Color color)
        {
            if (_textManager == null) return;
            var idx = (int)UISettingEnums.TextsEnum.GPUStatus;
            if (_textManager.Texts != null && idx < _textManager.Texts.Count && _textManager.Texts[idx] != null)
            {
                _textManager.Texts[idx].text = text;
                _textManager.Texts[idx].color = color;
            }
        }

        /// <summary>
        /// 게임 오브젝트의 활성화 상태를 안전하게 변경합니다. (null 체크 및 상태 비교)
        /// </summary>
        private static void SafeSetActive(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        #endregion
    }
}
