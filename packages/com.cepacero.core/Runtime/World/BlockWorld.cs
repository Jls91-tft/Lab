using System;
using System.Collections.Generic;
using CepaCero.Util;

namespace CepaCero.World
{
    /// <summary>
    /// Grid of blocks split into chunks, with levels and layers (D-022). On the host it is the authoritative
    /// world; on clients it is a replica fed with <see cref="ApplyChange"/> and <see cref="ReadChunkPlane"/>.
    /// Every mutation is recorded so the host can ship it (<see cref="DrainChanges"/>) and views know what to redraw.
    ///
    /// A cell is addressed by (x, y, level z, layer). Level 0 is the ground, negative levels are underground and
    /// positive ones are upper floors. Layers are structure, floor, roof and object. The calls without z and layer
    /// work on the ground-level structure, which is all the legacy code (pathfinding, collisions) needs.
    /// </summary>
    public sealed class BlockWorld
    {
        public const int DefaultMinLevel = -3;
        public const int DefaultMaxLevel = 3;

        private readonly Chunk[] _chunks;
        private readonly List<BlockChange> _pending = new List<BlockChange>();

        public BlockWorld(int width, int height, BlockRegistry registry, int minLevel = DefaultMinLevel, int maxLevel = DefaultMaxLevel)
        {
            if (width <= 0 || height <= 0 || width > ushort.MaxValue || height > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(width), $"World size {width}x{height} out of range.");
            }
            if (minLevel > 0 || maxLevel < 0 || maxLevel - minLevel > 15)
            {
                throw new ArgumentOutOfRangeException(nameof(minLevel), $"Levels {minLevel}..{maxLevel} must include 0 and span at most 16.");
            }
            Width = width;
            Height = height;
            MinLevel = minLevel;
            MaxLevel = maxLevel;
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
            ChunksX = (width + Chunk.Size - 1) / Chunk.Size;
            ChunksY = (height + Chunk.Size - 1) / Chunk.Size;
            _chunks = new Chunk[ChunksX * ChunksY];
            for (int cy = 0; cy < ChunksY; cy++)
            {
                for (int cx = 0; cx < ChunksX; cx++)
                {
                    _chunks[cy * ChunksX + cx] = new Chunk(cx, cy, LevelCount, -minLevel);
                }
            }
        }

        public int Width { get; }

        public int Height { get; }

        public int ChunksX { get; }

        public int ChunksY { get; }

        /// <summary>Lowest level (a basement), 0 or less.</summary>
        public int MinLevel { get; }

        /// <summary>Highest level (an upper floor), 0 or more.</summary>
        public int MaxLevel { get; }

        public int LevelCount => MaxLevel - MinLevel + 1;

        public BlockRegistry Registry { get; }

        /// <summary>Increments on every change anywhere, including block health.</summary>
        public int Revision { get; private set; }

        /// <summary>
        /// Increments only when a ground-level structure changes type (placed, destroyed, door toggled): what
        /// pathfinding cares about. Health-only changes do not count, so a siege does not force a flow field rebuild
        /// on every hit.
        /// </summary>
        public int StructureRevision { get; private set; }

        /// <summary>
        /// Raised when a ground-level structure changes type (x, y, old type, new type), e.g. a chest
        /// destroyed by zombies spilling its contents. Not raised by the readers.
        /// </summary>
        public event Action<int, int, ushort, ushort> TypeChanged;

        /// <summary>Raised when any cell changes type: x, y, level, layer, old type, new type.</summary>
        public event Action<int, int, int, BuildLayer, ushort, ushort> CellChanged;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool InLevels(int z) => z >= MinLevel && z <= MaxLevel;

        public Chunk GetChunk(int chunkX, int chunkY) => _chunks[chunkY * ChunksX + chunkX];

        // ------------------------------------------------------------ ground-level structure (the legacy view)
        public ushort GetTypeId(int x, int y) => GetTypeId(x, y, 0, BuildLayer.Structure);

        public BlockType GetBlock(int x, int y) => Registry[GetTypeId(x, y)];

        public ushort GetHealth(int x, int y) => GetHealth(x, y, 0, BuildLayer.Structure);

