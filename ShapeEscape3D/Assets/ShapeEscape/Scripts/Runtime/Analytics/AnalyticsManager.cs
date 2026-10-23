using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>Event names (snake_case, ≤ 40 chars, Firebase-compatible).</summary>
    public static class AnalyticsEvents
    {
        public const string GameLaunch = "game_launch";
        public const string SessionStart = "session_start_custom";
        public const string SessionEnd = "session_end";
        public const string LevelStart = "level_start";
        public const string LevelComplete = "level_complete";
        public const string LevelFail = "level_fail";
        public const string LevelRestart = "level_restart";
        public const string MoveBlocked = "move_blocked";
        public const string HintUsed = "hint_used";
        public const string HintEarned = "hint_earned";
        public const string AdImpression = "ad_impression_custom";
        public const string AdReward = "ad_reward";
        public const string AdFailed = "ad_failed";
        public const string DailyStart = "daily_challenge_start";
        public const string DailyComplete = "daily_challenge_complete";
        public const string DailyLoginReward = "daily_login_reward";
        public const string CollectionProgress = "collection_progress";
        public const string CollectionView = "collection_view";
        public const string CoinsEarned = "coins_earned";
        public const string CoinsSpent = "coins_spent";
        public const string TutorialShown = "tutorial_shown";
        public const string Error = "client_error";
    }

    public interface IAnalyticsProvider
    {
        string Name { get; }
        void Initialize();
        void LogEvent(string name, Dictionary<string, object> parameters);
        void SetUserProperty(string name, string value);
        void SetCollectionEnabled(bool enabled);
    }

    /// <summary>Logs to the console in the editor and development builds.</summary>
    public sealed class DebugAnalyticsProvider : IAnalyticsProvider
    {
        public string Name { get { return "debug"; } }
        public void Initialize() { }
        public void SetCollectionEnabled(bool enabled) { }
        public void SetUserProperty(string name, string value) { Debug.Log("[Analytics] user." + name + " = " + value); }

        public void LogEvent(string name, Dictionary<string, object> parameters)
        {
            var sb = new System.Text.StringBuilder("[Analytics] ").Append(name);
            if (parameters != null)
                foreach (var kv in parameters) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            Debug.Log(sb.ToString());
        }
    }

#if SE_FIREBASE
    /// <summary>Firebase Analytics adapter. Requires the Firebase Unity SDK and the SE_FIREBASE scripting define.</summary>
    public sealed class FirebaseAnalyticsProvider : IAnalyticsProvider
    {
        bool _ready;
        public string Name { get { return "firebase"; } }

        public void Initialize()
        {
            Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
            {
                _ready = task.Result == Firebase.DependencyStatus.Available;
                if (!_ready) Debug.LogWarning("[Analytics] Firebase unavailable: " + task.Result);
            });
        }

        public void SetCollectionEnabled(bool enabled)
        {
            Firebase.Analytics.FirebaseAnalytics.SetAnalyticsCollectionEnabled(enabled);
        }

        public void SetUserProperty(string name, string value)
        {
            if (_ready) Firebase.Analytics.FirebaseAnalytics.SetUserProperty(name, value);
        }

        public void LogEvent(string name, Dictionary<string, object> parameters)
        {
            if (!_ready) return;
            var list = new List<Firebase.Analytics.Parameter>();
            if (parameters != null)
                foreach (var kv in parameters)
                {
                    if (kv.Value is int) list.Add(new Firebase.Analytics.Parameter(kv.Key, (long)(int)kv.Value));
                    else if (kv.Value is long) list.Add(new Firebase.Analytics.Parameter(kv.Key, (long)kv.Value));
                    else if (kv.Value is float) list.Add(new Firebase.Analytics.Parameter(kv.Key, (double)(float)kv.Value));
                    else if (kv.Value is double) list.Add(new Firebase.Analytics.Parameter(kv.Key, (double)kv.Value));
                    else list.Add(new Firebase.Analytics.Parameter(kv.Key, kv.Value != null ? kv.Value.ToString() : ""));
                }
            Firebase.Analytics.FirebaseAnalytics.LogEvent(name, list.ToArray());
        }
    }
#endif

    /// <summary>
    /// Provider-agnostic analytics facade. Gameplay code calls <see cref="Log"/> with key/value pairs; the
    /// manager adds common context (session, level progress) and fans out to every registered provider.
    /// No personal data is collected by the game itself.
    /// </summary>
    public sealed class AnalyticsManager : MonoBehaviour
    {
        readonly List<IAnalyticsProvider> _providers = new List<IAnalyticsProvider>();
        float _sessionStart;
        float _pausedAt = -1f;
        int _sessionIndex;
        GameManager _game;

        /// <summary>Last N events, for the in-game debug overlay and tests.</summary>
        public readonly List<string> RecentEvents = new List<string>();

        public void Initialize(GameManager game)
        {
            _game = game;
#if SE_FIREBASE
            _providers.Add(new FirebaseAnalyticsProvider());
#endif
            if (Debug.isDebugBuild || Application.isEditor) _providers.Add(new DebugAnalyticsProvider());
            foreach (var p in _providers) p.Initialize();
            _sessionStart = Time.unscaledTime;
            _sessionIndex = game.Save.Data.sessions;
        }

        public void AddProvider(IAnalyticsProvider provider)
        {
            provider.Initialize();
            _providers.Add(provider);
        }

        public void SetCollectionEnabled(bool enabled)
        {
            foreach (var p in _providers) p.SetCollectionEnabled(enabled);
        }

        /// <summary>Logs an event: Log("level_start", "level", 3, "tier", "Easy").</summary>
        public void Log(string name, params object[] keyValues)
        {
            var dict = new Dictionary<string, object>();
            for (int i = 0; i + 1 < keyValues.Length; i += 2)
                dict[Convert.ToString(keyValues[i])] = keyValues[i + 1];
            LogEvent(name, dict);
        }

        public void LogEvent(string name, Dictionary<string, object> parameters)
        {
            if (!parameters.ContainsKey("session")) parameters["session"] = _sessionIndex;
            if (_game != null && _game.Save != null && _game.Save.Data != null && !parameters.ContainsKey("max_level"))
                parameters["max_level"] = _game.Save.Data.highestUnlockedLevel;

            RecentEvents.Add(name);
            if (RecentEvents.Count > 50) RecentEvents.RemoveAt(0);

            foreach (var p in _providers)
            {
                try { p.LogEvent(name, parameters); }
                catch (Exception e) { Debug.LogWarning("[Analytics] " + p.Name + " failed: " + e.Message); }
            }
        }

        public void SetUserProperty(string name, string value)
        {
            foreach (var p in _providers) p.SetUserProperty(name, value);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _pausedAt = Time.unscaledTime;
                Log(AnalyticsEvents.SessionEnd, "duration_s", Mathf.RoundToInt(Time.unscaledTime - _sessionStart));
            }
            else if (_pausedAt >= 0f)
            {
                // A pause longer than 30 s starts a new session.
                if (Time.unscaledTime - _pausedAt > 30f)
                {
                    _sessionStart = Time.unscaledTime;
                    _sessionIndex++;
                    Log(AnalyticsEvents.SessionStart);
                }
                _pausedAt = -1f;
            }
        }

        void OnApplicationQuit()
        {
            Log(AnalyticsEvents.SessionEnd, "duration_s", Mathf.RoundToInt(Time.unscaledTime - _sessionStart));
        }
    }
}
