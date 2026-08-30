/*
 * DragonScreen - Pages
 *
 * PURE. The page router: given a page index and the vessel state, fill a DisplayList.
 *
 * ---- THIS IS A SWITCH, NOT AN ENGINE ----
 * CLAUDE.md, "Where the cfg line is": a page can only be added or changed by editing C# and
 * rebuilding. There is no parser here, no element table, nothing config-driven. That is the whole
 * distinction between this and the rejected page engine, and it is worth re-reading the one-sentence
 * test before adding anything to this file.
 *
 * ---- ALL FIVE PAGES ARE REAL AS OF 2026-08-06 ----
 * Placeholder() survives for a page index that should not exist, and nothing routes to it any more.
 * The rule it enforced still stands and now lives at the level of individual readouts: a page never
 * shows a plausible invented number. Where stock KSP has no value, either the reading is SIMULATED
 * from real state by a stated model (CabinEnvironment) or it is dashed. A screen confidently reading
 * 0.0 is indistinguishable from a dead feed, which is the one failure a pilot most needs to see.
 */
namespace DragonScreen
{
    /// <summary>
    /// Where the vehicle is, in the only three categories the DISPLAY cares about.
    ///
    /// This exists because a readout that is technically correct can still be useless. Orbital speed
    /// on the launchpad is 175 m/s - Kerbin's rotation - and that is a true number that makes the
    /// screen look broken. Periapsis on the pad is -598.4 km, also true, also meaningless.
    ///
    /// KSP's own Vessel.Situations has eight values and is a KSP type, so the TRANSLATION happens in
    /// the glue (VesselData) and the POLICY - which speed, which fields are meaningful - lives here
    /// where it is headless tested. MAS groups LANDED | PRELAUNCH | SPLASHED the same way
    /// (MASAutoPilot.cs:257), which is the conventional split, not one invented here.
    /// </summary>
    public enum FlightRegime : byte
    {
        /// <summary>LANDED, PRELAUNCH, SPLASHED. No trajectory worth showing.</summary>
        Ground = 0,
        /// <summary>FLYING. Surface-relative speed is what the pilot wants.</summary>
        Atmosphere = 1,
        /// <summary>SUB_ORBITAL, ORBITING, ESCAPING, DOCKED. Orbital speed and a real trajectory.</summary>
        Space = 2
    }

    /// <summary>
    /// Everything the pages draw, PRE-FORMATTED. No engine types.
    ///
    /// Strings because formatting allocates, and the draw path runs every frame on three screens -
    /// the caller formats when a VALUE changes, not when a frame does. Same rule as ChromeState.
    ///
    /// RAW NUMBERS ARE HERE TOO, and that is not a contradiction: a bar needs a fraction and a dial
    /// needs an angle, and neither can be derived from a formatted string. The rule is that nothing
    /// in the draw path FORMATS - not that nothing in the draw path calculates.
    /// </summary>
    public struct PageState
    {
        public string Phase;
        public string Altitude;
        public string Velocity;
        public string Apoapsis;
        public string Periapsis;
        public string Body;
        /// <summary>Surface speed. What VELOCITY shows on the ground and in atmosphere.</summary>
        public string SurfaceVelocity;
        public FlightRegime Regime;
        /// <summary>Decided by OrbitReadout - see there for why perigee is not like apogee.</summary>
        public bool ApogeeShown, PerigeeShown;

        // ---- RAW ORBITAL STATE ----
        // Metres and degrees. Bars need fractions of a range, and the orbit plot needs a conic.
        public double AltitudeM, ApogeeM, PerigeeM, VelocityMps, SurfaceVelocityMps, InclinationDeg;
        public double BodyRadiusM, AtmosphereDepthM, CircularSpeedMps;
        /// <summary>Climbing. The half of the orbit plot the conic cannot supply.</summary>
        public bool Ascending;
        public string InclinationText, PeriodText, TimeToApText, TimeToPeText;

        /// <summary>
        /// Time to splashdown. SHOWN ONLY ON A REAL DESCENT - see VesselData for the model and its
        /// stated limits. The reference's top strip carries this where ours had MET.
        /// </summary>
        public string SplashdownText;
        public bool SplashdownShown;

        // ---- GAUGE INPUTS ----
        // Fractions 0..1 for the geometry (no allocation) plus the pre-formatted text (allocated when
        // the VALUE changes, never per frame). Both are needed: the ring needs a number, the middle
        // of the ring needs a string, and neither can be derived from the other in the draw path.
        public double Propellant01, Power01, GForce01;
        public string PropellantText, PowerText, GForceText;
        /// <summary>
        /// Names what the propellant dial is measuring RIGHT NOW - booster LF/OX, then the second
        /// stage, then Draco monopropellant. The gauge changes meaning through the flight, so the
        /// caption has to say which, exactly as the velocity readout does. See PropellantReadout.
        /// </summary>
        public string PropellantCaption;

        /// <summary>
        /// Life support, SIMULATED from real state - see CabinEnvironment for what is real and what
        /// is modelled. Values and their pre-formatted text, same rule as everything else.
        /// </summary>
        public CabinReadout Cabin;
        public string Ppo2Text, Co2Text, PressText, CabinTempText, LoopAText, LoopBText;
        public string NetPwr1Text, NetPwr2Text;
        public string CrewText;

        // ---- DOCKING ----
        // Everything the DOCKING page needs. HasTarget is the page's whole precondition: without a
        // target there is nothing to align to, and the page must say so rather than draw a HUD
        // aimed at nothing.
        public bool HasTarget;
        public string TargetName;
        public string RangeText, RateText;
        public string OffXText, OffYText, OffZText;
        public string PitchText, YawText, RollText;
        /// <summary>Angle between our port axis and the line to the target, 0..1 of 90 degrees.</summary>
        public double Align01;
        public string AlignText;
        /// <summary>Closing rate is NEGATIVE. True when we are actually approaching.</summary>
        public bool Closing;
        /// <summary>Closing hard at short range. The one docking condition worth an alarm.</summary>
        public bool ClosingFast;
        public double RangeM;
        /// <summary>
        /// RCS demand, -1..1, straight off FlightCtrlState. Drives the two right-hand corner rings
        /// on DOCKING, which the reference lights from WASD / R-F / Q-E. Ours are analogue because
        /// the control state is.
        /// </summary>
        public float TransX, TransY, TransZ, RotPitch, RotYaw, RotRoll;
        public bool RcsOn;

        // ---- NAV ----
        /// <summary>False when there is no body to be over - pages then draw no marker at all.</summary>
        // ---- FLIGHT'S SEQUENCE ----
        /// <summary>Everything the countdown/ascent step list reads. See StepList.</summary>
        public StepInputs Steps;

        /// <summary>Simulated power strings, consumables, fire and leak. See VehicleSystems.</summary>
        public SystemsState Systems;

        /// <summary>Ascent autopilot: engaged, and which phase it is flying.</summary>
        public bool AutoEngaged;
        public string AutoPhase;

        /// <summary>
        /// Control authority (rule C6, "automation must be visible"): who owns the vehicle right now, from the
        /// single AuthorityManager (Phase 2) — AUTO while the autopilot flies, MANUAL / ABORT / RECOVERY / IDLE
        /// otherwise. Raw enum for colour logic; pre-formatted text for the draw path (same rule as the rest).
        /// </summary>
        public ControlMode Mode;
        public string ModeText;

        /// <summary>
        /// FDIR (rule §4.2 fix): the authoritative fault spine reaching the crew alert channel — the
        /// highest-priority tripped fault, its recovery rung, and the pre-formatted name. From
        /// FlightDriver.LastFdirReport; None / "NOMINAL" when the autopilot isn't flying. The chrome STATE
        /// severity folds this in (Alarms.SystemSeverity) instead of the screen inventing alerts.
        /// </summary>
        public FaultKind Fault;
        public Recovery FaultResponse;
        public string FaultText;

        /// <summary>Crew procedure gate the user must act on now - the checklist card. See GateCard.</summary>
        public bool GateActive;
        public string GateTitle;
        public GatePhase GateStage;
        public GateItemView[] GateItems;

