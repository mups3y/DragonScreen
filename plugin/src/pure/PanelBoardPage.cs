/*
 * DragonScreen - PanelBoardPage
 *
 * PURE, AND PREVIEW-ONLY. Draws the lower console - six plates, thirty-eight buttons, one dash each -
 * so that what a press does to the lighting can be looked at with the game closed.
 *
 * ---- THIS IS NOT A SCREEN, AND IT MUST NEVER BECOME ONE ----
 * There is no `UiPage` for it, nothing navigates to it, and `ScreenPainter` never builds it. The
 * console is PHYSICAL: its buttons are meshes on Tundra's IVA prop and its indicators are Tundra's
 * dashes, driven through a material tint by `PanelButtons`. Putting a drawing of the console onto a
 * touchscreen would be inventing a page the real capsule does not have.
 *
 * What it is for is the one thing the panel could not otherwise have: a way to check the LIGHTING
 * without spending a restart. Restarts are the scarce resource (CLAUDE.md), the panel's whole
 * visual language is one mark per button, and "after this sequence of presses, which dashes are
 * bright?" is a question a PNG answers completely and honestly.
 *
 * ---- THE LAYOUT COMES FROM THE TRANSFORM DUMP, NOT FROM A SCREENSHOT ----
 * Plate centres and widths are the prop-space numbers in docs/REAL_DRAGON_SCREENS.md, mapped
 * linearly to the canvas - so the plates sit in the real order at the real relative spacing,
 * including the two blank fillers and the narrow one. Within a plate, BUT1..BUT5 is the top row left
 * to right and BUT6..BUT10 the bottom, which is the model's own naming rule. Nothing here is
 * eyeballed from an image; that is how every wrong page in this project got made.
 *
 * ---- WHAT IT DELIBERATELY ADDS ----
 * Two things the real console does not show, because this is a diagnostic and not a reconstruction:
 * an INERT control's label is drawn dim (§14.4(b) is invisible otherwise - an inert button and a
 * dark working button look identical), and the button just pressed gets an outline. Both are called
 * out in the legend on the image so no future session can mistake either for a claim about the real
 * panel.
 */
namespace DragonScreen
{
    public static class PanelBoardPage
    {
        /// <summary>Worst case: 38 cells at ~9 commands, 8 plates, the handle, the caption block.</summary>
        public const int Commands = 520;

        // ---- prop-space geometry, docs/REAL_DRAGON_SCREENS.md 2026-08-05 ----
        // Eight plates, x = the plate's centre in prop space. Plate 7 is the narrow blank filler.
        private static readonly string[] PlateOrder =
        {
            PanelMap.PlateLeftEmerg,   // -0.376  LEFT emergencies
            PanelMap.PlatePower,       // -0.267  power + strings
            PanelMap.PlateChutes,      // -0.157  chutes + pyros
            null,                      // -0.077  blank filler, narrow
            PanelMap.PlateAbort,       //  0.000  ABORT handle
            PanelMap.PlateEntry,       // +0.153  entry mode
            null,                      // +0.262  blank filler
            PanelMap.PlateRightEmerg   // +0.373  RIGHT emergencies (mirror of the left)
        };
        private static readonly float[] PlateX =
            { -0.376f, -0.267f, -0.157f, -0.077f, 0.000f, 0.153f, 0.262f, 0.373f };
        private static readonly float[] PlateW =
            { 0.0975f, 0.0975f, 0.0975f, 0.0396f, 0.0975f, 0.0975f, 0.0975f, 0.0975f };

        private static readonly string[] PlateTitle =
        {
            "EMERGENCY (L)", "POWER + STRINGS", "CHUTES + PYROS", "",
            "ABORT", "ENTRY MODE", "", "EMERGENCY (R)"
        };

        /// <summary>Left edge of the leftmost plate and right edge of the rightmost, in prop space.</summary>
        private const float SpanMin = -0.376f - 0.04875f;
        private const float SpanMax = 0.373f + 0.04875f;

