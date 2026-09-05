// DragonScreen — AscentPage  (PURE: "Ascent / Launch", T12 — data-buildable, layout reconstructed + MARKED)
// ============================================================================================
// SCREEN_INVENTORY.md #14 / BUILD_PLAN.md §3 row "Ascent / Launch": the one screen with NO public
// in-cabin frame at all — confirmed absent, not just unfound (SCREEN_INVENTORY's "still genuinely
// dark" / "no public frame exists" notes). Its DATA is real and complete (§8's mission-timeline pass,
// tier-1: NASA CCP press kit + DM-2/Crew-2 timelines), so §3 marks it "DATA-BUILDABLE, layout
// reconstructed + MARKED (T12)" — the same class as DeorbitBurnPrepPage (T7) and EntryPage (T8), one
// step further: those had a blurry/partial PHOTO to anchor a layout guess against; this one has none,
// so the whole page CHROME (F9 schematic proportions, event-callout placement) is ours, not just the
// spacing between real fields.
//
// ---- WHAT IS REAL, WHAT IS OURS (§1.4) ----
// REAL, from §8's mission-timeline pass (tier-1, T+ values from liftoff): Liftoff · Pitch kick ~0:10 ·
// Max-Q ~1:00 · Mach 1 ~1:09 · Stage-1b abort-mode ~1:14 · MECO ~2:30–2:35 · stage sep ~2:35–2:39 ·
// S2 ignition ~2:36–2:47 · SECO-1/orbit insertion ~4:20–8:43 · Dragon sep ~9:00–12:02 · nose-cone open
// ~12:48–13:23 — transcribed verbatim, not invented. REAL, public Falcon 9 facts (well documented by
// SpaceX, not Crew-Dragon-interior specific so no owner discussion needed, same footing as
// PropSchematic's "16 Dracos in 4 quads"): 9 Merlin 1D engines on stage 1, 1 Merlin Vacuum on stage 2;
// §8's own vehicle-structure facts (trunk = half solar array + half radiators, jettisoned before
// reentry). "ACTIVE PHASE" is not decoration: it reads PageState.Phase, the SAME live field
// DockingPage/DockingPageCentral already display (VesselData.cs threads it from Mission.Classify),
// reused rather than a second independently-invented status line (the discipline T6/T7/T9 all followed
// for their own live signals) — this is not new live-data wiring (that is T13's job), only reuse of an
// already-wired real field.
// OURS, and stated as such: the F9 line-art PROFILE (nose/capsule/trunk proportions, stage boundaries,
// grid-fin/leg/engine marks) and exactly where each real T+ event is called out against it — there is
// no photo to measure a layout from, so the placement is a narrative ordering (ground → orbit reads
// bottom → top, matching the real physical stack) rather than a scaled timeline or altitude plot.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class AscentPage
    {
        // background + title + active-phase line + ~30 hull/leg/fin/engine strokes + 4 section labels
        // + 11 event leaders + 11 event labels + bottom bar.
        public const int Commands = 100;
        const float RefW = 3427f, RefH = 2112f;

        // ---- the F9 + Dragon stack, in design space. Proportions are ours (no photo to measure). ----
        const float CX = 620f;                 // stack centreline
        const float HWCap = 125f;               // capsule + trunk half-width (Dragon, ~4 m)
        const float HWStack = 115f;             // S2 + interstage + S1 half-width (F9 core, 3.7 m)

        const float NoseY = 260f;               // Dragon nose-cone tip
        const float ShoulderY = 330f;           // nose cone ends / pressurised capsule begins
        const float CapsuleBotY = 460f;         // capsule / trunk boundary
        const float TrunkBotY = 700f;           // trunk / stage-2 boundary (Dragon separates here)
        const float S2BotY = 1080f;             // stage-2 engine mount / interstage top
        const float InterstageBotY = 1170f;     // interstage / stage-1 boundary
        const float BaseY = 1800f;              // stage-1 engine mounts (Octaweb)

        /// <summary>Half-width of the drawn hull at design-y, so every leader line starts exactly on
        /// the outline regardless of which section it points at.</summary>
        static float HalfWidth(float y)
        {
            if (y <= NoseY) return 0f;
            if (y < ShoulderY) return HWCap * (y - NoseY) / (ShoulderY - NoseY);
            if (y <= TrunkBotY) return HWCap;
            return HWStack;
        }

        // ---- the 11 real §8 ascent events, in physical/chronological order (ground -> orbit reads
        // bottom -> top, matching the stack: stage 1 at the base, Dragon's nose at the tip). ----
        static readonly float[] EventY =
            { 1780f, 1680f, 1560f, 1480f, 1400f, 1190f, 1125f, 1000f, 800f, 680f, 300f };
        static readonly string[] EventText = {
            "LIFTOFF",
            "T+0:10 — PITCH KICK",
            "T+1:00 — MAX-Q",
            "T+1:09 — MACH 1",
            "T+1:14 — STAGE-1B ABORT MODE",
            "T+2:30–2:35 — MECO",
            "T+2:35–2:39 — STAGE SEPARATION",
            "T+2:36–2:47 — S2 IGNITION",
            "T+4:20–8:43 — SECO-1 / ORBIT INSERTION",
            "T+9:00–12:02 — DRAGON SEPARATION",
            "T+12:48–13:23 — NOSE-CONE OPEN"
        };

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f; if (ox < 0f) ox = 0f;
            float X(float x) => x * sc + ox;
            float Y(float y) => y * sc;
            float Z(float v) => v * sc;
            void LN(float x0, float y0, float x1, float y1, Rgba c) =>
                dl.Line(X(x0), Y(y0), X(x1), Y(y1), Z(3f), c);

            var Hull = DragonPalette.Text6;
            var Faint = DragonPalette.Text7;

            dl.Rect(0, 0, w, h, DragonPalette.Background);
            dl.Text("ASCENT / LAUNCH", w * 0.5f, Y(60), Z(44), TextAlign.Centre, DragonPalette.Accent);

            // ---- live, reused: the same PageState.Phase DockingPage already reads (§1.4) ----
            string phase = s.Valid ? (string.IsNullOrEmpty(s.Phase) ? "-" : s.Phase) : "-";
            dl.Text("ACTIVE PHASE — " + phase, X(3127f), Y(130), Z(28), TextAlign.Right, DragonPalette.Text2);

            // ---- THE STACK OUTLINE, nose (Dragon) at the top, engines (stage 1) at the base ----
            LN(CX, NoseY, CX - HWCap, ShoulderY, Hull);                 // nose cone, left
            LN(CX, NoseY, CX + HWCap, ShoulderY, Hull);                 // nose cone, right
            LN(CX - HWCap, ShoulderY, CX - HWCap, TrunkBotY, Hull);      // capsule + trunk, left
            LN(CX + HWCap, ShoulderY, CX + HWCap, TrunkBotY, Hull);      // capsule + trunk, right
            LN(CX - HWCap, TrunkBotY, CX - HWStack, TrunkBotY, Hull);    // payload/core step, left
            LN(CX + HWCap, TrunkBotY, CX + HWStack, TrunkBotY, Hull);    // payload/core step, right
            LN(CX - HWStack, TrunkBotY, CX - HWStack, BaseY, Hull);      // S2 + interstage + S1, left
            LN(CX + HWStack, TrunkBotY, CX + HWStack, BaseY, Hull);      // S2 + interstage + S1, right
            LN(CX - HWCap, CapsuleBotY, CX + HWCap, CapsuleBotY, Faint); // capsule / trunk ring
            LN(CX - HWStack, S2BotY, CX + HWStack, S2BotY, Faint);       // stage-2 / interstage ring
            LN(CX - HWStack, InterstageBotY, CX + HWStack, InterstageBotY, Faint); // interstage / S1 ring
            LN(CX - HWStack, BaseY, CX + HWStack, BaseY, Hull);          // engine mount line
            for (int i = 1; i <= 2; i++)                                 // trunk ribs
            {
                float ry = CapsuleBotY + (TrunkBotY - CapsuleBotY) * i / 3f;
                LN(CX - HWCap, ry, CX + HWCap, ry, DragonPalette.Hairline);
            }

            // ---- grid fins (near the top of stage 1) and landing legs (near the base) ----
            LN(CX - HWStack, InterstageBotY + 20f, CX - HWStack - 46f, InterstageBotY + 4f, Faint);
            LN(CX + HWStack, InterstageBotY + 20f, CX + HWStack + 46f, InterstageBotY + 4f, Faint);
            LN(CX - HWStack, BaseY - 60f, CX - HWStack - 70f, BaseY, Faint);
            LN(CX + HWStack, BaseY - 60f, CX + HWStack + 70f, BaseY, Faint);

            // ---- Octaweb engine ticks along the base ----
            for (int i = -2; i <= 2; i++)
                LN(CX + i * 40f, BaseY, CX + i * 40f, BaseY + 26f, Faint);

            // ---- section labels — real Falcon 9 / Dragon public facts, left of the stack ----
            void SectionLabel(string t, float y) =>
                dl.Text(t, X(CX - HWCap - 20f), Y(y), Z(24), TextAlign.Right, Faint);
            SectionLabel("DRAGON", 400f);
            SectionLabel("TRUNK", 590f);
            SectionLabel("STAGE 2 — 1 MERLIN VACUUM", 900f);
            SectionLabel("STAGE 1 — 9 MERLIN 1D", 1500f);

            // ---- the 11 real §8 events, called out against the stack ----
            for (int i = 0; i < EventY.Length; i++)
            {
                float ty = EventY[i], hw = HalfWidth(ty);
                LN(CX + hw, ty, CX + hw + 40f, ty, DragonPalette.Accent);
                dl.Text(EventText[i], X(CX + hw + 50f), Y(ty), Z(26), TextAlign.Left, DragonPalette.Text2);
            }

            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame
        }
    }
}
