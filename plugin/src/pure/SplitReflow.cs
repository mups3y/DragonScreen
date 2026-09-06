// DragonScreen — SplitReflow  (PURE: the fill-to-fit x-map for the two SPLIT pages, ONE copy)
// ============================================================================================
// The Cover (Frame 67) and Manual Chute Deploy are the only two pages in the build that reflow with a
// SPLIT rather than a letterbox — verified: `grep` for `Split = 1500` / `>= Split` across
// `plugin/src/pure/` returns those two files and nothing else. Every other Figma-era page centres its
// frame with `ox = (w - RefW*sc) * 0.5f`, and their slack is a different, still-open question.
//
// ---- WHY THIS FILE EXISTS (S174) ----
// The map was written out FOUR times — `CoverPage.Build`, `CoverPage.DrawRail`, `CoverPage.HitTest`'s
// INVERSE, and `ManualChuteDeployPage.Build` — in two files. `MarginAffordance`'s header records where
// that ends: three copies of one rectangle, and a hit band that had not matched its own painted box for
// as long as anyone had been looking. **Eight of the Cover's ten hit rects live inside the content
// panel this task widens**, so a forward map that moved without its inverse would have re-created that
// defect on the page the crew opens on. Forward, width and inverse are one function each, here.
//
// ---- THE RULE ----
//     x < PanelL            ->  x*sc                     the left margin and the phase rail: unmoved
//     PanelL <= x < PanelR  ->  PanelL*sc + (x-PanelL)*sc*K   the content panel: STRETCHED about its left edge
//     PanelR <= x < Split   ->  x*sc + share             carried across by the panel's growth
//     x >= Split            ->  x*sc + extra             the right block: still pinned to the right edge
//
// It is continuous at PanelR by construction (PanelL*sc + PanelW*sc*K == PanelR*sc + share) and steps at
// Split by (extra - share) — that step IS the remaining gap, and it is the same kind of step the original
// rule made with the whole of `extra`.
//
// ⭐ `Wd` is now `X(x + wref) - X(x)` — the general definition, with no cases of its own. The old
// hand-written version had to special-case straddlers; this one gets them right because it cannot do
// anything else, and it is correct for a span in any zone including ones nobody has drawn yet.
//
// ---- WHAT THE PANEL'S SHARE BUYS, AND WHY STRETCHING THE MAP IS THE POINT ----
// The panel's contents are placed at absolute design x INSIDE it, so widening the box alone would give a
// wider empty panel with the same overflows. Stretching the map carries them: a card row drawn at design
// 340 in a card that ends at 1427 moves right by 122*(K-1)*sc while its card's right edge moves by
// 1209*(K-1)*sc — nearly ten times as far — so the row gains 1087*(K-1)*sc of room. That ratio is the
// whole mechanism, and it is why the rule stretches rather than translates.
//
// ⛔ THE CAMERA SLOT DOES NOT SHRINK. It is drawn by its own `v*sc + extra` map (CoverPage's camera
// block), so its width is (RefW - ViewLeft)*sc at any panel size and `share` never enters it. What
// shrinks is the GAP between the panel and the slot. The owner accepted a shrinking slot as a cost of
// this option; measured, the rule does not spend it.
// ============================================================================================
namespace DragonScreen
{
    public static class SplitReflow
    {
        public const float RefW = 3427f, RefH = 2112f;

        /// <summary>Design x where the right-hand block begins. Everything at or past it is pinned to the
        /// panel's right edge, which is what keeps the globe, its controls and the telemetry cells in the
        /// same place on any panel. NOT the reference's number — ours (see the superseded note in
        /// CoverPage.Build), which is why re-distributing around it changes no tier-1 proportion.</summary>
        public const float Split = 1500f;

        /// <summary>`rectangle_178`'s own measured design span — the content panel. Taken from the asset
        /// placement, not typed independently: `Box` gives it as x 218, w 1224.</summary>
        public const float PanelL = 218f, PanelR = 1442f;
        public const float PanelW = PanelR - PanelL;      // 1224