        public bool IsSolid(int x, int y) => !InBounds(x, y) || GetBlock(x, y).IsSolid;

        /// <summary>Unconditional write at full health (map loading, host commands). The type decides its own layer.</summary>
        public void SetBlock(int x, int y, ushort typeId)
        {
            CheckType(typeId);
            SetBlock(x, y, 0, LayerOf(typeId), typeId);
        }

        public PlaceResult TryPlace(int x, int y, ushort typeId, GroundLayer ground = null) => TryPlace(x, y, 0, typeId, 0, ground);

        public DamageResult Damage(int x, int y, int amount) => Damage(x, y, 0, BuildLayer.Structure, amount);

        public bool Toggle(int x, int y) => Toggle(x, y, 0, BuildLayer.Structure);

        // ------------------------------------------------------------ any level and layer
        public ushort GetTypeId(int x, int y, int z, BuildLayer layer)
        {
            Locate(x, y, z, out Chunk chunk, out int zi, out int i);
            Chunk.Plane plane = chunk.GetPlane(zi, layer, false);
            return plane == null ? BlockRegistry.AirId : plane.Types[i];
        }

        public BlockType GetBlock(int x, int y, int z, BuildLayer layer) => Registry[GetTypeId(x, y, z, layer)];

        public ushort GetHealth(int x, int y, int z, BuildLayer layer)
        {
            Locate(x, y, z, out Chunk chunk, out int zi, out int i);
            Chunk.Plane plane = chunk.GetPlane(zi, layer, false);
            return plane == null ? (ushort)0 : plane.Health[i];
        }

        /// <summary>Colour and rotation of a cell (<see cref="BlockVariant"/>); 0 for air.</summary>
        public byte GetVariant(int x, int y, int z, BuildLayer layer)
        {
            Locate(x, y, z, out Chunk chunk, out int zi, out int i);
            Chunk.Plane plane = chunk.GetPlane(zi, layer, false);
            return plane?.Variant == null ? (byte)0 : plane.Variant[i];
        }

        /// <summary>Unconditional write at full health. A non-air block must belong to the given layer.</summary>
        public void SetBlock(int x, int y, int z, BuildLayer layer, ushort typeId, byte variant = 0)
        {
            CheckType(typeId);
            if (typeId != BlockRegistry.AirId && LayerOf(typeId) != layer)
            {
                throw new ArgumentException($"Block {Registry[typeId].Key} belongs to the {LayerOf(typeId)} layer, not {layer}.", nameof(layer));
            }
            Write(x, y, z, layer, typeId, Registry[typeId].MaxHealth, variant);
        }

        /// <summary>
        /// Host-side validation for a player's "place block" intent. Distance, inventory and "is a player standing
        /// there" are checked by the caller. The block's own layer must be free in that cell.
        /// </summary>
        public PlaceResult TryPlace(int x, int y, int z, ushort typeId, byte variant, GroundLayer ground = null)
        {
            if (!InBounds(x, y) || !InLevels(z))
            {
                return PlaceResult.OutOfBounds;
            }
            if (!Registry.IsValid(typeId) || typeId == BlockRegistry.AirId)
            {
                return PlaceResult.InvalidType;
            }
            BuildLayer layer = LayerOf(typeId);
            if (GetTypeId(x, y, z, layer) != BlockRegistry.AirId)
            {
                return PlaceResult.Occupied;
            }
            if (z == 0 && ground != null && !GroundLayer.IsBuildable(ground.Get(x, y)))
            {
                return PlaceResult.BlockedGround;
            }
            SetBlock(x, y, z, layer, typeId, BlockVariant.Sanitize(variant, Registry[typeId]));
            return PlaceResult.Placed;
        }

