/*
 * DragonScreen - DockingPage
 *
 * PURE. DOCKING, laid out from the REFERENCE'S OWN SOURCE rather than from a picture of it.
 *
 * ---- EVERY NUMBER IN THIS FILE IS QUOTED, NOT CHOSEN ----
 * The previous version was designed from `Frame 58.svg`, a static Figma export, plus notes written
 * while looking at the live demo. It put the rings dead centre with the attitude ball inside them,
 * the offsets in a left column and the attitude in a right column. That is wrong in every particular,
 * and it cost a rebuild to find out - because a render loses the layout and a screenshot adds
 * perspective error, while the source states it exactly.
 *
 * The source is `assets/reference/dragon2-ui-master/src/views/Second.vue`, extracted into
 * `docs/UI_AUDIT.md` by `plugin/build/audit_ui.py`. Its style block says, verbatim:
 *
 *      #hud-darken    top 50%  left 50%   min(85vh, 100vw * 0.714) square
 *      #hud-ring      centred, HALF the darken size
 *      #upper-left    top 3%     left 14.59%     200px   ACCELERATION
 *      #upper-right   top 3%     right 14.59%    200px   RCS translate + rotate
 *      #lower-left    bottom 8%  left 14.59%     200px   the ring behind the navball
 *      #navball       bottom 8%  left 14.59%     200px
 *      #lower-right   bottom 8%  right 14.59%    200px   RCS up/down + roll
 *      #roll-number   top 12.5%  centred         around the ring, not in a column
 *      #yaw-number    top 85%    centred
 *      #pitch-number  top 50%    left 86%
 *      #xyz-number    left 14%   top 47 / 50 / 53%
 *      RANGE / RATE   top 72% (label) and 77% (value), at left 25% and 75%
 *
 * FOUR 200 px CORNER RINGS ON A SHARED INSET is the whole shape of the page. Keep it.
 *
 * ---- THE TWO RIGHT-HAND RINGS ARE CONTROL INPUT, NOT CLAMPS ----
 * Read off the template: the upper-right ring lights quadrant wedges from `currentMajorKey`
 * 87/68/65/83 - W A S D, translation - and inner wedges from `currentArrowKey` 38/40/37/39, which are
 * pitch and yaw. The lower-right ring lights from `currentMinorKey` 82 - R, with F its pair - and
 * Q/E for roll. Their own legend agrees: `WASD - MOVE`, `R | F - UP | DOWN`, `Q | E - ROLL`.
 *
 * So these are RCS demand indicators. For us that is `FlightCtrlState`, which is real, live, and
 * moves when the pilot moves - a better fit than it looks, because a keyboard indicator and a
 * translation indicator are the same instrument once the input is analogue.
 */
namespace DragonScreen
{
    public static class DockingPage
    {
        // ---- the shared corner inset, quoted from the source ----
        private const float InsetX = 0.1459f;   // left: 14.59% / right: 14.59%
        private const float InsetTop = 0.03f;   // top: 3%
        private const float InsetBottom = 0.08f;// bottom: 8%

        /// <summary>
        /// Corner ring diameter as a fraction of the page body.
        ///
        /// The source says 200 px, but 200 px of THEIR viewport is not 200 px of a 1280x639 IVA
        /// panel. Scaling off the body height keeps the ring the same size RELATIVE to the insets it
        /// shares an edge with, which is what makes the four-corner shape read.
        /// </summary>
        private const float RingFraction = 0.30f;

        /// <summary>Body height for a page of this height - everything above the chrome bar.</summary>
        public static float BodyHeight(int h) { return h - ChromeBar.Height; }

        /// <summary>The central HUD square: min(85% of the body, 71.4% of the width).</summary>
        public static float HudSize(int w, int h)
        {
            float byHeight = BodyHeight(h) * 0.85f;
            float byWidth = w * 0.714f;
            return (byHeight < byWidth) ? byHeight : byWidth;
        }

        public static float RingDiameter(int h) { return BodyHeight(h) * RingFraction; }

