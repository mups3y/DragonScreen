/*
 * DragonScreen - Orbital
 *
 * PURE. The shared orbital toolbox, ported from `COMMON/GNC.ks`.
 *
 * ---- THIS IS THE LAYER EVERYTHING ELSE STANDS ON, AND IT WAS THE LAST THING I BUILT ----
 * `docs/F9I_PORT_MAP.md` step 1. GNC.ks is 1 513 lines and 48 functions, and until this file existed
 * NONE of it was ported - which is exactly why my ascent, landing and rendezvous kept coming out as
 * re-derivations instead of ports. You cannot faithfully port `StCwSolve` or `DgFindOverflight`
 * without the primitives they are written in terms of.
 *
 * Pure by design: no KSP, no Unity, no vessel. Every function takes the orbit's numbers and returns
 * a number, so the whole toolbox is exercised headless before anything flies it.
 *
 * ---- kOS TRIGONOMETRY IS IN DEGREES. THIS IS IN RADIANS INTERNALLY. ----
 * GNC.ks reads `sin(x)` with x in degrees and sprinkles `constant:radtodeg` where the maths needs
 * radians - `MA1 is EA1 - ecc * constant:radtodeg * sin(EA1)` is Kepler's equation with exactly that
 * conversion baked in. Ported to radians throughout and converted only at the edges, because half a
 * port in degrees and half in radians is a bug generator.
 */
namespace DragonScreen
{
    public static class Orbital
    {
        private const double Deg = 180.0 / System.Math.PI;
        private const double Rad = System.Math.PI / 180.0;
        private const double TwoPi = 2.0 * System.Math.PI;

        /// <summary>Wrap to 0..2pi.</summary>
        public static double Wrap(double r)
        {
            r = r % TwoPi;
            return (r < 0.0) ? r + TwoPi : r;
        }

        // ------------------------------------------------------------------ vis-viva

        /// <summary>
        /// Speed on an orbit at radius r. `GNC.ks` uses this shape all over; F9I also has it as
        /// `StVisViva` in station_ops.
        ///
        ///     v = sqrt( mu * (2/r - 1/a) )
        ///
        /// A hyperbolic orbit has negative `a`, which this handles - the caller does not have to
        /// special-case an escape trajectory, and after the 13:36 flight that matters.
        /// </summary>
        public static double VisViva(double mu, double r, double sma)
        {
            if (r <= 0.0) return 0.0;
            double t = mu * (2.0 / r - 1.0 / sma);
            return (t > 0.0) ? System.Math.Sqrt(t) : 0.0;
        }

        /// <summary>Speed of a circular orbit at radius r. The target of any circularisation.</summary>
        public static double CircularSpeed(double mu, double r)
        {
            if (r <= 0.0) return 0.0;
            return System.Math.Sqrt(mu / r);
        }

        /// <summary>Orbital period. Returns 0 for an unbound orbit rather than NaN.</summary>
        public static double Period(double mu, double sma)
        {
            if (sma <= 0.0 || mu <= 0.0) return 0.0;
            return TwoPi * System.Math.Sqrt(sma * sma * sma / mu);
        }

        // ------------------------------------------------------------------ anomalies

        /// <summary>
        /// True anomaly at a given altitude, both roots: [0] climbing, [1] falling. `AltToTA`.
        ///
        /// ---- ⚠ THE SIGNATURE TRAP F9I FLAGGED IN ITS OWN SOURCE, FIXED HERE ----
        /// GNC.ks's version takes `sma, ecc, body, alt` but then clamps against
        /// `ship:orbit:periapsis/apoapsis` - the SHIP's, not the orbit it was handed. Its own comment
        /// says so: "handing it another vessel's sma/ecc would mix two orbits and return nonsense...
        /// the signature invites the mistake." Both its callers happen to pass the ship's own
        /// figures, so it has never been wrong in flight.
        ///
        /// We will not be so lucky: the rendezvous port has to ask this about the TARGET's orbit. So
        /// periapsis and apoapsis are parameters here and the trap is closed rather than inherited.
        ///
        /// The clamp is not cosmetic either - arccos is undefined outside -1..1, so asking for an
        /// altitude the orbit never reaches would throw instead of answering sensibly.
        /// </summary>
        public static void AltitudeToTrueAnomaly(double sma, double ecc, double bodyRadius,
                                                 double altitude, double periapsis, double apoapsis,
                                                 out double climbing, out double falling)
        {
            double alt = altitude;
            if (alt < periapsis) alt = periapsis;
            if (alt > apoapsis) alt = apoapsis;
            double r = alt + bodyRadius;

            double c = (-sma * ecc * ecc + sma - r) / (ecc * r);
            if (c > 1.0) c = 1.0; else if (c < -1.0) c = -1.0;
            climbing = System.Math.Acos(c);
            falling = TwoPi - climbing;
        }

