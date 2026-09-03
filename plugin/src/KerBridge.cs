// DragonScreen — KerBridge  (KSP glue: SOFT-read Kerbal Engineer's live fuel-flow sim by reflection)
// ============================================================================================
// KER (Kerbal Engineer Redux, installed) runs a RealFuels/RO-accurate fuel-flow simulation of the ACTIVE vessel
// and publishes it on the static class KerbalEngineer.VesselSimulator.SimManager. We read it by REFLECTION so
// DragonScreen never has a compile-time reference to KerbalEngineer.dll — a SOFT dependency: if KER is present we
// get proven per-stage Δv/TWR/thrust/Isp/mass/burn-time (mirrored into pure/KerData.KerStage, SI units); if it is
// absent, Available is false and every consumer draws a dash. See docs/KER_DATA_RESEARCH.md §1.
// ⛔ NEVER make this a hard dependency (user policy 2026-08-28).
//
// ---- WHAT IS REAL AND WHAT IS MODELLED (S46) ----
//     REAL      everything this file returns. Every number is KER's own fuel-flow solve of the REAL part tree
//               (RealFuels/RO engine models, crossfeed, per-engine grouping), read live and only mirrored
//               into SI here. Nothing on this path is modelled, interpolated or invented.
//     MODELLED  nothing.
//     ABSENT    KER not installed, or no result yet for this vessel, or we are DOCKED → null → the page
//               dashes the row. A dash is the honest answer; a stale or merged-stack number is not.
// This is tier-2 under BUILD_PLAN §14.4(e) step (1) — "an installed mod's value" — and is MARKED per §5.3
// mechanism 1: this note, the PageState doc-comment on KerPerformance, and a docs/TELEMETRY_REGISTRY.md row.
// KER is GPL-3.0 (CYBUTEK / jrbudda); DragonScreen is GPL-3.0 already and this copies no code and holds no
// compile-time reference, so the obligation is ATTRIBUTION only — see README.md and §5.4.
//
// ---- READING IS NOT ENOUGH: WE HAVE TO DRIVE THE PROCESSOR (§1.3/§1.6) ----
// KER does NOT compute continuously. Its values are produced by processor objects that FlightEngineerCore
// ticks only when (a) they are REGISTERED via AddUpdatable and (b) UpdateRequested is set that frame — and KER
// itself does both only for readouts the user has switched on in its own window. So a plugin that just reads
// the statics gets whatever zeros they were born with. Attach() does (a) once per flight scene;
// RequestUpdate() does (b) on our existing 5 Hz tick and the value is read on the NEXT tick. FlightEngineerCore
// is [KSPAddon(FlightAndKSC)], so this works with KER's window shut.
// ⚠ SimManager.RequestSimulation() below is NOT that path and never was: it only sets a flag
// (TryStartSimulation is what starts a run, and in flight only SimulationProcessor.Update() calls it). It is
// left as-is — REGISTER.md S45 finding 1 logs it — and is not on the live path; RequestUpdate() is.
//
// Units: KER's VesselSimulator works in kN (thrust) and tonnes (mass); we convert to N/kg here. Δv/Isp/TWR/time
// are unit-unambiguous. ⚠ UNVERIFIED IN FLIGHT: the kN→N / t→kg conversions and the stage-index semantics have
// never been cross-checked against a live game, because until now KER had no consumer at all (a 1000× or
// off-by-one disagreement is the tell). Held as a glass-go item — docs/KER_DATA_RESEARCH.md §6.2 V1/V2/V4.
// ============================================================================================
using System;
using System.Reflection;
using UnityEngine;

namespace DragonScreen
{
    public static class KerBridge
    {
        static bool _probed, _available, _driven;
        static PropertyInfo _pStages;
        static MethodInfo _mRequest, _mReady;
        static FieldInfo _fNumber, _fDeltaV, _fTotalDeltaV, _fThrust, _fActualThrust, _fTwr, _fActualTwr, _fMaxTwr,
                         _fIsp, _fMass, _fTotalMass, _fResourceMass, _fTime;

        // ---- the PROCESSOR plane (§1.3 plane B): what lets us drive the simulation ourselves ----
        static PropertyInfo _pCoreInstance, _pProcInstance, _pShowDetails;
        static MethodInfo _mAddUpdatable, _mProcRequest;
        // The FlightEngineerCore we registered with, held BY REFERENCE and compared by reference. It is a
        // [KSPAddon(FlightAndKSC)] MonoBehaviour, so a new one exists per scene with an empty module list —
        // identity is therefore the scene test, and re-registering is exactly what a scene change needs.
        static object _attachedTo;

        /// <summary>KER is installed and its fuel-flow types bound (guard level 1, §5.2).</summary>
        public static bool Available { get { Probe(); return _available; } }