        /// <summary>
        /// Centre of corner ring <paramref name="corner"/>: 0 upper-left, 1 upper-right,
        /// 2 lower-left, 3 lower-right. ONE function, used by drawing and by the tests.
        /// </summary>
        public static void CornerCentre(int corner, int w, int h, out float cx, out float cy)
        {
            float d = RingDiameter(h), body = BodyHeight(h);
            bool right = (corner == 1 || corner == 3);
            bool lower = (corner == 2 || corner == 3);

            cx = right ? (w * (1f - InsetX) - d * 0.5f) : (w * InsetX + d * 0.5f);
            cy = lower ? (body * (1f - InsetBottom) - d * 0.5f) : (body * InsetTop + d * 0.5f);
        }

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            float body = BodyHeight(h);
            float cx = w * 0.5f, cy = body * 0.5f;
            float hud = HudSize(w, h);
            float ringD = RingDiameter(h);

            // ---- THE LIVE VIEW, EDGE TO EDGE ----
            // Drawn first and full bleed: it is the background, not a panel. If the camera is not
            // running the page simply has a dark background and every instrument still works.
            dl.Rect(0f, 0f, w, body, DragonPalette.Background);
            dl.Image(ImageId.DockingCamLive, 0f, 0f, w, body, DragonPalette.White);

            // The vignette sits over it so white text reads against a sunlit target.
            dl.Image(ImageId.HudDarken, cx - hud * 0.5f, cy - hud * 0.5f, hud, hud,
                     DragonPalette.White);

            if (!s.Valid || !s.HasTarget)
            {
                dl.Text("DOCKING", cx, cy - 40f, Typography.Hero, TextAlign.Centre,
                        DragonPalette.Text4);
                dl.Text("NO TARGET SELECTED", cx, cy + 20f, Typography.Body, TextAlign.Centre,
                        DragonPalette.Text7);
                Corners(dl, w, h, s, ringD);      // the control rings are still live and useful
                return;
            }

            dl.Text(s.TargetName ?? "TARGET", cx, 16f, Typography.Body, TextAlign.Centre,
                    DragonPalette.Text1);

            // ---- THE CENTRAL HUD ----
            float ringSize = hud * 0.5f;                       // #hud-ring is HALF the darken size
            float ix, iy, iw, ih;
            if (Images.FitHeight(ImageId.HudRing, cx, cy, ringSize, out ix, out iy, out iw, out ih))
                dl.Image(ImageId.HudRing, ix, iy, iw, ih, DragonPalette.Text3);
            if (Images.FitHeight(ImageId.HudRingInner, cx, cy, ringSize * 0.62f,
                                 out ix, out iy, out iw, out ih))
                dl.Image(ImageId.HudRingInner, ix, iy, iw, ih, DragonPalette.Text4);

            dl.Rect(cx - 18f, cy - 1f, 36f, 2f, DragonPalette.Text2);
            dl.Rect(cx - 1f, cy - 18f, 2f, 36f, DragonPalette.Text2);

            // Everything below is placed in HUD-BOX fractions, exactly as the source does.
            float hx = cx - hud * 0.5f, hy = cy - hud * 0.5f;

            // ROLL top-centre, YAW bottom-centre, PITCH right - AROUND the ring, which is the part
            // our old two-column version got most wrong.
            Readout(dl, hx + hud * 0.50f, hy + hud * 0.125f, "ROLL", s.RollText, TextAlign.Centre);
            Readout(dl, hx + hud * 0.50f, hy + hud * 0.85f, "YAW", s.YawText, TextAlign.Centre);
            Readout(dl, hx + hud * 0.86f, hy + hud * 0.50f, "PITCH", s.PitchText, TextAlign.Centre);

