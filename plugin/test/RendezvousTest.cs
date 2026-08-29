// Tests for the named-burn rendezvous FSM (pure/Rendezvous.cs): phase progression on measured range,
// CW-targeted burns to offset aim points, and the FULL-CONTROL contract — a unit AimLvlh at all times.
using System;
using DragonScreen;

public static class RendezvousTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    static RendezvousInputs In(double rx, double ry, double rz, bool nominal = true)
    {
        RendezvousInputs s = new RendezvousInputs();
        s.Valid = true; s.N = 1.128e-3; s.AllNominal = nominal; s.AttitudeReady = true;
        s.CoEllipticBelowM = 10000; s.CoEllipticBehindM = 20000; s.AiRangeM = 7500; s.CorridorRangeM = 2000;
        s.Rel = new LvlhState { Rx = rx, Ry = ry, Rz = rz, Vx = 0, Vy = 0, Vz = 0 };
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen rendezvous FSM tests");

        // ---- FULL CONTROL: AimLvlh is a unit vector in EVERY phase, incl. invalid ----
        foreach (RvPhase ph in new[] { RvPhase.Idle, RvPhase.Phasing, RvPhase.CoElliptic, RvPhase.ApproachInit,
                                       RvPhase.Midcourse, RvPhase.Arrived })
        {
            RendezvousCommand cc = Rendezvous.Guide(In(-2000, -7000, 0), ph);
            Check("AimLvlh is a UNIT vector in " + ph, Math.Abs(cc.AimLvlh.Magnitude - 1.0) < 1e-6, cc.AimLvlh.Magnitude.ToString("F6"));
        }
        RendezvousInputs bad = new RendezvousInputs { Valid = false };
        Check("invalid vessel still gets a unit aim (never floating)",
              Math.Abs(Rendezvous.Guide(bad, RvPhase.Phasing).AimLvlh.Magnitude - 1.0) < 1e-6, "");

        // ---- Campaign 1 (C2a): far-field coast attitude gate — hold prograde tight ONLY when burning; on a
        // coast, re-acquire only past the band, else release (drift, no RCS chatter). ----
        Check("burning → always hold prograde (even at 0°)", RvCoast.HoldPrograde(true, 0.0, 3.0), "");
        Check("coast within the band → release (no RCS)", !RvCoast.HoldPrograde(false, 1.0, 3.0), "");
        Check("coast drifted past the band → re-acquire", RvCoast.HoldPrograde(false, 4.0, 3.0), "");
        Check("coast exactly at the band → still released (strict >)", !RvCoast.HoldPrograde(false, 3.0, 3.0), "");

        // ---- phase progression on measured range ----
        Check("Idle → Phasing", Rendezvous.Guide(In(-10000, -50000, 0), RvPhase.Idle).Phase == RvPhase.Phasing, "");
        Check("Phasing → CoElliptic inside 30 km", Rendezvous.Guide(In(-10000, -20000, 0), RvPhase.Phasing).Phase == RvPhase.CoElliptic, "");
        Check("stays Phasing outside 30 km", Rendezvous.Guide(In(-10000, -50000, 0), RvPhase.Phasing).Phase == RvPhase.Phasing, "");
        Check("CoElliptic → ApproachInit near 7.5 km", Rendezvous.Guide(In(-2000, -7000, 0), RvPhase.CoElliptic).Phase == RvPhase.ApproachInit, "");
        Check("ApproachInit → Midcourse near the corridor", Rendezvous.Guide(In(-400, -2500, 0), RvPhase.ApproachInit).Phase == RvPhase.Midcourse, "");
        Check("Midcourse → Arrived at the corridor", Rendezvous.Guide(In(-300, -1800, 0), RvPhase.Midcourse).Phase == RvPhase.Arrived, "");

        // ---- burns are computed toward the offset aim, gated by GO ----
        RendezvousCommand cph = Rendezvous.Guide(In(-10000, -50000, 0), RvPhase.Phasing);
        Check("a phasing burn is computed", cph.BurnDvMps > 0.0, cph.BurnDvMps.ToString("F2"));
        Check("...pointing along the burn (aim = burn dir)",
              Vec3.Dot(cph.AimLvlh, cph.BurnLvlh.Normalized) > 0.99, "");
        Check("the burn commits only when nominal (GO)", cph.Burn, "");
        Check("NO-GO holds the burn (no drift, still pointed)",
              !Rendezvous.Guide(In(-10000, -50000, 0, false), RvPhase.Phasing).Burn, "");

        // ---- arrived hands off, holding a V-bar attitude ----
        RendezvousCommand arr = Rendezvous.Guide(In(-300, -1500, 0), RvPhase.Arrived);
        Check("arrived holds attitude (unit aim) and stops burning", Math.Abs(arr.AimLvlh.Magnitude - 1.0) < 1e-6 && !arr.Burn, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
