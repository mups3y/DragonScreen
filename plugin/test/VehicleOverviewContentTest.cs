/*
 * VehicleOverviewContentTest — `SPEC_OVERVIEW_STATUS_ROWS.md` §7/§8/§9 asserted rather than argued.
 *
 * ⭐ THE IDIOMS, SAME AS `BasePageIconTest`/`DialGaugeTest`:
 *  (1) TWO INDEPENDENT EXPRESSIONS OF ONE DESIGN. §8's SIZES are re-derived here from the reference
 *      pixels and the map (`ref * 0.816149`), and its PLACEMENTS from the reference's page fraction
 *      (`ref / 1410 * 1054`). `VehicleOverviewContent` carries only the results, and neither knows
 *      about the other. ⛔ THAT IS THE POINT: a page and a suite that both spell the number the same
 *      way agree about a typo.
 *  (2) EVERY EXPECTATION IS A LITERAL OR A DERIVATION, never a read-back of the class under test.
 *  (3) §8.5's CLEARANCES ARE RECOMPUTED, not copied — the spec says "RECOMPUTE THESE AFTER ANY MOVE"
 *      and this is what does it.
 *
 * ⛔ THE LOAD-BEARING CHECK IS `OneSeveritySourceReachesBothTheDialAndTheRow`. `SPEC_GAUGES.md` §7:
 * "Test by driving one channel into caution and asserting BOTH change — never both against a
 * constant." A row reading nominal while its dial reads caution is what the rule exists to prevent,
 * and nothing else in this suite would catch it.
 *
 * ⚠ DESIGN SPACE ONLY, at 1920x1054 where k = 1 and both offsets are 0.
 */
using DragonScreen;
using System;
using System.Collections.Generic;

