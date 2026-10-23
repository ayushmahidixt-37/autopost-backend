using System;

namespace ShapeEscape.Core
{
    /// <summary>
    /// Candy-Crush-style economy rules, kept engine-free so they are unit tested with the puzzle core.
    ///  • 5 lives; one comes back every 30 minutes. A failed level, or leaving a level after moving, costs a life.
    ///  • Each level allows 3 wrong moves (hearts) from level 4 on; levels 1-3 are free practice.
    ///  • 3 free hints for the lifetime of the install; after that one hint per rewarded ad (5 per day).
    ///  • Optional rewarded ads: +2 moves once per attempt, +1 life (5 per day), x2 coins on level complete.
    ///  • Interstitials only between levels: from level 6, every 3 completed levels, never within 2 minutes
    ///    of a rewarded ad.
    /// </summary>
    public static class EconomyRules
    {
        public const int MaxLives = 5;
        public static readonly TimeSpan LifeRegen = TimeSpan.FromMinutes(30);
        public const int MistakesPerLevel = 3;
        public const int PracticeLevels = 3;
        public const int ContinueMoves = 2;
        public const int FreeHints = 3;
        public const int AdHintsPerDay = 5;
        public const int AdLivesPerDay = 5;
        public const int LivesRefillCost = 250;
        public const int InterstitialFromLevel = 6;
        public const int InterstitialEveryLevels = 3;
        public static readonly TimeSpan NoInterstitialAfterRewarded = TimeSpan.FromMinutes(2);
        public const int StreakBonusCoins = 100;

        /// <summary>Wrong moves allowed in a level (int.MaxValue = unlimited practice).</summary>
        public static int MistakesFor(LevelSpec spec)
        {
            if (spec != null && !spec.isDaily && spec.number <= PracticeLevels) return int.MaxValue;
            return MistakesPerLevel;
        }

        /// <summary>Coin multiplier for hard / super-hard levels.</summary>
        public static float CoinMultiplier(LevelSpec spec)
        {
            if (spec == null) return 1f;
            return spec.superHard ? 2f : spec.hard ? 1.5f : 1f;
        }
    }

    /// <summary>
    /// Lives with time-based regeneration. State is two numbers (lives, regenStartTicks) so it persists trivially;
    /// regenStartTicks is the UTC time the oldest missing life started refilling (0 when full).
    /// </summary>
    public struct LivesState
    {
        public int lives;
        public long regenStartTicks;

        public LivesState(int lives, long regenStartTicks)
        {
            this.lives = lives;
            this.regenStartTicks = regenStartTicks;
        }

        public bool IsFull { get { return lives >= EconomyRules.MaxLives; } }

        /// <summary>Applies elapsed regeneration up to <paramref name="nowUtc"/>.</summary>
        public LivesState Sync(DateTime nowUtc)
        {
            var s = this;
            if (s.lives >= EconomyRules.MaxLives)
            {
                s.lives = EconomyRules.MaxLives;
                s.regenStartTicks = 0;
                return s;
            }
            if (s.lives < 0) s.lives = 0;
            if (s.regenStartTicks <= 0 || s.regenStartTicks > nowUtc.Ticks) s.regenStartTicks = nowUtc.Ticks;
            long gained = (nowUtc.Ticks - s.regenStartTicks) / EconomyRules.LifeRegen.Ticks;
            if (gained > 0)
            {
                s.lives = (int)Math.Min(EconomyRules.MaxLives, s.lives + gained);
                s.regenStartTicks = s.lives >= EconomyRules.MaxLives ? 0 : s.regenStartTicks + gained * EconomyRules.LifeRegen.Ticks;
            }
            return s;
        }

        public LivesState Lose(DateTime nowUtc)
        {
            var s = Sync(nowUtc);
            if (s.lives >= EconomyRules.MaxLives) s.regenStartTicks = nowUtc.Ticks;
            s.lives = Math.Max(0, s.lives - 1);
            return s;
        }

        public LivesState Add(int amount, DateTime nowUtc)
        {
            var s = Sync(nowUtc);
            s.lives = Math.Min(EconomyRules.MaxLives, s.lives + Math.Max(0, amount));
            if (s.lives >= EconomyRules.MaxLives) s.regenStartTicks = 0;
            return s;
        }

        /// <summary>Time until the next life (zero when full).</summary>
        public TimeSpan TimeToNext(DateTime nowUtc)
        {
            var s = Sync(nowUtc);
            if (s.IsFull) return TimeSpan.Zero;
            long remaining = s.regenStartTicks + EconomyRules.LifeRegen.Ticks - nowUtc.Ticks;
            return TimeSpan.FromTicks(Math.Max(0, remaining));
        }
    }
}
