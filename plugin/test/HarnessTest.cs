// [[S167]] — THE SUITE THAT TESTS THE THING THAT RUNS THE SUITES.
//
// ⛔ WHAT WAS WRONG. `TestMain.Main` called every `Run()` bare. A suite returning a failure count is
// the DESIGNED path and reports fine; a suite THROWING was not caught anywhere, so the exception
// reached the CLR, killed the process, and every suite registered below it never ran. The report a
// reader got was one stack trace and a non-zero exit — with no counts at all for the suites that were
// skipped, and nothing to distinguish "they are green" from "they were never asked".
//
// MEASURED before the fix, on this tree (2026-09-06): a `NullReferenceException` thrown at the top of
// `AudioScopeTest.Run` (suite 25 of 55) left 25 suites with counts, 1 with a banner and no count, and
// 29 that never ran. Exit 3762504530.
//
// ⭐ AND WHY IT MATTERS TO MUTATION TESTING, which is how [[S164]] found it: a mutation harness that
// reads only the exit code cannot tell a genuine kill by the suite under test from a crash in some
// unrelated suite above it. S164's mutation Z8 was "killed" by the wrong check.
//
// ⚠ THIS SUITE PROVES THE IN-PROCESS HALF ONLY — that `Guard` converts a throw into a named failure
// of that suite and returns control. The other half of S167's done-criteria ("every later suite still
// runs and reports, and the exit code is non-zero") is a claim about a WHOLE PROCESS and cannot be
// checked from inside one: a check that ran would only prove it had itself survived. That half is
// `python plugin/build.py harnesscheck`, which re-runs the built test exe with
// `DRAGONSCREEN_HARNESS_FAULT=1` and reads the report back. Both halves are needed; neither is
// sufficient.
//
// ⚠ AND IT MUST NOT CRY WOLF. `Guard` takes its log sink as a parameter precisely so this suite can
// exercise the crash path without a `!! SUITE CRASHED` banner appearing in a green build — a
// self-test that prints a scary line every run trains the reader to ignore the line.
using System;
using System.Collections.Generic;

