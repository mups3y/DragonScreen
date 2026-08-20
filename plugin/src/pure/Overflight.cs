/*
 * DragonScreen - Overflight
 *
 * PURE. WHEN to leave orbit, and how far out of plane the landing site will be when we get there.
 * Ported from `F9I/dragon_deorbit.ks` - `DgLandLag:821`, `DgSiteInertialAt:805`, `DgOffPlaneAt:839`,
 * `DgTrackMissAt:847`, `DgFindOverflight:856`.
 *
 * ---- ⛔ THE ONE IDEA THIS FILE EXISTS FOR: THE SITE MOVES WHILE WE FALL ----
 * A retrograde burn does not change the orbital PLANE, so where the capsule ends up cross-track is
 * decided by one question only: **is the landing site in the plane at the moment the capsule lands?**
 * Not when it crosses overhead in orbit - when it LANDS, which on Kerbin is about 164 s of body
 * rotation later.
 *
 * F9I asked that question at the wrong time for its whole life and it cost `CargoDragon_070` **55 km**.
 * Kerbin turns 6.1° in the 368 s the naive derivation gives, which swings an equatorial site
 * 6.1·sin(inc) out of plane - 2.1° at inclination 20, about 22 km of cross-track.
 *
 * ---- ⚠ AND ON AN EQUATORIAL ORBIT THE BUG IS INVISIBLE ----
 * The site never leaves an equatorial plane no matter how the clock slips, so the entire error lands
 * in ALONG-track, where the aim and the entry loop absorb it. Same code, 558 m on flight 068 and
 * 55 km on 070. Our station orbit is inclination 0.133°, so **this file cannot be validated by our
 * own flights** - it is carried faithfully for the day one is flown inclined, and its constant is
 * F9I's measured one rather than anything derived here.
 *
 * ---- ⛔ THERE IS NO PLANE-CHANGE BURN HERE, AND THAT IS DELIBERATE ----
 * F9I solves the plane geometry and then refuses to fly it: `dgPlaneChangeEnabled` is FALSE because
 * flights 082-100 flew that path and missed by 54-262 km. Its own source also carries an open
 * contradiction - two computations of the same off-plane angle that disagree by 3.6x to 26x - with an
 * explicit instruction not to reconcile them by guessing. So this file MEASURES the off-plane angle
 * and reports it. It does not burn. See `docs/PORT_PLAN.md` E4.
 *
 * The measurement still earns its place: it is the difference between "we missed" and "we missed
 * cross-track by the amount the geometry predicted before we left orbit".
 */
namespace DragonScreen
{
    /// <summary>The best de-orbit opportunity found, and how good it is.</summary>
    public struct OverflightResult
    {
        public bool Ok;
        /// <summary>Universal time of the pass.</summary>
        public double Ut;
        /// <summary>Ground-track miss at that pass, metres - measured against the site AT TOUCHDOWN.</summary>
        public double TrackMissM;
        /// <summary>Seconds from now until it.</summary>
        public double InS;
        public string Note;
    }

    /// <summary>Ground-track miss at a universal time. Supplied by the glue, which owns the orbit.</summary>
    public delegate double TrackMissAtUt(double ut);

    public static class Overflight
    {
        // ---- F9I's CONSTANTS. dragon_deorbit.ks:110, 455-459. ----

        /// <summary>
        /// Seconds from de-orbit ignition to touchdown. `dgDescentTime`. MEASURED: 835.7 s on flight
        /// 068 and 853.4 s on 070 - stable enough that one constant serves both.
        /// </summary>
        public const double DescentTimeS = 845.0;

