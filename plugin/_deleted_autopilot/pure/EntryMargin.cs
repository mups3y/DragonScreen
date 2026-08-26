// DragonScreen - EntryMargin
// ---- WHAT THIS IS ----
// ---- ⛔ EVERY NUMBER IS MEASURED, FROM A NAMED FLIGHT. DO NOT ROUND THEM. ----
// ---- AND IT IS ONLY VALID FOR THE ENERGY IT WAS MEASURED AT ----
namespace DragonScreen
{
    public static class EntryMargin
    {
        [Tunable] public static double MarginScale = 0.0;

        public const double ZeroAtM = 9000.0;

        public static double WantLongM(double altitudeM)
        {
            return MarginScale * RawM(altitudeM);
        }

        public static double RawM(double h)
        {
            if (h >= 50000.0) return 215000.0 + (h - 50000.0) * 6.0;
            if (h >= 36000.0) return 126000.0 + (h - 36000.0) * (89000.0 / 14000.0);
            if (h >= 31000.0) return 80000.0 + (h - 31000.0) * (46000.0 / 5000.0);
            if (h >= 28000.0) return 58000.0 + (h - 28000.0) * (22000.0 / 3000.0);
            if (h >= 23000.0) return 28000.0 + (h - 23000.0) * (30000.0 / 5000.0);
            if (h >= 18000.0) return 10000.0 + (h - 18000.0) * (18000.0 / 5000.0);
            if (h >= 14000.0) return 3200.0 + (h - 14000.0) * (1500.0 / 4000.0);
            if (h >= 12000.0) return 1900.0 + (h - 12000.0) * (1300.0 / 2000.0);
            if (h >= ZeroAtM) return (h - ZeroAtM) * (1900.0 / 3000.0);
            return 0.0;
        }

        public static double WantLongClampedM(double altitudeM, double alongTrackRemainingM)
        {
            double want = WantLongM(altitudeM);
            if (alongTrackRemainingM <= 0.0) return 0.0;
            return (want > alongTrackRemainingM) ? alongTrackRemainingM : want;
        }

    }
}
