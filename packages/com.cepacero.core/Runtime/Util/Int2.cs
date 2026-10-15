using System;

namespace CepaCero.Util
{
    /// <summary>Integer 2D coordinate (tile or chunk). Engine-independent replacement for Vector2Int.</summary>
    [Serializable]
    public readonly struct Int2 : IEquatable<Int2>
    {
        public readonly int X;
        public readonly int Y;

        public Int2(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Int2 operator +(Int2 a, Int2 b) => new Int2(a.X + b.X, a.Y + b.Y);
        public static Int2 operator -(Int2 a, Int2 b) => new Int2(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(Int2 a, Int2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Int2 a, Int2 b) => !(a == b);

        public bool Equals(Int2 other) => this == other;
        public override bool Equals(object obj) => obj is Int2 other && this == other;
        public override int GetHashCode() => (X * 73856093) ^ (Y * 19349663);
        public override string ToString() => $"({X}, {Y})";
    }
}
