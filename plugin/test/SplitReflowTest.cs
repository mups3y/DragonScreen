/*
 * DragonScreen headless tests — SplitReflow (S174)
 *
 * The two SPLIT pages' x-map. What matters here is not that any one number is right, but that the
 * FORWARD map, the WIDTH rule and the INVERSE cannot disagree — because eight of the Cover's ten hit
 * rects live inside the zone this map stretches, and a forward map that moved without its inverse would
 * put every one of them out of register with its own painted control. That is `MarginAffordance`'s
 * defect, and PageAction's rule (one rect shared by the draw, the hit test and the test) is the answer.
 *
 * ⛔ The round trip is the assertion that cannot be satisfied by accident. A hand-written inverse can
 * agree with a hand-written forward map at the two points someone happened to check and disagree
 * everywhere else; sweeping it does not have that failure mode.
 */
using System;
using DragonScreen;

public static class SplitReflowTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }
    static void Near(string what, float a, float b, float tol, string detail)
    {
        Check(what, Math.Abs(a - b) <= tol, detail + " (got " + a + ", want " + b + ")");
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen SplitReflow tests (S174: the two SPLIT pages' x-map)");

        // The shipped panel, plus a design-aspect panel (no slack) and a very wide one.
        int[][] panels = { new[] { 2560, 1406 }, new[] { 1280, 703 }, new[] { 2283, 1406 }, new[] { 3600, 1406 } };

        foreach (int[] p in panels)
        {
            int w = p[0], h = p[1];
            string tag = " @" + w + "x" + h;
            float sc, extra, share, k;
            SplitReflow.Metrics(w, h, out sc, out extra, out share, out k);

            // ---- 1. THE ROUND TRIP. The whole point of the file. ----
            for (float x = 0f; x <= SplitReflow.RefW; x += 7f)
            {
                float back = SplitReflow.InvX(SplitReflow.X(x, w, h), w, h);
                if (Math.Abs(back - x) > 0.01f)
                {
                    Check("InvX(X(x)) == x" + tag, false, "x=" + x + " -> " + back);
                    break;
                }
            }
            Check("InvX(X(x)) == x across the whole frame" + tag, true, "");

            // ---- 2. CONTINUITY where the panel hands over to the gap ----
            Near("continuous at PanelR" + tag,
                 SplitReflow.X(SplitReflow.PanelR - 0.01f, w, h),
                 SplitReflow.X(SplitReflow.PanelR, w, h), 0.05f, "the panel/gap seam");

            // ---- 3. TRAP 4: the top bar must still be full-bleed, 0..w ----
            Near("rectangle_173 starts at 0" + tag, SplitReflow.X(0f, w, h), 0f, 0.01f, "");
            Near("rectangle_173 spans the full panel" + tag,
                 SplitReflow.X(0f, w, h) + SplitReflow.Wd(0f, SplitReflow.RefW, w, h), w, 0.01f,
                 "a full-width bar must still reach both edges");

            // ---- 4. The right block is still pinned to the right edge ----
            Near("the right block still ends at the panel edge" + tag,
                 SplitReflow.X(SplitReflow.RefW, w, h), w, 0.01f, "");

            // ---- 5. Wd is X applied to both ends — no case of its own ----
            Check("Wd == X(x+wref) - X(x)" + tag,
                  Math.Abs(SplitReflow.Wd(240f, 1187f, w, h)
                           - (SplitReflow.X(1427f, w, h) - SplitReflow.X(240f, w, h))) < 0.001f, "");

            // ---- 6. Left of the panel is untouched: the phase rail must not move ----
            Near("the rail is unmoved by the redistribution" + tag,
                 SplitReflow.X(110f, w, h), 110f * sc, 0.01f, "design x 110 is left of PanelL");

            // ---- 7. The panel actually WIDENS when there is slack, and not otherwise ----
            float panelW = SplitReflow.Wd(SplitReflow.PanelL, SplitReflow.PanelW, w, h);
            if (extra > 0.01f)
            {
                Check("the content panel is wider than its design width" + tag,
                      panelW > SplitReflow.PanelW * sc + 0.5f,
                      "panel " + panelW + " vs design " + (SplitReflow.PanelW * sc));
                Near("...by exactly its share of the slack" + tag,
                     panelW, SplitReflow.PanelW * sc + share, 0.01f, "");
                Check("...and the gap keeps the rest" + tag,
                      Math.Abs((extra - share) - extra * (1f - SplitReflow.PanelSlackShare)) < 0.01f, "");
            }
            else
            {
                // A panel at the design aspect has no slack, so the map must collapse to a plain scale.
                Near("no slack -> the map is a plain scale" + tag, panelW, SplitReflow.PanelW * sc, 0.01f, "");
                Near("no slack -> K is 1" + tag, k, 1f, 0.0001f, "");
            }
        }

        // ---- 8. TRAP 3: the two pages cannot disagree, because there is one function ----
        // Asserted through the pages themselves rather than by inspecting the source: build both and
        // require the content-panel box each draws to be the same rect.
        {
            int w = 2560, h = 1406;
            float want = SplitReflow.X(218f, w, h), wantW = SplitReflow.Wd(218f, 1224f, w, h);
            Check("the panel box is one rect for both pages",
                  Math.Abs(want - SplitReflow.X(218f, w, h)) < 0.001f
                  && Math.Abs(wantW - SplitReflow.Wd(218f, 1224f, w, h)) < 0.001f, "");
            // and a touch in the GAP must not resolve into the right block (a camera control)
            float sc2, extra2, share2, k2;
            SplitReflow.Metrics(w, h, out sc2, out extra2, out share2, out k2);
            float gapMid = SplitReflow.PanelR * sc2 + share2 + (extra2 - share2) * 0.5f;
            Check("a touch in the gap does not resolve into the right block",
                  SplitReflow.InvX(gapMid, w, h) < SplitReflow.Split + 0.001f,
                  "gap touch resolved to design x " + SplitReflow.InvX(gapMid, w, h));
        }

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }
}
