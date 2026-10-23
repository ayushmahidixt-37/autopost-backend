using System;
using System.Collections;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>Abstraction over an ad network so AdManager's pacing rules are network-independent.</summary>
    public interface IAdProvider
    {
        string Name { get; }
        bool IsInitialized { get; }
        bool IsRewardedReady { get; }
        bool IsInterstitialReady { get; }
        /// <summary>True when the user must be offered a way to change their consent choices (GDPR).</summary>
        bool PrivacyOptionsRequired { get; }

        /// <summary>Gathers consent (UMP) and initialises the SDK. <paramref name="done"/> receives whether ads may be requested.</summary>
        void Initialize(AdSettings settings, MonoBehaviour host, Action<bool> done);
        void ShowRewarded(Action<bool> done);
        void ShowInterstitial(Action done);
        void ShowPrivacyOptions(Action done);

        event Action<string> Impression;
    }

    /// <summary>
    /// Editor / development stand-in that shows an obvious "TEST AD (simulated)" overlay. It is never used in
    /// release builds: without a real SDK, release builds simply report that no ads are available.
    /// </summary>
    public sealed class SimulatedAdProvider : IAdProvider
    {
        readonly Func<bool, Action<bool>, bool> _showOverlay;

        public SimulatedAdProvider(Func<bool, Action<bool>, bool> showOverlay)
        {
            _showOverlay = showOverlay;
        }

        public string Name { get { return "simulated"; } }
        public bool IsInitialized { get; private set; }
        public bool IsRewardedReady { get { return IsInitialized; } }
        public bool IsInterstitialReady { get { return IsInitialized; } }
        public bool PrivacyOptionsRequired { get { return false; } }
        public event Action<string> Impression;

        public void Initialize(AdSettings settings, MonoBehaviour host, Action<bool> done)
        {
            IsInitialized = true;
            if (done != null) done(true);
        }

        public void ShowRewarded(Action<bool> done)
        {
            if (Impression != null) Impression("rewarded");
            if (!_showOverlay(true, done) && done != null) done(false);
        }

        public void ShowInterstitial(Action done)
        {
            if (Impression != null) Impression("interstitial");
            if (!_showOverlay(false, _ => { if (done != null) done(); }) && done != null) done();
        }

        public void ShowPrivacyOptions(Action done) { if (done != null) done(); }
    }

    /// <summary>Release build without an ad SDK: no ads at all (rewarded buttons are hidden).</summary>
    public sealed class NoAdProvider : IAdProvider
    {
        public string Name { get { return "none"; } }
        public bool IsInitialized { get { return true; } }
        public bool IsRewardedReady { get { return false; } }
        public bool IsInterstitialReady { get { return false; } }
        public bool PrivacyOptionsRequired { get { return false; } }
        public event Action<string> Impression { add { } remove { } }
        public void Initialize(AdSettings settings, MonoBehaviour host, Action<bool> done) { if (done != null) done(false); }
        public void ShowRewarded(Action<bool> done) { if (done != null) done(false); }
        public void ShowInterstitial(Action done) { if (done != null) done(); }
        public void ShowPrivacyOptions(Action done) { if (done != null) done(); }
    }

