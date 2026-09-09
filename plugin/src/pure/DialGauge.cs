/*
 * DragonScreen — DialGauge : the eight-dial family of the rebuilt VEHICLE OVERVIEW.
 *
 * ⛔ BUILT FROM `SPEC_GAUGES.md` AND NOTHING ELSE — §1 the scale, §2 the geometry as ratios of the
 * track radius, §2.1 the colours, §2.2 the three lines of text, §2.3 no card, §3 the four threshold
 * lines, §4 the three owner-ruled departures, §5 the three live behaviours, §5.7 the formatting and
 * the invalid case, §6 where the numbers live, §7 one severity source.
 *
 * ⛔ `pure/Gauge.cs` IS A DIFFERENT INSTRUMENT AND WAS NOT READ, CALLED OR PATTERNED ON. That file
 * is the old build's 270° dial, still drawn by `DockingPage` / `DockingPageCentral` / `Pages`; it is
 * not superseded by this and nothing here touches it. Two dial families exist on purpose while the
 * rebuild runs beside the flying pages.
 *
 * ---- ⭐ THE ONE THING THIS CLASS WILL NOT DO: CLASSIFY ----
 * §2.1: "Which one is used is decided by `Alarms.Band` (§6), never by the gauge. The gauge is handed
 * a severity and looks up a colour; it does not classify." So `Draw` takes a `Severity` and there is
 * exactly ONE severity→colour function here (`Colour`). §7's rule — the arc and the tab icon come
 * from the same function — is satisfied UPSTREAM, by both reading `Alarms.*`; it cannot be satisfied
 * by a second classifier in here, which is precisely what §6.2 says the mirrored five-band scheme
 * would have been.
 *
 * ---- ⚠ THREE NUMBERS THE SPEC DOES NOT CARRY, MEASURED HERE RATHER THAN CHOSEN ----
 * ⛔ Each is a gap in an owner-LOCKED file, so none of them is "settled" by being written here. They
 * are measured, the measurement is stated, and they are raised.
 *
 *  (1) THE TICK WIDTH — `BOB-33`, raised 2026-09-09 and still open. §2 gives both ticks a LENGTH
 *      (0.072 R, 0.135 R) and says the length "is the ONLY dimension that differs between the two
 *      tick types" — so there is one width for both, and it is nowhere in the file. Measured off
 *      §0's own source (`interface_2352x1410.png`) at the fitted centre (743.50, 330.66), R 111.57,
 *      by sampling luminance across each of the four threshold lines at four radii: the runs land
 *      between 1.0 and 2.2 px, i.e. 0.009–0.020 R, centred on ≈0.0134 R. That is the DOT DIAMETER
 *      (0.0137 R) to within the noise, and a tick that starts on the dotted line at the dots' own
 *      weight is the reading that needs no extra number — so `TickWidth = DotDiameter`, one constant,
 *      not two that can drift.
 *
 *  (2) THE TRACK GREY — §2.1's table says "track / dots / threshold ticks: grey, or the state colour
 *      when escalated" and never gives the grey. Measured on the same sheet, on PPO2's BARE track
 *      (the sector its arc does not cover, φ 20°..148°): the dots saturate at rgb(150,155,196).
 *      `#9499C3` is rgb(148,153,195) — two counts per channel, and it is a colour this design already
 *      names three times over (the unchecked marker and the rail titles in
 *      `SPEC_OVERVIEW_STATUS_ROWS.md` §7.1/§8.2). So the grey is not a new colour, it is that one.
 *
 *  (3) THE RING SEAM — §2 gives "dot pitch 2.66 deg (135 dots per full circle)" and §4.1 makes the
 *      track a FULL circle. 135 × 2.66 = 359.1, so the two halves of that line disagree by 0.9° —
 *      1.43 px of gap at the top row's R. ⭐ 135 DOTS WINS: `360/135 = 2.6667`, which is 0.0067° from
 *      the measured pitch (0.25 %) and closes the ring exactly, where honouring 2.66 leaves a seam
 *      the eye finds immediately on a closed circle. Raised as `BOB-36`.
 *
 * ---- ⚠ AND ONE THE SPEC CARRIES TWICE, DIFFERENTLY ----
 * §5.7 prints an invalid value as `--` (two hyphens). `Dashes.None` is the project's ONE glyph for
 * "there is no value" (`—`), and S148 exists because two dash glyphs had drifted apart. This file
 * follows §5.7 because SPEC_GAUGES is owner-locked and explicit, and the collision is raised rather
 * than settled here — the page's own panel follows `Dashes.None`, so ONE PAGE currently prints BOTH.
 * See `VehicleOverviewContent`'s header.
 */
using System;

