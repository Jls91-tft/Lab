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

        /// <summary>Level of the cell (0 = ground, negative = underground, positive = upper floors).</summary>
        public readonly sbyte Z;

        public readonly BuildLayer Layer;

        /// <summary>Colour and rotation (<see cref="BlockVariant"/>).</summary>
        public readonly byte Variant;

        public BlockChange(ushort x, ushort y, ushort type, ushort health, sbyte z = 0, BuildLayer layer = BuildLayer.Structure, byte variant = 0)
        {
            X = x;
            Y = y;
            Type = type;
            Health = health;
            Z = z;
            Layer = layer;
            Variant = variant;
        }

        public override string ToString() => $"({X},{Y},z{Z},{Layer}) type={Type} hp={Health} variant={Variant}";

        public const int Bytes = 11;

        /// <summary>Wire format: x, y, type, health (ushort each), level (sbyte), layer (byte), variant (byte) = 11 bytes.</summary>
        public void Write(ByteWriter writer)
        {
            writer.WriteUShort(X);
            writer.WriteUShort(Y);
            writer.WriteUShort(Type);
            writer.WriteUShort(Health);
            writer.WriteByte((byte)Z);
            writer.WriteByte((byte)Layer);
            writer.WriteByte(Variant);
        }

        public static BlockChange Read(ByteReader reader)
        {
            ushort x = reader.ReadUShort();
            ushort y = reader.ReadUShort();
            ushort type = reader.ReadUShort();
            ushort health = reader.ReadUShort();
            sbyte z = (sbyte)reader.ReadByte();
            byte layer = reader.ReadByte();
            byte variant = reader.ReadByte();
            if (layer >= Chunk.Layers)
            {
                throw new System.FormatException($"Unknown layer {layer} in a block change.");
            }
            return new BlockChange(x, y, type, health, z, (BuildLayer)layer, variant);
        }

        /// <summary>Count (ushort) then <see cref="Bytes"/> per change.</summary>
        public static void WriteBatch(IReadOnlyList<BlockChange> changes, ByteWriter writer)
        {
            writer.WriteUShort((ushort)changes.Count);
            for (int i = 0; i < changes.Count; i++)
            {
                changes[i].Write(writer);
            }
        }

        public static void ReadBatch(ByteReader reader, List<BlockChange> into)
        {
            int count = reader.ReadUShort();
            for (int i = 0; i < count; i++)
            {
                into.Add(Read(reader));
            }
        }
    }
}
