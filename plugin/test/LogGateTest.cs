// Tests for LogGate (pure/LogGate.cs) - the seen-set that turns a standing condition's warning from
// one line per FRAME into one line per CONDITION.
//
// The defect it was written for: the 2026-09-03 in-orbit flight put ~450 copies of ImageStore's
// "no usable scaled-space map for Earth on shader 'Custom/HapkeScaled'" into KSP.log, one per frame,
// because the body-map lookup deliberately retries (the texture can appear later) and re-said its
// diagnosis on every retry. The RETRY is right and is untouched; only the speech is gated.
//
// ImageStore itself is glue and cannot be run here - it needs FlightGlobals and a Material. What CAN
// be run, and is the part that was wrong, is the rule: same key once, a different key again. That is
// what these check, on the exact key shape ImageStore.MapFailKey builds.
using System;
using DragonScreen;

public static class LogGateTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    // The shape ImageStore.MapFailKey builds. Kept here as a local copy on purpose: this suite is
    // testing the GATE, and duplicating four characters of key syntax is cheaper than making a glue
    // private visible to a headless test.
    static string MapKey(string body, string shader) { return "bodymap|" + body + "|" + shader; }

    public static int Run()
    {
        Console.WriteLine("DragonScreen log-gate tests (S40)");
        LogGate.Reset();

        // ---- THE FLOOD, AS IT ACTUALLY HAPPENED ----
        // 450 frames of one unchanging condition must produce exactly one line.
        string rss = MapKey("Earth", "Custom/HapkeScaled");
        int said = 0;
        for (int i = 0; i < 450; i++) if (LogGate.First(rss)) said++;
        Check("450 frames of one condition say it once", said == 1, "said " + said);
        Check("and the gate knows it has been said", LogGate.Said(rss), "");
        Check("one distinct diagnosis recorded", LogGate.Count == 1, "count " + LogGate.Count);

        // ---- A DIFFERENT SHADER ON THE SAME BODY IS A DIFFERENT DIAGNOSIS ----
        // Kopernicus can swap the scaled-space material after load; the crew needs to hear about the
        // new one, and the key carries the shader precisely so that it is heard.
        Check("same body, new shader, said again",
              LogGate.First(MapKey("Earth", "Terrain/Scaled Planet (Simple)")), "");
        Check("and not a third time",
              !LogGate.First(MapKey("Earth", "Terrain/Scaled Planet (Simple)")), "");

        // ---- A DIFFERENT BODY IS A DIFFERENT DIAGNOSIS ----
        Check("new body on the same shader, said again",
              LogGate.First(MapKey("Moon", "Custom/HapkeScaled")), "");
        Check("three distinct diagnoses now", LogGate.Count == 3, "count " + LogGate.Count);

        // ---- THE FIRST CONDITION IS STILL SILENT ----
        // Reporting a second body must not un-say the first, which is the failure mode a single
        // bool flag (the PanelButtons pattern) would have had here.
        Check("the original condition stays said", !LogGate.First(rss), "");

        // ---- Said() ASKS WITHOUT CLAIMING ----
        Check("Said on an unseen key is false", !LogGate.Said(MapKey("Mars", "x")), "");
        Check("and asking did not claim it", LogGate.First(MapKey("Mars", "x")), "");

        // ---- A KEYLESS CALL IS NEVER GATED ----
        // Collapsing every unrelated condition into one empty key would silence the second one for
        // the wrong reason, so an empty key opts out and the caller behaves exactly as before.
        Check("null key is never gated", LogGate.First(null) && LogGate.First(null), "");
        Check("empty key is never gated", LogGate.First("") && LogGate.First(""), "");
        Check("and neither was recorded", LogGate.Count == 4, "count " + LogGate.Count);

        // ---- Reset IS FOR THE TESTS, AND IT WORKS ----
        LogGate.Reset();
        Check("reset forgets everything", LogGate.Count == 0 && LogGate.First(rss), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }
}
