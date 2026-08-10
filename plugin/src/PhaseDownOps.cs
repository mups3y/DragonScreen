/*
 * DragonScreen - PhaseDownOps
 *
 * GLUE. Drops from the station's orbit onto the orbit every landing aim was calibrated from, in two
 * Hohmann half-burns. Law in `pure/DeorbitOrbit.cs`. Ported from
 * `F9I/station_ops.ks:2560 StPhaseToDeorbitOrbit`.
 *
 * ---- WHY THIS RUNS AT ALL ----
 * `pure/Deorbit.cs`'s aim constants - 286 000 m for a crew S2 de-orbit, 315 450 for cargo - were each
 * fitted from an 85.1 × 79.2 km orbit. De-orbiting from the station's 86.8 × 85.8 km instead hands
 * those constants an entry energy they do not describe. The phase-down is what makes them true.
 *
 * ---- ⛔ AND IT IS ALLOWED TO GIVE UP ----
 * Two failure paths both end in "de-orbit from here" rather than a stall:
 *   · no engine will light  - F9I logs it, warns the crew, and returns false
 *   · a burn is refused     - the periapsis floor inside `NodeExecutor` said no
 * A capsule that will not phase down can still come home; a capsule stuck in a phase that cannot
 * complete cannot. `Outcome` tells the caller which of the three happened.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    public enum PhaseDownStage : byte
    {
        Idle = 0,
        /// <summary>Commanding ignition and waiting to SEE thrust. See the header.</summary>
        Igniting,
        /// <summary>Burn 1, at apoapsis: lower the periapsis.</summary>
        LoweringPeriapsis,
        /// <summary>Burn 2, at the new periapsis: lower the apoapsis.</summary>
        LoweringApoapsis,
        /// <summary>On the landing orbit.</summary>
        Done,
        /// <summary>Not on it, and not going to be. De-orbit from where we are.</summary>
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

        /// <summary>True once the sequence has settled, either way. The caller may proceed.</summary>
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
            stageStartedAt = Planetarium.GetUniversalTime();

            if (DeorbitOrbit.AlreadyOnOrbit(v.orbit.ApA, v.orbit.PeA))
            {
                // A needless burn only spends the margin the landing depends on.
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

            if (PodEngines.Present(v) && !PodEngines.Available(v)) PodEngines.On(v);
            Go(PhaseDownStage.Igniting);
            Debug.Log(Tag + "phase-down engaged: " + (v.orbit.ApA / 1000.0).ToString("F1") + " x "
                      + (v.orbit.PeA / 1000.0).ToString("F1") + " km -> "
                      + (DeorbitOrbit.TargetApoapsisM / 1000.0).ToString("F1") + " x "
                      + (DeorbitOrbit.TargetPeriapsisM / 1000.0).ToString("F1") + " km, about "
                      + PlannedDvMps.ToString("F1") + " m/s");
        }

        public static void Reset()
        {
            Engaged = false; Stage = PhaseDownStage.Idle; ship = null;
            Note = "-"; PlannedDvMps = 0.0; burnLaunched = false;
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

        /// <summary>
        /// Command ignition, then WAIT AND CHECK. The header explains what happens when this step is
        /// assumed rather than verified: the burn falls back to RCS and pushes the wrong way.
        /// </summary>
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
                // The executor has finished with it, one way or the other.
                if (NodeExecutor.Phase == BurnPhase.Failed) { Skip(NodeExecutor.Note); return; }
                Debug.Log(Tag + "phase-down burn 1 done - now "
                          + (ship.orbit.ApA / 1000.0).ToString("F1") + " x "
                          + (ship.orbit.PeA / 1000.0).ToString("F1") + " km");
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

        /// <summary>
        /// Hand one half-burn to the executor.
        ///
        /// ⚠ THE Δv DIRECTION IS THE VELOCITY AT THE NODE, NOT NOW. Half an orbit away those differ by
        /// 180°, so using the current one burns exactly backwards - the same trap `StationApproach`
        /// already carries a comment about.
        /// </summary>
        private static bool Launch(PhaseDownBurn burn, double burnUt)
        {
            Vector3d velAtNode = ship.orbit.getOrbitalVelocityAtUT(burnUt);
            if (velAtNode.sqrMagnitude < 1.0) { Note = "no velocity at the node"; return false; }

            Vector3d dv = velAtNode.normalized * burn.DvMps;
            if (!NodeExecutor.Begin(ship, dv, burnUt, "phase-down: " + burn.Label)) return false;
            burnLaunched = true;
            return true;
        }
    }
}
