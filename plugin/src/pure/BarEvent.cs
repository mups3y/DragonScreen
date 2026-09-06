// DragonScreen — BarEvent  (PURE: the bottom bar's centre-cell event announcement)
// ============================================================================================
// The bar's centre cell — between its two vertical rules — is EMPTY in `component_48.png`, and that is
// not a design choice. Profiling the asset column by column while [[S176]] cut it into elements found
// NOTHING between x 1464-1465 and x 1943-1944: a 475-design-px span, 13.9% of the whole bar, carrying no
// ink at all. It was landed as ground and logged as unexplained.
//
// ---- WHAT BELONGS THERE (owner, 2026-09-06) ----
// The owner supplied an image of the bar's centre section from ANOTHER BUILD, verbatim: *"You will
// notice the pop up box "trunk jettison and de-orbit enabled" This dialog box appears during events
// throughout the flight"* — and then, before anything was built, *"the example I showed you is from
// someone elses build it was just an example of what ours should do"*.
//
// ⛔ THAT SECOND SENTENCE IS WHY NONE OF THAT BUILD'S WORDING APPEARS HERE. §1.4's ladder is
// verified-real → other users' → invent only by owner discussion. Another builder's screen is TIER 2 —
// the same tier as the community Figma export this UI is reconstructed from — so it is good evidence
// that the ELEMENT EXISTS and what it is for, and it is NOT a source for the copy. The export baked the
// cell empty because no event was active in the frame that was exported.
//
// ---- WHERE THE COPY DOES COME FROM (owner, 2026-09-06: "look up what space x use during their
// ---- streams. Like MECO etc") ----
// The SpaceX/NASA stream callouts, TRANSCRIBED, and already in this repository — which is why C7 puts
// external URLs off-limits as a build source: "research complete, captured in docs/". Every string in
// `Label` below carries its source in the table beside it. A term census across `docs/` before writing
// any of them: MECO 141 mentions, SECO 92, Max-Q 22, Boostback 13, trunk jettison 21. The vocabulary
// was already here; this file only formats it.
//
// ---- ⭐ AND THE CALLOUTS FIT WHERE THE GATE TITLES DO NOT, WHICH IS THE WHOLE REASON THIS WORKS ----
// [[S179]] first concluded the cell was too narrow, measured against `CrewGates`' titles — but those are
// SENTENCES ("HOLD — WP0 (400 m below) — GO TO ENTER KOS", 42 chars). Stream callouts are terse by
// design. Measured against the same cell: of the 40 below, 20 fit on ONE line at the glanceable floor
// and 20 need two. NONE overflows.
//
// ⛔ SUPERSEDED IN PLACE 2026-09-07 (S179 job 2) — THE SENTENCE ABOVE IS KEPT AND ITS NUMBERS ARE WRONG
// TWICE OVER. C1.16 / G12. (1) Its "20 and 20" was computed against a budget of 427 design px — the
// BOX's inner width — because `bar_event_callouts.py` modelled `Inset` and never modelled `TextPad`.
// The code has always fitted against `Usable`, which subtracts the padding too. At the glanceable floor
// the true split was 16 on one line and 24 on two. The tool now models both and agrees with the code.
// (2) The glanceable floor is no longer this box's size at all: the owner's D1 puts it at
// `Typography.BarDesign` = 29 (see the `size` block in Draw).
//
// ⭐ AT 29, AGAINST THE REAL 379-PX BUDGET, ALL 40 FIT ON ONE LINE AND NONE OVERFLOWS. The widest is
// `DRAGON ON INT POWER` at 365.8 by the estimator — 13.2 px of headroom — and 293.7 as D-DIN actually
// renders it. The two-line path below is therefore DEAD CODE for today's catalogue, and is kept
// deliberately: the enum is append-only and the 41st callout may need it.
//
// ⭐ PROVEN TWICE, THE SECOND TIME AGAINST THE FONT ITSELF, because the first way can hide a defect.
// The character budget uses `MarginAffordance.CapAdvance` (0.6638 em), an all-caps AVERAGE — and an
// average can hide one wide word, which matters because A WRAP CAN NEVER BREAK A WORD. Re-measured with
// D-DIN's own advance widths out of `assets/d-din/D-DIN.ttf`, at the floor size of 48.07 design px
// against 427 usable: the widest single WORD in the entire set is `SPLASHDOWN` at 292.9 px. Not one word
// is too wide for a line, so the greedy wrap below always succeeds.
// ⚠ 2026-09-07: those two figures are the OLD size and the OLD (padding-blind) budget — see the block
// above. The conclusion survives the correction and gets stronger, which is why it is only annotated:
// at 29 against 379, `SPLASHDOWN` is 176.6 px, and no single word comes close to a line's width. `plugin/tools/bar_event_callouts.py`
// re-derives that rather than asking anyone to trust it, and `BarEventTest` pins it headlessly.
//
// ---- ⛔ WHAT THIS FILE DELIBERATELY CANNOT DO: INVENT AN EVENT ----
// It draws whatever `PageState.Event` says and nothing else. It owns NO trigger, NO timer and NO clock.
// That is not fastidiousness — the copy problem is solved and the DETECTION problem is not, and they
// split three ways:
//
//   crew gates (GO FOR …, the waypoints)        LIVE — CrewProcedureOps ticks every physics frame
//   flight-phase transitions (ENTRY INTERFACE…) LIVE — MissionPhase's classifier
//   MECO / SECO-1 / MAX-Q / STAGE SEPARATION    ⛔ NO DETECTOR EXISTS ANYWHERE IN THE TREE
//   the named rendezvous burns                  ⛔ PART B (§B12.5); the conductor does not fly yet
//
// So the callouts whose signal is live are raised, and the rest simply never fire until their detector
// lands. §14.4(f) read exactly: include the feature, fill it from a live source, and where there is no
// source there is no event. ⛔ A `MECO` fired off a stopwatch would be the single worst thing this bar
// could do — a crew reading a real callout that nothing measured — and it is what this note exists to
// prevent. Do not "temporarily" wire one to MET.
// ============================================================================================
namespace DragonScreen
{
    /// <summary>
    /// The stream callouts the bar can announce. ⛔ APPEND ONLY, and never renumber: the glue maps live
    /// signals onto these, and a saved or logged value must keep meaning what it meant.
    /// </summary>
    public enum BarCallout : byte
    {
        None = 0,

