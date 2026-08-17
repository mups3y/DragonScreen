/*
 * DragonScreen - Deorbit
 *
 * PURE. The return leg: de-orbit burn, vacuum trim, and the propulsive landing throttle.
 *
 * ---- ⛔ EVERY CONSTANT HERE IS FITTED FLIGHT DATA FROM `F9I/dragon_deorbit.ks`. ----
 * Not derived, not chosen, not scaled from Earth. Several are annotated in the source with the miss
 * distance they produced, which is the strongest possible provenance a number can have:
 *
 *      dgAimS2Crew    286 000   FITTED - landed 159 m
 *      dgAimS2Cargo   315 450   FITTED - landed 331 m
 *      dgAimDracoCrew 270 700   CONFIRMED by flight 076 (285 200 overshot)
 *
 * Retune them in `dragon_deorbit.ks` against the black box, never here.
 *
 * ---- VARIABLE THRUST, WHICH IS THE WHOLE POINT ----
 * Nothing in this file is bang-bang. F9I throttles on the ERROR, with a different law per phase, and
 * every one of them has a floor and a ceiling:
 *
 *      de-orbit burn   max(peErr/30 km, rgErr/150 km) clamped to 0.02 .. 0.70
 *      coarse trim     sqrt(miss / 400 000)           clamped to 0.01 .. 0.60
 *      fine trim       sqrt(miss / 100 000)           clamped to 0.01 .. 0.35
 *      landing         StopDist / TrueRadar           floored by TrueRadar/40, ceiling 1
 *
 * The sqrt on the trims is the interesting one: it gives near-full authority while the miss is large
 * and then tapers hard, so the capsule stops chasing its own overshoot as it converges. A linear
 * gain there hunts; this does not.
 *
 * The de-orbit ceiling of 0.70 is not timidity either - it is what keeps the burn shortenable. A
 * full-throttle de-orbit overshoots the aim point between guidance ticks and cannot be taken back,
 * because the engines only push one way.
 */
namespace DragonScreen
{
    public enum DeorbitPhase : byte
    {
        Idle = 0,
        /// <summary>Burning to drive periapsis and the impact point onto the aim.</summary>
        Burn,
        /// <summary>Engines off, RCS nulling cross-track and trimming range in vacuum.</summary>
        Trim,
        /// <summary>Handed over to Entry.</summary>
        Entry,
        Done
    }

    public struct DeorbitInputs
    {
        public bool Valid;
        /// <summary>Current periapsis, metres. Goes deeply NEGATIVE for an entry.</summary>
        public double PeriapsisM;
        /// <summary>Predicted impact range from the launch point along the track, metres.</summary>
        public double PredictedRangeM;
        /// <summary>Cross-track miss, metres.</summary>
        public double CrossTrackM;
        /// <summary>Monopropellant remaining, units.</summary>
        public double MonoUnits;
        public bool Crewed;
        /// <summary>True when de-orbiting on Draco rather than the second stage.</summary>
        public bool OnDraco;

        /// <summary>
        /// Orbit altitude the de-orbit is flown FROM, metres. Zero = use the fitted altitude.
        ///
        /// The aim was a bare constant fitted at 86 km, so moving the station to 120 km silently
        /// invalidated it with nothing in the code to say so. See `AimRange`.
        /// </summary>
        public double OrbitAltM;
        /// <summary>Landing under chutes rather than propulsively.</summary>
        public bool ChuteLanding;
    }

    public struct DeorbitCommand
    {
        public DeorbitPhase Phase;
        public double Throttle;
        /// <summary>RCS fore command, -1..1. +1 pushes retrograde, which SHORTENS the range.</summary>
        public double Fore;
        public string Note;
        public double PeriapsisTargetM, AimRangeM;
    }

