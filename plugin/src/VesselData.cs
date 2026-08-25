/*
 * DragonScreen - VesselData
 *
 * THE ONE PLACE THAT READS KSP. Turns the active vessel into the pre-formatted strings the pure
 * pages draw, and nothing else in the drawing path touches a KSP type.
 *
 * ---- REFRESHED ONCE PER FRAME, NOT ONCE PER SCREEN ----
 * Three painters call Refresh() every frame. Without a guard that is three identical reads and three
 * sets of formatting for one vessel. Comparing Time.frameCount makes the second and third calls free,
 * and keeps the three screens showing the SAME instant - which matters, because two displays
 * disagreeing about altitude by one frame is exactly the kind of thing that looks like a bug.
 *
 * ---- FORMATTING IS RATE-LIMITED, AND THAT IS A DELIBERATE TRADE ----
 * The project rule is no allocation in the draw path, and string formatting allocates. Rather than
 * change-detect eight fields individually - more code, more places to get a stale value - this
 * reformats at a fixed 5 Hz. That is ~8 short strings 5 times a second for the whole vessel, not per
 * screen and not per frame, and a display updating faster than 5 Hz is not readable anyway.
 *
 * The rule is about the PER-FRAME path in OnPostRender. This is not that path, and the bound is
 * stated rather than assumed.
 *
 * ---- NO VESSEL IS A STATE, NOT AN ERROR ----
 * Valid goes false and the pages draw dashes. They must never draw a plausible zero: a screen
 * confidently reading 0.0 km is indistinguishable from a dead feed, which is the one failure a pilot
 * most needs to be able to see.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    internal static class VesselData
    {
        private const float RefreshInterval = 0.2f;   // 5 Hz

        private static int lastFrame = -1;
        private static float lastFormat = -999f;
        private static PageState state;
        private static string met = "T+ 00:00:00";

        // Charge differencing state. -1 means "no sample yet", which is not the same as zero flow.
        private static double lastCharge = -1.0;
        private static float lastChargeAt = -1f;
        private static double powerFlow;

        // ---- GROUND TRACK ----
        // Allocated ONCE and refilled in place. PageState holds the reference, so a page reads
        // TrackCount entries and never keeps it - see PageState's comment.
        //
        // 90 samples is what makes the dotted track read as a line at zoom 1 without turning into a
        // solid bar at zoom 32, and it is the number the display list is sized around.
        private const int TrackSamples = 90;
        private static readonly double[] trackLat = new double[TrackSamples];
        private static readonly double[] trackLon = new double[TrackSamples];

        /// <summary>
        /// The track is rebuilt at 0.5 Hz, not at the 5 Hz everything else uses.
        ///
        /// Each sample is a Kepler solve; 90 of them five times a second is 450 solves per second for
        /// a curve that visibly changes over minutes. This is the one read in the file with a cost
        /// worth thinking about, so it gets its own clock rather than riding the formatting one.
        /// </summary>
        private const float TrackInterval = 2f;
        private static float lastTrackAt = -999f;

        internal static PageState State { get { return state; } }
        internal static string Met { get { return met; } }

        internal static void Refresh()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;

            if (Time.realtimeSinceStartup - lastFormat < RefreshInterval) return;
            lastFormat = Time.realtimeSinceStartup;

            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.orbit == null)
            {
                state.Valid = false;
                return;
            }

            try
            {
                state.Regime = Regime(v.situation);

                // ACTIVE PHASE is the MISSION phase, not KSP's situation - see MissionPhase.
                MissionInputs mi = new MissionInputs();
                mi.Regime = state.Regime;
                mi.RadarAltitude = v.radarAltitude;
                mi.VerticalSpeed = v.verticalSpeed;
                mi.Docked = (v.situation == Vessel.Situations.DOCKED);
                mi.Splashed = (v.situation == Vessel.Situations.SPLASHED);
                mi.HasTarget = (v.targetObject != null);
                mi.TargetRange = mi.HasTarget
                    ? (v.targetObject.GetTransform().position - v.transform.position).magnitude
                    : 0.0;
                Chutes(v, ref mi);
                MissionPhase phase = Mission.Classify(mi);
                state.Phase = Mission.Name(phase);
                Steps(v, phase, ref state);
                state.Altitude = Km(v.altitude);
                // BOTH speeds are supplied; the PAGE decides which to show, because that is a
                // display policy and display policy is headless tested (see Pages.FlightRegime).
                state.Velocity = Speed(v.obt_speed);
                state.SurfaceVelocity = Speed(v.srfSpeed);
                state.Apoapsis = Km(v.orbit.ApA);
                state.Periapsis = Km(v.orbit.PeA);
                // Decided once, here, where the raw numbers and the body are - both pages then read
                // the same answer. atmosphereDepth scales the perigee test to RSS/RO for free.
                double atmo = (v.mainBody != null) ? v.mainBody.atmosphereDepth : 0.0;
                state.ApogeeShown = OrbitReadout.ApogeeMeaningful(state.Regime);
                state.PerigeeShown = OrbitReadout.PerigeeMeaningful(state.Regime, v.orbit.PeA, atmo);
                state.Body = (v.mainBody != null)
                             ? v.mainBody.bodyName.ToUpperInvariant() : "-";

                // ---- RAW ORBITAL STATE ----
                // The bars need fractions and the orbit plot needs a conic, and neither can be got
                // back out of a formatted string. Ranges are derived in src/pure (BarScale) from
                // these plus the body constants below, so the policy stays testable.
                state.AltitudeM = v.altitude;
                state.ApogeeM = v.orbit.ApA;
                state.PerigeeM = v.orbit.PeA;
                state.VelocityMps = v.obt_speed;
                state.SurfaceVelocityMps = v.srfSpeed;
                state.Ascending = (v.verticalSpeed > 0.0);
                CelestialBody body = v.mainBody;
                state.BodyRadiusM = (body != null) ? body.Radius : 0.0;
                state.AtmosphereDepthM = atmo;
                // Circular orbital speed AT THE SURFACE - a constant of the body, so the velocity bar
                // means the same thing for a whole mission instead of rescaling as you climb.
                state.CircularSpeedMps = (body != null && body.Radius > 0.0)
                    ? Math.Sqrt(body.gravParameter / body.Radius) : 0.0;

                state.InclinationDeg = v.orbit.inclination;
                state.InclinationText = v.orbit.inclination.ToString("F2") + " deg";
                // PERIOD follows the same rule as the apsides: a landed vessel's "orbit" is the
                // degenerate ellipse through the planet, and 9.2 min is a true number for a thing
                // that is not orbiting. Reported on the pad, 2026-08-06 - the third member of the
                // 175 m/s family, and the reason OrbitReadout exists.
                state.PeriodText = state.ApogeeShown ? Period(v.orbit.period) : "-";
                state.TimeToApText = state.ApogeeShown ? Clock(v.orbit.timeToAp) : "-";
                state.TimeToPeText = state.PerigeeShown ? Clock(v.orbit.timeToPe) : "-";

                // ---- SPLASHDOWN TIME ----
                // The reference's top strip carries this and gets it from a real trajectory
                // predictor. We do not have one wired yet (Trajectories is documented in
                // docs/FLIGHT_SYSTEMS.md and not yet called), so this is the honest interim: radar
                // altitude over the current descent rate.
                //
                // THAT MODEL IS ONLY RIGHT AT A STEADY RATE - under the chutes it is good, in free
                // fall it reads long because the vehicle is still accelerating. So it is shown ONLY
                // on an actual descent and dashed the rest of the time, rather than being printed all
                // mission at whatever number the arithmetic happened to produce. Replace it with the
                // predictor, do not tune it.
                bool descending = (v.verticalSpeed < -1.0);
                state.SplashdownShown = descending && state.Regime != FlightRegime.Space
                                        && v.radarAltitude > 0.0;
                state.SplashdownText = state.SplashdownShown
                    ? "T- " + Clock(v.radarAltitude / -v.verticalSpeed) : "-";

                // ---- WHERE WE ARE OVER THE BODY ----
                state.HasFix = (body != null);
                state.Latitude = v.latitude;
                state.Longitude = MapProjection.Wrap180(v.longitude);
                state.LatText = LatLon(v.latitude, "N", "S");
                state.LonText = LatLon(state.Longitude, "E", "W");
                GroundTrack(v);

                // ---- GAUGES ----
                // PROPELLANT follows the ENGINES THAT ARE BURNING, not a fixed resource - see
                // Propellants() and PropellantReadout. Reading MonoPropellant always meant the dial
                // sat at 100% the whole way to orbit.
                Propellants(v);

                double amt, max;
                Resource(v, "ElectricCharge", out amt, out max);
                state.Power01 = (max > 0.0) ? amt / max : 0.0;
                state.PowerText = Percent(state.Power01, max);

                // Charge FLOW, by differencing against the last sample. KSP exposes no net-rate
                // property, and summing every producer and consumer would be a far larger read for
                // a number that only drives two readouts. Guarded against the first sample and
                // against a time step of zero, either of which would divide badly.
                float now = Time.realtimeSinceStartup;
                if (lastCharge >= 0.0 && now > lastChargeAt)
                    powerFlow = (amt - lastCharge) / (now - lastChargeAt);
                lastCharge = amt;
                lastChargeAt = now;

                // Full scale 5 g: the crewed abort limit quoted for SuperDraco is around that, and a
                // nominal entry peaks near 4. A dial scaled to 10 would sit near empty all mission
                // and tell the crew nothing at the moment it matters.
                double gee = v.geeForce;
                state.GForce01 = Clamp01(gee / 5.0);
                state.GForceText = gee.ToString("F1");

                // ---- LIFE SUPPORT ----
                // ppO2 and CO2 are now driven by REAL TAC Life Support state (the Dragon's own O2 supply
                // and CO2 accumulator, isolated from the station when docked - see LifeSupportBridge).
                // Cabin pressure, temperature and the coolant loops TAC does not model, so those stay
                // derived from real hull temperature and the electrical state. See CabinEnvironment.
                CabinInputs ci = new CabinInputs();
                ci.Crew = v.GetCrewCount();
                ci.CrewCapacity = v.GetCrewCapacity();
                ci.HullTempC = HullTempC(v);
                ci.MissionTime = v.missionTime;
                ci.Power01 = state.Power01;
                ci.PowerFlow = powerFlow;
                // Below 1% charge the cabin systems are treated as down, and the readouts degrade
                // rather than freeze - a fake cannot fail convincingly, a model can.
                ci.Powered = (state.Power01 > 0.01);

                LsState ls = LifeSupportBridge.Read(v);
                ci.HasLifeSupport = ls.Present;
                ci.OxygenFrac = ls.Oxygen01;
                ci.Co2Frac = ls.Co201;

                CabinReadout cb = Cabin.Compute(ci);
                state.Cabin = cb;
                state.Ppo2Text      = cb.Ppo2Psia.ToString("F2");
                state.Co2Text       = cb.Co2MmHg.ToString("F2");
                state.PressText     = cb.PressPsia.ToString("F2");
                state.CabinTempText = cb.CabinTempC.ToString("F1");
                state.LoopAText     = cb.LoopAC.ToString("F1");
                state.LoopBText     = cb.LoopBC.ToString("F1");
                state.NetPwr1Text   = cb.NetPwr1W.ToString("F0");
                state.NetPwr2Text   = cb.NetPwr2W.ToString("F0");
                state.CrewText      = ci.Crew + " / " + ci.CrewCapacity;
                Seats(v);
                Pyro(v);
                Acceleration(v);

                // Lights: the vessel's own Light action group, READ every refresh rather than
                // remembered. The crew can also press L, and a settings page that disagreed with the
                // actual state of the cabin lights would be worse than not having the control.
                state.LightsOn = v.ActionGroups[KSPActionGroup.Light];
                // How many light groups the vehicle REALLY has - see SettingsPage on why the
                // reference's eight named zones are not drawn.
                state.LightCount = CountLights(v);
                state.CameraView = cameraView;
                // The vehicle's own cameras, re-read each refresh: they arrive with a part and leave
                // with one, so a remembered list would offer a view of a jettisoned interstage.
                state.CamLabels = HullCams.Labels();
                state.RcsOn = v.ActionGroups[KSPActionGroup.RCS];

                // ---- CONTROL DEMAND, FOR THE DOCKING CORNER RINGS ----
                // FlightCtrlState is what the pilot is actually asking for, whatever the source -
                // keyboard, stick, or an autopilot. The reference lights its rings from key codes;
                // reading the control state instead means the rings are right when MechJeb is flying
                // as well as when a human is, and it is analogue rather than on/off.
                FlightCtrlState c = v.ctrlState;
                if (c != null)
                {
                    state.TransX = c.X; state.TransY = c.Y; state.TransZ = c.Z;
                    state.RotPitch = c.pitch; state.RotYaw = c.yaw; state.RotRoll = c.roll;
                }

                state.Valid = true;

                met = "T+ " + Clock(v.missionTime);
                Docking(v, ref state);
            }
            catch (Exception e)
            {
                // A formatting fault must not take the screens down. Marked invalid so the pages
                // show dashes and say nothing they cannot back up.
                state.Valid = false;
                Debug.LogWarning("[DragonScreen] vessel read failed: " + e.Message);
            }
        }

        /// <summary>
        /// KSP's eight situations, collapsed to the three the display cares about.
        ///
        /// The LANDED | PRELAUNCH | SPLASHED grouping is the conventional one - MAS uses exactly
        /// this set (MASAutoPilot.cs:257). SUB_ORBITAL counts as space: an ascending rocket has a
        /// real trajectory and its apoapsis is the number the pilot is watching.
        /// </summary>
        private static FlightRegime Regime(Vessel.Situations s)
        {
            switch (s)
            {
                case Vessel.Situations.LANDED:
                case Vessel.Situations.PRELAUNCH:
                case Vessel.Situations.SPLASHED:
                    return FlightRegime.Ground;
                case Vessel.Situations.FLYING:
                    return FlightRegime.Atmosphere;
                default:
                    return FlightRegime.Space;
            }
        }

        /// <summary>
        /// Are the drogues or mains out?
        ///
        /// Read from the parachute modules rather than inferred from altitude. The real deploy
        /// altitudes are known (5486 m / 1830 m) and it would be easy to guess from those - and wrong
        /// the moment a chute fails, is cut, or is deployed manually early, which are exactly the
        /// moments the phase readout matters most.
        ///
        /// DROGUE vs MAIN is told apart by the module's own minimum-pressure/altitude settings rather
        /// than by part name: drogues deploy higher. Part names vary between Tundra part variants and
        /// would silently stop matching.
        /// </summary>
        private static void Chutes(Vessel v, ref MissionInputs mi)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part part = v.parts[i];
                for (int m = 0; m < part.Modules.Count; m++)
                {
                    ModuleParachute mp = part.Modules[m] as ModuleParachute;
                    if (mp == null) continue;
                    bool open = (mp.deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED
                              || mp.deploymentState == ModuleParachute.deploymentStates.DEPLOYED);
                    if (!open) continue;
                    if (mp.deployAltitude >= Mission.MainAltitude * 1.5) mi.DroguesOut = true;
                    else mi.MainsOut = true;
                }
            }
        }

        // ------------------------------------------------------------------ FLIGHT's step list

        /// <summary>Crew ticks, and the Max-Q latch. Reset when a new vessel takes the screens.</summary>
        private static int ackMask;
        private static bool maxQPassed;
        private static double peakQ;
        private static uint lastVesselId;

        /// <summary>
        /// The hottest part on the vessel as a fraction of ITS OWN maximum temperature.
        ///
        /// A fraction, not a temperature: parts have wildly different limits, so "900 K" means
        /// nothing on its own while "0.92 of the way to failure" means the same thing everywhere.
        /// Skin and core are both checked because a heat shield fails from the skin in.
        /// </summary>
        private static double HottestPart(Vessel v)
        {
            double worst = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p.maxTemp > 0.0)
                {
                    double f = p.temperature / p.maxTemp;
                    if (f > worst) worst = f;
                }
                if (p.skinMaxTemp > 0.0)
                {
                    double f = p.skinTemperature / p.skinMaxTemp;
                    if (f > worst) worst = f;
                }
            }
            return worst;
        }

        /// <summary>Tick a crew step. Called from the painter when FLIGHT's list is touched.</summary>
        public static void AcknowledgeStep(int id)
        {
            if (id < 0 || id >= (int)StepId.Count) return;
            ackMask = StepList.Acknowledge(ackMask, (StepId)id);
        }

        /// <summary>
        /// Fill everything FLIGHT's sequence list reads.
        ///
        /// ---- MAX Q IS A PEAK, SO IT HAS TO BE LATCHED ----
        /// There is no instant at which the vehicle "is at" max Q - it is the moment dynamic pressure
        /// stops rising. So track the peak and declare it passed once Q has fallen well off it while
        /// genuinely climbing. A threshold on Q alone would fire on the pad, where Q is zero.
        ///
        /// ---- ENGINES ARE COUNTED PER STAGE BY WHAT THEY ARE ATTACHED TO ----
        /// Booster vs second stage is decided by part name, which is how the trunk decoupler is found
        /// too. Tundra's naming is stable across the variants we fly and this is checked against the
        /// live tree, not assumed.
        /// </summary>
        private static void Steps(Vessel v, MissionPhase phase, ref PageState state)
        {
            if (v.persistentId != lastVesselId)
            {
                // A NEW VESSEL IS A NEW COUNTDOWN. Staging, undocking and reverting all produce one,
                // and carrying ticks across would show a checklist completed for a vehicle that never
                // did it.
                lastVesselId = v.persistentId;
                ackMask = 0; maxQPassed = false; peakQ = 0.0;
            }

            // ---- THE SIMULATED SYSTEMS ----
            // Advanced here because this is the one place that already runs once per frame for the
            // whole vehicle. Every trigger it reads is real: charge, the hottest part as a fraction
            // of its OWN limit, and measured g.
            SystemsInputs sy = new SystemsInputs();
            sy.Valid = true;
            sy.Dt = Time.deltaTime * (TimeWarp.CurrentRate > 1f ? TimeWarp.CurrentRate : 1f);
            sy.Crew = v.GetCrewCount();
            sy.Charge01 = state.Power01;
            sy.GForce = v.geeForce;
            sy.HottestPart01 = HottestPart(v);
            FlightCommands.Charge01 = state.Power01;
            Systems.Update(ref FlightCommands.State, sy);
            state.Systems = FlightCommands.State;

            // The FLIGHT-page AUTO SEQUENCE button is the crew-in-the-loop conductor (CrewProcedureOps). It
            // lights while the conductor is running and names the current gate or phase (GO FOR LAUNCH,
            // ASCENT, HOLD - WP0, ...). A bare ascent flown manually from STRING 1A leaves this dark.
            state.AutoEngaged = CrewProcedureOps.Engaged;
            state.AutoPhase = CrewProcedureOps.Engaged ? CrewProcedureOps.PhaseName : null;

            // Crew checklist card: the gate the crew must act on now (GateCard reads these).
            state.GateActive = CrewProcedureOps.CrewActionNeeded();
            if (state.GateActive)
            {
                Gate g = CrewProcedureOps.CurrentGate();
                ProcState pr = CrewProcedureOps.Proc;
                state.GateTitle = g.Title;
                state.GateStage = pr.Phase;
                int n = (g.Items == null) ? 0 : g.Items.Length;
                GateItemView[] views = new GateItemView[n];
                for (int i = 0; i < n; i++)
                {
                    views[i].Label = g.Items[i].Label;
                    views[i].Checked = pr.Satisfied != null && i < pr.Satisfied.Length && pr.Satisfied[i];
                    views[i].CrewActionable = g.Items[i].Kind == ItemKind.CrewAck;
                }
                state.GateItems = views;
            }
            else { state.GateTitle = null; state.GateItems = null; }

            state.RendezvousEngaged = StationApproach.Engaged;
            state.RendezvousNote = StationApproach.Note;
            state.DockEngaged = DockingOps.Engaged;
            state.DockNote = DockingOps.Note;
            state.UndockEngaged = UndockOps.Engaged;
            state.UndockNote = UndockOps.Note;
            state.Docked = DockedSide.Docked(v);

            StepInputs si = new StepInputs();
            si.Valid = true;
            si.Phase = phase;
            si.Crew = v.GetCrewCount();
            si.OnPad = (v.situation == Vessel.Situations.PRELAUNCH);
            si.RadarAltitude = v.radarAltitude;
            si.VerticalSpeed = v.verticalSpeed;
            si.InSpace = (v.mainBody == null) || (v.altitude > v.mainBody.atmosphereDepth);
            si.EscapeArmed = FlightCommands.EscapeArmed;
            si.Acknowledged = ackMask;
            si.Propellant01 = state.Propellant01;
            si.Powered = state.Cabin.NetPwr1W != 0.0 || state.Power01 > 0.0;

            double q = v.dynamicPressurekPa;
            if (q > peakQ) peakQ = q;
            if (!maxQPassed && peakQ > 5.0 && q < peakQ * 0.7 && v.altitude > 10000.0)
                maxQPassed = true;
            si.MaxQPassed = maxQPassed;

            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                // "K1"/"K2"/"MVAC" were PAW titles, not part names, so FLIGHT's step list reported
                // staging that had not happened. See VehicleParts.
                bool booster = VehicleParts.IsBooster(p.name);
                bool second = VehicleParts.IsSecondStage(p.name);

                if (booster) si.BoosterAttached = true;
                if (second) si.S2Attached = true;

                if (p.Modules.Contains<LaunchClamp>()) si.Clamped = true;

                if (!booster && !second) continue;
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleEngines e = p.Modules[m] as ModuleEngines;
                    if (e == null || !e.EngineIgnited || e.finalThrust <= 0.1f) continue;
                    if (booster) si.BoosterLit = true; else si.S2Lit = true;
                }

                // The hinged nose cone, by animation name - same lookup FlightCommands uses.
                List<ModuleAnimateGeneric> an = p.Modules.GetModules<ModuleAnimateGeneric>();
                for (int m = 0; m < an.Count; m++)
                    if (an[m].animationName == "TE_23_CD2_NOSECONE_ANI" && an[m].Progress > 0.5f)
                        si.NoseConeOpen = true;
            }

            state.Steps = si;
        }

        /// <summary>
        /// Relative state to the selected target: range, closing rate, offsets, attitude, alignment.
        ///
        /// ---- SIGN CONVENTION: CLOSING IS NEGATIVE ----
        /// Taken from the reference art, which shows RATE -0.250 m/s while approaching. So rate is
        /// the rate of change of RANGE: negative means the gap is shrinking. Getting this backwards
        /// would be the worst possible bug on this page, because the number would look perfectly
        /// plausible while telling the pilot to do the opposite thing.
        ///
        /// Offsets are in OUR control frame, so they answer "which way do I translate", which is what
        /// the pilot is holding the controls to do.
        /// </summary>
        private static void Docking(Vessel v, ref PageState st)
        {
            ITargetable tgt = v.targetObject;
            st.HasTarget = (tgt != null);
            st.HasTargetGround = false;
            if (!st.HasTarget) return;

            Transform tt = tgt.GetTransform();
            if (tt == null) { st.HasTarget = false; return; }

            st.TargetName = tgt.GetName();

            // Where the target is over the body, for NAV's map marker. Same body as us: a target in
            // another sphere of influence has no meaningful ground position on THIS map, and drawing
            // one would put a confident marker in a place nothing is.
            CelestialBody b = v.mainBody;
            if (b != null && tgt.GetOrbit() != null && tgt.GetOrbit().referenceBody == b)
            {
                st.TargetLat = b.GetLatitude(tt.position);
                st.TargetLon = MapProjection.Wrap180(b.GetLongitude(tt.position));
                st.TargetLatText = LatLon(st.TargetLat, "N", "S");
                st.TargetLonText = LatLon(st.TargetLon, "E", "W");
                st.HasTargetGround = true;
            }

            Vector3d rel = tt.position - v.transform.position;
            double range = rel.magnitude;
            st.RangeM = range;
            st.RangeText = (range >= 1000.0)
                ? (range / 1000.0).ToString("F2") + " km"
                : range.ToString("F1") + " m";

            Vector3d relVel = v.obt_velocity - tgt.GetObtVelocity();
            // d(range)/dt. rel points AT the target, so a velocity toward it shrinks the range.
            double rate = (range > 0.001) ? -Vector3d.Dot(relVel, rel / range) : 0.0;
            st.Closing = (rate < 0.0);
            // The one docking condition worth an alarm: inside 100 m and still closing at more than
            // 2 m/s. Those are the numbers a soft-capture survives, and they are stated here rather
            // than being a colour decision buried in the page.
            st.ClosingFast = (range < 100.0 && rate < -2.0);
            st.RateText = rate.ToString("F2") + " m/s";

            // Our control frame: KSP's reference transform has UP pointing out of the control point,
            // so that is the docking axis and the other two are the translation plane.
            Transform ct = v.ReferenceTransform;
            if (ct != null)
            {
                st.OffXText = Metres(Vector3d.Dot(rel, ct.right));
                st.OffYText = Metres(Vector3d.Dot(rel, ct.forward));
                st.OffZText = Metres(Vector3d.Dot(rel, ct.up));

                double align = Vector3d.Angle(ct.up, rel);
                // Full scale 90 degrees: beyond that the port is not facing the target at all and
                // the exact number stops mattering.
                st.Align01 = Clamp01(align / 90.0);
                st.AlignText = align.ToString("F1") + " deg";
            }

            // PITCH / YAW / ROLL RELATIVE TO THE TARGET, not to the horizon.
            //
            // A first attempt called v.GetPitch()/GetHeading()/GetRoll(). Those do not exist - the
            // compiler caught an invented API, which is the cheapest place to catch one. They would
            // also have been the WRONG numbers: a docking page wants misalignment from the docking
            // axis, not attitude against the local horizon.
            //
            // Derived instead: decompose the line to the target in our control frame. Up is the port
            // axis, so the right and forward components ARE the yaw and pitch errors.
            if (ct != null && range > 0.001)
            {
                Vector3d axis = ct.up;
                st.YawText   = Deg(Math.Atan2(Vector3d.Dot(rel, ct.right),
                                              Vector3d.Dot(rel, axis)) * 180.0 / Math.PI);
                st.PitchText = Deg(Math.Atan2(Vector3d.Dot(rel, ct.forward),
                                              Vector3d.Dot(rel, axis)) * 180.0 / Math.PI);

                // Roll: how far our port is twisted about the docking axis relative to the target's.
                // Both "right" vectors are flattened into the plane perpendicular to the axis first -
                // comparing them unflattened would fold pitch and yaw error into the roll reading.
                Vector3d ourR = Flatten(ct.right, axis);
                Vector3d tgtR = Flatten(tt.right, axis);
                if (ourR.magnitude > 1e-6 && tgtR.magnitude > 1e-6)
                {
                    double roll = Vector3d.Angle(ourR, tgtR);
                    // Signed, so the pilot knows WHICH WAY to roll. An unsigned error tells them
                    // there is a problem and not how to fix it.
                    if (Vector3d.Dot(Vector3d.Cross(ourR, tgtR), axis) < 0.0) roll = -roll;
                    st.RollText = Deg(roll);
                }
                else st.RollText = "-";
            }
        }

        /// <summary>
        /// The ground track: where the vehicle will pass over, in latitude and longitude.
        ///
        /// ---- THE BODY TURNS UNDERNEATH, AND THAT IS THE WHOLE PROBLEM ----
        /// GetLongitude answers "what longitude is this world position, RIGHT NOW". Ask it about a
        /// position the vessel will occupy in twenty minutes and it answers in today's frame, so the
        /// track comes out as a closed loop instead of the westward-marching sine wave a real ground
        /// track is. Subtracting the rotation the body will have done by then is the correction, and
        /// it is the single line that makes this page true rather than decorative.
        ///
        /// LATITUDE NEEDS NO SUCH CORRECTION - the body spins about its poles, so a rotation moves
        /// longitude and nothing else.
        /// </summary>
        private static void GroundTrack(Vessel v)
        {
            CelestialBody b = v.mainBody;
            state.TrackLat = trackLat;
            state.TrackLon = trackLon;

            // On the ground there is no trajectory to draw and orbit.getPositionAtUT describes a
            // degenerate ellipse through the planet - the same artefact that made periapsis read
            // -598.4 km. The marker alone is the honest answer there.
            if (b == null || v.orbit == null || state.Regime == FlightRegime.Ground)
            {
                state.TrackCount = 0;
                return;
            }

            if (Time.realtimeSinceStartup - lastTrackAt < TrackInterval && state.TrackCount > 0)
                return;
            lastTrackAt = Time.realtimeSinceStartup;

            double now = Planetarium.GetUniversalTime();
            double period = v.orbit.period;
            // One full revolution where there is one, capped so a highly elliptical or escaping orbit
            // does not draw a track the crew will never fly. In atmosphere the orbit is notional and
            // fifteen minutes of it is already generous.
            double span = 1800.0;
            if (period > 0.0 && !double.IsNaN(period) && !double.IsInfinity(period))
                span = Math.Min(period, 3.0 * 3600.0);
            if (state.Regime != FlightRegime.Space) span = Math.Min(span, 900.0);

            double rot = b.rotationPeriod;

            try
            {
                for (int i = 0; i < TrackSamples; i++)
                {
                    double dt = span * i / (TrackSamples - 1);
                    Vector3d p = v.orbit.getPositionAtUT(now + dt);
                    double lat = b.GetLatitude(p);
                    double lon = b.GetLongitude(p);
                    if (rot > 0.0) lon -= 360.0 * dt / rot;
                    trackLat[i] = lat;
                    trackLon[i] = MapProjection.Wrap180(lon);
                }
                state.TrackCount = TrackSamples;
            }
            catch (Exception)
            {
                // A propagation fault costs the track, not the page. NAV still draws the grid, the
                // vessel marker and every readout.
                state.TrackCount = 0;
            }
        }

        // Seat names, reused rather than reallocated - this runs at 5 Hz forever.
        private static readonly string[] seatNames = new string[8];

        /// <summary>
        /// Who is in which IVA SEAT.
        ///
        /// ---- SEATS, NOT A CREW LIST ----
        /// v.GetVesselCrew() gives everyone aboard in roster order, which is not where they are
        /// sitting. part.internalModel.seats[i].crew is the seat itself, so seat 2 being empty while
        /// seats 1, 3 and 4 are full comes out correctly - and that is the only reason to draw seats
        /// rather than a number. Technique confirmed against MASFlightComputer.cs:1694-1716, which
        /// builds its localCrew array exactly this way.
        ///
        /// The internal model is null whenever the IVA is not loaded, which is most of the time in
        /// external view - a real state, reported as no seats rather than as an error.
        /// </summary>
        private static void Seats(Vessel v)
        {
            state.SeatNames = seatNames;
            state.SeatCount = 0;
            for (int i = 0; i < seatNames.Length; i++) seatNames[i] = null;

            Part p = SeatPart(v);
            if (p == null || p.internalModel == null) return;

            int n = p.internalModel.seats.Count;
            if (n > seatNames.Length) n = seatNames.Length;
            state.SeatCount = n;
            for (int i = 0; i < n; i++)
            {
                ProtoCrewMember c = p.internalModel.seats[i].crew;
                // First name only: "Jebediah Kerman" does not fit a 78 px cell, and every Kerbal
                // shares the surname anyway, so the part that identifies them is the part we keep.
                if (c != null) seatNames[i] = FirstName(c.name);
            }
        }

        /// <summary>
        /// The part whose IVA these screens live in.
        ///
        /// ---- PREFER THE POD CARRYING OUR OWN MODULE ----
        /// A first attempt asked InternalCamera for the part it was in. That property does not exist
        /// - the compiler caught an invented API, which is the cheapest place to catch one - and it
        /// would have been the wrong question anyway: the card should describe the capsule these
        /// displays are IN, which does not change when the player looks somewhere else.
        ///
        /// DragonScreenState is patched onto exactly the two Dragon pods (see DragonScreen.cfg), so
        /// its presence IS the marker for "this is our capsule". That matters on a docked stack,
        /// where the station may well have crewed parts of its own and the first one in the list
        /// would be arbitrary. Falling back to any crewed part with an interior keeps a sensible
        /// answer if the patch did not land.
        /// </summary>
        private static Part SeatPart(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p.internalModel != null && DragonScreenState.FindOn(p) != null) return p;
            }
            for (int i = 0; i < v.parts.Count; i++)
                if (v.parts[i].internalModel != null && v.parts[i].CrewCapacity > 0)
                    return v.parts[i];
            return null;
        }

        private static string FirstName(string full)
        {
            if (string.IsNullOrEmpty(full)) return null;
            int sp = full.IndexOf(' ');
            return (sp > 0) ? full.Substring(0, sp) : full;
        }

        /// <summary>
        /// Move the IVA view to the crew member in this seat.
        ///
        /// ---- A CONTROL THAT DOES SOMETHING REAL ----
        /// The reference's per-seat page is about audio, which stock KSP does not have; rather than
        /// build a slider bound to nothing, the seat does the thing the game genuinely supports. In a
        /// four-seat capsule where the outer displays are a reach away, being able to move to another
        /// seat from the panel you are already touching is worth having.
        ///
        /// Silently does nothing for an empty seat: there is nobody to look through, and that is not
        /// an error worth a log line every time someone taps an empty chair.
        /// </summary>
        internal static void ViewFromSeat(int seatIndex)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || seatIndex < 0) return;

            Part p = SeatPart(v);
            if (p == null || p.internalModel == null) return;
            if (seatIndex >= p.internalModel.seats.Count) return;

            InternalSeat seat = p.internalModel.seats[seatIndex];
            if (seat == null || seat.kerbalRef == null) return;

            try
            {
                CameraManager.Instance.SetCameraIVA(seat.kerbalRef, false);
                Debug.Log("[DragonScreen] IVA view -> seat " + seatIndex + " ("
                          + seat.kerbalRef.crewMemberName + ")");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] could not move the IVA view: " + e.Message);
            }
        }

        /// <summary>
        /// Independent light groups on the vessel: ModuleLight plus ModuleColorChanger, which is what
        /// this pod actually uses for its cabin lighting.
        /// </summary>
        private static int CountLights(Vessel v)
        {
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    PartModule pm = p.Modules[m];
                    if (pm is ModuleLight || pm is ModuleColorChanger) n++;
                }
            }
            return n;
        }

        /// <summary>
        /// The pyro sequence, for the MECH subview: trunk and chutes.
        ///
        /// ---- ALL OF THIS IS REAL STATE, WHICH IS WHY THE PANEL IS WORTH HAVING ----
        /// A decoupler that has fired reports it, and a ModuleParachute has an explicit deployment
        /// state with a CUT value. During an entry this row is what the crew actually watches, and it
        /// needs no simulation at all.
        ///
        /// TRUNK SET means a trunk decoupler is present and has NOT fired; TRUNK FIRED means it has.
        /// Both false means this vehicle has no trunk to drop - a capsule flying alone - which is a
        /// third state and reads as a dash rather than as a pyro that failed.
        /// </summary>
        private static void Pyro(Vessel v)
        {
            state.TrunkSet = false; state.TrunkFired = false;
            state.DroguesFired = false; state.MainsFired = false; state.MainsReleased = false;

            for (int i = 0; i < v.parts.Count; i++)
            {
                Part part = v.parts[i];
                for (int m = 0; m < part.Modules.Count; m++)
                {
                    PartModule pm = part.Modules[m];

                    ModuleDecouple dec = pm as ModuleDecouple;
                    if (dec != null)
                    {
                        if (dec.isDecoupled) state.TrunkFired = true; else state.TrunkSet = true;
                        continue;
                    }
                    ModuleAnchoredDecoupler anc = pm as ModuleAnchoredDecoupler;
                    if (anc != null)
                    {
                        if (anc.isDecoupled) state.TrunkFired = true; else state.TrunkSet = true;
                        continue;
                    }

                    ModuleParachute mp = pm as ModuleParachute;
                    if (mp == null) continue;

                    bool semi = (mp.deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED);
                    bool full = (mp.deploymentState == ModuleParachute.deploymentStates.DEPLOYED);
                    bool cut  = (mp.deploymentState == ModuleParachute.deploymentStates.CUT);

                    // Drogue vs main by the module's own deploy altitude, not by part name - the same
                    // test Chutes() uses, and for the same reason: names vary between Tundra variants
                    // and would silently stop matching.
                    bool drogue = (mp.deployAltitude >= Mission.MainAltitude * 1.5);
                    if (drogue) { if (semi || full || cut) state.DroguesFired = true; }
                    else
                    {
                        if (semi || full) state.MainsFired = true;
                        if (cut) { state.MainsFired = true; state.MainsReleased = true; }
                    }
                }
            }
        }

        /// <summary>
        /// ACCELERATION, split the way the reference's MECH PANEL splits it.
        ///
        /// POSITIVE and NEGATIVE are the along-axis g-force resolved by sign - thrust versus braking
        /// - rather than one unsigned number, because on a panel watched during entry the direction
        /// is the point. ANGULAR is the body rate. CENTRIPETAL is the orbital term, v^2/r in g, which
        /// is genuinely different from the sensed g the accelerometer reads.
        /// </summary>
        private static void Acceleration(Vessel v)
        {
            double gee = v.geeForce;
            Transform rt = v.ReferenceTransform;
            double along = gee;
            if (rt != null)
            {
                // Sensed acceleration along the control axis. Positive is "pushed into the seat".
                Vector3d acc = v.acceleration;
                along = Vector3d.Dot(acc, rt.up) / 9.80665;
            }
            state.AccelPosText = (along > 0.0) ? along.ToString("F2") : "0.00";
            state.AccelNegText = (along < 0.0) ? (-along).ToString("F2") : "0.00";

            state.AccelAngText = v.angularVelocity.magnitude.ToString("F2");

            double r = v.altitude + ((v.mainBody != null) ? v.mainBody.Radius : 0.0);
            double cent = (r > 1.0) ? (v.obt_speed * v.obt_speed / r) / 9.80665 : 0.0;
            state.AccelCentText = cent.ToString("F3");
        }

        /// <summary>
        /// Set the vessel's Light action group.
        ///
        /// ---- THE ONE PLACE A SCREEN COMMANDS THE VESSEL, SO FAR ----
        /// Called from ScreenPainter.Apply when the SETTINGS control is touched. The optimistic write
        /// to state is only so the button lights on the same frame it is pressed; the next Refresh
        /// reads the action group back, so if something else refuses or reverses it, the display
        /// follows the vessel rather than its own last instruction.
        /// </summary>
        /// <summary>
        /// Which way the VIDEO tab points the live camera. Held here rather than per screen: there
        /// is one camera, so there is one direction, and two displays disagreeing about it would be
        /// a promise the hardware cannot keep.
        /// </summary>
        private static int cameraView;

        internal static int CameraView { get { return cameraView; } }

        internal static void SetCameraView(int view)
        {
            // ⚠ THE UPPER BOUND IS THE VEHICLE'S, NOT A CONSTANT. This read `view > 3` when the
            // four hull-swept directions were all there were; with the vehicle's own cameras
            // appended, a hard 3 would silently swallow every press on a real camera.
            int max = DockingCamRenderer.HullCamBase + HullCams.Count - 1;
            if (view < 0 || view > max) return;
            cameraView = view;
            Debug.Log("[DragonScreen] camera -> " + view + " (" + CameraLabel(view) + ")");
        }

        /// <summary>The name of a view, for the log and the page. Never an index the crew must decode.</summary>
        internal static string CameraLabel(int view)
        {
            if (view < DockingCamRenderer.HullCamBase)
                return (view >= 0 && view < SettingsPage.CamNames.Length)
                     ? SettingsPage.CamNames[view] : "?";
            HullCam hc;
            return HullCams.TryGet(view - DockingCamRenderer.HullCamBase, out hc) ? hc.Label : "?";
        }

        /// <summary>
        /// Drop back to FRONT when the picked camera has left the vehicle.
        ///
        /// ⛔ A VIEW CAN BE JETTISONED MID-FLIGHT. The interstage cameras go with the first stage
        /// and the trunk's go before entry, so a selection that was valid at launch can point at
        /// nothing by the time it matters. Falling back beats a black rectangle the crew has to
        /// diagnose.
        /// </summary>
        internal static void ValidateCameraView()
        {
            int max = DockingCamRenderer.HullCamBase + HullCams.Count - 1;
            if (cameraView <= max) return;
            // ⚠ "FRONT" no longer exists - the computed directions were removed on 2026-08-13.
            // View 0 is now the FIRST REAL CAMERA, and if the vehicle has none there is no picture,
            // which the renderer reports rather than hiding behind a black rectangle.
            Debug.Log("[DragonScreen] camera view " + cameraView + " is gone with its part - "
                      + (HullCams.Count > 0
                         ? "falling back to the first camera on the vehicle"
                         : "and this vehicle has no cameras at all"));
            cameraView = 0;
        }

        internal static void ToggleLights()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;
            bool on = v.ActionGroups[KSPActionGroup.Light];
            v.ActionGroups.SetGroup(KSPActionGroup.Light, !on);
            state.LightsOn = !on;
            Debug.Log("[DragonScreen] lights -> " + (!on ? "ON" : "OFF"));
        }

        /// <summary>Component of v perpendicular to axis. Hand-rolled rather than assuming a helper.</summary>
        private static Vector3d Flatten(Vector3d v, Vector3d axis)
        {
            double n = axis.magnitude;
            if (n < 1e-9) return v;
            Vector3d a = axis / n;
            return v - a * Vector3d.Dot(v, a);
        }

        private static string Metres(double m)
        {
            if (double.IsNaN(m)) return "-";
            return (Math.Abs(m) >= 1000.0)
                ? (m / 1000.0).ToString("F2") + " km" : m.ToString("F1") + " m";
        }

        private static string Deg(double d)
        {
            if (double.IsNaN(d)) return "-";
            return d.ToString("F1") + " deg";
        }

        /// <summary>
        /// Hull temperature in Celsius, taken from the HOTTEST part.
        ///
        /// The hottest part is the one the crew would care about and the one that actually drives
        /// the thermal loops - averaging would smear an entry heat pulse into nothing. KSP stores
        /// temperature in Kelvin.
        /// </summary>
        private static double HullTempC(Vessel v)
        {
            double hottest = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                double t = v.parts[i].temperature;
                if (t > hottest) hottest = t;
            }
            return hottest - 273.15;
        }

        // Propellant scratch, allocated once - this runs at 5 Hz forever.
        private static readonly int[] propId = new int[PropellantReadout.MaxSources];
        private static readonly string[] propName = new string[PropellantReadout.MaxSources];
        private static readonly double[] propFrac = new double[PropellantReadout.MaxSources];

        /// <summary>
        /// What the PROPELLANT gauge should read: the tanks feeding the engines that are running.
        ///
        /// ---- LIT ENGINES FIRST, ALL ENGINES AS THE FALLBACK ----
        /// While something is burning, that is unambiguously the propellant the crew cares about.
        /// With nothing lit - on the pad, or coasting - the useful answer is the stage you are about
        /// to burn, and "every engine still attached" is a good enough stand-in for that without
        /// needing to reason about staging: once the booster is gone its engines are on another
        /// vessel, so the set narrows by itself.
        ///
        /// GetConsumedResources() rather than the propellants list, matching
        /// MASVesselComputerModules.cs:530 - it hands back the definitions, so density and name come
        /// with it and there is no second lookup to get wrong.
        /// </summary>
        private static void Propellants(Vessel v)
        {
            int n = CollectPropellants(v, true);
            if (n == 0) n = CollectPropellants(v, false);

            for (int i = 0; i < n; i++)
            {
                double amt, max;
                ResourceById(v, propId[i], out amt, out max);
                propFrac[i] = (max > 0.0) ? amt / max : 0.0;
            }

            double frac = PropellantReadout.Fraction(propFrac, n);
            state.PropellantCaption = PropellantReadout.Caption(propName, n);

            // A vehicle with no engines at all - a coasting capsule that has spent its Dracos, or a
            // probe - reads a dash, not a zero. "Not fitted" and "you are out" are different.
            if (frac < 0.0)
            {
                state.Propellant01 = 0.0;
                state.PropellantText = "-";
            }
            else
            {
                state.Propellant01 = frac;
                state.PropellantText = (frac * 100.0).ToString("F0");
            }
        }

        /// <summary>
        /// Fill propId/propName with the distinct resources consumed by this vessel's engines.
        /// Returns how many. <paramref name="litOnly"/> restricts it to engines actually burning.
        /// </summary>
        private static int CollectPropellants(Vessel v, bool litOnly)
        {
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part part = v.parts[i];
                for (int m = 0; m < part.Modules.Count; m++)
                {
                    // ModuleEnginesFX derives from ModuleEngines, so this catches both.
                    ModuleEngines me = part.Modules[m] as ModuleEngines;
                    if (me == null) continue;
                    if (litOnly && !(me.EngineIgnited && me.isEnabled && me.isOperational)) continue;

                    List<PartResourceDefinition> consumed = me.GetConsumedResources();
                    if (consumed == null) continue;
                    for (int r = 0; r < consumed.Count; r++)
                    {
                        PartResourceDefinition def = consumed[r];
                        if (def == null) continue;
                        // MASSLESS RESOURCES ARE NOT PROPELLANT. ElectricCharge and IntakeAir come
                        // back from GetConsumedResources for ion and jet engines, and neither is a
                        // tank the crew can run dry in any way this gauge should report.
                        if (def.density <= 0f) continue;

                        bool seen = false;
                        for (int k = 0; k < n; k++) if (propId[k] == def.id) { seen = true; break; }
                        if (seen) continue;

                        if (n >= propId.Length) return n;
                        propId[n] = def.id;
                        propName[n] = def.name;
                        n++;
                    }
                }
            }
            return n;
        }

        /// <summary>
        /// Connected total for a resource by ID.
        ///
        /// Falls back to pulling=false when the flow-restricted query finds no capacity at all.
        /// SolidFuel is NO_FLOW - it cannot be drawn from another part - so the crossfeed-respecting
        /// query legitimately reports nothing at vessel scope, and a booster reading "no propellant"
        /// while it is thrusting would be a worse lie than ignoring the flow rules for the readout.
        /// </summary>
        private static void ResourceById(Vessel v, int id, out double amount, out double max)
        {
            v.GetConnectedResourceTotals(id, out amount, out max, true);
            if (max <= 0.0) v.GetConnectedResourceTotals(id, out amount, out max, false);
        }

        /// <summary>
        /// Vessel total of a resource by name, across everything currently connected.
        ///
        /// GetConnectedResourceTotals with pulling=true, matching MAS's usage - it respects crossfeed
        /// and flow rules, so a tank that cannot actually be drawn from is not counted as available.
        /// Summing part resources by hand would over-report exactly when it matters.
        /// </summary>
        private static void Resource(Vessel v, string name, out double amount, out double max)
        {
            amount = 0.0; max = 0.0;
            PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(name);
            if (def == null) return;
            v.GetConnectedResourceTotals(def.id, out amount, out max, true);
        }

        /// <summary>
        /// A percentage, or a dash when the vessel simply has none of that resource.
        ///
        /// ZERO CAPACITY IS NOT ZERO PERCENT. A capsule with no monopropellant tank at all should
        /// read "-", not "0%" - the first says "not fitted", the second says "you are out", and
        /// confusing them during an approach would be serious.
        /// </summary>
        private static string Percent(double frac, double max)
        {
            if (max <= 0.0) return "-";
            return (frac * 100.0).ToString("F0");
        }

        private static double Clamp01(double v)
        {
            if (double.IsNaN(v)) return 0.0;
            return (v < 0.0) ? 0.0 : (v > 1.0) ? 1.0 : v;
        }

        /// <summary>
        /// Speed with a sensible unit. Above 1 km/s the metres are noise on a glance-read display,
        /// and an orbital velocity of "2280 m/s" is harder to take in than "2.28 km/s".
        /// </summary>
        private static string Speed(double mps)
        {
            if (double.IsNaN(mps) || double.IsInfinity(mps)) return "-";
            if (Math.Abs(mps) >= 1000.0) return (mps / 1000.0).ToString("F2") + " km/s";
            return mps.ToString("F0") + " m/s";
        }

        /// <summary>
        /// Altitudes in km with one decimal. Below 10 km it switches to metres, because "0.4 km" is
        /// a worse number than "412 m" at exactly the moment altitude matters most - the last
        /// kilometre of a landing.
        /// </summary>
        private static string Km(double metres)
        {
            if (double.IsNaN(metres) || double.IsInfinity(metres)) return "-";
            if (Math.Abs(metres) < 10000.0) return metres.ToString("F0") + " m";
            return (metres / 1000.0).ToString("F1") + " km";
        }

        /// <summary>
        /// A coordinate as degrees plus a hemisphere letter, never a signed number.
        ///
        /// "-51.64 deg" makes the reader do the conversion; "51.64 S" does not, and a hemisphere is
        /// the thing most likely to be misread at a glance during an entry.
        /// </summary>
        private static string LatLon(double deg, string pos, string neg)
        {
            if (double.IsNaN(deg)) return "-";
            string hemi = (deg >= 0.0) ? pos : neg;
            double a = (deg < 0.0) ? -deg : deg;
            return a.ToString("F2") + " " + hemi;
        }

        /// <summary>Orbital period. Minutes below two hours, because that is how it is talked about.</summary>
        private static string Period(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0.0) return "-";
            if (seconds < 7200.0) return (seconds / 60.0).ToString("F1") + " min";
            return Clock(seconds);
        }

        /// <summary>HH:MM:SS. Days are not shown yet; the chrome bar has no room for them.</summary>
        private static string Clock(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0.0) seconds = 0.0;
            int t = (int)seconds;
            int hh = t / 3600, mm = (t / 60) % 60, ss = t % 60;
            return hh.ToString("00") + ":" + mm.ToString("00") + ":" + ss.ToString("00");
        }
    }
}
