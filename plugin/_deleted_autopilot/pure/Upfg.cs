// DragonScreen - Upfg (Unified Powered Flight Guidance)
// ---- PROVENANCE ----
// ---- THE FRAME AND THE SIGN TRAP ----
using System;
using MechJebLib.Primitives;

namespace DragonScreen
{
    public struct UpfgTarget
    {
        public V3 Iy;
        public double RadiusM;
        public double SpeedMps;
        public double GammaRad;
    }

    public struct UpfgVehicle
    {
        public double ExhaustVel;
        public double ThrustN;
        public double MassKg;
    }

    public struct UpfgState
    {
        public V3 Vgo, Rbias, Rd, Rgrav, Vprev;
        public double TgoS;
        public bool Initialised;
    }

    public struct UpfgGuidance
    {
        public V3 IF;
        public double TgoS;
        public bool Valid;
    }

    public static class Upfg
    {
        public static UpfgState Init(V3 r, V3 v, double mu, UpfgTarget t, UpfgVehicle veh)
        {
            UpfgState s = new UpfgState();
            V3 ix = V3.Normalize(r);
            V3 iz = V3.Normalize(V3.Cross(ix, t.Iy));
            V3 vdGuess = t.SpeedMps * (Math.Sin(t.GammaRad) * ix + Math.Cos(t.GammaRad) * iz);
            s.Vgo = vdGuess - v;
            double tu = veh.ExhaustVel * veh.MassKg / (veh.ThrustN > 0.0 ? veh.ThrustN : 1.0);
            double dv = s.Vgo.magnitude;
            s.TgoS = tu * (1.0 - Math.Exp(-dv / veh.ExhaustVel));
            s.Rd = t.RadiusM * ix;
            s.Rgrav = (-0.5 * mu / (r.sqrMagnitude * r.magnitude) * s.TgoS * s.TgoS) * r;
            s.Rbias = V3.zero;
            s.Vprev = v;
            s.Initialised = true;
            return s;
        }

        public static UpfgGuidance Step(V3 r, V3 v, double mu, UpfgTarget t, UpfgVehicle veh, ref UpfgState s)
        {
            UpfgGuidance g = new UpfgGuidance();
            if (!s.Initialised) s = Init(r, v, mu, t, veh);

            double ve = veh.ExhaustVel;
            double aT = (veh.MassKg > 0.0) ? veh.ThrustN / veh.MassKg : 0.0;
            if (aT <= 0.0 || ve <= 0.0) { g.Valid = false; g.TgoS = s.TgoS; g.IF = V3.Normalize(s.Vgo); return g; }
            double tu = ve / aT;

            // ---- BLOCK 2: decrement Vgo by the velocity sensed since last call ----
            V3 vgo = s.Vgo - (v - s.Vprev);

            // ---- BLOCK 3: time-to-go for a single stage from |Vgo| via Tsiolkovsky ----
            double dv = vgo.magnitude;
            double tgo = tu * (1.0 - Math.Exp(-dv / ve));
            if (tgo > 0.999 * tu) tgo = 0.999 * tu;

            // ---- BLOCK 4: thrust integrals (single stage, tgoi-1 = 0) ----
            double L = ve * Math.Log(tu / (tu - tgo));
            double J = tu * L - ve * tgo;
            double S = tgo * L - J;
            double Q = S * tu - 0.5 * ve * tgo * tgo;
            double P = Q * tu - 0.5 * ve * tgo * tgo * (tgo / 3.0);
            double H = J * tgo - Q;
            if (L <= 0.0) { g.Valid = false; g.TgoS = tgo; g.IF = V3.Normalize(vgo); return g; }

            // ---- BLOCK 5: steering (linear tangent) ----
            V3 lambda = V3.Normalize(vgo);
            V3 rgrav = s.Rgrav;
            if (s.TgoS > 1e-6) { double k = tgo / s.TgoS; rgrav = (k * k) * rgrav; }
            V3 rgo = s.Rd - (r + v * tgo + rgrav);
            V3 iz = V3.Normalize(V3.Cross(s.Rd, t.Iy));
            V3 rgoxy = rgo - V3.Dot(iz, rgo) * iz;
            double lz = V3.Dot(lambda, iz);
            double rgoz = (S - V3.Dot(lambda, rgoxy)) / (Math.Abs(lz) > 1e-9 ? lz : 1e-9);
            rgo = rgoxy + rgoz * iz + s.Rbias;
            double lambdade = Q - S * J / L;
            V3 lambdadot = (rgo - S * lambda) / (Math.Abs(lambdade) > 1e-9 ? lambdade : 1e-9);
            V3 iF = V3.Normalize(lambda - lambdadot * (J / L));

            double phi = Math.Acos(Clamp(V3.Dot(iF, lambda), -1.0, 1.0));
            double phidot = (Math.Abs(J) > 1e-9) ? -phi * L / J : 0.0;
            V3 ild = V3.Normalize(lambdadot);
            V3 vthrust = (L - 0.5 * L * phi * phi - J * phi * phidot - 0.5 * H * phidot * phidot) * lambda
                       - (L * phi + J * phidot) * ild;
            V3 rthrust = (S - 0.5 * S * phi * phi - Q * phi * phidot - 0.5 * P * phidot * phidot) * lambda
                       - (S * phi + Q * phidot) * ild;
            V3 vbias = vgo - vthrust;
            V3 rbias = rgo - rthrust;

            // ---- BLOCK 7: gravity by conic state extrapolation (our Kepler propagator) ----
            V3 rc1 = r - 0.1 * rthrust - (1.0 / 30.0) * vthrust * tgo;
            V3 vc1 = v + 1.2 * rthrust / tgo - 0.1 * vthrust;
            V3 rc2, vc2;
            if (!Kepler.Propagate(rc1, vc1, mu, tgo, out rc2, out vc2))
            {
                V3 gmid = (-mu / (r.sqrMagnitude * r.magnitude)) * r;
                vc2 = vc1 + gmid * tgo; rc2 = rc1 + vc1 * tgo + 0.5 * gmid * tgo * tgo;
            }
            V3 vgrav = vc2 - vc1;
            rgrav = rc2 - rc1 - vc1 * tgo;

            // ---- BLOCK 8: predict cutoff, rebuild the desired state, update Vgo ----
            V3 rp = r + v * tgo + rgrav + rthrust;
            rp = rp - V3.Dot(rp, t.Iy) * t.Iy;
            V3 rd = t.RadiusM * V3.Normalize(rp);
            V3 ix = V3.Normalize(rd);
            V3 iz2 = V3.Cross(ix, t.Iy);
            V3 vd = t.SpeedMps * (Math.Sin(t.GammaRad) * ix + Math.Cos(t.GammaRad) * iz2);
            vgo = vd - v - vgrav + vbias;

            // ---- store ----
            s.Vgo = vgo; s.Rbias = rbias; s.Rd = rd; s.Rgrav = rgrav; s.TgoS = tgo; s.Vprev = v;

            g.IF = iF; g.TgoS = tgo; g.Valid = true;
            return g;
        }

        private static double Clamp(double x, double lo, double hi)
        {
            return x < lo ? lo : (x > hi ? hi : x);
        }
    }
}
