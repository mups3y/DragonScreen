// THE PREVIEW MUST RENDER AT THE SIZE THE MOD SHIPS. That is the whole of this suite.
//
// ---- THE DEFECT IT EXISTS FOR (S100, from QC finding H-01) ----
// `plugin/preview/PreviewMain.cs` rendered every Figma-era page at `int CW = W * 2, CH = H * 2;` -
// 2560x1406 - and justified it in a comment that said "the in-game RenderTexture should match -
// screenWidth 2560 in the cfg". `DragonScreen.cfg` said `screenWidth = 1280`, on all three screens
// (:60, :76, :87), and had said so the whole time; so did the glue's own default
// (`DragonScreenMonitor.cs:52`). CLAUDE.md names the preview as the instrument layout, palette and
// legibility are judged from, and C1.3 makes an inspected preview PNG a condition of marking a task
// DONE - so for as long as that line stood, the project's legibility gate was passing pages at FOUR
// TIMES the pixel count the crew actually gets, and every sign-off taken from a preview was taken at
// the wrong size.
//
// ---- WHY IT WAS NEVER CAUGHT ----
// Nothing in `plugin/test/` referenced `ScreenSpec` or `screenWidth`. `PreviewMain` states the
// governing principle for the FONT in as many words - "If this and PreviewMain.FontFamily ever
// disagree, the preview is lying about the real page" - and nobody wrote the same rule for
// RESOLUTION, which is exactly where it broke. A comment asserting a value in a file it does not
// read cannot fail. This suite can.
//
// ---- WHY IT READS SOURCE TEXT, WHICH IS NOT THE USUAL WAY TO WRITE A TEST ----
// The headless tests link `src/pure` + `test`; the preview is a SEPARATE assembly built from
// `src/pure` + `preview` (build.py: build_tests / build_preview). So `PreviewMain.Screens` is not
// callable from here, and the only alternatives were to move the screen table into the shipped DLL
// as code the game never executes, or to read the preview's source off disk. Reading it is honest
// about what it is and costs the shipped plugin nothing.
//
// It is FAIL-CLOSED, and that is the property that matters: every marker below must be FOUND. If
// somebody renames the derivation, the suite goes red saying it could not be located - it never
// passes by failing to look. There is a precedent for reading the shipped artefact rather than
// trusting a declaration: LayoutTest reads each PNG's IHDR and fails if `Images.Size` has drifted
// from the file that actually ships. This is that, for the screen.
using System;
using System.IO;

