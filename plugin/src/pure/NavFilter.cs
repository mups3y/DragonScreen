// DragonScreen — NavFilter  (autopilot rebuild B6: the strict-fidelity navigation filter, L1.5)
// ============================================================================================
// The real Crew Dragon does NOT fly on a perfect state — it FUSES sensors (relative GPS + IMU + star
// trackers + LIDAR/vision) through a Dragonfly-class filter into a STATE ESTIMATE and flies the guidance on
// that estimate. KSP hands us the exact truth; strict implementation fidelity (docs/CREW_DRAGON_GNC_RESEARCH
// §5, [[crew2-full-fidelity-no-deviation]]) means we SIMULATE the sensors from that truth (bias + noise) and
// run the filter, so the guidance sees realistic nav error — matching the real pipeline AND proving the
// approach is robust to sensor error.
//
// This is the TRANSLATIONAL core: three DECOUPLED per-axis Kalman filters, each a 3-state [position, velocity,
// accel-bias]. Propagate with the IMU accelerometer (whose bias the filter estimates and removes); correct
// with the relative-GPS position. Decoupling per axis is exact for the linear translational dynamics (pos'=vel,
// vel'=a) in an inertial/LVLH frame, and keeps the covariance math a hand-verified 3×3 (no matrix library, no
// allocation). Attitude (star-tracker + gyro quaternion EKF) and LIDAR/vision terminal nav are follow-ups.
// Pure + headless-tested (assert the estimate tracks truth within the covariance bound and the bias converges).
// ============================================================================================
using System;

namespace DragonScreen
{
    // One axis's estimate + its symmetric 3×3 covariance (upper triangle). State order: [pos, vel, bias].
    public struct AxisNav
    {
        public double Pos, Vel, Bias;
        public double P00, P01, P02, P11, P12, P22;
        public bool Init;
    }

    public static class NavFilter
    {
        // Sensor / process noise (1σ), from typical spec (docs/CREW_DRAGON_GNC_RESEARCH §1). Nav-tunable.
        [Tunable] public static double ImuAccelNoiseMps2 = 0.01;   // accelerometer white noise
        [Tunable] public static double BiasWalkMps2 = 1.0e-4;      // accel-bias random walk (per tick)
        [Tunable] public static double RgpsNoiseM = 5.0;           // relative-GPS position 1σ (m)
        // ⭐ TERMINAL SENSOR HANDOFF (the follow-up this header flags): the real Dragon does NOT dock on rel-GPS
        // — inside ~1 km it switches to DragonEye LIDAR + thermal/optical relative nav (cm-class), which is how
        // the sub-metre soft-capture is possible at all. Model the handoff as a range-scheduled measurement 1σ:
        // rel-GPS far, LIDAR near, linearly blended across the band. Strict fidelity ([[crew2-full-fidelity]]).
        [Tunable] public static double LidarNoiseM = 0.02;         // terminal LIDAR/optical rel-nav 1σ (m)
        [Tunable] public static double LidarHandoffFarM = 1000.0;  // pure rel-GPS beyond this range
        [Tunable] public static double LidarHandoffNearM = 200.0;  // full LIDAR within this range
        // initial covariance (how little we trust the seed): position/velocity/bias variances.
        [Tunable] public static double InitPosVar = 100.0, InitVelVar = 25.0, InitBiasVar = 1.0;

        // The scheduled terminal-sensor 1σ at a given true relative range: rel-GPS far → LIDAR near, blended
        // linearly across [LidarHandoffNearM, LidarHandoffFarM]. Guards a degenerate (near ≥ far) band → LIDAR.
        public static double TerminalSensorNoiseM(double rangeM)
        {
            if (rangeM >= LidarHandoffFarM) return RgpsNoiseM;
            if (rangeM <= LidarHandoffNearM) return LidarNoiseM;
            double span = LidarHandoffFarM - LidarHandoffNearM;
            if (span <= 0.0) return LidarNoiseM;
            double t = (rangeM - LidarHandoffNearM) / span;       // 0 at near, 1 at far
            return LidarNoiseM + t * (RgpsNoiseM - LidarNoiseM);
        }

        public static AxisNav Init(double pos, double vel)
        {
            AxisNav s = new AxisNav();
            s.Pos = pos; s.Vel = vel; s.Bias = 0.0;
            s.P00 = InitPosVar; s.P11 = InitVelVar; s.P22 = InitBiasVar;
            s.Init = true;
            return s;
        }

