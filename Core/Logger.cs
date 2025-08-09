using EZPlay.Core.Interfaces;
using UnityEngine;

namespace EZPlay.Core
{
    public enum LogLevel
    {
        DEBUG,
        INFO,
        WARNING,
        ERROR
    }

    public class Logger : EZPlay.Core.Interfaces.ILogger
    {
        public static LogLevel CurrentLogLevel { get; set; } = LogLevel.INFO;

        private readonly string prefix;

        public Logger(string prefix)
        {
            this.prefix = prefix;
        }

        public void Debug(string message)
        {
            if (CurrentLogLevel <= LogLevel.DEBUG)
            {
                UnityEngine.Debug.Log($"[{prefix}] [DEBUG] {message}");
            }
        }

        public void Info(string message)
        {
            if (CurrentLogLevel <= LogLevel.INFO)
            {
                UnityEngine.Debug.Log($"[{prefix}] [INFO] {message}");
            }
        }

        public void Warning(string message)
        {
            if (CurrentLogLevel <= LogLevel.WARNING)
            {
                UnityEngine.Debug.LogWarning($"[{prefix}] [WARNING] {message}");
            }
        }

        public void Error(string message)
        {
            if (CurrentLogLevel <= LogLevel.ERROR)
            {
                UnityEngine.Debug.LogError($"[{prefix}] [ERROR] {message}");
            }
        }
    }
}