namespace CepaCero.World
{
    /// <summary>Immutable definition of a kind of block. Instances live in a <see cref="BlockRegistry"/>.</summary>
    public sealed class BlockType
    {
        public BlockType(ushort id, string key, ushort maxHealth, BlockMaterial material, bool isSolid,
            bool isIndestructible = false, ushort toggleTo = 0, int contactDamage = 0, int contactWear = 0, BlockBuildInfo build = null)
        {
            Build = build;
            Id = id;
            Key = key;
            MaxHealth = maxHealth;
            Material = material;
            IsSolid = isSolid;
            IsIndestructible = isIndestructible;
            ToggleTo = toggleTo;
            ContactDamage = contactDamage;
            ContactWear = contactWear;
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

        /// <summary>Damage dealt to a zombie standing on it, once per <c>ZombieSimulation.TrapInterval</c> (traps).</summary>
        public int ContactDamage { get; }

        /// <summary>Health the block loses each time it hurts someone (traps wear out).</summary>
        public int ContactWear { get; }

        /// <summary>Cost, tool, noise, size and layer from the construction data (D-022); null for the legacy blocks.</summary>
        public BlockBuildInfo Build { get; }

        /// <summary>The layer of the cell this block lives in: legacy blocks are all structure.</summary>
        public BuildLayer Layer => Build != null ? Build.Layer : BuildLayer.Structure;

        public bool IsTrap => ContactDamage > 0;

        public bool IsAir => Id == BlockRegistry.AirId;

        public override string ToString() => $"{Key}#{Id}";
    }
}
