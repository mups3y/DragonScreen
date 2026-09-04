// DragonScreen — SystemsTreePage  (PURE: the systems / electrical TREE deep-view — T9)
// ============================================================================================
// SCREEN_INVENTORY.md #27 / BUILD_PLAN.md §3 row "Vehicle systems deep-views" / §11b: a REAL Crew
// Dragon screen photographed on the JSC sim rig (`jsc2024e064449`, LEFT screen — tier-1). §11b
// characterises it as "a HIERARCHICAL box-and-connector diagram (labelled boxes joined by connector
// lines) — a power-distribution / systems tree", one of the two subsystem deep-views distinct from
// the P&ID plumbing view (SystemsPidPage) and from the thruster schematic (PropSchematic).
//
// ---- WHAT IS REAL, WHAT IS OURS (§1.4) ----
// REAL, from the photo: the LAYOUT GRAMMAR — a hierarchy of labelled boxes joined by connector lines.
// §11b is explicit that the exact on-screen text is NOT transcribable at any resolution we have, so
// this page is "layout-real / labels-reconstructed", the same footing as DeorbitBurnPrepPage (T7) and
// EntryPage (T8).
// REAL, and NOT invented for this page: every box label below is already-confirmed Crew Dragon
// vocabulary this codebase ships. POWER 1 / POWER 2 and STRING 1A–1C / 2A–2C are the REAL console's
// own button legends (pure/PanelMap.cs, transcribed — never edited without a real-source confirmation,
// C1.4), and §4 confirms POWER 1/2 as the main buses and the strings as the triple-redundant
// flight-computer strings (18 units / 54 voting processors — the caption at the foot of the tree).
// SOLAR ARRAY and BATTERIES are VehicleSubsystemPage's own Power checklist entries, reused verbatim
// so no second wording can disagree with the one already on the glass.
//
// S23 (owner decision (b), via the overseer, 2026-09-02): the real screen's own label is "BATTERIES
// ×4" — the real Crew Dragon's fixed battery count — and that transcription is not in dispute. It is
// DROPPED here (and on the Power subsystem page, VehicleSubsystemPage.cs) because the box's own state
// line is this VESSEL's live count, and a static "×4" over it reads as "N of 4 present" on any craft
// that isn't 4 batteries — a count claim the live value can contradict. See REGISTER.md S23/S25.
//
// ---- SIMULATE, NEVER FAKE ----
// The tree is not a picture of a wiring diagram: every box and every connector is coloured by the LIVE
// simulated power system (PageState.Systems — pure/VehicleSystems.cs, the same model the MECH page and
// the console's STRING/POWER/RESET buttons drive). A bus that the crew has not powered reads BUS OFF
// and its branch goes dark; a string reads ON / ISOL / TRIP straight from Systems.StateWord; the state
// of charge is PageState.Power01, the same signal that raises the vehicle's own POWER caution.
//
// T13a closed the two exceptions this header used to list. The array's word is now the REAL
// ModuleDeployableSolarPanel state on the vessel (DEPLOYED / STOWED / "n / m" while a panel is moving or
// broken / NONE when the craft carries no panel at all), and the battery node's "4 / 4" is now the real
// count of parts holding charge over the parts that can hold it — so a drained pack reads differently
// from a full one. Both arrive pre-formatted from VesselData.VehicleSources; NONE is a fact about the
// vessel, not a missing feed, so it prints as itself while a dead feed still prints "—".
// (The same "Deployed" word on the Power SUBSYSTEM page is still representative — that page is T13b.)
//
// Reachability: the Menu grid, like T7's and T8's pages. It is deliberately NOT a ninth VehicleTabBar
// tab — that strip's eight tabs are confirmed-real from the clean designer mockup, and adding one
// would be editing a real-sourced label set (C1.4). A real in-page entry point is T14's job.
// ============================================================================================
using System;

namespace DragonScreen
{
    public static class SystemsTreePage
    {
        public const int Commands = 195;   // +1: S56's touch caption
        const float RefW = 3427f, RefH = 2112f;

        static readonly Rgba Bg     = DragonPalette.Background;
        static readonly Rgba Node   = DragonPalette.Inset1;
        static readonly Rgba Wire   = DragonPalette.Hairline;
        static readonly Rgba White  = DragonPalette.White;
        static readonly Rgba Dim    = DragonPalette.Text6;
        static readonly Rgba Faint  = DragonPalette.Text7;
        static readonly Rgba Accent = DragonPalette.Accent;

        // Pre-built so the draw path never formats a string (DisplayList's rule).
        static readonly string[] Online3 =
            { "0 / 3 ONLINE", "1 / 3 ONLINE", "2 / 3 ONLINE", "3 / 3 ONLINE" };
        static readonly string[] StringName1 = { "STRING 1A", "STRING 1B", "STRING 1C" };
        static readonly string[] StringName2 = { "STRING 2A", "STRING 2B", "STRING 2C" };

