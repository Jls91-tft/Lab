using System;
using CepaCero.Items;
using CepaCero.Rules;
using UnityEngine;

namespace CepaCero.Unity.Prototype
{
    /// <summary>
    /// Prototype HUD drawn in OnGUI, styled as a survivor's notebook (D-036): cards of kraft paper held on with tape, ink
    /// outlines, round badges for the status icons, a sky dial with the Pilar on the horizon for the clock, nights counted in
    /// tally marks, the hotbar as paper tags and the diary as a page torn from a notebook. Laid out in art pixels and scaled by
    /// the same whole number as the world, so it stays crisp. Prototype only: its texts are not localised yet (the real UI will
    /// read data/generated/loc/strings.csv).
    /// </summary>
    public sealed class PrototypeHud
    {
        private const int Slot = 22;
        private const int Gap = 3;
        private const int BagColumns = 5;
        private const int HotbarCount = 9;

        // The notebook's colours: kraft card, ink, cream paper, grey tape (all from the interface palette, tools/artgen).
        private static readonly Color Kraft = PrototypePalette.Tan;
        private static readonly Color KraftLight = new Color(0.84f, 0.70f, 0.50f);
        private static readonly Color KraftDark = new Color(143f / 255f, 99f / 255f, 64f / 255f);
        private static readonly Color Ink = PrototypePalette.Ink;
        private static readonly Color Paper = PrototypePalette.White;
        private static readonly Color PaperShade = PrototypePalette.Pale;
        private static readonly Color Tape = new Color(0.90f, 0.89f, 0.84f, 0.78f);
        private static readonly Color DropShadow = new Color(0.05f, 0.05f, 0.06f, 0.45f);
        private static readonly Color RuleBlue = new Color(0.38f, 0.59f, 0.82f, 0.45f);

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
        private GUIStyle _numberRight;
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
                Label(Width / 2f, 8f, m.Level > 0 ? $"Planta {m.Level}" : $"Subsuelo {-m.Level}");
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

        // ------------------------------------------------------------- vitals

        private void DrawVitals(in HudModel m)
        {
            bool survival = m.Food >= 0;
            bool status = m.Me.IsDead || m.Me.IsDowned;
            float x = 8f;
            float y = 8f;
            float w = 136f;
            float h = survival ? 56f : 40f;
            Card(x, y, w, h, -3f, 4f);

            float health = m.Me.Health / (float)Math.Max(1, m.MaxHealth);
            Badge("icon_health", x + 5f, y + 5f);
            Pill(x + 22f, y + 8f, 82f, 9f, health, health <= 0.3f ? PrototypePalette.Red : new Color(0.84f, 0.25f, 0.27f), health <= 0.3f);
            Write(x + 108f, y + 6f, 26f, 12f, m.Me.Health.ToString(), _number);

            float stamina = m.Me.Stamina / Math.Max(1f, m.MaxStamina);
            Badge("icon_stamina", x + 5f, y + 22f);
            Pill(x + 22f, y + 26f, 82f, 7f, stamina, PrototypePalette.Yellow, false);
            Write(x + 108f, y + 23f, 26f, 12f, ((int)m.Me.Stamina).ToString(), _number);

            if (survival)
            {
                float food = m.Food / 100f;
                float water = m.Water / 100f;
                Sprite("icon_food", x + 8f, y + 40f);
                Pill(x + 21f, y + 42f, 40f, 6f, food, m.Food <= 25 ? PrototypePalette.Red : PrototypePalette.Orange, m.Food <= 25);
                Sprite("icon_water", x + 70f, y + 40f);
                Pill(x + 83f, y + 42f, 40f, 6f, water, m.Water <= 25 ? PrototypePalette.Red : PrototypePalette.LightBlue, m.Water <= 25);
            }
            if (status)
            {
                Stamp(x + w / 2f, y + h + 12f, m.Me.IsDead ? "MUERTO · al amanecer" : "CAÍDO · que te levanten", m.Me.IsDead ? PrototypePalette.Red : PrototypePalette.Orange);
            }
        }

