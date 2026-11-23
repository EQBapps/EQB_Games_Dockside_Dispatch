#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;

namespace Dockside.EditorTools
{
    /// <summary>
    /// Finishes the generated Xcode project for the App Store:
    ///  - Info.plist: the App Tracking Transparency prompt text (replaces the generic text an SDK may have added) and
    ///    ITSAppUsesNonExemptEncryption = NO (the game only uses HTTPS), so uploads skip the export-compliance question.
    ///  - Links AppTrackingTransparency.framework into UnityFramework, where the native bridge (DocksideNative.mm) lives.
    ///  - Game Center capability (entitlement and GameKit) for achievements.
    /// Runs after the SDKs' own post-build steps. Only compiled while the project's platform is iOS, like theirs.
    /// </summary>
    public class IosPostProcess : IPostprocessBuildWithReport
    {
        public int callbackOrder => 900;

        public const string TrackingPrompt =
            "Allow tracking so our ad partners can show you ads that suit you. Dockside Dispatch stays free and works the same either way.";

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            string path = report.summary.outputPath;

            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSUserTrackingUsageDescription", TrackingPrompt);
            plist.root.SetBoolean("ITSAppUsesNonExemptEncryption", false);
            plist.WriteToFile(plistPath);

            string projPath = PBXProject.GetPBXProjectPath(path);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);
            proj.AddFrameworkToProject(proj.GetUnityFrameworkTargetGuid(), "AppTrackingTransparency.framework", true);
            proj.WriteToFile(projPath);

            var caps = new ProjectCapabilityManager(projPath, "Unity-iPhone/DocksideDispatch.entitlements", null, proj.GetUnityMainTargetGuid());
            caps.AddGameCenter();
            caps.WriteToFile();

            UnityEngine.Debug.Log("[Dockside] iOS post-build: tracking prompt text, encryption flag, AppTrackingTransparency, Game Center.");
        }
    }
}
#endif
