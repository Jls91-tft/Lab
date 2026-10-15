using System.Text;
using CepaCero.Items;
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

        public void Draw(in HudModel m)
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                _box = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft, wordWrap = true, richText = true };
            }

            _sb.Clear();
            _sb.Append("<b>Día ").Append(m.Day).Append("</b> · ").Append(PhaseName(m.Phase))
               .Append(" ").Append((int)(m.PhaseProgress * 100)).Append("%");
            if (m.NightsSurvived >= 0)
            {
                _sb.Append("   Noches superadas: ").Append(m.NightsSurvived);
            }
            _sb.Append("   Zombis a la vista: ").Append(m.ZombiesShown);
            if (m.ZombiesKilled >= 0)
            {
                _sb.Append("   Abatidos: ").Append(m.ZombiesKilled);
            }
            _sb.Append('\n');
            _sb.Append("Vida <b>").Append(m.Me.Health).Append("</b>   Stamina ").Append((int)m.Me.Stamina)
               .Append(m.Me.IsDead ? "   <color=red>MUERTO: reapareces al amanecer</color>" : m.Me.IsDowned ? "   <color=orange>CAÍDO: que te levanten (R)</color>" : "").Append('\n');
            _sb.Append("Construir (B): <b>").Append(m.Building).Append("</b>");
            if (!string.IsNullOrEmpty(m.Network))
            {
                _sb.Append("   <color=cyan>").Append(m.Network).Append("</color>");
            }
            _sb.Append('\n');
            if (m.Inventory != null)
            {
                Inventory inv = m.Inventory;
                _sb.Append("Acceso rápido: ");
                for (int h = 0; h < inv.HotbarSlots; h++)
                {
                    ItemStack st = inv.GetHotbar(h);
                    string name = st.IsEmpty ? "—" : inv.Catalog[st.Item].name + (st.Count > 1 ? " ×" + st.Count : "") + (st.Durability > 0 ? " [" + st.Durability + "]" : "");
                    _sb.Append(h == inv.SelectedHotbar ? "<b>[" : " ").Append(h + 1).Append(": ").Append(name).Append(h == inv.SelectedHotbar ? "]</b>" : " ").Append("  ");
                }
                _sb.Append("\nMochila (").Append(inv.UsedSlots).Append('/').Append(inv.BagSlots).Append("): ");
                AppendBag(inv);
                if (m.OpenContainer != null)
                {
                    _sb.Append("\n<b>Contenedor</b> (T para coger): ");
                    if (m.OpenContainer.Bag.Count == 0)
                    {
                        _sb.Append("(vacío)");
                    }
                    AppendBag(m.OpenContainer);
                }
            }
            if (!string.IsNullOrEmpty(m.Message))
            {
                _sb.Append("\n<color=yellow>").Append(m.Message).Append("</color>");
            }
            GUI.Box(new Rect(10, 10, Mathf.Min(900, Screen.width - 20), 150), _sb.ToString(), _box);

            GUI.Label(new Rect(10, Screen.height - 46, Screen.width - 20, 40),
                "WASD moverse · Shift correr · Clic izq. atacar/disparar · Clic der. construir · B cambiar bloque · F romper/talar · " +
                "E puerta/contenedor · T coger · G recoger del suelo · Q usar · 1-5 elegir · V equipar 1.º de la mochila · C vendas · " +
                "R levantar compañero · F5 guardar · F9 cargar",
                _style);

            if (!string.IsNullOrEmpty(m.Diary))
            {
                GUI.Box(new Rect(Screen.width - 430, 170, 420, 160), "<b>Diario</b>\n" + m.Diary, _box);
            }
        }

        private void AppendBag(Inventory inv)
        {
            for (int i = 0; i < inv.Bag.Count; i++)
            {
                ItemStack st = inv.Bag[i];
                _sb.Append(inv.Catalog[st.Item].name).Append(" ×").Append(st.Count).Append(i < inv.Bag.Count - 1 ? ", " : "");
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
