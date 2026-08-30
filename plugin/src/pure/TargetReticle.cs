// DragonScreen — TargetReticle  (Phase 6: pure display component)
// ============================================================================================
// The docking aim marks (SCREEN_EVIDENCE_MATRIX.md, Frame 58): a centred CROSSHAIR (a thin ring with
// a small cross) that the target must be brought onto, and a small diamond MARKER that shows the
// current target direction. Both are geometry-only — the caller supplies the colour (cyan for the
// live target, green when aligned, per the page).
//
// PURE: no KSP/Unity. Angles/rings via DisplayList's ArcBand/Line primitives.
// ============================================================================================
namespace DragonScreen
{
    public static class TargetReticle
    {
        // Crosshair = ring (1 ArcBand) + cross (2 lines) = 3 cmds; Marker = diamond (4 lines) = 4 cmds.
        public const int Commands = 4;

        private const float Stroke = 2f;

        /// <summary>A centred aim reticle: a thin ring of radius r with a small cross inside it.</summary>
        public static void Crosshair(DisplayList dl, float cx, float cy, float r, Rgba colour)
        {
            if (dl == null || r <= 0f) return;
            dl.ArcBand(cx, cy, r - Stroke, r, 0.0, 360.0, colour);   // the ring
            float arm = r * 0.55f;                                   // cross arms, kept inside the ring
            dl.Line(cx - arm, cy, cx + arm, cy, Stroke, colour);
            dl.Line(cx, cy - arm, cx, cy + arm, Stroke, colour);
        }

        /// <summary>A small diamond marker (the target-direction pip), outlined in four strokes.</summary>
        public static void Marker(DisplayList dl, float cx, float cy, float half, Rgba colour)
        {
            if (dl == null || half <= 0f) return;
            dl.Line(cx, cy - half, cx + half, cy, Stroke, colour);   // top → right
            dl.Line(cx + half, cy, cx, cy + half, Stroke, colour);   // right → bottom
            dl.Line(cx, cy + half, cx - half, cy, Stroke, colour);   // bottom → left
            dl.Line(cx - half, cy, cx, cy - half, Stroke, colour);   // left → top
        }
    }
}
