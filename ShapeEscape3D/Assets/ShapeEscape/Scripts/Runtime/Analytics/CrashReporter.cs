using System;
using System.IO;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Captures unhandled exceptions and errors: forwards them to Firebase Crashlytics when the SDK is present
    /// (SE_FIREBASE define), reports a rate-limited analytics event, and keeps a small local log
    /// (persistentDataPath/crash_log.txt) that testers can attach to bug reports.
    /// </summary>
    public sealed class CrashReporter : MonoBehaviour
    {
        const int MaxLogBytes = 64 * 1024;
        const int MaxReportsPerSession = 20;

        string _logPath;
        int _reported;
        readonly object _lock = new object();
        AnalyticsManager _analytics;
        string _pendingEvent;

        public void Initialize(AnalyticsManager analytics)
        {
            _analytics = analytics;
            _logPath = Path.Combine(Application.persistentDataPath, "crash_log.txt");
            Application.logMessageReceivedThreaded += OnLog;
#if SE_FIREBASE
            Firebase.Crashlytics.Crashlytics.ReportUncaughtExceptionsAsFatal = true;
#endif
        }

        void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= OnLog;
        }

        void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            lock (_lock)
            {
                if (_reported >= MaxReportsPerSession) return;
                _reported++;
                try
                {
                    if (File.Exists(_logPath) && new FileInfo(_logPath).Length > MaxLogBytes) File.Delete(_logPath);
                    File.AppendAllText(_logPath, DateTime.UtcNow.ToString("o") + " [" + type + "] " + condition + "\n" + stackTrace + "\n");
                }
                catch (Exception)
                {
                    // Never throw from the log callback.
                }
                // Analytics must be called on the main thread; queue it.
                _pendingEvent = condition.Length > 90 ? condition.Substring(0, 90) : condition;
            }
        }

        void Update()
        {
            string pending = null;
            lock (_lock)
            {
                pending = _pendingEvent;
                _pendingEvent = null;
            }
            if (pending != null && _analytics != null) _analytics.Log(AnalyticsEvents.Error, "message", pending);
        }

        public string ReadLog()
        {
            try { return File.Exists(_logPath) ? File.ReadAllText(_logPath) : ""; }
            catch (Exception) { return ""; }
        }
    }
}
