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

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (the process-level half is `build.py harnesscheck`)");
        return failures > 0 ? 1 : 0;
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
