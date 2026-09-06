// Tests for [[S154b]] / QC `H-02` / H10 — Frame 58's attitude block, drawn live over its baked ink.
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
        Console.WriteLine("Frame58AttitudeTest (S154b / QC H-02: six baked attitude numbers, now live)");
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

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (the patch is counted on both a live and a dead feed)");
        return failures > 0 ? 1 : 0;
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
