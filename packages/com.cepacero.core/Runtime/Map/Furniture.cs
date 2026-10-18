using CepaCero.Util;

namespace CepaCero.Map
{
    /// <summary>A piece of furniture planned inside a building (D-026): where it stands and what it is. Same plan on every peer.</summary>
    public readonly struct Furniture
    {
        public readonly Int2 Cell;
        public readonly string Kind;

        public Furniture(Int2 cell, string kind, char face = ' ')
        {
            Cell = cell;
            Kind = kind;
            Face = face;
        }

        /// <summary>Sprite name in the world atlas (the view falls back to a plain box when it is missing).</summary>
        public string Sprite => FurnitureKinds.Of(Kind).Sprite;

        /// <summary>The loot table this piece is rolled with, or null when it is only decoration. An empty string means "the building's own kind".</summary>
        public string Loot => FurnitureKinds.Of(Kind).Loot;

        public bool IsContainer => Loot != null;

        /// <summary>Hangs on a wall: <see cref="Cell"/> is the wall, <see cref="Face"/> the side it shows on.</summary>
        public bool OnWall => FurnitureKinds.Of(Kind).OnWall;

        public bool IsFlat => FurnitureKinds.Of(Kind).Flat;

        /// <summary>For wall pieces: 's' (south face: the wall is north of the room) or 'w' (west face: the wall is east of it).</summary>
        public readonly char Face;
    }
}
