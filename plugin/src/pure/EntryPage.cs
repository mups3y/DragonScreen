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

        /// <summary>⛔ TAKES `PageState` SINCE S157 (2026-09-06). It did not, and that was the whole of
        /// S49's H31: *"Entry page: nothing live at all, structurally — `Build(dl,w,h)` takes no
        /// `PageState`"*. The page printed real parachute-deployment altitudes and real deploy actions
        /// while `RadarAltitude`, `DroguesFired`, `MainsFired` and `MainsReleased` were all live one
        /// call away, and it could not see any of them because they were not passed in.</summary>
        public static void Build(DisplayList dl, int w, int h, PageState s)
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
            // ---- S157 / S49 H31: THE STEPS TRACK, ON EXACTLY THE MODEL [[S156]] LANDED ----
            // Three states from the palette the sibling chute page uses, so the two screens describing
            // the same physical event describe it the same way (C7.1):
            //     PASSED   Faint  - behind us
            //     CURRENT  Accent - the step being flown
            //     PENDING  Text2  - exactly what every line looked like before this
            //
            // ⛔ `Done` IS A tri-state, NOT A bool, and that is the point. `null` means THIS LINE HAS
            // NO SOURCE and must never be given a verdict - "Land under >= 3 mains" needs a count of
            // deployed canopies that nothing in the build models, so it stays neutral rather than
            // being folded into MainsFired, which would be a different claim wearing this one's words.
            // ⛔ And with no valid state EVERY line is pending: a page that cannot read the vehicle
            // must not claim a step was completed (S22 / S31's guardrail).
            bool live = s.Valid;
            double radarM = s.Steps.RadarAltitude;

            void Lines(string[] lines, bool?[] done, float titleY, float spacing)
            {
                // The first line whose step is not yet done is the CURRENT one. A line with no source
                // is skipped for that purpose rather than blocking the cursor behind it.
                int current = -1;
                if (live)
                    for (int i = 0; i < lines.Length; i++)
                        if (done[i].HasValue && !done[i].Value) { current = i; break; }

                float ry = titleY + 56f;
                for (int i = 0; i < lines.Length; i++)
                {
                    Rgba col = DragonPalette.Text2;
                    if (live && done[i].HasValue)
                        col = done[i].Value ? DragonPalette.Text7
                            : (i == current) ? DragonPalette.Accent
                                             : DragonPalette.Text2;
                    dl.Text(lines[i], X(CardX + 40f), Y(ry), Z(26), TextAlign.Left, col);
                    ry += spacing;
                }
            }

            // ---- PARACHUTE DEPLOYMENT ALTITUDE — the one real section (transcribed title); the
            // altitude/action steps beneath reuse ManualChuteDeployPage's real Standard-schedule
            // numbers for the same physical event (see header comment) ----
            const float C1Y = 260f;
            Dot(C1Y); Title("PARACHUTE DEPLOYMENT ALTITUDE", C1Y);
            // ⛔ THE GATE ALTITUDES ARE THE PAGE'S OWN, TRANSCRIBED FROM ITS OWN LABELS — 5500 and
            // 1600 — and NOT MissionPhase's FSM constants (5486 / 1830). `SCREEN_INVENTORY.md`
            // records that those are intentionally two different things: SpaceX's own "(TBC)"
            // placeholder text, kept verbatim (§1.4). [[S156]] landed the identical rule on
            // ManualChuteDeployPage, whose Standard schedule these six lines already reuse — so the
            // two pages now agree about one physical event in BOTH the numbers and the tracking.
            Lines(new[] {
                "5.5 km (TBC): monitor altitude, arm and verify backup pyros",
                "Deploy drogues — latch",
                "1.6 km (TBC): fire pyro, arm and verify backup pyros",
                "Deploy mains — execute",
                "Land under ≥ 3 mains",
                "CUT MAINS after splashdown" },
                new bool?[] {
                    radarM <= 5500.0,      // the page's own 5.5 km, never the FSM's 5486
                    s.DroguesFired,
                    radarM <= 1600.0,      // the page's own 1.6 km, never the FSM's 1830
                    s.MainsFired,
                    null,                  // no source: nothing models a CANOPY COUNT. Stays neutral.
                    s.MainsReleased },
                C1Y, 40f);

            BottomBar.Draw(dl, w, h, s);   // S103: undistorted, in the design frame; S147: CURRENT STATE live
        }
    }
}
