using System;
using ProjectLucia.Status;
using UnityEngine;

namespace ProjectLucia.ThirdParty.Whisper.Runtime.Utils
{
    public enum LogLevel
    {
        Verbose,
        Log,
        Warning,
        Error,
    }

    /// <summary>
    /// Wrapper for Unity logger that can be configured by log level.
    /// </summary>
    public static class LogUtils
    {
        public static LogLevel Level = LogLevel.Verbose;
        
        public static void Exception(Exception msg)
        {
            if(SettingData.IsDebug) Debug.LogException(msg);
        }
        
        public static void Error(string msg)
        {
            if(SettingData.IsDebug) Debug.LogError(msg);
        }

        public static void Warning(string msg)
        {
            if (Level > LogLevel.Warning)
                return;
            if(SettingData.IsDebug) Debug.LogWarning(msg);
        }

        public static void Log(string msg)
        {
            if (Level > LogLevel.Log)
                return;
            if(SettingData.IsDebug) Debug.Log(msg);
        }
        
        public static void Verbose(string msg)
        {
            if (Level > LogLevel.Verbose)
                return;
            if(SettingData.IsDebug) Debug.Log(msg);
        }      
    }
}