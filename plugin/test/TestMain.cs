/*
 * Headless test runner. Every suite returns a failure count; the process exit code is non-zero if any
 * of them failed, so build.py stops rather than cheerfully reporting "ok" over a broken build.
 *
 * PART B RECOVERY IS UNDER WAY (§B12.8). The autopilot deleted 2026-09-01 comes back in four
 * dependency-ordered waves, and each wave RE-REGISTERS the suites that prove it, below. Wave A (W1)
 * is the collision-free pure support layer; Waves B-D follow. A suite is registered here only once
 * the module it proves is actually in the tree - never ahead of it.
 *
 * ---- S167: A SUITE THAT THROWS MUST NOT TAKE THE REST OF THE RUN WITH IT --------------------
 * Every call below goes through `Suite`, which catches. It did not used to, and the consequence is
 * the reason this file changed: `Run()` returning a failure count is the DESIGNED failure path and
 * reports fine, but an EXCEPTION escaping a suite reached `Main` uncaught, killed the process, and
 * took every suite below it with it.
 *
 * MEASURED, on this tree, 2026-09-06 (S167). A deliberate `NullReferenceException` thrown at the top
 * of `AudioScopeTest.Run` - suite 25 of 55 - produced: 25 suites with counts, ONE suite that printed
 * its banner and no count, and TWENTY-NINE that never ran at all. The output ended in a stack trace
 * and `TESTS FAILED (exit 3762504530)`. Nothing in that report distinguishes "the 29 below are green"
 * from "the 29 below are red", so a real regression could sit under an unrelated crash and the run
 * still looked like it had told you everything.
 *
 * AND IT MISATTRIBUTES A MUTATION KILL, which is how S164 found it. A mutation harness that reads
 * only the exit code cannot tell "the suite under test caught this" from "some suite above it fell
 * over on the way past". S164's mutation Z8 was killed by a suite that was not the one under test.
 *
 * A THROW AND A FAILED CHECK ARE PRINTED DIFFERENTLY ON PURPOSE. They mean different things: a
 * failed `Check` is the suite doing its job, a throw is the suite unable to. The first shows as that
 * suite's own `N failed`; the second shows as a `!! SUITE CRASHED` block naming the suite, and the
 * closing line counts them separately. Both are failures and both make the exit code non-zero.
 *
 * The two halves of the proof, because neither is sufficient alone:
 *   test/HarnessTest.cs     the in-process half - a throw becomes a NAMED failure, control returns
 *   build.py harnesscheck   the process half   - every suite BELOW the crash still runs and reports
 */
using System;

public static class TestMain
{
    // ---- S167 ------------------------------------------------------------------------------------
    // How many suites THREW rather than reporting. Separate from `bad` because a crash and a failed
    // check are different diagnoses, and the closing line says which happened.
    static int crashed;

    /// <summary>
    /// S167: run one suite so that a throw becomes a NAMED failure of that suite instead of the end
    /// of the run. Returns the suite's own failure count, or 1 if it threw.
    ///
    /// The log sink is injected so `HarnessTest` can prove this catches WITHOUT printing a crash
    /// banner into a green build - the alternative was a self-test that cried wolf on every run.
    /// </summary>
    public static int Guard(string name, Func<int> run, Action<string> log)
    {
        try
        {
            return run();
        }
        catch (Exception ex)
        {
            // EVERYTHING IN HERE MUST BE THROW-PROOF. A guard that dies while reporting a death
            // reproduces the exact defect it exists to fix, one frame further out.
            string kind, msg, stack;
            try { kind = ex.GetType().Name; } catch { kind = "Exception"; }
            try { msg = ex.Message; } catch { msg = "(message unavailable)"; }
            try { stack = ex.StackTrace; } catch { stack = null; }
            if (string.IsNullOrEmpty(stack)) stack = "(no stack trace)";
            log("!! SUITE CRASHED  " + name + "  threw " + kind + ": " + msg
                + Environment.NewLine + Indent(stack));
            return 1;
        }
    }

    // S167: the stack trace goes under the CRASH line indented, so a reader scanning a long report
    // can see where one suite's wreckage ends and the next suite's banner begins.
    const char LF = (char)10;

    static string Indent(string text)
    {
        string[] lines = (text ?? "").Split(LF);
        for (int i = 0; i < lines.Length; i++) lines[i] = "     " + lines[i].TrimEnd();
        return string.Join(Environment.NewLine, lines).TrimEnd();
    }

