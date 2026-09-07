// DragonScreen — BlackBox / EVENT LOG  (register BB1; spec: §2.9, §4.1, §4.5)
// ============================================================================================
// PURE. The EVR half of §1.3, and §3.4 files it BUILD FRESH: NEITHER prior recorder had one. Recorder
// A had free-text `a_note`/`r_note` per row (which no tool ever parsed) plus edge latches it degraded
// by folding the edge into the next 5 Hz sample; Recorder B had state columns only.
//
// ---- WHY JSONL AND NOT MORE CSV (§4.1) ----
// The parameter stream is rectangular and CSV fits it perfectly — which is why §3.4 reuses the corpus
// format wholesale for it. The event log is NOT rectangular: a `gnc.replan` payload and a `crew.touch`
// payload share almost no fields. Forcing them into one flat schema either explodes the column count
// or collapses everything into an escaped free-text blob, and the escaped-blob version is exactly what
// `a_note` was. JSONL keeps payloads typed and variable, appends cleanly, TOLERATES A TRUNCATED FINAL
// LINE, and costs the Python tooling three lines.
//
// ---- SUB-FRAME EDGE LATCHING (§2.9) ----
// An event carries ITS OWN `ut` — the instant it was DETECTED in FixedUpdate — not the next row's.
// It also carries the `seq` of the row it falls between, so it is placed exactly AND is joinable to
// the stream without a search. Quantising a transition to the row period throws away the one thing
// that makes a narrative (§1.4(d)), and a separate stream removes the compromise entirely.
//
// ---- NO ALLOCATION IN THE STEADY STATE, AND WHY THAT IS EASY HERE ----
// Events are rare (§2.10 estimates 2 000-10 000 per 19 h mission, i.e. ~0.1/s) so this file allocates
// freely — a `StringBuilder` per event is nothing at that rate. It is the ROW path that must not
// allocate, and the row path does not come through here.
// ============================================================================================
using System;
using System.Globalization;
using System.Text;

namespace DragonScreen.BlackBox
{
    /// <summary>One typed key/value in an event payload. Value is pre-rendered JSON.</summary>
    public struct Kv
    {
        public string Key;
        public string Json;

        public static Kv Str(string k, string v)  { Kv p; p.Key = k; p.Json = BlackBoxEvents.JsonString(v); return p; }
        public static Kv Num(string k, double v)  { Kv p; p.Key = k; p.Json = BlackBoxEvents.JsonNum(v); return p; }
        public static Kv Int(string k, int v)     { Kv p; p.Key = k; p.Json = v.ToString(CultureInfo.InvariantCulture); return p; }
        public static Kv Bit(string k, bool v)    { Kv p; p.Key = k; p.Json = v ? "true" : "false"; return p; }
    }

    public static class BlackBoxEvents
    {
        /// <summary>
        /// ⛔ NaN/Inf → `null`, never 0 and never the bare tokens `NaN`/`Infinity` (which are not JSON
        /// and which `json.loads` rejects, taking the whole line with them). Same rule as §4.6's
        /// blank-not-zero for the CSV, expressed in the type system JSON actually has.
        /// </summary>
        public static string JsonNum(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "null";
            return v.ToString("0.######", CultureInfo.InvariantCulture);
        }