public static class HarnessTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    public static int Run()
    {
        Console.WriteLine("HarnessTest (S167: a throwing suite is a named failure, not the end of the run)");
        checks = 0; failures = 0;

        // ---- 1. A SUITE THAT RETURNS IS UNTOUCHED --------------------------------------------
        // The guard must be invisible on the happy path, or 55 green suites become 55 suspects.
        List<string> log = new List<string>();
        Check("S167 a passing suite's 0 comes straight back",
              TestMain.Guard("Fine", delegate { return 0; }, log.Add) == 0, "");
        Check("S167 ...and logs nothing", log.Count == 0, "logged " + log.Count);
        Check("S167 a FAILING suite's own count comes straight back — a failed Check is not a crash",
              TestMain.Guard("Fails", delegate { return 7; }, log.Add) == 7, "");
        Check("S167 ...and still logs nothing", log.Count == 0, "logged " + log.Count);

        // ---- 2. ⭐ THE DEFECT ITSELF: A THROW DOES NOT ESCAPE ----------------------------------
        // If `Guard` did not catch, this call would take the process down and no line below it —
        // including every suite after HarnessTest in TestMain — would ever run. The check that the
        // next line executes at all IS the regression test; the assertions refine it.
        log.Clear();
        int rc = -99;
        rc = TestMain.Guard("Boom", delegate { throw new NullReferenceException("deliberate"); }, log.Add);
        Check("S167 the throw was caught and control returned", rc != -99, "");
        Check("S167 a throw counts as ONE failure of that suite", rc == 1, "got " + rc);
        Check("S167 the crash was logged exactly once", log.Count == 1, "logged " + log.Count);

        string m = log.Count == 1 ? log[0] : "";
        Check("S167 the report NAMES the suite that threw", m.Contains("Boom"), m);
        Check("S167 the report gives the exception TYPE",
              m.Contains("NullReferenceException"), m);
        Check("S167 the report gives the exception MESSAGE", m.Contains("deliberate"), m);
        // ⚠ DISTINGUISHABLE FROM A FAILED CHECK, on purpose. A failed `Check` prints `FAIL`; a throw
        // prints `SUITE CRASHED`. They are different diagnoses — one is the suite doing its job, the
        // other is the suite unable to — and a reader who cannot tell them apart will debug the wrong
        // thing. The register line asked for this explicitly.
        Check("S167 a crash is worded differently from a failed check",
              m.Contains("SUITE CRASHED") && !m.StartsWith("  FAIL"), m);

        // ---- 3. THE GUARD ITSELF IS THROW-PROOF -----------------------------------------------
        // A guard that dies while reporting a death reproduces the defect one frame further out. The
        // nastiest realistic shapes: a null Message, and an exception with no stack (never thrown).
        log.Clear();
        Check("S167 an exception carrying a null message does not kill the guard",
              TestMain.Guard("NullMsg", delegate { throw new NullMessageException(); }, log.Add) == 1, "");
        Check("S167 ...and it still logged something naming the suite",
              log.Count == 1 && log[0].Contains("NullMsg"), log.Count + " entries");
        log.Clear();
        Check("S167 an exception with no stack trace does not kill the guard",
              TestMain.Guard("NoStack", delegate { throw new NoStackException(); }, log.Add) == 1, "");
        Check("S167 ...and SAYS the stack is missing rather than printing a blank",
              log.Count == 1 && log[0].Contains("(no stack trace)"), log.Count == 1 ? log[0] : "");

        // ---- 4. THE NAME COMES OFF THE DELEGATE, NOT A HAND-TYPED STRING ----------------------
        // 55 call sites × a duplicated literal is 55 chances for a rename to leave a lie behind.
        // ⛔ THIS IS WHAT MAKES THE "NAMED FAILURE" IN THE DONE-CRITERIA TRUSTWORTHY: the name is the
        // suite's own type name, so it cannot name a suite other than the one that actually threw.
        Check("S167 a suite's name is read off its own method",
              TestMain.NameOf(HarnessTest.Run) == "HarnessTest",
              TestMain.NameOf(HarnessTest.Run));
        Check("S167 ...for another suite too, so it is not a constant",
              TestMain.NameOf(LayoutTest.Run) == "LayoutTest", TestMain.NameOf(LayoutTest.Run));
        Check("S167 a null delegate does not throw on the way to being named",
              TestMain.NameOf(null) == "(unnamed suite)", TestMain.NameOf(null));

        // =========================================================================================
        //  ⛔⛔ S262 — `install` SHIPPED THE WRONG BRANCH AND SAID NOTHING.
        //
        //  🟢 OWNER, 2026-09-10: *"oops I think I fucked things up by clicking through github when I
        //  do not understand fully how it works."* ⛔ HE DID NOT. He merged a pull request in GitHub
        //  Desktop, which checked the repo out to `master`; three minutes later `install` compiled
        //  and shipped MASTER's build into the game — 22 commits behind, predating S240 — and
        //  overwrote `DragonScreen.cfg` with master's. It printed `ALL SUITES PASSED` and `--- ok`,
        //  and BOTH WERE TRUE OF THE WRONG CODE.
        //  ⭐ The accident was ordinary. THE TOOL'S SILENCE WAS THE DEFECT, so the tool is what got
        //  fixed, and this is where that fix is held down.
        //
        //  ⚠ THIS IS A STRING CHECK ON SOURCE, AND HERE THAT IS THE RIGHT INSTRUMENT — the same
        //  reasoning as S261's `ScreenPainter` guard, and it must be written down so nobody later
        //  "upgrades" it into something that stops answering the question:
        //    1. `build.py` IS PYTHON. This suite is C#. It cannot execute it, cannot import it, and
        //       cannot observe its behaviour at all — there is no runtime alternative to compare
        //       against, so the choice is a source-text check or no check.
        //    2. ⛔ AND A RULE THAT LIVES ONLY IN THE BUILD TOOL IS A RULE NO TEST CAN REACH. That is
        //       exactly how the silent install survived: it was never in a file the suite could load.
        //  ⭐ `LivePy` strips comments FIRST — and it strips `#`, not `//`, because the file under
        //  test is Python. ⛔ Reusing the C# `Live()` here would have silently matched this very
        //  comment block and passed on a file with no guard in it at all.
        // =========================================================================================
        string bp = LivePy(ReadRepo("plugin", "build.py"));

        // ⭐ VACUITY FIRST. Every check below is a substring test, and a missing or empty file makes
        // all of them fail in a way that looks like a real defect — or, if inverted, pass on nothing.
        Check("S262 build.py was really read, so the checks below are not vacuous",
              bp.Length > 20000 && bp.Contains("def install(") && bp.Contains("def branch_guard("),
              "read " + bp.Length + " chars");

        // ---- 1. THE BRANCH IS DECLARED ONCE, AS A NAMED CONSTANT --------------------------------
        // ⚠ A branch name hardcoded at three call sites is a branch name that will be wrong at two
        // of them the day the rebuild lands.
        Check("S262 ⛔ the allowed branch is ONE named constant, not a literal at the call site",
              CountOf(bp, "EXPECT_BRANCH =") == 1 && CountOf(bp, "EXPECT_BRANCH") >= 2,
              "definitions " + CountOf(bp, "EXPECT_BRANCH =") + ", uses " + CountOf(bp, "EXPECT_BRANCH"));

        // ---- 2. install() ACTUALLY CALLS THE GUARD, AND THE ORPHAN REPORT ------------------------
        // ⛔ THE POINT OF READING THE BODY rather than the file: a `branch_guard` that exists and is
        // never called is exactly the shape of the S261 defect — a correct thing nothing consults.
        string inst = PyBody(bp, "install");
        Check("S262 ⛔⛔ install() calls branch_guard() — defined-but-never-called is the S261 defect",
              CountOf(inst, "branch_guard()") == 1, "occurrences " + CountOf(inst, "branch_guard()"));
        Check("S262 ⛔ ...and it reports orphans too",
              CountOf(inst, "report_orphans(") == 1, "occurrences " + CountOf(inst, "report_orphans("));

        // ---- 3. THE GUARD ASKS GIT, AND REFUSES BY NAMING BOTH BRANCHES -------------------------
        string bg = PyBody(bp, "branch_guard");
        Check("S262 the guard asks git which branch is checked out",
              bg.Contains("rev-parse") && bg.Contains("--abbrev-ref"), "");
        Check("S262 ...and compares it against the constant, not a literal",
              bg.Contains("EXPECT_BRANCH"), "");
        // ⛔ A REFUSAL THAT NAMES ONLY ONE BRANCH IS HALF A MESSAGE. The owner needs to see what he
        // is on AND what was expected, or he cannot tell which of the two to change.
        Check("S262 ⛔ the refusal names BOTH the actual branch and the expected one",
              bg.Contains("on branch") && bg.Contains("expected") && bg.Contains("REFUSED"), "");
        Check("S262 ...and says nothing was copied, so a refusal is not mistaken for a part-install",
              bg.Contains("Nothing was copied"), "");
        Check("S262 ⛔ the escape exists and must NAME the branch, so it cannot be a silent bypass",
              bg.Contains("--branch"), "");
        // ⚠ WARN, NEVER REFUSE, on a dirty tree — installing uncommitted work is routine in the
        // capsule. A guard that blocks legitimate iteration gets switched off, and then guards nothing.
        Check("S262 a dirty tree WARNS rather than refusing — it must not block capsule iteration",
              bg.Contains("status") && bg.Contains("--porcelain")
              && !PyBody(bp, "branch_guard").Contains("REFUSED - uncommitted"), "");

        // ---- 4. ⛔ THE ORPHAN REPORT REMOVES NOTHING ---------------------------------------------
        // The task that added it said so explicitly: deleting from a live game folder is the OWNER's
        // decision, not the build tool's — an orphan may be another mod's, or something he put there.
        // ⚠ SCOPED TO THIS FUNCTION'S BODY ON PURPOSE: `build.py` does delete elsewhere (the
        // previewdiff worktrees), so a file-wide "no deletes" check would be a false claim.
        string ro = PyBody(bp, "report_orphans");
        Check("S262 ⛔⛔ report_orphans REMOVES NOTHING — it reports, and the owner decides",
              !ro.Contains("os.remove") && !ro.Contains("os.unlink")
              && !ro.Contains("shutil.rmtree") && !ro.Contains("os.rmdir"), "");
        Check("S262 ...and says out loud that install never prunes",
              ro.Contains("never prunes"), "");
        // ⭐ AND THE BODY EXTRACTOR IS PROVED TO WORK, or every check above passes on an empty string.
        Check("S262 PyBody really isolated a body — not the whole file, not nothing",
              ro.Length > 200 && ro.Length < bp.Length && !ro.Contains("def install("),
              "report_orphans body " + ro.Length + " chars of " + bp.Length);

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (the process-level half is `build.py harnesscheck`)");
        return failures > 0 ? 1 : 0;
    }

    /// <summary>S262 — a repo file's text, from the test exe's own location
    /// (`plugin/build/DragonScreenTest.exe` → `../..`). Same shape as `AscentReadbackTest.Repo`.</summary>
    static string ReadRepo(params string[] parts)
    {
        string p = System.IO.Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location);
        p = System.IO.Path.Combine(p, "..", "..");
        for (int i = 0; i < parts.Length; i++) p = System.IO.Path.Combine(p, parts[i]);
        return System.IO.File.ReadAllText(System.IO.Path.GetFullPath(p));
    }

    /// <summary>S262 — PYTHON source with whole-line `#` comments removed. ⛔ NOT the C# `Live()`:
    /// this file under test is Python, and a `//` filter would leave every `#` comment standing, so
    /// a guard that existed only in a comment would pass.</summary>
    static string LivePy(string src)
    {
        string[] lines = src.Replace("\r\n", "\n").Split(new char[] { (char)10 });
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("#")) continue;
            sb.Append(lines[i]).Append((char)10);
        }
        return sb.ToString();
    }

    /// <summary>S262 — the body of a top-level `def name(`, up to the next column-0 `def`. ⚠ Asking
    /// "is this call inside THIS function" rather than "is it anywhere in the file": a `branch_guard`
    /// that is defined and never called is the exact shape of the S261 defect.</summary>
    static string PyBody(string src, string name)
    {
        int a = src.IndexOf("\ndef " + name + "(", StringComparison.Ordinal);
        if (a < 0) return "";
        a++;
        int b = src.IndexOf("\ndef ", a + 1, StringComparison.Ordinal);
        return b < 0 ? src.Substring(a) : src.Substring(a, b - a);
    }

    /// <summary>S262 — non-overlapping occurrences of `needle` in `hay`.</summary>
    static int CountOf(string hay, string needle)
    {
        int n = 0, i = hay.IndexOf(needle, StringComparison.Ordinal);
        while (i >= 0) { n++; i = hay.IndexOf(needle, i + needle.Length, StringComparison.Ordinal); }
        return n;
    }

    // An exception whose Message is genuinely null — `Exception.Message` normally synthesises text,
    // so the only way to test the guard against a null is to override it.
    class NullMessageException : Exception
    {
        public override string Message { get { return null; } }
    }

    // A thrown exception normally carries a stack. This one does not, which is the only way to reach
    // the guard's `(no stack trace)` branch from a real `catch`.
    class NoStackException : Exception
    {
        public override string StackTrace { get { return null; } }
    }
}
