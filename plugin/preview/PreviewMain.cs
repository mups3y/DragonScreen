/*
 * DragonScreen - PreviewMain
 *
 * RENDERS PAGES TO PNG WITH THE GAME CLOSED.
 *
 *     python build.py preview      -> build/preview/screen1.png, screen2.png, screen3.png
 *
 * ---- WHY: A RESTART IS THE SCARCE RESOURCE ----
 * A DLL change costs a full KSP restart and so does a cfg change. Page design is where the iteration
 * count explodes - proportions, palette, whether a gauge reads at a glance - and almost none of it
 * needs the game. This turns "rebuild, restart, load a save, enter IVA, look" into "look".
 *
 * A restart is then spent only on what genuinely needs the capsule: how the material behaves,
 * whether MSAA holds up, legibility at real IVA distance, and input.
 *
 * ---- IT MUST WALK THE SAME LIST, OR IT IS WORSE THAN NOTHING ----
 * This links src/pure ONLY - no KSP, no Unity - exactly like the headless tests. It executes the
 * same DisplayList that ScreenPainter executes, and every decision about geometry is taken in
 * src/pure where both call the same function; notably ArcGeometry.ArcScreen, which owns the y-flip.
 * A preview that quietly disagreed with the screen would cost more than it saved.
 *
 * WHAT IT DELIBERATELY DOES NOT PROMISE: pixel-identical output. GDI+ and the GPU rasterise and
 * anti-alias differently, and the real screen is a texture on angled geometry under Deferred. This
 * is for LAYOUT, PROPORTION, COLOUR and LEGIBILITY. Anything about how it sits on the glass is still
 * a question for the game.
 *
 * Renders at the MEASURED sizes of the three real screens, so what is judged here is the shape that
 * actually exists - including the centre screen being ~1% narrower than the outer two.
 */
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using DragonScreen;

public static class PreviewMain
{
    // Measured from the screen meshes in game, 2026-08-05, and logged by DragonScreenMonitor.
    // Hard-coded here because the game is not running; if the model ever changes, the log is the
    // source of truth and these follow it.
    private struct ScreenSpec { public int Index, W, H; }
    private static readonly ScreenSpec[] Screens = {
        new ScreenSpec { Index = 1, W = 1280, H = 703 },   // left
        new ScreenSpec { Index = 2, W = 1280, H = 710 },   // centre - NOT the same shape
        new ScreenSpec { Index = 3, W = 1280, H = 703 }    // right
    };

    /// <summary>Frozen sweep position. A fixed value so two runs can be compared byte for byte.</summary>
    private const double Phase = 0.62;

