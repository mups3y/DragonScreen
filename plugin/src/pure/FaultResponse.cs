/*
 * DragonScreen - FaultResponse
 *
 * PURE. The DECISION layer of the Layer-3 autonomy stack (docs/LAYER3_AUTONOMY_PLAN.md). A
 * HealthMonitor says WHAT is wrong (a verdict on one system); this says WHAT TO DO about it. It is the
 * "decide for itself" half of a true autopilot - the part that, told an engine is under-performing,
 * chooses to re-plan the ascent rather than sit and watch the apoapsis fall short.
 *
 * ---- THE RECOVERY LADDER IS REAL FDIR, LEAST-INTERVENTION FIRST ----
 * Flight FDIR escalates: try the local, cheap recovery before the drastic, system-level one, and only
 * fall through to safe mode when nothing local can hold the mission (NASA/ESA FDIR levels; the
 * model-based-FDIR survey literature). Ours, in strict order of severity:
 *
 *      Continue    nominal, or inside tolerance - fly the plan
 *      Retry       transient / actuation hiccup - re-attempt the SAME action (relight, settle, re-align)
 *      Reconfigure switch a mode or redundancy WITHOUT changing the plan (fewer engines, flip a sign)
 *      Replan      recompute the guidance for the new reality (engine-out re-solve, re-phase)
 *      Downmode    accept a REDUCED objective (a reachable point instead of the barge; a safe hold)
 *      Abort       the plan cannot be recovered - hand to AbortResponder (escape / retreat / safe-hold)
 *
 * The enum is ORDERED by severity, so when several monitors fire at once the conductor takes the
 * WORST recovery (Worst()) - the escalation is automatic, not a special case.
 *
 * ---- THE MAPPING IS NOT INVENTED. IT IS WHAT THE CODE ALREADY DOES, MADE EXPLICIT. ----
 * Every (fault, domain) pair below is the choice some existing controller already makes ad hoc; this
 * file only names it and gives it one home so the responses become a coherent policy instead of
 * scattered guards (the whole point of Layer 3 - unify, do not restart). The seed for each is cited
 * at the branch.
 *
 * ---- WHY A SEPARATE DOMAIN ENUM, NOT MissionPhase ----
 * MissionPhase (pure/MissionPhase.cs) is the CAPSULE's observable display phase - and it says so: it
 * cannot see the returning first stage at all, and it returns Coast rather than guess intent. FDIR
 * needs the FLIGHT REGIME the fault occurred in, which spans the booster recovery MissionPhase has no
 * word for, and is driven by which controller is engaged (known to the glue), not by observation. So
 * FaultDomain is its own small, honest enum. The glue maps engaged-controller -> domain in one place.
 */
namespace DragonScreen
{
    /// <summary>What the autopilot does about a fault, ordered by severity (Continue cheapest, Abort
    /// most drastic). Comparing two with <c>&gt;</c> gives the more severe.</summary>
    public enum Recovery : byte
    {
        Continue = 0,
        Retry = 1,
        Reconfigure = 2,
        Replan = 3,
        Downmode = 4,
        Abort = 5
    }

