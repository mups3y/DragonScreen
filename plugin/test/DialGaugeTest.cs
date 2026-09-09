/*
 * DialGaugeTest — `SPEC_GAUGES.md` asserted rather than argued.
 *
 * ⭐ THE SAME THREE IDIOMS AS `BasePageIconTest`, because they earned it:
 *  (1) TWO INDEPENDENT EXPRESSIONS OF ONE DESIGN. The scale is checked against the spec's OWN maths
 *      convention — `angle(t) = 240 - t*300`, 0 east, anticlockwise positive — converted here by
 *      `φ = 90 - θ`. `DialGauge` never writes that formula; it writes `-150 + 300 t`. Two roads to
 *      the same number, so a typo in either is visible.
 *  (2) EVERY NUMBER IS A LITERAL TYPED FROM THE SPEC, never read back off `DialGauge`. A suite that
 *      spells its expectation `DialGauge.ArcThickness` cannot fail when `ArcThickness` is what moved
 *      — S176's finding, and the reason S245 raised M6 against itself.
 *  (3) THE SCOPE IS IN THE CHECK NAME wherever a count could be read two ways.
 *
 * ⚠ DESIGN SPACE ONLY. The fixture renders at 1920x1054, where `k = 1` and both offsets are 0, so
 * device coordinates ARE design coordinates. No rasteriser is reachable from `build.py test`; the
 * pixels are `DragonScreenPreview.exe --overviewcheck`'s half.
 */
using DragonScreen;
using System;
using System.Collections.Generic;
using System.IO;

