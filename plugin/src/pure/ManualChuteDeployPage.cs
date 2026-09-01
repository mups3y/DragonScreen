// DragonScreen — ManualChuteDeployPage  (PURE: the real "Manual Chute Deploy" deorbit page)
// ============================================================================================
// A real Crew Dragon page reconstructed from full-res capsule photos (discovery5 = the "(Complete FC
// Failed)" state, discovery15 = nominal). It is a deorbit-phase page in the Cover's chrome: the shared
// 7-item phase rail on the LEFT (with "Manual Chute" lit — via CoverPage.DrawRail so the two rails are
// pixel-identical), the two chute-procedure sections in the CENTRE content panel, and the live globe on
// the RIGHT. Reached from the Cover's "Manual Chute" rail item (FigmaUI); the rail returns to the Cover.
//
// Each command step carries a right-side ACTION the crew taps (Arm and verify / Execute / Latch / Monitor
// altitude / Halt); altitude-gate rows are dim context. Altitudes are marked "(TBC)" — that is SpaceX's
// own to-be-confirmed placeholder text on the real screen, kept verbatim. Actions are display-only until
// the touch pass (like the Suit Leak Check's read-only controls).
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

        // one procedure step: label · action (empty = an altitude-gate context row, drawn dim, no button)
        struct Step { public string Label, Act; public bool Gate;
            public Step(string l, string a, bool g){ Label=l; Act=a; Gate=g; } }

        static readonly Step[] High = {
            new Step("10.6 km (TBC)   ·   6 nm   ·   drogues", "", true),
            new Step("ENABLE BACKUP PYROS", "Arm and verify", false),
            new Step("DEPLOY DROGUES", "Execute", false),
            new Step("10.0 km (TBC)   ·   6 nm   ·   drogues", "", true),
            new Step("FIRE PYRO", "Execute", false),
            new Step("2.5 km (TBC)   ·   6 nm   ·   mains", "", true),
            new Step("ENABLE BACKUP PYROS", "Arm and verify", false),
            new Step("DEPLOY MAINS", "Execute", false),
            new Step("2.2 km (TBC)   ·   6 nm   ·   mains", "", true),
            new Step("FIRE PYRO", "Execute", false) };

        static readonly Step[] Standard = {
            new Step("5.5 km (TBC)   ·   6 nm   ·   drogues", "Monitor altitude", false),
            new Step("ENABLE BACKUP PYROS", "Arm and verify", false),
            new Step("DEPLOY DROGUES", "Latch", false),
            new Step("1.6 km (TBC)   ·   6 nm   ·   mains", "", true),
            new Step("FIRE PYRO", "Execute", false),
            new Step("ENABLE BACKUP PYROS", "Arm and verify", false),
            new Step("DEPLOY MAINS", "Execute", false) };

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
            dl.Asset("component_48", X(0), Y(1877), Wd(0, 3427), Z(235), White);

            // live globe, right of the content panel (identical placement to the Cover)
            float gs = Z(1809);
            float gcx = (X(1442f) + w) * 0.5f;
            float gcy = (Y(220f) + Y(1877f)) * 0.5f;
            NavPage.Planet(dl, s, view, gcx - gs * 0.5f, gcy - gs * 0.5f, gs, gs);

            // top-bar telemetry (drawn — the Cover's baked bar assets are private to it)
            L("ACTIVE PHASE", 250, 44, 22, Dim);   L("Deorbit Coast", 250, 78, 40, White);
            C("SPLASHDOWN TIME", 690, 44, 22, Dim); C("T-01:08:36", 690, 78, 40, White);
            string[] tl = { "INERTIAL VELOCITY", "ALTITUDE", "APOGEE", "PERIGEE", "INCLINATION" };
            string[] tv = { "7.67 km/s", "406.4 km", "428.9 km", "380.7 km", "51.64°" };
            float[] tx = { 1900f, 2300f, 2620f, 2940f, 3260f };
            for (int i = 0; i < tl.Length; i++) { C(tl[i], tx[i], 44, 20, Dim); C(tv[i], tx[i], 78, 34, White); }

            // the shared 7-item phase rail with "Manual Chute" (index 6) lit
            CoverPage.DrawRail(dl, w, h, 6);

            // ---- content panel header ----
            dl.Asset("ic_sharp_arrow_back", X(291), Y(289), Z(48), Z(48), White);
            L("Manual Chute Deploy", 490, 286, 58, White);
            R("MANUAL", 1420, 300, 30, Accent);
            dl.Line(X(260), Y(400), X(1420), Y(400), St(2), Hair);

            // ---- two procedure sections ----
            float rowH = 60f;
            void Section(string title, Step[] steps, ref float y)
            {
                dl.ArcBand(X(300), Y(y + 14), Z(5), Z(15), 0, 360, Alarm);       // red section marker
                L(title, 340, y, 34, White);
                y += 76f;
                for (int i = 0; i < steps.Length; i++)
                {
                    Step st = steps[i];
                    L(st.Label, 320, y, st.Gate ? 26 : 28, st.Gate ? Dim : White);
                    if (st.Act.Length > 0)
                    {
                        dl.Box(X(1130), Y(y - 6), Wd(1130, 280) , Z(46), St(2), Hair);
                        C(st.Act, 1270, y - 2, 24, Accent);
                    }
                    dl.Line(X(300), Y(y + rowH - 14), X(1410), Y(y + rowH - 14), St(1), DragonPalette.Text8);
                    y += rowH;
                }
                y += 24f;
            }

            float cy = 470f;
            Section("High Altitude Chute Deploy", High, ref cy);
            Section("Standard Altitude Chute Deploy", Standard, ref cy);
        }
    }
}