    /// <summary>
    /// The kind of fault, independent of which system reported it. A concrete monitor maps its own
    /// verdict onto one of these so the response policy can be written once for all of them.
    /// </summary>
    public enum FaultKind : byte
    {
        None = 0,
        /// <summary>Producing less Δv-rate than the guidance expects - a weak, starved or unlit engine,
        /// or an on-axis burn delivering nothing (BurnExec progress; NodeExecutor's finalThrust accounting).</summary>
        ThrustShortfall,
        /// <summary>Delivering Δv the WRONG WAY along the intended axis - an inverted translation or an
        /// off-axis burn growing its own residual (BurnExec.Runaway; NodeExecutor's RCS sign self-correct).</summary>
        ThrustReversed,
        /// <summary>The trajectory error is GROWING, not shrinking - apoapsis running away, the
        /// circularisation diverging (Ascent's APOAPSIS RUNAWAY / CIRCULARISATION DIVERGING aborts).</summary>
        TrajectoryDiverging,
        /// <summary>The error is not falling though it is not diverging either - a stalled burn that is
        /// making no progress (NodeExecutor's progress-based backstop).</summary>
        ConvergenceStalled,
        /// <summary>The objective cannot be met from here - not enough thrust to stop (Landing.NoSolution),
        /// or a planned burn that would drop periapsis below the floor (NodeExecutor.PeriapsisSafe refusal).</summary>
        NoControlSolution,
        /// <summary>An unplanned keep-out-sphere penetration or corridor departure during prox ops - the
        /// real Crew Dragon rule that any KOS breach commands an automatic abort (AbortResponder Retreat).</summary>
        KeepOutBreach,
        /// <summary>Propellant or consumables below the margin the phase needs (the return budget; the
        /// landing reserve; the life-support NO-GO gates).</summary>
        ResourceCritical,
        /// <summary>The state estimate itself is untrustworthy - guidance cannot be flown on a bad state.</summary>
        SensorInvalid
    }

    /// <summary>The flight regime a fault occurred in. Set by the glue from the engaged controller, not
    /// observed - see the header on why this is separate from MissionPhase.</summary>
    public enum FaultDomain : byte
    {
        Ascent = 0,
        /// <summary>The returning first stage (BoosterRecovery / Landing).</summary>
        BoosterRecovery,
        /// <summary>Free flight and phasing - no station in the immediate corridor.</summary>
        OrbitCoast,
        /// <summary>Proximity operations near the station, where the keep-out sphere is live.</summary>
        Rendezvous,
        Docked,
        /// <summary>The de-orbit burn.</summary>
        Deorbit,
        /// <summary>Atmospheric entry and descent.</summary>
        Entry
    }

