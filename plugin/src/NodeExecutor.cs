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

        /// <summary>Δv actually delivered so far (planned minus remaining) - the burn-path proof: an RCS
        /// burn that reads 0 here delivered nothing however long it "burned". For the recorder.</summary>
        public static double DeliveredDvMps { get { return InitialDvMps - RemainingDvMps; } }
        /// <summary>This burn is on RCS/Draco translation, not the main-engine throttle. For the recorder.</summary>
        public static bool RcsBurn { get { return rcsBurn; } }
        /// <summary>Seconds until ignition (negative once past), or 0 when idle. For the recorder.</summary>
        public static double TimeToIgnitionS
        {
            get { return Active ? ignitionUt - Planetarium.GetUniversalTime() : 0.0; }
        }

        private static Vessel ship;
        private static Vector3d dvWorld;        // the ORIGINAL request, for the overshoot test
        private static Vector3d dvRemaining;
        private static double nodeUt, ignitionUt, startedBurnAt;
        private static bool rcsWasOn, boughtRcs, warpRequested, warpRefused, orientWarpRequested;

        /// <summary>Burn on RCS forward-translation (Draco), not main-engine throttle. Set by Begin.</summary>
        private static bool rcsBurn;
        /// <summary>Previous-tick orbital velocity, for measuring the Δv an RCS burn actually delivered
        /// (RCS thrust is not in LiveThrust, so the throttle-based accounting cannot see it).</summary>
        private static Vector3d prevObtVel;
        /// <summary>Lowest residual seen this burn - the backstop clock resets whenever it falls, so a slow
        /// but PROGRESSING RCS burn never trips the runaway timeout; only a stalled one does.</summary>
        private static double lastProgressDv;

        /// <summary>Arrive this long before ignition, so there is time to drop out of warp.</summary>
        public const double WarpArriveLeadS = 6.0;
        /// <summary>Do not bother warping a wait shorter than this. `DgWarpTo`'s 12 s.</summary>
        public const double WarpWorthwhileS = 12.0;
        /// <summary>
        /// Point at the Δv only once ignition is within this, seconds. Before it, the coast is warped
        /// with the controller RELEASED - the crew's rule (2026-08-19): "warp to within 10 minutes of
        /// the node and ONLY then point the right way for the burn." No attitude is held across the
        /// coast, so a plotted intercept is not nudged, and the tank is not drained holding for minutes
        /// (flight_0819). In stock the align inside this window is flown on reaction wheels; in RO there
        /// are none, so it turns on RCS - but the dead coast BEFORE the window is still warped on rails
        /// (thrusters do not fire), so the RCS cost is only the turn itself, not the full ten minutes.
        /// </summary>
        public const double OrientLeadS = 600.0;

        /// <summary>Spool lead for a THROTTLE (main-engine) node burn, seconds - it ramps to full over
        /// roughly this, so the half-burn ignites earlier to keep the impulse on the node. Unused for the
        /// Draco RCS burns (pressure-fed, instant). [Tunable].</summary>
        public const double SpoolLeadS = 1.5;

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
        /// <param name="useRcs">Burn on RCS forward-translation (Draco) instead of the main-engine
        /// throttle. Crew Dragon does its orbital maneuvers on Draco; its SuperDraco reads zero available
        /// thrust to the throttle path (unlit / needs ullage), so a throttle burn delivered NOTHING and the
        /// residual never fell (flight_0823_233243: apoapsis unchanged, range diverging). See Burn().</param>
        public static bool Begin(Vessel v, Vector3d dv, double atUt, string label, bool useRcs = false)
        {
            if (v == null || dv.sqrMagnitude < 1e-8)
            {
                Note = label + " - nothing to burn";
                Phase = BurnPhase.Failed;
                return false;
            }
            rcsBurn = useRcs;

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

            // Factor the spool into the half-burn lead (user 2026-08-24). An RCS/Draco burn is pressure-fed
            // and instant (0); a throttle burn on a main engine ramps, so it must ignite earlier - the
            // SpoolLeadS lead keeps the impulse centred on the node instead of arriving late.
            double spool = rcsBurn ? 0.0 : SpoolLeadS;
            double half = BurnExec.HalfBurnS(InitialDvMps, v.GetTotalMass(), Thrust(v), spool);
            ignitionUt = atUt - half;

            rcsWasOn = v.ActionGroups[KSPActionGroup.RCS];
            boughtRcs = false;
            warpRequested = false;
            warpRefused = false;
            orientWarpRequested = false;
            Phase = BurnPhase.Aligning;
            Note = label;

            // ⛔ START FROM REAL TIME. A rendezvous phasing warp can hand the executor over at a high warp
            // rate; if it inherits that rate, WarpToOrient cannot decelerate in time and OVERSHOOTS the
            // node - the vehicle drops out PAST ignition still unaligned and lights off-axis. That is the
            // NC phasing burn that aborted "past its backstop" with a full 62 m/s residual
            // (flight_0823_222127). Drop warp here so the executor's own WarpToOrient / WarpToIgnition own
            // the timeline. Paired with the loose-align guard in Align() as a belt-and-suspenders fix.
            if (TimeWarp.CurrentRateIndex > 0) TimeWarp.SetRate(0, true);

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

            // ---- ⛔ WARP TO WITHIN OrientLeadS OF THE NODE BEFORE POINTING AT IT (user, 2026-08-19). ----
            // The crew's rendezvous procedure, verbatim: "warp to within 10 minutes of the manoeuvre
            // node and only then point the right way for the burn. From there warp to the manoeuvre and
            // complete the burn." Orienting far from the node holds attitude the whole coast, and on a
            // capsule that means RCS - which nudges the orbit and RUINS a plotted intercept, and burns
            // the monopropellant the de-orbit needs (flight_0819: the tank was dry before de-orbit). So
            // while ignition is more than OrientLeadS away the controller is RELEASED - no steering, no
            // RCS - and the empty coast is warped. Only inside the window does Align() turn the ship:
            // on reaction wheels in stock, on RCS in RO where there are none (NeedRcsToAlign checks the
            // actual wheel authority, so it buys RCS from the start when the wheels do not exist).
            if (Phase == BurnPhase.Aligning && (ignitionUt - now) > OrientLeadS)
            {
                AttitudeController.Ascent.Throttle = 0.0;
                AttitudeController.Ascent.Release(ship);
                PointingErrorDeg = Vector3d.Angle(ship.ReferenceTransform.up, aim.normalized);
                WarpToOrient(now);
                return;
            }

            // Inside the orient window (or a warp overshot into it), Aligning steers in real time - a turn
            // under rails warp does nothing. Holding runs its own warp-to-ignition, so leave that alone.
            if (Phase == BurnPhase.Aligning && TimeWarp.CurrentRateIndex > 0) TimeWarp.SetRate(0, true);

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
            bool haveWheels = HaveWheelAuthority(ship);

            // RCS is the de-orbit and landing budget. With reaction wheels (stock) it is bought only
            // when the clock says the wheels will not finish the turn in time. Without them (RO strips
            // them - RO_ReactionWheels.cfg) it is the ONLY thing that turns the ship, so NeedRcsToAlign
            // returns true the moment we are off-attitude. See BurnExec.NeedRcsToAlign.
            if (!boughtRcs && BurnExec.NeedRcsToAlign(toIgnition, PointingErrorDeg, haveWheels))
            {
                boughtRcs = true;
                ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
                Debug.Log(Tag + toIgnition.ToString("F0") + " s to ignition and still "
                          + PointingErrorDeg.ToString("F1") + " deg off - RCS "
                          + (haveWheels ? "assisting the turn" : "turning it (no reaction wheels)"));
            }

            // ⛔ THE PAD CLAUSE MUST NOT LIGHT AN OFF-AXIS ENGINE. `toIgnition <= AlignPadS` also fires
            // when the node time has already PASSED (a warp overshoot leaves toIgnition negative). If the
            // vehicle is still tens of degrees off then, handing to Holding lights the engine off-axis and
            // the burn aborts having delivered nothing (the NC failure, flight_0823_222127). Gate the pad
            // transition on a loose alignment; otherwise stay in Align, keep steering in real time, and
            // burn late-but-clean once actually pointed.
            if (BurnExec.Aligned(PointingErrorDeg)
                || (toIgnition <= BurnExec.AlignPadS && PointingErrorDeg < BurnExec.LooseAlignDeg))
            {
                // ⛔ Hand RCS back the way we found it ONLY if wheels can hold the attitude to ignition.
                // With no wheels, dropping RCS here leaves nothing to hold the burn attitude (the engine
                // is not lit yet, and a Dragon maneuvers on Draco RCS in the first place), so KEEP it on
                // through the hold and burn - Stop() restores it at the end. The intervening coast is
                // warped on rails, where thrusters do not fire, so this costs no extra propellant.
                if (boughtRcs && !rcsWasOn && haveWheels) ship.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
                Phase = BurnPhase.Holding;
            }
        }

        /// <summary>
        /// Does this vehicle have usable reaction-wheel torque? ~No in RealismOverhaul, which strips
        /// ModuleReactionWheel (RO_ReactionWheels.cfg: 392 removed, the survivors cut to ~0.1 N·m CMGs).
        /// Capability-measured, not a body/RSS check - the project rule is detect-by-capability - so
        /// stock keeps its wheels and RO is recognised wherever it is flown.
        /// </summary>
        private static bool HaveWheelAuthority(Vessel v)
        {
            if (v == null) return false;
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleReactionWheel> ws =
                    v.parts[i].Modules.GetModules<ModuleReactionWheel>();
                for (int m = 0; m < ws.Count; m++)
                {
                    ModuleReactionWheel w = ws[m];
                    if (w.wheelState != ModuleReactionWheel.WheelState.Active) continue;
                    t += (w.PitchTorque + w.YawTorque + w.RollTorque) * (w.authorityLimiter / 100f);
                    if (t >= WheelAuthorityFloorKNm) return true;
                }
            }
            return false;
        }

        /// <summary>Summed 3-axis reaction-wheel torque below this (kN·m) counts as "no wheels": RO's
        /// stripped/CMG survivors sit near zero, real stock wheels at 5-30.</summary>
        private const double WheelAuthorityFloorKNm = 1.0;

        /// <summary>
        /// Warp the dead coast down to <see cref="OrientLeadS"/> before ignition WITHOUT orienting -
        /// the first half of the crew's procedure. One-shot, like the ignition warp, so a manual cancel
        /// stays cancelled. No one-orbit refusal here: the caller has already bounded the node time (an
        /// intercept is at most `Approach.CaSpanPeriods` orbits out by construction), and refusing a
        /// legitimate multi-lap coast is exactly what would strand the approach sitting in real time.
        /// </summary>
        private static void WarpToOrient(double now)
        {
            if (orientWarpRequested) return;
            double target = ignitionUt - OrientLeadS;
            double wait = target - now;
            if (wait < WarpWorthwhileS) return;

            orientWarpRequested = true;
            Debug.Log(Tag + "warping " + wait.ToString("F0") + " s to within "
                      + (OrientLeadS / 60.0).ToString("F0") + " min of '" + Note
                      + "' - controls released, will orient there");
            TimeWarp.fetch.WarpTo(target);
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
            prevObtVel = ship.obt_velocity;             // seed the RCS velocity-delta accounting
            lastProgressDv = RemainingDvMps;            // seed the progress-based backstop
            Phase = BurnPhase.Burning;
            Debug.Log(Tag + "ignition - " + Note + ", " + InitialDvMps.ToString("F2") + " m/s"
                      + (rcsBurn ? " (RCS translation)" : ""));
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

            // ---- ⛔ DO NOT THROTTLE UP UNTIL THE NOSE IS ON THE Δv. ----
            // BurnExec.Throttle sizes on remaining Δv alone - it does not know where the nose points.
            // A small burn (a phasing exit, 3.88 m/s) that ignites before the slew is finished pushes
            // OFF-AXIS: the delivered Δv never reduces the aim-direction remainder, so the residual
            // GROWS and the burn runs to its 300 s backstop. Measured 2026-08-17: 3.88 m/s commanded,
            // 58.58 m/s residual, ABORTED - and that overshoot, three phasing burns of it, is what
            // ran the tank dry before rendezvous. Hold throttle at zero and keep steering until
            // aligned; then burn clean. The attitude controller is already pointed at the Δv.
            bool onAxis = BurnExec.Aligned(PointingErrorDeg);
            double dt = TimeWarp.fixedDeltaTime;

            if (rcsBurn)
            {
                // ---- CREW DRAGON: BURN ON DRACO RCS, NOT THE MAIN-ENGINE THROTTLE. ----
                // Real Crew-2 flies its phasing/rendezvous burns on the 16 Draco thrusters; the SuperDracos
                // are launch-abort only and read zero available thrust to the throttle path, so a throttle
                // burn delivered NOTHING (flight_0823_233243: apoapsis flat, range diverging). UllageFore
                // drives forward RCS translation (s.Z) along the nose - which SteerTo has already put on the
                // Δv - so translating forward burns along the Δv. This is MechJeb's RCS-translation method,
                // tuned to the Dragon. Only push when aligned; measure what actually went in (RCS is invisible
                // to LiveThrust).
                if (!boughtRcs) { ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true); boughtRcs = true; }
                AttitudeController.Ascent.Throttle = 0.0;
                AttitudeController.Ascent.UllageFore = onAxis ? 1.0 : 0.0;
                ThrottleCmd = onAxis ? 1.0 : 0.0;                  // for the pages/recorder: "translating"
                if (onAxis) AccountByVelocity(dt);
            }
            else
            {
                // ---- RealFuels relight: SETTLE ULLAGE UNTIL THE ENGINE LIGHTS. ----
                // The Dragon's SuperDraco (and any main engine) shuts off during the coast; commanding
                // throttle on unsettled propellant lights nothing (flight_0823_233243: availThrust 0, Δv 0).
                // So while aligned but not yet producing thrust, fire RCS forward (UllageFore) to settle -
                // exactly the booster relight - AND command throttle so it ignites the moment fuel reaches
                // the feed. In vacuum there is no dynamic-pressure ignition penalty, so it lights cleanly.
                bool lit = LiveThrust(ship) > 1.0;
                if (onAxis && !lit)
                {
                    if (!boughtRcs) { ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true); boughtRcs = true; }
                    AttitudeController.Ascent.UllageFore = 1.0;
                }
                else AttitudeController.Ascent.UllageFore = 0.0;
                ThrottleCmd = onAxis ? BurnExec.Throttle(s) : 0.0;
                AttitudeController.Ascent.Throttle = ThrottleCmd;
                if (onAxis) AccountForDelivered(dt);
            }

            // ---- PROGRESS-BASED BACKSTOP. The runaway timer counts time since the residual LAST FELL,
            // not total burn time - so a slow-but-working Draco burn (a 59 m/s phasing raise takes minutes)
            // never trips it, while a genuinely stalled burn (off-axis, dead engine, no thrust) still aborts
            // within MaxBurnDurationS. Slewing off-axis also resets it (it is not burn time either).
            bool progressed = RemainingDvMps < lastProgressDv - 0.01;
            if (progressed) lastProgressDv = RemainingDvMps;
            if (progressed || !onAxis) startedBurnAt = now;
        }

        private static void Stop()
        {
            ThrottleCmd = 0.0;
            if (ship != null)
            {
                AttitudeController.Ascent.Throttle = 0.0;
                AttitudeController.Ascent.UllageFore = 0.0;      // stop any RCS-translation burn
                AttitudeController.Ascent.Release(ship);
                if (boughtRcs && !rcsWasOn) ship.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
            }
            rcsBurn = false;
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

        /// <summary>
        /// Decrement the remaining Δv by the vehicle's ACTUAL velocity change this tick minus the
        /// gravitational part - propulsion-agnostic accounting for an RCS burn, whose thrust LiveThrust
        /// cannot see. Gravity over one physics tick is `-mu·r/|r|³·dt`, exact enough to isolate the small
        /// per-tick thrust Δv (the two are ~0.18 vs ~0.004 m/s, so the gravity term is computed exactly,
        /// not measured). Integrated over the burn this is the delivered Δv.
        /// </summary>
        private static void AccountByVelocity(double dt)
        {
            if (dt <= 0.0 || ship == null || ship.mainBody == null) return;
            Vector3d nowVel = ship.obt_velocity;
            Vector3d r = ship.CoM - ship.mainBody.position;
            double rm = r.magnitude;
            if (rm > 1.0)
            {
                Vector3d grav = -ship.mainBody.gravParameter / (rm * rm * rm) * r;   // world accel
                Vector3d dvThrust = (nowVel - prevObtVel) - grav * dt;               // remove gravity
                dvRemaining -= dvThrust;
            }
            prevObtVel = nowVel;
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
            // ⛔ AN RCS BURN'S THRUST IS THE DRACO, NOT THE (unlit) SUPERDRACO. Only used for the
            // half-burn LEAD that centres the burn on the node; the cutoff is measured off the orbit, so
            // an approximate figure is fine, but the main-engine sum below would hand back the SuperDraco's
            // ~680 kN for a Draco burn and start it far too late. Estimate the fore/aft RCS thrust from the
            // live thrusterPower x thrustPercentage x nozzle count, times a translation-alignment fraction.
            if (rcsBurn) return RcsTranslationThrust(v);

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

        /// <summary>Approximate fore/aft RCS translation thrust, kN: live per-nozzle power x nozzle count,
        /// scaled by the fraction of nozzles that point along a translation axis. See Thrust().</summary>
        private static double RcsTranslationThrust(Vessel v)
        {
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleRCS> rs =
                    v.parts[i].Modules.GetModules<ModuleRCS>();
                for (int m = 0; m < rs.Count; m++)
                {
                    ModuleRCS rcs = rs[m];
                    if (!rcs.isEnabled || rcs.isJustForShow || rcs.flameout) continue;
                    int nz = (rcs.thrusterTransforms != null && rcs.thrusterTransforms.Count > 0)
                             ? rcs.thrusterTransforms.Count : 1;
                    t += rcs.thrusterPower * (rcs.thrustPercentage * 0.01) * nz;
                }
            }
            return t * RcsAlignFraction;
        }

        /// <summary>Fraction of the RCS nozzles that push along a given translation axis - the rest point
        /// off-axis (roll/pitch/lateral) and do not add to fore thrust. A coarse constant; the burn cutoff
        /// is measured, so it only has to get the half-burn lead into the right ballpark.</summary>
        public const double RcsAlignFraction = 0.4;

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
