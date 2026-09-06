// Tests for pure/RecoveryPlan.cs — §B16.7's physics-range lifecycle (register W9).
//
// THE PROPERTIES THAT MATTER, AND WHY EACH ONE IS HERE RATHER THAN OBVIOUS:
//  • The ranges go wide BEFORE separation. `RangeExtender.Enable` walks the vessel list, so a call made
//    after the split cannot have widened a vessel that did not exist when it ran; arming late is the
//    failure step 1 exists to prevent, and it is invisible in a code read.
//  • The ranges ALWAYS come back. Wide ranges carry §B16.7's accepted risk (phantom forces past 100 km),
//    so "wide and nobody recovering" must be unreachable from every stage — disarm, timeout, host
//    release, an abort that never separated, and the terminal stage.
//  • A recovery is REPORTED only while one is really running. `Armed` is a physics state with no booster
//    in it; `InProgress` lighting there would claim a recovery that has not started (§14.4(a)).
//  • The machine is TOTAL. Every stage × every input combination returns a stage and an action.
using System;
using DragonScreen;

public static class RecoveryPlanTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string d)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + d); } }

    // A stack that is up and armed, with nothing separated yet and stock ranges.
    static RecoveryInputs Base()
    {
        RecoveryInputs i;
        i.Armed = true;
        i.ActiveIsStack = true;
        i.ActiveAirborne = true;
        i.BoosterBound = false;
        i.RangeExtended = false;
        i.RecoveringForS = 0.0;
        i.TimeoutS = 1200.0;
        return i;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen RecoveryPlan (§B16.7 physics-range lifecycle) tests");

        // ---- STEP 1: arm before separation ---------------------------------------------------------
        RecoveryDecision d = RecoveryPlan.Next(RecoveryStage.Idle, Base());
        Check("Idle + armed + stack airborne -> Armed", d.Stage == RecoveryStage.Armed, d.Stage.ToString());
        Check("...and the ranges go WIDE, before anything separates", d.Range == RangeAct.Extend, d.Range.ToString());
        Check("...and it says so", d.Note != null, "no note");

        // On the pad: airborne is the gate, so nothing widens.
        RecoveryInputs pad = Base(); pad.ActiveAirborne = false;
        d = RecoveryPlan.Next(RecoveryStage.Idle, pad);
        Check("Idle on the pad stays Idle", d.Stage == RecoveryStage.Idle, d.Stage.ToString());
        Check("Idle on the pad touches nothing", d.Range == RangeAct.None, d.Range.ToString());

        // Not the stack (no pod aboard) — this machine only ever arms off the vessel focus must not leave.
        RecoveryInputs notStack = Base(); notStack.ActiveIsStack = false;
        d = RecoveryPlan.Next(RecoveryStage.Idle, notStack);
        Check("Idle with no pod aboard the active vessel stays Idle", d.Stage == RecoveryStage.Idle, d.Stage.ToString());
        Check("...and does not widen ranges off a non-stack vessel", d.Range == RangeAct.None, d.Range.ToString());

        // ---- STEP 2: the host binds a booster ------------------------------------------------------
        RecoveryInputs bound = Base(); bound.RangeExtended = true; bound.BoosterBound = true;
        d = RecoveryPlan.Next(RecoveryStage.Armed, bound);
        Check("Armed + the host holds a booster -> Recovering", d.Stage == RecoveryStage.Recovering, d.Stage.ToString());
        Check("...and RE-extends, because the booster did not exist at arming time",
              d.Range == RangeAct.Extend, d.Range.ToString());

        // Waiting: hold, and do not re-issue Enable every frame.
        RecoveryInputs waiting = Base(); waiting.RangeExtended = true;
        d = RecoveryPlan.Next(RecoveryStage.Armed, waiting);
        Check("Armed and waiting stays Armed", d.Stage == RecoveryStage.Armed, d.Stage.ToString());
        Check("...and does not re-Enable every frame", d.Range == RangeAct.None, d.Range.ToString());
        Check("...and does not annunciate every frame", d.Note == null, d.Note);

        // ...but if something else put the ranges back (a scene reload), re-assert them.
        d = RecoveryPlan.Next(RecoveryStage.Armed, Base());
        Check("Armed with the ranges lost re-Extends", d.Range == RangeAct.Extend, d.Range.ToString());

        // ---- THE ABORT / REVERT PATH: airborne and then not, with nothing ever separated ------------
        RecoveryInputs cameDown = Base(); cameDown.ActiveAirborne = false; cameDown.RangeExtended = true;
        d = RecoveryPlan.Next(RecoveryStage.Armed, cameDown);
        Check("Armed + the stack is down again -> Idle", d.Stage == RecoveryStage.Idle, d.Stage.ToString());
        Check("...and the ranges are RESTORED", d.Range == RangeAct.Restore, d.Range.ToString());

        // ---- STEP 5: the host lets go --------------------------------------------------------------
        RecoveryInputs released = Base(); released.RangeExtended = true; released.RecoveringForS = 300.0;
        d = RecoveryPlan.Next(RecoveryStage.Recovering, released);
        Check("Recovering + the host released -> Done", d.Stage == RecoveryStage.Done, d.Stage.ToString());
        Check("...and the ranges are RESTORED", d.Range == RangeAct.Restore, d.Range.ToString());
        Check("...and it says so", d.Note != null, "no note");

        // Still flying: hold the ranges, say nothing.
        RecoveryInputs flying = Base(); flying.BoosterBound = true; flying.RangeExtended = true;
        flying.RecoveringForS = 300.0;
        d = RecoveryPlan.Next(RecoveryStage.Recovering, flying);
        Check("Recovering + still bound stays Recovering", d.Stage == RecoveryStage.Recovering, d.Stage.ToString());
        Check("...and holds the ranges wide", d.Range == RangeAct.None, d.Range.ToString());
        Check("...and does not annunciate every frame", d.Note == null, d.Note);

        // ---- THE BACKSTOP: a host bound forever must not hold the ranges forever --------------------
        RecoveryInputs stuck = Base(); stuck.BoosterBound = true; stuck.RangeExtended = true;
        stuck.RecoveringForS = 1200.1;
        d = RecoveryPlan.Next(RecoveryStage.Recovering, stuck);
        Check("Recovering past the timeout -> Done even though the host is still bound",
              d.Stage == RecoveryStage.Done, d.Stage.ToString());
        Check("...and the ranges are RESTORED", d.Range == RangeAct.Restore, d.Range.ToString());

        // Exactly at the timeout is NOT past it (strict >, so a zero-length tick cannot end a recovery).
        RecoveryInputs atLimit = Base(); atLimit.BoosterBound = true; atLimit.RangeExtended = true;
        atLimit.RecoveringForS = 1200.0;
        d = RecoveryPlan.Next(RecoveryStage.Recovering, atLimit);
        Check("Recovering exactly AT the timeout keeps recovering", d.Stage == RecoveryStage.Recovering, d.Stage.ToString());

        // A zero/negative timeout disables the backstop rather than tripping it instantly.
        RecoveryInputs noTimeout = Base(); noTimeout.BoosterBound = true; noTimeout.RangeExtended = true;
        noTimeout.RecoveringForS = 1e9; noTimeout.TimeoutS = 0.0;
        d = RecoveryPlan.Next(RecoveryStage.Recovering, noTimeout);
        Check("a zero timeout disables the backstop, it does not trip it",
              d.Stage == RecoveryStage.Recovering, d.Stage.ToString());

        // ---- THE DISARM, FROM EVERY STAGE ----------------------------------------------------------
        // The crew's arm is a live control and its one guaranteed power is putting the physics back.
        bool disarmIdles = true, disarmRestores = true;
        foreach (RecoveryStage st in new[] { RecoveryStage.Idle, RecoveryStage.Armed,
                                             RecoveryStage.Recovering, RecoveryStage.Done })
        {
            RecoveryInputs off = Base();
            off.Armed = false; off.RangeExtended = true; off.BoosterBound = true;
            RecoveryDecision dd = RecoveryPlan.Next(st, off);
            if (dd.Stage != RecoveryStage.Idle) disarmIdles = false;
            if (dd.Range != RangeAct.Restore) disarmRestores = false;
        }
        Check("disarming from ANY stage returns to Idle", disarmIdles, "");
        Check("disarming from ANY stage restores the ranges", disarmRestores, "");

        // Disarmed with stock ranges already: nothing to do, and no log line.
        RecoveryInputs offClean = Base(); offClean.Armed = false;
        d = RecoveryPlan.Next(RecoveryStage.Armed, offClean);
        Check("disarmed with stock ranges already touches nothing", d.Range == RangeAct.None, d.Range.ToString());
        Check("...and stays quiet", d.Note == null, d.Note);

        // ---- Done is terminal for the flight, but never leaves the ranges wide ----------------------
        RecoveryInputs after = Base(); after.BoosterBound = true; after.RangeExtended = false;
        d = RecoveryPlan.Next(RecoveryStage.Done, after);
        Check("Done does not re-arm off a second separated craft", d.Stage == RecoveryStage.Done, d.Stage.ToString());
        Check("...and touches nothing while the ranges are stock", d.Range == RangeAct.None, d.Range.ToString());
        RecoveryInputs afterWide = Base(); afterWide.RangeExtended = true;
        d = RecoveryPlan.Next(RecoveryStage.Done, afterWide);
        Check("Done RESTORES if it ever finds the ranges wide again", d.Range == RangeAct.Restore, d.Range.ToString());

        // ---- ⭐ THE INVARIANT, SWEPT: "ranges wide and nobody recovering" is unreachable -------------
        // Every stage × every boolean combination. Whenever the decision leaves the machine somewhere
        // other than Armed or Recovering, the ranges must not be left wide.
        bool neverStrands = true; int combos = 0;
        foreach (RecoveryStage st in new[] { RecoveryStage.Idle, RecoveryStage.Armed,
                                             RecoveryStage.Recovering, RecoveryStage.Done })
            for (int m = 0; m < 32; m++)
            {
                RecoveryInputs i;
                i.Armed = (m & 1) != 0;
                i.ActiveIsStack = (m & 2) != 0;
                i.ActiveAirborne = (m & 4) != 0;
                i.BoosterBound = (m & 8) != 0;
                i.RangeExtended = (m & 16) != 0;
                i.RecoveringForS = 5.0;
                i.TimeoutS = 1200.0;
                RecoveryDecision dd = RecoveryPlan.Next(st, i);
                combos++;
                bool holdsRanges = dd.Stage == RecoveryStage.Armed || dd.Stage == RecoveryStage.Recovering;
                bool wideAfter = holdsRanges
                    ? true                                     // allowed to be wide
                    : dd.Range == RangeAct.Restore || !i.RangeExtended;
                if (!wideAfter) neverStrands = false;
                if (!holdsRanges && dd.Range == RangeAct.Extend) neverStrands = false;   // never widen on the way out
            }
        Check("no stage ever leaves the ranges wide without a recovery to justify them",
              neverStrands, combos + " combinations");
        Check("the sweep actually ran", combos == 128, combos.ToString());

        // ---- InProgress is exactly Recovering (§14.4(a): no lamp for a physics state) ---------------
        Check("InProgress: Recovering", RecoveryPlan.InProgress(RecoveryStage.Recovering), "");
        Check("InProgress: NOT Armed", !RecoveryPlan.InProgress(RecoveryStage.Armed), "");
        Check("InProgress: NOT Idle", !RecoveryPlan.InProgress(RecoveryStage.Idle), "");
        Check("InProgress: NOT Done", !RecoveryPlan.InProgress(RecoveryStage.Done), "");

        // ---- ⛔ NO FOCUS VERB EXISTS (§B16.7). The type system is the guarantee, so assert its shape --
        Check("RangeAct has exactly three values, none of which moves focus",
              Enum.GetValues(typeof(RangeAct)).Length == 3, Enum.GetValues(typeof(RangeAct)).Length.ToString());
        Check("RecoveryStage has exactly four values",
              Enum.GetValues(typeof(RecoveryStage)).Length == 4, Enum.GetValues(typeof(RecoveryStage)).Length.ToString());

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
