// DragonScreen — Ullage  (KSP glue: read RealFuels propellant-settling state via reflection)
// ============================================================================================
// ⛔⛔ RESTORED AS AN **OPEN DEFECT**, AND **DELIBERATELY NOT WIRED IN**. ⛔⛔
// ============================================================================================
// Register **W5**, 2026-09-05. Restored from `8b81816^` — 3,917 B, byte-for-byte R1 §5.1's row — with
// **no line of logic changed**; every edit below this banner is a COMMENT. R1 §7.1 lists this file, with
// `pure/IgnitionGate.cs`, under *"directly implicated — a named, located, **UNFIXED** defect"*. The full
// statement of the defect, the flight evidence, the honest "the proximate root is NOT proven" caveat and
// the retry-policy answer are in **`pure/IgnitionGate.cs`'s header** — read it, it is the primary record
// and is not duplicated here. This header states only what is specific to THIS file.
//
// ⭐ **WHY IT IS RESTORED AT ALL, AND WHY THE REFLECTION STAYS.** C1.15 (evidence-gated mod-first) names
// this file BY NAME as the already-installed source a screens pass had been ignoring: **RealFuels IS
// installed** (`docs/reference/INSTALLED_MODS.md` row 1 names it, and names this file as its reader) and
// **no other installed mod models ullage**. R1 §5.1 grades it *"RSS-RO (RealFuels) — irreplaceable, no
// stock analogue"*. So the quantity has a real mod source and needs no simulation, and §14.4(e)/(f) do
// not apply. ⛔ The reflection is kept exactly as it was. Nothing here was replaced by a model.
//
// ============================================================================================
// ⛔ THE DEFECT IN THIS FILE — IT FAILS **OPEN**, AND IT CANNOT REPORT THAT IT FAILED
// ============================================================================================
// `Stability()` returns **1.0 — "fully settled" — on EVERY failure path**, seven of them:
//   • RealFuels assembly not found (`rf == null`)         • `ModuleEnginesRF`/`UllageSet` type missing
//   • a reflected field/method missing (`ok == false`)     • the engine is not a `ModuleEnginesRF`
//   • `ullage == false` on that module                     • `ullageSet == null`
//   • anything thrown, anywhere (`catch { return 1.0; }`)
// and `Stable()` is `Stability() >= 0.996`, so it answers **true** both when it has MEASURED settled
// propellant and when it has measured NOTHING. There is no third answer and no way for a caller to ask
// which it got. In a stock build that is right, and the header below says so in as many words. **This
// vehicle flies RSS-RO**, where "I could not read RealFuels" and "the propellant is settled" are opposite
// facts with opposite consequences: one is a wait, the other is an ignition spent on a light that
// RealFuels will refuse — with `TestFlightFailure_IgnitionFail` on the same part (§B16.4).
//
// **PROPOSED FIX (not applied — an owner call, C1.12):** return a three-state answer
// (KNOWN-SETTLED / KNOWN-UNSETTLED / UNKNOWN) plus a "source live" flag, and let UNKNOWN gate CLOSED
// wherever the regime models ullage. `Init()` already computes exactly the fact needed — `ok` is true
// only when all three reflection handles resolved — it is simply not reported to anyone.
//
// ============================================================================================
// ⛔ AND THAT IS WHY THIS FILE IS NOT WIRED TO ANYTHING (W5's deliberate non-action)
// ============================================================================================
// `src/BoosterHost.cs` holds a `public static Func<Vessel, bool> UllageSettled` seam whose comment reads
// *"the ullage gate is CLOSED unless a real source says otherwise (register W5)"*. Assigning
// `Ullage.Stable` to it is a ONE-LINE change and W5 did **not** make it, on purpose: with the fail-open
// above, that one line converts "no source, so never burn" into "any reflection failure, so burn now" —
// putting a known-defective gate into the flight path inside the very task that restored it, which is
// what §B12.8 rider (b) forbids. **The seam stays null. The gate stays closed. Nothing burns.**
// Arming it is the PROPOSED FIX above plus an owner go — see W5's register line.
//
// ---- THE ORIGINAL HEADER, VERBATIM (C1.16 — reasoning is kept, never replaced) ---------------
// RealFuels models ullage: after a coast in free-fall the propellant floats off the engine intake and an
// ignition can fail (and there is NO retry budget — one ignition per engine mode). Before every relight the
// autopilot settles the propellant (fire the aft RCS) and only lights when the stability ≥ 0.996.
//
// We cannot reference the RealFuels assembly, so this reads the same fields MechJeb does (VesselState):
//   RealFuels.ModuleEnginesRF.ullage      (bool)   — is this engine ullage-modelled?
//   RealFuels.ModuleEnginesRF.ullageSet   (object) — the UllageSet
//   RealFuels.Ullage.UllageSet.GetUllageStability() (double 0..1) — propellant stability
// Without RealFuels (or on an engine it does not model) stability is 1.0 (always stable) — so the ascent
// logic degrades to "ignite immediately", which is correct for a stock build.
// ⚠ W5: that last sentence is the defect above, stated by the file itself. It is correct for stock and
//   wrong here, and the code cannot tell the two situations apart. Kept verbatim; not acted on.
// ⚠ W5: *"there is NO retry budget — one ignition per engine mode"* is now UNMEASURED, not fact — see
//   register [[BB8]] (the install's own cfg sets `%ignitions = -1`, unlimited; only a PRELAUNCH pad read
//   ever gave 1, and nobody has sampled it in flight). Kept verbatim; the claim is flagged, not edited.
// ============================================================================================
using System;
using System.Reflection;

