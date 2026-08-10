/*
 * DragonScreen - DockingOps
 *
 * GLUE. Picks the ports, flies to the gate, rounds the hull if the path is blocked, holds at the
 * standoff and then runs straight down the port axis. Geometry in `pure/DockGeometry.cs`, speed
 * ladder in `pure/Approach.cs`.
 *
 * Ported from `station_ops.ks:369 StClosestPort` and `falcon9.ks:10246-10310` (the two-move docking)
 * with the keep-out solve at `:10700-10765`.
 *
 * ---- THE WHOLE THING IS TWO MOVES ----
 * falcon9.ks:10246: "Docking is now always the same two moves: go to a point FalconDockStandoff
 * metres directly in front of the target port, then go down the axis." Everything else here exists
 * to get to the first of those without hitting the station.
 *
 * ---- ⚠ AND THE SPEED LADDER STILL GOVERNS ----
 * `Approach.SpeedCap` caps closing speed at 1 m/s inside 100 m, and that band exists because flight
 * 035 "arrived too fast at the end, missed the port and bounced off the hull - 21.95 units of
 * monopropellant on the docking alone, more than the whole approach that delivered it there".
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public enum DockStage : byte
    {
        Idle = 0,
        /// <summary>No usable pair of ports. Says so rather than flying at the hull.</summary>
        NoPort,
        /// <summary>Flying to the gate - the point where the port axis leaves the keep-out sphere.</summary>
        ToGate,
        /// <summary>The direct path cuts the hull; sliding round the sphere instead.</summary>
        Rounding,
        /// <summary>Holding at the standoff, lined up on the axis.</summary>
        Standoff,
        /// <summary>Straight down the port axis.</summary>
        Axial,
        Docked
    }

    public static class DockingOps
    {
        private const string Tag = "[DragonScreen] ";

        public static DockStage Stage { get; private set; }
        public static string Note = "-";
        public static double RangeToPortM, ClosingMps, AxisErrorDeg;

        private static Vessel ship, station;
        private static ModuleDockingNode ourPort, theirPort;
        private static double keepOutR;
        private static double startedAt;

        public static bool Engaged { get; private set; }

        // ------------------------------------------------------------------ lifecycle

        public static void Engage(Vessel v, Vessel target)
        {
            if (v == null || target == null) return;
            ship = v; station = target;
            startedAt = Planetarium.GetUniversalTime();

            if (!PickPorts())
            {
                Stage = DockStage.NoPort;
                Engaged = false;
                Debug.LogWarning(Tag + "docking refused - " + Note);
                return;
            }

            keepOutR = MeasureKeepOut(station);
            Engaged = true;
            Stage = DockStage.ToGate;
            Debug.Log(Tag + "docking engaged - '" + ourPort.part.partInfo.title + "' to '"
                      + theirPort.part.partInfo.title + "', keep-out " + keepOutR.ToString("F0")
                      + " m");
        }

        public static void Disengage(string why)
        {
            if (!Engaged) return;
            Engaged = false;
            Translate(0.0);
            AttitudeController.Ascent.Release(ship);
            Debug.Log(Tag + "docking disengaged - " + why);
        }

        public static void Reset()
        {
            Engaged = false; Stage = DockStage.Idle; Note = "-";
            ship = null; station = null; ourPort = null; theirPort = null;
            RangeToPortM = 0.0; ClosingMps = 0.0; AxisErrorDeg = 0.0;
        }

        /// <summary>
        /// Nearest FREE port of a MATCHING node type. `StClosestPort`.
        ///
        /// Matching the type matters: a Clamp-O-Tron will happily sit a metre from a shielded port
        /// for ever, and "closest" alone would choose it.
        /// </summary>
        private static bool PickPorts()
        {
            ourPort = null; theirPort = null;
            List<ModuleDockingNode> ours = OpenPorts(ship);
            List<ModuleDockingNode> theirs = OpenPorts(station);
            if (ours.Count == 0) { Note = "no free docking port on this vehicle"; return false; }
            if (theirs.Count == 0) { Note = "no free docking port on the station"; return false; }

            double best = double.MaxValue;
            for (int i = 0; i < ours.Count; i++)
            {
                for (int j = 0; j < theirs.Count; j++)
                {
                    if (ours[i].nodeType != theirs[j].nodeType) continue;
                    double d = Vector3d.Distance(ours[i].nodeTransform.position,
                                                 theirs[j].nodeTransform.position);
                    if (d < best) { best = d; ourPort = ours[i]; theirPort = theirs[j]; }
                }
            }
            if (ourPort == null)
            {
                Note = "no port pair of a matching type";
                return false;
            }
            Note = "closest free port is " + best.ToString("F0") + " m away";
            return true;
        }

        private static List<ModuleDockingNode> OpenPorts(Vessel v)
        {
            List<ModuleDockingNode> open = new List<ModuleDockingNode>();
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleDockingNode> ns = v.parts[i].Modules.GetModules<ModuleDockingNode>();
                for (int m = 0; m < ns.Count; m++)
                {
                    if (ns[m].otherNode != null) continue;              // already docked
                    if (ns[m].nodeTransform == null) continue;
                    open.Add(ns[m]);
                }
            }
            return open;
        }

        /// <summary>
        /// The station's bounding radius, measured from its own parts rather than assumed.
        ///
        /// `falcon-station-ferry` is explicit that the station was MEASURED and that its berths are
        /// on arm tips - so a guessed radius is exactly the thing that puts the gate in the wrong
        /// place. The pad is added by DockGeometry, not here.
        /// </summary>
        private static double MeasureKeepOut(Vessel v)
        {
            double furthest = 0.0;
            Vector3d c = v.CoM;
            for (int i = 0; i < v.parts.Count; i++)
            {
                double d = Vector3d.Distance(v.parts[i].transform.position, c);
                if (d > furthest) furthest = d;
            }
            return furthest;
        }

        // ------------------------------------------------------------------ the loop

        public static void Tick()
        {
            if (!Engaged) return;
            if (ship == null || station == null || ourPort == null || theirPort == null
                || ship.state == Vessel.State.DEAD)
            {
                Disengage("vessel or port lost");
                return;
            }

            if (ourPort.otherNode != null)
            {
                Stage = DockStage.Docked;
                Note = "DOCKED";
                Disengage("docked");
                return;
            }

            Vector3d ourPos = ourPort.nodeTransform.position;
            Vector3d tgtPos = theirPort.nodeTransform.position;
            // The port's OUTWARD axis. KSP's docking node points along its transform's forward.
            Vector3d axis = theirPort.nodeTransform.forward.normalized;

            Vector3d toPort = tgtPos - ourPos;
            RangeToPortM = toPort.magnitude;
            Vector3d relVel = station.obt_velocity - ship.obt_velocity;
            ClosingMps = Vector3d.Dot(-relVel, toPort.normalized);

            // The gate: where the port axis leaves the keep-out sphere, plus the pad.
            Vector3d c = station.CoM - tgtPos;                 // port -> station centre
            double gateD = DockGeometry.GateDistanceM(Vector3d.Dot(axis, c), c.sqrMagnitude,
                                                      keepOutR);
            Vector3d gate = tgtPos + axis * gateD;
            Vector3d standoff = tgtPos + axis * DockGeometry.StandoffM;

            Vector3d target;
            if (DockGeometry.AtStandoff((standoff - ourPos).magnitude))
            {
                Stage = DockStage.Axial;
                target = tgtPos;
                Note = "AXIAL - " + RangeToPortM.ToString("F1") + " m";
            }
            else
            {
                Vector3d toGate = gate - ourPos;
                Vector3d cs = station.CoM - ourPos;            // us -> station centre
                bool clear = DockGeometry.PathClear(cs.magnitude, cs.sqrMagnitude,
                                                    Vector3d.Dot(cs, toGate.normalized),
                                                    toGate.magnitude, keepOutR);
                if (clear)
                {
                    Stage = DockStage.ToGate;
                    target = gate;
                    Note = "TO GATE - " + toGate.magnitude.ToString("F0") + " m";
                }
                else
                {
                    // Slide ROUND the sphere rather than driving at it. Recomputed every tick, so
                    // this is a continuous curve rather than a waypoint that has to be reached.
                    Stage = DockStage.Rounding;
                    target = Skirt(ourPos, gate);
                    Note = "ROUNDING HULL";
                }
            }

            FlyTo(ourPos, target, axis);
        }

        /// <summary>
        /// A point abeam the station, in the plane containing us, the centre and the gate, at the
        /// keep-out radius PLUS the pad. Aiming AT the radius is what produced the stuck loop.
        /// </summary>
        private static Vector3d Skirt(Vector3d ourPos, Vector3d gate)
        {
            Vector3d c = station.CoM - ourPos;
            Vector3d side = Vector3d.Exclude(c.normalized, gate - ourPos);
            if (side.magnitude < 1.0)
            {
                // The gate is dead behind the station: any perpendicular will do to start round.
                side = Vector3d.Exclude(c.normalized, (ship.CoM - ship.mainBody.position));
            }
            if (side.magnitude < 1.0) return gate;
            return station.CoM + side.normalized * DockGeometry.SkirtRadiusM(keepOutR);
        }

        /// <summary>
        /// Fly at a point on the speed ladder, with RCS, holding the port axis.
        ///
        /// ⚠ THE ATTITUDE IS THE PORT AXIS, NOT THE DIRECTION OF TRAVEL. The two differ while
        /// rounding the hull, and arriving at a berth pointing where you were going rather than
        /// where the port is means the ports never mate.
        /// </summary>
        private static void FlyTo(Vector3d ourPos, Vector3d target, Vector3d axis)
        {
            double elapsed = Planetarium.GetUniversalTime() - startedAt;
            Vector3d to = target - ourPos;
            double range = to.magnitude;
            Vector3d relVel = station.obt_velocity - ship.obt_velocity;
            double closing = Vector3d.Dot(-relVel, to.sqrMagnitude > 1e-6
                                                   ? to.normalized : Vector3d.zero);
            double lateral = Vector3d.Exclude(to.normalized, -relVel).magnitude;

            TerminalCommand c = Approach.Terminal(range, closing, lateral,
                                                  DockGeometry.StandoffToleranceM * 0.25, elapsed);
            Note += "  " + c.Note;

            // Nose down the port axis, always: -axis is "facing the port".
            AttitudeController.Ascent.SteerTo(ship, -axis, Vector3d.zero);
            AxisErrorDeg = Vector3d.Angle(ship.ReferenceTransform.up, -axis);

            if (!ship.ActionGroups[KSPActionGroup.RCS])
                ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

            if (c.Coast) { Translate(0.0); return; }

            // Translation, not rotation: the capsule holds its attitude on the port and pushes
            // sideways. Fore/aft only for now - lateral RCS is the next thing this needs.
            bool tooSlow = c.WantClosingMps > closing;
            Translate(tooSlow ? 1.0 : -1.0);
        }

        private static void Translate(double fore)
        {
            AttitudeController.Ascent.UllageFore = fore;
        }
    }
}
