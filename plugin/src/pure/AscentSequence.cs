// DragonScreen — AscentSequence  (PURE: the ascent EVENT chain the conductor actuates directly)
// ============================================================================================
// Register T18, §B8 / §B12.7 / §B11. This is the second half of "wire Ascent (PVG)". The first half is
// MechJeb's: PVG steers and throttles. This half is OURS, and it exists because of one owner directive.
//
// ---- ⛔ WHY THIS FILE HAS TO EXIST AT ALL — §B8's AUTOSTAGE RULE ----
// §B8, owner directive 2026-09-03 via the overseer, verbatim in the plan:
//     "PVG KEEPS ITS FULL STAGE MODEL … `MechJebModuleStagingController.Autostage` = FALSE. MechJeb
//      never actuates a separation or an ignition. The conductor performs every separation and every
//      ignition DIRECTLY, at the times PVG predicts, through the named-part path of §B12.7
//      (`ModuleDecouple.Decouple()`, `ModuleEngines.Activate()` per engine) — never staging, never an
//      action group. A T18 chat must not read the old sentence and switch autostage back on."
// So MechJeb flies the ARC and the conductor works the STAGES. This file decides WHEN each of those
// stage events happens; `src/MechConductor.cs` performs them through the recovered `src/Actuator.cs`.
//
// ⛔ AND BOTH PROFILES SHIP AUTOSTAGE ON, WHICH IS WHY T18 MUST TURN IT OFF EXPLICITLY. Checked, not
// assumed, in the pinned tree and in the shipped cfg:
//   • RO's own defaults set it ON — `MechJebModuleAscentSettings.ApplyRODefaults()` contains the line
//     `Autostage = true;` (plugin/mech/MechJeb2/MechJebModuleAscentSettings.cs), and that method runs
//     from that module's `OnStart` whenever `ForceResetROSettings && IsLoadedRealismOverhaul`.
//   • The shipped tune sets it ON too — `_autostage = True` in the `MechJebModuleAscentSettings` node
//     of `plugin/GameData/DragonScreen/PluginData/mechjeb_settings_type_Crew-Dragon.cfg`.
// ⇒ turning autostage OFF is NOT a tune toward either profile; it is an owner directive that overrides
// both of them, and it is the ONE ascent-shaping value T18 writes. See the profile note below.
//
// ---- ⭐ WHICH PROFILE T18 BUILDS AGAINST (register S195 — answered here, not decided here) ----
// **T18 builds against WHATEVER PROFILE THE CORE HAS LOADED, and writes no ascent-shaping knob.**
// The mechanism, read out of the pinned tree rather than reported:
//   `DragonMechJebCore.OnStart` → `base.OnStart(state)` runs every module's `OnStart`, which is where
//   `ApplyRODefaults()` lands the RSS-RO baseline → then our own `ApplyTune()` lays the TUNED Crew-2
//   cfg on top of it. So today the stack is **RO defaults, then the tuning TARGET over them**, which is
//   exactly the divergence S195 logged. §B5's two-profile split says flight 1 should fly the RO
//   defaults and that the Crew-2 numbers are a §B11 TARGET.
// ⛔ T18 DOES NOT RESOLVE IT AND MUST NOT. Which profile flight 1 flies is a §B5/T22 plan question, the
// seam is `DragonMechJebCore.tuneFile` (blank = load nothing), `docs/BUILD_PLAN.md` is a guarded file
// (C1.12 / G10), and silently tuning toward the target would make the first recorded flight's data
// meaningless. T18 therefore writes exactly three things into MechJeb, each with its own authority and
// none of them a tune (see `src/MechConductor.cs`, which performs them):
//   1. `Autostage = false`     — §B8 owner directive, above. Overrides both profiles.
//   2. `AscentType = PSG`      — §B8 "Target PVG(1)". Already the RO default (`ApplyRODefaults` ends
//                                with `AscentType = AscentType.PSG`); asserted because a persisted
//                                craft or type value could still be CLASSIC, which §B8 rules out.
//   3. the TARGET ORBIT        — §B5's own named exception: "Target-orbit values are the one exception
//                                … they are MISSION FACTS … These load correctly from flight 1
//                                regardless of which profile is active." Taken from the resolved
//                                `MissionProfile`, which is mission-as-data.
// Everything else — Pitch Rate, Pitch Start Velocity, LimitQa, MaxAoA, the attitude PID — is left
// exactly where the loaded profile put it. **This file contains no ascent-shaping number of any kind.**
//
// ---- WHERE THE INTERVALS COME FROM (§1.4: verified-real, in-repo, cited line by line) ----
// `docs/CREW_MISSION_TELEMETRY.md` §2's ISS-crew event table, tagged [P] (published), with two
// verified wall-clock→MET anchor missions beside it:
//     | Stage sep | ~2:38–2:39 | pneumatic pushers, **~3 s after MECO** |
//     | SES-1     | ~2:44–2:47 | MVac ignition, **~8 s after sep**      |
//     | Dragon sep| ~11:57–12:00 | from S2 …                            |
//   and §5's callout MET table: MECO 0:02:37 · sep 0:02:40 (**MECO + 3 s**) · SES-1 0:02:48
//   (**sep + 8 s**) · SECO-1 0:08:50 · Dragon sep 0:12:03 · nosecone open 0:12:48.
//   Dragon sep − SECO-1 = 193 s from §5; the anchors give 191 s (Crew-2) and 190 s (Crew-6).
//   Nosecone − Dragon sep = 45 s from §5.
//
// ⛔ AND THESE ARE INTERVALS AFTER A MEASURED EVENT, NEVER A STOPWATCH FROM T0 — the distinction
// `pure/BarEvent.cs` insists on: "⛔ A `MECO` fired off a stopwatch would be the single worst thing
// this bar could do — a crew reading a real callout that nothing measured … Do not 'temporarily' wire
// one to MET." Nothing here reads MET. MECO is triggered by MEASURED propellant depletion or a
// MEASURED flameout; every later step is a delay after the step before it, which is what the real
// vehicle's pneumatic pushers and MVac start actually are.
//
// ---- NO INVENTED THRESHOLD LIVES HERE (§1.4 / C1.15) ----
// Every number below is either a documented interval cited above, a floating-point epsilon that is
// stated as one, or a DISABLED-BY-DEFAULT sentinel. The two engineering knobs both ship inert:
//   `MecoPropellantFrac` ships **0.0** = "no early cutoff, run the stage to depletion" — the honest
//   un-tuned baseline, not a chosen value. T22 raises it if the real profile wants an early MECO.
//   `SecoBackstopPeriapsisM` ships **disabled** — see the SECO note in `Step`.
// Both arrive on `AscentInputs` from the caller, the same shape `pure/Conductor.cs` uses for §B12.4's
// tolerances, so a T22 tune moves a number and never touches this rule.
//
// PURE: no Unity, no KSP, no MechJeb reference. `Step` is a function of its inputs and holds no clock —
// the caller measures the time in the current step and passes it in.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>Where the ascent has got to. One step at a time, advanced only by measured state.</summary>
    public enum AscentStep : byte
    {
        /// <summary>Nothing commanded. The pad, before the crew's launch GO.</summary>
        Idle = 0,
        /// <summary>Octaweb commanded alight; the hold-downs still hold. `IgnitionGate` owns this step.</summary>
        Ignition,
        /// <summary>Clamps released, first stage flying. PVG steers.</summary>
        Liftoff,
        /// <summary>Octaweb commanded shut; waiting for its thrust to actually die before separating.</summary>
        Meco,
        /// <summary>Interstage fired, booster falling away; waiting the documented cold-stage lead.</summary>
        Separation,
        /// <summary>MVac ignition commanded; waiting for a light (RealFuels may need several tries).</summary>
        Ses1,
        /// <summary>Second stage burning. PVG flies the vacuum arc to insertion.</summary>
        S2Flight,
        /// <summary>PVG has finished. Coasting the documented interval before Dragon separation.</summary>
        Seco1,
        /// <summary>Dragon separated from S2; waiting the documented interval before the nose cone.</summary>
        DragonSep,
        /// <summary>Nose cone commanded open.</summary>
        NoseCone,
        /// <summary>Ascent done — the conductor may advance the mission plan.</summary>
        Complete,
        /// <summary>⛔ The pad ignition did not make thrust. Engines safed, clamps NEVER released.</summary>
        Safed
    }

    /// <summary>What the glue must actuate this tick. One per tick; `None` most ticks.</summary>
    public enum AscentAct : byte
    {
        None = 0,
        /// <summary>`Actuator.IgniteOctawebLiftoff` — the all-engines octaweb mode only.</summary>
        IgniteStageOne,
        /// <summary>`Actuator.ReleaseHoldDowns` — the one irreversible pad action.</summary>
        ReleaseHoldDowns,
        /// <summary>`Actuator.ShutdownBoosterEngines` — MECO.</summary>
        ShutdownStageOne,
        /// <summary>`Actuator.SeparateBooster` — the interstage decoupler, by role.</summary>
        SeparateBooster,
        /// <summary>`Actuator.IgniteSecondStage` — SES-1. Re-commanded each tick until it lights.</summary>
        IgniteStageTwo,
        /// <summary>`Actuator.SeparateDragon` — the Dragon↔S2 decoupler. ⛔ NOT the trunk.</summary>
        SeparateDragon,
        /// <summary>`Actuator.OpenNoseShroud` — the hinged nose cone (Dragon has no fairing).</summary>
        OpenNoseCone,
        /// <summary>`Actuator.ShutdownBoosterEngines` on a failed pad light. Clamps stay held.</summary>
        SafeAbort
    }

    /// <summary>The measured vehicle state one ascent decision is made from, plus the caller's numbers.</summary>
    public struct AscentInputs
    {
        // ── intent ──────────────────────────────────────────────────────────────────────
        /// <summary>The crew's LAUNCH GO (gate G7) has been given. Latched by the caller.</summary>
        public bool LaunchCommanded;

        // ── stage one, measured ─────────────────────────────────────────────────────────
        public double S1ThrustN;      // summed live thrust of the octaweb
        public double S1MaxThrustN;   // its available thrust at this altitude
        public int    S1LitCount;     // engine modules reporting ignited
        /// <summary>Usable propellant remaining in the booster stage, 0..1. Measured, not modelled.</summary>
        public double S1PropellantFrac;
        /// <summary>Every octaweb engine reports `flameout`. A KSP boolean, not a threshold.</summary>
        public bool   S1Flameout;

        // ── stage two, measured ─────────────────────────────────────────────────────────
        public double S2ThrustN;
        public int    S2LitCount;

        // ── guidance / orbit, measured ──────────────────────────────────────────────────
        /// <summary>PVG reports `PSGStatus.FINISHED` — the insertion is flown. THE SECO signal.</summary>
        public bool   PvgFinished;
        public bool   OrbitClosed;
        public double PeriapsisM;

        // ── time in the CURRENT step, measured by the caller ────────────────────────────
        public double SinceStepS;

        // ── the caller's numbers (§1.4 / C1.15 — the rule is ours, the values are the caller's) ──
        /// <summary>MECO when the stage-1 usable fraction falls to this. Ships 0.0 = run to depletion.</summary>
        public double MecoPropellantFrac;
        /// <summary>Thrust at or below this newtons counts as dead. An epsilon on a meganewton stage.</summary>
        public double DeadThrustN;
        /// <summary>MECO → separation. Documented 3 s (`CREW_MISSION_TELEMETRY.md` §2/§5).</summary>
        public double MecoToSepS;
        /// <summary>Separation → SES-1. Documented 8 s (same source).</summary>
        public double SepToSes1S;
        /// <summary>SECO-1 → Dragon separation. Documented ~190 s (same source).</summary>
        public double Seco1ToDragonSepS;
        /// <summary>Dragon separation → nose-cone open. Documented 45 s (same source).</summary>
        public double DragonSepToNoseS;
        // ⛔ NO PAD-HOLD TIMEOUT FIELD HERE, DELIBERATELY. `IgnitionGate.Evaluate` reads its own
        // `MaxHoldS` constant (2.0 s) and takes no caller override; adding an unread field beside it
        // would advertise a knob that governs nothing. If T22 ever needs that timeout tunable, it moves
        // on `IgnitionGate` where the rule lives, not here.
        /// <summary>
        /// SECO backstop periapsis. **SHIPS DISABLED** (`double.MaxValue`) — see the note in `Step`:
        /// a premature SECO is worse than a hold, so PVG's own FINISHED is the only signal by default.
        /// </summary>
        public double SecoBackstopPeriapsisM;

        /// <summary>
        /// The documented, in-repo values — everything the caller does not have a measured source for.
        /// ⚠ These are the REAL VEHICLE's intervals (§1.4 tier-1), not a tune; T22 converges only the
        /// two that ship inert. Every value here is cited line by line in this file's header.
        /// </summary>
        public static AscentInputs Nominal()
        {
            AscentInputs s = new AscentInputs();
            s.MecoPropellantFrac     = MecoPropellantFracDefault;
            s.DeadThrustN            = DeadThrustNewtons;
            s.MecoToSepS             = MecoToSepSeconds;
            s.SepToSes1S             = SepToSes1Seconds;
            s.Seco1ToDragonSepS      = Seco1ToDragonSepSeconds;
            s.DragonSepToNoseS       = DragonSepToNoseSeconds;
            s.SecoBackstopPeriapsisM = SecoBackstopDisabled;
            return s;
        }

        // ── the documented constants, each with its source ──────────────────────────────
        /// <summary>`CREW_MISSION_TELEMETRY.md` §2 "pneumatic pushers, ~3 s after MECO"; §5 gives MECO
        /// 0:02:37 → sep 0:02:40.</summary>
        public const double MecoToSepSeconds = 3.0;
        /// <summary>`CREW_MISSION_TELEMETRY.md` §2 "MVac ignition, ~8 s after sep"; §5 gives sep 0:02:40
        /// → SES-1 0:02:48.</summary>
        public const double SepToSes1Seconds = 8.0;
        /// <summary>`CREW_MISSION_TELEMETRY.md` §5: SECO-1 0:08:50 → Dragon sep 0:12:03 = 193 s. The two
        /// verified anchors give 191 s (Crew-2) and 190 s (Crew-6); 190 is the low, safe end of the
        /// published spread.</summary>
        public const double Seco1ToDragonSepSeconds = 190.0;
        /// <summary>`CREW_MISSION_TELEMETRY.md` §5: Dragon sep 0:12:03 → nosecone open 0:12:48 = 45 s.</summary>
        public const double DragonSepToNoseSeconds = 45.0;
        /// <summary>⚠ NOT A TUNING VALUE. One newton on a stage that makes seven meganewtons: a
        /// floating-point epsilon for "the thrust has gone", stated as one rather than hidden.</summary>
        public const double DeadThrustNewtons = 1.0;
        /// <summary>⚠ SHIPS INERT. 0.0 = "no early cutoff, run the stage to depletion" — the honest
        /// un-tuned baseline. T22 raises it if the flown profile wants MECO before the tanks are dry.</summary>
        public const double MecoPropellantFracDefault = 0.0;
        /// <summary>⚠ SHIPS DISABLED. See the SECO note in <see cref="AscentSequence.Step"/>.</summary>
        public const double SecoBackstopDisabled = double.MaxValue;
    }

    /// <summary>One ascent decision: where to go next, and what (if anything) to actuate to get there.</summary>
    public struct AscentDecision
    {
        public AscentStep Next;
        public AscentAct  Act;
        /// <summary>WHY, in words — the same discipline `ConductorAction.Reason` carries. §0's three
        /// misdiagnoses were all "the vehicle did something and nothing said why".</summary>
        public string     Reason;

        public static AscentDecision Of(AscentStep next, AscentAct act, string why)
        { AscentDecision d; d.Next = next; d.Act = act; d.Reason = why; return d; }

        /// <summary>Stay where you are and do nothing. ⛔ Only ever called with the step the
        /// caller is already in — a transition uses <see cref="Of"/>, so a reader can tell the two
        /// apart at the call site.</summary>
        public static AscentDecision Stay(AscentStep here, string why)
        { return Of(here, AscentAct.None, why); }

        public override string ToString()
        {
            return Next.ToString() + (Act != AscentAct.None ? " / " + Act : "")
                 + (string.IsNullOrEmpty(Reason) ? "" : " — " + Reason);
        }
    }

    // ============================================================================================
    //  THE TARGET ORBIT — §B5's ONE NAMED EXCEPTION TO THE TWO-PROFILE SPLIT
    // ============================================================================================
    //  §B5, verbatim: "**Target-orbit values are the one exception — and here is why.**
    //  `DesiredInclination`/`DesiredOrbitAltitude` etc. are not ascent-shaping knobs at all — they are
    //  **MISSION FACTS** (the real Crew-2 orbit, ~210 km × 51.63°, §8). A destination is not something
    //  you tune toward; it is data you already have. These load correctly from flight 1 regardless of
    //  which profile (RO-default or Crew-2-tuned) is active."
    //
    //  ⚠ AND UNDER RO DEFAULTS THEY DO **NOT** LOAD CORRECTLY, WHICH IS WHY THIS RESOLVER EXISTS.
    //  Read out of the pinned tree: `MechJebModuleAscentSettings.ApplyRODefaults()` sets
    //  `DesiredOrbitAltitude.Val = 145000` and says nothing at all about inclination — whose field
    //  default is `new EditableDouble(0.0)`. So a core running RO defaults alone would fly a 145 km
    //  EQUATORIAL ascent from a 28.6° pad. The conductor therefore takes the destination from the
    //  resolved `MissionProfile` (mission-as-data, W4) rather than from whichever cfg happens to be on.
    //
    //  ⛔ THE SIGN OF THE INCLINATION IS **NOT** DECIDED HERE — IT IS PRESERVED. `MechJebModuleAscent
    //  PSGAutopilot:108-116` passes `DesiredInclination` straight into `Glueball.SetTarget` sign and all,
    //  and MechJeb's own menu takes `Math.Abs()` of it before checking it against the launch latitude, so
    //  the sign carries a launch-azimuth meaning that this repo has no source for. The shipped Crew-2
    //  profile carries **−51.6316**; `Missions.Catalog` carries **+51.6**. Rather than pick one, the
    //  resolver keeps the MAGNITUDE from the mission and the SIGN from whatever is already loaded, and a
    //  zero/unset load defaults to positive. ⚠ **That default is the one thing here with no in-repo
    //  source, and it is raised as an owner question on T18's register line rather than settled.**
    // ============================================================================================

    /// <summary>Where the ascent is going. A destination, not a tune (§B5's exception, above).</summary>
    public struct AscentTarget
    {
        /// <summary>Insertion periapsis, metres ASL. PVG reads this as `DesiredOrbitAltitude`.</summary>
        public double PeriapsisM;
        /// <summary>Insertion apoapsis, metres ASL. PVG reads this as `DesiredApoapsis`.</summary>
        public double ApoapsisM;
        /// <summary>Target inclination, degrees, SIGN PRESERVED from whatever the core had loaded.</summary>
        public double InclinationDeg;
        /// <summary>True when the mission named its own apsides; false = the standard ISS insertion.</summary>
        public bool FromProfileApsides;
    }

    /// <summary>Turns a <see cref="MissionProfile"/> into the orbit PVG is asked for.</summary>
    public static class AscentTargets
    {
        /// <summary>
        /// The standard ISS-crew insertion altitude, used when a profile carries no apsides of its own
        /// (every `Iss(...)` row in `Missions.Catalog` ships `PeriKm = ApoKm = 0`, whose comment reads
        /// "0/0 = the standard ~200 km circular ISS insertion").
        /// ⭐ SOURCE, not a choice: §B11 "Insertion orbit **[DOC/cfg]**: **~190–210 km × 51.63°**
        /// (Crew-2 = 210/-51.6316)", and the shipped `mechjeb_settings_type_Crew-Dragon.cfg` carries
        /// `DesiredOrbitAltitude = 210000`. Taking the cfg's own value keeps the two in agreement.
        /// </summary>
        public const double IssInsertionAltitudeM = 210000.0;

        /// <summary>
        /// The destination for one mission. <paramref name="loadedInclinationDeg"/> is what the core
        /// currently holds — its SIGN is kept, its magnitude is replaced (see the block above).
        /// </summary>
        public static AscentTarget For(MissionProfile m, double loadedInclinationDeg)
        {
            AscentTarget t;

            // A profile that names its own orbit gets it. Only the three free-flyers do
            // (Inspiration4 575×585, Polaris Dawn 190×1400, Fram2 202×413).
            bool named = m.PeriKm > 0.0 || m.ApoKm > 0.0;
            t.FromProfileApsides = named;
            if (named)
            {
                double pe = m.PeriKm > 0.0 ? m.PeriKm : m.ApoKm;
                double ap = m.ApoKm > 0.0 ? m.ApoKm : m.PeriKm;
                // A profile that names them the wrong way round is corrected rather than flown.
                t.PeriapsisM = 1000.0 * (pe <= ap ? pe : ap);
                t.ApoapsisM  = 1000.0 * (pe <= ap ? ap : pe);
            }
            else
            {
                t.PeriapsisM = IssInsertionAltitudeM;
                t.ApoapsisM  = IssInsertionAltitudeM;
            }

            double mag = m.IncDeg < 0.0 ? -m.IncDeg : m.IncDeg;
            t.InclinationDeg = loadedInclinationDeg < 0.0 ? -mag : mag;
            return t;
        }
    }

    /// <summary>The pure ascent event chain. One entry point, no state, no clock.</summary>
    public static class AscentSequence
    {
        /// <summary>
        /// Advance the ascent one tick.
        ///
        /// ⛔ EVERY TRANSITION IS DRIVEN BY MEASURED STATE OR BY A DELAY AFTER A MEASURED EVENT.
        /// None of them reads MET, and none of them may be changed to (`pure/BarEvent.cs`'s standing
        /// rule about a stopwatch-fired MECO).
        /// </summary>
        public static AscentDecision Step(AscentInputs s, AscentStep here)
        {
            switch (here)
            {
                // ── the pad ────────────────────────────────────────────────────────────
                case AscentStep.Idle:
                    if (!s.LaunchCommanded)
                        return AscentDecision.Stay(AscentStep.Idle, "no launch GO — the pad is quiet");
                    return AscentDecision.Of(AscentStep.Ignition, AscentAct.IgniteStageOne,
                                             "launch GO — octaweb ignition (all-engines mode only)");

                // ⛔ THE CLAMP DECISION IS NOT RE-DERIVED HERE. `pure/IgnitionGate.cs` already owns it,
                // it is flight-recovered (W5/W34), it is separately tested, and the owner has ruled on
                // it (W34, 2026-09-05). Release only at ≥99% of available thrust; a stage that has not
                // made thrust inside `MaxHoldS` is SAFED with the clamps still holding, because a
                // half-lit Falcon released from the hold-downs is the one pad failure that cannot be
                // taken back.
                case AscentStep.Ignition:
                    {
                        ClampAction c = IgnitionGate.Evaluate(s.S1ThrustN, s.S1MaxThrustN,
                                                              s.S1LitCount, s.SinceStepS);
                        if (c == ClampAction.Release)
                            return AscentDecision.Of(AscentStep.Liftoff, AscentAct.ReleaseHoldDowns,
                                                     "thrust good — hold-downs released");
                        if (c == ClampAction.SafeAbort)
                            return AscentDecision.Of(AscentStep.Safed, AscentAct.SafeAbort,
                                                     "ignition did not make thrust — engines safed, "
                                                     + "CLAMPS STILL HELD");
                        return AscentDecision.Stay(AscentStep.Ignition, "thrust building on the clamps");
                    }

                // ── first stage ────────────────────────────────────────────────────────
                // MECO is MEASURED: the tanks are empty, or every engine reports flameout. There is no
                // timer here and there must never be one.
                case AscentStep.Liftoff:
                    if (s.S1Flameout)
                        return AscentDecision.Of(AscentStep.Meco, AscentAct.ShutdownStageOne,
                                                 "MECO — octaweb flameout");
                    if (s.S1PropellantFrac <= s.MecoPropellantFrac)
                        return AscentDecision.Of(AscentStep.Meco, AscentAct.ShutdownStageOne,
                                                 "MECO — first-stage propellant spent");
                    return AscentDecision.Stay(AscentStep.Liftoff, "first stage flying — PVG steering");

                // ⛔ TWO CONDITIONS, AND THE FIRST ONE IS A FLIGHT LESSON, NOT CAUTION.
                // `src/Actuator.cs:SeparateBooster` carries it in its own header: "MECO is now a TWO-STEP
                // sequence — shut the octaweb, WAIT for its thrust to actually die, THEN SeparateBooster —
                // because decoupling a still-thrusting booster made it ram the S2 and push it off course
                // (flight 194334)". The documented 3 s is the SECOND condition, not a substitute for it.
                case AscentStep.Meco:
                    {
                        bool dead = s.S1LitCount == 0 && s.S1ThrustN <= s.DeadThrustN;
                        if (!dead)
                            return AscentDecision.Stay(AscentStep.Meco,
                                                       "waiting for octaweb thrust to die before sep");
                        if (s.SinceStepS < s.MecoToSepS)
                            return AscentDecision.Stay(AscentStep.Meco, "thrust dead — holding the sep lead");
                        return AscentDecision.Of(AscentStep.Separation, AscentAct.SeparateBooster,
                                                 "stage separation — interstage decoupler");
                    }

                // §B8: "real F9 does a COLD stage sep (so the conductor separates, then ignites, with its
                // own lead time — it does not hot-stage)". The lead is the documented sep→SES-1 gap, and
                // it is also the window RO's own `AutoRCSUllaging` uses to settle the MVac's propellant.
                case AscentStep.Separation:
                    if (s.SinceStepS < s.SepToSes1S)
                        return AscentDecision.Stay(AscentStep.Separation,
                                                   "cold-stage coast — booster clearing");
                    return AscentDecision.Of(AscentStep.Ses1, AscentAct.IgniteStageTwo,
                                             "SES-1 — MVac ignition");

                // ⭐ THE IGNITION COMMAND REPEATS UNTIL IT TAKES, and that is the documented behaviour of
                // the actuator it drives: `Actuator.IgniteSecondStage`'s own comment says "the RealFuels
                // settle→light→retry cycle calls this every tick". A single Activate() on a pressure-fed
                // engine whose propellant has not settled is a light that silently does not happen.
                case AscentStep.Ses1:
                    if (s.S2LitCount > 0 && s.S2ThrustN > s.DeadThrustN)
                        return AscentDecision.Of(AscentStep.S2Flight, AscentAct.None, "MVac lit");
                    return AscentDecision.Of(AscentStep.Ses1, AscentAct.IgniteStageTwo,
                                             "MVac has not lit — re-commanding ignition");

                // ── second stage ───────────────────────────────────────────────────────
                // ⛔ SECO IS PVG'S CALL, AND THE BACKSTOP SHIPS DISABLED ON PURPOSE. PVG reporting
                // FINISHED is the only signal that means "the insertion this vehicle was asked for has
                // been flown". A periapsis backstop fires EARLY — during the S2 burn the periapsis rises
                // through the atmosphere long before the target orbit is reached — and an early SECO
                // strands the vehicle short of orbit, which is strictly worse than an ascent that holds
                // where the crew can see it holding. `SecoBackstopPeriapsisM` therefore ships
                // `double.MaxValue`; T22 may enable it once a flown profile says where it belongs.
                case AscentStep.S2Flight:
                    if (s.PvgFinished)
                        return AscentDecision.Of(AscentStep.Seco1, AscentAct.None,
                                                 "SECO-1 — PVG reports the insertion flown");
                    if (s.OrbitClosed && s.PeriapsisM >= s.SecoBackstopPeriapsisM)
                        return AscentDecision.Of(AscentStep.Seco1, AscentAct.None,
                                                 "SECO-1 — periapsis backstop (PVG did not report)");
                    return AscentDecision.Stay(AscentStep.S2Flight, "second stage burning to insertion");

                case AscentStep.Seco1:
                    if (s.SinceStepS < s.Seco1ToDragonSepS)
                        return AscentDecision.Stay(AscentStep.Seco1, "post-insertion coast before Dragon sep");
                    return AscentDecision.Of(AscentStep.DragonSep, AscentAct.SeparateDragon,
                                             "Dragon separation from the second stage");

                case AscentStep.DragonSep:
                    if (s.SinceStepS < s.DragonSepToNoseS)
                        return AscentDecision.Stay(AscentStep.DragonSep, "backing away from S2 before the nose cone");
                    return AscentDecision.Of(AscentStep.NoseCone, AscentAct.OpenNoseCone,
                                             "nose cone open");

                case AscentStep.NoseCone:
                    return AscentDecision.Of(AscentStep.Complete, AscentAct.None,
                                             "ascent complete — insertion orbit achieved");

                // ── terminal ───────────────────────────────────────────────────────────
                case AscentStep.Complete:
                    return AscentDecision.Stay(AscentStep.Complete, "ascent complete");

                // ⛔ ABSORBING. A safed pad does not retry itself: the crew disengage, work the problem
                // and re-engage. Nothing here may release a clamp.
                case AscentStep.Safed:
                    return AscentDecision.Stay(AscentStep.Safed, "SAFED on the pad — crew action required");

                default:
                    return AscentDecision.Stay(AscentStep.Idle, "unknown ascent step");
            }
        }

        /// <summary>
        /// Has the ascent reached a step from which the mission plan may advance? Only `Complete`.
        /// ⛔ `Safed` is NOT complete — the plan must not walk past a pad that failed to light.
        /// </summary>
        public static bool CanAdvancePlan(AscentStep step) { return step == AscentStep.Complete; }

        /// <summary>
        /// ⭐ WHERE TO PICK THE ASCENT UP WHEN AUTO SEQUENCE IS ENGAGED PART-WAY THROUGH ONE.
        ///
        /// `CrewProcedureOps.Engage` already does this for the mission PLAN — its own comment reads
        /// *"AUTO SEQUENCE must KNOW WHERE IT IS and what to do next — pressing it in orbit must NOT
        /// restart the launch"* — and this is the same rule one level down, for the stage chain. Without
        /// it a conductor engaged after liftoff would sit at `Idle` waiting for a launch GO that has
        /// already been given, and the vehicle would reach depletion with nobody to command MECO.
        ///
        /// ⛔ IT CANNOT PRODUCE `Ignition`. Re-engaging never re-lights an octaweb and never touches a
        /// clamp: the only route into `Ignition` is a fresh launch GO from `Idle`, on the pad.
        /// </summary>
        /// <param name="onPad">Landed, splashed or PRELAUNCH — the vehicle has not flown yet.</param>
        /// <param name="boosterAttached">A first-stage part is still on this vessel.</param>
        /// <param name="secondStageAttached">A second-stage engine is still on this vessel.</param>
        /// <param name="secondStageLit">That engine is burning.</param>
        public static AscentStep ResumeFrom(bool onPad, bool boosterAttached,
                                            bool secondStageAttached, bool secondStageLit)
        {
            // Still bolted down: the ordinary case, and the only one that may ever ignite.
            if (onPad) return AscentStep.Idle;

            // No second stage left to burn — the Dragon is on its own. Whatever happened, the ascent is
            // over; the nose cone is the crew's or a later increment's, never a resume's to command.
            if (!secondStageAttached) return AscentStep.Complete;

            // The booster is gone but the stack is not: we are on the second stage.
            if (!boosterAttached) return secondStageLit ? AscentStep.S2Flight : AscentStep.Ses1;

            // Booster still attached and we are off the pad: first-stage flight, MECO ahead.
            return AscentStep.Liftoff;
        }

        /// <summary>
        /// Is the first stage still attached and being flown? The glue uses this to know whether the
        /// booster-recovery hand-off (§B16) is still ahead of it.
        /// </summary>
        public static bool BoosterAttached(AscentStep step)
        {
            return step == AscentStep.Idle || step == AscentStep.Ignition || step == AscentStep.Liftoff
                || step == AscentStep.Meco || step == AscentStep.Safed;
        }
    }
}
