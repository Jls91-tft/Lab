namespace CepaCero.AI
{
    /// <summary>What a zombie is doing. Sent to clients (4 bits) to pick the animation.</summary>
    public enum ZombieState : byte
    {
        Idle = 0,
        Chasing = 1,
        AttackingBlock = 2,
        AttackingPlayer = 3,
        Resting = 4,
    }
}
