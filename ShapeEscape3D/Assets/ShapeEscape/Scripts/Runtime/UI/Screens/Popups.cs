using System;
using System.Collections.Generic;
using ShapeEscape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShapeEscape
{
    /// <summary>Base for centred card popups.</summary>
    public abstract class CardPopup : UIScreen
    {
        protected RectTransform Card;
        protected Text TitleText;

        public override bool IsPopup { get { return true; } }

        protected void BuildCard(float width, float height, string title)
        {
            var img = UIFactory.Card(Root, UITheme.Card, 56, "Card");
            Card = img.rectTransform;
            UIFactory.Place(Card, new Vector2(0.5f, 0.5f), new Vector2(width, height), Vector2.zero);
            TitleText = UIFactory.Label(Card, title, UITheme.HeadingSize, UITheme.Ink, FontStyle.Bold);
            UIFactory.Place(TitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(width - 160, 90), new Vector2(0, -40), new Vector2(0.5f, 1f));
        }

        protected UIButton CloseButton(Action onClose)
        {
            var b = UIFactory.IconButton(Card, Icon.Close, ColorUtil.Hex("#F1EFF7"), UITheme.InkSoft, () => onClose(), 96);
            UIFactory.Place(b.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(96, 96), new Vector2(-30, -30), new Vector2(1, 1));
            return b;
        }
    }

    public sealed class DialogPopup : CardPopup
    {
        Text _message;
        RectTransform _buttons;
        Action _backAction;

        protected override void Build()
        {
            BuildCard(900, 640, "");
            _message = UIFactory.Label(Card, "", UITheme.BodySize, UITheme.InkSoft);
            UIFactory.Place(_message.rectTransform, new Vector2(0.5f, 1f), new Vector2(780, 260), new Vector2(0, -150), new Vector2(0.5f, 1f));
            _buttons = UIFactory.Rect("Buttons", Card);
            UIFactory.Place(_buttons, new Vector2(0.5f, 0f), new Vector2(800, 300), new Vector2(0, 40), new Vector2(0.5f, 0f));
            var v = UIFactory.VStack(_buttons, 24, null, TextAnchor.LowerCenter);
            v.childForceExpandWidth = true;
        }

        public void Setup(string title, string message, KeyValuePair<string, Action>[] buttons)
        {
            TitleText.text = title;
            _message.text = message;
            UIFactory.Clear(_buttons);
            _backAction = null;
            for (int i = 0; i < buttons.Length; i++)
            {
                var b = buttons[i];
                bool primary = i == 0;
                var btn = UIFactory.Button(_buttons, b.Key, primary ? UITheme.Primary : ColorUtil.Hex("#ECEAF5"), primary ? Color.white : UITheme.Ink,
                    () =>
                    {
                        UI.ClosePopup(this);
                        if (b.Value != null) b.Value();
                    }, null, UITheme.BodySize);
                UIFactory.Layout(btn, 130);
                if (i == buttons.Length - 1) _backAction = b.Value;
            }
            float h = 240 + Mathf.Max(1, buttons.Length) * 154 + 120;
            Card.sizeDelta = new Vector2(900, h);
        }

        public override bool HandleBack()
        {
            UI.ClosePopup(this);
            if (_backAction != null) _backAction();
            return true;
        }
    }

    public sealed class PausePopup : CardPopup
    {
        UISwitch _music;
        UISwitch _sound;

        protected override void Build()
        {
            BuildCard(860, 980, Loc.T("paused"));
            var stack = UIFactory.Rect("Stack", Card);
            UIFactory.Place(stack, new Vector2(0.5f, 1f), new Vector2(680, 760), new Vector2(0, -170), new Vector2(0.5f, 1f));
            UIFactory.VStack(stack, 28);
            UIFactory.Layout(UIFactory.Button(stack, Loc.T("resume"), UITheme.Primary, Color.white, () => Game.ResumeGame(), Icon.Play, 48), 150);
            UIFactory.Layout(UIFactory.Button(stack, Loc.T("restart"), UITheme.Secondary, Color.white, () => Game.RestartLevel(), Icon.Restart, 44), 130);
            UIFactory.Layout(UIFactory.Button(stack, Loc.T("home"), ColorUtil.Hex("#ECEAF5"), UITheme.Ink, () => Game.QuitToHome(true), Icon.Home, 44), 130);

            var toggles = UIFactory.Rect("Toggles", stack);
            UIFactory.Layout(toggles, 120);
            UIFactory.HStack(toggles, 30);
            var mi = UIFactory.IconImage(toggles, Icon.Music, UITheme.InkSoft, 64);
            UIFactory.Layout(mi, 64, 64);
            _music = UIFactory.Switch(toggles, Game.Save.Data.settings.musicOn, v => { Game.Save.Data.settings.musicOn = v; Game.SettingsChanged(); });
            var spacer = UIFactory.Rect("Spacer", toggles);
            UIFactory.Layout(spacer, 10, 50);
            var si = UIFactory.IconImage(toggles, Icon.Speaker, UITheme.InkSoft, 64);
            UIFactory.Layout(si, 64, 64);
            _sound = UIFactory.Switch(toggles, Game.Save.Data.settings.sfxOn, v => { Game.Save.Data.settings.sfxOn = v; Game.SettingsChanged(); });
        }

        protected override void OnShow()
        {
            _music.SetWithoutNotify(Game.Save.Data.settings.musicOn);
            _sound.SetWithoutNotify(Game.Save.Data.settings.sfxOn);
        }

        public override bool HandleBack()
        {
            Game.ResumeGame();
            return true;
        }
    }

    /// <summary>7-day login calendar.</summary>
    public sealed class LoginRewardPopup : CardPopup
    {
        readonly List<Image> _tiles = new List<Image>();
        readonly List<Text> _tileTexts = new List<Text>();
        Text _body;
        UIButton _claim;

        protected override void Build()
        {
            BuildCard(940, 1100, Loc.T("login_title"));
            var gift = UIFactory.IconImage(Card, Icon.Gift, UITheme.Primary, 130);
            UIFactory.Place(gift.rectTransform, new Vector2(0.5f, 1f), new Vector2(130, 130), new Vector2(0, -140), new Vector2(0.5f, 1f));
            _body = UIFactory.Label(Card, "", UITheme.SmallSize + 2, UITheme.InkSoft);
            UIFactory.Place(_body.rectTransform, new Vector2(0.5f, 1f), new Vector2(800, 110), new Vector2(0, -280), new Vector2(0.5f, 1f));

            var grid = UIFactory.Rect("Days", Card);
            UIFactory.Place(grid, new Vector2(0.5f, 1f), new Vector2(820, 420), new Vector2(0, -400), new Vector2(0.5f, 1f));
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(185, 195);
            g.spacing = new Vector2(20, 20);
            g.childAlignment = TextAnchor.UpperCenter;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 4;
            for (int i = 0; i < ProgressionManager.LoginRewards.Length; i++)
            {
                var tile = UIFactory.Panel(grid, ColorUtil.Hex("#F1EFF7"), 30, "Day" + (i + 1));
                var dayLabel = UIFactory.Label(tile.transform, (i + 1).ToString(), UITheme.SmallSize, UITheme.InkSoft, FontStyle.Bold);
                UIFactory.Place(dayLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(160, 46), new Vector2(0, -8), new Vector2(0.5f, 1f));
                var coin = UIFactory.IconImage(tile.transform, i == 6 ? Icon.Gift : Icon.Coin, UITheme.Gold, 70);
                UIFactory.Place(coin.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(70, 70), new Vector2(0, 0));
                var amount = UIFactory.Label(tile.transform, ProgressionManager.LoginRewards[i].ToString(), UITheme.SmallSize, UITheme.Ink, FontStyle.Bold);
                UIFactory.Place(amount.rectTransform, new Vector2(0.5f, 0f), new Vector2(160, 46), new Vector2(0, 8), new Vector2(0.5f, 0f));
                _tiles.Add(tile);
                _tileTexts.Add(amount);
            }

            _claim = UIFactory.Button(Card, Loc.T("claim"), UITheme.Primary, Color.white, Claim, Icon.Gift, 48);
            UIFactory.Place(_claim.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(560, 150), new Vector2(0, 50), new Vector2(0.5f, 0f));
        }

        protected override void OnShow()
        {
            int day = Game.Progression.PendingLoginDay;
            _body.text = Loc.F("login_body", day + 1);
            for (int i = 0; i < _tiles.Count; i++)
            {
                bool past = i < day, today = i == day;
                _tiles[i].color = today ? UITheme.Gold.WithAlpha(0.35f) : past ? UITheme.Success.WithAlpha(0.25f) : ColorUtil.Hex("#F1EFF7");
                _tiles[i].transform.localScale = today ? Vector3.one * 1.06f : Vector3.one;
            }
            _claim.SetInteractable(Game.Progression.LoginRewardAvailable);
        }

        void Claim()
        {
            bool hint;
            int coins = Game.Progression.ClaimLoginReward(out hint);
            if (coins > 0)
            {
                Game.Audio.Play(Sfx.Coin);
                Game.Haptics.Light();
                UI.Toast("+" + coins + (hint ? "  ·  " + Loc.F("bonus_hint", 1) : ""));
            }
            UI.ClosePopup(this);
        }
    }

    /// <summary>Introduces a special arrow type the first time it appears.</summary>
    public sealed class TutorialPopup : CardPopup
    {
        Text _body;
        Image _swatch;
        Image _icon;

        protected override void Build()
        {
            BuildCard(900, 900, "");
            var circle = UIFactory.Panel(Card, Color.white, 90, "Swatch");
            UIFactory.Place(circle.rectTransform, new Vector2(0.5f, 1f), new Vector2(180, 180), new Vector2(0, -160), new Vector2(0.5f, 1f));
            _swatch = circle;
            _icon = UIFactory.IconImage(circle.transform, Icon.Play, Color.white, 100);
            UIFactory.Place(_icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(100, 100), Vector2.zero);
            _body = UIFactory.Label(Card, "", UITheme.BodySize, UITheme.InkSoft);
            UIFactory.Place(_body.rectTransform, new Vector2(0.5f, 1f), new Vector2(780, 260), new Vector2(0, -380), new Vector2(0.5f, 1f));
            var ok = UIFactory.Button(Card, Loc.T("got_it"), UITheme.Primary, Color.white, () => { UI.ClosePopup(this); Game.ResumeAfterTutorial(); }, null, 48);
            UIFactory.Place(ok.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(520, 150), new Vector2(0, 50), new Vector2(0.5f, 0f));
        }

        public void Setup(ArrowKind kind)
        {
            string key = kind.ToString().ToLowerInvariant();
            TitleText.text = Loc.T("mech_" + key + "_title");
            _body.text = Loc.T("mech_" + key + "_body");
            Color c;
            Icon icon = Icon.Play;
            switch (kind)
            {
                case ArrowKind.Locked: c = ArrowPiece.CageColor; icon = Icon.Lock; break;
                case ArrowKind.Chain: c = ArrowPiece.ArrowChain; break;
                case ArrowKind.Rotating: c = ArrowPiece.ArrowRotating; icon = Icon.Restart; break;
                case ArrowKind.Hidden: c = UITheme.InkSoft; icon = Icon.Recenter; break;
                default: c = PuzzleBoardView.GateColor(0); icon = Icon.Grid; break;
            }
            _swatch.color = c;
            _icon.sprite = IconFactory.Get(icon);
        }

        public override bool HandleBack()
        {
            UI.ClosePopup(this);
            Game.ResumeAfterTutorial();
            return true;
        }
    }
}
