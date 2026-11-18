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

        /// <summary>Both APKs in one go (batch: -executeMethod Dockside.EditorTools.BuildTools.BuildAndroidBoth).</summary>
        public static void BuildAndroidBoth()
        {
            bool ok = Build(true, exitWhenDone: false) && Build(false, exitWhenDone: false);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Build(bool adTestSuite, bool exitWhenDone = true, bool testTools = false)
        {
            ProjectSetup.ConfigureSplash();
            ProjectSetup.ConfigureMobileRendering();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            EditorUserBuildSettings.buildAppBundle = false;      // APK for sideloading; Play Store builds use an AAB
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;   // Android 8.0+
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.useCustomKeystore = false;    // Unity's debug key; fine for testing, not for the store
            PlayerSettings.Android.bundleVersionCode = Math.Max(PlayerSettings.Android.bundleVersionCode, 1);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "Android"));
            Directory.CreateDirectory(dir);
            string apk = Path.Combine(dir, $"DocksideDispatch-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}{(adTestSuite ? "-adtest" : "")}{(testTools ? "-testtools" : "")}.apk");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Game/Scenes/Main.unity" },
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = adTestSuite ? BuildOptions.Development : BuildOptions.None,
                extraScriptingDefines = testTools ? new[] { "DOCKSIDE_TEST_TOOLS" } : null,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Dockside] Android build {s.result}: {apk} ({s.totalSize / (1024f * 1024f):0.0} MB) in {s.totalTime:mm\\:ss}, " +
                      $"{s.totalErrors} errors, {s.totalWarnings} warnings");
            bool ok = s.result == BuildResult.Succeeded;
            if (exitWhenDone && Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
            return ok;
        }
    }
}