        // ---- Countdown — docs/CREW_MISSION_TELEMETRY.md §5, the NASA timeline PDF's own gate rows ----
        GoForPropLoad,      // §5 G4  "SpaceX Launch Director verifies GO for propellant load"
        LesArmed,           // §5 G5  "Dragon launch escape system is ARMED"
        DragonInternal,     // §5 G6  "Dragon transitions to internal power"
        GoForLaunch,        // §5 G7  "SpaceX Launch Director verifies GO for launch"

        // ---- Ascent — §5's ascent callout MET table ----
        Liftoff,            // §5 MET 0:00:00
        MaxQ,               // §5 MET 0:00:58
        Meco,               // §5 MET 0:02:37
        StageSeparation,    // §5 MET 0:02:40
        Ses1,               // §5 MET 0:02:48  (2nd-stage engine start)
        Seco1,              // §5 MET 0:08:50  (orbit insertion)
        DragonSeparation,   // §5 MET 0:12:03
        NoseconeOpen,       // §5 MET 0:12:48

        // ---- Booster — §5's first-stage rows + the booster research docs ----
        BoostbackBurn,      // booster docs (13 mentions)
        EntryBurn,          // §5 MET 0:07:29  (1st-stage entry burn)
        LandingBurn,        // §5 MET 0:08:59  (1st-stage landing burn)

        // ---- Rendezvous — §6a, the real named-burn schedule off the Crew-1 rendezvous PDF ----
        PhaseBurn,          // §6a +0/00:45:42
        BoostBurn,          // §6a +0/15:53:39
        CloseBurn,          // §6a +0/16:35:25
        TransferBurn,       // §6a +0/22:18:05
        CoellipticBurn,     // §6a +0/23:04:33
        GoForAiBurn,        // §6a +1/01:37:42  G9
        AiBurn,             // §6a +1/02:02:42  (7.5 km, 90 s, 0.72 m/s)
        MidcourseBurn,      // §6a +1/02:27:42