        // ---- geometry (design space) ----
        const float SrcY = 230f, SrcH = 140f, SrcW = 520f;
        const float MainY = 480f, MainH = 160f, MainW = 660f, MainCX = 1713.5f;
        const float BusY = 760f, BusH = 160f, BusW = 560f;
        const float StrY = 1060f, StrH = 150f, StrW = 195f;
        const float Bus1CX = 1093f, Bus2CX = 2334f;
        const float StrPitch = 240f;
        // The horizontal "bus" runs each hierarchy level joins on.
        const float SrcBusY = 420f, MainBusY = 700f, StrBusY = 990f, FootBusY = 1300f;
        const float FootY = 1370f, FootH = 150f;

        static float StringCX(int bus, int i)
        {
            float centre = bus == 1 ? Bus1CX : Bus2CX;
            return centre + (i - 1) * StrPitch;
        }

        // ============================================================================================
        // TOUCH (S56 / audit H32) — the eight nodes the crew can already SEE state on, made switchable.
        // ============================================================================================
        // The defect this closes: the tree rendered BUS OFF, ISOL and TRIP off a model whose toggles
        // (`Systems.ToggleBus` / `ToggleString`) existed, worked, and were reachable ONLY from the
        // physical IVA plate. A crew member could read the fault on the glass and do nothing about it.
        //
        // ---- ONE WALK, SHARED BY DRAWING AND HITTING ----
        // `BusRect` / `StringRect` are the ONLY place either box's rectangle is computed. Build calls
        // them; HitTest calls them; a geometry edit therefore cannot move the drawing away from the
        // finger, which is the failure mode a PNG can never show (TouchWiringTest's own premise).
        //
        // ---- THE SAME DISPATCHER, NOT A SECOND ONE (T14's rule) ----
        // Each node resolves to the console plate's OWN `PanelCommand` for that bus or string, so the
        // press goes through `FlightCommands.Run` and is read back by `PanelPolicy` exactly as the
        // plate's button is. Pressing POWER 1 on the glass and POWER 1 on the plate cannot come to
        // different answers, because neither surface owns the answer. **Nothing here flies the vehicle**
        // — `SystemsState` is local display state (§14.4(a) is not in play).
        //
        // ---- WHAT IS DELIBERATELY *NOT* TOUCHABLE ----
        // RESET 1/2. The plate carries it (`PanelCommand.Reset1/2` → `Systems.ResetBus`) and it is the
        // only way back from TRIP, but this page DRAWS no reset control, and drawing one would be
        // inventing a control on a page whose own header calls it "layout-real / labels-reconstructed"
        // (§1.4 / C1.4). So reset stays a plate action until a real source says otherwise, and the
        // register carries the question rather than this file answering it.
        // The SOLAR ARRAY / BATTERIES / MAIN POWER / FLIGHT COMPUTER STRINGS boxes are readouts of
        // things no crew switch commands, so they are correctly inert — and, per S75, they are drawn
        // exactly as they always were: nothing here paints a control that is not one.

        /// <summary>Panel-pixel rect of a POWER bus node. One calculation for the draw and the hit.</summary>
        public static void BusRect(int bus, int w, int h, out float x, out float y,
                                   out float rw, out float rh)
        {
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f; if (ox < 0f) ox = 0f;
            float cx = bus == 1 ? Bus1CX : Bus2CX;
            x = (cx - BusW * 0.5f) * sc + ox; y = BusY * sc; rw = BusW * sc; rh = BusH * sc;
        }

        /// <summary>Panel-pixel rect of one STRING node (bus 1|2, i 0..2). Draw and hit share it.</summary>
        public static void StringRect(int bus, int i, int w, int h, out float x, out float y,
                                      out float rw, out float rh)
        {
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f; if (ox < 0f) ox = 0f;
            float cx = StringCX(bus, i);
            x = (cx - StrW * 0.5f) * sc + ox; y = StrY * sc; rw = StrW * sc; rh = StrH * sc;
        }

        /// <summary>The bus node's command — the plate's own POWER 1 / POWER 2 button.</summary>
        public static PanelCommand BusCommand(int bus)
        { return bus == 1 ? PanelCommand.Power1 : PanelCommand.Power2; }

        /// <summary>The string node's command — the plate's own STRING nX button.</summary>
        public static PanelCommand StringCommand(int bus, int i)
        {
            if (bus == 1)
                return i == 0 ? PanelCommand.String1A : i == 1 ? PanelCommand.String1B
                                                               : PanelCommand.String1C;
            return i == 0 ? PanelCommand.String2A : i == 1 ? PanelCommand.String2B
                                                           : PanelCommand.String2C;
        }

