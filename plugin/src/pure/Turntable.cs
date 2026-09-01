// DragonScreen — Turntable  (PURE: the capsule sprite turntable, BUILD_PLAN §5)
// ============================================================================================
// §5 settled the shape of this: a true 2-photo 3D reconstruction is impossible and the renderer is
// 2D, so the capsule view is a PRE-RENDERED SPRITE TURNTABLE that reads as 3D — N frames around the
// vehicle, picked by a horizontal drag.
//
// ---- WHAT IS HERE, AND WHAT IS DELIBERATELY NOT (T11a / T11b) ----
// T11 was SPLIT (owner decision via the overseer, 2026-09-02) because §5's C1 prerequisite — the
// MaTte0 CC-BY model — is NOT in the repo, and C7 bars going to look for it. So:
//
//      T11a (this file)   the MODEL-INDEPENDENT half: the sequence naming, the frame picker, and
//                         the drag-delta -> frame-index maths with wrap. All pure, so every one of
//                         those is settled headlessly instead of on the glass.
//      T11b render half   DONE 2026-09-02: the owner placed the MaTte0 model in the repo and the
//                         real trunk-inclusive sequence is baked by plugin/build/render_turntable.py.
//                         Placeholder below is now FALSE — see it for what that turns off.
//      T11b glass half    still open: the glue that turns a finger on the glass into calls to Drag
//                         below, and confirming how the gesture FEELS in the capsule (tracked with
//                         the other capsule-only checks under S17).
//
// ---- WHY A CONTINUOUS `Turn` AND NOT AN INT FRAME ----
// The obvious version keeps an int frame and adds `(int)(dx / pxPerFrame)` to it. That silently
// eats every drag smaller than one frame: a slow, careful drag — which is the one a crew member
// making a small look-around actually makes — moves nothing at all, and the control feels dead.
// Keeping the position as a float and rounding only when a FRAME is asked for means the remainder
// is never thrown away, so a thousand one-pixel drags add up to exactly the same place one
// thousand-pixel drag does. There is a test for precisely that.
//
// ---- THE STATE IS A VALUE, NOT AN OBJECT ----
// Same rule as MapView: the caller holds it, every operation returns a new one, nothing here
// mutates or allocates. That is what lets the preview drive the real drag function and render what
// it produced, rather than rendering a frame index someone typed in by hand.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>
    /// Where the turntable is pointing: a CONTINUOUS frame position, always wrapped into
    /// [0, Turntable.Count). Fractional on purpose — see the header.
    /// </summary>
    public struct TurntableState
    {
        public float Turn;
    }

    public static class Turntable
    {
        // ---- THE SEQUENCE -------------------------------------------------------------------

        /// <summary>Frames in one revolution. §5's starting figure ("36 @ 10°, 72 if needed"); the
        /// step is derived from it below so the two cannot disagree.</summary>
        public const int Count = 36;

        /// <summary>Degrees of azimuth between consecutive frames.</summary>
        public const float StepDegrees = 360f / Count;

        /// <summary>The frame the vehicle faces the crew from — §5's "front" for the reset tap.
        /// Frame 0 by construction: the render starts at the front and turns from there.</summary>
        public const int FrontFrame = 0;

        /// <summary>art/cover/&lt;prefix&gt;NNN.png — §5's C3 naming, resolved by
        /// ImageStore.ResolveAsset in the game and by PreviewMain.DrawCoverAsset out of it. Both read
        /// art/cover/, which is why the sequence lives there and not beside dragon.png.</summary>
        public const string KeyPrefix = "dragon_turn_";

        // Nominal sprite size. NOT decoration: an Asset command carries no declared size the way
        // Images.Size does for a built-in ImageId, so the PAGE has to know the aspect to place the
        // rect — and if the real render (T11b) is produced at a different aspect, the capsule will
        // sit in a rect shaped for something else. 1:2 is the vehicle: Crew Dragon is ~4 m across
        // and ~8.1 m tall with the trunk. The placeholder generator writes exactly this size, so the
        // one number is honoured on both sides of the split.
        public const int FrameW = 512, FrameH = 1024;

        /// <summary>
        /// TRUE while the shipped sequence is a marked PLACEHOLDER set, not a real render.
        ///
        /// **FALSE since T11b's render half (2026-09-02)**: art/cover/dragon_turn_NNN.png is now the
        /// real Crew Dragon + trunk, rendered by plugin/build/render_turntable.py from the MaTte0
        /// CC-BY model (attribution in assets/ASSET_PROVENANCE.md). Clearing it turns off two things
        /// in one move — the label the page printed over the sprite, and the strip CoverPage reserved
        /// at the bottom of the slot to fit that label in.
        ///
        /// The MECHANISM stays. §1.4 forbids passing invented art off as sourced art, and a stand-in
        /// that is not labelled is exactly that; if the sequence is ever re-stood-in (the 72-frame
        /// experiment §5 leaves open, say), setting this back to true is the whole of the marking.
        /// The test asserts on whichever way it is set, so the two cannot drift apart.
        ///
        /// `static readonly`, not `const`, deliberately: as a const the compiler folds every test of
        /// it and reports the other branch as unreachable code, so flipping it would trade one dead
        /// branch for another and the build would carry a warning either way round.
        /// </summary>
        public static readonly bool Placeholder = false;

        /// <summary>What the page prints over a placeholder sequence — drawn only while
        /// <see cref="Placeholder"/> is true, so nothing prints it today. Names the task that
        /// replaced the stand-in, so the wording still says what happened.</summary>
        public const string PlaceholderLabel = "PLACEHOLDER SEQUENCE - T11b RENDERS THE REAL CAPSULE";

        /// <summary>The asset key for a frame, wrapped — so a caller that computed 36 or -1 gets a
        /// real frame rather than a missing-asset warning. Three digits: 36 frames today, and 72
        /// (§5's fallback) still fits without renaming the set.</summary>
        public static string Key(int frame)
        {
            int f = WrapFrame(frame);
            // Built by hand rather than with a format string: this runs inside the draw path, and
            // the no-allocation rule in DisplayList's header applies to everything it calls. Three
            // digits, zero-padded, no culture in play.
            char[] d = new char[3];
            d[0] = (char)('0' + (f / 100) % 10);
            d[1] = (char)('0' + (f / 10) % 10);
            d[2] = (char)('0' + f % 10);
            return KeyPrefix + new string(d);
        }

        /// <summary>The azimuth a frame was rendered at, in degrees, 0 at the front.</summary>
        public static float AngleOf(int frame) { return WrapFrame(frame) * StepDegrees; }

        // ---- THE FRAME PICKER ---------------------------------------------------------------

        /// <summary>Bring any integer frame into [0, Count) — C#'s % keeps the sign of the dividend,
        /// so -1 % 36 is -1, not 35, and a plain modulo here would name a file that does not
        /// exist.</summary>
        public static int WrapFrame(int frame)
        {
            // No Count > 0 guard: Count is a compile-time constant, so the guard would be dead code
            // the compiler is right to warn about rather than a defence against anything.
            int f = frame % Count;
            if (f < 0) f += Count;
            return f;
        }

        /// <summary>Bring a continuous turn into [0, Count). A non-finite input (a drag computed from
        /// a zero-size slot upstream, say) resolves to the front rather than to a frame index that
        /// cannot be formatted — the page must never be given a NaN to draw.</summary>
        public static float Wrap(float turn)
        {
            if (float.IsNaN(turn) || float.IsInfinity(turn)) return FrontFrame;
            float t = turn % Count;
            if (t < 0f) t += Count;
            // A turn a hair under Count can round UP to Count in single precision; clamp rather than
            // let FrameOf hand out an index one past the end.
            if (t >= Count) t = 0f;
            return t;
        }

        /// <summary>Which sprite to draw: the NEAREST frame, so the vehicle sits square-on at the
        /// front instead of half a step past it. Frame 0 therefore owns [-0.5, +0.5) of a turn.</summary>
        public static int FrameOf(TurntableState s)
        {
            float t = Wrap(s.Turn);
            return WrapFrame((int)Math.Floor(t + 0.5f));
        }

        /// <summary>The asset key for the state — the whole picker, in the form the page wants.</summary>
        public static string KeyOf(TurntableState s) { return Key(FrameOf(s)); }

        /// <summary>The azimuth the state is showing, in degrees.</summary>
        public static float AngleOf(TurntableState s) { return AngleOf(FrameOf(s)); }

        // ---- CONSTRUCTORS -------------------------------------------------------------------

        /// <summary>Facing the crew: §5's reset/"front" position.</summary>
        public static TurntableState Front() { return AtFrame(FrontFrame); }

        public static TurntableState AtFrame(int frame)
        {
            TurntableState s; s.Turn = WrapFrame(frame); return s;
        }

        /// <summary>At an azimuth in degrees — how a caller that thinks in angles (the render script,
        /// a test) addresses the sequence without knowing the frame count.</summary>
        public static TurntableState AtAngle(float degrees)
        {
            TurntableState s; s.Turn = Wrap(degrees / StepDegrees); return s;
        }

        // ---- THE DRAG -----------------------------------------------------------------------
        //
        // ---- WHY THE SLOT WIDTH IS AN ARGUMENT AND NOT A CONSTANT ----
        // A fixed pixels-per-frame would mean the same wrist movement spins the vehicle a different
        // amount on the preview (1280 wide), on the in-game RenderTexture (2560), and on the 2x
        // cover render. Expressing the gearing as a FRACTION OF THE SLOT makes the gesture the same
        // physical sweep across the glass at every resolution, which is the only definition of "the
        // same drag" that survives all three.
        //
        // ---- ONE FULL SLOT = ONE FULL REVOLUTION ----
        // Chosen, not measured: there is no reference for it (the reference UI's capsule is a live
        // three.js model with its own orbit control, not a sprite set). A whole revolution for a
        // whole sweep of the view is the ratio a turntable widget conventionally uses, and it brings
        // the vehicle back where it started when the finger comes back where it started.
        //
        // ---- THE SIGN, AND THE ONE THING GLASS STILL DECIDES ----
        // Drag RIGHT advances the frame index, so the vehicle's near face follows the finger — the
        // "grab it and turn it" reading, not the "orbit the camera" one. That is correct ONLY if the
        // sequence is rendered turning the same way, and as of T11b's render half IT IS: the shipped
        // frames put the vehicle at +i·10° about the up axis with the camera on the near side, so a
        // surface feature enters at the LEFT limb and leaves at the RIGHT as the index rises. Both
        // halves of that claim are checked — render_turntable.py derives it in its header, and the
        // frames themselves were measured (the trunk's solar array enters at the left limb on frame
        // 3 and leaves at the right on frame 33, its centroid moving right the whole way, and no
        // other frame order produces that). What glass still decides is only how the GESTURE feels —
        // the gearing below — not which way the vehicle turns. If it ever needs reversing, the fix is
        // the sign of this one constant, and nothing else in the file moves.

        /// <summary>Frames advanced by dragging right across the full width of the slot.</summary>
        public const float FramesPerSlot = Count;

        /// <summary>
        /// The frame delta a horizontal drag means. Split out from <see cref="Drag"/> so the gearing
        /// can be tested on its own, and so the glue (T11b) can report it while tuning on the glass.
        /// A slot with no width yields no rotation rather than an infinity.
        /// </summary>
        public static float DragFrames(float dxPanelPx, float slotWidthPx)
        {
            if (slotWidthPx <= 0f) return 0f;
            if (float.IsNaN(slotWidthPx) || float.IsInfinity(slotWidthPx)) return 0f;
            if (float.IsNaN(dxPanelPx) || float.IsInfinity(dxPanelPx)) return 0f;
            return dxPanelPx * (FramesPerSlot / slotWidthPx);
        }

        /// <summary>
        /// Apply a horizontal drag. <paramref name="dxPanelPx"/> is the movement since the last call,
        /// in PANEL pixels (positive = right); <paramref name="slotWidthPx"/> is the width of the
        /// capsule slot the gesture is happening in, in the same units.
        ///
        /// The remainder is kept — see the header. Wrapping happens here, once, so no caller can hold
        /// a state that is out of range.
        /// </summary>
        public static TurntableState Drag(TurntableState s, float dxPanelPx, float slotWidthPx)
        {
            TurntableState o;
            o.Turn = Wrap(Wrap(s.Turn) + DragFrames(dxPanelPx, slotWidthPx));
            return o;
        }

        // ---- PLACEMENT ----------------------------------------------------------------------

        /// <summary>
        /// The sprite's rect for a target height, centred on (cx, cy) — Images.FitHeight for a
        /// sequence, using the nominal FrameW/FrameH above. The aspect is the CALLER's to honour and
        /// the renderer's to trust, exactly as for a built-in image: a renderer that letterboxed
        /// would hide a layout mistake instead of showing it.
        /// </summary>
        public static bool FitHeight(float cx, float cy, float targetHeight,
                                     out float x, out float y, out float w, out float h)
        {
            x = y = w = h = 0f;
            if (targetHeight <= 0f || FrameW <= 0 || FrameH <= 0) return false;
            h = targetHeight;
            w = targetHeight * ((float)FrameW / FrameH);
            x = cx - w * 0.5f;
            y = cy - h * 0.5f;
            return true;
        }
    }
}