    public static class Deorbit
    {
        // ---- TARGETS ----
        /// <summary>S2 de-orbit periapsis. The capsule's trim then lifts ~9 km to the real entry.</summary>
        public const double PeriapsisTargetS2 = -40800.0;
        /// <summary>Draco de-orbit: no trim authority, so aim the entry directly.</summary>
        public const double PeriapsisTargetDraco = -31800.0;

        /// <summary>FITTED aim ranges, metres. The comment in the source is the miss they produced.</summary>
        public const double AimS2Crew = 286000.0;     // 159 m
        public const double AimS2Cargo = 315450.0;    // 331 m
        /// <summary>
        /// ⚠ RE-FITTED 2026-08-12 for OUR vehicle: 270 700 -> 284 400 m.
        ///
        /// F9I's 270 700 was confirmed by its flight 076 and is not wrong - for F9I's capsule. Ours
        /// is no longer the same vehicle at entry interface: since the second stage started
        /// performing the orbital insertion, the Dragon arrives with most of its monopropellant
        /// instead of a third of it, which is real mass and a different ballistic coefficient.
        ///
        /// The 2026-08-12 return is the first that flew end to end, and it landed 9.6 km SHORT with
        /// `r_liftMin` flat at zero for the whole entry - the signature of an aim that is too short,
        /// because the loop never once needed to shorten and spent the descent trying to stretch.
        /// The settled miss was 9.2 km, so `9200 / AimGain` = 13 700 m of aim.
        ///
        /// ⛔ ONE FLIGHT, ONE FIT. The sign is unambiguous and the size is small, but this is a
        /// single data point. If the next return lands LONG, halve the change rather than reverting
        /// it, and do not re-fit from `WorstErrorM` - see the note in EntryOps.Handover.
        /// </summary>
        /// ⛔ RE-FIT 2026-08-17 AGAINST A 120 km RETURN AT REPEATABLE MASS: 295400 -> 221500.
        /// The 295400 value was an 86 km fit carried unchanged onto the 120 km station - a longer,
        /// shallower glide - so it aimed long. flight_0817_193135 is the first return the comment's
        /// own recipe applies to cleanly: auto-refuel fills the tank every docked flight, so the mass
        /// is now repeatable, and it landed along -49.5 km (LONG) with liftMin railed at -1 the whole
        /// entry - the entry shortening as hard as it could and still overshooting. The recipe is
        /// `new = current - settledMiss / AimGain` = 295400 - 49500/0.67 = 221500. Read the NEXT
        /// return's miss and re-fit again ONCE if it is not within the entry's own authority; do not
        /// blind-guess between flights. Tunable, so it can also be dialed live in tuning.cfg.
        [Tunable] public static double AimDracoCrew = 221500.0;  // 270700->284400->295400->221500

        /// <summary>De-orbit aims this far PAST the landing zone.</summary>
        public const double OvershootM = 35000.0;

        /// <summary>Closed-loop cutoff: impact within this of target counts as solved.</summary>
        public const double LzToleranceM = 50.0;

        // ---- THE VARIABLE-THROTTLE SPANS ----
        /// <summary>Periapsis error that commands FULL de-orbit throttle.</summary>
        public const double PeSpanM = 30000.0;
        /// <summary>Downrange aim error that commands FULL de-orbit throttle.</summary>
        public const double RangeSpanM = 150000.0;
        /// <summary>Floor - enough to keep closing without slamming past the aim.</summary>
        public const double ThrottleMin = 0.02;
        /// <summary>Ceiling on the S2 de-orbit burn. See the header for why it is not 1.0.</summary>
        public const double ThrottleMax = 0.70;

        /// <summary>Seconds of LEAD on the cutoff, covering the loop tick.</summary>
        public const double CutLeadS = 0.35;

        // ---- TRIM ----
        /// <summary>Cross-track deadband for the vacuum trim, metres.</summary>
        public const double CrossToleranceM = 300.0;
        /// <summary>Range trim deadband. 50 m is unreachable for an RCS trim, so this is 2 km.</summary>
        public const double TrimToleranceM = 2000.0;
        /// <summary>Km of miss closed per km of aim added. MEASURED.</summary>
        [Tunable] public static double AimGain = 0.67;

