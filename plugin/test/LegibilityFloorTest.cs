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
        TheSameElementReportsTheSamePercentageAtBothWidths();
        NavPageTracksThePanel();
        NavPageIsUnchangedAtTheReferenceWidth();

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

        // ⛔ REPORTED, NOT ASSERTED - AND THE GAP IS A SECOND DEFECT, NOT A TOLERANCE.
        // R-02's verify line wants these two percentages EQUAL. They are not yet, and the residual is
        // not noise: MarginAffordance's box is built from Inset = 4f, BorderPx = 2f and Pad = 2f, all
        // DEVICE-pixel constants subtracted from a letterbox that scales with the panel. So the usable
        // width grows 2.13x while the panel grows 2.00x, and the fitted type comes out slightly LESS
        // bad at 2560 for a reason that has nothing to do with legibility. QC measured that same 2.13x
        // independently.
        //
        // That is the SAME FAMILY as R-02 and a DIFFERENT INSTANCE of it, and it is enumerated by job
        // 3 of the 2026-09-06 batch, which owns MarginAffordance.cs's Inset. Asserting equality here
        // would make job 2's suite depend on job 3's fix and leave the tree red in between. Printed
        // instead, the way FigmaUINavTest prints this same control's floor, and TIGHTENED TO AN
        // ASSERTION BY JOB 3 - the numbers below are what it has to make equal.
        Console.WriteLine("  note  fitted type vs the floor: MANUAL/DOCKING "
            + (pct1 * 100f).ToString("0.0") + "% @1280 vs " + (pct2 * 100f).ToString("0.0") + "% @2560"
            + ", RENDEZVOUS " + (rz1 * 100f).ToString("0.0") + "% vs " + (rz2 * 100f).ToString("0.0")
            + "%   (residual = MarginAffordance's device-px Inset/Border/Pad - batch job 3)");

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
        return MarginAffordance.FitSize(bw, h * 0.020f, a, b) / Typography.MinFor(w);
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
        {
            float x1, y1, w1, h1, x2, y2, w2, h2;
            NavPage.NextViewRect(W1, H1, out x1, out y1, out w1, out h1);
            NavPage.NextViewRect(W2, H2, out x2, out y2, out w2, out h2);
            // Two-sided on purpose. "Clears the bar" alone would pass if the page left a 64 px hole
            // above it, which is what scaling ChromeBar.Height here (while the bar itself is not
            // scaled) would do. The gap must be the page's own padding, exactly - no overlap, no hole.
            Check("NAV sits exactly one Pad above the chrome bar it clears, at 1280",
                  Math.Abs((ChromeBar.TopY(H1) - (y1 + h1)) - 24f * Typography.ScaleFor(W1)) < 0.01f,
                  "gap " + (ChromeBar.TopY(H1) - (y1 + h1)) + ", want " + (24f * Typography.ScaleFor(W1)));
            Check("NAV sits exactly one Pad above the chrome bar it clears, at 2560",
                  Math.Abs((ChromeBar.TopY(H2) - (y2 + h2)) - 24f * Typography.ScaleFor(W2)) < 0.01f,
                  "gap " + (ChromeBar.TopY(H2) - (y2 + h2)) + ", want " + (24f * Typography.ScaleFor(W2)));
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
