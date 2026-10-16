using System;

namespace CepaCero.Building
{
    /// <summary>The 11 roof sprites of tools/spritegen (<c>roof_&lt;material&gt;_&lt;name&gt;</c>), chosen from the roof around a cell.</summary>
    public enum RoofShape : byte
    {
        Flat,
        SlopeN,
        SlopeE,
        SlopeS,
        SlopeW,
        CornerNE,
        CornerNW,
        CornerSE,
        CornerSW,
        RidgeH,
        RidgeV,
    }

    /// <summary>
    /// Roofs are one block per material; their shape is not stored but read from the neighbours, like walls join (D-022). A cell at the
    /// edge slopes towards it, two open sides make a corner, and flat cells in the middle of a long strip carry a ridge along it.
    /// </summary>
    public static class RoofShapes
    {
        private const int RidgeReach = 3;

        public static RoofShape At(Func<int, int, bool> hasRoof, int x, int y)
        {
            bool n = !hasRoof(x, y + 1), e = !hasRoof(x + 1, y), s = !hasRoof(x, y - 1), w = !hasRoof(x - 1, y);
            if (n && e) { return RoofShape.CornerNE; }
            if (n && w) { return RoofShape.CornerNW; }
            if (s && e) { return RoofShape.CornerSE; }
            if (s && w) { return RoofShape.CornerSW; }
            if (n) { return RoofShape.SlopeN; }
            if (s) { return RoofShape.SlopeS; }
            if (e) { return RoofShape.SlopeE; }
            if (w) { return RoofShape.SlopeW; }
            bool vertical = !hasRoof(x, y + RidgeReach) || !hasRoof(x, y - RidgeReach);
            bool horizontal = !hasRoof(x + RidgeReach, y) || !hasRoof(x - RidgeReach, y);
            if (vertical && !horizontal) { return RoofShape.RidgeH; }
            if (horizontal && !vertical) { return RoofShape.RidgeV; }
            return RoofShape.Flat;
        }

        /// <summary>The sprite name suffix: "flat", "slope_n", "corner_ne", "ridge_h"...</summary>
        public static string SpriteSuffix(RoofShape shape)
        {
            switch (shape)
            {
                case RoofShape.SlopeN: return "slope_n";
                case RoofShape.SlopeE: return "slope_e";
                case RoofShape.SlopeS: return "slope_s";
                case RoofShape.SlopeW: return "slope_w";
                case RoofShape.CornerNE: return "corner_ne";
                case RoofShape.CornerNW: return "corner_nw";
                case RoofShape.CornerSE: return "corner_se";
                case RoofShape.CornerSW: return "corner_sw";
                case RoofShape.RidgeH: return "ridge_h";
                case RoofShape.RidgeV: return "ridge_v";
                default: return "flat";
            }
        }
    }
}
