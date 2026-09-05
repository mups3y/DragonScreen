// THE LEGIBILITY FLOOR IS A RATIO, AND A PAGE'S TYPE HAS TO TRACK THE PANEL. That is this suite.
//
// ---- THE DEFECT IT EXISTS FOR (QC R-02 + S117, landed 2026-09-06) ----
// `Typography.Min = 16f` is a MEASURED number - "at 1280 px across a screen 0.2844 m wide, seen from
// the seat", in Typography's own header, from the owner's own 2026-08-05 legibility ramp. What was
// measured is an ANGLE; 16 px is how the angle was written down at the width it was measured at.
//
// On 2026-09-05 S115 raised the shipped `screenWidth` 1280 -> 2560 (Q5). The glass and the crew did
// not move, so the angle did not move - but every `>= Typography.Min` comparison in the build still
// read 16, which on a 2560-wide render is HALF the angle it was measured as. Every legibility check
// silently became twice as permissive on the day the cfg changed. Two tasks (S112, S115) then
// computed QC C-05's fix against that halved floor and recorded it as safe; against the true floor
// the same fix overflows its card by 131 design px, which is exactly the number S112 had measured at
// 1280 and believed it had escaped. Nobody could check the constant against its own premise, because
// the premise had been DELETED out of Typography.cs at 158eb2a (55 lines -> 20, the two section
// headings left standing over nothing) and only the bare number survived.
//
// The second half is the same defect on a live screen: `NavPage` draws in literal RefPanelW pixels
// with no scale factor at all, so doubling the canvas HALVED the physical size of the NAV screen's
// text to the crew. NAV is what the right-hand console shows by default. That is S117.
//
// ---- WHY IT WAS NEVER CAUGHT ----
// Nothing in plugin/test/ compared the SAME element at TWO widths. Every check ran at 1280x703,
// where Min and MinFor(w) are the same number and an un-scaled page and a scaled one are identical.
// A floor whose premise is a width cannot be tested at one width. So every check below is written as
// a comparison ACROSS widths, and that is the property that makes it fail-closed: the arithmetic it
// pins is invariant, so it does not need re-deriving when the cfg moves again.
using System;
using DragonScreen;

