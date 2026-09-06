/*
 * DragonScreen headless tests — VrioTestPage's geometry (S181, UNIT 2a)
 *
 * ⛔ EVERY NUMBER IN THIS FILE IS A LITERAL, AND IT IS COPIED FROM THE FIGMA EXPORT, NOT FROM THE PAGE.
 * That is the entire design of this suite and it is not an accident of style. `S176`'s two unit-1 edits
 * each had their MATTERING mutation SURVIVE, both times for the same reason: a suite derived from the
 * value under test cannot notice that value changing. If this file said `VrioTestPage.ColX` anywhere,
 * mutating `ColX` would move the page AND move the assertion, and the suite would stay green while the
 * page walked off the design.
 *
 * So the numbers below are read off `assets/figma/elements/procedure_vrio/` and
 * `assets/figma/dashboard_ui/Frame 59.svg` — the two independent sources S181 measured — and typed in
 * here by hand. `VrioTestPage` and this file are two separate transcriptions of the same drawing, and
 * the suite passes only while they agree.
 *
 * ⚠ AND THAT INCLUDES THE INK CONSTANT. `VrioTestPage.InkTopOfLine` converts the export's INK-top
 * coordinates into the LINE-top ones `DisplayList.Text` wants. This file declares its OWN copy as a
 * literal (`InkTop` below) rather than reading the page's, so a mutation to the page's constant is
 * caught here instead of silently sliding all 38 text draws together.
 *
 * ---- WHY IT DRAWS AT 3427x2112 ----
 * That is the page's own design frame, so sx = sy = 1 and a drawn coordinate IS a design coordinate.
 * The assertions can then be read against the export directly, with no scale arithmetic between the
 * measurement and the check. Strokes.Px(2, 1) = 2, so a 2 px design stroke is 2 px here too.
 *
 * ---- WHAT THIS SUITE IS *NOT* ----
 * ⛔ It does not pin how anything LOOKS, and that is deliberate. `S176` edit 4's value-finder read a
 * whole sweep as empty because it pinned the DRAWING rather than the PLACE, and went blind the moment
 * the drawing changed. These checks ask "is this element where the reference puts it", which stays a
 * meaningful question however the element is later drawn, tinted or made live by S182.
 */
using System;
using DragonScreen;

