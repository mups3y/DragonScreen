// DragonScreen - LandingSites
// ---- EVERY NUMBER HERE IS F9I'S, COPIED NOT DERIVED ----
// ---- ⛔ THE LAUNCH PAD IS NOT THE LANDING ZONE ----
// ---- AND THE DRONESHIP IS NOT A COORDINATE AT ALL ----
namespace DragonScreen
{
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
        public static readonly LandingSite Lz0 = new LandingSite("LZ-0", -0.11, -70.0);

        public static readonly LandingSite Lz1 =
            new LandingSite("LZ-1", -0.132287731225158, -74.5494025150112);

        public static readonly LandingSite Lz2 =
            new LandingSite("LZ-2", -0.140425956708956, -74.5495256417959);

        public static readonly LandingSite Splashdown =
            new LandingSite("SPLASHDOWN", -0.1179, -74.4550);

        public const string DroneshipVesselName = "DRONESHIP_MAIN";

        public static LandingSite For(LandingProfile p)
        {
            if (p == LandingProfile.Droneship) return Lz0;
            return Lz1;
        }

        // ------------------------------------------------------------------ ascent, per profile

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

        public static double FlipDeg(LandingProfile p)
        {
            return (p == LandingProfile.Droneship) ? 170.0 : 180.0;
        }

        public const double FlipPower = 0.333;

        public static bool Recovers(LandingProfile p) { return p != LandingProfile.Expendable; }
    }
}
