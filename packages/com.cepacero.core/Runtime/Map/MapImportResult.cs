using System.Collections.Generic;
using CepaCero.World;

namespace CepaCero.Map
{
    public sealed class MapImportResult
    {
        internal MapImportResult(string name, string attribution, float metersPerTile, BlockWorld blocks,
            GroundLayer ground, IReadOnlyList<BuildingInfo> buildings, IReadOnlyList<string> warnings)
        {
            Name = name;
            Attribution = attribution;
            MetersPerTile = metersPerTile;
            Blocks = blocks;
            Ground = ground;
            Buildings = buildings;
            Warnings = warnings;
        }

        public string Name { get; }

        /// <summary>ODbL attribution. Must be shown in the credits and the map info screen.</summary>
        public string Attribution { get; }

        public float MetersPerTile { get; }

        public BlockWorld Blocks { get; }

        public GroundLayer Ground { get; }

        public IReadOnlyList<BuildingInfo> Buildings { get; }

        /// <summary>Non-fatal issues (unknown legend entries mapped to a default).</summary>
        public IReadOnlyList<string> Warnings { get; }
    }
}
