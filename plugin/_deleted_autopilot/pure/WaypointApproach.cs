// DragonScreen - WaypointApproach
// ---- THE PROFILE, IN THE STATION'S LVLH FRAME ----
// ---- WHY WAYPOINTS AND HOLDS, NOT A LINE ----
// ---- THE WP0->WP1 FLY-AROUND (was the one leg that breached the keep-out sphere) ----
using System;

namespace DragonScreen
{
    public enum WpPhase : byte
    {
        Idle = 0,
        ToWP0,
        Hold0,
        ToArc1,
        ToArc2,
        ToWP1,
        Hold1,
        ToWP2,
        Hold2,
        Handover,
        Abort
    }

    public struct WpInputs
    {
        public bool Valid;
        public bool HasTarget;
        public double RadialM;
        public double AlongM;
        public double CrossM;
        public double RangeM;
        public double RadialRateMps, AlongRateMps, CrossRateMps;
        public bool Docked;
        public double HoldElapsedS;
        public bool Go;
        public bool RequireCrewGo;
    }

    public struct WpCommand
    {
        public WpPhase Phase;
        public double TgtRadialM, TgtAlongM, TgtCrossM;
        public double CmdRadialRateMps, CmdAlongRateMps, CmdCrossRateMps;
        public bool Arrived;
        public bool Passed;
        public bool KeepOutBreach;
        public string Note;
    }

    public static class WaypointApproach
    {
        // ---- THE PUBLISHED CREW-2 WAYPOINTS (LVLH, metres, relative to the station) ----
        public const double WP0RadialM = -400.0;
        public const double WP0AlongM = 0.0;
        public const double WP1AlongM = 220.0;
        public const double WP2AlongM = 20.0;

        public const double KeepOutRadiusM = 200.0;
        public const double CorridorRadiusM = 30.0;

        public const double ArriveDistM = 8.0;
        public const double ArriveRateMps = 0.2;

        public const double HoldS = 15.0;

        // ---- THE APPROACH CORRIDOR RATE: fast far, crawling in ----
        public const double CloseRate = 0.05;
        public const double CloseMax = 10.0;
        public const double CloseMin = 0.1;

        public static void ArcPoint(double s, out double radial, out double along)
        {
            if (s < 0.0) s = 0.0;
            if (s > 1.0) s = 1.0;
            double r0 = -WP0RadialM;
            double r1 = WP1AlongM;
            double radius = r0 + (r1 - r0) * s;
            double phi = Math.PI + (0.5 * Math.PI - Math.PI) * s;
            radial = radius * Math.Cos(phi);
            along = radius * Math.Sin(phi);
        }

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

        public static bool KeepOutBreached(WpInputs s)
        {
            if (s.RangeM >= KeepOutRadiusM) return false;
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

            double er = tr - s.RadialM, ea = ta - s.AlongM, ec = tc - s.CrossM;
            double dist = Math.Sqrt(er * er + ea * ea + ec * ec);
            double speed = Math.Sqrt(s.RadialRateMps * s.RadialRateMps
                                   + s.AlongRateMps * s.AlongRateMps
                                   + s.CrossRateMps * s.CrossRateMps);
            c.Arrived = dist <= ArriveDistM && speed <= ArriveRateMps;
            c.Passed = dist <= ArriveDistM;

            bool holding = phase == WpPhase.Hold0 || phase == WpPhase.Hold1 || phase == WpPhase.Hold2;

            if (holding)
            {
                CommandToward(ref c, er, ea, ec, 0.0);
                bool go = HoldReleased(s);
                c.Note = go ? "GO" : ("HOLD - station-keeping (" + s.HoldElapsedS.ToString("F0") + " s)");
                return c;
            }

            double approach = dist * CloseRate;
            if (approach > CloseMax) approach = CloseMax;
            if (approach < CloseMin) approach = CloseMin;
            CommandToward(ref c, er, ea, ec, approach);
            c.Note = PhaseName(phase) + "  " + dist.ToString("F0") + " m";
            return c;
        }

        public static WpPhase StepPhase(WpInputs s, WpPhase phase, WpCommand c)
        {
            if (c.KeepOutBreach) return WpPhase.Abort;
            bool go = HoldReleased(s);
            switch (phase)
            {
                case WpPhase.Idle:  return WpPhase.ToWP0;
                case WpPhase.ToWP0: return c.Arrived ? WpPhase.Hold0 : WpPhase.ToWP0;
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

        public static bool HoldReleased(WpInputs s)
        {
            return s.RequireCrewGo ? s.Go : (s.Go || s.HoldElapsedS >= HoldS);
        }

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
