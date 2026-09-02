/*
 * DragonScreen headless tests — the touch pass (T14).
 *
 * §6's "display-only controls → real" is a claim about four groups of controls: the Manual Chute
 * Deploy per-step actions, the manual-docking clusters, the Suit Leak Check's fail branch + timer, and
 * the lower console panel (§4, already live since T10 — asserted here as the SAME policy the two new
 * surfaces borrow, so a change to it cannot silently un-wire them).
 *
 * ---- WHY A PNG CANNOT CHECK ANY OF THIS ----
 * A preview shows the button. It cannot show that the rect the finger hits is the rect that was drawn,
 * that the action fired is the one the label names, or that a control the plan says must NOT act stayed
 * silent. All three are arithmetic and enum equality, and each of them would otherwise be found in the
 * capsule at the cost of a restart (C1.6). So they are asserted here, at every screen size the pages
 * are drawn at, aiming at the CENTRE of the rect the page itself publishes — the same shape as PageTest
 * and FigmaUINavTest.
 *
 * ---- THE §14.4 ASSERTIONS ARE THE POINT ----
 * The interesting checks here are the NEGATIVE ones. §14.4(a) says flight actuation is an honest no-op
 * until Part B, and the easiest way to break that is for someone to make a docking pad "just nudge a
 * little" or a chute step latch a light it has not earned. Those are asserted directly: the twelve
 * direction pads are actuation, the three chute commands that would fire something cannot light, and
 * ENABLE BACKUP PYROS lights from the vessel's own flag rather than from a latch of the page's.
 */
using System;
using DragonScreen;

