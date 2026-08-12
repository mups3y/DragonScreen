/*
 * DragonScreen - ChuteGuard
 *
 * GLUE. Deploys the parachutes if nothing else has, whoever is flying.
 *
 * ---- ⛔ WHY THIS EXISTS: THE CHUTES ARE INSIDE THE AUTOPILOT, AND THE CREW WAS FLYING ----
 * On 2026-08-13 the second stage separated against the trunk and pushed the capsule off course.
 * The crew took over, rolled the stage off during entry, and flew the descent by hand. The
 * recorder's last row reads:
 *
 *      alt 701 m     vertical speed -246.6 m/s     drogues 0     mains 0
 *
 * `r_stage` never left `Idle` for the whole flight - `EntryOps` was never engaged, so nothing was
 * watching for the drogue altitude. The chute logic was correct and simply was not running.
 *
 * A parachute is not a guidance decision. It is the last thing standing between the crew and the
 * ground, and it must not be a member of a sequence that can be skipped, aborted or never started.
 * So this watches independently, every tick, and fires on the same real altitudes `Terminal` uses.
 *
 * ---- IT DEFERS, IT DOES NOT COMPETE ----
 * When `EntryOps` IS flying the return it deploys the chutes itself, on its own schedule, and this
 * sees them already deployed and does nothing. The two cannot fight: deployment is a latch, and
 * asking a deployed chute to deploy is a no-op. The guard exists for the case where the sequence
 * is absent, not to second-guess it.
 *
 * ---- AND IT WILL NOT DEPLOY INTO A SPEED THAT SHREDS THEM ----
 * KSP's own `ModuleParachute` refuses an unsafe deployment and reports `deploymentSafeStatus`, so
 * the honest thing is to ask and let it judge, rather than encoding a speed limit here that would
 * be wrong for a different chute. Same reasoning as reading `acquireRange` off the docking port
 * instead of guessing a capture envelope.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class ChuteGuard
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>Only ever fires while falling. A rising vehicle is not landing.</summary>
        public const double MinDescentRate = 5.0;

        /// <summary>Reported once per vessel so the log says the guard acted, not the sequence.</summary>
        private static uint firedFor;

        public static void Tick()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || !v.loaded || v.parts == null) return;
            if (v.mainBody == null || !v.mainBody.atmosphere) return;
            if (v.LandedOrSplashed) return;

            double alt = v.radarAltitude;
            double vs = v.verticalSpeed;
            if (vs > -MinDescentRate) return;                 // not descending
            if (alt > Entry.DrogueAltitude) return;           // not low enough for anything

            bool wantMains = alt <= Entry.MainAltitude;
            int armed = 0;

            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleParachute mp = p.Modules[m] as ModuleParachute;
                    if (mp == null) continue;

                    // Already doing its job - semi-deployed, deployed or cut. Leave it alone.
                    if (mp.deploymentState != ModuleParachute.deploymentStates.STOWED) continue;

                    // A main is not a drogue. Below the main altitude everything goes; above it,
                    // only the drogues - a main opened at drogue altitude is the failure the two
                    // stages exist to prevent.
                    bool isDrogue = mp.part.partInfo != null
                                    && mp.part.partInfo.name.IndexOf("DROGUE",
                                           StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!wantMains && !isDrogue) continue;

                    try { mp.Deploy(); armed++; }
                    catch (Exception e)
                    {
                        Debug.LogWarning(Tag + "chute guard could not deploy '"
                                         + p.partInfo.title + "': " + e.Message);
                    }
                }
            }

            if (armed > 0 && firedFor != v.persistentId)
            {
                firedFor = v.persistentId;
                Debug.LogWarning(Tag + "CHUTE GUARD fired - " + armed + " chute(s) at "
                                 + alt.ToString("F0") + " m, " + vs.ToString("F0") + " m/s. "
                                 + "Nothing else had deployed them; the return sequence was "
                                 + (EntryOps.Engaged ? "engaged but had not reached the chutes."
                                                     : "NOT running."));
            }
        }

        public static void Reset() { firedFor = 0; }
    }
}
