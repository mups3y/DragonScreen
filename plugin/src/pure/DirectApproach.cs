/*
 * DragonScreen - DirectApproach
 *
 * PURE. The short-range rendezvous: point at the station and close, instead of flying Clohessy-
 * Wiltshire legs. Ported from `F9I/station_ops.ks:1365 StDirectApproach` and `:1334 StDirectDv`.
 *
 * ---- WHY THIS EXISTS WHEN WE ALREADY HAVE A CW LADDER ----
 * Because inside a few kilometres the ladder is the wrong tool, and F9I has the flight to prove it.
 * Flight 029: *"a good intercept, MATCH-VEL at 731.7 s, STATION-DIRECT at 803.7 s - and then
 * APPROACH-500M-1 at 879.6 s, 500M-2, 200M-1, 200M-2, each with its own match burn, taking until
 * 1936 s. The first approach was well targeted and every leg after it was sloppier than the one
 * before, because a CW transfer flown from a standstill 2 km out is solving a problem we do not
 * have."* Twenty minutes of legs to cover two kilometres we were already co-moving across.
 *
 * ⚠ AND THE LAUNCH WINDOW MAKES THIS THE NORMAL CASE, NOT THE EXCEPTION. F9I: *"The launch window
 * now puts us within a few km."* Ours does too, as of today - so a rendezvous that cannot fly this
 * branch spends every arrival in the machinery written to avoid it.
 *
 * ---- ⛔ THE RANGE GATE IS LOAD-BEARING. IT IS NOT A TUNING PARAMETER. ----
 * This is a PURSUIT law, and pursuit is unconditionally unstable along-track in orbit. It is exactly
 * what de-orbited flight 012, which pointed at a station 51 km away and thrust until periapsis
 * reached −159 km. What makes it safe inside `GateM` is that the manoeuvre is SHORT compared with an
 * orbit - a few minutes at 5 m/s - so the orbital coupling has no time to build. Outside that gate it
 * must never run, and `InsideGate` is the only thing standing between this file and flight 012.
 *
 * ---- ⛔ FOUR THINGS HERE ARE COUNTER-INTUITIVE AND EVERY ONE IS A FLIGHT ----
 *
 * 1. ONE VECTOR. DO NOT SPLIT AIM FROM SPEED.
 *        dv = wantSpeed × (unit vector to the station) − (current relative velocity)
 *    Burn that and the relative velocity IS wantSpeed straight at the station: direction and speed
 *    together, because it is a single vector identity. F9I split it into "kill the sideways drift
 *    first, then add closing speed" and flight 037 made three attempts - +2.59 m/s crawling,
 *    −0.05 m/s stopped dead, and −10.84 m/s **in the wrong direction** - because whenever drift
 *    exceeded tolerance the entire commanded burn was the lateral kill and no closing speed was ever
 *    built. The glue owns the vectors; this file owns `wantSpeed`, and there is exactly one of it.
 *
 * 2. THE VECTORING IS CONTINUOUS. A pursuit vector in orbit is valid only at the instant it is
 *    computed. Flight 062, coasting after a good acceleration:
 *        rng 1077.6 clos 4.73 · 854.4/4.49 · 698.2/4.15 · 558.4/3.56 · 451.1/2.52 · 405.4/1.48
 *        · 388.2/0.24 · 388.7/−0.03 → stopped dead 188 m short of the 200 m goal
 *    Closing fell from 4.73 to zero across 230 s and 720 m **with no thrust applied**. The velocity
 *    was never lost - orbital curvature rotated it off the line of sight until none of it pointed at
 *    the station any more. Re-solving every tick is the whole mechanism, not an optimisation.
 *
 * 3. THE COMMANDED SPEED IS MEASURED FROM THE GOAL, NOT FROM THE STATION. `d × CloseRate` still
 *    wanted 4 m/s AT the 200 m handover, so the approach could never arrive at the matched speed
 *    however well it flew - it was always going to get there too fast. `(d − goal) × rate + MatchVel`
 *    goes to exactly `MatchVel` as `d` reaches the goal: the deceleration is built into the profile.
 *
 * 4. THERE IS NO SEPARATE BRAKING PHASE ON THE WAY IN. Handing over to a flip-and-brake at a computed
 *    stopping distance is what put a capsule INTO the station: braking there needs the nose swung
 *    right round to retrograde before the throttle may open, and there is no longer a long coast to
 *    do it in. The speed profile above already brakes. Let it.
 */
namespace DragonScreen
{
    public enum DirectPhase : byte
    {
        Idle = 0,
        /// <summary>Swinging the nose onto the correction vector before any thrust.</summary>
        Vectoring,
        /// <summary>Building the commanded closing speed.</summary>
        Accelerating,
        /// <summary>Holding the vector in, re-solving every tick. Coasts when it can.</summary>
        Closing,
        /// <summary>At the goal; killing what relative velocity is left.</summary>
        Matching,
        Done,
        /// <summary>Outside the gate, or it ran out of time. See `Note`.</summary>
        Refused
    }

