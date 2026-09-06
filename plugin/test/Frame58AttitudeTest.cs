// Tests for [[S154b]] + [[S154c]] / QC `H-02` / H10 — Frame 58's TWELVE baked numbers, drawn live
// over their own ink. S154b is the six attitude readouts; S154c the six translation ones.
//
// ⛔ WHAT WAS WRONG. QC `H-02` counted TWELVE baked numbers on this frame, EIGHT of which contradict
// live state in the same frame. Six of them are the attitude block — ROLL / PITCH / YAW and their
// rates — and they are the worst of the twelve because the page's whole job is attitude: the bowl beside
// them has been live since T5, while the numbers under it were a picture of someone else's docking.
//
// ---- ⭐ THE TWO FAILURE MODES THIS SUITE IS ACTUALLY FOR --------------------------------------
// Drawing live text over a raster has two ways to go wrong that a normal "is it live" check misses,
// and BOTH are specific to this technique:
//
//   (1) THE PATCH IS SKIPPED AND THE BAKED NUMBER SHOWS THROUGH. Two numbers in one box, or worse,
//       the old one alone. A check that only asks "is the live value drawn" passes while the frame
//       still shows the export's 15.0°.
//   (2) THE PATCH IS SKIPPED ON A DEAD FEED. This is the sharper one: with no value there is nothing
//       to draw, so a version that returns early leaves the BAKED number showing — and the page then
//       prints a confident attitude at the exact moment it has none. ⚠ That is `E4` ([[S147]]: never
//       state a value the vehicle has not supplied) applied to a raster instead of to a string, and it
//       is strictly worse than a blank, because it looks right.
//
// So every check below comes in a pair: the live render and the dead one.
using System;
using DragonScreen;

