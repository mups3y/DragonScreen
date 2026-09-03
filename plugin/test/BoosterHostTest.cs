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
