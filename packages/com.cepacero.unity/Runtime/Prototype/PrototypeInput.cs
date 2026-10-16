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

        /// <summary>F, held, not latched: helping a teammate up needs the key kept down.</summary>
        public bool Revive;

        /// <summary>Ctrl held: Ctrl + drag draws a room, Ctrl + Z undoes.</summary>
        public bool Ctrl;

        public Float2 MouseWorld;

        /// <summary>Left button pressed: the hand decides what it does (D-022).</summary>
        public bool Attack;

        /// <summary>Left button held (chopping and digging keep going while it is down).</summary>
        public bool AttackHeld;

        /// <summary>Right button pressed: open the door or chest, or take a piece down with material in hand.</summary>
        public bool Secondary;

        /// <summary>Middle button: copy the piece under the cursor (shape, colour, rotation).</summary>
        public bool Pick;

        /// <summary>Q / E: previous and next shape of the material in the hand (−1, 0, +1 summed per tick).</summary>
        public int ShapeStep;

        /// <summary>[ and ] (or , and .): previous and next paint colour.</summary>
        public int ColorStep;

        /// <summary>R: turn the piece a quarter.</summary>
        public bool Rotate;

        /// <summary>Ctrl + Z.</summary>
        public bool Undo;

        /// <summary>Alt held: Alt + drag selects an area to copy.</summary>
        public bool Alt;

        /// <summary>Ctrl + C, Ctrl + V, Ctrl + S, Ctrl + L: copy, paste, save and load the next saved blueprint.</summary>
        public bool CopyPlan;
        public bool PastePlan;
        public bool SavePlan;
        public bool LoadPlan;

        public bool Take;
        public bool PickUp;

        /// <summary>Left button released (ends a building drag).</summary>
        public bool AttackReleased;

        /// <summary>Escape (cancels a drag, then the build queue).</summary>
        public bool Cancel;

        /// <summary>Mouse wheel notches since the last tick (picks the hotbar slot).</summary>
        public int Scroll;
        public bool EquipFromBag;
        public bool CraftBandage;
        public bool Save;
        public bool Load;
        public int Hotbar = -1;

        /// <summary>Controls panel on screen; H toggles it (not a simulation action).</summary>
        public bool ShowHelp;

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
            Ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            Revive = kb.fKey.isPressed;
            Alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;

            Take |= kb.tKey.wasPressedThisFrame;
            PickUp |= kb.gKey.wasPressedThisFrame;
            Cancel |= kb.escapeKey.wasPressedThisFrame;
            EquipFromBag |= !Ctrl && kb.vKey.wasPressedThisFrame;
            CraftBandage |= !Ctrl && kb.cKey.wasPressedThisFrame;
            CopyPlan |= Ctrl && kb.cKey.wasPressedThisFrame;
            PastePlan |= Ctrl && kb.vKey.wasPressedThisFrame;
            SavePlan |= Ctrl && kb.sKey.wasPressedThisFrame;
            LoadPlan |= Ctrl && kb.lKey.wasPressedThisFrame;
            Save |= kb.f5Key.wasPressedThisFrame;
            Load |= kb.f9Key.wasPressedThisFrame;
            ShapeStep += (kb.eKey.wasPressedThisFrame ? 1 : 0) - (kb.qKey.wasPressedThisFrame ? 1 : 0);
            ColorStep += (kb.rightBracketKey.wasPressedThisFrame || kb.periodKey.wasPressedThisFrame ? 1 : 0)
                - (kb.leftBracketKey.wasPressedThisFrame || kb.commaKey.wasPressedThisFrame ? 1 : 0);
            Undo |= Ctrl && kb.zKey.wasPressedThisFrame;
            Rotate |= !Ctrl && kb.rKey.wasPressedThisFrame;
            if (kb.hKey.wasPressedThisFrame)
            {
                ShowHelp = !ShowHelp;
            }
            if (kb.digit1Key.wasPressedThisFrame) { Hotbar = 0; }
            if (kb.digit2Key.wasPressedThisFrame) { Hotbar = 1; }
            if (kb.digit3Key.wasPressedThisFrame) { Hotbar = 2; }
            if (kb.digit4Key.wasPressedThisFrame) { Hotbar = 3; }
            if (kb.digit5Key.wasPressedThisFrame) { Hotbar = 4; }
            if (kb.digit6Key.wasPressedThisFrame) { Hotbar = 5; }
            if (kb.digit7Key.wasPressedThisFrame) { Hotbar = 6; }
            if (kb.digit8Key.wasPressedThisFrame) { Hotbar = 7; }
            if (kb.digit9Key.wasPressedThisFrame) { Hotbar = 8; }

            if (mouse != null && camera != null)
            {
                Attack |= mouse.leftButton.wasPressedThisFrame;
                AttackReleased |= mouse.leftButton.wasReleasedThisFrame;
                AttackHeld = mouse.leftButton.isPressed;
                Secondary |= mouse.rightButton.wasPressedThisFrame;
                Pick |= mouse.middleButton.wasPressedThisFrame;
                float wheel = mouse.scroll.ReadValue().y;
                Scroll += wheel > 0f ? -1 : wheel < 0f ? 1 : 0;
                Vector2 screen = mouse.position.ReadValue();
                Vector3 world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
                MouseWorld = new Float2(world.x, world.y);
            }
        }

        /// <summary>Clears the latched presses once the simulation tick has used them.</summary>
        public void ClearPresses()
        {
            Attack = Secondary = Pick = Take = PickUp = EquipFromBag = CraftBandage = Save = Load = false;
            Rotate = Undo = AttackReleased = Cancel = CopyPlan = PastePlan = SavePlan = LoadPlan = false;
            ShapeStep = ColorStep = 0;
            Hotbar = -1;
            Scroll = 0;
        }
    }
}
