using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ShapeEscape
{
    public sealed class SettingsPopup : CardPopup
    {
        RectTransform _content;
        readonly List<UIButton> _gfxButtons = new List<UIButton>();

        protected override void Build()
        {
            BuildCard(960, 1560, Loc.T("settings"));
            CloseButton(() => UI.ClosePopup(this));
            var scroll = UIFactory.ScrollArea(Card, out _content);
            UIFactory.Stretch(scroll.GetComponent<RectTransform>(), 40, 40, 150, 40);
            UIFactory.VStack(_content, 18, new RectOffset(0, 0, 10, 30));
            Rebuild();
        }

        protected override void OnShow()
        {
            Rebuild();
        }

        public override void Refresh()
        {
            if (_content != null && IsVisible) Rebuild();
        }

        void Rebuild()
        {
            if (_content == null) return;
            UIFactory.Clear(_content);
            _gfxButtons.Clear();
            TitleText.text = Loc.T("settings");
            var s = Game.Save.Data.settings;

            Row(Icon.Music, Loc.T("music"), row => UIFactory.Switch(row, s.musicOn, v => { s.musicOn = v; Game.SettingsChanged(); }));
            Row(null, "", row => UIFactory.SliderControl(row, s.musicVolume, v => { s.musicVolume = v; Game.SettingsChanged(); }));
            Row(Icon.Speaker, Loc.T("sound"), row => UIFactory.Switch(row, s.sfxOn, v => { s.sfxOn = v; Game.SettingsChanged(); }));
            Row(Icon.Vibrate, Loc.T("vibration"), row => UIFactory.Switch(row, s.vibrationOn, v => { s.vibrationOn = v; Game.SettingsChanged(); if (v) Game.Haptics.Medium(); }));

            Row(Icon.Gauge, Loc.T("graphics"), row =>
            {
                var group = UIFactory.Rect("Segments", row);
                UIFactory.HStack(group, 10);
                UIFactory.Layout(group, 96, 470);
                string[] keys = { "gfx_auto", "gfx_smooth", "gfx_battery" };
                for (int i = 0; i < 3; i++)
                {
                    int mode = i;
                    var b = UIFactory.Button(group, Loc.T(keys[i]), UITheme.Disabled, UITheme.Ink, () =>
                    {
                        Game.Quality.SetMode((QualityManager.Mode)mode);
                        UpdateGfxButtons();
                    }, null, UITheme.SmallSize - 2, 30);
                    UIFactory.Layout(b, 90, 150);
                    _gfxButtons.Add(b);
                }
                UpdateGfxButtons();
                return group;
            });

            Row(Icon.Globe, Loc.T("language"), row =>
            {
                int idx = Mathf.Max(0, System.Array.IndexOf(Loc.Languages, Loc.Current));
                var b = UIFactory.Button(row, Loc.LanguageNames[idx], UITheme.Secondary, Color.white, () =>
                {
                    int next = (System.Array.IndexOf(Loc.Languages, Loc.Current) + 1) % Loc.Languages.Length;
                    Game.SetLanguage(Loc.Languages[next]);
                }, null, UITheme.SmallSize, 36);
                UIFactory.Layout(b, 96, 330);
                return b;
            });

            Divider();
            LinkButton(Icon.Document, Loc.T("privacy_policy"), () => Application.OpenURL(GameConfig.PrivacyPolicyUrl));
            if (Game.Ads.PrivacyOptionsRequired)
                LinkButton(Icon.Shield, Loc.T("privacy_options"), () => Game.Ads.ShowPrivacyOptions(null));
            if (Game.Purchases.IsAvailable)
            {
                if (!Game.Save.Data.ads.adsRemoved)
                    LinkButton(Icon.Video, Loc.T("remove_ads") + (string.IsNullOrEmpty(Game.Purchases.RemoveAdsPrice) ? "" : "  " + Game.Purchases.RemoveAdsPrice),
                        () => Game.Purchases.BuyRemoveAds(ok => { if (!ok) UI.Toast(Loc.T("purchase_failed")); else Rebuild(); }));
                LinkButton(Icon.Restart, Loc.T("restore_purchases"),
                    () => Game.Purchases.Restore(ok => UI.Toast(ok ? Loc.T("purchase_restored") : Loc.T("purchase_failed"))));
            }

            var version = UIFactory.Label(_content, Loc.F("version", Application.version), UITheme.SmallSize - 4, UITheme.InkSoft);
            UIFactory.Layout(version, 70);
        }

        void UpdateGfxButtons()
        {
            int mode = (int)Game.Quality.CurrentMode;
            for (int i = 0; i < _gfxButtons.Count; i++)
                _gfxButtons[i].SetColors(i == mode ? UITheme.Accent : ColorUtil.Hex("#ECEAF5"), i == mode ? Color.white : UITheme.Ink);
        }

        void Row(Icon? icon, string label, System.Func<RectTransform, Component> control)
        {
            var row = UIFactory.Rect("Row", _content);
            UIFactory.Layout(row, 110);
            var h = UIFactory.HStack(row, 24, new RectOffset(10, 10, 0, 0), TextAnchor.MiddleLeft);
            h.childForceExpandWidth = false;
            if (icon.HasValue)
            {
                var ic = UIFactory.IconImage(row, icon.Value, UITheme.InkSoft, 64);
                UIFactory.Layout(ic, 64, 64);
            }
            if (!string.IsNullOrEmpty(label))
            {
                var t = UIFactory.Label(row, label, UITheme.BodySize, UITheme.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
                UIFactory.Layout(t, 80, 300, 1);
            }
            control(row);
        }

        void LinkButton(Icon icon, string label, UnityEngine.Events.UnityAction action)
        {
            var b = UIFactory.Button(_content, label, ColorUtil.Hex("#ECEAF5"), UITheme.Ink, action, icon, UITheme.BodySize - 4, 40);
            UIFactory.Layout(b, 120);
        }

        void Divider()
        {
            var d = UIFactory.Panel(_content, UITheme.Disabled.WithAlpha(0.5f), 2, "Divider");
            UIFactory.Layout(d, 4);
        }
    }

    /// <summary>Build-time constants that must be configured before release.</summary>
    public static class GameConfig
    {
        /// <summary>Replace with the URL where Docs/PRIVACY_POLICY.md is hosted (required by Google Play and AdMob).</summary>
        public const string PrivacyPolicyUrl = "https://example.com/shape-escape-3d/privacy-policy";
        public const string SupportEmail = "support@example.com";
    }
}
