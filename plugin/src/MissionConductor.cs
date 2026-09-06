// DragonScreen — MissionConductor  (KSP glue: the mission-level orchestration — time-warp + the §B16.7
// physics-range lifecycle that lets a separated booster be flown at all)
// ============================================================================================
// Above the per-phase controllers sits the CONDUCTOR: it warps through the long ballistic coasts, and it
// owns the physics ranges the two-vessel booster recovery runs inside. Built entirely on STOCK KSP APIs
// (TimeWarp, FlightGlobals) — no external mod is required or assumed; the "never overshoot a burn"
// guarantee is ours (`pure/WarpPlan.cs` + a lead drop-out).
//
// ---- RESTORED BY W9, 2026-09-07, from `8b81816^` (24,299 B) — AND THE RESTORE IS NOT BYTE-FOR-BYTE ----
// W2/W3's provenance idiom: say exactly what changed and why, in the file, so a later reader is never
// left comparing this against git and guessing. THREE things differ from the recovered file, and each
// one is a settled decision this line was told to honour rather than a liberty taken:
//
//   1. ⛔ **THE FOCUS SWITCH IS GONE — `ForceSetActiveVessel` appears nowhere in this file.** The
//      recovered `FocusOn(Vessel, string)` handed focus to the separated booster so `BoosterControl`
//      could fly it. **§B16.7 (owner decision O-B1 REVISED, 2026-09-03) settled the opposite**:
//      *"⛔ FOCUS NEVER LEAVES THE UPPER STAGE."* The booster lands UNFOCUSED, flown by its own core on
//      its own vessel. Restoring the verb would re-import a design the owner replaced, and it would also
//      stop the booster dead — `BoosterHostPlan.RequireNonActive` releases the host the moment the
//      booster becomes active (`BoosterHostStop.BecameActive`, annunciated *"focus moved ONTO the
//      booster — releasing (§B16.7)"*). So the two halves of the tree already agree; this file joins them.
//
//   2. ⛔ **NO `BoosterControl` BYTE IS BACK.** The recovered recovery FSM called `BoosterControl.Reset()`
//      / `.IsRecoverableBooster()` / `.DriveNonActive()` and hooked the booster's own `OnFlyByWire`
//      itself. CLAUDE.md keeps that implementation **deleted** and §B16.1 writes the booster core fresh —
//      and it HAS been written: `src/BoosterHost.cs` (W23/W24) binds, selects, steers and actuates the
//      booster, with `pure/BoosterHostPlan.cs` making "never the Dragon" a tested property rather than an
//      intention. So this file does not search for a booster, does not hook a callback and does not drive
//      an axis. It reads `BoosterHost.Engaged` and manages the ranges around it. **One searcher, one
//      driver** — a second opinion about which vessel is the booster is exactly the class of bug two
//      independent searches produce.
//
//   3. ⚠ **THE BURN GUARD NOW ONLY DROPS WARP THE CONDUCTOR ITSELF COMMANDED.** The recovered guard was
//      unconditional: any commanded burn on the active vessel called `Realtime()`, which called
//      `TimeWarp.SetRate(0, true)` whether or not the conductor had started that warp. It also read
//      `FlightDriver.CmdTransX/Y/Z` — live RCS-translation readbacks that the read-only host (W10) does
//      not have and, per §B12.8(a), must not gain ahead of the controller that writes them. Both halves
//      of the change fall out of the same fact: **nothing in this tree publishes a warp target yet**
//      (see WARP below), so an unconditional drop could only ever cancel a warp THE CREW started, over a
//      throttle THE CREW opened — an unrequested command from a build whose whole claim is that it flies
//      nothing (§14.4(a)). The guard's real job — "a warp of ours must never carry through a burn" — is
//      kept in full and is what `conductorOwnsWarp` below tracks. The pending target is still zeroed
//      unconditionally, because that costs the crew nothing. When an increment lands a controller that
//      commands translation, it adds its own readback to `FlightDriver` in the same diff (rider (c)) and
//      widens `BurnCommanded` to read it.
//
// ---- WARP: LIVE CODE, NO CALLER, AND SAYING SO IS THE POINT ------------------------------------------
// `WarpToEvent(ut)` warps on-rails toward a future event and drops back to real time `WarpPlan.BurnLeadS`
// before it, so the transition out of warp can never carry us past the burn. `Realtime()` forces 1×. The
// decision (should-warp / drop-out rate / must-be-realtime) is the headless-tested `pure/WarpPlan.cs`;
// `TimeWarp.SetRate` is the mechanism.
// ⚠ **NOTHING CALLS `WarpToEvent` TODAY, SO NOTHING WARPS TODAY.** Its callers were the coast controllers
// (`RendezvousControl` / `ReturnControl` / `DeorbitBurn`), and the owner's 2026-09-04 upper-stage decision
// re-verdicted all three **RECOVER-REFERENCE** (§B12.8 rider (d)) — they land no code, ever. The coast
// warp arrives with the conductor increments that schedule the burns (T18 onward), which is when this
// half acquires a caller. It is restored now, with the recovery half, because they were one file and
// splitting a file to defer half of it is the "quiet deletion inside another task's diff" §B12.8 rider
// (b) forbids. Until then: `warpTargetUT` stays 0, `ApplyRailWarp` never runs, and the crew's own warp is
// untouched. `pure/WarpPlan.cs` + `pure/CoastEta.cs` finally have a compiled consumer; they still have no
// FLIGHT that exercises them, and their four margins remain [UN-CONVERGED] (§B16.8 ruling 2).
//
// ---- THE RANGES: THIS IS THE HALF THE BOOSTER IS ACTUALLY BLOCKED ON ---------------------------------
// `src/BoosterHost.cs` says it in its own header: *"Until W9 lands, a real flight will packs-out the
// booster within a few km and this host will say so and let go."* KSP only accepts control input for an
// UNPACKED vessel, and stock unpack range is a couple of km — so without wide ranges the booster host
// binds, flies for a second or two and releases with `BoosterHostStop.Unloaded`, whose annunciation reads
// *"booster unloaded (out of physics range — PRE is register W9's)"*. `RangeExtender.Enable` is what
// fixes that, and it may only be called from here because it writes `vesselRanges` on EVERY loaded vessel
// including the Dragon (the host is forbidden it by name at `BoosterHost.cs:100`).
// The lifecycle — when wide, how long, when back — is `pure/RecoveryPlan.cs`, headless-tested. This file
// is the two `RangeExtender` calls it decides, the KSP reads it decides from, and nothing else.
// ⛔ **§B16.7 STEP 4 (auto-recover the booster) IS NOT BUILT.** `pure/RecoveryPlan.cs`'s header states the
// reason in full: no source in this repo records how a NON-ACTIVE landed vessel is recovered from the
// flight scene, and C1.15/§1.4 forbid inventing the binding. The protocol runs 1-2-3-5; the booster is
// left standing where it landed. W9's register line poses the question.
//
// ---- ⛔ THIS FILE COMMANDS NO FLIGHT CONTROL, AND W9 IS NOT THE LINE THAT LIFTS THAT -----------------
// No throttle, no attitude, no translation, no staging, no ignition, no abort. It sets a time-warp rate
// and it sets physics ranges. §14.4(a) binds until T18 (ascent) — a separate line.
// ============================================================================================
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class MissionConductor
    {
        // ---- WARP ----
        static double warpTargetUT;        // the event UT we are warping toward (0 = idle)
        static bool conductorOwnsWarp;     // ⭐ TRUE only while the rate on screen is one WE set — see
                                           // change (3) in the header. Cleared whenever we drop to
                                           // realtime, and never set by anything the crew does.

        // ⭐ TUNABLE TIME WARP (owner: "perfect control of it so we never miss a manoeuvre"). Every knob
        // that shapes the auto-warp is a [Tunable] — the margins live in WarpPlan (BurnLeadS /
        // SettleMarginS / MinWarpGapS / LookaheadTicks) and these two masters here.
        [Tunable] public static bool AutoWarpEnabled = true;       // master: false → the conductor NEVER warps
                                                                   // (a burn is still forced out of a warp of
                                                                   // OURS; this stops the coast/phase warp).
        [Tunable] public static double MaxWarpRateX = 10000.0;     // ⛔ hard cap on the on-rails rate — never warp
                                                                   // faster than this even far from the event, so the
                                                                   // deceleration always has room and a burn is never
                                                                   // approached at a screaming rate. 10000× compresses
                                                                   // multi-day coasts yet one 0.02 s window = 200 s,
                                                                   // trivially stoppable before the BurnLeadS drop-out.

        // ---- THE §B16.7 RANGE LIFECYCLE ----
        // ⚠ DEFAULT-ON is the RECOVERED default (owner, 2026-08-29: *"don't make me re-arm it every
        // session"*), and it is kept — but what it now arms is a different, cheaper thing than it armed
        // then. It used to mean "hand focus to the booster and give up this flight's orbit"; under §B16.7
        // it means "keep the physics ranges wide so the booster host can fly the booster while the Dragon
        // flies its own mission". Nothing is sacrificed, so there is nothing to re-arm defensively. The
        // on-screen caption said the old thing and was corrected in this same diff
        // (`pure/SettingsPage.cs`) — a screen must never state a design that was superseded (§14.4(a)).
        [Tunable] public static bool AutoRecoverBooster = true;
        [Tunable] public static double PreRangeKm = 600.0;         // PRE range = max booster↔upper-stage sep + margin
                                                                   // (owner: "say 500 km → set 600"). [UN-CONVERGED]:
                                                                   // no recorded flight measured the real separation —
                                                                   // the recorder follows the active vessel only, which
                                                                   // is why LogSeparation below exists at all.
        [Tunable] public static double RecoveryTimeoutS = 1200.0;  // hard backstop on the range lifecycle

        static RecoveryStage recStage = RecoveryStage.Idle;
        static double recStartUT;                                  // UT the recovery stage was entered
        static double maxSepM, lastSepLogUT = -999.0;              // the two-vessel separation instrument

        /// <summary>The booster currently being recovered, for `BoosterRecovery.Tracked` (the HullCams
        /// follow) — null when no recovery is running. ⭐ IT IS THE HOST'S OWN BOUND VESSEL, not a
        /// vessel this file found: one searcher, one driver (header note 2). Gated on the stage as well
        /// as on the host so the lamp cannot light off a host that bound something while the crew had
        /// the arm switched off.</summary>
        public static Vessel RecoveryBooster
        {
            get { return RecoveryPlan.InProgress(recStage) ? BoosterHost.Booster : null; }
        }

        /// <summary>True while the range lifecycle is holding the physics ranges wide for a booster that
        /// is actually being flown. Display-facing and honest: `Armed` is not "in progress".</summary>
        public static bool BoosterRecoveryActive { get { return RecoveryPlan.InProgress(recStage); } }

        /// <summary>A fresh flight scene starts with stock ranges and an idle conductor. Called by
        /// `FlightDriver.Start` — the static state here survives a scene change, so without this the last
        /// flight's stage and warp target carry onto the next vehicle.</summary>
        public static void Reset()
        {
            warpTargetUT = 0.0; conductorOwnsWarp = false;
            recStage = RecoveryStage.Idle;
            recStartUT = 0.0; maxSepM = 0.0; lastSepLogUT = -999.0;
            if (RangeExtender.Active) RangeExtender.Disable();
        }

        // -------------------------------------------------------------- warp orchestration
        /// <summary>Warp on-rails toward a future event, dropping out `WarpPlan.BurnLeadS` before it
        /// (never overshoot). ⚠ No caller yet — see the header.</summary>
        public static void WarpToEvent(double eventUT)
        {
            warpTargetUT = (eventUT > Now()) ? eventUT : 0.0;
        }

        /// <summary>Cancel any pending warp and, if the running warp is OURS, drop to real time. A burn
        /// is imminent or active. ⚠ It does not touch a warp the crew started — header note (3).</summary>
        public static void Realtime()
        {
            warpTargetUT = 0.0;
            if (conductorOwnsWarp) DropToRealtime();
        }

        /// <summary>Called every physics frame from `FlightDriver`. Runs the §B16.7 range lifecycle, then
        /// maintains the warp toward `warpTargetUT` — and, the universal safety net, forces real time
        /// whenever the active vessel is commanding a burn under a warp of ours, so a live burn is never
        /// run under warp. Guarded end to end: a glue fault logs and the flight carries on.</summary>
        public static void Tick(Vessel active)
        {
            try
            {
                // ⭐ THE RANGE LIFECYCLE IS CHECKED FIRST AND INDEPENDENTLY of the burn/warp net below —
                // physics ranges have nothing to do with warp. It is a no-op before separation and when
                // the arm is off, so it is safe every frame. (In the recovered file this sat AFTER the
                // burn early-return and therefore never fired at all, because the S2 ascent commands a
                // burn on every frame from MECO — flight 180029. Order is not cosmetic here.)
                TickRecovery(active);

                if (active != null && BurnCommanded(active))
                {
                    Realtime();   // ⛔ never run a live burn under a warp of ours; also cancels the target
                    return;
                }

                if (warpTargetUT <= 0.0) return;

                // ⭐ master enable: OFF → the conductor never warps. Drop any warp already running that
                // is ours; a warp the crew started stays theirs.
                if (!AutoWarpEnabled) { if (conductorOwnsWarp) DropToRealtime(); return; }

                double timeToEvent = warpTargetUT - Now();
                if (WarpPlan.MustBeRealtime(timeToEvent)) { Realtime(); return; }

                // physics (LOW) warp is never used for a coast — drop ours; on-rails only below.
                if (conductorOwnsWarp && Warped() && TimeWarp.WarpMode == TimeWarp.Modes.LOW) DropToRealtime();

                // ⭐ Deterministic, CAPPED on-rails ladder: `WarpPlan.SafeRate` picks the highest rail
                // rate that cannot overshoot the drop-out point in one physics window and ratchets DOWN
                // monotonically as it nears; `MaxWarpRateX` hard-caps it. So the rate is fully under our
                // tunable control and a manoeuvre is never approached faster than we can cleanly stop.
                if (WarpPlan.ShouldWarp(timeToEvent))
                    ApplyRailWarp(WarpPlan.DropOutUT(warpTargetUT));
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] conductor tick failed: " + e.Message); }
        }

        // -------------------------------------------------------------- §B16.7 range lifecycle
        // The DECISION is `pure/RecoveryPlan.cs`. This is the read that feeds it and the two calls that
        // come out of it — deliberately nothing more, so the whole rule stays headless-testable.
        static void TickRecovery(Vessel active)
        {
            RecoveryInputs i;
            i.Armed = AutoRecoverBooster;
            i.ActiveIsStack = active != null && VesselHasPod(active);
            i.ActiveAirborne = active != null && Airborne(active);
            i.BoosterBound = BoosterHost.Engaged;
            i.RangeExtended = RangeExtender.Active;
            i.RecoveringForS = (recStartUT > 0.0) ? Now() - recStartUT : 0.0;
            i.TimeoutS = RecoveryTimeoutS;

            RecoveryDecision d = RecoveryPlan.Next(recStage, i);

            if (d.Stage != recStage)
            {
                recStage = d.Stage;
                recStartUT = (d.Stage == RecoveryStage.Recovering) ? Now() : 0.0;
                if (d.Stage == RecoveryStage.Idle) { maxSepM = 0.0; lastSepLogUT = -999.0; }
            }

            if (d.Range == RangeAct.Extend) RangeExtender.Enable(PreRangeKm * 1000.0);
            else if (d.Range == RangeAct.Restore) RangeExtender.Disable();

            if (d.Note != null) Debug.Log("[DragonScreen] " + d.Note);

            if (RecoveryPlan.InProgress(recStage)) LogSeparation(active);
        }

        // The two-vessel separation instrument — the number that sizes `PreRangeKm`, and the one number
        // no single-vessel recording can contain (the flight recorder follows the ACTIVE vessel only).
        // ⚠ L6's bug, kept fixed: the pair is the BOUND BOOSTER against the upper stage. Differencing the
        // active vessel against itself is what printed `sep 0 km` on every earlier flight.
        static void LogSeparation(Vessel upperStage)
        {
            try
            {
                Vessel booster = BoosterHost.Booster;
                if (booster == null || upperStage == null || !booster.loaded) return;
                if (ReferenceEquals(booster, upperStage)) return;
                double sep = (booster.CoM - upperStage.CoM).magnitude;
                if (sep > maxSepM) maxSepM = sep;
                double now = Now();
                if (now - lastSepLogUT <= 5.0) return;          // ~every 5 s (the S40 flood rule)
                lastSepLogUT = now;
                Debug.Log("[DragonScreen] booster recovery: sep " + (sep / 1000.0).ToString("F0") + " km (max "
                          + (maxSepM / 1000.0).ToString("F0") + " km) — booster loaded=" + booster.loaded
                          + " unpacked=" + (!booster.packed) + ", upper-stage loaded=" + upperStage.loaded
                          + " unpacked=" + (!upperStage.packed) + " [wide ranges keeping both alive].");
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] separation log failed: " + e.Message); }
        }

        static bool Airborne(Vessel v)
        {
            return v.situation == Vessel.Situations.FLYING || v.situation == Vessel.Situations.SUB_ORBITAL
                || v.situation == Vessel.Situations.ORBITING || v.situation == Vessel.Situations.ESCAPING;
        }

        static bool VesselHasPod(Vessel v)
        {
            if (v.parts == null) return false;
            for (int i = 0; i < v.parts.Count; i++)
                if (v.parts[i] != null && VehicleParts.IsPod(v.parts[i].name)) return true;
            return false;
        }

        // -------------------------------------------------------------- stock TimeWarp helpers (guarded)
        static double Now() { try { return Planetarium.GetUniversalTime(); } catch { return 0.0; } }
        static bool Warped() { return TimeWarp.CurrentRateIndex != 0; }

        // Any burn commanded this frame. ⚠ TODAY THAT IS THE MAIN THROTTLE ONLY, and the omission is
        // named rather than silent: the recovered guard also summed `FlightDriver.CmdTransX/Y/Z` (live
        // RCS-translation readbacks) because the rendezvous, departure and deorbit burns are TRANSLATION,
        // not throttle. Those readbacks do not exist on W10's read-only host and §B12.8(a) forbids adding
        // them ahead of a controller that writes them — and no controller in this tree commands
        // translation, so there is no live Draco burn for the guard to miss. The increment that lands one
        // adds its readback and widens this in the same diff (§B12.8 rider (c)).
        static bool BurnCommanded(Vessel v)
        {
            try { return v.ctrlState != null && v.ctrlState.mainThrottle > 0.01; }
            catch { return false; }
        }

        static void DropToRealtime()
        {
            try { if (TimeWarp.CurrentRateIndex != 0) TimeWarp.SetRate(0, true); }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] warp-stop failed: " + e.Message); }
            conductorOwnsWarp = false;
        }

        // ⭐ Set the on-rails warp rate to the SAFE, CAPPED value for the time left to the drop-out point.
        // The safe rate (`WarpPlan.SafeRate`) can never advance more than LookaheadTicks physics windows
        // past the drop-out at that rate, and ratchets DOWN to 1× as it nears; MaxWarpRateX caps it
        // further. `SetRate` is idempotent (only written when the index changes) and KSP clamps it to the
        // altitude-allowed maximum, so this is safe to call every frame. Guarded — a failed warp call
        // logs and leaves realtime.
        static double[] railRates;                                 // cached on-rails rate table (ascending)

        static void ApplyRailWarp(double dropOutUT)
        {
            try
            {
                TimeWarp tw = TimeWarp.fetch;
                if (tw == null || tw.warpRates == null || tw.warpRates.Length == 0) return;
                double timeToDropoutS = dropOutUT - Now();
                if (timeToDropoutS <= WarpPlan.SettleMarginS) { if (conductorOwnsWarp) DropToRealtime(); return; }

                // cache the ascending rate table as double[] (rebuild only if the table changed).
                if (railRates == null || railRates.Length != tw.warpRates.Length)
                {
                    railRates = new double[tw.warpRates.Length];
                    for (int i = 0; i < tw.warpRates.Length; i++) railRates[i] = tw.warpRates[i];
                }

                double tickS = Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 0.02;
                double safe = WarpPlan.SafeRate(timeToDropoutS, railRates, tickS);
                if (safe > MaxWarpRateX) safe = MaxWarpRateX;          // ⛔ the hard cap

                // highest rail index whose rate ≤ the safe/capped rate.
                int idx = 0;
                for (int i = 0; i < railRates.Length; i++) if (railRates[i] <= safe + 1e-6) idx = i;
                // ⛔ INSTANT on the way DOWN, gradual on the way UP (MechJeb WarpController
                // instantOnDecrease=true — docs/TIME_WARP_RESEARCH.md §5). On-rails warp is kinematic, so
                // snapping the rate down is safe; a GRADUAL step-down from a high rate takes ~1-2 REAL
                // seconds to spin down, during which game-time races ahead (1 real s = rate game-s) and
                // can carry us PAST the drop-out — the LookaheadTicks headroom is in game-seconds and far
                // too small to cover that. Instant-down closes that overshoot.
                if (TimeWarp.CurrentRateIndex != idx)
                    TimeWarp.SetRate(idx, idx < TimeWarp.CurrentRateIndex);
                // From here the rate on screen is one we set, so the burn guard may drop it (note 3).
                conductorOwnsWarp = idx != 0;
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] rail-warp failed: " + e.Message); }
        }
    }
}
