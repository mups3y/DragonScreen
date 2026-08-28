// Tests for the IDSS soft-capture envelope gate (pure/DockCapture.cs), IDSS IDD Rev E Table 3.3.1.1-2.
using System;
using DragonScreen;

public static class DockCaptureTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen L3 dock-capture (IDSS envelope) tests");
        CaptureLimits idss = DockCapture.Idss();

        Check("IDSS limits match the standard", idss.MaxClosingMps == 0.10 && idss.MaxLateralRateMps == 0.04
              && idss.MaxLateralOffsetM == 0.10 && idss.MaxAngleDeg == 4.0 && idss.MaxAngRateDegS == 0.20, "");

        // a clean contact at 0.08 m/s, on-axis, aligned → inside the box.
        Check("clean 8 cm/s aligned contact is within the envelope",
              DockCapture.WithinEnvelope(0.08, 0.01, 0.03, 1.5, 0.10, idss), "");

        // exactly on the limits → inside (≤).
        Check("exactly on the IDSS limits is within",
              DockCapture.WithinEnvelope(0.10, 0.04, 0.10, 4.0, 0.20, idss), "");

        // each single violation → outside.
        Check("too fast (0.15 m/s) is outside", !DockCapture.WithinEnvelope(0.15, 0.01, 0.03, 1.5, 0.10, idss), "");
        Check("too much lateral rate (0.06) is outside", !DockCapture.WithinEnvelope(0.08, 0.06, 0.03, 1.5, 0.10, idss), "");
        Check("too much lateral offset (0.20 m) is outside", !DockCapture.WithinEnvelope(0.08, 0.01, 0.20, 1.5, 0.10, idss), "");
        Check("too much angle (7°) is outside", !DockCapture.WithinEnvelope(0.08, 0.01, 0.03, 7.0, 0.10, idss), "");
        Check("too much angular rate (0.5°/s) is outside", !DockCapture.WithinEnvelope(0.08, 0.01, 0.03, 1.5, 0.5, idss), "");

        // a gently-receding capsule passes the closing bound (not too fast).
        Check("slightly receding passes the closing bound", DockCapture.WithinEnvelope(-0.01, 0.01, 0.03, 1.5, 0.10, idss), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