public static class Frame58AttitudeTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    const int W = 2560, H = 1406;

    public static int Run()
    {
        Console.WriteLine("Frame58AttitudeTest (S154b + S154c / QC H-02: all twelve baked numbers, now live)");
        checks = 0; failures = 0;

        PageState a = Fixture("15.0°", "0.1°", "0.2°", "0.0 deg/s", "0.3 deg/s", "0.4 deg/s");
        PageState b = Fixture("-42.5°", "7.7°", "3.3°", "1.1 deg/s", "2.2 deg/s", "5.5 deg/s");
        PageState dead = a; dead.Valid = false;

        DisplayList da = Build(a), db = Build(b), dd = Build(dead);

        // ---- 1. ALL SIX ARE DRAWN, AND NONE OF THEM IS A CONSTANT ------------------------------
        string[] va = { a.RollDegText, a.PitchDegText, a.YawDegText,
                        a.RollRateText, a.PitchRateText, a.YawRateText };
        for (int i = 0; i < va.Length; i++)
        {
            Check("S154b the HUD draws " + va[i], Drew(da, va[i]), "");
            Check("S154b ⭐ " + va[i] + " is not a constant", !Drew(db, va[i]),
                  "still drawn for a completely different attitude");
            Check("S154b " + va[i] + " is gone with no feed", !Drew(dd, va[i]), "");
        }

        // ---- 2. ⛔ THE PATCH, WHICH IS THE HALF A LIVENESS CHECK CANNOT SEE --------------------
        // One opaque Background rect per readout, over the box `Frame58Map` measured. Counted rather
        // than merely "present": five patches for six readouts is the bug that leaves one baked number
        // showing, and it looks identical to the correct render on every other check in this file.
        Check("S154b ⭐ six baked boxes are patched on a LIVE feed", PatchesOver(da) == 6,
              "got " + PatchesOver(da));
        Check("S154b ⭐⭐ ...and still six on a DEAD one — or the baked number shows through",
              PatchesOver(dd) == 6, "got " + PatchesOver(dd));

        // ---- 3. A DEAD FEED DASHES, it does not go blank ----------------------------------------
        // ⚠ Six dashes, not "no text": a patched box with nothing in it reads as a rendering fault,
        // and the mod's one idiom for "the source is absent" is the em dash.
        Check("S154b a dead feed draws six dashes", CountText(dd, Dashes.None) >= 6,
              "got " + CountText(dd, Dashes.None));

        // ---- 4. AT THE MEASURED BOXES, AND AT THE LEGIBILITY FLOOR ------------------------------
        // ⭐ The DONE-when says "at the mapped boxes, at MinDesignFor". Both are checked against the
        // SOURCES rather than against numbers typed here: the box centres come from `Frame58Map` and
        // the size from `Typography`, so neither can drift from what the rest of the build believes.
        float sc = H / Frame58Map.RefH, ox = (W - Frame58Map.RefW * sc) * 0.5f;
        float want = Typography.MinDesignFor(W, sc) * sc;
        Frame58Map.Box[] boxes = { Frame58Map.RollValue, Frame58Map.PitchValue, Frame58Map.YawValue,
                                   Frame58Map.RollRate,  Frame58Map.PitchRate,  Frame58Map.YawRate };
        for (int i = 0; i < boxes.Length; i++)
        {
            float cx = ox + boxes[i].Cx * sc;
            // ⛔ THE Y TOO, AND MUTATION X7 IS WHY. Checking only x let a version that anchored every
            // readout at its box's TOP pass — which walks each number upward by half the difference
            // between the baked size and the floor, roughly 11 design px, on all six at once. The box
            // is an INK bounding box, so its CENTRE is where the number looked centred in the export.
            float cy = (boxes[i].Cy - want / sc * 0.5f) * sc;
            bool at = false, sized = false, placed = false;
            for (int k = 0; k < da.Count; k++)
            {
                DrawCmd c = da.At(k);
                if (c.Kind != DrawKind.Text || Math.Abs(c.A - cx) > 0.5f) continue;
                at = true;
                if (Math.Abs(c.C - want) < 0.01f) sized = true;
                if (Math.Abs(c.B - cy) < 0.5f) placed = true;
            }
            Check("S154b readout " + i + " is drawn at its mapped box centre", at,
                  "nothing at x " + cx);
            Check("S154b readout " + i + " is CENTRED on the box, not hung from its top", placed,
                  "wanted y " + cy);
            Check("S154b readout " + i + " is at MinDesignFor", sized,
                  "wanted " + want + " px");
        }

        // ---- 5. THE COLOUR SPLIT IS THE DRAWING'S OWN ------------------------------------------
        // Values green, rates cyan — `#1FE327` and `#20FBFD`, which [[S154a]] measured off the export
        // and which are `DragonPalette.Go` and `.Accent` exactly. ⚠ Checked because getting it backwards
        // is invisible to every other check here and would silently re-code what the frame means.
        Check("S154b the three VALUES draw in the frame's green",
              Same(InkOf(da, a.RollDegText), DragonPalette.Go)
              && Same(InkOf(da, a.PitchDegText), DragonPalette.Go)
              && Same(InkOf(da, a.YawDegText), DragonPalette.Go), "");
        Check("S154b the three RATES draw in the frame's cyan",
              Same(InkOf(da, a.RollRateText), DragonPalette.Accent)
              && Same(InkOf(da, a.PitchRateText), DragonPalette.Accent)
              && Same(InkOf(da, a.YawRateText), DragonPalette.Accent), "");
        // ⚠ `Rgba` stores 0..1 FLOATS, not bytes - so the export's hex is compared by reconstructing
        // the colour from the string the drawing actually carries, rather than by testing channels
        // against 0x1F. Getting that wrong is what the first version of this check did.
        Check("S154b ...and the two inks are the export's own, not near-misses",
              Same(DragonPalette.Go, Rgba.Hex("1FE327"))
              && Same(DragonPalette.Accent, Rgba.Hex("20FBFD")),
              "Go " + DragonPalette.Go.R + "," + DragonPalette.Go.G + "," + DragonPalette.Go.B);

        TranslationBlock();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (the patch is counted on both a live and a dead feed)");
        return failures > 0 ? 1 : 0;
    }

    // =============================================================================================
    // [[S154c]] — the other six of QC `H-02`'s twelve: X / Y / Z, RANGE, RATE, ACCELERATION.
    // Same technique, same two failure modes, plus two of its own.
    // =============================================================================================
    static void TranslationBlock()
    {
        PageState a = Trans("22.7 m", "0.1 m", "0.0 m", "202.6 m", "-0.25 m/s", "1.42");
        PageState b = Trans("-8.4 m", "9.9 m", "4.2 m", "88.8 m",  "0.75 m/s",  "0.31");
        PageState dead = a; dead.Valid = false;
        DisplayList da = Build(a), db = Build(b), dd = Build(dead);

        string[] v = { a.OffXText, a.OffYText, a.OffZText, a.RangeText, a.RateText };
        for (int i = 0; i < v.Length; i++)
        {
            Check("S154c the HUD draws " + v[i], Drew(da, v[i]), "");
            Check("S154c ⭐ " + v[i] + " is not a constant", !Drew(db, v[i]), "");
            Check("S154c " + v[i] + " is gone with no feed", !Drew(dd, v[i]), "");
        }

        // ---- ⛔ ACCELERATION CARRIES ITS UNIT, AND THAT IS ITS OWN CHECK ------------------------
        // `AccelValue`'s box is the ink of the WHOLE baked string `0.00g` — digits and the `g` — so
        // patching it erases the unit, and `AccelPosText` is a bare number. The first render of this
        // block read `1.42` under a label saying ACCELERATION. ⚠ A quantity with no unit on a flight
        // display is a defect of the same family as a frozen one: it looks like data and is not usable.
        Check("S154c ⭐ ACCELERATION prints its unit, which the patch erased", Drew(da, "1.42g"),
              "drew the bare number instead");
        Check("S154c ...and the bare number is NOT drawn on its own", !Drew(da, "1.42"), "");
        Check("S154c ⭐ the unit does not make it a constant", !Drew(db, "1.42g"), "");
        Check("S154c ACCELERATION dashes with no feed", !Drew(dd, "1.42g"), "");

        // ---- ⛔ THE X/Y/Z STACK DOES NOT OVERLAP ITSELF ------------------------------------------
        // ⭐ THE REASON THIS BLOCK IS NOT SIX `Readout` CALLS. The export pitches those three rows 35
        // design px apart around 17-px ink; at the glanceable floor the type is 48.07, so keeping the
        // export's pitch would overlap every row by 13 px. The stack is re-pitched about its own
        // centre, and this is what says it worked — measured off the draw commands, not eyeballed.
        float sc = H / Frame58Map.RefH, ox = (W - Frame58Map.RefW * sc) * 0.5f;
        float size = Typography.MinDesignFor(W, sc) * sc;
        float[] tops = { YOf(da, a.OffXText), YOf(da, a.OffYText), YOf(da, a.OffZText) };
        Check("S154c all three offsets are drawn", tops[0] >= 0f && tops[1] >= 0f && tops[2] >= 0f, "");
        if (tops[0] >= 0f && tops[1] >= 0f && tops[2] >= 0f)
        {
            Check("S154c ⭐ X and Y do not overlap", tops[1] - tops[0] >= size,
                  "pitch " + (tops[1] - tops[0]) + " px for " + size + " px type");
            Check("S154c ⭐ Y and Z do not overlap", tops[2] - tops[1] >= size,
                  "pitch " + (tops[2] - tops[1]) + " px for " + size + " px type");
            Check("S154c ...and the pitch is even", Math.Abs((tops[1] - tops[0]) - (tops[2] - tops[1])) < 0.5f,
                  "");
            // ⛔ AND THE GROUP HAS NOT WALKED. Re-pitching about the TOP row would push the block down
            // the frame by half the growth; about the centre it stays where the export put it.
            float drawnCentre = (tops[0] + tops[2]) * 0.5f + size * 0.5f;
            float exportCentre = (Frame58Map.XValue.Cy + Frame58Map.ZValue.Cy) * 0.5f * sc;
            Check("S154c ⭐ the stack is still centred where the export put it",
                  Math.Abs(drawnCentre - exportCentre) < 1.0f,
                  "drawn " + drawnCentre + " vs export " + exportCentre);
        }

        // ---- the frame's own two inks here too -------------------------------------------------
        // ⚠ RANGE and the three offsets are VALUES (green); RATE is a rate (cyan). Same split as the
        // attitude block, and getting it backwards would silently re-code what the frame means.
        Check("S154c the offsets and RANGE draw green",
              Same(InkOf(da, a.OffXText), DragonPalette.Go)
              && Same(InkOf(da, a.OffZText), DragonPalette.Go)
              && Same(InkOf(da, a.RangeText), DragonPalette.Go), "");
        Check("S154c RATE draws cyan", Same(InkOf(da, a.RateText), DragonPalette.Accent), "");
        Check("S154c ACCELERATION draws green", Same(InkOf(da, "1.42g"), DragonPalette.Go), "");

        // ---- the patch, on both feeds, for ALL SIX boxes ----------------------------------------
        // ⚠ ALL SIX, not just the three single ones. Mutation Y6 dropped the patch from the X/Y/Z
        // stack alone and SURVIVED a version that counted only RANGE / RATE / ACCELERATION - the three
        // baked offsets would have stayed on the frame under the live ones, and every other check here
        // passed. A patch count that does not cover every patched box is a patch count for the boxes
        // someone remembered.
        Check("S154c all six translation boxes are patched on a live feed", TransPatches(da) == 6,
              "got " + TransPatches(da));
        Check("S154c ⭐ ...and on a dead one", TransPatches(dd) == 6, "got " + TransPatches(dd));
    }

    static PageState Trans(string x, string y, string z, string range, string rate, string accel)
    {
        PageState s = new PageState();
        s.Valid = true;
        s.OffXText = x; s.OffYText = y; s.OffZText = z;
        s.RangeText = range; s.RateText = rate; s.AccelPosText = accel;
        return s;
    }

    /// <summary>The y a string was drawn at, or −1.</summary>
    static float YOf(DisplayList dl, string t)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && c.Str == t) return c.B;
        }
        return -1f;
    }

    static int TransPatches(DisplayList dl)
    {
        Frame58Map.Box[] boxes = { Frame58Map.RangeValue, Frame58Map.RateValue, Frame58Map.AccelValue,
                                   Frame58Map.XValue, Frame58Map.YValue, Frame58Map.ZValue };
        float sc = H / Frame58Map.RefH, ox = (W - Frame58Map.RefW * sc) * 0.5f;
        int found = 0;
        for (int b = 0; b < boxes.Length; b++)
        {
            float x = ox + boxes[b].X0 * sc, y = boxes[b].Y0 * sc;
            for (int i = 0; i < dl.Count; i++)
            {
                DrawCmd c = dl.At(i);
                if (c.Kind != DrawKind.Rect || !Same(c.Colour, DragonPalette.Background)) continue;
                if (!(c.A <= x + 0.01f && c.B <= y + 0.01f
                      && c.A + c.C >= ox + boxes[b].X1 * sc - 0.01f
                      && c.B + c.D >= boxes[b].Y1 * sc - 0.01f)) continue;
                const float Slop = 12f;
                if (c.C > (boxes[b].W + Slop) * sc || c.D > (boxes[b].H + Slop) * sc) continue;
                found++; break;
            }
        }
        return found;
    }

    static PageState Fixture(string roll, string pitch, string yaw,
                             string rr, string pr, string yr)
    {
        PageState s = new PageState();
        s.Valid = true;
        s.RollDegText = roll; s.PitchDegText = pitch; s.YawDegText = yaw;
        s.RollRateText = rr; s.PitchRateText = pr; s.YawRateText = yr;
        return s;
    }

    static DisplayList Build(PageState s)
    {
        DisplayList dl = new DisplayList(Frame58Hud.Commands + 60);
        Frame58Hud.Build(dl, W, H, s);
        return dl;
    }

    static bool Drew(DisplayList dl, string t)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && c.Str == t) return true;
        }
        return false;
    }

    static int CountText(DisplayList dl, string t)
    {
        int n = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && c.Str == t) n++;
        }
        return n;
    }

    static Rgba InkOf(DisplayList dl, string t)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && c.Str == t) return c.Colour;
        }
        return new Rgba();
    }

    /// <summary>`Rgba` has no `==`, so compare the four channels. ⚠ EXACT, not near: the point of the
    /// colour check is that the live ink is the export's OWN ink and not a lookalike.</summary>
    static bool Same(Rgba a, Rgba b) { return a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A; }

    /// <summary>Background rects covering the six mapped attitude boxes — the patch, counted.</summary>
    static int PatchesOver(DisplayList dl)
    {
        float sc = H / Frame58Map.RefH, ox = (W - Frame58Map.RefW * sc) * 0.5f;
        Frame58Map.Box[] boxes = { Frame58Map.RollValue, Frame58Map.PitchValue, Frame58Map.YawValue,
                                   Frame58Map.RollRate,  Frame58Map.PitchRate,  Frame58Map.YawRate };
        int found = 0;
        for (int b = 0; b < boxes.Length; b++)
        {
            float x = ox + boxes[b].X0 * sc, y = boxes[b].Y0 * sc;
            for (int i = 0; i < dl.Count; i++)
            {
                DrawCmd c = dl.At(i);
                if (c.Kind != DrawKind.Rect || !Same(c.Colour, DragonPalette.Background)) continue;
                // the patch is the box plus a small bleed, so it starts at or just before the ink
                if (!(c.A <= x + 0.01f && c.B <= y + 0.01f
                      && c.A + c.C >= ox + boxes[b].X1 * sc - 0.01f
                      && c.B + c.D >= boxes[b].Y1 * sc - 0.01f)) continue;
                // ⛔ AND IT MUST BE A PATCH, NOT THE PAGE'S OWN GROUND. Found by mutation X2: deleting
                // `Patch` entirely left this check GREEN, because `Build`'s opening
                // `dl.Rect(0, 0, w, h, Background)` covers every box on the frame and satisfied the
                // containment test above. A check that any covering rect exists is a check that the
                // page has a background. So the rect must be BOUNDED to the box it claims to patch —
                // no more than a few design px larger on each side.
                const float Slop = 12f;
                if (c.C > (boxes[b].W + Slop) * sc || c.D > (boxes[b].H + Slop) * sc) continue;
                found++; break;
            }
        }
        return found;
    }
}
