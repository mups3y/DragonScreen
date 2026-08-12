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
        /// THE CAPSULE'S SPLASHDOWN POINT - just off shore, east of KSC.
        ///
        /// Crew Dragon comes down in water, not on a pad, and it does so within helicopter reach of
        /// the recovery fleet rather than in mid-ocean. LZ-1 sits at -0.1323, -74.5494; the coast
        /// runs east of the space centre, so this is placed a few kilometres offshore on the same
        /// latitude - close enough to be "just off KSC", far enough that a long entry lands in
        /// water rather than on the runway.
        ///
        /// ⚠ THE CAPSULE MUST NOT AIM AT THE BOOSTER'S PAD. It was defaulting to `Lz1` - the RTLS
        /// pad - so a perfect entry would have put a crewed capsule on concrete, and any error
        /// short of perfect put it inland. The two vehicles come home to different places, which is
        /// true of the real ones as well.
        /// </summary>
        public static readonly LandingSite Splashdown =
            new LandingSite("SPLASHDOWN", -0.0972, -74.3200);

        /// <summary>
        /// BOOSTER.ks:158 looks the droneship up by this name and takes its CURRENT position,
        /// because it is a vessel someone parked, not a fixed coordinate.
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

        // ---- ⚠ REFERENCE ONLY. NOT WIRED TO ANYTHING. DO NOT READ THESE AS IMPLEMENTED. ----
        // An external reviewer flagged exactly this and was right: FlipDeg and FlipPower are
        // transcribed from BOOSTER.ks:226/231 and nothing calls them. They are kept because the
        // numbers were read from source and are worth not losing, but the manoeuvre they describe -
        // F9I's Flip1, a rate-limited rotation to a commanded attitude BEFORE the boostback burn -
        // does not exist in this mod. Our boostback simply points at the landing zone and lights.
        //
        // Recorded as an explicit gap in docs/F9I_PORT_MAP.md. The project rule is "never build a
        // control bound to nothing"; a constant that looks like a setting is the same trap wearing
        // a different hat, so it says so here rather than being quietly correct one day.

        /// <summary>
        /// NOT WIRED. How far past its MECO heading F9I rotates the booster before boostback.
        /// BOOSTER.ks:226 `Flip1(180, 0.333)` RTLS, :231 `Flip1(170, 0.333)` droneship. 180 is fully
        /// reversed, which a return to the launch site needs; a droneship sits downrange so the
        /// stage only trims its trajectory and stops 10 degrees short.
        /// </summary>
        public static double FlipDeg(LandingProfile p)
        {
            return (p == LandingProfile.Droneship) ? 170.0 : 180.0;
        }

        /// <summary>NOT WIRED. `flipPower` 0.333 at both Flip1 call sites.</summary>
        public const double FlipPower = 0.333;

        /// <summary>Does this profile fly the stage home at all?</summary>
        public static bool Recovers(LandingProfile p) { return p != LandingProfile.Expendable; }
    }
}
