using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Object-inspection controls. Instead of orbiting the camera, the controller rotates the *object* (a target
    /// transform) so the key light stays flattering from the player's point of view; zoom moves the camera.
    /// Features: free 360° trackball rotation with inertia, pinch/scroll zoom with limits derived from the
    /// object's bounding sphere (the camera can never clip into the object), automatic framing, smooth reset,
    /// and smooth "look at this face" focusing for hints.
    /// </summary>
    public sealed class CameraController : MonoBehaviour
    {
        public static readonly Quaternion DefaultRotation = Quaternion.Euler(18f, -32f, 0f);

        [Tooltip("Degrees of rotation per screen-height of drag.")]
        public float rotateSensitivity = 260f;
        [Tooltip("How quickly the spin decays after release (higher = stops sooner).")]
        public float inertiaDamping = 4.5f;
        public float rotationSharpness = 18f;
        public float zoomSharpness = 12f;

        Camera _cam;
        Transform _target;
        Quaternion _targetRotation = DefaultRotation;
        Vector2 _angularVelocity;
        bool _dragging;
        float _distance = 10f;
        float _targetDistance = 10f;
        float _minDistance = 3f;
        float _maxDistance = 30f;
        float _fitDistance = 10f;
        float _radius = 2f;
        float _verticalOffset;
        float _distanceScale = 1f;
        float _autoSpin;

        public bool UserControlEnabled { get; set; }
        public Transform Target { get { return _target; } }

        public void Initialize(Camera cam)
        {
            _cam = cam;
            UserControlEnabled = true;
        }

        /// <summary>Attaches to a new object and frames it. Keeps the current rotation unless <paramref name="resetRotation"/>.</summary>
        public void SetTarget(Transform target, float boundingRadius, bool resetRotation = true, bool instant = true)
        {
            _target = target;
            _radius = Mathf.Max(0.5f, boundingRadius);
            ComputeLimits();
            if (resetRotation) _targetRotation = DefaultRotation;
            _targetDistance = _fitDistance;
            _angularVelocity = Vector2.zero;
            if (instant)
            {
                _distance = _targetDistance;
                if (_target != null) _target.localRotation = _targetRotation;
                ApplyCamera();
            }
        }

        /// <summary>Slow idle spin (home screen, collection viewer, level complete).</summary>
        public void SetAutoSpin(float degreesPerSecond)
        {
            _autoSpin = degreesPerSecond;
        }

        /// <summary>
        /// Framing per screen: <paramref name="distanceScale"/> &gt; 1 shows the object smaller, and
        /// <paramref name="verticalOffset"/> (fraction of the radius) moves it up to clear bottom UI panels.
        /// </summary>
        public void SetFraming(float distanceScale, float verticalOffset)
        {
            _distanceScale = Mathf.Max(0.5f, distanceScale);
            _verticalOffset = verticalOffset;
            ComputeLimits();
            _targetDistance = _fitDistance;
        }

        void ComputeLimits()
        {
            if (_cam == null) return;
            float vFov = _cam.fieldOfView * Mathf.Deg2Rad;
            float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f) * Mathf.Max(0.3f, _cam.aspect));
            float fov = Mathf.Min(vFov, hFov);
            // Distance at which the bounding sphere fills ~82% of the narrower view.
            _fitDistance = _radius / Mathf.Sin(fov * 0.5f) / 0.82f * _distanceScale;
            _minDistance = Mathf.Max(_radius * 1.25f + _cam.nearClipPlane, _fitDistance * 0.45f);
            _maxDistance = _fitDistance * 2.2f;
        }

        public void ResetView()
        {
            _targetRotation = DefaultRotation;
            _targetDistance = _fitDistance;
            _angularVelocity = Vector2.zero;
        }

        // ---------------------------------------------------------------- input hooks (from TouchInputManager)

        public void OnDragBegin()
        {
            if (!UserControlEnabled) return;
            _dragging = true;
            _angularVelocity = Vector2.zero;
        }

        public void OnDrag(Vector2 deltaPixels)
        {
            if (!UserControlEnabled || _target == null) return;
            float scale = rotateSensitivity / Mathf.Max(320f, Screen.height);
            Vector2 deg = deltaPixels * scale;
            RotateBy(deg);
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1f / 120f);
            _angularVelocity = Vector2.Lerp(_angularVelocity, deg / dt, 0.5f);
        }

        public void OnDragEnd()
        {
            _dragging = false;
            if (_angularVelocity.magnitude > 900f) _angularVelocity = _angularVelocity.normalized * 900f;
        }

        /// <summary>factor &gt; 1 zooms in.</summary>
        public void OnZoom(float factor)
        {
            if (!UserControlEnabled) return;
            _targetDistance = Mathf.Clamp(_targetDistance / Mathf.Max(0.01f, factor), _minDistance, _maxDistance);
        }

        void RotateBy(Vector2 deg)
        {
            // Horizontal drag spins around the screen's up axis, vertical drag around its right axis (trackball).
            var up = _cam != null ? _cam.transform.up : Vector3.up;
            var right = _cam != null ? _cam.transform.right : Vector3.right;
            _targetRotation = Quaternion.AngleAxis(-deg.x, up) * Quaternion.AngleAxis(deg.y, right) * _targetRotation;
        }

        /// <summary>Rotates the object so that its local direction <paramref name="localDir"/> faces the camera.</summary>
        public void FocusLocalDirection(Vector3 localDir)
        {
            if (_target == null || _cam == null) return;
            Vector3 toCamera = -_cam.transform.forward;
            Vector3 current = _targetRotation * localDir.normalized;
            _targetRotation = Quaternion.FromToRotation(current, toCamera) * _targetRotation;
            // Keep a slight tilt so the player sees depth, not a flat face.
            _targetRotation = Quaternion.AngleAxis(-14f, _cam.transform.right) * Quaternion.AngleAxis(12f, _cam.transform.up) * _targetRotation;
            _angularVelocity = Vector2.zero;
        }

        void LateUpdate()
        {
            if (_target == null || _cam == null) return;
            float dt = Time.unscaledDeltaTime;

            if (!_dragging && _angularVelocity.sqrMagnitude > 0.01f)
            {
                RotateBy(_angularVelocity * dt);
                _angularVelocity *= Mathf.Exp(-inertiaDamping * dt);
            }
            if (_autoSpin != 0f && !_dragging)
                _targetRotation = Quaternion.AngleAxis(_autoSpin * dt, Vector3.up) * _targetRotation;

            _target.localRotation = Quaternion.Slerp(_target.localRotation, _targetRotation, Tween.Damp(rotationSharpness, dt));
            _distance = Mathf.Lerp(_distance, _targetDistance, Tween.Damp(zoomSharpness, dt));
            ApplyCamera();
        }

        void ApplyCamera()
        {
            if (_cam == null) return;
            var pivot = _target != null ? _target.position : Vector3.zero;
            pivot.y -= _verticalOffset * _radius;
            _cam.transform.position = pivot - Vector3.forward * _distance;
            _cam.transform.rotation = Quaternion.identity;
        }

        void OnRectTransformDimensionsChange() { ComputeLimits(); }

        /// <summary>Re-evaluates zoom limits (call after orientation/aspect changes).</summary>
        public void RefreshLimits()
        {
            ComputeLimits();
            _targetDistance = Mathf.Clamp(_targetDistance, _minDistance, _maxDistance);
        }
    }
}
