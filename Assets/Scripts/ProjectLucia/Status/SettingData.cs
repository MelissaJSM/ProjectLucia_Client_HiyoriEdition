using System.Collections.Generic;

namespace ProjectLucia.Status
{
    [System.Serializable]
    public static class SettingData
    {
        #region Live2D Settings (Live2D 설정)
        /// <summary>
        /// Live2D 모델의 크기 값
        /// </summary>
        public static float Live2DSizeValue;

        /// <summary>
        /// 키보드 입력 활성화 여부
        /// </summary>
        public static bool IsInputKeyboard;

        /// <summary>
        /// 시선 추적(Look Target) 활성화 여부
        /// </summary>
        public static bool IsLookTarget;

        /// <summary>
        /// Live2D 모델 클릭 상태
        /// </summary>
        public static bool L2dClicked;

        /// <summary>
        /// 대기 모션(Idle Motion) 활성화 여부
        /// </summary>
        public static bool IsIdleMotion;

        /// <summary>
        /// 대기 모션 랜덤 재생 여부
        /// </summary>
        public static bool IsIdleMotionRandom;

        /// <summary>
        /// 대기 모션 랜덤 재생 최대 시간 간격
        /// </summary>
        public static float IdleMotionRandomMax;

        /// <summary>
        /// 대기 모션 랜덤 재생 최소 시간 간격
        /// </summary>
        public static float IdleMotionRandomMin;

        /// <summary>
        /// 대기 모션 고정 시간 간격
        /// </summary>
        public static float IdleMotionFixed;

        /// <summary>
        /// 말풍선 텍스트 표시 설정 값
        /// </summary>
        public static int BubbleTextValue;
        #endregion

        #region UI Settings (TalkingPanel)
        /// <summary>
        /// 대화창 폰트 크기
        /// </summary>
        public static float TalkingFontSize;
        #endregion

        #region Network & Server Settings (서버 설정)
        public static string ServerIP;
        public static string MySqlIp;
        public static string DatabaseName;
        public static string SqlUserName;
        public static string SqlPassword;
        public static string SqlPort;
        public static string ServerPort;
        #endregion

        #region Intro & Initialization (초기화 및 인트로)
        /// <summary>
        /// 인트로 SQL 연결 확인 여부
        /// </summary>
        public static bool IsIntroSql;

        /// <summary>
        /// 인트로 서버 연결 확인 여부
        /// </summary>
        public static bool IsIntroServer;

        /// <summary>
        /// 시작 모드 여부
        /// </summary>
        public static bool IsStartMode;
        #endregion

        #region Debug Settings (디버그)
        /// <summary>
        /// 디버그 모드 활성화 여부
        /// </summary>
        public static bool IsDebug;
        #endregion

        #region Graphics Settings (그래픽 설정)
        public static int PresetIndex;
        public static int AntiIndex;
        
        // 품질 설정
        public static int IsVsync;
        public static int IsMsaaQuality;
        public static int IsSmaaQuality;
        public static int IsAnisotropicFiltering;
        public static int IsMipMap;
        public static int IsRenderingPath;
        
        // FXAA 및 해상도 설정
        public static bool IsFxaaFastMode;
        public static bool IsFxaaAlphaKeep;
        public static bool IsDynamicResolution;
        
        // 세부 그래픽 옵션
        public static float IsFrameLimit;
        public static float IsJitterSpread;
        public static float IsStaionaryBlending;
        public static float IsMotionBlending;
        public static float IsSharpness;
        
        public static int IsIconFade;
        #endregion

        #region Voice & Audio Settings (음성 설정)
        /// <summary>
        /// Whisper 모델 설정 인덱스
        /// </summary>
        public static int SetWhisperModel;
        
        /// <summary>
        /// 기본 모델 설정 인덱스
        /// </summary>
        public static int DefaultModel;
        
        /// <summary>
        /// 선택된 마이크 장치 이름
        /// </summary>
        public static string MicDevice;
        
        /// <summary>
        /// Whisper 양자화(Quantization) 설정
        /// </summary>
        public static string WhisperQuantization;

        /// <summary>
        /// 음성인식 호출 시스템 활성화
        /// </summary>
        public static bool IsCallNow;

