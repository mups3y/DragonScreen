/*
 * S223 — THE GUIDANCE INSTRUMENT: the ascent read-back, the fitted guidance columns, the manifest.
 *
 * ⛔ WHAT THIS EXISTS TO STOP, AND IT HAS HAPPENED THREE TIMES: **a green that cannot go red.**
 * S75's tints, QC H-01's preview width and S130's silent false green were all checks that passed on
 * every input they could be given. So the load-bearing test in this file is not "the read-back agrees"
 * — it is `SeededDivergence`, which perturbs ONE box and asserts the headline turns red, names the box,
 * and counts exactly one. A check that cannot fail has not checked anything.
 *
 * ⚠ WHAT THIS DOES *NOT* PROVE. That the values MechJeb holds in flight are the expected ones — the
 * read itself is KSP glue (`src/MechAscentReadback.cs`) and cannot be compiled headlessly at all. What
 * it proves is that the INSTRUMENT is complete (every audited box is measured), COUPLED (the
 * expectation table cannot drift from the audit it measures against), HONEST (it goes red when it
 * should and only then), and WIRED (the glue actually calls it, at both points, and writes the columns
 * it declared). The flight itself is what the instrument is for.
 *
 * ⛔ AND ONE CHECK IS ABOUT THE TASK'S OWN LICENCE. `NothingIsWritten` parses the glue and fails the
 * build on any assignment into a MechJeb field. S223 was allowed to run before the reference flight on
 * one condition — the owner's standing order, *"let native mechjeb do it AND THEN WE TUNE FROM TRUSTED
 * CAPTURED VALUES!!!"* — and that condition is only worth anything if it is enforced by something other
 * than a promise in a header.
 */
