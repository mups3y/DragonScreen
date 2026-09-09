/*
 * DragonScreen - Typography
 *
 * PURE. The type scale, in render-target pixels.
 *
 * ---- 16 PX IS MEASURED, NOT CHOSEN ----
 * Established in game 2026-08-05 from the proof page's legibility ramp, at 1280 px across a screen
 * 0.2844 m wide, seen from the seat:
 *
 *     "technically every single one is visible from the seat as you can use the mouse wheel to zoom
 *      view. 16px is legible without needing to zoom"
 *
 * Both halves of that matter. Zoom means nothing is UNREADABLE, so there is no hard floor - but
 * text the pilot has to zoom for is text they will not read during an approach. The threshold that
 * counts is GLANCEABLE, and it is 16.
 *
 * ---- THE RULE THAT FALLS OUT OF IT ----
 * NEVER SHRINK BELOW 16 PX TO CREATE HIERARCHY. GO DIMMER INSTEAD.
 *
 * That is not a compromise, it is what the source art already does: DragonPalette's nine-step text
 * ladder exists, in its own words, to carry "the hierarchy that would otherwise need weight or size
 * changes". Caption-versus-value on this design is a BRIGHTNESS difference far more than a size one.
 * Reaching for a smaller size is the instinct to resist.
 *
 * ---- THE FLOOR IS A RATIO, NOT A PIXEL COUNT (R-02, 2026-09-06) ----
 * ⛔ ADDED, NOT A REWRITE. Everything above this line is the ORIGINAL text, recovered VERBATIM
 * from commit 14b8c2a. It was deleted at 158eb2a (the ground-up autopilot rebuild), which took this
 * file from 55 lines to 20 and left the two headings above standing over nothing for ten days. Read
 * what follows as an amendment to it: the measurement above is still the measurement, and it is the
 * whole reason for this.
 *
 * READ THE PREMISE AGAIN: "at 1280 px across a screen 0.2844 m wide, seen from the seat". The 16 is
 * a count of RENDER-TARGET PIXELS, and it is the glanceable threshold only while the render target
 * is 1280 px across that same 0.2844 m of glass. What was measured is an ANGLE. The pixel count is
 * how the angle was written down.
 *
 * On 2026-09-05 the shipped screenWidth went 1280 -> 2560 (Q5 / S115). The glass did not move and
 * the crew's eyes did not move, so the measured angle is unchanged - but every comparison in the
 * build still read 16, and 16 px on a 2560-wide render is HALF the angle it was measured as. Every
 * legibility check in the build silently became twice as permissive on the day the cfg changed. Two
 * tasks then computed a fix for QC C-05 against that halved floor and called it safe (S112, S115;
 * unwound by the 2026-09-06 batch, job 1). Nobody could check the constant against its own premise,
 * because the premise had been deleted out of this file.
 *
 * So the floor is expressed as a RATIO to the width it was measured at and resolved at the point of
 * comparison: MinFor(panelW). Min stays exactly the number that was measured - it is the value AT
 * RefPanelW, and it is what MinFor returns there.
 *
 * ⛔ DO NOT "FIX" THIS BY RETYPING 16 AS 32. That is right for today's cfg and wrong for the next
 * one, and it throws the measurement away a second time: a bare constant cannot be checked against a
 * premise it does not carry. With the ratio form, a width change needs no edit here at all.
 *
 * ⛔ AND RAISING THE RESOLUTION DOES NOT MAKE TEXT MORE LEGIBLE. It buys crispness - more texture
 * pixels per glyph - and nothing else. Anything drawn as a FRACTION of the panel subtends the same
 * angle in the seat at any width, so every "too small to read" finding survives a width change
 * unchanged; only its pixel figures move (QC R-01, verified at both widths).
 *
 * ---- TWO FLOORS, NOT ONE: THE OWNER'S R-01 POLICY (S153, 2026-09-06) ----
 * ⛔ ADDED. Nothing above this line is edited.
 *
 * QC R-01 found that essentially every text element on the Figma-era pages sits under the floor
 * above. Fixing it is a design decision, not an arithmetic one, and the owner made it on 2026-09-06
 * by selecting "SPLIT BY CONTENT TYPE" from presented options (a SELECTION, not free text - there is
 * no verbatim quote and none is invented here; see REGISTER.md S153). The option, as presented:
 *
 *     Raise anything LIVE to the floor - checklist state words, CONSUMABLES rows, SEAT TACH rows,
 *     Frame58's MANUAL/DOCKING. Permit STATIC reference tables (the Cover's timeline/contingency
 *     cards, pad captions) to sit at Typography.Dense, which already exists for exactly this: "a
 *     table someone leans in to read... NOT for any live value, any alert."
 *
 * So there are now TWO floors, and which one applies is a question about the CONTENT:
 *     LIVE               -> MinFor(panelW)     anything that changes, or that matters in a hurry
 *     STATIC REFERENCE   -> DenseFor(panelW)   a printed table the crew lean in to read
 * Dense's own docstring already drew that line; this makes it the project's stated policy rather
 * than one size's private note.
 *
 * ⚠ AND "PERMIT THEM TO SIT AT DENSE" IS A RAISE, NOT A PARDON - measured, because reading it the
 * other way is the obvious mistake. The owner's own named static examples are BELOW Dense today:
 * the Cover's reference rows draw 17.3 panel px and the docking pad captions 14.6, against a Dense
 * floor of 24 at the shipped 2560. Of 868 below-floor text draws counted across the 20 Figma-era
 * page files, only 43 sit in the Dense..floor band where the static allowance changes the verdict.
 * The policy makes the job smaller for those 43; it does not excuse anything from moving.
 *
 * ---- AND THE PAGES MUST NOT HARDCODE THE ANSWER ----
 * A Figma-era page draws in a 3427x2112 DESIGN frame and multiplies by its own frame scale, so the
 * design size that clears a floor is `floor / frameScale`. At the shipped 2560x1406 that is 48.07
 * design px for LIVE and 36.05 for static reference. ⛔ DO NOT WRITE 48 INTO A PAGE. That is R-02
 * repeating itself one layer up: a bare number is right for one cfg, wrong for the next, and cannot
 * be checked against a premise it does not carry. MinDesignFor / DenseDesignFor below resolve it at
 * the point of comparison, from the panel the page is actually drawing on.
 *
 * ---- A THIRD FLOOR, AND IT IS THE BOTTOM BAR'S ALONE (S176 edit 3, 2026-09-06) ----
 * ⛔ ADDED, NOT A REWRITE. Nothing above this line is edited.
 *
 * The owner selected, on 2026-09-06, that the BOTTOM BAR's text draws at 29 DESIGN px against a floor
 * of the bar's own - and, in the same breath, that the bar "still has a floor and is still in the
 * census - it was NOT exempted". BOTH HALVES ARE THE RULING. It is a SELECTION relayed through the
 * overseer, not free text, exactly as S153's policy above was; REGISTER.md S176 edit 3 quotes the
 * wording it arrived in and states that form plainly rather than inventing a sentence for it.
 *
 * WHY A THIRD FLOOR AND NOT A HOLE IN THE CENSUS. He was shown this bar's one live value rendered at
 * the glanceable floor and rejected it in those words - "the text is way to big and looks out of
 * place" (REGISTER.md S179, D1) - then chose 29 off a rendered 20 / 29 / 36 / 48 ladder, captioned
 * with what each rung matched. 29 is below BOTH floors above, so there were only three ways to draw
 * it: exempt the bar from the census, lower the hard ratchet for every surface, or give the bar a
 * floor of its own that the census still enforces. He took the third - and it is the only one of the
 * three that keeps the property the census exists for, which is that below-floor text stays VISIBLE
 * to the build instead of becoming invisible to it.
 *
 * ⚠ THIS IS A NARROWER FLOOR, NOT A LOWER STANDARD. It binds ONE component. Every other surface
 * keeps MinFor / DenseFor exactly as they are, and the bar is still ratcheted - against 29 rather
 * than against 24. "The bar is exempt" is NOT what was ruled, and a later page quietly borrowing this
 * floor for something that is not the bar is the failure mode to watch for.
 *
 * ⛔ AND WHY IT IS A DESIGN-SPACE CONSTANT, WHICH IS R-02 A THIRD TIME. 29 design px is ALREADY a
 * fraction of the panel: the bar draws everything at BottomBar.Scale(h) = h / 2112, so the drawn size
 * is 29 * h / 2112 - the same fraction of the glass, and therefore the same ANGLE in the seat, at any
 * render width. It needs no ratio form because it IS one. Writing its PANEL-pixel value (19.31 at the
 * shipped 2560x1406) into anything is the R-02 mistake repeating itself once more.
 */
