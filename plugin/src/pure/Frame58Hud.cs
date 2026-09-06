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
        // ⚠ RAISED FROM 20 BY [[S133]], 2026-09-06. The page used to be five draws and a raster. It now
        // carries S132's two patches and two live values, and an ALERT ACTIVITY list of up to twelve
        // rows at two commands each - a label and a value. 20 + 4 + 24 = 48, and the headroom above
        // that is for the list growing rather than for guesswork.
        public const int Commands = 64;
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
            DrawAttitudeBlock(dl, w, ox, sc, s);
            DrawAlertActivity(dl, w, ox, sc, s);

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

        /// <summary>
        /// ⭐ [[S154b]] / QC `H-02` / H10: THE ATTITUDE BLOCK, LIVE.
        ///
        /// ⛔ WHAT WAS WRONG, AND WHY IT IS THE WORST OF THE TWELVE. QC `H-02` found that 8 of the 12
        /// baked numbers on this frame contradict live state IN THE SAME FRAME. Six of them are here —
        /// ROLL / PITCH / YAW and their rates — and an attitude readout contradicting the vehicle's
        /// actual attitude is the single worst case on a page whose whole job is attitude. The bowl
        /// beside them has been live since T5; the numbers under it were a picture of someone else's
        /// docking.
        ///
        /// ⭐ ZERO NEW DATA AND ZERO NEW MODEL. All six fields are already published and already
        /// pre-formatted, and `DockingSimPage` draws the same six today — so this is a DRAWING change,
        /// not a telemetry one, and the two surfaces cannot disagree about the vehicle's attitude
        /// because they read the same fields (C7.1).
        ///
        /// ⚠ THE COLOUR SPLIT IS THE DRAWING'S OWN, and it is how value was told from rate when
        /// [[S154a]] measured the boxes: the values are green (`#1FE327`) and the rates cyan
        /// (`#20FBFD`). Those are `DragonPalette.Go` and `DragonPalette.Accent` EXACTLY — checked, not
        /// approximated — so the live text lands in the frame's own two inks.
        ///
        /// ⛔ AND THE BAKED INK IS COVERED FIRST. `Patch` erases each box in the ground it sits on,
        /// which S154a measured as flat `DragonPalette.Background` over every one of them. Drawing over
        /// baked ink without erasing it is how you get two numbers in one box, which is worse than
        /// either.
        /// </summary>
        static void DrawAttitudeBlock(DisplayList dl, int w, float ox, float sc, PageState s)
        {
            // The floor, in this frame's design units — the same call `DrawBottomRow` uses, and for the
            // same reason: the row clears [[S153]]'s legibility floor by construction rather than by
            // matching whatever size the export happened to bake (which was ~26 design px, 54 %).
            float live = Typography.MinDesignFor(w, sc);

            Readout(dl, ox, sc, live, Frame58Map.RollValue,  s.Valid ? s.RollDegText   : null, DragonPalette.Go);
            Readout(dl, ox, sc, live, Frame58Map.PitchValue, s.Valid ? s.PitchDegText  : null, DragonPalette.Go);
            Readout(dl, ox, sc, live, Frame58Map.YawValue,   s.Valid ? s.YawDegText    : null, DragonPalette.Go);
            Readout(dl, ox, sc, live, Frame58Map.RollRate,   s.Valid ? s.RollRateText  : null, DragonPalette.Accent);
            Readout(dl, ox, sc, live, Frame58Map.PitchRate,  s.Valid ? s.PitchRateText : null, DragonPalette.Accent);
            Readout(dl, ox, sc, live, Frame58Map.YawRate,    s.Valid ? s.YawRateText   : null, DragonPalette.Accent);
        }

        /// <summary>
        /// One baked box replaced by one live value, centred where the baked ink was.
        ///
        /// ⚠ A NULL OR EMPTY VALUE DRAWS THE DASH, DIMMED — and it still PATCHES first, which is the
        /// part that is easy to get wrong. Skipping the patch on a dead feed would leave the baked
        /// number showing, so the page would print a confident attitude exactly when it has none. That
        /// is the [[S147]] rule (`E4`: never state a value the vehicle has not supplied) applied to a
        /// raster instead of to a string.
        ///
        /// ⚠ CENTRED ON THE BOX'S OWN CENTRE, not hung from its top-left: the box is an INK bounding
        /// box (see `Frame58Map`'s header), so its centre is where the number looked centred in the
        /// export, and the replacement is a different size. Anchoring at the top would walk every value
        /// upward by half the size difference.
        /// </summary>
        static void Readout(DisplayList dl, float ox, float sc, float size,
                            Frame58Map.Box b, string text, Rgba ink)
        {
            Patch(dl, ox, sc, b);
            bool have = !string.IsNullOrEmpty(text);
            dl.Text(have ? text : Dashes.None, ox + b.Cx * sc, (b.Cy - size * 0.5f) * sc, size * sc,
                    TextAlign.Centre, have ? ink : DragonPalette.Text6);
        }

        /// <summary>Reused across frames - the draw path allocates nothing.</summary>
        static readonly AlertItem[] alertScratch = new AlertItem[AlertActivity.Max];

        /// <summary>
        /// S133 / QC `H-05`: fill the ALERT ACTIVITY panel, which had 822 px of nothing under its own
        /// title while `Alarms` folded the whole vehicle every frame and threw the answer away.
        ///
        /// ⭐ Nothing is patched here. Unlike the CAMERA row and the timer, this region is genuinely
        /// EMPTY in the raster - measured, not assumed (see `Frame58Map.AlertPanel`) - so the rows are
        /// simply drawn into it. No baked ink is covered and nothing can be lost behind a patch.
        ///
        /// ⚠ A QUIET VEHICLE AND A DEAD FEED GET DIFFERENT WORDS. `AlertActivity.EmptyText` answers
        /// both cases, and they must never read alike (rule E4).
        /// </summary>
        static void DrawAlertActivity(DisplayList dl, int w, float ox, float sc, PageState s)
        {
            Frame58Map.Box p = Frame58Map.AlertPanel;
            float size = Typography.MinDesignFor(w, sc);
            float pitch = size * 1.28f;
            float x = ox + p.X0 * sc;

            int n = AlertActivity.Build(s, alertScratch);
            if (n == 0)
            {
                dl.Text(AlertActivity.EmptyText(s), x, p.Y0 * sc, size * sc, TextAlign.Left,
                        DragonPalette.Text6);
                return;
            }

            // ⚠ CLIPPED BY THE PANEL, NOT BY THE ARRAY. A list longer than the space drops its
            // QUIETEST rows, because they are the ones at the bottom - worst-first ordering makes the
            // clip safe. A panel that ran off the frame would hide whatever it overflowed onto.
            int fit = (int)((p.H) / pitch);
            if (fit < 1) fit = 1;
            int rows = (n < fit) ? n : fit;

            for (int i = 0; i < rows; i++)
            {
                float y = (p.Y0 + i * pitch) * sc;
                Rgba ink = Alarms.Colour(alertScratch[i].Sev);
                dl.Text(alertScratch[i].Label, x, y, size * sc, TextAlign.Left, ink);
                dl.Text(alertScratch[i].Value, ox + p.X1 * sc, y, size * sc, TextAlign.Right, ink);
            }
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
