using System;
using System.Collections.Generic;
using CepaCero.Items;
using CepaCero.Player;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Net
{
    /// <summary>
    /// The host's referee for inventory actions: equip, craft, use, pick up,
    /// drop and move items to and from containers. Not rate limited (UI
    /// actions), but every one is validated: reach, room, ingredients, recipes.
    /// </summary>
    public sealed class HostItemRules
    {
        private const float WorkbenchReach = 2.5f;

        private readonly BlockWorld _blocks;
        private readonly PlayerSettings _settings;
        private readonly ItemCatalog _items;
        private readonly RecipeBook _recipes;
        private readonly LootRoller _loot;
        private readonly ContainerStore _containers;
        private readonly GroundItems _ground;
        private readonly DeterministicRandom _rng;
        private readonly ushort _workbench;

        public HostItemRules(BlockWorld blocks, PlayerSettings settings, ItemCatalog items, RecipeBook recipes, LootRoller loot,
            ContainerStore containers, GroundItems ground, DeterministicRandom rng)
        {
            _blocks = blocks;
            _settings = settings;
            _items = items;
            _recipes = recipes;
            _loot = loot;
            _containers = containers;
            _ground = ground;
            _rng = rng;
            _workbench = blocks.Registry.TryGet("workbench", out BlockType wb) ? wb.Id : ushort.MaxValue;
        }

        /// <summary>Health restored by the last UseItem.</summary>
        public int LastHeal { get; private set; }

        /// <summary>Sample analysed by the last UseItem (recipes unlocked for the team), or null.</summary>
        public string LastUnlockedSample { get; private set; }

        public IntentResult Apply(int playerIndex, in PlayerIntent intent, IList<PlayerState> players, IReadOnlyList<Inventory> inventories)
        {
            LastHeal = 0;
            LastUnlockedSample = null;
            if (playerIndex < 0 || playerIndex >= players.Count || playerIndex >= inventories.Count)
            {
                return IntentResult.NotAllowed;
            }
            if (players[playerIndex].IsDowned)
            {
                return IntentResult.PlayerDowned;
            }
            Inventory inv = inventories[playerIndex];
            switch (intent.Type)
            {
                case IntentType.SelectHotbar:
                    if (intent.Arg >= inv.HotbarSlots)
                    {
                        return IntentResult.InvalidTarget;
                    }
                    inv.SelectHotbar(intent.Arg);
                    return IntentResult.Ok;
                case IntentType.MoveToHotbar:
                    return inv.MoveToHotbar(intent.X, intent.Arg) ? IntentResult.Ok : IntentResult.NoRoom;
                case IntentType.MoveToBag:
                    return inv.MoveToBag(intent.Arg) ? IntentResult.Ok : IntentResult.NoRoom;
                case IntentType.Craft:
                    return Craft(players[playerIndex], inv, intent.Arg);
                case IntentType.UseItem:
                    return Use(playerIndex, players, inv);
                case IntentType.PickUp:
                    return PickUp(players[playerIndex], inv);
                case IntentType.DropStack:
                    if (!inv.TakeBagStack(intent.X, out ItemStack dropped))
                    {
                        return IntentResult.InvalidTarget;
                    }
                    _ground.Drop(players[playerIndex].Position, new[] { dropped });
                    return IntentResult.Ok;
                case IntentType.TakeFromContainer:
                    return Take(players[playerIndex], inv, intent);
                case IntentType.PutInContainer:
                    return Put(players[playerIndex], inv, intent);
                default:
                    return IntentResult.NotAllowed;
            }
        }

        private IntentResult Craft(PlayerState player, Inventory inv, int recipeIndex)
        {
            CraftResult result = _recipes.Craft(inv, recipeIndex, NearWorkbench(player.Position));
            switch (result)
            {
                case CraftResult.Ok:
                    return IntentResult.Ok;
                case CraftResult.Locked:
                    return IntentResult.Locked;
                case CraftResult.NeedsWorkbench:
                    return IntentResult.NeedsWorkbench;
                case CraftResult.MissingIngredients:
                    return IntentResult.MissingIngredients;
                case CraftResult.NoRoom:
                    return IntentResult.NoRoom;
                default:
                    return IntentResult.InvalidTarget;
            }
        }

        private IntentResult Use(int playerIndex, IList<PlayerState> players, Inventory inv)
        {
            ItemStack equipped = inv.Equipped;
            if (equipped.IsEmpty)
            {
                return IntentResult.NothingToUse;
            }
            ItemDefinition def = _items[equipped.Item];
            if (def.category == "sample")
            {
                // Analysing a sample teaches the whole team its recipes (D-012).
                inv.Remove(equipped.Item, 1);
                _recipes.Unlock(def.id);
                LastUnlockedSample = def.id;
                return IntentResult.Ok;
            }
            if (def.heal <= 0)
            {
                return IntentResult.NothingToUse;
            }
            PlayerState state = players[playerIndex];
            if (state.Health >= _settings.MaxHealth)
            {
                return IntentResult.NothingToUse;
            }
            inv.Remove(equipped.Item, 1);
            int healed = Math.Min(def.heal, _settings.MaxHealth - state.Health);
            state.Health += healed;
            players[playerIndex] = state;
            LastHeal = healed;
            return IntentResult.Ok;
        }

        private IntentResult PickUp(PlayerState player, Inventory inv)
        {
            ItemPile pile = _ground.Nearest(player.Position, _settings.PickUpReach);
            if (pile == null)
            {
                return IntentResult.InvalidTarget;
            }
            int version = inv.Version;
            _ground.PickUp(pile, inv);
            return inv.Version != version ? IntentResult.Ok : IntentResult.NoRoom;
        }

        private IntentResult Take(PlayerState player, Inventory inv, in PlayerIntent intent)
        {
            Inventory box = OpenInReach(player, intent.X, intent.Y);
            if (box == null)
            {
                return IntentResult.OutOfReach;
            }
            if (!box.TakeBagStack(intent.Arg, out ItemStack stack))
            {
                return IntentResult.InvalidTarget;
            }
            if (inv.PutStack(stack))
            {
                return IntentResult.Ok;
            }
            box.PutStack(stack); // put it back: the player has no room
            return IntentResult.NoRoom;
        }

        private IntentResult Put(PlayerState player, Inventory inv, in PlayerIntent intent)
        {
            Inventory box = OpenInReach(player, intent.X, intent.Y);
            if (box == null)
            {
                return IntentResult.OutOfReach;
            }
            if (!inv.TakeBagStack(intent.Arg, out ItemStack stack))
            {
                return IntentResult.InvalidTarget;
            }
            if (box.PutStack(stack))
            {
                return IntentResult.Ok;
            }
            inv.PutStack(stack);
            return IntentResult.NoRoom;
        }

        /// <summary>Contents of the container at (x, y) if the player can reach it (rolls its loot the first time).</summary>
        public Inventory OpenInReach(PlayerState player, int x, int y)
        {
            var cell = new Int2(x, y);
            var center = new Float2(x + 0.5f, y + 0.5f);
            if (!_containers.Has(cell) || (center - player.Position).LengthSquared > _settings.Reach * _settings.Reach)
            {
                return null;
            }
            return _containers.Open(cell, _loot, _rng);
        }

        private bool NearWorkbench(Float2 position)
        {
            int r = (int)MathF.Ceiling(WorkbenchReach);
            Int2 c = position.Tile;
            for (int y = c.Y - r; y <= c.Y + r; y++)
            {
                for (int x = c.X - r; x <= c.X + r; x++)
                {
                    if (_blocks.InBounds(x, y) && _blocks.GetTypeId(x, y) == _workbench
                        && (new Float2(x + 0.5f, y + 0.5f) - position).LengthSquared <= WorkbenchReach * WorkbenchReach)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
