// Tests for the audio panel's grid and its signal glyph — [[S134e]] / QC `A-03` + `A-04`.
//
// ⛔ WHAT WAS WRONG. The four dividers cut the audio panel into five cells. Ten of the panel's
// thirteen x positions sat exactly on those cells' centres and three did not: AUX's label and value
// 42 design px right of theirs, INTERCOM's ± pair 46 px right, ALERTS' pair 45.5 px right. And the
// signal glyph was drawn 22 design px below its own plate's centre line, at under a third of the
// width its ± siblings use.
//
// ⭐ HOW THESE CHECKS AVOID BEING TAUTOLOGIES — and this run has already found five that were not
// avoided. NOTHING below reads `CellCx`, `MinusCx`, `SignalCx` or any layout constant. The grid is
// recovered from the RENDER: the panel rect gives the outer edges, the four divider LINES give the
// inner ones, and everything else — text anchors, plate centres, glyph radii, stroke widths — is read
// out of the same DisplayList. Move a cluster and the cell it is measured against does not move with
// it, because the cell comes from the dividers, which QC's must-not-break says stay put.
using System;
using DragonScreen;

public static class AudioGridTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen audio panel grid + signal glyph (S134e / QC A-03, A-04)");
        checks = 0; failures = 0;

        // ⚠ TWO ASPECTS FROM THE START. S134a and S134b each had a projection bug survive a suite that
        // ran at one aspect only; this page is STRETCHED in x, so a wrong inverse hides at 2560.
        Grid(2560, 1406, SettingsAudioPage.CabinScope);
        Grid(4416, 1406, SettingsAudioPage.CabinScope);
        Grid(2560, 1406, 1);                 // a SEAT scope: the caveat line must not join the grid
        Glyph(2560, 1406);
        Glyph(4416, 1406);
        Capacity();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (thirteen positions on one grid, and a glyph on its siblings' line)");
        return failures;
    }

    // =============================================================================================
    //  QC A-03 — every position in the panel sits in the cell its dividers cut
    // =============================================================================================
    static void Grid(int w, int h, int sel)
    {
        string at = " @" + w + "x" + h + " sel" + sel;
        DisplayList dl = new DisplayList(SettingsAudioPage.Commands + 200);
        SettingsAudioPage.Build(dl, w, h, sel, Live());
        float sx = w / 3427f, sy = h / 2112f;

        // ---- the panel, read from the render ----------------------------------------------------
        // The audio panel is the Panel-coloured rect in the lower half; the other one is the selected
        // seat's highlight, which is far above it.
        float pL = 0f, pR = 0f, pT = 0f, pB = 0f; int panels = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Rect || !Same(c.Colour, DragonPalette.Panel)) continue;
            if (c.B < 1000f * sy) continue;                       // the seat highlight, not the panel
            panels++; pL = c.A / sx; pR = (c.A + c.C) / sx; pT = c.B / sy; pB = (c.B + c.D) / sy;
        }
        Check("S134e the audio panel is one rect" + at, panels == 1, "got " + panels);
        if (panels != 1) return;

        // ---- the four dividers, read from the render ---------------------------------------------
        float[] div = new float[8]; int nd = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Line || !Same(c.Colour, DragonPalette.Hairline)) continue;
            if (Math.Abs(c.A - c.C) > 0.01f) continue;            // vertical only
            if (nd < div.Length) div[nd++] = c.A / sx;
        }
        Check("S134e the panel is cut by four dividers" + at, nd == 4, "got " + nd);
        if (nd != 4) return;
        Array.Sort(div, 0, 4);

        // ---- the cells they define ---------------------------------------------------------------
        float[] edge = { pL, div[0], div[1], div[2], div[3], pR };
        bool rising = true;
        for (int i = 1; i < edge.Length; i++) if (edge[i] <= edge[i - 1]) rising = false;
        Check("S134e the six edges are in order" + at, rising, Join(edge));

        float[] cx = new float[5];
        for (int i = 0; i < 5; i++) cx[i] = (edge[i] + edge[i + 1]) * 0.5f;

        // ⭐ AND THE GRID IS ALMOST-BUT-NOT-QUITE REGULAR, which is the half-pixel QC's own formula
        // missed: 2489 design px over five cells is 497.8, so four cells are 498 and the fifth is 497.
        Check("S134e four cells are 498 design px and the fifth is 497" + at,
              Near(edge[1] - edge[0], 498f, 0.05f) && Near(edge[2] - edge[1], 498f, 0.05f)
              && Near(edge[3] - edge[2], 498f, 0.05f) && Near(edge[4] - edge[3], 498f, 0.05f)
              && Near(edge[5] - edge[4], 497f, 0.05f),
              Join(edge));

        // ---- EVERY line of text inside the panel is on its cell's centre ------------------------
        // ⚠ QC's verify criterion is *"within 2 px of the same cell centre"*. This is set eight times
        // tighter on purpose: every position is now DERIVED from the cells, so the error is zero and a
        // 2 px window would not tell the derivation apart from QC's own proposed formula
        // (`468 + 498 * (i + 0.5)`), which is half a pixel out on the fifth cell. 0.25 discriminates;
        // float round-trip through `A / sx` is ~1e-4 design px, four orders of magnitude below it.
        const float Tol = 0.25f;
        int texts = 0, offText = 0; float worstT = 0f; string worstS = "";
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Text) continue;
            if (c.B < pT * sy || c.B > pB * sy) continue;
            texts++;
            if (c.Align != TextAlign.Centre) { offText++; continue; }
            float d = c.A / sx;
            int cell = CellOf(edge, d);
            float err = cell < 0 ? 9999f : Math.Abs(d - cx[cell]);
            if (err > Tol) { offText++; if (err > worstT) { worstT = err; worstS = c.Str; } }
        }
        Check("S134e the panel holds twelve centred lines" + at, texts == 12, "got " + texts);
        Check("S134e every one of them is on its cell's centre" + at, offText == 0,
              offText + " off, worst " + worstT.ToString("F1") + " px on \"" + worstS + "\"");

        // ---- and so is every button CLUSTER ------------------------------------------------------
        // A plate is a SQUARE fill; `Box`'s four border rects are thin and cannot be mistaken for one.
        float[] lo = new float[5], hi = new float[5]; int[] cnt = new int[5];
        for (int i = 0; i < 5; i++) { lo[i] = 1e9f; hi[i] = -1e9f; }
        int plates = 0, stray = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Rect) continue;
            if (Math.Abs(c.C - c.D) > 0.01f || c.C < 20f) continue;
            if (c.B < pT * sy || c.B > pB * sy) continue;
            plates++;
            float d = (c.A + c.C * 0.5f) / sx;
            int cell = CellOf(edge, d);
            if (cell < 0) { stray++; continue; }
            cnt[cell]++;
            if (d < lo[cell]) lo[cell] = d;
            if (d > hi[cell]) hi[cell] = d;
        }
        Check("S134e the row holds ten plates" + at, plates == 10, "got " + plates);
        Check("S134e none of them falls outside a cell" + at, stray == 0, "got " + stray);
        Check("S134e three in GROUND and AUX, two in INTERCOM and ALERTS, none in MAIN" + at,
              cnt[0] == 3 && cnt[1] == 3 && cnt[2] == 0 && cnt[3] == 2 && cnt[4] == 2,
              cnt[0] + "," + cnt[1] + "," + cnt[2] + "," + cnt[3] + "," + cnt[4]);

        int offCluster = 0; float worstC = 0f; int worstI = -1;
        for (int i = 0; i < 5; i++)
        {
            if (cnt[i] == 0) continue;
            float mid = (lo[i] + hi[i]) * 0.5f;
            float err = Math.Abs(mid - cx[i]);
            if (err > Tol) { offCluster++; if (err > worstC) { worstC = err; worstI = i; } }
        }
        Check("S134e every cluster is centred in its own cell" + at, offCluster == 0,
              offCluster + " off, worst cell " + worstI + " by " + worstC.ToString("F1") + " design px");

        // ⭐ AND THE PITCH IS ONE NUMBER. QC's evidence that the clusters were PLACED by one rule and
        // CENTRED by another is that all four already shared a 152 px pitch; that has to survive.
        int pitchBad = 0;
        for (int i = 0; i < 5; i++)
        {
            if (cnt[i] < 2) continue;
            float span = hi[i] - lo[i];
            float pitch = span / (cnt[i] - 1);
            if (!Near(pitch, 152f, 0.05f)) pitchBad++;
        }
        Check("S134e ...and every cluster still steps by 152 design px" + at, pitchBad == 0,
              pitchBad + " cluster(s) off pitch");

        // ---- ⭐ AND THE TOUCH IS THE SAME RECTANGLE (QC `H-04`) ----------------------------------
        // ⚠ Probed against the plate AS DRAWN, not against the geometry function — the hit test is
        // asked whether it agrees with the render, which is the question `H-04` is about. Only the two
        // live channels can answer yes at all, so those are the ones this can measure; the check is
        // that wherever it says yes at the centre, it says yes to the whole plate and no past its edge.
        int live = 0, edgeBad = 0, outsideBad = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Rect) continue;
            if (Math.Abs(c.C - c.D) > 0.01f || c.C < 20f) continue;
            if (c.B < pT * sy || c.B > pB * sy) continue;
            float mx = c.A + c.C * 0.5f, my = c.B + c.D * 0.5f, hw = c.C * 0.5f;
            SettingsAudioPage.AudioAct mid = SettingsAudioPage.HitTest(mx, my, w, h, Live());
            if (mid == SettingsAudioPage.AudioAct.None) continue;
            live++;
            if (SettingsAudioPage.HitTest(mx - hw + 1f, my, w, h, Live()) != mid
                || SettingsAudioPage.HitTest(mx + hw - 1f, my, w, h, Live()) != mid
                || SettingsAudioPage.HitTest(mx, c.B + 1f, w, h, Live()) != mid
                || SettingsAudioPage.HitTest(mx, c.B + c.D - 1f, w, h, Live()) != mid) edgeBad++;
            if (SettingsAudioPage.HitTest(mx - hw - 2f, my, w, h, Live()) == mid
                || SettingsAudioPage.HitTest(mx + hw + 2f, my, w, h, Live()) == mid
                || SettingsAudioPage.HitTest(mx, c.B - 2f, w, h, Live()) == mid
                || SettingsAudioPage.HitTest(mx, c.B + c.D + 2f, w, h, Live()) == mid) outsideBad++;
        }
        Check("S134e four plates answer the touch" + at, live == 4, "got " + live);
        Check("S134e ...each over the whole plate it is drawn as" + at, edgeBad == 0,
              edgeBad + " plate(s) dead inside their own border");
        Check("S134e ...and not one pixel past it" + at, outsideBad == 0,
              outsideBad + " plate(s) taking touches off their own face");
    }

    // =============================================================================================
    //  QC A-04 — the signal glyph reads as one of the three marks in its row
    // =============================================================================================
    static void Glyph(int w, int h)
    {
        string at = " @" + w + "x" + h;
        DisplayList dl = new DisplayList(SettingsAudioPage.Commands + 200);
        SettingsAudioPage.Build(dl, w, h, SettingsAudioPage.CabinScope, Live());
        float sy = h / 2112f;

        // ---- the ± glyphs, read from the render: their centre line and their span ----------------
        // A minus is the only horizontal line in the button row; there are four of them and they must
        // agree, so their y IS the row's centre line as DRAWN.
        float lineY = -1f, span = -1f, stroke = -1f; int minuses = 0; bool oneLine = true;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Line) continue;
            if (Math.Abs(c.B - c.D) > 0.01f) continue;            // horizontal
            if (c.B < 1500f * sy) continue;                       // the button row, not the dividers
            minuses++;
            float y = c.B, sp = Math.Abs(c.C - c.A);
            if (lineY < 0f) { lineY = y; span = sp; stroke = c.StartDeg; }
            else if (Math.Abs(y - lineY) > 0.01f || Math.Abs(sp - span) > 0.01f) oneLine = false;
        }
        Check("S134e eight horizontal glyph strokes (four minus, four plus)" + at, minuses == 8,
              "got " + minuses);
        Check("S134e ...all on one centre line, all one span" + at, oneLine, "");
        if (lineY < 0f) return;

        // ---- the two signal fans, read from the render -------------------------------------------
        // Grouped by their own centre-x, which is all the display list says about which is which.
        float[] gx = new float[4]; int ng = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.ArcBand || c.B < 1500f * sy) continue;
            bool seen = false;
            for (int k = 0; k < ng; k++) if (Math.Abs(gx[k] - c.A) < 0.01f) seen = true;
            if (!seen && ng < gx.Length) gx[ng++] = c.A;
        }
        Check("S134e two signal glyphs in the button row" + at, ng == 2, "got " + ng);

        for (int k = 0; k < ng; k++)
        {
            string aat = at + " glyph" + k;
            int fan = 0, dots = 0;
            float cy = 0f, rMax = 0f, dotR = 0f, sweep = 0f, band = -1f;
            bool oneBand = true;
            float[] inner = new float[8], outer = new float[8]; int nf = 0;
            for (int i = 0; i < dl.Count; i++)
            {
                DrawCmd c = dl.At(i);
                if (c.Kind != DrawKind.ArcBand || c.B < 1500f * sy) continue;
                if (Math.Abs(c.A - gx[k]) > 0.01f) continue;
                cy = c.B;
                if (c.C <= 0.01f) { dots++; dotR = c.D; continue; }
                fan++;
                if (nf < inner.Length) { inner[nf] = c.C; outer[nf] = c.D; nf++; }
                if (c.D > rMax) rMax = c.D;
                float hs = Math.Max(Math.Abs(c.StartDeg), Math.Abs(c.EndDeg));
                if (hs > sweep) sweep = hs;
                float t = c.D - c.C;
                if (band < 0f) band = t; else if (Math.Abs(t - band) > 0.01f) oneBand = false;
            }
            Check("S134e it is three arcs and one source dot" + aat, fan == 3 && dots == 1,
                  fan + " arcs, " + dots + " dots");
            if (fan != 3 || dots != 1) continue;

            // ⭐ (1) ONE CENTRE LINE WITH THE ±. The fan is one-sided — arcs up, dot down — so what has
            // to land on the line is the INK box, not the arc origin. Filed defect: 22 design px low.
            float top = cy - rMax, bot = cy + dotR;
            float ink = (top + bot) * 0.5f;
            Check("S134e the glyph's ink is on the same centre line as the ±" + aat,
                  Math.Abs(ink - lineY) <= 1.0f,
                  "ink " + ink.ToString("F2") + " vs line " + lineY.ToString("F2")
                  + " (" + ((ink - lineY) / sy).ToString("F1") + " design px)");

            // ⭐ (2) AND IT IS THE SIBLINGS' SIZE. Filed: "under a third of the width its siblings use".
            float wide = 2f * rMax * (float)Math.Sin(sweep * Math.PI / 180.0);
            float ratio = wide / span;
            Check("S134e ...and as wide as they are" + aat, ratio >= 0.85f && ratio <= 1.35f,
                  "glyph " + wide.ToString("F1") + " px vs ± " + span.ToString("F1")
                  + " px, ratio " + ratio.ToString("F2"));

            // ⭐ (3) ARCS, NOT A WEDGE. The filed glyph was ONE band 14 design px thick over ±55°,
            // which reads as a solid mushroom. Three bands the weight of the ± strokes, with daylight
            // between them, is what a fan is.
            Check("S134e the arcs carry the ± strokes' own weight" + aat,
                  band > 0f && Math.Abs(band - stroke) <= 0.01f,
                  "band " + band.ToString("F2") + " vs stroke " + stroke.ToString("F2"));
            Check("S134e ...one weight for all three" + aat, oneBand, "");
            Array.Sort(outer, 0, nf); Array.Sort(inner, 0, nf);
            int touching = 0;
            for (int i = 1; i < nf; i++) if (inner[i] <= outer[i - 1] + 0.01f) touching++;
            Check("S134e ...and daylight between them, so they read as three" + aat, touching == 0,
                  touching + " pair(s) touching");
            Check("S134e the dot sits clear inside the innermost arc" + aat,
                  dotR > 0f && dotR < inner[0] - 0.01f,
                  "dot r " + dotR.ToString("F2") + " vs inner " + inner[0].ToString("F2"));

            // ---- and none of it escapes its own plate ------------------------------------------
            float bx0 = 0f, bx1 = 0f, by0 = 0f, by1 = 0f; bool found = false;
            for (int i = 0; i < dl.Count; i++)
            {
                DrawCmd c = dl.At(i);
                if (c.Kind != DrawKind.Rect || Math.Abs(c.C - c.D) > 0.01f || c.C < 20f) continue;
                if (c.B < 1500f * sy) continue;
                if (Math.Abs(c.A + c.C * 0.5f - gx[k]) > 0.01f) continue;
                bx0 = c.A; bx1 = c.A + c.C; by0 = c.B; by1 = c.B + c.D; found = true;
            }
            Check("S134e the glyph has a plate under it" + aat, found, "");
            if (!found) continue;
            Check("S134e the whole mark is inside that plate" + aat,
                  top >= by0 && bot <= by1
                  && gx[k] - wide * 0.5f >= bx0 && gx[k] + wide * 0.5f <= bx1,
                  "ink y " + top.ToString("F1") + ".." + bot.ToString("F1")
                  + " in " + by0.ToString("F1") + ".." + by1.ToString("F1"));
        }
    }

    // ⚠ The rebuilt glyph draws four commands where the old one drew two. `Commands` is what the glue
    // sizes its buffer from, and a DisplayList that overflows drops commands SILENTLY (it only sets a
    // flag), so the page would lose its tail rather than fail.
    static void Capacity()
    {
        DisplayList dl = new DisplayList(SettingsAudioPage.Commands);
        SettingsAudioPage.Build(dl, 2560, 1406, 1, Live());
        Check("S134e the page still fits in its declared capacity", !dl.Overflowed,
              SettingsAudioPage.Commands + " declared, " + dl.Count + " used");
    }

    static int CellOf(float[] edge, float d)
    {
        for (int i = 0; i < 5; i++) if (d >= edge[i] && d < edge[i + 1]) return i;
        return -1;
    }

    static PageState Live()
    {
        PageState s = new PageState();
        s.Valid = true;
        AudioLevels a = new AudioLevels();
        a.Valid = true; a.Master = 0.8f; a.Ambience = 0.6f; a.Voice = 0.5f; a.Ship = 0.7f;
        s.Audio = a;
        s.CrewText = "3";
        return s;
    }

    static bool Near(float a, float b, float tol) { return Math.Abs(a - b) <= tol; }
    static bool Same(Rgba a, Rgba b)
    { return Math.Abs(a.R - b.R) < 1e-4f && Math.Abs(a.G - b.G) < 1e-4f && Math.Abs(a.B - b.B) < 1e-4f; }
    static string Join(float[] a)
    {
        string s = "";
        for (int i = 0; i < a.Length; i++) s += (i > 0 ? ", " : "") + a[i].ToString("F1");
        return s;
    }
}
