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
                case "level_start":
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, "Dockside", Level(f));
                    break;
                case "level_complete":
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, "Dockside", Level(f), ToInt(f, "moves"));
                    break;
                case "level_fail":
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, "Dockside", Level(f));
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

        /// <summary>Coins earned (source) or spent (sink), as a GameAnalytics resource event.</summary>
        public static void Coins(bool earned, int amount, string itemType, string itemId)
        {
            Debug.Log($"[Analytics] coins_{(earned ? "earned" : "spent")} amount={amount} type={itemType} id={itemId}");
            if (_gaReady)
                GameAnalytics.NewResourceEvent(earned ? GAResourceFlowType.Source : GAResourceFlowType.Sink, "coins", amount, itemType, Clean(itemId));
        }

        private static string Level(Dictionary<string, object> f) => $"level_{ToInt(f, "level"):000}";

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
