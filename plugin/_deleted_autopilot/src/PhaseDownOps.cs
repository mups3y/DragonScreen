// DragonScreen - PhaseDownOps
// ---- WHY THIS RUNS AT ALL ----
// ---- ⛔ AND IT IS ALLOWED TO GIVE UP ----
using System;
using UnityEngine;

namespace DragonScreen
{
    public enum PhaseDownStage : byte
    {
        Idle = 0,
        Igniting,
        LoweringPeriapsis,
        LoweringApoapsis,
        Done,
        Skipped
    }

    public static class PhaseDownOps
    {
        private const string Tag = "[DragonScreen] ";

        public static PhaseDownStage Stage { get; private set; }
        public static bool Engaged { get; private set; }
        public static string Note = "-";
        public static double PlannedDvMps;

        private static Vessel ship;
        private static double stageStartedAt;
        private static bool burnLaunched;

        private const int MaxPeriapsisTopUps = 3;
        private static int periapsisTopUps;

        public static bool Finished
        {
            get { return Stage == PhaseDownStage.Done || Stage == PhaseDownStage.Skipped; }
        }

        public static void Engage(Vessel v)
        {
            if (v == null || v.orbit == null) return;
            ship = v;
            Engaged = true;
            burnLaunched = false;
            periapsisTopUps = 0;
            stageStartedAt = Planetarium.GetUniversalTime();

            if (DeorbitOrbit.AlreadyOnOrbit(v.orbit.ApA, v.orbit.PeA))
            {
                Stage = PhaseDownStage.Done;
                Note = "already on the landing orbit - "
                     + (v.orbit.ApA / 1000.0).ToString("F1") + " x "
                     + (v.orbit.PeA / 1000.0).ToString("F1") + " km";
                Debug.Log(Tag + "phase-down not needed: " + Note);
                Engaged = false;
                return;
            }

            PlannedDvMps = DeorbitOrbit.TotalDvMps(v.mainBody.gravParameter, v.mainBody.Radius,
                                                   v.orbit.ApA, v.orbit.PeA, v.orbit.semiMajorAxis);

            Go(PhaseDownStage.LoweringPeriapsis);
            Debug.Log(Tag + "phase-down engaged: " + (v.orbit.ApA / 1000.0).ToString("F1") + " x "
                      + (v.orbit.PeA / 1000.0).ToString("F1") + " km -> "
                      + (DeorbitOrbit.TargetApoapsisM / 1000.0).ToString("F1") + " x "
                      + (DeorbitOrbit.TargetPeriapsisM / 1000.0).ToString("F1") + " km, about "
                      + PlannedDvMps.ToString("F1") + " m/s");
        }

        public static void Reset()
        {
            Engaged = false; Stage = PhaseDownStage.Idle; ship = null;
            Note = "-"; PlannedDvMps = 0.0; burnLaunched = false; periapsisTopUps = 0;
        }

        private static void Go(PhaseDownStage s)
        {
            Stage = s;
            stageStartedAt = Planetarium.GetUniversalTime();
            burnLaunched = false;
        }

        private static void Skip(string why)
        {
            Stage = PhaseDownStage.Skipped;
            Engaged = false;
            Note = "SKIPPED - " + why;
            Debug.LogWarning(Tag + "phase-down " + Note + ". De-orbiting from the current orbit.");
        }

        // ------------------------------------------------------------------ the loop

        public static void Tick()
        {
            if (!Engaged) return;
            if (ship == null || ship.state == Vessel.State.DEAD) { Skip("vessel lost"); return; }
            if (ship.orbit == null) { Skip("no orbit"); return; }

            double now = Planetarium.GetUniversalTime();
            double inStage = now - stageStartedAt;

            switch (Stage)
            {
                case PhaseDownStage.Igniting: Ignite(inStage); break;
                case PhaseDownStage.LoweringPeriapsis: BurnOne(now); break;
                case PhaseDownStage.LoweringApoapsis: BurnTwo(now); break;
            }
        }

