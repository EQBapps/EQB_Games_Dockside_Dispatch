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
        public static void BuildAndroidApk()
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
            string apk = Path.Combine(dir, $"DocksideDispatch-{PlayerSettings.bundleVersion}-{PlayerSettings.Android.bundleVersionCode}.apk");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Game/Scenes/Main.unity" },
                locationPathName = apk,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var s = report.summary;
            Debug.Log($"[Dockside] Android build {s.result}: {apk} ({s.totalSize / (1024f * 1024f):0.0} MB) in {s.totalTime:mm\\:ss}, " +
                      $"{s.totalErrors} errors, {s.totalWarnings} warnings");
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
