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
// are confirmed-real from the clean designer mockup, C1.4). Placed clear of the tab strip's own hit
// region — to its right, past the rightmost real tab's hit edge — so drawing and hitting a link can
// never collide with drawing or hitting a real tab.
//
// PURE: one rect per link, shared by Draw and HitTest (PageAction's "one rect function per control"
// rule), so the drawn text and the hit region can never drift apart.
// ============================================================================================
namespace DragonScreen
{
    public static class VehicleDeepViewLinks
    {
        public const int Commands = 8;   // 2 labels + 2 underline rules (+ headroom)
        const float RefW = 3427f, RefH = 2112f;

        static readonly string[] Label = { "SYSTEMS TREE", "SYSTEMS P&ID" };

        /// <summary>Link i's destination page. Index matches Label/X.</summary>
        public static readonly UiPage[] Target = { UiPage.SystemsTree, UiPage.SystemsPid };

        // Same row as VehicleTabBar's own strip (LabelY 1812 / MarkY 1858), starting past the tab
        // strip's rightmost hit edge (VehicleTabBar.CentreX(7) + half-pitch = 2431 + 102.5 = 2533.5) so
        // there is always clear separation between the last real tab and the first link.
        static readonly float[] X = { 2650f, 2960f };
        const float LinkW = 260f, TextY = 1812f, TextSize = 26f, RuleY = 1858f, RuleH = 4f;
        const float HitTop = 1778f, HitBot = 1878f;   // mirrors VehicleTabBar's own hit band exactly

        public static void Draw(DisplayList dl, int w, int h)
        {
            float sx = w / RefW, sy = h / RefH;
            for (int i = 0; i < Label.Length; i++)
            {
                dl.Text(Label[i], X[i] * sx, TextY * sy, TextSize * sy, TextAlign.Left, DragonPalette.Accent);
                dl.Rect(X[i] * sx, RuleY * sy, LinkW * sx, RuleH * sy, DragonPalette.Accent);
            }
        }

        /// <summary>Which link (0 Systems Tree, 1 Systems P&amp;ID) a touch hit, or -1.</summary>
        public static int HitTest(float px, float py, int w, int h)
        {
            float dx = px * RefW / w, dy = py * RefH / h;
            if (dy < HitTop || dy >= HitBot) return -1;
            for (int i = 0; i < X.Length; i++)
                if (dx >= X[i] - 20f && dx < X[i] + LinkW + 20f) return i;
            return -1;
        }
    }
}
