using System.Collections.Generic;
using System.Globalization;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace Dockside
{
    /// <summary>
    /// Player privacy choices for ads, saved in PlayerPrefs and passed to LevelPlay before it initializes.
    ///  - GDPR: players whose device region is in the EU/EEA, UK or Switzerland are asked once, on first launch,
    ///    whether ad partners may personalise ads. LevelPlay is not initialized until they answer.
    ///  - CCPA/CPRA: a "Do not sell or share my info" opt-out, off by default, changeable in the pause menu.
    /// The region comes from the device's locale settings (a reasonable default for a prototype; a consent
    /// management platform or IP-based geolocation would be more precise for release).
    /// This is a technical implementation, not legal advice.
    /// </summary>
    public static class Privacy
    {
        private const string GdprKey = "dd.privacy.gdpr", CcpaKey = "dd.privacy.ccpa_optout";

        private static readonly HashSet<string> GdprRegions = new HashSet<string>
        {
            // EU member states
            "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR", "HU", "IE", "IT", "LV", "LT",
            "LU", "MT", "NL", "PL", "PT", "RO", "SK", "SI", "ES", "SE",
            // Rest of the EEA, plus the UK and Switzerland
            "IS", "LI", "NO", "GB", "CH",
        };

        public static string Region
        {
            get
            {
                try { return RegionInfo.CurrentRegion.TwoLetterISORegionName.ToUpperInvariant(); }
                catch { return ""; }
            }
        }

        public static bool GdprApplies => GdprRegions.Contains(Region);

        /// <summary>-1 = not asked yet, 0 = declined, 1 = consented.</summary>
        public static int GdprAnswer
        {
            get => PlayerPrefs.GetInt(GdprKey, -1);
            set { PlayerPrefs.SetInt(GdprKey, value); PlayerPrefs.Save(); }
        }

        public static bool CcpaOptOut
        {
            get => PlayerPrefs.GetInt(CcpaKey, 0) == 1;
            set { PlayerPrefs.SetInt(CcpaKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>True when the first-launch consent question still has to be shown.</summary>
        public static bool NeedsGdprAnswer => GdprApplies && GdprAnswer < 0;

        /// <summary>Ads may initialize once no required question is outstanding.</summary>
        public static bool Ready => !NeedsGdprAnswer;

        /// <summary>Passes the saved choices to LevelPlay. Call before LevelPlay.Init(), and again whenever they change.</summary>
        public static void ApplyToLevelPlay()
        {
            if (GdprApplies || GdprAnswer >= 0) LevelPlayPrivacySettings.SetGDPRConsent(GdprAnswer == 1);
            LevelPlayPrivacySettings.SetCCPA(CcpaOptOut);
        }
    }
}
