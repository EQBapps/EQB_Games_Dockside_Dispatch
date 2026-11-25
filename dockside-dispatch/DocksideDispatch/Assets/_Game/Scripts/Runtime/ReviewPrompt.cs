using System;
using System.Collections;
using Dockside.Core;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Asks for a store rating with the store's own card (Google Play In-App Review / Apple's review request), straight
    /// after a win, with nothing in front of it (store policy: no "Do you like the game?" question first).
    /// <list type="bullet">
    /// <item>First ask: the first win once the player has cleared <see cref="FirstAskAfter"/> levels (Load and Unload together).</item>
    /// <item>Again only if <see cref="DaysBetween"/> days and <see cref="LevelsBetween"/> more cleared levels have passed,
    /// and at most <see cref="MaxAsks"/> times in all. The stores never say whether the player rated, and they limit how
    /// often the card really appears (Play may skip it), so a few well-spaced asks is the intended pattern.</item>
    /// <item>Never on a TV (Android TV has no in-app review card).</item>
    /// </list>
    /// </summary>
    public static class ReviewPrompt
    {
        public const int FirstAskAfter = 6, LevelsBetween = 10, DaysBetween = 7, MaxAsks = 4;
        private const string AsksKey = "dd.review.asks", LastLevelsKey = "dd.review.last_levels", LastTimeKey = "dd.review.last_time";

        /// <summary>Levels cleared so far in both modes.</summary>
        public static int LevelsCleared => SaveData.LevelFor(GameMode.Load) - 1 + SaveData.LevelFor(GameMode.Unload) - 1;

        public static int Asks => PlayerPrefs.GetInt(AsksKey, 0);

        /// <summary>True when this win is a good moment to ask (checked after the win is saved).</summary>
        public static bool Due
        {
            get
            {
                if (ScreenInfo.IsTv || !Supported) return false;
                int asks = Asks, cleared = LevelsCleared;
                if (asks >= MaxAsks || cleared < FirstAskAfter) return false;
                if (asks == 0) return true;
                long last = long.TryParse(PlayerPrefs.GetString(LastTimeKey, "0"), out var t) ? t : 0;
                double days = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - last) / 86400.0;
                return cleared - PlayerPrefs.GetInt(LastLevelsKey, 0) >= LevelsBetween && days >= DaysBetween;
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private const bool Supported = true;
#elif UNITY_IOS && !UNITY_EDITOR
        private const bool Supported = true;
#else
        private static bool Supported => BuildFlags.TestTools;   // the Editor walks through the flow without a card
#endif

        /// <summary>
        /// Asks for a rating and runs <paramref name="then"/> once the card is closed (or right away if the store
        /// declines to show it). Records the ask so the schedule moves on.
        /// </summary>
        public static IEnumerator Ask(string reason, Action then)
        {
            int asks = Asks + 1;
            PlayerPrefs.SetInt(AsksKey, asks);
            PlayerPrefs.SetInt(LastLevelsKey, LevelsCleared);
            PlayerPrefs.SetString(LastTimeKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
            PlayerPrefs.Save();
            Analytics.Event("review_prompt", ("ask", asks), ("levels", LevelsCleared), ("reason", reason));
#if UNITY_ANDROID && !UNITY_EDITOR
            var cb = new Callback();
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var bridge = new AndroidJavaClass("com.eqbbusiness.dockside.ReviewBridge");
                bridge.CallStatic("request", activity, cb);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Dockside] In-app review unavailable: {e.Message}");
                cb.done(false, e.Message);
            }
            // Wait for Play's answer (a few seconds at most), then, if the card is up, for the player to close it.
            float give = Time.realtimeSinceStartup + 5f;
            while (!cb.Finished && !cb.Launching && Time.realtimeSinceStartup < give) yield return null;
            float close = Time.realtimeSinceStartup + 180f;
            while (cb.Launching && !cb.Finished && Time.realtimeSinceStartup < close) yield return null;
            if (cb.Error != null) Analytics.Error(false, "review", cb.Error);
#elif UNITY_IOS && !UNITY_EDITOR
            UnityEngine.iOS.Device.RequestStoreReview();   // Apple shows it at most 3 times a year, on its own terms
            yield return new WaitForSecondsRealtime(0.5f);
#else
            Debug.Log($"[Dockside] (Editor) In-app review would be requested now: ask {asks}, {LevelsCleared} levels cleared.");
            yield return null;
#endif
            then?.Invoke();
        }

        /// <summary>Test tools: start the schedule again from the beginning.</summary>
        public static void DevReset()
        {
            PlayerPrefs.DeleteKey(AsksKey);
            PlayerPrefs.DeleteKey(LastLevelsKey);
            PlayerPrefs.DeleteKey(LastTimeKey);
            PlayerPrefs.Save();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>ReviewBridge.Callback. Java calls it on the Android UI thread; the coroutine above only reads the flags.</summary>
        private sealed class Callback : AndroidJavaProxy
        {
            public volatile bool Launching, Finished;
            public volatile string Error;
            public Callback() : base("com.eqbbusiness.dockside.ReviewBridge$Callback") { }
            public void launching() => Launching = true;
            public void done(bool ok, string error) { Error = ok ? null : error; Finished = true; }
        }
#endif
    }
}
