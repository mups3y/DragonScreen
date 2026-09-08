/*
 * S240 — THE `Tri` PRIMITIVE: its packing, its one rule, and the pin that keeps two renderers honest.
 *
 * ⛔ WHY THIS PRIMITIVE EXISTS. The locked base-screen design needs two FILLED shapes that no existing
 * DrawKind can make: the outer border's 45-degree chamfer at each bottom corner, and the icon window's
 * trapezoidal notch. `Line` can STROKE a diagonal; until now nothing could FILL beside one. S239 stopped
 * Prompt 1 at Gate 0 over exactly this.
 *
 * ⭐ WHAT THIS SUITE CAN AND CANNOT REACH, SAID UP FRONT. `build.py test` compiles `src/pure` + `test`
 * ONLY, so NEITHER rasteriser is reachable from here: `ScreenPainter` needs Unity, `PreviewMain` needs
 * System.Drawing. So this suite proves the PURE half — the packing and the degeneracy rule — and pins
 * the two renderers against each other BY SOURCE. The pixels are proved separately, by
 * `DragonScreenPreview.exe --tricheck`, which runs the real preview path at 1920x1054 and at the shipped
 * 2560x1405. ⛔ The GL painter is rasterised by neither, and that is this task's honest limit.
 *
 * ⭐⭐ THE PIN IS ABOUT PAIRING, NOT ORDER, AND THE DIFFERENCE IS A REAL FINDING.
 * Swapping two VERTICES of a triangle yields the same three points and therefore the same shape — GDI+
 * does not care about winding for a simple polygon, and the GL painter forces `_Cull Off`
 * (`ScreenPainter.cs:1356`). So a vertex swap is invisible in BOTH rasterisers and no picture can catch
 * it; it is caught here, at the packing level. What IS visible is a MIS-PAIRING — reading (A,C) and
 * (B,D) as the points instead of (A,B) and (C,D) — which builds a different triangle. `--tricheck`
 * measures that difference, so the pin below is guarding something that demonstrably matters.
 */
