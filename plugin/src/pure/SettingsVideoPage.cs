// DragonScreen — SettingsVideoPage  (PURE: the Settings "Video" tab — the vehicle's own cameras)
// ============================================================================================
// The reference UI's Video tab used the device webcam; ours shows the VEHICLE's real cameras, exactly
// as the original DragonScreen VIDEO tab did — the forward/hull views rendered by DockingCamRenderer
// (ImageId.DockingCamLive), never a stock still. Restyled to the new theme to sit beside CABIN
// SETTINGS (frame66): title top-centre, a left CAMERA column, the live feed in a bordered box, the
// resolution readout, and the Audio/Cabin/Video tab strip (Video active).
//
// Camera list + selection come from PageState (SettingsPage.CamList / s.CameraView), which the painter
// already populates; the actual camera claim (DockingCamRenderer.Request) is wired in the painter.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class SettingsVideoPage
    {
        public const int Commands = 80;
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba Bg     = DragonPalette.Background;
        static readonly Rgba Panel  = DragonPalette.Panel;
        static readonly Rgba Accent = DragonPalette.Accent;
        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Dim    = DragonPalette.Text6;
        static readonly Rgba Hair   = DragonPalette.Hairline;

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            // ---- S153f: LIVE, for SettingsAudioPage's reason - these name cameras the crew select --
            float SZ(float v) => Typography.LiveDesign(v, w, sy) * sy;   // ⚠ TEXT ONLY here - see SettingsAudioPage's TZ for why that matters
            int St(float rs) => Strokes.Px(rs, sy);   // ONE rule, in Strokes.cs - rounds UP (R-02 family)
            void C(string t, float cx, float y, float z, Rgba c) => dl.Text(t, PX(cx), PY(y), SZ(z), TextAlign.Centre, c);
            void L(string t, float x, float y, float z, Rgba c) => dl.Text(t, PX(x), PY(y), SZ(z), TextAlign.Left, c);
            void R(string t, float rx, float y, float z, Rgba c) => dl.Text(t, PX(rx), PY(y), SZ(z), TextAlign.Right, c);

            dl.Rect(0, 0, w, h, Bg);
            C("VIDEO SETTINGS", 1713, 55, 46, White);

            // ---- left CAMERA column: the vehicle's OWN cameras (populated in game; empty with no craft) ----
            L("CAMERA", 150, 300, 30, Accent);
            string[] cams = s.CamLabels ?? new string[0];
            if (cams.Length == 0)
            {
                L("no cameras on vehicle", 150, 392, 28, Dim);
            }
            else
            {
                int n = VisibleCams(s);
                for (int i = 0; i < n; i++)
                {
                    // ⭐ S134b: the rect comes from `RowRect`, which `HitTest` also calls — one
                    // geometry for the drawing and the touch, the `ChromeBar.LinkRect` rule. Before
                    // this the page had NO hit test at all and the rows were a selection the crew
                    // could see and not change (QC `VV-02`).
                    float rx, ry, rw, rh;
                    RowRect(i, out rx, out ry, out rw, out rh);
                    bool sel = s.CameraView == i;
                    dl.Rect(PX(rx), PY(ry), rw * sx, rh * sy, sel ? Panel : Bg);
                    dl.Box(PX(rx), PY(ry), rw * sx, rh * sy, St(sel ? 4 : 2), sel ? Accent : Hair);
                    L(cams[i], rx + 50, ry + 40, 32, sel ? White : Dim);
                }
            }

            // ---- the live feed (2:1), centred ----
            float vx = 850, vy = 420, vw = 2020, vh = 1010;   // 2:1 letterbox
            dl.Rect(PX(vx), PY(vy), vw * sx, vh * sy, DragonPalette.Inset2);
            dl.Image(ImageId.DockingCamLive, PX(vx), PY(vy), vw * sx, vh * sy, White);
            dl.Box(PX(vx), PY(vy), vw * sx, vh * sy, St(3), Hair);
            if (s.CameraHeldByDocking)
                C("FORWARD VIEW IN USE BY DOCKING", vx + vw * 0.5f, vy + vh * 0.5f, 34, DragonPalette.Caution);
            else if (cams.Length == 0)
                C("NO SIGNAL", vx + vw * 0.5f, vy + vh * 0.5f - 18f, 40, Dim);

            // ---- S107 / QC VV-01: a resolution is a property of a CAMERA ----
            // This row printed unconditionally, so the page could state three things at once: "no cameras
            // on vehicle" in the left column, "NO SIGNAL" in the viewport, and "RESOLUTION 640 x 360"
            // underneath it. The `?? "—"` fallback shows a dash was always intended for this case; it
            // never fired, because the FIELD is populated even when the camera LIST is empty -
            // `DockingCamRenderer.Resolution` is the RenderTexture's own size, set once at construction.
            // ⚠ HELD-BY-DOCKING still shows the number, deliberately: docking having the forward view
            // means a camera EXISTS and its feed really is that size - this page just cannot see it.
            bool feedExists = cams.Length > 0 || s.CameraHeldByDocking;
            L("RESOLUTION", vx, vy + vh + 24, 28, Dim);
            R(feedExists ? (s.CameraResText ?? "—") : "—",
              vx + vw, vy + vh + 24, 32, feedExists ? White : Dim);

            // ---- Audio / Cabin / Video tab strip (Video active) ----
            // ⭐ S134a: shared geometry, see SettingsTabStrip (QC F-04).
            SettingsTabStrip.Draw(dl, w, h, 2);

            BottomBar.Draw(dl, w, h, s);   // S103: undistorted, in the design frame; S147: CURRENT STATE live
        }

        // =========================================================================================
        //  S134b / QC VV-02 — THE ROWS BECOME TOUCHABLE
        // =========================================================================================
        // ⛔ THE DEFECT: this page read `s.CamLabels` off a real vessel scan and highlighted
        // `s.CameraView` — a genuinely live list — and had **no `HitTest` in the file**. `FigmaUI`'s
        // settings branch resolved only the three tabs. So the crew could see which camera was selected
        // and had no way to select another.
        //
        // ⭐ AND THE WRITER WAS NEVER ACTUALLY MISSING. `VesselData.SetCameraView` is live, validates its
        // argument against the real hull-cam count, and is what `DockingCamRenderer` reads. What was
        // stranded was the PATH to it: the only caller sat in the legacy `SettingsPage.HitTest` →
        // `PageAct.SetCamera` dispatch, unreachable under `FigmaMode`. So this is a routing fix, not new
        // machinery — which is also why it is (A) and not §14.4(a)-blocked: choosing which camera a
        // screen shows commands nothing.
        //
        // ⚠ THE ROW GEOMETRY IS `RowRect`, USED BY BOTH. See the draw loop above.

        /// <summary>Row height and pitch, in the design frame — the reference's own numbers.</summary>
        public const float RowX = 150f, RowW = 560f, RowH = 118f, RowTop = 370f, RowPitch = 150f;

        /// <summary>The most rows the page will draw, whatever the vehicle carries.</summary>
        public const int MaxRows = 8;

        /// <summary>Where camera row <paramref name="i"/> is drawn, in design coordinates.</summary>
        public static void RowRect(int i, out float x, out float y, out float w, out float h)
        {
            x = RowX; w = RowW; h = RowH;
            y = RowTop + i * RowPitch;
        }

        /// <summary>How many rows this state actually draws. ⛔ The SAME clamp the draw applies, asked
        /// as a function so the hit test cannot offer a row that was never painted — "a button bound to
        /// nothing wearing the shape of one that works", which is the note `SettingsPage.HitTest`
        /// already carries about its own camera list.</summary>
        public static int VisibleCams(PageState s)
        {
            int n = (s.CamLabels == null) ? 0 : s.CamLabels.Length;
            return (n < MaxRows) ? n : MaxRows;
        }

        /// <summary>
        /// Which camera row a touch fell on, or −1.
        ///
        /// ⚠ THE PAGE IS STRETCHED, not letterboxed — `PX(x) = x * w / RefW` — so the inverse is the
        /// same stretch. [[S134a]] is the line about getting that wrong on a sibling page; this one is
        /// drawn in code and there is only one projection to get right.
        /// </summary>
        public static int HitTest(float px, float py, int w, int h, PageState s)
        {
            if (w <= 0 || h <= 0) return -1;
            float dx = px * RefW / w, dy = py * RefH / h;
            int n = VisibleCams(s);
            for (int i = 0; i < n; i++)
            {
                float rx, ry, rw, rh;
                RowRect(i, out rx, out ry, out rw, out rh);
                if (dx >= rx && dx < rx + rw && dy >= ry && dy < ry + rh) return i;
            }
            return -1;
        }
    }
}
