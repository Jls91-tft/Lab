using System;
using System.Collections.Generic;
using CepaCero.World;

namespace CepaCero.AI
{
    /// <summary>
    /// Zombie types available in a match. The index of each type (a byte) is
    /// what network snapshots carry instead of the id string.
    /// </summary>
    public sealed class ZombieCatalog
    {
        public const string Format = "cepacero.bestiary";
        public const int SupportedVersion = 1;

        private readonly List<ZombieDefinition> _byIndex = new List<ZombieDefinition>();
        private readonly Dictionary<string, byte> _indexById = new Dictionary<string, byte>(StringComparer.Ordinal);

        public ZombieCatalog(BestiaryData data)
        {
            if (data == null || data.format != Format)
            {
                throw new FormatException($"Not a {Format} document.");
            }
            if (data.version != SupportedVersion)
            {
                throw new FormatException($"Unsupported {Format} version {data.version}.");
            }
            if (data.zombies == null || data.zombies.Length == 0 || data.zombies.Length > byte.MaxValue)
            {
                throw new FormatException("A bestiary needs between 1 and 255 zombie types.");
            }
            foreach (ZombieDefinition z in data.zombies)
            {
                if (string.IsNullOrEmpty(z.id) || z.health <= 0 || z.speed < 0f)
                {
                    throw new FormatException($"Invalid zombie definition '{z.id}'.");
                }
                if (_indexById.ContainsKey(z.id))
                {
                    throw new FormatException($"Duplicate zombie id '{z.id}'.");
                }
                z.structure_multipliers ??= new MaterialMultipliers();
                z.damage_taken ??= new DamageMultipliers();
                _indexById.Add(z.id, (byte)_byIndex.Count);
                _byIndex.Add(z);
            }
        }

        public int Count => _byIndex.Count;

        public ZombieDefinition this[byte index] => _byIndex[index];

        public bool TryGetIndex(string id, out byte index) => _indexById.TryGetValue(id, out index);

        public byte IndexOf(string id)
        {
            if (!_indexById.TryGetValue(id, out byte index))
            {
                throw new KeyNotFoundException($"Unknown zombie id '{id}'.");
            }
            return index;
        }

        /// <summary>Damage a zombie of this type deals to a block of the given material in one hit.</summary>
        public static int StructureDamage(ZombieDefinition z, BlockMaterial material)
        {
            float m = material switch
            {
                BlockMaterial.Wood => z.structure_multipliers.wood,
                BlockMaterial.Metal => z.structure_multipliers.metal,
                BlockMaterial.Concrete => z.structure_multipliers.concrete,
                _ => 1f,
            };
            return Math.Max(0, (int)Math.Round(z.structure_damage * m, MidpointRounding.AwayFromZero));
        }
    }
}
