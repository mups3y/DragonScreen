// Tests for the far-field co-elliptic phasing + the crew-safety floor (pure/Phasing.cs) and the CW-validity
// guard in Rendezvous.Guide — the fix for the PHASING self-deorbit (flight 214827: CW at 13,000 km → 28 km/s
// retrograde → pe +178 → −143 km). The far field must be RAISE-or-COAST only (never a lowering/deorbit burn),
// the floor must forbid a burn below a safe periapsis, and Guide must refuse to emit a CW burn far out.
using System;
using DragonScreen;

public static class PhasingTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen phasing / safety-floor tests");

        // ---- co-elliptic target = a set height BELOW the station ----
        Check("co-elliptic target is below the station", Math.Abs(Phasing.CoEllipticTargetAltM(420000, 10000) - 410000) < 1e-6, "");

        // ---- ShouldRaise: raise while below, coast once reached ----
        Check("far below target → RAISE", Phasing.ShouldRaise(200000, 178000, 410000, 2000), "");
        Check("apoapsis low → RAISE", Phasing.ShouldRaise(405000, 411000, 410000, 2000), "");
        Check("periapsis low → RAISE", Phasing.ShouldRaise(411000, 405000, 410000, 2000), "");
        Check("both apses at target → COAST (no raise)", !Phasing.ShouldRaise(411000, 409000, 410000, 2000), "");
        Check("above target → COAST (never lowers)", !Phasing.ShouldRaise(430000, 425000, 410000, 2000), "");

        // ---- the hard periapsis floor ----
        Check("pe well above floor is safe", Phasing.PeSafe(178000, 150000), "");
        Check("pe below floor is UNSAFE (burns held)", !Phasing.PeSafe(149000, 150000), "");
        Check("pe exactly at floor is UNSAFE (strict)", !Phasing.PeSafe(150000, 150000), "");
        Check("the flight's decayed pe (−143 km) is UNSAFE", !Phasing.PeSafe(-143000, 150000), "");

        // ---- far/near split ----
        Check("13,322 km is FAR FIELD", Phasing.FarField(13322000, 50000), "");
        Check("40 km is NEAR FIELD (CW)", !Phasing.FarField(40000, 50000), "");
        Check("60 km is FAR FIELD", Phasing.FarField(60000, 50000), "");

        // ---- CW-validity guard in Guide: NO garbage burn far out, but still a unit aim (full control) ----
        RendezvousInputs far = new RendezvousInputs();
        far.Valid = true; far.N = 1.128e-3; far.AllNominal = true; far.AttitudeReady = true;
        far.CoEllipticBelowM = 10000; far.CoEllipticBehindM = 20000; far.AiRangeM = 7500; far.CorridorRangeM = 2000;
        far.Rel = new LvlhState { Rx = 0, Ry = -13322000, Rz = 0, Vx = 0, Vy = 0, Vz = 0 };
        RendezvousCommand fc = Rendezvous.Guide(far, RvPhase.Phasing);
        Check("Guide emits NO burn beyond CW's valid range", !fc.Burn && fc.BurnDvMps < 1e-9,
              "Burn=" + fc.Burn + " dv=" + fc.BurnDvMps.ToString("F1"));
        Check("...but still holds a unit aim (never floating)", Math.Abs(fc.AimLvlh.Magnitude - 1.0) < 1e-6, "");

        // ---- near field still computes a CW burn (regression guard for the existing terminal legs) ----
        RendezvousInputs near = far; near.Rel = new LvlhState { Rx = -2000, Ry = -7000, Rz = 0 };
        RendezvousCommand nc = Rendezvous.Guide(near, RvPhase.CoElliptic);
        Check("near field still computes a CW burn", nc.BurnDvMps > 0.0, nc.BurnDvMps.ToString("F3"));

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
