using ShapeEscape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShapeEscape
{
    /// <summary>In-level HUD: pause, level/object title, progress, pieces left, camera reset and hints.</summary>
    public sealed class GameplayScreen : UIScreen
    {
        Text _title;
        Text _subtitle;
        Text _remaining;
        UIProgressBar _progress;
        Text _hintBadge;
        UIButton _hint;
        RectTransform _tutorial;
        Text _tutorialText;
        CanvasGroup _tutorialGroup;
        float _tutorialTime;
        bool _tutorialVisible;

        protected override void Build()
        {
            var top = UIFactory.Strip(UIFactory.Rect("Top", Root), true, 260);
            var pause = UIFactory.IconButton(top, Icon.Pause, UITheme.Card, UITheme.Ink, () => Game.PauseGame(), 120);
            UIFactory.Place(pause.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(120, 120), new Vector2(40, -30), new Vector2(0, 1));

            _title = UIFactory.Label(top, "", UITheme.HeadingSize, UITheme.Ink, FontStyle.Bold);
            UIFactory.Place(_title.rectTransform, new Vector2(0.5f, 1), new Vector2(640, 72), new Vector2(0, -32), new Vector2(0.5f, 1));
            _subtitle = UIFactory.Label(top, "", UITheme.SmallSize, UITheme.InkSoft, FontStyle.Bold);
            UIFactory.Place(_subtitle.rectTransform, new Vector2(0.5f, 1), new Vector2(640, 50), new Vector2(0, -102), new Vector2(0.5f, 1));

            var remainingPill = UIFactory.Panel(top, UITheme.Card, 40, "Remaining");
            UIFactory.Place(remainingPill.rectTransform, new Vector2(1, 1), new Vector2(220, 100), new Vector2(-40, -40), new Vector2(1, 1));
            _remaining = UIFactory.Label(remainingPill.transform, "", UITheme.BodySize - 2, UITheme.Ink, FontStyle.Bold);
            UIFactory.Stretch(_remaining.rectTransform);

            var barHolder = UIFactory.Rect("ProgressHolder", top);
            UIFactory.Place(barHolder, new Vector2(0.5f, 1), new Vector2(700, 30), new Vector2(0, -175), new Vector2(0.5f, 1));
            _progress = UIFactory.ProgressBar(barHolder, UITheme.CardSoft, UITheme.Secondary, 30);
            UIFactory.Stretch(_progress.GetComponent<RectTransform>());

            var bottom = UIFactory.Strip(UIFactory.Rect("Bottom", Root), false, 220);
            var recenter = UIFactory.IconButton(bottom, Icon.Recenter, UITheme.Card, UITheme.Ink, () => Game.CameraRig.ResetView(), 140);
            UIFactory.Place(recenter.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(140, 140), new Vector2(50, 50), new Vector2(0, 0));

            _hint = UIFactory.IconButton(bottom, Icon.Bulb, UITheme.Gold, Color.white, () => Game.RequestHint(), 160);
            UIFactory.Place(_hint.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(160, 160), new Vector2(-50, 45), new Vector2(1, 0));
            _hintBadge = UIFactory.Badge(_hint.face.transform, "0", UITheme.Primary, 62);

            // Tutorial / tip banner above the bottom buttons.
            _tutorial = UIFactory.Rect("Tip", Root);
            UIFactory.Place(_tutorial, new Vector2(0.5f, 0), new Vector2(820, 130), new Vector2(0, 250), new Vector2(0.5f, 0));
            var tipBg = _tutorial.gameObject.AddComponent<Image>();
            tipBg.sprite = UIFactory.Rounded(40);
            tipBg.type = Image.Type.Sliced;
            tipBg.color = UITheme.Ink.WithAlpha(0.85f);
            tipBg.raycastTarget = false;
            _tutorialText = UIFactory.Label(_tutorial, "", UITheme.SmallSize + 2, Color.white);
            UIFactory.Stretch(_tutorialText.rectTransform, 30, 30, 8, 8);
            _tutorialGroup = _tutorial.gameObject.AddComponent<CanvasGroup>();
            _tutorialGroup.alpha = 0;
            _tutorialGroup.blocksRaycasts = false;

            Game.Hints.HintsChanged += n => UpdateHints();
        }

        protected override void OnShow()
        {
            Bind(Game.Levels.Current);
        }

        public void Bind(LevelSession s)
        {
            if (_title == null || s == null) return;
            if (s.IsDaily)
            {
                _title.text = Loc.T("daily_challenge");
                _subtitle.text = Loc.T("tier_" + s.Spec.tier);
            }
            else
            {
                _title.text = Loc.F("level_n", s.Spec.number);
                _subtitle.text = Loc.T("mystery") + "  ·  " + Loc.T("tier_" + s.Spec.tier);
            }
            UpdateProgress(s, true);
            UpdateHints();
        }

        public void UpdateProgress(LevelSession s, bool instant = false)
        {
            if (_remaining == null || s == null) return;
            var state = Game.Levels.Collision.State;
            int left = state != null ? state.RemainingCount : 0;
            int total = Mathf.Max(1, s.TotalPieces);
            _remaining.text = Loc.F("pieces_left", left);
            _progress.SetValue(1f - left / (float)total, instant);
        }

        void UpdateHints()
        {
            if (_hintBadge == null) return;
            int n = Game.Hints.Available;
            _hintBadge.text = n > 0 ? n.ToString() : "+";
        }

        public void ShowTip(string text, float seconds = 0f)
        {
            if (_tutorialText == null) return;
            _tutorialText.text = text;
            _tutorialVisible = true;
            _tutorialTime = seconds > 0 ? seconds : float.MaxValue;
        }

        public void HideTip()
        {
            _tutorialVisible = false;
        }

        void Update()
        {
            if (_tutorialGroup == null) return;
            if (_tutorialVisible && _tutorialTime < float.MaxValue)
            {
                _tutorialTime -= Time.unscaledDeltaTime;
                if (_tutorialTime <= 0) _tutorialVisible = false;
            }
            float target = _tutorialVisible ? 1f : 0f;
            _tutorialGroup.alpha = Mathf.MoveTowards(_tutorialGroup.alpha, target, Time.unscaledDeltaTime * 4f);
            if (_tutorialVisible)
                _tutorial.anchoredPosition = new Vector2(0, 250 + 6f * Mathf.Sin(Time.unscaledTime * 3f));
        }

        public override bool HandleBack()
        {
            Game.PauseGame();
            return true;
        }
    }
}
