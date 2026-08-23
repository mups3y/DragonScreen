/*
 * DragonScreen - Hohmann
 *
 * PURE. The two-impulse transfer that raises (or lowers) one apsis to a target radius. The rendezvous
 * needs it because Crew Dragon inserts LOW - Crew-2 went into a ~190 km orbit and climbed to the ISS at
 * ~420 km over the phasing days - so the approach has to be able to lift the whole orbit ~220 km, not
 * just circularise where it already is. Before this, `StationApproach.MatchAltitude` refused when the
 * station sat above our apoapsis ("never reaches its 420 km") and the rendezvous dead-ended.
 *
 * Everything is vis-viva, mu = GM: the speed on an orbit of semi-major axis a at radius r is
 * sqrt(mu (2/r - 1/a)). A burn AT AN APSIS (where velocity is purely horizontal) changes only the
 * OPPOSITE apsis, so the transfer is: burn prograde at periapsis to raise apoapsis to the target, coast
 * half an orbit, then circularise at the new apoapsis. Two burns, each a plain vis-viva difference.
 */
namespace DragonScreen
{
    public static class Hohmann
    {
        /// <summary>Orbital speed at radius r on an orbit of semi-major axis a (vis-viva). 0 if unbound here.</summary>
        public static double SpeedAt(double r, double a, double mu)
        {
            if (r <= 0.0 || a <= 0.0 || mu <= 0.0) return 0.0;
            double v2 = mu * (2.0 / r - 1.0 / a);
            return v2 > 0.0 ? System.Math.Sqrt(v2) : 0.0;
        }

        /// <summary>
        /// Signed prograde dv at an apsis of radius <paramref name="rBurn"/> to move the semi-major axis
        /// from <paramref name="aOld"/> to <paramref name="aNew"/>. Positive raises the far apsis
        /// (prograde), negative lowers it (retrograde). Apply along the velocity direction at the apsis.
        /// </summary>
        public static double ApsisBurnDv(double rBurn, double aOld, double aNew, double mu)
        {
            return SpeedAt(rBurn, aNew, mu) - SpeedAt(rBurn, aOld, mu);
        }

        /// <summary>
        /// The semi-major axis of the transfer ellipse that touches <paramref name="rBurn"/> at one apsis
        /// and <paramref name="rOther"/> at the other. (rBurn + rOther) / 2.
        /// </summary>
        public static double TransferSma(double rBurn, double rOther)
        {
            return 0.5 * (rBurn + rOther);
        }

        /// <summary>
        /// First burn of a Hohmann from our current orbit up (or down) to a target circular radius: the
        /// prograde dv at the burn apsis (periapsis to raise, apoapsis to lower) that sets the OPPOSITE
        /// apsis onto <paramref name="rTarget"/>. Signed as ApsisBurnDv.
        /// </summary>
        public static double RaiseOppositeApsisDv(double rBurn, double aOld, double rTarget, double mu)
        {
            return ApsisBurnDv(rBurn, aOld, TransferSma(rBurn, rTarget), mu);
        }

        /// <summary>Circularisation dv AT radius r for an orbit currently of semi-major axis a: bring the
        /// speed to the local circular speed sqrt(mu/r). Signed (negative when we are faster than circular,
        /// i.e. at periapsis of an ellipse).</summary>
        public static double CirculariseDv(double r, double a, double mu)
        {
            return SpeedAt(r, r, mu) - SpeedAt(r, a, mu);   // SpeedAt(r,r,..) == sqrt(mu/r)
        }
    }
}