namespace DragonScreen
{
    /// <summary>One dial's reading. ⛔ A `Severity`, never a value to classify — see the header.</summary>
    public struct DialReading
    {
        /// <summary>§2.2's three lines. `Value` is already formatted (§5.7: two decimal places on
        /// every channel, `--` when invalid) because `DisplayList.Text` may not allocate.</summary>
        public string Title, Value, Unit;
        /// <summary>Position on §1's scale, 0..1. Clamped by <see cref="DialGauge.Draw"/>.</summary>
        public double T;
        /// <summary>The verdict, computed upstream by `Alarms` (§6/§7). ⛔ NOT computed here.</summary>
        public Severity Sev;
        /// <summary>§5.7: false draws the track and the ticks, NO arc and NO tip tick. ⛔ An arc of
        /// length zero and an arc that is absent must not look the same.</summary>
        public bool Valid;
        /// <summary>§4.2: LOOP A / LOOP B are FULL CLOSED RINGS and take NO threshold ticks and no
        /// tip. Owner-ruled — "the temp loops depicted correctly", "no ticks on temp loops".</summary>
        public bool Loop;
    }

    public static class DialGauge
    {
        // ==========================================================================================
        //  §1 — THE SCALE. ⛔ Expressed in the project's ONE instrument convention (0 at twelve
        //  o'clock, increasing clockwise — `DisplayList.ArcBand`), NOT in the spec's own maths
        //  convention. §1 reads `angle(t) = 240 - t*300` with 0 east and anticlockwise positive;
        //  φ = 90 - θ converts it, giving -150 + 300 t. ⭐ The two agree at every t and the
        //  conversion is asserted in `DialGaugeTest` rather than trusted.
        // ==========================================================================================
        public const float StartDeg = -150f;
        public const float SweepDeg = 300f;

        /// <summary>§1's scale, in instrument degrees. t is clamped to 0..1.</summary>
        public static double Angle(double t)
        {
            if (t < 0.0) t = 0.0; else if (t > 1.0) t = 1.0;
            return StartDeg + SweepDeg * t;
        }

        // ==========================================================================================
        //  §2 — GEOMETRY, AS RATIOS OF THE TRACK RADIUS R, so they survive any resolution.
        // ==========================================================================================
        /// <summary>§2: 0.0137 R (1.53 px at the reference's R = 111.57).</summary>
        public const float DotDiameter = 0.0137f;
        /// <summary>§2: 135 dots per full circle. ⭐ The COUNT is authoritative and the pitch is
        /// derived from it — see the header, `BOB-36`.</summary>
        public const int DotCount = 135;
        /// <summary>§2: 0.071 R, stroked INSIDE R so its outer edge sits on the dotted line.
        /// 🟢 OWNER 2026-09-09: "no one told you to move the arc. put it back and only do as i asked."
        /// ⛔ Do not re-centre it on the line, whatever the reference appears to do.</summary>
        public const float ArcThickness = 0.071f;
        /// <summary>§2: 0.072 R, from the dotted line INWARD only.</summary>
        public const float ThresholdTickLength = 0.072f;
        /// <summary>§2: 0.135 R. Longer than a threshold tick for ONE reason — with the arc stroked
        /// inside R, a tick of the same length is completely hidden by the arc body.</summary>
        public const float TipTickLength = 0.135f;
        /// <summary>⚠ NOT IN THE SPEC. Measured — see the header, `BOB-33`. One width for both tick
        /// types, because §2 says the length is the only dimension that differs.</summary>
        public const float TickWidth = DotDiameter;
        /// <summary>§4.3: 3.0°, owner-ruled ("more of a visible break", then "3 degree break").
        /// ⚠ The reference's own break is 0.60° — invisible at our R.</summary>
        public const float BreakDeg = 3.0f;

        /// <summary>
        /// §3's four threshold lines, MIRRORED: 0.087 + 0.913 = 1.000 and 0.193 + 0.807 = 1.000.
        ///
        /// ⛔ THEY ARE THE DIAL FACE, NOT BAND BOUNDARIES (§6.2). Treating them as boundaries and
        /// solving for a scale gave a cabin-pressure range that could not display a depressurisation;
        /// the build's real `Cabin.PressFullScale` puts the paper's 8.0 psia contingency mid-dial.
        /// The COLOUR comes from `CabinLimits` through `Alarms.Band`, never from these.
        /// </summary>
        public static readonly float[] Thresholds = { 0.087f, 0.193f, 0.807f, 0.913f };

