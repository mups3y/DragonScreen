// DragonScreen — MarginAffordance  (PURE: the letterbox-margin page link, ONE geometry)
// ============================================================================================
// The fit-to-height Figma pages leave a letterbox margin either side of the frame art. Two of them
// put a page link in that margin: the attitude HUD's "MANUAL DOCKING" (→ UiPage.Docking) and the
// manual-docking page's "RENDEZVOUS" (→ UiPage.Rendezvous). The FigmaUI comment on the second one
// already said they are the same thing — "same construction as the HUD's own Docking affordance".
//
// ---- WHY THIS FILE EXISTS (S108 / QC H-04 + DK-04) ----
// They were not the same construction. The rectangle was written out THREE times, in two files, with
// different constants, and every copy disagreed with at least one other:
//
//   Frame58Hud.cs:44   DRAWN   by = h * 0.44f, bh = h * 0.12f      →  0.44 h … 0.56 h
//   FigmaUI.cs:317     HIT     py >= h * 0.40f && py < h * 0.60f   →  0.40 h … 0.60 h   (HUD)
//   FigmaUI.cs:343     HIT     py >= h * 0.40f && py < h * 0.60f   →  0.40 h … 0.60 h   (Docking)
//
// So on the HUD a 20%-tall hit band sat behind a 12%-tall painted box: an invisible halo of 0.04 h
// above and below — 28.1 px each way at the shipped 703 — in which a tap on empty letterbox silently
// navigated. And on the manual-docking page it was worse: the band fires and NOTHING IS DRAWN AT ALL.
// DockingSimPage never painted a RENDEZVOUS affordance, so that whole rectangle was S54's defect in
// its purest form — a control the crew cannot see, cannot predict, and cannot avoid.
//
// `PageAction`'s standing rule is one rect shared by the draw, the hit test and the test; the Cover
// obeys it with NextViewRect / PadRect / CapsuleRect. These two pages are that rule, applied.
//
// ⛔ THE DRAWN BOX IS THE SHARED TRUTH, NOT THE HIT BAND. The crew can only aim at what they can see,
// and a control that fires outside its own border is the defect — so the 12% painted box won and the
// 20% band is gone. If the button is too small to hit comfortably, the answer is to draw it bigger,
// never to keep a secret halo.
// ============================================================================================
namespace DragonScreen
{
    public static class MarginAffordance
    {
        const float RefW = 3427f, RefH = 2112f;

        /// <summary>Below this much letterbox there is no room for the control, so it is neither drawn
        /// nor hit — the CLEAN 1 guard, and it must stay on both sides together, which is exactly what
        /// sharing one function guarantees. ⚠ A panel whose aspect leaves 40 px or less therefore has no
        /// margin route at all; the Menu grid is the second route to both destinations.
        ///
        /// ⛔ 40 PX AT Typography.RefPanelW, resolved through ScaleFor at the point of use — see the
        /// SCALE block below. "Is there room for a control" is a question about PHYSICAL room, so the
        /// threshold has to follow the panel or it means something different on every screen.</summary>
        public const float MinMargin = 40f;

        /// <summary>Gap between the panel edge / frame art and the box. Was 12 on both sides, which cost
        /// 24 px of a margin only 69.6 px wide at the shipped size — see FitSize. At RefPanelW.</summary>
        const float Inset = 4f;

        const float TopFrac = 0.44f, HeightFrac = 0.12f;

        // ---- EVERY PIXEL CONSTANT IN THIS FILE IS A SIZE AT Typography.RefPanelW (QC R-02 family) ----
        // MinMargin, Inset, BorderPx and Pad are PANEL pixels, measured at 1280. The letterbox they are
        // subtracted from - ox = (w - RefW*sc) / 2 - scales with the panel. So when S115 doubled the
        // shipped screenWidth on 2026-09-05, ox doubled and these four did not, and the usable box grew
        // 2.13x instead of 2.00x. QC measured that 2.13x independently.
        //
        // The visible consequence was a control that looked LESS illegible for a reason that has nothing
        // to do with legibility: the fitted MANUAL/DOCKING type reported 72.2% of the floor at 1280 and
        // 82.9% at 2560, for a box that had not changed size in the seat by one arc-minute. R-02's own
        // verify line wants those two numbers EQUAL, and with the scale applied they are - pinned by
        // LegibilityFloorTest, which prints both and asserts they agree.
        //
        // TopFrac / HeightFrac above are FRACTIONS of h and were always right; CapAdvance is an em ratio
        // and is dimensionless. Only the four pixel counts needed this.
        static float S(int w) { return Typography.ScaleFor(w); }

        /// <summary>D-DIN capital ink width per character, in ems. MEASURED, not chosen: at the shipped
        /// 1280×703 the HUD renders "MANUAL" at ts = 14.06 px with its white ink spanning x 8…63, i.e.
        /// 56 px over 6 characters = 9.333 px each = 0.6638 em. It is a per-character average over an
        /// all-caps string, so it runs a little wide for words with an I or an L and a little narrow for
        /// wide ones — which is the right direction for a FIT, because erring wide errs inside the box.
        /// Pinned by MarginAffordanceTest against the same render it came from.</summary>
        public const float CapAdvance = 0.6638f;

