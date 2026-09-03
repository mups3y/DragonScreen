// DragonScreen — OctawebBinding  (PURE: §B16.4's HARD ASSERTION on which vehicle's octaweb we may bind)
// ============================================================================================
// ⛔ NOT a restored file. Written by W2 (Wave B, 2026-09-04) to land §B16.4's hard assertion, which the
// W2 register line requires "as a guarded test against docs/reference/craftdump.csv". Everything it
// decides is decidable from PART NAMES alone, so it is pure and headless-tested (test/ActuationTest.cs)
// against the real dump rather than discovered on a live vessel.
//
// WHY IT EXISTS. The owner installed Kartoffelkuchen "Launchers Pack" on 2026-09-03. It ships its OWN
// Falcon 9 — parts prefixed KK_SPX_ / KK_F9demo_, including its own octaweb KK_SPX_F9_Octaweb. Our
// booster binding therefore MUST (§B16.4):
//   • assert that EXACTLY ONE octaweb is found, and that it is the Tundra one (TE.19.F9.S1.Engine);
//   • reject any part whose name contains "KK_SPX" or "KK_F9demo";
//   • REFUSE AND ANNUNCIATE on either failure rather than picking one — a booster controller that binds
//     the wrong vehicle's engines is a lost booster with no error message.
// §B16.4 records that no Kartoffelkuchen part name contains ".S1." today, so VehicleParts.IsBooster
// still discriminates; this assertion exists so it cannot silently bind the wrong vehicle IF THAT EVER
// CHANGES — a mod update, or a craft that mixes the two packs.
//
// ⚠ WHAT THIS DOES *NOT* CLAIM. Nothing calls it yet. Wave B is the actuation layer; the phase-boundary
// resolution that §B12.7/§B16.4 describe (find the booster part, bind its three ModuleEnginesRF BY
// engineID into a named table, resolve ONCE) belongs to the booster controller in Wave C. This is the
// guard that binder MUST call before it binds — it is not itself a binder, and it guards nothing live.
//
// ⛔ NAMES ONLY, NEVER AN INDEX. The identity is the part NAME string. Never a persistent_id, never a
// stage index, never a position, never an engine-part COUNT (§B12.7, §B16.4, VehicleParts.cs:37).
// ============================================================================================
using System;

namespace DragonScreen
{
    // The verdict of the octaweb binding assertion. Anything but Ok means REFUSE + ANNUNCIATE.
    public enum OctawebBind : byte
    {
        Ok,             // exactly one Tundra octaweb, no foreign booster part on the vessel
        NotFound,       // no octaweb at all — nothing to bind
        Ambiguous,      // more than one Tundra octaweb — do NOT pick one
        ForeignVehicle  // a KK_SPX / KK_F9demo part is present — the wrong Falcon 9 is on this vessel
    }

    public static class OctawebBinding
    {
        // The one real octaweb, verified against docs/reference/craftdump.csv (§B16.4): part name
        // "TE.19.F9.S1.Engine", title "Falcon 9/Heavy Full Thrust Octoweb", three ModuleEnginesRF with
        // engineID AllEngines / ThreeLanding / CenterOnly. §1.4 verified-real — do not edit without a dump.
        public const string TundraOctawebPart = "TE.19.F9.S1.Engine";

        // The foreign-vehicle markers, named verbatim by §B16.4. Substring match, case-insensitive.
        public const string ForeignKkSpx = "KK_SPX";
        public const string ForeignKkF9Demo = "KK_F9demo";

        // The three octaweb engineID strings — THE binding keys (§B16.4 step 2). Listed here so the test can
        // assert the dump still exposes exactly these three and that Actuation.EngineRoleOf splits them.
        public const string EngineIdAll = "AllEngines";
        public const string EngineIdThreeLanding = "ThreeLanding";
        public const string EngineIdCenterOnly = "CenterOnly";

        static bool Has(string partName, string marker)
        {
            return partName != null
                && partName.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Is this part from the OTHER Falcon 9 (Kartoffelkuchen Launchers Pack)?
        public static bool IsForeignBoosterPart(string partName)
        {
            return Has(partName, ForeignKkSpx) || Has(partName, ForeignKkF9Demo);
        }

        // Is this part OUR octaweb? Identity is the whole name, not a marker — two different octawebs is
        // exactly the failure this guards, so a loose substring test would defeat the point.
        public static bool IsTundraOctaweb(string partName)
        {
            return partName != null
                && string.Equals(partName.Trim(), TundraOctawebPart, StringComparison.OrdinalIgnoreCase);
        }

        // THE ASSERTION. Feed it every part name on the vessel (glue: v.parts[i].name). On Ok, `bound` is
        // the octaweb's part name; on any failure `bound` is null and the caller must refuse + annunciate.
        //
        // Order matters and is deliberate: a foreign part anywhere on the vessel loses, EVEN IF our octaweb
        // is also present. §B16.4 says "refuse and annunciate on either failure rather than picking one",
        // and a vessel carrying both packs' boosters is precisely the case where picking is the wrong move.
        public static OctawebBind Bind(string[] vesselPartNames, out string bound)
        {
            bound = null;
            if (vesselPartNames == null) return OctawebBind.NotFound;

            for (int i = 0; i < vesselPartNames.Length; i++)
                if (IsForeignBoosterPart(vesselPartNames[i])) return OctawebBind.ForeignVehicle;

            int found = 0;
            string first = null;
            for (int i = 0; i < vesselPartNames.Length; i++)
            {
                if (!IsTundraOctaweb(vesselPartNames[i])) continue;
                found++;
                if (first == null) first = vesselPartNames[i];
            }
            if (found == 0) return OctawebBind.NotFound;
            if (found > 1) return OctawebBind.Ambiguous;

            bound = first;
            return OctawebBind.Ok;
        }

        // The annunciation text for a refusal — one line, screen/log-ready, says WHICH failure it was.
        // (Ok has no annunciation: a successful bind is silent.)
        public static string Annunciation(OctawebBind r)
        {
            switch (r)
            {
                case OctawebBind.NotFound: return "OCTAWEB NOT FOUND — NO BOOSTER ENGINES TO BIND";
                case OctawebBind.Ambiguous: return "OCTAWEB AMBIGUOUS — MORE THAN ONE FOUND, REFUSING TO PICK";
                case OctawebBind.ForeignVehicle: return "FOREIGN BOOSTER PART — WRONG FALCON 9, BINDING REFUSED";
                default: return null;
            }
        }
    }
}
