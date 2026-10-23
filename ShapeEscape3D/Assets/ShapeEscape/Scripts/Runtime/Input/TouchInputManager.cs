using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ShapeEscape
{
    /// <summary>
    /// Unifies touch and mouse input into high-level gestures: tap, one-finger drag (rotate) and
    /// two-finger pinch (zoom). Gestures that start on top of UI are ignored. Works with the legacy Input
    /// Manager and, when the project uses only the new Input System package, with that package instead.
    /// </summary>
    public sealed class TouchInputManager : MonoBehaviour
    {
        public event Action<Vector2> Tapped;
        public event Action DragBegan;
        public event Action<Vector2> Dragged;
        public event Action DragEnded;
        /// <summary>Zoom factor for this frame (&gt;1 = fingers moving apart / zoom in).</summary>
        public event Action<float> Pinched;

        [Tooltip("Max press duration that still counts as a tap.")]
        public float tapMaxDuration = 0.35f;
        [Tooltip("Movement (in points, scaled by DPI) after which a touch becomes a drag.")]
        public float dragThresholdPoints = 9f;

        public bool InputEnabled { get; set; }

        struct Pointer
        {
            public int id;
            public Vector2 position;
            public bool down;
        }

        readonly List<Pointer> _pointers = new List<Pointer>(4);
        bool _tracking;
        bool _isDragging;
        bool _startedOverUi;
        bool _wasPinching;
        float _pressTime;
        Vector2 _pressPos;
        Vector2 _lastPos;
        float _lastPinchDistance;
        float _dragThresholdPx;

        void Awake()
        {
            InputEnabled = true;
            float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
            _dragThresholdPx = dragThresholdPoints * dpi / 160f;
        }

        void Update()
        {
            ReadPointers();
            if (!InputEnabled)
            {
                if (_isDragging && DragEnded != null) DragEnded();
                _tracking = _isDragging = _wasPinching = false;
                return;
            }

            ReadScrollZoom();

            int active = 0;
            for (int i = 0; i < _pointers.Count; i++) if (_pointers[i].down) active++;

            if (active >= 2)
            {
                HandlePinch();
                return;
            }
            if (_wasPinching)
            {
                // After a pinch, wait until all fingers lift before starting new gestures.
                if (active == 0) _wasPinching = false;
                _tracking = false;
                return;
            }

            if (active == 1)
            {
                var p = FirstDown();
                if (!_tracking)
                {
                    _tracking = true;
                    _isDragging = false;
                    _pressTime = Time.unscaledTime;
                    _pressPos = _lastPos = p.position;
                    _startedOverUi = IsOverUi(p.id, p.position);
                    return;
                }
                if (_startedOverUi) return;

                if (!_isDragging && (p.position - _pressPos).magnitude > _dragThresholdPx)
                {
                    _isDragging = true;
                    if (DragBegan != null) DragBegan();
                    _lastPos = _pressPos;
                }
                if (_isDragging)
                {
                    var delta = p.position - _lastPos;
                    if (delta.sqrMagnitude > 0f && Dragged != null) Dragged(delta);
                }
                _lastPos = p.position;
                return;
            }

            // No pointer down: resolve the gesture that just ended.
            if (_tracking)
            {
                if (_isDragging)
                {
                    if (DragEnded != null) DragEnded();
                }
                else if (!_startedOverUi && Time.unscaledTime - _pressTime <= tapMaxDuration)
                {
                    if (Tapped != null) Tapped(_lastPos);
                }
            }
            _tracking = false;
            _isDragging = false;
        }

        Pointer FirstDown()
        {
            for (int i = 0; i < _pointers.Count; i++) if (_pointers[i].down) return _pointers[i];
            return default(Pointer);
        }

        void HandlePinch()
        {
            Pointer a = default(Pointer), b = default(Pointer);
            int found = 0;
            for (int i = 0; i < _pointers.Count && found < 2; i++)
            {
                if (!_pointers[i].down) continue;
                if (found == 0) a = _pointers[i]; else b = _pointers[i];
                found++;
            }
            float dist = Vector2.Distance(a.position, b.position);
            if (!_wasPinching)
            {
                if (_isDragging && DragEnded != null) DragEnded();
                _isDragging = false;
                _tracking = false;
                _wasPinching = true;
                _startedOverUi = IsOverUi(a.id, a.position) || IsOverUi(b.id, b.position);
                _lastPinchDistance = dist;
                return;
            }
            if (_startedOverUi || _lastPinchDistance < 1f) return;
            float factor = dist / _lastPinchDistance;
            _lastPinchDistance = dist;
            if (Mathf.Abs(factor - 1f) > 0.0005f && Pinched != null) Pinched(factor);
        }

        static bool IsOverUi(int pointerId, Vector2 position)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            if (pointerId >= 0 && es.IsPointerOverGameObject(pointerId)) return true;
            if (pointerId < 0 && es.IsPointerOverGameObject()) return true;
            // Fallback raycast (IsPointerOverGameObject can lag by a frame on touch-down).
            var data = new PointerEventData(es) { position = position };
            var results = new List<RaycastResult>();
            es.RaycastAll(data, results);
            return results.Count > 0;
        }

        // ---------------------------------------------------------------- backends

        void ReadPointers()
        {
            _pointers.Clear();
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    bool down = t.phase != TouchPhase.Ended && t.phase != TouchPhase.Canceled;
                    _pointers.Add(new Pointer { id = t.fingerId, position = t.position, down = down });
                }
                return;
            }
            _pointers.Add(new Pointer { id = -1, position = Input.mousePosition, down = Input.GetMouseButton(0) });
#elif ENABLE_INPUT_SYSTEM && SE_INPUT_SYSTEM_PACKAGE
            var ts = UnityEngine.InputSystem.Touchscreen.current;
            if (ts != null)
            {
                foreach (var touch in ts.touches)
                {
                    if (!touch.press.isPressed && !touch.press.wasReleasedThisFrame) continue;
                    _pointers.Add(new Pointer { id = touch.touchId.ReadValue(), position = touch.position.ReadValue(), down = touch.press.isPressed });
                }
                if (_pointers.Count > 0) return;
            }
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
                _pointers.Add(new Pointer { id = -1, position = mouse.position.ReadValue(), down = mouse.leftButton.isPressed });
#endif
        }

        void ReadScrollZoom()
        {
            float scroll = 0f;
#if ENABLE_LEGACY_INPUT_MANAGER
            scroll = Input.mouseScrollDelta.y;
#elif ENABLE_INPUT_SYSTEM && SE_INPUT_SYSTEM_PACKAGE
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null) scroll = mouse.scroll.ReadValue().y / 120f;
#endif
            if (Mathf.Abs(scroll) > 0.01f && Pinched != null && !IsOverUi(-1, MousePosition()))
                Pinched(1f + scroll * 0.1f);
        }

        static Vector2 MousePosition()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#elif ENABLE_INPUT_SYSTEM && SE_INPUT_SYSTEM_PACKAGE
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#else
            return Vector2.zero;
#endif
        }

        /// <summary>True when the platform "back" action was pressed this frame (Android back button / Escape).</summary>
        public static bool BackPressed()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#elif ENABLE_INPUT_SYSTEM && SE_INPUT_SYSTEM_PACKAGE
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#else
            return false;
#endif
        }
    }
}
