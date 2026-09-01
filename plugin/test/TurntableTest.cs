/*
 * DragonScreen headless tests - the capsule turntable (T11a + T11b render half, BUILD_PLAN §5).
 *
 * ---- WHY THIS IS TESTED HERE AND NOT ON THE GLASS ----
 * The turntable is two things: a NAMING convention (which file is frame 9) and a GESTURE (what a
 * drag of so many pixels means). Both are exactly the sort of thing a capsule session is worst at
 * checking - the interesting cases are the wrap at each end, a drag smaller than one frame, and a
 * drag from a zero-width slot, none of which you would think to make with a mouse, and each of which
 * costs a full restart to try. Half a second here.
 *
 * T11a wrote this against a marked stand-in sequence and left one question open: which way the real
 * render turns, so whether "drag right" reads as grabbing the vehicle or as orbiting the camera.
 * T11b's render half answered the first half of it - the shipped frames turn the way Turntable's sign
 * assumes, measured off the frames themselves - so Marking() below now asserts the opposite of what
 * it did: the sequence is real, and nothing may label it a placeholder. What is still open is only
 * how the gesture FEELS, which is glass work and is tracked with the other capsule-only checks.
 *
 * ---- WHAT T11b's GLUE HALF ADDED, AND WHY IT IS TESTED HERE OF ALL PLACES ----
 * The plumbing itself - ScreenTouch's OnMouseDown/Drag/Up and the painter's three forwards - cannot
 * be run headlessly: it needs InternalCamera, a collider and a mouse. So it was written to hold no
 * decisions at all, and everything it forwards to is pure and is exercised below:
 *
 *      Gesture()    plays whole gestures through Press / Move / Release exactly as the glue calls
 *                   them, including the one judgement in the chain - was that a drag or a tap? - on
 *                   which the reset-to-front depends.
 *      Region()     the press region, which is CoverPage.CapsuleRect: the rect the sprite is DRAWN
 *                   from, so the thing the crew grabs cannot drift from the thing they see.
 *      Residency()  the window that bounds resident texture. 36 frames at 512x1024 RGBA is ~75 MB
 *                   if a drag round the vehicle keeps every frame it touched; the policy says which
 *                   few may be held, and this is where "never all of them" is actually proved.
 */
using System;
using DragonScreen;

