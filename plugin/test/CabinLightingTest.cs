// Tests for pure/CabinLightingPanel.cs — [[S134d]] / QC `F-03`.
//
// ⛔ WHAT WAS WRONG: Frame 66's baked LIGHTING panel drew FIFTEEN per-zone lighting rows and an
// instruction to tap them, on a page with no hit test, for a pod that carries exactly ONE bindable
// light module. Plus four art faults in the PNG itself — three identical column headings, `- DISPLAY 3`
// twice with no DISPLAY 4, a short fourth column, and a caption ending mid-clause.
//
// ⭐ The rebuild takes all of that with it, so the checks below are about what REPLACED it: that it
// says what the vehicle has, that it never claims to be tappable, and that a dead feed does not read
// as "lights off".
using System;
using DragonScreen;

public static class CabinLightingTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    const int W = 2560, H = 1406;

    public static int Run()
    {
        Console.WriteLine("DragonScreen cabin LIGHTING panel (S134d / QC F-03)");
        checks = 0; failures = 0;

        // ---- the readout says what the vehicle has ----------------------------------------------
        PageState on = Live(); on.LightsOn = true; on.LightCount = 1;
        PageState off = Live(); off.LightsOn = false; off.LightCount = 1;
        PageState many = Live(); many.LightsOn = true; many.LightCount = 4;
        PageState dead = new PageState();

        Check("S134d lights on reads ON", CabinLightingPanel.StateText(on) == "ON",
              CabinLightingPanel.StateText(on));
        Check("S134d lights off reads OFF", CabinLightingPanel.StateText(off) == "OFF",
              CabinLightingPanel.StateText(off));
        // ⛔ "OFF" for "cannot read" is a claim about the vehicle — rule E4 is exactly about not making it
        Check("S134d a dead feed does NOT read OFF", CabinLightingPanel.StateText(dead) != "OFF",
              CabinLightingPanel.StateText(dead));
        Check("S134d ...it dashes", CabinLightingPanel.StateText(dead) == Dashes.None,
              CabinLightingPanel.StateText(dead));
        Check("S134d ...and its ink is dim, not the live accent",
              !SameRgba(CabinLightingPanel.StateInk(dead), CabinLightingPanel.StateInk(on)), "");

        // ---- ⭐ THE COUNT IS THE VESSEL'S, AND IT MATCHES SettingsPage's WORDING -----------------
        Check("S134d one module reads SINGLE CABIN LIGHT GROUP",
              CabinLightingPanel.GroupsText(on) == "SINGLE CABIN LIGHT GROUP",
              CabinLightingPanel.GroupsText(on));
        Check("S134d four modules read 4 LIGHT GROUPS",
              CabinLightingPanel.GroupsText(many) == "4 LIGHT GROUPS",
              CabinLightingPanel.GroupsText(many));
        Check("S134d a dead feed dashes the count too",
              CabinLightingPanel.GroupsText(dead) == Dashes.None,
              CabinLightingPanel.GroupsText(dead));

        // ---- ⛔ S153f: THE COUNT IS A LIVE READOUT AND MUST BE DRAWN AT THE LIVE FLOOR --------------
        // It was on the STATIC floor, because the three lines around it are labels and it sits between
        // them. The classification is about the CONTENT: `GroupsText` reads `s.LightCount` and dashes
        // when the feed is invalid - the checks directly above are the proof of that - so the owner's
        // R-01 policy puts it with `StateText`, not with `CABIN LIGHTS`.
        //
        // ⚠ THIS CHECK EXISTS BECAUSE THE RATCHET CANNOT SEE IT. `LegibilityFloorTest`'s R-01 census
        // counts draws below each floor; moving a LIVE element DOWN to the static floor leaves its
        // below-Dense count unchanged, so the hard ratchet stays green and the soft one only PRINTS.
        // The census header says so in terms - *"what this deliberately CANNOT catch is a LIVE element
        // placed at the static floor. Telling those apart needs a per-element classification"*. This is
        // that classification, for this element, and a mutation putting it back is killed HERE or
        // nowhere: verified by running exactly that mutation.
        {
            const int W2 = 2560, H2 = 1406;
            float sc = H2 / 2112f;
            float live = Typography.MinDesignFor(W2, sc), dense = Typography.DenseDesignFor(W2, sc);
            DisplayList dl = new DisplayList(64);
            CabinLightingPanel.Draw(dl, W2, H2, on);
            float got = -1f;
            for (int i = 0; i < dl.Count; i++)
            {
                DrawCmd c = dl.At(i);
                if (c.Kind == DrawKind.Text && c.Str == CabinLightingPanel.GroupsText(on)) got = c.C / sc;
            }
            Check("S153f the light-group COUNT is drawn at the LIVE floor, not the static one",
                  got > 0f && Math.Abs(got - live) < 0.01f,
                  "drawn at " + got.ToString("F2") + " design px; live floor is " + live.ToString("F2")
                  + ", static floor " + dense.ToString("F2")
                  + " - it is a count OF THIS VEHICLE, so it belongs on the live one");
        }

        // ---- ⛔ AND NOTHING ON IT CLAIMS TO BE TAPPABLE ------------------------------------------
        // The baked panel's caption was "Tap to disable display / or" — an instruction to tap on a page
        // with no hit test, which QC calls the strongest form of the dead-control defect. It must not
        // come back in any form.
        string[] texts = TextsOf(on);
        bool saysTap = false, saysDisplay = false;
        for (int i = 0; i < texts.Length; i++)
        {
            string t = texts[i].ToUpperInvariant();
            if (t.IndexOf("TAP", StringComparison.Ordinal) >= 0) saysTap = true;
            if (t.IndexOf("DISPLAY ", StringComparison.Ordinal) >= 0) saysDisplay = true;
        }
        Check("S134d the panel never tells the crew to tap anything", !saysTap, Join(texts));
        Check("S134d ...and no per-zone DISPLAY rows came back", !saysDisplay, Join(texts));
        Check("S134d the panel draws four lines, not fifteen rows", texts.Length == 4,
              texts.Length + ": " + Join(texts));

        // ⭐ and it says WHY there are no zones, because the baked version promised fifteen
        bool explains = false;
        for (int i = 0; i < texts.Length; i++)
            if (texts[i].IndexOf("PER-ZONE", StringComparison.Ordinal) >= 0) explains = true;
        Check("S134d it says why there are no per-zone rows", explains, Join(texts));

        // ---- ⚠ EVERYTHING IT DRAWS IS INSIDE THE BOX IT PAINTED OUT ------------------------------
        // The patch is exactly the baked panel's own box (a fill scan of frame66.png). Drawing outside
        // it would put text on the cabin illustration, which QC's must-not-break protects.
        {
            DisplayList dl = new DisplayList(64);
            CabinLightingPanel.Draw(dl, W, H, on);
            float sc = H / 2112f, ox = (W - 3427f * sc) * 0.5f;
            float y0 = CabinLightingPanel.Y0 * sc, y1 = CabinLightingPanel.Y1 * sc;
            float x0 = ox + CabinLightingPanel.X0 * sc, x1 = ox + CabinLightingPanel.X1 * sc;
            int outside = 0;
            for (int i = 0; i < dl.Count; i++)
            {
                DrawCmd c = dl.At(i);
                if (c.Kind != DrawKind.Text) continue;
                if (c.B < y0 || c.B + c.C > y1 || c.A < x0 || c.A > x1) outside++;
            }
            Check("S134d every line is inside the box the patch covers", outside == 0,
                  outside + " line(s) outside");

            // and the patch itself is exactly the measured box
            bool patched = false;
            for (int i = 0; i < dl.Count; i++)
            {
                DrawCmd c = dl.At(i);
                if (c.Kind != DrawKind.Rect) continue;
                if (Math.Abs(c.A - x0) < 0.6f && Math.Abs(c.B - y0) < 0.6f
                    && Math.Abs(c.C - (x1 - x0)) < 0.6f && Math.Abs(c.D - (y1 - y0)) < 0.6f) patched = true;
            }
            Check("S134d the baked panel is painted out, exactly over its own box", patched, "");
        }

        // ---- ⛔ THE PATCH BOX IS THE MEASURED ONE, STATED INDEPENDENTLY -------------------------
        // ⚠ Every check above locates the box with `X0..Y1` and then asserts things about that box, so
        // widening it moves the goalposts with it - mutation Y4 grew the box 85 px past the baked
        // panel's bottom edge and every check still passed, while the patch would in fact have painted
        // over the cabin illustration QC's must-not-break protects.
        // ⭐ These four numbers came from a fill scan of `frame66.png`: the panel is uniformly
        // `DragonPalette.Background` from x 1008 to 2420 and y 1157 to 1815, with the first full-width
        // row at 1160 and the last at 1812. Pinned as the measurement they are.
        Check("S134d the patch box is the panel's own measured x extent",
              CabinLightingPanel.X0 == 1008f && CabinLightingPanel.X1 == 2420f,
              CabinLightingPanel.X0 + ".." + CabinLightingPanel.X1);
        Check("S134d ...and its measured y extent, which must not reach the illustration below",
              CabinLightingPanel.Y0 == 1157f && CabinLightingPanel.Y1 == 1815f,
              CabinLightingPanel.Y0 + ".." + CabinLightingPanel.Y1);
        Check("S134d the box sits below the baked LIGHTING title, which is kept",
              CabinLightingPanel.Y0 > 1140f, "top " + CabinLightingPanel.Y0);

        // ---- ⚠ AND THE TYPE CLEARS THE FLOOR, because this is a rebuild and has no excuse ---------
        {
            DisplayList dl = new DisplayList(64);
            CabinLightingPanel.Draw(dl, W, H, on);
            float sc = H / 2112f;
            float floor = Typography.DenseFor(W);          // static floor, in panel px
            int below = 0; float worst = 999f;
            for (int i = 0; i < dl.Count; i++)
            {
                DrawCmd c = dl.At(i);
                if (c.Kind != DrawKind.Text) continue;
                float panel = c.C;                          // already panel px
                if (panel < floor - 1e-3f) { below++; if (panel < worst) worst = panel; }
            }
            Check("S134d nothing on the rebuilt panel is below the static floor", below == 0,
                  below + " line(s), smallest " + worst + " vs floor " + floor);
        }

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (fifteen dead rows replaced by what the vehicle actually has)");
        return failures;
    }

    static PageState Live() { PageState s = new PageState(); s.Valid = true; return s; }

    static string[] TextsOf(PageState s)
    {
        DisplayList dl = new DisplayList(64);
        CabinLightingPanel.Draw(dl, W, H, s);
        int n = 0;
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Kind == DrawKind.Text) n++;
        string[] a = new string[n]; int k = 0;
        for (int i = 0; i < dl.Count; i++)
            if (dl.At(i).Kind == DrawKind.Text) a[k++] = dl.At(i).Str ?? "";
        return a;
    }

    static string Join(string[] a) { return string.Join(" | ", a); }

    static bool SameRgba(Rgba a, Rgba b)
    { return Math.Abs(a.R - b.R) < 1e-4f && Math.Abs(a.G - b.G) < 1e-4f && Math.Abs(a.B - b.B) < 1e-4f; }
}
