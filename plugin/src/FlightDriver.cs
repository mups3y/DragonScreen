// DragonScreen — FlightDriver  (KSP glue: the autopilot host, a flight-scene KSPAddon)
// ============================================================================================
// THE CONDUCTOR TICK LIVES HERE, not on the IVA screen objects. A KSPAddon(Flight) is scoped to the
// flight SCENE, so it survives the active-vessel switch a booster handover performs — the IVA (and the
// ScreenPainter on it) is destroyed the instant the Dragon stops being the active vessel, which is why
// nothing vehicle-wide may be ticked from there (see `ScreenPainter.Update`, which says so at :1002).
//
// ---- RESTORED BY W10, 2026-09-05, from `8b81816^` (59,523 B, R1 §5.2 RECOVER-CODE, "the Part-B host").
// ---- ⛔ ONLY THE READ-ONLY HALF. §B12.6 build-order step (3) — "glue driver implements the stub surfaces
// ---- read-only (report phase/engaged) — NO COMMANDS YET" — and that is the whole of this file:
//        • own the flight-scene addon and its lifecycle,
//        • own `OnFlyByWire` on the active vessel (it writes NO axis — see the hook),
//        • tick `CrewProcedureOps` with the live vessel, once per physics frame,
//        • report phase / engaged, and supply `engaged` + `ActivePhase` to `Mission.AuthoritativePhase`
//          (rule T4) — already wired at `VesselData.cs:103`, it only ever needed a real conductor,
//        • command NOTHING.
//
// ⛔ WHAT IS DELIBERATELY NOT HERE, AND WHY IT IS NOT AN OVERSIGHT. The recovered file is 980 lines and
// most of them ACTUATE: `StartLaunch`/`Ignite`/`ClampGate` (pad ignition + hold-down release),
// `TickErectorClear`, `TickLaunchHold` + `WarpTo` (time-warp), `DriveActivePhase`'s dispatch to
// `AscentControl`/`RendezvousControl`/`DockingControl`/`ReturnControl`/`BoosterControl`, the throttle /
// translation / attitude / roll authority latches those controllers write through, the RCS pulse shaper,
// the structural-g abort, the abort FX (klaxon + IVA strobe), `UpdateAbort`, FDIR's acting path and
// `FlightLog`. Every one of those needs a controller that is NOT in this tree, or is itself a command.
// §B12.8 rider (c) is explicit about the shape this takes instead: **every later Wave E / T-series line
// GROWS THIS SAME HOST by exactly the dispatch its own controller needs, one increment at a time. No Wave
// E line re-restores this file, and none of them may add a member to it speculatively.** So the members
// above come back WITH the controllers that use them, never ahead of them.
//
// ⚠ HONEST TEST COVERAGE (C1.3). `python plugin/build.py test` runs `build_plugin()` — which DOES compile
// this file, against the KSP + embedded-MechJeb references — and then `build_tests()`, which compiles and
// runs `src/pure` + `test` ONLY. So the suite proves this file COMPILES and that the pure decisions it
// composes are correct (`test/ConductorWalkTest.cs` walks the exact CrewGate→ModeManager composition
// `CrewProcedureOps.Tick` performs); it CANNOT execute this file, because a `[KSPAddon]` MonoBehaviour
// needs the game. That half is glass time, and glass time is a separate owner gate (§0).
// ============================================================================================
using System;
using UnityEngine;

