// DragonScreen - ArcGeometry
// ---- PORTED FROM MAS, NOT INVENTED ----
// ---- WHY THIS IS THE PURE HALF ----
// ---- ANGLE CONVENTION, STATED ONCE ----
using System;

namespace DragonScreen
{
    public static class ArcGeometry
    {
        public static double ToMathRadians(double instrumentDegrees)
        {
            return (90.0 - instrumentDegrees) * Math.PI / 180.0;
        }

        public static int VertexCount(double sweepDegrees, double degreesPerSegment)
        {
            if (degreesPerSegment <= 0.0) degreesPerSegment = 3.0;
            double sweep = Math.Abs(sweepDegrees);
            int segments = (int)Math.Ceiling(sweep / degreesPerSegment);
            if (segments < 1) segments = 1;
            if (segments > 1024) segments = 1024;
            return segments + 1;
        }

        public static int Arc(float[] outXY, int count,
                              double cx, double cy, double radiusX, double radiusY,
                              double startDeg, double endDeg)
        {
            if (outXY == null || count < 2) return 0;
            if (outXY.Length < count * 2) return 0;

            double step = (endDeg - startDeg) / (count - 1);

            for (int i = 0; i < count; i++)
            {
                double theta = ToMathRadians(startDeg + step * i);
                outXY[i * 2]     = (float)(cx + radiusX * Math.Cos(theta));
                outXY[i * 2 + 1] = (float)(cy + radiusY * Math.Sin(theta));
            }
            return count;
        }

        public static int ArcScreen(float[] outXY, int count,
                                    double cx, double cy, double radius,
                                    double startDeg, double endDeg)
        {
            return Arc(outXY, count, cx, cy, radius, -radius, startDeg, endDeg);
        }

        public static double ValueToAngle(double value, double min, double max,
                                          double startDeg, double endDeg)
        {
            if (max == min) return startDeg;

            double t = (value - min) / (max - min);
            if (t < 0.0) t = 0.0;
            if (t > 1.0) t = 1.0;
            return startDeg + t * (endDeg - startDeg);
        }

        public static bool IsOffScale(double value, double min, double max)
        {
            double lo = Math.Min(min, max);
            double hi = Math.Max(min, max);
            return value < lo || value > hi;
        }
    }
}