public static class LegibilityFloorTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }
    static void Eq(string what, float got, float want, float tol)
    { Check(what, Math.Abs(got - want) <= tol, "got " + got + ", want " + want); }

    // The two widths the project has actually shipped, with the mesh-derived heights the cfg gives.
    const int W1 = 1280, H1 = 703;
    const int W2 = 2560, H2 = 1406;

    public static int Run()
    {
        Console.WriteLine("LegibilityFloorTest (QC R-02 + S117: the floor is a ratio, and NAV tracks the panel)");
        checks = 0; failures = 0;

        TheFloorCarriesItsPremise();
        R01Census();
        TheTwoFloorsAreBothRatios();
        TheSameElementReportsTheSamePercentageAtBothWidths();
        NavPageTracksThePanel();
        NavPageIsUnchangedAtTheReferenceWidth();
        StrokesKeepTheirPhysicalWeight();
        ChromeBarTracksThePanel();
        TheShippedPanelsAreExactlyTwoToOne();
        SharedWidgetsTrackThePanel();
        FlightPageTracksThePanel();
        VehiclePageTracksThePanel();
        DockingPageTracksThePanel();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (floor " + Typography.MinFor(W1) + " px @" + W1
                          + ", " + Typography.MinFor(W2) + " px @" + W2 + ")");
        return failures;
    }

    // ---- 1. THE CONSTANT STILL MEANS WHAT IT WAS MEASURED TO MEAN -------------------------------
    static void TheFloorCarriesItsPremise()
    {
        // The measured number itself. ⛔ If this ever reads 32, someone has "fixed" R-02 by retyping
        // the constant - which is right for one cfg and wrong for the next, and throws the
        // measurement away a second time. Typography's header says so in as many words.
        Eq("Min is still the 16 px that was MEASURED at RefPanelW", Typography.Min, 16f, 0f);
        Eq("RefPanelW is the width it was measured at", Typography.RefPanelW, 1280f, 0f);

        // The floor IS the constant at the reference width, and a ratio away from it everywhere else.
        Eq("MinFor(RefPanelW) is exactly Min", Typography.MinFor(Typography.RefPanelW), Typography.Min, 1e-4f);
        Eq("the floor at the shipped 2560 is 32 px", Typography.MinFor(W2), 32f, 1e-4f);
        Eq("...and at 640 it is 8 px", Typography.MinFor(640f), 8f, 1e-4f);
        Eq("...and at 3840 it is 48 px", Typography.MinFor(3840f), 48f, 1e-4f);

        // The scale is the same ratio, and it is what a page multiplies its RefPanelW sizes by.
        Eq("ScaleFor(1280) is 1", Typography.ScaleFor(W1), 1f, 1e-6f);
        Eq("ScaleFor(2560) is 2", Typography.ScaleFor(W2), 2f, 1e-6f);
        Check("the floor is proportional, not stepped",
              Math.Abs(Typography.MinFor(W2) / Typography.MinFor(W1) - (float)W2 / W1) < 1e-5f,
              "ratio " + (Typography.MinFor(W2) / Typography.MinFor(W1)));

        // A degenerate width must not divide by zero or hand back a negative floor - the glue clamps
        // screenWidth at 16 (DragonScreenMonitor), but a pure function may not assume its caller did.
        Check("a zero width falls back to the reference rather than exploding",
              Typography.MinFor(0f) == Typography.Min, "got " + Typography.MinFor(0f));
    }

    // ---- 2. THE INVARIANT R-02 ASKS FOR, ON A REAL ELEMENT --------------------------------------
    // R-02's own verify line: "at 1280 and 2560 the same element reports the same PERCENTAGE of the
    // floor". That is the whole finding in one sentence - the pixel figures double, the legibility
    // does not move, and a check that only ever ran at one width could not tell the two apart.
    static void TheSameElementReportsTheSamePercentageAtBothWidths()
    {
        // The HUD's margin affordance: a real control, on a shipped page, whose type is fitted to its
        // box and then measured against the floor. QC H-06 / Q8 - it does NOT clear the floor, at
        // either width, and that is the point: the verdict must not change when the canvas doubles.
        float pct1 = FitPercentOfFloor(W1, H1, "MANUAL", "DOCKING");
        float pct2 = FitPercentOfFloor(W2, H2, "MANUAL", "DOCKING");
        float rz1 = FitPercentOfFloor(W1, H1, "RENDEZVOUS", null);
        float rz2 = FitPercentOfFloor(W2, H2, "RENDEZVOUS", null);

        // ⛔ ASSERTED SINCE JOB 3 (2026-09-06). This was a PRINT when job 2 landed, because the two
        // percentages were 72.2% @1280 and 82.9% @2560 and the residual was a SECOND instance of the
        // same family, not a tolerance: MarginAffordance's box is built from Inset = 4f, BorderPx = 2f
        // and Pad = 2f - RefPanelW constants subtracted from a letterbox that scales - so the usable
        // width grew 2.13x while the panel grew 2.00x, and the fitted type came out slightly LESS bad
        // at 2560 for a reason with nothing to do with legibility. QC measured that same 2.13x
        // independently. Job 3 scaled those three constants; this is now an equality, which is what
        // R-02's own verify line asks for.
        Console.WriteLine("  note  fitted type vs the floor: MANUAL/DOCKING "
            + (pct1 * 100f).ToString("0.0") + "% @1280 vs " + (pct2 * 100f).ToString("0.0") + "% @2560"
            + ", RENDEZVOUS " + (rz1 * 100f).ToString("0.0") + "% vs " + (rz2 * 100f).ToString("0.0") + "%");
        Check("MANUAL/DOCKING is the SAME fraction of the floor at 1280 and 2560",
              Math.Abs(pct1 - pct2) < 0.005f,
              "@1280 " + (pct1 * 100f).ToString("0.00") + "%, @2560 " + (pct2 * 100f).ToString("0.00") + "%");
        Check("RENDEZVOUS likewise", Math.Abs(rz1 - rz2) < 0.005f,
              "@1280 " + (rz1 * 100f).ToString("0.00") + "%, @2560 " + (rz2 * 100f).ToString("0.00") + "%");

        // What job 2 DOES own is the floor side of that comparison, and it is now exact: the same
        // element measured against a floor that scales cannot look better merely for being rendered
        // larger. Before this fix pct2 was computed against 16 and read 165.8% - "comfortably legible"
        // for a control that had not changed size in the seat by one arc-minute.
        Check("the floor the fit is judged against doubles with the panel",
              Math.Abs(Typography.MinFor(W2) - 2f * Typography.MinFor(W1)) < 1e-4f,
              "@1280 " + Typography.MinFor(W1) + ", @2560 " + Typography.MinFor(W2));
        Check("...so neither label reads as MORE than the floor at the wider panel",
              pct2 <= 1f && rz2 <= 1f,
              "MANUAL/DOCKING " + (pct2 * 100f).ToString("0.0") + "%, RENDEZVOUS "
                  + (rz2 * 100f).ToString("0.0") + "%");

        // And the VERDICT the build reads off it, which is what was twice as easy to pass.
        Check("FitsLegibly returns the same answer at both widths",
              MarginAffordance.FitsLegibly(W1, H1, "MANUAL", "DOCKING")
                  == MarginAffordance.FitsLegibly(W2, H2, "MANUAL", "DOCKING"),
              "@1280 " + MarginAffordance.FitsLegibly(W1, H1, "MANUAL", "DOCKING")
                  + ", @2560 " + MarginAffordance.FitsLegibly(W2, H2, "MANUAL", "DOCKING"));

        // The MinMargin guard is a question about PHYSICAL room ("is there space for a control here"),
        // so the same ASPECT must answer the same way at any width. 1219x703 and 2438x1406 are the same
        // shape; both leave a letterbox just too narrow. With MinMargin left as a device-pixel 40 the
        // wide one would answer YES and the narrow one NO, for a screen of identical proportions.
        // Two aspects, each rendered at both widths. The narrow pair leaves a letterbox 2% of the
        // panel (under the 40/1280 = 3.125% threshold); the roomier pair leaves 4% (over it). With
        // MinMargin left as a device-pixel 40, the 2560 narrow panel measures 51.2 px of letterbox
        // against a 40 px bar and says YES while its identical-shape 1280 twin says NO.
        {
            float x, y, bw, bh;
            bool tightNarrow = MarginAffordance.Rect(1280, 757, out x, out y, out bw, out bh);
            bool wideNarrow  = MarginAffordance.Rect(2560, 1515, out x, out y, out bw, out bh);
            bool tightRoomy  = MarginAffordance.Rect(1280, 726, out x, out y, out bw, out bh);
            bool wideRoomy   = MarginAffordance.Rect(2560, 1451, out x, out y, out bw, out bh);
            Check("the MinMargin guard refuses a 2%-letterbox aspect at BOTH widths",
                  !tightNarrow && !wideNarrow, "@1280 " + tightNarrow + ", @2560 " + wideNarrow);
            Check("...and admits a 4%-letterbox aspect at BOTH widths",
                  tightRoomy && wideRoomy, "@1280 " + tightRoomy + ", @2560 " + wideRoomy);
        }

        // ⛔ AND IT IS STILL FALSE, DELIBERATELY. H-06 / Q8 is an open DESIGN question about the
        // margin's width; this suite pins that R-02 did not silently close it by doubling the canvas.
        Check("...and that answer is still FALSE - 2560 did not make this control legible",
              !MarginAffordance.FitsLegibly(W2, H2, "MANUAL", "DOCKING"),
              "the margin affordance now claims to clear the floor; if that is real, close QC H-06");
    }

    static float FitPercentOfFloor(int w, int h, string a, string b)
    {
        float x, y, bw, bh;
        if (!MarginAffordance.Rect(w, h, out x, out y, out bw, out bh)) return 0f;
        return MarginAffordance.FitSize(bw, h * 0.020f, a, b, Typography.ScaleFor(w)) / Typography.MinFor(w);
    }

    // ---- 3. S117: NAV'S TEXT AND GEOMETRY TRACK THE PANEL ---------------------------------------
    // S115 measured this empirically and its DONE-when says how to prove it: "ink rows at 1280 vs
    // 2560 must differ by the resolution ratio, not be identical". Measured here on the display list
    // rather than on pixels, which is the same statement one step earlier and needs no renderer.
    static void NavPageTracksThePanel()
    {
        foreach (NavMode mode in new[] { NavMode.Map, NavMode.Orbit, NavMode.Planet })
        {
            MapView view = MapProjection.WithMode(MapProjection.Default(), mode);
            DisplayList a = BuildNav(W1, H1, view);
            DisplayList b = BuildNav(W2, H2, view);

            Check("NAV/" + mode + ": doubling the panel draws the same page, command for command",
                  a.Count == b.Count && a.Count > 0, "1280 " + a.Count + " cmds, 2560 " + b.Count + " cmds");
            if (a.Count != b.Count) continue;

            int texts = 0, worst = -1;
            float worstA = 0f, worstB = 0f;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.At(i).Kind != DrawKind.Text) continue;
                texts++;
                // DrawCmd.C is the pixel size for a Text command.
                float ta = a.At(i).C, tb = b.At(i).C;
                if (Math.Abs(tb - ta * 2f) > 0.01f && worst < 0)
                { worst = i; worstA = ta; worstB = tb; }
            }
            Check("NAV/" + mode + ": it draws text at all", texts > 0, "found " + texts);
            Check("NAV/" + mode + ": EVERY label doubles when the panel doubles", worst < 0,
                  worst < 0 ? "" : "'" + a.At(worst).Str + "' is " + worstA + " px @1280 and "
                      + worstB + " px @2560 - it should be " + (worstA * 2f)
                      + ". A literal Typography.* with no * sc, which is S117.");

            // The boxes have to grow with the type or the type walks out of them - S117's own warning.
            // The readout column is the tightest case: doubled labels in a 276 px column that stayed
            // 276 px would have overrun it.
            float mx1, my1, mw1, mh1, mx2, my2, mw2, mh2;
            NavPage.MapRect(W1, H1, out mx1, out my1, out mw1, out mh1);
            NavPage.MapRect(W2, H2, out mx2, out my2, out mw2, out mh2);
            Eq("NAV/" + mode + ": the map well's left inset doubles", mx2, mx1 * 2f, 0.01f);
            Eq("NAV/" + mode + ": the map well's top doubles", my2, my1 * 2f, 0.01f);

            float bx1, by1, bw1, bh1, bx2, by2, bw2, bh2;
            NavPage.NextViewRect(W1, H1, out bx1, out by1, out bw1, out bh1);
            NavPage.NextViewRect(W2, H2, out bx2, out by2, out bw2, out bh2);
            Eq("NAV/" + mode + ": the NEXT VIEW button's width doubles", bw2, bw1 * 2f, 0.01f);
            Eq("NAV/" + mode + ": ...and its height doubles", bh2, bh1 * 2f, 0.01f);
        }

        // The one thing that must NOT double, and why: the page has to clear the chrome bar that is
        // ACTUALLY DRAWN, and ChromeBar is still RefPanelW-literal (its Height, Pitch and label do not
        // track screenWidth - the same defect on a bar that appears on every legacy page). Scaling the
        // clearance here and not the bar there would open a gap. Logged for the 2026-09-06 batch's
        // job 3; pinned here so the two cannot drift apart in the meantime.
        //
        // ⚠ SUPERSEDED IN PLACE 2026-09-06 by [[S120]] (C1.16/G12): the bar is no longer
        // RefPanelW-literal - it scales, and NavPage.ColumnBottom now clears HeightFor(w). The
        // paragraph above is kept because it is why this check was written two-sided in the first
        // place, and THE CHECK BELOW IS UNCHANGED: it was already correct for a scaled bar, because
        // it asserts the GAP rather than either side's absolute value. That is the whole reason it
        // survived the fix untouched, and it is the argument for writing checks this way.
        {
            float x1, y1, w1, h1, x2, y2, w2, h2;
            NavPage.NextViewRect(W1, H1, out x1, out y1, out w1, out h1);
            NavPage.NextViewRect(W2, H2, out x2, out y2, out w2, out h2);
            // Two-sided on purpose. "Clears the bar" alone would pass if the page left a 64 px hole
            // above it, which is what scaling ChromeBar.Height here (while the bar itself is not
            // scaled) would do. The gap must be the page's own padding, exactly - no overlap, no hole.
            // ⚠ Post-S120 that hazard runs the other way too: leaving the page on the bare constant
            // while the bar scales would put the bar OVER the controls at 2560. Two-sided catches
            // both, which is why it is stated as an exact equality and not a clearance.
            Check("NAV sits exactly one Pad above the chrome bar it clears, at 1280",
                  Math.Abs((ChromeBar.TopY(W1, H1) - (y1 + h1)) - 24f * Typography.ScaleFor(W1)) < 0.01f,
                  "gap " + (ChromeBar.TopY(W1, H1) - (y1 + h1)) + ", want " + (24f * Typography.ScaleFor(W1)));
            Check("NAV sits exactly one Pad above the chrome bar it clears, at 2560",
                  Math.Abs((ChromeBar.TopY(W2, H2) - (y2 + h2)) - 24f * Typography.ScaleFor(W2)) < 0.01f,
                  "gap " + (ChromeBar.TopY(W2, H2) - (y2 + h2)) + ", want " + (24f * Typography.ScaleFor(W2)));
        }
    }

    // ---- 4. AND THE REFERENCE WIDTH IS UNTOUCHED ------------------------------------------------
    // Sc(1280) is exactly 1, so the scale pass must be a no-op at the width every other test and
    // every historical measurement in docs/ was taken at. If this fails, the fix has moved the
    // baseline the whole project's numbers are quoted against.
    static void NavPageIsUnchangedAtTheReferenceWidth()
    {
        Eq("the reference width scales by exactly 1", Typography.ScaleFor(W1), 1f, 0f);

        MapView view = MapProjection.WithMode(MapProjection.Default(), NavMode.Map);
        DisplayList a = BuildNav(W1, H1, view);
        bool anyOffScale = false;
        string offender = "";
        for (int i = 0; i < a.Count; i++)
        {
            if (a.At(i).Kind != DrawKind.Text) continue;
            float t = a.At(i).C;
            // At sc = 1 every label must still be one of the type scale's own sizes.
            bool known = Math.Abs(t - Typography.Dense) < 0.01f
                      || Math.Abs(t - Typography.Caption) < 0.01f
                      || Math.Abs(t - Typography.Body) < 0.01f
                      || Math.Abs(t - Typography.Value) < 0.01f
                      || Math.Abs(t - Typography.Hero) < 0.01f;
            if (!known && !anyOffScale) { anyOffScale = true; offender = "'" + a.At(i).Str + "' at " + t + " px"; }
        }
        Check("at 1280 every NAV label is still exactly a Typography size", !anyOffScale, offender);

        // The map well at 1280 is the rect it always was: Pad 24 in, MapTop 58 down.
        float mx, my, mw, mh;
        NavPage.MapRect(W1, H1, out mx, out my, out mw, out mh);
        Eq("the map well still starts at Pad", mx, 24f, 0.01f);
        Eq("...and at MapTop", my, 58f, 0.01f);
    }

    // ---- 5. THE SAME DEFECT IN STROKES (batch job 3) --------------------------------------------
    // St(2) rounded 2*sc to the NEAREST whole device pixel with a floor of 1, which is 1 px at BOTH
    // 1280 and 2560 - so the design's 2 px rules became physically HALF AS THICK to the crew the day
    // the panel doubled. QC measured this while answering S101. Strokes.Px rounds UP instead, which
    // makes the thickness a constant fraction of the panel and errs thick rather than thin.
    static void StrokesKeepTheirPhysicalWeight()
    {
        // The design frame is 3427 x 2112, so a page's stroke scale is panelH / 2112.
        float sc1 = 703f / 2112f, sc2 = 1406f / 2112f;

        // The invariant: a design stroke is the same FRACTION of the panel at both widths.
        foreach (float rs in new[] { 2f, 3f, 5f })
        {
            float f1 = Strokes.Px(rs, sc1) / (float)W1;
            float f2 = Strokes.Px(rs, sc2) / (float)W2;
            Check("St(" + rs + ") is the same fraction of the panel at 1280 and 2560",
                  Math.Abs(f1 - f2) < 1e-6f,
                  Strokes.Px(rs, sc1) + " px of " + W1 + " (" + (f1 * 100f).ToString("0.000")
                      + "%) vs " + Strokes.Px(rs, sc2) + " px of " + W2 + " ("
                      + (f2 * 100f).ToString("0.000") + "%)");
        }

        // ...and never thinner than the design asks for in proportion, nor thinner than one pixel.
        foreach (float rs in new[] { 1f, 2f, 3f, 5f })
            foreach (float sc in new[] { sc1, sc2 })
            {
                Check("St(" + rs + ") at sc " + sc.ToString("0.000") + " is never below the design ratio",
                      Strokes.Px(rs, sc) >= rs * sc - 1e-4f, "got " + Strokes.Px(rs, sc));
                Check("St(" + rs + ") at sc " + sc.ToString("0.000") + " is never below one pixel",
                      Strokes.Px(rs, sc) >= 1, "got " + Strokes.Px(rs, sc));
            }

        // ⛔ AND THE REFERENCE WIDTH IS UNTOUCHED, for every argument the build actually uses. Ceiling
        // and round-to-nearest agree at 1280 on all four, which is why this fix moves nothing at the
        // width every historical figure in docs/ was measured at.
        foreach (float rs in new[] { 1f, 2f, 3f, 5f })
        {
            int roundWas = (int)Math.Round(rs * sc1); if (roundWas < 1) roundWas = 1;
            Check("St(" + rs + ") at 1280 is what it always was", Strokes.Px(rs, sc1) == roundWas,
                  "now " + Strokes.Px(rs, sc1) + ", was " + roundWas);
        }

        // The one that CANNOT be proportional, stated so nobody "fixes" it into a sub-pixel smear: a
        // 1 px design rule is 0.33 device px at 1280 and 0.67 at 2560, both under the 1 px a renderer
        // can draw, so both clamp to 1 and it does halve physically between them.
        Check("St(1) is at the one-pixel floor at both widths, which is the stated limit",
              Strokes.Px(1f, sc1) == 1 && Strokes.Px(1f, sc2) == 1,
              "@1280 " + Strokes.Px(1f, sc1) + ", @2560 " + Strokes.Px(1f, sc2));

        // ---- S122: AND THE COVER ANSWERS "HOW THICK IS A RULE" EXACTLY ONCE ------------------------
        // CoverPage had TWO stroke rules. St(rs) went through Strokes.Px and returned whole pixels;
        // a local `Stroke(sc, refPx)` returned a float with a 1 px floor, and four draws used it - the
        // map well's box, the NEXT VIEW pill, that pill's bar, and the d-pad button box.
        //
        // ⛔ AND THE FLOAT ONE WAS NOT MERELY INCONSISTENT, IT WAS THE R-02 DEFECT AGAIN. It is
        // proportional only ABOVE its own floor, and three of its four call sites asked for 2 px,
        // which is exactly where the floor fires at 1280 and not at 2560: 1.000 px of 1280 (0.0781%)
        // against 1.331 px of 2560 (0.0520%). The page's 2 px rules were a THIRD THINNER physically
        // at the shipped width. Job 3 of the 2026-09-06 batch ruled `Stroke` "correctly screen-space
        // rather than an instance of R-02's family" - true of the formula, false of the floored
        // function as actually called, and measuring it is what caught that.
        //
        // THE CHECK: collect every thin Rect the Cover draws - Box decomposes into four Rects whose
        // stroke becomes a side - and require the SET of thicknesses at 2560 to be the SET at 1280,
        // doubled. That is integrality and proportionality in one statement, and it is what having a
        // single rule buys. With the float rule the 2560 set contained 1.3314 and 3.9943.
        {
            string thin1 = ThinRectThicknesses(W1, H1);
            string thin2 = ThinRectThicknesses(W2, H2);
            Check("the Cover's rules are whole device pixels at 1280", thin1 == "1, 2", "got " + thin1);
            Check("...and exactly twice that at 2560 - one rule, scaled", thin2 == "2, 4", "got " + thin2);
        }
    }

    // ---- 6. S120: THE CHROME BAR TRACKS THE PANEL -----------------------------------------------
    // The bar is on EVERY legacy page, so it is the single component where this defect was most
    // visible and least noticed: at the shipped 2560 it was 64 px on a 1406-high panel - 4.55% of the
    // height where it was designed as 9.1% - with 16 device px labels on glass twice as wide.
    //
    // ⛔ WRITTEN ACROSS WIDTHS, like everything else here, and for the same reason: at 1280 alone a
    // scaled bar and an un-scaled one are the same bar. That is why nothing caught this for ten days.
    // ---- S121b-i: THE FLIGHT PAGE, ACROSS WIDTHS ------------------------------------------------
    // ⛔ THE CHECK THAT MATTERS HERE IS THE ROUND TRIP, NOT THE TYPE. FLIGHT is the one page in this
    // family whose controls are hit-tested: the fifteen crew steps, AUTO SEQUENCE and UNDOCK. Scaling
    // what is DRAWN without what is HIT lands a tap on the wrong milestone, and a wrong milestone
    // acknowledged is worse than an unreadable one. So the page is rendered and hit at BOTH widths.
    static void FlightPageTracksThePanel()
    {
        // ---- the step list's origin and pitch, which the draw and the rects share ----------------
        Eq("S121b-i StepTopFor doubles with the panel",
           Pages.StepTopFor(W2), 2f * Pages.StepTopFor(W1), 1e-3f);
        Eq("S121b-i StepTopFor at RefPanelW is the unscaled StepTop",
           Pages.StepTopFor(W1), Pages.StepTop, 1e-3f);
        Eq("S121b-i the step pitch doubles with the panel",
           Pages.StepPitchFor(W2, H2), 2f * Pages.StepPitchFor(W1, H1), 1e-3f);
        Check("S121b-i ...and the same number of milestones is reachable at both widths",
              Pages.StepVisible(W1, H1) == Pages.StepVisible(W2, H2),
              Pages.StepVisible(W1, H1) + " vs " + Pages.StepVisible(W2, H2));

        // ---- the two button rects ---------------------------------------------------------------
        RectDoubles("AutoRect", AutoBox(W1, H1), AutoBox(W2, H2));
        RectDoubles("MissionRect", MissionBox(W1, H1), MissionBox(W2, H2));

        // ---- ⭐ THE ROUND TRIP, AT BOTH WIDTHS ---------------------------------------------------
        FlightHitRoundTrip(W1, H1);
        FlightHitRoundTrip(W2, H2);

        // ---- and the page's TYPE, read off what it actually drew ---------------------------------
        // ⛔ Not a table of expected sizes: the page is built at both widths and the emitted text
        // commands are compared. Every size must double and the command count must be identical —
        // a page that scaled its type but dropped or added a draw would fail the second half.
        float[] t1 = FlightTextSizes(W1, H1), t2 = FlightTextSizes(W2, H2);
        Check("S121b-i the FLIGHT page draws the same number of text commands at both widths",
              t1.Length == t2.Length, t1.Length + " vs " + t2.Length);
        int bad = 0; float worst = 0f; int worstI = -1;
        for (int i = 0; i < t1.Length && i < t2.Length; i++)
        {
            float d = Math.Abs(t2[i] - 2f * t1[i]);
            if (d > 1e-3f) { bad++; if (d > worst) { worst = d; worstI = i; } }
        }
        Check("S121b-i every text size on the FLIGHT page doubles with the panel",
              bad == 0, bad + " of " + t1.Length + " did not; worst at index " + worstI
                        + " (@1280 " + (worstI >= 0 ? t1[worstI] : 0f)
                        + ", @2560 " + (worstI >= 0 ? t2[worstI] : 0f) + ")");
        Check("S121b-i ...and there is real type on the page to check",
              t1.Length > 40, "only " + t1.Length + " text commands");

        // ⚠ the whole point of the exercise: the smallest type on FLIGHT is the same SHARE of the
        // panel at both widths, whatever that share is. (It does not clear the floor — that is
        // [[S153b]]'s job — but it must not get worse when the canvas grows.)
        float min1 = Min(t1), min2 = Min(t2);
        Check("S121b-i the smallest type on FLIGHT is the same share of the panel at both widths",
              Math.Abs(min1 / W1 - min2 / W2) < 1e-7f,
              min1 + "/" + W1 + " vs " + min2 + "/" + W2);

        // ---- ⛔ AND THREE THINGS A CROSS-WIDTH RATIO CANNOT SEE ----------------------------------
        // Mutation testing found all three, and each survived a suite that looked thorough:
        //
        //   1. THE DRAWN ROWS AND THE HIT ROWS CAN SEPARATE. Every check above either compares two
        //      widths or locates a control with the very function it then tests, so `Flight` drawing
        //      its step column at the OLD unscaled offset while `StepRect` uses `StepTopFor` changed
        //      nothing any of them looked at — and that is a tap landing on the wrong milestone.
        //   2. THE WRONG SCALE CAN STILL DOUBLE. `StepColumn` deriving `sc` from its COLUMN width
        //      instead of the panel gives 0.256 and 0.513 — absurd, and exactly 2x apart, because the
        //      column doubles with the panel. Only an ABSOLUTE check at RefPanelW catches it.
        //   3. A TAP AT THE CENTRE OF A ROW HITS WHATEVER THE BAND IS. The tappable band is inset from
        //      the row; an unscaled inset is wrong at 2560 and invisible to a centre probe.
        // ⭐ the same position invariant [[S121b-ii]] added, applied to FLIGHT: on exactly-2:1 panels
        // every drawn coordinate doubles, which catches a column or a width left behind even when no
        // type size moved. Stated here rather than only for VEHICLE, because FLIGHT is the page with
        // the hit rects and a moved control is the expensive kind of wrong.
        float[] fp1 = PageTextPositions(0, W1, H1), fp2 = PageTextPositions(0, W2, H2);
        int badp = 0; int worstP = -1; float worstD = 0f;
        for (int i = 0; i < fp1.Length && i < fp2.Length; i++)
        {
            float d = Math.Abs(fp2[i] - 2f * fp1[i]);
            if (d > 0.02f) { badp++; if (d > worstD) { worstD = d; worstP = i; } }
        }
        Check("S121b-i every text POSITION on FLIGHT doubles with the panel", badp == 0,
              badp + " of " + fp1.Length + " did not; worst at index " + worstP
              + " (@1280 " + (worstP >= 0 ? fp1[worstP] : 0f)
              + ", @2560 " + (worstP >= 0 ? fp2[worstP] : 0f) + ", off by " + worstD + ")");

        DrawnStepsMatchTheHitRects(W1, H1);
        DrawnStepsMatchTheHitRects(W2, H2);

        // ⭐ THE ABSOLUTE ANCHOR: at RefPanelW every size must be its own unscaled constant, because
        // sc is exactly 1 there. This is the "the 1280 render is byte-identical" half of the
        // DONE-when, stated as something a test can fail.
        Check("S121b-i at RefPanelW the step list draws at exactly Typography.Dense",
              HasSize(t1, Typography.Dense), "no " + Typography.Dense + " px text at 1280");
        Check("S121b-i at RefPanelW the strip captions draw at exactly Typography.Caption",
              HasSize(t1, Typography.Caption), "no " + Typography.Caption + " px text at 1280");
        Check("S121b-i ...and at 2560 those same two are exactly doubled, not merely proportional",
              HasSize(t2, Typography.Dense * 2f) && HasSize(t2, Typography.Caption * 2f),
              "missing " + (Typography.Dense * 2f) + " or " + (Typography.Caption * 2f) + " px text at 2560");

        TappableBandIsProportional(W1, H1);
        TappableBandIsProportional(W2, H2);
    }

    static bool HasSize(float[] a, float want)
    { for (int i = 0; i < a.Length; i++) if (Math.Abs(a[i] - want) < 1e-3f) return true; return false; }

    /// <summary>The step labels the page DREW, against the rects the hit test USES. ⛔ The two are
    /// computed by different code from different starting points, which is the only arrangement in
    /// which this check can fail — and it did, under mutation W6.</summary>
    static void DrawnStepsMatchTheHitRects(int w, int h)
    {
        PageState ps = new PageState(); ps.Valid = true;
        DisplayList dl = new DisplayList(4096);
        Pages.Build(dl, 0, w, h, ps, MapProjection.Default(), 1);
        float sc = Typography.ScaleFor(w);
        float dense = Typography.Dense * sc;

        // the step rows are the only Dense-sized text on this page; two per row (label + time ref)
        float top = float.MaxValue;
        int n = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Text || Math.Abs(c.C - dense) > 1e-3f) continue;
            n++;
            if (c.B < top) top = c.B;
        }
        Check("S121b-i @" + w + " the FLIGHT page actually drew a step list to check",
              n >= 2, "found " + n + " dense-sized text commands");
        if (n < 2) return;

        float rx, ry, rw, rh;
        Pages.StepRect(0, w, h, out rx, out ry, out rw, out rh);
        Check("S121b-i @" + w + " the FIRST step is DRAWN where the hit test expects it",
              Math.Abs(top - ry) < 0.51f, "drawn at " + top + ", StepRect says " + ry);
    }

    /// <summary>
    /// The tappable band is inset from the drawn row. Probe just inside its lower edge and just past
    /// it: a centre-only probe passes whatever the inset is (mutation W10).
    ///
    /// ⚠ AND THE PROBE ASKS WHICH STEP, NOT WHETHER A STEP. A first version checked that the far
    /// probe did NOT return `AckStep` and failed at both widths — correctly, because **the rows
    /// deliberately abut** (`LayoutSweepTest` says so in terms: "drawn on an 18 px pitch with a taller
    /// tappable box, so they deliberately abut"). A tap past row 0's lower edge is not in dead space;
    /// it is in ROW 1. So the question that distinguishes a right inset from a wrong one is which
    /// milestone answers, and getting that wrong is precisely the defect worth catching — the crew
    /// acknowledging the step below the one they touched.
    /// </summary>
    static void TappableBandIsProportional(int w, int h)
    {
        float sc = Typography.ScaleFor(w);
        float x, y, rw, rh;
        Pages.StepRect(0, w, h, out x, out y, out rw, out rh);
        float px = x + rw * 0.5f;
        int id0 = Pages.StepIdAt(0, w, h), id1 = Pages.StepIdAt(1, w, h);

        PageHit inside = Pages.HitTest(0, px, y + rh - 5f * sc, w, h);
        PageHit beyond = Pages.HitTest(0, px, y + rh - 3f * sc, w, h);
        Check("S121b-i @" + w + " a tap just inside row 0's band acknowledges row 0's step",
              inside.Act == PageAct.AckStep && inside.Arg == id0,
              "got " + inside.Act + " arg " + inside.Arg + ", want AckStep " + id0);
        Check("S121b-i @" + w + " ...and a tap past its lower edge belongs to row 1, not row 0",
              beyond.Act == PageAct.AckStep && beyond.Arg == id1,
              "got " + beyond.Act + " arg " + beyond.Arg + ", want AckStep " + id1
              + " — the band's inset is not tracking the panel");
    }

    static float Min(float[] a)
    { float m = float.MaxValue; for (int i = 0; i < a.Length; i++) if (a[i] < m) m = a[i]; return m; }

    static float[] AutoBox(int w, int h)
    { float x, y, rw, rh; Pages.AutoRect(w, h, out x, out y, out rw, out rh); return new float[] { rw, rh }; }

    static float[] MissionBox(int w, int h)
    { float x, y, rw, rh; Pages.MissionRect(0, w, h, out x, out y, out rw, out rh); return new float[] { rw, rh }; }

    static void RectDoubles(string name, float[] a, float[] b)
    {
        for (int i = 0; i < a.Length; i++)
            Check("S121b-i " + name + " dimension " + i + " doubles with the panel",
                  Math.Abs(b[i] - 2f * a[i]) < 1e-3f, "@1280 " + a[i] + " @2560 " + b[i]);
    }

    /// <summary>Build FLIGHT and hand back every text size it emitted, in order.</summary>
    static float[] FlightTextSizes(int w, int h) { return PageTextSizes(0, w, h); }

    /// <summary>Build any legacy page and hand back every text size it emitted, in order. ⛔ Reads the
    /// EMITTED commands — the point is to compare what the page drew, never what the test expected it
    /// to draw. Shared by [[S121b-i]] and [[S121b-ii]].</summary>
    static float[] PageTextSizes(int pageIndex, int w, int h) { return PageTextSizes(pageIndex, w, h, false); }

    /// <summary>⚠ `target` matters for DOCKING and only for DOCKING: without one it draws two words
    /// ("NO TARGET SELECTED") instead of the HUD, and a check run against that would pass while
    /// proving nothing about the page it is supposed to cover.</summary>
    static float[] PageTextSizes(int pageIndex, int w, int h, bool target)
    {
        PageState ps = new PageState(); ps.Valid = true; ps.HasTarget = target;
        DisplayList dl = new DisplayList(8192);
        Pages.Build(dl, pageIndex, w, h, ps, MapProjection.Default(), 1);
        int n = 0;
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Kind == DrawKind.Text) n++;
        float[] outp = new float[n]; int k = 0;
        for (int i = 0; i < dl.Count; i++)
            if (dl.At(i).Kind == DrawKind.Text) outp[k++] = dl.At(i).C;
        return outp;
    }

    /// <summary>The shape both page checks want: same command count, every size exactly doubled, and
    /// an ABSOLUTE anchor at RefPanelW — which is the only thing that catches a scale derived from the
    /// wrong width, since a wrong width that doubles still doubles ([[S121b-i]] mutation W7).</summary>
    static void PageTypeTracksThePanel(string name, int pageIndex) { PageTypeTracksThePanel(name, pageIndex, false); }

    static void PageTypeTracksThePanel(string name, int pageIndex, bool target)
    {
        float[] t1 = PageTextSizes(pageIndex, W1, H1, target), t2 = PageTextSizes(pageIndex, W2, H2, target);
        Check("S121b " + name + " draws the same number of text commands at both widths",
              t1.Length == t2.Length, t1.Length + " vs " + t2.Length);
        Check("S121b " + name + " has real type on it to check", t1.Length > 20,
              "only " + t1.Length + " text commands");
        int bad = 0; int worstI = -1; float worst = 0f;
        for (int i = 0; i < t1.Length && i < t2.Length; i++)
        {
            float d = Math.Abs(t2[i] - 2f * t1[i]);
            if (d > 1e-3f) { bad++; if (d > worst) { worst = d; worstI = i; } }
        }
        Check("S121b every text size on " + name + " doubles with the panel", bad == 0,
              bad + " of " + t1.Length + " did not; worst at index " + worstI
              + " (@1280 " + (worstI >= 0 ? t1[worstI] : 0f)
              + ", @2560 " + (worstI >= 0 ? t2[worstI] : 0f) + ")");
        Check("S121b " + name + " draws at exactly Typography.Caption at RefPanelW",
              HasSize(t1, Typography.Caption), "no " + Typography.Caption + " px text at 1280");
        Check("S121b " + name + " draws at exactly Typography.Dense at RefPanelW",
              HasSize(t1, Typography.Dense), "no " + Typography.Dense + " px text at 1280");
        Check("S121b ...and " + name + " doubles both of those exactly at 2560",
              HasSize(t2, Typography.Caption * 2f) && HasSize(t2, Typography.Dense * 2f),
              "missing " + (Typography.Caption * 2f) + " or " + (Typography.Dense * 2f) + " px at 2560");
        float min1 = Min(t1), min2 = Min(t2);
        Check("S121b the smallest type on " + name + " is the same share of the panel at both widths",
              Math.Abs(min1 / W1 - min2 / W2) < 1e-7f, min1 + "/" + W1 + " vs " + min2 + "/" + W2);

        // ---- ⭐ AND WHERE IT IS DRAWN, NOT ONLY HOW BIG -----------------------------------------
        // ⛔ THE SIZE CHECKS ABOVE MISS HALF THE DEFECT, and mutation found it: leaving a COLUMN
        // position or a bar WIDTH unscaled moves the page around without changing a single type size.
        // The two shipped panels are exactly 2:1 (pinned by TheShippedPanelsAreExactlyTwoToOne), so on
        // a correctly-scaled page EVERY drawn coordinate doubles — a much stronger statement than the
        // sizes doubling, and it costs nothing extra to make.
        float[] p1 = PageTextPositions(pageIndex, W1, H1, target), p2 = PageTextPositions(pageIndex, W2, H2, target);
        int badp = 0; int worstP = -1; float worstD = 0f;
        for (int i = 0; i < p1.Length && i < p2.Length; i++)
        {
            float d = Math.Abs(p2[i] - 2f * p1[i]);
            if (d > 0.02f) { badp++; if (d > worstD) { worstD = d; worstP = i; } }
        }
        Check("S121b every text POSITION on " + name + " doubles with the panel", badp == 0,
              badp + " of " + p1.Length + " did not; worst at index " + worstP
              + " (@1280 " + (worstP >= 0 ? p1[worstP] : 0f)
              + ", @2560 " + (worstP >= 0 ? p2[worstP] : 0f) + ", off by " + worstD + ")");
    }

    /// <summary>Every text command's x and y, interleaved. See the note in PageTypeTracksThePanel:
    /// the panels are exactly 2:1, so every coordinate on a scaled page doubles.</summary>
    static float[] PageTextPositions(int pageIndex, int w, int h) { return PageTextPositions(pageIndex, w, h, false); }

    static float[] PageTextPositions(int pageIndex, int w, int h, bool target)
    {
        PageState ps = new PageState(); ps.Valid = true; ps.HasTarget = target;
        DisplayList dl = new DisplayList(8192);
        Pages.Build(dl, pageIndex, w, h, ps, MapProjection.Default(), 1);
        int n = 0;
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Kind == DrawKind.Text) n++;
        float[] outp = new float[n * 2]; int k = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Text) continue;
            outp[k++] = c.A; outp[k++] = c.B;
        }
        return outp;
    }

    // ---- S121b-ii: THE VEHICLE PAGE -------------------------------------------------------------
    // ⚠ No hit rects of its own — its only controls are the Card tabs, which [[S121a]] already pins at
    // both widths. So this is the type-and-geometry half, read off the render.
    static void VehiclePageTracksThePanel()
    {
        PageTypeTracksThePanel("VEHICLE", 1);

        // ⭐ The alarm dots are a SQUARE beside a word. A fixed square next to type twice the size
        // reads as a different symbol, so the square has to scale with it. Read off the render: the
        // smallest square on the page (the dot) must double.
        Check("S121b-ii the status dot doubles with the panel",
              Math.Abs(SmallestSquare(1, W2, H2) - 2f * SmallestSquare(1, W1, H1)) < 1e-3f,
              "@1280 " + SmallestSquare(1, W1, H1) + " @2560 " + SmallestSquare(1, W2, H2));
        Check("S121b-ii ...and at RefPanelW it is exactly the 10 px it was measured as",
              Math.Abs(SmallestSquare(1, W1, H1) - 10f) < 1e-3f,
              "got " + SmallestSquare(1, W1, H1));
    }

    // ---- S121b-iii: THE LEGACY DOCKING PAGE -----------------------------------------------------
    // ⛔ NOT `Frame58Hud` — that is the live HUD and belongs to [[S154b]]/[[S154c]]. This is the
    // dormant legacy page, and it carries more RefPanelW literals than any other method in `Pages.cs`.
    static void DockingPageTracksThePanel()
    {
        // ⛔ AND `Pages.Build(…, 3, …)` IS NOT THIS PAGE. It routes to `Pages.Docking`, which is two
        // lines calling `DockingPage.Build` — a different FILE, and [[S121d]]'s to pass. A check on
        // page 3 here would be testing someone else's work and would fail until they do it.
        // ⭐ `DockingOld` — the method this split was written around — HAS NO CALLER AT ALL (see its
        // own header). It cannot be reached through `Pages.Build`, so it cannot be rendered, so its
        // type cannot be read off a display list. What CAN be checked is the public HUD geometry it
        // uses, which is the part with a real defect in it, and the placeholder branch.

        // ⭐ THE ONE PIECE OF THIS PAGE THAT IS NOT TYPE OR A COLUMN: the attitude ball's clearance
        // inside the ALIGN sweep. `AlignRingRadius` is a FRACTION of the ring and already tracks the
        // panel; `BallClearance` is 22 RefPanelW pixels subtracted from it, so an unscaled clearance
        // makes the ball too LARGE relative to the ring it sits in — and this page's own header
        // records that gap closing to nine pixels once already, in game.
        float ring1 = Pages.DockingRingHeight(W1, H1), ring2 = Pages.DockingRingHeight(W2, H2);
        Eq("S121b-iii the docking ring keeps its share of the glass", ring2, 2f * ring1, 1e-3f);

        float gap1 = Pages.AlignRingRadius(ring1) - Pages.BallDiameter(ring1, Typography.ScaleFor(W1)) * 0.5f;
        float gap2 = Pages.AlignRingRadius(ring2) - Pages.BallDiameter(ring2, Typography.ScaleFor(W2)) * 0.5f;
        Eq("S121b-iii ...and the ball's clearance inside the ALIGN sweep doubles with it",
           gap2, 2f * gap1, 1e-3f);
        Eq("S121b-iii the clearance at RefPanelW is exactly the 22 px it was measured as",
           gap1, Pages.BallClearance, 1e-3f);
        Check("S121b-iii the ball still fits inside the sweep at both widths",
              Pages.BallDiameter(ring1, Typography.ScaleFor(W1)) > 0f
              && Pages.BallDiameter(ring2, Typography.ScaleFor(W2)) < 2f * Pages.AlignRingRadius(ring2),
              "d1 " + Pages.BallDiameter(ring1, Typography.ScaleFor(W1))
              + " d2 " + Pages.BallDiameter(ring2, Typography.ScaleFor(W2)));
        Check("S121b-iii the 1-argument BallDiameter still delegates at sc = 1",
              Math.Abs(Pages.BallDiameter(ring1) - Pages.BallDiameter(ring1, 1f)) < 1e-4f,
              "got " + Pages.BallDiameter(ring1) + " vs " + Pages.BallDiameter(ring1, 1f));

        // ---- the placeholder branch, which a malformed save can still reach --------------------
        PageTypeCountsOnly("PLACEHOLDER", 9);
    }

    /// <summary>For a page too small to satisfy PageTypeTracksThePanel's "real type on it" bar — the
    /// placeholder draws exactly two words — but which must still double.</summary>
    static void PageTypeCountsOnly(string name, int pageIndex)
    {
        float[] t1 = PageTextSizes(pageIndex, W1, H1), t2 = PageTextSizes(pageIndex, W2, H2);
        float[] q1 = PageTextPositions(pageIndex, W1, H1), q2 = PageTextPositions(pageIndex, W2, H2);
        Check("S121b-iii " + name + " draws the same commands at both widths",
              t1.Length == t2.Length && t1.Length > 0, t1.Length + " vs " + t2.Length);
        int bad = 0;
        for (int i = 0; i < t1.Length && i < t2.Length; i++)
            if (Math.Abs(t2[i] - 2f * t1[i]) > 1e-3f) bad++;
        Check("S121b-iii every " + name + " text size doubles", bad == 0, bad + " of " + t1.Length);
        int badq = 0;
        for (int i = 0; i < q1.Length && i < q2.Length; i++)
            if (Math.Abs(q2[i] - 2f * q1[i]) > 0.02f) badq++;
        Check("S121b-iii every " + name + " text position doubles", badq == 0, badq + " of " + q1.Length);
    }

    /// <summary>The smallest square Rect a page emits — on VEHICLE that is the alarm dot.</summary>
    static float SmallestSquare(int pageIndex, int w, int h)
    {
        PageState ps = new PageState(); ps.Valid = true;
        DisplayList dl = new DisplayList(8192);
        Pages.Build(dl, pageIndex, w, h, ps, MapProjection.Default(), 1);
        float best = float.MaxValue;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Rect) continue;
            if (c.C <= 0f || c.D <= 0f) continue;
            if (Math.Abs(c.C - c.D) > 0.01f) continue;      // square only
            if (c.C < best) best = c.C;
        }
        return best;
    }

    /// <summary>Every FLIGHT control, hit at the centre of where it is DRAWN.</summary>
    static void FlightHitRoundTrip(int w, int h)
    {
        float ax, ay, aw, ah;
        Pages.AutoRect(w, h, out ax, out ay, out aw, out ah);
        PageHit a = Pages.HitTest(0, ax + aw * 0.5f, ay + ah * 0.5f, w, h);
        Check("S121b-i @" + w + " the centre of AUTO SEQUENCE hits ToggleAuto",
              a.Act == PageAct.ToggleAuto, "got " + a.Act);

        float mx, my, mw, mh;
        Pages.MissionRect(0, w, h, out mx, out my, out mw, out mh);
        PageHit m = Pages.HitTest(0, mx + mw * 0.5f, my + mh * 0.5f, w, h);
        Check("S121b-i @" + w + " the centre of the mission button hits Undock",
              m.Act == PageAct.Undock, "got " + m.Act);

        int visible = Pages.StepVisible(w, h);
        int hitOk = 0;
        for (int i = 0; i < visible; i++)
        {
            float x, y, rw, rh;
            Pages.StepRect(i, w, h, out x, out y, out rw, out rh);
            PageHit g = Pages.HitTest(0, x + rw * 0.5f, y + rh * 0.5f, w, h);
            if (g.Act == PageAct.AckStep && g.Arg == Pages.StepIdAt(i, w, h)) hitOk++;
        }
        Check("S121b-i @" + w + " every drawn crew step is hit by a tap at its own centre",
              hitOk == visible, hitOk + " of " + visible);

        // ⚠ and the list must still clear the chrome bar at this width - the defect StepPitchFor exists
        // for, which scaling the pitch could reintroduce if StepTop had been left behind.
        float lx, ly, lrw, lrh;
        Pages.StepRect(visible - 1, w, h, out lx, out ly, out lrw, out lrh);
        Check("S121b-i @" + w + " the last milestone stays clear of the chrome bar",
              ly + lrh <= ChromeBar.TopY(w, h) + 0.01f,
              "last row ends " + (ly + lrh) + ", bar starts " + ChromeBar.TopY(w, h));
    }

    // ---- S121a: THE FIVE SHARED WIDGETS, ACROSS WIDTHS ------------------------------------------
    // These five are what every legacy page draws THROUGH, so a page pass that scales its own literals
    // while calling an unscaled widget just moves the defect one call deep. The checks are the same
    // shape as the rest of this suite: a comparison ACROSS widths, plus the un-passed caller rendering
    // exactly as before, which is the property that makes landing this safe.
    static void SharedWidgetsTrackThePanel()
    {
        float sc1 = Typography.ScaleFor(W1), sc2 = Typography.ScaleFor(W2);

        // ---- Gauge.ValueSize: THE ONE THAT WAS CLAMPED, NOT MERELY UN-SCALED --------------------
        // ⛔ The defect in one line: a dial whose radius DOUBLES produced the same 28 px number, so its
        // share of the panel halved. The old 1-arg form is kept and must still do exactly that, because
        // that is what "un-passed callers are unchanged" means.
        Eq("S121a Gauge.ValueSize at RefPanelW is unchanged by the overload",
           Gauge.ValueSize(115.73f, sc1), Gauge.ValueSize(115.73f), 1e-4f);
        Check("S121a the OLD Gauge.ValueSize still clamps a doubled radius to the 1280 ceiling — the defect, pinned",
              Math.Abs(Gauge.ValueSize(235.20f) - Typography.Value) < 1e-4f,
              "got " + Gauge.ValueSize(235.20f) + ", Typography.Value " + Typography.Value);
        Check("S121a ...and the SCALED form lets it follow the panel instead",
              Gauge.ValueSize(235.20f, sc2) > Typography.Value + 1f,
              "got " + Gauge.ValueSize(235.20f, sc2));
        // the real dial: radius 115.73 at 1280, 235.20 at 2560 (measured off Pages.cs' own expressions)
        Eq("S121a the shipped dial's number is exactly twice the size on a twice-as-wide panel",
           Gauge.ValueSize(235.20f, sc2), 2f * Gauge.ValueSize(115.73f, sc1), 1e-3f);
        Check("S121a ...which is the same share of the panel at both widths",
              Math.Abs(Gauge.ValueSize(115.73f, sc1) / W1 - Gauge.ValueSize(235.20f, sc2) / W2) < 1e-7f,
              Gauge.ValueSize(115.73f, sc1) / W1 + " vs " + Gauge.ValueSize(235.20f, sc2) / W2);
        // the floor half of the clamp is a ratio too - R-02's whole point
        Eq("S121a the scaled lower bound IS Typography.MinFor, not a second floor",
           Gauge.ValueSize(0f, sc2), Typography.MinFor(W2), 1e-4f);

        // ---- the four text widgets: every size doubles, nothing at RefPanelW moves --------------
        WidgetRow("Gauge.Bar", GaugeBarSizes(sc1), GaugeBarSizes(sc2));
        WidgetRow("NumericReadout.Paired", PairedSizes(sc1), PairedSizes(sc2));
        WidgetRow("StatusIndicator.Badge", BadgeSizes(sc1), BadgeSizes(sc2));
        WidgetRow("GateCard", GateSizes(W1), GateSizes(W2));

        // ---- GateCard: THE DRAW AND THE HIT TEST MUST AGREE AT BOTH WIDTHS ----------------------
        // ⛔ This is the check that matters most in this split. Scaling a card's drawing without its
        // hit test is QC H-04 again ([[S108]]): a control painted where the touch test does not look.
        // Both entry points derive sc from the same w, so this is true by construction — and pinned
        // here so a later refactor cannot quietly separate them.
        GateCardHitFollowsTheDraw(W1, H1);
        GateCardHitFollowsTheDraw(W2, H2);

        // ---- Card: the tab strip is drawn by Build and hit by HitTest, same story ---------------
        CardTabsAgree(W1, H1);
        CardTabsAgree(W2, H2);

        // the two quantities each owned by ONE function, compared across widths directly (see the
        // note in GateCardHitFollowsTheDraw: a hit test cannot catch a defect it moves with)
        Eq("S121a GateCard's checklist row pitch doubles with the panel",
           GateRowPitch(W2, H2), 2f * GateRowPitch(W1, H1), 1e-3f);
        Eq("S121a Card's tab height doubles with the panel",
           CardTabHeight(W2, H2), 2f * CardTabHeight(W1, H1), 0.5f);
        Check("S121a Card's notch fractions are NOT scaled — they already track the panel",
              CardBodyWidthFrac(W1, H1) > 0.90f && Math.Abs(CardBodyWidthFrac(W1, H1) - CardBodyWidthFrac(W2, H2)) < 0.006f,
              "body/panel " + CardBodyWidthFrac(W1, H1) + " vs " + CardBodyWidthFrac(W2, H2));
    }

    // ---- ⛔ THESE READ THE WIDGET, THEY DO NOT RESTATE IT ----------------------------------------
    // A first version of these three helpers returned `Typography.Caption * sc` and friends computed
    // HERE, then asserted the 2560 list was twice the 1280 list. That passes whatever the widgets do:
    // it is `Caption * 2 == 2 * (Caption * 1)`, arithmetic about the test's own expression, and it
    // would have gone on passing with every `* sc` deleted from the source. ⭐ So each helper now
    // RENDERS the widget into a DisplayList and reads the sizes and positions back out of the emitted
    // commands. What is compared is what the widget actually drew.
    static float[] TextSizes(DisplayList dl)
    {
        int n = 0;
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Kind == DrawKind.Text) n++;
        float[] outp = new float[n * 2];
        int k = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Text) continue;
            outp[k++] = c.C;    // pixelSize
            outp[k++] = c.B;    // the baseline it was placed at - the half a size alone cannot catch
        }
        return outp;
    }

    static float[] GaugeBarSizes(float sc)
    {
        DisplayList dl = new DisplayList(64);
        // ⚠ `width` is the CALLER's and is passed already scaled, exactly as a passed page would.
        Gauge.Bar(dl, 0f, 0f, 400f * sc, "CAPTION", "12.3", "m/s", 0.5, DragonPalette.Go, sc);
        float[] t = TextSizes(dl);
        // plus the track rect the bar draws under the type: its y and its thickness
        float by = 0f, bh = 0f;
        for (int i = 0; i < dl.Count; i++)
            if (dl.At(i).Kind == DrawKind.Rect) { by = dl.At(i).B; bh = dl.At(i).D; break; }
        float[] outp = new float[t.Length + 2];
        for (int i = 0; i < t.Length; i++) outp[i] = t[i];
        outp[t.Length] = by; outp[t.Length + 1] = bh;
        return outp;
    }

    static float[] PairedSizes(float sc)
    {
        DisplayList dl = new DisplayList(32);
        NumericReadout.Paired(dl, 0f, 0f, "CAPTION", "1.0", "2.0", sc);
        return TextSizes(dl);
    }

    static float[] BadgeSizes(float sc)
    {
        DisplayList dl = new DisplayList(32);
        // ⭐ The box is the caller's and IS scaled, which is the case the latent centring bug needs:
        // an unscaled `Typography.Caption` inside a scaled `h` is what pushes the word off centre.
        StatusIndicator.Badge(dl, 0f, 0f, 200f * sc, 60f * sc, "AUTO", DragonPalette.Go, sc);
        float[] t = TextSizes(dl);
        // ⚠ There is no DrawKind.Box: DisplayList.Box expands into FOUR Rects (top, bottom, left,
        // right), so the frame's thickness is the HEIGHT of the top edge — command 1, straight after
        // the plate's own background fill. Read off the emitted commands, not from the source.
        float stroke = (dl.Count > 1 && dl.At(1).Kind == DrawKind.Rect) ? dl.At(1).D : 0f;
        float[] outp = new float[t.Length + 1];
        for (int i = 0; i < t.Length; i++) outp[i] = t[i];
        outp[t.Length] = stroke;
        return outp;
    }
    // ⚠ The card's RAW size, deliberately NOT divided by sc. A first version returned `cw / sc` and
    // reported 640 at both widths — which is true and proves nothing, because dividing by the scale
    // normalises away the exact quantity under test. The claim is that the card itself doubles.
    static float[] GateSizes(int w)
    {
        float x, y, cw, ch;
        GateCard.CardRect(w, w * 703 / 1280, 4, out x, out y, out cw, out ch);
        return new float[] { cw, ch };
    }

    static void WidgetRow(string name, float[] at1280, float[] at2560)
    {
        Check("S121a " + name + " reports the same number of sizes at both widths",
              at1280.Length == at2560.Length, at1280.Length + " vs " + at2560.Length);
        for (int i = 0; i < at1280.Length && i < at2560.Length; i++)
            Check("S121a " + name + " size " + i + " doubles with the panel",
                  Math.Abs(at2560[i] - 2f * at1280[i]) < 1e-3f,
                  "@1280 " + at1280[i] + " @2560 " + at2560[i]);
    }

    static void GateCardHitFollowsTheDraw(int w, int h)
    {
        const int items = 4;
        float x, y, cw, ch;
        GateCard.CardRect(w, h, items, out x, out y, out cw, out ch);
        float sc = Typography.ScaleFor(w);

        // the centre of every drawn control must hit that control, and nothing else
        for (int i = 0; i < items; i++)
        {
            float rx, ry, rw, rh;
            GateCard.ItemRect(i, x, y, cw, sc, out rx, out ry, out rw, out rh);
            GateHit g = GateCard.HitTest(rx + rw * 0.5f, ry + rh * 0.5f, w, h, items);
            Check("S121a @" + w + " the centre of item " + i + " hits item " + i,
                  g.Kind == GateHitKind.Item && g.Item == i, "got " + g.Kind + " " + g.Item);
        }
        GateHitKind[] want = { GateHitKind.Go, GateHitKind.NoGo, GateHitKind.Abort };
        for (int b = 0; b < 3; b++)
        {
            float bx, by, bw, bh;
            GateCard.ButtonRect(b, x, y, cw, ch, sc, out bx, out by, out bw, out bh);
            GateHit g = GateCard.HitTest(bx + bw * 0.5f, by + bh * 0.5f, w, h, items);
            Check("S121a @" + w + " the centre of button " + b + " hits " + want[b],
                  g.Kind == want[b], "got " + g.Kind);
            Check("S121a @" + w + " button " + b + " is inside its own card",
                  bx >= x && bx + bw <= x + cw + 0.01f && by >= y && by + bh <= y + ch + 0.01f,
                  "btn " + bx + "," + by + " " + bw + "x" + bh + " card " + x + "," + y + " " + cw + "x" + ch);
        }
        // ⚠ and the card must not be a quarter of the panel at 2560 — that was MaxW's unscaled clamp
        Check("S121a @" + w + " the card keeps its share of the panel",
              cw / w > 0.45f, "card " + cw + " on panel " + w + " = " + (cw / w));

        // ---- ⛔ CONSISTENCY IS NOT CORRECTNESS, AND THAT GAP LET TWO MUTATIONS THROUGH -------------
        // The checks above find the control's centre with ItemRect and then hit-test it — so a
        // mutation that moves BOTH (dropping the scale inside ItemRect, say) stays self-consistent and
        // passes, while the rows march off the bottom of the card. Mutation V10 did exactly that and
        // survived. So the rows are also checked against something they do NOT share: the card that
        // has to contain them, and the buttons they must not reach.
        float lastBottom = 0f;
        {
            float rx, ry, rw, rh;
            GateCard.ItemRect(items - 1, x, y, cw, sc, out rx, out ry, out rw, out rh);
            lastBottom = ry + rh;
        }
        float btnTop;
        {
            float bx, by, bw, bh;
            GateCard.ButtonRect(0, x, y, cw, ch, sc, out bx, out by, out bw, out bh);
            btnTop = by;
        }
        Check("S121a @" + w + " the last checklist row stays clear of the buttons",
              lastBottom <= btnTop + 0.01f, "last row ends " + lastBottom + ", buttons start " + btnTop);
        Check("S121a @" + w + " every checklist row is inside the card",
              lastBottom <= y + ch + 0.01f, "last row ends " + lastBottom + ", card ends " + (y + ch));
    }

    /// <summary>The row pitch is the thing ItemRect owns alone, so it is compared across widths
    /// directly rather than through a hit test that would move with it.</summary>
    static float GateRowPitch(int w, int h)
    {
        float x, y, cw, ch;
        GateCard.CardRect(w, h, 4, out x, out y, out cw, out ch);
        float sc = Typography.ScaleFor(w);
        float x0, y0, w0, h0, x1, y1, w1, h1;
        GateCard.ItemRect(0, x, y, cw, sc, out x0, out y0, out w0, out h0);
        GateCard.ItemRect(1, x, y, cw, sc, out x1, out y1, out w1, out h1);
        return y1 - y0;
    }

    /// <summary>Card's tab height, which TabRect owns alone — the same argument as GateRowPitch.</summary>
    static float CardTabHeight(int w, int h)
    {
        float tx, ty, tw, th;
        Card.TabRect(0, 3, w, h, out tx, out ty, out tw, out th);
        return th;
    }

    static void CardTabsAgree(int w, int h)
    {
        string[] tabs = { "A", "B", "C" };
        for (int i = 0; i < tabs.Length; i++)
        {
            float tx, ty, tw, th;
            Card.TabRect(i, tabs.Length, w, h, out tx, out ty, out tw, out th);
            int hit = Card.HitTest(tx + tw * 0.5f, ty + th * 0.5f, tabs.Length, w, h);
            Check("S121a @" + w + " the centre of Card tab " + i + " hits tab " + i,
                  hit == i, "got " + hit);
        }
    }

    static float CardBodyWidthFrac(int w, int h)
    {
        float x, y, bw, bh;
        Card.Body(w, h, out x, out y, out bw, out bh);
        return bw / w;
    }

    static void ChromeBarTracksThePanel()
    {
        // The geometry. The bar is a FRACTION of the glass, not a pixel count.
        Eq("the bar is the measured 64 px at the width it was measured at",
           ChromeBar.HeightFor(W1), ChromeBar.Height, 1e-4f);
        Eq("...and doubles with the panel", ChromeBar.HeightFor(W2), 2f * ChromeBar.Height, 1e-4f);
        Check("...so it is the same fraction of the panel height at both",
              Math.Abs(ChromeBar.HeightFor(W1) / H1 - ChromeBar.HeightFor(W2) / H2) < 1e-6f,
              ChromeBar.HeightFor(W1) / H1 + " vs " + ChromeBar.HeightFor(W2) / H2);
        Eq("the bar still sits flush on the bottom edge at 1280",
           ChromeBar.TopY(W1, H1) + ChromeBar.HeightFor(W1), (float)H1, 1e-4f);
        Eq("...and at 2560", ChromeBar.TopY(W2, H2) + ChromeBar.HeightFor(W2), (float)H2, 1e-4f);

        // The page links: same rectangles, same fraction of the panel, and hit-testing follows them.
        for (int i = 0; i < ChromeBar.PageNames.Length; i++)
        {
            float ax, ay, aw, ah, bx, by, bw, bh;
            ChromeBar.LinkRect(i, W1, H1, out ax, out ay, out aw, out ah);
            ChromeBar.LinkRect(i, W2, H2, out bx, out by, out bw, out bh);
            Eq("link " + i + "'s x doubles with the panel", bx, ax * 2f, 0.01f);
            Eq("link " + i + "'s width doubles with the panel", bw, aw * 2f, 0.01f);
            Eq("link " + i + "'s height doubles with the panel", bh, ah * 2f, 0.01f);
            // ⛔ The ChromeBar.LinkRect rule: one source for drawing AND hitting. If the rects scale
            // and HitTest does not, every tab becomes unpressable at the shipped width.
            Check("...and a touch at link " + i + "'s centre still selects it at 2560",
                  ChromeBar.HitTest(bx + bw * 0.5f, by + bh * 0.5f, W2, H2) == i,
                  "got " + ChromeBar.HitTest(bx + bw * 0.5f, by + bh * 0.5f, W2, H2));
        }

        // The type. Every label the bar draws must double when the panel does - this is the half that
        // makes the bar legible, as opposed to merely correctly placed.
        DisplayList a = BuildBar(W1, H1);
        DisplayList b = BuildBar(W2, H2);
        Check("the bar draws the same page at both widths, command for command",
              a.Count == b.Count && a.Count > 0, "1280 " + a.Count + " cmds, 2560 " + b.Count + " cmds");
        if (a.Count == b.Count)
        {
            int texts = 0, worst = -1; float worstA = 0f, worstB = 0f;
            for (int i = 0; i < a.Count; i++)
            {
                if (a.At(i).Kind != DrawKind.Text) continue;
                texts++;
                float ta = a.At(i).C, tb = b.At(i).C;
                if (Math.Abs(tb - ta * 2f) > 0.01f && worst < 0)
                { worst = i; worstA = ta; worstB = tb; }
            }
            Check("the bar draws text at all", texts > 0, "found " + texts);
            Check("EVERY chrome-bar label doubles when the panel doubles", worst < 0,
                  worst < 0 ? "" : "'" + a.At(worst).Str + "' is " + worstA + " px @1280 and "
                      + worstB + " px @2560 - it should be " + (worstA * 2f));
        }

        // ...and every label stays INSIDE the bar it is drawn in. Scaling the type without scaling
        // the box is S117's own trap, and on this component it would push MET and STATE off the
        // bottom edge of the panel entirely.
        foreach (int[] wh in new[] { new[] { W1, H1 }, new[] { W2, H2 } })
        {
            DisplayList d = BuildBar(wh[0], wh[1]);
            float top = ChromeBar.TopY(wh[0], wh[1]);
            int outside = 0; string first = null;
            for (int i = 0; i < d.Count; i++)
            {
                DrawCmd t = d.At(i);
                if (t.Kind != DrawKind.Text) continue;
                if (t.B >= top && t.B + t.C <= wh[1]) continue;
                outside++; if (first == null) first = "'" + t.Str + "' at y " + t.B + " size " + t.C;
            }
            Check("every label sits inside the bar at " + wh[0] + "x" + wh[1], outside == 0,
                  outside + " outside, first " + first);
        }
    }

    // ---- 7. S118: THE TWO SHIPPED PANELS ARE EXACTLY 2:1, SO EVERY FIGURE DOUBLES ---------------
    // S118 had to restate six comments' pixel figures at the current shipped width. Rather than
    // double six sets of numbers by hand and hope, this pins the PROPERTY that makes the doubling
    // exact - and it covers every figure in every one of those comments at once.
    //
    // 2560 = 2 x 1280 and 1406 = 2 x 703, so for a page laid out by CoverPage's idiom - sc = h/RefH,
    // with the horizontal slack `extra = w - RefW*sc` put into one gap - BOTH sc and extra double,
    // and therefore so does every coordinate and every size the page emits.
    //
    // ⛔ THIS IS NOT A TAUTOLOGY AND IT IS NOT GUARANTEED. It fails the moment any page mixes a
    // device-pixel constant into that arithmetic, which is exactly the R-02 family: an un-scaled
    // literal shows up here as a command that did NOT double. So this doubles as a standing sweep
    // for new instances of R-02 on the two pages that carry the most Figma-derived geometry.
    static void TheShippedPanelsAreExactlyTwoToOne()
    {
        Eq("the shipped width is exactly twice the reference width", (float)W2, W1 * 2f, 0f);
        Eq("...and the height with it, so the aspect is identical", (float)H2, H1 * 2f, 0f);

        PageState ps = new PageState(); ps.Valid = true;
        for (int which = 0; which < 2; which++)
        {
            string what = which == 0 ? "CoverPage" : "NavOrbitPlotPage";
            DisplayList a = new DisplayList(4096), b = new DisplayList(4096);
            if (which == 0)
            {
                CoverPage.Build(a, W1, H1, ps, MapProjection.Default(), 1, CoverPage.CoverCam.Map);
                CoverPage.Build(b, W2, H2, ps, MapProjection.Default(), 1, CoverPage.CoverCam.Map);
            }
            else
            {
                NavOrbitPlotPage.Build(a, W1, H1, ps);
                NavOrbitPlotPage.Build(b, W2, H2, ps);
            }
            Check(what + ": the same page is drawn at both widths", a.Count == b.Count && a.Count > 0,
                  a.Count + " vs " + b.Count);
            if (a.Count != b.Count) continue;

            int bad = 0; string first = null;
            for (int i2 = 0; i2 < a.Count; i2++)
            {
                DrawCmd x = a.At(i2), y = b.At(i2);
                float[] xs = { x.A, x.B, x.C, x.D }, ys = { y.A, y.B, y.C, y.D };
                for (int k = 0; k < 4; k++)
                    if (Math.Abs(ys[k] - xs[k] * 2f) > 0.02f)
                    {
                        bad++;
                        if (first == null)
                            first = x.Kind + " field " + k + ": " + xs[k] + " -> " + ys[k]
                                  + " (want " + (xs[k] * 2f) + ") " + x.Str + x.AssetKey;
                        break;
                    }
            }
            Check(what + ": EVERY command doubles exactly when the panel doubles", bad == 0,
                  bad + " of " + a.Count + " did not. First: " + first
                      + "  - an un-scaled device-pixel constant, i.e. a new R-02.");
        }
    }

    /// <summary>Every distinct thickness among the Cover's thin Rects, sorted, as a string. A Box is
    /// four Rects whose stroke is one side, so a stroke rule shows up here as a thin Rect. 12 px is a
    /// generous ceiling for "this is a rule, not a panel" and is well clear of the next thing up.</summary>
    static string ThinRectThicknesses(int w, int h)
    {
        PageState ps = new PageState(); ps.Valid = true;
        System.Collections.Generic.SortedSet<float> set = new System.Collections.Generic.SortedSet<float>();
        // ⛔ ALL THREE CAMERA VIEWS, not just the default. Three of the four draws this check exists
        // for - the map well's box, the NEXT VIEW pill and the d-pad button box - are only reached
        // under CoverCam.Map, so a sweep of the default Earth view passes while the defect is live.
        // Mutation J proved exactly that: the first version of this check did not fail when the float
        // rule was put back, because it never rendered the page that draws it.
        foreach (CoverPage.CoverCam cam in new[] { CoverPage.CoverCam.Earth, CoverPage.CoverCam.Map, CoverPage.CoverCam.Capsule })
        {
            DisplayList d = new DisplayList(CoverPage.Commands);
            CoverPage.Build(d, w, h, ps, MapProjection.Default(), 1, cam);
            for (int i = 0; i < d.Count; i++)
            {
                DrawCmd t = d.At(i);
                if (t.Kind != DrawKind.Rect) continue;
                float mn = Math.Min(t.C, t.D);
                if (mn <= 12f) set.Add((float)Math.Round(mn, 4));
            }
        }
        string[] parts = new string[set.Count];
        int k = 0;
        foreach (float v in set) parts[k++] = v.ToString("0.####");
        return string.Join(", ", parts);
    }

    static DisplayList BuildBar(int w, int h)
    {
        ChromeState cs = new ChromeState();
        cs.Met = "01:23:45"; cs.VehicleState = "ORBIT"; cs.LinkName = "COM1";
        cs.LinkTimer = "00:42"; cs.LinkUp = true; cs.SelectedPage = 2;
        DisplayList dl = new DisplayList(ChromeBar.Commands);
        ChromeBar.Build(dl, w, h, cs);
        return dl;
    }

    // ---- 8. R-01: THE CENSUS, AND THE RATCHET UNDER IT (S153) -----------------------------------
    // QC R-01 sampled SEVENTEEN elements across nine pages and found all seventeen under the floor.
    // ⚠ IT UNDERSTATED THE SCOPE BY A FACTOR OF FIFTY. Walked exhaustively over every non-placeholder
    // page at the shipped size, the real figure is 868 of 914 text draws - and only 46 elements in the
    // whole Figma-era build clear the floor. QC's seventeen were a sample, correctly labelled as one;
    // this is the population.
    //
    // ⭐ WHAT THIS CHECK IS FOR IS THE OTHER DIRECTION. The 868 are owned by the split lines S153a-f
    // and will come down page by page. What must not happen in the meantime is a page quietly gaining
    // a NEW sub-floor element while those lines are outstanding - which is exactly what the six
    // R-01-gated content lines (S126/S136/S137/S138/S145/S147) would do, since each ADDS text to a
    // page in this table. So the baseline below is a RATCHET: it may fall, never rise.
    //
    // ⛔ AN ENTRY HERE IS A DEFECT ON RECORD, NOT A PARDON - the same standing as build.py's KNOWN_DEAD
    // list. The number is what the page measured on 2026-09-06; a split line that raises type LOWERS
    // its number, and the check says so out loud when it does.
    //
    // ⚠ THE COUNTS ARE FIXTURE-RELATIVE, and that is stated rather than hidden: they are taken against
    // Leo(), the same orbit fixture the rest of this suite uses. A change that makes a page draw MORE
    // ROWS of legitimately-sized text will trip this, and the honest response is to re-baseline in the
    // owning register line - not to widen the tolerance.
    // ⚠ AMENDED 2026-09-06 BY [[S126]], THE VERY NEXT LINE TO USE IT, and the amendment is the
    // interesting part. The first form counted only elements below the GLANCEABLE floor and failed on
    // any rise. S126 then added two captions at `DenseDesignFor` - which is where the owner's policy
    // puts a static label, and the ruling names "pad captions" as exactly that - and the guard failed
    // the build for doing the right thing. ⛔ A ONE-FLOOR RATCHET CANNOT POLICE A TWO-FLOOR POLICY.
    //
    // So there are two numbers per page and they are NOT the same kind of rule:
    //   `BelowDense` is the HARD one - nothing may EVER sit below the static-reference floor, whatever
    //       it is. A rise fails the build.
    //   `Below` is SOFT - it may rise only when `BelowDense` did NOT, i.e. the new element landed in
    //       the Dense..floor band where the policy permits static content to live. That is reported,
    //       loudly, with the page named; it is not silently accepted and it is not a failure either.
    // ⚠ What this deliberately CANNOT catch is a LIVE element placed at the static floor. Telling
    // those apart needs a per-element classification, which S153 left to the per-page split lines;
    // this guard covers the half that is knowable without one, and says so rather than implying more.
    struct FloorBaseline { public UiPage Page; public int Below; public int BelowDense; }

    static readonly FloorBaseline[] Baseline = {
        B(UiPage.Cover,             26, 23),   // S153a  ⚠ 26 not 24: [[S126]] added two STATIC captions
        B(UiPage.Menu,              24, 24),   // S153f
        // ⭐ S147 PUT THIS PAGE IN THE CENSUS. `UiPage.Cabin` is a flat frame that drew NO text at all,
        // so it was legitimately absent from this table - until the bottom bar started printing
        // CURRENT STATE on every page. Its one text draw is at the glanceable floor, so both counts
        // are 0, and the ratchet caught the omission the moment it appeared rather than letting a
        // page slip out of the census.
        B(UiPage.Cabin,              0,  0),   // S153f (the frame itself is [[S136]]'s)
        B(UiPage.Hud,                2,  2),   // S153f - MarginAffordance's MANUAL/DOCKING, also QC H-06
        B(UiPage.Audio,             12, 10),   // S153f
        B(UiPage.AudioVideo,         9,  7),   // S153f
        B(UiPage.Procedure,         37, 35),   // S153c - the same page file as VrioTest (S110)
        B(UiPage.VrioTest,          37, 35),   // S153c
        B(UiPage.SuitCheck,         47, 46),   // S153c
        B(UiPage.ManualChute,       58, 56),   // S153c
        B(UiPage.DeorbitBurnPrep,   21, 20),   // S153c
        B(UiPage.EntryProcedure,     8,  7),   // S153c
        B(UiPage.Vehicle,           80, 79),   // S153b
        B(UiPage.VehicleMech,       32, 31),   // S153b
        B(UiPage.VehicleCrew,       43, 42),   // S153b
        B(UiPage.VehiclePropulsion,114,113),   // S153b - the worst single page in the build
        B(UiPage.VehiclePower,      43, 42),   // S153b
        B(UiPage.VehicleAvionics,   43, 42),   // S153b
        B(UiPage.VehicleGnc,        43, 42),   // S153b
        B(UiPage.VehicleThermal,    43, 42),   // S153b
        B(UiPage.SystemsTree,       31, 30),   // S153d
        B(UiPage.SystemsPid,        42, 41),   // S153d
        B(UiPage.Docking,           39, 31),   // S153e
        B(UiPage.Rendezvous,         8,  1),   // S153e ⭐ 7 of its 8 already sit in the Dense..floor band
        B(UiPage.Ascent,            17, 16),   // S153e
        B(UiPage.NavOrbitPlot,      11,  8),   // S153e
    };

    static FloorBaseline B(UiPage p, int below, int belowDense)
    { FloorBaseline f = new FloorBaseline(); f.Page = p; f.Below = below; f.BelowDense = belowDense; return f; }

    /// <summary>Flip to true for one run to dump the baseline table, then flip back. It exists
    /// because a table of 25 pairs is not something to type from a screenshot.</summary>
    const bool PrintBaselines = false;

    static void R01Census()
    {
        float floor = Typography.MinFor(W2), dense = Typography.DenseFor(W2);
        int tT = 0, tOk = 0, tStatic = 0, tBelowDense = 0, tBelow = 0, regressed = 0, improved = 0;
        int covered = 0;

        Console.WriteLine("  ---- R-01 census @" + W2 + "x" + H2 + ": floor " + floor
                          + " px, static-reference floor " + dense + " px ----");
        foreach (UiPage up in (UiPage[])Enum.GetValues(typeof(UiPage)))
        {
            if (FigmaUI.IsPlaceholder(up)) continue;
            DisplayList dl = new DisplayList(1200);
            FigmaUI.Build(dl, up, W2, H2, Leo(), MapProjection.Default());
            int n = 0, ok = 0, st = 0, bd = 0;
            for (int i = 0; i < dl.Count; i++)
            {
                DrawCmd c = dl.At(i);
                if (c.Kind != DrawKind.Text) continue;
                n++;
                if (c.C >= floor) ok++; else if (c.C >= dense) st++; else bd++;
            }
            if (n == 0) continue;
            tT += n; tOk += ok; tStatic += st; tBelowDense += bd; tBelow += (n - ok);
            if (PrintBaselines)
                Console.WriteLine(string.Format("        B(UiPage.{0}, {1}, {2}),", up, n - ok, bd));

            int want = -1, wantD = -1;
            for (int i = 0; i < Baseline.Length; i++)
                if (Baseline[i].Page == up)
                { want = Baseline[i].Below; wantD = Baseline[i].BelowDense; covered++; }

            Check("R-01: " + up + " is in the floor baseline table", want >= 0,
                  "a page that draws text and is not listed cannot be ratcheted - add it, owned by a "
                  + "split line");
            if (want < 0) continue;

            // ---- THE HARD RATCHET: nothing may ever drop below the STATIC-reference floor ----
            if (bd > wantD)
            {
                regressed++;
                Check("R-01: " + up + " gained text below even the static floor", false,
                      "baseline " + wantD + ", now " + bd + " - a new element below " + dense
                      + " px, which no content type is allowed to be. Raise it.");
                for (int i = 0; i < dl.Count; i++)
                {
                    DrawCmd c = dl.At(i);
                    if (c.Kind == DrawKind.Text && c.C < dense)
                        Console.WriteLine(string.Format("        {0,6:0.0}px {1,4:0}%  {2}",
                            c.C, 100f * c.C / floor, c.Str));
                }
            }
            // ---- THE SOFT ONE: a rise is allowed ONLY into the Dense..floor band ----
            else if (n - ok > want)
            {
                Console.WriteLine(string.Format(
                    "    STATIC+   {0,-18} below-floor {1,3} -> {2,3}, below-Dense unchanged at {3,3}"
                    + "   (new STATIC-reference text; re-baseline in the owning line)",
                    up, want, n - ok, bd));
            }
            else if (n - ok < want)
            {
                improved++;
                Console.WriteLine(string.Format(
                    "    IMPROVED  {0,-18} baseline {1,3} -> {2,3}   lower it in the owning split line",
                    up, want, n - ok));
            }
        }

        Check("R-01: every baselined page was actually walked", covered == Baseline.Length,
              "table has " + Baseline.Length + ", walked " + covered);
        Console.WriteLine(string.Format(
            "    {0} text draws: {1} clear the floor, {2} in the Dense..floor band (static-reference "
            + "only), {3} below even Dense", tT, tOk, tStatic, tBelowDense));
        Console.WriteLine("    " + tBelow + " below the floor, " + regressed + " page(s) regressed, "
                          + improved + " improved");
    }

    // ---- 9. THE OWNER'S TWO-FLOOR POLICY, AS ARITHMETIC (S153) ----------------------------------
    static void TheTwoFloorsAreBothRatios()
    {
        // Dense's ratio form, exactly as MinFor is Min's.
        Eq("DenseFor(RefPanelW) is exactly Dense", Typography.DenseFor(Typography.RefPanelW),
           Typography.Dense, 1e-4f);
        Eq("the static-reference floor at the shipped 2560 is 24 px", Typography.DenseFor(W2), 24f, 1e-4f);
        Check("the static floor is BELOW the glanceable floor, at every width",
              Typography.DenseFor(W1) < Typography.MinFor(W1)
              && Typography.DenseFor(W2) < Typography.MinFor(W2), "");
        Check("...and it is the same fraction of it at both widths",
              Math.Abs(Typography.DenseFor(W1) / Typography.MinFor(W1)
                       - Typography.DenseFor(W2) / Typography.MinFor(W2)) < 1e-6f,
              "@1280 " + (Typography.DenseFor(W1) / Typography.MinFor(W1))
              + ", @2560 " + (Typography.DenseFor(W2) / Typography.MinFor(W2)));

        // ⭐ THE PROPERTY THE SPLIT LINES ACTUALLY NEED, and the one R-02 says a bare 48 could never
        // have: the required DESIGN size is the same number at BOTH widths, because the design frame
        // and the floor scale together. A page that writes MinDesignFor(w, sc) is correct at any cfg;
        // a page that writes 48 is correct at exactly one.
        float sc1 = (float)H1 / 2112f, sc2 = (float)H2 / 2112f;
        Eq("the LIVE design floor is 48.07 at the shipped size",
           Typography.MinDesignFor(W2, sc2), 48.068f, 0.01f);
        Eq("the STATIC design floor is 36.05 at the shipped size",
           Typography.DenseDesignFor(W2, sc2), 36.051f, 0.01f);
        Check("the LIVE design floor is the SAME number at 1280 and 2560",
              Math.Abs(Typography.MinDesignFor(W1, sc1) - Typography.MinDesignFor(W2, sc2)) < 0.01f,
              "@1280 " + Typography.MinDesignFor(W1, sc1) + ", @2560 " + Typography.MinDesignFor(W2, sc2));
        Check("...and so is the STATIC one",
              Math.Abs(Typography.DenseDesignFor(W1, sc1) - Typography.DenseDesignFor(W2, sc2)) < 0.01f,
              "@1280 " + Typography.DenseDesignFor(W1, sc1) + ", @2560 " + Typography.DenseDesignFor(W2, sc2));

        // A degenerate frame scale falls back to the panel floor rather than dividing by zero - the
        // same contract MinFor(0) has.
        Eq("a zero frame scale falls back to the panel floor",
           Typography.MinDesignFor(W2, 0f), Typography.MinFor(W2), 1e-4f);
        Eq("...and the static one likewise",
           Typography.DenseDesignFor(W2, 0f), Typography.DenseFor(W2), 1e-4f);

        // ⚠ THE MEASUREMENT THAT STOPS THE POLICY BEING MISREAD. "Permit static tables to sit at
        // Dense" is a RAISE for the owner's own named examples, not a pardon: the Cover's reference
        // rows draw Z(26) and the docking pad captions Z(22), both BELOW the 24 px static floor.
        Check("the Cover's reference rows are below even the STATIC floor",
              26f * sc2 < Typography.DenseFor(W2), "Z(26) = " + (26f * sc2) + " px");
        Check("the docking pad captions likewise",
              22f * sc2 < Typography.DenseFor(W2), "Z(22) = " + (22f * sc2) + " px");
    }

    static DisplayList BuildNav(int w, int h, MapView view)
    {
        DisplayList dl = new DisplayList(Pages.Commands + 64);
        NavPage.Build(dl, w, h, Leo(), view);
        return dl;
    }

    /// <summary>An RSS-Earth low orbit - the same shape PageTest flies its NAV checks against, so a
    /// failure here is about the scale and not about an exotic conic.</summary>
    static PageState Leo()
    {
        PageState s = new PageState();
        s.Valid = true;
        s.Regime = FlightRegime.Space;
        s.BodyRadiusM = 6371000.0; s.AtmosphereDepthM = 140000.0;
        s.ApogeeM = 202000.0; s.PerigeeM = 198000.0; s.AltitudeM = 200000.0;
        s.ApogeeShown = OrbitReadout.ApogeeMeaningful(s.Regime);
        s.PerigeeShown = OrbitReadout.PerigeeMeaningful(s.Regime, s.PerigeeM, s.AtmosphereDepthM);
        s.Ascending = true;
        s.HasFix = true; s.Latitude = 28.5; s.Longitude = -80.6;
        s.LatText = "28.50 N"; s.LonText = "80.60 W"; s.Altitude = "200.0 km";
        s.InclinationText = "51.6 deg"; s.PeriodText = "88:32";
        s.Apoapsis = "202.0 km"; s.Periapsis = "198.0 km";
        s.TimeToApText = "00:12"; s.TimeToPeText = "44:28";
        return s;
    }
}