    /// <summary>
    /// S167: the name comes off the delegate's own method, never a hand-typed string - a string here
    /// would be a second copy of the suite name at all 55 call sites, and every copy is a chance for
    /// a rename to leave a lie behind. It is also what makes the NAME in a crash report trustworthy:
    /// it is the throwing suite's own type, so it cannot name a different one.
    /// </summary>
    public static string NameOf(Func<int> run)
    {
        try
        {
            if (run != null && run.Method != null && run.Method.DeclaringType != null)
                return run.Method.DeclaringType.Name;
        }
        catch { }
        return "(unnamed suite)";
    }

    static int Suite(Func<int> run) { return Suite(NameOf(run), run); }

    static int Suite(string name, Func<int> run)
    {
        return Guard(name, run, delegate(string m) { crashed++; Console.WriteLine(m); });
    }

    public static int Main()
    {
        crashed = 0;
        // The SCREEN + shared-display-math suites. The autopilot suites removed on 2026-09-01 return
        // wave by wave underneath them (§B12.8); the ones still missing are the ones whose modules are.
        int bad = 0;

        // ---- S167's fault-injection seam -------------------------------------------------------
        // `build.py harnesscheck` sets this and asserts the run still reports every suite BELOW the
        // crash - the half of S167's done-criteria that no in-process check can reach, because it is
        // a claim about a whole process and so it takes a whole process to prove. Injected FIRST, so
        // that "every later suite" means all of them. Unset in every normal run, and the ONE call
        // site that passes an explicit name: this delegate has no suite class to be named after.
        if (Environment.GetEnvironmentVariable("DRAGONSCREEN_HARNESS_FAULT") == "1")
            bad += Suite("DeliberateFaultInjection",
                         delegate { throw new InvalidOperationException(
                             "S167 fault injection - the run must survive this and report every suite below it"); });

        // S167: HarnessTest FIRST, deliberately. It proves the guard that every other line on this
        // list now depends on, so it runs before any of them - and if the guard itself were broken the
        // wreckage would land here, rather than 30 suites downstream where it reads as someone else's bug.
        bad += Suite(HarnessTest.Run);

        bad += Suite(LayoutTest.Run);
        // ⭐⭐ S240. The `Tri` primitive - the FILLED counterpart of `Line`, added because the locked
        // base-screen design needs two fills no existing DrawKind can make (the border's 45-degree
        // chamfers and the icon window's notch). S239 stopped Prompt 1 at Gate 0 over exactly this.
        // ⛔ Its load-bearing check is `BothRenderersUnpackTheSamePairing`: the two rasterisers are
        // dispatched separately and a mis-pairing would leave one right and the other wrong with
        // nothing to show for it - which is H-01's shape. Pixels are proved separately by
        // `DragonScreenPreview.exe --tricheck`, because no rasteriser is reachable from this build.
        bad += Suite(DisplayListTriTest.Run);   // S240: Tri packing, degeneracy, and the two-renderer pin
        bad += Suite(LayoutSweepTest.Run);
        bad += Suite(PageTest.Run);
        bad += Suite(ComponentsTest.Run);       // Phase 6: pure display widgets (NumericReadout/StatusIndicator/TargetReticle)
        bad += Suite(PanelTest.Run);
        bad += Suite(GlobeProjectionTest.Run);  // screens: orthographic globe projection + occlusion (NAV 3D)
        bad += Suite(NavGlobeLongitudeTest.Run); // screens: the 3D globe's longitude - marker vs texture (S197)
        bad += Suite(PlanetGeomTest.Run);       // screens: scaled-space camera framing/projection/occlusion (S10a)
        bad += Suite(OrbitalTest.Run);          // shared display math: orbit readouts
        bad += Suite(VehiclePartsTest.Run);     // screens: part classification for the systems display
        bad += Suite(MissionPhaseTest.Run);     // shared: the phase enum the screens label
        bad += Suite(StageStatsTest.Run);       // display: per-stage dV/TWR/burn-time readout (KER-mirrored)
        bad += Suite(KerDataTest.Run);          // KER soft-integration: per-stage selection over the mirrored KER sim data
        bad += Suite(FigmaUINavTest.Run);       // new Figma UI: bottom-bar nav + back chevron hit routing
        bad += Suite(TurntableTest.Run);        // screens: the capsule sprite turntable — naming, picker, drag (T11a, §5)
        bad += Suite(TouchWiringTest.Run);      // screens: the touch pass (T14) - chute actions, docking clusters, suit fail branch
        bad += Suite(LogGateTest.Run);          // diagnostics: the seen-set that stops a standing warning flooding KSP.log (S40)
        // S100 (QC H-01): THE INSTRUMENT ITSELF. The preview is what layout, palette and legibility
        // are judged from (CLAUDE.md) and what C1.3 requires before anything is marked DONE, and it
        // was rendering every Figma page at twice the shipped width on the strength of a cfg value
        // the cfg contradicted. This suite reads the cfg and the preview's source and fails if they
        // ever disagree again - the rule this file already had for the FONT, finally written down
        // for RESOLUTION.
        bad += Suite(ScreenSizeTest.Run);

        // 2026-09-06 batch, job 2 (QC R-02 + S117): THE FLOOR ITSELF. Typography.Min is a MEASURED
        // number at a MEASURED WIDTH, and when S115 doubled the shipped panel the number did not
        // move - so every legibility check in the build became twice as permissive, and NavPage,
        // which draws in literal RefPanelW pixels, halved the physical size of a LIVE screen's text.
        // Neither could be caught by a suite that only ever ran at 1280, where the two widths are the
        // same width. Every check in here is a comparison ACROSS widths, which is the only shape that
        // can fail.
        bad += Suite(LegibilityFloorTest.Run);
        bad += Suite(SplitReflowTest.Run);
        // S181 / unit 2a: VrioTestPage is laid out on the Figma export's own coordinates. Every
        // number in that suite is a literal read off the export, never off the page - see its
        // header for why (S176's two edits each had the mattering mutation survive a suite that
        // derived its expectations from the value under test).
        bad += Suite(VrioGeometryTest.Run);
        // S185 / unit 3: VehicleOverviewPage's centre block sits on the page centreline. Same rule
        // as the suite above - every number is a literal, typed from Overview.vue's CSS, the owner's
        // rendered mock and VehicleSubsystemPage's own capsule x, never read off the page.
        bad += Suite(VehicleGeometryTest.Run);
        // S193 / unit 3c: the strip navigates, warns and reads live. The navigation half is a ROUND
        // TRIP - press the icon, follow the NavHit, draw that page, and ask its own strip which tab it
        // underlines - because comparing two mapping arrays only catches a transposition if the test
        // transposes it back by hand.
        bad += Suite(VehicleLiveTest.Run);
        bad += Suite(Frame58MapTest.Run);
        bad += Suite(CoverActsTest.Run);
        bad += Suite(BarEventTest.Run);
        bad += Suite(CoverAlarmTest.Run);
        bad += Suite(Frame58ControlsTest.Run);
        bad += Suite(AlertActivityTest.Run);
        bad += Suite(SettingsTabStripTest.Run);
        bad += Suite(VideoCamRowsTest.Run);
        bad += Suite(AudioScopeTest.Run);
        bad += Suite(CabinLightingTest.Run);   // S134d / QC F-03: Frame 66's LIGHTING panel, rebuilt
        bad += Suite(AudioGridTest.Run);       // S134e / QC A-03 + A-04: the audio panel's grid + its signal glyph      // S134c / QC A-01: the audio page's five scopes    // S134b / QC VV-02: the Video page's camera rows are touchable // S134a / QC F-04: one strip geometry, two projections   // S133 / QC H-05: Frame 58's ALERT ACTIVITY panel // S132 / H11: Frame 58's FRAME, CAMERA and stopwatch      // S130 / H7: the Figma UI's alarm channel finally has a consumer       // S128: what the Cover's four action rows do, and cannot do       // S154a: Frame 58's element geometry (research only - draws nothing)

        // ---- PART B RECOVERY, WAVE A (W1, §B12.8) - the collision-free pure support layer ----
        // Recovered from `8b81816^` with their modules. The fixtures are as they were: ConicTest and
        // LambertTest are RSS (mu = 3.986e14); TrajectoryTest and PredictTest are a STOCK Kerbin fixture
        // DELIBERATELY - they prove the integrator's ARITHMETIC against closed forms, and prove nothing
        // about RSS-RO tuning (R1 §3.5). Do not "fix" a fixture into RSS thinking it validates more.
        bad += Suite(AeroTest.Run);             // L1 derived aero: q, speed of sound, Mach, isothermal density
        bad += Suite(AuthorityTest.Run);        // L1 the vehicle's own control authority (torque / MOI)
        bad += Suite(ConicTest.Run);            // L3 support: Vec3 + universal-variable conic propagation
        bad += Suite(TrajectoryTest.Run);       // §B16 prediction engine: RK4 through-atmosphere, drag MEASURED
        bad += Suite(PredictTest.Run);          // where we will be / hit / pass closest - damped fixed point
        bad += Suite(LambertTest.Run);          // B7 Lambert two-point BVP, self-inverted against our propagator
        bad += Suite(RendezvousMathTest.Run);   // L3 rendezvous: the LVLH frame + Clohessy-Wiltshire targeting
        // S63's guard on the one irreplaceable RSS-RO dataset in the tree. `pure/BoosterDrag.cs` had no
        // suite and NEVER did (a grep of the whole pre-deletion tree at `8b81816^` finds its name in two
        // places: the file, and a prose sentence in `Aero.cs`). Its ten Mach-binned bc values came from
        // 18,080 samples over 48 RSS/RO flights whose raw CSVs were GITIGNORED AND NEVER COMMITTED
        // (R1 §3.5/§4.3, §B16.8 ruling 1) - so a changed digit could not be detected OR re-derived here,
        // and would surface as a landing miss rather than a red build. The suite transcribes the numbers
        // from R1 §3.5's verbatim quotation (a second surviving copy in the repo) rather than reading them
        // back out of the module, which is what makes it a guard and not a tautology.
        // ⚠ It pins the TABLE and the interpolator's SHAPE. It does not validate the curve - R1 is a
        // quotation of the same lost corpus, not an independent measurement - and R1 §3.5 records that the
        // data came from flights that mostly did NOT land, with no after-case for the miss it fixed. Only a
        // recorded RE-FLIGHT converges this (owner decision on R1 Q2), which needs glass time: an owner gate.
        bad += Suite(BoosterDragTest.Run);      // S63: the corpus bc-vs-Mach curve, pinned against R1 §3.5

        // ---- PART B RECOVERY, WAVE B (W2, §B12.8) - the actuation layer (§B12.7 direct part control) ----
        // ActuationTest proves the pure capability->role classifier the restored glue `src/Actuator.cs` acts
        // on, and carries W2's added §B16.4 HARD ASSERTION - read against the REAL `docs/reference/craftdump.csv`
        // on disk, so a wrong-vehicle bind (the Kartoffelkuchen Falcon 9, installed 2026-09-03) is caught
        // headless. ⚠ A MISSING DUMP FAILS this suite deliberately: the assertion is worthless without one.
        // ThrustBalanceTest proves the B3 balancer trio (ThrustBalance + DiffThrottle + RcsBalance), which
        // came back in this wave because `Actuator.BalanceOctawebThrust` / `RcsInducedTorque` will not compile
        // without them (R1 §3.1: "both should be recovered *with* the Actuator").
        // ⚠ Their CONSTANTS are UN-CONVERGED and UNATTRIBUTED (R1 §7.4) and engine-out was NEVER FLOWN
        // (R1 §5.1) - the suites prove the solver's ARITHMETIC, never that any of it is tuned. Each file
        // carries that marking in its own header; do not read a green suite as a validated number.
        bad += Suite(ActuationTest.Run);        // §B12.7 capability->role map + §B16.4's octaweb binding assertion
        bad += Suite(ThrustBalanceTest.Run);    // B3 TCA torque-nulling solver + its engine-out / RCS wrappers

        // ---- PART B RECOVERY, WAVE C (W3, §B12.8) - the booster set (§B16) ----
        // BoosterTest proves the three restored booster modules: the hoverslam ignition solver
        // (pure/Hoverslam.cs), the grid-fin steering law (pure/GridFin.cs) and the recovery FSM
        // (pure/BoosterDescent.cs). OctawebResolveTest proves W3's octaweb BINDER (pure/OctawebResolve.cs)
        // - guard first, then bind the three ModuleEnginesRF BY engineID into a named table, resolved
        // ONCE - and, like ActuationTest, reads the REAL `docs/reference/craftdump.csv` off disk, so a
        // MISSING DUMP FAILS IT DELIBERATELY.
        // ⚠ THE BOOSTER WAS NEVER RECOVERED IN FLIGHT (R1 §4.2) and every constant these suites touch is
        // UN-CONVERGED for RSS-RO with its regime recorded NOWHERE (R1 §7.4, §B16.8). BoosterTest's
        // fixture IS that defect - it carries the wave's only anchors, and they disagree with the only
        // other written set. These are PROPERTY checks: monotonicity, sign, unit-length, the AoA cap, the
        // FSM contract. Green here means the ARITHMETIC is right. It means NOTHING about tuning, and the
        // FSM under test is four phases where §B16.2 specifies five (no boostback state). Each file says
        // so in its own header; read one before trusting a number that came through it.
        bad += Suite(BoosterTest.Run);          // §B16 booster: hoverslam solver + grid-fin steering + the recovery FSM
        bad += Suite(OctawebResolveTest.Run);   // §B16.4 step 2: the octaweb binder, guard-first, against the real dump

        // ---- PART B RECOVERY, W23 (§B16) - the booster HOST: the thing that RUNS the script ----
        // W8 built the five-phase script and recorded that NOTHING CALLED IT. W23 built the caller:
        // pure/BoosterHostPlan.cs (the decisions) + src/BoosterHost.cs (the KSP glue). This suite proves
        // the decision half - WHICH VESSEL (and above all which NOT), WHEN TO STOP, WHETHER A COMMAND MAY
        // GO OUT, and WHICH ENGINE SET a command names. Its sharpest checks are NEGATIVE: the DRAGON is
        // exercised as a candidate from every angle and must never be selected, with each of the three
        // independent exclusions checked ALONE.
        // ⚠ NOTHING FLIES. `BoosterHost.Actuate` is FALSE by default - there is no steering law (register
        // W24), and a booster that lights an engine with an uncontrolled attitude is flight 194334
        // (`8225df7`: "fires thr=1.0 0.3 s after MECO at 'sep 0 km' ... LOST in ~10 s - and its 0-km burn
        // kicks the upper stage"). The suite pins that default. The two hold-off constants it exercises
        // are [UN-CONVERGED] (§B16.8): 194334 gives the FAILING point, never a converged safe value.
        bad += Suite(LandingTargetTest.Run);    // W25: the booster aim point - sourced coords, land-anywhere
        bad += Suite(IgnitionGateTest.Run);     // W5: the clamp-release + ullage gates, RESTORED AS AN OPEN DEFECT
        bad += Suite(BoosterHostTest.Run);      // §B16 booster host: selection, stop, command gate, engine roles
        bad += Suite(AscentProfileTest.Run);    // S219: every ascent setting accounted for + the countdown ordering
        bad += Suite(ConductorEngageTest.Run);  // S219 JOB 3: configured-AND-engaged, the ignition chain, the T-0 staging
        bad += Suite(AutoTargetTest.Run);       // S220: the station is targeted on the pad, and refuses rather than guesses
        // ⭐ S223 (NTSB-2026-001 R-03/R-04). The audit above says what we DECIDED; this suite covers the
        // instrument that says what MechJeb actually HOLDS — the read-back and its expectation table, the
        // ten guidance columns fitted so the next unstable-guidance window is IN THE RECORDING rather
        // than only in a `KSP.log` that gets overwritten, and the manifest's live MechJeb block.
        // ⛔ Its load-bearing check is `SeededDivergence`: perturb one box, assert the verdict goes RED.
        bad += Suite(AscentReadbackTest.Run);   // S223: the audit reads back, and the guidance is recorded
        // ⭐ S227 (owner, 2026-09-08: *"we need to know exactly if or when a part failed and which
        // failed first"*). The recorder had no part channel at all — no `GameEvents` subscription
        // anywhere in the glue, and every `stage.*` event is a COMMANDED action — so a separator that
        // vanished uncommanded produced no record of any kind.
        // ⛔ Its load-bearing check is `StagingDoesNotPromoteToCommanded`: a stage command must NOT
        // license calling a death routine, because staging is the likeliest moment for a real failure.
        // ⭐⭐ And `TheScannerCanFail` proves the Add/Remove scanner rejects a leak — including one
        // whose `.Remove` is only commented out (S220's two surviving mutants).
        bad += Suite(PartLossTest.Run);         // S227: which part went, when, and whether anybody asked
        // ⛔⛔ S228 (NTSB-2026-002). MechJebCore.FixedUpdate clears the module cache and forces
        // OnLoad(null) on its first frame as master-and-focus, 68 ms after Configure() finished — 17
        // boxes reverted, `_autostage` among them, and nothing re-checked. Five stages fired in one
        // frame at 33.9 km; splashdown at ~134 m/s with no parachutes.
        // ⭐⭐ Its load-bearing check is `ScrubRefusesTheIgnition`, and every scrub assertion is paired
        // with a NEGATIVE CONTROL that ignites on the same inputs — the conductor already MEASURED this
        // failure and launched anyway, so "it did not light" has to be proven, not observed.
        bad += Suite(ConfigWipeTest.Run);       // S228: the wipe is detected, the count can scrub, the cascade is floored

        // ---- PART B RECOVERY, W24 (§B16) - the booster STEERING LAW -------------------------------
        // `docs/BOOSTER_STEERING_MOD_SEARCH.md` (C1.15) could neither rule TCA in nor out; the owner ruled
        // (via the overseer, 2026-09-04): OURS, TCA's METHOD borrowed, no dependency (Q1), and a marked,
        // [UN-CONVERGED], DEFAULT-ZERO deadband seam (Q2). `pure/BoosterSteer.cs` is that law. It is
        // written against the ACTUAL failure `docs/FLIGHT_CORPUS_ASSESSMENT.md` §3 found — a DIVERGENCE
        // (an unbounded commanded rate), not the limit cycle the inherited folklore blamed — by making the
        // outer angle-to-rate stage structurally incapable of demanding more than a fixed ceiling.
        // ⚠ NO BYTE of `AttitudePilot.cs`/`AttitudeController.cs`/`pure/AttitudeLoop.cs` is here (R1 §3.2:
        // RECOVER-REFERENCE ONLY, owner directive) — only the documented frame-conversion FORMULA is
        // reused, in the glue (`src/BoosterHost.cs`), per R1's own list of what those files are reference
        // FOR. Every gain is [UN-CONVERGED] (§B16.8 ruling 2) and the per-axis SIGN is UNVERIFIED — this
        // law has no recorded flight of its own. `BoosterHost.Actuate` flips to TRUE with this task, per
        // the owner's ruling on W23's Q1: the next flight is the first time this commands a real vessel.
        bad += Suite(BoosterSteerTest.Run);     // W24: the steering law - rate ceiling, deadband seam, bounds

        // ---- PART B RECOVERY, W6 (§B16, R1-tagged but in NO §B12.8 wave) - the B8 impact divert ----
        // pure/CourseCorrect.cs is the layer between the two above: it turns a predicted-impact ERROR
        // (BoosterDescent.ErrorTo, over pure/Trajectory.cs) into the control change that nulls it - a 2x2
        // finite-difference Jacobian for the booster's down/cross grid-fin steer, and a 1x1 Newton step for
        // the capsule entry range channel. R1 §5.1 gives it RECOVER-CODE - §B16 but no wave named it, so it
        // is its own register line (W6) rather than a quiet passenger in someone else's diff.
        // ⚠ NEVER FLOWN (R1 §5.1: "❌ NO"; e90a63f: "no lifting-entry flight in the corpus"). Its three
        // constants are UN-CONVERGED for RSS-RO (§B16.8 ruling 2) and this suite's fixtures are ANALYTIC -
        // a known linear impact model, chosen because it has an exact closed-form divert. Green proves the
        // linear algebra recovers that answer, the damping leaves exactly its residual, and the solve
        // REFUSES rather than diverting on noise when the Jacobian is unobservable or rank-deficient. It
        // proves nothing about a tuned number. The file's header says so.
        bad += Suite(CourseCorrectTest.Run);    // B8 impact-point divert: the 2x2 Jacobian solve + the 1x1 Newton step

        // ---- PART B RECOVERY, WAVE E-3 (W15, §B12.8 rider (c)) - the safe-water splashdown selector ----
        // pure/SafeLandingSite.cs picks WHICH point on the sampled ground track a returning capsule aims at:
        // the nearest OPEN WATER inside the reachable entry-glide window, else -1 so the glue coasts a step.
        // Its glue half (`src/LandingSiteScan.cs`, restored with it) does the body sampling and carries the
        // F4 water gate - `TerrainAltitude(lat, lon, true)`, the three-arg overload that returns the real
        // negative seabed height; the default two-arg call CLAMPS ocean depth to 0, so an RSS scan reads
        // ZERO water and never commits. That fix is the reason the file exists (R1 §5.2: regime RSS).
        // ⚠ NOTHING CALLS EITHER FILE YET. Both intended callers are absent - `AbortControl` is W19 and
        // `ReturnControl` (W18) is now RECOVER-REFERENCE and lands no code - so the module is restored
        // DORMANT, for them to consume. This suite is the only thing exercising it.
        // ⚠ NEVER FLOWN (R1 §5.1/§5.2: flown ❌ NO). The glide window it selects against is [UN-CONVERGED]
        // for RSS-RO (§B16.8 ruling 2), marked on `src/LandingSiteScan.cs` which supplies the band; the pure
        // selector defines no constant at all. Three of the checks are RECOVERED VERBATIM from the deleted
        // `test/FdirTest.cs:152-164` (the rest of that suite stays deleted - it is `AbortResponder`/`Fdir`
        // coverage and neither type is in the tree). The fixtures are ANALYTIC. Green proves the selector
        // picks the right sample; it proves nothing about the window being the right window.
        bad += Suite(SafeLandingSiteTest.Run);  // W15: nearest safe water inside the reachable glide window

        // ---- PART B RECOVERY, WAVE D (W4, §B12.8) - the PURE conductor set ----
        // The mission-conductor decision layer: ModeManager (the mission plan + the phase sequencer),
        // CrewGate (the crew-in-the-loop GATE state machine), CrewGates (the real G1..G15 catalog),
        // MissionProfile (mission-as-data, resolved from the VAB craft name), WarpPlan (the never-overshoot
        // time-warp rule) and CoastEta (a range-closing coast's ETA, so a long chase can be warped).
        // CrewGateTest is the one suite that covers CrewGate + CrewGates + ModeManager together.
        // ⚠ NOTHING HERE FLIES ANYTHING, and that is still true now that the glue has landed. **W10
        // (2026-09-05) restored `src/CrewProcedureOps.cs` and its host `src/FlightDriver.cs`**, so this pure
        // layer finally HAS a caller: the conductor's gates are live, the crew's GO advances the plan, and
        // the host reports phase/engaged. The host is READ-ONLY (§B12.6 step (3)) - it commands nothing, and
        // it holds at the first Fly step because no controller exists to complete a phase. `src/
        // ⭐ W9 (2026-09-07) landed `src/MissionConductor.cs` too, so WarpPlan and CoastEta finally have a
        // compiled consumer and `RecoveryPlan` (new, below) has its live one. That changes what these
        // suites cover and NOT what the vehicle does: the conductor sets a time-warp rate and a physics
        // range, and nothing else - no throttle, no attitude, no staging, no ignition. So these suites
        // still prove DECISIONS, not a flown mission; every flight command on every screen is still
        // §14.4(a)'s honest no-op. CrewGates' gate TITLES and CHECKLIST ITEMS are §1.4 source-of-truth material
        // (transcribed NASA/SpaceX callouts) - do not edit one to make a test pass.
        bad += Suite(MissionProfileTest.Run);   // L-S0b mission-as-data: the 19-mission catalog + craft-name resolve
        bad += Suite(CrewGateTest.Run);         // L4 crew gate machine + the real gate catalog + the phase sequencer
        bad += Suite(ConductorWalkTest.Run);    // W10: the gate WALK the restored glue composes, and the AutoAdvanceGates runaway
        bad += Suite(ConductorTest.Run);        // T16: the pure ConductorAction core — §B12.3's phase table, §B12.4's re-plan rule

        // ---- PART B, THE CONDUCTOR INCREMENTS (T18 onward) ----
        // T18: §B8's autostage-off DIRECT-CONTROL ascent chain — the half MechJeb does not do. PVG
        // steers and throttles; the conductor ignites, releases the clamps, cuts, separates, relights,
        // separates the Dragon and opens the nose cone. Every transition here is driven by MEASURED
        // state or by a documented interval after a measured event; ⛔ none of them reads MET, which is
        // `pure/BarEvent.cs`'s standing rule and is pinned by a 100000-second check in the suite.
        bad += Suite(AscentSequenceTest.Run);
        bad += Suite(PvgPreflightTest.Run);
        // S215: the LAUNCH WINDOW. The plane-crossing geometry mirrored from the vendored `Astro`, the
        // Q2 inclination-agreement check, the Q4 no-target refusal, and the countdown hold that keeps
        // the octaweb dark until T-0 minus the documented 3 s lead. ⛔ Its cases are built so that a
        // window calculation which IGNORED THE LAN — the failure that produces a perfect ascent into
        // the wrong plane — fails them; see the suite's own header for why that shape was necessary.
        bad += Suite(LaunchWindowTest.Run);

        // T18-T21: ONE MISSION, WALKED END TO END, CLOSED-LOOP. The gates advance the plan, the plan hands
        // a phase to `Conductor.Decide`, the decision hands work to that phase's sequencer, the sequencer
        // commands a fixture VEHICLE, and the vehicle answers back. ⭐ A chain of individually correct
        // decisions can still fail to TERMINATE, and a flight is an expensive place to find that out.
        // ⚠ A CONTRACT test over the pure layer, not an execution of the glue - see its header.
        // T19: §B11's approach geometry, §B10.2's op parameters and §B12.4's numbers - plus the half
        // that can only be proved against the PINNED MECHJEB TREE: that each `ConductorOp` names a class
        // that EXISTS, and that each operation's DEFAULT `TimeReference` is the one §B9/§B10.2 asks for.
        // ⛔ The conductor cannot SET a TimeSelector (private static readonly in every Operation), so it
        // relies on those defaults; a re-pin that reorders one would silently move a rendezvous burn.
        bad += Suite(RendezvousOpsTest.Run);

        // T20: §B10.3's speedLimit ladder (⚠ "the single most important docking knob"), §B11's hard
        // "< 0.2 m/s inside 5 m" rule checked as a property of the WHOLE ladder, and the one thing the
        // phase enum cannot answer - which of `ModeManager`'s TWO `Fly(Docked)` steps we are standing on.
        bad += Suite(DockingLadderTest.Run);

        // T21: the return leg - undock, back away, trunk, deorbit, entry attitude (O8), chutes, splash.
        // ⛔ The four checks that would cost the vehicle: nothing on the return acts while the hooks
        // are still closed, the chute gates need altitude AND DESCENT, a resume can never land on a step
        // that fires hardware, and the drogue/main altitudes are read from `Mission` rather than copied.
        bad += Suite(ReturnSequenceTest.Run);

        // S213: "4.100 Mission Sequence" - the conductor's ONLY entry point on the glass, and the
        // page whose absence let a whole batch ship an autopilot nobody could engage. Most of the
        // suite is REACHABILITY, because that is the check whose absence was the defect.
        bad += Suite(CrewGatePageTest.Run);

        bad += Suite(MissionWalkTest.Run);
        bad += Suite(WarpPlanTest.Run);         // conductor: the on-rails rate that can never overshoot the drop-out
        bad += Suite(CoastEtaTest.Run);         // conductor: range-closing coast ETA -> the warp target UT
        bad += Suite(RecoveryPlanTest.Run);     // W9: §B16.7's physics-range lifecycle - wide before sep, always restored

        // ---- PART B0 (BB1, §B0) - the BlackBox flight recorder core ----
        // ⭐ THE ONE LINE THAT MUST BE REMOVED IF THE RECORDER IS EXCISED FOR RELEASE. BB1 is
        // excisable by design (owner, 2026-09-03): delete `src/pure/blackbox/`, `src/BlackBoxRecorder.cs`,
        // `test/BlackBoxTest.cs`, `plugin/tools/assess_flight.py`, and this line. Nothing else in the tree
        // names it - the dependency arrow points one way, BlackBox -> tree, and never back.
        // ⚠ S96 (2026-09-05): `plugin/tools/assess_flight.py` parses `BlackBoxSchema.cs` to build its
        // fixture (`build.py`'s `tool_tests()` runs it as part of `test`) and cannot survive the schema's
        // deletion - S85 proved this by running the excision and hitting a red `SELFTEST FAILED` on the
        // python tool before this line named it.
        // ⚠ This suite proves the PURE half only: the schema, RFC-4180 + invariant formatting, §4.6's
        // blank-never-zero validity, §2.0's rate ladder and warp floor, the R0 accumulators, the warp
        // void, the manifest, and the ghost-column coverage check S76's finding demanded. It proves
        // NOTHING about the glue reading the right KSP field into the right column - that is
        // `src/BlackBoxRecorder.cs`, it needs a Vessel, and it is confirmed on the glass by **BB4**.
        bad += Suite(BlackBoxTest.Run);       // BB1: recorder core - schema, validity, rates, manifest, coverage
        // ⚠ S85's suite is NOT part of the excision above. `pure/CrewControlIds.cs`, `pure/CrewPressLog.cs`
        // and this line stay when the recorder goes: the press buffer is SCREEN-side, the two choke
        // points write into it, and it is the reason `ScreenPainter.cs`/`PanelButtons.cs` still compile
        // with `pure/blackbox/` and `BlackBoxRecorder.cs` deleted. Verified by physical removal (S85).
        bad += Suite(CrewPressTest.Run);      // S85: the CVR press channel - the control_id namespace + buffer

        // T15b: the embedded MechJeb's HOST rules, checked against the pinned tree itself - the
        // blacklist's substring behaviour (and its collateral), the three [KSPAddon]s staying out
        // of the compile, and the shipped tune. Nothing here loads a core; that needs the game.
        // S79: the Vehicle Overview's MARGIN column and the pure `Depletion` core under it. The
        // column was one literal on eight rows; this proves it is computed, and pins the two rows that
        // dash BY DESIGN for most of a mission in both directions - dash while coasting, countdown
        // during a burn - which is the only shape that can tell a computed dash from a printed one.
        // S154b / QC H-02: Frame 58's six attitude readouts, drawn live OVER their baked ink. The
        // sharp check is the PATCH COUNT on a DEAD feed - a version that returns early when there is
        // no value leaves the baked number showing, so the page prints a confident attitude at the
        // exact moment it has none.
        bad += Suite(Frame58AttitudeTest.Run);

        bad += Suite(MarginColumnTest.Run);

        bad += Suite(MechHostTest.Run);

        // S167: a crash is counted and NAMED separately from a failed check. A reader who sees only
        // "N SUITE(S) FAILED" cannot tell an unhealthy harness from an unhealthy build.
        Console.WriteLine(bad == 0 ? "ALL SUITES PASSED"
                          : crashed == 0 ? bad + " SUITE(S) FAILED"
                          : bad + " SUITE(S) FAILED, " + crashed + " OF THEM BY THROWING (see SUITE CRASHED above)");
        return bad == 0 ? 0 : 1;
    }
}
