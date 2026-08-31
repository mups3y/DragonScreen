// DragonScreen - VesselData
// ---- REFRESHED ONCE PER FRAME, NOT ONCE PER SCREEN ----
// ---- FORMATTING IS RATE-LIMITED, AND THAT IS A DELIBERATE TRADE ----
// ---- NO VESSEL IS A STATE, NOT AN ERROR ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    internal static class VesselData
    {
        private const float RefreshInterval = 0.2f;

        private static int lastFrame = -1;
        private static float lastFormat = -999f;
        private static PageState state;
        private static string met = "T+ 00:00:00";

        private static double lastCharge = -1.0;
        private static float lastChargeAt = -1f;
        private static double powerFlow;

        // ---- GROUND TRACK ----
        private const int TrackSamples = 90;
        private static readonly double[] trackLat = new double[TrackSamples];
        private static readonly double[] trackLon = new double[TrackSamples];
        // Radius as a multiple of the body radius, per sample - lets the 3D globe view FLOAT the orbit
        // above the surface (a ground track is ratio 1). Filled alongside lat/lon in GroundTrack.
        private static readonly double[] trackRatio = new double[TrackSamples];

        // The target's own orbit track, for the 3D globe view. Same layout, rotation-corrected.
        private static readonly double[] tgtTrackLat = new double[TrackSamples];
        private static readonly double[] tgtTrackLon = new double[TrackSamples];
        private static readonly double[] tgtTrackRatio = new double[TrackSamples];
        private static int tgtTrackCount;

        private const float TrackInterval = 2f;
        private static float lastTrackAt = -999f;

        // The 3D globe overlay: refs to the buffers above + the point markers. Reused every frame.
        private static readonly PlanetOverlay planetOverlay = new PlanetOverlay();

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
                // ⭐ U1: the orbit is CLOSED once periapsis clears the atmosphere (a self-sustaining orbit). Below
                // that, an in-space targeted vehicle is still on its ascent/insertion burn, not phasing.
                mi.OrbitClosed = v.mainBody != null && v.orbit.PeA > v.mainBody.atmosphereDepth;
                Chutes(v, ref mi);
                MissionPhase classified = Mission.Classify(mi);
                // ⛔ ONE AUTHORITATIVE PHASE (rule T4): while the autopilot is FLYING a known phase, the phase
                // WORD is the mission FSM's ActivePhase — never the independent classifier — so the screen can
                // never disagree with the autopilot. Disengaged / at a gate → the live classifier is the fallback.
                MissionPhase phase = Mission.AuthoritativePhase(CrewProcedureOps.Engaged, CrewProcedureOps.ActivePhase, classified);
                state.Phase = Mission.Name(phase);
                Steps(v, classified, ref state);
                state.Altitude = Km(v.altitude);
                state.Velocity = Speed(v.obt_speed);
                state.SurfaceVelocity = Speed(v.srfSpeed);
                state.Apoapsis = Km(v.orbit.ApA);
                state.Periapsis = Km(v.orbit.PeA);
                double atmo = (v.mainBody != null) ? v.mainBody.atmosphereDepth : 0.0;
                state.ApogeeShown = OrbitReadout.ApogeeMeaningful(state.Regime);
                state.PerigeeShown = OrbitReadout.PerigeeMeaningful(state.Regime, v.orbit.PeA, atmo);
                state.Body = (v.mainBody != null)
                             ? v.mainBody.bodyName.ToUpperInvariant() : "-";

                // ---- RAW ORBITAL STATE ----
                state.AltitudeM = v.altitude;
                state.ApogeeM = v.orbit.ApA;
                state.PerigeeM = v.orbit.PeA;
                state.VelocityMps = v.obt_speed;
                state.SurfaceVelocityMps = v.srfSpeed;
                state.Ascending = (v.verticalSpeed > 0.0);
                CelestialBody body = v.mainBody;
                state.BodyRadiusM = (body != null) ? body.Radius : 0.0;
                state.AtmosphereDepthM = atmo;
                state.CircularSpeedMps = (body != null && body.Radius > 0.0)
                    ? Math.Sqrt(body.gravParameter / body.Radius) : 0.0;

                state.InclinationDeg = v.orbit.inclination;
                state.InclinationText = v.orbit.inclination.ToString("F2") + " deg";
                state.PeriodText = state.ApogeeShown ? Period(v.orbit.period) : "-";
                state.TimeToApText = state.ApogeeShown ? Clock(v.orbit.timeToAp) : "-";
                state.TimeToPeText = state.PerigeeShown ? Clock(v.orbit.timeToPe) : "-";

                // ---- SPLASHDOWN TIME ----
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
                PlanetOverlayFill(v);

                // ---- GAUGES ----
                Propellants(v);

                double amt, max;
                Resource(v, "ElectricCharge", out amt, out max);
                state.Power01 = (max > 0.0) ? amt / max : 0.0;
                state.PowerText = Percent(state.Power01, max);

                float now = Time.realtimeSinceStartup;
                if (lastCharge >= 0.0 && now > lastChargeAt)
                    powerFlow = (amt - lastCharge) / (now - lastChargeAt);
                lastCharge = amt;
                lastChargeAt = now;

                double gee = v.geeForce;
                state.GForce01 = Clamp01(gee / 5.0);
                state.GForceText = gee.ToString("F1");

                // ---- LIFE SUPPORT ----
                CabinInputs ci = new CabinInputs();
                ci.Crew = v.GetCrewCount();
                ci.CrewCapacity = v.GetCrewCapacity();
                ci.HullTempC = HullTempC(v);
                ci.MissionTime = v.missionTime;
                ci.Power01 = state.Power01;
                ci.PowerFlow = powerFlow;
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

                state.LightsOn = v.ActionGroups[KSPActionGroup.Light];
                state.LightCount = CountLights(v);
                state.CameraView = cameraView;
                state.CamLabels = HullCams.Labels();
                state.RcsOn = v.ActionGroups[KSPActionGroup.RCS];

                // ---- CONTROL DEMAND, FOR THE DOCKING CORNER RINGS ----
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
                state.Valid = false;
                Debug.LogWarning("[DragonScreen] vessel read failed: " + e.Message);
            }
        }

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

        private static int ackMask;
        private static bool maxQPassed;
        private static double peakQ;
        private static uint lastVesselId;

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

        public static void AcknowledgeStep(int id)
        {
            if (id < 0 || id >= (int)StepId.Count) return;
            ackMask = StepList.Acknowledge(ackMask, (StepId)id);
        }

        /// ---- MAX Q IS A PEAK, SO IT HAS TO BE LATCHED ----
        /// ---- ENGINES ARE COUNTED PER STAGE BY WHAT THEY ARE ATTACHED TO ----
        private static void Steps(Vessel v, MissionPhase phase, ref PageState state)
        {
            if (v.persistentId != lastVesselId)
            {
                lastVesselId = v.persistentId;
                ackMask = 0; maxQPassed = false; peakQ = 0.0;
            }

            // ---- THE SIMULATED SYSTEMS ----
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

            state.AutoEngaged = CrewProcedureOps.Engaged;
            state.AutoPhase = CrewProcedureOps.Engaged ? CrewProcedureOps.PhaseName : null;

            // ⛔ C6 (automation must be visible): the crew-facing control authority, straight from the single
            // AuthorityManager (Phase 2) — AUTO while the autopilot flies, MANUAL/ABORT/RECOVERY/IDLE otherwise.
            state.Mode = FlightDriver.MissionMode;
            state.ModeText = AuthorityManager.Name(state.Mode);

            // ⛔ FDIR → the crew alert channel (§4.2): the authoritative fault spine, published by FlightDriver.
            // The screen's STATE severity now folds this in (Alarms.SystemSeverity) instead of inventing alerts.
            FdirReport fdir = FlightDriver.LastFdirReport;
            state.Fault = fdir.Fault;
            state.FaultResponse = fdir.Response;
            state.FaultText = Fdir.FaultName(fdir.Fault);

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

                List<ModuleAnimateGeneric> an = p.Modules.GetModules<ModuleAnimateGeneric>();
                for (int m = 0; m < an.Count; m++)
                    if (an[m].animationName == "TE_23_CD2_NOSECONE_ANI" && an[m].Progress > 0.5f)
                        si.NoseConeOpen = true;
            }

            state.Steps = si;
        }

        /// ---- SIGN CONVENTION: CLOSING IS NEGATIVE ----
        private static void Docking(Vessel v, ref PageState st)
        {
            ITargetable tgt = v.targetObject;
            st.HasTarget = (tgt != null);
            st.HasTargetGround = false;
            if (!st.HasTarget) return;

            Transform tt = tgt.GetTransform();
            if (tt == null) { st.HasTarget = false; return; }

            st.TargetName = tgt.GetName();

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
            double rate = (range > 0.001) ? -Vector3d.Dot(relVel, rel / range) : 0.0;
            st.Closing = (rate < 0.0);
            st.ClosingFast = (range < 100.0 && rate < -2.0);
            st.RateText = rate.ToString("F2") + " m/s";

            // Body angular RATES (deg/s) — the BLUE rate under each GREEN correction in the docking
            // two-number scheme. Published here (rule T3: the docking page showed "—" because nothing
            // upstream supplied them) from vessel.angularVelocity, axes pitch=x / roll=y / yaw=z, the
            // same convention FlightLog/FlightRecorder use. Independent of the target, but the page only
            // reads them when a target is present, so they ride in this block.
            Vector3 avDps = v.angularVelocity * Mathf.Rad2Deg;
            st.PitchRateText = DegRate(avDps.x);
            st.RollRateText  = DegRate(avDps.y);
            st.YawRateText   = DegRate(avDps.z);

            Transform ct = v.ReferenceTransform;
            if (ct != null)
            {
                st.OffXText = Metres(Vector3d.Dot(rel, ct.right));
                st.OffYText = Metres(Vector3d.Dot(rel, ct.forward));
                st.OffZText = Metres(Vector3d.Dot(rel, ct.up));

                double align = Vector3d.Angle(ct.up, rel);
                st.Align01 = Clamp01(align / 90.0);
                st.AlignText = align.ToString("F1") + " deg";
            }

            if (ct != null && range > 0.001)
            {
                Vector3d axis = ct.up;
                st.YawText   = Deg(Math.Atan2(Vector3d.Dot(rel, ct.right),
                                              Vector3d.Dot(rel, axis)) * 180.0 / Math.PI);
                st.PitchText = Deg(Math.Atan2(Vector3d.Dot(rel, ct.forward),
                                              Vector3d.Dot(rel, axis)) * 180.0 / Math.PI);

                Vector3d ourR = Flatten(ct.right, axis);
                Vector3d tgtR = Flatten(tt.right, axis);
                if (ourR.magnitude > 1e-6 && tgtR.magnitude > 1e-6)
                {
                    double roll = Vector3d.Angle(ourR, tgtR);
                    if (Vector3d.Dot(Vector3d.Cross(ourR, tgtR), axis) < 0.0) roll = -roll;
                    st.RollText = Deg(roll);
                }
                else st.RollText = "-";
            }
        }

        /// ---- THE BODY TURNS UNDERNEATH, AND THAT IS THE WHOLE PROBLEM ----
        private static void GroundTrack(Vessel v)
        {
            CelestialBody b = v.mainBody;
            state.TrackLat = trackLat;
            state.TrackLon = trackLon;

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
            double span = 1800.0;
            if (period > 0.0 && !double.IsNaN(period) && !double.IsInfinity(period))
                span = Math.Min(period, 3.0 * 3600.0);
            if (state.Regime != FlightRegime.Space) span = Math.Min(span, 900.0);

            double rot = b.rotationPeriod;
            double invR = (b.Radius > 0.0) ? 1.0 / b.Radius : 0.0;

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
                    trackRatio[i] = (p - b.position).magnitude * invR;   // for the 3D globe float
                }
                state.TrackCount = TrackSamples;
            }
            catch (Exception)
            {
                state.TrackCount = 0;
            }

            // ---- the TARGET's own orbit track, for the 3D globe (one full period) ----
            tgtTrackCount = 0;
            ITargetable tgt = v.targetObject;
            Orbit to = (tgt != null) ? tgt.GetOrbit() : null;
            if (to != null && to.referenceBody == b)
            {
                double tp = to.period;
                double tspan = (tp > 0.0 && !double.IsNaN(tp) && !double.IsInfinity(tp)) ? tp : 5400.0;
                try
                {
                    for (int i = 0; i < TrackSamples; i++)
                    {
                        double dt = tspan * i / (TrackSamples - 1);
                        Vector3d p = to.getPositionAtUT(now + dt);
                        double lon = b.GetLongitude(p);
                        if (rot > 0.0) lon -= 360.0 * dt / rot;
                        tgtTrackLat[i] = b.GetLatitude(p);
                        tgtTrackLon[i] = MapProjection.Wrap180(lon);
                        tgtTrackRatio[i] = (p - b.position).magnitude * invR;
                    }
                    tgtTrackCount = TrackSamples;
                }
                catch (Exception) { tgtTrackCount = 0; }
            }
        }

        /// <summary>
        /// Fill the 3D globe overlay (PlanetOverlay) - the orbit/target tracks (shared refs to the
        /// buffers above) plus the vessel/apsis/target markers, all as body-fixed (lat, lon, ratio).
        /// The tracks are refreshed on the GroundTrack cadence; the four markers are recomputed every
        /// frame (cheap) so they slide smoothly. NavPage projects them onto the globe.
        /// </summary>
        private static void PlanetOverlayFill(Vessel v)
        {
            planetOverlay.Reset();
            state.Planet = planetOverlay;

            CelestialBody b = v.mainBody;
            if (b == null || b.Radius <= 0.0 || v.orbit == null) return;
            double invR = 1.0 / b.Radius;

            planetOverlay.OrbitLat = trackLat;
            planetOverlay.OrbitLon = trackLon;
            planetOverlay.OrbitRatio = trackRatio;
            planetOverlay.OrbitCount = state.TrackCount;
            planetOverlay.OnSurface = (state.TrackCount == 0);
            planetOverlay.Ready = true;

            planetOverlay.Vessel = new GlobePoint
            {
                Lat = state.Latitude,
                Lon = state.Longitude,
                Ratio = (b.Radius + state.AltitudeM) * invR,
                Has = state.HasFix
            };

            double now = Planetarium.GetUniversalTime();
            double rot = b.rotationPeriod;
            if (state.ApogeeShown) planetOverlay.Ap = ArcMarker(v.orbit, now, v.orbit.timeToAp, b, rot, invR);
            if (state.PerigeeShown) planetOverlay.Pe = ArcMarker(v.orbit, now, v.orbit.timeToPe, b, rot, invR);

            ITargetable tgt = v.targetObject;
            Orbit to = (tgt != null) ? tgt.GetOrbit() : null;
            if (to != null && to.referenceBody == b)
            {
                planetOverlay.Target = ArcMarker(to, now, 0.0, b, rot, invR);
                if (tgtTrackCount > 1)
                {
                    planetOverlay.TgtLat = tgtTrackLat;
                    planetOverlay.TgtLon = tgtTrackLon;
                    planetOverlay.TgtRatio = tgtTrackRatio;
                    planetOverlay.TgtCount = tgtTrackCount;
                }
            }
        }

        /// <summary>One globe marker from an orbit at now+dt: body-fixed lat/lon (rotation-corrected)
        /// and radius ratio.</summary>
        private static GlobePoint ArcMarker(Orbit o, double now, double dt, CelestialBody b,
                                            double rot, double invR)
        {
            GlobePoint g = new GlobePoint();
            if (o == null || double.IsNaN(dt) || double.IsInfinity(dt)) return g;
            try
            {
                Vector3d p = o.getPositionAtUT(now + dt);
                double lon = b.GetLongitude(p);
                if (rot > 0.0) lon -= 360.0 * dt / rot;
                g.Lat = b.GetLatitude(p);
                g.Lon = MapProjection.Wrap180(lon);
                g.Ratio = (p - b.position).magnitude * invR;
                g.Has = true;
            }
            catch (Exception) { g.Has = false; }
            return g;
        }

        private static readonly string[] seatNames = new string[8];

        /// ---- SEATS, NOT A CREW LIST ----
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
                if (c != null) seatNames[i] = FirstName(c.name);
            }
        }

        /// ---- PREFER THE POD CARRYING OUR OWN MODULE ----
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

        /// ---- A CONTROL THAT DOES SOMETHING REAL ----
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

        /// ---- ALL OF THIS IS REAL STATE, WHICH IS WHY THE PANEL IS WORTH HAVING ----
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

        private static void Acceleration(Vessel v)
        {
            double gee = v.geeForce;
            Transform rt = v.ReferenceTransform;
            double along = gee;
            if (rt != null)
            {
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

        /// ---- THE ONE PLACE A SCREEN COMMANDS THE VESSEL, SO FAR ----
        private static int cameraView;

        internal static int CameraView { get { return cameraView; } }

        internal static void SetCameraView(int view)
        {
            int max = DockingCamRenderer.HullCamBase + HullCams.Count - 1;
            if (view < 0 || view > max) return;
            cameraView = view;
            Debug.Log("[DragonScreen] camera -> " + view + " (" + CameraLabel(view) + ")");
        }

        internal static string CameraLabel(int view)
        {
            if (view < DockingCamRenderer.HullCamBase)
                return (view >= 0 && view < SettingsPage.CamNames.Length)
                     ? SettingsPage.CamNames[view] : "?";
            HullCam hc;
            return HullCams.TryGet(view - DockingCamRenderer.HullCamBase, out hc) ? hc.Label : "?";
        }

        internal static void ValidateCameraView()
        {
            int max = DockingCamRenderer.HullCamBase + HullCams.Count - 1;
            if (cameraView <= max) return;
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

        private static string DegRate(double dps)
        {
            if (double.IsNaN(dps) || double.IsInfinity(dps)) return "-";
            return dps.ToString("F1") + " deg/s";
        }

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

        private static readonly int[] propId = new int[PropellantReadout.MaxSources];
        private static readonly string[] propName = new string[PropellantReadout.MaxSources];
        private static readonly double[] propFrac = new double[PropellantReadout.MaxSources];

        /// ---- LIT ENGINES FIRST, ALL ENGINES AS THE FALLBACK ----
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

        private static int CollectPropellants(Vessel v, bool litOnly)
        {
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part part = v.parts[i];
                for (int m = 0; m < part.Modules.Count; m++)
                {
                    ModuleEngines me = part.Modules[m] as ModuleEngines;
                    if (me == null) continue;
                    if (litOnly && !(me.EngineIgnited && me.isEnabled && me.isOperational)) continue;

                    List<PartResourceDefinition> consumed = me.GetConsumedResources();
                    if (consumed == null) continue;
                    for (int r = 0; r < consumed.Count; r++)
                    {
                        PartResourceDefinition def = consumed[r];
                        if (def == null) continue;
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

        private static void ResourceById(Vessel v, int id, out double amount, out double max)
        {
            v.GetConnectedResourceTotals(id, out amount, out max, true);
            if (max <= 0.0) v.GetConnectedResourceTotals(id, out amount, out max, false);
        }

        private static void Resource(Vessel v, string name, out double amount, out double max)
        {
            amount = 0.0; max = 0.0;
            PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition(name);
            if (def == null) return;
            v.GetConnectedResourceTotals(def.id, out amount, out max, true);
        }

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

        private static string Speed(double mps)
        {
            if (double.IsNaN(mps) || double.IsInfinity(mps)) return "-";
            if (Math.Abs(mps) >= 1000.0) return (mps / 1000.0).ToString("F2") + " km/s";
            return mps.ToString("F0") + " m/s";
        }

        private static string Km(double metres)
        {
            if (double.IsNaN(metres) || double.IsInfinity(metres)) return "-";
            if (Math.Abs(metres) < 10000.0) return metres.ToString("F0") + " m";
            return (metres / 1000.0).ToString("F1") + " km";
        }

        private static string LatLon(double deg, string pos, string neg)
        {
            if (double.IsNaN(deg)) return "-";
            string hemi = (deg >= 0.0) ? pos : neg;
            double a = (deg < 0.0) ? -deg : deg;
            return a.ToString("F2") + " " + hemi;
        }

        private static string Period(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0.0) return "-";
            if (seconds < 7200.0) return (seconds / 60.0).ToString("F1") + " min";
            return Clock(seconds);
        }

        private static string Clock(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0.0) seconds = 0.0;
            int t = (int)seconds;
            int hh = t / 3600, mm = (t / 60) % 60, ss = t % 60;
            return hh.ToString("00") + ":" + mm.ToString("00") + ":" + ss.ToString("00");
        }
    }
}
