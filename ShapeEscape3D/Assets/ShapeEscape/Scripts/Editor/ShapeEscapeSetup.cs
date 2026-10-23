using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShapeEscape.EditorTools
{
    /// <summary>
    /// One-click project configuration ("Shape Escape" menu). Everything here is idempotent and can be re-run.
    ///  1. Configure Project: URP pipeline + mobile settings, Android player settings (AAB, IL2CPP, ARM64,
    ///     target SDK), input handling, shader-carrying materials, app icons and splash colours.
    ///  2. Create Main Scene: a scene containing the GameManager, added to Build Settings.
    /// </summary>
    public static class ShapeEscapeSetup
    {
        public const string Root = "Assets/ShapeEscape";
        public const string ResourcesDir = Root + "/Resources/ShapeEscape";
        public const string MaterialsDir = ResourcesDir + "/Materials";
        public const string SettingsDir = Root + "/Settings";
        public const string ScenePath = Root + "/Scenes/Main.unity";
        public const string IconDir = Root + "/Art/Icons";

        public const string DefaultPackageName = "com.yourstudio.shapeescape3d";
        public const string ProductName = "Shape Escape 3D";
        /// <summary>
        /// Google Play target API level. Google raises the minimum every year (API 35 since Aug 2025, API 36
        /// expected from Aug 2026) — check Play Console › Policy › "Target API level requirements" before release.
        /// </summary>
        public const int TargetApiLevel = 36;
        public const int MinApiLevel = 24;

        [MenuItem("Shape Escape/1. Configure Project", priority = 1)]
        public static void ConfigureProject()
        {
            EnsureFolders();
            ConfigureRenderPipeline();
            CreateMaterials();
            ConfigurePlayer();
            ConfigureInputHandling();
            GenerateIcons();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Shape Escape] Project configured. If 'Active Input Handling' changed, Unity will ask to restart.");
        }

        [MenuItem("Shape Escape/2. Create Main Scene", priority = 2)]
        public static void CreateMainScene()
        {
            EnsureFolders();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("ShapeEscapeGame").AddComponent<GameManager>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[Shape Escape] Created " + ScenePath + " and added it to Build Settings.");
        }

        static void EnsureFolders()
        {
            foreach (var dir in new[] { ResourcesDir, MaterialsDir, SettingsDir, Root + "/Scenes", IconDir })
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            AssetDatabase.Refresh();
        }

        // ---------------------------------------------------------------- URP

        static RenderPipelineAsset CurrentPipeline
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return GraphicsSettings.defaultRenderPipeline;
#else
                return GraphicsSettings.renderPipelineAsset;
#endif
            }
            set
            {
#if UNITY_6000_0_OR_NEWER
                GraphicsSettings.defaultRenderPipeline = value;
#else
                GraphicsSettings.renderPipelineAsset = value;
#endif
            }
        }

        /// <summary>Creates (via reflection, so the project compiles even without URP) and assigns a mobile URP asset.</summary>
        static void ConfigureRenderPipeline()
        {
            var urpAssetType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset, Unity.RenderPipelines.Universal.Runtime");
            var rendererDataType = Type.GetType("UnityEngine.Rendering.Universal.UniversalRendererData, Unity.RenderPipelines.Universal.Runtime");
            if (urpAssetType == null || rendererDataType == null)
            {
                Debug.LogWarning("[Shape Escape] URP package not found. Install com.unity.render-pipelines.universal (it is listed in Packages/manifest.json).");
                return;
            }

            var pipeline = CurrentPipeline;
            if (pipeline == null || !urpAssetType.IsInstanceOfType(pipeline))
            {
                string assetPath = SettingsDir + "/ShapeEscape_URP.asset";
                pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(assetPath);
                if (pipeline == null)
                {
                    var rendererData = ScriptableObject.CreateInstance(rendererDataType);
                    AssetDatabase.CreateAsset(rendererData, SettingsDir + "/ShapeEscape_URP_Renderer.asset");
                    var create = urpAssetType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
                    pipeline = (RenderPipelineAsset)create.Invoke(null, new object[] { rendererData });
                    AssetDatabase.CreateAsset(pipeline, assetPath);
                }
                CurrentPipeline = pipeline;
            }
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }

            // Mobile-friendly defaults.
            SetIfWritable(pipeline, "supportsHDR", false);
            SetIfWritable(pipeline, "msaaSampleCount", 2);
            SetIfWritable(pipeline, "renderScale", 1f);
            SetIfWritable(pipeline, "shadowDistance", 30f);
            SetIfWritable(pipeline, "shadowCascadeCount", 1);
            EditorUtility.SetDirty(pipeline);
            Debug.Log("[Shape Escape] URP asset assigned: " + AssetDatabase.GetAssetPath(pipeline));
        }

        static void SetIfWritable(object target, string property, object value)
        {
            var p = target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
            if (p == null || !p.CanWrite) return;
            try { p.SetValue(target, Convert.ChangeType(value, p.PropertyType), null); }
            catch (Exception e) { Debug.LogWarning("[Shape Escape] Could not set " + property + ": " + e.Message); }
        }

        // ---------------------------------------------------------------- materials (guarantee shader inclusion)

        static void CreateMaterials()
        {
            bool urp = CurrentPipeline != null;
            CreateMaterial("PieceLit", urp ? "Universal Render Pipeline/Lit" : "Standard");
            CreateMaterial("ArrowUnlit", urp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
            CreateMaterial("FxSprite", "Sprites/Default");
        }

        static void CreateMaterial(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning("[Shape Escape] Shader not found: " + shaderName);
                return;
            }
            string path = MaterialsDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;
            mat.enableInstancing = true;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(mat);
        }

        // ---------------------------------------------------------------- player settings

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = PlayerSettings.companyName == "DefaultCompany" ? "YourStudio" : PlayerSettings.companyName;
            PlayerSettings.productName = ProductName;
            if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1") PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.91f, 0.88f, 1f);
            PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.DarkOnLight;

            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)MinApiLevel;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)TargetApiLevel;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            if (PlayerSettings.Android.bundleVersionCode < 1) PlayerSettings.Android.bundleVersionCode = 1;
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });

