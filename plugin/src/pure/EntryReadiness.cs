// DragonScreen — EntryReadiness  (PURE: what the Cover's ENTRY ENABLED row is allowed to say)
// ============================================================================================
// The Cover's deorbit procedure, step 2: "After SpaceX GO for deorbit, verify entry is enabled." The
// row under it drew `True` and `False` as two baked PNGs with the emphasis exported into the art — and
// the exported emphasis is on FALSE. So the page answered a safety question, permanently, on every
// phase, from a picture. QC C-08: *"It is a safety verdict with no model behind it, which is the
// S31/S32 guardrail: a verdict word must be computed, never declared."*
//
// ---- WHY THIS SAT HELD FOR TWO DAYS, AND WHAT UNBLOCKED IT ----
// The row's CLASS was undecided and the class decided the build. S49 H6 read it one way, QC C-08 read
// it another and said so; if the row means "the crew ticked it" it is a local latch and Part A, and if
// it means "the vehicle has armed entry" it is an arming flag and §14.4(a)'s honest no-op until Part B.
//
// The owner answered the underlying question on 2026-09-05 (Q3, verbatim):
//
//     "that list should be the autopilot checking everything is ready for re-entry, so if it cannot
//      hold real values we simulate the vehicle performing the checklist. After confirming everything
//      is green/ticked there should be a crew gate question to continue with re-entry go no go
//      decision. If yes autopilot proceeds with re-entry if no there must be a way for the user to
//      retriger the sequence when ready to re-enter"
//
// ...and attached his own condition to it: *"I will answer them and then ask the overseer to assess
// before acting on them."* That assessment happened on 2026-09-06 and settled the class:
//
//   ⭐ THE CHECKLIST AND THE GO/NO-GO GATE COMMAND NOTHING THAT FLIES, so they are §14.4(f) — include
//      the feature, FILL it from a live source, else a coherent MARKED simulation that BEHAVES live.
//   ⛔ THE CREW-GO → AUTOPILOT EDGE *does* fly the vehicle and stays an honest no-op until Part B.
//
// So this row is a READOUT of the autopilot's own readiness check. It is NEITHER a crew latch nor an
// arming flag, which is why the two hit rects it used to carry are gone (see CoverPage.Hits): a verdict
// the crew cannot set must not offer them a rectangle that looks like they can.
//
// ---- AND THE MODEL ALREADY EXISTED. NOTHING IS SIMULATED HERE ----
// "The autopilot checking everything is ready for re-entry" is `pure/CrewGate.cs` + `pure/CrewGates.cs`,
// restored by W4 and given a live driver by W10 (`src/CrewProcedureOps.cs` feeds it the vessel every
// physics frame). `CrewGates.Return()` already defines the exact gate this row is about:
//
//     G(GateId.DeorbitGoG15, "GO FOR DEORBIT BURN",
//         A("Departure burns complete — stable orbit below the station"),
//         A("Consumables margin for return + reserve"),
//         C("Mission control GO for deorbit"))
//
// Two AUTO items the machine confirms from vessel state and one CREW item the crew tap — which is the
// owner's sentence, built, months before he wrote it. This file does not re-derive any of that. It
// answers ONE question: given where that machine has got to, what may the row say?
//
// ---- THE DASH IS THE POINT ----
// ⛔ `Unknown` is not a failure mode, it is the honest answer whenever the gate machine is not running.
// The conductor is only engaged when the crew engage it, and with it disengaged there is no autopilot
// checking anything — so a `False` there would be exactly the defect being removed: a confident verdict
// with nothing behind it. QC's own must-not-break says so: *"the row must dash, not read `False`, when
// there is no source."* Once the conductor IS engaged, `NotEnabled` is a real verdict rather than a
// default — the autopilot is checking and the answer is no.
// ============================================================================================
namespace DragonScreen
{
    /// <summary>What the ENTRY ENABLED row may say. Three states, because "nobody is checking" and
    /// "checked, and no" are different facts and the crew must be able to tell them apart.</summary>
    public enum EntryVerdict : byte { Unknown = 0, NotEnabled, Enabled }

