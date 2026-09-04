// DragonScreen — BoosterHost  (KSP glue: the thing that RUNS `pure/BoosterDescent.cs` on the booster)
// ============================================================================================
// ⛔ NOT a restored file. Written by W23 (2026-09-04). W8 built the five-phase booster script and
// recorded, in its own header, that **nothing calls it**. This is the caller.
//
// OWNER DIRECTION, 2026-09-04, via the overseer: *"we use MechJeb for all upper stage manoeuvres as
// planned. BOOSTER SCRIPTED."* · *"as soon as the booster gets dropped it runs its script."*
// Two autopilots, two vessels, and **they must not interfere with each other's flights**. This host runs
// ONE vessel — the separated first stage — and the selection in `pure/BoosterHostPlan.cs` is what makes
// that a guarantee rather than an intention (three independent tests each exclude the Dragon).
//
// ============================================================================================
// ⭐ THE GATING QUESTION, ANSWERED WITH EVIDENCE: **CAN AN UNFOCUSED, LOADED VESSEL BE COMMANDED?**
// **YES — and it was PROVEN IN FLIGHT, TWICE, by this project.** §B16.7 has the booster landing
// UNFOCUSED, so this gates the whole protocol; it is answered here rather than assumed.
//
// The constraint is **UNPACKED, not ACTIVE**. KSP has three states, not two
// (`docs/BOOSTER_RECOVERY_ARCHITECTURE.md` §1.1): UNLOADED (on rails, no control) · LOADED-but-PACKED
// (partial physics, **no control**) · LOADED+UNPACKED (full physics, **control yes**). kOS documents the
// limit as an unpack limit — *"KSP limits some features (like throttle control) to only vessels that are
// unpacked"* — and BDArmory flies many non-active craft by hooking each vessel's control callback, with
// PhysicsRangeExtender as a hard dependency for exactly that reason.
//
// ⭐ AND THIS REPO'S OWN GIT HISTORY IS THE PRIMARY EVIDENCE — two flights, KSP.log + CSV cross-checked:
//
//  • **Flight 134620** (`8fd533d`, "C2 Step-1 Tick-3 GREEN: dual-flight proof passed"):
//      *"KSP drives the non-active booster's OnFlyByWire under PRE (cbEntered=2250, body-rate 7.9→36 dps,
//       loaded+unpacked throughout) → control reaches it."*
//    The BODY RATE CHANGED — so the axes did not merely get written, they took effect.
//
//  • **Flight Crew-2_20260829_144114** (`e2b7ea6`, "C2 Step-2 Tick-3 result: dual-flight control PROVEN"):
//      *"The non-active booster's OWN OnFlyByWire flew the full recovery FSM: 16,139 calls, EntryBurn→
//       LandingBurn, grid fins, engine mode AllEngines→ThreeLanding switched exactly once … attitude loop
//       live (att.err 105→6 deg). Two vehicles controlled at once."* — and in the same flight the Dragon
//      reached orbit 200×197.7 km, inc 51.64. **Both vessels flew. Neither disturbed the other.**
//
//    That flight exercised all four command classes this host uses, on a NON-ACTIVE vessel:
//      attitude axes ✔ (att.err 105→6°) · throttle via `FlightCtrlState.mainThrottle` ✔ ·
//      engine ACTIVATION by `ModuleEngines.Activate()` on the matching-`engineID` module ✔ ·
//      part actions (grid-fin deploy) ✔.
//
//  • ⚠ **THE ONE THING THAT DID NOT WORK WAS NOT A FOCUS PROBLEM.** Same flight: *"Booster engine never
//    lit (eng_ignited=0 whole descent) → ballistic → LOST @14 km. Root = RealFuels ullage."* The
//    activation command REACHED the engine; RealFuels' ullage gate refused the ignition. That is register
//    **H1b / W5**, and it is why this host holds the ullage gate CLOSED (see `Ullaged` below).
//
// ⇒ **§B16.7's protocol stands. No STOP, no owner decision needed on feasibility.** What remains is a
// PHYSICS risk, not a permissions one, and §B16.7 already states and accepts it: KSP re-centres its
// floating origin on the ACTIVE vessel, so the unfocused booster is the one that shakes. Keeping the
// booster loaded+unpacked at range is PhysicsRangeExtender's job (`src/RangeExtender.cs`, §B16.7 step 1)
// and belongs to the recovery conductor — **register W9**, not this host: `RangeExtender.Enable` writes
// `vesselRanges` on EVERY loaded vessel, including the Dragon, and this host touches one vessel only.
// Until W9 lands, a real flight will packs-out the booster within a few km and this host will say so and
// let go — honestly, and by the stated rule.
//
// ⚠ NOT PROVEN IN FLIGHT: `independentThrottle` / `independentThrottlePercentage` on a non-active vessel.
// The flown path was `s.mainThrottle`. We write BOTH, with the SAME value, so they cannot disagree — see
// `ApplyThrottle`.
//
// ============================================================================================
// ⭐ ATTITUDE IS NOW COMMANDED — register W24, 2026-09-04.
// `Guide()` returns a unit `AimForward` every tick. Turning an aim vector into torque is a CONTROL LAW,
// and it is precisely the component that failed before: `AttitudePilot` / `AttitudeController` /
// `pure/AttitudeLoop.cs` — RCS chatter, a roll under-control, an attitude limit cycle, DS-ASC-007's
// *"RCS loss = ~97% attitude"* — reverted three times, ordered stripped by the owner (`70dc239`), and
// filed by R1 §3.2 as ⛔ **RECOVER-REFERENCE ONLY — never live code (owner directive)**. It got its own
// register line, its own scrutiny and its own gate — **W24** — and NO BYTE of those three files is here.
// `docs/FLIGHT_CORPUS_ASSESSMENT.md` §3 corrected the inherited diagnosis: the ascent failure was a
// DIVERGENCE (an unbounded commanded rate), not a limit cycle, so `pure/BoosterSteer.cs`'s law makes that
// specific shape structurally unreachable (a fixed rate ceiling, never a live authority estimate) rather
// than merely re-tuning it.
// `AttitudeError()` below converts `AimForward` into per-axis pitch/yaw/roll DEGREES using the ONE piece
// of the deleted law R1 §3.2 names as reusable independent of its gains — the frame-conversion formula,
// freshly written here, no code copied. `pure/BoosterSteer.Steer()` turns those (plus `v.angularVelocity`)
// into a bounded command, which `Fbw()` now writes to `s.pitch`/`s.yaw`/`s.roll` — gated, like every other
// axis this host writes, on `fbwOwned` and on `Actuate`. §14.4(a) still governs: while blocked or unbound,
// nothing is written (the axes are RELEASED, not zeroed), and `AttitudeUncommanded` reports which state
// is in force so no screen can show a command that is not actually live.
//
// ============================================================================================
// ⛔ WHAT THIS FILE MUST NEVER DO
// • NEVER TOUCH THE DRAGON. One bound vessel, selected by `BoosterHostPlan.Select`, never the active
//   vessel, never one carrying a pod. That separation IS the non-interference guarantee.
// • NEVER CREATE A MechJebCore (§B16.1) — the booster core is OURS, and O2 forbids using the MechJeb
//   that flies the mission.
// • NEVER STAGE, NEVER FIRE AN ACTION GROUP, NEVER `NextEngineModeAction`, NEVER `ModuleEngineConfigs`
//   as a switch, NEVER write `ModuleTundraEngineSwitch.selectedIndex` (§B16.3 / §B16.4). Engine sets are
//   selected ABSOLUTELY by activating the matching-`engineID` module WHILE OFF.
// • NEVER RE-LIGHT A LIT SET. The rule stands regardless of ignition count — a re-light is a real
//   shutdown plus a real spool mid-flight — so the role change is the only thing that ever touches an
//   engine. ⚠ **THE IGNITION COUNT IS UNMEASURED, not the fact this line used to assert.**
//   `docs/reference/craftdump.csv` reads `ignitions = 1` on each `engineID` set, but that is a PRELAUNCH
//   pad read; register [[BB8]] records the install's own `%ignitions = -1` (RealFuels: unlimited)
//   ConfigCache carrying −1 on the octaweb nine times, and nobody has sampled it in flight. See
//   `SelectEngineSet`'s own doc comment below (fixed by [[OCT4]]) and [[OCT7]], which corrected this line
//   and `pure/BoosterDescent.cs`'s file header — the only two sites that still stated the pad read as fact.
// • NEVER WIDEN PHYSICS RANGES (that is global, and therefore the Dragon's too — register W9).
// • NEVER SEARCH FOR AN ENGINE PER FRAME. `OctawebEngines.Resolve` once, `StillValid` thereafter.
// ============================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    /// <summary>The flight-scene tick. Deliberately tiny and excisable — delete it and the host simply
    /// never runs, exactly as `CraftDumpAddon` is to `CraftDump`.</summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class BoosterHostAddon : MonoBehaviour
    {
        void FixedUpdate() { BoosterHost.Tick(); }
        void OnDestroy() { BoosterHost.Release("flight scene ended"); }
    }

    public static class BoosterHost
    {
        // =========================================================================================
        // THE ARM — register W24 flips this to TRUE, per the owner's ruling on W23's Q1
        // =========================================================================================
        // ⛔ **HISTORY, KEPT BECAUSE IT IS WHY THIS FLAG EXISTS.** `Actuate = false` used to mean no
        // command left this host at all — the script bound at separation, ticked every physics frame,
        // advanced its phases and reported, but the WRITE was gated because there was no steering law:
        // a booster that lights an engine with an uncontrolled attitude is not a landing — it is flight
        // 194334. `8225df7`, finding A1: *"Booster recovery self-destructs: fires thr=1.0 0.3 s after
        // MECO at 'sep 0 km', attitude diverges 2→85 deg, LOST in ~10 s — and its 0-km burn kicks the
        // upper stage."* The owner's own fix at the time was to stop the engines firing (`LetFall = true`).
        //
        // ⭐ **W24 (2026-09-04) BUILT THE STEERING LAW** (`pure/BoosterSteer.cs` + this file's attitude
        // wiring below) and flips this flag, per the owner's own recorded ruling on W23's Q1 option 1:
        // *"Leave `Actuate = false`; W24 (the steering law) flips it as part of its own gate."* That is
        // exactly what this is — not a build chat closing a gate on its own authority (C1.12); the owner
        // named this task as the one that gets to make this call, in the open question that asked it.
        //
        // ⚠ **STATED PLAINLY: the next flight is the first time this host commands a real vessel.** The
        // steering law is fresh, its gains are `[UN-CONVERGED]` (§B16.8 ruling 2) and its per-axis SIGN is
        // UNVERIFIED — there is no recorded flight to derive it from (see the open question at the foot of
        // this file). `install` and glass time remain a SEPARATE owner gate (CLAUDE.md's build-go section)
        // — this flag changes what the CODE will do once that separate gate opens, not whether it opens.
        // ℹ It is `[Tunable]`, so the owner can hold it back at `false` from `PluginData/tuning.cfg` with
        //   no recompile if that is preferred instead — the override lands once `Tuning.Build()` has run
        //   (`DragonScreenMonitor`, i.e. after the IVA screens have ticked once). The CODE DEFAULT is now
        //   TRUE, per the ruling above; `Tuning.cs`'s rule that the code default stays the authority still
        //   applies to whatever value is actually in force.
        [Tunable] public static bool Actuate = true;

        // =========================================================================================
        // THE SEAM W5 FILLS — and until it does, the ullage gate is CLOSED
        // =========================================================================================
        // `BoosterInputs.Ullaged` is *"propellant SETTLED (method §6, §B16.3)"*, and it is **the failure
        // that lost the booster**: `docs/FLIGHT_144114_SCREEN_AUDIT.md` — *"booster ballistic, eng never
        // lit → LOST"*. The real source is RealFuels' propellant-settling state, read by reflection in
        // `src/Ullage.cs` — RECOVER-CODE, HIGH priority, and owned by **register W5**, which is TODO.
        // C1.1/§B12.8 rider (b) forbid restoring it inside this diff.
        //
        // C1.15 (evidence-gated mod-first): searched `docs/reference/INSTALLED_MODS.md` for a source of
        // propellant-settling state. **RealFuels is installed and IS the source** (row 1 of that file
        // names it, and names `Ullage.cs` as the reader). No other installed mod models ullage. **So no
        // simulation is written here** — the quantity has a real mod source, it simply has no reader in
        // the tree yet. A null hook reports NOT SETTLED, so every phase that wants thrust raises
        // `UllageRcs` and refuses to burn. That is the safe direction and it is the FSM's own design.
        public static Func<Vessel, bool> UllageSettled;

        // =========================================================================================
        // ⚠ THERE IS NO AIM POINT IN THE TREE, AND THIS HOST DOES NOT INVENT ONE
        // =========================================================================================
        // `BoosterInputs.TargetBearing`, `.DownrangeErrM`, `.InitialDownrangeErrM` and the grid-fin error
        // terms all need a landing target. `src/BoosterTargeting.cs` is RECOVER-REFERENCE and §B16.9
        // hands the per-mission table to **LZ1**, whose deliverable
        // (`docs/reference/LZ_RECOVERY_TABLE.md`) is a DOC — real, sourced, and not yet code.
        // So they are supplied as ZERO, honestly, and the consequences are the FSM's own stated ones:
        //   • RTLS boostback REFUSES and annunciates — *"boostback refused: RTLS has no target bearing"*;
        //   • ASDS is inert anyway (default magnitude 0 — §B16.2's C1.8 OVERRIDE);
        //   • the grid-fin law steers toward zero error, i.e. holds retrograde.
        // Turning LZ1's table into a live aim point (and running `PredictImpact` over `pure/Trajectory.cs`
        // for the error) is **register W25** — its own line, because it is guidance, not hosting.
        // ⛔ Do not put a latitude/longitude in this file. §1.4: sourced and marked, or not at all.

        // =========================================================================================
        // [UN-CONVERGED] the ullage settle IMPULSE direction
        // =========================================================================================
        // §B16.3: *"settle propellant with RCS before EVERY relight"*. Settling is a BODY-FRAME push —
        // accelerate along the vehicle's own fore axis so the propellant goes aft onto the engines — so
        // it needs no steering law and no attitude control to be correct. What it does need is KSP's
        // translation SIGN, and this repo has that flight-anchored rather than guessed: the deleted
        // `src/DockingControl.cs` (RECOVER-REFERENCE — read, quoted, no code taken) records
        // `RcsFwdSign = -1.0` with `s.Z = −Dot(demand, ct.up)`, and anchors it to flight 131412, where
        // *"RendezvousControl's prograde burn uses s.Z = −1 with the nose (=ct.up) prograde and raised
        // apoapsis correctly"*. So **`s.Z = −1` is fore**. Full authority; RO ullage wants a real push.
        public const double UllageFwdSign = -1.0;
        [Tunable] public static double UllageTranslate = 1.0;   // [UN-CONVERGED] fraction of RCS translation authority

        // How often the honest status line goes to KSP.log. Not a control constant.
        [Tunable] public static double LogIntervalS = 2.0;

        // ---------------------------------------------------------------------------------------
        // BOUND STATE — resolved ONCE at separation and held (§B16.4 step 2: never re-searched per frame)
        // ---------------------------------------------------------------------------------------
        static Vessel bound;
        static OctawebEngines octaweb;
        static BoosterProfile profile;
        static bool hooked;
        static double bindUT;

        // The FSM's carried state. `Guide()` is a pure function: the shaper's memory lives HERE and is
        // handed back in every tick, which is what keeps it pure (BoosterDescent's own note).
        static BoosterPhase phase = BoosterPhase.Idle;
        static Vec3 commandedForward = Vec3.Zero;
        static double commandedThrottle;

        /// <summary>OCT6's one-way shed latch, carried across ticks in the SAME mechanism that carries
        /// `phase`: a host static, handed into `Guide()` and read back out of its command every tick. A
        /// per-call local could not hold it, and `EnginesFor` sits on its own boundary — un-latched, the
        /// 3→1 shed would chatter, and every flip is a real shutdown plus a real re-ignition. Cleared
        /// ONLY where `phase` is cleared: on a fresh bind and on release.</summary>
        static bool landingShed;

        // What we have actually done to the vehicle, so nothing is repeated (one ignition per set).
        static EngineRole currentRole = EngineRole.None;
        static bool finsOut, legsDown;

        // What the OnFlyByWire callback is allowed to write this frame. ⛔ `fbwOwned` is the whole point:
        // the callback takes an axis ONLY when a dispatch actually ran, exactly as the deleted
        // `FlightDriver` did (*"Only take an axis when a controller is actively commanding it; otherwise
        // leave the player/idle in control"*). Unarmed or blocked, the host writes NOTHING AT ALL — not
        // even a zero, because forcing a throttle to zero is still a command.
        static bool fbwOwned;
        static double fbwThrottle;
        static bool fbwUllage;
        static double fbwPitch, fbwYaw, fbwRoll;           // register W24 — the steering law's output

        // ⭐ OBSERVABILITY (the owner's Q2 refinement on `docs/BOOSTER_STEERING_MOD_SEARCH.md`): whether
        // the deadband suppressed each axis' error THIS TICK, and what value it ran at. A future BlackBox
        // column (register BB1) reads these; this host invents no recording mechanism of its own.
        static bool steerPitchDeadbanded, steerYawDeadbanded, steerRollDeadbanded;
        static double steerDeadbandDeg;

        static double lastLogUT = -999.0;
        static double lastBindTryUT = -999.0;
        static BoosterBind lastBindVerdict = BoosterBind.NoVessel;
        static double landedSinceUT = -1.0;

        // ---------------------------------------------------------------------------------------
        // READ-ONLY REPORT — what the host is doing, for a screen or the BlackBox when one exists
        // ---------------------------------------------------------------------------------------
        public static Vessel Booster { get { return bound; } }
        public static bool Engaged { get { return bound != null; } }
        public static BoosterPhase Phase { get { return phase; } }
        public static TargetMode Mode { get { return profile.Mode; } }

        /// <summary>THE COMMANDED ATTITUDE, unit, every tick, in the world frame. Register W24's steering
        /// law (`pure/BoosterSteer.cs`) now closes the loop on this — see `AttitudeUncommanded` for
        /// whether it is actually reaching the vehicle THIS tick.</summary>
        public static Vec3 AimForward { get; private set; }

        /// <summary>§14.4(a): true whenever nothing is actually driving the axes this tick — unbound,
        /// blocked (hold-off, packed, `Actuate=false`), or the axes were simply released rather than
        /// zeroed. False exactly when `Fbw()` is about to write `s.pitch`/`s.yaw`/`s.roll`. Any display
        /// that shows the aim MUST show this with it.</summary>
        public static bool AttitudeUncommanded { get { return !fbwOwned; } }

        /// <summary>The steering law's own commanded axes, [-1,1], for the same tick as `AimForward` —
        /// reported REGARDLESS of `fbwOwned`, exactly as `AimForward` always was, so a screen or a future
        /// BlackBox can show what was COMPUTED even while it is not being WRITTEN.</summary>
        public static double SteerPitch { get; private set; }
        public static double SteerYaw { get; private set; }
        public static double SteerRoll { get; private set; }

        // ⭐ Q2 OBSERVABILITY — read-only, for a screen or the BlackBox (register BB1) to surface.
        public static bool SteerPitchDeadbanded { get { return steerPitchDeadbanded; } }
        public static bool SteerYawDeadbanded { get { return steerYawDeadbanded; } }
        public static bool SteerRollDeadbanded { get { return steerRollDeadbanded; } }
        public static double SteerDeadbandDeg { get { return steerDeadbandDeg; } }

        public static double Throttle { get { return fbwThrottle; } }
        public static string Refusal { get; private set; }        // the FSM's own refusal, verbatim
        public static string BlockNote { get; private set; }      // why no command left the host this tick

        /// <summary>BB9: the SAME reason as `BlockNote`, as the stable enum `BoosterHostPlan.BlockedFor`
        /// returned, for a recording to filter on — `BlockNote` is prose and `Annunciation`'s wording is
        /// free to change; this is not.</summary>
        public static BoosterCommandBlock Block { get { return lastBlock; } }
        static BoosterCommandBlock lastBlock;

        /// <summary>[[OCT11]] What `currentRole` currently claims lit — i.e. what `Dispatch` last
        /// commanded. Read-only mirror of the private command record, so a screen or the BlackBox can
        /// NAME the bank in the commanded-vs-lit divergence below rather than merely inferring it.</summary>
        public static EngineRole CommandedRole { get { return currentRole; } }

        /// <summary>[[OCT11]] THE COMMANDED-VS-LIT DIVERGENCE, STATED. True when `currentRole` names a
        /// bank and that SAME bank's own `ModuleEngines.EngineIgnited` says it is not burning — the exact
        /// shape that lost the booster on flight Crew-2_20260829_144114 (*"eng_ignited=0 whole descent"*).
        /// Recomputed EVERY tick in `Fly()` from the bound per-bank module, independent of whether a
        /// dispatch ran this tick, so it tracks RESOLUTION too (a bank commanded now can light several
        /// ticks later once ullage settles) — not just the moment it was first commanded.
        /// ⛔ **THIS PROPERTY ANNOUNCES. IT DOES NOT RETRY.** `pure/BoosterHostPlan.CommandedNotIgnited`
        /// carries the overseer ruling (2026-09-05) in full: re-deriving `currentRole` from this predicate
        /// would retry `Activate()` every tick against RealFuels' UNMEASURED ignition budget while
        /// `TestFlightFailure_IgnitionFail` sits on this exact part (§B16.4) — register W5's call, not
        /// this one's.</summary>
        public static bool CommandedNotIgnited { get; private set; }

        /// <summary>[[OCT11]] Prose for the divergence above, on the SAME channel `BlockNote` uses (a free-
        /// text field beside a stable enum/bool a recording or screen actually keys on) — `null` when
        /// there is no divergence to report.</summary>
        public static string CommandedNotIgnitedNote { get; private set; }

        // =========================================================================================
        // THE TICK
        // =========================================================================================
        public static void Tick()
        {
            try
            {
                if (!HighLogic.LoadedSceneIsFlight) { Release("not in flight"); return; }
                if (bound == null) { TryBind(); return; }

                BoosterFlightSnapshot snap = Snapshot(bound);
                BoosterHostStop stop = BoosterHostPlan.StopReason(snap);
                if (stop != BoosterHostStop.None)
                {
                    Release(BoosterHostPlan.Annunciation(stop));
                    return;
                }

                Fly(bound, snap);
            }
            catch (Exception e)
            {
                // A glue fault logs and carries on; it never takes a flight down and never leaves an
                // engine lit on a half-finished tick.
                Debug.LogWarning("[DragonScreen] booster host tick failed: " + e.Message);
                fbwOwned = false; fbwThrottle = 0.0; fbwUllage = false;
            }
        }

        // =========================================================================================
        // BINDING — once, at separation, and it must be the BOOSTER
        // =========================================================================================
        static void TryBind()
        {
            double now = Now();
            if (now - lastBindTryUT < 0.5) return;      // the scan is cheap but not free; twice a second
            lastBindTryUT = now;

            List<Vessel> all = FlightGlobals.Vessels;
            if (all == null || all.Count == 0) return;
            Vessel active = FlightGlobals.ActiveVessel;

            BoosterCandidate[] cands = new BoosterCandidate[all.Count];
            for (int i = 0; i < all.Count; i++) cands[i] = Describe(all[i], active);

            int idx;
            BoosterBind verdict = BoosterHostPlan.Select(cands, out idx);
            if (verdict != BoosterBind.Ok)
            {
                // Only annunciate a CHANGE — "no separated booster" is the normal state for most of a
                // flight and must not flood KSP.log (the S40 lesson).
                if (verdict != lastBindVerdict && verdict != BoosterBind.NoSeparatedBooster
                    && verdict != BoosterBind.NoVessel)
                    Debug.LogWarning("[DragonScreen] " + BoosterHostPlan.Annunciation(verdict));
                lastBindVerdict = verdict;
                return;
            }
            lastBindVerdict = verdict;

            Vessel v = all[idx];

            // The octaweb table — §B16.4's guard runs inside `Resolve` and refuses the wrong vehicle.
            OctawebEngines table = OctawebEngines.Resolve(v);
            if (!table.Ok)
            {
                // Once per distinct refusal (S40 / `pure/LogGate.cs`): the bind is retried twice a second
                // for the whole descent, and a standing warning would bury KSP.log.
                if (LogGate.First("booster-host-octaweb:" + table.Table.Plan))
                    Debug.LogWarning("[DragonScreen] booster host: found the booster but the octaweb bind "
                                     + "REFUSED — " + (table.Annunciation ?? "no reason given") + " (not binding)");
                return;
            }

            bound = v;
            octaweb = table;
            bindUT = Now();
            phase = BoosterPhase.Idle;
            commandedForward = Vec3.Zero;
            commandedThrottle = 0.0;
            landingShed = false;                 // OCT6 — a fresh binding is a fresh, un-shed burn
            currentRole = EngineRole.None;
            finsOut = false; legsDown = false;
            landedSinceUT = -1.0;
            fbwOwned = false; fbwThrottle = 0.0; fbwUllage = false;
            fbwPitch = 0.0; fbwYaw = 0.0; fbwRoll = 0.0;
            steerPitchDeadbanded = false; steerYawDeadbanded = false; steerRollDeadbanded = false;
            steerDeadbandDeg = 0.0;
            Refusal = null; BlockNote = null; lastBlock = BoosterCommandBlock.None;
            CommandedNotIgnited = false; CommandedNotIgnitedNote = null;   // [[OCT11]] — fresh bind, fresh record

            // The TARGET MODE, from an in-repo source and nothing else: the vessel name → the mission
            // catalog → `RecoveryMode` → `TargetMode`. An unresolved name falls back to ASDS, whose
            // default boostback magnitude is ZERO — the inert profile (`BoosterHostPlan.ProfileFor`).
            MissionProfile mission = Missions.Resolve(v.vesselName);
            profile = BoosterHostPlan.ProfileFor(mission);

            Hook(v);

            Debug.Log("[DragonScreen] ⭐ BOOSTER HOST BOUND — \"" + v.vesselName + "\" ("
                      + v.parts.Count + " parts), octaweb " + table.OctawebPart
                      + ", mission " + (mission.Valid ? mission.Name : "UNRESOLVED→inert")
                      + ", mode " + profile.Mode
                      + ". Actuate=" + Actuate + ", ullage source="
                      + (UllageSettled != null ? "live" : "NONE (gate held closed — register W5)")
                      + ". ⛔ ATTITUDE UNCOMMANDED — AimForward is reported, not flown (register W24).");
        }

        /// <summary>Reduce a live `Vessel` to the facts `BoosterHostPlan.Select` turns on. Read-only: this
        /// walks parts to classify them and touches nothing.</summary>
        static BoosterCandidate Describe(Vessel v, Vessel active)
        {
            BoosterCandidate c = new BoosterCandidate();
            if (v == null || v.state == Vessel.State.DEAD) return c;
            c.Exists = true;
            c.IsActive = ReferenceEquals(v, active);
            c.Loaded = v.loaded;
            if (!v.loaded || v.parts == null) return c;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p == null) continue;
                // ⛔ `partInfo.name`, NOT `Part.name` (OCT1, 2026-09-05) — the SAME expression the two dumps
                // and `OctawebEngines.Resolve` use. This walk and that one must classify one vessel
                // identically: on 2026-09-05 they did not, because this one asked `IsBooster` for a `.S1.`
                // SUBSTRING (which survived the extra characters `Part.name` carried) while the octaweb
                // binder asked for whole-name EQUALITY (which did not). "Found the booster" and "octaweb
                // not found" about the same part, 264 times. Change this line and `OctawebEngines.PartName`
                // together or the disagreement comes straight back.
                string nm = (p.partInfo != null ? p.partInfo.name : p.name) ?? "";
                if (OctawebBinding.IsForeignBoosterPart(nm)) c.HasForeignBoosterPart = true;
                if (VehicleParts.IsPod(nm)) c.HasPod = true;
                else if (VehicleParts.IsBooster(nm)) c.HasBoosterPart = true;
            }
            return c;
        }

        static BoosterFlightSnapshot Snapshot(Vessel v)
        {
            BoosterFlightSnapshot s = new BoosterFlightSnapshot();
            s.Exists = v != null && v.state != Vessel.State.DEAD;
            if (!s.Exists) return s;
            s.Loaded = v.loaded;
            s.Packed = v.packed;
            s.IsActive = ReferenceEquals(v, FlightGlobals.ActiveVessel);
            s.LandedOrSplashed = v.situation == Vessel.Situations.LANDED
                                 || v.situation == Vessel.Situations.SPLASHED;
            s.OctawebStillValid = octaweb != null && octaweb.StillValid(v);

            // §B16.7 step 3's settle clock — started on the first landed tick, never re-started.
            if (s.LandedOrSplashed)
            {
                if (landedSinceUT < 0.0) landedSinceUT = Now();
                s.SinceLandedS = Now() - landedSinceUT;
            }
            else landedSinceUT = -1.0;
            return s;
        }

        // =========================================================================================
        // THE FLIGHT TICK — build the inputs, run the script, dispatch what may be dispatched
        // =========================================================================================
        static void Fly(Vessel v, BoosterFlightSnapshot snap)
        {
            CelestialBody body = v.mainBody;
            Vector3d upW = (body != null) ? (v.CoM - body.position).normalized : (Vector3d)v.transform.up;
            Vector3d srfVel = v.srf_velocity;
            double speed = srfVel.magnitude;
            double descent = -Vector3d.Dot(srfVel, upW);            // + = descending
            double alt = v.radarAltitude;
            double massKg = v.totalMass * 1000.0;
            double r = (body != null) ? (v.CoM - body.position).magnitude : 0.0;
            double mu = (body != null) ? body.gravParameter : 0.0;
            double g = (r > 1.0 && mu > 0.0) ? mu / (r * r) : 9.80665;

            BoosterInputs bi = new BoosterInputs();
            bi.Valid = true;
            bi.SurfaceVelocity = new Vec3(srfVel.x, srfVel.y, srfVel.z);
            bi.Up = new Vec3(upW.x, upW.y, upW.z);
            bi.AltitudeM = alt;
            bi.SpeedMps = speed;
            bi.DescentSpeedMps = descent;
            bi.Profile = profile;

            // ⛔ THE HOVERSLAM SOLVE reads LIVE thrust and LIVE mass — never a pre-computed schedule
            // (S48 §2.5's RO trap). Thrust comes from the BOUND modules, not a part walk.
            double centreN = MaxThrustN(EngineRole.OctawebCentre);
            double threeN = MaxThrustN(EngineRole.OctawebThree);
            HoverslamInputs land = new HoverslamInputs();
            land.AltitudeM = alt;
            land.DescentSpeedMps = descent;
            land.ThrustAccelMps2 = massKg > 1.0 ? (centreN > 0.0 ? centreN : threeN) / massKg : 0.0;
            land.GravityMps2 = g;
            land.TerminalSpeedMps = descent > 1.0 ? descent : 100.0;   // measured proxy, as the deleted glue had it
            land.DeadTimeS = 0.0;    // [UN-CONVERGED] the ullage dead-fall is W5's to establish, not this host's
            land.SpoolS = 0.0;       // instant-spool Merlin
            bi.Land = land;

            // ⛔ OCT6 — THE SECOND BANK, so `Hoverslam.EnginesFor` can be asked the question it exists to
            // answer (owner ruling: three engines brake, one flies the touchdown, and the shed point is
            // *"comput[ed] from current hover slam solver"*). Built the SAME way as `land` — LIVE thrust
            // off the bound `ThreeLanding` module over LIVE mass — so the S48 §2.5 trap stays sidestepped
            // for both banks alike.
            // ⛔ SUPPLIED ONLY WHEN **BOTH** BANKS WERE MEASURED. `land` falls back to `threeN` when the
            // centre module reports nothing, so supplying `LandThree` unconditionally could hand the
            // solver the same bank twice and let it "decide" between one measurement and itself. Left
            // unsupplied (`ThrustAccelMps2 == 0`), the FSM's shed is INERT and the landing burn flies
            // `CenterOnly` throughout — the pre-OCT6 behaviour, which is the right thing to fly when we
            // cannot see both banks.
            if (centreN > 0.0 && threeN > 0.0 && massKg > 1.0)
            {
                HoverslamInputs landThree = land;
                landThree.ThrustAccelMps2 = threeN / massKg;
                bi.LandThree = landThree;
            }

            // ⚠ NO AIM POINT EXISTS (see the header block). Everything target-derived stays ZERO, so the
            // FSM's own refusals fire honestly instead of steering at an invented coordinate.
            bi.Fin = new GridFinInputs();
            bi.AllNominal = false;
            bi.OffsetToMissM = 0.0;
            bi.TargetBearing = Vec3.Zero;
            bi.DownrangeErrM = 0.0;
            bi.InitialDownrangeErrM = 0.0;
            bi.PayloadMassKg = 0.0;      // §4.3's correction stays inert (BoosterDescent Q2)

            // The carried state — the shaper needs continuity across ticks or it is not a shaper.
            bi.CommandedForward = commandedForward;
            bi.CommandedThrottle = commandedThrottle;
            bi.LandingShedLatched = landingShed;      // OCT6 — the shed latch is carried state, like the above
            bi.DtS = TimeWarp.fixedDeltaTime;

            // ⛔ THE REAL FACING, so the flip's LEAD GATE tells the truth. `ReferenceTransform.up` is the
            // direction thrust pushes a bottom-engined stack — the same convention `AimForward` uses.
            // ⭐ Register W24 landed the steering law, so the vehicle now actually tracks this — the flip's
            // lead gate can open and the reported flip can complete, exactly as designed.
            Transform rt = v.ReferenceTransform;
            if (rt != null) { Vector3 f = rt.up; bi.Facing = new Vec3(f.x, f.y, f.z); }

            // ⛔ THE ULLAGE GATE, HELD CLOSED unless a real source says otherwise (register W5).
            bi.Ullaged = UllageSettled != null && SafeUllage(v);

            // §B16.3's ignition budget, read LIVE off the bound modules. 0 = not supplied → inert guard.
            bi.IgnitionsThreeLanding = IgnitionsOf(EngineRole.OctawebThree);
            bi.IgnitionsCentreOnly = IgnitionsOf(EngineRole.OctawebCentre);

            // ---- RUN THE SCRIPT -----------------------------------------------------------------
            BoosterCommand c = BoosterDescent.Guide(bi, phase);
            phase = c.Phase;
            commandedForward = c.AimForward;
            commandedThrottle = c.Throttle;
            // OCT6 — one way; `Guide()` never clears it.
            // ⭐ OCT4 ASKED WHETHER THIS MAY ADVANCE ON A **BLOCKED** TICK, since it sits ahead of the
            // gate and so can record "we shed" on a tick where the banks never swapped. **It not only may,
            // it MUST**, and that is provable rather than a matter of taste:
            //   1. The latch records a DECISION ("the solver has asked for one engine"), not a physical
            //      state ("the banks have swapped"). It has to. `AllowedRoleForPhase` uses it to name the
            //      legal bank for the command being gated, so on the shed tick itself the banks have NOT
            //      yet moved — a latch meaning "physically shed" would make the gate refuse the very
            //      command that performs the shed, and the shed could never happen at all.
            //   2. Dropping it on a blocked tick would UN-LATCH the FSM: `Guide()` re-evaluates
            //      `Hoverslam.EnginesFor` only when `!s.LandingShedLatched` (`BoosterDescent.cs`), so the
            //      next tick would re-demand three engines. That is precisely the chatter OCT6's
            //      mutation run measured at 8 ticks (0.8 s) after the shed, and every flip of it is a real
            //      shutdown plus a real re-ignition mid-brake.
            // So the latch follows this file's stated convention — `phase`, `AimForward` and the steering
            // law all advance regardless of dispatch — and it is load-bearing that it does. The
            // re-detection is what self-corrects: `currentRole` does NOT advance on a blocked tick, so the
            // change is simply re-detected and actuated on the next unblocked one.
            landingShed = c.LandingShedLatched;
            AimForward = c.AimForward;               // reported EVERY tick, dispatched or not.
            Refusal = c.Refusal;

            // ---- register W24: THE STEERING LAW -----------------------------------------------------
            // Convert `AimForward` into per-axis pitch/yaw/roll DEGREES (glue, needs Quaternion/Transform)
            // and hand them, with the live body rates, to the pure law. Computed and REPORTED every tick
            // regardless of whether anything may be dispatched — same rule `AimForward` has always
            // followed — so a screen or the BlackBox can show what was decided even while it is blocked.
            double pitchErrDeg, yawErrDeg, rollErrDeg;
            AttitudeError(rt, c.AimForward, out pitchErrDeg, out yawErrDeg, out rollErrDeg);
            Vector3 rateDps = v.angularVelocity * Mathf.Rad2Deg;   // x=pitch, y=roll, z=yaw (VesselData T13b)

            BoosterSteerInputs steerIn = new BoosterSteerInputs();
            steerIn.PitchErrDeg = pitchErrDeg; steerIn.YawErrDeg = yawErrDeg; steerIn.RollErrDeg = rollErrDeg;
            steerIn.PitchRateDps = rateDps.x; steerIn.YawRateDps = rateDps.z; steerIn.RollRateDps = rateDps.y;
            BoosterSteerCommand steer = BoosterSteer.Steer(steerIn);

            SteerPitch = steer.Pitch; SteerYaw = steer.Yaw; SteerRoll = steer.Roll;
            steerPitchDeadbanded = steer.PitchDeadbanded;
            steerYawDeadbanded = steer.YawDeadbanded;
            steerRollDeadbanded = steer.RollDeadbanded;
            steerDeadbandDeg = steer.DeadbandDegApplied;

            // ---- MAY ANYTHING GO OUT? ------------------------------------------------------------
            // OCT3: the phase gate reads the SAME decode `Dispatch` uses (`CommandedRole`), so a command
            // `Blocked` lets through and one `Dispatch` would actuate never disagree on which bank it is.
            //
            // ⛔ OCT4 FIX (2026-09-05). This call used to name `c.Phase` and the decoded role BY HAND and
            // omit `c.LandingShedLatched`, so the gate ran on that parameter's default — `false`, "not yet
            // shed". OCT6's shed was therefore refused `WrongEngineForPhase` on EVERY tick after the latch
            // raised, permanently, and the three-engine bank would have burned to the ground at its last
            // throttle. `BlockedFor` takes the whole command and reads all three fields from it, so the
            // gate and the FSM cannot be handed a different tick's answer or be missing one.
            double sep = SeparationM(v);
            BoosterCommandBlock block = BoosterHostPlan.BlockedFor(Actuate, snap, sep, Now() - bindUT, c);
            lastBlock = block;                                  // BB9: the stable reason, beside the prose
            BlockNote = BoosterHostPlan.Annunciation(block);

            if (block == BoosterCommandBlock.None) Dispatch(v, c, steer);
            else
            {
                // ⛔ RELEASE THE AXES — do not write a zero. And do NOT shut an engine that is already
                // burning: the realistic mid-flight block is `Packed`, where we have no control path
                // anyway, and §B16.3 is explicit that commanding zero mid-burn is an instant shutdown
                // whose relight costs a spool this vehicle cannot afford. `Release` shuts what we lit.
                //
                // ⚠ OCT4 JUDGED THE FIVE BLOCK REASONS SEPARATELY — they are not one case, and "leave the
                // lit bank burning" is right for four of them for DIFFERENT reasons:
                //   • `NotArmed`  — `Actuate` is false, so no dispatch ever ran and nothing of ours is lit.
                //   • `HoldOff`   — pre-ignition by construction (≤10 s from bind / ≤500 m from the stack).
                //   • `Packed`    — KSP runs no control path: we could not shut it if we wanted to.
                //   • `NoOctaweb` — UNREACHABLE HERE. `Tick` runs `StopReason` first and `!OctawebStillValid`
                //     returns `BindLost`, which `Release`s before `Fly` is ever called; and `Release` skips
                //     its own shutdown for the same reason. A stale table means the module references may
                //     belong to another vessel, so shutting is not merely useless, it is FORBIDDEN — that
                //     is the never-touch-the-Dragon rule. The branch in `Blocked` is defensive redundancy
                //     for direct callers (the tests), not a live path.
                //   • `WrongEngineForPhase` (OCT3) — ⚠ THE ONE THAT IS NOT OBVIOUSLY RIGHT. A full control
                //     path exists and a bank is burning, and we hold it at its last throttle on a LOGIC
                //     refusal. Brief on a transient disagreement, unbounded if the refusal is standing.
                //     Whether a standing `WrongEngineForPhase` should hold, shut, or abort is a POLICY the
                //     owner has not ruled on; OCT4 does not decide it. Logged as a register stray + a
                //     C1.14 question. (The one standing case that DID exist — the shed refused forever —
                //     was a wiring defect and is fixed above, not a policy question.)
                //
                // ⚠ AND NOTE WHAT IS NOT ROLLED BACK HERE: `phase`, `AimForward`, the steering law AND
                // OCT6's shed latch all advanced before this branch. For the latch that is not merely
                // tolerable, it is REQUIRED — see the note at its assignment above.
                fbwOwned = false; fbwThrottle = 0.0; fbwUllage = false;
                fbwPitch = 0.0; fbwYaw = 0.0; fbwRoll = 0.0;
            }

            // ---- [[OCT11]]: THE COMMANDED-VS-LIT DIVERGENCE, EVERY TICK, DISPATCHED OR NOT. ------------
            // Read PER-BANK off the already-bound module (`octaweb.For`, no search — §B16.4 step 2), which
            // is strictly more precise than the vessel-wide `eng_ignited` count. This runs regardless of
            // `block` on purpose: a bank commanded on an earlier, unblocked tick can still be dark on a
            // LATER tick that is itself blocked (e.g. `Packed`), and the divergence must not appear to
            // clear just because this tick's dispatch didn't run. It also runs regardless of RESOLUTION
            // timing — a bank that lights several ticks after being commanded (ullage settling) clears the
            // flag the same tick it actually lights, with no retry logic of this file's own.
            CommandedNotIgnited = BoosterHostPlan.CommandedNotIgnited(currentRole, BankIgnited(currentRole));
            CommandedNotIgnitedNote = CommandedNotIgnited
                ? BoosterHostPlan.AnnunciationCommandedNotIgnited(currentRole) : null;

            Report(v, c, block, sep);
        }

        /// <summary>
        /// Register W24's frame conversion — `AimForward` (world, unit) to per-axis pitch/yaw/roll ERROR
        /// degrees, in the vehicle's own control frame. Reuses, freshly written, the ONE piece of the
        /// deleted `AttitudeController.cs` that R1 §3.2 names as reference independent of its gains:
        /// *"current = ReferenceTransform.rotation * Euler(-90,0,0) ... yaw NEGATED"* — no code copied,
        /// only the documented formula. The vehicle's OWN current roll reference is used as
        /// `LookRotation`'s "up" (the same convention), which is what makes the returned roll error ~0 by
        /// construction: `AimForward` is a single direction and cannot define a roll target on its own,
        /// and inventing one here would be manufacturing a failure mode the guidance never asked for.
        /// </summary>
        static void AttitudeError(Transform rt, Vec3 aimWorld, out double pitchErrDeg, out double yawErrDeg,
                                   out double rollErrDeg)
        {
            pitchErrDeg = 0.0; yawErrDeg = 0.0; rollErrDeg = 0.0;
            if (rt == null) return;
            Vector3 aim = new Vector3((float)aimWorld.X, (float)aimWorld.Y, (float)aimWorld.Z);
            if (aim.sqrMagnitude < 1e-9f) return;

            Quaternion current = rt.rotation * Quaternion.Euler(-90f, 0f, 0f);   // nose -> +Z, LookRotation convention
            Vector3 rollRef = current * Vector3.up;                              // own current roll ref: rollErr ~ 0
            Quaternion requested = Quaternion.LookRotation(aim, rollRef);
            Quaternion delta = Quaternion.Inverse(current) * requested;
            Vector3 euler = delta.eulerAngles;

            pitchErrDeg = ClampPi(euler.x);
            rollErrDeg = ClampPi(euler.z);
            yawErrDeg = -ClampPi(euler.y);
        }

        static double ClampPi(float deg)
        {
            double d = deg;
            while (d > 180.0) d -= 360.0;
            while (d < -180.0) d += 360.0;
            return d;
        }

        // =========================================================================================
        // DISPATCH — the only place a command reaches the vehicle, and it reaches ONE vessel
        // =========================================================================================
        static void Dispatch(Vessel v, BoosterCommand c, BoosterSteerCommand steer)
        {
            // ---- ENGINES. `EnginesLit` is the authority, never the mode: `EngineMode == 0` is BOTH
            // `ModeAllEngines` AND the struct's default (the command struct says so itself), so the mode
            // alone can never light anything. `CommandedRole` is the single decode.
            EngineRole want = BoosterHostPlan.CommandedRole(c.EnginesLit, c.EngineMode);
            if (want != currentRole)
            {
                SelectEngineSet(currentRole, want);
                // ⚠ OCT4: `currentRole` records what we COMMANDED, not what is physically ignited, and it
                // advances even when the activate did not take (an exception, or a RealFuels ullage
                // refusal — flight Crew-2_20260829_144114, *"eng_ignited=0 whole descent"*). It is right
                // for its job, which is "do not command the same set twice"; it is NOT a reading of the
                // vehicle, and nothing may treat it as one. Logged as a stray, not changed here.
                currentRole = want;
            }

            // ---- THROTTLE. Both paths, one number (see the header's "NOT PROVEN IN FLIGHT" note).
            // ⚠ OCT4 — THE ORDER WITHIN THE SWAP FRAME, TRACED AND FOUND BENIGN. `SelectEngineSet` runs
            // FIRST, so on a 3→1 shed the centre bank is `Activate()`d BEFORE its `independentThrottle`
            // is set here, and the shut three-engine bank's stale `independentThrottle = true` is cleared
            // only afterwards. Neither matters, for one reason: both calls happen inside a single
            // `Dispatch`, i.e. one `FixedUpdate`, with NO physics integration between them, so no engine
            // step ever observes the intermediate state. And the intermediate state is benign anyway —
            // a bank whose `independentThrottle` is still false follows `FlightCtrlState.mainThrottle`,
            // which `Fbw` has been writing to the previous tick's landing-burn throttle (never zero, never
            // full), and `independentThrottle` on an engine that has just been `Shutdown()` scales
            // nothing. Worst case, if Unity happened to run a `ModuleEngines` physics step ahead of
            // `BoosterHostAddon.FixedUpdate`, the newly-lit bank spends ONE frame on the previous tick's
            // throttle — bounded, and far smaller than the spool transient OCT6 measured at 8 ticks.
            double thr = want == EngineRole.None ? 0.0 : c.Throttle;
            ApplyThrottle(want, thr);
            fbwThrottle = thr;

            // ---- register W24: ATTITUDE. Gimbal steering reaches the octaweb automatically from
            // `s.pitch`/`s.yaw`/`s.roll` — `ModuleGimbal` (and, in AeroDescent, the grid fins' own control
            // surface) reads the SAME `FlightCtrlState` axes, so writing them once here is the whole
            // dispatch; no separate gimbal call exists or is needed.
            fbwPitch = steer.Pitch; fbwYaw = steer.Yaw; fbwRoll = steer.Roll;
            fbwOwned = true;                      // a dispatch ran — the callback may write this frame

            // ---- ENGINE-OUT DIFFERENTIAL THROTTLE (B3). With <2 live modules in the role (today's
            // octaweb: one multi-nozzle `ModuleEngines` per set) this is a no-op that HOLDS the thrust
            // limiters at 100% and leaves steering to the gimbal — exactly what a single-module role
            // needs. Vec3.Zero: this call is not asking for torque, only for the limiters to stay full;
            // wiring a live torque demand through here is engine-OUT contingency, a separate concern
            // (§B12.5(4)) this task does not add. `Feasible=false` is logged, never treated as fatal — the
            // gimbal path above is entirely independent of it.
            if (want != EngineRole.None)
            {
                bool feasible = Actuator.BalanceOctawebThrust(v, want, Vec3.Zero);
                if (!feasible && LogGate.First("booster-host-thrustbalance:" + want))
                    Debug.LogWarning("[DragonScreen] booster host: BalanceOctawebThrust reports infeasible for "
                                      + want + " (informational — gimbal steering is unaffected)");
            }
            else
            {
                // ---- register W24: RCS is the ONLY rotation authority with no engine lit (Flip / Coast /
                // AeroDescent) — the gimbal has nothing to deflect. Idempotent and per-vessel (Actuator.cs
                // header), so this never reaches the Dragon.
                Actuator.EnableRcs(v);
            }

            // ---- ULLAGE. §B16.3: settle before EVERY relight. `EnableRcs` does the per-thruster enable
            // AND the vessel-level master KSP requires for translation to actuate — both per-vessel, so
            // neither reaches the Dragon.
            fbwUllage = c.UllageRcs;
            if (c.UllageRcs) Actuator.EnableRcs(v);

            // ---- AERO SURFACES + LEGS. Direct part control (§B12.7), latched to a single call each.
            // Proven on a non-active vessel in flight Crew-2_20260829_144114 ("grid fins").
            if (c.DeployFins && !finsOut) { Actuator.DeployGridFins(v); finsOut = true; }
            if (c.DeployLegs && !legsDown) { Actuator.DeployLegs(v); legsDown = true; }
        }

        /// <summary>
        /// ⛔ SELECT ABSOLUTELY, WHILE OFF, BY `engineID`. Shut every octaweb set that is not wanted, THEN
        /// activate the wanted one if it is not already lit. Never `NextEngineMode`, never
        /// `ModuleEngineConfigs`, never `selectedIndex` (§B16.3 / §B16.4).
        ///
        /// ⭐ **SHUT-BEFORE-LIGHT IS FORCED BY THE PART'S GEOMETRY, NOT CHOSEN.** The three banks are
        /// NESTED SUBSETS of one nine-nozzle octaweb (`BoosterHostPlan`'s §4c block has the dump rows and
        /// the transform names), so the centre nozzle belongs to BOTH `ThreeLanding` AND `CenterOnly` and
        /// those two can never burn at once. Light-then-shut was never an available option, and OCT6's
        /// 3→1 shed therefore NECESSARILY crosses a thrust discontinuity ([[OCT9]]). Nothing in this
        /// method can remove that; re-ordering it would only make the overlap illegal instead of absent.
        ///
        /// ⛔ **THE ORDER IS DECIDED IN `pure/`** — `BoosterHostPlan.EngineSwitchSteps` — so the transition
        /// table is asserted headlessly (OCT4) and a future re-order turns the suite red. This method is
        /// its interpreter and holds no sequencing rule of its own.
        ///
        /// ⛔ **NEVER RE-LIGHT A LIT SET.** The rule stands on its own merits — a re-light is a real
        /// shutdown plus a real spool mid-flight — and is enforced twice over: `Dispatch` calls this only
        /// on a role CHANGE (`EngineSwitchSteps` returns nothing when `from == to`), and the activate is
        /// additionally guarded on `!EngineIgnited`.
        /// ⚠ **THE IGNITION COUNT IS UNMEASURED.** This comment used to justify the rule with *"each set
        /// carries `ignitions = 1` in the dump"*. That count is exactly the premise [[BB8]] exists to
        /// settle: `Crew2_Patches/F9_Engines_InstantSpool.cfg` sets `%ignitions = -1` (RealFuels:
        /// unlimited) and the ModuleManager ConfigCache carries −1 nine times; only a PRELAUNCH pad read
        /// ever returned 1, and nobody has sampled it in flight. Do not build a budget guard on it until
        /// BB8 has measured it. (Two more sites state the same unverified count as fact —
        /// `pure/BoosterDescent.cs:52-57` and this file's header at `:89-90`; both are [[OCT7]]'s to fix,
        /// deliberately untouched here.)
        /// </summary>
        static void SelectEngineSet(EngineRole from, EngineRole want)
        {
            BoosterHostPlan.OctawebStep[] steps = BoosterHostPlan.EngineSwitchSteps(from, want);
            if (steps.Length == 0) return;                       // `from == want` — nothing to actuate

            for (int i = 0; i < steps.Length; i++)
            {
                if (steps[i].Kind == BoosterHostPlan.OctawebStepKind.Shutdown) Shut(steps[i].Role);
                else Light(steps[i].Role);
            }

            if (want == EngineRole.None)
                Debug.Log("[DragonScreen] booster engines → OFF (all sets shut)");
        }

        /// <summary>Shut one bank if it is actually burning. A bank that is not ignited is left alone —
        /// the sweep deliberately names banks we do not believe are lit (see `EngineSwitchSteps`).</summary>
        static void Shut(EngineRole role)
        {
            ModuleEngines e = octaweb.For(role);
            if (e == null || !e.EngineIgnited) return;
            try { e.Shutdown(); }
            catch (Exception ex) { Debug.LogWarning("[DragonScreen] booster engine shutdown failed: " + ex.Message); }
        }

        /// <summary>Activate one bank by its bound `engineID` module, once, and only while it is off.</summary>
        static void Light(EngineRole role)
        {
            ModuleEngines e = octaweb.For(role);
            if (e == null)
            {
                Debug.LogWarning("[DragonScreen] booster host: no bound module for " + role + " — not commanding it");
                return;
            }
            if (!e.EngineIgnited)
            {
                try { e.Activate(); }
                catch (Exception ex) { Debug.LogWarning("[DragonScreen] booster engine activate failed: " + ex.Message); }
            }
            Debug.Log("[DragonScreen] booster engine set → " + role + " (\"" + e.engineID
                      + "\", activated by engineID — never NextEngineMode)");
        }

        /// <summary>
        /// §B16.3 names `independentThrottle` + `independentThrottlePercentage` as the throttle mechanism,
        /// and `docs/reference/craftdump.csv` confirms both fields on all three `ModuleEnginesRF`. The
        /// FLIGHT-PROVEN path, though, is `FlightCtrlState.mainThrottle` (flight Crew-2_20260829_144114).
        /// **We write both, with the SAME value**, so whichever the module honours it gets the same
        /// number and the two cannot disagree. Sets that are not commanded are returned to the vessel
        /// throttle so nothing is left latched.
        /// ⚠ The FSM has already applied the engine's own measured minimum-throttle floor (§B16.3: never
        /// command zero mid-burn). This method does not second-guess it — it relays.
        /// </summary>
        static void ApplyThrottle(EngineRole want, double thr)
        {
            SetIndependent(EngineRole.OctawebAll, want, thr);
            SetIndependent(EngineRole.OctawebThree, want, thr);
            SetIndependent(EngineRole.OctawebCentre, want, thr);
        }

        static void SetIndependent(EngineRole role, EngineRole want, double thr)
        {
            ModuleEngines e = octaweb.For(role);
            if (e == null) return;
            try
            {
                if (role == want)
                {
                    e.independentThrottle = true;
                    e.independentThrottlePercentage = (float)(Clamp01(thr) * 100.0);
                }
                else if (e.independentThrottle)
                {
                    e.independentThrottle = false;
                    e.independentThrottlePercentage = 0f;
                }
            }
            catch (Exception ex) { Debug.LogWarning("[DragonScreen] booster throttle set failed: " + ex.Message); }
        }

        // =========================================================================================
        // THE CONTROL-PIPELINE HOOK — throttle, ullage translation, and (register W24) attitude.
        // =========================================================================================
        static void Hook(Vessel v)
        {
            if (hooked || v == null) return;
            v.OnFlyByWire += Fbw;
            hooked = true;
        }

        static void Unhook()
        {
            if (!hooked || bound == null) { hooked = false; return; }
            try { bound.OnFlyByWire -= Fbw; }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] booster host unhook failed: " + e.Message); }
            hooked = false;
        }

        /// <summary>
        /// KSP hands this the BOUND vessel's OWN `FlightCtrlState` — per-vessel, so nothing written here
        /// can reach the Dragon. Proven to fire for a loaded, non-active, unpacked craft (flights 134620
        /// and Crew-2_20260829_144114 — see the header).
        ///
        /// ⛔ IT WRITES AT MOST FIVE THINGS: the throttle, the ullage settle translation, and — register
        /// W24 — `pitch`/`yaw`/`roll`, all gated identically on `fbwOwned` saying a dispatch actually ran
        /// this frame. `ModuleGimbal` and the grid fins' own control surface both read these SAME axes
        /// automatically; nothing else in this file "drives" them. Unowned, it writes NOTHING AT ALL —
        /// §14.4(a): leaving an axis untouched leaves whatever the vessel would have done alone, which is
        /// the honest state, and is never the same as commanding a zero.
        /// </summary>
        static void Fbw(FlightCtrlState s)
        {
            try
            {
                if (s == null || bound == null || !fbwOwned) return;
                s.mainThrottle = (float)Clamp01(fbwThrottle);
                if (fbwUllage) s.Z = (float)Clamp1(UllageFwdSign * UllageTranslate);
                s.pitch = (float)Clamp1(fbwPitch);
                s.yaw = (float)Clamp1(fbwYaw);
                s.roll = (float)Clamp1(fbwRoll);
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] booster fly-by-wire write failed: " + e.Message); }
        }

        // =========================================================================================
        // RELEASE — cleanly, once, for a stated reason
        // =========================================================================================
        public static void Release(string why)
        {
            if (bound == null) { hooked = false; return; }
            Vessel v = bound;

            // Undo OUR OWN actuation and nothing else: shut the set we lit, and hand the throttle back to
            // the vessel. Leaving a lit, independently-throttled engine on a vessel nobody is driving is
            // strictly worse than a clean shutdown.
            try
            {
                if (octaweb != null && octaweb.StillValid(v))
                {
                    if (currentRole != EngineRole.None) SelectEngineSet(currentRole, EngineRole.None);
                    ApplyThrottle(EngineRole.None, 0.0);
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] booster host release cleanup failed: " + e.Message); }

            Unhook();

            Debug.Log("[DragonScreen] booster host RELEASED — " + (why ?? "no reason given")
                      + " (phase " + phase + ", \"" + (v != null ? v.vesselName : "?") + "\")");

            bound = null; octaweb = null; hooked = false;
            phase = BoosterPhase.Idle;
            commandedForward = Vec3.Zero; commandedThrottle = 0.0;
            landingShed = false;                 // OCT6
            currentRole = EngineRole.None;
            finsOut = false; legsDown = false;
            fbwOwned = false; fbwThrottle = 0.0; fbwUllage = false;
            fbwPitch = 0.0; fbwYaw = 0.0; fbwRoll = 0.0;
            steerPitchDeadbanded = false; steerYawDeadbanded = false; steerRollDeadbanded = false;
            steerDeadbandDeg = 0.0;
            landedSinceUT = -1.0;
            AimForward = Vec3.Zero; Refusal = null; BlockNote = null; lastBlock = BoosterCommandBlock.None;
            SteerPitch = 0.0; SteerYaw = 0.0; SteerRoll = 0.0;
            CommandedNotIgnited = false; CommandedNotIgnitedNote = null;   // [[OCT11]] — released, nothing commanded
            lastBindVerdict = BoosterBind.NoVessel;
        }

        // =========================================================================================
        // REPORTING — §14.4(a) honest, rate-limited (the S40 lesson: a standing line must not flood)
        // =========================================================================================
        static void Report(Vessel v, BoosterCommand c, BoosterCommandBlock block, double sepM)
        {
            double now = Now();
            if (now - lastLogUT < LogIntervalS) return;
            lastLogUT = now;

            string aim = "aim(" + c.AimForward.X.ToString("F3") + "," + c.AimForward.Y.ToString("F3")
                       + "," + c.AimForward.Z.ToString("F3") + ")";
            string steer = "steer(p=" + SteerPitch.ToString("F2") + (steerPitchDeadbanded ? "db" : "")
                          + " y=" + SteerYaw.ToString("F2") + (steerYawDeadbanded ? "db" : "")
                          + " r=" + SteerRoll.ToString("F2") + (steerRollDeadbanded ? "db" : "") + ")";

            Debug.Log("[DragonScreen] booster " + c.Phase + "/" + c.Mode
                      + " alt=" + v.radarAltitude.ToString("F0") + "m"
                      + " spd=" + v.srf_velocity.magnitude.ToString("F0") + "m/s"
                      + " sep=" + (sepM > 0.0 ? (sepM / 1000.0).ToString("F1") + "km" : "?")
                      + " set=" + BoosterHostPlan.CommandedRole(c.EnginesLit, c.EngineMode)
                      + " thr=" + fbwThrottle.ToString("F2")
                      + (c.UllageRcs ? " ULLAGE" : "")
                      + (c.DeployFins ? " FINS" : "") + (c.DeployLegs ? " LEGS" : "")
                      + " | " + aim + " " + steer + (fbwOwned ? " (LIVE)" : " ⛔ ATT UNCMD (not dispatched)")
                      + (block != BoosterCommandBlock.None
                            ? " | NO COMMAND: " + BoosterHostPlan.Annunciation(block) : "")
                      + (c.Refusal != null ? " | FSM REFUSED: " + c.Refusal : ""));
        }

        // =========================================================================================
        // small helpers
        // =========================================================================================
        static double Now()
        {
            try { return Planetarium.GetUniversalTime(); } catch { return 0.0; }
        }

        /// <summary>Booster ↔ active-vessel separation, metres, or 0 when it cannot be measured.
        /// ⚠ L6's bug was differencing the active vessel against ITSELF (`sep 0 km` every flight); the
        /// real pair is the BOUND BOOSTER against the ACTIVE vessel, which is what this does.</summary>
        static double SeparationM(Vessel v)
        {
            Vessel active = FlightGlobals.ActiveVessel;
            if (active == null || v == null || ReferenceEquals(active, v)) return 0.0;
            try { return (v.CoM - active.CoM).magnitude; }
            catch { return 0.0; }
        }

        /// <summary>[[OCT11]] Per-bank ignition reading, off the already-bound module — cheap, no search
        /// (§B16.4 step 2). `EngineRole.None` never makes this matter: `CommandedNotIgnited` short-
        /// circuits on it, so a missing module only reads as "not ignited" for a bank we actually asked
        /// for, which is the honest answer — we cannot tell it is lit if we cannot even find it.</summary>
        static bool BankIgnited(EngineRole role)
        {
            if (role == EngineRole.None) return true;
            ModuleEngines e = octaweb != null ? octaweb.For(role) : null;
            return e != null && e.EngineIgnited;
        }

        static double MaxThrustN(EngineRole role)
        {
            ModuleEngines e = octaweb != null ? octaweb.For(role) : null;
            if (e == null) return 0.0;
            try { return e.maxThrust * 1000.0; } catch { return 0.0; }
        }

        /// <summary>§B16.3's finite ignition budget, read off the LIVE module. `ignitions` is RealFuels'
        /// (`ModuleEnginesRF`) KSPField — the craft dump lists it as *"Ignitions Remaining"* — so it is
        /// read through KSP's own `BaseField` table rather than by referencing an RO assembly. 0 = could
        /// not be read, which the FSM treats as "not supplied" and leaves the guard inert.</summary>
        static int IgnitionsOf(EngineRole role)
        {
            ModuleEngines e = octaweb != null ? octaweb.For(role) : null;
            if (e == null) return 0;
            try
            {
                BaseField f = e.Fields["ignitions"];
                if (f == null) return 0;
                object val = f.GetValue(e);
                if (val == null) return 0;
                int n = Convert.ToInt32(val);
                return n > 0 ? n : 0;
            }
            catch { return 0; }
        }

        static bool SafeUllage(Vessel v)
        {
            try { return UllageSettled(v); }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] booster ullage source threw: " + e.Message + " — treating as NOT settled");
                return false;
            }
        }

        static double Clamp01(double d)
        {
            if (double.IsNaN(d)) return 0.0;
            return d < 0.0 ? 0.0 : (d > 1.0 ? 1.0 : d);
        }

        static double Clamp1(double d)
        {
            if (double.IsNaN(d)) return 0.0;
            return d < -1.0 ? -1.0 : (d > 1.0 ? 1.0 : d);
        }
    }
}

