/*
 * DragonScreen - LaunchWindow
 *
 * PURE. When to launch to reach the station. Ported from `F9I/station_ops.ks:290-311`
 * (`StPhaseAtLaunch`, `StRequiredLead`, `StLaunchPhaseWait`).
 *
 * ---- ⛔ THIS IS NOT THE REAL CREW DRAGON PROCEDURE, AND THAT IS DELIBERATE ----
 * A real Dragon launches into the ISS's PLANE, and the window is set by when the pad rotates under
 * that plane - an instantaneous window, with phasing handled afterwards by the orbit.
 *
 * Our station sits at inclination 0.133 degrees. `falcon-station-ferry`: the plane window is
 * DEGENERATE at the equator - every moment is in plane, so a plane-based window has no solution and
 * no meaning. F9I therefore launches on PHASE ANGLE. Copying the real procedure here would be
 * copying the wrong thing for this world, which is why it is written down rather than left to look
 * like an oversight.
 *
 * ---- CO-ORBITAL PARK: THERE IS NO TRANSFER, SO THERE IS NO TRANSFER LEAD ----
 * We launch into the station's OWN orbit and settle a fixed distance behind it. So the lead the
 * station must have at our insertion is simply that trailing distance expressed as an angle - not a
 * Hohmann phase angle. Taken from the station's live orbit so moving the station needs no code change.
 *
 * ---- AND THE TWO NUMBERS THAT MUST COME FROM THE LAST FLIGHT, NOT FROM A CONSTANT ----
 * `StLaunchPhaseWait` reads ascent time and phase bias back from disk every launch, and F9I says why:
 * "This is the whole reason the window drifted: the ascent changed twice and this number did not
 * follow it." They are measurements, not settings.
 */
namespace DragonScreen
{
    public struct WindowInputs
    {
        /// <summary>Pad longitude, degrees.</summary>
        public double PadLonDeg;
        /// <summary>Station longitude AT OUR PREDICTED INSERTION, degrees.</summary>
        public double StationLonAtInsertionDeg;
        /// <summary>Station semi-major axis, metres. The lead is read off the real orbit.</summary>
        public double StationSmaM;
        /// <summary>Station orbital period, seconds.</summary>
        public double StationPeriodS;
        /// <summary>Vehicle's period in the parking orbit, seconds. Zero if not yet known.</summary>
        public double ParkingPeriodS;

        // ---- MEASURED, NOT CHOSEN. Read back from the last flight. ----
        /// <summary>Seconds from liftoff to insertion, as the last flight actually flew it.</summary>
        public double AscentTimeS;
        /// <summary>Degrees of longitude the ascent gains. Fixed for a given profile.</summary>
        public double AscentLonDeg;
        /// <summary>Correction the last arrival measured. Absorbs everything not modelled.</summary>
        public double PhaseBiasDeg;
        /// <summary>How far behind the station to settle, metres.</summary>
        public double TrailDistM;
    }

    public static class LaunchWindow
    {
        /// <summary>
        /// Phase angle from us to the station at our insertion, degrees, 0..360.
        ///
        /// Our insertion longitude is FIXED whenever we launch - the pad longitude never changes and
        /// the ascent always gains the same measured arc - so this is entirely a question of where the
        /// station will be.
        /// </summary>
        public static double PhaseAtLaunch(WindowInputs w)
        {
            double vehLon = Wrap360(w.PadLonDeg + w.AscentLonDeg);
            double stnLon = Wrap360(w.StationLonAtInsertionDeg);
            return Wrap360(stnLon - vehLon);
        }

        /// <summary>
        /// The lead the station must have at our insertion, degrees.
        ///
        /// CO-ORBITAL: no transfer, so no transfer lead. It is the trailing distance as an angle,
        /// plus whatever the last arrival measured.
        /// </summary>
        public static double RequiredLead(WindowInputs w)
        {
            if (w.StationSmaM <= 0.0) return w.PhaseBiasDeg;
            return (w.TrailDistM / w.StationSmaM) * (180.0 / System.Math.PI) + w.PhaseBiasDeg;
        }

        /// <summary>Signed error between the phase we have and the phase we want, -180..180.</summary>
        public static double PhaseErrorDeg(WindowInputs w)
        {
            return Wrap180(PhaseAtLaunch(w) - RequiredLead(w));
        }

        /// <summary>
        /// Seconds until the window, or zero when it is open now.
        ///
        /// The phase closes at the DIFFERENCE of the two mean motions, not at the station's. Using the
        /// station's alone is a classic way to get a launch window that is consistently late: while we
        /// sit on the pad our own "orbit" is the planet's rotation, and that is what
        /// <see cref="WindowInputs.ParkingPeriodS"/> stands in for once we have one.
        /// </summary>
        public static double SecondsToWindow(WindowInputs w, double bodyRotationPeriodS)
        {
            double err = PhaseErrorDeg(w);
            if (System.Math.Abs(err) < WindowToleranceDeg) return 0.0;

            // Degrees per second the phase angle closes at. On the pad we rotate with the body.
            double stnRate = (w.StationPeriodS > 0.0) ? 360.0 / w.StationPeriodS : 0.0;
            double ourRate = (bodyRotationPeriodS > 0.0) ? 360.0 / bodyRotationPeriodS : 0.0;
            double closing = stnRate - ourRate;
            if (System.Math.Abs(closing) < 1e-9) return -1.0;      // never closes; say so

            // Wait for the error to come round to zero from below.
            double wait = -err / closing;
            double period = 360.0 / System.Math.Abs(closing);
            while (wait < 0.0) wait += period;
            return wait;
        }

        /// <summary>Inside this the window counts as open. A degree is about 25 s of hold.</summary>
        public const double WindowToleranceDeg = 0.25;

        private static double Wrap360(double d)
        {
            d = d % 360.0;
            return (d < 0.0) ? d + 360.0 : d;
        }

        private static double Wrap180(double d)
        {
            d = Wrap360(d);
            return (d > 180.0) ? d - 360.0 : d;
        }
    }
}
