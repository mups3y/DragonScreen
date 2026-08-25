/*
 * DragonScreen - WaypointApproach
 *
 * PURE. The real Crew Dragon proximity-operations approach: the L-shaped R-bar -> V-bar path through
 * three station-keeping waypoints, each a HOLD awaiting a GO. This is the profile SpaceX and NASA
 * publish (spacex.com/CREW-2 step 5, docs/REAL_CREW_DRAGON_MISSION.md), which the straight-in
 * DirectApproach never flew - it went to a single 200 m goal and handed to the docking.
 *
 * ---- THE PROFILE, IN THE STATION'S LVLH FRAME ----
 * Everything here is in the station's local-vertical/local-horizontal frame, metres, relative to the
 * station: RADIAL (+ above / away from Earth, - below), ALONG-track (+ ahead, in the velocity
 * direction, where Harmony's forward port faces), CROSS-track (out of plane). The glue builds that
 * frame from the station's orbit and projects our relative position and velocity into it; this file
 * never touches KSP.
 *
 *     WP0 : 400 m BELOW the station (radial -400)          - arrive from below on the R-bar, HOLD
 *     WP1 : 220 m IN FRONT on the docking axis (along +220) - swing up and forward (the L), HOLD
 *     WP2 :  20 m in front (along +20)                      - final standoff, HOLD
 *     then hand to the docking controller for contact and capture at the port.
 *
 * ---- WHY WAYPOINTS AND HOLDS, NOT A LINE ----
 * `REAL_CREW_DRAGON_MISSION.md`: "THE APPROACH IS AN L, NOT A LINE" and "EVERY WAYPOINT IS A HOLD".
 * Dragon stops and station-keeps at each, awaiting a GO, so the approach is abortable at every stage.
 * A 200 m KEEP-OUT SPHERE around the station is a hard backstop: inside it and off the docking axis is
 * an abort. Offset targeting (aim at the waypoint, never at the station) is the formal version of
 * `falcon-rendezvous-approach-law`'s "never chase the target".
 *
 * ---- THE WP0->WP1 FLY-AROUND (was the one leg that breached the keep-out sphere) ----
 * A STRAIGHT WP0->WP1 chord grazes ~193 m from the station, off the V-bar corridor - inside the 200 m
 * KOS, so `KeepOutBreached` would abort mid-L. Real vehicles fly AROUND the sphere, so this leg is a
 * fly-around: two intermediate waypoints (ToArc1/ToArc2) on the arc `ArcPoint` traces from WP0 to WP1.
 * The arc is a monotone spiral in the radial-along plane - bearing sweeps 180deg (straight down) ->
 * 90deg (straight ahead) while the radius eases 400 -> 220 m. Because BOTH the radius and the bearing
 * move monotonically and both endpoints sit outside the sphere, every point on the arc, and every
 * chord between the sampled waypoints, stays at radius >= 220 m - proven by the sweep test in
 * WaypointApproachTest. The transition waypoints are fly-THROUGH, not holds: the sequencer advances on
 * proximity alone (`Passed`) so the capsule scallops the arc without stopping. The three published
 * stations - WP0, WP1, WP2 - are still full holds. RSS only; stock keeps DirectApproach.
 */
using System;

namespace DragonScreen
{
    public enum WpPhase : byte
    {
        Idle = 0,
        /// <summary>Flying to WP0, 400 m below, from the approach-initiation point.</summary>
        ToWP0,
        Hold0,
        /// <summary>The L, flown as a keep-out-sphere fly-around: arc point 1 of 2 (see ArcPoint).</summary>
        ToArc1,
        /// <summary>Arc point 2 of 2 - the R-bar->V-bar sweep before rolling out onto the V-bar.</summary>
        ToArc2,
        /// <summary>Rolling out onto the V-bar to WP1, 220 m in front.</summary>
        ToWP1,
        Hold1,
        /// <summary>Final standoff run to WP2, 20 m in front.</summary>
        ToWP2,
        Hold2,
        /// <summary>At WP2 - hand to the docking controller for contact and capture.</summary>
        Handover,
        /// <summary>Keep-out breach or corridor violation - release to the crew.</summary>
        Abort
    }

