// DragonScreen — SuitCheckPage  (PURE: the "4.011 Suit Leak Check", ECLSS procedure)
// ============================================================================================
// Rebuilt to the REAL Crew Dragon page, corrected against full-res capsule photographs (Discovery
// "Inside SpaceX's Crew Dragon Capsule", discovery2 = in-progress, discovery3 = completion popup).
// The reference-demo (neel-dandiwala/SpaceX-Dragon2-UI) gave the layout, but its copy was a paraphrase;
// the real wording is used here. Three columns: LEFT = title "4.011 - Suit Leak Check" / "ECLSS" + the
// two-step checklist + read-only controls; MAIN = "SECTION 2: IN PROGRESS · Execute Suit Leak Check",
// step 2.3 + INITIATE, step 2.4, and the table (TIME REMAINING IN LEAK CHECK + four SUIT n DELTA
// PRESSURE + four SUIT n STATUS = "Nominal") + FINISH; RIGHT = the caution note + HALT.
//
// FLOW: INITIATE runs a 5→0 countdown (~0.9s/step); at 0 the run's RESULT POPUP shows — the completion
// box ("4.011 - Suit Leak Check / ECLSS / CLEAR / PROCEDURE COMPLETE / Crew can open their visors if
// desired but must not open zippers or disconnect umbilical.") on a clean run, or, on the 5% run that
// finds a leak, the same box carrying the leak result and "Repair suit and rerun suit check." (S31, our
// own copy for our own simulated feature). HALT stops + resets. FINISH ends the procedure early and
// raises the same popup (step 2.5's own completion control). TRY ADDITIONAL TIMER re-runs the countdown
// and RE-ROLLS — which is what a second timed run of a leak check is. TROUBLESHOOT (S32) is the fail
// branch's own control: it lights ONLY while a suit is reading "Failed Low", and pressing it REPAIRS
// that suit and re-runs the check on a fresh roll — the recovery the leak result box itself instructs
// ("Repair suit and rerun suit check."), taken through that same re-run path rather than a second one.
//
// TIME REMAINING counts the real procedure countdown. The four SUIT n DELTA PRESSURE rows and the four
// SUIT n STATUS words are a MARKED SIMULATION (S31, BUILD_PLAN §14.4(e)): the differential is measured
// against the REAL, TAC-LS-driven cabin pressure, so those rows move when the cabin moves, and STATUS is
// a verdict COMPUTED from that differential rather than a green word printed unconditionally.
// pure/SuitLeakSim.cs is the model and states what in it is real, what is simulated and what is rolled.
// With no vessel feed the whole table dashes, exactly as any other unsourced readout does.
//
// ---- WHAT IS REFERENCE AND WHAT IS RECONSTRUCTION, ON THIS PAGE ----
// The popup's "CLEAR" line, the Failed-Low / TROUBLESHOOT / TRY ADDITIONAL TIMER block and step 2.5
// ("On completion, contact SpaceX to report results") appear in NO real frame. They are drawn anyway,
// and deliberately: BUILD_PLAN §14.4(d) is the owner's decision on exactly this content — KEEP it,
// MARKED as a reconstruction, because a leak check must have a fail path and the real table plainly
// scrolls past its "Scroll to continue" fold. (An earlier version of this header claimed the opposite,
// that none of it is drawn; it was written before §14.4(d) and the code never matched it. Corrected
// here rather than left to mislead someone into deleting an owner-decided reconstruction.)
//
// S32 (owner, via the overseer, 2026-09-02) extends that decision to what TROUBLESHOOT DOES.
// RECONSTRUCTED-FROM-FUNCTION under §14.4(d)+(e), MARKED, and NOT claimed as real: there is no 4.011
// continuation frame to verify against — the real table scrolls past its "Scroll to continue" fold and
// that content is ITAR-class, so no source for it is coming. The function is read off the page's own
// instruction to the crew instead: a suit that failed is repaired, and the check is run again.
//
// Icons are referenced by asset key (ic_check/ic_dash/ic_grid/ic_refresh/ic_eye/ic_circle).
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class SuitCheckPage
    {
        public const int Commands = 200;
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba Bg     = DragonPalette.Background;
        static readonly Rgba Panel  = DragonPalette.Panel;
        static readonly Rgba Accent = DragonPalette.Accent;
        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Dim    = DragonPalette.Text6;
        static readonly Rgba Hair   = DragonPalette.Hairline;
        static readonly Rgba Amber  = DragonPalette.Caution;
        static readonly Rgba Red    = DragonPalette.Alarm;
        static readonly Rgba Go     = DragonPalette.Go;

        static readonly string[] Suit = { "SUIT 1", "SUIT 2", "SUIT 3", "SUIT 4" };
        const string Dash = "—";     // no source — never a plausible reading

        /// <summary>countdown: the SUIT PRESSURE counter (5..0); showPopup: the run's RESULT dialog is up
        /// (which of the two it is follows <paramref name="suits"/>); suits: the suit model this page
        /// reads — four differentials and the verdict they support, from pure/SuitLeakSim.cs. S31 threaded
        /// it in because until then this page took no vessel state at all and so had nothing to be honest
        /// about; the countdown is still the painter's own live procedure timer, untouched.</summary>
        public static void Build(DisplayList dl, int w, int h, int countdown, bool showPopup,
                                 SuitCheckState suits)
        {
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            float SZ(float v) => v * sy;
            int St(float rs) => Strokes.Px(rs, sy);   // ONE rule, in Strokes.cs - rounds UP (R-02 family)
            void L(string t, float x, float y, float sz, Rgba c) => dl.Text(t, PX(x), PY(y), SZ(sz), TextAlign.Left, c);
            void C(string t, float cx, float y, float sz, Rgba c) => dl.Text(t, PX(cx), PY(y), SZ(sz), TextAlign.Centre, c);
            void Ico(string k, float x, float y, float s, Rgba c) => dl.Asset(k, PX(x), PY(y), SZ(s), SZ(s), c);
            void Pl(float x, float y, float pw, float ph, Rgba border)
            { dl.Rect(PX(x), PY(y), pw * sx, ph * sy, Panel); dl.Box(PX(x), PY(y), pw * sx, ph * sy, St(3), border); }

            dl.Rect(0, 0, w, h, Bg);

            // ================= LEFT PANEL: 4.011 checklist =================
            dl.Box(PX(48), PY(96), 720 * sx, 1700 * sy, St(3), Panel);
            C("4.011 - Suit Leak Check", 408, 180, 40, White);
            C("ECLSS", 408, 244, 24, Accent);
            L("SUIT", 120, 350, 28, Accent);
            Ico("ic_check", 120, 452, 38, White); L("1. PREPARE SUITS FOR LEAK CHECK", 176, 458, 26, White);
            Ico("ic_check", 120, 560, 38, White); L("2. EXECUTE SUIT LEAK CHECK", 176, 566, 26, White);
            // read-only controls (bottom) — S29 (owner, via the overseer, 2026-09-02): both plates stay
            // INERT, drawn only, no HitTest entry. One caption, two plates: the reference does not say
            // which of ic_grid/ic_eye arms read-only mode or what the other one does, so §1.4 (inert
            // until verified) applies rather than inventing a real-only-console function for either. Not
            // wired by T14 either (outside §6's four groups). Recorded, not built; see REGISTER.md S29.
            dl.Line(PX(120), PY(1560), PX(700), PY(1560), St(2), Hair);
            // ---- S106 / QC SC-02: S29 SETTLED THE BEHAVIOUR, S75 SETTLED THE APPEARANCE ----
            // S29 (above) ruled both plates INERT. They were still drawn plate + border + full-white
            // glyph - byte-identical in idiom to INITIATE SUIT LEAK CHECK and TROUBLESHOOT on this same
            // screen, both of which act. S75 (2026-09-04, two days after S29) decided what a control that
            // does nothing must LOOK like: `Text6`, the "nothing live behind this" tint. The appearance
            // half was never applied here, so the page honoured the ruling in behaviour and contradicted
            // it in paint. It does not get a hit rect - C1.8 keeps S29 standing until the owner says
            // otherwise; only the tint changes.
            Pl(210, 1600, 130, 130, Dim); Ico("ic_grid", 245, 1635, 60, Dim);
            Pl(430, 1600, 130, 130, Dim); Ico("ic_eye", 465, 1635, 60, Dim);
            C("ENTER READ-ONLY", 408, 1770, 26, Dim);

            // ================= MAIN PANEL =================
            dl.Box(PX(820), PY(96), 2000 * sx, 1700 * sy, St(3), Panel);
            Ico("ic_refresh", 1180, 168, 34, Accent);
            L("SECTION 2: IN PROGRESS", 1230, 172, 30, Accent);
            C("Execute Suit Leak Check", 1800, 280, 62, White);

            // 2.3 + INITIATE
            L("2.3", 1060, 430, 34, White); L("On SpaceX GO to pressurize - command:", 1160, 430, 32, White);
            Pl(2300, 400, 470, 120, White);
            Ico("ic_grid", 2350, 442, 36, White); C("INITIATE SUIT LEAK CHECK", 2575, 448, 26, White);

            // 2.4 monitor
            L("2.4", 1060, 600, 34, White);
            L("Monitor suit delta pressure. Confirm all four suit", 1160, 588, 30, White);
            L("statuses are nominal after time remaining is 0.", 1160, 628, 30, White);

            // ---- the log table ----
            float tx = 1160, tvx = 2050, tix = 2560, rowH = TableRowH, y0 = TableY0;
            void Row(int i, string label, string val, string icon, Rgba iconTint, Rgba valTint)
            {
                float y = y0 + i * rowH;
                dl.Line(PX(tx), PY(y), PX(2620), PY(y), St(1), Hair);
                L(label, tx, y + 28, 30, DragonPalette.Text3);
                L(val, tvx, y + 28, 30, valTint);
                Ico(icon, tix, y + 16, 40, iconTint);
            }
            Row(0, "TIME REMAINING IN LEAK CHECK", countdown + "s", "ic_refresh", White, White);
            // ---- SUIT n DELTA PRESSURE: a MARKED simulation off the real cabin (S31, §14.4(e)) ----
            // A suit delta pressure is suit pressure minus cabin pressure. This row was "0.01psi", a
            // representative constant — and a constant sitting in a LEAK CHECK is the worst place in the
            // build for one, since four suits reading a confident 0.01 psi is exactly how a screen says
            // "no leak" while knowing nothing. T13c replaced it with a permanent dash, because nothing
            // here modelled a suit: not VehicleSystems, not CabinEnvironment, and KSP has no per-crew
            // pressure resource to stand in for one.
            //
            // §14.4(e) (owner, 2026-09-02) supersedes that dash: a real quantity that is merely
            // unmodelled goes to an installed mod's value, else to a COHERENT MARKED simulation, and a
            // dash is kept only for quantities that genuinely do not exist. No installed mod models a
            // pressure suit, so pure/SuitLeakSim.cs is the model — a regulated suit loop measured against
            // the REAL, TAC-LS-driven cabin pressure, which is why these four rows move when the cabin
            // moves rather than sitting still with a unit after them. A leaking suit bleeds down as the
            // countdown runs, so step 2.4's "monitor suit delta pressure" is something a crew can do.
            // No vessel feed at all is still a dash: an unreadable vessel has no differential to show,
            // and that is a different fact from an unmodelled one.
            for (int i = 0; i < 4; i++)
            {
                bool bad = suits.Failed(i);
                Rgba tint = !suits.Valid ? Dim : (bad ? Amber : White);
                Row(1 + i, Suit[i] + " DELTA PRESSURE",
                    suits.Valid ? SuitLeak.Text(suits.Delta(i)) : Dash,
                    suits.Valid ? "ic_refresh" : "ic_dash", tint, tint);
            }
            // ---- SUIT n STATUS: the VERDICT, and it FOLLOWS the sim (S31; §14.4(e)'s guardrail) ----
            // These four words used to be drawn green and unconditional — the page stating a safety
            // result it had no way to know, the same shape S22 fixed on the vehicle pages and could not
            // reach here (S22's dash-and-dim keys off a feed, and this page took no vessel state until
            // S31 threaded one in). Now "Nominal" can only appear while the modelled differential is
            // actually holding above SuitLeak.PassPsi; a suit that has bled below it reads "Failed Low",
            // the wording the reconstructed fail branch below already uses (§14.4(d)). Nothing in this
            // file writes a verdict in by hand, which is the whole of the §14.4(e) guardrail.
            for (int i = 0; i < 4; i++)
            {
                bool bad = suits.Failed(i);
                Rgba tint = !suits.Valid ? Dim : (bad ? Amber : Go);
                Row(5 + i, Suit[i] + " STATUS",
                    !suits.Valid ? Dash : (bad ? "Failed Low" : "Nominal"),
                    !suits.Valid ? "ic_dash" : (bad ? "ic_stop" : "ic_check"), tint, tint);
            }
            dl.Line(PX(tx), PY(y0 + 9 * rowH), PX(2620), PY(y0 + 9 * rowH), St(1), Hair);

            // 2.5 + FINISH — the procedure continues below the "Scroll to continue" fold seen in the
            // photos; this is the next step and the completion control.
            L("2.5", 1060, y0 + 9 * rowH + 44, 34, White);
            L("On completion, contact SpaceX to report results.", 1160, y0 + 9 * rowH + 44, 30, White);
            Pl(1160, FinishY, 300, 84, Hair); C("FINISH", 1310, FinishY + 24f, 30, White);

            // ================= RIGHT PANEL: caution + troubleshoot + HALT =================
            dl.Rect(PX(2870), PY(300), 500 * sx, 300 * sy, Panel);
            dl.Rect(PX(2870), PY(300), 12 * sx, 300 * sy, Amber);       // amber left rule
            L("Caution:", 2910, 350, 26, Amber);
            L("It is critical to remain still", 3010, 350, 26, White);
            L("during final 15 seconds of leak", 2910, 396, 26, White);
            L("check to ensure accurate", 2910, 442, 26, White);
            L("automated evaluation of suit", 2910, 488, 26, White);
            L("delta pressure values.", 2910, 534, 26, White);

            // ---- failure branch (reconstructed, §14.4(d) KEEP; see header note). Not in the captured
            // frames, but a leak check has a fail path and the main table scrolls past "Scroll to
            // continue" — this is the crew's response when a suit reads "Failed Low".
            //
            // T14 wired the two controls and they came out asymmetric; S32 (owner, via the overseer)
            // closed that gap. TRY ADDITIONAL TIMER always acted: "another timed run" is a complete
            // instruction and this page already owns the timer. TROUBLESHOOT now acts too, but only
            // when there is something to troubleshoot: the model has to be reading a suit below the
            // pass threshold, which S31 made possible and this control now responds to. Its action is
            // RECONSTRUCTED FROM FUNCTION (§14.4(d)+(e), marked — see the header): repair the failed
            // suit and re-run the check, which is what the result box tells the crew to do.
            //
            // The lit state and the live state are ONE question, asked once: Available() reads
            // SuitCheckState.AnyFailed, the same verdict this block draws from and the same one the
            // glue gates the press on. So a dimmed TROUBLESHOOT cannot act, a live one cannot look
            // unavailable, and with all four suits holding it is dimmed and silent exactly as before. ----
            dl.Rect(PX(2870), PY(680), 500 * sx, 640 * sy, Panel);
            L("Did any suit fail the", 2910, 730, 28, White);
            L("leak check?", 2910, 772, 28, White);
            L("A suit reading Failed Low did", 2910, 848, 24, Dim);
            L("not hold pressure.", 2910, 892, 24, Dim);
            Rgba tsCol = Available(SuitAct.Troubleshoot, suits) ? White : Dim;
            Pl(2910, 970, 420, 110, tsCol);
            Ico("ic_grid", 2950, 1004, 40, tsCol); C("TROUBLESHOOT", 3150, 1010, 28, tsCol);
            Pl(2910, 1120, 420, 110, White);
            Ico("ic_refresh", 2950, 1154, 40, White); C("TRY ADDITIONAL TIMER", 3150, 1162, 24, White);

            Pl(2900, 1600, 470, 120, Red);
            Ico("ic_grid", 2950, 1642, 36, Red); C("HALT SUIT LEAK CHECK", 3155, 1648, 30, White);

            // ================= bottom status bar =================
            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame

            // ================= the run's RESULT popup =================
            // ONE box, two outcomes. The completion box is the photographed one (discovery3) and is
            // unchanged. The LEAK box is S31's, and is deliberately the SAME box — same scrim, same
            // panel, same title/ECLSS/status-word/headline/body/close structure, same close control in
            // the same place — because a result the crew has to read is the same kind of thing whichever
            // way it went, and a second dialog style would be a second thing to trust. Only the words
            // and the status word's colour differ, and its copy is OURS (marked): it reports our own
            // simulated leak, so there is no real frame it could have come from.
            if (showPopup)
            {
                dl.Rect(0, 0, w, h, new Rgba(0.008f, 0.027f, 0.216f, 0.82f));   // #020738 scrim
                float pw = 1500, ph = 1040, px = (RefW - pw) * 0.5f, py = (RefH - ph) * 0.5f - 40;
                dl.Rect(PX(px), PY(py), pw * sx, ph * sy, Panel);
                dl.Box(PX(px), PY(py), pw * sx, ph * sy, St(3), Hair);
                float cx = RefW * 0.5f;
                C("4.011 - Suit Leak Check", cx, py + 130, 48, White);
                C("ECLSS", cx, py + 200, 26, Accent);
                if (suits.Leak)
                {
                    C("FAILED LOW", cx, py + 280, 34, Amber);
                    C("SUIT LEAK DETECTED", cx, py + 360, 44, White);
                    C("Suit " + suits.LeakSuit + " did not hold pressure.", cx, py + 470, 34, DragonPalette.Text3);
                    C("Repair suit and rerun suit check.", cx, py + 522, 34, DragonPalette.Text3);
                }
                else
                {
                    C("CLEAR", cx, py + 280, 34, Accent);
                    C("PROCEDURE COMPLETE", cx, py + 360, 44, White);
                    C("Crew can open their visors if desired but", cx, py + 470, 34, DragonPalette.Text3);
                    C("must not open zippers or disconnect", cx, py + 522, 34, DragonPalette.Text3);
                    C("umbilical.", cx, py + 574, 34, DragonPalette.Text3);
                }
                Pl(cx - 90, py + ph - 220, 180, 130, White);
                Ico("ic_circle", cx - 40, py + ph - 195, 80, White);
            }
        }

        // ---- INTERACTIVITY ----
        // START runs the countdown, HALT stops+resets, CLOSE dismisses the result popup, FINISH ends the
        // run at step 2.5 and raises that same popup, RETIME re-runs the countdown, and TROUBLESHOOT is
        // the fail branch's recovery — live only while a suit is actually reading "Failed Low" (S32; see
        // Available). The rects below are the exact ones Build draws (keep them in step). While the popup
        // is up only CLOSE is live, so a stray touch on anything behind the scrim does nothing.
        public enum SuitAct { None, Start, Halt, Close, Finish, Retime, Troubleshoot }

        /// <summary>
        /// Does the fail branch's TROUBLESHOOT control have an ACTION at all? Since S32 it does, and the
        /// two reasons it did not fell in that order. S31's marked simulation (pure/SuitLeakSim.cs) took
        /// the first: a suit CAN now bleed below the pass threshold and read "Failed Low", so the branch
        /// is no longer answering a question that answered itself. The second was that no reference says
        /// what pressing it DOES — and none ever will, because the real procedure scrolls past its
        /// "Scroll to continue" fold and that content is ITAR-class. So the owner decided it (via the
        /// overseer, 2026-09-02) the way §14.4(d) decided the branch itself: RECONSTRUCT FROM FUNCTION,
        /// marked, not claimed as real. The function comes from the page's own instruction to the crew,
        /// "Repair suit and rerun suit check." — pressing TROUBLESHOOT repairs the failed suit and
        /// re-runs the check on a fresh roll, through the path TRY ADDITIONAL TIMER already uses.
        ///
        /// This constant says the branch HAS an action; whether the control is live RIGHT NOW is
        /// Available(), which asks the model whether a suit has actually failed. It stays a named
        /// constant so that a real source contradicting any of this still has exactly one place to
        /// change, and so the test can assert the page and the hit test agree about it.
        /// </summary>
        public const bool FailBranchLive = true;

        /// <summary>True if this act does something on the state in front of the crew. TROUBLESHOOT is
        /// the conditional one: it responds to a suit that FAILED, so while all four are holding there
        /// is nothing to troubleshoot and it stays dimmed and inert exactly as it did before S32. Build
        /// lights the control from this same call, so the lit control and the live control cannot
        /// disagree; the glue asks it once more before acting, so neither can the press.</summary>
        public static bool Available(SuitAct a, SuitCheckState suits)
        {
            if (a == SuitAct.None) return false;
            if (a == SuitAct.Troubleshoot) return FailBranchLive && suits.AnyFailed;
            return true;
        }

        public static SuitAct HitTest(float px, float py, int w, int h, bool popup)
        {
            float dx = px * RefW / w, dy = py * RefH / h;
            bool In(float x, float y, float ww, float hh) => dx >= x && dx < x + ww && dy >= y && dy < y + hh;

            if (popup)
            {
                const float ph = 1040f, pTop = (RefH - ph) * 0.5f - 40f, cx = RefW * 0.5f;
                return In(cx - 90f, pTop + ph - 220f, 180f, 130f) ? SuitAct.Close : SuitAct.None;
            }
            if (In(2300f, 400f, 470f, 120f)) return SuitAct.Start;
            if (In(2900f, 1600f, 470f, 120f)) return SuitAct.Halt;
            // step 2.5's FINISH plate, and the two fail-branch plates. FinishY tracks the table the same
            // way Build's own `y0 + 9 * rowH` does, from the one pair of constants below.
            if (In(1160f, FinishY, 300f, 84f)) return SuitAct.Finish;
            if (In(2910f, 970f, 420f, 110f)) return SuitAct.Troubleshoot;
            if (In(2910f, 1120f, 420f, 110f)) return SuitAct.Retime;
            return SuitAct.None;
        }

        // The log table's origin and pitch, shared by Build and the FINISH rect above so the plate cannot
        // be hit a row away from where it is drawn.
        const float TableY0 = 720f, TableRowH = 92f;
        const float FinishY = TableY0 + 9f * TableRowH + 100f;
    }
}
