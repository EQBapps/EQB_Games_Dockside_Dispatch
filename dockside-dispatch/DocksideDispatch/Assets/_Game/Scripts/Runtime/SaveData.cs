using UnityEngine;

namespace Dockside
{
    /// <summary>Player progress in PlayerPrefs (fine for the prototype; move to a save file / cloud save later).</summary>
    public static class SaveData
    {
        private const string LevelKey = "dd.level", CoinsKey = "dd.coins", BoosterKey = "dd.booster.";
        public const int StartingBoosters = 3;

        public static int Level
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt(LevelKey, 1));
            set { PlayerPrefs.SetInt(LevelKey, Mathf.Max(1, value)); PlayerPrefs.Save(); }
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
