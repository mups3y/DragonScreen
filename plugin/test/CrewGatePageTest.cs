// CrewGatePageTest — register S213. "4.100 Mission Sequence": the crew-gate procedure that gives the
// conductor its first entry point on the glass.
//
// ---- WHAT THIS SUITE IS FOR ----
// The defect S213 records is that a control the crew were TOLD to press did not exist, and the reason
// nothing caught it is that no test asked whether the conductor was reachable. So these checks are
// mostly about REACHABILITY and about the paint agreeing with the press:
//   ⛔ every control the page draws has a hit rect, and every rect is the one the draw used;
//   ⛔ an AUTO row cannot be ticked by a finger — only the system satisfies those;
//   ⛔ the page re-titles itself per gate, which is the owner's own choice for having ONE page;
//   ⛔ the SECTION number is the gate's own G-number and is not invented here;
//   ⛔ INITIATE and HALT are mutually exclusive — you cannot start what is running.
//
// ⛔ WHAT IT CANNOT DO. It does not press anything on a real screen. `ScreenPainter` routes these
// actions to `CrewProcedureOps`, which needs a Vessel; that half is the glass.
using System;
using DragonScreen;

public static class CrewGatePageTest
{
    static int checks = 0, failures = 0;
    static void Check(bool ok, string what)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL: " + what); } }

    const int W = 1600, H = 986;   // a 3427x2112-shaped panel, so design px map cleanly

