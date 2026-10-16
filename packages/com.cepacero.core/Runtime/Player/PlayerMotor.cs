using CepaCero.Util;
using CepaCero.World;

namespace CepaCero.Player
{
    /// <summary>
    /// Pure movement step: same inputs, same world → same result. The owning
    /// client runs it to predict; the host runs it to be authoritative; on a
    /// mismatch FishNet reconciles by replaying inputs through this method.
    /// </summary>
    public static class PlayerMotor
    {
        /// <summary>Advances one tick. Returns the noise radius the footsteps made (0 = silent).</summary>
        public static float Step(ref PlayerState state, in PlayerInput input, float dt, BlockWorld blocks,
            GroundLayer ground, PlayerSettings settings, float staminaRegenFactor = 1f)
        {
            if (state.IsDowned)
            {
                state.IsSprinting = false;
                return 0f;
            }
            Float2 move = input.Move;
            bool moving = move.LengthSquared > 1e-4f;

            bool wantsSprint = input.Sprint && moving;
            if (wantsSprint && !state.IsSprinting && state.Stamina >= settings.StaminaToStartSprint)
            {
                state.IsSprinting = true;
            }
            if (!wantsSprint || state.Stamina <= 0f)
            {
                state.IsSprinting = false;
            }

            if (state.IsSprinting)
            {
                state.Stamina = System.Math.Max(0f, state.Stamina - settings.SprintCostPerSecond * dt);
            }
            else
            {
                state.Stamina = System.Math.Min(settings.MaxStamina, state.Stamina + settings.StaminaRegenPerSecond * staminaRegenFactor * dt);
            }

            if (!moving)
            {
                return 0f;
            }
            float speed = state.IsSprinting ? settings.SprintSpeed : settings.WalkSpeed;
            state.Position = TileCollision.Move(state.Position, move * (speed * dt), settings.Radius, blocks, ground, state.Level);
            return state.IsSprinting ? settings.SprintNoise : settings.WalkNoise;
        }
    }
}
