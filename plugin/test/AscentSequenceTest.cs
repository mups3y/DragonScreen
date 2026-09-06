// AscentSequenceTest — register T18. The pure ascent EVENT chain: §B8's autostage-off direct control,
// walked end to end on measured state.
//
// ---- WHAT THIS SUITE IS FOR ----
// T18's DONE-when is IN-SIM ("PVG flies to insertion"). This suite covers everything about that which
// is decidable WITHOUT the game, so the flight only has to test what genuinely needs the sim:
//   • the whole pad→ignition→liftoff→MECO→sep→SES-1→SECO→Dragon-sep→nose-cone chain, in order;
//   • that MECO is MEASURED and can never be produced by time alone (`pure/BarEvent.cs`'s rule);
//   • that a booster is NEVER separated while it is still making thrust (flight 194334);
//   • that a pad which fails to light is SAFED with the clamps still holding, and stays safed;
//   • that the mission plan cannot advance out of any step but `Complete`;
//   • that the documented intervals are the DOCUMENTED ones, pinned as literals here.
//
// ⛔ WHAT IT DOES NOT COVER, said plainly. Nothing here actuates. `AscentSequence.Step` returns a
// VALUE; `src/MechConductor.cs` turns that value into an `Actuator` call, and that file needs a Vessel.
// Green here means the DECISIONS are right; it says nothing about a decoupler firing.
//
// ---- ⚠ THE LITERAL PIN (the batch's own rule) ----
// "A suite derived entirely from the value under test cannot notice that value changing — pin any
// chosen number as a literal, once, somewhere that does not read the constant." `DocumentedIntervals`
// below writes 3.0 / 8.0 / 190.0 / 45.0 out by hand, against `docs/CREW_MISSION_TELEMETRY.md`'s tables.
// If somebody "tunes" one of those constants without an owner ruling, this suite says so.
using System;
using DragonScreen;

public static class AscentSequenceTest
{
    static int checks = 0, failures = 0;
    static void Check(bool ok, string what)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL: " + what); } }

    static void Is(AscentDecision d, AscentStep next, AscentAct act, string what)
    {
        Check(d.Next == next && d.Act == act, what + "   (got " + d + ")");
        // Every decision must be able to say why — the same discipline ConductorAction carries.
        Check(!string.IsNullOrEmpty(d.Reason), what + " — carries a reason");
    }