    /// <summary>Our state in the station's LVLH frame. The glue projects the real vectors into this.</summary>
    public struct WpInputs
    {
        public bool Valid;
        public bool HasTarget;
        /// <summary>Our position relative to the station, metres: + ABOVE (higher orbit), - BELOW.</summary>
        public double RadialM;
        /// <summary>+ AHEAD (velocity direction, where the forward port faces), - behind.</summary>
        public double AlongM;
        /// <summary>Out of plane, metres.</summary>
        public double CrossM;
        /// <summary>Straight-line range to the station, metres.</summary>
        public double RangeM;
        /// <summary>Our velocity relative to the station in the same frame, m/s.</summary>
        public double RadialRateMps, AlongRateMps, CrossRateMps;
        public bool Docked;
        /// <summary>Seconds station-kept at the current hold. Auto-GO after WaypointApproach.HoldS.</summary>
        public double HoldElapsedS;
        /// <summary>Crew/ground GO to leave a hold. See RequireCrewGo for whether it is mandatory.</summary>
        public bool Go;
        /// <summary>
        /// The real Crew Dragon rule: a hold is left ONLY on a crew/ground GO. When true, the auto-GO
        /// timer is disabled and the vehicle station-keeps at the waypoint until <see cref="Go"/> arrives -
        /// which is what makes the approach abortable at every stage. When false (nobody conducting), the
        /// hold auto-releases after HoldS so an un-crewed approach still completes.
        /// </summary>
        public bool RequireCrewGo;
    }

    public struct WpCommand
    {
        public WpPhase Phase;
        /// <summary>The LVLH offset we are flying to right now, metres.</summary>
        public double TgtRadialM, TgtAlongM, TgtCrossM;
        /// <summary>Relative velocity to command in the LVLH frame, m/s. The glue turns it into RCS.</summary>
        public double CmdRadialRateMps, CmdAlongRateMps, CmdCrossRateMps;
        /// <summary>True once parked on the current waypoint (position + rate both inside tolerance).</summary>
        public bool Arrived;
        /// <summary>True once within arrival distance of the waypoint, REGARDLESS of speed. Fly-through
        /// arc points advance on this so the capsule scallops the fly-around without stopping; the
        /// published holds still wait for <see cref="Arrived"/> (settled).</summary>
        public bool Passed;
        /// <summary>Hard backstop tripped - inside the keep-out sphere and off the docking corridor.</summary>
        public bool KeepOutBreach;
        public string Note;
    }

    public static class WaypointApproach
    {
        // ---- THE PUBLISHED CREW-2 WAYPOINTS (LVLH, metres, relative to the station) ----
        /// <summary>WP0: 400 m directly below the station, on the R-bar.</summary>
        public const double WP0RadialM = -400.0;
        public const double WP0AlongM = 0.0;
        /// <summary>WP1: 220 m in front, on the docking axis (V-bar).</summary>
        public const double WP1AlongM = 220.0;
        /// <summary>WP2: 20 m in front - the final standoff before the docking controller takes over.</summary>
        public const double WP2AlongM = 20.0;

        /// <summary>Keep-out sphere: a hard no-go around the station except on the docking corridor, m.</summary>
        public const double KeepOutRadiusM = 200.0;
        /// <summary>Half-width of the docking corridor along the V-bar that is exempt from the KOS, m.</summary>
        public const double CorridorRadiusM = 30.0;

        /// <summary>Parked at a waypoint: within this of it, metres.</summary>
        public const double ArriveDistM = 8.0;
        /// <summary>...and below this relative speed, m/s.</summary>
        public const double ArriveRateMps = 0.2;

        /// <summary>Auto-GO out of a hold after this long station-keeping, seconds (crew GO leaves early).</summary>
        public const double HoldS = 15.0;

        // ---- THE APPROACH CORRIDOR RATE: fast far, crawling in ----
        /// <summary>Commanded closing speed per metre to the waypoint, 1/s.</summary>
        public const double CloseRate = 0.05;
        /// <summary>Ceiling on the commanded approach speed, m/s.</summary>
        public const double CloseMax = 10.0;
        /// <summary>Floor, so the last metres still close, m/s.</summary>
        public const double CloseMin = 0.1;

