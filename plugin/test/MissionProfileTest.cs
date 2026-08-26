// Tests for the mission-as-data resolver (pure/MissionProfile.cs). The autopilot picks the mission from
// the VAB craft name, so the match must be exact where it can be and must NOT confuse Crew-1 with Crew-11.
using System;
using DragonScreen;

public static class MissionProfileTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen mission-profile resolver tests");

        // ---- exact name (the generated craft use the bare mission name) ----
        MissionProfile c2 = Missions.Resolve("Crew-2");
        Check("Crew-2 resolves", c2.Valid && c2.Name == "Crew-2", c2.Name);
        Check("Crew-2 is 51.6 deg ISS crew", c2.IncDeg == 51.6 && c2.Kind == MissionKind.IssCrew, c2.IncDeg.ToString());
        Check("Crew-2 has rendezvous", c2.HasRendezvous, "");
        Check("Crew-2 booster is B1061 flight 2", c2.BoosterTail == "B1061" && c2.BoosterFlight == 2,
              c2.BoosterTail + "." + c2.BoosterFlight);
        Check("Crew-2 capsule Endeavour", c2.Capsule == "Endeavour", c2.Capsule);

        // ---- descriptive craft name still resolves (substring fallback) ----
        Check("'Falcon 9 - Crew-2 Real Size' resolves to Crew-2",
              Missions.Resolve("Falcon 9 - Crew-2 Real Size").Name == "Crew-2", "");

        // ---- THE COLLISION: Crew-1 must not match Crew-11, and vice-versa ----
        Check("Crew-1 resolves to Crew-1 (not Crew-11)", Missions.Resolve("Crew-1").Name == "Crew-1",
              Missions.Resolve("Crew-1").Name);
        Check("Crew-11 resolves to Crew-11", Missions.Resolve("Crew-11").Name == "Crew-11",
              Missions.Resolve("Crew-11").Name);
        Check("'Falcon 9 - Crew-11 Real Size' -> Crew-11 (longest match wins)",
              Missions.Resolve("Falcon 9 - Crew-11 Real Size").Name == "Crew-11",
              Missions.Resolve("Falcon 9 - Crew-11 Real Size").Name);

        // ---- free-flyers: no rendezvous, their own orbit ----
        MissionProfile fram = Missions.Resolve("Fram2");
        Check("Fram2 is a free-flyer", fram.FreeFlyer && !fram.HasRendezvous, "");
        Check("Fram2 is polar ~90 deg", Math.Abs(fram.IncDeg - 90.01) < 0.1, fram.IncDeg.ToString());
        Check("Fram2 orbit 202x413", fram.PeriKm == 202 && fram.ApoKm == 413, fram.PeriKm + "x" + fram.ApoKm);

        MissionProfile pd = Missions.Resolve("Polaris Dawn");
        Check("Polaris Dawn free-flyer, high ellipse", pd.FreeFlyer && pd.ApoKm == 1400, pd.ApoKm.ToString());

        // ---- recovery mode carried from the profile (Ax-2/Ax-3 were RTLS) ----
        Check("Ax-2 is RTLS", Missions.Resolve("Ax-2").Recovery == RecoveryMode.RTLS, "");
        Check("Crew-2 is droneship", Missions.Resolve("Crew-2").Recovery == RecoveryMode.Droneship, "");

        // ---- no match / empty -> fallback, Valid=false (never fly a guessed mission) ----
        Check("unknown craft -> fallback (NO-GO)", !Missions.Resolve("My Cool Rocket").Valid, "");
        Check("empty name -> fallback", !Missions.Resolve("").Valid, "");
        Check("null name -> fallback", !Missions.Resolve(null).Valid, "");

        // ---- catalog integrity ----
        Check("catalog has 19 crewed missions", Missions.Catalog.Length == 19, Missions.Catalog.Length.ToString());
        bool allValid = true, allNamed = true;
        for (int i = 0; i < Missions.Catalog.Length; i++)
        {
            if (!Missions.Catalog[i].Valid) allValid = false;
            if (string.IsNullOrEmpty(Missions.Catalog[i].Name)) allNamed = false;
            // every entry must round-trip: its own name resolves back to itself
            if (Missions.Resolve(Missions.Catalog[i].Name).Name != Missions.Catalog[i].Name)
                Check("round-trip " + Missions.Catalog[i].Name, false, "did not resolve to itself");
        }
        Check("every catalog entry is Valid", allValid, "");
        Check("every catalog entry is named", allNamed, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
