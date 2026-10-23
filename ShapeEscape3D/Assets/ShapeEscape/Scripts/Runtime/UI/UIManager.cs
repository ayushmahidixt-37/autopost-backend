using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShapeEscape
{
    /// <summary>Base class for full-screen UI pages and popups. Built lazily on first show.</summary>
    public abstract class UIScreen : MonoBehaviour
    {
        protected GameManager Game;
        protected UIManager UI;
        protected RectTransform Root;
        CanvasGroup _group;
        Coroutine _anim;
        bool _built;

        public bool IsVisible { get; private set; }
        /// <summary>Popups dim the screen behind them and stack above pages.</summary>
        public virtual bool IsPopup { get { return false; } }

        public void Attach(GameManager game, UIManager ui)
        {
            Game = game;
            UI = ui;
            Root = (RectTransform)transform;
            _group = gameObject.AddComponent<CanvasGroup>();
            gameObject.SetActive(false);
        }

        protected abstract void Build();
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }

        /// <summary>Android back button. Return true if handled.</summary>
        public virtual bool HandleBack() { return false; }

        public void Show()
        {
            if (!_built)
            {
                _built = true;
                Build();
            }
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            IsVisible = true;
            _group.interactable = true;
            _group.blocksRaycasts = true;
            OnShow();
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Animate(true));
        }

        public void Hide()
        {
            if (!IsVisible) return;
            IsVisible = false;
            _group.interactable = false;
            _group.blocksRaycasts = false;
            OnHide();
            if (!gameObject.activeInHierarchy) return;
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(Animate(false));
        }

        IEnumerator Animate(bool show)
        {
            float t = 0f;
            const float duration = 0.22f;
            float from = _group.alpha, to = show ? 1f : 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Tween.EaseOutCubic(Mathf.Clamp01(t / duration));
                _group.alpha = Mathf.Lerp(from, to, k);
                float scale = show ? Mathf.Lerp(IsPopup ? 0.92f : 0.98f, 1f, k) : Mathf.Lerp(1f, 0.98f, k);
                transform.localScale = Vector3.one * scale;
                yield return null;
            }
            _group.alpha = to;
            transform.localScale = Vector3.one;
            if (!show) gameObject.SetActive(false);
            _anim = null;
        }

        /// <summary>Rebuilds dynamic content (called after language changes).</summary>
        public virtual void Refresh() { }
    }

    /// <summary>Keeps content inside the device safe area (notches, rounded corners, gesture bars).</summary>
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect _applied;

        void Update()
        {
            var safe = Screen.safeArea;
            if (safe == _applied || Screen.width <= 0 || Screen.height <= 0) return;
            _applied = safe;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }

    /// <summary>
    /// Owns the canvas, event system, screen stack, popups, toasts and the simulated-ad overlay.
    /// Screens are plain code-built pages (see UI/Screens).
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        GameManager _game;
        RectTransform _safe;
        RectTransform _pages;
        RectTransform _popups;
        RectTransform _toasts;
        Image _scrim;
        readonly Dictionary<Type, UIScreen> _screens = new Dictionary<Type, UIScreen>();
        readonly List<UIScreen> _popupStack = new List<UIScreen>();
        UIScreen _currentPage;

        public Canvas Canvas { get; private set; }
        public UIScreen CurrentPage { get { return _currentPage; } }
        public bool AnyPopupOpen { get { return _popupStack.Count > 0; } }

        public void Initialize(GameManager game)
        {
            _game = game;
            UIButton.ClickSound = () => _game.Audio.Play(Sfx.UiClick);

            var canvasGo = new GameObject("UI Canvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.layer = 5;
            Canvas = canvasGo.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 10;
            Canvas.pixelPerfect = false;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.35f;
            canvasGo.AddComponent<GraphicRaycaster>();

            EnsureEventSystem();

            _safe = UIFactory.Stretch(UIFactory.Rect("SafeArea", canvasGo.transform));
            _safe.gameObject.AddComponent<SafeAreaFitter>();
            _pages = UIFactory.Stretch(UIFactory.Rect("Pages", _safe));

            // Scrim covers the full screen (outside the safe area too) behind popups.
            var scrimRt = UIFactory.Stretch(UIFactory.Rect("Scrim", canvasGo.transform));
            _scrim = scrimRt.gameObject.AddComponent<Image>();
            _scrim.color = UITheme.Scrim;
            _scrim.gameObject.SetActive(false);
            _popups = UIFactory.Stretch(UIFactory.Rect("Popups", canvasGo.transform));
            var popupSafe = _popups.gameObject.AddComponent<SafeAreaFitter>();
            popupSafe.enabled = true;
            _toasts = UIFactory.Stretch(UIFactory.Rect("Toasts", canvasGo.transform));
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.transform.SetParent(transform, false);
            es.AddComponent<EventSystem>();
#if ENABLE_LEGACY_INPUT_MANAGER
            es.AddComponent<StandaloneInputModule>();
#else
            // New Input System only: add its UI module without a compile-time dependency.
            var type = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (type != null) es.AddComponent(type);
            else Debug.LogError("[UI] No input module available. Enable 'Both' in Player Settings > Active Input Handling.");
#endif
        }

        public T Get<T>() where T : UIScreen
        {
            UIScreen s;
            if (_screens.TryGetValue(typeof(T), out s)) return (T)s;
            var rt = UIFactory.Stretch(UIFactory.Rect(typeof(T).Name, _pages));
            var screen = rt.gameObject.AddComponent<T>();
            screen.Attach(_game, this);
            if (screen.IsPopup) rt.SetParent(_popups, false);
            _screens[typeof(T)] = screen;
            return screen;
        }

        /// <summary>Switches the main page (closing popups).</summary>
        public T ShowPage<T>() where T : UIScreen
        {
            CloseAllPopups();
            var next = Get<T>();
            if (_currentPage != null && _currentPage != next) _currentPage.Hide();
            _currentPage = next;
            next.Show();
            return next;
        }

        public T ShowPopup<T>() where T : UIScreen
        {
            var p = Get<T>();
            _popupStack.Remove(p);
            _popupStack.Add(p);
            p.Show();
            UpdateScrim();
            _game.OnPopupStateChanged();
            return p;
        }

        public void ClosePopup(UIScreen popup)
        {
            if (popup == null) return;
            popup.Hide();
            _popupStack.Remove(popup);
            UpdateScrim();
            _game.OnPopupStateChanged();
        }

        public void CloseAllPopups()
        {
            foreach (var p in _popupStack) p.Hide();
            _popupStack.Clear();
            UpdateScrim();
        }

        void UpdateScrim()
        {
            _scrim.gameObject.SetActive(_popupStack.Count > 0);
            _scrim.transform.SetAsLastSibling();
            _popups.SetAsLastSibling();
            _toasts.SetAsLastSibling();
        }

        void Update()
        {
            if (!TouchInputManager.BackPressed()) return;
            if (_popupStack.Count > 0)
            {
                var top = _popupStack[_popupStack.Count - 1];
                if (!top.HandleBack()) ClosePopup(top);
                return;
            }
            if (_currentPage != null && _currentPage.HandleBack()) return;
            _game.OnBackAtRoot();
        }

        public void RefreshAll()
        {
            foreach (var s in _screens.Values) s.Refresh();
        }

        // ---------------------------------------------------------------- generic dialog / toast

        /// <summary>Simple modal dialog. Buttons are (label, action) pairs; the first is the primary action.</summary>
        public void Dialog(string title, string message, params KeyValuePair<string, Action>[] buttons)
        {
            var d = ShowPopup<DialogPopup>();
            d.Setup(title, message, buttons);
        }

        public static KeyValuePair<string, Action> Btn(string label, Action action)
        {
            return new KeyValuePair<string, Action>(label, action);
        }

        public void Toast(string message, float seconds = 2.2f)
        {
            StartCoroutine(ToastRoutine(message, seconds));
        }

        IEnumerator ToastRoutine(string message, float seconds)
        {
            var card = UIFactory.Panel(_toasts, UITheme.Ink.WithAlpha(0.88f), 40, "Toast");
            UIFactory.Place(card.rectTransform, new Vector2(0.5f, 0.2f), new Vector2(880, 140), Vector2.zero);
            card.raycastTarget = false;
            var t = UIFactory.Label(card.transform, message, UITheme.BodySize - 4, Color.white);
            UIFactory.Stretch(t.rectTransform, 30, 30, 10, 10);
            var cg = card.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            float time = 0;
            while (time < seconds + 0.5f)
            {
                time += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Clamp01(time / 0.2f) * Mathf.Clamp01((seconds + 0.5f - time) / 0.3f);
                card.rectTransform.anchoredPosition = new Vector2(0, Mathf.Lerp(-30, 0, Tween.EaseOutCubic(Mathf.Clamp01(time / 0.25f))));
                yield return null;
            }
            Destroy(card.gameObject);
        }

        // ---------------------------------------------------------------- simulated ads (editor / dev builds only)

        public bool ShowSimulatedAd(bool rewarded, Action<bool> done)
        {
            StartCoroutine(SimulatedAdRoutine(rewarded, done));
            return true;
        }

        IEnumerator SimulatedAdRoutine(bool rewarded, Action<bool> done)
        {
            var overlay = UIFactory.Stretch(UIFactory.Rect("SimulatedAd", Canvas.transform));
            var bg = overlay.gameObject.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.96f);
            var title = UIFactory.Label(overlay, "TEST AD (simulated)\n" + (rewarded ? "Rewarded" : "Interstitial"), 64, Color.white, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 0.6f), new Vector2(1000, 300), Vector2.zero);
            var note = UIFactory.Label(overlay, "Real ads appear only in builds with the AdMob SDK (SE_ADMOB).", 34, new Color(1, 1, 1, 0.6f));
            UIFactory.Place(note.rectTransform, new Vector2(0.5f, 0.45f), new Vector2(900, 160), Vector2.zero);
            var counter = UIFactory.Label(overlay, "", 48, Color.white);
            UIFactory.Place(counter.rectTransform, new Vector2(0.5f, 0.32f), new Vector2(600, 100), Vector2.zero);
            bool closed = false;
            bool earned = false;
            var close = UIFactory.Button(overlay, "Close", UITheme.InkSoft, Color.white, () => closed = true);
            UIFactory.Place(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0.2f), new Vector2(420, 130), Vector2.zero);
            close.gameObject.SetActive(false);

            float t = 2.5f;
            while (t > 0 && !closed)
            {
                t -= Time.unscaledDeltaTime;
                counter.text = Mathf.CeilToInt(Mathf.Max(0, t)) + "s";
                yield return null;
            }
            earned = rewarded;
            counter.text = rewarded ? "Reward granted" : "";
            close.gameObject.SetActive(true);
            while (!closed) yield return null;
            Destroy(overlay.gameObject);
            if (done != null) done(earned);
        }
    }
}
