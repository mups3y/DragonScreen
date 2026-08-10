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
        AscentProfile();

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
        SystemsState s = SystemsState.Fresh();
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
        SystemsState p = SystemsState.Fresh();
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
        SystemsState iso = SystemsState.Fresh();
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

    // ------------------------------------------------------------------ ascent guidance

    static AscentInputs Fly(double altM, double apM, double peM, double qKpa)
    {
        AscentInputs a = new AscentInputs();
        a.Valid = true;
        a.Altitude = altM; a.RadarAltitude = altM;
        a.ApoapsisM = apM; a.PeriapsisM = peM;
        a.AtmosphereDepthM = 70000.0;      // Kerbin
        a.DynamicPressureKpa = qKpa;
        a.TimeToApoapsisS = 200.0;
        a.AvailableThrust = 800.0;
        return a;
    }

    /// <summary>An orbit with a given periapsis, for the is-it-actually-in-orbit checks.</summary>
    static AscentInputs Sub(AscentTarget t, double periapsis)
    {
        AscentInputs a = Fly(86000.0, 86200.0, periapsis, 0.0);
        a.CircDvMps = 0.1; a.TimeToApoapsisS = 100.0;
        return a;
    }

    static void AscentProfile()
    {
        AscentTarget t = AscentTarget.Station();
        Check("station target is the MEASURED orbit", Math.Abs(t.AltitudeM - 86000.0) < 1.0,
              t.AltitudeM.ToString());

        // ---- THE TURN IS MONOTONIC AND NEVER POINTS AT THE GROUND ----
        // A guidance law that commands a negative pitch during ascent is a bug that flies the
        // vehicle into the ground at full throttle, so it is worth an explicit check at every
        // altitude rather than trusting the formula.
        double prev = 91.0;
        for (double alt = 0; alt <= 80000.0; alt += 500.0)
        {
            double p = Ascent.TurnPitch(Fly(alt, 1000.0, 0.0, 0.0));
            Check("pitch never below the horizon at " + alt, p >= 0.0, p.ToString("F2"));
            Check("pitch never increases at " + alt, p <= prev + 0.001,
                  p.ToString("F2") + " after " + prev.ToString("F2"));
            prev = p;
        }
        Check("starts vertical", Ascent.TurnPitch(Fly(0.0, 1000.0, 0.0, 0.0)) > 89.0, "");

        // ---- THE FIRST STAGE FLOORS AT THE MECO ANGLE. IT DOES NOT GO FLAT. ----
        // This is F9I's law, not the sqrt curve that was here first, and the floor is the whole
        // point: BOOSTER.ks separates on `heading(azimuth, MECOangle)` and depends on the stack
        // having been HOLDING 45 degrees at separation. A first stage that pitched to zero would
        // hand the booster an attitude its recovery was never written for.
        AscentTarget rtls = AscentTarget.Station();
        Check("RTLS constants are F9I's",
              rtls.MecoAngleDeg == 45.0 && rtls.PitchGain == 110.0 && rtls.StageAltM == 60000.0,
              rtls.MecoAngleDeg + "/" + rtls.PitchGain + "/" + rtls.StageAltM);
        Check("first stage floors at MECO angle",
              Math.Abs(Ascent.TurnPitch(Fly(60000.0, 1000.0, 0.0, 0.0), rtls) - 45.0) < 0.001,
              Ascent.TurnPitch(Fly(60000.0, 1000.0, 0.0, 0.0), rtls).ToString("F3"));
        Check("first stage never goes below the MECO angle",
              Ascent.TurnPitch(Fly(200000.0, 1000.0, 0.0, 0.0), rtls) >= 45.0, "");
        // 90*(1 - 33000/66000) = 45, so the floor is reached at exactly half the shaped altitude.
        Check("floor is reached at half the shaped altitude",
              Math.Abs(Ascent.TurnPitch(Fly(33000.0, 1000.0, 0.0, 0.0), rtls) - 45.0) < 0.01,
              Ascent.TurnPitch(Fly(33000.0, 1000.0, 0.0, 0.0), rtls).ToString("F3"));

        // ---- THE SECOND STAGE FLIES THE OTHER LAW, AND IT DOES GO FLAT ----
        AscentInputs s2 = Fly(60000.0, 80000.0, 10000.0, 0.0);
        s2.SecondStage = true; s2.TimeToApoapsisS = 200.0;
        Check("second stage pitches well down", Ascent.TurnPitch(s2, rtls) < 20.0,
              Ascent.TurnPitch(s2, rtls).ToString("F2"));
        AscentInputs s2top = Fly(69500.0, 80000.0, 10000.0, 0.0);
        s2top.SecondStage = true; s2top.TimeToApoapsisS = 200.0;
        Check("second stage is nearly flat at the tangent altitude",
              Ascent.TurnPitch(s2top, rtls) < 1.0, Ascent.TurnPitch(s2top, rtls).ToString("F3"));
        // avoidFireDeath: inside 30 s to apoapsis it pitches UP to stop the apoapsis running away.
        AscentInputs fire = s2; fire.TimeToApoapsisS = 5.0;
        Check("avoidFireDeath pitches up near apoapsis",
              Ascent.TurnPitch(fire, rtls) > Ascent.TurnPitch(s2, rtls),
              Ascent.TurnPitch(fire, rtls).ToString("F2"));
        Check("but never above the MECO angle", Ascent.TurnPitch(fire, rtls) <= 45.0, "");

        // ---- MAX Q THROTTLING ----
        Check("full throttle below the Q limit",
              Ascent.QThrottle(Fly(5000.0, 1000.0, 0.0, 10.0)) == 1.0, "");
        Check("throttles back above it",
              Ascent.QThrottle(Fly(8000.0, 1000.0, 0.0, 30.0)) < 1.0, "");
        Check("never throttles below the floor",
              Ascent.QThrottle(Fly(8000.0, 1000.0, 0.0, 200.0)) >= 0.35, "");

        // ---- PHASES ADVANCE ON STATE, AND ONLY FORWARD ----
        AscentCommand c = Ascent.Guide(Fly(50.0, 300.0, 0.0, 1.0), t, AscentPhase.Idle);
        Check("starts with a vertical rise", c.Phase == AscentPhase.VerticalRise, c.Phase.ToString());
        Check("vertical rise is full throttle", c.Throttle == 1.0, c.Throttle.ToString());
        Check("vertical rise points up", c.PitchDeg > 89.0, c.PitchDeg.ToString("F1"));

        c = Ascent.Guide(Fly(4000.0, 20000.0, 0.0, 12.0), t, AscentPhase.VerticalRise);
        Check("turns once clear of the pad", c.Phase == AscentPhase.GravityTurn, c.Phase.ToString());

        // ---- ⛔ TWO TARGETS, NOT ONE. THIS IS THE ARCHITECTURE FIX. ----
        // tgtAlt 60 km is the MECO apoapsis; tgtOrbPE 86 km is the orbit. Flying the first stage at
        // the 86 km figure is what burned the booster dry and left the recovery a stage with nothing
        // for boostback.
        Check("MECO target is well below the orbit target",
              Ascent.StageTarget(t) < t.AltitudeM * 0.8,
              Ascent.StageTarget(t) + " vs " + t.AltitudeM);

        // First stage ends at 60 km apoapsis - with propellant still aboard.
        c = Ascent.Guide(Fly(40000.0, 60500.0, 20000.0, 0.1), t, AscentPhase.GravityTurn);
        Check("first stage ends at the MECO target", c.Phase == AscentPhase.Meco, c.Phase.ToString());
        Check("MECO cuts the engines", c.Throttle == 0.0, c.Throttle.ToString());
        Check("MECO holds the separation attitude",
              Math.Abs(c.PitchDeg - t.MecoAngleDeg) < 0.001, c.PitchDeg.ToString("F2"));
        Check("MECO does not stage instantly", !c.Stage, "");

        // ...and it does NOT end at the orbit target, which is the bug this replaced.
        c = Ascent.Guide(Fly(40000.0, 40500.0, 20000.0, 0.1), t, AscentPhase.GravityTurn);
        Check("first stage keeps burning below the MECO target",
              c.Phase == AscentPhase.GravityTurn, c.Phase.ToString());

        // The hold expires, then it separates.
        AscentInputs held = Fly(60000.0, 60500.0, 20000.0, 0.1);
        held.PhaseElapsedS = Ascent.MecoHoldS + 0.1;
        c = Ascent.Guide(held, t, AscentPhase.Meco);
        Check("after the hold it separates", c.Phase == AscentPhase.StageSep, c.Phase.ToString());

        // ---- ⛔ AND THE MVac MUST NOT LIGHT YET ----
        // Separation and MVac ignition used to land on the same tick, so the plume went straight
        // into the booster at zero range - `falcon-open-issues` number one. StageSep is the gap.
        Check("the MVac stays out during the separation coast",
              Math.Abs(c.Throttle) < 1e-9, c.Throttle.ToString("F3"));
        Check("RCS is on for the unpowered coast - the gimbal went with the engines", c.Rcs, "");

        AscentInputs sepHeld = Fly(60000.0, 60500.0, 20000.0, 0.1);
        sepHeld.PhaseElapsedS = Ascent.PostSepHoldS * 0.5;
        Check("the coast is held, not skipped",
              Ascent.Guide(sepHeld, t, AscentPhase.StageSep).Phase == AscentPhase.StageSep, "");
        Check("and it is silent while it is held",
              Math.Abs(Ascent.Guide(sepHeld, t, AscentPhase.StageSep).Throttle) < 1e-9, "");
        sepHeld.PhaseElapsedS = Ascent.PostSepHoldS + 0.1;
        Check("then the second stage takes over",
              Ascent.Guide(sepHeld, t, AscentPhase.StageSep).Phase == AscentPhase.BurnToApoapsis,
              "");
        Check("the gap is long enough to matter", Ascent.PostSepHoldS >= 2.0, "");

        // ---- ULLAGE BEFORE THE SECOND STAGE IS ASKED FOR THRUST ----
        AscentInputs ull = Fly(62000.0, 62000.0, 20000.0, 0.0);
        ull.SecondStage = true; ull.PhaseElapsedS = 2.0; ull.TimeToApoapsisS = 200.0;
        c = Ascent.Guide(ull, t, AscentPhase.BurnToApoapsis);
        Check("ullage runs first", c.Note == "ULLAGE", c.Note);
        Check("ullage is a trickle", Math.Abs(c.Throttle - Ascent.UllageThrottle) < 1e-9,
              c.Throttle.ToString("F4"));
        Check("and RCS settles the tanks", c.UllageFore > 0.5, c.UllageFore.ToString("F2"));

        // Then the proportional apoapsis burn - variable, never a locked full throttle.
        AscentInputs bta = ull; bta.PhaseElapsedS = 10.0;
        c = Ascent.Guide(bta, t, AscentPhase.BurnToApoapsis);
        Check("then it burns to apoapsis", c.Note == "BURN TO APOAPSIS", c.Note);
        Check("throttle is variable, not pinned",
              c.Throttle > 0.0 && c.Throttle <= 1.0, c.Throttle.ToString("F3"));
        // Closer to the target means less throttle - it is proportional to the DEFICIT.
        AscentInputs nearly = bta; nearly.ApoapsisM = 85000.0;
        Check("nearly there means gentler",
              Ascent.ApoapsisThrottle(nearly, t) < Ascent.ApoapsisThrottle(bta, t),
              Ascent.ApoapsisThrottle(nearly, t).ToString("F3"));
        Check("but never stops closing", Ascent.ApoapsisThrottle(nearly, t) >= 0.1, "");

        // Second stage reaching the ORBIT target hands over to the coast.
        c = Ascent.Guide(Fly(80000.0, 86500.0, 20000.0, 0.0), t, AscentPhase.BurnToApoapsis);
        Check("second stage ends at the orbit target", c.Phase == AscentPhase.Coast,
              c.Phase.ToString());
        Check("coast is engines off", c.Throttle == 0.0, c.Throttle.ToString());

        // ---- CIRCULARISATION RUNS ON THE dv, NOT ON PERIAPSIS ----
        // The periapsis-chasing version had no fixed point and flew a stage to escape velocity.
        AscentInputs near = Fly(85000.0, 86500.0, 20000.0, 0.0);
        near.TimeToApoapsisS = 5.0; near.CircDvMps = 120.0;
        c = Ascent.Guide(near, t, AscentPhase.Coast);
        Check("circularises near apoapsis", c.Phase == AscentPhase.Circularise, c.Phase.ToString());
        Check("a large dv burns hard", c.Throttle > 0.9, c.Throttle.ToString("F3"));

        // Throttle EASES as the dv closes - a full-throttle finish overshoots between ticks.
        AscentInputs closing = near; closing.CircDvMps = 5.0;
        Check("throttle eases as dv closes",
              Ascent.Guide(closing, t, AscentPhase.Circularise).Throttle < 0.5,
              Ascent.Guide(closing, t, AscentPhase.Circularise).Throttle.ToString("F3"));

        // Converged: dv at zero AND a periapsis that actually clears the atmosphere.
        AscentInputs done = Fly(86000.0, 86200.0, 85800.0, 0.0);
        done.CircDvMps = 0.1; done.TimeToApoapsisS = 100.0;
        c = Ascent.Guide(done, t, AscentPhase.Circularise);
        Check("dv at zero finishes it", c.Phase == AscentPhase.Done, c.Phase.ToString());
        Check("done means engines off", c.Throttle == 0.0, c.Throttle.ToString());

        // ---- ⛔ INSERTION COMPLETE MUST MEAN AN ACTUAL ORBIT ----
        // Flight 16:58 announced it with apoapsis 129 km and periapsis MINUS 598 km, then
        // disengaged and handed back a vehicle nobody was flying. Every condition that fired was
        // individually defensible; none asked whether it was in orbit.
        AscentInputs lob = Fly(86000.0, 129000.0, -598000.0, 0.0);
        lob.CircDvMps = 0.1; lob.TimeToApoapsisS = 100.0;
        Check("a suborbital lob is NOT circularised", !Ascent.Circularised(lob, t), "");
        Check("periapsis inside the atmosphere is not an orbit either",
              !Ascent.Circularised(Sub(t, 20000.0), t), "");
        Check("periapsis above the atmosphere with no dv left IS",
              Ascent.Circularised(Sub(t, 85800.0), t), "");

        // ---- ⛔ A DIVERGING BURN IS NOT A FINISHED ONE ----
        // circDv ROSE from 2098 to 2174 m/s while the burn ran; the direction swung past 90 degrees
        // and the overshoot test read that as success.
        AscentInputs diverging = Fly(86000.0, 129000.0, -598000.0, 0.0);
        diverging.CircDvFlipped = true; diverging.CircDvMps = 2174.0;
        diverging.TimeToApoapsisS = 100.0;
        c = Ascent.Guide(diverging, t, AscentPhase.Circularise);
        Check("a diverging burn ABORTS", c.Phase == AscentPhase.Done, c.Phase.ToString());
        Check("and says so rather than claiming success",
              c.Note != null && c.Note.Contains("DIVERGING"), c.Note);
        Check("and cuts the throttle", c.Throttle == 0.0, c.Throttle.ToString());

        // A genuine overshoot - reversed with a TINY dv - is still a clean finish.
        AscentInputs over = Fly(86000.0, 86200.0, 85800.0, 0.0);
        over.CircDvFlipped = true; over.CircDvMps = 0.2; over.TimeToApoapsisS = 100.0;
        c = Ascent.Guide(over, t, AscentPhase.Circularise);
        Check("a real overshoot finishes cleanly", c.Phase == AscentPhase.Done, c.Phase.ToString());
        Check("without the abort note", c.Note == null || !c.Note.Contains("DIVERGING"), c.Note);

        // ---- ⛔ ONE TRANSITION PER CALL. THE ROOT CAUSE OF THE WHOLE FLIGHT. ----
        // A chain of plain `if`s let GRAVITY TURN -> Meco -> BurnToApoapsis happen in ONE call,
        // because the MECO hold was compared against the time spent in the PREVIOUS phase. MECO was
        // never held, never logged, never staged - and every other failure followed from that.
        AscentInputs cross = Fly(40000.0, 60500.0, 20000.0, 0.1);
        cross.PhaseElapsedS = 90.0;          // a long time in GRAVITY TURN, as on the real flight
        c = Ascent.Guide(cross, t, AscentPhase.GravityTurn);
        Check("crossing the MECO target stops AT Meco", c.Phase == AscentPhase.Meco,
              c.Phase.ToString());
        Check("it does NOT skip through to the second stage",
              c.Phase != AscentPhase.BurnToApoapsis, c.Phase.ToString());
        // ...and only advances once the hold has been served IN Meco.
        AscentInputs inMeco = cross; inMeco.PhaseElapsedS = 0.5;
        Check("a fresh MECO holds", Ascent.Guide(inMeco, t, AscentPhase.Meco).Phase
              == AscentPhase.Meco, "");
        inMeco.PhaseElapsedS = Ascent.MecoHoldS + 0.1;
        AscentCommand meco = Ascent.Guide(inMeco, t, AscentPhase.Meco);
        Check("a served MECO advances", meco.Phase == AscentPhase.StageSep,
              meco.Phase.ToString());

        // ---- ⛔ AND IT MUST ACTUALLY COMMAND THE SEPARATION ----
        // The `else if` fix alone would have flown the SAME failed mission a second time. `Stage`
        // was set inside `case Meco:`, which can never run on the tick the hold expires - the
        // transition fires first and the switch renders BurnToApoapsis instead. So the booster
        // stayed attached, and everything downstream failed again, with a fix in place that looked
        // right. The command belongs ON THE TRANSITION.
        Check("the MECO transition STAGES", meco.Stage, "booster would stay attached");
        AscentInputs holding = cross; holding.PhaseElapsedS = 0.5;
        Check("a held MECO does not stage yet",
              !Ascent.Guide(holding, t, AscentPhase.Meco).Stage, "staged during the hold");
        Check("and no other phase stages",
              !Ascent.Guide(cross, t, AscentPhase.GravityTurn).Stage
              && !Ascent.Guide(done, t, AscentPhase.Circularise).Stage, "");

        // ---- ⛔ THE RUNAWAY BACKSTOP. THIS IS THE ESCAPE-TRAJECTORY GUARD. ----
        // It must fire regardless of phase and regardless of what the guidance believes.
        AscentInputs runaway = Fly(200000.0, 400000.0, 80000.0, 0.0);
        runaway.CircDvMps = 500.0;
        foreach (AscentPhase ph in new[] { AscentPhase.GravityTurn, AscentPhase.Coast,
                                           AscentPhase.Circularise })
        {
            AscentCommand rc = Ascent.Guide(runaway, t, ph);
            Check("runaway aborts from " + ph, rc.Phase == AscentPhase.Done, rc.Phase.ToString());
            Check("runaway cuts the throttle from " + ph, rc.Throttle == 0.0,
                  rc.Throttle.ToString());
        }
        Check("backstop trips above 1.5x target",
              Ascent.ApoapsisRunawayFactor == 1.5, Ascent.ApoapsisRunawayFactor.ToString());

        // An invalid vessel must never command thrust.
        AscentInputs bad = new AscentInputs();
        c = Ascent.Guide(bad, t, AscentPhase.GravityTurn);
        Check("no vessel commands no throttle", c.Throttle == 0.0, c.Throttle.ToString());

        // ---- THE BUTTON IS WHERE IT IS DRAWN, AND ON NOTHING ELSE ---- (see below for the rest)
        float ax, ay, aw, ah;
        Pages.AutoRect(W, H, out ax, out ay, out aw, out ah);
        PageHit got = Pages.HitTest(0, ax + aw * 0.5f, ay + ah * 0.5f, W, H, 0);
        Check("auto button is hit where it is drawn", got.Act == PageAct.ToggleAuto, got.Act.ToString());
        Check("auto button clears the chrome bar", ay + ah < H - ChromeBar.Height,
              "ends at " + (ay + ah));
        // And it must not be sitting on a step, which is exactly what the first placement did.
        for (int i = 0; i < (int)StepId.Count; i++)
        {
            float sx, sy, sw2, sh;
            Pages.StepRect(i, W, H, out sx, out sy, out sw2, out sh);
            bool overlap = ax < sx + sw2 && ax + aw > sx && ay < sy + sh && ay + ah > sy;
            Check("auto button does not cover step " + i, !overlap, "");
        }
    }
}
