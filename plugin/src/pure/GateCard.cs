/*
 * DragonScreen - GateCard (PURE)
 *
 * The crew checklist card: the on-screen face of the crew-in-the-loop procedure engine. When the
 * conductor reaches a gate the crew must act on, this card takes the FLIGHT page - the gate's title, its
 * checklist (crew items to tap, auto items the system confirms), and the GO / NO-GO / ABORT controls -
 * so the user does exactly what the real Crew Dragon crew do to authorise the next step.
 *
 * ---- PURE LAYOUT + DRAW; THE GLUE ROUTES THE TOUCH ----
 * All geometry and drawing are here and headless-tested (one Rect function per element, shared by draw
 * and hit-test - the project's standing rule). The touch itself is routed in the glue (ScreenPainter),
 * because acting on it drives CrewProcedureOps directly and the glue already holds that state; HitTest
 * returns which element a point fell on so the glue can dispatch it.
 *
 * ---- ONLY SHOWN WHEN THERE IS SOMETHING TO DO ----
 * CrewProcedureOps.CrewActionNeeded gates this: the card appears when a crew item is pending or the gate
 * is GO-ready, and stays OUT of the way while the autopilot is still flying to the point the gate
 * authorises (ascent, phasing). So it never blocks the crew's view during a phase they are monitoring.
 */
namespace DragonScreen
{
    /// <summary>One checklist row, ready to draw. The glue builds these from the live gate + item bits.</summary>
    public struct GateItemView
    {
        public string Label;
        public bool Checked;
        /// <summary>A crew item the user taps. False = an Auto item the system confirms (not tappable).</summary>
        public bool CrewActionable;
    }

    public enum GateHitKind : byte { None, Item, Go, NoGo, Abort }

    public struct GateHit
    {
        public GateHitKind Kind;
        public int Item;   // meaningful when Kind == Item
        public static GateHit Of(GateHitKind k) { GateHit g; g.Kind = k; g.Item = 0; return g; }
        public static GateHit ItemAt(int i) { GateHit g; g.Kind = GateHitKind.Item; g.Item = i; return g; }
        public static GateHit None { get { return Of(GateHitKind.None); } }
    }

    public static class GateCard
    {
        // ---- layout, render-target pixels ----
        private const float Pad = 18f;
        private const float TitleH = 34f;
        private const float StatusH = 24f;
        private const float RowH = 34f;
        private const float RowGap = 6f;
        private const float BtnH = 42f;
        private const float BtnGap = 10f;
        private const float MinW = 400f;
        private const float MaxW = 640f;
        private const float BoxSize = 20f;

        // ---- S121a, 2026-09-06: THE SCALE HAS TO REACH THE HIT TEST, NOT JUST THE DRAW ----------------
        // ⛔ EVERY CONSTANT ABOVE IS A RefPanelW PIXEL, and this card is the one widget in the R-02 family
        // where getting that half-right is worse than leaving it alone: `Draw` and `HitTest` BOTH lay out
        // through CardRect/ItemRect/ButtonRect, so scaling the drawing side only would put the buttons
        // somewhere the touch test does not look. That is QC H-04 exactly — S108 found a tap on empty
        // letterbox navigating because a box was written twice with different constants — and the fix
        // there was one rect shared by draw and hit. The same discipline is kept here.
        //
        // ⭐ SO THE SCALE IS NOT AN OVERLOAD ON THIS ONE. `CardRect` takes `w` already and derives it,
        // which makes divergence impossible rather than merely unlikely; `ItemRect`/`ButtonRect` have no
        // `w` to derive from, so they take `sc` as a REQUIRED parameter — fail-closed, the same move
        // [[S120]] made with `ChromeBar.TopY`, [[S158]] with `runActive` and [[S124]] with `floorDesign`.
        // A default would silently reintroduce the divergence this block exists to prevent.
        //
        // ⚠ `MaxW` is why this matters beyond type: `cw = w * 0.60f` clamped to a bare 640 gave a card
        // 640 px wide on a 2560 panel — a quarter of the width, holding a 400 px minimum meant for half
        // of 1280. Scaled, the clamp does what it was measured to do at either width.
        public static void CardRect(int w, int h, int items,
                                    out float x, out float y, out float cw, out float ch)
        {
            float sc = Typography.ScaleFor(w);
            cw = w * 0.60f;
            if (cw < MinW * sc) cw = MinW * sc;
            if (cw > MaxW * sc) cw = MaxW * sc;
            float rows = (items * RowH + (items > 0 ? (items - 1) * RowGap : 0f)) * sc;
            ch = (Pad + TitleH + StatusH) * sc + rows + (16f + BtnH + Pad) * sc;
            x = (w - cw) * 0.5f;
            y = (h - ch) * 0.5f - h * 0.03f;    // a touch above centre, clear of the gauges/chrome
            if (y < 36f * sc) y = 36f * sc;
        }