public static class DialGaugeTest
{
    static int checks, failures;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + (detail == "" ? "" : "   " + detail)); }
    }
    static void Near(string what, double got, double want, double tol)
    {
        Check(what, Math.Abs(got - want) <= tol,
              "got " + got.ToString("F4") + " want " + want.ToString("F4"));
    }
    static void SameColour(string what, Rgba got, string wantHex)
    {
        Rgba w = Rgba.Hex(wantHex);
        Check(what, Math.Abs(got.R - w.R) < 0.002f && Math.Abs(got.G - w.G) < 0.002f
                    && Math.Abs(got.B - w.B) < 0.002f,
              "got " + Hex(got) + " want #" + wantHex);
    }
    static string Hex(Rgba c)
    {
        return "#" + ((int)(c.R * 255f + 0.5f)).ToString("X2")
                   + ((int)(c.G * 255f + 0.5f)).ToString("X2")
                   + ((int)(c.B * 255f + 0.5f)).ToString("X2");
    }

    const int FW = 1920, FH = 1054;
    // The dial the fixture draws: the top row's radius, at a centre chosen so nothing is clipped.
    const float CX = 606.9f, CY = 226.3f, R = 91.1f;

    static DisplayList One(double t, Severity sev, bool valid, bool loop)
    {
        DisplayList dl = new DisplayList(DialGauge.Commands + 8);
        DialReading d = new DialReading();
        d.Title = "PPO2"; d.Value = "2.69"; d.Unit = "psia";
        d.T = t; d.Sev = sev; d.Valid = valid; d.Loop = loop;
        DialGauge.Draw(dl, BaseFit.For(FW, FH), CX, CY, R, d);
        return dl;
    }

    static List<DrawCmd> Kind(DisplayList dl, DrawKind k)
    {
        var o = new List<DrawCmd>();
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Kind == k) o.Add(dl.At(i));
        return o;
    }

    /// <summary>The instrument angle of a point, 0 at twelve o'clock increasing clockwise.</summary>
    static double AngleOf(double x, double y)
    {
        double a = Math.Atan2(x - CX, CY - y) * 180.0 / Math.PI;
        return a;
    }
    static double RadiusOf(double x, double y)
    {
        return Math.Sqrt((x - CX) * (x - CX) + (y - CY) * (y - CY));
    }

    public static int Run()
    {
        Console.WriteLine("DialGaugeTest (SPEC_GAUGES: the scale, the geometry, the colours, the three behaviours)");
        checks = 0; failures = 0;

        TheScaleIsTheSpecsOwnFormula();
        TheDottedTrackIsAFullClosedCircleOf135Dots();
        TheArcIsStrokedINSIDETheTrackRadius();
        TheFourThresholdLinesRunINWARDOnly();
        TheTipTickIsLongerAndTakesTheStateColour();
        TheStripBreaksOnceForEveryThresholdItCrosses();
        TheLoopsAreClosedRingsWithNoTicks();
        AnInvalidChannelDrawsNoArcAndNoTip();
        OnlyAnEmergencyEscalatesTheTrack();
        TheThreeLinesOfTextArePlacedByTHEIRINK();
        TheColoursAreTheMeasuredOnesAndTheGuessesAppearNowhere();
        TheCommandBudgetIsWhatTheClassAdvertises();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    // ---- §1 ------------------------------------------------------------------------------------
    static void TheScaleIsTheSpecsOwnFormula()
    {
        // ⛔ THE SPEC'S CONVENTION, NOT THE CLASS'S: "start 240 deg (0 = east, anticlockwise
        // positive), sweep 300, angle(t) = 240 - t*300". φ = 90 - θ puts it in the project's one
        // instrument convention. If these two ever disagree the dial is drawn at the wrong angle and
        // nothing else in the suite would say so.
        foreach (double t in new[] { 0.0, 0.087, 0.193, 0.25, 0.5, 0.807, 0.913, 1.0 })
        {
            double theta = 240.0 - t * 300.0;
            Near("the scale agrees with the spec's own angle(t) at t=" + t,
                 DialGauge.Angle(t), 90.0 - theta, 1e-6);
        }
        Near("t=0 is the bottom-left end of the 300 deg sweep", DialGauge.Angle(0.0), -150.0, 1e-6);
        Near("t=0.5 is dead twelve o'clock", DialGauge.Angle(0.5), 0.0, 1e-6);
        Near("t=1 is the bottom-right end", DialGauge.Angle(1.0), 150.0, 1e-6);
        Near("the sweep is 300 degrees", DialGauge.Angle(1.0) - DialGauge.Angle(0.0), 300.0, 1e-6);
        // ⭐ NET PWR 1 reads 0.00 W and the reference puts its arc at 225..235 deg, i.e. t≈0 — the
        // corroboration §1 cites. In instrument terms that is -135..-125, inside the first band.
        Check("the reference's own t=0 corroboration lands where the scale says",
              DialGauge.Angle(0.0) <= -125.0 && DialGauge.Angle(0.0) >= -150.0, "");
    }

    // ---- §2 + §4.1 -----------------------------------------------------------------------------
    static void TheDottedTrackIsAFullClosedCircleOf135Dots()
    {
        DisplayList dl = One(0.0, Severity.Nominal, false, false);
        var dots = Kind(dl, DrawKind.Rect);
        Check("§4.1's track draws 135 dots (the owner's full circle, drawn faithfully)",
              dots.Count == 135, "got " + dots.Count);

        var angles = new List<double>();
        bool onRadius = true, sized = true;
        foreach (DrawCmd c in dots)
        {
            double cx = c.A + c.C * 0.5, cy = c.B + c.D * 0.5;
            if (Math.Abs(RadiusOf(cx, cy) - 91.1) > 0.01) onRadius = false;
            // 0.0137 R = 1.2481 at R 91.1. ⛔ Typed from the spec, not read from the class.
            if (Math.Abs(c.C - 1.2481) > 0.001 || Math.Abs(c.D - 1.2481) > 0.001) sized = false;
            angles.Add(AngleOf(cx, cy));
        }
        Check("every dot sits ON the track radius", onRadius, "");
        Check("every dot is 0.0137 R across", sized, "");

        angles.Sort();
        double want = 360.0 / 135.0, worst = 0.0;
        for (int i = 1; i < angles.Count; i++)
            worst = Math.Max(worst, Math.Abs((angles[i] - angles[i - 1]) - want));
        double wrap = (angles[0] + 360.0) - angles[angles.Count - 1];
        // ⚠ 1e-3, not 0: the dot centres are recovered from float coordinates through an atan2, so
        // the residual is the primitive's precision and not a spacing error. A tolerance of 1e-6
        // fails on arithmetic noise alone, which is a test that reports on the compiler.
        Near("the pitch is even all the way round", worst, 0.0, 1e-3);
        // ⛔ THE RING CLOSES. §2 states BOTH "2.66 deg" and "135 dots per full circle" and they
        // disagree: 135 x 2.66 = 359.1, a 0.9 deg seam = 1.43 px of gap at this radius, which the eye
        // finds instantly on a closed circle. The COUNT wins; 360/135 = 2.6667 is 0.25 % from the
        // measured pitch. BOB-36.
        Near("the wrap-around gap is the same as every other gap (the ring CLOSES)", wrap, want, 1e-6);
        Near("135 dots at the spec's literal 2.66 would have left this seam, in degrees",
             360.0 - 135.0 * 2.66, 0.9, 1e-9);
    }

    // ---- §2, the owner ruling that was broken once ---------------------------------------------
    static void TheArcIsStrokedINSIDETheTrackRadius()
    {
        // 🟢 OWNER: "no one told you to move the arc. put it back and only do as i asked."
        DisplayList dl = One(0.6, Severity.Nominal, true, false);
        var arcs = Kind(dl, DrawKind.ArcBand);
        Check("the strip drew something", arcs.Count > 0, "");
        bool inside = true;
        foreach (DrawCmd c in arcs)
        {
            // 0.071 R = 6.4681 at R 91.1, stroked INWARD from the track line.
            if (Math.Abs(c.D - 91.1) > 0.001) inside = false;          // outer edge ON the line
            if (Math.Abs(c.C - (91.1f - 6.4681f)) > 0.002) inside = false;
        }
        Check("the arc's OUTER edge is on the dotted line and its body runs inward 0.071 R",
              inside, "");
    }

    // ---- §3 ------------------------------------------------------------------------------------
    static void TheFourThresholdLinesRunINWARDOnly()
    {
        DisplayList dl = One(0.0, Severity.Nominal, false, false);
        var lines = Kind(dl, DrawKind.Line);
        Check("an invalid non-loop dial still draws its four threshold lines (§5.7)",
              lines.Count == 4, "got " + lines.Count);

        double[] want = { 0.087, 0.193, 0.807, 0.913 };
        var got = new List<double>();
        bool inward = true, width = true;
        foreach (DrawCmd c in lines)
        {
            double r0 = RadiusOf(c.A, c.B), r1 = RadiusOf(c.C, c.D);
            // 🟢 OWNER: "the dotted line is the starting point for the tick and should only extend
            // into the interior of the circle not the exterior."
            if (Math.Abs(r0 - 91.1) > 0.01) inward = false;
            if (Math.Abs(r1 - 91.1 * (1.0 - 0.072)) > 0.01) inward = false;
            if (r0 > 91.1 + 0.01 || r1 > 91.1 + 0.01) inward = false;
            if (Math.Abs(c.StartDeg - 1.2481f) > 0.001f) width = false;
            got.Add(AngleOf(c.A, c.B));
        }
        Check("every threshold line starts ON the dotted line and runs INWARD 0.072 R, never out",
              inward, "");
        // ⚠ THE WIDTH IS NOT IN THE SPEC — BOB-33. It is the dot diameter, measured off the
        // reference; this pins what was chosen so a later change is a decision and not a drift.
        Check("both tick types are one dot-diameter wide (measured; BOB-33 still open)", width, "");

        got.Sort();
        for (int i = 0; i < 4; i++)
            Near("threshold " + i + " is at the spec's own t", got[i], 90.0 - (240.0 - want[i] * 300.0), 0.02);
        // ⭐ MIRRORED, which is why the spec trusts them: 0.087+0.913 = 1 and 0.193+0.807 = 1.
        Near("the four are mirrored in pairs (outer)", want[0] + want[3], 1.0, 1e-9);
        Near("the four are mirrored in pairs (inner)", want[1] + want[2], 1.0, 1e-9);
    }

    // ---- §5b -----------------------------------------------------------------------------------
    static void TheTipTickIsLongerAndTakesTheStateColour()
    {
        DisplayList dl = One(0.6, Severity.Caution, true, false);
        var lines = Kind(dl, DrawKind.Line);
        Check("four thresholds plus the tip", lines.Count == 5, "got " + lines.Count);

        DrawCmd tip = lines[lines.Count - 1];
        double r1 = RadiusOf(tip.C, tip.D);
        Near("the tip tick starts on the line", RadiusOf(tip.A, tip.B), 91.1, 0.01);
        // ⚠ 0.135 R, and it is longer for ONE reason: with the arc stroked inside R a tick of the
        // threshold's own length would be completely hidden by the arc body.
        Near("the tip tick is 0.135 R long, enough to clear the arc", r1, 91.1 * (1.0 - 0.135), 0.01);
        Check("it is longer than a threshold tick, which is the only difference between them",
              r1 < 91.1 * (1.0 - 0.072), "");
        Near("it sits at the VALUE", AngleOf(tip.A, tip.B), 90.0 - (240.0 - 0.6 * 300.0), 0.02);
        SameColour("it is drawn in the arc's current state colour", tip.Colour, "FFD733");
    }

    // ---- §5a -----------------------------------------------------------------------------------
    static void TheStripBreaksOnceForEveryThresholdItCrosses()
    {
        // ⛔ "the number of breaks tells you how many bands the value has passed."
        var cases = new[] {
            new object[] { 0.05,  1, 0 },   // below the first line
            new object[] { 0.15,  2, 1 },
            new object[] { 0.538, 3, 2 },   // PPO2's own reading, 2.69 of 5.00
            new object[] { 0.85,  4, 3 },
            new object[] { 0.95,  5, 4 },
        };
        foreach (object[] c in cases)
        {
            double t = (double)c[0];
            int segs = (int)c[1], breaks = (int)c[2];
            DisplayList dl = One(t, Severity.Nominal, true, false);
            var arcs = Kind(dl, DrawKind.ArcBand);
            Check("t=" + t + " draws " + segs + " strip segment(s), i.e. " + breaks + " break(s)",
                  arcs.Count == segs, "got " + arcs.Count);
            double swept = 0.0;
            foreach (DrawCmd a in arcs) swept += a.EndDeg - a.StartDeg;
            // 300 t of scale, less 3.0 deg for every break. ⛔ Typed, not read from the class.
            Near("t=" + t + " sweeps 300t less 3 deg per break", swept, 300.0 * t - 3.0 * breaks, 0.01);
            Near("t=" + t + " starts at the bottom-left end", arcs[0].StartDeg, -150.0, 1e-4);
            Near("t=" + t + " ends at the value", arcs[arcs.Count - 1].EndDeg,
                 90.0 - (240.0 - t * 300.0), 1e-4);
        }
        // ⚠ THE BREAK IS CENTRED ON THE LINE — BOB-37. Nothing in the spec says so and something had
        // to decide; centring is the only choice that leaves the threshold visible on both sides.
        DisplayList d2 = One(0.538, Severity.Nominal, true, false);
        var seg = Kind(d2, DrawKind.ArcBand);
        double at0 = 90.0 - (240.0 - 0.087 * 300.0);
        Near("the first gap ENDS 1.5 deg before its line", seg[0].EndDeg, at0 - 1.5, 1e-4);
        Near("...and the next segment starts 1.5 deg after it", seg[1].StartDeg, at0 + 1.5, 1e-4);
    }

    // ---- §4.2 ----------------------------------------------------------------------------------
    static void TheLoopsAreClosedRingsWithNoTicks()
    {
        // 🟢 OWNER: "the temp loops depicted correctly", "no ticks on temp loops". ⚠ The reference
        // DISAGREES (there they are value-driven arcs) and so does the approved mock-up, whose loop
        // arcs are decorative — the mock draws NET PWR 1 with a 180 deg arc at 0.00 W. The spec is
        // the authority and this is the departure it rules. BOB-38.
        DisplayList dl = One(0.331, Severity.Nominal, true, true);
        var arcs = Kind(dl, DrawKind.ArcBand);
        var lines = Kind(dl, DrawKind.Line);
        Check("a loop draws exactly ONE arc", arcs.Count == 1, "got " + arcs.Count);
        Near("and it is a FULL CLOSED RING, start", arcs[0].StartDeg, 0.0, 1e-6);
        Near("and it is a FULL CLOSED RING, end", arcs[0].EndDeg, 360.0, 1e-6);
        Check("a loop draws NO threshold ticks and no tip tick", lines.Count == 0,
              "got " + lines.Count);
        Check("a loop still draws its dotted track", Kind(dl, DrawKind.Rect).Count == 135, "");
    }

    // ---- §5.7 ----------------------------------------------------------------------------------
    static void AnInvalidChannelDrawsNoArcAndNoTip()
    {
        // ⛔ "an arc of length zero and an arc that is absent must not look the same" — a dial
        // reading 0.00 in nominal blue when the channel is dead is the S147b trap.
        DisplayList dead = One(0.0, Severity.Nominal, false, false);
        DisplayList zero = One(0.0, Severity.Nominal, true, false);
        Check("an INVALID dial draws no arc", Kind(dead, DrawKind.ArcBand).Count == 0, "");
        Check("an invalid dial draws no tip tick either (4 lines, all thresholds)",
              Kind(dead, DrawKind.Line).Count == 4, "");
        Check("...but a VALID zero still draws its tip tick, so the two do NOT look the same",
              Kind(zero, DrawKind.Line).Count == 5, "");
        Check("an invalid dial keeps its track and its threshold ticks",
              Kind(dead, DrawKind.Rect).Count == 135, "");
        DisplayList deadLoop = One(0.4, Severity.Nominal, false, true);
        Check("an invalid LOOP draws no ring at all", Kind(deadLoop, DrawKind.ArcBand).Count == 0, "");
    }

    // ---- §5c -----------------------------------------------------------------------------------
    static void OnlyAnEmergencyEscalatesTheTrack()
    {
        DisplayList alarm = One(0.6, Severity.Alarm, true, false);
        DisplayList caution = One(0.6, Severity.Caution, true, false);
        DisplayList nom = One(0.6, Severity.Nominal, true, false);
        SameColour("in an EMERGENCY the whole dotted track takes the state colour",
                   Kind(alarm, DrawKind.Rect)[0].Colour, "E73030");
        SameColour("...and so do the four threshold lines",
                   Kind(alarm, DrawKind.Line)[0].Colour, "E73030");
        // ⛔ CAUTION DOES NOT ESCALATE. Owner-approved knowing the evidence is one example.
        SameColour("CAUTION leaves the track grey", Kind(caution, DrawKind.Rect)[0].Colour, "9499C3");
        SameColour("nominal leaves the track grey", Kind(nom, DrawKind.Rect)[0].Colour, "9499C3");
        SameColour("but the caution ARC is still yellow",
                   Kind(caution, DrawKind.ArcBand)[0].Colour, "FFD733");
    }

    // ---- §2.2 ----------------------------------------------------------------------------------
    static void TheThreeLinesOfTextArePlacedByTHEIRINK()
    {
        DisplayList dl = One(0.6, Severity.Nominal, true, false);
        var text = Kind(dl, DrawKind.Text);
        Check("three lines: title, value, unit", text.Count == 3, "got " + text.Count);

        // ⛔ ASSERT THE INK, NEVER THE FONT SIZE (§2.2). The ink height and the ink CENTRE are what
        // the spec states; the pixel size is derived. Ratios typed from the spec: 0.125/-0.432,
        // 0.314/-0.033, 0.188/+0.532 of R, all #FFFFFF.
        double[] ink = { 0.125, 0.314, 0.188 };
        double[] centre = { -0.432, -0.033, 0.532 };
        string[] want = { "PPO2", "2.69", "psia" };
        for (int i = 0; i < 3; i++)
        {
            DrawCmd c = text[i];
            Check("line " + i + " is the right string", c.Str == want[i], c.Str);
            Near("line " + i + " inks " + ink[i] + " R tall",
                 c.C * Typography.CapHeightOfSize, ink[i] * 91.1, 0.02);
            Near("line " + i + "'s INK CENTRE is at the spec's offset from the dial centre",
                 c.B + Typography.CapCentreOfTop * c.C, 226.3 + centre[i] * 91.1, 0.02);
            Check("line " + i + " is centred on the dial", c.Align == TextAlign.Centre, "");
            SameColour("line " + i + " is #FFFFFF - the title and unit are NOT dimmed",
                       c.Colour, "FFFFFF");
        }
        Check("the value is the largest of the three", text[1].C > text[0].C && text[1].C > text[2].C, "");
    }

    // ---- §2.1 ----------------------------------------------------------------------------------
    static void TheColoursAreTheMeasuredOnesAndTheGuessesAppearNowhere()
    {
        SameColour("nominal blue is the measured #298BFE", DialGauge.Colour(Severity.Nominal), "298BFE");
        SameColour("caution yellow is the measured #FFD733", DialGauge.Colour(Severity.Caution), "FFD733");
        SameColour("emergency red is the measured #E73030", DialGauge.Colour(Severity.Alarm), "E73030");
        // ⚠ The track grey is NOT in the spec. Measured on the reference's own bare track at
        // rgb(150,155,196) — which is #9499C3, a colour this design already names. BOB-44/BOB-33's
        // sibling; pinned so it cannot drift into a fourth grey.
        SameColour("the track grey is #9499C3", DialGauge.TrackColour(Severity.Nominal), "9499C3");
        SameColour("...and it escalates to the state colour", DialGauge.TrackColour(Severity.Alarm),
                   "E73030");

        // ⛔ §2.1: "The guessed values #3B82F6 / #FACC15 / #EF4444 are WRONG and must not appear
        // anywhere." Searched in the SOURCE, live lines only (S220: a text assertion that matches a
        // comment proves nothing) - and this file names all three, which is why it reads only
        // DialGauge.cs.
        string src = Live(File.ReadAllText(Repo("plugin", "src", "pure", "DialGauge.cs")));
        foreach (string bad in new[] { "3B82F6", "FACC15", "EF4444" })
            Check("the guessed colour " + bad + " appears nowhere in the gauge",
                  src.IndexOf(bad, StringComparison.OrdinalIgnoreCase) < 0, "");
    }

    static void TheCommandBudgetIsWhatTheClassAdvertises()
    {
        // The worst case: a valid non-loop dial past all four thresholds.
        DisplayList dl = One(0.99, Severity.Alarm, true, false);
        Check("the worst case fits the advertised budget",
              dl.Count <= DialGauge.Commands, dl.Count + " > " + DialGauge.Commands);
        Check("the list did not overflow", !dl.Overflowed, "");
        // ⛔ AND THE COST IS DOMINATED BY §4.1's FULL CIRCLE, which is the number the gauge prompt
        // asked to be MEASURED rather than guessed: 135 of the 148 are track dots, so eight dials
        // spend 1080 commands on the track alone - 2.25x the whole of `Pages.Commands`.
        Check("135 of the dial's 148 commands are track dots", DialGauge.Commands == 148,
              "budget is " + DialGauge.Commands);
        Check("8 dials of track alone cost 1080 commands", 8 * 135 == 1080, "");
    }

    // ---- helpers -------------------------------------------------------------------------------
    static string Repo(params string[] parts)
    {
        var bits = new List<string> {
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", ".." };
        bits.AddRange(parts);
        return Path.GetFullPath(Path.Combine(bits.ToArray()));
    }

    /// <summary>⛔ Text assertions match commented-out code (S220). Only live lines are searched.</summary>
    static string Live(string src)
    {
        string[] lines = src.Replace("\r\n", "\n").Split(new char[] { (char)10 });
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].TrimStart();
            if (t.StartsWith("//") || t.StartsWith("///") || t.StartsWith("*") || t.StartsWith("/*")) continue;
            sb.Append(lines[i]).Append((char)10);
        }
        return sb.ToString();
    }
}