        // ---- Prox-ops / docking — §6a's waypoint holds and the two capture events ----
        Waypoint0,          // §6a +1/02:47:42  G10  (400 m below)
        Waypoint1,          // §6a +1/03:11:42  G11  (~220 m on the V-bar)
        Waypoint2,          // §6a +1/03:22:42  G12  (20 m)
        GoForDocking,       // §6a +1/03:23:42  G12
        SoftCapture,        // §6a +1/03:32:42  contact / capture
        DockingComplete,    // §6a +1/03:45:42  hard capture / hooks

        // ---- Return — §6b, the NASA blog return sequence (DM-2 / Crew-1 / Crew-2) ----
        GoForUndock,        // §6b G14
        DepartureBurn0,     // §6b  "Departure burns 0,1,2,3"
        PhasingBurn,        // §6b  "Departure phasing burn (~6 min)"
        TrunkJettison,      // §6b
        GoForDeorbitBurn,   // §6b G15
        DeorbitBurn,        // §6b  "Deorbit burn start", trunk + ~5 min
        NoseconeClosed,     // docs/EXTRACT_RETURN_CONTROL.md §8 (closed on burn completion)

        // ---- Entry — docs/PHASE_6_DEORBIT_ENTRY_SPLASHDOWN_RESEARCH.md §2 and §4 ----
        EntryInterface,     // §2  ~120 km
        DroguesDeployed,    // §4  ~5.5 km / 156 m/s
        MainsDeployed,      // §4  ~1.8 km / 53 m/s
        Splashdown          // §4  ~5-8 m/s
    }

    public static class BarEvent
    {
        /// <summary>
        /// What each callout READS, in the stream's own words. ⛔ INDEXED BY THE ENUM — the order here
        /// and the order there are one thing, and `BarEventTest` proves the two are the same length so a
        /// value added to one and not the other cannot ship.
        ///
        /// ⚠ THESE ARE TRANSCRIBED, NOT COMPOSED. Each maps to a row of the tables cited against its
        /// enum member above. Where the stream says a thing in more words than the cell holds, the
        /// SHORT FORM the stream itself uses is taken (the callout is "MECO", not "main engine cutoff")
        /// — that is the whole reason the owner pointed at the stream vocabulary rather than at the
        /// gate titles, which are sentences and do not fit (see the header).
        /// </summary>
        static readonly string[] Label =
        {
            "",                     // None
            "GO FOR PROP LOAD",  "LES ARMED",         "DRAGON ON INT POWER", "GO FOR LAUNCH",
            "LIFTOFF",           "MAX-Q",             "MECO",                "STAGE SEPARATION",
            "SES-1",             "SECO-1",            "DRAGON SEPARATION",   "NOSECONE OPEN",
            "BOOSTBACK BURN",    "ENTRY BURN",        "LANDING BURN",
            "PHASE BURN",        "BOOST BURN",        "CLOSE BURN",          "TRANSFER BURN",
            "COELLIPTIC BURN",   "GO FOR AI BURN",    "AI BURN",             "MIDCOURSE BURN",
            "WAYPOINT 0 - 400 M","WAYPOINT 1 - 220 M","WAYPOINT 2 - 20 M",   "GO FOR DOCKING",
            "SOFT CAPTURE",      "DOCKING COMPLETE",
            "GO FOR UNDOCK",     "DEPARTURE BURN 0",  "PHASING BURN",        "TRUNK JETTISON",
            "GO FOR DEORBIT BURN","DEORBIT BURN",     "NOSECONE CLOSED",
            "ENTRY INTERFACE",   "DROGUES DEPLOYED",  "MAINS DEPLOYED",      "SPLASHDOWN"
        };

        /// <summary>How many callouts there are, `None` excluded — the owner's "all 40".</summary>
        public const int Count = 40;

        /// <summary>What a callout reads, or "" for <see cref="BarCallout.None"/> and anything out of
        /// range. ⛔ Never returns a plausible substitute: an unknown value draws nothing.</summary>
        public static string Text(BarCallout c)
        {
            int i = (int)c;
            return (i > 0 && i < Label.Length) ? Label[i] : "";
        }

        // ---- THE BOX, IN THE BAR'S OWN MEASURED GEOMETRY ------------------------------------------
        // ⛔ NOT TRACED OFF THE OWNER'S IMAGE. CLAUDE.md: "Build pages from the reference's own source,
        // never a screenshot ... Screenshot/SVG-derived pages came out wrong every time." Every number
        // below is either measured off `component_48.png` or borrowed from a number that was.