        /// <summary>Mission-phase buttons: lit when that phase is the one running.</summary>
        public bool RendezvousEngaged, DockEngaged, UndockEngaged;
        /// <summary>What each is doing, shown on the button itself when it is running.</summary>
        public string RendezvousNote, DockNote, UndockNote;
        /// <summary>True once docked - AUTO-DOCK becomes meaningless and UNDOCK becomes the live one.</summary>
        public bool Docked;

        // ---- NAV ----
        public bool HasFix;
        public double Latitude, Longitude;
        public string LatText, LonText;
        /// <summary>
        /// Ground track, in degrees. Arrays are OWNED BY THE GLUE and reused every refresh - a page
        /// must read TrackCount entries and never keep the reference.
        /// </summary>
        public double[] TrackLat, TrackLon;
        public int TrackCount;
        public bool HasTargetGround;
        public double TargetLat, TargetLon;
        public string TargetLatText, TargetLonText;
        /// <summary>Live 3D scaled-planet overlay (viewport fractions), OWNED BY THE GLUE and reused
        /// every frame. Null in the PNG preview and until the renderer fills it - the page checks
        /// Ready. See PlanetOverlay.</summary>
        public PlanetOverlay Planet;

        // ---- CREW, BY SEAT ----
        // Indexed by IVA seat, which is the capsule's OWN numbering (part.internalModel.seats), not
        // a list of who is aboard. That distinction is the point of the card: seat 2 being empty is
        // a different fact from "three crew aboard", and only the seat view shows it.
        //
        // Names are pre-formatted like everything else. Null means the seat is empty.
        public string[] SeatNames;
        public int SeatCount;

        // ---- PYRO / SEQUENCE, for the MECH subview ----
        // Every one of these is a real KSP state - a decoupler that has fired, a ModuleParachute's
        // deployment state - and together they are what a crew watches through an entry.
        public bool TrunkSet, TrunkFired, DroguesFired, MainsFired, MainsReleased;
        /// <summary>Acceleration broken out the way the reference's MECH PANEL does.</summary>
        public string AccelPosText, AccelNegText, AccelAngText, AccelCentText;

        // ---- SETTINGS ----
        public bool LightsOn;
        /// <summary>
        /// How many independent light groups the vessel really has. The reference names eight cabin
        /// lighting zones; this pod has ONE ModuleColorChanger, so the panel reports what exists
        /// rather than drawing seven buttons bound to nothing.
        /// </summary>
        public int LightCount;
        /// <summary>
        /// Which view the live camera is showing. 0-3 are the hull-swept directions; 4 upward are
        /// the vehicle's own cameras, in <see cref="CamLabels"/> order.
        /// </summary>
        public int CameraView;
        /// <summary>
        /// Every view this vehicle can actually offer, in button order: the four directions, then
        /// one entry per real camera found on the hull.
        ///
        /// ⚠ SUPPLIED BY THE GLUE, NOT LISTED HERE. The set depends on what is bolted to the
        /// craft and on whether HullCameraVDS is installed at all, neither of which pure code can
        /// know. Null or empty means "the four directions only", which is what a vehicle with no
        /// hull cameras genuinely has - the same reasoning as LightCount above: draw what exists,
        /// never a button bound to nothing.
        /// </summary>
        public string[] CamLabels;
        public string CameraResText;
        /// <summary>DOCKING has the camera forward, so the VIDEO tab cannot aim it right now.</summary>
        public bool CameraHeldByDocking;
        /// <summary>Screen brightness in tenths, 3..10.</summary>
        public int Brightness;
        /// <summary>AUTO BOOSTER RECOVERY armed (MissionConductor.AutoRecoverBooster) — the DISPLAY-tab toggle.</summary>
        public bool BoosterRecoveryOn;
        /// <summary>Page index per screen, indexed BY SCREEN ID (1..3). Element 0 is unused.</summary>
        public int[] ScreenPages;

        /// <summary>False when there is no vessel to read - pages then show dashes, not zeroes.</summary>
        public bool Valid;
    }

    /// <summary>
    /// Which orbital readouts are worth showing, and what to print when they are not.
    ///
    /// ---- ONE RULE, ONE FUNCTION, BECAUSE THE FIRST VERSION HAD TWO ----
    /// FLIGHT and VEHICLE both show apogee and perigee. The ground-guard was written into FLIGHT
    /// only, so on the pad one screen read "-" and the other read "-598.4 km" AT THE SAME MOMENT.
    /// Same class of defect as a hit-box computed apart from its label: two copies of a rule drift
    /// the instant either is edited. Both pages now call this.
    /// </summary>
    public static class OrbitReadout
    {
        /// <summary>
        /// Apogee means something as soon as the vehicle is moving - an ascending rocket's apogee is
        /// precisely the number being watched. On the ground it is the degenerate-ellipse artefact.
        /// </summary>
        public static bool ApogeeMeaningful(FlightRegime regime)
        {
            return regime != FlightRegime.Ground;
        }

        /// <summary>
        /// Perigee is different from apogee, and treating them alike is what produced the bug.
        ///
        /// A vessel with little horizontal speed has a periapsis approaching MINUS THE BODY RADIUS -
        /// -598.4 km on Kerbin. That is true and useless: it is an artefact of the radius, not
        /// anything the pilot did, and it persists through most of the ascent, not just on the pad.
        /// So the ground-guard alone was not enough.
        ///
        /// But a DEORBIT periapsis is deliberately negative - F9I aims -20 to -50 km - and that is
        /// among the most important numbers on the page. It must survive the test.
        ///
        /// The line between them is drawn at TWICE THE ATMOSPHERE DEPTH below the surface: deep
        /// enough to keep every real deorbit target, shallow enough to reject the radius artefact.
        /// Scaling from the atmosphere rather than a fixed kilometre figure is what makes it work
        /// unchanged under RSS/RO, where both numbers are several times larger.
        /// </summary>
        public static bool PerigeeMeaningful(FlightRegime regime, double perigeeMetres,
                                             double atmosphereDepthMetres)
        {
            if (regime == FlightRegime.Ground) return false;
            if (double.IsNaN(perigeeMetres)) return false;
            double floor = (atmosphereDepthMetres > 0.0) ? -2.0 * atmosphereDepthMetres : -140000.0;
            return perigeeMetres > floor;
        }

        /// <summary>
        /// WHICH VELOCITY TO SHOW, AND WHAT TO CALL IT. Caption and value together, always.
        ///
        /// ---- THIS IS THE THIRD TIME ----
        /// Orbital speed reads 175 m/s standing on the launchpad, because that is Kerbin turning.
        /// The user reported it on FLIGHT; it was fixed there. Then VEHICLE gained a velocity BAR on
        /// 2026-08-06 that read s.Velocity directly, and the capture from the pad shows
        /// "INERTIAL VELOCITY 175 m/s" all over again - a rule written in one place and not the
        /// other, which is exactly what OrbitReadout was created to stop happening to apogee and
        /// perigee. Adding a readout without routing it through the shared rule is the failure mode,
        /// not the rule being wrong.
        ///
        /// The caption travels WITH the value and is not optional. A speed that silently changes
        /// meaning is worse than one that is consistently wrong, because nothing on screen says it
        /// has changed.
        /// </summary>
        public static void Velocity(PageState s, out string caption, out string value,
                                    out double mps)
        {
            bool space = (s.Regime == FlightRegime.Space);
            caption = space ? "ORBITAL VELOCITY" : "SURFACE VELOCITY";
            value = space ? s.Velocity : s.SurfaceVelocity;
            mps = space ? s.VelocityMps : s.SurfaceVelocityMps;
        }
    }

    /// <summary>
    /// Full-scale values for the horizontal bar gauges.
    ///
    /// ---- A BAR WITHOUT A STATED RANGE IS A DECORATION ----
    /// The reference fills its bars to a fraction, which means every one of them has a range its
    /// designer chose. Ours are DERIVED FROM THE BODY rather than hard-coded in kilometres, so the
    /// same code is sensible over Kerbin and over Earth under RSS - the same reasoning that makes
    /// OrbitReadout's perigee floor scale off the atmosphere.
    ///
    /// They return -1 for "no meaningful range", which draws the track and no fill. That is honest:
    /// an empty bar under a real number says "this reading has no scale here", where a bar filled
    /// against a guessed maximum would quietly assert one.
    /// </summary>
    public static class BarScale
    {
        /// <summary>Against circular orbital speed at the surface, with headroom for a transfer.</summary>
        public static double Velocity(double mps, double circularSpeed)
        {
            if (circularSpeed <= 0.0) return -1.0;
            return Frac(mps / (circularSpeed * 1.25));
        }

