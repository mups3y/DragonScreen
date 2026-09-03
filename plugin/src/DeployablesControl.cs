// DragonScreen — DeployablesControl  (KSP glue: on-orbit solar/antenna deploy, pre-return retract)
// ============================================================================================
// The one-shot manager for the vehicle's deployables (Chris's ticked "auto-deploy panels/antennas"). Arrays +
// antennas ride retracted through ascent; once the vehicle is on a STABLE orbit (pe above the atmosphere) they
// deploy, and they retract again before the return deorbit. Latched so each transition fires once. Actuation is
// direct part-module (Actuator.Deploy*/Retract*), never an action group; a vehicle without deployables is a
// harmless no-op. Driven by the mission phase from FlightDriver — no bespoke timing of its own.
// ============================================================================================
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class DeployablesControl
    {
        [Tunable] public static bool UseDeployables = true;
        [Tunable] public static double DeployMinPeM = 120000.0;   // only deploy once pe is a stable (above-atmosphere) orbit

        static bool deployed, retracted;

        public static void Reset() { deployed = false; retracted = false; }

        public static void Tick(Vessel v, MissionPhase phase, bool isReturn)
        {
            if (!UseDeployables || v == null) return;
            try
            {
                // ---- deploy ONCE on a stable outbound orbit (rendezvous/approach/docked, pe above the atmosphere) ----
                if (!deployed && !isReturn
                    && (phase == MissionPhase.Phasing || phase == MissionPhase.Approach || phase == MissionPhase.Docked)
                    && v.orbit != null && v.orbit.PeA >= DeployMinPeM)
                {
                    Actuator.DeploySolarPanels(v);
                    Actuator.DeployAntennas(v);
                    deployed = true;
                    Debug.Log("[DragonScreen] on-orbit: solar panels + antennas deployed (pe "
                              + (v.orbit.PeA / 1000.0).ToString("F0") + " km)");
                }

                // ---- retract ONCE before the return deorbit/entry (protect body panels; trunk arrays go with
                //      the trunk on jettison regardless). Any return phase, or the entry itself, triggers it. ----
                if (!retracted && (isReturn || phase == MissionPhase.Entry || phase == MissionPhase.Drogues))
                {
                    Actuator.RetractSolarPanels(v);
                    retracted = true;
                    Debug.Log("[DragonScreen] pre-return: retractable solar panels stowed");
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] deployables tick failed: " + e.Message); }
        }
    }
}
