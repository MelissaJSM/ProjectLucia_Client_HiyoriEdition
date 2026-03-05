using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ProjectLucia.Windows
{
    public static class WindowsFileBrowser
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class OpenFileName
        {
            public int structSize = 0;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public string filter = null;
            public string customFilter = null;
            public int maxCustFilter = 0;
            public int filterIndex = 0;
            public string file = null;
            public int maxFile = 0;
            public string fileTitle = null;
            public int maxFileTitle = 0;
            public string initialDir = null;
            public string title = null;
            public int flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public string defExt = null;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public string templateName = null;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;
        }

        [DllImport("Comdlg32.dll", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Auto)]
        public static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        public static string[] ShowDialog(string title, string filter)
        {
            OpenFileName ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(ofn);
            ofn.filter = filter.Replace('|', '\0') + "\0"; // 필터 구분자를 널 문자로 변환
            ofn.file = new string(new char[2048]);
            ofn.maxFile = ofn.file.Length;
            ofn.fileTitle = new string(new char[64]);
            ofn.maxFileTitle = ofn.fileTitle.Length;
            ofn.initialDir = UnityEngine.Application.dataPath;
            ofn.title = title;
            ofn.defExt = "wav";
            
            // OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_ALLOWMULTISELECT | OFN_NOCHANGEDIR
            ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000200 | 0x00000008;

            if (GetOpenFileName(ofn))
            {
                // 다중 선택 시 디렉토리와 파일명이 널 문자로 구분됨
                // 단일 선택 시 전체 경로가 반환됨
                string[] parts = ofn.file.Split('\0');
                
                // 유효한 경로만 필터링
                var validPaths = new System.Collections.Generic.List<string>();
                foreach(var p in parts)
                {
                    if(!string.IsNullOrWhiteSpace(p)) validPaths.Add(p);
                }

                if (validPaths.Count == 1)
                {
                    return new string[] { validPaths[0] };
                }
                else if (validPaths.Count > 1)
                {
                    string dir = validPaths[0];
                    string[] files = new string[validPaths.Count - 1];
                    for (int i = 1; i < validPaths.Count; i++)
                    {
                        files[i - 1] = Path.Combine(dir, validPaths[i]);
                    }
                    return files;
                }
            }
            return null;
        }
    }
}