using Dockside.Core;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Loads levels baked to Resources/Levels/level_NN.json (Dockside > Bake Levels).
    /// Falls back to generating at runtime if a file is missing. After the last baked level,
    /// the late-game levels repeat so the prototype never runs out.
    /// </summary>
    public static class LevelRepository
    {
        public const string Folder = "Levels";

        public static string FileName(int n) => $"level_{n:00}";

        /// <summary>Resources folder for a mode's baked levels (Load keeps the original folder).</summary>
        public static string FolderFor(GameMode mode) => mode == GameMode.Unload ? Folder + "/Unload" : Folder;

        public static LevelData Get(int levelNumber, GameMode mode = GameMode.Load)
        {
            int n = levelNumber;
            n = LevelGenerator.Canonical(n);
            var asset = Resources.Load<TextAsset>($"{FolderFor(mode)}/{FileName(n)}");
            if (asset != null)
            {
                var level = JsonUtility.FromJson<LevelData>(asset.text);
                if (level != null && level.Validate() == null)
                {
                    level.levelNumber = levelNumber;
                    level.mode = mode;
                    return level;
                }
                Debug.LogWarning($"[Dockside] Baked level {n} is invalid; generating instead.");
            }
            var generated = LevelCatalog.Get(n, mode);
            generated.levelNumber = levelNumber;
            return generated;
        }
    }
}