        /// <summary>
        /// The fraction of an orbit the de-orbit burn happens before the overflight. `dgLagArcFrac`.
        ///
        /// ⛔ CALIBRATED, NOT DERIVED - AND THE DIFFERENCE COST THREE FLIGHTS. The obvious derivation
        /// (`dgPhaseFrac` = 0.255, the burn's own phasing fraction) gives a 366 s lag. Flights 072 and
        /// 074 flew it and went from 070's +53 216 m of cross to −63 923 and −63 331: the correction
        /// overshot straight through zero and out the other side, and 076 at inclination 53 missed by
        /// 149 km. The MECHANISM was right - it is a pure clock offset and it scales with sin(inc)
        /// exactly as it should - only the magnitude was wrong. Solving cross = 0 from the measured
        /// slope gives 163.5 s, 164.3 s and 163.1 s from three flights at two inclinations. As a
        /// fraction of the period that is 0.358, not 0.255.
        ///
        /// If the aim or the descent time ever moves far, THIS MUST BE RE-FLOWN, NOT RE-DERIVED. The
        /// derivation has already been wrong once.
        /// </summary>
        public const double LagArcFrac = 0.358;

        /// <summary>At most this many orbits are searched for a pass. `dgOrbitCap`.</summary>
        public const double OrbitCap = 5.0;

        /// <summary>
        /// How much better a LATER pass must be before it may steal the choice from an earlier one,
        /// metres of ground-track miss. `dgPassTieMargin`.
        ///
        /// ---- ⛔ WHY A GLOBAL MINIMUM IS A COIN-FLIP OVER AN EQUATORIAL SITE ----
        /// The ground track crosses a near-equatorial landing site once (almost) every lap, and each
        /// crossing is an all-but-equal minimum of `TrackMissM`. Picking the single smallest of them
        /// therefore turns on the last few km of orbit jitter: flight_0820_175458 and _182849 took the
        /// pass at −155.9° lon, flight_0820_181043 took the ADJACENT lap at +172°, all three from the
        /// SAME save, and the de-orbit fired a whole lap apart for a ~300 m swing in where the capsule
        /// came down. Requiring a real improvement before a later pass wins makes near-ties resolve to
        /// the EARLIEST pass every time - the same pass across loads, so the residual is finally a
        /// systematic offset that can be trimmed instead of coin-flip scatter that cannot. An honestly
        /// better pass (an inclined orbit, where the crossings are kilometres apart) still wins by
        /// clearing the margin.
        /// </summary>
        public const double TieMarginM = 3000.0;

        /// <summary>
        /// How far before the overflight the de-orbit burn is lit, as a fraction of an orbit.
        /// `dgPhaseFrac`.
        ///
        /// ---- ⛔ THIS IS NOT `LagArcFrac`, AND CONFUSING THE TWO COST THREE FLIGHTS ----
        /// Two different fractions of the same orbit, 0.255 and 0.358, both about "when":
        ///   · **this one** is the IGNITION LEAD - light the engine this far before the pass, because
        ///     the capsule has to fall for a while after it burns.
        ///   · **`LagArcFrac`** is the LANDING LAG - the site keeps rotating for 164 s after that pass,
        ///     and the cross-track solve must be evaluated where the site will BE.
        /// F9I derived the second from the first, reasoning that a capsule lands one descent after a
        /// burn placed 0.255 of an orbit early. It is the right mechanism and the wrong number, and
        /// flights 072, 074 and 076 paid for the difference. Keep them separate.
        /// </summary>
        public const double PhaseArcFrac = 0.255;

        /// <summary>Never commit to an ignition closer than this, seconds. Room to turn and settle.</summary>
        public const double GoTimeMarginS = 60.0;

        /// <summary>
        /// When to light the de-orbit burn for a given pass. `DgPhasing`'s `dgGoUt`.
        ///
        /// The lead can easily land in the past - the search happily returns a pass that is minutes
        /// away, and the ignition for it was a quarter of an orbit before that. Rolling forward by
        /// whole orbits keeps the same GEOMETRY on a later lap rather than firing late into this one.
        /// </summary>
        public static double GoTimeUt(double overflightUt, double nowUt, double orbitPeriodS)
        {
            double go = overflightUt - (orbitPeriodS * PhaseArcFrac);
            if (orbitPeriodS <= 0.0) return go;
            while (go <= nowUt + GoTimeMarginS) go += orbitPeriodS;
            return go;
        }

