/*
 * DragonScreen - ChuteGuard
 *
 * GLUE. Deploys the parachutes if nothing else has, whoever is flying.
 *
 * ---- ⛔ WHY THIS EXISTS: THE CHUTES ARE INSIDE THE AUTOPILOT, AND THE CREW WAS FLYING ----
 * On 2026-08-13 the second stage separated against the trunk and pushed the capsule off course. The
 * crew took over and flew the descent by hand, and the recorder's last row reads:
 *
 *      alt 701 m     vertical speed -246.6 m/s     drogues 0     mains 0
 *
 * `r_stage` never left `Idle` - `EntryOps` was never engaged, so nothing watched for the drogue
 * altitude. A parachute is not a guidance decision; it is the last thing between the crew and the
 * ground, and it must not belong to a sequence that can be skipped, aborted or never started. So this
 * watches independently, every tick, and fires on the same real altitudes `Terminal`/`Entry` use.
 *
 * ---- ⛔ AND IT FIRES THE PART'S OWN "DEPLOY CHUTE" HANDLE - NOT A STOCK MODULE CAST (2026-08-26) ----
 * The RO Dragon's chutes are RealChuteModule, NOT stock ModuleParachute (craft dump: the DROGUES and
 * MAINS parts carry ONLY RealChuteModule). The old guard cast `as ModuleParachute`, found nothing, and
 * was a SILENT NO-OP on this craft - the exact "looks finished, does nothing" trap. So it now fires the
 * chute's OWN capability, the "Deploy Chute" right-click event (or its action fallback), matched by GUI
 * name - the same detect-by-capability path EntryOps.DoEvent uses, which works for both RealChute and
 * stock. Drogues by name at the drogue altitude, mains at the main altitude.
 *
 * ---- IT DEFERS, IT DOES NOT COMPETE ----
 * When `EntryOps` IS flying the return it deploys the chutes itself; this then finds their deploy event
 * already inactive (a deployed chute offers no "Deploy Chute") and does nothing. The two cannot fight.
 * The guard exists for the case where the sequence is absent, not to second-guess it.
 */
using System;
using System.Collections.Generic;
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
        /// <summary>Per-vessel latches so a fired stage is not re-commanded every tick.</summary>
        private static uint stateFor;
        private static bool droguesDone, mainsDone, shroudClosed;

        /// <summary>The guard has deployed the drogues / mains on the active vessel. The recorder ORs these
        /// with EntryOps' own flags so the chute telemetry is truthful whoever deployed them - the guard
        /// fires on the manual/DEORBIT-NOW return where EntryOps never runs (flight_0826_014654).</summary>
        public static bool DroguesDeployed { get { return droguesDone; } }
        public static bool MainsDeployed { get { return mainsDone; } }

        public static void Tick()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || !v.loaded || v.parts == null) return;
            if (v.mainBody == null || !v.mainBody.atmosphere) return;
            if (v.LandedOrSplashed) return;

            // Fresh vessel / focus change resets the stage latches.
            if (v.persistentId != stateFor)
            { stateFor = v.persistentId; droguesDone = false; mainsDone = false; shroudClosed = false; }

            double alt = v.radarAltitude;
            if (v.verticalSpeed > -MinDescentRate) return;    // not descending

            // ⛔ NOSE CONE SHUT FOR RE-ENTRY, INDEPENDENT OF ANY SEQUENCE (user 2026-08-26). The docking
            // shroud must be closed for entry heating, and a manual / DEORBIT-NOW return can skip the
            // sequence that closes it (r_stage=Idle the whole descent). Close it once as the capsule falls
            // below the top of the atmosphere - well above the heating, and long after any orbital-altitude
            // Draco deorbit burn, so the forward Dracos are not needed by now. Harmless if already shut.
            if (!shroudClosed && v.altitude < v.mainBody.atmosphereDepth)
            {
                DockShroud.Close(v);
                shroudClosed = true;
            }

            if (alt > Entry.DrogueAltitude) return;           // not low enough for the chutes

            int armed = 0;

            // Drogues first, at the drogue altitude. A main opened up here is the failure the two
            // stages exist to prevent, so only the drogues fire above the main altitude.
            if (!droguesDone && alt <= Entry.DrogueAltitude)
            {
                int n = FireChutes(v, true);
                if (n > 0) { droguesDone = true; armed += n; }
            }
            // Mains at the main altitude.
            if (!mainsDone && alt <= Entry.MainAltitude)
            {
                int n = FireChutes(v, false);
                if (n > 0) { mainsDone = true; armed += n; }
            }

            if (armed > 0 && firedFor != v.persistentId)
            {
                firedFor = v.persistentId;
                Debug.LogWarning(Tag + "CHUTE GUARD fired - " + armed + " chute(s) at "
                                 + alt.ToString("F0") + " m, " + v.verticalSpeed.ToString("F0") + " m/s. "
                                 + "Nothing else had deployed them; the return sequence was "
                                 + (EntryOps.Engaged ? "engaged but had not reached the chutes."
                                                     : "NOT running."));
            }
        }

        /// <summary>Fire "deploy chute" on the drogue (<paramref name="drogues"/> true) or main parts,
        /// found by name (VehicleParts). Returns how many actually took the command.</summary>
        private static int FireChutes(Vessel v, bool drogues)
        {
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p == null || p.partInfo == null) continue;
                bool isDrogue = VehicleParts.IsDrogues(p.name);
                bool isMain = VehicleParts.IsMains(p.name);
                if (drogues ? !isDrogue : !isMain) continue;
                // The chute's OWN "Deploy Chute" handle (event-or-action, RealChute + stock).
                if (VehicleControl.FireByGuiName(p, "deploy chute")) n++;
            }
            return n;
        }

        public static void Reset()
        { firedFor = 0; stateFor = 0; droguesDone = false; mainsDone = false; shroudClosed = false; }
    }
}
