// DragonScreen - AutoPilot
// ---- NO MECHJEB, NO kOS ----
// ---- IT FLIES ONE NAMED VESSEL, NOT "THE ACTIVE ONE" ----
// ---- STEERING IS OURS, NOT SAS ----
// ---- STAGING IS OBSERVED, NOT SCHEDULED ----
using System;
using KSP.UI.Screens;
using UnityEngine;
using MechJebLib.Primitives;

namespace DragonScreen
{
    public static class AutoPilot
    {
        private const string Tag = "[DragonScreen] ";

        public static bool Engaged { get; private set; }
        public static AscentPhase Phase { get; private set; }
        public static AscentTarget Target = AscentTarget.Station();

        [Tunable] public static double AscentInclinationBiasDeg = 3.9;
        const double RssParkingAltitudeM = 200000.0;

        // ---- RSS second-stage LOFT (interim, see Steer) ----
        [Tunable] public static double S2LoftGainDeg = 70.0;
        [Tunable] public static double S2MaxLoftDeg = 45.0;

        public static bool ResumeAscent;

        public static AscentCommand Command;

        public static double LastCircDvMps;

        public static double PhaseElapsedS;
        public static double RangeToBoosterM;

        private static int lastFrame = -1;
        private static double starvedFor;
        private static double lastStageAt = -99.0;

        public static void Toggle()
        {
            if (Engaged) Disengage("crew"); else Engage();
        }

        public static void Engage()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            // ---- ⛔ THIS IS AN ASCENT AUTOPILOT. IT MUST REFUSE TO "ASCEND" FROM ORBIT. ----
            if (v.orbit != null && v.mainBody != null && v.orbit.PeA > v.mainBody.atmosphereDepth)
            {
                Debug.LogWarning(Tag + "AUTO SEQUENCE refused - already in orbit ("
                                     + (v.orbit.PeA / 1000.0).ToString("F1") + " x "
                                     + (v.orbit.ApA / 1000.0).ToString("F1")
                                     + " km). The ascent autopilot flies to orbit; it will not fly "
                                     + "from one. Use the manoeuvre page for orbital burns.");
                return;
            }

            // ---- ⛔ HOLD FOR THE PHASE WINDOW. §1 OF THE MISSION, AND IT WAS UNREACHABLE. ----
            windowOpensUt = 0.0;
            windowWarped = false;
            if (v.situation == Vessel.Situations.PRELAUNCH
                || v.situation == Vessel.Situations.LANDED)
            {
                double wait = LaunchWindowOps.SecondsToWait(v);
                if (wait > 0.0)
                {
                    windowOpensUt = Planetarium.GetUniversalTime() + wait;
                    Debug.Log(Tag + "LAUNCH WINDOW: " + LaunchWindowOps.Note);
                }
                else Debug.Log(Tag + "launch window open now - " + LaunchWindowOps.Note);
            }

            Engaged = true;
            ResumeAscent = false;
            Phase = AscentPhase.Idle;
            BoosterRecovery.Reset();
            // ---- CREW-2: DRONESHIP recovery downrange, ascent scaled to Earth. ----
            BoosterRecovery.Profile = LandingProfile.Droneship;
            Target = AscentTarget.ForBody(BoosterRecovery.Profile, RssParkingAltitudeM);
            ascentVessel = v;

            // ---- TARGET THE STATION (ISS) FROM LAUNCH. ----
            {
                Vessel stn = StationApproach.Find();
                if (stn != null)
                {
                    DockingOps.SetTarget(stn, "launch - targeting the station");

                    // ---- LAUNCH INTO THE STATION'S PLANE, NOT BLINDLY DUE EAST. ----
                    if (stn.orbit != null && v.mainBody != null)
                    {
                        double r = v.mainBody.Radius + Target.AltitudeM;
                        double vOrb = Math.Sqrt(v.mainBody.gravParameter / r);
                        double vEq = LaunchAzimuth.SurfaceEastwardSpeedMps(
                            v.mainBody.Radius, v.mainBody.rotationPeriod, v.latitude);
                        double incTarget = stn.orbit.inclination + AscentInclinationBiasDeg;
                        Target.HeadingDeg = LaunchAzimuth.GroundHeadingDeg(
                            incTarget, v.latitude, vOrb, vEq);
                        Debug.Log(Tag + "launch azimuth " + Target.HeadingDeg.ToString("F1")
                                  + " deg for a " + incTarget.ToString("F2") + " deg aim (station "
                                  + stn.orbit.inclination.ToString("F2") + " + bias "
                                  + (incTarget - stn.orbit.inclination).ToString("F2")
                                  + ", pad " + v.latitude.ToString("F2") + " N)");
                    }
                }
            }
            packedReported = false;

