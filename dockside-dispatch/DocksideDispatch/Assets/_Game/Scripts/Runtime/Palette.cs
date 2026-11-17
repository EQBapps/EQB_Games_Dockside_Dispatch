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
        public Color hidden = Hex("6E5A7E");
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
