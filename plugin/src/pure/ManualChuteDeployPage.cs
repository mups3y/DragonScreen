// DragonScreen — ManualChuteDeployPage  (PURE: the real "Manual Chute Deploy" deorbit page)
// ============================================================================================
// A real Crew Dragon page reconstructed from full-res capsule photos (discovery5 = the "(Complete FC
// Failed)" state, discovery15 = nominal). It is a deorbit-phase page in the Cover's chrome: the shared
// 7-item phase rail on the LEFT (with "Manual Chute" lit — via CoverPage.DrawRail so the two rails are
// pixel-identical), the two chute-procedure sections in the CENTRE content panel, and the live globe on
// the RIGHT. Reached from the Cover's "Manual Chute" rail item (FigmaUI); the rail returns to the Cover.
//
// The top telemetry strip is LIVE (T13c): ACTIVE PHASE / SPLASHDOWN TIME / INERTIAL VELOCITY /
// ALTITUDE / APOGEE / PERIGEE / INCLINATION all read PageState, and dash when the feed is dead or the
// quantity is not meaningful. The PROCEDURE COPY below is untouched reference text — the altitudes,
// the step names and the actions are what the real screen says, not values to wire.
//
// Each command step carries a right-side ACTION the crew taps (Arm and verify / Execute / Latch / Monitor
// altitude / Halt); altitude-gate rows are dim context. Altitudes are marked "(TBC)" — that is SpaceX's
// own to-be-confirmed placeholder text on the real screen, kept verbatim.
//
// ---- THE ACTIONS ARE LIVE (T14, the touch pass) ----
// Four of the five command steps name a control the LOWER CONSOLE PANEL also carries — ENABLE BACKUP
// PYROS, DEPLOY DROGUES, FIRE PYRO, DEPLOY MAINS are §4's EnableBackupPyros / DroguesAndMains / FirePyro
// / MainsOnly. They are the SAME commands, so a tap here goes through the SAME dispatcher the plate's
// button does (`FlightCommands.Run`) and its outcome is read by the SAME policy (`PanelPolicy`, which is
// where BUILD_PLAN §14.4(a)+(b) live). Nothing about what a chute command MEANS is decided in this file:
// pressing DEPLOY DROGUES on the glass and pressing DROGUES & MAINS on the plate cannot come to different
// answers, because there is only one answer and neither surface owns it.
//
// What that yields TODAY: ENABLE BACKUP PYROS arms and its row LIGHTS (a real display-state command,
// §4's CONFIRMED list) — and it lights on the console plate too, off the one flag. The three that would
// actually fire something are §14.4(a) flight actuation with no flight software behind them yet, so they
// CLICK INTO SILENCE: no light, no action, no red. Part B fills them in (§B12.5) without this page
// changing. "Monitor altitude" names no command at all — it is the crew watching the ALTITUDE the strip
// above already draws live — so it stays dark, which is the same answer for the same reason.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class ManualChuteDeployPage
    {
        public const int Commands = 320;   // includes the live globe (NavPage.Planet) command load
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba White = DragonPalette.White;
        static readonly Rgba Dim   = DragonPalette.Text6;
        static readonly Rgba Faint = DragonPalette.Text7;
        static readonly Rgba Accent= DragonPalette.Accent;
        static readonly Rgba Alarm = DragonPalette.Alarm;
        static readonly Rgba Hair  = DragonPalette.Hairline;
        static readonly Rgba Panel = DragonPalette.Panel;
        const string Dash = "—";     // no source / not meaningful — never a plausible zero

        // one procedure step: label · action (empty = an altitude-gate context row, drawn dim, no button)
        // · Cmd = the console command this step IS, or None for a step that names no command (T14).
        struct Step { public string Label, Act; public bool Gate; public PanelCommand Cmd;
            public Step(string l, string a, bool g){ Label=l; Act=a; Gate=g; Cmd=PanelCommand.None; }
            public Step(string l, string a, PanelCommand c){ Label=l; Act=a; Gate=false; Cmd=c; } }

        // The step→command map. Read off the step's OWN LABEL against §4's modelled console inventory —
        // no interpretation: "ENABLE BACKUP PYROS" is the plate's ENABLE BACKUP PYROS, "DEPLOY DROGUES" is
        // DROGUES & MAINS (§4's confirmed "2 drogues → 4 mains"), "DEPLOY MAINS" is MAINS ONLY, "FIRE
        // PYRO" is FIRE PYRO. Giving a step a command its label does not name would be exactly the
        // invention §1.4 forbids, so the rule is: the label names it, or it is None.
        const PanelCommand Pyros   = PanelCommand.EnableBackupPyros;
        const PanelCommand Drogues = PanelCommand.DroguesAndMains;
        const PanelCommand Mains   = PanelCommand.MainsOnly;
        const PanelCommand Pyro    = PanelCommand.FirePyro;

        static readonly Step[] High = {
            new Step("10.6 km (TBC)   ·   6 nm   ·   drogues", "", true),
            new Step("ENABLE BACKUP PYROS", "Arm and verify", Pyros),
            new Step("DEPLOY DROGUES", "Execute", Drogues),
            new Step("10.0 km (TBC)   ·   6 nm   ·   drogues", "", true),
            new Step("FIRE PYRO", "Execute", Pyro),
            new Step("2.5 km (TBC)   ·   6 nm   ·   mains", "", true),
            new Step("ENABLE BACKUP PYROS", "Arm and verify", Pyros),
            new Step("DEPLOY MAINS", "Execute", Mains),
            new Step("2.2 km (TBC)   ·   6 nm   ·   mains", "", true),
            new Step("FIRE PYRO", "Execute", Pyro) };

        static readonly Step[] Standard = {
            // "Monitor altitude" is the one action here that names no command — the crew watching
            // the ALTITUDE the live strip above already draws. It has a button because the real page
            // draws one; it commands nothing because the words on it do not.
            new Step("5.5 km (TBC)   ·   6 nm   ·   drogues", "Monitor altitude", false),
            new Step("ENABLE BACKUP PYROS", "Arm and verify", Pyros),
            new Step("DEPLOY DROGUES", "Latch", Drogues),
            new Step("1.6 km (TBC)   ·   6 nm   ·   mains", "", true),
            new Step("FIRE PYRO", "Execute", Pyro),
            new Step("ENABLE BACKUP PYROS", "Arm and verify", Pyros),
            new Step("DEPLOY MAINS", "Execute", Mains) };

        // ---- ROW GEOMETRY: ONE WALK, SHARED BY DRAWING AND HITTING (PageAction's rule) ----
        // Build used to accumulate its row `y` inline. The hit test needs the identical ladder, and two
        // ladders down the same rows are two chances to drift — a button that is drawn one row above the
        // one it fires is the classic version of that, and it is invisible in a PNG. So the walk happens
        // ONCE, here, at class-init, and Build and ActionRect both read the result.
        const float ContentTop = 470f, RowH = 60f, SectionGap = 24f, TitleGap = 76f;
        const float ActX = 1130f, ActW = 280f, ActH = 46f, ActDY = -6f;

        static readonly float HighTitleY, StdTitleY;
        static readonly float[] HighY, StdY;

        /// <summary>One tappable ACTION: where it sits, what step it belongs to, and the console command
        /// it IS (None = it names no command — see the Step table).</summary>
        public struct Action { public float Y; public string Label, Act; public PanelCommand Command; }

        /// <summary>Every action on the page, top to bottom — the order the crew reads them in, and the
        /// index the hit test and the tests both speak.</summary>
        public static readonly Action[] Actions;

        static ManualChuteDeployPage()
        {
            float y = ContentTop;
            HighTitleY = y; y += TitleGap;
            HighY = new float[High.Length];
            for (int i = 0; i < High.Length; i++) { HighY[i] = y; y += RowH; }
            y += SectionGap;
            StdTitleY = y; y += TitleGap;
            StdY = new float[Standard.Length];
            for (int i = 0; i < Standard.Length; i++) { StdY[i] = y; y += RowH; }

            int n = 0;
            for (int i = 0; i < High.Length; i++) if (High[i].Act.Length > 0) n++;
            for (int i = 0; i < Standard.Length; i++) if (Standard[i].Act.Length > 0) n++;
            Actions = new Action[n];
            n = 0;
            for (int i = 0; i < High.Length; i++) if (High[i].Act.Length > 0) Actions[n++] = Of(High[i], HighY[i]);
            for (int i = 0; i < Standard.Length; i++) if (Standard[i].Act.Length > 0) Actions[n++] = Of(Standard[i], StdY[i]);
        }

        static Action Of(Step st, float y)
        { Action a; a.Y = y; a.Label = st.Label; a.Act = st.Act; a.Command = st.Cmd; return a; }

        /// <summary>Action <paramref name="i"/>'s box, in PANEL pixels — the one rect Build draws and
        /// HitTest tests. The box lies wholly left of the page's reflow Split (1130..1410 &lt; 1500), so it
        /// scales without the right-hand block's extra width; see Build's own X / Wd.</summary>
        public static void ActionRect(int i, int w, int h, out float x, out float y, out float bw, out float bh)
        {
            x = y = bw = bh = 0f;
            if (i < 0 || i >= Actions.Length || h <= 0) return;
            float sc = h / RefH;
            x = ActX * sc; y = (Actions[i].Y + ActDY) * sc; bw = ActW * sc; bh = ActH * sc;
        }

        /// <summary>
        /// Is action <paramref name="i"/>'s control LIT? §14.4(a)'s two-state language — bright when the
        /// control is active, armed or fired; dark otherwise; there is no third answer.
        ///
        /// Only ENABLE BACKUP PYROS has a state to be in today, and it is read from the flag the CONSOLE
        /// plate's own dash reads (PageState.BackupPyrosArmed ← FlightCommands.BackupPyros), never from a
        /// latch of this page's own — that is what stops the two surfaces disagreeing. The three actuation
        /// commands cannot act yet, so §14.4(a) says dark; they light the day Part B gives them something
        /// to do, with no edit here.
        /// </summary>
        public static bool Lit(int i, PageState s)
        {
            if (i < 0 || i >= Actions.Length) return false;
            return Actions[i].Command == PanelCommand.EnableBackupPyros && s.BackupPyrosArmed;
        }

        /// <summary>Which action a touch hit, or -1. Aimed at the SAME rect Build draws (ActionRect).</summary>
        public static int HitTest(float px, float py, int w, int h)
        {
            for (int i = 0; i < Actions.Length; i++)
            {
                float x, y, bw, bh;
                ActionRect(i, w, h, out x, out y, out bw, out bh);
                if (Control.Hit(px, py, x, y, bw, bh)) return i;
            }
            return -1;
        }

        public static void Build(DisplayList dl, int w, int h, PageState s, MapView view)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float sc = h / RefH; float extra = w - RefW * sc; if (extra < 0f) extra = 0f; const float Split = 1500f;
            float X(float x) => x * sc + (x >= Split ? extra : 0f);
            float Y(float y) => y * sc;
            float Z(float v) => v * sc;
            float Wd(float x, float wref) => wref * sc + (x < Split && x + wref > Split ? extra : 0f);
            int St(float rs) { int p = (int)Math.Round(rs * sc); return p < 1 ? 1 : p; }
            void L(string t, float x, float y, float z, Rgba c) => dl.Text(t, X(x), Y(y), Z(z), TextAlign.Left, c);
            void C(string t, float cx, float y, float z, Rgba c) => dl.Text(t, X(cx), Y(y), Z(z), TextAlign.Centre, c);
            void R(string t, float rx, float y, float z, Rgba c) => dl.Text(t, X(rx), Y(y), Z(z), TextAlign.Right, c);

            dl.Rect(0, 0, w, h, DragonPalette.Background);

            // chrome: content-panel border, top bar bg, bottom bar (shared with the Cover)
            dl.Asset("rectangle_178", X(218), Y(216), Wd(218, 1224), Z(1779), White);
            dl.Asset("rectangle_173", X(0), Y(0), Wd(0, 3427), Z(220), White);
            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame

            // live globe, right of the content panel (identical placement to the Cover)
            float gs = Z(1809);
            float gcx = (X(1442f) + w) * 0.5f;
            float gcy = (Y(220f) + Y(1877f)) * 0.5f;
            NavPage.Planet(dl, s, view, gcx - gs * 0.5f, gcy - gs * 0.5f, gs, gs);

            // ---- top-bar telemetry: LIVE (T13c) ----
            // Drawn rather than placed because the Cover's baked bar assets are private to it. The seven
            // values were the reference export's own baked strings ("7.67 km/s", "T-01:08:36", ...) —
            // §6's "the numeric VALUES are the placeholders" — and every one of them was already in
            // PageState, formatted by VesselData in exactly these renderings. No feed, or a quantity the
            // vehicle's own state says is meaningless, draws a dash: apogee and perigee follow the same
            // ApogeeShown/PerigeeShown flags every other page's apsides do (a conic through a landed
            // vessel is a real solution and a meaningless number), and SPLASHDOWN TIME follows
            // SplashdownShown, which is the registry's "N/A off-return" for SPLASHDOWN_ETA.
            bool ok = s.Valid;
            string T(string t) => (ok && !string.IsNullOrEmpty(t)) ? t : Dash;
            L("ACTIVE PHASE", 250, 44, 22, Dim);   L(T(s.Phase), 250, 78, 40, White);
            C("SPLASHDOWN TIME", 690, 44, 22, Dim);
            C(ok && s.SplashdownShown ? T(s.SplashdownText) : Dash, 690, 78, 40, White);
            string[] tl = { "INERTIAL VELOCITY", "ALTITUDE", "APOGEE", "PERIGEE", "INCLINATION" };
            string[] tv = { T(s.Velocity), T(s.Altitude),
                            ok && s.ApogeeShown ? T(s.Apoapsis) : Dash,
                            ok && s.PerigeeShown ? T(s.Periapsis) : Dash,
                            T(s.InclinationDegText) };
            float[] tx = { 1900f, 2300f, 2620f, 2940f, 3260f };
            for (int i = 0; i < tl.Length; i++)
            {
                C(tl[i], tx[i], 44, 20, Dim);
                C(tv[i], tx[i], 78, 34, tv[i] == Dash ? Dim : White);
            }

            // the shared 7-item phase rail with "Manual Chute" (index 6) lit
            CoverPage.DrawRail(dl, w, h, 6);

            // ---- content panel header ----
            dl.Asset("ic_sharp_arrow_back", X(291), Y(289), Z(48), Z(48), White);
            L("Manual Chute Deploy", 490, 286, 58, White);
            R("MANUAL", 1420, 300, 30, Accent);
            dl.Line(X(260), Y(400), X(1420), Y(400), St(2), Hair);

            // ---- two procedure sections ----
            // Rows are placed from the SHARED ladder (HighY / StdY), never from a `y` this loop walks —
            // see the geometry block above. `act` counts off the Actions array in the same order it was
            // built, which is how a drawn button knows which entry it is and therefore whether it is lit.
            int act = 0;
            void Section(string title, Step[] steps, float[] rowY, float titleY)
            {
                dl.ArcBand(X(300), Y(titleY + 14), Z(5), Z(15), 0, 360, Alarm);   // red section marker
                L(title, 340, titleY, 34, White);
                for (int i = 0; i < steps.Length; i++)
                {
                    Step st = steps[i];
                    float y = rowY[i];
                    L(st.Label, 320, y, st.Gate ? 26 : 28, st.Gate ? Dim : White);
                    if (st.Act.Length > 0)
                    {
                        // §14.4(a): lit = BRIGHT (accent plate, label knocked out), unlit = the plain
                        // hairline box. Two states, and no red for the ones that cannot act.
                        bool on = Lit(act, s);
                        float bx, by, bw, bh;
                        ActionRect(act, w, h, out bx, out by, out bw, out bh);
                        if (on) dl.Rect(bx, by, bw, bh, Accent);
                        dl.Box(bx, by, bw, bh, St(2), on ? Accent : Hair);
                        C(st.Act, 1270, y - 2, 24, on ? DragonPalette.Background : Accent);
                        act++;
                    }
                    dl.Line(X(300), Y(y + RowH - 14), X(1410), Y(y + RowH - 14), St(1), DragonPalette.Text8);
                }
            }

            Section("High Altitude Chute Deploy", High, HighY, HighTitleY);
            Section("Standard Altitude Chute Deploy", Standard, StdY, StdTitleY);
        }
    }
}
