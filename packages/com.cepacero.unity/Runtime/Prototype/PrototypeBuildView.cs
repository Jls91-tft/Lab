using System.Collections.Generic;
using CepaCero.Util;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Ghosts of the building mode: the piece under the cursor or along the drag
    /// (green when it can go there, red when not) and the queued cells waiting to be
    /// built (pale).
    /// </summary>
    public sealed class PrototypeBuildView
    {
        private static readonly Color Valid = new Color(0.55f, 1f, 0.55f, 0.65f);
        private static readonly Color Invalid = new Color(1f, 0.35f, 0.35f, 0.6f);
        private static readonly Color Waiting = new Color(0.8f, 0.9f, 1f, 0.6f);
        private static readonly Color Queued = new Color(1f, 1f, 1f, 0.35f);

        private readonly Transform _root;
        private readonly PrototypeArt _art;
        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();

        public PrototypeBuildView(Transform parent, PrototypeArt art)
        {
            _root = new GameObject("Build ghosts (prototype)").transform;
            _root.SetParent(parent, false);
            _art = art;
        }

        public void Draw(PrototypeBuildMode mode)
        {
            int used = 0;
            if (mode.Active)
            {
                foreach (PrototypeBuildMode.Pending p in mode.Queue)
                {
                    Place(used++, p.Cell, p.Key, Queued);
                }
                for (int i = 0; i < mode.Preview.Count; i++)
                {
                    PrototypeBuildMode.Verdict v = mode.Verdicts[i];
                    Color c = v == PrototypeBuildMode.Verdict.Ok ? Valid : v == PrototypeBuildMode.Verdict.OutOfReach ? Waiting : Invalid;
                    Place(used++, mode.Preview[i], mode.SelectedKey, c);
                }
            }
            for (int i = used; i < _pool.Count; i++)
            {
                if (_pool[i].gameObject.activeSelf)
                {
                    _pool[i].gameObject.SetActive(false);
                }
            }
        }

        private void Place(int index, Int2 cell, string key, Color color)
        {
            while (_pool.Count <= index)
            {
                var go = new GameObject("ghost");
                go.transform.SetParent(_root, false);
                SpriteRenderer r = go.AddComponent<SpriteRenderer>();
                r.sortingOrder = short.MaxValue - 1; // over everything but the cursor
                PrototypeMaterials.Apply(r);
                _pool.Add(r);
            }
            SpriteRenderer sr = _pool[index];
            if (!sr.gameObject.activeSelf)
            {
                sr.gameObject.SetActive(true);
            }
            sr.sprite = _art.Block(key);
            sr.color = color;
            sr.transform.localPosition = new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0f);
        }
    }
}
