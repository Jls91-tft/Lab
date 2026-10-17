namespace CepaCero.Lighting
{
    /// <summary>One light on the plan (D-024): a lamp, a fire, the player's torch. Positions and radius are in tiles.</summary>
    public struct LightSource
    {
        public float X;
        public float Y;

        /// <summary>How far it reaches; the light fades smoothly to nothing at this distance.</summary>
        public float Radius;

        /// <summary>Colour of the light, 0..1 per channel.</summary>
        public float Red;
        public float Green;
        public float Blue;

        /// <summary>Strength at the source, 0..1.</summary>
        public float Intensity;

        /// <summary>Direction of a cone (unit vector); ignored when <see cref="ConeCos"/> is −1.</summary>
        public float DirX;
        public float DirY;

        /// <summary>Cosine of the cone's half angle: −1 lights all around (a lamp), 0.85 is a narrow torch beam.</summary>
        public float ConeCos;

        public static LightSource Omni(float x, float y, float radius, float red, float green, float blue, float intensity) => new LightSource
        {
            X = x,
            Y = y,
            Radius = radius,
            Red = red,
            Green = green,
            Blue = blue,
            Intensity = intensity,
            ConeCos = -1f,
        };

        public static LightSource Cone(float x, float y, float radius, float dirX, float dirY, float halfAngleCos, float red, float green, float blue, float intensity)
        {
            LightSource s = Omni(x, y, radius, red, green, blue, intensity);
            s.DirX = dirX;
            s.DirY = dirY;
            s.ConeCos = halfAngleCos;
            return s;
        }
    }
}