namespace DragonScreen
{
    public static class Typography
    {
        /// <summary>
        /// The width every size in this class was MEASURED at: 1280 render-target px across the
        /// 0.2844 m screen, from the seat (see the header). Read each constant below as "px at this
        /// width"; ScaleFor / MinFor turn that into px at the width actually being rendered.
        ///
        /// ⛔ NOT the shipped width, and it must never be edited to follow it. The shipped width is
        /// screenWidth in plugin/GameData/DragonScreen/DragonScreen.cfg (2560 since S115) and is free
        /// to change. This is the width the eye test was done at, which is history and does not.
        /// </summary>
        public const float RefPanelW = 1280f;

        /// <summary>
        /// The scale from a size in this class to panel pixels, on a panel panelW device px wide.
        /// 1.0 at RefPanelW, 2.0 at 2560. Multiply any Typography.* size by this to draw it at the
        /// physical size it was measured at, whatever the render target is.
        /// </summary>
        public static float ScaleFor(float panelW)
        {
            return (panelW > 0f ? panelW : RefPanelW) / RefPanelW;
        }

        /// <summary>
        /// The glanceable floor IN PANEL PIXELS, on a panel panelW device px wide - the honest form of
        /// Min, and the one every legibility comparison should be written against.
        ///
        /// 16 at 1280, 32 at 2560, and the same ANGLE in the seat at both, which is what was actually
        /// measured. A comparison written against Min instead is measuring against the 1280 floor on
        /// whatever panel it is running on: that is R-02, and it is how C-05's fix came to be computed
        /// at half size twice and recorded as safe.
        /// </summary>
        public static float MinFor(float panelW)
        {
            return Min * ScaleFor(panelW);
        }

