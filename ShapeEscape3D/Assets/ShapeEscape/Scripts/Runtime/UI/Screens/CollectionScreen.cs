using System.Collections.Generic;
using ShapeEscape.Core;
using UnityEngine;
using UnityEngine.UI;

namespace ShapeEscape
{
    /// <summary>
    /// Object Collection: themed rooms (tabs), a grid of collected / locked objects, and an interactive 3D
    /// viewer for any collected object (drag to rotate, pinch to zoom).
    /// </summary>
    public sealed class CollectionScreen : UIScreen
    {
        RectTransform _listPanel;
        RectTransform _viewerPanel;
        RectTransform _tabs;
        RectTransform _grid;
        Text _progressText;
        UIProgressBar _progress;
        Text _title;
        Text _viewerName;
        Text _viewerInfo;
        ObjectCategory _room = ObjectCategory.Kitchen;
        readonly List<UIButton> _tabButtons = new List<UIButton>();
        bool _inViewer;

        protected override void Build()
        {
            _listPanel = UIFactory.Stretch(UIFactory.Rect("List", Root));
            ScreenParts.Header(_listPanel, Loc.T("collection"), () => Game.CloseCollection(), out _title);

            var prog = UIFactory.Rect("Progress", _listPanel);
            UIFactory.Place(prog, new Vector2(0.5f, 1f), new Vector2(900, 110), new Vector2(0, -180), new Vector2(0.5f, 1f));
            _progressText = UIFactory.Label(prog, "", UITheme.BodySize, UITheme.Ink, FontStyle.Bold);
            UIFactory.Place(_progressText.rectTransform, new Vector2(0.5f, 1f), new Vector2(900, 56), Vector2.zero, new Vector2(0.5f, 1f));
            var barHolder = UIFactory.Rect("Bar", prog);
            UIFactory.Place(barHolder, new Vector2(0.5f, 0f), new Vector2(760, 30), new Vector2(0, 6), new Vector2(0.5f, 0f));
            _progress = UIFactory.ProgressBar(barHolder, UITheme.CardSoft, UITheme.Accent, 30);
            UIFactory.Stretch(_progress.GetComponent<RectTransform>());

            var tabsScroll = UIFactory.ScrollArea(_listPanel, out _tabs, false);
            var tabsRt = tabsScroll.GetComponent<RectTransform>();
            tabsRt.anchorMin = new Vector2(0, 1);
            tabsRt.anchorMax = new Vector2(1, 1);
            tabsRt.pivot = new Vector2(0.5f, 1);
            tabsRt.sizeDelta = new Vector2(0, 130);
            tabsRt.anchoredPosition = new Vector2(0, -310);
            var h = UIFactory.HStack(_tabs, 16, new RectOffset(40, 40, 10, 10), TextAnchor.MiddleLeft);
            h.childForceExpandHeight = true;

            var gridScroll = UIFactory.ScrollArea(_listPanel, out _grid);
            UIFactory.Stretch(gridScroll.GetComponent<RectTransform>(), 40, 40, 460, 30);
            var g = _grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(480, 330);
            g.spacing = new Vector2(20, 24);
            g.padding = new RectOffset(0, 0, 10, 40);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = 2;
            g.childAlignment = TextAnchor.UpperCenter;

            // Viewer overlay (shown while inspecting an object in 3D).
            _viewerPanel = UIFactory.Stretch(UIFactory.Rect("Viewer", Root));
            Text dummy;
            ScreenParts.Header(_viewerPanel, "", () => CloseViewer(), out dummy);
            _viewerName = UIFactory.Label(_viewerPanel, "", 72, UITheme.Ink, FontStyle.Bold);
            UIFactory.Place(_viewerName.rectTransform, new Vector2(0.5f, 1f), new Vector2(1000, 100), new Vector2(0, -190), new Vector2(0.5f, 1f));
            _viewerInfo = UIFactory.Label(_viewerPanel, "", UITheme.BodySize, UITheme.InkSoft, FontStyle.Bold);
            UIFactory.Place(_viewerInfo.rectTransform, new Vector2(0.5f, 0f), new Vector2(1000, 160), new Vector2(0, 120), new Vector2(0.5f, 0f));
            var hint = UIFactory.Label(_viewerPanel, Loc.T("drag_to_rotate"), UITheme.SmallSize, UITheme.InkSoft);
            UIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(1000, 60), new Vector2(0, 60), new Vector2(0.5f, 0f));
            _viewerPanel.gameObject.SetActive(false);
        }

        protected override void OnShow()
        {
            _inViewer = false;
            _listPanel.gameObject.SetActive(true);
            _viewerPanel.gameObject.SetActive(false);
            Rebuild();
            Game.Analytics.Log(AnalyticsEvents.CollectionView, "unlocked", Game.Collection.UnlockedCount);
        }

        public override void Refresh()
        {
            if (IsVisible) Rebuild();
        }

