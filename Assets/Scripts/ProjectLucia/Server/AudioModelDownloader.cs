using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using ProjectLucia.GUI;
using ProjectLucia.Status;
using ProjectLucia.ThirdParty.Whisper.Runtime.Utils;

namespace ProjectLucia.Server
{
    /// <summary>
    /// Whisper 및 VAD 모델 파일을 다운로드하고 관리하는 클래스입니다.
    /// 다운로드 진행 상황을 UI에 표시하고, 파일 무결성(SHA1)을 검증합니다.
    /// </summary>
    public class AudioModelDownloader : MonoBehaviour
    {
        #region Constants & Fields (상수 및 필드)

        private const string WhisperFolder = "Whisper";
        private const string VadFolder = "Vad";

        [Header("UI References (UI 참조)")]
        [Tooltip("Whisper 모델 다운로드 화면 패널")]
        [SerializeField] private GameObject whisperDownloadScreen;
        public GameObject WhisperDownloadScreen => whisperDownloadScreen;

        [Tooltip("VAD 모델 다운로드 화면 패널")]
        [SerializeField] private GameObject vadDownloadScreen;
        public GameObject VadDownloadScreen => vadDownloadScreen;

        [Tooltip("VAD 다운로드 상태 표시 UI")]
        [SerializeField] private GameObject vadDownloadStatus;
        public GameObject VadDownloadStatus => vadDownloadStatus;

        [Tooltip("음성 모델 리스트 UI")]
        [SerializeField] private GameObject voiceList;
        public GameObject VoiceList => voiceList;

        [Header("File Names (파일명)")]
        [Tooltip("VAD 모델 파일명")]
        public string vadFileName = "silero_vad.onnx";

        [Tooltip("VoxCeleb 모델 파일명")]
        public string voxcelebFileName = "voxceleb.onnx";

        // 코루틴 핸들
        private Coroutine _whisperCoroutine;
        private Coroutine _vadCoroutine;

        // 현재 실행 중인 요청
        public UnityWebRequest WhisperRequest;
        public UnityWebRequest VadRequest;

        // 다운로드 중인 파일명
        private string _currentWhisperFileName;
        private string _currentVadFileName;

        // UI 구조체
        private struct DownloadUI
        {
            public Slider ProgressBar;
            public TMP_Text PercentText;
            public TMP_Text TimeRemainingText;
            public TMP_Text ButtonText;
            public Transform StatusRoot;
        }

        private DownloadUI _whisperUI;
        private DownloadUI _vadUI;

        // 매니저 참조
        private DropdownManager _dropdownManager;
        private MicrophoneRecord _microphoneRecord;
        private SettingController _settingController;

        // 버튼 상호작용 상태
        public bool buttonsAreInteractable = true;

        // UI 업데이트 간격 (최적화)
        private const float UiUpdateInterval = 0.1f;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            // 매니저 참조 가져오기
            _dropdownManager = GameManager.Instance.DropdownManager;
            _microphoneRecord = GameManager.Instance.MicrophoneRecord;
            _settingController = GameManager.Instance.SettingController;

            // Whisper UI 초기화
            var ws = whisperDownloadScreen.transform;
            _whisperUI.StatusRoot = ws.Find("DownLoadStatus");
            _whisperUI.ProgressBar = ws.Find("DownLoadStatus/DownLoadSlider").GetComponent<Slider>();
            _whisperUI.PercentText = ws.Find("DownLoadStatus/DownLoadTextPercent").GetComponent<TMP_Text>();
            _whisperUI.TimeRemainingText = ws.Find("DownLoadStatus/DownLoadTime").GetComponent<TMP_Text>();
            _whisperUI.ButtonText = ws.Find("DownLoadButton/DownLoadButtonText").GetComponent<TMP_Text>();

            // VAD UI 초기화
            var vs = vadDownloadScreen.transform;
            _vadUI.StatusRoot = vs.Find("DownLoadStatus");
            _vadUI.ProgressBar = vs.Find("DownLoadStatus/DownLoadSlider").GetComponent<Slider>();
            _vadUI.PercentText = vs.Find("DownLoadStatus/DownLoadTextPercent").GetComponent<TMP_Text>();
            _vadUI.TimeRemainingText = vs.Find("DownLoadStatus/DownLoadTime").GetComponent<TMP_Text>();
            _vadUI.ButtonText = vs.Find("DownLoadButton/DownLoadButtonText").GetComponent<TMP_Text>();
        }

