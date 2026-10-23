using System.Collections.Generic;
using ShapeEscape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShapeEscape
{
    /// <summary>
    /// Candy-Crush-style level map: a winding path climbing from level 1 at the bottom, split into worlds,
    /// with stars on solved levels, a bouncing current level, and red / purple rings on Hard / Super Hard levels.
    /// </summary>
    public sealed class LevelMapScreen : UIScreen
    {
        const float NodeSize = 150f;
        const float Spacing = 230f;
        const float Margin = 220f;

        static readonly KeyValuePair<int, string>[] Worlds =
        {
            new KeyValuePair<int, string>(1, "world_1"),
            new KeyValuePair<int, string>(11, "world_2"),
            new KeyValuePair<int, string>(21, "world_3")
        };

        ScrollRect _scroll;
        RectTransform _content;
        Text _title;
        RectTransform _current;
        float _time;

        protected override void Build()
        {
            ScreenParts.Header(Root, Loc.T("level_map"), () => Game.GoHome(), out _title);
            _scroll = UIFactory.ScrollArea(Root, out _content);
            UIFactory.Stretch(_scroll.GetComponent<RectTransform>(), 0, 0, 180, 0);
            // Absolute layout: drop the auto-size fitter and size the content ourselves.
            var fitter = _content.GetComponent<ContentSizeFitter>();
            if (fitter != null) Destroy(fitter);
        }

        protected override void OnShow()
        {
            Rebuild();
        }

        public override void Refresh()
        {
            if (IsVisible) Rebuild();
        }

        Vector2 NodePos(int n, float width)
        {
            float x = Mathf.Sin(n * 0.85f) * width * 0.3f;
            int count = LevelProvider.LevelCount;
            float y = -(Margin + (count - n) * Spacing);
            return new Vector2(x, y);
        }

        void Rebuild()
        {
            _title.text = Loc.T("level_map");
            UIFactory.Clear(_content);
            int count = LevelProvider.LevelCount;
            float width = 1000f;
            _content.sizeDelta = new Vector2(0, Margin * 2 + (count - 1) * Spacing);
            int next = Game.Progression.NextLevel;

            // Path dots between nodes.
            for (int n = 1; n < count; n++)
            {
                Vector2 a = NodePos(n, width), b = NodePos(n + 1, width);
                for (int k = 1; k <= 4; k++)
                {
                    var dot = UIFactory.Panel(_content, UITheme.CardSoft, 12, "Dot");
                    dot.raycastTarget = false;
                    UIFactory.Place(dot.rectTransform, new Vector2(0.5f, 1f), new Vector2(22, 22), Vector2.Lerp(a, b, k / 5f));
                }
            }

            foreach (var w in Worlds)
            {
                if (w.Key > count) continue;
                var banner = UIFactory.Panel(_content, UITheme.Accent.WithAlpha(0.15f), 30, "World");
                banner.raycastTarget = false;
                UIFactory.Place(banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(860, 80), NodePos(w.Key, width) + new Vector2(0, -130));
                var t = UIFactory.Label(banner.transform, Loc.T(w.Value), UITheme.SmallSize + 2, UITheme.Accent, FontStyle.Bold);
                UIFactory.Stretch(t.rectTransform);
            }

            _current = null;
            for (int n = 1; n <= count; n++)
            {
                var spec = LevelProvider.GetSpec(n);
                int stars = Game.Progression.StarsFor(n);
                bool done = stars > 0, isNext = n == next, locked = n > next && !done;
                Color bg = done ? UITheme.Success : isNext ? UITheme.Gold : UITheme.Disabled;
                int level = n;
                var node = UIFactory.Button(_content, n.ToString(), bg, Color.white, locked ? null : (UnityEngine.Events.UnityAction)(() => Game.PlayLevel(level)),
                    null, 56, (int)(NodeSize / 2), "Level" + n);
                var rt = node.GetComponent<RectTransform>();
                UIFactory.Place(rt, new Vector2(0.5f, 1f), new Vector2(NodeSize, NodeSize), NodePos(n, width));
                if (locked) node.SetInteractable(false);

                if (spec != null && (spec.hard || spec.superHard))
                {
                    var ring = UIFactory.Panel(rt, Color.clear, (int)(NodeSize / 2) + 10, "Ring");
                    ring.raycastTarget = false;
                    UIFactory.Stretch(ring.rectTransform, -14, -14, -14, -14);
                    ring.transform.SetAsFirstSibling();
                    ring.color = spec.superHard ? UITheme.Accent : UITheme.Primary;
                    var tag = UIFactory.Panel(rt, ring.color, 20, "Tag");
                    tag.raycastTarget = false;
                    UIFactory.Place(tag.rectTransform, new Vector2(0.5f, 1f), new Vector2(spec.superHard ? 240 : 140, 46), new Vector2(0, 36), new Vector2(0.5f, 0f));
                    var tt = UIFactory.Label(tag.transform, Loc.T(spec.superHard ? "super_hard" : "hard"), UITheme.SmallSize - 8, Color.white, FontStyle.Bold);
                    UIFactory.Stretch(tt.rectTransform);
                }
                if (done)
                {
                    var row = UIFactory.Rect("Stars", rt);
                    UIFactory.Place(row, new Vector2(0.5f, 0f), new Vector2(150, 44), new Vector2(0, -50), new Vector2(0.5f, 0f));
                    UIFactory.HStack(row, 2);
                    for (int i = 0; i < 3; i++)
                        UIFactory.Layout(UIFactory.IconImage(row, Icon.Star, i < stars ? UITheme.Gold : UITheme.Disabled, 40), 40, 40);
                }
                if (isNext) _current = rt;
            }

            // Scroll so the current level is centred.
            Canvas.ForceUpdateCanvases();
            if (_current != null)
            {
                float viewport = ((RectTransform)_scroll.transform).rect.height;
                float target = -_current.anchoredPosition.y - viewport * 0.5f;
                float max = Mathf.Max(1f, _content.sizeDelta.y - viewport);
                _scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(target / max);
            }
        }

        void Update()
        {
            if (_current == null) return;
            _time += Time.unscaledDeltaTime;
            _current.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(_time * 4f));
        }

        public override bool HandleBack()
        {
            Game.GoHome();
            return true;
        }
    }
}