        /// <summary>
        /// Five atmospheres deep is a good working ceiling: 350 km over Kerbin, ~700 km over Earth,
        /// so a station orbit sits in the middle of the bar in both cases rather than at one end.
        /// Airless bodies fall back to a fraction of the radius.
        /// </summary>
        public static double Altitude(double metres, double atmosphereDepth, double bodyRadius)
        {
            double full = (atmosphereDepth > 0.0) ? atmosphereDepth * 5.0 : bodyRadius * 0.5;
            if (full <= 0.0) return -1.0;
            return Frac(metres / full);
        }

        /// <summary>Polar is full scale. A near-equatorial orbit SHOULD read almost empty.</summary>
        public static double Inclination(double deg)
        {
            if (double.IsNaN(deg)) return -1.0;
            double d = (deg < 0.0) ? -deg : deg;
            if (d > 90.0) d = 180.0 - d;
            return Frac(d / 90.0);
        }

        /// <summary>
        /// Range to the target. Full scale 100 km, which covers a phasing orbit; inside that the bar
        /// is the useful one, and the number carries the detail when it pegs.
        /// </summary>
        public static double Range(double metres)
        {
            if (metres <= 0.0) return -1.0;
            return Frac(metres / 100000.0);
        }

        private static double Frac(double v)
        {
            if (double.IsNaN(v) || v <= 0.0) return 0.0;
            return (v > 1.0) ? 1.0 : v;
        }
    }

    public static class Pages
    {
        /// <summary>
        /// Worst-case commands any single page emits.
        ///
        /// NAV is the driver, and its three views cost differently: the MAP view is one command per
        /// ground-track sample (90), the ORBIT view is up to two per globe strip (128) plus the
        /// ellipse, and the 3D PLANET view is one live image plus a dot per projected orbit AND target
        /// sample (2 x 96) plus markers. Sized with headroom rather than to the exact worst case,
        /// because the failure mode of being one short is a silently truncated page - and the capacity
        /// test builds every page in ALL THREE NAV views precisely because the worst case is not the
        /// obvious one. FLIGHT's crew checklist card (GateCard) adds ~25 commands over the base page;
        /// the headroom covers it.
        /// </summary>
        public const int Commands = 480;

        /// <summary>Top of the page body, below the telemetry strip.</summary>
        private const float StripHeight = 76f;

        public static void Build(DisplayList dl, int pageIndex, int w, int h, PageState s,
                                 MapView view, int thisScreen, int subview)
        {
            if (dl == null || w <= 0 || h <= 0) return;

            switch (pageIndex)
            {
                case 0: Flight(dl, w, h, s); break;
                case 1: VehicleTabs(dl, w, h, s, subview); break;
                case 2: NavPage.Build(dl, w, h, s, view); break;
                case 3: Docking(dl, w, h, s); break;
                case 4: SettingsTabs(dl, w, h, s, thisScreen, subview); break;
                default: Placeholder(dl, w, h, pageIndex); break;
            }
        }

        /// <summary>Subview-less overload, for pages and tests that do not care.</summary>
        public static void Build(DisplayList dl, int pageIndex, int w, int h, PageState s,
                                 MapView view, int thisScreen)
        {
            Build(dl, pageIndex, w, h, s, view, thisScreen, 0);
        }

        /// <summary>Subview-less overload, kept so callers that have no tabs stay simple.</summary>
        public static PageHit HitTest(int pageIndex, float px, float py, int w, int h)
        {
            return HitTest(pageIndex, px, py, w, h, 0);
        }

        /// <summary>Tab names for a page, or null when it has no subviews.</summary>
        public static string[] Tabs(int pageIndex)
        {
            if (pageIndex == 1) return VehicleTabNames;
            if (pageIndex == 4) return SettingsPage.Tabs;
            return null;
        }

        private static readonly string[] VehicleTabNames = { "OVERVIEW", "MECH" };

        /// <summary>SETTINGS is a card with tabs too - Fifth.vue is Audio + Cabin + Video.</summary>
        private static void SettingsTabs(DisplayList dl, int w, int h, PageState s,
                                         int thisScreen, int subview)
        {
            Card.Build(dl, w, h, SettingsPage.Tabs, subview);
            SettingsPage.Build(dl, w, h, s, thisScreen, subview);
        }

        /// <summary>
        /// VEHICLE is a CARD WITH TABS, which is what Third.vue actually is - see Card. Overview was
        /// the whole page until 2026-08-06; Mech is the half we never had.
        /// </summary>
        private static void VehicleTabs(DisplayList dl, int w, int h, PageState s, int subview)
        {
            Card.Build(dl, w, h, VehicleTabNames, subview);
            if (subview == 1) MechPage.Build(dl, w, h, s);
            else Vehicle(dl, w, h, s);
        }

        /// <summary>
        /// What a touch inside the page body means. The chrome bar is tested FIRST by the caller, so
        /// this never has to know about it.
        ///
        /// Pages with no controls return None, which is not a failure - most of a flight is spent
        /// reading rather than pressing, and a page that swallowed touches would be worse than one
        /// that ignores them.
        /// </summary>
        public static PageHit HitTest(int pageIndex, float px, float py, int w, int h,
                                      int subview, int extraCams = 0)
        {
            // TABS FIRST. They live in the card's notch, over the subview, so a subview control
            // that happened to overlap the notch must not eat the only way back out of it - the same
            // reason the chrome bar is tested before any page.
            string[] tabs = Tabs(pageIndex);
            if (tabs != null)
            {
                int t = Card.HitTest(px, py, tabs.Length, w, h);
                if (t >= 0) return PageHit.Of(PageAct.SetSubview, t);
            }

            switch (pageIndex)
            {
                case 0: return FlightHitTest(px, py, w, h);
                case 2: return NavPage.HitTest(px, py, w, h);
                case 4: return SettingsPage.HitTest(px, py, w, h, subview, extraCams);
                default: return PageHit.None;
            }
        }