        private void OnDisable()
        {
            CleanupAll();
        }

        private void OnDestroy()
        {
            CleanupAll();
            Resources.UnloadUnusedAssets();
        }

        #endregion

        #region Public Methods (공개 메서드)
        
        /// <summary>
        /// 다운로드 버튼 클릭 시 호출됩니다.
        /// </summary>
        /// <param name="isWhisper">true면 Whisper 모델, false면 VAD 모델 다운로드</param>
        public void OnDownloadButtonClicked(bool isWhisper)
        {
            if (isWhisper)
            {
                if (_whisperCoroutine == null)
                {
                    var dd = _dropdownManager;
                    var ddIdx = (int)UISettingEnums.DropDownEnum.WhisperModel;

                    if (dd == null || dd.Dropdowns == null || dd.Dropdowns.Count <= ddIdx)
                    {
                        if(SettingData.IsDebug) Debug.LogError("Whisper 드롭다운 참조가 유효하지 않습니다.");
                        return;
                    }

                    int modelIndex = dd.Dropdowns[ddIdx].value;
                    string modelName = dd.Dropdowns[ddIdx].options[modelIndex].text;

                    // 다운로드할 파일 목록 준비 (원본, Q8, Q5)
                    var downloadList = new List<(string fileName, string url, string sha1)>();

                    // 1. Original
                    if (DownloadUrl.WhisperUrls.TryGetValue(modelName, out string url1) &&
                        DownloadSha1.WhisperSha1.TryGetValue(modelName, out string sha1_1))
                    {
                        downloadList.Add((modelName, url1, sha1_1));
                    }

                    // 2. Q8
                    string nameQ8 = modelName.Replace(".bin", "-q8.bin");
                    if (DownloadUrl.WhisperUrlsQ8.TryGetValue(nameQ8, out string url2) &&
                        DownloadSha1.WhisperSha1Q8.TryGetValue(nameQ8, out string sha1_2))
                    {
                        downloadList.Add((nameQ8, url2, sha1_2));
                    }

                    // 3. Q5
                    string nameQ5 = modelName.Replace(".bin", "-q5.bin");
                    if (DownloadUrl.WhisperUrlsQ5.TryGetValue(nameQ5, out string url3) &&
                        DownloadSha1.WhisperSha1Q5.TryGetValue(nameQ5, out string sha1_3))
                    {
                        downloadList.Add((nameQ5, url3, sha1_3));
                    }

                    if (downloadList.Count == 0)
                    {
                        if(SettingData.IsDebug) Debug.LogError($"Whisper URL/SHA1을 찾을 수 없습니다: {modelName}");
                        SafeSetUIError(_whisperUI, "실패", "모델 정보 없음");
                        return;
                    }

                    _whisperCoroutine = StartCoroutine(
                        DownloadWhisperSetRoutine(modelName, modelIndex, downloadList)
                    );
                }
                else
                {
                    CancelDownload(true);
                }
            }
            else
            {
                if (_vadCoroutine == null)
                {
                    var downloadList = new List<(string fileName, string url, string sha1)>();

                    if (!string.IsNullOrEmpty(DownloadUrl.VadUrl) &&
                        !string.IsNullOrEmpty(DownloadSha1.VadExpectedSHA1))
                    {
                        downloadList.Add((vadFileName, DownloadUrl.VadUrl, DownloadSha1.VadExpectedSHA1));
                    }

                    if (!string.IsNullOrEmpty(DownloadUrl.VoxcelebUrl) &&
                        !string.IsNullOrEmpty(DownloadSha1.VoxcelebSHA1))
                    {
                        downloadList.Add((voxcelebFileName, DownloadUrl.VoxcelebUrl, DownloadSha1.VoxcelebSHA1));
                    }

                    if (downloadList.Count == 0)
                    {
                        if(SettingData.IsDebug) Debug.LogError("VAD URL/SHA1이 설정되지 않았습니다.");
                        SafeSetUIError(_vadUI, "실패", "모델 정보 없음");
                        return;
                    }

                    _vadCoroutine = StartCoroutine(DownloadVadSetRoutine(downloadList));
                }
                else
                {
                    CancelDownload(false);
                }
            }
        }

        #endregion

        #region Download Routines (다운로드 루틴)

