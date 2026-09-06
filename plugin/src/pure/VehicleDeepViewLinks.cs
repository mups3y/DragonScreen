// DragonScreen — VehicleDeepViewLinks  (PURE: Vehicle-page link to the two systems deep-views, S27)
// ============================================================================================
// T9 built two vehicle systems deep-views (SystemsTree, SystemsPid) with no real in-page entry point
// of their own — reachable only via the Menu grid, "a real entry point is T14's job" (their own enum
// comments). T14 did not add one: S27 found the Cover phase rail's two "Procedure" slots are the only
// candidate real entry points, and what CONTENT belongs behind either is not in any source — assigning
// one would be a §1.4 tier-3 invention of a real screen's content, which is the owner's call and never
// a build chat's (C1.4/C1.12).
//
// The owner's S27 decision (option b, 2026-09-02, via the overseer): leave the rail alone and give the
// deep-views an affordance FROM the Vehicle pages instead — our geometry, marked as ours, same footing
// as T5's FUNCTIONS|ALERTS toggle (an invented control on a real page, stated as such in the code) and
// T6's Docking→Rendezvous affordance (a rect this build adds to reach a page with no real nav source of
// its own). Drawn on every Vehicle-family page (VehicleOverviewPage, VehicleSubsystemPage's six
// sub-tabs, VehicleMechPage — FigmaUI.IsVehiclePage's own set) so the two deep-views are one tap from
// wherever a crew member already is on the Vehicle side of the UI, not just from the Menu grid.
//
// Deliberately NOT a ninth/tenth VehicleTabBar tab: T9 already ruled that out (that strip's eight tabs
// are confirmed-real from the clean designer mockup, C1.4).
//
// ============================================================================================
// ---- S192, 2026-09-07: THE TWO LINKS ARE NOW THE REFERENCE'S OWN BUTTON PAIR --------------------
//
// 🟢 OWNER, 2026-09-07, verbatim, on the marked-up S185 preview:
//
//     "Systems cabin buttons are missing. Clicking cabin switches the white to that side and switches
//      to the cabin screen and vice versa. Except we are going to put replace them with systems tree
//      and systems p&id buttons. So keep "SYSTEMS" the same as in green box but change "CABIN" to
//      " SYS P&ID""
//
// ⭐ WHAT CHANGED, AND WHAT DID NOT. The two DESTINATIONS are untouched — `Target` still reads
// { SystemsTree, SystemsPid } and `FigmaUI` still maps a hit through it, so S27's decision is intact
// and no navigation was gained or lost. What changed is the AFFORDANCE: two accent words with an
// underline, parked to the RIGHT of the tab strip, become the reference's own bottom-LEFT pill pair —
// a white filled pill and an outlined one, which is the shape the owner pointed at. The labels are his:
// "SYSTEMS" (kept, in his words, "the same as in green box") and "SYS P&ID" (his replacement for the
// reference's "CABIN").
//
// ⚠ AND THE ACTIVE SIDE IS A REAL STATE, NOT DECORATION. The reference pair is a toggle — his own
// description, "clicking cabin switches the white to that side" — and his instruction for the left pill
// is visual and explicit: "keep "SYSTEMS" the same as in green box", where it is a solid WHITE pill.
// So the Vehicle pages light SYSTEMS, and what the light MEANS is "you are in the systems half of the
// UI", which on a Vehicle-family page is true. It is not a highlight for its own sake.
// ⛔ WHAT IS ONLY HALF-BUILT, SAID PLAINLY: the white cannot MOVE yet. `SystemsTreePage` and
// `SystemsPidPage` do not draw this pair and `FigmaUI.IsVehiclePage` does not include them, so pressing
// SYS P&ID takes you to a page that shows no pair to move the white to. The `active` parameter is here
// for the unit that reaches those two pages; until then the second half of his sentence is owed, and
// this comment is the record of that rather than a claim it is done.
//
// ⛔ GEOMETRY MEASURED, NOT PICKED. The pair's size and place come from the owner's own mock
// (assets/reference/nasa/interface_1950x1260.png, landed by S184): on its 1708x1019 card the white pill
// measures x 62..292, y 896..968 — 0.0363..0.1710 of width, 0.0716 of height tall. Scaled onto this
// build's 3427x2112 design frame that is a 151-unit-tall pair starting at x 124. The mock's Y cannot be
// copied (it has no global bottom bar and this build's `component_48` starts at y1877), so the pair is
// fitted into the band above it, level with the tab strip's own new panel.
//
// PURE: one rect function per control (PageAction's rule), shared by Draw and HitTest, so the drawn
// pill and the hit region can never drift apart.
// ============================================================================================
namespace DragonScreen
{
    public static class VehicleDeepViewLinks
    {
        // S192: 2 pills x (1 fill + 4 box edges + 2 round caps) + 2 labels.
        public const int Commands = 20;
        const float RefW = 3427f, RefH = 2112f;

