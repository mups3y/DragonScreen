// DragonScreen - Lvlh
// ---- WHY THIS IS ITS OWN TESTED FILE ----
// ---- THE BASIS (identical to StationApproach.BuildState) ----
namespace DragonScreen
{
    public struct LvlhState
    {
        public bool Valid;
        public double RadialM;
        public double AlongM;
        public double CrossM;
        public double RangeM;
        public double RadialRateMps, AlongRateMps, CrossRateMps;
    }

    public static class Lvlh
    {
        // ---- tiny vector helpers in plain doubles (no Unity in src/pure) ----
        private static double Dot(double ax, double ay, double az, double bx, double by, double bz)
        {
            return ax * bx + ay * by + az * bz;
        }

        private static void Cross(double ax, double ay, double az, double bx, double by, double bz,
                                  out double cx, out double cy, out double cz)
        {
            cx = ay * bz - az * by;
            cy = az * bx - ax * bz;
            cz = ax * by - ay * bx;
        }

        private static void Normalise(ref double x, ref double y, ref double z)
        {
            double m = System.Math.Sqrt(x * x + y * y + z * z);
            if (m < 1e-12) { x = 0.0; y = 0.0; z = 0.0; return; }
            x /= m; y /= m; z /= m;
        }

        private static void Basis(
            double stnRx, double stnRy, double stnRz, double stnVx, double stnVy, double stnVz,
            out double xhx, out double xhy, out double xhz,
            out double yhx, out double yhy, out double yhz,
            out double zhx, out double zhy, out double zhz)
        {
            xhx = stnRx; xhy = stnRy; xhz = stnRz;
            Normalise(ref xhx, ref xhy, ref xhz);

            double proj = Dot(stnVx, stnVy, stnVz, xhx, xhy, xhz);
            yhx = stnVx - proj * xhx; yhy = stnVy - proj * xhy; yhz = stnVz - proj * xhz;
            Normalise(ref yhx, ref yhy, ref yhz);

            Cross(xhx, xhy, xhz, yhx, yhy, yhz, out zhx, out zhy, out zhz);
            Normalise(ref zhx, ref zhy, ref zhz);
        }

        public static LvlhState Project(
            double stnRx, double stnRy, double stnRz, double stnVx, double stnVy, double stnVz,
            double relRx, double relRy, double relRz, double relVx, double relVy, double relVz)
        {
            LvlhState s = new LvlhState();

            double xhx, xhy, xhz, yhx, yhy, yhz, zhx, zhy, zhz;
            Basis(stnRx, stnRy, stnRz, stnVx, stnVy, stnVz,
                  out xhx, out xhy, out xhz, out yhx, out yhy, out yhz, out zhx, out zhy, out zhz);
            if (xhx == 0.0 && xhy == 0.0 && xhz == 0.0) return s;

            s.RadialM = Dot(relRx, relRy, relRz, xhx, xhy, xhz);
            s.AlongM = Dot(relRx, relRy, relRz, yhx, yhy, yhz);
            s.CrossM = Dot(relRx, relRy, relRz, zhx, zhy, zhz);
            s.RadialRateMps = Dot(relVx, relVy, relVz, xhx, xhy, xhz);
            s.AlongRateMps = Dot(relVx, relVy, relVz, yhx, yhy, yhz);
            s.CrossRateMps = Dot(relVx, relVy, relVz, zhx, zhy, zhz);
            s.RangeM = System.Math.Sqrt(relRx * relRx + relRy * relRy + relRz * relRz);
            s.Valid = true;
            return s;
        }

        public static void OffsetToWorld(
            double stnRx, double stnRy, double stnRz, double stnVx, double stnVy, double stnVz,
            double radial, double along, double cross,
            out double ox, out double oy, out double oz)
        {
            double xhx, xhy, xhz, yhx, yhy, yhz, zhx, zhy, zhz;
            Basis(stnRx, stnRy, stnRz, stnVx, stnVy, stnVz,
                  out xhx, out xhy, out xhz, out yhx, out yhy, out yhz, out zhx, out zhy, out zhz);
            ox = radial * xhx + along * yhx + cross * zhx;
            oy = radial * xhy + along * yhy + cross * zhy;
            oz = radial * xhz + along * yhz + cross * zhz;
        }
    }
}