        private static void Ignite(double inStage)
        {
            if (PodEngines.Available(ship))
            {
                Debug.Log(Tag + "phase-down engines lit - "
                          + PodEngines.ThrustKn(ship).ToString("F1") + " kN");
                Go(PhaseDownStage.LoweringPeriapsis);
                return;
            }

            Note = "LIGHTING ENGINES - " + inStage.ToString("F0") + " s";
            if (inStage > PodEngines.IgnitionTimeoutS)
                Skip("no thrust available (" + PodEngines.ThrustKn(ship).ToString("F2") + " kN)");
        }

        private static void BurnOne(double now)
        {
            if (NodeExecutor.Active)
            {
                Note = "LOWERING PERIAPSIS - " + NodeExecutor.Phase + " " + NodeExecutor.Note;
                return;
            }

            if (burnLaunched)
            {
                if (NodeExecutor.Phase == BurnPhase.Failed) { Skip(NodeExecutor.Note); return; }

                if (ship.orbit.PeA > DeorbitOrbit.TargetPeriapsisM + DeorbitOrbit.ToleranceM
                    && periapsisTopUps < MaxPeriapsisTopUps)
                {
                    periapsisTopUps++;
                    burnLaunched = false;
                    Debug.Log(Tag + "phase-down burn 1 left periapsis at "
                              + (ship.orbit.PeA / 1000.0).ToString("F1") + " km, above the "
                              + (DeorbitOrbit.TargetPeriapsisM / 1000.0).ToString("F1")
                              + " km target - top-up " + periapsisTopUps + " of " + MaxPeriapsisTopUps);
                    return;
                }

                Debug.Log(Tag + "phase-down burn 1 done - now "
                          + (ship.orbit.ApA / 1000.0).ToString("F1") + " x "
                          + (ship.orbit.PeA / 1000.0).ToString("F1") + " km"
                          + (periapsisTopUps > 0 ? " (" + periapsisTopUps + " top-ups)" : ""));
                Go(PhaseDownStage.LoweringApoapsis);
                return;
            }

            CelestialBody b = ship.mainBody;
            PhaseDownBurn one = DeorbitOrbit.LowerPeriapsis(b.gravParameter, b.Radius,
                                                            ship.orbit.ApA, ship.orbit.semiMajorAxis);
            if (!one.Needed) { Go(PhaseDownStage.LoweringApoapsis); return; }

            double burnUt = now + ship.orbit.timeToAp;
            if (!Launch(one, burnUt)) Skip(NodeExecutor.Note);
        }

        private static void BurnTwo(double now)
        {
            if (NodeExecutor.Active)
            {
                Note = "LOWERING APOAPSIS - " + NodeExecutor.Phase + " " + NodeExecutor.Note;
                return;
            }

            if (burnLaunched)
            {
                if (NodeExecutor.Phase == BurnPhase.Failed) { Skip(NodeExecutor.Note); return; }
                Stage = PhaseDownStage.Done;
                Engaged = false;
                Note = "ON THE LANDING ORBIT - " + (ship.orbit.ApA / 1000.0).ToString("F1") + " x "
                     + (ship.orbit.PeA / 1000.0).ToString("F1") + " km";
                Debug.Log(Tag + "phase-down complete - " + Note);
                return;
            }

            CelestialBody b = ship.mainBody;
            PhaseDownBurn two = DeorbitOrbit.LowerApoapsis(b.gravParameter, b.Radius,
                                                           ship.orbit.PeA, ship.orbit.semiMajorAxis);
            if (!two.Needed)
            {
                Stage = PhaseDownStage.Done;
                Engaged = false;
                Note = "ON THE LANDING ORBIT";
                return;
            }

            double burnUt = now + ship.orbit.timeToPe;
            if (!Launch(two, burnUt)) Skip(NodeExecutor.Note);
        }

        private static bool Launch(PhaseDownBurn burn, double burnUt)
        {
            Vector3d velAtNode = ship.orbit.getOrbitalVelocityAtUT(burnUt).xzy;
            if (velAtNode.sqrMagnitude < 1.0) { Note = "no velocity at the node"; return false; }

            Vector3d dv = velAtNode.normalized * burn.DvMps;
            CapsuleRcs.Set(ship, CapsuleRcs.BurnPct);
            if (!NodeExecutor.Begin(ship, dv, burnUt, "phase-down: " + burn.Label, useRcs: true)) return false;
            burnLaunched = true;
            return true;
        }
    }
}