        // ==========================================================================================
        //  §2.1 — THE COLOURS. MEASURED, and they were missing from the spec's own v1.
        //  ⛔ The guessed values #3B82F6 / #FACC15 / #EF4444 are WRONG and appear nowhere.
        // ==========================================================================================
        /// <summary>§2.1: 11,895 px of 12,348 across both loop rings, and the same value as the
        /// progress-bar fill — one blue page-wide.</summary>
        public static readonly Rgba Nominal = Rgba.Hex("298BFE");
        /// <summary>§2.1: 712 px, PPO2's arc.</summary>
        public static readonly Rgba Caution = Rgba.Hex("FFD733");
        /// <summary>§2.1: 946 px of 1,787, off CABIN TEMP's whole escalated track.</summary>
        public static readonly Rgba Emergency = Rgba.Hex("E73030");
        /// <summary>The track, the dots and the threshold ticks at rest. ⚠ Measured here, not in the
        /// spec — see the header (2).</summary>
        public static readonly Rgba Track = Rgba.Hex("9499C3");
        /// <summary>§2.2: all three lines of text are `#FFFFFF`. ⛔ "The title and unit only *look*
        /// dimmer than the value because they are thinner — do not dim them."</summary>
        public static readonly Rgba Ink = new Rgba(1f, 1f, 1f, 1f);

        /// <summary>
        /// §2.1 + §7 — THE ONE severity→colour lookup for this family.
        ///
        /// ⛔ A LOOKUP, NOT A CLASSIFIER. It is handed the verdict `Alarms` already reached, so a dial
        /// and the tab strip that reads the same `Alarms` function cannot disagree. A second function
        /// that decided severity from a value would be exactly what §7 calls "worse than no colour at
        /// all", and §6.2 shows the shape it takes.
        ///
        /// ⚠ It is deliberately NOT `Alarms.Colour`. That returns the FLIGHT palette
        /// (`DragonPalette.Go/Caution/Alarm`) which the flown pages use; these three are §2.1's
        /// measured values for THIS design. Same severity, different ink — one source, two palettes.
        /// </summary>
        public static Rgba Colour(Severity sev)
        {
            if (sev == Severity.Alarm) return Emergency;
            if (sev == Severity.Caution) return Caution;
            return Nominal;
        }

        /// <summary>§5c: in an EMERGENCY the whole dotted track and all four threshold lines take the
        /// state colour. ⛔ Caution does not escalate — owner-approved knowing the evidence is one
        /// example (CABIN TEMP is the reference's only emergency dial).</summary>
        public static Rgba TrackColour(Severity sev)
        {
            return sev == Severity.Alarm ? Emergency : Track;
        }

        // ==========================================================================================
        //  §2.2 — THE TEXT INSIDE THE DIAL. Ink heights and ink CENTRES as ratios of R.
        //  ⛔ "Set the pixel size so the measured ink height matches, and ASSERT THE INK HEIGHT, not
        //  the font size." The two conversions live in `Typography` (one place, both measured).
        // ==========================================================================================
        public const float TitleInk = 0.125f, TitleCentre = -0.432f;
        public const float ValueInk = 0.314f, ValueCentre = -0.033f;
        public const float UnitInk = 0.188f, UnitCentre = 0.532f;

        /// <summary>
        /// Worst-case commands for one dial: 135 dots + 4 threshold ticks + 5 arc segments (four
        /// breaks split the strip into at most five) + 1 tip tick + 3 lines of text.
        ///
        /// ⛔ 135 OF THOSE 148 ARE DOTS, and that is the measured cost of §4.1's owner-ruled full
        /// circle — 1080 commands for the eight dials before anything else is drawn. It is stated
        /// here so nobody has to re-derive it from a profiler: `VehicleOverviewContent` sums it.
        /// </summary>
        public const int Commands = DotCount + 4 + 5 + 1 + 3;