        public static void ItemRect(int i, float x, float y, float cw, float sc,
                                    out float rx, out float ry, out float rw, out float rh)
        {
            rx = x + Pad * sc;
            ry = y + (Pad + TitleH + StatusH) * sc + i * (RowH + RowGap) * sc;
            rw = cw - Pad * 2f * sc;
            rh = RowH * sc;
        }

        /// <summary>which: 0 GO, 1 NO-GO, 2 ABORT.</summary>
        public static void ButtonRect(int which, float x, float y, float cw, float ch, float sc,
                                      out float bx, out float by, out float bw, out float bh)
        {
            bh = BtnH * sc;
            by = y + ch - Pad * sc - bh;
            float total = cw - Pad * 2f * sc;
            bw = (total - BtnGap * 2f * sc) / 3f;
            bx = x + Pad * sc + (bw + BtnGap * sc) * which;
        }

        public static void Draw(DisplayList dl, string title, GatePhase phase,
                                GateItemView[] items, int w, int h)
        {
            int n = (items == null) ? 0 : items.Length;
            float sc = Typography.ScaleFor(w);
            float x, y, cw, ch;
            CardRect(w, h, n, out x, out y, out cw, out ch);

            // A dim backdrop over the whole screen, then the card - so the checklist reads as a modal
            // step, not another widget competing with the gauges behind it.
            dl.Rect(0f, 0f, w, h, new Rgba(0.008f, 0.027f, 0.22f, 0.72f));
            dl.Rect(x, y, cw, ch, DragonPalette.Panel);
            dl.Box(x, y, cw, ch, 2f * sc, DragonPalette.Accent);

            dl.Text(title ?? "", x + cw * 0.5f, y + Pad * sc, Typography.Value * sc, TextAlign.Centre,
                    DragonPalette.Accent);
            dl.Text(StatusText(phase), x + cw * 0.5f, y + (Pad + TitleH + 2f) * sc, Typography.Caption * sc,
                    TextAlign.Centre, StatusColour(phase));

            for (int i = 0; i < n; i++)
            {
                float rx, ry, rw, rh;
                ItemRect(i, x, y, cw, sc, out rx, out ry, out rw, out rh);
                DrawItem(dl, items[i], rx, ry, rw, rh, sc);
            }

            float gx, gy, gw, gh;
            ButtonRect(0, x, y, cw, ch, sc, out gx, out gy, out gw, out gh);
            bool goReady = phase == GatePhase.GoReady;
            Plate(dl, gx, gy, gw, gh, "GO",
                  goReady ? DragonPalette.Go : DragonPalette.Inset2,
                  goReady ? DragonPalette.Background : DragonPalette.Text7, sc);

            float nx, ny, nw, nh;
            ButtonRect(1, x, y, cw, ch, sc, out nx, out ny, out nw, out nh);
            Plate(dl, nx, ny, nw, nh, "NO-GO",
                  phase == GatePhase.NoGo ? DragonPalette.Caution : DragonPalette.Panel,
                  DragonPalette.Caution, sc);

            float ax, ay, aw, ah;
            ButtonRect(2, x, y, cw, ch, sc, out ax, out ay, out aw, out ah);
            Plate(dl, ax, ay, aw, ah, "ABORT", DragonPalette.Alarm, DragonPalette.White, sc);
        }

