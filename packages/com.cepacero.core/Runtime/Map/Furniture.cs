using CepaCero.Util;

namespace CepaCero.Map
{
    /// <summary>A piece of furniture planned inside a building (D-026): where it stands and what it is. Same plan on every peer.</summary>
    public readonly struct Furniture
    {
        public readonly Int2 Cell;
        public readonly string Kind;

        public Furniture(Int2 cell, string kind)
        {
            Cell = cell;
            Kind = kind;
        }

        /// <summary>Sprite name in the world atlas (the view falls back to a plain box when it is missing).</summary>
        public string Sprite => FurnitureKinds.Of(Kind).Sprite;

        /// <summary>The loot table this piece is rolled with, or null when it is only decoration. An empty string means "the building's own kind".</summary>
        public string Loot => FurnitureKinds.Of(Kind).Loot;

        public bool IsContainer => Loot != null;
    }
}
