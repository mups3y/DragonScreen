// DragonScreen — OctawebResolve  (PURE: §B16.4 step 2 — WHICH engineID plays WHICH role, decided once)
// ============================================================================================
// ⛔ NOT a restored file. Written by W3 (Wave C, 2026-09-04) to discharge the W3 register line's second
// requirement: *"the octaweb binder is this wave's job… Wave C's booster controller MUST call
// OctawebBinding.Bind(…) and refuse + annunciate on anything but Ok BEFORE it binds the three
// ModuleEnginesRF by engineID into its named table (§B12.7: resolve ONCE at the phase boundary, never
// per frame; NEVER by position, count or persistent_id)."*
//
// W2 built the GUARD (`pure/OctawebBinding.cs`: is this the right vehicle's octaweb at all?) and stated
// plainly that nothing called it. This file is the BINDER that calls it — the decision half. It answers
// one question and nothing else:
//
//     given every part name on the vessel, and every (part, engineID) pair carrying an engine module on
//     it, is there exactly one complete octaweb role table — and if so, WHICH ENTRY plays AllEngines,
//     WHICH plays ThreeLanding, WHICH plays CenterOnly?
//
// It returns INDICES into the caller's own array, never objects, which is what keeps it pure and
// headless-testable against `docs/reference/craftdump.csv` (test/OctawebResolveTest.cs) instead of
// discovered on a live vessel. The glue that holds the actual `ModuleEnginesRF` references is
// `src/OctawebEngines.cs`; it calls this, once.
//
// ---- ⛔ THE FOUR THINGS THIS REFUSES, AND WHY EACH IS A REFUSAL AND NOT A GUESS ----
// (1) THE GUARD FAILED. `OctawebBinding.Bind` returned NotFound / Ambiguous / ForeignVehicle — no
//     octaweb, two octawebs, or a Kartoffelkuchen `KK_SPX` / `KK_F9demo` part on the vessel (§B16.4).
//     A booster controller that binds the wrong vehicle's engines is a lost booster with no error
//     message, so the whole resolve stops here and annunciates the guard's own line.
// (2) A MODE IS MISSING. Fewer than three roles resolved. The craft dump says the octaweb carries all
//     three (`AllEngines` / `ThreeLanding` / `CenterOnly`); a vessel where one is absent is not a
//     vehicle this guidance knows, and a 9-3-1 schedule with a hole in it is not flyable.
// (3) A MODE IS DUPLICATED. Two engine modules claiming the same role. Picking one is exactly the move
//     §B16.4 forbids for the octaweb itself, for the same reason.
// (4) AN OCTAWEB-ROLE ENGINE SITS ON A PART THAT IS NOT THE BOUND OCTAWEB. `Actuation.EngineRoleOf`
//     classifies by `VehicleParts.IsBooster` (the ".S1." marker) + engineID, so ANY booster part could
//     in principle offer an octaweb role. The table must come off the ONE part the guard bound, or the
//     identity check the guard performed has been quietly discarded downstream.
//
// ⛔ NEVER BY POSITION, COUNT OR persistent_id (§B12.7, §B16.4 step 3, VehicleParts.cs:37). The identity
// is the engineID STRING, resolved through `Actuation.EngineRoleOf`, and the octaweb's identity is its
// whole part NAME. `OctawebEngineCount = 9` is NOZZLES; it is never an expected part or module count —
// the real craft is ONE part with THREE modules, and a test asserts that 3 != 9 on the real dump.
//
// ⛔ AND NEVER `ModuleTundraEngineSwitch` / `ModuleEngineConfigs` AS A SWITCHING MECHANISM (§B16.3,
// §B16.4). Selecting a mode means ACTIVATING the bound `ModuleEnginesRF` for that role while it is off,
// before ignition. Mode-cycling causes the RE-IGNITIONS and lag the owner directed us away from, and RO
// ignitions are a finite per-engine resource.
//
// ⚠ WHAT THIS DOES *NOT* DO. It does not actuate: no Activate, no Shutdown, no throttle. It does not
// hold a reference to anything live. And — stated so the next reader is not misled by a green suite —
// **nothing calls it yet in a flight path.** `src/OctawebEngines.cs` is the glue that binds through it;
// the CONTROLLER that would call that at a phase boundary is §B16.1's own booster core, which is
// written FRESH and is not this wave (`BoosterControl.cs` is RECOVER-REFERENCE and stays deleted).
// ============================================================================================
using System;

namespace DragonScreen
{
    // One engine module as the resolver sees it: the part it sits on, and its engineID. That pair is
    // ALL the identity §B16.4 permits — no index, no id, no position.
    public struct OctawebEngineRef
    {
        public string PartName;
        public string EngineId;
    }

    // Why a resolve ended. Anything but Ok means REFUSE + ANNUNCIATE; the table is unusable.
    public enum OctawebPlan : byte
    {
        Ok,             // exactly one complete role table off the one bound octaweb
        GuardRefused,   // OctawebBinding.Bind said anything but Ok — see the Guard field
        ModeMissing,    // fewer than three octaweb roles resolved
        ModeDuplicate,  // two engine modules claim the same octaweb role
        ForeignPart     // an octaweb-role engine sits on a part that is not the bound octaweb
    }

