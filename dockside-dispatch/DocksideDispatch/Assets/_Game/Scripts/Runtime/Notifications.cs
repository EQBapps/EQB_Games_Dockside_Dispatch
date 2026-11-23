using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif
#if UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace Dockside
{
    /// <summary>
    /// Local re-engagement reminders (no server, nothing leaves the device).
    /// Policy: the player opts in through our own card after an early win (never at first launch); Android 13+ and iOS
    /// then show the system permission dialog. When the app goes to the background we schedule three reminders, 1, 3 and 7 days out,
    /// moved into 11:00-20:00 local time; opening the app cancels them all. After the 7-day one nothing more is sent.
    /// The player can switch reminders off (or back on) under Pause.
    /// </summary>
    public static class Notifications
    {
        public const string ChannelId = "reminders";
        private const string EnabledKey = "dd.notif.enabled";     // 1 on, 0 off (default: off until the player opts in)
        private const string PromptsKey = "dd.notif.prompts";     // how many times our opt-in card has been shown
        private const string LastOpenKey = "dd.notif.last_open";  // id of the last reminder that opened the app (logged once)
        public const int MaxPrompts = 2;
        private static readonly int[] DelayHours = { 24, 72, 168 };
        private const int WindowStartHour = 11, WindowEndHour = 20;
        public const string SmallIcon = "icon_small", LargeIcon = "icon_large";

        /// <summary>The player's in-game choice. Reminders also need the device permission (<see cref="HasPermission"/>).</summary>
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(EnabledKey, 0) == 1;
            set { PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int PromptsShown
        {
            get => PlayerPrefs.GetInt(PromptsKey, 0);
            set { PlayerPrefs.SetInt(PromptsKey, value); PlayerPrefs.Save(); }
        }

        /// <summary>True when reminders are on and will actually be shown.</summary>
        public static bool Active => Enabled && HasPermission;

        /// <summary>Device-level permission (always true before Android 13 unless the player blocked the app in Settings).</summary>
        public static bool HasPermission
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.Allowed;
#elif UNITY_IOS && !UNITY_EDITOR
                var status = iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus;
                return status == AuthorizationStatus.Authorized || status == AuthorizationStatus.Provisional || status == AuthorizationStatus.Ephemeral;
#else
                return true;   // editor: behave as if granted so the flow can be tested
#endif
            }
        }

        /// <summary>Our opt-in card is offered at most <see cref="MaxPrompts"/> times, and never once reminders are on.</summary>
        public static bool ShouldOffer => !Active && PromptsShown < MaxPrompts && !DeclinedAtSystemLevel;

        private static bool DeclinedAtSystemLevel
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.NotificationsBlockedForApp;
#elif UNITY_IOS && !UNITY_EDITOR
                return iOSNotificationCenter.GetNotificationSettings().AuthorizationStatus == AuthorizationStatus.Denied;
#else
                return false;
#endif
            }
        }

        /// <summary>Call once at startup: registers the channel, clears pending reminders and logs an open from a reminder.</summary>
        public static void Init()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!AndroidNotificationCenter.Initialize()) return;
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(
                ChannelId, "Reminders", "A gentle nudge when the harbour has been quiet for a while.", Importance.Default));
#endif
            OnAppResumed();
        }

        /// <summary>App opened or brought back: cancel everything still pending and record whether a reminder brought the player back.</summary>
        public static void OnAppResumed()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!AndroidNotificationCenter.Initialize()) return;
            AndroidNotificationCenter.CancelAllNotifications();
            var intent = AndroidNotificationCenter.GetLastNotificationIntent();
            if (intent != null && intent.Id != PlayerPrefs.GetInt(LastOpenKey, 0))
            {
                PlayerPrefs.SetInt(LastOpenKey, intent.Id);
                PlayerPrefs.Save();
                Analytics.Event("notification_open", ("reminder", intent.Notification.IntentData ?? ""));
            }
#elif UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.RemoveAllScheduledNotifications();
            iOSNotificationCenter.RemoveAllDeliveredNotifications();
#pragma warning disable CS0618   // the synchronous call is enough here: a reminder that opened the app is already known at launch
            var opened = iOSNotificationCenter.GetLastRespondedNotification();
#pragma warning restore CS0618
            if (opened != null && opened.Identifier != PlayerPrefs.GetString(LastOpenKey + ".ios", ""))
            {
                PlayerPrefs.SetString(LastOpenKey + ".ios", opened.Identifier);
                PlayerPrefs.Save();
                Analytics.Event("notification_open", ("reminder", opened.Data ?? ""));
            }
