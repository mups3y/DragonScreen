// DragonScreen - DeorbitOps
// ---- ⛔ THIS REPLACES `FlightCommands.StartDeorbit`, IT DOES NOT EXTEND IT ----
// ---- WHAT F9I NEEDED THAT WE HAD TO BUILD ----
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class DeorbitOps
    {
        private const string Tag = "[DragonScreen] ";

        public static bool Engaged { get; private set; }
        public static string Note = "-";
        public static double AimMissM = -1.0, ThrottleCmd, PeriapsisM;

        public static Vessel Vehicle { get { return ship; } }

        private static Vessel ship;
        private static DeorbitState st;
        private static double startedAt, lastScanAt, prevPeri, prevPeriAt;
        private static double ignitedAt;
        private static double aligningSinceAt;
        private static bool aligned, phaseDownPending, passFound, warpRequested;

        private static double impactLat, impactLon;
        private static bool haveImpact;
        private static double aimAlongM, aimCrossM;

        public const double AimAlongTolM = 1500.0;
        public const double AimCrossTolM = 800.0;

        public const double PassWarpLeadS = 25.0;
        private static double goTimeUt, trackMissM, offPlaneDeg;

        [Tunable] public static double IgnitionAimPeriapsisM = DeorbitBurn.PeriapsisTargetM;

        public const double IgnitionWindowFrac = 0.9;

        private static double ignAimRp;

        public static double SecondsToIgnition
        {
            get { return passFound ? goTimeUt - Planetarium.GetUniversalTime() : -1.0; }
        }
        public static double PassTrackMissM { get { return trackMissM; } }
        public static double PassOffPlaneDeg { get { return offPlaneDeg; } }

        public static double TargetLatDeg = LandingSites.Lz1.LatDeg;
        public static double TargetLonDeg = LandingSites.Lz1.LonDeg;

        // ---- RSS/RO SPLASHDOWN: Crew-2 came down in the Gulf off Pensacola, FL ----
        [Tunable] public static double SplashdownEarthLatDeg = 29.8;
        [Tunable] public static double SplashdownEarthLonDeg = -87.3;

        public static void Toggle()
        {
            if (Engaged) Disengage("crew"); else Engage();
        }

        public static void Engage()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            // ---- ⚠ A SECOND PRESS IS NOT A SECOND DE-ORBIT. ----
            if (Engaged)
            {
                Debug.Log(Tag + "DE-ORBIT already running - " + Note + ". Press ignored; use the "
                          + "same button again only after it has finished or been cancelled.");
                return;
            }

            // ---- CAPSULE TARGET FOLLOWS THE LANDING METHOD (user 2026-08-21) ----
            if (EntryOps.PropulsiveRequested)
            {
                TargetLatDeg = LandingSites.Lz1.LatDeg;
                TargetLonDeg = LandingSites.Lz1.LonDeg;
            }
            else
            {
                TargetLatDeg = SplashdownEarthLatDeg;
                TargetLonDeg = SplashdownEarthLonDeg;
            }

            // ---- IS A RETURN EVEN MEANINGFUL FROM HERE? `StReturnAllowed`. ----
            string why;
            bool down = v.situation == Vessel.Situations.LANDED
                     || v.situation == Vessel.Situations.SPLASHED
                     || v.situation == Vessel.Situations.PRELAUNCH;
            if (!ReturnBudget.ReturnAllowed(down, v.altitude, v.orbit != null ? v.orbit.PeA : -1.0,
                                            v.mainBody.atmosphereDepth, out why))
            {
                Debug.LogWarning(Tag + "DE-ORBIT refused - " + why);
                Note = "REFUSED - " + why;
                return;
            }

            // ---- AND THE SECOND STAGE MUST BE GONE ----
            System.Collections.Generic.List<Part> mine = DockedSide.Ours(v);
            for (int i = 0; i < mine.Count; i++)
            {
                if (!VehicleParts.IsSecondStage(mine[i].name)) continue;
                Note = "REFUSED - the second stage is still attached";
                Debug.LogWarning(Tag + "DE-ORBIT " + Note);
                return;
            }

            // ---- ⛔ THE PROPELLANT GATE GOES HERE, BEFORE ANYTHING MOVES. ----
            BudgetInputs gate = Budget(v);
            BudgetReport gateRep = ReturnBudget.Report(gate);
            bool unlimited = CheatOptions.InfinitePropellant;
            if (!unlimited && gateRep.EntryInterfaceUnits > 0.0
                && gateRep.HaveUnits < gateRep.EntryInterfaceUnits + DeorbitBurn.MonoFloorUnits)
            {
                Note = "REFUSED - " + gateRep.HaveUnits.ToString("F1") + " units of monopropellant "
                     + "cannot reach the atmosphere; that needs "
                     + gateRep.EntryInterfaceUnits.ToString("F1");
                Debug.LogError(Tag + Note + ". Nothing engaged and nothing burned - the capsule is "
                             + "left in the orbit it is in. Dock and refuel.");
                return;
            }

            // ---- ⛔ THE DE-ORBIT OWNS THE VEHICLE ALONE ----
            if (AutoPilot.Engaged) AutoPilot.Disengage("de-orbit takes the vehicle");
            if (StationApproach.Engaged) StationApproach.Disengage("de-orbit takes the vehicle");
            if (DirectApproachOps.Engaged) DirectApproachOps.Disengage("de-orbit takes the vehicle");
            if (DockingOps.Engaged) DockingOps.Reset();
            if (UndockOps.Engaged) UndockOps.Reset();

            ship = v;
            Engaged = true;
            DockShroud.Open(v);
            aligned = false;
            haveImpact = false;
            startedAt = Planetarium.GetUniversalTime();
            lastScanAt = 0.0;
            prevPeri = v.orbit.PeA;
            prevPeriAt = startedAt;

            // ---- ⛔ PHASE DOWN FIRST, OR EVERY AIM CONSTANT BELOW IS DESCRIBING A DIFFERENT ORBIT ----
            phaseDownPending = false;
            passFound = false; warpRequested = false;
            trackMissM = 0.0; offPlaneDeg = 0.0; goTimeUt = 0.0;
            bool landingOrbitValidHere = DeorbitOrbit.TargetPeriapsisM > v.mainBody.atmosphereDepth;
            if (landingOrbitValidHere && !DeorbitOrbit.AlreadyOnOrbit(v.orbit.ApA, v.orbit.PeA))
            {
                PhaseDownOps.Engage(v);
                phaseDownPending = !PhaseDownOps.Finished;
            }
            else if (!landingOrbitValidHere)
            {
                Debug.Log(Tag + "phase-down skipped - the " + (DeorbitOrbit.TargetPeriapsisM / 1000.0).ToString("F0")
                    + " km landing orbit is below this body's " + (v.mainBody.atmosphereDepth / 1000.0).ToString("F0")
                    + " km atmosphere (RSS Earth), so it is not a valid orbit here. De-orbiting closed-loop "
                    + "from the current orbit, as the real Crew Dragon does.");
            }

            st = new DeorbitState();
            st.AimMissM = -1.0;
            st.BestMissM = 9.9e12;
            st.UsingS2 = false;

            BudgetInputs b = Budget(v);
            BudgetReport rep = ReturnBudget.Report(b);
            Debug.Log(Tag + "DE-ORBIT engaged - target " + TargetLatDeg.ToString("F4") + ", "
                      + TargetLonDeg.ToString("F4") + ". Mono budget: " + rep.Line);
            if (rep.HaveUnits < rep.DeorbitUnits + DeorbitBurn.MonoFloorUnits)
                Debug.LogWarning(Tag + "⚠ " + rep.HaveUnits.ToString("F1") + " units will reach "
                                 + "the atmosphere but not the " + rep.DeorbitUnits.ToString("F1")
                                 + " unit aim point - the entry starts shallow and the landing will "
                                 + "miss long.");
            else if (!rep.Sufficient)
                Debug.LogWarning(Tag + "⚠ MONOPROP SHORT by "
                                 + (-rep.MarginUnits).ToString("F1")
                                 + " units - the de-orbit will finish but the entry and landing "
                                 + "reserve is not there; expect the landing to miss.");
        }

        public static void Disengage(string why)
        {
            if (!Engaged) return;
            Engaged = false;
            AttitudeController.Ascent.Throttle = 0.0;
            AttitudeController.Ascent.UllageFore = 0.0;
            CapsuleRcs.Forget();
            AttitudeController.Ascent.Release(ship);
            ship = null;
            Debug.Log(Tag + "DE-ORBIT disengaged - " + why);
        }

        public static void Reset()
        {
            Engaged = false; ship = null; Note = "-"; phaseDownPending = false;
            passFound = false; warpRequested = false; haveImpact = false;
            goTimeUt = 0.0; trackMissM = 0.0; offPlaneDeg = 0.0;
            AimMissM = -1.0; ThrottleCmd = 0.0; PeriapsisM = 0.0;
        }

        private static BudgetInputs Budget(Vessel v)
        {
            BudgetInputs b = new BudgetInputs();
            b.MonoUnits = Mono(v);
            b.MassT = v.GetTotalMass();
            b.ApoapsisM = v.orbit.ApA;
            b.SmaM = v.orbit.semiMajorAxis;
            b.BodyRadiusM = v.mainBody.Radius;
            b.Mu = v.mainBody.gravParameter;
            b.S2Attached = false;
            b.Mode = LandingMode.Parachute;
            return b;
        }

        // ------------------------------------------------------------------ the loop

        public static void Tick()
        {
            if (!Engaged) return;
            if (ship == null || ship.state == Vessel.State.DEAD) { Disengage("vessel lost"); return; }

            double now = Planetarium.GetUniversalTime();
            PeriapsisM = ship.orbit != null ? ship.orbit.PeA : 0.0;

            if (phaseDownPending)
            {
                if (!PhaseDownOps.Finished)
                {
                    Note = "PHASE-DOWN - " + PhaseDownOps.Note;
                    return;
                }
                phaseDownPending = false;
                startedAt = now;
                aligningSinceAt = now;
                prevPeri = PeriapsisM;
                prevPeriAt = now;
                Debug.Log(Tag + "phase-down settled (" + PhaseDownOps.Stage
                          + ") - finding the de-orbit point from "
                          + (ship.orbit.ApA / 1000.0).ToString("F1") + " x "
                          + (ship.orbit.PeA / 1000.0).ToString("F1") + " km");
            }

            // ---- ⛔ WHERE IN THE ORBIT WE BURN IS NOT A DETAIL. IT IS THE LANDING. ----
            if (!passFound) { FindPass(now); return; }
            if (now < goTimeUt)
            {
                AttitudeController.Ascent.Throttle = 0.0;
                AttitudeController.Ascent.SteerTo(ship, -ship.obt_velocity.normalized, Vector3d.zero);

                // ---- AND WARP THERE. The pass can be five orbits away. ----
                if (!warpRequested && goTimeUt - PassWarpLeadS - now > NodeExecutor.WarpWorthwhileS)
                {
                    warpRequested = true;
                    Debug.Log(Tag + "warping " + (goTimeUt - PassWarpLeadS - now).ToString("F0")
                              + " s to the de-orbit point");
                    TimeWarp.fetch.WarpTo(goTimeUt - PassWarpLeadS);
                }
                else if (goTimeUt - now <= PassWarpLeadS && TimeWarp.CurrentRateIndex > 0)
                {
                    TimeWarp.SetRate(0, true);
                }

                Note = "WAITING FOR THE DE-ORBIT POINT - T-" + (goTimeUt - now).ToString("F0")
                     + " s, track miss " + (trackMissM / 1000.0).ToString("F1") + " km";
                return;
            }

            // ---- ⛔ ONE CONTINUOUS BURN: RETROGRADE UNTIL THERE IS AN IMPACT, THEN AIM IT. (user, 2026-08-20) ----
            Vector3d retro = -ship.obt_velocity.normalized;
            double off = Vector3d.Angle(ship.ReferenceTransform.up, retro);

            if (!aligned)
            {
                AttitudeController.Ascent.SteerTo(ship, retro, Vector3d.zero);
                AttitudeController.Ascent.Throttle = 0.0;
                Note = "ALIGNING - " + off.ToString("F1") + " deg";
                if (aligningSinceAt <= 0.0) aligningSinceAt = now;
                if (off < DeorbitBurn.AlignedDeg || now - aligningSinceAt > 30.0)
                {
                    aligned = true;
                    ignitedAt = now;
                    Debug.Log(Tag + "de-orbit ignition, " + off.ToString("F1") + " deg off retrograde");
                }
                return;
            }

            // ---- ⛔ THE DEPTH TEST IS POLLED EVERY TICK; THE AIM SCAN IS NOT. ----
            double dt = now - prevPeriAt;
            if (dt > 0.1)
            {
                st.PeriapsisRateMps = (PeriapsisM - prevPeri) / dt;
                prevPeri = PeriapsisM;
                prevPeriAt = now;
            }
            st.PeriapsisM = PeriapsisM;
            st.ElapsedS = now - ignitedAt;
            st.MonoUnits = DockedSide.Mono(ship);

            if (now - lastScanAt >= DeorbitBurn.AimScanIntervalS)
            {
                lastScanAt = now;
                // ---- ⛔ DRAG-AWARE MISS, NOT VACUUM. AIM THE BURN DIRECTLY AT THE LZ (2026-08-19). ----
                Impact aim = ImpactPredictor.Predict(ship, EntryGuidance.CapsuleBcKgM2);
                haveImpact = aim.Valid;
                if (aim.Valid)
                {
                    impactLat = aim.LatDeg; impactLon = aim.LonDeg;
                    double miss;
                    Orbital.DownCross(ship.mainBody.Radius, ship.latitude, ship.longitude,
                                      impactLat, impactLon, TargetLatDeg, TargetLonDeg,
                                      out aimAlongM, out aimCrossM, out miss);
                    st.AimMissM = miss;
                }
                else { st.AimMissM = -1.0; }
                AimMissM = st.AimMissM;
                DeorbitBurn.Track(ref st);
            }

            // ---- ⛔ BURN TO THE ENTRY PERIAPSIS - THE AIMING IS RCS'S JOB, NOT THE ENGINE'S. ----
            string why = "";
            bool stop = DeorbitBurn.DepthLimitReached(st.PeriapsisM, st.PeriapsisRateMps)
                     || (!CheatOptions.InfinitePropellant && st.MonoUnits < DeorbitBurn.MonoFloorUnits)
                     || st.ElapsedS > DeorbitBurn.MaxBurnS;
            if (DeorbitBurn.DepthLimitReached(st.PeriapsisM, st.PeriapsisRateMps)) why = "reached entry depth";
            else if (!CheatOptions.InfinitePropellant && st.MonoUnits < DeorbitBurn.MonoFloorUnits) why = "ABORTED - out of monopropellant";
            else if (st.ElapsedS > DeorbitBurn.MaxBurnS) why = "ABORTED - burn ran past its backstop";

            if (stop)
            {
                AttitudeController.Ascent.Throttle = 0.0;
                ThrottleCmd = 0.0;
                Debug.Log(Tag + "de-orbit burn complete - " + why + ". Pe "
                          + (PeriapsisM / 1000.0).ToString("F1") + " km, along "
                          + (aimAlongM / 1000.0).ToString("F2") + " km, cross "
                          + (aimCrossM / 1000.0).ToString("F2") + " km");
                Note = "BURN COMPLETE - " + why;

                // ---- ⛔ THE ENTRY IS HANDED A RE-ENTRY TRAJECTORY, OR IT IS NOT HANDED ANYTHING. ----
                double atmoTopM = (ship.mainBody != null && ship.mainBody.atmosphere)
                                ? ship.mainBody.atmosphereDepth : 0.0;
                if (PeriapsisM > atmoTopM)
                {
                    Note = "DE-ORBIT FAILED - " + why + ", periapsis still "
                         + (PeriapsisM / 1000.0).ToString("F1") + " km";
                    Debug.LogError(Tag + Note + " - above the " + (atmoTopM / 1000.0).ToString("F0")
                                 + " km atmosphere, so this is not a re-entry trajectory. The entry "
                                 + "sequence is NOT engaged and the trunk is NOT jettisoned. The "
                                 + "capsule is in a stable orbit; fix the propellant and retry.");
                    Disengage(why);
                    return;
                }

                EntryOps.TargetLatDeg = TargetLatDeg;
                EntryOps.TargetLonDeg = TargetLonDeg;
                EntryOps.Engage(ship);

                Disengage(why);
                return;
            }

            // ---- ⛔ DEEPEN TO THE ENTRY PERIAPSIS, EASING NEAR IT; CANCEL CROSS ON THE WAY. ----
            Vector3d aimSteer = retro;
            if (haveImpact)
            {
                Vector3d cn = CrossNullDir(retro);
                Vector3d blended = retro + DeorbitBurn.CrossBlend * cn;
                if (blended.sqrMagnitude > 1e-9) aimSteer = blended.normalized;
            }
            double depthAbove = st.PeriapsisM - DeorbitBurn.PeriapsisTargetM;
            double th = depthAbove / DeorbitBurn.DepthEaseM;
            if (th > DeorbitBurn.DepthThrottleMax) th = DeorbitBurn.DepthThrottleMax;
            if (th < DeorbitBurn.ThrottleMin) th = DeorbitBurn.ThrottleMin;
            ThrottleCmd = haveImpact ? th : DeorbitBurn.ThrottleBlind;
            AttitudeController.Ascent.SteerTo(ship, aimSteer, Vector3d.zero);
            AttitudeController.Ascent.Throttle = 0.0;
            CapsuleRcs.Set(ship, CapsuleRcs.BurnPct * ThrottleCmd);
            AttitudeController.Ascent.UllageFore = (ThrottleCmd > 0.0) ? 1.0 : 0.0;
            if (!ship.ActionGroups[KSPActionGroup.RCS])
                ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

            Note = "DE-ORBIT - " + (haveImpact
                     ? ("along " + (aimAlongM / 1000.0).ToString("F1") + " / cross "
                        + (aimCrossM / 1000.0).ToString("F1") + " km")
                     : "acquiring impact")
                 + ", Pe " + (PeriapsisM / 1000.0).ToString("F1") + " km, thr "
                 + (ThrottleCmd * 100.0).ToString("F0") + "%";
        }

        private static Vector3d VectoredDir(Vector3d retro)
        {
            CelestialBody b = ship.mainBody;
            if (b == null) return retro;
            Vector3d up = (ship.CoM - b.position).normalized;
            Vector3d impactPos = (Vector3d)b.GetWorldSurfacePosition(impactLat, impactLon, 0.0) - b.position;
            Vector3d lzPos = (Vector3d)b.GetWorldSurfacePosition(TargetLatDeg, TargetLonDeg, 0.0) - b.position;
            Vector3d toLZ = Vector3d.Exclude(up, lzPos - impactPos);
            if (toLZ.sqrMagnitude < 1e-6) return retro;

            double g = AimMissM / DeorbitBurn.VectorScaleM;
            if (g > DeorbitBurn.VectorMaxGain) g = DeorbitBurn.VectorMaxGain;
            if (g < 0.0) g = 0.0;
            Vector3d steer = retro + g * toLZ.normalized;
            return (steer.sqrMagnitude > 1e-9) ? steer.normalized : retro;
        }

        private static Vector3d CrossNullDir(Vector3d retro)
        {
            CelestialBody b = ship.mainBody;
            if (b == null) return retro;
            Vector3d up = (ship.CoM - b.position).normalized;
            Vector3d vhat = ship.obt_velocity.normalized;
            Vector3d normal = Vector3d.Cross(up, vhat);
            if (normal.sqrMagnitude < 1e-9) return retro;
            normal = normal.normalized;

            Vector3d impactPos = (Vector3d)b.GetWorldSurfacePosition(impactLat, impactLon, 0.0) - b.position;
            Vector3d lzPos = (Vector3d)b.GetWorldSurfacePosition(TargetLatDeg, TargetLonDeg, 0.0) - b.position;
            double crossErr = Vector3d.Dot(lzPos - impactPos, normal);
            return (crossErr >= 0.0) ? normal : -normal;
        }

        private static void FindPass(double now)
        {
            CelestialBody b = ship.mainBody;
            Orbit o = ship.orbit;
            if (o == null || o.period <= 0.0)
            {
                passFound = true;
                goTimeUt = now;
                Debug.LogWarning(Tag + "no usable orbit for an overflight search - de-orbiting now");
                return;
            }

            searchShip = ship;
            searchLag = Overflight.LandLagS(o.period);
            searchNow = now;
            OverflightResult r = Overflight.Search(now, o.period, new TrackMissAtUt(MissAt));

            trackMissM = r.TrackMissM;

            // ---- ⛔ OVERFLIGHT PICKS THE PASS (cross-track); THE ADAPTIVE SEARCH PICKS THE LEAD. ----
            goTimeUt = Overflight.GoTimeUt(r.Ut, now, o.period);
            passFound = true;

            Vector3d r1 = o.getPositionAtUT(r.Ut) - b.position;
            Vector3d r2 = o.getPositionAtUT(r.Ut + 60.0) - b.position;
            Vector3d n = Vector3d.Cross(r1, r2).normalized;
            Vector3d np = b.position + n * b.Radius;
            offPlaneDeg = Overflight.OffPlaneDeg(b.GetLatitude(np), b.GetLongitude(np),
                                                 TargetLatDeg,
                                                 Overflight.SiteLonAtDeg(TargetLonDeg,
                                                                         (r.Ut + searchLag) - now,
                                                                         b.rotationPeriod));

            Debug.Log(Tag + "de-orbit point: pass in " + r.InS.ToString("F0")
                      + " s, track miss " + (trackMissM / 1000.0).ToString("F1")
                      + " km, landing lag " + searchLag.ToString("F0")
                      + " s, off-plane at touchdown " + offPlaneDeg.ToString("F2")
                      + " deg (" + (Overflight.CrossTrackFromOffPlaneM(offPlaneDeg, b.Radius) / 1000.0)
                          .ToString("F1") + " km of cross). Ignition in "
                      + (goTimeUt - now).ToString("F0") + " s.");

            if (Math.Abs(offPlaneDeg) > Overflight.PlaneToleranceDeg)
                Debug.Log(Tag + "note: " + offPlaneDeg.ToString("F2")
                          + " deg out of plane and no plane change is flown (F9I disabled its own "
                          + "after flights 082-100). The entry's cross-track loop absorbs this on a "
                          + "near-equatorial orbit - flight 080 touched down at -5 m of cross.");
        }

        private static Vessel searchShip;
        private static double searchLag, searchNow;

        private static double MissAt(double ut)
        {
            CelestialBody b = searchShip.mainBody;
            Vector3d p = searchShip.orbit.getPositionAtUT(ut);
            return Overflight.TrackMissM(b.Radius, b.rotationPeriod,
                                         b.GetLatitude(p), b.GetLongitude(p),
                                         (ut - searchNow) + searchLag,
                                         TargetLatDeg, TargetLonDeg);
        }

        private static double AdaptiveIgnition(double now, double passUt, double period, double nominalGo)
        {
            CelestialBody b = searchShip.mainBody;
            ignAimRp = b.Radius + IgnitionAimPeriapsisM;

            if (!IgnitionFrameOk(now))
            {
                Debug.LogWarning(Tag + "adaptive de-orbit ignition DISABLED - orbit-frame swizzle check "
                               + "failed; using the fixed PhaseArcFrac lead.");
                return nominalGo;
            }

            double lo = passUt - period * IgnitionWindowFrac;
            double hi = passUt - Overflight.GoTimeMarginS;
            if (lo < now + Overflight.GoTimeMarginS) lo = now + Overflight.GoTimeMarginS;

            double bestUt;
            IgnitionMiss best = DeorbitPoint.Search(lo, hi, new IgnitionMissAtUt(IgnitionMissAt),
                                                    out bestUt);
            if (!best.Ok)
            {
                Debug.LogWarning(Tag + "adaptive de-orbit ignition found no landing in the window - "
                               + "using the fixed PhaseArcFrac lead instead.");
                return nominalGo;
            }

            Debug.Log(Tag + "adaptive de-orbit ignition: a burn to "
                      + (best.PeriapsisM / 1000.0).ToString("F1") + " km would land "
                      + (best.MissM / 1000.0).ToString("F1") + " km from target (dv "
                      + best.DvMps.ToString("F0") + " m/s). Fixed lead was T-"
                      + (nominalGo - now).ToString("F0") + " s; adaptive is T-"
                      + (bestUt - now).ToString("F0") + " s.");
            return bestUt;
        }

        private static IgnitionMiss IgnitionMissAt(double ut)
        {
            IgnitionMiss m = new IgnitionMiss();
            CelestialBody b = searchShip.mainBody;
            Orbit o = searchShip.orbit;

            Vector3d pw = Swizzle(o.getRelativePositionAtUT(ut));
            Vector3d vw = Swizzle(o.getOrbitalVelocityAtUT(ut));

            double dv = DeorbitPoint.DvForPeriapsis(pw.x, pw.y, pw.z, vw.x, vw.y, vw.z,
                                                    b.gravParameter, ignAimRp);
            double vmag = vw.magnitude;
            Vector3d vAfter = (vmag > 1e-6) ? vw * (1.0 - dv / vmag) : vw;

            Impact im = ImpactPredictor.PredictFromState(b, pw, vAfter, EntryGuidance.CapsuleBcKgM2);
            if (!im.Valid) { m.Ok = false; return m; }

            // ---- ⛔ WIND THE IMPACT LON BACK BY THE ROTATION OVER THE IGNITION LEAD. ----
            double leadRotDeg = (ut - searchNow) * 360.0 / b.rotationPeriod;
            double lon = im.LonDeg - leadRotDeg;
            while (lon < -180.0) lon += 360.0;
            while (lon > 180.0) lon -= 360.0;

            m.Ok = true;
            m.MissM = BoosterRecovery.GroundRange(b, im.LatDeg, lon, TargetLatDeg, TargetLonDeg);
            m.DvMps = dv;
            m.PeriapsisM = ignAimRp - b.Radius;
            return m;
        }

        private static bool IgnitionFrameOk(double now)
        {
            Orbit o = searchShip.orbit;
            CelestialBody b = searchShip.mainBody;
            Vector3d pw = Swizzle(o.getRelativePositionAtUT(now));
            Vector3d vw = Swizzle(o.getOrbitalVelocityAtUT(now));
            double perr = (pw - (Vector3d)(searchShip.CoM - b.position)).magnitude;
            double verr = (vw - searchShip.obt_velocity).magnitude;
            bool ok = perr <= 5000.0 && verr <= 50.0;
            if (!ok)
                Debug.LogWarning(Tag + "⚠ ignition-frame swizzle check OFF: position error "
                               + perr.ToString("F0") + " m, velocity error " + verr.ToString("F1")
                               + " m/s.");
            return ok;
        }

        private static Vector3d Swizzle(Vector3d v) { return new Vector3d(v.x, v.z, v.y); }

        private static double Mono(Vessel v)
        {
            return DockedSide.Mono(v);
        }
    }
}
