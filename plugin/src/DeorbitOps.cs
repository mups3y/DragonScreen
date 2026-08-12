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

        /// <summary>Drop out of warp this long before the de-orbit point, to settle. `DgPhasing`'s 25 s.</summary>
        public const double PassWarpLeadS = 25.0;
        private static double goTimeUt, trackMissM, offPlaneDeg;

        /// <summary>When the burn will be lit, and how good the pass is. For the pages and the crew.</summary>
        public static double SecondsToIgnition
        {
            get { return passFound ? goTimeUt - Planetarium.GetUniversalTime() : -1.0; }
        }
        public static double PassTrackMissM { get { return trackMissM; } }
        public static double PassOffPlaneDeg { get { return offPlaneDeg; } }

        /// <summary>Where the capsule is trying to land. Defaults to LZ-1.</summary>
        // ⛔ THE SAME POINT THE ENTRY AIMS AT, OR THE BURN AND THE ENTRY DISAGREE. The de-orbit
        // solves for an impact and the entry then steers to a target; with two different
        // coordinates the entry spends its whole (shorten-only) authority fixing an error the
        // burn deliberately introduced. Both are the capsule's SPLASHDOWN point, not the pad.
        public static double TargetLatDeg = LandingSites.Splashdown.LatDeg;
        public static double TargetLonDeg = LandingSites.Splashdown.LonDeg;

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

            ship = v;
            Engaged = true;
            aligned = false;
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
            passFound = false; warpRequested = false;
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

            // Retrograde, and hold it. The burn is long and shallow; a capsule that wanders off
            // retrograde is putting its dv somewhere other than where the solve assumed.
            Vector3d retro = -ship.obt_velocity.normalized;
            AttitudeController.Ascent.SteerTo(ship, retro, Vector3d.zero);
            double off = Vector3d.Angle(ship.ReferenceTransform.up, retro);

            if (!aligned)
            {
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
                st.AimMissM = ImpactPredictor.MissTo(ship, TargetLatDeg, TargetLonDeg);
                AimMissM = st.AimMissM;
                DeorbitBurn.Track(ref st);
            }

            string why;
            if (DeorbitBurn.Complete(st, out why))
            {
                AttitudeController.Ascent.Throttle = 0.0;
                ThrottleCmd = 0.0;
                Debug.Log(Tag + "de-orbit burn complete - " + why + ". Pe "
                          + (PeriapsisM / 1000.0).ToString("F1") + " km, aim miss "
                          + (st.AimMissM >= 0.0
                             ? (st.AimMissM / 1000.0).ToString("F2") + " km" : "unknown"));
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

            ThrottleCmd = DeorbitBurn.Throttle(st);
            AttitudeController.Ascent.Throttle = ThrottleCmd;
            if (!ship.ActionGroups[KSPActionGroup.RCS])
                ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true);

            Note = "DE-ORBIT - miss "
                 + (st.AimMissM >= 0.0 ? (st.AimMissM / 1000.0).ToString("F1") + " km" : "acquiring")
                 + ", Pe " + (PeriapsisM / 1000.0).ToString("F1") + " km, thr "
                 + (ThrottleCmd * 100.0).ToString("F0") + "%";
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

        private static double Mono(Vessel v)
        {
            // OUR side of any docking joint, never the merged vessel - see DockedSide. Correct
            // today only because both of these run after the undock; one caller moved earlier and
            // the budget would silently become the station's.
            return DockedSide.Mono(v);
        }
    }
}
