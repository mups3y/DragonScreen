// Tests for pure/NavFilter.cs (B6) — the translational nav filter (per-axis 3-state Kalman: pos, vel, bias).
// Simulate a known constant-accel trajectory with a BIASED IMU + noisy relative-GPS; assert the estimate
// tracks truth, the accel-bias converges, the fused estimate beats raw GPS, and prediction-only drifts.
using System;
using DragonScreen;

public static class NavFilterTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string d)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + d); } }
    static void Near(string what, double got, double want, double tol)
    { checks++; if (Math.Abs(got - want) > tol) { failures++; Console.WriteLine("  FAIL  " + what + "   got " + got.ToString("F4") + " want " + want.ToString("F4")); } }

    // deterministic pseudo-Gaussian (CLT: sum of 12 uniforms − 6 ≈ N(0,1)), seeded.
    sealed class Rng
    {
        ulong s;
        public Rng(ulong seed) { s = seed == 0 ? 1UL : seed; }
        double U() { s ^= s >> 12; s ^= s << 25; s ^= s >> 27; return ((s * 0x2545F4914F6CDD1DUL) >> 11) * (1.0 / 9007199254740992.0); }
        public double Gauss(double sigma) { double x = 0; for (int i = 0; i < 12; i++) x += U(); return (x - 6.0) * sigma; }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen B6 NavFilter tests");
        double dt = 0.1, aTrue = 0.5, biasTrue = 0.2;

        // ---- perfect GPS + biased IMU → estimate tracks truth and the bias converges ----
        {
            double truePos = 0, trueVel = 0;
            AxisNav s = NavFilter.Init(0, 0);
            for (int i = 0; i < 400; i++)
            {
                truePos += trueVel * dt + 0.5 * aTrue * dt * dt; trueVel += aTrue * dt;
                NavFilter.Predict(ref s, aTrue + biasTrue, dt);      // IMU = true accel + bias, no noise
                NavFilter.UpdatePosition(ref s, truePos);            // perfect GPS
            }
            Near("perfect GPS: est pos tracks truth", s.Pos, 0.5 * aTrue * (40.0 * 40.0), 1.0);   // ½·a·t², t=40s
            Near("perfect GPS: est vel tracks truth", s.Vel, aTrue * 40.0, 0.3);
            Near("bias converges to the true bias", s.Bias, biasTrue, 0.03);
            Check("covariance shrank below the initial", NavFilter.PosStd(s) < Math.Sqrt(NavFilter.InitPosVar), "std=" + NavFilter.PosStd(s).ToString("F3"));
        }

        // ---- noisy GPS + biased IMU → the fused estimate beats raw GPS (lower RMS position error) ----
        {
            double truePos = 0, trueVel = 0;
            AxisNav s = NavFilter.Init(0, 0);
            var rng = new Rng(0xBEEF);
            double sumFilt = 0, sumRaw = 0; int n = 0;
            for (int i = 0; i < 600; i++)
            {
                truePos += trueVel * dt + 0.5 * aTrue * dt * dt; trueVel += aTrue * dt;
                double imu = aTrue + biasTrue + rng.Gauss(NavFilter.ImuAccelNoiseMps2);
                double z = truePos + rng.Gauss(NavFilter.RgpsNoiseM);
                NavFilter.Predict(ref s, imu, dt);
                NavFilter.UpdatePosition(ref s, z);
                if (i > 200)   // after convergence
                {
                    double ef = s.Pos - truePos, er = z - truePos;
                    sumFilt += ef * ef; sumRaw += er * er; n++;
                }
            }
            double rmsFilt = Math.Sqrt(sumFilt / n), rmsRaw = Math.Sqrt(sumRaw / n);
            Check("filter RMS error beats raw GPS", rmsFilt < rmsRaw,
                  "filt=" + rmsFilt.ToString("F2") + " raw=" + rmsRaw.ToString("F2"));
            Check("filter RMS position error is small (< RGPS 1σ)", rmsFilt < NavFilter.RgpsNoiseM,
                  "rmsFilt=" + rmsFilt.ToString("F2"));
        }

        // ---- prediction-only (no GPS) with a biased IMU → the estimate DRIFTS (bias uncorrected) ----
        {
            double truePos = 0, trueVel = 0;
            AxisNav s = NavFilter.Init(0, 0);
            for (int i = 0; i < 200; i++)
            {
                truePos += trueVel * dt + 0.5 * aTrue * dt * dt; trueVel += aTrue * dt;
                NavFilter.Predict(ref s, aTrue + biasTrue, dt);      // no UpdatePosition
            }
            Check("prediction-only drifts from truth (bias uncorrected)", Math.Abs(s.Pos - truePos) > 5.0,
                  "drift=" + (s.Pos - truePos).ToString("F1"));
            Check("prediction-only covariance grows", NavFilter.PosStd(s) > Math.Sqrt(NavFilter.InitPosVar), "");
        }

        // ---- NavState3: the 3-axis wrapper estimates a Vec3 state ----
        {
            NavState3 nav = NavState3.Init(new Vec3(0, 0, 0), new Vec3(0, 0, 0));
            for (int i = 0; i < 200; i++)
            {
                nav.Predict(new Vec3(0.0, 0.3, 0.0), dt);
                nav.UpdatePosition(new Vec3(0.0, 0.5 * 0.3 * ((i + 1) * dt) * ((i + 1) * dt), 0.0));
            }
            Check("NavState3 tracks the +y axis", nav.EstPos.Y > 0.0 && Math.Abs(nav.EstPos.X) < 1.0, "y=" + nav.EstPos.Y.ToString("F1"));
        }

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
