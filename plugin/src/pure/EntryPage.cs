// DragonScreen — EntryPage  (PURE: "Entry", T8 — reconstruct, marked)
// ============================================================================================
// SCREEN_INVENTORY.md #25 / BUILD_PLAN.md §3 row "Entry": a real Crew Dragon entry/descent procedure
// screen whose only evidence is a PARTIAL `discovery` "Entry" frame (tier-1, real capsule, but only
// one section legible) — "reconstruct + MARK" per §7 item 4, the same footing as DeorbitBurnPrepPage
// (T7) and VrioTestPage (also photo-only, no Figma/demo ref) — thinner still, since only a section
// TITLE transcribed, not any step text.
//
// ---- WHAT IS REAL, WHAT IS OURS (§1.4) ----
// The one fact transcribed from the frame (SCREEN_INVENTORY.md, both the table row and the "NEW
// findings" residual-research bullet): a "Parachute Deployment Altitude" section + numbered steps —
// no step TEXT was legible. Rather than invent step text, this card reuses the SAME real, already-
// vetted drogue/main deploy altitudes + actions shipped on ManualChuteDeployPage.cs's Standard
// schedule — one real source for one real physical event, not a second, independently-invented set
// of numbers that could silently disagree with it (the same discipline T6 applied to NavPage.Orbit
// and T7 applied to reusing PageState fields across pages). Those numbers were NOT legible in the
// Entry frame itself — only the section title was; the reuse is noted here so the attribution stays
// honest. "(TBC)" is kept verbatim, matching ManualChuteDeployPage's own comment that it is SpaceX's
// own to-be-confirmed placeholder text, not ours.
// The page CHROME (card layout, title, spacing) is ours — no layout is measurable from a partial
// frame — built in DeorbitBurnPrepPage's card style (accent dot + title + lines), which itself
// followed CoverPage.DrawReferenceContent's (T3) convention for reconstructed real content.
// UiPage.Entry (14) was NOT reused for this page: its FigmaUI.Titles entry is "ENTRY GO / NO-GO",
// a leftover phase-rail ACTION-item int from the old numbering (S14) — unrelated to this standalone
// screen despite the name collision. A new value, EntryProcedure, was appended instead so the Menu
// card's label ("ENTRY") actually matches what this page draws.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class EntryPage
    {
        // background + title + 1 card (dot+title+lines) + bottom bar.
        public const int Commands = 40;
        const float RefW = 3427f, RefH = 2112f;
        const float CardX = 300f, CardW = 2827f;

        public static void Build(DisplayList dl, int w, int h)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f; if (ox < 0f) ox = 0f;
            float X(float x) => x * sc + ox;
            float Y(float y) => y * sc;
            float Z(float v) => v * sc;

            dl.Rect(0, 0, w, h, DragonPalette.Background);
            dl.Text("ENTRY", w * 0.5f, Y(60), Z(44), TextAlign.Centre, DragonPalette.Accent);

            void Dot(float titleY) => dl.ArcBand(X(CardX + 33f), Y(titleY + 28), Z(4), Z(9), 0, 360, DragonPalette.Accent);
            void Title(string t, float titleY) =>
                dl.Text(t, X(CardX + 62f), Y(titleY), Z(34), TextAlign.Left, DragonPalette.White);
            void Lines(string[] lines, float titleY, float spacing)
            {
                float ry = titleY + 56f;
                for (int i = 0; i < lines.Length; i++)
                {
                    dl.Text(lines[i], X(CardX + 40f), Y(ry), Z(26), TextAlign.Left, DragonPalette.Text2);
                    ry += spacing;
                }
            }

            // ---- PARACHUTE DEPLOYMENT ALTITUDE — the one real section (transcribed title); the
            // altitude/action steps beneath reuse ManualChuteDeployPage's real Standard-schedule
            // numbers for the same physical event (see header comment) ----
            const float C1Y = 260f;
            Dot(C1Y); Title("PARACHUTE DEPLOYMENT ALTITUDE", C1Y);
            Lines(new[] {
                "5.5 km (TBC): monitor altitude, arm and verify backup pyros",
                "Deploy drogues — latch",
                "1.6 km (TBC): fire pyro, arm and verify backup pyros",
                "Deploy mains — execute",
                "Land under ≥ 3 mains",
                "CUT MAINS after splashdown" }, C1Y, 40f);

            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame
        }
    }
}
