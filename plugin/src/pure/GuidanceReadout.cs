// DragonScreen — GuidanceReadout  (PURE: what the flight recorder is told about MechJeb's steering)
// ============================================================================================
// Register S223 JOB 2 (NTSB-2026-001 R-03). ⛔ THE ACCIDENT THIS EXISTS FOR:
//
//     For 25 seconds of the last flight MechJeb's own ascent `Status` read "WARNING: Unstable
//     Guidance" and the commanded pitch was `min(90, SrfvelPitch(), VesselState.Pitch)` — a law that
//     can only pitch DOWN (`MechJebModuleAscentPSGAutopilot.cs:181-184`). **None of that exists
//     anywhere in the recording.** The accident was only readable because `KSP.log` had not yet been
//     overwritten.
//
// Five black-box columns were declared for exactly this and never fitted: `gnc_module`, `gnc_status`,
// `cmd_pitch_deg`, `cmd_heading_deg`, `cmd_throttle` (`BlackBoxSchema.cs`, `Fit.Unfitted`, "T17"/"T18").
// This struct is the seam that fits them: the glue (`MechConductor`) fills it out of the live core once
// per row, and `BlackBoxRecorder` writes it. It carries no MechJeb type, so the pure tree stays pure and
// the recorder does not have to learn what a `QuaternionD` is.
//
// ---- ⛔ THE DECLARED SOURCE WAS WRONG AND IS SUPERSEDED IN PLACE (C1.16 / G12) ----
// The three `cmd_*` columns were declared, in `BlackBoxSchema`, as coming from *"the conductor's
// command struct"*. **THAT NOTE IS WRONG AND MUST NOT BE BUILT TO.** Under §14.4(a) the conductor does
// not command attitude at all — MechJeb does, and the conductor only decides WHICH MechJeb module holds
// the vehicle. There is no conductor command struct to record, and building one to satisfy a stale note
// would be inventing a signal. The note is corrected in the schema, in place, with this reason attached;
// the real sources are named below and each was read in the vendored tree before it was chosen.
//
// ---- THE SOURCES, AND WHY EACH ONE ----
//  • `cmd_pitch_deg` / `cmd_heading_deg` ← `MechJebModuleAttitudeController.RequestedAttitude`
//    (`:109`, a world-frame `QuaternionD`, recomputed every `Drive` as `attitudeGetReferenceRotation
//    (attitudeReference) * attitudeTarget`), resolved to surface pitch/heading by the glue.
//    ⭐⭐ THIS IS THE RIGHT INSTRUMENT PRECISELY BECAUSE IT CAPTURES THE FALLBACK LAW'S OUTPUT. The
//    unstable-guidance branch calls `AttitudeTo(pitch, SrfvelHeading())` like any other branch, so its
//    runaway pitch-down arrives here as a number and not as an absence. A column fed from the PSG
//    solution instead would have gone blank for the 25 seconds that matter.
//  • `cmd_throttle` ← `MechJebModuleThrustController.TargetThrottle` (`:211`, `float`) — the throttle
//    the controller ASKED for, which is what pairs with the applied `throttle` column already recorded.
//  • `gnc_status` ← the ascent autopilot's own `Status` string (`MechJebModuleAscentBaseAutopilot.cs:21`),
//    which is the field that literally said "WARNING: Unstable Guidance", widened with
//    `Core.Guidance.Status` + `IsStable()` so a reader gets the convergence state and the word together.
//  • `pvg_vgo_mps` / `pvg_tgo_s` ← `MechJebModuleGuidanceController.Vgo` / `.Tgo` (`:49-50`).
//  • `tgt_ap_km` / `tgt_pe_km` / `tgt_inc_deg` ← the LIVE ascent settings. ⭐ That is S223 JOB 1's
//    question answered per row, and the cheapest R-04 there is: "what were we aiming at" stops being
//    something a reader has to infer from our intent.
//
// ---- ⛔ WHY THE `Have*` FLAGS, AND WHY THEY ARE NOT ONE FLAG ----
// §4.6: BLANK, NEVER A PLAUSIBLE NUMBER. The three groups become available at different moments — the
// target apsides exist as soon as a core is bound, the commanded attitude only once MechJeb holds the
// vehicle, the PVG numbers only once the solver runs — and collapsing them into one flag would either
// withhold a signal that exists or record one that does not. Each group's column is `Fit.Conditional`
// against its own flag, and `BlackBoxCoverage` reads a blank there as a fact about the flight.
// ============================================================================================

namespace DragonScreen
{
    /// <summary>
    /// One tick's worth of "what is flying this vehicle, and what is it asking for". PURE: filled by
    /// `MechConductor.Readout` from the live core, read by `BlackBoxRecorder`.
    /// </summary>
    public struct GuidanceReadout
    {
        /// <summary>An embedded core is bound. Everything below is blank when this is false.</summary>
        public bool Valid;

        /// <summary>
        /// The MechJeb module the conductor currently has engaged, by its vendored TYPE NAME, or
        /// "none". ⭐ "none" is a RECORDED ANSWER, not a blank: §2.5's rule for the idle seams is that
        /// recording the constant is itself the proof the seam was idle, and the same holds here — a
        /// reader must be able to tell "nothing was flying" from "nobody wrote this column".
        /// </summary>
        public string Module;

        /// <summary>That module's own status word, plus the PVG convergence state when the ascent is
        /// the module in question. Empty when no module is engaged.</summary>
        public string Status;

        /// <summary>MechJeb holds the vehicle and has commanded an attitude/throttle this tick.</summary>
        public bool HaveCommand;
        public double CmdPitchDeg, CmdHeadingDeg, CmdThrottle;
        // ⚠ `MechJebModuleAttitudeController.attitudeError` (:117) IS DELIBERATELY NOT HERE, and it is
        // not an oversight: it would be a genuinely useful eleventh column ("a command nobody achieved
        // is a different event from a command nobody gave"), but S223's scope is the ten columns the
        // brief names, and a new column inserted among them REORDERS the schema and forces a
        // `SchemaVersion` bump, which stops an analyser chaining a new recording with an old one
        // (§4.2). Logged as a stray on S223's register line instead (C1.1) — it belongs in an append
        // at the end of the table, which is a decision of its own.

        /// <summary>The PVG guidance controller is running and its numbers mean something.</summary>
        public bool HaveGuidance;
        public double VgoMps, TgoS;

        /// <summary>The live ascent target, readable whenever a core is bound.</summary>
        public bool HaveTarget;
        public double TgtApKm, TgtPeKm, TgtIncDeg;

        /// <summary>The all-blank reading: no core, nothing engaged, nothing commanded.</summary>
        public static GuidanceReadout None()
        {
            GuidanceReadout r = new GuidanceReadout();
            r.Valid = false; r.Module = null; r.Status = null;
            return r;
        }

        /// <summary>The module name a recorder should write when a core is bound and idle.</summary>
        public const string NoModule = "none";
    }
}