        public static GateHit HitTest(float px, float py, int w, int h, int items)
        {
            float sc = Typography.ScaleFor(w);
            float x, y, cw, ch;
            CardRect(w, h, items, out x, out y, out cw, out ch);

            for (int i = 0; i < items; i++)
            {
                float rx, ry, rw, rh;
                ItemRect(i, x, y, cw, sc, out rx, out ry, out rw, out rh);
                if (Control.Hit(px, py, rx, ry, rw, rh)) return GateHit.ItemAt(i);
            }
            float bx, by, bw, bh;
            ButtonRect(0, x, y, cw, ch, sc, out bx, out by, out bw, out bh);
            if (Control.Hit(px, py, bx, by, bw, bh)) return GateHit.Of(GateHitKind.Go);
            ButtonRect(1, x, y, cw, ch, sc, out bx, out by, out bw, out bh);
            if (Control.Hit(px, py, bx, by, bw, bh)) return GateHit.Of(GateHitKind.NoGo);
            ButtonRect(2, x, y, cw, ch, sc, out bx, out by, out bw, out bh);
            if (Control.Hit(px, py, bx, by, bw, bh)) return GateHit.Of(GateHitKind.Abort);
            return GateHit.None;
        }

        // ---- pieces ----
        // ⭐ DrawItem and Plate carry the OTHER half of the latent pair [[S121]] names (the first is
        // StatusIndicator.Badge): `(rh - Typography.Caption) * 0.5f` compares a real panel-pixel row
        // height against a RefPanelW type size. Correct only while nothing scales; wrong the moment
        // anything does. Scaled here so the label stays centred in its own row at either width.
        private static void DrawItem(DisplayList dl, GateItemView it,
                                     float rx, float ry, float rw, float rh, float sc)
        {
            float box = BoxSize * sc;
            float by = ry + (rh - box) * 0.5f;
            dl.Rect(rx, by, box, box, it.Checked ? DragonPalette.Go : DragonPalette.Inset2);
            dl.Box(rx, by, box, box, 2f * sc, DragonPalette.Hairline);

            Rgba tc = it.Checked ? DragonPalette.Text1
                    : it.CrewActionable ? DragonPalette.Text2 : DragonPalette.Text6;
            dl.Text(it.Label ?? "", rx + box + 12f * sc,
                    ry + (rh - Typography.Caption * sc) * 0.5f - 1f * sc, Typography.Caption * sc,
                    TextAlign.Left, tc);
        }

        private static void Plate(DisplayList dl, float x, float y, float w, float h,
                                  string label, Rgba face, Rgba text, float sc)
        {
            dl.Rect(x, y, w, h, face);
            dl.Box(x, y, w, h, 2f * sc, DragonPalette.Hairline);
            dl.Text(label, x + w * 0.5f, y + (h - Typography.Caption * sc) * 0.5f - 1f * sc,
                    Typography.Caption * sc, TextAlign.Centre, text);
        }

        private static string StatusText(GatePhase p)
        {
            switch (p)
            {
                case GatePhase.GoReady: return "READY - CREW GO REQUIRED";
                case GatePhase.Go:      return "GO";
                case GatePhase.NoGo:    return "NO-GO - HOLDING";
                case GatePhase.Abort:   return "ABORT";
                default:                return "COMPLETE THE CHECKLIST";
            }
        }

        private static Rgba StatusColour(GatePhase p)
        {
            switch (p)
            {
                case GatePhase.GoReady: return DragonPalette.Go;
                case GatePhase.Go:      return DragonPalette.Go;
                case GatePhase.NoGo:    return DragonPalette.Caution;
                case GatePhase.Abort:   return DragonPalette.Alarm;
                default:                return DragonPalette.Text4;
            }
        }
    }
}
