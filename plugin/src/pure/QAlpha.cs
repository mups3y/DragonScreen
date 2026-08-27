// DragonScreen — QAlpha  (autopilot rebuild B2: q·α moderation — the controllability-region AoA cap)
// ============================================================================================
// The advanced angle-of-attack limiter, from the AtmosphereAutopilot method (the mod ships DLL-only, so this
// is built from its documented principle + first principles, not a line port). The idea: don't cap AoA on a
// blind q-schedule — cap it at the largest AoA whose AERODYNAMIC pitching moment the available CONTROL moment
// can still overcome. Working in angular-acceleration space (what pure/Authority gives directly):
//     kAero    = M_α / I   — aero pitch angular-accel per radian of AoA (1/s²), grows with q
//     aCtrlMax = τ_max / I  — control angular-accel the gimbal+RCS can produce (rad/s², from Authority)
//     α_max    = factor · aCtrlMax / |kAero|            (the controllability region)
// So the cap TIGHTENS automatically as q (hence kAero) spikes through max-Q, and it accounts for the vehicle's
// real authority and static stability — not a fixed schedule. When statically UNSTABLE (FAR transonic), the
// control must actively arrest divergence, so a smaller factor holds well inside the limit.
//
// kAero comes from the ⭐ SelfCal online estimator (SelfCal.AeroPitchStiffness, regresses the measured aero
// angular-accel on AoA) once it has converged; before that, a q-seed (StiffnessSeedPerPa·q) sized to the
// current cap's calibration point stands in. Pure + headless-tested; the glue composes the physics cap with
// the MinAoa steering floor (⛔ never cap below it — flight 235215 RUD'd when a cap hit 0 and lost steering).
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct QAlphaLimit
    {
        public double AoaMaxRad;   // the controllability cap (+∞ = no aero limit here; guidance's own cap governs)
        public bool Active;        // above the dynamic-pressure gate?
        public bool Stable;        // aero restoring (stable) vs diverging (statically unstable)
    }

    public static class QAlpha
    {
        // Below this q, aero pitching moments are negligible — no controllability cap (guidance cap governs).
        [Tunable] public static double QActivePa = 2000.0;                 // ~2 kPa; judgment
        // How close to the equilibrium limit to allow. Stable aero self-restores → ride the limit; a statically
        // unstable vehicle (FAR transonic) needs margin to arrest divergence → hold well inside it.
        [Tunable] public static double StableFactor = 1.0;
        [Tunable] public static double UnstableFactor = 0.5;               // AA conservatism when unstable
        // kAero q-SEED (1/s² per rad per Pa) used only until SelfCal.AeroPitchStiffness converges. Sized so the
        // physics cap ≈ MinAoaDeg (5°=0.087 rad) at QAoaZeroPa (15 kPa) with ~0.1 rad/s² gimbal authority and
        // the unstable factor: kAero=0.5·0.1/0.087≈0.57 /s² at 15 kPa → 0.57/15000. Replaced by the live estimate.
        [Tunable] public static double StiffnessSeedPerPa = 3.8e-5;

        // The kAero q-seed: aero pitch stiffness scales with dynamic pressure (∝ q·S·L·Cm_α/I).
        public static double AeroStiffnessSeed(double qPa)
        {
            return qPa > 0.0 ? StiffnessSeedPerPa * qPa : 0.0;
        }

        // The controllability cap. kAeroPerRad = M_α/I (1/s² per rad; magnitude used, sign via `stable`);
        // aCtrlMaxRadS2 = control angular-accel available (from Authority). Below the q-gate, or with no aero,
        // there is no cap (+∞). With no control authority, no AoA is holdable (0) — the glue floors it.
        public static QAlphaLimit Limit(double kAeroPerRad, double aCtrlMaxRadS2, bool stable, double qPa)
        {
            QAlphaLimit r = new QAlphaLimit();
            r.Stable = stable;
            r.Active = qPa >= QActivePa;
            if (!r.Active) { r.AoaMaxRad = double.PositiveInfinity; return r; }

            double k = Math.Abs(kAeroPerRad);
            if (k < 1e-9) { r.AoaMaxRad = double.PositiveInfinity; return r; }   // no aero moment → no cap
            if (aCtrlMaxRadS2 <= 0.0) { r.AoaMaxRad = 0.0; return r; }           // no authority → hold zero AoA

            double factor = stable ? StableFactor : UnstableFactor;
            r.AoaMaxRad = factor * aCtrlMaxRadS2 / k;
            return r;
        }

        // Clamp a commanded AoA (rad) symmetrically into ±aoaMax. +∞ passes through unchanged.
        public static double Clamp(double cmdAoaRad, double aoaMaxRad)
        {
            if (double.IsInfinity(aoaMaxRad) || aoaMaxRad < 0.0) return cmdAoaRad;
            if (cmdAoaRad > aoaMaxRad) return aoaMaxRad;
            if (cmdAoaRad < -aoaMaxRad) return -aoaMaxRad;
            return cmdAoaRad;
        }

        // Compose the physics cap with the guidance's own ceiling and the MinAoa STEERING FLOOR: the effective
        // cap lives in [floor, ceil]. ⛔ Never below the floor — losing all steering AoA at high q RUDs the
        // stack (flight 235215); a small commanded AoA that may not fully hold beats zero plane authority.
        public static double EffectiveCapRad(double physicsCapRad, double floorRad, double ceilRad)
        {
            double c = physicsCapRad;
            if (c > ceilRad) c = ceilRad;
            if (c < floorRad) c = floorRad;
            return c;
        }
    }
}
