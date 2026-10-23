using System.Collections;
using ShapeEscape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShapeEscape
{
    /// <summary>Celebration page shown over the revealed 3D object: name, stars, coins, continue, optional ad bonus.</summary>
    public sealed class LevelCompleteScreen : UIScreen
    {
        Text _headline;
        Text _objectName;
        Image _newChip;
        Text _newChipText;
        Image[] _stars;
        Text _coins;
        Text _extra;
        UIButton _continue;
        UIButton _double;
        RewardSummary _summary;
        RectTransform _card;
        bool _doubled;

        protected override void Build()
        {
            var top = UIFactory.Rect("Top", Root);
            UIFactory.Place(top, new Vector2(0.5f, 1f), new Vector2(1000, 400), new Vector2(0, -60), new Vector2(0.5f, 1f));
            _headline = ScreenParts.Title(top, "", 104, Color.white);
            UIFactory.Place(_headline.rectTransform, new Vector2(0.5f, 1f), new Vector2(1000, 140), Vector2.zero, new Vector2(0.5f, 1f));
            _objectName = UIFactory.Label(top, "", 68, UITheme.Ink, FontStyle.Bold);
            UIFactory.Place(_objectName.rectTransform, new Vector2(0.5f, 1f), new Vector2(1000, 90), new Vector2(0, -150), new Vector2(0.5f, 1f));
            _newChip = UIFactory.Panel(top, UITheme.Accent, 32, "NewChip");
            UIFactory.Place(_newChip.rectTransform, new Vector2(0.5f, 1f), new Vector2(520, 76), new Vector2(0, -255), new Vector2(0.5f, 1f));
            _newChipText = UIFactory.Label(_newChip.transform, "", UITheme.SmallSize + 2, Color.white, FontStyle.Bold);
            UIFactory.Stretch(_newChipText.rectTransform);

            var cardImg = UIFactory.Card(Root, UITheme.Card, 56, "Card");
            _card = cardImg.rectTransform;
            UIFactory.Place(_card, new Vector2(0.5f, 0f), new Vector2(940, 620), new Vector2(0, 50), new Vector2(0.5f, 0f));

            var starsHolder = UIFactory.Rect("StarsHolder", _card);
            UIFactory.Place(starsHolder, new Vector2(0.5f, 1f), new Vector2(500, 130), new Vector2(0, -40), new Vector2(0.5f, 1f));
            ScreenParts.StarsRow(starsHolder, 0, 120, out _stars);
            UIFactory.Stretch((RectTransform)starsHolder.GetChild(0));

            var coinRow = UIFactory.Rect("Coins", _card);
            UIFactory.Place(coinRow, new Vector2(0.5f, 1f), new Vector2(500, 90), new Vector2(0, -185), new Vector2(0.5f, 1f));
            UIFactory.HStack(coinRow, 16);
            var ci = UIFactory.IconImage(coinRow, Icon.Coin, UITheme.Gold, 80);
            UIFactory.Layout(ci, 80, 80);
            _coins = UIFactory.Label(coinRow, "", 64, UITheme.Ink, FontStyle.Bold);
            _coins.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIFactory.Layout(_coins, 90, 220);

            _extra = UIFactory.Label(_card, "", UITheme.SmallSize, UITheme.InkSoft);
            UIFactory.Place(_extra.rectTransform, new Vector2(0.5f, 1f), new Vector2(860, 60), new Vector2(0, -285), new Vector2(0.5f, 1f));

            _double = UIFactory.Button(_card, Loc.T("double_coins"), UITheme.Accent, Color.white, OnDouble, Icon.Video, 40, 44, "Double");
            UIFactory.Place(_double.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(560, 120), new Vector2(0, 200), new Vector2(0.5f, 0f));
            _continue = UIFactory.Button(_card, Loc.T("continue"), UITheme.Primary, Color.white, () => Game.ContinueAfterComplete(), null, 52, 56, "Continue");
            UIFactory.Place(_continue.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(600, 150), new Vector2(0, 40), new Vector2(0.5f, 0f));
        }

        public void Setup(LevelSession session, RewardSummary summary)
        {
            if (_headline == null) return;
            _summary = summary;
            _doubled = false;
            _headline.text = Loc.T("complete_" + Random.Range(1, 5));
            _objectName.text = Loc.ObjectName(session.Template.id, session.Template.displayName);
            bool showChip = summary.newObject;
            _newChip.gameObject.SetActive(showChip);
            if (showChip) _newChipText.text = Loc.F("new_in_room", Loc.T("room_" + session.Template.category));

            var extra = new System.Text.StringBuilder();
            if (summary.isDaily) extra.Append(Loc.F("streak_n", summary.dailyStreak));
            if (summary.bonusHints > 0)
            {
                if (extra.Length > 0) extra.Append("  ·  ");
                extra.Append(Loc.F("bonus_hint", summary.bonusHints));
            }
            if (summary.campaignFinished)
            {
                if (extra.Length > 0) extra.Append("\n");
                extra.Append(Loc.T("campaign_done"));
            }
            _extra.text = extra.ToString();
            _double.SetLabel(Loc.T("double_coins"));
            _continue.SetLabel(Loc.T("continue"));
            _double.gameObject.SetActive(summary.coins > 0 && Game.Ads.CanShowRewarded);
            StartCoroutine(Animate(summary));
        }

        IEnumerator Animate(RewardSummary summary)
        {
            foreach (var s in _stars)
            {
                s.color = UITheme.Disabled;
                s.rectTransform.localScale = Vector3.one;
            }
            _coins.text = "+0";
            _card.anchoredPosition = new Vector2(0, -700);
            float t = 0;
            while (t < 0.45f)
            {
                t += Time.unscaledDeltaTime;
                _card.anchoredPosition = new Vector2(0, Mathf.LerpUnclamped(-700, 50, Tween.EaseOutBack(Mathf.Clamp01(t / 0.45f))));
                yield return null;
            }
            for (int i = 0; i < summary.stars && i < 3; i++)
            {
                _stars[i].color = UITheme.Gold;
                Game.Audio.Play(Sfx.Reveal, 0f, 1f + i * 0.12f);
                float k = 0;
                while (k < 0.25f)
                {
                    k += Time.unscaledDeltaTime;
                    _stars[i].rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(1.6f, 1f, Tween.EaseOutBack(Mathf.Clamp01(k / 0.25f)));
                    yield return null;
                }
            }
            yield return CountCoins(0, summary.coins);
        }

        IEnumerator CountCoins(int from, int to)
        {
            Game.Audio.Play(Sfx.Coin);
            float t = 0;
            while (t < 0.6f)
            {
                t += Time.unscaledDeltaTime;
                _coins.text = "+" + Mathf.RoundToInt(Mathf.Lerp(from, to, Tween.EaseOutCubic(Mathf.Clamp01(t / 0.6f))));
                yield return null;
            }
            _coins.text = "+" + to;
        }

        void OnDouble()
        {
            if (_doubled || _summary == null) return;
            _double.SetInteractable(false);
            Game.Ads.ShowRewarded("double_coins", earned =>
            {
                if (earned)
                {
                    _doubled = true;
                    int bonus = _summary.coins;
                    Game.Progression.AddCoins(bonus, "ad_double");
                    StartCoroutine(CountCoins(_summary.coins, _summary.coins + bonus));
                    _double.gameObject.SetActive(false);
                }
                else
                {
                    _double.SetInteractable(true);
                    UI.Toast(Loc.T("ad_unavailable"));
                }
            });
        }

        public override bool HandleBack()
        {
            Game.ContinueAfterComplete();
            return true;
        }
    }
}