    public static int Main(string[] args)
    {
        string outDir = (args.Length > 0)
            ? args[0]
            : Path.Combine(Path.GetDirectoryName(
                  System.Reflection.Assembly.GetExecutingAssembly().Location), "preview");
        Directory.CreateDirectory(outDir);

        DisplayList dl = new DisplayList(Pages.Commands + ChromeBar.Commands + 4);

        // ---- EVERY PAGE, not every screen ----
        // Screens differ only by a few pixels of height; PAGES differ completely, and pages are what
        // is being designed. Rendering all five at the real screen size is what makes the design loop
        // fast: five PNGs, game closed, seconds.
        //
        // Realistic sample values, clearly not live. The preview has no vessel; inventing plausible
        // telemetry here is fine BECAUSE it is a design tool that nobody flies - the same numbers on
        // the glass in game would be a lie, which is why Pages draws dashes when the feed is invalid.
        PageState ps = new PageState();
        ps.Valid = true;
        ps.Phase = "ORBITING";
        ps.Altitude = "123.4 km";
        ps.Velocity = "2280 m/s";
        ps.Apoapsis = "124.0 km";
        ps.Periapsis = "121.9 km";
        ps.Body = "KERBIN";
        ps.Regime = FlightRegime.Space;
        ps.ApogeeShown = true; ps.PerigeeShown = true;
        ps.HasTarget = true; ps.TargetName = "SPACE X STATION";
        ps.RangeText = "202.6 m"; ps.RateText = "-0.25 m/s"; ps.Closing = true;
        ps.OffXText = "22.7 m"; ps.OffYText = "0.1 m"; ps.OffZText = "0.0 m";
        ps.PitchText = "0.1 deg"; ps.YawText = "0.1 deg"; ps.RollText = "15.0 deg";
        ps.PitchRateText = "0.0 deg/s"; ps.YawRateText = "0.1 deg/s"; ps.RollRateText = "0.0 deg/s";
        // T13c: the same three errors in the glyph form the MANUAL docking page prints. VesselData
        // formats the pair off one value; the fixture keeps them consistent for the same reason.
        ps.PitchDegText = "0.1°"; ps.YawDegText = "0.1°"; ps.RollDegText = "15.0°";
        // S26: the raw doubles behind the three lines above - DockingSimPage's target diamond and its
        // "green when corrected" ring tint read these, not the text, so the fixture has to carry both.
        ps.PitchDeg = 0.1; ps.YawDeg = 0.1; ps.RollDeg = 15.0;
        ps.Align01 = 0.06; ps.AlignText = "5.4 deg";
        ps.Mode = ControlMode.Auto; ps.ModeText = "AUTO";
        // ---- THE SIMULATED SYSTEMS ----
        // Fresh() then partly used, so MECH shows consumables that have MOVED and a bus with one
        // string off. A default-constructed SystemsState renders 0% oxygen and three healthy buses,
        // which is both wrong and checks nothing - the same trap the sequence fixture fell into.
        ps.Systems = SystemsState.Fresh();
        ps.Systems.Oxygen = 0.86; ps.Systems.Nitrogen = 0.93; ps.Systems.CanisterUsed = 0.22;
        ps.Systems.B2 = StringState.Isolated;

        // ---- FLIGHT'S SEQUENCE ----
        // Mid-ascent, second stage burning, so the preview shows the interesting middle of the list:
        // countdown closed out, staging done, SECO and beyond still pending, one step running. A
        // fixture left at defaults renders every row identical and checks nothing.
        ps.Steps.Valid = true;
        ps.Steps.Phase = MissionPhase.Ascent;
        ps.Steps.Crew = 4;
        ps.Steps.OnPad = false; ps.Steps.Clamped = false; ps.Steps.Powered = true;
        ps.Steps.Propellant01 = 0.62; ps.Steps.EscapeArmed = true;
        ps.Steps.RadarAltitude = 96000.0; ps.Steps.VerticalSpeed = 480.0;
        ps.Steps.MaxQPassed = true;
        ps.Steps.BoosterAttached = false; ps.Steps.S2Attached = true; ps.Steps.S2Lit = true;

        ps.Propellant01 = 0.62;  ps.PropellantText = "62";
        // Mid-ascent: the booster is lit, so the dial is on LF/OX, not the Dracos.
        ps.PropellantCaption = "PROPELLANT LF/OX";
        ps.Power01      = 0.18;  ps.PowerText      = "18";     // caution band, so it is visible
        ps.GForce01     = 0.04;  ps.GForceText     = "0.2";

        // ---- RAW ORBITAL STATE ----
        // Kerbin's real numbers, so the bars fill to the fractions they will actually fill to in
        // game rather than to whatever looked good here.
        ps.AltitudeM = 123400.0; ps.ApogeeM = 124000.0; ps.PerigeeM = 121900.0;
        ps.VelocityMps = 2280.0; ps.InclinationDeg = 0.13;
        ps.BodyRadiusM = 600000.0; ps.AtmosphereDepthM = 70000.0;
        ps.CircularSpeedMps = 2426.0;      // sqrt(mu/R) for Kerbin
        ps.Ascending = true;
        ps.InclinationText = "0.13 deg";
        ps.InclinationDegText = "0.13°";      // T13c: the Manual Chute strip's own rendering
        ps.PeriodText = "31.4 min";
        ps.TimeToApText = "00:12:07";
        ps.TimeToPeText = "00:27:49";
        ps.SplashdownShown = false; ps.SplashdownText = "-";
        ps.RangeM = 202.6;
        // T13c: where the target sits on the rendezvous plot. A little ABOVE and 0.7 rad AHEAD of
        // us, which is what a real approach from a lower phasing orbit looks like - and it puts the
        // chord across open panel rather than along the ellipse where it could not be judged.
        ps.HasTargetOrbit = true; ps.TargetRadiusM = 728000.0; ps.TargetPhaseRad = 0.7;

        // ---- NAV ----
        // A synthetic track: a 51.6 degree inclination pass, which is a shape that makes a wrong
        // projection obvious at a glance in a way a near-equatorial Kerbin orbit would not.
        ps.HasFix = true;
        ps.Latitude = 28.6; ps.Longitude = -80.6;      // the real pad, near enough
        ps.LatText = "28.60 N"; ps.LonText = "80.60 W";
        int track = 90;
        ps.TrackLat = new double[track];
        ps.TrackLon = new double[track];
        for (int i = 0; i < track; i++)
        {
            double frac = i / (double)(track - 1);
            double arg = frac * 360.0;
            ps.TrackLat[i] = 51.6 * Math.Sin(arg * Math.PI / 180.0);
            // Marching west as the body turns under it - the correction VesselData applies for real.
            ps.TrackLon[i] = MapProjection.Wrap180(-80.6 + arg - frac * 22.0);
        }
        ps.TrackCount = track;
        ps.HasTargetGround = true;
        ps.TargetLat = 51.6; ps.TargetLon = 14.0;
        ps.TargetLatText = "51.60 N"; ps.TargetLonText = "14.00 E";

        // ---- SETTINGS ----
        ps.LightsOn = true;
        ps.Brightness = SettingsPage.MaxBright;
        ps.ScreenPages = new int[] { -1, 1, 0, 2 };    // LEFT VEHICLE, CENTRE FLIGHT, RIGHT NAV
        ps.CrewText = "3 / 4";
        // Seat 2 EMPTY on purpose: a full capsule and a crew count look the same, and the whole
        // point of drawing seats is that they do not.
        ps.SeatNames = new string[] { "Jebediah", null, "Valentina", "Bill" };
        ps.SeatCount = 4;
        // Mid-entry: trunk gone, drogues out, mains not yet - the moment the MECH panel is for.
        ps.TrunkSet = false; ps.TrunkFired = true;
        ps.DroguesFired = true; ps.MainsFired = false; ps.MainsReleased = false;
        ps.AccelPosText = "1.42"; ps.AccelNegText = "0.00";
        ps.AccelAngText = "2.10"; ps.AccelCentText = "0.881";
        // The dial fractions for those same three readings, against VesselData's stated full scales
        // (5 g axial, 2 g centripetal) — derived here so the fixture's rings and numbers agree.
        ps.AccelPos01 = 1.42 / 5.0; ps.AccelNeg01 = 0.0; ps.AccelCent01 = 0.881 / 2.0;
        ps.LightCount = 1;                 // what TE_CD2_POD.cfg actually has
        ps.CameraView = 0; ps.CameraResText = "640 x 360"; ps.CameraHeldByDocking = false;

        // Life support, run through the SAME model the game uses - four crew, powered, cool hull.
        // Not hand-written numbers: if the model changes, the preview changes with it.
        CabinInputs pci = new CabinInputs();
        pci.Crew = 4; pci.CrewCapacity = 4; pci.HullTempC = 21.0;
        pci.MissionTime = 754.0; pci.Power01 = 0.18; pci.PowerFlow = -0.9; pci.Powered = true;
        ps.Cabin = Cabin.Compute(pci);
        ps.Ppo2Text      = ps.Cabin.Ppo2Psia.ToString("F2");
        ps.Co2Text       = ps.Cabin.Co2MmHg.ToString("F2");
        ps.PressText     = ps.Cabin.PressPsia.ToString("F2");
        ps.CabinTempText = ps.Cabin.CabinTempC.ToString("F1");
        ps.LoopAText     = ps.Cabin.LoopAC.ToString("F1");
        ps.LoopBText     = ps.Cabin.LoopBC.ToString("F1");
        // No " W" suffix: these are DIALS now, and the dial puts the unit on its own line under the
        // number. Appending it here produced "-59 W" with a "W" beneath it.
        ps.NetPwr1Text   = ps.Cabin.NetPwr1W.ToString("F0");
        ps.NetPwr2Text   = ps.Cabin.NetPwr2W.ToString("F0");

        // ---- THE VEHICLE PAGE'S OWN SOURCES (T13a) ----
        // Derived here the SAME way VesselData.VehicleSources derives them from a real vessel, so the
        // fixture cannot drift out of agreement with itself: the power-unit rows are ps.PowerText with
        // its unit (one charge pool, reported on both rows), and the rest is a plausible Dragon —
        // capsule propellant in kg, a deployed array, and two battery parts both holding charge.
        ps.PowerUnit1Text = ps.PowerText + " %";
        ps.PowerUnit2Text = ps.PowerUnit1Text;
        ps.DeorbitFuelText = "791.1 kg";
        ps.DeorbitOxText   = "1308.0 kg";
        ps.SolarArrayText  = "DEPLOYED";
        ps.BatteryText     = "2 / 2";

        // ---- THE SIX SUBSYSTEM SUB-TABS' OWN SOURCES (T13b) ----
        // Derived here the SAME way VesselData derives them from a real vessel, so the fixture cannot
        // drift out of agreement with itself: the two gas-store rows are ps.Systems' own fractions (set
        // above), the water row is a TAC-sized tank, the propellant fractions are the same Dragon tanks
        // whose kilograms the CONSUMABLES rows print, and net power is the two NET PWR dials added up.
        // Where the tab has no source at all the field stays NULL and the page draws its dash - which is
        // most of AVIONICS, and is exactly what should be previewed.
        ps.O2TankText = (ps.Systems.Oxygen * 100.0).ToString("F0") + " %";
        ps.N2TankText = (ps.Systems.Nitrogen * 100.0).ToString("F0") + " %";
        ps.Water01 = 0.72; ps.WaterText = "108 L";
        ps.Crew01 = 3.0 / 4.0;                       // matches ps.CrewText "3 / 4" above

        // 791.1 kg of 950 usable fuel, 1308.0 kg of 1500 oxidiser - the CONSUMABLES rows' own masses.
        ps.DragonFuel01 = 791.1 / 950.0;
        ps.DragonOx01   = 1308.0 / 1500.0;
        ps.DragonProp01 = (791.1 + 1308.0) / (950.0 + 1500.0);
        ps.DragonFuelText    = (ps.DragonFuel01 * 100.0).ToString("F0");
        ps.DragonOxText      = (ps.DragonOx01   * 100.0).ToString("F0");
        ps.DragonPropText    = (ps.DragonProp01 * 100.0).ToString("F0");
        ps.PropRemainingText = ps.DragonPropText + " %";
        // RCS is off in the baseline fixture, so the honest duty is zero - the FIRING render below is
        // where a moving one is proved, exactly as it is for the schematic's own segments.
        ps.DracoDutyText = "0 %";

        // S46: the PROPULSION tab's "Thrust Avail", from Kerbal Engineer's fuel-flow simulation. Built by
        // calling the REAL pure path (KerData.Performance) over a synthetic KER stage rather than by
        // assigning the string, so the preview exercises the same selection, docked guard and kN formatting
        // the game will - a fixture that hard-codes "568.0 kN" would render identically no matter what the
        // conversion did. Two SuperDraco pairs' worth of thrust on a ~9.5 t capsule, in SI as KerBridge
        // hands it over (newtons, kilograms). The KER-ABSENT render further down is where the dash is proved.
        {
            KerStage k = new KerStage();
            k.Number = 0; k.Valid = true;
            k.DeltaVMps = 390.0; k.TotalDeltaVMps = 415.0;
            k.ThrustN = 568000.0; k.ActualThrustN = 142000.0;
            k.Twr = 6.09; k.ActualTwr = 1.52; k.MaxTwr = 7.40;
            k.IspS = 235.0;
            k.MassKg = 9525.0; k.TotalMassKg = 12055.0; k.ResourceMassKg = 1388.0;
            k.BurnTimeS = 31.0;
            ps.Ker = KerData.Performance(new[] { k }, false);
        }

        // S24 (owner decision (b)): the one part of AVIONICS with a real source, stock KSP's own
        // CommNet. The baseline fixture is a GOOD link - the ui_vehicleavionics_commoff render further
        // down is where CommNet being off/absent is proved to dash gracefully instead.
        ps.SBandText = "Linked"; ps.SBandLinked = true;
        ps.CommSignal01 = 0.82; ps.UplinkText = "82 %"; ps.DownlinkText = "82 %";

        // A single deployed array making 2.6 kW of its 3.4 kW rating - a real Dragon attitude, not a
        // panel pointed perfectly at the sun.
        ps.Array01 = 2.6 / 3.4;
        ps.ArrayKwText = "2.60"; ps.ArrayOutputText = "2.60 kW";
        double netW = ps.Cabin.NetPwr1W + ps.Cabin.NetPwr2W;
        ps.NetPowerText   = (netW > 0.0 ? "+" : "") + netW.ToString("F0") + " W";
        ps.ChargeRateText = (netW > 0.0 ? "+" : "") + (netW / 1000.0).ToString("F2") + " kW";

        // Mid-entry (the moment the MECH panel above is set for): the shield is hot and well inside the
        // part's own limit, which is what the ring shows.
        ps.HullTempText = "312"; ps.TpsMaxText = "312 °C"; ps.HullTemp01 = 0.41;

        // Body rates: a slow coast attitude hold, off the same axes the docking page's rate lines use.
        ps.BodyPitchDps = 0.12; ps.BodyRollDps = -0.05; ps.BodyYawDps = 0.31;
        ps.BodyPitchText = "0.12"; ps.BodyRollText = "-0.05"; ps.BodyYawText = "0.31";
        ps.BodyRateText = "0.3 deg/s";

        int W = Screens[0].W, H = Screens[0].H;
        // NAV is rendered at a MODERATE ZOOM rather than at the default whole-body view: zoom 0
        // letterboxes and hides the wrap and clamp logic, which is exactly the part most likely to
        // be wrong. Preview the awkward case, not the easy one.
        MapView view = MapProjection.Default();
        view = MapProjection.Zoom(view, 2);
        view = MapProjection.Centre(view, ps.Latitude, ps.Longitude);

        for (int i = 0; i < ChromeBar.PageNames.Length; i++)
        {
            dl.Clear();
            // Screen 2 as the "this display" for the SETTINGS grid, so its highlight is visible.
            Pages.Build(dl, i, W, H, ps, view, 2);

            ChromeState cs = new ChromeState();
            cs.Met = "T+ 00:12:34";
            cs.VehicleState = "NOMINAL";
            cs.LinkName = "COM1/TLM";
            cs.LinkTimer = "00:04:12";
            cs.LinkUp = true;
            cs.SelectedPage = i;
            // One page carries an alert so the routing colour is visible in every render, not just
            // when someone remembers to set it.
            cs.AlertMask = 1 << 1;
            ChromeBar.Build(dl, W, H, cs);

            if (dl.Overflowed)
                Console.WriteLine("  WARNING page " + ChromeBar.PageNames[i]
                                  + " OVERFLOWED at " + dl.Capacity + " commands");

            string path = Path.Combine(outDir,
                "page" + i + "_" + ChromeBar.PageNames[i].ToLowerInvariant() + ".png");
            Render(dl, W, H, path);
            Console.WriteLine("  " + path + "   " + W + "x" + H + "   " + dl.Count + " commands");
        }

        // ---- NAV HAS TWO VIEWS AND ONLY ONE WAS EVER PREVIEWED ----
        // The ORBIT view went into the game untested by the cheap channel, and that is exactly where
        // the invisible planet survived: a near-black disc on a near-black background looks fine in
        // code review and like nothing at all on the glass. Anything reachable by a control needs a
        // render, not just the state it happens to start in.
        {
            MapView orbit = MapProjection.NextMode(MapProjection.Default());
            dl.Clear();
            Pages.Build(dl, 2, W, H, ps, orbit, 2);
            ChromeState cs = new ChromeState();
            cs.Met = "T+ 00:12:34"; cs.VehicleState = "NOMINAL";
            cs.LinkName = "COM1/TLM"; cs.LinkTimer = "00:04:12"; cs.LinkUp = true;
            cs.SelectedPage = 2;
            ChromeBar.Build(dl, W, H, cs);

            if (dl.Overflowed)
                Console.WriteLine("  WARNING page NAV/ORBIT OVERFLOWED at " + dl.Capacity);

            string path = Path.Combine(outDir, "page2_nav_orbit.png");
            Render(dl, W, H, path);
            Console.WriteLine("  " + path + "   " + W + "x" + H + "   " + dl.Count + " commands");
        }

        // ---- NAV / ORBIT, MID-ASCENT: THE TRAJECTORY THAT IS NOT A CLOSED ORBIT (S41) ----
        // The 2026-09-03 flight went up sub-orbital before it circularised, which is what every
        // real ascent does for several minutes, and the ORBIT view had never been previewed in
        // that state - only in the closed 123x122 km orbit above. Periapsis is then hundreds of
        // kilometres BELOW the surface: the conic through the vehicle is a real solution and the
        // part of it under the ground is not a place the vehicle will ever be.
        //
        // RSS Earth numbers, because that is the install the flight was flown on and the one whose
        // radius makes the sub-surface arc enormous; the geometry itself is scale-free. PerigeeShown
        // is FALSE here and that is correct - OrbitReadout rejects a periapsis this far below the
        // surface as the radius artefact it is - so the readouts dash while the plot still has a
        // trajectory to draw. The two are not in conflict: there is no PERIAPSIS worth printing,
        // and there IS an arc worth flying.
        {
            MapView orbit = MapProjection.NextMode(MapProjection.Default());
            PageState sub = ps;                       // PageState is a struct: this is a copy
            sub.Phase = "ASCENT";
            sub.Body = "EARTH";
            sub.Regime = FlightRegime.Space;
            sub.BodyRadiusM = 6371000.0; sub.AtmosphereDepthM = 140000.0;
            sub.ApogeeM = 210000.0; sub.PerigeeM = -5900000.0;
            sub.AltitudeM = 148000.0; sub.Altitude = "148.0 km";
            sub.Apoapsis = "210.0 km"; sub.Periapsis = "-5900.0 km";
            sub.ApogeeShown = true; sub.PerigeeShown = false;
            sub.Ascending = true;
            sub.TimeToApText = "00:03:41"; sub.TimeToPeText = "-"; sub.PeriodText = "-";
            sub.HasTarget = false; sub.HasTargetOrbit = false;

            dl.Clear();
            Pages.Build(dl, 2, W, H, sub, orbit, 2);
            ChromeState cs = new ChromeState();
            cs.Met = "T+ 00:04:52"; cs.VehicleState = "NOMINAL";
            cs.LinkName = "COM1/TLM"; cs.LinkTimer = "00:04:12"; cs.LinkUp = true;
            cs.SelectedPage = 2;
            ChromeBar.Build(dl, W, H, cs);

            if (dl.Overflowed)
                Console.WriteLine("  WARNING page NAV/ORBIT SUBORBITAL OVERFLOWED at " + dl.Capacity);

            string path = Path.Combine(outDir, "page2_nav_orbit_suborbital.png");
            Render(dl, W, H, path);
            Console.WriteLine("  " + path + "   " + W + "x" + H + "   " + dl.Count + " commands");
        }

        // ---- THE SAME OPEN ARC AT KERBIN SCALE, WHERE IT CAN ACTUALLY BE JUDGED (S41) ----
        // The RSS render above is correct and nearly invisible: a 210 km trajectory over a 6371 km
        // radius is 3% of the globe, so the honest arc is a hairline on the limb. That is a
        // SCALE property of this plot, not a property of the fix - the closed RSS orbit the flight
        // ended in draws the same hairline ring - and it is logged as S43 rather than fixed here.
        // This scene is the same geometry on Kerbin's 600 km radius, where the arc is a third of a
        // radius tall and the shape of the fix is plain: it rises out of the limb, arcs over
        // apoapsis, and goes back into the planet, with no PE marker because PE is underground.
        {
            MapView orbit = MapProjection.NextMode(MapProjection.Default());
            PageState sub = ps;                       // PageState is a struct: this is a copy
            sub.Phase = "ASCENT";
            sub.Regime = FlightRegime.Space;
            sub.ApogeeM = 200000.0; sub.PerigeeM = -250000.0;
            sub.AltitudeM = 88000.0;
            sub.Apoapsis = "200.0 km"; sub.Periapsis = "-250.0 km";
            sub.Altitude = "88.0 km";
            sub.ApogeeShown = true; sub.PerigeeShown = false;
            sub.Ascending = true;
            sub.TimeToApText = "00:02:18"; sub.TimeToPeText = "-"; sub.PeriodText = "-";
            sub.HasTarget = false; sub.HasTargetOrbit = false;

            dl.Clear();
            Pages.Build(dl, 2, W, H, sub, orbit, 2);
            ChromeState cs = new ChromeState();
            cs.Met = "T+ 00:03:10"; cs.VehicleState = "NOMINAL";
            cs.LinkName = "COM1/TLM"; cs.LinkTimer = "00:04:12"; cs.LinkUp = true;
            cs.SelectedPage = 2;
            ChromeBar.Build(dl, W, H, cs);

            if (dl.Overflowed)
                Console.WriteLine("  WARNING page NAV/ORBIT SUBORBITAL KERBIN OVERFLOWED at " + dl.Capacity);

            string path = Path.Combine(outDir, "page2_nav_orbit_suborbital_kerbin.png");
            Render(dl, W, H, path);
            Console.WriteLine("  " + path + "   " + W + "x" + H + "   " + dl.Count + " commands");
        }

        // NAV / PLANET: the 3D globe view. The globe itself is the same textured disc the ORBIT view
        // draws (from the body-map stand-in here), and the orbit overlay is pure 2D projection, so
        // unlike a camera view the preview shows the WHOLE thing - which is exactly what makes the
        // orbit-on-globe projection and its occlusion cheap to iterate.
        {
            MapView planet = MapProjection.NextMode(MapProjection.NextMode(MapProjection.Default()));

            // A synthetic inclined orbit so the projection + far-side occlusion are visible offline.
            PlanetOverlay ov = new PlanetOverlay();
            int N = PlanetOverlay.DefaultSamples;
            double[] olat = new double[N], olon = new double[N], orat = new double[N];
            const double inc = 51.6, D2R = Math.PI / 180.0, R2D = 180.0 / Math.PI;
            for (int i = 0; i < N; i++)
            {
                double th = (360.0 * i / (N - 1)) * D2R;
                olat[i] = Math.Asin(Math.Sin(inc * D2R) * Math.Sin(th)) * R2D;
                olon[i] = Math.Atan2(Math.Cos(inc * D2R) * Math.Sin(th), Math.Cos(th)) * R2D;
                orat[i] = 1.06;
            }
            ov.Ready = true;
            ov.OrbitLat = olat; ov.OrbitLon = olon; ov.OrbitRatio = orat; ov.OrbitCount = N;
            ov.Vessel = new GlobePoint { Lat = olat[0], Lon = olon[0], Ratio = 1.06, Has = true };
            ov.Ap = new GlobePoint { Lat = olat[N / 4], Lon = olon[N / 4], Ratio = 1.06, Has = true };
            ov.Pe = new GlobePoint { Lat = olat[3 * N / 4], Lon = olon[3 * N / 4], Ratio = 1.06, Has = true };
            ps.Planet = ov;
            ps.HasFix = true; ps.Latitude = 0.0; ps.Longitude = 0.0;

            dl.Clear();
            Pages.Build(dl, 2, W, H, ps, planet, 2);
            ChromeState cs = new ChromeState();
            cs.Met = "T+ 00:12:34"; cs.VehicleState = "NOMINAL";
            cs.LinkName = "COM1/TLM"; cs.LinkTimer = "00:04:12"; cs.LinkUp = true;
            cs.SelectedPage = 2;
            ChromeBar.Build(dl, W, H, cs);

            if (dl.Overflowed)
                Console.WriteLine("  WARNING page NAV/PLANET OVERFLOWED at " + dl.Capacity);

            string path = Path.Combine(outDir, "page2_nav_planet.png");
            Render(dl, W, H, path);
            Console.WriteLine("  " + path + "   " + W + "x" + H + "   " + dl.Count + " commands");
        }

        // ---- COVER (Figma rebuild, node 12221-244): the Deorbit dashboard, drawn standalone with
        // its OWN chrome (its own top bar), so no ChromeBar. ps.Planet is still the inclined-orbit
        // overlay set up just above, so the cover's globe shows the same live disc + track. ----
        {
            MapView planet = MapProjection.NextMode(MapProjection.NextMode(MapProjection.Default()));
            // Render at 2x the screen size: the Figma assets carry 2px hairline borders that fall to
            // sub-pixel (~0.7px) at 1280 and drop inconsistently; 2x keeps them crisp (the in-game
            // RenderTexture should match — screenWidth 2560 in the cfg).
            int CW = W * 2, CH = H * 2;
            DisplayList cdl = new DisplayList(CoverPage.Commands + 400);
            CoverPage.Build(cdl, CW, CH, ps, planet);
            if (cdl.Overflowed) Console.WriteLine("  WARNING COVER OVERFLOWED at " + cdl.Capacity);
            string path = Path.Combine(outDir, "cover.png");
            Render(cdl, CW, CH, path);
            Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + cdl.Count + " commands");
        }