        /// <summary>
        /// FLIGHT - the telemetry strip over the vehicle gauges.
        ///
        /// ---- THE STRIP IS THE REFERENCE'S, FIELD FOR FIELD ----
        /// Frame 67's top strip reads
        ///     ACTIVE PHASE  SPLASHDOWN TIME  INERTIAL VELOCITY  ALTITUDE  APOGEE  PERIGEE  INCLINATION
        /// and is identical on every page that has it, which is what marks it as chrome rather than
        /// content. Ours had six fields and was missing SPLASHDOWN TIME and INCLINATION.
        ///
        /// APOGEE / PERIGEE, not APOAPSIS / PERIAPSIS: SpaceX uses the Earth terms and so does every
        /// frame in the reference set. KSP's wording is not this vehicle's wording.
        ///
        /// The one place we deliberately DIVERGE is the velocity caption - see below.
        /// </summary>
        private static void Flight(DisplayList dl, int w, int h, PageState s)
        {
            float pad = 28f;
            float y = pad;

            // The strip sits on its own panel so it reads as chrome-of-the-page rather than as
            // floating text, which is how the reference separates it from the content below.
            float stripH = StripHeight;
            dl.Rect(0f, 0f, w, stripH, DragonPalette.Inset1);
            dl.Rect(0f, stripH - 2f, w, 2f, DragonPalette.Hairline);

            // ---- WHICH VELOCITY, AND SAY WHICH ----
            // The reference says INERTIAL VELOCITY always. We do not, and this is the one field where
            // being unlike the reference is the correct call - see OrbitReadout.Velocity, which BOTH
            // pages now call rather than each deciding for itself.
            string velCap, velVal;
            double velMps;
            OrbitReadout.Velocity(s, out velCap, out velVal, out velMps);

            // Apogee and perigee are NOT governed by the same test - see OrbitReadout. Both pages
            // call it; neither decides for itself.
            string apo = s.ApogeeShown ? s.Apoapsis : "-";
            string per = s.PerigeeShown ? s.Periapsis : "-";
            string spl = s.SplashdownShown ? s.SplashdownText : "-";

            string[] caps = { "ACTIVE PHASE", "SPLASHDOWN TIME", velCap, "ALTITUDE",
                              "APOGEE", "PERIGEE", "INCLINATION" };
            string[] vals = { s.Phase, spl, velVal, s.Altitude, apo, per, s.InclinationText };

            // Evenly pitched: with no way to measure a string in src/pure (see DisplayList.Text),
            // even pitch is the honest layout rather than one that pretends to know how wide
            // "SPLASHDOWN TIME" is.
            float pitch = (w - pad * 2f) / caps.Length;
            for (int i = 0; i < caps.Length; i++)
            {
                float x = pad + pitch * i;
                dl.Text(caps[i], x, y, Typography.Caption, TextAlign.Left, DragonPalette.Text6);
                dl.Text(s.Valid ? (vals[i] ?? "-") : "-", x, y + 24f,
                        Typography.Body, TextAlign.Left,
                        s.Valid ? DragonPalette.Text0 : DragonPalette.Text7);
            }

            // ---- THE GAUGES ----
            // Three, because three is what we can honestly read from the vessel: Draco propellant,
            // electrical charge, and g-force.
            //
            // Body area runs from the strip to the chrome bar. Centred in it, so the row stays put
            // when the screen height differs by a few pixels between displays.
            float bodyTop = stripH;
            float bodyBottom = h - ChromeBar.Height;
            float cy = (bodyTop + bodyBottom) * 0.5f;

            // ---- SIZE THE GAUGES FROM THE TIGHTER AXIS, NOT THE ROOMIER ONE ----
            // First attempt took the radius from the body HEIGHT (0.30 x 563 = 169 px) while the
            // horizontal pitch was only 243 px - so 338 px of gauge went into a 243 px slot and all
            // three overlapped. Caught in the PNG preview in seconds; it would have cost a restart to
            // find in game. A layout constrained by two axes must be sized by the smaller one.
            float gaugesLeft = w * 0.30f;          // left of this belongs to the phase sidebar
            float gaugesRight = w - pad;
            float step = (gaugesRight - gaugesLeft) / 3f;
            float radius = step * 0.40f;           // 0.40 leaves a fifth of the pitch as clear gap
            float vertical = (bodyBottom - bodyTop) * 0.30f;
            if (radius > vertical) radius = vertical;
            float thickness = radius * 0.16f;

            float first = gaugesLeft + step * 0.5f;

            // IDENTITY COLOURS, NOT THRESHOLDS. Each dial keeps its own colour whatever it reads;
            // alarm goes to the chrome bar through Alarms.Mask. See DragonPalette's gauge block.
            Gauge.Labelled(dl, first, cy, radius, thickness,
                           s.Valid ? s.Propellant01 : 0.0,
                           s.Valid ? s.PropellantText : "-", "%",
                           s.PropellantCaption ?? "PROPELLANT",
                           DragonPalette.GaugeTrack, DragonPalette.GaugePropellant);

            Gauge.Labelled(dl, first + step, cy, radius, thickness,
                           s.Valid ? s.Power01 : 0.0,
                           s.Valid ? s.PowerText : "-", "%", "POWER",
                           DragonPalette.GaugeTrack, DragonPalette.GaugePower);

            Gauge.Labelled(dl, first + step * 2f, cy, radius, thickness,
                           s.Valid ? s.GForce01 : 0.0,
                           s.Valid ? s.GForceText : "-", "g", "G-FORCE",
                           DragonPalette.GaugeTrack, DragonPalette.GaugeGForce);

            // ---- THE PHASE SIDEBAR ----
            // Frame 67, the closest reference to this page, puts the mission steps here with the
            // running one highlighted. The step LIST waits on the flight sequencer that knows which
            // step is running; the current phase is real now and comes from MissionPhase, not from
            // Vessel.Situations.
            float sideX = pad;
            float sw = w * 0.30f - pad * 2f;

            dl.Text("PHASE", sideX, bodyTop + 28f, Typography.Caption, TextAlign.Left,
                    DragonPalette.Text6);
            dl.Text(s.Valid ? (s.Phase ?? "-") : "-", sideX, bodyTop + 54f,
                    Typography.Value, TextAlign.Left,
                    s.Valid ? DragonPalette.Accent : DragonPalette.Text7);

            // ABORT MODE sits beside the phase because that is what it is - which escape option the
            // vehicle currently has. Read the caveat in StepList.AbortMode before quoting it
            // anywhere: the eight-mode structure is the real vehicle's, the boundaries are ours.
            dl.Text("ABORT MODE", sideX + sw * 0.55f, bodyTop + 28f, Typography.Caption,
                    TextAlign.Left, DragonPalette.Text6);
            string am = s.Valid ? StepList.AbortMode(s.Steps) : "-";
            dl.Text(am, sideX + sw * 0.55f, bodyTop + 56f, Typography.Caption, TextAlign.Left,
                    (am == "DISARMED") ? DragonPalette.Caution : DragonPalette.Text2);

            // ---- BODY, AND THE APSIS CLOCKS ----
            // BODY was in the six-field strip and fell out when the strip was rebuilt to the
            // reference's seven, which have no room for it. It is not a field to lose: everything
            // else on the page - which velocity is meaningful, what an altitude means, where the
            // perigee floor sits - is relative to the body being orbited.
            float sy = bodyTop + 110f;
            SideRow(dl, sideX, sy, sw, "BODY", s.Valid ? s.Body : "-");
            SideRow(dl, sideX, sy + 42f, sw, "TIME TO APOGEE",
                    (s.Valid && s.ApogeeShown) ? s.TimeToApText : "-");
            SideRow(dl, sideX, sy + 84f, sw, "TIME TO PERIGEE",
                    (s.Valid && s.PerigeeShown) ? s.TimeToPeText : "-");
            SideRow(dl, sideX, sy + 126f, sw, "PERIOD", s.Valid ? s.PeriodText : "-");

            StepColumn(dl, s, sideX, sy + 182f, sw, h);

            // ---- AUTO SEQUENCE ----
            // The vehicle flies itself and the crew can take it or hand it back - that authority is
            // real, and it is not a launch button. The countdown is still the ground's.
            float ax, ay, aw, ah;
            AutoRect(w, h, out ax, out ay, out aw, out ah);
            Control.Button(dl, ax, ay, aw, ah,
                           s.AutoEngaged ? ("AUTO  " + (s.AutoPhase ?? "")) : "AUTO SEQUENCE",
                           s.AutoEngaged, s.Valid);

            for (int i = 0; i < MissionButtons; i++)
            {
                float mx, my, mw, mh;
                MissionRect(i, w, h, out mx, out my, out mw, out mh);
                Control.Button(dl, mx, my, mw, mh, MissionLabel(s, i),
                               MissionLit(s, i), s.Valid && MissionUsable(s, i));
            }

            // ---- CREW CHECKLIST CARD ----
            // Drawn LAST, over everything: when the conductor reaches a gate the crew must act on, the
            // card is the modal step - complete the checklist, then GO / NO-GO / ABORT. It is only active
            // when there is something to do (CrewProcedureOps.CrewActionNeeded), so it never covers the
            // gauges during a phase the crew is monitoring.
            if (s.GateActive)
                GateCard.Draw(dl, s.GateTitle, s.GateStage, s.GateItems, w, h);
        }

        /// <summary>What the (single) mission button says. RENDEZVOUS + AUTO-DOCK were removed (user
        /// 2026-08-28) — AUTO SEQUENCE flies those; only UNDOCK is a manual control, for when the crew is
        /// ready to leave the station. The return is flown by pressing AUTO SEQUENCE again.</summary>
        public static string MissionLabel(PageState s, int index)
        {
            return s.UndockEngaged ? ("UNDOCK  " + Short(s.UndockNote)) : "UNDOCK";
        }

        public static bool MissionLit(PageState s, int index)
        {
            return s.UndockEngaged;
        }

        /// <summary>
        /// Greyed rather than hidden when it cannot be pressed. Only meaningful from the berth (docked).
        ///
        /// ⚠ A control the crew can see but not use must LOOK unusable. A live-looking button that
        /// silently does nothing is indistinguishable from a dead touchscreen - the same argument
        /// FlightHitTest's header already makes about swallowing presses.
        /// </summary>
        public static bool MissionUsable(PageState s, int index)
        {
            return s.Docked || s.UndockEngaged;        // only meaningful from the berth
        }

