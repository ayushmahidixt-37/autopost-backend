using ShapeEscape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShapeEscape
{
    /// <summary>Home: logo, coins, settings, big Play button (the next puzzle spins behind the UI), daily and collection cards.</summary>
    public sealed class HomeScreen : UIScreen
    {
        Text _levelLabel;
        Text _dailyStatus;
        Text _collectionStatus;
        UIButton _play;
        Image _dailyDot;
        Text _subtitle;
        Text _playLabel;
        Text _dailyTitle;
        Text _collectionTitle;
        Text _lives;
        Text _livesTimer;
        float _time;
        float _tick;

        protected override void Build()
        {
            // Top bar
            var top = UIFactory.Strip(UIFactory.Rect("Top", Root), true, 170);
            var settings = UIFactory.IconButton(top, Icon.Gear, UITheme.Card, UITheme.Ink, () => Game.OpenSettings(), 120);
            UIFactory.Place(settings.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(120, 120), new Vector2(40, 0), new Vector2(0, 0.5f));
            // Lives: tap for the lives popup (ad / friends / refill / countdown).
            var lives = UIFactory.Button(top, null, UITheme.Card, UITheme.Ink, () => Game.ShowLives(), null, 40, 40, "Lives");
            UIFactory.Place(lives.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(280, 100), new Vector2(180, 0), new Vector2(0, 0.5f));
            var heart = UIFactory.IconImage(lives.face.transform, Icon.Heart, UITheme.Primary, 76);
            UIFactory.Place(heart.rectTransform, new Vector2(0, 0.5f), new Vector2(76, 76), new Vector2(14, 0), new Vector2(0, 0.5f));
            _lives = UIFactory.Label(heart.transform, "5", 36, Color.white, FontStyle.Bold);
            UIFactory.Stretch(_lives.rectTransform, 0, 0, 0, 6);
            _livesTimer = UIFactory.Label(lives.face.transform, "", UITheme.SmallSize, UITheme.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
            UIFactory.Stretch(_livesTimer.rectTransform, 104, 10, 0, 0);

            var coins = CoinPill.Create(top, Game);
            UIFactory.Place(coins.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(300, 100), new Vector2(-40, 0), new Vector2(1, 0.5f));

            // Logo
            var logo = UIFactory.Rect("Logo", Root);
            UIFactory.Place(logo, new Vector2(0.5f, 1f), new Vector2(1000, 330), new Vector2(0, -190), new Vector2(0.5f, 1f));
            var title = ScreenParts.Title(logo, "SHAPE ESCAPE", 108, Color.white);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(1000, 140), new Vector2(0, 0), new Vector2(0.5f, 1f));
            var badge = UIFactory.Panel(logo, UITheme.Primary, 34, "Badge3D");
            UIFactory.Place(badge.rectTransform, new Vector2(0.5f, 1f), new Vector2(150, 90), new Vector2(0, -150), new Vector2(0.5f, 1f));
            badge.rectTransform.localRotation = Quaternion.Euler(0, 0, 6f);
            var three = UIFactory.Label(badge.transform, "3D", 64, Color.white, FontStyle.Bold);
            UIFactory.Stretch(three.rectTransform);
            _subtitle = UIFactory.Label(logo, Loc.T("subtitle"), 46, UITheme.InkSoft, FontStyle.Bold);
            UIFactory.Place(_subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(900, 70), new Vector2(0, -255), new Vector2(0.5f, 1f));

            // Bottom: level chip + play button + cards
            var bottom = UIFactory.Rect("Bottom", Root);
            UIFactory.Place(bottom, new Vector2(0.5f, 0f), new Vector2(1000, 760), new Vector2(0, 40), new Vector2(0.5f, 0f));

            // Level chip doubles as the entrance to the level map.
            var chip = UIFactory.Button(bottom, null, UITheme.CardSoft, UITheme.Ink, () => Game.OpenMap(), null, 40, 30, "LevelChip");
            UIFactory.Place(chip.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(620, 84), new Vector2(0, 0), new Vector2(0.5f, 1f));
            var mapIcon = UIFactory.IconImage(chip.face.transform, Icon.Map, UITheme.Accent, 52);
            UIFactory.Place(mapIcon.rectTransform, new Vector2(1, 0.5f), new Vector2(52, 52), new Vector2(-24, 0), new Vector2(1, 0.5f));
            _levelLabel = UIFactory.Label(chip.face.transform, "", UITheme.BodySize - 2, UITheme.Ink, FontStyle.Bold);
            UIFactory.Stretch(_levelLabel.rectTransform, 20, 80, 0, 0);

            _play = UIFactory.Button(bottom, Loc.T("play"), UITheme.Primary, Color.white, () => Game.PlayNextLevel(), Icon.Play, 72, 70, "Play");
            UIFactory.Place(_play.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(640, 190), new Vector2(0, -110), new Vector2(0.5f, 1f));
            _playLabel = _play.label;

            var daily = UIFactory.Button(bottom, null, UITheme.Card, UITheme.Ink, () => Game.OpenDaily(), null, 40, 44, "DailyCard");
            UIFactory.Place(daily.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(480, 300), new Vector2(10, 20), new Vector2(0f, 0f));
            BuildCard(daily, Icon.Calendar, UITheme.Accent, out _dailyTitle, out _dailyStatus);
            _dailyDot = UIFactory.Panel(daily.face.transform, UITheme.Primary, 18, "Dot");
            UIFactory.Place(_dailyDot.rectTransform, new Vector2(1, 1), new Vector2(36, 36), new Vector2(-24, -24), new Vector2(1, 1));

            var coll = UIFactory.Button(bottom, null, UITheme.Card, UITheme.Ink, () => Game.OpenCollection(), null, 40, 44, "CollectionCard");
            UIFactory.Place(coll.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(480, 300), new Vector2(-10, 20), new Vector2(1f, 0f));
            BuildCard(coll, Icon.Grid, UITheme.Secondary, out _collectionTitle, out _collectionStatus);
        }

        static void BuildCard(UIButton b, Icon icon, Color tint, out Text title, out Text status)
        {
            var face = b.face.transform;
            var circle = UIFactory.Panel(face, tint, 56, "IconBg");
            UIFactory.Place(circle.rectTransform, new Vector2(0.5f, 1f), new Vector2(112, 112), new Vector2(0, -30), new Vector2(0.5f, 1f));
            var ic = UIFactory.IconImage(circle.transform, icon, Color.white, 70);
            UIFactory.Place(ic.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(70, 70), Vector2.zero);
            title = UIFactory.Label(face, "", UITheme.BodySize + 4, UITheme.Ink, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0f), new Vector2(440, 60), new Vector2(0, 92), new Vector2(0.5f, 0f));
            status = UIFactory.Label(face, "", UITheme.SmallSize, UITheme.InkSoft);
            UIFactory.Place(status.rectTransform, new Vector2(0.5f, 0f), new Vector2(440, 60), new Vector2(0, 36), new Vector2(0.5f, 0f));
        }

        protected override void OnShow()
        {
            Refresh();
        }

        public override void Refresh()
        {
            if (_levelLabel == null) return;
            _subtitle.text = Loc.T("subtitle");
            _playLabel.text = Loc.T("play");
            int next = Game.Progression.NextLevel;
            var spec = LevelProvider.GetSpec(next);
            _levelLabel.text = Loc.F("level_n", next) + (spec != null ? "  ·  " + (spec.superHard ? Loc.T("super_hard") : spec.hard ? Loc.T("hard") : Loc.T("tier_" + spec.tier)) : "");
            UpdateLives();

            _dailyTitle.text = Loc.T("daily");
            var daily = Game.Daily;
            if (daily.CompletedToday)
            {
                _dailyStatus.text = Loc.T("done_today") + " · " + Loc.F("streak_n", daily.CurrentStreak);
                _dailyDot.gameObject.SetActive(false);
            }
            else
            {
                _dailyStatus.text = daily.CurrentStreak > 0 ? Loc.F("streak_n", daily.CurrentStreak) : Loc.F("reward_n", daily.NextReward);
                _dailyDot.gameObject.SetActive(true);
            }

            _collectionTitle.text = Loc.T("collection");
            _collectionStatus.text = Game.Collection.UnlockedCount + " / " + Game.Collection.TotalCount;
        }

        void UpdateLives()
        {
            var lives = Game.Lives;
            _lives.text = lives.Lives.ToString();
            _livesTimer.text = lives.IsFull ? Loc.T("full") : LivesManager.FormatCountdown(lives.TimeToNext);
        }

        void Update()
        {
            if (_play == null) return;
            _tick -= Time.unscaledDeltaTime;
            if (_tick <= 0) { _tick = 1f; UpdateLives(); }
            // Gentle breathing on the Play button.
            _time += Time.unscaledDeltaTime;
            float s = 1f + 0.025f * Mathf.Sin(_time * 3f);
            _play.face.rectTransform.localScale = new Vector3(s, s, 1f);
        }

        public override bool HandleBack()
        {
            return false; // GameManager decides (quit confirmation)
        }
    }
}
