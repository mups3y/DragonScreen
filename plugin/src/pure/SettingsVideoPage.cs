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
            float SZ(float v) => v * sy;
            int St(float rs) { int p = (int)Math.Round(rs * sy); return p < 1 ? 1 : p; }
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
                for (int i = 0; i < cams.Length && i < 8; i++)
                {
                    float by = 370 + i * 150;
                    bool sel = s.CameraView == i;
                    dl.Rect(PX(150), PY(by), 560 * sx, 118 * sy, sel ? Panel : Bg);
                    dl.Box(PX(150), PY(by), 560 * sx, 118 * sy, St(sel ? 4 : 2), sel ? Accent : Hair);
                    L(cams[i], 200, by + 40, 32, sel ? White : Dim);
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
            C("Audio", 1584, 1921, 28, Dim);
            C("Cabin", 1714, 1921, 28, Dim);
            C("Video", 1843, 1921, 28, White);
            dl.Rect(PX(1783), PY(1974), 120 * sx, 8 * sy, Accent);

            BottomBar.Draw(dl, w, h);   // S103: undistorted, in the design frame
        }
    }
}