        /// <summary>
        /// Draw the board.
        /// </summary>
        /// <param name="board">Whose lamps are drawn. Never mutated here.</param>
        /// <param name="title">Pre-built scene caption, e.g. "ARMED - DEORBIT NOW (left plate)".</param>
        /// <param name="note">Pre-built second line: what the press did, in words.</param>
        /// <param name="pressed">Index into PanelMap.All to outline, or -1.</param>
        public static void Build(DisplayList dl, int w, int h, PanelBoard board,
                                 string title, string note, int pressed)
        {
            if (dl == null || board == null || w <= 0 || h <= 0) return;

            dl.Rect(0f, 0f, w, h, DragonPalette.Background);

            float margin = w * 0.018f;
            float headerH = 96f;
            float legendH = 78f;

            dl.Text(title, margin, 22f, Typography.Value, TextAlign.Left, DragonPalette.Text0);
            dl.Text(note, margin, 22f + Typography.Value + 8f, Typography.Body,
                    TextAlign.Left, DragonPalette.Text4);

            float boardY = headerH;
            float boardH = h - headerH - legendH;
            float scale = (w - margin * 2f) / (SpanMax - SpanMin);

            PanelEntry[] all = PanelMap.All;

            for (int p = 0; p < PlateOrder.Length; p++)
            {
                float px = margin + (PlateX[p] - PlateW[p] * 0.5f - SpanMin) * scale;
                float pw = PlateW[p] * scale;

                dl.Rect(px, boardY, pw, boardH, DragonPalette.Panel);
                dl.Box(px, boardY, pw, boardH, 2f, DragonPalette.Hairline);

                if (PlateOrder[p] == null) continue;      // a blank filler plate has no children

                dl.Text(PlateTitle[p], px + pw * 0.5f, boardY + 10f, Typography.Dense,
                        TextAlign.Centre, DragonPalette.Text6);

                // The abort plate holds a pull-twist HANDLE, not a row of switches, and the handle is
                // not in the map - PanelButtons wires it separately. Drawn as what it is.
                if (PlateOrder[p] == PanelMap.PlateAbort)
                {
                    float hx = px + pw * 0.28f, hw = pw * 0.44f;
                    float hy = boardY + boardH * 0.34f, hh = boardH * 0.30f;
                    dl.Rect(hx, hy, hw, hh, DragonPalette.Inset1);
                    dl.Box(hx, hy, hw, hh, 2f, DragonPalette.Text7);
                    dl.Text("EJECT", px + pw * 0.5f, hy + hh + 12f, Typography.Caption,
                            TextAlign.Centre, DragonPalette.Text3);
                    dl.Text("PULL + TWIST", px + pw * 0.5f, hy + hh + 12f + Typography.Caption + 4f,
                            Typography.Dense, TextAlign.Centre, DragonPalette.Text7);
                    continue;
                }

                float rowTop = boardY + 34f;
                float rowH = (boardH - 44f) * 0.5f;
                float colW = pw / 5f;

                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].Plate != PlateOrder[p]) continue;

                    int n = ButtonNumber(all[i].Button);
                    if (n < 1 || n > 10) continue;
                    int row = (n <= 5) ? 0 : 1;
                    int col = (n <= 5) ? (n - 1) : (n - 6);

