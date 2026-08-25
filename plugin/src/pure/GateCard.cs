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

        public static void CardRect(int w, int h, int items,
                                    out float x, out float y, out float cw, out float ch)
        {
            cw = w * 0.60f;
            if (cw < MinW) cw = MinW;
            if (cw > MaxW) cw = MaxW;
            float rows = items * RowH + (items > 0 ? (items - 1) * RowGap : 0f);
            ch = Pad + TitleH + StatusH + rows + 16f + BtnH + Pad;
            x = (w - cw) * 0.5f;
            y = (h - ch) * 0.5f - h * 0.03f;    // a touch above centre, clear of the gauges/chrome
            if (y < 36f) y = 36f;
        }

        public static void ItemRect(int i, float x, float y, float cw,
                                    out float rx, out float ry, out float rw, out float rh)
        {
            rx = x + Pad;
            ry = y + Pad + TitleH + StatusH + i * (RowH + RowGap);
            rw = cw - Pad * 2f;
            rh = RowH;
        }

        /// <summary>which: 0 GO, 1 NO-GO, 2 ABORT.</summary>
        public static void ButtonRect(int which, float x, float y, float cw, float ch,
                                      out float bx, out float by, out float bw, out float bh)
        {
            bh = BtnH;
            by = y + ch - Pad - bh;
            float total = cw - Pad * 2f;
            bw = (total - BtnGap * 2f) / 3f;
            bx = x + Pad + (bw + BtnGap) * which;
        }

        public static void Draw(DisplayList dl, string title, GatePhase phase,
                                GateItemView[] items, int w, int h)
        {
            int n = (items == null) ? 0 : items.Length;
            float x, y, cw, ch;
            CardRect(w, h, n, out x, out y, out cw, out ch);

            // A dim backdrop over the whole screen, then the card - so the checklist reads as a modal
            // step, not another widget competing with the gauges behind it.
            dl.Rect(0f, 0f, w, h, new Rgba(0.008f, 0.027f, 0.22f, 0.72f));
            dl.Rect(x, y, cw, ch, DragonPalette.Panel);
            dl.Box(x, y, cw, ch, 2f, DragonPalette.Accent);

            dl.Text(title ?? "", x + cw * 0.5f, y + Pad, Typography.Value, TextAlign.Centre,
                    DragonPalette.Accent);
            dl.Text(StatusText(phase), x + cw * 0.5f, y + Pad + TitleH + 2f, Typography.Caption,
                    TextAlign.Centre, StatusColour(phase));

            for (int i = 0; i < n; i++)
            {
                float rx, ry, rw, rh;
                ItemRect(i, x, y, cw, out rx, out ry, out rw, out rh);
                DrawItem(dl, items[i], rx, ry, rw, rh);
            }

            float gx, gy, gw, gh;
            ButtonRect(0, x, y, cw, ch, out gx, out gy, out gw, out gh);
            bool goReady = phase == GatePhase.GoReady;
            Plate(dl, gx, gy, gw, gh, "GO",
                  goReady ? DragonPalette.Go : DragonPalette.Inset2,
                  goReady ? DragonPalette.Background : DragonPalette.Text7);

            float nx, ny, nw, nh;
            ButtonRect(1, x, y, cw, ch, out nx, out ny, out nw, out nh);
            Plate(dl, nx, ny, nw, nh, "NO-GO",
                  phase == GatePhase.NoGo ? DragonPalette.Caution : DragonPalette.Panel,
                  DragonPalette.Caution);

            float ax, ay, aw, ah;
            ButtonRect(2, x, y, cw, ch, out ax, out ay, out aw, out ah);
            Plate(dl, ax, ay, aw, ah, "ABORT", DragonPalette.Alarm, DragonPalette.White);
        }

        public static GateHit HitTest(float px, float py, int w, int h, int items)
        {
            float x, y, cw, ch;
            CardRect(w, h, items, out x, out y, out cw, out ch);

            for (int i = 0; i < items; i++)
            {
                float rx, ry, rw, rh;
                ItemRect(i, x, y, cw, out rx, out ry, out rw, out rh);
                if (Control.Hit(px, py, rx, ry, rw, rh)) return GateHit.ItemAt(i);
            }
            float bx, by, bw, bh;
            ButtonRect(0, x, y, cw, ch, out bx, out by, out bw, out bh);
            if (Control.Hit(px, py, bx, by, bw, bh)) return GateHit.Of(GateHitKind.Go);
            ButtonRect(1, x, y, cw, ch, out bx, out by, out bw, out bh);
            if (Control.Hit(px, py, bx, by, bw, bh)) return GateHit.Of(GateHitKind.NoGo);
            ButtonRect(2, x, y, cw, ch, out bx, out by, out bw, out bh);
            if (Control.Hit(px, py, bx, by, bw, bh)) return GateHit.Of(GateHitKind.Abort);
            return GateHit.None;
        }

        // ---- pieces ----
        private static void DrawItem(DisplayList dl, GateItemView it,
                                     float rx, float ry, float rw, float rh)
        {
            float by = ry + (rh - BoxSize) * 0.5f;
            dl.Rect(rx, by, BoxSize, BoxSize, it.Checked ? DragonPalette.Go : DragonPalette.Inset2);
            dl.Box(rx, by, BoxSize, BoxSize, 2f, DragonPalette.Hairline);

            Rgba tc = it.Checked ? DragonPalette.Text1
                    : it.CrewActionable ? DragonPalette.Text2 : DragonPalette.Text6;
            dl.Text(it.Label ?? "", rx + BoxSize + 12f,
                    ry + (rh - Typography.Caption) * 0.5f - 1f, Typography.Caption, TextAlign.Left, tc);
        }

        private static void Plate(DisplayList dl, float x, float y, float w, float h,
                                  string label, Rgba face, Rgba text)
        {
            dl.Rect(x, y, w, h, face);
            dl.Box(x, y, w, h, 2f, DragonPalette.Hairline);
            dl.Text(label, x + w * 0.5f, y + (h - Typography.Caption) * 0.5f - 1f,
                    Typography.Caption, TextAlign.Centre, text);
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
