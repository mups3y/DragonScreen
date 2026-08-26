// DragonScreen — Vec3  (autopilot rebuild L3 support: a minimal 3-D vector)
// ============================================================================================
// A small, dependency-free double-precision 3-vector for the orbital/ascent math (UPFG, conic
// propagation, launch geometry). Written fresh for the rebuild — no external primitive. Right-handed;
// the caller keeps a consistent frame (UPFG works in an inertial Earth-centred frame within a tick).
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct Vec3
    {
        public double X, Y, Z;
        public Vec3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public static readonly Vec3 Zero = new Vec3(0, 0, 0);

        public static Vec3 operator +(Vec3 a, Vec3 b) { return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static Vec3 operator -(Vec3 a, Vec3 b) { return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static Vec3 operator -(Vec3 a) { return new Vec3(-a.X, -a.Y, -a.Z); }
        public static Vec3 operator *(Vec3 a, double s) { return new Vec3(a.X * s, a.Y * s, a.Z * s); }
        public static Vec3 operator *(double s, Vec3 a) { return new Vec3(a.X * s, a.Y * s, a.Z * s); }
        public static Vec3 operator /(Vec3 a, double s) { return new Vec3(a.X / s, a.Y / s, a.Z / s); }

        public double SqrMagnitude { get { return X * X + Y * Y + Z * Z; } }
        public double Magnitude { get { return Math.Sqrt(X * X + Y * Y + Z * Z); } }

        public Vec3 Normalized
        {
            get { double m = Magnitude; return m > 1e-12 ? new Vec3(X / m, Y / m, Z / m) : Zero; }
        }

        public static double Dot(Vec3 a, Vec3 b) { return a.X * b.X + a.Y * b.Y + a.Z * b.Z; }

        public static Vec3 Cross(Vec3 a, Vec3 b)
        {
            return new Vec3(a.Y * b.Z - a.Z * b.Y,
                            a.Z * b.X - a.X * b.Z,
                            a.X * b.Y - a.Y * b.X);
        }

        // Angle between two vectors, radians [0, pi].
        public static double Angle(Vec3 a, Vec3 b)
        {
            double d = a.Magnitude * b.Magnitude;
            if (d < 1e-12) return 0.0;
            double c = Dot(a, b) / d;
            if (c > 1.0) c = 1.0; else if (c < -1.0) c = -1.0;
            return Math.Acos(c);
        }

        // Component of a perpendicular to unit vector n (n must be unit). Removes the along-n part.
        public static Vec3 ExcludeUnit(Vec3 a, Vec3 nUnit) { return a - nUnit * Dot(a, nUnit); }

        public bool IsFinite
        {
            get { return !(double.IsNaN(X) || double.IsNaN(Y) || double.IsNaN(Z) ||
                           double.IsInfinity(X) || double.IsInfinity(Y) || double.IsInfinity(Z)); }
        }
    }
}