using DragonScreen;
using DragonScreen.BlackBox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public static class AscentReadbackTest
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

    // ⛔ TEXT ASSERTIONS MATCH COMMENTED-OUT CODE. S220 had two mutants survive for exactly this
    // reason — commenting a statement out left every regex still matching. Strip line comments before
    // asserting that a statement EXISTS. (Borrowed from `AutoTargetTest.Live`, deliberately: the rule
    // is the same one and duplicating six lines is cheaper than coupling two suites.)
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
        Match m = Regex.Match(src, @"(static|public|internal)[^\n]*\b" + Regex.Escape(method) + @"\s*[\(\{]");
        if (!m.Success) return "";
        int start = m.Index;
        Match next = Regex.Match(src.Substring(start + 1), @"\n        (static|public|internal|///|// =)");
        int len = next.Success ? next.Index : Math.Min(9000, src.Length - start - 1);
        return src.Substring(start, len);
    }

    /// <summary>A reading in which every checkable box holds exactly what its declared source produces.</summary>
    static AscentObserved[] Perfect()
    {
        var obs = new AscentObserved[AscentReadback.Expected.Length];
        for (int i = 0; i < obs.Length; i++)
        {
            AscentExpect e = AscentReadback.Expected[i];
            if (!e.Checkable) obs[i] = AscentObserved.Word(e.Name, "(a runtime value)");
            else if (e.IsNumber) obs[i] = AscentObserved.Num(e.Name, e.Number);
            else obs[i] = AscentObserved.Word(e.Name, e.Text);
        }
        return obs;
    }

    static int IndexOf(string name)
    {
        for (int i = 0; i < AscentReadback.Expected.Length; i++)
            if (AscentReadback.Expected[i].Name == name) return i;
        return -1;
    }

    public static int Run()
    {
        Console.WriteLine("AscentReadbackTest (S223: the audit READS BACK, the guidance columns are fitted, the manifest says what we flew)");
        checks = 0; failures = 0;

        TableIsCompleteAndCoupled();
        SeededDivergence();
        UnreadIsNotDisagreement();
        UncheckableRowsNeverDisagree();
        Tolerance();
        DeltaIsTheExperiment();
        ManifestLines();
        GuidanceColumnsAreFitted();
        CoverageIsCleanBothWays();
        ManifestCarriesMechJeb();
        TheGlueIsWired();
        NothingIsWritten();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    // ---------------------------------------------------------------- 1. the table
    static void TableIsCompleteAndCoupled()
    {
        // ⭐ THE COMPLETENESS CHECK IS THE POINT OF THE PAIR. A box the audit decides about and the
        // instrument cannot measure is a box back in the state S219 ended: a claim with nothing behind it.
        Check("S223 every audited box has an expectation",
              AscentReadback.FirstUnmeasured() == null,
              "unmeasured: " + AscentReadback.FirstUnmeasured());
        Check("S223 no expectation is an orphan (a box nobody decided about)",
              AscentReadback.FirstOrphan() == null, "orphan: " + AscentReadback.FirstOrphan());
        Check("S223 the two tables are the same length",
              AscentReadback.Expected.Length == AscentProfile.Audit.Length,
              AscentReadback.Expected.Length + " vs " + AscentProfile.Audit.Length);

        // ⭐ THE COUPLING CHECK. Without it the audit could be changed underneath the instrument — a row
        // moved from RoDefault to Write — and the instrument would keep measuring against the old
        // decision and keep reporting green.
        Check("S223 every expectation's SOURCE agrees with the audit's DISPOSITION for the same box",
              AscentReadback.FirstSourceMismatch() == null,
              "mismatch: " + AscentReadback.FirstSourceMismatch());

        // ...and that check has to be able to fail, or it is decoration.
        Check("S223 ...and that agreement check REJECTS a real mismatch",
              !AscentReadback.Agrees(AscentDisposition.RoDefault, ExpectSource.OurWrite)
              && !AscentReadback.Agrees(AscentDisposition.Write, ExpectSource.RoDefault)
              && !AscentReadback.Agrees(AscentDisposition.RuntimeMission, ExpectSource.FieldDefault), "");

        var seen = new Dictionary<string, bool>();
        string dup = null;
        for (int i = 0; i < AscentReadback.Expected.Length; i++)
        {
            string n = AscentReadback.Expected[i].Name;
            if (seen.ContainsKey(n)) { dup = n; break; }
            seen[n] = true;
        }
        Check("S223 no box is expected twice (two expectations, one silently winning)", dup == null, "dup: " + dup);

        // A citation is what makes an expectation checkable by a reader without running anything. A row
        // with a number and no provenance is the same "trust me" the audit was criticised for.
        string noCite = null;
        for (int i = 0; i < AscentReadback.Expected.Length; i++)
            if (string.IsNullOrEmpty(AscentReadback.Expected[i].Cite) || AscentReadback.Expected[i].Cite.Length < 20)
            { noCite = AscentReadback.Expected[i].Name; break; }
        Check("S223 every expectation cites where its value comes from", noCite == null, "no cite: " + noCite);

        // 🟢🟢 S250 — ~~PitchRate is expected at RO's 5.0~~ SUPERSEDED IN PLACE (C1.16).
        // ⭐ THE ROW THE WHOLE FILE WAS BUILT AROUND HAS CHANGED SIDES, and that is the right
        // outcome rather than a loss: the shipped tune carried 0.75 against an audit that claimed 5.0,
        // and this instrument existed to make that gap REPORTABLE. The owner has now ruled 0.75 on
        // real-mission telemetry, so the expectation IS 0.75 and the instrument now reports the
        // opposite failure — our write not landing.
        // ⛔ AND THAT IS THE FAILURE MODE WITH NO OTHER SYMPTOM AT ALL, which is exactly what
        // `ExpectSource.OurWrite` is documented to catch.
        int pr = IndexOf("PitchRate");
        Check("S250 PitchRate is expected at the OWNER's 0.75, and declared as OUR write",
              pr >= 0 && AscentReadback.Expected[pr].IsNumber
              && AscentReadback.Expected[pr].Number == 0.75
              && AscentReadback.Expected[pr].Source == ExpectSource.OurWrite, "");
        Check("S250 ...and the audit agrees it is written, not awaiting the owner",
              AscentProfile.Row("PitchRate").How == AscentDisposition.Write, "");
        Check("S250 ...as is Core.Thrust.LimitDynamicPressure (Q2, closed by the owner)",
              AscentProfile.Row("Core.Thrust.LimitDynamicPressure").How == AscentDisposition.Write, "");
        // ⛔ AND THE MEASURED MAGNITUDE, because a limiter with no threshold is decoration: RO's
        // 50000 sits above our own last flight's 48,971 Pa peak, so it could never have fired.
        int mq = IndexOf("Core.Thrust.MaxDynamicPressure");
        Check("S250 the max-Q threshold is expected at the measured 24000 Pa, as our write",
              mq >= 0 && AscentReadback.Expected[mq].Number == 24000.0
              && AscentReadback.Expected[mq].Source == ExpectSource.OurWrite, "");

        // ⭐⭐ S253 — THE ACCELERATION LIMITER, AND THE PAIR IS THE POINT. The toggle alone is
        // meaningless without the magnitude and the magnitude alone actuates nothing, so both are
        // expected and both are declared `OurWrite`. ⚠ The magnitude's expectation (40) is ALSO
        // MechJeb's own field default, so this row can never fire on a stale RO seed — there is no RO
        // seed for it. It fires if a GLOBAL settings file re-seeds the field or if our write never runs.
        int la = IndexOf("Core.Thrust.LimitAcceleration");
        Check("S253 the acceleration limiter is expected ON, and declared as OUR write",
              la >= 0 && !AscentReadback.Expected[la].IsNumber
              && AscentReadback.Expected[la].Text == "true"
              && AscentReadback.Expected[la].Source == ExpectSource.OurWrite, "");
        int ma = IndexOf("Core.Thrust.MaxAcceleration");
        Check("S253 ...and its magnitude is expected at the owner's 40 m/s², as our write",
              ma >= 0 && AscentReadback.Expected[ma].IsNumber
              && AscentReadback.Expected[ma].Number == 40.0
              && AscentReadback.Expected[ma].Source == ExpectSource.OurWrite, "");
        Check("S253 ...and the audit agrees both are written, not left at RO's default",
              AscentProfile.Row("Core.Thrust.LimitAcceleration").How == AscentDisposition.Write
              && AscentProfile.Row("Core.Thrust.MaxAcceleration").How == AscentDisposition.Write, "");

        // ⚠ RO ASSIGNS DesiredAttachAlt TWICE and the LAST one is 145 km, not the 110 km the audit's
        // prose says. The expectation follows RO's actual behaviour; if this is ever "corrected" to
        // 110000 the instrument starts firing a false red on every flight.
        int da = IndexOf("DesiredAttachAlt");
        Check("S223 DesiredAttachAlt expects RO's LAST assignment (145 km), not its first",
              da >= 0 && AscentReadback.Expected[da].Number == 145000.0, "");
    }

    // ---------------------------------------------------------------- 2. ⭐⭐ the load-bearing one
    static void SeededDivergence()
    {
        AscentObserved[] obs = Perfect();
        ReadbackVerdict[] v = AscentReadback.Verdicts(obs);

        Check("S223 a core holding exactly its declared values reports ZERO disagreements",
              AscentReadback.CountDisagreements(v) == 0,
              AscentReadback.CountDisagreements(v) + " disagreed");
        Check("S223 ...and the headline says so without a stop sign",
              AscentReadback.Headline(v).Contains("0 disagree")
              && !AscentReadback.Headline(v).Contains("DISAGREE"), AscentReadback.Headline(v));

        // ⚠⚠ S250 — THE SEED HAD TO MOVE, AND IT IS THE SAME DEFECT IN THE MIRROR.
        // ~~seed the tune's 0.75 onto a box the audit says flies RO's 5.0~~ — 0.75 IS the expected
        // value now, so that seed is a PASS and the check would have proved nothing while still
        // reading green. ⛔ A test whose fixture quietly stops diverging is the S220 shape.
        // ⭐ So it seeds RO's OWN 5.0 arriving on a box we declare we write: our write did not
        // land, which is the failure mode `OurWrite` exists to catch and the one with no other symptom.
        int pr = IndexOf("PitchRate");
        obs[pr] = AscentObserved.Num("PitchRate", 5.0);
        v = AscentReadback.Verdicts(obs);

        Check("S250 ⭐ a seeded divergence turns the verdict RED",
              AscentReadback.CountDisagreements(v) == 1,
              AscentReadback.CountDisagreements(v) + " disagreed, expected exactly 1");
        Check("S250 ...and the headline NAMES it as a disagreement",
              AscentReadback.Headline(v).Contains("⛔") && AscentReadback.Headline(v).Contains("1 row(s) DISAGREE"),
              AscentReadback.Headline(v));
        Check("S250 ...and the row is PitchRate: RO's 5 where OUR 0.75 should be",
              v[pr].Disagrees && v[pr].Live == "5" && v[pr].Expected == "0.75"
              && !string.IsNullOrEmpty(v[pr].Cite), v[pr].Live + " vs " + v[pr].Expected);
        string table = AscentReadback.Render("a test", v);
        Check("S250 ...and the rendered table leads with it rather than burying it among 77 rows",
              table.Contains("⛔ PitchRate = 5")
              && table.IndexOf("⛔ PitchRate") < table.IndexOf("---- every box"), "");

        // A BOOLEAN divergence too — the numeric path and the word path are different code.
        obs = Perfect();
        int au = IndexOf("Autostage");
        // ⚠ S250: Autostage is RO's `true` now, so the divergence is the OPPOSITE value.
        obs[au] = AscentObserved.Flag("Autostage", false);     // RO's seed did not land
        v = AscentReadback.Verdicts(obs);
        Check("S223 a BOOLEAN box that lost our write is a disagreement too",
              AscentReadback.CountDisagreements(v) == 1 && v[au].Disagrees, "");

        // ...and case must not manufacture one: MechJeb renders bools through our own Flag(), but a
        // future reader that hands "True" must not be reported as a fault.
        obs[au] = AscentObserved.Word("Autostage", "TRUE");
        v = AscentReadback.Verdicts(obs);
        Check("S223 ...but a case difference is NOT a disagreement",
              AscentReadback.CountDisagreements(v) == 0, "");
    }

    // ---------------------------------------------------------------- 3. unread ≠ wrong
    static void UnreadIsNotDisagreement()
    {
        AscentObserved[] obs = Perfect();
        int lq = IndexOf("LimitQa");
        obs[lq] = AscentObserved.Unreadable("LimitQa", "NullReferenceException: no settings module");
        ReadbackVerdict[] v = AscentReadback.Verdicts(obs);

        Check("S223 a field that could not be READ is not reported as a WRONG value",
              v[lq].Unread && !v[lq].Disagrees, "");
        Check("S223 ...it is counted and surfaced separately",
              AscentReadback.CountUnread(v) == 1
              && AscentReadback.Headline(v).Contains("COULD NOT BE READ"), AscentReadback.Headline(v));
        Check("S223 ...and it carries the reason, not a zero",
              v[lq].Live.Contains("NullReferenceException"), v[lq].Live);

        // ⛔ A ROW WITH NO READING AT ALL IS UNREAD, NOT ABSENT. A verdict list quietly shorter than the
        // table is the hole this instrument exists to close.
        v = AscentReadback.Verdicts(new AscentObserved[0]);
        Check("S223 an EMPTY reading yields a full-length verdict, every row unread",
              v.Length == AscentReadback.Expected.Length
              && AscentReadback.CountUnread(v) == v.Length
              && AscentReadback.CountDisagreements(v) == 0, "");
        Check("S223 ...and a null reading does not throw",
              AscentReadback.Verdicts(null).Length == AscentReadback.Expected.Length, "");
        Check("S223 no verdicts at all is stated as such, not as a pass",
              AscentReadback.Headline(new ReadbackVerdict[0]).Contains("READ-BACK NOT TAKEN"), "");
    }

    // ---------------------------------------------------------------- 4. the three unchecked classes
    static void UncheckableRowsNeverDisagree()
    {
        AscentObserved[] obs = Perfect();
        obs[IndexOf("DesiredApoapsis")] = AscentObserved.Num("DesiredApoapsis", 999999.0);
        obs[IndexOf("OptimizeStageFlag")] = AscentObserved.Flag("OptimizeStageFlag", true);
        obs[IndexOf("LimitingAoA")] = AscentObserved.Flag("LimitingAoA", true);
        ReadbackVerdict[] v = AscentReadback.Verdicts(obs);

        Check("S223 a MISSION FACT / MENU-DERIVED / STATUS FLAG row is never a disagreement",
              AscentReadback.CountDisagreements(v) == 0, "");
        Check("S223 ...but it IS read and printed, with its live value",
              v[IndexOf("DesiredApoapsis")].Live == "999999"
              && v[IndexOf("DesiredApoapsis")].NotChecked, v[IndexOf("DesiredApoapsis")].Live);

        int notChecked = 0;
        for (int i = 0; i < v.Length; i++) if (v[i].NotChecked) notChecked++;
        Check("S223 the headline says how many rows are not checkable, so 'checked' means something",
              AscentReadback.Headline(v).Contains(notChecked + " not checkable") && notChecked > 0,
              AscentReadback.Headline(v));

        // ⛔ AND THE UNCHECKED SET IS SMALL AND NAMED. If a later task quietly moved rows into it, the
        // headline's "checked" count would fall and nothing would notice; six is what S223 shipped.
        // ⚠ S235 — THIS PIN USED TO READ, VERBATIM:
        //       Check("S223 only six rows are unchecked (4 mission facts, 1 menu-derived, 1 status flag)",
        //             notChecked == 6, notChecked + " unchecked");
        //   SUPERSEDED IN PLACE (C1.16 / G12). It is now SEVEN, and the seventh is NOT a quiet
        //   demotion of the kind this pin exists to catch — it is `AutostageLimit`, ADDED by S235
        //   JOB 4 as unchecked-BUT-COUNTED. ⭐ Its correct value is derived from the craft
        //   (`StagingFloor.For`), so there is no constant to score it against; but its Source is
        //   `OurWrite`, so `SourceIsSetting` is true and `CountMoved` DOES count it. The pin below
        //   now asserts BOTH numbers, so a genuine demotion (a setting quietly becoming telemetry)
        //   still fails even though the unchecked count went up.
        Check("S223/S235 seven rows are unchecked (4 mission facts, 1 menu-derived, 1 status flag, 1 craft-derived)",
              notChecked == 7, notChecked + " unchecked");
        int notSettings = 0;
        for (int i = 0; i < AscentReadback.Expected.Length; i++)
            if (!AscentReadback.Expected[i].IsSetting) notSettings++;
        // ⭐ SEVEN, not six — and the seventh is a J3 AUDIT FINDING I did not expect: `LimitQaEnabled`
        // is declared `B()` (CHECKABLE) with `ExpectSource.MenuDerived`, and `MirrorTheMenus` rewrites
        // it every tick from `a.AscentType == PSG`. It never MOVED, so it never triggered a false
        // re-assert like `LimitingAoA` did — but it is menu-derived and self-healing, so counting it
        // would have been the same defect waiting for a different flight.
        // ⛔ AutostageLimit is NOT among them: its Source is `OurWrite`, so it IS counted.
        Check("S235 ⛔ seven rows are non-settings, and AutostageLimit is not one of them",
              notSettings == 7 && AscentReadback.Expect("AutostageLimit").IsSetting,
              notSettings + " non-settings");
    }

    // ---------------------------------------------------------------- 5. float widening
    static void Tolerance()
    {
        AscentObserved[] obs = Perfect();
        // `AutoTurnPerc` is a `float` in the vendored source; 0.05f widens to 0.05000000074505806.
        obs[IndexOf("AutoTurnPerc")] = AscentObserved.Num("AutoTurnPerc", (double)0.05f);
        ReadbackVerdict[] v = AscentReadback.Verdicts(obs);
        Check("S223 a float widened to double is NOT a disagreement",
              AscentReadback.CountDisagreements(v) == 0, "");

        // ...and the tolerance is not so loose that a real change slips through it.
        obs[IndexOf("AutoTurnPerc")] = AscentObserved.Num("AutoTurnPerc", 0.0501);
        v = AscentReadback.Verdicts(obs);
        Check("S223 ...but a real 0.2% change IS", AscentReadback.CountDisagreements(v) == 1, "");

        Check("S223 SameNumber is relative, so a big value's last digits do not fail it",
              AscentReadback.SameNumber(145000.0000001, 145000.0)
              && !AscentReadback.SameNumber(145000.0, 110000.0), "");
        Check("S223 formatting is invariant and does not round a setting away",
              AscentReadback.Fmt(0.75) == "0.75" && AscentReadback.Fmt(1.0471975511965976) == "1.0471975512"
              && AscentReadback.Fmt(-1.0) == "-1", AscentReadback.Fmt(0.75));
    }

    // ---------------------------------------------------------------- 6. the delta IS the experiment
    static void DeltaIsTheExperiment()
    {
        AscentObserved[] a = Perfect();
        Check("S223 two identical readings report that NOTHING MOVED — a result, not an absence",
              AscentReadback.Delta(a, Perfect()).Contains("NOTHING MOVED"), "");

        // ⚠ S250: `Perfect()` now holds the owner-ruled 0.75, so seeding 0.75 would be NO
        // change at all and this check would read green while measuring nothing. The seeded value is
        // RO's own 5.0 arriving instead — a re-seed after our write, which is precisely what the
        // delta exists to catch (`MechJebCore.FixedUpdate` reloading modules mid-flight, F-102).
        AscentObserved[] b = Perfect();
        b[IndexOf("PitchRate")] = AscentObserved.Num("PitchRate", 5.0);
        string d = AscentReadback.Delta(a, b);
        Check("S250 ⭐ a box that changed between the two readings is NAMED, with both values",
              d.Contains("1 SETTING(S) CHANGED") && d.Contains("PitchRate: 0.75  ->  5"), d);

        // A field that became unreadable between the two readings is a change worth seeing too.
        b = Perfect();
        b[IndexOf("PitchRate")] = AscentObserved.Unreadable("PitchRate", "gone");
        Check("S250 a box that became UNREADABLE between readings is also reported",
              AscentReadback.Delta(a, b).Contains("PitchRate: 0.75  ->  ?"), "");
    }

    // ---------------------------------------------------------------- 7. R-04's manifest lines
    static void ManifestLines()
    {
        List<string> lines = AscentReadback.ManifestLines(Perfect());
        Check("S223 one manifest line per audited box",
              lines.Count == AscentReadback.Expected.Length, lines.Count.ToString());
        Check("S223 every line is prefixed MechJeb., so it can never be read as one of ours",
              lines.TrueForAll(delegate (string s) { return s.StartsWith("MechJeb."); }), "");
        Check("S250 the values are the LIVE ones", lines.Contains("MechJeb.PitchRate = 0.75"), "");

        List<string> none = AscentReadback.ManifestLines(null);
        Check("S223 an unread box records '?', never a plausible number (§4.6)",
              none.Count == lines.Count && none.Contains("MechJeb.PitchRate = ?"), "");
    }

    // ---------------------------------------------------------------- 8. R-03: the columns are fitted
    static void GuidanceColumnsAreFitted()
    {
        string[] fitted = { "gnc_module", "gnc_status", "cmd_pitch_deg", "cmd_heading_deg", "cmd_throttle",
                            "pvg_vgo_mps", "pvg_tgo_s", "tgt_ap_km", "tgt_pe_km", "tgt_inc_deg" };
        for (int i = 0; i < fitted.Length; i++)
        {
            int idx = BlackBoxSchema.Index(fitted[i]);
            Check("S223 '" + fitted[i] + "' exists", idx >= 0, "");
            if (idx < 0) continue;
            Col c = BlackBoxSchema.Columns[idx];
            Check("S223 '" + fitted[i] + "' is no longer Fit.Unfitted", c.Fit != Fit.Unfitted, c.Fit.ToString());
            // Conditional, not Live: the three groups come and go across a flight, and a Live column
            // with holes is BB6's `partially_written` defect firing on every normal ascent.
            Check("S223 '" + fitted[i] + "' is Conditional, with the condition stated",
                  c.Fit == Fit.Conditional && !string.IsNullOrEmpty(c.Note) && c.Note.Length > 40, "");
            Check("S223 '" + fitted[i] + "' is Scope.Capsule (MechConductor is the capsule's singleton)",
                  c.Scope == Scope.Capsule, c.Scope.ToString());
            Check("S223 '" + fitted[i] + "' names a real source, not a promise",
                  c.Source.Contains("MechConductor.Readout"), c.Source);
        }

        // ⛔ THE NOTE THAT WAS WRONG. "the conductor's command struct" described a thing that does not
        // exist and was never going to: under §14.4(a) MechJeb commands attitude, not the conductor.
        for (int i = 0; i < BlackBoxSchema.Columns.Length; i++)
            Check("S223 no column still claims 'the conductor's command struct'",
                  BlackBoxSchema.Columns[i].Source == null
                  || !BlackBoxSchema.Columns[i].Source.Contains("conductor's command struct"),
                  BlackBoxSchema.Columns[i].Name);

        // The pure readout's own contract: blank is blank, and "none" is a recorded answer.
        GuidanceReadout r = GuidanceReadout.None();
        Check("S223 the empty readout claims nothing",
              !r.Valid && !r.HaveCommand && !r.HaveGuidance && !r.HaveTarget && r.Module == null, "");
        Check("S223 an idle-but-bound core has a NAME for 'nothing is flying'",
              GuidanceReadout.NoModule == "none", "");
    }

    // ---------------------------------------------------------------- 9. ⛔ the trap in JOB 2
    static void CoverageIsCleanBothWays()
    {
        string[] fitted = { "gnc_module", "gnc_status", "cmd_pitch_deg", "cmd_heading_deg", "cmd_throttle",
                            "pvg_vgo_mps", "pvg_tgo_s", "tgt_ap_km", "tgt_pe_km", "tgt_inc_deg" };

        // (a) WRITTEN. Declaration and writer landed together, so a written guidance column is no
        //     longer `unexpected_writer` — the failure that fires on every flight if only the writer
        //     is added.
        var wrote = new BlackBoxCoverage();
        string[] row = BlackBoxSchema.NewRow();
        for (int i = 0; i < row.Length; i++)
            if (BlackBoxSchema.Columns[i].Fit != Fit.Unfitted) row[i] = "1";
        wrote.Note(row);
        List<CoverageFinding> f = wrote.Findings();
        for (int i = 0; i < f.Count; i++)
            Check("S223 a WRITTEN guidance column is not an unexpected_writer",
                  !(Array.IndexOf(fitted, f[i].Column) >= 0 && f[i].Kind == "unexpected_writer"),
                  f[i].Column);

        // (b) BLANK. The other direction: a flight that never engaged the ascent leaves them empty, and
        //     that is a fact about the FLIGHT. Reported with its condition, and NOT a defect — a defect
        //     that fires on every flight is one nobody reads.
        var blank = new BlackBoxCoverage();
        string[] r2 = BlackBoxSchema.NewRow();
        for (int i = 0; i < r2.Length; i++)
            if (BlackBoxSchema.Columns[i].Fit == Fit.Live) r2[i] = "1";
        blank.Note(r2);
        f = blank.Findings();
        int noted = 0;
        for (int i = 0; i < f.Count; i++)
            if (Array.IndexOf(fitted, f[i].Column) >= 0)
            {
                noted++;
                Check("S223 a blank '" + f[i].Column + "' is a NOTE, not a defect", !f[i].Defect, "");
                Check("S223 ...and it carries the declared condition",
                      !string.IsNullOrEmpty(f[i].Declared) && f[i].Declared.Length > 40, "");
            }
        Check("S223 all ten blank guidance columns are reported rather than silent",
              noted == fitted.Length, noted + " of " + fitted.Length);

        // (c) ...and the ghost check still has teeth on a column that IS still unfitted, so this is not
        //     a green bought by turning the machinery off.
        var surprise = new BlackBoxCoverage();
        string[] r3 = BlackBoxSchema.NewRow();
        for (int i = 0; i < r3.Length; i++) r3[i] = "1";
        surprise.Note(r3);
        bool fired = false;
        f = surprise.Findings();
        for (int i = 0; i < f.Count; i++) if (f[i].Kind == "unexpected_writer" && f[i].Defect) fired = true;
        Check("S223 the unexpected_writer check still fires on a still-Unfitted column", fired, "");
    }

    // ---------------------------------------------------------------- 10. R-04's manifest keys
    static void ManifestCarriesMechJeb()
    {
        ManifestInfo m = ManifestInfo.Fresh();
        m.Tunables.Add("Tuning.Foo = 1");
        m.MechJebAscent.Add("MechJeb.PitchRate = 5");
        m.MechJebCfgSha = "abc123";
        m.MechJebCfgFile = "mechjeb_settings_type_Crew-Dragon.cfg";
        m.MechJebCfgNote = "sha256 of the tune MechHost.ApplyTune() loads";
        string json = BlackBoxManifest.Build(m);

        Check("S223 the MechJeb ascent settings get their OWN manifest key",
              json.Contains("\"mechjeb_ascent_settings\": [\"MechJeb.PitchRate = 5\"]"), "");
        Check("S223 ...and are NOT merged into our own tunables",
              json.Contains("\"tunables\": [\"Tuning.Foo = 1\"]"), "");
        Check("S223 the cfg digest is attributable to a file and carries its reason",
              json.Contains("\"mechjeb_cfg_sha\": \"abc123\"")
              && json.Contains("\"mechjeb_cfg_file\": \"mechjeb_settings_type_Crew-Dragon.cfg\"")
              && json.Contains("\"mechjeb_cfg_note\": \"sha256 of the tune"), "");

        // A fresh manifest must not carry the old lie: null with no reason at all.
        ManifestInfo empty = ManifestInfo.Fresh();
        Check("S223 the ascent collection exists on a fresh manifest (never null into the writer)",
              empty.MechJebAscent != null && empty.MechJebAscent.Count == 0, "");
    }

    // ---------------------------------------------------------------- 11. the glue is actually wired
    static void TheGlueIsWired()
    {
        string cond = File.ReadAllText(Repo("plugin", "src", "MechConductor.cs"));
        string rec  = File.ReadAllText(Repo("plugin", "src", "BlackBoxRecorder.cs"));

        // ⛔ Live() throughout: a call that has been commented out must not pass as a call.
        Check("S223 Configure takes reading (a) — after Configure, before the audit line is built",
              Regex.IsMatch(Live(Body(cond, "Configure")), @"MechAscentReadback\.TakeAtConfigure\s*\(\s*core\s*\)"), "");
        Check("S223 ...and the audit's own headline carries the read-back's verdict",
              Regex.IsMatch(Live(Body(cond, "Configure")),
                            @"AscentProfile\.Render\(\)[\s\S]{0,200}MechAscentReadback\.HeadlineOf"), "");
        Check("S223 ...and the whole table is logged",
              Live(Body(cond, "Configure")).Contains("MechAscentReadback.LogTable"), "");
        Check("S223 TickTerminalCount takes reading (b) at T-10 s",
              Regex.IsMatch(Live(Body(cond, "TickTerminalCount")),
                            @"MechAscentReadback\.TakeAtTerminalCount\s*\(\s*core\s*\)"), "");
        Check("S223 RunAscent ticks the status-on-change log",
              Regex.IsMatch(Live(Body(cond, "RunAscent")), @"TickGuidanceStatusLog\s*\(\s*\)"), "");
        Check("S223 Reset clears the readings, so a revert cannot inherit them",
              Live(Body(cond, "Reset")).Contains("MechAscentReadback.Reset()"), "");

        // ⚠ ON CHANGE ONLY. A per-tick line at physics rate would bury the transition it exists to show.
        string sw = Live(Body(cond, "TickGuidanceStatusLog"));
        Check("S223 the status log compares against the last value and RETURNS when unchanged",
              Regex.IsMatch(sw, @"if\s*\(!moved\)\s*return;")
              && sw.Contains("lastAscentStatus") && sw.Contains("lastGuidanceStatus"), "");
        Check("S223 ...and it reads MechJeb's own words, not ours",
              sw.Contains("core.Ascent") && sw.Contains("core.Guidance.IsStable()"), "");

        // The recorder: one readout per row, and the ten columns written from it.
        Check("S223 the recorder takes ONE guidance readout per row, withheld off-focus",
              Regex.IsMatch(Live(rec), @"GuidanceReadout\s+gnc\s*=\s*focused\s*\?\s*MechConductor\.Readout"), "");
        string[] cols = { "CmdPitchDeg", "CmdHeadingDeg", "CmdThrottle", "PvgVgoMps", "PvgTgoS",
                          "GncModule", "GncStatus", "TgtApKm", "TgtPeKm", "TgtIncDeg" };
        string liveRec = Live(rec);
        for (int i = 0; i < cols.Length; i++)
            Check("S223 the recorder writes BlackBoxCols." + cols[i],
                  Regex.IsMatch(liveRec, @"Set\(c,\s*BlackBoxCols\." + cols[i] + @"\s*,"), "");
        Check("S223 each group is gated on its OWN flag, not one blanket one",
              liveRec.Contains("gnc.HaveCommand") && liveRec.Contains("gnc.HaveGuidance")
              && liveRec.Contains("gnc.HaveTarget"), "");

        // R-04: the stale null and its expired comment are gone, replaced by a real digest.
        Check("S223 the stale 'no MechJeb core is embedded yet' null is gone from the live code",
              !Regex.IsMatch(liveRec, @"MechJebCfgSha\s*=\s*null;\s*//\s*no MechJeb core"), "");
        Check("S223 ...and the reasoning survives, marked superseded in place (C1.16)",
              rec.Contains("no MechJeb core is embedded yet") && rec.Contains("C1.16"), "");
        Check("S223 the manifest hashes the tune file MechHost actually loads",
              Regex.IsMatch(liveRec, @"DragonMechJebCore\.TunePath\s*\(\s*file\s*\)")
              && liveRec.Contains("MechProfile.TuneFileName"), "");
        Check("S223 ...and records the live ascent settings beside it",
              liveRec.Contains("MechAscentReadback.ManifestLines(MechConductor.BoundCore)"), "");
        // ⚠ matched on the whole live file, not on a `Body(...)` slice: `CollectMechJeb` is a private
        // instance method with no access modifier, which `Body`'s pattern does not anchor on.
        Check("S223 a missing tune file is a stated reason, never a bare null",
              Regex.IsMatch(liveRec, @"File\.Exists\(path\)[\s\S]{0,300}MechJebCfgNote\s*=")
              && liveRec.Contains("MechJebCfgSha = null;"), "");
    }

    // ---------------------------------------------------------------- 12. ⛔ the task's own licence
    static void NothingIsWritten()
    {
        string src = Live(File.ReadAllText(Repo("plugin", "src", "MechAscentReadback.cs")));

        // ⛔ S223 WAS ALLOWED TO RUN BEFORE THE REFERENCE FLIGHT ON ONE CONDITION: it changes nothing
        // the vehicle does. Owner, 2026-09-08: *"let native mechjeb do it AND THEN WE TUNE FROM TRUSTED
        // CAPTURED VALUES!!!"*. An assignment into any MechJeb field here would break that silently —
        // the file would still be called a read-back and would still log a table.
        Check("S223 ⛔ the read-back never assigns a MechJeb settings field",
              !Regex.IsMatch(src, @"\ba\.[A-Za-z_][A-Za-z0-9_.]*\s*=[^=]"), "an `a.<field> =` was found");
        Check("S223 ⛔ ...nor a core field",
              !Regex.IsMatch(src, @"\bcore\.[A-Za-z_][A-Za-z0-9_.]*\s*=[^=]"), "a `core.<field> =` was found");
        Check("S223 ⛔ ...nor an EditableDouble's .Val",
              !Regex.IsMatch(src, @"\.Val\s*=[^=]"), "a `.Val =` was found");
        Check("S223 ⛔ ...and no MechJeb method that could actuate is called",
              !src.Contains("Users.Add") && !src.Contains("AuthorizeDrive")
              && !src.Contains("ApplyRODefaults") && !src.Contains("ActivateNextStage"), "");

        // ...and the check has teeth: it must reject the thing it is looking for.
        Check("S223 ...and that scan REJECTS a write when one is present",
              Regex.IsMatch("a.PitchRate.Val = 0.75;", @"\.Val\s*=[^=]")
              && Regex.IsMatch("core.Thrust.LimitThrottle = true;", @"\bcore\.[A-Za-z_][A-Za-z0-9_.]*\s*=[^=]"), "");

        // Every audited box is actually READ by the glue, not just expected by the pure table.
        int missing = 0; string firstMissing = null;
        for (int i = 0; i < AscentReadback.Expected.Length; i++)
        {
            string n = AscentReadback.Expected[i].Name;
            if (!src.Contains("\"" + n + "\"")) { missing++; if (firstMissing == null) firstMissing = n; }
        }
        Check("S223 the glue reads every box the expectation table judges",
              missing == 0, missing + " missing, first: " + firstMissing);

        // A throwing field must cost only itself its reading.
        Check("S223 each read is individually caught, so one bad field does not blank the other 76",
              src.Contains("catch (Exception e)") && src.Contains("AscentObserved.Unreadable"), "");
    }
}
