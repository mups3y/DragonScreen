// Tests for pure/Frame58Map.cs — [[S154a]]'s geometry for Figma "Frame 58", the docking HUD.
//
// ⭐ WHAT THIS FILE IS FOR, AND WHY IT IS NOT A TABLE OF RE-TYPED LITERALS. Every number in
// Frame58Map was read off `assets/figma/dashboard_ui/Frame 58.svg` and then re-measured in the
// raster we actually ship. A test that simply restated those numbers would prove only that I can
// copy. So the checks below are RELATIONS the drawing has to satisfy — the two anchors agreeing with
// constants that were derived independently and years of pixels apart, the ring containing the bowl,
// the readouts lying where the drawing's own structure says they must. A wrong box fails a relation.
//
// ⛔ S154a DRAWS NOTHING, so there is no render to check and none is attempted here.
using System;
using DragonScreen;

public static class Frame58MapTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }
    static void Near(string what, float got, float want, float tol)
    { Check(what, Math.Abs(got - want) <= tol, "got " + got.ToString("F3") + " want " + want.ToString("F3") + " tol " + tol.ToString("F3")); }

    public static int Run()
    {
        Console.WriteLine("DragonScreen Frame 58 map (S154a — geometry only, nothing drawn)");

        // ---- THE FRAME IS THE FRAME. The whole method rests on the SVG's viewBox being the design
        // frame Frame58Hud already draws in; if that ever stops being true every box below is wrong,
        // and it must fail loudly rather than silently shift 13 readouts.
        Near("S154a the map's design frame is the one Frame58Hud fits the raster to (w)", Frame58Map.RefW, 3427f, 0f);
        Near("S154a ...and (h)", Frame58Map.RefH, 2112f, 0f);
        Check("S154a the frame's aspect is the one the floor argument assumes",
              Math.Abs(Frame58Map.RefW / Frame58Map.RefH - 1.62263f) < 1e-4f,
              "aspect " + (Frame58Map.RefW / Frame58Map.RefH));

        // ---- ANCHOR 1: the bowl. ⭐ THE POINT OF THIS CHECK IS THAT THE TWO NUMBERS HAVE SEPARATE
        // PROVENANCE. Frame58Hud's 1706/984 came from the frame metadata long before S154a existed;
        // Frame58Map's came from parsing the SVG's own <circle>. They are not allowed to drift.
        Near("S154a ANCHOR 1 — the SVG bowl centre confirms Frame58Hud's BowlCx", Frame58Map.BowlCx, 1706f, 1.0f);
        Near("S154a ANCHOR 1 — ...and BowlCy", Frame58Map.BowlCy, 984f, 1.0f);

        // ---- ANCHOR 2: the ring, whose outer radius was measured in frame58.png by circle fit.
        // ⛔ 0.6 px, not "about right": the fit gave 585.33 across and 584.94 down against the SVG's
        // 585.48, and a tolerance loose enough to pass a wrong drawing would defeat the anchor.
        Near("S154a ANCHOR 2 — the shipped raster's ring radius (x-scaled) matches the SVG", Frame58Map.RingOuterR, 585.33f, 0.6f);
        Near("S154a ANCHOR 2 — ...and y-scaled", Frame58Map.RingOuterR, 584.94f, 0.6f);
        Near("S154a ANCHOR 2 — the raster's ring centre x matches the SVG's", Frame58Map.RingCx, 1704.93f, 1.5f);
        Near("S154a ANCHOR 2 — ...and its centre y", Frame58Map.RingCy, 986.22f, 1.5f);

        // ---- THE TWO ANCHORS MUST ALSO AGREE WITH EACH OTHER, which is the half neither can prove
        // alone. The bowl is a disc drawn inside the ring, so their centres are the same point to
        // within the drawing's own slack, and the ring's band is a band.
        Check("S154a the two anchors are the same centre",
              Math.Abs(Frame58Map.BowlCx - Frame58Map.RingCx) < 2.0f &&
              Math.Abs(Frame58Map.BowlCy - Frame58Map.RingCy) < 2.0f,
              "bowl (" + Frame58Map.BowlCx + "," + Frame58Map.BowlCy + ") ring (" + Frame58Map.RingCx + "," + Frame58Map.RingCy + ")");
        Check("S154a the ring band has positive thickness, inner inside outer",
              Frame58Map.RingInnerR > 0f && Frame58Map.RingInnerR < Frame58Map.RingOuterR,
              "inner " + Frame58Map.RingInnerR + " outer " + Frame58Map.RingOuterR);
        Near("S154a the ring band is 35.65 design px thick",
             Frame58Map.RingOuterR - Frame58Map.RingInnerR, 35.65f, 0.05f);

        // ---- THE READOUTS SIT WHERE THE DRAWING'S STRUCTURE PUTS THEM ---------------------------
        // ROLL above centre, YAW below, PITCH right — and each rate directly under its value. These
        // are the relations that catch a transposed or mis-assigned box, which is the realistic way
        // this table goes wrong.
        Check("S154a ROLL is above the ring centre and YAW below it",
              Frame58Map.RollValue.Cy < Frame58Map.RingCy && Frame58Map.YawValue.Cy > Frame58Map.RingCy,
              "roll " + Frame58Map.RollValue.Cy + " yaw " + Frame58Map.YawValue.Cy);
        Check("S154a PITCH is right of centre and level with it",
              Frame58Map.PitchValue.Cx > Frame58Map.RingCx + 300f &&
              Math.Abs(Frame58Map.PitchValue.Cy - Frame58Map.RingCy) < 30f,
              "pitch (" + Frame58Map.PitchValue.Cx + "," + Frame58Map.PitchValue.Cy + ")");
        Check("S154a ROLL and YAW are centred on the same vertical as the ring",
              Math.Abs(Frame58Map.RollValue.Cx - Frame58Map.RingCx) < 3f &&
              Math.Abs(Frame58Map.YawValue.Cx - Frame58Map.RingCx) < 3f,
              "roll cx " + Frame58Map.RollValue.Cx + " yaw cx " + Frame58Map.YawValue.Cx);

        // each rate is BELOW its value, and close under it — the drawing stacks them as a pair.
        CheckRatePair("ROLL", Frame58Map.RollValue, Frame58Map.RollRate);
        CheckRatePair("PITCH", Frame58Map.PitchValue, Frame58Map.PitchRate);
        CheckRatePair("YAW", Frame58Map.YawValue, Frame58Map.YawRate);

        // ---- the label names the readout, so it sits beside it ----
        Check("S154a the ROLL label sits directly above the ROLL value",
              Frame58Map.RollLabel.Y1 <= Frame58Map.RollValue.Y0 &&
              Frame58Map.RollValue.Y0 - Frame58Map.RollLabel.Y1 < 60f,
              "label ends " + Frame58Map.RollLabel.Y1 + ", value starts " + Frame58Map.RollValue.Y0);
        Check("S154a the YAW label sits directly BELOW the YAW value (the frame mirrors it)",
              Frame58Map.YawLabel.Y0 >= Frame58Map.YawRate.Y1 &&
              Frame58Map.YawLabel.Y0 - Frame58Map.YawRate.Y1 < 60f,
              "rate ends " + Frame58Map.YawRate.Y1 + ", label starts " + Frame58Map.YawLabel.Y0);
        Check("S154a the PITCH label is the VERTICAL stack — taller than it is wide, by a lot",
              Frame58Map.PitchLabel.H > 5f * Frame58Map.PitchLabel.W,
              "w " + Frame58Map.PitchLabel.W + " h " + Frame58Map.PitchLabel.H);

        // ---- X / Y / Z ARE LEFT-ALIGNED, WHICH IS A PLACEMENT RULE S154c DEPENDS ON --------------
        // ⛔ Stated as a shared LEFT EDGE, not a shared centre. If these were read as centred, three
        // live values of differing width would each drift by half their own difference.
        float lx = Frame58Map.XValue.X0;
        Check("S154a X/Y/Z share a left edge to 0.1 px",
              Math.Abs(Frame58Map.YValue.X0 - lx) < 0.1f && Math.Abs(Frame58Map.ZValue.X0 - lx) < 0.1f,
              "x0s " + lx + " / " + Frame58Map.YValue.X0 + " / " + Frame58Map.ZValue.X0);
        Check("S154a ...and they do NOT share a centre, which is why the left edge is the rule",
              Math.Abs(Frame58Map.XValue.Cx - Frame58Map.ZValue.Cx) > 3f,
              "cx " + Frame58Map.XValue.Cx + " vs " + Frame58Map.ZValue.Cx);
        Check("S154a X/Y/Z are evenly stacked",
              Math.Abs((Frame58Map.YValue.Cy - Frame58Map.XValue.Cy) - (Frame58Map.ZValue.Cy - Frame58Map.YValue.Cy)) < 2f,
              "pitches " + (Frame58Map.YValue.Cy - Frame58Map.XValue.Cy) + " / " + (Frame58Map.ZValue.Cy - Frame58Map.YValue.Cy));

        // ---- RANGE / RATE are a labelled pair on the same baseline, low in the bowl ----
        Check("S154a RANGE and RATE labels share a baseline",
              Math.Abs(Frame58Map.RangeLabel.Cy - Frame58Map.RateLabel.Cy) < 1f,
              "range " + Frame58Map.RangeLabel.Cy + " rate " + Frame58Map.RateLabel.Cy);
        Check("S154a each of RANGE / RATE has its value under its own label",
              Frame58Map.RangeValue.Y0 > Frame58Map.RangeLabel.Y1 &&
              Frame58Map.RateValue.Y0 > Frame58Map.RateLabel.Y1,
              "");
        Check("S154a RANGE is left of RATE, as the labels are",
              Frame58Map.RangeValue.Cx < Frame58Map.RateValue.Cx &&
              Frame58Map.RangeLabel.Cx < Frame58Map.RateLabel.Cx, "");

        // ---- ACCELERATION lives in its own dial, OUTSIDE the ring — the one readout that does ----
        Check("S154a ACCELERATION is outside the ring, up and left",
              Dist(Frame58Map.AccelValue.Cx, Frame58Map.AccelValue.Cy) > Frame58Map.RingOuterR,
              "distance from ring centre " + Dist(Frame58Map.AccelValue.Cx, Frame58Map.AccelValue.Cy));
        Check("S154a ...with its label directly above it",
              Frame58Map.AccelLabel.Y1 <= Frame58Map.AccelValue.Y0, "");
        // ⚠ Stated as "strictly the tallest of the twelve", NOT as a multiple. A first draft asserted
        // `> 3 x RollValue.H` on no evidence and failed at the measured 2.84 (74.65 against 26.25).
        // The multiple was a guess; being the largest is the property the drawing actually has, and
        // it is the one that would catch AccelValue being confused with an ordinary readout.
        Check("S154a ACCELERATION is the tallest readout in the frame — it is the dial's headline",
              TallestNumberIsAccel(),
              "accel h " + Frame58Map.AccelValue.H + " roll h " + Frame58Map.RollValue.H);

        // ---- AND EVERY BOX MUST BE A BOX, INSIDE THE FRAME ---------------------------------------
        // A cheap check that catches the expensive mistake: a transcription that swaps an x for a y,
        // or a sign, produces a non-positive extent or a box off the frame.
        AllBoxes();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed   (map is geometry only — nothing drawn)");
        return failures;
    }

    static bool TallestNumberIsAccel()
    {
        Frame58Map.Box[] nums = {
            Frame58Map.RollValue, Frame58Map.RollRate, Frame58Map.PitchValue, Frame58Map.PitchRate,
            Frame58Map.YawValue, Frame58Map.YawRate, Frame58Map.XValue, Frame58Map.YValue,
            Frame58Map.ZValue, Frame58Map.RangeValue, Frame58Map.RateValue };
        for (int i = 0; i < nums.Length; i++)
            if (nums[i].H >= Frame58Map.AccelValue.H) return false;
        return true;
    }

    static float Dist(float x, float y)
    {
        float dx = x - Frame58Map.RingCx, dy = y - Frame58Map.RingCy;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    static void CheckRatePair(string name, Frame58Map.Box val, Frame58Map.Box rate)
    {
        Check("S154a the " + name + " rate sits under its value", rate.Y0 >= val.Y1 && rate.Y0 - val.Y1 < 40f,
              "value ends " + val.Y1 + ", rate starts " + rate.Y0);
        Check("S154a the " + name + " rate is set smaller than its value", rate.H < val.H,
              "value h " + val.H + " rate h " + rate.H);
    }

    static void AllBoxes()
    {
        Frame58Map.Box[] all = {
            Frame58Map.RollValue, Frame58Map.RollRate, Frame58Map.PitchValue, Frame58Map.PitchRate,
            Frame58Map.YawValue, Frame58Map.YawRate, Frame58Map.XValue, Frame58Map.YValue,
            Frame58Map.ZValue, Frame58Map.RangeValue, Frame58Map.RateValue, Frame58Map.AccelValue,
            Frame58Map.RollLabel, Frame58Map.PitchLabel, Frame58Map.YawLabel,
            Frame58Map.RangeLabel, Frame58Map.RateLabel, Frame58Map.AccelLabel };
        Check("S154a the map holds all 18 measured elements", all.Length == 18, "got " + all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            Frame58Map.Box b = all[i];
            Check("S154a box " + i + " has positive extent", b.W > 0f && b.H > 0f, "w " + b.W + " h " + b.H);
            Check("S154a box " + i + " is inside the design frame",
                  b.X0 >= 0f && b.Y0 >= 0f && b.X1 <= Frame58Map.RefW && b.Y1 <= Frame58Map.RefH,
                  "x " + b.X0 + ".." + b.X1 + " y " + b.Y0 + ".." + b.Y1);
        }
    }
}
