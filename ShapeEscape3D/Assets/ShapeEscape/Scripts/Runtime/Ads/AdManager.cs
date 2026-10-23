using System;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Modular ad manager enforcing the game's player-friendly ad policy:
    ///  • Rewarded ads are always optional (hints, coin doubling) — a level never requires an ad.
    ///  • Interstitials only between levels, never during play, never in the first levels, frequency-capped,
    ///    and never right after a rewarded ad. Removed entirely by the "Remove Ads" purchase.
    ///  • Rewards are granted only from the SDK reward callback; a daily cap and cooldown limit abuse /
    ///    invalid traffic. No ads are requested before consent (UMP) allows it.
    /// </summary>
    public sealed class AdManager : MonoBehaviour
    {
        public event Action StateChanged;

        IAdProvider _provider;
        AdSettings _settings;
        GameManager _game;
        float _lastRewardedTime = -9999f;
        bool _showing;

        public bool AdsAllowed { get; private set; }
        public string ProviderName { get { return _provider != null ? _provider.Name : "none"; } }
        public bool PrivacyOptionsRequired { get { return _provider != null && _provider.PrivacyOptionsRequired; } }
        SaveData.AdsData Data { get { return _game.Save.Data.ads; } }

        public void Initialize(GameManager game)
        {
            _game = game;
            _settings = AdSettings.Load();
#if SE_ADMOB && !UNITY_EDITOR
            _provider = new AdMobAdProvider();
#else
            if (Application.isEditor || Debug.isDebugBuild)
                _provider = new SimulatedAdProvider((rewarded, done) => _game.UI.ShowSimulatedAd(rewarded, done));
            else
                _provider = new NoAdProvider();
#endif
            _provider.Impression += format =>
            {
                _game.Analytics.Log(AnalyticsEvents.AdImpression, "format", format, "network", _provider.Name);
            };
        }

        /// <summary>Starts consent gathering + SDK init. Call once the first screen is visible.</summary>
        public void StartConsentAndInit()
        {
            _provider.Initialize(_settings, this, allowed =>
            {
                AdsAllowed = allowed;
                if (StateChanged != null) StateChanged();
            });
        }

        // ---------------------------------------------------------------- rewarded

        void RollDailyCounter()
        {
            string today = ProgressionManager.Today;
            if (Data.rewardedDate != today)
            {
                Data.rewardedDate = today;
                Data.rewardedToday = 0;
                Data.hintAdsToday = 0;
                Data.lifeAdsToday = 0;
            }
        }

        public bool CanShowRewarded
        {
            get
            {
                if (_showing || _provider == null || !_provider.IsRewardedReady) return false;
                RollDailyCounter();
                if (Data.rewardedToday >= _settings.rewardedDailyCap) return false;
                return Time.unscaledTime - _lastRewardedTime >= _settings.minSecondsBetweenRewarded;
            }
        }

        /// <summary>Shows a rewarded ad. <paramref name="onResult"/> is true only if the SDK confirmed the reward.</summary>
        public void ShowRewarded(string placement, Action<bool> onResult)
        {
            if (!CanShowRewarded)
            {
                _game.Analytics.Log(AnalyticsEvents.AdFailed, "format", "rewarded", "placement", placement, "reason", "not_ready");
                if (onResult != null) onResult(false);
                return;
            }
            _showing = true;
            _game.Audio.SetMusicDucked(true);
            _provider.ShowRewarded(earned =>
            {
                _showing = false;
                _game.Audio.ApplySettings();
                _lastRewardedTime = Time.unscaledTime;
                if (earned)
                {
                    RollDailyCounter();
                    Data.rewardedToday++;
                    Data.rewardedTotal++;
                    _game.Save.MarkDirty();
                    _game.Analytics.Log(AnalyticsEvents.AdReward, "placement", placement, "network", _provider.Name);
                }
                if (onResult != null) onResult(earned);
                if (StateChanged != null) StateChanged();
            });
        }

        /// <summary>Rewarded ads left today for hints.</summary>
        public int HintAdsLeftToday
        {
            get { RollDailyCounter(); return Mathf.Max(0, _settings.hintAdsPerDay - Data.hintAdsToday); }
        }

        /// <summary>Rewarded ads left today for extra lives.</summary>
        public int LifeAdsLeftToday
        {
            get { RollDailyCounter(); return Mathf.Max(0, _settings.lifeAdsPerDay - Data.lifeAdsToday); }
        }

        /// <summary>Rewarded ad that grants a hint (counts against the daily hint-ad quota).</summary>
        public void ShowRewardedForHint(Action<bool> onResult)
        {
            if (HintAdsLeftToday <= 0) { if (onResult != null) onResult(false); return; }
            ShowRewarded("hint", ok =>
            {
                if (ok) { Data.hintAdsToday++; _game.Save.MarkDirty(); }
                if (onResult != null) onResult(ok);
            });
        }

        /// <summary>Rewarded ad that grants a life (counts against the daily life-ad quota).</summary>
        public void ShowRewardedForLife(Action<bool> onResult)
        {
            if (LifeAdsLeftToday <= 0) { if (onResult != null) onResult(false); return; }
            ShowRewarded("life", ok =>
            {
                if (ok) { Data.lifeAdsToday++; _game.Save.MarkDirty(); }
                if (onResult != null) onResult(ok);
            });
        }

        // ---------------------------------------------------------------- interstitial

        int _lastCompletedLevel;

        /// <summary>Call after each completed campaign level; counts towards interstitial pacing.</summary>
        public void NotifyLevelCompleted(int levelNumber)
        {
            _lastCompletedLevel = levelNumber;
            Data.levelsSinceInterstitial++;
            _game.Save.MarkDirty();
        }

        public bool ShouldShowInterstitial
        {
            get
            {
                if (_showing || Data.adsRemoved || _provider == null || !_provider.IsInterstitialReady) return false;
                if (_lastCompletedLevel < _settings.firstInterstitialLevel) return false;
                if (Data.levelsSinceInterstitial < _settings.levelsBetweenInterstitials) return false;
                double since = (DateTime.UtcNow - new DateTime(Data.lastInterstitialUtcTicks, DateTimeKind.Utc)).TotalSeconds;
                if (Data.lastInterstitialUtcTicks > 0 && since < _settings.minSecondsBetweenInterstitials) return false;
                return Time.unscaledTime - _lastRewardedTime >= _settings.secondsAfterRewarded;
            }
        }

        /// <summary>Shows an interstitial only if every pacing rule allows it; <paramref name="then"/> always runs.</summary>
        public void MaybeShowInterstitial(string placement, Action then)
        {
            if (!ShouldShowInterstitial)
            {
                if (then != null) then();
                return;
            }
            _showing = true;
            _game.Audio.SetMusicDucked(true);
            _provider.ShowInterstitial(() =>
            {
                _showing = false;
                _game.Audio.ApplySettings();
                Data.levelsSinceInterstitial = 0;
                Data.lastInterstitialUtcTicks = DateTime.UtcNow.Ticks;
                Data.interstitialTotal++;
                _game.Save.MarkDirty();
                if (then != null) then();
            });
        }

        public void ShowPrivacyOptions(Action done)
        {
            if (_provider != null) _provider.ShowPrivacyOptions(done);
            else if (done != null) done();
        }

        public void SetAdsRemoved(bool removed)
        {
            Data.adsRemoved = removed;
            _game.Save.MarkDirty();
            if (StateChanged != null) StateChanged();
        }
    }
}
