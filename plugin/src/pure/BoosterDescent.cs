// DragonScreen — BoosterDescent  (autopilot rebuild L3 booster: the recovery FSM)
// ============================================================================================
// Flip → entry burn → grid-fin aero descent → hoverslam landing, for a droneship recovery. Built fresh
// from the research (PHASE_2_BOOSTER_RECOVERY_RESEARCH, BOOSTER_GUIDANCE_DESIGN, §5.0 vehicle numbers).
//
// ⛔ THE CONTRACT: PERFECT CONTROL AT ALL TIMES. Guide() ALWAYS returns a definite unit AimForward — the
// stage is never uncommanded, never drifting at a weird angle of attack. The base attitude is ENGINES-
// FIRST (retrograde: thrust opposes the surface velocity), which is right for every powered phase — it
// sheds speed on the entry burn and, once the descent is near-vertical, thrusts up for the hoverslam and
// nulls any horizontal drift. During the aero descent the stage flies a SMALL, DELIBERATE, CAPPED angle
// of attack (GridFin) tilted off retrograde to steer the predicted impact onto the deck; L2 holds that
// attitude with all authority (cold-gas in vacuum, grid fins + gimbal in air). AoA is a held command,
// not an emergent drift.
// ============================================================================================
using System;

namespace DragonScreen
{
    public enum BoosterPhase : byte { Idle, Flip, EntryBurn, AeroDescent, LandingBurn, Landed }

    public struct BoosterInputs
    {
        public bool Valid;
        public Vec3 SurfaceVelocity;    // surface-relative velocity, world frame
        public Vec3 Up;                 // local radial-up, world frame
        public double AltitudeM;        // height above the deck
        public double SpeedMps;         // |surface velocity| (entry-burn cut)
        public double DescentSpeedMps;  // vertical descent magnitude (hoverslam)

        // grid-fin steering (predicted-impact error on the deck; aim-to-miss until AllNominal)
        public GridFinInputs Fin;
        public bool AllNominal;
        public double OffsetToMissM;    // cross-deck bias applied until nominal

        public HoverslamInputs Land;    // for the landing ignition altitude
    }

    public struct BoosterCommand
    {
        public BoosterPhase Phase;
        public Vec3 AimForward;         // ALWAYS a unit vector — the thrust/engine axis to point
        public double Throttle;
        public int EngineMode;          // 0 = off, 3 = three-engine, 1 = centre engine
        public double AoaDeg;           // the deliberate, held angle of attack (0 outside aero steering)
        public bool DeployFins, DeployLegs;
    }

    public static class BoosterDescent
    {
        [Tunable] public static double EntryBurnStartAltM = 70000.0;   // light the 3-engine entry burn descending through here
        [Tunable] public static double EntryBurnCutSpeedMps = 1300.0;  // §5.0: bleed to a survivable reentry speed
        [Tunable] public static double FinDeployAltM = 70000.0;        // grid fins bite as the air thickens
        public const double LegsDeployAltM = 500.0;
        public const double LandedSpeedMps = 2.0;

        static Vec3 Retro(Vec3 sv, Vec3 up)
        {
            return sv.Magnitude > 1.0 ? (-sv).Normalized : up;   // never undefined
        }

        // Retrograde tilted off by the commanded AoA toward the grid-fin steering direction.
        static Vec3 SteerAim(BoosterInputs s, GridFinCommand fin, out double aoaDeg)
        {
            Vec3 sv = s.SurfaceVelocity, up = s.Up;
            Vec3 retro = Retro(sv, up);
            aoaDeg = fin.AoaDeg;
            if (fin.AoaDeg < 1e-6 || sv.Magnitude < 1.0) return retro;

            Vec3 downHat = (sv - up * Vec3.Dot(sv, up)).Normalized;   // along-track horizontal
            if (downHat.Magnitude < 0.5) return retro;
            Vec3 crossHat = Vec3.Cross(up, downHat).Normalized;
            Vec3 tilt = downHat * fin.TiltDown + crossHat * fin.TiltCross;
            tilt = (tilt - retro * Vec3.Dot(tilt, retro)).Normalized;  // perpendicular to retro
            if (tilt.Magnitude < 0.5) return retro;

            double aoa = fin.AoaDeg * Math.PI / 180.0;
            return (retro * Math.Cos(aoa) + tilt * Math.Sin(aoa)).Normalized;
        }

