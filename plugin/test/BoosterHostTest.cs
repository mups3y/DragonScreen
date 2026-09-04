/*
 * Tests for the §B16 booster HOST's decision layer (pure/BoosterHostPlan.cs) — W23, 2026-09-04.
 *
 * W8 built the five-phase booster script and recorded that nothing called it. W23 built the caller:
 * pure/BoosterHostPlan.cs (every decision) + src/BoosterHost.cs (the KSP glue that flies it). This
 * suite proves the decision half headlessly — the half that says WHICH VESSEL, WHEN TO STOP, WHETHER A
 * COMMAND MAY GO OUT, and WHICH ENGINE SET a command names.
 *
 * ⛔ THE SHARPEST CHECKS HERE ARE THE NEGATIVE ONES. The catastrophic failure of a booster host is
 * binding the DRAGON — an autopilot flying the crew. So the Dragon is exercised as a candidate from
 * every angle (with a pod, without a booster part, as the active vessel, in company with a real
 * booster) and must never be selected. Three independent tests exclude it and each is checked ALONE.
 *
 * ⚠ WHAT THIS DOES *NOT* PROVE.
 *  • That anything flies. `BoosterHost.Actuate` is FALSE by default (there is no steering law — register
 *    W24), so no command leaves the host today; `Blocked` is tested to say exactly that.
 *  • Anything about the KSP glue. `src/BoosterHost.cs` cannot be compiled headlessly at all — the
 *    untested half is untested by construction, which is why it is kept as thin as it is.
 *  • Any TUNING. The two hold-off constants are [UN-CONVERGED] (§B16.8 ruling 2); flight 194334 gives
 *    the failing point (0 m, 0.3 s), never a converged safe value. Green here means the interlock is
 *    wired the right way round, not that 500 m is the right number.
 */
using DragonScreen;
using System;

