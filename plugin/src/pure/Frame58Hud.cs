// DragonScreen — Frame58Hud  (PURE: the Figma "Frame 58" attitude / docking HUD)
// ============================================================================================
// The docking HUD. The raw game navball read as a mess on the glass (a photographic sphere with
// mirrored numerals) next to the design's clean SYNTHETIC instrument, so this now shows the Figma
// frame itself (art/cover/frame58.png) — the light-blue attitude bowl + graticule, corner thruster
// rings, acceleration gauge — exactly as designed, fit to height and centred so the ball stays round.
//
// The owner's nose-cone rule is preserved: with the nose cone CLOSED the light-blue bowl shows; when
// it is toggled OPEN the LIVE docking-camera feed (ImageId.DockingCamLive) fills the bowl (clipped to
// the circle) with the centre crosshair over it. Making the attitude readouts live again is a later
// pass — a synthetic tilting bowl, not the navball.
// ============================================================================================
namespace DragonScreen
{
    public static class Frame58Hud
    {
        public const int Commands = 20;
        const float RefW = 3427f, RefH = 2112f;

        // the light-blue attitude bowl, from the frame metadata (Ellipse 6 centre) — the disc the
        // docking camera fills when the nose cone is open.
        const float BowlCx = 1706f, BowlCy = 984f, BowlR = 470f;
        static readonly Rgba BowlBlue = Rgba.Hex("2C4A7E");   // masks the cam's square corners to the bowl

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f;

            dl.Rect(0, 0, w, h, DragonPalette.Background);
            dl.Asset("frame58", ox, 0f, RefW * sc, h, DragonPalette.White);

            // nose cone open -> the docking camera fills the bowl; else the frame's own light-blue bowl.
            if (s.Steps.NoseConeOpen)
            {
                float bcx = ox + BowlCx * sc, bcy = BowlCy * sc, r = BowlR * sc;
                dl.ImageCircle(ImageId.DockingCamLive, bcx - r, bcy - r, 2f * r, 2f * r, DragonPalette.White, BowlBlue);
                TargetReticle.Crosshair(dl, bcx, bcy, r * 0.14f, DragonPalette.Text2);
            }

            // ---- S132 / H11: THE BOTTOM ROW — CAMERA, AND THE TIMER'S OWN NUMBER --------------
            // ⛔ Both are BAKED INTO THE RASTER, so making them live means covering the baked ink and
            // redrawing. That is [[S154a]]'s route 3, and it is safe here for the reason S154a
            // measured: every one of these boxes sits on a locally FLAT ground, and that ground is an
            // exact palette colour — `DragonPalette.Background`, sampled (2, 7, 56) inside the plates
            // and inside the timer panel. A patch in Background is invisible except where it erases.
            // ⚠ FRAME IS DELIBERATELY NOT TOUCHED. LVLH is a constant with no second value anywhere in
            // the vessel, the snapshot or the reference — see `Frame58Controls`' header. The baked row
            // is correct, and redrawing a correct constant would only add a way to get it wrong.
            DrawBottomRow(dl, w, ox, sc, s);

            // "MANUAL DOCKING" entry in the letterbox margin (screen-space, so it never overlaps the
            // fit-to-height frame art). Opens the manual docking screen.
            //
            // S108 / QC H-04: the box used to be written here AND, with DIFFERENT constants, in
            // FigmaUI.HitTest - drawn 0.44..0.56 h, hit 0.40..0.60 h - so a tap on empty letterbox up to
            // 28 px above or below the visible button silently navigated. One rect now, in
            // MarginAffordance, shared by the draw and the hit test the way PageAction has always
            // required and the way the Cover already does it.
            // S108 / QC H-06: and the type is sized from the BOX now, not from the panel height. It was
            // h * 0.020 against a box derived from the letterbox WIDTH - two unrelated quantities - so
            // "MANUAL" rendered 56 px of ink in a 45.6 px box, overhanging its own border by 4.0 px left
            // and 5.4 px right at the shipped size.
            MarginAffordance.Draw(dl, w, h, "MANUAL", "DOCKING");

            // full-width bottom status bar over the frame so it reaches both edges.
            BottomBar.Draw(dl, w, h, s);   // S103: undistorted, in the design frame; S147: CURRENT STATE live
        }