        /// <summary>Applies damage; a block reaching 0 health becomes air.</summary>
        public DamageResult Damage(int x, int y, int z, BuildLayer layer, int amount)
        {
            if (!InBounds(x, y) || !InLevels(z) || amount <= 0)
            {
                return DamageResult.None;
            }
            Locate(x, y, z, out Chunk chunk, out int zi, out int i);
            Chunk.Plane plane = chunk.GetPlane(zi, layer, false);
            if (plane == null)
            {
                return DamageResult.None;
            }
            BlockType type = Registry[plane.Types[i]];
            if (type.IsAir || type.IsIndestructible)
            {
                return DamageResult.None;
            }
            int health = plane.Health[i] - amount;
            byte variant = plane.Variant == null ? (byte)0 : plane.Variant[i];
            if (health <= 0)
            {
                Write(x, y, z, layer, BlockRegistry.AirId, 0, 0);
                return DamageResult.Destroyed;
            }
            Write(x, y, z, layer, type.Id, (ushort)health, variant);
            return DamageResult.Damaged;
        }

        /// <summary>Opens/closes a door keeping its health and variant. Returns false if the block is not toggleable.</summary>
        public bool Toggle(int x, int y, int z, BuildLayer layer)
        {
            if (!InBounds(x, y) || !InLevels(z))
            {
                return false;
            }
            BlockType type = GetBlock(x, y, z, layer);
            if (type.ToggleTo == 0)
            {
                return false;
            }
            Write(x, y, z, layer, type.ToggleTo, GetHealth(x, y, z, layer), GetVariant(x, y, z, layer));
            return true;
        }

        /// <summary>Client side: apply a change received from the host.</summary>
        public void ApplyChange(in BlockChange change)
        {
            if (!InBounds(change.X, change.Y) || !InLevels(change.Z) || !Registry.IsValid(change.Type))
            {
                throw new ArgumentOutOfRangeException(nameof(change), $"Invalid change {change}.");
            }
            Write(change.X, change.Y, change.Z, change.Layer, change.Type, change.Health, change.Variant);
        }

        /// <summary>Moves all recorded changes into <paramref name="into"/> and clears the queue.</summary>
        public void DrainChanges(List<BlockChange> into)
        {
            into.AddRange(_pending);
            _pending.Clear();
        }

        public int PendingChangeCount => _pending.Count;

        // ------------------------------------------------------------ snapshots (network and saves)
        /// <summary>
        /// The planes of a chunk that have something in them, ground-level structure first (always listed: it is what a
        /// snapshot resets). Each is one message on the network.
        /// </summary>
        public void GetChunkPlanes(int chunkX, int chunkY, List<KeyValuePair<int, BuildLayer>> into)
        {
            Chunk chunk = GetChunk(chunkX, chunkY);
            into.Add(new KeyValuePair<int, BuildLayer>(0, BuildLayer.Structure));
            for (int zi = 0; zi < LevelCount; zi++)
            {
                for (int l = 0; l < Chunk.Layers; l++)
                {
                    var layer = (BuildLayer)l;
                    if (zi + MinLevel == 0 && layer == BuildLayer.Structure)
                    {
                        continue;
                    }
                    if (chunk.HasContent(zi, layer))
                    {
                        into.Add(new KeyValuePair<int, BuildLayer>(zi + MinLevel, layer));
                    }
                }
            }
        }

        /// <summary>
        /// One plane of a chunk for late joiners. Format: chunkX, chunkY (ushort), reset (byte: 1 = first plane, clears the chunk
        /// first), level (sbyte), layer (byte), then run-length records [count, type, health (ushort each), variant (byte)]
        /// until 1024 cells. An empty plane is 9 bytes; the busiest ones a few kilobytes.
        /// </summary>
        public void WriteChunkPlane(int chunkX, int chunkY, int z, BuildLayer layer, bool reset, ByteWriter writer)
        {
            writer.WriteUShort((ushort)chunkX);
            writer.WriteUShort((ushort)chunkY);
            writer.WriteByte(reset ? (byte)1 : (byte)0);
            WritePlane(GetChunk(chunkX, chunkY), z, layer, writer);
        }

        /// <summary>
        /// Applies a plane from <see cref="WriteChunkPlane"/>. Does not record changes. <paramref name="remap"/> translates stored
        /// block ids to this registry's ids (saves made with an older registry); null means "same ids".
        /// </summary>
        public void ReadChunkPlane(ByteReader reader, ushort[] remap = null)
        {
            int chunkX = reader.ReadUShort();
            int chunkY = reader.ReadUShort();
            bool reset = reader.ReadByte() != 0;
            Chunk chunk = ChunkAt(chunkX, chunkY);
            if (reset)
            {
                ClearChunk(chunk);
            }
            ReadPlane(chunk, reader, remap);
            Touch(chunk);
        }

