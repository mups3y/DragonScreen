/*
 * DragonScreen - NodeExecutor
 *
 * GLUE. Turns to a Δv vector and burns it. Ported from `F9I/station_ops.ks:2437 StExecNode` /
 * `:2469 StBurnNode`, with the law in `pure/BurnExec.cs`.
 *
 * ---- WHY THIS IS ITS OWN FILE, AND WHY IT WAS BUILT FIRST ----
 * Five items need it: the CW transfer, the phasing leg, the orbit match, the undock separation, the
 * plane match and the de-orbit burn. Every one of them is "turn to a vector and burn this Δv
 * accurately". Writing any of them before this existed would have meant writing a burn mechanism
 * twice and throwing one away, which is the specific waste `docs/PORT_PLAN.md` exists to stop.
 *
 * ---- ⛔ THE PERIAPSIS FLOOR IS CHECKED HERE, BEFORE IGNITION, ALWAYS ----
 * `StNodeSafe:906` is "the guard flight 012 did not have". A solver hands over a Δv; it does not
 * know there is a planet under it. Any burn whose result would put periapsis below the floor is
 * REFUSED and said out loud, not flown and regretted.
 *
 * ---- AND IT GOVERNS THE THROTTLE ITSELF ----
 * F9I's warning, measured on flight 013's 120 s phasing burn: a node executor that does not actually
 * take the throttle "flew the entire thing on RCS translation - 61 units of monopropellant - and
 * because RCS pushes wherever the nose happens to be rather than along the node, it dragged
 * periapsis DOWN 7.7 km while raising apoapsis". That wrecked orbit is what the next de-orbit flew
 * from, and it is why a later flight splashed down 17.8 km from the target instead of 159 m.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    public enum BurnPhase : byte
    {
        Idle = 0,
        /// <summary>Turning to the Δv. Nothing is lit.</summary>
        Aligning,
        /// <summary>Pointed, waiting for the ignition time.</summary>
        Holding,
        Burning,
        Done,
        /// <summary>Refused or aborted. `Note` says which.</summary>
        Failed
    }

    public static class NodeExecutor
    {
        private const string Tag = "[DragonScreen] ";

        public static BurnPhase Phase { get; private set; }
        public static string Note = "-";

        /// <summary>For the pages and the recorder.</summary>
        public static double RemainingDvMps, InitialDvMps, ThrottleCmd, PointingErrorDeg;

        private static Vessel ship;
        private static Vector3d dvWorld;        // the ORIGINAL request, for the overshoot test
        private static Vector3d dvRemaining;
        private static double nodeUt, ignitionUt, startedBurnAt;
        private static bool rcsWasOn, boughtRcs, warpRequested, warpRefused;

        /// <summary>Arrive this long before ignition, so there is time to drop out of warp.</summary>
        public const double WarpArriveLeadS = 6.0;
        /// <summary>Do not bother warping a wait shorter than this. `DgWarpTo`'s 12 s.</summary>
        public const double WarpWorthwhileS = 12.0;

        public static bool Active
        {
            get { return Phase == BurnPhase.Aligning || Phase == BurnPhase.Holding
                      || Phase == BurnPhase.Burning; }
        }

        /// <summary>
        /// Plan a burn of <paramref name="dv"/> at <paramref name="atUt"/>.
        ///
        /// Returns false and explains itself if the burn is refused. The floor check happens HERE,
        /// before anything turns or lights.
        /// </summary>
        public static bool Begin(Vessel v, Vector3d dv, double atUt, string label)
        {
            if (v == null || dv.sqrMagnitude < 1e-8)
            {
                Note = label + " - nothing to burn";
                Phase = BurnPhase.Failed;
                return false;
            }

            // Declared first, not inline: the build is .NET Framework csc, which is C# 5. Inline
            // `out` variables are C# 7 and the compiler rejects them - the same reason the sort in
            // BoosterRecovery uses `delegate` rather than a lambda.
            string why;
            if (!PeriapsisSafe(v, dv, out why))
            {
                Note = "REFUSED " + label + " - " + why;
                Phase = BurnPhase.Failed;
                Debug.LogWarning(Tag + Note);
                return false;
            }

            ship = v;
            dvWorld = dv;
            dvRemaining = dv;
            InitialDvMps = dv.magnitude;
            nodeUt = atUt;

            double half = BurnExec.HalfBurnS(InitialDvMps, v.GetTotalMass(), Thrust(v));
            ignitionUt = atUt - half;

            rcsWasOn = v.ActionGroups[KSPActionGroup.RCS];
            boughtRcs = false;
            warpRequested = false;
            warpRefused = false;
            Phase = BurnPhase.Aligning;
            Note = label;

            Debug.Log(Tag + "burn planned: " + label + " - " + InitialDvMps.ToString("F2")
                      + " m/s, ignition in "
                      + (ignitionUt - Planetarium.GetUniversalTime()).ToString("F0")
                      + " s (half-burn lead " + half.ToString("F1") + " s)");
            return true;
        }

        public static void Abort(string why)
        {
            if (!Active) return;
            if (warpRequested && TimeWarp.CurrentRateIndex > 0) TimeWarp.SetRate(0, true);
            Stop();
            Phase = BurnPhase.Failed;
            Note = "ABORTED - " + why;
            Debug.LogWarning(Tag + "burn " + Note);
        }

        public static void Reset()
        {
            Phase = BurnPhase.Idle; ship = null; Note = "-";
            RemainingDvMps = 0.0; InitialDvMps = 0.0; ThrottleCmd = 0.0; PointingErrorDeg = 0.0;
        }

        // ------------------------------------------------------------------ the loop

        public static void Tick()
        {
            if (!Active) return;
            if (ship == null || ship.state == Vessel.State.DEAD) { Abort("vessel lost"); return; }

            double now = Planetarium.GetUniversalTime();

            // The remaining Δv is the ORIGINAL request minus what has been delivered. Taking it from
            // the vessel's velocity change rather than integrating thrust means an engine that
            // under-performs, flames out or is throttled by something else is accounted for.
            RemainingDvMps = dvRemaining.magnitude;

            Vector3d aim = dvRemaining.sqrMagnitude > 1e-8 ? dvRemaining : dvWorld;
            AttitudeController.Ascent.SteerTo(ship, aim.normalized, Vector3d.zero);
            PointingErrorDeg = Vector3d.Angle(ship.ReferenceTransform.up, aim.normalized);

            switch (Phase)
            {
                case BurnPhase.Aligning: Align(now); break;
                case BurnPhase.Holding: Hold(now); break;
                case BurnPhase.Burning: Burn(now); break;
            }
        }

        private static void Align(double now)
        {
            AttitudeController.Ascent.Throttle = 0.0;
            double toIgnition = ignitionUt - now;

            // RCS is the de-orbit and landing budget. Bought only when the clock says the wheels
            // will not finish the turn in time - see BurnExec.NeedRcsToAlign.
            if (!boughtRcs && BurnExec.NeedRcsToAlign(toIgnition, PointingErrorDeg))
            {
                boughtRcs = true;
                ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
                Debug.Log(Tag + toIgnition.ToString("F0") + " s to ignition and still "
                          + PointingErrorDeg.ToString("F1") + " deg off - RCS assisting the turn");
            }

            if (BurnExec.Aligned(PointingErrorDeg) || toIgnition <= BurnExec.AlignPadS)
            {
                // Hand RCS back the way we found it. The burn re-enables it if it needs it.
                if (boughtRcs && !rcsWasOn) ship.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
                Phase = BurnPhase.Holding;
            }
        }

        /// <summary>
        /// Skip the dead wait to ignition. `DgWarpTo`, ported: only if the wait is worth warping, and
        /// only ever AFTER the alignment - warping while still turning gives the controller no physics
        /// to turn with.
        ///
        /// ---- ⛔ WITHOUT THIS THE RETURN IS UNFLYABLE, AND THAT IS NOT AN EXAGGERATION. ----
        /// On 2026-08-11 the crew pressed DEORBIT NOW and the phase-down planned a **4.12 m/s** burn
        /// with "ignition in 1511 s". Nothing warped, so the console sat there for twenty-five
        /// REAL-TIME MINUTES showing a burn that had not started. The crew pressed the button again
        /// eight minutes later, which is exactly what anyone would do. Warp automation was written off
        /// in `docs/PORT_PLAN.md` as "a separate question"; it is not - a node executor that cannot
        /// reach its own node is broken, and F9I warps inside `DgExecNode` for this reason.
        ///
        /// ⚠ THE CREW CAN STILL OVERRIDE IT. This asks for rails warp once and then leaves the
        /// controls alone; it does not re-issue every tick, so a manual cancel stays cancelled.
        /// </summary>
        private static void WarpToIgnition(double now)
        {
            if (warpRequested) return;
            double lead = ignitionUt - WarpArriveLeadS;
            double wait = lead - now;
            if (wait < WarpWorthwhileS) return;

            // ---- ⛔ A BURN MORE THAN ONE ORBIT AWAY MEANS THE PLAN IS WRONG. ----
            // On 2026-08-11 this warped ~25 minutes at a time, twenty-eight times, for burns of
            // 0.13 to 5.57 m/s - 11.7 hours of game time inside a twenty-minute session, because
            // nothing upstream had a convergence test. Skipping half an hour is not a way to make a
            // bad plan cheap; it is how a bad plan goes unnoticed. Refuse, say so, and let the
            // caller's own bound deal with it.
            double period = (ship.orbit != null) ? ship.orbit.period : 0.0;
            if (period > 0.0 && wait > period)
            {
                if (!warpRefused)
                {
                    warpRefused = true;
                    Debug.LogWarning(Tag + "NOT warping " + (wait / 60.0).ToString("F1")
                                     + " min to '" + Note + "' - that is more than one orbit ("
                                     + (period / 60.0).ToString("F1") + " min) away. A burn that far "
                                     + "out is a planning error, not a wait. Holding.");
                }
                return;
            }

            warpRequested = true;
            Debug.Log(Tag + "warping " + wait.ToString("F0") + " s to ignition for '"
                      + Note + "'");
            TimeWarp.fetch.WarpTo(lead);
        }

        private static void Hold(double now)
        {
            AttitudeController.Ascent.Throttle = 0.0;

            if (now < ignitionUt - WarpArriveLeadS) { WarpToIgnition(now); return; }

            // Back to real time before the engine lights: a burn under rails warp is not a burn.
            if (TimeWarp.CurrentRateIndex > 0) TimeWarp.SetRate(0, true);
            if (now < ignitionUt) return;

            // ⚠ IF THE IGNITION TIME HAS ALREADY GONE, BURN NOW rather than skipping it. F9I:
            // "DO NOT WARP TO A TIME THAT HAS ALREADY GONE... a token warp, then straight on as if
            // the match had been flown."
            startedBurnAt = now;
            Phase = BurnPhase.Burning;
            Debug.Log(Tag + "ignition - " + Note + ", " + InitialDvMps.ToString("F2") + " m/s");
        }

        private static void Burn(double now)
        {
            BurnState s = new BurnState();
            s.RemainingDvMps = RemainingDvMps;
            s.InitialDvMps = InitialDvMps;
            s.MassT = ship.GetTotalMass();
            s.AvailableThrustKn = Thrust(ship);
            s.PointingErrorDeg = PointingErrorDeg;
            s.ElapsedS = now - startedBurnAt;
            // THE OVERSHOOT TEST. The remaining Δv has reversed against where it pointed at
            // ignition, so we have burned past the node. A countdown cannot notice this.
            s.Overshot = Vector3d.Dot(dvWorld, dvRemaining) < 0.0;

            if (BurnExec.Complete(s))
            {
                string why = BurnExec.CompletionNote(s);
                Stop();
                Phase = why.Contains("ABORTED") ? BurnPhase.Failed : BurnPhase.Done;
                Debug.Log(Tag + "burn complete - " + Note + ": "
                          + InitialDvMps.ToString("F2") + " m/s commanded, "
                          + RemainingDvMps.ToString("F2") + " m/s residual (" + why + ")");
                return;
            }

            ThrottleCmd = BurnExec.Throttle(s);
            AttitudeController.Ascent.Throttle = ThrottleCmd;
            AccountForDelivered(TimeWarp.fixedDeltaTime);
        }

        private static void Stop()
        {
            ThrottleCmd = 0.0;
            if (ship != null)
            {
                AttitudeController.Ascent.Throttle = 0.0;
                AttitudeController.Ascent.Release(ship);
                if (boughtRcs && !rcsWasOn) ship.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
            }
        }

        /// <summary>
        /// Subtract what the burn ACTUALLY delivered this tick.
        ///
        /// Integrated from `finalThrust` - what the engines really produced - and not from the
        /// throttle we asked for. An engine that under-performs, is starved, or flames out therefore
        /// shortens nothing: the remaining Δv simply stops going down and the burn keeps running.
        /// Assuming the commanded throttle was delivered is how a burn reports success on a stage
        /// that never lit.
        ///
        /// Along the vessel's own thrust axis rather than along the aim, because those differ while
        /// the controller is still settling and the difference is real Δv going somewhere else.
        /// </summary>
        private static void AccountForDelivered(double dt)
        {
            if (dt <= 0.0 || ship == null) return;
            double thrust = LiveThrust(ship);
            if (thrust <= 0.0) return;

            double mass = ship.GetTotalMass();
            if (mass <= 0.0) return;

            double dv = thrust / mass * dt;
            dvRemaining -= (Vector3d)ship.ReferenceTransform.up * dv;
        }

        /// <summary>Thrust the engines are ACTUALLY producing, kN. Not the maximum.</summary>
        private static double LiveThrust(Vessel v)
        {
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (es[m].EngineIgnited && !es[m].flameout) t += es[m].finalThrust;
            }
            return t;
        }

        private static double Thrust(Vessel v)
        {
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (!es[m].flameout && es[m].isEnabled) t += es[m].MaxThrustOutputVac(true);
            }
            return t;
        }

        /// <summary>
        /// `StNodeSafe`. Would this burn leave periapsis below the floor?
        ///
        /// Energy and angular momentum after the impulse, so it costs nothing and can be asked of
        /// every candidate a solver produces. Escaping counts as a refusal too - a hyperbolic result
        /// from a rendezvous solver means the solver is wrong, not that we should fly it.
        /// </summary>
        public static bool PeriapsisSafe(Vessel v, Vector3d dv, out string why)
        {
            why = "";
            CelestialBody b = v.mainBody;
            if (b == null) { why = "no body"; return false; }

            Vector3d r = v.CoM - b.position;
            Vector3d vel = v.obt_velocity + dv;
            double rm = r.magnitude;
            double mu = b.gravParameter;
            if (rm <= 0.0 || mu <= 0.0) { why = "degenerate state"; return false; }

            double energy = vel.sqrMagnitude / 2.0 - mu / rm;
            if (energy >= 0.0) { why = "it is an escape trajectory"; return false; }

            double sma = -mu / (2.0 * energy);
            Vector3d h = Vector3d.Cross(r, vel);
            double ecc = Math.Sqrt(Math.Max(0.0, 1.0 - h.sqrMagnitude / (sma * mu)));
            double peri = sma * (1.0 - ecc) - b.Radius;

            if (peri < b.atmosphereDepth)
            {
                why = "it would leave periapsis at " + (peri / 1000.0).ToString("F1")
                    + " km, below the " + (b.atmosphereDepth / 1000.0).ToString("F1") + " km floor";
                return false;
            }
            return true;
        }
    }
}
