using System;
using System.Collections.Generic;

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
            return r;
        }
    }
}
