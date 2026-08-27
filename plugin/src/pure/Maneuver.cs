// DragonScreen — Maneuver  (autopilot rebuild B7: the maneuver-node / finite-burn library)
// ============================================================================================
// The thin library that turns a planned impulsive Δv into a real finite burn, and a target intercept into a
// Δv. Composes the pieces we already have: the rocket equation for the burn duration (as in StageStats), and
// the Lambert solver for the intercept. Feeds the burn executor: start a burn CenterOfBurnLeadS before the
// node so its impulse centroid lands on the node (KerbalEngineer NodeHalfBurnTime). Pure + headless-tested.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class Maneuver
    {
        public const double G0 = 9.80665;

        // Finite-burn DURATION for an impulsive Δv (rocket equation): m1 = m0·exp(−dv/ve); burn = (m0−m1)·ve/F.
        public static double BurnTimeS(double dvMps, double thrustN, double ispS, double massKg)
        {
            double ve = ispS * G0;
            if (dvMps <= 0.0 || thrustN <= 0.0 || ve <= 0.0 || massKg <= 0.0) return 0.0;
            double m1 = massKg * Math.Exp(-dvMps / ve);
            double mprop = massKg - m1;
            double mdot = thrustN / ve;
            return mdot > 0.0 ? mprop / mdot : 0.0;
        }

        // Center-of-burn lead: start the finite burn HALF its duration BEFORE the node, so the burn is split
        // evenly about the node and its impulse centroid lands on it (KerbalEngineer NodeHalfBurnTime).
        public static double CenterOfBurnLeadS(double burnTimeS) { return 0.5 * burnTimeS; }

        // The intercept Δv: solve Lambert from the chaser to the target's FUTURE position over tof, then
        // subtract the chaser's current velocity. ok=false if the Lambert geometry is degenerate.
        public static Vec3 InterceptDv(Vec3 chaserPos, Vec3 chaserVel, Vec3 targetFuturePos,
                                       double tofS, double mu, bool shortWay, out bool ok)
        {
            LambertSolution s = Lambert.Solve(chaserPos, targetFuturePos, tofS, mu, shortWay);
            ok = s.Ok;
            return ok ? (s.V1 - chaserVel) : Vec3.Zero;
        }
    }
}
