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
        private const string Root = "Assets/_Game";
        private const string ScenePath = Root + "/Scenes/Main.unity";

        [MenuItem("Dockside/Setup Project")]
        public static void Run()
        {
            foreach (var dir in new[] { "Materials", "Settings", "Scenes" })
                Directory.CreateDirectory(Path.Combine(Root, dir));
            AssetDatabase.Refresh();

            var lit = CreateMaterial(Root + "/Materials/M_Lit.mat", "Universal Render Pipeline/Lit");
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
            EditorUtility.SetDirty(palette);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var game = new GameObject("Game");
            var boot = game.AddComponent<GameBootstrap>();
            var so = new SerializedObject(boot);
            so.FindProperty("palette").objectReferenceValue = palette;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.bundleVersion = "0.1.0";

            AssetDatabase.SaveAssets();
            Debug.Log($"[Dockside] Project setup complete: scene {ScenePath}, bundle id {BundleId}.");
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
