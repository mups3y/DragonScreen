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
//      T11b glue half     DONE 2026-09-02: the gesture (press / drag / release, and the tap that
//                         resets to the front) and the RESIDENCY policy that bounds how many 2 MB
//                         frames may be in memory at once. Both live here, pure, for the same
//                         reason the maths does — see their own headers below.
//      T11b glass half    all that is left: how the gesture FEELS in the capsule — the sign and the
//                         gearing against the real sprites. Tracked with the other capsule-only
//                         checks under S17; nothing in this file is waiting on it.
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

    /// <summary>
    /// A press in progress on the capsule: the turntable's gesture, between OnMouseDown and
    /// OnMouseUp. Like TurntableState a VALUE - the caller holds it, every operation returns a new
    /// one - and for the same reason: it lets a headless test play a whole gesture through the real
    /// functions the glue calls.
    /// </summary>
    public struct TurntableTouch
    {
        /// <summary>A press landed on the capsule and has not been released yet.</summary>
        public bool Dragging;
        /// <summary>Page x of the most recent sample - the next move is measured from it.</summary>
        public float LastX;
        /// <summary>Total |dx| travelled since the press. The path, not the displacement.</summary>
        public float TravelPx;
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

        // Every key, built ONCE at type init. The keys are needed in two places that both run every
        // frame — the draw (one key) and the warm/evict sweep (all Count of them) — and building
        // them on demand allocated a char[] and two strings per call, which is the no-allocation
        // rule in DisplayList's header broken at the least visible place. A 36-entry table is 36
        // strings, for the life of the process.
        static readonly string[] Keys = BuildKeys();

        static string[] BuildKeys()
        {
            string[] k = new string[Count];
            for (int f = 0; f < Count; f++)
            {
                // Three digits, zero-padded, no culture in play: 36 frames today, and 72 (§5's
                // fallback) still fits without renaming the set.
                char[] d = new char[3];
                d[0] = (char)('0' + (f / 100) % 10);
                d[1] = (char)('0' + (f / 10) % 10);
                d[2] = (char)('0' + f % 10);
                k[f] = KeyPrefix + new string(d);
            }
            return k;
        }

        /// <summary>The asset key for a frame, wrapped — so a caller that computed 36 or -1 gets a
        /// real frame rather than a missing-asset warning.</summary>
        public static string Key(int frame) { return Keys[WrapFrame(frame)]; }

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

        // ---- THE GESTURE: PRESS, DRAG, RELEASE (T11b items 4 + 5) ---------------------------
        //
        // ---- WHY THE GESTURE IS PURE AND NOT LEFT IN THE GLUE ----
        // ScreenTouch can only be exercised in the capsule: it needs InternalCamera, a collider and
        // a mouse. So everything it could get WRONG is moved off it and into here - where the press
        // remembers its x, how a move turns into a call to Drag, how far a finger travelled, and the
        // one judgement call in the whole gesture (was that a drag or a tap?). What is left on the
        // glue side is three one-line forwards, which is the least a headless test can be denied.
        //
        // ---- THE RESET IS A TAP ON THE VEHICLE, NOT A BUTTON ----
        // Section 5's C4 asks for "a reset/front tap". A tap on the capsule itself is the whole
        // control: it needs no new chrome on a page whose layout is measured from the reference, and
        // it is the gesture the object already invites - you grabbed it to turn it, you tap it to
        // let it go back. Nothing else on the page moves, and nothing new is drawn.
        //
        // ---- WHAT COUNTS AS A TAP, AND WHY IT IS MEASURED IN FRAMES ----
        // Not "under N pixels": N would mean a different gesture on the 1280 preview, the 2560
        // RenderTexture and the 2x cover render, exactly as a fixed pixels-per-frame would (see the
        // drag header). It is measured in FRAMES OF ROTATION, through the same gearing: a press and
        // release that turned the vehicle less than half a frame never showed the crew a different
        // sprite, so calling it a tap cannot contradict anything they saw. Travel is the total PATH,
        // not the net displacement, so a wiggle that ends where it started is a drag, not a tap.

        /// <summary>Total travel, in frames of rotation, a press-and-release may cover and still be
        /// a tap. Half a frame: see the header - below this the sprite on the glass never changed.</summary>
        public const float TapSlopFrames = 0.5f;

        /// <summary>No press in progress.</summary>
        public static TurntableTouch Idle()
        {
            TurntableTouch g; g.Dragging = false; g.LastX = 0f; g.TravelPx = 0f; return g;
        }

        /// <summary>A press landed on the capsule at page x. Nothing turns yet - a press that never
        /// moves is a tap, and which of the two it was is only known at release.</summary>
        public static TurntableTouch Press(float px)
        {
            TurntableTouch g; g.Dragging = true; g.LastX = px; g.TravelPx = 0f; return g;
        }

        /// <summary>
        /// A drag sample: the pointer is now at page x. Returns where the turntable has turned to;
        /// <paramref name="moved"/> is the advanced gesture. A sample arriving with no press in
        /// progress is ignored rather than treated as a press - the glue gets a move every frame the
        /// button is held, including ones whose press was claimed by some other control.
        /// </summary>
        public static TurntableState Move(TurntableState s, TurntableTouch g, float px,
                                          float slotWidthPx, out TurntableTouch moved)
        {
            moved = g;
            if (!g.Dragging) return s;
            if (float.IsNaN(px) || float.IsInfinity(px)) return s;   // a sample off a failed raycast

            float dx = px - g.LastX;
            moved.LastX = px;
            moved.TravelPx = g.TravelPx + Math.Abs(dx);
            return Drag(s, dx, slotWidthPx);
        }

        /// <summary>Was this press-and-release a tap rather than a drag? See TapSlopFrames.</summary>
        public static bool IsTap(TurntableTouch g, float slotWidthPx)
        {
            if (!g.Dragging) return false;
            float frames = DragFrames(g.TravelPx, slotWidthPx);
            if (frames < 0f) frames = -frames;
            return frames <= TapSlopFrames;
        }

        /// <summary>
        /// The press ended. A tap resets the sequence to the authored front (C4); a real drag leaves
        /// the vehicle where the finger left it. Either way the gesture goes idle.
        /// </summary>
        public static TurntableState Release(TurntableState s, TurntableTouch g, float slotWidthPx,
                                             out TurntableTouch idle)
        {
            bool tap = IsTap(g, slotWidthPx);
            idle = Idle();
            return tap ? Front() : s;
        }

        // ---- RESIDENCY: WHICH FRAMES MAY BE IN MEMORY (T11b item 6) -------------------------
        //
        // ---- THE NUMBER THAT MAKES THIS A POLICY AND NOT A CACHE ----
        // The sequence is 512x1024 RGBA. That decodes to 2 MB of texture PER FRAME, so a crew member
        // who drags one full revolution touches all 36 and leaves ~75 MB resident - for a decoration
        // on one view of one page. ImageStore's ordinary rule (load once, keep for ever) is right for
        // the couple of dozen page assets and wrong here, and it is wrong by two orders of magnitude.
        //
        // ---- A WINDOW, NOT AN LRU ----
        // The frames a drag is about to want are known: they are the ones either side of where it is
        // now. So the resident set is a WINDOW around the current frame - a pure function of the
        // frame, with no use-order to keep, nothing to age, and no way for it to grow. WarmRadius 2
        // is 20 degrees of look-ahead each way, which a slow drag never outruns and a fast one
        // outruns whatever the radius is; five frames is 10 MB.
        //
        // ---- WHY CENTRES IS AN ARRAY ----
        // Three screens share one ImageStore and any of them may be showing the capsule, at its own
        // angle. One shared window would then be evicted and reloaded by each screen in turn - a
        // disk read PER FRAME, which is worse than the hitch this exists to remove. So residency is
        // the UNION over the screens, and a screen that is not showing the view says so with
        // NotShowing and holds nothing. When no screen is showing it, the whole sequence goes.
        //
        // ---- THE FRONT IS PINNED WHILE ANYONE IS LOOKING ----
        // Frame 0 is what the view opens on and what the reset tap snaps to, so it is the one frame
        // whose load is guaranteed to be noticed. It costs 2 MB to keep it ready.

        /// <summary>A screen that is not showing the capsule view. Any negative value means this;
        /// the constant exists so the glue does not spell -1 and mean something else by it.</summary>
        public const int NotShowing = -1;

        /// <summary>Frames either side of the current one that are kept loaded.</summary>
        public const int WarmRadius = 2;

        /// <summary>Frames in one screen's window - the current one and WarmRadius each side.</summary>
        public const int WarmSteps = WarmRadius * 2 + 1;

        /// <summary>Decoded bytes one frame costs: RGBA at the nominal sprite size.</summary>
        public const int FrameBytes = FrameW * FrameH * 4;

        /// <summary>
        /// The i'th window offset, NEAREST FIRST: 0, -1, +1, -2, +2. The order is the point - the
        /// glue warms one frame per draw rather than five in the frame the view opens, so arriving
        /// costs a little on each of five frames instead of landing as one hitch, and the frame
        /// being LOOKED at is always the one warmed first.
        /// </summary>
        public static int WarmOffset(int i)
        {
            if (i < 0 || i >= WarmSteps) return 0;
            int off = (i + 1) / 2;
            return ((i & 1) == 1) ? -off : off;
        }

        /// <summary>Shortest distance between two frames, going either way round the seam: frames 35
        /// and 0 are ONE apart, not thirty-five - which is the whole reason this is not a
        /// subtraction.</summary>
        public static int Distance(int a, int b)
        {
            int d = WrapFrame(a) - WrapFrame(b);
            if (d < 0) d = -d;
            if (d > Count - d) d = Count - d;
            return d;
        }

        /// <summary>Is this frame inside one screen's warm window? A NotShowing centre has no window
        /// at all.</summary>
        public static bool InWindow(int frame, int centre)
        {
            if (centre < 0) return false;
            return Distance(frame, centre) <= WarmRadius;
        }

        /// <summary>
        /// May this frame be in memory, given what every screen is showing? True inside any screen's
        /// window, and for the pinned front while at least one screen is showing the view. False for
        /// EVERY frame when none is - which is what releases the sequence when the crew leaves.
        /// </summary>
        public static bool IsResident(int frame, int[] centres)
        {
            if (centres == null) return false;
            bool anyShowing = false;
            for (int i = 0; i < centres.Length; i++)
            {
                if (centres[i] < 0) continue;
                anyShowing = true;
                if (InWindow(frame, centres[i])) return true;
            }
            return anyShowing && WrapFrame(frame) == FrontFrame;
        }

        /// <summary>How many frames the policy allows to be resident right now. The number the
        /// memory claim is made of, and what a test asserts against Count.</summary>
        public static int ResidentCount(int[] centres)
        {
            int n = 0;
            for (int f = 0; f < Count; f++) if (IsResident(f, centres)) n++;
            return n;
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
