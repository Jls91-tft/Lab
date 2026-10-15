using System;
using System.Collections.Generic;

namespace CepaCero.Items
{
    public enum CraftResult : byte
    {
        Ok = 0,
        UnknownRecipe = 1,
        Locked = 2,
        NeedsWorkbench = 3,
        MissingIngredients = 4,
        NoRoom = 5,
        NotAnItemRecipe = 6,
    }

    /// <summary>
    /// The team's recipes. Samples from bosses unlock recipes for everyone
    /// (D-012); building a block costs the ingredients of its build recipe.
    /// </summary>
    public sealed class RecipeBook
    {
        public const string Format = "cepacero.recipes";

        private readonly ItemCatalog _items;
        private readonly List<RecipeDefinition> _recipes = new List<RecipeDefinition>();
        private readonly Dictionary<string, int> _indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, RecipeDefinition> _byBlock = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
        private readonly HashSet<string> _unlockedSamples = new HashSet<string>(StringComparer.Ordinal);

        public RecipeBook(RecipesData data, ItemCatalog items)
        {
            if (data == null || data.format != Format || data.version != 1 || data.recipes == null)
            {
                throw new FormatException($"Not a {Format} v1 document.");
            }
            _items = items ?? throw new ArgumentNullException(nameof(items));
            foreach (RecipeDefinition r in data.recipes)
            {
                if (_indexById.ContainsKey(r.id))
                {
                    throw new FormatException($"Duplicate recipe '{r.id}'.");
                }
                foreach (Ingredient ing in r.ingredients)
                {
                    if (!items.TryGetIndex(ing.item, out _))
                    {
                        throw new FormatException($"Recipe '{r.id}' uses unknown item '{ing.item}'.");
                    }
                }
                if (!r.BuildsBlock && !items.TryGetIndex(r.result, out _))
                {
                    throw new FormatException($"Recipe '{r.id}' makes unknown item '{r.result}'.");
                }
                _indexById.Add(r.id, _recipes.Count);
                _recipes.Add(r);
                if (r.BuildsBlock)
                {
                    _byBlock[r.result_block] = r;
                }
            }
        }

        public IReadOnlyList<RecipeDefinition> Recipes => _recipes;

        public IEnumerable<string> UnlockedSamples => _unlockedSamples;

        /// <summary>Unlocks, for the whole team, the recipes gated by this sample. False if it already was.</summary>
        public bool Unlock(string sampleId) => _unlockedSamples.Add(sampleId);

        public bool IsUnlocked(RecipeDefinition r) => !r.IsLocked || _unlockedSamples.Contains(r.unlocked_by);

        public bool TryGet(string id, out RecipeDefinition recipe)
        {
            recipe = _indexById.TryGetValue(id, out int i) ? _recipes[i] : null;
            return recipe != null;
        }

        public int IndexOf(string id) => _indexById.TryGetValue(id, out int i) ? i : -1;

        /// <summary>The build recipe of a block key, or null if players cannot build it.</summary>
        public RecipeDefinition BuildRecipe(string blockKey) => _byBlock.TryGetValue(blockKey, out RecipeDefinition r) ? r : null;

        public bool HasIngredients(Inventory inventory, RecipeDefinition r)
        {
            foreach (Ingredient ing in r.ingredients)
            {
                if (inventory.Count(_items.IndexOf(ing.item)) < ing.count)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Checks and pays the cost of placing a block. Returns false (and takes nothing) if unaffordable.</summary>
        public bool TryPayForBlock(Inventory inventory, string blockKey)
        {
            RecipeDefinition r = BuildRecipe(blockKey);
            if (r == null || !IsUnlocked(r) || !HasIngredients(inventory, r))
            {
                return false;
            }
            Consume(inventory, r);
            return true;
        }

        public CraftResult Craft(Inventory inventory, int recipeIndex, bool nearWorkbench)
        {
            if (recipeIndex < 0 || recipeIndex >= _recipes.Count)
            {
                return CraftResult.UnknownRecipe;
            }
            RecipeDefinition r = _recipes[recipeIndex];
            if (r.BuildsBlock)
            {
                return CraftResult.NotAnItemRecipe;
            }
            if (!IsUnlocked(r))
            {
                return CraftResult.Locked;
            }
            if (r.NeedsWorkbench && !nearWorkbench)
            {
                return CraftResult.NeedsWorkbench;
            }
            if (!HasIngredients(inventory, r))
            {
                return CraftResult.MissingIngredients;
            }
            ushort result = _items.IndexOf(r.result);
            ItemDefinition def = _items[result];
            int count = Math.Max(1, r.count);
            // Room check after paying: ingredients free up slots. Undo if it still does not fit.
            Consume(inventory, r);
            int added = inventory.Add(result, count);
            if (added < count)
            {
                inventory.Remove(result, added);
                foreach (Ingredient ing in r.ingredients)
                {
                    inventory.Add(_items.IndexOf(ing.item), ing.count);
                }
                return CraftResult.NoRoom;
            }
            return CraftResult.Ok;
        }

        private void Consume(Inventory inventory, RecipeDefinition r)
        {
            foreach (Ingredient ing in r.ingredients)
            {
                inventory.Remove(_items.IndexOf(ing.item), ing.count);
            }
        }
    }
}
