// DragonScreen — PropSchematic  (PURE: the Draco RCS thruster schematic — T9, the real Vehicle·Prop look)
// ============================================================================================
// SCREEN_INVENTORY.md #26 / BUILD_PLAN.md §3 row "Vehicle · Prop — real look = thruster/RCS schematic"
// / §11b. A REAL Crew Dragon propulsion page, photographed in the NASA JSC crew-training series
// (`jsc2026e404727`, Crew-13 — tier-1, a real capsule with the screen lit). §11b characterises it:
// "the Dragon drawn in HORIZONTAL profile (capsule + trunk line-art) ringed by the Draco thruster-quad
// arc symbols with per-cluster firing/status, per-thruster data along the bottom, a LEFT alert +
// sub-nav rail". We built Prop as a generic four-gauge template; §3 marks it REFINE — this is what it
// should be. Our LEFT rail is the page's existing subsystem checklist plus VehicleTabBar (whose eight
// tabs are themselves confirmed-real, C1.4 — no tab is added here).
//
// ---- WHAT IS REAL, WHAT IS OURS (§1.4) ----
// REAL, from the photo: the LAYOUT GRAMMAR above — horizontal profile, quad arc symbols around the
// hull, per-cluster firing/status, a per-thruster data band along the bottom.
// REAL, public vehicle facts already shipped in this codebase (VehicleSubsystemPage's own Prop
// checklist): 16 Dracos in 4 quads, 8 SuperDracos in 4 pairs, NTO/MMH propellant, helium pressurant.
// RECONSTRUCTED + MARKED — §11b's own verdict on this screen is "layout-real / labels-reconstructed …
// exact on-screen text is NOT transcribable", the same footing as DeorbitBurnPrepPage (T7) and
// EntryPage (T8): every STRING drawn here. The quad names A–D, the per-thruster designators and their
// four roles are OURS; SpaceX's real thruster naming and control allocation are not public. The four
// propellant readouts and the five detail readouts in the bottom band are the template's own existing
// representative values, passed in unchanged — moved, never re-invented, and still T13's to make live.
//
// ---- THE FIRING INDICATORS ARE SIMULATED, NEVER FAKED ----
// Every lit segment here is the LIVE RCS demand resolved onto the pod that would have to answer it:
// PageState.TransX/Y/Z and RotPitch/Yaw/Roll come straight off FlightCtrlState in VesselData (the same
// signal the DOCKING page's corner rings already draw), gated by the real RCS action group
// (PageState.RcsOn). Nothing moves here unless the vehicle's controls moved. The pod GEOMETRY — four
// quads 90° apart, each carrying a forward, an aft, a lateral and a tangential thruster — is the
// standard quad arrangement and is ours; the profile spreads the four pods fore/aft for legibility
// rather than stacking them at one axial station, as the source photo's own "ringed" arrangement does.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class PropSchematic
    {
        /// <summary>Worst case: hull line-art + 4 quads + the per-thruster band + the readout columns.</summary>
        public const int Commands = 200;
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba Hull   = DragonPalette.Text6;
        static readonly Rgba Dim    = DragonPalette.Text6;
        static readonly Rgba Faint  = DragonPalette.Text7;
        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Accent = DragonPalette.Accent;

        // ---- the four Draco quads ----
        // The real pods sit at ONE axial station, 90° apart around the hull, so the four rings are drawn
        // as callouts at the corners around the capsule with their leaders converging on that station —
        // the source photo's "ringed by the arc symbols" arrangement, and truer than spreading the pods
        // fore and aft would be.
        static readonly float[] QX = { 1160f, 1840f, 1160f, 1840f };
        static readonly float[] QY = {  300f,  300f, 1140f, 1140f };
        // Roll-axis azimuth of each pod, degrees. Four pods 90° apart is the real arrangement; which
        // pod is called which is ours.
        static readonly double[] QAz = { 45.0, 135.0, 225.0, 315.0 };
        static readonly string[] QuadName = { "QUAD A", "QUAD B", "QUAD C", "QUAD D" };

        // Per-pod thruster roles, in index order, and where each sits on the pod's ring (instrument
        // convention: 0° at twelve o'clock, increasing clockwise).
        static readonly string[] Role    = { "FWD", "AFT", "LAT", "ROLL" };
        static readonly float[]  TickDeg = { 0f, 180f, 90f, 270f };
        static readonly string[][] Des = {
            new[] { "A1", "A2", "A3", "A4" }, new[] { "B1", "B2", "B3", "B4" },
            new[] { "C1", "C2", "C3", "C4" }, new[] { "D1", "D2", "D3", "D4" } };

        static readonly float[] SuperDracoX = { 1250f, 1350f };

        const float QuadRi = 75f, QuadR = 100f;   // ring; ticks run QuadR+4 .. QuadR+22
        // The profile is drawn to the real vehicle's proportions — 4 m across, a 4.4 m capsule and a
        // 3.7 m trunk — so it reads as a Dragon rather than as a wedge that happens to fill the panel.
        const float HullCY = 720f;
        const float NoseX = 1000f, ShoulderX = 1120f, BaseX = 1600f;
        const float TrunkX0 = 1640f, TrunkX1 = 2120f;
        const float NoseHH = 95f, BaseHH = 250f;
        const float PodX = 1440f;                  // the axial station the four pods share

        static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        /// <summary>Half-height of the capsule profile at design-x — the drawn cone, so the leader lines
        /// from the quads land exactly on the hull at any x.</summary>
        static float HalfHeight(float x)
        {
            if (x <= ShoulderX) return NoseHH;
            if (x >= BaseX) return BaseHH;
            return NoseHH + (x - ShoulderX) / (BaseX - ShoulderX) * (BaseHH - NoseHH);
        }

        /// <summary>One thruster's duty, 0..1: the share of the LIVE RCS demand a pod at this azimuth
        /// would have to answer through this role. A thruster only pushes one way, so the opposing
        /// halves of a demand light opposite pods — which is why the negative side is clamped away.</summary>
        public static float ThrusterDuty(PageState s, int quad, int role)
        {
            if (!s.Valid || !s.RcsOn) return 0f;
            double az = QAz[quad] * Math.PI / 180.0;
            float nx = (float)Math.Sin(az), ny = (float)Math.Cos(az);
            switch (role)
            {
                case 0: return Clamp01(s.TransZ);
                case 1: return Clamp01(-s.TransZ);
                case 2:
                {
                    // Lateral: translation away from this pod, plus the pitch/yaw couple resolved on it.
                    float lat = -(s.TransX * nx + s.TransY * ny);
                    float couple = -(s.RotYaw * nx + s.RotPitch * ny);
                    return Clamp01(Clamp01(lat) + Clamp01(couple));
                }
                default:
                    return Clamp01(Math.Abs(s.RotRoll));   // tangential: all four pods roll together
            }
        }

        /// <summary>The pod's own indicator: the hardest-working thruster in it.</summary>
        public static float QuadDuty(PageState s, int quad)
        {
            float d = 0f;
            for (int r = 0; r < 4; r++) { float t = ThrusterDuty(s, quad, r); if (t > d) d = t; }
            return d;
        }

        /// <summary>Draw the schematic across the Prop page's centre + right zone. The caller's four
        /// headline-gauge values and five detail readouts are passed through so their (representative,
        /// T13) numbers move into the data band rather than being lost or re-invented.</summary>
        public static void Draw(DisplayList dl, int w, int h, PageState s,
                                string[] gLabel, string[] gVal, string[] gUnit, float[] gFrac,
                                string[] rLabel, string[] rVal)
        {
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            float SZ(float v) => v * sy;
            void L(string t, float x, float y, float sz, Rgba c) => dl.Text(t, PX(x), PY(y), SZ(sz), TextAlign.Left, c);
            void C(string t, float x, float y, float sz, Rgba c) => dl.Text(t, PX(x), PY(y), SZ(sz), TextAlign.Centre, c);
            void R(string t, float x, float y, float sz, Rgba c) => dl.Text(t, PX(x), PY(y), SZ(sz), TextAlign.Right, c);
            void LN(float x0, float y0, float x1, float y1, Rgba c) =>
                dl.Line(PX(x0), PY(y0), PX(x1), PY(y1), SZ(3f), c);

            // ---- zone heading + the real RCS master state ----
            L("DRACO RCS · 16 THRUSTERS IN 4 QUADS", 900, 130, 30, Accent);
            bool rcs = s.Valid && s.RcsOn;
            R(rcs ? "RCS ENABLED" : "RCS DISABLED", 3380, 130, 30, rcs ? DragonPalette.Go : Faint);

            // ---- HULL: Dragon in horizontal profile, nose left, trunk right ----
            // The nose cone is blunt, not a point — it caps the docking adapter.
            LN(NoseX, HullCY - 34f, NoseX, HullCY + 34f, Hull);
            LN(NoseX, HullCY - 34f, ShoulderX, HullCY - NoseHH, Hull);    // nose cone, upper
            LN(NoseX, HullCY + 34f, ShoulderX, HullCY + NoseHH, Hull);    // nose cone, lower
            LN(ShoulderX, HullCY - NoseHH, BaseX, HullCY - BaseHH, Hull); // sidewall, upper
            LN(ShoulderX, HullCY + NoseHH, BaseX, HullCY + BaseHH, Hull); // sidewall, lower
            LN(BaseX, HullCY - BaseHH, BaseX, HullCY + BaseHH, Hull);     // heat shield
            LN(TrunkX0, HullCY - BaseHH, TrunkX0, HullCY + BaseHH, Hull);  // trunk forward ring
            LN(TrunkX0, HullCY - BaseHH, TrunkX1, HullCY - BaseHH, Hull);
            LN(TrunkX0, HullCY + BaseHH, TrunkX1, HullCY + BaseHH, Hull);
            LN(TrunkX1, HullCY - BaseHH, TrunkX1, HullCY + BaseHH, Hull);
            for (int i = 1; i <= 2; i++)                                   // trunk ribs
            {
                float rx = TrunkX0 + (TrunkX1 - TrunkX0) * i / 3f;
                LN(rx, HullCY - BaseHH, rx, HullCY + BaseHH, DragonPalette.Hairline);
            }

            // SuperDraco pods — 4 pairs on the capsule sidewall, two of them edge-on in profile.
            for (int i = 0; i < SuperDracoX.Length; i++)
            {
                float px2 = SuperDracoX[i], hh = HalfHeight(px2);
                LN(px2, HullCY - hh, px2 - 22f, HullCY - hh - 52f, Faint);
                LN(px2, HullCY + hh, px2 - 22f, HullCY + hh + 52f, Faint);
            }
            L("DRACO ×16 — 4 QUADS OF 4", 900, 950, 24, Dim);
            L("SUPERDRACO ×8 — 4 PAIRS", 900, 992, 24, Faint);

            // ---- the four Draco quads ----
            float podHi = HullCY - HalfHeight(PodX), podLo = HullCY + HalfHeight(PodX);
            for (int q = 0; q < 4; q++)
            {
                float cx = QX[q], cy = QY[q];
                float duty = QuadDuty(s, q);

                dl.ArcBand(PX(cx), PY(cy), SZ(QuadRi), SZ(QuadR), 0, 360, Faint);
                if (duty > 0.01f)
                    dl.ArcBand(PX(cx), PY(cy), SZ(QuadRi), SZ(QuadR), 0, 360.0 * duty, Accent);

                for (int r = 0; r < 4; r++)
                {
                    float td = ThrusterDuty(s, q, r);
                    dl.ArcBand(PX(cx), PY(cy), SZ(QuadR + 4f), SZ(QuadR + 22f),
                               TickDeg[r] - 9f, TickDeg[r] + 9f, td > 0.01f ? Accent : Faint);
                }

                C(QuadName[q], cx, cy - 34f, 26, White);
                C(duty > 0.01f ? "FIRING" : (rcs ? "IDLE" : "OFF"), cx, cy + 2f,
                  22, duty > 0.01f ? Accent : Faint);

                // Leader to the pod station: all four converge on one axial ring around the hull, which
                // is where the real pods are — the drawn spread is a callout, not four stations.
                float ax = PodX, ay = cy < HullCY ? podHi : podLo;
                float dx = ax - cx, dy = ay - cy;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len > QuadR + 30f)
                    LN(cx + (float)(dx / len) * (QuadR + 22f), cy + (float)(dy / len) * (QuadR + 22f),
                       ax, ay, DragonPalette.Hairline);
            }

            // ---- RIGHT: this subsystem's own readouts, beside the vehicle ----
            // The template's four headline-gauge values and five detail readouts, moved here intact.
            L("PROPELLANT", 2250, 240, 28, Accent);
            for (int i = 0; i < gLabel.Length && i < 4; i++)
            {
                float ry = 310f + i * 112f;
                L(gLabel[i], 2250, ry, 24, Faint);
                R(gVal[i], 3300, ry - 8f, 34, White);
                L(gUnit[i], 3312, ry, 22, Faint);
                dl.Rect(PX(2250), PY(ry + 52f), 1130f * sx, SZ(6), Faint);
                float f = (i < gFrac.Length) ? gFrac[i] : 0f;
                if (f > 1f) f = 1f; else if (f < 0f) f = 0f;
                if (f > 0f) dl.Rect(PX(2250), PY(ry + 52f), 1130f * sx * f, SZ(6), Accent);
            }
            dl.Rect(PX(2250), PY(790), 1130f * sx, SZ(3), DragonPalette.Hairline);
            L("SYSTEM", 2250, 830, 28, Accent);
            for (int i = 0; i < rLabel.Length && i < 5; i++)
            {
                float ry = 900f + i * 74f;
                L(rLabel[i], 2250, ry, 24, Faint);
                R(rVal[i], 3380, ry, 26, White);
            }

            // ---- DATA BAND along the bottom ----
            dl.Rect(PX(900), PY(1290), 2480f * sx, SZ(3), DragonPalette.Hairline);
            L("THRUSTER STATUS", 900, 1330, 26, Accent);

            for (int q = 0; q < 4; q++)
            {
                float colX = 900f + q * 620f;
                L(QuadName[q], colX, 1395, 24, Dim);
                for (int r = 0; r < 4; r++)
                {
                    float ry = 1450f + r * 60f;
                    float td = ThrusterDuty(s, q, r);
                    L(Des[q][r], colX, ry, 22, td > 0.01f ? White : Faint);
                    L(Role[r], colX + 90f, ry, 22, Faint);
                    dl.Rect(PX(colX + 230f), PY(ry + 8f), 300f * sx, SZ(6), Faint);
                    if (td > 0.01f) dl.Rect(PX(colX + 230f), PY(ry + 8f), 300f * sx * td, SZ(6), Accent);
                }
            }
        }
    }
}