        private IEnumerator DownloadWhisperSetRoutine(
            string baseModelName,
            int modelIndex,
            List<(string fileName, string url, string sha1)> files)
        {
            for (int i = 0; i < files.Count; i++)
            {
                var (fileName, url, sha1) = files[i];
                _currentWhisperFileName = fileName;
                bool isSuccess = false;

                if(SettingData.IsDebug) Debug.Log($"[DownloadSet] Starting {i + 1}/{files.Count}: {fileName}");

                string stepInfo = $"({i + 1}/{files.Count})";

                yield return StartCoroutine(DownloadRoutine(
                    url: url,
                    fileName: fileName,
                    folderName: WhisperFolder,
                    ui: _whisperUI,
                    expectedSHA1: sha1,
                    onSuccess: () => { isSuccess = true; },
                    setRequestRef: (req) => WhisperRequest = req,
                    clearRequestRef: () => WhisperRequest = null,
                    clearCoroutineRef: () => { /* Do not clear whisperCoroutine here */ },
                    stepInfo: stepInfo
                ));

                if (!isSuccess)
                {
                    if(SettingData.IsDebug) Debug.LogError($"[DownloadSet] Failed at {fileName}. Stopping set.");
                    _whisperCoroutine = null;
                    yield break;
                }
            }

            // All success
            string relativePath = Path.Combine(WhisperFolder, baseModelName);
            _dropdownManager?.SetWhisperModelAfterDownload(modelIndex, relativePath);
            _settingController?.WhisperModelInquiry();

            _whisperCoroutine = null;
        }

        private IEnumerator DownloadVadSetRoutine(List<(string fileName, string url, string sha1)> files)
        {
            for (int i = 0; i < files.Count; i++)
            {
                var (fileName, url, sha1) = files[i];
                _currentVadFileName = fileName;
                bool isSuccess = false;

                if(SettingData.IsDebug) Debug.Log($"[DownloadSet] Starting {i + 1}/{files.Count}: {fileName}");

                string stepInfo = $"({i + 1}/{files.Count})";

                yield return StartCoroutine(DownloadRoutine(
                    url: url,
                    fileName: fileName,
                    folderName: VadFolder,
                    ui: _vadUI,
                    expectedSHA1: sha1,
                    onSuccess: () => { isSuccess = true; },
                    setRequestRef: (req) => VadRequest = req,
                    clearRequestRef: () => VadRequest = null,
                    clearCoroutineRef: () => { /* Do not clear vadCoroutine here */ },
                    stepInfo: stepInfo
                ));

                if (!isSuccess)
                {
                    if(SettingData.IsDebug) Debug.LogError($"[DownloadSet] Failed at {fileName}. Stopping set.");
                    _vadCoroutine = null;
                    yield break;
                }
            }

            // All success
            _microphoneRecord?.CheckVadModel();
            if (vadDownloadScreen != null) vadDownloadScreen.SetActive(false);

            _vadCoroutine = null;
        }

