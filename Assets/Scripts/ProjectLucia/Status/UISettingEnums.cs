using UnityEngine;
// ReSharper disable InconsistentNaming

namespace ProjectLucia.Status
{
    /// <summary>
    /// UI 요소 및 설정 관련 열거형(Enum)들을 관리하는 정적 클래스입니다.
    /// </summary>
    public static class UISettingEnums
    {
        #region UI Components (UI 컴포넌트 인덱스)

        /// <summary>
        /// InputField UI 요소들의 인덱스 정의
        /// </summary>
        public enum InputEnum
        {
            [Tooltip("기본 입력 필드")]
            InputField = 0,
            [Tooltip("서버 IP 입력 필드")]
            ServerIP = 1,
            [Tooltip("서버 포트 입력 필드")]
            ServerPort = 2,
            [Tooltip("MySQL IP 입력 필드")]
            SqlIP = 3,
            [Tooltip("데이터베이스 이름 입력 필드")]
            DatabaseName = 4,
            [Tooltip("MySQL 사용자 이름 입력 필드")]
            SqlUserName = 5,
            [Tooltip("MySQL 비밀번호 입력 필드")]
            SqlPassword = 6,
            [Tooltip("MySQL 포트 입력 필드")]
            SqlPort = 7,
            [Tooltip("피드백 입력 필드")]
            Feedback = 8,
            [Tooltip("기존 비밀번호 입력 필드 (잠금 해제 등)")]
            OldPassword = 9,
            [Tooltip("새 비밀번호 입력 필드")]
            NewPassword = 10,
            [Tooltip("새 비밀번호 확인 입력 필드")]
            NewPasswordCheck = 11,
            [Tooltip("잠금 화면 비밀번호 입력 필드")]
            LockPasswordInput = 12,
            [Tooltip("사용자 이름 입력 필드")]
            UserName = 13,
            [Tooltip("호출 이름 설정 필드")]
            CallName = 14,
        }

        /// <summary>
        /// Dropdown UI 요소들의 인덱스 정의
        /// </summary>
        public enum DropDownEnum
        {
            // 그래픽 관련
            [Tooltip("그래픽 프리셋 선택")]
            PresetDropDown = 0,
            [Tooltip("안티앨리어싱 모드 선택")]
            AntiDropDown = 1,
            [Tooltip("MSAA 품질 선택")]
            MSAAQuality = 2,
            [Tooltip("SMAA 품질 선택")]
            SmaaQuality = 3,
            [Tooltip("이방성 필터링(Anisotropic Filtering) 수준 선택")]
            AnisotropicFiltering = 4,
            [Tooltip("밉맵(MipMap) 설정 선택")]
            MipMap = 5,
            [Tooltip("렌더링 경로(Rendering Path) 선택")]
            RenderingPath = 6,
            [Tooltip("수직 동기화(VSync) 설정 선택")]
            VSync = 7,
            
            // 음성 및 기타
            [Tooltip("Whisper 모델 선택")]
            WhisperModel = 8,
            [Tooltip("마이크 장치 선택")]
            MicDevice = 9,
            [Tooltip("기본 언어 모델 선택")]
            DefaultModel = 10,
            [Tooltip("리얼타임 픽셀 품질 선택")]
            RealtimePixel = 11,
            [Tooltip("아이콘 페이드 효과 설정")]
            IconFade = 12,
            [Tooltip("말풍선 텍스트 표시 설정")]
            BubbleText = 13,
            [Tooltip("사용자 성별 선택")]
            UserGender = 14,
            [Tooltip("Whisper 양자화(Quantization) 설정")]
            WhisperQuantization = 15,
        }

        /// <summary>
        /// Scrollbar UI 요소들의 인덱스 정의
        /// </summary>
        public enum ScrollbarEnum
        {
            [Tooltip("Live2D 모델 크기 조절")]
            Live2dSizeScrollbar = 0,
            
            // 그래픽 세부 설정
            [Tooltip("프레임 제한 설정")]
            FrameLimitScrollbar = 1,
            [Tooltip("Jitter Spread 설정")]
            JitterSpreadScrollbar = 2,
            [Tooltip("Stationary Blending 설정")]
            StaionaryBlendingScrollbar = 3,
            [Tooltip("Motion Blending 설정")]
            MotionBlendingScrollbar = 4,
            [Tooltip("선명도(Sharpness) 설정")]
            SharpnessScrollbar = 5,
            
