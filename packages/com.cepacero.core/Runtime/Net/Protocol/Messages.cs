using System;
using System.Collections.Generic;
using CepaCero.AI;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Net.Protocol
{
    /// <summary>
    /// Encoders for every message. Readers live in <see cref="ClientMatchState"/>
    /// (host → client) and <see cref="HostBroadcaster"/> (client → host).
    /// </summary>
    public static class Messages
    {
        /// <summary>Bump on any incompatible change; Welcome refuses mismatches.</summary>
        public const ushort ProtocolVersion = 5;

        public const byte FlagSprinting = 1;
        public const byte FlagDowned = 2;
        public const byte FlagDead = 4;

        /// <summary>Starts a framed message. Dispose the returned scope to write its length.</summary>
        public static Frame Begin(ByteWriter writer, MessageType type)
        {
            writer.WriteByte((byte)type);
            return new Frame(writer, writer.ReserveUShort());
        }

        public readonly struct Frame : IDisposable
        {
            private readonly ByteWriter _writer;
            private readonly int _lengthAt;

            public Frame(ByteWriter writer, int lengthAt)
            {
                _writer = writer;
                _lengthAt = lengthAt;
            }

            public void Dispose()
            {
                int length = _writer.Length - _lengthAt - 2;
                if (length > ushort.MaxValue)
                {
                    throw new InvalidOperationException($"Message too long ({length} bytes).");
                }
                _writer.PatchUShort(_lengthAt, (ushort)length);
            }
        }

        /// <summary>
        /// Fingerprint of the design data (block keys, zombie ids, item ids): host and
        /// client must run the same game data or ids would mean different things.
        /// </summary>
        public static uint DataFingerprint(BlockRegistry blocks, GameData data)
        {
            uint hash = 2166136261;
            void Mix(string s)
            {
                foreach (char c in s)
                {
                    hash = (hash ^ c) * 16777619;
                }
                hash = (hash ^ 0xFF) * 16777619;
            }
            for (ushort i = 0; i < blocks.Count; i++)
            {
                Mix(blocks[i].Key);
            }
            for (byte z = 0; z < data.Zombies.Count; z++)
            {
                Mix(data.Zombies[z].id);
            }
            if (data.Items != null)
            {
                for (ushort i = 0; i < data.Items.Count; i++)
                {
                    Mix(data.Items[i].id);
                }
            }
            return hash;
        }

        public static void WriteWelcome(ByteWriter w, string mapName, BlockWorld blocks, GameSettings settings, uint fingerprint,
            int yourPlayer, long tick)
        {
            using (Begin(w, MessageType.Welcome))
            {
                w.WriteUShort(ProtocolVersion);
                w.WriteString(mapName);
                w.WriteUShort((ushort)blocks.Width);
                w.WriteUShort((ushort)blocks.Height);
                w.WriteUInt(fingerprint);
                w.WriteByte((byte)yourPlayer);
                w.WriteLong(tick);
                w.WriteByte((byte)settings.TickRate);
                w.WriteInt(settings.DaySeconds);
                w.WriteInt(settings.DuskSeconds);
                w.WriteInt(settings.NightSeconds);
                w.WriteInt(settings.DawnSeconds);
            }
        }

        /// <summary>A chunk for a late joiner: one message per plane that has something (the first one resets the chunk).</summary>
        public static void WriteChunk(ByteWriter w, BlockWorld blocks, int chunkX, int chunkY)
        {
            var planes = new List<KeyValuePair<int, BuildLayer>>();
            blocks.GetChunkPlanes(chunkX, chunkY, planes);
            for (int i = 0; i < planes.Count; i++)
            {
                using (Begin(w, MessageType.ChunkSnapshot))
                {
                    blocks.WriteChunkPlane(chunkX, chunkY, planes[i].Key, planes[i].Value, reset: i == 0, w);
                }
            }
        }

        /// <summary>Block deltas, split so no frame exceeds the ushort length.</summary>
        public static void WriteBlockDeltas(ByteWriter w, IReadOnlyList<BlockChange> changes)
        {
            const int perMessage = 4000;
            for (int start = 0; start < changes.Count; start += perMessage)
            {
                int count = Math.Min(perMessage, changes.Count - start);
                using (Begin(w, MessageType.BlockDeltas))
                {
                    w.WriteUShort((ushort)count);
                    for (int i = start; i < start + count; i++)
                    {
                        changes[i].Write(w);
                    }
                }
            }
        }

        /// <summary>All players: position (float, exact for reconciliation), level, health, stamina (0-255) and flags. 13 B each.</summary>
        public static void WritePlayerStates(ByteWriter w, uint tick, IReadOnlyList<PlayerState> players, float maxStamina)
        {
            using (Begin(w, MessageType.PlayerStates))
            {
                w.WriteUInt(tick);
                w.WriteByte((byte)players.Count);
                foreach (PlayerState p in players)
                {
                    w.WriteFloat(p.Position.X);
                    w.WriteFloat(p.Position.Y);
                    w.WriteByte((byte)(sbyte)p.Level);
                    w.WriteUShort((ushort)Math.Clamp(p.Health, 0, ushort.MaxValue));
                    w.WriteByte((byte)Math.Clamp((int)MathF.Round(p.Stamina / Math.Max(1f, maxStamina) * 255f), 0, 255));
                    byte flags = 0;
                    flags |= p.IsSprinting ? FlagSprinting : (byte)0;
                    flags |= p.IsDowned && !p.IsDead ? FlagDowned : (byte)0;
                    flags |= p.IsDead ? FlagDead : (byte)0;
                    w.WriteByte(flags);
                }
            }
        }

        public static int WriteZombieSnapshot(ByteWriter w, ZombieSimulation sim, uint tick, Float2 viewer, float radius, List<int> scratch, int viewerLevel = 0)
        {
            using (Begin(w, MessageType.ZombieSnapshot))
            {
                return ZombieSnapshotCodec.Write(sim, tick, viewer, radius, w, scratch, viewerLevel: viewerLevel);
            }
        }

        public static void WriteZombieEvents(ByteWriter w, IReadOnlyList<ZombieEvent> events)
        {
            using (Begin(w, MessageType.ZombieEvents))
            {
                w.WriteUShort((ushort)Math.Min(events.Count, ushort.MaxValue));
                for (int i = 0; i < events.Count && i < ushort.MaxValue; i++)
                {
                    ZombieEvent e = events[i];
                    w.WriteByte((byte)e.Type);
                    w.WriteUShort(e.ZombieId);
                    w.WriteFloat(e.Position.X);
                    w.WriteFloat(e.Position.Y);
                }
            }
        }

        public static void WriteSessionEvents(ByteWriter w, IReadOnlyList<SessionEvent> events)
        {
            using (Begin(w, MessageType.SessionEvents))
            {
                w.WriteUShort((ushort)Math.Min(events.Count, ushort.MaxValue));
                for (int i = 0; i < events.Count && i < ushort.MaxValue; i++)
                {
                    SessionEvent e = events[i];
                    w.WriteByte((byte)e.Type);
                    w.WriteByte(unchecked((byte)(sbyte)e.Player));
                    w.WriteInt(e.Value);
                    w.WriteByte((byte)e.Phase);
                    w.WriteByte((byte)e.Result);
                }
            }
        }

        public static void WriteClock(ByteWriter w, long tick)
        {
            using (Begin(w, MessageType.Clock))
            {
                w.WriteLong(tick);
            }
        }

        /// <summary>Bars quantised to whole percents: 2 bytes, only for their owner.</summary>
        public static void WriteVitals(ByteWriter w, byte food, byte water)
        {
            using (Begin(w, MessageType.Vitals))
            {
                w.WriteByte(food);
                w.WriteByte(water);
            }
        }

        /// <summary>A bar (0-100) as the byte <see cref="WriteVitals"/> sends.</summary>
        public static byte QuantizeBar(float value)
        {
            return (byte)Math.Max(0, Math.Min(100, (int)Math.Ceiling(value))); // ceil: a nearly empty bar is not shown as empty
        }

        public static void WriteInventory(ByteWriter w, Inventory inventory)
        {
            using (Begin(w, MessageType.Inventory))
            {
                inventory.Write(w);
            }
        }

        /// <summary>Contents of the container the player has open; <paramref name="contents"/> null = closed.</summary>
        public static void WriteContainer(ByteWriter w, Int2 cell, Inventory contents)
        {
            using (Begin(w, MessageType.ContainerContents))
            {
                bool open = contents != null;
                w.WriteByte(open ? (byte)1 : (byte)0);
                if (!open)
                {
                    return;
                }
                w.WriteUShort((ushort)cell.X);
                w.WriteUShort((ushort)cell.Y);
                w.WriteByte((byte)contents.BagSlots);
                contents.Write(w);
            }
        }

        public static void WriteGroundPiles(ByteWriter w, IReadOnlyList<ItemPile> piles)
        {
            using (Begin(w, MessageType.GroundPiles))
            {
                w.WriteUShort((ushort)Math.Min(piles.Count, ushort.MaxValue));
                for (int i = 0; i < piles.Count && i < ushort.MaxValue; i++)
                {
                    ItemPile pile = piles[i];
                    w.WriteUShort(pile.Id);
                    w.WriteFloat(pile.Position.X);
                    w.WriteFloat(pile.Position.Y);
                    int stacks = Math.Min(pile.Stacks.Count, 255);
                    w.WriteByte((byte)stacks);
                    for (int k = 0; k < stacks; k++)
                    {
                        w.WriteUShort(pile.Stacks[k].Item);
                        w.WriteUShort(pile.Stacks[k].Count);
                        w.WriteUShort(pile.Stacks[k].Durability);
                    }
                }
            }
        }

        public static void WriteContainerList(ByteWriter w, ContainerStore containers)
        {
            using (Begin(w, MessageType.ContainerList))
            {
                int count = containers?.Count ?? 0;
                w.WriteUShort((ushort)Math.Min(count, ushort.MaxValue));
                if (containers == null)
                {
                    return;
                }
                int written = 0;
                foreach (Int2 cell in containers.Positions)
                {
                    if (written++ >= ushort.MaxValue)
                    {
                        break;
                    }
                    w.WriteUShort((ushort)cell.X);
                    w.WriteUShort((ushort)cell.Y);
                }
            }
        }

        public static void WriteTeamProgress(ByteWriter w, GameSession session)
        {
            using (Begin(w, MessageType.TeamProgress))
            {
                var samples = new List<string>();
                if (session.Recipes != null)
                {
                    samples.AddRange(session.Recipes.UnlockedSamples);
                }
                w.WriteUShort((ushort)samples.Count);
                foreach (string s in samples)
                {
                    w.WriteString(s);
                }
                IReadOnlyList<int> diary = session.Diary?.Unlocked ?? (IReadOnlyList<int>)Array.Empty<int>();
                w.WriteUShort((ushort)diary.Count);
                foreach (int entry in diary)
                {
                    w.WriteString(session.Diary.Entries[entry].id);
                }
            }
        }

        // ---------------------------------------------------- client → host

        public static void WriteInput(ByteWriter w, uint tick, PlayerInput input)
        {
            using (Begin(w, MessageType.Input))
            {
                w.WriteUInt(tick);
                input.Write(w);
            }
        }

        public static void WriteIntent(ByteWriter w, PlayerIntent intent)
        {
            using (Begin(w, MessageType.Intent))
            {
                intent.Write(w);
            }
        }
    }
}
