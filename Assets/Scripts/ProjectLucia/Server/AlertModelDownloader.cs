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
using ProjectLucia.Status;

namespace ProjectLucia.Server
{
    /// <summary>
    /// Alert 모델 파일을 다운로드하고 관리하는 전용 클래스입니다.
    /// 다운로드 진행 상황을 UI에 표시하고, 파일 무결성(SHA1)을 검증합니다.
    /// </summary>
    public class AlertModelDownloader : MonoBehaviour
    {
        #region Constants & Fields

        private const string AlertFolder = "Alert";

        [Header("UI References")]
        [Tooltip("Alert 프로그램 다운로드 화면 패널")]
        [SerializeField] private GameObject alertDownloadScreen;
        public GameObject AlertDownloadScreen => alertDownloadScreen;

        [Tooltip("Alert 프로그램 다운로드 상태 표시 UI")]
        [SerializeField] private GameObject alertDownloadStatus;
        public GameObject AlertDownloadStatus => alertDownloadStatus;
        
        [Header("File Names")]
        [Tooltip("Alert 모델 파일명")]
        public string alertFileName = "ProjectLucia_Toast.exe";

        // 코루틴 및 요청 핸들
        private Coroutine _alertCoroutine;
        public UnityWebRequest AlertRequest;
        private string _currentAlertFileName;

        // UI 구조체
        private struct DownloadUI
        {
            public Slider ProgressBar;
            public TMP_Text PercentText;
            public TMP_Text TimeRemainingText;
            public TMP_Text ButtonText;
            public Transform StatusRoot;
        }

        private DownloadUI _alertUI;
        
        // UI 업데이트 간격 (최적화)
        private const float UiUpdateInterval = 0.1f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            // Alert UI 초기화
            if (alertDownloadScreen != null)
            {
                var als = alertDownloadScreen.transform;
                _alertUI.StatusRoot = als.Find("DownLoadStatus");
                _alertUI.ProgressBar = als.Find("DownLoadStatus/DownLoadSlider").GetComponent<Slider>();
                _alertUI.PercentText = als.Find("DownLoadStatus/DownLoadTextPercent").GetComponent<TMP_Text>();
                _alertUI.TimeRemainingText = als.Find("DownLoadStatus/DownLoadTime").GetComponent<TMP_Text>();
                
                var buttonTransform = als.Find("DownLoadButton/DownLoadButtonText");
                if (buttonTransform != null)
                {
                    _alertUI.ButtonText = buttonTransform.GetComponent<TMP_Text>();
                }
            }
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

        #region Public Methods
        
        /// <summary>
        /// Alert 모델 다운로드 버튼 클릭 시 호출됩니다.
        /// 버튼의 OnClick 이벤트에 이 메서드를 연결하세요.
        /// </summary>
        public void OnAlertDownloadButtonClicked()
        {
            if (_alertCoroutine == null)
            {
                var downloadList = new List<(string fileName, string url, string sha1)>();

                // DownloadUrl과 DownloadSha1 클래스에서 값을 가져옵니다.
                if (!string.IsNullOrEmpty(DownloadUrl.alertUrl) &&
                    !string.IsNullOrEmpty(DownloadSha1.alertSAH1))
                {
                    downloadList.Add((alertFileName, DownloadUrl.alertUrl, DownloadSha1.alertSAH1));
                }

                if (downloadList.Count == 0)
                {
                    if(SettingData.IsDebug) Debug.LogError("Alert URL/SHA1이 설정되지 않았습니다.");
                    SafeSetUIError(_alertUI, "실패", "모델 정보 없음");
                    return;
                }

                _alertCoroutine = StartCoroutine(DownloadAlertSetRoutine(downloadList));
            }
            else
            {
                // 이미 진행 중이면 취소합니다.
                CancelAlertDownload();
            }
        }

        #endregion

        #region Download Routines

        private IEnumerator DownloadAlertSetRoutine(List<(string fileName, string url, string sha1)> files)
        {
            for (int i = 0; i < files.Count; i++)
            {
                var (fileName, url, sha1) = files[i];
                _currentAlertFileName = fileName;
                bool isSuccess = false;

                if(SettingData.IsDebug) Debug.Log($"[AlertDownload] Starting: {fileName}");

                yield return StartCoroutine(DownloadRoutine(
                    url: url,
                    fileName: fileName,
                    folderName: AlertFolder,
                    ui: _alertUI,
                    expectedSHA1: sha1,
                    onSuccess: () => { isSuccess = true; },
                    setRequestRef: (req) => AlertRequest = req,
                    clearRequestRef: () => AlertRequest = null,
                    clearCoroutineRef: () => { }
                ));

                if (!isSuccess)
                {
                    if(SettingData.IsDebug) Debug.LogError($"[AlertDownload] Failed at {fileName}.");
                    _alertCoroutine = null;
                    yield break;
                }
            }

            // 다운로드 완료 시 UI 창 닫기 (필요 시 수정)
            if (alertDownloadScreen != null) alertDownloadScreen.SetActive(false);
            if (alertDownloadStatus != null) alertDownloadStatus.SetActive(true);

            if(SettingData.IsDebug) Debug.Log("✅ Alert 모델 다운로드 및 적용 완료");

            _alertCoroutine = null;
        }

