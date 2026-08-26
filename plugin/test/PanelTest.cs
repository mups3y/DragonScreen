/*
 * DragonScreen headless tests - the lower console.
 *
 * ---- WHY THE INTERLOCK IS TESTED HERE AND NOT IN THE CAPSULE ----
 * Arm/execute is a state machine, and a state machine is exactly the thing a game restart is worst at
 * checking: every case needs its own press sequence, most of them are ones you would never think to
 * try by hand, and the interesting ones are the failures. Half a second here beats a restart each.
 *
 * The map is checked for the properties that a transcription can plausibly get wrong - a duplicate
 * transform, a plate that lost a button, a command wired to two places - rather than re-asserting the
 * labels, which would just be the same transcription typed twice.
 */
using System;
using System.Collections.Generic;
using DragonScreen;

public static class PanelTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok)
        {
            failures++;
            Console.WriteLine("  FAIL  " + what + "   " + detail);
        }
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen panel + sequence tests");

        Map();
        Sequence();
        Steps();
        Simulated();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // ------------------------------------------------------------------ the map

    static void Map()
    {
        PanelEntry[] all = PanelMap.All;

        // Transcribed from the in-game dump: 8 + 10 + 7 + 5 + 8 across the six populated plates.
        Check("map size", all.Length == 38, "got " + all.Length + " want 38");

        // No transform addressed twice. A duplicate would mean two controls fighting over one button.
        Dictionary<string, string> seen = new Dictionary<string, string>();
        for (int i = 0; i < all.Length; i++)
        {
            string key = all[i].Plate + "/" + all[i].Button;
            Check("unique " + key, !seen.ContainsKey(key),
                  "also used by " + (seen.ContainsKey(key) ? seen[key] : ""));
            seen[key] = all[i].Label;
        }

        // Every entry carries a label and a command. An entry mapped to None would be a dead button
        // that looks wired.
        for (int i = 0; i < all.Length; i++)
        {
            Check("labelled " + all[i].Plate + "/" + all[i].Button,
                  !string.IsNullOrEmpty(all[i].Label), "empty label");
            Check("commanded " + all[i].Label, all[i].Command != PanelCommand.None, "None");
        }

        // The two emergency plates must be identical control sets - either seat can reach one, and a
        // plate that quietly lost a button would leave one seat unable to abort.
        int left = 0, right = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].Plate == PanelMap.PlateLeftEmerg) left++;
            if (all[i].Plate == PanelMap.PlateRightEmerg) right++;
        }
        Check("emergency plates match", left == right && left == 8,
              "left " + left + " right " + right);

        Check("only three need execute",
              PanelMap.NeedsExecute(PanelCommand.DeorbitNow)
              && PanelMap.NeedsExecute(PanelCommand.WaterDeorbit)
              && PanelMap.NeedsExecute(PanelCommand.Breakout)
              && !PanelMap.NeedsExecute(PanelCommand.Execute)
              && !PanelMap.NeedsExecute(PanelCommand.Cancel)
              && !PanelMap.NeedsExecute(PanelCommand.CutMains), "");
    }

    // ------------------------------------------------------------------ arm / execute

    static void Sequence()
    {
        Interlock k = new Interlock();

        // EXECUTE with nothing armed is the dangerous press: the crew believes something is armed.
        // It must refuse loudly, not quietly do nothing.
        Check("bare execute refuses", k.Press(PanelCommand.Execute) == PressResult.Refused, "");
        Check("bare execute fires nothing", k.Fired == PanelCommand.None, k.Fired.ToString());

        // CANCEL with nothing armed is the careful press. It must NOT go red.
        Check("bare cancel ignored", k.Press(PanelCommand.Cancel) == PressResult.Ignored, "");

        // The normal path.
        Check("arm", k.Press(PanelCommand.DeorbitNow) == PressResult.Armed, "");
        Check("armed is held", k.Armed == PanelCommand.DeorbitNow, k.Armed.ToString());
        Check("execute fires", k.Press(PanelCommand.Execute) == PressResult.Fire, "");
        Check("fired the armed one", k.Fired == PanelCommand.DeorbitNow, k.Fired.ToString());
        Check("disarmed after firing", k.Armed == PanelCommand.None, k.Armed.ToString());

        // ...and must not fire twice off one arming.
        Check("second execute refuses", k.Press(PanelCommand.Execute) == PressResult.Refused, "");

        // CANCEL clears.
        k.Press(PanelCommand.Breakout);
        Check("cancel clears", k.Press(PanelCommand.Cancel) == PressResult.Cancelled, "");
        Check("nothing armed after cancel", k.Armed == PanelCommand.None, k.Armed.ToString());

        // Same button twice disarms - it is its own toggle.
        k.Press(PanelCommand.WaterDeorbit);
        Check("re-press disarms", k.Press(PanelCommand.WaterDeorbit) == PressResult.Cancelled, "");
        Check("really disarmed", k.Armed == PanelCommand.None, k.Armed.ToString());

        // Changing your mind re-arms rather than refusing. The one situation this panel exists for is
        // the one with no seconds to spare for finding CANCEL first.
        k.Press(PanelCommand.DeorbitNow);
        Check("switch re-arms", k.Press(PanelCommand.Breakout) == PressResult.Armed, "");
        Check("switched to the new one", k.Armed == PanelCommand.Breakout, k.Armed.ToString());
        Check("switch fires the NEW one",
              k.Press(PanelCommand.Execute) == PressResult.Fire && k.Fired == PanelCommand.Breakout,
              k.Fired.ToString());

        // A non-interlock command must not disturb an arming - pressing CUT MAINS while a deorbit is
        // armed should leave the deorbit armed.
        k.Clear();
        k.Press(PanelCommand.DeorbitNow);
        Check("unrelated press ignored", k.Press(PanelCommand.CutMains) == PressResult.Ignored, "");
        Check("arming survives it", k.Armed == PanelCommand.DeorbitNow, k.Armed.ToString());
    }

    // ------------------------------------------------------------------ FLIGHT's sequence

    const int W = 1280, H = 710;

    static StepInputs Pad()
    {
        StepInputs s = new StepInputs();
        s.Valid = true; s.Phase = MissionPhase.Prelaunch;
        s.Crew = 4; s.OnPad = true; s.Clamped = true; s.Powered = true;
        s.Propellant01 = 0.97; s.EscapeArmed = true;
        s.BoosterAttached = true; s.S2Attached = true;
        return s;
    }

    static StepState StateOf(StepInputs s, StepId id)
    {
        StepRow[] rows = new StepRow[(int)StepId.Count];
        int n = StepList.Build(s, rows);
        for (int i = 0; i < n; i++) if (rows[i].Id == id) return rows[i].State;
        return StepState.Pending;
    }

    static void Steps()
    {
        // ---- THE DESIGN INVARIANT ----
        // Launch is commanded by the Launch Director, not the crew. If a LAUNCH step ever appears
        // here it means someone has given the console an authority the real one does not have, and
        // that is worth failing a build over rather than catching in a screenshot.
        StepRow[] rows = new StepRow[(int)StepId.Count];
        int n = StepList.Build(Pad(), rows);
        Check("sequence is populated", n == (int)StepId.Count, "got " + n);
        for (int i = 0; i < n; i++)
        {
            string l = rows[i].Label;
            Check("no launch command on the list " + l,
                  l != "LAUNCH" && l != "GO FOR LAUNCH" && l != "START LAUNCH" && l != "IGNITION", l);
        }

        // Exactly one running step, always - it is what the highlight means.
        int active = 0;
        for (int i = 0; i < n; i++) if (rows[i].State == StepState.Active) active++;
        Check("exactly one active step on the pad", active == 1, "got " + active);

        // On the pad with nothing ticked, the crew steps are outstanding and the first is running.
        StepInputs pad = Pad();
        Check("crew aboard is observed done", StateOf(pad, StepId.CrewAboard) == StepState.Done, "");
        Check("comm check waits for the crew",
              StateOf(pad, StepId.CommCheck) == StepState.Active, "");
        Check("hatch close still pending",
              StateOf(pad, StepId.HatchClose) == StepState.Pending, "");

        // Ticking advances it, and only it.
        pad.Acknowledged = StepList.Acknowledge(pad.Acknowledged, StepId.CommCheck);
        Check("tick completes comm check", StateOf(pad, StepId.CommCheck) == StepState.Done, "");
        Check("next crew step becomes active",
              StateOf(pad, StepId.SeatRotation) == StepState.Active, "");

        // Clamped on the pad means liftoff has NOT happened, however much else is ticked.
        Check("liftoff not done while clamped",
              StateOf(pad, StepId.Liftoff) != StepState.Done, "");

        // ---- ONCE FLYING, THE COUNTDOWN IS OVER ----
        // Untidy but correct: a crew member who skipped a tap must not be nagged at 40 km by a list
        // that events have overtaken.
        StepInputs up = Pad();
        up.OnPad = false; up.Clamped = false; up.Acknowledged = 0;
        up.RadarAltitude = 40000.0; up.BoosterLit = true;
        Check("hatch close closes out once flying",
              StateOf(up, StepId.HatchClose) == StepState.Done, "");
        Check("liftoff done once flying", StateOf(up, StepId.Liftoff) == StepState.Done, "");

        // Staging is observed, not assumed.
        StepInputs sep = up;
        sep.BoosterAttached = false; sep.BoosterLit = false; sep.S2Lit = true;
        Check("stage sep observed", StateOf(sep, StepId.StageSep) == StepState.Done, "");
        Check("MECO implied by a gone booster", StateOf(sep, StepId.Meco) == StepState.Done, "");
        Check("dragon sep NOT done with S2 attached",
              StateOf(sep, StepId.DragonSep) != StepState.Done, "");

        // ---- ABORT MODES ----
        Check("pad abort on the pad", StepList.AbortMode(Pad()) == "PAD ABORT",
              StepList.AbortMode(Pad()));
        StepInputs dis = Pad(); dis.EscapeArmed = false;
        Check("disarmed reads disarmed", StepList.AbortMode(dis) == "DISARMED",
              StepList.AbortMode(dis));
        StepInputs low = up; low.MaxQPassed = false;
        Check("mode 1 before max q", StepList.AbortMode(low) == "MODE 1 - LOW ALT",
              StepList.AbortMode(low));
        StepInputs hi = up; hi.MaxQPassed = true;
        Check("mode 2 after max q", StepList.AbortMode(hi) == "MODE 2 - HIGH ALT",
              StepList.AbortMode(hi));
        StepInputs free = up;
        free.BoosterAttached = false; free.S2Attached = false;
        Check("no mode once Dragon is free",
              StepList.AbortMode(free) == "NONE - DRAGON FREE", StepList.AbortMode(free));

        // ---- DRAWN WHERE IT IS HIT ----
        // The ChromeBar.LinkRect rule: aim at the centre of the rectangle the page draws and assert
        // the page's own hit test returns that step.
        for (int i = 0; i < (int)StepId.Count; i++)
        {
            float x, y, rw, rh;
            Pages.StepRect(i, W, H, out x, out y, out rw, out rh);
            PageHit got = Pages.HitTest(0, x + rw * 0.5f, y + rh * 0.4f, W, H, 0);
            Check("step " + i + " is hit where it is drawn",
                  got.Act == PageAct.AckStep && got.Arg == i,
                  got.Act + " arg " + got.Arg);
        }

        // And the list must fit above the chrome bar, or the last steps are unreachable.
        float lx, ly, lw, lh;
        Pages.StepRect((int)StepId.Count - 1, W, H, out lx, out ly, out lw, out lh);
        Check("the whole list fits on the page", ly + lh < H - ChromeBar.Height,
              "last step ends at " + (ly + lh) + ", chrome starts at " + (H - ChromeBar.Height));
    }

    // ------------------------------------------------------------------ the simulated systems

    static SystemsInputs Quiet(double dt)
    {
        SystemsInputs i = new SystemsInputs();
        i.Valid = true; i.Dt = dt; i.Crew = 4;
        i.Charge01 = 0.9; i.HottestPart01 = 0.2; i.GForce = 1.0;
        return i;
    }

    static void Simulated()
    {
        // ---- NOTHING FIRES IN NORMAL FLIGHT ----
        // The whole model is worthless if it cries wolf. An hour of quiet cruise must produce no
        // fire, no leak and no trip.
        SystemsState s = SystemsState.Fresh(); s.Bus1On = true;   // powered, or Get() reads Online regardless
        for (int t = 0; t < 360; t++) Systems.Update(ref s, Quiet(10.0));
        Check("quiet cruise starts no fire", !s.Fire, s.FireIntensity.ToString("F3"));
        Check("quiet cruise springs no leak", !s.Leaking, s.LeakRate.ToString("F3"));
        Check("quiet cruise trips no string",
              Systems.Get(s, 1, 2) == StringState.Online, Systems.Get(s, 1, 2).ToString());
        // ...but the consumables MUST have moved, or they are decoration.
        Check("oxygen is being used", s.Oxygen < 1.0 && s.Oxygen > 0.9, s.Oxygen.ToString("F4"));
        Check("canisters are loading", s.CanisterUsed > 0.0, s.CanisterUsed.ToString("F4"));

        // ---- EVERY EVENT HAS A REAL TRIGGER ----
        SystemsState f = SystemsState.Fresh();
        SystemsInputs hot = Quiet(1.0); hot.HottestPart01 = 0.97;
        for (int t = 0; t < 60; t++) Systems.Update(ref f, hot);
        Check("a part near its limit starts a fire", f.Fire, f.FireIntensity.ToString("F3"));

        // The bottle puts it out, and is a one-shot.
        Check("suppressant works", Systems.SuppressFire(ref f), "");
        Check("suppressant is spent by half", f.Suppressant < 0.6, f.Suppressant.ToString("F2"));
        // Refuses on a cabin with no fire - a red dash then means "nothing to fight", not "broken".
        SystemsState calm = SystemsState.Fresh();
        Check("suppress refuses with no fire", !Systems.SuppressFire(ref calm), "");
        Check("depress refuses with no leak", !Systems.DepressResponse(ref calm), "");

        // Overstress opens a leak; isolating closes it, and not instantly.
        SystemsState lk = SystemsState.Fresh();
        SystemsInputs hard = Quiet(1.0); hard.GForce = 14.0;
        Systems.Update(ref lk, hard);
        Check("overstress springs a leak", lk.Leaking, lk.LeakRate.ToString("F3"));
        Check("depress response takes", Systems.DepressResponse(ref lk), "");
        Systems.Update(ref lk, Quiet(1.0));
        Check("leak is still closing after a second", lk.Leaking, lk.LeakRate.ToString("F3"));
        for (int t = 0; t < 70; t++) Systems.Update(ref lk, Quiet(1.0));
        Check("leak closes within a minute", !lk.Leaking, lk.LeakRate.ToString("F3"));

        // ---- STRINGS ----
        // Buses now start OFF (the flight-computer power gate), so the sim's own string behaviour is
        // only exercised with the bus powered - which is the only state in which it means anything.
        SystemsState p = SystemsState.Fresh(); p.Bus1On = true;
        SystemsInputs flat = Quiet(1.0); flat.Charge01 = 0.05;
        Systems.Update(ref p, flat);
        Check("undervoltage trips the C strings",
              Systems.Get(p, 1, 2) == StringState.Tripped, Systems.Get(p, 1, 2).ToString());
        Check("a tripped string will not toggle", !Systems.ToggleString(ref p, 1, 2), "");
        Check("reset refuses on a sick bus", !Systems.ResetBus(ref p, 1, 0.05), "");
        Check("reset takes once charge recovers", Systems.ResetBus(ref p, 1, 0.9), "");
        Check("string is back", Systems.Get(p, 1, 2) == StringState.Online,
              Systems.Get(p, 1, 2).ToString());

        // A string the CREW isolated must never be quietly re-closed by the model.
        SystemsState iso = SystemsState.Fresh(); iso.Bus2On = true;
        Systems.ToggleString(ref iso, 2, 0);
        Check("crew isolation holds", Systems.Get(iso, 2, 0) == StringState.Isolated, "");
        for (int t = 0; t < 100; t++) Systems.Update(ref iso, Quiet(1.0));
        Check("model does not re-close it",
              Systems.Get(iso, 2, 0) == StringState.Isolated, Systems.Get(iso, 2, 0).ToString());
        Check("online count reflects it", Systems.OnlineCount(iso, 2) == 2,
              Systems.OnlineCount(iso, 2).ToString());

        // ---- DETERMINISTIC ----
        // Two vehicles in the same state must give the same answer, or three screens disagree and
        // the whole thing is a random number with extra steps.
        SystemsState a1 = SystemsState.Fresh(), a2 = SystemsState.Fresh();
        for (int t = 0; t < 50; t++) { Systems.Update(ref a1, hot); Systems.Update(ref a2, hot); }
        Check("model is deterministic", a1.FireIntensity == a2.FireIntensity,
              a1.FireIntensity + " vs " + a2.FireIntensity);
    }
}
