// DragonScreen — SHELL PAGE  (register S243; overseer PROMPT 2)
// ============================================================================================
// PURE. The three proving pages of the rebuild: `ShellCover` and `ShellSuitCheck` on `BaseScreen`,
// `ShellVehicle` on `BaseScreenTabbed`. ONE file for all three, because they differ only in which
// base they sit on and what they are called — and a third copy of the same six calls is exactly the
// signal PROMPT 1 said to factor out rather than paste.
//
// ---- ⛔ THE CONTENT AREA IS EMPTY ON PURPOSE. TWICE OVER. ----
//  1. The owner adds real content one page at a time afterwards, and that sequencing IS the plan. No
//     placeholder charts, boxes or lorem — an empty window is the deliverable, not a stub.
//  2. ⛔ AND THE GAUGE IS DELIBERATELY ABSENT. Its design is approved (thin arc, dotted track, interior
//     stack, five mirrored bands) but its PLACEMENT has not been discussed and its thresholds are not
//     sourced. Owner, 2026-09-09, verbatim: *"we have not discussed placement of the gauges yet, just
//     the design — bob should not touch these yet."* ⭐ Written here so a later chat reads the empty
//     window as an instruction followed, not as an oversight.
//
// ---- ⭐ WHAT IS LIVE, AND WHAT "WIRED BUT EMPTY" MEANS ----
// The page name, the five nav icons and their sliding marker, the nine tabs and their sliding
// selector, and the event pop-up with its two bar captions are all placed by code every frame.
// ⛔ THE POP-UP DRAWING NOTHING IS THE CORRECT STATE, NOT A MISSING FEATURE. Owner: *"have them all
// ready to go and wired in so when the prompts arrive they are ready."* `BarEvent.Draw` returns on its
// first line for `BarCallout.None`, so with no mission event the cell is empty — and the moment a real
// callout arrives the same call renders it. `ShellPageTest.ThePopupIsWiredNotAbsent` drives a value in
// and watches it appear, because "it draws nothing" and "it is not connected" look identical from
// outside and only one of them is right.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class ShellPage
    {
        /// <summary>
        /// Where the page's own name sits, in `BaseScreen`'s 1920 x 1054 frame.
        /// ⚠ Inside the content window's top-left, one window-inset in from its corner — so it moves
        /// with the window rather than with the panel, and a change to `WinL`/`WinT` carries it along.
        /// </summary>
        public const float TitleX = BaseScreen.WinL + 24f;
        public const float TitleY = BaseScreen.WinT + 22f;
        public const float TitlePx = 26f;

        /// <summary>
        /// A NON-ICON shell: base, page name, bar, marker, event cell.
        /// ⛔ `BarFit.Stretch` because the body is drawn with `sx = w/RefW` — the same reasoning
        /// `CrewGatePage.cs:317` gives, and the fit MUST match the body's own map or the bar's rule
        /// stops continuing the page's column divider (`BottomBar`'s own `BarFit` docstring).
        /// </summary>
        public static void Build(DisplayList dl, int w, int h, PageState s, UiPage page)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            BaseScreen.Draw(dl, w, h);
            Title(dl, w, page);
            Chrome(dl, w, h, s, page);
        }

        /// <summary>The ICON shell — the same, on the tabbed base, with the nine-tab strip.</summary>
        public static void BuildTabbed(DisplayList dl, int w, int h, PageState s, int activeTab)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            BaseScreenTabbed.Draw(dl, w, h, activeTab);
            Title(dl, w, UiPage.ShellVehicle);
            Chrome(dl, w, h, s, UiPage.ShellVehicle);
        }

        /// <summary>
        /// The page's own name, LIVE from <see cref="FigmaUI.Name"/>.
        /// ⛔ Never a string literal per page and never a picture of one: §14.4's rule and the `S147b`
        /// trap. One source means the Menu card and the page heading cannot disagree about what a page
        /// is called.
        /// </summary>
        public static void Title(DisplayList dl, int w, UiPage page)
        {
            float sc = BaseScreen.Sc(w);
            dl.Text(FigmaUI.Name(page), TitleX * sc, TitleY * sc, TitlePx * sc,
                    TextAlign.Left, BasePalette.Stroke);
        }

        /// <summary>
        /// The bottom bar. ⭐ AND THAT IS ALL THIS HAS TO DO, WHICH IS THE POINT — the two things the
        /// prompt asks to be "wired" are wired ALREADY, by construction, and re-doing either here would
        /// draw it twice:
        ///   • THE MARKER is drawn centrally for every page by `FigmaUI.BottomBarMarker`, at the end of
        ///     `Build` (`FigmaUI.cs:302`). It computes x from the active index each frame and shares
        ///     `Rect()` with `BottomBar.Hit`, so draw and touch target move together or not at all
        ///     (`BottomBar.cs:68-77`). ⛔ Called there, never reimplemented here.
        ///   • THE POP-UP AND THE TWO CAPTIONS come with the bar: `BottomBar.Draw` ends in
        ///     `BarEvent.Draw(dl, s.Event, w, h, fit)` (`BottomBar.cs:542`), which returns on its first
        ///     line for `BarCallout.None`. ⛔ So "wired but drawing nothing" is not something this page
        ///     arranges — it is what the bar already does, and the empty cell IS the correct state.
        ///
        /// ⛔ `BottomBar.FitFor(page)` is the ONE table that answers which fit a page takes. The fit must
        /// match the body's own map or the bar's rule stops continuing the page's column divider — the
        /// reasoning `CrewGatePage.cs:317` gives for its own `BarFit.Stretch`.
        /// </summary>
        public static void Chrome(DisplayList dl, int w, int h, PageState s, UiPage page)
        {
            BottomBar.Draw(dl, w, h, s, BottomBar.FitFor(page));
        }
    }
}
