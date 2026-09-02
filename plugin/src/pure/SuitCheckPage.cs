// DragonScreen — SuitCheckPage  (PURE: the "4.011 Suit Leak Check", ECLSS procedure)
// ============================================================================================
// Rebuilt to the REAL Crew Dragon page, corrected against full-res capsule photographs (Discovery
// "Inside SpaceX's Crew Dragon Capsule", discovery2 = in-progress, discovery3 = completion popup).
// The reference-demo (neel-dandiwala/SpaceX-Dragon2-UI) gave the layout, but its copy was a paraphrase;
// the real wording is used here. Three columns: LEFT = title "4.011 - Suit Leak Check" / "ECLSS" + the
// two-step checklist + read-only controls; MAIN = "SECTION 2: IN PROGRESS · Execute Suit Leak Check",
// step 2.3 + INITIATE, step 2.4, and the table (TIME REMAINING IN LEAK CHECK + four SUIT n DELTA
// PRESSURE + four SUIT n STATUS = "Nominal") + FINISH; RIGHT = the caution note + HALT.
//
// FLOW: INITIATE runs a 5→0 countdown (~0.9s/step); at 0 the completion POPUP shows ("4.011 - Suit
// Leak Check / ECLSS / PROCEDURE COMPLETE / Crew can open their visors if desired but must not open
// zippers or disconnect umbilical."); HALT stops + resets. TIME REMAINING counts the real procedure
// countdown; the four SUIT n DELTA PRESSURE rows DASH — nothing in this build models a suit, so there
// is no honest number to put there (T13c; see the note at the rows themselves). The four SUIT n STATUS
// rows are reference COPY, not values, and are reproduced as the real page words them (S22). NOTE: the real page has NO "CLEAR" line, and NO Failed-Low / TROUBLESHOOT / "2.5
// Contact SpaceX" content — none of that appears in any real frame, so it is deliberately not drawn.
//
// Icons are referenced by asset key (ic_check/ic_dash/ic_grid/ic_refresh/ic_eye/ic_circle).
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class SuitCheckPage
    {
        public const int Commands = 200;
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba Bg     = DragonPalette.Background;
        static readonly Rgba Panel  = DragonPalette.Panel;
        static readonly Rgba Accent = DragonPalette.Accent;
        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Dim    = DragonPalette.Text6;
        static readonly Rgba Hair   = DragonPalette.Hairline;
        static readonly Rgba Amber  = DragonPalette.Caution;
        static readonly Rgba Red    = DragonPalette.Alarm;
        static readonly Rgba Go     = DragonPalette.Go;

        static readonly string[] Suit = { "SUIT 1", "SUIT 2", "SUIT 3", "SUIT 4" };
        const string Dash = "—";     // no source — never a plausible reading

        /// <summary>countdown: the SUIT PRESSURE counter (5..0); showPopup: the completion dialog is up.</summary>
        public static void Build(DisplayList dl, int w, int h, int countdown, bool showPopup)
        {
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            float SZ(float v) => v * sy;
            int St(float rs) { int p = (int)Math.Round(rs * sy); return p < 1 ? 1 : p; }
            void L(string t, float x, float y, float sz, Rgba c) => dl.Text(t, PX(x), PY(y), SZ(sz), TextAlign.Left, c);
            void C(string t, float cx, float y, float sz, Rgba c) => dl.Text(t, PX(cx), PY(y), SZ(sz), TextAlign.Centre, c);
            void Ico(string k, float x, float y, float s, Rgba c) => dl.Asset(k, PX(x), PY(y), SZ(s), SZ(s), c);
            void Pl(float x, float y, float pw, float ph, Rgba border)
            { dl.Rect(PX(x), PY(y), pw * sx, ph * sy, Panel); dl.Box(PX(x), PY(y), pw * sx, ph * sy, St(3), border); }

            dl.Rect(0, 0, w, h, Bg);

            // ================= LEFT PANEL: 4.011 checklist =================
            dl.Box(PX(48), PY(96), 720 * sx, 1700 * sy, St(3), Panel);
            C("4.011 - Suit Leak Check", 408, 180, 40, White);
            C("ECLSS", 408, 244, 24, Accent);
            L("SUIT", 120, 350, 28, Accent);
            Ico("ic_check", 120, 452, 38, White); L("1. PREPARE SUITS FOR LEAK CHECK", 176, 458, 26, White);
            Ico("ic_check", 120, 560, 38, White); L("2. EXECUTE SUIT LEAK CHECK", 176, 566, 26, White);
            // read-only controls (bottom)
            dl.Line(PX(120), PY(1560), PX(700), PY(1560), St(2), Hair);
            Pl(210, 1600, 130, 130, White); Ico("ic_grid", 245, 1635, 60, White);
            Pl(430, 1600, 130, 130, White); Ico("ic_eye", 465, 1635, 60, White);
            C("ENTER READ-ONLY", 408, 1770, 26, White);

            // ================= MAIN PANEL =================
            dl.Box(PX(820), PY(96), 2000 * sx, 1700 * sy, St(3), Panel);
            Ico("ic_refresh", 1180, 168, 34, Accent);
            L("SECTION 2: IN PROGRESS", 1230, 172, 30, Accent);
            C("Execute Suit Leak Check", 1800, 280, 62, White);

            // 2.3 + INITIATE
            L("2.3", 1060, 430, 34, White); L("On SpaceX GO to pressurize - command:", 1160, 430, 32, White);
            Pl(2300, 400, 470, 120, White);
            Ico("ic_grid", 2350, 442, 36, White); C("INITIATE SUIT LEAK CHECK", 2575, 448, 26, White);

            // 2.4 monitor
            L("2.4", 1060, 600, 34, White);
            L("Monitor suit delta pressure. Confirm all four suit", 1160, 588, 30, White);
            L("statuses are nominal after time remaining is 0.", 1160, 628, 30, White);

            // ---- the log table ----
            float tx = 1160, tvx = 2050, tix = 2560, rowH = 92f, y0 = 720f;
            void Row(int i, string label, string val, string icon, Rgba iconTint, Rgba valTint)
            {
                float y = y0 + i * rowH;
                dl.Line(PX(tx), PY(y), PX(2620), PY(y), St(1), Hair);
                L(label, tx, y + 28, 30, DragonPalette.Text3);
                L(val, tvx, y + 28, 30, valTint);
                Ico(icon, tix, y + 16, 40, iconTint);
            }
            Row(0, "TIME REMAINING IN LEAK CHECK", countdown + "s", "ic_refresh", White, White);
            // ---- SUIT n DELTA PRESSURE: no source, so a dash (T13c) ----
            // The one readout on this page with nothing behind it. A suit delta pressure is suit
            // pressure minus cabin pressure, and this build models no suit at all: not in
            // VehicleSystems, not in CabinEnvironment, and KSP has no per-crew pressure resource to
            // stand in for one. "0.01psi" was a representative constant, and a constant sitting in a
            // LEAK CHECK is the worst place in the build for one — four suits reading a confident
            // 0.01 psi is exactly how a screen says "no leak" when it in fact knows nothing.
            // TELEMETRY_REGISTRY's rule: no real source -> not invented. Live the day a suit is
            // modelled; dashed until then.
            for (int i = 0; i < 4; i++) Row(1 + i, Suit[i] + " DELTA PRESSURE", Dash, "ic_dash", Dim, Dim);
            // STATUS reads "Nominal" (green) per suit; a suit that fails the check reads "Failed Low" —
            // the failure branch (right-panel TROUBLESHOOT block) is the crew's response to that.
            for (int i = 0; i < 4; i++) Row(5 + i, Suit[i] + " STATUS", "Nominal", "ic_check", Go, Go);
            dl.Line(PX(tx), PY(y0 + 9 * rowH), PX(2620), PY(y0 + 9 * rowH), St(1), Hair);

            // 2.5 + FINISH — the procedure continues below the "Scroll to continue" fold seen in the
            // photos; this is the next step and the completion control.
            L("2.5", 1060, y0 + 9 * rowH + 44, 34, White);
            L("On completion, contact SpaceX to report results.", 1160, y0 + 9 * rowH + 44, 30, White);
            Pl(1160, y0 + 9 * rowH + 100, 300, 84, Hair); C("FINISH", 1310, y0 + 9 * rowH + 124, 30, White);

            // ================= RIGHT PANEL: caution + troubleshoot + HALT =================
            dl.Rect(PX(2870), PY(300), 500 * sx, 300 * sy, Panel);
            dl.Rect(PX(2870), PY(300), 12 * sx, 300 * sy, Amber);       // amber left rule
            L("Caution:", 2910, 350, 26, Amber);
            L("It is critical to remain still", 3010, 350, 26, White);
            L("during final 15 seconds of leak", 2910, 396, 26, White);
            L("check to ensure accurate", 2910, 442, 26, White);
            L("automated evaluation of suit", 2910, 488, 26, White);
            L("delta pressure values.", 2910, 534, 26, White);

            // ---- failure branch (reconstructed; see header note). Not in the captured frames, but a
            // leak check has a fail path and the main table scrolls past "Scroll to continue" — this is
            // the crew's response when a suit reads "Failed Low". Display-only until the touch pass. ----
            dl.Rect(PX(2870), PY(680), 500 * sx, 640 * sy, Panel);
            L("Did any suit fail the", 2910, 730, 28, White);
            L("leak check?", 2910, 772, 28, White);
            L("A suit reading Failed Low did", 2910, 848, 24, Dim);
            L("not hold pressure.", 2910, 892, 24, Dim);
            Pl(2910, 970, 420, 110, White);
            Ico("ic_grid", 2950, 1004, 40, White); C("TROUBLESHOOT", 3150, 1010, 28, White);
            Pl(2910, 1120, 420, 110, White);
            Ico("ic_refresh", 2950, 1154, 40, White); C("TRY ADDITIONAL TIMER", 3150, 1162, 24, White);

            Pl(2900, 1600, 470, 120, Red);
            Ico("ic_grid", 2950, 1642, 36, Red); C("HALT SUIT LEAK CHECK", 3155, 1648, 30, White);

            // ================= bottom status bar =================
            dl.Asset("component_48", 0f, PY(1877), w, SZ(235), White);

            // ================= completion popup =================
            if (showPopup)
            {
                dl.Rect(0, 0, w, h, new Rgba(0.008f, 0.027f, 0.216f, 0.82f));   // #020738 scrim
                float pw = 1500, ph = 1040, px = (RefW - pw) * 0.5f, py = (RefH - ph) * 0.5f - 40;
                dl.Rect(PX(px), PY(py), pw * sx, ph * sy, Panel);
                dl.Box(PX(px), PY(py), pw * sx, ph * sy, St(3), Hair);
                float cx = RefW * 0.5f;
                C("4.011 - Suit Leak Check", cx, py + 130, 48, White);
                C("ECLSS", cx, py + 200, 26, Accent);
                C("CLEAR", cx, py + 280, 34, Accent);
                C("PROCEDURE COMPLETE", cx, py + 360, 44, White);
                C("Crew can open their visors if desired but", cx, py + 470, 34, DragonPalette.Text3);
                C("must not open zippers or disconnect", cx, py + 522, 34, DragonPalette.Text3);
                C("umbilical.", cx, py + 574, 34, DragonPalette.Text3);
                Pl(cx - 90, py + ph - 220, 180, 130, White);
                Ico("ic_circle", cx - 40, py + ph - 195, 80, White);
            }
        }

        // ---- INTERACTIVITY ----
        // START runs the countdown, HALT stops+resets, CLOSE dismisses the completion popup. The rects
        // below are the exact ones Build draws (keep them in step). While the popup is up only CLOSE is
        // live, so a stray touch on START/HALT behind the scrim does nothing.
        public enum SuitAct { None, Start, Halt, Close }

        public static SuitAct HitTest(float px, float py, int w, int h, bool popup)
        {
            float dx = px * RefW / w, dy = py * RefH / h;
            bool In(float x, float y, float ww, float hh) => dx >= x && dx < x + ww && dy >= y && dy < y + hh;

            if (popup)
            {
                const float ph = 1040f, pTop = (RefH - ph) * 0.5f - 40f, cx = RefW * 0.5f;
                return In(cx - 90f, pTop + ph - 220f, 180f, 130f) ? SuitAct.Close : SuitAct.None;
            }
            if (In(2300f, 400f, 470f, 120f)) return SuitAct.Start;
            if (In(2900f, 1600f, 470f, 120f)) return SuitAct.Halt;
            return SuitAct.None;
        }
    }
}