        // ---- RESERVES ----
        /// <summary>Monoprop kept back for a PROPULSIVE (SuperDraco) landing, units.</summary>
        public const double MonoReservePropulsive = 50.0;
        /// <summary>...and for a parachute landing, which only needs attitude.</summary>
        public const double MonoReserveChute = 12.0;

        public static double PeriapsisTarget(DeorbitInputs s)
        {
            return s.OnDraco ? PeriapsisTargetDraco : PeriapsisTargetS2;
        }

        /// <summary>Altitude the Draco aim was fitted at. See `AimRange`.</summary>
        public const double AimFitAltM = 86000.0;

        /// <summary>
        /// How far PAST the target to place the de-orbit's predicted impact point, metres.
        ///
        /// ---- ⛔ TUNED 2026-08-13 FROM THE ONE RETURN THAT EVER COMPLETED, AND MADE TO SCALE ----
        /// The aim exists because the PREDICTION is drag-free and the real entry is not: the
        /// capsule always falls short of the vacuum impact point, and this is how far short.
        ///
        /// MEASURED, flight_0813_005927 - the only flight in the archive that flew a return end to
        /// end - landed **7.4 km SHORT** with the aim at 284 400 and `r_liftMin` flat zero. Zero
        /// lift means the entry guidance never once needed to SHORTEN, which is the signature of
        /// arriving short, and it has read zero on every return we have. The aim has been too
        /// short the whole time. `settled / AimGain` = 7395 / 0.67 = 11.0 km, so 295 400.
        ///
        /// ⛔ AND SHORT IS THE DANGEROUS DIRECTION, WHICH IS WHY THIS IS NOT SPLIT-THE-DIFFERENCE.
        /// `EntryGuidance` is SHORTEN-ONLY by construction - "Trap 4: shorten or coast. Never
        /// extend." Arrive long and the entry flies the excess off; arrive short and nothing in the
        /// vehicle can recover it. An aim erring long is recoverable, an aim erring short is a miss.
        ///
        /// ---- THE ALTITUDE SCALING, AND WHAT IT IS NOT ----
        /// The station moved from 86 km to 120 km and a bare constant had no way to notice. Entry
        /// range grows with the energy carried through the interface, and interface speed rises
        /// from 2216 m/s (86 km) to 2249 (120 km) for the same target periapsis - so the aim is
        /// scaled by the square of the interface-speed ratio, first order in energy.
        ///
        /// ⚠ THIS IS A FIRST-ORDER CORRECTION, NOT A FIT. No return has ever been flown from
        /// 120 km, so there is nothing to fit against and I will not pretend otherwise. It exists
        /// so the number moves in the right direction and by a defensible amount instead of
        /// silently staying tied to an altitude we no longer fly. The FIRST return from the new
        /// orbit re-fits it properly: read the settled miss, divide by `AimGain`, and set
        /// `AimDracoCrew` - the scaling then rides on top of a fit that is actually current.
        /// </summary>
        public static double AimRange(DeorbitInputs s)
        {
            if (!s.OnDraco) return s.Crewed ? AimS2Crew : AimS2Cargo;

            double aim = AimDracoCrew;
            if (s.OrbitAltM > 0.0 && s.OrbitAltM != AimFitAltM)
                aim *= InterfaceEnergyRatio(s.OrbitAltM);
            return aim;
        }

        /// <summary>
        /// Square of the interface-speed ratio between this orbit and the one the aim was fitted
        /// at - the first-order energy scaling. Vis-viva at the 70 km interface, both de-orbiting
        /// to the same target periapsis.
        /// </summary>
        public static double InterfaceEnergyRatio(double orbitAltM)
        {
            double vNow = InterfaceSpeed(orbitAltM);
            double vFit = InterfaceSpeed(AimFitAltM);
            if (vFit <= 0.0) return 1.0;
            double k = vNow / vFit;
            return k * k;
        }

