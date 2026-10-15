using System;

namespace CepaCero.Util
{
    /// <summary>Float 2D vector in tile units. Engine-independent replacement for Vector2.</summary>
    [Serializable]
    public struct Float2 : IEquatable<Float2>
    {
        public float X;
        public float Y;

        public Float2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Float2 Zero = new Float2(0f, 0f);

        public float LengthSquared => X * X + Y * Y;
        public float Length => MathF.Sqrt(LengthSquared);

        public Float2 Normalized
        {
            get
            {
                float len = Length;
                return len > 1e-6f ? new Float2(X / len, Y / len) : Zero;
            }
        }

        /// <summary>Tile that contains this point (floor of each component).</summary>
        public Int2 Tile => new Int2((int)MathF.Floor(X), (int)MathF.Floor(Y));

        public static Float2 operator +(Float2 a, Float2 b) => new Float2(a.X + b.X, a.Y + b.Y);
        public static Float2 operator -(Float2 a, Float2 b) => new Float2(a.X - b.X, a.Y - b.Y);
        public static Float2 operator *(Float2 a, float s) => new Float2(a.X * s, a.Y * s);

        public bool Equals(Float2 other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Float2 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }
}