            // ---- ⛔ THE PAD HOLD LIVES IN Tick(), NOT HERE. ----
            s2Separated = false;
            boosterSeparated = false;
            s2IgnitionAttempts = 0;
            lastS2IgniteAt = -99.0;
            upfgState.Initialised = false;
            upfgActive = false;
            clampReleased = false;
            s1IgniteAt = -1.0;
            lastHandoverTry = 0.0;
            starvedFor = 0.0;
            blindStages = 0;
            // ---- ⛔ RESET THE STAGING LOCKOUT, OR A REVERT-TO-LAUNCH NEVER IGNITES. ----
            lastStageAt = -99.0;
            phaseStartedAt = Planetarium.GetUniversalTime();
            lastCommanded = Vector3d.zero;

            // ---- ⛔ EXTEND THE PHYSICS RANGE NOW, NOT AT HANDOVER. ----
            BoosterRecovery.PrepareForSeparation(v);
            if (v.situation == Vessel.Situations.PRELAUNCH || v.situation == Vessel.Situations.LANDED)
            {
                liftoffUt = 0.0;
                liftoffLonDeg = 0.0;
            }
            // ---- READ THE VEHICLE BEFORE FLYING IT. ----
            VehicleCheck.Report(v);

            Debug.Log(Tag + "autopilot ENGAGED - target " + (Target.AltitudeM / 1000.0).ToString("F0")
                      + " km, heading " + Target.HeadingDeg.ToString("F0")
                      + ". ⚠ INTERIM: gravity turn, not the PSG ascent in FLIGHT_SOFTWARE_PLAN.md");
        }

        public static void Disengage(string why)
        {
            if (!Engaged) return;
            Engaged = false;
            Phase = AscentPhase.Idle;
            AttitudeController.Ascent.Release(ascentVessel);
            if (FlightGlobals.ActiveVessel == ascentVessel)
                FlightInputHandler.state.mainThrottle = 0f;
            ascentVessel = null;
            Debug.Log(Tag + "autopilot DISENGAGED - " + why);

        }

        public static void Tick()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;

            // ---- ⛔ BOTH VEHICLES FLY. THIS USED TO `return` HERE, AND THAT WAS THE CEILING. ----
            if (BoosterRecovery.Active || BoosterRecovery.Settling) BoosterRecovery.Tick();

            if (!Engaged) return;

            // ---- FLY THE VESSEL WE LAUNCHED, NOT WHICHEVER ONE THE CAMERA IS ON ----
            Vessel v = ascentVessel;
            if (v == null || v.state == Vessel.State.DEAD || v.orbit == null || v.mainBody == null)
            {
                Disengage("upper stage lost");
                return;
            }

            if (v.packed)
            {
                if (!packedReported)
                {
                    packedReported = true;
                    Debug.LogWarning(Tag + "upper stage has gone on rails (beyond the physics "
                                         + "range) - guidance is suspended until it reloads");
                }
                return;
            }
            packedReported = false;

            // ---- THE PAD HOLD. Nothing is commanded until the phase comes round. ----
            if (windowOpensUt > 0.0)
            {
                double leftS = windowOpensUt - Planetarium.GetUniversalTime();
                if (leftS > 0.0)
                {
                    Command.Note = "HOLD FOR PHASE WINDOW - T-" + leftS.ToString("F0") + " s";
                    if (!windowWarped && leftS > NodeExecutor.WarpWorthwhileS)
                    {
                        windowWarped = true;
                        TimeWarp.fetch.WarpTo(windowOpensUt - WindowWarpLeadS);
                    }
                    else if (leftS <= WindowWarpLeadS && TimeWarp.CurrentRateIndex > 0)
                        TimeWarp.SetRate(0, true);
                    return;
                }
                if (TimeWarp.CurrentRateIndex > 0) { TimeWarp.SetRate(0, true); return; }
                windowOpensUt = 0.0;
                Debug.Log(Tag + "launch window OPEN - releasing the countdown");
            }

            // ---- STAMP LIFTOFF WHEN IT HAPPENS, NOT WHEN THE CREW ARMED THE AUTOPILOT. ----
            if (liftoffUt <= 0.0
                && v.situation != Vessel.Situations.PRELAUNCH
                && v.situation != Vessel.Situations.LANDED)
            {
                liftoffUt = Planetarium.GetUniversalTime();
                liftoffLonDeg = v.longitude;
                Debug.Log(Tag + "liftoff - clock started for the launch-window fit");
            }

            // ---- ⛔ ISSUES 3 AND 4. STATICS MUST VALIDATE, NOT REMEMBER. ----

            BoosterRecovery.RememberPad(v);

            // ---- ⛔ TAKE THE BOOSTER AS SOON AS ONE EXISTS. ----
            if (Phase == AscentPhase.Meco || Phase == AscentPhase.StageSep
                || Phase == AscentPhase.BurnToApoapsis || Phase == AscentPhase.Coast)
            {
                double nowUt = Planetarium.GetUniversalTime();
                if (nowUt - lastHandoverTry > 0.5)
                {
                    lastHandoverTry = nowUt;
                    BoosterRecovery.TryHandover(v);
                }
            }

            // ---- HAND BACK RATHER THAN FIGHT ----
            if (PilotInput(v)) { Disengage("manual input"); return; }

