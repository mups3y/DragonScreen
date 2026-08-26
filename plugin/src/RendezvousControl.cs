// DragonScreen — RendezvousControl  (KSP glue: seam 4, the coarse rendezvous controller)
// ============================================================================================
// Flies the Fly(Phasing) step — from the post-insertion orbit down to the Approach-Initiation standoff
// (~7.5 km) — with the PURE guidance (pure/Rendezvous.cs named-burn FSM, pure/Lvlh.cs relative frame,
// pure/Cw.cs + pure/Hohmann.cs). It projects the chaser into the station's LVLH frame, asks Rendezvous
// for the burn, and executes it on the DRACOS.
//
// ⛔ FULL CONTROL, the Dragon has NO reaction wheels (16 Dracos share rotation + translation): so it is
// ATTITUDE-FIRST-THEN-TRANSLATE — point the nose ALONG the burn (SAS), and only once pointed
// (AttitudeReady) translate FORWARD to deliver the Δv; never rotate and translate at once. The forward
// Dracos are SHIELDED by the nose cone, so the shroud is OPENED before any Draco burn ([[dragon-nose-cone
// -rcs]]). Closed-loop: the CW solve re-runs each tick, so the residual Δv shrinks as it's delivered.
// When the range reaches the AI standoff it hands back to the conductor (→ the G9 GO-for-AI gate).
//
// ⚠ FIRST CUT (validate in flight): the RCS translation axis/sign (ForwardSign — a mirrored burn shows in
// the CSV and is a one-constant fix), and the named-burn execution over the long phasing coast (the user
// time-warps between burns). Attitude on the SAS inner loop. Instrumented into the FlightRecorder.
// ============================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class RendezvousControl
    {
        [Tunable] public static double ForwardSign = -1.0;    // KSP forward RCS translation (H key = Z −1)
        [Tunable] public static double AttitudeReadyDeg = 5.0;
        [Tunable] public static double BurnDoneDvMps = 0.02;

        static RvPhase phase = RvPhase.Idle;
        static bool shroudOpened;
        static RendezvousCommand lastCmd;
        static LvlhState lastRel;

        public static void Reset()
        {
            phase = RvPhase.Idle; shroudOpened = false;
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
            if (body == null || tgt == null || tgt.GetOrbit() == null)
            {
                // no station targeted → cannot rendezvous; idle and wait for the crew to target it.
                FlightDriver.ReleaseTranslation();
                return;
            }

            // ---- open the nose shroud before any Draco burn (exposes the forward Dracos + the port) ----
            if (!shroudOpened) { OpenNoseShroud(v); shroudOpened = true; }
            Actuator.EnableRcs(v);   // ⛔ direct: per-thruster rcsEnabled + master (no craft AG binding)

            // ---- relative state in the station LVLH frame ----
            double mu = body.gravParameter;
            Vector3d tgtPos = tgt.GetTransform() != null ? (Vector3d)tgt.GetTransform().position
                                                         : tgt.GetOrbit().getPositionAtUT(Planetarium.GetUniversalTime());
            Vector3d tgtVel = tgt.GetObtVelocity();
            double sma = tgt.GetOrbit().semiMajorAxis;
            double n = Lvlh.MeanMotion(mu, sma);

            Vec3 targetR = V(tgtPos - body.position);
            Vec3 targetV = V(tgtVel);
            Vec3 relPos = V((Vector3d)v.CoM - tgtPos);
            Vec3 relVel = V(v.obt_velocity - tgtVel);
            LvlhState rel = Lvlh.Project(targetR, targetV, relPos, relVel, n);
            lastRel = rel;

            // ---- guidance ----
            RendezvousInputs ri = new RendezvousInputs();
            ri.Valid = true; ri.Rel = rel; ri.N = n; ri.AllNominal = true;
            ri.CoEllipticBelowM = 10000; ri.CoEllipticBehindM = 20000;
            ri.AiRangeM = 7500; ri.CorridorRangeM = 2000;

            // aim = the burn axis (LVLH → world); attitude-ready when the nose is on it
            Vec3 aimL = FirstNonZero(lastCmd.AimLvlh, new Vec3(0, -1, 0));
            Vector3d aimWorld = W(Lvlh.OffsetToWorld(targetR, targetV, aimL.X, aimL.Y, aimL.Z));
            double perr = Steering.PointingErrorDeg(v, aimWorld);
            ri.AttitudeReady = perr <= AttitudeReadyDeg;

            RendezvousCommand cmd = Rendezvous.Guide(ri, phase);
            phase = cmd.Phase;
            lastCmd = cmd;

            // point the nose along the (updated) burn axis
            aimWorld = W(Lvlh.OffsetToWorld(targetR, targetV, cmd.AimLvlh.X, cmd.AimLvlh.Y, cmd.AimLvlh.Z));
            Steering.Point(v, aimWorld);
            perr = Steering.PointingErrorDeg(v, aimWorld);

            // ---- execute: attitude-first, THEN forward-translate to deliver the residual Δv ----
            if (cmd.Burn && perr <= AttitudeReadyDeg && cmd.BurnDvMps > BurnDoneDvMps)
                FlightDriver.SetTranslation(0, 0, ForwardSign);      // forward on the nose (Dracos)
            else
                FlightDriver.ReleaseTranslation();

            // ---- hand back at the AI standoff (→ the G9 GO-for-AI gate) ----
            if (rel.RangeM <= ri.AiRangeM)
            {
                FlightDriver.ReleaseTranslation();
                CrewProcedureOps.PhaseComplete();
            }

            FlightLog.Fill = FillRow;
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

        static void FillRow(string[] row)
        {
            FlightRecorder.PutRendezvous(row, lastCmd, lastRel);
        }
    }
}
