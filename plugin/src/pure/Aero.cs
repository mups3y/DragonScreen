// DragonScreen — Aero  (autopilot rebuild L1: derived aerodynamic quantities)
// ============================================================================================
// The derived quantities KSP does not hand you directly, as pure functions of the sensed state.
// Dynamic pressure drives the max-Q throttle bucket and the aero limits; Mach drives the drag
// model (BoosterDrag) and the transonic bucket; the isothermal density is a headless/fallback air
// model (in flight the glue passes KSP's real atmospheric density into the trajectory integrator).
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class Aero
    {
        public const double Gamma = 1.4;      // diatomic air
        public const double RSpecific = 287.05;

        // Dynamic pressure q = 1/2 rho v^2  (Pa). The max-Q bucket throttles down through the peak.
        public static double DynamicPressurePa(double densityKgM3, double speedMps)
        {
            if (densityKgM3 <= 0.0 || speedMps <= 0.0) return 0.0;
            return 0.5 * densityKgM3 * speedMps * speedMps;
        }

        // Speed of sound a = sqrt(gamma * P / rho) = sqrt(gamma * R * T). Either form; use what you have.
        public static double SoundSpeedFromPressure(double pressurePa, double densityKgM3)
        {
            if (pressurePa <= 0.0 || densityKgM3 <= 1e-12) return 0.0;
            return Math.Sqrt(Gamma * pressurePa / densityKgM3);
        }

        public static double SoundSpeedFromTemperature(double temperatureK)
        {
            if (temperatureK <= 0.0) return 0.0;
            return Math.Sqrt(Gamma * RSpecific * temperatureK);
        }

        public static double Mach(double speedMps, double soundSpeedMps)
        {
            if (soundSpeedMps <= 1e-6) return 0.0;
            return speedMps / soundSpeedMps;
        }

        // Isothermal exponential atmosphere rho(h) = rho0 * exp(-h / H). A fallback/headless density; in
        // flight the trajectory integrator is handed KSP's own density at altitude instead.
        public static double IsothermalDensity(double seaLevelDensityKgM3, double altitudeM,
                                               double scaleHeightM, double atmosphereDepthM)
        {
            if (altitudeM < 0.0) altitudeM = 0.0;
            if (altitudeM >= atmosphereDepthM || scaleHeightM <= 0.0) return 0.0;
            return seaLevelDensityKgM3 * Math.Exp(-altitudeM / scaleHeightM);
        }
    }
}
