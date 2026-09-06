// Tests for pure/SettingsTabStrip.cs — [[S134a]] / QC `F-04`.
//
// ⛔ THE DONE-CRITERION: "each drawn tab's centre hit-tests back to its own page on ALL THREE pages at
// TWO aspects." That shape matters. The defect was invisible at the shipped aspect — every tab still
// landed inside its own 130 px band — so a suite that only ran at 2560×1406 would have gone on passing
// through the whole of F-04's life. Every check below runs at two aspects, and one of them is wide
// enough to have broken the old mapping.
using System;
using DragonScreen;

public static class SettingsTabStripTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    // the shipped panel, and one wide enough that the OLD mapping put a tab outside its own band
    static readonly int[] W = { 2560, 4416 };
    static readonly int[] H = { 1406, 1406 };

    public static int Run()
    {
        Console.WriteLine("DragonScreen settings tab strip (S134a / QC F-04)");
        checks = 0; failures = 0;

        Check("S134a the strip has three tabs", SettingsTabStrip.Labels.Length == 3,
              "got " + SettingsTabStrip.Labels.Length);
        Check("S134a ...and each names a page", SettingsTabStrip.Targets.Length == 3, "");

        // ---- ⛔ THE THREE TARGETS, NAMED. This has to be an INDEPENDENT statement. ---------------
        // The round trip below asserts `HitTest(centre of tab t) routes to Targets[t]` - which is true
        // however Targets is ordered, because both sides read the same array. A mutation swapping two
        // entries sailed through it. Naming the pages here is the only thing that can catch a strip
        // whose tabs are internally consistent and go to the wrong screens.
        Check("S134a tab 0 is Audio", SettingsTabStrip.Targets[0] == UiPage.Audio,
              "got " + SettingsTabStrip.Targets[0]);
        Check("S134a tab 1 is Cabin", SettingsTabStrip.Targets[1] == UiPage.Cabin,
              "got " + SettingsTabStrip.Targets[1]);
        Check("S134a tab 2 is AudioVideo - the enum name is older than the page's VIDEO title",
              SettingsTabStrip.Targets[2] == UiPage.AudioVideo, "got " + SettingsTabStrip.Targets[2]);
        // ⚠ and each label must name its own destination, or the strip reads one thing and does another
        Check("S134a each label names the page it goes to",
              SettingsTabStrip.Labels[0] == "Audio" && SettingsTabStrip.Labels[1] == "Cabin"
              && SettingsTabStrip.Labels[2] == "Video",
              string.Join("/", SettingsTabStrip.Labels));
        // ⭐ and the LETTERBOXED page is the one whose target is Cabin - so IsLetterboxed and Targets
        // cannot drift onto different tabs
        Check("S134a the letterboxed page is the one tab 1 goes to",
              SettingsTabStrip.IsLetterboxed(SettingsTabStrip.Targets[1])
              && !SettingsTabStrip.IsLetterboxed(SettingsTabStrip.Targets[0])
              && !SettingsTabStrip.IsLetterboxed(SettingsTabStrip.Targets[2]), "");

        // ---- ⭐ EXACTLY ONE PAGE IS LETTERBOXED, and it is the one whose strip is baked ------------
        Check("S134a Cabin is letterboxed — its strip comes from frame66",
              SettingsTabStrip.IsLetterboxed(UiPage.Cabin), "");
        Check("S134a ...and the two code-drawn pages are not",
              !SettingsTabStrip.IsLetterboxed(UiPage.Audio)
              && !SettingsTabStrip.IsLetterboxed(UiPage.AudioVideo), "");

        for (int k = 0; k < W.Length; k++)
        {
            int w = W[k], h = H[k];

            // ---- THE ROUND TRIP, on each of the three pages ---------------------------------------
            for (int p = 0; p < SettingsTabStrip.Targets.Length; p++)
            {
                UiPage page = SettingsTabStrip.Targets[p];
                bool lb = SettingsTabStrip.IsLetterboxed(page);
                for (int t = 0; t < 3; t++)
                {
                    float px, py;
                    Project(SettingsTabStrip.Cx[t], SettingsTabStrip.LabelY, w, h, lb, out px, out py);
                    int hit = SettingsTabStrip.HitTest(px, py, w, h, lb);
                    Check("S134a @" + w + " on " + page + ", tab " + t + "'s own centre hits tab " + t,
                          hit == t, "got " + hit);

                    // and through the real nav path, which is what the crew actually touches
                    NavHit nh = FigmaUI.HitTest(page, px, py, w, h);
                    Check("S134a @" + w + " ...and FigmaUI routes it to " + SettingsTabStrip.Targets[t],
                          nh.Act == NavAct.Goto && nh.Target == SettingsTabStrip.Targets[t],
                          "got " + nh.Act + " " + nh.Target);
                }
            }

            // ---- ⛔ AND THE PROJECTIONS ARE GENUINELY DIFFERENT, or the check above is vacuous ------
            float sx, sy, lx, ly;
            Project(SettingsTabStrip.Cx[2], SettingsTabStrip.LabelY, w, h, false, out sx, out sy);
            Project(SettingsTabStrip.Cx[2], SettingsTabStrip.LabelY, w, h, true, out lx, out ly);
            Check("S134a @" + w + " the stretched and letterboxed projections put the same tab in "
                  + "different places", Math.Abs(sx - lx) > 1f,
                  "stretched " + sx + ", letterboxed " + lx);

            // ⭐ THE DEFECT ITSELF: a letterboxed tab tested with the STRETCHED mapping. At 2560 it
            // still lands in its band (which is why F-04 was invisible); at 4416 it does not.
            int wrong = SettingsTabStrip.HitTest(lx, ly, w, h, false);
            if (w >= 4416)
                Check("S134a @" + w + " the OLD single-mapping hit test would now miss — the defect made"
                      + " visible", wrong != 2, "it still returned tab " + wrong);
            else
                Console.WriteLine("  note  @" + w + " the old mapping still landed in band "
                                  + wrong + " — which is why F-04 had never bitten");
        }

        // ---- a touch outside the band is inert, at both aspects ----------------------------------
        for (int k = 0; k < W.Length; k++)
        {
            int w = W[k], h = H[k];
            float px, py;
            Project(SettingsTabStrip.Cx[0], SettingsTabStrip.Y0 - 60f, w, h, false, out px, out py);
            Check("S134a @" + w + " a touch above the strip is inert",
                  SettingsTabStrip.HitTest(px, py, w, h, false) < 0, "");
            Project((SettingsTabStrip.X1[0] + SettingsTabStrip.X0[1]) * 0.5f,
                    SettingsTabStrip.LabelY, w, h, false, out px, out py);
            Check("S134a @" + w + " the gap between two tabs is inert",
                  SettingsTabStrip.HitTest(px, py, w, h, false) < 0, "");
        }

        // ---- the bands are the ones FigmaUI always had — this line moved the SPACE, not the numbers --
        Check("S134a the hit bands are unchanged from before the fix",
              SettingsTabStrip.X0[0] == 1520f && SettingsTabStrip.X1[0] == 1650f
              && SettingsTabStrip.X0[1] == 1652f && SettingsTabStrip.X1[1] == 1780f
              && SettingsTabStrip.X0[2] == 1782f && SettingsTabStrip.X1[2] == 1910f
              && SettingsTabStrip.Y0 == 1890f && SettingsTabStrip.Y1 == 2000f, "");
        // ⚠ every label centre must be inside its own band, or the drawn strip and the bands
        // disagree in DESIGN space, which no projection could rescue
        for (int t = 0; t < 3; t++)
            Check("S134a tab " + t + "'s label centre is inside its own band",
                  SettingsTabStrip.Cx[t] > SettingsTabStrip.X0[t]
                  && SettingsTabStrip.Cx[t] < SettingsTabStrip.X1[t],
                  "centre " + SettingsTabStrip.Cx[t] + " band " + SettingsTabStrip.X0[t]
                  + ".." + SettingsTabStrip.X1[t]);

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (one geometry, two projections, three pages)");
        return failures;
    }

    /// <summary>Design (dx, dy) to panel pixels, in the projection the page actually draws in.</summary>
    static void Project(float dx, float dy, int w, int h, bool letterboxed, out float px, out float py)
    {
        if (letterboxed)
        {
            float sc = h / SettingsTabStrip.RefH;
            px = (w - SettingsTabStrip.RefW * sc) * 0.5f + dx * sc;
            py = dy * sc;
        }
        else
        {
            px = dx * w / SettingsTabStrip.RefW;
            py = dy * h / SettingsTabStrip.RefH;
        }
    }
}
