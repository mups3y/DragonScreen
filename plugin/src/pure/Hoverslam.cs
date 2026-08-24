/*
 * DragonScreen - Hoverslam
 *
 * PURE. The drag-aware suicide-burn (hoverslam) ignition-point solver for the Falcon booster landing.
 *
 * ---- WHY THIS EXISTS, AND WHAT IT TAKES FROM MECHJEB ----
 * MechJeb's HoverslamSimulation (MechJebLib/HoverslamSimulation) solves the same problem the RIGHT way:
 * NUMERICALLY INTEGRATE the descent and ROOT-SOLVE for the ignition point, instead of a closed form. We
 * take that METHOD. But MechJeb's version is VACUUM - its RHS is `dv = -mu*r/r^3 + thrust*u`, gravity and
 * thrust only, aero is a `TODO` in its own source - and it is a 3D orbital integration on a heavy stack
 * (Tsit5 ODE, BrentRoot, Shepperd, Astro, Phase). The Falcon LANDING burn is a 1D vertical problem
 * DOMINATED BY AERO DRAG (the stage falls at terminal velocity, where drag == gravity), so a drag-free
 * solver mis-times the light exactly like the closed form `StopDist = v^2/(2(a-g))` we replace.
 *
 * So this is MechJeb's method, tailored: a 1D vertical integrator of altitude / vertical speed / mass
 * under gravity + thrust + the MEASURED drag + the Merlin's ~3.5 s spool, bisection-root-solved for the
 * ignition altitude at which a full-throttle burn arrests the stage exactly at the deck. Ignite as late
 * as that allows - the suicide burn - so drag does maximum free braking. All MEASURED inputs; no tuning.
 *
 * Drag model: at terminal velocity the stage falls at constant speed, so drag == gravity there. Drag
 * scales with v^2, so `dragAccel(v) = DragRefAccel * (v/DragRefSpeed)^2` - give it the measured terminal
 * fall (DragRefSpeed = terminal speed, DragRefAccel = g) and it is exact near the deck where the burn
 * lives, with no atmosphere model needed over the short final kilometres.
 */
namespace DragonScreen
{
    /// <summary>Everything the hoverslam solver needs, all MEASURED off the descending stage.</summary>
    public struct HoverslamInputs
    {
        /// <summary>Height above the deck, metres (the booster's own base, not the CoM - see BoosterHeightM).</summary>
        public double AltitudeM;
        /// <summary>Vertical speed, m/s. NEGATIVE descending.</summary>
        public double VerticalSpeed;
        /// <summary>Mass, tonnes.</summary>
        public double MassT;
        /// <summary>Local gravity, m/s^2.</summary>
        public double GravityMps2;
        /// <summary>Full thrust on the engines the landing burn will light, kN.</summary>
        public double ThrustKn;
        /// <summary>Propellant flow at full throttle, t/s (= Thrust/(Isp*g0)). Zero = ignore mass loss.</summary>
        public double MdotTps;
        /// <summary>A drag-accel sample: the deceleration drag makes at DragRefSpeed, m/s^2. At terminal
        /// velocity this equals gravity, which is the easy measurement to feed it.</summary>
        public double DragRefAccel;
        /// <summary>The speed at which DragRefAccel was measured, m/s (e.g. the terminal fall speed).</summary>
        public double DragRefSpeed;
        /// <summary>
        /// Seconds of near-ZERO thrust after the ignition COMMAND, before the engine ramps: the ullage
        /// settle (engine held off, RCS settling propellant) PLUS the RealFuels chamber-pressure build.
        /// MEASURED 5.4 s on flight_0824_031348 (cmd at 1571 m, first real thrust at 283 m). The stage
        /// FREE-FALLS this whole time - modelling it as part of the spool ramp gives phantom early braking
        /// and lights the burn far too low (that flight hit the deck at 192 m/s). Zero = no dead time.
        /// </summary>
        public double DeadTimeS;
        /// <summary>Seconds the engine takes to ramp 0 -> full thrust AFTER DeadTimeS. RO Merlin ~1.2.</summary>
        public double SpoolS;
    }

