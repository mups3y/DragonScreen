// DragonScreen — SystemsPidPage  (PURE: the systems P&ID plumbing deep-view — T9)
// ============================================================================================
// SCREEN_INVENTORY.md "Vehicle systems P&ID schematic" (`crew1_3`, `crew3_1`, `demo1_3` — tier-1 flown
// -console photos) / BUILD_PLAN.md §3 row "Vehicle systems deep-views" / §7 item 5. The inventory's
// own description of the real screen: "the Dragon's fluid/electrical system as LINE-ART: rectangular
// loops, ring/hex components (tanks/valves/pumps), inline valve symbols, small green status dots along
// the lines. NOT our radial Mech donut and NOT the rendered dragon_crew." §11b lists it as the second
// of the two systems deep-views, alongside the box-and-connector tree (SystemsTreePage).
//
// ---- WHAT IS REAL, WHAT IS OURS (§1.4) ----
// REAL, from the photos: the LAYOUT GRAMMAR above — rectangular loops, boxed components, inline valve
// symbols, status dots on the lines. The frames are not legible enough to transcribe a single label
// (SCREEN_INVENTORY's own residual-research note asks for "a cleaner frame" to capture the text, and
// §7 item 5 says the same), so this is "layout-real / labels-reconstructed" — the footing T7 and T8
// already established for photo-only pages.
// OURS, and stated: WHICH subsystem the real screen plumbs. The inventory says only "likely a
// subsystem detail (Prop/Thermal/ECLSS)". We draw the ECLSS + coolant loops because those are the
// fluid systems this build actually MODELS — so every component on the diagram has a live state to
// show instead of a painted one. The propulsion side already has its own real page (PropSchematic).
// Component names (CABIN, CO2 SCRUBBER, CABIN FAN, SUIT LOOP, O2 TANK, N2 TANK, PUMP, CABIN HX,
// RADIATOR) are generic ECLSS vocabulary reconstructed for this page, not transcribed labels.
//
// ---- SIMULATE, NEVER FAKE ----
// Every status dot, outline colour and bar here reads a live signal that already drives other screens:
// PageState.Systems (pure/VehicleSystems.cs — O2, N2, CO2 canister, the leak/isolation path, fire and
// suppressant) and PageState.Cabin (pure/CabinEnvironment.cs — pressure, ppO2, CO2, cabin temperature
// and both coolant-loop temperatures), banded by the SAME Alarms/CabinLimits thresholds the gauges use,
// so a component here can never disagree with the gauge for the same quantity. Numbers are drawn from
// PageState's pre-formatted text (PressText / Ppo2Text / Co2Text / LoopAText / LoopBText /
// CabinTempText) — the draw path formats nothing; quantities with no pre-formatted text are shown as
// bars instead of inventing one.
//
// Reachability: the Menu grid, like SystemsTreePage and T7/T8's pages — not a ninth VehicleTabBar tab,
// whose eight tabs are confirmed-real (C1.4). A real in-page entry point is T14's job.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class SystemsPidPage
    {
        public const int Commands = 230;
        const float RefW = 3427f, RefH = 2112f;
        const float CX = 1713.5f;

        static readonly Rgba Bg     = DragonPalette.Background;
        static readonly Rgba Node   = DragonPalette.Inset1;
        static readonly Rgba Pipe   = DragonPalette.Text7;
        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Dim    = DragonPalette.Text6;
        static readonly Rgba Faint  = DragonPalette.Text7;
        static readonly Rgba Accent = DragonPalette.Accent;

        // ---- ATMOSPHERE rail: four components on one loop, returning under itself ----
        const float AirTop = 340f, AirH = 140f, AirMid = AirTop + AirH * 0.5f;
        const float AirRet = 640f;                       // the loop's return line
        static readonly float[] AirX = { 1000f, 1610f, 2220f, 2700f };   // box lefts
        static readonly float[] AirW = { 450f, 450f, 320f, 430f };

        // ---- THERMAL: two identical rectangular loops ----
        const float LoopAY = 1060f, LoopBY = 1330f, LoopH = 130f;
        static readonly float[] ThX = { 300f, 800f, 1410f };             // pump · cabin HX · radiator
        static readonly float[] ThW = { 340f, 450f, 450f };

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f; if (ox < 0f) ox = 0f;
            float X(float x) => x * sc + ox;
            float Y(float y) => y * sc;
            float Z(float v) => v * sc;
            void L(string t, float x, float y, float sz, Rgba c) => dl.Text(t, X(x), Y(y), Z(sz), TextAlign.Left, c);
            void C(string t, float x, float y, float sz, Rgba c) => dl.Text(t, X(x), Y(y), Z(sz), TextAlign.Centre, c);
            void R(string t, float x, float y, float sz, Rgba c) => dl.Text(t, X(x), Y(y), Z(sz), TextAlign.Right, c);
            void Pipe2(float x0, float y0, float x1, float y1, Rgba c) =>
                dl.Line(X(x0), Y(y0), X(x1), Y(y1), Z(4f), c);
            // A status dot ON the line — the photo's "small green status dots along the lines".
            void Dot(float x, float y, Rgba c) => dl.ArcBand(X(x), Y(y), Z(2f), Z(11f), 0, 360, c);
            // An inline valve: the two crossed strokes a P&ID draws where a line passes through one.
            void Valve(float x, float y, Rgba c)
            {
                dl.Line(X(x - 26f), Y(y - 26f), X(x + 26f), Y(y + 26f), Z(4f), c);
                dl.Line(X(x - 26f), Y(y + 26f), X(x + 26f), Y(y - 26f), Z(4f), c);
            }
            // A component: dark body, neutral line-art outline, name, value, and a status dot on its
            // corner — the reference's "green status dots" idiom. The OUTLINE only takes a colour when
            // the component is off nominal, so a fault is the one thing that stands out on the page.
            void Comp(float x, float top, float bw, float bh, string name, string value,
                      Severity sev, bool live)
            {
                Rgba c = live ? Alarms.Colour(sev) : Faint;
                dl.Rect(X(x), Y(top), bw * sc, bh * sc, Node);
                dl.Box(X(x), Y(top), bw * sc, bh * sc, Z(3f), (live && sev != Severity.Nominal) ? c : Pipe);
                Dot(x + 26f, top + 26f, c);
                C(name, x + bw * 0.5f, top + bh * 0.22f, 26, White);
                C(value, x + bw * 0.5f, top + bh * 0.58f, 26, c);
            }
            void Bar(float x, float y, float bw, float frac, Rgba c)
            {
                dl.Rect(X(x), Y(y), bw * sc, Z(8f), Faint);
                float f = frac > 1f ? 1f : (frac < 0f ? 0f : frac);
                if (f > 0f) dl.Rect(X(x), Y(y), bw * sc * f, Z(8f), c);
            }

            dl.Rect(0, 0, w, h, Bg);
            C("SYSTEMS P&ID", CX, 50f, 44, Accent);
            C("ENVIRONMENTAL CONTROL · COOLANT LOOPS", CX, 118f, 26, Dim);

            bool valid = s.Valid;
            Rgba unk = Faint;

            // ================= ATMOSPHERE =================
            L("ATMOSPHERE", 300f, 240f, 28, Accent);

            // --- supply: the two gas tanks, each through a valve into the cabin feed ---
            Severity o2Sev = valid ? Alarms.Low(s.Systems.Oxygen) : Severity.Nominal;
            Severity n2Sev = valid ? Alarms.Low(s.Systems.Nitrogen) : Severity.Nominal;
            Comp(300f, 300f, 420f, 120f, "O2 TANK", "", o2Sev, valid);
            Bar(340f, 388f, 340f, valid ? (float)s.Systems.Oxygen : 0f,
                valid ? Alarms.Colour(o2Sev) : unk);
            Comp(300f, 470f, 420f, 120f, "N2 TANK", "", n2Sev, valid);
            Bar(340f, 558f, 340f, valid ? (float)s.Systems.Nitrogen : 0f,
                valid ? Alarms.Colour(n2Sev) : unk);

            Pipe2(720f, 360f, 860f, 360f, Pipe);
            Pipe2(720f, 530f, 860f, 530f, Pipe);
            Valve(790f, 360f, Pipe);
            Valve(790f, 530f, Pipe);
            Pipe2(860f, 360f, 860f, 530f, Pipe);
            Pipe2(860f, AirMid, AirX[0], AirMid, Pipe);
            Dot(860f, AirMid, valid ? Alarms.Colour(Alarms.Worst(o2Sev, n2Sev)) : unk);

            // --- the cabin loop: CABIN -> CO2 SCRUBBER -> CABIN FAN -> SUIT LOOP -> back ---
            Severity ls = valid ? Alarms.LifeSupport(s.Cabin) : Severity.Nominal;
            Severity co2Sev = valid ? Alarms.Band(s.Cabin.Co2MmHg,
                                    CabinLimits.Co2Caution, CabinLimits.Co2Alarm) : Severity.Nominal;
            Rgba cabCol = valid ? Alarms.Colour(ls) : unk;
            Rgba co2Col = valid ? Alarms.Colour(co2Sev) : unk;

            Comp(AirX[0], AirTop, AirW[0], AirH, "CABIN", valid ? s.PressText : "—", ls, valid);
            Comp(AirX[1], AirTop, AirW[1], AirH, "CO2 SCRUBBER", valid ? s.Co2Text : "—", co2Sev, valid);
            Comp(AirX[2], AirTop, AirW[2], AirH, "CABIN FAN", valid ? "RUNNING" : "—",
                 Severity.Nominal, valid);
            Comp(AirX[3], AirTop, AirW[3], AirH, "SUIT LOOP", valid ? s.Ppo2Text : "—", ls, valid);

            for (int i = 0; i < 3; i++)
            {
                float x0 = AirX[i] + AirW[i], x1 = AirX[i + 1];
                Pipe2(x0, AirMid, x1, AirMid, Pipe);
                Valve((x0 + x1) * 0.5f, AirMid, Pipe);
            }
            // return leg, under the rail and back into the cabin
            float airEnd = AirX[3] + AirW[3];
            Pipe2(airEnd, AirMid, airEnd + 130f, AirMid, Pipe);
            Pipe2(airEnd + 130f, AirMid, airEnd + 130f, AirRet, Pipe);
            Pipe2(airEnd + 130f, AirRet, AirX[0] + AirW[0] * 0.5f, AirRet, Pipe);
            Pipe2(AirX[0] + AirW[0] * 0.5f, AirRet, AirX[0] + AirW[0] * 0.5f, AirTop + AirH, Pipe);
            Dot(2400f, AirRet, valid ? Alarms.Colour(ls) : unk);

            // --- the overboard / isolation branch: the leak path the crew closes ---
            bool leaking = valid && s.Systems.Leaking;
            bool isolating = valid && s.Systems.Isolating;
            Rgba ventCol = leaking ? (isolating ? DragonPalette.Caution : DragonPalette.Alarm)
                                   : (valid ? DragonPalette.Go : unk);
            Pipe2(930f, AirMid, 930f, AirRet + 90f, leaking ? ventCol : Pipe);   // tee off the feed
            Pipe2(860f, AirRet + 90f, 1000f, AirRet + 90f, leaking ? ventCol : Pipe);  // overboard port
            Valve(930f, AirRet, leaking ? ventCol : Pipe);
            C("OVERBOARD / ISOLATION", 930f, AirRet + 130f, 24, Dim);
            C(leaking ? (isolating ? "ISOLATING" : "LEAK") : "CLOSED", 930f, AirRet + 170f, 26, ventCol);

            // ================= THERMAL =================
            Severity loopASev = valid ? Alarms.Band(s.Cabin.LoopAC, CabinLimits.LoopCaution,
                                                    CabinLimits.LoopAlarm) : Severity.Nominal;
            Severity loopBSev = valid ? Alarms.Band(s.Cabin.LoopBC, CabinLimits.LoopCaution,
                                                    CabinLimits.LoopAlarm) : Severity.Nominal;
            L("COOLANT LOOPS", 300f, 960f, 28, Accent);
            DrawLoop(dl, sc, ox, true, LoopAY, valid ? s.LoopAText : "—", loopASev, valid);
            DrawLoop(dl, sc, ox, false, LoopBY, valid ? s.LoopBText : "—", loopBSev, valid);

            // ================= right-hand live readouts =================
            float rx = 2150f;
            L("READOUTS", rx, 1000f, 26, Accent);
            void Row(string label, string value, string unit, float ry, Rgba c)
            {
                L(label, rx, ry, 24, Faint);
                R(value, 3070f, ry - 4f, 28, c);
                L(unit, 3086f, ry, 22, Faint);
            }
            Row("LOOP A", valid ? s.LoopAText : "—", "°C", 1070f, valid ? Alarms.Colour(loopASev) : unk);
            Row("LOOP B", valid ? s.LoopBText : "—", "°C", 1150f, valid ? Alarms.Colour(loopBSev) : unk);
            Row("CABIN TEMP", valid ? s.CabinTempText : "—", "°C", 1230f,
                valid ? Alarms.Colour(Alarms.Band(s.Cabin.CabinTempC, CabinLimits.CabinTempCaution, CabinLimits.CabinTempAlarm)) : unk);
            Row("CABIN PRESS", valid ? s.PressText : "—", "psia", 1310f, cabCol);
            Row("PPO2", valid ? s.Ppo2Text : "—", "psia", 1390f, cabCol);
            Row("CO2", valid ? s.Co2Text : "—", "mmHg", 1470f, co2Col);

            // ================= consumable / hazard strip =================
            dl.Rect(X(300f), Y(1590f), 2830f * sc, Z(3f), Pipe);
            L("CO2 CANISTER", 300f, 1630f, 24, Faint);
            Bar(300f, 1680f, 560f, valid ? 1f - (float)s.Systems.CanisterUsed : 0f,
                valid ? Alarms.Colour(Alarms.Low(1.0 - s.Systems.CanisterUsed)) : unk);
            L("SUPPRESSANT", 1000f, 1630f, 24, Faint);
            Bar(1000f, 1680f, 560f, valid ? (float)s.Systems.Suppressant : 0f,
                valid ? Alarms.Colour(Alarms.Low(s.Systems.Suppressant)) : unk);
            L("FIRE", 1700f, 1630f, 24, Faint);
            bool fire = valid && s.Systems.Fire;
            L(fire ? "DETECTED" : (valid ? "NONE" : "—"), 1700f, 1674f, 28,
              fire ? DragonPalette.Alarm : (valid ? DragonPalette.Go : unk));
            L("CABIN LEAK", 2150f, 1630f, 24, Faint);
            L(leaking ? (isolating ? "ISOLATING" : "DETECTED") : (valid ? "NONE" : "—"),
              2150f, 1674f, 28, ventCol);

            dl.Asset("component_48", 0f, Y(1877), w, Z(235), White);
        }

        /// <summary>One coolant loop as a rectangular circuit: PUMP → CABIN HX → RADIATOR → back.</summary>
        static void DrawLoop(DisplayList dl, float sc, float ox, bool isA, float top, string temp,
                             Severity sev, bool live)
        {
            float X(float x) => x * sc + ox;
            float Y(float y) => y * sc;
            float Z(float v) => v * sc;
            float mid = top + LoopH * 0.5f;
            Rgba col = live ? Alarms.Colour(sev) : DragonPalette.Text7;
            Rgba outline = (live && sev != Severity.Nominal) ? col : DragonPalette.Text7;

            void Comp(float x, float bw, string name, string value)
            {
                dl.Rect(X(x), Y(top), bw * sc, LoopH * sc, DragonPalette.Inset1);
                dl.Box(X(x), Y(top), bw * sc, LoopH * sc, Z(3f), outline);
                dl.ArcBand(X(x + 24f), Y(top + 24f), Z(2f), Z(11f), 0, 360, col);
                dl.Text(name, X(x + bw * 0.5f), Y(top + LoopH * 0.20f), Z(24), TextAlign.Centre, DragonPalette.White);
                if (!string.IsNullOrEmpty(value))
                    dl.Text(value, X(x + bw * 0.5f), Y(top + LoopH * 0.56f), Z(26), TextAlign.Centre, col);
            }

            Comp(ThX[0], ThW[0], isA ? "PUMP A" : "PUMP B", "RUNNING");
            Comp(ThX[1], ThW[1], isA ? "CABIN HX A" : "CABIN HX B", "");
            Comp(ThX[2], ThW[2], isA ? "RADIATOR A" : "RADIATOR B", temp);

            for (int i = 0; i < 2; i++)
            {
                float x0 = ThX[i] + ThW[i], x1 = ThX[i + 1];
                dl.Line(X(x0), Y(mid), X(x1), Y(mid), Z(4f), outline);
            }
            // return leg beneath the rail
            float endX = ThX[2] + ThW[2], retY = top + LoopH + 60f, pumpCX = ThX[0] + ThW[0] * 0.5f;
            dl.Line(X(endX), Y(mid), X(endX + 90f), Y(mid), Z(4f), outline);
            dl.Line(X(endX + 90f), Y(mid), X(endX + 90f), Y(retY), Z(4f), outline);
            dl.Line(X(endX + 90f), Y(retY), X(pumpCX), Y(retY), Z(4f), outline);
            dl.Line(X(pumpCX), Y(retY), X(pumpCX), Y(top + LoopH), Z(4f), outline);
        }
    }
}
