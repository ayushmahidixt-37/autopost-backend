using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShapeEscape
{
    /// <summary>Palette and typography for the pastel UI.</summary>
    public static class UITheme
    {
        public static readonly Color Ink = ColorUtil.Hex("#2B2D42");
        public static readonly Color InkSoft = ColorUtil.Hex("#6C6F8A");
        public static readonly Color Card = ColorUtil.Hex("#FFFFFF", 0.94f);
        public static readonly Color CardSoft = ColorUtil.Hex("#FFFFFF", 0.7f);
        public static readonly Color Primary = ColorUtil.Hex("#FF6B6B");
        public static readonly Color PrimaryDark = ColorUtil.Hex("#E05252");
        public static readonly Color Secondary = ColorUtil.Hex("#4ECDC4");
        public static readonly Color Accent = ColorUtil.Hex("#7B61FF");
        public static readonly Color Gold = ColorUtil.Hex("#FFC23C");
        public static readonly Color Success = ColorUtil.Hex("#06D6A0");
        public static readonly Color Disabled = ColorUtil.Hex("#C9C9D6");
        public static readonly Color Scrim = new Color(0.13f, 0.11f, 0.22f, 0.45f);

        public const int TitleSize = 84;
        public const int HeadingSize = 54;
        public const int BodySize = 40;
        public const int SmallSize = 32;

        static Font _font;

        /// <summary>Built-in dynamic font (falls back to OS fonts for missing glyphs). Swap for a branded TMP/Font asset in production.</summary>
        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.Load<Font>("ShapeEscape/Fonts/Main");
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                return _font;
            }
        }
    }

    /// <summary>
    /// Code-driven uGUI construction helpers. All screens are built from these, so the UI needs no prefabs
    /// and stays consistent; designers can still replace any screen with a prefab later.
    /// </summary>
    public static class UIFactory
    {
        static readonly Dictionary<int, Sprite> RoundedCache = new Dictionary<int, Sprite>();
        static Sprite _shadow;

        /// <summary>9-sliced rounded rectangle sprite with the given corner radius (in sprite pixels).</summary>
        public static Sprite Rounded(int radius)
        {
            Sprite s;
            if (RoundedCache.TryGetValue(radius, out s) && s != null) return s;
            int size = radius * 2 + 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Rounded" + radius };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                    float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - radius;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = radius + 1;
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            RoundedCache[radius] = s;
            return s;
        }

        /// <summary>Soft blurred rounded shadow (9-sliced).</summary>
        public static Sprite Shadow
        {
            get
            {
                if (_shadow != null) return _shadow;
                const int size = 96, r = 40;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Shadow" };
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                        float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        float a = Mathf.Clamp01(1f - d / r);
                        px[y * size + x] = new Color32(0, 0, 0, (byte)(a * a * 255));
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                _shadow = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return _shadow;
            }
        }

        // ---------------------------------------------------------------- layout helpers

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchors at a normalised point with a fixed size and offset.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 offset, Vector2? pivot = null)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? anchor;
            rt.sizeDelta = size;
            rt.anchoredPosition = offset;
            return rt;
        }

        /// <summary>Full-width strip anchored to the top or bottom.</summary>
        public static RectTransform Strip(RectTransform rt, bool top, float height, float inset = 0)
        {
            rt.anchorMin = new Vector2(0, top ? 1 : 0);
            rt.anchorMax = new Vector2(1, top ? 1 : 0);
            rt.pivot = new Vector2(0.5f, top ? 1 : 0);
            rt.sizeDelta = new Vector2(-inset * 2, height);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        public static LayoutElement Layout(Component c, float preferredHeight = -1, float preferredWidth = -1, float flexibleWidth = -1)
        {
            var le = c.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
            if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.UpperCenter)
        {
            var g = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding ?? new RectOffset();
            g.childAlignment = align;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = true;
            g.childForceExpandHeight = false;
            return g;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var g = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding ?? new RectOffset();
            g.childAlignment = align;
            g.childControlWidth = true;
            g.childControlHeight = true;
            g.childForceExpandWidth = false;
            g.childForceExpandHeight = false;
            return g;
        }

        // ---------------------------------------------------------------- widgets

        public static Image Panel(Transform parent, Color color, int radius = 36, string name = "Panel")
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = radius > 0 ? Rounded(radius) : null;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        /// <summary>Card with a soft drop shadow behind it.</summary>
        public static Image Card(Transform parent, Color color, int radius = 44, string name = "Card")
        {
            var holder = Rect(name, parent);
            var shadow = Rect("Shadow", holder);
            Stretch(shadow, -18, -18, -10, -26);
            var sImg = shadow.gameObject.AddComponent<Image>();
            sImg.sprite = Shadow;
            sImg.type = Image.Type.Sliced;
            sImg.color = new Color(0.25f, 0.15f, 0.4f, 0.18f);
            sImg.raycastTarget = false;
            var img = holder.gameObject.AddComponent<Image>();
            img.sprite = Rounded(radius);
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, FontStyle style = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleCenter, string name = "Label")
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = UITheme.Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.resizeTextForBestFit = false;
            return t;
        }

        public static Image IconImage(Transform parent, Icon icon, Color color, float size, string name = "Icon")
        {
            var rt = Rect(name, parent);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = IconFactory.Get(icon);
            img.color = color;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Pill / rounded button with optional icon and label.</summary>
        public static UIButton Button(Transform parent, string label, Color bg, Color fg, UnityAction onClick, Icon? icon = null, int fontSize = UITheme.BodySize, int radius = 48, string name = "Button")
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded(radius);
            img.type = Image.Type.Sliced;
            img.color = bg;
            var btn = rt.gameObject.AddComponent<UIButton>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            if (onClick != null) btn.onClick.AddListener(onClick);

            // Lower "lip" gives buttons a soft 3D look.
            var lip = Rect("Lip", rt);
            Stretch(lip, 0, 0, 10, -10);
            lip.SetAsFirstSibling();
            var lipImg = lip.gameObject.AddComponent<Image>();
            lipImg.sprite = Rounded(radius);
            lipImg.type = Image.Type.Sliced;
            lipImg.color = ColorUtil.Darken(bg, 0.18f).WithAlpha(bg.a);
            lipImg.raycastTarget = false;
            // The visible face is a child drawn above the lip; the root image is an invisible hit area.
            var face = Rect("Face", rt);
            Stretch(face);
            var faceImg = face.gameObject.AddComponent<Image>();
            faceImg.sprite = Rounded(radius);
            faceImg.type = Image.Type.Sliced;
            faceImg.color = bg;
            faceImg.raycastTarget = false;
            img.color = new Color(0, 0, 0, 0);
            btn.face = faceImg;
            btn.lip = lipImg;

            var content = Rect("Content", face);
            Stretch(content, 24, 24, 0, 0);
            var h = HStack(content, 18);
            h.childAlignment = TextAnchor.MiddleCenter;
            if (icon.HasValue)
            {
                var ic = IconImage(content, icon.Value, fg, fontSize * 1.25f);
                Layout(ic, fontSize * 1.25f, fontSize * 1.25f);
                btn.icon = ic;
            }
            if (!string.IsNullOrEmpty(label))
            {
                var t = Label(content, label, fontSize, fg, FontStyle.Bold);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                Layout(t, fontSize * 1.3f);
                btn.label = t;
            }
            return btn;
        }

        public static UIButton IconButton(Transform parent, Icon icon, Color bg, Color fg, UnityAction onClick, float size = 132, string name = "IconButton")
        {
            var b = Button(parent, null, bg, fg, onClick, icon, (int)(size * 0.42f), (int)(size * 0.36f), name);
            b.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);
            return b;
        }

        /// <summary>Small round counter badge (e.g. hint count).</summary>
        public static Text Badge(Transform parent, string text, Color bg, float size = 56)
        {
            var img = Panel(parent, bg, 28, "Badge");
            Place(img.rectTransform, new Vector2(1, 1), new Vector2(size, size), new Vector2(8, 8), new Vector2(0.5f, 0.5f));
            img.raycastTarget = false;
            var t = Label(img.transform, text, (int)(size * 0.55f), Color.white, FontStyle.Bold);
            Stretch(t.rectTransform);
            return t;
        }

        public static UIProgressBar ProgressBar(Transform parent, Color back, Color fill, float height = 28)
        {
            var bgImg = Panel(parent, back, 14, "Progress");
            Layout(bgImg, height);
            var fillRt = Rect("Fill", bgImg.transform);
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(0, 1);
            fillRt.pivot = new Vector2(0, 0.5f);
            fillRt.offsetMin = new Vector2(4, 4);
            fillRt.offsetMax = new Vector2(4, -4);
            var fImg = fillRt.gameObject.AddComponent<Image>();
            fImg.sprite = Rounded(12);
            fImg.type = Image.Type.Sliced;
            fImg.color = fill;
            fImg.raycastTarget = false;
            var bar = bgImg.gameObject.AddComponent<UIProgressBar>();
            bar.fill = fillRt;
            return bar;
        }

        public static UISwitch Switch(Transform parent, bool value, Action<bool> onChanged)
        {
            var track = Panel(parent, UITheme.Disabled, 30, "Switch");
            track.rectTransform.sizeDelta = new Vector2(132, 72);
            Layout(track, 72, 132);
            var knob = Panel(track.transform, Color.white, 28, "Knob");
            Place(knob.rectTransform, new Vector2(0, 0.5f), new Vector2(60, 60), new Vector2(6, 0), new Vector2(0, 0.5f));
            knob.raycastTarget = false;
            var sw = track.gameObject.AddComponent<UISwitch>();
            sw.Setup(track, knob.rectTransform, value, onChanged);
            return sw;
        }

        /// <summary>Simple horizontal slider (0..1) built on uGUI's Slider.</summary>
        public static Slider SliderControl(Transform parent, float value, Action<float> onChanged)
        {
            var root = Rect("Slider", parent);
            Layout(root, 64, 360, 1);
            var slider = root.gameObject.AddComponent<Slider>();
            var bg = Panel(root, UITheme.Disabled, 12, "Track");
            bg.rectTransform.anchorMin = new Vector2(0, 0.5f);
            bg.rectTransform.anchorMax = new Vector2(1, 0.5f);
            bg.rectTransform.sizeDelta = new Vector2(0, 20);
            var fillArea = Rect("FillArea", root);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.sizeDelta = new Vector2(0, 20);
            var fill = Panel(fillArea, UITheme.Secondary, 12, "Fill");
            fill.rectTransform.sizeDelta = Vector2.zero;
            var handleArea = Rect("HandleArea", root);
            Stretch(handleArea, 24, 24, 0, 0);
            var handle = Panel(handleArea, Color.white, 28, "Handle");
            handle.rectTransform.sizeDelta = new Vector2(56, 56);
            var outline = handle.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.12f);
            outline.effectDistance = new Vector2(2, -3);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = value;
            slider.onValueChanged.AddListener(v => { if (onChanged != null) onChanged(v); });
            return slider;
        }

        public static ScrollRect ScrollArea(Transform parent, out RectTransform content, bool vertical = true)
        {
            var root = Rect("Scroll", parent);
            var img = root.gameObject.AddComponent<Image>();
            img.color = new Color(1, 1, 1, 0.001f);
            root.gameObject.AddComponent<RectMask2D>();
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = !vertical;
            scroll.vertical = vertical;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            content = Rect("Content", root);
            if (vertical)
            {
                content.anchorMin = new Vector2(0, 1);
                content.anchorMax = new Vector2(1, 1);
                content.pivot = new Vector2(0.5f, 1);
            }
            else
            {
                content.anchorMin = new Vector2(0, 0);
                content.anchorMax = new Vector2(0, 1);
                content.pivot = new Vector2(0, 0.5f);
            }
            content.sizeDelta = Vector2.zero;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            if (vertical) fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            else fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = root;
            return scroll;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }

    /// <summary>Button with a squishy press animation and click sound.</summary>
    public sealed class UIButton : Button
    {
        public Image face;
        public Image lip;
        public Image icon;
        public Text label;
        float _press;
        float _target;

        public static Action ClickSound;

        protected override void Awake()
        {
            base.Awake();
            onClick.AddListener(() => { if (ClickSound != null) ClickSound(); });
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (interactable) _target = 1f;
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);
            _target = 0f;
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            _target = 0f;
        }

        void Update()
        {
            if (Mathf.Approximately(_press, _target)) return;
            _press = Mathf.MoveTowards(_press, _target, Time.unscaledDeltaTime * 12f);
            if (face != null) face.rectTransform.anchoredPosition = new Vector2(0, -8f * _press);
            transform.localScale = Vector3.one * (1f - 0.04f * _press);
        }

        public void SetColors(Color bg, Color fg)
        {
            if (face != null) face.color = bg;
            if (lip != null) lip.color = ColorUtil.Darken(bg, 0.18f).WithAlpha(bg.a);
            if (icon != null) icon.color = fg;
            if (label != null) label.color = fg;
        }

        public void SetLabel(string text)
        {
            if (label != null) label.text = text;
        }

        public void SetInteractable(bool value)
        {
            interactable = value;
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = value ? 1f : 0.5f;
        }
    }

    public sealed class UIProgressBar : MonoBehaviour
    {
        public RectTransform fill;
        float _value;
        float _shown;

        public void SetValue(float v, bool instant = false)
        {
            _value = Mathf.Clamp01(v);
            if (instant) _shown = _value;
            Apply();
        }

        void Update()
        {
            if (Mathf.Abs(_shown - _value) < 0.001f) return;
            _shown = Mathf.Lerp(_shown, _value, Tween.Damp(10f, Time.unscaledDeltaTime));
            Apply();
        }

        void Apply()
        {
            if (fill == null) return;
            var parent = (RectTransform)fill.parent;
            float w = Mathf.Max(0, parent.rect.width - 8f);
            fill.sizeDelta = new Vector2(Mathf.Max(_shown > 0.001f ? 24f : 0f, w * _shown), fill.sizeDelta.y);
        }

        void OnRectTransformDimensionsChange() { Apply(); }
    }

    public sealed class UISwitch : MonoBehaviour, IPointerClickHandler
    {
        Image _track;
        RectTransform _knob;
        Action<bool> _changed;
        float _anim;

        public bool Value { get; private set; }

        public void Setup(Image track, RectTransform knob, bool value, Action<bool> changed)
        {
            _track = track;
            _knob = knob;
            _changed = changed;
            Value = value;
            _anim = value ? 1 : 0;
            Apply();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Set(!Value, true);
        }

        /// <summary>Updates the visual state without raising the change callback (e.g. when a screen re-opens).</summary>
        public void SetWithoutNotify(bool value)
        {
            Value = value;
        }

        public void Set(bool value, bool notify)
        {
            Value = value;
            if (UIButton.ClickSound != null) UIButton.ClickSound();
            if (notify && _changed != null) _changed(value);
        }

        void Update()
        {
            float target = Value ? 1 : 0;
            if (Mathf.Approximately(_anim, target)) return;
            _anim = Mathf.MoveTowards(_anim, target, Time.unscaledDeltaTime * 6f);
            Apply();
        }

        void Apply()
        {
            float k = Tween.EaseOutCubic(_anim);
            _knob.anchoredPosition = new Vector2(Mathf.Lerp(6, 66, k), 0);
            _track.color = Color.Lerp(UITheme.Disabled, UITheme.Secondary, k);
        }
    }
}
