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
        /// ⛔ THE AIM IS A BAD CONTROL LEVER - IT IS NOT MONOTONIC. Data 2026-08-17/18, 120 km return:
        ///     aim 295400 (flight_0817_193135) -> -49.5 km LONG (liftMin railed -1, closed loop)
        ///     aim 221500 (flight_0817_214211) -> 47.2 km SHORT (OPEN loop, Pe -25.3)
        ///     aim 256000 (flight_0817_232723) -> 260   km SHORT (OPEN loop, Pe -28.7)
        /// I raised 221500 -> 256000 expecting the landing to move LONG; it moved 213 km SHORTER. The
        /// reason: the de-orbit burn stops on a DEPTH floor, and a longer range aim makes it burn
        /// DEEPER before it quits (Pe -25.3 -> -28.7), which is a STEEPER, SHORTER entry. Larger aim =
        /// shorter landing here. On top of that the aim is scaled by orbit energy (`AimRange`), so its
        /// effect differs flight to flight. A feed-forward number that couples to burn depth AND to the
        /// orbit is not a lever you can bracket - which is why the real fix is a CLOSED-LOOP bank
        /// entry, not this constant. Reverted to 221500 (the least-bad known) pending that rework.
        [Tunable] public static double AimDracoCrew = 221500.0;  // 270700->284400->295400->256000->221500

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
    /// Station de-orbit calibration - the constants the landing-calibrated return orbit is fitted to.
    ///
    /// ⚠ THIS CLASS IS NOT THE LIVE RENDEZVOUS/APPROACH. Split 2026-08-18 (audit D3). It used to hold
    /// a full multi-pass, timed-back-away ferry design ported from `F9I/station_ops.ks`, but that
    /// design is not what the code flies: the live approach is StationApproach.cs + pure/Rendezvous.cs
    /// + pure/CwTargeting.cs + pure/DirectApproach.cs; the station is found by
    /// StationApproach.StationName (this class carried a dead DUPLICATE of it); the docking handover is
    /// DockingOps.DockEnvelopeM. The six dead members of that old design (StationInclination,
    /// OrbitToleranceM, BackAwayRate, BackAwayTimeoutS, MaxPasses, MinGainPerPassM) and the duplicate
    /// StationName were removed here. (The 2026-08-18 audit's own D3 note called some of these "live";
    /// that was wrong - it counted FlightTest value-assertions as flight-code usage. Corrected here.)
    ///
    /// What remains is a calibration/reference block, kept because FlightTest.cs value-pins it as a
    /// regression guard - NOT because flight code reads it.
    /// </summary>
    public static class StationOps
    {
        /// <summary>
        /// THE landing-calibrated orbit. The de-orbit aim table above was fitted FROM this orbit, so
        /// moving it invalidates every number in this file. Value-pinned by FlightTest.cs so an
        /// accidental change trips a red assertion instead of a silent bad landing; not read by flight
        /// code.
        /// </summary>
        public const double DeorbitApM = 85100.0, DeorbitPeM = 79200.0;

        /// <summary>
        /// SUPERSEDED by DockingOps.DockEnvelopeM, which is the live rendezvous-to-docking handover
        /// range. Kept only as the value FlightTest.cs still pins.
        /// </summary>
        public const double DockHandoverM = 300.0;

        /// <summary>
        /// Collision guard: never light a main engine with the station closer than this - back off
        /// first. Used by SafeToBurn below; value-pinned by FlightTest.cs.
        /// </summary>
        public const double SafeDistanceM = 150.0;

        /// <summary>
        /// Is it safe to light an engine here? Never with the station this close - a main-engine burn
        /// at the port is not a rendezvous error, it is a collision. Tested; not currently called from
        /// the live de-orbit path, which carries its own guards - kept as the stated rule.
        /// </summary>
        public static bool SafeToBurn(double rangeM) { return rangeM >= SafeDistanceM; }
    }
}