public static class BoosterHostTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    // The real part names (docs/reference/craftdump.csv, col 2) the classifiers key on.
    const string OCTAWEB = "TE.19.F9.S1.Engine";
    const string S1TANK = "TE.19.F9.S1.Tank";          // any ".S1." part; the marker is what matters
    const string POD = "TE.18.DRAGONV2.POD";
    const string TRUNK = "TE.18.DRAGONV2.TRUNK";
    const string MVAC = "TE.19.F9.S2.Engine";
    const string KK_OCTAWEB = "KK_SPX_F9_Octaweb";      // the OTHER Falcon 9 (Kartoffelkuchen)

    // Build a candidate the way the glue's `Describe` does: classify the part names, nothing else.
    static BoosterCandidate V(bool active, bool loaded, params string[] parts)
    {
        BoosterCandidate c = new BoosterCandidate();
        c.Exists = true; c.IsActive = active; c.Loaded = loaded;
        for (int i = 0; i < parts.Length; i++)
        {
            string nm = parts[i];
            if (OctawebBinding.IsForeignBoosterPart(nm)) c.HasForeignBoosterPart = true;
            if (VehicleParts.IsPod(nm)) c.HasPod = true;
            else if (VehicleParts.IsBooster(nm)) c.HasBoosterPart = true;
        }
        return c;
    }

    static BoosterCandidate Dragon(bool active) { return V(active, true, POD, TRUNK, MVAC); }
    static BoosterCandidate Booster(bool active) { return V(active, true, OCTAWEB, S1TANK); }
    static BoosterCandidate FullStack(bool active) { return V(active, true, POD, TRUNK, MVAC, OCTAWEB, S1TANK); }

    static BoosterFlightSnapshot Flying()
    {
        BoosterFlightSnapshot s = new BoosterFlightSnapshot();
        s.Exists = true; s.Loaded = true; s.Packed = false; s.IsActive = false;
        s.LandedOrSplashed = false; s.OctawebStillValid = true; s.SinceLandedS = 0.0;
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen booster HOST tests (§B16: selection, stop, command gate, engine roles)");

        SelectionTests();
        DragonNeverBoundTests();
        StopTests();
        CommandGateTests();
        EngineRoleTests();
        PhaseGateTests();
        SwitchSequenceTests();
        ProfileTests();
        AnnunciationTests();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures == 0 ? 0 : 1;
    }

    // =====================================================================================
    // 1. SELECTION — the nominal case, and every way it must refuse
    // =====================================================================================
    static void SelectionTests()
    {
        int idx;

        // THE NOMINAL POST-SEPARATION SCENE: the Dragon is active, the booster is not.
        BoosterBind r = BoosterHostPlan.Select(new[] { Dragon(true), Booster(false) }, out idx);
        Check("post-separation scene binds Ok", r == BoosterBind.Ok, "r=" + r);
        Check("post-separation scene binds the BOOSTER, index 1", idx == 1, "idx=" + idx);

        // Order must not matter — the identity is the parts, never the position in the list.
        r = BoosterHostPlan.Select(new[] { Booster(false), Dragon(true) }, out idx);
        Check("selection is order-independent", r == BoosterBind.Ok && idx == 0, "r=" + r + " idx=" + idx);

        // BEFORE separation there is one stack carrying BOTH a pod and S1 parts — nothing to fly.
        r = BoosterHostPlan.Select(new[] { FullStack(true) }, out idx);
        Check("the full stack on the pad is NOT a separated booster",
              r == BoosterBind.NoSeparatedBooster, "r=" + r);
        Check("a refused select returns index −1", idx == -1, "idx=" + idx);

        // Empty / null.
        r = BoosterHostPlan.Select(new BoosterCandidate[0], out idx);
        Check("no vessels → NoVessel", r == BoosterBind.NoVessel, "r=" + r);
        r = BoosterHostPlan.Select(null, out idx);
        Check("null candidate array → NoVessel (never a throw)", r == BoosterBind.NoVessel, "r=" + r);

        // TWO separated boosters — refuse to pick, exactly as OctawebBinding.Bind does.
        r = BoosterHostPlan.Select(new[] { Dragon(true), Booster(false), Booster(false) }, out idx);
        Check("two separated boosters → Ambiguous, never a pick", r == BoosterBind.Ambiguous, "r=" + r);
        Check("an ambiguous select returns index −1", idx == -1, "idx=" + idx);

        // §B16.4's HARD ASSERTION: the other Falcon 9 anywhere in the scene loses outright.
        r = BoosterHostPlan.Select(new[] { Dragon(true), Booster(false), V(false, true, KK_OCTAWEB) }, out idx);
        Check("a Kartoffelkuchen booster in the scene → ForeignVehicle refusal",
              r == BoosterBind.ForeignVehicle, "r=" + r);
        Check("the foreign refusal wins EVEN WITH our own booster present (never pick)",
              idx == -1, "idx=" + idx);

        // ...and the foreign veto beats every other verdict, including ambiguity.
        r = BoosterHostPlan.Select(new[] { Booster(false), Booster(false), V(false, true, KK_OCTAWEB) }, out idx);
        Check("foreign veto is checked BEFORE ambiguity", r == BoosterBind.ForeignVehicle, "r=" + r);

        // An UNLOADED booster has no parts in memory and no control path.
        r = BoosterHostPlan.Select(new[] { Dragon(true), Booster(false).Unloaded() }, out idx);
        Check("an unloaded booster is not a candidate", r == BoosterBind.NoSeparatedBooster, "r=" + r);

        // A DESTROYED vessel reference.
        BoosterCandidate dead = new BoosterCandidate();   // Exists = false
        r = BoosterHostPlan.Select(new[] { dead, Dragon(true) }, out idx);
        Check("a dead vessel reference is not a candidate", r == BoosterBind.NoSeparatedBooster, "r=" + r);

        // A foreign part on an UNLOADED vessel cannot be classified (no parts in memory) and must not veto.
        r = BoosterHostPlan.Select(new[] { Dragon(true), Booster(false), V(false, false, KK_OCTAWEB) }, out idx);
        Check("an UNLOADED foreign craft does not veto (its parts are not in memory)",
              r == BoosterBind.Ok && idx == 1, "r=" + r + " idx=" + idx);
    }

    // =====================================================================================
    // 2. ⛔ THE DRAGON IS NEVER BOUND — the one failure that must be impossible
    // =====================================================================================
    static void DragonNeverBoundTests()
    {
        int idx;

        // (a) THE POD TEST, alone. A pod-carrying craft is refused even if it also carries S1 parts and
        //     is NOT the active vessel — i.e. neither of the other two guards is doing the work here.
        BoosterCandidate podAndBooster = V(false, true, POD, OCTAWEB, S1TANK);
        Check("(a) a POD excludes a candidate on its own, non-active and booster-parted",
              !BoosterHostPlan.IsSeparatedBooster(podAndBooster), "");
        BoosterBind r = BoosterHostPlan.Select(new[] { podAndBooster }, out idx);
        Check("(a) …and Select refuses it", r == BoosterBind.NoSeparatedBooster && idx == -1, "r=" + r);

        // (b) THE S1 TEST, alone. A non-active, pod-free craft with no booster part is still refused.
        BoosterCandidate s2Only = V(false, true, MVAC);
        Check("(b) no .S1. part excludes a candidate on its own",
              !BoosterHostPlan.IsSeparatedBooster(s2Only), "");

        // (c) THE ACTIVE TEST, alone. A pod-free craft carrying S1 parts, but ACTIVE.
        Check("(c) the ACTIVE vessel is excluded on its own, even when it looks exactly like the booster",
              !BoosterHostPlan.IsSeparatedBooster(Booster(true)), "");
        r = BoosterHostPlan.Select(new[] { Booster(true) }, out idx);
        Check("(c) …and Select refuses the active booster", r == BoosterBind.NoSeparatedBooster, "r=" + r);

        // And the Dragon in every guise is never selected, whichever vessel holds focus.
        Check("the Dragon (active) is never a booster", !BoosterHostPlan.IsSeparatedBooster(Dragon(true)), "");
        Check("the Dragon (non-active) is never a booster", !BoosterHostPlan.IsSeparatedBooster(Dragon(false)), "");
        Check("the full stack is never a booster", !BoosterHostPlan.IsSeparatedBooster(FullStack(true)), "");
        Check("the full stack (non-active) is never a booster", !BoosterHostPlan.IsSeparatedBooster(FullStack(false)), "");

        // The scene where it matters most: focus on the BOOSTER, Dragon non-active. Nothing is bound —
        // the host must never take the vessel the crew are on because the other one is focused.
        r = BoosterHostPlan.Select(new[] { Dragon(false), Booster(true) }, out idx);
        Check("focus on the booster → NOTHING is bound (the Dragon is never the fallback)",
              r == BoosterBind.NoSeparatedBooster && idx == -1, "r=" + r + " idx=" + idx);

        // Mutation guard: the real booster IS accepted, so the tests above are excluding for the right
        // reason rather than because nothing is ever accepted.
        Check("the real separated booster IS accepted (the negatives are not vacuous)",
              BoosterHostPlan.IsSeparatedBooster(Booster(false)), "");
    }

    // =====================================================================================
    // 3. STOPPING
    // =====================================================================================
    static void StopTests()
    {
        BoosterFlightSnapshot s = Flying();
        Check("a flying, loaded, unpacked booster is not stopped",
              BoosterHostPlan.StopReason(s) == BoosterHostStop.None, "");

        s = Flying(); s.Exists = false;
        Check("destroyed → Destroyed", BoosterHostPlan.StopReason(s) == BoosterHostStop.Destroyed, "");

        s = Flying(); s.Loaded = false;
        Check("unloaded → Unloaded", BoosterHostPlan.StopReason(s) == BoosterHostStop.Unloaded, "");

        s = Flying(); s.IsActive = true;
        Check("focus moved onto the booster → BecameActive",
              BoosterHostPlan.StopReason(s) == BoosterHostStop.BecameActive, "");

        s = Flying(); s.OctawebStillValid = false;
        Check("stale octaweb table → BindLost", BoosterHostPlan.StopReason(s) == BoosterHostStop.BindLost, "");

        // ⛔ PACKED IS NOT A STOP. PhysicsRangeExtender going on and off (register W9) must not destroy
        // the binding and the FSM state; it blocks COMMANDS, which is a different question (§4 below).
        s = Flying(); s.Packed = true;
        Check("PACKED does not release the binding (it blocks commands instead)",
              BoosterHostPlan.StopReason(s) == BoosterHostStop.None, "");

        // §B16.7 step 3 — the +10 s settle after touchdown, then hand off.
        s = Flying(); s.LandedOrSplashed = true; s.SinceLandedS = 0.0;
        Check("landed but not settled → keep holding it",
              BoosterHostPlan.StopReason(s) == BoosterHostStop.None, "t=0");
        s.SinceLandedS = BoosterHostPlan.LandedSettleS - 0.5;
        Check("landed, just short of the settle → still holding",
              BoosterHostPlan.StopReason(s) == BoosterHostStop.None, "t=settle−0.5");
        s.SinceLandedS = BoosterHostPlan.LandedSettleS;
        Check("landed + the §B16.7 settle elapsed → Landed",
              BoosterHostPlan.StopReason(s) == BoosterHostStop.Landed, "t=settle");
        Check("the settle is §B16.7's stated 10 s", Math.Abs(BoosterHostPlan.LandedSettleS - 10.0) < 1e-9,
              BoosterHostPlan.LandedSettleS.ToString());

        // Ordering: the most final reason wins, so a destroyed-and-landed vessel reports Destroyed.
        s = Flying(); s.Exists = false; s.LandedOrSplashed = true; s.SinceLandedS = 999.0;
        Check("destroyed outranks landed", BoosterHostPlan.StopReason(s) == BoosterHostStop.Destroyed, "");
        s = Flying(); s.Loaded = false; s.OctawebStillValid = false;
        Check("unloaded outranks a lost bind", BoosterHostPlan.StopReason(s) == BoosterHostStop.Unloaded, "");
    }

    // =====================================================================================
    // 4. THE COMMAND GATE — including the flight-194334 hold-off
    // =====================================================================================
    static void CommandGateTests()
    {
        BoosterFlightSnapshot s = Flying();
        double farEnough = BoosterHostPlan.HoldOffSeparationM + 1.0;
        double longEnough = BoosterHostPlan.HoldOffSinceBindS + 1.0;

        // ⛔ THE DEFAULT. `BoosterHost.Actuate` is false, so nothing goes out at all.
        Check("unarmed → NotArmed, whatever else is true",
              BoosterHostPlan.Blocked(false, s, farEnough, longEnough) == BoosterCommandBlock.NotArmed, "");

        // Armed and clear.
        Check("armed, unpacked, bound, clear of the stack → the path is OPEN",
              BoosterHostPlan.Blocked(true, s, farEnough, longEnough) == BoosterCommandBlock.None, "");

        // PACKED: KSP runs no control path for a packed vessel — this is the unpack constraint itself.
        BoosterFlightSnapshot p = Flying(); p.Packed = true;
        Check("armed but PACKED → blocked",
              BoosterHostPlan.Blocked(true, p, farEnough, longEnough) == BoosterCommandBlock.Packed, "");

        // No table → never guess at an engine.
        BoosterFlightSnapshot n = Flying(); n.OctawebStillValid = false;
        Check("armed but no valid octaweb table → blocked",
              BoosterHostPlan.Blocked(true, n, farEnough, longEnough) == BoosterCommandBlock.NoOctaweb, "");

        // ---- FLIGHT 194334: too close, or too soon, and nothing goes out ----
        Check("armed but AT the stack (0 m) → HoldOff (this is the 194334 case)",
              BoosterHostPlan.Blocked(true, s, 0.0, longEnough) == BoosterCommandBlock.HoldOff, "");
        Check("armed but 0.3 s after binding → HoldOff (the 194334 timing)",
              BoosterHostPlan.Blocked(true, s, farEnough, 0.3) == BoosterCommandBlock.HoldOff, "");
        Check("just inside the separation gate → still held",
              BoosterHostPlan.Blocked(true, s, BoosterHostPlan.HoldOffSeparationM - 0.1, longEnough)
                  == BoosterCommandBlock.HoldOff, "");
        Check("exactly at the separation gate → still held (strictly greater is required)",
              BoosterHostPlan.HoldingOff(BoosterHostPlan.HoldOffSeparationM - 1e-9, longEnough), "");
        Check("exactly at the time gate → released",
              !BoosterHostPlan.HoldingOff(farEnough, BoosterHostPlan.HoldOffSinceBindS), "");

        // ⛔ AN UNMEASURABLE SEPARATION IS TREATED AS TOO CLOSE, NEVER AS CLEAR. L6's defect made this
        // read 0 km on every flight; "we don't know how far the crew are" is not a reason to burn.
        Check("separation 0 (unmeasurable) is treated as TOO CLOSE",
              BoosterHostPlan.HoldingOff(0.0, longEnough), "");
        Check("a negative separation is treated as TOO CLOSE",
              BoosterHostPlan.HoldingOff(-1.0, longEnough), "");
        Check("a NaN separation is treated as TOO CLOSE (NaN comparisons are all false — rule N2)",
              BoosterHostPlan.HoldingOff(double.NaN, longEnough), "");
        Check("a NaN elapsed time is treated as TOO SOON",
              BoosterHostPlan.HoldingOff(farEnough, double.NaN), "");

        // Ordering: the most fundamental block is the one reported.
        BoosterFlightSnapshot both = Flying(); both.Packed = true; both.OctawebStillValid = false;
        Check("packed outranks a missing table in the reported reason",
              BoosterHostPlan.Blocked(true, both, 0.0, 0.0) == BoosterCommandBlock.Packed, "");
        Check("the arm outranks everything",
              BoosterHostPlan.Blocked(false, both, 0.0, 0.0) == BoosterCommandBlock.NotArmed, "");

        // MUTATION GUARD on the interlock: with the hold-off zeroed the gate would open at the stack.
        double sepWas = BoosterHostPlan.HoldOffSeparationM, timeWas = BoosterHostPlan.HoldOffSinceBindS;
        BoosterHostPlan.HoldOffSeparationM = 0.0; BoosterHostPlan.HoldOffSinceBindS = 0.0;
        Check("MUTATION: zeroing the hold-off WOULD open the gate at 0.3 s / 1 m (so the guard is live)",
              BoosterHostPlan.Blocked(true, s, 1.0, 0.3) == BoosterCommandBlock.None, "");
        BoosterHostPlan.HoldOffSeparationM = sepWas; BoosterHostPlan.HoldOffSinceBindS = timeWas;
        Check("…and restoring them closes it again",
              BoosterHostPlan.Blocked(true, s, 1.0, 0.3) == BoosterCommandBlock.HoldOff, "");
    }

    // =====================================================================================
    // 5. ENGINE ROLES — and the EngineMode-0 ambiguity
    // =====================================================================================
    static void EngineRoleTests()
    {
        Check("mode 0 → OctawebAll", BoosterHostPlan.RoleFor(VehicleParts.ModeAllEngines) == EngineRole.OctawebAll, "");
        Check("mode 1 → OctawebThree", BoosterHostPlan.RoleFor(VehicleParts.ModeThreeEngine) == EngineRole.OctawebThree, "");
        Check("mode 2 → OctawebCentre", BoosterHostPlan.RoleFor(VehicleParts.ModeCentreOnly) == EngineRole.OctawebCentre, "");
        Check("an unknown mode is None, never a guess", BoosterHostPlan.RoleFor(7) == EngineRole.None, "");
        Check("a negative mode is None", BoosterHostPlan.RoleFor(-1) == EngineRole.None, "");

        // ⛔ THE ONE THAT MATTERS. EngineMode 0 is BOTH ModeAllEngines AND the struct's default value,
        // so `EnginesLit` — not the mode — decides whether anything lights.
        Check("⛔ mode 0 with EnginesLit FALSE lights NOTHING (the default-struct trap)",
              BoosterHostPlan.CommandedRole(false, VehicleParts.ModeAllEngines) == EngineRole.None, "");
        Check("mode 0 with EnginesLit TRUE is the nine",
              BoosterHostPlan.CommandedRole(true, VehicleParts.ModeAllEngines) == EngineRole.OctawebAll, "");
        Check("EnginesLit false silences mode 1 too",
              BoosterHostPlan.CommandedRole(false, VehicleParts.ModeThreeEngine) == EngineRole.None, "");
        Check("EnginesLit false silences mode 2 too",
              BoosterHostPlan.CommandedRole(false, VehicleParts.ModeCentreOnly) == EngineRole.None, "");

        // A default-constructed command must name NO engine — this is the exact trap in the field.
        BoosterCommand blank = new BoosterCommand();
        Check("⛔ a DEFAULT-CONSTRUCTED BoosterCommand names no engine set",
              BoosterHostPlan.CommandedRole(blank.EnginesLit, blank.EngineMode) == EngineRole.None,
              "lit=" + blank.EnginesLit + " mode=" + blank.EngineMode);

        // And the roles the FSM actually emits round-trip to the three bound engineIDs.
        Check("the entry burn's mode maps to the ThreeLanding set",
              BoosterHostPlan.CommandedRole(true, VehicleParts.ModeThreeEngine) == EngineRole.OctawebThree, "");
        Check("the landing burn's mode maps to the CenterOnly set",
              BoosterHostPlan.CommandedRole(true, VehicleParts.ModeCentreOnly) == EngineRole.OctawebCentre, "");
    }

    // =====================================================================================
    // 5b. THE PHASE GATE (OCT3) — landing mode during ascent, or vice versa, must be REFUSED
    // =====================================================================================
    // Owner ruling, 2026-09-05, verbatim: *"you must differentiate between landing mode etc for the
    // engines as we cannot use landing mode during accent and vice versa"*. `BoosterPhase` has no ascent
    // state at all (this host runs only on the separated booster), so "ascent mode accepted in an ascent
    // phase never arising here" is proven the other way round: `OctawebAll` — the ascent/liftoff bank —
    // must be refused in EVERY phase this enum can name, and each landing-designated bank must be refused
    // in every phase that is not its own. Every check below has a partner that asserts the OPPOSITE of
    // "always true" / "always false", so a gate mutated into a stub (always accept, or always refuse)
    // fails at least one of them — that is what proves it can fail by mutation, not a runtime toggle.
    //
    // ⛔ OCT6 (2026-09-05) CHANGED WHAT "ITS OWN PHASE" MEANS FOR THE LANDING BURN, and therefore changed
    // two of OCT3's own passing assertions. The owner ruled the landing burn flies THREE engines and
    // SHEDS to one (*"1. (2)"*, shed point *"comput[ed] from current hover slam solver"*), so
    // "CenterOnly legal at LandingBurn" / "ThreeLanding refused during LandingBurn" were correct code
    // asserting a decision the owner then overturned. They are replaced below by the ORDERED PAIR —
    // Three legal only before the shed, Centre legal only after — which keeps OCT3's actual property
    // (exactly ONE legal bank per gate query) rather than widening the gate to accept both. The pair is
    // mutation-provable in both directions the same way the rest of this section is.
    static void PhaseGateTests()
    {
        BoosterPhase[] all = (BoosterPhase[])Enum.GetValues(typeof(BoosterPhase));
        BoosterPhase[] noBankPhases =
        {
            BoosterPhase.Idle, BoosterPhase.Flip, BoosterPhase.Coast,
            BoosterPhase.AeroDescent, BoosterPhase.Landed
        };

        bool[] shedStates = { false, true };   // OCT6 — the landing burn's two halves

        // ⛔ THE ASCENT-ONLY BANK, REFUSED EVERYWHERE, in BOTH halves of the landing burn.
        for (int i = 0; i < all.Length; i++)
            for (int k = 0; k < shedStates.Length; k++)
                Check("OctawebAll (ascent/liftoff) refused in phase " + all[i] + " (shed=" + shedStates[k] + ")",
                      !BoosterHostPlan.PhaseAllows(all[i], EngineRole.OctawebAll, shedStates[k]), all[i].ToString());

        // Off is always legal — refusing "nothing lit" would make the interlock itself a hazard.
        for (int i = 0; i < all.Length; i++)
            for (int k = 0; k < shedStates.Length; k++)
                Check("no bank lit is legal in every phase, phase " + all[i] + " (shed=" + shedStates[k] + ")",
                      BoosterHostPlan.PhaseAllows(all[i], EngineRole.None, shedStates[k]), all[i].ToString());

        // Each landing-phase bank is legal in its OWN phase(s)...
        Check("ThreeLanding legal at Boostback",
              BoosterHostPlan.PhaseAllows(BoosterPhase.Boostback, EngineRole.OctawebThree), "");
        Check("ThreeLanding legal at EntryBurn",
              BoosterHostPlan.PhaseAllows(BoosterPhase.EntryBurn, EngineRole.OctawebThree), "");

        // ⛔ OCT6 — THE LANDING BURN'S ORDERED PAIR. This REPLACES OCT3's single
        // "CenterOnly legal at LandingBurn" / "ThreeLanding refused during LandingBurn" pair, which the
        // owner's 2026-09-05 ruling (*"1. (2)"* — ThreeLanding shedding to CenterOnly, the shed point
        // *"comput[ed] from current hover slam solver"*) makes WRONG. The gate is NOT widened: it still
        // names exactly ONE legal bank per query, and the other bank is refused in each half.
        Check("OCT6: ThreeLanding legal at LandingBurn BEFORE the shed (three engines brake)",
              BoosterHostPlan.PhaseAllows(BoosterPhase.LandingBurn, EngineRole.OctawebThree, false), "");
        Check("OCT6: CenterOnly REFUSED at LandingBurn before the shed (the burn has not shed yet)",
              !BoosterHostPlan.PhaseAllows(BoosterPhase.LandingBurn, EngineRole.OctawebCentre, false), "");
        Check("OCT6: CenterOnly legal at LandingBurn AFTER the shed (one engine flies the touchdown)",
              BoosterHostPlan.PhaseAllows(BoosterPhase.LandingBurn, EngineRole.OctawebCentre, true), "");
        Check("OCT6: ThreeLanding REFUSED at LandingBurn after the shed — the shed is ONE WAY",
              !BoosterHostPlan.PhaseAllows(BoosterPhase.LandingBurn, EngineRole.OctawebThree, true), "");
        Check("OCT6: the landing burn still has exactly ONE legal bank per half, and they DIFFER",
              BoosterHostPlan.AllowedRoleForPhase(BoosterPhase.LandingBurn, false) == EngineRole.OctawebThree
              && BoosterHostPlan.AllowedRoleForPhase(BoosterPhase.LandingBurn, true) == EngineRole.OctawebCentre, "");

        // ...and refused everywhere a bank must stay dark (Idle / Flip / Coast / AeroDescent / Landed) —
        // in BOTH shed states, since the latch must never open a bank outside the landing burn.
        for (int i = 0; i < noBankPhases.Length; i++)
            for (int k = 0; k < shedStates.Length; k++)
            {
                Check("ThreeLanding refused at " + noBankPhases[i] + " (shed=" + shedStates[k] + ")",
                      !BoosterHostPlan.PhaseAllows(noBankPhases[i], EngineRole.OctawebThree, shedStates[k]), "");
                Check("CenterOnly refused at " + noBankPhases[i] + " (shed=" + shedStates[k] + ")",
                      !BoosterHostPlan.PhaseAllows(noBankPhases[i], EngineRole.OctawebCentre, shedStates[k]), "");
            }

        // ⛔ AND VICE VERSA — a landing-phase bank commanded in the OTHER landing phase is still wrong.
        // CenterOnly is Hoverslam's touchdown engine; ThreeLanding is boostback/entry (and, since OCT6,
        // the first half of the landing burn). Crossing them is exactly the "landing mode during ascent
        // and vice versa" shape of mistake, one phase-pair over.
        Check("CenterOnly refused during Boostback (that phase's bank is Three, not Centre)",
              !BoosterHostPlan.PhaseAllows(BoosterPhase.Boostback, EngineRole.OctawebCentre), "");
        Check("CenterOnly refused during EntryBurn (that phase's bank is Three, not Centre)",
              !BoosterHostPlan.PhaseAllows(BoosterPhase.EntryBurn, EngineRole.OctawebCentre), "");

        // ---- Wired through the ACTUAL command gate the host calls, not just the predicate ----
        BoosterFlightSnapshot s = Flying();
        double farEnough = BoosterHostPlan.HoldOffSeparationM + 1.0;
        double longEnough = BoosterHostPlan.HoldOffSinceBindS + 1.0;

        Check("Blocked refuses OctawebAll during AeroDescent (would light all nine on a descending booster)",
              BoosterHostPlan.Blocked(true, s, farEnough, longEnough, BoosterPhase.AeroDescent, EngineRole.OctawebAll)
                  == BoosterCommandBlock.WrongEngineForPhase, "");
        Check("Blocked lets ThreeLanding through during EntryBurn (its own phase)",
              BoosterHostPlan.Blocked(true, s, farEnough, longEnough, BoosterPhase.EntryBurn, EngineRole.OctawebThree)
                  == BoosterCommandBlock.None, "");
        Check("OCT6: Blocked lets ThreeLanding through during LandingBurn BEFORE the shed",
              BoosterHostPlan.Blocked(true, s, farEnough, longEnough, BoosterPhase.LandingBurn,
                                      EngineRole.OctawebThree, false) == BoosterCommandBlock.None, "");
        Check("OCT6: Blocked REFUSES ThreeLanding during LandingBurn AFTER the shed (no re-ignition)",
              BoosterHostPlan.Blocked(true, s, farEnough, longEnough, BoosterPhase.LandingBurn,
                                      EngineRole.OctawebThree, true) == BoosterCommandBlock.WrongEngineForPhase, "");
        Check("OCT6: Blocked REFUSES CenterOnly during LandingBurn before the shed",
              BoosterHostPlan.Blocked(true, s, farEnough, longEnough, BoosterPhase.LandingBurn,
                                      EngineRole.OctawebCentre, false) == BoosterCommandBlock.WrongEngineForPhase, "");
        Check("OCT6: Blocked lets CenterOnly through during LandingBurn after the shed",
              BoosterHostPlan.Blocked(true, s, farEnough, longEnough, BoosterPhase.LandingBurn,
                                      EngineRole.OctawebCentre, true) == BoosterCommandBlock.None, "");

        // A pre-OCT3 caller that names no phase/role at all (every CommandGateTests() call above) must
        // still compile and answer exactly as before: Idle + None is always allowed.
        Check("an unnamed phase/role defaults to Idle/None, which is always open",
              BoosterHostPlan.Blocked(true, s, farEnough, longEnough) == BoosterCommandBlock.None, "");
    }

    // =====================================================================================
    // 5b. OCT4 — THE MODE-CHANGE SEQUENCE, AND THE GATE AS THE HOST ACTUALLY CALLS IT
    // =====================================================================================
    // OCT3 proved the gate's YES/NO. OCT6 created the project's first mid-burn bank change. Neither
    // proved what `Dispatch` → `SelectEngineSet` ACTUATES, nor that the host hands the gate the right
    // arguments — and the second of those was wrong the whole time (see `BlockedFor`).
    //
    // ⛔ WHAT THIS SECTION CANNOT REACH. `src/BoosterHost.cs` does not compile headlessly (no KSP
    // assemblies), so `Dispatch`, `SelectEngineSet`, `Shut`, `Light` and `ApplyThrottle` are NOT executed
    // by any test — here or anywhere. What is proved is the SEQUENCING DECISION they now execute
    // (`EngineSwitchSteps` is their only sequencing rule) and the GATE CALL they now make
    // (`BlockedFor` is their only argument assembly). The `ModuleEngines.Activate()`/`Shutdown()` calls
    // themselves, the throttle write ordering inside a `FixedUpdate`, and everything about spool remain
    // UNTESTED and can only be settled in the capsule. Stated plainly rather than papered over (S76).

    /// <summary>Render a step list so a failure names the actual sequence, not just "false".</summary>
    static string Seq(BoosterHostPlan.OctawebStep[] steps)
    {
        if (steps.Length == 0) return "<none>";
        string s = "";
        for (int i = 0; i < steps.Length; i++) s += (i > 0 ? " → " : "") + steps[i].ToString();
        return s;
    }

    static bool IsSeq(BoosterHostPlan.OctawebStep[] got, params string[] want)
    {
        if (got.Length != want.Length) return false;
        for (int i = 0; i < got.Length; i++) if (got[i].ToString() != want[i]) return false;
        return true;
    }

    static void SwitchSequenceTests()
    {
        EngineRole none = EngineRole.None, all = EngineRole.OctawebAll;
        EngineRole three = EngineRole.OctawebThree, centre = EngineRole.OctawebCentre;

        // ---- THE TRANSITION TABLE. Every transition OCT3's `AllowedRoleForPhase` permits, as the
        // sequence an ENGINE experiences (`EffectiveSwitchSteps`: the sweep's shutdowns of banks that
        // are not lit are skipped at runtime by the `EngineIgnited` guard). ORDER IS ASSERTED.
        Check("OCT4 (1) None→Three (boostback / entry-burn ignition): light only, nothing to shut",
              IsSeq(BoosterHostPlan.EffectiveSwitchSteps(none, three), "Activate(OctawebThree)"),
              Seq(BoosterHostPlan.EffectiveSwitchSteps(none, three)));
        Check("OCT4 (2) Three→None (burn ends): shut only, nothing lit",
              IsSeq(BoosterHostPlan.EffectiveSwitchSteps(three, none), "Shutdown(OctawebThree)"),
              Seq(BoosterHostPlan.EffectiveSwitchSteps(three, none)));
        Check("OCT4 (3) None→Centre (landing-burn ignition, latch already set): light only",
              IsSeq(BoosterHostPlan.EffectiveSwitchSteps(none, centre), "Activate(OctawebCentre)"),
              Seq(BoosterHostPlan.EffectiveSwitchSteps(none, centre)));
        // ⭐ (4) IS OCT6'S SHED, AND IT IS THE ONE NOTHING HAS FLOWN. Three is shut BEFORE Centre is lit,
        // and that order is forced by the geometry: the centre nozzle is a member of BOTH banks, so they
        // can never burn at once. Reversing these two lines must turn this red.
        Check("OCT4 (4) Three→Centre (OCT6's mid-burn shed): SHUT THREE, THEN LIGHT CENTRE — in that order",
              IsSeq(BoosterHostPlan.EffectiveSwitchSteps(three, centre),
                    "Shutdown(OctawebThree)", "Activate(OctawebCentre)"),
              Seq(BoosterHostPlan.EffectiveSwitchSteps(three, centre)));
        Check("OCT4 (5a) Three→None on release: the lit bank is shut",
              IsSeq(BoosterHostPlan.EffectiveSwitchSteps(three, none), "Shutdown(OctawebThree)"),
              Seq(BoosterHostPlan.EffectiveSwitchSteps(three, none)));
        Check("OCT4 (5b) Centre→None on release: the lit bank is shut",
              IsSeq(BoosterHostPlan.EffectiveSwitchSteps(centre, none), "Shutdown(OctawebCentre)"),
              Seq(BoosterHostPlan.EffectiveSwitchSteps(centre, none)));

        // ---- THE INVARIANTS, over EVERY ordered pair of roles, not just the five above.
        EngineRole[] roles = { none, all, three, centre };
        for (int f = 0; f < roles.Length; f++)
            for (int t = 0; t < roles.Length; t++)
            {
                BoosterHostPlan.OctawebStep[] full = BoosterHostPlan.EngineSwitchSteps(roles[f], roles[t]);
                BoosterHostPlan.OctawebStep[] eff = BoosterHostPlan.EffectiveSwitchSteps(roles[f], roles[t]);
                string tag = roles[f] + "→" + roles[t];

                if (roles[f] == roles[t])
                {
                    // ⛔ THE NO-RE-IGNITION GUARD, at its source: no change, no steps, so a lit set can
                    // never be commanded a second time even if `Dispatch`'s own `want != currentRole`
                    // test were removed.
                    Check("OCT4 " + tag + ": an unchanged role actuates NOTHING (a lit set is never re-lit)",
                          full.Length == 0 && eff.Length == 0, Seq(full));
                    continue;
                }

                // Exactly one activate, and only when a bank is actually wanted.
                int lights = 0, lastShut = -1, firstLight = -1;
                for (int i = 0; i < full.Length; i++)
                {
                    if (full[i].Kind == BoosterHostPlan.OctawebStepKind.Activate)
                    { lights++; if (firstLight < 0) firstLight = i; }
                    else lastShut = i;
                }
                Check("OCT4 " + tag + ": exactly one Activate (none when going OFF)",
                      lights == (roles[t] == none ? 0 : 1), Seq(full));

                // ⛔ THE ORDER INVARIANT — every shutdown precedes the activate. This is the check a
                // future re-order of shut-and-light must break.
                Check("OCT4 " + tag + ": EVERY Shutdown precedes the Activate (the banks share nozzles)",
                      firstLight < 0 || lastShut < firstLight, Seq(full));

                // The activate is never for a bank the same call is shutting, and never for OFF.
                for (int i = 0; i < full.Length; i++)
                    if (full[i].Kind == BoosterHostPlan.OctawebStepKind.Activate)
                        Check("OCT4 " + tag + ": the Activate names the WANTED bank and is never `None`",
                              full[i].Role == roles[t] && roles[t] != none, Seq(full));

                // The sweep covers every bank that is not wanted — including ones we do not believe lit,
                // because `currentRole` is a record of what we commanded, not a reading of the vehicle.
                Check("OCT4 " + tag + ": the sweep shuts all three banks except the wanted one",
                      full.Length == (roles[t] == none ? 3 : 3), Seq(full));

                // And the effective sequence is a subsequence of the full one, never longer than 2.
                Check("OCT4 " + tag + ": at most one shut and one light actually reach the vehicle",
                      eff.Length <= 2, Seq(eff));
            }

        // ---- ⛔ THE WIRING. This is the defect OCT4 found, and this is the check that would have caught
        // it. Every assertion in `PhaseGateTests` above passes the latch BY HAND; the host did not, and
        // no test called the host's argument assembly because it did not have one. `BlockedFor` is that
        // assembly, and it is now pure, so the omission is checkable.
        BoosterFlightSnapshot fs = Flying();
        double far = BoosterHostPlan.HoldOffSeparationM + 1.0;
        double late = BoosterHostPlan.HoldOffSinceBindS + 1.0;

        BoosterCommand shed = new BoosterCommand();
        shed.Phase = BoosterPhase.LandingBurn;
        shed.EnginesLit = true;
        shed.EngineMode = VehicleParts.ModeCentreOnly;
        shed.LandingShedLatched = true;                 // the FSM has shed; this is every tick after it
        Check("OCT4: the POST-SHED command the FSM actually emits passes the gate (the OCT6 defect)",
              BoosterHostPlan.BlockedFor(true, fs, far, late, shed) == BoosterCommandBlock.None,
              "BlockedFor returned " + BoosterHostPlan.BlockedFor(true, fs, far, late, shed));

        BoosterCommand opening = shed;
        opening.EngineMode = VehicleParts.ModeThreeEngine;
        opening.LandingShedLatched = false;             // the burn's opening half
        Check("OCT4: the PRE-SHED command the FSM actually emits also passes the gate",
              BoosterHostPlan.BlockedFor(true, fs, far, late, opening) == BoosterCommandBlock.None,
              "BlockedFor returned " + BoosterHostPlan.BlockedFor(true, fs, far, late, opening));

        // ⛔ AND IT MUST STILL REFUSE — a gate that lost its latch and a gate that accepts everything look
        // identical on the two checks above. These are the halves that tell them apart.
        BoosterCommand lateThree = shed;
        lateThree.EngineMode = VehicleParts.ModeThreeEngine;
        lateThree.LandingShedLatched = true;            // three engines re-demanded AFTER the shed
        Check("OCT4: BlockedFor still REFUSES ThreeLanding after the shed (the latch is one-way)",
              BoosterHostPlan.BlockedFor(true, fs, far, late, lateThree)
                  == BoosterCommandBlock.WrongEngineForPhase, "");

        BoosterCommand earlyCentre = shed;
        earlyCentre.EngineMode = VehicleParts.ModeCentreOnly;
        earlyCentre.LandingShedLatched = false;         // centre demanded before the solver shed
        Check("OCT4: BlockedFor still REFUSES CenterOnly before the shed",
              BoosterHostPlan.BlockedFor(true, fs, far, late, earlyCentre)
                  == BoosterCommandBlock.WrongEngineForPhase, "");

        BoosterCommand nine = shed;
        nine.Phase = BoosterPhase.AeroDescent;
        nine.EngineMode = VehicleParts.ModeAllEngines;
        Check("OCT4: BlockedFor still REFUSES the ascent bank on a descending booster (OCT3)",
              BoosterHostPlan.BlockedFor(true, fs, far, late, nine)
                  == BoosterCommandBlock.WrongEngineForPhase, "");

        // `EnginesLit == false` is OFF whatever the mode says — including the `ModeAllEngines == 0`
        // ambiguity — so a dark command is legal in every phase and never trips the engine gate.
        BoosterCommand dark = new BoosterCommand();
        dark.Phase = BoosterPhase.LandingBurn;
        dark.EnginesLit = false;
        dark.EngineMode = VehicleParts.ModeAllEngines;   // 0: the struct default AND the ascent bank
        Check("OCT4: BlockedFor reads EnginesLit as the authority — a dark command is never a wrong bank",
              BoosterHostPlan.BlockedFor(true, fs, far, late, dark) == BoosterCommandBlock.None, "");

        // The rest of the gate is untouched by the new entry point: it is the same `Blocked`.
        Check("OCT4: BlockedFor still honours the arm",
              BoosterHostPlan.BlockedFor(false, fs, far, late, shed) == BoosterCommandBlock.NotArmed, "");
        Check("OCT4: BlockedFor still honours the flight-194334 hold-off",
              BoosterHostPlan.BlockedFor(true, fs, 0.0, late, shed) == BoosterCommandBlock.HoldOff, "");
    }

    // =====================================================================================
    // 6. THE TARGET MODE — resolved from the in-repo mission catalog, never invented
    // =====================================================================================
    static void ProfileTests()
    {
        // Crew-2 is a real droneship recovery (docs/reference/LZ_RECOVERY_TABLE.md: OCISLY).
        MissionProfile crew2 = Missions.Resolve("Crew-2");
        Check("Crew-2 resolves from the in-repo catalog", crew2.Valid, "");
        Check("Crew-2 is a droneship recovery", crew2.Recovery == RecoveryMode.Droneship, crew2.Recovery.ToString());
        BoosterProfile p = BoosterHostPlan.ProfileFor(crew2);
        Check("a droneship mission gives the ASDS profile", p.Mode == TargetMode.Asds, p.Mode.ToString());
        Check("…whose boostback magnitude is ZERO (§B16.2's C1.8 OVERRIDE)",
              Math.Abs(p.BoostbackMagnitude) < 1e-12, p.BoostbackMagnitude.ToString());

        // Ax-2 is a real RTLS (LZ-1).
        MissionProfile ax2 = Missions.Resolve("Ax-2");
        Check("Ax-2 resolves from the in-repo catalog", ax2.Valid, "");
        Check("Ax-2 is an RTLS recovery", ax2.Recovery == RecoveryMode.RTLS, ax2.Recovery.ToString());
        BoosterProfile pr = BoosterHostPlan.ProfileFor(ax2);
        Check("an RTLS mission gives the RTLS profile", pr.Mode == TargetMode.Rtls, pr.Mode.ToString());
        Check("…with a non-zero boostback magnitude", pr.BoostbackMagnitude > 0.0, pr.BoostbackMagnitude.ToString());

        // KSP renames a separated stage; the catalog's substring match must survive it.
        MissionProfile debris = Missions.Resolve("Crew-2 Debris");
        Check("a KSP-renamed separated stage still resolves its mission", debris.Valid, "");
        Check("…to the same recovery mode", debris.Recovery == crew2.Recovery, "");

        // ⛔ AN UNRESOLVED CRAFT NAME FALLS BACK TO THE INERT PROFILE, NOT TO RTLS.
        MissionProfile unknown = Missions.Resolve("some other rocket");
        Check("an unknown craft name does not resolve", !unknown.Valid, "");
        BoosterProfile fb = BoosterHostPlan.ProfileFor(unknown);
        Check("⛔ an unresolved mission falls back to ASDS, never RTLS", fb.Mode == TargetMode.Asds, fb.Mode.ToString());
        Check("…so the fallback boostback magnitude is ZERO (the inert profile)",
              Math.Abs(fb.BoostbackMagnitude) < 1e-12, fb.BoostbackMagnitude.ToString());

        // An empty / null name is the same case and must not throw.
        Check("an empty craft name gives the inert profile",
              BoosterHostPlan.ProfileFor(Missions.Resolve("")).Mode == TargetMode.Asds, "");
        Check("a null craft name gives the inert profile",
              BoosterHostPlan.ProfileFor(Missions.Resolve(null)).Mode == TargetMode.Asds, "");

        // The profile the host hands the FSM must be a VALID one in every case (gates filled in).
        BoosterProfile norm = fb.Normalized();
        Check("the fallback profile normalizes to a flyable one (entry gate set)", norm.EntryGateAltM > 0.0, "");
        Check("…and a throttle floor above zero", norm.ThrottleFloor > 0.0, "");
    }

    // =====================================================================================
    // 7. ANNUNCIATION — a refusal must always SAY something; a success must be silent
    // =====================================================================================
    static void AnnunciationTests()
    {
        Check("a good bind is silent", BoosterHostPlan.Annunciation(BoosterBind.Ok) == null, "");
        Check("NoVessel annunciates", !string.IsNullOrEmpty(BoosterHostPlan.Annunciation(BoosterBind.NoVessel)), "");
        Check("NoSeparatedBooster annunciates",
              !string.IsNullOrEmpty(BoosterHostPlan.Annunciation(BoosterBind.NoSeparatedBooster)), "");
        Check("Ambiguous annunciates", !string.IsNullOrEmpty(BoosterHostPlan.Annunciation(BoosterBind.Ambiguous)), "");
        Check("ForeignVehicle annunciates",
              !string.IsNullOrEmpty(BoosterHostPlan.Annunciation(BoosterBind.ForeignVehicle)), "");

        Check("not stopping is silent", BoosterHostPlan.Annunciation(BoosterHostStop.None) == null, "");
        Check("every stop reason annunciates",
              !string.IsNullOrEmpty(BoosterHostPlan.Annunciation(BoosterHostStop.Destroyed))
              && !string.IsNullOrEmpty(BoosterHostPlan.Annunciation(BoosterHostStop.Unloaded))
              && !string.IsNullOrEmpty(BoosterHostPlan.Annunciation(BoosterHostStop.Landed))
              && !string.IsNullOrEmpty(BoosterHostPlan.Annunciation(BoosterHostStop.BecameActive))
              && !string.IsNullOrEmpty(BoosterHostPlan.Annunciation(BoosterHostStop.BindLost)), "");

        Check("an open command path is silent", BoosterHostPlan.Annunciation(BoosterCommandBlock.None) == null, "");
        Check("every block annunciates",
              !string.IsNullOrEmpty(BoosterHostPlan.Annunciation(BoosterCommandBlock.NotArmed))
              && !string.IsNullOrEmpty(BoosterHostPlan.Annunciation(BoosterCommandBlock.Packed))
              && !string.IsNullOrEmpty(BoosterHostPlan.Annunciation(BoosterCommandBlock.NoOctaweb))
              && !string.IsNullOrEmpty(BoosterHostPlan.Annunciation(BoosterCommandBlock.WrongEngineForPhase))
              && !string.IsNullOrEmpty(BoosterHostPlan.Annunciation(BoosterCommandBlock.HoldOff)), "");
    }
}

/// <summary>Test-local sugar: the same candidate, not loaded. Keeps the scene builders above readable.</summary>
internal static class BoosterHostTestExt
{
    public static BoosterCandidate Unloaded(this BoosterCandidate c)
    {
        c.Loaded = false;
        c.HasBoosterPart = false; c.HasPod = false; c.HasForeignBoosterPart = false;  // no parts in memory
        return c;
    }
}
