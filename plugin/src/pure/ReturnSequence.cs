// DragonScreen — ReturnSequence  (PURE: undock → departure → deorbit → entry → chutes → splash)
// ============================================================================================
// Register T21, §B9 Phases 6–10 / §B10.4 / §B10.5 / §B13. The return leg, expressed the same way
// `pure/AscentSequence.cs` expresses the ascent: an event chain driven by MEASURED state, deciding
// WHAT to actuate and never actuating anything.
//
// ---- ⭐ WHY A SEQUENCER AND NOT JUST `Conductor.Decide` ----
// `pure/Conductor.cs`'s phase table (T16, §B12.3 verbatim) answers *which MechJeb module* a phase wants.
// It deliberately does not answer the ORDER of the things MechJeb does not do, and the return is mostly
// those things: back away, jettison the trunk, close the nose cone, deploy the drogues, deploy the
// mains. §B12.3's own Entry row even says the deorbit node is planned *"beforehand"* without saying by
// whom, and its Drogues/Mains row is *"chute triggering"* — an actuation, not a module. So the split is
// the same one T18 used and for the same reason:
//     `Conductor.Decide` → the SAFETY ORDER (abort ⟩ complete ⟩ hold ⟩ advance) and the module choice
//     `ReturnSequence`   → the ORDER of the return's own events, and what to actuate for each
//
// ---- THE THREE PLAN STEPS THIS COVERS ----
// `pure/ModeManager.cs`'s return is:
//     gate G14 "GO FOR UNDOCK"
//     Fly(Phasing, "Departure & phasing")     ← the DEPARTURE leg  (§B9 P6, + the trunk)
//     gate G15 "GO FOR DEORBIT BURN"
//     Fly(Entry,   "Deorbit → lifting entry") ← the ENTRY leg      (§B9 P7 + P8)
//     Fly(Drogues, "Drogues → mains → splashdown") ← the DESCENT leg (§B9 P9 + P10)
// ⚠ The departure step is `MissionPhase.Phasing` — the SAME phase as the outbound rendezvous.
// `CrewProcedureOps.IsReturn` is what distinguishes them, and it is set the moment G14 clears.
//
// ---- WHERE THE NUMBERS COME FROM, AND THE ONE THAT IS AN ESTIMATE ----
// Documented, in-repo, tier-1:
//   • drogues **5486 m**, mains **1830 m** — `Mission.DrogueAltitude` / `Mission.MainAltitude`, already
//     in `pure/MissionPhase.cs` with "REAL NUMBERS, from NASA/SpaceX sources. See §8." ⛔ NOT redefined
//     here: one source, read from where it already lives.
//   • the departure clearance — §B11's **4 km Approach Ellipsoid**, read from `RendezvousOps`, so the
//     leg the Dragon leaves by is the boundary it arrived through. No new constant.
//   • entry attitude — §B10.5 / O8: **SURFACE_RETROGRADE, heat shield forward, and `force_roll` NOT
//     engaged**. O8 (owner, 2026-09-03) settled that the baseline is *"attitude hold, NO active
//     steering … pure ballistic, no commanded bank"*.
//
// ⛔⛔ AND ONE NUMBER THAT IS AN **ENGINEERING ESTIMATE BY THIS BUILD CHAT**, SAID SO IN CAPITALS
// BECAUSE §1.4 RESERVES TIER-3 INVENTION FOR OWNER DISCUSSION: **the deorbit target periapsis.**
// §B10.2 says only *"deorbit `new_periapsis` = a low/negative value putting entry FPA in-corridor"* —
// a SHAPE, not a value. §B11 gives the entry interface as **122 km [DOC]** and the entry flight-path
// angle as **−1.4° to −1.6° [EST]**, and lists that FPA among *"the four numbers to pin empirically
// in-sim"*. There is no target periapsis anywhere in the repo.
//   The seed below is **50 km**, and the whole of its reasoning is: it is comfortably BELOW the 122 km
//   entry interface, so the trajectory cannot skip; and it is well ABOVE zero, so the entry is not
//   needlessly steep — §B11 puts Dragon's nominal peak decel at 4–4.5 g against a 7–8 g capsule
//   worst case, and periapsis is the knob that moves it.
// ⚠ **THAT IS A JUSTIFICATION, NOT A SOURCE.** It arrives on `ReturnInputs` from the caller so T22 can
// move it without touching this rule, it is the FIRST owner question on T21's register line, and it is
// the item on the flight checklist most likely to need changing. Nothing else here is invented.
//
// PURE: no Unity, no KSP, no MechJeb reference. `Step` is a function of its inputs and holds no clock.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>Where the return has got to.</summary>
    public enum ReturnStep : byte
    {
        /// <summary>Not on the return leg.</summary>
        Idle = 0,
        /// <summary>§B9 P6 — backing away from the station on RCS, retrograde-ish.</summary>
        Backout,
        /// <summary>Clear of the proximity zone; the trunk goes next.</summary>
        TrunkJettison,
        /// <summary>Departure leg finished — the plan may raise the G15 deorbit poll.</summary>
        Departed,
        /// <summary>§B9 P7 — building the deorbit node (`OperationPeriapsis`).</summary>
        DeorbitPlan,
        /// <summary>...and flying it with the Node Executor.</summary>
        DeorbitBurn,
        /// <summary>Nose cone closed and locked for entry.</summary>
        NoseCone,
        /// <summary>§B9 P8 / O8 — heat shield forward, no commanded bank, all the way down.</summary>
        EntryAttitude,
        /// <summary>§B9 P9 — drogues out at the real 5486 m.</summary>
        Drogues,
        /// <summary>...mains out at the real 1830 m.</summary>
        Mains,
        /// <summary>§B9 P10 — down. Control goes back to the crew.</summary>
        Splashdown,
        /// <summary>The return is over.</summary>
        Complete
    }

    /// <summary>What the glue must actuate this tick.</summary>
    public enum ReturnAct : byte
    {
        None = 0,
        /// <summary>SmartASS target-minus / retrograde + RCS: open the range from the station.</summary>
        BackAway,
        /// <summary>`Actuator.JettisonTrunk` — the trunk decoupler by role. ⛔ NOT the Dragon decoupler.</summary>
        JettisonTrunk,
        /// <summary>Build the `OperationPeriapsis` deorbit node.</summary>
        PlanDeorbit,
        /// <summary>Hand it to the Node Executor.</summary>
        BurnDeorbit,
        /// <summary>`Actuator.CloseNoseShroud` — closed and locked before entry interface.</summary>
        CloseNoseCone,
        /// <summary>SmartASS SURFACE_RETROGRADE. ⛔ O8: no commanded roll, no bank.</summary>
        HoldHeatShieldForward,
        /// <summary>`Actuator.DeployChutes(v, drogue: true)` at 5486 m.</summary>
        DeployDrogues,
        /// <summary>`Actuator.DeployChutes(v, drogue: false)` at 1830 m.</summary>
        DeployMains,
        /// <summary>Give the vehicle back — §B9 P10, "conductor releases control".</summary>
        ReleaseControl
    }

    /// <summary>The measured state one return decision is made from, plus the caller's numbers.</summary>
    public struct ReturnInputs
    {
        // ── measured ────────────────────────────────────────────────────────────────────────────
        /// <summary>Range to the station, metres. 0 = no target / already far away.</summary>
        public double RangeM;
        /// <summary>Still hard-mated. ⛔ Nothing on the return may act while this is true.</summary>
        public bool Docked;
        /// <summary>A trunk part is still on this vessel.</summary>
        public bool TrunkAttached;
        /// <summary>Altitude above the surface (or sea level), metres.</summary>
        public double AltitudeM;
        /// <summary>Descending — the chute steps must never arm on the way UP.</summary>
        public bool Descending;
        public bool DroguesOut, MainsOut;
        public bool Splashed;
        /// <summary>A deorbit node has been built for this step (a LATCH — see `MechConductor`).</summary>
        public bool NodeExists;
        /// <summary>...and the Node Executor flew it to completion.</summary>
        public bool NodeBurned;
        /// <summary>Time in the current step, measured by the caller.</summary>
        public double SinceStepS;

        // ── the caller's numbers ────────────────────────────────────────────────────────────────
        /// <summary>Range at which the departure counts as clear. §B11's 4 km Approach Ellipsoid.</summary>
        public double DepartureClearanceM;
        /// <summary>⛔ THE ONE ENGINEERING ESTIMATE — see this file's header, and T21's Q1.</summary>
        public double DeorbitPeriapsisM;
        /// <summary>Drogue altitude. `Mission.DrogueAltitude`, 5486 m.</summary>
        public double DrogueAltitudeM;
        /// <summary>Main altitude. `Mission.MainAltitude`, 1830 m.</summary>
        public double MainAltitudeM;

        /// <summary>The documented values, read from where each already lives.</summary>
        public static ReturnInputs Nominal()
        {
            ReturnInputs s = new ReturnInputs();
            s.DepartureClearanceM = RendezvousOps.ApproachEllipsoidM;   // §B11's 4 km
            s.DeorbitPeriapsisM   = DeorbitPeriapsisEstimateM;
            s.DrogueAltitudeM     = Mission.DrogueAltitude;             // 5486 m, §8
            s.MainAltitudeM       = Mission.MainAltitude;               // 1830 m, §8
            return s;
        }

        /// <summary>
        /// ⛔⛔ **A TIER-3 ENGINEERING ESTIMATE BY A BUILD CHAT (T21, 2026-09-07), NOT A SOURCED VALUE.**
        /// §B10.2 gives only the shape ("a low/negative value putting entry FPA in-corridor"); §B11's
        /// entry FPA is itself [EST] and one of "the four numbers to pin empirically in-sim". 50 km is
        /// below the documented 122 km entry interface so the trajectory cannot skip, and well above
        /// zero so the entry is not needlessly steep (§B11: Dragon nominal ~4–4.5 g vs a 7–8 g capsule
        /// worst case). ⚠ That is a justification, not a source. T22 pins it; T21's Q1 raises it.
        /// </summary>
        public const double DeorbitPeriapsisEstimateM = 50000.0;
    }

    /// <summary>One return decision.</summary>
    public struct ReturnDecision
    {
        public ReturnStep Next;
        public ReturnAct  Act;
        public string     Reason;

        public static ReturnDecision Of(ReturnStep next, ReturnAct act, string why)
        { ReturnDecision d; d.Next = next; d.Act = act; d.Reason = why; return d; }

        /// <summary>Stay put and do nothing. ⛔ Only ever called with the step already occupied.</summary>
        public static ReturnDecision Stay(ReturnStep here, string why)
        { return Of(here, ReturnAct.None, why); }

        public override string ToString()
        {
            return Next.ToString() + (Act != ReturnAct.None ? " / " + Act : "")
                 + (string.IsNullOrEmpty(Reason) ? "" : " — " + Reason);
        }
    }

    /// <summary>The pure return event chain. One entry point, no state, no clock.</summary>
    public static class ReturnSequence
    {
        /// <summary>
        /// Advance the return one tick.
        ///
        /// ⛔ NOTHING HERE READS A MISSION CLOCK. Every transition is a measured range, a measured
        /// altitude, a measured node state or a measured splash — the same rule `pure/BarEvent.cs`
        /// imposes on the ascent callouts and for the same reason.
        /// </summary>
        public static ReturnDecision Step(ReturnInputs s, ReturnStep here)
        {
            // ⛔ THE HARD GUARD, ABOVE EVERYTHING. While the hooks are still closed the return has not
            // begun, and NOTHING on it may fire — not a back-away, and above all not the trunk
            // decoupler on a vehicle attached to a space station.
            if (s.Docked && here != ReturnStep.Idle)
                return ReturnDecision.Stay(here, "still hard-mated — the return does not act while docked");

            switch (here)
            {
                case ReturnStep.Idle:
                    if (s.Docked)
                        return ReturnDecision.Stay(ReturnStep.Idle, "docked — waiting for the crew's undock");
                    return ReturnDecision.Of(ReturnStep.Backout, ReturnAct.BackAway,
                                             "undocked — backing away from the station");

                // §B9 P6: "SmartASS (retrograde/target) for the backout … keep clear of the Keep-Out
                // Sphere." The clearance is §B11's own Approach Ellipsoid — the Dragon leaves by the
                // boundary it arrived through, which needs no new number.
                case ReturnStep.Backout:
                    if (s.RangeM > 0.0 && s.RangeM < s.DepartureClearanceM)
                        return ReturnDecision.Of(ReturnStep.Backout, ReturnAct.BackAway,
                                                 "backing away — " + (s.RangeM / 1000.0).ToString("F2")
                                                 + " km of " + (s.DepartureClearanceM / 1000.0).ToString("F1"));
                    return ReturnDecision.Of(ReturnStep.TrunkJettison, ReturnAct.JettisonTrunk,
                                             "clear of the proximity zone — trunk jettison");

                case ReturnStep.TrunkJettison:
                    if (s.TrunkAttached)
                        return ReturnDecision.Of(ReturnStep.TrunkJettison, ReturnAct.JettisonTrunk,
                                                 "trunk still attached — re-commanding the decoupler");
                    return ReturnDecision.Of(ReturnStep.Departed, ReturnAct.None,
                                             "trunk away — departure complete, GO for deorbit next");

                // The departure leg ends here and the plan raises G15. The crew's GO is what starts the
                // deorbit; this step does not walk past it on its own.
                case ReturnStep.Departed:
                    return ReturnDecision.Stay(ReturnStep.Departed, "departed — holding for the deorbit GO");

                // §B9 P7 / §B12.3: "deorbit via OperationPeriapsis … then SmartASS heat-shield-forward".
                case ReturnStep.DeorbitPlan:
                    if (!s.NodeExists)
                        return ReturnDecision.Of(ReturnStep.DeorbitPlan, ReturnAct.PlanDeorbit,
                                                 "planning the deorbit burn");
                    return ReturnDecision.Of(ReturnStep.DeorbitBurn, ReturnAct.BurnDeorbit,
                                             "deorbit node built — burning");

                case ReturnStep.DeorbitBurn:
                    if (!s.NodeBurned)
                        return ReturnDecision.Of(ReturnStep.DeorbitBurn, ReturnAct.BurnDeorbit,
                                                 "deorbit burn running");
                    return ReturnDecision.Of(ReturnStep.NoseCone, ReturnAct.CloseNoseCone,
                                             "deorbit burn complete — nose cone closed and locked");

                case ReturnStep.NoseCone:
                    return ReturnDecision.Of(ReturnStep.EntryAttitude, ReturnAct.HoldHeatShieldForward,
                                             "entry attitude — heat shield forward (O8: no commanded bank)");

                // ⛔ THE CHUTE GATES ARE ALTITUDE **AND** DESCENT, NOT ALTITUDE ALONE. A vehicle can be
                // below 5486 m on the way UP — an abort, a lofted trajectory — and a drogue deployed
                // into that is a drogue destroyed.
                case ReturnStep.EntryAttitude:
                    if (s.Descending && s.AltitudeM <= s.DrogueAltitudeM)
                        return ReturnDecision.Of(ReturnStep.Drogues, ReturnAct.DeployDrogues,
                                                 "drogues — " + s.DrogueAltitudeM.ToString("F0") + " m");
                    return ReturnDecision.Of(ReturnStep.EntryAttitude, ReturnAct.HoldHeatShieldForward,
                                             "entry — holding heat shield forward");

                case ReturnStep.Drogues:
                    if (s.Splashed)
                        return ReturnDecision.Of(ReturnStep.Splashdown, ReturnAct.ReleaseControl,
                                                 "splashdown");
                    if (s.Descending && s.AltitudeM <= s.MainAltitudeM)
                        return ReturnDecision.Of(ReturnStep.Mains, ReturnAct.DeployMains,
                                                 "mains — " + s.MainAltitudeM.ToString("F0") + " m");
                    if (!s.DroguesOut)
                        return ReturnDecision.Of(ReturnStep.Drogues, ReturnAct.DeployDrogues,
                                                 "drogues have not deployed — re-commanding");
                    return ReturnDecision.Stay(ReturnStep.Drogues, "under drogues");

                case ReturnStep.Mains:
                    if (s.Splashed)
                        return ReturnDecision.Of(ReturnStep.Splashdown, ReturnAct.ReleaseControl,
                                                 "splashdown");
                    if (!s.MainsOut)
                        return ReturnDecision.Of(ReturnStep.Mains, ReturnAct.DeployMains,
                                                 "mains have not deployed — re-commanding");
                    return ReturnDecision.Stay(ReturnStep.Mains, "under mains");

                case ReturnStep.Splashdown:
                    return ReturnDecision.Of(ReturnStep.Complete, ReturnAct.None,
                                             "down — control released to the crew");

                case ReturnStep.Complete:
                    return ReturnDecision.Stay(ReturnStep.Complete, "return complete");

                default:
                    return ReturnDecision.Stay(ReturnStep.Idle, "unknown return step");
            }
        }

        /// <summary>
        /// Where the return picks up when the crew engage AUTO SEQUENCE part-way through one — the same
        /// rule `CrewProcedureOps.Engage` and `AscentSequence.ResumeFrom` apply at their own levels.
        /// ⛔ It can never return a step that FIRES A DECOUPLER on its first tick: the trunk and the
        /// deorbit burn are entered only by walking to them, never by resuming into them.
        /// </summary>
        public static ReturnStep ResumeFrom(MissionPhase phase, bool docked, bool trunkAttached)
        {
            if (docked) return ReturnStep.Idle;
            switch (phase)
            {
                case MissionPhase.Drogues:
                case MissionPhase.Mains:      return ReturnStep.EntryAttitude;   // the chute gates ahead
                case MissionPhase.Entry:      return ReturnStep.DeorbitPlan;
                case MissionPhase.Splashdown:
                case MissionPhase.Landed:     return ReturnStep.Complete;
                default:                      return trunkAttached ? ReturnStep.Backout
                                                                   : ReturnStep.Departed;
            }
        }

        /// <summary>Has the DEPARTURE leg finished, so the plan may raise the G15 deorbit poll?</summary>
        public static bool DepartureComplete(ReturnStep step)
        {
            return step == ReturnStep.Departed || Beyond(step);
        }

        /// <summary>Has the whole return finished, so the plan may end?</summary>
        public static bool ReturnComplete(ReturnStep step)
        {
            return step == ReturnStep.Complete;
        }

        /// <summary>Is this step past the departure — i.e. on the deorbit/entry/descent side?</summary>
        public static bool Beyond(ReturnStep step)
        {
            return step == ReturnStep.DeorbitPlan || step == ReturnStep.DeorbitBurn
                || step == ReturnStep.NoseCone || step == ReturnStep.EntryAttitude
                || step == ReturnStep.Drogues || step == ReturnStep.Mains
                || step == ReturnStep.Splashdown || step == ReturnStep.Complete;
        }

        /// <summary>
        /// Which step the ENTRY leg starts at, once the crew have cleared G15. ⛔ A separate entry point
        /// from `Step`, because the deorbit is the one thing on the return the crew explicitly authorise
        /// and nothing may walk into it by itself.
        /// </summary>
        public static ReturnStep BeginDeorbit(ReturnStep step)
        {
            return Beyond(step) ? step : ReturnStep.DeorbitPlan;
        }
    }
}
