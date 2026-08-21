/*
 * DragonScreen - DeorbitOps
 *
 * GLUE. Flies the closed-loop de-orbit burn in `pure/DeorbitBurn.cs` against the impact prediction
 * from `src/ImpactPredictor.cs`. Ported from `F9I/dragon_deorbit.ks:1328 DgDeorbitBurn`.
 *
 * ---- ⛔ THIS REPLACES `FlightCommands.StartDeorbit`, IT DOES NOT EXTEND IT ----
 * That was a plain retrograde burn to a target periapsis with no idea where it would land. The real
 * one is flown against the AIM MISS - how far the predicted impact is from the landing zone - with
 * the periapsis target demoted to a depth LIMIT the burn must not punch through. Two different
 * questions; the old code only asked one of them, and it was the less important one.
 *
 * ---- WHAT F9I NEEDED THAT WE HAD TO BUILD ----
 * `DgInitTarget` refuses to run without the Trajectories add-on: "Trajectories mod not available -
 * Dragon needs it." We do not take that dependency, so `ImpactPredictor` is the replacement, and it
 * measures the capsule's drag rather than modelling it. Everything downstream of the prediction is
 * F9I's.
 */
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

        /// <summary>
        /// The capsule flying the burn. Exposed so `FlightDriver` can keep its drag measured - the
        /// burn is steered by the impact prediction, and an unmeasured vehicle predicts in vacuum.
        /// </summary>
        public static Vessel Vehicle { get { return ship; } }

        private static Vessel ship;
        private static DeorbitState st;
        private static double startedAt, lastScanAt, prevPeri, prevPeriAt;
        /// <summary>
        /// When the engine actually lit. ⛔ NOT the same clock as <c>startedAt</c>.
        ///
        /// `DeorbitBurn.MaxBurnS` is a 300 s RUNAWAY BACKSTOP on the burn, and it was being fed
        /// `now - startedAt`, which starts when the phase-down settles - before FindPass, before the
        /// wait for the pass, and before a warp that on 2026-08-11 was 3663 seconds long. So the
        /// backstop had expired 3388 s before ignition: the burn lit at 13:52:07.241, completed at
        /// 13:52:07.260, and reported "ABORTED - burn ran past its backstop" after 19 milliseconds
        /// of thrust. A backstop measured from before the wait is a backstop on the wait.
        /// </summary>
        private static double ignitedAt;
        /// <summary>When the retrograde turn began, for the alignment timeout - same argument.</summary>
        private static double aligningSinceAt;
        private static bool aligned, phaseDownPending, passFound, warpRequested;

        /// <summary>The latest predicted impact, for the vectored steering. Valid only when haveImpact.</summary>
        private static double impactLat, impactLon;
        private static bool haveImpact;
        /// <summary>Signed along/cross of the predicted impact to the LZ, metres. along&lt;0 = LONG (past LZ).</summary>
        private static double aimAlongM, aimCrossM;

        /// <summary>Along-track within this counts as on the LZ; the burn stops deepening. Metres.</summary>
        public const double AimAlongTolM = 1500.0;
        /// <summary>Cross-track within this counts as on the LZ. Metres.</summary>
        public const double AimCrossTolM = 800.0;

        /// <summary>Drop out of warp this long before the de-orbit point, to settle. `DgPhasing`'s 25 s.</summary>
        public const double PassWarpLeadS = 25.0;
        private static double goTimeUt, trackMissM, offPlaneDeg;

        /// <summary>
        /// The periapsis (metres ASL, negative = subsurface) the ADAPTIVE IGNITION search models the
        /// de-orbit burn reaching when it chooses the ignition time. Defaults to the burn's own depth
        /// target so the modelled landing matches what the closed loop will actually fly. Tunable so
        /// the margin above the depth floor can be opened without a rebuild if the burn crowds the floor.
        /// </summary>
        [Tunable] public static double IgnitionAimPeriapsisM = DeorbitBurn.PeriapsisTargetM;

        /// <summary>How much of the lap before the pass the ignition search covers. Under 1 orbit so the
        /// along-track miss has a single minimum in the window.</summary>
        public const double IgnitionWindowFrac = 0.9;

        /// <summary>Target periapsis RADIUS for the current ignition search, metres from the centre.</summary>
        private static double ignAimRp;

        /// <summary>When the burn will be lit, and how good the pass is. For the pages and the crew.</summary>
        public static double SecondsToIgnition
        {
            get { return passFound ? goTimeUt - Planetarium.GetUniversalTime() : -1.0; }
        }
        public static double PassTrackMissM { get { return trackMissM; } }
        public static double PassOffPlaneDeg { get { return offPlaneDeg; } }

        /// <summary>Where the capsule is trying to land. Defaults to LZ-1.</summary>
        // ⛔ THE SAME POINT THE ENTRY AIMS AT, OR THE BURN AND THE ENTRY DISAGREE. The de-orbit
        // solves for an impact and the entry then trims to a target; two different coordinates would
        // have the trim fighting an error the burn introduced. Both are LZ-1, by user directive
        // (2026-08-20): "the predicted impact must be DIRECTLY ON LZ1." See EntryOps.TargetLatDeg.
        public static double TargetLatDeg = LandingSites.Lz1.LatDeg;
        public static double TargetLonDeg = LandingSites.Lz1.LonDeg;

        public static void Toggle()
        {
            if (Engaged) Disengage("crew"); else Engage();
        }

        public static void Engage()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            // ---- ⚠ A SECOND PRESS IS NOT A SECOND DE-ORBIT. ----
            // On 2026-08-11 the crew pressed DEORBIT NOW, watched nothing happen for eight minutes
            // (the phase-down was waiting on an un-warped apoapsis), and pressed it again. That
            // re-ran this whole method underneath a burn the node executor had already planned:
            // fresh phase-down, `passFound` cleared, the overflight thrown away. Re-engaging is
            // never what a second press means, so say what is happening instead.
            if (Engaged)
            {
                Debug.Log(Tag + "DE-ORBIT already running - " + Note + ". Press ignored; use the "
                          + "same button again only after it has finished or been cancelled.");
                return;
            }

            // ---- CAPSULE TARGET FOLLOWS THE LANDING METHOD (user 2026-08-21) ----
            // A PROPULSIVE landing puts the capsule exactly on LZ-1 (the pad); a PARACHUTE landing
            // splashes down ~1 km off shore of it. The method is the NORMAL/BACKUP entry selection
            // (EntryOps.PropulsiveRequested), chosen before this press. Set here, before the budget and
            // the aim use it, and propagated to EntryOps at the handover (line ~456).
            if (EntryOps.PropulsiveRequested)
            {
                TargetLatDeg = LandingSites.Lz1.LatDeg;
                TargetLonDeg = LandingSites.Lz1.LonDeg;
            }
            else
            {
                TargetLatDeg = LandingSites.Splashdown.LatDeg;
                TargetLonDeg = LandingSites.Splashdown.LonDeg;
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
            // A capsule with a spent tank on its nose cannot hold a heat shield forward. The old
            // StartDeorbit checked this too and it is the one thing worth keeping from it.
            // ⚠ OUR side: a spent stage parked on the STATION is not attached to us, and scanning
            // the merged vessel would refuse the de-orbit because of somebody else's hardware.
            System.Collections.Generic.List<Part> mine = DockedSide.Ours(v);
            for (int i = 0; i < mine.Count; i++)
            {
                if (!VehicleParts.IsSecondStage(mine[i].name)) continue;
                Note = "REFUSED - the second stage is still attached";
                Debug.LogWarning(Tag + "DE-ORBIT " + Note);
                return;
            }

            // ---- ⛔ THE PROPELLANT GATE GOES HERE, BEFORE ANYTHING MOVES. ----
            // This check used to sit AFTER `PhaseDownOps.Engage` below, so on 2026-08-11 16:12 it
            // printed "REFUSED ... Nothing burned" and then the phase-down lit its engines 1
            // millisecond later, flew both burns, and reported "ON THE LANDING ORBIT". The guard
            // fired and stopped nothing, which is worse than having no guard: the log said one thing
            // and the vehicle did another.
            //
            // ⚠ AND IT REFUSES ON THE RIGHT NUMBER NOW. It compared against `DeorbitUnits`, the
            // cost of driving periapsis to MINUS 31.8 km, and refused a capsule holding 71.1 units
            // against 78.7. Reaching the 40 km entry interface costs roughly a third of that - the
            // vehicle could have come home, and was told it could not. Refusing a de-orbit strands
            // the crew, so the bar is what is PHYSICALLY IMPOSSIBLE, not what will land badly.
            BudgetInputs gate = Budget(v);
            BudgetReport gateRep = ReturnBudget.Report(gate);
            // ⚠ AND THE FLOOR ONLY APPLIES WHEN THERE IS A BURN TO MAKE. A capsule already on an
            // entry trajectory needs ZERO units, and comparing 0.0 against a 5-unit ullage floor
            // refused it anyway: "REFUSED - 0.0 units of monopropellant cannot reach the atmosphere;
            // that needs 0.0", logged three times on 2026-08-12 while periapsis was -88.7 km and the
            // capsule was already coming down. A reserve against ullage is meaningless when nothing
            // is going to be burned.
            // ⚠ AND NOT WHEN PROPELLANT IS NOT A CONSTRAINT. With KSP's infinite-propellant
            // cheat on, the tank still READS whatever it fell to - 0.0 on 2026-08-12 - so the gate
            // refused a vehicle that could physically burn all day. Refusing a de-orbit strands the
            // crew; refusing one that would work is worse than not checking.
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
            // flight_0821_060847: an approach controller was still engaged when the crew de-orbited,
            // so two controllers drove one capsule for 702 s and the return never ran. A de-orbit is
            // the point of no return - it must not share the stick. Clear anything else that steers,
            // belt-and-suspenders on top of each controller's own cancel path.
            if (AutoPilot.Engaged) AutoPilot.Disengage("de-orbit takes the vehicle");
            if (StationApproach.Engaged) StationApproach.Disengage("de-orbit takes the vehicle");
            if (DirectApproachOps.Engaged) DirectApproachOps.Disengage("de-orbit takes the vehicle");
            if (DockingOps.Engaged) DockingOps.Reset();
            if (UndockOps.Engaged) UndockOps.Reset();

            ship = v;
            Engaged = true;
            aligned = false;
            haveImpact = false;
            startedAt = Planetarium.GetUniversalTime();
            lastScanAt = 0.0;
            prevPeri = v.orbit.PeA;
            prevPeriAt = startedAt;

            // ---- ⛔ PHASE DOWN FIRST, OR EVERY AIM CONSTANT BELOW IS DESCRIBING A DIFFERENT ORBIT ----
            // `pure/Deorbit.cs`'s aims were each fitted from 85.1 x 79.2 km. The station sits at
            // 86.8 x 85.8. F9I runs `StPhaseToDeorbitOrbit` before handing over to the de-orbit for
            // exactly this reason, and the phase-down knows how to skip itself when it is unnecessary
            // or impossible - so asking is free and not asking is a miss.
            phaseDownPending = false;
            passFound = false; warpRequested = false;
            trackMissM = 0.0; offPlaneDeg = 0.0; goTimeUt = 0.0;
            if (!DeorbitOrbit.AlreadyOnOrbit(v.orbit.ApA, v.orbit.PeA))
            {
                PhaseDownOps.Engage(v);
                phaseDownPending = !PhaseDownOps.Finished;
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

            // The phase-down owns the vehicle until it settles, one way or the other. It is allowed to
            // give up - "de-orbit from here" beats "stuck in a phase that cannot complete" - so this
            // waits on Finished, not on success.
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
            // A retrograde burn cannot move the plane and can barely move the ground track, so almost
            // all of where the capsule ends up is decided by WHEN the engine lights. This used to
            // ignite the moment the button was pressed, from wherever the capsule happened to be,
            // which hands the closed loop a miss of hundreds of kilometres it has no authority to fix
            // - it would simply burn to its own timeout. `DgPhasing` picks the pass and waits for it.
            if (!passFound) { FindPass(now); return; }
            if (now < goTimeUt)
            {
                // Hold retrograde while we wait, so the burn's alignment falls through when it starts.
                AttitudeController.Ascent.Throttle = 0.0;
                AttitudeController.Ascent.SteerTo(ship, -ship.obt_velocity.normalized, Vector3d.zero);

                // ---- AND WARP THERE. The pass can be five orbits away. ----
                // `DgPhasing` ends with `DgWarpTo(dgGoUt - 25)` for exactly this reason: the wait is
                // dead time by construction - the whole point of picking a pass is that it is not
                // now. Asked once, and only once we are pointed, so a manual cancel stays cancelled.
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
            // Above the atmosphere the capsule is still in orbit, so the predictor has no impact to aim at.
            // The burn therefore lights PURE RETROGRADE and holds it until the predictor first returns an
            // impact (periapsis has dropped into the atmosphere); only THEN does it start aiming, walking
            // the red X onto the green X. There is no shutdown between the two - it is one burn. The
            // aiming itself is decoupled (see below the aim scan): DEPTH nulls along-track, a sideways push
            // nulls cross-track. `off` measures the retrograde alignment the ignition gate waits for.
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
            // Reading periapsis is free and integrating a trajectory is not. Flight 035 measured what
            // happens when the cheap test waits on the expensive one: 3.24 s of latency, 7.9 km of
            // excess entry depth, and a trim that spent the whole descent hauling the impact back.
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

            // The aim scan, at its own slower rate.
            if (now - lastScanAt >= DeorbitBurn.AimScanIntervalS)
            {
                lastScanAt = now;
                // ---- ⛔ DRAG-AWARE MISS, NOT VACUUM. AIM THE BURN DIRECTLY AT THE LZ (2026-08-19). ----
                // The burn happens at ~80 km, above the atmosphere, where NO drag has been measured, so
                // `MissTo(ship)` (live BC = 0) fell back to a VACUUM solve - always long by tens of km -
                // and the burn chased that phantom overshoot into the depth floor still ~171 km out and
                // NEVER aimed at the real LZ. Use the KNOWN capsule BC (the same 440 the pre-entry trim
                // and entry guidance already fly), so the predicted impact is drag-aware and the closed
                // loop drives THAT onto the target, within the depth budget. This is the "put the
                // predicted impact directly on the LZ" the crew asked for.
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
            // (2026-08-20, after researching MechJeb's landing autopilot.) Aiming the impact by DEEPENING
            // is one-way and hyper-sensitive at the grazing entry - flight_0820_112928 threw the impact
            // 37 km past the target in one aim scan and slammed to the floor, unrecoverable. So the burn
            // no longer chases the impact; it lowers periapsis to a fixed sane depth and stops. The
            // predicted impact then sits within RCS range of the target, and EntryOps.Trim closes it with
            // a TWO-WAY course correction (prograde/retro for along, lateral for cross) on the coast.
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
                // `DgRecoveryMain` runs separation, the coast, the trim, the lifting entry and the
                // chutes as one continuous sequence with no crew action between them - but every one
                // of those steps assumes the capsule is coming down. On 2026-08-11 an aborted burn
                // left periapsis at 80.1 km, ten kilometres ABOVE the atmosphere, and this handed it
                // to the entry anyway: it jettisoned the trunk in a stable orbit and then had nothing
                // to do for the 22 minutes to the end of the session. The test is physical, not a
                // reading of `why` - what matters is where periapsis is, however the burn ended.
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

                // Engage BEFORE disengaging: Disengage drops our vessel reference.
                EntryOps.TargetLatDeg = TargetLatDeg;
                EntryOps.TargetLonDeg = TargetLonDeg;
                EntryOps.Engage(ship);

                Disengage(why);
                return;
            }

            // ---- ⛔ DEEPEN TO THE ENTRY PERIAPSIS, EASING NEAR IT; CANCEL CROSS ON THE WAY. ----
            // Throttle is proportional to how far periapsis still has to fall, so it eases to nothing at
            // the target instead of slamming past it. Steer retrograde until there is an impact, then
            // blend in a gentle sideways (normal) push to take cross-track out - a normal push cannot
            // change periapsis, so it never overshoots the depth. Along-track is deliberately NOT aimed
            // here; the coast trim does that two-way.
            Vector3d aimSteer = retro;
            if (haveImpact)
            {
                Vector3d cn = CrossNullDir(retro);
                Vector3d blended = retro + DeorbitBurn.CrossBlend * cn;
                if (blended.sqrMagnitude > 1e-9) aimSteer = blended.normalized;
            }
            double depthAbove = st.PeriapsisM - DeorbitBurn.PeriapsisTargetM;   // metres periapsis still to fall
            double th = depthAbove / DeorbitBurn.DepthEaseM;
            if (th > DeorbitBurn.DepthThrottleMax) th = DeorbitBurn.DepthThrottleMax;
            if (th < DeorbitBurn.ThrottleMin) th = DeorbitBurn.ThrottleMin;
            ThrottleCmd = haveImpact ? th : DeorbitBurn.ThrottleBlind;   // get periapsis into the air fast, then ease
            AttitudeController.Ascent.SteerTo(ship, aimSteer, Vector3d.zero);
            AttitudeController.Ascent.Throttle = ThrottleCmd;
            if (!ship.ActionGroups[KSPActionGroup.RCS])
                ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

            Note = "DE-ORBIT - " + (haveImpact
                     ? ("along " + (aimAlongM / 1000.0).ToString("F1") + " / cross "
                        + (aimCrossM / 1000.0).ToString("F1") + " km")
                     : "acquiring impact")
                 + ", Pe " + (PeriapsisM / 1000.0).ToString("F1") + " km, thr "
                 + (ThrottleCmd * 100.0).ToString("F0") + "%";
        }

        /// <summary>
        /// The thrust direction for the vectored burn: retrograde blended with a push toward the LZ.
        ///
        /// The predicted impact moves in the direction of the delta-v, so pushing the velocity along the
        /// horizontal impact->LZ vector walks the impact onto the target. The blend is
        /// `normalize(retro + gain * towardLZ)` with gain &lt; 1, so a towardLZ that opposes retrograde
        /// only shortens the resultant (the normalise keeps its direction) and the thrust stays
        /// net-retrograde - periapsis keeps falling. What survives the blend is the CROSS-track component,
        /// which is exactly the axis a pure retrograde burn cannot correct. Along-track is still the
        /// depth loop's; this only stops the burn missing sideways.
        /// </summary>
        private static Vector3d VectoredDir(Vector3d retro)
        {
            CelestialBody b = ship.mainBody;
            if (b == null) return retro;
            Vector3d up = (ship.CoM - b.position).normalized;
            Vector3d impactPos = (Vector3d)b.GetWorldSurfacePosition(impactLat, impactLon, 0.0) - b.position;
            Vector3d lzPos = (Vector3d)b.GetWorldSurfacePosition(TargetLatDeg, TargetLonDeg, 0.0) - b.position;
            Vector3d toLZ = Vector3d.Exclude(up, lzPos - impactPos);   // horizontal, impact -> LZ
            if (toLZ.sqrMagnitude < 1e-6) return retro;

            double g = AimMissM / DeorbitBurn.VectorScaleM;
            if (g > DeorbitBurn.VectorMaxGain) g = DeorbitBurn.VectorMaxGain;
            if (g < 0.0) g = 0.0;
            Vector3d steer = retro + g * toLZ.normalized;
            return (steer.sqrMagnitude > 1e-9) ? steer.normalized : retro;
        }

        /// <summary>
        /// Thrust direction to null CROSS-track ONLY: a pure normal push (perpendicular to the ground
        /// track, horizontal) toward the LZ's side. A normal burn rotates the plane without changing
        /// energy, so periapsis - and therefore along-track - stays put, which is exactly what the aim
        /// phase wants once along-track is already on the LZ.
        /// </summary>
        private static Vector3d CrossNullDir(Vector3d retro)
        {
            CelestialBody b = ship.mainBody;
            if (b == null) return retro;
            Vector3d up = (ship.CoM - b.position).normalized;
            Vector3d vhat = ship.obt_velocity.normalized;
            Vector3d normal = Vector3d.Cross(up, vhat);       // horizontal, perpendicular to the track
            if (normal.sqrMagnitude < 1e-9) return retro;
            normal = normal.normalized;

            Vector3d impactPos = (Vector3d)b.GetWorldSurfacePosition(impactLat, impactLon, 0.0) - b.position;
            Vector3d lzPos = (Vector3d)b.GetWorldSurfacePosition(TargetLatDeg, TargetLonDeg, 0.0) - b.position;
            double crossErr = Vector3d.Dot(lzPos - impactPos, normal);   // + = LZ on the +normal side
            return (crossErr >= 0.0) ? normal : -normal;
        }

        /// <summary>
        /// Pick the pass and the ignition time. `DgPhasing`, once, when the phase-down has settled.
        ///
        /// ⚠ THE SITE IS EVALUATED AT TOUCHDOWN. `Overflight.LandLagS` is why - the ground keeps
        /// turning for about 164 s after the pass, and scoring the pass against where the site is NOW
        /// is `CargoDragon_070`'s 55 km. The search costs a few hundred trajectory samples and runs
        /// exactly once.
        /// </summary>
        private static void FindPass(double now)
        {
            CelestialBody b = ship.mainBody;
            Orbit o = ship.orbit;
            if (o == null || o.period <= 0.0)
            {
                // No orbit to search is not a reason to refuse to come home - burn from here.
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
            // The fixed PhaseArcFrac lead is F9I's capsule's, and on flight_0818 it put the burn 455 km
            // LONG - past the depth budget - so the closed loop hit the floor still 455 km out and the
            // entry, which can only give range away, clawed it to a 92.6 km splashdown miss. The
            // drag-aware predictor called that miss BEFORE the burn (101.7 vs 92.6 - EntryOps.cs:194),
            // which is the green light to let it choose the ignition time. A retrograde burn cannot move
            // the plane or (much) the ground track, so cross-track stays Overflight's; only the
            // along-track lead is ours to solve.
            // ⛔ REVERTED 2026-08-18: the custom "adaptive ignition" search (AdaptiveIgnition/
            // DeorbitPoint) made the landing ~1100 km WORSE than this ~92 km method, and its
            // planet-rotation correction was wrong (the impact prediction already accounts for
            // rotation). Back to F9I's fixed lead; diagnose the remaining ~92 km from flight data, not
            // another invented search. (The comment block just above is stale - it describes the
            // reverted approach; left only because a stray glyph blocks its edit, clean next pass.)
            goTimeUt = Overflight.GoTimeUt(r.Ut, now, o.period);
            passFound = true;

            // The off-plane angle at touchdown, reported not burnt - see pure/Overflight.cs. On our
            // 0.133 degree station orbit this is noise; on an inclined one it is the whole cross-track
            // error, and the crew should be told before the engine lights rather than after.
            //
            // ⚠ THE NORMAL IS BUILT FROM TWO WORLD POSITIONS, NOT FROM position x velocity.
            // `getRelativePositionAtUT` and `getOrbitalVelocityAtUT` both return KSP's SWIZZLED orbit
            // frame (y and z exchanged), while `GetLatitude`/`GetLongitude` take a WORLD position.
            // Crossing the swizzled pair and then asking the body where it points mixes two frames and
            // produces a plausible number that is wrong by an arbitrary rotation - the kind of defect
            // that reads fine and lands 50 km out. `getPositionAtUT` is already world, so two of those
            // a minute apart give the normal with no frame question at all.
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

        // The search callback needs state the delegate signature cannot carry. Static rather than a
        // closure: C# 5, and the rest of the plugin uses named delegates for the same reason.
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

        /// <summary>
        /// The ignition time whose modelled burn lands on the target, in the lap up to the pass. Rolls
        /// nothing forward - the window is clamped to the future - and falls back to the fixed
        /// PhaseArcFrac lead if the predictor never brings the model down (a bad orbit still comes home
        /// the old way rather than not at all).
        /// </summary>
        private static double AdaptiveIgnition(double now, double passUt, double period, double nominalGo)
        {
            CelestialBody b = searchShip.mainBody;
            ignAimRp = b.Radius + IgnitionAimPeriapsisM;

            // The swizzle between KSP's orbit frame and world is the exact "reads fine, lands 50 km out"
            // trap this file already warns about for the plane normal. Check it once against the live
            // vessel; if it is off, the whole search is built on a bad frame, so fall back to the proven
            // fixed lead rather than fly a garbage point. This makes the adaptive point strictly no worse
            // than the old behaviour on any frame surprise.
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

        /// <summary>
        /// Landing miss for a candidate ignition time: propagate the orbit there, model the de-orbit
        /// burn as an impulsive retrograde to the aim periapsis, and integrate the real drag descent.
        /// </summary>
        private static IgnitionMiss IgnitionMissAt(double ut)
        {
            IgnitionMiss m = new IgnitionMiss();
            CelestialBody b = searchShip.mainBody;
            Orbit o = searchShip.orbit;

            Vector3d pw = Swizzle(o.getRelativePositionAtUT(ut));   // world axes, body-relative
            Vector3d vw = Swizzle(o.getOrbitalVelocityAtUT(ut));    // world axes, body-relative

            double dv = DeorbitPoint.DvForPeriapsis(pw.x, pw.y, pw.z, vw.x, vw.y, vw.z,
                                                    b.gravParameter, ignAimRp);
            double vmag = vw.magnitude;
            Vector3d vAfter = (vmag > 1e-6) ? vw * (1.0 - dv / vmag) : vw;

            Impact im = ImpactPredictor.PredictFromState(b, pw, vAfter, EntryGuidance.CapsuleBcKgM2);
            if (!im.Valid) { m.Ok = false; return m; }

            // ---- ⛔ WIND THE IMPACT LON BACK BY THE ROTATION OVER THE IGNITION LEAD. ----
            // PredictFromState returns lat/lon in the body frame AS OF searchNow - it only removes the
            // rotation during the FALL. But the burn is (ut - searchNow) in the FUTURE, over which the
            // body turns another (ut-searchNow)*360/rotationPeriod degrees. On flight_0818_154218 the
            // lead was 6262 s = ~104 deg = ~1090 km: the search minimised the miss in a stale frame and
            // reported 2.0 km for an ignition that flew the capsule 1103 km off. `MissAt` (cross-track)
            // already carries this via (ut - searchNow) + searchLag; the along-track search must too.
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

        /// <summary>
        /// Is the orbit-frame → world swizzle sound? Rebuild the live vessel's own state at NOW from the
        /// orbit and compare to what the vessel reports directly. If position or velocity disagrees, the
        /// swizzle (or the API's frame) is not what the search assumes and the caller must not trust it.
        /// </summary>
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

        /// <summary>KSP's orbit frame swaps Y and Z relative to world. Convert one vector across.</summary>
        private static Vector3d Swizzle(Vector3d v) { return new Vector3d(v.x, v.z, v.y); }

        private static double Mono(Vessel v)
        {
            // OUR side of any docking joint, never the merged vessel - see DockedSide. Correct
            // today only because both of these run after the undock; one caller moved earlier and
            // the budget would silently become the station's.
            return DockedSide.Mono(v);
        }
    }
}