        /// <summary>The owner's labels (S192), verbatim: "SYSTEMS" kept, "CABIN" replaced.</summary>
        static readonly string[] Label = { "SYSTEMS", "SYS P&ID" };

        /// <summary>Link i's destination page. Index matches Label/X. UNCHANGED by S192 — the look
        /// changed, the navigation did not.</summary>
        public static readonly UiPage[] Target = { UiPage.SystemsTree, UiPage.SystemsPid };

        // The pair, in design units. Left edge and height are the mock's own (see the header); the
        // width is split evenly because the two labels are near enough the same length, and the right
        // edge stops clear of VehicleTabBar's leftmost hit edge (CentreX(0) - half-pitch = 893.5).
        const float X0 = 124f, PillW = 380f, Top = 1700f, PillH = 151f, Radius = 24f;
        const float TextSize = 30f;

        /// <summary>Pill i's rect in DESIGN units. The one source both Draw and HitTest read.</summary>
        public static void Rect(int i, out float x, out float y, out float pw, out float ph)
        {
            x = X0 + i * PillW; y = Top; pw = PillW; ph = PillH;
        }

        /// <summary>Draw the pair as a Vehicle-family page wants it: the SYSTEMS side lit.
        ///
        /// 🟢 The owner's instruction is explicit and visual — "keep "SYSTEMS" the same as in green
        /// box" — and in that box SYSTEMS is a solid white pill. ⭐ THE LIT SIDE MEANS SOMETHING: it
        /// says which half of the UI you are in, and on every Vehicle-family page you are in the
        /// systems half. It is not a decorative highlight.
        /// ⚠ AND THE RESIDUAL IS STATED RATHER THAN HIDDEN: the white cannot MOVE yet, because the two
        /// deep-view pages do not draw this pair and `FigmaUI.IsVehiclePage` does not include them, so
        /// the "clicking switches the white to that side" half of his description is only half-built.
        /// The `active` parameter exists for the unit that reaches those two pages; nothing here
        /// pretends it is already done.</summary>
        public static void Draw(DisplayList dl, int w, int h) { Draw(dl, w, h, 0); }

        /// <summary>As above, with pill <paramref name="active"/> filled white and its label reversed
        /// out. Pass -1 for neither.</summary>
        public static void Draw(DisplayList dl, int w, int h, int active)
        {
            float sx = w / RefW, sy = h / RefH;
            for (int i = 0; i < Label.Length; i++)
            {
                float dx, dy, dw, dh;
                Rect(i, out dx, out dy, out dw, out dh);
                float x = dx * sx, y = dy * sy, pw = dw * sx, ph = dh * sy, r = Radius * sy;
                bool on = (i == active);
                if (on)
                {
                    // The filled side. Rounded ends built the way CoverPage's pill caps are, because
                    // DisplayList has no rounded-rect primitive and a square pill would not read as
                    // the same control the reference draws.
                    dl.Rect(x + r, y, pw - r * 2f, ph, DragonPalette.White);
                    dl.ArcBand(x + r, y + r, 0f, r, 270.0, 360.0, DragonPalette.White);
                    dl.ArcBand(x + r, y + ph - r, 0f, r, 180.0, 270.0, DragonPalette.White);
                    dl.Rect(x, y + r, r, ph - r * 2f, DragonPalette.White);
                    dl.ArcBand(x + pw - r, y + r, 0f, r, 0.0, 90.0, DragonPalette.White);
                    dl.ArcBand(x + pw - r, y + ph - r, 0f, r, 90.0, 180.0, DragonPalette.White);
                    dl.Rect(x + pw - r, y + r, r, ph - r * 2f, DragonPalette.White);
                }
                else
                {
                    dl.Box(x, y, pw, ph, Strokes.Px(3f, sy), DragonPalette.Text5);
                }
                dl.Text(Label[i], x + pw * 0.5f,
                        y + ph * 0.5f - TextSize * sy * Typography.CapCentreOfTop,
                        TextSize * sy, TextAlign.Centre,
                        on ? DragonPalette.Background : DragonPalette.White);
            }
        }

        /// <summary>Which link (0 Systems Tree, 1 Systems P&amp;ID) a touch hit, or -1.</summary>
        public static int HitTest(float px, float py, int w, int h)
        {
            float dx = px * RefW / w, dy = py * RefH / h;
            for (int i = 0; i < Label.Length; i++)
            {
                float x, y, pw, ph;
                Rect(i, out x, out y, out pw, out ph);
                if (dx >= x && dx < x + pw && dy >= y && dy < y + ph) return i;
            }
            return -1;
        }
    }
}
