// DragonScreen — VrioTestPage  (PURE: "4.700 Deorbit Preparation — Test VRIO Health LEDs")
// ============================================================================================
// ⚠ S110 / QC F-01 — THIS HEADER USED TO SAY "NO Figma/demo reference", AND THAT WAS WRONG.
// `art/cover/frame59.png` is a Figma frame OF THIS EXACT SCREEN - same title, same DEORBIT checklist with
// the same 4-of-5 state, same steps 4.1-4.5, same three command buttons, same NEXT and ENTER READ-ONLY,
// same two note cards word for word. It was in the repo the whole time, and `UiPage.Procedure` was
// rendering it. This page was reconstructed from photographs while a reference frame sat in the tree -
// which is C7's own failure mode, building from a weaker source than the one already present - and the
// two drawings then drifted apart (see F-01 for the list). frame59 is this page's REFERENCE now; where
// the two disagree, §1.4 says the frame is the source and this file is what gets corrected.
//
// A real Crew Dragon procedure screen, also corroborated by photographs of the actual capsule displays
// (REAL_SPACEX_SCREENSHOTS, the shanemielke.com walkthrough). It shares the
// Suit-Leak-Check procedure template: LEFT = the 4.700 deorbit checklist + read-only control; MAIN = the
// numbered command steps ("Test VRIO Health LEDs" — start/stop the two VRIO LED tests, verify, report);
// RIGHT = the engineering notes. VRIO = the vehicle's redundant I/O the automated chute backup rides on.
//
// FUNCTION: VRIO LEDs are zero-fault-tolerant health indicators for the flight-computer / automated-
// chute path; the crew runs each LED test, verifies the lamps on the command panel, and reports to
// SpaceX, so a malfunction is known before entry. Static for now; the command buttons wire to real
// state in the touch pass, like the Suit Leak Check.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class VrioTestPage
    {
        public const int Commands = 140;
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba Bg     = DragonPalette.Background;
        static readonly Rgba Panel  = DragonPalette.Panel;
        static readonly Rgba Accent = DragonPalette.Accent;
        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Dim    = DragonPalette.Text6;
        static readonly Rgba Hair   = DragonPalette.Hairline;
        static readonly Rgba Go     = DragonPalette.Go;

        static readonly string[] Check = {
            "1. THERMAL PRE-CHILL", "2. BEGIN FLUID LOADING", "3. STORE ITEMS",
            "4. TEST VRIO HEALTH LEDS", "5. COMPLETE FLUID LOADING" };
        static readonly bool[] Done = { true, true, true, true, false };

        public static void Build(DisplayList dl, int w, int h)
        {
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            float SZ(float v) => v * sy;
            int St(float rs) { int p = (int)Math.Round(rs * sy); return p < 1 ? 1 : p; }
            void L(string t, float x, float y, float z, Rgba c) => dl.Text(t, PX(x), PY(y), SZ(z), TextAlign.Left, c);
            void C(string t, float cx, float y, float z, Rgba c) => dl.Text(t, PX(cx), PY(y), SZ(z), TextAlign.Centre, c);
            void Ico(string k, float x, float y, float s, Rgba c) => dl.Asset(k, PX(x), PY(y), SZ(s), SZ(s), c);
            void Pl(float x, float y, float pw, float ph, Rgba border)
            { dl.Rect(PX(x), PY(y), pw * sx, ph * sy, Panel); dl.Box(PX(x), PY(y), pw * sx, ph * sy, St(3), border); }
            // a numbered command step: "N.N Command:" + a labelled button on the right
            void Cmd(string num, string label, float y)
            {
                L(num, 1050, y, 32, White); L("Command:", 1160, y, 32, White);
                // ---- S110 / QC VT-01: THESE THREE COMMAND THE VEHICLE AND CANNOT, SO THEY ARE INERT ----
                // START VRIO 1 / START VRIO 2 / STOP VRIO 2 drive the flight computer's health LEDs.
                // QC classes them (B): §14.4(a) honest no-op until Part B, and they must NOT be given
                // working rectangles in Part A. They were drawn plate + border + white glyph + white
                // label - the full live idiom, on the most complete procedure screen in the build, with
                // seven painted controls and zero hit rects between them.
                // S75's rule applies whichever class they land in: a control that cannot act is not
                // painted as one. They go back to White AND into a hit table together, or not at all.
                Pl(2280, y - 34, 500, 96, Dim);
                Ico("ic_grid", 2320, y - 6, 34, Dim); C(label, 2540, y - 2, 26, Dim);
            }

            dl.Rect(0, 0, w, h, Bg);

            // ================= LEFT PANEL: 4.700 checklist =================
            dl.Box(PX(48), PY(96), 720 * sx, 1700 * sy, St(3), Panel);
            C("4.700 - Deorbit", 408, 180, 40, White);
            C("Preparation", 408, 232, 40, White);
            L("DEORBIT", 120, 330, 30, Accent);
            for (int i = 0; i < Check.Length; i++)
            {
                float y = 440 + i * 100;
                // S110 / QC VT-01 + F-01, and both point the same way. `Done` is a COMPILE-TIME LITERAL
                // (`:34`), so a filled GREEN tick was this page asserting a completion verdict it has no
                // source for - S31/S32's rule, and the same shape MP-01 was fixed for. The reference
                // frame draws these as white-on-dark, not green, so §1.4 and the liveness rule agree.
                // ⛔ The STATE (four done, one open) is reference copy and is reproduced untouched; only
                // the colour was ours. It goes back to `Go` when a real step model drives it - which is
                // VT-01's remaining half, blocked on the stranded `StepList` (S49 §1.1 / H34).
                Ico("ic_check", 120, y - 6, 36, Done[i] ? White : Dim);
                L(Check[i], 176, y, 26, White);
            }
            dl.Line(PX(120), PY(1560), PX(700), PY(1560), St(2), Hair);
            // S110 / QC F-01 + VT-01. Two changes, two different reasons:
            // GLYPH: this was `ic_stop`, a filled rounded rect, where the reference frame draws an EYE -
            // and `ic_eye` is already in the asset set, already used by SuitCheckPage for the identical
            // "ENTER READ-ONLY" control. It was a placeholder that outlived its excuse; the reference and
            // the sibling page agree, so there is nothing to decide.
            // TINT: the control has no hit rect (no HitTest in this file, no ScreenPainter branch), so
            // S75 says it must not be painted as a live button - the same call SC-02 made for the two
            // plates on the Suit Leak Check, which is this page's own template.
            Pl(340, 1600, 130, 130, Dim); Ico("ic_eye", 375, 1635, 60, Dim);
            C("ENTER READ-ONLY", 408, 1770, 26, Dim);

            // ================= MAIN PANEL =================
            dl.Box(PX(820), PY(96), 2000 * sx, 1700 * sy, St(3), Panel);
            Ico("ic_refresh", 1180, 168, 34, Accent);
            L("SECTION 4: IN PROGRESS", 1230, 172, 30, Accent);
            C("Test VRIO Health LEDs", 1800, 280, 62, White);

            Cmd("4.1", "START VRIO 1 LED TEST", 470);
            Cmd("4.2", "START VRIO 2 LED TEST", 640);
            L("4.3", 1050, 810, 32, White);
            L("Verify functionality of VRIO health LEDs (left side of command panel)", 1160, 810, 30, White);
            L("4.4", 1050, 950, 32, White);
            L("Contact SpaceX to report LED status", 1160, 950, 30, White);
            Cmd("4.5", "STOP VRIO 2 LED TEST", 1120);
            // S110 / QC VT-01: NEXT is (A) - navigation/screen state, buildable in principle - but it is
            // not built, because what it advances TO is the step model that does not exist yet (the
            // stranded `StepList`, S49 §1.1 / H34). Until it advances something it is a painted control
            // that resolves to nothing, so it takes the inert tint like its neighbours.
            Pl(1050, 1560, 300, 100, Hair); C("NEXT", 1200, 1590, 34, Dim);

            // ================= RIGHT PANEL: notes =================
            dl.Rect(PX(2870), PY(300), 500 * sx, 340 * sy, Panel);
            L("Note:", 2910, 350, 26, Accent);
            L("Each VRIO LED is zero fault", 3010, 350, 26, White);
            L("tolerant. This test ensures prior", 2910, 396, 26, White);
            L("awareness of a malfunction.", 2910, 442, 26, White);

            dl.Rect(PX(2870), PY(680), 500 * sx, 560 * sy, Panel);
            L("Note:", 2910, 730, 26, Accent);
            L("LED operation begins at the", 3010, 730, 26, White);
            L("start of entry sequence. Any", 2910, 776, 26, White);
            L("light flashing indicates automated", 2910, 822, 24, White);
            L("chute deployment is available.", 2910, 866, 24, White);
            L("- VRIO 1 LED - FC connected", 2910, 940, 24, Dim);
            L("- VRIO 2 LED - Ready for", 2910, 990, 24, Dim);
            L("  automated backup if FC", 2910, 1032, 24, Dim);
            L("  disconnected", 2910, 1074, 24, Dim);

            // ================= bottom status bar =================
            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame
        }
    }
}
