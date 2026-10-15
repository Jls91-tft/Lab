using System;
using CepaCero.Util;

namespace CepaCero.Player
{
    /// <summary>
    /// One tick of player input (keyboard, gamepad or, later, touch). This is
    /// what the client sends and replays for prediction; quantised so the
    /// host and the client simulate exactly the same values.
    /// </summary>
    public struct PlayerInput
    {
        public sbyte MoveX;
        public sbyte MoveY;
        public bool Sprint;

        public const int Bytes = 3;

        /// <summary>Builds an input from a stick/WASD vector (length clamped to 1).</summary>
        public static PlayerInput FromVector(Float2 move, bool sprint)
        {
            if (move.LengthSquared > 1f)
            {
                move = move.Normalized;
            }
            return new PlayerInput
            {
                MoveX = (sbyte)Math.Clamp((int)MathF.Round(move.X * 127f), -127, 127),
                MoveY = (sbyte)Math.Clamp((int)MathF.Round(move.Y * 127f), -127, 127),
                Sprint = sprint,
            };
        }

        public Float2 Move
        {
            get
            {
                var v = new Float2(MoveX / 127f, MoveY / 127f);
                return v.LengthSquared > 1f ? v.Normalized : v;
            }
        }

        public void Write(ByteWriter writer)
        {
            writer.WriteByte(unchecked((byte)MoveX));
            writer.WriteByte(unchecked((byte)MoveY));
            writer.WriteByte(Sprint ? (byte)1 : (byte)0);
        }

        public static PlayerInput Read(ByteReader reader) => new PlayerInput
        {
            MoveX = unchecked((sbyte)reader.ReadByte()),
            MoveY = unchecked((sbyte)reader.ReadByte()),
            Sprint = reader.ReadByte() != 0,
        };
    }
}