        void Rebuild()
        {
            var col = Game.Collection;
            _title.text = Loc.T("collection");
            _progressText.text = col.UnlockedCount + " / " + col.TotalCount;
            _progress.SetValue(col.UnlockedCount / (float)Mathf.Max(1, col.TotalCount), true);

            UIFactory.Clear(_tabs);
            _tabButtons.Clear();
            foreach (var room in CollectionManager.Rooms)
            {
                var r = room;
                string label = Loc.T("room_" + room) + "  " + col.UnlockedIn(room) + "/" + col.ObjectsIn(room).Count;
                var b = UIFactory.Button(_tabs, label, room == _room ? UITheme.Accent : UITheme.Card, room == _room ? Color.white : UITheme.Ink,
                    () => SelectRoom(r), null, UITheme.SmallSize, 40);
                b.label.horizontalOverflow = HorizontalWrapMode.Overflow;
                UIFactory.Layout(b, 100, Mathf.Max(260, label.Length * 21 + 80));
                _tabButtons.Add(b);
            }
            SelectRoom(_room);
        }

        void SelectRoom(ObjectCategory room)
        {
            _room = room;
            Color top, bottom;
            CollectionManager.RoomColors(room, out top, out bottom);
            Game.Environment.SetBackground(top, bottom);
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                bool sel = CollectionManager.Rooms[i] == room;
                _tabButtons[i].SetColors(sel ? UITheme.Accent : UITheme.Card, sel ? Color.white : UITheme.Ink);
            }

            UIFactory.Clear(_grid);
            foreach (var t in Game.Collection.ObjectsIn(room))
                BuildCard(t);
        }

        void BuildCard(ObjectTemplate t)
        {
            bool unlocked = Game.Collection.IsUnlocked(t.id);
            var tpl = t;
            var card = UIFactory.Button(_grid, null, unlocked ? UITheme.Card : UITheme.CardSoft, UITheme.Ink,
                unlocked ? (UnityEngine.Events.UnityAction)(() => OpenViewer(tpl)) : null, null, 40, 44, t.id);
            var face = card.face.transform;

            var swatch = UIFactory.Panel(face, unlocked ? PuzzleBoardView.PaletteColor(t, 0) : UITheme.Disabled, 64, "Swatch");
            UIFactory.Place(swatch.rectTransform, new Vector2(0.5f, 1f), new Vector2(128, 128), new Vector2(0, -30), new Vector2(0.5f, 1f));
            if (unlocked && t.palette.Length > 1)
            {
                var inner = UIFactory.Panel(swatch.transform, PuzzleBoardView.PaletteColor(t, 1), 30, "Inner");
                UIFactory.Place(inner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(60, 60), Vector2.zero);
            }
            else if (!unlocked)
            {
                var lockIcon = UIFactory.IconImage(swatch.transform, Icon.Lock, Color.white, 64);
                UIFactory.Place(lockIcon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(64, 64), Vector2.zero);
            }

            var name = UIFactory.Label(face, unlocked ? Loc.ObjectName(t.id, t.displayName) : Loc.T("mystery"), UITheme.BodySize, UITheme.Ink, FontStyle.Bold);
            UIFactory.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(440, 60), new Vector2(0, 92), new Vector2(0.5f, 0f));

            string sub;
            if (unlocked) sub = t.dailyExclusive ? Loc.T("special_collectible") : Loc.F("solved_variants", Game.Collection.VariantsCompleted(t.id));
            else if (t.dailyExclusive) sub = Loc.T("unlock_by_daily");
            else sub = Loc.F("unlock_by_level", System.Array.IndexOf(ObjectCatalog.CampaignOrder, t.id) + 1);
            var info = UIFactory.Label(face, sub, UITheme.SmallSize - 2, UITheme.InkSoft);
            UIFactory.Place(info.rectTransform, new Vector2(0.5f, 0f), new Vector2(440, 50), new Vector2(0, 40), new Vector2(0.5f, 0f));
        }

        void OpenViewer(ObjectTemplate t)
        {
            _inViewer = true;
            _listPanel.gameObject.SetActive(false);
            _viewerPanel.gameObject.SetActive(true);
            _viewerName.text = Loc.ObjectName(t.id, t.displayName);
            _viewerInfo.text = Loc.T("room_" + t.category) + "\n" +
                               (t.dailyExclusive ? Loc.T("special_collectible") : Loc.F("solved_variants", Game.Collection.VariantsCompleted(t.id)));
            Game.Viewer.Show(t);
        }

        void CloseViewer()
        {
            _inViewer = false;
            Game.Viewer.Hide();
            _listPanel.gameObject.SetActive(true);
            _viewerPanel.gameObject.SetActive(false);
        }

        protected override void OnHide()
        {
            if (_inViewer) CloseViewer();
        }

        public override bool HandleBack()
        {
            if (_inViewer)
            {
                CloseViewer();
                return true;
            }
            Game.CloseCollection();
            return true;
        }
    }
}
