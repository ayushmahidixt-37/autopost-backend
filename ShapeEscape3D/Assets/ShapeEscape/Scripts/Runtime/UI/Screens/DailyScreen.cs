using System;
using ShapeEscape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShapeEscape
{
    /// <summary>Daily challenge hub: today's puzzle, reward, streak and history.</summary>
    public sealed class DailyScreen : UIScreen
    {
        Text _title;
        Text _date;
        Text _tier;
        Text _reward;
        Text _special;
        Text _streak;
        Text _best;
        UIButton _play;
        Text _done;
        RectTransform _history;

        protected override void Build()
        {
            ScreenParts.Header(Root, Loc.T("daily_challenge"), () => Game.CloseDaily(), out _title);

            var card = UIFactory.Card(Root, UITheme.Card, 56, "Today").rectTransform;
            UIFactory.Place(card, new Vector2(0.5f, 1f), new Vector2(960, 640), new Vector2(0, -200), new Vector2(0.5f, 1f));
            var cal = UIFactory.Panel(card, UITheme.Accent, 64, "IconBg");
            UIFactory.Place(cal.rectTransform, new Vector2(0.5f, 1f), new Vector2(128, 128), new Vector2(0, -36), new Vector2(0.5f, 1f));
            var ci = UIFactory.IconImage(cal.transform, Icon.Calendar, Color.white, 80);
            UIFactory.Place(ci.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(80, 80), Vector2.zero);
            _date = UIFactory.Label(card, "", UITheme.HeadingSize, UITheme.Ink, FontStyle.Bold);
            UIFactory.Place(_date.rectTransform, new Vector2(0.5f, 1f), new Vector2(900, 80), new Vector2(0, -180), new Vector2(0.5f, 1f));
            _tier = UIFactory.Label(card, "", UITheme.BodySize, UITheme.InkSoft, FontStyle.Bold);
            UIFactory.Place(_tier.rectTransform, new Vector2(0.5f, 1f), new Vector2(900, 60), new Vector2(0, -258), new Vector2(0.5f, 1f));

            var rewardRow = UIFactory.Rect("Reward", card);
            UIFactory.Place(rewardRow, new Vector2(0.5f, 1f), new Vector2(600, 80), new Vector2(0, -322), new Vector2(0.5f, 1f));
            UIFactory.HStack(rewardRow, 12);
            UIFactory.Layout(UIFactory.IconImage(rewardRow, Icon.Coin, UITheme.Gold, 64), 64, 64);
            _reward = UIFactory.Label(rewardRow, "", UITheme.BodySize + 6, UITheme.Ink, FontStyle.Bold);
            _reward.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIFactory.Layout(_reward, 70, 200);

            _special = UIFactory.Label(card, "", UITheme.SmallSize, UITheme.Accent, FontStyle.Bold);
            UIFactory.Place(_special.rectTransform, new Vector2(0.5f, 1f), new Vector2(900, 50), new Vector2(0, -405), new Vector2(0.5f, 1f));

            _play = UIFactory.Button(card, Loc.T("play"), UITheme.Primary, Color.white, () => Game.PlayDaily(), Icon.Play, 56);
            UIFactory.Place(_play.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(560, 150), new Vector2(0, 40), new Vector2(0.5f, 0f));
            _done = UIFactory.Label(card, "", UITheme.BodySize, UITheme.Success, FontStyle.Bold);
            UIFactory.Place(_done.rectTransform, new Vector2(0.5f, 0f), new Vector2(900, 150), new Vector2(0, 40), new Vector2(0.5f, 0f));

            // Streak strip
            var streak = UIFactory.Panel(Root, UITheme.CardSoft, 40, "Streak");
            UIFactory.Place(streak.rectTransform, new Vector2(0.5f, 1f), new Vector2(960, 130), new Vector2(0, -870), new Vector2(0.5f, 1f));
            var flame = UIFactory.IconImage(streak.transform, Icon.Flame, UITheme.Primary, 80);
            UIFactory.Place(flame.rectTransform, new Vector2(0, 0.5f), new Vector2(80, 80), new Vector2(40, 0), new Vector2(0, 0.5f));
            _streak = UIFactory.Label(streak.transform, "", UITheme.BodySize, UITheme.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
            UIFactory.Stretch(_streak.rectTransform, 150, 300, 0, 0);
            _best = UIFactory.Label(streak.transform, "", UITheme.SmallSize, UITheme.InkSoft, FontStyle.Normal, TextAnchor.MiddleRight);
            UIFactory.Stretch(_best.rectTransform, 500, 40, 0, 0);

            var historyTitle = UIFactory.Label(Root, Loc.T("history"), UITheme.BodySize, UITheme.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
            UIFactory.Place(historyTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(940, 70), new Vector2(0, -1020), new Vector2(0.5f, 1f));
            var scroll = UIFactory.ScrollArea(Root, out _history);
            UIFactory.Stretch(scroll.GetComponent<RectTransform>(), 60, 60, 1100, 30);
            UIFactory.VStack(_history, 14, new RectOffset(0, 0, 0, 30));
        }

        protected override void OnShow()
        {
            Refresh();
        }

        public override void Refresh()
        {
            if (_date == null) return;
            var daily = Game.Daily;
            var spec = daily.TodaySpec();
            var template = ObjectCatalog.Get(spec.objectId);
            _title.text = Loc.T("daily_challenge");
            _date.text = Loc.T("today") + " · " + DateTime.Now.ToString("d MMM", System.Globalization.CultureInfo.CurrentCulture);
            _tier.text = Loc.T("tier_" + spec.tier);
            _reward.text = "+" + daily.NextReward;
            _special.text = template != null && template.dailyExclusive ? Loc.T("special_collectible") : "";
            bool done = daily.CompletedToday;
            _play.gameObject.SetActive(!done);
            _play.SetLabel(Loc.T("play"));
            _done.gameObject.SetActive(done);
            _done.text = Loc.T("done_today") + "\n" + Loc.T("come_back_tomorrow");
            _streak.text = Loc.F("streak_n", daily.CurrentStreak);
            _best.text = Loc.F("best_streak_n", daily.BestStreak);

            UIFactory.Clear(_history);
            var history = Game.Save.Data.daily.history;
            if (history.Count == 0)
            {
                var empty = UIFactory.Label(_history, Loc.T("no_history"), UITheme.SmallSize, UITheme.InkSoft);
                UIFactory.Layout(empty, 120);
            }
            foreach (var h in history)
            {
                var row = UIFactory.Panel(_history, UITheme.Card, 30, "Row");
                UIFactory.Layout(row, 110);
                var t = ObjectCatalog.Get(h.objectId);
                string name = t != null ? Loc.ObjectName(t.id, t.displayName) : h.objectId;
                var left = UIFactory.Label(row.transform, h.date + "   " + name, UITheme.SmallSize, UITheme.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
                UIFactory.Stretch(left.rectTransform, 30, 300, 0, 0);
                var right = UIFactory.Label(row.transform, FormatTime(h.seconds) + "   +" + h.coins, UITheme.SmallSize, UITheme.InkSoft, FontStyle.Normal, TextAnchor.MiddleRight);
                UIFactory.Stretch(right.rectTransform, 500, 30, 0, 0);
            }
        }

        static string FormatTime(float seconds)
        {
            int s = Mathf.RoundToInt(seconds);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        public override bool HandleBack()
        {
            Game.CloseDaily();
            return true;
        }
    }
}
