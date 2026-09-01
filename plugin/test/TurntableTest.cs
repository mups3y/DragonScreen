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
        Console.WriteLine("DragonScreen capsule turntable (T11a + T11b render) tests");

        Sequence();
        Naming();
        Wrapping();
        Picker();
        Drag();
        Placement();
        Marking();
        OnThePage();

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
}
