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
        [Tunable] public static double RaiseTolM = 2000.0;       // reached-co-elliptic tolerance
        [Tunable] public static double SafePeFloorM = 150000.0;  // ⛔ never let a burn drop pe below this
        [Tunable] public static bool CoastWarp = true;           // warp-to-maneuvers through the co-elliptic coast
        [Tunable] public static double CoastWarpFallbackHorizonS = 5400.0; // bounded look-ahead if the period is unusable
        [Tunable] public static double CoastWarpMinRangeM = 120000.0; // never warp inside this of the station (buffer
                                                                      // above the 50 km CW hand-off → realtime terminal approach)

        static RvPhase phase = RvPhase.Idle;
        static bool shroudOpened;
        static bool floorLogged;
        static RendezvousCommand lastCmd;
        static LvlhState lastRel;

        public static void Reset()
        {
            phase = RvPhase.Idle; shroudOpened = false; floorLogged = false;
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

            // ---- FAR FIELD: prograde co-elliptic raise (never CW, never a lowering burn) ----
            if (Phasing.FarField(rangeM, CwHandoffRangeM))
            {
                double rangeRate = RangeRateMps(v, tgtOrbit, now, tgtWorld);   // + = separating, − = closing
                FlyFarFieldRaise(v, tgtOrbit, apAlt, peAlt, rangeM, rangeRate);
                FlightLog.Fill = FillRow;
                return;
            }

            // ---- NEAR FIELD: CW terminal legs (the target is loaded within physics range here) ----
            FlyNearFieldCw(v, body, tgt, now, peAlt, rangeM);
            FlightLog.Fill = FillRow;
        }

        // FAR FIELD — raise the chaser to a co-elliptic orbit below the station, PROGRADE ONLY. Cannot deorbit
        // (prograde raises the orbit); once co-elliptic, coast and let the phase close for the CW hand-off.
        static void FlyFarFieldRaise(Vessel v, Orbit tgtOrbit, double apAlt, double peAlt, double rangeM,
                                     double rangeRateMps)
        {
            double tgtMeanAlt = 0.5 * (tgtOrbit.ApA + tgtOrbit.PeA);
            double coElAlt = Phasing.CoEllipticTargetAltM(tgtMeanAlt, CoEllipticBelowM);

            Vector3d pro = Steering.Prograde(v);
            Steering.Point(v, pro);                              // attitude-first: point the nose prograde
            double perr = Steering.PointingErrorDeg(v, pro);

            bool wantRaise = Phasing.ShouldRaise(apAlt, peAlt, coElAlt, RaiseTolM);
            // prograde raise (forward on the Dracos) once pointed. A CORRECT prograde burn raises pe, so the
            // floor won't trip — but ForwardSign is a first-cut constant, and if it were reversed this "raise"
            // would be retrograde. So the pe floor STILL gates it: a wrong sign is caught at 150 km (the capsule
            // holds safely, above the 140 km atmosphere) instead of self-deorbiting. ⚠ verify ForwardSign +
            // that ap/pe RISE in the CSV; if pe falls, ForwardSign is flipped.
            bool peSafe = Phasing.PeSafe(peAlt, SafePeFloorM);
            if (!peSafe && !floorLogged)
            {
                Debug.LogWarning("[DragonScreen] RV pe-floor (far): pe " + (peAlt / 1000.0).ToString("F0")
                                 + " km ≤ floor " + (SafePeFloorM / 1000.0).ToString("F0") + " km — burns HELD.");
                floorLogged = true;
            }
            if (wantRaise && perr <= AttitudeReadyDeg && peSafe)
                FlightDriver.SetTranslation(0, 0, ForwardSign);
            else
                FlightDriver.ReleaseTranslation();

            // ⭐ WARP-TO-MANEUVERS: once co-elliptic (no raise burn), the chaser just COASTS while the lower/faster
            // orbit closes the phase to the CW hand-off — that can be many orbits (hours). Warp toward the ETA of
            // the range crossing CwHandoffRangeM; the conductor drops out with lead and its universal burn-guard
            // forces realtime the instant any Draco burn is commanded, so warping can never skip a burn. Only
            // while genuinely coasting + pe-safe — a raise burn (above) sets translation and self-cancels the warp.
            if (CoastWarp && !wantRaise && peSafe && rangeM > CoastWarpMinRangeM)
            {
                double horizonS = (tgtOrbit.period > 60.0) ? tgtOrbit.period : CoastWarpFallbackHorizonS;
                double etaS = CoastEta.TimeToRange(rangeM, rangeRateMps, CwHandoffRangeM, horizonS);
                MissionConductor.WarpToEvent(Planetarium.GetUniversalTime() + etaS);
            }
            else
            {
                // raising / inside the terminal buffer / pe-held → no warp; clear any pending target so a stale
                // far-coast warp can't carry into the burn or the near-field approach.
                MissionConductor.Realtime();
            }

            // recorder: keep the far-field visible (phase + range + whether we're raising)
            phase = RvPhase.Phasing;
            lastCmd = new RendezvousCommand
            {
                Phase = RvPhase.Phasing,
                AimLvlh = new Vec3(0, 1, 0),                     // prograde / along-track
                BurnLvlh = new Vec3(0, wantRaise ? 1.0 : 0.0, 0),
                BurnDvMps = wantRaise ? 1.0 : 0.0,
                Burn = wantRaise
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