        private static string Short(string note)
        {
            if (string.IsNullOrEmpty(note) || note == "-") return "";
            int cut = note.IndexOf(" - ");
            string head = (cut > 0) ? note.Substring(0, cut) : note;
            return (head.Length > 16) ? head.Substring(0, 16) : head;
        }

        /// <summary>
        /// The three MISSION buttons, stacked under AUTO SEQUENCE. Same rect for drawing and hitting.
        ///
        /// ⚠ PLACED IN THE EMPTY MIDDLE, on the precedent AUTO SEQUENCE already set and for the same
        /// reason: the reference UI has no control for any of this because the real Dragon's crew do
        /// not command a rendezvous from a touchscreen. These are ours, so they go where there is
        /// room rather than on top of something that belongs to the reference - which is the mistake
        /// AutoRect's own comment records being caught twice.
        /// </summary>
        public static void MissionRect(int index, int w, int h, out float x, out float y,
                                       out float rw, out float rh)
        {
            rh = MissionHeight;

            // ---- ⛔ MEASURED UP FROM THE TAB BAR, NOT DOWN FROM AUTO SEQUENCE. ----
            // Stacked downward they ran straight under the chrome bar: on a 703-high screen the bar
            // starts at 639 and two of the three landed at 623-657 and 665-699, so AUTO-DOCK was half
            // covered and UNDOCK & LAND was invisible. The test that should have caught it asserted
            // against the PAGE bottom instead of the bar. `NavPage.MapRect` had the right pattern
            // three files away.
            y = ChromeBar.TopY(h) - MissionGap - rh;

            // ---- AND A ROW, NOT A COLUMN, STARTING RIGHT OF THE SIDEBAR. ----
            // A column here would cross the gauges; the strip between AUTO SEQUENCE and the bar is
            // one button tall. The left 30% belongs to the phase sidebar and the step list, so the
            // row starts clear of it rather than centring on the page and overlapping the steps.
            float left = w * SidebarFrac + MissionGap;
            float right = w - SidePad;
            float total = right - left;
            if (total > MissionRowMax) total = MissionRowMax;
            rw = (total - MissionGap * (MissionButtons - 1)) / MissionButtons;
            x = left + (rw + MissionGap) * index;
        }

        /// <summary>Gap between the mission buttons, and between the row and the tab bar.</summary>
        public const float MissionGap = 8f;
        /// <summary>Height of a mission button.</summary>
        public const float MissionHeight = 34f;
        /// <summary>The row never grows past this, however wide the screen.</summary>
        public const float MissionRowMax = 660f;
        /// <summary>Left 30% is the phase sidebar and the step list - see Flight().</summary>
        public const float SidebarFrac = 0.30f;
        /// <summary>Page margin, matching Flight()'s own `pad`.</summary>
        public const float SidePad = 28f;
        /// <summary>How many there are: RENDEZVOUS, AUTO-DOCK, UNDOCK &amp; LAND.</summary>
        public const int MissionButtons = 1;   // just UNDOCK (RENDEZVOUS + AUTO-DOCK removed — AUTO SEQUENCE flies them)

        /// <summary>
        /// The AUTO SEQUENCE button, bottom of FLIGHT's sidebar. Same rect for drawing and hitting.
        /// </summary>
        public static void AutoRect(int w, int h, out float x, out float y,
                                    out float rw, out float rh)
        {
            // CENTRED UNDER THE GAUGES, NOT UNDER THE SIDEBAR. The first placement put it at the
            // bottom of the sidebar, straight on top of DRAGON SEPARATION and NOSE CONE OPEN - the
            // hit test round-trip caught it, which is the second time that check has found a control
            // sitting on another one. The middle of the page is empty and this is what it is for.
            rw = 280f;
            rh = 34f;
            x = w * 0.5f - rw * 0.5f;
            y = h - ChromeBar.Height - 100f;
        }

        /// <summary>
        /// FLIGHT's controls: ticking a crew step, and AUTO SEQUENCE.
        ///
        /// Deliberately does NOT test whether the step is the running one - that is the caller's
        /// business, and a hit test that silently swallowed a press on a step the crew could see
        /// would be indistinguishable from a dead touchscreen.
        /// </summary>
        private static PageHit FlightHitTest(float px, float py, int w, int h)
        {
            float ax, ay, aw, ah;
            AutoRect(w, h, out ax, out ay, out aw, out ah);
            if (px >= ax && px <= ax + aw && py >= ay && py <= ay + ah)
                return PageHit.Of(PageAct.ToggleAuto, 0);

            for (int i = 0; i < MissionButtons; i++)
            {
                float mx, my, mw, mh;
                MissionRect(i, w, h, out mx, out my, out mw, out mh);
                if (px < mx || px > mx + mw || py < my || py > my + mh) continue;
                return PageHit.Of(PageAct.Undock, 0);   // the one mission button = UNDOCK
            }

            int visible = StepVisible(h);
            for (int i = 0; i < visible; i++)
            {
                float x, y, rw, rh;
                StepRect(i, w, h, out x, out y, out rw, out rh);
                if (px >= x && px <= x + rw && py >= y - 3f && py <= y + rh - 4f)
                    return PageHit.Of(PageAct.AckStep, StepIdAt(i, h));
            }
            return PageHit.None;
        }

        // ------------------------------------------------------------------ the step list

        /// <summary>
        /// Row pitch of the step list, and the tappable height of a crew step.
        ///
        /// 18, not 30. Fifteen milestones have to fit between the apsis rows and the chrome bar -
        /// 278 px - and at 30 the last four ran off the bottom of the page where nothing could reach
        /// them. Caught by the test that asserts the whole list clears the chrome bar, which is worth
        /// more than it looks: an unreachable control is invisible in a screenshot of the part of the
        /// page that DID fit. Dense type is 12, so 18 leaves a comfortable 6 px of leading.
        /// </summary>
        public const float StepPitch = 18f;

        /// <summary>Never below this - dense type is 12 and needs a little leading.</summary>
        public const float StepPitchMin = 13f;

        /// <summary>Top of the step list, matching FlightPage's sidebar exactly.</summary>
        public static float StepTop { get { return StripHeight + 110f + 182f; } }

        /// <summary>
        /// The pitch that actually fits on THIS screen.
        ///
        /// ---- ⛔ A CONSTANT PITCH IS A BUG WAITING FOR A SHORTER SCREEN. ----
        /// 18 was chosen when the only screens were 703 and 710 high, and the comment above records
        /// the same fault being fixed once already by changing 30 to 18. At 600 the list runs to 638
        /// against a tab bar at 536 and the last six milestones are unreachable - found by the layout
        /// sweep, not by a flight, which is the whole point of the sweep.
        ///
        /// Deriving it from the space left means the list can never overflow at any resolution, and
        /// on the screens we actually ship it is still exactly 18 because the cap binds first.
        /// </summary>
        public static float StepPitchFor(int h)
        {
            int rows = (int)StepId.Count;
            if (rows < 2) return StepPitch;
            float room = ChromeBar.TopY(h) - StepTop;
            float fit = room / rows;
            if (fit > StepPitch) fit = StepPitch;
            if (fit < StepPitchMin) fit = StepPitchMin;
            return fit;
        }

        /// <summary>
        /// How many step rows actually fit, and therefore how many are drawn.
        ///
        /// ---- ⚠ SHRINKING TYPE HAS A FLOOR; THE LIST DOES NOT HAVE TO. ----
        /// On a screen too short for all fifteen milestones even at `StepPitchMin`, the choice is
        /// between unreadable rows and fewer rows. Fewer rows, and the window is anchored at the
        /// END: the completed milestones scroll off the top and the RUNNING and UPCOMING ones stay
        /// on the glass, which is the half the crew is actually reading. Clipping the bottom - what
        /// the fixed pitch used to do - hides exactly the wrong end, including SPLASHDOWN.
        ///
        /// On both screens the Dragon actually has (703 and 710 high) this returns all fifteen and
        /// the pitch is the full 18, so nothing changes on the vehicle we fly.
        /// </summary>
        public static int StepVisible(int h)
        {
            int rows = (int)StepId.Count;
            float pitch = StepPitchFor(h);
            if (pitch <= 0f) return rows;
            int fit = (int)((ChromeBar.TopY(h) - StepTop) / pitch);
            if (fit > rows) fit = rows;
            if (fit < 1) fit = 1;
            return fit;
        }

