/*
 * DragonScreen - MechPage
 *
 * PURE. VEHICLE's second subview: MECH PANEL. Laid out from `Mech.vue` (1005 lines), extracted into
 * docs/UI_AUDIT.md.
 *
 *      #title        top 0.5%, centred          "MECH PANEL"
 *      .left-menu    left 13.5%, width 27.5%    the subsystem column
 *      .ring-svg     left 63%, max(27.5vw,55vh) two big concentric rings, centre-right
 *      .left-panel   left 35.5%, width 10%
 *      .right-panel  left 90.5%, width 10%
 *      .bottom-panel left 63%, width 30%, height 25%, four .inner-slot at 12.5/37.25/62.5/87.5%
 *
 * ---- WHAT IS REAL HERE, AND IT IS MORE THAN IT LOOKS ----
 * The pyro row is the prize: `TRUNKS SET / TRUNKS FIRED / DROGUES FIRED / MAINS FIRED /
 * MAINS RELEASED`. Every one of those is a state KSP genuinely has - a decoupler that has or has not
 * fired, a ModuleParachute's deployment state - and it is exactly what a crew watches during an
 * entry. This is the most flyable panel in the reference and we did not have it at all.
 *
 * ACCELERATION splits into POSITIVE / NEGATIVE / ANGULAR / CENTRIPETAL, all derivable from the
 * vessel. PRESSURE - LQ OXYGEN, LQ NITROGEN, CO2 CANISTERS - is consumables, simulated by
 * CabinEnvironment like the rest of the cabin.
 *
 * ---- WHAT IS DELIBERATELY MISSING, AND IT IS NOT A JUDGEMENT CALL ----
 * `SEAT 1..4 TACH  RPM/s`. Checked in the source rather than assumed (Mech.vue:474-541, 607-621):
 *
 *      randomNumber: [79510.01, 71267.02, 73125.03, 75069.04, 71128.05]
 *      animate() { setTimeout(this.animate, 1000); this.randomizer = Math.random() * 1000 - 500 }
 *      {{ Math.round((this.randomNumber[0] + this.randomizer) * 100) / 100 }}
 *
 * A hard-coded number with +/-500 of Math.random() added, once a second. It is jitter to make the
 * panel look alive, there is no input behind it, and nothing on a Dragon seat rotates to have an RPM.
 * That is precisely the one category CLAUDE.md forbids by name - "FAKE - never: a constant, or a
 * random number" - so the slots show crew occupancy instead, which is real.
 */