        /// <summary>Which node the touch landed on, as the console command it IS —
        /// <c>PanelCommand.None</c> for everything else on the page.</summary>
        public static PanelCommand HitTest(float px, float py, int w, int h)
        {
            if (w <= 0 || h <= 0) return PanelCommand.None;
            float x, y, rw, rh;
            for (int bus = 1; bus <= 2; bus++)
            {
                BusRect(bus, w, h, out x, out y, out rw, out rh);
                if (Control.Hit(px, py, x, y, rw, rh)) return BusCommand(bus);
                for (int i = 0; i < 3; i++)
                {
                    StringRect(bus, i, w, h, out x, out y, out rw, out rh);
                    if (Control.Hit(px, py, x, y, rw, rh)) return StringCommand(bus, i);
                }
            }
            return PanelCommand.None;
        }

        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            if (dl == null || w <= 0 || h <= 0) return;
            float sc = h / RefH, ox = (w - RefW * sc) * 0.5f; if (ox < 0f) ox = 0f;
            float X(float x) => x * sc + ox;
            float Y(float y) => y * sc;
            float Z(float v) => v * sc;
            void C(string t, float cx, float y, float sz, Rgba col) =>
                dl.Text(t, X(cx), Y(y), Z(sz), TextAlign.Centre, col);
            void LN(float x0, float y0, float x1, float y1, Rgba col) =>
                dl.Line(X(x0), Y(y0), X(x1), Y(y1), Z(4f), col);

            // A tree node: an outlined box whose OUTLINE carries the state colour, its label, and the
            // state word beneath it. Line-art with a dark fill, as the source photo's boxes read.
            void NodeBox(float cx, float top, float bw, float bh, string label, string state, Rgba col)
            {
                dl.Rect(X(cx - bw * 0.5f), Y(top), bw * sc, bh * sc, Node);
                dl.Box(X(cx - bw * 0.5f), Y(top), bw * sc, bh * sc, Z(3f), col);
                C(label, cx, top + bh * 0.26f, 28, White);
                C(state, cx, top + bh * 0.60f, 24, col);
            }

            // The same box, addressed in PANEL PIXELS — fed by BusRect / StringRect, which HitTest also
            // calls. The eight touchable nodes go through this one so the box drawn IS the box hit.
            void NodeBoxPx(float bx, float by, float bw, float bh, string label, string state, Rgba col)
            {
                dl.Rect(bx, by, bw, bh, Node);
                dl.Box(bx, by, bw, bh, Z(3f), col);
                dl.Text(label, bx + bw * 0.5f, by + bh * 0.26f, Z(28), TextAlign.Centre, White);
                dl.Text(state, bx + bw * 0.5f, by + bh * 0.60f, Z(24), TextAlign.Centre, col);
            }

            dl.Rect(0, 0, w, h, Bg);
            C("SYSTEMS TREE", MainCX, 60f, 44, Accent);
            C("ELECTRICAL POWER DISTRIBUTION", MainCX, 130f, 26, Dim);

            bool valid = s.Valid;
            Severity soc = valid ? Alarms.Low(s.Power01) : Severity.Nominal;
            Rgba socCol = valid ? Alarms.Colour(soc) : Faint;

            // ---- sources (LIVE — VesselData.VehicleSources) ----
            // A source the vehicle does not carry reads NONE and goes dark: that is a real fact about
            // this craft, and it must not look the same as a healthy feed.
            string arrayWord = valid ? s.SolarArrayText : null;
            string cellWord  = valid ? s.BatteryText : null;
            bool arrayUp = arrayWord == "DEPLOYED";
            bool cellsUp = !string.IsNullOrEmpty(cellWord) && cellWord != "NONE";
            NodeBox(1293f, SrcY, SrcW, SrcH, "SOLAR ARRAY",
                    string.IsNullOrEmpty(arrayWord) ? "—" : arrayWord,
                    arrayUp ? DragonPalette.Go : (arrayWord == null ? Faint : DragonPalette.Caution));
            // S23 (b): the "×4" is dropped — see the file header. The state line beneath the label is
            // THIS vessel's live count, which is a different question from the real vehicle's fixed one.
            NodeBox(2134f, SrcY, SrcW, SrcH, "BATTERIES",
                    string.IsNullOrEmpty(cellWord) ? "—" : cellWord,
                    cellsUp ? DragonPalette.Go : Faint);

            // sources -> MAIN POWER (a header bus, the way a distribution diagram joins two feeds)
            LN(1293f, SrcY + SrcH, 1293f, SrcBusY, Wire);
            LN(2134f, SrcY + SrcH, 2134f, SrcBusY, Wire);
            LN(1293f, SrcBusY, 2134f, SrcBusY, Wire);
            LN(MainCX, SrcBusY, MainCX, MainY, Wire);