        // Predict with the IMU accelerometer reading (true accel ≈ imuAccel − estimated bias). Advances the
        // state and inflates the covariance: P⁻ = F·P·Fᵀ + Q,  F = [[1,dt,−½dt²],[0,1,−dt],[0,0,1]].
        public static void Predict(ref AxisNav s, double imuAccelMps2, double dt)
        {
            if (!s.Init || dt <= 0.0) return;
            double a = imuAccelMps2 - s.Bias;          // bias-corrected acceleration
            s.Pos += s.Vel * dt + 0.5 * a * dt * dt;
            s.Vel += a * dt;
            // bias unchanged (random walk handled by Q)

            double A = dt, B = -0.5 * dt * dt, C = -dt;    // F's non-trivial entries
            double p00 = s.P00, p01 = s.P01, p02 = s.P02, p11 = s.P11, p12 = s.P12, p22 = s.P22;
            // FP = F·P (only the rows we need)
            double fp00 = p00 + A * p01 + B * p02;
            double fp01 = p01 + A * p11 + B * p12;
            double fp02 = p02 + A * p12 + B * p22;
            double fp11 = p11 + C * p12;
            double fp12 = p12 + C * p22;
            double fp22 = p22;
            // P⁻ = FP·Fᵀ + Q  (Q diagonal from the IMU accel noise + bias walk)
            double qPos = (0.5 * ImuAccelNoiseMps2 * dt * dt); qPos *= qPos;
            double qVel = (ImuAccelNoiseMps2 * dt); qVel *= qVel;
            double qBias = (BiasWalkMps2 * dt); qBias *= qBias;
            s.P00 = fp00 + A * fp01 + B * fp02 + qPos;
            s.P01 = fp01 + C * fp02;
            s.P02 = fp02;
            s.P11 = fp11 + C * fp12 + qVel;
            s.P12 = fp12;
            s.P22 = fp22 + qBias;
        }

        // Correct with a relative-GPS position measurement (uses the rel-GPS 1σ). H = [1,0,0].
        public static void UpdatePosition(ref AxisNav s, double zPosM) { UpdatePosition(ref s, zPosM, RgpsNoiseM); }

        // Correct with an EXPLICIT measurement 1σ — the terminal-sensor handoff feeds TerminalSensorNoiseM(range)
        // here so the filter's assumed noise matches the sensor actually in use (rel-GPS far, LIDAR near). H=[1,0,0].
        public static void UpdatePosition(ref AxisNav s, double zPosM, double sensorNoiseM)
        {
            if (!s.Init) { s = Init(zPosM, 0.0); return; }
            double S = s.P00 + sensorNoiseM * sensorNoiseM;  // innovation variance (measurement-noise scheduled)
            if (S <= 0.0) return;
            double k0 = s.P00 / S, k1 = s.P01 / S, k2 = s.P02 / S;   // Kalman gain
            double y = zPosM - s.Pos;                                 // innovation
            s.Pos += k0 * y; s.Vel += k1 * y; s.Bias += k2 * y;
            // P = (I − K·H)·P  (H picks row/col 0)
            double p00 = s.P00, p01 = s.P01, p02 = s.P02, p11 = s.P11, p12 = s.P12, p22 = s.P22;
            s.P00 = (1.0 - k0) * p00;
            s.P01 = (1.0 - k0) * p01;
            s.P02 = (1.0 - k0) * p02;
            s.P11 = p11 - k1 * p01;
            s.P12 = p12 - k1 * p02;
            s.P22 = p22 - k2 * p02;
        }

        public static double PosStd(AxisNav s) { return s.P00 > 0.0 ? Math.Sqrt(s.P00) : 0.0; }
    }

    // Three axes = the translational relative-state estimate the rendezvous/docking guidance flies on. The glue
    // feeds Vec3 IMU acceleration (predict) and Vec3 relative-GPS position (correct), both in a consistent frame.
    public struct NavState3
    {
        public AxisNav X, Y, Z;

        public static NavState3 Init(Vec3 pos, Vec3 vel)
        {
            NavState3 n; n.X = NavFilter.Init(pos.X, vel.X); n.Y = NavFilter.Init(pos.Y, vel.Y); n.Z = NavFilter.Init(pos.Z, vel.Z);
            return n;
        }
        public void Predict(Vec3 imuAccel, double dt)
        { NavFilter.Predict(ref X, imuAccel.X, dt); NavFilter.Predict(ref Y, imuAccel.Y, dt); NavFilter.Predict(ref Z, imuAccel.Z, dt); }
        public void UpdatePosition(Vec3 zPos)
        { NavFilter.UpdatePosition(ref X, zPos.X); NavFilter.UpdatePosition(ref Y, zPos.Y); NavFilter.UpdatePosition(ref Z, zPos.Z); }
        // terminal handoff: fuse with an explicit (range-scheduled) measurement 1σ.
        public void UpdatePosition(Vec3 zPos, double sensorNoiseM)
        { NavFilter.UpdatePosition(ref X, zPos.X, sensorNoiseM); NavFilter.UpdatePosition(ref Y, zPos.Y, sensorNoiseM); NavFilter.UpdatePosition(ref Z, zPos.Z, sensorNoiseM); }

        public Vec3 EstPos { get { return new Vec3(X.Pos, Y.Pos, Z.Pos); } }
        public Vec3 EstVel { get { return new Vec3(X.Vel, Y.Vel, Z.Vel); } }
        public Vec3 EstBias { get { return new Vec3(X.Bias, Y.Bias, Z.Bias); } }
    }
}
