using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ProjectLucia.Status;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
// ReSharper disable EmptyGeneralCatchClause

namespace ProjectLucia.Server
{
    /// <summary>
    /// 키워드 추출 모델(ONNX) 및 관련 파일(Vocab)을 다운로드하고 관리하는 클래스입니다.
    /// 다운로드 진행 상황을 UI에 표시하고, 파일 무결성(SHA1/SHA256)을 검증합니다.
    /// </summary>
    public class KeywordModelDownloader : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Header("UI References (UI 참조)")]
        [Tooltip("키워드 모델 다운로드 화면 패널")]
        [SerializeField] private GameObject keywordDownloadScreen;
        public GameObject KeywordDownloadScreen => keywordDownloadScreen;
            
        [Header("File & Folder Settings (파일 및 폴더 설정)")]
        [Tooltip("최종 저장될 키워드 모델 파일명")]
        public string keywordFileName  = "keyword_model.onnx"; 

        [Tooltip("StreamingAssets 하위의 저장 폴더명 (대소문자 구분)")]
        public string keywordFolder    = "Keyword";                

        [Tooltip("Vocab 파일명")]
        public string keywordVocab = "vocab.txt";

        #endregion

        #region Private Fields (비공개 필드)

        // 진행 코루틴 & 현재 요청
        public UnityWebRequest KeywordRequest;
        private Coroutine _keywordCoroutine;
        private string _currentKeywordFileName;

        // UI 구조체
        private struct DownloadUI
        {
            public Slider   ProgressBar;
            public TMP_Text PercentText;
            public TMP_Text TimeRemainingText;
            public TMP_Text ButtonText;
            public Transform StatusRoot;
        }
        private DownloadUI _ui;

        // UI 업데이트 간격 (최적화)
        private const float UiUpdateInterval = 0.1f;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        private void Awake()
        {
            if (keywordDownloadScreen == null)
            {
                if(SettingData.IsDebug) Debug.LogWarning("[KeywordModelDownloader] keywordDownloadScreen 연결 필요");
                return;
            }

            var t = keywordDownloadScreen.transform;

            // UI 컴포넌트 찾기 (경로 주의)
            _ui.StatusRoot        = t.Find("KeywordDownLoadStatus");                    
            _ui.ProgressBar       = t.Find("KeywordDownLoadStatus/DownLoadSlider")?.GetComponent<Slider>();
            _ui.PercentText       = t.Find("KeywordDownLoadStatus/DownLoadTextPercent")?.GetComponent<TMP_Text>();
            _ui.TimeRemainingText = t.Find("KeywordDownLoadStatus/DownLoadTime")?.GetComponent<TMP_Text>();
            _ui.ButtonText        = t.Find("KeywordDownLoadButton/DownLoadButtonText")?.GetComponent<TMP_Text>();

            // 초기엔 상태 UI 숨김
            SafeActivate(_ui.StatusRoot, false);
        }

        private void OnDisable() => CleanupAll();
        
        private void OnDestroy()
        {
            CleanupAll();
            Resources.UnloadUnusedAssets();
        }

        #endregion

        #region Public Methods (공개 메서드)

        /// <summary>
        /// 키워드 모델 다운로드 버튼 클릭 시 호출됩니다.
        /// 다운로드를 시작하거나, 이미 진행 중이면 취소합니다.
        /// </summary>
        public void OnKeywordDownloadButtonClicked()
        {
            if (_keywordCoroutine == null)
            {
                _keywordCoroutine = StartCoroutine(DownloadFullSequence());
            }
            else
            {
                CancelDownload();
            }
        }

        /// <summary>
        /// 키워드 모델 파일들의 존재 여부와 무결성을 검사합니다.
        /// </summary>
        /// <returns>모든 파일이 유효하면 true</returns>
        public bool CheckKeywordModelFile()
        {
            string folderPath = Path.Combine(Application.streamingAssetsPath, keywordFolder);
            
            // 1. Keyword Model Check
            string modelPath = Path.Combine(folderPath, keywordFileName);
            if (!CheckFile(modelPath, DownloadSha1.KeywordExpectedSHA1))
            {
                SettingData.IsExistKeyword = false;
                return false;
            }

            // 2. Vocab Check
            string vocabPath = Path.Combine(folderPath, keywordVocab);
            if (!CheckFile(vocabPath, DownloadSha1.KeywordVocabSah1))
            {
                SettingData.IsExistKeyword = false;
                return false;
            }

            SettingData.IsExistKeyword = true;
            if(SettingData.IsDebug) Debug.Log($"[KeywordModelDownloader] 모든 키워드 모델 파일 유효성 검증 통과");
            return true;
        }

        /// <summary>
        /// 진행 중인 다운로드를 취소하고 임시 파일을 정리합니다.
        /// </summary>
        public void CancelDownload()
        {
            CleanupRequest();

            if (_keywordCoroutine != null)
            {
                StopCoroutine(_keywordCoroutine);
                _keywordCoroutine = null;
            }

            string folderPath = Path.Combine(Application.streamingAssetsPath, keywordFolder);
            string fullPath = Path.Combine(folderPath, _currentKeywordFileName);
            SafeDeleteFile(fullPath);

            SafeSetProgress(_ui, 0f, "취소됨", "중단됨");
            SafeSetUIButton(_ui, "다운로드");
        }

        #endregion

        #region Download Logic (다운로드 로직)

        private IEnumerator DownloadFullSequence()
        {
            bool success = false;

            // 1. Keyword Model 다운로드
            yield return StartCoroutine(DownloadFileProcess(
                DownloadUrl.KeywordUrl,
                keywordFileName,
                keywordFolder,
                DownloadSha1.KeywordExpectedSHA1,
                (res) => success = res
            ));

            if (!success)
            {
                _keywordCoroutine = null;
                yield break;
            }

            // 2. Vocab 다운로드
            yield return StartCoroutine(DownloadFileProcess(
                DownloadUrl.VocabUrl,
                keywordVocab,
                keywordFolder,
                DownloadSha1.KeywordVocabSah1,
                (res) => success = res
            ));

            if (success)
            {
                SettingData.IsExistKeyword = true;
                if (keywordDownloadScreen != null) keywordDownloadScreen.SetActive(false);
            }
            else
            {
                SettingData.IsExistKeyword = false;
            }

            _keywordCoroutine = null;
        }

        private IEnumerator RedirectProbeWithRange(string url, Action<long, string> onDone)
        {
            using var req = UnityWebRequest.Get(url);
            req.redirectLimit = 0;
            req.timeout = 30;
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Range", "bytes=0-0");
            req.SetRequestHeader("Accept", "application/octet-stream");
            req.SetRequestHeader("Accept-Encoding", "identity");
            req.SetRequestHeader("User-Agent", "ProjectLucia/KeywordDL/1.0");

            yield return req.SendWebRequest();

            string location = null;
            var headers = req.GetResponseHeaders();
            if (headers != null && headers.TryGetValue("Location", out var loc))
                location = loc;

            onDone?.Invoke(req.responseCode, location);
        }

        private IEnumerator ResolveFinalUrl(string url, Action<string> onResolved)
        {
            string candidate = null;
            long responseCode = 0;

            yield return RedirectProbeWithRange(url, (code, location) =>
            {
                responseCode = code;
                if (code is >= 300 and < 400 && !string.IsNullOrEmpty(location))
                {
                    if (!location.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            Uri baseUri = new Uri(url);
                            Uri resolvedUri = new Uri(baseUri, location);
                            candidate = resolvedUri.AbsoluteUri;
                        }
                        catch (Exception e)
                        {
                            if(SettingData.IsDebug) Debug.LogWarning($"[KeywordModelDownloader] 리다이렉트 URL 변환 실패: {e.Message}");
                            candidate = location;
                        }
                    }
                    else
                    {
                        candidate = location;
                    }
                }
            });

            if (!string.IsNullOrEmpty(candidate))
            {
                onResolved?.Invoke(candidate);
            }
            else
            {
                if (responseCode is >= 200 and < 300)
                {
                    onResolved?.Invoke(url);
                }
                else
                {
                    string modified = url.Contains("?") ? (url + "&download=1") : (url + "?download=1");
                    onResolved?.Invoke(modified);
                }
            }
        }

        private IEnumerator DownloadFileProcess(string url, string fileName, string folderName, string expectedHash, Action<bool> onResult)
        {
            _currentKeywordFileName = fileName;
            SafeActivate(_ui.StatusRoot, true);
            SafeSetUIButton(_ui, "중지");
            SafeSetProgress(_ui, 0f, "0.0%", $"{fileName} 준비 중...");

            float startTime = Time.time;
            string folderPath = Path.Combine(Application.streamingAssetsPath, folderName);
            EnsureDirectory(folderPath);
            string fullPath = Path.Combine(folderPath, fileName);

            string finalUrl = null;

            yield return ResolveFinalUrl(url, u => finalUrl = u);

            bool ok = false;

            yield return StartCoroutine(SendDownload(finalUrl, fullPath, startTime,
                onProgress: (p, remainText) =>
                {
                    SafeSetProgress(_ui, p, (p * 100f).ToString("F1") + "%", remainText);
                },
                onDone: success => ok = success
            ));

            if (!ok && KeywordRequest is { responseCode: >= 500 and < 600 })
            {
                for (int i = 0; i < 2 && !ok; i++)
                {
                    float backoff = Mathf.Pow(2, i); 
                    yield return new WaitForSeconds(backoff);

                    yield return StartCoroutine(SendDownload(finalUrl, fullPath, Time.time,
                        onProgress: (p, remainText) =>
                        {
                            SafeSetProgress(_ui, p, (p * 100f).ToString("F1") + "%", remainText);
                        },
                        onDone: success => ok = success
                    ));
                }
            }

            SafeSetUIButton(_ui, "다운로드");

            if (ok)
            {
                if (!File.Exists(fullPath))
                {
                    if(SettingData.IsDebug) Debug.LogError("[KeywordModelDownloader] 파일이 존재하지 않습니다.");
                    SafeSetUIError(_ui, "실패", "파일 없음");
                    onResult?.Invoke(false);
                }
                else
                {
                    SafeSetTime(_ui, "무결성 검사 중...");
                    yield return null; 

                    string actualHash = SafeComputeHash(fullPath, expectedHash);
                    if (!string.IsNullOrEmpty(actualHash) &&
                        string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    {
                        SafeSetProgress(_ui, 1f, "100%", "완료 (검증됨)");
                        if(SettingData.IsDebug) Debug.Log($"✅ {fileName} 다운로드 & Hash 검증 성공");
                        onResult?.Invoke(true);
                    }
                    else
                    {
                        if(SettingData.IsDebug) Debug.LogError($"❌ Hash 불일치. 예상:{expectedHash}, 실제:{actualHash}");
                        SafeSetUIError(_ui, "검증 실패", "무결성 오류");
                        SafeDeleteFile(fullPath);
                        onResult?.Invoke(false);
                    }
                }
            }
            else
            {
                if(SettingData.IsDebug) Debug.LogError($"❌ {fileName} 다운로드 실패");
                SafeSetUIError(_ui, "실패", "오류 발생");
                SafeDeleteFile(fullPath);
                onResult?.Invoke(false);
            }

            CleanupRequest();
        }

        private IEnumerator SendDownload(string finalUrl, string fullPath, float startTime,
            Action<float, string> onProgress, Action<bool> onDone)
        {
            bool success = false;

            CleanupRequest();

            var req = UnityWebRequest.Get(finalUrl);
            KeywordRequest = req; 

            var dh = new DownloadHandlerFile(fullPath) { removeFileOnAbort = true };
            req.downloadHandler = dh;

            req.redirectLimit = 64;
            req.timeout = 0;
#pragma warning disable CS0618
            req.chunkedTransfer = false;
#pragma warning restore CS0618
            req.SetRequestHeader("Accept", "application/octet-stream");
            req.SetRequestHeader("Accept-Encoding", "identity");
            req.SetRequestHeader("User-Agent", "ProjectLucia/KeywordDL/1.0");

            var op = req.SendWebRequest();

            float lastUiUpdateTime = 0f;

            while (!op.isDone)
            {
                if (KeywordRequest == null)
                {
                    yield break;
                }

                if (Time.time - lastUiUpdateTime >= UiUpdateInterval)
                {
                    float p = Mathf.Clamp01(req.downloadProgress);
                    float elapsed = Time.time - startTime;
                    float total = p > 0f ? (elapsed / p) : 0f;
                    float remain = Mathf.Max(0f, total - elapsed);
                    onProgress?.Invoke(p, $"{remain:F1}초 남음");
                    lastUiUpdateTime = Time.time;
                }
                yield return null;
            }

            if (KeywordRequest == null)
            {
                onDone?.Invoke(false);
                yield break;
            }

            if (req.result == UnityWebRequest.Result.Success)
            {
                success = true;
            }
            else
            {
                if(SettingData.IsDebug) Debug.LogError($"[KeywordModelDownloader] Download Error: {req.error} (Code: {req.responseCode}) URL: {finalUrl}");
            }

            onDone?.Invoke(success);
        }

        #endregion

        #region Helper Methods (보조 메서드)

        private bool CheckFile(string path, string expectedHash)
        {
            if (!File.Exists(path))
            {
                if(SettingData.IsDebug) Debug.LogWarning($"[KeywordModelDownloader] 파일이 존재하지 않습니다: {path}");
                return false;
            }

            string actual = SafeComputeHash(path, expectedHash);
            if (string.IsNullOrEmpty(actual) || !string.Equals(actual, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                if(SettingData.IsDebug) Debug.LogWarning($"[KeywordModelDownloader] Hash 불일치 ({Path.GetFileName(path)}): 예상={expectedHash}, 실제={actual}");
                return false;
            }
            return true;
        }

        private void CleanupRequest()
        {
            if (KeywordRequest != null)
            {
                try { KeywordRequest.Abort(); } catch { }
                try { KeywordRequest.Dispose(); } catch { }
                KeywordRequest = null;
            }
        }

        private void CleanupAll()
        {
            CleanupRequest();

            if (_keywordCoroutine != null)
            {
                StopCoroutine(_keywordCoroutine);
                _keywordCoroutine = null;
            }
        }

        private static void EnsureDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        private static void SafeDeleteFile(string path)
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

        private static string SafeComputeHash(string path, string expectedHash)
        {
            try
            {
                using var stream = File.OpenRead(path);
                HashAlgorithm algo;

                if (!string.IsNullOrEmpty(expectedHash) && expectedHash.Length == 64)
                    algo = SHA256.Create();
                else
                    algo = SHA1.Create();

                using (algo)
                {
                    var hash = algo.ComputeHash(stream);
                    var sb = new StringBuilder();
                    foreach (var b in hash) sb.Append(b.ToString("x2"));
                    return sb.ToString();
                }
            }
            catch (Exception e)
            {
                if(SettingData.IsDebug) Debug.LogError($"Hash 계산 실패: {path}\n{e}");
                return null;
            }
        }

        // UI Helper Methods
        private static void SafeActivate(Transform t, bool active)
        {
            if (t == null) return;
            var go = t.gameObject;
            if (go != null && go.activeSelf != active) go.SetActive(active);
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