                    Cell(dl, px + col * colW, rowTop + row * rowH, colW, rowH,
                         all[i], board.Lamp(i), i == pressed);
                }
            }

            Legend(dl, w, h, legendH, margin);
        }

        /// <summary>One button: its dash, its label, and the outline if it was the one pressed.</summary>
        private static void Cell(DisplayList dl, float x, float y, float w, float h,
                                 PanelEntry e, PanelLight lamp, bool outline)
        {
            float pad = w * 0.09f;
            float cx = x + w * 0.5f;

            dl.Rect(x + pad, y + pad, w - pad * 2f, h - pad * 2f, DragonPalette.Inset2);

            // ---- THE DASH ----
            // Every button on the real console carries one small horizontal mark above its label, and
            // that mark is the panel's entire visual language for state. Two states only: as modelled,
            // or BRIGHT. There is no red (§14.4(a)).
            float dashW = (w - pad * 2f) * 0.46f;
            float dashH = 6f;
            float dashY = y + pad + h * 0.10f;
            dl.Rect(cx - dashW * 0.5f, dashY, dashW, dashH,
                    lamp == PanelLight.Lit ? DragonPalette.White : DragonPalette.Text8);

            bool inert = PanelPolicy.IsInert(e.Command);
            Rgba ink = inert ? DragonPalette.Text7 : DragonPalette.Text2;

            float ly = dashY + dashH + h * 0.10f;
            string[] lines = Wrap(e.Label);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] == null) break;
                dl.Text(lines[i], cx, ly + i * (Typography.Dense + 2f), Typography.Dense,
                        TextAlign.Centre, ink);
            }

            if (outline) dl.Box(x + pad * 0.4f, y + pad * 0.4f, w - pad * 0.8f, h - pad * 0.8f,
                                2f, DragonPalette.Accent);
        }

        private static void Legend(DisplayList dl, int w, int h, float legendH, float margin)
        {
            float y = h - legendH + 10f;
            dl.Rect(0f, h - legendH, w, 2f, DragonPalette.Hairline);

            float dy = y + 6f;
            dl.Rect(margin, dy, 34f, 6f, DragonPalette.White);
            dl.Text("BRIGHT = active / armed / fired", margin + 46f, dy - 5f, Typography.Caption,
                    TextAlign.Left, DragonPalette.Text3);

            float x2 = margin + 420f;
            dl.Rect(x2, dy, 34f, 6f, DragonPalette.Text8);
            dl.Text("as modelled = everything else. NO red state (§14.4a).",
                    x2 + 46f, dy - 5f, Typography.Caption, TextAlign.Left, DragonPalette.Text3);

            dl.Text("PREVIEW-ONLY DIAGNOSTIC, NOT A SCREEN: a dim label marks a control left INERT "
                    + "until a real source verifies it (§14.4b); the cyan outline is the button "
                    + "just pressed. Neither mark exists on the real console.",
                    margin, dy + Typography.Caption + 12f, Typography.Dense,
                    TextAlign.Left, DragonPalette.Text6);
        }

        /// <summary>"BUT7" -> 7. Returns -1 for anything that is not a numbered button.</summary>
        private static int ButtonNumber(string button)
        {
            if (string.IsNullOrEmpty(button) || button.Length < 4) return -1;
            if (button[0] != 'B' || button[1] != 'U' || button[2] != 'T') return -1;
            int n = 0;
            for (int i = 3; i < button.Length; i++)
            {
                char c = button[i];
                if (c < '0' || c > '9') return -1;
                n = n * 10 + (c - '0');
            }
            return n;
        }

        /// <summary>
        /// Greedy word wrap to three lines at a fixed character budget.
        ///
        /// A character budget and not a measurement, because a pure page has no font - see
        /// DisplayList.Text. The longest label on the console is "ENABLE BACKUP PYROS" and every
        /// label is a short upper-case phrase, so counting characters is enough here and a
        /// measurement interface would be machinery for one diagnostic.
        /// </summary>
        private static string[] Wrap(string label)
        {
            string[] outLines = new string[3];
            if (string.IsNullOrEmpty(label)) return outLines;

            const int Budget = 9;                 // "STRING 1A" and "FIRE PYRD" fit on one line

            string[] words = label.Split(' ');
            int line = 0;
            string cur = "";
            for (int i = 0; i < words.Length; i++)
            {
                string next = (cur.Length == 0) ? words[i] : cur + " " + words[i];
                // The LAST line takes whatever is left rather than dropping it. A label silently
                // truncated in a diagnostic is worse than one drawn a little wide, because the wide
                // one is visible and the truncated one is a wrong answer that looks right.
                if (cur.Length > 0 && next.Length > Budget && line < outLines.Length - 1)
                {
                    outLines[line++] = cur;
                    cur = words[i];
                }
                else cur = next;
            }
            outLines[line] = cur;
            return outLines;
        }
    }
}
