using UnityEngine;

namespace Dockside
{
    public enum DayPhase { Morning, Midday, Afternoon, Sunset, Night }

    /// <summary>
    /// Colours and sun/moon placement for one time of day. The scene follows the player's local clock
    /// (morning 5-10, midday 10-14, afternoon 14-17, sunset 17-20, night 20-5); a preview override is kept in
    /// PlayerPrefs ("dd.sky", -1 = follow the clock) and cycled from the pause menu.
    /// </summary>
    public class SkyTheme
    {
        public DayPhase phase;
        public Color zenith, high, mid, horizon, sunGlow, sun;
        public float sunX, sunV, sunRadius;          // sun (or moon) position above the horizon, world units
        public bool moon, stars, lightsOn, sunStreaks;
        public Color cloudLight, cloudShade;
        public Color haze, silhouetteFar, silhouetteNear, lights;
        public Color waterShallow, waterDeep, waterHorizon, waterHighlight, waterGlitter, foam;
        public float night;                           // 0..1 darkening of the flat-shaded harbour (Flat shader _DocksideNight)
        public float birdInterval = 22f;              // average seconds between flocks

        private const string OverrideKey = "dd.sky";

        public static int Override
        {
            get => PlayerPrefs.GetInt(OverrideKey, -1);
            set { PlayerPrefs.SetInt(OverrideKey, value); PlayerPrefs.Save(); }
        }

        public static DayPhase PhaseFromClock(System.DateTime now)
        {
            int h = now.Hour;
            if (h >= 5 && h < 10) return DayPhase.Morning;
            if (h >= 10 && h < 14) return DayPhase.Midday;
            if (h >= 14 && h < 17) return DayPhase.Afternoon;
            if (h >= 17 && h < 20) return DayPhase.Sunset;
            return DayPhase.Night;
        }

        // The preview override is a test tool: release builds always follow the clock.
        public static DayPhase CurrentPhase => Override >= 0 && (Application.isEditor || Debug.isDebugBuild)
            ? (DayPhase)Mathf.Clamp(Override, 0, 4) : PhaseFromClock(System.DateTime.Now);

        public static string Label(DayPhase p) => p switch
        {
            DayPhase.Morning => "Morning", DayPhase.Midday => "Midday", DayPhase.Afternoon => "Afternoon",
            DayPhase.Sunset => "Sunset", _ => "Night",
        };

        public static SkyTheme Current(Palette pal) => For(CurrentPhase, pal);

        public static SkyTheme For(DayPhase phase, Palette pal)
        {
            Color H(string hex) => Palette.Hex(hex);
            var t = new SkyTheme { phase = phase };
            switch (phase)
            {
                case DayPhase.Morning:
                    t.zenith = H("8EC5E8"); t.high = H("B6DBF0"); t.mid = H("F6D7CF"); t.horizon = H("FFE4C4");
                    t.sun = H("FFF6DA"); t.sunGlow = H("FFEFD0"); t.sunX = -2.8f; t.sunV = 0.95f; t.sunRadius = 0.7f;
                    t.cloudLight = H("FFFFFF"); t.cloudShade = H("F2D6DC");
                    t.silhouetteFar = H("B3BFD6"); t.silhouetteNear = H("7D89A8"); t.lights = H("FFE6A0");
                    t.waterShallow = H("44B4AE"); t.waterDeep = H("2A8E9A"); t.waterHorizon = H("CFE6EC");
                    t.waterHighlight = H("BFEFE6"); t.waterGlitter = H("FFF4DC"); t.foam = H("F5FCFA");
                    t.night = 0f; break;
                case DayPhase.Midday:
                    t.zenith = H("3F9FE3"); t.high = H("6FB9EB"); t.mid = H("A4D5F1"); t.horizon = H("DCF0F8");
                    t.sun = H("FFFDF2"); t.sunGlow = H("FFFBE8"); t.sunX = 1.8f; t.sunV = 2.35f; t.sunRadius = 0.55f;
                    t.cloudLight = H("FFFFFF"); t.cloudShade = H("D9E8F3");
                    t.silhouetteFar = H("8FADC8"); t.silhouetteNear = H("5D7896"); t.lights = H("FFE6A0");
                    t.waterShallow = H("3BC0B4"); t.waterDeep = H("1D92A3"); t.waterHorizon = H("A3DAE7");
                    t.waterHighlight = H("C9F4EC"); t.waterGlitter = H("FFFFFF"); t.foam = H("F7FDFB");
                    t.night = 0f; break;
                case DayPhase.Afternoon:
                    t.zenith = H("5DA3D6"); t.high = H("8EC0E0"); t.mid = H("E3D7BC"); t.horizon = H("FFE0A6");
                    t.sun = H("FFF3C8"); t.sunGlow = H("FFE8B2"); t.sunX = 2.6f; t.sunV = 1.6f; t.sunRadius = 0.72f;
                    t.cloudLight = H("FFF9EC"); t.cloudShade = H("EAD2B2");
                    t.silhouetteFar = H("A99CB0"); t.silhouetteNear = H("6E6585"); t.lights = H("FFE6A0");
                    t.waterShallow = H("36AEA7"); t.waterDeep = H("1F8995"); t.waterHorizon = H("E5D3B1");
                    t.waterHighlight = H("BDEDE4"); t.waterGlitter = H("FFF1C9"); t.foam = H("F6FBF7");
                    t.night = 0f; break;
                case DayPhase.Night:
                    t.zenith = H("121937"); t.high = H("1C2752"); t.mid = H("2A3767"); t.horizon = H("47507F");
                    t.sun = H("F2EFD8"); t.sunGlow = H("4E5A8E"); t.sunX = -2.5f; t.sunV = 2.05f; t.sunRadius = 0.42f;
                    t.moon = true; t.stars = true; t.lightsOn = true;
                    t.cloudLight = H("3A4573"); t.cloudShade = H("2B3461");
                    t.silhouetteFar = H("242C55"); t.silhouetteNear = H("151B3A"); t.lights = H("FFD98A");
                    t.waterShallow = H("1F5B70"); t.waterDeep = H("133F57"); t.waterHorizon = H("2F3B68");
                    t.waterHighlight = H("4C7EA0"); t.waterGlitter = H("E8E4C6"); t.foam = H("B8CAD6");
                    t.night = 1f; t.birdInterval = 45f; break;
                default: // Sunset: the Golden Hour Harbour art direction
                    t.zenith = pal.skyZenith; t.high = pal.skyHigh; t.mid = pal.skyMid; t.horizon = pal.skyHorizon;
                    t.sun = pal.sun; t.sunGlow = pal.sunGlow; t.sunX = 1.2f; t.sunV = 0.55f; t.sunRadius = 0.98f;
                    t.sunStreaks = true; t.lightsOn = true;
                    t.cloudLight = pal.cloudLight; t.cloudShade = pal.cloudShade;
                    t.silhouetteFar = pal.silhouetteFar; t.silhouetteNear = pal.silhouetteNear; t.lights = pal.silhouetteLight;
                    t.waterShallow = pal.waterShallow; t.waterDeep = pal.waterDeep; t.waterHorizon = pal.waterHorizon;
                    t.waterHighlight = pal.waterHighlight; t.waterGlitter = pal.waterGlitter; t.foam = pal.foam;
                    t.night = 0.12f; break;
            }
            t.haze = Color.Lerp(t.silhouetteFar, t.horizon, 0.45f);
            return t;
        }
    }
}
