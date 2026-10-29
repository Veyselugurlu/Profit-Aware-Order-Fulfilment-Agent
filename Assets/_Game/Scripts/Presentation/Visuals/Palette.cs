using UnityEngine;

namespace Fulfilment.Presentation
{
    /// <summary>One colour palette for the whole scene, so states read the same everywhere.</summary>
    public static class Palette
    {
        // Ground
        public static readonly Color Block = Hex("2B3240");
        public static readonly Color Road = Hex("3F4757");
        public static readonly Color River = Hex("1E5A8A");
        public static readonly Color Bridge = Hex("8A6A45");
        public static readonly Color Background = Hex("1B2029");

        // Entities
        public static readonly Color Center = Hex("4FC3F7");
        public static readonly Color Region = Hex("B39DDB");
        public static readonly Color Parcel = Hex("4DD0E1");
        public static readonly Color ParcelExpress = Hex("FFD54F");

        // States
        public static readonly Color Ok = Hex("66BB6A");
        public static readonly Color Warning = Hex("FFA726");
        public static readonly Color Critical = Hex("EF5350");
        public static readonly Color Neutral = Hex("ECEFF1");
        public static readonly Color Selection = Hex("FFFFFF");

        public static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.magenta;

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }
}
