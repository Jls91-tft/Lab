using System.Collections.Generic;
using CepaCero.Util;

namespace CepaCero.World
{
    /// <summary>One block cell after a mutation. Batches of these are the network delta for the world.</summary>
    public readonly struct BlockChange
    {
        public readonly ushort X;
        public readonly ushort Y;
        public readonly ushort Type;
        public readonly ushort Health;

        public BlockChange(ushort x, ushort y, ushort type, ushort health)
        {
            X = x;
            Y = y;
            Type = type;
            Health = health;
        }

        public override string ToString() => $"({X},{Y}) type={Type} hp={Health}";

        /// <summary>Wire format: count (ushort) then 8 bytes per change.</summary>
        public static void WriteBatch(IReadOnlyList<BlockChange> changes, ByteWriter writer)
        {
            writer.WriteUShort((ushort)changes.Count);
            for (int i = 0; i < changes.Count; i++)
            {
                BlockChange c = changes[i];
                writer.WriteUShort(c.X);
                writer.WriteUShort(c.Y);
                writer.WriteUShort(c.Type);
                writer.WriteUShort(c.Health);
            }
        }

        public static void ReadBatch(ByteReader reader, List<BlockChange> into)
        {
            int count = reader.ReadUShort();
            for (int i = 0; i < count; i++)
            {
                into.Add(new BlockChange(reader.ReadUShort(), reader.ReadUShort(), reader.ReadUShort(), reader.ReadUShort()));
            }
        }
    }
}