    // The resolved table. Indices point into the array handed to Build; -1 means unresolved. Struct, so
    // a caller cannot end up holding a null table by accident.
    public struct OctawebTable
    {
        public OctawebPlan Plan;
        public OctawebBind Guard;      // the guard's own verdict, kept so the refusal can say which one
        public string OctawebPart;     // the bound part name (null unless Plan == Ok)
        public int AllIndex, ThreeIndex, CentreIndex;

        public bool Ok { get { return Plan == OctawebPlan.Ok; } }

        // The bound index for a role, or -1. OctawebAll / OctawebThree / OctawebCentre only — every other
        // EngineRole (SecondStage, PodAbort, None) is not the octaweb's and is not in this table.
        public int IndexFor(EngineRole role)
        {
            if (!Ok) return -1;
            if (role == EngineRole.OctawebAll) return AllIndex;
            if (role == EngineRole.OctawebThree) return ThreeIndex;
            if (role == EngineRole.OctawebCentre) return CentreIndex;
            return -1;
        }
    }

    public static class OctawebResolve
    {
        // THE BINDER. Call ONCE at a phase boundary (§B12.7) and hold the result — never per frame.
        //
        // `vesselPartNames` is every part on the vessel (glue: v.parts[i].name); `engines` is every
        // engine module on it as a (part name, engineID) pair, IN THE CALLER'S OWN ORDER, so the
        // returned indices address the caller's array directly.
        public static OctawebTable Build(string[] vesselPartNames, OctawebEngineRef[] engines)
        {
            OctawebTable t = new OctawebTable();
            t.AllIndex = t.ThreeIndex = t.CentreIndex = -1;

            // (1) THE GUARD FIRST — always, and before a single engineID is looked at. §B16.4.
            string bound;
            t.Guard = OctawebBinding.Bind(vesselPartNames, out bound);
            if (t.Guard != OctawebBind.Ok) { t.Plan = OctawebPlan.GuardRefused; return t; }

            if (engines == null) { t.Plan = OctawebPlan.ModeMissing; return t; }

            for (int i = 0; i < engines.Length; i++)
            {
                string part = engines[i].PartName;
                EngineRole role = Actuation.EngineRoleOf(part, engines[i].EngineId);
                if (role != EngineRole.OctawebAll && role != EngineRole.OctawebThree
                    && role != EngineRole.OctawebCentre) continue;   // MVac / SuperDraco / not ours

                // (4) an octaweb ROLE must come off the octaweb PART the guard bound — not merely off
                // some booster part. Otherwise the guard's identity check is silently discarded here.
                if (!OctawebBinding.IsTundraOctaweb(part)) { t.Plan = OctawebPlan.ForeignPart; return Clear(t); }

                // (3) one module per role. Two claimants is a refusal, never a pick.
                if (role == EngineRole.OctawebAll)
                {
                    if (t.AllIndex >= 0) { t.Plan = OctawebPlan.ModeDuplicate; return Clear(t); }
                    t.AllIndex = i;
                }
                else if (role == EngineRole.OctawebThree)
                {
                    if (t.ThreeIndex >= 0) { t.Plan = OctawebPlan.ModeDuplicate; return Clear(t); }
                    t.ThreeIndex = i;
                }
                else
                {
                    if (t.CentreIndex >= 0) { t.Plan = OctawebPlan.ModeDuplicate; return Clear(t); }
                    t.CentreIndex = i;
                }
            }

            // (2) all three, or none of it. A 9-3-1 schedule with a hole is not flyable.
            if (t.AllIndex < 0 || t.ThreeIndex < 0 || t.CentreIndex < 0)
            { t.Plan = OctawebPlan.ModeMissing; return Clear(t); }

            t.Plan = OctawebPlan.Ok;
            t.OctawebPart = bound;
            return t;
        }

        // A refusal binds NOTHING — no half-table survives for a caller to read past the verdict.
        static OctawebTable Clear(OctawebTable t)
        {
            t.OctawebPart = null;
            t.AllIndex = t.ThreeIndex = t.CentreIndex = -1;
            return t;
        }

        // The one-line, screen/log-ready annunciation for a refusal. A successful bind is SILENT (null),
        // matching OctawebBinding.Annunciation. A guard refusal defers to the guard's own wording so the
        // operator sees WHICH of §B16.4's three failures happened, not a generic "binding failed".
        public static string Annunciation(OctawebTable t)
        {
            switch (t.Plan)
            {
                case OctawebPlan.GuardRefused: return OctawebBinding.Annunciation(t.Guard);
                case OctawebPlan.ModeMissing: return "OCTAWEB MODES INCOMPLETE — 9-3-1 SCHEDULE UNBINDABLE";
                case OctawebPlan.ModeDuplicate: return "OCTAWEB MODE DUPLICATED — REFUSING TO PICK";
                case OctawebPlan.ForeignPart: return "OCTAWEB MODE ON THE WRONG PART — BINDING REFUSED";
                default: return null;
            }
        }
    }
}
