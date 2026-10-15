using System;
using System.Collections.Generic;
using CepaCero.Util;

namespace CepaCero.World
{
    /// <summary>
    /// Grid of blocks split into chunks. On the host it is the authoritative
    /// world; on clients it is a replica fed with <see cref="ApplyChange"/> and
    /// <see cref="ReadChunk"/>. Every mutation is recorded so the host can ship
    /// it (<see cref="DrainChanges"/>) and views know what to redraw.
    /// </summary>
    public sealed class BlockWorld
    {
        private readonly Chunk[] _chunks;
        private readonly List<BlockChange> _pending = new List<BlockChange>();

        public BlockWorld(int width, int height, BlockRegistry registry)
        {
            if (width <= 0 || height <= 0 || width > ushort.MaxValue || height > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(width), $"World size {width}x{height} out of range.");
            }
            Width = width;
            Height = height;
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            ChunksX = (width + Chunk.Size - 1) / Chunk.Size;
            ChunksY = (height + Chunk.Size - 1) / Chunk.Size;
            _chunks = new Chunk[ChunksX * ChunksY];
            for (int cy = 0; cy < ChunksY; cy++)
            {
                for (int cx = 0; cx < ChunksX; cx++)
                {
                    _chunks[cy * ChunksX + cx] = new Chunk(cx, cy);
                }
            }
        }

        public int Width { get; }

        public int Height { get; }

        public int ChunksX { get; }

        public int ChunksY { get; }

        public BlockRegistry Registry { get; }

        /// <summary>Increments on every change anywhere. Cheap "did anything change?" check for caches (flow fields).</summary>
        public int Revision { get; private set; }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public Chunk GetChunk(int chunkX, int chunkY) => _chunks[chunkY * ChunksX + chunkX];

        public ushort GetTypeId(int x, int y)
        {
            Locate(x, y, out Chunk chunk, out int i);
            return chunk.Types[i];
        }

        public BlockType GetBlock(int x, int y) => Registry[GetTypeId(x, y)];

        public ushort GetHealth(int x, int y)
        {
            Locate(x, y, out Chunk chunk, out int i);
            return chunk.Health[i];
        }

        public bool IsSolid(int x, int y) => !InBounds(x, y) || GetBlock(x, y).IsSolid;

        /// <summary>Unconditional write at full health (map loading, host commands).</summary>
        public void SetBlock(int x, int y, ushort typeId)
        {
            if (!Registry.IsValid(typeId))
            {
                throw new ArgumentOutOfRangeException(nameof(typeId), $"Unknown block id {typeId}.");
            }
            Write(x, y, typeId, Registry[typeId].MaxHealth);
        }

        /// <summary>
        /// Host-side validation for a player's "place block" intent. Distance,
        /// inventory and "is a player standing there" are checked by the caller.
        /// </summary>
        public PlaceResult TryPlace(int x, int y, ushort typeId, GroundLayer ground = null)
        {
            if (!InBounds(x, y))
            {
                return PlaceResult.OutOfBounds;
            }
            if (!Registry.IsValid(typeId) || typeId == BlockRegistry.AirId)
            {
                return PlaceResult.InvalidType;
            }
            if (GetTypeId(x, y) != BlockRegistry.AirId)
            {
                return PlaceResult.Occupied;
            }
            if (ground != null && !GroundLayer.IsBuildable(ground.Get(x, y)))
            {
                return PlaceResult.BlockedGround;
            }
            SetBlock(x, y, typeId);
            return PlaceResult.Placed;
        }

        /// <summary>Applies damage; a block reaching 0 health becomes air.</summary>
        public DamageResult Damage(int x, int y, int amount)
        {
            if (!InBounds(x, y) || amount <= 0)
            {
                return DamageResult.None;
            }
            Locate(x, y, out Chunk chunk, out int i);
            BlockType type = Registry[chunk.Types[i]];
            if (type.IsAir || type.IsIndestructible)
            {
                return DamageResult.None;
            }
            int health = chunk.Health[i] - amount;
            if (health <= 0)
            {
                Write(x, y, BlockRegistry.AirId, 0);
                return DamageResult.Destroyed;
            }
            Write(x, y, type.Id, (ushort)health);
            return DamageResult.Damaged;
        }