        /// <summary>The cell's own edges: the INNER faces of the bar's two vertical rules, which are
        /// measured at x 1464-1465 and 1943-1944.</summary>
        public const float CellL = 1466f, CellR = 1941f;

        /// <summary>The box's inset inside that cell. BORROWED, not chosen: the bar's own text-to-rule
        /// margin, which is 23 design px on the CURRENT STATE side (caption ends 1441, rule at 1464) and
        /// 26 on the POINTING MODE side (rule ends 1944, caption starts 1969). 24 is the pair's own
        /// middle and gives the 427 usable px the fit above is computed against.</summary>
        public const float Inset = 24f;

        /// <summary>The box's top and bottom, in design y. BORROWED again: the vertical rules' own
        /// extent — measured at asset rows 121..218, i.e. design y 1998..2095 — so the box occupies
        /// exactly the band the bar already treats as its content area.</summary>
        public const float BoxTop = 1998f, BoxBottom = 2095f;

        /// <summary>Corner radius. ⚠ OURS (§1.4 tier 3, no source): the reference image shows a rounded
        /// box but is another build's and is not measured from. A sixth of the box's height keeps the
        /// curve proportional at any panel size and reads as the same family as the frame's own rounded
        /// corners without claiming to be them.</summary>
        const float RadiusOfHeight = 1f / 6f;

        /// <summary>Gap between the two wrapped lines, as a fraction of the type size. Caps have no
        /// descenders, so a tight pitch is correct here; 1.05 puts two lines of floor-size type inside
        /// the 97-px band with room at both ends (measured: 83.7 of 97 used).</summary>
        const float LinePitch = 1.05f;

        /// <summary>Padding between the box's border and its text.
        ///
        /// ⚠ ADDED AFTER MEASURING THE FIRST RENDER: with no padding the wrap budget WAS the box's
        /// inner width, so the worst-case line would have touched the border on both sides. Nothing
        /// overflowed — the estimator runs conservative — but "did not overflow" is not "deliberate".
        ///
        /// ⛔ AND 24 WAS TRIED FIRST AND IS WRONG, WHICH IS WHY THE NUMBER IS 16. Borrowing
        /// <see cref="Inset"/>'s 24 leaves a 379-px budget, and `DEORBIT BURN` measures 382.9 by the
        /// estimator — 3.9 px over. `BarEventTest` failed on exactly that string, which is the check
        /// doing its job on the day it was written rather than a year later. 16 is the largest value
        /// at which every one of the 40 still wraps inside the box with real margin: the worst line is
        /// `LANDING BURN` at 382.9 against a 395 budget, **12.1 design px of headroom**.
        /// ⚠ Measured against the ESTIMATOR, deliberately — that is what the wrap decides with, so it
        /// is what the budget must be sized against. The real font is kinder still (`LANDING BURN`
        /// renders 319.2), and relying on that difference would be trusting a number the code never
        /// consults.
        ///
        /// ⛔ SUPERSEDED IN PLACE 2026-09-07 (S179 job 2) — THE PARAGRAPH ABOVE IS KEPT AND ITS
        /// CONCLUSION IS REVERSED. Every figure in it was measured at the GLANCEABLE floor of 48.07
        /// design px. The owner's D1 moved this text to `Typography.BarDesign` = 29, and at 29 the
        /// constraint that forced 16 simply evaporates — `DEORBIT BURN` is 12 chars × 29 × 0.6638 =
        /// **230.9** by the same estimator, against a 379 budget. It was never 3.9 px over at this
        /// size; it was 3.9 px over at the size that has since been rejected.
        ///
        /// ⭐ SO 24 IS RESTORED, AND IT IS BORROWED RATHER THAN CHOSEN — it is <see cref="Inset"/>, the
        /// gap between the cell's rules and the box, used again between the box and its text, so the
        /// box's inner and outer breathing space match. 16 was a number picked to survive an arithmetic
        /// squeeze that no longer exists, and keeping it would leave the padding looking arbitrary for
        /// a reason nothing in the file could still explain.
        ///
        /// ⚠ THE WORST CASE AT 29 IS NOW `DRAGON ON INT POWER`: 19 chars → **365.8** by the estimator
        /// against the 379 budget, **13.2 design px of headroom**, and it fits on ONE line. Still
        /// measured against the ESTIMATOR for the reason the superseded block gives, which is the one
        /// thing in it that has not changed.</summary>
        public const float TextPad = Inset;

