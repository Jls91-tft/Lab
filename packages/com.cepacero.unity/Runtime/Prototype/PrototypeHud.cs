using System.Text;
using CepaCero.Core;
using CepaCero.Items;
using CepaCero.Player;
using CepaCero.Rules;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Immediate-mode debug HUD (OnGUI). Debug-only, so its texts are not localised;
    /// the real UI (UI Toolkit) will read data/generated/loc/strings.csv.
    /// </summary>
    public sealed class PrototypeHud
    {
        private readonly StringBuilder _sb = new StringBuilder(512);
        private GUIStyle _style;
        private GUIStyle _box;

        public void Draw(GameSession s, string building, string message, string diaryText)
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                _box = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true };
            }
            PlayerState me = s.Players[0];

            _sb.Clear();
            _sb.Append("<b>Día ").Append(s.Clock.Day).Append("</b> · ").Append(PhaseName(s.Clock.Phase))
               .Append(" ").Append((int)(s.Clock.PhaseProgress * 100)).Append("%")
               .Append("   Noches superadas: ").Append(s.NightsSurvived)
               .Append("   Zombis: ").Append(s.Zombies.Count).Append("   Abatidos: ").Append(s.ZombiesKilled).Append('\n');
            _sb.Append("Vida <b>").Append(me.Health).Append("</b>   Stamina ").Append((int)me.Stamina)
               .Append(me.IsDead ? "   <color=red>MUERTO: reapareces al amanecer</color>" : me.IsDowned ? "   <color=orange>CAÍDO</color>" : "").Append('\n');
            _sb.Append("Construir (B): <b>").Append(building).Append("</b>\n");
            if (s.Inventories.Count > 0)
            {
                Inventory inv = s.Inventories[0];
                _sb.Append("Acceso rápido: ");
                for (int h = 0; h < inv.HotbarSlots; h++)
                {
                    ItemStack st = inv.GetHotbar(h);
                    string name = st.IsEmpty ? "—" : inv.Catalog[st.Item].name + (st.Count > 1 ? " ×" + st.Count : "") + (st.Durability > 0 ? " [" + st.Durability + "]" : "");
                    _sb.Append(h == inv.SelectedHotbar ? "<b>[" : " ").Append(h + 1).Append(": ").Append(name).Append(h == inv.SelectedHotbar ? "]</b>" : " ").Append("  ");
                }
                _sb.Append("\nMochila (").Append(inv.UsedSlots).Append('/').Append(inv.BagSlots).Append("): ");
                for (int i = 0; i < inv.Bag.Count; i++)
                {
                    ItemStack st = inv.Bag[i];
                    _sb.Append(inv.Catalog[st.Item].name).Append(" ×").Append(st.Count).Append(i < inv.Bag.Count - 1 ? ", " : "");
                }
                Inventory box = s.OpenContainerContents(0);
                if (box != null)
                {
                    _sb.Append("\n<b>Contenedor</b> (T para coger): ");
                    for (int i = 0; i < box.Bag.Count; i++)
                    {
                        _sb.Append(box.Catalog[box.Bag[i].Item].name).Append(" ×").Append(box.Bag[i].Count).Append(i < box.Bag.Count - 1 ? ", " : "");
                    }
                    if (box.Bag.Count == 0)
                    {
                        _sb.Append("(vacío)");
                    }
                }
            }
            if (!string.IsNullOrEmpty(message))
            {
                _sb.Append("\n<color=yellow>").Append(message).Append("</color>");
            }
            GUI.Box(new Rect(10, 10, Mathf.Min(900, Screen.width - 20), 150), _sb.ToString(), _box);

            GUI.Label(new Rect(10, Screen.height - 46, Screen.width - 20, 40),
                "WASD moverse · Shift correr · Clic izq. atacar/disparar · Clic der. construir · B cambiar bloque · F romper/talar · " +
                "E puerta/contenedor · T coger · G recoger del suelo · Q usar · 1-5 elegir · V equipar 1.º de la mochila · C vendas · F5 guardar · F9 cargar",
                _style);

            if (!string.IsNullOrEmpty(diaryText))
            {
                GUI.Box(new Rect(Screen.width - 430, 170, 420, 160), "<b>Diario</b>\n" + diaryText, _box);
            }
        }

        private static string PhaseName(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Day: return "Día";
                case DayPhase.Dusk: return "Atardecer";
                case DayPhase.Night: return "<color=red>Noche</color>";
                default: return "Amanecer";
            }
        }
    }
}
