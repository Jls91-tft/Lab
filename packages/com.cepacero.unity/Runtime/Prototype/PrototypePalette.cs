using CepaCero.World;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>Flat colours for the debug prototype (same spirit as the osm2tiles preview). Not final art.</summary>
    public static class PrototypePalette
    {
        public static Color32 Ground(GroundType type)
        {
            switch (type)
            {
                case GroundType.Asphalt: return new Color32(58, 58, 64, 255);
                case GroundType.Sidewalk: return new Color32(150, 146, 140, 255);
                case GroundType.Grass: return new Color32(76, 128, 62, 255);
                case GroundType.Water: return new Color32(52, 96, 160, 255);
                case GroundType.Floor: return new Color32(196, 186, 166, 255);
                case GroundType.Rail: return new Color32(110, 80, 70, 255);
                default: return new Color32(122, 104, 84, 255);
            }
        }

        /// <summary>Colour of a block, or null-like alpha 0 for air (show the ground).</summary>
        public static Color32 Block(string key)
        {
            switch (key)
            {
                case "building_wall": return new Color32(40, 30, 30, 255);
                case "tree": return new Color32(30, 80, 34, 255);
                case "door": return new Color32(220, 160, 40, 255);
                case "door_open": return new Color32(240, 210, 120, 255);
                case "fence": return new Color32(170, 120, 70, 255);
                case "wood_wall": return new Color32(150, 100, 50, 255);
                case "metal_wall": return new Color32(150, 160, 175, 255);
                case "concrete_wall": return new Color32(110, 110, 115, 255);
                case "workbench": return new Color32(200, 120, 40, 255);
                case "chest": return new Color32(120, 70, 30, 255);
                case "spike_trap": return new Color32(200, 60, 60, 255);
                default: return new Color32(0, 0, 0, 0);
            }
        }

        public static Color Zombie(string id)
        {
            switch (id)
            {
                case "walker": return new Color(0.45f, 0.6f, 0.45f);
                case "runner": return new Color(0.7f, 0.85f, 0.3f);
                case "screamer": return new Color(0.7f, 0.35f, 0.85f);
                case "spitter": return new Color(0.2f, 1f, 0.35f);
                case "brute": return new Color(0.6f, 0.15f, 0.15f);
                default: return new Color(0.5f, 0.7f, 0.5f);
            }
        }

        public static readonly Color Player = new Color(0.3f, 0.7f, 1f);
        public static readonly Color OtherPlayer = new Color(1f, 0.6f, 0.2f);
        public static readonly Color Downed = new Color(0.4f, 0.4f, 0.4f);
        public static readonly Color Pile = new Color(1f, 0.9f, 0.3f);
        public static readonly Color Container = new Color(0.55f, 0.35f, 0.15f);
    }
}
