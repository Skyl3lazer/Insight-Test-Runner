using UnityEngine;

namespace TestRunner
{
    public sealed class CapturedError
    {
        public readonly LogType LogType;
        public readonly string Message;
        public readonly string StackTrace;
        public int Count;

        public CapturedError(LogType logType, string message, string stackTrace)
        {
            LogType = logType;
            Message = message;
            StackTrace = stackTrace;
            Count = 1;
        }
    }
}
