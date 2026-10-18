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

        /// <summary>Level of the player (0 = street).</summary>
        public int Level;

        /// <summary>Bar maxima (PlayerSettings of the match).</summary>
        public int MaxHealth;
        public float MaxStamina;

        /// <summary>Food and water bars in whole percents; −1 when hunger and thirst are off.</summary>
        public int Food;
        public int Water;

        public Inventory Inventory;
        public Inventory OpenContainer;
        public string Message;

        /// <summary>1 while a message is fresh, fading to 0 in its last second.</summary>
        public float MessageAlpha;

        /// <summary>The backpack panel is open (Tab); it also opens by itself next to an open container.</summary>
        public bool ShowBag;
        public string Diary;
        public float DiaryAlpha;

        /// <summary>Network line for the loopback mode ("" otherwise).</summary>
        public string Network;

        /// <summary>Show the controls panel (toggled with H).</summary>
        public bool ShowHelp;

        /// <summary>Building with the hand (D-022): what the item in the hand can place, the ghost and the queue.</summary>
        public PrototypeBuilder Build;

        /// <summary>"Muro de ladrillo · 3 Ladrillo" while the hand holds a material, otherwise "".</summary>
        public string BuildTitle;

        /// <summary>What the ghost says: a problem, or how to use the piece.</summary>
        public string BuildHint;
    }
}
