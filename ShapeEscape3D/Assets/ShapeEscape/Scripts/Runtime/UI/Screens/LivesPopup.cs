using ShapeEscape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShapeEscape
{
    /// <summary>
    /// Lives status / "out of lives" popup: countdown to the next life plus the ways to get one now
    /// (rewarded ad with a daily cap, asking friends when a friends provider is connected, a coin refill).
    /// </summary>
    public sealed class LivesPopup : CardPopup
    {
        Text _count;
        Text _body;
        UIButton _ad;
        UIButton _friends;
        UIButton _refill;
        UIButton _close;
        RectTransform _stack;
        float _tick;

        protected override void Build()
        {
            BuildCard(900, 1080, "");
            CloseButton(() => UI.ClosePopup(this));
            var heart = UIFactory.IconImage(Card, Icon.Heart, UITheme.Primary, 150);
            UIFactory.Place(heart.rectTransform, new Vector2(0.5f, 1f), new Vector2(150, 150), new Vector2(0, -140), new Vector2(0.5f, 1f));
            _count = UIFactory.Label(heart.transform, "5", 64, Color.white, FontStyle.Bold);
            UIFactory.Stretch(_count.rectTransform, 0, 0, 0, 12);
            _body = UIFactory.Label(Card, "", UITheme.BodySize - 2, UITheme.InkSoft);
            UIFactory.Place(_body.rectTransform, new Vector2(0.5f, 1f), new Vector2(780, 150), new Vector2(0, -305), new Vector2(0.5f, 1f));

            _stack = UIFactory.Rect("Buttons", Card);
            UIFactory.Place(_stack, new Vector2(0.5f, 0f), new Vector2(760, 560), new Vector2(0, 40), new Vector2(0.5f, 0f));
            var v = UIFactory.VStack(_stack, 20, null, TextAnchor.LowerCenter);
            v.childForceExpandWidth = true;
            _ad = UIFactory.Button(_stack, "", UITheme.Accent, Color.white, WatchAd, Icon.Video, UITheme.BodySize - 4);
            UIFactory.Layout(_ad, 120);
            _friends = UIFactory.Button(_stack, Loc.T("ask_friends"), UITheme.Secondary, Color.white, AskFriends, Icon.Friends, UITheme.BodySize - 4);
            UIFactory.Layout(_friends, 120);
            _refill = UIFactory.Button(_stack, "", UITheme.Gold, Color.white, Refill, Icon.Coin, UITheme.BodySize - 4);
            UIFactory.Layout(_refill, 120);
            _close = UIFactory.Button(_stack, Loc.T("wait"), ColorUtil.Hex("#ECEAF5"), UITheme.Ink, () => UI.ClosePopup(this), null, UITheme.BodySize - 4);
            UIFactory.Layout(_close, 120);
        }

        protected override void OnShow()
        {
            Game.Analytics.Log(AnalyticsEvents.OutOfLives, "lives", Game.Lives.Lives);
            Refresh();
        }

        public override void Refresh()
        {
            if (_count == null) return;
            var lives = Game.Lives;
            bool full = lives.IsFull;
            _count.text = lives.Lives.ToString();
            TitleText.text = full ? Loc.T("lives_full") : lives.Lives == 0 ? Loc.T("out_of_lives") : Loc.F("lives_n", lives.Lives, EconomyRules.MaxLives);
            _body.text = full ? Loc.T("lives_full_body") : Loc.F("next_life_in", LivesManager.FormatCountdown(lives.TimeToNext));

            int adsLeft = Game.Ads.LifeAdsLeftToday;
            _ad.gameObject.SetActive(!full);
            _ad.SetLabel(Loc.F("ad_for_life", adsLeft));
            _ad.SetInteractable(adsLeft > 0 && Game.Ads.CanShowRewarded);
            _friends.gameObject.SetActive(!full && Game.Friends.IsAvailable);
            _refill.gameObject.SetActive(!full);
            _refill.SetLabel(Loc.F("refill_lives", EconomyRules.LivesRefillCost));
            _refill.SetInteractable(Game.Progression.Coins >= EconomyRules.LivesRefillCost);
            _close.SetLabel(full ? Loc.T("ok") : Loc.T("wait"));
        }

        void Update()
        {
            if (!IsVisible) return;
            _tick -= Time.unscaledDeltaTime;
            if (_tick > 0) return;
            _tick = 1f;
            Refresh();
        }

        void WatchAd()
        {
            _ad.SetInteractable(false);
            Game.Ads.ShowRewardedForLife(ok =>
            {
                if (ok)
                {
                    Game.Lives.AddLives(1, "rewarded_ad");
                    UI.Toast("+1 " + Loc.T("life"));
                }
                else UI.Toast(Loc.T("ad_unavailable"));
                Refresh();
            });
        }

        void AskFriends()
        {
            Game.Friends.AskForLives(ok => UI.Toast(ok ? Loc.T("asked_friends") : Loc.T("friends_unavailable")));
        }

        void Refill()
        {
            if (Game.Lives.RefillWithCoins()) UI.Toast(Loc.T("lives_refilled"));
            else UI.Toast(Loc.T("not_enough_coins"));
            Refresh();
        }
    }
}