    public static class DirectApproach
    {
        // ---- F9I's CONSTANTS. station_ops.ks:496-569, 673-680, 726. ----

        /// <summary>
        /// ⛔ NEVER point at the station from beyond this, metres. `stDirectMax`. See the header:
        /// this is the difference between a three-minute approach and flight 012's re-entry.
        /// </summary>
        public const double GateM = 10000.0;

        /// <summary>Commanded closing speed per metre of range, 1/s. `stCloseRate`.</summary>
        public const double CloseRate = 0.02;
        /// <summary>Hand over to the docking at this range, metres. `stMatchDist`.</summary>
        public const double GoalM = 200.0;
        /// <summary>...and at or below this relative speed, m/s. `stMatchVel`.</summary>
        public const double MatchVelMps = 0.5;

        /// <summary>Correction below this needs no burn, m/s. `stDirectTol`.</summary>
        public const double DvToleranceMps = 0.30;
        /// <summary>Thrust gate: nose must be within this of the correction, degrees. `stAimTol`.</summary>
        public const double AimToleranceDeg = 2.0;
        /// <summary>The initial "pointed at it" gate before the first burn, degrees. `stAimAlign`.</summary>
        public const double AimAlignDeg = 1.5;
        /// <summary>Braking gate: nose within this of retrograde before the throttle opens, degrees.</summary>
        public const double BrakeAlignDeg = 10.0;

        /// <summary>Cap on the acceleration phase, seconds. `stDirectAccMax`.</summary>
        public const double AccelMaxS = 60.0;
        /// <summary>Give up after this long, seconds. `stCloseTime`.</summary>
        public const double CloseTimeoutS = 1800.0;

        /// <summary>Cruise acceleration for approach burns, m/s². `stBurnAccel`.</summary>
        public const double BurnAccel = 1.5;
        /// <summary>Taper the last of the Δv over this long, seconds. `stBurnTaper`.</summary>
        public const double BurnTaperS = 4.0;
        /// <summary>Never command less than this while burning. `stThrMin`.</summary>
        public const double ThrottleMin = 0.01;
        /// <summary>Count the goal reached inside this multiple of its range. `stWpTol`.</summary>
        public const double GoalTolerance = 1.30;

        /// <summary>
        /// May we fly this at all? The one gate that keeps a pursuit law safe.
        /// </summary>
        public static bool InsideGate(double rangeM) { return rangeM <= GateM; }

        /// <summary>
        /// The commanded closing speed at this range, m/s. `StDirectDv`'s inner term.
        ///
        /// ⚠ Trap 3: measured from the GOAL. As the range reaches the goal this becomes exactly
        /// `MatchVelMps`, so the capsule decelerates onto its handover speed by construction rather
        /// than needing a braking phase bolted on afterwards.
        /// </summary>
        public static double WantSpeedMps(double rangeM)
        {
            double taper = ((rangeM - GoalM) * CloseRate) + MatchVelMps;
            if (taper < MatchVelMps) taper = MatchVelMps;
            double cap = Approach.SpeedCap(rangeM);
            return (taper < cap) ? taper : cap;
        }

        /// <summary>
        /// Throttle for a correction of this size. `min(BurnAccel, dv/taper) × mass / thrust`,
        /// floored so the engine stays lit and its gimbal authoritative.
        /// </summary>
        public static double Throttle(double dvMps, double massT, double thrustKn)
        {
            double want = dvMps / BurnTaperS;
            if (want > BurnAccel) want = BurnAccel;
            double t = want * massT / ((thrustKn > 1.0) ? thrustKn : 1.0);
            if (t < ThrottleMin) t = ThrottleMin;
            if (t > 1.0) t = 1.0;
            return t;
        }

        /// <summary>
        /// Should the throttle open at all this tick?
        ///
        /// ⚠ THE HARD CAP IS A HARD CAP. Over the speed limit means NO THRUST - the residue is
        /// cancelled on the way in by the profile itself, never by stopping and starting again.
        /// F9I: "Overspeed is cancelled on the braking burn, never by stopping and starting again."
        /// </summary>
        public static bool Burn(double dvMps, double aimErrorDeg, double closingMps, double rangeM)
        {
            if (dvMps <= DvToleranceMps) return false;
            if (aimErrorDeg >= AimToleranceDeg) return false;
            return closingMps < Approach.SpeedCap(rangeM);
        }

        /// <summary>Arrived: inside the goal (with its tolerance) and slow enough to hand over.</summary>
        public static bool Arrived(double rangeM, double relSpeedMps)
        {
            return rangeM <= GoalM * GoalTolerance && relSpeedMps <= MatchVelMps;
        }
    }
}
