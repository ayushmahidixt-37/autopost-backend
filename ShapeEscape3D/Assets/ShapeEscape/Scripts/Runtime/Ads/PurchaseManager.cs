using System;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>Store abstraction for the single non-consumable "remove_ads" product.</summary>
    public interface IPurchaseProvider
    {
        bool IsAvailable { get; }
        string RemoveAdsPrice { get; }
        void BuyRemoveAds(Action<bool> done);
        void RestorePurchases(Action<bool> done);
    }

    /// <summary>
    /// Default: purchases unavailable, so the Remove Ads / Restore buttons stay hidden. To enable, implement
    /// IPurchaseProvider with Unity IAP (com.unity.purchasing) or Google Play Billing and register it via
    /// PurchaseManager.SetProvider before the settings screen opens. See Docs/MONETIZATION.md.
    /// </summary>
    public sealed class UnavailablePurchaseProvider : IPurchaseProvider
    {
        public bool IsAvailable { get { return false; } }
        public string RemoveAdsPrice { get { return ""; } }
        public void BuyRemoveAds(Action<bool> done) { if (done != null) done(false); }
        public void RestorePurchases(Action<bool> done) { if (done != null) done(false); }
    }

    public sealed class PurchaseManager : MonoBehaviour
    {
        public const string RemoveAdsProductId = "remove_ads";

        IPurchaseProvider _provider = new UnavailablePurchaseProvider();
        GameManager _game;

        public bool IsAvailable { get { return _provider.IsAvailable; } }
        public string RemoveAdsPrice { get { return _provider.RemoveAdsPrice; } }

        public void Initialize(GameManager game)
        {
            _game = game;
        }

        public void SetProvider(IPurchaseProvider provider)
        {
            _provider = provider ?? new UnavailablePurchaseProvider();
        }

        public void BuyRemoveAds(Action<bool> done)
        {
            _provider.BuyRemoveAds(ok =>
            {
                if (ok) _game.Ads.SetAdsRemoved(true);
                if (done != null) done(ok);
            });
        }

        public void Restore(Action<bool> done)
        {
            _provider.RestorePurchases(ok =>
            {
                if (ok) _game.Ads.SetAdsRemoved(true);
                if (done != null) done(ok);
            });
        }
    }
}