        /// <summary>
        /// The STATIC-REFERENCE floor in panel pixels, on a panel panelW device px wide - Dense's
        /// ratio form, exactly as MinFor is Min's. 12 at 1280, 24 at 2560.
        ///
        /// ⛔ This is NOT a second glanceable floor and must never be used as one. It is the level the
        /// owner's 2026-09-06 policy permits for a printed reference table the crew lean in to read,
        /// and Dense's own docstring states the boundary: "NOT for any live value, any alert, or
        /// anything on the nav bar. If it would be a problem to miss it, it is not this size."
        /// </summary>
        public static float DenseFor(float panelW)
        {
            return Dense * ScaleFor(panelW);
        }

        /// <summary>
        /// The smallest DESIGN-space size that clears the glanceable floor, for a page whose design
        /// frame maps to the panel by <paramref name="frameScale"/> (a Figma-era page's own `sc`).
        /// 48.07 at the shipped 2560x1406.
        ///
        /// ⛔ EXISTS SO NO PAGE WRITES 48. That number is only correct for one cfg and one design
        /// frame, and a page carrying it could not be checked against the measurement it came from -
        /// which is R-02, one layer up. A degenerate scale falls back to the panel floor rather than
        /// dividing by zero, the same way MinFor handles a zero width.
        /// </summary>
        public static float MinDesignFor(float panelW, float frameScale)
        {
            return frameScale > 0f ? MinFor(panelW) / frameScale : MinFor(panelW);
        }

        /// <summary>The same, for STATIC REFERENCE content: the smallest design size that clears
        /// DenseFor. 36.05 at the shipped 2560x1406. Same rule about not hardcoding it.</summary>
        public static float DenseDesignFor(float panelW, float frameScale)
        {
            return frameScale > 0f ? DenseFor(panelW) / frameScale : DenseFor(panelW);
        }

