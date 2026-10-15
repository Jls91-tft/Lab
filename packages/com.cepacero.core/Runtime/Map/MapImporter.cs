using System;
using System.Collections.Generic;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Map
{
    /// <summary>
    /// Turns a parsed <see cref="CepaMapData"/> into a <see cref="BlockWorld"/>
    /// and a <see cref="GroundLayer"/>. Legend entries are matched by name, so
    /// the map file and the game can evolve their ids independently.
    /// </summary>
    public static class MapImporter
    {
        public const string Format = "cepacero.map";
        public const int SupportedVersion = 1;

        private static readonly Dictionary<string, GroundType> GroundByName = new Dictionary<string, GroundType>(StringComparer.Ordinal)
        {
            { "dirt", GroundType.Dirt },
            { "asphalt", GroundType.Asphalt },
            { "sidewalk", GroundType.Sidewalk },
            { "grass", GroundType.Grass },
            { "water", GroundType.Water },
            { "floor", GroundType.Floor },
            { "rail", GroundType.Rail },
        };

        // Map legend name → block registry key ("none" is air).
        private static readonly Dictionary<string, string> BlockKeyByName = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "none", "air" },
            { "building_wall", "building_wall" },
            { "tree", "tree" },
            { "door", "door" },
            { "fence", "fence" },
        };

        /// <exception cref="FormatException">The document is not a valid cepacero.map v1.</exception>
        public static MapImportResult Import(CepaMapData data, BlockRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }
            Validate(data);
            var warnings = new List<string>();
            int w = data.width;
            int h = data.height;

            GroundType[] groundMap = BuildGroundMap(data.legend.ground, warnings);
            ushort[] blockMap = BuildBlockMap(data.legend.blocks, registry, warnings);

            var ground = new GroundLayer(w, h);
            var blocks = new BlockWorld(w, h, registry);
            int[] g = data.layers.ground;
            int[] b = data.layers.blocks;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    ground.Set(x, y, Lookup(groundMap, g[i], "ground", i));
                    ushort block = Lookup(blockMap, b[i], "blocks", i);
                    if (block != BlockRegistry.AirId)
                    {
                        blocks.SetBlock(x, y, block);
                    }
                }
            }
            // The map is the shared starting point every peer loads: not a delta to send.
            blocks.DrainChanges(new List<BlockChange>());

            return new MapImportResult(data.name ?? "", data.attribution ?? "", data.meters_per_tile, blocks, ground,
                ReadBuildings(data, warnings), warnings);
        }

        private static void Validate(CepaMapData data)
        {
            if (data == null)
            {
                throw new FormatException("Map document is null.");
            }
            if (data.format != Format)
            {
                throw new FormatException($"Not a {Format} document (format='{data.format}').");
            }
            if (data.version != SupportedVersion)
            {
                throw new FormatException($"Unsupported {Format} version {data.version}; this build reads v{SupportedVersion}.");
            }
            if (data.width <= 0 || data.height <= 0 || data.width > ushort.MaxValue || data.height > ushort.MaxValue)
            {
                throw new FormatException($"Invalid map size {data.width}x{data.height}.");
            }
            if (data.legend?.ground == null || data.legend.blocks == null)
            {
                throw new FormatException("Missing legend.");
            }
            long cells = (long)data.width * data.height;
            if (data.layers?.ground == null || data.layers.ground.Length != cells)
            {
                throw new FormatException($"Layer 'ground' must have {cells} cells.");
            }
            if (data.layers.blocks == null || data.layers.blocks.Length != cells)
            {
                throw new FormatException($"Layer 'blocks' must have {cells} cells.");
            }
            if (string.IsNullOrEmpty(data.attribution))
            {
                throw new FormatException("Missing attribution (required by the ODbL).");
            }
        }

        private static GroundType[] BuildGroundMap(string[] legend, List<string> warnings)
        {
            var map = new GroundType[legend.Length];
            for (int i = 0; i < legend.Length; i++)
            {
                if (!GroundByName.TryGetValue(legend[i], out map[i]))
                {
                    map[i] = GroundType.Dirt;
                    warnings.Add($"Unknown ground '{legend[i]}' (id {i}) imported as dirt.");
                }
            }
            return map;
        }

        private static ushort[] BuildBlockMap(string[] legend, BlockRegistry registry, List<string> warnings)
        {
            var map = new ushort[legend.Length];
            for (int i = 0; i < legend.Length; i++)
            {
                if (BlockKeyByName.TryGetValue(legend[i], out string key) && registry.TryGet(key, out BlockType type))
                {
                    map[i] = type.Id;
                }
                else
                {
                    map[i] = BlockRegistry.AirId;
                    warnings.Add($"Unknown block '{legend[i]}' (id {i}) imported as air.");
                }
            }
            return map;
        }

        private static T Lookup<T>(T[] map, int id, string layer, int cell)
        {
            if (id < 0 || id >= map.Length)
            {
                throw new FormatException($"Layer '{layer}' cell {cell} has id {id}, outside its legend (0..{map.Length - 1}).");
            }
            return map[id];
        }

        private static List<BuildingInfo> ReadBuildings(CepaMapData data, List<string> warnings)
        {
            var list = new List<BuildingInfo>();
            if (data.buildings == null)
            {
                return list;
            }
            foreach (CepaMapBuilding b in data.buildings)
            {
                if (b?.bbox == null || b.bbox.Length != 4)
                {
                    warnings.Add($"Building {b?.id} skipped: bbox must have 4 values.");
                    continue;
                }
                var doors = new List<Int2>();
                int[] d = b.doors ?? Array.Empty<int>();
                for (int k = 0; k + 1 < d.Length; k += 2)
                {
                    doors.Add(new Int2(d[k], d[k + 1]));
                }
                list.Add(new BuildingInfo(b.id, b.kind ?? "generic", new Int2(b.bbox[0], b.bbox[1]),
                    new Int2(b.bbox[2], b.bbox[3]), b.tiles, doors));
            }
            return list;
        }
    }
}
