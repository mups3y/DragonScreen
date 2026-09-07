// DragonScreen — VrioTestPage  (PURE: "4.700 Deorbit Preparation — Test VRIO Health LEDs")
// ============================================================================================
// ⚠ S110 / QC F-01 — THIS HEADER USED TO SAY "NO Figma/demo reference", AND THAT WAS WRONG.
// `art/cover/frame59.png` is a Figma frame OF THIS EXACT SCREEN - same title, same DEORBIT checklist with
// the same 4-of-5 state, same steps 4.1-4.5, same three command buttons, same NEXT and ENTER READ-ONLY,
// same two note cards word for word. It was in the repo the whole time, and `UiPage.Procedure` was
// rendering it (S110 pointed page 3 here instead, so there is one screen and one renderer).
//
// ⛔ S111 — BUT THE FRAME DOES NOT OUTRANK THIS FILE, AND S110's FIRST VERSION OF THIS COMMENT SAID IT
// DID. That was backwards. §1.4 (owner, 2026-09-02, and it "governs EVERY element") ranks
// (1) VERIFIED-REAL first, (2) other users' recreations second - and it names the **community Figma** in
// tier 2 explicitly. §14.2 then lists the captured **VRIO** screen LAYOUT in TIER-1. So the photographic
// reconstruction this file is built from is the HIGHER source, and `frame59` is a tier-2 recreation of
// the same screen. Where they disagree, the default is that THIS FILE is right and the frame is the
// marked fallback - the opposite of what S110 wrote.
// ⚠ What is genuinely open is narrower and is QC **Q9**: for the specific elements where the
// photographs may not resolve alignment and type size, a tier-2 frame is a legitimate fill - and the
// frame is measurably more legible at the shipped width. Which elements those are is not recorded
// anywhere, so VT-02 is NOT actioned until the owner answers. Nothing here is corrected to the frame.
//
// ⛔⛔ SUPERSEDED IN PLACE 2026-09-07 BY S181 (UNIT 2a) - THE PARAGRAPH DIRECTLY ABOVE ONLY.
// C1.16 / G12 forbid deleting it, so it stands and this says what changed. Q9 WAS ANSWERED on
// 2026-09-06 (REGISTER.md S162, an option SELECTED via the overseer, not a verbatim quote):
//
//        "TAKE FRAME59'S ALIGNMENT AND LAYOUT, MARKED TIER-2."
//
// So "VT-02 is NOT actioned until the owner answers" and "Nothing here is corrected to the frame" are
// both spent: he answered, and this file IS now laid out to the frame. Everything else in the two
// paragraphs above - the source hierarchy, S111's correction of S110 - is UNCHANGED and still governs.
//
// ⛔ AND THE RULING'S OWN GUARDRAIL IS THE POINT OF IT. It applies where tier-1 evidence is ABSENT,
// "not where it is merely inconvenient". `REAL_SPACEX_SCREENSHOTS/` is not in this repository - S162
// checked four ways (`ls`, `.gitignore`, `git log --all --diff-filter=A`, `find -iname discovery*`) and
// S181 re-checked rather than assuming it had appeared. It has not. So LAYOUT here is a tier-2 fill and
// every block below says so at its own call site; unmarked, this silently becomes the §1.4 inversion
// S111 caught in S110, which is indistinguishable from a tier-1 claim to every later reader.
//
// ⭐ WHAT STAYS TIER-1, AND IT IS THE HALF THAT MATTERS MOST: THE CONTENT. Not one string below was
// changed. `docs/SCREEN_INVENTORY.md` #6 transcribes this procedure's steps, its section numbering and
// its row labels from the real screen, and that is a verified-real source which the frame does not
// outrank. The split is exactly the one §1.4 clause (2) describes - TIER-1 FOR *WHAT*, TIER-2 FOR
// *WHERE* - and it is why the ruling could be applied without touching a word of the procedure.
//
// A real Crew Dragon procedure screen, also corroborated by photographs of the actual capsule displays
// (REAL_SPACEX_SCREENSHOTS, the shanemielke.com walkthrough). It shares the
// Suit-Leak-Check procedure template: LEFT = the 4.700 deorbit checklist + read-only control; MAIN = the
// numbered command steps ("Test VRIO Health LEDs" — start/stop the two VRIO LED tests, verify, report);
// RIGHT = the engineering notes. VRIO = the vehicle's redundant I/O the automated chute backup rides on.
//
// FUNCTION: VRIO LEDs are zero-fault-tolerant health indicators for the flight-computer / automated-
// chute path; the crew runs each LED test, verifies the lamps on the command panel, and reports to
// SpaceX, so a malfunction is known before entry. Static for now; the command buttons wire to real
// state in the touch pass, like the Suit Leak Check.
//
// ============================================================================================
// ---- S181 (UNIT 2a, 2026-09-07): WHERE EVERY COORDINATE IN THIS FILE COMES FROM ----
//
// ⭐ THE GEOMETRY BELOW IS MEASURED, NOT CHOSEN, AND IT WAS MEASURED TWICE FROM TWO INDEPENDENT SOURCES.
// §14.2a (G13) clause (1): an element present in the Figma export is BUILT FROM that export, at its own
// coordinates, layered - and text the export renders as text is TYPED, not imported as pixels, because
// only typed text can go live and S182 (unit 2b) depends on that.
//
//   SOURCE A  `assets/figma/elements/procedure_vrio/` - 51 per-element PNGs, alpha-trimmed 1:1 crops of a
//             3427x2112 frame, which is exactly this page's RefW/RefH. Each was located in
//             `assets/figma/frames/Frame 59.png` by masked FFT correlation.
//   SOURCE B  `assets/figma/dashboard_ui/Frame 59.svg` - the same frame's VECTOR coordinates. It renders
//             all text as paths (so it settles no type size), but every rect, line, circle and plate in
//             it carries an exact number.
//
// They agree. `Rectangle 178` correlates to (22,26) at 827x1929 and `Rectangle 179` to (889,26) at
// 2516x1929; a raster scan of the frame puts its panel strokes at x 22..23 / 847..848 / 889..890 /
// 3403..3404; and the SVG's two panel paths run x 23..848 and x 890..3404 with a 2 px centred stroke.
// Three readings, one answer. Where a 2 px line is involved the SVG wins, because a 2 px stroke is
// degenerate to correlate and the SVG states it outright.
//
// ⭐ AND THIS SET IS FRAME 59, WHICH WAS NOT KNOWN BEFORE THIS UNIT. `docs/reference/
// FIGMA_ELEMENT_EXPORTS.md` guessed `procedure_vrio` was "probably Frame 66" and left it open as
// S175-Q1 for "the unit that first needs one of those sets". This is that unit. Frame 66 is CABIN
// SETTINGS; the same four probe elements score RMS 148/157/70/127 against it and 42/44/38/69 against
// Frame 59, and only Frame 59's positions are mutually coherent (the five checklist rows fall on one
// 94 px pitch; the three button glyphs sit at a constant +44,+39 from their own plates). Recorded in
// the manifest, with the old guess marked superseded in place rather than deleted (C7.1 / C1.16).
//
// ---- ⛔ WHAT THIS UNIT DELIBERATELY DID NOT TOUCH ----
//
// TYPE. Every size below is the size it already had. S153c owns this page's 37 below-floor text draws
// and is HELD on its own owner question (S153c-Q1: where the LIVE/STATIC line runs on a checklist row).
// ⛔ Nothing here raises type and nothing here lowers it, so the R-01 census cannot move: the same 38
// text draws, the same strings, the same sizes. Only x, y and alignment changed.
// ⛔ `Typography.BarDesign` (29) binds the BOTTOM BAR ALONE and is not borrowed here - that is the named
// failure mode of the bar's own ruling. Every surface on this page keeps MinDesignFor / DenseDesignFor.
//
// TINTS THAT CARRY A RECORDED DECISION. The export strokes the three command plates WHITE. They stay
// `Dim`, because S75 and §14.4(a) say a control that cannot act is not painted as one, and S110 wrote
// that down: "They go back to White AND into a hit table together, or not at all." Same for NEXT, for
// ENTER READ-ONLY, for the note text's dim tail, and for the checklist's `Done[i] ? White : Dim`.
// C1.8 makes those final; the export does not overrule them and this unit did not try.
//
// THE GLYPHS. The export ships its own `Vector.png` (a filled disc + tick), `Ellipse 110.png` (an EMPTY
// RING, for the one step not done), `bytesize_eye.png` and `heroicons-solid_view-grid.png`. This file
// keeps `ic_check` / `ic_eye` / `ic_grid` and only moves them to the export's boxes. ⚠ Swapping step 5's
// dim tick for the export's empty ring is a GLYPH change, and the register already ruled on that shape:
// "the `White : Dim` pair is `VrioTestPage`'s established idiom and changing the GLYPH would be a new
// decision, so it was not made here" (REGISTER.md, S158's observation for QC). Logged, not taken.
//
// THE PAGE BORDER. `Frame 59.svg` draws a 2 px white border round the whole artboard. That is S177, and
// S177 scopes it to whichever unit reaches the Cover and the Menu, not to this one. C1.1: not taken.
//
// THE BOTTOM BAR. Done, approved, and not rebuilt - here or anywhere.
//
// ---- ⛔ THE COLOUR POLICY THIS UNIT APPLIED, STATED ONCE SO IT IS NOT RE-DERIVED PER CALL SITE ----
//
// The export's raster was sampled directly rather than read off the SVG, so these are pixel values:
// every rule, both panel outlines, all three command plates and the read-only plate are pure WHITE;
// NEXT's plate alone is #8489A3; both note cards and the notes rail are #313D7B. Those last two are
// exactly `DragonPalette.Hairline`, and the plates' fill is #020738, exactly `DragonPalette.Background`
// - the design this palette was derived from, agreeing with itself.
//
//   FILLS of an element this unit rebuilt or ADDED from the export  ->  the export's own value.
//        (the note cards and the rail become `Hairline`; the plates become UNFILLED, which is what
//         #020738 means on a #020738 ground - and see `Pl` below, where that agrees with S75.)
//   STROKE AND TEXT TINTS                                           ->  THE PAGE'S OWN IDIOM, UNCHANGED.
//        The two panel outlines stay `Panel` and the nine rules take `Hair`, the tint of the single
//        rule this page already had.
//
// ⚠ SO THE PANELS DIVERGE FROM THE EXPORT ON PURPOSE, AND IT IS THE ONE THING WORTH ARGUING WITH.
// The reference outlines them white; this file leaves them `Panel`. Two reasons, neither of them
// "it was easier": a tint is not geometry, and `dl.Box(PX(48), PY(96), 720, 1700, St(3), Panel)` is
// shared VERBATIM with `SuitCheckPage`, the sibling procedure page whose template this one is. Making
// one of a matched pair white inside a geometry unit would split an idiom on no authority. **Logged
// for the owner rather than taken** - it is a one-line change if he wants it.
//
// ---- ⚠ THE ONE PLACE GEOMETRY AND TYPE WOULD NOT SEPARATE CLEANLY, SAID PLAINLY ----
//
// A multi-line block's ROW PITCH is set by the type it was drawn for. The export's heading rows are
// 86 px apart at a 48 px cap; its note rows are 45 px apart at a 21 px ascender. This page keeps a
// 40 px heading and 24-26 px note text, so taking those pitches LITERALLY would leave both blocks
// floating in white space at a third more leading than the design has.
// ⭐ So a block's ORIGIN and ALIGNMENT are the export's EXACTLY, and its pitch is the export's pitch
// SCALED by the ratio of this page's rendered ink height to the export's for that same block. The
// scaling is parameterised entirely by the export and goes to 1.0 - i.e. the pitch becomes the export's
// own - the day S153c sets this page's type to the frame's. Every other pitch on the page is
// STRUCTURAL rather than typographic (the checklist rows are bounded by the export's own rules, the
// command rows by its own button plates) and is therefore taken literally, unscaled.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class VrioTestPage
    {
        public const int Commands = 200;   // +BottomBar.Commands (S176: the bar is 19 commands, not 2)
                                           // S181: 160 -> 200. The rebuild adds the export's nine rules,
                                           // its note rail and its two note cards.
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba Bg     = DragonPalette.Background;
        static readonly Rgba Panel  = DragonPalette.Panel;
        static readonly Rgba Accent = DragonPalette.Accent;
        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Dim    = DragonPalette.Text6;
        static readonly Rgba Hair   = DragonPalette.Hairline;
        static readonly Rgba Go     = DragonPalette.Go;

        // ---- MEASURED OFF THIS PAGE'S OWN RENDER (S181), AND IT IS ONE NUMBER FROM ONE MEASUREMENT ----
        // `DisplayList.Text`'s y is the TOP OF THE LINE BOX. The export's coordinates are INK boxes -
        // alpha-trimmed crops - so placing an element needs the distance between the two, and that is a
        // property of the FONT and of the two renderers' layout, not of the design frame. Measured on the
        // five checklist rows of the 2026-09-07 baseline render (2560x1406), which are all-caps, isolated
        // and identical in size: ink began 5.92 design px below the drawn y at size 26, i.e. 0.228 of the
        // size, repeatable to +/-0.65 px across the five.
        // ⛔ IT IS CONTENT-DEPENDENT AND CANNOT BE OTHERWISE. A line whose tallest glyph is an ascender
        // (`h`, `l`, `t`) inks higher than one that is all capitals - the 62 px title measures 0.184.
        // Both drawings use the SAME strings, so the dependence largely cancels; the residual is under
        // 3 design px (2 device px at the shipped width) and is reported rather than tuned away.
        // ⚠ NOT `Typography.CapCentreOfTop` (0.553). That constant places a CAP CENTRE and was measured
        // for the bottom bar; this places an INK TOP. They are different questions and unifying them
        // would be a change to a shared measured constant, which is not this unit's to make.
        const float InkTopOfLine = 0.228f;

        static readonly string[] Check = {
            "1. THERMAL PRE-CHILL", "2. BEGIN FLUID LOADING", "3. STORE ITEMS",
            "4. TEST VRIO HEALTH LEDS", "5. COMPLETE FLUID LOADING" };
        static readonly bool[] Done = { true, true, true, true, false };

        // ==========================================================================================
        // THE EXPORT'S OWN COORDINATES.  Every number below is a measurement, and the comment on each
        // names the element it came from.  MARKED TIER-2 THROUGHOUT (S162's ruling; see the header).
        // ==========================================================================================

        // -- the two content panels.  `Rectangle 178` / `Rectangle 179`, confirmed by the SVG's two panel
        //    paths and by a raster scan of the frame's strokes.  ⚠ The export rounds their outer corners
        //    (r=9, and r=71 on the one corner that meets the bar); `DisplayList` has no rounded-rect
        //    primitive and inventing one is not this unit's job, so they are drawn square.  Logged.
        const float LeftX = 22f,  LeftY = 26f,  LeftW = 827f,  LeftH = 1929f;
        const float MainX = 889f, MainY = 26f,  MainW = 2516f, MainH = 1929f;

        // -- left panel interior
        const float HeadX     = 156f, HeadInk   = 272f;   // `4.700 - Deorbit Preparation` (462x147)
        const float HeadPitch = 46f;                      // export 86 at a 48 px cap; scaled to this
                                                          // page's 40 px type (cap 25.4): 86*25.4/48.
        const float DeorbitInk = 454f;                    // `deorbit` (136x24), same x as the heading
        const float RuleX0 = 154f, RuleX1 = 759f;         // `Line 93`..`Line 99`, all 605 px wide
        const float Rule0  = 510f, RulePitch = 94f;       // rules at 510,604,698,792,886,980
        const float TickX  = 163f, TickInk = 537f, TickS = 42f;   // `Vector` x4 / `Ellipse 110`
        const float RowX   = 230f, RowInk  = 550f;        // the five checklist labels
        const float FootRule = 1698f;                     // `Line 99`
        const float EyeBoxX = 219f, EyeBoxY = 1725f, EyeBoxS = 110f;  // `Rectangle 177` (SVG r=9)
        const float EyeIcoX = 250f, EyeIcoY = 1756f, EyeIcoS = 48f;   // `bytesize_eye`
        const float EyeLabX = 165f, EyeLabInk = 1860f;                // `enter read-only` (220x18)

        // -- main panel interior
        const float ColX     = 982f;                      // the export's single left text column
        const float SectInk  = 215f;                      // `section 4_ in progress` (389x24)
        // the KEPT refresh glyph (clause (2) - absent from the export, so it stays). Its 34 px box and
        // its 16 px gap to the label are the drawing's own, carried over unchanged; only the anchor moved.
        const float RefreshS = 34f, RefreshGap = 16f;
        const float TitleInk = 274f;                      // `Test VRIO Health LEDs`  (766x54)
        const float MainRuleX0 = 981f, MainRuleX1 = 2653f;// `Line 100` / `Line 101`, both 1672 px
        const float TopRule = 401f, BotRule = 1558f;
        // ⭐ THE STEP TEXT COLUMN. The export sets "4.1 Command:" as ONE run - the widest internal gap in
        //    that element is 12 px, an ordinary word space, not a gutter. This file keeps TWO draws so the
        //    text census cannot move, so it needs the run's second segment: measured at +61/+66/+65 on the
        //    three command rows and +63/+65 on 4.3/4.4, i.e. one column at +64. This page's numerals are
        //    narrower than the export's (33.5-37.5 px against 51-54), so nothing collides.
        const float StepDx = 64f;
        // ⭐ THE COMMAND BUTTON, RECOVERED AS A COMPONENT RATHER THAN AS THREE COINCIDENCES. All three
        //    plates RIGHT-ALIGN at x=2630 (2183+447 = 2197+433) and carry their glyph at +44,+39 and
        //    their label ink at +84,+42 from their own top-left, on every one of the three. The plate top
        //    sits +89 below its own step's text ink, on every one of the three.
        const float BtnRight = 2630f, BtnH = 106f, BtnDy = 89f;
        const float BtnIcoDx = 44f, BtnIcoDy = 39f, BtnLabDx = 84f, BtnLabInkDy = 42f;
        // `ic_grid`'s ink fills 0.887 of its 160 px source box, so the export's 28 px ink square needs a
        // 31.6 px draw box placed 1.78 px up and left of the ink origin.
        const float BtnIcoBox = 31.6f, BtnIcoPad = 1.78f;
        const float Cmd1Ink = 456f, Cmd2Ink = 762f, Step3Ink = 1047f, Step4Ink = 1176f, Cmd5Ink = 1304f;
        const float Btn1W = 447f, Btn2W = 447f, Btn5W = 433f;    // `Rectangle 138` / `187` / `188`
        const float NextX = 981f, NextY = 1591f, NextW = 274f, NextH = 106f;   // `Rectangle 189`
        const float NextInkX = 1082f, NextInk = 1633f;                         // `next` (72x21)

        // -- the notes rail and the two cards.  ⚠ THE RAIL IS NEW: `Rectangle 186` is in the export and
        //    was not on this page at all, so clause (1) obliges it.  Both card fills and the rail are
        //    #313D7B in the export, which is exactly `DragonPalette.Hairline`.
        const float RailX = 2663f, RailY = 398f, RailW = 20f, RailH = 867f;
        const float CardX = 2703f, CardW = 625f;
        const float Card1Y = 400f, Card1H = 202f;         // `Rectangle 185`, taken exactly
        const float Card2Y = 622f;                        // `Rectangle 190`
        const float NoteX = 2742f, NoteTextDx = 78f;      // "Note:" then the run's second segment (+79/+76)
        const float Note1Ink = 445f, Note2Ink = 668f;
        const float NotePitch = 38f;                      // export 45 at a 21 px ascender; scaled to this
                                                          // page's 26 px type (ink 17.5): 45*17.5/21.

        public static void Build(DisplayList dl, int w, int h)
        {
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            float SZ(float v) => v * sy;
            int St(float rs) => Strokes.Px(rs, sy);   // ONE rule, in Strokes.cs - rounds UP (R-02 family)
            // ---- the export gives INK tops; DisplayList wants LINE tops.  See InkTopOfLine above. ----
            float TY(float inkY, float size) => inkY - InkTopOfLine * size;
            void L(string t, float x, float inkY, float z, Rgba c)
                => dl.Text(t, PX(x), PY(TY(inkY, z)), SZ(z), TextAlign.Left, c);
            void Ico(string k, float x, float y, float s, Rgba c) => dl.Asset(k, PX(x), PY(y), SZ(s), SZ(s), c);
            void Rule(float x0, float x1, float y)
                => dl.Line(PX(x0), PY(y), PX(x1), PY(y), St(2), Hair);
            // an unfilled plate.  ⭐ THE EXPORT FILLS THESE WITH #020738 - THE PAGE GROUND - so they are
            // outlines, not filled buttons.  This file used to fill them `Panel`, which read as a live
            // control and is the exact shape S75 forbids on a page whose controls cannot act.  Taking the
            // export's own value therefore agrees with S75 rather than fighting it.
            void Pl(float x, float y, float pw, float ph, Rgba border)
                { dl.Box(PX(x), PY(y), pw * sx, ph * sy, St(2), border); }

            // a numbered command step: "N.N Command:" + a labelled button on the right
            // ⭐ S181 makes that last phrase exact rather than replacing it: the button is not merely
            // "on the right", the three plates RIGHT-ALIGN on the export's own x=2630 and vary in width
            // to fit their labels. The original wording is kept above because it was never wrong.
            // ⛔ INERT, AND DRAWN AS INERT - see the header and S110's note below.
            void Cmd(string num, string label, float inkY, float plateW)
            {
                L(num, ColX, inkY, 32, White); L("Command:", ColX + StepDx, inkY, 32, White);
                // ---- S110 / QC VT-01: THESE THREE COMMAND THE VEHICLE AND CANNOT, SO THEY ARE INERT ----
                // START VRIO 1 / START VRIO 2 / STOP VRIO 2 drive the flight computer's health LEDs.
                // QC classes them (B): §14.4(a) honest no-op until Part B, and they must NOT be given
                // working rectangles in Part A. They were drawn plate + border + white glyph + white
                // label - the full live idiom, on the most complete procedure screen in the build, with
                // seven painted controls and zero hit rects between them.
                // S75's rule applies whichever class they land in: a control that cannot act is not
                // painted as one. They go back to White AND into a hit table together, or not at all.
                // ⚠ S181: this plate MOVED and it is still not hittable. There is no HitTest in this file
                // and no ScreenPainter branch, so draw, hit and marker did NOT move together - because
                // there is no hit and no marker to move. That is S182's (unit 2b's) work and it is said
                // here rather than left to be discovered from a control that looks live and is not.
                float px = BtnRight - plateW, py = inkY + BtnDy;
                Pl(px, py, plateW, BtnH, Dim);
                Ico("ic_grid", px + BtnIcoDx - BtnIcoPad, py + BtnIcoDy - BtnIcoPad, BtnIcoBox, Dim);
                L(label, px + BtnLabDx, py + BtnLabInkDy, 26, Dim);
            }

            dl.Rect(0, 0, w, h, Bg);

            // ================= LEFT PANEL: 4.700 checklist =================
            dl.Box(PX(LeftX), PY(LeftY), LeftW * sx, LeftH * sy, St(3), Panel);
            // ⭐ TIER-2 FILL (S162): the frame sets this heading LEFT-ALIGNED at its own x; this file used
            //    to centre it. Alignment is the frame's, the SIZE is untouched (S153c owns type).
            L("4.700 - Deorbit", HeadX, HeadInk, 40, White);
            L("Preparation",     HeadX, HeadInk + HeadPitch, 40, White);
            L("DEORBIT", HeadX, DeorbitInk, 30, Accent);
            for (int i = 0; i < Check.Length; i++)
            {
                // ⭐ TIER-2 FILL (S162): the row pitch, the rule above each row and the tick's box are the
                //    export's own - `Line 93`..`Line 98` at a 94 px pitch, `Vector` at 42x42.
                Rule(RuleX0, RuleX1, Rule0 + i * RulePitch);
                // S110 / QC VT-01 + F-01, and both point the same way. `Done` is a COMPILE-TIME LITERAL
                // (`:34`), so a filled GREEN tick was this page asserting a completion verdict it has no
                // source for - S31/S32's rule, and the same shape MP-01 was fixed for. The reference
                // frame draws these as white-on-dark, not green, so §1.4 and the liveness rule agree.
                // ⛔ The STATE (four done, one open) is reference copy and is reproduced untouched; only
                // the colour was ours. It goes back to `Go` when a real step model drives it - which is
                // VT-01's remaining half, blocked on the stranded `StepList` (S49 §1.1 / H34).
                // ⚠ S181: the export draws step 5 as `Ellipse 110`, an EMPTY RING, rather than as a dim
                // tick. That is a GLYPH change and the register has already called that "a new decision",
                // so it is logged and not taken here. Only the box moved.
                Ico("ic_check", TickX, TickInk + i * RulePitch, TickS, Done[i] ? White : Dim);
                L(Check[i], RowX, RowInk + i * RulePitch, 26, White);
            }
            Rule(RuleX0, RuleX1, Rule0 + Check.Length * RulePitch);   // the rule closing the last row
            Rule(RuleX0, RuleX1, FootRule);                           // `Line 99`
            // S110 / QC F-01 + VT-01. Two changes, two different reasons:
            // GLYPH: this was `ic_stop`, a filled rounded rect, where the reference frame draws an EYE -
            // and `ic_eye` is already in the asset set, already used by SuitCheckPage for the identical
            // "ENTER READ-ONLY" control. It was a placeholder that outlived its excuse; the reference and
            // the sibling page agree, so there is nothing to decide.
            // TINT: the control has no hit rect (no HitTest in this file, no ScreenPainter branch), so
            // S75 says it must not be painted as a live button - the same call SC-02 made for the two
            // plates on the Suit Leak Check, which is this page's own template.
            // ⚠ S181: still no hit rect. The plate moved to `Rectangle 177`'s box and the tint is unchanged.
            Pl(EyeBoxX, EyeBoxY, EyeBoxS, EyeBoxS, Dim); Ico("ic_eye", EyeIcoX, EyeIcoY, EyeIcoS, Dim);
            // ⭐ TIER-2 FILL (S162): the frame sets this label LEFT-ALIGNED under the plate, not centred.
            L("ENTER READ-ONLY", EyeLabX, EyeLabInk, 26, Dim);

            // ================= MAIN PANEL =================
            dl.Box(PX(MainX), PY(MainY), MainW * sx, MainH * sy, St(3), Panel);
            // ⚠ THE REFRESH GLYPH IS **KEPT**, AND CLAUSE (2) IS WHY. QC filed it as VT-02's fourth
            // difference - the rebuild adds a glyph the frame does not have - and S153c cautioned that it
            // is `SuitCheckPage`'s own idiom and "the sibling procedure frame has to be checked before
            // removing it". It need not be: §14.2a clause (2) settles it outright. "Absence bounds what
            // may be ADDED. It says nothing whatever about what must be REMOVED." It is absent from the
            // export, so it STAYS EXACTLY AS IT IS.
            // ⚠ It KEEPS ITS RELATIONSHIP to the label rather than its absolute x, and that is the only
            // honest reading of "stays exactly as it is" for an element whose anchor moved: the label it
            // sits in front of has moved left to the export's column, and a glyph frozen at x=1180 would
            // now be to the RIGHT of the text it introduces. Its 16 px gap and its vertical centring on
            // the label's cap band are both preserved from the drawing it came from.
            Ico("ic_refresh", ColX - RefreshGap - RefreshS, SectInk + 19.1f / 2f - RefreshS / 2f,
                RefreshS, Accent);
            // ⭐ TIER-2 FILL (S162): section label and title both take the frame's single left column.
            // ⭐⭐ S224, 2026-09-08 — A TIER-1 PHOTOGRAPH NOW CORROBORATES THE LEFT ALIGNMENT ABOVE.
            //    The comment line above is left EXACTLY as written (C1.16 / G12: superseded reasoning is
            //    marked in place, never rewritten), and its **`TIER-2 FILL` mark STAYS**. This annotation
            //    raises the CONFIDENCE in the two draws below. It does not change the tier, and ⛔ **no
            //    pixel moved** — nothing here touched a coordinate, a colour, a size or a draw call.
            //    ⭐ WHAT IS CONFIRMED. When [[S162]] ruled, no photograph was reachable, so the single
            //    left column both elements take was INFERRED from `frame59`, the tier-2 export. A
            //    photograph is now reachable and it AGREES: the layout [[S153c]] applied is CONFIRMED,
            //    not merely filled. Independent source, same answer.
            //    ⭐ MEASURED, NOT ASSERTED — THE LIBRARIAN, 2026-09-08, on `discovery2.jpg` at native
            //    resolution, ink extents isolated by colour, keystone corrected +0.086 px/px:
            //      `SECTION 2: IN PROGRESS`          measured left 37, left-if-centred 216 → 179 px apart
            //      step title `Execute Suit Leak Check` measured left 42, left-if-centred 167 → 125 px
            //    Both sit hard left, and both are an order of magnitude further from their centred
            //    position than any keystone or ink-extent error, so neither is a close call.
            //    ⚠ THE STRENGTH IS **TEMPLATE-LEVEL, NOT PAGE-LEVEL** — DO NOT OVERSTATE IT.
            //    `discovery2.jpg` photographs procedure **4.011 Suit Leak Check**, NOT this page's
            //    **4.700**. It is still the right evidence at the right strength, and `REGISTER.md:4578`
            //    is why: *"4.011's photographed grammar is the grammar copied."* The photograph fixes the
            //    TEMPLATE's column; it does not photograph THIS page. That residual gap is precisely why
            //    the tier does NOT move and the mark above stays.
            //    ⚠ PROVENANCE, STATED SO IT IS NOT MISTAKEN FOR AN IN-REPO SOURCE: `discovery2.jpg` is
            //    NOT in this repository (`find . -iname "discovery*"` → nothing, 2026-09-08 — the same
            //    absence [[S162]] found four ways). The numbers above are RELAYED and cannot be
            //    re-measured from this repo as it stands. See [[S184]] Q1 for the reachability question.
            //    🟢 THE OWNER RULED IT, 2026-09-08, verbatim **"option 2"** — re-label the affected
            //    comment. ⛔ **`VT-02` WAS NOT REOPENED** and is not reopened here.
            //    ⭐ AND WHY A FULL REOPEN WAS REJECTED, because a future reader will ask: **4 of the 7
            //    `VT-02` elements are not visible in ANY of the 80 archived photographs.** A reopened
            //    element-by-element pass would still be INFERENCE on those four, and would land on the
            //    same pixels these draws already use — re-deciding nothing, at the cost of re-opening a
            //    closed finding.
            L("SECTION 4: IN PROGRESS", ColX, SectInk, 30, Accent);
            L("Test VRIO Health LEDs", ColX, TitleInk, 62, White);
            Rule(MainRuleX0, MainRuleX1, TopRule);     // `Line 100`

            Cmd("4.1", "START VRIO 1 LED TEST", Cmd1Ink, Btn1W);
            Cmd("4.2", "START VRIO 2 LED TEST", Cmd2Ink, Btn2W);
            L("4.3", ColX, Step3Ink, 32, White);
            L("Verify functionality of VRIO health LEDs (left side of command panel)",
              ColX + StepDx, Step3Ink, 30, White);
            L("4.4", ColX, Step4Ink, 32, White);
            L("Contact SpaceX to report LED status", ColX + StepDx, Step4Ink, 30, White);
            Cmd("4.5", "STOP VRIO 2 LED TEST", Cmd5Ink, Btn5W);
            Rule(MainRuleX0, MainRuleX1, BotRule);     // `Line 101`
            // S110 / QC VT-01: NEXT is (A) - navigation/screen state, buildable in principle - but it is
            // not built, because what it advances TO is the step model that does not exist yet (the
            // stranded `StepList`, S49 §1.1 / H34). Until it advances something it is a painted control
            // that resolves to nothing, so it takes the inert tint like its neighbours.
            // ⚠ S181: the export strokes this plate #8489A3 - which IS `Dim`, this page's inert tint and
            // the one its own comment says it takes "like its neighbours", while the code used `Hair`.
            // The tint is NOT changed here (colour is not this unit's scope and S75 owns these plates);
            // the divergence is logged so the two readings are not silently reconciled by a later chat.
            Pl(NextX, NextY, NextW, NextH, Hair);
            // ⭐ TIER-2 FILL (S162): the frame sets NEXT's label left in its plate, not centred.
            L("NEXT", NextInkX, NextInk, 34, Dim);

            // ================= RIGHT PANEL: notes =================
            // ⚠ NEW ELEMENT, REQUIRED BY CLAUSE (1): `Rectangle 186` (20x867 at 2663,398, fill #313D7B)
            //    is in the export and was not drawn on this page at all.
            dl.Rect(PX(RailX), PY(RailY), RailW * sx, RailH * sy, Hair);

            // ⭐ TIER-2 FILL (S162): both cards take the export's x, width and top. Card 1 also takes its
            //    height exactly. Card 2's height is COMPUTED, and that is stated rather than hidden: the
            //    export fits its note into SIX rows of its own smaller type, this page's line-breaking
            //    needs EIGHT of its own, so the card is grown to contain them at the export's own bottom
            //    inset. It returns to the export's 353 the day S153c sets the type and the text re-breaks.
            dl.Rect(PX(CardX), PY(Card1Y), CardW * sx, Card1H * sy, Hair);
            L("Note:", NoteX, Note1Ink, 26, Accent);
            L("Each VRIO LED is zero fault",   NoteX + NoteTextDx, Note1Ink,                 26, White);
            L("tolerant. This test ensures prior", NoteX,          Note1Ink + NotePitch,     26, White);
            L("awareness of a malfunction.",   NoteX,              Note1Ink + NotePitch * 2, 26, White);

            const float note2Rows = 8f, note2BottomInset = 46f;
            float card2H = (Note2Ink + (note2Rows - 1f) * NotePitch + 24f + note2BottomInset) - Card2Y;
            dl.Rect(PX(CardX), PY(Card2Y), CardW * sx, card2H * sy, Hair);
            L("Note:", NoteX, Note2Ink, 26, Accent);
            L("LED operation begins at the",     NoteX + NoteTextDx, Note2Ink,                 26, White);
            L("start of entry sequence. Any",    NoteX,              Note2Ink + NotePitch,     26, White);
            L("light flashing indicates automated", NoteX,           Note2Ink + NotePitch * 2, 24, White);
            L("chute deployment is available.",  NoteX,              Note2Ink + NotePitch * 3, 24, White);
            L("- VRIO 1 LED - FC connected",     NoteX,              Note2Ink + NotePitch * 4, 24, Dim);
            L("- VRIO 2 LED - Ready for",        NoteX,              Note2Ink + NotePitch * 5, 24, Dim);
            L("  automated backup if FC",        NoteX,              Note2Ink + NotePitch * 6, 24, Dim);
            L("  disconnected",                  NoteX,              Note2Ink + NotePitch * 7, 24, Dim);

            // ================= bottom status bar =================
            BottomBar.Draw(dl, w, h, BarFit.Stretch);   // S103: undistorted;
            // S176 / S172: FULL BLEED - the bar takes this page's own x-map, not the letterbox.
        }
    }
}