        /// <summary>Speed at the 70 km entry interface after de-orbiting from `orbitAltM`.</summary>
        public static double InterfaceSpeed(double orbitAltM)
        {
            const double R = 600000.0, MU = 3.5316e12, INTERFACE = 70000.0;
            double ra = R + orbitAltM;
            double rp = R + PeriapsisTargetDraco;
            double ri = R + INTERFACE;
            double a = (ra + rp) / 2.0;
            double t = MU * (2.0 / ri - 1.0 / a);
            return (t > 0.0) ? System.Math.Sqrt(t) : 0.0;
        }

        public static double MonoReserve(DeorbitInputs s)
        {
            return s.ChuteLanding ? MonoReserveChute : MonoReservePropulsive;
        }

        /// <summary>
        /// The de-orbit burn throttle. Whichever error is proportionally larger drives it, so the
        /// burn keeps pushing while EITHER periapsis or range is still short, and eases as both
        /// converge. Clamped both ends - see the header for why the ceiling matters.
        /// </summary>
        public static double BurnThrottle(double periapsisErrorM, double rangeErrorM)
        {
            double tp = (periapsisErrorM > 0.0) ? periapsisErrorM / PeSpanM : 0.0;
            double tr = (rangeErrorM > 0.0) ? rangeErrorM / RangeSpanM : 0.0;
            if (tp > 1.0) tp = 1.0;
            if (tr > 1.0) tr = 1.0;
            double t = (tp > tr) ? tp : tr;
            if (t <= 0.0) return 0.0;
            if (t < ThrottleMin) t = ThrottleMin;
            if (t > ThrottleMax) t = ThrottleMax;
            return t;
        }

        /// <summary>
        /// Trim throttle: sqrt of the miss over a span. Near-full authority while the miss is large,
        /// tapering hard as it closes, so the capsule does not chase its own overshoot.
        /// </summary>
        public static double TrimThrottle(double missM, bool coarse)
        {
            double span = coarse ? 400000.0 : 100000.0;
            double cap = coarse ? 0.60 : 0.35;
            if (missM <= 0.0) return 0.0;
            double t = System.Math.Sqrt(missM / span);
            if (t < 0.01) t = 0.01;
            if (t > cap) t = cap;
            return t;
        }

        /// <summary>
        /// The propulsive landing throttle, `dragon_deorbit.ks:2401`:
        ///
        ///     min(1, max(min(0.05, trueRadar/40), stopDist / max(1, trueRadar)))
        ///
        /// The inner `min(0.05, trueRadar/40)` is a FLOOR that fades out in the last two metres -
        /// it keeps the engines lit and responsive on the way down, then lets them go at touchdown
        /// instead of holding 5% into the ground.
        /// </summary>
        public static double LandingThrottle(double trueRadarM, double stopDistM)
        {
            double floor = trueRadarM / 40.0;
            if (floor > 0.05) floor = 0.05;
            double h = (trueRadarM > 1.0) ? trueRadarM : 1.0;
            double t = stopDistM / h;
            if (t < floor) t = floor;
            if (t > 1.0) t = 1.0;
            return t;
        }