#endif
        }

        /// <summary>App going to the background: schedule the three reminders (if the player has them on).</summary>
        public static void OnAppBackgrounded(int loadLevel, bool unloadWaiting)
        {
            if (!Active) return;
            var now = DateTime.Now;
            string port = PortSkylines.NameForLevel(loadLevel);
            for (int i = 0; i < DelayHours.Length; i++)
            {
                var (title, text, tag) = Message(i, port, loadLevel, unloadWaiting);
                var fire = FitWindow(now, now.AddHours(DelayHours[i]));
#if UNITY_ANDROID && !UNITY_EDITOR
                if (!AndroidNotificationCenter.Initialize()) return;
                var n = new AndroidNotification(title, text, fire)
                {
                    SmallIcon = SmallIcon,
                    LargeIcon = LargeIcon,
                    IntentData = tag,
                    ShouldAutoCancel = true,
                };
                AndroidNotificationCenter.SendNotification(n, ChannelId);
#elif UNITY_IOS && !UNITY_EDITOR
                iOSNotificationCenter.ScheduleNotification(IosReminder($"{tag}.{now.Ticks}", title, text, tag, fire - now));
#else
                Debug.Log($"[Dockside] Reminder '{tag}' at {fire:ddd dd MMM HH:mm}: {title} / {text}");
#endif
            }
        }

        private static (string title, string text, string tag) Message(int index, string port, int loadLevel, bool unloadWaiting)
        {
            switch (index)
            {
                case 0:
                    return unloadWaiting
                        ? ("A ship has come in full!", "Unload mode is waiting at the docks. Can you fill every truck?", "d1_unload")
                        : ("The ships are waiting", $"Level {loadLevel} in {port} is ready to load.", "d1");
                case 1:
                    return ($"Cargo is piling up in {port}", "A few quick puzzles will clear the yard.", "d3");
                default:
                    return ("We kept your berth free", "Your next port is ready whenever you are.", "d7");
            }
        }

        /// <summary>
        /// Moves a reminder into the 11:00-20:00 window: early-morning times wait until 11:00; late-evening ones come back to
        /// 19:00 the same day if that is still 20+ hours after the player left, otherwise they wait until 11:00 the next day.
        /// </summary>
        public static DateTime FitWindow(DateTime left, DateTime t)
        {
            if (t.Hour < WindowStartHour) return t.Date.AddHours(WindowStartHour).AddMinutes(t.Minute % 30);
            if (t.Hour < WindowEndHour) return t;
            var evening = t.Date.AddHours(WindowEndHour - 1);
            return evening - left >= TimeSpan.FromHours(20) ? evening : t.Date.AddDays(1).AddHours(WindowStartHour);
        }

        /// <summary>
        /// Asks for the device permission (Android 13+ shows the system dialog; older versions grant it straight away).
        /// Calls back with true when reminders can be shown.
        /// </summary>
        public static IEnumerator RequestPermission(Action<bool> done)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!AndroidNotificationCenter.Initialize()) { done(false); yield break; }
            var request = new PermissionRequest();
            while (request.Status == PermissionStatus.RequestPending) yield return null;
            Analytics.Event("notification_permission", ("status", request.Status.ToString()));
            done(request.Status == PermissionStatus.Allowed);
#elif UNITY_IOS && !UNITY_EDITOR
            using (var request = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Sound, false))
            {
                while (!request.IsFinished) yield return null;
                Analytics.Event("notification_permission", ("status", request.Granted ? "Allowed" : "Denied"));
                done(request.Granted);
            }
#else
            yield return null;
            done(true);
#endif
        }

        /// <summary>Test tool: the first reminder, one minute from now (checks the permission, icon and wording on a device).</summary>
        public static void SendTest(int loadLevel, bool unloadWaiting)
        {
            var (title, text, _) = Message(0, PortSkylines.NameForLevel(loadLevel), loadLevel, unloadWaiting);
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!AndroidNotificationCenter.Initialize()) return;
            AndroidNotificationCenter.SendNotification(new AndroidNotification(title, text, DateTime.Now.AddMinutes(1))
            {
                SmallIcon = SmallIcon, LargeIcon = LargeIcon, IntentData = "test", ShouldAutoCancel = true,
            }, ChannelId);
#elif UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.ScheduleNotification(IosReminder("test", title, text, "test", TimeSpan.FromMinutes(1)));
#else
            Debug.Log($"[Dockside] Test reminder in 1 minute: {title} / {text}");
#endif
        }

        /// <summary>Opens the system notification settings for this app (when the permission was refused before).</summary>
        public static void OpenSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (AndroidNotificationCenter.Initialize()) AndroidNotificationCenter.OpenNotificationSettings();
#elif UNITY_IOS && !UNITY_EDITOR
            iOSNotificationCenter.OpenNotificationSettings();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        /// <summary>A one-off local reminder that fires after <paramref name="delay"/> (only shown while the game is closed).</summary>
        private static iOSNotification IosReminder(string id, string title, string text, string tag, TimeSpan delay) => new iOSNotification
        {
            Identifier = id,
            Title = title,
            Body = text,
            Data = tag,
            ShowInForeground = false,
            Trigger = new iOSNotificationTimeIntervalTrigger
            {
                TimeInterval = delay < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : delay,
                Repeats = false,
            },
        };
#endif
    }
}