        /// <summary>The StepId drawn in window slot <paramref name="slot"/>. See StepVisible.</summary>
        public static int StepIdAt(int slot, int h)
        {
            int rows = (int)StepId.Count;
            return slot + (rows - StepVisible(h));
        }

        /// <summary>Reused every frame - the draw path allocates nothing. See the DisplayList rule.</summary>
        private static readonly StepRow[] stepScratch = new StepRow[(int)StepId.Count];

        /// <summary>
        /// Where step <paramref name="i"/> is drawn, for the hit test. ONE source for drawing and
        /// hitting, the ChromeBar.LinkRect rule - a second copy of this arithmetic is how a control
        /// ends up a few pixels from where it looks.
        /// </summary>
        public static void StepRect(int i, int w, int h, out float x, out float y,
                                    out float rw, out float rh)
        {
            float pad = w * 0.02f;
            x = pad;
            rw = w * 0.30f - pad * 2f;
            // Must track FlightPage's sidebar exactly: bodyTop + 110 for the apsis rows, + 182 for
            // the step column below them. Both live here so the two cannot drift apart.
            float pitch = StepPitchFor(h);
            // `i` is a WINDOW SLOT, not a StepId - see StepVisible. On our screens they are the same
            // because every row fits.
            y = StepTop + i * pitch;
            rh = pitch;
        }

        /// <summary>
        /// The countdown and ascent milestones.
        ///
        /// THERE IS NO LAUNCH STEP. The Launch Director commands the countdown and the ground calls
        /// the abort-mode switches - the crew do checks and monitor. See StepList's header.
        /// </summary>
        private static void StepColumn(DisplayList dl, PageState s, float x, float y, float w, int h)
        {
            dl.Text("SEQUENCE", x, y - 26f, Typography.Caption, TextAlign.Left, DragonPalette.Text6);

            // ONE pitch for drawing and hitting - StepRect derives the same number from h.
            float pitch = StepPitchFor(h);

            int n = s.Valid ? StepList.Build(s.Steps, stepScratch) : 0;
            if (n == 0)
            {
                dl.Text("-", x, y, Typography.Caption, TextAlign.Left, DragonPalette.Text7);
                return;
            }

            int first = n - StepVisible(h);
            if (first < 0) first = 0;
            for (int i = first; i < n; i++)
            {
                StepRow r = stepScratch[i];
                float ry = y + (i - first) * pitch;

                // A marker per state, in the panel's own language rather than an invented icon set:
                // filled for done, hollow-bright for the running one, dim for pending.
                Rgba tint = (r.State == StepState.Done) ? DragonPalette.Text3
                          : (r.State == StepState.Active) ? DragonPalette.Accent
                          : DragonPalette.Text7;

                float mx = x + 5f, my = ry + 3f;
                if (r.State == StepState.Done) dl.Rect(mx, my, 8f, 8f, DragonPalette.Go);
                else if (r.State == StepState.Active) dl.Box(mx, my, 8f, 8f, 2f, DragonPalette.Accent);
                else dl.Box(mx, my, 8f, 8f, 1f, DragonPalette.Text8);

                dl.Text(r.Label, x + 22f, ry, Typography.Dense, TextAlign.Left, tint);
                dl.Text(r.TimeRef, x + w, ry, Typography.Dense, TextAlign.Right,
                        DragonPalette.Text8);

                // A CREW step that is still running is the only tappable thing here, so it is the
                // only one that gets an affordance. Marking the others would promise a control that
                // is not there - the vehicle does those, not the crew.
                if (r.CrewStep && r.State == StepState.Active)
                    dl.Box(x, ry - 2f, w, pitch - 2f, 1f, DragonPalette.Accent);
            }
        }

        private static void SideRow(DisplayList dl, float x, float y, float w,
                                    string caption, string value)
        {
            dl.Text(caption, x, y, Typography.Caption, TextAlign.Left, DragonPalette.Text6);
            dl.Text(value ?? "-", x + w, y, Typography.Body, TextAlign.Right, DragonPalette.Text0);
            dl.Rect(x, y + 28f, w, 1f, DragonPalette.Inset1);
        }