    public static class HoverslamSolver
    {
        /// <summary>Integration step, seconds. 0.05 is well inside the dynamics and fast to root-solve.</summary>
        private const double DtS = 0.05;

        /// <summary>
        /// The altitude (above the deck) at which to LIGHT the landing burn: the latest altitude from which
        /// a full-throttle burn - through the spool, against gravity, WITH drag helping - still arrests the
        /// stage at the deck. Ignite once the real altitude falls to this. Returns the current altitude if
        /// the stage cannot stop from here (already too late), so the caller lights immediately.
        /// </summary>
        public static double IgnitionAltitude(HoverslamInputs s)
        {
            if (s.ThrustKn <= 0.0 || s.MassT <= 0.0) return s.AltitudeM;

            // Bisection on the ignition altitude. StopAltitude(h) is where the stage comes to rest (v=0)
            // if it lights a full-throttle burn at altitude h with the current descent speed. It rises
            // monotonically with h (more height to burn -> stops higher), so we bracket a root of
            // StopAltitude(h) == 0 (rest exactly at the deck). Bracket: [0, a generous ceiling].
            double lo = 0.0;
            double hi = System.Math.Max(s.AltitudeM * 2.0, 5000.0);
            if (StopAltitude(hi, s) < 0.0) return s.AltitudeM;   // cannot stop even from way up - light now

            for (int i = 0; i < 60; i++)
            {
                double mid = 0.5 * (lo + hi);
                if (StopAltitude(mid, s) > 0.0) hi = mid; else lo = mid;
                if (hi - lo < 0.5) break;
            }
            return 0.5 * (lo + hi);
        }

        /// <summary>
        /// Integrate a full-throttle burn that STARTS at ignition altitude <paramref name="hIgn"/> with the
        /// current descent speed, and return the altitude at which vertical speed reaches zero (the rest
        /// altitude). Positive = stops above the deck (fuel to spare / could have waited), negative = hit
        /// the deck still moving (too late). Models the spool ramp, mass loss and v^2 drag.
        /// </summary>
        public static double StopAltitude(double hIgn, HoverslamInputs s)
        {
            double h = hIgn;
            double v = s.VerticalSpeed;                 // negative, descending
            double m = s.MassT;
            double t = 0.0;
            double g = s.GravityMps2;

            for (int step = 0; step < 20000; step++)
            {
                if (v >= 0.0) return h;                  // arrested
                if (h <= -2000.0) return h;              // ran it into the ground, done bracketing

                // Dead time first: the engine makes no useful thrust through the ullage settle + chamber
                // build, so the stage FREE-FALLS (drag + gravity only). Then the spool ramps from there.
                double thrustAccel = 0.0, spool = 0.0;
                if (t >= s.DeadTimeS)
                {
                    spool = (s.SpoolS > 0.0) ? System.Math.Min(1.0, (t - s.DeadTimeS) / s.SpoolS) : 1.0;
                    thrustAccel = (m > 0.0) ? s.ThrustKn * spool / m : 0.0;       // up
                }
                double dragAccel = DragAccel(-v, s);                              // opposes descent -> up
                double a = -g + dragAccel + thrustAccel;                          // net, up positive

                v += a * DtS;
                h += v * DtS;
                m -= s.MdotTps * spool * DtS;            // fuel only burns once thrust is being made
                t += DtS;
            }
            return h;
        }

        /// <summary>Drag deceleration at descent SPEED (m/s, positive), m/s^2. `DragRefAccel*(speed/DragRefSpeed)^2`.</summary>
        private static double DragAccel(double speed, HoverslamInputs s)
        {
            if (s.DragRefSpeed <= 0.0 || s.DragRefAccel <= 0.0) return 0.0;
            double r = speed / s.DragRefSpeed;
            return s.DragRefAccel * r * r;
        }
    }
}
