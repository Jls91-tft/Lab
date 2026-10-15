namespace CepaCero.World
{
    /// <summary>Immutable definition of a kind of block. Instances live in a <see cref="BlockRegistry"/>.</summary>
    public sealed class BlockType
    {
        public BlockType(ushort id, string key, ushort maxHealth, BlockMaterial material, bool isSolid,
            bool isIndestructible = false, ushort toggleTo = 0)
        {
            Id = id;
            Key = key;
            MaxHealth = maxHealth;
            Material = material;
            IsSolid = isSolid;
            IsIndestructible = isIndestructible;
            ToggleTo = toggleTo;
        }

        public ushort Id { get; }

        /// <summary>Stable text key, e.g. "wood_wall". Used by data files and the map importer.</summary>
        public string Key { get; }

        public ushort MaxHealth { get; }

        public BlockMaterial Material { get; }

        /// <summary>Blocks movement of players and zombies.</summary>
        public bool IsSolid { get; }

        public bool IsIndestructible { get; }

        /// <summary>Block this one becomes when toggled (doors). 0 = not toggleable.</summary>
        public ushort ToggleTo { get; }

        public bool IsAir => Id == BlockRegistry.AirId;

        public override string ToString() => $"{Key}#{Id}";
    }
}
