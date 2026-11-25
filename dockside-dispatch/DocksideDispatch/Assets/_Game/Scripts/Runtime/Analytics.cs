using System.Collections.Generic;
using System.Globalization;
using GameAnalyticsSDK;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Analytics for Dockside Dispatch, sent to GameAnalytics (keys live in Resources/GameAnalytics/Settings,
    /// written by Dockside > Setup Project). Every event is also logged to the console as "[Analytics] ...".
    ///
    /// Mapping to GameAnalytics:
    ///  - level_start / level_complete / level_fail  -> progression events "Dockside:level_NNN" (score = moves on complete)
    ///  - ad_rewarded_show / ad_rewarded_granted / ad_interstitial_show -> ad events (SDK "levelplay", placement)
    ///  - ad_impression (LevelPlay impression-level revenue) -> design event "AdRevenue:format:network" with the USD value
    ///    (this GameAnalytics package has no public impression-event API)
    ///  - AdClicked / AdFailed -> ad events (Clicked, FailedShow with the reason)
    ///  - Business (the Premium purchase) -> business event with the store price, currency and receipt
    ///  - Error (failed purchases, ads that failed to show, store / ads unavailable) -> error events (warning or error)
    ///  - Coins -> resource events
    ///  - custom dimensions: 01 device (phone / tablet / tv), 02 Premium (free / premium), 03 starting boosters (start1 / start3)
    ///  - anything else -> design event "name:key:value" built from the first field
    /// </summary>
    public static class Analytics
    {
        private static bool _gaReady;

        /// <summary>Starts GameAnalytics if this platform has keys configured. Safe to call more than once.</summary>
        public static void Initialize()
        {
            if (_gaReady) return;
            var settings = Resources.Load<GameAnalyticsSDK.Setup.Settings>("GameAnalytics/Settings");
            int index = settings != null ? settings.Platforms.IndexOf(Application.platform) : -1;
            if (index < 0 || string.IsNullOrEmpty(settings.GetGameKey(index)) || string.IsNullOrEmpty(settings.GetSecretKey(index)))
            {
                Debug.Log($"[Analytics] GameAnalytics not configured for {Application.platform}; logging to the console only.");
                return;
            }
            if (Object.FindFirstObjectByType<GameAnalytics>() == null)
                new GameObject("GameAnalytics").AddComponent<GameAnalytics>();   // handles sessions on pause/resume
            GameAnalytics.Initialize();
            _gaReady = true;
            Debug.Log("[Analytics] GameAnalytics initialized");
            UpdateDimensions();
            Premium.Changed += UpdateDimensions;   // bought, restored or refunded
        }

        public static void Event(string name, params (string key, object value)[] fields)
        {
            var sb = new System.Text.StringBuilder("[Analytics] ").Append(name);
            foreach (var (k, v) in fields) sb.Append(' ').Append(k).Append('=').Append(v);
            Debug.Log(sb.ToString());
            if (!_gaReady) return;

            var f = new Dictionary<string, object>();
            foreach (var (k, v) in fields) f[k] = v;
            string Field(string k) => f.TryGetValue(k, out var v) ? Clean(v) : "";

            switch (name)
            {
                // Load keeps the original "Dockside" progression so existing dashboards continue; Unload has its own.
                case "level_start":
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, Progression(f), Level(f));
                    break;
                case "level_complete":
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, Progression(f), Level(f), ToInt(f, "moves"));
                    break;
                case "level_fail":
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, Progression(f), Level(f));
                    break;
                case "ad_rewarded_show":
                    GameAnalytics.NewAdEvent(GAAdAction.Show, GAAdType.RewardedVideo, "levelplay", Field("placement"));
                    break;
                case "ad_rewarded_granted":
                    GameAnalytics.NewAdEvent(GAAdAction.RewardReceived, GAAdType.RewardedVideo, "levelplay", Field("placement"));
                    break;
                case "ad_interstitial_show":
                    GameAnalytics.NewAdEvent(GAAdAction.Show, GAAdType.Interstitial, "levelplay", "level_end");
                    break;
                default:
                    var id = fields.Length > 0 ? $"{Clean(name)}:{Clean(fields[0].key)}:{Clean(fields[0].value)}" : Clean(name);
                    GameAnalytics.NewDesignEvent(id);
                    break;
            }
        }

        /// <summary>Impression-level ad revenue (LevelPlay ILRD), summed in GameAnalytics as a design event value.</summary>
        public static void AdImpression(string network, string format, string instance, double revenueUsd, string rawJson)
        {
            Debug.Log($"[Analytics] ad_impression network={network} format={format} instance={instance} revenue_usd={revenueUsd}");
            if (_gaReady) GameAnalytics.NewDesignEvent($"AdRevenue:{Clean(format)}:{Clean(network)}", (float)revenueUsd);
        }

        /// <summary>
        /// A real-money purchase (GameAnalytics business event): amount in the currency's minor units (₹250.00 = 25000),
        /// with the store receipt when there is one, so GameAnalytics can validate it. cartType says where it was bought.
        /// </summary>
        public static void Business(string isoCurrency, int amountMinor, string itemType, string itemId, string cartType,
                                    string receipt = null, string signature = null)
        {
            Debug.Log($"[Analytics] business currency={isoCurrency} amount={amountMinor} type={itemType} id={itemId} cart={cartType} receipt={(receipt != null ? "yes" : "no")}");
            if (!_gaReady || string.IsNullOrEmpty(isoCurrency) || amountMinor <= 0) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!string.IsNullOrEmpty(receipt) && !string.IsNullOrEmpty(signature))
            {
                GameAnalytics.NewBusinessEventGooglePlay(isoCurrency, amountMinor, Clean(itemType), Clean(itemId), Clean(cartType), receipt, signature);
                return;
            }
