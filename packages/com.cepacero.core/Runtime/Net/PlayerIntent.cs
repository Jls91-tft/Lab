using CepaCero.Util;

namespace CepaCero.Net
{
    public enum IntentType : byte
    {
        // World actions (HostIntentRules): rate limited.
        PlaceBlock = 1,
        HitBlock = 2,
        ToggleDoor = 3,
        MeleeAttack = 4,
        RangedAttack = 5,

        // Inventory actions (HostItemRules). X/Y/Arg meaning in each factory below.
        SelectHotbar = 10,
        MoveToHotbar = 11,
        MoveToBag = 12,
        Craft = 13,
        UseItem = 14,
        PickUp = 15,
        DropStack = 16,
        TakeFromContainer = 17,
        PutInContainer = 18,

        /// <summary>Looks inside a container: the host starts sending its contents to this player.</summary>
        OpenContainer = 19,

        // Helping a downed teammate: sent every tick while the key is held.
        Revive = 20,
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

        public static PlayerIntent Ranged(Float2 aim)
        {
            PlayerIntent i = Melee(aim);
            i.Type = IntentType.RangedAttack;
            return i;
        }

        public static PlayerIntent Select(int hotbarSlot) => new PlayerIntent { Type = IntentType.SelectHotbar, Arg = (ushort)hotbarSlot };

        public static PlayerIntent ToHotbar(int bagIndex, int hotbarSlot) =>
            new PlayerIntent { Type = IntentType.MoveToHotbar, X = (ushort)bagIndex, Arg = (ushort)hotbarSlot };

        public static PlayerIntent ToBag(int hotbarSlot) => new PlayerIntent { Type = IntentType.MoveToBag, Arg = (ushort)hotbarSlot };

        public static PlayerIntent CraftRecipe(int recipeIndex) => new PlayerIntent { Type = IntentType.Craft, Arg = (ushort)recipeIndex };

        /// <summary>Uses the selected quick-access item (heal, analyse a sample).</summary>
        public static PlayerIntent Use() => new PlayerIntent { Type = IntentType.UseItem };

        public static PlayerIntent PickUpNearest() => new PlayerIntent { Type = IntentType.PickUp };

        public static PlayerIntent Drop(int bagIndex) => new PlayerIntent { Type = IntentType.DropStack, X = (ushort)bagIndex };

        public static PlayerIntent Take(int x, int y, int containerBagIndex) =>
            new PlayerIntent { Type = IntentType.TakeFromContainer, X = (ushort)x, Y = (ushort)y, Arg = (ushort)containerBagIndex };

        public static PlayerIntent Put(int x, int y, int playerBagIndex) =>
            new PlayerIntent { Type = IntentType.PutInContainer, X = (ushort)x, Y = (ushort)y, Arg = (ushort)playerBagIndex };

        public static PlayerIntent ReviveTeammate(int player) => new PlayerIntent { Type = IntentType.Revive, X = (ushort)player };

        public static PlayerIntent Open(int x, int y) => new PlayerIntent { Type = IntentType.OpenContainer, X = (ushort)x, Y = (ushort)y };

        public bool IsInventoryAction => Type >= IntentType.SelectHotbar && Type <= IntentType.OpenContainer;

        public bool UsesContainer => Type == IntentType.OpenContainer || Type == IntentType.TakeFromContainer || Type == IntentType.PutInContainer;

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