            // X / Y / Z stacked at left 14%, on the three rows the source uses.
            dl.Text("X", hx + hud * 0.14f - 46f, hy + hud * 0.47f, Typography.Caption,
                    TextAlign.Left, DragonPalette.Text5);
            dl.Text(s.OffXText ?? "-", hx + hud * 0.14f + 46f, hy + hud * 0.47f, Typography.Caption,
                    TextAlign.Right, DragonPalette.Text0);
            dl.Text("Y", hx + hud * 0.14f - 46f, hy + hud * 0.50f + 8f, Typography.Caption,
                    TextAlign.Left, DragonPalette.Text5);
            dl.Text(s.OffYText ?? "-", hx + hud * 0.14f + 46f, hy + hud * 0.50f + 8f,
                    Typography.Caption, TextAlign.Right, DragonPalette.Text0);
            dl.Text("Z", hx + hud * 0.14f - 46f, hy + hud * 0.53f + 16f, Typography.Caption,
                    TextAlign.Left, DragonPalette.Text5);
            dl.Text(s.OffZText ?? "-", hx + hud * 0.14f + 46f, hy + hud * 0.53f + 16f,
                    Typography.Caption, TextAlign.Right, DragonPalette.Text0);

            // RANGE and RATE under the ring at 25% and 75%, label then value. Green in the source,
            // and they earn it: these are the two numbers a manual approach is flown on.
            dl.Text("RANGE", hx + hud * 0.25f, hy + hud * 0.72f, Typography.Caption,
                    TextAlign.Centre, DragonPalette.Text6);
            dl.Text(s.RangeText ?? "-", hx + hud * 0.25f, hy + hud * 0.77f, Typography.Value,
                    TextAlign.Centre, DragonPalette.Go);
            dl.Text("RATE", hx + hud * 0.75f, hy + hud * 0.72f, Typography.Caption,
                    TextAlign.Centre, DragonPalette.Text6);
            dl.Text(s.RateText ?? "-", hx + hud * 0.75f, hy + hud * 0.77f, Typography.Value,
                    TextAlign.Centre,
                    s.ClosingFast ? DragonPalette.Alarm
                                  : s.Closing ? DragonPalette.Go : DragonPalette.Caution);

            // The alignment sweep stays on the inner ring. It is a DEVIATION, so it keeps threshold
            // colour where the measuring dials do not - see Pages.Docking's old note, still true.
            float ar = ringSize * 0.30f;
            Gauge.Ring(dl, cx, cy, ar, ar * 0.06f, s.Align01,
                       DragonPalette.Inset1, Alarms.Colour(Alarms.High(s.Align01)));

            Corners(dl, w, h, s, ringD);
        }

        private static void Readout(DisplayList dl, float x, float y, string caption, string value,
                                    TextAlign align)
        {
            dl.Text(caption, x, y, Typography.Caption, align, DragonPalette.Text6);
            dl.Text(value ?? "-", x, y + 20f, Typography.Body, align, DragonPalette.Text0);
        }

        // ------------------------------------------------------------------ the four corner rings

        private static void Corners(DisplayList dl, int w, int h, PageState s, float d)
        {
            float r = d * 0.5f;
            float cx, cy;

            // Each ring sits on its own disc - `.corner-circles-base` is a circle of r=49 in a 100
            // box, stroked white 0.5, filled #020738, which is our Background exactly.
            for (int i = 0; i < 4; i++)
            {
                CornerCentre(i, w, h, out cx, out cy);
                dl.ArcBand(cx, cy, 0f, r, 0.0, 360.0, DragonPalette.Background);
                dl.ArcBand(cx, cy, r - 1.5f, r, 0.0, 360.0, DragonPalette.Text5);
            }

            // ---- UPPER LEFT: ACCELERATION ----
            // #d7b733 is quoted from the source, and it is the SAME mustard as PPO2 - so the
            // identity-colour rule reaches here too. Our G-FORCE dial on FLIGHT was assigned red by
            // a family argument; the reference disagrees, and the reference wins.
            CornerCentre(0, w, h, out cx, out cy);
            Gauge.Labelled(dl, cx, cy, r * 0.78f, r * 0.16f, s.Valid ? s.GForce01 : 0.0,
                           s.Valid ? s.GForceText : "-", "g", "ACCELERATION",
                           DragonPalette.GaugeTrack, DragonPalette.GaugeAcceleration);

            // ---- LOWER LEFT: the attitude ball, inside its ring ----
            CornerCentre(2, w, h, out cx, out cy);
            float ballD = (r - 10f) * 2f;
            if (ballD > 0f)
                dl.Image(ImageId.NavBallLive, cx - ballD * 0.5f, cy - ballD * 0.5f, ballD, ballD,
                         DragonPalette.White);

            // ---- UPPER RIGHT: translation X/Y and rotation pitch/yaw ----
            CornerCentre(1, w, h, out cx, out cy);
            Quadrants(dl, cx, cy, r, s.Valid ? s.TransX : 0f, s.Valid ? s.TransY : 0f,
                      s.Valid ? s.RotYaw : 0f, s.Valid ? s.RotPitch : 0f);
            dl.Text("RCS", cx, cy + r * 0.62f, Typography.Caption, TextAlign.Centre,
                    s.RcsOn ? DragonPalette.Accent : DragonPalette.Text7);

            // ---- LOWER RIGHT: translation Z (up/down) and roll ----
            CornerCentre(3, w, h, out cx, out cy);
            Quadrants(dl, cx, cy, r, s.Valid ? s.RotRoll : 0f, s.Valid ? s.TransZ : 0f, 0f, 0f);
            dl.Text("Z / ROLL", cx, cy + r * 0.62f, Typography.Caption, TextAlign.Centre,
                    DragonPalette.Text6);
        }