public static class TurntableTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok)
        {
            failures++;
            Console.WriteLine("  FAIL  " + what + "   " + detail);
        }
    }

    static void Near(string what, float got, float want, float tol)
    {
        Check(what, Math.Abs(got - want) <= tol, "got " + got + " want " + want);
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen capsule turntable (T11a + T11b) tests");

        Sequence();
        Naming();
        Wrapping();
        Picker();
        Drag();
        Placement();
        Marking();
        OnThePage();
        Gesture();
        Region();
        Residency();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    // ------------------------------------------------------------------ the sequence

    static void Sequence()
    {
        // §5's starting figure. If this ever moves to 72, the step must move with it - which is the
        // point of deriving one from the other rather than writing both down.
        Check("36 frames", Turntable.Count == 36, "got " + Turntable.Count);
        Near("step is 360/count", Turntable.StepDegrees, 10f, 1e-4f);
        Near("a full turn is one revolution", Turntable.StepDegrees * Turntable.Count, 360f, 1e-3f);
        Check("front is frame 0", Turntable.FrontFrame == 0, "got " + Turntable.FrontFrame);

        // The gearing is expressed in frames per slot, and one slot must be one revolution - the
        // property that makes the vehicle come back where it started when the finger does.
        Near("one slot = one revolution", Turntable.FramesPerSlot, Turntable.Count, 1e-4f);

        for (int i = 0; i < Turntable.Count; i++)
            Near("angle of frame " + i, Turntable.AngleOf(i), i * 10f, 1e-3f);
    }

    // ------------------------------------------------------------------ file naming

    static void Naming()
    {
        Check("frame 0 key", Turntable.Key(0) == "dragon_turn_000", Turntable.Key(0));
        Check("frame 9 key", Turntable.Key(9) == "dragon_turn_009", Turntable.Key(9));
        Check("frame 35 key", Turntable.Key(35) == "dragon_turn_035", Turntable.Key(35));

        // Out of range wraps rather than naming a file that does not exist. A bare % would give
        // "dragon_turn_-01" for -1, which loads as a missing asset and draws nothing at all.
        Check("key wraps past the end", Turntable.Key(36) == "dragon_turn_000", Turntable.Key(36));
        Check("key wraps below zero", Turntable.Key(-1) == "dragon_turn_035", Turntable.Key(-1));
        Check("key wraps twice round", Turntable.Key(72) == "dragon_turn_000", Turntable.Key(72));
        Check("key wraps far below", Turntable.Key(-37) == "dragon_turn_035", Turntable.Key(-37));

        // Every frame is a distinct file - a formatting slip that collided two indices would show as
        // a turntable that sticks, which is hard to spot and easy to blame on the drag.
        for (int i = 0; i < Turntable.Count; i++)
            for (int j = i + 1; j < Turntable.Count; j++)
                if (Turntable.Key(i) == Turntable.Key(j))
                    Check("frames " + i + "/" + j + " share a key", false, Turntable.Key(i));

        // ...and every key carries the prefix the loader (ImageStore.ResolveAsset / the preview's
        // DrawCoverAsset) resolves under art/cover/.
        for (int i = 0; i < Turntable.Count; i++)
            Check("frame " + i + " keeps the prefix",
                  Turntable.Key(i).StartsWith(Turntable.KeyPrefix), Turntable.Key(i));
    }

    // ------------------------------------------------------------------ wrap

    static void Wrapping()
    {
        Check("wrapFrame(-1)", Turntable.WrapFrame(-1) == 35, "got " + Turntable.WrapFrame(-1));
        Check("wrapFrame(36)", Turntable.WrapFrame(36) == 0, "got " + Turntable.WrapFrame(36));
        Check("wrapFrame(35)", Turntable.WrapFrame(35) == 35, "got " + Turntable.WrapFrame(35));

        Near("wrap(-0.5)", Turntable.Wrap(-0.5f), 35.5f, 1e-3f);
        Near("wrap(36)", Turntable.Wrap(36f), 0f, 1e-3f);
        Near("wrap(36.5)", Turntable.Wrap(36.5f), 0.5f, 1e-3f);
        Near("wrap(-36.5)", Turntable.Wrap(-36.5f), 35.5f, 1e-3f);
        Near("wrap(12.25) is untouched", Turntable.Wrap(12.25f), 12.25f, 1e-4f);

        // A wrap must never hand out an index one past the end, however it was reached.
        Check("wrap stays below the count", Turntable.Wrap(35.99999f) < Turntable.Count,
              "got " + Turntable.Wrap(35.99999f));

        // Non-finite resolves to the front rather than to something that cannot be formatted: the
        // page must never be given a NaN to draw.
        Near("wrap(NaN) -> front", Turntable.Wrap(float.NaN), Turntable.FrontFrame, 1e-4f);
        Near("wrap(+inf) -> front", Turntable.Wrap(float.PositiveInfinity), Turntable.FrontFrame, 1e-4f);
        Near("wrap(-inf) -> front", Turntable.Wrap(float.NegativeInfinity), Turntable.FrontFrame, 1e-4f);
    }

    // ------------------------------------------------------------------ the picker

    static void Picker()
    {
        TurntableState s;

        // NEAREST, not floor: frame 0 owns [-0.5, +0.5) so the vehicle sits square-on at the front
        // rather than half a step past it.
        s.Turn = 0f;      Check("0.00 -> 0", Turntable.FrameOf(s) == 0, "got " + Turntable.FrameOf(s));
        s.Turn = 0.49f;   Check("0.49 -> 0", Turntable.FrameOf(s) == 0, "got " + Turntable.FrameOf(s));
        s.Turn = 0.51f;   Check("0.51 -> 1", Turntable.FrameOf(s) == 1, "got " + Turntable.FrameOf(s));
        s.Turn = 34.6f;   Check("34.6 -> 35", Turntable.FrameOf(s) == 35, "got " + Turntable.FrameOf(s));

        // The rounding must wrap too: 35.6 is nearer the front than it is to frame 35.
        s.Turn = 35.6f;   Check("35.6 -> 0 (rounds round the seam)",
                                Turntable.FrameOf(s) == 0, "got " + Turntable.FrameOf(s));
        s.Turn = 35.4f;   Check("35.4 -> 35", Turntable.FrameOf(s) == 35, "got " + Turntable.FrameOf(s));

        // Constructors
        Check("Front() is frame 0", Turntable.FrameOf(Turntable.Front()) == 0, "");
        Check("AtFrame(9)", Turntable.FrameOf(Turntable.AtFrame(9)) == 9, "");
        Check("AtFrame wraps", Turntable.FrameOf(Turntable.AtFrame(-1)) == 35, "");
        Check("AtAngle(90) -> 9", Turntable.FrameOf(Turntable.AtAngle(90f)) == 9,
              "got " + Turntable.FrameOf(Turntable.AtAngle(90f)));
        Check("AtAngle(180) -> 18", Turntable.FrameOf(Turntable.AtAngle(180f)) == 18, "");
        Check("AtAngle(357) -> 0 (nearest, over the seam)",
              Turntable.FrameOf(Turntable.AtAngle(357f)) == 0,
              "got " + Turntable.FrameOf(Turntable.AtAngle(357f)));
        Check("AtAngle(-10) -> 35", Turntable.FrameOf(Turntable.AtAngle(-10f)) == 35,
              "got " + Turntable.FrameOf(Turntable.AtAngle(-10f)));

        // Angle -> frame -> angle round-trips exactly on every frame the sequence has.
        for (int i = 0; i < Turntable.Count; i++)
        {
            TurntableState r = Turntable.AtAngle(Turntable.AngleOf(i));
            Check("round trip frame " + i, Turntable.FrameOf(r) == i, "got " + Turntable.FrameOf(r));
            Check("KeyOf matches Key on " + i, Turntable.KeyOf(r) == Turntable.Key(i),
                  Turntable.KeyOf(r));
        }
    }

    // ------------------------------------------------------------------ the drag

    static void Drag()
    {
        const float Slot = 800f;

        // The gearing, on its own.
        Near("full slot = one revolution", Turntable.DragFrames(Slot, Slot), Turntable.Count, 1e-3f);
        Near("half slot = half a revolution", Turntable.DragFrames(Slot * 0.5f, Slot),
             Turntable.Count * 0.5f, 1e-3f);
        Near("left is negative", Turntable.DragFrames(-Slot * 0.25f, Slot),
             -Turntable.Count * 0.25f, 1e-3f);

        // A slot with no width yields no rotation rather than an infinity. This is not theoretical:
        // a page built at h=0 (which the layout sweep does) hands exactly this in.
        Near("zero-width slot", Turntable.DragFrames(100f, 0f), 0f, 1e-6f);
        Near("negative-width slot", Turntable.DragFrames(100f, -5f), 0f, 1e-6f);
        Near("NaN dx", Turntable.DragFrames(float.NaN, Slot), 0f, 1e-6f);
        Near("infinite dx", Turntable.DragFrames(float.PositiveInfinity, Slot), 0f, 1e-6f);

        // DIRECTION. Documented convention: right advances the frame index, so the near face follows
        // the finger. Whether that reads correctly against the real render is a glass question
        // (T11b); that it is what the code does is not.
        TurntableState s = Turntable.Drag(Turntable.Front(), Slot * 0.25f, Slot);
        Check("drag right advances", Turntable.FrameOf(s) == 9, "got " + Turntable.FrameOf(s));

        // WRAP BELOW ZERO - dragging left from the front must land on the far end, not on frame 0.
        s = Turntable.Drag(Turntable.Front(), -Slot * 0.25f, Slot);
        Check("drag left wraps under", Turntable.FrameOf(s) == 27, "got " + Turntable.FrameOf(s));

        // WRAP OVER THE TOP - a full sweep is a full revolution and lands back on the front.
        s = Turntable.Drag(Turntable.Front(), Slot, Slot);
        Check("one full sweep returns to the front", Turntable.FrameOf(s) == 0,
              "got " + Turntable.FrameOf(s));
        s = Turntable.Drag(Turntable.Front(), Slot * 3f, Slot);
        Check("three sweeps return to the front", Turntable.FrameOf(s) == 0,
              "got " + Turntable.FrameOf(s));

        // The exact claim the four preview PNGs make.
        s = Turntable.Front();
        int[] want = { 0, 9, 18, 27 };
        for (int q = 0; q < 4; q++)
        {
            Check("quarter sweep " + q, Turntable.FrameOf(s) == want[q],
                  "got " + Turntable.FrameOf(s) + " want " + want[q]);
            s = Turntable.Drag(s, Slot * 0.25f, Slot);
        }
        Check("four quarter sweeps close the loop", Turntable.FrameOf(s) == 0,
              "got " + Turntable.FrameOf(s));

        // ---- THE REMAINDER IS NOT THROWN AWAY ----
        // This is the whole reason the state is a float. Six hundred one-pixel drags must arrive at
        // exactly the same place one six-hundred-pixel drag does; an int frame would arrive at the
        // front, having eaten every one of them.
        TurntableState many = Turntable.Front();
        for (int i = 0; i < 600; i++) many = Turntable.Drag(many, 1f, Slot);
        TurntableState one = Turntable.Drag(Turntable.Front(), 600f, Slot);
        Near("600 x 1px == 1 x 600px", many.Turn, one.Turn, 0.05f);
        Check("600 x 1px picks the same frame",
              Turntable.FrameOf(many) == Turntable.FrameOf(one),
              "got " + Turntable.FrameOf(many) + " vs " + Turntable.FrameOf(one));

        // A drag far smaller than one frame still MOVES something, even though the frame does not
        // change yet - "the control feels dead" is exactly the bug this rules out.
        TurntableState tiny = Turntable.Drag(Turntable.Front(), Slot / (Turntable.Count * 8f), Slot);
        Check("a sub-frame drag moves the state", tiny.Turn > 0f, "got " + tiny.Turn);
        Check("a sub-frame drag does not yet change the frame",
              Turntable.FrameOf(tiny) == 0, "got " + Turntable.FrameOf(tiny));

        // Never out of range, whatever it is fed.
        TurntableState wild = Turntable.Drag(Turntable.AtFrame(35), Slot * 12.3f, Slot);
        Check("drag stays in range", wild.Turn >= 0f && wild.Turn < Turntable.Count,
              "got " + wild.Turn);
        TurntableState nan = Turntable.Drag(Turntable.Front(), float.NaN, Slot);
        Check("NaN drag leaves a drawable state", Turntable.FrameOf(nan) == 0,
              "got " + Turntable.FrameOf(nan));
    }

    // ------------------------------------------------------------------ placement

    static void Placement()
    {
        float x, y, w, h;
        Check("FitHeight succeeds", Turntable.FitHeight(500f, 400f, 600f, out x, out y, out w, out h), "");
        Near("height honoured", h, 600f, 1e-3f);
        Near("aspect honoured", w / h, (float)Turntable.FrameW / Turntable.FrameH, 1e-4f);
        Near("centred in x", x + w * 0.5f, 500f, 1e-3f);
        Near("centred in y", y + h * 0.5f, 400f, 1e-3f);

        Check("FitHeight refuses zero height",
              !Turntable.FitHeight(0f, 0f, 0f, out x, out y, out w, out h), "");

        // The page rect: inside the camera slot, right of the content panel, at the sprite's aspect.
        // Shared by the draw and (T11b) the gesture - PageAction's one-rect rule.
        float sx, sy, sw, sh;
        CoverPage.CapsuleRect(2560, 1420, out sx, out sy, out sw, out sh);
        Check("capsule rect has size", sw > 0f && sh > 0f, sw + "x" + sh);
        Near("capsule rect keeps the sprite aspect", sw / sh,
             (float)Turntable.FrameW / Turntable.FrameH, 1e-3f);
        Check("capsule rect is on screen", sx >= 0f && sy >= 0f
              && sx + sw <= 2560f && sy + sh <= 1420f,
              sx + "," + sy + " " + sw + "x" + sh);
        // Right of the split, where the camera slot is - not over the content panel.
        Check("capsule rect is in the camera slot", sx > 2560f * 0.4f, "got " + sx);

        // A degenerate panel must not throw or hand back nonsense - LayoutSweepTest builds pages at
        // sizes like this and a rect function that divided by zero would take the sweep down.
        CoverPage.CapsuleRect(0, 0, out sx, out sy, out sw, out sh);
        Check("capsule rect at 0x0 is empty, not NaN",
              sw == 0f && sh == 0f, sw + "x" + sh);
    }

    // ------------------------------------------------------------------ the marking (§1.4)

    static void Marking()
    {
        // T11a shipped a marked stand-in sequence and this test asserted the page LABELLED it. T11b's
        // render half replaced those frames with the real Crew Dragon + trunk (rendered from the
        // MaTte0 CC-BY model by plugin/build/render_turntable.py, attributed in
        // assets/ASSET_PROVENANCE.md), so the assertion turns over: the shipped sequence is the real
        // render, and the page must now print NOTHING that calls it a placeholder.
        //
        // §1.4 is what both halves serve - invented material is never passed off as sourced material.
        // The label was the mechanism while the frames were invented; with sourced frames in, the
        // mechanism has to be OFF, and a label left drawing over a real render would be its own kind
        // of lie about the source. So this is still a test rather than a comment, and it still fails
        // if the flag and the frames on disk ever disagree.
        Check("the shipped sequence is the real render, not a stand-in",
              !Turntable.Placeholder, "Turntable.Placeholder is true");

        // The wording is kept for the day the sequence IS stood in again (§5 leaves a 72-frame
        // variant open), so it must stay usable - but nothing may draw it today.
        Check("the marking wording is still available for a future stand-in",
              !string.IsNullOrEmpty(Turntable.PlaceholderLabel)
              && Turntable.PlaceholderLabel.ToUpperInvariant().Contains("PLACEHOLDER"),
              Turntable.PlaceholderLabel);

        // The page, not the flag: build the capsule view and read back every string it emits. This is
        // the assertion that would catch a label drawn from somewhere other than Turntable.Placeholder
        // - checking the flag alone would only prove the flag.
        const int W = 2560, H = 1420;
        DisplayList dl = new DisplayList(600);
        CoverPage.Build(dl, W, H, new PageState(), MapProjection.Default(), 1,
                        CoverPage.CoverCam.Capsule, Turntable.AtFrame(7));
        string marked = null;
        for (int i = 0; i < dl.Count; i++)
        {
            DrawCmd c = dl.At(i);
            if (c.Kind != DrawKind.Text || c.Str == null) continue;
            string u = c.Str.ToUpperInvariant();
            if (u.Contains("PLACEHOLDER") || u.Contains("NOT A RENDER") || u.Contains("T11B"))
            { marked = c.Str; break; }
        }
        Check("the capsule view prints no placeholder marking", marked == null, "drew " + marked);

        // Clearing the flag also gives back the strip CoverPage reserved at the bottom of the slot
        // for that marking, so the sprite is drawn TALLER than it was under T11a. The strip is a
        // design-pixel constant private to CoverPage, so what is checked here is the consequence that
        // is visible from outside: the sprite still fits the slot at the larger size.
        float cx, cy, cw, ch;
        CoverPage.CapsuleRect(W, H, out cx, out cy, out cw, out ch);
        Check("the un-stripped sprite still fits the slot",
              cy >= 0f && cy + ch <= H, cy + " + " + ch + " in " + H);
    }

    // ------------------------------------------------------------------ on the page

    static void OnThePage()
    {
        // The capsule view must actually EMIT the frame the picker chose. Without this the maths
        // could be perfect and the page could still be drawing frame 0 forever, which is precisely
        // what it did before T11a.
        const int W = 2560, H = 1420;
        PageState s = new PageState();
        MapView v = MapProjection.Default();

        string first = null, second = null;
        int firstCount = 0;

        DisplayList a = new DisplayList(600);
        CoverPage.Build(a, W, H, s, v, 1, CoverPage.CoverCam.Capsule, Turntable.Front());
        for (int i = 0; i < a.Count; i++)
        {
            DrawCmd c = a.At(i);
            if (c.Kind == DrawKind.Image && c.AssetKey != null
                && c.AssetKey.StartsWith(Turntable.KeyPrefix))
            { first = c.AssetKey; firstCount++; }
        }

        DisplayList b = new DisplayList(600);
        CoverPage.Build(b, W, H, s, v, 1, CoverPage.CoverCam.Capsule, Turntable.AtFrame(18));
        for (int i = 0; i < b.Count; i++)
        {
            DrawCmd c = b.At(i);
            if (c.Kind == DrawKind.Image && c.AssetKey != null
                && c.AssetKey.StartsWith(Turntable.KeyPrefix))
                second = c.AssetKey;
        }

        Check("capsule view draws exactly one turntable frame", firstCount == 1,
              "got " + firstCount);
        Check("front state draws frame 000", first == "dragon_turn_000", "got " + first);
        Check("frame 18 state draws frame 018", second == "dragon_turn_018", "got " + second);

        // Neither of the other two camera views may reach for the sequence - the globe and the map
        // share the slot, and a stray frame under them would be invisible until it was not.
        foreach (CoverPage.CoverCam cam in new[] { CoverPage.CoverCam.Earth, CoverPage.CoverCam.Map })
        {
            DisplayList d = new DisplayList(600);
            CoverPage.Build(d, W, H, s, v, 1, cam, Turntable.AtFrame(7));
            int n = 0;
            for (int i = 0; i < d.Count; i++)
            {
                DrawCmd c = d.At(i);
                if (c.Kind == DrawKind.Image && c.AssetKey != null
                    && c.AssetKey.StartsWith(Turntable.KeyPrefix)) n++;
            }
            Check(cam + " view draws no turntable frame", n == 0, "got " + n);
        }

        // The overloads that predate T11a must still open on the front - the capsule view is not
        // supposed to start half-turned just because a caller had nothing to say about it.
        DisplayList e = new DisplayList(600);
        CoverPage.Build(e, W, H, s, v, 1, CoverPage.CoverCam.Capsule);
        string legacy = null;
        for (int i = 0; i < e.Count; i++)
        {
            DrawCmd c = e.At(i);
            if (c.Kind == DrawKind.Image && c.AssetKey != null
                && c.AssetKey.StartsWith(Turntable.KeyPrefix)) legacy = c.AssetKey;
        }
        Check("the old overload opens on the front", legacy == "dragon_turn_000", "got " + legacy);

        // And the page must not overflow its declared budget with the sequence in it.
        DisplayList f = new DisplayList(CoverPage.Commands);
        CoverPage.Build(f, W, H, s, v, 1, CoverPage.CoverCam.Capsule, Turntable.AtFrame(4));
        Check("capsule view fits the command budget", !f.Overflowed,
              "overflowed at " + CoverPage.Commands);
    }

    // ------------------------------------------------------------------ the gesture (T11b item 4/5)

    static void Gesture()
    {
        const float Slot = 800f;

        // Idle is idle: a move or a release that arrives with no press behind it must do NOTHING.
        // This is not hypothetical - Unity raises OnMouseDrag every frame the button is held, and
        // OnMouseUp when it is let go, including for presses some other control on the page claimed.
        // If either acted, tapping NEXT VIEW would spin or reset the vehicle.
        TurntableTouch idle = Turntable.Idle();
        Check("idle is not dragging", !idle.Dragging, "");
        TurntableTouch after;
        TurntableState s = Turntable.Move(Turntable.AtFrame(9), idle, 500f, Slot, out after);
        Check("a move with no press does not turn", Turntable.FrameOf(s) == 9,
              "got " + Turntable.FrameOf(s));
        Check("a move with no press stays idle", !after.Dragging, "");
        s = Turntable.Release(Turntable.AtFrame(9), idle, Slot, out after);
        Check("a release with no press does not reset", Turntable.FrameOf(s) == 9,
              "got " + Turntable.FrameOf(s));

        // A press records where it started and turns nothing yet - which of drag or tap it was is
        // only knowable at the release.
        TurntableTouch g = Turntable.Press(120f);
        Check("press is dragging", g.Dragging, "");
        Near("press remembers x", g.LastX, 120f, 1e-3f);
        Near("press has travelled nothing", g.TravelPx, 0f, 1e-6f);

        // ---- THE GLUE SEQUENCE, PLAYED THROUGH ----
        // press at x, then absolute pointer positions as they arrive frame by frame. This is exactly
        // what ScreenPainter.TouchDrag does with each sample, so what passes here is what runs.
        g = Turntable.Press(100f);
        s = Turntable.Front();
        s = Turntable.Move(s, g, 100f + Slot * 0.25f, Slot, out g);
        Check("a quarter-slot drag lands on frame 9", Turntable.FrameOf(s) == 9,
              "got " + Turntable.FrameOf(s));
        Near("travel is the distance moved", g.TravelPx, Slot * 0.25f, 1e-2f);
        Near("the gesture tracks the pointer", g.LastX, 100f + Slot * 0.25f, 1e-2f);

        // MANY SAMPLES == ONE SAMPLE. A drag arrives as one move per rendered frame, so the same
        // sweep delivered in 50 steps must land where it lands in one - the remainder-keeping claim
        // the continuous Turn exists for, now made through the gesture the glue actually calls.
        TurntableTouch many = Turntable.Press(100f);
        TurntableState ms = Turntable.Front();
        for (int i = 1; i <= 50; i++)
            ms = Turntable.Move(ms, many, 100f + Slot * 0.25f * i / 50f, Slot, out many);
        Near("50 samples == 1 sample", ms.Turn, s.Turn, 0.05f);
        Near("50 samples travelled the same distance", many.TravelPx, Slot * 0.25f, 1e-1f);

        // Dragging LEFT wraps under, through the gesture rather than through Drag directly.
        TurntableTouch lg = Turntable.Press(600f);
        TurntableState ls = Turntable.Move(Turntable.Front(), lg, 600f - Slot * 0.25f, Slot, out lg);
        Check("dragging left wraps under", Turntable.FrameOf(ls) == 27,
              "got " + Turntable.FrameOf(ls));

        // ---- RELEASE: DRAG vs TAP ----
        TurntableState kept = Turntable.Release(s, g, Slot, out after);
        Check("releasing a real drag keeps the frame", Turntable.FrameOf(kept) == 9,
              "got " + Turntable.FrameOf(kept));
        Check("release goes idle", !after.Dragging, "");

        // §5 C4's reset: press and release on the capsule with no travel, from a turned state, puts
        // the vehicle back on the authored front. This is the whole of the "front tap".
        TurntableTouch tap = Turntable.Press(300f);
        TurntableState reset = Turntable.Release(Turntable.AtFrame(23), tap, Slot, out after);
        Check("a tap resets to the front", Turntable.FrameOf(reset) == Turntable.FrontFrame,
              "got " + Turntable.FrameOf(reset));
        Check("a tap resets exactly, not nearly", reset.Turn == 0f, "got " + reset.Turn);
        Check("the tap goes idle too", !after.Dragging, "");

        // A tap that jittered a pixel or two is still a tap - a finger on glass is never still.
        TurntableTouch jitter = Turntable.Press(300f);
        TurntableState js = Turntable.AtFrame(23);
        js = Turntable.Move(js, jitter, 301f, Slot, out jitter);
        js = Turntable.Move(js, jitter, 300f, Slot, out jitter);
        Check("a jittered tap is still a tap", Turntable.IsTap(jitter, Slot),
              "travelled " + jitter.TravelPx);
        js = Turntable.Release(js, jitter, Slot, out after);
        Check("a jittered tap still resets", Turntable.FrameOf(js) == Turntable.FrontFrame,
              "got " + Turntable.FrameOf(js));

        // TRAVEL IS THE PATH, NOT THE DISPLACEMENT. A wiggle that ends where it began has moved the
        // vehicle nowhere but is emphatically not a tap - and must not be treated as one, or a crew
        // member turning the capsule back and forth would have it snap to the front on release.
        TurntableTouch wig = Turntable.Press(300f);
        TurntableState ws = Turntable.AtFrame(9);
        ws = Turntable.Move(ws, wig, 400f, Slot, out wig);
        ws = Turntable.Move(ws, wig, 300f, Slot, out wig);
        Near("a wiggle travelled its whole path", wig.TravelPx, 200f, 1e-2f);
        Check("a wiggle is not a tap", !Turntable.IsTap(wig, Slot), "travelled " + wig.TravelPx);
        ws = Turntable.Release(ws, wig, Slot, out after);
        Check("a wiggle does not reset", Turntable.FrameOf(ws) == 9, "got " + Turntable.FrameOf(ws));

        // The threshold itself, from both sides. Half a frame of rotation: below it the sprite on the
        // glass never changed, so calling it a tap cannot contradict what the crew saw.
        float halfFrame = Slot * (Turntable.TapSlopFrames / Turntable.FramesPerSlot);
        TurntableTouch onSlop = Turntable.Press(0f);
        Turntable.Move(Turntable.Front(), onSlop, halfFrame * 0.99f, Slot, out onSlop);
        Check("just under half a frame is a tap", Turntable.IsTap(onSlop, Slot),
              "travelled " + onSlop.TravelPx);
        TurntableTouch overSlop = Turntable.Press(0f);
        Turntable.Move(Turntable.Front(), overSlop, halfFrame * 1.5f, Slot, out overSlop);
        Check("half again is a drag", !Turntable.IsTap(overSlop, Slot),
              "travelled " + overSlop.TravelPx);
        Check("an idle gesture is never a tap", !Turntable.IsTap(Turntable.Idle(), Slot), "");

        // THE SLOP IS IN FRAMES, SO IT IS THE SAME GESTURE AT EVERY RESOLUTION. The identical
        // fraction of a wider slot must read the same way - this is the reason it is not "N pixels".
        TurntableTouch wide = Turntable.Press(0f);
        Turntable.Move(Turntable.Front(), wide, Slot * 4f * 0.99f * (Turntable.TapSlopFrames / Turntable.FramesPerSlot),
                       Slot * 4f, out wide);
        Check("the tap slop scales with the slot", Turntable.IsTap(wide, Slot * 4f),
              "travelled " + wide.TravelPx);

        // A sample that could not be turned into a page pixel (the raycast missed) must not poison
        // the gesture with a NaN - the glue skips those, and this is the belt to that brace.
        TurntableTouch bad = Turntable.Press(100f);
        TurntableState bs = Turntable.Move(Turntable.AtFrame(4), bad, float.NaN, Slot, out bad);
        Check("a NaN sample turns nothing", Turntable.FrameOf(bs) == 4, "got " + Turntable.FrameOf(bs));
        Near("a NaN sample does not poison the travel", bad.TravelPx, 0f, 1e-6f);
        Near("a NaN sample does not poison the anchor", bad.LastX, 100f, 1e-3f);
        Check("a NaN sample leaves the press alive", bad.Dragging, "");

        // A gesture on a zero-width slot (the layout sweep builds pages at h=0) must stay drawable.
        TurntableTouch zero = Turntable.Press(0f);
        TurntableState zs = Turntable.Move(Turntable.Front(), zero, 400f, 0f, out zero);
        Check("a drag in a zero-width slot turns nothing", Turntable.FrameOf(zs) == 0,
              "got " + Turntable.FrameOf(zs));
        zs = Turntable.Release(zs, zero, 0f, out after);
        Check("releasing in a zero-width slot is drawable", Turntable.FrameOf(zs) == 0,
              "got " + Turntable.FrameOf(zs));
    }

    // ------------------------------------------------------------------ the press region

    static void Region()
    {
        const int W = 2560, H = 1420;
        float x, y, w, h;
        CoverPage.CapsuleRect(W, H, out x, out y, out w, out h);

        // The region IS the drawn rect - PageAction's one-rect rule. Centre, and each edge.
        Check("the capsule centre starts a gesture",
              CoverPage.CapsuleHit(x + w * 0.5f, y + h * 0.5f, W, H, CoverPage.CoverCam.Capsule), "");
        Check("the top-left corner is inside",
              CoverPage.CapsuleHit(x, y, W, H, CoverPage.CoverCam.Capsule), "");
        Check("just left of the sprite is outside",
              !CoverPage.CapsuleHit(x - 2f, y + h * 0.5f, W, H, CoverPage.CoverCam.Capsule), "");
        Check("just right of the sprite is outside",
              !CoverPage.CapsuleHit(x + w + 2f, y + h * 0.5f, W, H, CoverPage.CoverCam.Capsule), "");
        Check("just above the sprite is outside",
              !CoverPage.CapsuleHit(x + w * 0.5f, y - 2f, W, H, CoverPage.CoverCam.Capsule), "");
        Check("just below the sprite is outside",
              !CoverPage.CapsuleHit(x + w * 0.5f, y + h + 2f, W, H, CoverPage.CoverCam.Capsule), "");

        // ONLY on the capsule view. The globe and the flat map share the same slot, and a press on
        // either of them turning a vehicle that is not being drawn is the bug this rules out.
        Check("the Earth view has no capsule region",
              !CoverPage.CapsuleHit(x + w * 0.5f, y + h * 0.5f, W, H, CoverPage.CoverCam.Earth), "");
        Check("the Map view has no capsule region",
              !CoverPage.CapsuleHit(x + w * 0.5f, y + h * 0.5f, W, H, CoverPage.CoverCam.Map), "");

        // A degenerate panel yields no region rather than a division by zero.
        Check("a 0x0 panel has no capsule region",
              !CoverPage.CapsuleHit(0f, 0f, 0, 0, CoverPage.CoverCam.Capsule), "");

        // THE CAPSULE MUST NOT SHADOW THE PAGE'S BUTTONS. It is the biggest thing on the view, so
        // every control the Cover hit-tests before it is swept here: whatever HitTest claims wins,
        // and the painter only offers the gesture what came back None.
        int shadowed = 0;
        for (float px = 0f; px < W; px += 17f)
            for (float py = 0f; py < H; py += 17f)
            {
                if (!CoverPage.CapsuleHit(px, py, W, H, CoverPage.CoverCam.Capsule)) continue;
                if (CoverPage.HitTest(px, py, W, H, CoverPage.CoverCam.Capsule)
                    != CoverPage.CoverButton.None) shadowed++;
            }
        // Not "must be zero" - the NEXT VIEW pill is deliberately drawn over the slot, and it WINS,
        // because the painter asks HitTest first. What is asserted is that the overlap is small: a
        // large one would mean the gesture region had been placed over the page's controls.
        Check("the capsule barely overlaps the page's controls", shadowed < 40,
              "overlapping sample points: " + shadowed);
    }

    // ------------------------------------------------------------------ residency (T11b item 6)

    static void Residency()
    {
        // ---- THE CLAIM THIS SUITE EXISTS TO MAKE ----
        // A full revolution touches every frame. Held for ever - ImageStore's rule for every other
        // asset - that is Count x FrameBytes of texture, and the policy's whole job is to make the
        // resident set a handful instead. Both numbers are computed, not written down, so the claim
        // cannot go stale if the sequence is ever re-rendered at another size or frame count.
        long whole = (long)Turntable.Count * Turntable.FrameBytes;
        Check("the unbounded sequence really is the problem", whole > 64L * 1024 * 1024,
              "whole sequence = " + (whole / (1024 * 1024)) + " MB");

        int[] none = { Turntable.NotShowing, Turntable.NotShowing, Turntable.NotShowing };
        Check("nothing is resident when no screen shows the view",
              Turntable.ResidentCount(none) == 0, "got " + Turntable.ResidentCount(none));
        for (int f = 0; f < Turntable.Count; f++)
            Check("frame " + f + " is not held when nobody is looking",
                  !Turntable.IsResident(f, none), "");
        Check("a null centre list holds nothing", !Turntable.IsResident(0, null), "");

        // ---- ONE SCREEN: THE WINDOW, PLUS THE PINNED FRONT ----
        // Every centre in turn, so the seam is covered rather than sampled. Expected size is derived
        // the same way the policy derives it: the window, and the front if it falls outside.
        for (int c = 0; c < Turntable.Count; c++)
        {
            int[] one = { c, Turntable.NotShowing, Turntable.NotShowing };
            bool frontInside = Turntable.Distance(Turntable.FrontFrame, c) <= Turntable.WarmRadius;
            int want = Turntable.WarmSteps + (frontInside ? 0 : 1);
            Check("centre " + c + " holds " + want + " frames",
                  Turntable.ResidentCount(one) == want,
                  "got " + Turntable.ResidentCount(one));
            Check("centre " + c + " holds its own frame", Turntable.IsResident(c, one), "");
            Check("centre " + c + " holds the front", Turntable.IsResident(Turntable.FrontFrame, one), "");
            Check("centre " + c + " never holds the whole sequence",
                  Turntable.ResidentCount(one) < Turntable.Count,
                  "got " + Turntable.ResidentCount(one));

            // Every frame either side of the window, at the exact boundary. The frame WarmRadius
            // away is in; the next one out is not (unless it is the pinned front).
            int inEdge = Turntable.WrapFrame(c + Turntable.WarmRadius);
            int outEdge = Turntable.WrapFrame(c + Turntable.WarmRadius + 1);
            Check("centre " + c + " holds the far edge of its window",
                  Turntable.IsResident(inEdge, one), "frame " + inEdge);
            Check("centre " + c + " lets go one past the window",
                  !Turntable.IsResident(outEdge, one) || outEdge == Turntable.FrontFrame,
                  "frame " + outEdge);
        }

        // The memory the policy actually allows one screen, against what it replaced.
        int[] worstOne = { 18, Turntable.NotShowing, Turntable.NotShowing };
        long held = (long)Turntable.ResidentCount(worstOne) * Turntable.FrameBytes;
        Check("one screen holds no more than 16 MB of turntable", held <= 16L * 1024 * 1024,
              "held = " + (held / (1024 * 1024)) + " MB");
        Check("the policy is at least five times better than holding everything",
              held * 5 <= whole, "held " + held + " of " + whole);

        // ---- THREE SCREENS: THE UNION, AND ITS BOUND ----
        // Three screens can each be showing the capsule at their own angle, so the resident set is
        // the union of their windows - the alternative, one shared window, has each screen evicting
        // the others' frames and reloading them from disk every frame. Swept rather than argued.
        int worst = 0;
        for (int a = 0; a < Turntable.Count; a += 1)
            for (int b = 0; b < Turntable.Count; b += 7)
                for (int c = 0; c < Turntable.Count; c += 11)
                {
                    int[] three = { a, b, c };
                    int n = Turntable.ResidentCount(three);
                    if (n > worst) worst = n;
                    Check("three screens never hold the whole sequence", n < Turntable.Count, "got " + n);
                    Check("each screen holds its own frame",
                          Turntable.IsResident(a, three) && Turntable.IsResident(b, three)
                          && Turntable.IsResident(c, three), a + "/" + b + "/" + c);
                }
        Check("three screens stay inside the union bound",
              worst <= 3 * Turntable.WarmSteps + 1, "worst = " + worst);
        long worstBytes = (long)worst * Turntable.FrameBytes;
        Check("even three diverged screens stay well under the whole sequence",
              worstBytes * 2 <= whole, "worst = " + (worstBytes / (1024 * 1024)) + " MB");

        // A screen that leaves the view gives its frames back: what is left is exactly the other
        // screen's window. This is the "release on leaving" half of the policy.
        int[] two = { 5, 25, Turntable.NotShowing };
        int[] left = { Turntable.NotShowing, 25, Turntable.NotShowing };
        Check("frame 5 is held while a screen is looking at it", Turntable.IsResident(5, two), "");
        Check("frame 5 is let go when that screen leaves", !Turntable.IsResident(5, left), "");
        Check("the other screen keeps its own window", Turntable.IsResident(25, left), "");
        Check("the front survives while anyone is looking",
              Turntable.IsResident(Turntable.FrontFrame, left), "");

        // ---- THE WARM ORDER ----
        // Nearest first, so the frame being LOOKED at is loaded before its neighbours and the cost of
        // opening the view is spread over frames instead of landing as one hitch.
        Check("the first frame warmed is the one on screen", Turntable.WarmOffset(0) == 0,
              "got " + Turntable.WarmOffset(0));
        int prev = -1;
        bool[] seen = new bool[Turntable.WarmSteps];
        for (int i = 0; i < Turntable.WarmSteps; i++)
        {
            int off = Turntable.WarmOffset(i);
            int mag = off < 0 ? -off : off;
            Check("warm step " + i + " is inside the window", mag <= Turntable.WarmRadius,
                  "offset " + off);
            Check("warm order never moves back towards the centre", mag >= prev,
                  "step " + i + " offset " + off);
            prev = mag;
            seen[off + Turntable.WarmRadius] = true;
        }
        for (int i = 0; i < Turntable.WarmSteps; i++)
            Check("warm order covers offset " + (i - Turntable.WarmRadius), seen[i], "");
        Check("an out-of-range warm step is the centre, not an exception",
              Turntable.WarmOffset(-1) == 0 && Turntable.WarmOffset(Turntable.WarmSteps) == 0, "");

        // The window the glue warms and the set the glue keeps must be the SAME set, or it would
        // load a frame and evict it on the next sweep, for ever.
        for (int c = 0; c < Turntable.Count; c++)
        {
            int[] one = { c, Turntable.NotShowing, Turntable.NotShowing };
            for (int i = 0; i < Turntable.WarmSteps; i++)
            {
                int f = Turntable.WrapFrame(c + Turntable.WarmOffset(i));
                Check("what is warmed at centre " + c + " is what is kept",
                      Turntable.IsResident(f, one), "frame " + f);
            }
        }

        // ---- THE SEAM ----
        // Residency is the only part of the turntable that measures a DISTANCE between frames, and a
        // subtraction would make 35 and 0 thirty-five apart - which would evict the frame the crew is
        // about to drag onto, every single time they crossed the front.
        Check("35 and 0 are one apart", Turntable.Distance(35, 0) == 1,
              "got " + Turntable.Distance(35, 0));
        Check("0 and 35 are one apart too", Turntable.Distance(0, 35) == 1,
              "got " + Turntable.Distance(0, 35));
        Check("18 is the far side", Turntable.Distance(0, 18) == 18, "got " + Turntable.Distance(0, 18));
        Check("distance wraps its inputs", Turntable.Distance(36, 1) == 1,
              "got " + Turntable.Distance(36, 1));
        for (int a = 0; a < Turntable.Count; a++)
            for (int b = 0; b < Turntable.Count; b++)
            {
                Check("distance is symmetric " + a + "/" + b,
                      Turntable.Distance(a, b) == Turntable.Distance(b, a), "");
                Check("distance never exceeds half the sequence " + a + "/" + b,
                      Turntable.Distance(a, b) <= Turntable.Count / 2, "");
            }

        // Every frame the policy may hold has a real key - a resident set that named a file that did
        // not exist would warm a MISSING asset once per centre change, for ever.
        int[] mid = { 17, Turntable.NotShowing, Turntable.NotShowing };
        for (int f = 0; f < Turntable.Count; f++)
        {
            if (!Turntable.IsResident(f, mid)) continue;
            Check("resident frame " + f + " has a real key",
                  Turntable.Key(f) != null && Turntable.Key(f).StartsWith(Turntable.KeyPrefix),
                  Turntable.Key(f));
        }
    }
}
