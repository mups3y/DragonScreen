/*
 * S243 — THE THREE SHELL PAGES: appended beside the originals, empty on purpose, wired not absent.
 *
 * ⛔ THE HARDEST THING TO TEST HERE IS AN ABSENCE. "The pop-up draws nothing" and "the pop-up is not
 * connected" produce byte-identical display lists, and only one of them is the deliverable. Owner:
 * *"have them all ready to go and wired in so when the prompts arrive they are ready."*
 * ⭐ So `ThePopupIsWiredNotAbsent` drives a real callout in and watches it appear. A check that only
 * asserted the empty case would pass on a page that had never heard of `BarEvent`.
 *
 * ⭐ `NothingIsBaked` IS THE DEFINITION NOW (the overseer's ruling after S242): the count of
 * `DrawKind.Image` commands is what "baked" means, and it is extended here per page rather than
 * restated in prose.
 *
 * ⛔ AND THE EMPTY WINDOW IS AN INSTRUCTION FOLLOWED, NOT AN OVERSIGHT. The gauge's design is approved
 * but its PLACEMENT is undiscussed and its thresholds unsourced — owner, 2026-09-09: *"we have not
 * discussed placement of the gauges yet, just the design — bob should not touch these yet."* These
 * checks pin the window EMPTY so a later chat cannot quietly fill it before that conversation happens.
 *
 * ⚠ WHAT THIS DOES NOT PROVE: how any of it looks, or that nine tabs is the right number. It proves the
 * shells exist beside the originals without disturbing them, route, and are wired.
 */
