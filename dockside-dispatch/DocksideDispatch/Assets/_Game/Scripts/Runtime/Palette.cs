using UnityEngine;

namespace Dockside
{
    /// <summary>Golden Hour Harbour colour palette and base materials (Option A art direction).</summary>
    [CreateAssetMenu(menuName = "Dockside/Palette", fileName = "Palette")]
    public class Palette : ScriptableObject
    {
        [Header("Base materials (URP)")]
        public Material litBase;
        public Material unlitBase;
        public Material waterMaterial;   // Dockside/Water shader (created by Dockside > Setup Project)
        public Material glowMaterial;    // Dockside/Glow shader: referenced so the shader ships in builds (night floodlights)

        [Header("Sunset sky")]
        public Color skyZenith = Hex("B86A9A");
        public Color skyHigh = Hex("E9809A");
        public Color skyMid = Hex("FF9F7E");
        public Color skyHorizon = Hex("FFD08F");
        public Color sunGlow = Hex("FFE7A6");
        public Color cloudLight = Hex("FFC9A8");
        public Color cloudShade = Hex("E8939C");

        [Header("Port silhouette")]
        public Color silhouetteFar = Hex("D9868F");
        public Color silhouetteNear = Hex("9C5274");
        public Color silhouetteLight = Hex("FFE6A0");

        [Header("Water")]
        public Color waterShallow = Hex("2FA39E");
        public Color waterDeep = Hex("1B7E87");
        public Color waterHorizon = Hex("F2B08E");
        public Color waterGlitter = Hex("FFE7A3");
        public Color foam = Hex("F3FBF6");
        public Color fender = Hex("3A3133");

        [Header("Containers (index = colour id)")]
        public Color[] containerColors =
        {
            Hex("E8463F"), // red     - circle
            Hex("2F7FE0"), // blue    - triangle
            Hex("F7C431"), // yellow  - square
            Hex("2DB37A"), // green   - diamond
            Hex("9B5DE5"), // purple  - plus
        };
        public string[] containerNames = { "Red", "Blue", "Yellow", "Green", "Purple" };

        [Header("Scene")]
        public Color skyTop = Hex("FF9E6D");
        public Color skyBottom = Hex("FFE0A8");
        public Color sun = Hex("FFF1C4");
        public Color water = Hex("1F8C8E");
        public Color waterHighlight = Hex("8FD6CF");
        public Color quay = Hex("E9C08E");
        public Color quayEdge = Hex("C48F5C");
        public Color yard = Hex("6E5D57");
        public Color yardLine = Hex("E8C25A");
        public Color[] town = { Hex("B86A74"), Hex("D08A7A"), Hex("A85A6E"), Hex("C77B78"), Hex("D9967E") };
        public Color roof = Hex("8E4A5C");
        public Color lighthouseRed = Hex("D9483B");
        public Color lighthouseWhite = Hex("FFF6E8");
        public Color lighthouseLamp = Hex("FFE17A");
        public Color crane = Hex("F2A23A");
        public Color craneDark = Hex("C97D1E");
        public Color deck = Hex("FFF4E2");
        public Color cabin = Hex("FFFFFF");
        public Color cabinWindow = Hex("5BB8D9");
        public Color symbol = new Color(1f, 1f, 1f, 1f);
        public Color locked = Hex("8A8F98");
        public Color hidden = Hex("4A4F5A");
        public Color keyGold = Hex("FFD24A");
        public Color slotOutline = Hex("C9A27C");

        [Header("Lighting")]
        public Color sunLight = Hex("FFE2BF");
        public Color ambient = Hex("D9B8A8");

        [Header("UI")]
        public Color uiCream = Hex("FFF4E2");
        public Color uiShadow = Hex("D9A577");
        public Color uiText = Hex("4E3F3B");
        public Color uiSubText = Hex("7A5A4A");
        public Color uiBar = Hex("4E3F3B");
        public Color uiAccent = Hex("E8463F");
        public Color uiGood = Hex("2DB37A");

        public Color ContainerColor(int index) => containerColors[Mathf.Abs(index) % containerColors.Length];
        public string ContainerName(int index) => containerNames[Mathf.Abs(index) % containerNames.Length];

        /// <summary>Parses "RRGGBB" without Unity API calls (safe in field initialisers).</summary>
        public static Color Hex(string hex)
        {
            int v = System.Convert.ToInt32(hex, 16);
            return new Color(((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f, 1f);
        }

        public static Color Darken(Color c, float amount) => Color.Lerp(c, Color.black, amount);
    }
}