        /// <summary>We can also DRIVE it — FlightEngineerCore.AddUpdatable + SimulationProcessor.RequestUpdate /
        /// .ShowDetails all bound. Separate from <see cref="Available"/> on purpose: a KER build that renamed a
        /// processor member must degrade to a dash, not take the whole bridge down, and partial matches are
        /// rejected (all five handles or none) rather than half-bound (§5.2 level 1).</summary>
        public static bool Driven { get { Probe(); return _available && _driven; } }

        static void Probe()
        {
            if (_probed) return;
            _probed = true;
            try
            {
                Type tSim = null, tStage = null, tCore = null, tProc = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = asm.GetType("KerbalEngineer.VesselSimulator.SimManager", false);
                    if (t == null) continue;
                    tSim   = t;
                    tStage = asm.GetType("KerbalEngineer.VesselSimulator.Stage", false);
                    // Same assembly, by construction — KER ships one DLL. Bound here rather than in a second
                    // walk so a KER that is present cannot be half-found.
                    tCore  = asm.GetType("KerbalEngineer.Flight.FlightEngineerCore", false);
                    tProc  = asm.GetType("KerbalEngineer.Flight.Readouts.Vessel.SimulationProcessor", false);
                    break;
                }
                if (tSim == null || tStage == null) return;

                const BindingFlags SP = BindingFlags.Public | BindingFlags.Static;
                const BindingFlags FP = BindingFlags.Public | BindingFlags.Instance;
                _pStages       = tSim.GetProperty("Stages", SP);
                _mRequest      = tSim.GetMethod("RequestSimulation", SP, null, Type.EmptyTypes, null);
                _mReady        = tSim.GetMethod("ResultsReady", SP, null, Type.EmptyTypes, null);
                _fNumber       = tStage.GetField("number", FP);
                _fDeltaV       = tStage.GetField("deltaV", FP);
                _fTotalDeltaV  = tStage.GetField("totalDeltaV", FP);
                _fThrust       = tStage.GetField("thrust", FP);
                _fActualThrust = tStage.GetField("actualThrust", FP);
                _fTwr          = tStage.GetField("thrustToWeight", FP);
                _fActualTwr    = tStage.GetField("actualThrustToWeight", FP);
                _fMaxTwr       = tStage.GetField("maxThrustToWeight", FP);
                _fIsp          = tStage.GetField("isp", FP);
                _fMass         = tStage.GetField("mass", FP);
                _fTotalMass    = tStage.GetField("totalMass", FP);
                _fResourceMass = tStage.GetField("resourceMass", FP);
                _fTime         = tStage.GetField("time", FP);

                if (tCore != null && tProc != null)
                {
                    _pCoreInstance = tCore.GetProperty("Instance", SP);
                    // No signature filter: AddUpdatable takes KER's own IUpdatable, a type we deliberately do
                    // not name, and it is the only overload.
                    _mAddUpdatable = tCore.GetMethod("AddUpdatable", BindingFlags.Public | BindingFlags.Instance);
                    _pProcInstance = tProc.GetProperty("Instance", SP);
                    _mProcRequest  = tProc.GetMethod("RequestUpdate", SP, null, Type.EmptyTypes, null);
                    _pShowDetails  = tProc.GetProperty("ShowDetails", SP);
                    _driven = _pCoreInstance != null && _mAddUpdatable != null && _pProcInstance != null
                              && _mProcRequest != null && _pShowDetails != null;
                }

                _available = _pStages != null && _fDeltaV != null && _fTotalDeltaV != null;
                Debug.Log("[DragonScreen] KerBridge: Kerbal Engineer " + (_available ? "FOUND — soft-reading its fuel-flow sim." : "types incomplete; using our own StageStats.")
                          + (_available ? (_driven ? "  SimulationProcessor drivable." : "  ⚠ processor types incomplete — propulsion rows will dash.") : ""));
            }
            catch (Exception e) { _available = false; Debug.LogWarning("[DragonScreen] KerBridge probe failed: " + e.Message); }
        }

