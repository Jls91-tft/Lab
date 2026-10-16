using CepaCero.Net;
using CepaCero.Rules;

namespace CepaCero.Core
{
    public enum SessionEventType : byte
    {
        PhaseChanged = 1,
        NightStarted = 2,
        PlayerDowned = 3,
        GameOver = 4,
        IntentResolved = 5,
        PlayerHurt = 6,

        /// <summary>The player's inventory changed: send it to its owner.</summary>
        InventoryChanged = 7,
        RecipeUnlocked = 8,
        PlayerHealed = 9,
        PlayerRevived = 10,
        PlayerDied = 11,
        PlayerRespawned = 12,

        /// <summary>A pile appeared or changed on the ground (Value = pile id).</summary>
        GroundItemsChanged = 13,

        /// <summary>A diary entry was found (Value = index in the diary data).</summary>
        DiaryUnlocked = 14,

        /// <summary>A food bar ran low (Value 0) or empty (Value 1).</summary>
        Hungry = 15,

        /// <summary>A water bar ran low (Value 0) or empty (Value 1).</summary>
        Thirsty = 16,

        /// <summary>A player fell to a lower level (Value = levels fallen).</summary>
        PlayerFell = 17,
    }

    /// <summary>Match-level happenings the host broadcasts (UI, music, results of intents).</summary>
    public readonly struct SessionEvent
    {
        public readonly SessionEventType Type;
        public readonly int Player;
        public readonly int Value;
        public readonly DayPhase Phase;
        public readonly IntentResult Result;

        public SessionEvent(SessionEventType type, int player = -1, int value = 0, DayPhase phase = DayPhase.Day,
            IntentResult result = IntentResult.Ok)
        {
            Type = type;
            Player = player;
            Value = value;
            Phase = phase;
            Result = result;
        }

        public override string ToString() => $"{Type} player={Player} value={Value} phase={Phase} result={Result}";
    }
}
