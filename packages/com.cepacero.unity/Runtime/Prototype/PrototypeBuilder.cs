using System;
using System.Collections.Generic;
using CepaCero.Building;
using CepaCero.Items;
using CepaCero.Net;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Building with the hand (D-022): there is no building mode. When the item in the hand is a material, the cursor
    /// shows a ghost of the selected piece and a click, a drag (line), Shift + drag (outline) or Ctrl + drag (room) puts
    /// the cells in a queue that sends one placement per action cooldown, the rate the host accepts. The host still
    /// validates every placement; the ghost only tells the truth about what the host would say.
    /// </summary>
    public sealed class PrototypeBuilder
    {
        public enum Verdict : byte
        {
            Ok,

            /// <summary>Another piece of the same layer is there and gets replaced (or repainted).</summary>
            Replace,
            OutOfReach,
            Occupied,
            NotBuildable,
            MissingMaterials,
        }

        public enum DragKind : byte
        {
            Line,
            Outline,
            Room,
        }

        public struct Placement
        {
            public Int2 Cell;
            public ushort Type;
            public byte Variant;
            public Verdict Verdict;
        }

        private readonly BuildPalette _palette;
        private readonly List<Placement> _queue = new List<Placement>();
        private readonly List<Placement> _preview = new List<Placement>();
        private readonly List<Int2> _cells = new List<Int2>();
        private readonly List<Int2> _selection = new List<Int2>();
        private readonly List<PlannedPiece> _planned = new List<PlannedPiece>();
        private readonly List<RoomCell> _room = new List<RoomCell>();
        private readonly Dictionary<string, int> _needed = new Dictionary<string, int>();
        private float _nextSend;

        public PrototypeBuilder(BuildPalette palette)
        {
            _palette = palette ?? throw new ArgumentNullException(nameof(palette));
        }

        public BuildCursor Cursor { get; } = new BuildCursor();

        public BuildPalette Palette => _palette;

        /// <summary>The item in the hand builds something.</summary>
        public bool Active { get; private set; }

        public string HeldItem { get; private set; }

        public BuildPiece? Piece { get; private set; }

        public Int2? DragStart { get; private set; }

        /// <summary>The last copied blueprint (Ctrl+C, or loaded from a file); null before the first copy.</summary>
        public Blueprint Clipboard { get; set; }

        /// <summary>A pasted plan follows the cursor until the player confirms with clicks or cancels.</summary>
        public bool Pasting { get; private set; }

        /// <summary>Alt + drag: corner of the selection being drawn.</summary>
        public Int2? SelectStart { get; private set; }

        /// <summary>Outline of the selection being drawn (for the ghost view).</summary>
        public IReadOnlyList<Int2> SelectionCells => _selection;

        public DragKind Kind { get; private set; }

        public IReadOnlyList<Placement> Queue => _queue;

        public IReadOnlyList<Placement> Preview => _preview;

        /// <summary>Call every frame with the hand: updates what is being built and drops the plan when the hand changes.</summary>
        public void SetHand(ItemDefinition held)
        {
            string id = held?.id;
            if (id != HeldItem)
            {
                HeldItem = id;
                DragStart = null;
                _queue.Clear();
            }
            Active = id != null && _palette.Builds(id);
            Piece = Active ? Cursor.Current(_palette, id) : null;
        }

        public void CycleShape(int step)
        {
            if (Active)
            {
                Cursor.CycleShape(_palette, HeldItem, step);
                Piece = Cursor.Current(_palette, HeldItem);
            }
        }

        public void BeginDrag(Int2 cell, DragKind kind)
        {
            DragStart = cell;
            Kind = kind;
        }

        public void BeginSelect(Int2 cell) => SelectStart = cell;

        /// <summary>Copies what was built inside the selection into the clipboard. Returns the number of pieces (0: nothing there).</summary>
        public int EndSelect(Int2 cell, BlockWorld world)
        {
            if (!SelectStart.HasValue)
            {
                return 0;
            }
            Int2 a = SelectStart.Value;
            SelectStart = null;
            _selection.Clear();
            Blueprint plan = Blueprint.Capture(world, a.X, a.Y, cell.X, cell.Y);
            if (plan.IsEmpty)
            {
                return 0;
            }
            Clipboard = plan;
            return plan.Pieces.Count;
        }

        public bool StartPaste()
        {
            Pasting = Clipboard != null && !Clipboard.IsEmpty;
            return Pasting;
        }

        public void RotatePaste()
        {
            if (Clipboard != null)
            {
                Clipboard = Clipboard.RotatedClockwise();
            }
        }

        /// <summary>Queues every piece of the pasted plan that can go there; the ghost stays for another copy.</summary>
        public int ConfirmPaste()
        {
            int added = 0;
            foreach (Placement p in _preview)
            {
                if ((p.Verdict == Verdict.Ok || p.Verdict == Verdict.Replace || p.Verdict == Verdict.OutOfReach) && !IsQueued(p.Cell, p.Type))
                {
                    _queue.Add(p);
                    added++;
                }
            }
            return added;
        }

        /// <summary>Right click or Escape: first cancels a paste or a drag, then the whole queue.</summary>
        public void Cancel()
        {
            if (Pasting)
            {
                Pasting = false;
            }
            else if (SelectStart.HasValue)
            {
                SelectStart = null;
                _selection.Clear();
            }
            else if (DragStart.HasValue)
            {
                DragStart = null;
            }
            else
            {
                _queue.Clear();
            }
        }

        /// <summary>Moves what the drag showed into the queue (cells out of reach wait until you walk closer).</summary>
        public int EndDrag()
        {
            int added = 0;
            foreach (Placement p in _preview)
            {
                if ((p.Verdict == Verdict.Ok || p.Verdict == Verdict.Replace || p.Verdict == Verdict.OutOfReach) && !IsQueued(p.Cell, p.Type))
                {
                    _queue.Add(p);
                    added++;
                }
            }
            DragStart = null;
            return added;
        }

        /// <summary>Recomputes the ghost: the piece under the cursor, a line, an outline or a whole room.</summary>
        public void UpdatePreview(Int2 cursor, in Context ctx)
        {
            _preview.Clear();
            _selection.Clear();
            if (SelectStart.HasValue)
            {
                Shape(SelectStart.Value, cursor, true, _selection);
            }
            if (Pasting && Clipboard != null)
            {
                _needed.Clear();
                foreach (Placement q in _queue)
                {
                    Tally(ctx.Blocks.Registry[q.Type]);
                }
                _planned.Clear();
                Clipboard.PlanAt(cursor.X - Clipboard.Width / 2, cursor.Y - Clipboard.Height / 2, ctx.Blocks.Registry, _planned, out _);
                foreach (PlannedPiece pp in _planned)
                {
                    Add(new Int2(pp.X, pp.Y), pp.Type, pp.Variant, ctx);
                }
                return;
            }
            if (!Active || !Piece.HasValue)
            {
                return;
            }
            string material = _palette.MaterialOf(HeldItem);
            BuildPiece piece = Piece.Value;
            ushort main = ctx.Blocks.Registry.Get(piece.Key).Id;
            byte variant = Cursor.Variant;
            _cells.Clear();
            _room.Clear();
            if (!DragStart.HasValue)
            {
                _cells.Add(cursor);
            }
            else if (Kind == DragKind.Room)
            {
                RoomPlanner.DoorSide side = RoomPlanner.SideFacing(DragStart.Value, cursor, ctx.Me);
                RoomPlanner.Plan(DragStart.Value, cursor, side, true, false, _room);
            }
            else
            {
                Shape(DragStart.Value, cursor, Kind == DragKind.Outline, _cells);
            }

            _needed.Clear();
            foreach (Placement q in _queue)
            {
                Tally(ctx.Blocks.Registry[q.Type]);
            }
            foreach (Int2 cell in _cells)
            {
                Add(cell, main, variant, ctx);
            }
            foreach (RoomCell rc in _room)
            {
                ushort type = main;
                if (rc.Role == RoomRole.Door || rc.Role == RoomRole.Floor)
                {
                    string key = (rc.Role == RoomRole.Door ? "door_" : "floor_") + material;
                    if (material == null || !ctx.Blocks.Registry.TryGet(key, out BlockType found))
                    {
                        continue;                       // this material has no door or floor: the room stays open there
                    }
                    type = found.Id;
                }
                Add(rc.Cell, type, variant, ctx);
            }
        }

        /// <summary>Sends the next queued placement when the cooldown allows. Returns the intent, if any.</summary>
        public bool TryNext(float time, in Context ctx, out PlayerIntent intent)
        {
            intent = default;
            if (time < _nextSend || _queue.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < _queue.Count; i++)
            {
                Placement p = _queue[i];
                Verdict v = Check(p.Cell, p.Type, p.Variant, ctx);
                if (v == Verdict.OutOfReach)
                {
                    continue;                           // waits until you walk closer
                }
                _queue.RemoveAt(i);
                if (v != Verdict.Ok && v != Verdict.Replace)
                {
                    return false;                       // gone for good (built over): drop it
                }
                intent = PlayerIntent.Place(p.Cell.X, p.Cell.Y, p.Type, p.Variant);
                _nextSend = time + ctx.Settings.ActionCooldown + 0.02f;
                return true;
            }
            return false;
        }

        /// <summary>What the client knows; mirrors HostIntentRules.Place so the ghost tells the truth.</summary>
        public readonly struct Context
        {
            public Context(BlockWorld blocks, GroundLayer ground, PlayerSettings settings, Float2 me,
                IReadOnlyList<PlayerState> players, Inventory inventory, BuildCostMode costMode)
            {
                Blocks = blocks;
                Ground = ground;
                Settings = settings;
                Me = me;
                Players = players;
                Inventory = inventory;
                CostMode = costMode;
            }

            public BlockWorld Blocks { get; }
            public GroundLayer Ground { get; }
            public PlayerSettings Settings { get; }
            public Float2 Me { get; }
            public IReadOnlyList<PlayerState> Players { get; }
            public Inventory Inventory { get; }
            public BuildCostMode CostMode { get; }
        }

        public Verdict Check(Int2 cell, ushort typeId, byte variant, in Context ctx)
        {
            if (!ctx.Blocks.InBounds(cell.X, cell.Y))
            {
                return Verdict.NotBuildable;
            }
            BlockType placing = ctx.Blocks.Registry[typeId];
            variant = BlockVariant.Sanitize(variant, placing);
            ushort existing = ctx.Blocks.GetTypeId(cell.X, cell.Y, 0, placing.Layer);
            BlockType old = existing == BlockRegistry.AirId ? null : ctx.Blocks.Registry[existing];
            if (old != null && (old.Build == null || (existing == typeId && ctx.Blocks.GetVariant(cell.X, cell.Y, 0, placing.Layer) == variant)))
            {
                return Verdict.Occupied;
            }
            if (!GroundLayer.IsBuildable(ctx.Ground.Get(cell.X, cell.Y)))
            {
                return Verdict.NotBuildable;
            }
            if (placing.IsSolid && (old == null || !old.IsSolid))
            {
                foreach (PlayerState p in ctx.Players)
                {
                    if (!p.IsDead && TileCollision.CircleTouchesCell(p.Position, ctx.Settings.Radius, cell.X, cell.Y))
                    {
                        return Verdict.Occupied;
                    }
                }
            }
            Float2 toCell = new Float2(cell.X + 0.5f, cell.Y + 0.5f) - ctx.Me;
            if (toCell.LengthSquared > ctx.Settings.Reach * ctx.Settings.Reach)
            {
                return Verdict.OutOfReach;
            }
            return old != null ? Verdict.Replace : Verdict.Ok;
        }

        /// <summary>"4 Madera · 2 Clavos", or "" when it is free.</summary>
        public static string CostText(BlockType type, ItemCatalog items, BuildCostMode mode)
        {
            if (type.Build == null || mode == BuildCostMode.Free)
            {
                return "";
            }
            var parts = new List<string>();
            foreach (BlockCostItem c in type.Build.Cost)
            {
                parts.Add($"{Priced(c.Count, mode)} {(items.TryGetIndex(c.Item, out ushort index) ? items[index].name : c.Item)}");
            }
            return string.Join(" · ", parts);
        }

        public static string Explain(Verdict v)
        {
            switch (v)
            {
                case Verdict.OutOfReach: return "Demasiado lejos: se construirá cuando te acerques";
                case Verdict.Occupied: return "Ocupado";
                case Verdict.NotBuildable: return "Aquí no se puede construir";
                case Verdict.MissingMaterials: return "Te faltan materiales";
                default: return "";
            }
        }

        /// <summary>Axis-aligned line (the longer axis wins) or the outline of a rectangle.</summary>
        public static void Shape(Int2 a, Int2 b, bool rectangle, List<Int2> into)
        {
            if (rectangle)
            {
                int x0 = Math.Min(a.X, b.X), x1 = Math.Max(a.X, b.X), y0 = Math.Min(a.Y, b.Y), y1 = Math.Max(a.Y, b.Y);
                for (int x = x0; x <= x1; x++)
                {
                    into.Add(new Int2(x, y0));
                    if (y1 != y0)
                    {
                        into.Add(new Int2(x, y1));
                    }
                }
                for (int y = y0 + 1; y < y1; y++)
                {
                    into.Add(new Int2(x0, y));
                    if (x1 != x0)
                    {
                        into.Add(new Int2(x1, y));
                    }
                }
                return;
            }
            bool horizontal = Math.Abs(b.X - a.X) >= Math.Abs(b.Y - a.Y);
            int from = horizontal ? a.X : a.Y;
            int to = horizontal ? b.X : b.Y;
            int step = to >= from ? 1 : -1;
            for (int v = from; v != to + step; v += step)
            {
                into.Add(horizontal ? new Int2(v, a.Y) : new Int2(a.X, v));
            }
        }

        private void Add(Int2 cell, ushort type, byte variant, in Context ctx)
        {
            Verdict v = Check(cell, type, variant, ctx);
            if (v == Verdict.Ok || v == Verdict.Replace || v == Verdict.OutOfReach)
            {
                if (!CanAfford(ctx.Blocks.Registry[type], ctx))
                {
                    v = Verdict.MissingMaterials;
                }
                else
                {
                    Tally(ctx.Blocks.Registry[type]);
                }
            }
            _preview.Add(new Placement { Cell = cell, Type = type, Variant = variant, Verdict = v });
        }

        private void Tally(BlockType type)
        {
            if (type.Build == null)
            {
                return;
            }
            foreach (BlockCostItem c in type.Build.Cost)
            {
                _needed[c.Item] = (_needed.TryGetValue(c.Item, out int n) ? n : 0) + c.Count;
            }
        }

        private bool CanAfford(BlockType type, in Context ctx)
        {
            if (ctx.CostMode == BuildCostMode.Free || ctx.Inventory == null || type.Build == null)
            {
                return true;
            }
            foreach (BlockCostItem c in type.Build.Cost)
            {
                int have = ctx.Inventory.Catalog.TryGetIndex(c.Item, out ushort item) ? ctx.Inventory.Count(item) : 0;
                int reserved = _needed.TryGetValue(c.Item, out int n) ? Priced(n, ctx.CostMode) : 0;
                if (have - reserved < Priced(c.Count, ctx.CostMode))
                {
                    return false;
                }
            }
            return true;
        }

        private static int Priced(int count, BuildCostMode mode) => mode == BuildCostMode.Cheap ? (count + 1) / 2 : count;

        private bool IsQueued(Int2 cell, ushort type)
        {
            foreach (Placement p in _queue)
            {
                if (p.Cell == cell && p.Type == type)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