        /// <summary>The control's box in PANEL space, or false when the margin is too narrow to hold it.
        /// The one source of truth: Build, HitTest and the tests all come here.</summary>
        public static bool Rect(int w, int h, out float x, out float y, out float bw, out float bh)
        {
            x = y = bw = bh = 0f;
            if (w <= 0 || h <= 0) return false;
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f;
            float ps = S(w);
            if (ox <= MinMargin * ps) return false;
            x = Inset * ps;
            bw = ox - Inset * 2f * ps;
            y = h * TopFrac;
            bh = h * HeightFrac;
            return true;
        }

        /// <summary>The type size that keeps the WIDEST of the labels inside <paramref name="bw"/>, never
        /// larger than <paramref name="wantSize"/>.
        ///
        /// ⚠ THIS SHRINKS, AND SHRINKING IS NOT ALWAYS THE RIGHT ANSWER — QC C-03 caught a fix plan that
        /// would have traded an overrun for an unreadable label, and the same trap is here. It is safe on
        /// the HUD because the shrink is small (see H-06's numbers) and unsafe for a long word, which is
        /// why <see cref="FitsLegibly"/> exists and why the callers do not silently rely on this.</summary>
        /// <param name="ps">the panel's scale, Typography.ScaleFor(w) — REQUIRED, with no default
        /// overload on purpose. The border and the breathing gap are RefPanelW pixels and have to follow
        /// the panel along with the box they are subtracted from (see the SCALE block above); a caller
        /// that could omit the scale would silently measure a 2560 box against a 1280 border, which is
        /// the whole shape of QC R-02. Every caller already has `w`, so every caller can say.</param>
        public static float FitSize(float bw, float wantSize, string a, string b, float ps)
        {
            int n = Longest(a, b);
            if (n < 1 || bw <= 0f) return wantSize;
            // The ink has to clear the 2-px border on BOTH sides, plus a 2-px breathing gap, or it
            // "fits" by touching its own frame - which is what the overflow looked like from the inside.
            float avail = bw - (BorderPx + Pad) * 2f * ps;
            if (avail <= 0f) return wantSize;
            float fit = avail / (n * CapAdvance);
            return fit < wantSize ? fit : wantSize;
        }

        /// <summary>Both at Typography.RefPanelW; scaled at the point of use — see the SCALE block.</summary>
        const float BorderPx = 2f, Pad = 2f;

        /// <summary>The widest label's ink width at the fitted size - what a test asserts against the
        /// box, and what the preview's own ink measurement is compared to.</summary>
        public static float InkWidth(float ts, string a, string b)
        {
            return Longest(a, b) * CapAdvance * ts;
        }

        /// <summary>Does the fitted type clear the measured legibility floor? Reported rather than
        /// silently accepted: a control that has to go below the floor to fit its own box is telling
        /// you the BOX is wrong, and that is a design question, not something a fit can solve.
        ///
        /// ---- THE FLOOR IS MinFor(w), NOT Min (QC R-02, 2026-09-06) ----
        /// This used to read `>= Typography.Min`. Min is 16 px AT Typography.RefPanelW, so on the
        /// 2560-wide panel shipped since S115 it was half the physical size it was measured as, and
        /// this predicate was twice as easy to pass as it was written to be. Both sides of the
        /// comparison now scale with the panel, so it reports the same verdict - and the same
        /// PERCENTAGE of the floor - at any width, which is the point.</summary>
        public static bool FitsLegibly(int w, int h, string a, string b)
        {
            float x, y, bw, bh;
            if (!Rect(w, h, out x, out y, out bw, out bh)) return false;
            return FitSize(bw, h * 0.020f, a, b, S(w)) >= Typography.MinFor(w);
        }

        static int Longest(string a, string b)
        {
            int n = (a == null) ? 0 : a.Length;
            int m = (b == null) ? 0 : b.Length;
            return m > n ? m : n;
        }

        /// <summary>Draw the control. <paramref name="b"/> may be null for a single-line label, which is
        /// centred rather than sitting where the first of two lines would.</summary>
        public static void Draw(DisplayList dl, int w, int h, string a, string b)
        {
            float x, y, bw, bh;
            if (!Rect(w, h, out x, out y, out bw, out bh)) return;

            float ps = S(w);
            dl.Rect(x, y, bw, bh, DragonPalette.Panel);
            dl.Box(x, y, bw, bh, BorderPx * ps, DragonPalette.Accent);

            float ts = FitSize(bw, h * 0.020f, a, b, ps);
            float cx = x + bw * 0.5f;
            if (b == null)
            {
                dl.Text(a, cx, y + bh * 0.40f, ts, TextAlign.Centre, DragonPalette.White);
                return;
            }
            dl.Text(a, cx, y + bh * 0.26f, ts, TextAlign.Centre, DragonPalette.White);
            dl.Text(b, cx, y + bh * 0.54f, ts, TextAlign.Centre, DragonPalette.Accent);
        }

        /// <summary>Did this touch land on the control? The SAME rect that was drawn — that is the whole
        /// point of the file.</summary>
        public static bool Hit(float px, float py, int w, int h)
        {
            float x, y, bw, bh;
            if (!Rect(w, h, out x, out y, out bw, out bh)) return false;
            return px >= x && px < x + bw && py >= y && py < y + bh;
        }
    }
}
