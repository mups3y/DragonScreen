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
        public const string RecOpen        = "rec.open";
        public const string RecClose       = "rec.close";
        public const string RecRevert      = "rec.revert_detected";
        public const string RecVesselChange = "rec.vessel_change";
        public const string RecFocusChange = "rec.focus_change";
        public const string RecWarpChange  = "rec.warp_change";
        public const string RecSceneChange = "rec.scene_change";
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

        public const string SysBusTrip     = "sys.bus_trip";
        public const string SysStringState = "sys.string_state";
        public const string SysFireStart   = "sys.fire_start";
        public const string SysFireOut     = "sys.fire_out";
        public const string SysLeakStart   = "sys.leak_start";
        public const string SysIsolate     = "sys.isolate";

        public const string FaultRaised    = "fault.raised";
        public const string FaultCleared   = "fault.cleared";
        public const string Exception      = "exception";
    }
}
