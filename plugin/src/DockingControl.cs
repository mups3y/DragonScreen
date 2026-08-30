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

        // ⭐ KOS AUTO-ABORT (real Crew Dragon: any unplanned KEEP-OUT-SPHERE penetration OFF the approach
        // corridor commands a RETREAT — SEQUENCE_MAP §1A / PHASE_4_DOCKING_RESEARCH). The pure DockCorridor test
        // is enforced ONLY on the V-bar terminal legs (toward WP2 + contact), where the nominal path holds the
        // docking axis; the R-bar climb and the WP0→WP1 swing legitimately arc through the KOS boundary and are
        // NOT gated (a blind check would false-abort the corner-cut). ⚠ CorridorConeDeg is the researched
        // ~10° cone (exact SpaceX value not public — §1A) to confirm from a flown approach.
        [Tunable] public static double KosRadiusM = 200.0;
        [Tunable] public static double CorridorConeDeg = 10.0;
        [Tunable] public static double CorridorMinHalfWidthM = 5.0;
        static bool kosAbortRaised;

        // ⭐ STRICT-FIDELITY TERMINAL REL-NAV (NavFilter, B6). The docking servo is the place nav precision
        // matters MOST — the real Dragon flies the sub-metre soft-capture on its DragonEye LIDAR/optical relative
        // nav, not truth. As in the rendezvous wire, we SIMULATE the sensor from KSP truth (+ noise) and fly the
        // servo on the ESTIMATE — but here the measurement 1σ is RANGE-SCHEDULED (rel-GPS → LIDAR) via
        // NavFilter.TerminalSensorNoiseM, so the noise collapses to cm-class in close and never wrecks the dock.
        // Tunable: false = fly on truth.
        [Tunable] public static bool UseNavFilter = true;
        static NavState3 navRel;
        static bool navInit;
        static uint navRng = 0x2468ACEu;   // independent seed from the rendezvous filter
        static double navLastLogUT = -999.0;

        static DockControl.Demand lastDemand;
        static LvlhState lastRel;

        public static void Reset()
        {
            kosAbortRaised = false;
            navInit = false;   // re-init the terminal rel-nav filter on a new docking approach
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
            double now = Planetarium.GetUniversalTime();
            Vessel tv = tgt.GetVessel();
            bool tgtLoaded = tv != null && tv.loaded && tgt.GetTransform() != null;
            Vector3d tgtPos = tgtLoaded ? (Vector3d)tgt.GetTransform().position
                                        : tgt.GetOrbit().getPositionAtUT(now);
            Vector3d tgtVel = tgt.GetObtVelocity();
            double n = Lvlh.MeanMotion(mu, tgt.GetOrbit().semiMajorAxis);
            Vec3 targetR = V(tgtPos - body.position), targetV = V(tgtVel);
            Vec3 relPos = V((Vector3d)v.CoM - tgtPos), relVel = V(v.obt_velocity - tgtVel);
            // ⭐ strict-fidelity terminal rel-nav: fuse the range-scheduled sensor (rel-GPS→LIDAR) through
            // NavFilter and fly the servo on the ESTIMATE. Near the port the LIDAR 1σ is cm-class, so the
            // estimate error « the 0.4 m capture gate; the KSP magnetic-capture truth stays the primary signal.
            if (UseNavFilter)
            {
                double dtf = TimeWarp.fixedDeltaTime;
                double sensor = NavFilter.TerminalSensorNoiseM(relPos.Magnitude);   // true range picks the sensor
                if (!navInit || dtf <= 0.0) { navRel = NavState3.Init(relPos, relVel); navInit = true; }
                else
                {
                    navRel.Predict(Vec3.Zero, dtf);
                    navRel.UpdatePosition(new Vec3(relPos.X + NavNoise(sensor), relPos.Y + NavNoise(sensor),
                                                   relPos.Z + NavNoise(sensor)), sensor);
                    Vec3 e = navRel.EstPos, ev = navRel.EstVel;
                    if (now - navLastLogUT > 5.0)
                    {
                        double dx = e.X - relPos.X, dy = e.Y - relPos.Y, dz = e.Z - relPos.Z;
                        Debug.Log("[DragonScreen] DOCK rel-filter |err| " + Math.Sqrt(dx * dx + dy * dy + dz * dz).ToString("F3")
                                  + " m  (sensor 1σ " + sensor.ToString("F2") + " m, range " + relPos.Magnitude.ToString("F0") + ")");
                        navLastLogUT = now;
                    }
                    relPos = e; relVel = ev;
                }
            }
            LvlhState rel = Lvlh.Project(targetR, targetV, relPos, relVel, n);
            lastRel = rel;

            // ---- which leg? the upcoming crew gate identifies the target waypoint ----
            GateId nextGate = CrewProcedureOps.NextGateId;
            double tx, ty, tz; bool contact;
            WaypointFor(nextGate, out tx, out ty, out tz, out contact);

            // ---- KOS auto-abort: on the V-bar terminal legs (toward WP2 + contact), an off-corridor KOS
            // penetration → RETREAT (routes through the abort responder → KosRetreat, since we are near the
            // station). NOT enforced on the R-bar/WP0→WP1 legs (they arc through the boundary by design). ----
            bool vbarLeg = contact || nextGate == GateId.WP2DockGoG12;
            if (vbarLeg && !kosAbortRaised
                && DockCorridor.Breached(rel, KosRadiusM, CorridorConeDeg * Math.PI / 180.0, CorridorMinHalfWidthM))
            {
                kosAbortRaised = true;
                Debug.LogWarning("[DragonScreen] ⛔ KOS BREACH — off the approach corridor inside the "
                                 + KosRadiusM.ToString("F0") + " m keep-out sphere (range " + rel.RangeM.ToString("F0")
                                 + " m) — ABORT (retreat).");
                FlightDriver.ReleaseTranslation();
                FlightDriver.RequestAbort();
                return;
            }

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
                // KSP's own docking magnetism (DockedSide.Docked) is the authoritative capture signal — a real
                // dock always completes. The geometric fallback additionally requires the IDSS soft-capture
                // envelope (closing/lateral/offset/angle/rate ≤ IDSS IDD Rev E limits), so a fast or skewed
                // fly-through at the contact tolerance does NOT count as a clean capture; the servo keeps
                // nulling until inside the box. ⭐ SEQUENCE_MAP §1A / DockCapture.
                if (DockedSide.Docked(v) || (rel.RangeM <= ContactTolM && WithinCaptureEnvelope(v, rel, aim)))
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

        // Measure the IDSS soft-capture envelope from the live relative state (LVLH) + attitude, and test it.
        // closing = axial rate toward the port (origin); lateral rate = the perpendicular speed; lateral offset
        // = the radial distance from the V-bar axis; angle = the docking-ring pointing error; angular rate from
        // the vessel body rate. DockCapture holds the IDSS IDD Rev E limits.
        static bool WithinCaptureEnvelope(Vessel v, LvlhState rel, Vector3d aim)
        {
            double rmag = Math.Sqrt(rel.Rx * rel.Rx + rel.Ry * rel.Ry + rel.Rz * rel.Rz);
            double closing = rmag > 1e-6 ? -((rel.Rx * rel.Vx + rel.Ry * rel.Vy + rel.Rz * rel.Vz) / rmag) : 0.0;
            double vmag2 = rel.Vx * rel.Vx + rel.Vy * rel.Vy + rel.Vz * rel.Vz;
            double lateralRate = Math.Sqrt(Math.Max(0.0, vmag2 - closing * closing));
            double lateralOffset = Math.Sqrt(rel.Rx * rel.Rx + rel.Rz * rel.Rz);
            double angleDeg = Steering.PointingErrorDeg(v, aim);
            double angRateDegS = v.angularVelocity.magnitude * 180.0 / Math.PI;
            return DockCapture.WithinEnvelope(closing, lateralRate, lateralOffset, angleDeg, angRateDegS, DockCapture.Idss());
        }

        static Vec3 V(Vector3d d) { return new Vec3(d.x, d.y, d.z); }
        static Vector3d W(Vec3 p) { return new Vector3d(p.X, p.Y, p.Z); }

        // Deterministic pseudo-gaussian sensor noise (LCG + Box-Muller) to simulate the rel-nav 1σ error.
        static double NavNoise(double sigma)
        {
            navRng = navRng * 1664525u + 1013904223u; double u1 = (((navRng >> 8) & 0xFFFFFF) + 1) / 16777217.0;
            navRng = navRng * 1664525u + 1013904223u; double u2 = ((navRng >> 8) & 0xFFFFFF) / 16777216.0;
            return sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        static void FillRow(string[] row)
        {
            DockCommand dc = new DockCommand { Phase = DockPhase.Contact, Hold = false };
            FlightRecorder.PutDocking(row, dc, lastRel.RangeM, lastDemand.ClosingCapMps);
        }
    }
}
