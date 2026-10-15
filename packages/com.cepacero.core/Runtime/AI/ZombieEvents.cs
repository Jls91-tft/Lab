using CepaCero.Util;

namespace CepaCero.AI
{
    /// <summary>A zombie hit a player. The host's game layer applies it to the player's health.</summary>
    public readonly struct PlayerHit
    {
        public readonly ushort ZombieId;
        public readonly int PlayerIndex;
        public readonly int Damage;
        public readonly byte ZombieType;

        public PlayerHit(ushort zombieId, int playerIndex, int damage, byte zombieType)
        {
            ZombieId = zombieId;
            PlayerIndex = playerIndex;
            Damage = damage;
            ZombieType = zombieType;
        }
    }

    /// <summary>A zombie died. Used for loot drops, samples and score.</summary>
    public readonly struct ZombieDeath
    {
        public readonly ushort ZombieId;
        public readonly byte ZombieType;
        public readonly Float2 Position;

        public ZombieDeath(ushort zombieId, byte zombieType, Float2 position)
        {
            ZombieId = zombieId;
            ZombieType = zombieType;
            Position = position;
        }
    }
}
