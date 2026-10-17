using CepaCero.Util;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// The isometric view (D-025, Project Zomboid look). The simulation keeps its square grid; only the view is projected 2:1:
    /// a cell is a 64x32 diamond, east goes up-right and north up-left, so you see the south and west faces of everything.
    ///
    /// Two roots: what lies flat (ground, floors, decals, light, cursor, ghosts) stays in grid coordinates under a root that is
    /// rotated 45 degrees and squashed to half height, which is exactly this projection; what stands up (walls, people, trees,
    /// cars) is placed at its projected point under an upright root and sorted by screen height (the 2D renderer sorts along
    /// the Y axis, Renderer2D.asset), so whoever is further down the screen is in front.
    /// </summary>
    public static class PrototypeIso
    {
        /// <summary>Sorting order shared by everything that stands up; within it the renderer sorts by screen height.</summary>
        public const int UprightOrder = 10000;

        /// <summary>Roofs go over the walls of their building (and over whoever is behind it).</summary>
        public const int RoofOrder = 15000;

        /// <summary>Height of a wall on screen, in world units (80 px: spritegen iso.WALL_H).</summary>
        public const float WallHeight = 2.5f;

        private const float Sqrt2 = 1.41421356f;

        /// <summary>Screen position (world units of the camera) of a point of the grid.</summary>
        public static Vector3 ToScreen(Float2 grid) => new Vector3(grid.X - grid.Y, (grid.X + grid.Y) * 0.5f, 0f);

        public static Vector3 ToScreen(float x, float y) => new Vector3(x - y, (x + y) * 0.5f, 0f);

        /// <summary>The grid point under a screen position (inverse of <see cref="ToScreen(Float2)"/>).</summary>
        public static Float2 ToGrid(float screenX, float screenY) => new Float2(screenY + screenX * 0.5f, screenY - screenX * 0.5f);

        /// <summary>Creates the two roots. Children of <paramref name="flat"/> use grid coordinates; children of <paramref name="upright"/> use screen ones.</summary>
        public static Transform CreateRoots(out Transform flat, out Transform upright)
        {
            var root = new GameObject("Cepa Cero (prototype view)").transform;
            var squash = new GameObject("Iso squash").transform;
            squash.SetParent(root, false);
            squash.localScale = new Vector3(Sqrt2, Sqrt2 * 0.5f, 1f);
            flat = new GameObject("Flat (grid)").transform;
            flat.SetParent(squash, false);
            flat.localRotation = Quaternion.Euler(0f, 0f, 45f);
            upright = new GameObject("Upright (screen)").transform;
            upright.SetParent(root, false);
            return root;
        }
    }
}
