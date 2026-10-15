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

        /// <summary>Screamer charging its scream (interruptible).</summary>
        Screaming = 5,

        /// <summary>La Mole winding up or running a charge.</summary>
        Charging = 6,

        /// <summary>La Mole after hitting metal: takes extra damage.</summary>
        Stunned = 7,

        /// <summary>Has not noticed anyone yet.</summary>
        Unaware = 8,
    }
}