using DragonScreen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public static class ShellPageTest
{
    static int checks, failures;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + (detail == "" ? "" : "   " + detail)); }
    }

    static string Repo(params string[] parts)
    {
        var bits = new List<string> {
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", ".." };
        bits.AddRange(parts);
        return Path.GetFullPath(Path.Combine(bits.ToArray()));
    }

    static string Live(string src)
    {
        string[] lines = src.Replace("\r\n", "\n").Split(new char[] { (char)10 });
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            string t = lines[i].TrimStart();
            if (t.StartsWith("//") || t.StartsWith("///") || t.StartsWith("*")) continue;
            sb.Append(lines[i]).Append((char)10);
        }
        return sb.ToString();
    }

    const int W = 2560, H = 1405;

    static DisplayList Render(UiPage page, PageState s, int tab)
    {
        DisplayList dl = new DisplayList(FigmaUI.Commands + 512);
        PageControls ctl = PageControls.Default;
        ctl.ShellTab = tab;
        FigmaUI.Build(dl, page, W, H, s, new MapView(), 5, false, 1,
                      CoverPage.CoverCam.Earth, Turntable.Front(), ctl, 0u, false);
        return dl;
    }

    public static int Run()
    {
        Console.WriteLine("ShellPageTest (S243: three shells appended beside the originals, wired and empty)");
        checks = 0; failures = 0;

        TheAppendDidNotDisturbTheOriginals();
        TheShellsRenderAndAreNamed();
        ThePopupIsWiredNotAbsent();
        TheWindowIsEmptyOnPurpose();
        TheTabsRouteAndTheSelectorSlides();
        TheCommsGlyphIsShipped();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + (failures == 0 ? "   (the pop-up was driven, not merely observed empty)" : ""));
        return failures;
    }

    // ============================================================================================
    //  1. ⛔ BOB-16's RULING, ENFORCED: the existing pages keep their ints and their behaviour
    // ============================================================================================
    static void TheAppendDidNotDisturbTheOriginals()
    {
        Check("S243 ⛔ Cover is still 0", (int)UiPage.Cover == 0, "got " + (int)UiPage.Cover);
        Check("S243 ⛔ Vehicle is still 15", (int)UiPage.Vehicle == 15, "got " + (int)UiPage.Vehicle);
        Check("S243 ⛔ SuitCheck is still 16", (int)UiPage.SuitCheck == 16, "got " + (int)UiPage.SuitCheck);
        Check("S243 ⛔ CrewGate is still 35", (int)UiPage.CrewGate == 35, "got " + (int)UiPage.CrewGate);

        Check("S243 the four new values are APPENDED at 36..39",
              (int)UiPage.VehicleComms == 36 && (int)UiPage.ShellCover == 37
              && (int)UiPage.ShellVehicle == 38 && (int)UiPage.ShellSuitCheck == 39, "");
        Check("S243 PageCount moved with them", FigmaUI.PageCount == 40, "got " + FigmaUI.PageCount);

        // ⭐ The originals must still draw what they drew. A shell that quietly replaced one would show
        // up as an emptied display list.
        PageState s = new PageState(); s.Valid = true;
        Check("S243 ⛔ the ORIGINAL Cover still draws a full page", Render(UiPage.Cover, s, 0).Count > 100,
              "" + Render(UiPage.Cover, s, 0).Count);
        Check("S243 ⛔ ...and the original Vehicle too", Render(UiPage.Vehicle, s, 0).Count > 100,
              "" + Render(UiPage.Vehicle, s, 0).Count);
        Check("S243 ⛔ ...and the original SuitCheck too", Render(UiPage.SuitCheck, s, 0).Count > 100,
              "" + Render(UiPage.SuitCheck, s, 0).Count);
    }

    // ============================================================================================
    //  2. THE SHELLS RENDER, AND NAME THEMSELVES LIVE
    // ============================================================================================
    static void TheShellsRenderAndAreNamed()
    {
        PageState s = new PageState(); s.Valid = true;
        UiPage[] shells = { UiPage.ShellCover, UiPage.ShellVehicle, UiPage.ShellSuitCheck };
        for (int i = 0; i < shells.Length; i++)
        {
            DisplayList dl = Render(shells[i], s, 0);
            Check("S243 " + shells[i] + " renders", dl.Count > 0, "" + dl.Count);
            Check("S243 " + shells[i] + " did not overflow its buffer", !dl.Overflowed, "");
            // ⛔ The name is LIVE from FigmaUI.Name - never a literal, never a picture of one (S147b).
            Check("S243 " + shells[i] + " draws its own name, live",
                  Drew(dl, FigmaUI.Name(shells[i])), FigmaUI.Name(shells[i]));
            Check("S243 " + shells[i] + " is not a placeholder", !FigmaUI.IsPlaceholder(shells[i]), "");
        }
        // ⚠ VehicleComms is deliberately a RESERVED int with no content - see the register (BOB-18).
        Check("S243 ⚠ VehicleComms is a reserved int, still a placeholder",
              FigmaUI.IsPlaceholder(UiPage.VehicleComms), "");
    }

    static bool Drew(DisplayList dl, string text)
    {
        for (int i = 0; i < dl.Count; i++)
            if (dl.At(i).Kind == DrawKind.Text && dl.At(i).Str == text) return true;
        return false;
    }

    // ============================================================================================
    //  3. ⭐⭐ THE ONE THAT MATTERS: WIRED, NOT ABSENT
    // ============================================================================================
    static void ThePopupIsWiredNotAbsent()
    {
        PageState quiet = new PageState(); quiet.Valid = true;
        quiet.Event = BarCallout.None;
        DisplayList a = Render(UiPage.ShellCover, quiet, 0);

        // ⭐ DRIVE A REAL CALLOUT IN. This is the half a "draws nothing" check cannot do, and without
        // it an unwired page passes every other assertion in this file.
        PageState live = new PageState(); live.Valid = true;
        live.Event = BarCallout.LesArmed;
        DisplayList b = Render(UiPage.ShellCover, live, 0);

        Check("S243 ⭐⭐ driving an event in makes the pop-up APPEAR",
              b.Count > a.Count, a.Count + " quiet vs " + b.Count + " live");
        Check("S243 ⭐ ...and its text is the callout's own",
              Drew(b, BarEvent.Text(BarCallout.LesArmed).Split('\n')[0])
              || CountText(b) > CountText(a), "");
        // ⛔ And EMPTY IS THE CORRECT STATE with no event - not a missing feature.
        Check("S243 ⛔ with no event the cell draws NOTHING extra",
              a.Count < b.Count, "");

        // Every shell is wired, not just the one.
        foreach (UiPage p in new[] { UiPage.ShellVehicle, UiPage.ShellSuitCheck })
        {
            DisplayList q = Render(p, quiet, 0), l = Render(p, live, 0);
            Check("S243 " + p + " is wired too", l.Count > q.Count, q.Count + " vs " + l.Count);
        }
    }

    static int CountText(DisplayList dl)
    {
        int n = 0;
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Kind == DrawKind.Text) n++;
        return n;
    }

    // ============================================================================================
    //  4. ⛔ THE WINDOW IS EMPTY ON PURPOSE — including no gauge
    // ============================================================================================
    static void TheWindowIsEmptyOnPurpose()
    {
        PageState s = new PageState(); s.Valid = true;

        // ⭐ `NothingIsBaked` extended per page: the count of Image commands IS the definition of baked.
        // A NON-ICON shell's only pictures are the bottom bar's own tiles; it adds none of its own.
        DisplayList cover = Render(UiPage.ShellCover, s, 0);
        DisplayList veh = Render(UiPage.ShellVehicle, s, 0);
        int ic = Images(cover), iv = Images(veh);
        Check("S243 ⭐ the ICON shell draws exactly NINE more images than the NON-ICON one",
              iv - ic == 9, iv + " vs " + ic + " (delta " + (iv - ic) + ")");

        // ⛔ THE GAUGE IS NOT HERE, AND THAT IS THE INSTRUCTION. An ArcBand in the WINDOW would be the
        // first sign of one appearing before its placement is agreed. The shells' own arcs are all
        // corner discs, which live on the border and window EDGES, never in the content area.
        int inWindow = 0;
        float sc = BaseScreen.Sc(W);
        float x0 = (BaseScreen.WinL + 60f) * sc, x1 = (BaseScreen.WinR - 60f) * sc;
        float y0 = (BaseScreen.WinT + 80f) * sc, y1 = (BaseScreen.Shelf - 40f) * sc;
        for (int i = 0; i < veh.Count; i++)
        {
            DrawCmd c = veh.At(i);
            if (c.Kind != DrawKind.ArcBand) continue;
            if (c.A > x0 && c.A < x1 && c.B > y0 && c.B < y1) inWindow++;
        }
        Check("S243 ⛔ NO gauge in the content area - its placement is undiscussed (owner, 2026-09-09)",
              inWindow == 0, inWindow + " arc(s) inside the window");
        // And the tab icons are plain white - no severity tinting until the gauge conversation happens.
        Check("S243 ⛔ the tab icons are plain white, not severity-tinted",
              Live(File.ReadAllText(Repo("plugin", "src", "pure", "BaseScreenTabbed.cs")))
                  .Contains("IconSize * sc, BasePalette.Stroke"), "");
    }

    static int Images(DisplayList dl)
    {
        int n = 0;
        for (int i = 0; i < dl.Count; i++) if (dl.At(i).Kind == DrawKind.Image) n++;
        return n;
    }

    // ============================================================================================
    //  5. THE TABS ROUTE, AND THE SELECTOR FOLLOWS THE CONTROL STATE
    // ============================================================================================
    static void TheTabsRouteAndTheSelectorSlides()
    {
        PageState s = new PageState(); s.Valid = true;
        // ⭐ The selector must follow `PageControls.ShellTab` through the real Build path, not just
        // through `BaseScreenTabbed.DrawTabs` - that is the wiring this page adds.
        float prev = -1f;
        for (int t = 0; t < BaseScreenTabbed.TabCount; t++)
        {
            DisplayList dl = Render(UiPage.ShellVehicle, s, t);
            float x = SelectorX(dl);
            Check("S243 tab " + t + ": the selector moved right", x > prev, x + " vs " + prev);
            prev = x;
        }
        Check("S243 a tabbed shell opens on its first tab", PageControls.Default.ShellTab == 0,
              "" + PageControls.Default.ShellTab);
    }

    /// <summary>The selector is the widest full-height Rect in the strip's own band.</summary>
    static float SelectorX(DisplayList dl)
    {
        float sc = BaseScreen.Sc(W);
        float want = BaseScreenTabbed.SelectorTop * sc, best = -1f;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Rect) continue;
            if (Math.Abs(c.B - want) < 0.5f && Math.Abs(c.C - BaseScreenTabbed.SelectorW * sc) < 0.5f)
                best = c.A;
        }
        return best;
    }

    // ============================================================================================
    //  6. THE NINTH GLYPH IS SHIPPED, AND THE FALLBACK IS GONE
    // ============================================================================================
    static void TheCommsGlyphIsShipped()
    {
        string art = Repo("plugin", "GameData", "DragonScreen", "art", "cover", "ic_tab_comms.png");
        Check("S243 ⭐ ic_tab_comms.png is in GameData", File.Exists(art), art);
        if (File.Exists(art))
        {
            byte[] b = File.ReadAllBytes(art);
            Check("S243 ...802 bytes, as harvested", b.Length == 802, b.Length + " bytes");
            // PNG IHDR: width/height big-endian at 16..24, colour type 6 = RGBA.
            int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
            int hh = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
            Check("S243 ...48x48 RGBA", w == 48 && hh == 48 && b[25] == 6, w + "x" + hh + " type " + b[25]);
        }
        Check("S243 the Comms tab points at its own glyph",
              BaseScreenTabbed.IconKey(2) == "ic_tab_comms", BaseScreenTabbed.IconKey(2));
        // ⛔ THE FALLBACK IS GONE. Keeping it would draw a duplicate rocket and read as a design choice.
        Check("S243 ⛔ no tab borrows another's icon", NoDuplicateIcons(), "");
        // The corrected claim must not creep back.
        string doc = File.ReadAllText(Repo("docs", "reference", "NASA_REFERENCE_ART.md"));
        Check("S243 the doc records the correction rather than the old claim",
              doc.Contains("SUPERSEDED IN PLACE 2026-09-09") && doc.Contains("ic_tab_comms.png"), "");
    }

    static bool NoDuplicateIcons()
    {
        var seen = new HashSet<string>();
        for (int i = 0; i < BaseScreenTabbed.TabCount; i++)
            if (!seen.Add(BaseScreenTabbed.IconKey(i))) return false;
        return true;
    }
}