        /// <summary>
        /// S132: the CAMERA row and the timer's value, drawn live over their baked ink.
        ///
        /// ⚠ CAMERA IS ONE MERGED PATH, caption and value together — `Frame58Map.CameraRow`'s 46.9 px
        /// height is two lines, not one. So the patch takes both and both are redrawn: the caption at
        /// the small size, the value under it. Erasing only the value would have taken "CAMERA" with it.
        ///
        /// ⭐ The sizes come from `Typography.MinDesignFor`, so this row clears [[S153]]'s legibility
        /// floor by construction rather than by matching whatever the export happened to bake.
        /// </summary>
        static void DrawBottomRow(DisplayList dl, int w, float ox, float sc, PageState s)
        {
            // ⛔ THE PANEL WIDTH AND THE FRAME SCALE, both of them. `MinDesignFor(panelW, frameScale)`
            // converts the PANEL-pixel floor into this frame's design units, so it needs the real `w`
            // — a first version passed `RefW` and `1f`, which is the design frame measuring itself and
            // has nothing to do with how large the glass is.
            float live = Typography.MinDesignFor(w, sc);

            // ---- ⚠ AND ONLY THE VALUE IS REDRAWN. THE BAKED CAPTION STAYS. --------------------
            // A first version patched the whole `CameraRow` box and redrew "CAMERA" above the value.
            // It did not fit and the preview showed why: the baked row is **46.9 design px for TWO
            // lines** — a 12 px caption over an 18 px value — and both are far under the legibility
            // floor. Two lines at the floor need ~84 px in an 85 px plate with no leading, and the
            // result overflowed the plate and spilled onto the frame.
            // ⭐ So the caption is left BAKED and only its value band is patched. That is honest and it
            // is also the correct division of labour: making this row LIVE is S132's job, and raising
            // the page's type to the floor is [[S153]]'s — the caption is existing artwork, not new
            // text this line is adding below the floor.
            // ⚠ The bands below are MEASURED, not chosen: a row profile of `frame58.png` puts the
            // CAMERA caption's ink at y 1780.9-1792.6 and its value's at 1807.7-1826.1, and a fill
            // scan puts the plate at x 2041.5-2319.2, y 1760.8-1846.1. The patch starts below the
            // caption and ends at the plate's own edge.
            const float PlateBot = 1846.1f, ValueTop = 1796.5f;
            const float CamPlateX0 = 2041.5f, CamPlateX1 = 2319.2f;

            dl.Rect(ox + CamPlateX0 * sc, ValueTop * sc,
                    (CamPlateX1 - CamPlateX0) * sc, (PlateBot - ValueTop) * sc,
                    DragonPalette.Background);
            dl.Text(Frame58Controls.CameraText(s), ox + (CamPlateX0 + CamPlateX1) * 0.5f * sc,
                    ValueTop * sc, live * sc, TextAlign.Centre, DragonPalette.White);

            // ---- THE TIMER'S NUMBER ----
            // ⚠ RESET and START are NOT redrawn: the baked plates are the buttons, and
            // `Frame58Controls.HitTest` matches them. Only the value moves. The patch is wider than the
            // baked "0s" because a live value is longer — "3661s" against "0s" — and the panel's whole
            // interior is the same flat Background, so the extra width costs nothing.
            Frame58Map.Box t = Frame58Map.TimerValue;
            const float TimerPatchX0 = 3000f, TimerPatchX1 = 3300f;
            float ty = t.Cy - live * 0.5f;
            dl.Rect(ox + TimerPatchX0 * sc, (ty - 4f) * sc,
                    (TimerPatchX1 - TimerPatchX0) * sc, (live + 8f) * sc, DragonPalette.Background);
            dl.Text(Frame58Controls.TimerText(s.HudTimerSeconds),
                    ox + (TimerPatchX0 + TimerPatchX1) * 0.5f * sc, ty * sc, live * sc,
                    TextAlign.Centre, DragonPalette.White);
        }

        /// <summary>Cover a baked box in the ground it sits on, so live text can replace it.
        /// ⚠ A 2-design-px bleed: the glyph bounds are the INK, and antialiasing puts a little of it
        /// outside. Measured flat over that margin too (S154a), so the bleed costs nothing.</summary>
        static void Patch(DisplayList dl, float ox, float sc, Frame58Map.Box b)
        {
            const float Bleed = 2f;
            dl.Rect(ox + (b.X0 - Bleed) * sc, (b.Y0 - Bleed) * sc,
                    (b.W + Bleed * 2f) * sc, (b.H + Bleed * 2f) * sc, DragonPalette.Background);
        }
    }
}
