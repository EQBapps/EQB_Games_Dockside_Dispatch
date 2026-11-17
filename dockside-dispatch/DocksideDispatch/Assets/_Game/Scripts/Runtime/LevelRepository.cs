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

        public static LevelData Get(int levelNumber)
        {
            int n = levelNumber;
            if (n > LevelGenerator.AuthoredLevelCount) n = 21 + (n - 1) % 10;
            var asset = Resources.Load<TextAsset>($"{Folder}/{FileName(n)}");
            if (asset != null)
            {
                var level = JsonUtility.FromJson<LevelData>(asset.text);
                if (level != null && level.Validate() == null)
                {
                    level.levelNumber = levelNumber;
                    return level;
                }
                Debug.LogWarning($"[Dockside] Baked level {n} is invalid; generating instead.");
            }
            var generated = LevelCatalog.Get(n);
            generated.levelNumber = levelNumber;
            return generated;
        }
    }
}
