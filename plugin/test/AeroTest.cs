// Tests for the derived aero quantities (pure/Aero.cs) — checked against textbook arithmetic.
using System;
using DragonScreen;

public static class AeroTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }
    static void Near(string what, double got, double want, double tol)
    { Check(what, Math.Abs(got - want) <= tol, got.ToString("F3") + " vs " + want.ToString("F3")); }

    public static int Run()
    {
        Console.WriteLine("DragonScreen aero-quantity tests");

        // q = 1/2 rho v^2 : 0.5 * 1.225 * 100^2 = 6125 Pa
        Near("dynamic pressure 0.5*rho*v^2", Aero.DynamicPressurePa(1.225, 100.0), 6125.0, 1e-6);
        Check("q is zero in vacuum", Aero.DynamicPressurePa(0.0, 1000.0) == 0.0, "");
        Check("q is zero at rest", Aero.DynamicPressurePa(1.225, 0.0) == 0.0, "");

        // speed of sound at 15 C (288.15 K) ~ 340.3 m/s, by both forms
        Near("sound speed from temperature (~340.3)", Aero.SoundSpeedFromTemperature(288.15), 340.3, 0.5);
        Near("sound speed from P/rho (sea level)", Aero.SoundSpeedFromPressure(101325.0, 1.225), 340.3, 1.0);
        Check("no sound speed in vacuum", Aero.SoundSpeedFromPressure(0.0, 1.225) == 0.0, "");

        // Mach
        Near("Mach 1 at the speed of sound", Aero.Mach(340.3, 340.3), 1.0, 1e-6);
        Near("Mach 2 at twice", Aero.Mach(680.6, 340.3), 2.0, 1e-6);
        Check("Mach guards a zero sound speed", Aero.Mach(300.0, 0.0) == 0.0, "");

        // isothermal atmosphere
        Near("density at sea level is rho0", Aero.IsothermalDensity(1.225, 0.0, 5600.0, 70000.0), 1.225, 1e-9);
        Near("density at one scale height is rho0/e",
             Aero.IsothermalDensity(1.225, 5600.0, 5600.0, 70000.0), 1.225 / Math.E, 1e-6);
        Check("no density above the atmosphere", Aero.IsothermalDensity(1.225, 80000.0, 5600.0, 70000.0) == 0.0, "");
        Check("density falls with altitude",
              Aero.IsothermalDensity(1.225, 10000.0, 5600.0, 70000.0) < Aero.IsothermalDensity(1.225, 1000.0, 5600.0, 70000.0), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
