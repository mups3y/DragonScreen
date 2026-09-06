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
        // ⚠ S135: the VALUE column is gone from this pair - it is resolved per channel now, by
        // ChannelText. The line above is kept as written because it names what the arrays WERE.
        static readonly string[] ChLabel = { "GROUND", "AUX", "MAIN", "INTERCOM", "ALERTS" };
        static readonly float[]  ChCx    = { 717, 1257, 1713, 2211, 2709 };

        // ---- S135 / QC A-02: THE FIVE VALUES WERE LITERALS AND NOW COME FROM SOMEWHERE ----
        // ⛔ SUPERSEDED IN PLACE, kept verbatim because it is the evidence (C1.16 / G12). The row above
        // used to read:
        //
        //     static readonly string[] ChValue = { "12dB", "0dB", "100", "+9dB", "50" };
        //
        // Five hardcoded strings in three different unit systems, none of which was measured from
        // anything. `+9dB` on an INTERCOM the page cannot hear, and `12dB` on a GROUND channel that is
        // not a channel at all. Each is now resolved per channel: see ChannelText.
        //
        // ⚠ `GROUND` IS NOT A CHANNEL, and this is where that shows. `Audio.vue`'s slot list is
        // "dB, AUX, MAIN, Vox, INTERCOM, ALERTS" (`SettingsPage.cs:17`); `GROUND` is a crew ROLE from
        // the seat rows ("PASSENGER / PILOT / GROUND", `:311`). The Figma rebuild put a role where the
        // reference has a unit legend. ⛔ NOT RENAMED HERE - this file's own header says it was rebuilt
        // from the Figma frames using their exact layer geometry, so the label is reference-sourced and
        // changing it is a §1.4 call, not a tidy-up. It resolves to a dash, which is the honest reading
        // of a channel with nothing behind it, and the discrepancy is written up on REGISTER.md S135.

        /// <summary>The eight ± buttons, in the order MinusX/PlusX place them. Only the channels the
        /// owner's mapping reaches are controls; the rest are painted INERT (S75) and have no hit
        /// rect at all (S29's precedent for a plate whose function no source names).</summary>
        static readonly string[] BtnCh = { "GROUND", "AUX", "INTERCOM", "ALERTS" };
        static readonly float[]  DivX     = { 966, 1464, 1962, 2460 };            // dividers between channels
        // -/+ button centres (design x) per side; MAIN has the VOX box instead
        static readonly float[]  MinusX  = { 717, 1219, 2181, 2678 };            // GROUND,AUX,INTERCOM,ALERTS
        static readonly float[]  PlusX   = { 869, 1371, 2333, 2830 };
        static readonly float[]  SignalX = { 565, 1067 };                        // GROUND,AUX have a signal icon

        public static void Build(DisplayList dl, int w, int h, int sel)
        { Build(dl, w, h, sel, new PageState()); }

        public static void Build(DisplayList dl, int w, int h, int sel, PageState s)
        {
            if (sel < 0 || sel > 4) sel = 2;
            float sx = w / RefW, sy = h / RefH;
            float PX(float x) => x * sx;
            float PY(float y) => y * sy;
            float SZ(float v) => v * sy;
            int St(float rs) => Strokes.Px(rs, sy);   // ONE rule, in Strokes.cs - rounds UP (R-02 family)
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
            CTxt(sel == CabinScope ? "CABIN AUDIO" : "SEAT " + (sel + 1) + " AUDIO", 1721, 1264, 34, White);
            // ---- ⛔ S134c / QC A-01: AND THE PAGE SAYS WHAT THE SELECTION DOES NOT CHANGE --------
            // QC's own warning about making the seats selectable: *"today `ChValue` is one literal array
            // shared by all five scopes, so selecting SEAT 2 would change a heading and nothing else —
            // five headings over one set of numbers, which is worse than one honest heading."*
            // ⭐ THAT IS STILL TRUE, and it is not a gap this line can close. [[S135]] settled what the
            // channels read — the owner's Q6 ruling bound four of them to the GAME's own audio layers,
            // and he was told and accepted that those are **global settings**. KSP has no per-seat
            // audio, so there is nothing per-seat for a seat to select.
            // ⚠ So the selection is real (the reference's own control, and (A)), and the page MARKS what
            // it does not do rather than implying it. §14.4(f): included, filled, and marked — never a
            // heading that quietly claims a scope the numbers do not have.
            // ⚠ AT THE STATIC FLOOR, and the size is DERIVED rather than picked. A first version used
            // `Typography.Dense * 2f` — 24 design px, which on this page is 24 * (h/RefH) = 16 PANEL px,
            // two thirds of the 24 px floor [[S153]]'s R-01 ruling permits even for a static label.
            // ⛔ AND THE RATCHET COULD NOT SEE IT: the R-01 census renders this page at its DEFAULT
            // scope, and the caveat only draws on the four seats. It failed only when a mutation moved
            // the line onto CABIN. Logged as [[S165]] — the census is blind to non-default page states.
            if (sel != CabinScope)
                CTxt("LEVELS BELOW ARE THE VEHICLE'S — KSP HAS NO PER-SEAT AUDIO",
                     1721, 1310, Typography.DenseDesignFor(w, h / RefH), DragonPalette.Text6);

            for (int i = 0; i < 5; i++)
            {
                CTxt(ChLabel[i], ChCx[i], 1382, 30, Dim);
                string val = ChannelText(s, ChLabel[i]);
                CTxt(val, ChCx[i], 1430, 118, val == Dashes.None ? Dim : White);
            }
            for (int i = 0; i < DivX.Length; i++) VLine(DivX[i], 1419, 1619, DragonPalette.Hairline);

            // ---- +/- buttons (GROUND, AUX, INTERCOM, ALERTS) + signal icons (GROUND, AUX) ----
            // ⛔ S135 / QC A-02: THESE WERE TEN CONTROLS THAT COULD NOT BE TOUCHED. Every one drew a
            // filled square with a St(3) WHITE border and a white glyph - this build's button idiom
            // everywhere - over a page with no hit test at all. `SettingsPage.cs:20-23` calls that
            // exact shape by name: "drawing eight buttons where seven do nothing is the dead-control
            // failure this project refuses".
            //
            // ⭐ FOUR OF THEM ARE REAL NOW and four are not, and the difference is PAINTED. The owner's
            // mapping reaches AUX and ALERTS; GROUND is not a channel and INTERCOM is a reading, so
            // their pairs get S75's "nothing live behind this" tint and NO hit rect (S29's precedent).
            // ⚠ The lit state and the live state are ONE question asked once - AudioChannels.Actionable
            // - which HitTest asks again, so a dimmed pair cannot act and a live one cannot look
            // unavailable. That is S32's rule, applied to a second page.
            for (int i = 0; i < MinusX.Length; i++)
            {
                Rgba bc = AudioChannels.Actionable(s.Audio, BtnCh[i]) ? White : Dim;
                Btn(MinusX[i], 1598, 140, Bg, bc);
                dl.Line(MinusX[i] * sx - SZ(28), PY(1668), MinusX[i] * sx + SZ(28), PY(1668), St(5), bc);   // minus
                Btn(PlusX[i], 1598, 140, Bg, bc);
                dl.Line(PlusX[i] * sx - SZ(28), PY(1668), PlusX[i] * sx + SZ(28), PY(1668), St(5), bc);      // plus -
                dl.Line(PlusX[i] * sx, PY(1668) - SZ(28), PlusX[i] * sx, PY(1668) + SZ(28), St(5), bc);      // plus |
            }
            // ⛔ THE TWO SIGNAL PLATES STAY INERT AND ARE NOW PAINTED THAT WAY. No source says what a
            // signal button on an audio channel DOES - the same §1.4 wall S29 hit on the Suit Leak
            // Check's two read-only plates, and answered the same way: drawn, dim, no hit rect.
            for (int i = 0; i < SignalX.Length; i++)
            {
                Btn(SignalX[i], 1598, 140, Bg, Dim);
                dl.ArcBand(SignalX[i] * sx, PY(1690), SZ(6), SZ(20), -55, 55, Dim);   // signal fan
                dl.ArcBand(SignalX[i] * sx, PY(1690), 0, SZ(5), 0, 360, Dim);
            }
            // MAIN's VOX indicator
            // ⛔ S135: "17" was a literal too, and VOX maps to VOICE_VOLUME.
            CTxt("VOX", 1713, 1614, 30, Dim);
            {
                string vox = ChannelText(s, "VOX");
                CTxt(vox, 1713, 1656, 44, vox == Dashes.None ? Dim : White);
            }

            // ---- bottom tabs (Audio / Cabin / Video) with the Audio tab underlined ----
            // ⭐ S134a: the strip's geometry lives in `SettingsTabStrip` now, and the hit test asks the
            // same numbers — so a tab cannot be drawn in one place and tested in another (QC F-04).
            SettingsTabStrip.Draw(dl, w, h, 0);

            // ---- bottom status bar (reused) ----
            BottomBar.Draw(dl, w, h, s);   // S103: undistorted, in the design frame; S147: CURRENT STATE live
        }

        // =========================================================================================
        //  INTERACTIVITY (S135) — the four buttons the owner's mapping makes real
        // =========================================================================================
        /// <summary>Every ± button the page DRAWS, named. ⛔ All eight are here even though only four
        /// can act: the enum is the page's GEOMETRY, and `Available` is the POLICY. Keeping them apart
        /// means a later mapping change (GROUND is a §1.4 question, see the header) needs no new enum
        /// value and no new control-id — and the CVR's `audio.` namespace stays complete whatever the
        /// mapping is. Order must not be reshuffled: `CrewPressTest` pins these names.</summary>
        public enum AudioAct : byte
        {
            None = 0,
            GroundMinus, GroundPlus,
            AuxMinus, AuxPlus,
            IntercomMinus, IntercomPlus,
            AlertsMinus, AlertsPlus
        }

        /// <summary>The channel label an act belongs to, or null for None.</summary>
        public static string ChannelOf(AudioAct a)
        {
            switch (a)
            {
                case AudioAct.GroundMinus:   case AudioAct.GroundPlus:   return "GROUND";
                case AudioAct.AuxMinus:      case AudioAct.AuxPlus:      return "AUX";
                case AudioAct.IntercomMinus: case AudioAct.IntercomPlus: return "INTERCOM";
                case AudioAct.AlertsMinus:   case AudioAct.AlertsPlus:   return "ALERTS";
            }
            return null;
        }

        /// <summary>-1 for a minus, +1 for a plus, 0 for None.</summary>
        public static int DirOf(AudioAct a)
        {
            switch (a)
            {
                case AudioAct.GroundMinus: case AudioAct.AuxMinus:
                case AudioAct.IntercomMinus: case AudioAct.AlertsMinus: return -1;
                case AudioAct.GroundPlus: case AudioAct.AuxPlus:
                case AudioAct.IntercomPlus: case AudioAct.AlertsPlus: return 1;
            }
            return 0;
        }

        /// <summary>Does this button DO anything? ⭐ THE ONE QUESTION, ASKED ONCE. `Build` tints from
        /// it, `HitTest` refuses on it and the glue asks it again before writing a setting, so a dimmed
        /// button cannot act and a live one cannot look unavailable — S32's rule, on a second page.
        /// `GROUND` is not a channel and `INTERCOM` is a reading, so neither is ever available however
        /// readable the settings are.</summary>
        public static bool Available(AudioAct a, AudioLevels levels)
        {
            return a != AudioAct.None && AudioChannels.Actionable(levels, ChannelOf(a));
        }

        /// <summary>Which ± button a touch hit, or None. ⛔ Geometry FIRST, then the gate: a button
        /// that is painted dim returns None even when the touch was dead on it, so the page cannot
        /// promise a press it will not honour.</summary>
        public static AudioAct HitTest(float px, float py, int w, int h, PageState st)
        {
            if (w <= 0 || h <= 0) return AudioAct.None;
            float sx = w / RefW, sy = h / RefH;
            // Btn draws a d x d square, sized on sy, centred on the design centre-x mapped by sx.
            float top = 1598f * sy, bot = top + 140f * sy, half = 70f * sy;
            if (py < top || py >= bot) return AudioAct.None;
            for (int i = 0; i < MinusX.Length; i++)
            {
                float mc = MinusX[i] * sx, pc = PlusX[i] * sx;
                AudioAct hit = AudioAct.None;
                if (px >= mc - half && px < mc + half) hit = (AudioAct)(1 + i * 2);
                else if (px >= pc - half && px < pc + half) hit = (AudioAct)(2 + i * 2);
                if (hit == AudioAct.None) continue;
                return Available(hit, st.Audio) ? hit : AudioAct.None;
            }
            return AudioAct.None;
        }

        /// <summary>
        /// What a channel prints. Three cases, and the third is the one the ruling names:
        ///   · a MAPPED layer prints the game's own gain as a percent (AudioChannels);
        ///   · INTERCOM prints the CREW READING it already is on the legacy page
        ///     (`SettingsPage.cs:333`, `s.CrewText`) - the owner's ruling keeps it a reading;
        ///   · anything else dashes, because there is nothing behind it.
        /// ⛔ No branch here can print a plausible number for an unreadable source. "0%" on a channel
        /// whose settings could not be read would say the game is MUTED, which is a different claim.
        /// </summary>
        // =========================================================================================
        //  S134c / QC A-01 — THE FIVE SEATS BECOME SELECTABLE
        // =========================================================================================
        // ⛔ THE DEFECT: `Build`'s `sel` picks which of five scopes is shown, and every caller passed
        // the literal `2`. `CABIN AUDIO` was the only heading this page could ever show; the four
        // `SEAT n AUDIO` layouts were written, correct and unreachable — S49 H9's dead-enum class,
        // inside a page that ships. And the page drew five selectable-LOOKING seats, none selectable.
        //
        // ⭐ ONE RECT FUNCTION FOR THE DRAW AND THE TOUCH, which is `PageAction`'s rule and the exact
        // thing QC `H-04` shows going wrong when it is not followed. `SeatBox` was already the draw's
        // geometry; this only gives it a name and an inverse.

        /// <summary>The scope index that means CABIN rather than a seat. ⚠ 2, not 0 — the cabin sits in
        /// the MIDDLE of the five illustrations, and the reference numbers them left to right.</summary>
        public const int CabinScope = 2;

        /// <summary>How many scopes the page draws.</summary>
        public const int Scopes = 5;

        /// <summary>Seat <paramref name="i"/>'s illustration box, in design coordinates — the same
        /// `SeatBox` row the draw places the artwork from.</summary>
        public static void SeatRect(int i, out float x, out float y, out float w, out float h)
        {
            if (i < 0 || i >= Scopes) { x = y = w = h = 0f; return; }
            x = SeatBox[i, 0]; y = SeatBox[i, 1]; w = SeatBox[i, 2]; h = SeatBox[i, 3];
        }

        /// <summary>
        /// Which scope a touch fell on, or −1.
        ///
        /// ⚠ THE PAGE IS STRETCHED — `PX(x) = x * w / RefW` — so the inverse is the same stretch.
        /// [[S134a]] is the line about a sibling page getting that wrong; this one draws in code and
        /// has only one projection.
        /// </summary>
        public static int SeatHitTest(float px, float py, int w, int h)
        {
            if (w <= 0 || h <= 0) return -1;
            float dx = px * RefW / w, dy = py * RefH / h;
            for (int i = 0; i < Scopes; i++)
            {
                float rx, ry, rw, rh;
                SeatRect(i, out rx, out ry, out rw, out rh);
                if (dx >= rx && dx < rx + rw && dy >= ry && dy < ry + rh) return i;
            }
            return -1;
        }

        public static string ChannelText(PageState s, string label)
        {
            if (label == "INTERCOM")
                return s.Valid && !string.IsNullOrEmpty(s.CrewText) ? s.CrewText : Dashes.None;
            return AudioChannels.Text(s.Audio, AudioChannels.LayerFor(label));
        }
    }
}
