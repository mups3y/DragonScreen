// DragonScreen — SettingsAudioPage  (PURE: the Figma "A-Settings" audio screen, Cabin + Seat 1–4)
// ============================================================================================
// Rebuilt from the Figma frames (A-Settings-Cabin 1:189 etc.) using the exact layer geometry from the
// Figma MCP. The seat illustrations are the design's own PNG exports (art/cover/settings_seat*.png);
// everything else — title, channel labels + values, dividers, +/- buttons, tabs — is drawn live from
// the metadata positions so it stays crisp and can go live later. The bottom status bar reuses the
// cover's component_48. One layout, `sel` picks which seat is highlighted (2 = Cabin).
//
// FILL-TO-FIT (undistorted): positions spread across the full width (sx); element SIZES use the height
// scale (sy) so nothing stretches — the seats spread apart, the panel widens, circles stay round.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class SettingsAudioPage
    {
        public const int Commands = 220;
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba Bg     = DragonPalette.Background;
        static readonly Rgba Panel  = DragonPalette.Panel;
        static readonly Rgba Accent = DragonPalette.Accent;
        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Dim    = DragonPalette.Text6;
        static readonly Rgba T3     = DragonPalette.Text3;

        // seat instances: key | x | y | w | h  (Cabin is index 2, slightly larger/higher)
        static readonly string[] SeatKey = { "settings_seat1", "settings_seat2", "settings_cabin_seat", "settings_seat3", "settings_seat4" };
        static readonly float[,] SeatBox = { {90,249,580,874},{745,249,580,874},{1387,215,608,944},{2057,249,580,874},{2713,249,580,874} };

        // 5 audio channels: label | value | value-centre-x (design)
        static readonly string[] ChLabel = { "GROUND", "AUX", "MAIN", "INTERCOM", "ALERTS" };
        static readonly string[] ChValue = { "12dB", "0dB", "100", "+9dB", "50" };
        static readonly float[]  ChCx    = { 717, 1257, 1713, 2211, 2709 };
        static readonly float[]  DivX     = { 966, 1464, 1962, 2460 };            // dividers between channels
        // -/+ button centres (design x) per side; MAIN has the VOX box instead
        static readonly float[]  MinusX  = { 717, 1219, 2181, 2678 };            // GROUND,AUX,INTERCOM,ALERTS
        static readonly float[]  PlusX   = { 869, 1371, 2333, 2830 };
        static readonly float[]  SignalX = { 565, 1067 };                        // GROUND,AUX have a signal icon

        public static void Build(DisplayList dl, int w, int h, int sel)
        {
            if (sel < 0 || sel > 4) sel = 2;
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            float SZ(float v) => v * sy;
            int St(float rs) { int p = (int)Math.Round(rs * sy); return p < 1 ? 1 : p; }
            // discrete image, undistorted, centred on its design centre-x
            void Img(string key, float x, float y, float wd, float hd) =>
                dl.Asset(key, (x + wd * 0.5f) * sx - wd * sy * 0.5f, PY(y), wd * sy, hd * sy, White);
            // discrete box (rounded look via fill+border), undistorted, centred on design centre-x
            void Btn(float cx, float y, float d, Rgba fill, Rgba border)
            {
                float left = cx * sx - d * sy * 0.5f;
                dl.Rect(left, PY(y), d * sy, d * sy, fill);
                dl.Box(left, PY(y), d * sy, d * sy, St(3), border);
            }
            void CTxt(string t, float cx, float y, float size, Rgba c) => dl.Text(t, cx * sx, PY(y), SZ(size), TextAlign.Centre, c);
            void VLine(float x, float y0, float y1, Rgba c) => dl.Line(PX(x), PY(y0), PX(x), PY(y1), St(2), c);

            dl.Rect(0, 0, w, h, Bg);

            // ---- selected-seat highlight (behind the selected seat) ----
            {
                float bx = SeatBox[sel, 0] - 40, by = SeatBox[sel, 1] - 34, bw = SeatBox[sel, 2] + 80, bh = SeatBox[sel, 3] + 120;
                float cx = (bx + bw * 0.5f) * sx, left = cx - bw * sy * 0.5f;
                dl.Rect(left, PY(by), bw * sy, bh * sy, Panel);
                dl.Box(left, PY(by), bw * sy, bh * sy, St(3), Accent);
            }
            // ---- the 5 seat illustrations ----
            for (int i = 0; i < 5; i++)
                Img(SeatKey[i], SeatBox[i, 0], SeatBox[i, 1], SeatBox[i, 2], SeatBox[i, 3]);

            // ---- Cabin speaker icons (Group 61/62): two stacked rings in the Cabin panel ----
            for (int k = 0; k < 2; k++)
            {
                float cyd = k == 0 ? 564f : 727f;
                dl.ArcBand(1696f * sx, PY(cyd), SZ(34), SZ(44), 0, 360, T3);
                dl.ArcBand(1696f * sx, PY(cyd), 0, SZ(13), 0, 360, T3);
            }

            // ---- title ----
            CTxt("AUDIO SETTINGS", 1692, 55, 46, White);

            // ---- audio panel ----
            dl.Rect(PX(468), PY(1323), 2489 * sx, 434 * sy, Panel);
            CTxt(sel == 2 ? "CABIN AUDIO" : "SEAT " + (sel + 1) + " AUDIO", 1721, 1264, 34, White);

            for (int i = 0; i < 5; i++)
            {
                CTxt(ChLabel[i], ChCx[i], 1382, 30, Dim);
                CTxt(ChValue[i], ChCx[i], 1430, 118, White);
            }
            for (int i = 0; i < DivX.Length; i++) VLine(DivX[i], 1419, 1619, DragonPalette.Hairline);

            // ---- +/- buttons (GROUND, AUX, INTERCOM, ALERTS) + signal icons (GROUND, AUX) ----
            for (int i = 0; i < MinusX.Length; i++)
            {
                Btn(MinusX[i], 1598, 140, Bg, White);
                dl.Line(MinusX[i] * sx - SZ(28), PY(1668), MinusX[i] * sx + SZ(28), PY(1668), St(5), White);   // minus
                Btn(PlusX[i], 1598, 140, Bg, White);
                dl.Line(PlusX[i] * sx - SZ(28), PY(1668), PlusX[i] * sx + SZ(28), PY(1668), St(5), White);      // plus -
                dl.Line(PlusX[i] * sx, PY(1668) - SZ(28), PlusX[i] * sx, PY(1668) + SZ(28), St(5), White);      // plus |
            }
            for (int i = 0; i < SignalX.Length; i++)
            {
                Btn(SignalX[i], 1598, 140, Bg, White);
                dl.ArcBand(SignalX[i] * sx, PY(1690), SZ(6), SZ(20), -55, 55, White);   // signal fan
                dl.ArcBand(SignalX[i] * sx, PY(1690), 0, SZ(5), 0, 360, White);
            }
            // MAIN's VOX indicator
            CTxt("VOX", 1713, 1614, 30, Dim);
            CTxt("17", 1713, 1656, 44, White);

            // ---- bottom tabs (Audio / Cabin / Video) with the Audio tab underlined ----
            CTxt("Audio", 1584, 1921, 28, White);
            CTxt("Cabin", 1714, 1921, 28, Dim);
            CTxt("Video", 1843, 1921, 28, Dim);
            dl.Rect(PX(1524), PY(1974), 120 * sx, 8 * sy, Accent);

            // ---- bottom status bar (reused) ----
            dl.Asset("component_48", PX(0), PY(1877), 3427 * sx, 235 * sy, White);
        }
    }
}
