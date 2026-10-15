namespace CepaCero.Items
{
    /// <summary>A pile of one item type. Weapons and tools are stacks of 1 with durability left.</summary>
    public struct ItemStack
    {
        public ushort Item;
        public ushort Count;

        /// <summary>Uses left before it breaks (0 for items without durability).</summary>
        public ushort Durability;

        public bool IsEmpty => Count == 0;

        public ItemStack(ushort item, ushort count, ushort durability = 0)
        {
            Item = item;
            Count = count;
            Durability = durability;
        }

        public override string ToString() => IsEmpty ? "(empty)" : $"#{Item}×{Count}" + (Durability > 0 ? $" [{Durability}]" : "");
    }
}
