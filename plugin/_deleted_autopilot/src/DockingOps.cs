// DragonScreen - DockingOps
// ---- THE WHOLE THING IS TWO MOVES ----
// ---- ⚠ AND THE SPEED LADDER STILL GOVERNS ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class DockingOps
    {
        private const string Tag = "[DragonScreen] ";

        public static DockStage Stage { get; private set; }
        public static string Note = "-";
        public static double RangeToPortM, ClosingMps, AxisErrorDeg;

        public static double DistF, DistS, DistT, VelF, VelS, VelT;

        private static Vessel ship, station;

        // ---- ⚠ THE PIDs LIVE HERE, NOT INSIDE THE SOLVER. ----
        private static readonly Pid pidF = new Pid();
        private static readonly Pid pidS = new Pid();
        private static readonly Pid pidT = new Pid();
        // ---- F9I's LOAD RANGES FOR THE TARGET. `station_ops.ks:1147-1150`, verbatim. ----
        private const double TargetUnloadM = 25000.0;
        private const double TargetLoadM   = 10050.0;
        private const double TargetUnpackM =  2100.0;
        private const double TargetPackM   =  2250.0;
        private const double TargetLoadTimeoutS = 30.0;

        [Tunable] public static double DockEnvelopeM = 300.0;
        [Tunable] public static double DockMaxRelSpeedMps = 2.0;

        [Tunable] public static double DockRollSign = 1.0;

        private static double loadWaitStartedAt;

        private static ModuleDockingNode ourPort, theirPort;
        private static double keepOutR;
        private static double startedAt;

        public static bool Engaged { get; private set; }

        // ------------------------------------------------------------------ lifecycle

        public static void Engage(Vessel v, Vessel target)
        {
            pidF.Reset(); pidS.Reset(); pidT.Reset();
            if (v == null || target == null) return;

            // ---- ⛔ THE DOCKING ENVELOPE. RCS CANNOT FLY A KILOMETRE-SCALE, FAST APPROACH. ----
            double engRangeM = Vector3d.Distance(v.CoM, target.CoM);
            double engRelMps = (target.obt_velocity - v.obt_velocity).magnitude;
            if (engRangeM > DockEnvelopeM || engRelMps > DockMaxRelSpeedMps)
            {
                Note = "TOO FAR/FAST TO DOCK - " + engRangeM.ToString("F0") + " m at "
                     + engRelMps.ToString("F1") + " m/s. Rendezvous to within "
                     + DockEnvelopeM.ToString("F0") + " m and under "
                     + DockMaxRelSpeedMps.ToString("F1") + " m/s first.";
                Stage = DockStage.NoPort;
                Engaged = false;
                Debug.LogWarning(Tag + "docking refused - " + Note);
                return;
            }

            ship = v; station = target;
            startedAt = Planetarium.GetUniversalTime();

            RaiseTargetRange(target);

            if (!target.loaded)
            {
                Engaged = true;
                Stage = DockStage.AwaitingTarget;
                loadWaitStartedAt = startedAt;
                Note = "WAITING FOR THE STATION TO LOAD";
                Debug.Log(Tag + "docking: '" + target.vesselName + "' is "
                          + (Vector3d.Distance(v.CoM, target.CoM) / 1000.0).ToString("F1")
                          + " km away and not loaded - its ports cannot be read yet. Range raised to "
                          + (TargetLoadM / 1000.0).ToString("F1") + " km, waiting up to "
                          + TargetLoadTimeoutS.ToString("F0") + " s.");
                return;
            }

            if (!PickPorts())
            {
                Stage = DockStage.NoPort;
                Engaged = false;
                Debug.LogWarning(Tag + "docking refused - " + Note);
                return;
            }

            // ---- ⛔ TARGET THE PORT ITSELF, NOT THE VESSEL. ----
            SetTarget(theirPort, "docking engaged");
            ControlFromPort();
            DockShroud.Open(ship);
            keepOutR = MeasureKeepOut(station);
            bestRangeM = double.MaxValue; bestRangeAt = 0.0;
            reached = DockStage.Idle;
            legRangeM = 0.0; lastWaypoint = DockWaypoint.None;
            Engaged = true;
            Commit(DockStage.ToGate);
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
            RestoreControlPoint();
            Debug.Log(Tag + "docking disengaged - " + why);
        }

        private static Part savedRefPart;

        private static void ControlFromPort()
        {
            try
            {
                if (ship == null || ourPort == null) return;
                savedRefPart = ship.GetReferenceTransformPart();
                ourPort.MakeReferenceTransform();
                Debug.Log(Tag + "control point -> docking port '" + ourPort.part.partInfo.title
                          + "' (was '" + (savedRefPart != null ? savedRefPart.partInfo.title : "-")
                          + "') so the roll aligns at the port");
            }
            catch (Exception e) { Debug.LogWarning(Tag + "could not control from the port: " + e.Message); }
        }

        private static void RestoreControlPoint()
        {
            try
            {
                if (ship != null && savedRefPart != null && savedRefPart.vessel == ship)
                    ship.SetReferenceTransform(savedRefPart);
            }
            catch (Exception e) { Debug.LogWarning(Tag + "could not restore control point: " + e.Message); }
            savedRefPart = null;
        }

        internal static void SetTarget(ITargetable t, string why)
        {
            try
            {
                if (FlightGlobals.fetch == null) return;
                FlightGlobals.fetch.SetVesselTarget(t, true);
                Debug.Log(Tag + (t == null ? "target CLEARED - " : "target set to '"
                          + t.GetName() + "' - ") + why);
            }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "could not " + (t == null ? "clear" : "set")
                                 + " the target: " + e.Message);
            }
        }

        public static void Reset()
        {
            Engaged = false; Stage = DockStage.Idle; Note = "-";
            reached = DockStage.Idle;
            legRangeM = 0.0; lastWaypoint = DockWaypoint.None;
            ship = null; station = null; ourPort = null; theirPort = null;
            RangeToPortM = 0.0; ClosingMps = 0.0; AxisErrorDeg = 0.0;
        }

        private static bool PickPorts()
        {
            ourPort = null; theirPort = null;
            List<ModuleDockingNode> ours = OpenPorts(ship);
            List<ModuleDockingNode> theirs = OpenPorts(station);
            // ---- ⛔ SAY WHAT WAS ACTUALLY FOUND, NOT JUST THAT NOTHING WAS. ----
            if (ours.Count == 0)
            {
                Note = "no free docking port on this vehicle - " + Census(ship);
                Debug.LogWarning(Tag + Note);
                return false;
            }
            if (theirs.Count == 0)
            {
                Note = "no free docking port on the station - " + Census(station);
                Debug.LogWarning(Tag + Note);
                return false;
            }

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

        private static string Census(Vessel v)
        {
            if (v == null) return "no vessel";
            int total = 0, docked = 0, shielded = 0, noTransform = 0, free = 0;
            System.Text.StringBuilder types = new System.Text.StringBuilder();

            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleDockingNode> ns = v.parts[i].Modules.GetModules<ModuleDockingNode>();
                for (int m = 0; m < ns.Count; m++)
                {
                    ModuleDockingNode n = ns[m];
                    total++;
                    if (n.otherNode != null) { docked++; continue; }
                    if (n.nodeTransform == null) { noTransform++; continue; }
                    if (!string.IsNullOrEmpty(n.state)
                        && n.state.ToLowerInvariant().Contains("disabled")) { shielded++; continue; }
                    free++;
                    if (types.Length > 0) types.Append("/");
                    types.Append(string.IsNullOrEmpty(n.nodeType) ? "?" : n.nodeType);
                }
            }
            return total + " node(s): " + docked + " already docked, " + shielded
                 + " shielded or disabled, " + noTransform + " with no transform, " + free
                 + " free" + (types.Length > 0 ? " (" + types + ")" : "")
                 + (shielded > 0 ? " - open the shields and try again" : "");
        }

        private static List<ModuleDockingNode> OpenPorts(Vessel v)
        {
            List<ModuleDockingNode> open = new List<ModuleDockingNode>();
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleDockingNode> ns = v.parts[i].Modules.GetModules<ModuleDockingNode>();
                for (int m = 0; m < ns.Count; m++)
                {
                    if (ns[m].otherNode != null) continue;
                    if (ns[m].nodeTransform == null) continue;
                    if (!string.IsNullOrEmpty(ns[m].state)
                        && ns[m].state.ToLowerInvariant().Contains("disabled")) continue;
                    open.Add(ns[m]);
                }
            }
            return open;
        }

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

        private static double legRangeM;
        private static DockWaypoint lastWaypoint;

        private static double bestRangeM = double.MaxValue, bestRangeAt;

        private static DockStage reached;

        private static void Commit(DockStage s)
        {
            Stage = s;
            if (DockApproach.Rank(s) > DockApproach.Rank(reached)) reached = s;
        }

        private const double NoProgressLimitS = 60.0;

        private const double ProgressEpsilonM = 1.0;

        private static bool DivergedTooLong()
        {
            double now = Planetarium.GetUniversalTime();

            // ---- ⛔ PROGRESS IS TOWARD THE CURRENT WAYPOINT, NOT TOWARD THE STATION. ----
            double range = (legRangeM > 0.0) ? legRangeM
                                             : Vector3d.Distance(ship.CoM, station.CoM);

            if (range < bestRangeM - ProgressEpsilonM)
            {
                bestRangeM = range;
                bestRangeAt = now;
                return false;
            }
            if (bestRangeAt <= 0.0) { bestRangeAt = now; return false; }
            if (now - bestRangeAt < NoProgressLimitS) return false;

            Note = "GAVE UP - no closer than " + bestRangeM.ToString("F0") + " m in "
                 + NoProgressLimitS.ToString("F0") + " s; now " + range.ToString("F0") + " m";
            Debug.LogError(Tag + "docking " + Note + ". Disengaging so the approach is not spent "
                         + "flying the wrong way. Check x_transX/Y/Z against x_dkDistS/T/F and "
                         + "x_ctlX/Y/Z in the recording - a command that holds one sign while its "
                         + "own offset grows is an inverted axis; a command that never reaches "
                         + "x_ctl* is not being applied at all.");

            // ---- ⛔ AND IT MUST STAY GIVEN UP. ----
            Stage = DockStage.NoPort;
            Disengage("no progress");
            return true;
        }

        private static void RaiseTargetRange(Vessel target)
        {
            if (target == null) return;
            VesselRanges r = target.vesselRanges;
            if (r == null) return;
            r.orbit.unload = (float)TargetUnloadM;
            r.orbit.load   = (float)TargetLoadM;
            r.orbit.unpack = (float)TargetUnpackM;
            r.orbit.pack   = (float)TargetPackM;
            target.vesselRanges = r;
        }

        private static void AwaitTarget()
        {
            double waited = Planetarium.GetUniversalTime() - loadWaitStartedAt;

            if (station.loaded)
            {
                if (!PickPorts())
                {
                    Stage = DockStage.NoPort;
                    Engaged = false;
                    Debug.LogWarning(Tag + "docking refused - " + Note);
                    return;
                }
                keepOutR = MeasureKeepOut(station);
                Stage = DockStage.ToGate;
                Debug.Log(Tag + "docking engaged - '" + ourPort.part.partInfo.title + "' to '"
                          + theirPort.part.partInfo.title + "', keep-out " + keepOutR.ToString("F0")
                          + " m (station loaded after " + waited.ToString("F1") + " s)");
                return;
            }

            if (waited > TargetLoadTimeoutS)
            {
                double km = Vector3d.Distance(ship.CoM, station.CoM) / 1000.0;
                Note = "REFUSED - the station is still not loaded after "
                     + TargetLoadTimeoutS.ToString("F0") + " s at " + km.ToString("F1") + " km";
                Stage = DockStage.NoPort;
                Engaged = false;
                Debug.LogWarning(Tag + "docking refused - " + Note
                               + ". Its ports cannot be read from here. Close to inside "
                               + (TargetLoadM / 1000.0).ToString("F1") + " km first.");
                return;
            }

            Note = "WAITING FOR THE STATION - " + waited.ToString("F0") + " / "
                 + TargetLoadTimeoutS.ToString("F0") + " s";
        }

        public static void Tick()
        {
            if (!Engaged) return;
            if (ship == null || station == null || ship.state == Vessel.State.DEAD)
            {
                Disengage("vessel lost");
                return;
            }

            if (Stage == DockStage.AwaitingTarget) { AwaitTarget(); return; }

            // ---- ⛔ A DOCKING THAT IS NOT CLOSING IS NOT DOCKING. BOUND IT. ----
            if (DivergedTooLong()) return;

            if (ourPort == null || theirPort == null)
            {
                Disengage("port lost");
                return;
            }

            if (ourPort.otherNode != null)
            {
                Stage = DockStage.Docked;
                Note = "DOCKED";
                Disengage("docked");
                return;
            }

            CapsuleRcs.Set(ship, CapsuleRcs.DockPct);

            Vector3d ourPos = ourPort.nodeTransform.position;
            Vector3d tgtPos = theirPort.nodeTransform.position;
            Vector3d axis = theirPort.nodeTransform.forward.normalized;

            Vector3d toPort = tgtPos - ourPos;
            RangeToPortM = toPort.magnitude;
            Vector3d relVel = station.obt_velocity - ship.obt_velocity;
            ClosingMps = Vector3d.Dot(-relVel, toPort.normalized);

            Vector3d c = station.CoM - tgtPos;
            double gateD = DockGeometry.GateDistanceM(Vector3d.Dot(axis, c), c.sqrMagnitude,
                                                      keepOutR);
            Vector3d gate = tgtPos + axis * gateD;
            Vector3d standoff = tgtPos + axis * DockGeometry.StandoffM;

            // ---- ⛔ THE DECISION IS `pure/DockApproach.Select`, AND THAT IS THE POINT. ----
            double axialM = -Vector3d.Dot(toPort, axis);
            Vector3d lateralVec = toPort - axis * Vector3d.Dot(toPort, axis);
            double lateralM = lateralVec.magnitude;

            Vector3d toGate = gate - ourPos;
            Vector3d cs = station.CoM - ourPos;
            bool clear = (toGate.sqrMagnitude > 1e-6)
                         && DockGeometry.PathClear(cs.magnitude, cs.sqrMagnitude,
                                                   Vector3d.Dot(cs, toGate.normalized),
                                                   toGate.magnitude, keepOutR);

            DockApproachInputs ai = new DockApproachInputs();
            ai.Valid = true;
            ai.AxialM = axialM;
            ai.LateralM = lateralM;
            ai.ToStandoffM = (standoff - ourPos).magnitude;
            ai.ToGateM = toGate.magnitude;
            ai.PathClear = clear;
            ai.AcquireM = (theirPort != null && theirPort.acquireRange > 0.0f)
                          ? theirPort.acquireRange * 0.5 : 0.25;
            ai.SafeM = keepOutR;

            DockApproachResult sel = DockApproach.Select(ai, reached);
            if (sel.Waypoint != lastWaypoint)
            {
                lastWaypoint = sel.Waypoint;
                bestRangeM = double.MaxValue;
                bestRangeAt = Planetarium.GetUniversalTime();
            }

            Commit(sel.Stage);
            Note = sel.Note;

            // ---- INSIDE THE CAPTURE RANGE: STOP THRUSTING AND LET THE MAGNETS TAKE IT. ----
            if (sel.Captured)
            {
                StopTranslating();
                if (!ship.ActionGroups[KSPActionGroup.RCS])
                    ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
                AttitudeController.Ascent.SteerTo(ship, -axis,
                    (theirPort != null && theirPort.nodeTransform != null)
                    ? (Vector3d)theirPort.nodeTransform.up * DockRollSign : Vector3d.zero);
                return;
            }

            Vector3d target;
            switch (sel.Waypoint)
            {
                case DockWaypoint.Port:
                    target = tgtPos;
                    break;
                case DockWaypoint.Standoff:
                    target = standoff;
                    break;
                case DockWaypoint.Skirt:
                    target = Skirt(ourPos, gate);
                    break;
                default:
                    target = clear ? gate : Skirt(ourPos, gate);
                    break;
            }

            legRangeM = (target - ourPos).magnitude;
            FlyTo(ourPos, target, axis);
        }

        private static Vector3d Skirt(Vector3d ourPos, Vector3d gate)
        {
            Vector3d c = station.CoM - ourPos;
            Vector3d side = Vector3d.Exclude(c.normalized, gate - ourPos);
            if (side.magnitude < 1.0)
            {
                side = Vector3d.Exclude(c.normalized, (ship.CoM - ship.mainBody.position));
            }
            if (side.magnitude < 1.0) return gate;
            return station.CoM + side.normalized * DockGeometry.SkirtRadiusM(keepOutR);
        }

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

            // ---- Nose down the port axis, always: -axis is "facing the port". ----
            Vector3d rollRef = (theirPort != null && theirPort.nodeTransform != null)
                             ? (Vector3d)theirPort.nodeTransform.up * DockRollSign : Vector3d.zero;
            AttitudeController.Ascent.SteerTo(ship, -axis, rollRef);
            AxisErrorDeg = Vector3d.Angle(ship.ReferenceTransform.up, -axis);

            if (!ship.ActionGroups[KSPActionGroup.RCS])
                ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

            // ---- ⛔ MEASURE BEFORE ANY EARLY RETURN. A STALE READING LOOKS EXACTLY LIKE A LIVE ONE. ----
            Transform rt = ship.ReferenceTransform;
            Vector3d nose = rt.up;

            DockState ds = new DockState();
            ds.Valid = true;
            ds.DistF = Vector3d.Dot(to, nose);
            ds.DistS = Vector3d.Dot(to, rt.right);
            ds.DistT = Vector3d.Dot(to, -rt.forward);
            Vector3d ourRel = -relVel;
            ds.VelF = Vector3d.Dot(ourRel, nose);
            ds.VelS = Vector3d.Dot(ourRel, rt.right);
            ds.VelT = Vector3d.Dot(ourRel, -rt.forward);
            // ---- ⛔ THE CONTACT-TAPERED CAP, NOT THE RENDEZVOUS LADDER'S. ----
            ds.SpeedCap = DockControl.SpeedCapFor(range);

            DistF = ds.DistF; DistS = ds.DistS; DistT = ds.DistT;
            VelF = ds.VelF; VelS = ds.VelS; VelT = ds.VelT;

            // ---- ⛔ NO COAST BYPASS ON THE DOCKING. THE SERVO FLIES EVERY TICK. ----

            // ---- TRANSLATE, DO NOT ROTATE ----
            // ---- ⛔ THE SERVO IS `pure/DockControl.cs`, THE PORT OF `GNC.ks:1190 DockGNC`. ----
            double dt = Time.fixedDeltaTime;
            if (dt <= 0.0) dt = 0.02;
            DockCommand dc = DockControl.Solve(ds, pidF, pidS, pidT, dt);

            AttitudeController.Ascent.UllageFore = dc.Fore;
            AttitudeController.Ascent.TranslateX = dc.Starboard;
            AttitudeController.Ascent.TranslateY = dc.Top;
            Note += " " + dc.Note;
        }

        public const double LateralDeadbandM = 0.35;

        private static double Clamp(double d)
        {
            if (d > 1.0) return 1.0;
            if (d < -1.0) return -1.0;
            return d;
        }

        private static void StopTranslating()
        {
            AttitudeController.Ascent.UllageFore = 0.0;
            AttitudeController.Ascent.TranslateX = 0.0;
            AttitudeController.Ascent.TranslateY = 0.0;
        }

        private static void Translate(double fore)
        {
            AttitudeController.Ascent.UllageFore = fore;
        }
    }
}