            // ---- MAIN POWER: the live state of charge ----
            NodeBox(MainCX, MainY, MainW, MainH, "MAIN POWER",
                    valid ? Alarms.Word(soc) : "NO DATA", socCol);
            {
                float bx = MainCX - MainW * 0.5f + 40f, bw = MainW - 80f;
                dl.Rect(X(bx), Y(MainY + MainH - 26f), bw * sc, Z(8f), Faint);
                float f = valid ? (float)s.Power01 : 0f;
                if (f > 1f) f = 1f; else if (f < 0f) f = 0f;
                if (f > 0f) dl.Rect(X(bx), Y(MainY + MainH - 26f), bw * sc * f, Z(8f), socCol);
            }

            // ---- MAIN POWER -> the two buses ----
            LN(MainCX, MainY + MainH, MainCX, MainBusY, Wire);
            LN(Bus1CX, MainBusY, Bus2CX, MainBusY, Wire);

            for (int bus = 1; bus <= 2; bus++)
            {
                float bcx = bus == 1 ? Bus1CX : Bus2CX;
                bool on = valid && (bus == 1 ? s.Systems.Bus1On : s.Systems.Bus2On);
                int online = valid ? Systems.OnlineCount(s.Systems, bus) : 0;
                Rgba busCol = !on ? Faint
                            : online == 3 ? DragonPalette.Go
                            : online == 0 ? DragonPalette.Alarm : DragonPalette.Caution;

                LN(bcx, MainBusY, bcx, BusY, on ? busCol : Wire);
                float nx, ny, nw, nh;
                BusRect(bus, w, h, out nx, out ny, out nw, out nh);
                NodeBoxPx(nx, ny, nw, nh, bus == 1 ? "POWER 1" : "POWER 2",
                          on ? Online3[online] : "BUS OFF", busCol);

                // bus -> its three strings
                LN(bcx, BusY + BusH, bcx, StrBusY, on ? busCol : Wire);
                LN(StringCX(bus, 0), StrBusY, StringCX(bus, 2), StrBusY, on ? busCol : Wire);

                for (int i = 0; i < 3; i++)
                {
                    StringState st = valid ? Systems.Get(s.Systems, bus, i) : StringState.Isolated;
                    bool live = on && st == StringState.Online;
                    Rgba sc2 = !on ? Faint
                             : st == StringState.Online ? DragonPalette.Go
                             : st == StringState.Isolated ? DragonPalette.Caution : DragonPalette.Alarm;
                    float scx = StringCX(bus, i);
                    LN(scx, StrBusY, scx, StrY, live ? sc2 : Wire);
                    StringRect(bus, i, w, h, out nx, out ny, out nw, out nh);
                    NodeBoxPx(nx, ny, nw, nh,
                              bus == 1 ? StringName1[i] : StringName2[i],
                              on ? Systems.StateWord(st) : "—", sc2);
                    LN(scx, StrY + StrH, scx, FootBusY, live ? sc2 : Wire);
                }
            }

            // ---- the loads the strings ARE (§4's confirmed fact, as the tree's foot) ----
            LN(StringCX(1, 0), FootBusY, StringCX(2, 2), FootBusY, Wire);
            LN(MainCX, FootBusY, MainCX, FootY, Wire);
            float footW = StringCX(2, 2) - StringCX(1, 0) + StrW;
            dl.Rect(X(StringCX(1, 0) - StrW * 0.5f), Y(FootY), footW * sc, FootH * sc, Node);
            dl.Box(X(StringCX(1, 0) - StrW * 0.5f), Y(FootY), footW * sc, FootH * sc, Z(3f), Wire);
            C("FLIGHT COMPUTER STRINGS", MainCX, FootY + 38f, 28, White);
            C("TRIPLE-REDUNDANT · 18 UNITS · 54 VOTING PROCESSORS", MainCX, FootY + 82f, 24, Dim);

            // ---- legend: what the outline colours mean, so a dark branch is not read as a fault ----
            C("ON", 1200f, 1640f, 24, DragonPalette.Go);
            C("ISOLATED", 1470f, 1640f, 24, DragonPalette.Caution);
            C("TRIPPED", 1780f, 1640f, 24, DragonPalette.Alarm);
            C("UNPOWERED", 2120f, 1640f, 24, Faint);

            // S56: the eight bus/string boxes are now CONTROLS, and a control the crew cannot tell is a
            // control is the same defect S75 closed pointing the other way. One caption, no new glyph —
            // the boxes keep the layout the photo gives them, and the page says what they do.
            C("TOUCH A POWER OR STRING NODE TO SWITCH IT — THE SAME COMMAND AS THE CONSOLE PLATE",
              MainCX, 1720f, 24, Dim);

            dl.Asset("component_48", 0f, Y(1877), w, Z(235), White);
        }
    }
}
