using System;
using CepaCero.Items;
using CepaCero.Rules;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Prototype HUD drawn in OnGUI from the generated atlas (D-018): vitals with icons
    /// and bars, clock, hotbar, bag, open container, messages, diary and a controls
    /// panel (H). Laid out in art pixels and scaled by the same whole number as the
    /// world, so it stays crisp. Prototype only: its texts are not localised yet (the
    /// real UI will read data/generated/loc/strings.csv).
    /// </summary>
    public sealed class PrototypeHud
    {
        private const int PanelBorder = 4; // tools/artgen ui.PANEL_BORDER
        private const int PanelSize = 12;
        private const int Slot = 20;
        private const int Gap = 2;
        private const int BagColumns = 5;
        private const int HotbarCount = 9;

        private static readonly string[] Controls =
        {
            "WASD moverse (W = arriba en pantalla) · Shift correr · Tab mochila · 1-9 o rueda: elegir de la barra",
            "El clic izquierdo hace lo que pida lo que llevas en la mano: arma ataca, hacha o pico rompen, pala cava, vendas se usan",
            "Con un material: clic pone · arrastra línea · Mayús contorno · Ctrl habitación · clic der. quita · clic central copia",
            "Q/E forma · R girar · [ ] (o , .) color · Ctrl+Z deshacer · martillo: desmonta y devuelve la mitad",
            "Planos: Alt + arrastrar elige · Ctrl+C copia · Ctrl+V pega (R gira, clic confirma) · Ctrl+S guarda · Ctrl+L carga el siguiente",
            "Niveles: Re Pág sube y Av Pág baja por escaleras, trampillas o fosas · pala y pico: clic cava (lateral bajo tierra, o hacia abajo en suelo libre)",
            "Clic der. abre puertas y contenedores · T coger del contenedor · G recoger del suelo · V equipar 1.º de la mochila",
            "C hacer vendas · L linterna · F (mantener) levantar a un compañero · F5 guardar · F9 cargar · F6 coste (normal, barato, gratis) · H ocultar",
            "El recuadro verde marca la casilla a tu alcance; rojo, demasiado lejos. Lo lejano espera en cola a que te acerques.",
        };

        private readonly PrototypeArt _art;
        private GUIStyle _text;
        private GUIStyle _small;
        private GUIStyle _smallRight;
        private GUIStyle _number;
        private GUIStyle _smallCenter;
        private GUIStyle _wrap;
        private int _styleScale = -1;
        private int _s = 1;

        public PrototypeHud(PrototypeArt art)
        {
            _art = art;
        }

        private float Width => Screen.width / (float)_s;

        private float Height => Screen.height / (float)_s;

        public void Draw(in HudModel m)
        {
            _s = PrototypeArt.UiZoom(Screen.height);
            if (_styleScale != _s)
            {
                BuildStyles();
            }
            Color previous = GUI.color;
            DrawVitals(m);
            DrawClock(m);
            if (m.Level != 0)
            {
                Text(Width / 2f - 60f, 4f, 120f, 11f, m.Level > 0 ? $"Planta {m.Level}" : $"Subsuelo {-m.Level}", PrototypePalette.Yellow, _smallCenter);
            }
            if (m.Inventory != null)
            {
                DrawHotbar(m.Inventory);
                if (m.Build != null && (m.Build.Active || m.Build.Pasting))
                {
                    DrawBuildPanel(m);
                }
                if (m.ShowBag || m.OpenContainer != null)
                {
                    DrawBag(m.Inventory);
                }
                if (m.OpenContainer != null)
                {
                    DrawContainer(m.OpenContainer);
                }
            }
            DrawMessage(m.Message, m.MessageAlpha);
            DrawDiary(m.Diary, m.DiaryAlpha);
            DrawHelp(m.ShowHelp, m.Inventory != null ? m.Inventory.BagSlots : 0);
            GUI.color = previous;
        }

        // ------------------------------------------------------------- panels

        private void DrawVitals(in HudModel m)
        {
            bool survival = m.Food >= 0;
            bool status = m.Me.IsDead || m.Me.IsDowned;
            float x = 4f;
            float y = 4f;
            Panel(x, y, 108f, 8f + (survival ? 4 : 2) * 12f + (status ? 11f : 0f));
            float row = y + 4f;
            Bar(x, ref row, "icon_health", m.Me.Health / (float)Math.Max(1, m.MaxHealth), PrototypePalette.Red, m.Me.Health.ToString());
            Bar(x, ref row, "icon_stamina", m.Me.Stamina / Math.Max(1f, m.MaxStamina), PrototypePalette.Yellow, ((int)m.Me.Stamina).ToString());
            if (survival)
            {
                Bar(x, ref row, "icon_food", m.Food / 100f, m.Food <= 25 ? PrototypePalette.Red : PrototypePalette.Orange, m.Food + "%");
                Bar(x, ref row, "icon_water", m.Water / 100f, m.Water <= 25 ? PrototypePalette.Red : PrototypePalette.LightBlue, m.Water + "%");
            }
            if (status)
            {
                Text(x + 5f, row, 100f, 11f, m.Me.IsDead ? "MUERTO: reapareces al amanecer" : "CAÍDO: que te levanten (R)",
                    m.Me.IsDead ? PrototypePalette.Red : PrototypePalette.Orange, _small);
            }
        }

        private void Bar(float x, ref float y, string icon, float fraction, Color color, string value)
        {
            Sprite(icon, x + 4f, y);
            float bx = x + 18f;
            Fill(bx, y + 3f, 60f, 5f, PrototypePalette.Black);
            Fill(bx, y + 3f, 60f * Mathf.Clamp01(fraction), 5f, color);
            Text(bx + 64f, y, 26f, 11f, value, PrototypePalette.White, _number);
            y += 12f;
        }

        private void DrawClock(in HudModel m)
        {
            float w = 116f;
            float x = Width - w - 4f;
            float y = 4f;
            bool nights = m.NightsSurvived >= 0;
            Panel(x, y, w, 8f + 11f + 6f + 11f + (nights ? 11f : 0f));
            Sprite(PhaseIcon(m.Phase), x + 4f, y + 4f);
            Text(x + 18f, y + 4f, w - 22f, 11f, $"Día {m.Day} · {PhaseName(m.Phase)}", PrototypePalette.White, _text);
            Fill(x + 4f, y + 17f, w - 8f, 3f, PrototypePalette.Black);
            Fill(x + 4f, y + 17f, (w - 8f) * Mathf.Clamp01(m.PhaseProgress), 3f, PhaseColor(m.Phase));
            float row = y + 23f;
            Sprite("icon_zombie", x + 4f, row);
            Text(x + 18f, row, 34f, 11f, m.ZombiesShown.ToString(), PrototypePalette.White, _number);
            if (m.ZombiesKilled >= 0)
            {
                Sprite("icon_skull", x + 56f, row);
                Text(x + 70f, row, 40f, 11f, m.ZombiesKilled.ToString(), PrototypePalette.White, _number);
            }
            if (nights)
            {
                Text(x + 4f, row + 11f, w - 8f, 11f, $"Noches superadas: {m.NightsSurvived}", PrototypePalette.Pale, _small);
            }
            if (!string.IsNullOrEmpty(m.Network))
            {
                float ny = y + 8f + 11f + 6f + 11f + (nights ? 11f : 0f) + 2f;
                Text(Width - 244f, ny, 240f, 11f, m.Network, PrototypePalette.Cyan, _smallRight);
            }
        }

        private void DrawHotbar(Inventory inv)
        {
            float total = HotbarCount * Slot + (HotbarCount - 1) * Gap;
            float x0 = (Width - total) / 2f;
            float y = Height - Slot - 4f;
            int slots = Math.Min(HotbarCount, inv.HotbarSlots);
            for (int h = 0; h < slots; h++)
            {
                float x = x0 + h * (Slot + Gap);
                Sprite(h == inv.SelectedHotbar ? "ui_slot_selected" : "ui_slot", x, y);
                ItemStack st = inv.GetHotbar(h);
                if (!st.IsEmpty)
                {
                    ItemInSlot(inv, st, x, y);
                }
                Text(x + 2f, y, 8f, 8f, (h + 1).ToString(), PrototypePalette.Pale, _small);
            }
            ItemStack selected = inv.SelectedHotbar >= 0 ? inv.GetHotbar(inv.SelectedHotbar) : default;
            if (!selected.IsEmpty)
            {
                Text(x0, y - 12f, total, 11f, inv.Catalog[selected.Item].name, PrototypePalette.White, _smallCenter);
            }
        }

        /// <summary>Over the hotbar while the hand holds a material: the piece and its cost, paint colour, turn and the ghost's hint.</summary>
        private void DrawBuildPanel(in HudModel m)
        {
            float total = HotbarCount * Slot + (HotbarCount - 1) * Gap;
            float panelW = Math.Max(total, 260f);
            float px = (Width - panelW) / 2f;
            float y = Height - Slot - 4f;
            Panel(px, y - 56f, panelW, 40f);
            Text(px + 4f, y - 54f, panelW - 8f, 11f, m.BuildTitle ?? "", PrototypePalette.Yellow, _smallCenter);
            int color = m.Build.Cursor.Color;
            Color previous = GUI.color;
            GUI.color = color == 0 ? PrototypePalette.Pale : (Color)PrototypePlaceholders.Paints[Mathf.Clamp(color, 0, PrototypePlaceholders.Paints.Length - 1)];
            Fill(px + 6f, y - 41f, 8f, 8f, GUI.color);
            GUI.color = previous;
            Text(px + 18f, y - 42f, panelW - 24f, 11f,
                $"Color {(color == 0 ? "natural" : color.ToString())} · Giro {m.Build.Cursor.Rotation * 90}°", PrototypePalette.Pale, _small);
            Text(px + 4f, y - 29f, panelW - 8f, 11f, m.BuildHint ?? "", PrototypePalette.Pale, _smallCenter);
        }

        private void DrawBag(Inventory inv)
        {
            Grid(4f, inv, inv.BagSlots, $"Mochila {inv.UsedSlots}/{inv.BagSlots}", false);
        }

        private void DrawContainer(Inventory box)
        {
            int capacity = Math.Max(BagColumns, (box.Bag.Count + BagColumns - 1) / BagColumns * BagColumns);
            float w = GridWidth();
            string title = box.Bag.Count == 0 ? "Contenedor (vacío)" : "Contenedor · T para coger";
            Grid(Width - w - 4f, box, capacity, title, true);
        }

        private float GridWidth() => 8f + BagColumns * Slot + (BagColumns - 1) * Gap;

        private void Grid(float x, Inventory inv, int capacity, string title, bool highlight)
        {
            int rows = Math.Max(1, (capacity + BagColumns - 1) / BagColumns);
            float w = GridWidth();
            float h = 4f + 12f + rows * (Slot + Gap) - Gap + 4f;
            float y = Height - h - 4f;
            Panel(x, y, w, h);
            Text(x + 4f, y + 3f, w - 8f, 11f, title, highlight ? PrototypePalette.Yellow : PrototypePalette.White, _small);
            for (int i = 0; i < capacity; i++)
            {
                float sx = x + 4f + (i % BagColumns) * (Slot + Gap);
                float sy = y + 16f + (i / BagColumns) * (Slot + Gap);
                Sprite("ui_slot", sx, sy);
                if (i < inv.Bag.Count)
                {
                    ItemInSlot(inv, inv.Bag[i], sx, sy);
                }
            }
        }

        private void ItemInSlot(Inventory inv, ItemStack st, float x, float y)
        {
            ItemDefinition def = inv.Catalog[st.Item];
            Sprite("item_" + def.id, x + 2f, y + 2f);
            if (def.HasDurability && st.Durability > 0)
            {
                float f = Mathf.Clamp01(st.Durability / (float)def.durability);
                Fill(x + 3f, y + 16f, 14f, 2f, PrototypePalette.Black);
                Fill(x + 3f, y + 16f, 14f * f, 2f, f > 0.5f ? PrototypePalette.Lime : f > 0.25f ? PrototypePalette.Yellow : PrototypePalette.Red);
            }
            if (st.Count > 1)
            {
                Text(x, y + 9f, Slot - 2f, 10f, st.Count.ToString(), PrototypePalette.White, _smallRight);
            }
        }

        // A line of text over the world, no box, fading out at the end: it should not hold the eye (D-024).
        private void DrawMessage(string message, float alpha)
        {
            if (string.IsNullOrEmpty(message) || alpha <= 0f)
            {
                return;
            }
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            float w = Math.Min(360f, Width - 240f);
            Text((Width - w) / 2f, 10f, w, 11f, message, PrototypePalette.Yellow, _smallCenter);
            GUI.color = previous;
        }

        private void DrawDiary(string diary, float alpha)
        {
            if (string.IsNullOrEmpty(diary) || alpha <= 0f)
            {
                return;
            }
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            float w = Math.Min(260f, Width - 240f);
            float x = (Width - w) / 2f;
            float y = Height - 118f;
            Panel(x, y, w, 56f);
            Text(x + 5f, y + 3f, w - 10f, 11f, "Diario", PrototypePalette.Tan, _text);
            Text(x + 5f, y + 15f, w - 10f, 40f, diary, PrototypePalette.White, _wrap);
            GUI.color = previous;
        }

        private void DrawHelp(bool show, int bagSlots)
        {
            if (!show)
            {
                Text(6f, Height - 14f, 220f, 11f, "Tab: mochila · H: controles", PrototypePalette.Pale, _small);
                return;
            }
            float w = Width - 8f;
            float h = 10f + Controls.Length * 12f;
            float x = (Width - w) / 2f;
            float y = (Height - h) / 2f;
            Panel(x, y, w, h);
            for (int i = 0; i < Controls.Length; i++)
            {
                Text(x + 6f, y + 5f + i * 12f, w - 12f, 11f, Controls[i], PrototypePalette.White, _small);
            }
        }

        // ---------------------------------------------------------- primitives

        private void Sprite(string name, float x, float y)
        {
            if (!_art.TryGet(name, out _))
            {
                return;
            }
            Vector2 size = _art.Size(name);
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(Px(x, y, size.x, size.y), _art.Texture, _art.Uv(name));
        }

        private void Fill(float x, float y, float w, float h, Color color)
        {
            if (w <= 0f || h <= 0f)
            {
                return;
            }
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(Px(x, y, w, h), _art.Texture, _art.Uv("ui_white"));
        }

        /// <summary>9-slice panel from ui_panel: the corners keep their size, the rest stretches.</summary>
        private void Panel(float x, float y, float w, float h)
        {
            Rect uv = _art.Uv("ui_panel");
            float bu = uv.width * PanelBorder / PanelSize;
            float bv = uv.height * PanelBorder / PanelSize;
            float[] xs = { x, x + PanelBorder, x + w - PanelBorder, x + w };
            float[] ys = { y, y + PanelBorder, y + h - PanelBorder, y + h };
            float[] us = { uv.x, uv.x + bu, uv.x + uv.width - bu, uv.x + uv.width };
            float[] vs = { uv.y + uv.height, uv.y + uv.height - bv, uv.y + bv, uv.y }; // texture v grows upwards
            GUI.color = Color.white;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    GUI.DrawTextureWithTexCoords(
                        Px(xs[i], ys[j], xs[i + 1] - xs[i], ys[j + 1] - ys[j]),
                        _art.Texture,
                        new Rect(us[i], vs[j + 1], us[i + 1] - us[i], vs[j] - vs[j + 1]));
                }
            }
        }

        /// <summary>Text with a one-pixel drop shadow, so it reads over any background.</summary>
        private void Text(float x, float y, float w, float h, string text, Color color, GUIStyle style)
        {
            Rect r = Px(x, y, w, h);
            float o = Math.Max(1, _s / 2);
            GUI.color = PrototypePalette.Black;
            GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, style);
            GUI.color = color;
            GUI.Label(r, text, style);
        }

        private Rect Px(float x, float y, float w, float h) => new Rect(x * _s, y * _s, w * _s, h * _s);

        private void BuildStyles()
        {
            _styleScale = _s;
            // Public Pixel (CC0) is drawn on an 8 px grid: sizes that are whole multiples of 8 keep it crisp.
            Font font = Resources.Load<Font>("Cepa/PublicPixel");
            int size = font != null ? 8 * ((_s + 1) / 2) : 0;          // long text: half the UI scale, still a whole multiple of 8
            int big = font != null ? 8 * _s : 0;                        // short numbers: full size
            _text = Style(font, size > 0 ? size : Math.Max(11, 7 * _s), TextAnchor.MiddleLeft, false);
            _small = Style(font, size > 0 ? size : Math.Max(10, 6 * _s), TextAnchor.MiddleLeft, false);
            _smallRight = Style(font, big > 0 ? big : Math.Max(10, 6 * _s), TextAnchor.LowerRight, false);
            _number = Style(font, big > 0 ? big : Math.Max(10, 6 * _s), TextAnchor.MiddleLeft, false);
            _smallCenter = Style(font, size > 0 ? size : Math.Max(10, 6 * _s), TextAnchor.MiddleCenter, false);
            _wrap = Style(font, size > 0 ? size : Math.Max(10, 6 * _s), TextAnchor.UpperLeft, true);
        }

        private static GUIStyle Style(Font font, int size, TextAnchor anchor, bool wrap)
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = size,
                alignment = anchor,
                wordWrap = wrap,
                richText = false,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            style.normal.textColor = Color.white;
            return style;
        }

        private static string PhaseIcon(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Day: return "icon_day";
                case DayPhase.Night: return "icon_night";
                default: return "icon_dusk";
            }
        }

        private static Color PhaseColor(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Day: return PrototypePalette.Yellow;
                case DayPhase.Dusk: return PrototypePalette.Orange;
                case DayPhase.Night: return PrototypePalette.Red;
                default: return PrototypePalette.Tan;
            }
        }

        private static string PhaseName(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Day: return "Día";
                case DayPhase.Dusk: return "Atardecer";
                case DayPhase.Night: return "Noche";
                default: return "Amanecer";
            }
        }
    }
}