public static class VehicleOverviewContentTest
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
                    && Math.Abs(got.B - w.B) < 0.002f, "got " + Hex(got) + " want #" + wantHex);
    }
    static string Hex(Rgba c)
    {
        return "#" + ((int)(c.R * 255f + 0.5f)).ToString("X2")
                   + ((int)(c.G * 255f + 0.5f)).ToString("X2")
                   + ((int)(c.B * 255f + 0.5f)).ToString("X2");
    }

    const int FW = 1920, FH = 1054;
    // §8's map, typed from the spec. ⛔ U() is a SIZE, F() is a PLACEMENT, and there is no y map.
    const double S = 0.816149, REFH = 1410.0, H = 1054.0;
    static double U(double v) { return v * S; }
    static double F(double v) { return v / REFH * H; }

    /// <summary>A vessel whose every channel is healthy, with the approved page's own readings.
    /// ⚠ Built by hand rather than defaulted: `new PageState()` zeroes `Cabin`, and a cabin at 0.0
    /// psia is an ALARM on three channels — a fixture that alarms everywhere proves nothing.</summary>
    static PageState Healthy()
    {
        PageState s = new PageState();
        s.Valid = true;
        s.Cabin.Ppo2Psia = 2.69; s.Cabin.Ppo201 = 2.69 / 5.0;
        s.Cabin.CabinTempC = 22.4; s.Cabin.CabinTemp01 = 22.4 / 40.0;
        s.Cabin.PressPsia = 14.0; s.Cabin.Press01 = 14.0 / 20.0;
        s.Cabin.Co2MmHg = 0.07; s.Cabin.Co201 = 0.07 / 8.0;
        s.Cabin.LoopAC = 26.5; s.Cabin.LoopA01 = 26.5 / 80.0;
        s.Cabin.LoopBC = 20.0; s.Cabin.LoopB01 = 20.0 / 80.0;
        s.Cabin.NetPwr1W = 0.0; s.Cabin.NetPwr2W = 0.0;
        s.Power01 = 0.92; s.DragonProp01 = 0.64;
        s.DragonFuel01 = 1.0; s.DragonOx01 = 1.0;
        s.PowerUnit1Text = "92 %"; s.PowerUnit2Text = "92 %";
        s.DeorbitFuelText = "693.0 kg"; s.DeorbitOxText = "538.2 kg";
        return s;
    }

    static OverviewInputs Ui(bool cabin, bool more, bool complete)
    {
        OverviewInputs u = new OverviewInputs();
        u.CabinSelected = cabin; u.MoreActive = more; u.ChecksComplete = complete;
        return u;
    }

    static DisplayList Content(PageState s, OverviewInputs u)
    {
        DisplayList dl = new DisplayList(VehicleOverviewContent.Commands + 8);
        VehicleOverviewContent.Content(dl, FW, FH, s, u);
        return dl;
    }

    static List<DrawCmd> Kind(DisplayList dl, DrawKind k)
    {
        var o = new List<DrawCmd>();
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Kind == k) o.Add(dl.At(i));
        return o;
    }
    static DrawCmd TextSaying(DisplayList dl, string s)
    {
        for (int i = 0; i < dl.Count; i++)
            if (dl.At(i).Kind == DrawKind.Text && dl.At(i).Str == s) return dl.At(i);
        return new DrawCmd();
    }
    static int TextCount(DisplayList dl, string s)
    {
        int n = 0;
        for (int i = 0; i < dl.Count; i++)
            if (dl.At(i).Kind == DrawKind.Text && dl.At(i).Str == s) n++;
        return n;
    }

    public static int Run()
    {
        Console.WriteLine("VehicleOverviewContentTest (SPEC_OVERVIEW_STATUS_ROWS §7/§8/§9 + SPEC_GAUGES §5.5/§5.6)");
        checks = 0; failures = 0;

        EverySizeIsTheReferenceTimesS();
        EveryPlacementIsTheReferencesPageFraction();
        TheTitleIsDerivedFromTheGaugeRowItCentresAgainst();
        Section85sClearancesRecomputedFromScratch();
        TheSevenRowsReadTheSourcesTheSpecNames();
        OneSeveritySourceReachesBothTheDialAndTheRow();
        TheMarkerHasFOURStatesAndGreyIsNotOneOfTheSeverities();
        TheStatusInkKeysOnTheMarkerNotOnTheSeverity();
        TheTwoDiscreteRowsComeFromTheBooleans();
        TheConnectionsBlockAndItsTwoDashes();
        HalfTheRightPanelDashesAndItIsBecauseTheSourceIsNull();
        TheToggleIsTwoOverlappingShapesAndTheSplitIsThePillsInnerEdge();
        AllFourControlStates();
        TheVehicleIsWidthFitAndNeverScaledPerAxis();
        TheEightDialsSitWhereTheLockedGridPutsThem();
        TheUnitsAreTheSpecsOwnAndTheDegreeSignSurvivedTheEncoding();
        TheBudgetHoldsAndTheShellIsNotDrawnTwice();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    // ---- §8: SIZES scale by S ------------------------------------------------------------------
    static void EverySizeIsTheReferenceTimesS()
    {
        Near("marker disc diameter is 35.5 ref px", VehicleOverviewContent.MarkerDiameter, U(35.5), 0.02);
        Near("the rail's left inset is 84 ref px", VehicleOverviewContent.MarkerLeft, U(84), 0.02);
        Near("the rail's text column is 138 ref px", VehicleOverviewContent.RailTextLeft, U(138), 0.02);
        Near("a rail title inks 16 ref px of caps", VehicleOverviewContent.RailTitleInk, U(16), 0.02);
        Near("title to status is 36 ref px", VehicleOverviewContent.RailTitleToStatus, U(36), 0.02);
        Near("the page title inks 29 ref px", VehicleOverviewContent.TitleInk, U(29), 0.02);
        Near("a CONNECTIONS row inks 15 ref px", VehicleOverviewContent.CnRowInk, U(15), 0.02);
        Near("the outline box is 371 ref px wide", VehicleOverviewContent.BoxW, U(371), 0.02);
        Near("the outline box is 85 ref px tall", VehicleOverviewContent.BoxH, U(85), 0.02);
        Near("the white pill is 201 ref px wide", VehicleOverviewContent.PillW, U(201), 0.02);
        Near("the white pill is 108 ref px tall", VehicleOverviewContent.PillH, U(108), 0.02);
        Near("MORE is 169 ref px wide", VehicleOverviewContent.MoreW, U(169), 0.02);
        Near("a control label inks 17 ref px", VehicleOverviewContent.CtrlInk, U(17), 0.02);
        // ⛔ THE PILL IS TALLER THAN THE BOX AND SITS PROUD — the easy mistake, made once already.
        Near("the pill is 18.77 px taller than the box it overlaps",
             VehicleOverviewContent.PillH - VehicleOverviewContent.BoxH, U(108) - U(85), 0.02);
    }

    // ---- §8: PLACEMENTS use the page fraction. ⛔ THERE IS NO y MAP ------------------------------
    static void EveryPlacementIsTheReferencesPageFraction()
    {
        // ⚠ ROW 1 IS THE EXCEPTION AND IT IS DELIBERATE: it keeps the UNIFORM-scale start, because
        // by page fraction it would sit at 77.8 — 43 px higher, 3 px under the title. Only the PITCH
        // compresses. A suite that "corrected" this would be undoing the spec's own reasoning.
        Near("rail row 1 keeps the UNIFORM-scale start", VehicleOverviewContent.Row1MarkerCy,
             U(161.5), 0.02);
        Near("rail row 7 lands on the reference's page fraction",
             VehicleOverviewContent.Row1MarkerCy + 6 * VehicleOverviewContent.RowPitch,
             F(161.5 + 6 * 136.5), 0.03);
        Near("so the pitch is the compressed one, not 136.5 * S",
             VehicleOverviewContent.RowPitch, (F(161.5 + 6 * 136.5) - U(161.5)) / 6.0, 0.01);
        Check("...and it is NOT the uniform scale, which is the whole trap",
              Math.Abs(VehicleOverviewContent.RowPitch - U(136.5)) > 10.0,
              "pitch " + VehicleOverviewContent.RowPitch + " vs uniform " + U(136.5));

        Near("the CONNECTIONS rule sits 42 ref px under its header",
             VehicleOverviewContent.CnRuleY - VehicleOverviewContent.CnHeaderInkTop,
             F(812) - F(770), 0.02);
        Near("the CONNECTIONS rows take the reference's compressed pitch",
             VehicleOverviewContent.CnRowPitch, (F(955) - F(829)) / 3.0, 0.02);

        // §8.4.1: the outline band's BOTTOM sits on the shelf line, and both controls share the band.
        Near("the control band's bottom is the shelf line",
             VehicleOverviewContent.BoxY + VehicleOverviewContent.BoxH, 893.0, 0.02);
        Near("the pill is centred on the box, so it is proud by the same amount top and bottom",
             VehicleOverviewContent.PillY + VehicleOverviewContent.PillH * 0.5f,
             VehicleOverviewContent.BoxY + VehicleOverviewContent.BoxH * 0.5f, 0.01);
        Near("MORE mirrors the rail's own left inset on the right edge",
             VehicleOverviewContent.MoreX, 1920.0 - U(84) - U(169), 0.05);
        Near("every control label centres on the shared band's centre",
             VehicleOverviewContent.CtrlInkCy,
             VehicleOverviewContent.BoxY + VehicleOverviewContent.BoxH * 0.5f, 0.02);
    }

    static void TheTitleIsDerivedFromTheGaugeRowItCentresAgainst()
    {
        // ⭐ 66.52 IS DERIVED, NOT TYPED: it centres 29-ref-px ink in the band between the window's
        // inner top and the top gauge row's own top edge. ⛔ Move the gauge row and this must move,
        // which is why the suite spells the derivation instead of the answer.
        double rowTop = VehicleOverviewContent.TopRowCy - VehicleOverviewContent.TopRowR;
        double want = (VehicleOverviewContent.WindowInnerTop + rowTop) / 2.0 - U(29) / 2.0;
        Near("the page title's ink top is the derivation, not a typed number",
             VehicleOverviewContent.TitleInkTop, want, 0.02);
        Near("which leaves the same gap above and below it",
             VehicleOverviewContent.TitleInkTop - VehicleOverviewContent.WindowInnerTop,
             rowTop - (VehicleOverviewContent.TitleInkTop + VehicleOverviewContent.TitleInk), 0.02);
        Near("and the gap is 45.02", VehicleOverviewContent.TitleInkTop - 21.5, 45.02, 0.03);

        DisplayList dl = Content(Healthy(), Ui(false, false, true));
        DrawCmd t = TextSaying(dl, "VEHICLE OVERVIEW");
        Check("the title is drawn", t.Str == "VEHICLE OVERVIEW", "");
        Near("centred on the frame", t.A, 960.0, 0.01);
        Check("centred, not left-anchored", t.Align == TextAlign.Centre, "");
        Near("its INK top is where the derivation puts it",
             t.B + Typography.CapCentreOfTop * t.C - t.C * Typography.CapHeightOfSize * 0.5,
             66.52, 0.03);
        SameColour("in #FFFFFF", t.Colour, "FFFFFF");
    }

    // ---- §8.5 ----------------------------------------------------------------------------------
    static void Section85sClearancesRecomputedFromScratch()
    {
        double titleBottom = VehicleOverviewContent.TitleInkTop + VehicleOverviewContent.TitleInk;
        double markerTop = VehicleOverviewContent.Row1MarkerCy
                           - VehicleOverviewContent.MarkerDiameter * 0.5;
        Near("title ink bottom", titleBottom, 90.19, 0.02);
        Near("rail row 1 marker top", markerTop, 117.32, 0.02);
        Near("title -> rail", markerTop - titleBottom, 27.13, 0.03);

        double row1Bottom = VehicleOverviewContent.TopRowCy + VehicleOverviewContent.TopRowR;
        double row2Top = VehicleOverviewContent.SecondRowCy - VehicleOverviewContent.SecondRowR;
        double row2Bottom = VehicleOverviewContent.SecondRowCy + VehicleOverviewContent.SecondRowR;
        Near("gauge row 1 bottom", row1Bottom, 317.40, 0.02);
        Near("gauge row 1 -> the vehicle is the TIGHTEST vertical on the page",
             VehicleOverviewContent.VehicleTop - row1Bottom, 17.10, 0.03);
        Near("gauge row 1 -> gauge row 2", row2Top - row1Bottom, 39.80, 0.03);
        Near("gauge row 2 -> the CONNECTIONS header",
             VehicleOverviewContent.CnHeaderInkTop - row2Bottom, 51.20, 0.03);

        double railBottom = VehicleOverviewContent.Row1MarkerCy + 6 * VehicleOverviewContent.RowPitch
                            - VehicleOverviewContent.TitleTopAboveMarker
                            + VehicleOverviewContent.RailTitleToStatus
                            + VehicleOverviewContent.RailStatusInk;
        Near("the rail's ink bottom", railBottom, 768.85, 0.03);
        Near("rail -> the control band", VehicleOverviewContent.BoxY - railBottom, 54.78, 0.04);

        double cnBottom = VehicleOverviewContent.CnRow1InkTop + 3 * VehicleOverviewContent.CnRowPitch
                          + VehicleOverviewContent.CnRowInk;
        Near("CONNECTIONS' ink bottom", cnBottom, 702.53, 0.03);
        Near("CONNECTIONS -> the control band", VehicleOverviewContent.BoxY - cnBottom, 121.10, 0.04);

        float vl, vt, vr, vb;
        VehicleOverviewContent.VehicleBox(out vl, out vt, out vr, out vb);
        Near("CONNECTIONS' right edge -> the vehicle", vl - VehicleOverviewContent.CnRuleX1, 46.94, 0.03);

        // ⭐⭐ THE SUB-PIXEL SIDE GAPS ARE DELIBERATE AND FAITHFUL — the reference does the same
        // thing, its capsule ending where the NET PWR card begins. ⛔ Do NOT tidy them into a round
        // number. ⚠ §5.5 states 0.8 / 1.0 and §8.5 states 0.9 / 0.9; the LOCKED x values give
        // 0.8 / 1.0, and that disagreement is reported rather than split.
        double loopBRight = VehicleOverviewContent.SecondRowCx[1] + VehicleOverviewContent.SecondRowR;
        double netPwr1Left = VehicleOverviewContent.SecondRowCx[2] - VehicleOverviewContent.SecondRowR;
        Near("LOOP B's right edge -> the vehicle", vl - loopBRight, 0.8, 0.02);
        Near("the vehicle -> NET PWR 1's left edge", netPwr1Left - vr, 1.0, 0.02);
        Check("the vehicle clears the shelf", vb < 893.0, "bottom " + vb);
    }

    // ---- §2 / §7.3 -----------------------------------------------------------------------------
    static void TheSevenRowsReadTheSourcesTheSpecNames()
    {
        PageState s = Healthy();
        string[] want = { "ALL SYSTEMS CHECK", "CABIN LIFE SUPPORT", "CABIN THERMAL CONTROL",
                          "ELECTRICAL POWER SYSTEM", "PROPELLANT QUANTITY", "TRUNK SEPARATION",
                          "PARACHUTE DEPLOYMENT" };
        DisplayList dl = Content(s, Ui(false, false, true));
        for (int i = 0; i < 7; i++)
            Check("row " + i + " is titled " + want[i],
                  VehicleOverviewContent.RailTitleAt(i) == want[i],
                  VehicleOverviewContent.RailTitleAt(i));
        Check("all seven titles are on the page",
              TextCount(dl, "ALL SYSTEMS CHECK") == 1 && TextCount(dl, "PARACHUTE DEPLOYMENT") == 1, "");

        // ⛔ THE SOURCE ITSELF, not a re-implementation: five of seven ARE `Alarms.*` calls, which is
        // what makes the marker map with zero invention.
        Check("row 0 is Alarms.VehicleSeverity",
              VehicleOverviewContent.RowSeverity(s, 0) == Alarms.VehicleSeverity(s), "");
        Check("row 1 is Alarms.LifeSupport(s.Cabin)",
              VehicleOverviewContent.RowSeverity(s, 1) == Alarms.LifeSupport(s.Cabin), "");
        Check("row 2 is Alarms.Thermal(s.Cabin)",
              VehicleOverviewContent.RowSeverity(s, 2) == Alarms.Thermal(s.Cabin), "");
        Check("row 3 is Alarms.PowerSeverity",
              VehicleOverviewContent.RowSeverity(s, 3) == Alarms.PowerSeverity(s), "");
        Check("row 4 is Alarms.PropellantSeverity",
              VehicleOverviewContent.RowSeverity(s, 4) == Alarms.PropellantSeverity(s), "");

        // §7.3's words are the ones already in the flying code. ⛔ A second vocabulary for one
        // Severity is what the one-severity-source rule forbids.
        Check("a healthy vessel reads Nominal", VehicleOverviewContent.RowWord(s, 1) == "Nominal",
              VehicleOverviewContent.RowWord(s, 1));
        PageState c = Healthy(); c.Cabin.Ppo2Psia = 2.2;
        Check("a cautioning channel reads Caution", VehicleOverviewContent.RowWord(c, 1) == "Caution",
              VehicleOverviewContent.RowWord(c, 1));
        PageState a = Healthy(); a.Cabin.Ppo2Psia = 1.5;
        Check("an alarming channel reads Alarm", VehicleOverviewContent.RowWord(a, 1) == "Alarm",
              VehicleOverviewContent.RowWord(a, 1));
        Check("and the words are title-case, not the ALERTS banner's caps",
              VehicleOverviewContent.RowWord(a, 1) != Alarms.Word(Severity.Alarm), "");
    }

    // ---- ⛔ THE LOAD-BEARING ONE (SPEC_GAUGES §7) ------------------------------------------------
    static void OneSeveritySourceReachesBothTheDialAndTheRow()
    {
        // "Test by driving one channel into caution and asserting BOTH change — never both against a
        // constant." So: read the dial's colour AND the row's marker + word, before and after.
        PageState ok = Healthy();
        PageState caution = Healthy();
        caution.Cabin.Ppo2Psia = 2.2;            // below CabinLimits.Ppo2Caution (2.5), above alarm
        PageState alarm = Healthy();
        alarm.Cabin.Ppo2Psia = 1.5;              // below CabinLimits.Ppo2Alarm (2.0)

        DisplayList dOk = Content(ok, Ui(false, false, true));
        DisplayList dCa = Content(caution, Ui(false, false, true));
        DisplayList dAl = Content(alarm, Ui(false, false, true));

        // the dial: PPO2's arc is the first ArcBand of the first dial, and the marker discs are
        // ArcBands too — so find the arc by its radii instead of by its index.
        Rgba arcOk = FirstDialArc(dOk), arcCa = FirstDialArc(dCa), arcAl = FirstDialArc(dAl);
        SameColour("PPO2's arc is BLUE while the channel is healthy", arcOk, "298BFE");
        SameColour("...YELLOW once it is in caution", arcCa, "FFD733");
        SameColour("...RED once it is in alarm", arcAl, "E73030");

        // the row that reads the same channel, in the same frame
        SameColour("CABIN LIFE SUPPORT's marker is GREEN while the channel is healthy",
                   MarkerOfRow(dOk, 1), "40C110");
        SameColour("...ORANGE in the SAME frame the dial turns yellow", MarkerOfRow(dCa, 1), "EA7B15");
        Check("...and RED in the same frame it turns red",
              Same(MarkerOfRow(dAl, 1), DragonPalette.Alarm), Hex(MarkerOfRow(dAl, 1)));
        Check("the row's WORD moved with it", TextCount(dCa, "Caution") >= 1
              && TextCount(dOk, "Caution") == 0, "");

        // ⛔ AND THE POINT: they cannot disagree, because they are one function. A dial that were
        // coloured by its own classifier would pass every check above and still fail this one the
        // first time a threshold moved.
        Check("dial and row are driven by the SAME Alarms call",
              VehicleOverviewContent.Reading(caution, 0).Sev
              == Alarms.Band(caution.Cabin.Ppo2Psia, CabinLimits.Ppo2Caution, CabinLimits.Ppo2Alarm)
              && VehicleOverviewContent.RowSeverity(caution, 1) == Alarms.LifeSupport(caution.Cabin),
              "");
        Check("and LifeSupport is itself banded from the same limits",
              Alarms.LifeSupport(caution.Cabin) == Severity.Caution, "");
    }

    static bool Same(Rgba a, Rgba b)
    {
        return Math.Abs(a.R - b.R) < 0.002f && Math.Abs(a.G - b.G) < 0.002f
            && Math.Abs(a.B - b.B) < 0.002f;
    }

    /// <summary>PPO2's strip: the first ArcBand whose outer radius is the top row's track radius.</summary>
    static Rgba FirstDialArc(DisplayList dl)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.ArcBand && Math.Abs(c.D - 91.1f) < 0.01f && c.C > 0f)
                return c.Colour;
        }
        return new Rgba(0f, 0f, 0f, 0f);
    }

    /// <summary>Row n's marker disc: an ArcBand of radius 28.97/2 centred on that row.</summary>
    static Rgba MarkerOfRow(DisplayList dl, int row)
    {
        float cy = 131.81f + row * 100.19f;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.ArcBand && c.C == 0f
                && Math.Abs(c.D - 28.97f * 0.5f) < 0.01f && Math.Abs(c.B - cy) < 0.01f)
                return c.Colour;
        }
        return new Rgba(0f, 0f, 0f, 0f);
    }

    // ---- §7.1 ----------------------------------------------------------------------------------
    static void TheMarkerHasFOURStatesAndGreyIsNotOneOfTheSeverities()
    {
        // ⛔ PROVED BY DRIVING SEVERITY, not by observing absence: "draws nothing" and "is not
        // connected" produce identical display lists.
        PageState ok = Healthy();
        PageState caution = Healthy(); caution.Cabin.Ppo2Psia = 2.2;
        PageState alarm = Healthy(); alarm.Cabin.Ppo2Psia = 1.5;

        SameColour("unchecked is #9499C3 whatever the severity says",
                   MarkerOfRow(Content(alarm, Ui(false, false, false)), 1), "9499C3");
        SameColour("...and an alarming channel is STILL grey while unchecked",
                   MarkerOfRow(Content(alarm, Ui(false, false, false)), 1), "9499C3");
        SameColour("complete + nominal is #40C110", MarkerOfRow(Content(ok, Ui(false, false, true)), 1),
                   "40C110");
        SameColour("complete + caution is #EA7B15",
                   MarkerOfRow(Content(caution, Ui(false, false, true)), 1), "EA7B15");
        Check("complete + alarm is DragonPalette.Alarm",
              Same(MarkerOfRow(Content(alarm, Ui(false, false, true)), 1), DragonPalette.Alarm), "");

        // ⛔ GREY IS NOT A SEVERITY. It is asked FIRST, because "how healthy" and "has it been
        // checked" are orthogonal — the conflation the overseer made once already.
        SameColour("the function asks 'checked?' before 'healthy?'",
                   VehicleOverviewContent.MarkerColour(Severity.Alarm, false), "9499C3");
        SameColour("and no severity can produce grey once the check is complete",
                   VehicleOverviewContent.MarkerColour(Severity.Nominal, true), "40C110");

        // the disc's own geometry, and the tick knocked out of it
        DisplayList dl = Content(ok, Ui(false, false, true));
        int discs = 0, ticks = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.ArcBand && c.C == 0f && Math.Abs(c.D - 14.485f) < 0.01f) discs++;
            if (c.Kind == DrawKind.Line && Same(c.Colour, BasePageNoIcon.Ground)) ticks++;
        }
        Check("seven marker discs", discs == 7, "got " + discs);
        Check("each carries a two-stroke check knocked out in the page ground", ticks == 14,
              "got " + ticks);
    }

    // ---- §7.2 ----------------------------------------------------------------------------------
    static void TheStatusInkKeysOnTheMarkerNotOnTheSeverity()
    {
        // ⚠ The overseer mis-stated this rule as "dim when nominal". ⛔ It is dim when GREY: a
        // completed check reads bright WHATEVER ITS OUTCOME.
        PageState ok = Healthy();
        PageState alarm = Healthy(); alarm.Cabin.Ppo2Psia = 1.5;
        DrawCmd dim = TextSaying(Content(ok, Ui(false, false, false)), "Nominal");
        DrawCmd lit = TextSaying(Content(ok, Ui(false, false, true)), "Nominal");
        DrawCmd hot = TextSaying(Content(alarm, Ui(false, false, true)), "Alarm");
        SameColour("unchecked reads quiet", dim.Colour, "555779");
        SameColour("a completed NOMINAL row reads white", lit.Colour, "FFFFFF");
        SameColour("and so does a completed ALARM row - the ink keys on the marker", hot.Colour,
                   "FFFFFF");
    }

    // ---- §7.4 ----------------------------------------------------------------------------------
    static void TheTwoDiscreteRowsComeFromTheBooleans()
    {
        // ⚠ These report a CONFIGURATION, not a health, so their marker state is defined from the
        // booleans and NOT from Alarms.Band.
        PageState s = Healthy();
        Check("trunk: Attached", VehicleOverviewContent.RowWord(s, 5) == "Attached", "");
        s.TrunkSet = true;
        Check("trunk: Armed", VehicleOverviewContent.RowWord(s, 5) == "Armed", "");
        s.TrunkFired = true;
        Check("trunk: Separated", VehicleOverviewContent.RowWord(s, 5) == "Separated", "");
        Check("...and a normal separation is not an alarm",
              VehicleOverviewContent.RowSeverity(s, 5) == Severity.Nominal, "");

        PageState u = Healthy(); u.TrunkFired = true;      // fired without ever being armed
        Check("trunk: Unscheduled when it fires without being armed",
              VehicleOverviewContent.RowWord(u, 5) == "Unscheduled", "");
        // ⛔ "all parts that have unscheduled events should alarm" — the owner's standing rule.
        Check("...and that is an ALARM", VehicleOverviewContent.RowSeverity(u, 5) == Severity.Alarm, "");

        PageState p = Healthy();
        Check("chutes: Stowed", VehicleOverviewContent.RowWord(p, 6) == "Stowed", "");
        p.DroguesFired = true;
        Check("chutes: Drogues Out", VehicleOverviewContent.RowWord(p, 6) == "Drogues Out", "");
        p.MainsFired = true;
        Check("chutes: Mains Out", VehicleOverviewContent.RowWord(p, 6) == "Mains Out", "");
        p.MainsReleased = true;
        Check("chutes: Released", VehicleOverviewContent.RowWord(p, 6) == "Released", "");
        Check("...four states, exactly what the three booleans give",
              VehicleOverviewContent.RowSeverity(p, 6) == Severity.Nominal, "");
        PageState q = Healthy(); q.MainsFired = true;      // mains without drogues
        Check("chutes: Unscheduled on an out-of-order deployment",
              VehicleOverviewContent.RowWord(q, 6) == "Unscheduled", "");
        Check("...and it alarms", VehicleOverviewContent.RowSeverity(q, 6) == Severity.Alarm, "");
    }

    // ---- §3 / §7.5 / §8.3 -----------------------------------------------------------------------
    static void TheConnectionsBlockAndItsTwoDashes()
    {
        PageState s = Healthy();
        s.Docked = true; s.SBandLinked = true;
        DisplayList dl = Content(s, Ui(false, false, true));

        Check("the header is drawn", TextCount(dl, "CONNECTIONS") == 1, "");
        string[] labels = { "Airlock", "Docking Interface", "Comms Link", "Nose Cone" };
        for (int i = 0; i < 4; i++)
        {
            DrawCmd c = TextSaying(dl, labels[i]);
            Check(labels[i] + " is drawn", c.Str == labels[i], "");
            Near(labels[i] + " starts at the block's left edge", c.A, 513.83, 0.01);
            Near(labels[i] + "'s ink top is on the row",
                 c.B + Typography.CapCentreOfTop * c.C - c.C * Typography.CapHeightOfSize * 0.5,
                 596.10 + i * 31.40, 0.03);
        }
        Check("Docked is live", TextCount(dl, "Docked") == 1, "");
        Check("S-Band is live", TextCount(dl, "S-Band") == 1, "");
        // ⛔ TWO OF THE FOUR DASH, FOR TWO DIFFERENT REASONS: Airlock has NO SOURCE anywhere in 131
        // mods; Nose Cone is actuated but its state is not yet on `PageState` — plumbing, and
        // explicitly not this task's.
        Check("two rows dash", TextCount(dl, Dashes.None) >= 2, "");
        Check("and they dash with the PROJECT's glyph, not the gauge's",
              Dashes.None != VehicleOverviewContent.DialNoValue, "");
        // the values are right-aligned on the block's right edge
        DrawCmd v = TextSaying(dl, "Docked");
        Near("values are right-aligned on the block's right edge", v.A, 767.06, 0.01);
        Check("...right-aligned, not left-anchored", v.Align == TextAlign.Right, "");
        Near("the block is as wide as the spec says", 767.06 - 513.83, 253.23, 0.01);
        // ⚠ §8.3 wants this MEASURED FROM THE LIVE FONT and src/pure has no font metrics. The width
        // is typed; independently measured through the preview's own text call it is 253.16. BOB-48.
        Near("the spec's own components add up",
             125.28 + 50.61 + 77.27, 253.16, 0.02);

        // the comms row's three states, which need a rule for Acquiring vs No Link
        PageState no = Healthy();
        Check("no signal at all reads No Link",
              VehicleOverviewContent.ConnectionValue(no, 2) == "No Link", "");
        no.CommSignal01 = 0.4;
        Check("a signal without a lock reads Acquiring",
              VehicleOverviewContent.ConnectionValue(no, 2) == "Acquiring", "");
        no.SBandLinked = true;
        Check("a lock reads S-Band", VehicleOverviewContent.ConnectionValue(no, 2) == "S-Band", "");
        // ⛔ AND A DEAD FEED DASHES ALL FOUR. `Docked` is a bool and cannot be null, so the feed's
        // own validity is the only "is there a source" test it has — printing "Docked" beside eight
        // dashed dials is the confident claim this project refuses.
        PageState dead = Healthy(); dead.Docked = true; dead.SBandLinked = true; dead.Valid = false;
        for (int i = 0; i < 4; i++)
            Check("connection row " + i + " dashes on a dead feed",
                  VehicleOverviewContent.ConnectionValue(dead, i) == Dashes.None,
                  VehicleOverviewContent.ConnectionValue(dead, i));
    }

    // ---- §9 --------------------------------------------------------------------------------------
    static void HalfTheRightPanelDashesAndItIsBecauseTheSourceIsNull()
    {
        // ⛔ "Prove a dashed row dashes because its source is null — not because the row is unwired.
        // Those are different defects with the same appearance."
        PageState live = Healthy();
        PageState blank = Healthy();
        blank.PowerUnit1Text = null; blank.PowerUnit2Text = null;
        blank.DeorbitFuelText = null; blank.DeorbitOxText = null;

        for (int i = 0; i < 4; i++)
        {
            Check("row " + i + " prints its source while the source EXISTS",
                  VehicleOverviewContent.PanelValue(live, i) != Dashes.None,
                  VehicleOverviewContent.PanelValue(live, i));
            Check("row " + i + " dashes the moment that same field is null",
                  VehicleOverviewContent.PanelValue(blank, i) == Dashes.None, "");
        }
        for (int i = 4; i < 8; i++)
            Check("subtank row " + i + " dashes even with a fully-populated vessel",
                  VehicleOverviewContent.PanelValue(live, i) == Dashes.None, "");

        // ⚠ Power Unit 1 and 2 CANNOT differ - KSP has ONE ElectricCharge pool.
        Check("the two power rows report the same number, honestly",
              VehicleOverviewContent.PanelValue(live, 0) == VehicleOverviewContent.PanelValue(live, 1)
              && VehicleOverviewContent.PanelFraction(live, 0)
                 == VehicleOverviewContent.PanelFraction(live, 1), "");

        // the bars: a dashed row draws its TRACK and no fill, so an empty bar and an absent bar do
        // not look the same.
        DisplayList dl = Content(live, Ui(false, false, true));
        int tracks = 0, fills = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Rect || Math.Abs(c.D - 8.2f) > 0.01f) continue;
            if (Same(c.Colour, Rgba.Hex("2E304B"))) tracks++;
            if (Same(c.Colour, Rgba.Hex("298BFE"))) fills++;
        }
        Check("eight tracks", tracks == 8, "got " + tracks);
        Check("four fills - the other four have no source to fill from", fills == 4, "got " + fills);

        // geometry: §5.6's two pitches, and 97.4 is a TOTAL pitch, not an addition
        Near("row 0", VehicleOverviewContent.PanelRowY(0), 144.0, 0.01);
        Near("row 1 is a pair pitch below it", VehicleOverviewContent.PanelRowY(1), 212.6, 0.01);
        Near("row 2 is a GROUP pitch below row 1", VehicleOverviewContent.PanelRowY(2), 310.0, 0.01);
        Near("row 7", VehicleOverviewContent.PanelRowY(7), 710.6, 0.01);
        Check("the last bar clears the control band",
              VehicleOverviewContent.PanelRowY(7) + 8.2 < 823.63, "");
        DrawCmd val = TextSaying(dl, "92 %");
        Near("values are right-aligned at the panel's own right edge", val.A, 1849.8, 0.01);
    }

    // ---- §8.4 ------------------------------------------------------------------------------------
    static void TheToggleIsTwoOverlappingShapesAndTheSplitIsThePillsInnerEdge()
    {
        // ⛔ THE LABEL SPLIT IS THE PILL'S INNER EDGE. Splitting at the pill's START centres the
        // unselected label underneath the pill and clips it — `CABIN` renders as `N`.
        DisplayList sys = Content(Healthy(), Ui(false, false, true));
        DrawCmd sysLabel = TextSaying(sys, "SYSTEMS");
        DrawCmd cabLabel = TextSaying(sys, "CABIN");
        Near("SYSTEMS selected: its label centres on the pill", sysLabel.A,
             (68.56 + 68.56 + 164.05) / 2.0, 0.02);
        Near("...and CABIN centres on what is LEFT of the box, not on the whole box", cabLabel.A,
             (68.56 + 164.05 + 68.56 + 302.79) / 2.0, 0.02);
        Check("...which is NOT the whole box's centre - the failure this rule exists for",
              Math.Abs(cabLabel.A - (68.56 + 302.79 / 2.0)) > 60.0, "");

        DisplayList cab = Content(Healthy(), Ui(true, false, true));
        DrawCmd sysLabel2 = TextSaying(cab, "SYSTEMS");
        DrawCmd cabLabel2 = TextSaying(cab, "CABIN");
        Near("CABIN selected: CABIN centres on the pill, now on the right", cabLabel2.A,
             (68.56 + 302.79 - 164.05 + 68.56 + 302.79) / 2.0, 0.02);
        Near("...and SYSTEMS centres on what is left of the box", sysLabel2.A,
             (68.56 + 68.56 + 302.79 - 164.05) / 2.0, 0.02);

        // ⭐ the measured centres from the owner's own state renders: 149.5 / 301.0 with SYSTEMS
        // selected, 137.5 / 288.5 with CABIN selected (ink centres, so ±1 of the layout centre).
        Near("SYSTEMS-selected agrees with the approved render's measured ink centre",
             sysLabel.A, 150.59, 1.5);
        Near("CABIN-selected agrees with the approved render's measured ink centre",
             cabLabel2.A, 289.33, 1.5);

        SameColour("the selected label is the owner's coloured writing", sysLabel.Colour, "1A1C48");
        SameColour("the unselected label is white", cabLabel.Colour, "FFFFFF");
        // ⛔ THE INFILL IS THE TAB BAND'S OWN COLOUR, by owner ruling, and it is the SAME EXPRESSION
        // so the two cannot drift. ~~#161738, measured on the reference~~ SUPERSEDED.
        Check("the infill is the tab band's colour, not the reference's measured one",
              Same(VehicleOverviewContent.CtrlInfill, BasePageNoIcon.Margin), "");
        Check("...which is #070810", Same(BasePageNoIcon.Margin, Rgba.Hex("070810")), "");
    }

    static void AllFourControlStates()
    {
        // ⛔ RENDER ALL FOUR: the split defect is invisible in three of them.
        for (int i = 0; i < 4; i++)
        {
            bool cabin = (i & 1) != 0, more = (i & 2) != 0;
            DisplayList dl = Content(Healthy(), Ui(cabin, more, true));
            Check("state " + i + " draws all three labels exactly once",
                  TextCount(dl, "SYSTEMS") == 1 && TextCount(dl, "CABIN") == 1
                  && TextCount(dl, "MORE") == 1, "");
            DrawCmd more1 = TextSaying(dl, "MORE");
            Near("state " + i + ": MORE is centred in its own box", more1.A,
                 1713.51 + 137.93 / 2.0, 0.02);
            SameColour("state " + i + ": MORE's ink follows its own state",
                       more1.Colour, more ? "1A1C48" : "FFFFFF");
            Check("state " + i + " does not overflow", !dl.Overflowed, "");
            // every label sits on the shared band's centre line, in every state
            Near("state " + i + ": labels share one centre line",
                 more1.B + Typography.CapCentreOfTop * more1.C, 858.32, 0.03);
        }
    }

    // ---- §8.6 ------------------------------------------------------------------------------------
    static void TheVehicleIsWidthFitAndNeverScaledPerAxis()
    {
        DisplayList dl = Content(Healthy(), Ui(false, false, true));
        // ⚠⚠ S258 — THERE ARE TWO `Image` COMMANDS ON THIS PAGE NOW. This loop used to take "the
        // last one" and that happened to be the vehicle; it still would, but relying on it is the
        // kind of accident that survives a reorder. ⛔ Both are found BY KEY.
        DrawCmd img = new DrawCmd(), shadow = new DrawCmd();
        int imgAt = -1, shadowAt = -1;
        for (int i = 0; i < dl.Count; i++)
        {
            if (dl.At(i).Kind != DrawKind.Image) continue;
            if (dl.At(i).AssetKey == "dragon_crew_v3") { img = dl.At(i); imgAt = i; }
            if (dl.At(i).AssetKey == "dragon_shadow_glow") { shadow = dl.At(i); shadowAt = i; }
        }
        // ⚠ S258 — ~~`dragon_turn_000`~~ superseded in place (C1.16). 🟢 OWNER, 2026-09-10: *"I would
        // like to replace the current 3d render on the new vehicle overview page with this one."*
        // ⛔ The turntable frame is NOT deleted — the COVER page still builds frame 000's key from
        // `Turntable.KeyPrefix`; this page simply stopped being the thing that used it.
        Check("S258 the vehicle is drawn from the asset that SHIPS",
              img.AssetKey == "dragon_crew_v3", img.AssetKey ?? "(none)");

        // ⛔ NEVER SCALE THE VEHICLE PER-AXIS. The drawn rect must carry the FILE's own aspect.
        Near("the drawn rect keeps the asset's aspect exactly", img.C / img.D, 1800.0 / 3010.0, 1e-4);

        // ⛔ READ OFF THE EMITTED COMMAND, NOT OFF `VehicleBox`. A mutation that scaled the whole
        // draw by 7.7 % SURVIVED a version of this check that asked `VehicleBox` where the artwork
        // was — because `VehicleBox` recomputes the same expression the draw uses, so the two agree
        // about any error. The artwork's own corner is recovered from the RECT that was drawn, with
        // the asset's measured opaque box as the second, independent, expression.
        // ⭐ S258 — the fractions are re-pointed to the NEW file's own measured alpha box
        // (1800×3010, artwork x 93..1707 / y 80..2944 = 1614×2864), measured by the S258 session off
        // the shipped PNG. ~~464/512, 24/512, 113/1024, 798/1024~~ superseded in place (C1.16).
        double aw = img.C * (1614.0 / 1800.0);
        double al = img.A + img.C * (93.0 / 1800.0);
        double at = img.B + img.D * (80.0 / 3010.0);
        double ah = img.D * (2864.0 / 3010.0);
        Near("the drawn ARTWORK is width-fit to the locked 292.0", aw, 292.0, 0.02);
        Near("its left edge is the locked 814.0", al, 814.0, 0.02);
        Near("its top is the locked 334.5", at, 334.5, 0.02);
        Near("it is centred on the frame", al + aw / 2.0, 960.0, 0.02);
        Near("its height follows from the width and the asset, never from the box", ah,
             292.0 / (1614.0 / 2864.0), 0.03);

        float l, t, r, b;
        VehicleOverviewContent.VehicleBox(out l, out t, out r, out b);
        Near("VehicleBox reports the same left edge the command drew", l, al, 0.02);
        Near("VehicleBox reports the same top the command drew", t, at, 0.02);
        Near("the ARTWORK is width-fit to the locked 292.0", r - l, 292.0, 0.01);
        Near("it is centred on the frame", (l + r) / 2.0, 960.0, 0.01);
        // ⚠ S258 — ~~"with THIS asset the artwork runs 502.18 tall"~~ and ~~"crew_hi would have given
        // 466.59"~~ SUPERSEDED IN PLACE (C1.16). Both were true of files this page no longer draws,
        // and `BOB-49` — which named the swap as "the four constants" — is closed by it.
        // ⛔ THE DERIVATION IS ASSERTED, NOT THE LITERAL: the height must fall out of
        // `AssetOpaqueH / AssetOpaqueW × VehicleW`, so a future asset swap cannot keep a stale number.
        Near("S258 the artwork now runs 518.147 tall — DERIVED from the new asset's own box",
             b - t, 292.0 / (1614.0 / 2864.0), 0.02);
        Near("S258 ...and that derivation is what the constants actually compute",
             b - t,
             VehicleOverviewContent.AssetOpaqueH / VehicleOverviewContent.AssetOpaqueW
             * VehicleOverviewContent.VehicleW, 0.002);
        Near("S258 the bottom edge is 852.647", b, 852.647, 0.01);
        // ⚠ THE CLEARANCE FELL 56.32 -> 40.354 AND THAT IS THE ONLY THING THAT MOVED. The width is
        // bound at 292.0 and unchanged, so §8.5's locked side gaps and dial clearances are untouched.
        Near("S258 the shelf clearance is 40.354 — the ONLY edge that moved", 893.0 - b, 40.354, 0.01);
        Check("S258 ...and it still clears the shelf", 893.0 - b > 0.0, "" + (893.0 - b));
        // ⭐ THE SIDE GAPS ARE ASSERTED UNCHANGED, because "only the bottom moved" is a claim.
        Near("S258 the left edge did not move", l, 814.0, 0.002);
        Near("S258 the right edge did not move", r, 1106.0, 0.002);

        // ---- ⭐⭐ S258: THE SHADOW + GLOW, AND THE ORDER IT MUST BE DRAWN IN --------------------
        Check("S258 the shadow+glow layer is emitted at all",
              shadowAt >= 0 && shadow.AssetKey == "dragon_shadow_glow", shadow.AssetKey ?? "(none)");
        // ⛔⛔ FIRST. The effect is light and shade on the DECK; drawn beside `Vehicle` it would lay
        // the glow ON TOP of the `LOOP B` and `NET PWR 1` dials. ⚠ At ×2.00 the glow genuinely reaches
        // those two at alpha 37/255 — 🟢 the owner was shown that and chose ×2.00 — so the ONLY thing
        // keeping their ink clean is that this layer is underneath. Asserted against every other
        // command, not just against the vehicle.
        // ⚠ IT IS INDEX 1, NOT 0, AND THAT IS CORRECT: §8.1's page TITLE draws first, at y≈96, which
        // cannot overlap an effect that lives between y 273 and the shelf. §4's requirement is that
        // the effect precede the RAIL, the DIALS and the VEHICLE, and that is what is asserted —
        // "index == 0" would be a tighter pin than the rule, and a tighter pin than the rule is how a
        // later correct change gets reported as a regression.
        Check("S258 ⛔ ...and NOTHING DRAWN precedes it — the only earlier command is §8.1's title",
              shadowAt == 1 && dl.At(0).Kind == DrawKind.Text, "index " + shadowAt
              + ", command 0 is " + dl.At(0).Kind);
        Check("S258 ⛔ ...strictly before the vehicle", shadowAt < imgAt,
              shadowAt + " vs " + imgAt);
        // ⛔ THE STATEMENT THAT ACTUALLY MATTERS: no painted surface — no rail disc, no dial dot, no
        // panel fill, no image — is emitted before the effect. If a later session moves the call down
        // beside `Vehicle`, this is what fails.
        int paintedBefore = 0;
        for (int i = 0; i < shadowAt; i++)
        {
            DrawKind k = dl.At(i).Kind;
            if (k != DrawKind.Text) paintedBefore++;
        }
        Check("S258 ⛔ ...so no rail, dial, panel or image is painted under-to-over the wrong way",
              paintedBefore == 0, paintedBefore + " painted command(s) before the effect");

        // ⛔ SYMMETRIC ON 960 BY CONSTRUCTION. S256 took a 0.5 px lean out of the tab strip; the
        // overseer's first bake of this asset was 0.33 px lopsided and was re-baked on purpose.
        Near("S258 ⭐ the effect box is symmetric on 960",
             VehicleOverviewContent.ShadowX + VehicleOverviewContent.ShadowW / 2f, 960.0, 0.002);
        // ⚠⚠ ~~273, NOT THE PROMPT'S 271 — the asset settled it. Its alpha ramp reaches 1/255 at its
        // own LAST ROW, so the bottom edge IS design y 893.0 … 271 + 620 = 891 contradicts both.~~
        // ⛔⛔ S259 — SUPERSEDED IN PLACE (C1.16). ⭐⭐ THE ASSERTION ITSELF IS UNCHANGED, DELIBERATELY:
        // it states the design rule — *the effect stops at the shelf* — and relaxing it to 891 would
        // have pinned S258's sloppy bake as if it were the intent. That is precisely the failure S256
        // wrote up: *"a check that asserts a defect is indistinguishable from one that asserts a
        // decision."* ⭐ The ASSET was re-baked instead, so this now passes on `271 + 622` HONESTLY.
        // ⚠ AND THE S258 REASONING ABOVE IS KEPT BECAUSE IT IS WRONG IN AN INSTRUCTIVE WAY: "the ramp
        // reaches 1/255 at the last row" cannot tell a ramp that ENDS there from one CUT there — a
        // smoothstep truncated 2 px early sits at ~0.7 % of local, which rounds to exactly 1/255.
        // ⭐ What discriminates, measured on both files: the old bake's last row with alpha>0 was its
        // own final row (edge alpha 1 — truncated); the new bake dies 6 rows before its edge (edge
        // alpha 0). Same ramp either way — alpha max diff 2/255 across the shared 1860 rows.
        Near("S259 ⛔ the effect reaches the shelf and stops there (bottom == 893.0)",
             VehicleOverviewContent.ShadowY + VehicleOverviewContent.ShadowH, 893.0, 0.002);
        Near("S259 ...and it starts at 271, where the layer was GENERATED",
             VehicleOverviewContent.ShadowY, 271.0, 0.002);
        // ⭐ The asset is exactly 3x the design box by construction — 786x3 = 2358, 622x3 = 1866.
        Near("S258 the drawn box is the design box, not the file's pixels",
             shadow.C, VehicleOverviewContent.ShadowW, 0.002);
        Near("S258 ...and its height likewise", shadow.D, VehicleOverviewContent.ShadowH, 0.002);
        // ⛔⛔ S259 — THE CHECK THAT WOULD HAVE CAUGHT S258 FROM THE OTHER SIDE, AND ITS ABSENCE IS
        // WHY THE CONTRADICTION SURVIVED REVIEW. The layer is drawn at exactly 3x with no per-axis
        // stretch, so the DESIGN BOX's aspect must equal the FILE's. At S258's 786x620 against a
        // 2358x1860 file the two agreed — which is exactly why it passed while being 2 px low: the
        // box was self-consistent and simply in the wrong place. It is the RE-BAKED file (2358x1866)
        // that makes 786x622 the only box that satisfies both this and the shelf rule at once.
        // ⚠ The file's pixels are typed here as literals, measured off the shipped PNG by S259 — an
        // independent second expression, not a re-read of the constants.
        Near("S259 ⭐⭐ the layer is drawn at exactly 3x — the box's aspect IS the file's",
             VehicleOverviewContent.ShadowW / VehicleOverviewContent.ShadowH, 2358.0 / 1866.0, 1e-4);
        Near("S259 ...and 3x is the literal scale, both axes, no per-axis stretch",
             2358.0 / VehicleOverviewContent.ShadowW, 3.0, 1e-6);
        Near("S259 ...both axes", 1866.0 / VehicleOverviewContent.ShadowH, 3.0, 1e-6);
        Check("S258 the effect is drawn at full white tint, so the asset's own alpha composites",
              shadow.Colour.R > 0.999f && shadow.Colour.G > 0.999f
              && shadow.Colour.B > 0.999f && shadow.Colour.A > 0.999f, "");
    }

    // ---- SPEC_GAUGES §5.5 -------------------------------------------------------------------------
    static void TheEightDialsSitWhereTheLockedGridPutsThem()
    {
        double[] topX = { 606.9, 842.1, 1077.3, 1312.4 };
        double[] secX = { 548.1, 741.4, 1178.8, 1372.2 };
        for (int i = 0; i < 4; i++)
        {
            Near("top row dial " + i + " x", VehicleOverviewContent.TopRowCx[i], topX[i], 0.01);
            Near("second row dial " + i + " x", VehicleOverviewContent.SecondRowCx[i], secX[i], 0.01);
        }
        Near("top row radius", VehicleOverviewContent.TopRowR, 91.1, 0.01);
        Near("second row radius", VehicleOverviewContent.SecondRowR, 71.8, 0.01);
        Near("second row y", VehicleOverviewContent.SecondRowCy, 429.0, 0.01);
        // ⛔ THE TOP ROW's y IS THE ONE NUMBER THE TWO SPECS DISAGREE ABOUT — SPEC_GAUGES §5.5 says
        // 212.0, SPEC_OVERVIEW §8.1/§8.5 say 226.3, and the page the owner approved measures 225.5
        // with R 92.5 including the dot. Built to 226.3; BOB-51.
        Near("top row y is the value §8.5's clearance table implies",
             VehicleOverviewContent.TopRowCy, 317.40 - 91.1, 0.01);

        // no two dials overlap, and all eight are inside the window
        for (int i = 0; i < 8; i++)
            for (int j = i + 1; j < 8; j++)
            {
                float xi = i < 4 ? VehicleOverviewContent.TopRowCx[i]
                                 : VehicleOverviewContent.SecondRowCx[i - 4];
                float yi = i < 4 ? VehicleOverviewContent.TopRowCy : VehicleOverviewContent.SecondRowCy;
                float ri = i < 4 ? VehicleOverviewContent.TopRowR : VehicleOverviewContent.SecondRowR;
                float xj = j < 4 ? VehicleOverviewContent.TopRowCx[j]
                                 : VehicleOverviewContent.SecondRowCx[j - 4];
                float yj = j < 4 ? VehicleOverviewContent.TopRowCy : VehicleOverviewContent.SecondRowCy;
                float rj = j < 4 ? VehicleOverviewContent.TopRowR : VehicleOverviewContent.SecondRowR;
                double d = Math.Sqrt((xi - xj) * (xi - xj) + (yi - yj) * (yi - yj));
                Check("dials " + i + " and " + j + " do not touch", d > ri + rj, "gap " + (d - ri - rj));
            }

        // the eight readings, and the two that have no threshold to band
        PageState s = Healthy();
        for (int i = 0; i < 8; i++)
        {
            DialReading d = VehicleOverviewContent.Reading(s, i);
            Check("dial " + i + " is valid on a healthy vessel", d.Valid, "");
            Check("dial " + i + " prints two decimal places", d.Value.IndexOf('.') == d.Value.Length - 3,
                  d.Value);
        }
        Check("LOOP A and LOOP B are the two closed rings",
              VehicleOverviewContent.Reading(s, 4).Loop && VehicleOverviewContent.Reading(s, 5).Loop, "");
        Check("nothing else is", !VehicleOverviewContent.Reading(s, 0).Loop
              && !VehicleOverviewContent.Reading(s, 6).Loop, "");
        // ⛔ NET PWR HAS NO ENTRY IN CabinLimits, so it is never banded — Alarms.GaugeColour's rule.
        PageState big = Healthy(); big.Cabin.NetPwr1W = -1900.0;
        Check("a large NEGATIVE net power is still not an alarm - it has no threshold to cross",
              VehicleOverviewContent.Reading(big, 6).Sev == Severity.Nominal, "");
        Near("...and the arc shows its MAGNITUDE", VehicleOverviewContent.Reading(big, 6).T,
             1900.0 / Cabin.NetPwrFullScale, 1e-6);
        Check("...while the SIGN survives in the printed value",
              VehicleOverviewContent.Reading(big, 6).Value == "-1900.00",
              VehicleOverviewContent.Reading(big, 6).Value);

        // §5.7's invalid case, end to end
        PageState dead = Healthy(); dead.Valid = false;
        Check("a dead feed dashes every dial with the GAUGE spec's own glyph",
              VehicleOverviewContent.Reading(dead, 0).Value == "--"
              && !VehicleOverviewContent.Reading(dead, 0).Valid, "");
    }

    static void TheUnitsAreTheSpecsOwnAndTheDegreeSignSurvivedTheEncoding()
    {
        string[] want = { "psia", "°C", "psia", "mmHg", "°C", "°C", "W", "W" };
        string[] titles = { "PPO2", "CABIN TEMP", "CABIN PRESSURE", "CO2",
                            "LOOP A", "LOOP B", "NET PWR 1", "NET PWR 2" };
        for (int i = 0; i < 8; i++)
        {
            Check("dial " + i + "'s unit is the spec's own", VehicleOverviewContent.DialUnitAt(i) == want[i],
                  VehicleOverviewContent.DialUnitAt(i));
            Check("dial " + i + "'s title is the spec's own",
                  VehicleOverviewContent.DialTitleAt(i) == titles[i],
                  VehicleOverviewContent.DialTitleAt(i));
        }
        // ⚠ The source file has no byte-order mark, so this is the check that the compiler read it as
        // UTF-8 rather than as a codepage - a mis-decode would print two glyphs, not one degree sign.
        Check("the degree sign is one character, U+00B0",
              VehicleOverviewContent.DialUnitAt(1).Length == 2
              && VehicleOverviewContent.DialUnitAt(1)[0] == (char)0x00B0, "");
    }

    static void TheBudgetHoldsAndTheShellIsNotDrawnTwice()
    {
        DisplayList dl = Content(Healthy(), Ui(false, false, true));
        Check("the content fits its advertised budget",
              dl.Count <= VehicleOverviewContent.Commands,
              dl.Count + " > " + VehicleOverviewContent.Commands);
        Check("nothing was dropped", !dl.Overflowed, "");
        // ⛔ IT IS A RENDERER: `Content` draws the CONTENT only, so a test can read this page's own
        // commands without the shell's in the way, and `Draw` composes the two.
        DisplayList whole = new DisplayList(VehicleOverviewContent.Commands + BasePageIcon.Commands + 8);
        VehicleOverviewContent.Draw(whole, FW, FH, Healthy(), Ui(false, false, true));
        // ⚠ AGAINST THE SHELL'S ACTUAL COUNT, NOT ITS BUDGET. `BasePageIcon.Commands` is an upper
        // bound — with no event lines the bar draws fewer — and comparing against a budget would
        // fail on a page that is perfectly correct. Drawing the shell alone is the honest yardstick.
        DisplayList shell = new DisplayList(BasePageIcon.Commands + 8);
        BasePageIcon.Draw(shell, FW, FH);
        Check("the whole page is the shell plus this content, and nothing is drawn twice",
              whole.Count == dl.Count + shell.Count,
              whole.Count + " vs " + (dl.Count + shell.Count));
        Check("the whole page does not overflow either", !whole.Overflowed, "");
        // ⚠ THE COST, STATED: 1184 of the content is the eight dials and 1080 of that is the dotted
        // track. `Pages.Commands` is 480 for comparison.
        Check("the eight dials are the overwhelming majority of the page",
              8 * DialGauge.Commands > dl.Count * 3 / 4, "" + dl.Count);
    }
}
