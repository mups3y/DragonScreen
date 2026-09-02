// DragonScreen — DeorbitBurnPrepPage  (PURE: "Deorbit Burn Prep", T7 — reconstruct, marked)
// ============================================================================================
// SCREEN_INVENTORY.md #24 / BUILD_PLAN.md §3 row "Deorbit Burn Prep": a real Crew Dragon procedure
// screen whose only evidence is a BLURRY `discovery` deorbit-burn photo frame (tier-1, real capsule,
// but not legible enough to measure a layout from) — "reconstruct + MARK" per §7 item 3, the same
// footing as the Suit-Leak fail branch (§14.4d) and VrioTestPage (also photo-only, no Figma/demo ref).
//
// ---- WHAT IS REAL, WHAT IS OURS (§1.4) ----
// Two independent real sources agree on this screen's field NAMES (never its pixel layout):
//   · SCREEN_INVENTORY.md's transcription of the blurry photo: "Crew Interrupt Conditions" +
//     "Slew for Deorbit Burn" (Roll/Pitch/Yaw, Maximum altitude rate, FC Slew) + numbered settle-burn
//     steps ("~3 min prior to burn Dragon performs single-pulse burns to settle propellant; 8 min
//     duration; 1 sec pulses at 30 sec intervals; state oscillates between Deorbit Burn Prep and
//     Deorbit Burn Settle").
//   · SCREENS_LOOK_AND_FUNCTION_RESEARCH.md's read of the community recreation's own First.vue source
//     (tier-1/2, UI_AUDIT.md's source): "Crew Interrupt Conditions (30° sustained altitude error /
//     FAR-FIELD POINTING / 600°/min altitude rate)".
// The three Crew Interrupt Conditions criteria and the settle-burn sequence text below are transcribed
// from those two sources, not invented. NOTE (flagged for the owner, not silently changed): both
// sources — and the community Figma's own baked layer name, `600deg_m_altitude_rate` in
// CoverPage.cs's Keys — say "altitude", which plausibly should read "ATTITUDE" (an attitude-error /
// attitude-rate interrupt makes physical sense for a SLEW maneuver; "altitude" does not). Kept
// verbatim as sourced rather than corrected unilaterally (C1.4) — matches the existing shipped
// wording so nothing in the codebase disagrees with itself.
// Roll / Pitch / Yaw and "Maximum altitude rate" are real FIELD NAMES with no legible VALUE in the
// source photo, and no live vehicle quantity to source them from (this is a free-flight/inertial
// attitude slew, not the docking-relative PageState.RollText/PitchText/YawText the HUD uses).
//
// ---- T13c LOOKED AT THESE FOUR ROWS AND LEFT THEM DASHED, DELIBERATELY ----
// T13c's register line asked for exactly this call, "live or stay dashed, against the registry", and
// the answer is STAY DASHED. The four rows are a COMMANDED slew, not a measurement: ROLL / PITCH /
// YAW under "SLEW FOR DEORBIT BURN" are the attitude the vehicle is being TOLD to hold for the burn,
// and docs/TELEMETRY_REGISTRY.md carries no row for any of them — no SLEW_* datum, no authority,
// no source. The near-misses were considered and rejected as inventions of MEANING rather than of a
// number: the docking-relative errors are an error against a docking TARGET, not against a burn
// attitude; the body rates VesselData.Rates() publishes are how fast we are turning, not where we
// are being told to turn to; and a latched peak rate under "MAXIMUM ALTITUDE RATE" would be a
// plausible number under a label whose meaning this build cannot confirm — which is exactly what the
// registry's rule ("anything with no real source is UNKNOWN — EVIDENCE REQUIRED ... never invented")
// exists to stop. Their real source is the thing that will COMMAND the slew: the Part B conductor
// (T21, deorbit/entry/chutes). Until it exists they read "—", the same no-source-yet idiom T5
// used for the CONSUMABLES margin column. "FC Slew" is not decoration: it reads
// PageState.DeorbitEngaged, which the glue already threads from DeorbitOps.Engaged (_AutopilotStub.cs,
// the same idle stub STRING1C already reports through PanelButtons) — "NOT ENGAGED" until Part B wires
// it, the same honest-stub idiom every other command surface in this build reports (RendezvousPage's
// Hold Capture card, T6).
// The page CHROME (card layout, title, spacing) is ours — no layout is measurable from a blurry photo
// — built in the same card style CoverPage.DrawReferenceContent (T3) established for reconstructed
// real content: an accent dot, a title, and the sourced lines beneath it.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class DeorbitBurnPrepPage
    {
        // background + title + 3 cards (dot+title+lines, card 2 also label/value pairs) + bottom bar.
        public const int Commands = 80;
        const float RefW = 3427f, RefH = 2112f;
        const float CardX = 300f, CardW = 2827f;

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f; if (ox < 0f) ox = 0f;
            float X(float x) => x * sc + ox;
            float Y(float y) => y * sc;
            float Z(float v) => v * sc;

            dl.Rect(0, 0, w, h, DragonPalette.Background);
            dl.Text("DEORBIT BURN PREP", w * 0.5f, Y(60), Z(44), TextAlign.Centre, DragonPalette.Accent);

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

            // ---- CREW INTERRUPT CONDITIONS — the three real trigger criteria (both sources agree) ----
            const float C1Y = 260f;
            Dot(C1Y); Title("CREW INTERRUPT CONDITIONS", C1Y);
            Lines(new[] {
                "30° sustained altitude error",
                "Far-field pointing",
                "600°/min altitude rate" }, C1Y, 40f);

            // ---- SLEW FOR DEORBIT BURN — real field names; deliberately dashed (T13c) ----
            // The four values below have no source and are deliberately not given one — see the
            // header for the call and its reasoning. FC SLEW underneath them IS live, and is the row
            // that shows why: it reads the same Part B seam (PageState.DeorbitEngaged) that will one
            // day COMMAND the slew those four describe.
            const float C2Y = 500f;
            Dot(C2Y); Title("SLEW FOR DEORBIT BURN", C2Y);
            void Row(string label, string value, Rgba valueColour, float ry)
            {
                dl.Text(label, X(CardX + 40f), Y(ry), Z(26), TextAlign.Left, DragonPalette.Text2);
                dl.Text(value, X(CardX + CardW - 40f), Y(ry), Z(26), TextAlign.Right, valueColour);
            }
            float rowY = C2Y + 56f;
            Row("ROLL", "—", DragonPalette.Text6, rowY); rowY += 40f;
            Row("PITCH", "—", DragonPalette.Text6, rowY); rowY += 40f;
            Row("YAW", "—", DragonPalette.Text6, rowY); rowY += 40f;
            Row("MAXIMUM ALTITUDE RATE", "—", DragonPalette.Text6, rowY); rowY += 40f;
            bool slewing = s.DeorbitEngaged;
            Row("FC SLEW", slewing ? "ENGAGED" : "NOT ENGAGED", slewing ? DragonPalette.Go : DragonPalette.Text6, rowY);

            // ---- settle-burn sequence — SCREEN_INVENTORY's transcription of the photo, near-verbatim ----
            const float C3Y = 820f;
            Dot(C3Y); Title("DEORBIT BURN SETTLE", C3Y);
            Lines(new[] {
                "~3 min prior to burn: single-pulse burns settle propellant",
                "8 min duration",
                "1 sec pulses at 30 sec intervals",
                "State oscillates between Deorbit Burn Prep and Deorbit Burn Settle" }, C3Y, 40f);

            dl.Asset("component_48", 0f, Y(1877), w, Z(235), DragonPalette.White);
        }
    }
}
