// DragonScreen — DeorbitBurn  (KSP glue: the ONE Draco retrograde deorbit burn, shared by both callers)
// ============================================================================================
// Real Crew Dragon deorbits on the DRACOS (MMH+NTO); the SuperDracos are ABORT-reserved and are EMPTY on a
// return. The retired rescue path (AbortControl.RunDeorbitBurn) throttled the SuperDraco
// (EngineRole.PodAbort) → an empty engine → ZERO thrust (flight 024400: throttle 0.36, thrust_n 0, pe
// 196.9→196.1 km unchanged) → the crew stranded. The nominal path (ReturnControl.FlyDeorbitEntry) already
// used the Dracos correctly, so the two diverged — one right, one wrong. This is the SINGLE implementation
// both now call, so they can never diverge again.
//
// It drives the headless-tested pure DeorbitGuidance FSM (trunk jettison → settle → retrograde Draco burn,
// closed-loop on MEASURED periapsis → orient shield-forward), with:
//   • DIRECT part actuation only — Actuator.EnableRcs / JettisonTrunk (fires the ModuleTundraDecoupler
//     "Decouple" event BY NAME via FireDecoupler) — never staging/action groups.
//   • the nose shroud kept OPEN through the whole burn (the forward Dracos are the attitude authority with
//     reaction wheels stripped — [[dragon-nose-cone-rcs]]); it CLOSES only once the burn completes, to
//     protect the docking adapter on entry. (The old ReturnControl closed it AT trunk jettison, before the
//     burn — obstructing the very thrusters holding retrograde.)
//   • planned + delivered Δv instrumented every tick (planned = the retrograde Hohmann first-burn from the
//     measured orbit; delivered = ∫ measured RCS thrust / mass dt) so the return propellant budget is
//     falsifiable in the recording.
// ============================================================================================
using UnityEngine;

namespace DragonScreen
{
    // Burn-local state for one deorbit (trunk-gone is vehicle-level, so it is passed by ref, not held here).
    public class DeorbitBurnState
    {
        public DeorbitPhase Phase;
        public double SettleStartUT;    // UT the settle dwell began (−1 = not yet)
        public double DvDeliveredMps;   // ∫ measured RCS thrust / mass dt while actually firing
        public double DvPlannedMps;     // retrograde Δv to lower pe to the corridor (measured-state formula)
        public double LastBurnUT;       // UT of the previous firing tick (−1 = not firing) for the ∫ step
        public bool ShroudClosed;       // one-shot: shroud closed after the burn completed
        public bool Done;               // pe on the corridor, oriented shield-forward → hand to Entry

        public DeorbitBurnState() { Reset(); }
        public void Reset()
        {
            Phase = DeorbitPhase.Idle; SettleStartUT = -1.0;
            DvDeliveredMps = 0.0; DvPlannedMps = 0.0; LastBurnUT = -1.0;
            ShroudClosed = false; Done = false;
        }
    }

    public static class DeorbitBurn
    {
        // One tick of the shared Draco deorbit. Returns true once the burn is complete (st.Done). The caller
        // owns trunkGone (it spans escape/deorbit/entry), so it is passed by ref; the caller passes its own
        // [Tunable] gate values so each keeps its knob (no magic literals, no forced behaviour change).
        public static bool Tick(Vessel v, DeorbitBurnState st, ref bool trunkGone,
                                double targetPeM, double attitudeReadyDeg, double settleS, double forwardSign)
        {
            if (v == null || v.mainBody == null) return st.Done;
            double now = Planetarium.GetUniversalTime();

            Actuator.EnableRcs(v);          // ⛔ direct: per-thruster rcsEnabled + master (no craft AG binding)
            Actuator.OpenNoseShroud(v);     // forward Dracos = attitude authority → keep OPEN through the burn (idempotent)

            Vector3d up = Steering.Up(v);
            Vector3d velI = v.obt_velocity;

            DeorbitInputs di = new DeorbitInputs();
            di.Valid = true;
            di.Velocity = new Vec3(velI.x, velI.y, velI.z);
            di.Up = new Vec3(up.x, up.y, up.z);
            di.PeriapsisAltM = v.orbit != null ? v.orbit.PeA : 0.0;   // MEASURED pe = the closed-loop cutoff
            di.EntryInterfaceAltM = targetPeM;
            di.TrunkAttached = !trunkGone;
            di.SettleS = settleS;
            di.SettleElapsedS = st.SettleStartUT > 0 ? now - st.SettleStartUT : 0.0;
            di.DvAppliedMps = st.DvDeliveredMps;   // feed the guidance's own backstop cutoff

            // planned deorbit Δv = retrograde Hohmann first-burn lowering pe from the current radius to the
            // corridor; recomputed each tick from the live orbit (measured-state formula, not a sim).
            if (v.orbit != null)
                st.DvPlannedMps = DeorbitGuidance.DeorbitDvMps(
                    (v.CoM - v.mainBody.position).magnitude, v.mainBody.Radius + targetPeM, v.mainBody.gravParameter);

            Vector3d retro = velI.magnitude > 1 ? -velI.normalized : up;
            di.AttitudeReady = Steering.PointingErrorDeg(v, retro) <= attitudeReadyDeg;
            di.AllNominal = true;

            DeorbitCommand dc = DeorbitGuidance.Guide(di, st.Phase);
            st.Phase = dc.Phase;

            // trunk goes FIRST (no shield, burns up; mass save) — the ModuleTundraDecoupler fired by name.
            if (dc.JettisonTrunk && !trunkGone) { Actuator.JettisonTrunk(v); trunkGone = true; }
            if (st.Phase == DeorbitPhase.Settle && st.SettleStartUT < 0) st.SettleStartUT = now;

            // ATTITUDE-FIRST-THEN-TRANSLATE: point retrograde, and only translate once actually pointed.
            Steering.Point(v, retro);
            bool ready = Steering.PointingErrorDeg(v, retro) <= attitudeReadyDeg;
            if (dc.Throttle > 0.0 && ready)
            {
                FlightDriver.SetTranslation(0, 0, forwardSign);   // Draco retrograde translation (nose is retrograde)
                double massKg = v.totalMass * 1000.0;
                if (st.LastBurnUT > 0 && massKg > 1.0)
                    st.DvDeliveredMps += Actuator.RcsThrustN(v) / massKg * (now - st.LastBurnUT);
                st.LastBurnUT = now;
            }
            else { FlightDriver.ReleaseTranslation(); st.LastBurnUT = -1.0; }

            if (dc.Complete)
            {
                st.Done = true;
                FlightDriver.ReleaseTranslation();
                if (!st.ShroudClosed) { Actuator.CloseNoseShroud(v); st.ShroudClosed = true; }   // burn done → close for entry
            }
            return st.Done;
        }
    }
}
