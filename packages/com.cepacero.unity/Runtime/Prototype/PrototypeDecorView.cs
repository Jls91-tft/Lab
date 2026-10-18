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
        private readonly List<(SpriteRenderer renderer, Sprite[] frames)> _animated = new List<(SpriteRenderer, Sprite[])>();
        private bool _visible = true;
        private int _roofVersion = -1;

        public PrototypeDecorView(IReadOnlyList<DecorItem> items, PrototypeArt art, Transform parent, Transform upright)
        {
            var root = new GameObject("Decoration (prototype)").transform;
            root.SetParent(parent, false);
            var standing = new GameObject("Standing decoration").transform;
            standing.SetParent(upright, false);
            foreach (DecorItem item in items)
            {
                if (!art.TryGet(item.Sprite, out Sprite sprite))
                {
                    continue; // an old atlas: skip rather than fail
                }
                var go = new GameObject(item.Sprite);
                SpriteRenderer r = go.AddComponent<SpriteRenderer>();
                r.sprite = sprite;
                r.flipX = item.FlipX;
                PrototypeMaterials.Apply(r);

                Vector2 size = art.UnitSize(item.Sprite);
                float x = item.X + item.Width / 2f + item.ShiftX;
                float y = item.Y + item.Height / 2f + item.ShiftY;
                if (item.Flat)
                {
                    go.transform.SetParent(root, false);                      // marks on the ground: on the grid (D-025)
                    go.transform.localPosition = new Vector3(x, y, 0f);
                    r.sortingOrder = FlatOrder;
                }
                else
                {
                    // Standing pieces: an anchor on the projected footprint sorts them with walls and people; the sprite stands on it.
                    var anchor = new GameObject(item.Sprite + " (anchor)");
                    anchor.transform.SetParent(standing, false);
                    anchor.transform.localPosition = PrototypeIso.ToScreen(x, y);
                    UnityEngine.Rendering.SortingGroup group = anchor.AddComponent<UnityEngine.Rendering.SortingGroup>();
                    group.sortingOrder = PrototypeIso.UprightOrder;
                    go.transform.SetParent(anchor.transform, false);
                    go.transform.localPosition = new Vector3(0f, size.y / 2f - 0.25f, 0f);
                }
                _renderers.Add(r);
                if (item.Sprite.EndsWith("_fire_0"))
                {
                    var frames = new Sprite[4];
                    for (int f = 0; f < 4; f++)
                    {
                        frames[f] = art.TryGet(item.Sprite.Substring(0, item.Sprite.Length - 1) + f, out Sprite fs) ? fs : sprite;
                    }
                    _animated.Add((r, frames));
                }
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

        /// <summary>Burning barrels and the like: about five frames a second, each out of step with the others.</summary>
        public void Animate(float time)
        {
            foreach ((SpriteRenderer renderer, Sprite[] frames) in _animated)
            {
                if (renderer.enabled)
                {
                    renderer.sprite = frames[((int)(time * 5f + renderer.transform.parent.localPosition.x * 0.7f)) & 3];
                }
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
