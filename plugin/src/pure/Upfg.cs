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

    // A vehicle STAGE for MULTI-STAGE UPFG (PEGAS "virtual stages", B5). Mode 1 = constant thrust, Mode 2 =
    // constant acceleration (g-limited). The ACTIVE (first) stage's thrust accel is recomputed from the LIVE
    // mass each call; later stages use their own start mass M0. ⛔ A coast is NOT a stage here — PEGAS guides
    // only the BURN stages; a ballistic coast is a separate mission phase, not a term in the thrust integrals.
    public struct UpfgStage
    {
        public int Mode;          // 1 = const thrust, 2 = const acceleration (g-limited)
        public double ExhaustVel; // Isp * g0 (m/s)
        public double ThrustN;    // vacuum thrust (N)
        public double M0;         // stage start mass (kg) — later stages; the active stage uses the live mass
        public double MaxT;       // max burn time of the stage (s)
        public double GLim;       // acceleration limit in g's (Mode 2 only)
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

        // SINGLE-STAGE UPFG (the MVac to orbit) — the n=1 special case of the multi-stage law.
        public static UpfgGuidance Step(Vec3 r, Vec3 v, double mu, UpfgTarget t, UpfgVehicle veh,
                                        ref UpfgState st)
        {
            if (!st.Init) st = Init(r, v, mu, t, veh);

            double ve = veh.ExhaustVel;
            double aT = veh.ThrustN / veh.MassKg;
            double tu = ve / aT;

            // BLOCK 2 — decrement Vgo by the velocity gained since the last call (the corrector).
            Vec3 vgo = st.Vgo - (v - st.LastV);

            // BLOCK 3/4 — single active stage: L = |Vgo|; thrust integrals (closed forms, tgoi1 = 0).
            double L = vgo.Magnitude;   // = |Vgo|; may legitimately exceed ve (multi-ve stage)
            if (L < 1e-6) return Coasted(v, vgo, ref st);
            double tb = tu * (1.0 - Math.Exp(-L / ve));   // = tgo
            double tgo = tb;
            double J = tu * L - ve * tb;
            double S = -J + tb * L;
            double Q = S * tu - 0.5 * ve * tb * tb;
            double P = Q * tu - 0.5 * ve * tb * tb * (tb / 3.0);
            double H = J * tgo - Q;

            return Steer(r, v, mu, t, vgo, L, J, S, Q, P, H, tgo, ref st);
        }

        // MULTI-STAGE UPFG (PEGAS virtual stages, B5). stages[0] is the ACTIVE stage (its thrust accel comes
        // from liveMassKg); later stages are planned from their own M0. Blocks 3/4 accumulate the thrust
        // integrals across the stages that are needed to spend |Vgo| (ported verbatim from
        // Noiredd/PEGAS-MATLAB unifiedPoweredFlightGuidance.m); blocks 5–8 are shared with the single-stage law.
        public static UpfgGuidance Step(Vec3 r, Vec3 v, double mu, UpfgTarget t, UpfgStage[] stages,
                                        double liveMassKg, ref UpfgState st)
        {
            if (stages == null || stages.Length == 0) { UpfgGuidance bad = new UpfgGuidance(); return bad; }
            if (!st.Init) st = Init(r, v, mu, t, ToVehicle(stages[0], liveMassKg));

            Vec3 vgo = st.Vgo - (v - st.LastV);
            double L = vgo.Magnitude;
            if (L < 1e-6) return Coasted(v, vgo, ref st);

            double Lf, J, S, Q, P, H, tgo;
            Integrals(stages, liveMassKg, L, out Lf, out J, out S, out Q, out P, out H, out tgo);

            return Steer(r, v, mu, t, vgo, Lf, J, S, Q, P, H, tgo, ref st);
        }

        static UpfgVehicle ToVehicle(UpfgStage s, double liveMassKg)
        {
            UpfgVehicle veh; veh.ExhaustVel = s.ExhaustVel; veh.ThrustN = s.ThrustN; veh.MassKg = liveMassKg;
            return veh;
        }

        // |Vgo| ≈ 0: the burn is essentially complete — hold the last thrust direction, report Tgo 0.
        static UpfgGuidance Coasted(Vec3 v, Vec3 vgo, ref UpfgState st)
        {
            UpfgGuidance g = new UpfgGuidance();
            g.IF = st.Vgo.Normalized; g.TgoS = 0.0; g.Valid = g.IF.IsFinite;
            st.Vgo = vgo; st.LastV = v;
            return g;
        }

        // BLOCK 1/3/4 — multi-stage thrust-integral accumulation, ported VERBATIM from PEGAS-MATLAB
        // (unifiedPoweredFlightGuidance.m). SM=1 constant thrust, SM=2 constant acceleration (g-limited). The
        // active stage (index 0) uses the live mass; the cutoff stage burns only the remaining |Vgo|. Each
        // stage's local integrals are shifted by tgoi1 (the burn time accumulated in the earlier stages).
        static void Integrals(UpfgStage[] stages, double liveMassKg, double vgoMag,
                              out double L, out double J, out double S, out double Q, out double P, out double H,
                              out double tgo)
        {
            int n = stages.Length;
            double[] ve = new double[n], tu = new double[n], aL = new double[n], Li = new double[n], tb = new double[n], tgoi = new double[n];
            int[] SM = new int[n];

            // BLOCK 1 — per-stage constants; the ACTIVE stage's thrust accel from the live mass, others from M0.
            for (int i = 0; i < n; i++)
            {
                SM[i] = stages[i].Mode <= 1 ? 1 : 2;
                ve[i] = stages[i].ExhaustVel;
                aL[i] = stages[i].GLim * G0;
                double aT = stages[i].ThrustN / (i == 0 ? liveMassKg : stages[i].M0);
                if (SM[i] == 2) aT = aL[i];
                tu[i] = ve[i] / aT;
            }

            // BLOCK 3 — full-burn Δv of each stage; find the CUTOFF stage k (first whose cumulative Δv ≥ |Vgo|).
            // The last stage always takes the remainder (never its full Δv), so its MaxT may exceed its own
            // propellant-time tu without tripping log(≤0).
            int k = n - 1;
            double Lacc = 0.0;
            for (int i = 0; i < n; i++)
            {
                if (i == n - 1) { Li[i] = vgoMag - Lacc; k = i; break; }
                double full = (SM[i] == 1) ? ve[i] * Math.Log(tu[i] / (tu[i] - stages[i].MaxT)) : aL[i] * stages[i].MaxT;
                if (Lacc + full >= vgoMag) { Li[i] = vgoMag - Lacc; k = i; break; }
                Li[i] = full; Lacc += full;
            }

            // per-stage burn time + cumulative tgoi over the active stages 0..k.
            for (int i = 0; i <= k; i++)
            {
                tb[i] = (SM[i] == 1) ? tu[i] * (1.0 - Math.Exp(-Li[i] / ve[i])) : Li[i] / aL[i];
                tgoi[i] = (i == 0) ? tb[i] : tgoi[i - 1] + tb[i];
            }
            tgo = tgoi[k];

            // BLOCK 4 — accumulate L, J, S, Q, P, H over the active stages with the tgoi1 shift terms.
            L = 0; J = 0; S = 0; Q = 0; P = 0; H = 0;
            for (int i = 0; i <= k; i++)
            {
                double tgoi1 = (i == 0) ? 0.0 : tgoi[i - 1];
                double Ji, Si, Qi, Pi;
                if (SM[i] == 1)
                {
                    Ji = tu[i] * Li[i] - ve[i] * tb[i];
                    Si = -Ji + tb[i] * Li[i];
                    Qi = Si * (tu[i] + tgoi1) - 0.5 * ve[i] * tb[i] * tb[i];
                    Pi = Qi * (tu[i] + tgoi1) - 0.5 * ve[i] * tb[i] * tb[i] * ((1.0 / 3.0) * tb[i] + tgoi1);
                }
                else
                {
                    Ji = 0.5 * Li[i] * tb[i];
                    Si = Ji;
                    Qi = Si * ((1.0 / 3.0) * tb[i] + tgoi1);
                    Pi = (1.0 / 6.0) * Si * (tgoi[i] * tgoi[i] + 2.0 * tgoi[i] * tgoi1 + 3.0 * tgoi1 * tgoi1);
                }
                // cross-stage shift (uses the ACCUMULATED L, J, H from the earlier stages — before adding stage i).
                Ji += Li[i] * tgoi1;
                Si += L * tb[i];
                Qi += J * tb[i];
                Pi += H * tb[i];

                L += Li[i]; J += Ji; S += Si; Q += Qi; P += Pi;
                H = J * tgoi[i] - Q;
            }
        }

        // BLOCKS 5–8 — linear-tangent steering + conic-gravity + Vgo update. Shared by both laws; takes the
        // (accumulated) thrust integrals and the decremented Vgo, returns iF/Tgo and advances the state.
        static UpfgGuidance Steer(Vec3 r, Vec3 v, double mu, UpfgTarget t, Vec3 vgo,
                                  double L, double J, double S, double Q, double P, double H, double tgo,
                                  ref UpfgState st)
        {
            UpfgGuidance g = new UpfgGuidance();

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