    public static int Run()
    {
        Console.WriteLine("CrewGatePageTest (S213: \"4.100 Mission Sequence\" - the conductor's way in)");
        checks = 0; failures = 0;

        TheInvention();
        SectionNumbersAreTheGatesOwn();
        Reachability();
        AutoRowsCannotBeFaked();
        InitiateAndHalt();
        Retitling();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    static PageState Idle()
    {
        PageState s = new PageState();
        s.AutoEngaged = false; s.GateActive = false;
        return s;
    }

    static PageState AtGate(params bool[] crew)
    {
        PageState s = new PageState();
        s.AutoEngaged = true; s.GateActive = true;
        s.GateTitle = "GO/NO-GO FOR LAUNCH";
        s.GateStage = GatePhase.Holding;
        GateItemView[] v = new GateItemView[crew.Length];
        for (int i = 0; i < crew.Length; i++)
        {
            v[i].Label = "step " + i;
            v[i].CrewActionable = crew[i];
            v[i].Checked = false;
        }
        s.GateItems = v;
        return s;
    }

    // ---- 1. THE INVENTION, PINNED AS LITERALS SO IT CANNOT GROW QUIETLY -------------------------
    // ⛔ The whole tier-3 invention is ONE number and ONE title. If a later chat invents a second, this
    // is where it shows up.
    static void TheInvention()
    {
        Check(CrewGatePage.ProcedureId == "4.100",
              "the procedure number is 4.100 - the ONE invented number (4.0xx is ECLSS, 4.7xx deorbit)");
        Check(CrewGatePage.ProcedureName == "Mission Sequence", "...and 'Mission Sequence' the ONE title");
        Check(CrewGatePage.ProcedureSystem == "GNC", "the system slot, in 4.011's own 'ECLSS' position");
        Check(CrewGatePage.ProcedureId != "4.011",
              "⛔ it does NOT claim to be 4.011 - that is the REAL Suit Leak Check, and gate G2 routes there");
        Check(FigmaUI.Name(UiPage.CrewGate) == "MISSION SEQUENCE", "the page names itself in the Menu grid");
        Check(!FigmaUI.IsPlaceholder(UiPage.CrewGate), "and it is a real page, not a placeholder");
    }

    // ---- 2. THE SECTION NUMBER IS THE GATE'S OWN --------------------------------------------------
    // ⛔ Read off the GateId name, never a second table, so the two cannot drift.
    static void SectionNumbersAreTheGatesOwn()
    {
        Check(CrewGates.NumberOf(GateId.IngressCommG1) == 1, "G1 is section 1");
        Check(CrewGates.NumberOf(GateId.SuitLeakG2) == 2, "G2 is section 2 - and G2 IS the real 4.011");
        Check(CrewGates.NumberOf(GateId.LaunchGoG7) == 7, "G7 is section 7");
        Check(CrewGates.NumberOf(GateId.ApproachInitGoG9) == 9,
              "⭐ G9 is section 9, NOT 8 - the real poll sequence skips G8, and reading the name keeps "
              + "that true where an index would have silently renumbered it");
        Check(CrewGates.NumberOf(GateId.WP2DockGoG12) == 12, "G12 is section 12");
        Check(CrewGates.NumberOf(GateId.DeorbitGoG15) == 15, "G15 is section 15");
        Check(CrewGates.NumberOf(GateId.None) == 0, "no gate is section 0");

        // Swept: every gate in the catalog resolves to a number, and they are all distinct.
        GateId[] all = (GateId[])Enum.GetValues(typeof(GateId));
        int named = 0; bool dup = false;
        var seen = new System.Collections.Generic.List<int>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == GateId.None) continue;
            int n = CrewGates.NumberOf(all[i]);
            if (n > 0) named++;
            if (seen.Contains(n)) dup = true;
            seen.Add(n);
        }
        Check(named == all.Length - 1, "every gate but None carries a number (" + named + ")");
        Check(!dup, "and no two gates share one");
    }

    // ---- 3. ⛔ REACHABILITY - THE CHECK WHOSE ABSENCE IS THE WHOLE OF S213 -----------------------
    static void Reachability()
    {
        PageState idle = Idle();

        // INITIATE is the only thing pressable when nothing is running, and it MUST be pressable -
        // it is the conductor's only entry point in the whole build.
        bool foundInitiate = false, foundAuto = false;
        for (int y = 0; y < H; y += 3)
            for (int x = 0; x < W; x += 3)
            {
                GateAction a = CrewGatePage.HitTest(x, y, W, H, idle);
                if (a.Act == GateAct.Initiate) foundInitiate = true;
                if (a.Act == GateAct.AutoGates) foundAuto = true;
            }
        Check(foundInitiate,
              "⭐ INITIATE IS REACHABLE - the check whose absence let a batch ship an autopilot with no "
              + "way to engage it");
        Check(foundAuto, "⭐ and so is the auto-gates checkbox, engaged or not - it is a standing choice");

        // At a gate, every control is reachable.
        PageState g = AtGate(true, true, true);
        bool go = false, nogo = false, halt = false, step0 = false, step2 = false;
        for (int y = 0; y < H; y += 3)
            for (int x = 0; x < W; x += 3)
            {
                GateAction a = CrewGatePage.HitTest(x, y, W, H, g);
                if (a.Act == GateAct.Go) go = true;
                if (a.Act == GateAct.NoGo) nogo = true;
                if (a.Act == GateAct.Halt) halt = true;
                if (a.Act == GateAct.Step && a.Index == 0) step0 = true;
                if (a.Act == GateAct.Step && a.Index == 2) step2 = true;
            }
        Check(go && nogo, "GO and NO-GO are both reachable at a gate");
        Check(halt, "HALT is reachable while the sequence runs");
        Check(step0 && step2, "every crew step is reachable, first and last");

        // ⛔ And nothing is reachable that should not be: a press far from any control is inert.
        Check(CrewGatePage.HitTest(W / 2, 5, W, H, g).Act == GateAct.None,
              "a press on empty page chrome does nothing");
    }

    // ---- 4. ⛔ AN AUTO ROW CANNOT BE TICKED BY A FINGER -------------------------------------------
    // The AUTO rows are satisfied from vessel state. A finger that could tick one would be faking a
    // system confirmation - which is the same class of lie as a lamp lit ahead of the vehicle.
    static void AutoRowsCannotBeFaked()
    {
        PageState g = AtGate(false, true, false);   // rows 0 and 2 are AUTO, row 1 is crew
        int hitAuto = 0, hitCrew = 0;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x += 2)
            {
                GateAction a = CrewGatePage.HitTest(x, y, W, H, g);
                if (a.Act != GateAct.Step) continue;
                if (a.Index == 1) hitCrew++; else hitAuto++;
            }
        Check(hitAuto == 0,
              "⛔ NO press anywhere on the page can tick an AUTO row (" + hitAuto + " could)");
        Check(hitCrew > 0, "...while the crew row takes presses normally (" + hitCrew + " points)");
    }

    // ---- 5. INITIATE AND HALT ARE MUTUALLY EXCLUSIVE ---------------------------------------------
    static void InitiateAndHalt()
    {
        PageState idle = Idle(), run = AtGate(true);
        bool idleHalt = false, runInitiate = false;
        for (int y = 0; y < H; y += 2)
            for (int x = 0; x < W; x += 2)
            {
                if (CrewGatePage.HitTest(x, y, W, H, idle).Act == GateAct.Halt) idleHalt = true;
                if (CrewGatePage.HitTest(x, y, W, H, run).Act == GateAct.Initiate) runInitiate = true;
            }
        Check(!idleHalt, "⛔ HALT cannot be pressed when nothing is running");
        Check(!runInitiate, "⛔ INITIATE cannot be pressed when the sequence already is");
    }

    // ---- 6. ONE PAGE, RE-TITLING ITSELF (the owner's own choice) ----------------------------------
    static void Retitling()
    {
        // Draw two different gates and confirm the page renders each one's own title - which is what
        // "one page that re-titles itself" means, and the reason only one number is ever invented.
        DisplayList a = new DisplayList(4096), b = new DisplayList(4096);
        PageState g1 = AtGate(true); g1.GateTitle = "CREW INGRESS & COMM CHECK";
        PageState g7 = AtGate(true, true, true); g7.GateTitle = "GO/NO-GO FOR LAUNCH";
        CrewGatePage.Build(a, W, H, g1, 1, false);
        CrewGatePage.Build(b, W, H, g7, 7, true);

        Check(Has(a, "CREW INGRESS & COMM CHECK"), "the page prints G1's own title");
        Check(Has(b, "GO/NO-GO FOR LAUNCH"), "...and G7's, from the same page");
        Check(!Has(a, "GO/NO-GO FOR LAUNCH"), "and does not print the other gate's");
        Check(Has(a, "4.100 - Mission Sequence") && Has(b, "4.100 - Mission Sequence"),
              "both carry the ONE procedure number");
        Check(Has(a, "1. CREW INGRESS & COMM CHECK"), "the section is G1's own number");
        Check(Has(b, "7. GO/NO-GO FOR LAUNCH"), "...and G7's");
        Check(Has(b, "7.1") && Has(b, "7.3"), "steps are numbered <section>.<n>, the 4.011 scheme");

        // The auto-gates card says which state it is in, in words, not only by a tick.
        Check(Has(b, "AUTO-SEQUENCE GATES"), "the checkbox is labelled");
        Check(Has(b, "gates. The crew are OUT of"), "and says plainly what ON means");
        Check(Has(a, "by hand. This is the real"), "...and what OFF means");

        // Idle draws INITIATE; running draws the running state instead.
        DisplayList i0 = new DisplayList(4096);
        CrewGatePage.Build(i0, W, H, Idle(), 0, false);
        Check(Has(i0, "INITIATE MISSION SEQUENCE"), "idle: INITIATE is on the glass");
        Check(Has(b, "SEQUENCE RUNNING"), "running: it reads SEQUENCE RUNNING instead");
    }

    /// <summary>Did the page draw this string? Reads the display list, so it asks what was DRAWN
    /// rather than what the code meant to draw.</summary>
    static bool Has(DisplayList dl, string text)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && c.Str == text) return true;
        }
        return false;
    }
}
