// DragonScreen — AlertActivity  (PURE: what Frame 58's ALERT ACTIVITY panel lists)
// ============================================================================================
// [[S133]] / QC `H-05`, 2026-09-06. Frame 58 carries a titled panel — "ALERT ACTIVITY" — with **822 px
// of nothing under it**, on the busiest page in the build, while `Alarms` folds the whole vehicle every
// frame and the black box writes the result down. The third member of the family [[S130]] and [[S137]]
// belong to: three surfaces that each computed a live alarm channel and then discarded it.
//
// ---- ⭐ IT IS BUILT ENTIRELY OUT OF [[S137]]'s MACHINERY, AND THAT IS THE POINT ----
// `AlertList.Build` already turns one `AlertScope` into the rows that scope's severity is MADE of, with
// every row's value formatted from the same number its severity is banded on. This adds no banding, no
// thresholds and no new vocabulary: it walks the five scopes, keeps the rows that are actually saying
// something, and orders them worst-first. So a row here and the same row on the Vehicle subsystem page
// cannot disagree — they are the same function.
//
// ---- ⛔ WHY THE WHOLE VEHICLE IS THE RIGHT SCOPE HERE, WHEN IT WAS THE WRONG ONE THERE ----
// S137's central defect was a whole-vehicle list under a word that reports ONE subsystem: the Crew tab
// printed a green NOMINAL over an amber POWER row. **One panel, two answers.** The fix was to scope the
// list to the severity printed above it.
// ⭐ This panel has NO severity word above it. Its title is "ALERT ACTIVITY" — a question about the
// vehicle, not a verdict about a subsystem — so a whole-vehicle list is what the title asks for and
// there is no word for it to contradict. The distinction is the title, and it is worth stating because
// the two panels otherwise look like the same job.
//
// ---- ⚠ AND AN EMPTY PANEL IS NOT THE SAME AS A DEAD ONE ----
// `Count` is 0 both when the vehicle is quiet and when there is no feed at all, and those must not read
// alike: the caller asks `s.Valid` and says "NO DATA" or "NO ALERT ACTIVITY". Rule E4 — a dash is
// honest, a plausible "nominal" over a dead feed is not.
//
// PURE: no KSP/Unity. The draw path allocates nothing (the scratch below is reused, the same idiom
// `Pages.stepScratch` uses).
// ============================================================================================
namespace DragonScreen
{
    public static class AlertActivity
    {
        /// <summary>The five scopes, in the order a quiet-vehicle list would grow. ⭐ Named as an array
        /// rather than a switch so "all five" is checkable — a scope added to `AlertScope` and not to
        /// this list would silently never be reported, which is the failure this panel exists to end.</summary>
        static readonly AlertScope[] Scopes = {
            AlertScope.Fdir, AlertScope.LifeSupport, AlertScope.Power,
            AlertScope.Propellant, AlertScope.Thermal };

        /// <summary>The most rows the panel can ever be asked to hold: every scope at its own maximum.</summary>
        public const int Max = AlertList.Max * 5;

        /// <summary>Reused across frames — the draw path allocates nothing (see the header).</summary>
        static readonly AlertItem[] scratch = new AlertItem[AlertList.Max];

        /// <summary>
        /// Fill <paramref name="into"/> with every row of every scope that is at Caution or worse,
        /// worst first, and return how many were written.
        ///
        /// ⛔ NOMINAL ROWS ARE DROPPED, and that is what makes this an ALERT list rather than a dump of
        /// the vehicle. `AlertList` returns the components a severity is made of — including the quiet
        /// ones, because a subsystem page is explaining its own word. This panel is answering "what is
        /// wrong", so a green row in it would be noise competing with the thing the crew is looking for.
        ///
        /// ⛔ ORDERING IS WORST-FIRST AND STABLE, the same guarantee `AlertList` gives: rows of equal
        /// severity keep the order the scopes are walked in, so a crew glancing twice sees the same
        /// list in the same order. An alert list that reshuffles under a steady state is unreadable.
        /// </summary>
        public static int Build(PageState s, AlertItem[] into)
        {
            // ⚠ `!s.Valid` HERE IS DEFENCE IN DEPTH, NOT A LOAD-BEARING GUARD, and it is labelled so
            // nobody reads it as covered. `AlertList.Build` already returns 0 on an invalid feed, so
            // deleting this clause changes no behaviour and a mutation of it survives the suite. It
            // stays because this function's own contract is "nothing without a vessel" and that should
            // not depend on a callee keeping its promise - but the honest word for it is redundant.
            if (into == null || !s.Valid) return 0;
            int n = 0;

            // Alarm first, then Caution — two passes rather than a sort, which keeps the ordering
            // stable within each band for free and allocates nothing.
            n = Collect(s, into, n, Severity.Alarm);
            n = Collect(s, into, n, Severity.Caution);
            return n;
        }

        static int Collect(PageState s, AlertItem[] into, int n, Severity want)
        {
            for (int i = 0; i < Scopes.Length; i++)
            {
                int m = AlertList.Build(s, Scopes[i], scratch);
                for (int j = 0; j < m && n < into.Length; j++)
                    if (scratch[j].Sev == want) into[n++] = scratch[j];
            }
            return n;
        }

        /// <summary>What the panel says when it has no rows. ⚠ TWO DIFFERENT SENTENCES: a quiet vehicle
        /// and a dead feed are not the same fact, and a panel that said "no alert activity" over a feed
        /// it could not read would be the exact lie rule E4 exists to prevent.</summary>
        public static string EmptyText(PageState s)
        {
            return s.Valid ? "NO ALERT ACTIVITY" : Dashes.None;
        }
    }
}
