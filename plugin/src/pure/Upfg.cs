/*
 * DragonScreen - Upfg (Unified Powered Flight Guidance)
 *
 * PURE. Closed-loop optimal ascent guidance - the linear-tangent predictor-corrector the Space Shuttle
 * flew (Brand, Brown & Higgins, "Unified Powered Flight Guidance", GN&C Equation Document 24) and the
 * algorithm RO ascents fly. This is the REAL fix for the RSS gate: a low-TWR upper stage (M-Vac TWR
 * ~0.82 from a 65 km MECO) cannot be flown to orbit by any fixed pitch heuristic; it needs a law that
 * continuously solves the thrust direction and time-to-go from the terminal constraints. See
 * docs/ASCENT_GUIDANCE_UPFG.md.
 *
 * ---- PROVENANCE ----
 * The nine blocks and every equation are the public Brand-Brown-Higgins UPFG, cross-checked against the
 * reference MATLAB implementation (Noiredd/PEGAS-MATLAB, unifiedPoweredFlightGuidance.m). Written FRESH
 * in C# - no kOS. The conic-state-extrapolation block uses our own Kepler propagator (pure/Kepler.cs);
 * vectors are the MechJebLib V3 we ported. Single active stage (the S2 to orbit); multi-stage can extend
 * Block 3/4 later.
 *
 * ---- THE FRAME AND THE SIGN TRAP ----
 * Earth-centred INERTIAL. The target is given as CONSTRAINTS, not a point: a plane normal `Iy`, a cutoff
 * radius, a cutoff speed, and a flight-path angle. ⛔ `Iy` is oriented OPPOSITE the orbital angular
 * momentum (r x v) - Init sets it that way for a prograde orbit; pass your own for a chosen plane.
 */
using System;
using MechJebLib.Primitives;

namespace DragonScreen
{
    /// <summary>Target insertion CONSTRAINTS for UPFG (not a single point).</summary>
    public struct UpfgTarget
    {
        /// <summary>Orbital plane normal, unit. ⛔ OPPOSITE the r x v angular momentum for prograde.</summary>
        public V3 Iy;
        public double RadiusM;      // cutoff radius (surface + altitude)
        public double SpeedMps;     // cutoff speed
        public double GammaRad;     // cutoff flight-path angle (0 = horizontal, i.e. circular)
    }

    /// <summary>Single-stage vehicle performance for UPFG.</summary>
    public struct UpfgVehicle
    {
        public double ExhaustVel;   // ve = Isp * g0, m/s
        public double ThrustN;      // current thrust, N
        public double MassKg;       // current mass, kg
    }

    /// <summary>UPFG state carried between calls (the predictor-corrector memory).</summary>
    public struct UpfgState
    {
        public V3 Vgo, Rbias, Rd, Rgrav, Vprev;
        public double TgoS;
        public bool Initialised;
    }

    /// <summary>What UPFG commands this tick.</summary>
    public struct UpfgGuidance
    {
        public V3 IF;               // thrust direction, unit, INERTIAL
        public double TgoS;         // seconds to cutoff (MECO/SECO when <= 0)
        public bool Valid;
    }

    public static class Upfg
    {
        /// <summary>
        /// Seed the predictor-corrector from the current state. Uses a rough constant-gravity Rgrav and a
        /// horizontal target-velocity guess for Vgo; two or three Step() calls then converge it.
        /// </summary>
        public static UpfgState Init(V3 r, V3 v, double mu, UpfgTarget t, UpfgVehicle veh)
        {
            UpfgState s = new UpfgState();
            V3 ix = V3.Normalize(r);
            V3 iz = V3.Normalize(V3.Cross(ix, t.Iy));
            V3 vdGuess = t.SpeedMps * (Math.Sin(t.GammaRad) * ix + Math.Cos(t.GammaRad) * iz);
            s.Vgo = vdGuess - v;
            double tu = veh.ExhaustVel * veh.MassKg / (veh.ThrustN > 0.0 ? veh.ThrustN : 1.0); // ve/aT
            double dv = s.Vgo.magnitude;
            s.TgoS = tu * (1.0 - Math.Exp(-dv / veh.ExhaustVel));
            s.Rd = t.RadiusM * ix;
            s.Rgrav = (-0.5 * mu / (r.sqrMagnitude * r.magnitude) * s.TgoS * s.TgoS) * r; // -0.5 g Tgo^2, radial
            s.Rbias = V3.zero;
            s.Vprev = v;
            s.Initialised = true;
            return s;
        }

        /// <summary>
        /// One predictor-corrector iteration = one guidance call. Feed the live state each physics tick;
        /// for a convergence check, call repeatedly with the SAME r,v (Vprev handles the sensed-dv term).
        /// Returns the thrust direction and time-to-go; updates `s` in place.
        /// </summary>
        public static UpfgGuidance Step(V3 r, V3 v, double mu, UpfgTarget t, UpfgVehicle veh, ref UpfgState s)
        {
            UpfgGuidance g = new UpfgGuidance();
            if (!s.Initialised) s = Init(r, v, mu, t, veh);

            double ve = veh.ExhaustVel;
            double aT = (veh.MassKg > 0.0) ? veh.ThrustN / veh.MassKg : 0.0;   // current thrust accel
            if (aT <= 0.0 || ve <= 0.0) { g.Valid = false; g.TgoS = s.TgoS; g.IF = V3.Normalize(s.Vgo); return g; }
            double tu = ve / aT;                                               // "time to burn all mass"

            // ---- BLOCK 2: decrement Vgo by the velocity sensed since last call ----
            V3 vgo = s.Vgo - (v - s.Vprev);

            // ---- BLOCK 3: time-to-go for a single stage from |Vgo| via Tsiolkovsky ----
            double dv = vgo.magnitude;
            double tgo = tu * (1.0 - Math.Exp(-dv / ve));       // Tsiolkovsky; saturates below tu
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
            // ⛔ BOTH thrust vectors carry the LATERAL (i_lambdadot) turning term - canonical Brand-Brown-
            // Higgins UPFG (PEGAS unifiedPoweredFlightGuidance.m). vthrust used to omit -(L*phi + J*phidot)*ild
            // while rthrust kept -(S*phi + Q*phidot)*ild: an inconsistent predictor whose vbias absorbed the
            // dropped lateral velocity as a spurious bias, so the Vgo correction converged to a slightly wrong
            // steering each call. Both terms restored to match the reference.
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
                // fall back to constant-gravity if the conic step failed to converge
                V3 gmid = (-mu / (r.sqrMagnitude * r.magnitude)) * r;
                vc2 = vc1 + gmid * tgo; rc2 = rc1 + vc1 * tgo + 0.5 * gmid * tgo * tgo;
            }
            V3 vgrav = vc2 - vc1;
            rgrav = rc2 - rc1 - vc1 * tgo;

            // ---- BLOCK 8: predict cutoff, rebuild the desired state, update Vgo ----
            V3 rp = r + v * tgo + rgrav + rthrust;
            rp = rp - V3.Dot(rp, t.Iy) * t.Iy;                  // into the target plane
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
