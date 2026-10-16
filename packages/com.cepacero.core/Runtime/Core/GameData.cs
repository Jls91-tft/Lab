using System;
using CepaCero.AI;
using CepaCero.Building;
using CepaCero.Items;
using CepaCero.Lore;

namespace CepaCero.Core
{
    /// <summary>All design data of a match, loaded from data/generated/*.json.</summary>
    public sealed class GameData
    {
        public GameData(ZombieCatalog zombies, ItemCatalog items = null, RecipesData recipes = null, LootData loot = null,
            DiaryData diary = null, ConstructionData construction = null)
        {
            Construction = construction;
            Zombies = zombies ?? throw new ArgumentNullException(nameof(zombies));
            Items = items;
            RecipesData = recipes;
            Loot = items != null && loot != null ? new LootRoller(loot, items) : null;
            Diary = diary;
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

        /// <summary>Raw diary entries (null: no diary). Each match builds its own <see cref="TeamDiary"/>.</summary>
        public DiaryData Diary { get; }

        /// <summary>Materials, shapes and costs of construction (D-022); null: only the legacy blocks.</summary>
        public ConstructionData Construction { get; }

        public bool HasItems => Items != null;
    }
}