public static class ScreenSizeTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    // build/ -> plugin/
    static string PluginDir()
    {
        return Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), ".."));
    }

    /// <summary>
    /// Every `screenWidth = N` in the shipped cfg, comment tails stripped. Deliberately a SECOND,
    /// independent implementation of the parse `PreviewMain.CfgScreenWidth` does: if both were the
    /// same code, this suite would only be proving that code agrees with itself.
    /// </summary>
    static System.Collections.Generic.List<int> CfgWidths(string cfg)
    {
        var found = new System.Collections.Generic.List<int>();
        foreach (string raw in File.ReadAllLines(cfg))
        {
            string line = raw;
            int c = line.IndexOf("//", StringComparison.Ordinal);
            if (c >= 0) line = line.Substring(0, c);
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            if (line.Substring(0, eq).Trim() != "screenWidth") continue;
            int v;
            if (int.TryParse(line.Substring(eq + 1).Trim(), out v)) found.Add(v);
            else Check("screenWidth parses", false, raw.Trim());
        }
        return found;
    }

    public static int Run()
    {
        Console.WriteLine("ScreenSizeTest (S100 / QC H-01: the preview renders at the SHIPPED size)");
        checks = 0; failures = 0;

        string plugin   = PluginDir();
        string cfgPath  = Path.Combine(plugin, "GameData", "DragonScreen", "DragonScreen.cfg");
        string prevSrc  = Path.Combine(plugin, "preview", "PreviewMain.cs");
        string gluePath = Path.Combine(plugin, "src", "DragonScreenMonitor.cs");

        Check("the shipped cfg is present", File.Exists(cfgPath), cfgPath);
        Check("the preview source is present", File.Exists(prevSrc), prevSrc);
        if (!File.Exists(cfgPath) || !File.Exists(prevSrc))
        {
            Console.WriteLine("  " + checks + " checks, " + failures + " failed");
            return failures;                       // nothing below can be evaluated
        }

        // ---- 1. THE CFG AGREES WITH ITSELF ----
        // Three screens, one preview render size. If they ever diverge the preview cannot represent
        // all three and somebody has to decide what it represents; fail rather than pick silently.
        var widths = CfgWidths(cfgPath);
        Check("the cfg declares screenWidth", widths.Count > 0, cfgPath);
        bool agree = widths.Count > 0;
        for (int i = 1; i < widths.Count; i++) if (widths[i] != widths[0]) agree = false;
        Check("every screenWidth in the cfg is the same value", agree,
              widths.Count + " values: "
              + string.Join(", ", widths.ConvertAll(x => x.ToString()).ToArray()));
        if (!agree)
        {
            Console.WriteLine("  " + checks + " checks, " + failures + " failed");
            return failures;
        }
        int cfgW = widths[0];
        Check("screenWidth clears the glue's own floor of 16 (DragonScreenMonitor.cs:345)",
              cfgW >= 16, "screenWidth " + cfgW);

        // ---- 2. THE GLUE'S DEFAULT AGREES WITH THE CFG ----
        // A KSPField default is what a screen gets if its MODULE block omits the field, so a default
        // that disagrees with the shipped cfg is a fourth opinion about the render size waiting to be
        // adopted by the next screen someone adds.
        if (File.Exists(gluePath))
        {
            string glue = File.ReadAllText(gluePath);
            string want = "public int screenWidth = " + cfgW + ";";
            Check("the glue's screenWidth default matches the cfg (" + cfgW + ")",
                  glue.Contains(want),
                  "DragonScreenMonitor.cs does not contain '" + want + "'");
        }

        // ---- 3. THE PREVIEW DERIVES ITS SIZE - IT DOES NOT DECLARE ONE ----
        string src = File.ReadAllText(prevSrc);

        Check("the preview reads screenWidth out of the cfg",
              src.Contains("CfgScreenWidth"),
              "no CfgScreenWidth in PreviewMain.cs - if the derivation was renamed, update THIS "
              + "guard rather than deleting it; a guard that cannot find what it guards must fail");
        Check("the preview names the cfg file it derives from",
              src.Contains("DragonScreen.cfg"), "PreviewMain.cs never names DragonScreen.cfg");
        Check("the render sizes come from the derived table",
              src.Contains("Screens[0].W"),
              "PreviewMain.cs no longer takes W/H from Screens[] - the derivation is bypassed");

        // ---- 4. NO CODE LINE MAY SCALE THE SCREEN SIZE ----
        // This is the exact expression that caused H-01. Comments may quote it - the fix comment does,
        // so a later reader can see what was wrong - so only CODE lines are inspected.
        int lineNo = 0;
        foreach (string raw in File.ReadAllLines(prevSrc))
        {
            lineNo++;
            string t = raw.TrimStart();
            if (t.StartsWith("//")) continue;                 // prose, including the fix's own record
            bool scaled = false;
            foreach (string op in new[] { "*", "/" })
                foreach (string lhs in new[] { "CW = ", "CH = ", "int W = ", "int H = " })
                {
                    if (t.Contains(lhs + "W " + op + " ")) scaled = true;
                    if (t.Contains(lhs + "H " + op + " ")) scaled = true;
                }
            Check("PreviewMain.cs:" + lineNo + " does not scale the screen size", !scaled, raw.Trim());
        }

        // ---- 5. THE MEASURED TABLE IS AN ASPECT, KEPT APART FROM THE RENDER SIZE ----
        // The mesh measurement was taken at 1280 wide. If the cfg moves, the ASPECT is what carries
        // over - so the measured table is the reference the aspect is expressed against, and it is
        // the one place 1280 may legitimately still appear.
        Check("the measured mesh table is kept separately from the render size",
              src.Contains("MeasuredScreens"),
              "PreviewMain.cs has no MeasuredScreens - the mesh aspect and the render size have been "
              + "collapsed back into one declaration, which is what H-01 was");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (cfg screenWidth = " + cfgW + ")");
        return failures;
    }
}