        /// <summary>
        /// VEHICLE OVERVIEW - laid out from the reference, not invented.
        ///
        /// `plugin/build/refart/flight_control_ui_container.png` plus the user's photograph of the
        /// real screen: the capsule render dead CENTRE, ring gauges to its left, and value-with-bar
        /// readouts down the right.
        ///
        /// ---- EIGHT DIALS, WHICH IS THE REFERENCE'S SET ----
        /// PPO2, CABIN TEMP, CABIN PRESSURE, CO2, LOOP A, LOOP B, NET PWR 1, NET PWR 2. Stock KSP
        /// models none of them, so CabinEnvironment SIMULATES them from inputs that ARE real - crew
        /// count, hull temperature, electrical state - rather than showing blanks or constants. A
        /// blank panel is less honest-looking than a working one; a constant is a lie. See that file.
        ///
        /// ---- AND THE STATUS ROW, WHICH IS NOW LOad-BEARING ----
        /// The reference's left checklist is its alarm channel, and since the dials became
        /// identity-coloured it is ours too: with no threshold colour on the rings, the status row and
        /// the chrome bar are the ONLY things that say something is wrong.
        /// </summary>
        private static void Vehicle(DisplayList dl, int w, int h, PageState s)
        {
            // ---- BOUNDED BY THE CARD, NOT BY THE PAGE ----
            // This page kept its full-page geometry when VEHICLE gained tabs, so the status row went
            // on drawing at the bottom of the SCREEN - which is now where the card's notch and the
            // OVERVIEW | MECH tabs are. "MECH" sat on top of "PROPELLANT". Any page inside a card
            // must take its bounds FROM the card; that is the whole reason Card.Body exists.
            float bx, by, bw, bh;
            Card.Body(w, h, out bx, out by, out bw, out bh);

            float pad = 28f;
            float bodyTop = by;
            float bodyBottom = by + bh;
            // 56, not 40: the row is two lines - a name and its state word - and at 40 the second
            // line sat exactly on the boundary below it.
            float statusH = 56f;
            float contentBottom = bodyBottom - statusH;
            float cy = (bodyTop + contentBottom) * 0.5f;
            float cxPage = bx + bw * 0.5f;

            dl.Text("VEHICLE OVERVIEW", cxPage, bodyTop, Typography.Body, TextAlign.Centre,
                    DragonPalette.Text1);

            // ---- THE CAPSULE, CENTRED ----
            // Height-driven so it always clears the chrome bar; Images.FitHeight keeps the aspect,
            // because a squashed Dragon is the most obvious possible sign that nobody looked.
            float artH = (contentBottom - bodyTop) * 0.80f;
            float ix, iy, iw, ih;
            if (Images.FitHeight(ImageId.Dragon, cxPage, cy + 8f, artH,
                                 out ix, out iy, out iw, out ih))
                dl.Image(ImageId.Dragon, ix, iy, iw, ih, DragonPalette.White);

            // ---- EIGHT DIALS, TWO COLUMNS, LEFT OF THE CAPSULE ----
            // Laid out on an explicit grid rather than by nudging offsets off the radius: the
            // previous three-row version derived its rows from the radius and a fourth row would have
            // run off the bottom. Rows are a pitch; the radius fits INSIDE the pitch.
            float gridTop = bodyTop + 34f;
            float gridH = contentBottom - gridTop;
            float rowPitch = gridH / 4f;
            float col1 = bx + 118f, col2 = bx + 354f;
            float gr = rowPitch * 0.34f;
            float gth = gr * 0.20f;

            CabinReadout cb = s.Cabin;
            // ---- MAGNITUDE ON THE DIAL, SIGN IN THE NUMBER ----
            // Net power is generation minus draw, so it is NEGATIVE whenever the vehicle is running
            // off its batteries - which is most of a mission. A three-quarter dial cannot show a
            // negative, and the first version simply drew nothing: two dials permanently empty next
            // to a readout saying -59 W. The dial shows how HARD the bus is working; the text keeps
            // the sign, which is where the direction belongs.
            double np1 = System.Math.Abs(cb.NetPwr1W) / Cabin.NetPwrFullScale;
            double np2 = System.Math.Abs(cb.NetPwr2W) / Cabin.NetPwrFullScale;

            float r0 = gridTop + rowPitch * 0.5f;
            Dial(dl, col1, r0, gr, gth, s, cb.Ppo201, s.Ppo2Text, "psia", "PPO2",
                 DragonPalette.GaugePpo2);
            Dial(dl, col2, r0, gr, gth, s, cb.CabinTemp01, s.CabinTempText, "deg C", "CABIN TEMP",
                 DragonPalette.GaugeCabinTemp);

            float r1 = gridTop + rowPitch * 1.5f;
            Dial(dl, col1, r1, gr, gth, s, cb.Press01, s.PressText, "psia", "CABIN PRESSURE",
                 DragonPalette.GaugePress);
            Dial(dl, col2, r1, gr, gth, s, cb.Co201, s.Co2Text, "mmHg", "CO2",
                 DragonPalette.GaugeCo2);

            float r2 = gridTop + rowPitch * 2.5f;
            Dial(dl, col1, r2, gr, gth, s, cb.LoopA01, s.LoopAText, "deg C", "LOOP A",
                 DragonPalette.GaugeLoop);
            Dial(dl, col2, r2, gr, gth, s, cb.LoopB01, s.LoopBText, "deg C", "LOOP B",
                 DragonPalette.GaugeLoop);

            float r3 = gridTop + rowPitch * 3.5f;
            Dial(dl, col1, r3, gr, gth, s, np1, s.NetPwr1Text, "W", "NET PWR 1",
                 DragonPalette.GaugePower);
            Dial(dl, col2, r3, gr, gth, s, np2, s.NetPwr2Text, "W", "NET PWR 2",
                 DragonPalette.GaugePower);

            // ---- BAR READOUTS, RIGHT ----
            // The reference's right-hand column, and its exact list: Inertial Velocity, Altitude,
            // Apogee, Perigee, Inclination, Range to ISS. Every one filled to a fraction of a stated
            // range - see BarScale for where the ranges come from and why they are body-derived.
            float barW = 300f;
            float rx = bx + bw - barW;
            float pitch = (contentBottom - gridTop) / 6f;
            float ry = gridTop + 8f;

            // Velocity goes through the SHARED rule, caption and all - see OrbitReadout.Velocity for
            // why this bar is not allowed to just read s.Velocity like it used to.
            string velCap, velVal;
            double velMps;
            OrbitReadout.Velocity(s, out velCap, out velVal, out velMps);
            Gauge.Bar(dl, rx, ry, barW, velCap, s.Valid ? velVal : "-", null,
                      s.Valid ? BarScale.Velocity(velMps, s.CircularSpeedMps) : -1.0,
                      DragonPalette.BarFill);
            Gauge.Bar(dl, rx, ry + pitch, barW, "ALTITUDE", s.Valid ? s.Altitude : "-", null,
                      s.Valid ? BarScale.Altitude(s.AltitudeM, s.AtmosphereDepthM, s.BodyRadiusM)
                              : -1.0,
                      DragonPalette.BarFill);
            Gauge.Bar(dl, rx, ry + pitch * 2f, barW, "APOGEE",
                      (s.Valid && s.ApogeeShown) ? s.Apoapsis : "-", null,
                      (s.Valid && s.ApogeeShown)
                          ? BarScale.Altitude(s.ApogeeM, s.AtmosphereDepthM, s.BodyRadiusM) : -1.0,
                      DragonPalette.BarFill);
            Gauge.Bar(dl, rx, ry + pitch * 3f, barW, "PERIGEE",
                      (s.Valid && s.PerigeeShown) ? s.Periapsis : "-", null,
                      (s.Valid && s.PerigeeShown)
                          ? BarScale.Altitude(s.PerigeeM, s.AtmosphereDepthM, s.BodyRadiusM) : -1.0,
                      DragonPalette.BarFill);
            Gauge.Bar(dl, rx, ry + pitch * 4f, barW, "INCLINATION",
                      s.Valid ? s.InclinationText : "-", null,
                      s.Valid ? BarScale.Inclination(s.InclinationDeg) : -1.0,
                      DragonPalette.BarFill);
            // "RANGE TO ISS" in the reference. Ours names the actual target, because under stock the
            // target is the Space X Station and under RSS it may really be the ISS - hard-coding the
            // caption would be accurate to the mock and wrong on the glass.
            Gauge.Bar(dl, rx, ry + pitch * 5f, barW,
                      s.HasTarget ? "RANGE TO TARGET" : "RANGE",
                      (s.Valid && s.HasTarget) ? s.RangeText : "-", null,
                      (s.Valid && s.HasTarget) ? BarScale.Range(s.RangeM) : -1.0,
                      DragonPalette.BarFill);

            // ---- STATUS ROW: the alarm channel ----
            Status(dl, bx, bw, contentBottom, s);
        }

        /// <summary>One ring gauge, dashed out when the feed is invalid. Saves eight repetitions.</summary>
        private static void Dial(DisplayList dl, float cx, float cy, float r, float th,
                                 PageState s, double value01, string text, string unit,
                                 string caption, Rgba fill)
        {
            Gauge.Labelled(dl, cx, cy, r, th, s.Valid ? value01 : 0.0, s.Valid ? text : "-",
                           unit, caption, DragonPalette.GaugeTrack, fill);
        }

        /// <summary>
        /// The subsystem status row: a name with a coloured dot, four across the bottom.
        ///
        /// This is the reference's left-hand checklist in the space we have. It is not decoration:
        /// the dials no longer change colour, so this row and the chrome bar are the whole alarm
        /// channel. Both read from Alarms, so a dot and a lit page link can never disagree.
        /// </summary>
        private static void Status(DisplayList dl, float bx, float bw, float top, PageState s)
        {
            float pad = 0f;
            float pitch = bw / 4f;

            // Both dots come from Alarms, so a dot here and a lit page link on the bar can never
            // disagree - they are literally the same function.
            Severity life = Alarms.LifeSupport(s.Cabin);
            Severity thermal = Alarms.Thermal(s.Cabin);

            Dot(dl, bx + pitch * 0f, top + 10f, "LIFE SUPPORT", life, s.Valid);
            Dot(dl, bx + pitch * 1f, top + 10f, "POWER", Alarms.Low(s.Power01), s.Valid);
            Dot(dl, bx + pitch * 2f, top + 10f, "PROPELLANT", Alarms.Low(s.Propellant01),
                s.Valid);
            Dot(dl, bx + pitch * 3f, top + 10f, "THERMAL", thermal, s.Valid);
        }

        private static void Dot(DisplayList dl, float x, float y, string name, Severity sev,
                                bool valid)
        {
            // An INVALID feed is not a nominal one. Grey says "no reading", green says "fine", and
            // the difference is the whole reason this project refuses to draw plausible zeroes.
            Rgba c = valid ? Alarms.Colour(sev) : DragonPalette.Text7;
            dl.Rect(x, y + 2f, 10f, 10f, c);
            dl.Text(name, x + 20f, y, Typography.Caption, TextAlign.Left, DragonPalette.Text4);
            dl.Text(valid ? Alarms.Word(sev) : "NO DATA", x + 20f, y + 18f, Typography.Dense,
                    TextAlign.Left, DragonPalette.Text6);
        }

        /// <summary>
        /// Radius of the ALIGN sweep for a HUD of this size. The attitude ball is sized FROM this,
        /// so the two are one number and cannot be edited apart.
        /// </summary>
        public static float AlignRingRadius(float ringHeight) { return ringHeight * 0.30f; }

        /// <summary>Gap between the attitude ball's edge and the inside of the ALIGN sweep.</summary>
        public const float BallClearance = 22f;

        /// <summary>Diameter the attitude ball gets inside a HUD of this size. Shared with the test.</summary>
        public static float BallDiameter(float ringHeight)
        {
            float d = (AlignRingRadius(ringHeight) - BallClearance) * 2f;
            return (d > 0f) ? d : 0f;
        }

        /// <summary>The HUD ring height for a page of this height. One source, drawing and tests.</summary>
        public static float DockingRingHeight(int h)
        {
            return ((h - ChromeBar.Height) - 24f) * 0.74f;
        }

