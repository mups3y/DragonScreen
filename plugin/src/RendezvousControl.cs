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
        [Tunable] public static double ForwardSign = -1.0;      // KSP forward RCS translation (H key = Z −1)
        [Tunable] public static double AttitudeReadyDeg = 5.0;
        [Tunable] public static double BurnDoneDvMps = 0.02;
        [Tunable] public static double CwHandoffRangeM = 50000.0; // far→near split (CW valid within tens of km)
        [Tunable] public static double CoEllipticBelowM = 10000.0;// co-elliptic parking height below the station
        [Tunable] public static double RaiseTolM = 2000.0;       // reached-co-elliptic tolerance (ap-raise + pe-circularize)
        [Tunable] public static double NearApoWindowS = 20.0;    // "at apoapsis" window → circularize burn only fires here
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

        public static void Reset()
        {
            phase = RvPhase.Idle; farPhase = FarPhase.Phase; lastFarPhase = FarPhase.Phase;
            shroudOpened = false; floorLogged = false;
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

            // ---- FAR FIELD: phase-timed Hohmann transfer (never CW, never a lowering burn) ----
            if (Phasing.FarField(rangeM, CwHandoffRangeM))
            {
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

            // Park CO-ELLIPTIC: raise BOTH apses to CoEllipticBelowM UNDER the station, not up to it — a slightly
            // lower, near-circular orbit that dwells just below/near the station for CW to close (never a bare
            // touch-and-go at apoapsis). "At apoapsis" is a small time window so the circularize burn raises pe.
            double parkAltM = (r2 - body.Radius) - CoEllipticBelowM;
            double timeToAp = v.orbit.timeToAp, per = v.orbit.period;
            bool nearApo = per > 1.0 && Math.Min(timeToAp, per - timeToAp) < NearApoWindowS;

            FarInputs fi = new FarInputs
            {
                PhaseNowRad = phaseNow,
                PhaseLeadRad = Hohmann.PhaseLeadRad(r1, r2, mu),
                Omega1 = o1, Omega2 = o2,
                ApAltM = apAlt, TargetAltM = parkAltM, RaiseTolM = RaiseTolM,
                PeAltM = peAlt, NearApoapsis = nearApo, FloorM = SafePeFloorM
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
            Vector3d pro = Steering.Prograde(v);
            Steering.Point(v, pro);
            double perr = Steering.PointingErrorDeg(v, pro);
            if (fc.PeHeld && !floorLogged)
            {
                Debug.LogWarning("[DragonScreen] RV pe-floor (far): pe " + (peAlt / 1000.0).ToString("F0")
                                 + " km ≤ floor " + (SafePeFloorM / 1000.0).ToString("F0") + " km — burns HELD.");
                floorLogged = true;
            }
            if (fc.Burn && perr <= AttitudeReadyDeg)
                FlightDriver.SetTranslation(0, 0, ForwardSign);   // prograde raise on the Dracos
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
            else if (CoastWarp && farPhase == FarPhase.Circularize && !nearApo && per > 1.0)
            {
                MissionConductor.WarpToEvent(now + timeToAp);   // warp the half-orbit coast up to apoapsis to circularize
            }
            else if (CoastWarp && farPhase == FarPhase.Coast && rangeM > CoastWarpMinRangeM)
            {
                double horizonS = (tgtOrbit.period > 60.0) ? tgtOrbit.period : CoastWarpFallbackHorizonS;
                double etaS = CoastEta.TimeToRange(rangeM, rangeRateMps, CwHandoffRangeM, horizonS);
                MissionConductor.WarpToEvent(now + etaS);
            }
            else
            {
                MissionConductor.Realtime();   // transferring/circularizing at apoapsis, or inside the buffer → no warp
            }

            // recorder: keep the far-field visible (sub-phase + range + whether we're burning). Map the far FSM
            // onto the RvPhase enum so the CSV rv_phase column shows WHICH far state flew: Phase/Transfer→Phasing,
            // Circularize→CoElliptic, Coast→ApproachInit.
            phase = RvPhase.Phasing;
            RvPhase recPhase = (farPhase == FarPhase.Circularize) ? RvPhase.CoElliptic
                             : (farPhase == FarPhase.Coast)       ? RvPhase.ApproachInit
                             : RvPhase.Phasing;
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

        // NEAR FIELD — the CW terminal legs to offset aim points, gated by the crew-safety pe floor.
        static void FlyNearFieldCw(Vessel v, CelestialBody body, ITargetable tgt, double now,
                                   double peAlt, double rangeM)
        {
            MissionConductor.Realtime();   // terminal approach is flown at 1× (precision + short legs); clears any warp
            double mu = body.gravParameter;
            Vector3d tgtPos = tgt.GetTransform() != null ? (Vector3d)tgt.GetTransform().position
                                                         : tgt.GetOrbit().getPositionAtUT(now);
            Vector3d tgtVel = tgt.GetObtVelocity();
            double sma = tgt.GetOrbit().semiMajorAxis;
            double n = Lvlh.MeanMotion(mu, sma);

            Vec3 targetR = V(tgtPos - body.position);
            Vec3 targetV = V(tgtVel);
            Vec3 relPos = V((Vector3d)v.CoM - tgtPos);
            Vec3 relVel = V(v.obt_velocity - tgtVel);
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
            if (cmd.Burn && perr <= AttitudeReadyDeg && cmd.BurnDvMps > BurnDoneDvMps && peSafe)
                FlightDriver.SetTranslation(0, 0, ForwardSign);      // forward on the nose (Dracos)
            else
                FlightDriver.ReleaseTranslation();

            // ---- hand back at the AI standoff (→ the G9 GO-for-AI gate) ----
            if (rel.RangeM <= ri.AiRangeM)
            {
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
