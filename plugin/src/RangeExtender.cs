// DragonScreen — RangeExtender  ("our PRE": widen the KSP physics/load ranges so BOTH the Dragon and the
// separated booster stay LOADED + UNPACKED — i.e. fully simulated AND controllable — through the booster
// recovery, without the PhysicsRangeExtender mod as a hard dependency.)
// ============================================================================================
// KSP only fully simulates + accepts control input for a vessel that is UNPACKED (within the unpack range of
// the active vessel); beyond it a vessel goes ON-RAILS (no physics, no throttle/steering). Stock unpack range
// is ~a few km, so a separated booster unloads long before it lands. PhysicsRangeExtender (jrodrigv, ported
// here from its source) fixes this by widening every vessel's VesselRanges. We port its exact method:
//   VesselRanges.Situation(load = R, unload = 1.05·R, pack = 1.10·R, unpack = 0.99·R)  on all 7 situations,
//   assigned to vessel.vesselRanges for every loaded vessel.
// So within ~R metres of the active vessel a booster stays unpacked (physics + OnFlyByWire-controllable).
//
// ⛔ USED AS CHRIS DIRECTED: turn ON before booster separation, OFF after focus returns to the upper stage
// post-recovery (MissionConductor drives the on/off). Range = the max booster↔upper-stage separation during
// recovery + margin (Chris: "say it's 500 km → set 600 km"). ⚠ PRE documents phantom forces / shaking beyond
// ~100 km — the real risk to a precision hoverslam — so keep the range only as wide as needed and off otherwise.
// Restores the STOCK ranges on Disable (PhysicsGlobals.VesselRangesDefault). Guarded end to end.
// ============================================================================================
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class RangeExtender
    {
        static bool active;
        public static bool Active { get { return active; } }

        // Widen every loaded vessel's ranges to rangeM (PRE method). Idempotent per call; MissionConductor calls
        // it at the arming transition and again once the separated booster exists (to catch the new vessel).
        public static void Enable(double rangeM)
        {
            try
            {
                VesselRanges vr = Build(rangeM);
                var list = FlightGlobals.Vessels;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null) list[i].vesselRanges = new VesselRanges(vr);
                if (!active)
                    Debug.Log("[DragonScreen] ⭐ PRE ON — physics/load ranges widened to "
                              + (rangeM / 1000.0).ToString("F0") + " km (booster + upper stage stay loaded+unpacked).");
                active = true;
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] PRE enable failed: " + e.Message); }
        }

        // Restore the STOCK ranges on every vessel (physics returns to the normal ~few-km unpack range).
        public static void Disable()
        {
            if (!active) return;
            try
            {
                VesselRanges def = (PhysicsGlobals.Instance != null) ? PhysicsGlobals.Instance.VesselRangesDefault : null;
                if (def != null)
                {
                    var list = FlightGlobals.Vessels;
                    for (int i = 0; i < list.Count; i++)
                        if (list[i] != null) list[i].vesselRanges = new VesselRanges(def);
                }
                Debug.Log("[DragonScreen] PRE OFF — stock physics/load ranges restored.");
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] PRE disable failed: " + e.Message); }
            active = false;
        }

        // Build a VesselRanges with all 7 situations at the PRE-formula distances for range R (metres).
        static VesselRanges Build(double R)
        {
            var s = new VesselRanges.Situation((float)R, (float)(R * 1.05), (float)(R * 1.10), (float)(R * 0.99));
            var vr = new VesselRanges();
            vr.orbit = s; vr.landed = s; vr.flying = s; vr.prelaunch = s;
            vr.subOrbital = s; vr.splashed = s; vr.escaping = s;
            return vr;
        }
    }
}