        /// <summary>
        /// A point on the WP0 -> WP1 fly-around arc, s in [0,1] (0 = WP0, 1 = WP1). In the radial-along
        /// plane the bearing sweeps 180deg (straight down, the R-bar) to 90deg (straight ahead, the
        /// V-bar) while the radius eases from WP0's 400 m to WP1's 220 m. Both endpoints are outside the
        /// 200 m keep-out sphere and both the radius and the bearing are monotone in s, so the whole arc
        /// - and every chord between sampled points - stays at radius >= 220 m. That is the property the
        /// straight chord lacked (it grazed ~193 m).
        /// </summary>
        public static void ArcPoint(double s, out double radial, out double along)
        {
            if (s < 0.0) s = 0.0;
            if (s > 1.0) s = 1.0;
            double r0 = -WP0RadialM;          // 400 m: WP0's radius (WP0 is below, radial negative)
            double r1 = WP1AlongM;            // 220 m: WP1's radius, on the +V-bar
            double radius = r0 + (r1 - r0) * s;
            double phi = Math.PI + (0.5 * Math.PI - Math.PI) * s;   // 180deg -> 90deg
            radial = radius * Math.Cos(phi);
            along = radius * Math.Sin(phi);
        }

        /// <summary>The target LVLH offset for a phase's waypoint.</summary>
        public static void Waypoint(WpPhase p, out double radial, out double along, out double cross)
        {
            cross = 0.0;
            switch (p)
            {
                case WpPhase.ToWP0: case WpPhase.Hold0: radial = WP0RadialM; along = WP0AlongM; return;
                case WpPhase.ToArc1: ArcPoint(1.0 / 3.0, out radial, out along); return;
                case WpPhase.ToArc2: ArcPoint(2.0 / 3.0, out radial, out along); return;
                case WpPhase.ToWP1: case WpPhase.Hold1: radial = 0.0;        along = WP1AlongM; return;
                case WpPhase.ToWP2: case WpPhase.Hold2:
                case WpPhase.Handover:                  radial = 0.0;        along = WP2AlongM; return;
                default:                                radial = 0.0;        along = 0.0;       return;
            }
        }

        /// <summary>
        /// Inside the keep-out sphere AND off the docking corridor. The corridor is a tube of radius
        /// CorridorRadiusM around the +V-bar axis in front of the station - the only way in.
        /// </summary>
        public static bool KeepOutBreached(WpInputs s)
        {
            if (s.RangeM >= KeepOutRadiusM) return false;
            // On the docking corridor: ahead of the station (+along) and close to the V-bar axis.
            bool onCorridor = s.AlongM > 0.0
                && Math.Sqrt(s.RadialM * s.RadialM + s.CrossM * s.CrossM) <= CorridorRadiusM;
            return !onCorridor;
        }

        public static WpCommand Guide(WpInputs s, WpPhase phase)
        {
            WpCommand c = new WpCommand();
            c.Phase = phase;

            if (!s.Valid || !s.HasTarget) { c.Phase = WpPhase.Idle; c.Note = "NO TARGET"; return c; }
            if (s.Docked) { c.Phase = WpPhase.Handover; c.Note = "DOCKED"; return c; }

            // ---- HARD BACKSTOP, CHECKED FIRST AND IN EVERY PHASE ----
            if (KeepOutBreached(s))
            {
                c.Phase = WpPhase.Abort;
                c.KeepOutBreach = true;
                c.Note = "KEEP-OUT BREACH - releasing to the crew";
                return c;
            }
            if (phase == WpPhase.Abort) { c.Phase = WpPhase.Abort; c.Note = "ABORTED"; return c; }

            double tr, ta, tc;
            Waypoint(phase, out tr, out ta, out tc);
            c.TgtRadialM = tr; c.TgtAlongM = ta; c.TgtCrossM = tc;

            // Position error to the current waypoint, and how fast we are moving.
            double er = tr - s.RadialM, ea = ta - s.AlongM, ec = tc - s.CrossM;
            double dist = Math.Sqrt(er * er + ea * ea + ec * ec);
            double speed = Math.Sqrt(s.RadialRateMps * s.RadialRateMps
                                   + s.AlongRateMps * s.AlongRateMps
                                   + s.CrossRateMps * s.CrossRateMps);
            c.Arrived = dist <= ArriveDistM && speed <= ArriveRateMps;
            c.Passed = dist <= ArriveDistM;   // reached position, ignoring speed - for fly-through arcs

            bool holding = phase == WpPhase.Hold0 || phase == WpPhase.Hold1 || phase == WpPhase.Hold2;

            if (holding)
            {
                // Station-keep ON the waypoint: null the relative velocity and any residual offset.
                CommandToward(ref c, er, ea, ec, 0.0);   // 0 approach speed - just hold position
                bool go = HoldReleased(s);
                c.Note = go ? "GO" : ("HOLD - station-keeping (" + s.HoldElapsedS.ToString("F0") + " s)");
                // The phase ADVANCE happens in the caller when it reads Arrived/go; see StepPhase.
                return c;
            }

            // A "to-waypoint" leg: fly toward the waypoint at the corridor rate.
            double approach = dist * CloseRate;
            if (approach > CloseMax) approach = CloseMax;
            if (approach < CloseMin) approach = CloseMin;
            CommandToward(ref c, er, ea, ec, approach);
            c.Note = PhaseName(phase) + "  " + dist.ToString("F0") + " m";
            return c;
        }

