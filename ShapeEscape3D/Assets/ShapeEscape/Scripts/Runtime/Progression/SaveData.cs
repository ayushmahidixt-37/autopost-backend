using System;
using System.Collections.Generic;

namespace ShapeEscape
{
    /// <summary>All persistent player state (JsonUtility-serialisable: fields only, lists instead of dictionaries).</summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int coins = 100;
        /// <summary>Hints in stock. New players get EconomyRules.FreeHints once; more only via rewarded ads.</summary>
        public int hints = ShapeEscape.Core.EconomyRules.FreeHints;
        public int lives = ShapeEscape.Core.EconomyRules.MaxLives;
        /// <summary>UTC ticks when the oldest missing life started refilling (0 = full).</summary>
        public long livesRegenStartTicks;
        /// <summary>Highest campaign level the player may play (1-based).</summary>
        public int highestUnlockedLevel = 1;
        public List<LevelRecord> levels = new List<LevelRecord>();
        public List<ObjectRecord> objects = new List<ObjectRecord>();
        public SettingsData settings = new SettingsData();
        public DailyData daily = new DailyData();
        public LoginData login = new LoginData();
        public AdsData ads = new AdsData();
        public List<string> seenTutorials = new List<string>();
        public long firstLaunchUtcTicks;
        public long lastSaveUtcTicks;
        public int sessions;
        public int totalLevelsCompleted;
        /// <summary>Monotonic counter used to resolve cloud-save conflicts.</summary>
        public long progressStamp;

        [Serializable]
        public sealed class LevelRecord
        {
            public int number;
            public int stars;
            public float bestSeconds;
            public int completions;
        }

        [Serializable]
        public sealed class ObjectRecord
        {
            public string id;
            /// <summary>Bit i set = difficulty variation i completed (daily objects use bit 0).</summary>
            public int variantsMask;
            public long unlockedUtcTicks;
        }

        [Serializable]
        public sealed class SettingsData
        {
            public bool musicOn = true;
            public bool sfxOn = true;
            public bool vibrationOn = true;
            public float musicVolume = 0.8f;
            public float sfxVolume = 1f;
            public string language = "";
            /// <summary>0 = auto, 1 = smooth (60 FPS), 2 = battery saver (30 FPS).</summary>
            public int qualityMode;
        }

        [Serializable]
        public sealed class DailyData
        {
            public string lastCompletedDate = "";
            public int streak;
            public int bestStreak;
            public List<DailyRecord> history = new List<DailyRecord>();
        }

        [Serializable]
        public sealed class DailyRecord
        {
            public string date;
            public string objectId;
            public float seconds;
            public int coins;
            public int streak;
        }

        [Serializable]
        public sealed class LoginData
        {
            public string lastClaimDate = "";
            /// <summary>Next reward day in the 7-day cycle (0..6).</summary>
            public int dayIndex;
        }

        [Serializable]
        public sealed class AdsData
        {
            public bool adsRemoved;
            public int levelsSinceInterstitial;
            public long lastInterstitialUtcTicks;
            public string rewardedDate = "";
            public int rewardedToday;
            public int hintAdsToday;
            public int lifeAdsToday;
            public int rewardedTotal;
            public int interstitialTotal;
        }

        public LevelRecord GetLevel(int number)
        {
            foreach (var l in levels) if (l.number == number) return l;
            return null;
        }

        public ObjectRecord GetObject(string id)
        {
            foreach (var o in objects) if (o.id == id) return o;
            return null;
        }

        /// <summary>Repairs data from older versions / corrupted fields.</summary>
        public void Sanitize()
        {
            if (levels == null) levels = new List<LevelRecord>();
            if (objects == null) objects = new List<ObjectRecord>();
            if (settings == null) settings = new SettingsData();
            if (daily == null) daily = new DailyData();
            if (daily.history == null) daily.history = new List<DailyRecord>();
            if (login == null) login = new LoginData();
            if (ads == null) ads = new AdsData();
            if (seenTutorials == null) seenTutorials = new List<string>();
            if (highestUnlockedLevel < 1) highestUnlockedLevel = 1;
            if (coins < 0) coins = 0;
            if (hints < 0) hints = 0;
            if (lives < 0) lives = 0;
            if (lives > ShapeEscape.Core.EconomyRules.MaxLives) lives = ShapeEscape.Core.EconomyRules.MaxLives;
            settings.musicVolume = Math.Max(0f, Math.Min(1f, settings.musicVolume));
            settings.sfxVolume = Math.Max(0f, Math.Min(1f, settings.sfxVolume));
            version = CurrentVersion;
        }
    }
}
