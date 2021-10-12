using System;
using System.Collections.Generic;
using System.Text;

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
        private Action<object> _info = Console.WriteLine;
        private Action<object> _warning = Console.WriteLine;
        private Action<object> _error = Console.Error.WriteLine;

        public Logger() { }

        public Logger(Action<object> info, Action<object> warning, Action<object> error)
        {
            _info = info;
            _warning = warning;
            _error = error;
        }

        public void Log(object msg, LogLevel logLevel = LogLevel.Info)
        {
            switch (logLevel)
            {
                case LogLevel.Info:
                    _info?.Invoke(msg);
                    break;
                case LogLevel.Warning:
                    _warning?.Invoke(msg);
                    break;
                case LogLevel.Error:
                    _error?.Invoke(msg);
                    break;
            }
        }
    }
}