using DragonScreen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public static class DisplayListTriTest
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

    // ⛔ Text assertions match commented-out code. (Idiom: `AutoTargetTest.Live`, S220, which shipped two
    // surviving mutants for exactly this reason.) This file leans on it hard: BOTH renderer branches are
    // introduced by comments that NAME the fields they unpack, so a naive scan would "find" the pairing
    // in the prose that describes it and pass on a deleted branch.
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

    public static int Run()
    {
        Console.WriteLine("DisplayListTriTest (S240: the Tri primitive - packing, degeneracy, and the two-renderer pin)");
        checks = 0; failures = 0;

        TheKindIsAppendedNotRenumbered();
        ThePacking();
        DegeneracyIsRejectedInThePureLayer();
        BothRenderersUnpackTheSamePairing();
        TheRasterProofExistsAndIsReachable();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + (failures == 0 ? "   (pixels are proved separately by --tricheck)" : ""));
        return failures;
    }

    // ============================================================================================
    //  1. ⛔ APPEND ONLY. The byte is persisted in a DrawCmd; renumbering silently repaints the world.
    // ============================================================================================
    static void TheKindIsAppendedNotRenumbered()
    {
        Check("S240 Tri is byte 5 - appended after Line, not inserted",
              (byte)DrawKind.Tri == 5, "got " + (byte)DrawKind.Tri);
        // ⛔ Every pre-existing kind keeps its byte. If one moved, every page in every recording and
        // every persisted list would repaint as a different shape, and nothing would say so.
        Check("S240 ...and Rect is still 0", (byte)DrawKind.Rect == 0, "got " + (byte)DrawKind.Rect);
        Check("S240 ...ArcBand still 1", (byte)DrawKind.ArcBand == 1, "got " + (byte)DrawKind.ArcBand);
        Check("S240 ...Text still 2", (byte)DrawKind.Text == 2, "got " + (byte)DrawKind.Text);
        Check("S240 ...Image still 3", (byte)DrawKind.Image == 3, "got " + (byte)DrawKind.Image);
        Check("S240 ...Line still 4", (byte)DrawKind.Line == 4, "got " + (byte)DrawKind.Line);
    }

    // ============================================================================================
    //  2. THE PACKING: A,B = p0 · C,D = p1 · StartDeg,EndDeg = p2
    // ============================================================================================
    static void ThePacking()
    {
        DisplayList dl = new DisplayList(8);
        // ⚠ (1,2) (3,4) (5,6) was the first fixture here and the degeneracy guard REJECTED it -
        // correctly: all three lie on y = x+1. ⭐ The guard caught the test, which is the right way
        // round. p2's y is 9 so the three points span a real area.
        dl.Tri(1f, 2f, 3f, 4f, 5f, 9f, new Rgba(0.1f, 0.2f, 0.3f, 0.4f));
        Check("S240 one Tri produces exactly one command", dl.Count == 1, "got " + dl.Count);
        DrawCmd c = dl.At(0);
        Check("S240 ...of kind Tri", c.Kind == DrawKind.Tri, c.Kind.ToString());
        // ⭐ Each vertex asserted as a PAIR, so a mis-pairing fails on the pair that moved rather than
        // on a single number that could be explained away.
        Check("S240 p0 is packed in A,B", c.A == 1f && c.B == 2f, c.A + "," + c.B);
        Check("S240 p1 is packed in C,D", c.C == 3f && c.D == 4f, c.C + "," + c.D);
        Check("S240 p2 is packed in StartDeg,EndDeg", c.StartDeg == 5f && c.EndDeg == 9f,
              c.StartDeg + "," + c.EndDeg);
        Check("S240 the colour survives", c.Colour.A == 0.4f && c.Colour.R == 0.1f, "");
        // A Tri carries no text, no image and no UV window - it must not look like another kind.
        Check("S240 a Tri is not mistakeable for a Text or Image command",
              c.Str == null && c.Image == ImageId.None, "");

        // ⛔ ORDER-SENSITIVITY OF THE PACKING. The shape is the same either way (see the header), but
        // the COMMAND is not, and this is the level at which a swap is catchable at all.
        DisplayList d2 = new DisplayList(8);
        d2.Tri(3f, 4f, 1f, 2f, 5f, 9f, new Rgba(0f, 0f, 0f, 1f));
        Check("S240 swapping two vertices changes the packed command (the only level it shows)",
              d2.At(0).A == 3f && d2.At(0).C == 1f, "");
    }

    // ============================================================================================
    //  3. ⭐⭐ THE ONE RULE, AND WHY IT LIVES HERE
    // ============================================================================================
    static void DegeneracyIsRejectedInThePureLayer()
    {
        // ⛔ THE ARGUMENT, because it is the whole reason this is not left to the renderers: "no pixels"
        // and "a hairline" are both defensible readings of a collinear polygon, and GDI+ and GL need not
        // agree on which. Deciding it once, here, removes the only question the two rasterisers could
        // have answered differently.
        DisplayList dl = new DisplayList(8);
        dl.Tri(0f, 0f, 10f, 0f, 20f, 0f, new Rgba(1f, 1f, 1f, 1f));      // collinear, horizontal
        Check("S240 a collinear triangle is dropped", dl.Count == 0, "got " + dl.Count);
        dl.Tri(0f, 0f, 5f, 5f, 10f, 10f, new Rgba(1f, 1f, 1f, 1f));      // collinear, diagonal
        Check("S240 ...on a diagonal too", dl.Count == 0, "got " + dl.Count);
        dl.Tri(7f, 7f, 7f, 7f, 7f, 7f, new Rgba(1f, 1f, 1f, 1f));        // all three coincident
        Check("S240 ...and three coincident points", dl.Count == 0, "got " + dl.Count);
        dl.Tri(0f, 0f, 10f, 0f, 10f, 0f, new Rgba(1f, 1f, 1f, 1f));      // two coincident
        Check("S240 ...and two coincident points", dl.Count == 0, "got " + dl.Count);
        dl.Tri(0f, 0f, float.NaN, 0f, 0f, 10f, new Rgba(1f, 1f, 1f, 1f));
        Check("S240 ...and a NaN vertex, which no rasteriser should be handed", dl.Count == 0,
              "got " + dl.Count);

        // ⭐ NEGATIVE CONTROL. If the guard rejected everything, every check above would pass and the
        // primitive would be useless. A real triangle - including a very thin one - must survive.
        dl.Tri(0f, 0f, 10f, 0f, 0f, 10f, new Rgba(1f, 1f, 1f, 1f));
        Check("S240 ⭐ NEGATIVE CONTROL - a real triangle is NOT dropped", dl.Count == 1, "got " + dl.Count);
        dl.Tri(0f, 0f, 1000f, 0f, 0f, 0.001f, new Rgba(1f, 1f, 1f, 1f));
        Check("S240 ...and a very thin but real triangle survives", dl.Count == 2, "got " + dl.Count);
        // Winding must not matter to acceptance - a clockwise triangle is as real as a counter-clockwise
        // one, and the GL painter forces `_Cull Off` precisely so neither is dropped downstream.
        dl.Tri(0f, 0f, 0f, 10f, 10f, 0f, new Rgba(1f, 1f, 1f, 1f));
        Check("S240 ...and the opposite winding is accepted too", dl.Count == 3, "got " + dl.Count);
    }

    // ============================================================================================
    //  4. ⛔⛔ THE TWO RENDERERS. The one thing no picture can catch on its own.
    // ============================================================================================
    static void BothRenderersUnpackTheSamePairing()
    {
        string gl = Live(File.ReadAllText(Repo("plugin", "src", "ScreenPainter.cs")));
        string pv = Live(File.ReadAllText(Repo("plugin", "preview", "PreviewMain.cs")));

        // Both must actually dispatch the kind. ⛔ `PreviewMain`'s chain ends in a bare `else` that
        // routes to DrawText, so a MISSING branch there would not crash - it would quietly try to draw
        // a triangle as a string. That is why this is asserted rather than assumed.
        Check("S240 the GL painter dispatches Tri", gl.Contains("c.Kind == DrawKind.Tri"), "");
        Check("S240 the preview dispatches Tri", pv.Contains("c.Kind == DrawKind.Tri"), "");
        Check("S240 ⛔ ...and the preview's branch comes BEFORE its catch-all else->DrawText",
              pv.IndexOf("DrawKind.Tri") < pv.IndexOf("DrawText(g, brush, c)"), "");

        // ⭐⭐ THE PAIRING PIN. Each renderer must read the six floats as THREE POINTS in the same
        // grouping. A mis-pairing builds a different triangle (measured by `--tricheck`: centroid x
        // 184.8 vs 47.0), and it would leave one renderer right and the other wrong with nothing to
        // show for it - H-01's shape exactly.
        Check("S240 ⭐⭐ the GL painter pairs (A,B) (C,D) (StartDeg,EndDeg)",
              Regex.IsMatch(gl, @"GL\.Vertex3\(c\.A, c\.B, 0f\);\s*GL\.Vertex3\(c\.C, c\.D, 0f\);\s*GL\.Vertex3\(c\.StartDeg, c\.EndDeg, 0f\);"),
              "");
        Check("S240 ⭐⭐ ...and the preview pairs them identically",
              Regex.IsMatch(pv, @"PointF\(c\.A, c\.B\),\s*new PointF\(c\.C, c\.D\),\s*new PointF\(c\.StartDeg, c\.EndDeg\)"),
              "");

        // The GL side must use a triangle primitive, not a quad with a duplicated vertex - a quad would
        // put a fourth vertex somewhere and the two renderers would cover different areas.
        Check("S240 the GL painter draws it as GL.TRIANGLES", gl.Contains("GL.Begin(GL.TRIANGLES)"), "");
        // The preview must FILL, not stroke: a stroked outline lands edge pixels by the pen's rule and
        // the interior by the fill's, which is how a shared edge with a Rect seams.
        Check("S240 the preview FILLS the polygon rather than stroking it",
              pv.Contains("g.FillPolygon(brush"), "");

        // ⛔ Neither renderer may re-decide degeneracy - it is settled in the pure helper, and a second
        // opinion in one renderer only would be a divergence with no symptom.
        Check("S240 ⛔ neither renderer second-guesses the degeneracy rule",
              !Regex.IsMatch(gl, @"DrawTri[\s\S]{0,400}?(area|collinear)")
              && !Regex.IsMatch(pv, @"FillTriPreview[\s\S]{0,400}?(area|collinear)"), "");
    }

    // ============================================================================================
    //  5. THE RASTER PROOF MUST EXIST — a suite that cannot see pixels must at least know who can
    // ============================================================================================
    static void TheRasterProofExistsAndIsReachable()
    {
        string pv = Live(File.ReadAllText(Repo("plugin", "preview", "PreviewMain.cs")));
        Check("S240 the raster check exists and is reachable from the preview binary",
              pv.Contains("--tricheck") && pv.Contains("TriCheck()"), "");
        // ⛔ It must exercise BOTH sizes. A primitive proved only at the preview's own frame is the
        // H-01 defect waiting to happen: that was a preview rendered at 2x the shipped width, and
        // nothing noticed because nothing checked the shipped size.
        Check("S240 ⛔ ...at the preview frame AND the shipped 2560x1405",
              pv.Contains("TriCheckAt(1920, 1054") && pv.Contains("TriCheckAt(2560, 1405"), "");
        // And it must dispatch through the REAL preview method, not a copy of it.
        Check("S240 ...through the real FillTriPreview, not a reimplementation",
              Regex.IsMatch(pv, @"static void Render\(Graphics g, DisplayList dl\)[\s\S]{0,600}?FillTriPreview\("), "");
    }
}
