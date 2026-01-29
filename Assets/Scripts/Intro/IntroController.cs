using System;
using System.Collections;
using System.Collections.Generic;
using ProjectLucia.Server;
using ProjectLucia.Status;
using ProjectLucia.ThirdParty.Whisper.Runtime;
using ProjectLucia.ThirdParty.Whisper.Runtime.Utils;
using ProjectLucia.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video; 

namespace Intro
{
    public class IntroController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private MicrophoneRecord microphoneRecord;
        [SerializeField] private WhisperManager whisperManager;
        [SerializeField] private KeywordModelDownloader keywordModelDownloader;
        [SerializeField] private TransparentApp transparentApp;

        [SerializeField] private MySQLManager mySqlManager;
        [SerializeField] private ServerClient serverClient;

        [Header("UI & Video")]
        [SerializeField] private TMP_Text loadingText;
        [SerializeField] private Image introCharacter; // Image 타입인 경우 (GameObject 아님)
        [SerializeField] private TMP_Text introTitle;  // TMP_Text 타입인 경우
        [SerializeField] private Image loadingImage;
        [SerializeField] private List<Sprite> loadingSprite;

        [Header("Character Emotion")] 
        [SerializeField] private Sprite characterIdle;
        [SerializeField] private Sprite characterSad;
        [SerializeField] private Sprite characterAngry;
        [SerializeField] private Sprite characterHappy;
        [SerializeField] private Sprite characterSurprise;

        [Space(10)]
        [SerializeField] private VideoPlayer introVideoPlayer; 
        [SerializeField] private VideoClip startClip; 
        [SerializeField] private VideoClip loopClip;  
        [SerializeField] private VideoClip endClip;   

        private void Awake()
        {
            // 시작하자마자 일단 다 숨김 (오브젝트 먼저 보이는 현상 방지)
            if(loadingText != null) loadingText.gameObject.SetActive(false);
            if(introCharacter != null) introCharacter.gameObject.SetActive(false);
            if(introTitle != null) introTitle.gameObject.SetActive(false);
            if(loadingImage != null) loadingImage.gameObject.SetActive(false);
        }

        private void Update()
        {
            transparentApp.Toggle = false;
            transparentApp.SetWindows();
        }

        private void Start()
        {
            if (introVideoPlayer == null) introVideoPlayer = GetComponent<VideoPlayer>();
            
            // VideoPlayer 설정: 첫 프레임 준비될 때까지 대기 옵션 켜기
            introVideoPlayer.waitForFirstFrame = true;

            StartCoroutine(CheckStart());
            StartCoroutine(LodingIcon());
        }

