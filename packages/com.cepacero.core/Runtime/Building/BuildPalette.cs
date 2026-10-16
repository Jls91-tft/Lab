using System;
using System.Collections.Generic;
using CepaCero.Items;

namespace CepaCero.Building
{
    /// <summary>
    /// What each item can build (D-022). The item that stands for a material is its least shared
    /// ingredient (nails and concrete mix serve several materials; the plank, the brick or the rebar do not),
    /// so holding that item in the hand selects the material and Q/E cycle through its shapes.
    /// </summary>
    public sealed class BuildPalette
    {
        public const string RoofShape = "roof";

        private readonly Dictionary<string, List<BuildPiece>> _byItem = new Dictionary<string, List<BuildPiece>>();
        private readonly Dictionary<string, string> _materialOfItem = new Dictionary<string, string>();

        public BuildPalette(ConstructionData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            var uses = new Dictionary<string, int>();
            foreach (ConstructionMaterial m in data.materials)
            {
                foreach (ConstructionCost c in m.ingredients)
                {
                    uses[c.item] = uses.TryGetValue(c.item, out int n) ? n + 1 : 1;
                }
            }
            var shapeNames = new Dictionary<string, string>();
            foreach (ConstructionShape s in data.shapes)
            {
                shapeNames[s.id] = s.name;
            }
            foreach (ConstructionMaterial m in data.materials)
            {
                string key = KeyItem(m.ingredients, uses);
                if (_materialOfItem.ContainsKey(key))
                {
                    throw new ArgumentException($"Two materials share the item '{key}'.");
                }
                _materialOfItem[key] = m.id;
                var pieces = new List<BuildPiece>();
                foreach (string shape in m.shapes)
                {
                    pieces.Add(new BuildPiece($"{shape}_{m.id}", shape, shapeNames.TryGetValue(shape, out string name) ? name : shape));
                }
                _byItem[key] = pieces;
            }
            foreach (ConstructionRoof r in data.roofs)
            {
                string key = r.ingredients[0].item;
                if (!_byItem.TryGetValue(key, out List<BuildPiece> pieces))
                {
                    pieces = new List<BuildPiece>();
                    _byItem[key] = pieces;
                }
                pieces.Add(new BuildPiece($"roof_{r.id}", RoofShape, "Tejado: " + r.name));
            }
        }

        private static string KeyItem(ConstructionCost[] ingredients, Dictionary<string, int> uses)
        {
            string best = ingredients[0].item;
            foreach (ConstructionCost c in ingredients)
            {
                if (uses[c.item] < uses[best])
                {
                    best = c.item;
                }
            }
            return best;
        }

        /// <summary>The pieces an item can place, in the order Q/E cycle through them. Empty when it builds nothing.</summary>
        public IReadOnlyList<BuildPiece> PiecesFor(string itemId) =>
            itemId != null && _byItem.TryGetValue(itemId, out List<BuildPiece> list) ? list : Empty;

        public bool Builds(string itemId) => PiecesFor(itemId).Count > 0;

        /// <summary>The material an item stands for ("brick"), or null (roof-only items, other items).</summary>
        public string MaterialOf(string itemId) => itemId != null && _materialOfItem.TryGetValue(itemId, out string m) ? m : null;

        private static readonly BuildPiece[] Empty = new BuildPiece[0];
    }
}
