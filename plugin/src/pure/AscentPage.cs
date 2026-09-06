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
        // S159 adds 11 event-state markers, one Rect each. MEASURED at 2560x1406 by the preview:
        // 64 commands with a live feed, 53 with none (a dead feed draws no marker at all). 100 stands.
        public const int Commands = 120;   // +BottomBar.Commands (S176: the bar is 19 commands, not 2)
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

        // ==================== S159 / S49 H34 / QC AS-01: THE ELEVEN EVENTS ARE MARKED ====================
        // All eleven drew in ONE tint, none marked passed, current or pending — while `pure/StepList.cs`,
        // a 15-row machine that resolves most of them off real vessel state, ran unread because it draws
        // only through `Pages.Build` and is stranded behind `FigmaMode` (S49 §1.1). QC AS-01: *"the state
        // to mark them with is in the fixture, ON THIS FRAME … by those five fields alone, six of the
        // eleven events have demonstrably occurred and the page marks none."*
        //
        // ⭐ THIS IS A HARVEST AND IT NEEDED NO PLUMBING AT ALL. `Build` already takes `PageState`, and
        // `PageState.Steps` IS the `StepInputs` that machine reads (`Pages.cs:165`, filled at
        // `VesselData.cs:410`). Nothing new is computed here, no new parameter, no new source.
        //
        // ⛔ THE T+ TIMES ARE NOT TOUCHED. They are tier-1 reference copy — [[T12]]'s own register line,
        // "all 11 T+ events transcribed verbatim" — and QC says so again: *"step tracking marks which have happened;
        // it does not change when they are printed to happen."* Only the TINT of each row changes.

        /// <summary>The event has a step of its own in <see cref="StepList"/>, or -1 if it has not.</summary>
        const int NoStep = -1;

        /// <summary>Which `StepList` row each of the eleven events is, in the same order as
        /// <c>EventText</c>. Three of them have no row and no observer anywhere — see EventMark.</summary>
        static readonly int[] EventStepIdx = {
            (int)StepId.Liftoff,      // LIFTOFF                    — the clamps letting go
            NoStep,                   // T+0:10 PITCH KICK          — nothing observes a pitch program
            (int)StepId.MaxQ,         // T+1:00 MAX-Q               — a latched peak detector
            NoStep,                   // T+1:09 MACH 1              — no Mach anywhere in StepInputs
            NoStep,                   // T+1:14 STAGE-1B ABORT MODE — see the note below
            (int)StepId.Meco,         // T+2:30–2:35 MECO
            (int)StepId.StageSep,     // T+2:35–2:39 STAGE SEPARATION
            NoStep,                   // T+2:36–2:47 S2 IGNITION    — not a StepId, but `S2Lit` IS an input
            (int)StepId.Seco,         // T+4:20–8:43 SECO-1
            (int)StepId.DragonSep,    // T+9:00–12:02 DRAGON SEPARATION
            (int)StepId.NoseConeOpen  // T+12:48–13:23 NOSE-CONE OPEN
        };

        /// <summary>S2 IGNITION's index. It is the one event sourced from a raw `StepInputs` FIELD rather
        /// than from a `StepList` row — QC counts `ps.Steps.S2Lit` among the five fields that prove six
        /// events have occurred, and it is already wired (`VesselData.cs:445`). ⚠ `S2Lit` is true only
        /// WHILE the engine burns, so on its own it would un-happen at SECO; the ordering rule below is
        /// what keeps it passed afterwards.</summary>
        const int S2IgnitionEvent = 7;

        /// <summary>Where the ascent has got to, per event. Three states, which is what the page can
        /// honestly distinguish.</summary>
        public enum EventMark : byte { Pending, Current, Passed }

        static readonly StepRow[] stepScratch = new StepRow[(int)StepId.Count];
        static readonly bool[] passedScratch = new bool[11];
        static readonly EventMark[] markScratch = new EventMark[11];

        /// <summary>
        /// Mark every event passed / current / pending from the LIVE step machine. Returns the number of
        /// marks written, and <b>0 when nothing is known</b> — a dead feed does not get eleven confident
        /// "pending"s, it gets no marking at all, which is what the caller draws.
        ///
        /// ---- THREE EVENTS HAVE NO OBSERVER, AND ORDERING IS WHAT ANSWERS THEM HONESTLY ----
        /// PITCH KICK, MACH 1 and STAGE-1B ABORT MODE are resolved by nothing in this build:
        /// • a PITCH KICK is a steering event and there is no steering state to read;
        /// • MACH 1 would need a speed of sound. `StepInputs` has none, and `PageState.SurfaceVelocityMps`
        ///   is not a Mach number — turning one into the other is a new simulation, which C1.15 gates
        ///   behind a documented mod-first search, and this line is a harvest;
        /// • ⛔ STAGE-1B ABORT MODE is the one that looks easy and is not. `StepList.AbortMode()` returns
        ///   MODE 1 / 2 / 3 during first-stage flight, and its own header says *"the actual boundary
        ///   conditions are NOT public … where each one starts is OURS. Do not cite these numbers as
        ///   SpaceX's."* Declaring our MODE 2 boundary to BE the real 1B call at T+1:14 is exactly that
        ///   citation. So it is not mapped.
        ///
        /// ⭐ Instead they are resolved by the ORDER THE PAGE ITSELF PRINTS THEM IN. `EventText` is
        /// chronological — the file's own comment says so, and the T+ figures beside each one are tier-1
        /// reference times. So if a LATER event has been observed, every earlier one is behind us: MAX-Q
        /// observed at T+1:00 puts the T+0:10 pitch kick in the past. That is a deduction from the page's
        /// printed timeline, not an invented reading, and it is the ONLY inference made here.
        /// ⛔ It runs BACKWARD ONLY. A passed event implies its predecessors; it never implies a successor.
        ///
        /// The remainder falls out with no fourth state: the first event that is not passed is the one the
        /// ascent has REACHED, so it is CURRENT — including the unobserved ones, for which "we are at this
        /// milestone and it is not confirmed done" is exactly the true statement.
        /// </summary>
        public static int Marks(PageState s, EventMark[] into)
        {
            int n = EventStepIdx.Length;
            if (into == null || into.Length < n) return 0;
            if (!s.Valid) return 0;

            int rows = StepList.Build(s.Steps, stepScratch);
            if (rows == 0) return 0;

            bool[] p = passedScratch;
            for (int i = 0; i < n; i++)
            {
                int idx = EventStepIdx[i];
                p[i] = idx != NoStep && StateOf(stepScratch, rows, (StepId)idx) == StepState.Done;
            }
            if (s.Steps.S2Lit) p[S2IgnitionEvent] = true;

            for (int i = n - 2; i >= 0; i--) if (p[i + 1]) p[i] = true;

            bool tookCurrent = false;
            for (int i = 0; i < n; i++)
            {
                if (p[i]) into[i] = EventMark.Passed;
                else if (!tookCurrent) { into[i] = EventMark.Current; tookCurrent = true; }
                else into[i] = EventMark.Pending;
            }
            return n;
        }

        /// <summary>A row's state, found by its Id rather than by its index. `StepId`'s own note —
        /// "order must not be reshuffled" — protects the ACKNOWLEDGEMENT BITMASK, not this array, so
        /// indexing `stepScratch` by `(int)StepId` would be leaning on a promise nobody made.</summary>
        static StepState StateOf(StepRow[] rows, int n, StepId id)
        {
            for (int i = 0; i < n; i++) if (rows[i].Id == id) return rows[i].State;
            return StepState.Pending;
        }

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
            string phase = s.Valid ? (string.IsNullOrEmpty(s.Phase) ? Dashes.None : s.Phase) : Dashes.None;
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
            // ---- ...and since S159, EACH ONE MARKED passed / current / pending ----
            // ⛔ THE COLOURS ARE THE BUILD'S EXISTING STEP LANGUAGE, NOT A NEW ONE. `Pages.StepColumn`
            // — the stranded surface this page is harvesting from — already says done / active / pending
            // as Go / Accent / dim, and [[S158]] made "not yet" `Text6` on the Suit Leak Check's ticks
            // the same day. One vocabulary for step state across every page that has one; a second would
            // be the C7.1 failure ([[S149]]) built on purpose.
            // ⚠ Pending uses `Text6`, not `StepColumn`'s `Text7`. Text7 is #585D7C on a #020738 ground,
            // which is very dark for a 26 px label on a page [[S153]] (R-01) already reports as under the
            // legibility floor. Text6 is the same "not yet" tint S158 used and is the brighter of the two.
            // ⚠ A DEAD FEED IS NOT ELEVEN PENDINGS. `Marks` returns 0, and everything then draws in one
            // dim tint with no marker at all: the page marks nothing because it knows nothing, rather
            // than asserting that no event has happened yet. Same rule as every dashed readout here.
            EventMark[] marks = markScratch;
            int nm = Marks(s, marks);
            for (int i = 0; i < EventY.Length; i++)
            {
                float ty = EventY[i], hw = HalfWidth(ty);
                Rgba mark, lead, label;
                if (nm == 0)                            { mark = Faint;                lead = Faint;                label = Faint; }
                else if (marks[i] == EventMark.Passed)  { mark = DragonPalette.Go;     lead = Hull;                 label = DragonPalette.Text2; }
                else if (marks[i] == EventMark.Current) { mark = DragonPalette.Accent; lead = DragonPalette.Accent; label = DragonPalette.Accent; }
                else                                    { mark = DragonPalette.Text8;  lead = DragonPalette.Text8;  label = DragonPalette.Text6; }

                LN(CX + hw, ty, CX + hw + 40f, ty, lead);
                if (nm != 0) dl.Rect(X(CX + hw + 46f), Y(ty - 8f), Z(16f), Z(16f), mark);
                dl.Text(EventText[i], X(CX + hw + 74f), Y(ty), Z(26), TextAlign.Left, label);
            }

            BottomBar.Draw(dl, w, h, s, BarFit.Frame);   // S103: undistorted, in the design frame; S147: CURRENT STATE live
        }
    }
}