        /// <summary>The box's own width in DESIGN px.</summary>
        public static float BoxWidth { get { return (CellR - CellL) - 2f * Inset; } }

        /// <summary>The usable TEXT width in DESIGN px — what the wrap fits against. **379** at the
        /// measured cell (427 box − 2 × 24 padding). At <see cref="Typography.BarDesign"/> the widest
        /// of the 40 is `DRAGON ON INT POWER` at **365.8** by the estimator (293.7 as D-DIN actually
        /// renders it), so the worst case clears by **13.2** — and clears it on ONE line.
        /// ⚠ The 395 / `LANDING BURN` 382.9 figures this used to quote were measured at the glanceable
        /// floor and against `TextPad` = 16; both are superseded, see <see cref="TextPad"/>.</summary>
        public static float Usable { get { return BoxWidth - 2f * TextPad; } }

        /// <summary>Split a callout into at most two lines that each fit <see cref="Usable"/> at
        /// `size` design px. Greedy: fill the first line, the rest goes on the second.
        ///
        /// ⛔ A WRAP CAN NEVER BREAK A WORD, which is why the header measures the widest WORD and not
        /// just the widest string. If a single word were wider than the line there would be no correct
        /// answer here — it is proven that none is, and `BarEventTest` re-proves it for every callout at
        /// both shipped widths rather than leaving it as a claim in a comment.</summary>
        public static void Wrap(string text, float size, out string line1, out string line2)
        {
            line1 = text; line2 = null;
            if (string.IsNullOrEmpty(text)) { line1 = ""; return; }
            if (Width(text, size) <= Usable) return;

            // ⛔ BOTH LINES ARE VALIDATED, AND THE FIRST VERSION OF THIS ONLY CHECKED THE FIRST.
            // Greedily filling line 1 and dropping the remainder on line 2 puts NO bound on line 2:
            // `GO FOR DEORBIT BURN` filled line 1 with "GO FOR" and left "DEORBIT BURN" — 382.9 px
            // against a 379 budget — and the draw would have run ink out of the box. `BarEventTest`
            // caught it. Take the split that makes line 1 as long as possible SUBJECT TO BOTH LINES
            // FITTING; if no split satisfies both, fall back to the most balanced one so the failure
            // is as small as it can be and is visible rather than lopsided.
            //
            // ⛔ CORRECTED IN PLACE 2026-09-07 (S179 job 3) — THE PARAGRAPH ABOVE IS KEPT AND ITS
            // CAUSAL CLAIM IS FALSE. C1.16 / G12: a wrong statement is marked, never deleted, because
            // the correction is only checkable against what it replaced. The overflow it describes was
            // REAL; the fix it credits was not the thing that fixed it.
            //
            // ⭐ WHY THE TWO FORMS CANNOT DIFFER, WHICH IS THE PART THE ORIGINAL MISSED. As the split
            // index i moves right, line 1 grows and line 2 SHRINKS — wa is increasing in i, wb is
            // decreasing. Naive greedy takes the LAST i with wa <= Usable, and among all splits with an
            // acceptable line 1 that is precisely the one with the SMALLEST line 2. So if any split
            // satisfies both bounds, the naive choice already does. Validating cannot beat it; it can
            // only agree with it or fall back. ⛔ Measured, not just argued: over all 40 callouts, at
            // the old 48.07 AND at today's 29, and at padding 16 AND 24, the two forms produce
            // BYTE-IDENTICAL output — 120 comparisons, 0 differences. Mutation `R1` reverts this to
            // naive greedy and SURVIVES; it is an EQUIVALENT MUTANT and is reported as one. No check
            // is invented to kill it, because a check that could would be testing the implementation
            // rather than the contract.
            //
            // ⭐ WHAT ACTUALLY CAUSED THE OVERFLOW WAS THE PADDING. At 48.07 with TextPad = 24 the
            // budget was 379 and `GO FOR DEORBIT BURN` split to a 382.9-px line 2 — under BOTH forms.
            // Dropping TextPad to 16 (budget 395) is what cleared it. That squeeze is now gone
            // entirely: at Typography.BarDesign = 29 every one of the 40 fits on ONE line and TextPad
            // is back to 24. See TextPad's own superseded block.
            //
            // ⚠ THE VALIDATING FORM IS KEPT ANYWAY, AND NOT OUT OF SENTIMENT. It is never worse, and
            // its balanced fallback is genuinely better in the case the argument above leaves open —
            // when NO split satisfies both bounds, naive returns a lopsided pair while this returns the
            // least-bad one. That case does not arise on today's 40; the catalogue is append-only, so
            // it is the 41st that this is here for.
            int split = -1, balanced = -1;
            float bestMax = float.MaxValue;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != ' ') continue;
                float wa = Width(text.Substring(0, i), size);
                float wb = Width(text.Substring(i + 1), size);
                if (wa <= Usable && wb <= Usable) split = i;          // valid; later is longer line 1
                float worse = (wa > wb) ? wa : wb;
                if (worse < bestMax) { bestMax = worse; balanced = i; }
            }
            if (split <= 0) split = balanced;
            // No space at all: leave it unsplit rather than cutting a word in half. The draw then
            // overflows visibly, which is the honest failure — and the test says it cannot happen.
            if (split <= 0) return;
            line1 = text.Substring(0, split);
            line2 = text.Substring(split + 1);
        }

        /// <summary>A string's width in design px at `size`, through the project's ONE measured
        /// advance — `MarginAffordance.CapAdvance`, 0.6638 em, taken off a render. ⛔ Not a second
        /// copy of that number: these callouts are all caps, which is exactly what it was measured on.</summary>
        public static float Width(string s, float size)
        {
            return (s == null) ? 0f : s.Length * MarginAffordance.CapAdvance * size;
        }

        /// <summary>
        /// Draw the announcement in the bar's centre cell.
        ///
        /// <paramref name="mapX"/> is the page's own design-x → panel-x map (the bar's `BarFit`), and
        /// <paramref name="k"/> the UNIFORM draw scale — the same separation the rest of the bar keeps,
        /// so the box lands between the rules on every fit while its type never stretches.
        /// </summary>
        public static void Draw(DisplayList dl, BarCallout c, int w, int h, BarFit fit)
        {
            if (dl == null || c == BarCallout.None) return;
            string text = Text(c);
            if (text.Length == 0) return;

            float k = BottomBar.Scale(h);
            if (k <= 0f) return;

            float x0 = BottomBar.MapX(CellL + Inset, w, h, fit);
            float x1 = BottomBar.MapX(CellR - Inset, w, h, fit);
            float boxW = x1 - x0;
            if (boxW <= 0f) return;
            float y0 = BoxTop * k, boxH = (BoxBottom - BoxTop) * k;

            RoundRect(dl, x0, y0, boxW, boxH, boxH * RadiusOfHeight, DragonPalette.Hairline);

            // ⛔ THE GLANCEABLE FLOOR, not the size that would make any string fit. This is the nav bar
            // (Typography.Dense excludes it by name) and it is an event announcement — if it is worth
            // interrupting the crew with, it is worth being able to read at a glance.
            //
            // ⛔ SUPERSEDED IN PLACE 2026-09-07 (S179 job 2, owner `OVERRIDE`) — C1.16 / G12: the three
            // lines above are KEPT and their conclusion no longer holds. The owner was shown this very
            // box built at the glanceable floor and rejected it in those words — "the text is way to
            // big and looks out of place" — then picked 29 design px off a rendered 20/29/36/48 ladder
            // (his D1; REGISTER.md S179). [[S176]] edit 3 made that a NAMED floor, `Typography.BarDesign`,
            // which the bar's own CURRENT STATE value already draws at (his D2).
            //
            // ⭐ SO THE BOX AND THE BAR'S VALUE ARE NOW THE SAME SIZE, WHICH IS THE POINT — the cell is
            // part of the bar, not a panel floating in it. ⚠ And the reasoning above is not simply
            // wrong: the bar IS excluded from Typography.Dense by name, and it IS an announcement. What
            // changed is that the bar now has a floor of its own to be read against, and it is still a
            // floor — the census ratchets this draw at 29 rather than exempting it.
            float size = Typography.BarDesign;
            string l1, l2;
            Wrap(text, size, out l1, out l2);

            float cx = x0 + boxW * 0.5f, cy = y0 + boxH * 0.5f;
            float pitch = size * LinePitch * k;
            float ts = size * k;
            if (l2 == null)
            {
                dl.Text(l1, cx, cy - ts * Typography.CapCentreOfTop, ts, TextAlign.Centre,
                        DragonPalette.White);
            }
            else
            {
                dl.Text(l1, cx, cy - pitch * 0.5f - ts * Typography.CapCentreOfTop, ts,
                        TextAlign.Centre, DragonPalette.White);
                dl.Text(l2, cx, cy + pitch * 0.5f - ts * Typography.CapCentreOfTop, ts,
                        TextAlign.Centre, DragonPalette.White);
            }
        }

        /// <summary>A filled rounded rectangle. There is no such primitive, so it is four corner discs
        /// and a cross of rects — the same construction `CoverPage.Pill` uses for its fully-round ends,
        /// generalised to a radius smaller than half the height.</summary>
        static void RoundRect(DisplayList dl, float x, float y, float w, float h, float r, Rgba c)
        {
            if (r > w * 0.5f) r = w * 0.5f;
            if (r > h * 0.5f) r = h * 0.5f;
            dl.Rect(x + r, y, w - 2f * r, h, c);          // the middle column, full height
            dl.Rect(x, y + r, r, h - 2f * r, c);          // the left edge, between the corners
            dl.Rect(x + w - r, y + r, r, h - 2f * r, c);  // the right edge
            dl.ArcBand(x + r, y + r, 0f, r, 270.0, 360.0, c);
            dl.ArcBand(x + w - r, y + r, 0f, r, 0.0, 90.0, c);
            dl.ArcBand(x + w - r, y + h - r, 0f, r, 90.0, 180.0, c);
            dl.ArcBand(x + r, y + h - r, 0f, r, 180.0, 270.0, c);
        }


        /// <summary>
        /// ⭐ MOVED OUT OF `VesselData` 2026-09-07 (S179), AND THE MOVE IS THE POINT. This is a total
        /// function from one PURE enum to another — `GateId` lives in `pure/CrewGates.cs` — so it was
        /// pure logic sitting in the KSP glue, where the project's load-bearing rule says it must not
        /// be and where no headless test could reach it. Mutation `R7` (a gate mapped to the WRONG
        /// callout) SURVIVED the whole suite for exactly that reason. It is pinned now.
        /// ⛔ The glue still owns the TRIGGER — reading `CrewProcedureOps` every physics frame. Only
        /// the mapping moved.
        ///
        /// Which stream callout a live crew gate raises in the bottom bar, or `None`.
        ///
        /// ⛔ A MAP, NOT A REWORDING. `CrewGates`' titles are transcribed NASA/SpaceX callouts and its
        /// header forbids editing them ("Do not reword one to make a test pass or a card fit"), so this
        /// does not shorten a title to fit the cell — it names the STREAM callout that corresponds to
        /// the same moment, from the same transcribed sources (docs/CREW_MISSION_TELEMETRY.md §5/§6a/§6b).
        /// ⚠ G1-G3 (ingress, suit leak, hatch close) have no entry: they are crew procedure and the
        /// stream does not call them. `None` is the honest answer, not a nearby callout.</summary>
        public static BarCallout ForGate(GateId g)
        {
            switch (g)
            {
                case GateId.PropLoadGoG4:      return BarCallout.GoForPropLoad;
                case GateId.LesArmG5:          return BarCallout.LesArmed;
                case GateId.InternalPowerG6:   return BarCallout.DragonInternal;
                case GateId.LaunchGoG7:        return BarCallout.GoForLaunch;
                case GateId.ApproachInitGoG9:  return BarCallout.GoForAiBurn;
                case GateId.WP0HoldG10:        return BarCallout.Waypoint0;
                case GateId.WP1HoldG11:        return BarCallout.Waypoint1;
                case GateId.WP2DockGoG12:      return BarCallout.GoForDocking;
                case GateId.DockingCompleteG13:return BarCallout.DockingComplete;
                case GateId.UndockGoG14:       return BarCallout.GoForUndock;
                case GateId.DeorbitGoG15:      return BarCallout.GoForDeorbitBurn;
                default:                       return BarCallout.None;
            }
        }

        /// <summary>Draw commands the box costs, worst case: 7 for the rounded rect + 2 lines.</summary>
        public const int Commands = 10;
    }
}
