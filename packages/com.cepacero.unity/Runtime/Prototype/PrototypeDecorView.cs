using System.Collections.Generic;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Draws a DecorPlanner plan: flat marks between the ground and the blocks, standing
    /// pieces sorted by height on screen together with players and zombies. Static
    /// sprites; only the night tint changes.
    /// </summary>
    public sealed class PrototypeDecorView
    {
        /// <summary>Between the ground (0) and the blocks (2) Tilemaps.</summary>
        public const int FlatOrder = 1;

        private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();
        private float _ambient = -1f;

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
                Vector2 size = art.Size(item.Sprite) / art.PixelsPerUnit;
                float x = item.X + item.Width / 2f;
                float y = item.Flat ? item.Y + item.Height / 2f : item.Y + size.y / 2f;
                go.transform.localPosition = new Vector3(x, y, 0f);
                r.sortingOrder = item.Flat ? FlatOrder : PrototypeEntityView.SortingOrder(item.Y + 0.3f, PrototypeEntityView.DecorBias);
                _renderers.Add(r);
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
            foreach (SpriteRenderer r in _renderers)
            {
                r.enabled = visible;
            }
        }

        private bool _visible = true;

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