        /// <summary>True anomaly to eccentric anomaly, radians.</summary>
        public static double TrueToEccentric(double trueAnomaly, double ecc)
        {
            double e2 = 1.0 - ecc * ecc;
            if (e2 < 0.0) e2 = 0.0;
            return Wrap(System.Math.Atan2(System.Math.Sqrt(e2) * System.Math.Sin(trueAnomaly),
                                          ecc + System.Math.Cos(trueAnomaly)));
        }

        /// <summary>Kepler's equation, forwards: eccentric anomaly to mean anomaly.</summary>
        public static double EccentricToMean(double eccentricAnomaly, double ecc)
        {
            return eccentricAnomaly - ecc * System.Math.Sin(eccentricAnomaly);
        }

        /// <summary>True anomaly straight to mean anomaly. The pair used by TimeToAltitude.</summary>
        public static double TrueToMean(double trueAnomaly, double ecc)
        {
            return EccentricToMean(TrueToEccentric(trueAnomaly, ecc), ecc);
        }

        /// <summary>
        /// Seconds from now until the orbit next reaches this altitude. `TimeToAltitude`.
        ///
        /// mode 0 = whichever comes first, 1 = the FALLING crossing, 2 = the CLIMBING crossing.
        ///
        /// Works by mean anomaly, which is the only anomaly that advances linearly with time: convert
        /// both crossings to mean anomaly, take the angular distance from where we are now, divide by
        /// the mean motion.
        ///
        /// ⚠ `FalconCircularize` has a whole comment about the way this fails: if the altitude is
        /// ABOVE apoapsis it never arrives, so the caller waits forever. The clamp in
        /// AltitudeToTrueAnomaly means we return the time to APOAPSIS instead of hanging - but a
        /// caller that needs to know it asked for the impossible must compare its own request
        /// against apoapsis first. Flight 020 sat in that wait for an entire window.
        /// </summary>
        public static double TimeToAltitude(double mu, double sma, double ecc, double bodyRadius,
                                            double periapsis, double apoapsis,
                                            double currentTrueAnomaly, double targetAltitude,
                                            int mode)
        {
            if (sma <= 0.0 || mu <= 0.0) return 0.0;

            double up, down;
            AltitudeToTrueAnomaly(sma, ecc, bodyRadius, targetAltitude, periapsis, apoapsis,
                                  out up, out down);

            double n = System.Math.Sqrt(mu / (sma * sma * sma));   // mean motion, rad/s
            if (n <= 0.0) return 0.0;

            double m0 = TrueToMean(currentTrueAnomaly, ecc);
            double tUp = Wrap(TrueToMean(up, ecc) - m0) / n;
            double tDown = Wrap(TrueToMean(down, ecc) - m0) / n;

            if (mode == 1) return tDown;
            if (mode == 2) return tUp;
            return (tUp < tDown) ? tUp : tDown;
        }

        // ------------------------------------------------------------------ phasing

        /// <summary>
        /// Seconds until the Hohmann phase angle is right. `PhaseTime`.
        ///
        /// `transferAng` is where the target must be when we depart: 180 degrees minus however far it
        /// travels during the half-ellipse. `angDiff` is how far the current phase angle is from
        /// that, and the closing rate is our angular rate minus theirs - which is why a LOWER orbit
        /// catches up.
        ///
        /// ⚠ GNC.ks returns `abs(t)`, and its own comment says why that matters: "it never says
        /// negative, but it also cannot tell you that you have JUST missed the window and the honest
        /// answer is one full synodic period." We return the SIGNED wait instead and let the caller
        /// see a negative, because a rendezvous that departs on a missed window is exactly the class
        /// of error this project keeps paying for.
        /// </summary>
        public static double PhaseWaitSeconds(double mu, double ourSma, double targetSma,
                                              double currentPhaseDeg)
        {
            double ourPeriod = Period(mu, ourSma);
            double tgtPeriod = Period(mu, targetSma);
            if (ourPeriod <= 0.0 || tgtPeriod <= 0.0) return 0.0;

            // The transfer ellipse between the two radii, and how far the target moves during it.
            double transferSma = (ourSma + targetSma) * 0.5;
            double transferTime = Period(mu, transferSma) * 0.5;
            double targetTravelDeg = 360.0 * transferTime / tgtPeriod;
            double wantPhaseDeg = 180.0 - targetTravelDeg;

            double diff = currentPhaseDeg - wantPhaseDeg;
            // Closing rate in degrees per second. Ours minus theirs.
            double rate = 360.0 / ourPeriod - 360.0 / tgtPeriod;
            if (System.Math.Abs(rate) < 1e-12) return 0.0;

            double t = diff / rate;
            // Never report a wait in the past - roll it forward by one synodic period instead. This
            // is the honest version of GNC.ks's abs(): the window has gone, here is the next one.
            double synodic = System.Math.Abs(360.0 / rate);
            while (t < 0.0) t += synodic;
            return t;
        }

