// Tests for pure/CoverActs.cs — [[S128]]: what the Cover's four action rows do, and what they cannot do.
//
// ⛔ THE DONE-CRITERION THIS FILE EXISTS FOR: "a headless test pins that none of them reaches
// FlightCommands". That is answered two ways here, deliberately:
//
//   1. BY CONSTRUCTION — `CoverAct` has no field that can name a command, an actuator or a seam. The
//      checks below enumerate every `CoverButton` and assert the resolved kind is one of exactly three
//      harmless things. A new kind that actuates would fail this without anyone remembering to look.
//   2. BY EXHAUSTION — every member of the enum is resolved, not a sample, so a button added later is
//      forced through the same statement.
//
// ⚠ The GLUE's own dispatch (`ScreenPainter.ApplyCoverAct`) needs Unity and is not testable here. That
// is exactly why the DECISION was put in pure code: what a press means is checkable headlessly, and the
// glue that applies it is four lines with no judgement in them.
using System;
using DragonScreen;

public static class CoverActsTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen Cover action rows (S128)");
        checks = 0; failures = 0;

        // ---- THE FOUR, EACH BY ITS OWN LABEL ----------------------------------------------------
        CoverAct review = CoverActs.Of(CoverPage.CoverButton.ActReview);
        Check("S128 REVIEW REFERENCE CONTENT selects a phase",
              review.Kind == CoverActKind.SelectPhase, "got " + review.Kind);
        Check("S128 ...and it is the phase whose body IS the reference content",
              review.Phase == CoverPage.ReferencePhase,
              "got " + review.Phase + ", ReferencePhase is " + CoverPage.ReferencePhase);

        CoverAct brief = CoverActs.Of(CoverPage.CoverButton.ActDeorbitBrief);
        Check("S128 DEORBIT BURN BRIEF navigates", brief.Kind == CoverActKind.GoPage, "got " + brief.Kind);
        Check("S128 ...to the Deorbit Burn Prep page, which exists",
              brief.Page == UiPage.DeorbitBurnPrep, "got " + brief.Page);

        CoverAct ack = CoverActs.Of(CoverPage.CoverButton.ActAcknowledge);
        Check("S128 ACKNOWLEDGE sets the crew latch",
              ack.Kind == CoverActKind.Latch && ack.Latch == CoverLatch.CrewAcknowledge,
              "got " + ack.Kind + "/" + ack.Latch);

        CoverAct spx = CoverActs.Of(CoverPage.CoverButton.ActOnSpaceX);
        Check("S128 'On SpaceX, on, begin procedure 4.700' sets the GROUND latch",
              spx.Kind == CoverActKind.Latch && spx.Latch == CoverLatch.GroundAuthorised,
              "got " + spx.Kind + "/" + spx.Latch);
        Check("S128 the two latches are DIFFERENT — a crew ack is not a ground go",
              ack.Latch != spx.Latch, "both " + ack.Latch);

        // ---- ⛔ AND NOTHING ELSE ON THE PAGE IS AN ACTION -----------------------------------------
        // Exhaustive: every CoverButton, not a sample. A button added later lands here on its own.
        Array all = Enum.GetValues(typeof(CoverPage.CoverButton));
        int actions = 0, total = 0;
        foreach (object o in all)
        {
            CoverPage.CoverButton b = (CoverPage.CoverButton)o;
            total++;
            CoverAct a = CoverActs.Of(b);

            // (1) the kind is one of exactly three harmless things, or None
            bool harmless = a.Kind == CoverActKind.None
                         || a.Kind == CoverActKind.SelectPhase
                         || a.Kind == CoverActKind.GoPage
                         || a.Kind == CoverActKind.Latch;
            Check("S128 " + b + " resolves to a view change, a latch, or nothing", harmless,
                  "got " + a.Kind);

            if (a.Kind != CoverActKind.None) actions++;
            Check("S128 " + b + " — IsAction agrees with Of",
                  CoverActs.IsAction(b) == (a.Kind != CoverActKind.None),
                  "IsAction " + CoverActs.IsAction(b) + " vs kind " + a.Kind);

            // (2) a SelectPhase target must be a real rail slot
            if (a.Kind == CoverActKind.SelectPhase)
                Check("S128 " + b + " selects a phase that exists",
                      a.Phase >= 0 && a.Phase < CoverPage.PhaseCount,
                      "phase " + a.Phase + " of " + CoverPage.PhaseCount);
        }
        Check("S128 EXACTLY FOUR of the Cover's buttons are actions", actions == 4,
              actions + " of " + total + " are actions");
        Check("S128 ...out of a page that has many more buttons than that", total > 10,
              "only " + total + " buttons");

        // ---- ⭐ THE §14.4(a) PIN, STATED AS A PROPERTY OF THE TYPE --------------------------------
        // `CoverAct` carries a kind, a phase index, a page and a latch. There is no member that could
        // hold a command id or an actuator, so no value of this type can express a flight command —
        // which is stronger than any test that goes looking for one call site.
        System.Reflection.FieldInfo[] fields = typeof(CoverAct).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Check("S128 CoverAct carries exactly four fields", fields.Length == 4,
              "got " + fields.Length);
        bool onlyHarmless = true; string names = "";
        for (int i = 0; i < fields.Length; i++)
        {
            names += fields[i].Name + " ";
            Type t = fields[i].FieldType;
            if (t != typeof(CoverActKind) && t != typeof(int) && t != typeof(UiPage) && t != typeof(CoverLatch))
                onlyHarmless = false;
        }
        Check("S128 ...and none of them can name a command or an actuator", onlyHarmless, names);

        // ---- the latch enum says what it means, and defaults to nothing ---------------------------
        Check("S128 CoverAct.None is genuinely inert",
              CoverAct.None.Kind == CoverActKind.None && CoverAct.None.Latch == CoverLatch.None,
              "got " + CoverAct.None.Kind + "/" + CoverAct.None.Latch);
        Check("S128 a default CoverAct is inert too — a zeroed struct must not mean 'act'",
              default(CoverAct).Kind == CoverActKind.None
              && default(CoverAct).Latch == CoverLatch.None,
              "got " + default(CoverAct).Kind + "/" + default(CoverAct).Latch);

        TheTwoPillsAreOnePair();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (" + actions + " action rows, none of which can command the vehicle)");
        return failures;
    }

    // ============================================================================================
    // S176 (OWNER, 2026-09-06) - THE TWO PILLS ARE ONE PAIR
    //
    // Owner, verbatim: "remove the "-" from the pills and make both "NEXT VIEW" AND "SETTINGS" all
    // caps, both the same font size as "NEXT VIEW" and centred withing the pills".
    //
    // Three claims, and each is only checkable against the RENDER: same size, both centred, no dash.
    // They were previously a primitive (NEXT VIEW) and a 140x37 raster (SETTINGS) maintained apart,
    // which is exactly how "the same size" stops being true without anyone noticing.
    // ============================================================================================
    static void TheTwoPillsAreOnePair()
    {
        int[,] sizes = { { 1280, 703 }, { 2560, 1406 } };
        for (int i = 0; i < sizes.GetLength(0); i++)
        {
            int w = sizes[i, 0], h = sizes[i, 1];
            string at = " @" + w + "x" + h;
            float sc = h / 2112f, extra = w - 3427f * sc; if (extra < 0f) extra = 0f;

            DisplayList dl = new DisplayList(CoverPage.Commands + 60);
            PageState st = new PageState(); st.Valid = true;
            CoverPage.Build(dl, w, h, st, MapProjection.Default(), 0,
                            CoverPage.CoverCam.Earth, new TurntableState());

            // The two pill rectangles, from the page's own geometry - NEXT VIEW's is public, and
            // SETTINGS' is rectangle_174's Box row, which is where the page draws it from.
            float nx, ny, nw, nh;
            CoverPage.NextViewRect(w, h, out nx, out ny, out nw, out nh);
            float sx = 2994f * sc + extra, sy = 1810f * sc, sw = 401f * sc, sh = 111f * sc;

            float[] gotSize = { -1f, -1f };
            float[] gotCx = { 0f, 0f };
            float[] gotY = { 0f, 0f };
            string[] want = { "NEXT VIEW", "SETTINGS" };
            for (int c = 0; c < dl.Count; c++)
            {
                DrawCmd d = dl.At(c);
                if (d.Kind != DrawKind.Text) continue;
                for (int n = 0; n < 2; n++)
                    if (d.Str == want[n]) { gotSize[n] = d.C; gotCx[n] = d.A; gotY[n] = d.B; }
            }

            Check("NEXT VIEW is drawn as TEXT" + at, gotSize[0] > 0f, "not found");
            Check("SETTINGS is drawn as TEXT, not the baked raster" + at, gotSize[1] > 0f, "not found");
            if (gotSize[0] <= 0f || gotSize[1] <= 0f) continue;

            // 1. THE SAME SIZE - the owner's own words, and the reason one helper draws both.
            Check("both pills' labels are the same size" + at,
                  Math.Abs(gotSize[0] - gotSize[1]) < 0.001f,
                  "NEXT VIEW " + gotSize[0] + ", SETTINGS " + gotSize[1]);

            // ...and that size clears the glanceable floor, because these are navigation controls.
            Check("...and it clears the glanceable floor" + at,
                  gotSize[0] >= Typography.MinFor(w) - 0.001f,
                  "size " + gotSize[0] + ", floor " + Typography.MinFor(w));

            // ⛔ AND IT IS NEXT VIEW'S OWN 50 DESIGN PX, WHICH IS WHAT THE OWNER NAMED - "both the
            // same font size as NEXT VIEW". The floor check above cannot see a drop from 50 to 48.07,
            // because `Typography.LiveDesign` lifts anything smaller back up to the floor and the two
            // labels stay equal to each other on the way down. Proven by mutation: lowering the
            // constant to 30 failed nothing until this line existed.
            Check("...and it is NEXT VIEW's own 50 design px, the size the owner named" + at,
                  Math.Abs(gotSize[0] / sc - 50f) < 0.01f,
                  "design size " + (gotSize[0] / sc));

            // 2. CENTRED, BOTH WAYS. The text is anchored Centre, so its x IS the centre; the y is the
            // TOP of the line box and the cap centre sits CapCentreOfTop of the size below it.
            float[] cx = { nx + nw * 0.5f, sx + sw * 0.5f };
            float[] cy = { ny + nh * 0.5f, sy + sh * 0.5f };
            for (int n = 0; n < 2; n++)
            {
                Check(want[n] + " is centred horizontally in its pill" + at,
                      Math.Abs(gotCx[n] - cx[n]) < 0.01f,
                      "drawn at " + gotCx[n] + ", pill centre " + cx[n]);
                Check(want[n] + " is centred vertically in its pill" + at,
                      Math.Abs(gotY[n] + gotSize[n] * Typography.CapCentreOfTop - cy[n]) < 0.01f,
                      "cap centre " + (gotY[n] + gotSize[n] * Typography.CapCentreOfTop)
                      + ", pill centre " + cy[n]);
            }

            // 3. NO DASH IN EITHER PILL. SETTINGS' was the `ic_sharp_subtract` asset and NEXT VIEW's
            // was a Strokes.Px(6) rect; the baked word `settings` went with them.
            bool dash = false, bakedWord = false;
            for (int c = 0; c < dl.Count; c++)
            {
                DrawCmd d = dl.At(c);
                if (d.AssetKey == "ic_sharp_subtract") dash = true;
                if (d.AssetKey == "settings") bakedWord = true;
            }
            Check("the SETTINGS pill's dash is gone" + at, !dash, "ic_sharp_subtract still drawn");
            Check("...and so is its baked word" + at, !bakedWord, "the `settings` raster still drawn");
            // NEXT VIEW's dash was the only thin white Rect INSIDE its pill; nothing may paint there
            // now except the pill's own border, which is at the box's edge.
            int inside = 0;
            for (int c = 0; c < dl.Count; c++)
            {
                DrawCmd d = dl.At(c);
                if (d.Kind != DrawKind.Rect) continue;
                if (d.Colour.R < 0.99f || d.Colour.G < 0.99f || d.Colour.B < 0.99f) continue;
                if (d.A > nx + 8f && d.A + d.C < nx + nw - 8f
                    && d.B > ny + 8f && d.B + d.D < ny + nh - 8f) inside++;
            }
            Check("the NEXT VIEW pill's interior is empty of primitives" + at, inside == 0,
                  inside + " white rect(s) inside the pill");

            // ============================================================================
            // 4. S176 EDIT 2 - THEY ARE STACKED NOW, SO THEY ARE ADJACENT, SO THEY CAN COLLIDE.
            //
            // Owner, 2026-09-06: "move next view button to above the setting button". The two hit
            // rects used to be at opposite ends of the camera slot and could not be confused; they are
            // now PillGap = 32 design px apart in y, and CoverPage.HitTest tests NEXT VIEW FIRST. A
            // pill that grew, or a gap that shrank, would let the upper one eat the lower one's
            // touches silently - the MarginAffordance failure, which is why this is asserted rather
            // than assumed.
            // ============================================================================
            Check("the two pills share an x and a width" + at,
                  Math.Abs(nx - sx) < 0.01f && Math.Abs(nw - sw) < 0.01f,
                  "NEXT VIEW x " + nx + " w " + nw + ", SETTINGS x " + sx + " w " + sw);
            Check("NEXT VIEW sits ABOVE SETTINGS, clear of it" + at,
                  ny + nh < sy - 0.01f, "NEXT VIEW ends " + (ny + nh) + ", SETTINGS starts " + sy);
            Check("...by the page's own 32 design px, not a chosen number" + at,
                  Math.Abs((sy - (ny + nh)) / sc - 32f) < 0.01f,
                  "gap " + ((sy - (ny + nh)) / sc) + " design px");

            Check("a touch in the NEXT VIEW pill hits NEXT VIEW" + at,
                  CoverPage.HitTest(nx + nw * 0.5f, ny + nh * 0.5f, w, h, 0)
                      == CoverPage.CoverButton.NextView,
                  "got " + CoverPage.HitTest(nx + nw * 0.5f, ny + nh * 0.5f, w, h, 0));
            Check("a touch in the SETTINGS pill still hits SETTINGS, not the pill above it" + at,
                  CoverPage.HitTest(sx + sw * 0.5f, sy + sh * 0.5f, w, h, 0)
                      == CoverPage.CoverButton.Settings,
                  "got " + CoverPage.HitTest(sx + sw * 0.5f, sy + sh * 0.5f, w, h, 0));
            Check("a touch in the gap between them hits neither" + at,
                  CoverPage.HitTest(sx + sw * 0.5f, (ny + nh + sy) * 0.5f, w, h, 0)
                      != CoverPage.CoverButton.NextView
                  && CoverPage.HitTest(sx + sw * 0.5f, (ny + nh + sy) * 0.5f, w, h, 0)
                      != CoverPage.CoverButton.Settings,
                  "got " + CoverPage.HitTest(sx + sw * 0.5f, (ny + nh + sy) * 0.5f, w, h, 0));

            // ⭐ AND THE CAMERA CAPTION MOVED WITH IT, rather than being left under the new pill.
            float capTop = -1f;
            for (int c = 0; c < dl.Count; c++)
            {
                DrawCmd d = dl.At(c);
                if (d.Kind == DrawKind.Text && d.Str == "CAMERA") capTop = d.B;
            }
            Check("the CAMERA caption is drawn" + at, capTop >= 0f, "not found");
            Check("...and it is clear ABOVE the NEXT VIEW pill" + at, capTop >= 0f && capTop < ny,
                  "caption top " + capTop + ", pill top " + ny);

            // ⭐ AND THE PILL IS STILL THE EXPORT'S OWN ART. Removing the interior must not remove
            // rectangle_174 - §14.2a clause (1) keeps the element that IS in the export.
            bool pillArt = false;
            for (int c = 0; c < dl.Count; c++)
                if (dl.At(c).AssetKey == "rectangle_174") pillArt = true;
            Check("the SETTINGS pill itself is still drawn from the export" + at, pillArt, "");
        }
    }

}
