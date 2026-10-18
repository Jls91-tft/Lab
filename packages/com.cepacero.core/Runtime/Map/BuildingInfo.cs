using System.Collections.Generic;
using CepaCero.Util;

namespace CepaCero.Map
{
    /// <summary>A building of the imported map. <see cref="Kind"/> selects its loot table.</summary>
    public sealed class BuildingInfo
    {
        public BuildingInfo(int id, string kind, Int2 min, Int2 max, int tiles, IReadOnlyList<Int2> doors,
            int levels = 1, int underground = 0, string roof = "")
        {
            Levels = levels < 1 ? 1 : levels;
            Underground = underground < 0 ? 0 : underground;
            Roof = roof ?? "";
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

        /// <summary>Storeys above ground with the ground floor (1 = single storey).</summary>
        public int Levels { get; }

        /// <summary>Basement levels.</summary>
        public int Underground { get; }

        /// <summary>tile, sheet, thatch, flat or glasshouse; empty when the map does not say.</summary>
        public string Roof { get; }

        /// <summary>The furniture planned inside (D-026); empty until the map importer furnishes the building.</summary>
        public IReadOnlyList<Furniture> Furniture => _furniture;

        private List<Furniture> _furniture = new List<Furniture>();

        internal void SetFurniture(List<Furniture> furniture) => _furniture = furniture ?? new List<Furniture>();

        /// <summary>The paint of each room's walls, by floor cell (D-027): wallpaper, tiles or paint seen from inside; empty for bare rooms.</summary>
        public IReadOnlyDictionary<Int2, string> WallPaint => _wallPaint;

        private Dictionary<Int2, string> _wallPaint = new Dictionary<Int2, string>();

        internal void SetWallPaint(Dictionary<Int2, string> paint) => _wallPaint = paint ?? new Dictionary<Int2, string>();
    }
}
