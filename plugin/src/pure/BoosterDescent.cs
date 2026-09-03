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
//
// ⚠ PROVENANCE + STATUS (W3, Wave C, 2026-09-04). RESTORED VERBATIM from `8b81816^`; every W3 edit to
// this file is COMMENT-ONLY (a comment-stripped diff against `8b81816^` is identical). No gen-1 copy of
// this file exists at `0d6423d` or `158eb2a^`, so there was no stripped commentary to recover.
//
// ⛔ EVERY CONSTANT BELOW IS UN-CONVERGED FOR RSS-RO (§B16.8 ruling 2, R1 §7.4). `EntryBurnStartAltM`,
// `EntryBurnCutSpeedMps`, `FinDeployAltM`, `LegsDeployAltM` and `LandedSpeedMps` are RESEARCHED DEFAULTS
// that were NEVER DB-SEEDED and NEVER FLOWN — R1 §5.1 files this module "RSS-RO researched, never
// DB-seeded / ❌ NO — booster LOST", and R1 §4.2 records that the booster was never recovered. The
// research documents they came from (PHASE_2_BOOSTER_RECOVERY_RESEARCH, BOOSTER_GUIDANCE_DESIGN, "§5.0
// vehicle numbers") were DELETED on 2026-09-01 and are not in this repo, so the numbers cannot be
// re-checked against their own source either. Establish the regime from a RECORDED flight before
// trusting any of them (§B16.8 ruling 3 — that needs the BlackBox and an owner glass gate).
//
// ⚠ THE FSM IS FOUR PHASES; §B16.2 SPECIFIES FIVE. This file has Flip → EntryBurn → AeroDescent →
// LandingBurn. §B16.2's profile is BOOSTBACK → COAST → ENTRY BURN → AERO DESCENT → LANDING BURN, and
// the owner settled (2026-09-03, closing G5a-Q2) that BOOSTBACK IS ONE ALWAYS-ENTERED STATE for both
// RTLS and ASDS, with its magnitude and aim-point offset parameterized by target mode — ASDS defaulting
// to a ZERO-MAGNITUDE trim. There is no boostback state here and no `TargetMode` at all, so this module
// as restored can fly neither profile's return leg. That gap is NOT closed by W3 (recovery, not
// rewrite — §B12.8: Wave C is "recovered as a STARTING POINT, not as working code"); it is logged as
// its own register line. Do not read a green `test/BoosterTest.cs` as "the booster FSM is complete".
//
// ⚠ NOTHING CALLS THIS. `pure/BoosterDescent`, `Hoverslam` and `GridFin` have no caller anywhere in
// `plugin/src` or `plugin/test` outside their own test. `BoosterControl.cs` — the gen-2 glue that used
// to drive them — is RECOVER-REFERENCE and STAYS DELETED (CLAUDE.md; R1 §5.2); §B16.1's replacement is
// our own booster core, written FRESH, and that is not this wave. Every flight command on every screen
// is still §14.4(a)'s honest no-op.
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
        public int EngineMode;          // VehicleParts consts: 0=off/AllEngines(unused in recovery), 1=ModeThreeEngine, 2=ModeCentreOnly
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
                    c.EngineMode = VehicleParts.ModeThreeEngine; c.Throttle = 1.0;   // ⛔ VehicleParts const (the bare 3 decoded as the
                                                                            // all/outer set = engines that spent their ignition at liftoff → H1b)
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
                    // and spool mid-braking (craftdump). The glue selects the mode ABSOLUTELY, WHILE OFF and
                    // before ignition, by ACTIVATING THE `CenterOnly` `ModuleEnginesRF` BOUND BY ITS engineID
                    // (§B16.4 step 2 / `OctawebEngines`) — NEVER by cycling NextEngineMode.
                    // ⛔ W3 COMMENT CORRECTION (2026-09-04, comment-only — no code changed): this sentence used
                    // to offer "set ModuleTundraEngineSwitch.selectedIndex = 2" as an equal alternative. It is
                    // NOT one. §B16.4 lists `selectedIndex` among `ModuleTundraEngineSwitch`'s fields that may be
                    // READ for annunciation, and §B16.3 bans that module as a SWITCHING mechanism outright —
                    // writing it is exactly the RO mode-cycle that causes the re-ignitions and lag the owner
                    // directed us away from. The comment predated §B16.3/§B16.4 and would have talked the next
                    // glue author into the one thing the section forbids. Thrust opposes velocity (≈ up,
                    // nulling horizontal drift). Legs in the final metres.
                    c.Phase = BoosterPhase.LandingBurn;
                    c.AimForward = Retro(s.SurfaceVelocity, s.Up);
                    c.Throttle = 1.0;
                    c.EngineMode = VehicleParts.ModeCentreOnly;       // CenterOnly (VehicleParts const; was the bare 1 = ThreeEngine) — one ignition, no re-light
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