        /// <summary>
        /// S153f: a LIVE element's design size, LIFTED to the glanceable floor and never below it.
        ///
        /// ⛔ IT LIFTS, IT DOES NOT SET. Math.Max, not assignment - an element already above its floor
        /// keeps the size the Figma export measured, so the design hierarchy the page was drawn with
        /// survives and only what is BELOW moves. That is what R-01 asked for and no more.
        ///
        /// ⛔ AND IT EXISTS SO THAT NO PAGE WRITES A FLOOR. Every caller passes its own panel width and
        /// frame scale and gets the answer resolved at the point of comparison - the rule the header
        /// above states twice, once for R-02 and once for the design frame. A page that computed
        /// `Math.Max(v, 48.07f)` would be right for one cfg and wrong for the next.
        ///
        /// ⚠ WHICH FLOOR APPLIES IS A QUESTION ABOUT THE CONTENT, and neither of these decides it. Use
        /// this for anything that changes or that matters in a hurry; use RefDesign for a printed table
        /// the crew lean in to read. Getting it the wrong way round is the one mistake the owner's
        /// two-floor policy makes possible, so neither has a default and both are named at the call site.
        /// </summary>
        public static float LiveDesign(float design, float panelW, float frameScale)
        {
            float f = MinDesignFor(panelW, frameScale);
            return design < f ? f : design;
        }

        /// <summary>S153f: a STATIC-REFERENCE element's design size, lifted to DenseFor's design form.
        /// ⛔ NOT a cheaper LiveDesign - see Dense's own docstring for the boundary.</summary>
        public static float RefDesign(float design, float panelW, float frameScale)
        {
            float f = DenseDesignFor(panelW, frameScale);
            return design < f ? f : design;
        }

        /// <summary>
        /// The floor for anything that must be read at a glance. MEASURED. Values, alerts, the nav
        /// bar, anything that changes, anything that matters in a hurry.
        /// </summary>
        /// <remarks>
        /// ⛔ THIS IS THE VALUE AT RefPanelW - it is not "the floor" on an arbitrary panel. Comparing
        /// a rendered panel-pixel size against it is correct only when the panel IS 1280 wide;
        /// anywhere else the comparison belongs against MinFor(panelW). Kept as a named constant
        /// because it is the measured number, and MinFor is derived from it.
        /// </remarks>
        public const float Min = 16f;

        /// <summary>Static labels and captions. At the floor deliberately - see the rule above.</summary>
        public const float Caption = 16f;

        /// <summary>Ordinary readable content.</summary>
        public const float Body = 20f;

        /// <summary>A value the pilot is actively watching.</summary>
        public const float Value = 28f;

        /// <summary>The one number a page is about. Use once per page, or it stops meaning anything.</summary>
        public const float Hero = 40f;

        /// <summary>
        /// Where a line of type puts its CAP CENTRE below the y it is drawn at, as a fraction of the
        /// size — the number you need to centre a label vertically in a box.
        ///
        /// ⛔ MEASURED OFF A RENDER, NOT DERIVED. `DisplayList.Text`'s y is the TOP of the line box,
        /// and how far the ink sits below that is a property of the FONT and of the two renderers'
        /// layout, not something the design frame knows. 0.553 was measured on a real render in
        /// [[S129]] for the bottom bar's CURRENT STATE value, and it is one number from one
        /// measurement — which is why it lives here rather than being re-guessed per call site.
        ///
        /// ⚠ THERE IS A THIRD FIGURE IN THE TREE AND IT IS NOT THIS ONE. `CoverPage.PadButton`
        /// centres with `0.45f`. It was not measured, it predates this constant, and unifying it
        /// moves the NavEarth cluster's labels, so it is LOGGED (REGISTER.md S178) rather than
        /// changed in passing.
        /// </summary>
        public const float CapCentreOfTop = 0.553f;