        /// <summary>The whole chunk in one stream (saves): chunkX, chunkY, plane count (byte), then each plane.</summary>
        public void WriteChunk(int chunkX, int chunkY, ByteWriter writer)
        {
            var planes = new List<KeyValuePair<int, BuildLayer>>();
            GetChunkPlanes(chunkX, chunkY, planes);
            Chunk chunk = GetChunk(chunkX, chunkY);
            writer.WriteUShort((ushort)chunkX);
            writer.WriteUShort((ushort)chunkY);
            writer.WriteByte((byte)planes.Count);
            foreach (KeyValuePair<int, BuildLayer> p in planes)
            {
                WritePlane(chunk, p.Key, p.Value, writer);
            }
        }

        /// <summary>Replaces a whole chunk from <see cref="WriteChunk"/> data. Does not record changes.</summary>
        public void ReadChunk(ByteReader reader, ushort[] remap = null)
        {
            Chunk chunk = ChunkAt(reader.ReadUShort(), reader.ReadUShort());
            int count = reader.ReadByte();
            ClearChunk(chunk);
            for (int p = 0; p < count; p++)
            {
                ReadPlane(chunk, reader, remap);
            }
            Touch(chunk);
        }

        /// <summary>
        /// Chunk of a save made before levels and layers (saves up to v4): chunkX, chunkY, then runs [count, type, health]
        /// of the ground-level structure. Everything else in the chunk is cleared.
        /// </summary>
        public void ReadChunkLegacy(ByteReader reader, ushort[] remap = null)
        {
            Chunk chunk = ChunkAt(reader.ReadUShort(), reader.ReadUShort());
            ClearChunk(chunk);
            int i = 0;
            while (i < Chunk.CellCount)
            {
                int run = reader.ReadUShort();
                ushort type = Remap(reader.ReadUShort(), remap);
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
            Touch(chunk);
        }

        // ------------------------------------------------------------ internals
        private void WritePlane(Chunk chunk, int z, BuildLayer layer, ByteWriter writer)
        {
            if (!InLevels(z))
            {
                throw new ArgumentOutOfRangeException(nameof(z), $"Level {z} outside {MinLevel}..{MaxLevel}.");
            }
            writer.WriteByte((byte)(sbyte)z);
            writer.WriteByte((byte)layer);
            Chunk.Plane plane = chunk.GetPlane(z - MinLevel, layer, false);
            if (plane == null)
            {
                writer.WriteUShort(Chunk.CellCount);
                writer.WriteUShort(0);
                writer.WriteUShort(0);
                writer.WriteByte(0);
                return;
            }
            int i = 0;
            while (i < Chunk.CellCount)
            {
                ushort type = plane.Types[i];
                ushort health = plane.Health[i];
                byte variant = plane.Variant == null ? (byte)0 : plane.Variant[i];
                int run = 1;
                while (i + run < Chunk.CellCount && plane.Types[i + run] == type && plane.Health[i + run] == health
                       && (plane.Variant == null ? (byte)0 : plane.Variant[i + run]) == variant)
                {
                    run++;
                }
                writer.WriteUShort((ushort)run);
                writer.WriteUShort(type);
                writer.WriteUShort(health);
                writer.WriteByte(variant);
                i += run;
            }
        }

        private void ReadPlane(Chunk chunk, ByteReader reader, ushort[] remap)
        {
            int z = (sbyte)reader.ReadByte();
            int layerByte = reader.ReadByte();
            if (!InLevels(z) || layerByte >= Chunk.Layers)
            {
                throw new FormatException($"Plane at level {z}, layer {layerByte} is outside this world.");
            }
            var layer = (BuildLayer)layerByte;
            Chunk.Plane plane = chunk.GetPlane(z - MinLevel, layer, true);
            int i = 0;
            while (i < Chunk.CellCount)
            {
                int run = reader.ReadUShort();
                ushort type = Remap(reader.ReadUShort(), remap);
                ushort health = reader.ReadUShort();
                byte variant = reader.ReadByte();
                if (run == 0 || i + run > Chunk.CellCount || !Registry.IsValid(type))
                {
                    throw new FormatException($"Corrupt chunk data at cell {i}.");
                }
                if (variant != 0 && plane.Variant == null)
                {
                    plane.Variant = new byte[Chunk.CellCount];
                }
                for (int k = 0; k < run; k++)
                {
                    plane.Types[i + k] = type;
                    plane.Health[i + k] = health;
                    if (plane.Variant != null)
                    {
                        plane.Variant[i + k] = variant;
                    }
                }
                i += run;
            }
        }

        private static ushort Remap(ushort type, ushort[] remap) => remap == null ? type : type < remap.Length ? remap[type] : ushort.MaxValue;

        private Chunk ChunkAt(int chunkX, int chunkY)
        {
            if (chunkX >= ChunksX || chunkY >= ChunksY)
            {
                throw new FormatException($"Chunk ({chunkX},{chunkY}) outside world of {ChunksX}x{ChunksY} chunks.");
            }
            return GetChunk(chunkX, chunkY);
        }

        private void ClearChunk(Chunk chunk)
        {
            for (int zi = 0; zi < LevelCount; zi++)
            {
                for (int l = 0; l < Chunk.Layers; l++)
                {
                    chunk.ClearPlane(zi, (BuildLayer)l);
                }
            }
        }

        private void Touch(Chunk chunk)
        {
            chunk.Version++;
            Revision++;
            StructureRevision++;
        }

        private BuildLayer LayerOf(ushort typeId) => typeId == BlockRegistry.AirId ? BuildLayer.Structure : Registry[typeId].Layer;

        private void CheckType(ushort typeId)
        {
            if (!Registry.IsValid(typeId))
            {
                throw new ArgumentOutOfRangeException(nameof(typeId), $"Unknown block id {typeId}.");
            }
        }

        private void Write(int x, int y, int z, BuildLayer layer, ushort typeId, ushort health, byte variant)
        {
            Locate(x, y, z, out Chunk chunk, out int zi, out int i);
            Chunk.Plane plane = chunk.GetPlane(zi, layer, typeId != BlockRegistry.AirId);
            if (plane == null)
            {
                return;                                          // air into a plane that does not exist: already air
            }
            if (typeId == BlockRegistry.AirId)
            {
                variant = 0;
            }
            byte oldVariant = plane.Variant == null ? (byte)0 : plane.Variant[i];
            if (plane.Types[i] == typeId && plane.Health[i] == health && oldVariant == variant)
            {
                return;
            }
            ushort previous = plane.Types[i];
            plane.Types[i] = typeId;
            plane.Health[i] = health;
            if (variant != 0 && plane.Variant == null)
            {
                plane.Variant = new byte[Chunk.CellCount];
            }
            if (plane.Variant != null)
            {
                plane.Variant[i] = variant;
            }
            chunk.Version++;
            Revision++;
            _pending.Add(new BlockChange((ushort)x, (ushort)y, typeId, health, (sbyte)z, layer, variant));
            if (previous != typeId)
            {
                CellChanged?.Invoke(x, y, z, layer, previous, typeId);
                if (z == 0 && layer == BuildLayer.Structure)
                {
                    StructureRevision++;
                    TypeChanged?.Invoke(x, y, previous, typeId);
                }
            }
        }

        private void Locate(int x, int y, int z, out Chunk chunk, out int levelIndex, out int index)
        {
            if (!InBounds(x, y))
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"Tile ({x},{y}) outside world {Width}x{Height}.");
            }
            if (!InLevels(z))
            {
                throw new ArgumentOutOfRangeException(nameof(z), $"Level {z} outside {MinLevel}..{MaxLevel}.");
            }
            chunk = _chunks[(y / Chunk.Size) * ChunksX + (x / Chunk.Size)];
            levelIndex = z - MinLevel;
            index = Chunk.LocalIndex(x % Chunk.Size, y % Chunk.Size);
        }
    }
}
