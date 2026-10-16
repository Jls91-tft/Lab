using System;
using System.Collections.Generic;
using CepaCero.Building;

namespace CepaCero.World
{
    /// <summary>
    /// All block types of a game session. Ids are part of saves and network
    /// messages: append new types at the end, never renumber.
    /// </summary>
    public sealed class BlockRegistry
    {
        public const ushort AirId = 0;

        private readonly List<BlockType> _byId = new List<BlockType>();
        private readonly Dictionary<string, BlockType> _byKey = new Dictionary<string, BlockType>(StringComparer.Ordinal);

        public int Count => _byId.Count;

        public BlockType this[ushort id] => _byId[id];

        public void Add(BlockType type)
        {
            if (type.Id != _byId.Count)
            {
                throw new ArgumentException($"Block ids must be consecutive: expected {_byId.Count}, got {type.Id} ({type.Key}).");
            }
            if (_byKey.ContainsKey(type.Key))
            {
                throw new ArgumentException($"Duplicate block key '{type.Key}'.");
            }
            _byId.Add(type);
            _byKey.Add(type.Key, type);
        }

        public bool TryGet(string key, out BlockType type) => _byKey.TryGetValue(key, out type);

        public BlockType Get(string key)
        {
            if (!_byKey.TryGetValue(key, out BlockType type))
            {
                throw new KeyNotFoundException($"Unknown block key '{key}'.");
            }
            return type;
        }

        public bool IsValid(ushort id) => id < _byId.Count;

        /// <summary>
        /// Appends the blocks of the construction data (D-022) after the existing ones. Ids follow the order of the data, so
        /// saves and the network stay valid as long as new blocks are added at the end of the data too.
        /// </summary>
        public void AddConstruction(ConstructionData data)
        {
            if (data?.blocks == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            int first = _byId.Count;
            var ids = new Dictionary<string, ushort>(StringComparer.Ordinal);
            for (int i = 0; i < data.blocks.Length; i++)
            {
                if (ids.ContainsKey(data.blocks[i].key))
                {
                    throw new ArgumentException($"Duplicate construction block '{data.blocks[i].key}'.");
                }
                ids.Add(data.blocks[i].key, (ushort)(first + i));
            }
            foreach (ConstructionBlock b in data.blocks)
            {
                ushort toggle = 0;
                if (!string.IsNullOrEmpty(b.toggle_to) && !ids.TryGetValue(b.toggle_to, out toggle))
                {
                    throw new ArgumentException($"Block '{b.key}' toggles to unknown '{b.toggle_to}'.");
                }
                var info = new BlockBuildInfo(b.shape, b.material, ParseLayer(b.layer), Math.Max(1, b.width), Math.Max(1, b.height), b.see_through,
                    b.tool, b.noise, b.paintable, b.build_seconds, b.connects, b.is_open_variant, ToCost(b.cost), ToCost(b.drops));
                Add(new BlockType(ids[b.key], b.key, (ushort)Math.Min(ushort.MaxValue, Math.Max(1, b.hp)), MaterialFor(b.material), b.solid,
                    toggleTo: toggle, build: info));
            }
        }

        /// <summary>The legacy blocks plus everything in the construction data.</summary>
        public static BlockRegistry CreateDefault(ConstructionData construction)
        {
            BlockRegistry r = CreateDefault();
            if (construction != null)
            {
                r.AddConstruction(construction);
            }
            return r;
        }

        private static BuildLayer ParseLayer(string layer)
        {
            switch (layer)
            {
                case "floor": return BuildLayer.Floor;
                case "roof": return BuildLayer.Roof;
                case "object": return BuildLayer.Object;
                default: return BuildLayer.Structure;
            }
        }

        private static IReadOnlyList<BlockCostItem> ToCost(ConstructionCost[] cost)
        {
            if (cost == null || cost.Length == 0)
            {
                return BlockBuildInfo.NoCost;
            }
            var list = new BlockCostItem[cost.Length];
            for (int i = 0; i < cost.Length; i++)
            {
                list[i] = new BlockCostItem(cost[i].item, cost[i].count);
            }
            return list;
        }

        /// <summary>What a material is "made of" for the special interactions of zombies (acid vs wood, La Mole vs metal).</summary>
        public static BlockMaterial MaterialFor(string materialId)
        {
            switch (materialId)
            {
                case "pallet": case "drywall": case "wood": case "log": case "glass": case "thatch": return BlockMaterial.Wood;
                case "scrap": case "container": case "steel": case "sheet": case "glasshouse": return BlockMaterial.Metal;
                case "brick": case "stone": case "concrete": case "rebar": case "tile": case "flat": return BlockMaterial.Concrete;
                case "sandbag": case "adobe": case "earth": case "rock": return BlockMaterial.Natural;
                default: return BlockMaterial.None;
            }
        }

        /// <summary>Prototype defaults. Health values follow docs/bestiario.md (wood 100, metal 300, concrete 600).</summary>
        public static BlockRegistry CreateDefault()
        {
            var r = new BlockRegistry();
            r.Add(new BlockType(0, "air", 0, BlockMaterial.None, isSolid: false));
            r.Add(new BlockType(1, "building_wall", 600, BlockMaterial.Concrete, isSolid: true));
            r.Add(new BlockType(2, "tree", 150, BlockMaterial.Natural, isSolid: true));
            r.Add(new BlockType(3, "door", 100, BlockMaterial.Wood, isSolid: true, toggleTo: 4));
            r.Add(new BlockType(4, "door_open", 100, BlockMaterial.Wood, isSolid: false, toggleTo: 3));
            r.Add(new BlockType(5, "fence", 60, BlockMaterial.Wood, isSolid: true));
            r.Add(new BlockType(6, "wood_wall", 100, BlockMaterial.Wood, isSolid: true));
            r.Add(new BlockType(7, "metal_wall", 300, BlockMaterial.Metal, isSolid: true));
            r.Add(new BlockType(8, "concrete_wall", 600, BlockMaterial.Concrete, isSolid: true));
            r.Add(new BlockType(9, "workbench", 150, BlockMaterial.Wood, isSolid: true));
            r.Add(new BlockType(10, "chest", 150, BlockMaterial.Wood, isSolid: true));
            r.Add(new BlockType(11, "spike_trap", 120, BlockMaterial.Wood, isSolid: false, contactDamage: 15, contactWear: 10));
            return r;
        }
    }
}
