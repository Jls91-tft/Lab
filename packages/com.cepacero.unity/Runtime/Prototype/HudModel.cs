using CepaCero.Items;
using CepaCero.Player;
using CepaCero.Rules;

namespace CepaCero.Unity.Prototype
{
    /// <summary>What the debug HUD shows. Filled from the host session or from a client replica.</summary>
    public struct HudModel
    {
        public int Day;
        public DayPhase Phase;
        public float PhaseProgress;

        /// <summary>−1 when unknown (a client does not get these numbers).</summary>
        public int NightsSurvived;

        public int ZombiesShown;

        /// <summary>−1 when unknown.</summary>
        public int ZombiesKilled;

        public PlayerState Me;
        public Inventory Inventory;
        public Inventory OpenContainer;
        public string Building;
        public string Message;
        public string Diary;

        /// <summary>Network line for the loopback mode ("" otherwise).</summary>
        public string Network;
    }
}