        /// <summary>
        /// Draw one dial, centred at (cx, cy) in DESIGN px with track radius r, through `fit`.
        ///
        /// ⚠ ORDER MATTERS AND IS THE DESIGN'S: dots, then ticks, then the coloured strip, then the
        /// tip, then the text. The strip is drawn OVER the dots it covers because the reference does
        /// — the dots are the empty part of the scale.
        /// </summary>
        public static void Draw(DisplayList dl, BaseFit fit, float cx, float cy, float r,
                                DialReading d)
        {
            if (r <= 0f) return;
            Rgba trackCol = TrackColour(d.Sev);
            Rgba stateCol = Colour(d.Sev);

            // ---- §4.1 the dotted track, the FULL circle. Owner: "the bottom one has the dotted
            // lines that go full circle." ⛔ 135 dots, evenly spaced so the ring closes (BOB-36).
            float dot = DotDiameter * r;
            for (int i = 0; i < DotCount; i++)
            {
                double a = StartDeg + i * (360.0 / DotCount);
                float x, y;
                Polar(cx, cy, r, a, out x, out y);
                dl.Rect(fit.X(x - dot * 0.5f), fit.Y(y - dot * 0.5f), fit.S(dot), fit.S(dot), trackCol);
            }

            // ---- §3 the four threshold lines: on the dotted line, INWARD ONLY, never outside it.
            // §4.2: the loops take none.
            if (!d.Loop)
                for (int i = 0; i < Thresholds.Length; i++)
                    Tick(dl, fit, cx, cy, r, Angle(Thresholds[i]), ThresholdTickLength, trackCol);

            if (d.Valid)
            {
                if (d.Loop)
                {
                    // §4.2 — a FULL CLOSED RING in the state colour. A closed coolant loop needs a
                    // state, never a threshold, which is the reason the owner ruled for it.
                    dl.ArcBand(fit.X(cx), fit.Y(cy), fit.S(r * (1f - ArcThickness)), fit.S(r),
                               0.0, 360.0, stateCol);
                }
                else
                {
                    Strip(dl, fit, cx, cy, r, d.T, stateCol);
                    // §5b the TIP TICK — owner-identified: "a tick line that follows/attached the tip
                    // of the arc and the same colour as the arcs state at the time." The design's
                    // only needle: it moves with the reading and recolours with severity.
                    Tick(dl, fit, cx, cy, r, Angle(d.T), TipTickLength, stateCol);
                }
            }

            Line(dl, fit, cx, cy + TitleCentre * r, TitleInk * r, d.Title);
            Line(dl, fit, cx, cy + ValueCentre * r, ValueInk * r, d.Value);
            Line(dl, fit, cx, cy + UnitCentre * r, UnitInk * r, d.Unit);
        }

        /// <summary>
        /// §5a — the coloured strip from t = 0 to the value, BROKEN by <see cref="BreakDeg"/> at every
        /// threshold line it crosses, so the number of breaks tells you how many bands the value has
        /// passed.
        ///
        /// ⚠ THE BREAK IS CENTRED ON THE LINE, which the spec does not state and something had to
        /// decide (`BOB-37`). Centring is the only choice that leaves the threshold visible on BOTH
        /// sides of the gap; putting the gap wholly before or after the line hides it under the strip
        /// on one side and shifts every band edge by 1.5°.
        /// </summary>
        private static void Strip(DisplayList dl, BaseFit fit, float cx, float cy, float r,
                                  double t, Rgba col)
        {
            if (t < 0.0) t = 0.0; else if (t > 1.0) t = 1.0;
            float rIn = r * (1f - ArcThickness);
            double from = Angle(0.0);
            double end = Angle(t);
            for (int i = 0; i < Thresholds.Length; i++)
            {
                if (Thresholds[i] >= t) break;                 // not crossed: nothing to break
                double at = Angle(Thresholds[i]);
                double stop = at - BreakDeg * 0.5;
                if (stop > from) dl.ArcBand(fit.X(cx), fit.Y(cy), fit.S(rIn), fit.S(r), from, stop, col);
                from = at + BreakDeg * 0.5;
            }
            if (end > from) dl.ArcBand(fit.X(cx), fit.Y(cy), fit.S(rIn), fit.S(r), from, end, col);
        }

        /// <summary>A tick: starts ON the dotted line and runs INWARD by `len` * r. 🟢 OWNER: "the
        /// dotted line is the starting point for the tick and should only extend into the interior of
        /// the circle not the exterior."</summary>
        private static void Tick(DisplayList dl, BaseFit fit, float cx, float cy, float r,
                                 double angle, float len, Rgba col)
        {
            float x0, y0, x1, y1;
            Polar(cx, cy, r, angle, out x0, out y0);
            Polar(cx, cy, r * (1f - len), angle, out x1, out y1);
            dl.Line(fit.X(x0), fit.Y(y0), fit.X(x1), fit.Y(y1), fit.S(TickWidth * r), col);
        }

        /// <summary>One of §2.2's three lines: centred on the dial's x, its INK centred on `inkCy`,
        /// at the pixel size whose ink height is `ink`. ⛔ The ink is what is specified and what the
        /// tests assert; the pixel size is derived from it and never typed.</summary>
        private static void Line(DisplayList dl, BaseFit fit, float cx, float inkCy, float ink,
                                 string text)
        {
            if (string.IsNullOrEmpty(text) || ink <= 0f) return;
            float px = Typography.SizeForInk(ink);
            dl.Text(text, fit.X(cx), fit.Y(inkCy - Typography.CapCentreOfTop * px), fit.S(px),
                    TextAlign.Centre, Ink);
        }

        /// <summary>Instrument convention: 0 at twelve o'clock, increasing clockwise, y down.</summary>
        private static void Polar(float cx, float cy, float r, double deg, out float x, out float y)
        {
            double a = deg * Math.PI / 180.0;
            x = cx + (float)(r * Math.Sin(a));
            y = cy - (float)(r * Math.Cos(a));
        }
    }
}
