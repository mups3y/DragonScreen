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
        private static double lastChargeAt = -1.0;
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
                // No vessel means no scaled-space camera to be watching either (S10) - and a stale
                // true here would let the 3D view claim LIVE CAMERA over a frozen last frame.
                state.PlanetCamLive = false;
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
                // T13c: the same number in the glyph form the reference's top strip prints. Formatted
                // here, beside its twin, so the two renderings come off one value (Pages.InclinationDegText).
                state.InclinationDegText = v.orbit.inclination.ToString("F2") + "°";
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

                // S6: this used to clock itself off Time.realtimeSinceStartup (wall-clock, keeps
                // ticking while KSP is paused). OnPostRender - and so Refresh() - fires every paused
                // frame too, so a paused session kept recomputing (amt - lastCharge) / (real dt) with
                // amt frozen and dt growing: an exact 0 W flow, every dial, every screenshot taken
                // while paused. v.missionTime is simulation time - it does not advance while paused,
                // so the guard below now holds the last REAL reading instead of overwriting it with a
                // pause artifact.
                double now = v.missionTime;
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
                double hullC = HullTempC(v);
                ci.HullTempC = hullC;
                // THERMAL tab (T13b). One reading, two formats: the SHIELD gauge puts its unit on its own
                // line, the "TPS Max" row prints one string. Formatted side by side so they cannot drift.
                string hullText = hullC.ToString("F0");
                state.HullTempText = hullText;
                state.TpsMaxText   = hullText + " °C";
                ci.MissionTime = v.missionTime;
                ci.Power01 = state.Power01;
                ci.PowerFlow = powerFlow;
                ci.Powered = (state.Power01 > 0.01);

                LsState ls = LifeSupportBridge.Read(v);
                ci.HasLifeSupport = ls.Present;
                ci.OxygenFrac = ls.Oxygen01;
                ci.Co2Frac = ls.Co201;
                // CREW tab (T13b): potable water is the life-support mod's own Water resource, in its own
                // unit. No mod, or no water tank on this vehicle -> null, and the row draws a dash.
                state.WaterText = ls.HasWater ? ls.WaterLitres.ToString("F0") + " L" : null;
                state.Water01 = ls.Water01;

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
                state.Crew01 = (ci.CrewCapacity > 0) ? (double)ci.Crew / ci.CrewCapacity : 0.0;
                // The CREW tab's two gas-store rows. SIMULATED, from pure/VehicleSystems.cs, whose stores
                // fall with the real crew count, the real power state and a real leak - the same signals
                // the P&ID page draws. Set here because Steps() has just published state.Systems.
                state.O2TankText = Pct(state.Systems.Oxygen);
                state.N2TankText = Pct(state.Systems.Nitrogen);
                Seats(v);
                Pyro(v);
                Acceleration(v);
                Rates(v);
                VehicleSources(v);
                Avionics(v);

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

                // PROP tab (T13b): how hard the hardest-working Draco quad is being asked to fire. Read
                // through PropSchematic.MaxDuty - the SAME function that lights the schematic's rings -
                // so the number in the data band and the segments above it can never disagree. It reads
                // the control demand and the RCS action group set just above, hence its position here.
                state.DracoDutyText = Pct(PropSchematic.MaxDuty(state));

                met = "T+ " + Clock(v.missionTime);
                Docking(v, ref state);
            }
            catch (Exception e)
            {
                state.Valid = false;
                state.PlanetCamLive = false;      // same reason as the no-vessel path above (S10)
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
            // T13b: the THERMAL tab's SHIELD ring is that same hottest reading as a fraction of the
            // part's OWN maximum - margin to limit, not a temperature against an invented full scale.
            // Taken here because HottestPart walks every part and this is the one place it is called.
            state.HullTemp01 = sy.HottestPart01;
            FlightCommands.Charge01 = state.Power01;
            Systems.Update(ref FlightCommands.State, sy);
            state.Systems = FlightCommands.State;
            // T14: the Manual Chute Deploy page carries ENABLE BACKUP PYROS on two of its steps -
            // the same command the console plate carries - so its lamp is read from the same flag
            // the plate's dash is, not from a second latch of its own.
            state.BackupPyrosArmed = FlightCommands.BackupPyros;

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
            state.DeorbitEngaged = DeorbitOps.Engaged;
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
            st.HasTargetOrbit = false;
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

            // ⚠ The body angular RATES used to be computed here. They are NOT target-dependent, and the
            // GNC subsystem tab needs them with no target at all - inside this block they were simply
            // stale there (T13b). They now live in Rates(), which runs every refresh; this block keeps
            // only what a target actually defines.

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
                double yawDeg = Math.Atan2(Vector3d.Dot(rel, ct.right),
                                           Vector3d.Dot(rel, axis)) * 180.0 / Math.PI;
                double pitchDeg = Math.Atan2(Vector3d.Dot(rel, ct.forward),
                                             Vector3d.Dot(rel, axis)) * 180.0 / Math.PI;
                st.YawText   = Deg(yawDeg);    st.YawDegText   = DegSym(yawDeg);
                st.PitchText = Deg(pitchDeg);  st.PitchDegText = DegSym(pitchDeg);

                Vector3d ourR = Flatten(ct.right, axis);
                Vector3d tgtR = Flatten(tt.right, axis);
                if (ourR.magnitude > 1e-6 && tgtR.magnitude > 1e-6)
                {
                    double roll = Vector3d.Angle(ourR, tgtR);
                    if (Vector3d.Dot(Vector3d.Cross(ourR, tgtR), axis) < 0.0) roll = -roll;
                    st.RollText = Deg(roll);
                    st.RollDegText = DegSym(roll);
                }
                else { st.RollText = "-"; st.RollDegText = "-"; }
            }

            TargetPlot(v, tgt, ref st);
        }

        /// ---- WHERE THE TARGET SITS ON OUR OWN ORBIT PLOT (T13c) ----
        /// The rendezvous plot's approach chord had nowhere real to run to: T6 drew it to periapsis as a
        /// stated stand-in because the target's orbital state was not in PageState. It is now, as the two
        /// numbers the plot actually needs - the target's radius from the same body centre the plot is
        /// focused on, and its PHASE ANGLE from us.
        ///
        /// The angle is measured FROM OUR POSITION, not from periapsis, and that is the point: the plot
        /// places our own marker from our current radius (so it can never disagree with the apogee and
        /// perigee printed beside it), and an angle measured from that marker inherits the same guarantee.
        /// Sign is right-handed about our angular momentum r x v, so positive is AHEAD of us along the
        /// direction of travel, whatever frame convention KSP hands back - both vectors come from the
        /// same call, and a dot or a cross between them does not care.
        ///
        /// APPROXIMATION, STATED: the target is projected into OUR orbital plane, so a target in a
        /// different plane is drawn at its in-plane bearing and its true distance. On a real rendezvous
        /// the two planes are all but identical (that is what the plane-change burn is for), and a 2D
        /// plot has nowhere else to put it. Not around our body at all -> no chord (HasTargetOrbit false).
        private static void TargetPlot(Vessel v, ITargetable tgt, ref PageState st)
        {
            Orbit us = v.orbit, to = (tgt != null) ? tgt.GetOrbit() : null;
            if (us == null || to == null || to.referenceBody != us.referenceBody) return;
            try
            {
                double now = Planetarium.GetUniversalTime();
                Vector3d rUs = us.getRelativePositionAtUT(now);
                Vector3d h   = Vector3d.Cross(rUs, us.getOrbitalVelocityAtUT(now));
                Vector3d rT  = to.getRelativePositionAtUT(now);
                if (h.magnitude < 1e-6 || rUs.magnitude < 1.0 || rT.magnitude < 1.0) return;

                Vector3d hHat = h / h.magnitude;
                Vector3d tp = rT - hHat * Vector3d.Dot(rT, hHat);      // target, in OUR plane
                if (tp.magnitude < 1.0) return;                        // straight over a pole of the plane

                st.TargetPhaseRad = Math.Atan2(Vector3d.Dot(Vector3d.Cross(rUs, tp), hHat),
                                               Vector3d.Dot(rUs, tp));
                st.TargetRadiusM = rT.magnitude;
                st.HasTargetOrbit = !double.IsNaN(st.TargetPhaseRad);
            }
            catch (Exception) { st.HasTargetOrbit = false; }
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

            // Is a scaled-space camera actually rendering behind the 3D view (S10)? Asked of the image
            // store, never assumed: the page must not print LIVE CAMERA over a textured disc. False
            // until S10b builds the renderer - see ImageStore.ScaledPlanetTexture. Set BEFORE the
            // early returns below, because "no orbit to plot" is exactly the state where an unmarked
            // globe would read as a feed.
            state.PlanetCamLive = ImageStore.ScaledPlanetLive();

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

        /// ---- THE VEHICLE PAGE'S OWN SOURCES ----
        /// T13a. The VEHICLE family (overview / mech / systems tree) drew these as constants. Every one
        /// here is a REAL count or a REAL resource total read off the vessel; where this vehicle simply
        /// has no such thing the text is left NULL and the page draws a dash, rather than a plausible
        /// number being invented at this end (docs/TELEMETRY_REGISTRY.md).
        ///
        /// ⛔ ONE PASS OVER THE PARTS. Three readouts want three different things off the same list, and
        /// VesselData already walks it several times a refresh; adding three more walks for four strings
        /// is the wrong trade on a vessel with a full stack attached.
        private static void VehicleSources(Vessel v)
        {
            // ---- CONSUMABLES: the two power units ----
            // ONE real charge pool, reported on both rows. The vehicle's two independent power units are
            // not modelled, and a bus switch does not change how much energy is STORED - so gating this
            // on Bus1On/Bus2On would answer a different question than the label asks. See PageState.
            string energy = Energy();
            state.PowerUnit1Text = energy;
            state.PowerUnit2Text = energy;

            double fuel = 0.0, ox = 0.0;
            double fuelMax = 0.0, oxMax = 0.0;
            bool anyFuel = false, anyOx = false;
            int panels = 0, extended = 0, cells = 0, liveCells = 0;
            double arrayFlow = 0.0, arrayRated = 0.0;

            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                // The DRAGON's own tanks, not the stack's: the Dracos those tanks feed are what flies
                // the deorbit burn, which is what the row is asking about.
                bool dragon = !VehicleParts.IsBooster(p.name) && !VehicleParts.IsSecondStage(p.name);
                bool cell = false;

                for (int k = 0; k < p.Resources.Count; k++)
                {
                    PartResource r = p.Resources[k];
                    if (r.maxAmount <= 0.0) continue;

                    if (r.resourceName == "ElectricCharge") { cell = true; if (r.amount > 0.0) liveCells++; continue; }
                    if (!dragon || r.info == null || r.info.density <= 0f) continue;

                    // KSP densities are tonnes per unit, so these are tonnes until Kg() scales them.
                    // ⛔ CAPACITY IS ACCUMULATED BESIDE THE CONTENTS, not derived from it: the PROP tab's
                    // OX / FUEL gauges are the fraction of these same tanks BY MASS, so the percentage and
                    // the kilogram row beside it are two views of one number rather than two claims.
                    // NTO / MMH are here because that is what the real Dracos burn and what RealFuels
                    // names them; the stock analogues stand in where RealFuels is not installed, exactly
                    // as DockedSide.ReturnProps already falls back for the return-propellant fraction.
                    if (r.resourceName == "Oxidizer" || r.resourceName == "NTO")
                    { ox += r.amount * r.info.density; oxMax += r.maxAmount * r.info.density; anyOx = true; }
                    else if (r.resourceName == "LiquidFuel" || r.resourceName == "MonoPropellant"
                             || r.resourceName == "MMH")
                    { fuel += r.amount * r.info.density; fuelMax += r.maxAmount * r.info.density; anyFuel = true; }
                }
                if (cell) cells++;

                for (int m = 0; m < p.Modules.Count; m++)
                {
                    ModuleDeployableSolarPanel sp = p.Modules[m] as ModuleDeployableSolarPanel;
                    if (sp == null) continue;
                    panels++;
                    if (sp.deployState == ModuleDeployablePart.DeployState.EXTENDED) extended++;
                    // POWER tab (T13b): what the array is MAKING, and what it could make. The ring is the
                    // ratio of the two - a real fraction off the panels' own rating rather than a full
                    // scale someone chose - and a shadowed or badly-pointed array reads low, correctly.
                    arrayFlow += sp.flowRate;
                    arrayRated += sp.chargeRate;
                }
            }

            state.DeorbitFuelText = anyFuel ? Kg(fuel) : null;
            state.DeorbitOxText   = anyOx   ? Kg(ox)   : null;

            // ---- PROP + GNC: the Dragon's own tanks as fractions (T13b) ----
            // Bare text for the gauges, unit-carrying text for the row, one source for all of it. Both
            // tanks together is what "Prop Remaining" and the GNC tab's "RCS FUEL" ask for: the Dracos
            // ARE the RCS, so those two readouts are one number on two pages.
            state.DragonOx01   = (oxMax   > 0.0) ? Clamp01(ox   / oxMax)   : 0.0;
            state.DragonFuel01 = (fuelMax > 0.0) ? Clamp01(fuel / fuelMax) : 0.0;
            double propMax = oxMax + fuelMax;
            state.DragonProp01 = (propMax > 0.0) ? Clamp01((ox + fuel) / propMax) : 0.0;
            state.DragonOxText   = (oxMax   > 0.0) ? Bare(state.DragonOx01)   : null;
            state.DragonFuelText = (fuelMax > 0.0) ? Bare(state.DragonFuel01) : null;
            state.DragonPropText    = (propMax > 0.0) ? Bare(state.DragonProp01) : null;
            state.PropRemainingText = (propMax > 0.0) ? Pct(state.DragonProp01)  : null;

            // ---- POWER: solar array output (T13b) ----
            // KSP's electric charge has no defined wattage, so the SCALE is the one pure/CabinEnvironment.cs
            // already states for the net-power dials (120 W per EC/s) - reused rather than a second scale
            // invented here, so the two power numbers on this page are in the same currency.
            state.Array01 = (arrayRated > 0.0) ? Clamp01(arrayFlow / arrayRated) : 0.0;
            double arrayKw = arrayFlow * EcWatts / 1000.0;
            state.ArrayKwText     = (panels > 0) ? arrayKw.ToString("F2") : null;
            state.ArrayOutputText = (panels > 0) ? arrayKw.ToString("F2") + " kW" : null;

            // A body-mounted panel is a ModuleDeployableSolarPanel that is permanently EXTENDED, so it
            // reads DEPLOYED and that is correct - it is deployed, by construction.
            state.SolarArrayText = (panels == 0) ? "NONE"
                                 : (extended == panels) ? "DEPLOYED"
                                 : (extended == 0) ? "STOWED"
                                 : extended + " / " + panels;
            state.BatteryText = (cells == 0) ? "NONE" : liveCells + " / " + cells;

            // ---- POWER: net electrical flow, both buses (T13b) ----
            // The SAME watts the overview's two NET PWR dials split. Signed: negative is draining, and
            // "Charge Rate" is that same flow in kW, which is what a charge rate is.
            double netW = state.Cabin.NetPwr1W + state.Cabin.NetPwr2W;
            state.NetPowerText   = Signed(netW, 0, " W");
            state.ChargeRateText = Signed(netW / 1000.0, 2, " kW");
        }

        /// <summary>AVIONICS: S-BAND COMMS + Uplink / Downlink, from stock KSP's OWN CommNet (S24, owner
        /// decision (b) - REGISTER.md). `Vessel.Connection` is the same "read the game's real state,
        /// never invent one" rule every other field in this file follows - `CommNet.CommNetVessel`,
        /// gated on the game's own CommNet difficulty toggle so an RSS/RO table that has turned CommNet
        /// off (or a vessel with no CommNetVessel at all - `Connection` can be null) dashes rather than
        /// reporting a link this build is not really tracking. CommNet has no separate uplink/downlink
        /// budget, so both readouts are the ONE real signal strength - not a copy-paste, the same
        /// reasoning as PowerUnit1Text/PowerUnit2Text above. LINK MARGIN (no dB conversion exists for a
        /// 0..1 strength), FC LOAD, BUS TRAFFIC, STORAGE and GPS are untouched by this - see Pages.cs.</summary>
        private static void Avionics(Vessel v)
        {
            CommNet.CommNetVessel conn = CommNet.CommNetScenario.CommNetEnabled ? v.Connection : null;
            bool linked = conn != null && conn.IsConnected;
            double sig01 = (conn != null) ? Clamp01(conn.SignalStrength) : 0.0;

            state.SBandText   = (conn != null) ? (linked ? "Linked" : "No Signal") : null;
            state.SBandLinked = linked;
            state.CommSignal01 = sig01;
            state.UplinkText   = (conn != null) ? Pct(sig01) : null;
            state.DownlinkText = state.UplinkText;
        }

        /// <summary>A power unit's energy: the vessel's real state of charge, with its unit. Null when
        /// there is no charge reading at all, which the page draws as a dash.</summary>
        private static string Energy()
        {
            if (string.IsNullOrEmpty(state.PowerText) || state.PowerText == "-") return null;
            return state.PowerText + " %";
        }

        /// <summary>W per unit of KSP electric charge per second. NOT a physical constant - KSP's charge
        /// has no defined wattage - but the one pure/CabinEnvironment.cs already picked for the net-power
        /// dials, reused here so two power readouts on one page cannot be in different currencies.</summary>
        private const double EcWatts = 120.0;

        /// <summary>A 0..1 fraction as a whole percent with NO unit - a headline gauge draws its unit on
        /// its own line beneath the number, so appending one here prints it twice. T13b.</summary>
        private static string Bare(double frac)
        {
            if (double.IsNaN(frac) || double.IsInfinity(frac)) return null;
            return (frac * 100.0).ToString("F0");
        }

        /// <summary>A SIGNED reading with its unit. The sign is the whole point of a net-power or
        /// charge-rate row - "68 W" and "-68 W" are opposite facts - so a positive one is written with
        /// its plus rather than left to look like an unsigned quantity. T13b.</summary>
        private static string Signed(double v, int dp, string unit)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return null;
            string s = v.ToString(dp == 0 ? "F0" : "F" + dp);
            return ((v > 0.0) ? "+" : "") + s + unit;
        }

        /// <summary>A 0..1 fraction as a whole percent WITH its unit - the format the subsystem tabs'
        /// detail rows print (one right-aligned string per row). T13b.</summary>
        private static string Pct(double frac)
        {
            if (double.IsNaN(frac) || double.IsInfinity(frac)) return null;
            return (frac * 100.0).ToString("F0") + " %";
        }

        private static string Kg(double tonnes)
        {
            if (double.IsNaN(tonnes) || double.IsInfinity(tonnes)) return "-";
            return (tonnes * 1000.0).ToString("F1") + " kg";
        }

        /// ---- BODY RATES BELONG TO THE VEHICLE, NOT TO A TARGET ----
        /// T13b. vessel.angularVelocity, axes pitch=x / roll=y / yaw=z - the same convention
        /// FlightLog/FlightRecorder use. This used to sit inside Docking(), which returns early with no
        /// target, so the GNC subsystem tab would have shown whatever the last docked approach left
        /// behind. One read, three consumers, and the two FORMATS come off the same numbers: bare for a
        /// gauge that prints its unit on its own line, with the unit for a row that prints one string.
        private static void Rates(Vessel v)
        {
            Vector3 dps = v.angularVelocity * Mathf.Rad2Deg;
            state.BodyPitchDps = dps.x;
            state.BodyRollDps  = dps.y;
            state.BodyYawDps   = dps.z;

            state.PitchRateText = DegRate(dps.x);
            state.RollRateText  = DegRate(dps.y);
            state.YawRateText   = DegRate(dps.z);

            state.BodyPitchText = dps.x.ToString("F2");
            state.BodyRollText  = dps.y.ToString("F2");
            state.BodyYawText   = dps.z.ToString("F2");
            state.BodyRateText  = DegRate(dps.magnitude);
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

            // ---- DIAL FULL SCALES, STATED ----
            // 5 g for the axial pair, which is the scale the G dial already uses (GForce01 = gee / 5)
            // so two dials for the same kind of quantity cannot mean different things. 2 g for the
            // centripetal term, because a low circular orbit sits near 1 g and that puts nominal in the
            // middle of the dial - the CabinEnvironment rule about a needle that never leaves one end.
            state.AccelPos01  = Clamp01(along / 5.0);
            state.AccelNeg01  = Clamp01(-along / 5.0);
            state.AccelCent01 = Clamp01(cent / 2.0);
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

        /// <summary>T13c. Deg's twin, in the glyph form the manual docking page prints ("15.0°").
        /// Same value, same guard, one rendering per surface - see PageState.RollDegText.</summary>
        private static string DegSym(double d)
        {
            if (double.IsNaN(d)) return "-";
            return d.ToString("F1") + "°";
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