        // ---- SETTINGS / AUDIO (Figma rebuild, A-Settings) — Cabin selected, 2x render ----
        {
            int CW = W * 2, CH = H * 2;
            DisplayList sdl = new DisplayList(SettingsAudioPage.Commands + 200);
            SettingsAudioPage.Build(sdl, CW, CH, 2);
            if (sdl.Overflowed) Console.WriteLine("  WARNING SETTINGS_AUDIO OVERFLOWED at " + sdl.Capacity);
            string path = Path.Combine(outDir, "settings_audio.png");
            Render(sdl, CW, CH, path);
            Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + sdl.Count + " commands");
        }

        // ---- Complex frame pages shown from their Figma export (attitude HUD, procedure, cabin) ----
        foreach (string fk in new[] { "frame58", "frame59", "frame66" })
        {
            int CW = W * 2, CH = H * 2;
            DisplayList fdl = new DisplayList(FigmaFramePage.Commands + 4);
            FigmaFramePage.Build(fdl, CW, CH, fk);
            if (fdl.Overflowed) Console.WriteLine("  WARNING " + fk + " OVERFLOWED");
            string path = Path.Combine(outDir, fk + ".png");
            Render(fdl, CW, CH, path);
            Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + fdl.Count + " commands");
        }

        // ---- Frame 58 LIVE HUD (attitude ball + readouts; nose cone toggles the centre image) ----
        // Two renders: nose CLOSED -> the live navball attitude view; nose OPEN -> the docking-cam feed
        // (which has no honest preview still, so the centre is the dark it will have when nothing is in
        // view — the game supplies the real forward camera there).
        {
            int CW = W * 2, CH = H * 2;
            bool savedNose = ps.Steps.NoseConeOpen;
            foreach (bool open in new[] { false, true })
            {
                ps.Steps.NoseConeOpen = open;
                DisplayList hdl = new DisplayList(Frame58Hud.Commands + 60);
                Frame58Hud.Build(hdl, CW, CH, ps);
                if (hdl.Overflowed) Console.WriteLine("  WARNING FRAME58_HUD OVERFLOWED at " + hdl.Capacity);
                string path = Path.Combine(outDir, open ? "frame58_hud_noseopen.png" : "frame58_hud.png");
                Render(hdl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + hdl.Count + " commands");
            }
            ps.Steps.NoseConeOpen = savedNose;
        }

        // ---- Figma UI navigation: dispatcher + placeholder + shared back chevron ----
        {
            int CW = W * 2, CH = H * 2;
            foreach (UiPage up in new[] { UiPage.Cover, UiPage.Menu, UiPage.PhaseDeport, UiPage.Hud, UiPage.SuitCheck, UiPage.Vehicle, UiPage.VehicleMech, UiPage.Cabin, UiPage.AudioVideo, UiPage.VrioTest,
                                          UiPage.VehicleCrew, UiPage.VehiclePropulsion, UiPage.VehiclePower, UiPage.VehicleAvionics, UiPage.VehicleGnc, UiPage.VehicleThermal,
                                          UiPage.ManualChute, UiPage.Docking, UiPage.Rendezvous, UiPage.DeorbitBurnPrep, UiPage.EntryProcedure,
                                          UiPage.SystemsTree, UiPage.SystemsPid, UiPage.Ascent, UiPage.NavOrbitPlot })
            {
                DisplayList udl = new DisplayList(600);
                FigmaUI.Build(udl, up, CW, CH, ps, MapProjection.Default());
                if (udl.Overflowed) Console.WriteLine("  WARNING UI " + up + " OVERFLOWED");
                string path = Path.Combine(outDir, "ui_" + up.ToString().ToLowerInvariant() + ".png");
                Render(udl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + udl.Count + " commands");
            }

            // ---- T13c: the prox-ops / procedure pages in the states their new live values have ----
            // The loop above renders each of these once, from the shared orbit fixture. Both pages now
            // have a SECOND look that only appears when the vessel is in a different state, and the same
            // "anything reachable needs a render" rule T4/T5 set applies to a state as much as to a
            // control. The Manual Chute strip is the deorbit look (SPLASHDOWN TIME is only meaningful on
            // a real descent); the Docking readouts are the no-target look, which is the failure mode
            // that matters most here - nothing to be misaligned with must read as dashes, not as a
            // confident 0.0 degrees of error against nothing.
            {
                bool savedShown = ps.SplashdownShown;
                string savedSplash = ps.SplashdownText;
                ps.SplashdownShown = true; ps.SplashdownText = "T- 01:08:36";
                DisplayList cdl = new DisplayList(600);
                FigmaUI.Build(cdl, UiPage.ManualChute, CW, CH, ps, MapProjection.Default());
                if (cdl.Overflowed) Console.WriteLine("  WARNING UI ManualChute descent OVERFLOWED");
                string path = Path.Combine(outDir, "ui_manualchute_descent.png");
                Render(cdl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + cdl.Count + " commands");
                ps.SplashdownShown = savedShown; ps.SplashdownText = savedSplash;
            }
            {
                bool savedTarget = ps.HasTarget, savedOrbit = ps.HasTargetOrbit;
                ps.HasTarget = false; ps.HasTargetOrbit = false;
                DisplayList ddl = new DisplayList(600);
                FigmaUI.Build(ddl, UiPage.Docking, CW, CH, ps, MapProjection.Default());
                if (ddl.Overflowed) Console.WriteLine("  WARNING UI Docking no-target OVERFLOWED");
                string path = Path.Combine(outDir, "ui_docking_notarget.png");
                Render(ddl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + ddl.Count + " commands");

                // The rendezvous plot's approach chord is drawn from the SAME target state, so the same
                // switch-off proves it vanishes rather than reverting to some other line.
                DisplayList rdl = new DisplayList(600);
                FigmaUI.Build(rdl, UiPage.Rendezvous, CW, CH, ps, MapProjection.Default());
                path = Path.Combine(outDir, "ui_rendezvous_notarget.png");
                Render(rdl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + rdl.Count + " commands");

                // S15's nav/orbit plot shares the SAME chord + reads the SAME RateText/RangeText, so the
                // same switch-off proves its readout dashes and the chord vanishes here too.
                DisplayList ndl = new DisplayList(600);
                FigmaUI.Build(ndl, UiPage.NavOrbitPlot, CW, CH, ps, MapProjection.Default());
                path = Path.Combine(outDir, "ui_navorbitplot_notarget.png");
                Render(ndl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + ndl.Count + " commands");
                ps.HasTarget = savedTarget; ps.HasTargetOrbit = savedOrbit;
            }
            {
                // S26: the OTHER state the ring readouts' colour rule needs a look at - all three axes
                // within DockingSimPage.CorrectedToleranceDeg of zero, so ROLL/PITCH/YAW read GREEN
                // (iss-sim: SCREEN_INVENTORY #11) and the diamond sits centred on the boresight.
                double savedRoll = ps.RollDeg, savedPitch = ps.PitchDeg, savedYaw = ps.YawDeg;
                string savedRollT = ps.RollDegText, savedPitchT = ps.PitchDegText, savedYawT = ps.YawDegText;
                ps.RollDeg = 0.1; ps.PitchDeg = -0.2; ps.YawDeg = 0.1;
                ps.RollDegText = "0.1°"; ps.PitchDegText = "-0.2°"; ps.YawDegText = "0.1°";
                DisplayList gdl = new DisplayList(600);
                FigmaUI.Build(gdl, UiPage.Docking, CW, CH, ps, MapProjection.Default());
                if (gdl.Overflowed) Console.WriteLine("  WARNING UI Docking corrected OVERFLOWED");
                string gpath = Path.Combine(outDir, "ui_docking_corrected.png");
                Render(gdl, CW, CH, gpath);
                Console.WriteLine("  " + gpath + "   " + CW + "x" + CH + "   " + gdl.Count + " commands");
                ps.RollDeg = savedRoll; ps.PitchDeg = savedPitch; ps.YawDeg = savedYaw;
                ps.RollDegText = savedRollT; ps.PitchDegText = savedPitchT; ps.YawDegText = savedYawT;
            }
            {
                bool savedValid = ps.Valid;
                ps.Valid = false;
                DisplayList ndl = new DisplayList(600);
                FigmaUI.Build(ndl, UiPage.ManualChute, CW, CH, ps, MapProjection.Default());
                string path = Path.Combine(outDir, "ui_manualchute_nofeed.png");
                Render(ndl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + ndl.Count + " commands");
                ps.Valid = savedValid;
            }

            // ---- T14: the states the TOUCH PASS created ----
            // The same rule T4/T5 set and T13c followed: anything a control can reach needs a render,
            // because a state nobody has looked at is a state nobody has checked. Two controls here
            // change what is DRAWN rather than only what is dispatched, so both get a PNG.
            {
                // Manual Chute with ENABLE BACKUP PYROS armed: §14.4(a)'s BRIGHT, on all four of the rows
                // that carry that command, and on nothing else - the three actuation steps must look
                // exactly as they did, because they cannot act and so cannot light.
                bool savedPyros = ps.BackupPyrosArmed;
                ps.BackupPyrosArmed = true;
                DisplayList adl = new DisplayList(600);
                FigmaUI.Build(adl, UiPage.ManualChute, CW, CH, ps, MapProjection.Default());
                if (adl.Overflowed) Console.WriteLine("  WARNING UI ManualChute armed OVERFLOWED");
                string path = Path.Combine(outDir, "ui_manualchute_armed.png");
                Render(adl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + adl.Count + " commands");
                ps.BackupPyrosArmed = savedPyros;
            }
            {
                // Manual Docking with both cluster magnitude toggles flipped to PRECISE - the one pair of
                // controls on that page that acts, and the only visible difference it makes.
                PageControls pc = PageControls.Default;
                pc.DockRotLarge = false; pc.DockTransLarge = false;
                DisplayList pdl = new DisplayList(600);
                FigmaUI.Build(pdl, UiPage.Docking, CW, CH, ps, MapProjection.Default(),
                              5, false, 1, CoverPage.CoverCam.Earth, Turntable.Front(), pc);
                if (pdl.Overflowed) Console.WriteLine("  WARNING UI Docking precise OVERFLOWED");
                string path = Path.Combine(outDir, "ui_docking_precise.png");
                Render(pdl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + pdl.Count + " commands");
            }

            // ---- VEHICLE ALERTS + red sub-nav (T5) ----
            // Anything reachable by a control needs a render (the T4 lesson, above). The FUNCTIONS/ALERTS
            // toggle and VehicleTabBar's per-tab severity aren't wired to touch yet (T14), so their other
            // states are only reachable by calling Build directly here, same as Cover's camera views.
            // ps.FaultText isn't set until the DOCKING PAGE PROTOTYPE block below (real glue sets it from
            // Fdir.FaultName) — the ALERTS view's FDIR row needs it now, so set the same nominal baseline
            // here first, same as that block does for itself.
            ps.Fault = FaultKind.None; ps.FaultText = "NOMINAL";
            // Propulsion is in the list because T9 gave its FUNCTIONS view a different body (the Draco
            // schematic); its ALERTS view still uses the shared template, and that pairing needs a render.
            foreach (VehicleSubsystemPage.Sub sub in new[] { VehicleSubsystemPage.Sub.Power,
                                                             VehicleSubsystemPage.Sub.Crew,
                                                             VehicleSubsystemPage.Sub.Propulsion })
            {
                DisplayList adl = new DisplayList(VehicleSubsystemPage.Commands + 60);
                VehicleSubsystemPage.Build(adl, CW, CH, sub, ps, true);
                if (adl.Overflowed) Console.WriteLine("  WARNING VEHICLE ALERTS " + sub + " OVERFLOWED");
                string path = Path.Combine(outDir, "ui_vehicle" + sub.ToString().ToLowerInvariant() + "_alerts.png");
                Render(adl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + adl.Count + " commands");
            }
            // ps.Power01 = 0.18 above is deliberately in the CAUTION band so the main Power render already
            // proves amber; push it into ALARM band here to prove the sub-nav genuinely turns red (not just
            // amber) from every vehicle page, per REAL_DRAGON_SCREENS.md's "turns red when that subview
            // holds an alert" — then restore it so every later render matches the documented baseline.
            {
                double savedPower = ps.Power01;
                string savedPowerText = ps.PowerText, savedUnit1 = ps.PowerUnit1Text, savedUnit2 = ps.PowerUnit2Text;
                ps.Power01 = 0.05;
                // T13b: the Power tab's BATTERY SOC gauge now reads Power01 AND PowerText, so pushing the
                // fraction into the alarm band without the number would put a 5 % ring under an 18 % readout
                // - the exact disagreement this wiring exists to make impossible. VesselData formats them
                // together; so does the fixture.
                ps.PowerText = "5";
                ps.PowerUnit1Text = "5 %"; ps.PowerUnit2Text = "5 %";
                DisplayList adl = new DisplayList(VehicleSubsystemPage.Commands + 60);
                VehicleSubsystemPage.Build(adl, CW, CH, VehicleSubsystemPage.Sub.Power, ps);
                string path = Path.Combine(outDir, "ui_vehiclepower_alarm.png");
                Render(adl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + adl.Count + " commands");

                DisplayList odl = new DisplayList(VehicleOverviewPage.Commands + 60);
                VehicleOverviewPage.Build(odl, CW, CH, ps);
                path = Path.Combine(outDir, "ui_vehicle_alarm.png");
                Render(odl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + odl.Count + " commands");

                // S12: VehicleMechPage's tab bar wasn't wired to Severities(s) at all, so it always read
                // nominal even on this same alarm; proves it now turns red like every other vehicle page.
                DisplayList mdl2 = new DisplayList(VehicleMechPage.Commands + 60);
                VehicleMechPage.Build(mdl2, CW, CH, ps);
                path = Path.Combine(outDir, "ui_vehiclemech_alarm.png");
                Render(mdl2, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + mdl2.Count + " commands");
                ps.Power01 = savedPower;
                ps.PowerText = savedPowerText;
                ps.PowerUnit1Text = savedUnit1; ps.PowerUnit2Text = savedUnit2;
            }

            // ---- T9: the Draco schematic FIRING, and the systems tree POWERED ----
            // Both pages are driven by live state that is idle in the shared fixture (RCS off, both
            // buses unpowered — SystemsState.Fresh's own honest starting point). The default renders
            // above prove the idle look; these prove the live one, the same "anything reachable needs
            // a render" rule T4/T5 followed. State is restored afterwards.
            {
                bool savedRcs = ps.RcsOn;
                float sTX = ps.TransX, sTY = ps.TransY, sTZ = ps.TransZ;
                float sRP = ps.RotPitch, sRY = ps.RotYaw, sRR = ps.RotRoll;
                ps.RcsOn = true;
                ps.TransX = 0.6f; ps.TransY = -0.35f; ps.TransZ = 0.4f;
                ps.RotPitch = 0.2f; ps.RotYaw = 0f; ps.RotRoll = 0.5f;
                // T13b: the "Draco Duty" readout in the data band is derived from this same demand, the
                // way VesselData derives it — so the number and the segments above it move together here
                // too, instead of the fixture's idle 0 % sitting over a firing schematic.
                string savedDuty = ps.DracoDutyText;
                ps.DracoDutyText = (PropSchematic.MaxDuty(ps) * 100f).ToString("F0") + " %";
                DisplayList pdl = new DisplayList(VehicleSubsystemPage.Commands + 60);
                VehicleSubsystemPage.Build(pdl, CW, CH, VehicleSubsystemPage.Sub.Propulsion, ps);
                if (pdl.Overflowed) Console.WriteLine("  WARNING PROP SCHEMATIC OVERFLOWED");
                string path = Path.Combine(outDir, "ui_vehiclepropulsion_firing.png");
                Render(pdl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + pdl.Count + " commands");
                ps.RcsOn = savedRcs;
                ps.DracoDutyText = savedDuty;
                ps.TransX = sTX; ps.TransY = sTY; ps.TransZ = sTZ;
                ps.RotPitch = sRP; ps.RotYaw = sRY; ps.RotRoll = sRR;
            }
            // ---- S46: the same tab with NO Kerbal Engineer ----
            // The baseline render above shows "Thrust Avail" live. This is the other half of that claim and
            // the half that is easy to get wrong: KER not installed, or installed with no result yet for this
            // vessel, or DOCKED (KSP merges both craft into one Vessel and KER then simulates the STACK, so
            // the Dragon's own thrust is unknowable). All three arrive as an EMPTY group, and the row must
            // fall back to the page's dash - never a stale number, never a confident zero. Distinct from the
            // Valid=false "no feed at all" case: everything else on this tab is still live here.
            {
                KerPerformance savedKer = ps.Ker;
                ps.Ker = new KerPerformance();
                DisplayList kdl = new DisplayList(VehicleSubsystemPage.Commands + 60);
                VehicleSubsystemPage.Build(kdl, CW, CH, VehicleSubsystemPage.Sub.Propulsion, ps);
                if (kdl.Overflowed) Console.WriteLine("  WARNING PROP KER-ABSENT OVERFLOWED");
                string path = Path.Combine(outDir, "ui_vehiclepropulsion_kerabsent.png");
                Render(kdl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + kdl.Count + " commands");
                ps.Ker = savedKer;
            }
            {
                SystemsState savedSys = ps.Systems;
                ps.Systems.Bus1On = true; ps.Systems.Bus2On = true;
                ps.Systems.C1 = StringState.Tripped;      // one tripped, one isolated (B2, from the fixture)
                DisplayList tdl = new DisplayList(SystemsTreePage.Commands + 60);
                SystemsTreePage.Build(tdl, CW, CH, ps);
                if (tdl.Overflowed) Console.WriteLine("  WARNING SYSTEMS TREE OVERFLOWED");
                string path = Path.Combine(outDir, "ui_systemstree_live.png");
                Render(tdl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + tdl.Count + " commands");
                ps.Systems = savedSys;
            }

            // ---- T13a: the VEHICLE family with NO FEED ----
            // Every number on these three pages is live now, so the failure mode has a look of its own and
            // it is the one that matters most: a screen confidently reading 0.0 is indistinguishable from a
            // dead one (Pages.cs). These renders prove the dashes appear and the rings empty rather than
            // the pages drawing a plausible zero. Same "anything reachable needs a render" rule as T4/T5.
            {
                bool savedValid = ps.Valid;
                ps.Valid = false;
                DisplayList ndl = new DisplayList(VehicleOverviewPage.Commands + 60);
                VehicleOverviewPage.Build(ndl, CW, CH, ps);
                string path = Path.Combine(outDir, "ui_vehicle_nofeed.png");
                Render(ndl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + ndl.Count + " commands");

                DisplayList mdl = new DisplayList(VehicleMechPage.Commands + 60);
                VehicleMechPage.Build(mdl, CW, CH, ps);
                path = Path.Combine(outDir, "ui_vehiclemech_nofeed.png");
                Render(mdl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + mdl.Count + " commands");

                // T13b: the same failure mode on the subsystem template, which all six tabs share. CREW
                // is the tab with the most live values on it, so it is the one where a stale ring or a
                // leftover number would show most plainly.
                DisplayList sdl2 = new DisplayList(VehicleSubsystemPage.Commands + 60);
                VehicleSubsystemPage.Build(sdl2, CW, CH, VehicleSubsystemPage.Sub.Crew, ps);
                path = Path.Combine(outDir, "ui_vehiclecrew_nofeed.png");
                Render(sdl2, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + sdl2.Count + " commands");
                ps.Valid = savedValid;
            }

            // ---- S24: AVIONICS with CommNet off/absent, vessel otherwise VALID ----
            // Distinct from the no-feed renders above: this vessel is fine, it simply has no CommNetVessel
            // (CommNet disabled in difficulty settings, RemoteTech installed, or no comm hardware) - the
            // one case S24's own guard names. S-BAND COMMS / Uplink / Downlink must dash exactly like the
            // tab's other unsourced rows, never keep showing the linked-fixture's stale text.
            {
                string savedBand = ps.SBandText; bool savedLinked = ps.SBandLinked;
                string savedUp = ps.UplinkText, savedDown = ps.DownlinkText;
                double savedSig = ps.CommSignal01;
                ps.SBandText = null; ps.SBandLinked = false;
                ps.UplinkText = null; ps.DownlinkText = null; ps.CommSignal01 = 0.0;
                DisplayList codl = new DisplayList(VehicleSubsystemPage.Commands + 60);
                VehicleSubsystemPage.Build(codl, CW, CH, VehicleSubsystemPage.Sub.Avionics, ps);
                string path = Path.Combine(outDir, "ui_vehicleavionics_commoff.png");
                Render(codl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + codl.Count + " commands");
                ps.SBandText = savedBand; ps.SBandLinked = savedLinked;
                ps.UplinkText = savedUp; ps.DownlinkText = savedDown; ps.CommSignal01 = savedSig;
            }

            // Cover with the LAST phase selected (Manual Chute Deploy, rail slot 6) to prove the expanded
            // seven-item rail + the in-page highlight/heading move to the bottom row.
            {
                DisplayList cdl = new DisplayList(600);
                CoverPage.Build(cdl, CW, CH, ps, MapProjection.Default(), 6);
                string path = Path.Combine(outDir, "ui_cover_phase6.png");
                Render(cdl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + cdl.Count + " commands");
            }

            // Cover with the Reference Content phase selected (rail slot 5, T3): proves the deorbit
            // quick-reference body swap replaces the baked Coast-phase content in the three card slots.
            {
                DisplayList cdl = new DisplayList(600);
                CoverPage.Build(cdl, CW, CH, ps, MapProjection.Default(), 5);
                string path = Path.Combine(outDir, "ui_cover_phase5.png");
                Render(cdl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + cdl.Count + " commands");
            }

            // ---- COVER CAMERA VIEWS (T4) ----
            // NEXT VIEW cycles First.vue's three views and cover.png above is only the first of them.
            // Anything reachable by a control needs a render, not just the state it happens to start in
            // — the lesson NAV's orbit view and its invisible globe both taught.
            foreach (CoverPage.CoverCam cam in new[] { CoverPage.CoverCam.Map, CoverPage.CoverCam.Capsule })
            {
                MapView cv = MapProjection.WithMode(MapProjection.Default(), CoverPage.CamMapMode(cam));
                cv = MapProjection.Centre(cv, ps.Latitude, ps.Longitude);
                DisplayList cdl = new DisplayList(600);
                CoverPage.Build(cdl, CW, CH, ps, cv, 1, cam);
                if (cdl.Overflowed)
                    Console.WriteLine("  WARNING COVER " + cam + " OVERFLOWED at " + cdl.Capacity);
                string path = Path.Combine(outDir, "ui_cover_cam_" + cam.ToString().ToLowerInvariant() + ".png");
                Render(cdl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + cdl.Count + " commands");
            }

            // The MAP view zoomed in, for MapProjection's wrap/clamp — the same reason the NAV preview
            // is rendered at a moderate zoom rather than at the easy whole-body default.
            {
                MapView cv = MapProjection.WithMode(MapProjection.Default(), NavMode.Map);
                cv = MapProjection.Centre(cv, ps.Latitude, ps.Longitude);
                cv = MapProjection.Zoom(cv, 2);
                DisplayList cdl = new DisplayList(600);
                CoverPage.Build(cdl, CW, CH, ps, cv, 1, CoverPage.CoverCam.Map);
                if (cdl.Overflowed) Console.WriteLine("  WARNING COVER MAP ZOOM OVERFLOWED at " + cdl.Capacity);
                string path = Path.Combine(outDir, "ui_cover_cam_map_zoom.png");
                Render(cdl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + cdl.Count + " commands");
            }

            // ---- THE CAPSULE TURNTABLE (T11a, §5) ----
            // Driven by the REAL drag function, never by a frame index typed in here: four
            // quarter-sweep drags across the vehicle must walk a quarter revolution each and land
            // back on the front, which is the whole claim these four PNGs exist to show. A preview
            // that set the frame directly would prove the ASSETS load and nothing about the maths.
            {
                float sx, sy, sw, sh;
                CoverPage.CapsuleRect(CW, CH, out sx, out sy, out sw, out sh);
                MapView cv = MapProjection.WithMode(MapProjection.Default(), NavMode.Planet);
                TurntableState t = Turntable.Front();
                for (int q = 0; q < 4; q++)
                {
                    DisplayList cdl = new DisplayList(600);
                    CoverPage.Build(cdl, CW, CH, ps, cv, 1, CoverPage.CoverCam.Capsule, t);
                    if (cdl.Overflowed)
                        Console.WriteLine("  WARNING COVER TURNTABLE OVERFLOWED at " + cdl.Capacity);
                    string path = Path.Combine(outDir, "ui_cover_turntable_" + q + ".png");
                    Render(cdl, CW, CH, path);
                    Console.WriteLine("  " + path + "   frame " + Turntable.FrameOf(t)
                                      + "  az " + (int)Turntable.AngleOf(t)
                                      + "  " + Turntable.KeyOf(t));
                    t = Turntable.Drag(t, sw * Turntable.UsableSweepFraction * 0.25f, sw);
                }
                Console.WriteLine("  turntable closed the loop at frame " + Turntable.FrameOf(t)
                                  + " (want " + Turntable.FrontFrame + ")");
            }

            // ---- THE GESTURE, AS THE GLUE PLAYS IT (T11b items 4 + 5) ----
            // The four above are driven by Drag. These are driven by the GESTURE the glue actually
            // calls - Turntable.Press, one Turntable.Move per pointer sample, then Turntable.Release
            // - with the pointer positions taken from CoverPage.CapsuleRect, which is the same rect
            // the sprite is drawn from and the same one CapsuleHit accepts a press in. So this is the
            // whole chain from "a finger is at page x" to "this frame is on the glass", short only of
            // ScreenTouch's raycast, which needs the capsule.
            {
                float sx, sy, sw, sh;
                CoverPage.CapsuleRect(CW, CH, out sx, out sy, out sw, out sh);
                MapView cv = MapProjection.WithMode(MapProjection.Default(), NavMode.Planet);

                // A press in the middle of the vehicle, then a slide to the right delivered as four
                // samples - the shape ScreenPainter.TouchDrag sees, one per rendered frame.
                float x0 = sx + sw * 0.5f;
                TurntableTouch g = Turntable.Press(x0);
                TurntableState t = Turntable.Front();
                Console.WriteLine("  gesture: press at x=" + (int)x0 + " on a "
                                  + (int)sw + " px capsule slot");
                for (int i = 0; i < 4; i++)
                {
                    DisplayList cdl = new DisplayList(600);
                    CoverPage.Build(cdl, CW, CH, ps, cv, 1, CoverPage.CoverCam.Capsule, t);
                    if (cdl.Overflowed)
                        Console.WriteLine("  WARNING COVER GESTURE OVERFLOWED at " + cdl.Capacity);
                    string path = Path.Combine(outDir, "ui_cover_turntable_drag_" + i + ".png");
                    Render(cdl, CW, CH, path);
                    Console.WriteLine("  " + path + "   frame " + Turntable.FrameOf(t)
                                      + "  az " + (int)Turntable.AngleOf(t)
                                      + "  travelled " + (int)g.TravelPx + " px");
                    t = Turntable.Move(t, g, x0 + sw * Turntable.UsableSweepFraction * 0.22f * (i + 1),
                                       sw, out g);
                }

                // Letting go of a real drag leaves the vehicle where the finger left it.
                TurntableTouch after;
                Console.WriteLine("  gesture: release after " + (int)g.TravelPx + " px is a "
                                  + (Turntable.IsTap(g, sw) ? "TAP" : "DRAG"));
                t = Turntable.Release(t, g, sw, out after);
                Console.WriteLine("  gesture: after release, frame " + Turntable.FrameOf(t));

                // ---- THE RESET (section 5's C4 "front tap") ----
                // From that turned state, a press and release that never travelled: the vehicle goes
                // back to the AUTHORED front, frame 0, which is what this PNG has to show.
                TurntableTouch tap = Turntable.Press(x0);
                Console.WriteLine("  gesture: a press with no travel is a "
                                  + (Turntable.IsTap(tap, sw) ? "TAP" : "DRAG"));
                t = Turntable.Release(t, tap, sw, out after);

                DisplayList rdl = new DisplayList(600);
                CoverPage.Build(rdl, CW, CH, ps, cv, 1, CoverPage.CoverCam.Capsule, t);
                if (rdl.Overflowed)
                    Console.WriteLine("  WARNING COVER RESET OVERFLOWED at " + rdl.Capacity);
                string rpath = Path.Combine(outDir, "ui_cover_turntable_reset.png");
                Render(rdl, CW, CH, rpath);
                Console.WriteLine("  " + rpath + "   frame " + Turntable.FrameOf(t)
                                  + "  " + Turntable.KeyOf(t)
                                  + "   (want frame " + Turntable.FrontFrame + ")");

                // ---- WHAT THE RESIDENCY POLICY WOULD HOLD (T11b item 6) ----
                // No PNG to show: it is a memory claim, so it is printed. ImageStore acts on exactly
                // these numbers - this is the same pure policy, asked the same question.
                int[] one = { Turntable.FrameOf(t), Turntable.NotShowing, Turntable.NotShowing };
                int[] three = { 0, 12, 24 };
                Console.WriteLine("  residency: one screen holds " + Turntable.ResidentCount(one)
                                  + " frames ("
                                  + (Turntable.ResidentCount(one) * (long)Turntable.FrameBytes / (1024 * 1024))
                                  + " MB); three diverged screens hold "
                                  + Turntable.ResidentCount(three) + " ("
                                  + (Turntable.ResidentCount(three) * (long)Turntable.FrameBytes / (1024 * 1024))
                                  + " MB); the whole sequence would be "
                                  + ((long)Turntable.Count * Turntable.FrameBytes / (1024 * 1024)) + " MB");
            }

            // EVERY frame of the sequence on one sheet. Two jobs: it is the only render that touches
            // all 36 keys, so a frame missing from art/cover/ shows up here as a MISSING line rather
            // than on the glass; and it is how the sequence itself is judged - whether consecutive
            // frames actually step, which is the thing a single still cannot show.
            {
                const int Cols = 6, CellW = 150, CellH = 300, Pad = 10;
                int rows = (Turntable.Count + Cols - 1) / Cols;
                int shW = Cols * CellW + Pad * 2, shH = rows * CellH + Pad * 2;
                DisplayList sdl = new DisplayList(Turntable.Count + 8);
                for (int i = 0; i < Turntable.Count; i++)
                {
                    float cxp = Pad + (i % Cols) * CellW, cyp = Pad + (i / Cols) * CellH;
                    float iw = CellH * 0.9f * ((float)Turntable.FrameW / Turntable.FrameH);
                    sdl.Asset(Turntable.Key(i), cxp + (CellW - iw) * 0.5f, cyp + CellH * 0.05f,
                              iw, CellH * 0.9f, DragonPalette.White);
                }
                string path = Path.Combine(outDir, "ui_turntable_sheet.png");
                Render(sdl, shW, shH, path);
                Console.WriteLine("  " + path + "   " + shW + "x" + shH + "   "
                                  + sdl.Count + " frames");
            }

            // ---- S31/S32: the Suit Leak Check's RESULTS, and the state the crew acts in ----
            // The page itself comes out of the loop above in its RESTING state (no run made, the four
            // differentials live off the fixture's cabin, every STATUS a verdict on them, TROUBLESHOOT
            // dimmed) - that is ui_suitcheck.png. Everything below only exists behind a run, so each
            // needs its own render or none of it has a cheap evidence channel. The 5% roll is seedable
            // exactly so this is possible without waiting for one: seed 0 is a run that found nothing,
            // SeedForLeak(3) is one that found a leak in suit 3.
            {
                DisplayList udl = new DisplayList(600);
                SuitCheckPage.Build(udl, CW, CH, 0, true, SuitLeak.From(ps, 0, true, 0u));
                if (udl.Overflowed) Console.WriteLine("  WARNING UI SUITCHECK/POPUP OVERFLOWED");
                string path = Path.Combine(outDir, "ui_suitcheck_popup.png");
                Render(udl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + udl.Count + " commands");
            }
            {
                // S32 RE-AIMED THIS RENDER. It was the leak RESULT BOX (now ui_suitcheck_leak_popup.png
                // below, beside the clean box). The box sits under an 82%-opaque scrim, so the one thing
                // S32 changed - TROUBLESHOOT lighting up - is not inspectable through it. What this file
                // shows instead is the state the crew actually acts in: the run found a leak, the box has
                // been closed, the table is still reading "Failed Low" (the countdown parks at 0 rather
                // than springing back to 5) and the fail branch's control is LIVE.
                uint leak = SuitLeak.SeedForLeak(3);
                SuitCheckState st = SuitLeak.From(ps, 0, true, leak);
                DisplayList udl = new DisplayList(600);
                SuitCheckPage.Build(udl, CW, CH, 0, false, st);
                if (udl.Overflowed) Console.WriteLine("  WARNING UI SUITCHECK/LEAK OVERFLOWED");
                string path = Path.Combine(outDir, "ui_suitcheck_leak.png");
                Render(udl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + udl.Count
                                  + " commands   seed " + leak
                                  + "   suit 3 delta " + SuitLeak.Text(st.Delta(2))
                                  + "   troubleshoot "
                                  + (SuitCheckPage.Available(SuitCheckPage.SuitAct.Troubleshoot, st)
                                     ? "LIVE" : "inert"));
            }
            {
                uint leak = SuitLeak.SeedForLeak(3);
                DisplayList udl = new DisplayList(600);
                SuitCheckPage.Build(udl, CW, CH, 0, true, SuitLeak.From(ps, 0, true, leak));
                if (udl.Overflowed) Console.WriteLine("  WARNING UI SUITCHECK/LEAK-POPUP OVERFLOWED");
                string path = Path.Combine(outDir, "ui_suitcheck_leak_popup.png");
                Render(udl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + udl.Count + " commands");
            }
        }

        // EVERY SETTINGS TAB. Four subviews behind one page is four things with no cheap evidence
        // channel unless each one is rendered - the lesson NAV's orbit view and the globe both taught.
        for (int t = 0; t < SettingsPage.Tabs.Length; t++)
        {
            dl.Clear();
            Pages.Build(dl, 4, W, H, ps, MapProjection.Default(), 2, t);
            ChromeState cs = new ChromeState();
            cs.Met = "T+ 00:12:34"; cs.VehicleState = "NOMINAL";
            cs.LinkName = "COM1/TLM"; cs.LinkTimer = "00:04:12"; cs.LinkUp = true;
            cs.SelectedPage = 4;
            ChromeBar.Build(dl, W, H, cs);
            if (dl.Overflowed) Console.WriteLine("  WARNING SETTINGS/" + SettingsPage.Tabs[t]
                                                 + " OVERFLOWED");
            string path = Path.Combine(outDir,
                "page4_settings_" + SettingsPage.Tabs[t].ToLowerInvariant() + ".png");
            Render(dl, W, H, path);
            Console.WriteLine("  " + path + "   " + W + "x" + H + "   " + dl.Count + " commands");
        }

        // VEHICLE's second TAB. Same lesson as NAV's second view: anything reachable by a control
        // needs a render, or it goes into the game with no cheap evidence channel at all.
        {
            dl.Clear();
            Pages.Build(dl, 1, W, H, ps, MapProjection.Default(), 2, 1);
            ChromeState cs = new ChromeState();
            cs.Met = "T+ 00:12:34"; cs.VehicleState = "NOMINAL";
            cs.LinkName = "COM1/TLM"; cs.LinkTimer = "00:04:12"; cs.LinkUp = true;
            cs.SelectedPage = 1;
            ChromeBar.Build(dl, W, H, cs);
            if (dl.Overflowed) Console.WriteLine("  WARNING VEHICLE/MECH OVERFLOWED");
            string path = Path.Combine(outDir, "page1_vehicle_mech.png");
            Render(dl, W, H, path);
            Console.WriteLine("  " + path + "   " + W + "x" + H + "   " + dl.Count + " commands");
        }

        // ---- THE CREW CHECKLIST CARD ----
        // The flagship interactive moment: the crew works the GO/NO-GO poll on the FLIGHT page. Rendered
        // GO-ready so the checked items and the green GO / amber NO-GO / red ABORT plates are all visible.
        {
            ps.GateActive = true;
            ps.GateTitle = "GO / NO-GO FOR LAUNCH";
            ps.GateStage = GatePhase.GoReady;
            ps.GateItems = new GateItemView[]
            {
                new GateItemView { Label = "GO/NO-GO poll complete", Checked = true, CrewActionable = true },
                new GateItemView { Label = "Dragon crew - GO",       Checked = true, CrewActionable = true },
                new GateItemView { Label = "SpaceX - GO for launch",  Checked = true, CrewActionable = true }
            };
            dl.Clear();
            Pages.Build(dl, 0, W, H, ps, MapProjection.Default(), 2);
            ChromeState cs = new ChromeState();
            cs.Met = "T- 00:00:45"; cs.VehicleState = "GO FOR LAUNCH";
            cs.LinkName = "COM1/TLM"; cs.LinkTimer = "00:04:12"; cs.LinkUp = true;
            cs.SelectedPage = 0;
            ChromeBar.Build(dl, W, H, cs);
            if (dl.Overflowed) Console.WriteLine("  WARNING FLIGHT/GATE OVERFLOWED at " + dl.Capacity);
            string path = Path.Combine(outDir, "page0_flight_gate.png");
            Render(dl, W, H, path);
            Console.WriteLine("  " + path + "   " + W + "x" + H + "   " + dl.Count + " commands");
            ps.GateActive = false;
        }

        // ---- THE ABORT OVERLAY ----
        // The emergency alert drawn OVER whatever page is up. Rendered here over the FLIGHT page with both
        // flash phases ON, and no art file present, so the layout + the plain-wordmark fallback are visible
        // (in-game, art/dontpanic.png replaces the wordmark). This is the look judged before a restart.
        {
            dl.Clear();
            Pages.Build(dl, 0, W, H, ps, MapProjection.Default(), 2);
            ChromeState cs = new ChromeState();
            cs.Met = "T+ 00:02:03"; cs.VehicleState = "ABORT"; cs.SelectedPage = 0;
            ChromeBar.Build(dl, W, H, cs);

            // Load the optional user art + its true aspect so the preview composites it exactly as the game will.
            float aspect = 1.5f; bool hasImg = false;
            string art = Path.Combine(Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "GameData", "DragonScreen", "art", Images.FileName(ImageId.DontPanic));
            if (File.Exists(art))
            {
                byte[] hdr = new byte[24];
                using (var fs = File.OpenRead(art)) fs.Read(hdr, 0, 24);
                int fw = (hdr[16] << 24) | (hdr[17] << 16) | (hdr[18] << 8) | hdr[19];
                int fh = (hdr[20] << 24) | (hdr[21] << 16) | (hdr[22] << 8) | hdr[23];
                if (fw > 0 && fh > 0) { aspect = (float)fw / fh; hasImg = true; }
                Console.WriteLine("  dontpanic.png " + fw + "x" + fh + "  aspect " + aspect.ToString("F3"));
            }
            AbortOverlay.Build(dl, W, H, true, hasImg, aspect);
            if (dl.Overflowed) Console.WriteLine("  WARNING ABORT OVERLAY OVERFLOWED at " + dl.Capacity);
            string path = Path.Combine(outDir, "abort_overlay.png");
            Render(dl, W, H, path);
            Console.WriteLine("  " + path + "   " + W + "x" + H + "   " + dl.Count + " commands"
                              + (hasImg ? "  (your art composited)" : "  (fallback wordmark)"));
        }

        // ---- DOCKING PAGE PROTOTYPE (central navball) — display-only, for the layout comparison ----
        {
            ps.PitchRateText = "0.1 °/s"; ps.YawRateText = "0.0 °/s"; ps.RollRateText = "0.0 °/s";
            ps.Mode = ControlMode.Auto; ps.ModeText = "AUTO";
            ps.RollText = "15.0°"; ps.PitchText = "-20.0°"; ps.YawText = "-10.0°";
            ps.RangeText = "202.6 m"; ps.RateText = "-0.25 m/s"; ps.Closing = true;
            ps.OffXText = "22.7 m"; ps.OffYText = "0.1 m"; ps.OffZText = "0.0 m";
            ps.Align01 = 0.06; ps.Fault = FaultKind.None; ps.FaultText = "NOMINAL";
            dl.Clear();
            DockingPageCentral.Build(dl, W, H, ps);
            ChromeState cs = new ChromeState();
            cs.Met = "T+ 21:14:07"; cs.VehicleState = "NOMINAL";
            cs.LinkName = "SPX/TDRS"; cs.LinkTimer = "00:04:12"; cs.LinkUp = true; cs.SelectedPage = 3;
            ChromeBar.Build(dl, W, H, cs);
            if (dl.Overflowed) Console.WriteLine("  WARNING DOCKING-CENTRAL OVERFLOWED at " + dl.Capacity);
            string dcpath = Path.Combine(outDir, "page_docking_central.png");
            Render(dl, W, H, dcpath);
            Console.WriteLine("  " + dcpath + "   " + W + "x" + H + "   " + dl.Count + " commands");
        }

        // ---- COMPONENT GALLERY (Phase 6): the pure display widgets, rendered so they can be looked at ----
        // Not a page — a bench. AttitudeHud composites the live navball (a circular skin stand-in here) with
        // the docking overlay; the small widgets are shown in their states. Judged from the PNG, game closed.
        {
            dl.Clear();
            dl.Text("COMPONENT GALLERY", 40f, 22f, Typography.Hero, TextAlign.Left, DragonPalette.Text1);
            dl.Text("pure display widgets — AttitudeHud (live navball) · NumericReadout · StatusIndicator · TargetReticle",
                    40f, 76f, Typography.Body, TextAlign.Left, DragonPalette.Text6);

            // AttitudeHud with the live navball, left half.
            AttitudeHudState a = new AttitudeHudState();
            a.Valid = true;
            a.RollErr = "15.0°";  a.RollRate = "0.0 °/s";
            a.PitchErr = "-20.0°"; a.PitchRate = "0.1 °/s";
            a.YawErr = "-10.0°";  a.YawRate = "0.0 °/s";
            a.Range = "202.6 m";  a.Rate = "-0.25 m/s"; a.Closing = true;
            a.OffX = "22.7 m"; a.OffY = "0.1 m"; a.OffZ = "0.0 m";
            AttitudeHud.Draw(dl, W * 0.33f, H * 0.55f, H * 0.24f, a);

            // Right column: the small widgets in their states.
            float rx = W * 0.63f, ry = 150f;
            dl.Text("StatusIndicator", rx, ry, Typography.Body, TextAlign.Left, DragonPalette.Text5); ry += 32f;
            StatusIndicator.Badge(dl, rx, ry, 150f, 44f, "AUTO", StatusIndicator.Colour(ControlMode.Auto));
            StatusIndicator.Badge(dl, rx + 168f, ry, 150f, 44f, "MANUAL", StatusIndicator.Colour(ControlMode.Manual));
            ry += 58f;
            StatusIndicator.Badge(dl, rx, ry, 150f, 44f, "ABORT", StatusIndicator.Colour(ControlMode.Abort));
            StatusIndicator.Badge(dl, rx + 168f, ry, 150f, 44f, "RECOVERY", StatusIndicator.Colour(ControlMode.Recovery));
            ry += 74f;
            StatusIndicator.Lamp(dl, rx, ry, "GNC", "MANUAL", DragonPalette.Accent);
            StatusIndicator.Lamp(dl, rx + 168f, ry, "STATE", "CAUTION", Alarms.Colour(Severity.Caution));
            ry += 86f;

            dl.Text("NumericReadout", rx, ry, Typography.Body, TextAlign.Left, DragonPalette.Text5); ry += 32f;
            NumericReadout.Value(dl, rx, ry, "ALTITUDE", "393.3 km", DragonPalette.Text0, Typography.Value);
            NumericReadout.Value(dl, rx + 210f, ry, "RATE (no data)", null, DragonPalette.AccentDim, Typography.Value);
            ry += 70f;
            NumericReadout.Paired(dl, rx, ry, "PITCH", "-20.0°", "0.0 °/s");
            NumericReadout.Paired(dl, rx + 150f, ry, "ROLL", "15.0°", "0.0 °/s");
            ry += 104f;

            dl.Text("TargetReticle", rx, ry, Typography.Body, TextAlign.Left, DragonPalette.Text5); ry += 42f;
            TargetReticle.Crosshair(dl, rx + 40f, ry + 16f, 26f, DragonPalette.Accent);
            TargetReticle.Marker(dl, rx + 140f, ry + 16f, 14f, DragonPalette.Go);

            if (dl.Overflowed) Console.WriteLine("  WARNING GALLERY OVERFLOWED at " + dl.Capacity);
            string gpath = Path.Combine(outDir, "page_gallery.png");
            Render(dl, W, H, gpath);
            Console.WriteLine("  " + gpath + "   " + W + "x" + H + "   " + dl.Count + " commands");
        }

        // ---- THE LOWER CONSOLE (§14.4 a + b) ----------------------------------------------------
        // NOT a screen - see pure/PanelBoardPage.cs. The console's buttons are meshes on Tundra's IVA
        // prop and its indicators are Tundra's dashes; this draws them only so the LIGHTING can be
        // judged with the game closed, which is otherwise the one part of the panel that costs a
        // restart to look at.
        //
        // Four scenes, because the decision has four halves worth seeing: nothing pressed, an arming
        // holding, that arming fired from the other seat, and an inert control pressed. What must be
        // true in all four is that no dash is any colour but bright or as-modelled.
        {
            const int PW = 3600, PH = 540;

            // -- at rest -------------------------------------------------------------------------
            PanelBoard rest = new PanelBoard();
            PanelScene(outDir, "panel_rest", PW, PH, rest, -1,
                       "LOWER CONSOLE - AT REST",
                       "No press. Every dash as modelled. This is what the panel looks like for most "
                       + "of a mission.");

            // -- armed: DEORBIT NOW on the LEFT plate, holding ------------------------------------
            PanelBoard armed = new PanelBoard();
            int leftArm = PanelBoard.IndexOf(PanelMap.PlateLeftEmerg, PanelCommand.DeorbitNow);
            armed.Press(leftArm, false, false, false);
            armed.FlashesOut();                       // a moment later: only the HELD lamp survives
            PanelScene(outDir, "panel_armed", PW, PH, armed, leftArm,
                       "ARMED - DEORBIT NOW, LEFT PLATE",
                       "Armed and waiting on EXECUTE, so the dash is BRIGHT and it HOLDS "
                       + "(§14.4a). Nothing else is lit.");

            // -- fired: POWER 1 on, then EXECUTE from the RIGHT seat ------------------------------
            // Two things at once on purpose. POWER 1 is a real display-state command, so its lamp is
            // the ACTIVE case; EXECUTE releasing DEORBIT NOW is the FIRED case, and with no flight
            // software behind it the release acts on nothing - which since §14.4(a) is a click into
            // silence rather than a red dash. The armed lamp must go out either way.
            PanelBoard fired = new PanelBoard();
            int pwr1 = PanelBoard.IndexOf(PanelMap.PlatePower, PanelCommand.Power1);
            int rightExec = PanelBoard.IndexOf(PanelMap.PlateRightEmerg, PanelCommand.Execute);
            fired.Press(pwr1, true, true, false);                 // bus 1 on: the lamp holds
            fired.Press(leftArm, false, false, false);            // arm from the left seat
            fired.Press(rightExec, true, false, false);           // ...execute from the right one
            PanelScene(outDir, "panel_fired", PW, PH, fired, rightExec,
                       "FIRED - EXECUTE FROM THE RIGHT SEAT (POWER 1 ACTIVE)",
                       "EXECUTE is bright for the moment it fires; the armed DEORBIT NOW lamp has "
                       + "gone out; POWER 1 holds because its bus is live.");

            // -- inert: SWAP 2 -------------------------------------------------------------------
            PanelBoard inert = new PanelBoard();
            int swap2 = PanelBoard.IndexOf(PanelMap.PlateEntry, PanelCommand.SwapString2);
            PanelPressKind k = inert.Press(swap2, true, true, false);
            PanelScene(outDir, "panel_inert_swap", PW, PH, inert, swap2,
                       "INERT - SWAP 2 PRESSED (" + k + ")",
                       "Clicked" + (inert.LastClicked ? " (audible)" : " (SILENT - WRONG)")
                       + ", did nothing, lit nothing (§14.4b). The board is entirely dark, and the "
                       + "dim labels are the six controls held inert until a real source verifies them.");
        }

        return 0;
    }

    /// <summary>One console scene to PNG. Kept here rather than inline so the four read as four.</summary>
    private static void PanelScene(string outDir, string name, int w, int h,
                                   PanelBoard board, int pressed, string title, string note)
    {
        DisplayList dl = new DisplayList(PanelBoardPage.Commands);
        PanelBoardPage.Build(dl, w, h, board, title, note, pressed);
        if (dl.Overflowed) Console.WriteLine("  WARNING PANEL/" + name + " OVERFLOWED at " + dl.Capacity);

        string path = Path.Combine(outDir, name + ".png");
        Render(dl, w, h, path);
        Console.WriteLine("  " + path + "   " + w + "x" + h + "   " + dl.Count + " commands"
                          + "   lit: " + LitCount(board) + " of " + board.Count);
    }

    /// <summary>How many dashes are bright - printed so the log alone catches a lamp storm.</summary>
    private static int LitCount(PanelBoard b)
    {
        int n = 0;
        for (int i = 0; i < b.Count; i++) if (b.Lamp(i) == PanelLight.Lit) n++;
        return n;
    }

    private static void Render(DisplayList dl, int w, int h, string path)
    {
        using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            // AntiAlias because the screen runs MSAA x4. Not identical, but a hard-edged preview of
            // a smoothed screen would make every curve look worse than it is and invite fixing a
            // problem that is not there - which has already nearly happened once on this project.
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(ToColor(DragonPalette.Background));

            // Grid-fit antialiasing: the closest GDI+ gets to how a font atlas is rasterised.
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            int n = dl.Count;
            for (int i = 0; i < n; i++)
            {
                DrawCmd c = dl.At(i);
                using (SolidBrush brush = new SolidBrush(ToColor(c.Colour)))
                {
                    if (c.Kind == DrawKind.Rect)
                        g.FillRectangle(brush, c.A, c.B, c.C, c.D);
                    else if (c.Kind == DrawKind.ArcBand)
                        FillArcBand(g, brush, c);
                    else if (c.Kind == DrawKind.Line)
                        DrawLinePreview(g, c);
                    else if (c.Kind == DrawKind.Image)
                        DrawImage(g, c);
                    else
                        DrawText(g, brush, c);
                }
            }
            bmp.Save(path, ImageFormat.Png);
        }
    }

    /// <summary>One line as a round-capped pen - the preview twin of DrawLine's rotated quad.</summary>
    private static void DrawLinePreview(Graphics g, DrawCmd c)
    {
        using (Pen pen = new Pen(ToColor(c.Colour), c.StartDeg))
        {
            pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
            pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
            g.DrawLine(pen, c.A, c.B, c.C, c.D);
        }
    }

    /// <summary>
    /// The band as one closed polygon: outer edge forward, inner edge back.
    ///
    /// NOT GDI+'s FillPie or AddArc. Those take their own angle convention and their own idea of
    /// where zero is, so using them would put a second, silently different definition of "0 degrees
    /// is twelve o'clock, clockwise" into the project. Building the polygon from ArcGeometry means
    /// the preview and the screen are drawing literally the same points.
    /// </summary>
    private static void FillArcBand(Graphics g, Brush brush, DrawCmd c)
    {
        int n = ArcGeometry.VertexCount(c.EndDeg - c.StartDeg, 2.0);
        if (n < 2) return;

        float[] inner = new float[n * 2];
        float[] outer = new float[n * 2];
        ArcGeometry.ArcScreen(inner, n, c.A, c.B, c.C, c.StartDeg, c.EndDeg);
        ArcGeometry.ArcScreen(outer, n, c.A, c.B, c.D, c.StartDeg, c.EndDeg);

        PointF[] poly = new PointF[n * 2];
        for (int i = 0; i < n; i++)
            poly[i] = new PointF(outer[i * 2], outer[i * 2 + 1]);
        for (int i = 0; i < n; i++)
            poly[n + i] = new PointF(inner[(n - 1 - i) * 2], inner[(n - 1 - i) * 2 + 1]);

        g.FillPolygon(brush, poly);
    }

    /// <summary>
    /// Font family for the preview.
    ///
    /// MUST MATCH THE fontName IN DragonScreen.cfg. The preview exists to answer questions about the
    /// real page, and a preview drawn in a font the game cannot get would answer them wrongly while
    /// looking completely convincing.
    ///
    /// D-DIN was INSTALLED PER-USER on 2026-08-05 (user's call), so both sides can have it. The
    /// family name is the one inside the file, verified rather than assumed: "D-DIN". There is also
    /// "D-DIN Condensed" and "D-DIN Exp" available for where space is tight.
    ///
    /// NOTE, still deliberately not done: PrivateFontCollection COULD load the TTF straight out of
    /// assets/d-din without any install. Using it would let the preview show typography the GAME
    /// cannot get, which is precisely the failure a preview must not have. Both sides resolve the
    /// font the same way - by OS install - so if one loses it, both do, visibly.
    /// </summary>
    private const string FontFamily = "D-DIN";

    /// <summary>
    /// y is the TOP of the line, matching DisplayList.Text and the GL painter.
    /// GDI+ measures and draws from the top-left of the layout box by default, so no baseline maths
    /// is needed here - but the two renderers use different font engines, so vertical placement
    /// agrees closely rather than exactly. That is inside the preview's stated contract.
    /// </summary>
    private static void DrawText(Graphics g, Brush brush, DrawCmd c)
    {
        if (string.IsNullOrEmpty(c.Str) || c.C <= 0f) return;
        using (Font f = new Font(FontFamily, c.C, FontStyle.Regular, GraphicsUnit.Pixel))
        using (StringFormat sf = new StringFormat(StringFormat.GenericTypographic))
        {
            sf.Alignment = (c.Align == TextAlign.Centre) ? StringAlignment.Center
                         : (c.Align == TextAlign.Right) ? StringAlignment.Far
                         : StringAlignment.Near;
            sf.FormatFlags |= StringFormatFlags.NoWrap;
            g.DrawString(c.Str, f, brush, new PointF(c.A, c.B), sf);
        }
    }

    /// <summary>
    /// Bitmaps, loaded from the SAME art folder the game ships - plugin/GameData/DragonScreen/art -
    /// so the preview cannot show a picture the game does not have.
    ///
    /// Cached: this walks the list per page and there are five pages.
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ImageId, Image> imgCache =
        new System.Collections.Generic.Dictionary<ImageId, Image>();

    private static Image LoadImage(ImageId id)
    {
        Image img;
        if (imgCache.TryGetValue(id, out img)) return img;
        img = null;

        // ---- A RUNTIME IMAGE GETS A STAND-IN, AND THAT IS NOT THE USUAL RULE ----
        // Everywhere else the preview refuses to show a picture the game does not have, because a
        // preview that flatters us is worse than none. ImageId.BodyMap is the exception that proves
        // it: the GAME always has one - KSP's scaled-space texture - and only the PREVIEW cannot,
        // because there is no game to ask. Drawing nothing here does not model the game's state, it
        // models a state that never occurs.
        //
        // And it cost a restart. The ORBIT globe's strips only draw when there IS a texture, so the
        // preview rendered the plain base disc, and the square stair-stepping around the limb was
        // invisible until it was on the glass. A render that omits the one thing being tested is not
        // a render of it.
        //
        // The stand-in is an equirectangular Earth from the reference assets, which is the same
        // PROJECTION and the same aspect as any body KSP will hand us - so geometry, seam handling
        // and the fringe trim are all exercised for real. It is read from assets/, never from the
        // shipped art folder, and it is not part of any release.
        if (Images.IsRuntime(id))
        {
            img = LoadStandIn(id);
            imgCache[id] = img;
            return img;
        }

        string file = Images.FileName(id);
        if (!string.IsNullOrEmpty(file))
        {
            string dir = Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location);
            // build/ -> plugin/ -> GameData/DragonScreen/art
            string path = Path.GetFullPath(Path.Combine(dir,
                "..", "GameData", "DragonScreen", "art", file));
            if (File.Exists(path))
            {
                try { img = Image.FromFile(path); }
                catch (Exception e) { Console.WriteLine("  image " + file + ": " + e.Message); }
            }
            else Console.WriteLine("  MISSING art " + path);
        }
        imgCache[id] = img;
        return img;
    }

    /// <summary>
    /// An equirectangular body map for the preview only. See LoadImage for why this one asset gets a
    /// stand-in when nothing else does.
    ///
    /// Absence is not an error: if the reference assets are not checked out, the preview falls back
    /// to the plain base disc exactly as it used to, and says so once rather than pretending.
    /// </summary>
    private static Image LoadStandIn(ImageId id)
    {
        string dir = Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location);

        // ---- THE NAVBALL'S STAND-IN IS ITS OWN SKIN ----
        // A shared stand-in would have drawn EARTH as the attitude ball, which is worse than drawing
        // nothing: it looks deliberate. The ball's own equirectangular texture is the honest choice,
        // and DrawImage clips it to a circle so the preview answers the only question it can - does a
        // sphere of this size sit correctly inside the rings.
        //
        // What the preview CANNOT show is the ball's orientation: that is a Unity camera rendering a
        // real mesh, and there is no camera here. Attitude is a question for the game.
        // ---- THE DOCKING CAMERA GETS NO STAND-IN ----
        // It is a view of whatever is out there; there is no honest still to put in its place, and
        // falling through to the body map drew EARTH behind the docking HUD - a picture that looks
        // deliberate and is pure fiction. The page is designed to work without it, so the preview
        // shows the dark background it will have when nothing is in view.
        if (id == ImageId.DockingCamLive) return null;

        // ---- AND NEITHER DOES THE LIVE 3D PLANET (S10a) ----
        // Exactly the same trap, one step worse: the shared stand-in below IS an equirectangular
        // Earth, so falling through would put a real-looking planet in the frame and let the preview
        // claim the scaled-space camera renders when there is no camera in the build at all (it is
        // S10b). The page never asks for this image unless PageState.PlanetCamLive is set, which
        // nothing here sets - this is the belt to that braces, so a forced preview state cannot
        // manufacture a render that does not exist. The honest picture is the textured disc under
        // PlanetGeom.NoSignalLabel, which is what NAV/PLANET draws.
        if (id == ImageId.ScaledPlanetLive) return null;

        if (id == ImageId.NavBallLive)
        {
            string skin = Path.GetFullPath(Path.Combine(dir, "..",
                "GameData", "DragonScreen", "art", "navball.png"));
            if (!File.Exists(skin)) return null;
            try { return Image.FromFile(skin); }
            catch (Exception e) { Console.WriteLine("  navball stand-in: " + e.Message); return null; }
        }

        // build/ -> plugin/ -> project root
        string path = Path.GetFullPath(Path.Combine(dir, "..", "..",
            "assets", "reference", "dragon2-ui-assets", "docs", "img",
            "earth_atmos_2048.e15eb8d2.jpg"));
        if (!File.Exists(path))
        {
            Console.WriteLine("  (no body-map stand-in at " + path + " - globe previews bare)");
            return null;
        }
        try { return Image.FromFile(path); }
        catch (Exception e)
        {
            Console.WriteLine("  body-map stand-in: " + e.Message);
            return null;
        }
    }

    // Cover-page PNG assets (art/cover/<key>.png), placed at their measured Figma positions.
    private static readonly System.Collections.Generic.Dictionary<string, Image> coverCache =
        new System.Collections.Generic.Dictionary<string, Image>();

    private static void DrawCoverAsset(Graphics g, DrawCmd c)
    {
        Image img;
        if (!coverCache.TryGetValue(c.AssetKey, out img))
        {
            string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string path = Path.GetFullPath(Path.Combine(dir, "..", "GameData", "DragonScreen", "art", "cover", c.AssetKey + ".png"));
            img = File.Exists(path) ? Image.FromFile(path) : null;
            if (img == null) Console.WriteLine("  MISSING cover asset " + c.AssetKey);
            coverCache[c.AssetKey] = img;
        }
        if (img == null) return;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(img, new RectangleF(c.A, c.B, c.C, c.D),
                    new RectangleF(0f, 0f, img.Width, img.Height), GraphicsUnit.Pixel);
    }

    private static void DrawImage(Graphics g, DrawCmd c)
    {
        if (c.AssetKey != null) { DrawCoverAsset(g, c); return; }
        Image img = LoadImage(c.Image);
        // Skipped, not substituted - same rule as the GL painter. A placeholder rectangle would put
        // a shape on the page that nothing asked for.
        if (img == null) return;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // SOURCE RECT, in pixels. GDI+ has v = 0 at the TOP of the image, the opposite of texture
        // space, so VMax - the north edge in MapProjection's convention - is the source rect's TOP.
        // Getting this backwards would flip the map in the preview only, which is the worst place for
        // a discrepancy to hide because the preview is what layout is judged from.
        //
        // ---- REVERSED-U IS A HORIZONTAL FLIP, NOT AN EMPTY DRAW ----
        // NavPage swaps u (left edge = the LARGER u) to un-mirror KSP's _ColorMap - the flat map has
        // always done it, the globe now does too (Campaign 4). That makes UMin > UMax, so a source
        // rect of width (UMax-UMin) is NEGATIVE and GDI+ draws nothing. The GL painter handles the
        // reversal (it is why the map reads correctly IN GAME); the preview must too, or it silently
        // omits the one texture it exists to show. Normalise the source to a positive window and, when
        // u was reversed, mirror the DESTINATION about its own centre so the flip renders for real.
        bool mirrorU = c.UMin > c.UMax;
        float uLo = mirrorU ? c.UMax : c.UMin;
        float uHi = mirrorU ? c.UMin : c.UMax;
        float sx = uLo * img.Width;
        float sw = (uHi - uLo) * img.Width;
        float sy = (1f - c.VMax) * img.Height;
        float sh = (c.VMax - c.VMin) * img.Height;
        if (sw <= 0f || sh <= 0f) return;

        // The navball is a SPHERE in game. Its stand-in here is the flat skin, so it is clipped to a
        // circle - otherwise the DOCKING preview would show a square where a ball goes, and someone
        // would eventually "fix" a layout that was never wrong. Preview-only: the game draws a real
        // mesh and needs no mask.
        System.Drawing.Drawing2D.GraphicsState st = null;
        if (c.Image == ImageId.NavBallLive || c.CircleClip)
        {
            st = g.Save();
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddEllipse(c.A, c.B, c.C, c.D);
                g.SetClip(path);
            }
        }

        // Mirror the destination about its own centre for a reversed-u window, so the region draws
        // horizontally flipped (matching the GL painter) while staying in the same screen rectangle.
        System.Drawing.Drawing2D.GraphicsState stFlip = null;
        if (mirrorU)
        {
            stFlip = g.Save();
            float cxd = c.A + c.C * 0.5f;
            g.TranslateTransform(cxd, 0f);
            g.ScaleTransform(-1f, 1f);
            g.TranslateTransform(-cxd, 0f);
        }

        g.DrawImage(img, new RectangleF(c.A, c.B, c.C, c.D),
                    new RectangleF(sx, sy, sw, sh), GraphicsUnit.Pixel);

        if (stFlip != null) g.Restore(stFlip);
        if (st != null) g.Restore(st);
    }

    private static Color ToColor(Rgba c)
    {
        return Color.FromArgb(Clamp8(c.A), Clamp8(c.R), Clamp8(c.G), Clamp8(c.B));
    }

    private static int Clamp8(float v)
    {
        int i = (int)Math.Round(v * 255.0);
        return (i < 0) ? 0 : (i > 255) ? 255 : i;
    }
}