            AscentInputs a = new AscentInputs();
            a.Valid = true;
            a.RadarAltitude = v.radarAltitude;
            a.Altitude = v.altitude;
            a.ApoapsisM = v.orbit.ApA;
            a.PeriapsisM = v.orbit.PeA;
            a.AtmosphereDepthM = v.mainBody.atmosphereDepth;
            a.VerticalSpeed = v.verticalSpeed;
            a.SurfaceSpeed = v.srfSpeed;
            a.DynamicPressureKpa = v.dynamicPressurekPa;
            a.TimeToApoapsisS = v.orbit.timeToAp;
            a.AvailableThrust = AvailableThrust(v);
            a.MaxThrustKn = MaxThrust(v);
            a.MassT = v.GetTotalMass();
            a.Landed = (v.situation == Vessel.Situations.LANDED
                     || v.situation == Vessel.Situations.PRELAUNCH
                     || v.situation == Vessel.Situations.SPLASHED);
            a.SecondStage = !HasBooster(v);
            a.PhaseElapsedS = Planetarium.GetUniversalTime() - phaseStartedAt;
            a.RangeToBoosterM = Range(v, BoosterRecovery.BoosterVessel);
            PhaseElapsedS = a.PhaseElapsedS;
            RangeToBoosterM = a.RangeToBoosterM;

            // ---- "MAKE THE ORBIT CIRCULAR HERE, NOW" ----
            circDv = CirculariseDv(v);
            a.CircDvMps = circDv.magnitude;
            LastCircDvMps = a.CircDvMps;
            a.CircDvFlipped = circDvStart.sqrMagnitude > 0.01
                              && Vector3d.Dot(circDv, circDvStart) < 0.0;

            AscentCommand c = Ascent.Guide(a, Target, Phase);
            if (c.Phase == AscentPhase.Circularise && Phase != AscentPhase.Circularise)
                circDvStart = circDv;
            if (c.Phase != AscentPhase.Circularise) circDvStart = Vector3d.zero;

            // ---- INSTRUMENT THE BURN, NOT JUST THE TRANSITIONS ----
            if (c.Throttle > 0.01 && Time.realtimeSinceStartup - lastBurnLog > 2f)
            {
                lastBurnLog = Time.realtimeSinceStartup;
                Debug.Log(Tag + "ascent " + Ascent.Name(c.Phase)
                          + "  ap " + (a.ApoapsisM / 1000.0).ToString("F1")
                          + "  pe " + (a.PeriapsisM / 1000.0).ToString("F1")
                          + "  circDv " + a.CircDvMps.ToString("F1")
                          + "  thr " + c.Throttle.ToString("F2"));
            }

            if (c.Phase != Phase)
                Debug.Log(Tag + "ascent -> "
                          + (string.IsNullOrEmpty(c.Note) ? Ascent.Name(c.Phase) : c.Note)
                          + "  ap " + (a.ApoapsisM / 1000.0).ToString("F1")
                          + " km, pe " + (a.PeriapsisM / 1000.0).ToString("F1") + " km"
                          + Crew2Sync(v));
            if (c.Phase != Phase) phaseStartedAt = Planetarium.GetUniversalTime();
            Phase = c.Phase;
            Command = c;

            // ---- ⛔ RSS/RO: UPFG OWNS THE SECOND STAGE ALL THE WAY TO ORBIT. ----
            if (!s2Separated)
            {
                if (UpfgEnabledS2 && !upfgActive && a.AvailableThrust > UpfgMinThrustKn
                    && (c.Phase == AscentPhase.BurnToApoapsis || c.Phase == AscentPhase.Coast
                        || c.Phase == AscentPhase.Circularise))
                    upfgActive = true;
                if (upfgActive && UpfgFlyS2(v, a))
                {
                    // ---- ⛔ UPFG RETURNS BEFORE THE UllageFore APPLICATION BELOW - SO APPLY IT HERE. ----
                    AttitudeController.Ascent.UllageFore = c.UllageFore;
                    SetAscentRcs(v, c);
                    return;
                }
            }

            if (Phase == AscentPhase.Done)
            {
                // ---- ⛔ REPORT WHY, NOT WHAT THE ENUM IS CALLED ----
                FlightInputHandler.state.mainThrottle = 0f;

                // ---- ⛔ SEPARATE HERE, BECAUSE THIS BRANCH RETURNS. ----
                if (c.SeparateS2) SeparateSecondStage(v);

                LaunchWindowOps.MeasureAtInsertion(v, liftoffUt, liftoffLonDeg);
                Disengage(string.IsNullOrEmpty(c.Note) ? "insertion complete" : c.Note);
                return;
            }

            Steer(v, c);
            AttitudeController.Ascent.Throttle = c.Throttle;
            if (FlightGlobals.ActiveVessel == v)
                FlightInputHandler.state.mainThrottle = (float)c.Throttle;
            // ---- ULLAGE. THE SIGN IS CONFIRMED; THE DELIVERY IS BEST-EFFORT. ----
            // ---- ⛔ ULLAGE GOES THROUGH THE CALLBACK NOW, NOT `v.ctrlState.Z` FROM HERE. ----
            AttitudeController.Ascent.UllageFore = c.UllageFore;
            SetAscentRcs(v, c);