    public static class FaultResponse
    {
        /// <summary>
        /// Choose ONE recovery for one fault in one regime at one verdict. Deterministic and total -
        /// every case is covered, and an unrecognised fault yields Continue rather than a surprise.
        ///
        /// The pattern in the table: a DEGRADED verdict gets the cheap, local recovery; a FAILED
        /// verdict escalates one rung. A Nominal verdict, or FaultKind.None, is always Continue.
        /// </summary>
        public static Recovery Decide(FaultKind kind, HealthVerdict verdict, FaultDomain domain)
        {
            if (kind == FaultKind.None || verdict == HealthVerdict.Nominal) return Recovery.Continue;
            bool failed = verdict == HealthVerdict.Failed;

            switch (kind)
            {
                // A weak/unlit engine: FIRST coax it to produce (settle ullage, relight - the
                // NodeExecutor + Ascent ullage seeds); if it stays weak, RE-PLAN on the thrust actually
                // available - UPFG re-solves Tgo on reduced thrust, exactly as a real launch vehicle
                // flies an engine-out. The booster instead RE-CONFIGURES its engine count live
                // (Landing.LandingEngines) as its first move, then re-plans the reserve if that is not enough.
                case FaultKind.ThrustShortfall:
                    if (domain == FaultDomain.BoosterRecovery)
                        return failed ? Recovery.Replan : Recovery.Reconfigure;
                    return failed ? Recovery.Replan : Recovery.Retry;

                // Wrong-way delivery: FIRST flip the sign / re-establish the aim (NodeExecutor's one-shot
                // RCS-sign self-correct); if it persists it is a genuine off-axis runaway (BurnExec.Runaway),
                // so stop the burn and RE-PLAN the orbit before it is wrecked (flight_0825_163535 drove
                // periapsis to -18 km delivering the wrong way for 8m39s).
                case FaultKind.ThrustReversed:
                    return failed ? Recovery.Replan : Recovery.Reconfigure;

                // A growing trajectory error. On ASCENT a diverging apoapsis is the one failure that loses
                // the whole vehicle and crew (an escape trajectory), so a confirmed one ABORTS - which is
                // exactly what Ascent's APOAPSIS RUNAWAY / CIRCULARISATION DIVERGING guards already do; a
                // milder divergence re-plans before it runs away. On ENTRY a diverging footprint cannot be
                // aborted, so DOWNMODE to the reachable point. Elsewhere, re-plan.
                case FaultKind.TrajectoryDiverging:
                    if (domain == FaultDomain.Ascent) return failed ? Recovery.Abort : Recovery.Replan;
                    if (domain == FaultDomain.Entry)  return failed ? Recovery.Downmode : Recovery.Replan;
                    return Recovery.Replan;

                // A stalled burn / stuck rendezvous. ON ORBIT (rendezvous / coast) a plan that has FROZEN
                // cannot be recovered by recomputing the same geometry forever - the crew's safety comes
                // before the docking objective, so a confirmed stall DOWNMODES: abandon the rendezvous and
                // come home (ReturnFallback / RendezvousFdir). This is the fault flight_0826_014654 had no
                // autonomous answer to - a HUMAN pressed CANCEL. The degraded step still RE-PLANS first
                // (abort the stuck node, recompute the leg) before it escalates. Elsewhere (a burn that
                // stalls mid-descent) the old ladder holds: RETRY while trying, RE-PLAN once genuinely stuck.
                case FaultKind.ConvergenceStalled:
                    if (domain == FaultDomain.Rendezvous)
                        return failed ? Recovery.Downmode : Recovery.Replan;
                    return failed ? Recovery.Replan : Recovery.Retry;

                // Cannot meet the objective from here. For a physical descent (booster/entry) there is no
                // re-plan that finds thrust that is not there: RECONFIGURE to maximum authority (all
                // engines - Landing.NoSolution fires everything), and if even that cannot, DOWNMODE and
                // accept the least-bad outcome. A PLANNING refusal (an unsafe burn) is different - the plan
                // is wrong, not the vehicle - so RE-PLAN a burn that clears the floor.
                case FaultKind.NoControlSolution:
                    if (domain == FaultDomain.BoosterRecovery || domain == FaultDomain.Entry)
                        return failed ? Recovery.Downmode : Recovery.Reconfigure;
                    return Recovery.Replan;

                // A keep-out breach is the real Crew Dragon automatic-abort rule. Drifting toward the
                // corridor edge holds (DOWNMODE - stop closing, station-keep); an actual penetration ABORTS
                // to the retreat (AbortResponder.Retreat).
                case FaultKind.KeepOutBreach:
                    return failed ? Recovery.Abort : Recovery.Downmode;

                // Thin margin trims the objective (DOWNMODE - reserve more, fly a shorter approach); below
                // the floor, do not commit an irreversible burn - safe-hold (ABORT). This is the
                // life-support / return-budget NO-GO discipline, made a live response rather than a gate.
                case FaultKind.ResourceCritical:
                    return failed ? Recovery.Abort : Recovery.Downmode;

                // A bad state estimate: you cannot fly guidance on it. Degraded, wait/re-acquire (RETRY);
                // failed, safe-hold (ABORT) rather than guide on numbers you do not trust.
                case FaultKind.SensorInvalid:
                    return failed ? Recovery.Abort : Recovery.Retry;
            }
            return Recovery.Continue;
        }

        /// <summary>The more severe of two recoveries - so several concurrent faults escalate to the
        /// worst response automatically.</summary>
        public static Recovery Worst(Recovery a, Recovery b)
        {
            return (byte)a >= (byte)b ? a : b;
        }

        public static string Name(Recovery r)
        {
            switch (r)
            {
                case Recovery.Retry:       return "RETRY";
                case Recovery.Reconfigure: return "RECONFIGURE";
                case Recovery.Replan:      return "REPLAN";
                case Recovery.Downmode:    return "DOWNMODE";
                case Recovery.Abort:       return "ABORT";
                default:                   return "CONTINUE";
            }
        }
    }
}