namespace DragonScreen
{
    public static class MechPage
    {
        public static void Build(DisplayList dl, int w, int h, PageState s)
        {
            float bx, by, bw, bh;
            Card.Body(w, h, out bx, out by, out bw, out bh);

            dl.Text("MECH PANEL", bx + bw * 0.5f, by + 4f, Typography.Body, TextAlign.Centre,
                    DragonPalette.Text1);

            float top = by + 34f;
            float colW = bw * 0.275f;                     // .left-menu width 27.5%
            float col1 = bx + bw * 0.135f - colW * 0.5f;  // .left-menu left 13.5%, centred

            // ---- LEFT COLUMN: the subsystem groups, in the source's order ----
            float y = top;
            y = Group(dl, col1, y, colW, "ACCELERATION");
            y = Row(dl, col1, y, colW, "POSITIVE", s.Valid ? s.AccelPosText : "-", "g");
            y = Row(dl, col1, y, colW, "NEGATIVE", s.Valid ? s.AccelNegText : "-", "g");
            y = Row(dl, col1, y, colW, "ANGULAR", s.Valid ? s.AccelAngText : "-", "deg/s");
            y = Row(dl, col1, y, colW, "CENTRIPETAL", s.Valid ? s.AccelCentText : "-", "g");

            // ---- CONSUMABLES, WHICH IS WHAT THESE ROWS ALWAYS SAID THEY WERE ----
            // `LQ OXYGEN` was fed PPO2 and `LQ NITROGEN` was fed TOTAL CABIN PRESSURE - cabin gas
            // readings wearing tank names, in psia, duplicating three rows already on CABIN. Wrong
            // twice over and the panel carried no information of its own.
            //
            // They are now the SIMULATED SUPPLY TANKS: quantity remaining, drawn down by the crew
            // actually aboard over the time that actually passed, and faster with a fire or a leak.
            // See VehicleSystems. Canisters count UP because saturation is what kills a scrubber.
            y += 14f;
            y = Group(dl, col1, y, colW, "PRESSURE");
            y = Row(dl, col1, y, colW, "LQ OXYGEN",
                    s.Valid ? Pct(s.Systems.Oxygen) : "-", "%");
            y = Row(dl, col1, y, colW, "LQ NITROGEN",
                    s.Valid ? Pct(s.Systems.Nitrogen) : "-", "%");
            y = Row(dl, col1, y, colW, "CO2 CANISTERS",
                    s.Valid ? Pct(1.0 - s.Systems.CanisterUsed) : "-", "%");

            // ---- POWER STRINGS ----
            // The console has ten buttons for these. A panel that lets the crew isolate a string and
            // then never shows them which strings are up would be the dead-control failure inverted.
            y += 14f;
            y = Group(dl, col1, y, colW, "POWER STRINGS");
            y = Row(dl, col1, y, colW, "BUS 1",
                    s.Valid ? StringWord(s.Systems, 1) : "-", null);
            y = Row(dl, col1, y, colW, "BUS 2",
                    s.Valid ? StringWord(s.Systems, 2) : "-", null);
            y = Row(dl, col1, y, colW, "SUPPRESSANT",
                    s.Valid ? Pct(s.Systems.Suppressant) : "-", "%");

            // ---- CENTRE-RIGHT: the two rings ----
            // `.ring-svg` and `.inner-ring-svg` at left 63%. In the source they carry the vehicle
            // state animation; ours shows the thing a mech panel is watched FOR during entry - how
            // far through the pyro sequence the vehicle is.
            float rx = bx + bw * 0.63f;
            float ry = by + bh * 0.42f;
            float rr = bh * 0.30f;
            dl.ArcBand(rx, ry, rr - 2f, rr, 0.0, 360.0, DragonPalette.Hairline);
            dl.ArcBand(rx, ry, rr * 0.86f - 2f, rr * 0.86f, 0.0, 360.0, DragonPalette.Inset1);

            int fired = 0, total = 5;
            if (s.TrunkSet) fired++;
            if (s.TrunkFired) fired++;
            if (s.DroguesFired) fired++;
            if (s.MainsFired) fired++;
            if (s.MainsReleased) fired++;
            double seq = (double)fired / total;
            Gauge.Ring(dl, rx, ry, rr * 0.72f, rr * 0.10f, s.Valid ? seq : 0.0,
                       DragonPalette.GaugeTrack, DragonPalette.Accent);
            dl.Text("SEQUENCE", rx, ry - 12f, Typography.Caption, TextAlign.Centre,
                    DragonPalette.Text6);
            dl.Text(s.Valid ? (fired + " / " + total) : "-", rx, ry + 8f, Typography.Value,
                    TextAlign.Centre, DragonPalette.Text0);

            // ---- THE PYRO ROW, which is the reason this panel matters ----
            float py = ry + rr + 18f;
            float pw = bw * 0.30f;
            float px = bx + bw * 0.63f - pw * 0.5f;
            py = State(dl, px, py, pw, "TRUNKS SET", s.Valid, s.TrunkSet, "SET");
            py = State(dl, px, py, pw, "TRUNKS FIRED", s.Valid, s.TrunkFired, "FIRED");
            py = State(dl, px, py, pw, "DROGUES FIRED", s.Valid, s.DroguesFired, "FIRED");
            py = State(dl, px, py, pw, "MAINS FIRED", s.Valid, s.MainsFired, "FIRED");
            py = State(dl, px, py, pw, "MAINS RELEASED", s.Valid, s.MainsReleased, "RELEASED");

            // ---- RIGHT PANEL: crew, in the four slots the bottom panel uses ----
            // The source puts SEAT n TACH here. There is no tachometer on a Dragon seat and nothing
            // in KSP to read for one, so the slots carry occupancy - real, and the same shape.
            float qx = bx + bw * 0.905f - bw * 0.05f;
            float qw = bw * 0.10f;
            dl.Text("SEATS", qx + qw * 0.5f, top, Typography.Caption, TextAlign.Centre,
                    DragonPalette.Text5);
            for (int i = 0; i < 4; i++)
            {
                bool crewed = (s.SeatNames != null && i < s.SeatNames.Length
                               && !string.IsNullOrEmpty(s.SeatNames[i]));
                float sy = top + 26f + i * 34f;
                dl.Rect(qx, sy + 4f, 8f, 8f,
                        crewed ? DragonPalette.Go : DragonPalette.Text7);
                dl.Text(crewed ? s.SeatNames[i] : "EMPTY", qx + 16f, sy, Typography.Dense,
                        TextAlign.Left, crewed ? DragonPalette.Text2 : DragonPalette.Text7);
            }

            // ---- ALL SYSTEMS CHECK ----
            // The source's status line. Ours reads from the same Alarms the chrome bar does, so the
            // panel and the bar cannot disagree.
            Severity worst = s.Valid ? Alarms.VehicleSeverity(s) : Severity.Nominal;
            dl.Text("ALL SYSTEMS CHECK", bx, by + bh - 22f, Typography.Caption, TextAlign.Left,
                    DragonPalette.Text6);
            dl.Text(s.Valid ? Alarms.Word(worst) : "Awaiting", bx + 190f, by + bh - 22f,
                    Typography.Caption, TextAlign.Left,
                    s.Valid ? Alarms.Colour(worst) : DragonPalette.Text7);
        }

