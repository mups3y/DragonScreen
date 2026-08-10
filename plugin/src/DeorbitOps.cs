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

        private static Vessel ship;
        private static DeorbitState st;
        private static double startedAt, lastScanAt, prevPeri, prevPeriAt;
        private static bool aligned;

        /// <summary>Where the capsule is trying to land. Defaults to LZ-1.</summary>
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
            for (int i = 0; i < v.parts.Count; i++)
            {
                if (!VehicleParts.IsSecondStage(v.parts[i].name)) continue;
                Note = "REFUSED - the second stage is still attached";
                Debug.LogWarning(Tag + "DE-ORBIT " + Note);
                return;
            }

            ship = v;
            Engaged = true;
            aligned = false;
            startedAt = Planetarium.GetUniversalTime();
            lastScanAt = 0.0;
            prevPeri = v.orbit.PeA;
            prevPeriAt = startedAt;

            st = new DeorbitState();
            st.AimMissM = -1.0;
            st.BestMissM = 9.9e12;
            st.UsingS2 = false;

            BudgetInputs b = Budget(v);
            BudgetReport rep = ReturnBudget.Report(b);
            Debug.Log(Tag + "DE-ORBIT engaged - target " + TargetLatDeg.ToString("F4") + ", "
                      + TargetLonDeg.ToString("F4") + ". Mono budget: " + rep.Line);
            if (!rep.Sufficient)
                Debug.LogWarning(Tag + "⚠ MONOPROP SHORT by "
                                 + (-rep.MarginUnits).ToString("F1")
                                 + " units - the burn may not finish and the landing will miss.");
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
            Engaged = false; ship = null; Note = "-";
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

            // Retrograde, and hold it. The burn is long and shallow; a capsule that wanders off
            // retrograde is putting its dv somewhere other than where the solve assumed.
            Vector3d retro = -ship.obt_velocity.normalized;
            AttitudeController.Ascent.SteerTo(ship, retro, Vector3d.zero);
            double off = Vector3d.Angle(ship.ReferenceTransform.up, retro);

            if (!aligned)
            {
                AttitudeController.Ascent.Throttle = 0.0;
                Note = "ALIGNING - " + off.ToString("F1") + " deg";
                if (off < DeorbitBurn.AlignedDeg || now - startedAt > 30.0)
                {
                    aligned = true;
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
            st.ElapsedS = now - startedAt;

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

        private static double Mono(Vessel v)
        {
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
                for (int k = 0; k < v.parts[i].Resources.Count; k++)
                    if (v.parts[i].Resources[k].resourceName == "MonoPropellant")
                        t += v.parts[i].Resources[k].amount;
            return t;
        }
    }
}
