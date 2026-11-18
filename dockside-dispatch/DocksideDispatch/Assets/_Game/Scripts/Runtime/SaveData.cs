using Dockside.Core;
using UnityEngine;

namespace Dockside
{
    /// <summary>Player progress in PlayerPrefs (fine for the prototype; move to a save file / cloud save later).</summary>
    public static class SaveData
    {
        // "dd.level" stays the Load level so existing players keep their progress.
        private const string LevelKey = "dd.level", UnloadLevelKey = "dd.unload.level", ModeKey = "dd.mode", CoinsKey = "dd.coins", BoosterKey = "dd.booster.";
        public const int StartingBoosters = 3;
        /// <summary>Unload opens once Load level 5 is complete (the player has learnt the core rules).</summary>
        public const int UnloadUnlockLevel = 6;

        /// <summary>Next Load level.</summary>
        public static int Level
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt(LevelKey, 1));
            set { PlayerPrefs.SetInt(LevelKey, Mathf.Max(1, value)); PlayerPrefs.Save(); }
        }

        /// <summary>Next level in a mode (each mode has its own campaign; coins and boosters are shared).</summary>
        public static int LevelFor(GameMode mode) => mode == GameMode.Unload ? Mathf.Max(1, PlayerPrefs.GetInt(UnloadLevelKey, 1)) : Level;

        public static void SetLevelFor(GameMode mode, int level)
        {
            if (mode == GameMode.Unload) { PlayerPrefs.SetInt(UnloadLevelKey, Mathf.Max(1, level)); PlayerPrefs.Save(); }
            else Level = level;
        }

        /// <summary>The mode played last (the menu opens on it).</summary>
        public static GameMode Mode
        {
            get => PlayerPrefs.GetInt(ModeKey, 0) == 1 && UnloadUnlocked ? GameMode.Unload : GameMode.Load;
            set { PlayerPrefs.SetInt(ModeKey, (int)value); PlayerPrefs.Save(); }
        }

        public static bool UnloadUnlocked => Level >= UnloadUnlockLevel;

        private const string UnloadPlayedKey = "dd.unload.played", UnloadRemindKey = "dd.unload.remind_at";

        /// <summary>The player has started an Unload level at least once (clears the menu's NEW badge and the reminder).</summary>
        public static bool UnloadPlayed
        {
            get => PlayerPrefs.GetInt(UnloadPlayedKey, 0) == 1;
            set { PlayerPrefs.SetInt(UnloadPlayedKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Load level at which to remind a player who chose "Keep loading" that Unload is waiting (0 = no reminder).</summary>
        public static int UnloadRemindAt
        {
            get => PlayerPrefs.GetInt(UnloadRemindKey, 0);
            set { PlayerPrefs.SetInt(UnloadRemindKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static int Coins
        {
            get => PlayerPrefs.GetInt(CoinsKey, 0);
            set { PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static int Boosters(BoosterType t) => PlayerPrefs.GetInt(BoosterKey + t, StartingBoosters);

        public static void SetBoosters(BoosterType t, int n)
        {
            PlayerPrefs.SetInt(BoosterKey + t, Mathf.Max(0, n));
            PlayerPrefs.Save();
        }
    }
}
