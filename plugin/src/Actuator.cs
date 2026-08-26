// DragonScreen — Actuator  (KSP glue: DIRECT part-module control — the ONLY place that touches parts)
// ============================================================================================
// ⛔ HARD RULE ([[direct-part-control-hard-rule]]): the autopilot NEVER stages and NEVER fires an
// action-group binding to actuate the vehicle. It reaches the live PART MODULES and calls them:
// ModuleEngines.Activate/Shutdown, ModuleDecouple/custom-decoupler .Decouple(), ModuleRCS.rcsEnabled,
// RealChute deploy, leg/fin deploy, the SuperDraco abort motor. WHICH part plays WHICH role is decided by
// the pure classifier (pure/Actuation.cs + pure/VehicleParts.cs), so the capability→actuation mapping is
// headless-tested against the real craft (test/ActuationTest.cs), never re-discovered live.
//
// The one deliberate exception is the RCS *master* toggle (KSPActionGroup.RCS): in KSP a thruster only
// answers FlightCtrlState.X/Y/Z translation while the vessel-level RCS flag is set, so EnableRcs sets both
// the per-thruster rcsEnabled (the deterministic per-thruster control the rule asks for) AND the master —
// exactly as our porting reference MechJeb does in every controller that translates on RCS (NodeExecutor,
// ThrustController, RCSController). That master is a stock vessel enable, not a VAB-dependent AG binding,
// which is the class of thing the rule forbids. Everything else here is pure direct module actuation.
//
// Defensive throughout: a failed actuation logs and returns; it never throws into the control tick.
// ============================================================================================
using System;
using System.Collections.Generic;
using ModuleWheels;
using UnityEngine;

namespace DragonScreen
{
    public static class Actuator
    {
        // ============================ ENGINES ============================