public static class TouchWiringTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    // Three sizes: the panel's real aspect, a squarer one, and the preview's own 2x. A control whose
    // hit rect is computed from a different scale than its draw shows up as a size-dependent miss.
    static readonly int[,] Sizes = { { 1280, 703 }, { 1024, 768 }, { 2560, 1406 } };

    public static int Run()
    {
        Console.WriteLine("DragonScreen touch wiring (T14) tests");
        ChuteActions();
        ChuteLamps();
        DockingClusters();
        DockingActuationIsHonest();
        SuitControls();
        ConsolePanelUnchanged();
        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    // ============================================================================================
    // MANUAL CHUTE DEPLOY — every action row resolves, and to the command its own label names.
    // ============================================================================================
    static void ChuteActions()
    {
        // Six actions in each section - High's four gate rows and Standard's one carry no button.
        Check("chute page publishes every action", ManualChuteDeployPage.Actions.Length == 12,
              "got " + ManualChuteDeployPage.Actions.Length);

        for (int si = 0; si < Sizes.GetLength(0); si++)
        {
            int w = Sizes[si, 0], h = Sizes[si, 1];
            for (int i = 0; i < ManualChuteDeployPage.Actions.Length; i++)
            {
                float x, y, bw, bh;
                ManualChuteDeployPage.ActionRect(i, w, h, out x, out y, out bw, out bh);
                Check("chute action " + i + " has a rect @" + w + "x" + h, bw > 0f && bh > 0f, "");

                int hit = ManualChuteDeployPage.HitTest(x + bw * 0.5f, y + bh * 0.5f, w, h);
                Check("chute action " + i + " hits itself @" + w + "x" + h, hit == i, "got " + hit);
            }

            // Between two rows is a gap, not a neighbour: the rows are 60 design-px apart and the box is
            // 46 tall, so the 14px band between them belongs to nobody. A hit test that answered anyway
            // would mean the boxes had been grown to touch, which is how a fat finger fires the step
            // BELOW the one it was aiming at - the failure this page can least afford.
            float ax, ay, aw, ah;
            ManualChuteDeployPage.ActionRect(0, w, h, out ax, out ay, out aw, out ah);
            int miss = ManualChuteDeployPage.HitTest(ax + aw * 0.5f, ay + ah + 4f * (h / 2112f), w, h);
            Check("chute rows do not touch @" + w + "x" + h, miss != 0, "got " + miss);

            // Off to the left of the plate is the step LABEL, which is text, not a control.
            Check("chute label column is not a control @" + w + "x" + h,
                  ManualChuteDeployPage.HitTest(ax - aw, ay + ah * 0.5f, w, h) < 0, "");
        }

        // The step -> command map, read back against the labels. This is the assertion that stops a
        // later edit pointing DEPLOY MAINS at, say, CutMains: the label is the only source (§1.4), so
        // the test names the same pairing the page does and they have to keep agreeing.
        for (int i = 0; i < ManualChuteDeployPage.Actions.Length; i++)
        {
            string label = ManualChuteDeployPage.Actions[i].Label;
            PanelCommand want =
                  label == "ENABLE BACKUP PYROS" ? PanelCommand.EnableBackupPyros
                : label == "DEPLOY DROGUES"      ? PanelCommand.DroguesAndMains
                : label == "DEPLOY MAINS"        ? PanelCommand.MainsOnly
                : label == "FIRE PYRO"           ? PanelCommand.FirePyro
                : PanelCommand.None;
            Check("chute '" + label + "' -> " + want,
                  ManualChuteDeployPage.Actions[i].Command == want,
                  "got " + ManualChuteDeployPage.Actions[i].Command);
        }

        // Exactly one action names no command, and it is the "Monitor altitude" one.
        int none = 0;
        for (int i = 0; i < ManualChuteDeployPage.Actions.Length; i++)
            if (ManualChuteDeployPage.Actions[i].Command == PanelCommand.None)
            {
                none++;
                Check("the commandless chute action is the monitor step",
                      ManualChuteDeployPage.Actions[i].Act == "Monitor altitude",
                      "got '" + ManualChuteDeployPage.Actions[i].Act + "'");
            }
        Check("exactly one chute action names no command", none == 1, "got " + none);
    }

    // ============================================================================================
    // §14.4(a) on the chute page: two lamp states, and the lit one is the vessel's, not the page's.
    // ============================================================================================
    static void ChuteLamps()
    {
        PageState off = new PageState();      // nothing armed
        PageState on = new PageState();
        on.BackupPyrosArmed = true;

        int litOff = 0, litOn = 0, pyroRows = 0;
        for (int i = 0; i < ManualChuteDeployPage.Actions.Length; i++)
        {
            if (ManualChuteDeployPage.Lit(i, off)) litOff++;
            if (ManualChuteDeployPage.Lit(i, on)) litOn++;
            if (ManualChuteDeployPage.Actions[i].Command == PanelCommand.EnableBackupPyros) pyroRows++;

            // The three that would FIRE something must never light off their own press: §14.4(a) is
            // explicit that flight actuation is a no-op until Part B, and a lit "DEPLOY MAINS" over an
            // undeployed chute is the exact dishonesty the decision exists to prevent.
            PanelCommand c = ManualChuteDeployPage.Actions[i].Command;
            if (c == PanelCommand.DroguesAndMains || c == PanelCommand.MainsOnly || c == PanelCommand.FirePyro)
                Check("chute actuation " + c + " never lights",
                      !ManualChuteDeployPage.Lit(i, off) && !ManualChuteDeployPage.Lit(i, on), "");
        }

        Check("nothing lit with the pyros unarmed", litOff == 0, "got " + litOff);
        Check("every ENABLE BACKUP PYROS row lights when the flag is set", litOn == pyroRows,
              "lit " + litOn + " of " + pyroRows);
        Check("there are four ENABLE BACKUP PYROS rows", pyroRows == 4, "got " + pyroRows);

        // The lamp is the VESSEL's state, so clearing the flag puts it out - a latch of the page's own
        // would stay lit here, which is precisely the drift PageState.BackupPyrosArmed exists to stop.
        Check("the lamp follows the flag back down", !ManualChuteDeployPage.Lit(1, off), "");
    }

    // ============================================================================================
    // MANUAL DOCKING — the clusters resolve, and the two centre toggles are the only ones that act.
    // ============================================================================================
    static void DockingClusters()
    {
        var seen = new System.Collections.Generic.List<DockingSimPage.DockAct>();
        for (int si = 0; si < Sizes.GetLength(0); si++)
        {
            int w = Sizes[si, 0], h = Sizes[si, 1];
            for (int rot = 0; rot < 2; rot++)
                for (int slot = 0; slot < 7; slot++)
                {
                    float x, y, bw, bh;
                    DockingSimPage.ClusterRect(rot == 0, slot, w, h, out x, out y, out bw, out bh);
                    Check("docking cluster rect " + rot + "/" + slot + " @" + w + "x" + h, bw > 0f, "");

                    DockingSimPage.DockAct a =
                        DockingSimPage.HitTest(x + bw * 0.5f, y + bh * 0.5f, w, h);
                    Check("docking cluster " + rot + "/" + slot + " resolves @" + w + "x" + h,
                          a != DockingSimPage.DockAct.None, "");
                    if (si == 0 && !seen.Contains(a)) seen.Add(a);
                }

            for (int i = 0; i < 3; i++)
            {
                float x, y, bw, bh;
                DockingSimPage.BottomRect(i, w, h, out x, out y, out bw, out bh);
                DockingSimPage.DockAct a = DockingSimPage.HitTest(x + bw * 0.5f, y + bh * 0.5f, w, h);
                DockingSimPage.DockAct want = i == 0 ? DockingSimPage.DockAct.Instructions
                                            : i == 1 ? DockingSimPage.DockAct.ResetPositions
                                                     : DockingSimPage.DockAct.Settings;
                Check("docking bottom control " + i + " resolves @" + w + "x" + h, a == want, "got " + a);
            }

            // The middle of the HUD rings is not a control - the reticle lives there.
            Check("the docking HUD centre is not a control @" + w + "x" + h,
                  DockingSimPage.HitTest(w * 0.5f, h * 0.42f, w, h) == DockingSimPage.DockAct.None, "");
        }

        // Fourteen cluster buttons, all different: a duplicate would mean two pads firing one act, which
        // reads as a dead button on the glass and is invisible in a PNG.
        Check("every cluster button is a distinct act", seen.Count == 14, "got " + seen.Count);
    }

    static void DockingActuationIsHonest()
    {
        // §14.4(a): the twelve pads and Reset Positions would MOVE the vehicle, so they are actuation
        // and must stay honest no-ops until Part B. The two magnitude toggles and the two informational
        // controls are not actuation - they are screen state and navigation.
        int actuation = 0;
        foreach (DockingSimPage.DockAct a in Enum.GetValues(typeof(DockingSimPage.DockAct)))
            if (DockingSimPage.IsActuation(a)) actuation++;
        Check("thirteen docking controls are flight actuation", actuation == 13, "got " + actuation);

        Check("the magnitude toggles are not actuation",
              !DockingSimPage.IsActuation(DockingSimPage.DockAct.RotMagnitude)
              && !DockingSimPage.IsActuation(DockingSimPage.DockAct.TransMagnitude), "");
        Check("Settings is not actuation",
              !DockingSimPage.IsActuation(DockingSimPage.DockAct.Settings), "");
        Check("None is not actuation", !DockingSimPage.IsActuation(DockingSimPage.DockAct.None), "");

        // Settings is claimed by the NAVIGATION layer before the page is asked, so a touch on it leaves
        // the Docking page rather than being swallowed. Its two neighbours are NOT navigation.
        for (int si = 0; si < Sizes.GetLength(0); si++)
        {
            int w = Sizes[si, 0], h = Sizes[si, 1];
            for (int i = 0; i < 3; i++)
            {
                float x, y, bw, bh;
                DockingSimPage.BottomRect(i, w, h, out x, out y, out bw, out bh);
                NavHit n = FigmaUI.HitTest(UiPage.Docking, x + bw * 0.5f, y + bh * 0.5f, w, h);
                if (i == 2)
                    Check("docking Settings navigates @" + w + "x" + h,
                          n.Act == NavAct.Goto && n.Target == UiPage.Audio, "got " + n.Act + "/" + n.Target);
                else
                    Check("docking control " + i + " is not navigation @" + w + "x" + h,
                          n.Act == NavAct.None, "got " + n.Act + "/" + n.Target);
            }
        }

        // The two toggles flip independently. PageControls is a value type, so this is also the check
        // that a screen's copy is its own and one display cannot re-aim another's cluster.
        PageControls c = PageControls.Default;
        Check("clusters open on LARGE", c.DockRotLarge && c.DockTransLarge, "");
        c.DockRotLarge = !c.DockRotLarge;
        Check("the rotation toggle moves alone", !c.DockRotLarge && c.DockTransLarge, "");
    }

    // ============================================================================================
    // SUIT LEAK CHECK — the fail branch and the timer, and the one control that must NOT act.
    // ============================================================================================
    static void SuitControls()
    {
        const float RefW = 3427f, RefH = 2112f;
        for (int si = 0; si < Sizes.GetLength(0); si++)
        {
            int w = Sizes[si, 0], h = Sizes[si, 1];
            float PX(float x) => x / RefW * w;
            float PY(float y) => y / RefH * h;

            // Each plate, aimed at its own drawn centre (SuitCheckPage's Build coordinates).
            Check("INITIATE resolves @" + w + "x" + h,
                  SuitCheckPage.HitTest(PX(2535f), PY(460f), w, h, false) == SuitCheckPage.SuitAct.Start, "");
            Check("HALT resolves @" + w + "x" + h,
                  SuitCheckPage.HitTest(PX(3135f), PY(1660f), w, h, false) == SuitCheckPage.SuitAct.Halt, "");
            Check("TROUBLESHOOT resolves @" + w + "x" + h,
                  SuitCheckPage.HitTest(PX(3120f), PY(1025f), w, h, false) == SuitCheckPage.SuitAct.Troubleshoot, "");
            Check("TRY ADDITIONAL TIMER resolves @" + w + "x" + h,
                  SuitCheckPage.HitTest(PX(3120f), PY(1175f), w, h, false) == SuitCheckPage.SuitAct.Retime, "");
            Check("FINISH resolves @" + w + "x" + h,
                  SuitCheckPage.HitTest(PX(1310f), PY(1690f), w, h, false) == SuitCheckPage.SuitAct.Finish, "");

            // The popup is modal: while it is up, only CLOSE is live. A stray touch on HALT behind the
            // scrim must not reset a procedure the crew is reading the result of.
            Check("the popup swallows HALT @" + w + "x" + h,
                  SuitCheckPage.HitTest(PX(3135f), PY(1660f), w, h, true) == SuitCheckPage.SuitAct.None, "");
            Check("the popup swallows FINISH @" + w + "x" + h,
                  SuitCheckPage.HitTest(PX(1310f), PY(1690f), w, h, true) == SuitCheckPage.SuitAct.None, "");
            Check("the popup's own control resolves @" + w + "x" + h,
                  SuitCheckPage.HitTest(PX(RefW * 0.5f), PY((RefH - 1040f) * 0.5f - 40f + 1040f - 155f), w, h, true)
                      == SuitCheckPage.SuitAct.Close, "");
        }

        // TROUBLESHOOT resolves but is UNAVAILABLE, and that is a fact about the model, not a mood: no
        // suit is modelled anywhere in this build, so no suit can read "Failed Low" and the branch has
        // nothing to respond to. The day one is modelled, FailBranchLive is the single edit.
        Check("no suit can fail this check yet", !SuitCheckPage.FailBranchLive, "");
        Check("TROUBLESHOOT is unavailable",
              !SuitCheckPage.Available(SuitCheckPage.SuitAct.Troubleshoot), "");
        Check("the timer control IS available",
              SuitCheckPage.Available(SuitCheckPage.SuitAct.Retime), "");
        Check("FINISH is available", SuitCheckPage.Available(SuitCheckPage.SuitAct.Finish), "");
        Check("None is never available", !SuitCheckPage.Available(SuitCheckPage.SuitAct.None), "");
    }

    // ============================================================================================
    // THE CONSOLE PANEL (§4) — already live since T10. Asserted here because the chute page now BORROWS
    // its policy: if these answers change, two surfaces change with them.
    // ============================================================================================
    static void ConsolePanelUnchanged()
    {
        // ENABLE BACKUP PYROS is a real display-state command (§4's CONFIRMED list) and a MODE, so a
        // successful press latches its lamp. This is the outcome the chute page's identical press gets.
        Check("EnableBackupPyros is not inert (§14.4(b))",
              !PanelPolicy.IsInert(PanelCommand.EnableBackupPyros), "");
        Check("EnableBackupPyros is a mode", PanelPolicy.IsMode(PanelCommand.EnableBackupPyros), "");
        Check("an armed EnableBackupPyros lights",
              PanelPolicy.ResolveImmediate(PanelCommand.EnableBackupPyros, true, true) == PanelPressKind.ModeOn, "");

        // The three chute/pyro ACTUATION commands: not §14.4(b)-inert (their function is confirmed, they
        // are simply not flyable yet), and with no dispatcher behind them the answer is Nothing - click,
        // no light, no action, and above all no red.
        PanelCommand[] act = { PanelCommand.DroguesAndMains, PanelCommand.MainsOnly, PanelCommand.FirePyro };
        for (int i = 0; i < act.Length; i++)
        {
            Check(act[i] + " is not inert", !PanelPolicy.IsInert(act[i]), "");
            Check(act[i] + " that cannot act is Nothing",
                  PanelPolicy.ResolveImmediate(act[i], false, false) == PanelPressKind.Nothing, "");
            Check(act[i] + " leaves its lamp dark",
                  PanelPolicy.LampFor(PanelPolicy.ResolveImmediate(act[i], false, false)) == PanelLight.Dark, "");
        }

        // Every press is audible, including the ones that do nothing (§14.4(a)) - which is what stops a
        // silent no-op reading as a collider that missed, on the plate and now on the glass alike.
        for (int i = 0; i < act.Length; i++) Check(act[i] + " still clicks", PanelPolicy.Clicks(act[i]), "");
    }
}
