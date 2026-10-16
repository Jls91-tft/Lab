using System.Collections.Generic;
using CepaCero.Util;
using CepaCero.World;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Ghosts of the piece in the hand: under the cursor or along the drag (green when
    /// it can go there, blue when it replaces another piece, red when not) and the
    /// queued cells waiting to be built (pale).
    /// </summary>
    public sealed class PrototypeBuildView
    {
        private static readonly Color Valid = new Color(0.55f, 1f, 0.55f, 0.65f);
        private static readonly Color Invalid = new Color(1f, 0.35f, 0.35f, 0.6f);
        private static readonly Color Waiting = new Color(0.8f, 0.9f, 1f, 0.6f);
        private static readonly Color Replacing = new Color(0.5f, 0.75f, 1f, 0.7f);
        private static readonly Color Selecting = new Color(0.4f, 0.7f, 1f, 0.45f);
        private static readonly Color Queued = new Color(1f, 1f, 1f, 0.35f);

        private readonly Transform _root;
        private readonly PrototypeWorldView _world;
        private readonly PrototypeArt _art;
        private readonly BlockRegistry _registry;
        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();

        public PrototypeBuildView(Transform parent, PrototypeWorldView world, PrototypeArt art, BlockRegistry registry)
        {
            _root = new GameObject("Build ghosts (prototype)").transform;
            _root.SetParent(parent, false);
            _world = world;
            _art = art;
            _registry = registry;
        }

        public void Draw(PrototypeBuilder builder)
        {
            int used = 0;
            if (builder.Active || builder.Pasting)
            {
                foreach (PrototypeBuilder.Placement p in builder.Queue)
                {
                    Place(used++, p.Cell, p.Type, Queued);
                }
                foreach (PrototypeBuilder.Placement p in builder.Preview)
                {
                    Color c;
                    switch (p.Verdict)
                    {
                        case PrototypeBuilder.Verdict.Ok: c = Valid; break;
                        case PrototypeBuilder.Verdict.Replace: c = Replacing; break;
                        case PrototypeBuilder.Verdict.OutOfReach: c = Waiting; break;
                        default: c = Invalid; break;
                    }
                    Place(used++, p.Cell, p.Type, c);
                }
            }
            foreach (Int2 cell in builder.SelectionCells)
            {
                Place(used++, cell, _art.Get("ui_white"), Selecting);
            }
            for (int i = used; i < _pool.Count; i++)
            {
                if (_pool[i].gameObject.activeSelf)
                {
                    _pool[i].gameObject.SetActive(false);
                }
            }
        }

        private void Place(int index, Int2 cell, ushort type, Color color) =>
            Place(index, cell, _world.SpriteOf(_registry[type]), color);

        private void AddRenderer()
        {
            var go = new GameObject("ghost");
            go.transform.SetParent(_root, false);
            SpriteRenderer r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = short.MaxValue - 1; // over everything but the cursor
            PrototypeMaterials.Apply(r);
            _pool.Add(r);
        }

        private void Place(int index, Int2 cell, Sprite sprite, Color color)
        {
            while (_pool.Count <= index)
            {
                AddRenderer();
            }
            SpriteRenderer sr = _pool[index];
            if (!sr.gameObject.activeSelf)
            {
                sr.gameObject.SetActive(true);
            }
            sr.sprite = sprite;
            sr.color = color;
            sr.transform.localPosition = new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0f);
        }
    }
}
