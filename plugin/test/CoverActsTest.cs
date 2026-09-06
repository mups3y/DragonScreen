// Tests for pure/CoverActs.cs — [[S128]]: what the Cover's four action rows do, and what they cannot do.
//
// ⛔ THE DONE-CRITERION THIS FILE EXISTS FOR: "a headless test pins that none of them reaches
// FlightCommands". That is answered two ways here, deliberately:
//
//   1. BY CONSTRUCTION — `CoverAct` has no field that can name a command, an actuator or a seam. The
//      checks below enumerate every `CoverButton` and assert the resolved kind is one of exactly three
//      harmless things. A new kind that actuates would fail this without anyone remembering to look.
//   2. BY EXHAUSTION — every member of the enum is resolved, not a sample, so a button added later is
//      forced through the same statement.
//
// ⚠ The GLUE's own dispatch (`ScreenPainter.ApplyCoverAct`) needs Unity and is not testable here. That
// is exactly why the DECISION was put in pure code: what a press means is checkable headlessly, and the
// glue that applies it is four lines with no judgement in them.
using System;
using DragonScreen;

public static class CoverActsTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen Cover action rows (S128)");
        checks = 0; failures = 0;

        // ---- THE FOUR, EACH BY ITS OWN LABEL ----------------------------------------------------
        CoverAct review = CoverActs.Of(CoverPage.CoverButton.ActReview);
        Check("S128 REVIEW REFERENCE CONTENT selects a phase",
              review.Kind == CoverActKind.SelectPhase, "got " + review.Kind);
        Check("S128 ...and it is the phase whose body IS the reference content",
              review.Phase == CoverPage.ReferencePhase,
              "got " + review.Phase + ", ReferencePhase is " + CoverPage.ReferencePhase);

        CoverAct brief = CoverActs.Of(CoverPage.CoverButton.ActDeorbitBrief);
        Check("S128 DEORBIT BURN BRIEF navigates", brief.Kind == CoverActKind.GoPage, "got " + brief.Kind);
        Check("S128 ...to the Deorbit Burn Prep page, which exists",
              brief.Page == UiPage.DeorbitBurnPrep, "got " + brief.Page);

        CoverAct ack = CoverActs.Of(CoverPage.CoverButton.ActAcknowledge);
        Check("S128 ACKNOWLEDGE sets the crew latch",
              ack.Kind == CoverActKind.Latch && ack.Latch == CoverLatch.CrewAcknowledge,
              "got " + ack.Kind + "/" + ack.Latch);

        CoverAct spx = CoverActs.Of(CoverPage.CoverButton.ActOnSpaceX);
        Check("S128 'On SpaceX, on, begin procedure 4.700' sets the GROUND latch",
              spx.Kind == CoverActKind.Latch && spx.Latch == CoverLatch.GroundAuthorised,
              "got " + spx.Kind + "/" + spx.Latch);
        Check("S128 the two latches are DIFFERENT — a crew ack is not a ground go",
              ack.Latch != spx.Latch, "both " + ack.Latch);

        // ---- ⛔ AND NOTHING ELSE ON THE PAGE IS AN ACTION -----------------------------------------
        // Exhaustive: every CoverButton, not a sample. A button added later lands here on its own.
        Array all = Enum.GetValues(typeof(CoverPage.CoverButton));
        int actions = 0, total = 0;
        foreach (object o in all)
        {
            CoverPage.CoverButton b = (CoverPage.CoverButton)o;
            total++;
            CoverAct a = CoverActs.Of(b);

            // (1) the kind is one of exactly three harmless things, or None
            bool harmless = a.Kind == CoverActKind.None
                         || a.Kind == CoverActKind.SelectPhase
                         || a.Kind == CoverActKind.GoPage
                         || a.Kind == CoverActKind.Latch;
            Check("S128 " + b + " resolves to a view change, a latch, or nothing", harmless,
                  "got " + a.Kind);

            if (a.Kind != CoverActKind.None) actions++;
            Check("S128 " + b + " — IsAction agrees with Of",
                  CoverActs.IsAction(b) == (a.Kind != CoverActKind.None),
                  "IsAction " + CoverActs.IsAction(b) + " vs kind " + a.Kind);

            // (2) a SelectPhase target must be a real rail slot
            if (a.Kind == CoverActKind.SelectPhase)
                Check("S128 " + b + " selects a phase that exists",
                      a.Phase >= 0 && a.Phase < CoverPage.PhaseCount,
                      "phase " + a.Phase + " of " + CoverPage.PhaseCount);
        }
        Check("S128 EXACTLY FOUR of the Cover's buttons are actions", actions == 4,
              actions + " of " + total + " are actions");
        Check("S128 ...out of a page that has many more buttons than that", total > 10,
              "only " + total + " buttons");

        // ---- ⭐ THE §14.4(a) PIN, STATED AS A PROPERTY OF THE TYPE --------------------------------
        // `CoverAct` carries a kind, a phase index, a page and a latch. There is no member that could
        // hold a command id or an actuator, so no value of this type can express a flight command —
        // which is stronger than any test that goes looking for one call site.
        System.Reflection.FieldInfo[] fields = typeof(CoverAct).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Check("S128 CoverAct carries exactly four fields", fields.Length == 4,
              "got " + fields.Length);
        bool onlyHarmless = true; string names = "";
        for (int i = 0; i < fields.Length; i++)
        {
            names += fields[i].Name + " ";
            Type t = fields[i].FieldType;
            if (t != typeof(CoverActKind) && t != typeof(int) && t != typeof(UiPage) && t != typeof(CoverLatch))
                onlyHarmless = false;
        }
        Check("S128 ...and none of them can name a command or an actuator", onlyHarmless, names);

        // ---- the latch enum says what it means, and defaults to nothing ---------------------------
        Check("S128 CoverAct.None is genuinely inert",
              CoverAct.None.Kind == CoverActKind.None && CoverAct.None.Latch == CoverLatch.None,
              "got " + CoverAct.None.Kind + "/" + CoverAct.None.Latch);
        Check("S128 a default CoverAct is inert too — a zeroed struct must not mean 'act'",
              default(CoverAct).Kind == CoverActKind.None
              && default(CoverAct).Latch == CoverLatch.None,
              "got " + default(CoverAct).Kind + "/" + default(CoverAct).Latch);

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (" + actions + " action rows, none of which can command the vehicle)");
        return failures;
    }
}