        public static DeorbitCommand Guide(DeorbitInputs s, DeorbitPhase phase)
        {
            DeorbitCommand c = new DeorbitCommand();
            c.Phase = phase;
            if (!s.Valid) { c.Phase = DeorbitPhase.Idle; c.Note = "no vessel"; return c; }

            double peTgt = PeriapsisTarget(s);
            double aim = AimRange(s);
            c.PeriapsisTargetM = peTgt;
            c.AimRangeM = aim;

            // Periapsis error is how much DEEPER we still need to go; range error how much further.
            double peErr = s.PeriapsisM - peTgt;
            double rgErr = aim - s.PredictedRangeM;

            if (phase == DeorbitPhase.Idle) phase = DeorbitPhase.Burn;

            if (phase == DeorbitPhase.Burn && peErr <= 0.0 && rgErr <= 0.0)
                phase = DeorbitPhase.Trim;

            // The trim is finished when BOTH the range and the cross-track are inside their own
            // deadbands. 50 m is the mission tolerance but it is unreachable for an RCS trim, which
            // is why the trim gets 2 km and the closed loop gets the 50.
            double cross = (s.CrossTrackM < 0.0) ? -s.CrossTrackM : s.CrossTrackM;
            double miss = (rgErr < 0.0) ? -rgErr : rgErr;
            if (phase == DeorbitPhase.Trim && miss < TrimToleranceM && cross < CrossToleranceM)
                phase = DeorbitPhase.Entry;

            c.Phase = phase;

            switch (phase)
            {
                case DeorbitPhase.Burn:
                    c.Throttle = BurnThrottle(peErr, rgErr);
                    c.Note = "DEORBIT BURN";
                    break;

                case DeorbitPhase.Trim:
                    // ---- RCS TRIMS BOTH WAYS; THE ENGINES ONLY SHORTEN ----
                    // Steering is locked retrograde, so thrust can only shorten the range. RCS
                    // translates fore AND aft without taking the capsule off retrograde, which is
                    // what lets the trim reach an aim point FURTHER OUT than separation left us.
                    c.Throttle = 0.0;
                    if (miss > TrimToleranceM) c.Fore = (rgErr < 0.0) ? 1.0 : -1.0;
                    c.Note = "VACUUM TRIM";
                    break;

                case DeorbitPhase.Entry:
                    c.Note = "HANDOVER TO ENTRY";
                    break;

                default:
                    c.Note = "STANDBY";
                    break;
            }
            return c;
        }

        public static string Name(DeorbitPhase p)
        {
            switch (p)
            {
                case DeorbitPhase.Burn:  return "DEORBIT BURN";
                case DeorbitPhase.Trim:  return "VACUUM TRIM";
                case DeorbitPhase.Entry: return "ENTRY";
                case DeorbitPhase.Done:  return "DONE";
                default:                 return "STANDBY";
            }
        }
    }

    /// <summary>
    /// Station operations: the constants `F9I/station_ops.ks` flies a ferry mission on.
    ///
    /// `stDeorbitAp` / `stDeorbitPe` are THE landing-calibrated orbit and the source says in as many
    /// words: do not change without re-fitting. The whole de-orbit aim table above was fitted FROM
    /// that orbit, so moving it invalidates every number in this file.
    /// </summary>
    public static class StationOps
    {
        public const string StationName = "Space X Station";
        /// <summary>Station inclination, degrees. Must match the landing-zone latitude.</summary>
        public const double StationInclination = 0.13;

        /// <summary>THE landing-calibrated orbit. Re-fit the aim table if you move it.</summary>
        public const double DeorbitApM = 85100.0, DeorbitPeM = 79200.0;
        /// <summary>How close to that orbit counts as arrived.</summary>
        public const double OrbitToleranceM = 1500.0;

        /// <summary>Hand the rendezvous over to the docking autopilot at this range.</summary>
        public const double DockHandoverM = 300.0;

        /// <summary>Back away this far from the station before ANY burn, and how fast.</summary>
        public const double SafeDistanceM = 150.0;
        public const double BackAwayRate = 1.5;
        public const double BackAwayTimeoutS = 180.0;

        /// <summary>Rendezvous passes allowed before giving up, and the minimum gain per pass.</summary>
        public const int MaxPasses = 8;
        public const double MinGainPerPassM = 250.0;

        /// <summary>
        /// Is it safe to light an engine here? Never with the station this close - back off first.
        /// This is why `stSafeDist` exists at all: a main-engine burn at the port is not a rendezvous
        /// error, it is a collision.
        /// </summary>
        public static bool SafeToBurn(double rangeM) { return rangeM >= SafeDistanceM; }
    }
}
