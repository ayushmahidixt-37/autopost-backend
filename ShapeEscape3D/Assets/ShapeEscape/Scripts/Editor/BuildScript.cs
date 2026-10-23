using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShapeEscape.EditorTools
{
    /// <summary>
    /// Android builds from the menu or the command line.
    ///
    /// Menu:  Shape Escape > Build > Android App Bundle (Release)  -> Builds/Android/*.aab (upload to Play Console)
    ///        Shape Escape > Build > Android APK (Development)      -> Builds/Android/*.apk (install on a test phone)
    ///
    /// CI / command line:
    ///   Unity -batchmode -quit -projectPath . -buildTarget Android \
    ///         -executeMethod ShapeEscape.EditorTools.BuildScript.CiReleaseAab
    ///
    /// Release signing comes from environment variables so secrets never live in the repository:
    ///   SE_KEYSTORE_PATH, SE_KEYSTORE_PASS, SE_KEY_ALIAS, SE_KEY_PASS   (and optionally SE_VERSION_CODE)
    /// When they are missing, the keystore already configured in Player Settings > Publishing Settings is used.
    /// </summary>
    public static class BuildScript
    {
        const string OutputDir = "Builds/Android";

        [MenuItem("Shape Escape/Build/Android App Bundle (Release)", priority = 40)]
        public static void ReleaseAab() { Build(true, true); }

        [MenuItem("Shape Escape/Build/Android APK (Development)", priority = 41)]
        public static void DevelopmentApk() { Build(false, false); }

        /// <summary>Entry point for -executeMethod; exits the editor with a non-zero code on failure.</summary>
        public static void CiReleaseAab()
        {
            bool ok = Build(true, true);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        public static void CiDevelopmentApk()
        {
            bool ok = Build(false, false);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static bool Build(bool release, bool appBundle)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] No scenes in Build Settings. Run 'Shape Escape > 2. Create Main Scene' first.");
                return false;
            }

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            string code = Environment.GetEnvironmentVariable("SE_VERSION_CODE");
            int versionCode;
            if (!string.IsNullOrEmpty(code) && int.TryParse(code, out versionCode)) PlayerSettings.Android.bundleVersionCode = versionCode;

            if (release && !ConfigureSigning()) return false;

#if UNITY_2018_3_OR_NEWER
            EditorUserBuildSettings.buildAppBundle = appBundle;
#endif
            Directory.CreateDirectory(OutputDir);
            string file = string.Format("{0}/ShapeEscape3D-{1}-{2}{3}.{4}", OutputDir, PlayerSettings.bundleVersion,
                PlayerSettings.Android.bundleVersionCode, release ? "" : "-dev", appBundle ? "aab" : "apk");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = file,
                target = BuildTarget.Android,
                options = release ? BuildOptions.None : BuildOptions.Development | BuildOptions.AllowDebugging
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[Build] " + summary.result + " with " + summary.totalErrors + " error(s). See the Console for details.");
                return false;
            }
            Debug.Log(string.Format("[Build] {0} ({1:0.0} MB) in {2:0}s", file, summary.totalSize / (1024.0 * 1024.0), summary.totalTime.TotalSeconds));
            return true;
        }

        static bool ConfigureSigning()
        {
            string path = Environment.GetEnvironmentVariable("SE_KEYSTORE_PATH");
            if (!string.IsNullOrEmpty(path))
            {
                if (!File.Exists(path))
                {
                    Debug.LogError("[Build] SE_KEYSTORE_PATH does not exist: " + path);
                    return false;
                }
#if UNITY_2019_1_OR_NEWER
                PlayerSettings.Android.useCustomKeystore = true;
#endif
                PlayerSettings.Android.keystoreName = path;
                PlayerSettings.Android.keystorePass = Environment.GetEnvironmentVariable("SE_KEYSTORE_PASS") ?? "";
                PlayerSettings.Android.keyaliasName = Environment.GetEnvironmentVariable("SE_KEY_ALIAS") ?? "";
                PlayerSettings.Android.keyaliasPass = Environment.GetEnvironmentVariable("SE_KEY_PASS") ?? "";
                return true;
            }
            if (string.IsNullOrEmpty(PlayerSettings.Android.keystoreName))
            {
                Debug.LogError("[Build] Release builds must be signed. Create an upload keystore (Player Settings > Publishing Settings > Keystore Manager) " +
                               "or set SE_KEYSTORE_PATH / SE_KEYSTORE_PASS / SE_KEY_ALIAS / SE_KEY_PASS.");
                return false;
            }
            return true;
        }
    }
}