        /// <summary>Off-plane angle accepted without comment, degrees. `dgPlaneTol`.</summary>
        public const double PlaneToleranceDeg = 0.05;

        // ---- the coarse/fine sweep. dgFindOverflight. ----
        /// <summary>Coarse sweep step, seconds. 60 s is about 45 km of ground track.</summary>
        public const double CoarseStepS = 60.0;
        /// <summary>First refining step, seconds.</summary>
        public const double RefineStartStepS = 5.0;
        /// <summary>Refining stops below this step, seconds - about 430 m of ground track.</summary>
        public const double RefineFloorS = 0.1;
        /// <summary>Each refining pass searches ±(step × this) around the best so far.</summary>
        public const int RefineHalfWidthSteps = 12;
        /// <summary>...and then divides the step by this.</summary>
        public const double RefineDivisor = 5.0;

        /// <summary>
        /// How long after the de-orbit burn the capsule actually arrives. `DgLandLag`.
        ///
        /// Positive: touchdown happens this long AFTER the orbital overflight the search picks.
        /// </summary>
        public static double LandLagS(double orbitPeriodS)
        {
            return DescentTimeS - (LagArcFrac * orbitPeriodS);
        }

        /// <summary>
        /// Where the landing site's longitude will have rotated to after `elapsedS`, expressed in the
        /// body frame as it is oriented NOW. `DgSiteInertialAt`, reduced to the one number that moves.
        ///
        /// The site is fixed to the ground; the ground turns. Everything else in this file is a
        /// consequence of that.
        /// </summary>
        public static double SiteLonAtDeg(double siteLonDeg, double elapsedS, double rotationPeriodS)
        {
            if (rotationPeriodS <= 0.0) return siteLonDeg;
            return WrapLonDeg(siteLonDeg + (360.0 * elapsedS / rotationPeriodS));
        }

        /// <summary>
        /// Fold a longitude into −180..180.
        ///
        /// ⚠ NOT `Orbital.Wrap`, which wraps RADIANS to 0..2π. Everything geodetic in this project is
        /// in degrees, and the two are one silent factor of 57 apart.
        /// </summary>
        public static double WrapLonDeg(double lonDeg)
        {
            double l = (lonDeg + 180.0) % 360.0;
            if (l < 0.0) l += 360.0;
            return l - 180.0;
        }

        /// <summary>
        /// Ground-track miss at one sample. `DgTrackMissAt`.
        ///
        /// ⚠ `subLonDeg` is the sub-satellite longitude read in the body frame FROZEN AT NOW - which is
        /// what a position propagated to a future time gives you. Winding it BACK by the rotation the
        /// body will have done over `elapsedS` puts it where the ground will actually be underneath.
        /// The elapsed time carries the landing lag, so the answer is the miss AT TOUCHDOWN, not at the
        /// overflight - see the file header.
        /// </summary>
        public static double TrackMissM(double bodyRadiusM, double rotationPeriodS,
                                        double subLatDeg, double subLonDeg, double elapsedS,
                                        double siteLatDeg, double siteLonDeg)
        {
            double lon = subLonDeg;
            if (rotationPeriodS > 0.0)
                lon = WrapLonDeg(subLonDeg - (360.0 * elapsedS / rotationPeriodS));
            return Orbital.GroundRange(bodyRadiusM, subLatDeg, lon, siteLatDeg, siteLonDeg);
        }