        /// <summary>
        /// 음성인식 호출 시스템 이름 설정
        /// </summary>
        public static string CallName;
        
        #endregion

        #region Emotion Settings (감정 표현)
        /// <summary>
        /// 감정 표현 활성화 여부
        /// </summary>
        public static bool IsEmotion;
        #endregion

        #region System Information (시스템 정보)
        /// <summary>
        /// 저장된 GPU 이름
        /// </summary>
        public static string SavedGPUName;
        
        /// <summary>
        /// 현재 감지된 GPU 이름
        /// </summary>
        public static string ResultGPUName;
        
        /// <summary>
        /// GPU 일치 여부 (저장된 정보와 비교)
        /// </summary>
        public static bool IsMatchedGPU;
        #endregion

        #region Model Status (모델 상태 확인)
        /// <summary>
        /// Whisper 모델 파일 존재 여부
        /// </summary>
        public static bool IsExistedWhisper;
        
        /// <summary>
        /// VAD 모델 파일 존재 여부
        /// </summary>
        public static bool IsExistedVad;
        
        /// <summary>
        /// 키워드 모델 파일 존재 여부
        /// </summary>
        public static bool IsExistKeyword;
        
        /// <summary>
        /// VAD 모델 경로
        /// </summary>
        public static string VadModelPath;
        #endregion

        #region Real Talk Settings (리얼 토크)
        /// <summary>
        /// 리얼 토크 기능 활성화 여부
        /// </summary>
        public static bool IsRealTalk;
        
        /// <summary>
        /// 리얼 토크 픽셀 품질 (기본값: "Low")
        /// </summary>
        public static string RealTalkPixel = "Low";
        #endregion

        #region Desktop Observer Settings (화면 감지)
        // 0.0 ~ 1.0 Normalized Values
        
        /// <summary>
        /// 화면 감지 주기
        /// </summary>
        public static float CheckIntervalValue;
        
        /// <summary>
        /// 변화 감지 임계값
        /// </summary>
        public static float ChangeThresholdValue;
        
        /// <summary>
        /// 안정화 지속 시간
        /// </summary>
        public static float StabilityDurationValue;
        
        /// <summary>
        /// 최소 전송 간격
        /// </summary>
        public static float MinSendIntervalValue;
        #endregion

        #region User Profile (사용자 정보)
        /// <summary>
        /// 사용자 생년월일
        /// </summary>
        public static string UserBirthDate;
        
        /// <summary>
        /// 사용자 성별
        /// </summary>
        public static int UserGender;
        
        /// <summary>
        /// 사용자 이름
        /// </summary>
        public static string UserName;
        #endregion
    }
    
    #region Download Configuration (다운로드 URL 및 설정)
    public static class DownloadUrl
    {
        public static readonly Dictionary<string, string> WhisperUrls = new Dictionary<string, string>()
        {
            { "ggml-tiny.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin" },
            { "ggml-small.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin" },
            { "ggml-base.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin" },
            { "ggml-medium.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin" },
            { "ggml-large-v3.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin" },
            { "ggml-large-v3-turbo.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo.bin"},
        };
        
        public static readonly Dictionary<string, string> WhisperUrlsQ8 = new Dictionary<string, string>()
        {
            { "ggml-tiny-q8.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny-q8_0.bin" },
            { "ggml-small-q8.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q8_0.bin" },
            { "ggml-base-q8.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base-q8_0.bin" },
            { "ggml-medium-q8.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium-q8_0.bin" },
            { "ggml-large-v3-q8.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v2-q8_0.bin" },
            { "ggml-large-v3-turbo-q8.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q8_0.bin"},
        };
        
        public static readonly Dictionary<string, string> WhisperUrlsQ5 = new Dictionary<string, string>()
        {
            { "ggml-tiny-q5.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny-q5_1.bin" },
            { "ggml-small-q5.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q5_1.bin" },
            { "ggml-base-q5.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base-q5_1.bin" },
            { "ggml-medium-q5.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium-q5_0.bin" },
            { "ggml-large-v3-q5.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-q5_0.bin" },
            { "ggml-large-v3-turbo-q5.bin", "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3-turbo-q5_0.bin"},
        };
        
