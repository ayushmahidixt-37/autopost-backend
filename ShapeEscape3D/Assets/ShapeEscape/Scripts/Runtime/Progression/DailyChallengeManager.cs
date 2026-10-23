using System;
using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// One special puzzle per calendar day (same seed for every player on that date). Completing it on
    /// consecutive days builds a streak that raises both the reward and the difficulty; every third day
    /// features a daily-exclusive collectible object. A capped history is kept for the Daily screen.
    /// </summary>
    public sealed class DailyChallengeManager : MonoBehaviour
    {
        public const int HistoryLimit = 60;

        GameManager _game;
        SaveData.DailyData Data { get { return _game.Save.Data.daily; } }

        public void Initialize(GameManager game)
        {
            _game = game;
        }

        public static DateTime TodayDate { get { return DateTime.Now.Date; } }
        public string TodayKey { get { return DailyChallengeRules.DateKey(TodayDate); } }
        public bool CompletedToday { get { return Data.lastCompletedDate == TodayKey; } }

        /// <summary>The streak as it stands today (0 if yesterday was missed).</summary>
        public int CurrentStreak
        {
            get
            {
                if (CompletedToday) return Data.streak;
                string yesterday = DailyChallengeRules.DateKey(TodayDate.AddDays(-1));
                return Data.lastCompletedDate == yesterday ? Data.streak : 0;
            }
        }

        public int BestStreak { get { return Data.bestStreak; } }

        public LevelSpec TodaySpec()
        {
            int streakForDifficulty = CompletedToday ? Math.Max(0, Data.streak - 1) : CurrentStreak;
            return DailyChallengeRules.Build(TodayDate, streakForDifficulty, _game.Progression.HighestUnlocked);
        }

        public int NextReward { get { return DailyChallengeRules.RewardFor(CurrentStreak + 1); } }

        public RewardSummary Complete(LevelSession session)
        {
            var summary = new RewardSummary { isDaily = true, objectId = session.Spec.objectId, stars = session.Stars };
            if (session.Spec.dailyDate != TodayKey || CompletedToday)
            {
                // Replay (or a challenge that crossed midnight): small consolation reward only.
                summary.coins = 5;
                _game.Progression.AddCoins(summary.coins, "daily_replay");
                summary.dailyStreak = Data.streak;
                return summary;
            }

            int streak = CurrentStreak + 1;
            Data.streak = streak;
            Data.bestStreak = Math.Max(Data.bestStreak, streak);
            Data.lastCompletedDate = TodayKey;
            summary.dailyStreak = streak;
            summary.coins = DailyChallengeRules.RewardFor(streak);
            _game.Progression.AddCoins(summary.coins, "daily_challenge");
            if (streak % 7 == 0)
            {
                summary.bonusHints = 2;
                _game.Hints.AddHints(2, "daily_streak");
            }
            summary.newObject = _game.Collection.Unlock(session.Spec.objectId, 0);

            Data.history.Insert(0, new SaveData.DailyRecord
            {
                date = TodayKey,
                objectId = session.Spec.objectId,
                seconds = session.Duration,
                coins = summary.coins,
                streak = streak
            });
            while (Data.history.Count > HistoryLimit) Data.history.RemoveAt(Data.history.Count - 1);

            _game.Analytics.Log(AnalyticsEvents.DailyComplete, "date", TodayKey, "streak", streak, "object", session.Spec.objectId,
                "seconds", Mathf.RoundToInt(session.Duration));
            _game.Save.MarkDirty();
            _game.Save.SaveNow();
            return summary;
        }
    }
}
