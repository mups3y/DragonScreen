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

        // Total 100% (config max) thrust of the engines in a role, N — for sizing a g-limited burn (e.g. the
        // SuperDraco deorbit). Counts them whether or not currently lit, so the throttle can be set BEFORE ignition.
        public static double MaxThrustN(Vessel v, EngineRole want)
        {
            if (v == null) return 0.0;
            double sum = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                string nm = p.name ?? "";
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e != null && Actuation.EngineRoleOf(nm, e.engineID) == want) sum += e.maxThrust * 1000.0;
                }
            }
            return sum;
        }

        // The first live engine playing the wanted role (null if none) — e.g. the MVac for the ullage settle.
        public static ModuleEngines FindEngine(Vessel v, EngineRole want)
        {
            if (v == null) return null;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                string nm = p.name ?? "";
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e != null && Actuation.EngineRoleOf(nm, e.engineID) == want) return e;
                }
            }
            return null;
        }

        // Measured vs available thrust (N) for a role, and how many of its engines are lit — the clamp gate's
        // release signal. `available` is the CURRENT-CONDITIONS max (maxFuelFlow·flowMultiplier·Isp·g0, the
        // MechJeb VesselState formula), NOT the static maxThrust — so at sea level a healthy engine at full
        // throttle reads ~100% (using vacuum maxThrust would read ~82% and never let the gate release a good
        // launch). Falls back to maxThrust if the flow terms are unavailable. finalThrust/maxThrust are in kN.
        public static void EngineThrust(Vessel v, EngineRole want, out double thrustN, out double maxN, out int litCount)
        {
            thrustN = 0.0; maxN = 0.0; litCount = 0;
            if (v == null) return;
            const double g0 = 9.80665;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                string nm = p.name ?? "";
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e == null || Actuation.EngineRoleOf(nm, e.engineID) != want) continue;
                    double isp = e.realIsp > 1f ? e.realIsp : 0.0;
                    double eMaxKn = e.maxFuelFlow * e.flowMultiplier * isp * g0;   // current-conditions max (kN)
                    if (!(eMaxKn > 0.0)) eMaxKn = e.maxThrust;                     // fallback: static config max
                    maxN += eMaxKn * 1000.0;
                    if (e.EngineIgnited && e.isOperational)
                    {
                        thrustN += e.finalThrust * 1000.0;
                        litCount++;
                    }
                }
            }
        }

        // Total thrust (N) of every lit engine on the vessel — the always-on recorder readout, so an
        // uncommanded cutout (like the one that starved the attitude loop at MET 121 s) is visible directly.
        public static double TotalActiveThrustN(Vessel v)
        {
            if (v == null) return 0.0;
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e != null && e.EngineIgnited && e.isOperational) t += e.finalThrust * 1000.0;
                }
            }
            return t;
        }

        // B3 engine-out differential octaweb throttle. Rebalance the OPERATIONAL engines' per-engine thrust
        // limiters (thrustPercentage) so their net torque holds `demandedTorque` — pass Vec3.Zero to NULL the
        // thrust asymmetry a failed engine leaves, keeping the gimbal free for attitude. The effectors are the
        // operational engines (a failed one is simply absent → the asymmetry the solver corrects); net torque
        // is frame-independent, so world-frame force/torque are used. Allocation-free on the symmetric path (a
        // quick net-torque check skips the solve). Returns false → the demand could not be met = FDIR
        // "insufficient control authority". [[falcon-detect-by-capability]] — matched by EngineRole, not name.
        [Tunable] public static double DiffTorqueDeadbandNm = 500.0;   // below this residual, treat as balanced

        public static bool BalanceOctawebThrust(Vessel v, EngineRole want, Vec3 demandedTorqueNm)
        {
            if (v == null) return true;
            Vector3d com = v.CoM;

            // pass 1 (allocation-free): net torque + operational-engine count.
            Vec3 net = Vec3.Zero; int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i]; string nm = p.name ?? "";
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e == null || Actuation.EngineRoleOf(nm, e.engineID) != want) continue;
                    if (!e.EngineIgnited || !e.isOperational) continue;
                    Vec3 f, tq; if (!EngineForceTorque(e, com, out f, out tq)) continue;
                    net = net + tq; n++;
                }
            }
            if (n == 0) return true;

            // symmetric / balanced → hold the limiters at full (write only where they drifted), skip the solve.
            if ((net - demandedTorqueNm).Magnitude < DiffTorqueDeadbandNm)
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i]; string nm = p.name ?? "";
                    for (int m = 0; m < p.Modules.Count; m++)
                    {
                        ModuleEngines e = p.Modules[m] as ModuleEngines;
                        if (e == null || Actuation.EngineRoleOf(nm, e.engineID) != want) continue;
                        if (e.EngineIgnited && e.isOperational && Math.Abs(e.thrustPercentage - 100f) > 0.5f)
                            e.thrustPercentage = 100f;
                    }
                }
                return true;
            }

            // asymmetric (engine-out) → build the effectors and solve the differential throttle (rare path).
            var eng = new System.Collections.Generic.List<ModuleEngines>();
            var force = new System.Collections.Generic.List<Vec3>();
            var torque = new System.Collections.Generic.List<Vec3>();
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i]; string nm = p.name ?? "";
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e == null || Actuation.EngineRoleOf(nm, e.engineID) != want) continue;
                    if (!e.EngineIgnited || !e.isOperational) continue;
                    Vec3 f, tq; if (!EngineForceTorque(e, com, out f, out tq)) continue;
                    eng.Add(e); force.Add(f); torque.Add(tq);
                }
            }
            BalanceResult r = DiffThrottle.Solve(force.ToArray(), torque.ToArray(), demandedTorqueNm);
            for (int i = 0; i < eng.Count; i++)
            {
                float pct = (float)(r.Limits[i] * 100.0);
                if (pct < 0f) pct = 0f; else if (pct > 100f) pct = 100f;
                if (Math.Abs(eng[i].thrustPercentage - pct) > 0.5f) eng[i].thrustPercentage = pct;
            }
            return r.Feasible;
        }

        // One engine's world-frame force + torque-about-CoM at FULL thrust, aggregated over its thrust
        // transforms (thrust pushes the vehicle opposite each nozzle's forward). Guarded; false if unusable.
        static bool EngineForceTorque(ModuleEngines e, Vector3d com, out Vec3 forceN, out Vec3 torqueNm)
        {
            forceN = Vec3.Zero; torqueNm = Vec3.Zero;
            var tts = e.thrustTransforms;
            if (tts == null || tts.Count == 0) return false;
            double eMaxN = e.maxThrust * 1000.0;
            if (!(eMaxN > 0.0)) return false;
            Vector3d f = Vector3d.zero, tq = Vector3d.zero;
            for (int k = 0; k < tts.Count; k++)
            {
                Transform tt = tts[k]; if (tt == null) continue;
                double mult = (e.thrustTransformMultipliers != null && k < e.thrustTransformMultipliers.Count)
                              ? e.thrustTransformMultipliers[k] : 1.0 / tts.Count;
                Vector3d fk = -(Vector3d)tt.forward * (eMaxN * mult);
                Vector3d rk = (Vector3d)tt.position - com;
                f += fk; tq += Vector3d.Cross(rk, fk);
            }
            forceN = new Vec3(f.x, f.y, f.z);
            torqueNm = new Vec3(tq.x, tq.y, tq.z);
            return true;
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

        // ⛔ Move the ERECTOR/strongback AWAY — its "Open Erector" animation (craft dump: TE.Ghidorah.Erector
        // ModuleAnimateGeneric, event Toggle "Open Erector"). The REAL pad sequence retracts the erector BEFORE
        // ignition; only the hold-down decoupler releases at liftoff (Actuator.ReleaseHoldDowns). Detected by
        // capability (IsErector), idempotent (skips an already-open erector). Returns true if it started opening.
        public static bool OpenErector(Vessel v)
        {
            if (v == null) return false;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    if (!VehicleParts.IsErector(p.name)) continue;
                    List<ModuleAnimateGeneric> an = p.Modules.GetModules<ModuleAnimateGeneric>();
                    bool moved = false;
                    for (int m = 0; m < an.Count; m++)
                        if (an[m].Progress < 0.5f) { an[m].Toggle(); moved = true; }
                    if (an.Count > 0) { Debug.Log("[DragonScreen] ERECTOR — moving away (Open Erector)"); return moved; }
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] open erector failed: " + e.Message); }
            return false;
        }

        // Is the erector fully clear (animation open)? — the clamp gate waits for this before liftoff.
        public static bool ErectorClear(Vessel v)
        {
            if (v == null) return true;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    if (!VehicleParts.IsErector(p.name)) continue;
                    List<ModuleAnimateGeneric> an = p.Modules.GetModules<ModuleAnimateGeneric>();
                    for (int m = 0; m < an.Count; m++) if (an[m].Progress < 0.98f) return false;   // still swinging
                    return true;
                }
            }
            catch { }
            return true;   // no erector found → nothing to wait for
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

        // ⛔ Jettison the TRUNK (clears the heat shield for entry) — mandatory on any abort that will re-enter.
        // The trunk decoupler is TE.18.DRAGONV2.TRUNK (DecouplerRole.TrunkJettison), NOT the Dragon-sep one.
        public static bool JettisonTrunk(Vessel v)
        {
            bool ok = FireDecoupler(v, DecouplerRole.TrunkJettison);
            Debug.Log(ok ? "[DragonScreen] TRUNK JETTISON (heat shield clear for entry)"
                         : "[DragonScreen] trunk jettison: no trunk decoupler found");
            return ok;
        }

        // Release the docking hooks (emergency undock). Idempotent — a non-docked node is skipped.
        public static bool Undock(Vessel v)
        {
            if (v == null) return false;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    ModuleDockingNode nd = v.parts[i].Modules.GetModule<ModuleDockingNode>();
                    if (nd != null && nd.otherNode != null)
                    { nd.Undock(); Debug.Log("[DragonScreen] EMERGENCY UNDOCK — hooks released"); return true; }
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] undock failed: " + e.Message); }
            return false;
        }

        // Open the nose shroud (exposes the forward Dracos + the port) — needed before ANY Draco burn on the
        // capsule ([[dragon-nose-cone-rcs]]). Idempotent — an already-open shroud is left alone.
        public static void OpenNoseShroud(Vessel v)
        {
            if (v == null) return;
            try
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    List<ModuleAnimateGeneric> an = v.parts[i].Modules.GetModules<ModuleAnimateGeneric>();
                    for (int m = 0; m < an.Count; m++)
                        if (an[m].animationName == "TE_23_CD2_NOSECONE_ANI" && an[m].Progress < 0.5f)
                        { an[m].Toggle(); Debug.Log("[DragonScreen] nose shroud OPENED (forward Dracos exposed)"); return; }
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] nose shroud open failed: " + e.Message); }
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

        // Total RCS thrust currently being DELIVERED (N) — the sum over every thruster nozzle of its live duty
        // (thrustForces[j]) × its thrusterPower. Ported from MechJeb (MechJebModuleInfoItems). This is the RCS
        // control AUTHORITY actually in use — for the tuning DB, so we can see how hard the Dracos are working
        // per phase and whether they saturate (out of translation/attitude authority on the capsule).
        public static double RcsThrustN(Vessel v)
        {
            if (v == null) return 0.0;
            double sum = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleRCS r = p.Modules[m] as ModuleRCS;
                    if (r == null || !r.rcsEnabled || r.thrustForces == null) continue;
                    for (int j = 0; j < r.thrustForces.Length; j++)
                        sum += r.thrustForces[j] * r.thrusterPower;
                }
            }
            return sum * 1000.0;   // thrusterPower is in kN
        }

        // Turn the RCS master OFF — so the attitude loop stops using the Dracos when a gimballed engine is doing
        // the steering (the S2 burn). Leaving it on made the fine attitude corrections fire the RCS non-stop.
        public static void DisableRcs(Vessel v)
        {
            if (v == null) return;
            if (v.ActionGroups[KSPActionGroup.RCS]) v.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
        }

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

        // ⛔ Cut (release) the drogues or mains — RealChute "Cut chute" event / stock ModuleParachute.CutParachute.
        // In an abort the drogues are cut BEFORE the mains deploy (the compressed abort sequence).
        public static void CutChutes(Vessel v, bool drogue)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                bool isD = VehicleParts.IsDrogues(p.name), isM = VehicleParts.IsMains(p.name);
                if (drogue ? !isD : !isM) continue;
                CutChutePart(p);
            }
        }

        static void CutChutePart(Part p)
        {
            try
            {
                ModuleParachute mp = p.Modules.GetModule<ModuleParachute>();
                if (mp != null)
                {
                    if (mp.deploymentState == ModuleParachute.deploymentStates.DEPLOYED
                        || mp.deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED)
                        mp.CutParachute();
                    return;
                }
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    PartModule pm = p.Modules[m];   // RealChute / any custom chute module
                    foreach (BaseEvent ev in pm.Events)
                    {
                        if (ev == null || !ev.active) continue;
                        string s = (ev.guiName ?? "") + " " + (ev.name ?? "");
                        if (s.IndexOf("cut", StringComparison.OrdinalIgnoreCase) >= 0)
                        { ev.Invoke(); return; }
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] chute cut failed on " + p.name + ": " + e.Message); }
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