        /// <summary>
        /// How far the landing site sits out of the orbital plane, degrees. `DgOffPlaneAt`.
        ///
        /// Both arguments are directions on the unit sphere in the same frame: the orbit's NORMAL, and
        /// the SITE. In plane means 90° apart, so the shortfall from 90 is the error - and its sign is
        /// which side.
        ///
        /// ⚠ THE TWO ARE TAKEN AT DIFFERENT TIMES ON PURPOSE. The plane the capsule will fly is
        /// evaluated at the overflight; the site is evaluated at TOUCHDOWN, one landing lag later.
        /// Evaluating both at the same moment is the `CargoDragon_070` bug.
        /// </summary>
        public static double OffPlaneDeg(double normalLatDeg, double normalLonDeg,
                                         double siteLatDeg, double siteLonDeg)
        {
            const double rad = System.Math.PI / 180.0;
            double c = System.Math.Sin(normalLatDeg * rad) * System.Math.Sin(siteLatDeg * rad)
                     + System.Math.Cos(normalLatDeg * rad) * System.Math.Cos(siteLatDeg * rad)
                     * System.Math.Cos((normalLonDeg - siteLonDeg) * rad);
            if (c > 1.0) c = 1.0; else if (c < -1.0) c = -1.0;
            return 90.0 - (System.Math.Acos(c) / rad);
        }

        /// <summary>The cross-track an off-plane angle becomes at touchdown, metres.</summary>
        public static double CrossTrackFromOffPlaneM(double offPlaneDeg, double bodyRadiusM)
        {
            return offPlaneDeg * System.Math.PI / 180.0 * bodyRadiusM;
        }

        /// <summary>
        /// Find the best pass inside the orbit cap. `DgFindOverflight`.
        ///
        /// ---- ⚠ WHY IT SEARCHES A SHORT WINDOW RATHER THAN WAITING FOR A GOOD ONE ----
        /// The ground track's equator crossing walks 31.7° WEST per orbit, so inside 5 orbits the best
        /// available pass is 7° off-plane at inclination 20 and 17° at 53. Waiting for the crossings to
        /// walk onto the site takes about 17 orbits - **ten hours of warp** - and F9I judged that not
        /// worth it. We inherit the judgement and, unlike F9I, we do not have a plane change to buy the
        /// difference with, so what this returns IS the answer.
        ///
        /// Three-stage sweep purely for cost: a flat 20 s step was 45 km of ground track per sample,
        /// and coarse-then-refine reaches 0.2 s for about the same number of evaluations.
        /// </summary>
        public static OverflightResult Search(double nowUt, double orbitPeriodS, TrackMissAtUt f)
        {
            OverflightResult r = new OverflightResult();
            r.Ut = nowUt;
            r.TrackMissM = 9.9e12;

            if (f == null || orbitPeriodS <= 0.0)
            {
                r.Note = "no orbit to search";
                return r;
            }

            // ⛔ The coarse sweep prefers the EARLIEST pass among near-equal ones (see TieMarginM): a
            // later sample must beat the incumbent by a real margin to steal the choice, so equatorial
            // laps - which are all-but-equal minima - resolve to the same pass across loads. The
            // refinement below then hones that chosen pass with a strict `<`, since ±12 steps stay
            // inside one crossing and cannot wander to another lap.
            double end = orbitPeriodS * OrbitCap;
            for (double t = 0.0; t <= end; t += CoarseStepS)
            {
                double m = f(nowUt + t);
                if (m < r.TrackMissM - TieMarginM) { r.TrackMissM = m; r.Ut = nowUt + t; }
            }

            double step = RefineStartStepS;
            while (step >= RefineFloorS)
            {
                double lo = r.Ut - (step * RefineHalfWidthSteps);
                double hi = r.Ut + (step * RefineHalfWidthSteps);
                if (lo < nowUt) lo = nowUt;
                for (double u = lo; u <= hi; u += step)
                {
                    double m = f(u);
                    if (m < r.TrackMissM) { r.TrackMissM = m; r.Ut = u; }
                }
                step = step / RefineDivisor;
            }

            r.Ok = true;
            r.InS = r.Ut - nowUt;
            r.Note = "pass in " + r.InS.ToString("F0") + " s, track miss "
                   + (r.TrackMissM / 1000.0).ToString("F1") + " km";
            return r;
        }
    }
}
