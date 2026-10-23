using System;
using System.Collections.Generic;

namespace ShapeEscape.Core
{
    /// <summary>Everything needed to (re)build one level deterministically.</summary>
    [Serializable]
    public class LevelSpec
    {
        public int number;
        public string objectId;
        /// <summary>0, 1, 2 — the three difficulty variations of each object.</summary>
        public int variant;
        public DifficultyTier tier;
        public int targetPieces;
        public int seed;
        public MechanicPlan mechanics = new MechanicPlan();
        /// <summary>Special arrow introduced (with a tutorial card) in this level, or Normal for none.</summary>
        public ArrowKind introduces = ArrowKind.Normal;
        public bool isDaily;
        public string dailyDate;

        public string Key
        {
            get { return isDaily ? "daily_" + dailyDate : "level_" + number; }
        }

        public GenerationRequest ToRequest(int devicePieceCap = 0)
        {
            return new GenerationRequest
            {
                objectId = objectId,
                tier = tier,
                targetPieces = targetPieces,
                seed = seed,
                mechanics = mechanics != null ? mechanics.Clone() : new MechanicPlan(),
                devicePieceCap = devicePieceCap
            };
        }
    }

    /// <summary>
    /// The 30-level campaign: 10 everyday objects x 3 difficulty variations, with special arrows introduced
    /// gradually. Levels 1-10 present every object once, 11-20 revisit them harder, 21-30 hardest.
    /// </summary>
    public static class LevelCatalog
    {
        public const int CampaignLevelCount = 30;

        // Level number at which each special arrow first appears.
        public const int IntroLocked = 7;
        public const int IntroChain = 11;
        public const int IntroHidden = 14;
        public const int IntroRotating = 18;
        public const int IntroColor = 22;

        static List<LevelSpec> _levels;

        public static IList<LevelSpec> Campaign
        {
            get
            {
                if (_levels == null) _levels = BuildCampaign();
                return _levels;
            }
        }

        public static LevelSpec Get(int number)
        {
            if (number < 1 || number > Campaign.Count) return null;
            return Campaign[number - 1];
        }

        public static DifficultyTier TierForLevel(int n)
        {
            if (n <= 4) return DifficultyTier.Beginner;
            if (n <= 10) return DifficultyTier.Easy;
            if (n <= 18) return DifficultyTier.Medium;
            if (n <= 25) return DifficultyTier.Hard;
            return DifficultyTier.Expert;
        }

        static List<LevelSpec> BuildCampaign()
        {
            var list = new List<LevelSpec>();
            for (int n = 1; n <= CampaignLevelCount; n++)
            {
                var tier = TierForLevel(n);
                var info = TierInfo.Get(tier);
                int first, last;
                TierLevelRange(tier, out first, out last);
                float t = last > first ? (n - first) / (float)(last - first) : 0.5f;
                int target = (int)Math.Round(info.minPieces + (info.maxPieces - info.minPieces) * (0.25f + 0.6f * t));

                var spec = new LevelSpec
                {
                    number = n,
                    objectId = ObjectCatalog.CampaignOrder[(n - 1) % ObjectCatalog.CampaignOrder.Length],
                    variant = (n - 1) / ObjectCatalog.CampaignOrder.Length,
                    tier = tier,
                    targetPieces = target,
                    seed = DeterministicRandom.Combine(0x5E3D, n * 7919),
                    mechanics = MechanicsForLevel(n)
                };
                if (n == IntroLocked) spec.introduces = ArrowKind.Locked;
                else if (n == IntroChain) spec.introduces = ArrowKind.Chain;
                else if (n == IntroHidden) spec.introduces = ArrowKind.Hidden;
                else if (n == IntroRotating) spec.introduces = ArrowKind.Rotating;
                else if (n == IntroColor) spec.introduces = ArrowKind.Color;
                list.Add(spec);
            }
            return list;
        }

        static void TierLevelRange(DifficultyTier tier, out int first, out int last)
        {
            switch (tier)
            {
                case DifficultyTier.Beginner: first = 1; last = 4; break;
                case DifficultyTier.Easy: first = 5; last = 10; break;
                case DifficultyTier.Medium: first = 11; last = 18; break;
                case DifficultyTier.Hard: first = 19; last = 25; break;
                default: first = 26; last = 30; break;
            }
        }