        /// <summary>Opens/closes a door keeping its health. Returns false if the block is not toggleable.</summary>
        public bool Toggle(int x, int y)
        {
            if (!InBounds(x, y))
            {
                return false;
            }
            BlockType type = GetBlock(x, y);
            if (type.ToggleTo == 0)
            {
                return false;
            }
            Write(x, y, type.ToggleTo, GetHealth(x, y));
            return true;
        }

        /// <summary>Client side: apply a change received from the host.</summary>
        public void ApplyChange(in BlockChange change)
        {
            if (!InBounds(change.X, change.Y) || !Registry.IsValid(change.Type))
            {
                throw new ArgumentOutOfRangeException(nameof(change), $"Invalid change {change}.");
            }
            Write(change.X, change.Y, change.Type, change.Health);
        }

        /// <summary>Moves all recorded changes into <paramref name="into"/> and clears the queue.</summary>
        public void DrainChanges(List<BlockChange> into)
        {
            into.AddRange(_pending);
            _pending.Clear();
        }

        public int PendingChangeCount => _pending.Count;

        /// <summary>
        /// Full chunk for late joiners. Format: chunkX, chunkY (ushort), then
        /// run-length pairs [count, type, health] (ushort each) until 1024 cells.
        /// A chunk of air is 8 bytes; a busy city chunk usually a few hundred.
        /// </summary>
        public void WriteChunk(int chunkX, int chunkY, ByteWriter writer)
        {
            Chunk chunk = GetChunk(chunkX, chunkY);
            writer.WriteUShort((ushort)chunkX);
            writer.WriteUShort((ushort)chunkY);
            int i = 0;
            while (i < Chunk.CellCount)
            {
                ushort type = chunk.Types[i];
                ushort health = chunk.Health[i];
                int run = 1;
                while (i + run < Chunk.CellCount && chunk.Types[i + run] == type && chunk.Health[i + run] == health)
                {
                    run++;
                }
                writer.WriteUShort((ushort)run);
                writer.WriteUShort(type);
                writer.WriteUShort(health);
                i += run;
            }
        }

        /// <summary>Replaces a whole chunk from <see cref="WriteChunk"/> data. Does not record changes.</summary>
        public void ReadChunk(ByteReader reader)
        {
            int chunkX = reader.ReadUShort();
            int chunkY = reader.ReadUShort();
            if (chunkX >= ChunksX || chunkY >= ChunksY)
            {
                throw new FormatException($"Chunk ({chunkX},{chunkY}) outside world of {ChunksX}x{ChunksY} chunks.");
            }
            Chunk chunk = GetChunk(chunkX, chunkY);
            int i = 0;
            while (i < Chunk.CellCount)
            {
                int run = reader.ReadUShort();
                ushort type = reader.ReadUShort();
                ushort health = reader.ReadUShort();
                if (run == 0 || i + run > Chunk.CellCount || !Registry.IsValid(type))
                {
                    throw new FormatException($"Corrupt chunk data at cell {i}.");
                }
                for (int k = 0; k < run; k++)
                {
                    chunk.Types[i + k] = type;
                    chunk.Health[i + k] = health;
                }
                i += run;
            }
            chunk.Version++;
            Revision++;
        }

        private void Write(int x, int y, ushort typeId, ushort health)
        {
            Locate(x, y, out Chunk chunk, out int i);
            if (chunk.Types[i] == typeId && chunk.Health[i] == health)
            {
                return;
            }
            chunk.Types[i] = typeId;
            chunk.Health[i] = health;
            chunk.Version++;
            Revision++;
            _pending.Add(new BlockChange((ushort)x, (ushort)y, typeId, health));
        }

        private void Locate(int x, int y, out Chunk chunk, out int index)
        {
            if (!InBounds(x, y))
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"Tile ({x},{y}) outside world {Width}x{Height}.");
            }
            chunk = _chunks[(y / Chunk.Size) * ChunksX + (x / Chunk.Size)];
            index = Chunk.LocalIndex(x % Chunk.Size, y % Chunk.Size);
        }
    }
}
