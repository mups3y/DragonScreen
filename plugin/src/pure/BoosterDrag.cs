// DragonScreen - BoosterDrag
// ---- ⛔ WHY A CURVE, NOT A SCALAR (the bug this fixes) ----
namespace DragonScreen
{
    public static class BoosterDrag
    {
        private static readonly double[] Mach = { 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0, 4.5, 5.0 };
        private static readonly double[] Bc   = { 2582, 1485, 1796, 1075, 1331, 1321, 1481, 1580, 1582, 1439 };

        public static double BcAtMach(double mach)
        {
            if (mach <= Mach[0]) return Bc[0];
            int n = Mach.Length;
            if (mach >= Mach[n - 1]) return Bc[n - 1];
            for (int i = 1; i < n; i++)
            {
                if (mach <= Mach[i])
                {
                    double f = (mach - Mach[i - 1]) / (Mach[i] - Mach[i - 1]);
                    return Bc[i - 1] + (Bc[i] - Bc[i - 1]) * f;
                }
            }
            return Bc[n - 1];
        }

        public static double DragFactor(double mach, double pseudoReynolds)
        {
            double bc = BcAtMach(mach);
            return (bc > 1.0) ? 1.0 / bc : 0.0;
        }
    }
}