        /// <summary>
        /// Synodic period: how long between successive identical phase angles.
        /// Two orbits with the SAME period never line up differently, so this returns 0 for them
        /// rather than dividing by zero - and a caller waiting on it must treat 0 as "never".
        /// </summary>
        public static double SynodicPeriod(double mu, double ourSma, double targetSma)
        {
            double a = Period(mu, ourSma), b = Period(mu, targetSma);
            if (a <= 0.0 || b <= 0.0) return 0.0;
            double d = System.Math.Abs(1.0 / a - 1.0 / b);
            if (d < 1e-15) return 0.0;
            return 1.0 / d;
        }

        // ------------------------------------------------------------------ Hohmann

        /// <summary>
        /// The two burns of a Hohmann transfer between circular radii, m/s. `Hohmann`.
        /// First raises (or lowers) into the ellipse, second circularises at the far end.
        /// Both come out POSITIVE for a raise and NEGATIVE for a lower.
        /// </summary>
        public static void Hohmann(double mu, double r1, double r2, out double dv1, out double dv2)
        {
            dv1 = 0.0; dv2 = 0.0;
            if (mu <= 0.0 || r1 <= 0.0 || r2 <= 0.0) return;
            double a = (r1 + r2) * 0.5;
            dv1 = VisViva(mu, r1, a) - CircularSpeed(mu, r1);
            dv2 = CircularSpeed(mu, r2) - VisViva(mu, r2, a);
        }

        /// <summary>
        /// The dv that circularises at the CURRENT radius, given the horizontal and vertical speed
        /// components. This is `FalconCircBurnVecNow` reduced to scalars for testing - the glue does
        /// the vector version because it needs the state vectors.
        ///
        /// Its whole value is the FIXED POINT: it is zero exactly when the orbit is circular here,
        /// which the prograde-until-periapsis version never had. See the 13:36 escape trajectory.
        /// </summary>
        public static double CircularisationDv(double mu, double r,
                                               double horizontalSpeed, double verticalSpeed)
        {
            double want = CircularSpeed(mu, r);
            double dh = want - horizontalSpeed;
            return System.Math.Sqrt(dh * dh + verticalSpeed * verticalSpeed);
        }

        // ------------------------------------------------------------------ ground

        /// <summary>
        /// Great-circle distance between two lat/lon, metres.
        ///
        /// ⚠ `kerbin-degree-to-metres`: a Kerbin degree is 10 472 m, NOT Earth's 111 320. Taking the
        /// body radius as a parameter is what keeps that right on every body without a table, and
        /// hard-coding Earth's figure has cost this project real miss distances.
        /// </summary>
        public static double GroundRange(double bodyRadius, double lat1, double lon1,
                                         double lat2, double lon2)
        {
            double p1 = lat1 * Rad, p2 = lat2 * Rad;
            double dp = (lat2 - lat1) * Rad;
            double dl = (lon2 - lon1) * Rad;
            double a = System.Math.Sin(dp / 2) * System.Math.Sin(dp / 2)
                     + System.Math.Cos(p1) * System.Math.Cos(p2)
                     * System.Math.Sin(dl / 2) * System.Math.Sin(dl / 2);
            return 2.0 * bodyRadius * System.Math.Atan2(System.Math.Sqrt(a),
                                                        System.Math.Sqrt(1.0 - a));
        }

        /// <summary>Initial bearing from one lat/lon to another, degrees. `DgBearing`.</summary>
        public static double Bearing(double lat1, double lon1, double lat2, double lon2)
        {
            double p1 = lat1 * Rad, p2 = lat2 * Rad;
            double dl = (lon2 - lon1) * Rad;
            double y = System.Math.Sin(dl) * System.Math.Cos(p2);
            double x = System.Math.Cos(p1) * System.Math.Sin(p2)
                     - System.Math.Sin(p1) * System.Math.Cos(p2) * System.Math.Cos(dl);
            double b = System.Math.Atan2(y, x) * Deg;
            return (b < 0.0) ? b + 360.0 : b;
        }