        // Activate every live engine matching the wanted role (idempotent — an already-lit engine is left).
        // Returns how many engines the role matched (whether or not they needed lighting).
        public static int ActivateEngines(Vessel v, EngineRole want)
        {
            int matched = 0;
            if (v == null) return 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                string nm = p.name ?? "";
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e == null) continue;
                    if (Actuation.EngineRoleOf(nm, e.engineID) != want) continue;
                    matched++;
                    if (!e.EngineIgnited)
                        try { e.Activate(); } catch (Exception ex) { Debug.LogWarning("[DragonScreen] engine activate failed on " + nm + ": " + ex.Message); }
                }
            }
            return matched;
        }

        // Shut every lit engine matching the role.
        public static void ShutdownEngines(Vessel v, EngineRole want)
        {
            if (v == null) return;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                string nm = p.name ?? "";
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e == null || !e.EngineIgnited) continue;
                    if (Actuation.EngineRoleOf(nm, e.engineID) != want) continue;
                    try { e.Shutdown(); } catch (Exception ex) { Debug.LogWarning("[DragonScreen] engine shutdown failed on " + nm + ": " + ex.Message); }
                }
            }
        }

        // Shut ALL lit engines on booster parts (covers whichever octaweb mode is currently lit).
        public static void ShutdownBoosterEngines(Vessel v)
        {
            if (v == null) return;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (!VehicleParts.IsBooster(p.name)) continue;
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e != null && e.EngineIgnited)
                        try { e.Shutdown(); } catch (Exception ex) { Debug.LogWarning("[DragonScreen] booster shutdown failed: " + ex.Message); }
                }
            }
        }

        // Liftoff: light ONLY the octaweb all-engines mode (⛔ never Three/Centre — flight_0822 tank cook).
        public static int IgniteOctawebLiftoff(Vessel v)
        {
            int n = ActivateEngines(v, EngineRole.OctawebAll);
            Debug.Log("[DragonScreen] octaweb liftoff ignition — " + n + " all-engines module(s) lit");
            return n;
        }

        // SES-1: light the MVac (second stage). ⛔ ullage must already be settled (Step C wires that ahead).
        public static int IgniteSecondStage(Vessel v)
        {
            int n = ActivateEngines(v, EngineRole.SecondStage);
            Debug.Log("[DragonScreen] S2 (MVac) ignition — " + n + " engine(s) lit");
            return n;
        }

        // MECO + stage separation: cut the octaweb, then fire the interstage decoupler (S1 falls away).
        public static void Meco(Vessel v)
        {
            ShutdownBoosterEngines(v);
            bool sep = FireDecoupler(v, DecouplerRole.StageSep);
            Debug.Log("[DragonScreen] MECO — octaweb shut, interstage " + (sep ? "decoupled" : "NOT FOUND"));
        }

        // ============================ DECOUPLERS ============================

        // Fire the (first) decoupler playing the given role. Returns true if one fired.
        public static bool FireDecoupler(Vessel v, DecouplerRole role)
        {
            if (v == null || role == DecouplerRole.None) return false;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (Actuation.DecouplerRoleOf(p.name) != role) continue;
                if (FirePartDecoupler(p)) return true;
            }
            return false;
        }

        // SECO: drop the spent second stage (the Dragon + trunk stays). ⛔ NOT the trunk decoupler.
        public static bool SeparateDragon(Vessel v)
        {
            bool ok = FireDecoupler(v, DecouplerRole.DragonSep);
            Debug.Log(ok ? "[DragonScreen] SECO — Dragon separated from S2" : "[DragonScreen] SECO: no Dragon decoupler found");
            return ok;
        }

        // Fire a part's decoupler however it is implemented: the stock base classes, or — for custom
        // decouplers like ModuleTundraDecoupler we cannot reference by type — its "Decouple" event by name.
        public static bool FirePartDecoupler(Part p)
        {
            try
            {
                ModuleDecouple d = p.Modules.GetModule<ModuleDecouple>();
                if (d != null && !d.isDecoupled) { d.Decouple(); return true; }
                ModuleAnchoredDecoupler a = p.Modules.GetModule<ModuleAnchoredDecoupler>();
                if (a != null && !a.isDecoupled) { a.Decouple(); return true; }
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    PartModule pm = p.Modules[m];
                    foreach (BaseEvent ev in pm.Events)
                    {
                        if (ev == null || !ev.active) continue;
                        string gn = ev.guiName ?? "", nm = ev.name ?? "";
                        if (gn.IndexOf("decouple", StringComparison.OrdinalIgnoreCase) >= 0
                            || nm.IndexOf("decouple", StringComparison.OrdinalIgnoreCase) >= 0)
                        { ev.Invoke(); return true; }
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] decoupler fire failed on " + p.name + ": " + e.Message); }
            return false;
        }

        // ⛔ Free EVERYTHING physically holding the rocket: the launch CLAMPS *and* the ERECTOR/strongback.
        // The erector (TE.Ghidorah.Erector) is NOT a LaunchClamp — it holds through a ModuleTundraDecoupler,
        // so LaunchClamp.Release() alone missed it and its asymmetric drag at max-Q ran the AoA away (RUD).
        // Idempotent — releasing a freed hold-down is a no-op.
        public static void ReleaseHoldDowns(Vessel v)
        {
            int clamps = 0, erectors = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                LaunchClamp lc = p.Modules.GetModule<LaunchClamp>();
                if (lc != null) { try { lc.Release(); clamps++; } catch { } }

                string nm = p.name ?? "";
                bool erectorOrClamp = Actuation.DecouplerRoleOf(nm) == DecouplerRole.Erector
                    || nm.IndexOf("Clamp", StringComparison.OrdinalIgnoreCase) >= 0
                    || nm.IndexOf("Strongback", StringComparison.OrdinalIgnoreCase) >= 0;
                if (erectorOrClamp && FirePartDecoupler(p)) erectors++;
            }
            Debug.Log("[DragonScreen] hold-downs released: " + clamps + " clamp(s), " + erectors + " erector/clamp decoupler(s)");
        }

        // ============================ ABORT (SuperDraco launch escape) ============================
        // ⛔ Direct equivalent of the old Abort action group: light the pod's SuperDraco motor at full
        // throttle and separate the capsule (drop S2). Chutes-to-splashdown is the caller's job.
        public static void FireAbort(Vessel v)
        {
            FlightDriver.SetThrottle(1.0);                 // SuperDracos fire at full — own the throttle
            int n = ActivateEngines(v, EngineRole.PodAbort);
            bool sep = FireDecoupler(v, DecouplerRole.DragonSep);
            Debug.Log("[DragonScreen] ⛔ ABORT — SuperDraco motor(s) lit: " + n + ", capsule separated: " + sep);
        }

        // ============================ RCS ============================
        // Enable RCS for translation/rotation: per-thruster rcsEnabled (deterministic control) + the vessel
        // master (KSP requires it for FlightCtrlState translation to actuate — MechJeb-confirmed). See header.
        public static void EnableRcs(Vessel v)
        {
            if (v == null) return;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleRCS r = p.Modules[m] as ModuleRCS;   // ModuleRCSFX derives from ModuleRCS
                    if (r != null && !r.rcsEnabled) r.rcsEnabled = true;
                }
            }
            if (!v.ActionGroups[KSPActionGroup.RCS]) v.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
        }

        public static bool IsRcsOn(Vessel v) { return v != null && v.ActionGroups[KSPActionGroup.RCS]; }

        // ============================ LEGS / FINS ============================

        // Deploy the landing legs (stock ModuleWheelDeployment). Ported from MechJeb's DeployLandingGears:
        // toggle ONLY from the retracted/retracting state, so it extends and is idempotent (already-down legs
        // are left alone) — no dependence on the Gear action group.
        public static void DeployLegs(Vessel v)
        {
            try
            {
                int n = 0;
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    foreach (ModuleWheelDeployment wd in p.FindModulesImplementing<ModuleWheelDeployment>())
                    {
                        if (wd.fsm != null && (wd.fsm.CurrentState == wd.st_retracted || wd.fsm.CurrentState == wd.st_retracting))
                        { wd.EventToggle(); n++; }
                    }
                }
                Debug.Log("[DragonScreen] landing legs extended on " + n + " deployment module(s)");
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] leg deploy failed: " + e.Message); }
        }

        // Deploy the grid fins (their ModuleAnimateGeneric). Caller latches to a single call.
        public static void DeployGridFins(Vessel v)
        {
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    if (p.name == null || p.name.IndexOf("Grid Fin", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    List<ModuleAnimateGeneric> an = p.Modules.GetModules<ModuleAnimateGeneric>();
                    for (int m = 0; m < an.Count; m++) if (an[m].Progress < 0.5f) an[m].Toggle();
                }
                Debug.Log("[DragonScreen] booster grid fins deployed");
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] fin deploy failed: " + e.Message); }
        }

        // ============================ PARACHUTES (RealChute-aware) ============================
        // ⛔ Deploy the drogues or mains — for STOCK ModuleParachute OR RealChute (this vehicle uses
        // RealChute/FAR, so ModuleParachute.Deploy alone did nothing and the crew hit the water at 139 m/s).
        // Selected by the drogue/main PART, deployed however the chute module exposes it.
        public static void DeployChutes(Vessel v, bool drogue)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                bool isD = VehicleParts.IsDrogues(p.name), isM = VehicleParts.IsMains(p.name);
                if (drogue ? !isD : !isM) continue;
                DeployChutePart(p);
            }
        }

        static void DeployChutePart(Part p)
        {
            try
            {
                ModuleParachute mp = p.Modules.GetModule<ModuleParachute>();
                if (mp != null)
                {
                    if (mp.deploymentState == ModuleParachute.deploymentStates.STOWED
                        || mp.deploymentState == ModuleParachute.deploymentStates.ACTIVE)
                        mp.Deploy();
                    return;
                }
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    PartModule pm = p.Modules[m];   // RealChute / any custom chute module
                    foreach (BaseEvent ev in pm.Events)
                    {
                        if (ev == null || !ev.active) continue;
                        string s = (ev.guiName ?? "") + " " + (ev.name ?? "");
                        if (s.IndexOf("deploy", StringComparison.OrdinalIgnoreCase) >= 0
                            && s.IndexOf("cut", StringComparison.OrdinalIgnoreCase) < 0)
                        { ev.Invoke(); return; }
                    }
                    foreach (BaseAction ba in pm.Actions)
                    {
                        string s = (ba.guiName ?? "") + " " + (ba.name ?? "");
                        if (s.IndexOf("deploy", StringComparison.OrdinalIgnoreCase) >= 0
                            && s.IndexOf("cut", StringComparison.OrdinalIgnoreCase) < 0)
                        { ba.Invoke(new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate)); return; }
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] chute deploy failed on " + p.name + ": " + e.Message); }
        }
    }
}
