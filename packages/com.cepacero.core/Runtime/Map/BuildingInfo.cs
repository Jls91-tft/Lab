using System.Collections.Generic;
using CepaCero.Util;

namespace CepaCero.Map
{
    /// <summary>A building of the imported map. <see cref="Kind"/> selects its loot table.</summary>
    public sealed class BuildingInfo
    {
        public BuildingInfo(int id, string kind, Int2 min, Int2 max, int tiles, IReadOnlyList<Int2> doors)
        {
            Id = id;
            Kind = kind;
            Min = min;
            Max = max;
            Tiles = tiles;
            Doors = doors;
        }

        public int Id { get; }

        public string Kind { get; }

        public Int2 Min { get; }

        public Int2 Max { get; }

        public int Tiles { get; }

        public IReadOnlyList<Int2> Doors { get; }
    }
}