#elif UNITY_IOS && !UNITY_EDITOR
            if (!string.IsNullOrEmpty(receipt))
            {
                GameAnalytics.NewBusinessEventIOS(isoCurrency, amountMinor, Clean(itemType), Clean(itemId), Clean(cartType), receipt);
                return;
            }
#endif
            GameAnalytics.NewBusinessEvent(isoCurrency, amountMinor, Clean(itemType), Clean(itemId), Clean(cartType));
        }

        public enum AdKind { Rewarded, Interstitial }

        /// <summary>An ad was clicked (GameAnalytics ad event).</summary>
        public static void AdClicked(AdKind kind, string placement)
        {
            Debug.Log($"[Analytics] ad_clicked type={kind} placement={placement}");
            if (_gaReady) GameAnalytics.NewAdEvent(GAAdAction.Clicked, AdType(kind), "levelplay", Clean(placement));
        }

        /// <summary>
        /// An ad was wanted but couldn't be shown (GameAnalytics ad event, FailedShow): none loaded yet, no fill, offline,
        /// or the ad network failed while showing it. lastErrorCode is LevelPlay's code from the last failed load or show.
        /// </summary>
        public static void AdFailed(AdKind kind, string placement, int lastErrorCode, bool duringShow)
        {
            var reason = Application.internetReachability == NetworkReachability.NotReachable ? GAAdError.Offline
                : duringShow ? GAAdError.InternalError
                : lastErrorCode == 509 || lastErrorCode == 606 || lastErrorCode == 621 ? GAAdError.NoFill   // LevelPlay "no fill" codes
                : GAAdError.UnableToPrecache;                                                             // still loading
            Debug.Log($"[Analytics] ad_failed type={kind} placement={placement} reason={reason} code={lastErrorCode}");
            if (_gaReady) GameAnalytics.NewAdEvent(GAAdAction.FailedShow, AdType(kind), "levelplay", Clean(placement), reason);
        }

        private static GAAdType AdType(AdKind kind) => kind == AdKind.Rewarded ? GAAdType.RewardedVideo : GAAdType.Interstitial;

        /// <summary>
        /// Something went wrong that the player may have noticed but that isn't a crash (GameAnalytics error event):
        /// a purchase that failed, an ad that failed to display, the store or ads unavailable. Crashes and Unity errors
        /// are sent by GameAnalytics itself ("Submit Errors" in its settings).
        /// </summary>
        public static void Error(bool serious, string area, string message)
        {
            string text = $"{area}: {message}";
            if (text.Length > 300) text = text.Substring(0, 300);   // GameAnalytics keeps error messages short
            Debug.Log($"[Analytics] error severity={(serious ? "error" : "warning")} {text}");
            if (_gaReady) GameAnalytics.NewErrorEvent(serious ? GAErrorSeverity.Error : GAErrorSeverity.Warning, text);
        }

        // Custom dimensions: every GameAnalytics report can be split by these. Their values must also be listed in
        // Resources/GameAnalytics/Settings (Custom Dimensions 01/02/03), or GameAnalytics drops them.
        public static readonly string[] DeviceDimension = { "phone", "tablet", "tv" };
        public static readonly string[] PremiumDimension = { "free", "premium" };
        public static readonly string[] StartDimension = { "start1", "start3" };

        /// <summary>Sets the custom dimensions: device kind, Premium owned, and the starting-booster group.</summary>
        public static void UpdateDimensions()
        {
            string device = ScreenInfo.IsTv ? "tv" : ScreenInfo.IsLargeScreen ? "tablet" : "phone";
            string premium = Premium.Owned ? "premium" : "free";
            string start = SaveData.StartingBoosters >= 3 ? "start3" : "start1";
            Debug.Log($"[Analytics] dimensions device={device} premium={premium} start={start}");
            if (!_gaReady) return;
            GameAnalytics.SetCustomDimension01(device);
            GameAnalytics.SetCustomDimension02(premium);
            GameAnalytics.SetCustomDimension03(start);
        }

        /// <summary>Coins earned (source) or spent (sink), as a GameAnalytics resource event.</summary>
        public static void Coins(bool earned, int amount, string itemType, string itemId)
        {
            Debug.Log($"[Analytics] coins_{(earned ? "earned" : "spent")} amount={amount} type={itemType} id={itemId}");
            if (_gaReady)
                GameAnalytics.NewResourceEvent(earned ? GAResourceFlowType.Source : GAResourceFlowType.Sink, "coins", amount, itemType, Clean(itemId));
        }

        private static string Level(Dictionary<string, object> f) => $"level_{ToInt(f, "level"):000}";

        private static string Progression(Dictionary<string, object> f) =>
            f.TryGetValue("mode", out var m) && System.Convert.ToString(m, CultureInfo.InvariantCulture) == "Unload" ? "Unload" : "Dockside";

        private static int ToInt(Dictionary<string, object> f, string key) =>
            f.TryGetValue(key, out var v) && int.TryParse(System.Convert.ToString(v, CultureInfo.InvariantCulture), out var n) ? n : 0;

        /// <summary>GameAnalytics event ids allow letters, digits and a few symbols; keep each part short and safe.</summary>
        private static string Clean(object value)
        {
            var s = System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '.' ? c : '_');
            return sb.Length > 32 ? sb.ToString(0, 32) : sb.ToString();
        }
    }
}
