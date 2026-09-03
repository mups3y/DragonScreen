/*
 * DragonScreen - LogGate
 *
 * PURE. "Has this diagnosis already been said?" - a seen-set of string keys, so a warning that
 * describes a STANDING condition is written once instead of once per frame.
 *
 * ---- WHY THIS EXISTS, AND WHY IT IS PURE ----
 * The 2026-09-03 orbit flight left ~450 copies of ONE line in KSP.log - ImageStore.BodyMap's
 * "no usable scaled-space map", emitted every frame for the whole flight because under RSS the
 * planet wears a custom Hapke shader whose texture slots are not the stock ones. Nothing about
 * that condition changed between frames, so 449 of those lines carried no information and buried
 * everything that did.
 *
 * The retry is NOT the bug and is not gated here: a scaled-space texture can genuinely appear
 * later (Kopernicus builds it late, the body changes on an SOI transition), so the lookup keeps
 * asking. Only the SPEECH is gated - and, because the caller tests the gate before composing the
 * message, so is the cost of building it.
 *
 * The key carries everything that would make the answer different - for the body map, the body
 * AND the shader it is wearing - so a new body, or the same body after a shader swap, is a new
 * diagnosis and is reported. Reset() exists for the headless tests; the game never calls it,
 * because a condition that is still true on the next scene load is still the same condition.
 *
 * It is pure so the RULE can be tested without a game: "same key once, different key again" is
 * decidable with the game closed, which is the whole point of the split. The glue keeps the
 * Debug.LogWarning call - the gate never knows what a log is.
 */
using System.Collections.Generic;

namespace DragonScreen
{
    public static class LogGate
    {
        private static readonly HashSet<string> said = new HashSet<string>();

        /// <summary>
        /// True the FIRST time this key is offered and false ever after, so a caller can write
        /// <c>if (LogGate.First(key)) Debug.LogWarning(...)</c> and know the line appears once.
        /// A null or empty key is never gated - it would collapse unrelated conditions into one -
        /// so it returns true every time and the caller behaves as it did before.
        /// </summary>
        public static bool First(string key)
        {
            if (string.IsNullOrEmpty(key)) return true;
            if (said.Contains(key)) return false;
            said.Add(key);
            return true;
        }

        /// <summary>Has this key already been said? Asks without claiming it.</summary>
        public static bool Said(string key)
        {
            return !string.IsNullOrEmpty(key) && said.Contains(key);
        }

        /// <summary>How many distinct diagnoses have been made. For the tests and for a dump.</summary>
        public static int Count { get { return said.Count; } }

        /// <summary>Forget everything. FOR THE TESTS - the game has no reason to call it.</summary>
        public static void Reset() { said.Clear(); }
    }
}
