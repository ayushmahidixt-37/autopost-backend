using UnityEngine;
using UnityEngine.UI;

namespace ShapeEscape
{
    /// <summary>Coin balance pill that animates changes.</summary>
    public sealed class CoinPill : MonoBehaviour
    {
        Text _text;
        RectTransform _icon;
        GameManager _game;
        float _shown;
        int _target;
        float _bump;

        public static CoinPill Create(Transform parent, GameManager game)
        {
            var bg = UIFactory.Panel(parent, UITheme.Card, 40, "CoinPill");
            bg.raycastTarget = false;
            var pill = bg.gameObject.AddComponent<CoinPill>();
            pill._game = game;
            var icon = UIFactory.IconImage(bg.transform, Icon.Coin, UITheme.Gold, 72);
            UIFactory.Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(72, 72), new Vector2(14, 0), new Vector2(0, 0.5f));
            pill._icon = icon.rectTransform;
            pill._text = UIFactory.Label(bg.transform, "0", UITheme.BodySize + 4, UITheme.Ink, FontStyle.Bold, TextAnchor.MiddleRight);
            UIFactory.Stretch(pill._text.rectTransform, 92, 28, 0, 0);
            pill._target = game.Save.Data.coins;
            pill._shown = pill._target;
            pill._text.text = pill._target.ToString();
            game.Progression.CoinsChanged += pill.OnCoins;
            return pill;
        }

        void OnDestroy()
        {
            if (_game != null && _game.Progression != null) _game.Progression.CoinsChanged -= OnCoins;
        }

        void OnCoins(int value)
        {
            if (value > _target) _bump = 1f;
            _target = value;
        }

        void Update()
        {
            if (_bump > 0)
            {
                _bump = Mathf.Max(0, _bump - Time.unscaledDeltaTime * 2.5f);
                _icon.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(_bump * Mathf.PI));
            }
            if (Mathf.Abs(_shown - _target) < 0.5f) return;
            _shown = Mathf.MoveTowards(_shown, _target, Mathf.Max(1f, Mathf.Abs(_target - _shown)) * Time.unscaledDeltaTime * 4f);
            _text.text = Mathf.RoundToInt(_shown).ToString();
        }
    }

    /// <summary>Shared builders for screen headers.</summary>
    public static class ScreenParts
    {
        public static RectTransform Header(Transform parent, string title, UnityEngine.Events.UnityAction onBack, out Text titleText)
        {
            var bar = UIFactory.Strip(UIFactory.Rect("Header", parent), true, 170);
            var back = UIFactory.IconButton(bar, Icon.Back, UITheme.Card, UITheme.Ink, onBack, 120);
            UIFactory.Place(back.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(120, 120), new Vector2(40, 0), new Vector2(0, 0.5f));
            titleText = UIFactory.Label(bar, title, UITheme.HeadingSize, UITheme.Ink, FontStyle.Bold);
            UIFactory.Stretch(titleText.rectTransform, 180, 180, 0, 0);
            return bar;
        }

        public static Text Title(Transform parent, string text, int size, Color color)
        {
            var t = UIFactory.Label(parent, text, size, color, FontStyle.Bold);
            var outline = t.gameObject.AddComponent<Outline>();
            outline.effectColor = UITheme.Accent;
            outline.effectDistance = new Vector2(4, -4);
            var shadow = t.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.3f, 0.2f, 0.5f, 0.35f);
            shadow.effectDistance = new Vector2(0, -10);
            return t;
        }

        public static Image StarsRow(Transform parent, int stars, float size, out Image[] images)
        {
            var row = UIFactory.Rect("Stars", parent);
            UIFactory.HStack(row, size * 0.15f);
            images = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                var s = UIFactory.IconImage(row, Icon.Star, i < stars ? UITheme.Gold : UITheme.Disabled, size);
                UIFactory.Layout(s, size, size);
                images[i] = s;
            }
            return null;
        }
    }
}