#if UNITY_2021_2_OR_NEWER
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            if (PlayerSettings.GetApplicationIdentifier(android) == "com.DefaultCompany." + Application.productName.Replace(" ", "") ||
                string.IsNullOrEmpty(PlayerSettings.GetApplicationIdentifier(android)) ||
                PlayerSettings.GetApplicationIdentifier(android).StartsWith("com.DefaultCompany"))
                PlayerSettings.SetApplicationIdentifier(android, DefaultPackageName);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
#else
            if (PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android).StartsWith("com.DefaultCompany") ||
                string.IsNullOrEmpty(PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)))
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, DefaultPackageName);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
#endif
        }

        /// <summary>Sets Active Input Handling to "Both" so legacy Input and the Input System package both work.</summary>
        static void ConfigureInputHandling()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (settings == null) return;
            var so = new SerializedObject(settings);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue != 2)
            {
                prop.intValue = 2;
                so.ApplyModifiedProperties();
                Debug.Log("[Shape Escape] Active Input Handling set to 'Both'. Restart the editor to apply.");
            }
        }

        // ---------------------------------------------------------------- icons

        [MenuItem("Shape Escape/Generate App Icons", priority = 20)]
        public static void GenerateIcons()
        {
            EnsureFolders();
            var bgPath = IconDir + "/icon_background.png";
            var fgPath = IconDir + "/icon_foreground.png";
            var legacyPath = IconDir + "/icon_legacy.png";
            File.WriteAllBytes(bgPath, IconArt.Background(432).EncodeToPNG());
            File.WriteAllBytes(fgPath, IconArt.Foreground(432).EncodeToPNG());
            File.WriteAllBytes(legacyPath, IconArt.Legacy(512).EncodeToPNG());
            AssetDatabase.Refresh();
            foreach (var p in new[] { bgPath, fgPath, legacyPath })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(p);
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(legacyPath);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { legacy });
            AssignAdaptiveIcons(AssetDatabase.LoadAssetAtPath<Texture2D>(bgPath), AssetDatabase.LoadAssetAtPath<Texture2D>(fgPath));
        }

        /// <summary>Android adaptive icons via reflection (UnityEditor.Android is only present with Android Build Support).</summary>
        static void AssignAdaptiveIcons(Texture2D bg, Texture2D fg)
        {
            var kindType = Type.GetType("UnityEditor.Android.AndroidPlatformIconKind, UnityEditor.Android.Extensions");
            if (kindType == null)
            {
                Debug.Log("[Shape Escape] Android Build Support not installed: skipped adaptive icons (legacy icon set).");
                return;
            }
            var adaptive = kindType.GetProperty("Adaptive", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
            var getIcons = typeof(PlayerSettings).GetMethods().FirstOrDefault(m => m.Name == "GetPlatformIcons" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(BuildTargetGroup));
            var setIcons = typeof(PlayerSettings).GetMethods().FirstOrDefault(m => m.Name == "SetPlatformIcons" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(BuildTargetGroup));
            if (getIcons == null || setIcons == null) return;
            var icons = (Array)getIcons.Invoke(null, new[] { (object)BuildTargetGroup.Android, adaptive });
            foreach (var icon in icons)
            {
                var setTextures = icon.GetType().GetMethod("SetTextures");
                if (setTextures != null) setTextures.Invoke(icon, new object[] { new[] { bg, fg } });
            }
            setIcons.Invoke(null, new[] { (object)BuildTargetGroup.Android, adaptive, icons });
            Debug.Log("[Shape Escape] Adaptive icons assigned.");
        }
    }

    /// <summary>Procedural app icon: pastel gradient + a stack of rounded cubes with an arrow (the game's motif).</summary>
    public static class IconArt
    {
        static readonly Color Top = new Color(0.62f, 0.55f, 1f);
        static readonly Color Bottom = new Color(1f, 0.6f, 0.62f);
        static readonly Color Coral = new Color(1f, 0.42f, 0.42f);
        static readonly Color Teal = new Color(0.31f, 0.8f, 0.77f);
        static readonly Color Gold = new Color(1f, 0.82f, 0.4f);
        static readonly Color Ink = new Color(0.17f, 0.18f, 0.26f);

        public static Texture2D Background(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    t.SetPixel(x, y, Color.Lerp(Bottom, Top, (y + x * 0.3f) / (size * 1.3f)));
            t.Apply();
            return t;
        }

        /// <summary>Foreground drawn inside the 66% adaptive-icon safe zone.</summary>
        public static Texture2D Foreground(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    t.SetPixel(x, y, clear);
            DrawMotif(t, size, 0.5f);
            t.Apply();
            return t;
        }

        public static Texture2D Legacy(int size)
        {
            var t = Background(size);
            DrawMotif(t, size, 0.7f);
            t.Apply();
            return t;
        }

        static void DrawMotif(Texture2D t, int size, float scale)
        {
            float cell = size * scale / 2.4f;
            float cx = size * 0.5f, cy = size * 0.5f;
            DrawCube(t, cx - cell * 0.55f, cy - cell * 0.55f, cell, Teal);
            DrawCube(t, cx + cell * 0.55f, cy - cell * 0.55f, cell, Gold);
            DrawCube(t, cx - cell * 0.55f, cy + cell * 0.55f, cell, Coral);
            // Escaping cube with arrow.
            DrawCube(t, cx + cell * 0.75f, cy + cell * 0.75f, cell, Color.white);
            DrawArrow(t, cx + cell * 0.75f, cy + cell * 0.75f, cell * 0.36f);
        }

        static void DrawCube(Texture2D t, float cx, float cy, float s, Color c)
        {
            float half = s * 0.46f, r = s * 0.16f;
            int x0 = Mathf.FloorToInt(cx - half - 2), x1 = Mathf.CeilToInt(cx + half + 2);
            int y0 = Mathf.FloorToInt(cy - half - 2), y1 = Mathf.CeilToInt(cy + half + 2);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (x < 0 || y < 0 || x >= t.width || y >= t.height) continue;
                    float qx = Mathf.Abs(x + 0.5f - cx) - half + r, qy = Mathf.Abs(y + 0.5f - cy) - half + r;
                    float d = Mathf.Sqrt(Mathf.Max(qx, 0) * Mathf.Max(qx, 0) + Mathf.Max(qy, 0) * Mathf.Max(qy, 0)) + Mathf.Min(Mathf.Max(qx, qy), 0) - r;
                    float a = Mathf.Clamp01(0.5f - d);
                    if (a <= 0) continue;
                    float shade = 1f - 0.18f * Mathf.Clamp01((cy - (y + 0.5f)) / half * 0.5f + 0.5f);
                    var col = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
                    var dst = t.GetPixel(x, y);
                    t.SetPixel(x, y, Color.Lerp(dst, col, a) + new Color(0, 0, 0, a));
                }
        }

        static void DrawArrow(Texture2D t, float cx, float cy, float s)
        {
            for (int y = Mathf.FloorToInt(cy - s); y <= cy + s; y++)
                for (int x = Mathf.FloorToInt(cx - s); x <= cx + s; x++)
                {
                    if (x < 0 || y < 0 || x >= t.width || y >= t.height) continue;
                    float u = (x + 0.5f - cx) / s, v = (y + 0.5f - cy) / s;
                    bool shaft = u > -0.9f && u < 0.1f && Mathf.Abs(v) < 0.22f;
                    bool head = u >= 0.0f && u < 0.9f && Mathf.Abs(v) < 0.65f * (0.9f - u) / 0.9f;
                    if (shaft || head) t.SetPixel(x, y, Ink);
                }
        }
    }
}