        /// <summary>
        /// DOCKING - the manual approach HUD.
        ///
        /// Laid out from `build/refart/dashboard_ui_frame_58.png` and the docking export: concentric
        /// rings dead centre with a crosshair, RANGE and RATE beneath them, the X/Y/Z offsets down
        /// the left of the ring, and pitch / yaw / roll around the outside. The rings are the real
        /// artwork (`hud_ring.png`, `hud_ring_inner.png`), not circles we drew.
        ///
        /// ---- NO TARGET IS THE COMMON CASE AND IT MUST NOT LOOK BROKEN ----
        /// A HUD drawn around nothing, with every number dashed, reads as a failed page. When there
        /// is no target this says so in one line and draws nothing else - the honest state, and the
        /// one the crew sees for most of a mission.
        /// </summary>
        private static void Docking(DisplayList dl, int w, int h, PageState s)
        {
            // Laid out from the reference SOURCE - see DockingPage.
            DockingPage.Build(dl, w, h, s);
        }

        private static void DockingOld(DisplayList dl, int w, int h, PageState s)
        {
            float bodyTop = 24f;
            float bodyBottom = h - ChromeBar.Height;
            float cx = w * 0.5f;
            float cy = (bodyTop + bodyBottom) * 0.5f;

            if (!s.Valid || !s.HasTarget)
            {
                dl.Text("DOCKING", cx, cy - 40f, Typography.Hero, TextAlign.Centre,
                        DragonPalette.Text4);
                dl.Text("NO TARGET SELECTED", cx, cy + 20f, Typography.Body, TextAlign.Centre,
                        DragonPalette.Text7);
                return;
            }

            dl.Text(s.TargetName ?? "TARGET", cx, bodyTop, Typography.Body, TextAlign.Centre,
                    DragonPalette.Text1);

            // ---- THE RINGS ----
            float ringH = DockingRingHeight(h);
            float ix, iy, iw, ih;
            if (Images.FitHeight(ImageId.HudRing, cx, cy, ringH, out ix, out iy, out iw, out ih))
                dl.Image(ImageId.HudRing, ix, iy, iw, ih, DragonPalette.Text3);
            if (Images.FitHeight(ImageId.HudRingInner, cx, cy, ringH * 0.62f,
                                 out ix, out iy, out iw, out ih))
                dl.Image(ImageId.HudRingInner, ix, iy, iw, ih, DragonPalette.Text4);

            // ---- THE ATTITUDE BALL ----
            // A real 3D navball rendered by NavBallRenderer into a RenderTexture and drawn here like
            // any other image. Inside the inner ring, so the rings read as a bezel around it.
            //
            // It is a LAYER, not the page: if the renderer failed the image is skipped and the HUD
            // is exactly what it was before, rings and crosshair and all. Same rule as the body map.
            // Square by construction - the render target is 1:1 and the camera's aspect is 1 - so
            // this places it directly rather than going through FitHeight, which exists to preserve
            // an aspect that could be wrong.
            //
            // ---- SIZED OFF THE ALIGN RING, NOT PICKED ----
            // The first version used 0.52 of the ring height, which left about NINE pixels between
            // the ball's edge and the ALIGN sweep at 0.30 radius. In game the two touched and the
            // ball read as misplaced rather than merely tight. Deriving the diameter from the ring
            // it has to sit inside means the two can never drift into each other again, and the
            // headless test asserts the clearance rather than the number.
            float ballD = BallDiameter(ringH);
            dl.Image(ImageId.NavBallLive, cx - ballD * 0.5f, cy - ballD * 0.5f, ballD, ballD,
                     DragonPalette.White);

            // Centre crosshair - two short bars, not a filled dot, so the alignment marker that will
            // sit here later is never hidden behind it. Drawn OVER the ball: it is the boresight, and
            // a boresight the attitude ball can hide is not a boresight.
            dl.Rect(cx - 18f, cy - 1f, 36f, 2f, DragonPalette.Text2);
            dl.Rect(cx - 1f, cy - 18f, 2f, 36f, DragonPalette.Text2);

            // ---- ALIGNMENT RING ----
            // ---- AND WHY THIS ONE IS STILL THRESHOLD-COLOURED ----
            // The identity-colour rule is about a dial that MEASURES something - PPO2 is PPO2 whatever
            // it reads. This is not that: it is a DEVIATION, an error against a target of zero, and
            // its whole content is how bad it is. The reference agrees in spirit, drawing RANGE and
            // RATE in green because they are flying qualities rather than readings.
            Rgba alignColour = Alarms.Colour(Alarms.High(s.Align01));
            float ar = AlignRingRadius(ringH);
            Gauge.Ring(dl, cx, cy, ar, ar * 0.06f, s.Align01,
                       DragonPalette.Inset1, alignColour);

            // ---- RANGE AND RATE, under the rings ----
            // Green in the reference, and it earns it: these are the two numbers a manual approach
            // is actually flown on. RATE goes CAUTION when opening - drifting away during an
            // approach is a condition, not a reading.
            float ry = cy + ringH * 0.40f;
            dl.Text("RANGE", cx - 150f, ry, Typography.Caption, TextAlign.Centre, DragonPalette.Text6);
            dl.Text(s.RangeText ?? "-", cx - 150f, ry + 22f, Typography.Value, TextAlign.Centre,
                    DragonPalette.Go);
            dl.Text("RATE", cx + 150f, ry, Typography.Caption, TextAlign.Centre, DragonPalette.Text6);
            dl.Text(s.RateText ?? "-", cx + 150f, ry + 22f, Typography.Value, TextAlign.Centre,
                    s.ClosingFast ? DragonPalette.Alarm
                                  : s.Closing ? DragonPalette.Go : DragonPalette.Caution);

            // ---- X / Y / Z OFFSETS, left of the ring ----
            float ox = w * 0.10f, oy = cy - 60f;
            dl.Text("OFFSET", ox, oy - 34f, Typography.Caption, TextAlign.Left, DragonPalette.Text6);
            Axis(dl, ox, oy,       "X", s.OffXText);
            Axis(dl, ox, oy + 40f, "Y", s.OffYText);
            Axis(dl, ox, oy + 80f, "Z", s.OffZText);

            // ---- ATTITUDE, right of the ring ----
            float ax = w - w * 0.10f, ay = cy - 60f;
            dl.Text("ATTITUDE", ax, ay - 34f, Typography.Caption, TextAlign.Right,
                    DragonPalette.Text6);
            AxisR(dl, ax, ay,       "PITCH", s.PitchText);
            AxisR(dl, ax, ay + 40f, "YAW",   s.YawText);
            AxisR(dl, ax, ay + 80f, "ROLL",  s.RollText);

            dl.Text("ALIGN", ax, ay + 128f, Typography.Caption, TextAlign.Right, DragonPalette.Text6);
            dl.Text(s.AlignText ?? "-", ax, ay + 150f, Typography.Body, TextAlign.Right,
                    alignColour);
        }

        private static void Axis(DisplayList dl, float x, float y, string name, string value)
        {
            dl.Text(name, x, y, Typography.Caption, TextAlign.Left, DragonPalette.Text5);
            dl.Text(value ?? "-", x + 150f, y, Typography.Body, TextAlign.Right, DragonPalette.Text0);
        }

        private static void AxisR(DisplayList dl, float x, float y, string name, string value)
        {
            dl.Text(name, x - 150f, y, Typography.Caption, TextAlign.Left, DragonPalette.Text5);
            dl.Text(value ?? "-", x, y, Typography.Body, TextAlign.Right, DragonPalette.Text0);
        }

        /// <summary>
        /// A page index with no page behind it. Nothing routes here now that all five are built; it
        /// stays as the honest answer to a bad index, which is a state a malformed save can produce.
        /// </summary>
        private static void Placeholder(DisplayList dl, int w, int h, int pageIndex)
        {
            string name = (pageIndex >= 0 && pageIndex < ChromeBar.PageNames.Length)
                          ? ChromeBar.PageNames[pageIndex] : "?";
            float cx = w * 0.5f;
            float cy = h * 0.42f;

            dl.Text(name, cx, cy - 30f, Typography.Hero, TextAlign.Centre, DragonPalette.Text4);
            dl.Text("NO SUCH PAGE", cx, cy + 24f, Typography.Body, TextAlign.Centre,
                    DragonPalette.Text7);
        }
    }
}
