// Tests for [[S134b]] / QC `VV-02` — the Video page's camera rows, which drew a selection the crew
// could see and could not change.
//
// ⛔ THE DONE-CRITERION: "a test that the drawn selection and the written one are the same value."
// That is the round trip below: the row the page DREW as selected is found by hit-testing where it
// DREW it, and the index that comes back is the one a caller would write. The two cannot be separately
// wrong because `RowRect` is the only geometry either of them has.
using System;
using DragonScreen;

public static class VideoCamRowsTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    const int W = 2560, H = 1406;
    const float RefW = 3427f, RefH = 2112f;

    public static int Run()
    {
        Console.WriteLine("DragonScreen Video page camera rows (S134b / QC VV-02)");
        checks = 0; failures = 0;

        PageState s = new PageState();
        s.Valid = true;
        s.CamLabels = new[] { "FORWARD", "INTERSTAGE", "TRUNK" };
        s.CameraView = 1;

        // ---- ⭐ THE ROUND TRIP: every drawn row is hit at its own centre --------------------------
        int n = SettingsVideoPage.VisibleCams(s);
        Check("S134b three cameras give three rows", n == 3, "got " + n);
        for (int i = 0; i < n; i++)
        {
            float px, py;
            Centre(i, out px, out py);
            int hit = SettingsVideoPage.HitTest(px, py, W, H, s);
            Check("S134b row " + i + "'s own centre hits row " + i, hit == i, "got " + hit);
        }

        // ---- ⛔ AND NO ROW IS OFFERED THAT WAS NEVER PAINTED -------------------------------------
        // "a button bound to nothing wearing the shape of one that works" — SettingsPage's own note
        // about this same camera list. The hit test must clamp exactly as the draw does.
        {
            float px, py;
            Centre(3, out px, out py);              // one past the last drawn row
            Check("S134b a touch below the last drawn row is inert",
                  SettingsVideoPage.HitTest(px, py, W, H, s) < 0, "");
        }
        {
            PageState none = new PageState(); none.Valid = true;   // CamLabels null
            float px, py; Centre(0, out px, out py);
            Check("S134b with no cameras there are no rows to hit",
                  SettingsVideoPage.VisibleCams(none) == 0
                  && SettingsVideoPage.HitTest(px, py, W, H, none) < 0, "");
        }
        {
            PageState many = new PageState(); many.Valid = true;
            many.CamLabels = new string[20];
            for (int i = 0; i < 20; i++) many.CamLabels[i] = "CAM" + i;
            Check("S134b a vehicle with twenty cameras still draws at most eight rows",
                  SettingsVideoPage.VisibleCams(many) == SettingsVideoPage.MaxRows,
                  "got " + SettingsVideoPage.VisibleCams(many));
            float px, py; Centre(SettingsVideoPage.MaxRows, out px, out py);
            Check("S134b ...and the ninth is not hittable, because it is not drawn",
                  SettingsVideoPage.HitTest(px, py, W, H, many) < 0, "");
        }

        // ---- the gap between rows is inert (the rows are 118 tall on a 150 pitch) -----------------
        {
            float rx, ry, rw, rh;
            SettingsVideoPage.RowRect(0, out rx, out ry, out rw, out rh);
            float px = (rx + rw * 0.5f) * W / RefW;
            float py = (ry + rh + 12f) * H / RefH;       // 12 px into the 32 px gap
            Check("S134b the gap between two rows is inert",
                  SettingsVideoPage.HitTest(px, py, W, H, s) < 0, "");
        }
        // and left of the column
        {
            float rx, ry, rw, rh;
            SettingsVideoPage.RowRect(0, out rx, out ry, out rw, out rh);
            float px = (rx - 30f) * W / RefW, py = (ry + rh * 0.5f) * H / RefH;
            Check("S134b a touch left of the column is inert",
                  SettingsVideoPage.HitTest(px, py, W, H, s) < 0, "");
        }

        // ---- ⭐ THE DRAWN SELECTION AND THE HIT INDEX ARE THE SAME VALUE -------------------------
        // The page draws row `s.CameraView` highlighted. Hit-testing that row's own centre must give
        // back that same index — which is what makes "tap a row and the highlight moves there" true.
        {
            float px, py;
            Centre(s.CameraView, out px, out py);
            Check("S134b hit-testing the SELECTED row returns the selection's own index",
                  SettingsVideoPage.HitTest(px, py, W, H, s) == s.CameraView,
                  "selection " + s.CameraView + ", hit " + SettingsVideoPage.HitTest(px, py, W, H, s));
        }
        // ⚠ and the highlight really is drawn on that row, read off the render rather than assumed
        Check("S134b the page draws exactly one row highlighted, and it is the selected one",
              HighlightedRow(s) == s.CameraView,
              "drew " + HighlightedRow(s) + ", selection is " + s.CameraView);

        // ---- ⛔ THE DRAWN RECTS, READ OFF THE RENDER, AGAINST RowRect ------------------------
        // ⚠ Every check above locates a row with `RowRect` and then hit-tests it - and `RowRect` is
        // also what the DRAW uses, so a mutation of the pitch moves all three together and passes.
        // (Mutation W2 did exactly that.) The only thing that can catch it is comparing `RowRect` to
        // what the page actually EMITTED.
        {
            DisplayList dl = new DisplayList(512);
            SettingsVideoPage.Build(dl, W, H, s);
            int matched = 0;
            for (int i = 0; i < n; i++)
            {
                float rx, ry, rw, rh;
                SettingsVideoPage.RowRect(i, out rx, out ry, out rw, out rh);
                float px = rx * W / RefW, py = ry * H / RefH;
                float pw = rw * W / RefW, ph = rh * H / RefH;
                for (int c = 0; c < dl.Count; c++)
                {
                    DrawCmd d = dl.At(c);
                    if (d.Kind != DrawKind.Rect) continue;
                    if (Math.Abs(d.A - px) < 0.6f && Math.Abs(d.B - py) < 0.6f
                        && Math.Abs(d.C - pw) < 0.6f && Math.Abs(d.D - ph) < 0.6f)
                    { matched++; break; }
                }
            }
            Check("S134b every row RowRect claims is a rectangle the page really drew",
                  matched == n, matched + " of " + n + " matched a drawn rect");
        }

        // ---- ⛔ AND THE ROUND TRIP AT A SECOND ASPECT ------------------------------------------
        // ⚠ At the shipped panel a wrong PROJECTION still lands inside these rows: they are 560
        // design px wide and the letterbox offset is only ~157 px of error, so mutation W5 - using the
        // letterboxed inverse on a stretched page - survived every check above. The error grows with
        // the letterbox, so a wider panel is what makes it visible. Same lesson as [[S134a]], which is
        // the sibling line: one aspect can never catch a projection bug.
        {
            const int W2 = 4416, H2 = 1406;
            for (int i = 0; i < n; i++)
            {
                float rx, ry, rw, rh;
                SettingsVideoPage.RowRect(i, out rx, out ry, out rw, out rh);
                float px = (rx + rw * 0.5f) * W2 / RefW, py = (ry + rh * 0.5f) * H2 / RefH;
                Check("S134b @" + W2 + " row " + i + "'s own centre still hits row " + i,
                      SettingsVideoPage.HitTest(px, py, W2, H2, s) == i,
                      "got " + SettingsVideoPage.HitTest(px, py, W2, H2, s));
            }
        }

        // ---- ⛔ THE ROW GEOMETRY ITSELF, PINNED AS THE MEASUREMENT IT IS ----------------------
        // ⚠ Everything above - the draw, the hit test, and this suite's own probes - reads `RowRect`,
        // so a change to the pitch moves all of them together and every check still passes. Mutation W2
        // did exactly that, twice: it survived the round trip AND the read-off-the-render check.
        // ⭐ The only thing that can catch it is stating the numbers independently, and they ARE worth
        // stating: 150 / 370 / 560 / 118 are the page's own layout, unchanged by [[S134b]], which only
        // gave them a name. Same argument `LegibilityFloorTest` makes for `Typography.Min == 16` - a
        // measured number, pinned so that "fixing" it by retyping is a failing build rather than a
        // quiet re-layout.
        Check("S134b the row column starts at design x 150 and is 560 wide",
              SettingsVideoPage.RowX == 150f && SettingsVideoPage.RowW == 560f,
              SettingsVideoPage.RowX + " / " + SettingsVideoPage.RowW);
        Check("S134b the first row is at y 370 and each is 118 tall",
              SettingsVideoPage.RowTop == 370f && SettingsVideoPage.RowH == 118f,
              SettingsVideoPage.RowTop + " / " + SettingsVideoPage.RowH);
        Check("S134b the pitch is 150, leaving a 32 px gap between rows",
              SettingsVideoPage.RowPitch == 150f
              && SettingsVideoPage.RowPitch - SettingsVideoPage.RowH == 32f,
              "pitch " + SettingsVideoPage.RowPitch + ", gap "
              + (SettingsVideoPage.RowPitch - SettingsVideoPage.RowH));

        // ---- the control id carries the row, and a miss carries nothing --------------------------
        Check("S134b a row press names itself in the CVR channel",
              CrewControlIds.VideoCam(2) == "video.cam2", CrewControlIds.VideoCam(2));
        Check("S134b a miss names nothing", CrewControlIds.VideoCam(-1) == null, "");

        // ---- ⚠ CameraHeldByDocking must not change which rows exist -----------------------------
        // QC's must-not-break: docking's precedence is over the FEED, not over the list.
        {
            PageState held = s; held.CameraHeldByDocking = true;
            Check("S134b docking holding the camera does not remove the rows",
                  SettingsVideoPage.VisibleCams(held) == SettingsVideoPage.VisibleCams(s), "");
            float px, py; Centre(2, out px, out py);
            Check("S134b ...and they are still selectable",
                  SettingsVideoPage.HitTest(px, py, W, H, held) == 2, "");
        }

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (the rows can be pressed; the writer was never the missing part)");
        return failures;
    }

    static void Centre(int row, out float px, out float py)
    {
        float rx, ry, rw, rh;
        SettingsVideoPage.RowRect(row, out rx, out ry, out rw, out rh);
        px = (rx + rw * 0.5f) * W / RefW;
        py = (ry + rh * 0.5f) * H / RefH;
    }

    /// <summary>Which row the page actually DREW as selected — found by the highlight's own fill,
    /// read off the display list rather than recomputed.</summary>
    static int HighlightedRow(PageState s)
    {
        DisplayList dl = new DisplayList(512);
        SettingsVideoPage.Build(dl, W, H, s);
        int found = -1, count = 0;
        int n = SettingsVideoPage.VisibleCams(s);
        for (int i = 0; i < n; i++)
        {
            float rx, ry, rw, rh;
            SettingsVideoPage.RowRect(i, out rx, out ry, out rw, out rh);
            float px = rx * W / RefW, py = ry * H / RefH;
            for (int c = 0; c < dl.Count; c++)
            {
                DrawCmd d = dl.At(c);
                if (d.Kind != DrawKind.Rect) continue;
                if (Math.Abs(d.A - px) > 1f || Math.Abs(d.B - py) > 1f) continue;
                if (Math.Abs(d.Colour.R - DragonPalette.Panel.R) < 1e-4f
                    && Math.Abs(d.Colour.G - DragonPalette.Panel.G) < 1e-4f
                    && Math.Abs(d.Colour.B - DragonPalette.Panel.B) < 1e-4f)
                { found = i; count++; }
            }
        }
        return (count == 1) ? found : -2;
    }
}
