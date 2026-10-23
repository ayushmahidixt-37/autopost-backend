using System;
using ShapeEscape.Core;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>What the player earned for finishing a level (shown on the Level Complete screen).</summary>
    public sealed class RewardSummary
    {
        public int coins;
        public int bonusHints;
        public int stars;
        public bool newObject;
        public bool newBest;
        public string objectId;
        public bool isDaily;
        public int dailyStreak;
        public bool campaignFinished;
        /// <summary>1, 1.5 (hard) or 2 (super hard).</summary>
        public float coinMultiplier = 1f;
    }

    /// <summary>
    /// Coins, level unlocking, stars and the 7-day login calendar. Hints are never handed out for free here:
    /// players get EconomyRules.FreeHints once, then earn hints through optional rewarded ads.
    /// </summary>
    public sealed class ProgressionManager : MonoBehaviour
    {
        public static readonly int[] LoginRewards = { 20, 30, 40, 50, 60, 80, 150 };

        public event Action<int> CoinsChanged;

        GameManager _game;
        SaveData Data { get { return _game.Save.Data; } }

        public int Coins { get { return Data.coins; } }
        public int HighestUnlocked { get { return Mathf.Clamp(Data.highestUnlockedLevel, 1, LevelProvider.LevelCount); } }
        public bool CampaignComplete { get { return Data.GetLevel(LevelProvider.LevelCount) != null; } }

        /// <summary>Level the Play button starts: the first unfinished level, or the last one when everything is done.</summary>
        public int NextLevel
        {
            get
            {
                for (int n = 1; n <= HighestUnlocked; n++)
                    if (Data.GetLevel(n) == null) return n;
                return HighestUnlocked;
            }
        }

        public void Initialize(GameManager game)
        {
            _game = game;
        }

        public void AddCoins(int amount, string source)
        {
            if (amount <= 0) return;
            Data.coins += amount;
            _game.Save.MarkDirty();
            _game.Analytics.Log(AnalyticsEvents.CoinsEarned, "amount", amount, "source", source, "balance", Data.coins);
            if (CoinsChanged != null) CoinsChanged(Data.coins);
        }

        public bool SpendCoins(int amount, string reason)
        {
            if (amount <= 0) return true;
            if (Data.coins < amount) return false;
            Data.coins -= amount;
            _game.Save.MarkDirty();
            _game.Analytics.Log(AnalyticsEvents.CoinsSpent, "amount", amount, "reason", reason, "balance", Data.coins);
            if (CoinsChanged != null) CoinsChanged(Data.coins);
            return true;
        }

        public bool IsUnlocked(int level) { return level >= 1 && level <= HighestUnlocked; }

        public int StarsFor(int level)
        {
            var r = Data.GetLevel(level);
            return r != null ? r.stars : 0;
        }

        /// <summary>Records a finished campaign level and grants rewards (coins are granted here, not by the UI).</summary>
        public RewardSummary CompleteCampaignLevel(LevelSession session)
        {
            var spec = session.Spec;
            var summary = new RewardSummary { stars = session.Stars, objectId = spec.objectId };
            var record = Data.GetLevel(spec.number);
            bool firstClear = record == null;
            if (record == null)
            {
                record = new SaveData.LevelRecord { number = spec.number, bestSeconds = float.MaxValue };
                Data.levels.Add(record);
            }
            record.completions++;
            if (session.Stars > record.stars) record.stars = session.Stars;
            if (session.Duration < record.bestSeconds)
            {
                summary.newBest = !firstClear;
                record.bestSeconds = session.Duration;
            }

            int baseCoins = TierInfo.Get(spec.tier).coinReward;
            summary.coinMultiplier = EconomyRules.CoinMultiplier(spec);
            summary.coins = Mathf.RoundToInt((firstClear ? baseCoins + (session.Stars - 1) * 5 : Mathf.Max(3, baseCoins / 4)) * summary.coinMultiplier);
            AddCoins(summary.coins, firstClear ? "level_first_clear" : "level_replay");

            Data.totalLevelsCompleted++;

            if (spec.number >= Data.highestUnlockedLevel && spec.number < LevelProvider.LevelCount)
                Data.highestUnlockedLevel = spec.number + 1;
            summary.campaignFinished = firstClear && spec.number == LevelProvider.LevelCount;

            summary.newObject = _game.Collection.Unlock(spec.objectId, spec.variant);
            _game.Save.MarkDirty();
            _game.Save.SaveNow();
            return summary;
        }

        // ---------------------------------------------------------------- daily login calendar

        public static string Today { get { return DailyChallengeRules.DateKey(DateTime.Now); } }

        public bool LoginRewardAvailable { get { return Data.login.lastClaimDate != Today; } }

        /// <summary>The calendar day (0..6) the next claim will grant, accounting for a broken streak.</summary>
        public int PendingLoginDay
        {
            get
            {
                var l = Data.login;
                if (string.IsNullOrEmpty(l.lastClaimDate)) return 0;
                string yesterday = DailyChallengeRules.DateKey(DateTime.Now.AddDays(-1));
                if (l.lastClaimDate == yesterday) return l.dayIndex % LoginRewards.Length;
                if (l.lastClaimDate == Today) return l.dayIndex % LoginRewards.Length;
                return 0;
            }
        }

        /// <summary>Grants today's login reward. Returns coins granted (0 if already claimed).</summary>
        public int ClaimLoginReward(out bool bonusHint)
        {
            bonusHint = false; // hints are ad-only after the free starter pack
            if (!LoginRewardAvailable) return 0;
            int day = PendingLoginDay;
            int coins = LoginRewards[day];
            Data.login.lastClaimDate = Today;
            Data.login.dayIndex = (day + 1) % LoginRewards.Length;
            AddCoins(coins, "daily_login");
            _game.Analytics.Log(AnalyticsEvents.DailyLoginReward, "day", day + 1, "coins", coins);
            _game.Save.MarkDirty();
            return coins;
        }
    }
}
