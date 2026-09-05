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
    private struct ScreenSpec { public int Index, W, H; }

    // ---- THE MESH ASPECT, MEASURED ---------------------------------------------------------
    // Measured from the screen meshes in game, 2026-08-05, and logged by DragonScreenMonitor.
    // Hard-coded here because the game is not running; if the model ever changes, the log is the
    // source of truth and these follow it.
    //
    // THESE ARE AN ASPECT, NOT A RESOLUTION. The number of PIXELS the game renders is `screenWidth`
    // in DragonScreen.cfg, and the cfg's own note says the HEIGHT is derived from the mesh - so the
    // mesh fixes the shape and the cfg fixes the size. `Screens` below applies the cfg to this
    // shape. Nothing in this file may name a render size that did not come through there.
    private static readonly ScreenSpec[] MeasuredScreens = {
        new ScreenSpec { Index = 1, W = 1280, H = 703 },   // left
        new ScreenSpec { Index = 2, W = 1280, H = 710 },   // centre - NOT the same shape
        new ScreenSpec { Index = 3, W = 1280, H = 703 }    // right
    };

    /// <summary>
    /// The sizes the preview actually renders at: the MEASURED aspect above, scaled to the width the
    /// shipped cfg asks for. Read from plugin/GameData/DragonScreen/DragonScreen.cfg at startup.
    ///
    /// ---- WHY THIS IS DERIVED AND NOT DECLARED (S100 / QC H-01) ----
    /// This file used to render every Figma-era page at `W * 2` - 2560x1406 - justified by a comment
    /// that said "the in-game RenderTexture should match - screenWidth 2560 in the cfg". The cfg said
    /// 1280, on all three screens, and had said so the whole time. So the project's own legibility
    /// gate - CLAUDE.md: "judge layout/palette/legibility from `python plugin/build.py preview`" -
    /// was judging at FOUR TIMES the pixel count the mod ships, on the strength of a number that did
    /// not exist in the file it named.
    ///
    /// The rule this file already states for the font is the same rule, and it was never written
    /// down for resolution: "If this and PreviewMain.FontFamily ever disagree, the preview is lying
    /// about the real page." A comment cannot enforce it. Derivation can: there is now no expression
    /// in this file that can produce a render size the cfg did not ask for.
    ///
    /// plugin/test/ScreenSizeTest.cs is the second half of the guard - it reads the cfg AND this
    /// source and fails the build if a doubling is reintroduced or the derivation is bypassed.
    /// </summary>
    private static readonly ScreenSpec[] Screens = DeriveScreens();

    /// <summary>The cfg the derivation reads. build/ -> plugin/ -> GameData/DragonScreen.</summary>
    private static string CfgPath()
    {
        return Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", "GameData", "DragonScreen", "DragonScreen.cfg"));
    }

    /// <summary>
    /// `screenWidth` out of the shipped cfg, and it is FATAL if it cannot be had.
    ///
    /// Not "fall back to 1280": a silent fallback is how the old comment survived - it looked right
    /// and agreed with nothing. If the cfg is missing, unparseable, or disagrees with itself across
    /// the three screens, the preview must not render at all, because whatever it rendered would be
    /// a size no source asked for. C7.1: the repo copy is authoritative.
    /// </summary>
    private static int CfgScreenWidth()
    {
        string path = CfgPath();
        if (!File.Exists(path))
            throw new InvalidOperationException(
                "PREVIEW CANNOT DERIVE ITS RENDER SIZE: no cfg at " + path);

        var widths = new System.Collections.Generic.List<int>();
        foreach (string raw in File.ReadAllLines(path))
        {
            // `screenWidth = 1280`, with any comment tail (// ...) dropped first.
            string line = raw;
            int c = line.IndexOf("//", StringComparison.Ordinal);
            if (c >= 0) line = line.Substring(0, c);
            int eq = line.IndexOf('=');
            if (eq < 0) continue;
            if (line.Substring(0, eq).Trim() != "screenWidth") continue;
            int v;
            if (!int.TryParse(line.Substring(eq + 1).Trim(), out v))
                throw new InvalidOperationException(
                    "PREVIEW CANNOT DERIVE ITS RENDER SIZE: unparseable screenWidth in " + path
                    + ": " + raw.Trim());
            widths.Add(v);
        }

        if (widths.Count == 0)
            throw new InvalidOperationException(
                "PREVIEW CANNOT DERIVE ITS RENDER SIZE: no screenWidth in " + path);
        for (int i = 1; i < widths.Count; i++)
            if (widths[i] != widths[0])
                throw new InvalidOperationException(
                    "PREVIEW CANNOT DERIVE ITS RENDER SIZE: the cfg's " + widths.Count
                    + " screenWidth values disagree in " + path
                    + ". The preview renders ONE size; the shipped screens must agree on it.");
        if (widths[0] < 16)
            throw new InvalidOperationException(
                "PREVIEW CANNOT DERIVE ITS RENDER SIZE: screenWidth " + widths[0] + " is below the "
                + "glue's own floor of 16 (DragonScreenMonitor.cs:345) in " + path);
        return widths[0];
    }

    private static ScreenSpec[] DeriveScreens()
    {
        int w = CfgScreenWidth();
        ScreenSpec[] outp = new ScreenSpec[MeasuredScreens.Length];
        for (int i = 0; i < MeasuredScreens.Length; i++)
        {
            ScreenSpec m = MeasuredScreens[i];
            // Height follows the MESH aspect, exactly as the cfg's own note says the game derives it.
            // At the shipped 1280 this reproduces the measured heights to the pixel.
            outp[i] = new ScreenSpec {
                Index = m.Index, W = w,
                H = (int)Math.Round(w * (double)m.H / m.W)
            };
        }
        return outp;
    }

    /// <summary>Frozen sweep position. A fixed value so two runs can be compared byte for byte.</summary>
    private const double Phase = 0.62;

    /// <summary>
    /// ONE ORBIT, and every description of the vehicle derived from it (S100, from QC finding C-10).
    ///
    /// ---- WHAT WAS WRONG ----
    /// The Cover fixture described the same vehicle three incompatible ways in one frame: the scalar
    /// readouts said inclination 0.13 deg and an altitude implying orbit ratio 1.206, the overlay
    /// said 51.6 deg at ratio 1.06, and the vessel marker sat at lat 0 lon 0 while its own ground
    /// track was built around lon -80.6. On the flat map the green vessel cross sat at the centre
    /// while its track passed ten degrees of latitude above it; on the globe the AP/PE markers sat
    /// just outside the disc rather than at the radius the ALTITUDE readout implies.
    ///
    /// Nobody chose that. The Cover reuses the NAV/PLANET overlay wholesale and inherited the lat/lon
    /// reset that came with it. But CLAUDE.md makes the preview the gate that saves restarts, and a
    /// fixture whose elements cannot disagree with each other cannot catch a page that draws them
    /// wrong - the marker could be plotted with a sign error and this render would look the same.
    ///
    /// ---- WHAT IS DERIVED HERE ----
    /// Apogee and perigee are the INPUTS (they are the numbers the page's own readouts quote); the
    /// orbit is built from them, and the current altitude, inclination, latitude, longitude, the
    /// overlay path, the AP/PE/vessel markers and the ground track all fall out of it. So the
    /// verification C-10 asks for is now meaningful: the vessel marker must sit ON its own ground
    /// track, and the AP/PE markers must sit at the radius the ALTITUDE readout implies.
    ///
    /// The 51.6-degree inclination is kept, per C-10 - it is the shape that makes a wrong projection
    /// and the far-side occlusion obvious, and it is the real Dragon inclination the baked strip
    /// prints. It was the SCALARS that were changed to match it, not the other way round.
    /// </summary>
    /// ⚠ `ref` IS LOAD-BEARING: PageState is a STRUCT (Pages.cs:53), so taking it by value here
    /// would silently discard every assignment below - the track would stay empty, the readouts would
    /// fall back to dashes, and the render would look plausible while describing nothing.
    private static void BuildOrbitFixture(ref PageState ps)
    {
        const double D2R = Math.PI / 180.0;
        const double inc = 51.6;                  // kept: see the header
        double bodyR = ps.BodyRadiusM;

        // Ap/Pe are the inputs. The orbit's radius is taken to vary sinusoidally between them, with
        // apogee a quarter-turn on and perigee three-quarters - which is where the markers were
        // already placed, so the shape the projection is exercised against does not change.
        double ratioAp = (bodyR + ps.ApogeeM) / bodyR;
        double ratioPe = (bodyR + ps.PerigeeM) / bodyR;
        double rMean = 0.5 * (ratioAp + ratioPe), rAmp = 0.5 * (ratioAp - ratioPe);

        // Where the vehicle is on that orbit. A mid-latitude on the ascending leg: clear of both
        // poles, so a wrong projection shows, and clear of the marker's own AP/PE neighbours.
        const double thNow = 40.0 * D2R;
        // Longitude of the ascending node - the pad, near enough, which is what the track was always
        // built around and what the LATITUDE / LONGITUDE readouts used to quote on their own.
        const double lonAn = -80.6;
        // How far the body turns under the vehicle in one revolution. The old track marched west by
        // this much; it is kept so the track still crosses the seam, which is what it is for.
        const double drift = 22.0;

        double latAt, lonAt;
        OrbitPoint(inc, lonAn, thNow, out latAt, out lonAt);

        // ---- the scalars, DERIVED ----
        double ratioNow = rMean + rAmp * Math.Sin(thNow);
        ps.InclinationDeg = inc;
        ps.InclinationText = inc.ToString("F2") + " deg";
        ps.InclinationDegText = inc.ToString("F2") + "\u00b0";
        ps.AltitudeM = (ratioNow - 1.0) * bodyR;
        ps.Altitude = (ps.AltitudeM / 1000.0).ToString("F1") + " km";
        ps.Latitude = latAt; ps.Longitude = lonAt;
        ps.LatText = Math.Abs(latAt).ToString("F2") + (latAt >= 0 ? " N" : " S");
        ps.LonText = Math.Abs(lonAt).ToString("F2") + (lonAt >= 0 ? " E" : " W");

        // ---- the ground track: one revolution of history, ENDING where the vehicle is now ----
        // The last sample is the vessel's own position, which is what makes "the marker sits on its
        // own track" a real check rather than a coincidence.
        int track = 90;
        ps.TrackLat = new double[track];
        ps.TrackLon = new double[track];
        for (int i = 0; i < track; i++)
        {
            double frac = i / (double)(track - 1);
            double th = thNow - (1.0 - frac) * 2.0 * Math.PI;
            double la, lo;
            OrbitPoint(inc, lonAn, th, out la, out lo);
            // Older samples were laid down further east, before the body turned; at frac = 1 the
            // correction is zero, so the track ends exactly on the vessel.
            ps.TrackLat[i] = la;
            ps.TrackLon[i] = MapProjection.Wrap180(lo + (1.0 - frac) * drift);
        }
        ps.TrackCount = track;

        // ---- the overlay: the same orbit, drawn whole ----
        PlanetOverlay ov = new PlanetOverlay();
        int N = PlanetOverlay.DefaultSamples;
        double[] olat = new double[N], olon = new double[N], orat = new double[N];
        for (int i = 0; i < N; i++)
        {
            double th = (2.0 * Math.PI * i) / (N - 1);
            OrbitPoint(inc, lonAn, th, out olat[i], out olon[i]);
            orat[i] = rMean + rAmp * Math.Sin(th);
        }
        ov.Ready = true;
        ov.OrbitLat = olat; ov.OrbitLon = olon; ov.OrbitRatio = orat; ov.OrbitCount = N;
        ov.Vessel = new GlobePoint { Lat = latAt, Lon = lonAt, Ratio = ratioNow, Has = true };
        ov.Ap = new GlobePoint { Lat = olat[N / 4], Lon = olon[N / 4], Ratio = ratioAp, Has = true };
        ov.Pe = new GlobePoint { Lat = olat[3 * N / 4], Lon = olon[3 * N / 4], Ratio = ratioPe, Has = true };
        ps.Planet = ov;
    }

    /// <summary>Sub-satellite point at argument-of-latitude th, for an orbit of this inclination
    /// whose ascending node is at lonAn. Degrees out, radians in.</summary>
    private static void OrbitPoint(double incDeg, double lonAnDeg, double th,
                                   out double latDeg, out double lonDeg)
    {
        const double D2R = Math.PI / 180.0, R2D = 180.0 / Math.PI;
        double inc = incDeg * D2R;
        latDeg = Math.Asin(Math.Sin(inc) * Math.Sin(th)) * R2D;
        lonDeg = MapProjection.Wrap180(
            lonAnDeg + Math.Atan2(Math.Cos(inc) * Math.Sin(th), Math.Cos(th)) * R2D);
    }

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
        ps.VelocityMps = 2280.0;      // InclinationDeg is set by BuildOrbitFixture (C-10)
        ps.BodyRadiusM = 600000.0; ps.AtmosphereDepthM = 70000.0;
        ps.CircularSpeedMps = 2426.0;      // sqrt(mu/R) for Kerbin
        ps.Ascending = true;
        // InclinationText / InclinationDegText are set by BuildOrbitFixture (C-10), off the same
        // inclination the overlay is drawn from. They used to say 0.13 deg beside a 51.6 deg overlay.
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
        // The ground track, the orbit overlay, the vessel marker and the scalar readouts all come
        // out of BuildOrbitFixture below, off ONE orbit. See its header for why (QC finding C-10).
        ps.HasFix = true;
        BuildOrbitFixture(ref ps);
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

            // ---- AND THE SAME SCENE WITH THE ZOOM S43 WIRED ----
            // This is the render S43 was logged FROM - the arc, the AP box, the AP label and the
            // vehicle tick all inside an 8 px band on the limb. At x4 the open arc separates from the
            // limb the way the closed ring does, the surface cut is still visible as the arc running
            // into the limb, and the caption still says why the line stops.
            MapView orbitZ = orbit;
            for (int k = 0; k < 2; k++) orbitZ = MapProjection.Zoom(orbitZ, 1);
            dl.Clear();
            Pages.Build(dl, 2, W, H, sub, orbitZ, 2);
            ChromeBar.Build(dl, W, H, cs);
            string pathZ = Path.Combine(outDir, "page2_nav_orbit_suborbital_x4.png");
            Render(dl, W, H, pathZ);
            Console.WriteLine("  " + pathZ + "   " + W + "x" + H + "   " + dl.Count + " commands");
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

        // ---- NAV / ORBIT AT THE 1:1 SCALE, x1 AND x4: WHAT S43 IS ABOUT (S43) ----
        // S43's headline case, and it is not previewed anywhere else: a CLOSED, near-circular 200 km
        // LEO over a 6371 km body. The ring is 1.0314 body radii, so on a 236 px globe it sits 7.4 px
        // off the limb with a 10 px apsis box centred on it - the box straddles the limb on both
        // sides. That is the truth, and it is unreadable, which is the whole of the line.
        //
        // The pair below is the owner's ruling rendered: x1 is what the page opens at, x4 is the same
        // geometry with the zoom the crew now has. Nothing between them changed but the viewport -
        // same conic, same apsis order, same surface rule - and the x4 render is what "LEGIBLE WHEN
        // ZOOMED" means, which is the DONE-when the ruling put in place of the old one.
        //
        // They are ALSO the two answers to S43's open question (does the page open at x1 or at a
        // default zoom?), which is why both are rendered rather than just the zoomed one.
        {
            PageState leo = ps;                       // PageState is a struct: this is a copy
            leo.Body = "EARTH";
            leo.Regime = FlightRegime.Space;
            leo.BodyRadiusM = 6371000.0; leo.AtmosphereDepthM = 140000.0;
            leo.ApogeeM = 202000.0; leo.PerigeeM = 198000.0;
            leo.AltitudeM = 200000.0; leo.Altitude = "200.0 km";
            leo.Apoapsis = "202.0 km"; leo.Periapsis = "198.0 km";
            leo.ApogeeShown = true; leo.PerigeeShown = true;
            leo.Ascending = true;
            leo.TimeToApText = "00:21:07"; leo.TimeToPeText = "00:65:41"; leo.PeriodText = "01:28:16";
            leo.HasTarget = false; leo.HasTargetOrbit = false;

            // x1, x4, and x4 panned left+up - the last one because a zoomed plot overflows its well
            // and the mask that paints the overflow out has to be seen working, not just asserted.
            MapView baseView = MapProjection.NextMode(MapProjection.Default());
            MapView[] views = new MapView[4];
            string[] names = new string[4];

            views[0] = baseView;                            names[0] = "leo_x1";
            MapView z4 = baseView;
            for (int k = 0; k < 2; k++) z4 = MapProjection.Zoom(z4, 1);
            views[1] = z4;                                  names[1] = "leo_x4";
            MapView z4p = z4;
            for (int k = 0; k < 3; k++) z4p = MapProjection.Pan(z4p, -1.0, 0.0);
            for (int k = 0; k < 2; k++) z4p = MapProjection.Pan(z4p, 0.0, 1.0);
            views[2] = z4p;                                 names[2] = "leo_x4_pan";
            // The clamp's own render: press + far more times than the range allows and land on x8,
            // which is what "zoom must not run away to a degenerate view" has to be looked at.
            MapView z8 = baseView;
            for (int k = 0; k < 9; k++) z8 = MapProjection.Zoom(z8, 1);
            views[3] = z8;                                  names[3] = "leo_x8";

            for (int v = 0; v < views.Length; v++)
            {
                dl.Clear();
                Pages.Build(dl, 2, W, H, leo, views[v], 2);
                ChromeState cs = new ChromeState();
                cs.Met = "T+ 01:12:04"; cs.VehicleState = "NOMINAL";
                cs.LinkName = "COM1/TLM"; cs.LinkTimer = "00:04:12"; cs.LinkUp = true;
                cs.SelectedPage = 2;
                ChromeBar.Build(dl, W, H, cs);

                if (dl.Overflowed)
                    Console.WriteLine("  WARNING page NAV/ORBIT " + names[v]
                                      + " OVERFLOWED at " + dl.Capacity);

                string p2 = Path.Combine(outDir, "page2_nav_orbit_" + names[v] + ".png");
                Render(dl, W, H, p2);
                Console.WriteLine("  " + p2 + "   " + W + "x" + H + "   " + dl.Count + " commands");
            }
        }

        // ---- THE SAME ZOOM AT KERBIN SCALE, WHERE NOTHING NEEDED FIXING (S43) ----
        // The x1 Kerbin render is byte-identical to the pre-S43 one and is above; this is the other
        // half of "the Kerbin case is unchanged" - that the control the RSS case needed does something
        // sane here too rather than tearing a picture that was already correct.
        {
            MapView z4 = MapProjection.NextMode(MapProjection.Default());
            for (int k = 0; k < 2; k++) z4 = MapProjection.Zoom(z4, 1);

            dl.Clear();
            Pages.Build(dl, 2, W, H, ps, z4, 2);
            ChromeState cs = new ChromeState();
            cs.Met = "T+ 00:12:34"; cs.VehicleState = "NOMINAL";
            cs.LinkName = "COM1/TLM"; cs.LinkTimer = "00:04:12"; cs.LinkUp = true;
            cs.SelectedPage = 2;
            ChromeBar.Build(dl, W, H, cs);

            if (dl.Overflowed)
                Console.WriteLine("  WARNING page NAV/ORBIT KERBIN x4 OVERFLOWED at " + dl.Capacity);

            string path = Path.Combine(outDir, "page2_nav_orbit_kerbin_x4.png");
            Render(dl, W, H, path);
            Console.WriteLine("  " + path + "   " + W + "x" + H + "   " + dl.Count + " commands");
        }

        // NAV / PLANET: the 3D globe view. The globe itself is the same textured disc the ORBIT view
        // draws (from the body-map stand-in here), and the orbit overlay is pure 2D projection, so
        // unlike a camera view the preview shows the WHOLE thing - which is exactly what makes the
        // orbit-on-globe projection and its occlusion cheap to iterate.
        {
            MapView planet = MapProjection.NextMode(MapProjection.NextMode(MapProjection.Default()));

            // ---- THE OVERLAY IS THE FIXTURE ORBIT, NOT A SECOND ONE (S100, QC finding C-10) ----
            // This block used to build its own synthetic 51.6-degree orbit at ratio 1.06 and then
            // write `ps.HasFix = true; ps.Latitude = 0.0; ps.Longitude = 0.0;` - which silently
            // overwrote the pad coordinates the ground track had been built around. Each half was a
            // reasonable local choice; together they meant the page described three different
            // vehicles at once, and the one question a preview of this page can answer - do the
            // markers, the track and the readouts agree? - could not be asked. A fixture that cannot
            // fail is not a gate.
            //
            // The orbit is now built ONCE, in BuildOrbitFixture, and this view just draws it. The
            // 51.6-degree inclination is kept deliberately: it is the shape that makes a wrong
            // projection and the far-side occlusion obvious, which is what this render is for.
            PlanetOverlay ov = ps.Planet;

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
            // ---- THE SHIPPED SIZE, AND WHY THERE IS NO LONGER A CHOICE HERE (S100 / QC H-01) ----
            // This line read `int CW = W * 2, CH = H * 2;` and was justified as: "Render at 2x the
            // screen size: the Figma assets carry 2px hairline borders that fall to sub-pixel
            // (~0.7px) at 1280 and drop inconsistently; 2x keeps them crisp (the in-game
            // RenderTexture should match - screenWidth 2560 in the cfg)."
            //
            // The cfg says 1280, three times (DragonScreen.cfg:60, :76, :87), and so does the glue's
            // own default (DragonScreenMonitor.cs:52). So the gate was judging every Figma page at
            // four times the shipped pixel count.
            //
            // THE HAIRLINE SENTENCE IS A REAL DEFECT REPORT, AND IT STANDS. At the shipped width the
            // design's 2px hairlines DO fall to ~0.7px and drop inconsistently. Rendering large did
            // not fix that; it hid it. It is now visible in these PNGs because they are finally the
            // size the crew gets, and it is logged as its own finding (REGISTER S101) for a screen
            // batch to fix in the art and the page code. Do NOT re-enlarge, round, or fudge this to
            // make the hairlines come back - that only re-hides the defect.
            int CW = W, CH = H;
            DisplayList cdl = new DisplayList(CoverPage.Commands + 400);
            CoverPage.Build(cdl, CW, CH, ps, planet);
            if (cdl.Overflowed) Console.WriteLine("  WARNING COVER OVERFLOWED at " + cdl.Capacity);
            string path = Path.Combine(outDir, "cover.png");
            Render(cdl, CW, CH, path);
            Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + cdl.Count + " commands");
        }

        // ---- SETTINGS / AUDIO (Figma rebuild, A-Settings) — Cabin selected, 2x render ----
        {
            int CW = W, CH = H;   // the shipped size - see the COVER block above (S100 / QC H-01)
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
            int CW = W, CH = H;   // the shipped size - see the COVER block above (S100 / QC H-01)
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
            int CW = W, CH = H;   // the shipped size - see the COVER block above (S100 / QC H-01)
            bool savedNose = ps.Steps.NoseConeOpen;
            // ---- THREE STATES, NOT TWO (S100, from QC finding H-09) ----
            // Nose CLOSED; nose OPEN with the bore-sight card, which is the shape the GAME always
            // has and only the preview cannot; and nose OPEN with NO feed, which is the real in-game
            // look when nothing is in view and which the GL painter agrees with today. All three
            // must stay visibly distinct - the middle one is the page ONE live feature and until now
            // it had never been rendered at all.
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
            {
                ps.Steps.NoseConeOpen = true;
                DockingCamStandIn = true;
                ForgetRuntimeImages();
                DisplayList hdl = new DisplayList(Frame58Hud.Commands + 60);
                Frame58Hud.Build(hdl, CW, CH, ps);
                if (hdl.Overflowed) Console.WriteLine("  WARNING FRAME58_HUD CAM OVERFLOWED at " + hdl.Capacity);
                string path = Path.Combine(outDir, "frame58_hud_noseopen_camfeed.png");
                Render(hdl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + hdl.Count + " commands");
                DockingCamStandIn = false;
                ForgetRuntimeImages();
            }
            ps.Steps.NoseConeOpen = savedNose;
        }

        // ---- Figma UI navigation: dispatcher + placeholder + shared back chevron ----
        {
            int CW = W, CH = H;   // the shipped size - see the COVER block above (S100 / QC H-01)
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
            // ---- THE VIDEO PAGE POPULATED (S100, from QC finding VV-02) ----
            // SettingsVideoPage reads s.CamLabels and highlights s.CameraView - a genuinely live list
            // off a real vessel scan, and the only live thing on the page. The fixture never set
            // CamLabels, so the ONE render of this page was its empty state and the list, the
            // selection highlight and the FORWARD VIEW IN USE BY DOCKING branch had never been drawn
            // at all. That is H-09 defect on a second page: the gate could not see the page live half.
            //
            // The empty state keeps its render (the loop above still draws it with CamLabels unset),
            // because "no cameras on vehicle" is a real in-game state and must stay checkable.
            //
            // ⚠ THIS IS THE FIXTURE HALF ONLY. VV-02 second half - re-homing the stranded writer so a
            // camera row can actually be TAPPED - is a change to SettingsVideoPage and FigmaUI.HitTest,
            // which this instrument task does not touch. The row still draws a selection the crew
            // cannot move; that stays open, and S49 H12 groups it with the other stranded settings
            // handlers to be done as one job.
            {
                string[] savedCams = ps.CamLabels;
                int savedView = ps.CameraView;
                bool savedHeld = ps.CameraHeldByDocking;
                // Names in the shape a real vessel scan produces, not invented prettiness.
                ps.CamLabels = new string[] { "FORWARD", "DOCKING PORT", "TRUNK", "CUPOLA" };
                ps.CameraView = 1;
                ps.CameraHeldByDocking = false;
                DisplayList vdl = new DisplayList(600);
                FigmaUI.Build(vdl, UiPage.AudioVideo, CW, CH, ps, MapProjection.Default());
                if (vdl.Overflowed) Console.WriteLine("  WARNING UI AudioVideo populated OVERFLOWED");
                string path = Path.Combine(outDir, "ui_audiovideo_cameras.png");
                Render(vdl, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + vdl.Count + " commands");

                // ...and the branch where docking owns the forward feed, which must take precedence
                // over the selection above rather than merely sit beside it.
                ps.CameraHeldByDocking = true;
                DisplayList hdl2 = new DisplayList(600);
                FigmaUI.Build(hdl2, UiPage.AudioVideo, CW, CH, ps, MapProjection.Default());
                if (hdl2.Overflowed) Console.WriteLine("  WARNING UI AudioVideo held OVERFLOWED");
                path = Path.Combine(outDir, "ui_audiovideo_cameras_heldbydocking.png");
                Render(hdl2, CW, CH, path);
                Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + hdl2.Count + " commands");

                ps.CamLabels = savedCams; ps.CameraView = savedView;
                ps.CameraHeldByDocking = savedHeld;
            }
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

            // ---- S113 / QC SP-01: THE P&ID'S NON-NOMINAL STATES, WHICH HAD NEVER BEEN DRAWN ----
            // The loop above renders `ui_systemspid.png` once, from the all-nominal fixture. Everything
            // S56 built into this page is COLOUR that only appears when something is wrong - the vent
            // path, the CABIN LEAK and FIRE words, the OVERBOARD/ISOLATION state, the fan/pump tints and
            // the per-loop severity bands - so on that one render they all resolve to the same nominal
            // colour and the gate could not see any of it.
            //
            // ⚠ THIS IS THE THIRD PAGE WITH THE SAME GAP, and the sweep should stop finding it: H-09 was
            // the HUD's docking-cam disc, VV-02 the Video page's camera list, this the P&ID. The rule
            // T4/T5 set - "anything reachable needs a render" - applies to a STATE as much as to a
            // control, and a page whose entire value is live colouring needs at least one render where
            // the colour is doing something, or the gate proves nothing about it.
            //
            // ⛔ Fixture edits only. No page source is touched here, and the nominal render above is
            // untouched, because it is the baseline every one of these is a comparison against.
            {
                SystemsState savedSys = ps.Systems;
                double savedA = ps.Cabin.LoopAC, savedB = ps.Cabin.LoopBC;

                // key | what it exercises on the page
                string[] key = { "leak", "fire", "pumpson", "hotloop" };
                for (int i = 0; i < key.Length; i++)
                {
                    ps.Systems = savedSys;
                    ps.Cabin.LoopAC = savedA; ps.Cabin.LoopBC = savedB;
                    // ⚠ Leaking / Fire / FanOn / PumpAOn / PumpBOn are COMPUTED PROPERTIES, not fields -
                    // `Fire` is `FireIntensity > 0.02`, `Leaking` is `LeakRate > 0.001`, and the pumps are
                    // `OnlineCount(bus) > 0`. So the fixture drives the MODEL and lets the page's own
                    // predicates fire, rather than forcing a display flag: "simulate, never fake" applies
                    // to a preview fixture as much as to a screen.
                    switch (i)
                    {
                        // Leaking: the vent path takes `ventCol` instead of `Pipe`, and CABIN LEAK lights.
                        case 0: ps.Systems.LeakRate = 0.05; break;
                        // Fire: the FIRE word, the one state on this page a crew would act on first.
                        case 1: ps.Systems.FireIntensity = 0.4; break;
                        // ⚠ PUMPS AND FAN **ON**, AND THE DIRECTION MATTERS - SP-01 ASSUMED THE WRONG ONE.
                        // The finding asked for "a pump/fan off vs on" render on the reading that the
                        // baseline is all-nominal. It is not: the shared fixture is
                        // `SystemsState.Fresh()` (`:316`), which ships BOTH BUSES OFF, so `OnlineCount`
                        // returns 0 and the one existing render has ALWAYS shown the fan and both pumps
                        // OFF. Rendering them off again produced a **0-pixel** difference, which is how
                        // this was caught. The state that had never been drawn is the powered one.
                        case 2: ps.Systems.Bus1On = true; ps.Systems.Bus2On = true; break;
                        // Loop A over CabinLimits.LoopAlarm: the per-loop severity band at :208-210,
                        // computed by Alarms.Band, which the nominal render can only ever show green.
                        case 3: ps.Cabin.LoopAC = CabinLimits.LoopAlarm + 5.0; break;
                    }
                    DisplayList sdl = new DisplayList(600);
                    FigmaUI.Build(sdl, UiPage.SystemsPid, CW, CH, ps, MapProjection.Default());
                    if (sdl.Overflowed) Console.WriteLine("  WARNING P&ID " + key[i] + " OVERFLOWED");
                    string path = Path.Combine(outDir, "ui_systemspid_" + key[i] + ".png");
                    Render(sdl, CW, CH, path);
                    Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + sdl.Count + " commands");
                }
                ps.Systems = savedSys;
                ps.Cabin.LoopAC = savedA; ps.Cabin.LoopBC = savedB;
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

            // ---- THE SAME TWO VIEWS AGAINST A MIRRORED TEXTURE (S100, from QC finding C-09) ----
            // The globe does not swap u and the flat map does, and PageTest.NavTexture pins both as
            // correct - which can only be true if they read the same way on one texture. On the
            // unmirrored stand-in they do not. These two extra renders put each view against the
            // OPPOSITE handedness, so the four PNGs together say what each view does to a texture of
            // each kind. They do NOT settle which one is right in game - only the glass can, and
            // that is an owner gate (C1.12; QC Q2). Nothing about either convention is changed here.
            {
                mirrorBodyMap = true;
                ForgetRuntimeImages();
                foreach (CoverPage.CoverCam cam in new[] { CoverPage.CoverCam.Earth, CoverPage.CoverCam.Map })
                {
                    MapView cv = MapProjection.WithMode(MapProjection.Default(), CoverPage.CamMapMode(cam));
                    cv = MapProjection.Centre(cv, ps.Latitude, ps.Longitude);
                    DisplayList cdl = new DisplayList(600);
                    CoverPage.Build(cdl, CW, CH, ps, cv, 1, cam);
                    if (cdl.Overflowed)
                        Console.WriteLine("  WARNING COVER MIRROR " + cam + " OVERFLOWED at " + cdl.Capacity);
                    string path = Path.Combine(outDir,
                        "ui_cover_cam_" + cam.ToString().ToLowerInvariant() + "_mirrored.png");
                    Render(cdl, CW, CH, path);
                    Console.WriteLine("  " + path + "   " + CW + "x" + CH + "   " + cdl.Count + " commands");
                }
                mirrorBodyMap = false;
                ForgetRuntimeImages();
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
            Gate g7 = CrewGates.ById(default(MissionProfile), GateId.LaunchGoG7);
            ps.GateActive = true;
            ps.GateTitle = g7.Title;
            ps.GateStage = GatePhase.GoReady;
            ps.GateItems = new GateItemView[g7.Items.Length];
            for (int gi = 0; gi < g7.Items.Length; gi++)
                ps.GateItems[gi] = new GateItemView {
                    Label = g7.Items[gi].Label, Checked = true,
                    CrewActionable = g7.Items[gi].Kind == ItemKind.CrewAck };
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
    /// <summary>
    /// Draw the body map MIRRORED in u. Preview-harness only (S100, from QC finding C-09).
    ///
    /// ---- WHAT THIS IS FOR, AND WHAT IT DELIBERATELY DOES NOT DO ----
    /// The globe and the flat map use OPPOSITE u-conventions and PageTest.NavTexture pins both as
    /// correct - "The two NAV textured views use OPPOSITE u-conventions, and BOTH are correct." That
    /// can only hold if the two read the same way on the same texture, and on the preview stand-in
    /// they visibly do not: ui_cover.png puts South America left, ui_cover_cam_map.png puts Asia
    /// left. Look at how each half was proved and the reason is plain - the flat map swap was
    /// confirmed IN GAME against KSP real _ColorMap, the globe no-swap was confirmed against this
    /// stand-in, and the two textures cannot have the same handedness, because the map swap exists
    /// precisely BECAUSE KSP is mirrored and the stand-in is a plain unmirrored Earth.
    ///
    /// So the preview will always flatter the globe and slander the map, whatever it shows. It
    /// CANNOT say which view is right in game, and this switch does not claim to: neither convention
    /// is changed here, and PageTest.NavTexture is untouched. What it does is give the question a
    /// second instrument - each view rendered against BOTH handednesses - so that whoever gets glass
    /// time can compare the four PNGs against one look at the real body instead of reasoning from
    /// one texture. Settling it needs the capsule, which is an owner gate (C1.12); QC Q2 poses it.
    /// </summary>
    private static bool mirrorBodyMap = false;

    /// <summary>Drop the runtime images so the next LoadImage re-resolves them. Called only when the
    /// mirror switch above flips, which happens between renders and never inside one.</summary>
    private static void ForgetRuntimeImages()
    {
        foreach (ImageId iid in Enum.GetValues(typeof(ImageId)))
            if (iid != ImageId.None && Images.IsRuntime(iid)) imgCache.Remove(iid);
    }

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
        // ---- THE DOCKING CAMERA GETS A MARKED TEST CARD (S100, from QC finding H-09) ----
        // It used to get nothing, for a reason that was right about the wrong thing: "there is no
        // honest still to put in its place, and falling through to the body map drew EARTH behind the
        // docking HUD - a picture that looks deliberate and is pure fiction."
        //
        // The PHOTOGRAPH was the problem, not the stand-in. ImageId.DockingCamLive is in exactly the
        // category the body map is in, and this file already states the exception for it: the GAME
        // always has a feed (DockingCamRenderer, claimed at ScreenPainter.cs:1131) and only the
        // PREVIEW cannot ask for one. With nothing here, diffing frame58_hud.png against
        // frame58_hud_noseopen.png came to 1109 pixels in an 88x88 box - the crosshair, and nothing
        // else - so the 626px docking disc and its BowlBlue corner mask, the page ONE live feature
        // and the whole point of the nose-cone gate, had never once appeared on a preview render.
        //
        // The card below is DRAWN, not photographed, and is unmistakably a test pattern: nobody can
        // mistake it for the feed, so the "a preview that flatters us is worse than none" rule is
        // kept. Its grid and bore sight make the circular clip and the corner mask CHECKABLE, which
        // is the geometry the disc exists to exercise. DockingCamStandIn below switches it off, so
        // the no-feed look - which agrees with the GL painter today - still has its own render.
        if (id == ImageId.DockingCamLive)
            return DockingCamStandIn ? BoresightCard() : null;

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
        try
        {
            Image earth = Image.FromFile(path);
            if (!mirrorBodyMap) return earth;
            // A horizontal flip and nothing else - so the ONLY difference between the two runs is
            // the texture handedness, and a disagreement in the render is the views, not the data.
            Bitmap flipped = new Bitmap(earth);
            flipped.RotateFlip(RotateFlipType.RotateNoneFlipX);
            earth.Dispose();
            return flipped;
        }
        catch (Exception e)
        {
            Console.WriteLine("  body-map stand-in: " + e.Message);
            return null;
        }
    }

    /// <summary>Whether LoadStandIn hands out the bore-sight card for the docking feed (H-09).</summary>
    private static bool DockingCamStandIn = false;

    /// <summary>
    /// A MARKED test card for the docking camera. Drawn here rather than read from assets/ on
    /// purpose: a generated pattern cannot be mistaken for a photograph of anything, and it needs no
    /// file to be checked out for the preview to exercise the disc.
    ///
    /// Square, because the disc clips it to a circle - so the card own edges landing outside the
    /// circle is itself the check that the clip is doing its job.
    /// </summary>
    private static Image BoresightCard()
    {
        const int S = 512;
        Bitmap bmp = new Bitmap(S, S, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        using (Pen grid = new Pen(Color.FromArgb(70, 120, 150, 170), 1f))
        using (Pen bore = new Pen(Color.FromArgb(200, 90, 200, 210), 2f))
        using (Font f = new Font(FontFamily, 18f, FontStyle.Regular))
        using (Brush ink = new SolidBrush(Color.FromArgb(210, 150, 200, 210)))
        {
            g.Clear(Color.FromArgb(255, 14, 22, 34));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 1; i < 8; i++)
            {
                int t = S * i / 8;
                g.DrawLine(grid, t, 0, t, S);
                g.DrawLine(grid, 0, t, S, t);
            }
            // Bore sight, and a scale ring at half radius so a wrong disc size is visible.
            g.DrawLine(bore, S / 2, S / 2 - 60, S / 2, S / 2 + 60);
            g.DrawLine(bore, S / 2 - 60, S / 2, S / 2 + 60, S / 2);
            g.DrawEllipse(bore, S / 4, S / 4, S / 2, S / 2);
            // Says what it is, in the render, so no reader has to be told.
            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            g.DrawString("PREVIEW TEST CARD", f, ink, new RectangleF(0, S * 0.30f, S, 30f), sf);
            g.DrawString("NOT A CAMERA FEED", f, ink, new RectangleF(0, S * 0.62f, S, 30f), sf);
        }
        return bmp;
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

        // ---- S75: THE TINT IS PART OF THE COMMAND, AND THE PREVIEW HAS TO OBEY IT ----
        // ScreenPainter.DrawImage multiplies every image by c.Colour (GL.Color(Tint(c.Colour)) on the
        // quad), so on the glass a named asset drawn in anything but opaque white comes out tinted.
        // This path ignored the colour entirely and drew the PNG raw, which made the preview and the
        // capsule disagree about a page's appearance — and the preview is the ONLY surface layout and
        // legibility are judged from (CLAUDE.md: restarts are the scarce resource). S75 found it by
        // tinting CoverPage's gridicons_refresh to the inert tint and watching the PNG not change.
        // Opaque white takes the old path exactly, so every other cover asset renders byte-identically.
        // ---- ONE GEOMETRY PATH, WHATEVER THE TINT (S100, from QC finding C-11) ----
        // S75's tint fix put the tinted draw on the `Rectangle` (INTEGER) overload while the opaque
        // path kept `RectangleF` (SUB-PIXEL). `ScreenPainter.DrawImage` uses float vertices for both
        // (ScreenPainter.cs:1181), so a tinted asset could sit up to 1px higher and left, and up to
        // 1px narrower and shorter, in the preview than in the game AND than an untinted asset drawn
        // beside it. Small, but it is exactly the silent two-renderer divergence S75 existed to end -
        // and it was introduced BY S75, because the new path took a different rounding rule from the
        // one it was added next to.
        //
        // The fix is not "use the float overload on both": that leaves two paths that can drift
        // again. The tint is baked into a CACHED BITMAP at native size, and then there is only one
        // draw call in this method, so there is no second rule to get wrong. Opaque white skips the
        // bake entirely and renders byte-identically, which was S75's own acceptance condition.
        Image src = img;
        if (!(c.Colour.R >= 0.999f && c.Colour.G >= 0.999f
              && c.Colour.B >= 0.999f && c.Colour.A >= 0.999f))
            src = TintedAsset(c.AssetKey, img, c.Colour);
        if (src == null) return;

        g.DrawImage(src, new RectangleF(c.A, c.B, c.C, c.D),
                    new RectangleF(0f, 0f, src.Width, src.Height), GraphicsUnit.Pixel);
    }

    // The tint baked in, one bitmap per (asset, colour). Both are small and few - the Cover draws a
    // single tinted asset today - and caching keeps the multiply off the per-frame path.
    private static readonly System.Collections.Generic.Dictionary<string, Image> tintCache =
        new System.Collections.Generic.Dictionary<string, Image>();

    private static Image TintedAsset(string key, Image img, Rgba col)
    {
        // Quantised to the byte the multiply will actually produce, so two colours that render
        // identically share one bitmap rather than filling the cache with float noise.
        string ck = key + "|" + (int)(col.R * 255f) + "," + (int)(col.G * 255f) + ","
                              + (int)(col.B * 255f) + "," + (int)(col.A * 255f);
        Image tinted;
        if (tintCache.TryGetValue(ck, out tinted)) return tinted;

        Bitmap bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
        using (Graphics tg = Graphics.FromImage(bmp))
        {
            tg.Clear(Color.Transparent);
            // Native size, 1:1 - the bake must not resample, or it would introduce a filtering
            // difference of its own on top of the one it is removing.
            tg.InterpolationMode = InterpolationMode.NearestNeighbor;
            tg.PixelOffsetMode = PixelOffsetMode.Half;
            ColorMatrix cm = new ColorMatrix(new float[][] {
                new float[] { col.R, 0f, 0f, 0f, 0f },
                new float[] { 0f, col.G, 0f, 0f, 0f },
                new float[] { 0f, 0f, col.B, 0f, 0f },
                new float[] { 0f, 0f, 0f, col.A, 0f },
                new float[] { 0f, 0f, 0f, 0f, 1f } });
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm);
                tg.DrawImage(img, new Rectangle(0, 0, img.Width, img.Height),
                             0, 0, img.Width, img.Height, GraphicsUnit.Pixel, ia);
            }
        }
        tintCache[ck] = bmp;
        return bmp;
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
