using System;
using CepaCero.AI;
using CepaCero.Items;

namespace CepaCero.Core
{
    /// <summary>All design data of a match, loaded from data/generated/*.json.</summary>
    public sealed class GameData
    {
        public GameData(ZombieCatalog zombies, ItemCatalog items = null, RecipesData recipes = null, LootData loot = null)
        {
            Zombies = zombies ?? throw new ArgumentNullException(nameof(zombies));
            Items = items;
            RecipesData = recipes;
            Loot = items != null && loot != null ? new LootRoller(loot, items) : null;
            if (items != null && recipes == null)
            {
                throw new ArgumentException("Items need their recipes.", nameof(recipes));
            }
        }

        public ZombieCatalog Zombies { get; }

        /// <summary>Null: a match without items (bare hands, free building). Used by some tests and benchmarks.</summary>
        public ItemCatalog Items { get; }

        /// <summary>Raw recipes: each match builds its own <see cref="RecipeBook"/> (unlocks are per match).</summary>
        public RecipesData RecipesData { get; }

        public LootRoller Loot { get; }

        public bool HasItems => Items != null;
    }
}
