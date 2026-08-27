// DragonScreen — KerBridge  (KSP glue: SOFT-read Kerbal Engineer's live fuel-flow sim by reflection)
// ============================================================================================
// KER (Kerbal Engineer Redux, installed) runs a RealFuels/RO-accurate fuel-flow simulation of the ACTIVE vessel
// and publishes it on the static class KerbalEngineer.VesselSimulator.SimManager. We read it by REFLECTION so
// DragonScreen never has a compile-time reference to KerbalEngineer.dll — a SOFT dependency: if KER is present we
// get proven per-stage Δv/TWR/thrust/Isp/mass/burn-time (mirrored into pure/KerData.KerStage, SI units); if it is
// absent, Available is false and every consumer falls back to our own pure StageStats/Hoverslam. See
// docs/MOD_INTEGRATION_RESEARCH.md §1. ⛔ NEVER make this a hard dependency (user policy 2026-08-28).
//
// Units: KER's VesselSimulator works in kN (thrust) and tonnes (mass); we convert to N/kg here. Δv/Isp/TWR/time
// are unit-unambiguous. ⚠ The kN→N / t→kg conversions + the stage-index semantics are flight-verified against our
// own numbers via the recorder cross-check (a 1000× or off-by-one disagreement is the tell that an assumption is
// wrong) before any consumer TRUSTS KER over its own pure fallback.
// ============================================================================================
using System;
using System.Reflection;
using UnityEngine;

namespace DragonScreen
{
    public static class KerBridge
    {
        static bool _probed, _available;
        static PropertyInfo _pStages;
        static MethodInfo _mRequest, _mReady;
        static FieldInfo _fNumber, _fDeltaV, _fTotalDeltaV, _fThrust, _fActualThrust, _fTwr, _fActualTwr, _fMaxTwr,
                         _fIsp, _fMass, _fTotalMass, _fResourceMass, _fTime;

        public static bool Available { get { Probe(); return _available; } }

        static void Probe()
        {
            if (_probed) return;
            _probed = true;
            try
            {
                Type tSim = null, tStage = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = asm.GetType("KerbalEngineer.VesselSimulator.SimManager", false);
                    if (t != null) { tSim = t; tStage = asm.GetType("KerbalEngineer.VesselSimulator.Stage", false); break; }
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

                _available = _pStages != null && _fDeltaV != null && _fTotalDeltaV != null;
                Debug.Log("[DragonScreen] KerBridge: Kerbal Engineer " + (_available ? "FOUND — soft-reading its fuel-flow sim." : "types incomplete; using our own StageStats."));
            }
            catch (Exception e) { _available = false; Debug.LogWarning("[DragonScreen] KerBridge probe failed: " + e.Message); }
        }

        // Ask KER to (re)run its simulation on the active vessel. KER coalesces requests (150 ms floor); it also
        // runs on its own when its flight engineer is active, so this just keeps the data fresh for us.
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