            // UI 및 동작 설정
            [Tooltip("대화창 폰트 크기 조절")]
            TalkFontScrollbar = 6,
            [Tooltip("대기 모션 랜덤 최대 시간 간격")]
            IdleMotionRandomMax = 7,
            [Tooltip("대기 모션 랜덤 최소 시간 간격")]
            IdleMotionRandomMin = 8,
            [Tooltip("대기 모션 고정 시간 간격")]
            IdleMotionFixed = 9,
            
            // DesktopObserver (화면 감지) 설정
            [Tooltip("화면 감지 주기 설정")]
            CheckIntervalScrollbar = 10,
            [Tooltip("화면 변화 감지 임계값 설정")]
            ChangeThresholdScrollbar = 11,
            [Tooltip("화면 안정화 지속 시간 설정")]
            StabilityDurationScrollbar = 12,
            [Tooltip("최소 전송 간격 설정")]
            MinSendIntervalScrollbar = 13,
            
            [Tooltip("화자 인식 유사도 임계값 설정")]
            SimilarityThresholdScrollbar = 14,
        }

        /// <summary>
        /// Text UI 요소들의 인덱스 정의
        /// </summary>
        public enum TextsEnum
        {
            [Tooltip("응답 텍스트 표시")]
            ResponseText = 0,
            [Tooltip("서버 상태 표시")]
            ServerStatusText = 1,
            
            // 슬라이더 값 표시용 텍스트
            [Tooltip("프레임 제한 값 표시")]
            FrameLimitSidebarText = 2,
            [Tooltip("Jitter Spread 값 표시")]
            JitterSpreadSidebarText = 3,
            [Tooltip("Stationary Blending 값 표시")]
            StationaryBlendingSidebarText = 4,
            [Tooltip("Motion Blending 값 표시")]
            MotionBlendingSidebarText = 5,
            [Tooltip("선명도 값 표시")]
            SharpnessSidebarText = 6,
            
            [Tooltip("MySQL 상태 표시")]
            MySqlStatusText = 7,
            [Tooltip("마우스 좌표 표시")]
            MouseCoordText = 8,
            [Tooltip("히트 테스트 결과 표시")]
            HitText = 9,
            [Tooltip("디버그 로그 표시")]
            DebugText = 10,
            [Tooltip("Live2D 크기 값 표시")]
            L2dSizeText = 11,
            [Tooltip("폰트 크기 값 표시")]
            FontSizeText = 12,
            
            // 대기 모션 설정 값 표시
            [Tooltip("대기 모션 랜덤 최대 시간 값 표시")]
            IdleMotionRandomMax = 13,
            [Tooltip("대기 모션 랜덤 최소 시간 값 표시")]
            IdleMotionRandomMin = 14,
            [Tooltip("대기 모션 고정 시간 값 표시")]
            IdleMotionFixed = 15,
            
            // GPU 정보 표시
            [Tooltip("GPU 확인 제목")]
            GPUCheckSubject = 16,
            [Tooltip("GPU 확인 안내 문구")]
            GPUCheckAnnounce = 17,
            [Tooltip("GPU 확인 버튼 텍스트")]
            GPUCheckButton = 18,
            [Tooltip("감지된 GPU 이름")]
            GPUName = 19,
            [Tooltip("GPU 상태 정보")]
            GPUStatus = 20,
            [Tooltip("VRAM 정보")]
            GPUVram = 21,
            
            // DesktopObserver 설정 값 표시
            [Tooltip("화면 감지 주기 값 표시")]
            CheckIntervalText = 22,
            [Tooltip("변화 감지 임계값 표시")]
            ChangeThresholdText = 23,
            [Tooltip("안정화 지속 시간 값 표시")]
            StabilityDurationText = 24,
            [Tooltip("최소 전송 간격 값 표시")]
            MinSendIntervalText = 25,
            
            [Tooltip("화자 인식 유사도 임계값 표시")]
            SimilarityThresholdText = 26,
        }

        /// <summary>
        /// Toggle UI 요소들의 인덱스 정의
        /// </summary>
        public enum TogglesEnum
        {
            [Tooltip("키보드 입력 활성화")]
            IsKeyboard = 0,
            [Tooltip("디버그 모드 활성화")]
            IsDebug = 1,
            
            // 그래픽 옵션
            [Tooltip("FXAA Fast Mode 활성화")]
            IsFXAAFastMode = 2,
            [Tooltip("FXAA Alpha Keep 활성화")]
            IsFXAAAlphaKeep = 3,
            [Tooltip("동적 해상도(Dynamic Resolution) 활성화")]
            IsDynamicResolution = 4,
            
