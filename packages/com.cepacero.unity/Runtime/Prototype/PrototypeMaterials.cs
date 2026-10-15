using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Unlit sprite material, so the prototype is visible without any 2D light in the
    /// scene (with the URP 2D renderer, lit sprites need lights). Falls back to the
    /// built-in sprite shader if the URP one is missing.
    /// </summary>
    public static class PrototypeMaterials
    {
        private static Material _unlit;

        public static Material Unlit
        {
            get
            {
                if (_unlit == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
                    _unlit = shader != null ? new Material(shader) { name = "PrototypeUnlit" } : null;
                }
                return _unlit;
            }
        }

        public static void Apply(SpriteRenderer renderer)
        {
            Material m = Unlit;
            if (m != null)
            {
                renderer.sharedMaterial = m;
            }
        }
    }
}
