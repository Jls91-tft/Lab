using System;
using System.Collections.Generic;
using CepaCero.Player;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Core
{
    /// <summary>
    /// Binary save of a match: the modified block world, the clock, the players
    /// and progress. The ground is not saved: it comes from the map file, which
    /// is identified by name and size. Zombies are not saved either.
    ///
    /// Layout (little-endian):
    ///   magic "CCSV" (uint) · version (ushort) · map name (string) · width, height (ushort)
    ///   tick (long) · nights survived, zombies killed (int)
    ///   block keys: count (ushort) + key (string) each   ← old saves load after blocks are added or reordered
    ///   players: count (byte) + x, y, stamina (float), health (int) each
    ///   chunks: count (ushort) + <see cref="BlockWorld.WriteChunk"/> each
    ///   checksum: FNV-1a 32 of everything before it (uint)
    /// </summary>
    public static class SaveGame
    {
        public const uint Magic = 0x56534343; // "CCSV"
        public const ushort Version = 1;

        public static void Write(GameSession session, string mapName, ByteWriter writer)
        {
            int start = writer.Length;
            writer.WriteUInt(Magic);
            writer.WriteUShort(Version);
            writer.WriteString(mapName);
            writer.WriteUShort((ushort)session.Blocks.Width);
            writer.WriteUShort((ushort)session.Blocks.Height);
            writer.WriteLong(session.Clock.Tick);
            writer.WriteInt(session.NightsSurvived);
            writer.WriteInt(session.ZombiesKilled);

            BlockRegistry registry = session.Blocks.Registry;
            writer.WriteUShort((ushort)registry.Count);
            for (ushort id = 0; id < registry.Count; id++)
            {
                writer.WriteString(registry[id].Key);
            }

            writer.WriteByte((byte)session.Players.Count);
            foreach (PlayerState p in session.Players)
            {
                writer.WriteFloat(p.Position.X);
                writer.WriteFloat(p.Position.Y);
                writer.WriteFloat(p.Stamina);
                writer.WriteInt(p.Health);
            }

            BlockWorld blocks = session.Blocks;
            writer.WriteUShort((ushort)(blocks.ChunksX * blocks.ChunksY));
            for (int cy = 0; cy < blocks.ChunksY; cy++)
            {
                for (int cx = 0; cx < blocks.ChunksX; cx++)
                {
                    blocks.WriteChunk(cx, cy, writer);
                }
            }
            ArraySegment<byte> written = writer.Segment;
            writer.WriteUInt(Fnv1a(written.Array, written.Offset + start, writer.Length - start));
        }

        /// <summary>
        /// Loads a save into <paramref name="session"/>, which must be freshly created from
        /// the same map. Throws <see cref="FormatException"/> on corrupt or mismatching saves.
        /// </summary>
        public static void Load(byte[] data, GameSession session, string mapName)
        {
            if (data == null || data.Length < 10)
            {
                throw new FormatException("Save file is empty or truncated.");
            }
            uint expected = Fnv1a(data, 0, data.Length - 4);
            var tail = new ByteReader(new ArraySegment<byte>(data, data.Length - 4, 4));
            if (tail.ReadUInt() != expected)
            {
                throw new FormatException("Save file is corrupt (checksum mismatch).");
            }
            var reader = new ByteReader(new ArraySegment<byte>(data, 0, data.Length - 4));
            if (reader.ReadUInt() != Magic)
            {
                throw new FormatException("Not a Cepa Cero save file.");
            }
            ushort version = reader.ReadUShort();
            if (version != Version)
            {
                throw new FormatException($"Unsupported save version {version}.");
            }
            string savedMap = reader.ReadString();
            int width = reader.ReadUShort();
            int height = reader.ReadUShort();
            BlockWorld blocks = session.Blocks;
            if (savedMap != mapName || width != blocks.Width || height != blocks.Height)
            {
                throw new FormatException($"Save is for map '{savedMap}' ({width}x{height}), not '{mapName}' ({blocks.Width}x{blocks.Height}).");
            }
            long tick = reader.ReadLong();
            int nights = reader.ReadInt();
            int kills = reader.ReadInt();

            // Blocks this version does not know are only a problem if the world actually uses them.
            int keyCount = reader.ReadUShort();
            var remap = new ushort[keyCount];
            var unknown = new List<string>();
            for (int i = 0; i < keyCount; i++)
            {
                string key = reader.ReadString();
                if (blocks.Registry.TryGet(key, out BlockType type))
                {
                    remap[i] = type.Id;
                }
                else
                {
                    remap[i] = ushort.MaxValue;
                    unknown.Add(key);
                }
            }

            int playerCount = reader.ReadByte();
            var players = new List<PlayerState>(playerCount);
            for (int i = 0; i < playerCount; i++)
            {
                players.Add(new PlayerState
                {
                    Position = new Float2(reader.ReadFloat(), reader.ReadFloat()),
                    Stamina = reader.ReadFloat(),
                    Health = reader.ReadInt(),
                });
            }

            int chunks = reader.ReadUShort();
            if (chunks != blocks.ChunksX * blocks.ChunksY)
            {
                throw new FormatException("Save chunk count does not match the map.");
            }
            for (int c = 0; c < chunks; c++)
            {
                try
                {
                    blocks.ReadChunk(reader, remap);
                }
                catch (FormatException) when (unknown.Count > 0)
                {
                    throw new FormatException($"Save uses blocks unknown to this version of the game: {string.Join(", ", unknown)}.");
                }
            }
            if (reader.Remaining != 0)
            {
                throw new FormatException("Unexpected data at the end of the save.");
            }
            blocks.DrainChanges(new List<BlockChange>());
            session.Restore(tick, nights, kills, players);
        }

        private static uint Fnv1a(byte[] data, int offset, int count)
        {
            uint hash = 2166136261;
            for (int i = offset; i < offset + count; i++)
            {
                hash ^= data[i];
                hash *= 16777619;
            }
            return hash;
        }
    }
}
