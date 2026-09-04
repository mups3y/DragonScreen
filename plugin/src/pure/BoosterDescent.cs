// DragonScreen — BoosterDescent  (§B16 booster recovery: the FIVE-PHASE return-flight FSM)
// ============================================================================================
// SEPARATION → BOOSTBACK → COAST → ENTRY BURN → AERO DESCENT → LANDING BURN → LANDED, as ONE guidance
// with a TARGET MODE (Rtls / Asds). §B16.2's five flying phases, with the pre-boostback FLIP kept as the
// attitude manoeuvre that enters phase 1 (`BOOSTER_GUIDANCE_METHOD.md` §4.1 — F9I flips *before* it
// boostbacks, on both profiles, so the slew is a state, not an instant).
//
// ⛔ THE CONTRACT — three guarantees, held in EVERY phase, test-guarded one phase at a time:
//   (1) `Guide()` ALWAYS returns a DEFINITE UNIT `AimForward`. Not zero, not NaN, not unnormalised —
//       for garbage inputs too. `Unit()` is the single choke point that enforces it.
//   (2) `|AoaDeg| <= AoaCapDeg` ALWAYS, and `AoaCapDeg` is itself the scheduled cap for the phase and
//       altitude (§4.4's authority taper / §4.5's terminal schedule), never an unbounded number.
//   (3) Where `AoaDeg != 0` the angle between `AimForward` and surface-retrograde is EXACTLY `|AoaDeg|`.
//       AoA is a HELD, DELIBERATE, CAPPED command — never an emergent drift.
// The base attitude everywhere except boostback is retrograde: `AimForward` is the direction THRUST
// PUSHES the vehicle (it opposes the surface velocity), which is what sheds speed on the entry burn and
// stands the stage up on its thrust axis for the hoverslam.
//
// ============================================================================================
// PROVENANCE + STATUS (W8, 2026-09-04) — READ BEFORE TRUSTING ANY NUMBER IN THIS FILE
// ============================================================================================
// W3 (Wave C) restored this module VERBATIM from `8b81816^` as a FOUR-phase FSM and logged, in its own
// header, that it could fly neither profile's return leg: no BOOSTBACK state, no COAST state, no
// `TargetMode`. **W8 closes that gap.** The method is `docs/BOOSTER_GUIDANCE_METHOD.md` — the in-repo
// extraction of the owner's own Falcon-9 Interface (F9I) kOS scripts, TIER 2 under §1.4, GPL-3.0,
// attribution to mups3y and upstream to the KSP Starship kOS Interface (Janus92 / Nubro). Per C7 this
// file was built from that extraction ONLY; the raw `.ks` scripts are outside the repo and were not read.
//
// ⛔ THE REGIME RULE (§B16.8 ruling 2, method §1.1). **F9I flew STOCK KSP. This vehicle flies RSS-RO.**
// The METHOD, the STRUCTURE and the CONTROL LAWS transfer. **NO NUMBER TRANSFERS AS TUNING** — not an
// altitude, a velocity, a timing, a throttle fraction, a drag figure, a gain or a margin. Every constant
// below is therefore in exactly one of three states, and each is marked in place:
//   [RSS-RO SOURCED]   a real, in-repo RSS-RO measurement — today only `docs/reference/craftdump.csv`'s
//                      own `_minThrottle` figures. These are §1.4 tier-1 and are NOT guesses.
//   [UN-CONVERGED]     a placeholder that makes the law run. Either an inherited RSS-RO researched
//                      default (never flown) or an `[F9I]` stock-Kerbin figure carried ONLY because a
//                      zero would delete the mechanism. It is a starting point to re-converge, and it is
//                      evidence of nothing.
//   [NOT SEEDED]       the three figures W8 was directed to treat as a CONVERGENCE TARGET, NOT A SEED —
//                      the 170° flip, the 5° retrograde offset and the 2700 m downrange aim (§B16.2's
//                      C1.8 OVERRIDE). These start at ZERO / at geometry, never at F9I's value.
// Re-convergence needs RECORDED RSS-RO RE-FLIGHTS: the BlackBox (`docs/BLACKBOX_RESEARCH.md`) and a
// SEPARATE owner glass gate (§B16.8 ruling 3). No task can converge one under the preview-only gate.
//
// ⛔ WHAT W8 DELIBERATELY DID **NOT** PORT — three of them, each for a reason recorded in the repo. ⚠ (b)
// WAS SUPERSEDED 2026-09-05 ([[OCT6]]/[[OCT7]]) and is kept per C1.16 — reasoning is never deleted, only
// recorded as overruled:
//   (a) **The engine actuation layer** (method §7). F9I switches engine counts by cycling the Tundra
//       engine-switch module's "next engine mode" action — precisely what §B16.3 forbids, and the source
//       itself is the evidence (it retries the write three times and warns that "landing may be wrong").
//       Engine commanding is OURS and already landed: `pure/OctawebBinding.cs` + `pure/OctawebResolve.cs`
//       bind the three `ModuleEnginesRF` BY `engineID` STRING (`AllEngines`/`ThreeLanding`/`CenterOnly`)
//       with the foreign-vehicle guard. This file only ever NAMES a mode and a throttle.
//   (b) **[SUPERSEDED — the 3→1 handover mid-landing-burn IS now flown.]** THE ORIGINAL REASONING, kept
//       verbatim rather than deleted: it was not ported because `docs/reference/craftdump.csv` records
//       `ignitions = 1` on EACH of the three `ModuleEnginesRF`, so stepping 3→1 during the brake would
//       spend `CenterOnly`'s single ignition and spool mid-braking; F9I never faced RO ignition limits and
//       method §10 item 4 says so plainly — "the 3→1 handover is an extra ignition event F9I spends
//       without thinking about it." **That `ignitions = 1` premise is UNMEASURED, not established** — the
//       dump's figure is a PRELAUNCH pad read, while register [[BB8]] records the install's own
//       `%ignitions = -1` (RealFuels: unlimited) ConfigCache carrying −1 on the octaweb nine times, and
//       nobody has sampled it in flight. **The owner OVERRULED this reasoning, 2026-09-05, verbatim**
//       (quoted in full where [[OCT6]] built the replacement): asked which of two options to fly,
//       *"1. (2)"* — `ThreeLanding` shedding to `CenterOnly` — with the shed point *"comput[ed] from
//       current hover slam solver"*. OCT6 built the handover as a ONE-WAY LATCH with NO new ignition-
//       budget guard laid on the unmeasured count (the pre-existing `IgnitionsCentreOnly == 0` refusal,
//       reading the LIVE module, is untouched); [[OCT9]] is the shed criterion's own derived margin. The
//       `LandingBurn` case below is what the file actually flies now — read it, not this paragraph, for
//       current behaviour.
//   (c) **The four supporting lat/lng PID pairs** (method §4.4's aside). The vector steering law is what
//       flies the flown path; the doc itself says a port should start there rather than bring four
//       untested PIDs across.
//
// ⚠ C1.15 (evidence-gated mod-first), recorded because this module needs a PREDICTED IMPACT POINT.
// Searched `docs/reference/INSTALLED_MODS.md` for a mod that supplies one: **Trajectories is NOT in the
// installed list** (the only guidance-adjacent entries are MechJeb2, KER — `Stage`/terminal-velocity only —
// and FAR, an aero model, not a predictor). F9I's primary predictor IS Trajectories, so that path is
// unavailable AND already excluded: §B16.5 SETTLED that prediction comes from OUR OWN integrator, and
// names taking Trajectories as an owner call a build chat may not make. Accepted candidate:
// `pure/Trajectory.cs` (body-agnostic RK4, unit-proven against analytic conics) fed by
// `pure/BoosterDrag.cs`'s Mach-binned bc curve — both in the tree, wired by `PredictImpact()` below.
// **No second predictor is added, and no new simulation of a real quantity is written by this task.**
// ⚠ `BoosterDrag`'s curve is a distillate whose 48-flight raw corpus is GONE (§B16.8 ruling 1, R1 §3.5):
// reference with stated provenance, the best number we have, not evidence.
//
// ⚠ STILL NOTHING CALLS THIS. `pure/BoosterDescent`, `Hoverslam` and `GridFin` have no caller anywhere in
// `plugin/src` or `plugin/test` outside their own test. `BoosterControl.cs` — the gen-2 glue that used to
// drive them — is RECOVER-REFERENCE and STAYS DELETED (CLAUDE.md; R1 §5.2); §B16.1's replacement booster
// core is written FRESH and is not this task. Every flight command on every screen is still §14.4(a)'s
// honest no-op. A green `test/BoosterTest.cs` proves this FSM's LAWS and CONTRACTS. It proves nothing
// about tuning, and the booster has never been recovered in flight (R1 §4.2).
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>
    /// The recovery FSM's states. §B16.2's FIVE flying phases are Boostback · Coast · EntryBurn ·
    /// AeroDescent · LandingBurn; Flip is the pre-boostback slew (method §4.1) and Idle/Landed are the
    /// two terminal book-ends.
    /// </summary>
    public enum BoosterPhase : byte { Idle, Flip, Boostback, Coast, EntryBurn, AeroDescent, LandingBurn, Landed }

    /// <summary>
    /// ONE guidance, a TARGET MODE (method §2 / §B16.2). The mode resolves an aim point and a boostback
    /// geometry into <see cref="BoosterProfile"/>; every steering and timing law downstream is SHARED.
    /// ⛔ There must never be two guidance implementations here.
    /// </summary>
    public enum TargetMode : byte { Rtls, Asds }

    /// <summary>
    /// The target mode's parameter block — the whole of what the mode changes. §B16.2's C1.8 OVERRIDE
    /// (owner, 2026-09-03, closing G5a-Q2): BOOSTBACK IS ONE ALWAYS-ENTERED STATE for both profiles, with
    /// its MAGNITUDE and AIM-POINT OFFSET parameterized here — never an RTLS-only optional state.
    /// </summary>
    public struct BoosterProfile
    {
        public TargetMode Mode;

        /// <summary>0..1 authority scale on the boostback throttle law. RTLS = full return burn;
        /// **ASDS DEFAULTS TO ZERO**, which reproduces the old "no boostback" behaviour EXACTLY.</summary>
        public double BoostbackMagnitude;

        /// <summary>Pitch offset of the ASDS trim aim off horizontal retrograde, degrees.
        /// [NOT SEEDED] — F9I's 5° is a CONVERGENCE TARGET, not a seed; starts at 0.</summary>
        public double AimOffsetDeg;

        /// <summary>The deliberate LONG bias at boostback cut, metres — the budget for the range that the
        /// entry burn's off-retrograde steering and the aero descent under drag will later remove.
        /// [NOT SEEDED] — F9I's 2700 m is a stock-Kerbin DRAG BUDGET and method §10 puts it FIRST in the
        /// re-convergence order precisely because RO's atmosphere is not Kerbin's. Starts at 0.</summary>
        public double DownrangeAimM;

        /// <summary>Boostback throttle floor. Seeded from the engine's own RSS-RO minimum throttle.</summary>
        public double ThrottleFloor;

        /// <summary>Entry-burn trigger altitude and cutoff speed, before the payload-mass correction.</summary>
        public double EntryGateAltM, EntryCutSpeedMps;

        /// <summary>Reference payload mass for the §4.3 correction, kg. **0 = NOT ESTABLISHED for RSS-RO**,
        /// which makes the correction INERT — see Q2 at the foot of this file. F9I's 7000 / 9000 are
        /// stock-Kerbin masses for the Tundra vehicle and are NOT transferred.</summary>
        public double MaxPayloadKg;

        /// <summary>The mode's defaults. Every figure here is [UN-CONVERGED] or [NOT SEEDED] except the
        /// throttle floor, which is [RSS-RO SOURCED] from the craft dump.</summary>
        public static BoosterProfile For(TargetMode mode)
        {
            BoosterProfile p = new BoosterProfile();
            p.Mode = mode;
            p.AimOffsetDeg = 0.0;                                   // [NOT SEEDED] convergence target: 5° (ASDS)
            p.DownrangeAimM = 0.0;                                  // [NOT SEEDED] convergence target: 2700 m
            p.ThrottleFloor = BoosterDescent.MinThrottleThreeLanding;
            p.EntryGateAltM = BoosterDescent.EntryBurnStartAltM;
            p.EntryCutSpeedMps = BoosterDescent.EntryBurnCutSpeedMps;
            p.MaxPayloadKg = 0.0;                                   // correction inert until an RSS-RO reference exists
            // ⛔ THE ONE THING THE MODE ACTUALLY DECIDES TODAY.
            p.BoostbackMagnitude = (mode == TargetMode.Rtls) ? 1.0 : 0.0;
            return p;
        }

        /// <summary>Fill any unset gate in from the module defaults, so a DEFAULT-CONSTRUCTED profile is
        /// a VALID, INERT one — `Mode = Rtls`, zero magnitude, standard gates, i.e. exactly the old
        /// four-phase behaviour. `Guide()` calls this, so a caller that never sets `Profile` still gets a
        /// flyable FSM rather than a zero entry gate that drops it straight into the landing burn.</summary>
        public BoosterProfile Normalized()
        {
            BoosterProfile q = this;
            if (q.EntryGateAltM <= 0.0) q.EntryGateAltM = BoosterDescent.EntryBurnStartAltM;
            if (q.EntryCutSpeedMps <= 0.0) q.EntryCutSpeedMps = BoosterDescent.EntryBurnCutSpeedMps;
            if (q.ThrottleFloor <= 0.0) q.ThrottleFloor = BoosterDescent.MinThrottleThreeLanding;
            return q;
        }
    }

    /// <summary>
    /// Method §5's prediction primitive, expressed as the guidance actually consumes it: the SIGNED
    /// great-circle miss of a predicted impact point against the aim point.
    /// </summary>
    public struct ImpactError
    {
        public bool Valid;

        /// <summary>Signed along-track miss, metres. **+ = LONG** (the impact is beyond the target,
        /// continuing away from the vehicle); **− = SHORT**. This is the long/short test F9I does by
        /// comparing the angles the two subtend at the body centre; here it is the same comparison taken
        /// in the local horizontal plane at the target, which is numerically better conditioned.</summary>
        public double DownrangeM;

        /// <summary>Signed cross-track miss, metres, positive toward `up × approach`.</summary>
        public double CrossrangeM;

        /// <summary>Unsigned great-circle arc from the impact point to the target, metres.</summary>
        public double GreatCircleM;
    }

    public struct BoosterInputs
    {
        public bool Valid;
        public Vec3 SurfaceVelocity;    // surface-relative velocity, world frame
        public Vec3 Up;                 // local radial-up, world frame (unit)
        public double AltitudeM;        // TRUE height above the deck (already corrected for stage height)
        public double SpeedMps;         // |surface velocity| (entry-burn cut)
        public double DescentSpeedMps;  // vertical descent magnitude (hoverslam)

        // grid-fin steering (predicted-impact error on the deck; aim-to-miss until AllNominal)
        public GridFinInputs Fin;
        public bool AllNominal;
        public double OffsetToMissM;    // cross-deck bias applied until nominal

        public HoverslamInputs Land;    // for the landing ignition altitude — the CENTRE (CenterOnly) bank

        /// <summary>OCT6 — the SAME solve for the THREE-engine (`ThreeLanding`) bank, so
        /// <see cref="Hoverslam.EnginesFor"/> can be asked the question it was written to answer: the
        /// FEWEST engines that can still arrest from here. Built by the glue from the THREE bank's LIVE
        /// `maxThrust` over the LIVE mass, exactly as <see cref="Land"/> is built from the centre bank's —
        /// never a pre-computed schedule (the S48 §2.5 RO trap; `src/BoosterHost.cs` says so in place).
        /// ⛔ **`ThrustAccelMps2 &lt;= 0` = NOT SUPPLIED**, the same "0 = inert" convention
        /// <see cref="IgnitionsThreeLanding"/> uses. The 3→1 shed is then INERT and the landing burn flies
        /// `CenterOnly` throughout, exactly as it did before OCT6 — the shed is a decision between two
        /// MEASURED banks, and a bank nobody measured is not a bank this FSM will light.</summary>
        public HoverslamInputs LandThree;

        /// <summary>OCT6's ONE-WAY SHED LATCH, and the reason it is an INPUT: `Guide()` is a pure
        /// function, so the latch — like <see cref="CommandedForward"/> and
        /// <see cref="CommandedThrottle"/> — is carried by the caller and handed back in
        /// <see cref="BoosterCommand.LandingShedLatched"/> every tick.
        /// ⛔ **WHY IT LATCHES.** `EnginesFor` is evaluated EVERY frame and sits near its own boundary by
        /// construction (the throttle law drives `stopDistance/altitude` to ≈1, which is the same
        /// comparison the shed turns on). Un-latched it chatters, and every flip is a real shutdown plus a
        /// real re-ignition: physically you cannot un-shut a hoverslam, and no ignition budget survives
        /// chatter. Once `CenterOnly` is commanded the burn NEVER returns to `ThreeLanding`.</summary>
        public bool LandingShedLatched;

        // ---- W8: the target mode and the return leg ----------------------------------------------

        /// <summary>The target mode and its parameter block. Default-constructed (`Mode = Rtls`,
        /// `BoostbackMagnitude = 0`) it is a valid, inert profile — the FSM still flies, it simply does
        /// not boostback. Callers resolve it with <see cref="BoosterDescent.TargetModeFor"/>.</summary>
        public BoosterProfile Profile;

        /// <summary>The vehicle's ACTUAL thrust-axis facing, world frame. Feeds the flip's LEAD GATE
        /// (method §4.1). Zero = not supplied, and the gate is then not applied.</summary>
        public Vec3 Facing;

        /// <summary>The flip shaper's carried state: LAST TICK'S `AimForward`. The shaper is a command
        /// SHAPER, not a controller, so its one piece of state is passed in and handed back rather than
        /// hidden in a static — which is what keeps `Guide()` a pure function. Zero on the first tick.</summary>
        public Vec3 CommandedForward;

        /// <summary>Tick length, seconds. 0 = not supplied; the flip then snaps and the throttle ramp is
        /// bypassed, so a headless caller with no clock still gets defined, testable behaviour.</summary>
        public double DtS;

        /// <summary>Unit HORIZONTAL direction from the vehicle toward the aim point. This is what the RTLS
        /// boostback points the thrust axis at (method §4.2). Zero = not supplied.</summary>
        public Vec3 TargetBearing;

        /// <summary>Signed predicted-impact downrange error against the aim point, metres, + = LONG.
        /// Supplied by the caller from <see cref="BoosterDescent.ErrorTo"/> over
        /// <see cref="BoosterDescent.PredictImpact"/>'s answer.</summary>
        public double DownrangeErrM;

        /// <summary>The downrange error latched at boostback ignition — the normaliser in method §4.2's
        /// proportional-error throttle law. 0 = not latched; the law then commands full authority.</summary>
        public double InitialDownrangeErrM;

        /// <summary>Payload mass, kg, for the §4.3 entry-burn correction. 0 = not supplied → inert.</summary>
        public double PayloadMassKg;

        /// <summary>Propellant SETTLED (method §6, §B16.3). While false, no phase commands thrust and
        /// `UllageRcs` is raised instead. **This is the failure that lost the booster** — see register
        /// H1b and `docs/FLIGHT_144114_SCREEN_AUDIT.md` ("booster ballistic, eng never lit → LOST").</summary>
        public bool Ullaged;

        /// <summary>Last tick's commanded throttle — the spool ramp's carried state (method §6).</summary>
        public double CommandedThrottle;

        /// <summary>Ignitions remaining on the `ThreeLanding` / `CenterOnly` engine sets, read from the
        /// live `ModuleEnginesRF` (§B16.3: "read `ignitions`, budget them, refuse a phase the budget
        /// cannot cover"). **0 = NOT SUPPLIED and the budget guard is INERT.** The glue MUST supply them.</summary>
        public int IgnitionsThreeLanding, IgnitionsCentreOnly;
    }

    public struct BoosterCommand
    {
        public BoosterPhase Phase;
        public TargetMode Mode;
        public Vec3 AimForward;         // ALWAYS a unit vector — the direction thrust pushes the vehicle
        public double Throttle;
        public int EngineMode;          // VehicleParts consts: -1=ModeOff (no bank; OCT3), 0=ModeAllEngines
                                        // (ASCENT ONLY — BoosterHostPlan refuses it in every descent phase),
                                        // 1=ModeThreeEngine, 2=ModeCentreOnly
        public bool EnginesLit;         // ⚠ EngineMode 0 is AMBIGUOUS (ModeAllEngines == 0). THIS is the
                                        // "are the engines commanded on" answer; read it, not EngineMode != 0.
        public double AoaDeg;           // SIGNED, held angle of attack (negative = §4.5's terminal lean)
        public double AoaCapDeg;        // the scheduled cap in force this tick — |AoaDeg| <= this, always
        public bool DeployFins, DeployLegs;
        public bool UllageRcs;          // settle propellant NOW: an ignition is wanted and Ullaged is false
        public string Refusal;          // null = nothing refused; otherwise WHY a phase declined to burn

        /// <summary>OCT6 — the shed latch as it stands AFTER this tick. Hand it straight back in
        /// <see cref="BoosterInputs.LandingShedLatched"/> next tick; it only ever goes false→true, and
        /// `Guide()` never clears it. The host's phase gate reads THIS field on THIS tick so the gate and
        /// the FSM can never disagree about which bank is the legal one (OCT3's identical-decode rule,
        /// one level up).</summary>
        public bool LandingShedLatched;
    }

    public static class BoosterDescent
    {
        // ---- inherited RSS-RO researched defaults (W3's restored values, unchanged) ----------------
        // [UN-CONVERGED] R1 §5.1 files this module "RSS-RO researched, never DB-seeded / ❌ NO — booster
        // LOST"; the research documents they came from were deleted 2026-09-01 and are not in this repo,
        // so they cannot even be re-checked against their own source. They are kept because they are at
        // least of the right REGIME — unlike F9I's 32 500 m / 550 m/s, which are stock Kerbin and are NOT
        // taken. Method §2 records that the tier-2 source runs a LOWER gate and cutoff on RTLS than on
        // ASDS; that SHAPE is a convergence target, so both profiles seed from the single value here.
        [Tunable] public static double EntryBurnStartAltM = 70000.0;   // light the 3-engine entry burn descending through here
        [Tunable] public static double EntryBurnCutSpeedMps = 1300.0;  // bleed to a survivable reentry speed
        [Tunable] public static double FinDeployAltM = 70000.0;        // grid fins bite as the air thickens
        public const double LegsDeployAltM = 500.0;                    // [UN-CONVERGED] (F9I's 200 m is stock; not taken)
        public const double LandedSpeedMps = 2.0;

        // ---- [RSS-RO SOURCED] docs/reference/craftdump.csv, TE.19.F9.S1.Engine ---------------------
        // The three `ModuleEnginesRF` report their own `_minThrottle`, and each reports `ignitions = 1`.
        // These are §1.4 TIER-1 verified-real RSS-RO values from the in-repo dump — the ONLY numbers in
        // this file that are not placeholders. §B16.3: "never command zero throttle mid-landing-burn —
        // hold a floor above the engine minimum"; this IS that engine minimum, measured.
        public const double MinThrottleAllEngines   = 0.361003;
        public const double MinThrottleThreeLanding = 0.390625;
        public const double MinThrottleCentreOnly   = 0.390625;

        /// <summary>Ignitions each `engineID` set carries, per the dump. Drives the §B16.3 budget guard
        /// and is the reason the 3→1 handover is NOT ported (see the header, and Q1 at the foot).</summary>
        public const int DumpIgnitionsPerEngineSet = 1;

        // ---- §4.1 THE FLIP — a rate-limited command SHAPER -----------------------------------------
        // [UN-CONVERGED]. F9I advances its virtual command 0.333° PER TICK; a per-tick step is not a rate,
        // so even the conversion is un-converged — at KSP's 0.02 s default physics step that is ≈16.65 °/s,
        // and nothing says an RO booster can track it. The lead-gate / free-run / snap angles are F9I's
        // stock figures, carried as placeholders because a zero would delete the mechanism.
        [Tunable] public static double FlipRateDegPerS = 16.65;
        [Tunable] public static double FlipLeadGateDeg = 7.5;    // advance only while the vehicle is within this of the command
        [Tunable] public static double FlipFreeRunDeg  = 25.0;   // inside this much remaining, drop the gate
        [Tunable] public static double FlipSnapDeg     = 15.0;   // inside this much remaining, command the exact final vector

        // ---- §4.2 BOOSTBACK ------------------------------------------------------------------------
        /// <summary>A remaining error below this is not worth lighting an engine for. [UN-CONVERGED].</summary>
        [Tunable] public static double BoostbackDeadbandM = 250.0;

        // ---- §4.3 ENTRY BURN: the payload-mass correction -------------------------------------------
        [Tunable] public static double EntryGatePayloadDivisor = 7.0;    // [UN-CONVERGED] F9I stock: metres of gate per kg short
        [Tunable] public static double EntryCutPayloadDivisor  = 35.0;   // [UN-CONVERGED] F9I stock: m/s of cutoff per kg short

        // ---- §4.4 AERO DESCENT: the AUTHORITY TAPER -------------------------------------------------
        // [UN-CONVERGED]. F9I's cap below 10 km is `altitude / 100` (metres → degrees): 15° at 1500 m,
        // 5° at 500 m, 1° at 100 m. Method §10 puts this THIRD in the re-convergence order because it
        // "encodes an assumed terminal descent profile" — and F9I's terminal profile is a stock one.
        // THIS IS WHAT MAKES THE LANDING VERTICAL: steering authority is surrendered smoothly as the
        // ground approaches, so the stage stops trading attitude for accuracy exactly when it must stand
        // up on its thrust axis.
        [Tunable] public static double AoaTaperBelowAltM = 10000.0;
        [Tunable] public static double AoaTaperDegPerM   = 0.01;

        // ---- §4.5 LANDING BURN ----------------------------------------------------------------------
        // [UN-CONVERGED] F9I stock figures, carried as placeholders: a zero margin makes the throttle law
        // exactly critical (no headroom, guaranteed hard arrival) and a zero flare deletes the flare.
        [Tunable] public static double LandingThrottleMargin = 0.06;
        [Tunable] public static double FlareThrottleMargin   = 0.34;
        [Tunable] public static double FlareAltM             = 25.0;
        // The terminal AoA schedule goes NEGATIVE and tightens — leaning the opposite way to the descent
        // correction bleeds out the lateral rate built up while steering, so the stage arrives VERTICAL
        // rather than merely on target. [UN-CONVERGED], all of it.
        [Tunable] public static double TerminalAoaBiasDeg = 0.25;
        [Tunable] public static double TerminalAoaMinDeg  = 1.0;
        [Tunable] public static double TerminalAoaMaxDeg  = 4.0;
        [Tunable] public static double TerminalAoaPinAltM = 300.0;

        // ---- §6 ULLAGE + SPOOL ----------------------------------------------------------------------
        // [UN-CONVERGED] F9I stock figures. "Ignite at a trickle, then RAMP — never step." Respecting
        // spool rather than commanding instant thrust is the discipline; the numbers are placeholders.
        // ⚠ OCT9 (2026-09-05) also reads `ThrottleRampPerS` to derive the landing-burn shed criterion's
        // margin (the `LandingBurn` case, below) — not a second tuned figure, the SAME placeholder used
        // twice, so re-converging it re-converges both consumers together.
        [Tunable] public static double IgnitionTrickle  = 0.025;
        [Tunable] public static double ThrottleRampPerS = 1.333;

        // =========================================================================================
        // Vector helpers — the choke points that make contract (1) unconditional.
        // =========================================================================================

        static readonly Vec3 LastResort = new Vec3(0.0, 1.0, 0.0);

        /// <summary>A GUARANTEED finite unit vector: `v` if it can be normalised, else `fallback`, else a
        /// fixed axis. Contract (1) is enforced here and nowhere else.</summary>
        static Vec3 Unit(Vec3 v, Vec3 fallback)
        {
            if (v.IsFinite && v.Magnitude > 1e-9)
            {
                Vec3 n = v.Normalized;
                if (n.IsFinite && n.Magnitude > 0.5) return n;
            }
            if (fallback.IsFinite && fallback.Magnitude > 1e-9)
            {
                Vec3 f = fallback.Normalized;
                if (f.IsFinite && f.Magnitude > 0.5) return f;
            }
            return LastResort;
        }

        static Vec3 AnyPerpendicular(Vec3 unitV)
        {
            Vec3 a = Math.Abs(unitV.X) < 0.9 ? new Vec3(1, 0, 0) : new Vec3(0, 1, 0);
            Vec3 p = Vec3.Cross(unitV, a);
            return p.Magnitude > 1e-9 ? p.Normalized : new Vec3(0, 0, 1);
        }

        /// <summary>Surface-retrograde — the direction thrust must push to oppose the surface velocity.
        /// Below 1 m/s the retrograde direction is meaningless, so it falls back to local up.</summary>
        static Vec3 Retro(Vec3 sv, Vec3 up)
        {
            Vec3 u = Unit(up, LastResort);
            return sv.IsFinite && sv.Magnitude > 1.0 ? Unit(-sv, u) : u;
        }

        /// <summary>The horizontal (vertical component removed) part of surface-retrograde.</summary>
        static Vec3 HorizontalRetro(Vec3 sv, Vec3 up)
        {
            Vec3 u = Unit(up, LastResort);
            Vec3 retro = Retro(sv, u);
            return Unit(Vec3.ExcludeUnit(retro, u), retro);
        }

        /// <summary>Rotate `from` toward `to` by `degrees`, in the plane the two span. `preferredPerp`
        /// resolves the degenerate ANTIPARALLEL case — which is exactly the 180° boostback flip, so here
        /// it is the normal case, not an edge case.</summary>
        public static Vec3 RotateToward(Vec3 from, Vec3 to, double degrees, Vec3 preferredPerp)
        {
            Vec3 f = Unit(from, LastResort);
            Vec3 t = Unit(to, f);
            double totalDeg = Vec3.Angle(f, t) * 180.0 / Math.PI;
            if (totalDeg < 1e-9 || degrees >= totalDeg) return t;
            if (degrees <= 0.0) return f;

            Vec3 perp = Vec3.ExcludeUnit(t, f);
            if (perp.Magnitude < 1e-6)
            {
                perp = Vec3.ExcludeUnit(preferredPerp, f);
                if (perp.Magnitude < 1e-6) perp = AnyPerpendicular(f);
            }
            perp = Unit(perp, AnyPerpendicular(f));

            double a = degrees * Math.PI / 180.0;
            return Unit(f * Math.Cos(a) + perp * Math.Sin(a), f);
        }

        /// <summary>Retrograde tilted off by a SIGNED angle of attack toward the grid-fin tilt direction.
        /// A negative angle leans the OTHER way (§4.5's terminal schedule). Contract (3) lives here: the
        /// tilt basis is made orthonormal to retrograde, so the returned angle off retrograde is EXACTLY
        /// |signedAoaDeg|.</summary>
        static Vec3 SteerAim(Vec3 sv, Vec3 up, double tiltDown, double tiltCross, double signedAoaDeg)
        {
            Vec3 u = Unit(up, LastResort);
            Vec3 retro = Retro(sv, u);
            double mag = Math.Abs(signedAoaDeg);
            if (mag < 1e-9 || !sv.IsFinite || sv.Magnitude < 1.0) return retro;

            Vec3 downHat = Vec3.ExcludeUnit(sv, u);
            if (downHat.Magnitude < 1e-6) return retro;
            downHat = downHat.Normalized;
            Vec3 crossHat = Vec3.Cross(u, downHat);
            if (crossHat.Magnitude < 1e-6) return retro;
            crossHat = crossHat.Normalized;

            Vec3 tilt = downHat * tiltDown + crossHat * tiltCross;
            if (signedAoaDeg < 0.0) tilt = -tilt;
            tilt = Vec3.ExcludeUnit(tilt, retro);          // perpendicular to retro, so the angle is exact
            if (tilt.Magnitude < 1e-6) return retro;
            tilt = tilt.Normalized;

            double a = mag * Math.PI / 180.0;
            return Unit(retro * Math.Cos(a) + tilt * Math.Sin(a), retro);
        }

        // =========================================================================================
        // §5 — the prediction primitive. ONE predictor (Trajectory + BoosterDrag), and the signed
        // long/short test, which method §5 says is ours to write and is pure.
        // =========================================================================================

        /// <summary>
        /// Method §5's TWO-TIER prediction, over OUR OWN integrator (§B16.5) — no second predictor, no
        /// Trajectories dependency. Tier 1 is the drag-modelled solve with `BoosterDrag`'s Mach-binned bc
        /// curve wired in (a `DragFactor` the caller supplied wins). Tier 2, taken only when tier 1 cannot
        /// answer, is the SAME integrator run DRAG-FREE — the coarse Keplerian answer F9I keeps for the
        /// reason it gives: a booster whose guidance hard-fails when the good prediction is momentarily
        /// unavailable is a lost booster. A tier-2 answer is always LONG (drag only ever shortens a
        /// trajectory) and says so in `Note`.
        /// </summary>
        public static TrajectoryResult PredictImpact(TrajectoryInputs s, DensityAt density)
        {
            if (s.DragFactor == null) s.DragFactor = BoosterDrag.DragFactor;
            TrajectoryResult r = Trajectory.Solve(s, density);
            if (r.Ok) return r;

            TrajectoryInputs vac = s;
            vac.DragFactor = null;
            vac.BallisticCoefficient = 0.0;
            vac.LiftToDrag = 0.0;
            vac.UseLdBand = false;
            TrajectoryResult k = Trajectory.Solve(vac, density);
            if (!k.Ok) return r;                            // neither tier answered — hand back tier 1's note
            k.DragModelled = false;
            k.Note = "keplerian fallback (no drag) - the answer is LONG";
            return k;
        }

        /// <summary>
        /// The SIGNED miss of a predicted impact point against the aim point, in metres — method §5's
        /// "downrange distance from the impact point to the target", carrying the long/short sign the
        /// boostback throttle law needs in order to know long from short. All four arguments are
        /// world-frame positions; `bodyCentre` is the body's centre.
        /// `DownrangeM` is **+ when the impact lies BEYOND the target**, i.e. further along the direction
        /// the vehicle is approaching from — which is the direction drag will later eat into.
        /// </summary>
        public static ImpactError ErrorTo(Vec3 impact, Vec3 target, Vec3 vehicle, Vec3 bodyCentre)
        {
            ImpactError e = new ImpactError();
            if (!impact.IsFinite || !target.IsFinite || !vehicle.IsFinite || !bodyCentre.IsFinite) return e;

            Vec3 rI = impact - bodyCentre;
            Vec3 rT = target - bodyCentre;
            Vec3 rV = vehicle - bodyCentre;
            if (rI.Magnitude < 1.0 || rT.Magnitude < 1.0) return e;

            // the great-circle arc the two subtend at the body centre, taken at the TARGET's radius
            e.GreatCircleM = rT.Magnitude * Vec3.Angle(rI, rT);

            // local horizontal frame at the target: `approach` is the ground direction the vehicle is
            // coming from, so "beyond the target along it" is unambiguously LONG.
            Vec3 upT = rT.Normalized;
            Vec3 approach = Vec3.ExcludeUnit(rT - rV, upT);
            if (approach.Magnitude < 1.0)
            {
                // the vehicle is (nearly) over the target — there is no approach direction to sign against.
                e.Valid = true;
                e.DownrangeM = e.GreatCircleM;
                e.CrossrangeM = 0.0;
                return e;
            }
            approach = approach.Normalized;
            Vec3 crossHat = Unit(Vec3.Cross(upT, approach), AnyPerpendicular(approach));

            Vec3 miss = Vec3.ExcludeUnit(impact - target, upT);
            double along = Vec3.Dot(miss, approach);
            double cross = Vec3.Dot(miss, crossHat);

            // keep the great-circle MAGNITUDE (that is the curved distance) and take the SIGN and the
            // split from the local frame — the two agree to well under a metre at recovery ranges.
            double planar = Math.Sqrt(along * along + cross * cross);
            double scale = planar > 1e-6 ? e.GreatCircleM / planar : 1.0;
            e.DownrangeM = along * scale;
            e.CrossrangeM = cross * scale;
            e.Valid = true;
            return e;
        }

        /// <summary>Resolve a mission's recovery mode into the guidance's target mode. This is the seam
        /// §B16.9's per-mission LZ resolution (and `docs/reference/LZ_RECOVERY_TABLE.md`) feeds — the
        /// thing W3 recorded as having no consumer anywhere in the module.</summary>
        public static TargetMode TargetModeFor(RecoveryMode recovery)
        {
            return recovery == RecoveryMode.RTLS ? TargetMode.Rtls : TargetMode.Asds;
        }

        // =========================================================================================
        // The laws — each pure, each separately testable.
        // =========================================================================================

        /// <summary>
        /// §4.1 — the flip, as a RATE-LIMITED COMMAND SHAPER, not a slew-to-target. A virtual commanded
        /// vector starts at the vehicle's own facing and is advanced toward the final vector by a bounded
        /// step, and **only while the vehicle is keeping within `FlipLeadGateDeg` of it** — so the
        /// attitude error never leaves the controller's linear range. Inside `FlipFreeRunDeg` remaining
        /// the gate is dropped; inside `FlipSnapDeg` the command snaps to the exact final vector.
        /// `dtS &lt;= 0` (no clock) snaps immediately, so a headless caller still gets defined behaviour.
        /// </summary>
        public static Vec3 AdvanceFlip(Vec3 commanded, Vec3 target, Vec3 facing, Vec3 flipAxisPerp,
                                       double dtS, out bool complete)
        {
            Vec3 tgt = Unit(target, commanded);
            Vec3 cmd = Unit(commanded, Unit(facing, tgt));       // first tick: start at the vehicle's own facing
            double toGoDeg = Vec3.Angle(cmd, tgt) * 180.0 / Math.PI;

            if (toGoDeg <= FlipSnapDeg || dtS <= 0.0) { complete = true; return tgt; }

            double step = FlipRateDegPerS * dtS;
            if (toGoDeg > FlipFreeRunDeg && facing.IsFinite && facing.Magnitude > 1e-9)
            {
                double leadDeg = Vec3.Angle(Unit(facing, cmd), cmd) * 180.0 / Math.PI;
                if (leadDeg > FlipLeadGateDeg) step = 0.0;       // the vehicle is behind — stop leading it
            }

            complete = false;
            if (step <= 0.0) return cmd;
            if (step >= toGoDeg) { complete = true; return tgt; }
            return RotateToward(cmd, tgt, step, flipAxisPerp);
        }

        /// <summary>
        /// §4.2 — the boostback throttle law, and the accuracy mechanism of the whole method: throttle is
        /// proportional to the REMAINING downrange error normalised by the error at burn start, so the
        /// burn opens at full authority and TAPERS SMOOTHLY to the floor as the predicted impact walks
        /// onto the aim point. No bang-bang, no fixed Δv, no burn timer.
        /// Returns 0 once the error is nulled down to the deliberate LONG bias — that is the cut.
        /// </summary>
        public static double BoostbackThrottle(BoosterProfile p, double errM, double initialErrM)
        {
            if (p.BoostbackMagnitude <= 0.0) return 0.0;
            double remaining = errM - p.DownrangeAimM;
            if (remaining <= 0.0) return 0.0;                    // the cut: the impact is at (or past) the aim
            double e0 = Math.Abs(initialErrM - p.DownrangeAimM);
            double f = e0 > 1.0 ? remaining / e0 : 1.0;
            if (f > 1.0) f = 1.0;
            double floor = p.ThrottleFloor > 0.0 ? p.ThrottleFloor : 0.0;
            if (f < floor) f = floor;
            double t = f * p.BoostbackMagnitude;
            if (t > 1.0) t = 1.0;
            return t < 0.0 ? 0.0 : t;
        }

        /// <summary>True once the boostback has nothing left to do — the cut, the deadband, or a mode
        /// whose magnitude is zero (ASDS's default, which is why this state is always ENTERED and then
        /// immediately LEFT rather than skipped).</summary>
        public static bool BoostbackComplete(BoosterProfile p, double errM)
        {
            if (p.BoostbackMagnitude <= 0.0) return true;
            if (errM - p.DownrangeAimM <= 0.0) return true;
            return Math.Abs(errM - p.DownrangeAimM) < BoostbackDeadbandM;
        }

        /// <summary>§4.3 — the entry-burn GATE with the payload-mass correction: a lighter payload means a
        /// hotter, faster booster, so the gate RISES linearly in the payload shortfall. INERT unless the
        /// caller supplies both a reference payload and a live one (see Q2).</summary>
        public static double EntryGateAltM(BoosterProfile p, double payloadKg)
        {
            if (p.MaxPayloadKg <= 0.0 || payloadKg <= 0.0 || EntryGatePayloadDivisor <= 0.0) return p.EntryGateAltM;
            double shortfall = p.MaxPayloadKg - payloadKg;
            if (shortfall < 0.0) shortfall = 0.0;
            return p.EntryGateAltM + shortfall / EntryGatePayloadDivisor;
        }

        /// <summary>§4.3 — the entry-burn CUTOFF speed, tightened linearly in the same payload shortfall.</summary>
        public static double EntryCutSpeedMps(BoosterProfile p, double payloadKg)
        {
            if (p.MaxPayloadKg <= 0.0 || payloadKg <= 0.0 || EntryCutPayloadDivisor <= 0.0) return p.EntryCutSpeedMps;
            double shortfall = p.MaxPayloadKg - payloadKg;
            if (shortfall < 0.0) shortfall = 0.0;
            double v = p.EntryCutSpeedMps - shortfall / EntryCutPayloadDivisor;
            return v > 0.0 ? v : 0.0;
        }

        /// <summary>
        /// The AoA CAP in force, by phase and altitude — §4.4's authority taper and §4.5's terminal
        /// schedule in one place, so contract (2) has exactly one definition. Returns a NON-NEGATIVE
        /// magnitude; the SIGN of the commanded angle is the phase's business.
        /// </summary>
        public static double AoaCapDeg(BoosterPhase phase, double altM, double baseCapDeg)
        {
            if (phase == BoosterPhase.AeroDescent)
            {
                double cap = baseCapDeg > 0.0 ? baseCapDeg : 0.0;
                if (altM < AoaTaperBelowAltM)
                {
                    double taper = altM > 0.0 ? altM * AoaTaperDegPerM : 0.0;
                    if (taper < cap) cap = taper;
                }
                return cap;
            }
            if (phase == BoosterPhase.LandingBurn)
            {
                if (altM <= TerminalAoaPinAltM) return TerminalAoaMinDeg;
                double m = (altM > 0.0 ? altM * AoaTaperDegPerM : 0.0) + TerminalAoaBiasDeg;
                if (m < TerminalAoaMinDeg) m = TerminalAoaMinDeg;
                if (m > TerminalAoaMaxDeg) m = TerminalAoaMaxDeg;
                return m;
            }
            return 0.0;   // no aero steering authority is claimed in any other phase
        }

        /// <summary>
        /// §4.5 — the landing-burn throttle law: `stopDistance / trueAltitude + margin`, evaluated every
        /// tick against LIVE mass and LIVE thrust. Self-correcting by construction: need more braking
        /// distance than you have altitude and the ratio exceeds 1 → full throttle; comfortably above it
        /// and the ratio drops → the throttle backs off. No trajectory replan, no scheduled profile.
        /// ⚠ It also sidesteps the RO trap S48 §2.5 records — a landing altitude pre-computed against a
        /// too-high predicted mass arms too early — by never pre-computing at all.
        /// Clamped BELOW at the engine's own measured minimum throttle: §B16.3, never command zero
        /// mid-burn, that is an instant shutdown and the relight costs an ignition we do not have.
        /// </summary>
        public static double LandingThrottle(BoosterInputs s)
        {
            return LandingThrottle(s, s.Land, MinThrottleCentreOnly);
        }

        /// <summary>OCT6 — the same law, solved against the bank that is ACTUALLY LIT. Flying three
        /// engines while the stop-distance solve models one is not "fly three": the modelled bank is
        /// weaker, so `stopDistance` comes out large, the ratio comes out high and the law over-throttles
        /// a bank three times stronger than the one it solved for. The bank is therefore passed in,
        /// alongside its own measured minimum throttle — the two `_minThrottle` figures are numerically
        /// EQUAL today (both 0.390625, `docs/reference/craftdump.csv`), so this changes no number; it
        /// stops the law naming a bank it is not flying.</summary>
        public static double LandingThrottle(BoosterInputs s, HoverslamInputs bank, double minThrottle)
        {
            HoverslamInputs lit = bank;
            lit.DeadTimeS = 0.0;                 // the engine is ALREADY lit — no dead fall left to cover
            lit.SpoolS = 0.0;
            double stop = Hoverslam.IgnitionAltitude(lit);

            double margin = LandingThrottleMargin;
            if (s.AltitudeM <= FlareAltM) margin += FlareThrottleMargin;

            double t = s.AltitudeM > 1.0 ? stop / s.AltitudeM + margin : 1.0;
            if (t > 1.0) t = 1.0;
            if (t < minThrottle) t = minThrottle;
            return t;
        }

        /// <summary>OCT6 — the three-engine landing bank as the solver should see it, or `Land` when the
        /// glue supplied no three-engine thrust. `ThrustAccelMps2 &lt;= 0` is the struct's own
        /// "0 = NOT SUPPLIED" convention (<see cref="BoosterInputs.LandThree"/>).</summary>
        public static bool ThreeBankSupplied(BoosterInputs s) { return s.LandThree.ThrustAccelMps2 > 0.0; }

        /// <summary>§6 — ignite at a TRICKLE, then RAMP; never step. Walks the commanded throttle toward
        /// the law's value at a bounded rate in both directions, respecting spool-up rather than
        /// commanding instant thrust. `dtS &lt;= 0` (no clock) passes the wanted value straight through,
        /// and a commanded cut is immediate.</summary>
        public static double RampThrottle(double previous, double wanted, double dtS)
        {
            if (dtS <= 0.0 || ThrottleRampPerS <= 0.0) return wanted;
            if (wanted <= 0.0) return 0.0;
            double from = previous > 0.0 ? previous : IgnitionTrickle;   // light at a trickle, then ramp
            double step = ThrottleRampPerS * dtS;
            if (wanted > from) { double v = from + step; return v < wanted ? v : wanted; }
            double d = from - step;
            return d > wanted ? d : wanted;
        }

        // =========================================================================================
        // The FSM.
        // =========================================================================================

        public static BoosterCommand Guide(BoosterInputs s, BoosterPhase phase)
        {
            BoosterCommand c = new BoosterCommand();
            c.Phase = phase;
            c.Mode = s.Profile.Mode;
            c.AimForward = s.Valid ? Retro(s.SurfaceVelocity, s.Up) : Unit(s.Up, LastResort);
            c.Throttle = 0.0; c.EngineMode = VehicleParts.ModeOff; c.EnginesLit = false;
            c.AoaDeg = 0.0; c.AoaCapDeg = 0.0; c.Refusal = null;
            // OCT6 — the shed latch is CARRIED, in every phase and on every exit path including the
            // invalid-input bail below. It only ever goes false→true, and only in the LandingBurn case:
            // nothing here, and nothing anywhere else in this FSM, ever clears it.
            c.LandingShedLatched = s.LandingShedLatched;

            if (!s.Valid) { c.Phase = BoosterPhase.Idle; c.AimForward = Unit(s.Up, LastResort); return c; }

            Vec3 up = Unit(s.Up, LastResort);
            Vec3 retro = Retro(s.SurfaceVelocity, up);
            BoosterProfile p = s.Profile.Normalized();
            double gateAlt = EntryGateAltM(p, s.PayloadMassKg);
            double cutSpeed = EntryCutSpeedMps(p, s.PayloadMassKg);
            double wantThrottle = 0.0;

            switch (phase)
            {
                case BoosterPhase.Idle:
                case BoosterPhase.Flip:
                {
                    // §4.1. Slew, rate-limited, to the attitude BOOSTBACK will burn at — which for a
                    // zero-magnitude profile is simply retrograde, reproducing the old behaviour exactly.
                    c.Phase = BoosterPhase.Flip;
                    Vec3 target = BoostbackAim(s, up, retro);
                    Vec3 axis = Vec3.Cross(HorizontalRetro(s.SurfaceVelocity, up), up);   // F9I's flip axis
                    bool done;
                    c.AimForward = AdvanceFlip(s.CommandedForward, target, s.Facing, axis, s.DtS, out done);
                    // Late or degenerate: if we are already at the entry gate there is no return leg left
                    // to fly — fall through rather than slew on into the atmosphere.
                    if (done || s.AltitudeM <= gateAlt) c.Phase = BoosterPhase.Boostback;
                    break;
                }

                case BoosterPhase.Boostback:
                {
                    // ⛔ §B16.2's C1.8 OVERRIDE: ONE ALWAYS-ENTERED STATE, for BOTH profiles. ASDS enters
                    // it at ZERO magnitude and leaves on the same tick — that is not "skipping boostback",
                    // it is the same state sized as a zero trim, and it is what lets the ASDS trim be
                    // converged later by changing a NUMBER rather than by adding a state.
                    c.Phase = BoosterPhase.Boostback;
                    c.AimForward = BoostbackAim(s, up, retro);

                    wantThrottle = BoostbackThrottle(p, s.DownrangeErrM, s.InitialDownrangeErrM);

                    if (wantThrottle > 0.0)
                    {
                        // §B16.3's ignition budget. The entry burn and this burn both want `ThreeLanding`,
                        // and the dump gives that set ONE ignition — refuse rather than strand the entry
                        // burn. (0 = the glue supplied no count → the guard is inert. See Q1.)
                        if (s.IgnitionsThreeLanding > 0 && s.IgnitionsThreeLanding < 2)
                        {
                            wantThrottle = 0.0;
                            c.Refusal = "boostback refused: ThreeLanding has " + s.IgnitionsThreeLanding
                                      + " ignition(s), the entry burn needs one";
                        }
                        else if (p.Mode == TargetMode.Rtls
                                 && (!s.TargetBearing.IsFinite || s.TargetBearing.Magnitude < 0.5))
                        {
                            wantThrottle = 0.0;
                            c.Refusal = "boostback refused: RTLS has no target bearing to aim at";
                        }
                        else if (!s.Ullaged)
                        {
                            wantThrottle = 0.0;
                            c.UllageRcs = true;      // §6 / §B16.3 — settle before EVERY relight
                        }
                    }

                    if (wantThrottle > 0.0) { c.EngineMode = VehicleParts.ModeThreeEngine; c.EnginesLit = true; }

                    if (c.Refusal != null || BoostbackComplete(p, s.DownrangeErrM) || s.AltitudeM <= gateAlt)
                    {
                        // OCT5: the exit can fire on the SAME tick the bank above was lit (a residual
                        // downrange error under the deadband, or the entry gate reached independently of
                        // it) — mirror EntryBurn's exit (below) so Coast never inherits a live bank.
                        c.Phase = BoosterPhase.Coast;
                        c.EnginesLit = false; wantThrottle = 0.0;
                        c.EngineMode = VehicleParts.ModeOff;
                    }
                    break;
                }

                case BoosterPhase.Coast:
                {
                    // §B16.2 phase 2: BALLISTIC, retrograde, engines off. The stage reorients and falls.
                    c.Phase = BoosterPhase.Coast;
                    c.AimForward = retro;
                    if (s.AltitudeM <= gateAlt)
                        c.Phase = s.SpeedMps > cutSpeed ? BoosterPhase.EntryBurn : BoosterPhase.AeroDescent;
                    break;
                }

                case BoosterPhase.EntryBurn:
                {
                    // §4.3: PURE surface-retrograde (thrust dominates; steering to target here wastes it),
                    // gated on ALTITUDE, cut on SPEED — never on duration or Δv. Grid fins out at the gate.
                    c.Phase = BoosterPhase.EntryBurn;
                    c.AimForward = retro;
                    c.DeployFins = s.AltitudeM <= FinDeployAltM;

                    if (!s.Ullaged)
                    {
                        c.UllageRcs = true;
                    }
                    else if (s.IgnitionsThreeLanding == 0 && s.IgnitionsCentreOnly > 0)
                    {
                        // a count WAS supplied and this set is spent — say so rather than command a dead engine.
                        c.Refusal = "entry burn refused: ThreeLanding has no ignition left";
                    }
                    else
                    {
                        wantThrottle = 1.0;
                        c.EngineMode = VehicleParts.ModeThreeEngine;   // ⛔ VehicleParts const (the bare 3 decoded as
                        c.EnginesLit = true;                           // the all/outer set = engines that spent their
                    }                                                  // ignition at liftoff → H1b)

                    if (s.SpeedMps <= cutSpeed || c.Refusal != null)
                    {
                        c.Phase = BoosterPhase.AeroDescent;
                        c.EnginesLit = false; wantThrottle = 0.0;
                        c.EngineMode = VehicleParts.ModeOff;
                    }
                    break;
                }

                case BoosterPhase.AeroDescent:
                {
                    // §4.4 — THE ONE STEERING LAW THAT FLIES THE WHOLE DESCENT: steer retrograde, then
                    // LEAN INTO THE MISS. Adding the miss vector to the retrograde vector tilts the stage
                    // so lift and drag push the predicted impact back onto the target; the cap is a true
                    // trigonometric limit, so the commanded deflection is EXACTLY the capped angle rather
                    // than an over-limit command that was merely rejected.
                    c.Phase = BoosterPhase.AeroDescent;
                    double cap = AoaCapDeg(BoosterPhase.AeroDescent, s.AltitudeM, s.Fin.AoaMaxDeg);
                    GridFinInputs fin = s.Fin;
                    fin.AoaMaxDeg = cap;                     // the AUTHORITY TAPER — this is what makes it vertical
                    GridFinCommand g = GridFin.Steer(fin);
                    double aoa = g.AoaDeg;
                    if (aoa > cap) aoa = cap;
                    if (aoa < 0.0) aoa = 0.0;

                    c.AoaCapDeg = cap;
                    c.AoaDeg = aoa;
                    c.AimForward = SteerAim(s.SurfaceVelocity, up, g.TiltDown, g.TiltCross, aoa);
                    c.DeployFins = s.AltitudeM <= FinDeployAltM;

                    if (s.AltitudeM <= Hoverslam.IgnitionAltitude(s.Land)) c.Phase = BoosterPhase.LandingBurn;
                    break;
                }

                case BoosterPhase.LandingBurn:
                {
                    // ⛔ OCT6 (owner ruling, 2026-09-05) — THE LANDING BURN LIGHTS **THREE** ENGINES AND
                    // SHEDS TO ONE, and the shed point is COMPUTED, not stated. Asked which of the two
                    // options to fly, the owner answered *"1. (2)"* — option (2), `ThreeLanding` shedding
                    // to `CenterOnly` — and, asked what triggers the shed, *"yes to computing from current
                    // hover slam solver"*. So `Hoverslam.EnginesFor` (which has carried exactly this
                    // decision since it was written, with no caller) is asked EVERY tick against the two
                    // measured banks, and the burn flies the FEWEST engines that can still arrest.
                    //
                    // ⛔ WHAT THIS OVERRULED, AND WHY THE REASONING IS KEPT (C1.16's spirit). Until this
                    // ruling the case read: *"each engineID set carries ONE ignition, so we do NOT step
                    // 3→1 during the burn — that would re-ignite CenterOnly and spool mid-braking"*, and
                    // the file header's §4.5 non-port (b) still says so. That premise is **UNMEASURED**.
                    // `docs/reference/craftdump.csv` does record `ignitions = 1` on each of the three
                    // octaweb `ModuleEnginesRF` — but that is a PRELAUNCH pad read, and register **BB8**
                    // records that the install's own `Crew2_Patches/F9_Engines_InstantSpool.cfg` sets
                    // `%ignitions = -1` (RealFuels: unlimited) with the final ModuleManager ConfigCache
                    // carrying −1 on the octaweb nine times and no other value. Config and persistence
                    // both say unlimited; only the pad read said 1. ⚠ **NOBODY HAS MEASURED IT IN
                    // FLIGHT** — BB8 is the line that will. So the ignition count is not evidence for or
                    // against the shed, and NO new ignition-budget guard is built on it here: the ONE
                    // budget refusal below is the pre-existing `IgnitionsCentreOnly == 0` guard, reading
                    // the LIVE module, unchanged.
                    //
                    // The glue selects the set ABSOLUTELY by ACTIVATING that bank's `ModuleEnginesRF`
                    // BOUND BY ITS engineID (§B16.4 step 2 / `pure/OctawebResolve.cs`) — NEVER by cycling
                    // NextEngineMode, and NEVER by writing `ModuleTundraEngineSwitch.selectedIndex`
                    // (§B16.3 bans that module as a switching mechanism outright; READ for annunciation
                    // only). ⚠ Whether that dispatch SEQUENCES a mid-burn bank change correctly is
                    // register **OCT4**, and OCT4 has not run: this is the first mid-burn mode change in
                    // the project and it will fly through dispatch code no line has audited.
                    c.Phase = BoosterPhase.LandingBurn;

                    // §4.5's TERMINAL AoA SCHEDULE — the cap goes NEGATIVE and tightens, leaning the
                    // opposite way to the descent correction so the lateral rate built up while steering
                    // is bled out and the stage arrives VERTICAL rather than merely on target.
                    double lcap = AoaCapDeg(BoosterPhase.LandingBurn, s.AltitudeM, s.Fin.AoaMaxDeg);
                    GridFinInputs lfin = s.Fin;
                    lfin.AoaMaxDeg = lcap;
                    GridFinCommand lg = GridFin.Steer(lfin);
                    double lmag = lg.AoaDeg;
                    if (lmag > lcap) lmag = lcap;
                    if (lmag < 0.0) lmag = 0.0;

                    c.AoaCapDeg = lcap;
                    c.AoaDeg = -lmag;                                    // negative: the terminal lean
                    c.AimForward = SteerAim(s.SurfaceVelocity, up, lg.TiltDown, lg.TiltCross, c.AoaDeg);

                    if (!s.Ullaged)
                    {
                        c.UllageRcs = true;
                    }
                    else if (s.IgnitionsCentreOnly == 0 && s.IgnitionsThreeLanding > 0)
                    {
                        c.Refusal = "landing burn refused: CenterOnly has no ignition left";
                    }
                    else
                    {
                        // ⛔ THE SHED DECISION, and it LATCHES ONE WAY. `EnginesFor` returns the fewest
                        // engines that can still arrest — 3, 1, or 0 for "not even three can". Once it has
                        // said 1 the latch holds `CenterOnly` for the rest of the burn: shedding is a real
                        // shutdown, un-shedding is a real re-ignition mid-brake, and the solver sits on its
                        // own boundary (the throttle law drives stop/altitude to ≈1 — the same comparison),
                        // so an un-latched answer chatters. There is no path back to `ThreeLanding`.
                        int bank;
                        if (!ThreeBankSupplied(s))
                            bank = 1;                    // no measured three-engine bank → the shed is
                                                         // inert and this is the pre-OCT6 burn, unchanged.
                        else if (s.LandingShedLatched)
                            bank = 1;                    // already shed — never re-evaluated.
                        else
                        {
                            // OCT9 (2026-09-05, owner ruling "(2), and give me OCT4") — RECONCILE TWO
                            // SPOOL MODELS rather than invent a margin. `s.Land.SpoolS` is fed 0 by
                            // `BoosterHost` — "instant-spool Merlin", true of the ENGINE given our
                            // `throttleResponseRate` patch. But the centre bank a shed lights has never
                            // fired THIS burn, and this FSM imposes its OWN §6 policy on top of the
                            // engine — "ignite at a trickle, then RAMP; never step" (`RampThrottle`,
                            // `ThrottleRampPerS`) — which the solver above was never told about. OCT6's
                            // mutation run measured the consequence: shed at the bare boundary, margin
                            // already negative 8 ticks (0.8 s) later.
                            // `HoverslamInputs.SpoolS` already models exactly "time for thrust to reach
                            // full" (see `Hoverslam.IgnitionAltitude`'s brake-phase ramp), so feed the
                            // SHED TEST — and only the shed test; `LandingThrottle` and the AeroDescent
                            // hand-over gate ([[OCT10]]) are untouched — the time OUR OWN throttle ramp
                            // actually takes to cross the full range. This TRACKS `ThrottleRampPerS`,
                            // itself [UN-CONVERGED] and already flagged above, rather than adding a
                            // second, invented figure: the shed simply moves to the altitude where one
                            // engine can arrest INCLUDING the ramp, not the altitude where it could arrest
                            // if it were already at full throttle the instant it lit.
                            // ⛔ HONESTY, PER THE BRIEF: this does not escape an un-converged constant —
                            // it makes the margin TRACK one already in the model and already flagged,
                            // instead of inventing a second. §B16.8 ruling 2 is not violated by that, but
                            // is not fully discharged either — `ThrottleRampPerS` itself still awaits a
                            // recorded RSS-RO re-flight.
                            HoverslamInputs landForShed = s.Land;
                            landForShed.SpoolS = ThrottleRampPerS > 1e-6 ? 1.0 / ThrottleRampPerS : 0.0;
                            bank = Hoverslam.EnginesFor(landForShed, s.LandThree);
                        }

                        if (bank == 0)
                        {
                            // `EnginesFor`'s own un-recoverable case: even THREE engines cannot null the
                            // descent from here. Say so — a fallback to some bank would be a landing this
                            // FSM has already computed it cannot make, flown silently.
                            c.Refusal = "landing burn refused: even the 3-engine bank cannot arrest from here";
                        }
                        else if (bank == 1)
                        {
                            c.LandingShedLatched = true;                 // ⛔ ONE WAY. Never cleared.
                            wantThrottle = LandingThrottle(s, s.Land, MinThrottleCentreOnly);
                            c.EngineMode = VehicleParts.ModeCentreOnly;
                            c.EnginesLit = true;
                        }
                        else
                        {
                            wantThrottle = LandingThrottle(s, s.LandThree, MinThrottleThreeLanding);
                            c.EngineMode = VehicleParts.ModeThreeEngine;
                            c.EnginesLit = true;
                        }
                    }

                    c.DeployFins = true;
                    c.DeployLegs = s.AltitudeM <= LegsDeployAltM;
                    if (s.AltitudeM <= 1.0 && s.DescentSpeedMps <= LandedSpeedMps)
                    {
                        c.Phase = BoosterPhase.Landed;
                        wantThrottle = 0.0; c.EnginesLit = false;
                        c.EngineMode = VehicleParts.ModeOff;
                        c.AoaDeg = 0.0; c.AoaCapDeg = 0.0;
                        c.AimForward = retro;
                    }
                    break;
                }

                default:
                    c.Phase = BoosterPhase.Landed;
                    wantThrottle = 0.0; c.EnginesLit = false;
                    c.EngineMode = VehicleParts.ModeOff;
                    c.AimForward = up;
                    break;
            }

            // §6 — spool. ONE shared ramp; every ignition in every phase goes through it.
            c.Throttle = c.EnginesLit ? RampThrottle(s.CommandedThrottle, wantThrottle, s.DtS) : 0.0;
            if (c.Throttle < 0.0) c.Throttle = 0.0;
            if (c.Throttle > 1.0) c.Throttle = 1.0;

            // Contracts (1) and (2), enforced unconditionally on the way out.
            c.AimForward = Unit(c.AimForward, up);
            if (c.AoaCapDeg < 0.0) c.AoaCapDeg = 0.0;
            if (c.AoaDeg > c.AoaCapDeg) c.AoaDeg = c.AoaCapDeg;
            if (c.AoaDeg < -c.AoaCapDeg) c.AoaDeg = -c.AoaCapDeg;
            return c;
        }

        /// <summary>
        /// §4.2's boostback attitude, and the ONE place the target mode changes the geometry.
        /// **RTLS** points the thrust axis at the horizontal bearing to the target — *not* retrograde.
        /// **ASDS** holds horizontal retrograde pitched by the mode's offset.
        /// **A zero-magnitude profile holds FULL surface retrograde**, which is exactly what the
        /// four-phase FSM did before boostback existed — the "no boostback" behaviour, reproduced.
        /// ⚠ F9I expresses this as a flip to 180° (RTLS) / 170° (ASDS) from the pre-flip attitude. We
        /// command the final VECTOR geometrically instead: the same attitude, with no dependence on what
        /// the stage happened to be pointing at at separation. The 170°/5° pair is a CONVERGENCE TARGET
        /// (§B16.2), so `AimOffsetDeg` starts at 0 — and the axis the offset pitches about (toward local
        /// up, the reading consistent with "170°" being 10° short of a full reversal) is OUR READING of
        /// method §4.2, recorded here because the doc does not name an axis. See Q3.
        /// </summary>
        static Vec3 BoostbackAim(BoosterInputs s, Vec3 up, Vec3 retro)
        {
            if (s.Profile.BoostbackMagnitude <= 0.0 && Math.Abs(s.Profile.AimOffsetDeg) < 1e-9)
                return retro;

            if (s.Profile.Mode == TargetMode.Rtls)
            {
                if (s.TargetBearing.IsFinite && s.TargetBearing.Magnitude > 0.5)
                    return Unit(Vec3.ExcludeUnit(s.TargetBearing, up), retro);
                return HorizontalRetro(s.SurfaceVelocity, up);   // refused in Guide(); still DEFINITE here
            }

            Vec3 hRetro = HorizontalRetro(s.SurfaceVelocity, up);
            if (Math.Abs(s.Profile.AimOffsetDeg) < 1e-9) return hRetro;
            Vec3 pitchTarget = s.Profile.AimOffsetDeg > 0.0 ? up : -up;
            return RotateToward(hRetro, pitchTarget, Math.Abs(s.Profile.AimOffsetDeg), up);
        }
    }
}