#if SE_ADMOB
    /// <summary>
    /// Google Mobile Ads (AdMob) + User Messaging Platform consent. Requires the Google Mobile Ads Unity plugin
    /// (v9 or newer) and the SE_ADMOB scripting define symbol. Consent is gathered before any ad request,
    /// ads load with exponential back-off, and rewards are granted only from the SDK's reward callback.
    /// </summary>
    public sealed class AdMobAdProvider : IAdProvider
    {
        AdSettings _settings;
        MonoBehaviour _host;
        GoogleMobileAds.Api.RewardedAd _rewarded;
        GoogleMobileAds.Api.InterstitialAd _interstitial;
        int _rewardedRetry;
        int _interstitialRetry;
        bool _sdkStarted;

        public string Name { get { return "admob"; } }
        public bool IsInitialized { get; private set; }
        public bool IsRewardedReady { get { return _rewarded != null && _rewarded.CanShowAd(); } }
        public bool IsInterstitialReady { get { return _interstitial != null && _interstitial.CanShowAd(); } }
        public event Action<string> Impression;

        public bool PrivacyOptionsRequired
        {
            get
            {
                return GoogleMobileAds.Ump.Api.ConsentInformation.PrivacyOptionsRequirementStatus ==
                       GoogleMobileAds.Ump.Api.PrivacyOptionsRequirementStatus.Required;
            }
        }

        public void Initialize(AdSettings settings, MonoBehaviour host, Action<bool> done)
        {
            _settings = settings;
            _host = host;
            var request = new GoogleMobileAds.Ump.Api.ConsentRequestParameters();
            GoogleMobileAds.Ump.Api.ConsentInformation.Update(request, updateError =>
            {
                if (updateError != null) Debug.LogWarning("[Ads] Consent update failed: " + updateError.Message);
                GoogleMobileAds.Ump.Api.ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null) Debug.LogWarning("[Ads] Consent form error: " + formError.Message);
                    if (GoogleMobileAds.Ump.Api.ConsentInformation.CanRequestAds()) StartSdk(done);
                    else if (done != null) done(false);
                });
            });
            // Consent from a previous session lets us start the SDK immediately, in parallel.
            if (GoogleMobileAds.Ump.Api.ConsentInformation.CanRequestAds()) StartSdk(null);
        }

        void StartSdk(Action<bool> done)
        {
            if (_sdkStarted)
            {
                if (done != null) done(true);
                return;
            }
            _sdkStarted = true;
            GoogleMobileAds.Api.MobileAds.RaiseAdEventsOnUnityMainThread = true;
            GoogleMobileAds.Api.MobileAds.SetRequestConfiguration(new GoogleMobileAds.Api.RequestConfiguration
            {
                MaxAdContentRating = GoogleMobileAds.Api.MaxAdContentRating.PG,
                TestDeviceIds = _settings.testDeviceIds
            });
            GoogleMobileAds.Api.MobileAds.Initialize(status =>
            {
                IsInitialized = true;
                LoadRewarded();
                LoadInterstitial();
                if (done != null) done(true);
            });
        }

        void LoadRewarded()
        {
            if (_rewarded != null) { _rewarded.Destroy(); _rewarded = null; }
            GoogleMobileAds.Api.RewardedAd.Load(_settings.RewardedId, new GoogleMobileAds.Api.AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    _host.StartCoroutine(Retry(++_rewardedRetry, LoadRewarded));
                    return;
                }
                _rewardedRetry = 0;
                _rewarded = ad;
                ad.OnAdImpressionRecorded += () => { if (Impression != null) Impression("rewarded"); };
            });
        }

        void LoadInterstitial()
        {
            if (_interstitial != null) { _interstitial.Destroy(); _interstitial = null; }
            GoogleMobileAds.Api.InterstitialAd.Load(_settings.InterstitialId, new GoogleMobileAds.Api.AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    _host.StartCoroutine(Retry(++_interstitialRetry, LoadInterstitial));
                    return;
                }
                _interstitialRetry = 0;
                _interstitial = ad;
                ad.OnAdImpressionRecorded += () => { if (Impression != null) Impression("interstitial"); };
            });
        }

        static IEnumerator Retry(int attempt, Action load)
        {
            yield return new WaitForSecondsRealtime(Mathf.Min(120f, Mathf.Pow(2f, Mathf.Min(attempt, 7))));
            load();
        }

        public void ShowRewarded(Action<bool> done)
        {
            if (!IsRewardedReady)
            {
                if (done != null) done(false);
                return;
            }
            bool earned = false;
            var ad = _rewarded;
            ad.OnAdFullScreenContentClosed += () =>
            {
                if (done != null) done(earned);
                LoadRewarded();
            };
            ad.OnAdFullScreenContentFailed += error =>
            {
                if (done != null) done(false);
                LoadRewarded();
            };
            ad.Show(reward => earned = true);
        }

        public void ShowInterstitial(Action done)
        {
            if (!IsInterstitialReady)
            {
                if (done != null) done();
                return;
            }
            var ad = _interstitial;
            ad.OnAdFullScreenContentClosed += () =>
            {
                if (done != null) done();
                LoadInterstitial();
            };
            ad.OnAdFullScreenContentFailed += error =>
            {
                if (done != null) done();
                LoadInterstitial();
            };
            ad.Show();
        }

        public void ShowPrivacyOptions(Action done)
        {
            GoogleMobileAds.Ump.Api.ConsentForm.ShowPrivacyOptionsForm(error =>
            {
                if (error != null) Debug.LogWarning("[Ads] Privacy options error: " + error.Message);
                if (done != null) done();
            });
        }
    }
#endif
}
