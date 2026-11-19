using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Dockside.EditorTools
{
    /// <summary>
    /// Android test builds. Menu: Dockside > Build Android APK.
    /// Batch: -buildTarget Android -executeMethod Dockside.EditorTools.BuildTools.BuildAndroidApk
    /// Output: Builds/Android/DocksideDispatch-&lt;version&gt;-&lt;build&gt;.apk (debug-signed, for sideloading onto test phones).
    /// Play Store: Dockside > Build Play Store AAB (batch: -executeMethod Dockside.EditorTools.BuildTools.BuildPlayStoreAab), signed with the
    /// upload key described by a properties file outside the repo (see <see cref="SigningFile"/>).
    /// </summary>
    public static class BuildTools
    {
        [MenuItem("Dockside/Build Android APK")]
        public static void BuildAndroidApk() => Build(false);

        /// <summary>Development build that opens the LevelPlay Test Suite after ads initialize (validates real ad networks).</summary>
        [MenuItem("Dockside/Build Android APK (ad test suite)")]
        public static void BuildAndroidApkAdTest() => Build(true);

        /// <summary>Release-style build with the test tools (Go to level, Skip level, Sky preview) compiled in.</summary>
        [MenuItem("Dockside/Build Android APK (test tools)")]
        public static void BuildAndroidApkTestTools() => Build(false, testTools: true);

        /// <summary>Signed app bundle for Google Play (upload key; Play App Signing re-signs it for players).</summary>
        [MenuItem("Dockside/Build Play Store AAB")]
        public static void BuildPlayStoreAab() => Build(false, store: true);

        /// <summary>
        /// Upload-key properties file (storeFile, keyAlias, storePassword, keyPassword). Never committed: lives next to the keystore,
        /// default %USERPROFILE%/Documents/EQB_Keys/dockside-dispatch-upload.properties, or the path in DOCKSIDE_SIGNING.
        /// </summary>
        private static string SigningFile =>
            Environment.GetEnvironmentVariable("DOCKSIDE_SIGNING") is { Length: > 0 } env ? env
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "EQB_Keys", "dockside-dispatch-upload.properties");

        private static bool ApplyUploadKey()
        {
            string file = SigningFile;
            if (!File.Exists(file)) { Debug.LogError($"[Dockside] Signing properties not found: {file}"); return false; }
            var props = new System.Collections.Generic.Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(file))
            {
                var t = line.Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                int eq = t.IndexOf('=');
                if (eq > 0) props[t.Substring(0, eq).Trim()] = t.Substring(eq + 1).Trim();
            }
            if (!props.TryGetValue("storeFile", out var store) || !File.Exists(store)) { Debug.LogError($"[Dockside] Keystore not found: {store}"); return false; }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = store;
            PlayerSettings.Android.keystorePass = props["storePassword"];
            PlayerSettings.Android.keyaliasName = props["keyAlias"];
            PlayerSettings.Android.keyaliasPass = props["keyPassword"];
            return true;
        }

        /// <summary>Back to Unity's debug key so the keystore path never lands in ProjectSettings.asset.</summary>
        private static void ClearUploadKey()
        {
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.keystoreName = "";
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasName = "";
            PlayerSettings.Android.keyaliasPass = "";
            AssetDatabase.SaveAssets();
        }

        /// <summary>Both APKs in one go (batch: -executeMethod Dockside.EditorTools.BuildTools.BuildAndroidBoth).</summary>
        public static void BuildAndroidBoth()
        {
            bool ok = Build(true, exitWhenDone: false) && Build(false, exitWhenDone: false);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Build(bool adTestSuite, bool exitWhenDone = true, bool testTools = false, bool store = false)
        {
            ProjectSetup.ConfigureSplash();
            ProjectSetup.ConfigureNotifications();
            ProjectSetup.ConfigureMobileRendering();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            EditorUserBuildSettings.buildAppBundle = store;      // APK for sideloading; the Play Store takes an AAB
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;   // Android 8.0+
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.useCustomKeystore = false;    // Unity's debug key for test APKs; store builds use the upload key below
            PlayerSettings.Android.bundleVersionCode = Math.Max(PlayerSettings.Android.bundleVersionCode, 1);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Android"));
            Directory.CreateDirectory(dir);
            string apk = Path.Combine(dir, $"DocksideDispatch-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}{(adTestSuite ? "-adtest" : "")}{(testTools ? "-testtools" : "")}.{(store ? "aab" : "apk")}");
            if (store)
            {
                if (adTestSuite || testTools) { Debug.LogError("[Dockside] Store builds can't include the ad test suite or test tools."); return Finish(false, exitWhenDone); }
                if (!ApplyUploadKey()) return Finish(false, exitWhenDone);
                // Native symbol tables inside the bundle, so Play Console can symbolicate crashes and ANRs.
                UnityEditor.Android.UserBuildSettings.DebugSymbols.level = Unity.Android.Types.DebugSymbolLevel.SymbolTable;
                UnityEditor.Android.UserBuildSettings.DebugSymbols.format = Unity.Android.Types.DebugSymbolFormat.IncludeInBundle;
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Game/Scenes/Main.unity" },
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = adTestSuite ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = testTools ? new[] { "DOCKSIDE_TEST_TOOLS" } : null,
            };
            BuildReport report;
            try { report = BuildPipeline.BuildPlayer(options); }
            finally { if (store) ClearUploadKey(); }
            var s = report.summary;
            Debug.Log($"[Dockside] Android build {s.result}: {apk} ({s.totalSize / (1024f * 1024f):0.0} MB) in {s.totalTime:mm\\:ss}, " +
                      $"{s.totalErrors} errors, {s.totalWarnings} warnings");
            return Finish(s.result == BuildResult.Succeeded, exitWhenDone);
        }

        private static bool Finish(bool ok, bool exitWhenDone)
        {
            if (exitWhenDone && Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
            return ok;
        }
    }
}