// ============================================================================================
// ## Open questions for the owner
// ============================================================================================
// Per C1.14 these are written here, in W8's own deliverable, with options and a recommendation. **W8
// decided none of them and proceeded past none** — each is either inert by default or already governed by
// a settled decision that this file follows.
//
// ---- Q1. RTLS BOOSTBACK HAS NO IGNITION LEFT IN THE CRAFT AS DUMPED -------------------------------
// **Situation.** `docs/reference/craftdump.csv` reports `ignitions = 1` on EACH of the three
// `ModuleEnginesRF` sets (`AllEngines`, `ThreeLanding`, `CenterOnly`). §B16.4's own table assigns
// `ThreeLanding` to "boostback, entry burn, landing-burn start" — three burns on a set with ONE ignition.
// The entry burn and the landing burn already claim `ThreeLanding` and `CenterOnly` respectively, so on
// this craft **a full RTLS boostback burn has nothing left to light.** ASDS is unaffected (its default
// magnitude is zero). W8 built the state as directed and added §B16.3's budget guard: when the glue
// supplies a live `IgnitionsThreeLanding` and it is below 2, boostback REFUSES and annunciates rather
// than stranding the entry burn. With no count supplied the guard is inert, so nothing changes silently.
// **Options.**
//   1. Accept the guard as built — RTLS boostback refuses on this craft until the vehicle changes, and
//      the refusal is visible rather than a silent dead engine.
//   2. Give the booster more `ThreeLanding` ignitions in the VAB / via a config and re-dump (§B16.4's
//      re-dump is already pending) — a CRAFT change, and therefore the owner's call.
//   3. Fly the RTLS boostback on `AllEngines` instead, spending the set that already burned at liftoff —
//      needs its `ignitions` re-checked, and it is a different engine count than the method assumes.
//   4. Re-read the dump's `ignitions` as "remaining at dump time" rather than as a budget, which would
//      make the question moot — but that needs a live in-flight read, i.e. glass time.
// **Recommendation: (2), with (1) standing in the meantime.** The guard is correct and costs nothing; the
// real fix is a vehicle that can actually fly the profile, and the re-dump is already scheduled. (3)
// spends an ignition on nine engines to do a three-engine job, and (4) cannot be settled under the
// preview-only gate.
//
// ---- Q2. THERE IS NO RSS-RO REFERENCE PAYLOAD MASS, SO §4.3's CORRECTION IS INERT -----------------
// **Situation.** Method §4.3's better engineering is the payload-mass compensation: a lighter payload
// means a hotter booster, so the entry gate rises and the cutoff tightens, both linearly in the shortfall
// against a reference payload. F9I's references are 7000 (RTLS) / 9000 (ASDS) — stock-Kerbin masses for
// the Tundra vehicle. The regime rule forbids transferring them, and this repo records no RSS-RO
// equivalent. W8 implemented the law and left `MaxPayloadKg = 0`, which makes it inert.
// **Options.**
//   1. Leave it inert until a recorded flight establishes a reference — the entry gate is then simply the
//      profile's flat value, which is today's behaviour exactly.
//   2. Derive a reference from the 16 in-repo `docs/reference/<mission>.craft` files' Dragon masses and
//      mark it tier-2-derived.
//   3. The owner supplies a figure.
// **Recommendation: (1).** The correction is a refinement on a gate that is itself un-converged; adding a
// second un-converged number underneath it buys no accuracy and hides which of the two is wrong. (2) is a
// real option once the pending re-dump lands, and switching to it is a one-line change.
//
// ---- Q3. THE ASDS OFFSET'S ROTATION AXIS IS OUR READING, NOT THE SOURCE'S -------------------------
// **Situation.** Method §4.2 says the ASDS boostback holds "horizontal retrograde rotated by the mode's
// offset", and §2 pairs that with a "170° flip". Neither names the axis. W8 pitches toward local up (the
// reading consistent with 170° being 10° short of a full reversal) and recorded that choice in the code.
// The value is 0 by directive, so nothing depends on the choice today.
// **Options.**
//   1. Keep the pitch reading and revisit when the ASDS trim is first converged.
//   2. Yaw about local up instead (a heading offset).
//   3. Ask the overseer to check the source and report the axis back as evidence.
// **Recommendation: (1).** It is inert, it is documented in place, and the first ASDS trim convergence
// will settle it from a recorded flight far better than a re-reading of the doc would.
//
// ---- Q4. THE `[F9I]` NUMBERS WRITTEN AS PLACEHOLDERS RATHER THAN ZEROED ---------------------------
// **Situation.** W8's instruction named three figures as CONVERGENCE TARGETS, NOT SEEDS — the 170° flip,
// the 5° offset and the 2700 m aim — and those start at zero/geometry here. For the rest (the flip's
// lead-gate / free-run / snap angles, the AoA taper's `altitude/100`, the landing margins 0.06 / 0.34, the
// 25 m flare, the spool ramp and the ignition trickle) W8 wrote F9I's figure as a marked `[UN-CONVERGED]`
// placeholder, because zeroing them deletes the mechanism the law exists to express — a zero flare margin
// is no flare, a zero taper is no authority schedule at all. Every one is `[Tunable]` and marked in place.
// **Options.**
//   1. Keep the placeholders — the laws run and are testable, and the marking says plainly that they are
//      evidence of nothing.
//   2. Zero them all, which leaves several phases as no-ops until a recorded flight.
//   3. Zero a named subset.
// **Recommendation: (1).** It matches §B16.8 ruling 2, which contemplates exactly this: every `[F9I]`
// value existing in our system, marked un-converged, and re-converged from recorded re-flights.
// ============================================================================================