    /// <summary>Where the crew-gate machine has got to, as the row needs to see it. Every field is
    /// read off `CrewProcedureOps`; nothing here is invented and nothing is a threshold.</summary>
    public struct EntryReadinessInputs
    {
        /// <summary>The mission conductor is running at all. False = nothing is checking = no verdict.</summary>
        public bool ConductorEngaged;
        /// <summary>The gate the conductor is holding at IS the deorbit gate (`GateId.DeorbitGoG15`).</summary>
        public bool AtEntryGate;
        /// <summary>That gate's phase, straight from `CrewGate.Step`.</summary>
        public GatePhase EntryGatePhase;
        /// <summary>Every item on that gate is satisfied — `CrewGate.AllSatisfied`, not a second count.</summary>
        public bool ChecklistComplete;
    }

    public static class EntryReadiness
    {
        /// <summary>
        /// The row's verdict. Reading order matters and is the safety order:
        ///
        /// 1. NOT ENGAGED wins over everything — no machine, no verdict, dash. ⛔ This is checked FIRST
        ///    so that no later branch can manufacture a confident answer out of a struct that was never
        ///    filled: an unengaged conductor leaves `EntryGatePhase` at its default `Holding`, and a
        ///    rule ordered the other way would read that as a real "not enabled".
        /// 2. ABORT — not enabled, and it is the one state that cannot be argued out of.
        /// 3. NOT AT THE ENTRY GATE — the conductor is checking, and it has not reached this gate.
        ///    Entry genuinely is not enabled yet, so this is a verdict and not an absence.
        /// 4. NO-GO — the crew held the gate. `CrewGate` holds rather than cancels (`:109-110`), which
        ///    is the "way to retrigger when ready" the owner asked for, already built. Entry is not
        ///    enabled while the hold stands.
        /// 5. GO — the gate cleared. Entry is enabled.
        /// 6. otherwise the checklist itself decides: complete (GoReady) is enabled, working is not.
        ///
        /// ⚠ `GoReady` READS AS ENABLED, and that is deliberate rather than lax. The gate's own last
        /// item is the CREW item "Mission control GO for deorbit", so a complete checklist already
        /// contains the SpaceX GO the Cover's step 2 names — "AFTER SpaceX GO for deorbit, verify entry
        /// is enabled". The row is what the crew verify at that moment; it is not the crew's own GO,
        /// which is the separate press that clears the gate.
        /// </summary>
        public static EntryVerdict Of(EntryReadinessInputs s)
        {
            if (!s.ConductorEngaged) return EntryVerdict.Unknown;
            if (s.EntryGatePhase == GatePhase.Abort) return EntryVerdict.NotEnabled;
            if (!s.AtEntryGate) return EntryVerdict.NotEnabled;
            if (s.EntryGatePhase == GatePhase.NoGo) return EntryVerdict.NotEnabled;
            if (s.EntryGatePhase == GatePhase.Go) return EntryVerdict.Enabled;
            return s.ChecklistComplete ? EntryVerdict.Enabled : EntryVerdict.NotEnabled;
        }

        /// <summary>The word the row draws for a verdict, or the dash when there is nothing to say.
        /// ⛔ `True` / `False` are REFERENCE COPY, not our wording: `docs/UI_AUDIT.md`'s Cover label
        /// list carries `ENTRY ENABLED`, `True` and `False` verbatim, which is why they are reproduced
        /// exactly rather than re-worded to something like ENABLED/INHIBITED (§1.4).</summary>
        public static string Text(EntryVerdict v)
        {
            if (v == EntryVerdict.Enabled) return "True";
            if (v == EntryVerdict.NotEnabled) return "False";
            return Dashes.None;
        }
    }
}
