using System.Collections.Generic;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Ad unit ids and frequency rules. Defaults are Google's public TEST ad units, which are safe to click
    /// during development. Before release create an asset at Resources/ShapeEscape/AdSettings with your real
    /// unit ids (and set your real AdMob App ID in Assets > Google Mobile Ads > Settings).
    /// Development builds always use test units when <see cref="forceTestAdsInDevelopment"/> is on.
    /// </summary>
    [CreateAssetMenu(menuName = "Shape Escape/Ad Settings", fileName = "AdSettings")]
    public sealed class AdSettings : ScriptableObject
    {
        public const string ResourcePath = "ShapeEscape/AdSettings";

        // Google sample ad units: https://developers.google.com/admob/android/test-ads
        public const string TestRewardedAndroid = "ca-app-pub-3940256099942544/5224354917";
        public const string TestInterstitialAndroid = "ca-app-pub-3940256099942544/1033173712";
        public const string TestRewardedIos = "ca-app-pub-3940256099942544/1712485313";
        public const string TestInterstitialIos = "ca-app-pub-3940256099942544/4411468910";

        [Header("Ad unit ids (production)")]
        public string androidRewardedId = TestRewardedAndroid;
        public string androidInterstitialId = TestInterstitialAndroid;
        public string iosRewardedId = TestRewardedIos;
        public string iosInterstitialId = TestInterstitialIos;

        [Header("Safety")]
        [Tooltip("Development builds and the editor always use Google's test ad units.")]
        public bool forceTestAdsInDevelopment = true;
        [Tooltip("Hashed device ids from logcat ('Use RequestConfiguration.Builder().setTestDeviceIds').")]
        public List<string> testDeviceIds = new List<string>();

        [Header("Interstitial pacing (only ever shown between levels)")]
        [Tooltip("Interstitials start after the player finishes this campaign level.")]
        public int firstInterstitialLevel = ShapeEscape.Core.EconomyRules.InterstitialFromLevel;
        [Tooltip("Show one interstitial every N completed levels.")]
        public int levelsBetweenInterstitials = ShapeEscape.Core.EconomyRules.InterstitialEveryLevels;
        public float minSecondsBetweenInterstitials = 150f;
        [Tooltip("No interstitial within this many seconds after a rewarded ad.")]
        public float secondsAfterRewarded = 120f;

        [Header("Rewarded limits")]
        public int rewardedDailyCap = 15;
        public int hintAdsPerDay = ShapeEscape.Core.EconomyRules.AdHintsPerDay;
        public int lifeAdsPerDay = ShapeEscape.Core.EconomyRules.AdLivesPerDay;
        public float minSecondsBetweenRewarded = 10f;

        static AdSettings _instance;

        public static AdSettings Load()
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<AdSettings>(ResourcePath);
            if (_instance == null) _instance = CreateInstance<AdSettings>();
            return _instance;
        }

        bool UseTestUnits { get { return forceTestAdsInDevelopment && (Debug.isDebugBuild || Application.isEditor); } }

        public string RewardedId
        {
            get
            {
#if UNITY_IOS
                return UseTestUnits ? TestRewardedIos : iosRewardedId;
#else
                return UseTestUnits ? TestRewardedAndroid : androidRewardedId;
#endif
            }
        }

        public string InterstitialId
        {
            get
            {
#if UNITY_IOS
                return UseTestUnits ? TestInterstitialIos : iosInterstitialId;
#else
                return UseTestUnits ? TestInterstitialAndroid : androidInterstitialId;
#endif
            }
        }
    }
}