        /// <summary>
        /// Four wedges around a ring, each lit in proportion to a signed demand.
        ///
        /// Outer band takes the horizontal/vertical pair, inner band the second pair - the source's
        /// arrangement, where WASD lights the outer quadrants and the arrow keys light inner ones.
        /// Ours are ANALOGUE: a wedge brightens with how far the axis is pushed, because
        /// FlightCtrlState is a float and throwing that away to draw a keyboard would be a downgrade.
        /// </summary>
        private static void Quadrants(DisplayList dl, float cx, float cy, float r,
                                      float horizontal, float vertical, float inHoriz, float inVert)
        {
            float outerIn = r * 0.62f, outerOut = r * 0.88f;
            float innerIn = r * 0.30f, innerOut = r * 0.56f;

            // A GAP between wedges, or the four of them abut into a solid ring and the quadrants
            // stop being readable as separate axes - which is what the first render showed.
            const double G = 4.0;
            Wedge(dl, cx, cy, outerIn, outerOut, 45.0 + G, 135.0 - G, horizontal);   // right
            Wedge(dl, cx, cy, outerIn, outerOut, 225.0 + G, 315.0 - G, -horizontal); // left
            Wedge(dl, cx, cy, outerIn, outerOut, -45.0 + G, 45.0 - G, vertical);     // up
            Wedge(dl, cx, cy, outerIn, outerOut, 135.0 + G, 225.0 - G, -vertical);   // down

            Wedge(dl, cx, cy, innerIn, innerOut, 45.0 + G, 135.0 - G, inHoriz);
            Wedge(dl, cx, cy, innerIn, innerOut, 225.0 + G, 315.0 - G, -inHoriz);
            Wedge(dl, cx, cy, innerIn, innerOut, -45.0 + G, 45.0 - G, inVert);
            Wedge(dl, cx, cy, innerIn, innerOut, 135.0 + G, 225.0 - G, -inVert);
        }

        /// <summary>
        /// One wedge. Always drawn as a dim track so the control layout is readable at rest; lit in
        /// proportion when the axis is pushed that way. A demand of zero must still show WHERE the
        /// indicator would appear, or the ring looks broken rather than idle.
        /// </summary>
        private static void Wedge(DisplayList dl, float cx, float cy, float rIn, float rOut,
                                  double from, double to, float demand)
        {
            // Hairline, NOT Inset1. The ring's disc is Background and Inset1 is one step from it,
            // so the idle wedges were invisible - the same mistake as the NAV graticule and the
            // orbit globe, third time. An indicator has to be visible when it is NOT lit, or the
            // pilot cannot see where the indication would appear.
            dl.ArcBand(cx, cy, rIn, rOut, from, to, DragonPalette.Hairline);
            if (demand <= 0.02f) return;
            if (demand > 1f) demand = 1f;
            // Grow from the wedge's centre outward, so it reads as a level rather than a sweep.
            double mid = (from + to) * 0.5, half = (to - from) * 0.5 * demand;
            dl.ArcBand(cx, cy, rIn, rOut, mid - half, mid + half, DragonPalette.Accent);
        }
    }
}
