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

        // ---- FOCUS ----
        [Tunable] public static bool AutoRecoverBooster = false;   // opt-in: hand focus to the booster to land it
        static bool boosterHandled;                                // one-shot per flight

        public static void Reset()
        {
            warpTargetUT = 0.0;
            boosterHandled = false;
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
        // universal safety net — forces real time whenever the active vessel is under thrust so a live burn is
        // never run under warp.
        public static void Tick(Vessel active)
        {
            try
            {
                if (active != null && ThrustActive(active) && Warped())
                {
                    Realtime();   // ⛔ never run a live burn under warp
                    return;
                }

                if (AutoRecoverBooster && !boosterHandled) TryFocusBooster(active);

                if (warpTargetUT <= 0.0) return;

                double timeToEvent = warpTargetUT - Now();
                if (WarpPlan.MustBeRealtime(timeToEvent)) { Realtime(); return; }

                // maintain the warp: physics-warp is never used for a coast; WarpTo decelerates to the drop-out.
                if (Warped() && TimeWarp.WarpMode == TimeWarp.Modes.LOW) DropToRealtime();
                if (WarpPlan.ShouldWarp(timeToEvent) && TimeWarp.CurrentRateIndex == 0)
                    WarpTo(WarpPlan.DropOutUT(warpTargetUT));
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] conductor tick failed: " + e.Message); }
        }

        // -------------------------------------------------------------- focus orchestration
        // Hand focus to a separated, airborne, recoverable booster so BoosterControl can fly it down. One-shot.
        static void TryFocusBooster(Vessel active)
        {
            if (active == null || !VesselHasPod(active)) return;   // only switch away FROM the crewed Dragon
            Vessel booster = null;
            var all = FlightGlobals.Vessels;
            for (int i = 0; i < all.Count; i++)
            {
                Vessel vv = all[i];
                if (vv != null && vv.loaded && vv != active && BoosterControl.IsRecoverableBooster(vv)) { booster = vv; break; }
            }
            if (booster == null) return;
            boosterHandled = true;
            try
            {
                FlightGlobals.ForceSetActiveVessel(booster);
                Debug.Log("[DragonScreen] conductor: focus → separated booster for recovery (opt-in). "
                          + "⚠ the Dragon is now unfocused (stock KSP on-rails); this is a booster-landing segment.");
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] booster focus switch failed: " + e.Message); }
        }

        static bool VesselHasPod(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++) if (VehicleParts.IsPod(v.parts[i].name)) return true;
            return false;
        }

        // -------------------------------------------------------------- stock TimeWarp helpers (guarded)
        static double Now() { return Planetarium.GetUniversalTime(); }
        static bool Warped() { return TimeWarp.CurrentRateIndex != 0; }

        static bool ThrustActive(Vessel v)
        {
            try { return v.ctrlState != null && v.ctrlState.mainThrottle > 0.01; }
            catch { return false; }
        }

        static void DropToRealtime()
        {
            try { if (TimeWarp.CurrentRateIndex != 0) TimeWarp.SetRate(0, true); }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] warp-stop failed: " + e.Message); }
        }

        static void WarpTo(double ut)
        {
            try { if (TimeWarp.fetch != null && ut > Now()) TimeWarp.fetch.WarpTo(ut); }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] warp-to failed: " + e.Message); }
        }
    }
}
