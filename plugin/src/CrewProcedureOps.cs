// DragonScreen — CrewProcedureOps  (KSP glue: the crew-in-the-loop mission conductor)
// ============================================================================================
// The real conductor, replacing the demolition stub. It drives the PURE, headless-tested L4 pieces —
// ModeManager (the mission plan), CrewGate (the gate state machine), CrewGates (the catalog) — against
// the LIVE vessel: it resolves the mission from the VAB craft name, walks the plan, satisfies each gate's
// AUTO items from vessel state (the crew taps the CrewAck items), and holds until the crew's GO. The
// SCREENS read this surface (VesselData → GateCard) and route the crew's taps here (ScreenPainter).
// `FlightDriver.FixedUpdate` advances it each physics frame — and it is the ONLY thing that may.
//
// ⛔ GLUE DISCIPLINE: no guidance math here — the decisions are the pure machine's; this only feeds it
// the vessel. Defensive throughout (the glue is where bugs live).
//
// ---- RESTORED BY W10, 2026-09-05, from `8b81816^` (20,016 B, R1 §5.2 RECOVER-CODE, "the crew-in-the-loop
// ---- mission conductor"), TOGETHER WITH ITS HOST — `src/FlightDriver.cs`, whose `:341` was the only
// ---- caller of `Tick` in the whole pre-deletion tree. Landing this file without that host was the
// ---- failure the register line existed to prevent: a lit AUTO SEQUENCE button over a conductor that
// ---- can never tick, and a crew GO latched for nobody — strictly worse than §14.4(a)'s honest no-op.
//
// ⛔ GEN-1 WAS CHECKED AND REJECTED (W1's mechanical rule). A gen-1 `CrewProcedureOps.cs` exists at
// `0d6423d` (15,868 B). Comment-stripped, the two share 77 of ~272 code lines (28%), and every one of
// those is trivial (`using UnityEngine;`, `namespace DragonScreen`, braces, `Vessel v =
// FlightGlobals.ActiveVessel;`). They are different implementations: gen 1 walks a `CrewProcedureCore` +
// `WpPhase` layer that is NOT in this tree and has public `Engage()`/`Disengage(string)`/`Tick()`; gen 2
// walks `MissionProfile`/`MissionStep[]`/`GateId` — exactly the pure layer W4 restored — and its tick is
// `Tick(Vessel)`. GEN 2 TAKEN WHOLE; nothing of gen 1 was used.
//
// ---- WHAT W10 CHANGED FROM `8b81816^`, AND WHY. Four things, all of them §14.4(a), none of them silent:
//  1. `AutoAdvanceGates` SHIPS FALSE (was `true`). ⭐ A BEHAVIOUR CHANGE, recorded on the register line
//     rather than made in a quiet edit. `true` synthesises the crew's taps AND the GO press, which makes
//     every interactive gate DECORATIVE — the opposite of the operating concept `pure/ModeManager.cs`
//     states in its own header ("autonomous between gates, authorised by the crew at each real decision
//     point"). The owner labelled the flag "⛔ TEMPORARY (user 2026-08-27)" in this very file and the
//     file's own comment said "Restore the interactive gates by setting AutoAdvanceGates = false";
//     honouring that label is not overriding him. If a hands-off test mode is wanted again it comes back
//     as an EXPLICIT NAMED OPTION, never as a default (logged as a stray, C1.1 — not built here).
//  2. `ActivePhase` reports a Fly phase ONLY when the host actually has a controller for it
//     (`FlightDriver.HasControllerFor`). This increment the host has NONE, so it reports `Unknown` and
//     rule T4's resolver (`Mission.AuthoritativePhase`) falls through to the live classifier — its own
//     designed fallback. Without this the conductor would park on the Ascent Fly step that nothing
//     completes and the glass would read ASCENT over a vehicle bolted to the pad.
//  3. `PhaseName` says HOLD at such a step, for the same reason: the AUTO SEQUENCE button renders
//     "AUTO  " + this string (`pure/Pages.cs:810`), and "AUTO  Ascent to orbit" on the pad is the same lie.
//  4. `PressAbort()` is an HONEST NO-OP. The gate card's ABORT hands to the abort responder — register
//     W19 (`AbortControl.cs`), which is not in the tree. Routing the press into the gate machine would
//     latch `GatePhase.Abort`, and `pure/GateCard.cs:195` paints that in `DragonPalette.Alarm`: a RED
//     ABORT card for an abort that cannot happen. §14.4(a) is explicit — click, no light, no action, and
//     NO RED. It was a no-op in the stub and it stays one; W19 makes it live.
// Everything else is `8b81816^` verbatim. GO / NO-GO / the item taps ARE live — they are crew PROCEDURE,
// not flight actuation, and nothing below commands the vehicle in any way.
// ============================================================================================
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class CrewProcedureOps
    {
        static bool engaged;
        static MissionProfile mission;
        static MissionStep[] plan;
        static int index;
        static GatePhase phase = GatePhase.Holding;
        static bool[] satisfied;
        static Gate gate;
        static uint boundVesselId;

        // ⭐ W10, 2026-09-05: SHIPS FALSE — see change (1) in the header. `true` auto-taps the crew-ack items
        // and synthesises the GO press, so every gate clears itself and the whole countdown runs away with no
        // crew in the loop. The AUTO (vessel-state) checks still hold a genuinely bad pad either way; what
        // `true` removes is the CREW. Left as a field, not a constant, so the register's decision is visible
        // here rather than compiled away — but it is not a default anything may flip back (C1.8).
        public static bool AutoAdvanceGates = false;

        // latched crew button events, consumed on the next Tick
        static bool goPressed, noGoPressed;

        // one-shot actuation intents FlightDriver consumes
        static bool launchPending;
        static bool abortLatched;
        static bool returnLeg;      // true once the undock gate (G14) clears — distinguishes the return
                                    // Phasing (departure) from the outbound Phasing (rendezvous)
        static bool dockedThisMission;  // ⭐ true once we have docked this mission (set on the UNDOCK press or a
                                        // live dock). Survives AUTO SEQUENCE off/on so re-engaging RESUMES at
                                        // departure (careful backaway → return), never re-docking. Reset per scene.

        // ⭐ The UNDOCK button calls this: we have berthed this mission, so the next AUTO SEQUENCE engage skips
        // rendezvous/dock and resumes at departure. (Set here rather than only on live-dock so it holds even
        // after the hooks are released and DockedSide.Docked goes false.) Also DISENGAGE AUTO SEQUENCE if it is
        // running (it holds at the berth) — so the crew's flow is exactly "press UNDOCK, then press AUTO
        // SEQUENCE" = fly the return: the single next press ENGAGES + resumes at departure, rather than toggling
        // a still-engaged conductor off.
        public static void MarkDockedThisMission()
        {
            dockedThisMission = true;
            if (engaged) Disengage();
        }

        // ---- screen-facing surface (unchanged signatures from the stub) ----
        public static bool Engaged { get { return engaged; } }

        // ⛔ W10 change (3): at a Fly step the host cannot fly, this says HOLD rather than naming the phase.
        // `pure/Pages.cs:810` renders the AUTO SEQUENCE button as "AUTO  " + this string, so returning
        // "Ascent to orbit" while the vehicle sits clamped to the pad would be the button CLAIMING a phase the
        // vehicle is not in. The conductor genuinely IS holding: the plan advanced into a Fly step and nothing
        // exists to complete it. Each Wave E / T-series increment that adds a controller (§B12.8 rider (c))
        // makes that step name itself again, one phase at a time, with no change here.
        public static string PhaseName
        {
            get
            {
                if (!engaged || plan == null || index >= plan.Length) return null;
                if (plan[index].Kind == StepKind.Fly && !FlightDriver.HasControllerFor(plan[index].Phase))
                    return "HOLD - NO CONTROLLER";
                return plan[index].Label;
            }
        }
        public static bool CrewActionNeeded()
        {
            return engaged && CurrentIsGate() && phase != GatePhase.Go;
        }
        public static Gate CurrentGate() { return gate; }
        public static ProcState Proc { get { ProcState p; p.Phase = phase; p.Satisfied = satisfied; return p; } }

        public static void Toggle() { if (engaged) Disengage(); else Engage(); }
        public static void ToggleItem(int i)
        {
            if (satisfied != null && i >= 0 && i < satisfied.Length && IsCrewItem(i))
                satisfied[i] = !satisfied[i];
        }
        public static void PressGo() { goPressed = true; }
        public static void PressNoGo() { noGoPressed = true; }

        // ⛔ W10 change (4): HONEST NO-OP (§14.4(a) — click, no light, no action, and NO RED). The gate card's
        // ABORT hands the mission to the abort responder, and that is register W19 (`src/AbortControl.cs`,
        // R1 §5.2 RECOVER-CODE, flight-validated) — not in this tree. Latching `GatePhase.Abort` here would
        // paint the gate card's status in `DragonPalette.Alarm` (`pure/GateCard.cs:195`): a red ABORT for an
        // abort that cannot happen, which is exactly the "lit button, dead press" this line exists to prevent.
        // It was a no-op in `_AutopilotStub.cs` and it stays one until W19 gives it something to hand to.
        public static void PressAbort() { }

        // ---- FlightDriver-facing surface ----
        public static bool ConsumeLaunch() { bool l = launchPending; launchPending = false; return l; }

        // Constant false this increment: nothing sets `abortLatched`, because `PressAbort` is a no-op until
        // W19. Kept (rather than deleted) because it is the recovered surface the abort responder binds to.
        public static bool AbortActive { get { return abortLatched; } }

        // ⛔ W10 change (2): ONE AUTHORITATIVE PHASE, and it must be one that is actually being flown. The
        // recovered file returned `plan[index].Phase` for any Fly step; with no controllers behind the host
        // that would publish e.g. `Ascent` to `Mission.AuthoritativePhase` (rule T4, `VesselData.cs:103`) for
        // a vehicle still on the pad, and the phase word on every screen would be wrong. Gating it on the
        // host's controller table makes the fallback the honest live classifier instead — which is precisely
        // what `Mission.AuthoritativePhase(engaged, Unknown, classified)` is designed to do (and is tested
        // for, `test/MissionPhaseTest.cs:32`).
        public static MissionPhase ActivePhase
        {
            get
            {
                if (!engaged || plan == null || index >= plan.Length) return MissionPhase.Unknown;
                if (plan[index].Kind != StepKind.Fly) return MissionPhase.Unknown;
                MissionPhase p = plan[index].Phase;
                return FlightDriver.HasControllerFor(p) ? p : MissionPhase.Unknown;
            }
        }
        public static MissionProfile Profile { get { return mission; } }

        // A ModeStep snapshot for the flight recorder (mission_phase + mode columns), built from the live
        // conductor state so the recorder always knows the phase — even during an abort or a between-phase gap.
        public static ModeStep CurrentMode
        {
            get
            {
                ModeStep ms = new ModeStep();
                ms.Index = index;
                ms.ActivePhase = ActivePhase;
                ms.Holding = engaged && CurrentIsGate() && phase != GatePhase.Go;
                ms.Flying = engaged && CurrentIsFly();
                ms.Aborted = abortLatched;
                return ms;
            }
        }

        public static bool IsReturn { get { return returnLeg; } }
        public static bool AtGate { get { return engaged && CurrentIsGate(); } }
        public static GateId CurrentGateId { get { return CurrentIsGate() ? plan[index].Gate : GateId.None; } }

        // The next GATE step after the current one — lets a flying controller know which leg it is on
        // (e.g. the docking approach leg toward WP0/WP1/WP2 is identified by the gate it leads to).
        public static GateId NextGateId
        {
            get
            {
                if (!engaged || plan == null) return GateId.None;
                for (int i = index; i < plan.Length; i++)
                    if (plan[i].Kind == StepKind.Gate) return plan[i].Gate;
                return GateId.None;
            }
        }

        // Signal from a flying controller that its phase is complete → the conductor advances.
        // ⚠ NOTHING CALLS THIS TODAY, and that is the honest state of the build: no controller exists to
        // finish a phase, so the plan holds at its first Fly step (see PhaseName/ActivePhase above). Each
        // increment that lands a controller calls it (§B12.8 rider (c)).
        public static void PhaseComplete()
        {
            if (!engaged || plan == null || !CurrentIsFly()) return;
            ModeStep ms = ModeManager.Advance(plan, index, new ModeInputs { PhaseComplete = true });
            index = ms.Index; LoadGate();
        }

        static void Engage()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;
            mission = Missions.Resolve(v.vesselName);
            if (!mission.Valid)
            {
                // NO-GO rather than fly a guessed mission — surface it, do not engage.
                Debug.LogWarning("[DragonScreen] AUTO SEQUENCE: craft name '" + v.vesselName
                                 + "' matches no mission profile — NO-GO. Rename the craft to a mission.");
            }
            plan = ModeManager.Plan(mission);
            index = 0; engaged = true; boundVesselId = v.persistentId;
            goPressed = noGoPressed = false;
            launchPending = false; abortLatched = false; returnLeg = false;

            // ⭐⭐ STATE-AWARE RESUME (user 2026-08-29): AUTO SEQUENCE must KNOW WHERE IT IS and what to do next —
            // pressing it in orbit must NOT restart the launch. Map the LIVE vessel state to the right plan step:
            // on the pad → countdown; ascending → ascent; in orbit not-docked with a station target → rendezvous;
            // docked/post-dock → departure; in orbit otherwise → deorbit; entering → ride it down. returnLeg is
            // set whenever we resume at/after the departure/deorbit branch. (This is a READ of vessel state; it
            // selects a plan position and commands nothing.)
            int resume = ResumeIndex(v, plan, mission);
            if (resume > 0 && resume < plan.Length)
            {
                index = resume;
                returnLeg = ResumeIsReturn(plan, resume);
                Debug.Log("[DragonScreen] AUTO SEQUENCE: state-aware RESUME at step " + resume + " '"
                          + plan[resume].Label + "' (situation " + v.situation + ", pe "
                          + (v.orbit != null ? (v.orbit.PeA / 1000.0).ToString("F0") : "?") + " km) — not restarting the launch.");
            }

            LoadGate();
            Debug.Log("[DragonScreen] AUTO SEQUENCE engaged: " + mission.Name + " (" + plan.Length + " steps"
                      + (index > 0 ? ", resumed at step " + index + ")" : ")"));
        }

        // ⭐ Map the LIVE vessel state → the plan step AUTO SEQUENCE should start at. The real Crew Dragon flight
        // rules pick the phase (and its abort mode) from where the vehicle physically IS.
        // 0 = start from the pad countdown. Never guesses launch when the vehicle is already in space.
        static int ResumeIndex(Vessel v, MissionStep[] p, MissionProfile m)
        {
            if (v == null || p == null) return 0;
            Vessel.Situations sit = v.situation;

            // On the pad (or landed/splashed at rest) → the full countdown from the start.
            if (sit == Vessel.Situations.PRELAUNCH || sit == Vessel.Situations.LANDED
                || sit == Vessel.Situations.SPLASHED) return 0;

            CelestialBody body = v.mainBody;
            double atm = (body != null && body.atmosphere) ? body.atmosphereDepth : 140000.0;
            Orbit o = v.orbit;
            double pe = o != null ? o.PeA : -1.0;
            double ap = o != null ? o.ApA : -1.0;
            bool descending = v.verticalSpeed < -1.0;
            bool inAtmo = v.altitude < atm;

            // Descending in the atmosphere at speed → already re-entering: ride it down (entry → chutes).
            if (inAtmo && descending && v.srfSpeed > 300.0) return PhaseIndex(p, MissionPhase.Entry);

            bool inSpace = sit == Vessel.Situations.ORBITING || sit == Vessel.Situations.ESCAPING
                        || sit == Vessel.Situations.DOCKED || (ap > atm && pe > 0.0 && !inAtmo);

            if (inSpace)
            {
                // periapsis already below the atmosphere and coasting down → committed to entry.
                if (pe < atm && descending) return PhaseIndex(p, MissionPhase.Entry);
                // docked, or docked earlier this mission → the return begins at DEPARTURE (careful KOS backaway).
                if (dockedThisMission || DockedSide.Docked(v)) return DepartureStepIndex(p);
                // ISS crew, not yet docked, a station is targeted → go RENDEZVOUS (the outbound Phasing), not launch.
                if (m.HasRendezvous && v.targetObject != null && v.targetObject.GetOrbit() != null)
                    return RendezvousStepIndex(p);
                // otherwise (free-flyer done, or nothing to rendezvous with) → come home: the DEORBIT gate.
                return DeorbitGateIndex(p);
            }

            // Sub-orbital / flying and NOT descending → still ascending: resume the ascent.
            if (sit == Vessel.Situations.FLYING || sit == Vessel.Situations.SUB_ORBITAL)
                return descending ? PhaseIndex(p, MissionPhase.Entry) : PhaseIndex(p, MissionPhase.Ascent);

            return 0;
        }

        // returnLeg must be true whenever we resume at or past the undock gate (departure/deorbit/entry), so the
        // shared Phasing phase flies as departure and the return controllers own it.
        static bool ResumeIsReturn(MissionStep[] p, int resume)
        {
            int dep = DepartureStepIndex(p);
            return dep >= 0 && resume >= dep;
        }

        // The FIRST Fly step of a given phase (ascent, entry, …). −1 if absent.
        static int PhaseIndex(MissionStep[] p, MissionPhase phase)
        {
            if (p == null) return -1;
            for (int i = 0; i < p.Length; i++)
                if (p[i].Kind == StepKind.Fly && p[i].Phase == phase) return i;
            return -1;
        }

        // The OUTBOUND rendezvous — the FIRST Phasing Fly step (before the undock gate). −1 for a free-flyer.
        static int RendezvousStepIndex(MissionStep[] p)
        {
            if (p == null) return -1;
            int dep = DepartureStepIndex(p);   // the departure Phasing is AFTER this; the outbound one is before
            for (int i = 0; i < p.Length; i++)
                if (p[i].Kind == StepKind.Fly && p[i].Phase == MissionPhase.Phasing && (dep < 0 || i < dep)) return i;
            return -1;
        }

        // The DEORBIT gate step (G15) — the start of the return for a vehicle that has no more station business. −1 if absent.
        static int DeorbitGateIndex(MissionStep[] p)
        {
            if (p == null) return -1;
            for (int i = 0; i < p.Length; i++)
                if (p[i].Kind == StepKind.Gate && p[i].Gate == GateId.DeorbitGoG15) return i;
            return -1;
        }

        // The plan index of the DEPARTURE (return) phase — the Fly step right after the G14 undock gate. −1 if
        // the profile has no dock (free-flyer) or no such step. Used by the state-aware resume.
        static int DepartureStepIndex(MissionStep[] p)
        {
            if (p == null) return -1;
            for (int i = 0; i < p.Length; i++)
                if (p[i].Kind == StepKind.Gate && p[i].Gate == GateId.UndockGoG14)
                    for (int j = i + 1; j < p.Length; j++)
                        if (p[j].Kind == StepKind.Fly) return j;
            return -1;
        }

        static void Disengage()
        {
            engaged = false; plan = null; satisfied = null; gate = new Gate();
            phase = GatePhase.Holding; launchPending = false; abortLatched = false;
            Debug.Log("[DragonScreen] AUTO SEQUENCE disengaged");
        }

        // ⛔ Hard reset for a NEW flight scene (revert-to-VAB/launch, fresh launch). The conductor is static,
        // so without this the previous flight's engaged/index/return state carries onto the next vehicle and
        // the conductor resumes mid-mission on a fresh pad rocket. Called from FlightDriver.Start().
        public static void ForceReset()
        {
            engaged = false; plan = null; satisfied = null; gate = new Gate();
            phase = GatePhase.Holding; index = 0; boundVesselId = 0;
            goPressed = noGoPressed = false;
            launchPending = false; abortLatched = false; returnLeg = false;
            dockedThisMission = false;   // a fresh scene has not docked yet
        }

        static bool CurrentIsGate() { return plan != null && index < plan.Length && plan[index].Kind == StepKind.Gate; }
        static bool CurrentIsFly() { return plan != null && index < plan.Length && plan[index].Kind == StepKind.Fly; }
        static bool IsCrewItem(int i)
        {
            return gate.Items != null && i < gate.Items.Length && gate.Items[i].Kind == ItemKind.CrewAck;
        }

        static void LoadGate()
        {
            if (CurrentIsGate())
            {
                gate = CrewGates.ById(mission, plan[index].Gate);
                int n = (gate.Items == null) ? 0 : gate.Items.Length;
                satisfied = new bool[n];
                phase = GatePhase.Holding;
            }
            else { gate = new Gate(); satisfied = null; phase = GatePhase.Holding; }
        }

        // Advanced each physics frame by FlightDriver with the live vessel. READS the vessel; commands nothing.
        public static void Tick(Vessel v)
        {
            if (!engaged || plan == null || v == null) return;
            if (v.persistentId != boundVesselId) { boundVesselId = v.persistentId; }   // follow handover
            if (DockedSide.Docked(v)) dockedThisMission = true;   // ⭐ remember the berth (post-dock AUTO SEQUENCE resume)
            if (index >= plan.Length) return;   // mission complete

            // At a Fly step the plan HOLDS: only a flying controller's PhaseComplete() advances it, and this
            // build has none. Drop any stale press so it cannot fire at the next gate.
            if (!CurrentIsGate()) { goPressed = noGoPressed = false; return; }

            // satisfy the AUTO items from live vessel state; CrewAck items keep their tapped value.
            if (gate.Items != null)
                for (int i = 0; i < gate.Items.Length && i < satisfied.Length; i++)
                    if (gate.Items[i].Kind == ItemKind.Auto)
                        satisfied[i] = AutoSatisfied(gate.Id, gate.Items[i].Label, v);

            // ⛔ hands-off test mode — SHIPS FALSE (W10, see the header). When on it auto-taps the crew-ack
            // items and presses GO, so the gates clear themselves and the crew is out of the loop entirely.
            if (AutoAdvanceGates)
            {
                if (gate.Items != null)
                    for (int i = 0; i < gate.Items.Length && i < satisfied.Length; i++)
                        if (gate.Items[i].Kind == ItemKind.CrewAck) satisfied[i] = true;
                goPressed = true;
            }

            CrewGateInputs gi;
            gi.Gate = gate; gi.Satisfied = satisfied;
            gi.GoPressed = goPressed; gi.NoGoPressed = noGoPressed;
            gi.AbortPressed = false;   // W10 change (4): PressAbort is a no-op until W19 — never latch a red ABORT.
            CrewGateStep step = CrewGate.Step(gi, phase);
            phase = step.Phase;
            // ⭐ CONSUMED ON THE FRAME IT WAS PRESSED: cleared unconditionally, after exactly one Step call, so a
            // GO can never clear two gates and a GO on an unsatisfied checklist is discarded, not remembered.
            goPressed = noGoPressed = false;

            if (step.Cleared)
            {
                GateId cleared = gate.Id;
                if (cleared == GateId.LaunchGoG7) launchPending = true;   // ignition INTENT — see FlightDriver
                if (cleared == GateId.UndockGoG14) returnLeg = true;      // now on the return leg
                ModeStep ms = ModeManager.Advance(plan, index, new ModeInputs { GateGo = true });
                index = ms.Index; LoadGate();
            }
        }

        // AUTO-item truth from the vessel. Real proxies where a signal exists; a healthy-pad default of
        // true otherwise (these are confirmations that are nominal on a good pad — the CrewAck items are
        // the real gates). Richer signals (LS margins, alignment) get wired as each system lands.
        static bool AutoSatisfied(GateId id, string label, Vessel v)
        {
            try
            {
                if (label.IndexOf("internal power", StringComparison.OrdinalIgnoreCase) >= 0
                    || label.IndexOf("power", StringComparison.OrdinalIgnoreCase) >= 0)
                    return HasCharge(v);
                if (label.IndexOf("consumables", StringComparison.OrdinalIgnoreCase) >= 0)
                    return v.GetCrewCount() > 0;   // crew aboard + LS present (LS margin proxy for now)
                if (label.IndexOf("Abort system armed", StringComparison.OrdinalIgnoreCase) >= 0)
                    return FlightCommands.EscapeArmed;
            }
            catch { }
            return true;   // nominal-on-a-healthy-pad confirmation
        }

        static bool HasCharge(Vessel v)
        {
            try
            {
                PartResourceDefinition def = PartResourceLibrary.Instance.GetDefinition("ElectricCharge");
                if (def == null) return true;
                double amt, max;
                v.GetConnectedResourceTotals(def.id, out amt, out max, true);
                return amt > 0.0;
            }
            catch { return true; }
        }
    }
}