        /// <summary>How much of the horizontal slack the content panel takes; the remainder stays in the
        /// gap. **A POLICY NUMBER, and the one knob this rule has** — a later task tunes the page by
        /// changing this alone.
        ///
        /// Half, for a stated reason rather than a round-number one: the panel needs far less than half to
        /// clear the overflows measured in S153a (the worst card row needs K >= 1.027, i.e. ~33 design px
        /// of the 418.5 available), so a share sized to today's content would be spent the moment the type
        /// rises again. Half leaves the panel real headroom for S153a-f AND leaves the gap a real gap, so
        /// the page still reads as two blocks with air between them rather than one wall. Taking all of it
        /// would close the gap entirely, which is a composition change nobody asked for.</summary>
        public const float PanelSlackShare = 0.5f;

        /// <summary>The four numbers every caller needs. One derivation, so `Build`, `DrawRail`, the hit
        /// test and Manual Chute cannot disagree about the frame they are working in.</summary>
        public static void Metrics(int w, int h, out float sc, out float extra, out float share, out float k)
        {
            sc = (h > 0) ? h / RefH : 0f;
            extra = w - RefW * sc; if (extra < 0f) extra = 0f;
            share = extra * PanelSlackShare;
            float panelPx = PanelW * sc;
            k = (panelPx > 0f) ? (panelPx + share) / panelPx : 1f;
        }

        /// <summary>Design x -> panel x.</summary>
        public static float X(float x, int w, int h)
        {
            float sc, extra, share, k; Metrics(w, h, out sc, out extra, out share, out k);
            if (x < PanelL) return x * sc;
            if (x < PanelR) return PanelL * sc + (x - PanelL) * sc * k;
            if (x < Split)  return x * sc + share;
            return x * sc + extra;
        }

        /// <summary>The width a design-space span occupies on the panel. ⭐ Defined as the map applied to
        /// both ends, so it has no cases of its own and cannot disagree with <see cref="X"/>.</summary>
        public static float Wd(float x, float wref, int w, int h)
        {
            return X(x + wref, w, h) - X(x, w, h);
        }

        /// <summary>Panel x -> design x: the exact inverse of <see cref="X"/>, for hit testing.
        ///
        /// ⛔ A touch that lands in the GAP (between the panel's right edge and the right block) has no
        /// design x at all — the map steps over that range. It is resolved to the gap's own design
        /// position, at or just past PanelR, which is dead space on both pages; it deliberately does NOT
        /// resolve into the right block, because that would let a touch on empty background fire a camera
        /// control. That is the `MarginAffordance` defect and it is not being re-created here.</summary>
        public static float InvX(float px, int w, int h)
        {
            float sc, extra, share, k; Metrics(w, h, out sc, out extra, out share, out k);
            if (sc <= 0f) return 0f;
            if (px < PanelL * sc) return px / sc;
            if (px < PanelR * sc + share) return PanelL + (px - PanelL * sc) / (sc * k);
            if (px < Split * sc + extra)
            {
                // ⛔ CLAMPED, AND SplitReflowTest FOUND THIS RATHER THAN A REVIEWER. Un-clamped this
                // read `(px - share) / sc`, which for a touch in the GAP returns a design x PAST Split -
                // measured 1546.6 at the shipped panel - i.e. it resolved empty background into the
                // right block, where the camera controls live. A touch on nothing firing a control is
                // the `MarginAffordance` defect exactly, and it would have been introduced by the very
                // change that removed the other four copies of this map.
                // The gap has no design x of its own; the honest answer is the last real coordinate
                // before it, so a gap touch reads as "just off the panel's right edge", which is what it
                // physically is. Nothing is drawn in design 1442..1500 on either page.
                float d = (px - share) / sc;
                return (d > Split) ? Split : d;
            }
            return (px - extra) / sc;
        }
    }
}
