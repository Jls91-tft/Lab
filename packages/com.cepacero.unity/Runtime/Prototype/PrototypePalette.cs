using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Interface colours, taken from the 32-colour palette of tools/artgen/artgen/palette.py
    /// (D-018), so bars and text match the sprites.
    /// </summary>
    public static class PrototypePalette
    {
        public static readonly Color Black = Rgb(13, 12, 16);
        public static readonly Color Ink = Rgb(28, 26, 33);
        public static readonly Color Shadow = Rgb(43, 40, 50);
        public static readonly Color Pale = Rgb(186, 181, 170);
        public static readonly Color White = Rgb(232, 228, 214);
        public static readonly Color Red = Rgb(170, 40, 44);
        public static readonly Color Orange = Rgb(220, 120, 40);
        public static readonly Color Yellow = Rgb(236, 200, 72);
        public static readonly Color Lime = Rgb(160, 200, 80);
        public static readonly Color LightBlue = Rgb(96, 150, 210);
        public static readonly Color Cyan = Rgb(120, 210, 220);
        public static readonly Color Tan = Rgb(194, 154, 105);

        /// <summary>Cursor tint over the targeted tile: reachable or not.</summary>
        public static readonly Color InReach = new Color(0.6f, 1f, 0.6f, 0.9f);
        public static readonly Color OutOfReach = new Color(1f, 0.45f, 0.45f, 0.9f);

        private static Color Rgb(byte r, byte g, byte b) => new Color32(r, g, b, 255);
    }
}
