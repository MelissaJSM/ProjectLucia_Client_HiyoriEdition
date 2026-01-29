using System;
using System.Collections.Generic;
using ProjectLucia.Live2D;
using ProjectLucia.Server;
using ProjectLucia.Status;
using ProjectLucia.Windows;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ProjectLucia.GUI
{
    /// <summary>
    /// 사이드바 UI의 스크롤바(슬라이더)들을 관리하고, 해당 값에 따라 설정을 변경하는 매니저 클래스입니다.
    /// Live2D 크기, 폰트 크기, 그래픽 설정, Idle 모션 시간, 화면 감지 설정 등을 처리합니다.
    /// </summary>
    public class SideBarManager : MonoBehaviour
    {
        #region Inspector Fields (인스펙터 설정)

        [Tooltip("관리할 스크롤바 리스트 (UISettingEnums.ScrollbarEnum 순서와 일치해야 함)")]
        [SerializeField] private List<Scrollbar> scrollbars; 
        public List<Scrollbar> Scrollbars => scrollbars;

        [Header("Live2D Size Range")]
        [Tooltip("Live2D 모델 최소 크기 배율")]
        [FormerlySerializedAs("MinValue")] public float minValue;

        [Tooltip("Live2D 모델 최대 크기 배율")]
        [FormerlySerializedAs("MaxValue")] public float maxValue  = 10f;

        #endregion

        #region Constants (상수)

        // 대화 폰트 크기 범위
        private const float TextMinPt = 8f;
        private const float TextMaxPt = 72f;

        // Idle 모션(초) 범위
        private const float IdleMinSec = 1f;
        private const float IdleMaxSec = 60f;

        // DesktopObserver 범위 상수
        private const float CheckIntervalMin = 1f;
        private const float CheckIntervalMax = 60f;
        
        private const float ChangeThresholdMin = 1f;
        private const float ChangeThresholdMax = 100f;
        
        private const float StabilityDurationMin = 1f;
        private const float StabilityDurationMax = 10f;
        
        private const float MinSendIntervalMin = 1f;
        private const float MinSendIntervalMax = 60f;

        #endregion

        #region Private Fields (비공개 필드)

        // 내부 상태(텍스트 파싱 없이 보관)
        private int _idleMinSec = 5;
        private int _idleMaxSec = 10;

        public float live2dSizeValue;

        private TextManager _textManager;
        private SettingController _settingController;
        private Live2DButtonPosition _live2DButtonPosition;
        private DesktopObserver _desktopObserver;

        // 캐싱: PostProcessLayer / TAA
        private UnityEngine.Rendering.PostProcessing.PostProcessLayer _ppl;
        private UnityEngine.Rendering.PostProcessing.TemporalAntialiasing _taa;

        #endregion

        #region Unity Lifecycle (유니티 생명주기)

        [Obsolete("Obsolete")]
        private void Awake()
        {
            // 매니저 참조 가져오기
            _textManager          = GameManager.Instance.TextManager;
            _settingController    = GameManager.Instance.SettingController;
            _live2DButtonPosition = GameManager.Instance.Live2DButtonPosition;
            
            // DesktopObserver 찾기 (씬에 하나만 있다고 가정)
            _desktopObserver = FindObjectOfType<DesktopObserver>();

            // PostProcessing 컴포넌트 캐싱
            _ppl = _settingController != null ? _settingController.PostProcessLayer : null;
            _taa = _ppl != null ? _ppl.temporalAntialiasing : null;

            // 초기 내부 상태를 SettingData 기반으로 세팅
            _idleMinSec = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(IdleMinSec, IdleMaxSec, SettingData.IdleMotionRandomMin)),
                (int)IdleMinSec, (int)IdleMaxSec - 1
            );
            _idleMaxSec = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(IdleMinSec, IdleMaxSec, SettingData.IdleMotionRandomMax)),
                _idleMinSec + 1, (int)IdleMaxSec
            );
        }

        #endregion

        #region Helper Methods (보조 메서드)

        private static float Lerp01ToRange(float t, float min, float max) => Mathf.Lerp(min, max, t);
        private static float RangeToLerp01(float v, float min, float max) => Mathf.InverseLerp(min, max, v);
        private void SetText(int idx, string text) => _textManager.Texts[idx].text = text;

        #endregion

        #region Font Size Settings (폰트 크기 설정)

        /// <summary>
        /// 대화창 폰트 크기를 설정합니다.
        /// </summary>
        public void SetTalkFontSize(bool isClick)
        {
            float t = isClick
                ? Scrollbars[(int)UISettingEnums.ScrollbarEnum.TalkFontScrollbar].value
                : SettingData.TalkingFontSize;

            if (!isClick)
                Scrollbars[(int)UISettingEnums.ScrollbarEnum.TalkFontScrollbar].SetValueWithoutNotify(t);

            float pt = Lerp01ToRange(t, TextMinPt, TextMaxPt);
            _textManager.TalkText.fontSize = pt;
            SetText((int)UISettingEnums.TextsEnum.FontSizeText, $"{(int)(t * 100)}%");
        }

        #endregion

        #region Idle Motion Settings (Idle 모션 설정)

        /// <summary>
        /// Idle 모션 랜덤 재생의 최대 시간을 설정합니다.
        /// </summary>
        public void SetIdleMotionRandomMax(bool isClick)
        {
            float t = isClick
                ? Scrollbars[(int)UISettingEnums.ScrollbarEnum.IdleMotionRandomMax].value
                : SettingData.IdleMotionRandomMax;

            if (!isClick)
                Scrollbars[(int)UISettingEnums.ScrollbarEnum.IdleMotionRandomMax].SetValueWithoutNotify(t);

            // 희망값(초)
            int desired = Mathf.RoundToInt(Lerp01ToRange(t, IdleMinSec, IdleMaxSec));
            // 최소 제약: min < max
            _idleMaxSec = Mathf.Clamp(desired, _idleMinSec + 1, (int)IdleMaxSec);

            if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"[IdleMax] t={t:F3} desired={desired} -> {_idleMaxSec}");

            SetText((int)UISettingEnums.TextsEnum.IdleMotionRandomMax, $"{_idleMaxSec}");
        }

        /// <summary>
        /// Idle 모션 랜덤 재생의 최소 시간을 설정합니다.
        /// </summary>
        public void SetIdleMotionRandomMin(bool isClick)
        {
            float t = isClick
                ? Scrollbars[(int)UISettingEnums.ScrollbarEnum.IdleMotionRandomMin].value
                : SettingData.IdleMotionRandomMin;

            if (!isClick)
                Scrollbars[(int)UISettingEnums.ScrollbarEnum.IdleMotionRandomMin].SetValueWithoutNotify(t);

            int desired = Mathf.RoundToInt(Lerp01ToRange(t, IdleMinSec, IdleMaxSec));
            // 최소 제약: min < max
            _idleMinSec = Mathf.Clamp(desired, (int)IdleMinSec, _idleMaxSec - 1);

            if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"[IdleMin] t={t:F3} desired={desired} -> {_idleMinSec}");

            SetText((int)UISettingEnums.TextsEnum.IdleMotionRandomMin, $"{_idleMinSec}");
        }

        /// <summary>
        /// Idle 모션 고정 재생 시간을 설정합니다.
        /// </summary>
        public void SetIdleMotionFixed(bool isClick)
        {
            float t = isClick
                ? Scrollbars[(int)UISettingEnums.ScrollbarEnum.IdleMotionFixed].value
                : SettingData.IdleMotionFixed;

            if (!isClick)
                Scrollbars[(int)UISettingEnums.ScrollbarEnum.IdleMotionFixed].SetValueWithoutNotify(t);

            int sec = Mathf.RoundToInt(Lerp01ToRange(t, IdleMinSec, IdleMaxSec));
            SetText((int)UISettingEnums.TextsEnum.IdleMotionFixed, $"{sec}");
        }
        
        // SettingController에서 호출할 헬퍼 메서드 (Idle Motion 값 계산용)
        public float GetIdleMotionRandomMin01() => RangeToLerp01(_idleMinSec, IdleMinSec, IdleMaxSec);
        public float GetIdleMotionRandomMax01() => RangeToLerp01(_idleMaxSec, IdleMinSec, IdleMaxSec);
        public float GetIdleMotionFixed01()
        {
            float t = Scrollbars[(int)UISettingEnums.ScrollbarEnum.IdleMotionFixed].value;
            int sec = Mathf.RoundToInt(Lerp01ToRange(t, IdleMinSec, IdleMaxSec));
            return RangeToLerp01(sec, IdleMinSec, IdleMaxSec);
        }

        #endregion

        #region Live2D Size Settings (Live2D 크기 설정)

        /// <summary>
        /// Live2D 모델의 크기를 조절합니다.
        /// </summary>
        public void ScrollbarController(bool isClick)
        {
            float t = isClick
                ? Scrollbars[(int)UISettingEnums.ScrollbarEnum.Live2dSizeScrollbar].value
                : SettingData.Live2DSizeValue;

            if (!isClick)
                Scrollbars[(int)UISettingEnums.ScrollbarEnum.Live2dSizeScrollbar].SetValueWithoutNotify(t);

            live2dSizeValue = Lerp01ToRange(t, minValue, maxValue);
            _live2DButtonPosition.Live2dSize(live2dSizeValue);

            SetText((int)UISettingEnums.TextsEnum.L2dSizeText, $"{(int)(t * 100)}%");
        }

        #endregion

        #region Frame Limit Settings (프레임 제한 설정)

        /// <summary>
        /// 프레임 제한을 설정합니다. (VSync가 꺼져있을 때만 동작)
        /// </summary>
        public void SetFrameLimitSideBar(bool isClick)
        {
            float t = isClick
                ? Scrollbars[(int)UISettingEnums.ScrollbarEnum.FrameLimitScrollbar].value
                : SettingData.IsFrameLimit;

            if (!isClick)
                Scrollbars[(int)UISettingEnums.ScrollbarEnum.FrameLimitScrollbar].SetValueWithoutNotify(t);

            int targetFps = Mathf.RoundToInt(Lerp01ToRange(t, 30f, 300f));

            // VSync가 꺼져 있을 때만 의미가 있음
            if (QualitySettings.vSyncCount == (int)UISettingEnums.VsyncEnum.None)
            {
                Application.targetFrameRate = targetFps;
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"프레임 제한 적용: {Application.targetFrameRate}");
            }
            else
            {
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log("VSync 활성화 중 → 프레임 제한 미적용");
            }

            SetText(
                (int)UISettingEnums.TextsEnum.FrameLimitSidebarText,
                (QualitySettings.vSyncCount == (int)UISettingEnums.VsyncEnum.None
                    ? Application.targetFrameRate
                    : targetFps).ToString()
            );
        }

        #endregion

        #region TAA Settings (TAA 설정)

        /// <summary>
        /// TAA Jitter Spread 값을 설정합니다.
        /// </summary>
        public void SetTAAJitterSpreadSideBar(bool isClick)
        {
            float t = isClick
                ? Scrollbars[(int)UISettingEnums.ScrollbarEnum.JitterSpreadScrollbar].value
                : SettingData.IsJitterSpread;

            if (!isClick)
                Scrollbars[(int)UISettingEnums.ScrollbarEnum.JitterSpreadScrollbar].SetValueWithoutNotify(t);

            if (_taa != null)
            {
                _taa.jitterSpread = t;
                SetText((int)UISettingEnums.TextsEnum.JitterSpreadSidebarText, _taa.jitterSpread.ToString("F2"));
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"TAA 노이즈 확산: {_taa.jitterSpread}");
            }
        }

        /// <summary>
        /// TAA Stationary Blending 값을 설정합니다.
        /// </summary>
        public void SetTAAStationaryBlendingSideBar(bool isClick)
        {
            float t = isClick
                ? Scrollbars[(int)UISettingEnums.ScrollbarEnum.StaionaryBlendingScrollbar].value
                : SettingData.IsStaionaryBlending;

            if (!isClick)
                Scrollbars[(int)UISettingEnums.ScrollbarEnum.StaionaryBlendingScrollbar].SetValueWithoutNotify(t);

            if (_taa != null)
            {
                _taa.stationaryBlending = t;
                SetText((int)UISettingEnums.TextsEnum.StationaryBlendingSidebarText, _taa.stationaryBlending.ToString("F2"));
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"TAA 정적 블렌딩: {_taa.stationaryBlending}");
            }
        }

        /// <summary>
        /// TAA Motion Blending 값을 설정합니다.
        /// </summary>
        public void SetTAAMotionBlendingSideBar(bool isClick)
        {
            float t = isClick
                ? Scrollbars[(int)UISettingEnums.ScrollbarEnum.MotionBlendingScrollbar].value
                : SettingData.IsMotionBlending;

            if (!isClick)
                Scrollbars[(int)UISettingEnums.ScrollbarEnum.MotionBlendingScrollbar].SetValueWithoutNotify(t);

            if (_taa != null)
            {
                _taa.motionBlending = t;
                SetText((int)UISettingEnums.TextsEnum.MotionBlendingSidebarText, _taa.motionBlending.ToString("F2"));
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"TAA 동적 블렌딩: {_taa.motionBlending}");
            }
        }

        /// <summary>
        /// TAA Sharpness 값을 설정합니다.
        /// </summary>
        public void SetTAASharpnessSideBar(bool isClick)
        {
            float t = isClick
                ? Scrollbars[(int)UISettingEnums.ScrollbarEnum.SharpnessScrollbar].value
                : SettingData.IsSharpness;

            if (!isClick)
                Scrollbars[(int)UISettingEnums.ScrollbarEnum.SharpnessScrollbar].SetValueWithoutNotify(t);

            if (_taa != null)
            {
                _taa.sharpness = t;
                SetText((int)UISettingEnums.TextsEnum.SharpnessSidebarText, _taa.sharpness.ToString("F2"));
                if (SettingData.IsDebug) if(SettingData.IsDebug) Debug.Log($"TAA 선명도: {_taa.sharpness}");
            }
        }

        #endregion

        #region DesktopObserver Settings (화면 감지 설정)

        /// <summary>
        /// 화면 감지 주기를 설정합니다.
        /// </summary>
        public void SetCheckInterval(bool isClick)
        {
            float t = isClick
                ? Scrollbars[(int)UISettingEnums.ScrollbarEnum.CheckIntervalScrollbar].value
                : SettingData.CheckIntervalValue;

            if (!isClick)
                Scrollbars[(int)UISettingEnums.ScrollbarEnum.CheckIntervalScrollbar].SetValueWithoutNotify(t);

            float val = Lerp01ToRange(t, CheckIntervalMin, CheckIntervalMax);
            SetText((int)UISettingEnums.TextsEnum.CheckIntervalText, $"{val:F1}s");

            if (_desktopObserver != null) _desktopObserver.checkInterval = val;
        }

        /// <summary>
        /// 화면 변화 감지 임계값을 설정합니다.
        /// </summary>
        public void SetChangeThreshold(bool isClick)
        {
            float t = isClick
                ? Scrollbars[(int)UISettingEnums.ScrollbarEnum.ChangeThresholdScrollbar].value
                : SettingData.ChangeThresholdValue;

            if (!isClick)
                Scrollbars[(int)UISettingEnums.ScrollbarEnum.ChangeThresholdScrollbar].SetValueWithoutNotify(t);

            float val = Lerp01ToRange(t, ChangeThresholdMin, ChangeThresholdMax);
            SetText((int)UISettingEnums.TextsEnum.ChangeThresholdText, $"{val:F1}%");

            if (_desktopObserver != null) _desktopObserver.changeThreshold = val;
        }

        /// <summary>
        /// 화면 안정화 지속 시간을 설정합니다.
        /// </summary>
        public void SetStabilityDuration(bool isClick)
        {
            float t = isClick
                ? Scrollbars[(int)UISettingEnums.ScrollbarEnum.StabilityDurationScrollbar].value
                : SettingData.StabilityDurationValue;

            if (!isClick)
                Scrollbars[(int)UISettingEnums.ScrollbarEnum.StabilityDurationScrollbar].SetValueWithoutNotify(t);

            float val = Lerp01ToRange(t, StabilityDurationMin, StabilityDurationMax);
            SetText((int)UISettingEnums.TextsEnum.StabilityDurationText, $"{val:F1}s");

            if (_desktopObserver != null) _desktopObserver.stabilityDuration = val;
        }

        /// <summary>
        /// 최소 전송 간격을 설정합니다.
        /// </summary>
        public void SetMinSendInterval(bool isClick)
        {
            float t = isClick
                ? Scrollbars[(int)UISettingEnums.ScrollbarEnum.MinSendIntervalScrollbar].value
                : SettingData.MinSendIntervalValue;

            if (!isClick)
                Scrollbars[(int)UISettingEnums.ScrollbarEnum.MinSendIntervalScrollbar].SetValueWithoutNotify(t);

            float val = Lerp01ToRange(t, MinSendIntervalMin, MinSendIntervalMax);
            SetText((int)UISettingEnums.TextsEnum.MinSendIntervalText, $"{val:F1}s");

            if (_desktopObserver != null) _desktopObserver.minSendInterval = val;
        }

        #endregion
    }
}
