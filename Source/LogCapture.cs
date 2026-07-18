using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace TestRunner
{
    // Session-wide red-error capture. Hooks Unity's log event once, as early as Insight code
    // ever runs (TestRegistry's static constructor), and buckets everything it sees into
    // Loading (a one-time snapshot of what already happened), Test-attributed (whatever step
    // is active via BeginStep/Dispose), or Other/Idle (everything else).
    public static class LogCapture
    {
        internal const string InsightPrefix = "[Insight] ";

        private static readonly object Sync = new object();
        private static readonly List<CapturedError> Loading = new List<CapturedError>();
        private static readonly List<CapturedError> Other = new List<CapturedError>();
        private static readonly List<Regex> Suppressions = new List<Regex>();
        private static Scope? active;
        private static bool initialized;

        public static void Init()
        {
            lock (Sync)
            {
                if (initialized)
                {
                    return;
                }

                initialized = true;
                SnapshotLoading();
                Application.logMessageReceivedThreaded += OnLog;
            }
        }

        public static void RegisterSuppressions(IEnumerable<string> patterns)
        {
            if (patterns == null)
            {
                return;
            }

            lock (Sync)
            {
                foreach (string pattern in patterns)
                {
                    if (pattern.NullOrEmpty())
                    {
                        continue;
                    }

                    try
                    {
                        Suppressions.Add(new Regex(pattern));
                    }
                    catch (Exception e)
                    {
                        Log.Error(InsightPrefix + "invalid suppression pattern '" + pattern + "': " + e.Message);
                    }
                }
            }
        }

        public static IReadOnlyList<CapturedError> LoadingErrors
        {
            get { lock (Sync) { return new List<CapturedError>(Loading); } }
        }

        public static IReadOnlyList<CapturedError> OtherErrors
        {
            get { lock (Sync) { return new List<CapturedError>(Other); } }
        }

        public static Scope BeginStep() => new Scope();

        private static void SnapshotLoading()
        {
            foreach (LogMessage m in Log.Messages)
            {
                if (m.type != LogMessageType.Error || IsInsightNoise(m.text))
                {
                    continue;
                }

                Add(Loading, LogType.Error, m.text, m.StackTrace, m.repeats + 1);
            }
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (!IsCapturedType(type) || IsInsightNoise(condition) || IsSuppressed(condition))
            {
                return;
            }

            lock (Sync)
            {
                if (active != null)
                {
                    Add(active.Entries, type, condition, stackTrace, 1);
                }
                else
                {
                    Add(Other, type, condition, stackTrace, 1);
                }
            }
        }

        private static bool IsCapturedType(LogType type) =>
            type == LogType.Error || type == LogType.Exception || type == LogType.Assert;

        private static bool IsInsightNoise(string message) => message != null && message.StartsWith(InsightPrefix, StringComparison.Ordinal);

        private static bool IsSuppressed(string message)
        {
            foreach (Regex r in Suppressions)
            {
                if (r.IsMatch(message))
                {
                    return true;
                }
            }

            return false;
        }

        private static void Add(List<CapturedError> list, LogType type, string message, string stackTrace, int occurrences)
        {
            string trace = stackTrace ?? "";
            foreach (CapturedError e in list)
            {
                if (e.LogType == type && e.Message == message && e.StackTrace == trace)
                {
                    e.Count += occurrences;
                    return;
                }
            }

            list.Add(new CapturedError(type, message, trace) { Count = occurrences });
        }

        public static void WriteDiagnosticsReport(string dir)
        {
            List<CapturedError> loading;
            List<CapturedError> other;
            lock (Sync)
            {
                loading = new List<CapturedError>(Loading);
                other = new List<CapturedError>(Other);
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"loadingErrors\": [\n");
            AppendErrors(sb, loading);
            sb.Append("  ],\n");
            sb.Append("  \"otherErrors\": [\n");
            AppendErrors(sb, other);
            sb.Append("  ]\n}\n");

            string path = Path.Combine(dir, "_diagnostics.json");
            try
            {
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception e)
            {
                Log.Error(InsightPrefix + "diagnostics report write failed: " + e);
            }
        }

        private static void AppendErrors(StringBuilder sb, List<CapturedError> errors)
        {
            for (int i = 0; i < errors.Count; i++)
            {
                CapturedError e = errors[i];
                sb.Append("    { \"type\": ").Append(JsonUtil.Escape(e.LogType.ToString()))
                  .Append(", \"message\": ").Append(JsonUtil.Escape(e.Message))
                  .Append(", \"stackTrace\": ").Append(JsonUtil.Escape(e.StackTrace))
                  .Append(", \"count\": ").Append(e.Count)
                  .Append(" }");
                sb.Append(i < errors.Count - 1 ? ",\n" : "\n");
            }
        }

        public sealed class Scope : IDisposable
        {
            public readonly List<CapturedError> Entries = new List<CapturedError>();

            internal Scope()
            {
                lock (Sync)
                {
                    active = this;
                }
            }

            public void Dispose()
            {
                lock (Sync)
                {
                    if (active == this)
                    {
                        active = null;
                    }
                }
            }
        }
    }
}
