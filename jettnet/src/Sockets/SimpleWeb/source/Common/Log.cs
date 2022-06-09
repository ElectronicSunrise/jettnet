using System;
using Conditional = System.Diagnostics.ConditionalAttribute;

namespace JamesFrowen.SimpleWeb
{
    public static class Log
    {
        // used for Conditional
        const string SIMPLEWEB_LOG_ENABLED = nameof(SIMPLEWEB_LOG_ENABLED);
        const string DEBUG = nameof(DEBUG);

        public static Action<string> OnError;
        public static Action<string> OnInfo;
        public static Action<string> OnWarning;

        public static string BufferToString(byte[] buffer, int offset = 0, int? length = null)
        {
            return BitConverter.ToString(buffer, offset, length ?? buffer.Length);
        }

        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void DumpBuffer(string label, byte[] buffer, int offset, int length)
        {
            OnInfo?.Invoke($"VERBOSE: <color=cyan>{label}: {BufferToString(buffer, offset, length)}</color>");
        }

        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void DumpBuffer(string label, ArrayBuffer arrayBuffer)
        {
            OnInfo?.Invoke($"{label}: {BufferToString(arrayBuffer.array, 0, arrayBuffer.count)}");
        }

        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void Verbose(string msg)
        {
            OnInfo?.Invoke($"{msg}");
        }

        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void Info(string msg)
        {
            OnInfo?.Invoke(msg);
        }

        /// <summary>
        /// An expected Exception was caught, useful for debugging but not important
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="showColor"></param>
        [Conditional(SIMPLEWEB_LOG_ENABLED)]
        public static void InfoException(Exception e)
        {
            OnError?.Invoke($"{e.GetType().Name} Message: {e.Message}\n{e.StackTrace}\n\n");
        }

        [Conditional(SIMPLEWEB_LOG_ENABLED), Conditional(DEBUG)]
        public static void Warn(string msg)
        {
            OnWarning?.Invoke(msg);
        }

        [Conditional(SIMPLEWEB_LOG_ENABLED), Conditional(DEBUG)]
        public static void Error(string msg)
        {
            OnError?.Invoke(msg);
        }

        public static void Exception(Exception e)
        {
            OnError?.Invoke($"{e.GetType().Name} Message: {e.Message}\n{e.StackTrace}\n\n");
        }
    }
}