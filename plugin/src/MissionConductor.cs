// DragonScreen — MissionConductor  (KSP glue: the mission-level orchestration — time-warp + vessel focus)
// ============================================================================================
// Above the per-phase controllers sits the CONDUCTOR: it warps through the long ballistic coasts and hands
// focus between the Dragon and a separated booster, so the autopilot flies the whole timeline rather than a
// single phase. Built entirely on STOCK KSP APIs (TimeWarp, FlightGlobals) — no external mod is required or
// assumed; the "never overshoot a burn" guarantee is ours (pure/WarpPlan + a lead drop-out).
//
// WARP: WarpToEvent(ut) warps on-rails toward a future event (a burn, a plane window) and drops back to real
// time WarpPlan.BurnLeadS before it, so the transition out of warp can never carry us past the burn. Realtime()
// forces 1× the instant a burn is imminent/active. The decision (should-warp / drop-out / must-be-realtime)
// is the headless-tested pure/WarpPlan; TimeWarp.WarpTo is the mechanism (it decelerates cleanly to the target).
//
// FOCUS: ⛔ STOCK KSP gives CONTROL INPUT to only the ACTIVE vessel. PhysicsRangeExtender (partially present in
// this install) keeps a separated craft LOADED and physically simulated far past the stock unpack range — so the
// booster is still really there to fly — but it does NOT make two vessels controllable at once; only the focused
// one flies. So a booster cannot be flown to a landing WHILE the Dragon flies to orbit — that is a stock engine
// limit, not a missing dependency. Booster recovery is therefore an OPT-IN, focus-managed SEGMENT
// (AutoRecoverBooster): after separation the conductor hands focus to the booster so BoosterControl lands it.
// FlightDriver is a flight-scene addon that survives the switch and drives whichever vessel is active. Default
// OFF so the crew mission (Dragon to orbit) is never sacrificed by accident.
// ============================================================================================
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class MissionConductor
    {
        // ---- WARP ----
        static double warpTargetUT;    // the event UT we are warping toward (0 = idle)

        // ⭐ TUNABLE TIME WARP (user: "perfect control of it so we never miss a manoeuvre"). Every knob that
        // shapes the auto-warp is a [Tunable] — the margins live in WarpPlan (BurnLeadS/SettleMarginS/
        // MinWarpGapS/LookaheadTicks), the coast triggers in RendezvousControl/ReturnControl (CoastWarp*), the
        // launch-window warp in FlightDriver (LaunchAutoWarp/LaunchLeadS), and these two masters here:
        [Tunable] public static bool AutoWarpEnabled = true;       // master: false → the conductor NEVER warps
                                                                   // (a burn is still forced to realtime; this only
                                                                   // stops the coast/phase warp). Chris can kill it live.
        [Tunable] public static double MaxWarpRateX = 10000.0;     // ⛔ hard cap on the on-rails rate — never warp
                                                                   // faster than this even far from the event, so the
                                                                   // deceleration always has room and a burn is never
                                                                   // approached at a screaming rate. 10000× compresses
                                                                   // multi-day coasts yet one 0.02 s window = 200 s,
                                                                   // trivially stoppable before the BurnLeadS drop-out.

        // ---- FOCUS + BOOSTER RECOVERY (our-PRE dual-flight, Chris 2026-08-29) ----
        // Chris's method: turn "our PRE" (RangeExtender — widen the physics/load ranges so both craft stay
        // loaded+unpacked) ON before booster separation, hand focus to the separated booster to fly it down
        // (BoosterControl), then RETURN focus to the upper stage and turn PRE back OFF. While focused on the
        // booster the upper stage is non-active → it COASTS (kept loaded by PRE), then resumes when refocused —
        // which is why the max booster↔upper-stage separation stays ~hundreds of km (the S2 is not accelerating
        // away). PreRangeKm = that max separation + margin ("say 500 km → set 600").
        [Tunable] public static bool AutoRecoverBooster = false;   // opt-in: recover the booster (our-PRE + focus)
        [Tunable] public static double PreRangeKm = 600.0;         // PRE range = max booster↔upper-stage sep + margin
        enum RecPhase : byte { Idle, Armed, FlyingBooster, Returned, Done }
        static RecPhase recPhase = RecPhase.Idle;
        static uint dragonId;                                      // the upper stage / Dragon to return focus to
        static double maxSepM, lastSepLogUT = -999.0;              // T8b: booster↔upper-stage separation instrument
        static double[] railRates;                                 // cached on-rails rate table (double, ascending)

        public static void Reset()
        {
            warpTargetUT = 0.0;
            recPhase = RecPhase.Idle; dragonId = 0; maxSepM = 0.0; lastSepLogUT = -999.0;
            if (RangeExtender.Active) RangeExtender.Disable();     // a fresh scene starts with stock ranges
        }

        // -------------------------------------------------------------- warp orchestration
        // Warp on-rails toward a future event, dropping out WarpPlan.BurnLeadS before it (never overshoot).
        public static void WarpToEvent(double eventUT)
        {
            warpTargetUT = (eventUT > Now()) ? eventUT : 0.0;
        }

        // Drop to real time immediately and cancel any pending warp (a burn is imminent or active).
        public static void Realtime()
        {
            warpTargetUT = 0.0;
            DropToRealtime();
        }

        // Called every physics frame from FlightDriver. Maintains the warp toward warpTargetUT, and — the
        // universal safety net — forces real time whenever the active vessel is commanding a burn so a live burn
        // is never run under warp. UNCONDITIONAL (not gated on Warped): a burn tick also ZEROES any pending warp
        // target, so the conductor can never re-warp mid-burn even from a stale target a coast controller set the
        // frame before. A coast controller only publishes WarpToEvent while it is NOT commanding a burn, so the
        // two never fight.
        public static void Tick(Vessel active)
        {
            try
            {
                // ⭐ BOOSTER RECOVERY (our-PRE dual-flight) — checked FIRST, INDEPENDENT of the burn/warp net
                // below (focus/ranges have nothing to do with warp). It is a per-frame state machine that turns
                // PRE on before sep, focuses the separated booster to land it, then returns focus to the upper
                // stage and turns PRE off. A no-op before sep + when AutoRecoverBooster is off, so it is safe to
                // call every frame. (It used to sit AFTER the BurnCommanded early-return and never fired, because
                // the S2 ascent commands a burn every frame from MECO — flight 180029.)
                TickBoosterRecovery(active);

                if (active != null && BurnCommanded(active))
                {
                    Realtime();   // ⛔ never run a live burn under warp; also cancels any pending warp target
                    return;
                }

                if (warpTargetUT <= 0.0) return;

                // ⭐ master enable: OFF → the conductor never warps (a commanded burn is already forced to
                // realtime above; this only stops the coast/phase compression). Drop any warp already running.
                if (!AutoWarpEnabled) { if (Warped()) DropToRealtime(); return; }

                double timeToEvent = warpTargetUT - Now();
                if (WarpPlan.MustBeRealtime(timeToEvent)) { Realtime(); return; }

                // physics (LOW) warp is never used for a coast — drop it; on-rails only below.
                if (Warped() && TimeWarp.WarpMode == TimeWarp.Modes.LOW) DropToRealtime();

                // ⭐ Deterministic, CAPPED on-rails ladder (replaces KSP's auto-rate WarpTo): WarpPlan.SafeRate
                // picks the highest rail rate that cannot overshoot the drop-out point in one physics window and
                // ratchets DOWN monotonically as it nears; MaxWarpRateX hard-caps it. So the rate is fully under
                // our tunable control and a manoeuvre is never approached faster than we can cleanly stop.
                if (WarpPlan.ShouldWarp(timeToEvent))
                    ApplyRailWarp(WarpPlan.DropOutUT(warpTargetUT));
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] conductor tick failed: " + e.Message); }
        }

        // -------------------------------------------------------------- booster recovery state machine
        // PRE ON before sep → focus the separated booster (fly it down) → return focus to the upper stage →
        // PRE OFF. See the field-block comment for why this keeps the separation small (the S2 coasts).
        static void TickBoosterRecovery(Vessel active)
        {
            if (!AutoRecoverBooster)
            {
                if (RangeExtender.Active) RangeExtender.Disable();   // turned off mid-flight → restore stock ranges
                recPhase = RecPhase.Idle; dragonId = 0;
                return;
            }
            if (active == null) return;

            switch (recPhase)
            {
                case RecPhase.Idle:
                    // ARM before separation: while the upper stage (the pod-carrying stack) is airborne in ascent,
                    // widen the ranges so the booster stays loaded+unpacked the instant it separates.
                    if (VesselHasPod(active) && Airborne(active))
                    {
                        dragonId = active.persistentId;
                        RangeExtender.Enable(PreRangeKm * 1000.0);
                        recPhase = RecPhase.Armed;
                    }
                    break;

                case RecPhase.Armed:
                    // After sep, a separated recoverable booster appears → re-apply PRE (to catch the NEW vessel)
                    // and hand focus to it. The upper stage (now non-active) coasts, kept loaded by PRE.
                    Vessel booster = FindLoadedBooster(active);
                    if (booster != null)
                    {
                        RangeExtender.Enable(PreRangeKm * 1000.0);
                        FocusOn(booster, "→ separated booster for recovery (upper stage coasts, kept loaded by PRE)");
                        recPhase = RecPhase.FlyingBooster;
                    }
                    break;

                case RecPhase.FlyingBooster:
                    // Instrument the booster↔upper-stage SEPARATION (T8b) — the number that sizes PreRangeKm.
                    // The CSV only follows the active vessel, so log the live + max separation here (~every 5 s).
                    LogSeparation(active);
                    // Fly the booster (FlightDriver drives BoosterControl on the active booster) until it is DOWN,
                    // then return focus to the upper stage so its ascent resumes. Also handle a KSP auto-switch
                    // back to the Dragon (e.g. booster destroyed) — if we are already back on the pod, move on.
                    if (VesselHasPod(active)) { recPhase = RecPhase.Returned; break; }
                    if (BoosterControl.IsRecoverableBooster(active) && (active.Landed || active.Splashed))
                    {
                        Vessel dragon = FindById(dragonId);
                        Debug.Log("[DragonScreen] booster recovered — MAX booster↔upper-stage separation this flight = "
                                  + (maxSepM / 1000.0).ToString("F0") + " km (PreRangeKm=" + PreRangeKm.ToString("F0")
                                  + " km; set PreRangeKm ≥ this + margin).");
                        if (dragon != null) FocusOn(dragon, "← upper stage (booster recovered); resuming ascent");
                        recPhase = RecPhase.Returned;
                    }
                    break;

                case RecPhase.Returned:
                    // Focus is back on the upper stage → PRE OFF (recovery complete for this flight).
                    if (VesselHasPod(active))
                    {
                        RangeExtender.Disable();
                        recPhase = RecPhase.Done;
                    }
                    break;

                case RecPhase.Done:
                    break;
            }
        }

        static bool Airborne(Vessel v)
        {
            return v.situation == Vessel.Situations.FLYING || v.situation == Vessel.Situations.SUB_ORBITAL
                || v.situation == Vessel.Situations.ORBITING || v.situation == Vessel.Situations.ESCAPING;
        }

        static Vessel FindLoadedBooster(Vessel active)
        {
            var all = FlightGlobals.Vessels;
            for (int i = 0; i < all.Count; i++)
            {
                Vessel vv = all[i];
                if (vv != null && vv.loaded && vv != active && BoosterControl.IsRecoverableBooster(vv)) return vv;
            }
            return null;
        }

        static Vessel FindById(uint id)
        {
            if (id == 0) return null;
            var all = FlightGlobals.Vessels;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].persistentId == id) return all[i];
            return null;
        }

        // T8b: log the live + max booster↔upper-stage separation (and whether both stay loaded+unpacked) — the
        // number that sizes PreRangeKm, which the single-vessel CSV cannot record. ~every 5 s while recovering.
        static void LogSeparation(Vessel booster)
        {
            try
            {
                Vessel dragon = FindById(dragonId);
                if (dragon == null || booster == null) return;
                double sep = (booster.CoM - dragon.CoM).magnitude;
                if (sep > maxSepM) maxSepM = sep;
                double now = Now();
                if (now - lastSepLogUT > 5.0)
                {
                    lastSepLogUT = now;
                    Debug.Log("[DragonScreen] booster recovery: sep " + (sep / 1000.0).ToString("F0") + " km (max "
                              + (maxSepM / 1000.0).ToString("F0") + " km) — booster loaded=" + booster.loaded
                              + " unpacked=" + (!booster.packed) + ", upper-stage loaded=" + dragon.loaded
                              + " unpacked=" + (!dragon.packed) + " [PRE keeping both alive = the H1 check].");
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] separation log failed: " + e.Message); }
        }

        static void FocusOn(Vessel v, string why)
        {
            try
            {
                FlightGlobals.ForceSetActiveVessel(v);
                Debug.Log("[DragonScreen] conductor: focus " + why + ".");
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] focus switch failed: " + e.Message); }
        }

        static bool VesselHasPod(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++) if (VehicleParts.IsPod(v.parts[i].name)) return true;
            return false;
        }

        // -------------------------------------------------------------- stock TimeWarp helpers (guarded)
        static double Now() { return Planetarium.GetUniversalTime(); }
        static bool Warped() { return TimeWarp.CurrentRateIndex != 0; }

        // Any burn commanded this frame — main-engine THROTTLE or an RCS (Draco) TRANSLATION. The rendezvous,
        // departure and deorbit burns are translation, NOT throttle, so the throttle check alone would let a
        // live Draco burn run under warp. FlightDriver's live command readbacks return 0 on a released axis, so
        // a genuine coast reads clean. Read AFTER DriveActivePhase (the conductor ticks later in FixedUpdate),
        // so this frame's guidance intent is already reflected.
        static bool BurnCommanded(Vessel v)
        {
            try
            {
                if (v.ctrlState != null && v.ctrlState.mainThrottle > 0.01) return true;
                double trans = Math.Abs(FlightDriver.CmdTransX) + Math.Abs(FlightDriver.CmdTransY)
                             + Math.Abs(FlightDriver.CmdTransZ);
                return trans > 0.001;
            }
            catch { return false; }
        }

        static void DropToRealtime()
        {
            try { if (TimeWarp.CurrentRateIndex != 0) TimeWarp.SetRate(0, true); }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] warp-stop failed: " + e.Message); }
        }

        // ⭐ Set the on-rails warp rate to the SAFE, CAPPED value for the time left to the drop-out point. The
        // safe rate (WarpPlan.SafeRate) can never advance more than LookaheadTicks physics windows past the
        // drop-out at that rate, and ratchets DOWN to 1× as it nears; MaxWarpRateX caps it further. SetRate is
        // idempotent (only written when the index changes) and KSP clamps it to the altitude-allowed maximum,
        // so this is safe to call every frame. Guarded — a failed warp call logs and leaves realtime.
        static void ApplyRailWarp(double dropOutUT)
        {
            try
            {
                TimeWarp tw = TimeWarp.fetch;
                if (tw == null || tw.warpRates == null || tw.warpRates.Length == 0) return;
                double timeToDropoutS = dropOutUT - Now();
                if (timeToDropoutS <= WarpPlan.SettleMarginS) { DropToRealtime(); return; }

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
                // ⛔ INSTANT on the way DOWN, gradual on the way UP (MechJeb WarpController instantOnDecrease=true —
                // docs/TIME_WARP_RESEARCH.md §5). On-rails warp is kinematic, so snapping the rate down is safe; a
                // GRADUAL step-down from a high rate takes ~1-2 REAL seconds to spin down, during which game-time
                // races ahead (1 real s = rate game-s) and can carry us PAST the drop-out — the LookaheadTicks
                // headroom is in game-seconds and far too small to cover that. Instant-down closes that overshoot.
                if (TimeWarp.CurrentRateIndex != idx)
                    TimeWarp.SetRate(idx, idx < TimeWarp.CurrentRateIndex);
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] rail-warp failed: " + e.Message); }
        }
    }
}
