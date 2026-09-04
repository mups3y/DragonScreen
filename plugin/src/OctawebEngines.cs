// DragonScreen — OctawebEngines  (KSP glue: the octaweb's NAMED TABLE, resolved ONCE, held)
// ============================================================================================
// ⛔ NOT a restored file. Written by W3 (Wave C, 2026-09-04) as the glue half of the octaweb binder the
// W3 register line requires. The DECISION is pure (`pure/OctawebResolve.cs`, headless-tested against
// `docs/reference/craftdump.csv`); this file only enumerates the live parts, hands the pure resolver the
// (part name, engineID) pairs, and keeps the three `ModuleEngines` references it names.
//
// ---- WHY A HELD TABLE AND NOT A SEARCH ----
// §B12.7 / §B16.4 step 2: bind the three `ModuleEnginesRF` BY THEIR engineID STRING into a named table,
// **resolved ONCE at the phase boundary, never re-searched per frame**. `Actuator`'s engine helpers
// (ActivateEngines / ShutdownEngines / FindEngine) walk every part and every module on every call — which
// is right for a one-shot mission event and wrong for a booster descent that commands an engine set every
// tick. This is the once-per-phase form of the same lookup.
//
// ⛔ THE GUARD RUNS FIRST, ALWAYS. `Resolve` cannot produce a bound table without `OctawebBinding.Bind`
// returning Ok (the pure resolver enforces that, not this file): exactly one Tundra octaweb
// `TE.19.F9.S1.Engine`, and no Kartoffelkuchen `KK_SPX` / `KK_F9demo` part anywhere on the vessel. On any
// refusal `Ok` is false, every reference is null, and `Annunciation` says which failure it was — a
// booster controller that binds the wrong vehicle's engines is a lost booster with no error message
// (§B16.4).
//
// ---- ⛔ WHAT THIS FILE MUST NEVER DO ----
// • NEVER ACTUATE. No Activate, no Shutdown, no throttle, no gimbal. It binds and hands back; direct part
//   control stays in `src/Actuator.cs` ([[direct-part-control-hard-rule]]), which is still the only place
//   that touches parts to make them do something. This one only READS the part list to resolve names.
// • NEVER CYCLE ENGINE MODES. `ModuleTundraEngineSwitch` (NextEngineMode*/PreviousEngineMode*/Toggle*,
//   and writing `selectedIndex`) and `ModuleEngineConfigs`-as-a-switch are FORBIDDEN by §B16.3/§B16.4:
//   RO mode-cycling causes engine RE-IGNITIONS and lag, and RO ignitions are a finite per-engine
//   resource. Selecting a mode = ACTIVATING the bound module for that role while it is off, before
//   ignition. Its read-only fields may be READ for annunciation, and that is the whole of the licence.
// • NEVER BY POSITION, COUNT OR persistent_id. Ids change between craft revisions and break silently on
//   the next VAB edit (§B16.4 step 3). `VehicleParts.OctawebEngineCount = 9` is NOZZLES, not parts.
//
// ---- ⛔ THE NAME SOURCE IS `partInfo.name`, NEVER `Part.name` (OCT1, 2026-09-05) ----
// This file used to read `v.parts[i].name`. `CraftDump` and `GeometryDump` read
// `p.partInfo != null ? p.partInfo.name : p.name`, and the pure layer is tested against the dump those
// two write — so the live glue was feeding the resolver a DIFFERENT string from the one every test
// asserts. On 2026-09-05 that string was not equal to `TE.19.F9.S1.Engine` (it carried extra characters;
// `OctawebBinding.IsTundraOctaweb` already `Trim()`s, so it was not surrounding whitespace), while
// `VehicleParts.IsBooster`'s `.S1.` SUBSTRING test survived the extras and passed. Result: the booster
// host bound the vessel, the octaweb refused 264 times, and every booster engine command was silently
// dropped for the whole descent. The two live classifiers must read the SAME expression as the dumps,
// which is what `PartName(Part)` below is for — change it here and in `BoosterHost.Describe` together
// or the two disagree again.
//
// ⚠ NOTHING CALLS THIS YET, and W3 does not pretend otherwise. The caller is a booster controller at a
// phase boundary; §B16.1's booster core is written FRESH and is not this wave, and the gen-2
// `BoosterControl.cs` that used to fill that role is RECOVER-REFERENCE and STAYS DELETED (CLAUDE.md,
// R1 §5.2). Every flight command on every screen is still §14.4(a)'s honest no-op. This is the binder
// that core MUST go through — built now so the guard W2 landed finally has a caller, and so the
// resolve-once discipline is in place before anything is flying against it.
// ============================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public sealed class OctawebEngines
    {
        // The pure verdict this table was built from — kept whole so a refusal can be reported exactly.
        public OctawebTable Table { get; private set; }

        // The three bound modes. Null unless Ok. (`ModuleEnginesRF` derives from stock `ModuleEngines`,
        // which is the API §B16.3 directs us to use — Activate/Shutdown and the independent-throttle
        // fields — so the reference is held at the stock type and no RO assembly is referenced here.)
        public ModuleEngines All { get; private set; }
        public ModuleEngines Three { get; private set; }
        public ModuleEngines Centre { get; private set; }

        // The vessel the table was resolved against, so a stale table can be detected rather than flown.
        readonly Vessel vessel;

        OctawebEngines(Vessel v) { vessel = v; }

        public bool Ok { get { return Table.Ok; } }
        public string OctawebPart { get { return Table.OctawebPart; } }

        // One screen/log-ready line on a refusal; null on a successful bind (a good bind is silent).
        public string Annunciation { get { return OctawebResolve.Annunciation(Table); } }

        // RESOLVE ONCE, at a phase boundary. Returns a table whose Ok says whether it may be used; it
        // never returns null and never throws into a control tick.
        public static OctawebEngines Resolve(Vessel v)
        {
            OctawebEngines o = new OctawebEngines(v);
            if (v == null || v.parts == null)
            {
                o.Table = OctawebResolve.Build(null, null);
                return o;
            }

            var partNames = new List<string>();
            var refs = new List<OctawebEngineRef>();
            var mods = new List<ModuleEngines>();
            string driftFed = null, driftRaw = null;   // first part whose two name sources disagree

            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p == null) continue;
                string nm = PartName(p);
                string raw = p.name ?? "";
                if (driftFed == null && !string.Equals(raw, nm, StringComparison.Ordinal))
                { driftFed = nm; driftRaw = raw; }
                partNames.Add(nm);
                if (p.Modules == null) continue;
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e == null) continue;
                    // ORDER MATTERS ONLY IN THAT IT IS THE SAME ORDER: the pure resolver returns indices
                    // into this list, so the two must be appended in lockstep.
                    refs.Add(new OctawebEngineRef { PartName = nm, EngineId = e.engineID });
                    mods.Add(e);
                }
            }

            OctawebTable t = OctawebResolve.Build(partNames.ToArray(), refs.ToArray());
            o.Table = t;

            // ---- OCT1 MEASUREMENT (1): say the drift out loud even when the bind now SUCCEEDS ----
            // The 2026-09-05 failure was invisible because nothing ever printed the string it refused.
            // With the name source corrected the refusal stops happening — so the evidence would be lost
            // with it. This line survives the fix: whenever `Part.name` and `partInfo.name` differ on any
            // part, it prints BOTH verbatim, once (LogGate / S40), and that is how the extra characters
            // finally get measured rather than assumed.
            if (driftFed != null && LogGate.First("octaweb-name-drift:" + driftRaw))
                Debug.LogWarning("[DragonScreen] part-name SOURCE DRIFT on \"" + v.vesselName + "\": "
                                 + "partInfo.name=" + Show(driftFed) + "  Part.name=" + Show(driftRaw)
                                 + " — the binder is fed partInfo.name (OCT1); Part.name is NOT the contract.");

            if (!t.Ok)
            {
                // ---- OCT1 MEASUREMENT (2): a NotFound that names the string it refused ----
                // "No octaweb" while a part on this very vessel classifies as a booster is the exact
                // 2026-09-05 contradiction: two functions read one vessel and disagreed. Print every
                // offending booster part name VERBATIM — escaped, with its length — so an invisible
                // character cannot hide in a log line the way it did for a whole descent.
                if (t.Guard == OctawebBind.NotFound)
                {
                    string offenders = BoosterNames(v);
                    if (offenders != null && LogGate.First("octaweb-notfound-names:" + offenders))
                        Debug.LogWarning("[DragonScreen] octaweb NOT FOUND, yet this vessel carries a booster "
                                         + "part — the two classifiers disagree. Offending name(s): " + offenders
                                         + "  (expected exactly " + Show(OctawebBinding.TundraOctawebPart) + ")");
                }

                // Once per distinct refusal (S40 / pure/LogGate.cs). `Resolve` is called from a bind retry
                // that runs twice a second, and on 2026-09-05 this line was written 264 times unthrottled.
                if (LogGate.First("octaweb-refused:" + t.Plan + "/" + t.Guard))
                    Debug.Log("[DragonScreen] octaweb bind REFUSED — " + (o.Annunciation ?? t.Plan.ToString()));
                return o;
            }

            o.All = mods[t.AllIndex];
            o.Three = mods[t.ThreeIndex];
            o.Centre = mods[t.CentreIndex];
            Debug.Log("[DragonScreen] octaweb bound on " + t.OctawebPart + " — "
                      + OctawebBinding.EngineIdAll + " / " + OctawebBinding.EngineIdThreeLanding
                      + " / " + OctawebBinding.EngineIdCenterOnly);
            return o;
        }

        // ---- THE ONE NAME SOURCE (OCT1) ----
        // Exactly the expression `CraftDump.DumpPart` and `GeometryDump` use, because the pure layer is
        // tested against what those two write. `Part.name` is a live Unity object name and is NOT the
        // contract; `partInfo.name` is the part's identity from the part database.
        static string PartName(Part p)
        {
            return (p.partInfo != null ? p.partInfo.name : p.name) ?? "";
        }

        // Render a name so an INVISIBLE difference is visible in KSP.log: quoted, every character outside
        // printable ASCII written as \uXXXX, and the length stated. OCT1's whole difficulty was that two
        // strings differed by characters nobody could see in a log line.
        static string Show(string s)
        {
            if (s == null) return "<null>";
            var sb = new System.Text.StringBuilder(s.Length + 16);
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= ' ' && c <= '~') sb.Append(c);
                else sb.Append("\\u").Append(((int)c).ToString("X4"));
            }
            sb.Append('"').Append(" [len=").Append(s.Length).Append(']');
            return sb.ToString();
        }

        // Every part on this vessel that `VehicleParts.IsBooster` accepts - by EITHER name source - rendered
        // verbatim. Null when there is none, which is the ordinary "Dragon only, post-MECO" case and is not
        // worth a line. This is the evidence OCT1 was missing: the names that made NotFound and IsBooster
        // contradict each other.
        static string BoosterNames(Vessel v)
        {
            System.Text.StringBuilder sb = null;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p == null) continue;
                string nm = PartName(p);
                string raw = p.name ?? "";
                if (!VehicleParts.IsBooster(nm) && !VehicleParts.IsBooster(raw)) continue;
                if (sb == null) sb = new System.Text.StringBuilder();
                else sb.Append("; ");
                sb.Append("partInfo.name=").Append(Show(nm)).Append(" Part.name=").Append(Show(raw));
            }
            return sb == null ? null : sb.ToString();
        }

        // The bound module for an octaweb role, or null. This is the ONLY way a controller should reach a
        // booster engine — going back to a per-frame part walk defeats the point of resolving once.
        public ModuleEngines For(EngineRole role)
        {
            if (!Ok) return null;
            if (role == EngineRole.OctawebAll) return All;
            if (role == EngineRole.OctawebThree) return Three;
            if (role == EngineRole.OctawebCentre) return Centre;
            return null;
        }

        // Is the held table still the right one? A staged-away booster, a vessel switch or a destroyed
        // part invalidates it, and the answer is to RE-RESOLVE at the next phase boundary — never to
        // fall back to a live search mid-descent.
        public bool StillValid(Vessel v)
        {
            if (!Ok || v == null || !ReferenceEquals(v, vessel)) return false;
            return All != null && Three != null && Centre != null
                && All.part != null && Three.part != null && Centre.part != null
                && All.vessel == v && Three.vessel == v && Centre.vessel == v;
        }
    }
}
