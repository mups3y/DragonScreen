// DragonScreen — BlackBox / COVERAGE  (register BB1; the S76 ghost-column defect, stated as a check)
// ============================================================================================
// PURE. ⭐ THE DEFECT THIS FILE EXISTS FOR, quoted from BB1's register line:
//
//   "(1) `torque_cmd` was declared in the schema and never written in any file — a column that exists
//    and is always empty fakes coverage; BB1 must fail loudly ... if a declared column is never
//    populated across a mission. (2) `mode_holding` / `mode_flying` carried no state — the same defect,
//    twice more; every declared column needs a real writer or it does not go in the schema."
//
// The failure is not that the cells were blank. It is that NOTHING IN THE FILE SAID SO. A reader
// looking for the commanded torque found the column, found it empty, and had no way to tell "the loop
// commanded nothing" from "nobody ever wrote this". Three columns of the last flown corpus were in that
// state and it took a separate audit (S76) to discover it — after the flights.
//
// ---- HOW THIS ANSWERS IT, AND WHY IT IS NOT JUST "WARN ON ANY EMPTY COLUMN" ----
// A blanket empty-column warning would fire on every `Fit.Unfitted` guidance column on every flight
// until Part B is finished, which is a warning that gets ignored, which is no warning at all. §2.5
// wants those columns present and blank — that IS how a real recorder reports an unfitted system.
//
// So the check is against the column's DECLARED fitness (`BlackBoxSchema.Col.Fit`):
//   • Live        — never written across the whole mission  ⇒  DEFECT. `rec.column_never_written`.
//   • Conditional — never written  ⇒  reported as a NOTE with the declared condition, not a defect.
//                   ("no target was ever selected" is a fact about the flight, not about the recorder.)
//   • Unfitted    — never written  ⇒  silent. That is its declared state, and the manifest says so.
// And symmetrically, the one case nobody thought to check for:
//   • Unfitted    — WRITTEN  ⇒  DEFECT, the other way round. A column declared to have no source
//                   produced values, so either a writer was added without updating the declaration or
//                   the value came from somewhere unaccounted for. Either way the manifest is now lying
//                   about provenance, which is the one thing the manifest exists to prevent.
//
// ---- COST ----
// One `bool[]` of schema width, one pass over the row at write time comparing each cell to "". At 10 Hz
// over ~150 columns that is ~1500 reference compares per second against a frame of KSP part-tree work.
// It is inside §4.7's < 0.3 ms row budget and is measured by `rec_build_us` like everything else.
// ============================================================================================
using System.Collections.Generic;

namespace DragonScreen.BlackBox
{
    /// <summary>One finding from the close-out coverage pass.</summary>
    public struct CoverageFinding
    {
        public string Column;
        /// <summary>
        /// "never_written" (a Live column produced nothing), "unexpected_writer" (an Unfitted one did),
        /// or BB2's "capsule_leak" (a `Scope.Capsule` column carried a value on a stream whose vessel
        /// never held the camera — the capsule's state filed under another vessel).
        /// </summary>
        public string Kind;
        /// <summary>True for the two DEFECT cases; false for the Conditional note.</summary>
        public bool Defect;
        /// <summary>The declared condition or the register line, verbatim from the schema.</summary>
        public string Declared;
    }

    /// <summary>
    /// Tracks, per column, whether ANY row of this stream ever carried a non-blank cell there.
    /// One instance per stream (a two-vessel mission has two — BB2), because a column can legitimately
    /// be live on one vessel and absent on the other.
    /// </summary>
    public sealed class BlackBoxCoverage
    {
        readonly bool[] written;

        public BlackBoxCoverage() { written = new bool[BlackBoxSchema.Width]; }

        /// <summary>Rows counted, so a finding can say "never, across N rows" rather than just "never".</summary>
        public int Rows { get; private set; }

        /// <summary>Call once per row, AFTER every filler has run and AFTER the warp void (§4.6).</summary>
        public void Note(string[] cells)
        {
            Rows++;
            int n = cells.Length < written.Length ? cells.Length : written.Length;
            for (int i = 0; i < n; i++)
                if (!written[i] && !string.IsNullOrEmpty(cells[i])) written[i] = true;
        }

        public bool WasWritten(int col)
        {
            return col >= 0 && col < written.Length && written[col];
        }

        /// <summary>
        /// The close-out pass for a stream that held the camera for at least one row. Kept so a
        /// single-vessel mission reads exactly as it did under BB1.
        /// </summary>
        public List<CoverageFinding> Findings() { return Findings(true); }

        /// <summary>
        /// The close-out pass. Empty list = every Live column produced values and no Unfitted one did.
        /// A stream with no rows at all returns nothing: "the recorder never ran" is a different fault
        /// and is already reported by `rows_written = 0` in the manifest.
        ///
        /// ⭐ BB2's `everFocused`. A tracked UNFOCUSED stream (the §B16 booster) correctly writes NO
        /// `Scope.Capsule` column — those are the Dragon's singletons, and copying them onto the
        /// booster's rows is the fabrication the scope declaration exists to prevent. Reporting each of
        /// those ~27 blanks as a ghost-column DEFECT would fire on every two-vessel flight, and a
        /// defect that always fires is a defect nobody reads — the exact failure mode this file's
        /// header rejects for the blanket empty-column warning. So on a stream that never held the
        /// camera they are NOTES carrying the reason, and the symmetric check takes over instead:
        /// a capsule value that DID reach a never-focused stream is a defect in its own right.
        /// </summary>
        public List<CoverageFinding> Findings(bool everFocused)
        {
            var found = new List<CoverageFinding>();
            if (Rows == 0) return found;

            Col[] cols = BlackBoxSchema.Columns;
            for (int i = 0; i < cols.Length; i++)
            {
                Col c = cols[i];
                bool capsuleOnOther = c.Scope == Scope.Capsule && !everFocused;

                if (!written[i])
                {
                    if (capsuleOnOther)
                        found.Add(Make(c.Name, "never_written", false,
                            "declared Scope.Capsule and this stream's vessel never held the camera, so "
                            + "the capsule singleton was withheld rather than copied (BB2): " + c.Source));
                    else if (c.Fit == Fit.Live)
                        found.Add(Make(c.Name, "never_written", true, "declared Live: " + c.Source));
                    else if (c.Fit == Fit.Conditional)
                        found.Add(Make(c.Name, "never_written", false, c.Note));
                    // Unfitted + never written is its declared state. Silence is correct.
                }
                else if (capsuleOnOther)
                {
                    // ⛔ THE LEAK. A stream whose vessel never held the camera carries a value in a
                    // column sourced from the capsule's singletons — so somebody's bus voltage, gate or
                    // mission phase is filed under the wrong vessel. That is §4.8's NEVER FABRICATE
                    // breached, and it is silent in the file: the cell looks like every other cell.
                    found.Add(Make(c.Name, "capsule_leak", true,
                        "declared Scope.Capsule but written on a stream that never held the camera — "
                        + "the capsule's state is filed under another vessel: " + c.Source));
                }
                else if (c.Fit == Fit.Unfitted)
                {
                    found.Add(Make(c.Name, "unexpected_writer", true,
                        "declared Unfitted, pending " + c.Note + " — the manifest's provenance is now wrong"));
                }
            }
            return found;
        }

        static CoverageFinding Make(string col, string kind, bool defect, string declared)
        {
            CoverageFinding f = new CoverageFinding();
            f.Column = col; f.Kind = kind; f.Defect = defect; f.Declared = declared;
            return f;
        }
    }
}
