/*
 * DragonScreen - PageSelection
 *
 * PURE. Encodes which page each screen is showing, as one short string.
 *
 * ---- WHY A STRING, AND WHY HERE ----
 * The selection has to SURVIVE. CLAUDE.md, "ONE SCREEN, FOUR SURFACES": internal models are torn
 * down and rebuilt whenever the camera changes, so a page held in an InternalModule dies with them,
 * and dies again on every save/load. It belongs on a PartModule on the pod - and a PartModule
 * persists a `[KSPField(isPersistant = true)]`, which wants a single primitive, not a dictionary.
 *
 * So: "1,0,2" means screen 1 shows page 1, screen 2 shows page 0, screen 3 shows page 2.
 *
 * The PARSING lives here rather than in the PartModule because parsing is exactly the kind of thing
 * that is fiddly, has edge cases nobody thinks about (empty, truncated, garbage from a hand-edited
 * save, a screen count that changed between versions) and is invisible until a save is corrupt.
 * That makes it src/pure work, headless tested, per the project's structural rule.
 *
 * ---- IT MUST NEVER THROW ON A BAD SAVE ----
 * This reads data a user can edit and that an older version of the mod may have written. Every
 * malformed input resolves to "use the default", never to an exception. A save that will not load
 * because a screen remembered the wrong page would be an absurd way to lose a flight.
 */
using System;
using System.Text;

namespace DragonScreen
{
    public static class PageSelection
    {
        /// <summary>How many screens the encoding covers. Three displays, ids 1..3.</summary>
        public const int Screens = 3;

        /// <summary>
        /// Page index for <paramref name="screenIndex"/> (1-based), or <paramref name="fallback"/>
        /// if the string does not say.
        ///
        /// Returns the fallback for: null, empty, too few fields, a non-numeric field, a negative
        /// index, or an index past the end of the page list. All of those are things a real save can
        /// contain and none of them is worth an exception.
        /// </summary>
        public static int Get(string encoded, int screenIndex, int pageCount, int fallback)
        {
            if (screenIndex < 1 || screenIndex > Screens) return fallback;
            if (string.IsNullOrEmpty(encoded)) return fallback;

            string[] parts = encoded.Split(',');
            int i = screenIndex - 1;
            if (i >= parts.Length) return fallback;

            int v;
            if (!int.TryParse(parts[i].Trim(), out v)) return fallback;
            if (v < 0 || v >= pageCount) return fallback;
            return v;
        }

        /// <summary>What an untouched screen encodes as. Any unparseable field means the same thing.</summary>
        public const string Unset = "-";

        /// <summary>
        /// Return <paramref name="encoded"/> with one screen's page replaced.
        ///
        /// Rebuilds the whole string rather than patching in place, because a partially written
        /// encoding is a corrupt one - and this runs on a touch, not per frame, so the allocation is
        /// not in any hot path.
        ///
        /// ---- UNTOUCHED SCREENS STAY UNTOUCHED, AND THE FIRST VERSION GOT THIS WRONG ----
        /// It filled every other screen with `Get(..., 0)` - defaulting a MISSING value to page 0
        /// while writing it out as though it had been chosen. Caught in KSP.log on 2026-08-06: one
        /// touch on screen 3 wrote `0,0,0`, so screens 1 and 2, which were showing their cfg defaults
        /// of VEHICLE and FLIGHT, were both silently pinned to FLIGHT for the life of that save.
        ///
        /// The header already said empty means "never touched" and that this is a real state rather
        /// than a missing value. That was true of the whole string and false of each field in it.
        /// Now a field can be unset too, so "the crew chose FLIGHT" and "nobody has said" are
        /// distinguishable - which is the same distinction the rest of this project keeps between a
        /// reading of zero and no reading at all.
        ///
        /// Reading is unchanged and needs no version check: Get already returns the fallback for any
        /// field it cannot parse, so an old `0,0,0` save still reads as three deliberate zeroes -
        /// which is exactly what it will have meant to whoever saved it.
        /// </summary>
        public static string Set(string encoded, int screenIndex, int page, int pageCount)
        {
            if (screenIndex < 1 || screenIndex > Screens) return encoded;
            if (page < 0 || page >= pageCount) return encoded;

            StringBuilder sb = new StringBuilder();
            for (int s = 1; s <= Screens; s++)
            {
                if (s > 1) sb.Append(',');
                if (s == screenIndex) { sb.Append(page); continue; }

                // -1 as the fallback is how "this field said nothing" is told apart from "this field
                // said zero". Anything malformed is rewritten as Unset rather than propagated.
                int v = Get(encoded, s, pageCount, -1);
                if (v < 0) sb.Append(Unset); else sb.Append(v);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Page index for a cfg name like "FLIGHT", or -1 if it is not one.
        /// Case-insensitive, because a cfg is hand-written and "Flight" is not a mistake worth
        /// punishing with a silently wrong default.
        /// </summary>
        public static int IndexOfName(string[] names, string name)
        {
            if (names == null || string.IsNullOrEmpty(name)) return -1;
            string want = name.Trim();
            for (int i = 0; i < names.Length; i++)
                if (string.Equals(names[i], want, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }
}
