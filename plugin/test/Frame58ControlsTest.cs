// Tests for pure/Frame58Controls.cs — [[S132]] / audit H11: Frame 58's FRAME, CAMERA and timer.
//
// ⛔ THE DONE-CRITERION THIS FILE CARRIES: "`FAR FIELD POSITIONING` is left inert with a comment naming
// it (B), and a test pins that it reaches no `FlightCommands`." That is answered the same way [[S128]]
// answered it for the Cover — by the shape of what a press can produce. `TimerAct` has three values,
// all of them local to a stopwatch, and there is no path from this page's touch handling to anything
// else. FAR FIELD POSITIONING has no hit rect at all, and this suite pins that it still has none.
using System;
using DragonScreen;

public static class Frame58ControlsTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }

    public static int Run()
    {
        Console.WriteLine("DragonScreen Frame 58 bottom row (S132 / H11)");
        checks = 0; failures = 0;

        // ---- CAMERA reads the bowl, and reads the SAME flag the bowl is switched on --------------
        PageState closed = new PageState(); closed.Valid = true;
        PageState open = new PageState(); open.Valid = true;
        open.Steps.NoseConeOpen = true;

        Check("S132 nose cone CLOSED reads the reference's own word for the synthetic bowl",
              Frame58Controls.CameraText(closed) == "Virtual",
              "got '" + Frame58Controls.CameraText(closed) + "'");
        Check("S132 nose cone OPEN stops saying Virtual over a live camera",
              Frame58Controls.CameraText(open) != "Virtual",
              "still '" + Frame58Controls.CameraText(open) + "'");
        Check("S132 ...and says which camera it is",
              Frame58Controls.CameraText(open) == "Forward",
              "got '" + Frame58Controls.CameraText(open) + "'");

        // ⭐ THE POINT OF THE ABOVE: the label and the picture come from ONE flag. `FigmaUI.WantsDockingCam`
        // is the same predicate the painter uses to claim the camera, so if these ever disagree the row
        // is lying about what is in front of the crew.
        Check("S132 the label follows the same flag that puts the camera in the bowl",
              (Frame58Controls.CameraText(open) != Frame58Controls.CameraText(closed))
              && FigmaUI.WantsDockingCam(UiPage.Hud, open)
              && !FigmaUI.WantsDockingCam(UiPage.Hud, closed),
              "camera-claim and label disagree");

        // ---- FRAME is a constant, and that is asserted rather than assumed --------------------
        Check("S132 FRAME is LVLH", Frame58Controls.Frame == "LVLH", Frame58Controls.Frame);

        // ---- the stopwatch's reading -------------------------------------------------------------
        Check("S132 a stopwatch that has not run reads 0s", Frame58Controls.TimerText(0.0) == "0s",
              Frame58Controls.TimerText(0.0));
        Check("S132 whole seconds, with the baked unit", Frame58Controls.TimerText(7.9) == "7s",
              Frame58Controls.TimerText(7.9));
        Check("S132 a long run does not wrap or go scientific",
              Frame58Controls.TimerText(3661.0) == "3661s", Frame58Controls.TimerText(3661.0));
        // ⚠ a stopwatch showing "-3s" is a bug wearing a readout
        Check("S132 a negative elapsed reads 0s, never a negative time",
              Frame58Controls.TimerText(-3.0) == "0s", Frame58Controls.TimerText(-3.0));
        Check("S132 NaN reads 0s too", Frame58Controls.TimerText(double.NaN) == "0s",
              Frame58Controls.TimerText(double.NaN));

        // ---- START/STOP and RESET mean what their labels say --------------------------------------
        Check("S132 START on a stopped clock starts it", Frame58Controls.Toggle(false), "");
        Check("S132 ...and pressing it again stops it", !Frame58Controls.Toggle(true), "");
        Check("S132 RESET zeroes the clock", Frame58Controls.ResetElapsed == 0.0,
              "" + Frame58Controls.ResetElapsed);
        // ⭐ a reset that left it running would be a restart wearing a reset's label
        Check("S132 ...and STOPS it, rather than restarting from zero",
              Frame58Controls.ResetRunning == false, "");

        // ---- ⛔ THE HIT RECTS ARE THE BAKED BUTTONS ----------------------------------------------
        Check("S132 the centre of the baked RESET plate hits Reset",
              Frame58Controls.HitTest(Frame58Map.ResetButton.Cx, Frame58Map.ResetButton.Cy) == TimerAct.Reset,
              "got " + Frame58Controls.HitTest(Frame58Map.ResetButton.Cx, Frame58Map.ResetButton.Cy));
        Check("S132 the centre of the baked START plate hits StartStop",
              Frame58Controls.HitTest(Frame58Map.StartButton.Cx, Frame58Map.StartButton.Cy) == TimerAct.StartStop,
              "got " + Frame58Controls.HitTest(Frame58Map.StartButton.Cx, Frame58Map.StartButton.Cy));
        Check("S132 the two plates do not overlap",
              Frame58Map.ResetButton.X1 < Frame58Map.StartButton.X0,
              "reset ends " + Frame58Map.ResetButton.X1 + ", start begins " + Frame58Map.StartButton.X0);
        Check("S132 the gap between them is inert — a tap there presses nothing",
              Frame58Controls.HitTest((Frame58Map.ResetButton.X1 + Frame58Map.StartButton.X0) * 0.5f,
                                      Frame58Map.ResetButton.Cy) == TimerAct.None, "");
        Check("S132 a tap far from the timer is inert",
              Frame58Controls.HitTest(100f, 100f) == TimerAct.None, "");

        // ⚠ and the plates must sit inside the frame, or they are being hit somewhere off the art
        Check("S132 both plates are inside the design frame",
              Frame58Map.ResetButton.X0 > 0f && Frame58Map.StartButton.X1 < Frame58Map.RefW
              && Frame58Map.ResetButton.Y0 > 0f && Frame58Map.StartButton.Y1 < Frame58Map.RefH, "");

        // ---- ⛔ FAR FIELD POSITIONING IS (B) AND STAYS INERT --------------------------------------
        // §14.4(a): it is a GNC MODE COMMAND, so it belongs to Part B and must not become pressable
        // here. `Frame58Controls.HitTest` is the ONLY hit test this page has, and it answers with a
        // `TimerAct` — a type whose three values are a stopwatch's. So there is no value this page's
        // touch handling can produce that names a flight command, and the button's own region answers
        // None. `FAR FIELD POSITIONING` sits around x 2400-3300, y 250-330 in the design frame.
        Check("S132 FAR FIELD POSITIONING has no hit rect — it is (B), Part B's",
              Frame58Controls.HitTest(2850f, 290f) == TimerAct.None,
              "got " + Frame58Controls.HitTest(2850f, 290f));
        int acts = 0;
        foreach (TimerAct a in Enum.GetValues(typeof(TimerAct))) acts++;
        Check("S132 the page's only press type has exactly three values, all of them a stopwatch's",
              acts == 3, "got " + acts);

        Console.WriteLine("  " + checks + " checks, " + failures + " failed"
                          + "   (FRAME constant, CAMERA live, timer local; FAR FIELD still inert)");
        return failures;
    }
}