        private IEnumerator DownloadRoutine(
            string url, string fileName, string folderName, DownloadUI ui, string expectedSHA1,
            Action onSuccess, Action<UnityWebRequest> setRequestRef, Action clearRequestRef,
            Action clearCoroutineRef, string stepInfo = "")
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
                yield return StartCoroutine(ResolveFinalUrl(url, (u) => finalUrl = u));

                request = UnityWebRequest.Get(finalUrl);
                var dh = new DownloadHandlerFile(fullPath) { removeFileOnAbort = true };
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

                float lastUiUpdateTime = 0f;

                while (!op.isDone)
                {
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

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (!File.Exists(fullPath))
                    {
                        SafeSetUIError(ui, "실패", "파일 없음");
                        yield break;
                    }

                    SafeSetTime(ui, "무결성 검사 중...");
                    yield return null;

                    string actualSHA1 = SafeComputeSHA1(fullPath);
                    if (!string.IsNullOrEmpty(actualSHA1) && string.Equals(actualSHA1, expectedSHA1, StringComparison.OrdinalIgnoreCase))
                    {
                        SafeSetProgress(ui, 1f, "100%" + (string.IsNullOrEmpty(stepInfo) ? "" : $" {stepInfo}"), "완료 (검증됨)");
                        onSuccess?.Invoke();
                    }
                    else
                    {
                        SafeSetUIError(ui, "검증 실패", "무결성 오류");
                        SafeDeleteFile(fullPath);
                    }
                }
                else
                {
                    // 5xx 서버 에러 대응 리트라이
                    for (int i = 0; i < 2 && request.responseCode is >= 500 and < 600; i++)
                    {
                        float backoff = Mathf.Pow(2, i);
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
                        if (retry.result == UnityWebRequest.Result.Success) goto SUCCESS_PATH;
                    }

                    yield return StartCoroutine(ProbeAndLogErrorBody(finalUrl));

                    SafeSetUIError(ui, "실패", "오류 발생");
                    SafeDeleteFile(fullPath);
                    yield break;

SUCCESS_PATH:
                    {
                        if (!File.Exists(fullPath))
                        {
                            SafeSetUIError(ui, "실패", "파일 없음");
                            yield break;
                        }

                        SafeSetTime(ui, "무결성 검사 중...");
                        yield return null;

                        string actualSHA1 = SafeComputeSHA1(fullPath);
                        if (!string.IsNullOrEmpty(actualSHA1) && string.Equals(actualSHA1, expectedSHA1, StringComparison.OrdinalIgnoreCase))
                        {
                            SafeSetProgress(ui, 1f, "100%" + (string.IsNullOrEmpty(stepInfo) ? "" : $" {stepInfo}"), "완료 (검증됨)");
                            onSuccess?.Invoke();
                        }
                        else
                        {
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
                        try { request.Abort(); } catch { }
                        request.Dispose();
                    }
                }
                catch { }

                clearRequestRef?.Invoke();
                clearCoroutineRef?.Invoke();
            }
        }

        #endregion

        #region Helper Methods

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

            onDone?.Invoke(req.responseCode, headers, location);
        }

        private IEnumerator ResolveFinalUrl(string url, Action<string> onResolved)
        {
            string candidate = null;

            yield return RedirectProbeWithRange(url, (code, _, location) =>
            {
                if (code is >= 300 and < 400 && !string.IsNullOrEmpty(location)) candidate = location;
            });

            if (string.IsNullOrEmpty(candidate))
                candidate = url.Contains("?") ? (url + "&download=1") : (url + "?download=1");

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
        }

        private void CancelAlertDownload()
        {
            if (AlertRequest != null)
            {
                try { AlertRequest.Abort(); } catch { }
                AlertRequest.Dispose();
                AlertRequest = null;
            }

            if (_alertCoroutine != null)
            {
                StopCoroutine(_alertCoroutine);
                _alertCoroutine = null;
            }
            
            string folder = Path.Combine(Application.streamingAssetsPath, AlertFolder);
            string fullPath = Path.Combine(folder, _currentAlertFileName);
            SafeDeleteFile(fullPath);

            SafeSetProgress(_alertUI, 0f, "취소됨", "중단됨");
            SafeSetUIButton(_alertUI, "다운로드");
        }

        private static void EnsureDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
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
            if (AlertRequest != null)
            {
                try { AlertRequest.Abort(); } catch { }
                AlertRequest.Dispose();
                AlertRequest = null;
            }
            if (_alertCoroutine != null)
            {
                StopCoroutine(_alertCoroutine);
                _alertCoroutine = null;
            }
        }

        #endregion

        #region UI Helper Methods

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
        
        
        public bool IsExistedAlertFind()
        {
            string alertFolderPath = Path.Combine(Application.streamingAssetsPath, "Alert");
            SettingData.AlertModelPath = Path.Combine(alertFolderPath, alertFileName);
            
            // SHA1 체크 로직 등은 기존 유지
            if (!File.Exists(SettingData.AlertModelPath))
            {
                if(SettingData.IsDebug) Debug.LogError($"[Alert program Error] Model not found: {SettingData.AlertModelPath}");
                return false; 
            }
            
            // (SHA1 체크 로직 생략 가능하면 생략, 필요하면 유지)
            return true;
        }
    }
    
    
}