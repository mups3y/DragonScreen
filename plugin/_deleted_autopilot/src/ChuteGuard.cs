// DragonScreen - ChuteGuard
// ---- ⛔ WHY THIS EXISTS: THE CHUTES ARE INSIDE THE AUTOPILOT, AND THE CREW WAS FLYING ----
// ---- ⛔ AND IT FIRES THE PART'S OWN "DEPLOY CHUTE" HANDLE - NOT A STOCK MODULE CAST (2026-08-26) ----
// ---- IT DEFERS, IT DOES NOT COMPETE ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class ChuteGuard
    {
        private const string Tag = "[DragonScreen] ";

        public const double MinDescentRate = 5.0;

        private static uint firedFor;
        private static uint stateFor;
        private static bool droguesDone, mainsDone, shroudClosed;

        public static bool DroguesDeployed { get { return droguesDone; } }
        public static bool MainsDeployed { get { return mainsDone; } }

        public static void Tick()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || !v.loaded || v.parts == null) return;
            if (v.mainBody == null || !v.mainBody.atmosphere) return;
            if (v.LandedOrSplashed) return;

            if (v.persistentId != stateFor)
            { stateFor = v.persistentId; droguesDone = false; mainsDone = false; shroudClosed = false; }

            double alt = v.radarAltitude;
            if (v.verticalSpeed > -MinDescentRate) return;

            if (!shroudClosed && v.altitude < v.mainBody.atmosphereDepth)
            {
                DockShroud.Close(v);
                shroudClosed = true;
            }

            if (alt > Entry.DrogueAltitude) return;

            int armed = 0;

            if (!droguesDone && alt <= Entry.DrogueAltitude)
            {
                int n = FireChutes(v, true);
                if (n > 0) { droguesDone = true; armed += n; }
            }
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
                if (VehicleControl.FireByGuiName(p, "deploy chute")) n++;
            }
            return n;
        }

        public static void Reset()
        { firedFor = 0; stateFor = 0; droguesDone = false; mainsDone = false; shroudClosed = false; }
    }
}
