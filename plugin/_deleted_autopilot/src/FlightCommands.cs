// DragonScreen - FlightCommands
// ---- EVERY ACTION HERE IS REAL KSP STATE, OR IT REFUSES ----
// ---- WHAT WAS CHECKED IN THE PART CONFIGS, NOT ASSUMED ----
// ---- DEORBIT NOW IS NO LONGER THE CRUDE ONE ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class FlightCommands
    {
        private const string Tag = "[DragonScreen] ";

        public static bool BackupPyros, EntryReboot, BackupEntry;

        public static bool EscapeArmed = true;

        public static SystemsState State = SystemsState.Fresh();

        public static double Charge01;

        private static bool Reset(int bus)
        {
            if (!Systems.ResetBus(ref State, bus, Charge01))
            {
                Log("RESET " + bus + " refused - charge " + (Charge01 * 100.0).ToString("F0")
                    + "%, need " + (Systems.ResetCharge * 100.0).ToString("F0")
                    + "%, or nothing was tripped");
                return false;
            }
            Log("RESET " + bus + " - strings restored");
            return true;
        }

        private static bool Powered(int bus)
        {
            bool on = (bus == 1) ? State.Bus1On : State.Bus2On;
            if (!on) Log("STRING row " + bus + " unpowered - press POWER " + bus + " first");
            return on;
        }

        private static bool ToggleRendezvousDock()
        {
            if (StationApproach.Engaged || DockingOps.Engaged)
            {
                if (DockingOps.Engaged) DockingOps.Reset();
                if (StationApproach.Engaged) StationApproach.Disengage("crew");
                Log("RENDEZVOUS/DOCK disengaged (STRING 1B)");
                return true;
            }
            MissionOps.AutoDock();
            return true;
        }

        public static bool CancelAllSequences()
        {
            bool any = false;
            if (AutoPilot.Engaged)        { AutoPilot.Disengage("CANCEL");        any = true; }
            if (StationApproach.Engaged)  { StationApproach.Disengage("CANCEL");  any = true; }
            if (DirectApproachOps.Engaged){ DirectApproachOps.Disengage("CANCEL"); any = true; }
            if (DockingOps.Engaged)       { DockingOps.Reset();                    any = true; }
            if (UndockOps.Engaged)        { UndockOps.Reset();                     any = true; }
            if (DeorbitOps.Engaged)       { DeorbitOps.Disengage("CANCEL");       any = true; }
            if (PhaseDownOps.Engaged)     { PhaseDownOps.Reset();                  any = true; }
            if (BurnActive)               { StopBurn("CANCEL");                    any = true; }
            if (any) Log("CANCEL - running sequence(s) stopped");
            return any;
        }

        private const double DeorbitTargetPe = 25000.0;

        private const double WaterDeorbitTargetPe = 40000.0;

        // ------------------------------------------------------------------ dispatch

        public static bool Run(PanelCommand c)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return false;

            try
            {
                switch (c)
                {
                    case PanelCommand.JettisonNoseCone: return ToggleNoseCone(v);
                    case PanelCommand.MainsOnly:        return Deploy(v, mains: true, drogues: false);
                    case PanelCommand.DroguesAndMains:  return Deploy(v, mains: true, drogues: true);
                    case PanelCommand.CutMains:         return CutChutes(v);
                    case PanelCommand.FirePyro:         return FirePyros(v);

                    case PanelCommand.EnableBackupPyros:
                        BackupPyros = !BackupPyros;
                        Log("backup pyros " + (BackupPyros ? "ENABLED" : "disabled"));
                        return true;

                    case PanelCommand.EnableEntryReboot:
                        EntryReboot = !EntryReboot;
                        Log("entry reboot " + (EntryReboot ? "ENABLED" : "disabled"));
                        return true;

                    // ---- ENTRY MODE = LANDING METHOD (user 2026-08-21) ----
                    case PanelCommand.EnableBackupEntry:
                        BackupEntry = true;  EntryOps.PropulsiveRequested = true;
                        Log("entry mode BACKUP - propulsive landing"); return true;
                    case PanelCommand.EnableNormalEntry:
                        BackupEntry = false; EntryOps.PropulsiveRequested = false;
                        Log("entry mode NORMAL - parachute landing"); return true;

                    // ---- POWER STRINGS ----
                    case PanelCommand.Power1: Systems.ToggleBus(ref State, 1);
                        Log("bus 1 " + (State.Bus1On ? "ON" : "OFF")); return true;
                    case PanelCommand.Power2: Systems.ToggleBus(ref State, 2);
                        Log("bus 2 " + (State.Bus2On ? "ON" : "OFF")); return true;

                    case PanelCommand.Reset1: return Reset(1);
                    case PanelCommand.Reset2: return Reset(2);

                    // ---- FLIGHT COMPUTER STRINGS: THE CREW-2 MISSION ON ROW 1. ----
                    case PanelCommand.String1A: if (!Powered(1)) return false;
                        AutoPilot.Toggle();  return true;
                    case PanelCommand.String1B: if (!Powered(1)) return false;
                        return ToggleRendezvousDock();
                    case PanelCommand.String1C: if (!Powered(1)) return false;
                        DeorbitOps.Toggle(); return true;

                    // ---- CABIN EMERGENCIES ----
                    case PanelCommand.DepressResponse:
                        if (!Systems.DepressResponse(ref State)) { Log("no leak to isolate"); return false; }
                        Log("DEPRESS RESPONSE - cabin isolating"); return true;

                    case PanelCommand.SuppressFire:
                        if (!Systems.SuppressFire(ref State))
                        { Log("no fire, or suppressant spent"); return false; }
                        Log("SUPPRESS FIRE - bottle discharged, "
                            + (State.Suppressant * 100.0).ToString("F0") + "% left"); return true;

                    case PanelCommand.FireResponse:
                        if (!Systems.FireResponse(ref State)) { Log("no fire to respond to"); return false; }
                        Log("FIRE RESPONSE - bus 2 shed, suppressant discharged"); return true;

                    case PanelCommand.SwapString1:
                    case PanelCommand.SwapString2:
                    case PanelCommand.SwapString3:
                        Log("entry string swap - undecided function, see REAL_DRAGON_SCREENS.md");
                        return true;

                    // ---- ⛔ THE REAL DE-ORBIT, NOT THE OLD RETROGRADE BURN ----
                    // ---- ⛔ THE EMERGENCY LANDINGS: IMMEDIATE, PARACHUTE, ARM+EXECUTE ----
                    case PanelCommand.DeorbitNow:
                        EntryOps.PropulsiveRequested = false;
                        return StartDeorbit(v, DeorbitTargetPe, "DEORBIT NOW");
                    case PanelCommand.WaterDeorbit:
                        EntryOps.PropulsiveRequested = false;
                        return StartDeorbit(v, WaterDeorbitTargetPe, "WATER DEORBIT");
                    case PanelCommand.Breakout:     return Breakout(v);
                    case PanelCommand.Abort:        return Abort(v);
                }
            }
            catch (Exception e)
            {
                Debug.LogError(Tag + "command " + c + " threw: " + e);
                return false;
            }

            return false;
        }

        // ------------------------------------------------------------------ mechanisms

        private static bool ToggleNoseCone(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleAnimateGeneric> mods = v.parts[i].Modules.GetModules<ModuleAnimateGeneric>();
                for (int m = 0; m < mods.Count; m++)
                {
                    if (mods[m].animationName != "TE_23_CD2_NOSECONE_ANI") continue;
                    mods[m].Toggle();
                    Log("nose cone toggled -> " + (mods[m].Progress > 0.5f ? "OPEN" : "CLOSED"));
                    return true;
                }
            }
            Log("no nose cone animation on this vessel - JETTISON NOSE CONE refused");
            return false;
        }

        private static bool Deploy(Vessel v, bool mains, bool drogues)
        {
            int fired = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p == null) continue;
                bool isDrogue = VehicleParts.IsDrogues(p.name);
                bool isMain = VehicleParts.IsMains(p.name);
                if (!isDrogue && !isMain) continue;
                if (isDrogue && !drogues) continue;
                if (isMain && !mains) continue;
                if (VehicleControl.FireByGuiName(p, "deploy chute")) fired++;
            }
            Log("chutes deployed: " + fired + " (mains=" + mains + " drogues=" + drogues + ")");
            return fired > 0;
        }

        private static bool CutChutes(Vessel v)
        {
            int cut = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p == null) continue;
                if (!VehicleParts.IsDrogues(p.name) && !VehicleParts.IsMains(p.name)) continue;
                if (VehicleControl.FireByGuiName(p, "cut main chute")) cut++;
            }
            Log("chutes cut: " + cut);
            return cut > 0;
        }

        private static bool FirePyros(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (!VehicleParts.IsTrunk(p.name)) continue;

                List<ModuleDecouple> ds = p.Modules.GetModules<ModuleDecouple>();
                for (int m = 0; m < ds.Count; m++)
                {
                    if (ds[m].isDecoupled) continue;
                    ds[m].Decouple();
                    Log("trunk pyro fired on '" + p.name + "'");
                    return true;
                }
            }
            Log("no undecoupled trunk decoupler found - FIRE PYRD refused");
            return false;
        }

        private static bool Breakout(Vessel v)
        {
            v.ActionGroups.SetGroup(KSPActionGroup.Abort, true);
            FlightInputHandler.state.mainThrottle = 1f;
            Log("BREAKOUT - abort action group fired, throttle full");
            return true;
        }

        private static bool Abort(Vessel v)
        {
            v.ActionGroups.SetGroup(KSPActionGroup.Abort, true);
            Log("ABORT handle - abort action group fired");
            return true;
        }

        // ------------------------------------------------------------------ deorbit

        public static double BurnTargetPe { get; private set; }

        public static bool BurnActive { get { return BurnTargetPe > 0.0; } }

        private static bool StartDeorbit(Vessel v, double targetPe, string what)
        {
            if (v.situation == Vessel.Situations.LANDED
                || v.situation == Vessel.Situations.SPLASHED
                || v.situation == Vessel.Situations.PRELAUNCH)
            {
                Log(what + " refused - on the ground");
                return false;
            }
            if (v.orbit == null || v.orbit.PeA <= targetPe)
            {
                Log(what + " refused - periapsis is already at or below "
                    + (targetPe / 1000.0).ToString("F0") + " km");
                return false;
            }

            // ---- ⛔ NOT WITH THE SECOND STAGE STILL ON. ----
            for (int i = 0; i < v.parts.Count; i++)
            {
                if (!VehicleParts.IsSecondStage(v.parts[i].name)) continue;
                Log(what + " REFUSED - the second stage is still attached. It should have gone at "
                    + "the 40 km periapsis gate during ascent (see AutoPilot.SeparateSecondStage). "
                    + "Drop it on the '" + VehicleParts.DragonDecouplerMarker + "' first.");
                return false;
            }

            // ---- THE TRUNK GOES BEFORE THE BURN, WHICH IS THE REAL PROFILE ----
            if (FirePyros(v)) Log(what + " - trunk jettisoned before the burn");
            else Log(what + " - no trunk to jettison (already gone)");

            BurnTargetPe = targetPe;
            // ---- ONE CONTROLLER AT A TIME ----
            AttitudeController held = AttitudeController.For(v);
            if (held != null) held.Release(v);
            v.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
            Log(what + " ignition, target Pe " + (targetPe / 1000.0).ToString("F0")
                + " km. ⚠ INTERIM GUIDANCE: retrograde burn to periapsis, not the entry solution in "
                + "docs/FLIGHT_SOFTWARE_PLAN.md.");
            return true;
        }

        private static int lastTickFrame = -1;

        public static void Tick()
        {
            if (Time.frameCount == lastTickFrame) return;
            lastTickFrame = Time.frameCount;

            if (!BurnActive) return;

            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.orbit == null) { StopBurn("no vessel"); return; }

            if (v.orbit.PeA <= BurnTargetPe) { StopBurn("target periapsis reached"); return; }

            v.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Retrograde);

            // ---- VARIABLE THRUST, NOT BANG-BANG ----
            Vector3d retro = -v.obt_velocity.normalized;
            double align = Vector3d.Dot(v.ReferenceTransform.up.normalized, retro);
            if (align <= 0.7) { FlightInputHandler.state.mainThrottle = 0f; return; }

            double peErr = v.orbit.PeA - BurnTargetPe;
            double th = Deorbit.BurnThrottle(peErr, 0.0);
            double ease = (align - 0.7) / 0.3;
            if (ease > 1.0) ease = 1.0;
            FlightInputHandler.state.mainThrottle = (float)(th * ease);
        }

        public static void StopBurn(string why)
        {
            if (!BurnActive) return;
            BurnTargetPe = 0.0;
            FlightInputHandler.state.mainThrottle = 0f;
            Log("burn ended - " + why);
        }

        private static void Log(string s) { Debug.Log(Tag + "cmd: " + s); }
    }
}
