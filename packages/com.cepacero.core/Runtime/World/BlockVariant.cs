namespace CepaCero.World
{
    /// <summary>
    /// The extra byte of a cell (D-022): colour in the low 5 bits (0 = natural, 1..16 = paint) and rotation in bits 5-6
    /// (quarter turns). It travels with the block in deltas and snapshots.
    /// </summary>
    public static class BlockVariant
    {
        public const int MaxColor = 16;

        public static byte Make(int color, int rotation) => (byte)((color & 31) | ((rotation & 3) << 5));

        public static int Color(byte variant) => variant & 31;

        public static int Rotation(byte variant) => (variant >> 5) & 3;

        /// <summary>What the host accepts from a client: paint only on paintable pieces and only the colours that exist.</summary>
        public static byte Sanitize(byte variant, BlockType type)
        {
            int color = Color(variant);
            if (color > MaxColor || type.Build == null || !type.Build.Paintable)
            {
                color = 0;
            }
            return Make(color, Rotation(variant));
        }
    }
}
