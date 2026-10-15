using System;
using System.Collections.Generic;
using CepaCero.Items;
using CepaCero.Player;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Core
{
    /// <summary>
    /// Binary save of a match: the modified block world, the clock, the players
    /// with their inventories, containers, items on the floor, unlocked recipes
    /// and progress. The ground is not saved: it comes from the map file,
    /// identified by name and size. Zombies are not saved either (D-013).
    ///
    /// Layout v3 (little-endian):
    ///   magic "CCSV" (uint) · version (ushort) · map name (string) · width, height (ushort)
    ///   tick (long) · nights survived, zombies killed (int) · respawn x, y (float)
    ///   block keys: count (ushort) + key (string) each    ← old saves load after blocks are added or reordered
    ///   item keys:  count (ushort) + id (string) each     ← same for items (0 = match without items)
    ///   players: count (byte) + x, y, stamina, downed seconds (float), health (int), dead (byte), inventory
    ///   unlocked samples: count (ushort) + id (string) each
    ///   containers (<see cref="ContainerStore.Write"/>) · ground piles: count (int) + x, y, stacks
    ///   diary: count (ushort) + entry id (string) each, in discovery order
    ///   chunks: count (ushort) + <see cref="BlockWorld.WriteChunk"/> each
    ///   checksum: FNV-1a 32 of everything before it (uint)
    /// </summary>
    public static class SaveGame
    {
        public const uint Magic = 0x56534343; // "CCSV"
        public const ushort Version = 3;

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
            writer.WriteFloat(session.RespawnPoint.X);
            writer.WriteFloat(session.RespawnPoint.Y);

            BlockRegistry registry = session.Blocks.Registry;
            writer.WriteUShort((ushort)registry.Count);
            for (ushort id = 0; id < registry.Count; id++)
            {
                writer.WriteString(registry[id].Key);
            }
            ItemCatalog items = session.Data.Items;
            writer.WriteUShort((ushort)(items?.Count ?? 0));
            for (ushort i = 0; items != null && i < items.Count; i++)
            {
                writer.WriteString(items[i].id);
            }

            writer.WriteByte((byte)session.Players.Count);
            for (int p = 0; p < session.Players.Count; p++)
            {
                PlayerState s = session.Players[p];
                writer.WriteFloat(s.Position.X);
                writer.WriteFloat(s.Position.Y);
                writer.WriteFloat(s.Stamina);
                writer.WriteFloat(s.DownedSeconds);
                writer.WriteInt(s.Health);
                writer.WriteByte(s.IsDead ? (byte)1 : (byte)0);
                if (items != null)
                {
                    session.Inventories[p].Write(writer);
                }
            }

            if (items != null)
            {
                var samples = new List<string>(session.Recipes.UnlockedSamples);
                writer.WriteUShort((ushort)samples.Count);
                foreach (string sample in samples)
                {
                    writer.WriteString(sample);
                }
                session.Containers.Write(writer);
                IReadOnlyList<ItemPile> piles = session.GroundItems.Piles;
                writer.WriteInt(piles.Count);
                foreach (ItemPile pile in piles)
                {
                    writer.WriteFloat(pile.Position.X);
                    writer.WriteFloat(pile.Position.Y);
                    writer.WriteByte((byte)Math.Min(255, pile.Stacks.Count));
                    for (int k = 0; k < pile.Stacks.Count && k < 255; k++)
                    {
                        writer.WriteUShort(pile.Stacks[k].Item);
                        writer.WriteUShort(pile.Stacks[k].Count);
                        writer.WriteUShort(pile.Stacks[k].Durability);
                    }
                }
            }

            var diary = new List<string>();
            if (session.Diary != null)
            {
                foreach (int entry in session.Diary.Unlocked)
                {
                    diary.Add(session.Diary.Entries[entry].id);
                }
            }
            writer.WriteUShort((ushort)diary.Count);
            foreach (string id in diary)
            {
                writer.WriteString(id);
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
        /// the same map and data. Throws <see cref="FormatException"/> on corrupt or mismatching saves.
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
                throw new FormatException($"Unsupported save version {version} (this build reads v{Version}).");
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
            var respawn = new Float2(reader.ReadFloat(), reader.ReadFloat());

            // Blocks or items this version does not know are only a problem if the save actually uses them.
            var unknown = new List<string>();
            ushort[] blockRemap = ReadKeys(reader, key => blocks.Registry.TryGet(key, out BlockType t) ? t.Id : (ushort?)null, unknown);
            ItemCatalog items = session.Data.Items;
            ushort[] itemRemap = ReadKeys(reader, id => items != null && items.TryGetIndex(id, out ushort i) ? i : (ushort?)null, unknown);
            bool hasItems = itemRemap.Length > 0;
            if (hasItems && items == null)
            {
                throw new FormatException("Save has items but this match was created without item data.");
            }

            try
            {
                int playerCount = reader.ReadByte();
                var players = new List<PlayerState>(playerCount);
                var inventories = new List<Inventory>(playerCount);
                for (int i = 0; i < playerCount; i++)
                {
                    players.Add(new PlayerState
                    {
                        Position = new Float2(reader.ReadFloat(), reader.ReadFloat()),
                        Stamina = reader.ReadFloat(),
                        DownedSeconds = reader.ReadFloat(),
                        Health = reader.ReadInt(),
                        IsDead = reader.ReadByte() != 0,
                    });
                    if (hasItems)
                    {
                        Inventory inv = Inventory.ForPlayer(items);
                        inv.Read(reader, itemRemap);
                        inventories.Add(inv);
                    }
                }

                var samples = new List<string>();
                var piles = new List<KeyValuePair<Float2, List<ItemStack>>>();
                if (hasItems)
                {
                    int sampleCount = reader.ReadUShort();
                    for (int i = 0; i < sampleCount; i++)
                    {
                        samples.Add(reader.ReadString());
                    }
                    session.Containers.Read(reader, itemRemap);
                    int pileCount = reader.ReadInt();
                    for (int i = 0; i < pileCount; i++)
                    {
                        var pos = new Float2(reader.ReadFloat(), reader.ReadFloat());
                        int stacks = reader.ReadByte();
                        var list = new List<ItemStack>(stacks);
                        for (int k = 0; k < stacks; k++)
                        {
                            var s = new ItemStack(reader.ReadUShort(), reader.ReadUShort(), reader.ReadUShort());
                            s.Item = s.Item < itemRemap.Length ? itemRemap[s.Item] : ushort.MaxValue;
                            if (s.Item >= items.Count)
                            {
                                throw new FormatException("Unknown item in a ground pile.");
                            }
                            list.Add(s);
                        }
                        piles.Add(new KeyValuePair<Float2, List<ItemStack>>(pos, list));
                    }
                }

                int diaryCount = reader.ReadUShort();
                var diary = new List<string>(diaryCount);
                for (int i = 0; i < diaryCount; i++)
                {
                    diary.Add(reader.ReadString());
                }

                int chunks = reader.ReadUShort();
                if (chunks != blocks.ChunksX * blocks.ChunksY)
                {
                    throw new FormatException("Save chunk count does not match the map.");
                }
                for (int c = 0; c < chunks; c++)
                {
                    blocks.ReadChunk(reader, blockRemap);
                }
                if (reader.Remaining != 0)
                {
                    throw new FormatException("Unexpected data at the end of the save.");
                }

                blocks.DrainChanges(new List<BlockChange>());
                session.Restore(tick, nights, kills, players, respawn);
                for (int p = 0; p < inventories.Count; p++)
                {
                    var w = new ByteWriter();
                    inventories[p].Write(w);
                    session.Inventories[p].Read(new ByteReader(w.ToArray()));
                }
                foreach (string sample in samples)
                {
                    session.Recipes.Unlock(sample);
                }
                foreach (KeyValuePair<Float2, List<ItemStack>> pile in piles)
                {
                    session.GroundItems.Drop(pile.Key, pile.Value);
                }
                foreach (string id in diary)
                {
                    session.Diary?.Unlock(id);
                }
            }
            catch (FormatException) when (unknown.Count > 0)
            {
                throw new FormatException($"Save uses things unknown to this version of the game: {string.Join(", ", unknown)}.");
            }
        }

        private static ushort[] ReadKeys(ByteReader reader, Func<string, ushort?> resolve, List<string> unknown)
        {
            int count = reader.ReadUShort();
            var remap = new ushort[count];
            for (int i = 0; i < count; i++)
            {
                string key = reader.ReadString();
                ushort? id = resolve(key);
                remap[i] = id ?? ushort.MaxValue;
                if (id == null)
                {
                    unknown.Add(key);
                }
            }
            return remap;
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
