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
        ps.Align01 = 0.06; ps.AlignText = "5.4 deg";
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
        ps.PeriodText = "31.4 min";
        ps.TimeToApText = "00:12:07";
        ps.TimeToPeText = "00:27:49";
        ps.SplashdownShown = false; ps.SplashdownText = "-";
        ps.RangeM = 202.6;

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

        return 0;
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

    private static void DrawImage(Graphics g, DrawCmd c)
    {
        Image img = LoadImage(c.Image);
        // Skipped, not substituted - same rule as the GL painter. A placeholder rectangle would put
        // a shape on the page that nothing asked for.
        if (img == null) return;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // SOURCE RECT, in pixels. GDI+ has v = 0 at the TOP of the image, the opposite of texture
        // space, so VMax - the north edge in MapProjection's convention - is the source rect's TOP.
        // Getting this backwards would flip the map in the preview only, which is the worst place for
        // a discrepancy to hide because the preview is what layout is judged from.
        float sx = c.UMin * img.Width;
        float sw = (c.UMax - c.UMin) * img.Width;
        float sy = (1f - c.VMax) * img.Height;
        float sh = (c.VMax - c.VMin) * img.Height;
        if (sw <= 0f || sh <= 0f) return;

        // The navball is a SPHERE in game. Its stand-in here is the flat skin, so it is clipped to a
        // circle - otherwise the DOCKING preview would show a square where a ball goes, and someone
        // would eventually "fix" a layout that was never wrong. Preview-only: the game draws a real
        // mesh and needs no mask.
        System.Drawing.Drawing2D.GraphicsState st = null;
        if (c.Image == ImageId.NavBallLive)
        {
            st = g.Save();
            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddEllipse(c.A, c.B, c.C, c.D);
                g.SetClip(path);
            }
        }

        g.DrawImage(img, new RectangleF(c.A, c.B, c.C, c.D),
                    new RectangleF(sx, sy, sw, sh), GraphicsUnit.Pixel);

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