        /// <summary>A rounded bar with an ink outline, a light top edge and tick marks every few pixels; it throbs when low.</summary>
        private void Pill(float x, float y, float w, float h, float fraction, Color color, bool warn)
        {
            fraction = Mathf.Clamp01(fraction);
            Fill(x + 1f, y, w - 2f, h, Ink);
            Fill(x, y + 1f, w, h - 2f, Ink);
            Fill(x + 1f, y + 1f, w - 2f, h - 2f, PrototypePalette.Shadow);
            if (warn)
            {
                color = Color.Lerp(color, Color.white, 0.35f * Mathf.PingPong(Time.time * 2.5f, 1f));
            }
            float fw = Mathf.Round((w - 2f) * fraction);
            if (fw > 0f)
            {
                Fill(x + 1f, y + 1f, fw, h - 2f, color);
                Fill(x + 1f, y + 1f, fw, 1f, Color.Lerp(color, Color.white, 0.45f));
                Fill(x + 1f, y + h - 2f, fw, 1f, Color.Lerp(color, Color.black, 0.3f));
            }
            for (float t = x + 9f; t < x + w - 2f; t += 8f)
            {
                Fill(t, y + 1f, 1f, h - 2f, new Color(0f, 0f, 0f, 0.22f));
            }
        }

        // ------------------------------------------------------------- clock

        private void DrawClock(in HudModel m)
        {
            float w = 104f;
            float x = Width - w - 8f;
            float y = 8f;
            bool nights = m.NightsSurvived >= 0;
            float h = nights ? 58f : 48f;
            Card(x, y, w, h, 3f, -4f);

            bool night = m.Phase == DayPhase.Night;
            Sprite(night ? "ui_dial_night" : "ui_dial_day", x + 5f, y + 5f);
            // The sun (or moon) rises on the left of the dial and sets on the right as the phase goes by.
            float a = Mathf.PI * (1f - Mathf.Clamp01(m.PhaseProgress));
            float cx = x + 5f + 21.5f + Mathf.Cos(a) * 15f;
            float cy = y + 5f + 20f - Mathf.Sin(a) * 13f;
            Sprite(PhaseIcon(m.Phase), Mathf.Round(cx - 5.5f), Mathf.Round(cy - 5.5f));

            Write(x + 54f, y + 8f, 20f, 9f, "Día", _small);
            Write(x + 74f, y + 5f, 28f, 13f, m.Day.ToString(), _number);
            Write(x + 54f, y + 20f, 48f, 9f, PhaseName(m.Phase), _small, PhaseColor(m.Phase));

            float row = y + 33f;
            Sprite("icon_zombie", x + 5f, row);
            Write(x + 18f, row + 1f, 30f, 10f, m.ZombiesShown.ToString(), _number);
            if (m.ZombiesKilled >= 0)
            {
                Sprite("icon_skull", x + 52f, row);
                Write(x + 65f, row + 1f, 36f, 10f, m.ZombiesKilled.ToString(), _number);
            }
            if (nights)
            {
                Write(x + 5f, y + 47f, 40f, 8f, "Noches", _small);
                Tally(x + 42f, y + 46f, m.NightsSurvived, w - 48f);
            }
            if (!string.IsNullOrEmpty(m.Network))
            {
                Shadowed(Width - 248f, y + h + 6f, 240f, 11f, m.Network, PrototypePalette.Cyan, _smallRight);
            }
        }

        /// <summary>Nights survived as pencil marks: four strokes and a fifth across them, as on a cell wall.</summary>
        private void Tally(float x, float y, int count, float room)
        {
            if (count <= 0)
            {
                Write(x, y + 1f, room, 8f, "—", _small);
                return;
            }
            int groups = count / 5;
            int maxGroups = Math.Max(1, (int)(room / 14f));
            if (groups > maxGroups)
            {
                Write(x, y + 1f, room, 8f, $"× {count}", _small);
                return;
            }
            float px = x;
            for (int i = 0; i < count; i++)
            {
                int k = i % 5;
                if (k == 4)
                {
                    for (int d = 0; d < 12; d++)
                    {
                        Fill(px - 12f + d, y + 6f - d / 2f, 1f, 1f, PrototypePalette.Red);       // the stroke across, in red pencil
                    }
                    px += 4f;
                    continue;
                }
                Fill(px, y + (k % 2), 1f, 7f, Ink);
                px += 3f;
            }
        }

        // ------------------------------------------------------------- hotbar and bags

