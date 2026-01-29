#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
using ProjectLucia.Status;
using UnityEngine;
// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

namespace ProjectLucia.Capture
{
    /// <summary>
    /// Windows 환경에서 화면 캡처 기능을 제공하는 정적 클래스입니다.
    /// GDI32 및 User32 API를 사용하여 화면의 특정 영역이나 전체 화면을 캡처합니다.
    /// </summary>
    public static class DesktopCapture
    {
        #region Win32 Constants & Structures (Win32 상수 및 구조체)

        // ─────────────────────────────────────────
        // Win32 상수 / 구조체
        // ─────────────────────────────────────────
        private const int SRCCOPY = 0x00CC0020;
        private const uint BI_RGB = 0;
        private const uint DIB_RGB_COLORS = 0;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public int bmiColors; // 32bpp RGB에서는 팔레트 안 씀
        }

        #endregion

        #region Win32 P/Invoke Declarations (Win32 API 선언)

        // ─────────────────────────────────────────
        // Win32 P/Invoke
        // ─────────────────────────────────────────
        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool BitBlt(
            IntPtr hdcDest,
            int nXDest,
            int nYDest,
            int nWidth,
            int nHeight,
            IntPtr hdcSrc,
            int nXSrc,
            int nYSrc,
            int dwRop);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetDIBits(
            IntPtr hdc,
            IntPtr hbmp,
            uint uStartScan,
            uint cScanLines,
            byte[] lpvBits,
            ref BITMAPINFO lpbi,
            uint uUsage);

        #endregion

        #region Public Properties (공개 속성)

        /// <summary>
        /// 주 모니터의 가로 해상도를 반환합니다.
        /// </summary>
        public static int PrimaryWidth => GetSystemMetrics(SM_CXSCREEN);

        /// <summary>
        /// 주 모니터의 세로 해상도를 반환합니다.
        /// </summary>
        public static int PrimaryHeight => GetSystemMetrics(SM_CYSCREEN);

        #endregion

        #region Public API (공개 메서드)

        // ─────────────────────────────────────────
        // 공개 API
        // ─────────────────────────────────────────

        /// <summary>
        /// 윈도우 전체(주 모니터)를 캡처하여 Texture2D로 반환합니다.
        /// (예: Shift + Pause 단축키 기능 등에서 사용)
        /// </summary>
        /// <returns>캡처된 화면 이미지 (Texture2D)</returns>
        public static Texture2D CaptureFullPrimaryDisplay()
        {
            int width = GetSystemMetrics(SM_CXSCREEN);
            int height = GetSystemMetrics(SM_CYSCREEN);
            return CaptureRegion(0, 0, width, height);
        }

        /// <summary>
        /// 화면의 특정 영역을 캡처하여 Texture2D로 반환합니다.
        /// </summary>
        /// <param name="screenX">캡처 시작 X 좌표 (Windows 스크린 좌표계, 좌상단 기준)</param>
        /// <param name="screenY">캡처 시작 Y 좌표 (Windows 스크린 좌표계, 좌상단 기준)</param>
        /// <param name="width">캡처할 영역의 너비</param>
        /// <param name="height">캡처할 영역의 높이</param>
        /// <returns>캡처된 영역 이미지 (Texture2D), 실패 시 null</returns>
        public static Texture2D CaptureRegion(int screenX, int screenY, int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                if(SettingData.IsDebug) Debug.LogError($"CaptureRegion: 유효하지 않은 크기입니다 ({width}x{height})");
                return null;
            }

            IntPtr hScreenDC = IntPtr.Zero;
            IntPtr hMemDC = IntPtr.Zero;
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr hOld = IntPtr.Zero;

            try
            {
                // 화면 DC 가져오기
                hScreenDC = GetDC(IntPtr.Zero);
                if (hScreenDC == IntPtr.Zero)
                {
                    if(SettingData.IsDebug) Debug.LogError("CaptureRegion: GetDC 실패");
                    return null;
                }

                // 호환 DC 생성
                hMemDC = CreateCompatibleDC(hScreenDC);
                if (hMemDC == IntPtr.Zero)
                {
                    if(SettingData.IsDebug) Debug.LogError("CaptureRegion: CreateCompatibleDC 실패");
                    return null;
                }

                // 호환 비트맵 생성
                hBitmap = CreateCompatibleBitmap(hScreenDC, width, height);
                if (hBitmap == IntPtr.Zero)
                {
                    if(SettingData.IsDebug) Debug.LogError("CaptureRegion: CreateCompatibleBitmap 실패");
                    return null;
                }

                // 비트맵 선택
                hOld = SelectObject(hMemDC, hBitmap);
                if (hOld == IntPtr.Zero)
                {
                    if(SettingData.IsDebug) Debug.LogError("CaptureRegion: SelectObject 실패");
                    return null;
                }

                // 화면 → 메모리 비트맵 복사 (BitBlt)
                if (!BitBlt(hMemDC, 0, 0, width, height, hScreenDC, screenX, screenY, SRCCOPY))
                {
                    if(SettingData.IsDebug) Debug.LogError("CaptureRegion: BitBlt 실패");
                    return null;
                }

                // 32bpp BGRX DIB 설정 (※ 여기서 높이는 "양수" = bottom-up 방식)
                BITMAPINFO bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = width;
                bmi.bmiHeader.biHeight = height; // ★ 양수 사용 (Bottom-Up)
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = (int)BI_RGB;
                bmi.bmiHeader.biSizeImage = width * height * 4;

                byte[] bgrx = new byte[width * height * 4];

                // 비트맵 데이터 가져오기
                int scanLines = GetDIBits(
                    hScreenDC,
                    hBitmap,
                    0,
                    (uint)height,
                    bgrx,
                    ref bmi,
                    DIB_RGB_COLORS);

                if (scanLines == 0)
                {
                    if(SettingData.IsDebug) Debug.LogError("CaptureRegion: GetDIBits 실패");
                    return null;
                }

                // BGRX(bottom-up) → RGBA(top-down)로 변환
                // Unity Texture2D는 기본적으로 Bottom-Up이지만, LoadRawTextureData는 데이터 순서대로 채움.
                // GDI GetDIBits가 Bottom-Up으로 데이터를 주므로, 이를 뒤집거나 그대로 쓰거나 해야 함.
                // 여기서는 직접 픽셀 순서를 재배치하여 RGBA 포맷으로 변환합니다.
                int stride = width * 4;
                byte[] rgba = new byte[width * height * 4];

                for (int y = 0; y < height; y++)
                {
                    int srcRow  = y;  
                    int srcBase = srcRow * stride;
                    int dstBase = y * stride;

                    for (int x = 0; x < width; x++)
                    {
                        int s = srcBase + x * 4;
                        int d = dstBase + x * 4;

                        byte b = bgrx[s + 0];
                        byte g = bgrx[s + 1];
                        byte r = bgrx[s + 2];

                        // BGRX -> RGBA 변환
                        rgba[d + 0] = r;
                        rgba[d + 1] = g;
                        rgba[d + 2] = b;
                        rgba[d + 3] = 255; // Alpha
                    }
                }

                // Texture2D 생성 및 데이터 로드
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.LoadRawTextureData(rgba);
                tex.Apply();

                return tex;
            }
            finally
            {
                // 리소스 해제
                if (hOld != IntPtr.Zero && hMemDC != IntPtr.Zero) SelectObject(hMemDC, hOld);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                if (hMemDC != IntPtr.Zero) DeleteDC(hMemDC);
                if (hScreenDC != IntPtr.Zero) ReleaseDC(IntPtr.Zero, hScreenDC);
            }
        }

        #endregion
    }
}
#endif