        /// <summary>
        /// The phase the sequencer should be in NEXT tick, given the command it just produced. Kept
        /// separate from Guide so the state machine advances exactly once per tick (the load-bearing
        /// rule from Ascent): arrive at a to-waypoint -> its hold; GO out of a hold -> the next leg.
        /// </summary>
        public static WpPhase StepPhase(WpInputs s, WpPhase phase, WpCommand c)
        {
            if (c.KeepOutBreach) return WpPhase.Abort;
            bool go = HoldReleased(s);
            switch (phase)
            {
                case WpPhase.Idle:  return WpPhase.ToWP0;
                case WpPhase.ToWP0: return c.Arrived ? WpPhase.Hold0 : WpPhase.ToWP0;
                // Hold0 GOes onto the fly-around, not straight at WP1. The arc points are fly-through:
                // advance on Passed (proximity alone) so the capsule scallops the arc without stopping.
                case WpPhase.Hold0:  return go ? WpPhase.ToArc1 : WpPhase.Hold0;
                case WpPhase.ToArc1: return c.Passed ? WpPhase.ToArc2 : WpPhase.ToArc1;
                case WpPhase.ToArc2: return c.Passed ? WpPhase.ToWP1 : WpPhase.ToArc2;
                case WpPhase.ToWP1: return c.Arrived ? WpPhase.Hold1 : WpPhase.ToWP1;
                case WpPhase.Hold1: return go ? WpPhase.ToWP2 : WpPhase.Hold1;
                case WpPhase.ToWP2: return c.Arrived ? WpPhase.Hold2 : WpPhase.ToWP2;
                case WpPhase.Hold2: return go ? WpPhase.Handover : WpPhase.Hold2;
                default:            return phase;
            }
        }

        /// <summary>
        /// A hold is released on a GO. With RequireCrewGo (a crew is conducting) that GO must be the crew's;
        /// otherwise the auto-GO timer releases it after HoldS so an unconducted approach still completes.
        /// </summary>
        public static bool HoldReleased(WpInputs s)
        {
            return s.RequireCrewGo ? s.Go : (s.Go || s.HoldElapsedS >= HoldS);
        }

        /// <summary>Command a velocity of `approachMps` from us toward the waypoint (offset er,ea,ec).</summary>
        private static void CommandToward(ref WpCommand c, double er, double ea, double ec, double approachMps)
        {
            double d = Math.Sqrt(er * er + ea * ea + ec * ec);
            if (d < 1e-3 || approachMps <= 0.0)
            {
                c.CmdRadialRateMps = 0.0; c.CmdAlongRateMps = 0.0; c.CmdCrossRateMps = 0.0;
                return;
            }
            double k = approachMps / d;
            c.CmdRadialRateMps = er * k;
            c.CmdAlongRateMps = ea * k;
            c.CmdCrossRateMps = ec * k;
        }

        public static string PhaseName(WpPhase p)
        {
            switch (p)
            {
                case WpPhase.ToWP0: return "TO WP0 (400 m below)";
                case WpPhase.Hold0: return "HOLD 0";
                case WpPhase.ToArc1: return "FLY-AROUND 1/2";
                case WpPhase.ToArc2: return "FLY-AROUND 2/2";
                case WpPhase.ToWP1: return "TO WP1 (220 m ahead)";
                case WpPhase.Hold1: return "HOLD 1";
                case WpPhase.ToWP2: return "TO WP2 (20 m)";
                case WpPhase.Hold2: return "HOLD 2";
                case WpPhase.Handover: return "DOCKING";
                case WpPhase.Abort: return "ABORT";
                default: return "STANDBY";
            }
        }
    }
}
