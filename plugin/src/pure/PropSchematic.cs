// DragonScreen — PropSchematic  (PURE: the Draco RCS thruster schematic — T9, redrawn by S65)
// ============================================================================================
// SCREEN_INVENTORY.md #26 / BUILD_PLAN.md §3 row "Vehicle · Prop — real look = thruster/RCS schematic"
// / §11b. A REAL Crew Dragon propulsion page, photographed in the NASA JSC crew-training series
// (`jsc2026e404727`, Crew-13 — TIER 1, a real capsule with the screen lit). §11b characterises it:
// "the Dragon drawn in HORIZONTAL profile (capsule + trunk line-art) ringed by the Draco thruster-quad
// arc symbols with per-cluster firing/status, per-thruster data along the bottom, a LEFT alert +
// sub-nav rail". Our LEFT rail is the page's existing subsystem checklist plus VehicleTabBar (whose
// eight tabs are themselves confirmed-real, C1.4 — no tab is added here).
//
// ---- S65: THE VEHICLE IS REDRAWN, THE MODEL BELOW IT IS NOT ----
// The first cut drew the Dragon as a wedge with a blank box for a trunk, and parked all four Draco
// pods at one mid-body station. This file now draws the real ARRANGEMENT — hinged nosecone over the
// docking mechanism, conical pressure vessel, four Draco quads at the FORWARD shoulder, four
// SuperDraco engine pods in raised sidewall fairings further aft, windows, the convex PICA-X base,
// the claw umbilical, and the finned trunk — plus an AXIAL KEY (a looking-forward section) that says
// where the four quads actually sit around the hull, which is the accuracy the drawing owed its own
// caption. ThrusterDuty() / QuadDuty() / MaxDuty() are UNTOUCHED: this task changed the DRAWING,
// never the behaviour (§14.4(f) — a live thing does not regress to static art).
//
// ---- ⛔ LICENCE: REDRAWN, NEVER COPIED (owner decision, 2026-09-04, via the overseer) ----
// The arrangement reference is a commercial third-party SpaceX blueprint poster. It is NOT in this
// repo, must never be added to it, and nothing here is cropped, traced or derived pixel-wise from it.
// This mod is publicly distributed and becomes GPLv3 once MechJeb is embedded (§B2/§B3), so shipping
// that image — even "as reference" — would be redistribution. What was used is the WRITTEN element
// list and arrangement (part names, counts, which part sits where): facts, not protected expression.
// Every line below is our own geometry, authored from that written spec. See docs/ART_SPEC_DRAGON.md.
//
// ---- WHAT IS REAL, WHAT IS OURS (§1.4) ----
// TIER 1 — the LAYOUT GRAMMAR from the JSC photo: horizontal profile, quad arc symbols with
//   per-cluster firing/status, the per-thruster data band along the bottom, the left rail. Kept.
// TIER 1 (repo) — public vehicle facts already shipped in this codebase (VehicleSubsystemPage's own
//   Prop checklist) and independently confirmed by docs/reference/craftdump.csv: 16 Dracos in 4 quads
//   of 4, 8 SuperDracos in 4 pods of 2, NTO/MMH, helium pressurant, a NASA Docking System, a PICA-X
//   heat shield, and a trunk carrying solar array + radiator (§8) and lifting surfaces (the fins).
// TIER 2, MARKED — the ARRANGEMENT: which part sits at which axial station, and the RADIAL clocking
//   (four engine pods 90 deg apart, four Draco quads 90 deg apart and clocked 45 deg off them). Taken
//   from the poster's element list + its three axial views as relayed in the S65 task spec, then
//   REDRAWN. The on-glass "QUADS CLOCKED / 45 DEG FROM PODS" note marks it for the crew.
// OURS — every STRING that is not in that element list: the quad names A-D, the per-thruster
//   designators and their four roles. §11b's verdict on this screen is "layout-real /
//   labels-reconstructed … exact on-screen text is NOT transcribable"; SpaceX's real thruster naming
//   and control allocation are not public. The four propellant readouts and the five detail readouts
//   in the bottom band are the subsystem template's own LIVE values, passed in unchanged.
// NO DIMENSION IS ASSERTED. The profile is drawn to PROPORTION only. Two versions of the poster
//   disagree with each other on overall length and neither matches the vehicle, and §8 / §B11 /
//   craftdump.csv — the three authorities — carry no linear dimension for Dragon at all. So no metre
//   figure is taken from the reference and none is drawn: every number on this page comes from
//   PageState. See docs/ART_SPEC_DRAGON.md, "Open questions for the owner".
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
        /// <summary>Worst case: the profile + its callouts + 4 quad rings + the axial key + the
        /// per-thruster band + the readout columns. Measured at ~250; the headroom keeps the whole
        /// Prop page inside VehicleSubsystemPage.Commands.</summary>
        public const int Commands = 300;
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba Hull   = DragonPalette.Text5;    // the vehicle's own outline
        static readonly Rgba Detail = DragonPalette.Text6;    // labels + secondary structure
        static readonly Rgba Faint  = DragonPalette.Text7;
        static readonly Rgba Hidden = DragonPalette.Hairline; // leaders + detail under a cover
        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Accent = DragonPalette.Accent;

        // ---- the four Draco quads ----
        // The four callout rings sit at the four corners IN CLOCK ORDER, so their placement on the
        // page is their placement around the hull: A upper-right, B lower-right, C lower-left,
        // D upper-left — exactly what the axial key below the vehicle shows against the hull circle.
        static readonly float[] QX = { 2040f, 2040f, 1100f, 1100f };
        static readonly float[] QY = {  320f, 1130f, 1130f,  320f };
        // Roll-axis azimuth of each pod, degrees (instrument convention: 0 at twelve o'clock,
        // increasing clockwise, viewed LOOKING FORWARD). Four pods 90 deg apart, clocked 45 deg off
        // the four engine pods, is the arrangement (tier 2, marked); which pod is called which is ours.
        static readonly double[] QAz = { 45.0, 135.0, 225.0, 315.0 };
        static readonly string[] QuadName = { "QUAD A", "QUAD B", "QUAD C", "QUAD D" };
        static readonly string[] QuadLetter = { "A", "B", "C", "D" };

        // Per-pod thruster roles, in index order, and where each sits on the pod's ring.
        static readonly string[] Role    = { "FWD", "AFT", "LAT", "ROLL" };
        static readonly float[]  TickDeg = { 0f, 180f, 90f, 270f };
        static readonly string[][] Des = {
            new[] { "A1", "A2", "A3", "A4" }, new[] { "B1", "B2", "B3", "B4" },
            new[] { "C1", "C2", "C3", "C4" }, new[] { "D1", "D2", "D3", "D4" } };

        const float QuadRi = 66f, QuadR = 84f;    // duty band; ticks run QuadR+4 .. QuadR+20

        // ---- the vehicle, in horizontal profile: nose LEFT, trunk RIGHT ----
        // PROPORTION ONLY (see the header): capsule, trunk and diameter are drawn in the vehicle's own
        // ratio to each other; no linear dimension is claimed and none is drawn.
        const float HullCY   = 725f;
        const float BaseHH   = 172f;   // heat-shield radius = the widest point
        const float FwdHH    =  85f;   // capsule forward ring, where the nosecone hinges
        const float NoseTipX = 1225f, ShoulderX = 1351f, BaseX = 1603f, ApexX = 1633f;
        const float TrunkX0  = 1653f, TrunkX1 = 1971f;
        const float FinX0    = 1893f, FinHH = 204f;
        const float PodX0    = 1362f, PodX1 = 1432f;    // the Draco quad station (forward shoulder)
        const float FairX0   = 1480f, FairX1 = 1584f;   // the SuperDraco engine-pod fairings
        const float FairH    =   30f;                   // how proud of the sidewall a fairing stands
        const float ConeK    = (BaseHH - FwdHH) / (BaseX - ShoulderX);

        // The nosecone's own profile — a blunt hinged cover, not a point.
        static readonly float[] NoseSx = { 1225f, 1258f, 1305f, 1351f };
        static readonly float[] NoseSh = {   22f,   48f,   72f,   85f };

        // The convex PICA-X base, rim inward: (x, half-height). The shield bulges AFT.
        static readonly float[] ShieldX = { 1603f, 1618f, 1629f, ApexX };
        static readonly float[] ShieldH = {  172f,  142f,   84f,    0f };

        // ---- the axial key (a looking-forward section, under the vehicle) ----
        const float KeyCX = 1570f, KeyCY = 1130f;

        static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }

        /// <summary>Half-height of the drawn profile at design-x, so any callout leader or pod lands
        /// exactly on the hull the renderer actually draws.</summary>
        static float HalfHeight(float x)
        {
            if (x <= NoseTipX) return 0f;
            if (x < ShoulderX)
            {
                for (int i = 1; i < NoseSx.Length; i++)
                    if (x < NoseSx[i])
                        return NoseSh[i - 1] + (x - NoseSx[i - 1]) / (NoseSx[i] - NoseSx[i - 1])
                                             * (NoseSh[i] - NoseSh[i - 1]);
                return FwdHH;
            }
            if (x >= BaseX) return BaseHH;
            return FwdHH + (x - ShoulderX) * ConeK;
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

        /// <summary>The whole cluster's indicator: the hardest-working QUAD, 0..1 — the Prop page's
        /// "Draco Duty" readout (T13b). Derived from the same live RCS demand the schematic's own rings
        /// are drawn from, so the number in the data band and the segments above it are one signal.</summary>
        public static float MaxDuty(PageState s)
        {
            float d = 0f;
            for (int q = 0; q < 4; q++) { float t = QuadDuty(s, q); if (t > d) d = t; }
            return d;
        }

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
            // One stroke drawn on BOTH sides of the roll axis — the profile is symmetric, and saying so
            // once keeps the two halves from drifting apart under a later edit.
            void MIR(float x0, float d0, float x1, float d1, Rgba c)
            { LN(x0, HullCY - d0, x1, HullCY - d1, c); LN(x0, HullCY + d0, x1, HullCY + d1, c); }
            void BOX(float x0, float y0, float x1, float y1, Rgba c) =>
                dl.Box(PX(x0), PY(y0), (x1 - x0) * sx, SZ(y1 - y0), SZ(3f), c);
            // A callout: the reference's own word, and a hairline leader to the thing it names.
            void CO(string t, float tx, float ty, float lx0, float ly0, float lx1, float ly1)
            { L(t, tx, ty, 22, Detail); LN(lx0, ly0, lx1, ly1, Hidden); }

            // ---- zone heading + the real RCS master state ----
            L("DRACO RCS · 16 THRUSTERS IN 4 QUADS", 900, 130, 30, Accent);
            bool rcs = s.Valid && s.RcsOn;
            R(rcs ? "RCS ENABLED" : "RCS DISABLED", 3380, 130, 30, rcs ? DragonPalette.Go : Faint);

            // ================= THE VEHICLE, IN HORIZONTAL PROFILE =================
            // NOSECONE — the hinged cover, blunt tip, hinging at the capsule's forward ring.
            LN(NoseSx[0], HullCY - NoseSh[0], NoseSx[0], HullCY + NoseSh[0], Hull);
            for (int i = 1; i < NoseSx.Length; i++)
                MIR(NoseSx[i - 1], NoseSh[i - 1], NoseSx[i], NoseSh[i], Hull);
            LN(ShoulderX, HullCY - FwdHH, ShoulderX, HullCY + FwdHH, Hidden);   // the hinge line

            // DOCKING MECHANISM — the NASA Docking System ring (craftdump part 19) sits UNDER the
            // closed nosecone, so it is drawn as the ring seen edge-on inside the cover: present,
            // never pretending to be visible from outside.
            LN(1308f, HullCY - 58f, 1346f, HullCY - 58f, Faint);
            LN(1308f, HullCY + 58f, 1346f, HullCY + 58f, Faint);

            // CAPSULE — the conical pressure vessel. The sidewall runs from the forward ring to where
            // the first engine-pod fairing takes over the silhouette.
            MIR(ShoulderX, FwdHH, FairX0, HalfHeight(FairX0), Hull);

            // ENGINE POD / 8x SUPERDRACO ENGINES — four pods of two in raised sidewall fairings, 90 deg
            // apart. Two are edge-on in profile, so over their span the FAIRING is the outline, not the
            // bare cone; the third faces the viewer and projects onto the roll axis; the fourth is
            // hidden behind the vehicle. Two canted nozzles per pod — that is the eight.
            float fh0 = HalfHeight(FairX0) + FairH, fh1 = HalfHeight(FairX1) + FairH;
            for (int side = -1; side <= 1; side += 2)
            {
                LN(FairX0, HullCY + side * HalfHeight(FairX0), 1500f, HullCY + side * fh0, Hull);
                LN(1500f, HullCY + side * fh0, FairX1, HullCY + side * fh1, Hull);
                LN(FairX1, HullCY + side * fh1, BaseX, HullCY + side * BaseHH, Hull);
                LN(1540f, HullCY + side * 178f, 1556f, HullCY + side * 192f, Hull);
                LN(1560f, HullCY + side * 187f, 1576f, HullCY + side * 201f, Hull);
            }
            // The near-side pod, seen face-on: same outline, projected onto the axis.
            LN(1500f, HullCY - 34f, 1580f, HullCY - 34f, Faint);
            LN(1500f, HullCY + 34f, 1580f, HullCY + 34f, Faint);
            LN(1500f, HullCY - 34f, 1486f, HullCY, Faint);
            LN(1486f, HullCY, 1500f, HullCY + 34f, Faint);
            LN(1580f, HullCY - 34f, 1594f, HullCY, Faint);
            LN(1594f, HullCY, 1580f, HullCY + 34f, Faint);

            // HEAT SHIELD — the capsule's blunt base, convex AFT (PICA-X, craftdump part 1).
            for (int i = 1; i < ShieldX.Length; i++)
                MIR(ShieldX[i - 1], ShieldH[i - 1], ShieldX[i], ShieldH[i], Hull);

            // WINDOWS — on the near face; a window off the roll axis projects inboard of the
            // silhouette, which is why they sit inside the outline rather than on it.
            for (int side = -1; side <= 1; side += 2)
            {
                float wy = HullCY + side * 68f - 12f;
                BOX(1445f, wy, 1485f, wy + 24f, Detail);
                BOX(1495f, wy, 1535f, wy + 24f, Detail);
            }

            // 16x DRACO THRUSTERS — four quads at the FORWARD shoulder, 90 deg apart and clocked
            // 45 deg off the engine pods, so in profile two project above the axis and two below, both
            // inboard of the silhouette. Four nozzles on each pod's outer face — that is the sixteen.
            for (int side = -1; side <= 1; side += 2)
            {
                float cy = HullCY + side * 71f, face = cy + side * 13f;
                BOX(PodX0, cy - 13f, PodX1, cy + 13f, Hull);
                for (int n = 0; n < 4; n++)
                {
                    float nx = PodX0 + 10f + n * 18f;
                    LN(nx, face, nx, face + side * 12f, Hull);
                }
            }

            // UMBILICAL — the claw, the trunk-to-capsule thermal/power/avionics link (§8), bridging the
            // heat-shield plane and the trunk's forward ring.
            BOX(1610f, HullCY + 172f, 1690f, HullCY + 204f, Detail);

            // TRUNK — the cylindrical body below the capsule: half solar array (the panel joints above
            // the split), half radiator (the loops below it) per §8, and it carries the fins (craftdump:
            // the trunk is the part holding the lifting surface).
            LN(TrunkX0, HullCY - BaseHH, TrunkX0, HullCY + BaseHH, Hull);
            MIR(TrunkX0, BaseHH, TrunkX1, BaseHH, Hull);
            LN(TrunkX1, HullCY - BaseHH, TrunkX1, HullCY + BaseHH, Hull);
            LN(TrunkX0, HullCY, TrunkX1, HullCY, Hidden);              // the array / radiator split
            LN(1723f, HullCY - BaseHH, 1723f, HullCY, Hidden);
            LN(1793f, HullCY - BaseHH, 1793f, HullCY, Hidden);
            LN(1863f, HullCY - BaseHH, 1863f, HullCY, Hidden);
            LN(1665f, HullCY + 58f, 1959f, HullCY + 58f, Hidden);
            LN(1665f, HullCY + 116f, 1959f, HullCY + 116f, Hidden);

            // FIN — the trunk's aerodynamic stabiliser, flaring aft. Two stand on the silhouette.
            for (int side = -1; side <= 1; side += 2)
            {
                LN(FinX0, HullCY + side * BaseHH, TrunkX1, HullCY + side * FinHH, Hull);
                LN(TrunkX1, HullCY + side * FinHH, TrunkX1, HullCY + side * BaseHH, Hull);
            }

            // ---- callouts: the reference's own element names, on our own geometry ----
            CO("NOSECONE",              1180, 455, 1230, 489, 1268, 668);
            CO("16× DRACO THRUSTERS",   1330, 455, 1400, 489, 1397, 641);
            CO("ENGINE POD",            1600, 455, 1660, 489, 1590, 528);
            CO("WINDOWS",               1180, 950, 1230, 946, 1498, 792);
            CO("8× SUPERDRACO ENGINES", 1330, 950, 1440, 946, 1543, 906);
            CO("UMBILICAL",             1620, 950, 1670, 946, 1660, 930);
            CO("FIN",                   1880, 950, 1900, 946, 1935, 912);

            // ================= THE FOUR QUAD INDICATORS =================
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
                    dl.ArcBand(PX(cx), PY(cy), SZ(QuadR + 4f), SZ(QuadR + 20f),
                               TickDeg[r] - 9f, TickDeg[r] + 9f, td > 0.01f ? Accent : Faint);
                }

                C(QuadName[q], cx, cy - 34f, 26, White);
                C(duty > 0.01f ? "FIRING" : (rcs ? "IDLE" : "OFF"), cx, cy + 2f,
                  22, duty > 0.01f ? Accent : Faint);
            }

            // ================= THE AXIAL KEY — where the quads actually are =================
            // The accuracy a profile cannot give: a looking-forward section of the hull carrying the
            // four engine pods and, clocked 45 deg off them, the four Draco quads. Each quad mark
            // lights from the SAME QuadDuty that lights its ring, so the key can never disagree.
            dl.ArcBand(PX(KeyCX), PY(KeyCY), SZ(72), SZ(76), 0, 360, Hull);
            dl.ArcBand(PX(KeyCX), PY(KeyCY), SZ(28), SZ(32), 0, 360, Hidden);   // the docking ring
            for (int p = 0; p < 4; p++)
                dl.ArcBand(PX(KeyCX), PY(KeyCY), SZ(78), SZ(92), p * 90.0 - 9.0, p * 90.0 + 9.0, Faint);
            for (int q = 0; q < 4; q++)
            {
                float d = QuadDuty(s, q);
                dl.ArcBand(PX(KeyCX), PY(KeyCY), SZ(78), SZ(94),
                           QAz[q] - 12.0, QAz[q] + 12.0, d > 0.01f ? Accent : Detail);
                double az = QAz[q] * Math.PI / 180.0;
                float ux = (float)Math.Sin(az), uy = -(float)Math.Cos(az);
                C(QuadLetter[q], KeyCX + ux * 112f, KeyCY + uy * 112f - 13f, 24,
                  d > 0.01f ? Accent : White);
            }
            C("AXIAL VIEW · LOOKING FWD", KeyCX, KeyCY + 120f, 22, Detail);

            L("DRACO ×16",        1250, 1052, 22, Detail);
            L("4 QUADS OF 4",     1250, 1086, 22, Faint);
            L("SUPERDRACO ×8",    1250, 1136, 22, Detail);
            L("4 PODS OF 2",      1250, 1170, 22, Faint);
            // The clocking is tier-2 ARRANGEMENT, not a measurement — say so where the crew can see it.
            L("QUADS CLOCKED",    1700, 1090, 22, Faint);
            L("45 DEG FROM PODS", 1700, 1124, 22, Faint);

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