            // ---- DROP THE S2 AND FINISH ON THE DRACOS ----
            if (c.SeparateS2) SeparateSecondStage(v);

            // ---- MECO STAGES ON COMMAND, NOT ON STARVATION ----
            // ---- ⛔ AND ONLY WHILE THERE IS A BOOSTER TO SEPARATE. ----
            if (c.Stage && HasBooster(v) && Planetarium.GetUniversalTime() - lastStageAt > 2.0)
            {
                lastStageAt = Planetarium.GetUniversalTime();
                blindStages = 0;
                // ---- SEPARATE THE S1 BY CAPABILITY (Crew-2). ----
                if (SeparateBooster(v))
                    Debug.Log(Tag + "MECO - booster separated by capability (interstage decoupler)");
                else
                {
                    StageManager.ActivateNextStage();
                    Debug.LogWarning(Tag + "MECO - no interstage decoupler found; fell back to staging, "
                                         + "now stage " + StageManager.CurrentStage);
                }
            }
            else if (c.Phase == AscentPhase.VerticalRise)
                RoLaunch(v, a);
            else if (c.Phase == AscentPhase.BurnToApoapsis)
                IgniteSecondStageWhenSettled(v, c, a);
            else
                Stage(v, c, a);
        }

        /// ---- ⛔ RCS THROUGH POWERED FLIGHT IS WASTE. ----
        private static void SetAscentRcs(Vessel v, AscentCommand c)
        {
            if (v == null) return;
            bool want = c.Rcs || c.UllageFore > 0.01;
            if (v.ActionGroups[KSPActionGroup.RCS] != want)
                v.ActionGroups.SetGroup(KSPActionGroup.RCS, want);
        }

        private static string Crew2Sync(Vessel v)
        {
            if (liftoffUt <= 0.0) return "";
            double met = Planetarium.GetUniversalTime() - liftoffUt;
            if (met < 0.0) return "";
            Crew2Event cur = Crew2Timeline.Current(met);
            string s = "  | Crew-2 T+" + met.ToString("F0") + "s: " + cur.Name;
            Crew2Event nxt;
            if (Crew2Timeline.Next(met, out nxt))
                s += ", next " + nxt.Name + " in " + Crew2Timeline.TimeToNext(met).ToString("F0") + "s";
            return s;
        }

        // ---- UPFG SECOND-STAGE GUIDANCE (RSS/RO) ----
        private static UpfgState upfgState;
        private static bool upfgActive;

        [Tunable] public static bool UpfgEnabledS2 = false;
        private const double UpfgSecoTgoS = 0.2;
        private const double UpfgMinThrustKn = 200.0;
        private static double lastUpfgLog;

        private static bool UpfgFlyS2(Vessel v, AscentInputs a)
        {
            if (v.mainBody == null) return false;
            V3 r = ToV3(v.CoM - v.mainBody.position);
            V3 vel = ToV3(v.obt_velocity);
            double mu = v.mainBody.gravParameter;
            if (r.magnitude <= 0.0 || vel.magnitude <= 0.0) return false;

            UpfgTarget t = new UpfgTarget();
            V3 up = V3.Normalize(r);
            V3 prograde = V3.Normalize(vel - V3.Dot(vel, up) * up);
            t.Iy = V3.Normalize(V3.Cross(prograde, up));
            t.RadiusM = v.mainBody.Radius + Target.AltitudeM;
            t.SpeedMps = Math.Sqrt(mu / t.RadiusM);
            t.GammaRad = 0.0;

            UpfgVehicle veh = new UpfgVehicle();
            veh.ThrustN = a.AvailableThrust * 1000.0;
            veh.MassKg = v.GetTotalMass() * 1000.0;
            veh.ExhaustVel = S2ExhaustVel(v);
            if (veh.MassKg <= 0.0 || veh.ExhaustVel <= 0.0) return false;
            if (a.AvailableThrust < UpfgMinThrustKn)
            {
                AttitudeController.Ascent.SteerTo(v, new Vector3d(prograde.x, prograde.y, prograde.z),
                    (v.CoM - v.mainBody.position).normalized);
                AttitudeController.Ascent.Throttle = 1.0;
                if (FlightGlobals.ActiveVessel == v) FlightInputHandler.state.mainThrottle = 1.0f;
                return true;
            }

            UpfgGuidance g = Upfg.Step(r, vel, mu, t, veh, ref upfgState);
            if (!g.Valid || double.IsNaN(g.TgoS)) return false;

            if (g.TgoS <= UpfgSecoTgoS)
            {
                AttitudeController.Ascent.Throttle = 0.0;
                if (FlightGlobals.ActiveVessel == v) FlightInputHandler.state.mainThrottle = 0f;
                SeparateSecondStage(v);
                LaunchWindowOps.MeasureAtInsertion(v, liftoffUt, liftoffLonDeg);
                Debug.Log(Tag + "SECO - UPFG orbit insertion complete" + Crew2Sync(v));
                Disengage("SECO - UPFG orbit insertion");
                return true;
            }

            Vector3d iF = new Vector3d(g.IF.x, g.IF.y, g.IF.z);
            Vector3d upW = (v.CoM - v.mainBody.position).normalized;
            AttitudeController.Ascent.SteerTo(v, iF, upW);
            double s2Throttle = Ascent.GThrottle(a, Target.GLimitMps2);
            AttitudeController.Ascent.Throttle = s2Throttle;
            if (FlightGlobals.ActiveVessel == v) FlightInputHandler.state.mainThrottle = (float)s2Throttle;

            if (Time.realtimeSinceStartup - lastUpfgLog > 2f)
            {
                lastUpfgLog = Time.realtimeSinceStartup;
                double ifPitch = Math.Asin(Math.Max(-1.0, Math.Min(1.0, V3.Dot(g.IF, up)))) * 57.29578;
                Debug.Log(Tag + "UPFG  tgo " + g.TgoS.ToString("F0") + "s  pitch "
                          + ifPitch.ToString("F0") + "deg  ap "
                          + (a.ApoapsisM / 1000.0).ToString("F1") + "  pe "
                          + (a.PeriapsisM / 1000.0).ToString("F1") + "  orb "
                          + v.obt_velocity.magnitude.ToString("F0") + "/" + t.SpeedMps.ToString("F0")
                          + Crew2Sync(v));
            }
            return true;
        }

        private static V3 ToV3(Vector3d w) { return new V3(w.x, w.y, w.z); }

        // ---- RO LAUNCH: ignite, spool, confirm thrust, THEN release the clamp (by capability) ----
        private static bool clampReleased;
        private static double s1IgniteAt = -1.0;
        private const double LaunchSpoolS = 3.0;

        private static void RoLaunch(Vessel v, AscentInputs a)
        {
            double now = Planetarium.GetUniversalTime();
            if (s1IgniteAt < 0.0)
            {
                int lit = IgniteFirstStage(v);
                if (lit > 0)
                {
                    s1IgniteAt = now;
                    Debug.Log(Tag + "S1 IGNITION - " + lit + " engine(s) lit, spooling up while clamped");
                }
                return;
            }
            if (clampReleased || now - s1IgniteAt < LaunchSpoolS || a.AvailableThrust <= 1.0) return;

            double rocketT = v.GetTotalMass() - ErectorMassT(v);
            double twr = (rocketT > 0.0) ? a.AvailableThrust / (rocketT * 9.80665) : 0.0;
            if (twr >= 1.0)
            {
                ReleaseLaunchClamp(v);
                clampReleased = true;
                Debug.Log(Tag + "LIFTOFF - clamp released at TWR " + twr.ToString("F2")
                              + " (rocket " + rocketT.ToString("F0") + " t)" + Crew2Sync(v));
            }
            else if (now - s1IgniteAt > LaunchSpoolS + 6.0)
                Debug.LogWarning(Tag + "PAD HOLD - TWR only " + twr.ToString("F2")
                                     + " after ignition; NOT releasing the clamp onto a stack that cannot fly");
        }

        /// ---- ⛔ ONLY THE ALL-ENGINES MODE. THE OCTAWEB CARRIES THREE ENGINE MODULES. ----
        private static int IgniteFirstStage(Vessel v)
        {
            int lit = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                if (!VehicleParts.IsBooster(v.parts[i].name)) continue;
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (!es[m].EngineIgnited && !es[m].flameout
                        && VehicleParts.EngineIdIsMode(es[m].engineID, VehicleParts.ModeAllEngines))
                    { es[m].Activate(); lit++; }
            }
            return lit;
        }

        private static void ReleaseLaunchClamp(Vessel v)
        {
            FireCapability(v, VehicleParts.IsErector, "open erector");
            int n = FireCapability(v, VehicleParts.IsErector, "decouple");
            if (n == 0)
                Debug.LogWarning(Tag + "clamp release: no 'decouple' answered on an '"
                                     + VehicleParts.ErectorMarker + "' part - is the erector on the craft?");
        }

        private static double ErectorMassT(Vessel v)
        {
            double m = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
                if (VehicleParts.IsErector(v.parts[i].name))
                    m += v.parts[i].mass + v.parts[i].GetResourceMass();
            return m;
        }

        private static int FireCapability(Vessel v, System.Func<string, bool> match, string cap)
        {
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (!match(p.name)) continue;
                for (int mod = 0; mod < p.Modules.Count; mod++)
                {
                    PartModule pm = p.Modules[mod];
                    for (int e = 0; e < pm.Events.Count; e++)
                    {
                        BaseEvent ev = pm.Events[e];
                        if (ev != null && ev.active && ev.guiName != null
                            && string.Equals(ev.guiName, cap, StringComparison.OrdinalIgnoreCase))
                        { ev.Invoke(); n++; }
                    }
                }
            }
            if (n > 0) return n;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (!match(p.name)) continue;
                for (int mod = 0; mod < p.Modules.Count; mod++)
                {
                    PartModule pm = p.Modules[mod];
                    for (int act = 0; act < pm.Actions.Count; act++)
                    {
                        BaseAction ac = pm.Actions[act];
                        if (ac != null && ac.guiName != null
                            && string.Equals(ac.guiName, cap, StringComparison.OrdinalIgnoreCase))
                        { ac.Invoke(new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate)); n++; }
                    }
                }
            }
            return n;
        }

        private static double S2ExhaustVel(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                if (!VehicleParts.IsSecondStage(v.parts[i].name)) continue;
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (es[m].EngineIgnited && !es[m].flameout && es[m].realIsp > 1.0)
                        return es[m].realIsp * 9.80665;
            }
            return 345.0 * 9.80665;
        }

        // ---- SECOND-STAGE IGNITION: RO-CORRECT, BY CAPABILITY ----
        private const int MaxS2IgnitionAttempts = 3;
        private const double S2SettleBeforeIgniteS = 2.0;
        private const double S2IgniteRetryGapS = 1.5;
        private static int s2IgnitionAttempts;
        private static double lastS2IgniteAt = -99.0;

        public static double S2Ullage = -1.0;
        private static double lastUllageLog = -99.0;

        private static void IgniteSecondStageWhenSettled(Vessel v, AscentCommand c, AscentInputs a)
        {
            if (c.Phase != AscentPhase.BurnToApoapsis) return;
            if (a.PhaseElapsedS < S2SettleBeforeIgniteS) return;
            if (a.AvailableThrust > 1.0) return;
            if (s2IgnitionAttempts >= MaxS2IgnitionAttempts) return;
            if (Planetarium.GetUniversalTime() - lastS2IgniteAt < S2IgniteRetryGapS) return;

            // ---- ⛔ LIGHT ONLY WHEN THE PROPELLANT IS ACTUALLY SETTLED (RealFuels LIVE ullage). ----
            int ullageN;
            S2Ullage = UllageProbe.VesselWorst(v, delegate(Part p) { return VehicleParts.IsSecondStage(p.name); },
                                               out ullageN);
            if (ullageN > 0 && S2Ullage >= 0.0 && S2Ullage < UllageProbe.SettledStability)
            {
                double now = Planetarium.GetUniversalTime();
                if (now - lastUllageLog > 3.0)
                {
                    lastUllageLog = now;
                    Debug.Log(Tag + "MVac HOLDING for ullage - propellant "
                              + (S2Ullage * 100.0).ToString("F0") + "% settled, need "
                              + (UllageProbe.SettledStability * 100.0).ToString("F0")
                              + "%. RCS still settling; not spending an ignition.");
                }
                return;
            }

            int lit = IgniteSecondStage(v);
            lastS2IgniteAt = Planetarium.GetUniversalTime();
            if (lit > 0)
            {
                s2IgnitionAttempts++;
                Debug.Log(Tag + "MVac ignition attempt " + s2IgnitionAttempts + "/" + MaxS2IgnitionAttempts
                              + " - activated " + lit + " engine module(s) after ullage settle");
            }
        }

        private static int IgniteSecondStage(Vessel v)
        {
            int lit = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                if (!VehicleParts.IsSecondStage(v.parts[i].name)) continue;
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                {
                    ModuleEngines e = es[m];
                    if (e.EngineIgnited && e.flameout) e.Shutdown();
                    if (!e.EngineIgnited) { e.Activate(); lit++; }
                }
            }
            return lit;
        }

        private static bool SeparateBooster(Vessel v)
        {
            if (boosterSeparated) return true;
            int n = 0, inactive = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (!VehicleParts.IsInterstage(p.name)) continue;
                for (int mod = 0; mod < p.Modules.Count; mod++)
                {
                    PartModule pm = p.Modules[mod];
                    for (int e = 0; e < pm.Events.Count; e++)
                    {
                        BaseEvent ev = pm.Events[e];
                        if (ev == null || ev.guiName == null) continue;
                        if (!string.Equals(ev.guiName, "decouple", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!ev.active) { inactive++; continue; }
                        ev.Invoke();
                        n++;
                    }
                }
            }
            if (n == 0)
            {
                for (int i = 0; i < v.parts.Count; i++)
                {
                    Part p = v.parts[i];
                    if (!VehicleParts.IsInterstage(p.name)) continue;
                    for (int mod = 0; mod < p.Modules.Count; mod++)
                    {
                        PartModule pm = p.Modules[mod];
                        for (int act = 0; act < pm.Actions.Count; act++)
                        {
                            BaseAction ac = pm.Actions[act];
                            if (ac == null || ac.guiName == null) continue;
                            if (!string.Equals(ac.guiName, "decouple", StringComparison.OrdinalIgnoreCase))
                                continue;
                            ac.Invoke(new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate));
                            n++;
                        }
                    }
                }
            }
            if (n > 0) { boosterSeparated = true; return true; }
            if (inactive > 0)
                Debug.LogWarning(Tag + "booster sep: '" + VehicleParts.InterstageMarker
                                     + "' decouple event(s) all inactive and no action answered");
            return false;
        }

        private static bool boosterSeparated;

        private static void Steer(Vessel v, AscentCommand c)
        {
            Vector3d up = (v.CoM - v.mainBody.position).normalized;
            Vector3d north = Vector3d.Exclude(up, v.mainBody.transform.up).normalized;
            Vector3d east = Vector3d.Cross(up, north).normalized;

            double hdg = c.HeadingDeg * Math.PI / 180.0;
            double pit = c.PitchDeg * Math.PI / 180.0;

            Vector3d horizontal = north * Math.Cos(hdg) + east * Math.Sin(hdg);
            Vector3d dir = horizontal * Math.Cos(pit) + up * Math.Sin(pit);

            if (c.Phase == AscentPhase.Coast)
            {
                Vector3d pro = v.obt_velocity.normalized;
                if (pro.sqrMagnitude > 0.5) dir = pro;
            }
            // ---- ⛔ RSS/RO SECOND STAGE: LOFT ABOVE PROGRADE, THEN FLATTEN. ----
            else if (c.Phase == AscentPhase.BurnToApoapsis)
            {
                Vector3d pro = v.obt_velocity.normalized;
                if (pro.sqrMagnitude > 0.5)
                {
                    double deficit = (Target.AltitudeM - v.orbit.ApA) / Target.AltitudeM;
                    if (deficit < 0.0) deficit = 0.0;
                    double loftDeg = deficit * S2LoftGainDeg;
                    if (loftDeg > S2MaxLoftDeg) loftDeg = S2MaxLoftDeg;
                    Vector3d upPerp = up - Vector3d.Project(up, pro);
                    if (loftDeg > 0.05 && upPerp.sqrMagnitude > 1e-6)
                    {
                        double l = loftDeg * Math.PI / 180.0;
                        dir = (pro * Math.Cos(l) + upPerp.normalized * Math.Sin(l)).normalized;
                    }
                    else dir = pro;
                }
            }
            else if (c.Phase == AscentPhase.Circularise)
            {
                if (circDv.sqrMagnitude > 0.01) dir = circDv.normalized;
                else dir = v.obt_velocity.normalized;
            }

            // ---- NEVER CALL SetMode HERE. IT UNDOES THE LINE BELOW IT. ----
            // ---- OUR OWN CONTROLLER, NOT SAS ----
            // ---- ⛔ AND DURING VERTICAL FLIGHT THE LOCAL VERTICAL IS NOT A ROLL REFERENCE. ----
            Vector3d upHint = up;
            if (Math.Abs(Vector3d.Dot(up, dir)) > 0.999) upHint = -horizontal;
            AttitudeController.Ascent.SteerTo(v, dir, upHint);
            lastCommanded = dir;
        }

        private static Vector3d lastCommanded = Vector3d.zero;

        private static bool PilotInput(Vessel v)
        {
            if (v == null || FlightGlobals.ActiveVessel != v) return false;
            try
            {
                if (Mathf.Abs(GameSettings.AXIS_PITCH.GetAxis()) > 0.2f) return true;
                if (Mathf.Abs(GameSettings.AXIS_YAW.GetAxis()) > 0.2f) return true;
                if (Mathf.Abs(GameSettings.AXIS_ROLL.GetAxis()) > 0.2f) return true;
                return GameSettings.PITCH_UP.GetKey() || GameSettings.PITCH_DOWN.GetKey()
                    || GameSettings.YAW_LEFT.GetKey() || GameSettings.YAW_RIGHT.GetKey();
            }
            catch (Exception) { return false; }
        }

        private static Vector3d circDv, circDvStart;
        private static double phaseStartedAt;

        private static Vessel ascentVessel;

        public static Vessel AscentVessel { get { return ascentVessel; } }
        private static bool packedReported;
        private static double lastHandoverTry;
        private static double windowOpensUt, liftoffUt, liftoffLonDeg;
        private const double WindowWarpLeadS = 5.0;
        private static bool windowWarped;
        private static float lastBurnLog = -999f;

        private static Vector3d CirculariseDv(Vessel v)
        {
            if (v.mainBody == null || v.orbit == null) return Vector3d.zero;
            Vector3d r = v.CoM - v.mainBody.position;
            Vector3d vel = v.obt_velocity;
            double mag = Math.Sqrt(v.mainBody.gravParameter / r.magnitude);
            Vector3d horiz = Vector3d.Exclude(r, vel);
            if (horiz.sqrMagnitude < 1e-6) return Vector3d.zero;
            return horiz.normalized * mag - vel;
        }

        private static bool HasBooster(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
                if (VehicleParts.IsBooster(v.parts[i].name)) return true;
            return false;
        }

        /// ---- WHY THE ORBIT IS NOT FINISHED ON THE MVac ----
        private static void SeparateSecondStage(Vessel v)
        {
            // ---- ⛔ THE CAPSULE IS NOT THE LAUNCH VEHICLE. GIVE IT ITS OWN RATE CEILING. ----
            AttitudeController.Ascent.MaxRateDps = Attitude.CapsuleMaxRateDps;
            if (s2Separated) return;
            s2Separated = true;

            // ---- ⛔ SHUT THE M-VAC DOWN BEFORE DROPPING THE S2. ----
            for (int i = 0; i < v.parts.Count; i++)
            {
                if (!VehicleParts.IsSecondStage(v.parts[i].name)) continue;
                System.Collections.Generic.List<ModuleEngines> se =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < se.Count; m++)
                    if (se[m].EngineIgnited) se[m].Shutdown();
            }

            bool fired = false;
            for (int i = 0; i < v.parts.Count && !fired; i++)
            {
                Part p = v.parts[i];
                if (!VehicleParts.IsDragonDecoupler(p.name)) continue;
                System.Collections.Generic.List<ModuleDecouple> ds =
                    p.Modules.GetModules<ModuleDecouple>();
                for (int m = 0; m < ds.Count; m++)
                {
                    if (ds[m].isDecoupled) continue;
                    ds[m].Decouple();
                    fired = true;
                    Debug.Log(Tag + "S2 SEP - dropped on '" + p.name + "' at "
                              + (v.orbit.ApA / 1000.0).ToString("F1") + " x "
                              + (v.orbit.PeA / 1000.0).ToString("F1") + " km");
                    break;
                }
            }

            if (!fired)
            {
                Debug.LogWarning(Tag + "S2 SEP FAILED - no undecoupled '"
                                     + VehicleParts.DragonDecouplerMarker + "' on this vehicle. "
                                     + "Circularising STACKED; de-orbit and entry will not work.");
                return;
            }

            int lit = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                if (!VehicleParts.IsPod(v.parts[i].name)) continue;
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (!es[m].EngineIgnited && !es[m].flameout) { es[m].Activate(); lit++; }
            }
            Debug.Log(Tag + "Dracos armed (" + lit + " engine module(s)) - the capsule closes its "
                          + "own orbit from here");

            // ---- ⛔ DEPLOY THE SOLAR PANELS. Without this the battery drains to zero. ----
            DeploySolarPanels(v);
            // ---- AND ACTIVATE THE TRUNK RADIATOR. The real Crew Dragon runs its radiators in orbit for
            int rad = VehicleControl.SetRadiators(v, true);
            if (rad > 0) Debug.Log(Tag + "trunk radiator(s) activated (" + rad + ") - thermal control on");
        }

        private static void DeploySolarPanels(Vessel v)
        {
            if (v == null) return;
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleDeployableSolarPanel> ps =
                    v.parts[i].Modules.GetModules<ModuleDeployableSolarPanel>();
                for (int m = 0; m < ps.Count; m++)
                {
                    ModuleDeployableSolarPanel p = ps[m];
                    if (p.useAnimation && p.deployState != ModuleDeployablePart.DeployState.EXTENDED)
                    { p.Extend(); n++; }
                }
            }
            if (n > 0) Debug.Log(Tag + "solar panels deployed (" + n + ") - closing the power budget");
        }

        private static bool s2Separated;

        public static double Range(Vessel a, Vessel b)
        {
            if (a == null || b == null || a == b) return 0.0;
            if (a.state == Vessel.State.DEAD || b.state == Vessel.State.DEAD) return 0.0;
            return Vector3d.Distance(a.CoM, b.CoM);
        }

        private static double AvailableThrust(Vessel v)
        {
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                {
                    ModuleEngines e = es[m];
                    if (!e.isEnabled || e.flameout) continue;
                    if (!e.EngineIgnited) continue;
                    t += e.finalThrust;
                }
            }
            return t;
        }

        private static double MaxThrust(Vessel v)
        {
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                {
                    ModuleEngines e = es[m];
                    if (!e.isEnabled || e.flameout || !e.EngineIgnited) continue;
                    t += e.MaxThrustOutputVac(true);
                }
            }
            return t;
        }

        private static void Stage(Vessel v, AscentCommand c, AscentInputs a)
        {
            // ---- ⛔ IN RSS/RO, STARVATION STAGING IS FOR THE PAD START ONLY. ----
            if (c.Phase != AscentPhase.VerticalRise && c.Phase != AscentPhase.Idle) return;
            if (c.Throttle < 0.05) { starvedFor = 0.0; return; }

            if (a.AvailableThrust > 0.1)
            {
                starvedFor = 0.0;
                blindStages = 0;
                return;
            }
            starvedFor += Time.deltaTime;

            if (starvedFor < 0.5) return;
            if (Planetarium.GetUniversalTime() - lastStageAt < 2.0) return;
            if (StageManager.CurrentStage <= 0) { Disengage("out of stages"); return; }

            // ---- STOP AFTER TWO STAGINGS THAT PRODUCE NOTHING ----
            if (blindStages >= 2)
            {
                Disengage("staged twice with no thrust - nothing left to light");
                return;
            }

            lastStageAt = Planetarium.GetUniversalTime();
            starvedFor = 0.0;
            blindStages++;
            StageManager.ActivateNextStage();
            Debug.Log(Tag + "autopilot staged - now stage " + StageManager.CurrentStage
                      + (blindStages > 1 ? "  (no thrust from the last one)" : ""));
        }

        private static int blindStages;
    }
}
