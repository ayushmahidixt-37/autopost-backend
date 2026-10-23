using System.Collections.Generic;
using UnityEngine;

namespace ShapeEscape
{
    /// <summary>
    /// Central, cached material factory. All pieces of one colour share one material, which keeps draw calls low
    /// and works with the URP SRP Batcher. Template materials are loaded from Resources/ShapeEscape/Materials
    /// (created by "Shape Escape > Configure Project") so the right shaders are always included in builds; when
    /// they are missing (fresh checkout in the editor) the library falls back to Shader.Find.
    /// </summary>
    public static class MaterialLibrary
    {
        static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();
        static Material _litTemplate;
        static Material _unlitTemplate;
        static Material _fxTemplate;

        public const string ResourcePath = "ShapeEscape/Materials/";

        static Material LitTemplate
        {
            get
            {
                if (_litTemplate == null)
                    _litTemplate = LoadOrCreate("PieceLit", "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit", "Standard");
                return _litTemplate;
            }
        }

        static Material UnlitTemplate
        {
            get
            {
                if (_unlitTemplate == null)
                    _unlitTemplate = LoadOrCreate("ArrowUnlit", "Universal Render Pipeline/Unlit", "Unlit/Color", "Sprites/Default");
                return _unlitTemplate;
            }
        }

        static Material FxTemplate
        {
            get
            {
                if (_fxTemplate == null)
                    _fxTemplate = LoadOrCreate("FxSprite", "Sprites/Default", "Universal Render Pipeline/Particles/Unlit", "Unlit/Transparent");
                return _fxTemplate;
            }
        }

        static Material LoadOrCreate(string resourceName, params string[] shaderNames)
        {
            var mat = Resources.Load<Material>(ResourcePath + resourceName);
            if (mat != null) return mat;
            foreach (var name in shaderNames)
            {
                var shader = Shader.Find(name);
                if (shader != null) return new Material(shader) { name = resourceName + " (runtime)" };
            }
            Debug.LogWarning("[MaterialLibrary] No shader found for " + resourceName + ", using default.");
            return new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        static string Key(string kind, Color c, float extra)
        {
            return kind + ColorUtility.ToHtmlStringRGBA(c) + extra.ToString("0.00");
        }

        /// <summary>Soft, slightly glossy lit material (toy-like plastic).</summary>
        public static Material Lit(Color color, float smoothness = 0.35f)
        {
            string key = Key("lit", color, smoothness);
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(LitTemplate) { name = "Lit " + ColorUtility.ToHtmlStringRGB(color) };
            SetColor(m, color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            m.enableInstancing = true;
            Cache[key] = m;
            return m;
        }

        /// <summary>Flat unlit colour, used for arrows so they stay readable under any lighting.</summary>
        public static Material Unlit(Color color)
        {
            string key = Key("unlit", color, 0);
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(UnlitTemplate) { name = "Unlit " + ColorUtility.ToHtmlStringRGB(color) };
            SetColor(m, color);
            m.enableInstancing = true;
            Cache[key] = m;
            return m;
        }

        /// <summary>Alpha-blended, vertex-coloured material for particles, background gradients and blob shadows.</summary>
        public static Material Fx(Texture texture = null)
        {
            string key = "fx" + (texture != null ? texture.GetInstanceID().ToString() : "none");
            Material m;
            if (Cache.TryGetValue(key, out m) && m != null) return m;
            m = new Material(FxTemplate) { name = "Fx " + (texture != null ? texture.name : "plain") };
            if (texture != null)
            {
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", texture);
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            }
            Cache[key] = m;
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        /// <summary>Releases cached runtime materials (call when changing quality profiles or on low-memory warnings).</summary>
        public static void ClearCache()
        {
            foreach (var kv in Cache)
                if (kv.Value != null) Object.Destroy(kv.Value);
            Cache.Clear();
        }
    }
}
