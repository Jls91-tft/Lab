using System;
using System.Collections.Generic;
using CepaCero.Util;

namespace CepaCero.AI
{
    /// <summary>One zombie as a client sees it.</summary>
    public struct ZombieSnapshotEntry
    {
        public ushort NetId;
        public Float2 Position;
        public byte Type;
        public ZombieState State;
        public byte Facing;
        public sbyte Level;
    }

    /// <summary>
    /// Compact zombie snapshots, host → client. Only zombies near the
    /// receiving player are sent (interest management).
    ///
    /// Wire format: tick (uint), count (ushort), then 8 bytes per zombie:
    /// netId (ushort), x and y in 1/16 tile (ushort each, so maps up to
    /// 4096 tiles), type (byte), state (high 4 bits) | facing (low 4 bits).
    /// 100 zombies ≈ 806 bytes; at 10 Hz ≈ 8 KB/s per client.
    /// </summary>
    public static class ZombieSnapshotCodec
    {
        public const int BytesPerZombie = 9;
        public const int HeaderBytes = 6;
        public const float PositionScale = 16f;

        /// <summary>Writes the zombies within <paramref name="radius"/> tiles of <paramref name="viewer"/>, nearest first.</summary>
        public static int Write(ZombieSimulation sim, uint tick, Float2 viewer, float radius, ByteWriter writer,
            List<int> scratch = null, int maxZombies = 255, int viewerLevel = 0)
        {
            scratch ??= new List<int>();
            scratch.Clear();
            float r2 = radius * radius;
            for (int i = 0; i < sim.Capacity; i++)
            {
                if (sim.IsAlive(i) && sim.GetLevel(i) == viewerLevel && (sim.GetPosition(i) - viewer).LengthSquared <= r2)
                {
                    scratch.Add(i);
                }
            }
            if (scratch.Count > maxZombies)
            {
                scratch.Sort((a, b) => (sim.GetPosition(a) - viewer).LengthSquared.CompareTo((sim.GetPosition(b) - viewer).LengthSquared));
                scratch.RemoveRange(maxZombies, scratch.Count - maxZombies);
            }

            writer.WriteUInt(tick);
            writer.WriteUShort((ushort)scratch.Count);
            foreach (int i in scratch)
            {
                Float2 p = sim.GetPosition(i);
                writer.WriteUShort(sim.GetNetId(i));
                writer.WriteUShort(Quantize(p.X));
                writer.WriteUShort(Quantize(p.Y));
                writer.WriteByte((byte)(sbyte)sim.GetLevel(i));
                writer.WriteByte(sim.GetTypeIndex(i));
                writer.WriteByte((byte)(((byte)sim.GetState(i) << 4) | (sim.GetFacing(i) & 0x0F)));
            }
            return scratch.Count;
        }

        public static uint Read(ByteReader reader, List<ZombieSnapshotEntry> into)
        {
            uint tick = reader.ReadUInt();
            int count = reader.ReadUShort();
            for (int k = 0; k < count; k++)
            {
                var e = new ZombieSnapshotEntry
                {
                    NetId = reader.ReadUShort(),
                    Position = new Float2(reader.ReadUShort() / PositionScale, reader.ReadUShort() / PositionScale),
                    Level = (sbyte)reader.ReadByte(),
                    Type = reader.ReadByte(),
                };
                byte packed = reader.ReadByte();
                e.State = (ZombieState)(packed >> 4);
                e.Facing = (byte)(packed & 0x0F);
                into.Add(e);
            }
            return tick;
        }

        private static ushort Quantize(float v) => (ushort)Math.Clamp((int)MathF.Round(v * PositionScale), 0, ushort.MaxValue);
    }
}
