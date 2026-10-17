using System.Collections.Generic;
using CepaCero.Util;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Draws a DecorPlanner plan: flat marks between the ground and the blocks, standing
    /// pieces sorted by height on screen together with players and zombies. Static
    /// sprites; the night tint changes, and whatever stands under a closed roof is
    /// hidden until you go into that house (D-024).
    /// </summary>
    public sealed class PrototypeDecorView
    {
        /// <summary>Between the ground (0) and the blocks (2) Tilemaps.</summary>
        public const int FlatOrder = 1;

        private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
        private readonly List<Float2> _centres = new List<Float2>();
        private float _ambient = -1f;
        private bool _visible = true;
        private int _roofVersion = -1;

        public PrototypeDecorView(IReadOnlyList<DecorItem> items, PrototypeArt art, Transform parent)
        {
            var root = new GameObject("Decoration (prototype)").transform;
            root.SetParent(parent, false);
            foreach (DecorItem item in items)
            {
                if (!art.TryGet(item.Sprite, out Sprite sprite))
                {
                    continue; // an old atlas: skip rather than fail
                }
                var go = new GameObject(item.Sprite);
                go.transform.SetParent(root, false);
                SpriteRenderer r = go.AddComponent<SpriteRenderer>();
                r.sprite = sprite;
                r.flipX = item.FlipX;
                PrototypeMaterials.Apply(r);

                // Centred on its footprint; standing pieces rest on the footprint's bottom edge.
                Vector2 size = art.UnitSize(item.Sprite);
                float x = item.X + item.Width / 2f;
                float y = item.Flat ? item.Y + item.Height / 2f : item.Y + size.y / 2f;
                go.transform.localPosition = new Vector3(x, y, 0f);
                r.sortingOrder = item.Flat ? FlatOrder : PrototypeEntityView.SortingOrder(item.Y + 0.3f, PrototypeEntityView.DecorBias);
                _renderers.Add(r);
                _centres.Add(new Float2(item.X + item.Width / 2f, item.Y + item.Height / 2f));
            }
        }

        public int Count => _renderers.Count;

        /// <summary>The street's decoration only exists on level 0.</summary>
        public void SetVisible(bool visible)
        {
            if (_visible == visible)
            {
                return;
            }
            _visible = visible;
            _roofVersion = -1;                 // re-check every piece against the roofs
        }

        /// <summary>Hides what stands under a closed roof and shows it again when you go in. Cheap: only runs when the roofs change.</summary>
        public void ApplyRoofs(PrototypeWorldView world)
        {
            if (world.RoofVersion == _roofVersion)
            {
                return;
            }
            _roofVersion = world.RoofVersion;
            for (int i = 0; i < _renderers.Count; i++)
            {
                _renderers[i].enabled = _visible && !world.IsHiddenByRoof(_centres[i]);
            }
        }

        /// <summary>Night tint, like the Tilemaps; only repaints when the light really changed.</summary>
        public void SetAmbient(float light)
        {
            if (Mathf.Abs(light - _ambient) < 0.01f)
            {
                return;
            }
            _ambient = light;
            float v = Mathf.Lerp(0.3f, 1f, light);
            var tint = new Color(v, v, Mathf.Min(1f, v * 1.1f), 1f);
            foreach (SpriteRenderer r in _renderers)
            {
                r.color = tint;
            }
        }
    }
}
