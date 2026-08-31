// DragonScreen — RendezvousControl  (KSP glue: seam 4, the coarse rendezvous controller)
// ============================================================================================
// Flies the Fly(Phasing) step — from the post-insertion orbit to the Approach-Initiation standoff (~7.5 km).
// TWO regimes, split by range (pure/Phasing.FarField):
//
//   • FAR FIELD (thousands → tens of km): the real CO-ELLIPTIC chase. Raise the chaser to a co-elliptic
//     orbit ~10 km BELOW the station by burning PROGRADE ONLY (prograde raises the orbit — it can never
//     lower periapsis, so it CANNOT deorbit). Once co-elliptic, coast: the lower/faster chaser closes the
//     phase. ⛔ CW is NOT used here — at 13,000 km its two-impulse inverse demanded ~28 km/s and the glue
//     fired the Dracos retrograde until the capsule self-deorbited (pe +178 → −143 km, flight 214827).
//
//   • NEAR FIELD (inside CwHandoffRangeM): the CW two-impulse terminal legs (pure/Cw.cs) to OFFSET aim
//     points, exactly as before — valid here because CW's linearisation holds within tens of km.
//
// ⛔ CREW-SAFETY FLOOR (independent of any guidance solve): no burn may fire while periapsis is at/below
// SafePeFloorM (pure/Phasing.PeSafe). The far-field raise is prograde-only so it can't trip it; the near-
// field CW is gated by it — a garbage command can never walk the orbit down into re-entry. The intentional
// ABORT deorbit does not go through here.
//
// ⛔ FULL CONTROL, no reaction wheels (16 Dracos share rotation + translation): ATTITUDE-FIRST-THEN-TRANSLATE
// — point the nose ALONG the burn axis, and only once pointed translate forward; never rotate + translate at
// once. The forward Dracos are shielded by the nose cone, so the shroud is OPENED first ([[dragon-nose-cone
// -rcs]]). Instrumented into the FlightRecorder.
// ============================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class RendezvousControl
    {
        [Tunable] public static double ForwardSign = -1.0;      // ⭐ DERIVED + FLIGHT-CONFIRMED: forward burn =
                                                                // s.Z = −Dot(A, ct.up). Nose points prograde
                                                                // (ct.up = nose) → s.Z=−1 raised apoapsis on
                                                                // flight 131412 (200→419 km). Anchors the
                                                                // DockingControl RCS sign derivation (all −1).
        // ⭐ C1 propellant-budget SAFETY NET (2026-08-31, DS-ASC-003): every rendezvous translation goes through
        // RvTranslate, which INHIBITS the burn once the return propellant (worst-case MMH/NTO fraction) falls to this
        // floor, then HOLDS + warns. Its job is to prevent the DS-ASC-003 total drain — 0% = no attitude control = a
        // certainly-dead crew — and to hold back a modest margin for a deorbit/return attempt, so the operator can act
        // on the reserve instead of stranding. It protects the RETURN, not the dock (it holds short rather than spend
        // the reserve). ⚠ At the Dragon's ~21% RCS efficiency, a full dock AND a full return do not both fit the tank
        // (docs/FLIGHT_VERIFICATION.md) — this floor is deliberately MODEST (a catastrophe-preventer, not a return
        // guarantee) so it does not block a within-budget A1 dock; SIZE IT to the measured deorbit cost on re-fly.
        [Tunable] public static double RvReturnReserveFrac = 0.20;
        static bool rvReserveWarned;

        // The guarded translation used by EVERY rendezvous burn (far-field / Lambert / near-field CW). Fires only while
        // the return propellant is above the reserve; below it, nulls the translation (hold) and surfaces it once.
        static void RvTranslate(Vessel v, double x, double y, double z)
        {
            double rf = (v != null) ? DockedSide.ReturnFraction(v) : 1.0;
            if (rf <= RvReturnReserveFrac)
            {
                FlightDriver.SetTranslation(0.0, 0.0, 0.0);   // hold — protect the deorbit/return budget
                if (!rvReserveWarned)
                {
                    rvReserveWarned = true;
                    Debug.LogWarning("[DragonScreen] RV propellant reserve: return prop " + (rf * 100.0).ToString("F0")
                        + "% ≤ reserve " + (RvReturnReserveFrac * 100.0).ToString("F0")
                        + "% — rendezvous translation INHIBITED to protect the deorbit/return budget (holding).");
                    ScreenMessages.PostScreenMessage("Rendezvous held — return-propellant reserve reached", 6f,
                        ScreenMessageStyle.UPPER_CENTER);
                }
                return;
            }
            FlightDriver.SetTranslation(x, y, z);
        }
        [Tunable] public static double AttitudeReadyDeg = 5.0;
        // ⭐ Campaign 1 (C2a): far-field coast prograde re-acquire band. On a far-field COAST/PHASE we re-acquire
        // prograde only after drifting past this, then release the attitude channel (drift, no RCS) — a hysteresis
        // that keeps us within ~this of prograde. MUST stay < AttitudeReadyDeg so a re-acquire leaves us burn-ready.
        [Tunable] public static double CoastReacquireDeg = 3.0;
        [Tunable] public static double BurnDoneDvMps = 0.02;
        [Tunable] public static double CwHandoffRangeM = 100000.0;// far→near split. The phase-timed transfer brings the
                                                                 // chaser to ~80 km at apoapsis (131412: 86 km, 165302:
                                                                 // 79 km); a 50 km split NEVER caught that → fly-past.
                                                                 // 100 km catches it so CW takes over on approach.
                                                                 // (CW's own guard bounds it to 200 km, so this is safe.)
        [Tunable] public static double CoEllipticBelowM = 10000.0;// co-elliptic parking height below the station
        [Tunable] public static double RaiseTolM = 2000.0;       // reached-co-elliptic tolerance (ap-raise + pe-circularize)
        [Tunable] public static double SafePeFloorM = 150000.0;  // ⛔ never let a burn drop pe below this
        [Tunable] public static bool CoastWarp = true;           // warp-to-maneuvers through the co-elliptic coast
        [Tunable] public static double CoastWarpFallbackHorizonS = 5400.0; // bounded look-ahead if the period is unusable
        [Tunable] public static double CoastWarpMinRangeM = 120000.0; // never warp inside this of the station (buffer
                                                                      // above the 50 km CW hand-off → realtime terminal approach)

        static RvPhase phase = RvPhase.Idle;
        static FarPhase farPhase = FarPhase.Phase;   // far-field transfer FSM state
        static FarPhase lastFarPhase = FarPhase.Phase; // for one-shot transition logging
        static bool shroudOpened;
        static bool floorLogged;
        static RendezvousCommand lastCmd;
        static LvlhState lastRel;

        // ⭐ STRICT-FIDELITY REL-NAV (NavFilter, B6) — the real Crew Dragon flies terminal rendezvous on a
        // FILTERED relative state (rel-GPS + IMU fused through a Dragonfly-class filter), never on perfect
        // truth ([[crew2-full-fidelity-no-deviation]]). KSP hands us truth, so we SIMULATE the rel-GPS from it
        // (+ noise), fuse it through NavFilter, and fly the CW guidance on the ESTIMATE — matching the real
        // pipeline and proving the guidance is robust to realistic nav error. Tunable: false = fly on truth.
        // Wired here (near-field, where the rel-state feeds CW); DockingControl is the next place to wire it.
        [Tunable] public static bool UseNavFilter = true;
        static NavState3 navRel;
        static bool navInit;
        static uint navRng = 0x1234567u;
        static double navLastLogUT = -999.0;

        // ⛔ DETUMBLE-AT-ENTRY (flight 194334 root cause). The capsule arrives from ascent with a large,
        // UNCONTROLLED roll rate: the single-engine S2 has no roll authority (ctrl_tq_roll=0 all through S2) and
        // RCS attitude is off while the engine is lit, so roll builds monotonically to ~54 dps by SECO. A spinning
        // capsule can NEVER hold prograde — the nose traces a cone, pitch/yaw thrash chasing it (att_err 50–177°,
        // ±90 dps), the burn gate perr≤5° is never met so the far-field never translates (trans_z=0 the whole
        // flight), and the thrash burns 23% of the Draco MMH/NTO. VERIFIED FIX (against the loop code, not comments):
        // hold CURRENT attitude first — worldDir = v.ReferenceTransform.up gives ZERO pitch/yaw error (from the
        // Quaternion.LookRotation frame math), so the RollControlRange gate opens AND, with no pitch/yaw slew, the
        // shared Dracos are FREE for the roll velocity loop to null the rate in seconds. One-shot: settle once at
        // entry, then normal guidance (which rate-limits its own slews). Never warp/burn while tumbling.
        [Tunable] public static double SettleRateDps = 2.0;   // detumble until the body rate is below this
        [Tunable] public static double SettleMaxS = 90.0;     // …but never hold longer than this (proceed on residual)
        static bool settleDone;
        static double settleStartUT = -1.0;
        static double settleLastLogUT = -999.0;

        // ⭐ LAMBERT MID-FIELD INTERCEPT (pure/RvIntercept, over the tested Lambert BVP solver). The phase-timed
        // co-elliptic raise is coarse (raise-to-parking + wait); a Lambert two-impulse intercept flies a DIRECT,
        // exact, arbitrary-geometry transfer to where the station WILL be — the proper intercept MechJeb uses.
        // ⛔ SAFE-BY-CONSTRUCTION: RvIntercept only ever returns a plan whose TRANSFER-ORBIT periapsis ≥ the pe
        // floor, so even a partly-retrograde intercept can never walk the orbit into re-entry (the failure mode
        // that made raw CW dangerous at range). DEFAULT OFF — flight-gated: CW/Hohmann stay the default until a
        // flight tunes this on. Closed-loop on a LATCHED arrival UT: re-solve to the fixed arrival each tick so
        // the residual departure Δv shrinks as the burn is flown, then coast to the CW hand-off.
        [Tunable] public static bool UseLambertIntercept = false;
        [Tunable] public static double LambertMinRangeM = 30000.0;   // below → hand to the CW terminal legs (don't Lambert)
        [Tunable] public static double LambertMaxRangeM = 300000.0;  // above → single-rev Lambert unreliable → phase-timed raise
        [Tunable] public static double LambertMinTofS = 45.0;        // abandon a plan whose arrival is inside this
        [Tunable] public static double LambertBurnDoneDvMps = 0.05;  // residual departure Δv that ends the intercept burn
        enum LambPhase : byte { Idle, Burn, Coast }
        static LambPhase lambPhase = LambPhase.Idle;
        static double lambArrivalUT;
        static bool lambShortWay = true;
        static double lambPlannedDv, lambLastLogUT = -999.0;

        // ---- FDIR feed (task T2b): the honest NEAR-FIELD closing signal for the ConvergenceStall monitor. The
        // controller is the honest source of intent — only it knows when it MEANT to be closing (a near-field CW
        // burn toward the standoff) vs coasting (the far-field phase-wait / co-elliptic coast, where a "stall" is
        // meaningless). FlightDriver.TickFdir feeds these; far-field / idle leave NearClosingActive false so an
        // intended coast is never a stall. + = closing (progressing), ≤0 = not closing while actively closing.
        public static double NearClosingRateMps;
        public static bool NearClosingActive;

        public static void Reset()
        {
            phase = RvPhase.Idle; farPhase = FarPhase.Phase; lastFarPhase = FarPhase.Phase;
            shroudOpened = false; floorLogged = false; rvReserveWarned = false;
            navInit = false;   // re-init the strict-fidelity rel-nav filter on a new rendezvous
            settleDone = false; settleStartUT = -1.0;   // re-arm the detumble-at-entry gate
            lambPhase = LambPhase.Idle;   // re-arm the Lambert intercept FSM on a new rendezvous
            NearClosingRateMps = 0.0; NearClosingActive = false;
            FlightDriver.ReleaseTranslation();
            Steering.Release();
        }

        public static bool HasTarget(Vessel v)
        {
            return v != null && v.targetObject != null && v.targetObject.GetOrbit() != null;
        }

        public static void Tick(Vessel v, MissionProfile mission)
        {
            try { Fly(v, mission); }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] rendezvous tick failed: " + e.Message);
                FlightDriver.ReleaseTranslation();
            }
        }

        static void Fly(Vessel v, MissionProfile mission)
        {
            CelestialBody body = v.mainBody;
            ITargetable tgt = v.targetObject;
            Orbit tgtOrbit = tgt != null ? tgt.GetOrbit() : null;
            if (body == null || tgt == null || tgtOrbit == null)
            {
                // no station targeted → cannot rendezvous; idle and wait for the crew to target it.
                NearClosingActive = false;   // FDIR: not closing → stall monitor stays nominal
                FlightDriver.ReleaseTranslation();
                return;
            }

            // ---- open the nose shroud before any Draco burn (exposes the forward Dracos + the port) ----
            if (!shroudOpened) { OpenNoseShroud(v); shroudOpened = true; }
            Actuator.EnableRcs(v);   // ⛔ direct: per-thruster rcsEnabled + master (no craft AG binding)

            double now = Planetarium.GetUniversalTime();
            // ⛔ ROBUST relative range from the ORBIT, not the transform. The ISS is UNLOADED far out on the pad
            // and through phasing, and its transform position is a placeholder there — reading it gave a bogus
            // 13,000 km separation and fed CW the garbage that self-deorbited us. getPositionAtUT is the world
            // frame (same convention as AscentControl.TargetPlaneNormal, which flies the correct plane).
            Vector3d tgtWorld = tgtOrbit.getPositionAtUT(now);
            double rangeM = (v.CoM - tgtWorld).magnitude;

            double peAlt = v.orbit != null ? v.orbit.PeA : 0.0;
            double apAlt = v.orbit != null ? v.orbit.ApA : double.MaxValue;

            // ⛔ DETUMBLE FIRST (one-shot at entry): kill the ascent-induced tumble before any guidance points or
            // burns. Hold CURRENT attitude (nose at v.ReferenceTransform.up → zero pitch/yaw error → thrusters
            // free → roll rate nulled), never warp while tumbling. See the field comment for the full root cause.
            if (!settleDone)
            {
                double rateDps = v.angularVelocity.magnitude * (180.0 / Math.PI);
                if (settleStartUT < 0.0) settleStartUT = now;
                bool timedOut = (now - settleStartUT) > SettleMaxS;     // insurance: never hold the mission forever
                if (rateDps > SettleRateDps && !timedOut)
                {
                    MissionConductor.Realtime();                        // never warp/burn while tumbling
                    Steering.Point(v, v.ReferenceTransform.up);         // hold current attitude = kill rotation (all axes)
                    FlightDriver.ReleaseTranslation();
                    NearClosingActive = false;
                    if (now - settleLastLogUT > 3.0)
                    {
                        Debug.Log("[DragonScreen] RV detumble: body rate " + rateDps.ToString("F1")
                                  + " dps > " + SettleRateDps.ToString("F1") + " — holding attitude to settle before phasing");
                        settleLastLogUT = now;
                    }
                    phase = RvPhase.Phasing;
                    lastCmd = new RendezvousCommand { Phase = RvPhase.Phasing, AimLvlh = new Vec3(0, 1, 0), Burn = false };
                    lastRel = new LvlhState { Rx = 0, Ry = -rangeM, Rz = 0 };
                    FlightLog.Fill = FillRow;
                    return;
                }
                settleDone = true;
                Debug.Log("[DragonScreen] RV settled — body rate " + rateDps.ToString("F1") + " dps"
                          + (timedOut ? " (settle TIMED OUT at " + SettleMaxS.ToString("F0") + " s — proceeding on residual)" : "")
                          + ", beginning phasing (range " + (rangeM / 1000.0).ToString("F0") + " km)");
            }

            // ---- FAR FIELD: phase-timed Hohmann transfer (never CW, never a lowering burn) ----
            if (Phasing.FarField(rangeM, CwHandoffRangeM))
            {
                NearClosingActive = false;   // FDIR: far-field coasts/raises are NOT monotonic closing → stall stays nominal
                double rangeRate = RangeRateMps(v, tgtOrbit, now, tgtWorld);   // + = separating, − = closing
                FlyFarField(v, tgtOrbit, apAlt, peAlt, rangeM, rangeRate, now);
                FlightLog.Fill = FillRow;
                return;
            }

            // ---- NEAR FIELD: CW terminal legs (the target is loaded within physics range here) ----
            FlyNearFieldCw(v, body, tgt, now, peAlt, rangeM);
            FlightLog.Fill = FillRow;
        }

        // FAR FIELD — the phase-timed Hohmann transfer (pure/Phasing.FarGuide): PHASE (coast+warp on the low, fast
        // insertion orbit until the phase angle reaches the Hohmann lead) → TRANSFER (burn prograde to raise
        // apoapsis to the station's altitude, then STOP — the fix for the flight-103303 200→772 over-raise) →
        // COAST (warp up to apoapsis, where the chaser arrives near the station and the range drops into CW's
        // regime). Prograde-only + the pe floor → the far field can never deorbit. The Hohmann timing is the
        // tested pure/Hohmann.cs; here the glue only computes the live phase angle + executes.
        static void FlyFarField(Vessel v, Orbit tgtOrbit, double apAlt, double peAlt, double rangeM,
                                double rangeRateMps, double now)
        {
            // ⭐ Lambert mid-field intercept (default OFF): if enabled and it can fly a pe-safe direct intercept
            // this tick, it OWNS the far field (burn/coast) and we skip the phase-timed raise. It hands back by
            // simply not claiming the tick once the range drops into the CW near-field (outer Fly() switches).
            if (TryLambertIntercept(v, tgtOrbit, rangeM, peAlt, now)) return;

            CelestialBody body = v.mainBody;
            double mu = body.gravParameter;
            Vector3d bc = body.position;
            Vector3d rc = (Vector3d)v.CoM - bc;                         // chaser radius vector (world)
            Vector3d rt = tgtOrbit.getPositionAtUT(now) - bc;          // station radius vector (world)
            Vector3d hHat = Vector3d.Cross(rc, v.obt_velocity);        // chaser orbit normal (prograde sense)
            if (hHat.magnitude > 1e-6) hHat = hHat.normalized;
            // signed phase angle: target AHEAD of chaser, measured prograde about the orbit normal → [0, 2π)
            double signed = Math.Atan2(Vector3d.Dot(Vector3d.Cross(rc, rt), hHat), Vector3d.Dot(rc, rt));
            double phaseNow = signed < 0.0 ? signed + 2.0 * Math.PI : signed;
            double r1 = rc.magnitude, r2 = rt.magnitude;
            double o1 = Math.Sqrt(mu / (r1 * r1 * r1)), o2 = Math.Sqrt(mu / (r2 * r2 * r2));

            // Raise APOAPSIS to just below the station (CoEllipticBelowM under it) so the coast-up carries the
            // chaser to ~just below the station near apoapsis — then CW takes over (the low-thrust circularize
            // was removed: it drifted 246→6,000 km over ~27 orbits; the fix was the wider CW hand-off).
            double parkAltM = (r2 - body.Radius) - CoEllipticBelowM;

            FarInputs fi = new FarInputs
            {
                PhaseNowRad = phaseNow,
                PhaseLeadRad = Hohmann.PhaseLeadRad(r1, r2, mu),
                Omega1 = o1, Omega2 = o2,
                ApAltM = apAlt, TargetAltM = parkAltM, RaiseTolM = RaiseTolM,
                PeAltM = peAlt, FloorM = SafePeFloorM
            };
            FarCommand fc = Phasing.FarGuide(fi, farPhase);
            farPhase = fc.Phase;
            if (farPhase != lastFarPhase)
            {
                Debug.Log("[DragonScreen] RV far-field: " + lastFarPhase + " → " + farPhase
                          + "  (ap " + (apAlt / 1000.0).ToString("F0") + " pe " + (peAlt / 1000.0).ToString("F0")
                          + " park " + (parkAltM / 1000.0).ToString("F0") + " km, range "
                          + (rangeM / 1000.0).ToString("F0") + " km)");
                lastFarPhase = farPhase;
            }

            // attitude-first: point prograde, burn only once pointed. FarGuide already gates Burn on the pe floor.
            // ⭐ RvCoast release-gate REMOVED (owner 2026-09-01, "remove Claude-invented loops"): the invented
            // coast re-acquire/RELEASE hysteresis is gone — just HOLD prograde continuously (MechJeb-style).
            Vector3d pro = Steering.Prograde(v);
            double perr = Steering.PointingErrorDeg(v, pro);
            Steering.Point(v, pro);
            if (fc.PeHeld && !floorLogged)
            {
                Debug.LogWarning("[DragonScreen] RV pe-floor (far): pe " + (peAlt / 1000.0).ToString("F0")
                                 + " km ≤ floor " + (SafePeFloorM / 1000.0).ToString("F0") + " km — burns HELD.");
                floorLogged = true;
            }
            if (fc.Burn && perr <= AttitudeReadyDeg)
                RvTranslate(v, 0, 0, ForwardSign);   // prograde raise on the Dracos (guarded by the return reserve)
            else
                FlightDriver.ReleaseTranslation();

            // ⭐ WARP-TO-MANEUVERS. PHASE: warp toward the phase-alignment UT (the long wait for the window).
            // COAST: warp toward the range closing into CW's regime. TRANSFER (burning) or inside the terminal
            // buffer: realtime (the conductor's burn-guard also forces 1× on any Draco burn, so a burn is never
            // run under warp). CoastEta gives the range a self-correcting ETA; WaitTimeS gives the phase one.
            if (CoastWarp && farPhase == FarPhase.Phase && fc.WaitS > 0.0)
            {
                MissionConductor.WarpToEvent(now + fc.WaitS);   // WarpPlan.ShouldWarp ignores gaps too short to bother
            }
            else if (CoastWarp && farPhase == FarPhase.Coast && rangeM > CoastWarpMinRangeM)
            {
                double horizonS = (tgtOrbit.period > 60.0) ? tgtOrbit.period : CoastWarpFallbackHorizonS;
                double etaS = CoastEta.TimeToRange(rangeM, rangeRateMps, CwHandoffRangeM, horizonS);
                MissionConductor.WarpToEvent(now + etaS);
            }
            else
            {
                MissionConductor.Realtime();   // transferring, or inside the buffer → no warp
            }

            // recorder: keep the far-field visible. Map the far FSM onto the RvPhase enum so the CSV rv_phase
            // column shows WHICH far state flew: Phase/Transfer→Phasing, Coast→ApproachInit.
            phase = RvPhase.Phasing;
            RvPhase recPhase = (farPhase == FarPhase.Coast) ? RvPhase.ApproachInit : RvPhase.Phasing;
            lastCmd = new RendezvousCommand
            {
                Phase = recPhase,
                AimLvlh = new Vec3(0, 1, 0),                     // prograde / along-track
                BurnLvlh = new Vec3(0, fc.Burn ? 1.0 : 0.0, 0),
                BurnDvMps = fc.Burn ? 1.0 : 0.0,
                Burn = fc.Burn
            };
            lastRel = new LvlhState { Rx = 0, Ry = -rangeM, Rz = 0 };
        }

        // ⭐ LAMBERT MID-FIELD INTERCEPT executor (pure/RvIntercept). Returns TRUE if it owns this tick (it
        // planned/burned/coasted a direct two-impulse intercept), FALSE to let the phase-timed raise fly. The
        // burn is closed-loop on a LATCHED arrival UT: each tick re-solve the departure Δv to the fixed arrival
        // (the residual shrinks as the burn is delivered), point the nose along it, translate forward once
        // pointed. ⛔ Every solve is pe-floor-gated inside RvIntercept, so a burn can never route through
        // re-entry; if a solve ever comes back unsafe/degenerate we bail to the co-elliptic raise.
        static bool TryLambertIntercept(Vessel v, Orbit tgtOrbit, double rangeM, double peAlt, double now)
        {
            if (!UseLambertIntercept) return false;
            CelestialBody body = v.mainBody;
            double mu = body.gravParameter;

            if (lambPhase == LambPhase.Idle)
            {
                if (rangeM < LambertMinRangeM || rangeM > LambertMaxRangeM) return false;
                if (tgtOrbit == null || tgtOrbit.period <= 0.0) return false;
                if (!EngageLambert(v, tgtOrbit, body, mu, now)) return false;   // no pe-safe plan → the raise flies
            }

            double tof = lambArrivalUT - now;

            if (lambPhase == LambPhase.Burn)
            {
                // arrival passed / range fell into the CW near-field → finish the intercept, hand on.
                if (tof < LambertMinTofS || rangeM < LambertMinRangeM) { EndLambert(); return false; }
                InterceptPlan p = PlanToArrival(v, tgtOrbit, body, mu, now, tof, lambShortWay);
                if (!p.Ok || !p.PeSafe) { EndLambert(); return false; }   // ⛔ safety: bail to the raise if unsafe

                MissionConductor.Realtime();                              // a Draco burn is never run under warp
                Vector3d aim = W(p.DepartureDv);
                Steering.Point(v, aim);
                double perr = Steering.PointingErrorDeg(v, aim);
                bool burning = false;
                if (p.DepartMagMps <= LambertBurnDoneDvMps)
                {
                    lambPhase = LambPhase.Coast; FlightDriver.ReleaseTranslation();
                    Debug.Log("[DragonScreen] LAMBERT burn complete → coast to intercept (range "
                              + (rangeM / 1000.0).ToString("F0") + " km)");
                }
                else if (perr <= AttitudeReadyDeg) { RvTranslate(v, 0, 0, ForwardSign); burning = true; }
                else FlightDriver.ReleaseTranslation();

                if (now - lambLastLogUT > 5.0)
                {
                    Debug.Log("[DragonScreen] LAMBERT burn: residual dv " + p.DepartMagMps.ToString("F2")
                              + " m/s, perr " + perr.ToString("F1") + "°, tof " + tof.ToString("F0") + " s");
                    lambLastLogUT = now;
                }
                RecordLambert(rangeM, burning ? p.DepartMagMps : 0.0);
                return true;
            }

            // COAST — hold prograde (continuously, MechJeb-style; the invented RvCoast release-gate is removed);
            // warp toward the latched arrival. Hands back when the range drops into the CW near-field.
            if (rangeM < LambertMinRangeM) { EndLambert(); return false; }
            FlightDriver.ReleaseTranslation();
            Vector3d pro = Steering.Prograde(v);
            Steering.Point(v, pro);
            if (CoastWarp && tof > LambertMinTofS && rangeM > CoastWarpMinRangeM)
                MissionConductor.WarpToEvent(lambArrivalUT);
            else
                MissionConductor.Realtime();
            RecordLambert(rangeM, 0.0);
            return true;
        }

        // Scan the tof band (both transfer-angle branches) using KSP's exact target ephemeris; latch the cheapest
        // pe-safe intercept under the cost cap. Returns false (→ phase-timed raise) when nothing qualifies.
        static bool EngageLambert(Vessel v, Orbit tgtOrbit, CelestialBody body, double mu, double now)
        {
            double period = tgtOrbit.period;
            if (period <= 0.0) return false;
            double lo = RvIntercept.TofMinFrac * period, hi = RvIntercept.TofMaxFrac * period;
            int n = RvIntercept.TofSamples;
            InterceptPlan best = new InterceptPlan(); double bestTof = 0.0; bool bestWay = true;
            for (int i = 0; i < n; i++)
            {
                double f = n > 1 ? (double)i / (n - 1) : 0.0;
                double tof = lo + (hi - lo) * f;
                if (tof < LambertMinTofS) continue;
                for (int s = 0; s < 2; s++)
                {
                    InterceptPlan p = PlanToArrival(v, tgtOrbit, body, mu, now, tof, s == 0);
                    if (!p.Ok || !p.PeSafe || p.DepartMagMps > RvIntercept.MaxDvMps) continue;
                    if (!best.Ok || p.DepartMagMps < best.DepartMagMps) { best = p; bestTof = tof; bestWay = (s == 0); }
                }
            }
            if (!best.Ok) return false;
            lambArrivalUT = now + bestTof; lambShortWay = bestWay; lambPlannedDv = best.DepartMagMps;
            lambPhase = LambPhase.Burn;
            Debug.Log("[DragonScreen] LAMBERT engage: dv " + best.DepartMagMps.ToString("F1") + " m/s, tof "
                      + bestTof.ToString("F0") + " s, transfer pe " + (best.TransferPeM / 1000.0).ToString("F0")
                      + " km, " + (bestWay ? "short" : "long") + " way");
            return true;
        }

        // Build the frame-consistent Lambert inputs and solve to a FIXED arrival. r1 is Earth-centered NOW; r2 is
        // Earth-centered AT ARRIVAL (subtract the body's own position at arrival, so the body's translation over
        // the tof cancels — the one correction the same-instant far-field code doesn't need). Chaser velocity is
        // the world-frame obt_velocity (same frame the working far-field phase-angle already uses).
        static InterceptPlan PlanToArrival(Vessel v, Orbit tgtOrbit, CelestialBody body, double mu,
                                           double now, double tof, bool shortWay)
        {
            double arrival = now + tof;
            Vec3 r1 = V((Vector3d)v.CoM - body.position);
            Vector3d bodyAtArrival = body.orbit != null ? body.orbit.getPositionAtUT(arrival) : (Vector3d)body.position;
            Vec3 r2 = V(tgtOrbit.getPositionAtUT(arrival) - bodyAtArrival);
            Vec3 vC = V(v.obt_velocity);
            return RvIntercept.Plan(r1, vC, r2, mu, body.Radius, tof, SafePeFloorM, shortWay);
        }

        static void EndLambert() { lambPhase = LambPhase.Idle; FlightDriver.ReleaseTranslation(); }

        static void RecordLambert(double rangeM, double burnDv)
        {
            phase = RvPhase.Phasing;
            lastCmd = new RendezvousCommand
            {
                Phase = RvPhase.Phasing,
                AimLvlh = new Vec3(0, 1, 0),
                BurnLvlh = new Vec3(0, burnDv > 0.0 ? 1.0 : 0.0, 0),
                BurnDvMps = burnDv,
                Burn = burnDv > 0.0
            };
            lastRel = new LvlhState { Rx = 0, Ry = -rangeM, Rz = 0 };
        }

        // NEAR FIELD — the CW terminal legs to offset aim points, gated by the crew-safety pe floor.
        static void FlyNearFieldCw(Vessel v, CelestialBody body, ITargetable tgt, double now,
                                   double peAlt, double rangeM)
        {
            MissionConductor.Realtime();   // terminal approach is flown at 1× (precision + short legs); clears any warp
            double mu = body.gravParameter;
            // ⛔ Use the ORBIT position unless the target is LOADED (within physics range). An UNLOADED vessel's
            // transform is a STALE PLACEHOLDER (frozen at unload / origin) — at the ~100 km CW hand-off, and until
            // the ISS loads at ~2.5 km (or the PhysicsRangeExtender range), it is unloaded, so reading its transform
            // fed the CW garbage → the terminal approach never converged (oc-nearfield). getPositionAtUT is the same
            // accurate world frame the far-field uses. The transform only takes over for the final precision metres.
            Vessel tv = tgt.GetVessel();
            bool tgtLoaded = tv != null && tv.loaded && tgt.GetTransform() != null;
            Vector3d tgtPos = tgtLoaded ? (Vector3d)tgt.GetTransform().position
                                        : tgt.GetOrbit().getPositionAtUT(now);
            Vector3d tgtVel = tgt.GetObtVelocity();
            double sma = tgt.GetOrbit().semiMajorAxis;
            double n = Lvlh.MeanMotion(mu, sma);

            Vec3 targetR = V(tgtPos - body.position);
            Vec3 targetV = V(tgtVel);
            Vec3 relPos = V((Vector3d)v.CoM - tgtPos);
            Vec3 relVel = V(v.obt_velocity - tgtVel);
            // ⭐ strict-fidelity rel-nav: fuse a simulated rel-GPS (truth + noise) through NavFilter and fly the
            // guidance on the ESTIMATE (real Dragon flies filtered rel-nav, not truth). Instrumented (rate-limited
            // est-vs-truth log). The estimate tracks truth within a few m (RgpsNoise 5 m « the km-scale CW legs),
            // so this adds realism/robustness without regressing the geometry. Tunable off → fly on truth.
            if (UseNavFilter)
            {
                double dtf = TimeWarp.fixedDeltaTime;
                if (!navInit || dtf <= 0.0) { navRel = NavState3.Init(relPos, relVel); navInit = true; }
                else
                {
                    navRel.Predict(new Vec3(0, 0, 0), dtf);   // coast model (thrust is short + GPS-corrected each tick)
                    navRel.UpdatePosition(new Vec3(relPos.X + NavNoise(NavFilter.RgpsNoiseM),
                                                   relPos.Y + NavNoise(NavFilter.RgpsNoiseM),
                                                   relPos.Z + NavNoise(NavFilter.RgpsNoiseM)));
                    Vec3 e = navRel.EstPos, ev = navRel.EstVel;
                    if (now - navLastLogUT > 5.0)
                    {
                        double dx = e.X - relPos.X, dy = e.Y - relPos.Y, dz = e.Z - relPos.Z;
                        Debug.Log("[DragonScreen] NAV rel-filter |err| " + Math.Sqrt(dx * dx + dy * dy + dz * dz).ToString("F1")
                                  + " m  (est range " + e.Magnitude.ToString("F0") + " / truth " + relPos.Magnitude.ToString("F0") + ")");
                        navLastLogUT = now;
                    }
                    relPos = e; relVel = ev;
                }
            }
            LvlhState rel = Lvlh.Project(targetR, targetV, relPos, relVel, n);
            lastRel = rel;

            RendezvousInputs ri = new RendezvousInputs();
            ri.Valid = true; ri.Rel = rel; ri.N = n; ri.AllNominal = true;
            ri.CoEllipticBelowM = CoEllipticBelowM; ri.CoEllipticBehindM = 20000;
            ri.AiRangeM = 7500; ri.CorridorRangeM = 2000;

            Vec3 aimL = FirstNonZero(lastCmd.AimLvlh, new Vec3(0, -1, 0));
            Vector3d aimWorld = W(Lvlh.OffsetToWorld(targetR, targetV, aimL.X, aimL.Y, aimL.Z));
            double perr = Steering.PointingErrorDeg(v, aimWorld);
            ri.AttitudeReady = perr <= AttitudeReadyDeg;

            RendezvousCommand cmd = Rendezvous.Guide(ri, phase);
            phase = cmd.Phase;
            lastCmd = cmd;

            aimWorld = W(Lvlh.OffsetToWorld(targetR, targetV, cmd.AimLvlh.X, cmd.AimLvlh.Y, cmd.AimLvlh.Z));
            Steering.Point(v, aimWorld);
            perr = Steering.PointingErrorDeg(v, aimWorld);

            // ⛔ pe-floor gate: a CW leg can carry a retrograde component — never fire it below the safety floor.
            bool peSafe = Phasing.PeSafe(peAlt, SafePeFloorM);
            if (!peSafe && !floorLogged)
            {
                Debug.LogWarning("[DragonScreen] RV pe-floor: pe " + (peAlt / 1000.0).ToString("F0")
                                 + " km ≤ floor " + (SafePeFloorM / 1000.0).ToString("F0") + " km — burns HELD.");
                floorLogged = true;
            }
            bool closingBurn = cmd.Burn && perr <= AttitudeReadyDeg && cmd.BurnDvMps > BurnDoneDvMps && peSafe;
            if (closingBurn)
                RvTranslate(v, 0, 0, ForwardSign);      // forward on the nose (Dracos, guarded by the return reserve)
            else
                FlightDriver.ReleaseTranslation();

            // ---- FDIR feed (T2b): while ACTIVELY closing to the standoff, publish the honest closing rate
            //      (+ = closing). LOS closing rate = −(relVel · r̂). Both terms are the same obt-velocity/world
            //      frame already used for the CW solve, so no frame mix. Only active when we are burning to
            //      close AND still outside the AI standoff — an intended hold/coast leaves it false (nominal).
            Vector3d relPosW = (Vector3d)v.CoM - tgtPos;
            double losRange = relPosW.magnitude;
            NearClosingRateMps = losRange > 1e-3
                ? -Vector3d.Dot(v.obt_velocity - tgtVel, relPosW / losRange) : 0.0;
            NearClosingActive = closingBurn && rel.RangeM > ri.AiRangeM;

            // ---- hand back at the AI standoff (→ the G9 GO-for-AI gate) ----
            if (rel.RangeM <= ri.AiRangeM)
            {
                NearClosingActive = false;   // arrived → not closing anymore (stall monitor stays nominal at the gate)
                FlightDriver.ReleaseTranslation();
                CrewProcedureOps.PhaseComplete();
            }
        }

        static void OpenNoseShroud(Vessel v)
        {
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    List<ModuleAnimateGeneric> an = v.parts[i].Modules.GetModules<ModuleAnimateGeneric>();
                    for (int m = 0; m < an.Count; m++)
                        if (an[m].animationName == "TE_23_CD2_NOSECONE_ANI" && an[m].Progress < 0.5f)
                        { an[m].Toggle(); Debug.Log("[DragonScreen] nose shroud OPENED (forward Dracos exposed)"); return; }
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] nose shroud open failed: " + e.Message); }
        }

        static Vec3 V(Vector3d d) { return new Vec3(d.x, d.y, d.z); }
        static Vector3d W(Vec3 p) { return new Vector3d(p.X, p.Y, p.Z); }
        static Vec3 FirstNonZero(Vec3 a, Vec3 fallback) { return a.Magnitude > 1e-6 ? a : fallback; }

        // Deterministic pseudo-gaussian sensor noise (LCG + Box-Muller) to simulate the rel-GPS 1σ error.
        static double NavNoise(double sigma)
        {
            navRng = navRng * 1664525u + 1013904223u; double u1 = (((navRng >> 8) & 0xFFFFFF) + 1) / 16777217.0;
            navRng = navRng * 1664525u + 1013904223u; double u2 = ((navRng >> 8) & 0xFFFFFF) / 16777216.0;
            return sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        // Signed relative range-rate (m/s; + = separating, − = closing) for the warp ETA. Both positions come
        // from getPositionAtUT, so they share the SAME world-frame convention (no swizzle, no CoM-vs-focus mix)
        // — the safe way to get a rate without touching the two orbital-velocity frames. dt=10 s smooths tick
        // noise. Falls back to 0 (treated as "not closing" → a bounded look-ahead warp) if the orbit is unusable.
        static double RangeRateMps(Vessel v, Orbit tgtOrbit, double now, Vector3d tgtWorldNow)
        {
            try
            {
                if (v.orbit == null) return 0.0;
                const double dt = 10.0;
                double r0 = (v.orbit.getPositionAtUT(now) - tgtWorldNow).magnitude;
                double r1 = (v.orbit.getPositionAtUT(now + dt) - tgtOrbit.getPositionAtUT(now + dt)).magnitude;
                return (r1 - r0) / dt;
            }
            catch { return 0.0; }
        }

        static void FillRow(string[] row)
        {
            FlightRecorder.PutRendezvous(row, lastCmd, lastRel);
        }
    }
}