        private IEnumerator CheckStart()
        {
            // ==========================================
            // [STEP 1] 오프닝 영상 (Intro Start) 재생
            // ==========================================
            loadingText.text = "프로젝트 루시아 - 히요리 에디션을 시작합니다.";

            if (startClip != null)
            {
                introVideoPlayer.clip = startClip;
                introVideoPlayer.isLooping = false;
                
                // 1. 비디오 준비 요청 (메모리에 올림)
                introVideoPlayer.Prepare();

                // 2. 비디오가 준비될 때까지 대기 (이게 핵심!)
                while (!introVideoPlayer.isPrepared)
                {
                    yield return null;
                }

                // 3. 재생 시작
                introVideoPlayer.Play();

                // 4. 비디오가 실제로 시작됨과 동시에 UI 켜기
                if(loadingText != null) loadingText.gameObject.SetActive(true);
                if(introCharacter != null) introCharacter.gameObject.SetActive(true);
                if(introTitle != null) introTitle.gameObject.SetActive(true);
                if(loadingImage != null) loadingImage.gameObject.SetActive(true);

                // 영상 길이만큼 대기
                yield return new WaitForSeconds((float)startClip.length);
            }
            else
            {
                // 만약 영상이 없다면 바로 UI 켜줘야 함 (안 그러면 영원히 안 보임)
                if(loadingText != null) loadingText.gameObject.SetActive(true);
                // ... 나머지 UI 켜기 ...
            }

            // ==========================================
            // [STEP 2] 로딩 루프 영상 (Intro Loop) 재생 및 로딩 시작
            // ==========================================
            if (loopClip != null)
            {
                introVideoPlayer.clip = loopClip;
                introVideoPlayer.isLooping = true; 
                introVideoPlayer.Play();
            }

            // --- 로딩 프로세스 시작 ---

            // 1. 데이터 불러오기
            loadingText.text = "사용자 설정 불러오고 있습니다.";
            SaveController.GetPlayerPref();
            yield return new WaitForSeconds(0.5f);

            // 2. 네트워크 상태 확인 (SQL)
            loadingText.text = "SQL 서버 상태를 확인하고 있습니다.";
            mySqlManager.ConnectToDatabase(true);
            if(SettingData.IsDebug) Debug.Log($"sql 접속여부 인트로 체크 : {SettingData.IsIntroSql}");
            yield return new WaitForSeconds(0.5f);

            // 3. LLM 서버 헬스체크
            loadingText.text = "LLM 서버 상태를 확인하고 있습니다.";
            serverClient.BuildUrlsFromSettingData();
            yield return StartCoroutine(IntroHealthCheck());
            if(SettingData.IsDebug) Debug.Log($"서버 접속여부 인트로 체크 : {SettingData.IsIntroServer}");
            yield return new WaitForSeconds(0.5f);

            // 4. VAD 조회
            loadingText.text = "VAD 파일을 확인하고 있습니다.";
            SettingData.IsExistedVad = microphoneRecord.IsExistedVadFind();
            yield return new WaitForSeconds(0.5f);

            // 5. Whisper 조회
            loadingText.text = "Whisper 파일을 확인하고 있습니다.";
            string selectedFile = whisperManager.WhisperModelList[SettingData.SetWhisperModel];
            string modelPathWhisper = $"Whisper/{selectedFile}";
            whisperManager.CheckWhisperModel(SettingData.SetWhisperModel, selectedFile, modelPathWhisper);
            yield return new WaitForSeconds(0.5f);

            // 6. Keyword 조회
            loadingText.text = "Keyword 파일을 확인하고 있습니다.";
            SettingData.IsExistKeyword = keywordModelDownloader.CheckKeywordModelFile();
            yield return new WaitForSeconds(0.5f);

            // --- 로딩 결과 판별 ---
            string resultText = null;

            if (!SettingData.IsIntroSql || !SettingData.IsIntroServer || !SettingData.IsExistedVad ||
                !SettingData.IsExistedWhisper || !SettingData.IsExistKeyword)
            {
                if (!SettingData.IsIntroSql || !SettingData.IsIntroServer)
                    resultText += "서버, ";
                if (!SettingData.IsExistedVad || !SettingData.IsExistedWhisper || !SettingData.IsExistKeyword)
                    resultText += "모델 파일, ";
                resultText += "에서 오류가 발생했습니다.\n설정에서 점검 해 주십시오.";

                loadingText.text = resultText.Replace(", 에", " 에");
                introCharacter.sprite = characterSurprise;
            }
            else
            {
                loadingText.text = "로딩이 완료되었습니다.";
                introCharacter.sprite = characterHappy;
            }

            // ==========================================
            // [STEP 3] 엔딩 영상 (Intro End) 재생
            // ==========================================
            yield return new WaitForSeconds(2.0f); 

            if (endClip != null)
            {
                introVideoPlayer.clip = endClip;
                introVideoPlayer.isLooping = false; 
                introVideoPlayer.Play();

                yield return new WaitForSeconds((float)endClip.length);
            }
            else
            {
                yield return new WaitForSeconds(3.0f);
            }

            SceneManager.LoadSceneAsync("MainScene");
        }

        private IEnumerator IntroHealthCheck()
        {
            string baseAudio = $"http://{SettingData.ServerIP}:{SettingData.ServerPort}/audio/";
            string healthUrl = baseAudio.Replace("/audio/", "/health");

            using (var req = UnityWebRequest.Get(healthUrl))
            {
                req.timeout = 3;
                yield return req.SendWebRequest();

                bool ok = (req.result == UnityWebRequest.Result.Success &&
                           req.responseCode == 200 &&
                           (req.downloadHandler?.text?.Trim() == "ok"));

                SettingData.IsIntroServer = ok;
            }
        }

        private IEnumerator LodingIcon()
        {
            int i = 0;
            while (true)
            {
                if (loadingSprite != null && loadingSprite.Count > 0)
                {
                    if (i >= loadingSprite.Count) i = 0;
                    loadingImage.sprite = loadingSprite[i];
                    i++;
                }
                yield return new WaitForSeconds(0.3f);
            }
        }
    }
}