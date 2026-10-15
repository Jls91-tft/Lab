using CepaCero.Util;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Keyboard and mouse for the debug prototype. Polled every frame; button
    /// presses are latched until the next fixed tick consumes them, so a quick
    /// click is never lost between two simulation ticks. Gamepad comes with the
    /// real input map (week 1).
    /// </summary>
    public sealed class PrototypeInput
    {
        public Float2 Move;
        public bool Sprint;
        public Float2 MouseWorld;

        public bool Attack;
        public bool Place;
        public bool Harvest;
        public bool Interact;
        public bool Take;
        public bool PickUp;
        public bool Use;
        public bool CycleBuild;
        public bool EquipFromBag;
        public bool CraftBandage;
        public bool Save;
        public bool Load;
        public int Hotbar = -1;

        public void Poll(Camera camera)
        {
            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (kb == null)
            {
                return;
            }
            float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
            Move = new Float2(x, y);
            Sprint = kb.leftShiftKey.isPressed;

            Harvest |= kb.fKey.wasPressedThisFrame;
            Interact |= kb.eKey.wasPressedThisFrame;
            Take |= kb.tKey.wasPressedThisFrame;
            PickUp |= kb.gKey.wasPressedThisFrame;
            Use |= kb.qKey.wasPressedThisFrame;
            CycleBuild |= kb.bKey.wasPressedThisFrame;
            EquipFromBag |= kb.vKey.wasPressedThisFrame;
            CraftBandage |= kb.cKey.wasPressedThisFrame;
            Save |= kb.f5Key.wasPressedThisFrame;
            Load |= kb.f9Key.wasPressedThisFrame;
            if (kb.digit1Key.wasPressedThisFrame) { Hotbar = 0; }
            if (kb.digit2Key.wasPressedThisFrame) { Hotbar = 1; }
            if (kb.digit3Key.wasPressedThisFrame) { Hotbar = 2; }
            if (kb.digit4Key.wasPressedThisFrame) { Hotbar = 3; }
            if (kb.digit5Key.wasPressedThisFrame) { Hotbar = 4; }

            if (mouse != null && camera != null)
            {
                Attack |= mouse.leftButton.wasPressedThisFrame;
                Place |= mouse.rightButton.wasPressedThisFrame;
                Vector2 screen = mouse.position.ReadValue();
                Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
                MouseWorld = new Float2(world.x, world.y);
            }
        }

        /// <summary>Clears the latched presses once the simulation tick has used them.</summary>
        public void ClearPresses()
        {
            Attack = Place = Harvest = Interact = Take = PickUp = Use = CycleBuild = EquipFromBag = CraftBandage = Save = Load = false;
            Hotbar = -1;
        }
    }
}
