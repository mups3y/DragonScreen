/*
 * S220 — AUTO-TARGET THE STATION ON THE PAD.
 *
 * Owner, 2026-09-07: "we also need to auto target the iss as soon as the vehicle is on the pad".
 *
 * ---- WHY THIS IS TESTED AS TEXT ----
 * `src/MechConductor.cs` needs KSP to compile, so no headless test can CALL AutoTargetStation. But every
 * property that matters here is a claim about WHAT THE CODE DOES, and this repo already proves such claims
 * against real files - ConductorEngageTest (S219), MechHostTest, RendezvousOpsTest. Same idiom, same reason:
 * these are exactly the guards that rot silently, because nothing fails when one is deleted.
 *
 * ⚠ WHAT THIS DOES NOT PROVE: that the target takes on a live core. That is glass time.
 *
 * ⭐ THE FOUR CASES, and each one is a mutant this suite must kill:
 *   0 candidates      -> refuse and say so           (never a silent no-op)
 *   2+ candidates     -> refuse and say so           (S219 job 1: a spent upper stage bound NEITHER)
 *   target already set-> leave the crew's choice     (§1.4: the crew's selection is the authority)
 *   not on the pad    -> do nothing                  (debris and a spent stage are in the scene later)
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public static class AutoTargetTest
{
    static int checks, failures;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + (detail == "" ? "" : "   " + detail)); }
    }

    static string Repo(params string[] parts)
    {
        var bits = new List<string> {
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", ".." };
        bits.AddRange(parts);
        return Path.GetFullPath(Path.Combine(bits.ToArray()));
    }

    // ⛔ TEXT ASSERTIONS MATCH COMMENTED-OUT CODE, AND THAT IS NOT A THEORETICAL FLAW.
    // Two of this suite's own mutants SURVIVED on first run for exactly this reason: commenting out
    // `stationTargetTried = false;` and `AutoTargetStation(v);` left the text in place and every regex
    // still matched. A check that passes on code that has been switched off proves nothing. Strip line
    // comments before matching anything that asserts a statement EXISTS.
    static string Live(string src)
    {
        string[] lines = src.Replace("\r\n", "\n").Split(new char[] { (char)10 });
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].TrimStart();
            if (t.StartsWith("//")) continue;
            sb.Append(lines[i]).Append((char)10);
        }
        return sb.ToString();
    }

    static string Body(string src, string method)
    {
        Match m = Regex.Match(src, @"(static|public)[^\n]*\b" + Regex.Escape(method) + @"\s*\(");
        if (!m.Success) return "";
        int start = m.Index;
        Match next = Regex.Match(src.Substring(start + 1), @"\n        (static|public|///|// =)");
        int len = next.Success ? next.Index : Math.Min(6000, src.Length - start - 1);
        return src.Substring(start, len);
    }

    public static int Run()
    {
        Console.WriteLine("AutoTargetTest (S220: the station is targeted on the pad, and refuses rather than guesses)");
        checks = 0; failures = 0;

        string src  = File.ReadAllText(Repo("plugin", "src", "MechConductor.cs"));
        string fn   = Body(src, "AutoTargetStation");

        Check("S220 AutoTargetStation exists", fn.Length > 0, "not found in MechConductor.cs");

        // ---- it is actually called, and from Tick. A guard nothing calls is not a guard. ----
        Check("S220 it is CALLED from Tick, not merely defined",
              Regex.IsMatch(Live(Body(src, "Tick")), @"AutoTargetStation\s*\(\s*v\s*\)"), "");

        // ---- selects on the vessel TYPE, never on a name (§1.4) ----
        Check("S220 selects on VesselType.Station",
              fn.Contains("VesselType.Station"), "");
        Check("S220 does NOT match on the name \"ISS\" — §1.4 forbids inventing a mapping",
              !fn.Contains("ISS") && !fn.Contains("vesselName.Contains"), "");

        // ---- THE FOUR CASES ----
        Check("S220 CASE exactly-one: sets the target only when the count is 1",
              Regex.IsMatch(fn, @"n\s*==\s*1"), "");
        Check("S220 CASE 0-or-2+: refuses, and the refusal is LOGGED not silent",
              fn.Contains("Debug.LogWarning") && fn.Contains("REFUSED"), "");
        Check("S220 CASE already-set: never stomps a manual selection",
              fn.Contains("NormalTargetExists"), "");
        Check("S220 CASE off-pad: PRELAUNCH only",
              fn.Contains("Vessel.Situations.PRELAUNCH"), "");
        Check("S220 it excludes our own vessel from the scan",
              Regex.IsMatch(fn, @"o\s*==\s*v"), "");
        Check("S220 holds until FlightGlobals.ready — a negative read off an unready scene is a fact "
            + "about when we looked (S219)",
              fn.Contains("FlightGlobals.ready"), "");

        // ---- one attempt per scene: a refusal must not re-log every frame ----
        Check("S220 tries once per flight scene",
              fn.Contains("stationTargetTried"), "");
        Check("S220 ...and the flag is CLEARED by Reset(), or the next flight inherits it",
              Regex.IsMatch(Live(Body(src, "Reset")), @"stationTargetTried\s*=\s*false"), "");
        Check("S220 ...and it is set BEFORE the refusal path, so a refusal cannot loop",
              fn.IndexOf("stationTargetTried = true") > 0 &&
              fn.IndexOf("stationTargetTried = true") < fn.IndexOf("REFUSED"), "");

        // ---- the thing that made this necessary: G7 refuses without a target (S215 Q4) ----
        Check("S220 the dependency is real — G7 reads SystemNoGo without a target in the same SoI",
              File.ReadAllText(Repo("plugin", "src", "pure", "CrewGate.cs")).Contains("SystemNoGo"), "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }
}
