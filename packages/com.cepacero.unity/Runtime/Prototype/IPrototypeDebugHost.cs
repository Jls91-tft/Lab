using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Player;
using CepaCero.Rules;
using CepaCero.World;

namespace CepaCero.Unity.Prototype
{
    /// <summary>What the debug bridge (D-023) reads from the running prototype.</summary>
    public interface IPrototypeDebugHost
    {
        /// <summary>The local player as the view shows it (the client replica in loopback mode).</summary>
        PlayerState Me { get; }

        int MeIndex { get; }

        /// <summary>The world the view draws (the client's copy in loopback mode).</summary>
        BlockWorld ViewBlocks { get; }

        GroundLayer ViewGround { get; }

        Inventory MyInventory { get; }

        DayNightCycle ViewClock { get; }

        /// <summary>The authoritative match: debug cheats (give, spawn, time) act here.</summary>
        GameSession HostSession { get; }

        PlayerSettings PlayerSettings { get; }

        bool IsRemoteClient { get; }

        /// <summary>Building with the hand: piece, queue and the ghost's verdicts.</summary>
        PrototypeBuilder Builder { get; }

        /// <summary>What the building ghost says right now ("" when the hand does not build).</summary>
        string BuildHint { get; }

        BuildCostMode BuildCost { get; }
    }
}
