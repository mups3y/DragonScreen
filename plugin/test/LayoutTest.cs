/*
 * DragonScreen headless tests. No KSP, no Unity - builds and runs with the game closed.
 *
 * The point of these is the hairline problem. Everything else about this plugin can be eyeballed in
 * game; sub-pixel stroke rounding cannot, because a 0.39 px line and a 1 px line look similar in a
 * screenshot and completely different in motion.
 */
using System;
using DragonScreen;

public static class LayoutTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok)
        {
            failures++;
            Console.WriteLine("  FAIL  " + what + "   " + detail);
        }
    }

    static void Eq(string what, float got, float want, float tol)
    {
        Check(what, Math.Abs(got - want) <= tol,
              "got " + got.ToString("F4") + " want " + want.ToString("F4"));
    }

    // ---- QC-AUDIT finding 3: read one surface's word for MAIN BUS A / POWER 1 out of its own draw
    // list, so the test compares what the CREW sees on each page rather than what each page's code
    // says. The state line is the first text drawn after the row's label. ----
    static string WordAfter(DisplayList dl, string label)
    {
        for (int i = 0; i < dl.Count; i++)
            if (dl.At(i).Kind == DrawKind.Text && dl.At(i).Str == label)
                for (int j = i + 1; j < dl.Count; j++)
                    if (dl.At(j).Kind == DrawKind.Text) return dl.At(j).Str;
        return null;
    }

    static string TabBusWord(SystemsState sys)
    {
        PageState bs = new PageState(); bs.Valid = true; bs.Systems = sys;
        DisplayList bd = new DisplayList(VehicleSubsystemPage.Commands);
        VehicleSubsystemPage.Build(bd, 2560, 1406, VehicleSubsystemPage.Sub.Power, bs);
        return WordAfter(bd, "MAIN BUS A");
    }

    static string TreeBusWord(SystemsState sys)
    {
        PageState ts = new PageState(); ts.Valid = true; ts.Systems = sys;
        DisplayList td = new DisplayList(SystemsTreePage.Commands);
        SystemsTreePage.Build(td, 2560, 1406, ts);
        return WordAfter(td, "POWER 1");
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen layout tests");

        // ---- the hairline problem, which is the whole reason this file exists ----
        // Naive scaling of a 2 px stroke to a 670 px panel gives 0.39 px. That must never be the
        // number that reaches the renderer.
        // The invariant is SUB-PIXEL (< 1), not "< 0.5". The first version of this check hard-coded
        // 0.5, which was really a statement about the old 670 px panel; widening to 960 made the naive
        // value 0.56 and failed a test that was still describing the right problem. Assert the property
        // that matters, not a number that happens to hold at one panel size.
        float naive = ScreenLayout.RefStroke * ScreenLayout.Scale(ScreenLayout.PanelWidth);
        Check("naive scale really is sub-pixel (states the problem)", naive < 1f,
              "naive = " + naive.ToString("F4") + " px");

        Check("stroke never rounds to zero at panel width",
              ScreenLayout.StrokePx(ScreenLayout.RefStroke, ScreenLayout.PanelWidth) >= 1, "");

        // Floor of 1 must hold at absurdly small panels too - a collapsed or animating panel must not
        // make lines vanish.
        for (float w = 16f; w <= 4096f; w *= 1.7f)
        {
            Check("stroke >= 1 at panel width " + w.ToString("F0"),
                  ScreenLayout.StrokePx(ScreenLayout.RefStroke, w) >= 1, "");
        }

        // At the reference size itself the stroke must come back as exactly what the art specifies.
        Check("stroke round-trips at reference width",
              ScreenLayout.StrokePx(ScreenLayout.RefStroke, ScreenLayout.RefWidth) == 2,
              "got " + ScreenLayout.StrokePx(ScreenLayout.RefStroke, ScreenLayout.RefWidth));

        // Stroke must never get THINNER as the panel gets WIDER. That is the property that actually
        // matters and it is not implied by the floor: a rounding mistake could easily produce
        // 1,2,1,2 across a sweep and still satisfy ">= 1" everywhere.
        // (An earlier version of this loop asserted `s == (int)s` on an int, which csc flagged as
        //  CS1718 - it was vacuously true and tested nothing. Integrality is guaranteed by the
        //  return type; monotonicity is not, so that is what is checked.)
        int prevStroke = 0;
        for (float w = 100f; w <= 3000f; w += 137f)
        {
            int s = ScreenLayout.StrokePx(ScreenLayout.RefStroke, w);
            Check("stroke is monotonic at panel width " + w.ToString("F0"), s >= prevStroke,
                  "got " + s + " after " + prevStroke);
            prevStroke = s;
        }

        // ---- pixel snapping ----
        // Odd strokes straddle a pixel centre (x.5), even strokes sit on an edge (x.0).
        Eq("odd stroke snaps to half-pixel", ScreenLayout.SnapToPixel(10.3f, 1), 10.5f, 1e-4f);
        Eq("even stroke snaps to whole pixel", ScreenLayout.SnapToPixel(10.7f, 2), 10.0f, 1e-4f);
        Eq("snapping is stable when already snapped",
           ScreenLayout.SnapToPixel(ScreenLayout.SnapToPixel(42.9f, 1), 1), 42.5f, 1e-4f);

        // ---- panel SHAPE ----
        // The panel must be the same shape as the reference art. It was 670x870 (0.77:1 portrait)
        // against a 3427x2112 (1.62:1 landscape) reference - almost exactly inverted - and that was
        // only caught by looking at it in game. These checks make it a build failure instead.
        Eq("panel aspect matches the reference exactly",
           ScreenLayout.PanelWidth / ScreenLayout.PanelHeight, ScreenLayout.RefAspect, 1e-4f);
        Check("panel is LANDSCAPE, like the real Dragon displays",
              ScreenLayout.PanelWidth > ScreenLayout.PanelHeight,
              ScreenLayout.PanelWidth + "x" + ScreenLayout.PanelHeight);
        Check("reference itself is landscape (guards the constants)",
              ScreenLayout.RefWidth > ScreenLayout.RefHeight, "");
        // A uniform scale must therefore map the reference frame onto the panel with no leftover in
        // either axis - that is what "same shape" buys us, and it is why the height is derived.
        Eq("uniform scale maps reference height onto panel height",
           ScreenLayout.ToPanel(ScreenLayout.RefHeight, ScreenLayout.PanelWidth),
           ScreenLayout.PanelHeight, 0.5f);

        // ---- scale sanity ----
        Eq("scale is 1 at reference width", ScreenLayout.Scale(ScreenLayout.RefWidth), 1f, 1e-6f);
        Check("scale is guarded against zero width", ScreenLayout.Scale(0f) == 0f, "");
        Eq("position scales linearly",
           ScreenLayout.ToPanel(ScreenLayout.RefWidth, ScreenLayout.PanelWidth),
           ScreenLayout.PanelWidth, 1e-3f);

        // ---- palette ----
        // Guard the three structural colours by value. If someone "tidies" a hex these fail loudly,
        // because these exact numbers were cross-verified against two independent sources.
        Eq("background R", DragonPalette.Background.R, 0x02 / 255f, 1e-6f);
        Eq("background G", DragonPalette.Background.G, 0x07 / 255f, 1e-6f);
        Eq("background B", DragonPalette.Background.B, 0x38 / 255f, 1e-6f);
        Eq("panel B", DragonPalette.Panel.B, 0x52 / 255f, 1e-6f);
        Eq("accent G", DragonPalette.Accent.G, 0xFB / 255f, 1e-6f);
        Eq("alpha defaults to opaque", DragonPalette.Panel.A, 1f, 1e-6f);

        Rgba withHash = Rgba.Hex("#20FBFD");
        Rgba without = Rgba.Hex("20FBFD");
        Check("hex parses with and without '#'",
              Math.Abs(withHash.R - without.R) < 1e-6f && Math.Abs(withHash.B - without.B) < 1e-6f, "");

        // The text ladder must be monotonically dimming, or the hierarchy it encodes is broken.
        Rgba[] ladder = { DragonPalette.Text0, DragonPalette.Text1, DragonPalette.Text2,
                          DragonPalette.Text3, DragonPalette.Text4, DragonPalette.Text5,
                          DragonPalette.Text6, DragonPalette.Text7, DragonPalette.Text8 };
        for (int i = 1; i < ladder.Length; i++)
        {
            float prev = ladder[i - 1].R + ladder[i - 1].G + ladder[i - 1].B;
            float cur = ladder[i].R + ladder[i].G + ladder[i].B;
            Check("text ladder dims at step " + i, cur < prev,
                  "step " + i + " luma " + cur.ToString("F3") + " >= previous " + prev.ToString("F3"));
        }

        // ---- render target shape, measured off the screen mesh ----
        // These exist because 1024x640 flew and came back stretched. The failure was invisible in
        // the layout and obvious only in the proof pattern's circle, which is exactly the class of
        // bug a headless check is worth having: nothing about it can be eyeballed in a screenshot.

        // A flat quad has one near-zero extent. Which axis it is depends on how the artist built the
        // model, so all three orderings must give the same answer.
        Check("aspect ignores the flat axis, flat in Z",
              Math.Abs(ScreenLayout.AspectFromExtents(2f, 1f, 0.001f) - 2f) < 1e-4f, "");
        Check("aspect ignores the flat axis, flat in X",
              Math.Abs(ScreenLayout.AspectFromExtents(0.001f, 2f, 1f) - 2f) < 1e-4f, "");
        Check("aspect ignores the flat axis, flat in Y",
              Math.Abs(ScreenLayout.AspectFromExtents(1f, 0.001f, 2f) - 2f) < 1e-4f, "");

        // Always landscape, never the reciprocal - the U axis runs along the long side.
        Check("aspect is always >= 1", ScreenLayout.AspectFromExtents(1f, 2f, 0f) >= 1f, "");

        // Negative extents are possible from a mirrored scale, and must not flip the answer.
        Check("aspect survives a mirrored scale",
              Math.Abs(ScreenLayout.AspectFromExtents(-2f, -1f, 0f) - 2f) < 1e-4f, "");

        // Degenerate: a line or a point cannot be measured, and must report so rather than
        // returning something plausible that silently sizes a 1 px texture.
        Check("degenerate box reports 0", ScreenLayout.AspectFromExtents(1f, 0f, 0f) == 0f, "");

        Check("height derives from width and aspect",
              ScreenLayout.PixelHeightFor(1280, 2f) == 640, "");
        Check("height rounds rather than truncating",
              ScreenLayout.PixelHeightFor(1000, 3f) == 333, "");
        Check("unmeasurable aspect gives 0, not a division blow-up",
              ScreenLayout.PixelHeightFor(1280, 0f) == 0, "");
        Check("nonsense width gives 0", ScreenLayout.PixelHeightFor(0, 2f) == 0, "");

        // The round trip that matters: derive the height, and the shape you get back is the shape
        // the mesh actually is. This is the whole point of measuring instead of typing.
        float measured = ScreenLayout.AspectFromExtents(0.5334f, 0.2667f, 0.0f);
        int h = ScreenLayout.PixelHeightFor(1280, measured);
        Check("derived target matches the measured shape",
              Math.Abs((1280f / h) - measured) < 0.01f,
              "measured " + measured.ToString("F4") + " target 1280x" + h);

        // ---- display list ----
        // The structure both renderers walk. If this is wrong, the PNG preview and the IVA screen
        // disagree, and a preview that lies is worse than no preview.

        DisplayList dl = new DisplayList(8);
        dl.Rect(0f, 0f, 10f, 10f, DragonPalette.Accent);
        Check("Rect adds one command", dl.Count == 1, "got " + dl.Count);

        dl.Clear();
        dl.Box(0f, 0f, 10f, 10f, 1f, DragonPalette.Accent);
        Check("Box is four rects", dl.Count == 4, "got " + dl.Count);

        // A zero or negative stroke would emit four degenerate rects, which draw nothing but still
        // consume capacity - and capacity exhaustion is silent by design.
        dl.Clear();
        dl.Box(0f, 0f, 10f, 10f, 0f, DragonPalette.Accent);
        Check("Box with no stroke emits nothing", dl.Count == 0, "got " + dl.Count);

        // Overflow must DEGRADE, not throw: a page that runs long should lose its tail, not take
        // the screen out mid-flight. And it must say so.
        dl.Clear();
        for (int i = 0; i < 20; i++) dl.Rect(0f, 0f, 1f, 1f, DragonPalette.Accent);
        Check("overflow clamps to capacity", dl.Count == dl.Capacity,
              "count " + dl.Count + " capacity " + dl.Capacity);
        Check("overflow is reported", dl.Overflowed, "");
        dl.Clear();
        Check("Clear resets the overflow flag", !dl.Overflowed, "");

        // THE CAPACITY GUARD. ProofPage.Commands is a hand-written number and the page it sizes will
        // grow; this fails the build the moment the page outgrows it, instead of silently losing
        // whatever was drawn last. Screen 3 is the worst case - one command per identity bar.
        DisplayList proof = new DisplayList(ProofPage.Commands);
        ProofPage.Build(proof, 1280, 703, 3, 0.5, "SCREEN 3   1280x703");
        Check("ProofPage fits its declared capacity", !proof.Overflowed,
              "needs more than " + ProofPage.Commands + " commands");

        // THE RAMP MUST NOT LIE ABOUT ITSELF. Its whole purpose is to answer "how small can text be
        // on the real screen", and it does that by printing its own size next to each line. A label
        // that has drifted from the size it is drawn at gives a confident wrong answer - the worst
        // kind - so the two arrays are checked for length AND for the size appearing in the text.
        Check("ramp has one label per size",
              ProofPage.SizeLabels.Length == ProofPage.LegibilitySizes.Length,
              ProofPage.SizeLabels.Length + " labels for "
              + ProofPage.LegibilitySizes.Length + " sizes");
        for (int i = 0; i < ProofPage.LegibilitySizes.Length && i < ProofPage.SizeLabels.Length; i++)
        {
            string want = ((int)ProofPage.LegibilitySizes[i]).ToString() + " px";
            Check("ramp label " + i + " states its own size",
                  ProofPage.SizeLabels[i].StartsWith(want),
                  "'" + ProofPage.SizeLabels[i] + "' should start '" + want + "'");
        }

        // Text with no string or no size must emit nothing rather than a command that every renderer
        // then has to defend against.
        dl.Clear();
        dl.Text(null, 0f, 0f, 12f, TextAlign.Left, DragonPalette.Text0);
        dl.Text("", 0f, 0f, 12f, TextAlign.Left, DragonPalette.Text0);
        dl.Text("x", 0f, 0f, 0f, TextAlign.Left, DragonPalette.Text0);
        Check("empty or sizeless text emits nothing", dl.Count == 0, "got " + dl.Count);

        dl.Clear();
        dl.Text("ALT", 10f, 20f, 16f, TextAlign.Centre, DragonPalette.Text0);
        Check("text survives the round trip",
              dl.Count == 1 && dl.At(0).Kind == DrawKind.Text && dl.At(0).Str == "ALT"
              && dl.At(0).Align == TextAlign.Centre && dl.At(0).C == 16f, "");

        // Both renderers size their vertex scratch from this, so a silent clamp here is a silently
        // truncated arc there.
        Check("a full circle fits the arc scratch",
              ArcGeometry.VertexCount(360.0, 2.0) <= 256,
              "needs " + ArcGeometry.VertexCount(360.0, 2.0) + " points");

        // ArcScreen must mirror in Y and nothing else: at 0 degrees - twelve o'clock - the point is
        // ABOVE the centre, which in a top-left origin means a SMALLER y.
        float[] pts = new float[4];
        ArcGeometry.ArcScreen(pts, 2, 100.0, 100.0, 50.0, 0.0, 90.0);
        Check("ArcScreen puts 0 degrees above centre", pts[1] < 100f,
              "y " + pts[1].ToString("F2"));
        Check("ArcScreen puts 90 degrees right of centre", pts[2] > 100f,
              "x " + pts[2].ToString("F2"));

        // ---- the chrome bar ----
        // ALERT MUST BEAT SELECTED. This is the one behaviour the bar exists for: the colour is
        // carrying "something is wrong on that page", and losing it to a selection highlight would
        // hide exactly the thing the pilot needs. It is one line in ChromeBar and trivially
        // re-broken by anyone tidying the conditional, so it is pinned here.
        DisplayList bar = new DisplayList(ChromeBar.Commands);
        ChromeState st = new ChromeState();
        st.Met = "T+ 00:00:00"; st.VehicleState = "NOMINAL";
        st.LinkName = "COM1/TLM"; st.LinkTimer = "00:00:00"; st.LinkUp = true;
        st.SelectedPage = 1;
        st.AlertMask = 1 << 1;            // the SELECTED page is also the one in alarm
        ChromeBar.Build(bar, 1280, 703, st);

        Rgba got = new Rgba(-1f, -1f, -1f, -1f);
        for (int i = 0; i < bar.Count; i++)
            if (bar.At(i).Kind == DrawKind.Text && bar.At(i).Str == ChromeBar.PageNames[1])
                got = bar.At(i).Colour;
        Check("alert colour beats selected colour",
              Math.Abs(got.R - DragonPalette.Alarm.R) < 1e-6f
              && Math.Abs(got.G - DragonPalette.Alarm.G) < 1e-6f,
              "got " + got.R.ToString("F3") + "," + got.G.ToString("F3"));
        Check("chrome fits its declared capacity", !bar.Overflowed,
              "needs more than " + ChromeBar.Commands);

        // Loss of signal must be visible, not a subtle grey. Same reasoning as the alert.
        bar.Clear(); st.AlertMask = 0; st.LinkUp = false;
        ChromeBar.Build(bar, 1280, 703, st);
        Rgba linkC = new Rgba(-1f, -1f, -1f, -1f);
        for (int i = 0; i < bar.Count; i++)
            if (bar.At(i).Kind == DrawKind.Text && bar.At(i).Str == st.LinkTimer)
                linkC = bar.At(i).Colour;
        Check("link down draws as alarm",
              Math.Abs(linkC.R - DragonPalette.Alarm.R) < 1e-6f, "");

        // Null strings must not crash a renderer walking the list.
        bar.Clear();
        ChromeBar.Build(bar, 1280, 703, new ChromeState());
        Check("chrome survives an empty state", bar.Count > 0 && !bar.Overflowed, "");

        // The bar sits ON the bottom edge, whatever the screen height - and the three real screens
        // are NOT the same height.
        Eq("chrome bottom lands on the screen edge, 1280x703",
           ChromeBar.TopY(1280, 703) + ChromeBar.HeightFor(1280), 703f, 1e-4f);
        Eq("chrome bottom lands on the screen edge, 1280x710",
           ChromeBar.TopY(1280, 710) + ChromeBar.HeightFor(1280), 710f, 1e-4f);
        // ⛔ AND AT THE SHIPPED WIDTH TOO ([[S120]]). The two checks above passed for ten days while
        // the bar was physically half the size it was designed as, because both ran at 1280 - the
        // one width where HeightFor(w) and the bare Height constant agree. A bar bolted to the
        // bottom edge is not the same claim as a bar of the right SIZE, and only a second width
        // can tell them apart. This is R-02's lesson applied to the bar's own geometry.
        Eq("chrome bottom lands on the screen edge, 2560x1406",
           ChromeBar.TopY(2560, 1406) + ChromeBar.HeightFor(2560), 1406f, 1e-4f);
        Eq("the bar is the measured 64 px at the width it was measured at",
           ChromeBar.HeightFor(1280), ChromeBar.Height, 1e-4f);
        Eq("...and twice that on a panel twice as wide - the same fraction of the glass",
           ChromeBar.HeightFor(2560), 128f, 1e-4f);
        Check("the bar is the same fraction of the panel height at both shipped sizes",
              Math.Abs(ChromeBar.HeightFor(1280) / 703f - ChromeBar.HeightFor(2560) / 1406f) < 1e-6f,
              ChromeBar.HeightFor(1280) / 703f + " vs " + ChromeBar.HeightFor(2560) / 1406f);

        // AlertMask is an int, so the page set must stay inside its bits with room to spare.
        Check("page count fits the alert bitmask",
              ChromeBar.PageNames.Length > 0 && ChromeBar.PageNames.Length <= 31,
              ChromeBar.PageNames.Length + " pages");

        // Nothing on the bar may drop below the measured glanceable floor.
        // ⛔ MinFor(1280), NOT Min. The bar above is BUILT at 1280, so the two are the same number
        // today - but writing the width down is the whole of QC R-02: a size compared against a bare
        // constant instead of against the panel it is drawn on is how a resolution change made every
        // legibility check in the build twice as permissive without one test going red. If this Build
        // width ever moves, the floor now follows it instead of silently lagging behind.
        for (int i = 0; i < bar.Count; i++)
            if (bar.At(i).Kind == DrawKind.Text)
                Check("chrome text >= the glanceable floor at the width it is drawn at",
                      bar.At(i).C >= Typography.MinFor(1280),
                      "'" + bar.At(i).Str + "' at " + bar.At(i).C + " px");

        // ---- page link hit testing ----
        // LAYOUT AND HIT TESTING MUST AGREE. Both go through ChromeBar.LinkRect, and these checks are
        // what keep that true: a clickable area computed apart from where the label is drawn drifts
        // the first time either is edited, and the symptom - "it works but you must click slightly
        // left of the word" - is maddening to find and trivial to prevent.
        int W = 1280, H = 703;
        for (int i = 0; i < ChromeBar.PageNames.Length; i++)
        {
            float lx, ly, lw, lh;
            ChromeBar.LinkRect(i, W, H, out lx, out ly, out lw, out lh);
            Check("link " + i + " centre hits itself",
                  ChromeBar.HitTest(lx + lw * 0.5f, ly + lh * 0.5f, W, H) == i,
                  "got " + ChromeBar.HitTest(lx + lw * 0.5f, ly + lh * 0.5f, W, H));
            // Edges: inclusive at the near edge, exclusive at the far one, so neighbours cannot both
            // claim the same pixel column.
            Check("link " + i + " claims its left edge",
                  ChromeBar.HitTest(lx, ly + 1f, W, H) == i, "");
            Check("link " + i + " does NOT claim its right edge",
                  ChromeBar.HitTest(lx + lw, ly + 1f, W, H) != i, "");
        }

        // Above the bar is page content, not chrome - a touch there must not select a page.
        Check("a touch above the bar hits nothing",
              ChromeBar.HitTest(100f, ChromeBar.TopY(W, H) - 1f, W, H) == -1, "");
        // Left of the first link is padding.
        Check("the left pad hits nothing",
              ChromeBar.HitTest(1f, ChromeBar.TopY(W, H) + 10f, W, H) == -1, "");
        // The right-hand readouts are not links.
        Check("the readout area hits nothing",
              ChromeBar.HitTest(W - 60f, ChromeBar.TopY(W, H) + 10f, W, H) == -1, "");
        // Off-screen and negative coordinates must not crash or wrap.
        Check("negative coordinates hit nothing", ChromeBar.HitTest(-50f, -50f, W, H) == -1, "");
        Check("far off-screen hits nothing", ChromeBar.HitTest(99999f, 99999f, W, H) == -1, "");

        // The links must fit on the narrowest real screen, or the last one is unreachable.
        {
            float lx, ly, lw, lh;
            ChromeBar.LinkRect(ChromeBar.PageNames.Length - 1, W, H, out lx, out ly, out lw, out lh);
            Check("the last page link fits on screen", lx + lw <= W,
                  "last link ends at " + (lx + lw) + " on a " + W + " px screen");
        }

        // ---- page selection persistence ----
        // This reads a string a USER CAN EDIT and that an older build may have written. Every
        // malformed case must resolve to the default, never to an exception: a save that refuses to
        // load because a screen remembered the wrong page would be an absurd way to lose a flight.
        int PC = ChromeBar.PageNames.Length;
        Check("round trip", PageSelection.Get(PageSelection.Set("", 2, 3, PC), 2, PC, 0) == 3, "");
        Check("other screens untouched",
              PageSelection.Get(PageSelection.Set("1,2,3", 2, 4, PC), 1, PC, 9) == 1, "");
        Check("empty falls back", PageSelection.Get("", 1, PC, 7) == 7, "");
        Check("null falls back", PageSelection.Get(null, 1, PC, 7) == 7, "");
        Check("truncated falls back", PageSelection.Get("2", 3, PC, 7) == 7, "");
        Check("garbage falls back", PageSelection.Get("a,b,c", 2, PC, 7) == 7, "");
        Check("negative falls back", PageSelection.Get("-1,0,0", 1, PC, 7) == 7, "");
        Check("out-of-range page falls back", PageSelection.Get("99,0,0", 1, PC, 7) == 7, "");
        Check("screen 0 is not a screen", PageSelection.Get("1,2,3", 0, PC, 7) == 7, "");
        Check("screen 4 is not a screen", PageSelection.Get("1,2,3", 4, PC, 7) == 7, "");
        Check("whitespace tolerated", PageSelection.Get(" 1 , 2 , 3 ", 2, PC, 9) == 2, "");
        // A short or malformed string must be REPAIRED by a write, not propagated.
        Check("Set repairs a short string",
              PageSelection.Set("9", 3, 1, PC).Split(',').Length == PageSelection.Screens, "");
        Check("Set rejects an impossible page", PageSelection.Set("0,0,0", 1, 99, PC) == "0,0,0", "");

        // Page names resolve from the cfg, case-insensitively - a hand-written "Flight" is not a
        // mistake worth punishing with a silently wrong default.
        Check("page name resolves", PageSelection.IndexOfName(ChromeBar.PageNames, "NAV") == 2, "");
        Check("page name is case-insensitive",
              PageSelection.IndexOfName(ChromeBar.PageNames, "flight") == 0, "");
        Check("unknown page name is -1",
              PageSelection.IndexOfName(ChromeBar.PageNames, "TELEMETRY") == -1, "");
        Check("empty page name is -1", PageSelection.IndexOfName(ChromeBar.PageNames, "") == -1, "");

        // Every page must fit its declared capacity, including the placeholders.
        for (int i = 0; i < ChromeBar.PageNames.Length; i++)
        {
            DisplayList pg = new DisplayList(Pages.Commands);
            PageState ps = new PageState();
            ps.Valid = true; ps.Phase = "ORBITING"; ps.Altitude = "123.4 km";
            ps.Velocity = "2280 m/s"; ps.Apoapsis = "124.0 km"; ps.Periapsis = "121.9 km";
            ps.Body = "KERBIN"; ps.ApogeeShown = true; ps.PerigeeShown = true;
            Pages.Build(pg, i, 1280, 703, ps, MapProjection.Default(), 1);
            Check("page " + ChromeBar.PageNames[i] + " fits its capacity", !pg.Overflowed, "");
            Check("page " + ChromeBar.PageNames[i] + " draws something", pg.Count > 0, "");
        }

        // An invalid state must produce dashes, never a plausible zero - a screen confidently
        // reading 0.0 km is indistinguishable from a dead feed.
        {
            DisplayList pg = new DisplayList(Pages.Commands);
            // Valid = false
            Pages.Build(pg, 0, 1280, 703, new PageState(), MapProjection.Default(), 1);
            bool anyZero = false;
            for (int i = 0; i < pg.Count; i++)
                if (pg.At(i).Kind == DrawKind.Text && pg.At(i).Str == "0") anyZero = true;
            Check("an invalid feed never draws a bare zero", !anyZero, "");
        }

        // ---- the launchpad bug, pinned ----
        // Shipped reading 175 m/s standing still on the pad, because that IS the orbital speed:
        // Kerbin's rotation. True, and useless. These checks say which number each regime shows and
        // that the caption names it, so nobody can "simplify" the branch away later.
        {
            PageState g = new PageState();
            g.Valid = true; g.Regime = FlightRegime.Ground;
            g.Velocity = "175 m/s"; g.SurfaceVelocity = "0 m/s";
            g.Apoapsis = "75 m"; g.Periapsis = "-598.4 km";
            g.Phase = "PRELAUNCH"; g.Altitude = "115 m"; g.Body = "KERBIN";

            DisplayList pg = new DisplayList(Pages.Commands);
            Pages.Build(pg, 0, 1280, 703, g, MapProjection.Default(), 1);

            bool sawSurface = false, sawOrbitalNumber = false, sawSurfaceCaption = false;
            bool sawNegativePeriapsis = false;
            for (int i = 0; i < pg.Count; i++)
            {
                if (pg.At(i).Kind != DrawKind.Text) continue;
                string t = pg.At(i).Str;
                if (t == "0 m/s") sawSurface = true;
                if (t == "175 m/s") sawOrbitalNumber = true;
                if (t == "SURFACE VELOCITY") sawSurfaceCaption = true;
                if (t == "-598.4 km") sawNegativePeriapsis = true;
            }
            Check("on the ground VELOCITY shows surface speed", sawSurface, "");
            Check("on the ground the orbital 175 m/s is NOT shown", !sawOrbitalNumber, "");
            Check("the caption says which velocity it is", sawSurfaceCaption, "");
            Check("on the ground periapsis is not a hole in the planet",
                  !sawNegativePeriapsis, "");
        }

        // In space the other branch must be taken, and named.
        {
            PageState o = new PageState();
            o.Valid = true; o.Regime = FlightRegime.Space;
            o.Velocity = "2.28 km/s"; o.SurfaceVelocity = "2.10 km/s";
            o.Apoapsis = "124.0 km"; o.Periapsis = "121.9 km";
            o.Phase = "ORBITING"; o.Altitude = "123.4 km"; o.Body = "KERBIN";
            o.ApogeeShown = true; o.PerigeeShown = true;

            DisplayList pg = new DisplayList(Pages.Commands);
            Pages.Build(pg, 0, 1280, 703, o, MapProjection.Default(), 1);

            bool sawOrbital = false, sawOrbitalCaption = false, sawApo = false;
            for (int i = 0; i < pg.Count; i++)
            {
                if (pg.At(i).Kind != DrawKind.Text) continue;
                string t = pg.At(i).Str;
                if (t == "2.28 km/s") sawOrbital = true;
                if (t == "ORBITAL VELOCITY") sawOrbitalCaption = true;
                if (t == "124.0 km") sawApo = true;
            }
            Check("in space VELOCITY shows orbital speed", sawOrbital, "");
            Check("in space the caption says orbital", sawOrbitalCaption, "");
            Check("in space apoapsis is shown", sawApo, "");
        }

        // Sub-orbital is a real trajectory - an ascending rocket's apoapsis is the number being
        // watched, so it must NOT be dashed out with the landed case.
        {
            PageState a = new PageState();
            a.Valid = true; a.Regime = FlightRegime.Atmosphere;
            a.Apoapsis = "42.0 km"; a.Periapsis = "-500.0 km";
            a.Velocity = "1.20 km/s"; a.SurfaceVelocity = "1.15 km/s";
            a.ApogeeShown = true;   // ascending: apogee is exactly what is being watched
            a.PerigeeShown = false; // ...while perigee is still the radius artefact
            DisplayList pg = new DisplayList(Pages.Commands);
            Pages.Build(pg, 0, 1280, 703, a, MapProjection.Default(), 1);
            bool sawApo = false, sawSurfaceCaption = false;
            for (int i = 0; i < pg.Count; i++)
            {
                if (pg.At(i).Kind != DrawKind.Text) continue;
                if (pg.At(i).Str == "42.0 km") sawApo = true;
                if (pg.At(i).Str == "SURFACE VELOCITY") sawSurfaceCaption = true;
            }
            Check("in atmosphere apoapsis is still shown", sawApo, "");
            Check("in atmosphere velocity is surface-relative", sawSurfaceCaption, "");
        }

        // ---- mission phase ----
        // ACTIVE PHASE is the REAL vehicle's phase, not KSP's Vessel.Situations. Order of precedence
        // is the safety-critical part: a screen reading ENTRY while the mains are out is worse than
        // useless, so chutes must beat everything altitude-based.
        {
            MissionInputs m = new MissionInputs();
            m.Regime = FlightRegime.Ground;
            Check("on the pad", Mission.Classify(m) == MissionPhase.Prelaunch, "");

            m.VerticalSpeed = 12.0;
            Check("climbing off the pad is ascent", Mission.Classify(m) == MissionPhase.Ascent, "");

            m.Regime = FlightRegime.Atmosphere; m.VerticalSpeed = 200.0;
            Check("climbing in atmosphere is ascent", Mission.Classify(m) == MissionPhase.Ascent, "");
            m.VerticalSpeed = -300.0;
            Check("falling in atmosphere is entry", Mission.Classify(m) == MissionPhase.Entry, "");

            // Chutes beat entry, and mains beat drogues.
            m.DroguesOut = true;
            Check("drogues beat entry", Mission.Classify(m) == MissionPhase.Drogues, "");
            m.MainsOut = true;
            Check("mains beat drogues", Mission.Classify(m) == MissionPhase.Mains, "");
            m.Splashed = true;
            Check("splashdown beats everything", Mission.Classify(m) == MissionPhase.Splashdown, "");
        }
        {
            MissionInputs m = new MissionInputs();
            m.Regime = FlightRegime.Space;
            Check("in space with no target is coast", Mission.Classify(m) == MissionPhase.Coast, "");

            m.HasTarget = true; m.TargetRange = 40000.0;
            // ⭐ U1: targeted + in space but orbit NOT closed (mid-insertion, pe sub-orbital) is still ASCENT.
            Check("targeted but orbit not closed is still ascent (U1)", Mission.Classify(m) == MissionPhase.Ascent, "");
            m.OrbitClosed = true;
            Check("far from the target is phasing", Mission.Classify(m) == MissionPhase.Phasing, "");
            m.TargetRange = Mission.ApproachRange - 1.0;
            Check("inside 3 km is approach", Mission.Classify(m) == MissionPhase.Approach, "");
            m.TargetRange = Mission.ApproachRange;
            Check("exactly 3 km is approach", Mission.Classify(m) == MissionPhase.Approach, "");

            m.Docked = true;
            Check("docked beats approach", Mission.Classify(m) == MissionPhase.Docked, "");
        }
        // Every phase must have a display name - an unnamed one shows as a blank on the glass.
        foreach (MissionPhase mp in Enum.GetValues(typeof(MissionPhase)))
        {
            string n = Mission.Name(mp);
            Check("phase " + mp + " has a name", !string.IsNullOrEmpty(n), "");
            if (mp != MissionPhase.Unknown)
                Check("phase " + mp + " is not a dash", n != "-", "");
        }
        // The real chute altitudes, pinned. F9I arms drogues at 7500 m, which is ~2 km high; these
        // are the published numbers and the ones the display must be held to.
        Eq("drogue altitude is 18000 ft", (float)Mission.DrogueAltitude, 5486f, 1f);
        Eq("main altitude is 6000 ft", (float)Mission.MainAltitude, 1830f, 1f);
        Check("drogues deploy above mains", Mission.DrogueAltitude > Mission.MainAltitude, "");

        // ---- gauges ----
        // The dial is the reference's shape: 270 deg, symmetric about twelve o'clock, opening down.
        Eq("dial sweeps 270 degrees", (float)(Gauge.EndDeg - Gauge.StartDeg), 270f, 1e-4f);
        Check("dial is symmetric about twelve o'clock",
              Math.Abs(Gauge.StartDeg + Gauge.EndDeg) < 1e-9, "");

        // The TRACK is always drawn, even at zero. An empty ring reads "this is zero"; a missing ring
        // reads as nothing at all, and that difference matters when the value is propellant.
        {
            DisplayList g = new DisplayList(Gauge.Commands);
            Gauge.Ring(g, 100f, 100f, 50f, 8f, 0.0, DragonPalette.Panel, DragonPalette.Accent);
            Check("a zero gauge still draws its track", g.Count >= 1, "");
            int atZero = g.Count;
            g.Clear();
            Gauge.Ring(g, 100f, 100f, 50f, 8f, 0.5, DragonPalette.Panel, DragonPalette.Accent);
            Check("a half gauge draws track AND fill", g.Count == atZero + 1, "got " + g.Count);
        }

        // ---- THE DIAL NO LONGER CARRIES THE ALARM ----
        // These checks used to assert that Gauge.LowIsBad returned red. That behaviour was WRONG
        // about the real vehicle and was replaced on 2026-08-06: every dial has a fixed identity
        // colour and alarm is routed through the chrome bar instead (user's call - see Alarms).
        // The threshold logic did not go away, so the checks did not either; they moved to
        // PageTest.AlarmRouting and now assert a SEVERITY. What is left here is the property that
        // replaced them: a dial's colour must not depend on its value.
        {
            DisplayList lo = new DisplayList(Gauge.Commands * 2);
            DisplayList hi = new DisplayList(Gauge.Commands * 2);
            Gauge.Labelled(lo, 100f, 100f, 50f, 8f, 0.02, "2", "%", "PROPELLANT",
                           DragonPalette.GaugeTrack, DragonPalette.GaugePropellant);
            Gauge.Labelled(hi, 100f, 100f, 50f, 8f, 0.98, "98", "%", "PROPELLANT",
                           DragonPalette.GaugeTrack, DragonPalette.GaugePropellant);

            Rgba loFill = new Rgba(0f, 0f, 0f, 0f), hiFill = new Rgba(0f, 0f, 0f, 0f);
            for (int i = 0; i < lo.Count; i++)
                if (lo.At(i).Kind == DrawKind.ArcBand) loFill = lo.At(i).Colour;
            for (int i = 0; i < hi.Count; i++)
                if (hi.At(i).Kind == DrawKind.ArcBand) hiFill = hi.At(i).Colour;

            Check("a dial keeps its colour when nearly empty",
                  loFill.R == DragonPalette.GaugePropellant.R
                  && loFill.G == DragonPalette.GaugePropellant.G, "");
            Check("a dial keeps its colour when nearly full",
                  hiFill.R == DragonPalette.GaugePropellant.R
                  && hiFill.G == DragonPalette.GaugePropellant.G, "");
        }

        // Out-of-range must clamp, not wrap - a needle that leaves the dial is worse than one that
        // pegs, because pegged reads as "at or beyond the limit", which is true.
        Eq("gauge clamps above full",
           (float)ArcGeometry.ValueToAngle(1.7, 0, 1, Gauge.StartDeg, Gauge.EndDeg),
           (float)Gauge.EndDeg, 1e-4f);
        Eq("gauge clamps below empty",
           (float)ArcGeometry.ValueToAngle(-0.4, 0, 1, Gauge.StartDeg, Gauge.EndDeg),
           (float)Gauge.StartDeg, 1e-4f);

        // ---- images ----
        // Images.Size is DECLARED in src/pure because both renderers must agree on the aspect to the
        // pixel. That means it can drift from the file that actually ships. Read the PNG header and
        // fail the build if it has, rather than letting the preview place it one way and the game
        // another.
        foreach (ImageId iid in Enum.GetValues(typeof(ImageId)))
        {
            if (iid == ImageId.None) continue;
            // A RUNTIME image has no file, no shipped bytes and no size known ahead of time -
            // ImageId.BodyMap is KSP's own scaled-space texture for whatever body the vessel is at.
            // Asserting it exists on disk would fail the build over an asset that is correct by not
            // existing. The property that DOES matter for it is the opposite one, below.
            if (Images.IsRuntime(iid))
            {
                Check("runtime image " + iid + " has no filename",
                      string.IsNullOrEmpty(Images.FileName(iid)), "");
                continue;
            }
            string fn = Images.FileName(iid);
            Check("image " + iid + " has a filename", !string.IsNullOrEmpty(fn), "");
            int dw, dh;
            Images.Size(iid, out dw, out dh);
            Check("image " + iid + " declares a size", dw > 0 && dh > 0, "");

            // Optional (user-supplied) art is not shipped and its size is nominal — don't require it on disk.
            if (Images.IsOptional(iid)) continue;

            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "GameData", "DragonScreen", "art", fn ?? ""));
            if (System.IO.File.Exists(path))
            {
                byte[] hdr = new byte[24];
                using (var fs = System.IO.File.OpenRead(path)) fs.Read(hdr, 0, 24);
                // PNG IHDR: width and height are big-endian 32-bit at offsets 16 and 20.
                int fw = (hdr[16] << 24) | (hdr[17] << 16) | (hdr[18] << 8) | hdr[19];
                int fh = (hdr[20] << 24) | (hdr[21] << 16) | (hdr[22] << 8) | hdr[23];
                Check("image " + iid + " declared size matches the shipped file",
                      fw == dw && fh == dh,
                      "file " + fw + "x" + fh + " declared " + dw + "x" + dh);
            }
            else Check("image " + iid + " is present in GameData art", false, path);
        }

        // FitHeight must preserve aspect and centre - a squashed Dragon is the most visible possible
        // sign that nobody checked.
        {
            float ix, iy, iw, ih;
            bool ok = Images.FitHeight(ImageId.Dragon, 640f, 350f, 400f,
                                       out ix, out iy, out iw, out ih);
            Check("FitHeight succeeds for a known image", ok, "");
            int dw, dh; Images.Size(ImageId.Dragon, out dw, out dh);
            Eq("FitHeight preserves aspect", iw / ih, (float)dw / dh, 1e-3f);
            Eq("FitHeight centres in x", ix + iw * 0.5f, 640f, 1e-3f);
            Eq("FitHeight centres in y", iy + ih * 0.5f, 350f, 1e-3f);
            Check("FitHeight rejects an unknown image",
                  !Images.FitHeight(ImageId.None, 0f, 0f, 100f, out ix, out iy, out iw, out ih), "");
        }

        // The tint is a MULTIPLY, so white must be the identity or every image draws dimmed.
        Check("White is the untinted identity",
              DragonPalette.White.R == 1f && DragonPalette.White.G == 1f
              && DragonPalette.White.B == 1f && DragonPalette.White.A == 1f, "");

        // ---- cabin environment ----
        // SIMULATED, not faked: every reading must MOVE and must move BECAUSE OF SOMETHING. A
        // constant with noise on it would be indistinguishable from a dead sensor, which is exactly
        // what the no-invented-telemetry rule exists to prevent.
        {
            CabinInputs a = new CabinInputs();
            a.Crew = 0; a.CrewCapacity = 4; a.HullTempC = 22.0;
            a.MissionTime = 100.0; a.Power01 = 0.9; a.Powered = true;
            CabinReadout ra = Cabin.Compute(a);

            CabinInputs b = a; b.Crew = 4;
            CabinReadout rb = Cabin.Compute(b);
            Check("more crew raises CO2", rb.Co2MmHg > ra.Co2MmHg,
                  ra.Co2MmHg.ToString("F2") + " -> " + rb.Co2MmHg.ToString("F2"));
            Check("more crew lowers PPO2", rb.Ppo2Psia < ra.Ppo2Psia, "");

            // A hot hull must warm the cabin and the loops - that is the REAL input doing the work,
            // and it is what makes the page come alive during entry.
            CabinInputs hot = a; hot.HullTempC = 900.0;
            CabinReadout rh = Cabin.Compute(hot);
            Check("a hot hull warms the cabin", rh.CabinTempC > ra.CabinTempC, "");
            Check("a hot hull warms loop A", rh.LoopAC > ra.LoopAC, "");

            // Losing power must DEGRADE the readings, not freeze them. A fake cannot fail
            // convincingly; a model can, and that is the argument for having one.
            CabinInputs dead = a; dead.Crew = 4; dead.Powered = false;
            CabinReadout rd = Cabin.Compute(dead);
            Check("power loss drives CO2 up", rd.Co2MmHg > rb.Co2MmHg, "");
            Check("power loss drives PPO2 down", rd.Ppo2Psia < rb.Ppo2Psia, "");
            Check("power loss couples the cabin harder to the hull",
                  Math.Abs(rd.CabinTempC - dead.HullTempC)
                  < Math.Abs(rb.CabinTempC - b.HullTempC) + 1e-9, "");

            // Deterministic: two screens showing the same instant must agree exactly.
            CabinReadout again = Cabin.Compute(b);
            Check("the model is deterministic", again.Co2MmHg == rb.Co2MmHg, "");

            // Nothing moves in lockstep - readings that all rise together look coupled and fake.
            CabinInputs t1 = b; t1.MissionTime = 500.0;
            CabinInputs t2 = b; t2.MissionTime = 560.0;
            CabinReadout r1 = Cabin.Compute(t1), r2 = Cabin.Compute(t2);
            Check("readings drift over time", r1.Co2MmHg != r2.Co2MmHg, "");
            Check("drift is not synchronised across readings",
                  Math.Sign(r2.Co2MmHg - r1.Co2MmHg) != 0
                  && (r2.PressPsia - r1.PressPsia) != (r2.Co2MmHg - r1.Co2MmHg), "");

            // Gauge fractions must stay on the dial whatever the model produces.
            CabinInputs wild = a; wild.Crew = 99; wild.HullTempC = 5000.0; wild.Powered = false;
            CabinReadout rw = Cabin.Compute(wild);
            Check("CO2 fraction clamps", rw.Co201 >= 0.0 && rw.Co201 <= 1.0, "");
            Check("PPO2 fraction clamps", rw.Ppo201 >= 0.0 && rw.Ppo201 <= 1.0, "");
            Check("cabin temp fraction clamps",
                  rw.CabinTemp01 >= 0.0 && rw.CabinTemp01 <= 1.0, "");

            // Nominal must sit in the READABLE middle of each dial, not pegged at an end - a needle
            // that never leaves the stop cannot show trouble.
            Check("nominal PPO2 sits mid-dial", ra.Ppo201 > 0.3 && ra.Ppo201 < 0.8,
                  ra.Ppo201.ToString("F2"));
            Check("nominal pressure sits mid-dial", ra.Press01 > 0.5 && ra.Press01 < 0.9,
                  ra.Press01.ToString("F2"));
            Check("nominal cabin temp sits mid-dial", ra.CabinTemp01 > 0.3 && ra.CabinTemp01 < 0.8,
                  ra.CabinTemp01.ToString("F2"));

            // ---- REAL TAC LIFE SUPPORT PATH ----
            // With HasLifeSupport, ppO2 and CO2 are driven by the Dragon's real O2 supply and CO2
            // accumulator fractions (LifeSupportBridge), not the crew-count fallback. The gauges must
            // read nominal at mission start and cross the alarm bands as TAC really depletes/accumulates.
            CabinInputs tacOk = new CabinInputs();
            tacOk.Crew = 4; tacOk.CrewCapacity = 4; tacOk.HullTempC = 22.0;
            tacOk.MissionTime = 100.0; tacOk.Power01 = 0.9; tacOk.Powered = true;
            tacOk.HasLifeSupport = true; tacOk.OxygenFrac = 1.0; tacOk.Co2Frac = 0.0;
            CabinReadout rt = Cabin.Compute(tacOk);
            Check("full O2 supply reads nominal ppO2", rt.Ppo2Psia > 2.8 && rt.Ppo2Psia < 3.2,
                  rt.Ppo2Psia.ToString("F2"));
            Check("empty CO2 accumulator reads below caution",
                  rt.Co2MmHg < CabinLimits.Co2Caution, rt.Co2MmHg.ToString("F2"));

            // A depleted O2 supply must drive ppO2 through the alarm limit.
            CabinInputs tacDry = tacOk; tacDry.OxygenFrac = 0.0;
            CabinReadout rdry = Cabin.Compute(tacDry);
            Check("O2 supply falling drops ppO2", rdry.Ppo2Psia < rt.Ppo2Psia, "");
            Check("empty O2 supply reaches the ppO2 alarm",
                  rdry.Ppo2Psia <= CabinLimits.Ppo2Alarm, rdry.Ppo2Psia.ToString("F2"));

            // A saturated CO2 accumulator (no scrubber on the Dragon) must reach the CO2 alarm.
            CabinInputs tacCo2 = tacOk; tacCo2.Co2Frac = 1.0;
            CabinReadout rco2 = Cabin.Compute(tacCo2);
            Check("CO2 accumulating raises CO2", rco2.Co2MmHg > rt.Co2MmHg, "");
            Check("saturated CO2 accumulator reaches the CO2 alarm",
                  rco2.Co2MmHg >= CabinLimits.Co2Alarm, rco2.Co2MmHg.ToString("F2"));

            // The TAC path is deterministic too, and its fractions stay on the dial.
            Check("TAC path is deterministic", Cabin.Compute(tacOk).Ppo2Psia == rt.Ppo2Psia, "");
            Check("TAC ppO2 fraction clamps", rdry.Ppo201 >= 0.0 && rdry.Ppo201 <= 1.0, "");
            Check("TAC CO2 fraction clamps", rco2.Co201 >= 0.0 && rco2.Co201 <= 1.0, "");
        }

        // ---- orbital readout meaningfulness ----
        // Reported in game: PERIGEE read -598.4 km on the VEHICLE page while FLIGHT correctly showed
        // "-", because the guard existed on one page and not the other. Two copies of one rule.
        // These pin both the rule AND the fact that both pages honour it.
        double kerbinAtmo = 70000.0;
        Check("no apogee on the ground",
              !OrbitReadout.ApogeeMeaningful(FlightRegime.Ground), "");
        Check("apogee shows while ascending",
              OrbitReadout.ApogeeMeaningful(FlightRegime.Atmosphere), "");
        Check("no perigee on the ground",
              !OrbitReadout.PerigeeMeaningful(FlightRegime.Ground, -598400.0, kerbinAtmo), "");
        // The exact reported value must be rejected IN FLIGHT too - the ground-guard alone was not
        // enough, because the radius artefact persists through most of an ascent.
        Check("the -598.4 km radius artefact is rejected while ascending",
              !OrbitReadout.PerigeeMeaningful(FlightRegime.Atmosphere, -598400.0, kerbinAtmo), "");
        Check("the -598.4 km radius artefact is rejected in space",
              !OrbitReadout.PerigeeMeaningful(FlightRegime.Space, -598400.0, kerbinAtmo), "");
        // ...but a real deorbit target MUST survive. F9I aims -20 to -50 km.
        Check("a -30 km deorbit perigee is shown",
              OrbitReadout.PerigeeMeaningful(FlightRegime.Space, -30000.0, kerbinAtmo), "");
        Check("a -41 km deorbit perigee is shown",
              OrbitReadout.PerigeeMeaningful(FlightRegime.Space, -41074.0, kerbinAtmo), "");
        Check("a normal orbit perigee is shown",
              OrbitReadout.PerigeeMeaningful(FlightRegime.Space, 85800.0, kerbinAtmo), "");
        // Scaling off the atmosphere is what makes this work unchanged under RSS/RO, where both the
        // deorbit targets and the body radius are several times larger.
        double earthAtmo = 140000.0;
        Check("an RSS deorbit perigee is shown",
              OrbitReadout.PerigeeMeaningful(FlightRegime.Space, -120000.0, earthAtmo), "");
        Check("an RSS radius artefact is still rejected",
              !OrbitReadout.PerigeeMeaningful(FlightRegime.Space, -6371000.0, earthAtmo), "");
        Check("a missing atmosphere depth still rejects the artefact",
              !OrbitReadout.PerigeeMeaningful(FlightRegime.Space, -598400.0, 0.0), "");

        // BOTH pages must honour it - that is the actual defect, not the threshold.
        foreach (int pageIdx in new int[] { 0, 1 })
        {
            PageState bad = new PageState();
            bad.Valid = true; bad.Regime = FlightRegime.Ground;
            bad.Periapsis = "-598.4 km"; bad.Apoapsis = "75 m";
            bad.ApogeeShown = false; bad.PerigeeShown = false;
            DisplayList pg = new DisplayList(Pages.Commands);
            Pages.Build(pg, pageIdx, 1280, 703, bad, MapProjection.Default(), 1);
            bool leaked = false;
            for (int i = 0; i < pg.Count; i++)
                if (pg.At(i).Kind == DrawKind.Text && pg.At(i).Str == "-598.4 km") leaked = true;
            Check("page " + ChromeBar.PageNames[pageIdx] + " hides a meaningless perigee",
                  !leaked, "");
        }

        // ============================================================================================
        // QC-AUDIT 2026-09-03 - the three glass findings that turned out to be real defects
        // ============================================================================================

        // ---- FINDING 6: TEXT MUST FIT ITS BOX -------------------------------------------------------
        // The Reference Content cards sit in three baked slots of very unequal height (317 / 449 / 550)
        // and the densest list, the seven-step ENTRY TIMELINE, is in the SHORTEST one. Its last row used
        // to render half on the card and half on the page ground. CoverPage.FitRows is the fix, so the
        // fix is what gets pinned - not the one string that happened to overflow.
        {
            float size, gap;
            // S116: FitRows now takes the panel it is fitting for, because the legibility floor is a
            // PANEL-pixel quantity and `size` is a DESIGN-frame one. The shipped panel and its design
            // scale, once, for every call below. sc = h / RefH (2112) is CoverPage's own Z() scale.
            const float PW = 2560f, PSc = 1406f / 2112f;

            // ENTRY TIMELINE: 7 rows from y=555 in a slot ending at 760. The design pitch does not fit.
            //
            // ⚠ SUPERSEDED IN PLACE 2026-09-06 (S123) — the CHECK is unchanged and still correct; only
            // its LABEL is stale, and it is kept per C1.16/G12 rather than retyped away. WHAT IT
            // CLAIMED: that this is where ENTRY TIMELINE draws. WHAT REPLACED IT: the owner's C-05
            // ruling ("option 2", 2026-09-06) moved ENTRY TIMELINE to card 3, so NO shipped card is a
            // 7-row block at y=555 any more. These arguments are now a SYNTHETIC too-dense block, and
            // that is exactly why they are worth keeping — FitRows' scaling path is no longer exercised
            // by any shipped content (see the early-return checks below), so without a synthetic caller
            // the whole clamp would go untested and a later regression in it would land silently.
            // ⚠ SUPERSEDED IN PLACE 2026-09-06 by [[S116]], per C1.16/G12 — and this is the ONE place
            // in this task where a check's ASSERTIONS changed, so it says exactly why.
            //
            // WHAT THESE FOUR CHECKS CLAIMED, with these same arguments:
            //   "QC6 a card too dense for its slot shrinks"       size < RowSize
            //   "QC6 ...to exactly the slot, less the bottom pad" block ends at slotBottom - RowPad
            //   "QC6 ...and its rows still do not overlap"        gap >= size
            //   "QC6 ...and the block keeps its proportions"      gap/size == wantGap/wantSize
            //
            // WHAT REPLACED IT, AND WHY IT IS NOT A TEST BENT TO FIT A FIX: those four pin FitRows'
            // PROPORTIONAL-SCALING branch, and with an honest floor these arguments no longer REACH
            // that branch. 7 rows of RowSize 26 in a 193-px slot scale to 23.018 design = 15.3 panel
            // px, which is under the floor, so the function does what its own summary always said it
            // would - "a slot too short for one legible line overflows visibly instead of turning to
            // mush" - and clamps instead. The POLICY is unchanged; the arguments changed branch.
            //
            // ⛔ SO BOTH BRANCHES ARE STILL PINNED, not one. These arguments now pin the CLAMP branch
            // (below), and a second call with a wantSize ABOVE the floor pins the scaling branch that
            // they used to. Deleting either would have been the back-door landing the old note warned
            // about, in the opposite direction.
            //
            // ⭐ AND THE NUMBER THIS NOW PINS IS THE ONE THE WHOLE C-05 SAGA WAS ABOUT. S112 measured
            // the overflow at 131 design px in 2026-09; job 1 of the 2026-09-06 batch re-derived it as
            // 131.5 and blocked S116 on it. With the fix in, the function produces 131.478 - so this
            // check is also the standing proof that the honest floor behaves exactly as predicted, and
            // that ENTRY TIMELINE could not have stayed in card 1.
            CoverPage.FitRows(555f, 760f, 7, CoverPage.RowSize, 32f, CoverPage.FloorDesign(PW, PSc), out size, out gap);
            Eq("QC6/S116 a slot too short for legible type clamps to the floor, it does not shrink",
               size, CoverPage.FloorDesign(PW, PSc), 0.001f);
            Check("QC6/S116 ...and its rows still do not overlap", gap >= size,
                  "gap " + gap + " size " + size);
            Eq("QC6/S116 ...and it OVERFLOWS VISIBLY by the 131 design px C-05 predicted",
               555f + gap * 6f + size - 760f, 131.478f, 0.01f);
            Check("QC6/S116 ...which is why ENTRY TIMELINE could not stay in card 1",
                  555f + gap * 6f + size > 760f, "block ends at " + (555f + gap * 6f + size));

            // ---- THE SCALING BRANCH, still pinned - with a wantSize that can actually reach it -----
            // ⛔ wantSize 100 is NOT arbitrary and must not be "tidied" back to RowSize. The scaling
            // branch is reachable only when wantSize * k >= floor for some k < 1, i.e. only when
            // wantSize > floor (48.07 design at the shipped aspect). RowSize is 26, so NO RowSize
            // block can ever take this branch any more - the wanted size is already below the floor.
            // That is a real consequence of S116 and it is logged as its own register line, not fixed
            // here (C1.1). Until then this synthetic caller is the only cover the branch has.
            //
            // ✅ SUPERSEDED IN PLACE 2026-09-06 BY [[S124]] (C1.16 / G12 - the paragraph above is kept
            // verbatim because it is the finding). It is still exactly right ABOUT THE LIVE FLOOR. What
            // changed is that the live floor is no longer the one this content is held to: the owner's
            // R-01 ruling ([[S153]]) puts the Cover's reference tables at the STATIC floor, 36.051
            // design px, and `FitRows` now takes its floor from the caller instead of assuming one.
            // ⭐ So the branch IS reachable for real content - at any row size above 36.051 - and the
            // block below pins that, beside the synthetic caller rather than instead of it.
            CoverPage.FitRows(0f, 500f, 4, 100f, 160f, CoverPage.FloorDesign(PW, PSc), out size, out gap);
            Check("QC6 a block that CAN scale legibly shrinks rather than clamping",
                  size < 100f && size > CoverPage.FloorDesign(PW, PSc), "size " + size);
            Eq("QC6 ...to exactly the slot, less the bottom pad",
               0f + gap * 3f + size, 500f - CoverPage.RowPad, 0.02f);
            Check("QC6 ...and its rows still do not overlap", gap >= size,
                  "gap " + gap + " size " + size);
            Check("QC6 ...and the block keeps its proportions",
                  Math.Abs(gap / size - 160f / 100f) < 0.001f, "ratio " + (gap / size));

            // ---- ⭐ S124: THE SAME BLOCK, AGAINST THE FLOOR ITS CONTENT IS ACTUALLY HELD TO --------
            // The two floors differ by exactly Dense/Min = 12/16 = 0.75, so the static one is 36.051
            // design px where the live one is 48.068. Everything below is the SAME call with the SAME
            // arguments as the clamp check above, and only the floor changed.
            {
                float liveFloor = CoverPage.FloorDesign(PW, PSc);
                float statFloor = CoverPage.StaticFloorDesign(PW, PSc);
                Eq("S124 the static floor is 36.051 design px at the shipped aspect", statFloor, 36.051f, 0.01f);
                Eq("S124 ...which is exactly Dense/Min of the live one", statFloor / liveFloor, 0.75f, 1e-4f);

                // ⛔ THE FINDING, AS ARITHMETIC: the branch needs the SCALED size to clear the floor,
                // not just the wanted one - `wantSize * k >= floor`. So the arguments below are chosen
                // to put the scaled size BETWEEN the two floors, which is the only place the policies
                // can differ: 4 rows of 60 with a 60 gap in 168 px of room gives k = 0.7 and a scaled
                // size of 42 - above 36.051, below 48.068.
                //
                // ⚠ AND `wantGap` IS 60, NOT SMALLER, FOR A REASON THAT IS NOT ARITHMETIC. FitRows ends
                // with `if (gap < size) gap = size;`, so whenever the gap would come out under the row
                // size the block occupies `count * size` regardless of what the scale computed. A first
                // attempt here used a 40 gap under a 46 size; the scale produced 38.8 and 33.7, the
                // trailing clamp pushed the gap back to 38.8, and the block ended at 155.2 rather than
                // the 140 the scale had fitted it to - overflowing under BOTH floors and distinguishing
                // nothing. ⭐ Recorded because it is a real property of this method: the proportional
                // branch only actually fits the slot when `wantGap >= wantSize`.
                float s1b, g1b, s2b, g2b;
                float slotTop = 0f, slotBot = 168f + CoverPage.RowPad;
                CoverPage.FitRows(slotTop, slotBot, 4, 60f, 60f, statFloor, out s1b, out g1b);
                CoverPage.FitRows(slotTop, slotBot, 4, 60f, 60f, liveFloor, out s2b, out g2b);
                Check("S124 a reference block SCALES against the static floor", s1b < 60f && s1b > statFloor,
                      "size " + s1b + ", static floor " + statFloor);
                Eq("S124 ...and CLAMPS against the live one, which is what made the branch unreachable",
                   s2b, liveFloor, 0.001f);
                Check("S124 ...so the same content overflows under one policy and fits under the other",
                      (slotTop + g2b * 3f + s2b > slotBot) && (slotTop + g1b * 3f + s1b <= slotBot + 0.02f),
                      "static ends " + (slotTop + g1b * 3f + s1b) + ", live ends "
                          + (slotTop + g2b * 3f + s2b) + ", slot ends " + slotBot);

                // ⚠ AND THE CURRENT RowSize STILL CANNOT REACH IT. 26 is below BOTH floors, so this
                // line fixes the MECHANISM and [[S153a]] owns the NUMBER. Pinned so that when S153a
                // raises RowSize this check fails and has to be re-read rather than silently passing.
                Check("S124 RowSize 26 is below even the static floor - S153a owns raising it",
                      CoverPage.RowSize < statFloor,
                      "RowSize " + CoverPage.RowSize + " vs static floor " + statFloor);
            }

            // PARACHUTES: 4 rows from 904 in a slot ending at 1241. Already fits, so nothing may move.
            CoverPage.FitRows(904f, 1241f, 4, CoverPage.RowSize, 40f, CoverPage.FloorDesign(PW, PSc), out size, out gap);
            Check("QC6 a card that already fits is left alone",
                  size == CoverPage.RowSize && gap == 40f, "size " + size + " gap " + gap);

            // A slot far too short must not answer with illegible type: Typography.Min is a MEASURED
            // floor, so the block overflows visibly rather than turning to mush.
            //
            // ⚠ SUPERSEDED IN PLACE 2026-09-06 by [[S116]], per C1.16/G12. WHAT THIS NOTE SAID:
            //   "⛔ THIS ONE STAYS `Typography.Min`, AND STAYS IN DESIGN SPACE, ON PURPOSE. `size` is a
            //   DESIGN-frame number and Min is a PANEL-pixel one - that mismatch IS QC C-05's unit bug.
            //   What this check pins is the OVERFLOW POLICY (FitRows' own summary: "a slot too short for
            //   one legible line overflows visibly instead of turning to mush"), not the floor, and it is
            //   the check that caught S112 inventing a third policy. C-05's fix is [[S116]] and it is
            //   BLOCKED: against an honest floor the clamp overflows its card by 131 design px at EVERY
            //   width, which needs an owner-level TIER-3 layout call (C1.12, not settled). Correcting the
            //   units here before that call would land the blocked fix by the back door. See FitRows."
            //
            // WHAT REPLACED IT: the owner made that TIER-3 call on 2026-09-06 ("option 2"), [[S123]]
            // landed the swap, and S116 then landed the unit fix. So the deliberate mismatch is gone
            // and this check is written in PANEL units, which is what S116's own verify line asks for
            // ("assert the floor in PANEL units so a future resolution change cannot silently reopen
            // this"). ⛔ THE OVERFLOW POLICY IT WAS GUARDING IS UNCHANGED and is still what is pinned
            // below: a slot too short for one legible line overflows VISIBLY. The check that caught
            // S112 inventing a third policy still catches it.
            CoverPage.FitRows(0f, 60f, 6, CoverPage.RowSize, 40f, CoverPage.FloorDesign(PW, PSc), out size, out gap);
            Check("QC6 type never goes under the floor, MEASURED IN PANEL PIXELS",
                  size * PSc >= Typography.MinFor(PW) - 1e-3f,
                  "size " + size + " design = " + (size * PSc) + " panel px, floor "
                      + Typography.MinFor(PW));

            // ---- S116: THE UNIT FIX ITSELF, PINNED AT TWO WIDTHS SO IT CANNOT SILENTLY REOPEN -------
            // R-02's lesson in one check: a floor whose premise is a width cannot be tested at one
            // width. The SAME slot fitted for two panels of the same ASPECT must clamp to the same
            // PHYSICAL size - which under the old design-px-vs-panel-px comparison it did not.
            {
                const float PW1 = 1280f, PSc1 = 703f / 2112f;
                float s1, g1, s2, g2;
                CoverPage.FitRows(0f, 60f, 6, CoverPage.RowSize, 40f, CoverPage.FloorDesign(PW1, PSc1), out s1, out g1);
                CoverPage.FitRows(0f, 60f, 6, CoverPage.RowSize, 40f, CoverPage.FloorDesign(PW, PSc), out s2, out g2);
                Check("S116 the clamped size is the same FRACTION of the panel at 1280 and 2560",
                      Math.Abs(s1 * PSc1 / PW1 - s2 * PSc / PW) < 1e-6f,
                      s1 * PSc1 + " px of " + PW1 + " vs " + s2 * PSc + " px of " + PW);
                Check("S116 ...and clears the floor at BOTH, not just the one it was written at",
                      s1 * PSc1 >= Typography.MinFor(PW1) - 1e-3f
                          && s2 * PSc >= Typography.MinFor(PW) - 1e-3f,
                      "@1280 " + (s1 * PSc1) + " vs " + Typography.MinFor(PW1)
                          + ", @2560 " + (s2 * PSc) + " vs " + Typography.MinFor(PW));

                // The conversion itself, stated once. ⛔ Asserted as a RATIO, never as the literal
                // 48.068: writing that number here would be R-02 again in a new place. It is
                // invariant under a change of SIZE at fixed aspect, and it MOVES with the aspect,
                // because sc comes from the height alone and the floor comes from the width.
                Check("S116 FloorDesign is MinFor(panelW) / sc, by construction",
                      Math.Abs(CoverPage.FloorDesign(PW, PSc) - Typography.MinFor(PW) / PSc) < 1e-4f,
                      "got " + CoverPage.FloorDesign(PW, PSc));
                Check("S116 ...so it is the same design number at both shipped widths (same aspect)",
                      Math.Abs(CoverPage.FloorDesign(PW1, PSc1) - CoverPage.FloorDesign(PW, PSc)) < 1e-3f,
                      "@1280 " + CoverPage.FloorDesign(PW1, PSc1) + ", @2560 " + CoverPage.FloorDesign(PW, PSc));
                Check("S116 ...and it MOVES when the ASPECT moves, which is why it is not a constant",
                      CoverPage.FloorDesign(2560f, 1920f / 2112f) < CoverPage.FloorDesign(PW, PSc) - 1f,
                      "4:3-ish " + CoverPage.FloorDesign(2560f, 1920f / 2112f)
                          + " vs shipped " + CoverPage.FloorDesign(PW, PSc));
                Check("S116 a degenerate scale falls back to the panel floor rather than dividing by zero",
                      CoverPage.FloorDesign(PW, 0f) == Typography.MinFor(PW),
                      "got " + CoverPage.FloorDesign(PW, 0f));
            }

            // ---- S123: AFTER THE SWAP, NEITHER CARD CLAMPS — AND [[S116]] DEPENDS ON THAT ----------
            // The owner's C-05 ruling ("option 2", 2026-09-06) put CONTINGENCY's 4 rows in card 1 and
            // ENTRY TIMELINE's 7 in card 3. Both blocks now fit at their WANTED size, so FitRows takes
            // its `need <= avail` early return on both and the clamp never fires. That is the property
            // S116 rests on: correcting the design-px/panel-px unit bug cannot move a render in which
            // the clamp is never reached. If either of these ever fails, S116 is live again and must be
            // re-derived before it lands.
            //
            // ⛔ Stated as `== RowSize` exactly, NOT ">= Typography.Min". An early return is the only
            // path that hands back the wanted size untouched; the clamp path cannot produce it.
            CoverPage.FitRows(555f, 760f, 4, CoverPage.RowSize, 40f, CoverPage.FloorDesign(PW, PSc), out size, out gap);
            Check("S123 card 1 (CONTINGENCY, 4 rows) fits unscaled — FitRows returns early",
                  size == CoverPage.RowSize && gap == 40f, "size " + size + " gap " + gap);
            CoverPage.FitRows(1385f, 1823f, 7, CoverPage.RowSize, 32f, CoverPage.FloorDesign(PW, PSc), out size, out gap);
            Check("S123 card 3 (ENTRY TIMELINE, 7 rows) fits unscaled — FitRows returns early",
                  size == CoverPage.RowSize && gap == 32f, "size " + size + " gap " + gap);

            // ---- ⭐ S124: AND THEREFORE THE FLOOR SWAP THIS LINE MADE IS INERT FOR TODAY'S CONTENT ----
            // ⛔ THIS IS A MUTATION RESULT, NOT A COMFORT. S124 mutated the PRODUCTION call site to pass
            // the LIVE floor — exactly the pre-S124 behaviour — and ran the whole suite: **nothing failed**.
            // Five other mutations of the same change were killed; that one cannot be, and the three
            // checks above are the reason. All three cards take `need <= avail` and return before the
            // floor is ever read, so no test can distinguish the two floors THROUGH `Draw`. Saying the
            // call site is "covered" would be false.
            //
            // ⭐ What IS checkable is the equivalence itself, so it is checked here rather than assumed:
            // the same card, fitted under both floors, must come back byte-identical AND at the wanted
            // size. That is the machine-readable form of "the swap changes no pixel today".
            // ⚠ It is also the tripwire. The moment a row is added, `RowSize` is raised ([[S153a]] owns
            // that), or a card is shortened, `need <= avail` stops holding, these checks diverge, and the
            // floor the production caller passes starts deciding what the crew see — which is when S124's
            // change earns its keep and has to be re-read rather than trusted.
            {
                float sA, gA, sB, gB;
                float stat = CoverPage.StaticFloorDesign(PW, PSc), live = CoverPage.FloorDesign(PW, PSc);
                Check("S124 the two floors are genuinely different numbers (else the check below is vacuous)",
                      stat < live - 10f, "static " + stat + " live " + live);

                CoverPage.FitRows(555f, 760f, 4, CoverPage.RowSize, 40f, stat, out sA, out gA);
                CoverPage.FitRows(555f, 760f, 4, CoverPage.RowSize, 40f, live, out sB, out gB);
                Check("S124 card 1 draws identically under BOTH floors — the swap is inert here",
                      sA == sB && gA == gB && sA == CoverPage.RowSize,
                      "static " + sA + "/" + gA + " live " + sB + "/" + gB);

                CoverPage.FitRows(904f, 1241f, 4, CoverPage.RowSize, 40f, stat, out sA, out gA);
                CoverPage.FitRows(904f, 1241f, 4, CoverPage.RowSize, 40f, live, out sB, out gB);
                Check("S124 card 2 draws identically under BOTH floors — the swap is inert here",
                      sA == sB && gA == gB && sA == CoverPage.RowSize,
                      "static " + sA + "/" + gA + " live " + sB + "/" + gB);

                CoverPage.FitRows(1385f, 1823f, 7, CoverPage.RowSize, 32f, stat, out sA, out gA);
                CoverPage.FitRows(1385f, 1823f, 7, CoverPage.RowSize, 32f, live, out sB, out gB);
                Check("S124 card 3 draws identically under BOTH floors — the swap is inert here",
                      sA == sB && gA == gB && sA == CoverPage.RowSize,
                      "static " + sA + "/" + gA + " live " + sB + "/" + gB);
            }
        }

        // ---- S123: THE SWAP ITSELF, READ OFF THE RENDER RATHER THAN OFF THE SOURCE ------------------
        // The two checks above pin the arithmetic; this pins that the shipped page actually draws it
        // that way, which is the half a re-typed literal cannot prove. Card 1's band must contain the
        // CONTINGENCY title and card 3's the ENTRY TIMELINE title — and every row in the card column
        // must draw at the WANTED size, which is the render-side statement of "no clamp fired".
        {
            const float CoverRefH = 2112f;
            int cw = 2560, ch = 1406;
            float csc = ch / CoverRefH;
            PageState ss = new PageState(); ss.Valid = true;
            DisplayList sd = new DisplayList(CoverPage.Commands);
            CoverPage.Build(sd, cw, ch, ss, MapProjection.Default(), 5);   // Reference Content phase

            string card1Title = null, card3Title = null;
            int rows = 0, offSize = 0; float worstSize = 0f;
            for (int i = 0; i < sd.Count; i++)
            {
                DrawCmd t = sd.At(i);
                if (t.Kind != DrawKind.Text || t.A < 240f * csc || t.A > 1427f * csc) continue;
                // Titles draw at 34 design px; rows at RowSize. Band by the baked card backgrounds.
                bool inCard1 = t.B >= 443f * csc && t.B < 760f * csc;
                bool inCard3 = t.B >= 1273f * csc && t.B < 1823f * csc;
                if (Math.Abs(t.C - 34f * csc) < 0.01f)
                {
                    if (inCard1) card1Title = t.Str;
                    if (inCard3) card3Title = t.Str;
                }
                else if (inCard1 || inCard3)
                {
                    rows++;
                    if (Math.Abs(t.C - CoverPage.RowSize * csc) > 0.01f)
                    { offSize++; if (Math.Abs(t.C - CoverPage.RowSize * csc) > worstSize) worstSize = Math.Abs(t.C - CoverPage.RowSize * csc); }
                }
            }
            Check("S123 card 1 now carries CONTINGENCY", card1Title == "CONTINGENCY",
                  "card 1 title is " + (card1Title ?? "<none>"));
            Check("S123 card 3 now carries ENTRY TIMELINE", card3Title == "ENTRY TIMELINE",
                  "card 3 title is " + (card3Title ?? "<none>"));
            Check("S123 cards 1 and 3 draw 11 rows between them (4 + 7)", rows == 11, "found " + rows);
            Check("S123 every one of them draws at the WANTED size — no clamp fired anywhere",
                  offSize == 0, offSize + " rows off by up to " + worstSize + " px");
        }

        // ...and the end-to-end guard: NO text drawn inside one of the three card slots may cross that
        // slot's bottom edge. This is the check that would have caught the defect on the preview PNG.
        {
            const float CoverRefH = 2112f;
            int cw = 2560, ch = 1406;
            float csc = ch / CoverRefH;
            // rectangle_179 / _180 / _181, the baked card backgrounds, as measured in CoverPage.Box.
            float[,] slot = { { 443f, 760f }, { 792f, 1241f }, { 1273f, 1823f } };
            PageState rs = new PageState(); rs.Valid = true;
            DisplayList rd = new DisplayList(CoverPage.Commands);
            CoverPage.Build(rd, cw, ch, rs, MapProjection.Default(), 5);   // Reference Content phase
            for (int c = 0; c < 3; c++)
            {
                float top = slot[c, 0] * csc, bot = slot[c, 1] * csc;
                float worst = 0f; string worstStr = null;
                for (int i = 0; i < rd.Count; i++)
                {
                    DrawCmd t = rd.At(i);
                    // the card column only: left of it is the phase rail, right of it is the globe.
                    if (t.Kind != DrawKind.Text || t.A < 240f * csc || t.A > 1427f * csc) continue;
                    if (t.B < top || t.B >= bot) continue;
                    if (t.B + t.C > bot && t.B + t.C - bot > worst)
                    { worst = t.B + t.C - bot; worstStr = t.Str; }
                }
                Check("QC6 card " + (c + 1) + " keeps every row inside its slot", worstStr == null,
                      worstStr + " overhangs by " + worst + " px");
            }
        }

        // ---- FINDING 4: ONE READING OF THE INTERRUPT CRITERIA (S13's residual) ----------------------
        // S13 settled the quantity as ATTITUDE and applied it to DeorbitBurnPrepPage, but the Cover kept
        // showing the baked "altitude" captions, so the two surfaces disagreed on glass (C7.1).
        {
            PageState cs = new PageState(); cs.Valid = true;
            DisplayList cd = new DisplayList(CoverPage.Commands);
            CoverPage.Build(cd, 2560, 1406, cs, MapProjection.Default(), 1);   // a baked-body phase
            bool err = false, rate = false, baked = false;
            for (int i = 0; i < cd.Count; i++)
            {
                DrawCmd t = cd.At(i);
                if (t.Kind == DrawKind.Text && t.Str == "30° sustained attitude error") err = true;
                if (t.Kind == DrawKind.Text && t.Str == "600°/min attitude rate") rate = true;
                if (t.Kind == DrawKind.Image &&
                    (t.AssetKey == "30deg_sustained_altitude_error" || t.AssetKey == "600deg_m_altitude_rate"))
                    baked = true;
            }
            Check("QC4 the Cover states the interrupt criterion as ATTITUDE error", err, "");
            Check("QC4 ...and the rate the same way", rate, "");
            Check("QC4 ...and no longer places the two baked altitude captions over it", !baked, "");
        }

        // ---- FINDING 3: ONE TRUTH FOR THE TWO MAIN BUSES --------------------------------------------
        // The systems tree read the buses off SystemsState; the ELECTRICAL POWER tab hard-coded a green
        // "Nominal". The buses START OFF (VehicleSystems.Fresh), so the tab was wrong on open.
        {
            SystemsState fresh = SystemsState.Fresh();
            Check("QC3 the tree calls an unpowered bus off", TreeBusWord(fresh) == "BUS OFF",
                  "tree said " + TreeBusWord(fresh));
            Check("QC3 ...and the POWER tab no longer calls the same bus Nominal",
                  TabBusWord(fresh) == "Off", "tab said " + TabBusWord(fresh));

            SystemsState up = SystemsState.Fresh();
            Systems.ToggleBus(ref up, 1);
            for (int i = 0; i < 3; i++) Systems.Set(ref up, 1, i, StringState.Online);
            Check("QC3 a fully online bus reads 3/3 on the tree",
                  TreeBusWord(up) == "3 / 3 ONLINE", "tree said " + TreeBusWord(up));
            Check("QC3 ...and Nominal on the tab", TabBusWord(up) == "Nominal",
                  "tab said " + TabBusWord(up));

            SystemsState part = SystemsState.Fresh();
            Systems.ToggleBus(ref part, 1);
            Systems.Set(ref part, 1, 0, StringState.Online);
            Systems.Set(ref part, 1, 1, StringState.Online);
            Systems.Set(ref part, 1, 2, StringState.Tripped);
            Check("QC3 a partly online bus says so on both surfaces",
                  TreeBusWord(part) == "2 / 3 ONLINE" && TabBusWord(part) == "2 / 3 Online",
                  "tree " + TreeBusWord(part) + " / tab " + TabBusWord(part));

            SystemsState dead = SystemsState.Fresh();
            Systems.ToggleBus(ref dead, 1);
            for (int i = 0; i < 3; i++) Systems.Set(ref dead, 1, i, StringState.Tripped);
            Check("QC3 a powered bus with no string left reads 0/3 on both",
                  TreeBusWord(dead) == "0 / 3 ONLINE" && TabBusWord(dead) == "0 / 3 Online",
                  "tree " + TreeBusWord(dead) + " / tab " + TabBusWord(dead));
        }

        // ---- S38: A VALUE MUST SIT BESIDE ITS LABEL, NOT ACROSS THE PAGE FROM IT ----------------
        // The 2026-09-03 glass session caught the crew reading their own life-support panel wrong:
        // CABIN TEMP showing 14.70 psia (the cabin PRESSURE), CABIN PRESS showing the ppO2, PPO2
        // showing the CO2. The data was correct and the preview PNG was correct. The console is a
        // TILTED QUAD in IVA, so a row that is horizontal in the RenderTexture is a sloping line to
        // the crew, and across a wide label-to-value gap the value column lifts by about a whole row.
        //
        // ⛔ NO PNG CHECK CAN FIND THIS. `build.py preview` renders the panel flat and square-on, so
        // these rows align perfectly there and always will. That is why the guard is a headless
        // assertion on the SPAN instead: keep the value close enough to its label that no plausible
        // viewing angle can slide it onto a neighbouring row.
        //
        // The span measured is label-x to value-x in panel pixels, over pairs emitted as one row
        // (a Left text followed within three commands by a Right one, sharing a row). The limit is a
        // multiple of the row's own type size, so it scales with the panel.
        {
            PageState fx = new PageState();
            fx.Valid = true;
            fx.Ppo2Text = "3.01"; fx.CabinTempText = "21.8"; fx.PressText = "14.72"; fx.Co2Text = "1.64";
            fx.LoopAText = "26.4"; fx.LoopBText = "20.1";
            fx.PowerText = "18"; fx.ArrayKwText = "2.60"; fx.HullTempText = "312";
            fx.SolarArrayText = "DEPLOYED"; fx.BatteryText = "2 / 2";
            fx.Systems = SystemsState.Fresh();

            // Worst label-to-value span on a page, in multiples of the label's type size.
            float WorstSpan(UiPage page)
            {
                DisplayList d = new DisplayList(4000);
                FigmaUI.Build(d, page, 2560, 1406, fx, MapProjection.Default());
                float worst = 0f;
                for (int i = 0; i < d.Count; i++)
                {
                    DrawCmd a2 = d.At(i);
                    if (a2.Kind != DrawKind.Text || a2.Align != TextAlign.Left || a2.Str == null) continue;
                    for (int j = i + 1; j < i + 4 && j < d.Count; j++)
                    {
                        DrawCmd b2 = d.At(j);
                        if (b2.Kind != DrawKind.Text || b2.Align != TextAlign.Right || b2.Str == null) continue;
                        if (b2.A <= a2.A) continue;
                        float ac = a2.B + a2.C * 0.5f, bc = b2.B + b2.C * 0.5f;
                        float tol = (a2.C < b2.C ? a2.C : b2.C) * 0.5f;
                        if (ac - bc > tol || bc - ac > tol) continue;
                        float rel = (b2.A - a2.A) / a2.C;
                        if (rel > worst) worst = rel;
                    }
                }
                return worst;
            }

            // SYSTEMS P&ID - the block the glass actually caught. Was 38x before the fix.
            Check("S38 the P&ID readouts keep their values beside their labels",
                  WorstSpan(UiPage.SystemsPid) <= 14f, "worst " + WorstSpan(UiPage.SystemsPid) + "x");

            // DEORBIT BURN PREP - the widest span in the build before the fix, at 105x: the label sat
            // at the far left of the card and the value at the far right, five rows stacked.
            Check("S38 the deorbit SLEW rows do too",
                  WorstSpan(UiPage.DeorbitBurnPrep) <= 20f,
                  "worst " + WorstSpan(UiPage.DeorbitBurnPrep) + "x");

            // The six subsystem tabs share ONE detail-row helper, so one fix covers all of them and
            // the check is worth running on more than the one that happened to be edited.
            UiPage[] subs = { UiPage.VehicleCrew, UiPage.VehiclePropulsion, UiPage.VehiclePower,
                              UiPage.VehicleAvionics, UiPage.VehicleGnc, UiPage.VehicleThermal };
            for (int i = 0; i < subs.Length; i++)
            {
                // Propulsion draws a section caption and a page-level badge on one line, which is not
                // a label-value row and is not what this guards; its DETAIL rows share the helper the
                // other five use, so those five pin the helper.
                if (subs[i] == UiPage.VehiclePropulsion) continue;
                Check("S38 " + subs[i] + "'s detail rows keep their values close",
                      WorstSpan(subs[i]) <= 18f, "worst " + WorstSpan(subs[i]) + "x");
            }

            // ---- S39: THE TWO BLOCKS S38'S SURVEY LEFT THAT THE SAME REMEDY CLOSES ----
            // S39 re-ran the survey and found the span number alone is not the discriminator - what
            // decides whether a wide gap MISREADS is the span against the ROW PITCH, and whether a
            // connector spans the gap. These two are the reachable blocks where both went the wrong
            // way: a wide span, a tight pitch, and nothing joining label to value.
            //
            // NAV ORBIT PLOT - three stacked rows, 580 units at a 44-unit pitch (span:pitch = 13),
            // the second-tightest in the build after the DeorbitBurnPrep residual. Was 22.3x.
            Check("S39 the orbit plot's g/rate rows keep their values beside their labels",
                  WorstSpan(UiPage.NavOrbitPlot) <= 12f,
                  "worst " + WorstSpan(UiPage.NavOrbitPlot) + "x");

            // VEHICLE MECH - up to seven stacked seat rows, 440 units at an 80-unit pitch. Milder,
            // same class, same remedy. Was 19.0x. The block stays CENTRED in the donut, so the label
            // moved out as far as the value moved in.
            Check("S39 the Mech page's seat rows do too",
                  WorstSpan(UiPage.VehicleMech) <= 12f,
                  "worst " + WorstSpan(UiPage.VehicleMech) + "x");

            // ⛔ NOT ASSERTED HERE, AND ON PURPOSE - see the S39 register line:
            //   · UiPage.Vehicle's CONSUMABLES table (29.3x) is three columns on a 145-unit pitch with
            //     a FULL-WIDTH RULE under every row - a connector that already spans the whole gap,
            //     which is the mechanism S38 found missing. Whether it also wants banding is a design
            //     call the owner has not made; a guard here would freeze a layout that may change.
            //   · UiPage.AudioVideo (81x) and UiPage.VehiclePropulsion (92.8x) are NOT stacked
            //     label-value rows - a lone caption under the video box, and a section caption beside
            //     a page badge. S39's own line names both as "not defects, do not fix them".
            //   · The old (non-Figma) FLIGHT screen measures 57.5x over NINETEEN pairs - the largest
            //     count anywhere - but Pages.Build sits in ScreenPainter's `else` branch behind
            //     FigmaMode = true, so no crew can reach it. It is stranded UI, not a live defect.
        }

        Console.WriteLine(failures == 0
            ? "  " + checks + " checks, all passed"
            : "  " + checks + " checks, " + failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}