namespace DragonScreen
{
    public static class Ullage
    {
        public const double StableThreshold = IgnitionGate.UllageStable;   // 0.996

        static bool inited, ok;
        static Type rfEngineType;
        static FieldInfo ullageField, ullageSetField;
        static MethodInfo getStability;

        static void Init()
        {
            if (inited) return;
            inited = true;
            try
            {
                Assembly rf = null;
                foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string n = a.GetName().Name;
                    if (n != null && n.IndexOf("RealFuels", StringComparison.OrdinalIgnoreCase) >= 0) { rf = a; break; }
                }
                if (rf == null) { UnityEngine.Debug.Log("[DragonScreen] Ullage: RealFuels not loaded — stability = stable"); return; }

                rfEngineType = rf.GetType("RealFuels.ModuleEnginesRF");
                Type ullageSetType = rf.GetType("RealFuels.Ullage.UllageSet");
                if (rfEngineType == null || ullageSetType == null) return;

                const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                ullageField = rfEngineType.GetField("ullage", F);
                ullageSetField = rfEngineType.GetField("ullageSet", F);
                getStability = ullageSetType.GetMethod("GetUllageStability", BindingFlags.Instance | BindingFlags.Public);

                ok = ullageField != null && ullageSetField != null && getStability != null;
                UnityEngine.Debug.Log("[DragonScreen] Ullage reflection " + (ok ? "READY (RealFuels)" : "INCOMPLETE — treating as stable"));
            }
            catch (Exception e) { ok = false; UnityEngine.Debug.LogWarning("[DragonScreen] Ullage init failed: " + e.Message); }
        }

        // Propellant stability [0..1] for the engine about to be lit. 1.0 = fully settled / not modelled.
        // ⛔ W5 DEFECT: every `return 1.0` below is a FAIL-OPEN — see the banner at the top of this file.
        //    Left exactly as it was; the fix is proposed there, not applied here.
        public static double Stability(ModuleEngines e)
        {
            Init();
            if (!ok || e == null || !rfEngineType.IsInstanceOfType(e)) return 1.0;
            try
            {
                bool ullage = (bool)ullageField.GetValue(e);
                if (!ullage) return 1.0;                       // engine not ullage-modelled → stable
                object us = ullageSetField.GetValue(e);
                if (us == null) return 1.0;
                object r = getStability.Invoke(us, null);
                return r is double ? (double)r : 1.0;
            }
            catch { return 1.0; }
        }

        public static bool Stable(ModuleEngines e) { return Stability(e) >= StableThreshold; }
    }
}
