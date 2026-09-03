// DragonScreen — PropSchematic  (PURE: the Draco RCS thruster schematic — T9, art backdrop by S68)
// ============================================================================================
// SCREEN_INVENTORY.md #26 / BUILD_PLAN.md §3 row "Vehicle · Prop — real look = thruster/RCS schematic"
// / §11b. A REAL Crew Dragon propulsion page, photographed in the NASA JSC crew-training series
// (`jsc2026e404727`, Crew-13 — TIER 1, a real capsule with the screen lit). §11b characterises it:
// "the Dragon drawn in HORIZONTAL profile (capsule + trunk line-art) ringed by the Draco thruster-quad
// arc symbols with per-cluster firing/status, per-thruster data along the bottom, a LEFT alert +
// sub-nav rail". Our LEFT rail is the page's existing subsystem checklist plus VehicleTabBar (whose
// eight tabs are themselves confirmed-real, C1.4 — no tab is added here).
//
// ---- S68: THE VEHICLE IS THE OWNER'S OWN ARTWORK; THE INSTRUMENT ON TOP IS STILL CODE ----
// S65 drew the vehicle as vector geometry and wrote "vector, and still LIVE … No bitmaps" into its
// spec. The OWNER overrode that for this page on 2026-09-04 (via the overseer): the constraint was
// absolute where it did not need to be — `DisplayList.Asset` already exists and BOTH renderers already
// draw bitmaps (`dragon_crew` proves it), so a bitmap here breaks no two-renderer contract. The
// vehicle is now `art/cover/dragon_prop_elevation.png`, the owner's OWN generated line-art elevation,
// rotated 90 deg counter-clockwise so the nose points LEFT — the horizontal profile §11b's TIER-1
// photo shows. Rotating an axially symmetric vehicle's elevation preserves that layout grammar.
//
// The art is a BACKDROP, never a replacement for the instrument. Everything that MOVES is still drawn
// in code, on top, from live PageState: the four quad duty rings, the sixteen per-thruster bars, the
// axial key and every callout. ThrusterDuty() / QuadDuty() / MaxDuty() are UNTOUCHED (§14.4(f) — a
// live thing does not regress to static art), and FigmaUINavTest.PropSchematicDuty() still guards them.
//
// ---- WHERE THE FOUR QUAD RINGS SIT, AND WHY THAT IS THE POINT ----
// The artwork itself draws the Draco clusters — four grouped-oval nozzle clusters on the capsule
// shoulders, two projecting above the roll axis and two below. The four rings are registered to THOSE
// CLUSTERS by pixel: QPx/QPy are positions in the ROTATED asset's own 1303x800 pixel space, measured
// as the bright-pixel centroid of each cluster, and AXd/AYd map that space onto the page. So the ring
// that says QUAD A is over the hull position it annotates, and moving or re-scaling the art moves the
// rings with it — the two cannot drift apart.
// Which cluster is called A/B/C/D is OURS (see §1.4 below): an elevation cannot distinguish a quad at
// 45 deg from one at 315 deg — both project "up" — so the pairing is chosen to agree with the axial
// key (A,D above the axis; B,C below), and the key's marks are lit by the SAME QuadDuty as the rings.
//
// ---- ⛔ PROVENANCE: THE ART IS THE OWNER'S OWN WORK (owner decision, 2026-09-04, via the overseer) ----
// `assets/reference/crew dragon with trunk.jpg` is the OWNER'S OWN generated work, filed in that folder
// by habit. That folder's .gitignore banner says "THIRD-PARTY SOURCE", and on that information an
// earlier chat correctly refused to ship it — the refusal was right, the label was simply not true of
// this one file. It is OURS to ship. Recorded in docs/INDEX.md so nobody has to re-litigate it.
// The S65 licence bar is UNCHANGED and still stands: the commercial third-party SpaceX blueprint poster
// used for the ARRANGEMENT is NOT in this repo and must never be added.
//
// ---- WHAT IS REAL, WHAT IS OURS (§1.4) ----
// TIER 1 — the LAYOUT GRAMMAR from the JSC photo: horizontal profile, quad symbols with per-cluster
//   firing/status, the per-thruster data band along the bottom, the left rail. Kept.
// TIER 1 (repo) — public vehicle facts already shipped in this codebase (VehicleSubsystemPage's own
//   Prop checklist) and independently confirmed by docs/reference/craftdump.csv: 16 Dracos in 4 quads
//   of 4, 8 SuperDracos in 4 pods of 2, NTO/MMH, helium pressurant, a NASA Docking System, a PICA-X
//   heat shield, and a trunk carrying solar array + radiator (§8) and lifting surfaces (the fins).
// OWNER ART — the vehicle drawing itself, and every hull feature the callouts name: the nosecone and
//   its covers, the windows, the umbilical, the trunk and its fin. Each callout leader ends on a
//   MEASURED pixel of that artwork, not on a guess.
// TIER 2, MARKED — the RADIAL clocking (four quads 90 deg apart, clocked 45 deg off the four engine
//   pods) drawn in the axial key. The on-glass "QUADS CLOCKED / 45 DEG FROM PODS" note marks it.
//   The ENGINE POD / SUPERDRACO callouts land on the capsule sidewall aft of the quads — the pods'
//   real station — because this artwork does not draw a separate pod fairing to point at.
// OURS — every STRING that is not in the element list: the quad names A-D, the per-thruster
//   designators and their four roles. §11b's verdict on this screen is "layout-real /
//   labels-reconstructed … exact on-screen text is NOT transcribable"; SpaceX's real thruster naming
//   and control allocation are not public. The four propellant readouts and the five detail readouts
//   in the bottom band are the subsystem template's own LIVE values, passed in unchanged.
// NO DIMENSION IS ASSERTED. §8, §B11 and craftdump.csv — the three authorities — carry no linear
//   dimension for Dragon, so none is drawn: every number on this page comes from PageState.
// S65's separate "16x DRACO THRUSTERS" callout is gone: the zone heading already says "16 THRUSTERS IN
//   4 QUADS" and four labelled rings now name each quad, so it pointed at what two other elements said.
//
// ---- THE FIRING INDICATORS ARE SIMULATED, NEVER FAKED ----
// Every lit segment here is the LIVE RCS demand resolved onto the pod that would have to answer it:
// PageState.TransX/Y/Z and RotPitch/Yaw/Roll come straight off FlightCtrlState in VesselData (the same
// signal the DOCKING page's corner rings already draw), gated by the real RCS action group
// (PageState.RcsOn). Nothing moves here unless the vehicle's controls moved.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class PropSchematic
    {
        /// <summary>Worst case: the art + its callouts + 4 quad rings + the axial key + the
        /// per-thruster band + the readout columns. Measured well under this; the headroom keeps the
        /// whole Prop page inside VehicleSubsystemPage.Commands.</summary>
        public const int Commands = 300;
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba Detail = DragonPalette.Text6;    // labels + secondary structure
        static readonly Rgba Faint  = DragonPalette.Text7;
        static readonly Rgba Hidden = DragonPalette.Hairline; // leaders
        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Accent = DragonPalette.Accent;

        // ---- the vehicle: the owner's own elevation, nose LEFT ----
        // art/cover/dragon_prop_elevation.png is 1303x800 with alpha. The placement keeps that aspect
        // exactly (a stretched vehicle would be a lie about a real shape), sits clear of the readout
        // columns at x >= 2250, and leaves a label band above and below it.
        const string ArtKey = "dragon_prop_elevation";
        const float ArtPxW = 1303f, ArtPxH = 800f;
        const float ArtX = 920f, ArtY = 402f, ArtW = 1070f;
        const float ArtH = ArtW * ArtPxH / ArtPxW;
        const float ArtK = ArtW / ArtPxW;          // asset pixels -> design units

        /// <summary>Design-space x of an asset pixel. Every anchor on the vehicle goes through this,
        /// so the overlay is registered to the ARTWORK and not to a remembered position.</summary>
        static float AXd(float px) { return ArtX + px * ArtK; }
        static float AYd(float py) { return ArtY + py * ArtK; }

        // ---- the four Draco quads ----
        // Positions are the measured bright-pixel centroids of the four nozzle clusters in the ROTATED
        // asset, in its own pixel space. A/D are the pair above the roll axis, B/C the pair below,
        // which is the pairing the axial key's azimuths below describe.
        static readonly float[] QPx = { 438.4f, 437.4f, 495.1f, 496.7f };
        static readonly float[] QPy = { 263.7f, 531.6f, 617.6f, 161.4f };
        // Roll-axis azimuth of each pod, degrees (instrument convention: 0 at twelve o'clock,
        // increasing clockwise, viewed LOOKING FORWARD). Four pods 90 deg apart, clocked 45 deg off
        // the four engine pods, is the arrangement (tier 2, marked); which pod is called which is ours.
        static readonly double[] QAz = { 45.0, 135.0, 225.0, 315.0 };
        static readonly string[] QuadName = { "QUAD A", "QUAD B", "QUAD C", "QUAD D" };
        static readonly string[] QuadLetter = { "A", "B", "C", "D" };

        // The rings are small because they are REGISTERED: the clusters they sit on are ~89 design
        // units apart at this art scale, so a ring wide enough to hold text would cover its neighbour.
        // The readable text lives in the label band, on a leader — the page's own callout grammar.
        const float QuadRi = 18f, QuadR = 27f;

        // Where each quad's label sits, and where its leader leaves the label. Top pair above the art,
        // bottom pair below it, ordered left-to-right so no two leaders cross.
        static readonly float[] QLabX = { 1130f, 1130f, 1500f, 1500f };
        static readonly float[] QLabY = {  250f, 1200f, 1200f,  250f };
        static readonly float[] QLeadX = { 1160f, 1160f, 1470f, 1470f };
        static readonly float[] QLeadY = {  322f, 1160f, 1160f,  322f };

        // ---- the axial key (a looking-forward section), in the strip aft of the trunk ----
        const float KeyCX = 2115f, KeyCY = 725f;

        static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

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

        /// <summary>The whole cluster's indicator: the hardest-working QUAD, 0..1 — the Prop page's
        /// "Draco Duty" readout (T13b). Derived from the same live RCS demand the schematic's own rings
        /// are drawn from, so the number in the data band and the segments above it are one signal.</summary>
        public static float MaxDuty(PageState s)
        {
            float d = 0f;
            for (int q = 0; q < 4; q++) { float t = QuadDuty(s, q); if (t > d) d = t; }
            return d;
        }

        // Per-pod thruster roles, in index order, and where each sits on the pod's ring.
        static readonly string[] Role = { "FWD", "AFT", "LAT", "ROLL" };
        static readonly string[][] Des = {
            new[] { "A1", "A2", "A3", "A4" }, new[] { "B1", "B2", "B3", "B4" },
            new[] { "C1", "C2", "C3", "C4" }, new[] { "D1", "D2", "D3", "D4" } };

        /// <summary>Draw the schematic across the Prop page's centre + right zone. The caller's four
        /// headline-gauge values and five detail readouts are passed through so their LIVE numbers move
        /// into the data band rather than being lost or re-invented.</summary>
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
            // A callout: the element's own name, and a hairline leader to the MEASURED asset pixel it
            // names — (ax, ay) are in the artwork's pixel space, never in page space.
            void CO(string t, float tx, float ty, float lx, float ly, float ax, float ay)
            { L(t, tx, ty, 22, Detail); LN(lx, ly, AXd(ax), AYd(ay), Hidden); }

            // ---- zone heading + the real RCS master state ----
            L("DRACO RCS · 16 THRUSTERS IN 4 QUADS", 900, 130, 30, Accent);
            bool rcs = s.Valid && s.RcsOn;
            R(rcs ? "RCS ENABLED" : "RCS DISABLED", 3380, 130, 30, rcs ? DragonPalette.Go : Faint);

            // ================= THE VEHICLE =================
            // White, not a palette tint: the GL painter multiplies this colour into the texture and the
            // PNG preview ignores it, so anything but white would make the glass and the preview — the
            // thing layout is judged from — disagree.
            dl.Asset(ArtKey, PX(ArtX), PY(ArtY), ArtW * sx, SZ(ArtH), White);

            // ---- callouts: each leader ends on a measured pixel of the artwork ----
            CO("NOSECONE",              950,  300, 1010,  340,  250f,   215f);   // the hinged cover
            CO("UMBILICAL",            1620,  300, 1610,  320,  616.5f, 268.7f);
            CO("ENGINE POD",           1560,  455, 1555,  465,  652f,   169f);   // sidewall, aft of the quads
            CO("WINDOWS",               950, 1160, 1010, 1148,  294.6f, 499.9f);
            CO("8× SUPERDRACO ENGINES",1560, 1110, 1555, 1098,  652f,   630f);
            CO("FIN",                  1880, 1160, 1930, 1148, 1150f,   763f);

            // ================= THE FOUR QUAD INDICATORS, ON THEIR CLUSTERS =================
            for (int q = 0; q < 4; q++)
            {
                float cx = AXd(QPx[q]), cy = AYd(QPy[q]);
                float duty = QuadDuty(s, q);
                bool firing = duty > 0.01f;

                dl.ArcBand(PX(cx), PY(cy), SZ(QuadRi), SZ(QuadR), 0, 360, Detail);
                if (firing)
                    dl.ArcBand(PX(cx), PY(cy), SZ(QuadRi), SZ(QuadR), 0, 360.0 * duty, Accent);

                // The readable half, out in the label band on a leader that STOPS at the ring's edge:
                // run it to the centre and the line is drawn straight through the gauge it points at.
                float dx = cx - QLeadX[q], dy = cy - QLeadY[q];
                float len = (float)Math.Sqrt(dx * dx + dy * dy);
                float t = (len > QuadR + 6f) ? (len - QuadR - 6f) / len : 0f;
                LN(QLeadX[q], QLeadY[q], QLeadX[q] + dx * t, QLeadY[q] + dy * t, Hidden);
                C(QuadName[q], QLabX[q], QLabY[q], 24, firing ? Accent : White);
                C(firing ? "FIRING" : (rcs ? "IDLE" : "OFF"), QLabX[q], QLabY[q] + 34f,
                  22, firing ? Accent : Faint);
            }

            // ================= THE AXIAL KEY — where the quads actually are =================
            // The accuracy an elevation cannot give: a looking-forward section of the hull carrying the
            // four engine pods and, clocked 45 deg off them, the four Draco quads. Each quad mark
            // lights from the SAME QuadDuty that lights its ring, so the key can never disagree.
            dl.ArcBand(PX(KeyCX), PY(KeyCY), SZ(58), SZ(62), 0, 360, Detail);
            dl.ArcBand(PX(KeyCX), PY(KeyCY), SZ(22), SZ(25), 0, 360, Hidden);   // the docking ring
            for (int p = 0; p < 4; p++)
                dl.ArcBand(PX(KeyCX), PY(KeyCY), SZ(64), SZ(76), p * 90.0 - 9.0, p * 90.0 + 9.0, Faint);
            for (int q = 0; q < 4; q++)
            {
                float d = QuadDuty(s, q);
                dl.ArcBand(PX(KeyCX), PY(KeyCY), SZ(64), SZ(78),
                           QAz[q] - 12.0, QAz[q] + 12.0, d > 0.01f ? Accent : Detail);
                double az = QAz[q] * Math.PI / 180.0;
                float ux = (float)Math.Sin(az), uy = -(float)Math.Cos(az);
                C(QuadLetter[q], KeyCX + ux * 92f, KeyCY + uy * 92f - 12f, 22,
                  d > 0.01f ? Accent : White);
            }
            C("AXIAL VIEW",       KeyCX, KeyCY + 122f, 22, Detail);
            C("LOOKING FWD",      KeyCX, KeyCY + 152f, 22, Detail);
            // The clocking is tier-2 ARRANGEMENT, not a measurement — say so where the crew can see it.
            C("QUADS CLOCKED",    KeyCX, KeyCY + 210f, 22, Faint);
            C("45 DEG FROM PODS", KeyCX, KeyCY + 240f, 22, Faint);
            C("DRACO ×16",        KeyCX, KeyCY + 300f, 22, Detail);
            C("4 QUADS OF 4",     KeyCX, KeyCY + 330f, 22, Faint);
            C("SUPERDRACO ×8",    KeyCX, KeyCY + 380f, 22, Detail);
            C("4 PODS OF 2",      KeyCX, KeyCY + 410f, 22, Faint);

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
                L(QuadName[q], colX, 1395, 24, Detail);
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