        public static BoosterCommand Guide(BoosterInputs s, BoosterPhase phase)
        {
            BoosterCommand c = new BoosterCommand();
            c.Phase = phase;
            c.AimForward = s.Valid ? Retro(s.SurfaceVelocity, s.Up) : s.Up;   // ALWAYS defined
            c.Throttle = 0.0; c.EngineMode = 0; c.AoaDeg = 0.0;

            if (!s.Valid) { c.Phase = BoosterPhase.Idle; return c; }

            switch (phase)
            {
                case BoosterPhase.Idle:
                case BoosterPhase.Flip:
                    // Point engines-first and hold it (cold-gas authority in vacuum). Descend to the entry burn.
                    c.Phase = BoosterPhase.Flip;
                    c.AimForward = Retro(s.SurfaceVelocity, s.Up);
                    if (s.AltitudeM <= EntryBurnStartAltM && s.SpeedMps > EntryBurnCutSpeedMps)
                        c.Phase = BoosterPhase.EntryBurn;
                    break;

                case BoosterPhase.EntryBurn:
                    // 3 engines, held retrograde, shedding speed. Cut at the survivable speed → aero descent.
                    c.Phase = BoosterPhase.EntryBurn;
                    c.AimForward = Retro(s.SurfaceVelocity, s.Up);
                    c.EngineMode = 3; c.Throttle = 1.0;
                    if (s.SpeedMps <= EntryBurnCutSpeedMps) { c.Phase = BoosterPhase.AeroDescent; c.EngineMode = 0; c.Throttle = 0.0; }
                    break;

                case BoosterPhase.AeroDescent:
                {
                    // Fly a small held AoA to steer the predicted impact onto the deck. Fins deployed.
                    GridFinCommand fin = GridFin.Steer(s.Fin);
                    double aoa;
                    c.Phase = BoosterPhase.AeroDescent;
                    c.AimForward = SteerAim(s, fin, out aoa);
                    c.AoaDeg = aoa;
                    c.DeployFins = s.AltitudeM <= FinDeployAltM;
                    // hand to the landing burn when we reach the ignition altitude.
                    if (s.AltitudeM <= Hoverslam.IgnitionAltitude(s.Land)) c.Phase = BoosterPhase.LandingBurn;
                    break;
                }

                case BoosterPhase.LandingBurn:
                {
                    // ⛔ Hoverslam on the SINGLE CENTRE ENGINE (CenterOnly), lit ONCE and running continuously
                    // to the deck. This vehicle's octaweb gives each mode (AllEngines/ThreeLanding/CenterOnly)
                    // only ONE ignition, so we do NOT step 3→1 during the burn — that would re-ignite CenterOnly
                    // and spool mid-braking (craftdump). The glue selects the mode ABSOLUTELY (set
                    // ModuleTundraEngineSwitch.selectedIndex = 2 / activate the CenterOnly ModuleEnginesRF) WHILE
                    // OFF before ignition — NEVER by cycling NextEngineMode. Thrust opposes velocity (≈ up,
                    // nulling horizontal drift). Legs in the final metres.
                    c.Phase = BoosterPhase.LandingBurn;
                    c.AimForward = Retro(s.SurfaceVelocity, s.Up);
                    c.Throttle = 1.0;
                    c.EngineMode = 1;                                  // CenterOnly — one engine, one ignition, no re-light
                    c.DeployFins = true;
                    c.DeployLegs = s.AltitudeM <= LegsDeployAltM;
                    if (s.AltitudeM <= 1.0 && s.DescentSpeedMps <= LandedSpeedMps)
                    { c.Phase = BoosterPhase.Landed; c.Throttle = 0.0; c.EngineMode = 0; }
                    break;
                }

                default:
                    c.Phase = BoosterPhase.Landed; c.Throttle = 0.0; c.EngineMode = 0;
                    c.AimForward = s.Up;
                    break;
            }
            return c;
        }
    }
}
