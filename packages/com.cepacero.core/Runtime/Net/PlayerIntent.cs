using CepaCero.Util;

namespace CepaCero.Net
{
    public enum IntentType : byte
    {
        PlaceBlock = 1,
        HitBlock = 2,
        ToggleDoor = 3,
        MeleeAttack = 4,
    }

    /// <summary>
    /// What a client asks the host to do. Clients never send results (damage,
    /// health, success): the host validates and computes them.
    /// Wire format: type (byte), x, y, arg (ushort each) = 7 bytes.
    /// </summary>
    public struct PlayerIntent
    {
        public IntentType Type;
        public ushort X;
        public ushort Y;

        /// <summary>PlaceBlock: block type id. MeleeAttack: aim angle (0..65535 = 0..360°). Otherwise 0.</summary>
        public ushort Arg;

        public const int Bytes = 7;

        public static PlayerIntent Place(int x, int y, ushort blockType) =>
            new PlayerIntent { Type = IntentType.PlaceBlock, X = (ushort)x, Y = (ushort)y, Arg = blockType };

        public static PlayerIntent Hit(int x, int y) => new PlayerIntent { Type = IntentType.HitBlock, X = (ushort)x, Y = (ushort)y };

        public static PlayerIntent Door(int x, int y) => new PlayerIntent { Type = IntentType.ToggleDoor, X = (ushort)x, Y = (ushort)y };

        public static PlayerIntent Melee(Float2 aim)
        {
            float angle = System.MathF.Atan2(aim.Y, aim.X);
            if (angle < 0f)
            {
                angle += 2f * System.MathF.PI;
            }
            return new PlayerIntent { Type = IntentType.MeleeAttack, Arg = (ushort)(angle / (2f * System.MathF.PI) * 65535f) };
        }

        public Float2 AimDirection
        {
            get
            {
                float angle = Arg / 65535f * 2f * System.MathF.PI;
                return new Float2(System.MathF.Cos(angle), System.MathF.Sin(angle));
            }
        }

        public void Write(ByteWriter writer)
        {
            writer.WriteByte((byte)Type);
            writer.WriteUShort(X);
            writer.WriteUShort(Y);
            writer.WriteUShort(Arg);
        }

        public static PlayerIntent Read(ByteReader reader) => new PlayerIntent
        {
            Type = (IntentType)reader.ReadByte(),
            X = reader.ReadUShort(),
            Y = reader.ReadUShort(),
            Arg = reader.ReadUShort(),
        };
    }
}
