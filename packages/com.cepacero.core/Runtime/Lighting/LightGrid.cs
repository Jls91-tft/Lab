using System;
using CepaCero.World;

namespace CepaCero.Lighting
{
    /// <summary>
    /// Light for a window of the map, one value per tile (D-024). Each source lights the tiles it can see: walls and closed doors
    /// block it (and cast a shadow), glass and open doors let it through. The view turns the result into a texture it multiplies
    /// over the world, so the night is dark wherever no light reaches. Pure data: nothing here touches the simulation.
    /// Reuse one grid between frames: it only allocates when the window changes size.
    /// </summary>
    public sealed class LightGrid
    {
        private float[] _r = new float[0];
        private float[] _g = new float[0];
        private float[] _b = new float[0];
        private float[] _tmp = new float[0];

        public int OriginX { get; private set; }

        public int OriginY { get; private set; }

        public int Width { get; private set; }

        public int Height { get; private set; }

        /// <summary>Starts a new frame over the window [origin, origin + size): clears every tile.</summary>
        public void Begin(int originX, int originY, int width, int height)
        {
            OriginX = originX;
            OriginY = originY;
            if (width != Width || height != Height)
            {
                Width = width;
                Height = height;
                _r = new float[width * height];
                _g = new float[width * height];
                _b = new float[width * height];
                _tmp = new float[width * height];
            }
            else
            {
                Array.Clear(_r, 0, _r.Length);
                Array.Clear(_g, 0, _g.Length);
                Array.Clear(_b, 0, _b.Length);
            }
        }

        public bool Contains(int x, int y) => x >= OriginX && y >= OriginY && x < OriginX + Width && y < OriginY + Height;

        /// <summary>The light added to a tile by the sources so far (0 outside the window).</summary>
        public void Get(int x, int y, out float red, out float green, out float blue)
        {
            if (!Contains(x, y))
            {
                red = green = blue = 0f;
                return;
            }
            int i = (y - OriginY) * Width + (x - OriginX);
            red = _r[i];
            green = _g[i];
            blue = _b[i];
        }

        /// <summary>Brightness of the tile, the strongest channel; for deciding what looks lit.</summary>
        public float Brightness(int x, int y)
        {
            Get(x, y, out float r, out float g, out float b);
            return Math.Max(r, Math.Max(g, b));
        }

        /// <summary>True when something blocks light in this cell: a solid wall, a closed door, untouched ground underground, the map's edge.</summary>
        public static bool Blocks(BlockWorld world, int level, int x, int y)
        {
            if (!world.InBounds(x, y))
            {
                return true;
            }
            BlockType type = world.GetBlock(x, y, level, BuildLayer.Structure);
            if (type.IsAir)
            {
                return level < 0 && world.IsUndug(x, y, level);
            }
            return type.IsSolid && (type.Build == null || !type.Build.SeeThrough);
        }

        /// <summary>Adds a light: every tile of the window within reach that the source can see gets its share.</summary>
        public void Add(BlockWorld world, int level, in LightSource light)
        {
            if (light.Intensity <= 0f || light.Radius <= 0f)
            {
                return;
            }
            int sx = (int)Math.Floor(light.X);
            int sy = (int)Math.Floor(light.Y);
            int reach = (int)Math.Ceiling(light.Radius);
            int x0 = Math.Max(OriginX, sx - reach);
            int x1 = Math.Min(OriginX + Width - 1, sx + reach);
            int y0 = Math.Max(OriginY, sy - reach);
            int y1 = Math.Min(OriginY + Height - 1, sy + reach);
            float r2 = light.Radius * light.Radius;
            bool cone = light.ConeCos > -0.999f;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x + 0.5f - light.X;
                    float dy = y + 0.5f - light.Y;
                    float d2 = dx * dx + dy * dy;
                    if (d2 >= r2)
                    {
                        continue;
                    }
                    float strength = Falloff(d2, r2) * light.Intensity;
                    if (cone)
                    {
                        float d = (float)Math.Sqrt(d2);
                        float c = d < 0.001f ? 1f : (dx * light.DirX + dy * light.DirY) / d;
                        strength *= ConeFactor(c, light.ConeCos);
                    }
                    if (strength <= 0.002f || !CanSee(world, level, sx, sy, x, y))
                    {
                        continue;
                    }
                    int i = (y - OriginY) * Width + (x - OriginX);
                    _r[i] += strength * light.Red;
                    _g[i] += strength * light.Green;
                    _b[i] += strength * light.Blue;
                }
            }
        }

        /// <summary>Softens the blocky edges of shadows and cones: one pass of a small weighted average.</summary>
        public void Smooth()
        {
            Smooth(_r);
            Smooth(_g);
            Smooth(_b);
        }

        /// <summary>The final tile colours: the ambient light plus what the sources added, capped at full brightness (no light is "more than day").</summary>
        public void Resolve(float ambientRed, float ambientGreen, float ambientBlue, byte[] rgba)
        {
            if (rgba.Length < Width * Height * 4)
            {
                throw new ArgumentException("The buffer is smaller than the window.", nameof(rgba));
            }
            for (int i = 0; i < Width * Height; i++)
            {
                rgba[i * 4] = ToByte(ambientRed + _r[i]);
                rgba[i * 4 + 1] = ToByte(ambientGreen + _g[i]);
                rgba[i * 4 + 2] = ToByte(ambientBlue + _b[i]);
                rgba[i * 4 + 3] = 255;
            }
        }

        private static byte ToByte(float v) => (byte)(Math.Min(1f, Math.Max(0f, v)) * 255f + 0.5f);

        /// <summary>1 at the source, 0 at the radius, smooth in between (a bright core and a long soft edge).</summary>
        private static float Falloff(float d2, float r2)
        {
            float t = 1f - d2 / r2;
            return t * t;
        }

        private static float ConeFactor(float cosine, float coneCos)
        {
            if (cosine <= coneCos)
            {
                return 0f;
            }
            float t = (cosine - coneCos) / Math.Max(0.0001f, 1f - coneCos);
            t = Math.Min(1f, t * 3f);                                // full strength a little inside the edge
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Straight line between two tiles (Bresenham): no blocking tile strictly between them. A step across a corner needs both
        /// of its neighbours to block, so light does not leak through a diagonal of walls but does not stop at a single corner.
        /// </summary>
        private static bool CanSee(BlockWorld world, int level, int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;
            int x = x0;
            int y = y0;
            while (x != x1 || y != y1)
            {
                int e2 = 2 * err;
                int nx = x;
                int ny = y;
                if (e2 > -dy)
                {
                    err -= dy;
                    nx += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    ny += sy;
                }
                if (nx != x && ny != y && Blocks(world, level, nx, y) && Blocks(world, level, x, ny))
                {
                    return false;
                }
                x = nx;
                y = ny;
                if ((x != x1 || y != y1) && Blocks(world, level, x, y))
                {
                    return false;
                }
            }
            return true;
        }

        private void Smooth(float[] channel)
        {
            Array.Copy(channel, _tmp, channel.Length);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float sum = 0f;
                    float weight = 0f;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= Height)
                        {
                            continue;
                        }
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= Width)
                            {
                                continue;
                            }
                            float w = (dx == 0 ? 2f : 1f) * (dy == 0 ? 2f : 1f);
                            sum += _tmp[yy * Width + xx] * w;
                            weight += w;
                        }
                    }
                    channel[y * Width + x] = sum / weight;
                }
            }
        }
    }
}