        private void DrawHotbar(Inventory inv)
        {
            int slots = Math.Min(HotbarCount, inv.HotbarSlots);
            float total = slots * Slot + (slots - 1) * Gap;
            float x0 = (Width - total) / 2f;
            float y = Height - Slot - 8f;
            // A strip of kraft behind the tags, taped at both ends.
            Card(x0 - 6f, y - 3f, total + 12f, Slot + 7f, -7f, 7f);
            for (int h = 0; h < slots; h++)
            {
                bool selected = h == inv.SelectedHotbar;
                float x = x0 + h * (Slot + Gap);
                float sy = selected ? y - 3f : y;                           // the tag in your hand stands out a little
                Tag(x, sy, selected);
                ItemStack st = inv.GetHotbar(h);
                if (!st.IsEmpty)
                {
                    ItemInSlot(inv, st, x + 1f, sy + 1f);
                }
                Write(x + 2f, sy + 1f, 8f, 8f, (h + 1).ToString(), _small, PrototypePalette.Shadow);
            }
            ItemStack held = inv.SelectedHotbar >= 0 ? inv.GetHotbar(inv.SelectedHotbar) : default;
            if (!held.IsEmpty)
            {
                Label(Width / 2f, y - 16f, inv.Catalog[held.Item].name);
            }
        }

        /// <summary>A paper tag for one slot: cream, ink outline, a shaded foot; the one in your hand has a thick yellow rim.</summary>
        private void Tag(float x, float y, bool selected)
        {
            Fill(x + 2f, y + 2f, Slot - 1f, Slot - 1f, DropShadow);
            Color edge = selected ? PrototypePalette.Yellow : Ink;
            Fill(x + 1f, y, Slot - 2f, Slot, edge);
            Fill(x, y + 1f, Slot, Slot - 2f, edge);
            float inset = selected ? 2f : 1f;
            Fill(x + inset, y + inset, Slot - inset * 2f, Slot - inset * 2f, selected ? Paper : new Color(0.86f, 0.83f, 0.76f));
            Fill(x + inset, y + Slot - inset - 3f, Slot - inset * 2f, 3f, PaperShade);
            if (selected)
            {
                Fill(x + 1f, y + 1f, Slot - 2f, 1f, Ink);
                Fill(x + 1f, y + Slot - 2f, Slot - 2f, 1f, Ink);
                Fill(x + 1f, y + 1f, 1f, Slot - 2f, Ink);
                Fill(x + Slot - 2f, y + 1f, 1f, Slot - 2f, Ink);
            }
        }

        /// <summary>Over the hotbar while the hand holds a material: the piece and its cost, paint colour, turn and the ghost's hint.</summary>
        private void DrawBuildPanel(in HudModel m)
        {
            float panelW = 348f;                                          // wide enough for the longest hint at the pixel font
            float px = (Width - panelW) / 2f;
            float y = Height - Slot - 8f - 62f;
            Card(px, y, panelW, 40f, -4f, 4f);
            Write(px + 6f, y + 4f, panelW - 12f, 10f, m.BuildTitle ?? "", _smallCenter, PrototypePalette.Red);
            int color = m.Build.Cursor.Color;
            Color swatch = color == 0 ? PrototypePalette.Pale : (Color)PrototypePlaceholders.Paints[Mathf.Clamp(color, 0, PrototypePlaceholders.Paints.Length - 1)];
            Fill(px + 7f, y + 16f, 10f, 10f, Ink);
            Fill(px + 8f, y + 17f, 8f, 8f, swatch);
            Write(px + 21f, y + 17f, panelW - 28f, 9f, $"Color {(color == 0 ? "natural" : color.ToString())} · Giro {m.Build.Cursor.Rotation * 90}°", _small);
            Write(px + 6f, y + 28f, panelW - 12f, 9f, m.BuildHint ?? "", _smallCenter, PrototypePalette.Shadow);
        }

        private void DrawBag(Inventory inv)
        {
            Grid(8f, inv, inv.BagSlots, $"Mochila {inv.UsedSlots}/{inv.BagSlots}", false);
        }

