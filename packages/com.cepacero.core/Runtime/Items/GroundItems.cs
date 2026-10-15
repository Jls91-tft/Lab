using System.Collections.Generic;
using CepaCero.Util;

namespace CepaCero.Items
{
    /// <summary>A pile of items on the floor: a zombie's drop, a dropped item or a dead player's backpack.</summary>
    public sealed class ItemPile
    {
        public ushort Id;
        public Float2 Position;
        public readonly List<ItemStack> Stacks = new List<ItemStack>();
    }

    /// <summary>Piles on the ground. Host-authoritative; clients get pile ids, positions and contents.</summary>
    public sealed class GroundItems
    {
        private readonly List<ItemPile> _piles = new List<ItemPile>();
        private ushort _nextId = 1;

        public IReadOnlyList<ItemPile> Piles => _piles;

        public ItemPile Drop(Float2 position, IReadOnlyList<ItemStack> stacks)
        {
            var pile = new ItemPile { Id = _nextId, Position = position };
            _nextId = (ushort)(_nextId == ushort.MaxValue ? 1 : _nextId + 1);
            foreach (ItemStack s in stacks)
            {
                if (!s.IsEmpty)
                {
                    pile.Stacks.Add(s);
                }
            }
            if (pile.Stacks.Count == 0)
            {
                return null;
            }
            _piles.Add(pile);
            return pile;
        }

        /// <summary>The nearest pile within <paramref name="reach"/> tiles, or null.</summary>
        public ItemPile Nearest(Float2 position, float reach)
        {
            ItemPile best = null;
            float bestD2 = reach * reach;
            foreach (ItemPile p in _piles)
            {
                float d2 = (p.Position - position).LengthSquared;
                if (d2 <= bestD2)
                {
                    bestD2 = d2;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>Moves as much of the pile as fits into the inventory. Returns stacks moved; removes empty piles.</summary>
        public int PickUp(ItemPile pile, Inventory into)
        {
            int moved = 0;
            for (int i = pile.Stacks.Count - 1; i >= 0; i--)
            {
                ItemStack s = pile.Stacks[i];
                if (into.PutStack(s))
                {
                    pile.Stacks.RemoveAt(i);
                    moved++;
                }
                else if (!into.Catalog[s.Item].HasDurability)
                {
                    int added = into.Add(s.Item, s.Count);
                    s.Count = (ushort)(s.Count - added);
                    pile.Stacks[i] = s;
                }
            }
            if (pile.Stacks.Count == 0)
            {
                _piles.Remove(pile);
            }
            return moved;
        }
    }
}
