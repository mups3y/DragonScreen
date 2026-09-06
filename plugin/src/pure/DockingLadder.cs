// DragonScreen — DockingLadder  (PURE: the speedLimit ladder, and which Docked leg is which)
// ============================================================================================
// Register T20, §B9 Phase 4 / §B10.3 / §B11 / §B14.2. The Docking Autopilot is the DEFAULT from the
// Keep-Out Sphere inward — **owner decision O6, 2026-09-03 via the overseer** — and §B10.3 names
// `speedLimit` "the single most important docking knob". This file owns the ladder it walks down, and
// the one other thing the plan's phase table cannot answer on its own: which of `ModeManager`'s two
// `Fly(Docked)` steps the conductor is standing on.
//
// ---- ⭐ THE TWO `Fly(Docked)` STEPS ARE DIFFERENT JOBS, AND THE PLAN SAYS SO IN TWO PLACES ----
// `pure/ModeManager.cs` puts TWO Docked steps in the mission, separated by a gate:
//       Fly(Docked, "Soft → hard capture")     → gate G13 "DOCKING COMPLETE — VESTIBULE"
//       Fly(Docked, "Docked — crew aboard")    → gate G14 "GO FOR UNDOCK"
// §B12.3's phase table has ONE `Docked` entry — *"Docked → idle/KILL-ROT"* — which describes the
// SECOND of those, the berthed state. The FIRST is §B9 Phase 4's *"capture at IDA-2"*, which the same
// §B12.3 sentence hands to the Docking AP: *"then hand off at the Keep-Out Sphere to the Docking AP —
// the DEFAULT (O6)"*. So `pure/Conductor.cs` returning KILL-ROT for `MissionPhase.Docked` is right for
// one of the two steps and wrong for the other, and nothing in the phase enum can tell them apart.
//
// ⭐ THE SEAM THAT CAN: `CrewProcedureOps.NextGateId`, whose own comment says it exists to *"let a
// flying controller know which leg it is on"*. The capture leg is the one walking toward **G13**; the
// berthed leg is the one walking toward **G14**. That is a fact about the mission plan, it is pure, and
// it is tested — so the discrimination lives here rather than as a special case in the glue, where it
// would rot the first time somebody tidied the dispatch.
// ⚠ LOGGED, NOT FIXED (C1.1): §B12.3's single `Docked` row does not distinguish them, and
// `docs/BUILD_PLAN.md` is a guarded file (G10). See T20's register line.
//
// ---- WHERE THE LADDER COMES FROM (§B10.3 + §B11, both [DOC]) ----
//   §B10.3: "**speedLimit** (m/s) — max approach speed cap. cfg **1**. Target: a **ladder DOWN through
//    the corridor** — keep-out approach ~1, waypoints ~0.3–0.5, contact ~**0.1–0.2** (real Dragon
//    closes very slowly). The single most important docking knob."
//   §B11:  "Crew Dragon **final contact ~0.1 m/s**, and rate must stay **< 0.2 m/s inside 5 m** range."
//
// ⛔ TWO OF THE THREE RUNGS ARE SINGLE DOCUMENTED VALUES AND ARE TAKEN AS THEY STAND: **1.0** m/s far
// (§B10.3's "~1", and the shipped cfg's own persisted `speedLimit = 1`) and **0.1** m/s at contact
// (§B11's "final contact ~0.1 m/s"). The middle rung is the only one the plan gives as a BAND rather
// than a value, and the rule applied is stated rather than hidden: **where the plan gives a band and no
// single figure, take the SLOWER end** — a too-slow approach costs time, a too-fast one costs the
// vehicle. So 0.3, not 0.5, and T22 converges it against a flown approach.
//
// ⛔ AND §B11's "< 0.2 m/s inside 5 m" IS A HARD LIMIT, NOT A RUNG. It is enforced separately, over the
// whole ladder, by `Conforms` — so a later tune that raises the contact rung past it fails the suite
// instead of quietly violating a documented corridor rule.
//
// PURE: no Unity, no KSP, no MechJeb reference.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>Which of the mission plan's two `Fly(Docked)` steps this is.</summary>
    public enum DockingLeg : byte
    {
        /// <summary>Not a docking step at all.</summary>
        None = 0,
        /// <summary>→ gate G13. §B9 Phase 4's "capture at IDA-2" — the Docking Autopilot flies it.</summary>
        Capture,
        /// <summary>→ gate G14. §B12.3's "Docked → idle/KILL-ROT" — berthed, holding attitude.</summary>
        Berthed
    }

    /// <summary>§B10.3's speedLimit ladder and the two-Docked-steps discrimination.</summary>
    public static class DockingLadder
    {
        // ── the three rungs ──────────────────────────────────────────────────────────────────────

        /// <summary>Outside the Keep-Out Sphere. §B10.3 "keep-out approach ~1", and the shipped
        /// `mechjeb_settings_type_Crew-Dragon.cfg`'s own persisted `speedLimit = 1`.</summary>
        public const double FarSpeedMps = 1.0;

        /// <summary>Inside the KOS, outside the contact range. §B10.3 "waypoints ~0.3–0.5" — a BAND, so
        /// the slower end is taken (see this file's header).</summary>
        public const double CorridorSpeedMps = 0.3;

        /// <summary>At contact. §B11 "[DOC] Crew Dragon **final contact ~0.1 m/s**".</summary>
        public const double ContactSpeedMps = 0.1;

        /// <summary>The range inside which §B11's hard rate rule applies: "rate must stay &lt; 0.2 m/s
        /// **inside 5 m** range".</summary>
        public const double ContactRangeM = 5.0;

        /// <summary>§B11's documented ceiling inside <see cref="ContactRangeM"/>. ⛔ A LIMIT, NOT A RUNG:
        /// nothing is ever set to it; it is what the ladder is checked against.</summary>
        public const double ContactRateLimitMps = 0.2;

        // ── the ladder ───────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// §B10.3's ladder: the `speedLimit` this range deserves.
        ///
        /// ⛔ IT IS A CAP, NOT A COMMAND. MechJeb's `FixSpeed` clamps its own computed approach speed to
        /// `speedLimit`; it never accelerates to reach it. So a rung that is too LOW only makes the
        /// approach slow, while a rung that is too HIGH removes the only guard there is — which is why
        /// the middle rung takes the slower end of §B10.3's band and why `Conforms` exists.
        /// </summary>
        public static double SpeedLimitFor(double rangeM)
        {
            if (rangeM <= 0.0) return ContactSpeedMps;                  // unknown range → the slowest rung
            if (rangeM <= ContactRangeM) return ContactSpeedMps;
            if (rangeM <= RendezvousOps.KeepOutSphereM) return CorridorSpeedMps;
            return FarSpeedMps;
        }

        /// <summary>
        /// Does a cap of <paramref name="capMps"/> honour §B11's documented corridor rule at this range?
        ///
        /// ⭐ THE CAP IS A PARAMETER, NOT READ FROM <see cref="SpeedLimitFor"/>, AND THAT IS DELIBERATE.
        /// A guard that computes its own input can only ever agree with itself: a version of this method
        /// that simply returned `true` would pass every check written against the real ladder, because
        /// the real ladder is conformant. It was written that way first and a mutation walked straight
        /// through it. Taking the cap as an argument means the suite can hand it a value the ladder
        /// would never produce and watch it say no.
        /// </summary>
        public static bool Conforms(double rangeM, double capMps)
        {
            return rangeM > ContactRangeM || capMps < ContactRateLimitMps;
        }

        /// <summary>The ladder's own cap at this range, checked against the rule. Convenience only.</summary>
        public static bool Conforms(double rangeM)
        {
            return Conforms(rangeM, SpeedLimitFor(rangeM));
        }

        /// <summary>The ladder never rises as the range closes. A property, swept by the suite.</summary>
        public static bool Monotone(double nearerM, double furtherM)
        {
            return SpeedLimitFor(nearerM) <= SpeedLimitFor(furtherM);
        }

        // ── which Docked step ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Which `Fly(Docked)` step this is, from the gate it walks toward — see this file's header for
        /// why the phase enum alone cannot answer it.
        /// </summary>
        public static DockingLeg LegFor(GateId nextGate)
        {
            switch (nextGate)
            {
                case GateId.DockingCompleteG13: return DockingLeg.Capture;
                case GateId.UndockGoG14:        return DockingLeg.Berthed;
                default:                        return DockingLeg.None;
            }
        }

        /// <summary>
        /// Is this step the one the Docking Autopilot flies? ⛔ ONLY the capture leg. Engaging it on the
        /// berthed leg would have the autopilot try to re-dock a vehicle that is already hard-mated.
        /// </summary>
        public static bool AutopilotFlies(DockingLeg leg) { return leg == DockingLeg.Capture; }

        /// <summary>
        /// Has this step finished?
        ///
        ///  • **Capture** ends on a MEASURED fact: the vehicle is docked. Not a timer, not a range.
        ///  • **Berthed** ⛔ NEVER ends on its own, and that is the design, not a gap.
        ///    `CrewProcedureOps.MarkDockedThisMission` — which the UNDOCK press calls — disengages AUTO
        ///    SEQUENCE, and the next engage resumes at the departure step past G14. That flow is stated
        ///    in that file's own comment: *"the crew's flow is exactly 'press UNDOCK, then press AUTO
        ///    SEQUENCE' = fly the return"*. A berthed step that completed itself would walk the plan
        ///    through the undock gate while the hooks were still closed.
        /// </summary>
        public static bool LegComplete(DockingLeg leg, bool docked)
        {
            return leg == DockingLeg.Capture && docked;
        }
    }
}