        /// <summary>
        /// Move a lat/lon by a distance along a bearing. `DgOffsetLatLng` - the aim-point arithmetic
        /// the whole de-orbit targeting is built on.
        /// </summary>
        public static void OffsetLatLon(double bodyRadius, double lat, double lon,
                                        double bearingDeg, double distanceM,
                                        out double outLat, out double outLon)
        {
            double d = distanceM / bodyRadius;
            double p1 = lat * Rad, l1 = lon * Rad, b = bearingDeg * Rad;
            double sp = System.Math.Sin(p1) * System.Math.Cos(d)
                      + System.Math.Cos(p1) * System.Math.Sin(d) * System.Math.Cos(b);
            if (sp > 1.0) sp = 1.0; else if (sp < -1.0) sp = -1.0;
            double p2 = System.Math.Asin(sp);
            double l2 = l1 + System.Math.Atan2(
                System.Math.Sin(b) * System.Math.Sin(d) * System.Math.Cos(p1),
                System.Math.Cos(d) - System.Math.Sin(p1) * sp);
            outLat = p2 * Deg;
            outLon = ((l2 * Deg + 540.0) % 360.0) - 180.0;
        }

        /// <summary>
        /// Split a predicted miss into ALONG-track and CROSS-track, metres. `DgDownCross`.
        ///
        /// `alongM` is NEGATIVE when the impact is predicted LONG - past the target - which is where
        /// the entry profile wants it for most of the descent. `crossM` is positive when the target
        /// lies to the RIGHT of the ground track.
        ///
        /// ---- ⛔ THE CROSS TERM IS NOT `miss · sin(Δbearing)`, AND THE OBVIOUS VERSION IS BIASED ----
        /// It was, and it read over a kilometre high. `track` is the ship→impact bearing measured AT
        /// THE SHIP, but the naive formula applies it at the IMPACT POINT, up to 1 165 km away - and a
        /// great-circle bearing rotates along its own path (meridian convergence). On flight 053 that
        /// rotation was only 0.22°, but it multiplies the MISS, which during a de-orbit is the 318 km
        /// aim overshoot rather than a small error: 318 894 · sin(0.22°) = 1 222 m of pure fiction.
        /// Measured that flight, the formula reported 1 473 m of cross when the true perpendicular
        /// offset was 251 m. 1473 − 251 = 1222, exactly. The yaw loop steered to that false null and
        /// kept correcting after the real cross had already passed through zero.
        ///
        /// The version below is the standard cross-track distance - the perpendicular from the TARGET
        /// to the great circle running ship→impact. Both bearings are taken AT THE SHIP, so there is no
        /// lever arm to amplify.
        ///
        /// ⚠ THE ALONG TERM WAS NEVER AFFECTED: it takes cos() of an angle near 180°, which is flat
        /// there, so the same 0.22° costs it under a metre. Only cross sat on the steep part of the
        /// curve. Do not "fix" along to match.
        ///
        /// ⚠ AND THE SIGN IS FLIGHT-VERIFIED, NOT DERIVED. Negating it would invert every cross-track
        /// control on the vehicle at once.
        /// </summary>
        public static void DownCross(double bodyRadius,
                                     double shipLat, double shipLon,
                                     double impactLat, double impactLon,
                                     double tgtLat, double tgtLon,
                                     out double alongM, out double crossM, out double missM)
        {
            missM = GroundRange(bodyRadius, impactLat, impactLon, tgtLat, tgtLon);
            double track = Bearing(shipLat, shipLon, impactLat, impactLon);
            double toTgt = Bearing(impactLat, impactLon, tgtLat, tgtLon);
            alongM = missM * System.Math.Cos((toTgt - track) * Rad);

            double tgtD = GroundRange(bodyRadius, shipLat, shipLon, tgtLat, tgtLon);
            double tgtB = Bearing(shipLat, shipLon, tgtLat, tgtLon);
            double arc = tgtD / bodyRadius;
            double s = System.Math.Sin(arc) * System.Math.Sin((tgtB - track) * Rad);
            if (s > 1.0) s = 1.0; else if (s < -1.0) s = -1.0;
            crossM = System.Math.Asin(s) * bodyRadius;
        }
    }
}