namespace DragonScreen
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class FlightDriver : MonoBehaviour
    {
        static FlightDriver instance;
        Vessel boundVessel;

        // ================== THE PHASE CONTROLLER TABLE — EMPTY, AND THAT IS THE POINT ==================
        // Which mission phases this host can actually FLY. `CrewProcedureOps.ActivePhase` and `.PhaseName`
        // both gate on it, so the conductor can never publish a phase word for a phase nothing is flying —
        // rule T4's resolver then falls back to the live classifier, which is the honest answer.
        //
        // ⛔ THIS IS THE GROWTH POINT §B12.8 rider (c) DESCRIBES. An increment that lands a controller adds
        // its own phase HERE and its own dispatch in the tick, in the same diff — and the moment it does, the
        // conductor names that phase again with no change to any screen file. Adding a phase to this table
        // ahead of a controller that flies it would be exactly the half-wiring §B12.5a(iv) forbids: a phase
        // word claiming a vehicle state that is not happening.
        // ⭐ T18, 2026-09-07 — THE TABLE IS NO LONGER EMPTY. `MechConductor` flies `Ascent`: PVG steers
        // and throttles, and the conductor works the stages directly (§B8's autostage-off rule, §B12.7).
        // ⛔ GATED ON `MechConductor.Available`, NOT ON THE FILE EXISTING. A phase word is a claim about
        // the VEHICLE, and if no embedded MechJebCore resolved on this vessel then nothing is flying it
        // — so the conductor must fall back to the honest live classifier exactly as it did before T18
        // (§B12.5a(iv): never half-wire a status). One property read, no search.
        // ⚠ T20/T21 add `Docked`, `Entry` and `Drogues` HERE, each in the same diff as the controller
        // that flies it (§B12.8 rider (c)) — never ahead of one.
        // ⭐ T19, 2026-09-07 — `Phasing`, `Coast` and `Approach` join it. `MechConductor`'s on-orbit
        // executor composes the §B10.2 Maneuver-Planner operations, flies them with the Node Executor
        // and re-plans them live (§B12.4). Same gate as Ascent: `MechConductor.Available`, because a
        // phase word is a claim about the VEHICLE and no embedded core means nothing is flying it.
        public static bool HasControllerFor(MissionPhase p)
        {
            if (!MechConductor.Available) return false;
            return p == MissionPhase.Ascent      // T18
                || p == MissionPhase.Phasing     // T19 — §B9 P2 insertion trim + the phasing orbit
                || p == MissionPhase.Coast       // T19 — the free-flyer's dwell, same executor
                || p == MissionPhase.Approach;   // T19 — §B9 P3, out to the Keep-Out Sphere
        }

        public void Start()
        {
            instance = this;
            // ⛔ A NEW flight scene (fresh launch, revert-to-VAB/launch, load) must start with the conductor
            // FULLY IDLE. It holds STATIC state that survives a scene change, so without this the last
            // flight's engaged/index/return state carries onto the next vehicle and AUTO SEQUENCE is still
            // "on", mid-mission, the moment you roll out.
            ResetAll();
            Debug.Log("[DragonScreen] FlightDriver up (flight-scene conductor host, read-only) — state reset");
        }

        // The recovered ResetAll() reset eight controllers, the steering layer, the authority latches, the
        // abort state and the flight log. Exactly one of those exists in this tree.
        static void ResetAll()
        {
            CrewProcedureOps.ForceReset();
            // T18, 2026-09-07: the MechJeb-facing executor holds static state too - the bound core, the
            // ascent step, the latched launch GO. A fresh scene must start with none of it.
            MechConductor.Reset();
            // W9, 2026-09-07: the conductor holds static warp + physics-range state that survives a scene
            // change, and a stale wide range on a fresh vehicle is exactly the kind of thing nobody would
            // think to look for. It resets with everything else.
            MissionConductor.Reset();
        }

        public void OnDestroy()
        {
            if (instance == this) instance = null;
            Unbind();
        }

        // ---- the OnFlyByWire seam: bound to whichever vessel is active, so it follows a handover ----
        void Bind(Vessel v)
        {
            if (boundVessel == v) return;
            Unbind();
            if (v != null) { v.OnFlyByWire += OnFlyByWire; boundVessel = v; }
        }
        void Unbind()
        {
            if (boundVessel != null) { boundVessel.OnFlyByWire -= OnFlyByWire; boundVessel = null; }
        }

        // ⛔ WRITES NO AXIS. THIS IS NOT A STUB — IT IS THE HONEST STATE OF THE BUILD. In the recovered file
        // this hook applied a throttle / translation / attitude / roll that an active flying controller had
        // latched; there is no such controller in this tree, so there is nothing to apply and the crew keeps
        // every axis. The hook is bound anyway because the BINDING is the part that has to be right — it must
        // follow the active vessel and detach cleanly on scene exit — and because §B12.8 rider (c) has each
        // later increment add exactly the axis its own controller commands, here, one at a time.
        // ⚠ Do NOT add an axis latch to this file ahead of the controller that writes it (§14.4(a)).
        void OnFlyByWire(FlightCtrlState st)
        {
        }

        // Physics-rate tick — control cadence, not display cadence.
        public void FixedUpdate()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || !HighLogic.LoadedSceneIsFlight) return;

            try
            {
                // ⭐ THE CONDUCTOR TICKS BEFORE THE AUTO-SEQUENCE GATE, AND DELIBERATELY SO (W9, 2026-09-07).
                // §B12.8 rider (c): every later increment GROWS THIS HOST by exactly the dispatch its own
                // controller needs — this is W9's, and it is one line. It sits ABOVE the `Engaged` early
                // return because what it drives is not part of the crew's AUTO SEQUENCE: the §B16.7 physics
                // ranges are armed by the DISPLAY-tab toggle (`MissionConductor.AutoRecoverBooster`) and
                // serve a SEPARATE vessel, whose own host (`BoosterHostAddon`) ticks unconditionally for
                // the same reason. Gating the ranges on AUTO SEQUENCE would mean the booster silently
                // packs out whenever the crew flew the Dragon by hand — the owner's direction is *"as soon
                // as the booster gets dropped it runs its script"*, with no mention of a mode.
                // ⛔ It commands no flight control: a time-warp rate and a physics range, nothing else.
                MissionConductor.Tick(v);

                if (!CrewProcedureOps.Engaged) { Unbind(); return; }

                Bind(v);

                // The crew-gate conductor advances on measured vessel state + the crew's GO. This is the ONE
                // caller of Tick in the tree, and the reason W10 had to land both files together.
                CrewProcedureOps.Tick(v);

                // ⭐ T18's ONE LINE OF HOST GROWTH (§B12.8 rider (c): "every later Wave E / T-series line
                // GROWS THIS SAME HOST by exactly the dispatch its own controller needs"). Everything the
                // conductor does with MechJeb is behind it: `Conductor.Decide` → the embedded core → the
                // §B12.7 direct-part actuation. It runs AFTER `CrewProcedureOps.Tick` because it reads the
                // phase that tick just resolved.
                //
                // ⛔ THE LAUNCH GO IS NO LONGER CONSUMED HERE. W10 consumed it in this method and logged
                // that nothing could act on it — correct then, §14.4(a). T18 gives it somewhere to go, so
                // `MechConductor.Tick` consumes it and latches it into the ascent sequence. Consuming it
                // in BOTH places would swallow the crew's GO on the frame it was pressed.
                MechConductor.Tick(v);
            }
            catch (Exception e)
            {
                // A glue fault logs and carries on — it never takes the flight down.
                Debug.LogWarning("[DragonScreen] FlightDriver tick failed: " + e.Message);
            }
        }

        // ================== THE GEN-1 FACADE SURFACE — UNCHANGED BEHAVIOUR ==================
        // These are the members the tree already calls on `FlightDriver` (`Actuator.cs:440`,
        // `BlackBoxRecorder.cs`, `ScreenPainter.cs:1212`, `VesselData.cs:364`). §B12.5a(iv): never rename one,
        // never add a parallel surface beside one, never half-wire one. §B12.5 allows exactly ONE facade
        // property to go live per increment and W10's is `AutoPilot.Engaged` (see `_AutopilotStub.cs`), so
        // every member below reports EXACTLY what the stub reported — the class behind the name changed, the
        // behaviour did not. That is the same swap W2 made for `Actuator`.

        // No abort path in this build (register W19, `AbortControl.cs`). Constant false — `ScreenPainter.cs:1212`
        // gates the red abort overlay on this, and §14.4(a) is explicit that there is NO RED without an abort.
        public static bool Aborting { get { return false; } }
        public static bool AbortFxSuppressed { get { return false; } }

        // The crew-facing control authority (`VesselData.cs:364` → the GNC lamp). `AuthorityManager` is a
        // display LABEL only in this tree (CLAUDE.md), and nothing commands the vehicle, so IDLE is the truth.
        public static ControlMode MissionMode { get { return ControlMode.Idle; } }

        // FDIR observes nothing yet: the recovered spine ran on feeds published by controllers that are not
        // here (thrust shortfall, control authority, closing rate). A default report is FaultKind.None —
        // honest, and it is what `Fdir.FaultName` turns into "NOMINAL".
        public static FdirReport LastFdirReport { get { return default(FdirReport); } }

        // ---- honest no-ops: the command entry points, kept because they are the recovered surface ----
        public static void RequestAbort() { }
        public static void SuppressAbortFx() { }
        public static void RequestDeorbit(bool propulsive) { }

        // The throttle-authority entry point the REAL `Actuator` calls (`Actuator.cs:440` — `FireAbort` owns
        // the throttle, SuperDracos fire at full). The recovered host latched a commanded throttle and applied
        // it in `OnFlyByWire`; that is an actuation increment, so here it stays the honest no-op it already
        // was in `_AutopilotStub.cs`. Nothing is throttled, and nothing pretends to be.
        public static void SetThrottle(double t) { }
    }
}