        /// <summary>RFC-8259 string escaping. Control characters go out as \u00XX, not raw.</summary>
        public static string JsonString(string s)
        {
            if (s == null) return "null";
            var sb = new StringBuilder(s.Length + 2);
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                switch (ch)
                {
                    case '"':  sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(ch);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>
        /// One event, one line. The four fixed keys come first and in a fixed order so a reader can
        /// see the shape without parsing the payload; the payload follows under `p` so a key named
        /// `ut` inside a payload can never shadow the event's own clock.
        /// </summary>
        public static string Line(string missionId, string vessel, double ut, double metS, long seq,
                                  string kind, Kv[] payload)
        {
            var sb = new StringBuilder(160);
            sb.Append('{');
            sb.Append("\"mission_id\":").Append(JsonString(missionId));
            sb.Append(",\"vessel\":").Append(JsonString(vessel));
            sb.Append(",\"ut\":").Append(JsonNum(ut));
            sb.Append(",\"met_s\":").Append(JsonNum(metS));
            sb.Append(",\"seq\":").Append(seq.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"kind\":").Append(JsonString(kind));
            sb.Append(",\"p\":{");
            if (payload != null)
            {
                for (int i = 0; i < payload.Length; i++)
                {
                    if (payload[i].Key == null) continue;
                    if (i > 0) sb.Append(',');
                    sb.Append(JsonString(payload[i].Key)).Append(':').Append(payload[i].Json ?? "null");
                }
            }
            sb.Append("}}");
            return sb.ToString();
        }

        // ---- §2.9's namespaces, as constants, so a typo is a compile error and not a lost channel ----
        // A misspelled `kind` does not fail: the line is written, the reader's filter misses it, and the
        // event is invisible in exactly the way §0's misdiagnoses were invisible. Naming them costs
        // nothing and removes the failure mode.
        // ---- ⚠ S90, 2026-09-06: `RecClose` and `RecSceneChange` WERE DECLARED HERE AND RETIRED ----
        // They read, verbatim:
        //     public const string RecClose       = "rec.close";
        //     public const string RecSceneChange = "rec.scene_change";
        // Nothing ever emitted either — `grep` found each name exactly once, at its own declaration.
        //
        // ⛔ THEY WERE REMOVED RATHER THAN WIRED, and this is the reasoning, because the opposite
        // choice was equally available and someone will wonder. Both are ALREADY SAID by
        // `RecStreamEnd`, which carries a `reason` — and `"scene_change"` is one of the reasons
        // actually passed (`BlackBoxRecorder.cs:123`, `OnDestroy` → `Close("scene_change")`), beside
        // `scene_start`, `self_disable`, `revert`, `row_failed`, `width_mismatch`, `size_ceiling`.
        // So wiring them would have produced a SECOND event saying what `rec.stream_end` already
        // says, on the same edge — two channels for one fact, which is the defect C7.1 is about,
        // not a fix for it.
        //
        // ⭐ The third ghost, `SysStringState`, was the OPPOSITE case and was WIRED instead: its
        // siblings (bus trip, fire start/out, leak start/isolate) all had emitters and it alone did
        // not, so the six power strings had R2 columns and no transitions. See BlackBoxRecorder's
        // systems-edges block. **The two halves of S90 went opposite ways on purpose.**
        //
        // ⚠ The header above still stands: naming a kind as a constant makes a typo a compile error
        // rather than a lost channel. What it does NOT do — and what S90 is — is guarantee that every
        // declared kind has an emitter. A reader filtering for a declared-but-dead kind finds nothing
        // and concludes the thing never happened, which is S76's ghost-column defect one level up.
        public const string RecOpen        = "rec.open";
        public const string RecRevert      = "rec.revert_detected";
        public const string RecVesselChange = "rec.vessel_change";
        public const string RecFocusChange = "rec.focus_change";
        public const string RecWarpChange  = "rec.warp_change";
        public const string RecWriteError  = "rec.write_error";
        public const string RecSelfDisable = "rec.self_disable";
        public const string RecWidthMismatch = "rec.width_mismatch";
        public const string RecRotate      = "rec.rotate";
        /// <summary>⭐ BB1's own addition — the S76 ghost-column defect, reported as an event at close.</summary>
        public const string RecColumnNeverWritten = "rec.column_never_written";
        /// <summary>The other direction: an Unfitted column produced values, so the manifest is now wrong.</summary>
        public const string RecColumnUnexpected = "rec.column_unexpected_writer";
        /// <summary>⭐ The §4.6 torn-row fix: written last, so a reader can tell a clean close from a cut file.</summary>
        public const string RecStreamEnd   = "rec.stream_end";

        public const string FlightLiftoff  = "flight.liftoff";
        public const string FlightMaxQ     = "flight.maxq";
        public const string FlightStaged   = "stage.staged";
        public const string EngineIgnite   = "stage.engine_ignite";
        public const string EngineShutdown = "stage.engine_shutdown";
        public const string EngineFlameout = "stage.engine_flameout";
        public const string FlightDrogue   = "flight.drogue_deploy";
        public const string FlightMain     = "flight.main_deploy";
        public const string FlightSplashdown = "flight.splashdown";
        public const string FlightTouchdown  = "flight.touchdown";

        public const string PhaseTransition = "phase.transition";
        public const string GncModeChange   = "gnc.mode_change";
        public const string CrewPageChange  = "crew.page_change";
        /// <summary>⭐ S85. A press that HIT a control — §2.7's `control_id` plus §2.9's verdict.</summary>
        public const string CrewPress       = "crew.press";
        /// <summary>⭐ S85. A touch that hit NO control. A different fact from a press that did nothing,
        /// and both are facts a poll of the screen state can never produce.</summary>
        public const string CrewTouch       = "crew.touch";
        /// <summary>⭐ S85. The press buffer overflowed and interactions were lost. Should never appear:
        /// presses are human-rate against a `FixedUpdate` drain. It is in the RECORDING rather than a log
        /// line because S76 is what a recorder losing data quietly costs.</summary>
        public const string CrewPressDropped = "crew.press_dropped";
        /// <summary>
        /// ⚠ §2.9 also names `crew.dispatch`. It is deliberately NOT defined, and that is a statement,
        /// not an omission: both choke points dispatch SYNCHRONOUSLY inside the press, so the dispatch
        /// and the press are one instant and one record — `crew.press` already carries `cmd`, `acted`
        /// and `press_kind`. A second event per press would double every dispatching press in the
        /// timeline, and in an ordered narrative a duplicate reads as two occurrences (the same reason
        /// BB2 emits capsule singletons from one stream only). If a dispatch is ever deferred past the
        /// press, that is when this kind earns its existence.
        /// </summary>

        public const string SysBusTrip     = "sys.bus_trip";
        public const string SysStringState = "sys.string_state";
        public const string SysFireStart   = "sys.fire_start";
        public const string SysFireOut     = "sys.fire_out";
        public const string SysLeakStart   = "sys.leak_start";
        public const string SysIsolate     = "sys.isolate";

        public const string FaultRaised    = "fault.raised";
        public const string FaultCleared   = "fault.cleared";
        // ---- ⚠ S161, 2026-09-06: `Exception` WAS DECLARED HERE AND RETIRED ----
        //     public const string Exception      = "exception";
        // Nothing ever emitted it. ⛔ RETIRED RATHER THAN WIRED, and for the opposite reason to the two
        // coverage kinds S161 DID wire: the recorder's own failure modes already have named kinds that
        // ARE emitted — `rec.write_error`, `rec.width_mismatch`, `rec.self_disable` — and its catch
        // blocks route to those. A generic "exception" would be a SECOND way to say what those three
        // already say, on the same edge, which is the argument S90 retired `rec.close` on.
        // ⚠ `tools/assess_flight.py`'s alert list was updated in the same commit: a scan for a kind
        // that cannot exist must not outlive the kind.

        /// <summary>⭐ [[OCT11]]. The host has COMMANDED a bank (`BoosterHost.CommandedRole`) and that
        /// bank's own ignition state says it is not lit — the exact shape that lost the booster on flight
        /// Crew-2_20260829_144114 (*"eng_ignited=0 whole descent" → ballistic → LOST @14 km. Root =
        /// RealFuels ullage"*, register H1b / W5). Raised on the RISING edge only (`boost.ignition_resolved`
        /// is the falling one) so a reader sees exactly one pair per standing occurrence, not one line per
        /// tick. ⛔ Announces only — nothing may read this kind as a signal to retry the activate; that
        /// policy is register W5's (`pure/BoosterHostPlan.CommandedNotIgnited`'s own header has the ruling).</summary>
        public const string BoostCommandedNotIgnited = "boost.commanded_not_ignited";
        /// <summary>⭐ [[OCT11]]. The falling edge of the above: the bank the host was holding lit finally
        /// ignited, or the command was withdrawn (role changed / host released). A separate kind from its
        /// rising edge — as `stage.engine_ignite`/`stage.engine_shutdown` already are for the vessel-wide
        /// count — so a reader measures how long the divergence stood without diffing two payload shapes.</summary>
        public const string BoostIgnitionResolved = "boost.ignition_resolved";

        // ---- ⭐⭐ S227, 2026-09-08: PART LOSS. The channel the owner asked for and we did not have ----
        // Owner, verbatim: *"we need to know exactly if or when a part failed and which failed first …
        // if a separator suddenly disappears and was not commanded to do anything it is probably
        // because it exploded or failed. This would lead to a cascade of failures that could be
        // confusing without analysing the very first failure point that lead to a instant RUD."*
        //
        // ⛔ EVERY STAGE EVENT ABOVE IS A **COMMANDED** ACTION. `stage.staged`, `stage.engine_ignite`
        // and `stage.engine_shutdown` record what the vehicle was TOLD to do. Nothing in this
        // vocabulary before S227 recorded something the vehicle did NOT choose, so an uncommanded loss
        // produced no line at all — not an event, not a column, not a count that dropped.
        //
        // ⭐ THE PAYLOAD JOINS TO `craftdump.csv` WITH NO LOOKUP TABLE. Every one of these carries
        // `part_idx`, `part_name`, `persistent_id` and `stage` under those EXACT key names, because
        // that dump (7,083 rows, written on the pad by `src/CraftDump.cs`) already uses them and it is
        // the contract the pure tests are written against (OCT2). A different spelling here would mean
        // an analyst joining two files by hand at the moment they can least afford to.
        //
        // ⚠ `part.explode` IS DELIBERATELY ABSENT, and this is the one rejection worth stating in the
        // vocabulary itself rather than only in the register: KSP's `onPartExplode` carries
        // `GameEvents.ExplosionReaction`, whose ONLY fields are `distance` and `magnitude`. **It does
        // not name a part.** A kind fed from it could say an explosion happened and never say what
        // exploded, which is precisely the "confusing cascade" the owner is trying to see through.
        /// <summary>
        /// ⭐ A part left the vessel. Raised from `onPartWillDie` — BEFORE destruction, so the part's
        /// parent, vessel, stage and persistent id are all still readable — with the classification
        /// from `pure/blackbox/PartLoss.cs` stated IN the payload (`loss_class` + `why`) beside the raw
        /// numbers it was decided from (`since_stage_s`, `since_decouple_s`). ⛔ The class may be
        /// `unclassified`, and that is a verdict, not a gap: see PartLoss.cs's header for why a recent
        /// stage command deliberately does NOT promote a death to `commanded`.
        /// </summary>
        public const string PartLost = "part.lost";
        /// <summary>⭐ KSP's own failure channel (`onPartFailure`) — the game asserting a part failed,
        /// which is a stronger statement than `part.lost` inferring it from an absent command. Kept as a
        /// separate kind rather than a payload flag so a reader can filter for "what the game called a
        /// failure" without trusting our classifier at all.</summary>
        public const string PartFailure = "part.failure";
        /// <summary>⭐ Structural break (`onPartJointBreak`, `EventData&lt;PartJoint,float&gt;`) — the
        /// "unzip" case: the joint holding two parts together let go, carrying the break force. A joint
        /// break need not kill either part, so this is NOT a duplicate of `part.lost`; it is often the
        /// FIRST line of the sequence and the one `part.lost` alone would miss.</summary>
        public const string PartJointBreak = "part.joint_break";
        /// <summary>
        /// ⛔ The part-loss budget was exhausted and the sequence was CUT. Carries `truncated_after`
        /// (the cap) and `seen` (how many losses were actually offered), emitted EXACTLY ONCE however
        /// long the cascade runs. It is in the RECORDING and not in a log line for the same reason
        /// `crew.press_dropped` is: a recorder that loses data quietly is the S76 defect, and at a RUD
        /// the truncation is itself a measurement of how violent the cascade was.
        /// </summary>
        public const string PartLossTruncated = "part.loss_truncated";

        // ---- ⭐⭐ S235 / NTSB-2026-003 F-308: EMIT WHAT WE ALREADY OBSERVE ----------------------------
        // ⛔ THE DEFECT THESE THREE FIX WAS MINE, IN S227, AND ITS SHAPE IS WORTH KEEPING.
        // `PartLossWatch` grew three handlers whose only job was to remember a timestamp so that a LATER
        // `part.lost` could be classified. The file said so as though it were a virtue: *"Nothing below
        // writes an event."* ⛔ But `part.lost` fires only from `onPartWillDie` — and on flight 003, at
        // MET 139.28, the sixteen launch-vehicle parts **did not die. They became a new vessel.** So the
        // one channel that could have spoken was silent BY CONSTRUCTION, and the observations that would
        // have named the agent had already been made and discarded.
        //
        // ⭐ THE INVESTIGATION TURNED ON ONE `int`. `onStageActivate` hands us the stage number; the
        // handler kept a timestamp and dropped it. Every software agent able to fire that decoupler was
        // eliminated with citations, the owner eliminated the last branch himself (*"i did not press
        // space"*), and the root cause is STILL unresolved — because nothing recorded whether
        // `StageManager.ActivateNextStage()` ran at all.
        //
        // ⚠ THE RULE THIS LEAVES BEHIND, WHICH IS BIGGER THAN THESE THREE KINDS: **an observation kept
        // only as internal state is not recorded.** A handler that reduces its parameters to a `double`
        // is a channel that exists for the code and not for the reader, and the reader is the whole point
        // of a black box. If a handler is handed it, the recording should be able to say it.
        /// <summary>
        /// ⭐⭐ **`StageManager.ActivateNextStage()` RAN, AND THIS IS THE STAGE IT FIRED.** The single
        /// most load-bearing line in this vocabulary for NTSB-2026-003: its PRESENCE says a stage command
        /// was issued and by whom it could have been; its ABSENCE at a separation says the parts left
        /// **without any stage command at all**, which eliminates every staging agent at once.
        /// ⛔ Distinct from `stage.staged`, which is polled from `Vessel.currentStage` on the row tick and
        /// therefore reports that staging HAS HAPPENED. This is the COMMAND, timestamped where it was
        /// issued, and the two disagreeing is itself a finding.
        /// </summary>
        public const string StageActivateCalled = "stage.activate_called";
        /// <summary>
        /// ⭐ KSP raised `onPartDeCouple` FOR THIS PART — the game saying it was released on purpose.
        /// [[S227]] already used this as the only evidence strong enough to classify a later loss as
        /// `commanded`; S235 makes it a RECORD as well as a judgement, so an analyst can see the release
        /// even when nothing subsequently dies.
        /// </summary>
        public const string PartDecoupled = "part.decoupled";
        /// <summary>The docking-side equivalent — `onPartUndock`. A separate kind rather than a payload
        /// flag on <see cref="PartDecoupled"/>, because a docking release and a staging release are
        /// different acts by different agents and a reader should not have to filter to tell them apart.</summary>
        public const string PartUndocked = "part.undocked";
    }
}
