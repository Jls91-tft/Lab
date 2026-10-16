using System;
using System.Collections.Generic;
using CepaCero.World;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Flat-colour stand-ins for the construction pieces (D-022) until the 32 px atlas of tools/spritegen is wired in:
    /// a 16 px square per piece, coloured by material and marked by shape, so a house can be built and read today.
    /// A piece that has a real sprite in the atlas never uses these.
    /// </summary>
    public sealed class PrototypePlaceholders
    {
        private const int Size = 16;
        private readonly int _pixelsPerUnit;
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        private static readonly Dictionary<string, Color32> MaterialColors = new Dictionary<string, Color32>(StringComparer.Ordinal)
        {
            { "pallet", new Color32(170, 130, 80, 255) }, { "drywall", new Color32(222, 220, 210, 255) },
            { "wood", new Color32(150, 100, 60, 255) }, { "log", new Color32(110, 75, 45, 255) },
            { "sandbag", new Color32(190, 170, 120, 255) }, { "scrap", new Color32(130, 120, 110, 255) },
            { "adobe", new Color32(200, 140, 100, 255) }, { "container", new Color32(60, 110, 150, 255) },
            { "brick", new Color32(170, 70, 55, 255) }, { "stone", new Color32(135, 135, 140, 255) },
            { "concrete", new Color32(165, 165, 160, 255) }, { "rebar", new Color32(110, 115, 120, 255) },
            { "steel", new Color32(95, 110, 130, 255) }, { "glass", new Color32(150, 210, 230, 255) },
            { "earth", new Color32(105, 75, 50, 255) }, { "rock", new Color32(80, 80, 90, 255) },
            { "cave", new Color32(70, 52, 38, 255) }, { "pit", new Color32(30, 22, 18, 255) },
        };

        /// <summary>The 16 paint colours of <see cref="BlockVariant"/> (index 1..16; 0 is the natural colour).</summary>
        public static readonly Color32[] Paints =
        {
            new Color32(255, 255, 255, 255),
            new Color32(200, 60, 60, 255), new Color32(230, 130, 50, 255), new Color32(235, 205, 70, 255), new Color32(150, 200, 70, 255),
            new Color32(60, 150, 70, 255), new Color32(60, 170, 170, 255), new Color32(110, 190, 230, 255), new Color32(60, 100, 190, 255),
            new Color32(90, 70, 170, 255), new Color32(180, 100, 190, 255), new Color32(230, 130, 170, 255), new Color32(120, 80, 55, 255),
            new Color32(240, 240, 235, 255), new Color32(160, 160, 165, 255), new Color32(70, 70, 78, 255), new Color32(30, 30, 36, 255),
        };

        public PrototypePlaceholders(int pixelsPerUnit)
        {
            _pixelsPerUnit = pixelsPerUnit;
        }

        public Sprite Get(BlockType type)
        {
            if (!_sprites.TryGetValue(type.Key, out Sprite sprite))
            {
                sprite = Build(type);
                _sprites.Add(type.Key, sprite);
            }
            return sprite;
        }

        private Sprite Build(BlockType type)
        {
            string material = type.Build != null ? type.Build.MaterialId : "";
            string shape = type.Build != null ? type.Build.Shape : "wall";
            if (type.Build != null && type.Layer == BuildLayer.Roof)
            {
                material = "brick";
                shape = "roof";
            }
            Color32 baseColor = MaterialColors.TryGetValue(material, out Color32 c) ? c : new Color32(200, 0, 200, 255);
            Color32 dark = Shade(baseColor, 0.6f), light = Shade(baseColor, 1.25f), clear = new Color32(0, 0, 0, 0);
            var px = new Color32[Size * Size];
            void Fill(int x0, int y0, int x1, int y1, Color32 col)
            {
                for (int y = Math.Max(0, y0); y <= Math.Min(Size - 1, y1); y++)
                {
                    for (int x = Math.Max(0, x0); x <= Math.Min(Size - 1, x1); x++)
                    {
                        px[y * Size + x] = col;
                    }
                }
            }
            void Frame(Color32 col)
            {
                Fill(0, 0, Size - 1, 0, col);
                Fill(0, Size - 1, Size - 1, Size - 1, col);
                Fill(0, 0, 0, Size - 1, col);
                Fill(Size - 1, 0, Size - 1, Size - 1, col);
            }

            bool open = type.Build != null && type.Build.IsOpenVariant;
            if (material == "cave")
            {
                shape = "cave";
            }
            else if (material == "pit")
            {
                shape = "pit";
            }
            switch (shape)
            {
                case "cave":
                    Fill(0, 0, 15, 15, baseColor);
                    for (int i = 0; i < 12; i++)
                    {
                        Fill((i * 7) % 16, (i * 11) % 16, (i * 7) % 16, (i * 11) % 16, dark);
                    }
                    break;
                case "pit":
                    Fill(0, 0, 15, 15, new Color32(70, 52, 38, 255));
                    for (int y = 2; y < 14; y++)
                    {
                        for (int x = 2; x < 14; x++)
                        {
                            if ((x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f) < 25f)
                            {
                                px[y * Size + x] = baseColor;
                            }
                        }
                    }
                    break;
                case "halfwall":
                    Fill(0, 5, 15, 10, baseColor);
                    Fill(0, 5, 15, 5, dark);
                    Fill(0, 10, 15, 10, light);
                    break;
                case "pillar":
                    Fill(5, 5, 10, 10, baseColor);
                    Fill(5, 5, 10, 5, dark);
                    break;
                case "arch":
                    Fill(0, 0, 3, 15, baseColor);
                    Fill(12, 0, 15, 15, baseColor);
                    Fill(0, 12, 15, 15, baseColor);
                    break;
                case "door":
                case "gate":
                    if (open)
                    {
                        Fill(0, 0, 2, 15, baseColor);
                        Fill(13, 0, 15, 15, baseColor);
                    }
                    else
                    {
                        Fill(0, 0, 15, 15, baseColor);
                        Frame(dark);
                        Fill(7, 1, 8, 14, dark);
                        Fill(10, 7, 11, 8, light);
                    }
                    break;
                case "window":
                case "bigwindow":
                    Fill(0, 0, 15, 15, baseColor);
                    Frame(dark);
                    Fill(4, 4, 11, 11, new Color32(150, 210, 235, 255));
                    break;
                case "fence":
                    Fill(0, 7, 15, 8, baseColor);
                    for (int x = 1; x < Size; x += 5)
                    {
                        Fill(x, 2, x + 1, 13, baseColor);
                    }
                    break;
                case "floor":
                    Fill(0, 0, 15, 15, baseColor);
                    Frame(dark);
                    Fill(7, 0, 7, 15, dark);
                    break;
                case "stairs":
                    Fill(0, 0, 15, 15, baseColor);
                    for (int y = 1; y < Size; y += 4)
                    {
                        Fill(0, y, 15, y, dark);
                    }
                    break;
                case "hatch":
                    Fill(0, 0, 15, 15, baseColor);
                    Frame(dark);
                    Fill(4, 4, 11, 11, dark);
                    break;
                case "roof":
                    Fill(0, 0, 15, 15, new Color32(baseColor.r, baseColor.g, baseColor.b, 150));
                    for (int i = 0; i < Size; i += 4)
                    {
                        Fill(i, 0, i, 15, new Color32(dark.r, dark.g, dark.b, 170));
                    }
                    break;
                default:
                    Fill(0, 0, 15, 15, baseColor);
                    Frame(dark);
                    Fill(1, 14, 14, 14, light);
                    break;
            }
            for (int i = 0; i < px.Length; i++)
            {
                if (px[i].a == 0 && px[i].r == 0)
                {
                    px[i] = clear;
                }
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(px);
            texture.Apply(false);
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), _pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        private static Color32 Shade(Color32 c, float f) =>
            new Color32((byte)Math.Min(255, c.r * f), (byte)Math.Min(255, c.g * f), (byte)Math.Min(255, c.b * f), 255);
    }
}