        public static readonly string VadUrl = "https://huggingface.co/deepghs/silero-vad-onnx/resolve/main/silero_vad.onnx";

        public static readonly string VoxcelebUrl =
            "https://huggingface.co/MelissaJ/spkrec-ecapa-voxceleb-onnx/resolve/main/voxceleb.onnx";
        public static readonly string KeywordUrl = "https://huggingface.co/sentence-transformers/distiluse-base-multilingual-cased-v2/resolve/main/onnx/model_quint8_avx2.onnx";
        public static readonly string VocabUrl = "https://huggingface.co/sentence-transformers/distiluse-base-multilingual-cased-v2/resolve/main/vocab.txt";
    }

    public static class DownloadSha1
    {
        public static readonly Dictionary<string, string> WhisperSha1 = new Dictionary<string, string>()
        {
            { "ggml-tiny.bin", "bd577a113a864445d4c299885e0cb97d4ba92b5f" },
            { "ggml-small.bin", "55356645c2b361a969dfd0ef2c5a50d530afd8d5" },
            { "ggml-base.bin", "465707469ff3a37a2b9b8d8f89f2f99de7299dac" },
            { "ggml-medium.bin", "fd9727b6e1217c2f614f9b698455c4ffd82463b4" },
            { "ggml-large-v3.bin", "ad82bf6a9043ceed055076d0fd39f5f186ff8062" },
            { "ggml-large-v3-turbo.bin", "4af2b29d7ec73d781377bfd1758ca957a807e941" },
        };
        public static readonly Dictionary<string, string> WhisperSha1Q8 = new Dictionary<string, string>()
        {
            { "ggml-tiny-q8.bin", "19e8118f6652a650569f5a949d962154e01571d9" },
            { "ggml-small-q8.bin", "bcad8a2083f4e53d648d586b7dbc0cd673d8afad" },
            { "ggml-base-q8.bin", "7bb89bb49ed6955013b166f1b6a6c04584a20fbe" },
            { "ggml-medium-q8.bin", "e66645948aff4bebbec71b3485c576f3d63af5d6" },
            { "ggml-large-v3-q8.bin", "da97d6ca8f8ffbeeb5fd147f79010eeea194ba38" },
            { "ggml-large-v3-turbo-q8.bin", "01bf15bedffe9f39d65c1b6ff9b687ea91f59e0e" },
        };
        public static readonly Dictionary<string, string> WhisperSha1Q5 = new Dictionary<string, string>()
        {
            { "ggml-tiny-q5.bin", "2827a03e495b1ed3048ef28a6a4620537db4ee51" },
            { "ggml-small-q5.bin", "6fe57ddcfdd1c6b07cdcc73aaf620810ce5fc771" },
            { "ggml-base-q5.bin", "a3733eda680ef76256db5fc5dd9de8629e62c5e7" },
            { "ggml-medium-q5.bin", "7718d4c1ec62ca96998f058114db98236937490e" },
            { "ggml-large-v3-q5.bin", "e6e2ed78495d403bef4b7cff42ef4aaadcfea8de" },
            { "ggml-large-v3-turbo-q5.bin", "e050f7970618a659205450ad97eb95a18d69c9ee" },
        };
        public static readonly string VadExpectedSHA1 = "e1e78d47ed5f5577c6d6b64b392a53936c2ca93e";
        public static readonly string VoxcelebSHA1 = "0cc44ed48e5dcd74516554d78962e8f6fd75af4d";
        public static readonly string KeywordExpectedSHA1 = "4aee880a7e1bfeafb6efedf7e3fe2a9a67dd5a8a";
        public static readonly string KeywordVocabSah1 = "fe0fda7c425b48c516fc8f160d594c8022a0808447475c1a7c6d6479763f310c";

    }
    #endregion

    #region Data Structures (데이터 구조)
    public class GpuData
    {
        public int GPUIndex;
        public string GPUName;
    }
    
    public static class VramStatus
    {
        
        public static readonly int SileroVad = 154;
        public static readonly int[] WhisperSize = new int[]
        {
            0, // null
            112, //tiny
            624, //small
            201, //base
            1791, //medium
            0, //large
            1777 //largeTurbo
        };
    }
    #endregion
}