// ============================================================================================
// ## Open questions for the owner
// ============================================================================================
// Per C1.14, written into this task's own deliverable, each with options and a recommendation. **W23
// decided none of them and proceeded past none** — every one of them is inert by default today.
//
// ---- Q1. THE HOST IS BUILT AND WIRED, BUT `Actuate` DEFAULTS TO **FALSE** -- ⭐ RESOLVED BY W24 -----
// **Resolution (2026-09-04, owner ruling relayed via the overseer on W24's resume): option 1.** W24
// built the steering law (`pure/BoosterSteer.cs` + this file's attitude wiring) and flips `Actuate` to
// `true` as part of its own gate, exactly as this question's recommendation named. The original
// situation and the three options are kept below for the record.
// **Situation (as it stood before W24).** The owner's direction is *"as soon as the booster gets
// dropped it runs its script."* It does: the host binds at separation, ticks `Guide()` every physics
// frame on live vessel state, advances the phases and reports. What it did NOT do by default was WRITE
// — throttle, engine activation, RCS and part actions were all built, wired and reviewable behind one
// named flag that was off. The reason was not caution in the abstract: this task's scope explicitly
// excluded the steering law, and a booster that lights an engine with an uncontrolled attitude is
// flight 194334 — `8225df7` finding A1, *"fires thr=1.0 0.3 s after MECO at 'sep 0 km', attitude
// diverges 2→85 deg, LOST in ~10 s — and its 0-km burn kicks the upper stage"* — which is also the exact
// non-interference failure the owner asked W23 to prevent.
// **Options (historical).**
//   1. Leave `Actuate = false`; W24 (the steering law) flips it as part of its own gate. ⭐ CHOSEN.
//   2. Owner flips it now, with attitude uncontrolled.
//   3. Flip it PARTIALLY — fins/legs/RCS but never engines (`LetFall`, 2026-08-30) as a third state.
//
// ---- Q2. THE HOST REFUSES TO BIND THE ACTIVE VESSEL, SO FOCUSING THE BOOSTER STOPS IT ------------
// **Situation.** `BoosterHostPlan.RequireNonActive` is a THIRD independent guarantee that the Dragon can
// never be bound (on top of "carries no pod" and "carries an S1 part"). §B16.7 is categorical that focus
// never leaves the upper stage, so on the designed protocol it costs nothing. But if the owner switches
// focus to the booster to watch it land, the host releases and the script stops.
// **Options.**
//   1. Keep it. It is free under §B16.7 and it is the strongest possible statement of "never the Dragon".
//   2. Allow an active booster (bind on parts alone), so the owner can watch from the booster.
//   3. Keep the refusal but make focusing the booster PAUSE rather than release, resuming on refocus.
// **Recommendation: (1).** §B16.7 already settled that focus stays on the upper stage; (2) trades a hard
// safety property for a viewing convenience the protocol says will not be used, and (3) adds a state
// machine for the same. If the owner does want to watch, `src/HullCams.cs` already follows the booster
// via `BoosterRecovery.Tracked` without any focus change.
//
// ---- Q3. THE SCREEN'S "AUTO BOOSTER RECOVERY" TOGGLE IS *NOT* WIRED TO THIS HOST -----------------
// **Situation.** `MissionConductor.AutoRecoverBooster` is a live DISPLAY-tab toggle
// (`ScreenPainter.cs:725`) that today does nothing. It is the obvious arm for this host — but its own
// on-screen text reads *"ARMED — after MECO the booster is focused + landed (Dragon orbit sacrificed
// this flight)"*, which describes the focus-switching design **§B16.7 superseded**: focus now never
// leaves the upper stage and no orbit is sacrificed. Wiring the host to that toggle would make a screen
// state a lie (§14.4(a)), and `src/MissionConductor.cs` belongs to **register W9**.
// **Options.**
//   1. Leave it unwired; **W9** owns the arm together with the §B16.7 PRE/focus protocol it is really
//      the switch for, and corrects the screen text in the same diff.
//   2. Wire it here and correct `ScreenPainter.cs`'s text in this diff.
//   3. Wire it here and leave the text — ⛔ not an option; that is a screen claiming something false.
// **Recommendation: (1).** The toggle's real job is steps 1/4/5 of §B16.7 (PRE on, auto-recover, PRE
// off), none of which this host owns or may own — widening ranges writes `vesselRanges` on every vessel
// including the Dragon.
//
// ---- Q4. TWO NEW HOLD-OFF CONSTANTS, BOTH UN-CONVERGED ------------------------------------------
// **Situation.** `HoldOffSeparationM = 500 m` and `HoldOffSinceBindS = 10 s` are the interlock that stops
// the host commanding thrust next to the stack. Flight 194334 gives the FAILING point (0 m, 0.3 s) and
// therefore only a lower bound; no recorded flight in this repo establishes a converged safe value, so
// both are marked [UN-CONVERGED] per §B16.8 ruling 2. Raising them costs landing propellant margin
// (a boostback that starts later starts further downrange); lowering them risks the booster and the
// stack together.
// **Options.**
//   1. Keep 500 m / 10 s as the placeholder and converge them from the first recorded re-flight, like
//      every other booster constant.
//   2. The owner sets a figure now from their own flight experience.
//   3. Derive a separation figure from the recorded stack-kick geometry — ⚠ not possible in this repo:
//      the raw flight CSVs behind 194334 were gitignored and never committed (§B16.8).
// **Recommendation: (1).** It is the standing rule for every booster constant, and the interlock is
// correct at any value in this range; only its cost is uncertain.
//
// ---- Q5 (register W24). THE STEERING LAW'S PER-AXIS SIGN IS UNVERIFIED -----------------------------
// **Situation.** `pure/BoosterSteer.cs` implements a fresh, negative-feedback control law (never
// AttitudeLoop's), and `AttitudeError()` above computes the per-axis error the SAME way R1 §3.2's
// frame-conversion formula defines it (reused as documented reference, not code). But the deleted law's
// FINAL line — the one that applied that error to `s.pitch`/`s.yaw`/`s.roll` with whatever sign made the
// feedback negative on THIS vehicle — is not recoverable (R1 §3.2 verdicts the files RECOVER-REFERENCE
// ONLY, and that line lived in `AttitudeController.cs`, one of the three). There is also no recorded
// booster ATTITUDE flight anywhere in this repo to derive it from empirically (R1 §4.2). Getting a sign
// wrong on one axis means POSITIVE feedback on that axis alone — an immediate, accelerating divergence,
// distinguishable from a merely-undertuned gain within a tick or two of telemetry.
// **This is not a not-yet-modelled quantity under §14.4(e)/(f)** — it is a control-loop sign that
// literally cannot be settled without either the deleted source (forbidden, R1 §3.2) or a flight (which
// this task's own gate is what authorizes). It is recorded here rather than guessed past silently.
// **Options.**
//   1. Fly it as built (`PitchSign = YawSign = RollSign = +1.0`) and watch the FIRST tick's telemetry —
//      `BoosterHost.SteerPitch/SteerYaw/SteerRoll` plus the recorded body rates. A wrong-signed axis
//      shows as an accelerating divergence on exactly that axis within 1-2 seconds; flip that ONE sign
//      from `PluginData/tuning.cfg` (no recompile) and re-fly.
//   2. Hold `Actuate = false` (override this task's flip from `tuning.cfg`) until the sign can be
//      verified some other way — costs another flight cycle with the booster still falling ballistically.
//   3. Recover `AttitudeController.cs`'s final application line as READ-ONLY evidence (not code) to check
//      the sign against — ⚠ R1 §3.2 verdicts the file RECOVER-REFERENCE ONLY; reading a single line for a
//      sign check is a narrower ask than resurrecting the controller, but it is still a call on a file
//      this task was told not to touch, so it is offered here rather than done.
// **Recommendation: (1).** The owner's own ruling on this resume already states *"the next flight is the
// first time this commands a real vessel"* — the sign is exactly the thing that first flight tests, the
// mitigation (a one-line, no-recompile flip per axis) is already built, and this is a KSP simulation: a
// wrong sign costs a reverted flight, not real hardware. (3) is offered because it is cheap to say no to
// explicitly rather than to leave unmentioned.
// ============================================================================================