    public static int Run()
    {
        Console.WriteLine("AscentSequenceTest (T18: §B8's direct-control ascent chain — pad to insertion)");
        checks = 0; failures = 0;

        DocumentedIntervals();
        PadAndClamps();
        MecoIsMeasured();
        NeverSepUnderThrust();
        ColdStageAndRelight();
        SecoAndHandback();
        WholeWalk();
        PlanAdvanceGuard();
        BoosterAttachment();
        TargetOrbit();
        Resume();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    // A vehicle on the pad, engines cold, tanks full. Everything else defaults to the documented values.
    static AscentInputs Pad()
    {
        AscentInputs s = AscentInputs.Nominal();
        s.S1MaxThrustN = 7600000.0;   // a Falcon-9-scale number, so the 99% gate is exercised on
        s.S1PropellantFrac = 1.0;       // a realistic magnitude rather than on 1.0 vs 0.99
        return s;
    }

    // A vehicle whose octaweb is at full thrust.
    static AscentInputs Burning()
    {
        AscentInputs s = Pad();
        s.LaunchCommanded = true;
        s.S1LitCount = 9;
        s.S1ThrustN = s.S1MaxThrustN;
        return s;
    }

    // ---- 1. THE DOCUMENTED INTERVALS, WRITTEN OUT BY HAND -------------------------------------
    // ⛔ These four literals are NOT read from the constants they check. That is the whole point: a
    // suite that says `Check(X == AscentInputs.X)` proves nothing about X.
    static void DocumentedIntervals()
    {
        Check(AscentInputs.MecoToSepSeconds == 3.0,
              "MECO->sep is the documented 3 s (CREW_MISSION_TELEMETRY.md §2 'pneumatic pushers, ~3 s "
              + "after MECO'; §5 MECO 0:02:37 -> sep 0:02:40)");
        Check(AscentInputs.SepToSes1Seconds == 8.0,
              "sep->SES-1 is the documented 8 s (§2 'MVac ignition, ~8 s after sep'; §5 0:02:40 -> 0:02:48)");
        Check(AscentInputs.Seco1ToDragonSepSeconds == 190.0,
              "SECO-1->Dragon sep is 190 s, the low end of the published spread (§5 gives 193 s; the "
              + "verified anchors give 191 s Crew-2 and 190 s Crew-6)");
        Check(AscentInputs.DragonSepToNoseSeconds == 45.0,
              "Dragon sep->nosecone is the documented 45 s (§5 0:12:03 -> 0:12:48)");

        // ⭐ AND THE TWO ENGINEERING KNOBS SHIP INERT. If either ever ships with a chosen value, that
        // is a tune, and a tune is T22's with an owner ruling behind it.
        Check(AscentInputs.MecoPropellantFracDefault == 0.0,
              "the MECO propellant fraction ships 0.0 — no early cutoff, run the stage to depletion");
        Check(AscentInputs.SecoBackstopDisabled == double.MaxValue,
              "the SECO periapsis backstop ships DISABLED — an early SECO is worse than a hold");

        AscentInputs n = AscentInputs.Nominal();
        Check(n.MecoToSepS == AscentInputs.MecoToSepSeconds
              && n.SepToSes1S == AscentInputs.SepToSes1Seconds
              && n.Seco1ToDragonSepS == AscentInputs.Seco1ToDragonSepSeconds
              && n.DragonSepToNoseS == AscentInputs.DragonSepToNoseSeconds
              && n.MecoPropellantFrac == AscentInputs.MecoPropellantFracDefault
              && n.SecoBackstopPeriapsisM == AscentInputs.SecoBackstopDisabled,
              "Nominal() seeds every interval from the documented constant, none of them from a literal "
              + "typed twice");
    }

    // ---- 2. THE PAD. The one irreversible action in the whole ascent. --------------------------
    static void PadAndClamps()
    {
        AscentInputs s = Pad();
        Is(AscentSequence.Step(s, AscentStep.Idle), AscentStep.Idle, AscentAct.None,
           "no launch GO -> the pad stays quiet and nothing is commanded");

        s.LaunchCommanded = true;
        Is(AscentSequence.Step(s, AscentStep.Idle), AscentStep.Ignition, AscentAct.IgniteStageOne,
           "launch GO -> octaweb ignition commanded");

        // Thrust still building: hold. ⛔ The clamps are the last thing that can save a bad light.
        s.S1LitCount = 9; s.S1ThrustN = 0.5 * s.S1MaxThrustN; s.SinceStepS = 0.4;
        Is(AscentSequence.Step(s, AscentStep.Ignition), AscentStep.Ignition, AscentAct.None,
           "half thrust -> HOLD the clamps");

        // 98% is not 99%. The gate is a hard threshold and this pins which side of it 98 falls.
        s.S1ThrustN = 0.98 * s.S1MaxThrustN;
        Is(AscentSequence.Step(s, AscentStep.Ignition), AscentStep.Ignition, AscentAct.None,
           "98% of available thrust is NOT enough to release (IgnitionGate.ReleaseThrustFrac = 0.99)");

        s.S1ThrustN = 0.995 * s.S1MaxThrustN;
        Is(AscentSequence.Step(s, AscentStep.Ignition), AscentStep.Liftoff, AscentAct.ReleaseHoldDowns,
           "thrust good -> hold-downs released, and only then");

        // ⛔ THE FAILED LIGHT. Past the hold window with no thrust: safe the engines, keep the clamps.
        AscentInputs bad = Pad();
        bad.LaunchCommanded = true; bad.S1LitCount = 0; bad.S1ThrustN = 0.0;
        bad.SinceStepS = IgnitionGate.MaxHoldS + 0.1;
        Is(AscentSequence.Step(bad, AscentStep.Ignition), AscentStep.Safed, AscentAct.SafeAbort,
           "a light that never made thrust SAFES the stage — and never releases a clamp");

        // ...and stays safed. A pad that failed does not retry itself.
        Is(AscentSequence.Step(Burning(), AscentStep.Safed), AscentStep.Safed, AscentAct.None,
           "SAFED is absorbing — even full thrust on the next tick does not un-safe it");
    }

    // ---- 3. MECO IS MEASURED. This is the check `pure/BarEvent.cs` asked for. ------------------
    static void MecoIsMeasured()
    {
        AscentInputs s = Burning();

        // A full tank at full thrust keeps flying, however long the step has run. THE STOPWATCH TEST:
        // if anybody ever wires MECO to elapsed time, this is what fails.
        s.SinceStepS = 100000.0;
        Is(AscentSequence.Step(s, AscentStep.Liftoff), AscentStep.Liftoff, AscentAct.None,
           "⛔ 100000 s of flight with full tanks does NOT produce MECO — no timer may reach this step");

        // Depletion does.
        s.SinceStepS = 155.0; s.S1PropellantFrac = 0.0;
        Is(AscentSequence.Step(s, AscentStep.Liftoff), AscentStep.Meco, AscentAct.ShutdownStageOne,
           "propellant spent -> MECO");

        // So does flameout, independently of the propellant reading.
        AscentInputs f = Burning();
        f.S1PropellantFrac = 0.4; f.S1Flameout = true;
        Is(AscentSequence.Step(f, AscentStep.Liftoff), AscentStep.Meco, AscentAct.ShutdownStageOne,
           "a flameout with propellant still showing -> MECO (the KSP boolean, not a threshold)");

        // The knob works when a caller sets it — T22's seam, proven rather than assumed.
        AscentInputs early = Burning();
        early.S1PropellantFrac = 0.05; early.MecoPropellantFrac = 0.06;
        Is(AscentSequence.Step(early, AscentStep.Liftoff), AscentStep.Meco, AscentAct.ShutdownStageOne,
           "a caller-raised MECO fraction cuts the stage early — the T22 seam is live");
        early.MecoPropellantFrac = 0.04;
        Is(AscentSequence.Step(early, AscentStep.Liftoff), AscentStep.Liftoff, AscentAct.None,
           "...and below it, the same vehicle keeps flying — the rule reads the caller's number");
    }

    // ---- 4. NEVER SEPARATE A BOOSTER THAT IS STILL PUSHING ------------------------------------
    // `src/Actuator.cs`'s own header: decoupling a still-thrusting booster "made it ram the S2 and push
    // it off course (flight 194334)". This is that lesson, pinned.
    static void NeverSepUnderThrust()
    {
        AscentInputs s = Burning();
        s.S1PropellantFrac = 0.0;

        // Shut command sent, but the engines are still lit and still pushing. Waiting is the answer,
        // and it is the answer NO MATTER HOW LONG the documented lead has been satisfied for.
        s.S1LitCount = 4; s.S1ThrustN = 2000000.0; s.SinceStepS = 60.0;
        Is(AscentSequence.Step(s, AscentStep.Meco), AscentStep.Meco, AscentAct.None,
           "⛔ still-lit engines making 2 MN: NO SEPARATION, 60 s into the step (flight 194334)");

        // Unlit but a residual thrust reading above the epsilon: still not dead.
        s.S1LitCount = 0; s.S1ThrustN = 5000.0;
        Is(AscentSequence.Step(s, AscentStep.Meco), AscentStep.Meco, AscentAct.None,
           "unlit but 5 kN of tail-off is not dead thrust");

        // Dead, but inside the documented 3 s lead: still waiting.
        s.S1ThrustN = 0.0; s.SinceStepS = 2.9;
        Is(AscentSequence.Step(s, AscentStep.Meco), AscentStep.Meco, AscentAct.None,
           "thrust dead but 2.9 s < the documented 3 s sep lead: still waiting");

        // Both conditions met.
        s.SinceStepS = 3.0;
        Is(AscentSequence.Step(s, AscentStep.Meco), AscentStep.Separation, AscentAct.SeparateBooster,
           "thrust dead AND the 3 s lead served -> separation");

        // ⭐ AND THE TWO CONDITIONS ARE AND, NOT OR — the check that catches a "simplification".
        AscentInputs late = Burning();
        late.S1LitCount = 9; late.S1ThrustN = late.S1MaxThrustN; late.SinceStepS = 999.0;
        Check(AscentSequence.Step(late, AscentStep.Meco).Act != AscentAct.SeparateBooster,
              "the lead alone can NEVER authorise a separation — the two conditions are AND");
    }

    // ---- 5. THE COLD STAGE, AND THE RELIGHT THAT HAS TO REPEAT ---------------------------------
    static void ColdStageAndRelight()
    {
        AscentInputs s = AscentInputs.Nominal();

        s.SinceStepS = 7.9;
        Is(AscentSequence.Step(s, AscentStep.Separation), AscentStep.Separation, AscentAct.None,
           "7.9 s after sep: still coasting — §B8's COLD stage, never a hot one");

        s.SinceStepS = 8.0;
        Is(AscentSequence.Step(s, AscentStep.Separation), AscentStep.Ses1, AscentAct.IgniteStageTwo,
           "the documented 8 s served -> SES-1 commanded");

        // ⭐ IT KEEPS COMMANDING UNTIL IT LIGHTS. `Actuator.IgniteSecondStage` is written for exactly
        // this: "the RealFuels settle->light->retry cycle calls this every tick".
        s.S2LitCount = 0; s.S2ThrustN = 0.0; s.SinceStepS = 30.0;
        Is(AscentSequence.Step(s, AscentStep.Ses1), AscentStep.Ses1, AscentAct.IgniteStageTwo,
           "MVac has not lit 30 s in -> the ignition command REPEATS (RealFuels settle/retry)");

        // A lit-but-not-pushing engine is not a light.
        s.S2LitCount = 1; s.S2ThrustN = 0.0;
        Is(AscentSequence.Step(s, AscentStep.Ses1), AscentStep.Ses1, AscentAct.IgniteStageTwo,
           "ignited flag with no thrust is NOT a light — keep commanding");

        s.S2ThrustN = 900000.0;
        Is(AscentSequence.Step(s, AscentStep.Ses1), AscentStep.S2Flight, AscentAct.None,
           "MVac lit and pushing -> second-stage flight");
    }

    // ---- 6. SECO, AND HANDING THE VEHICLE BACK -------------------------------------------------
    static void SecoAndHandback()
    {
        AscentInputs s = AscentInputs.Nominal();
        s.SinceStepS = 400.0;

        Is(AscentSequence.Step(s, AscentStep.S2Flight), AscentStep.S2Flight, AscentAct.None,
           "PVG still running -> keep burning, however long it takes");

        // ⛔ THE BACKSTOP SHIPS DISABLED, and this is what proves it: a periapsis far above any real
        // atmosphere still does not cut the burn while PVG has not finished.
        s.OrbitClosed = true; s.PeriapsisM = 9.0e8;
        Is(AscentSequence.Step(s, AscentStep.S2Flight), AscentStep.S2Flight, AscentAct.None,
           "⛔ a 900 000 km periapsis does NOT trigger SECO — the backstop ships disabled on purpose");

        // A caller who enables it gets it.
        s.SecoBackstopPeriapsisM = 180000.0; s.PeriapsisM = 185000.0;
        Is(AscentSequence.Step(s, AscentStep.S2Flight), AscentStep.Seco1, AscentAct.None,
           "a caller-enabled backstop fires when the orbit is closed above it");
        s.OrbitClosed = false;
        Is(AscentSequence.Step(s, AscentStep.S2Flight), AscentStep.S2Flight, AscentAct.None,
           "...and the backstop needs a CLOSED orbit, not just an altitude");

        // PVG's own word is the signal.
        AscentInputs done = AscentInputs.Nominal();
        done.PvgFinished = true;
        Is(AscentSequence.Step(done, AscentStep.S2Flight), AscentStep.Seco1, AscentAct.None,
           "PVG reports FINISHED -> SECO-1");

        done.SinceStepS = 189.0;
        Is(AscentSequence.Step(done, AscentStep.Seco1), AscentStep.Seco1, AscentAct.None,
           "189 s after SECO: the post-insertion coast is not over");
        done.SinceStepS = 190.0;
        Is(AscentSequence.Step(done, AscentStep.Seco1), AscentStep.DragonSep, AscentAct.SeparateDragon,
           "the documented 190 s served -> Dragon separates from S2");

        done.SinceStepS = 44.0;
        Is(AscentSequence.Step(done, AscentStep.DragonSep), AscentStep.DragonSep, AscentAct.None,
           "44 s after Dragon sep: still backing away");
        done.SinceStepS = 45.0;
        Is(AscentSequence.Step(done, AscentStep.DragonSep), AscentStep.NoseCone, AscentAct.OpenNoseCone,
           "the documented 45 s served -> nose cone open");

        Is(AscentSequence.Step(done, AscentStep.NoseCone), AscentStep.Complete, AscentAct.None,
           "nose cone commanded -> ascent complete");
        Is(AscentSequence.Step(done, AscentStep.Complete), AscentStep.Complete, AscentAct.None,
           "Complete is terminal");
    }

    // ---- 7. THE WHOLE WALK, IN ORDER, ONCE ------------------------------------------------------
    // A step-by-step drive of one nominal ascent. This is the check that would notice a transition
    // wired to the wrong successor — each of the six checks above tests one hop in isolation.
    static void WholeWalk()
    {
        AscentStep at = AscentStep.Idle;
        AscentInputs s = Pad();
        int acts = 0;
        AscentAct[] seen = new AscentAct[16];

        // pad -> ignition
        s.LaunchCommanded = true;
        at = Drive(ref s, at, ref acts, seen);
        // ignition -> liftoff (thrust arrives)
        s.S1LitCount = 9; s.S1ThrustN = s.S1MaxThrustN; s.SinceStepS = 0.6;
        at = Drive(ref s, at, ref acts, seen);
        // liftoff -> MECO (tanks dry)
        s.SinceStepS = 155.0; s.S1PropellantFrac = 0.0;
        at = Drive(ref s, at, ref acts, seen);
        // MECO -> separation (thrust dead + lead)
        s.S1LitCount = 0; s.S1ThrustN = 0.0; s.SinceStepS = 3.1;
        at = Drive(ref s, at, ref acts, seen);
        // separation -> SES-1 (lead)
        s.SinceStepS = 8.1;
        at = Drive(ref s, at, ref acts, seen);
        // SES-1 -> S2 flight (it lights)
        s.S2LitCount = 1; s.S2ThrustN = 900000.0;
        at = Drive(ref s, at, ref acts, seen);
        // S2 flight -> SECO
        s.PvgFinished = true;
        at = Drive(ref s, at, ref acts, seen);
        // SECO -> Dragon sep
        s.SinceStepS = 191.0;
        at = Drive(ref s, at, ref acts, seen);
        // Dragon sep -> nose cone
        s.SinceStepS = 46.0;
        at = Drive(ref s, at, ref acts, seen);
        // nose cone -> complete
        at = Drive(ref s, at, ref acts, seen);

        Check(at == AscentStep.Complete, "the nominal walk ends at Complete (got " + at + ")");
        Check(AscentSequence.CanAdvancePlan(at), "and only then may the mission plan advance");

        // ⭐ THE ACTUATION ORDER IS THE FLIGHT ORDER. Seven commands, in exactly this sequence — and
        // no clamp release before the ignition that earned it.
        AscentAct[] want = {
            AscentAct.IgniteStageOne, AscentAct.ReleaseHoldDowns, AscentAct.ShutdownStageOne,
            AscentAct.SeparateBooster, AscentAct.IgniteStageTwo, AscentAct.SeparateDragon,
            AscentAct.OpenNoseCone
        };
        Check(acts == want.Length, "the nominal walk actuates exactly " + want.Length
              + " commands (got " + acts + ")");
        for (int i = 0; i < want.Length && i < acts; i++)
            Check(seen[i] == want[i], "walk command " + (i + 1) + " is " + want[i] + " (got " + seen[i] + ")");
    }

    // Advance until the step changes, recording any command issued. Bounded so a stuck rule fails the
    // suite rather than hanging the build.
    static AscentStep Drive(ref AscentInputs s, AscentStep at, ref int acts, AscentAct[] seen)
    {
        for (int i = 0; i < 64; i++)
        {
            AscentDecision d = AscentSequence.Step(s, at);
            if (d.Act != AscentAct.None && acts < seen.Length) { seen[acts] = d.Act; acts++; }
            if (d.Next != at) return d.Next;
            if (d.Act == AscentAct.None) return at;   // settled where it is
        }
        Check(false, "Drive did not settle in 64 ticks from " + at);
        return at;
    }

    // ---- 8. NOTHING BUT `Complete` LETS THE MISSION PLAN MOVE ON --------------------------------
    // ⛔ Including `Safed`. A pad that failed to light must not advance the plan into the ascent.
    static void PlanAdvanceGuard()
    {
        AscentStep[] all = (AscentStep[])Enum.GetValues(typeof(AscentStep));
        int advancing = 0;
        for (int i = 0; i < all.Length; i++)
            if (AscentSequence.CanAdvancePlan(all[i])) advancing++;
        Check(advancing == 1, "exactly one ascent step may advance the plan (got " + advancing + ")");
        Check(AscentSequence.CanAdvancePlan(AscentStep.Complete), "and it is Complete");
        Check(!AscentSequence.CanAdvancePlan(AscentStep.Safed),
              "⛔ a SAFED pad does NOT advance the mission plan");
    }

    // ---- 11. RESUMING AN ASCENT ALREADY UNDER WAY ------------------------------------------------
    // ⛔ The property that matters most here is the one that must NEVER happen.
    static void Resume()
    {
        Check(AscentSequence.ResumeFrom(true, true, true, false) == AscentStep.Idle,
              "on the pad -> Idle, the ordinary case");
        Check(AscentSequence.ResumeFrom(false, true, true, false) == AscentStep.Liftoff,
              "flying with the booster still attached -> Liftoff (MECO ahead)");
        Check(AscentSequence.ResumeFrom(false, false, true, true) == AscentStep.S2Flight,
              "booster gone, MVac burning -> S2 flight");
        Check(AscentSequence.ResumeFrom(false, false, true, false) == AscentStep.Ses1,
              "booster gone, MVac cold -> SES-1, so the relight is commanded rather than waited on");
        Check(AscentSequence.ResumeFrom(false, false, false, false) == AscentStep.Complete,
              "no second stage left -> the ascent is over");

        // ⭐ THE ONE THAT MUST NEVER HAPPEN, over EVERY combination of the four inputs: a resume can
        // never re-enter Ignition, because Ignition is the step that releases the hold-downs.
        int ignitions = 0, count = 0;
        for (int i = 0; i < 16; i++)
        {
            AscentStep r = AscentSequence.ResumeFrom((i & 1) != 0, (i & 2) != 0, (i & 4) != 0, (i & 8) != 0);
            count++;
            if (r == AscentStep.Ignition) ignitions++;
        }
        Check(count == 16 && ignitions == 0,
              "⛔ NO combination of the sixteen possible vehicle states resumes into Ignition — a resume "
              + "can never re-light an octaweb or release a clamp (got " + ignitions + ")");
    }

    // ---- 10. THE TARGET ORBIT (§B5's one exception) --------------------------------------------
    // ⛔ The two things this must never do: invent an altitude, and pick an inclination SIGN.
    static void TargetOrbit()
    {
        Check(AscentTargets.IssInsertionAltitudeM == 210000.0,
              "the standard ISS insertion is 210 km — §B11 [DOC/cfg] '~190-210 km x 51.63' and the "
              + "shipped cfg's own DesiredOrbitAltitude = 210000");

        // An ISS-crew row carries no apsides of its own (PeriKm = ApoKm = 0).
        MissionProfile iss = Missions.Resolve("Crew-2");
        Check(iss.Valid && iss.HasRendezvous, "the Crew-2 row resolves and has a rendezvous");
        AscentTarget a = AscentTargets.For(iss, -51.6316);
        Check(!a.FromProfileApsides, "an ISS row does not name its own apsides");
        Check(a.PeriapsisM == 210000.0 && a.ApoapsisM == 210000.0,
              "...so it gets the standard circular insertion, not RO's 145 km default");

        // ⭐ THE SIGN IS THE LOADED ONE, THE MAGNITUDE IS THE MISSION'S. Both directions.
        Check(a.InclinationDeg < 0.0 && Math.Abs(Math.Abs(a.InclinationDeg) - 51.6) < 1e-9,
              "a core loaded with -51.6316 keeps its NEGATIVE sign and takes the mission's 51.6 magnitude "
              + "(got " + a.InclinationDeg + ")");
        AscentTarget b = AscentTargets.For(iss, 0.0);
        Check(b.InclinationDeg > 0.0 && Math.Abs(b.InclinationDeg - 51.6) < 1e-9,
              "an unset (0.0) core defaults POSITIVE — the one value here with no in-repo source, and an "
              + "open owner question on T18's line (got " + b.InclinationDeg + ")");
        AscentTarget c = AscentTargets.For(iss, 28.5);
        Check(c.InclinationDeg > 0.0, "a positively-loaded core keeps its positive sign");

        // ⛔ AND IT NEVER FLIES 0° FROM A 28.6° PAD, which is what RO defaults alone would do.
        Check(Math.Abs(AscentTargets.For(iss, -1.0).InclinationDeg) > 1.0,
              "the resolver can never hand PVG a 0-degree (equatorial) target for an ISS mission");

        // A free-flyer names its own orbit and gets it.
        MissionProfile pd = Missions.Resolve("Polaris Dawn");
        Check(pd.Valid && !pd.HasRendezvous, "the Polaris Dawn row resolves and is a free-flyer");
        AscentTarget f = AscentTargets.For(pd, -1.0);
        Check(f.FromProfileApsides, "a free-flyer names its own apsides");
        Check(f.PeriapsisM == 190000.0 && f.ApoapsisM == 1400000.0,
              "...and gets 190 x 1400 km, not the ISS default (got " + f.PeriapsisM + " x " + f.ApoapsisM + ")");

        // Apsides given the wrong way round are ordered, never flown as given.
        MissionProfile swapped = pd; swapped.PeriKm = 1400; swapped.ApoKm = 190;
        AscentTarget s2 = AscentTargets.For(swapped, 1.0);
        Check(s2.PeriapsisM == 190000.0 && s2.ApoapsisM == 1400000.0,
              "apsides given high-then-low are ordered, not handed to PVG inverted");
    }

    // ---- 9. WHEN THE BOOSTER IS STILL OURS ------------------------------------------------------
    static void BoosterAttachment()
    {
        Check(AscentSequence.BoosterAttached(AscentStep.Idle), "the booster is attached on the pad");
        Check(AscentSequence.BoosterAttached(AscentStep.Liftoff), "...and through first-stage flight");
        Check(AscentSequence.BoosterAttached(AscentStep.Meco), "...and at MECO, before the decoupler fires");
        Check(!AscentSequence.BoosterAttached(AscentStep.Separation),
              "⛔ NOT after separation — from here it is §B16's vessel, not ours");
        Check(!AscentSequence.BoosterAttached(AscentStep.Complete), "...nor at the end of the ascent");
    }
}
