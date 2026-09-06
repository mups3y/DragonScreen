// DragonScreen — Frame58Controls  (PURE: Frame 58's bottom row — FRAME, CAMERA and the timer)
// ============================================================================================
// [[S132]] / audit H11, 2026-09-06. Three things were baked into `frame58.png` with nothing behind
// them: `FRAME LVLH`, `CAMERA Virtual`, and a `0s` timer with RESET and START buttons that had no hit
// rect at all. This module is what they read and what pressing them means; `Frame58Hud` draws it and
// the painter owns the clock.
//
// ---- ⭐ FRAME IS A CONSTANT, AND THAT IS THE ANSWER RATHER THAN A DODGE ----
// LVLH is the frame this HUD works in. Nothing in the vessel, the snapshot or the reference offers a
// second one: `DockingPageCentral` draws the same literal, `SCREEN_EVIDENCE_MATRIX` records the row as
// "FRAME: LVLH", and there is no frame SELECTOR anywhere in the reference UI. §14.4(f) asks for a real
// readout to be FILLED, not for a constant to be made to wobble — so the baked `LVLH` is correct, it
// is left exactly as it is, and this comment is the record of that being checked rather than assumed.
// ⛔ Do not "make FRAME live". There is nothing to make it live FROM.
//
// ---- ⚠ CAMERA IS LIVE, AND ONE OF ITS TWO WORDS IS OURS ----
// The row says which view is in the bowl, and Frame 58 has exactly two: the synthetic attitude bowl the
// frame draws itself, and the live docking-camera feed that replaces it when the nose cone opens
// (`Frame58Hud.Build` switches on `s.Steps.NoseConeOpen`, and this reads the SAME flag, so the label
// and the picture cannot disagree).
//   "Virtual"  — the reference's own word for the synthetic bowl, tier 1.
//   "Forward"  — ⚠ OURS. No source names the second view: the reference only ever captured the frame in
//                its Virtual state. The word is not invented here either, though — it is the one this
//                build already puts in front of a crew for this exact camera, in the Video tab's
//                "FORWARD VIEW IN USE BY DOCKING". Using our own established term beats coining a new
//                one, and beats leaving the label reading "Virtual" over a live camera, which would be
//                the one thing that is definitely false. Recorded on [[S132]] as the single word in this
//                row that is not from the reference.
//
// PURE: no KSP/Unity and no clock of its own — the caller owns the elapsed time, which is what makes
// the whole of this headless-testable.
// ============================================================================================
namespace DragonScreen
{
    /// <summary>What a press on Frame 58's timer means.</summary>
    public enum TimerAct : byte { None = 0, StartStop = 1, Reset = 2 }

    public static class Frame58Controls
    {
        /// <summary>The frame this HUD works in. A constant — see the header before changing it.</summary>
        public const string Frame = "LVLH";

        /// <summary>The synthetic attitude bowl the frame draws itself. The reference's own word.</summary>
        public const string CameraVirtual = "Virtual";

        /// <summary>The live docking-camera feed. ⚠ Our word, not the reference's — see the header.</summary>
        public const string CameraForward = "Forward";

        /// <summary>
        /// Which view the bowl is showing. ⛔ Reads `s.Steps.NoseConeOpen`, the SAME flag
        /// `Frame58Hud.Build` switches the bowl on, so the label cannot contradict the picture beside
        /// it — the defect QC `H-02` is made of, one row further down the page.
        /// </summary>
        public static string CameraText(PageState s)
        {
            return s.Steps.NoseConeOpen ? CameraForward : CameraVirtual;
        }

        /// <summary>
        /// The timer's own reading. Whole seconds with an `s`, which is the baked format (`0s`).
        ///
        /// ⚠ NEGATIVE AND NaN BOTH READ AS ZERO rather than printing something impossible. A stopwatch
        /// that has not run has run for no time; a stopwatch showing "-3s" is a bug wearing a readout.
        /// </summary>
        public static string TimerText(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || elapsedSeconds <= 0.0) return "0s";
            long whole = (long)elapsedSeconds;
            return whole + "s";
        }

        /// <summary>Which timer control a touch at design-frame (x, y) hit. ⛔ Against the BAKED button
        /// plates (`Frame58Map.ResetButton` / `StartButton`), because the artwork is the button and
        /// nothing redraws it — a hit rect that did not match it would be QC `H-04`.</summary>
        public static TimerAct HitTest(float dx, float dy)
        {
            if (In(Frame58Map.StartButton, dx, dy)) return TimerAct.StartStop;
            if (In(Frame58Map.ResetButton, dx, dy)) return TimerAct.Reset;
            return TimerAct.None;
        }

        static bool In(Frame58Map.Box b, float x, float y)
        { return x >= b.X0 && x <= b.X1 && y >= b.Y0 && y <= b.Y1; }

        /// <summary>
        /// What START/STOP does to the running flag. Split out so the painter holds state and no
        /// decision — press it while stopped and it runs, press it while running and it stops.
        /// </summary>
        public static bool Toggle(bool running) { return !running; }

        /// <summary>
        /// The elapsed time after a RESET. ⭐ Zero, and the run STOPS: a reset that left the clock
        /// running would start counting from zero again, which is a restart wearing a reset's label.
        /// </summary>
        public const double ResetElapsed = 0.0;
        public const bool ResetRunning = false;
    }
}
