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
    public static class DockingOps
    {
        private const string Tag = "[DragonScreen] ";

        public static DockStage Stage { get; private set; }
        public static string Note = "-";
        public static double RangeToPortM, ClosingMps, AxisErrorDeg;

        /// <summary>
        /// The controller's INPUTS, for the recorder. Offsets and relative velocity on the capsule's
        /// own axes, exactly as `DockControl.Solve` sees them.
        ///
        /// ⛔ THESE EXIST BECAUSE THE OUTPUTS ALONE COULD NOT SETTLE THE 2026-08-12 FAILURE.
        /// `x_transX` was railed at -0.50 for 213 seconds while the range grew 197 m -> 2943 m, and
        /// with only the commands recorded there was no way to tell an inverted axis from a correct
        /// axis chasing a real and growing error. One flight with these decides it: if a command is
        /// steadily non-zero while ITS OWN offset grows in the same direction, that axis is inverted.
        /// </summary>
        public static double DistF, DistS, DistT, VelF, VelS, VelT;

        private static Vessel ship, station;

        // ---- ⚠ THE PIDs LIVE HERE, NOT INSIDE THE SOLVER. ----
        // DockControl.Solve takes them as arguments precisely so their integral and derivative
        // survive between ticks: "a PID recreated every frame is just a P controller with extra
        // steps." Reset on Engage, never inside the loop.
        private static readonly Pid pidF = new Pid();
        private static readonly Pid pidS = new Pid();
        private static readonly Pid pidT = new Pid();
        // ---- F9I's LOAD RANGES FOR THE TARGET. `station_ops.ks:1147-1150`, verbatim. ----
        // ⛔ AN UNLOADED VESSEL HAS NO PARTS, SO IT HAS NO PORTS. KSP represents anything past the
        // load distance as a ProtoVessel: `station.parts` is EMPTY, every port scan returns nothing,
        // and the honest-sounding refusal "no free docking port on the station" is simply false. The
        // 2026-08-11 partdump shows the station carrying several free `ModuleDockingNode`s at the
        // moment we said it had none - we were 10 km out and could not see any of them.
        //
        // F9I raises the ranges on the TARGET and then waits for `target:loaded` before reading a
        // single port, and `StClosestPort:373` opens by refusing outright if it is still not loaded.
        // Note `load` is 10050 m - fifty metres past the 10 km direct-approach gate, so the station
        // is in memory by the time the approach hands over, not at the instant it arrives.
        private const double TargetUnloadM = 25000.0;
        private const double TargetLoadM   = 10050.0;
        private const double TargetUnpackM =  2100.0;
        private const double TargetPackM   =  2250.0;
        /// <summary>How long to wait for the load before giving up. F9I's `stLoadT`, 30 s.</summary>
        private const double TargetLoadTimeoutS = 30.0;

        /// <summary>Furthest the docking servo will accept the job, metres. Beyond this the rendezvous
        /// owns the approach - the RCS servo cannot fly a kilometre-scale gap.</summary>
        [Tunable] public static double DockEnvelopeM = 300.0;
        /// <summary>Fastest relative speed the servo will accept at engage, m/s. The rendezvous matches
        /// to ~0.5 m/s before handing over; this allows margin.</summary>
        [Tunable] public static double DockMaxRelSpeedMps = 2.0;

        /// <summary>
        /// Sign on the roll reference (the target port's up). +1 clocks our port's up PARALLEL to
        /// theirs; -1 flips it 180 deg. We control from our port so the roll steers the port directly,
        /// but whether KDSS's `captureMinRollDot` wants the ups parallel or opposed is a convention
        /// only a flight settles - so this is tunable LIVE: if the ports sit aligned but refuse to
        /// latch, set `DockingOps.DockRollSign = -1` in tuning.cfg and watch it grab, no rebuild.
        /// </summary>
        [Tunable] public static double DockRollSign = 1.0;

        private static double loadWaitStartedAt;

        private static ModuleDockingNode ourPort, theirPort;
        private static double keepOutR;
        private static double startedAt;

        public static bool Engaged { get; private set; }

        // ------------------------------------------------------------------ lifecycle

        public static void Engage(Vessel v, Vessel target)
        {
            // The servo's memory belongs to THIS approach, not the last one.
            pidF.Reset(); pidS.Reset(); pidT.Reset();
            if (v == null || target == null) return;

            // ---- ⛔ THE DOCKING ENVELOPE. RCS CANNOT FLY A KILOMETRE-SCALE, FAST APPROACH. ----
            // The servo has 0.15 m/s^2 of authority and a contact-speed law. Handed a 3.5 km gap at
            // 30 m/s - the manual AUTO-DOCK button pressed mid-rendezvous, measured 2026-08-17 - it
            // cannot null the closing velocity and thrashes the whole tank dry without ever reaching
            // the gate. Docking is the LAST few hundred metres; the rendezvous owns everything before
            // that and matches velocity to ~0.5 m/s before it hands over (well inside this envelope).
            // Refuse out of envelope and say why, so the crew rendezvous first.
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
                // Not a refusal - a wait. See RaiseTargetRange.
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
            // `ModuleDockingNode` is an `ITargetable`, and targeting the NODE is what makes KSP
            // produce port-relative state: the navball's target marker becomes the port's axis
            // rather than the station's centre of mass, and the DOCKING page's alignment ring,
            // Z/ROLL readouts and closing rate all read off that. Targeting the vessel gives you
            // the middle of the station, which is not where the capsule is going and is why the
            // page has never shown what it was built to show.
            //
            // It is also what a crew flying this manually would do first.
            SetTarget(theirPort, "docking engaged");
            ControlFromPort();
            DockShroud.Open(ship);          // the nose must be open before the ports can mate
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

        /// <summary>The control point held before docking took it, to put back afterwards.</summary>
        private static Part savedRefPart;

        /// <summary>
        /// ⛔ CONTROL FROM OUR DOCKING PORT. This is what makes the ROLL align at the port.
        ///
        /// MechJeb requires it - `MechJebModuleDockingGuidance.cs:54` warns "this vessel is not
        /// controlled from a docking node... Control from here" and does all its docking math in that
        /// frame. Ours steered the POD's roll to the target port's up, so OUR PORT ended up clocked
        /// ~180 deg off theirs; with `captureMinRollDot = 0.5` on both KDSS halves the magnets refuse
        /// the latch even from a flawless 0.5 m hold - which is exactly what the crew watched, the
        /// capsule frozen half a metre out. Controlling from the port makes `-axis`/roll steer the
        /// PORT, so aligning to the target port's up clocks the two ports together. Restored on
        /// disengage so de-orbit and entry fly from the pod again.
        /// </summary>
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

        /// <summary>
        /// Point KSP's target machinery at something, or clear it with null.
        ///
        /// Guarded and logged: `SetVesselTarget` on a vessel that is not the active one throws in
        /// some KSP versions, and an exception here would take the docking loop with it - the same
        /// class of failure as an unguarded `OnFlyByWire`.
        /// </summary>
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
            // ---- ⛔ SAY WHAT WAS ACTUALLY FOUND, NOT JUST THAT NOTHING WAS. ----
            // On 2026-08-11 this refused four times with "no free docking port on the station" and
            // that sentence is unactionable: it cannot distinguish a station with no ports, a
            // station whose ports are all occupied, one whose shields are shut, or a node type we
            // cannot mate with. The next flight should not have to guess which.
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

        /// <summary>
        /// Every docking node on a vessel and why each one was or was not usable.
        ///
        /// Printed only on a refusal, so it costs nothing on a normal approach and turns the one
        /// message that matters from "no" into "no, because".
        /// </summary>
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
                    // A shielded port reports itself Disabled until its cover is opened.
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
                    if (ns[m].otherNode != null) continue;              // already docked
                    if (ns[m].nodeTransform == null) continue;
                    // ⚠ A SHIELDED PORT IS NOT A FREE PORT. It reports Disabled until its cover
                    // opens, and driving a capsule onto one is a collision with a closed hatch.
                    if (!string.IsNullOrEmpty(ns[m].state)
                        && ns[m].state.ToLowerInvariant().Contains("disabled")) continue;
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

        /// <summary>
        /// Distance to the waypoint the profile is currently flying to, metres. Zero until known.
        ///
        /// The no-progress guard judges THIS, not the range to the station - see DivergedTooLong.
        /// </summary>
        private static double legRangeM;
        private static DockWaypoint lastWaypoint;

        /// <summary>Best range seen this approach, and when it was last improved on.</summary>
        private static double bestRangeM = double.MaxValue, bestRangeAt;


        /// <summary>
        /// The furthest stage this approach has reached. The stage machine is MONOTONE - see the
        /// note in Tick. Replaces the single-purpose `inCorridor` latch, which fixed this bug for
        /// one stage and left the others holding it.
        /// </summary>
        private static DockStage reached;

        // Ranking lives in `pure/DockApproach` so the flight code and the simulator cannot
        // disagree about what "further along" means. A second copy here is the ChromeBar.LinkRect
        // defect in another costume.

        /// <summary>Enter a stage, and remember it as the high-water mark.</summary>
        private static void Commit(DockStage s)
        {
            Stage = s;
            if (DockApproach.Rank(s) > DockApproach.Rank(reached)) reached = s;
        }

        /// <summary>Seconds the range may fail to improve before the docking gives up.</summary>
        private const double NoProgressLimitS = 60.0;

        /// <summary>Range must beat the best by this to count as progress, metres.</summary>
        private const double ProgressEpsilonM = 1.0;

        /// <summary>
        /// True when the docking has stopped making progress and has been disengaged.
        ///
        /// Measured against the BEST range seen, not the previous tick, so a momentary drift while
        /// rounding the keep-out sphere does not trip it - only a sustained failure to get closer.
        /// </summary>
        private static bool DivergedTooLong()
        {
            double now = Planetarium.GetUniversalTime();

            // ---- ⛔ PROGRESS IS TOWARD THE CURRENT WAYPOINT, NOT TOWARD THE STATION. ----
            // This measured `ship.CoM -> station.CoM` and gave up when that stopped shrinking. But
            // rounding the hull deliberately flies AWAY from the station centre for a while, so the
            // guard has to judge the leg being flown, not the range to the station. `legRangeM` is
            // the distance to the waypoint the profile actually chose - gate, standoff or port, all
            // near the station - which shrinks monotonically on every leg of a working approach and
            // on none of a broken one.
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
            // `Disengage` only clears `Engaged`, and `StationApproach.Arrived` re-engages anything
            // that is not Docked or NoPort - so on 2026-08-12 the guard fired and the docking came
            // straight back 0.02 s later, three times, achieving exactly nothing but a tidier log.
            // The same shape as the propellant refusal that "stopped" a phase-down already running:
            // a guard whose effect is undone by the next tick is not a guard.
            Stage = DockStage.NoPort;
            Disengage("no progress");
            return true;
        }

        /// <summary>
        /// Ask KSP to keep the station in memory out to the docking range. F9I `station_ops:1147`.
        ///
        /// ⚠ ORBIT SITUATION ONLY. These are the ranges that apply where we dock; raising the
        /// landed or flying ones would keep station geometry loaded in situations that have no use
        /// for it and every frame cost of it. `falcon-physics-range-clamp` is the standing warning
        /// that KSP does not always honour a raised range - it clamped the booster's 1500 km request
        /// to about 300 km on four measured flights - so the wait below has a timeout and a refusal
        /// rather than an assumption that the ask worked.
        /// </summary>
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

        /// <summary>
        /// Hold while KSP loads the station, then pick the ports. F9I's `until target:loaded` loop.
        ///
        /// Nothing is flown here on purpose: the capsule is co-moving and the crew asked to dock, not
        /// to close. Timing out is a refusal that names the real reason, which is the whole point of
        /// the change - "no free docking port on the station" was a true sentence about an empty part
        /// list and a false one about the station.
        /// </summary>
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
            // On 2026-08-12 this held the vehicle for 213 seconds while the range went 197 m to
            // 2943 m and the tank went 36 units to zero, and nothing stopped it - the crew did, by
            // cancelling. Whatever the underlying fault turns out to be, a controller allowed to
            // run indefinitely while its own objective gets monotonically worse is its own bug, and
            // it is the one that costs the mission.
            //
            // Same shape as the phasing lap cap: not a tuning knob, a bound. It gives up and says
            // why, leaving the capsule co-moving with its propellant, which is a recoverable state.
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

            // ⛔ DOCK GENTLE. The Draco now carries full real thrust; at 100 % it would over-drive this
            // servo, which is tuned for the old weak RCS. DockPct (~20 %) reproduces that old authority,
            // so docking is unchanged - and it OVERRIDES whatever strength the rendezvous burn left at
            // 100 %. (user 2026-08-24, "tune the thruster strength for the task at hand".)
            CapsuleRcs.Set(ship, CapsuleRcs.DockPct);

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

            // ---- ⛔ THE DECISION IS `pure/DockApproach.Select`, AND THAT IS THE POINT. ----
            // It used to be written out here, in KSP glue, where no test could reach it - and
            // `DockControlTest` flew the servo for 40 000 steps and passed 4704 checks while
            // docking never once succeeded, because it fed the servo the distance STRAIGHT TO THE
            // PORT and the flight code feeds it the distance to the current WAYPOINT. The tested
            // layer was not the broken layer. `DockApproachTest` now flies this exact function
            // against a 3-D plant, and it found four defects on its first run.
            //
            // This block's only job is to turn the vessel state into scalars and the chosen
            // waypoint back into a point.
            double axialM = -Vector3d.Dot(toPort, axis);      // + when we are IN FRONT of the port
            Vector3d lateralVec = toPort - axis * Vector3d.Dot(toPort, axis);
            double lateralM = lateralVec.magnitude;

            Vector3d toGate = gate - ourPos;
            Vector3d cs = station.CoM - ourPos;               // us -> station centre
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
            // ⛔ THE PORT'S OWN CAPTURE RANGE, NOT A CONSTANT. `MechJebModuleDockingAutopilot.cs:342`
            // takes `acquireRange * 0.5` off the target node and falls back to 0.25 m. Ours had no
            // notion of capture distance at all, so "arrived" was never a state the geometry could
            // reach - it could only ever be reported by KSP coupling the ports.
            ai.AcquireM = (theirPort != null && theirPort.acquireRange > 0.0f)
                          ? theirPort.acquireRange * 0.5 : 0.25;
            ai.SafeM = keepOutR;

            DockApproachResult sel = DockApproach.Select(ai, reached);
            if (sel.Waypoint != lastWaypoint)
            {
                // A NEW LEG IS NOT A FAILURE OF THE OLD ONE. Without this the high-water mark from
                // the previous waypoint condemns the next one before it has flown a metre.
                lastWaypoint = sel.Waypoint;
                bestRangeM = double.MaxValue;
                bestRangeAt = Planetarium.GetUniversalTime();
            }

            Commit(sel.Stage);
            Note = sel.Note;

            // ---- INSIDE THE CAPTURE RANGE: STOP THRUSTING AND LET THE MAGNETS TAKE IT. ----
            // MechJeb ends the approach at `zSep < acquireRange` (`:293`) rather than driving to
            // contact. Holding the attitude matters - the ports must stay aligned for the latch -
            // but pushing does not, and pushing through a capture is how a port bounces.
            if (sel.Captured)
            {
                StopTranslating();
                // ⛔ RCS ON TO HOLD THE PORT AXIS FOR THE LATCH. In RO there are no reaction wheels
                // (RO_ReactionWheels.cfg), so this hold turns on RCS or nothing keeps the ports aligned
                // while the magnets pull in - and captureMinRollDot 0.5 loses the latch on a few degrees
                // of drift. FlyTo enables RCS every approach tick; this final hold must GUARANTEE it too
                // rather than inherit it, since it returns before ever reaching FlyTo's enable.
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
                default:                            // Gate, or anything unexpected: fly to the gate,
                    target = clear ? gate : Skirt(ourPos, gate);   // rounding if the path is blocked
                    break;
            }

            legRangeM = (target - ourPos).magnitude;
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

            // ---- Nose down the port axis, always: -axis is "facing the port". ----
            // ⛔ AND WITH A ROLL REFERENCE, BECAUSE KDSS WILL NOT CAPTURE WITHOUT ONE.
            // Both halves of the pair we fly - `KDSS/Parts/KDSS/KDSSA.cfg:43` and `KDSSP.cfg:44` -
            // carry `captureMinRollDot = 0.5`, so the two ports must be within 60 degrees of roll
            // alignment or the magnets never latch however perfect the approach was. Passing
            // `Vector3d.zero` here leaves roll unconstrained, which is the same omission that let the
            // booster flip drift to 89.8 degrees before its roll gate timed out on 2026-08-11.
            //
            // The reference is the TARGET PORT's own up, not the capsule's and not the orbit's: it is
            // the one frame in which "aligned" means what the capture test means by it.
            Vector3d rollRef = (theirPort != null && theirPort.nodeTransform != null)
                             ? (Vector3d)theirPort.nodeTransform.up * DockRollSign : Vector3d.zero;
            AttitudeController.Ascent.SteerTo(ship, -axis, rollRef);
            AxisErrorDeg = Vector3d.Angle(ship.ReferenceTransform.up, -axis);

            if (!ship.ActionGroups[KSPActionGroup.RCS])
                ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

            // ---- ⛔ MEASURE BEFORE ANY EARLY RETURN. A STALE READING LOOKS EXACTLY LIKE A LIVE ONE. ----
            // These six used to be written only on the path that also commands thrust, so a coast
            // left the LAST COMMANDED tick's numbers sitting in the recorder looking current. On
            // 2026-08-12 `x_dkDistF` held 11.64 for 8 s while the true range fell 24.7 m -> 17.4 m,
            // and reading that back it was impossible to tell a frozen sample from a controller
            // that had stopped responding. The offsets are a MEASUREMENT of where the capsule is;
            // they do not depend on whether we chose to burn, so they are taken every tick.
            // Same family as the navball serving a RenderTexture nothing was rendering into.
            Transform rt = ship.ReferenceTransform;
            Vector3d nose = rt.up;

            DockState ds = new DockState();
            ds.Valid = true;
            // Offsets and relative velocity resolved onto the capsule's OWN axes. `rt.forward` is the
            // controller's -top - see AttitudeController's -90 note - so top is its negation.
            ds.DistF = Vector3d.Dot(to, nose);
            ds.DistS = Vector3d.Dot(to, rt.right);
            ds.DistT = Vector3d.Dot(to, -rt.forward);
            Vector3d ourRel = -relVel;                 // ours minus theirs
            ds.VelF = Vector3d.Dot(ourRel, nose);
            ds.VelS = Vector3d.Dot(ourRel, rt.right);
            ds.VelT = Vector3d.Dot(ourRel, -rt.forward);
            // ---- ⛔ THE CONTACT-TAPERED CAP, NOT THE RENDEZVOUS LADDER'S. ----
            // This used `c.WantClosingMps` from `Approach.Terminal` - the rendezvous speed ladder,
            // which bottoms out near 1 m/s and does NOT taper to a contact speed. Measured
            // 2026-08-17: the capsule coasted the last 7 m into the port at a steady 0.97 m/s,
            // arrived ~1 m off centre far too fast to capture, missed, and flew back out to the
            // standoff - the +/-20 m oscillation the crew watched. `SpeedCapFor` is the cap the
            // headless DockControl test flies and CAPTURES on: it tapers to `ContactFloorMps`
            // (0.15 m/s) at the port, so the servo slows to a controlled contact instead of drifting
            // through the capture envelope. `range` is the distance to the current waypoint, which
            // on the final run is the port itself.
            ds.SpeedCap = DockControl.SpeedCapFor(range);

            DistF = ds.DistF; DistS = ds.DistS; DistT = ds.DistT;
            VelF = ds.VelF; VelS = ds.VelS; VelT = ds.VelT;

            // ---- ⛔ NO COAST BYPASS ON THE DOCKING. THE SERVO FLIES EVERY TICK. ----
            // `Approach.Terminal`'s coast is a RENDEZVOUS economy - stop thrusting inside a speed
            // deadband to save monopropellant over a long transit. On the final docking approach it
            // was catastrophic: `if (c.Coast) return` skipped `DockControl.Solve` ENTIRELY, so on
            // the last few metres there was NO braking curve and NO lateral trim - the capsule
            // coasted in at whatever speed it had and drifted off the axis. That is exactly what the
            // crew reported: "you just forward thrust and hope for the best". The velocity servo
            // below has its own bound (the braking curve) and commands ~0 once it is at the target
            // speed, so it needs no coast - and it is the only thing that keeps the approach lined
            // up. This is also why the headless test passed while flight failed: the test always
            // ran the servo; the flight coasted around it.

            // ---- TRANSLATE, DO NOT ROTATE ----
            // The capsule holds its nose on the port axis and slides. Yawing to correct a lateral
            // drift takes it off the axis it has to arrive on, and arriving crooked is how flight 035
            // "missed the port and bounced off the hull".
            //
            // ---- ⛔ THE SERVO IS `pure/DockControl.cs`, THE PORT OF `GNC.ks:1190 DockGNC`. ----
            // What stood here was mine: `UllageFore = tooSlow ? 1.0 : -1.0` and a normalised lateral
            // push. That is BANG-BANG - full thrust one way or the other, every tick, on all three
            // axes at once - against a ported velocity servo that was already written, already
            // tested, and wired to nothing. RULE 0, exactly: I took F9I's numbers and wrote my own
            // mechanism. Two things the servo has that the bang-bang cannot:
            //
            //   · a BRAKING CURVE. Each axis gets a target speed of sqrt(2 a d) capped by the
            //     ladder, so the approach speed is bounded BY CONSTRUCTION at every range instead of
            //     emerging from whatever the gains happen to do. Bang-bang has no such bound: it
            //     closes at whatever speed it has picked up and reverses at the deadband.
            //   · AUTHORITY MIXING. The three axes share one set of thrusters, so the dominant one
            //     takes half and the others a quarter each. Commanding all three flat out is how a
            //     capsule crabs sideways into a hull.
            double dt = Time.fixedDeltaTime;
            if (dt <= 0.0) dt = 0.02;
            DockCommand dc = DockControl.Solve(ds, pidF, pidS, pidT, dt);

            AttitudeController.Ascent.UllageFore = dc.Fore;
            AttitudeController.Ascent.TranslateX = dc.Starboard;
            AttitudeController.Ascent.TranslateY = dc.Top;
            Note += " " + dc.Note;
        }

        /// <summary>
        /// ⛔ NEVER WIRED, AND DELIBERATELY NOT WIRING IT. Kept as the record of a rejected idea.
        ///
        /// "Lateral offset below 0.35 m is not worth spending monopropellant on" - transcribed,
        /// never read by anything, and it is as well. A lateral deadband on the FINAL approach is
        /// precisely how a capsule misses the port: it stops trimming at 0.35 m and coasts the last
        /// metre off-centre. The port's own capture envelope is a few centimetres, so the trim has
        /// to run all the way to contact. `AxisSpeedLimit` already tapers the command to zero as
        /// the error closes, which is the honest version of what this was reaching for.
        /// </summary>
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
