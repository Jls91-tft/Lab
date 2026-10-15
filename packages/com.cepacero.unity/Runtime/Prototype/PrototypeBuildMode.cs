using System;
using System.Collections.Generic;
using CepaCero.Items;
using CepaCero.Net;
using CepaCero.Player;
using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Building mode, step 1 of D-020 (no core changes): pick a piece, see a ghost that
    /// says whether and why it can be placed, drag a line (Shift: a rectangle outline),
    /// and the cells join a build queue that sends one placement per action cooldown,
    /// the same rate the host accepts. The host still validates every placement.
    /// </summary>
    public sealed class PrototypeBuildMode
    {
        public static readonly string[] Pieces =
            { "wood_wall", "fence", "door", "metal_wall", "concrete_wall", "spike_trap", "chest", "workbench" };

        public enum Verdict : byte
        {
            Ok,
            OutOfReach,
            Occupied,
            NotBuildable,
            MissingMaterials,
            Locked,
        }

        public struct Pending
        {
            public Int2 Cell;
            public string Key;
        }

        private readonly List<Pending> _queue = new List<Pending>();
        private readonly List<Int2> _preview = new List<Int2>();
        private readonly List<Verdict> _verdicts = new List<Verdict>();
        private float _nextSend;

        public bool Active { get; private set; }

        public int Selected { get; private set; }

        public string SelectedKey => Pieces[Selected];

        /// <summary>Start of the drag in progress; null when not dragging.</summary>
        public Int2? DragStart { get; private set; }

        public IReadOnlyList<Pending> Queue => _queue;

        /// <summary>Cells under the cursor or the drag, with their verdicts (same order).</summary>
        public IReadOnlyList<Int2> Preview => _preview;

        public IReadOnlyList<Verdict> Verdicts => _verdicts;

        public void Toggle()
        {
            Active = !Active;
            DragStart = null;
            if (!Active)
            {
                _queue.Clear(); // leaving the mode cancels the plan: no surprises later
            }
        }

        public void Select(int index)
        {
            if (index >= 0 && index < Pieces.Length)
            {
                Selected = index;
            }
        }

        public void Scroll(int steps)
        {
            Selected = ((Selected + steps) % Pieces.Length + Pieces.Length) % Pieces.Length;
        }

        public void BeginDrag(Int2 cell) => DragStart = cell;

        /// <summary>Right click or Escape: first cancels the drag, then the whole queue.</summary>
        public void Cancel()
        {
            if (DragStart.HasValue)
            {
                DragStart = null;
            }
            else
            {
                _queue.Clear();
            }
        }

        /// <summary>Drops the dragged cells that can be built (except reach: you can walk closer) into the queue.</summary>
        public int EndDrag()
        {
            int added = 0;
            for (int i = 0; i < _preview.Count; i++)
            {
                Verdict v = _verdicts[i];
                if ((v == Verdict.Ok || v == Verdict.OutOfReach) && !IsQueued(_preview[i]))
                {
                    _queue.Add(new Pending { Cell = _preview[i], Key = SelectedKey });
                    added++;
                }
            }
            DragStart = null;
            return added;
        }

        /// <summary>Recomputes the ghost: one cell, a line, or a rectangle outline with Shift.</summary>
        public void UpdatePreview(Int2 cursor, bool rectangle, in Context ctx)
        {
            _preview.Clear();
            _verdicts.Clear();
            if (!Active)
            {
                return;
            }
            if (DragStart.HasValue)
            {
                Shape(DragStart.Value, cursor, rectangle, _preview);
            }
            else
            {
                _preview.Add(cursor);
            }
            int affordable = Affordable(SelectedKey, ctx) - QueuedCost(SelectedKey);
            foreach (Int2 cell in _preview)
            {
                Verdict v = Check(cell, SelectedKey, ctx);
                if (v == Verdict.Ok || v == Verdict.OutOfReach)
                {
                    if (affordable <= 0)
                    {
                        v = ctx.Recipes != null && IsLocked(SelectedKey, ctx) ? Verdict.Locked : Verdict.MissingMaterials;
                    }
                    affordable--;
                }
                _verdicts.Add(v);
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
                Pending p = _queue[i];
                Verdict v = Check(p.Cell, p.Key, ctx);
                if (v == Verdict.OutOfReach)
                {
                    continue; // waits until you walk closer
                }
                _queue.RemoveAt(i);
                if (v != Verdict.Ok || Affordable(p.Key, ctx) <= 0)
                {
                    return false; // gone for good (built over, or no material): drop it
                }
                ushort type = ctx.Blocks.Registry.Get(p.Key).Id;
                intent = PlayerIntent.Place(p.Cell.X, p.Cell.Y, type);
                _nextSend = time + ctx.Settings.ActionCooldown + 0.02f;
                return true;
            }
            return false;
        }

        /// <summary>What the client knows; mirrors HostIntentRules.Place so the ghost tells the truth.</summary>
        public readonly struct Context
        {
            public Context(BlockWorld blocks, GroundLayer ground, PlayerSettings settings, Float2 me,
                IReadOnlyList<PlayerState> players, Inventory inventory, RecipeBook recipes)
            {
                Blocks = blocks;
                Ground = ground;
                Settings = settings;
                Me = me;
                Players = players;
                Inventory = inventory;
                Recipes = recipes;
            }

            public BlockWorld Blocks { get; }
            public GroundLayer Ground { get; }
            public PlayerSettings Settings { get; }
            public Float2 Me { get; }
            public IReadOnlyList<PlayerState> Players { get; }
            public Inventory Inventory { get; }
            public RecipeBook Recipes { get; }
        }

        public static Verdict Check(Int2 cell, string key, in Context ctx)
        {
            if (!ctx.Blocks.InBounds(cell.X, cell.Y))
            {
                return Verdict.NotBuildable;
            }
            if (!ctx.Blocks.GetBlock(cell.X, cell.Y).IsAir)
            {
                return Verdict.Occupied;
            }
            if (!GroundLayer.IsBuildable(ctx.Ground.Get(cell.X, cell.Y)))
            {
                return Verdict.NotBuildable;
            }
            foreach (PlayerState p in ctx.Players)
            {
                if (!p.IsDead && (int)Math.Floor(p.Position.X) == cell.X && (int)Math.Floor(p.Position.Y) == cell.Y)
                {
                    return Verdict.Occupied;
                }
            }
            Float2 toCell = new Float2(cell.X + 0.5f, cell.Y + 0.5f) - ctx.Me;
            return toCell.LengthSquared <= ctx.Settings.Reach * ctx.Settings.Reach ? Verdict.Ok : Verdict.OutOfReach;
        }

        /// <summary>How many of this piece the inventory pays for (int.MaxValue when building is free).</summary>
        public static int Affordable(string key, in Context ctx)
        {
            RecipeDefinition r = ctx.Recipes?.BuildRecipe(key);
            if (r == null || ctx.Inventory == null)
            {
                return int.MaxValue;
            }
            if (!ctx.Recipes.IsUnlocked(r))
            {
                return 0;
            }
            int n = int.MaxValue;
            foreach (Ingredient ing in r.ingredients)
            {
                int have = ctx.Inventory.Catalog.TryGetIndex(ing.item, out ushort item) ? ctx.Inventory.Count(item) : 0;
                n = Math.Min(n, ing.count > 0 ? have / ing.count : int.MaxValue);
            }
            return n;
        }

        public static bool IsLocked(string key, in Context ctx)
        {
            RecipeDefinition r = ctx.Recipes?.BuildRecipe(key);
            return r != null && !ctx.Recipes.IsUnlocked(r);
        }

        /// <summary>"4 Madera · 2 Clavos" for the bar, or "" when free.</summary>
        public static string CostText(string key, RecipeBook recipes, ItemCatalog items)
        {
            RecipeDefinition r = recipes?.BuildRecipe(key);
            if (r == null || items == null)
            {
                return "";
            }
            var parts = new List<string>();
            foreach (Ingredient ing in r.ingredients)
            {
                parts.Add($"{ing.count} {(items.TryGetIndex(ing.item, out ushort index) ? items[index].name : ing.item)}");
            }
            return string.Join(" · ", parts);
        }

        public static string Name(string key)
        {
            switch (key)
            {
                case "wood_wall": return "Muro de madera";
                case "fence": return "Valla";
                case "door": return "Puerta";
                case "metal_wall": return "Muro de chapa";
                case "concrete_wall": return "Muro de hormigón";
                case "spike_trap": return "Trampa de pinchos";
                case "chest": return "Cofre";
                case "workbench": return "Banco de trabajo";
                default: return key;
            }
        }

        public static string Explain(Verdict v)
        {
            switch (v)
            {
                case Verdict.OutOfReach: return "Demasiado lejos: se construirá cuando te acerques";
                case Verdict.Occupied: return "Ocupado";
                case Verdict.NotBuildable: return "Aquí no se puede construir";
                case Verdict.MissingMaterials: return "Te faltan materiales";
                case Verdict.Locked: return "Receta bloqueada: analiza la muestra";
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

        private bool IsQueued(Int2 cell)
        {
            foreach (Pending p in _queue)
            {
                if (p.Cell.X == cell.X && p.Cell.Y == cell.Y)
                {
                    return true;
                }
            }
            return false;
        }

        private int QueuedCost(string key)
        {
            int n = 0;
            foreach (Pending p in _queue)
            {
                if (p.Key == key)
                {
                    n++;
                }
            }
            return n;
        }
    }
}
