/*
 * DragonScreen - LandingSites
 *
 * PURE. Where a booster is allowed to come down, and which ascent flies it there.
 *
 * ---- EVERY NUMBER HERE IS F9I'S, COPIED NOT DERIVED ----
 * `SPACEX/PARAM.ks`. The coordinates are surveyed against the actual pads in this install and the
 * ascent constants are the ones those landings were flown with. Retune them in PARAM.ks, not here.
 *
 * ---- ⛔ THE LAUNCH PAD IS NOT THE LANDING ZONE ----
 * BoosterRecovery captured the vessel's own position at liftoff and used that as the RTLS target.
 * That is close - a few hundred metres - and "close" is the whole problem: the boosters we are
 * copying land 0.34-0.56 m from the mark, so a 400 m target error is three orders of magnitude
 * larger than the thing being tuned. It would have been invisible in the logs and permanent in the
 * results.
 *
 * ---- AND THE DRONESHIP IS NOT A COORDINATE AT ALL ----
 * BOOSTER.ks:147 - it "is parked by hand and moves between missions". It is found by VESSEL NAME and
 * asked where it is. LZ0 is only the fallback for when it is not in the world.
 */
namespace DragonScreen
{
    /// <summary>PARAM.ks: 1 = RTLS, 2 = ASDS (droneship), 3 = expendable.</summary>
    public enum LandingProfile : byte
    {
        Rtls = 1,
        Droneship = 2,
        Expendable = 3
    }

    public struct LandingSite
    {
        public double LatDeg, LonDeg;
        public string Name;

        public LandingSite(string name, double lat, double lon)
        {
            Name = name; LatDeg = lat; LonDeg = lon;
        }
    }

    public static class LandingSites
    {
        // ---- PARAM.ks:21-23, to the digit ----
        /// <summary>Fallback only - the droneship's nominal park, used when it is not in the world.</summary>
        public static readonly LandingSite Lz0 = new LandingSite("LZ-0", -0.11, -70.0);

        /// <summary>LZ-1. The RTLS pad, and the default for a returning first stage.</summary>
        public static readonly LandingSite Lz1 =
            new LandingSite("LZ-1", -0.132287731225158, -74.5494025150112);

        /// <summary>LZ-2. The second pad - a Falcon Heavy side booster's.</summary>
        public static readonly LandingSite Lz2 =
            new LandingSite("LZ-2", -0.140425956708956, -74.5495256417959);

        /// <summary>
        /// THE CAPSULE'S PARACHUTE SPLASHDOWN POINT - ~1 km off shore, seaward of LZ-1.
        ///
        /// The capsule's target follows the LANDING METHOD (user 2026-08-21, wired in DeorbitOps.Engage):
        /// a PARACHUTE landing splashes down about 1 km off shore from LZ-1, a PROPULSIVE landing puts
        /// the capsule exactly on LZ-1 (the pad). So this is the parachute target only.
        ///
        /// Placed 1 km from LZ-1 along the bearing of the previous, known-water splashdown point
        /// (-0.0972, -74.3200, which was ~2.4 km out on a heading just north of east) - i.e. the same
        /// seaward direction, pulled in to 1 km. Kerbin: 1 deg ~ 10.472 km.
        /// </summary>
        public static readonly LandingSite Splashdown =
            new LandingSite("SPLASHDOWN", -0.1179, -74.4550);

        /// <summary>
        /// STOCK droneship vessel name. The stock build parks a droneship VESSEL, found by this name
        /// with its CURRENT position taken (it moves between missions). RSS/RO parks the barge as a
        /// KerbalKonstructs STATIC ("Of Course I Still Love You") which is NOT a vessel, so the RSS
        /// booster aims at a fixed coordinate instead (BoosterRecovery.DroneshipEarth*) - this name is
        /// the stock path only. FindDroneship also falls back to the droneship PART, for either build.
        /// </summary>
        public const string DroneshipVesselName = "DRONESHIP_MAIN";

        /// <summary>The site a profile aims at, before the droneship lookup overrides it.</summary>
        public static LandingSite For(LandingProfile p)
        {
            if (p == LandingProfile.Droneship) return Lz0;
            return Lz1;
        }

        // ------------------------------------------------------------------ ascent, per profile

        /// <summary>
        /// The ascent that puts the booster where that profile can recover it. PARAM.ks RTLSmode /
        /// ASDSmode / EXPENDmode0.
        ///
        /// These are not interchangeable tunings of one ascent - they are three different missions.
        /// RTLS stages EARLY and STEEP (45 degrees, 60 km) so the booster has the propellant and the
        /// altitude to fly home. A droneship stages later and flatter (40 degrees, 70 km) because it
        /// does not have to come back. Flying an RTLS profile and then asking for a droneship landing
        /// wastes most of a stage's performance; flying an ASDS profile and then asking for RTLS does
        /// not leave enough to get home.
        /// </summary>
        public static void AscentFor(LandingProfile p,
                                     out double mecoAngleDeg, out double stageAltM,
                                     out double pitchGain, out double maxPayloadKg)
        {
            if (p == LandingProfile.Droneship)
            {
                mecoAngleDeg = 40.0; stageAltM = 70000.0; pitchGain = 97.0; maxPayloadKg = 9000.0;
            }
            else if (p == LandingProfile.Expendable)
            {
                mecoAngleDeg = 10.0; stageAltM = 70000.0; pitchGain = 72.5; maxPayloadKg = 12000.0;
            }
            else
            {
                mecoAngleDeg = 45.0; stageAltM = 60000.0; pitchGain = 110.0; maxPayloadKg = 7000.0;
            }
        }

        // ---- FlipDeg is LIVE; FlipPower is still reference-only. Split 2026-08-18 (audit D2). ----
        // These were BOTH transcribed from BOOSTER.ks:226/231 and BOTH once labelled "NOT WIRED". An
        // external reviewer was right about FlipPower and stale about FlipDeg: FlipDeg is now called
        // every booster recovery (BoosterRecovery.cs:953, feeding FlipGeometry.Solve for the flip
        // axis), so the blanket "do not read as implemented" banner had gone actively false for it -
        // the exact trap docs/F9I_PORT_MAP.md §2 warns about. FlipPower stays genuinely dead (the
        // rate-limited Flip1 rotation it parameterises does not exist here; our boostback points at
        // the landing zone and lights), so it keeps the NOT WIRED note below.

        /// <summary>
        /// LIVE - called at BoosterRecovery.cs:953. How far past its MECO heading the booster rotates
        /// before boostback. BOOSTER.ks:226 `Flip1(180, 0.333)` RTLS, :231 `Flip1(170, 0.333)`
        /// droneship. 180 is fully reversed, which a return to the launch site needs; a droneship sits
        /// downrange so the stage only trims its trajectory and stops 10 degrees short.
        /// </summary>
        public static double FlipDeg(LandingProfile p)
        {
            return (p == LandingProfile.Droneship) ? 170.0 : 180.0;
        }

        /// <summary>
        /// NOT WIRED. `flipPower` 0.333 at both Flip1 call sites - kept as the read-from-source number
        /// for the rate-limited flip F9I runs and this mod does not. Nothing calls it.
        /// </summary>
        public const double FlipPower = 0.333;

        /// <summary>Does this profile fly the stage home at all?</summary>
        public static bool Recovers(LandingProfile p) { return p != LandingProfile.Expendable; }
    }
}