        /// <summary>Special-arrow mix for a campaign level (new mechanics appear alone first, then mix in).</summary>
        public static MechanicPlan MechanicsForLevel(int n)
        {
            var m = new MechanicPlan();
            if (n >= IntroLocked) m.locked = n == IntroLocked ? 1 : n < IntroChain ? 2 : n < 20 ? 2 : 3;
            if (n >= IntroChain) m.chain = n == IntroChain ? 2 : n < 20 ? 2 : 3;
            if (n >= IntroHidden) m.hidden = n == IntroHidden ? 2 : n < 25 ? 2 : 4;
            if (n >= IntroRotating) m.rotating = n == IntroRotating ? 1 : n < 25 ? 2 : 3;
            if (n >= IntroColor) m.colorGates = n == IntroColor ? 1 : n < 27 ? 1 : 2;

            // The introduction level focuses on the new mechanic.
            if (n == IntroChain) m.locked = 0;
            if (n == IntroHidden) { m.locked = 1; m.chain = 0; }
            if (n == IntroRotating) { m.hidden = 0; m.chain = 0; m.locked = 1; }
            if (n == IntroColor) { m.rotating = 0; m.hidden = 0; m.chain = 1; }
            return m;
        }

        /// <summary>Mechanics unlocked for a player who has reached <paramref name="highestLevel"/> (used by daily challenges).</summary>
        public static MechanicPlan MechanicsUnlockedBy(int highestLevel, DifficultyTier tier)
        {
            var m = new MechanicPlan();
            int scale = tier >= DifficultyTier.Hard ? 2 : 1;
            if (highestLevel >= IntroLocked) m.locked = scale;
            if (highestLevel >= IntroChain) m.chain = scale;
            if (highestLevel >= IntroHidden) m.hidden = 1 + scale;
            if (highestLevel >= IntroRotating) m.rotating = scale;
            if (highestLevel >= IntroColor) m.colorGates = 1;
            return m;
        }
    }

    /// <summary>Deterministic daily challenge rules (same puzzle for everyone on a given date).</summary>
    public static class DailyChallengeRules
    {
        /// <summary>Weekday drives the base difficulty: Monday is gentle, the weekend is hardest.</summary>
        public static DifficultyTier BaseTierFor(DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Monday:
                case DayOfWeek.Tuesday: return DifficultyTier.Easy;
                case DayOfWeek.Wednesday:
                case DayOfWeek.Thursday: return DifficultyTier.Medium;
                case DayOfWeek.Friday:
                case DayOfWeek.Saturday: return DifficultyTier.Medium;
                default: return DifficultyTier.Hard;
            }
        }

        public static string DateKey(DateTime date) { return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture); }

        /// <summary>Builds the challenge for a date. Difficulty rises with the player's streak (capped at Expert).</summary>
        public static LevelSpec Build(DateTime date, int streak, int highestLevel)
        {
            string key = DateKey(date);
            int seed = DeterministicRandom.Hash("shape-escape-daily-" + key);
            int dayIndex = (int)(date.Date - new DateTime(2024, 1, 1)).TotalDays;

            var tierValue = (int)BaseTierFor(date.DayOfWeek) + Math.Min(2, streak / 5);
            if (highestLevel < 5) tierValue = Math.Min(tierValue, (int)DifficultyTier.Easy);
            var tier = (DifficultyTier)Math.Min((int)DifficultyTier.Expert, tierValue);

            // Every third day features a daily-exclusive collectible; other days remix campaign objects.
            string objectId;
            if (dayIndex % 3 == 0)
            {
                var pool = ObjectCatalog.DailyExclusives;
                objectId = pool[((dayIndex / 3) % pool.Length + pool.Length) % pool.Length];
            }
            else
            {
                var pool = ObjectCatalog.CampaignOrder;
                objectId = pool[(Math.Abs(seed) % pool.Length)];
            }

            var info = TierInfo.Get(tier);
            return new LevelSpec
            {
                number = 0,
                objectId = objectId,
                variant = 0,
                tier = tier,
                targetPieces = (info.minPieces + info.maxPieces) / 2,
                seed = seed,
                mechanics = LevelCatalog.MechanicsUnlockedBy(highestLevel, tier),
                isDaily = true,
                dailyDate = key
            };
        }

        /// <summary>Coins for completing the daily challenge with the given (new) streak.</summary>
        public static int RewardFor(int streak)
        {
            return 50 + Math.Min(10, Math.Max(0, streak - 1)) * 10;
        }
    }
}
