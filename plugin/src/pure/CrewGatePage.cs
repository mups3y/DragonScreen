// DragonScreen — CrewGatePage  (PURE: "4.100 — Mission Sequence", the crew's Go/No-Go procedure)
// ============================================================================================
// Register S213. The conductor's crew gates, drawn in the REAL DRAGON PROCEDURE GRAMMAR, because the
// gates ARE procedures and this build already has two of the real ones to copy from.
//
// ---- 🟢 THE OWNER'S AUTHORITY FOR INVENTING THIS AT ALL (§1.4, C1.12's evidentiary standard) ----
// Owner, 2026-09-07, in chat, verbatim:
//     "we are supposed to simulate/create whatever is missing. We do not have the full dragon crew
//      screens space x has because they have not been released to the public. So we need to invent what
//      is not readily available the best way we can. We will have to make educated guesses based on
//      space x behaviour/procedures so it feels like it fits not shoe horned in place"
// and, settling this page's two open choices:
//     "one page that re-titles itself, and hands-off gates for flight 1. First run I will check gates
//      manually to make sure it works, also put an AutoAdvanceGates check box on the first page so the
//      user can choose to manually check the gates or auto sequence the launch"
//
// ---- ⭐ WHY THIS IS A PROCEDURE AND NOT AN "AUTOPILOT" SCREEN ----
// Crew Dragon is autonomous. The crew do not engage an autopilot — no real Dragon screen has such a
// control, and putting one on the glass would be exactly the "shoe horned" thing the ruling forbids.
// What the crew DO have is numbered PROCEDURES and Go/No-Go polls, and this build already carries two
// of them, both reconstructed from tier-1 photographs:
//     `pure/VrioTestPage.cs`   "4.700 Deorbit Preparation — Test VRIO Health LEDs"
//     `pure/SuitCheckPage.cs`  "4.011 - Suit Leak Check / ECLSS"
// 4.011's own grammar — photographed, not guessed — is the grammar copied here: a procedure ID and
// system top-left, a left rail of numbered SECTIONS with tick marks, a centre panel headed
// "SECTION n: IN PROGRESS" over numbered steps, an INITIATE action top-right, live per-row status
// going green "Nominal", a FINISH at the foot of the steps, and a red HALT bottom-right.
//
// ⭐⭐ AND THE TWO CATALOGUES ALREADY MEET. `pure/CrewGates.cs`' gate **G2 is "SUIT LEAK CHECK"**, which
// IS procedure 4.011 — a gate that is already a built page. The gates were always procedures; only the
// screen to run them on was missing.
//
// ---- ⛔ EXACTLY WHAT IS INVENTED HERE, STATED SO IT CAN BE JUDGED (§1.4 tier 3) ----
// **ONE number and ONE title: `4.100` and `MISSION SEQUENCE`.** That is the whole of it.
//   • The 4.xxx family is procedures, grouped by hundreds — 4.0xx is ECLSS (4.011), 4.7xx is deorbit
//     (4.700). A mission-sequence procedure in a hundred of its own is the educated guess.
//   • SECTION numbers are NOT invented: they are the gates' own G-numbers, which `CrewGates` already
//     carries as G1…G15 from transcribed NASA/SpaceX callouts.
//   • STEP numbers are NOT invented: `<section>.<n>` is 4.011's own scheme, whose photographed steps
//     read 2.3, 2.4, 2.5.
//   • Every TITLE and every ITEM LABEL is the gate catalog's, untouched.
// ⚠ It is NOT claimed to BE 4.011, or to be a document SpaceX has. It is ours, in their grammar.
//
// ---- ⛔ S75: A CONTROL THAT CANNOT ACT IS NOT PAINTED AS ONE — AND EVERY CONTROL HERE ACTS ----
// `VrioTestPage`'s three command plates are drawn INERT because they command the vehicle and cannot.
// The opposite applies here: INITIATE, the item taps, GO, NO-GO, HALT and the auto-gates box all reach
// `CrewProcedureOps` for real, so they are drawn live AND carry hit rects, in the same file, from the
// same geometry — which is S108/QC H-04's rule (one rect, drawn and hit) and the reason `Layout` below
// is the single source both `Build` and `HitTest` read.
//
// PURE: no KSP, no Unity. It draws from a `PageState` and returns a `GateHitKind` for a press.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>What a press on the mission-sequence procedure means.</summary>
    public enum GateAct : byte
    {
        None = 0,
        /// <summary>INITIATE — engage the conductor. The 4.011 idiom's own top-right action.</summary>
        Initiate,
        /// <summary>A crew step ticked. `Index` says which.</summary>
        Step,
        /// <summary>GO — clear the gate. Where 4.011 has FINISH.</summary>
        Go,
        /// <summary>NO-GO — hold at this gate. (`CrewGate`: NO-GO holds, it does not cancel.)</summary>
        NoGo,
        /// <summary>HALT — disengage. Where 4.011 has its red HALT.</summary>
        Halt,
        /// <summary>The hands-off gate checkbox.</summary>
        AutoGates
    }

    /// <summary>One press.</summary>
    public struct GateAction
    {
        public GateAct Act;
        public int Index;
        public static GateAction None { get { GateAction a; a.Act = GateAct.None; a.Index = 0; return a; } }
        public static GateAction Of(GateAct k) { GateAction a; a.Act = k; a.Index = 0; return a; }
        public static GateAction StepAt(int i) { GateAction a; a.Act = GateAct.Step; a.Index = i; return a; }
    }

    /// <summary>"4.100 — Mission Sequence": the crew gate the conductor is holding at, as a procedure.</summary>
    public static class CrewGatePage
    {
        // The design space this page's family works in — VrioTestPage's own RefW/RefH, so the two
        // procedure screens share one coordinate system and one visual scale.
        const float RefW = 3427f, RefH = 2112f;

        /// <summary>⛔ THE ONE INVENTED NUMBER. See the header.</summary>
        public const string ProcedureId = "4.100";
        /// <summary>⛔ THE ONE INVENTED TITLE.</summary>
        public const string ProcedureName = "Mission Sequence";
        /// <summary>The owning system, in the 4.011 idiom's own slot ("ECLSS" there).</summary>
        public const string ProcedureSystem = "GNC";

        /// <summary>Most steps a gate can show without the rows crowding. The real catalog's biggest
        /// gate is three items; the surplus is headroom, not a promise.</summary>
        public const int MaxSteps = 8;

        // ---- layout, design space. ONE source; Build and HitTest both read it. ----
        const float ColL = 150f, ColLW = 780f;         // left rail: id + sections
        const float ColC = 1010f, ColCW = 1640f;       // centre: the running section
        const float ColR = 2740f, ColRW = 540f;        // right: caution, auto-gates, HALT
        const float HeadY = 250f;
        const float StepTop = 640f, StepPitch = 108f, StepH = 84f;

        static void InitiateRect(out float x, out float y, out float w, out float h)
        { x = ColC + ColCW - 660f; y = HeadY - 60f; w = 660f; h = 118f; }

        static void StepRect(int i, out float x, out float y, out float w, out float h)
        { x = ColC; y = StepTop + StepPitch * i; w = ColCW; h = StepH; }

        /// <summary>
        /// GO / NO-GO sit directly under the LAST STEP, not at a fixed foot.
        /// ⛔ THE FIRST LAYOUT PINNED THEM AT THE BOTTOM OF THE PAGE AND THE PREVIEW SHOWED WHY THAT
        /// IS WRONG: a three-step gate left a screen-tall gap between 7.3 and the buttons, and a poll
        /// whose decision is a thousand pixels from the thing being decided reads as an unfinished page.
        /// Following the content also means a 2-step gate and an 8-step gate are the same page.
        /// </summary>
        static void GoRect(int steps, out float x, out float y, out float w, out float h)
        { x = ColC; y = FootY(steps); w = 500f; h = 128f; }

        static void NoGoRect(int steps, out float x, out float y, out float w, out float h)
        { x = ColC + 550f; y = FootY(steps); w = 500f; h = 128f; }

        /// <summary>Where the decision row sits for a gate of this many steps. ONE source, so the
        /// drawn buttons and the hit rects cannot land in different places (S108 / QC H-04).</summary>
        static float FootY(int steps)
        {
            if (steps < 1) steps = 1;
            return StepTop + StepPitch * steps + 96f;
        }

        static void AutoRect(out float x, out float y, out float w, out float h)
        { x = ColR; y = 800f; w = ColRW; h = 220f; }

        static void HaltRect(out float x, out float y, out float w, out float h)
        { x = ColR; y = 1700f; w = ColRW; h = 128f; }

        /// <summary>The auto-gates TICK BOX inside <see cref="AutoRect"/> — the part that is pressed.</summary>
        static void AutoBoxRect(out float x, out float y, out float s)
        { x = ColR + 26f; y = 826f; s = 56f; }

        // ============================ DRAW ============================

        /// <summary>
        /// Draw the procedure for whatever gate the conductor is holding at.
        ///
        /// ⭐ IT RE-TITLES ITSELF — the owner's own choice, and the reason there is ONE page rather than
        /// fifteen: `s.GateTitle` is the heading and the gate's G-number is the SECTION, so G1…G15 all
        /// run here and only one procedure number is ever invented.
        /// </summary>
        public static void Build(DisplayList dl, int w, int h, PageState s, int gateNumber, bool autoGates)
        {
            if (dl == null || w <= 0 || h <= 0) return;

            float sx = w / RefW, sy = h / RefH;
            float PX(float x) { return x * sx; }
            float PY(float y) { return y * sy; }
            float SZ(float v) { return v * sy; }
            float St(float rs) { return Strokes.Px(rs, sy); }
            void L(string t, float x, float y, float z, Rgba c)
            { dl.Text(t, PX(x), PY(y), SZ(z), TextAlign.Left, c); }
            void R(string t, float x, float y, float z, Rgba c)
            { dl.Text(t, PX(x), PY(y), SZ(z), TextAlign.Right, c); }
            void Rule(float x0, float x1, float y, Rgba c)
            { dl.Line(PX(x0), PY(y), PX(x1), PY(y), St(2), c); }
            void Plate(float x, float y, float pw, float ph, Rgba border)
            { dl.Box(PX(x), PY(y), pw * sx, ph * sy, St(2), border); }

            bool engaged = s.AutoEngaged;
            bool active  = s.GateActive;
            int n = (s.GateItems == null) ? 0 : s.GateItems.Length;
            if (n > MaxSteps) n = MaxSteps;

            // ---- left rail: the procedure's identity, then the sections ----
            L(ProcedureId + " - " + ProcedureName, ColL, 190f, 54f, DragonPalette.Text0);
            L(ProcedureSystem, ColL, 262f, 30f, DragonPalette.Text6);
            Rule(ColL, ColL + ColLW, 320f, DragonPalette.Hairline);

            L("MISSION", ColL, 420f, 30f, DragonPalette.Accent);
            // The gate's own G-number as the section, and the gate's own title beside it. Neither is
            // this file's invention; both come out of `CrewGates`.
            string sectionNo = gateNumber > 0 ? gateNumber.ToString() : "-";
            L(sectionNo + ". " + (active ? (s.GateTitle ?? "") : "AWAITING SEQUENCE START"),
              ColL + 60f, 500f, 34f, active ? DragonPalette.Text1 : DragonPalette.Text6);
            if (active)
                dl.Asset("check", PX(ColL), PY(492f), SZ(40f), SZ(40f),
                         AllTicked(s, n) ? DragonPalette.Go : DragonPalette.Text7);

            // ---- centre: the running section ----
            string eyebrow = !engaged ? "SEQUENCE: NOT STARTED"
                           : !active  ? "SEQUENCE: RUNNING - NO CREW ACTION REQUIRED"
                           : "SECTION " + sectionNo + ": IN PROGRESS";
            L(eyebrow, ColC, 170f, 32f, engaged ? DragonPalette.Accent : DragonPalette.Text6);
            L(active ? (s.GateTitle ?? "") : ProcedureName.ToUpperInvariant(),
              ColC, HeadY, 62f, DragonPalette.Text0);

            // INITIATE / running — 4.011's own top-right action slot.
            float ix, iy, iw, ih; InitiateRect(out ix, out iy, out iw, out ih);
            Plate(ix, iy, iw, ih, engaged ? DragonPalette.Go : DragonPalette.Hairline);
            dl.Text(engaged ? "SEQUENCE RUNNING" : "INITIATE MISSION SEQUENCE",
                    PX(ix + iw * 0.5f), PY(iy + ih * 0.5f - 18f), SZ(30f), TextAlign.Centre,
                    engaged ? DragonPalette.Go : DragonPalette.Text1);

            Rule(ColC, ColC + ColCW, 420f, DragonPalette.Hairline);

            if (!engaged)
            {
                L("Press INITIATE to begin the mission sequence. The conductor holds at each",
                  ColC, 540f, 32f, DragonPalette.Text4);
                L("Go/No-Go and does not fly past one until the crew clear it.",
                  ColC, 590f, 32f, DragonPalette.Text4);
            }
            else if (!active)
            {
                L("Sequence running. No crew action is required at this step.",
                  ColC, 540f, 32f, DragonPalette.Text4);
                L(s.AutoPhase ?? "", ColC, 600f, 34f, DragonPalette.Text2);
            }
            else
            {
                // ---- the steps: <section>.<n>, the 4.011 scheme ----
                for (int i = 0; i < n; i++)
                {
                    GateItemView it = s.GateItems[i];
                    float rx, ry, rw, rh; StepRect(i, out rx, out ry, out rw, out rh);

                    L(sectionNo + "." + (i + 1), rx, ry + 20f, 30f, DragonPalette.Text6);
                    L(it.Label ?? "", rx + 130f, ry + 16f, 34f,
                      it.Checked ? DragonPalette.Text1 : DragonPalette.Text4);

                    // The status word, in 4.011's own vocabulary: a satisfied row reads "Nominal" in
                    // green. An unsatisfied AUTO row is the system still working; an unsatisfied CREW
                    // row is waiting for a finger, and says which.
                    string st = it.Checked ? "Nominal" : it.CrewActionable ? "Crew action" : "Awaiting";
                    Rgba sc = it.Checked ? DragonPalette.Go
                            : it.CrewActionable ? DragonPalette.Caution : DragonPalette.Text6;
                    R(st, rx + rw - 90f, ry + 16f, 32f, sc);

                    // The tick, and it is only a CONTROL on a crew row — an AUTO row is the system's to
                    // satisfy and a finger must not be able to fake it.
                    if (it.CrewActionable)
                        Plate(rx + rw - 76f, ry + 8f, 52f, 52f,
                              it.Checked ? DragonPalette.Go : DragonPalette.Hairline);
                    if (it.Checked)
                        dl.Asset("check", PX(rx + rw - 68f), PY(ry + 16f), SZ(36f), SZ(36f),
                                 DragonPalette.Go);

                    Rule(rx, rx + rw, ry + rh, DragonPalette.Inset1);
                }

                // GO / NO-GO. GO is live only on a fully satisfied checklist — `CrewGate.Step` refuses
                // it otherwise, so painting it live would promise something the machine declines.
                bool ready = AllTicked(s, n);
                float gx, gy, gw, gh; GoRect(n, out gx, out gy, out gw, out gh);
                Plate(gx, gy, gw, gh, ready ? DragonPalette.Go : DragonPalette.Inset2);
                dl.Text("GO", PX(gx + gw * 0.5f), PY(gy + gh * 0.5f - 22f), SZ(40f), TextAlign.Centre,
                        ready ? DragonPalette.Go : DragonPalette.Text7);

                float nx, ny, nw, nh; NoGoRect(n, out nx, out ny, out nw, out nh);
                Plate(nx, ny, nw, nh, DragonPalette.Caution);
                dl.Text("NO-GO", PX(nx + nw * 0.5f), PY(ny + nh * 0.5f - 22f), SZ(40f), TextAlign.Centre,
                        DragonPalette.Caution);

                L(ready ? "Checklist complete - crew GO releases the next phase."
                        : "Complete the checklist. GO is refused on an unsatisfied list.",
                  ColC, FootY(n) - 40f, 28f, ready ? DragonPalette.Text3 : DragonPalette.Text6);
            }

            // ---- right column: the caution card, the auto-gates choice, and HALT ----
            Plate(ColR, 420f, ColRW, 300f, DragonPalette.Hairline);
            L("Caution:", ColR + 26f, 460f, 28f, DragonPalette.Caution);
            L("The conductor holds at every", ColR + 26f, 508f, 26f, DragonPalette.Text4);
            L("gate. NO-GO holds the mission", ColR + 26f, 548f, 26f, DragonPalette.Text4);
            L("at this step - it does not", ColR + 26f, 588f, 26f, DragonPalette.Text4);
            L("cancel it. Clear it with GO", ColR + 26f, 628f, 26f, DragonPalette.Text4);
            L("when ready.", ColR + 26f, 668f, 26f, DragonPalette.Text4);

            // ⭐ THE OWNER'S CHECKBOX. His words: "so the user can choose to manually check the gates or
            // auto sequence the launch". Drawn as a real tick box because it is one.
            float ax, ay, aw, ah; AutoRect(out ax, out ay, out aw, out ah);
            Plate(ax, ay, aw, ah, autoGates ? DragonPalette.Accent : DragonPalette.Hairline);
            float bx, by, bs; AutoBoxRect(out bx, out by, out bs);
            Plate(bx, by, bs, bs, autoGates ? DragonPalette.Accent : DragonPalette.Text6);
            if (autoGates)
                dl.Asset("check", PX(bx + 8f), PY(by + 8f), SZ(bs - 16f), SZ(bs - 16f),
                         DragonPalette.Accent);
            L("AUTO-SEQUENCE GATES", ax + 100f, ay + 22f, 28f,
              autoGates ? DragonPalette.Accent : DragonPalette.Text2);
            L(autoGates ? "The sequence clears its own" : "The crew work every gate",
              ax + 26f, ay + 96f, 26f, DragonPalette.Text4);
            L(autoGates ? "gates. The crew are OUT of" : "by hand. This is the real",
              ax + 26f, ay + 134f, 26f, DragonPalette.Text4);
            L(autoGates ? "the loop." : "operating concept.",
              ax + 26f, ay + 172f, 26f, DragonPalette.Text4);

            // ---- the bottom bar, spread with this page's own body (S103 / S147) ----
            // ⛔ `BarFit.Stretch` because the body above is drawn with `sx = w/RefW`. `BottomBar.FitFor`
            // is the ONE table that answers this, and `FigmaUINavTest.BarFollowsItsPage` renders this
            // page and measures the tile it actually drew against the hit map — so the two cannot drift.
            // The PageState overload is the one that matters: CURRENT STATE is live, not a picture.
            BottomBar.Draw(dl, w, h, s, BarFit.Stretch);

            float hx, hy, hw, hh; HaltRect(out hx, out hy, out hw, out hh);
            Plate(hx, hy, hw, hh, engaged ? DragonPalette.Alarm : DragonPalette.Inset2);
            dl.Text("HALT SEQUENCE", PX(hx + hw * 0.5f), PY(hy + hh * 0.5f - 18f), SZ(30f),
                    TextAlign.Centre, engaged ? DragonPalette.Alarm : DragonPalette.Text7);
        }

        /// <summary>Is every row of the current gate satisfied? The same question `CrewGate.AllSatisfied`
        /// asks of the machine — asked here of the VIEW, so the paint and the machine cannot disagree.</summary>
        public static bool AllTicked(PageState s, int n)
        {
            if (s.GateItems == null) return false;
            for (int i = 0; i < n && i < s.GateItems.Length; i++)
                if (!s.GateItems[i].Checked) return false;
            return n > 0;
        }

        // ============================ HIT ============================

        /// <summary>
        /// A press, in PAGE pixels. ⛔ Reads the SAME rects `Build` draws — S108 / QC H-04's rule, and the
        /// reason every rect above is a named method rather than a literal in two places.
        /// </summary>
        public static GateAction HitTest(float px, float py, int w, int h, PageState s)
        {
            if (w <= 0 || h <= 0) return GateAction.None;
            float sx = w / RefW, sy = h / RefH;
            bool In(float x, float y, float bw, float bh)
            { return Control.Hit(px, py, x * sx, y * sy, bw * sx, bh * sy); }

            bool engaged = s.AutoEngaged;

            float x0, y0, w0, h0;

            // The auto-gates choice is reachable whether or not the sequence is running — it is the
            // crew's standing preference, not a step.
            AutoRect(out x0, out y0, out w0, out h0);
            if (In(x0, y0, w0, h0)) return GateAction.Of(GateAct.AutoGates);

            HaltRect(out x0, out y0, out w0, out h0);
            if (engaged && In(x0, y0, w0, h0)) return GateAction.Of(GateAct.Halt);

            InitiateRect(out x0, out y0, out w0, out h0);
            if (!engaged && In(x0, y0, w0, h0)) return GateAction.Of(GateAct.Initiate);

            if (!s.GateActive) return GateAction.None;

            int n = (s.GateItems == null) ? 0 : s.GateItems.Length;
            if (n > MaxSteps) n = MaxSteps;
            for (int i = 0; i < n; i++)
            {
                // ⛔ ONLY A CREW ROW TAKES A PRESS. An AUTO row is satisfied from vessel state, and a
                // finger that could tick it would be faking a system confirmation.
                if (!s.GateItems[i].CrewActionable) continue;
                StepRect(i, out x0, out y0, out w0, out h0);
                if (In(x0, y0, w0, h0)) return GateAction.StepAt(i);
            }

            GoRect(n, out x0, out y0, out w0, out h0);
            if (In(x0, y0, w0, h0)) return GateAction.Of(GateAct.Go);

            NoGoRect(n, out x0, out y0, out w0, out h0);
            if (In(x0, y0, w0, h0)) return GateAction.Of(GateAct.NoGo);

            return GateAction.None;
        }
    }
}