        /// <summary>Percent with no decimals. Consumables are read at a glance, not to 0.1%.</summary>
        private static string Pct(double v)
        {
            if (v < 0.0) v = 0.0; else if (v > 1.0) v = 1.0;
            return (v * 100.0).ToString("F0");
        }

        /// <summary>"A B C" with each letter replaced by its state, so one glance reads the bus.</summary>
        private static string StringWord(SystemsState s, int bus)
        {
            string outp = "";
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) outp += " ";
                StringState st = Systems.Get(s, bus, i);
                outp += (char)('A' + i);
                outp += (st == StringState.Online) ? "+" : (st == StringState.Isolated) ? "o" : "x";
            }
            return outp;
        }

        private static float Group(DisplayList dl, float x, float y, float w, string name)
        {
            dl.Text(name, x, y, Typography.Caption, TextAlign.Left, DragonPalette.Text5);
            dl.Rect(x, y + 20f, w, 1f, DragonPalette.Hairline);
            return y + 28f;
        }

        private static float Row(DisplayList dl, float x, float y, float w,
                                 string caption, string value, string unit)
        {
            Readouts.Row(dl, x, y, w, caption, value, unit, Typography.Dense);
            return y + 24f;
        }

        /// <summary>
        /// A pyro state: a name, a lamp, and the word. Three readings, not two - an unfired pyro and
        /// a vehicle we cannot read are different, and during an entry that difference matters.
        /// </summary>
        private static float State(DisplayList dl, float x, float y, float w,
                                   string name, bool valid, bool on, string word)
        {
            // THE WORD IS THE ROW'S OWN. A first version printed "SET" for every false row, so
            // "TRUNKS SET / SET" read as a state when it meant "not yet" - and after the trunk had
            // gone it still said SET. A row that is not true shows a dash; only a true row gets a
            // word, and the word is that row's word.
            Rgba c = !valid ? DragonPalette.Text7
                   : on ? DragonPalette.Go : DragonPalette.Text7;
            dl.Rect(x, y + 3f, 9f, 9f, c);
            dl.Text(name, x + 18f, y, Typography.Dense, TextAlign.Left,
                    on ? DragonPalette.Text2 : DragonPalette.Text6);
            dl.Text(!valid ? "-" : on ? word : "-", x + w, y, Typography.Dense,
                    TextAlign.Right, c);
            return y + 22f;
        }
    }
}