        /// <summary>
        /// How tall a line of CAPS or DIGITS actually inks, as a fraction of the size handed to
        /// <see cref="DisplayList.Text"/> — the number you need when a spec states an INK HEIGHT and
        /// the primitive takes a PIXEL SIZE.
        ///
        /// ⭐ IT EXISTS BECAUSE THE REBUILD'S SPECS ARE WRITTEN IN INK. `SPEC_GAUGES.md` §2.2 and
        /// `SPEC_OVERVIEW_STATUS_ROWS.md` §8 give every line an ink height and say, in as many words,
        /// "ASSERT INK HEIGHT, NEVER FONT SIZE — font metrics differ between the two renderers".
        /// A page that types a pixel size has thrown that measurement away; a page that divides by
        /// this has not, and <see cref="SizeForInk"/> is the one place the division happens.
        ///
        /// ⛔ MEASURED OFF A RENDER, NOT DERIVED — the same rule as <see cref="CapCentreOfTop"/>
        /// above. Measured 2026-09-09 through `PreviewMain.DrawText`'s exact call (GenericTypographic,
        /// AntiAliasGridFit) at six sizes from 16.0 to 44.4 device px: 0.690–0.766, mean 0.727. The
        /// spread is hinting, not disagreement — at 16 px a whole pixel is 6 % of the ink.
        ///
        /// ⚠⚠ AND THE FACE IT WAS MEASURED ON IS NOT THE FACE THE GAME DRAWS. On 2026-09-09 the
        /// preview was found to be resolving `Microsoft Sans Serif` for every D-DIN request in this
        /// build environment — proved by rendering the whole preview with `FontFamily` set to
        /// `"Microsoft Sans Serif"` and getting a BYTE-IDENTICAL PNG — while `KSP.log` records the
        /// game resolving `'D-DIN'` correctly on all three screens. The two faces' cap ratios differ
        /// by 3.5 % (D-DIN 0.710, Microsoft Sans Serif 0.735 by direct glyph measurement), so a line
        /// sized through this constant inks about 3.5 % shorter on the glass than in the preview.
        /// ⛔ DO NOT "CORRECT" THIS TO 0.710 UNTIL THE PREVIEW ACTUALLY REACHES D-DIN. The constant
        /// must describe the renderer that is measuring, or every ink assertion in the suite starts
        /// failing against a font nobody can see. See REGISTER.md S248 and `BOB-45`.
        /// </summary>
        public const float CapHeightOfSize = 0.727f;

        /// <summary>
        /// The pixel size that inks <paramref name="ink"/> px tall. The inverse of
        /// <see cref="CapHeightOfSize"/>, named so no page writes the division itself — the same
        /// reason <see cref="MinDesignFor"/> exists.
        /// </summary>
        public static float SizeForInk(float ink)
        {
            return ink <= 0f ? 0f : ink / CapHeightOfSize;
        }

        /// <summary>
        /// THE BOTTOM BAR'S OWN FLOOR, in DESIGN pixels - owner-selected 2026-09-06. See the header.
        ///
        /// ⛔ THIS ONE COMPONENT ONLY. 29 design px is 19.31 panel px at the shipped 2560x1406,
        /// which is below BOTH <see cref="MinFor"/> and <see cref="DenseFor"/>. It is legal on the bar
        /// because the owner ruled it there and nowhere else; anything else drawing below DenseFor is
        /// still a defect and the census still says so.
        ///
        /// ⛔ AND IT IS THE SIZE, NOT JUST THE FLOOR. The bar's text draws AT this rather than
        /// being lifted to it, so there is deliberately no BarDesign equivalent of
        /// <see cref="LiveDesign"/>: a "lift" implies a range of legal sizes above the floor, and the
        /// ruling named ONE size. If bar text ever needs to be larger than this, that is a new ruling.
        /// </summary>
        public const float BarDesign = 29f;

        /// <summary>
        /// The bar's floor in PANEL pixels, for a bar drawn at <paramref name="frameScale"/> (i.e.
        /// <c>BottomBar.Scale(h)</c>). 19.31 at the shipped 2560x1406.
        ///
        /// ⛔ EXISTS SO THAT NOTHING WRITES 19.31 - the same reason MinFor exists, and the same
        /// reason MinDesignFor exists one layer up. The census resolves the bar's floor through this
        /// at the point of comparison, from the panel the bar is actually drawn on.
        /// </summary>
        public static float BarFor(float frameScale)
        {
            return BarDesign * frameScale;
        }

        /// <summary>Dense reference detail, BELOW the glanceable floor and legal only because zoom exists.
        ///
        /// Permitted for a table someone leans in to read. NOT for any live value, any alert, or
        /// anything on the nav bar. If it would be a problem to miss it, it is not this size.
        /// </summary>
        public const float Dense = 12f;
    }
}