        private void DrawContainer(Inventory box)
        {
            int capacity = Math.Max(BagColumns, (box.Bag.Count + BagColumns - 1) / BagColumns * BagColumns);
            float w = GridWidth();
            string title = box.Bag.Count == 0 ? "Vacío" : "T: coger";
            Grid(Width - w - 8f, box, capacity, title, true);
        }

        private float GridWidth() => 12f + BagColumns * Slot + (BagColumns - 1) * Gap;

        private void Grid(float x, Inventory inv, int capacity, string title, bool highlight)
        {
            int rows = Math.Max(1, (capacity + BagColumns - 1) / BagColumns);
            float w = GridWidth();
            float h = 6f + 13f + rows * (Slot + Gap) - Gap + 7f;
            float y = Height - Slot - 8f - 16f - h;
            Card(x, y, w, h, highlight ? 4f : -4f, highlight ? -3f : 3f);
            Write(x + 6f, y + 6f, w - 12f, 9f, title, _small, highlight ? PrototypePalette.Red : Ink);
            for (int i = 0; i < capacity; i++)
            {
                float sx = x + 6f + (i % BagColumns) * (Slot + Gap);
                float sy = y + 19f + (i / BagColumns) * (Slot + Gap);
                Tag(sx, sy, false);
                if (i < inv.Bag.Count)
                {
                    ItemInSlot(inv, inv.Bag[i], sx + 1f, sy + 1f);
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
                Fill(x + 3f, y + 17f, 14f, 2f, Ink);
                Fill(x + 3f, y + 17f, Mathf.Round(14f * f), 2f, f > 0.5f ? PrototypePalette.Lime : f > 0.25f ? PrototypePalette.Yellow : PrototypePalette.Red);
            }
            if (st.Count > 1)
            {
                Outlined(x, y + 9f, Slot - 3f, 11f, st.Count.ToString(), _numberRight);
            }
        }

        // ------------------------------------------------------------- messages, diary, help

        // A strip of tape across the top of the screen with the message written on it, fading out at the end (D-024).
        private void DrawMessage(string message, float alpha)
        {
            if (string.IsNullOrEmpty(message) || alpha <= 0f)
            {
                return;
            }
            float w = Math.Min(330f, Width - 280f);
            float x = (Width - w) / 2f;
            float y = 10f;
            Color fade = new Color(1f, 1f, 1f, alpha);
            Fill(x, y, w, 14f, Mul(Tape, fade));
            for (float t = x; t < x + w; t += 4f)
            {
                Fill(t, y + 14f, 2f, 1f, Mul(Tape, fade));                // a torn edge
            }
            Write(x, y + 3f, w, 9f, message, _smallCenter, Mul(Ink, fade));
        }

        /// <summary>A page torn from a notebook, taped on a slant: ruled lines, a red margin, the heading in red ink.</summary>
        private void DrawDiary(string diary, float alpha)
        {
            if (string.IsNullOrEmpty(diary) || alpha <= 0f)
            {
                return;
            }
            string heading = "Diario";
            string body = diary;
            int open = diary.IndexOf("<i>", StringComparison.Ordinal);
            int close = diary.IndexOf("</i>", StringComparison.Ordinal);
            if (open >= 0 && close > open)
            {
                heading = diary.Substring(open + 3, close - open - 3);
                body = (diary.Substring(0, open) + diary.Substring(close + 4)).Trim();
            }
            Color fade = new Color(1f, 1f, 1f, alpha);
            float w = Math.Min(270f, Width - 260f);
            float h = 66f;
            float x = (Width - w) / 2f;
            float y = Height - Slot - 8f - 26f - h;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(-1.5f, new Vector2((x + w / 2f) * _s, (y + h / 2f) * _s));
            Fill(x + 3f, y + 3f, w, h, Mul(DropShadow, fade));
            Fill(x, y, w, h, Mul(Paper, fade));
            for (float ly = y + 17f; ly < y + h - 3f; ly += 8f)
            {
                Fill(x + 2f, ly, w - 4f, 1f, Mul(RuleBlue, fade));
            }
            Fill(x + 14f, y, 1f, h, Mul(new Color(0.8f, 0.3f, 0.3f, 0.6f), fade));
            for (float t = x; t < x + w; t += 3f)
            {
                Fill(t, y - 1f, 2f, 1f, Mul(Paper, fade));                 // the torn top edge
            }
            Write(x + 18f, y + 5f, w - 24f, 9f, heading, _small, Mul(PrototypePalette.Red, fade));
            Write(x + 18f, y + 18f, w - 24f, h - 20f, body, _wrap, Mul(Ink, fade));
            TapeStrip(x + w / 2f - 16f, y - 5f, 32f, 9f, 4f, alpha);
            GUI.matrix = saved;
        }

        private void DrawHelp(bool show, int bagSlots)
        {
            if (!show)
            {
                float x = 8f;
                float y = Height - 18f;
                x = Key(x, y, "Tab") + 3f;
                Shadowed(x, y + 1f, 60f, 10f, "mochila", PrototypePalette.White, _small);
                x += 62f;
                x = Key(x, y, "H") + 3f;
                Shadowed(x, y + 1f, 80f, 10f, "controles", PrototypePalette.White, _small);
                return;
            }
            float w = Width - 24f;
            float h = 16f + Controls.Length * 12f;
            float hx = (Width - w) / 2f;
            float hy = (Height - h) / 2f;
            Card(hx, hy, w, h, -3f, 3f);
            for (int i = 0; i < Controls.Length; i++)
            {
                Write(hx + 8f, hy + 8f + i * 12f, w - 16f, 11f, Controls[i], _small);
            }
        }

        /// <summary>A keyboard key drawn as a little cap; returns where the text after it can start.</summary>
        private float Key(float x, float y, string label)
        {
            float w = 6f + label.Length * 6f;
            Fill(x + 1f, y, w - 2f, 13f, Ink);
            Fill(x, y + 1f, w, 11f, Ink);
            Fill(x + 1f, y + 1f, w - 2f, 11f, PaperShade);
            Fill(x + 1f, y + 1f, w - 2f, 8f, Paper);
            Write(x, y + 1f, w, 8f, label, _smallCenter);
            return x + w;
        }

        /// <summary>A short text on a scrap of tape, centred on <paramref name="cx"/>.</summary>
        private void Label(float cx, float y, string text)
        {
            float w = Math.Min(Width - 40f, 14f + text.Length * 6f);
            float x = cx - w / 2f;
            Fill(x + 2f, y + 2f, w, 12f, DropShadow);
            Fill(x, y, w, 12f, Kraft);
            Fill(x, y, w, 1f, KraftLight);
            Fill(x, y + 11f, w, 1f, KraftDark);
            Write(x, y + 2f, w, 9f, text, _smallCenter);
        }

        /// <summary>A rubber stamp, tilted: red ink in a box, for being down or dead.</summary>
        private void Stamp(float cx, float cy, string text, Color color)
        {
            float w = 14f + text.Length * 6f;
            float h = 14f;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(-4f, new Vector2(cx * _s, cy * _s));
            float x = cx - w / 2f;
            float y = cy - h / 2f;
            Fill(x, y, w, h, new Color(0.93f, 0.9f, 0.82f, 0.85f));
            Fill(x, y, w, 2f, color);
            Fill(x, y + h - 2f, w, 2f, color);
            Fill(x, y, 2f, h, color);
            Fill(x + w - 2f, y, 2f, h, color);
            Write(x, y + 3f, w, 9f, text, _smallCenter, color);
            GUI.matrix = saved;
        }

        // ---------------------------------------------------------- primitives

        /// <summary>A card of kraft paper: a soft shadow, an ink outline with rounded corners, a light top and dark foot, and two pieces of tape.</summary>
        private void Card(float x, float y, float w, float h, float tapeLeft, float tapeRight)
        {
            Fill(x + 3f, y + 3f, w, h, DropShadow);
            Fill(x + 1f, y, w - 2f, h, Ink);
            Fill(x, y + 1f, w, h - 2f, Ink);
            Fill(x + 1f, y + 1f, w - 2f, h - 2f, Kraft);
            Fill(x + 2f, y + 1f, w - 4f, 1f, KraftLight);
            Fill(x + 2f, y + h - 3f, w - 4f, 1f, KraftDark);
            // Fibres in the card, always the same for the same card.
            uint seed = unchecked((uint)(x * 73f + y * 151f + w * 7f));
            for (int i = 0; i < (int)(w * h / 90f); i++)
            {
                seed = seed * 1664525u + 1013904223u;
                float fx = x + 3f + (seed >> 8) % (uint)Math.Max(1f, w - 6f);
                float fy = y + 3f + (seed >> 20) % (uint)Math.Max(1f, h - 6f);
                Fill(fx, fy, 2f, 1f, new Color(KraftDark.r, KraftDark.g, KraftDark.b, 0.45f));
            }
            TapeStrip(x - 5f, y - 3f, 18f, 8f, -30f + tapeLeft, 1f);
            TapeStrip(x + w - 13f, y - 3f, 18f, 8f, 30f + tapeRight, 1f);
        }

        /// <summary>A strip of grey tape at an angle, its ends cut in a zigzag.</summary>
        private void TapeStrip(float x, float y, float w, float h, float angle, float alpha)
        {
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, new Vector2((x + w / 2f) * _s, (y + h / 2f) * _s));
            Color c = new Color(Tape.r, Tape.g, Tape.b, Tape.a * alpha);
            Fill(x + 1f, y, w - 2f, h, c);
            for (float t = y; t < y + h; t += 2f)
            {
                Fill(x, t, 1f, 1f, c);
                Fill(x + w - 1f, t + 1f, 1f, 1f, c);
            }
            Fill(x + 1f, y + 1f, w - 2f, 1f, new Color(1f, 1f, 1f, 0.35f * alpha));
            GUI.matrix = saved;
        }

