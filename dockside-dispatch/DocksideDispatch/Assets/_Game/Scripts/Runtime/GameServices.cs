using System;
using Dockside.Core;
using UnityEngine;
#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace Dockside
{
    /// <summary>
    /// Google Play Games: automatic sign-in and achievements. Play never blocks the game: if sign-in fails or the player
    /// declines, everything works as before and progress is still counted on the device.
    /// Progress is kept locally (PlayerPrefs) and pushed idempotently (unlock / SetStepsAtLeast), so anything earned while
    /// signed out or offline is sent the next time the player is signed in, and existing players get credit for the levels
    /// and ports they had already reached.
    /// </summary>
    public static class GameServices
    {
        private const string StatPrefix = "dd.stat.", EarnedPrefix = "dd.ach.";
        public const int CleanSheetFromLevel = 20, LastPortLevel = 96;   // the 20th port starts at level 96

        public static bool SignedIn { get; private set; }

        // Incremental achievements: local counter -> achievement id and steps.
        private static readonly (string stat, string id, int steps)[] Counters =
        {
            ("cranelift", GPGSIds.achievement_crane_operator, 25),
            ("unlocked", GPGSIds.achievement_key_holder, 50),
            ("revealed", GPGSIds.achievement_mystery_solved, 100),
            ("long", GPGSIds.achievement_long_haul, 100),
            ("ships", GPGSIds.achievement_fleet_commander, 500),
            ("trucks", GPGSIds.achievement_convoy_leader, 500),
        };

        public static int Stat(string key) => PlayerPrefs.GetInt(StatPrefix + key, 0);

        /// <summary>Call once at startup: activates the plugin and tries the silent, automatic sign-in.</summary>
        public static void Init()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            PlayGamesPlatform.DebugLogEnabled = Debug.isDebugBuild;
            PlayGamesPlatform.Activate();
            PlayGamesPlatform.Instance.Authenticate(OnSignIn);
#else
            Debug.Log("[Dockside] Play Games: editor, sign-in skipped (achievements are only counted locally).");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static void OnSignIn(SignInStatus status)
        {
            SignedIn = status == SignInStatus.Success;
            Analytics.Event("gpgs_signin", ("status", status.ToString()));
            if (SignedIn) SyncAll();
        }
#endif

        /// <summary>Pause > Achievements: shows Play's list, signing in first if the automatic sign-in didn't happen.</summary>
        public static void ShowAchievements(Action<bool> signInFailed = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (SignedIn) { PlayGamesPlatform.Instance.ShowAchievementsUI(); return; }
            PlayGamesPlatform.Instance.ManuallyAuthenticate(status =>
            {
                OnSignIn(status);
                if (SignedIn) PlayGamesPlatform.Instance.ShowAchievementsUI();
                else signInFailed?.Invoke(true);
            });
#else
            Debug.Log("[Dockside] Play Games: achievements UI (device only). Earned: " + string.Join(", ", EarnedNames()));
            signInFailed?.Invoke(false);
#endif
        }

        // ------------------------------------------------------------------ game hooks
        public static void AddStat(string key, int amount = 1)
        {
            if (amount <= 0) return;
            int total = Stat(key) + amount;
            PlayerPrefs.SetInt(StatPrefix + key, total);
            foreach (var c in Counters)
                if (c.stat == key) PushSteps(c.id, Math.Min(total, c.steps));
        }

        /// <summary>After a win: the milestone and skill achievements. Counters are fed separately as events happen.</summary>
        public static void OnLevelWon(GameMode mode, int level, int boostersUsed, bool usedSecondChance, bool night)
        {
            Earn(GPGSIds.achievement_first_dispatch);
            if (mode == GameMode.Load)
            {
                if (level >= 10) Earn(GPGSIds.achievement_harbour_hand);
                if (level >= 50) Earn(GPGSIds.achievement_dock_master);
                if (level >= 100) Earn(GPGSIds.achievement_harbour_legend);
            }
            else
            {
                Earn(GPGSIds.achievement_change_of_shift);
                if (level >= 50) Earn(GPGSIds.achievement_yard_boss);
                if (level >= 100) Earn(GPGSIds.achievement_unload_legend);
            }
            if (level + 1 >= LastPortLevel) Earn(GPGSIds.achievement_around_the_world);   // the next level is in Seattle
            if (level >= CleanSheetFromLevel && boostersUsed == 0) Earn(GPGSIds.achievement_clean_sheet);
            if (usedSecondChance) Earn(GPGSIds.achievement_comeback_captain);
            if (night) Earn(GPGSIds.achievement_night_shift);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ internals
        private static void Earn(string id)
        {
            if (PlayerPrefs.GetInt(EarnedPrefix + id, 0) == 1) return;
            PlayerPrefs.SetInt(EarnedPrefix + id, 1);
            Analytics.Event("achievement", ("id", id));
            Unlock(id);
        }

        /// <summary>Everything earned so far, from the local record and from saved progress (for players from before Play Games).</summary>
        private static void SyncAll()
        {
            int loadDone = SaveData.LevelFor(GameMode.Load) - 1, unloadDone = SaveData.LevelFor(GameMode.Unload) - 1;
            if (loadDone >= 1 || unloadDone >= 1) PlayerPrefs.SetInt(EarnedPrefix + GPGSIds.achievement_first_dispatch, 1);
            if (loadDone >= 10) PlayerPrefs.SetInt(EarnedPrefix + GPGSIds.achievement_harbour_hand, 1);
            if (loadDone >= 50) PlayerPrefs.SetInt(EarnedPrefix + GPGSIds.achievement_dock_master, 1);
            if (loadDone >= 100) PlayerPrefs.SetInt(EarnedPrefix + GPGSIds.achievement_harbour_legend, 1);
            if (unloadDone >= 1) PlayerPrefs.SetInt(EarnedPrefix + GPGSIds.achievement_change_of_shift, 1);
            if (unloadDone >= 50) PlayerPrefs.SetInt(EarnedPrefix + GPGSIds.achievement_yard_boss, 1);
            if (unloadDone >= 100) PlayerPrefs.SetInt(EarnedPrefix + GPGSIds.achievement_unload_legend, 1);
            if (Math.Max(loadDone, unloadDone) + 1 >= LastPortLevel) PlayerPrefs.SetInt(EarnedPrefix + GPGSIds.achievement_around_the_world, 1);
            PlayerPrefs.Save();
            foreach (var id in AllStandardIds())
                if (PlayerPrefs.GetInt(EarnedPrefix + id, 0) == 1) Unlock(id);
            foreach (var c in Counters)
                if (Stat(c.stat) > 0) PushSteps(c.id, Math.Min(Stat(c.stat), c.steps));
        }

        private static string[] AllStandardIds() => new[]
        {
            GPGSIds.achievement_first_dispatch, GPGSIds.achievement_harbour_hand, GPGSIds.achievement_dock_master,
            GPGSIds.achievement_harbour_legend, GPGSIds.achievement_change_of_shift, GPGSIds.achievement_yard_boss,
            GPGSIds.achievement_unload_legend, GPGSIds.achievement_around_the_world, GPGSIds.achievement_clean_sheet,
            GPGSIds.achievement_comeback_captain, GPGSIds.achievement_night_shift,
        };

        private static System.Collections.Generic.IEnumerable<string> EarnedNames()
        {
            foreach (var id in AllStandardIds())
                if (PlayerPrefs.GetInt(EarnedPrefix + id, 0) == 1) yield return id;
        }

        private static void Unlock(string id)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (SignedIn) PlayGamesPlatform.Instance.UnlockAchievement(id);
#endif
        }

        private static void PushSteps(string id, int steps)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (SignedIn && steps > 0) PlayGamesPlatform.Instance.SetStepsAtLeast(id, steps, _ => { });
#endif
        }
    }
}
