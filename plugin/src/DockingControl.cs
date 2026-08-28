// DragonScreen — DockingControl  (KSP glue: seam 5, the terminal approach + docking)
// ============================================================================================
// Flies the L-approach from the AI standoff to soft capture with the PURE guidance (pure/DockControl.cs
// glideslope servo, pure/Lvlh.cs). The mode manager holds the WP0/WP1/WP2 station-keeps as the G10/G11/G12
// CREW GATES, so this controller flies ONE leg at a time: it reads which gate is next (WP0 400 m below →
// WP1 ~220 m front → WP2 20 m → contact) and drives DockControl to that LVLH point on the DRACOS, then
// signals PhaseComplete when the waypoint is reached so the crew's GO at the gate releases the next leg.
//
// ⛔ ATTITUDE-FIRST-THEN-TRANSLATE: point the docking ring at the port (SAS), then the servo translates
// (X/Y/Z Dracos). The closing-speed cap tapers to ~8 cm/s at contact so the approach stays slow and
// abortable; the crew's ABORT on the gate routes to the responder. The nose shroud is already open (seam 4).
//
// The RCS translation signs are DERIVED (not flight-guessed) — see the sign block below. The servo gains
// (KPos/KVel) and arrival tolerances remain first-cut tunables validated from the CSV. Attitude on SAS.
// Instrumented into the FlightRecorder.
// ============================================================================================
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class DockingControl
    {
        // RCS translation sign map (world demand → FlightCtrlState X/Y/Z). ⭐ DERIVED from MechJeb's proven
        // RCS controller (MechJebModuleRCSController.Drive): it expresses the world velocity error in the
        // control-transform local frame (Quaternion.Inverse(GetTransform().rotation)·worldVec) and writes
        //   s.X = local.x (right),  s.Y = local.z (forward),  s.Z = local.y (up)   ← the y/z SWAP we replicate
        // uniformly POSITIVE off the velocity ERROR = (current − target). Our demandWorld is the desired
        // ACCELERATION A = −error, so the correct sign on every axis is −Dot(A, axis): ALL THREE are −1.
        // Flight-anchored: RendezvousControl's prograde burn uses s.Z = −1 with the nose (=ct.up) prograde and
        // raised apoapsis correctly (flight 131412) — proving s.Z = −Dot(A,up); the same uniform mechanism
        // (MechJeb assigns all three axes identically) gives −1 on right and forward too. (Was +1/+1 on
        // right/up — unreasoned defaults that INVERT those axes → the servo pushes lateral error the wrong
        // way → docking diverges off the corridor. −1/−1/−1 makes all three close the error.)
        [Tunable] public static double RcsRightSign = -1.0;  // s.X = −Dot(demand, ct.right)
        [Tunable] public static double RcsUpSign = -1.0;     // s.Y = −Dot(demand, ct.forward) (dorsal)
        [Tunable] public static double RcsFwdSign = -1.0;    // s.Z = −Dot(demand, ct.up) (nose/fore)

        [Tunable] public static double FarSpeedMps = 20.0;   // closing cap far out
        [Tunable] public static double ContactSpeedMps = 0.08;
        [Tunable] public static double TaperRangeM = 400.0;
        [Tunable] public static double KPos = 0.1, KVel = 1.0;
        [Tunable] public static double ArriveTolM = 10.0;    // "reached this waypoint" (contact uses ContactTol)
        public const double ContactTolM = 0.4;

        static DockControl.Demand lastDemand;
        static LvlhState lastRel;

        public static void Reset()
        {
            FlightDriver.ReleaseTranslation();
            Steering.Release();
        }

        public static void Tick(Vessel v, MissionProfile mission)
        {
            try { Fly(v); }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] docking tick failed: " + e.Message);
                FlightDriver.ReleaseTranslation();
            }
        }

        static void Fly(Vessel v)
        {
            CelestialBody body = v.mainBody;
            ITargetable tgt = v.targetObject;
            if (body == null || tgt == null || tgt.GetOrbit() == null)
            { FlightDriver.ReleaseTranslation(); return; }

            Actuator.EnableRcs(v);   // ⛔ direct: per-thruster rcsEnabled + master (no craft AG binding)

            // ---- relative state in the station LVLH frame ----
            // ⛔ transform only when the target is LOADED (physics range); else the orbit position — an unloaded
            // vessel's transform is a stale placeholder (see RendezvousControl.FlyNearFieldCw). Docking's final
            // metres need the loaded port transform; the approach uses the orbit until the station loads.
            double mu = body.gravParameter;
            Vessel tv = tgt.GetVessel();
            bool tgtLoaded = tv != null && tv.loaded && tgt.GetTransform() != null;
            Vector3d tgtPos = tgtLoaded ? (Vector3d)tgt.GetTransform().position
                                        : tgt.GetOrbit().getPositionAtUT(Planetarium.GetUniversalTime());
            Vector3d tgtVel = tgt.GetObtVelocity();
            double n = Lvlh.MeanMotion(mu, tgt.GetOrbit().semiMajorAxis);
            Vec3 targetR = V(tgtPos - body.position), targetV = V(tgtVel);
            Vec3 relPos = V((Vector3d)v.CoM - tgtPos), relVel = V(v.obt_velocity - tgtVel);
            LvlhState rel = Lvlh.Project(targetR, targetV, relPos, relVel, n);
            lastRel = rel;

            // ---- which leg? the upcoming crew gate identifies the target waypoint ----
            double tx, ty, tz; bool contact;
            WaypointFor(CrewProcedureOps.NextGateId, out tx, out ty, out tz, out contact);

            // ---- glideslope servo toward the waypoint (LVLH) ----
            double errR = rel.Rx - tx, errA = rel.Ry - ty, errC = rel.Rz - tz;
            DockControl.Demand d = DockControl.Translate(errR, errA, errC, rel.Vx, rel.Vy, rel.Vz,
                                                         ContactSpeedMps, FarSpeedMps, TaperRangeM, KPos, KVel);
            lastDemand = d;

            // ---- aim the docking ring at the port (toward the station) ----
            Vector3d aim = tgtPos - (Vector3d)v.CoM;
            Steering.Point(v, aim);

            // ---- world demand → control-frame Draco translation ----
            Vector3d demandWorld = W(Lvlh.OffsetToWorld(targetR, targetV, d.Radial, d.Along, d.Cross));
            Transform ct = v.ReferenceTransform;
            if (ct != null && demandWorld.magnitude > 1e-6)
            {
                double sx = RcsRightSign * Vector3d.Dot(demandWorld, ct.right);
                double sy = RcsUpSign * Vector3d.Dot(demandWorld, ct.forward);
                double sz = RcsFwdSign * Vector3d.Dot(demandWorld, ct.up);
                FlightDriver.SetTranslation(sx, sy, sz);
            }
            else FlightDriver.ReleaseTranslation();

            // ---- arrival / capture → hand back to the conductor (the crew's GO releases the next leg) ----
            double range = Math.Sqrt(errR * errR + errA * errA + errC * errC);
            if (contact)
            {
                if (DockedSide.Docked(v) || rel.RangeM <= ContactTolM)
                { FlightDriver.ReleaseTranslation(); CrewProcedureOps.PhaseComplete(); }
            }
            else if (range <= ArriveTolM)
            { FlightDriver.ReleaseTranslation(); CrewProcedureOps.PhaseComplete(); }

            FlightLog.Fill = FillRow;
        }

        // The LVLH waypoint for the leg leading to the next gate (matches DockApproach's geometry).
        static void WaypointFor(GateId next, out double x, out double y, out double z, out bool contact)
        {
            x = 0; y = 0; z = 0; contact = false;
            switch (next)
            {
                case GateId.WP0HoldG10: x = -400; break;   // 400 m below (−radial)
                case GateId.WP1HoldG11: y = 220; break;    // ~220 m front on the V-bar
                case GateId.WP2DockGoG12: y = 20; break;   // 20 m
                default: contact = true; break;            // close to the port (0,0,0)
            }
        }

        static Vec3 V(Vector3d d) { return new Vec3(d.x, d.y, d.z); }
        static Vector3d W(Vec3 p) { return new Vector3d(p.X, p.Y, p.Z); }

        static void FillRow(string[] row)
        {
            DockCommand dc = new DockCommand { Phase = DockPhase.Contact, Hold = false };
            FlightRecorder.PutDocking(row, dc, lastRel.RangeM, lastDemand.ClosingCapMps);
        }
    }
}
