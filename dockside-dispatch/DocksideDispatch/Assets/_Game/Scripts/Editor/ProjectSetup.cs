using System.IO;
using Dockside.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Dockside.EditorTools
{
    /// <summary>
    /// One-click (or batch-mode) project setup: materials, palette, Main scene, build settings and app identity.
    /// Menu: Dockside > Setup Project. Batch: -executeMethod Dockside.EditorTools.ProjectSetup.Run
    /// </summary>
    public static class ProjectSetup
    {
        public const string CompanyName = "EQB Business Solutions";
        public const string ProductName = "Dockside Dispatch";
        public const string BundleId = "com.eqbbusiness.docksidedispatch";
        public const string LevelPlayAppKey = "29a53b4f5";   // LevelPlay dashboard > Apps > Dockside Dispatch (Android)
        // LevelPlay dashboard > Ad units (Android). Empty = that format is disabled.
        public const string RewardedAdUnitId = "hrowdp60uf3dioyo";
        public const string InterstitialAdUnitId = "0bssvtrl84y2qzmb";
        public const double RewardedBidFloor = 0, InterstitialBidFloor = 0;   // USD, 0 = no floor

        // GameAnalytics (gameanalytics.com > game settings > Setup). Empty = analytics only logs to the console.
        public const string GameAnalyticsGameKey = "8b5026d7d4b8c8993a6e8a5c9581cbd1";
        public const string GameAnalyticsSecretKey = "c68e14d9948e2d9f7b6eae5c460145441e530a30";
        private const string GameAnalyticsSettingsPath = "Assets/Resources/GameAnalytics/Settings.asset";   // where the SDK looks
        private const string Root = "Assets/_Game";
        private const string ScenePath = Root + "/Scenes/Main.unity";

        [MenuItem("Dockside/Setup Project")]
        public static void Run()
        {
            foreach (var dir in new[] { "Materials", "Settings", "Scenes" })
                Directory.CreateDirectory(Path.Combine(Root, dir));
            AssetDatabase.Refresh();

            // "Lit" objects use the flat-shaded look (exact palette colours, darker camera-facing sides).
            var lit = CreateMaterial(Root + "/Materials/M_Lit.mat", "Dockside/Flat");
            var unlit = CreateMaterial(Root + "/Materials/M_Unlit.mat", "Universal Render Pipeline/Unlit");

            var palettePath = Root + "/Settings/Palette.asset";
            var palette = AssetDatabase.LoadAssetAtPath<Palette>(palettePath);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<Palette>();
                AssetDatabase.CreateAsset(palette, palettePath);
            }
            palette.litBase = lit;
            palette.unlitBase = unlit;
            palette.waterMaterial = CreateMaterial(Root + "/Materials/M_Water.mat", "Dockside/Water");
            EditorUtility.SetDirty(palette);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var game = new GameObject("Game");
            var boot = game.AddComponent<GameBootstrap>();
            var so = new SerializedObject(boot);
            so.FindProperty("palette").objectReferenceValue = palette;
            so.ApplyModifiedPropertiesWithoutUndo();
            // LevelPlay (ads): initializer with the app key from the LevelPlay dashboard.
            var ads = game.AddComponent<LevelPlayInitializer>();
            var adsSo = new SerializedObject(ads);
            adsSo.FindProperty("appKey").stringValue = LevelPlayAppKey;
            adsSo.ApplyModifiedPropertiesWithoutUndo();
            var adManager = game.AddComponent<AdManager>();
            var amSo = new SerializedObject(adManager);
            amSo.FindProperty("rewardedAdUnitId").stringValue = RewardedAdUnitId;
            amSo.FindProperty("interstitialAdUnitId").stringValue = InterstitialAdUnitId;
            amSo.FindProperty("rewardedBidFloor").doubleValue = RewardedBidFloor;
            amSo.FindProperty("interstitialBidFloor").doubleValue = InterstitialBidFloor;
            amSo.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.bundleVersion = "0.4.0";
            PlayerSettings.Android.bundleVersionCode = 4;   // must increase for each build installed over the last

            ConfigureSplash();
            ConfigureMobileRendering();
            ConfigureGameAnalytics();

            AssetDatabase.SaveAssets();
            Debug.Log($"[Dockside] Project setup complete: scene {ScenePath}, bundle id {BundleId}.");
        }

        public const string LogoPath = Root + "/Branding/EQB_Games_Logo.png";
        public const string GameLogoPath = Root + "/Branding/DocksideDispatch_Logo.png";
        public const string GameIconPath = Root + "/Branding/DocksideDispatch_Icon.png";

        /// <summary>
        /// Boot splash: the EQB Games logo, then the Dockside Dispatch logo, on the dusk-plum background
        /// (Unity logo off, allowed from Unity 6). Also sets the app icon.
        /// </summary>
        [MenuItem("Dockside/Configure Splash and Icon")]
        public static void ConfigureSplash()
        {
            var logos = new System.Collections.Generic.List<PlayerSettings.SplashScreenLogo>();
            foreach (var (path, seconds) in new[] { (LogoPath, 2.0f), (GameLogoPath, 2.5f) })
            {
                var sprite = ImportSprite(path);
                if (sprite != null) logos.Add(PlayerSettings.SplashScreenLogo.Create(seconds, sprite));
                else Debug.LogWarning($"[Dockside] Splash logo not found at {path}.");
            }
            if (logos.Count > 0)
            {
                PlayerSettings.SplashScreen.show = true;
                PlayerSettings.SplashScreen.showUnityLogo = false;
                PlayerSettings.SplashScreen.unityLogoStyle = PlayerSettings.SplashScreen.UnityLogoStyle.LightOnDark;
                PlayerSettings.SplashScreen.animationMode = PlayerSettings.SplashScreen.AnimationMode.Dolly;
                PlayerSettings.SplashScreen.backgroundColor = Palette.Hex("2A1B33");
                PlayerSettings.SplashScreen.overlayOpacity = 0f;
                PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.AllSequential;
                PlayerSettings.SplashScreen.logos = logos.ToArray();
            }

            if (File.Exists(GameIconPath))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(GameIconPath);
                if (importer.maxTextureSize != 1024 || importer.textureCompression != TextureImporterCompression.Uncompressed)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.maxTextureSize = 1024;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                }
                var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(GameIconPath);
                PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            }
            Debug.Log($"[Dockside] Splash: {logos.Count} logo(s); app icon {(File.Exists(GameIconPath) ? "set" : "missing")}.");
        }

        private static Sprite ImportSprite(string path)
        {
            if (!File.Exists(path)) return null;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.textureType != TextureImporterType.Sprite || importer.maxTextureSize != 2048)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Writes the GameAnalytics settings asset: Android platform with the game and secret keys.</summary>
        [MenuItem("Dockside/Configure GameAnalytics")]
        public static void ConfigureGameAnalytics()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GameAnalyticsSDK.Setup.Settings>(GameAnalyticsSettingsPath);
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(GameAnalyticsSettingsPath));
                settings = ScriptableObject.CreateInstance<GameAnalyticsSDK.Setup.Settings>();
                AssetDatabase.CreateAsset(settings, GameAnalyticsSettingsPath);
            }
            if (!settings.Platforms.Contains(RuntimePlatform.Android)) settings.AddPlatform(RuntimePlatform.Android);
            int i = settings.Platforms.IndexOf(RuntimePlatform.Android);
            if (!string.IsNullOrEmpty(GameAnalyticsGameKey) && !string.IsNullOrEmpty(GameAnalyticsSecretKey))
            {
                settings.UpdateGameKey(i, GameAnalyticsGameKey);
                settings.UpdateSecretKey(i, GameAnalyticsSecretKey);
            }
            // Resource events (coins earned/spent) must use currencies and item types declared up front.
            settings.ResourceCurrencies = new System.Collections.Generic.List<string> { "coins" };
            settings.ResourceItemTypes = new System.Collections.Generic.List<string> { "level_reward", "booster", "continue" };
            EditorUtility.SetDirty(settings);
            Debug.Log($"[Dockside] GameAnalytics: Android platform {(string.IsNullOrEmpty(GameAnalyticsGameKey) ? "added, keys not set yet" : "configured")}.");
        }

        /// <summary>
        /// Phone rendering: full resolution (the template renders mobile at 0.8x, which looks soft) and 4x MSAA
        /// for clean edges. Tile-based mobile GPUs resolve MSAA cheaply.
        /// </summary>
        [MenuItem("Dockside/Configure Mobile Rendering")]
        public static void ConfigureMobileRendering()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (asset == null)
            {
                Debug.LogWarning("[Dockside] Mobile_RPAsset not found; mobile rendering left unchanged.");
                return;
            }
            asset.renderScale = 1f;
            asset.msaaSampleCount = 4;
            asset.shadowDistance = 30f;
            EditorUtility.SetDirty(asset);
            Debug.Log("[Dockside] Mobile rendering: render scale 1.0, MSAA 4x, shadow distance 30.");
        }

        private static Material CreateMaterial(string path, string shaderName)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[Dockside] Shader '{shaderName}' not found. Is URP installed and active?");
                return null;
            }
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Generates the prototype levels and saves them as JSON under Resources/Levels (Dockside > Bake Levels).</summary>
        [MenuItem("Dockside/Bake Levels")]
        public static void BakeLevels()
        {
            var dir = Path.Combine(Root, "Resources", LevelRepository.Folder);
            Directory.CreateDirectory(dir);
            for (int n = 1; n <= LevelGenerator.AuthoredLevelCount; n++)
            {
                var level = LevelGenerator.Generate(n);
                File.WriteAllText(Path.Combine(dir, LevelRepository.FileName(n) + ".json"), JsonUtility.ToJson(level, true));
                Debug.Log($"[Dockside] Baked level {n}: {level.width}x{level.height}, {level.containers.Count} containers, " +
                          $"{level.ships.Count} ships, casual win rate {level.casualWinRate:0.00}");
            }
            AssetDatabase.Refresh();
        }

        /// <summary>Generates every prototype level and logs its size and solver stats (Dockside > Report Levels).</summary>
        [MenuItem("Dockside/Report Levels")]
        public static void ReportLevels()
        {
            for (int n = 1; n <= LevelGenerator.AuthoredLevelCount; n++)
            {
                var level = LevelGenerator.Generate(n);
                var solve = Solver.Solve(level);
                Debug.Log($"[Dockside] Level {n}: {level.width}x{level.height}, {level.containers.Count} containers, " +
                          $"{level.ships.Count} ships, colours {level.colorCount}, solvable={solve.solvable}, " +
                          $"moves={solve.solution?.Count}, peak holding-bay use={solve.peakBufferUse}, nodes={solve.nodesExplored}");
            }
        }
    }
}