        private void Badge(string icon, float x, float y)
        {
            Sprite("ui_badge", x, y);
            Sprite(icon, x + 2f, y + 2f);
        }

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
            if (w <= 0f || h <= 0f || color.a <= 0f)
            {
                return;
            }
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(Px(x, y, w, h), _art.Texture, _art.Uv("ui_white"));
        }

        /// <summary>Ink on paper: no shadow.</summary>
        private void Write(float x, float y, float w, float h, string text, GUIStyle style, Color? color = null)
        {
            GUI.color = color ?? Ink;
            GUI.Label(Px(x, y, w, h), text, style);
        }

        /// <summary>Light text straight on the world, with a one-pixel drop shadow so it reads over anything.</summary>
        private void Shadowed(float x, float y, float w, float h, string text, Color color, GUIStyle style)
        {
            Rect r = Px(x, y, w, h);
            float o = Math.Max(1, _s / 2);
            GUI.color = PrototypePalette.Black;
            GUI.Label(new Rect(r.x + o, r.y + o, r.width, r.height), text, style);
            GUI.color = color;
            GUI.Label(r, text, style);
        }

        /// <summary>White figures with an ink outline all round: a stack's count over its icon.</summary>
        private void Outlined(float x, float y, float w, float h, string text, GUIStyle style)
        {
            Rect r = Px(x, y, w, h);
            float o = Math.Max(1, _s / 2);
            GUI.color = Ink;
            for (int i = 0; i < 4; i++)
            {
                GUI.Label(new Rect(r.x + (i == 0 ? o : i == 1 ? -o : 0f), r.y + (i == 2 ? o : i == 3 ? -o : 0f), r.width, r.height), text, style);
            }
            GUI.color = PrototypePalette.White;
            GUI.Label(r, text, style);
        }

        private static Color Mul(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);

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
            _smallRight = Style(font, size > 0 ? size : Math.Max(10, 6 * _s), TextAnchor.MiddleRight, false);
            _number = Style(font, big > 0 ? big : Math.Max(10, 6 * _s), TextAnchor.MiddleLeft, false);
            _numberRight = Style(font, big > 0 ? big : Math.Max(10, 6 * _s), TextAnchor.LowerRight, false);
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
                case DayPhase.Day: return new Color(0.55f, 0.36f, 0.05f);
                case DayPhase.Dusk: return new Color(0.70f, 0.30f, 0.08f);
                case DayPhase.Night: return PrototypePalette.Red;
                default: return new Color(0.45f, 0.30f, 0.45f);
            }
        }

        private static string PhaseName(DayPhase phase)
        {
            switch (phase)
            {
                case DayPhase.Day: return "Pleno día";
                case DayPhase.Dusk: return "Atardecer";
                case DayPhase.Night: return "Noche";
                default: return "Amanecer";
            }
        }
    }
}
