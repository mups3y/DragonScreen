// DragonScreen — VrioTestPage  (PURE: "4.700 Deorbit Preparation — Test VRIO Health LEDs")
// ============================================================================================
// A real Crew Dragon procedure screen with NO Figma/demo reference — reconstructed from photographs of
// the actual capsule displays (REAL_SPACEX_SCREENSHOTS, the shanemielke.com walkthrough). It shares the
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
                Pl(2280, y - 34, 500, 96, White);
                Ico("ic_grid", 2320, y - 6, 34, White); C(label, 2540, y - 2, 26, White);
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
                Ico("ic_check", 120, y - 6, 36, Done[i] ? Go : Dim);
                L(Check[i], 176, y, 26, White);
            }
            dl.Line(PX(120), PY(1560), PX(700), PY(1560), St(2), Hair);
            Pl(340, 1600, 130, 130, White); Ico("ic_stop", 375, 1635, 60, White);
            C("ENTER READ-ONLY", 408, 1770, 26, White);

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
            Pl(1050, 1560, 300, 100, Hair); C("NEXT", 1200, 1590, 34, White);

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
