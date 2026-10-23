using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Camera, lighting, pastel gradient background and soft blob shadow. Everything is created in code so the
    /// game runs from an empty scene; if the scene already has a Main Camera / Directional Light they are reused.
    /// </summary>
    public sealed class SceneEnvironment : MonoBehaviour
    {
        public Camera MainCamera { get; private set; }
        public Light KeyLight { get; private set; }

        Texture2D _gradient;
        MeshRenderer _background;
        Transform _shadow;
        Color _top, _bottom, _targetTop, _targetBottom;

        public static readonly Color DefaultTop = ColorUtil.Hex("#E8E1FF");
        public static readonly Color DefaultBottom = ColorUtil.Hex("#FFE5D9");

        public void Initialize()
        {
            MainCamera = Camera.main;
            if (MainCamera == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                MainCamera = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            MainCamera.transform.SetParent(null);
            MainCamera.transform.position = new Vector3(0, 0, -10);
            MainCamera.transform.rotation = Quaternion.identity;
            MainCamera.fieldOfView = 38f;
            MainCamera.nearClipPlane = 0.1f;
            MainCamera.farClipPlane = 120f;
            MainCamera.clearFlags = CameraClearFlags.SolidColor;
            MainCamera.backgroundColor = DefaultBottom;
            MainCamera.allowHDR = false;
            MainCamera.allowMSAA = true;

            foreach (var l in FindObjectsByTypeCompat<Light>())
                if (l.type == LightType.Directional) { KeyLight = l; break; }
            if (KeyLight == null)
            {
                KeyLight = new GameObject("Key Light").AddComponent<Light>();
                KeyLight.type = LightType.Directional;
            }
            KeyLight.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            KeyLight.color = ColorUtil.Hex("#FFF6EA");
            KeyLight.intensity = 1.05f;
            KeyLight.shadows = LightShadows.Soft;
            KeyLight.shadowStrength = 0.55f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ColorUtil.Hex("#D9D4FF");
            RenderSettings.ambientEquatorColor = ColorUtil.Hex("#FFE3D8");
            RenderSettings.ambientGroundColor = ColorUtil.Hex("#C9B8C8");
            RenderSettings.fog = false;

            BuildBackground();
            BuildShadow();
            SetBackground(DefaultTop, DefaultBottom, true);
        }

        static T[] FindObjectsByTypeCompat<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return FindObjectsOfType<T>();
#endif
        }

        void BuildBackground()
        {
            _gradient = TextureFactory.VerticalGradient(DefaultBottom, DefaultTop);
            var go = new GameObject("Background");
            go.transform.SetParent(MainCamera.transform, false);
            float dist = MainCamera.farClipPlane * 0.9f;
            float h = 2f * dist * Mathf.Tan(MainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.15f;
            go.transform.localPosition = new Vector3(0, 0, dist);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(h * 2.4f, h, 1f); // wide enough for tablets/landscape
            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Quad;
            _background = go.AddComponent<MeshRenderer>();
            var mat = new Material(MaterialLibrary.Fx(_gradient)) { name = "Background", renderQueue = 1000 };
            _background.sharedMaterial = mat;
            _background.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _background.receiveShadows = false;
        }

        void BuildShadow()
        {
            var go = new GameObject("BlobShadow");
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0, 0);
            go.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = new Material(MaterialLibrary.Fx(TextureFactory.SoftCircle)) { name = "BlobShadow" };
            MaterialLibrary.SetColor(r.sharedMaterial, new Color(0.35f, 0.25f, 0.45f, 0.22f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _shadow = go.transform;
            SetShadow(2f, false);
        }

        /// <summary>Places the soft contact shadow under an object of the given radius.</summary>
        public void SetShadow(float radius, bool visible)
        {
            if (_shadow == null) return;
            _shadow.gameObject.SetActive(visible);
            _shadow.position = new Vector3(0, -radius * 0.95f, 0);
            _shadow.localScale = new Vector3(radius * 2.4f, radius * 1.4f, 1f);
        }

        /// <summary>Cross-fades the background gradient (room themes, level complete).</summary>
        public void SetBackground(Color top, Color bottom, bool instant = false)
        {
            _targetTop = top;
            _targetBottom = bottom;
            if (instant)
            {
                _top = top;
                _bottom = bottom;
                ApplyGradient();
            }
        }

        void Update()
        {
            if (_gradient == null) return;
            if (ApproximatelyEqual(_top, _targetTop) && ApproximatelyEqual(_bottom, _targetBottom)) return;
            float k = Tween.Damp(4f, Time.unscaledDeltaTime);
            _top = Color.Lerp(_top, _targetTop, k);
            _bottom = Color.Lerp(_bottom, _targetBottom, k);
            ApplyGradient();
        }

        static bool ApproximatelyEqual(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.003f;
        }

        void ApplyGradient()
        {
            int h = _gradient.height;
            for (int y = 0; y < h; y++) _gradient.SetPixel(0, y, Color.Lerp(_bottom, _top, y / (float)(h - 1)));
            _gradient.Apply(false, false);
            if (MainCamera != null) MainCamera.backgroundColor = _bottom;
        }

        public void SetShadowsEnabled(bool enabled)
        {
            if (KeyLight != null) KeyLight.shadows = enabled ? LightShadows.Soft : LightShadows.None;
        }
    }
}
