// DragonScreen — Upfg  (autopilot rebuild L3 ascent: closed-loop second-stage guidance)
// ============================================================================================
// Unified Powered Flight Guidance in Standard Ascent Mode (Brand-Brown-Higgins, Space Shuttle GN&C
// Equation Document 24). Ported FRESH from the PEGAS reference (Noiredd/PEGAS-MATLAB,
// unifiedPoweredFlightGuidance.m — the primary source the research cites), for the single active stage
// (the MVac to orbit). It is a predictor-corrector: each call re-solves the linear-tangent steering
// (tan β = A·t + B) from the measured state to the target CONSTRAINTS (plane normal iY, cutoff radius,
// speed, flight-path angle), using conic state extrapolation for gravity (pure/Conic.cs, block 7) so it
// never assumes constant g. Converges in a few iterations; on Tgo→0 the conductor commands SECO.
// This is the guidance that closes the RSS orbit where no fixed pitch could. Frame: inertial,
// Earth-centred, consistent within a tick. ⚠ target.Iy is oriented OPPOSITE the angular momentum.
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct UpfgTarget
    {
        public Vec3 Iy;          // orbit plane normal, OPPOSITE the angular-velocity vector (sign trap)
        public double RadiusM;   // cutoff radius
        public double SpeedMps;  // cutoff inertial speed
        public double GammaRad;  // cutoff flight-path angle (0 = horizontal/circular)
    }

    public struct UpfgVehicle
    {
        public double ExhaustVel;  // Isp * g0  (m/s)
        public double ThrustN;
        public double MassKg;
    }

    public struct UpfgState
    {
        public bool Init;
        public Vec3 Vgo, Rd, Rgrav, Rbias, LastV;
        public double Tgo;
    }

    public struct UpfgGuidance
    {
        public Vec3 IF;      // commanded thrust unit vector
        public double TgoS;  // time-to-go to cutoff
        public bool Valid;
    }

    public static class Upfg
    {
        public const double G0 = 9.80665;

        // First guess so the predictor-corrector has something to refine (a rough Vgo + gravity drop).
        public static UpfgState Init(Vec3 r, Vec3 v, double mu, UpfgTarget t, UpfgVehicle veh)
        {
            UpfgState s = new UpfgState();
            double ve = veh.ExhaustVel, aT = veh.ThrustN / veh.MassKg, tu = ve / aT;
            double rmag = r.Magnitude;
            Vec3 up = r.Normalized;

            Vec3 vh = v - up * Vec3.Dot(v, up);                  // horizontal component of current velocity
            if (vh.Magnitude < 1.0) vh = Vec3.Cross(t.Iy, up);   // degenerate (straight up): use in-plane horizontal
            Vec3 vgo = vh.Normalized * t.SpeedMps - v;           // rough Δv-to-go

            double tgoEst = tu * (1.0 - Math.Exp(-vgo.Magnitude / ve));
            double gmag = mu / (rmag * rmag);

            s.Vgo = vgo;
            s.Rgrav = up * (-0.5 * gmag * tgoEst * tgoEst);
            s.Rd = up * t.RadiusM;
            s.Rbias = Vec3.Zero;
            s.LastV = v;
            s.Tgo = 0.0;                                         // first call: skip the rgrav (tgo/tgoPrev)^2 scaling
            s.Init = true;
            return s;
        }

        public static UpfgGuidance Step(Vec3 r, Vec3 v, double mu, UpfgTarget t, UpfgVehicle veh,
                                        ref UpfgState st)
        {
            UpfgGuidance g = new UpfgGuidance();
            if (!st.Init) st = Init(r, v, mu, t, veh);

            double ve = veh.ExhaustVel;
            double aT = veh.ThrustN / veh.MassKg;
            double tu = ve / aT;

            // BLOCK 2 — decrement Vgo by the velocity gained since the last call (the corrector).
            Vec3 dvsensed = v - st.LastV;
            Vec3 vgo = st.Vgo - dvsensed;

            // BLOCK 3/4 — single active stage: L = |Vgo|; thrust integrals (closed forms, tgoi1 = 0).
            double L = vgo.Magnitude;   // = |Vgo|; may legitimately exceed ve (multi-ve stage)
            if (L < 1e-6) { g.IF = st.Vgo.Normalized; g.TgoS = 0.0; g.Valid = g.IF.IsFinite; st.Vgo = vgo; st.LastV = v; return g; }
            double tb = tu * (1.0 - Math.Exp(-L / ve));   // = tgo
            double tgo = tb;
            double J = tu * L - ve * tb;
            double S = -J + tb * L;
            double Q = S * tu - 0.5 * ve * tb * tb;
            double P = Q * tu - 0.5 * ve * tb * tb * (tb / 3.0);
            double H = J * tgo - Q;

            // BLOCK 5 — linear-tangent steering direction.
            Vec3 lambda = vgo.Normalized;
            Vec3 rgrav = st.Rgrav;
            if (st.Tgo != 0.0) { double k = tgo / st.Tgo; rgrav = rgrav * (k * k); }
            Vec3 rgo = st.Rd - (r + v * tgo + rgrav);
            Vec3 iz = Vec3.Cross(st.Rd, t.Iy).Normalized;
            Vec3 rgoxy = rgo - iz * Vec3.Dot(iz, rgo);
            double lamIz = Vec3.Dot(lambda, iz);
            double rgoz = Math.Abs(lamIz) > 1e-9 ? (S - Vec3.Dot(lambda, rgoxy)) / lamIz : 0.0;
            rgo = rgoxy + iz * rgoz + st.Rbias;

            double lambdade = Q - S * J / L;
            Vec3 lambdadot = Math.Abs(lambdade) > 1e-9 ? (rgo - lambda * S) / lambdade : Vec3.Zero;
            Vec3 iF = (lambda - lambdadot * (J / L)).Normalized;

            double cosphi = Vec3.Dot(iF, lambda);
            if (cosphi > 1.0) cosphi = 1.0; else if (cosphi < -1.0) cosphi = -1.0;
            double phi = Math.Acos(cosphi);
            double phidot = J != 0.0 ? -phi * L / J : 0.0;
            Vec3 unitLdot = lambdadot.Normalized;

            Vec3 vthrust = lambda * (L - 0.5 * L * phi * phi - J * phi * phidot - 0.5 * H * phidot * phidot);
            Vec3 rthrust = lambda * (S - 0.5 * S * phi * phi - Q * phi * phidot - 0.5 * P * phidot * phidot)
                           - unitLdot * (S * phi + Q * phidot);
            Vec3 vbias = vgo - vthrust;
            Vec3 rbias = rgo - rthrust;

            // BLOCK 7 — gravity by conic state extrapolation over Tgo (no constant-g assumption).
            Vec3 rc1 = r - rthrust * 0.1 - vthrust * (tgo / 30.0);
            Vec3 vc1 = v + rthrust * (1.2 / tgo) - vthrust * 0.1;
            Vec3 rc2, vc2;
            if (Conic.Propagate(rc1, vc1, mu, tgo, out rc2, out vc2))
            {
                Vec3 vgrav = vc2 - vc1;
                rgrav = rc2 - rc1 - vc1 * tgo;

                // BLOCK 8 — predict cutoff, rebuild the desired cutoff state on the plane, update Vgo.
                Vec3 rp = r + v * tgo + rgrav + rthrust;
                rp = rp - t.Iy * Vec3.Dot(rp, t.Iy);
                Vec3 rd = rp.Normalized * t.RadiusM;
                Vec3 ix = rd.Normalized;
                Vec3 iz8 = Vec3.Cross(ix, t.Iy);
                Vec3 vd = (ix * Math.Sin(t.GammaRad) + iz8 * Math.Cos(t.GammaRad)) * t.SpeedMps;
                Vec3 vgop = vd - v - vgrav + vbias;

                st.Vgo = vgop;
                st.Rd = rd;
                st.Rgrav = rgrav;
                st.Rbias = rbias;
            }
            st.Tgo = tgo;
            st.LastV = v;

            g.IF = iF;
            g.TgoS = tgo;
            g.Valid = iF.IsFinite && tgo > 0.0 && !double.IsNaN(tgo);
            return g;
        }
    }
}
