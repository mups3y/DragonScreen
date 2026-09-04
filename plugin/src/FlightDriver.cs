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
        public static bool HasControllerFor(MissionPhase p) { return false; }

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
                if (!CrewProcedureOps.Engaged) { Unbind(); return; }

                Bind(v);

                // The crew-gate conductor advances on measured vessel state + the crew's GO. This is the ONE
                // caller of Tick in the tree, and the reason W10 had to land both files together.
                CrewProcedureOps.Tick(v);

                // ⛔ THE LAUNCH GO IS CONSUMED AND GOES NOWHERE — §14.4(a), deliberately. Clearing G7 raises an
                // ignition INTENT; the recovered host turned that into `StartLaunch` → plane-window hold →
                // erector clear → `Ignite` → the hold-down clamp gate, every step of which commands the
                // vehicle through `Actuator`. None of that is in scope here (§B12.6 step (3)). It is consumed
                // rather than left latched so it cannot fire late at some future increment's first frame.
                if (CrewProcedureOps.ConsumeLaunch())
                    Debug.Log("[DragonScreen] LAUNCH GO cleared (G7) — no ignition path in this build; "
                              + "the conductor is read-only until the actuation increments land. Nothing fired.");
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