        // ⚠ NOT the live path, and never worked as one — see the header. SimManager.RequestSimulation() only
        // sets bRequested; TryStartSimulation() starts the run, and in flight only SimulationProcessor.Update()
        // calls that. Kept as-is (REGISTER.md S45 finding 1 logs it); nothing in DragonScreen calls it. Use RequestUpdate().
        public static void RequestSimulation()
        {
            if (!Available) return;
            try { if (_mRequest != null) _mRequest.Invoke(null, null); }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] KerBridge request failed: " + e.Message); }
        }

        public static bool ResultsReady()
        {
            if (!Available || _mReady == null) return false;
            try { object r = _mReady.Invoke(null, null); return r is bool && (bool)r; }
            catch { return false; }
        }

        /// <summary>
        /// Register SimulationProcessor with the flight scene's FlightEngineerCore — step (a) of §1.6. Idempotent
        /// by core identity, so calling it every tick costs one reflected property read and a reference compare;
        /// that is deliberate, because the core may not exist yet when the prop's Start() runs, and a scene change
        /// swaps it for a fresh one with an empty module list. Nothing here throws outward.
        /// </summary>
        public static void Attach()
        {
            if (!Driven) return;
            try
            {
                object core = _pCoreInstance.GetValue(null, null);
                // No core yet (or the scene went away): forget the old one so the next tick re-registers.
                if (core == null) { _attachedTo = null; return; }
                if (ReferenceEquals(core, _attachedTo)) return;
                object proc = _pProcInstance.GetValue(null, null);
                if (proc == null) return;
                _mAddUpdatable.Invoke(core, new object[] { proc });
                _attachedTo = core;
                Debug.Log("[DragonScreen] KerBridge: SimulationProcessor registered with FlightEngineerCore.");
            }
            catch (Exception e)
            {
                _attachedTo = null;
                if (LogGate.First("ker-attach")) Debug.LogWarning("[DragonScreen] KerBridge attach failed: " + e.Message);
            }
        }

        /// <summary>
        /// Step (b) of §1.6: set UpdateRequested so FlightEngineerCore runs the processor THIS frame — the value
        /// is read on the NEXT tick. Call once per VesselData.Refresh() (5 Hz), never per screen: the solve walks
        /// the whole part tree, and ScreenPainter.Update() runs three times a frame. Attaches first, so a scene
        /// entered after the prop's Start() still ends up registered.
        /// </summary>
        public static void RequestUpdate()
        {
            if (!Driven) return;
            Attach();
            try { _mProcRequest.Invoke(null, null); }
            catch (Exception e)
            {
                if (LogGate.First("ker-request")) Debug.LogWarning("[DragonScreen] KerBridge request-update failed: " + e.Message);
            }
        }

        /// <summary>KER's own validity gate (guard level 2, §5.2): SimulationProcessor sets this only once it has
        /// both Stages and a LastStage for the active vessel. NOT ResultsReady(), which is !bRunning and is true
        /// before the first run has ever happened (§1.5).</summary>
        public static bool ShowDetails
        {
            get
            {
                if (!Driven) return false;
                try { object r = _pShowDetails.GetValue(null, null); return r is bool && (bool)r; }
                catch { return false; }
            }
        }

        /// <summary>
        /// The read a consumer should use: stages, but ONLY when we are the ones driving the processor and KER
        /// says it has a result for this vessel. Both guards matter — without Driven we would be reporting
        /// whatever the user's own KER window happened to leave behind, and without ShowDetails we would report a
        /// processor's birth zeros as a confident number.
        /// </summary>
        public static bool TryGetPerformance(out KerStage[] stages)
        {
            stages = null;
            if (!Driven || !ShowDetails) return false;
            return TryGetStages(out stages);
        }

        // Mirror KER's current Stages[] into our SI POCO. Returns false (stages=null) if KER is absent or has not
        // produced a result for the active vessel yet — the caller then uses its own pure fallback.
        public static bool TryGetStages(out KerStage[] stages)
        {
            stages = null;
            if (!Available) return false;
            try
            {
                Array arr = _pStages.GetValue(null, null) as Array;
                if (arr == null || arr.Length == 0) return false;
                KerStage[] outp = new KerStage[arr.Length];
                for (int i = 0; i < arr.Length; i++)
                {
                    object s = arr.GetValue(i);
                    if (s == null) return false;
                    outp[i] = Read(s);
                }
                stages = outp;
                return true;
            }
            catch (Exception e) { Debug.LogWarning("[DragonScreen] KerBridge read failed: " + e.Message); return false; }
        }

        static KerStage Read(object s)
        {
            KerStage k = new KerStage();
            k.Number         = _fNumber != null ? Convert.ToInt32(_fNumber.GetValue(s)) : 0;
            k.DeltaVMps       = D(_fDeltaV, s);
            k.TotalDeltaVMps  = D(_fTotalDeltaV, s);
            k.ThrustN         = D(_fThrust, s) * 1000.0;        // KER kN → N
            k.ActualThrustN   = D(_fActualThrust, s) * 1000.0;
            k.Twr             = D(_fTwr, s);
            k.ActualTwr       = D(_fActualTwr, s);
            k.MaxTwr          = D(_fMaxTwr, s);
            k.IspS            = D(_fIsp, s);
            k.MassKg          = D(_fMass, s) * 1000.0;          // KER t → kg
            k.TotalMassKg     = D(_fTotalMass, s) * 1000.0;
            k.ResourceMassKg  = D(_fResourceMass, s) * 1000.0;
            k.BurnTimeS       = D(_fTime, s);
            k.Valid           = true;
            return k;
        }

        static double D(FieldInfo f, object s)
        {
            if (f == null) return 0.0;
            try { return Convert.ToDouble(f.GetValue(s)); } catch { return 0.0; }
        }
    }
}
