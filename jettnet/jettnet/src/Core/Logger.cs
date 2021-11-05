using System;

namespace jettnet.logging
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error
    }

    public class Logger
    {
        private readonly Action<object> _error   = Console.Error.WriteLine;
        private readonly Action<object> _info    = Console.WriteLine;
        private readonly Action<object> _warning = Console.WriteLine;

        public Logger()
        {
        }

        public Logger(Action<object> info, Action<object> warning, Action<object> error)
        {
            _info    = info;
            _warning = warning;
            _error   = error;
        }

        public void Log(object msg, LogLevel logLevel = LogLevel.Info)
        {
#if UNITY_64
            switch (logLevel)
            {
                case LogLevel.Info:
                    UnityEngine.Debug.Log(msg.ToString());
                    break;
                case LogLevel.Warning:
                    UnityEngine.Debug.LogWarning(msg.ToString());
                    break;
                case LogLevel.Error:
                    UnityEngine.Debug.LogError(msg.ToString());
                    break;
            }
#else

            switch (logLevel)
            {
                case LogLevel.Info:
                    _info?.Invoke(msg.ToString());
                    break;
                case LogLevel.Warning:
                    _warning?.Invoke(msg.ToString());
                    break;
                case LogLevel.Error:
                    _error?.Invoke(msg.ToString());
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
            }

#endif
        }
    }
}