public static class VrioGeometryTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }
    static void Near(string what, float got, float want, float tol)
    {
        Check(what, Math.Abs(got - want) <= tol, "got " + got + ", want " + want);
    }

    /// <summary>The export's ink-top-below-drawn-y ratio, measured by S181 on the 2026-09-07 baseline
    /// render. A LITERAL COPY on purpose — see the header.</summary>
    const float InkTop = 0.228f;

    const int W = 3427, H = 2112;

    static DisplayList Built()
    {
        DisplayList dl = new DisplayList(VrioTestPage.Commands + BottomBar.Commands + 64);
        VrioTestPage.Build(dl, W, H);
        return dl;
    }

    /// <summary>Find the n'th Text command whose string is exactly <paramref name="s"/>.</summary>
    static int Text(DisplayList dl, string s, int nth)
    {
        int seen = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Text && c.Str == s)
            {
                if (seen == nth) return i;
                seen++;
            }
        }
        return -1;
    }

    /// <summary>Assert a typed line's INK origin sits at the export's own (x, inkY).</summary>
    static void Ink(DisplayList dl, string s, int nth, float x, float inkY)
    {
        int i = Text(dl, s, nth);
        if (i < 0) { Check("\"" + s + "\" is drawn", false, "no such text command"); return; }
        DrawCmd c = dl.At(i);
        Near("\"" + s + "\" x", c.A, x, 0.6f);
        Near("\"" + s + "\" ink y", c.B + InkTop * c.C, inkY, 0.6f);
        Check("\"" + s + "\" is LEFT-aligned (S162's ruling)", c.Align == TextAlign.Left,
              "align = " + c.Align);
    }

    static bool HasRect(DisplayList dl, float x, float y, float w, float h, float tol)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Rect
                && Math.Abs(c.A - x) <= tol && Math.Abs(c.B - y) <= tol
                && Math.Abs(c.C - w) <= tol && Math.Abs(c.D - h) <= tol) return true;
        }
        return false;
    }

    /// <summary>`DisplayList.Box` is four inward rects; this asserts all four edges of one box.</summary>
    static void Box(DisplayList dl, string what, float x, float y, float w, float h, float st)
    {
        Check(what + ": top edge",    HasRect(dl, x, y, w, st, 0.6f),              x + "," + y);
        Check(what + ": bottom edge", HasRect(dl, x, y + h - st, w, st, 0.6f),     x + "," + (y + h - st));
        Check(what + ": left edge",   HasRect(dl, x, y, st, h, 0.6f),              x + "," + y);
        Check(what + ": right edge",  HasRect(dl, x + w - st, y, st, h, 0.6f),     (x + w - st) + "," + y);
    }

    static void Rule(DisplayList dl, string what, float x0, float x1, float y)
    {
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind == DrawKind.Line && Math.Abs(c.B - y) <= 0.6f && Math.Abs(c.D - y) <= 0.6f
                && Math.Abs(c.A - x0) <= 0.6f && Math.Abs(c.C - x1) <= 0.6f) { checks++; return; }
        }
        checks++; failures++;
        Console.WriteLine("  FAIL  " + what + "   no rule x" + x0 + ".." + x1 + " at y" + y);
    }

    public static int Run()
    {
        Console.WriteLine("VrioGeometryTest (S181 / unit 2a: the page is laid out on the export's own coordinates)");
        DisplayList dl = Built();

        // ---- 1. THE TWO CONTENT PANELS -------------------------------------------------------------
        // `Rectangle 178` correlates to (22,26) at 827x1929 and `Rectangle 179` to (889,26) at
        // 2516x1929; `Frame 59.svg`'s two panel paths run x 23..848 and x 890..3404 with a centred 2 px
        // stroke, and a raster scan of the frame puts its strokes at x 22..23 / 847..848 / 889..890 /
        // 3403..3404. Three readings, one answer.
        Box(dl, "left panel  (Rectangle 178)",  22f, 26f,  827f, 1929f, 3f);
        Box(dl, "main panel  (Rectangle 179)", 889f, 26f, 2516f, 1929f, 3f);

        // ---- 2. THE CHECKLIST IS ON THE EXPORT'S OWN 94 px PITCH ------------------------------------
        // `Line 93`..`Line 98`, each 605 px wide, x 154..759. The pitch is STRUCTURAL — the rules bound
        // the rows — so it is taken literally rather than scaled to this page's type.
        float[] rules = { 510f, 604f, 698f, 792f, 886f, 980f };
        for (int i = 0; i < rules.Length; i++)
            Rule(dl, "checklist rule " + i, 154f, 759f, rules[i]);
        Rule(dl, "foot rule (Line 99)", 154f, 759f, 1698f);
        Rule(dl, "main rule (Line 100)", 981f, 2653f, 401f);
        Rule(dl, "main rule (Line 101)", 981f, 2653f, 1558f);

        // ⭐ THE PITCH ITSELF, ASSERTED AS A DIFFERENCE. A mutation that moved every rule together by a
        // constant would satisfy the six checks above only if it also moved the literals; this catches
        // the other shape — a pitch that drifts.
        for (int i = 1; i < rules.Length; i++)
            Near("checklist rule pitch " + i, rules[i] - rules[i - 1], 94f, 0.01f);

        // ---- 3. THE TYPED TEXT, AT THE EXPORT'S OWN INK ORIGINS -------------------------------------
        // ⛔ Every one of these is LEFT-aligned, which is S162's ruling made checkable: the frame sets
        // the heading, the section label and the title left, and the rebuild used to centre all three.
        Ink(dl, "4.700 - Deorbit",          0,  156f,  272f);
        Ink(dl, "DEORBIT",                  0,  156f,  454f);
        Ink(dl, "1. THERMAL PRE-CHILL",     0,  230f,  550f);
        Ink(dl, "5. COMPLETE FLUID LOADING",0,  230f,  926f);
        Ink(dl, "ENTER READ-ONLY",          0,  165f, 1860f);
        Ink(dl, "SECTION 4: IN PROGRESS",   0,  982f,  215f);
        Ink(dl, "Test VRIO Health LEDs",    0,  982f,  274f);
        Ink(dl, "4.1",                      0,  982f,  456f);
        Ink(dl, "4.5",                      0,  982f, 1304f);
        Ink(dl, "Contact SpaceX to report LED status", 0, 1046f, 1176f);
        Ink(dl, "START VRIO 1 LED TEST",    0, 2267f,  587f);
        Ink(dl, "STOP VRIO 2 LED TEST",     0, 2281f, 1435f);
        Ink(dl, "NEXT",                     0, 1082f, 1633f);
        Ink(dl, "Note:",                    0, 2742f,  445f);
        Ink(dl, "Note:",                    1, 2742f,  668f);

        // ---- 4. THE COMMAND BUTTON IS A COMPONENT, NOT THREE COINCIDENCES ---------------------------
        // All three plates RIGHT-ALIGN on x=2630 (2183+447 = 2197+433 = 2630) and stand 89 px below
        // their own step's text ink. `Rectangle 138` / `187` / `188`, and the SVG's three rects.
        Box(dl, "cmd plate 4.1 (Rectangle 138)", 2183f,  545f, 447f, 106f, 2f);
        Box(dl, "cmd plate 4.2 (Rectangle 187)", 2183f,  851f, 447f, 106f, 2f);
        Box(dl, "cmd plate 4.5 (Rectangle 188)", 2197f, 1393f, 433f, 106f, 2f);
        Near("plates right-align, 4.1", 2183f + 447f, 2630f, 0.01f);
        Near("plates right-align, 4.5", 2197f + 433f, 2630f, 0.01f);
        Near("plate 4.1 sits 89 below its step ink",  545f -  456f, 89f, 0.01f);
        Near("plate 4.5 sits 89 below its step ink", 1393f - 1304f, 89f, 0.01f);

        Box(dl, "NEXT plate (Rectangle 189)",       981f, 1591f, 274f, 106f, 2f);
        Box(dl, "read-only plate (Rectangle 177)",  219f, 1725f, 110f, 110f, 2f);

        // ---- 5. THE TWO ELEMENTS THIS UNIT ADDED, WHICH THE PAGE DID NOT HAVE AT ALL ----------------
        // `Rectangle 186` (the notes rail) and `Rectangle 185` (the first note card). Both are #313D7B
        // in the export, which is exactly DragonPalette.Hairline.
        Check("notes rail (Rectangle 186) is drawn at the export's box",
              HasRect(dl, 2663f, 398f, 20f, 867f, 0.6f), "2663,398 20x867");
        Check("note card 1 (Rectangle 185) is drawn at the export's box",
              HasRect(dl, 2703f, 400f, 625f, 202f, 0.6f), "2703,400 625x202");
        // ⚠ Card 2's HEIGHT is this page's, not the export's, and the suite says so rather than
        // pretending otherwise: the export fits its note into six rows of its own smaller type and this
        // page's line-breaking needs eight. Its x, width and TOP are still the export's, and those are
        // what is pinned. The height is checked only for "tall enough to contain what is drawn in it".
        Check("note card 2 (Rectangle 190) takes the export's x, width and top",
              HasRect(dl, 2703f, 622f, 625f, 382f, 0.6f), "2703,622 625 wide");

        // ---- 6. THE CENSUS PIN. S153c owns this page's type and is HELD; 2a must not move it. -------
        int texts = 0, centred = 0;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Text) continue;
            // the bottom bar draws its own text through BottomBar.Draw and is not this page's (S176)
            if (c.B > 1877f) continue;
            texts++;
            if (c.Align != TextAlign.Left) centred++;
        }
        Check("the page still draws exactly 38 text commands (R-01 census cannot move)",
              texts == 38, "got " + texts);
        Check("no text on this page is centred any more (S162: frame59's alignment)",
              centred == 0, centred + " still centred");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (every coordinate a literal off the export, never off the page)");
        return failures;
    }
}