        private IEnumerator DownloadRoutine(
            string url,
            string fileName,
            string folderName,
            DownloadUI ui,
            string expectedSHA1,
            Action onSuccess,
            Action<UnityWebRequest> setRequestRef,
            Action clearRequestRef,
            Action clearCoroutineRef,
            string stepInfo = ""
        )
        {
            SafeActivate(ui.StatusRoot, true);
            SafeSetUIButton(ui, "중지");
            SafeSetProgress(ui, 0f, "0.0%" + (string.IsNullOrEmpty(stepInfo) ? "" : $" {stepInfo}"), "준비 중...");

            float startTime = Time.time;
            string folder = Path.Combine(Application.streamingAssetsPath, folderName);
            EnsureDirectory(folder);
            string fullPath = Path.Combine(folder, fileName);

            UnityWebRequest request = null;
            string finalUrl = null;

            try
            {
                if(SettingData.IsDebug) Debug.Log($"[DOWNLOAD] Start download: url={url}, file={fileName}, path={folder}");

                // 1) 최종 URL 결정
                yield return StartCoroutine(ResolveFinalUrl(url, (u) => finalUrl = u));

                // 2) 실제 다운로드
                request = UnityWebRequest.Get(finalUrl);

                var dh = new DownloadHandlerFile(fullPath)
                {
                    removeFileOnAbort = true
                };
                request.downloadHandler = dh;

                request.redirectLimit = 64;
                request.timeout = 0;
#pragma warning disable CS0618
                request.chunkedTransfer = false;
#pragma warning restore CS0618
                request.SetRequestHeader("Accept", "application/octet-stream");
                request.SetRequestHeader("Accept-Encoding", "identity");
                request.SetRequestHeader("User-Agent", "ProjectLucia/1.0");

                setRequestRef?.Invoke(request);

                var op = request.SendWebRequest();

                // 최적화: UI 업데이트 타이머
                float lastUiUpdateTime = 0f;

                while (!op.isDone)
                {
                    // UI 업데이트 빈도 제한
                    if (Time.time - lastUiUpdateTime >= UiUpdateInterval)
                    {
                        float progress = Mathf.Clamp01(request.downloadProgress);
                        float elapsed = Time.time - startTime;
                        float estimatedTotal = progress > 0f ? (elapsed / progress) : 0f;
                        float remaining = Mathf.Max(0f, estimatedTotal - elapsed);

                        SafeSetProgress(ui, progress, (progress * 100f).ToString("F1") + "%" + (string.IsNullOrEmpty(stepInfo) ? "" : $" {stepInfo}"), $"{remaining:F1}초 남음");
                        lastUiUpdateTime = Time.time;
                    }
                    yield return null;
                }

                SafeSetUIButton(ui, "다운로드");

                if(SettingData.IsDebug) Debug.Log($"[HTTP] code={request.responseCode}, result={request.result}, err={request.error}");
                var respHeaders = request.GetResponseHeaders();
                if (respHeaders != null)
                {
                    foreach (var kv in respHeaders) if(SettingData.IsDebug) Debug.Log($"[HDR] {kv.Key}: {kv.Value}");
                }

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (!File.Exists(fullPath))
                    {
                        if(SettingData.IsDebug) Debug.LogError($"[CHECK] 다운로드된 파일이 존재하지 않습니다: {fullPath}");
                        SafeSetUIError(ui, "실패", "파일 없음");
                        yield break;
                    }
                    if(SettingData.IsDebug) Debug.Log($"[CHECK] 파일 크기: {new FileInfo(fullPath).Length} bytes");

                    SafeSetTime(ui, "무결성 검사 중...");
                    yield return null;

                    string actualSHA1 = SafeComputeSHA1(fullPath);
                    if (!string.IsNullOrEmpty(actualSHA1) &&
                        string.Equals(actualSHA1, expectedSHA1, StringComparison.OrdinalIgnoreCase))
                    {
                        SafeSetProgress(ui, 1f, "100%" + (string.IsNullOrEmpty(stepInfo) ? "" : $" {stepInfo}"), "완료 (검증됨)");
                        if(SettingData.IsDebug) Debug.Log("✅ 다운로드 & SHA1 검증 성공");
                        onSuccess?.Invoke();
                    }
                    else
                    {
                        if(SettingData.IsDebug) Debug.LogError($"❌ SHA1 불일치 또는 계산 실패! 예상:{expectedSHA1}, 실제:{actualSHA1}");
                        SafeSetUIError(ui, "검증 실패", "무결성 오류");
                        SafeDeleteFile(fullPath);
                    }
                }
                else
                {
                    // 5xx 간단 리트라이 (최대 2회)
                    for (int i = 0; i < 2 && request.responseCode is >= 500 and < 600; i++)
                    {
                        float backoff = Mathf.Pow(2, i); // 1s, 2s
                        if(SettingData.IsDebug) Debug.LogWarning($"[RETRY] 5xx 감지 → {backoff}초 대기 후 재시도 #{i + 1}");
                        yield return new WaitForSeconds(backoff);

                        using var retry = UnityWebRequest.Get(finalUrl);
                        retry.downloadHandler = new DownloadHandlerFile(fullPath) { removeFileOnAbort = true };
                        retry.redirectLimit = 64;
                        retry.timeout = 0;
#pragma warning disable CS0618
                        retry.chunkedTransfer = false;
#pragma warning restore CS0618
                        retry.SetRequestHeader("Accept", "application/octet-stream");
                        retry.SetRequestHeader("Accept-Encoding", "identity");
                        retry.SetRequestHeader("User-Agent", "ProjectLucia/1.0");

                        yield return retry.SendWebRequest();
                        if(SettingData.IsDebug) Debug.Log($"[RETRY] code={retry.responseCode}, result={retry.result}, err={retry.error}");
                        if (retry.result == UnityWebRequest.Result.Success)
                        {
                            goto SUCCESS_PATH;
                        }
                    }

                    // ❗ 실패 본문 확보 – try/catch 없이 별도 코루틴으로 분리
                    yield return StartCoroutine(ProbeAndLogErrorBody(finalUrl));

                    if(SettingData.IsDebug) Debug.LogError("❌ 다운로드 실패");
                    SafeSetUIError(ui, "실패", "오류 발생");
                    SafeDeleteFile(fullPath);
                    yield break;

SUCCESS_PATH:
                    {
                        if (!File.Exists(fullPath))
                        {
                            if(SettingData.IsDebug) Debug.LogError($"[CHECK] 다운로드된 파일이 존재하지 않습니다: {fullPath}");
                            SafeSetUIError(ui, "실패", "파일 없음");
                            yield break;
                        }
                        if(SettingData.IsDebug) Debug.Log($"[CHECK] 파일 크기: {new FileInfo(fullPath).Length} bytes");

                        SafeSetTime(ui, "무결성 검사 중...");
                        yield return null;

                        string actualSHA1 = SafeComputeSHA1(fullPath);
                        if (!string.IsNullOrEmpty(actualSHA1) &&
                            string.Equals(actualSHA1, expectedSHA1, StringComparison.OrdinalIgnoreCase))
                        {
                            SafeSetProgress(ui, 1f, "100%" + (string.IsNullOrEmpty(stepInfo) ? "" : $" {stepInfo}"), "완료 (검증됨)");
                            if(SettingData.IsDebug) Debug.Log("✅ 다운로드 & SHA1 검증 성공");
                            onSuccess?.Invoke();
                        }
                        else
                        {
                            if(SettingData.IsDebug) Debug.LogError($"❌ SHA1 불일치 또는 계산 실패! 예상:{expectedSHA1}, 실제:{actualSHA1}");
                            SafeSetUIError(ui, "검증 실패", "무결성 오류");
                            SafeDeleteFile(fullPath);
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    if (request != null)
                    {
                        try { request.Abort(); }
                        catch
                        {
                            // ignored
                        }

                        request.Dispose();
                    }
                }
                catch
                {
                    // ignored
                }

                clearRequestRef?.Invoke();
                clearCoroutineRef?.Invoke();
            }
        }

        #endregion

        #region Helper Methods (보조 메서드)

        private IEnumerator RedirectProbeWithRange(string url, Action<long, Dictionary<string, string>, string> onDone)
        {
            using var req = UnityWebRequest.Get(url);
            req.redirectLimit = 0;
            req.timeout = 30;
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Range", "bytes=0-0");
            req.SetRequestHeader("Accept", "application/octet-stream");
            req.SetRequestHeader("Accept-Encoding", "identity");
            req.SetRequestHeader("User-Agent", "ProjectLucia/1.0");

            yield return req.SendWebRequest();

            var headers = req.GetResponseHeaders();
            string location = null;
            if (headers != null) headers.TryGetValue("Location", out location);

            if(SettingData.IsDebug) Debug.Log($"[PROBE] code={req.responseCode}, result={req.result}, err={req.error}, location={location}");
            onDone?.Invoke(req.responseCode, headers, location);
        }

        private IEnumerator ResolveFinalUrl(string url, Action<string> onResolved)
        {
            string candidate = null;

            yield return RedirectProbeWithRange(url, (code, _, location) =>
            {
                if (code is >= 300 and < 400 && !string.IsNullOrEmpty(location))
                {
                    candidate = location;
                }
            });

            if (string.IsNullOrEmpty(candidate))
            {
                candidate = url.Contains("?") ? (url + "&download=1") : (url + "?download=1");
            }

            if(SettingData.IsDebug) Debug.Log($"[RESOLVE] finalUrl={candidate}");
            onResolved?.Invoke(candidate);
        }

        private IEnumerator ProbeAndLogErrorBody(string finalUrl)
        {
            using var probe = UnityWebRequest.Get(finalUrl);
            probe.redirectLimit = 64;
            probe.timeout = 30;
            probe.downloadHandler = new DownloadHandlerBuffer();
            probe.SetRequestHeader("Accept", "text/plain, application/json, */*");
            probe.SetRequestHeader("Accept-Encoding", "identity");
            probe.SetRequestHeader("User-Agent", "ProjectLucia/1.0");

            yield return probe.SendWebRequest();

            if(SettingData.IsDebug) Debug.Log($"[ERR-PROBE] code={probe.responseCode}, result={probe.result}, err={probe.error}");
            var body = probe.downloadHandler?.text;
            if (!string.IsNullOrEmpty(body)) if(SettingData.IsDebug) Debug.Log($"[ERR-BODY]\n{body}");
        }

        private void CancelDownload(bool isWhisper)
        {
            if (isWhisper)
            {
                if (WhisperRequest != null)
                {
                    try { WhisperRequest.Abort(); }
                    catch { /* ignored */ }

                    WhisperRequest.Dispose();
                    WhisperRequest = null;
                }

                if (_whisperCoroutine != null)
                {
                    StopCoroutine(_whisperCoroutine);
                    _whisperCoroutine = null;
                }

                CancelAndReset(WhisperFolder, _currentWhisperFileName, _whisperUI);
            }
            else
            {
                if (VadRequest != null)
                {
                    try { VadRequest.Abort(); }
                    catch { /* ignored */ }

                    VadRequest.Dispose();
                    VadRequest = null;
                }

                if (_vadCoroutine != null)
                {
                    StopCoroutine(_vadCoroutine);
                    _vadCoroutine = null;
                }

                CancelAndReset(VadFolder, _currentVadFileName, _vadUI);
            }
        }

        private void CancelAndReset(string folderName, string fileName, DownloadUI ui)
        {
            string folder = Path.Combine(Application.streamingAssetsPath, folderName);
            string fullPath = Path.Combine(folder, fileName);
            SafeDeleteFile(fullPath);

            SafeSetProgress(ui, 0f, "취소됨", "중단됨");
            SafeSetUIButton(ui, "다운로드");
        }

        private static void EnsureDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static void SafeDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                if(SettingData.IsDebug) Debug.LogWarning($"파일 삭제 실패: {path}\n{e}");
            }
        }

        public static string SafeComputeSHA1(string path)
        {
            try
            {
                using var sha1 = SHA1.Create();
                using var stream = File.OpenRead(path);
                var hash = sha1.ComputeHash(stream);
                var sb = new StringBuilder();
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
            catch (Exception e)
            {
                if(SettingData.IsDebug) Debug.LogError($"SHA1 계산 실패: {path}\n{e}");
                return null;
            }
        }

        private void CleanupAll()
        {
            if (WhisperRequest != null)
            {
                try { WhisperRequest.Abort(); }
                catch { /* ignored */ }

                WhisperRequest.Dispose();
                WhisperRequest = null;
            }
            if (_whisperCoroutine != null)
            {
                StopCoroutine(_whisperCoroutine);
                _whisperCoroutine = null;
            }

            if (VadRequest != null)
            {
                try { VadRequest.Abort(); }
                catch { /* ignored */ }

                VadRequest.Dispose();
                VadRequest = null;
            }
            if (_vadCoroutine != null)
            {
                StopCoroutine(_vadCoroutine);
                _vadCoroutine = null;
            }
        }

        #endregion

        #region UI Helper Methods (UI 보조 메서드)

        private static void SafeActivate(Transform t, bool active)
        {
            if (t == null) return;
            if (t.gameObject != null) t.gameObject.SetActive(active);
        }

        private static void SafeSetUIButton(DownloadUI ui, string text)
        {
            if (ui.ButtonText != null) ui.ButtonText.text = text;
        }

        private static void SafeSetProgress(DownloadUI ui, float progress, string percent, string timeText)
        {
            if (ui.ProgressBar != null) ui.ProgressBar.value = Mathf.Clamp01(progress);
            if (ui.PercentText != null) ui.PercentText.text = percent;
            if (ui.TimeRemainingText != null) ui.TimeRemainingText.text = timeText;
        }

        private static void SafeSetUIError(DownloadUI ui, string percentLabel, string timeLabel)
        {
            if (ui.PercentText != null) ui.PercentText.text = percentLabel;
            if (ui.TimeRemainingText != null) ui.TimeRemainingText.text = timeLabel;
        }

        private static void SafeSetTime(DownloadUI ui, string timeText)
        {
            if (ui.TimeRemainingText != null) ui.TimeRemainingText.text = timeText;
        }

        #endregion
    }
}