            // 동작 옵션
            [Tooltip("마우스 추적(시선 처리) 활성화")]
            IsMouseChaser = 5,
            [Tooltip("감정 표현 활성화")]
            IsEmotion = 6,
            [Tooltip("대기 모션 활성화")]
            IsIdleMotion = 7,
            [Tooltip("터치 반응 모션 활성화")]
            IsTouchMotion = 8,
            [Tooltip("대기 모션 랜덤 재생 활성화")]
            IsIdleMotionRandom = 9,
            
            //음성 인식 관련 옵션
            [Tooltip("음성인식 호출 모드 활성화")]
            IsCallNow = 10
        }

        /// <summary>
        /// 메인 UI 버튼들의 인덱스 정의
        /// </summary>
        public enum MainUiButtonEnum
        {
            [Tooltip("설정 버튼")]
            SettingButton = 0,
            [Tooltip("로그 버튼")]
            LogsButton = 1,
            [Tooltip("마이크 버튼")]
            MicsButton = 2,
            [Tooltip("RAG(지식 검색) 버튼")]
            RagButton = 3,
        }

        /// <summary>
        /// 패널(Panel)들의 인덱스 정의
        /// </summary>
        public enum PanelsEnum
        {
            [Tooltip("대화 패널")]
            TalikngPanel = 0,
            [Tooltip("설정 패널")]
            SettingPanel = 1,
            [Tooltip("로그 패널")]
            LOGPanel = 2,
            [Tooltip("옵션 패널")]
            OptionPanel = 3,
            [Tooltip("잠금 패널")]
            LockPanel = 4,
            [Tooltip("상태 패널")]
            StatusPanel = 5,
        }

        /// <summary>
        /// 설정 탭(Tab)들의 인덱스 정의
        /// </summary>
        public enum TabsEnum
        {
            [Tooltip("Live2D 설정 탭")]
            Live2D = 0,
            [Tooltip("그래픽 설정 탭")]
            Graphic = 1,
            [Tooltip("서버 설정 탭")]
            Server = 2,
            [Tooltip("음성 설정 탭")]
            Voice = 3,
            [Tooltip("기타 설정 탭")]
            Etc = 4,
        }

        #endregion

        #region Graphic Settings Enums (그래픽 설정 열거형)

        /// <summary>
        /// 그래픽 품질 프리셋 목록
        /// </summary>
        public enum PresetListEnum
        {
            VeryLow = 0,
            Low = 1,
            Medium = 2,
            High = 3,
            VeryHigh = 4,
            Ultra = 5,
            Custom = 6,
        }

        /// <summary>
        /// 렌더링 경로(Rendering Path) 옵션
        /// </summary>
        public enum RenderingPathEnum
        {
            VertexLit = 0,
            Forward = 1,
            DeferredShading = 2
        }

        /// <summary>
        /// 수직 동기화(VSync) 옵션
        /// </summary>
        public enum VsyncEnum
        {
            None = 0,
            VsyncOn = 1,
            VsyncHalf = 2,
        }

        /// <summary>
        /// SMAA 품질 옵션
        /// </summary>
        public enum SMAAEnum
        {
            Low = 0,
            Medium = 1,
            High = 2,
        }

        /// <summary>
        /// 안티앨리어싱(Anti-Aliasing) 모드 목록
        /// </summary>
        public enum AntiListEnum
        {
            None = 0,
            FXAA = 1,
            SMAA = 2,
            TAA = 3,
            MSAA = 4,
        }

        /// <summary>
        /// 리얼타임 픽셀 품질 옵션 (해상도 등)
        /// </summary>
        public enum RealtimePixelEnums
        {
            Low = 64,
            Medium = 256,
            High = 512,
        }

        #endregion

        #region Other Settings Enums (기타 설정 열거형)

        /// <summary>
        /// 기본 언어 설정 옵션
        /// </summary>
        public enum DefaultLanguageEnum
        {
            auto = 0,
            en = 1,
            ko = 2,
            ja = 3
        }

        /// <summary>
        /// 다운로드 가능한 모델/리소스 종류
        /// </summary>
        public enum DownloadEnums
        {
            Vad = 1,
            Whisper = 2,
            Keyword = 3,
            All = 4,
        }

        #endregion
    }

    /// <summary>
    /// Live2D 관련 열거형들을 관리하는 정적 클래스입니다.
    /// </summary>
    public static class Live2DEnums
    {
        /// <summary>
        /// Live2D 동작/상태 목록
        /// </summary>
        public enum Live2DList
        {
            Idle = -1,
            Loading = 0,
            Happy = 1,
            Sad = 2,
            Angry = 3
        }

        /// <summary>
        /// Live2D 스프라이트(표정 등) 인덱스
        /// </summary>
        public enum SpriteEnum
        {
            Idle = 0,
            Happy = 1,
            Sad = 2,
            Angry = 3
        }
    }
}
