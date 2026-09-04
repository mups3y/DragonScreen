#!/usr/bin/env python
# -*- coding: utf-8 -*-
# DragonScreen - assess_flight.py  --  THE REPORT GENERATOR  (docs/BLACKBOX_RESEARCH.md §4.10, register BB3)
# =============================================================================================
# The program that reads a recording back and returns the correct and full recorded mission.
#
#   python plugin/tools/assess_flight.py                 # newest recording (BlackBox mission, else legacy CSV)
#   python plugin/tools/assess_flight.py <MissionId>     # a BlackBox mission by id, or ANY of its three files
#   python plugin/tools/assess_flight.py <file.csv>      # one legacy Recorder-B capture
#   python plugin/tools/assess_flight.py --list          # list everything readable, newest-first
#   python plugin/tools/assess_flight.py --all           # assess EVERY recording, in order
#   python plugin/tools/assess_flight.py --selftest      # synthesise a BB1/BB2 recording and assess it
#                                                        #   (add --verbose to print the report it checks)
#   ... --external                                       # ALSO sweep the KSP capture dir + the quarantine
#                                                        #   archive (opt-in: they are outside the repo, C7)
#   ... --out <file>                                     # ALSO write the report to a file (§4.10 "Output")
#
# ⛔ Reads recorded data only - NOT a physics/orbital sim (those are banned). It reports, unasked, the
# TWELVE sections of §4.10:
#   0 provenance   1 recorder health   2 physics self-check   3 ascent   4 booster
#   5 rendezvous/phasing   6 deorbit/entry/chute   7 abort + FDIR   8 control authority
#   9 crew & screens   10 event timeline   11 exceedances (FOQA)   12 verdict
# Anything it does NOT flag has been CHECKED, not skipped.
#
# ---- TWO SCHEMAS, ONE TOOL (§3.4: "COMPOSE - extend, do not replace") ----
# BB3 did not replace the nine-section Recorder-B analyser; it extended it, because everything S76
# repaired (reuniting the analyser with the recovered corpus) and S77 fixed (`is_warp()` filtering, the
# act_sat / authority metrics, the vis-viva self-check, the "read the orbit a few rows AFTER thrust
# dies" correction) is hard-won and reads the same quantities under mostly the same names. So:
#   • BLACKBOX - a BB1/BB2 mission: `<MissionId>.params.csv` + `<MissionId>.<Vessel>.params.csv`
#     (SAME id) + ONE shared `<MissionId>.events.jsonl` + one `.manifest.json` PER STREAM.
#   • LEGACY   - one Recorder-B `Crew-2_*.csv` from the recovered corpus. No manifest, no event log.
# A legacy file is loaded as a one-stream mission with an empty manifest and no events, so all twelve
# sections still run and each says plainly what its schema cannot supply, rather than printing nothing
# and letting a reader mistake an unreadable channel for a quiet one. Column-name drift between the two
# schemas is handled ONCE, in `ALIAS`, and nowhere else.
#
# ---- NOTHING IS ASSUMED THAT THE MANIFEST CAN STATE (§3.4's BREAK rows) ----
# Row period is READ, never assumed (five literal `.2`s in the gen-1 analyser; Recorder B changed to
# 0.25 s and every duration it printed would have read 20 % short). Units come from the manifest, not
# from a column-name suffix. The body comes from the manifest / the `body` column, so no reader detects
# a body from the data. Where a legacy file cannot supply one of those, the report says so.
#
# ---- WHAT A RECORDING IS, AND IS NOT (§5) ----
# A recording is EVIDENCE about a flight - the basis for a finding, a register line, a tune step. It is
# NEVER a build source (C7), and a `SIMULATED` column is evidence about OUR MODEL and nothing else.
# §0 prints the provenance of every column before a single number is read, for exactly that reason.
#
# NOTE plugin/build/assess_flight.py reads an EARLIER, gen-1 schema (ut / a_phase / x_owner) and cannot
# read either schema here; owner decision S8 keeps it for T22 and its own banner says "do not extend it
# - extend the tools/ one". Its booster deck-miss geometry is the ONLY surviving recovery coordinate
# pair, and §4.10 §4 says to PORT it here rather than edit it there: see `DECK` below.
# =============================================================================================
import csv, os, sys, glob, math, json, re

# CORPUS LOCATION (S76, 2026-09-04). The recovered corpus lives IN THE REPO - `docs/flights/` - and that is
# the FIRST place this reads, per C7 ("the ONLY source of truth is the repo"). The two external directories are
# kept as OPTIONAL fallbacks only: CAPTURE is where a NEW recording lands while KSP is running, so it stays
# useful; ARCHIVE is the historical quarantine copy. Neither is a build source - anything found there that
# matters belongs in `docs/flights/`. Both are skipped silently when absent.
REPO_FLIGHTS = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__)))),
                            "docs", "flights")
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CAPTURE = r"C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\DragonScreen_capture"
ARCHIVE = r"C:\Users\User\Desktop\quarantine\dragonscreen_flightdata"
MU, RE = 3.9860044e14, 6371000.0          # Earth (RSS/RO). vis-viva + circular speed use these.
TARGET_INC = 51.6                          # ISS reference; a free-flyer profile would differ.
PE_FLOOR_KM = 150.0                        # the rendezvous/phasing safety floor (pure/Phasing)

# ---- §B11 FLIGHT-DATA TARGETS, as data (docs/BUILD_PLAN.md §B11). [DOC] = publicly documented,
# ---- [EST] = engineering estimate to VALIDATE in-sim. §11 turns each into a rule and §3 quotes them at
# ---- the matching ascent event. The tag travels WITH the number so the report can never present an
# ---- estimate as a measurement - the same discipline §0 applies to a SIMULATED column.
B11 = {
    "maxq_pa":        (30000.0, 35000.0,  "[DOC]",     "max-Q ~30-35 kPa, ~T+1:12, F9 throttles down through it"),
    "maxq_alt_km":    (10.0,    14.0,     "[DOC]",     "max-Q altitude ~12 km"),
    "meco_alt_km":    (70.0,    90.0,     "[DOC]",     "MECO ~T+2:17, alt ~80 km"),
    "meco_mach":      (8.0,     12.0,     "[DOC]",     "MECO ~Mach 10"),
    "seco_s":         (480.0,   570.0,    "[DOC]",     "SECO-1 ~T+8:33 (in orbit ~9 min)"),
    "insert_ap_km":   (190.0,   210.0,    "[DOC/cfg]", "insertion ~190-210 km (Crew-2 cfg = 210)"),
    "insert_pe_km":   (150.0,   210.0,    "[DOC]",     "insertion circularised above the 150 km phasing floor"),
    "insert_inc_deg": (51.4,    51.9,     "[DOC/cfg]", "51.63 deg (Crew-2 cfg = -51.6316)"),
    "ascent_g":       (0.0,     4.0,      "[EST]",     "peak axial accel ~4 g near MECO and again near SECO"),
    "entry_g":        (0.0,     4.5,      "[DOC/EST]", "Dragon nominal entry decel ~4-4.5 g"),
    "crew_g":         (0.0,     7.5,      "[DOC/EST]", "generic crew capsule worst case ~7-8 g"),
    "ei_alt_km":      (118.0,   126.0,    "[DOC]",     "entry interface 122 km (400,000 ft)"),
    "ei_speed_mps":   (7400.0,  8100.0,   "[DOC]",     "entry interface ~7.8 km/s"),
    "mains_alt_km":   (1.5,     2.5,      "[DOC]",     "4 mains at ~2 km"),
    "touchdown_mps":  (0.0,     8.0,      "[DOC]",     "under >=3 mains, splash ~5-8 m/s"),
    "phasing_pe_km":  (PE_FLOOR_KM, 1e9,  "[DOC]",     "phasing periapsis floor 150 km (pure/Phasing)"),
    "kos_m":          (200.0,   200.0,    "[DOC]",     "Keep-Out Sphere ~200 m; halt at ~1 km for the Go/No-Go"),
    "contact_mps":    (0.0,     0.2,      "[DOC]",     "rate < 0.2 m/s inside 5 m; final contact ~0.1 m/s"),
    "skin_temp_frac": (0.0,     0.9,      "[EST]",     "hottest part skin temp / its max - a max-Q Overheat! was "
                                                       "invisible without this column"),
}

# ---- `pure/Alarms.cs` CabinLimits, mirrored as rules (§4.10 §11: "every CabinLimits threshold").
# ---- (caution, alarm, sense, label) - sense -1 means LOW is bad, +1 means HIGH is bad, exactly as
# ---- `Alarms.Band` decides it from whether `alarm > caution`. Mirrored, not imported: this is a Python
# ---- tool and `Alarms.cs` is C#, so the numbers are quoted with their source and §12 says if they drift.
CABIN_LIMITS = {
    "ppo2_psia":    (2.5,  2.0,  -1, "cabin ppO2 (psia)"),
    "co2_mmhg":     (4.0,  6.0,  +1, "cabin CO2 (mmHg)"),
    "cabin_psia":   (13.0, 11.0, -1, "cabin pressure (psia)"),
    "cabin_temp_c": (30.0, 35.0, +1, "cabin temperature (degC)"),
    "loop_a_c":     (45.0, 55.0, +1, "coolant loop A (degC)"),
    "loop_b_c":     (45.0, 55.0, +1, "coolant loop B (degC)"),
}
CABIN_LIMITS_SRC = "plugin/src/pure/Alarms.cs :: CabinLimits"

# ---- BOOSTER RECOVERY GEOMETRY - PORTED VERBATIM from `plugin/build/assess_flight.py:398-436`, which
# ---- §4.10 §4 names as "the only surviving recovery coordinates". Ported, not re-derived and not
# ---- re-sourced: `build/`'s own banner forbids extending it, so the numbers move HERE unchanged.
# ⛔ DECK is the physical DECK CENTRE (OCISLY group centre 32.7875/-76.6445 + the SpaceXbarge2 model
# offset of ~5.7 m), NOT the group centre / waypoint. Aiming at the group centre made an on-aim landing
# read "dead centre" while it was ~5.7 m off a 25 m-wide deck - the circular measure the owner caught.
# ⚠ PROVISIONAL, and the report says so every time it prints. OWNER RULING, 2026-09-04, recorded by S89
# (`8580c81`) and quoted there: "the droneships are placed at rough, explicitly provisional coordinates;
# the first booster is flown to wherever it naturally lands for a clean nominal descent - the trajectory
# is not fought to reach a target - and then the droneship is moved to that exact measured position."
# So a deck miss measured here is a measurement OF THE AIM POINT, not yet a verdict on the guidance.
# JRTI/ASOG are deliberately ABSENT: `docs/reference/LZ_RECOVERY_TABLE.md` holds them (PROVISIONAL, LZ1
# is NEEDS-WORK) and W25 owns the aim point. This tool invents no coordinate.
PAD  = (28.6084, -80.6043)          # LC-39A
DECK = (32.787551, -76.644507)      # OCISLY deck centre == BoosterRecovery.DroneshipEarthLat/LonDeg
DECK_HALF_ALONG_M, DECK_HALF_CROSS_M = 25.0, 12.5     # barge 50 m x 25 m -> edge at 25 m / 12.5 m

# The three sentinels Recorder A used for "no value" (§3.4 BREAK: blank is the only one). The BlackBox
# writes blank, the legacy corpus writes all three, and this tool has to read both.
BLANKS = ("", "-", "Idle", "None")


# =============================================================================================
#  OUTPUT - stdout, and optionally a file the owner can paste to the overseer (§4.10 "Output")
# =============================================================================================
_SINK = []          # extra writable streams
_QUIET = [False]    # stdout suppressed (the selftest captures into a buffer and asserts on it)
ALERTS = []         # every *** finding *** raised this mission, for §12


# The report quotes section marks and warning glyphs, and a Windows console is routinely cp1252. A report
# that CRASHES on its own punctuation is worse than one that renders a glyph as "?", so stdout is
# reconfigured to UTF-8 where the interpreter allows it and every write falls back to an ASCII
# transliteration otherwise. The `--out` file is always UTF-8 and always exact.
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass


def P(s=""):
    if not _QUIET[0]:
        try:
            print(s)
        except UnicodeEncodeError:
            print(s.encode("ascii", "replace").decode("ascii"))
    for f in _SINK:
        f.write(s + chr(10))


def alert(s):
    """A finding. Printed where it was found AND carried to the verdict - §12 is a summary, not a rerun."""
    ALERTS.append(s)
    P("  *** " + s + " ***")


def sec(t):
    P("\n" + "=" * 78 + "\n  " + t + "\n" + "=" * 78)


def sub(t):
    P("\n  ---- " + t + " " + "-" * max(0, 68 - len(t)))


# =============================================================================================
#  READING - values, rows, files
# =============================================================================================
def fnum(s):
    try:
        return float(s)
    except (TypeError, ValueError):
        return None


# ---- THE ONE PLACE COLUMN-NAME DRIFT LIVES (see the header). Legacy name -> BlackBox name. Applied
# ---- only when the legacy name is absent from the row, so a legacy file is never re-mapped.
ALIAS = {
    "att_point_deg":    "att_err_deg",
    "descent_speed_mps": "vspeed_mps",
    "rv_range_m":       "range_m",
    "azimuth_deg":      "heading_deg",
    "bank_deg":         "roll_deg",
    "boost_aoa_deg":    "aoa_deg",
    "dv_planned_mps":   "dv_planned",
    "dv_delivered_mps": "dv_delivered",
    "dv_residual_mps":  "dv_residual",
    "close_speed_mps":  "closing_mps",
    "dock_range_m":     "range_m",
}


def sval(r, c):
    """A cell as a string, honouring ALIAS. Never None - a missing column reads exactly like a blank one."""
    v = r.get(c)
    if v is None or v == "":
        a = ALIAS.get(c)
        if a is not None:
            v = r.get(a)
    return "" if v is None else v


def g(r, c):
    return fnum(sval(r, c))


# S76: a recording that was REVERTED mid-write ends with a TORN row - the stream was cut between two
# commas, so the last line carries fewer fields than the header (seen in both DS-ASC-001/002 probe files:
# 36 and 77 of 116). csv.DictReader's default `restval` is None, so every missing column came back as the
# OBJECT None, which is not the STRING "None" the blank-filters test for - so a torn tail read as a phase
# TRANSITION to None, and then crashed the string joins in sections 4-8. restval="" makes a missing trailing
# field read exactly like an inactive subsystem's blank cell, which is what it is.
def load(path):
    return list(csv.DictReader(open(path, encoding="utf-8", errors="replace"), restval=""))


def torn_rows(path, ncols):
    """Rows with fewer fields than the header (a cut-off recorder stream). Reported, never silently dropped."""
    out = []
    for i, rec in enumerate(csv.reader(open(path, encoding="utf-8", errors="replace"))):
        if i and rec and len(rec) != ncols:
            out.append((i, len(rec)))
    return out


def transitions(rows, col):
    """(row, value) at each change of a phase column, skipping idle/blank."""
    out, prev = [], None
    for r in rows:
        v = sval(r, col)
        if v != prev and v not in BLANKS:
            out.append((r, v))
            prev = v
        elif v != prev:
            prev = v
    return out


def col_active(rows, col):
    return any(sval(r, col) not in BLANKS for r in rows)


def has_col(st, col):
    """Is the column DECLARED in this stream's header (as itself or under its alias)?"""
    return col in st.hdr or ALIAS.get(col) in st.hdr


# ⭐ P0.0 (I1, C1-refined): exclude ONLY on-rails HIGH-warp rows from control-signal stats - those are the ones the
# recorder blanks (physics warp <=4x keeps control LIVE and must be kept). So the test is "warped AND the recorder
# blanked the control columns", not "warp_rate>1" (which wrongly dropped live physics-warp rows).
# BB1/BB2 additionally record `warp_rails` outright, so on a BlackBox file the test stops being an
# inference: the recorder states whether it was on rails. The blank-check is kept as the fallback for the
# legacy corpus, which has no such column.
def is_warp(r):
    rails = fnum(r.get("warp_rails"))
    if rails is not None:
        return rails >= 0.5
    w = fnum(r.get("warp_rate"))
    if w is None or w <= 1.0:
        return False                                                  # realtime
    return r.get("ctrl_tq_pitch", "") == "" and r.get("act_pitch", "") == ""   # on-rails = control blanked


# =============================================================================================
#  THE MISSION MODEL - a BlackBox mission (BB1/BB2), or a legacy CSV wearing the same shape
# =============================================================================================
class Stream(object):
    """One vessel's parameter stream, plus the manifest that decodes it."""

    def __init__(self, path, rows, manifest=None, label=None):
        self.path = path
        self.rows = rows
        self.hdr = list(rows[0].keys()) if rows else []
        self.manifest = manifest or {}
        self.label = label or os.path.basename(path)
        self.vessel = (self.manifest.get("vessel")
                       or (rows[0].get("vessel") if rows else "")
                       or os.path.basename(path).split(".")[0])
        # A legacy recorder wrote ONLY the focused vessel, so the absent manifest means focused - which
        # is a fact about that recorder, not a default.
        self.role = self.manifest.get("stream_role") or "focused"
        self.ever_focused = bool(self.manifest.get("ever_focused", True))

    @property
    def tracked(self):
        return self.role == "tracked"

    def cols(self):
        """The manifest's per-column metadata, by name. Empty for a legacy file."""
        return dict((c.get("name"), c) for c in self.manifest.get("columns", []) if c.get("name"))

    def row_period_s(self):
        """READ, never assumed (§3.4). Falls back to the MEASURED median row spacing."""
        hz = self.manifest.get("row_rate_dynamic_hz")
        if hz:
            return 1.0 / float(hz)
        ts = [g(r, "ut") for r in self.rows]
        ts = [t for t in ts if t is not None]
        if len(ts) < 3:
            ts = [g(r, "met_s") for r in self.rows]
            ts = [t for t in ts if t is not None]
        d = sorted(b - a for a, b in zip(ts, ts[1:]) if 0 < b - a < 60)
        return d[len(d) // 2] if d else None


class Mission(object):
    def __init__(self, mid, streams, events=None, kind="blackbox", events_path=None, events_bad=0,
                 manifest_errors=None):
        self.id = mid
        self.streams = streams
        self.events = events or []
        self.kind = kind
        self.events_path = events_path
        self.events_bad = events_bad             # unparseable JSONL lines (a torn tail is one of them)
        self.manifest_errors = manifest_errors or []

    @property
    def primary(self):
        for s in self.streams:
            if not s.tracked:
                return s
        return self.streams[0]

    def stream_for(self, vessel):
        for s in self.streams:
            if s.vessel == vessel:
                return s
        return self.primary

    def body(self):
        for s in self.streams:
            b = s.manifest.get("body")
            if b:
                return b
        for s in self.streams:
            for r in s.rows:
                v = sval(r, "body")
                if v:
                    return v
        return None


def load_events(path):
    """The shared events.jsonl. A TRUNCATED FINAL LINE is expected and counted, never fatal (§4.1)."""
    out, bad = [], 0
    if not path or not os.path.exists(path):
        return out, bad
    with open(path, encoding="utf-8", errors="replace") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            try:
                out.append(json.loads(line))
            except ValueError:
                bad += 1
    out.sort(key=lambda e: (e.get("ut") if isinstance(e.get("ut"), (int, float)) else 0.0,
                            e.get("seq") if isinstance(e.get("seq"), (int, float)) else 0))
    return out, bad


def _stamp(path):
    """Sort key: the YYYYmmdd_HHMMSS the recorder puts in the filename, else the file mtime.

    (mtime is useless for the repo copies - git sets it to the checkout time, so every recovered file
    looks equally "new"; the name carries the real recording time.)"""
    m = re.search(r"(\d{8})_(\d{6})", os.path.basename(path))
    return (0, m.group(1) + m.group(2)) if m else (1, "%020.0f" % os.path.getmtime(path))


def _dirs(external):
    dirs = [REPO_FLIGHTS]
    if external:
        dirs = [ARCHIVE, CAPTURE, REPO_FLIGHTS]   # repo LAST so its copy overrides a namesake
    return [d for d in dirs if os.path.isdir(d)]


def captures(external=False):
    """Every LEGACY recorder CSV. THE REPO IS THE CORPUS; the external dirs are an opt-in fallback.

    C7: the only source of truth is the repo, so `docs/flights/` is read FIRST and, by default, ALONE.
    `--external` additionally sweeps CAPTURE (where a NEW recording lands while KSP is running) and
    ARCHIVE (the historical quarantine copy). Those two are deliberately NOT automatic: whatever lives
    there is uncommitted and unreviewed, and an analysis that silently mixes it with the repo corpus is
    not reproducible from the repo. Deduped by basename, repo copy wins. Missing dirs are skipped.

    `*_geometry_dump_*.csv` is EXCLUDED: it matches `Crew-2*.csv` but is the read-only GeometryDump
    instrument's own schema (row/part_idx/part_name/...), not a recorder tick stream - feeding one to
    this script produces zeroes, not a finding. See `docs/flights/README.md`.
    ⭐ BB3: `*.params.csv` is EXCLUDED TOO. A BlackBox stream is named `Crew-2_<stamp>.params.csv` and so
    matches this same glob; read as a legacy file it would lose its manifest, its sibling stream and its
    event log - i.e. it would be assessed as a third of itself. `missions()` owns those.
    """
    by_name = {}
    for d in _dirs(external):
        for p in glob.glob(os.path.join(d, "Crew-2*.csv")):
            b = os.path.basename(p)
            if "geometry_dump" in b or b.endswith(".params.csv"):
                continue
            by_name[b] = p
    return sorted(by_name.values(), key=_stamp)


def missions(external=False, only=None):
    """Every BLACKBOX mission, newest last. Grouped by `mission_id` - never by filename or timestamp.

    §4.4's two-vessel rule is "the same mission id", and the manifest states `stream_join_on:
    ["mission_id","ut"]` outright, so this globs the MANIFESTS and groups on what they say. That is the
    fix for the old paired `Crew-2_*.csv` / `Crew-2_Probe_*.csv` streams, which could only be associated
    by guessing from timestamps.
    """
    groups, errs = {}, {}
    for d in _dirs(external):
        for mp in sorted(glob.glob(os.path.join(d, "*.manifest.json"))):
            try:
                man = json.load(open(mp, encoding="utf-8", errors="replace"))
            except ValueError as e:
                # An unparseable manifest does not hide the recording: the stream is still read, and
                # §0/§1 report the manifest as unreadable. Losing the whole mission because its sidecar
                # was cut would be the analyser making a bad flight worse.
                man, mid = {}, os.path.basename(mp).split(".")[0]
                errs.setdefault(mid, []).append("%s: %s" % (os.path.basename(mp), e))
            mid = man.get("mission_id") or os.path.basename(mp).split(".")[0]
            groups.setdefault(mid, {})[mp] = man
    out = []
    for mid, mans in groups.items():
        # `only` skips LOADING the rows of every other mission. A 19 h mission is a large file and the
        # corpus is meant to accumulate; resolving one id must not read all of them.
        if only is not None and mid != only and not any(
                os.path.basename(m.get("params_file") or "") == only or
                mid.split(".")[0] == only.split(".")[0] for m in mans.values()):
            continue
        streams, evpath = [], None
        for mp, man in sorted(mans.items()):
            d = os.path.dirname(mp)
            stem = os.path.basename(mp)[:-len(".manifest.json")]
            pf = man.get("params_file") or (stem + ".params.csv")
            pp = os.path.join(d, pf)
            rows = load(pp) if os.path.exists(pp) else []
            streams.append(Stream(pp, rows, man, label=os.path.basename(pp)))
            ev = man.get("events_file") or (mid + ".events.jsonl")
            if evpath is None and os.path.exists(os.path.join(d, ev)):
                evpath = os.path.join(d, ev)
        streams.sort(key=lambda s: (s.tracked, s.vessel))     # focused stream first
        events, bad = load_events(evpath)
        out.append(Mission(mid, streams, events, "blackbox", evpath, bad, errs.get(mid)))
    return sorted(out, key=lambda m: _stamp(m.primary.path if m.streams else m.id))


def legacy_mission(path):
    """One legacy CSV, wearing the Mission shape so all twelve sections run against it unchanged."""
    rows = load(path)
    st = Stream(path, rows, None, label=os.path.basename(path))
    st.role = "focused"
    return Mission(os.path.basename(path), [st], [], "legacy")


def resolve(arg, external=False):
    """A mission id, a manifest, a params.csv, an events.jsonl, or a legacy CSV - all reach their mission."""
    base = os.path.basename(arg)
    # Two passes: the cheap one keyed on the id, then the full scan for a path or an events file.
    ms = missions(external, only=base.split(".")[0]) or missions(external)
    for m in ms:
        if m.id == arg or m.id == base.split(".")[0]:
            return m
        for s in m.streams:
            if os.path.abspath(s.path) == os.path.abspath(arg) or os.path.basename(s.path) == base:
                return m
        if m.events_path and os.path.basename(m.events_path) == base:
            return m
    if os.path.exists(arg) and arg.lower().endswith(".csv"):
        return legacy_mission(arg)
    # `--list` prints BASENAMES, so a basename has to resolve - otherwise the tool advertises a name it
    # then refuses. Looked up in the corpus by the same rule `captures()` uses.
    for pth in captures(external):
        if os.path.basename(pth) == base:
            return legacy_mission(pth)
    return None


# =============================================================================================
#  SCHEMA + PAGE NAMES, DERIVED FROM THE TREE  (never a second copy of either table)
# =============================================================================================
_SRC_SCHEMA = os.path.join(REPO, "plugin", "src", "pure", "blackbox", "BlackBoxSchema.cs")
_SRC_PAGES = os.path.join(REPO, "plugin", "src", "pure", "FigmaUI.cs")
_COL_RE = re.compile(r'^\s*(CondCap|Cond|Unfit|Cap|C)\(\s*"([^"]+)"\s*,\s*"([^"]*)"\s*,\s*'
                     r'Tier\.(\w+)\s*,\s*"([^"]*)"'
                     r'(?:\s*,\s*"([^"]*)")?(?:\s*,\s*"([^"]*)")?')


def schema_from_source():
    """The BlackBox column table, parsed out of its own C# source.

    Used ONLY by `--selftest`, to synthesise a fixture at the CURRENT schema rather than at a copy of it
    that would rot the day a column is appended. If the parse ever stops matching the source, the
    selftest fails loudly rather than testing a stale shape - which is the whole point of deriving it.
    """
    if not os.path.exists(_SRC_SCHEMA):
        return []
    out = []
    for line in open(_SRC_SCHEMA, encoding="utf-8", errors="replace"):
        m = _COL_RE.match(line)
        if not m:
            continue
        kind, name, units, tier, fourth, fifth, sixth = m.groups()
        # Unfit(name, units, tier, SOURCE, REGISTER-LINE) - its 4th arg is the source and its 5th is the
        # line that will fill it, which is the whole point of the Unfitted declaration and must reach the
        # manifest. Cond/CondCap carry the condition last, sometimes as a named constant off this line;
        # where it is not a literal here the fixture says so rather than inventing wording.
        note = None
        if kind == "Unfit":
            note = fifth
        elif kind in ("Cond", "CondCap"):
            note = sixth or "conditional - the condition is a named constant in BlackBoxSchema.cs"
        out.append({
            "name": name, "units": units, "tier": tier,
            "fit": {"C": "Live", "Cap": "Live", "Cond": "Conditional",
                    "CondCap": "Conditional", "Unfit": "Unfitted"}[kind],
            "scope": "capsule" if kind in ("Cap", "CondCap") else "vessel",
            "provenance": "conductor" if kind == "Unfit" else (fourth if kind != "Unfit" else "conductor"),
            "source": fifth if kind != "Unfit" else fourth,
            "note": note,
        })
    return out


def page_names():
    """`UiPage` as {int: NAME}, parsed from `pure/FigmaUI.cs`. §9 prints the name, never a bare int."""
    if not os.path.exists(_SRC_PAGES):
        return {}
    txt = open(_SRC_PAGES, encoding="utf-8", errors="replace").read()
    at = txt.find("enum UiPage")
    if at < 0:
        return {}
    body = txt[at:txt.find("\n    }", at)]
    body = re.sub(r"//[^\n]*", "", body)
    return dict((int(v), k) for k, v in re.findall(r"(\w+)\s*=\s*(\d+)", body))


PAGES = {}


def page(v):
    n = fnum(v)
    if n is None:
        return "-"
    return "%s(%d)" % (PAGES.get(int(n), "?"), int(n))


# =============================================================================================
#  0. PROVENANCE  --  NEW (§4.10 #0): what wrote this, with what, and WHICH COLUMNS ARE SIMULATED
# =============================================================================================
def provenance(M):
    sec("0. PROVENANCE  (read this before any number below)")
    if M.kind == "legacy":
        P("  LEGACY Recorder-B capture: %s" % M.primary.label)
        alert("NO MANIFEST. Schema version, units, mod versions, tunables, the MechJeb cfg hash and "
              "every column's provenance are UNRECORDED for this file - §1.5's 'undecodable in six "
              "months'. Nothing below can be attributed to a build or a tune step")
        P("  Column provenance is therefore UNKNOWN per column: a SIMULATED value cannot be told from a")
        P("  measured one in this schema. Treat every number below as evidence about the run, not the vehicle.")
        pr = M.primary.row_period_s()
        P("  row period: %s (MEASURED from the data - the file does not state it)" %
          ("%.3f s" % pr if pr else "indeterminate"))
        return
    for e in M.manifest_errors or []:
        alert("UNREADABLE MANIFEST %s - the stream below is read, but undecoded" % e)
    m0 = M.primary.manifest
    P("  mission_id        %s" % M.id)
    P("  schema_version    %s        recorder_version %s" %
      (m0.get("schema_version"), m0.get("recorder_version")))
    P("  DragonScreen      %s   dll sha256 %s" %
      (m0.get("dragonscreen_asm_version"), (m0.get("dragonscreen_dll_sha256") or "")[:16]))
    P("  KSP               %s" % m0.get("ksp_version"))
    P("  body              %s" % (M.body() or "-"))
    P("  crew              %s" % (", ".join(m0.get("crew") or []) or "-"))
    P("  target            %s" % (m0.get("target_name") or "-"))
    P("  launch UT %s   opened UT %s   real-world %s" %
      (m0.get("launch_ut"), m0.get("ut_at_open"), m0.get("real_world_utc_at_open")))
    if m0.get("launch_lat_deg") is not None:
        P("  launch reference  %.5f, %.5f  (shared by every stream, so downrange means one thing)" %
          (m0.get("launch_lat_deg") or 0.0, m0.get("launch_lon_deg") or 0.0))
    P("  row rate          %s  dynamic %s Hz  quiescent %s Hz  warp wall floor %s s" %
      (m0.get("row_rate_mode"), m0.get("row_rate_dynamic_hz"), m0.get("row_rate_quiescent_hz"),
       m0.get("warp_wall_floor_s")))
    if m0.get("dynamic_phase_rule"):
        P("    dynamic when: %s" % m0.get("dynamic_phase_rule"))

    sub("streams (join on %s)" % ", ".join(m0.get("stream_join_on") or ["mission_id", "ut"]))
    for s in M.streams:
        P("  %-28s %-9s ever_focused=%-5s rows=%-7s vessel=%s" %
          (s.label, s.role, s.ever_focused, s.manifest.get("rows_written", len(s.rows)), s.vessel))
    P("  events            %s (%d line%s)" %
      (os.path.basename(M.events_path) if M.events_path else "NONE",
       len(M.events), "" if len(M.events) == 1 else "s"))
    if M.events_bad:
        P("  %d unparseable event line(s) - a truncated final line is expected after an unclean end" %
          M.events_bad)
    if len(M.streams) > 1 and not M.events_path:
        alert("a two-stream mission with NO event log - the booster hand-off cannot be narrated")

    sub("what the vehicle was flown WITH (this is what makes a one-parameter tune falsifiable)")
    P("  mechjeb_cfg_sha   %s" % (m0.get("mechjeb_cfg_sha") or "-"))
    tun = m0.get("tunables") or []
    P("  tunables          %d recorded" % len(tun))
    for t in tun[:40]:
        P("     %s" % t)
    if len(tun) > 40:
        P("     ... %d more" % (len(tun) - 40))
    mods = m0.get("mod_versions") or []
    P("  mods              %d loaded assemblies: %s%s" %
      (len(mods), ", ".join(mods[:8]), " ..." if len(mods) > 8 else ""))

    # ---- THE MARKING PASS. §14.4(e)/(f) + §5: a SIMULATED column is evidence about OUR MODEL and
    # ---- never about the vehicle, and it is surfaced HERE, before any number is read.
    sub("column provenance  (§14.4(e)/(f) marking - a SIMULATED column is evidence about the MODEL)")
    cols = M.primary.cols()
    byprov = {}
    for c in cols.values():
        byprov.setdefault(c.get("provenance") or "?", []).append(c.get("name"))
    for pv in sorted(byprov, key=lambda k: (-len(byprov[k]), k)):
        names = sorted(byprov[pv])
        P("  %-12s %3d  %s%s" % (pv, len(names), ", ".join(names[:10]),
                                 " ..." if len(names) > 10 else ""))
    simd = sorted(byprov.get("SIMULATED", []))
    if simd:
        P("")
        P("  ⚠ SIMULATED (%d): %s" % (len(simd), ", ".join(simd)))
        P("    These MOVE because the vessel moved (the 'simulate, never fake' rule) but they are our")
        P("    model's output. A finding resting on one is a finding about DragonScreen, not about Dragon.")
    unfit = sorted(n for n, c in cols.items() if c.get("fit") == "Unfitted")
    if unfit:
        P("")
        P("  UNFITTED (%d) - declared with no writer yet; blank is their honest state, and each names the" %
          len(unfit))
        P("  register line that fills it:")
        for n in unfit:
            P("     %-16s pending %s" % (n, cols[n].get("note")))
    cond = sorted(n for n, c in cols.items() if c.get("fit") == "Conditional")
    P("")
    P("  CONDITIONAL (%d): %s%s" % (len(cond), ", ".join(cond[:12]), " ..." if len(cond) > 12 else ""))
    P("  (a blank in one of those is a stated condition, not a fault - §1 reads the same declaration)")


# =============================================================================================
#  1. RECORDER HEALTH  --  EXTENDED (§4.10 #1): + `seq` gaps and the manifest's own error counts
# =============================================================================================
def recorder_health(M, st):
    sec("1. RECORDER HEALTH  [%s]" % st.label)
    rows = st.rows
    if not rows:
        alert("stream %s has NO ROWS - the recorder opened and never wrote" % st.label)
        return
    t0, t1 = g(rows[0], "met_s"), g(rows[-1], "met_s")
    per = st.row_period_s()
    P("  %d rows, %d columns, met %.1f..%.1f s (%.0f s), row period %s" %
      (len(rows), len(st.hdr), t0 or 0, t1 or 0, (t1 or 0) - (t0 or 0),
       ("%.3f s (%s)" % (per, "manifest" if st.manifest.get("row_rate_dynamic_hz") else "measured"))
       if per else "indeterminate"))

    # ---- the manifest's own tail. It knows things no column can: whether the file was CLOSED. ----
    man = st.manifest
    if man:
        closed = man.get("closed")
        P("  closed=%s  reason=%s  rows_written=%s  events_written=%s  write_errors=%s  max_rec_build_us=%s" %
          (closed, man.get("closed_reason"), man.get("rows_written"), man.get("events_written"),
           man.get("write_errors"), man.get("max_rec_build_us")))
        if not closed:
            alert("manifest says closed=false - the game ended without finalising this stream; the tail "
                  "of the recording is what is missing, and that is the part that matters")
        if (man.get("write_errors") or 0) > 0:
            alert("%s WRITE ERROR(S) recorded by the recorder itself - rows are missing from this file" %
                  man.get("write_errors"))
        mb = man.get("max_rec_build_us")
        if mb is not None and mb > 2000.0:
            alert("max_rec_build_us = %.0f us - the recorder cost more than 2 ms in a frame (§4.7 budget)"
                  % mb)
        if man.get("rows_written") is not None and abs(int(man["rows_written"]) - len(rows)) > 1:
            alert("manifest says %s rows written, the file holds %d - %d row(s) did not reach disk" %
                  (man["rows_written"], len(rows), int(man["rows_written"]) - len(rows)))

    # ---- ⭐ NEW: `seq` gaps. §2.1 calls seq "monotonic - A GAP IS A DROPPED ROW", so it is not a
    # ---- heuristic here, it is the recorder's own statement that a row is missing.
    seqs = [g(r, "seq") for r in rows]
    if any(s is not None for s in seqs):
        gaps, dup, back = [], 0, 0
        prev = None
        for i, s in enumerate(seqs):
            if s is None:
                continue
            if prev is not None:
                d = s - prev
                if d > 1:
                    gaps.append((prev, s, int(d - 1)))
                elif d == 0:
                    dup += 1
                elif d < 0:
                    back += 1
            prev = s
        lost = sum(n for _, _, n in gaps)
        if gaps:
            alert("%d seq GAP(S), %d row(s) dropped: %s" %
                  (len(gaps), lost, ", ".join("%d->%d (-%d)" % t for t in gaps[:6])))
        else:
            P("  seq: continuous over %d rows - no dropped rows" % len(rows))
        if dup:
            alert("%d DUPLICATE seq value(s) - two rows claim the same tick" % dup)
        if back:
            alert("%d seq value(s) went BACKWARDS - the stream is not one recording" % back)
    else:
        P("  seq: absent from this schema (legacy) - dropped rows are undetectable in this file")

    # ---- torn rows (S76). A revert mid-write cuts the last line between two commas. ----
    torn = torn_rows(st.path, len(st.hdr))
    if torn:
        alert("%d TRUNCATED row(s) - the stream was cut mid-line (revert / crash): %s" %
              (len(torn), ", ".join("row %d has %d of %d fields" % (i, n, len(st.hdr)) for i, n in torn[:4])))
        P("      (missing trailing fields are read as BLANK, not as data)")
    # ---- and the BlackBox's own answer to it: the last event written is `rec.stream_end`. ----
    if M.kind == "blackbox":
        mine = [e for e in M.events if e.get("vessel") == st.vessel]
        if mine and mine[-1].get("kind") == "rec.stream_end":
            P("  rec.stream_end present and last - this stream ended cleanly")
        elif M.events:
            P("  no rec.stream_end for this vessel - the recording did not end cleanly (or is still open)")

    # ---- blocks that never ran are legitimately idle - don't flag their columns ----
    blocks = {"ascent": "ascent_phase", "boost": "boost_phase", "rv": "rv_phase",
              "deorbit": "deorbit_phase", "entry": "entry_phase", "dock": "dock_phase",
              "chute": "chute_phase", "abort": "abort_mode", "mission": "mission_phase"}
    present = dict((b, c) for b, c in blocks.items() if has_col(st, c))
    dormant = [b for b, c in present.items() if not col_active(rows, c)]
    if dormant:
        P("  blocks not exercised (columns correctly idle): " + ", ".join(sorted(dormant)))

    # ---- ⭐ THE GHOST-COLUMN PASS, read from the manifest's own declaration (BB1) plus the coverage
    # ---- verdict the recorder wrote at close. A blank column means one of THREE things and only the
    # ---- manifest can say which - which is why this does not just list empty columns.
    cols = st.cols()
    if cols:
        empty = [c for c in st.hdr if not any(r.get(c) for r in rows)]
        ghosts = [c for c in empty if (cols.get(c) or {}).get("fit") == "Live"
                  and not ((cols.get(c) or {}).get("scope") == "capsule" and not st.ever_focused)]
        withheld = [c for c in empty if (cols.get(c) or {}).get("scope") == "capsule" and not st.ever_focused]
        declared = [c for c in empty if (cols.get(c) or {}).get("fit") in ("Conditional", "Unfitted")
                    and c not in withheld]
        if ghosts:
            alert("%d GHOST COLUMN(S) - declared Live and never written across %d rows: %s" %
                  (len(ghosts), len(rows), ", ".join(sorted(ghosts))))
            P("      (a column that exists and is always empty FAKES COVERAGE - S76's defect)")
        else:
            P("  no ghost columns: every Live column produced values")
        if withheld:
            P("  %d capsule column(s) WITHHELD, not missing - this stream's vessel never held the camera,"
              % len(withheld))
            P("      so the Dragon's singletons were not copied onto it (BB2 Scope.Capsule). NOT a fault.")
        if declared:
            P("  %d blank column(s) whose blankness is DECLARED (conditional/unfitted): %s%s" %
              (len(declared), ", ".join(sorted(declared)[:10]), " ..." if len(declared) > 10 else ""))
        leaks = [c for c in st.hdr if (cols.get(c) or {}).get("scope") == "capsule"
                 and not st.ever_focused and any(r.get(c) for r in rows)]
        if leaks:
            alert("%d CAPSULE LEAK(S) on a never-focused stream - the capsule's state is filed under "
                  "vessel '%s': %s" % (len(leaks), st.vessel, ", ".join(sorted(leaks))))
    else:
        empty = [c for c in st.hdr if not any(r.get(c) for r in rows)]
        if empty:
            P("  %d empty column(s) (no manifest, so 'never sampled' cannot be told from 'never fitted'): %s%s"
              % (len(empty), ", ".join(sorted(empty)[:10]), " ..." if len(empty) > 10 else ""))

    # ---- the recorder's coverage verdict, if it wrote one ----
    cov = st.manifest.get("coverage")
    if cov:
        defects = [c for c in cov if c.get("defect")]
        P("  recorder's own coverage pass: %d finding(s), %d defect(s)" % (len(cov), len(defects)))
        for c in defects[:10]:
            alert("coverage: %s [%s] - %s" % (c.get("column"), c.get("kind"), c.get("declared")))
    elif st.manifest:
        P("  recorder's own coverage pass: clean (every Live column produced values, no Unfitted one did)")

    # ---- constant / NaN / empty numeric columns over the WHOLE file (informational) ----
    flags = []
    for c in st.hdr:
        vals = [r.get(c, "") for r in rows]
        ne = [v for v in vals if v not in ("",)]
        nums = [fnum(v) for v in ne if fnum(v) is not None]
        if nums and len(nums) == len(ne) and any(math.isnan(x) or math.isinf(x) for x in nums):
            flags.append(c)
    if flags:
        alert("%d numeric column(s) with NaN/Inf (§4.6 says blank, never a plausible number): %s" %
              (len(flags), ", ".join(flags)))
    else:
        P("  no NaN/Inf in any numeric column")

    # ---- ⭐ NEW (S76's fourth defect): warp rows must be MARKED, not silently mixed in. ----
    nwarp = sum(1 for r in rows if is_warp(r))
    if nwarp:
        P("  %d of %d rows are ON-RAILS WARP - their control columns are blanked by the recorder and are"
          % (nwarp, len(rows)))
        P("      excluded from every control statistic below (§4.6, and S77's is_warp fix)")


# =============================================================================================
#  2. PHYSICS SELF-CHECK  --  EXTENDED (§4.10 #2): + thrust/mass vs accel_g, + mass vs propellant
# =============================================================================================
def physics(M, st):
    sec("2. PHYSICS SELF-CHECK (does the file agree with itself)  [%s]" % st.label)
    rows = st.rows
    body = M.body()
    if body and body.lower() not in ("earth", "kerbin"):
        P("  body = %s: the vis-viva and circular-speed checks below use Earth's mu/R and are SKIPPED." % body)
    P("  body: %s (read from %s - no reader detects the body from the data)" %
      (body or "UNSTATED", "the manifest" if st.manifest.get("body") else
       ("the `body` column" if body else "nowhere: this schema records none")))

    errs, prev = [], None
    for r in rows:
        a, t, vs = g(r, "alt_m"), g(r, "met_s"), g(r, "vspeed_mps")
        if None in (a, t, vs):
            continue
        if prev and 0.05 < t - prev[1] < 1.2 and abs(vs) > 1:
            errs.append(abs((a - prev[0]) / (t - prev[1]) - vs) / max(abs(vs), 1))
        prev = (a, t)
    if errs:
        errs.sort()
        P("  vspeed vs d(alt)/dt    median %.3f  p95 %.3f  (n=%d)" %
          (errs[len(errs) // 2], errs[int(len(errs) * .95)], len(errs)))
        if errs[len(errs) // 2] > 0.2:
            alert("vspeed disagrees with d(alt)/dt by >20% at the median - the file does not agree with itself")

    if not body or body.lower() in ("earth", "kerbin"):
        bad = n = 0
        for r in rows[::max(1, len(rows) // 400)]:
            ap, pe, v, alt = g(r, "ap_km"), g(r, "pe_km"), g(r, "speed_mps"), g(r, "alt_m")
            if None in (ap, pe, v, alt) or pe < 0 or v < 100:
                continue
            sma = RE + (ap + pe) * 500.0
            term = MU * (2.0 / (RE + alt) - 1.0 / sma)
            if term <= 0:
                continue
            n += 1
            if abs(math.sqrt(term) - v) / v > 0.03:
                bad += 1
        if n:
            P("  orbital speed vs vis-viva   %d of %d samples off by >3%%" % (bad, n))
            if bad > n * 0.1:
                alert("vis-viva disagrees with the recorded speed on %d of %d samples" % (bad, n))

    # ---- ⭐ NEW: thrust_n / mass_kg vs accel_g. The two columns and the acceleration they imply must
    # ---- agree in FREE FLIGHT; they cannot in the atmosphere (drag) or on the pad (the ground), so
    # ---- the check is restricted to where it is decidable and says so rather than flagging physics.
    samples = []
    for r in rows:
        th, mk, ag, dens = g(r, "thrust_n"), g(r, "mass_kg"), g(r, "accel_g"), g(r, "atm_density")
        alt = g(r, "alt_m")
        if None in (th, mk, ag) or mk <= 0 or th < 1000.0:
            continue
        if (dens is not None and dens > 1e-4) or (dens is None and alt is not None and alt < 60000.0):
            continue                                        # atmosphere: drag is not in the ledger
        samples.append(abs((th / mk / 9.80665) - ag) / max(ag, 1e-3))
    if samples:
        samples.sort()
        med = samples[len(samples) // 2]
        P("  thrust/mass vs accel_g      median %.3f  p95 %.3f  (n=%d, vacuum thrusting rows only)" %
          (med, samples[int(len(samples) * .95)], len(samples)))
        if med > 0.15:
            alert("thrust_n/mass_kg implies an acceleration %.0f%% away from accel_g in vacuum - one of "
                  "the three columns is wrong" % (med * 100))
    else:
        P("  thrust/mass vs accel_g      no vacuum thrusting rows to check")

    # ---- ⭐ NEW: mass vs integrated propellant. There is no mass-flow column and no Isp in the schema,
    # ---- so this checks what the recorded columns CAN decide: mass must never RISE, and a mass drop
    # ---- must be accompanied by a propellant-fraction drop. Stated, rather than a number invented to
    # ---- fill the row §4.10 asked for.
    masses = [(g(r, "met_s"), g(r, "mass_kg")) for r in rows]
    masses = [(t, m) for t, m in masses if None not in (t, m)]
    if masses:
        P("  mass_kg  %.0f -> %.0f  (%.0f burned)" % (masses[0][1], masses[-1][1],
                                                      masses[0][1] - masses[-1][1]))
        rises = [(t, b - a) for (_, a), (t, b) in zip(masses, masses[1:]) if b - a > max(1.0, a * 1e-4)]
        staged = set()
        for e in M.events:
            if e.get("kind") == "stage.staged":
                staged.add(round(e.get("met_s") or -1e9, 0))
        unexplained = [(t, d) for t, d in rises if round(t, 0) not in staged]
        if unexplained:
            P("  mass ROSE on %d sample(s) (%d of them not at a staging event) - largest +%.0f kg at met %.1f"
              % (len(rises), len(unexplained), max(d for _, d in unexplained),
                 max(unexplained, key=lambda x: x[1])[0]))
            alert("recorded mass increased in flight with no staging event to explain it")
        fr = [c for c in ("mmh_frac", "nto_frac", "ec_frac") if has_col(st, c)]
        if fr:
            drops = []
            for c in fr:
                vals = [g(r, c) for r in rows]
                vals = [v for v in vals if v is not None]
                if vals:
                    drops.append("%s %.3f->%.3f" % (c, vals[0], vals[-1]))
            P("  propellant fractions: %s" % ("  ".join(drops) if drops else "recorded but never filled"))
            if masses[0][1] - masses[-1][1] > 100.0 and drops and all(
                    abs(float(d.split("->")[0].split()[-1]) - float(d.split("->")[1])) < 1e-6 for d in drops):
                alert("mass fell by %.0f kg while every propellant fraction stayed put - the ledger does "
                      "not close" % (masses[0][1] - masses[-1][1]))
        else:
            P("  propellant fractions: not in this schema - the mass ledger cannot be closed from this file")

    accs = [g(r, "accel_g") for r in rows if g(r, "accel_g") is not None]
    if accs:
        P("  accel_g  max %.2f  (crew limit ~4.5 g)" % max(accs))


# =============================================================================================
#  3. ASCENT  --  EXTENDED (§4.10 #3): driven off `events.jsonl`, not off phase-column transitions
# =============================================================================================
def _near(st, ut=None, met=None):
    """The row closest to an instant. Events carry sub-frame timestamps; rows do not (§2.9)."""
    best, bd = None, None
    for r in st.rows:
        t = g(r, "ut") if ut is not None else g(r, "met_s")
        if t is None:
            continue
        d = abs(t - (ut if ut is not None else met))
        if bd is None or d < bd:
            best, bd = r, d
    return best


def _ctx(r):
    if r is None:
        return "(no row)"
    return ("alt %6.1fkm  spd %7.1f  M %5.2f  q %7.0f  g %5.2f  thr %4.2f  ap %7.1f pe %8.1f" %
            ((g(r, "alt_m") or 0) / 1000.0, g(r, "speed_mps") or g(r, "srf_speed_mps") or 0,
             g(r, "mach") or 0, g(r, "q_pa") or 0, g(r, "accel_g") or 0, g(r, "throttle") or 0,
             g(r, "ap_km") or 0, g(r, "pe_km") or 0))


def _vs(name, v, unit=""):
    """One measurement against its §B11 target, with the [DOC]/[EST] tag. Returns True if in band."""
    lo, hi, tag, why = B11[name]
    ok = v is not None and lo <= v <= hi
    P("     %-14s %10s   target %s%s  %s   %s" %
      (name, ("%.2f" % v if v is not None else "-") + unit,
       "%.1f-%.1f" % (lo, hi) if hi < 1e8 else ">=%.1f" % lo, unit, tag,
       "OK" if ok else ("*** OUT OF BAND ***" if v is not None else "not recorded")))
    if v is not None and not ok:
        ALERTS.append("§B11 %s = %.2f%s outside %.1f-%.1f %s (%s)" % (name, v, unit, lo, hi, tag, why))
    return ok


# ---- BB5: a Tier.R2/R3 column (alt_m, pe_km, ...) is written on ITS OWN period and blank on every
# ---- other row by design (BlackBoxRate.cs's decimation ladder - see its header comment). `g()` is a
# ---- raw single-row read, so it comes back None on a row the tier hasn't reached yet. `_vs` already
# ---- prints "not recorded" and raises no alert for a None - the bug was every call site upstream of it
# ---- doing `(g(...) or 0)` before `_vs` ever saw the blank, turning "not measured this row" into a
# ---- confident, fabricated 0.00 (or -9999 for a sentinel default) that then fails a §B11 band check.
# ---- `gkm` is the fix, applied at every such call site: convert units WITHOUT manufacturing a value.
def gkm(v):
    """metres -> km, preserving None. `(None or 0) / 1000.0 == 0.0`; `gkm(None) is None`."""
    return v / 1000.0 if v is not None else None


# ⛔ BB10: `mission_phase`/`phase_classified` are ALSO Tier.R2 (BlackBoxSchema.cs:379-380), blank on
# exactly the same non-R2 rows `alt_m` is - a raw `sval(r, col) == "ASCENT"` equality read is blind to
# that decimation and silently drops a genuinely-ascending row ~4 times in 5 during a 10 Hz dynamic
# phase. §4.6's own documented contract for a decimated column is forward-fill ("blank on every other
# row, and the manifest's period_s tells a reader exactly how far forward to fill") - so the fix is to
# carry the last KNOWN classification forward, never to guess one for a row that was never classified.
# A row before the column's FIRST fill stays "" (BB5's lesson, one level up: a blank staying blank beats
# a blank becoming a confident value) - it is never back-filled.
def _ffill(rows, col):
    """The column's value at each row, forward-filled across Tier.R2 decimation gaps. 1:1 with `rows`."""
    out = []
    last = ""
    for r in rows:
        v = sval(r, col)
        if v not in BLANKS:
            last = v
        out.append(last)
    return out


def ascent(M, st):
    sec("3. ASCENT (event-by-event, against §B11's targets)  [%s]" % st.label)
    ev = [e for e in M.events if e.get("vessel") == st.vessel] or M.events
    kinds = ("flight.liftoff", "flight.maxq", "stage.staged", "stage.engine_ignite",
             "stage.engine_shutdown", "stage.engine_flameout", "phase.transition")
    asc_ev = [e for e in ev if e.get("kind") in kinds]
    tr = transitions(st.rows, "ascent_phase") if has_col(st, "ascent_phase") else []

    if asc_ev:
        P("  from the event log (sub-frame timestamps, §2.9 - not quantised to the row period):")
        for e in asc_ev:
            p = e.get("p") or {}
            det = " ".join("%s=%s" % (k, v) for k, v in p.items() if k not in ("alt_m",))
            P("  met %8.2f  %-22s %s" % (e.get("met_s") or 0, e.get("kind"), det[:78]))
            P("              %s" % _ctx(_near(st, ut=e.get("ut"))))
    elif tr:
        P("  no event log - falling back to `ascent_phase` transitions (legacy schema):")
        for r, p in tr:
            P("  met %7.1f  %-14s %s  pitch %5.1f az %5.1f  ptErr %5.1f" %
              (g(r, "met_s") or 0, p, _ctx(r), g(r, "pitch_deg") or 0, g(r, "azimuth_deg") or 0,
               g(r, "att_point_deg") or -1))
    else:
        P("  no ascent in this recording (no ascent events, no ascent phase column active)")
        return None

    # ---- the ascent rows, however this schema marks them ----
    if has_col(st, "ascent_phase") and col_active(st.rows, "ascent_phase"):
        asc = [r for r in st.rows if sval(r, "ascent_phase") not in BLANKS]
    else:
        # BB10: forward-filled, not a raw per-row equality read - see `_ffill`'s header above.
        mp_ff = _ffill(st.rows, "mission_phase")
        pc_ff = _ffill(st.rows, "phase_classified")
        asc = [r for r, mp, pc in zip(st.rows, mp_ff, pc_ff) if mp == "ASCENT" or pc == "ASCENT"]
    if not asc:
        asc = st.rows

    sub("ascent extremes vs §B11")
    qmax = max([g(r, "q_pa_peak") or g(r, "q_pa") or 0 for r in asc] or [0])
    gmax = max([g(r, "accel_g_peak") or g(r, "accel_g") or 0 for r in asc] or [0])
    qrow = max(asc, key=lambda r: (g(r, "q_pa_peak") or g(r, "q_pa") or 0))
    mq = next((e for e in ev if e.get("kind") == "flight.maxq"), None)
    qalt = (mq.get("p") or {}).get("alt_m") if mq else g(qrow, "alt_m")
    _vs("maxq_pa", qmax, " Pa")
    _vs("maxq_alt_km", gkm(qalt), " km")
    _vs("ascent_g", gmax, " g")

    # MECO: the engine-shutdown event if there is one, else the biggest thrust drop.
    meco = next((e for e in ev if e.get("kind") == "stage.engine_shutdown"), None)
    mrow = _near(st, ut=meco.get("ut")) if meco else None
    if mrow is None:
        best = None
        for a, b in zip(asc, asc[1:]):
            d = (g(a, "thrust_n") or 0) - (g(b, "thrust_n") or 0)
            if d > 0 and (best is None or d > best[0]):
                best = (d, b)
        mrow = best[1] if best else None
    if mrow is not None:
        P("     MECO at met %.1f s" % (g(mrow, "met_s") or 0))
        _vs("meco_alt_km", gkm(g(mrow, "alt_m")), " km")
        _vs("meco_mach", g(mrow, "mach"), "")

    # ⛔ SECO orbit = the SETTLED orbit AFTER the engine cuts - NOT the last ascent-phase row (which is
    # ~0.3 s BEFORE cutoff, with pe still rising fast: it read a false 200x160 when the real insertion was
    # 200x197). Find the last ascent row, scan forward to where thrust dies, read the orbit past that.
    idx = {id(r): i for i, r in enumerate(st.rows)}
    lastIdx = max(idx[id(r)] for r in asc)
    last = st.rows[lastIdx]
    for j in range(lastIdx, min(lastIdx + 80, len(st.rows))):
        if (g(st.rows[j], "thrust_n") or 0) < 1000.0:
            last = st.rows[min(j + 3, len(st.rows) - 1)]
            break
    ap, pe, inc = g(last, "ap_km"), g(last, "pe_km"), g(last, "inc_deg")
    sub("SECO / insertion  (the orbit read AFTER thrust died, never at the last burn row)")
    P("     met %.1f s   raan %.1f" % (g(last, "met_s") or 0, g(last, "raan_deg") or 0))
    _vs("seco_s", g(last, "met_s"), " s")
    _vs("insert_ap_km", ap, " km")
    _vs("insert_pe_km", pe, " km")
    _vs("insert_inc_deg", inc, " deg")
    # ⛔ BB5: pe_km is Tier.R2 - blank on most rows by design (BlackBoxRate.cs). `last` is picked by
    # thrust/time, not by which rows the R2 tier actually filled, so pe can legitimately be None here.
    # The old code defaulted a None pe to the sentinel -9999, which reads as "> 100 is False" and prints
    # a confident, fabricated "*** SUBORBITAL ***" for a value that was simply never sampled on this row.
    # A blank pe means the reached-orbit call cannot be made - it is refused, not guessed at -9999.
    if pe is None:
        orbit = None
        P("  --> ORBIT STATUS NOT DETERMINED (pe_km not recorded on the settled-orbit row)")
    else:
        orbit = pe > 100
        P("  --> %s" % ("REACHED ORBIT" if orbit else "*** SUBORBITAL (pe <= 100 km) ***"))
        if not orbit:
            ALERTS.append("SUBORBITAL: insertion pe %.1f km" % pe)
    return {"ap": ap, "pe": pe, "inc": inc, "orbit": orbit, "qmax": qmax, "gmax": gmax}


# =============================================================================================
#  4. BOOSTER (§B16)  --  EXTENDED (§4.10 #4): + the ported deck-miss geometry
# =============================================================================================
def _bearing_dist(a, c):
    """Great-circle bearing (deg) and distance (km). Ported with the geometry it serves."""
    p1, p2 = math.radians(a[0]), math.radians(c[0])
    dl = math.radians(c[1] - a[1])
    x = math.sin(dl) * math.cos(p2)
    y = math.cos(p1) * math.sin(p2) - math.sin(p1) * math.cos(p2) * math.cos(dl)
    h = math.sin((p2 - p1) / 2) ** 2 + math.cos(p1) * math.cos(p2) * math.sin(dl / 2) ** 2
    return math.degrees(math.atan2(x, y)) % 360, 6371.0 * 2 * math.asin(math.sqrt(h))


def booster(M, st):
    sec("4. BOOSTER (§B16)  [%s]" % st.label)
    rows = st.rows
    if not has_col(st, "boost_phase") or not col_active(rows, "boost_phase"):
        P("  no booster leg in this stream (%s)" %
          ("column absent from this schema" if not has_col(st, "boost_phase") else "column idle throughout"))
        if M.kind == "blackbox" and len(M.streams) == 1:
            P("  and this mission has ONE stream - if a booster flew, it flew unrecorded (BB2's whole point)")
        return
    tr = transitions(rows, "boost_phase")
    P("  phase timeline:")
    for r, p in tr:
        P("  met %7.1f  %-14s alt %6.1fkm  vspd %7.1f  thr %4.2f  aoa %6s  db p/y/r %s/%s/%s @ %s deg" %
          (g(r, "met_s") or 0, p, (g(r, "alt_m") or 0) / 1000.0, g(r, "descent_speed_mps") or 0,
           g(r, "boost_throttle") or 0, sval(r, "boost_aoa_deg") or "-",
           sval(r, "boost_db_pitch") or "-", sval(r, "boost_db_yaw") or "-",
           sval(r, "boost_db_roll") or "-", sval(r, "boost_db_deg") or "-"))

    b = [r for r in rows if sval(r, "boost_phase") not in BLANKS]
    sub("per-phase steering authority (the observability block W24 exposed for this line)")
    per = st.row_period_s() or 0.1
    ph = {}
    for r in b:
        ph.setdefault(sval(r, "boost_phase"), []).append(r)
    P("  phase          secs | steer p/y/r max |a| | sat%  | uncommanded rows | deadbanded p/y/r")
    for k, v in ph.items():
        def mx(c):
            vals = [abs(g(r, c) or 0) for r in v]
            return max(vals) if vals else 0.0
        sat = 100.0 * sum(1 for r in v if max(abs(g(r, "boost_steer_pitch") or 0),
                                              abs(g(r, "boost_steer_yaw") or 0),
                                              abs(g(r, "boost_steer_roll") or 0)) >= 0.99) / len(v)
        unc = sum(1 for r in v if (g(r, "boost_uncommanded") or 0) >= 0.5)
        dbs = tuple(sum(1 for r in v if (g(r, c) or 0) >= 0.5)
                    for c in ("boost_db_pitch", "boost_db_yaw", "boost_db_roll"))
        P("  %-14s %5.0f | %5.2f %5.2f %5.2f  | %5.1f | %6d of %-6d | %d/%d/%d" %
          (k, len(v) * per, mx("boost_steer_pitch"), mx("boost_steer_yaw"), mx("boost_steer_roll"),
           sat, unc, len(v), dbs[0], dbs[1], dbs[2]))
        if unc:
            ALERTS.append("booster attitude UNCOMMANDED on %d row(s) in %s - the axes were not held" % (unc, k))

    # ---- ACCURACY: touchdown vs the DECK CENTRE, split into along-track (downrange) + cross-track.
    # ---- PORTED from plugin/build/assess_flight.py:398-436 per §4.10 §4.
    last = next((r for r in reversed(b) if g(r, "lat_deg") not in (None, 0.0)), None)
    sub("ACCURACY vs the recovery aim point")
    if last is None:
        P("  no lat/lon on any booster row - the landing point is not in this recording")
        return
    land = (g(last, "lat_deg"), g(last, "lon_deg"))
    br_l, d_l = _bearing_dist(PAD, land)
    br_b, _ = _bearing_dist(PAD, DECK)
    _, miss = _bearing_dist(land, DECK)
    dn = math.radians(land[0] - DECK[0]) * 6371.0
    de = math.radians(land[1] - DECK[1]) * 6371.0 * math.cos(math.radians((land[0] + DECK[0]) / 2))
    th = math.radians(br_b)
    along = (dn * math.cos(th) + de * math.sin(th)) * 1000.0      # + = past the deck (long)
    cross = (-dn * math.sin(th) + de * math.cos(th)) * 1000.0     # + = right of the track
    miss_m = miss * 1000.0
    on_deck = abs(along) <= DECK_HALF_ALONG_M and abs(cross) <= DECK_HALF_CROSS_M
    P("  touchdown   %.5f,%.5f  = %3.0f km / %.1f deg from the pad (%.5f,%.5f)" %
      (land[0], land[1], d_l, br_l, PAD[0], PAD[1]))
    P("  DECK CENTRE %.5f,%.5f  (the physical barge deck = the guidance aim)" % DECK)
    P("  MISS vs DECK CENTRE = %.1f m   (downrange %+.1f m, cross %+.1f m)   %s" %
      (miss_m, along, cross, "ON DECK" if on_deck else "OFF DECK"))
    P("  (deck 50 m x 25 m: edge at %.0f m downrange / %.1f m cross)" % (DECK_HALF_ALONG_M, DECK_HALF_CROSS_M))
    P("  ⚠ PROVISIONAL AIM POINT. Owner ruling 2026-09-04 (S89): the droneships sit at rough, explicitly")
    P("    provisional coordinates; the first booster is flown to wherever it naturally lands and the")
    P("    droneship is then MOVED to that measured position. So this miss measures the AIM POINT, and")
    P("    the number to carry forward is the touchdown lat/lon above, not the miss.")
    if not on_deck:
        ALERTS.append("booster touchdown %.0f m off the provisional deck centre (downrange %+.0f m, "
                      "cross %+.0f m) - see the aim-point caveat in §4" % (miss_m, along, cross))


# =============================================================================================
#  5. RENDEZVOUS / PHASING  --  KEEP (§4.10 #5), reading whichever schema is present
# =============================================================================================
def rendezvous(M, st):
    sec("5. RENDEZVOUS / PHASING  (the self-deorbit check)  [%s]" % st.label)
    rows = st.rows
    tr = transitions(rows, "rv_phase") if has_col(st, "rv_phase") else []
    if tr:
        for r, p in tr:
            P("  met %7.1f  %-12s range %9.1f km  burn_dv %6.2f  ap %6.1f pe %8.1f  ptErr %5.1f" %
              (g(r, "met_s") or 0, p, (g(r, "rv_range_m") or 0) / 1000.0, g(r, "rv_burn_dv") or 0,
               g(r, "ap_km") or 0, g(r, "pe_km") or 0, g(r, "att_point_deg") or -1))
        rv = [r for r in rows if sval(r, "rv_phase") not in BLANKS]
    else:
        # The BlackBox has no rv_phase; the phasing leg is the PHASING/APPROACH/DOCKED mission phase,
        # and the geometry is `range_m` + `closing_mps` (Conditional on a target being selected).
        rv = [r for r in rows if sval(r, "mission_phase") in ("PHASING", "APPROACH", "DOCKED")
              or sval(r, "phase_classified") in ("PHASING", "APPROACH", "DOCKED")]
        if not rv:
            P("  no rendezvous/phasing in this recording")
            return
        P("  phase timeline (from mission_phase - this schema has no rv_phase column):")
        for r, p in transitions(rv, "mission_phase") or transitions(rv, "phase_classified"):
            P("  met %7.1f  %-12s range %9s  closing %8s  ap %6.1f pe %8.1f" %
              (g(r, "met_s") or 0, p,
               ("%.1f km" % ((g(r, "range_m") or 0) / 1000.0)) if g(r, "range_m") is not None else "-",
               ("%.3f m/s" % g(r, "closing_mps")) if g(r, "closing_mps") is not None else "-",
               g(r, "ap_km") or 0, g(r, "pe_km") or 0))

    pes = [g(r, "pe_km") for r in rv if g(r, "pe_km") is not None]
    aps = [g(r, "ap_km") for r in rv if g(r, "ap_km") is not None]
    dvs = [g(r, "rv_burn_dv") for r in rv if g(r, "rv_burn_dv") is not None]
    if pes:
        minpe = min(pes)
        P("  --- phasing pe: min %.1f km (floor %.0f)  ap: %.1f..%.1f km  max burn_dv %.2f m/s ---" %
          (minpe, PE_FLOOR_KM, min(aps), max(aps), max(dvs) if dvs else 0))
        if minpe < PE_FLOOR_KM:
            alert("SELF-DEORBIT / FLOOR BREACH: pe dropped to %.1f km (< %.0f) DURING PHASING" %
                  (minpe, PE_FLOOR_KM))
        else:
            P("  --> pe held above the floor the whole phasing leg (no self-deorbit)")

    # ---- the §B11 intercept ladder + the contact-rate gate, where the range channel exists ----
    rng = [(g(r, "met_s"), g(r, "range_m"), g(r, "closing_mps")) for r in rv]
    rng = [t for t in rng if t[1] is not None]
    if rng:
        sub("§B11 approach ladder (4 km -> 1 km -> 220 m -> 20 m; KOS ~200 m)")
        for gate in (4000.0, 1000.0, 220.0, 200.0, 20.0, 5.0):
            hit = next((t for t in rng if t[1] <= gate), None)
            if hit:
                P("     %6.0f m reached at met %8.1f   closing %s m/s" %
                  (gate, hit[0] or 0, "%.3f" % hit[2] if hit[2] is not None else "-"))
        fast = [t for t in rng if t[1] is not None and t[1] <= 5.0 and t[2] is not None
                and abs(t[2]) > B11["contact_mps"][1]]
        if fast:
            alert("closing rate exceeded %.1f m/s inside 5 m on %d sample(s) - max %.3f m/s (§B11 [DOC])"
                  % (B11["contact_mps"][1], len(fast), max(abs(t[2]) for t in fast)))
        elif any(t[1] <= 5.0 for t in rng):
            P("     inside 5 m the closing rate stayed under %.1f m/s (§B11 [DOC])" % B11["contact_mps"][1])
    elif has_col(st, "range_m"):
        P("  range_m is declared but blank throughout - Conditional on a target being selected (§0)")


# =============================================================================================
#  6. DEORBIT / ENTRY / CHUTE  --  KEEP (§4.10 #6)
# =============================================================================================
def return_entry(M, st):
    sec("6. DEORBIT / ENTRY / CHUTE  [%s]" % st.label)
    rows = st.rows
    any_r = False
    for col, name in (("dep_phase", "DEPART"), ("deorbit_phase", "DEORBIT"),
                      ("entry_phase", "ENTRY"), ("chute_phase", "CHUTE")):
        if not has_col(st, col):
            continue
        tr = transitions(rows, col)
        if not tr:
            continue
        any_r = True
        for r, p in tr:
            P("  met %7.1f  %-8s %-12s alt %6.1fkm  ap %6.1f pe %8.1f  bank %6.1f  drogue=%s main=%s" %
              (g(r, "met_s") or 0, name, p, (g(r, "alt_m") or 0) / 1000.0, g(r, "ap_km") or 0,
               g(r, "pe_km") or 0, g(r, "bank_deg") or 0, sval(r, "drogue") or "-", sval(r, "main") or "-"))
    # The BlackBox marks the return through mission_phase + the flight.* events.
    rev = [e for e in M.events if e.get("kind") in
           ("flight.drogue_deploy", "flight.main_deploy", "flight.splashdown", "flight.touchdown")
           and e.get("vessel") == st.vessel]
    ret_rows = [r for r in rows if sval(r, "mission_phase") in ("ENTRY", "DROGUES", "MAINS", "SPLASHDOWN", "LANDED")
                or sval(r, "phase_classified") in ("ENTRY", "DROGUES", "MAINS", "SPLASHDOWN", "LANDED")]
    if rev or ret_rows:
        any_r = True
        for e in rev:
            p = e.get("p") or {}
            P("  met %8.2f  %-22s %s" % (e.get("met_s") or 0, e.get("kind"),
                                         " ".join("%s=%.2f" % (k, v) for k, v in p.items()
                                                  if isinstance(v, (int, float)))))
            P("              %s" % _ctx(_near(st, ut=e.get("ut"))))
    if not any_r:
        P("  no deorbit/entry/chute in this recording")
        return

    # ---- the §B11 return gates ----
    sub("§B11 return gates")
    ei = next((r for r in (ret_rows or [r for r in rows if (g(r, "vspeed_mps") or 0) < 0])
               if (g(r, "alt_m") or 1e9) < 122000.0 and (g(r, "speed_mps") or 0) > 5000.0
               and (g(r, "vspeed_mps") or 0) < 0), None)
    if ei is not None:
        P("     entry interface at met %.1f" % (g(ei, "met_s") or 0))
        _vs("ei_alt_km", gkm(g(ei, "alt_m")), " km")
        _vs("ei_speed_mps", g(ei, "speed_mps"), " m/s")
    mains = next((e for e in rev if e.get("kind") == "flight.main_deploy"), None)
    if mains:
        _vs("mains_alt_km", gkm((mains.get("p") or {}).get("alt_radar_m")), " km")
    if ret_rows:
        gs = [g(r, "accel_g") for r in ret_rows if g(r, "accel_g") is not None]
        if gs:
            _vs("entry_g", max(gs), " g")

    # ---- dv ledger + final descent ----
    for lbl in ("dv_planned_mps", "dv_delivered_mps", "dv_residual_mps"):
        vals = [g(r, lbl) for r in rows if g(r, lbl) is not None]
        if vals:
            P("  %-18s last %.2f  max %.2f" % (lbl, vals[-1], max(vals)))
    tail = rows[-1]
    P("  final: alt %.2f km  srf_speed %.1f m/s  vspeed %.1f m/s" %
      ((g(tail, "alt_m") or 0) / 1000.0, g(tail, "srf_speed_mps") or 0, g(tail, "vspeed_mps") or 0))
    down = next((e for e in rev if e.get("kind") in ("flight.splashdown", "flight.touchdown")), None)
    if down:
        vspd = (down.get("p") or {}).get("vspeed_mps")
        _vs("touchdown_mps", abs(vspd) if vspd is not None else None, " m/s")


# =============================================================================================
#  7. ABORT + FDIR  --  KEEP (§4.10 #7)
# =============================================================================================
def abort(M, st):
    sec("7. ABORT + FDIR  [%s]" % st.label)
    rows = st.rows
    tr = transitions(rows, "abort_mode") if has_col(st, "abort_mode") else []
    faults = [(r, sval(r, "fdir_fault")) for r in rows if sval(r, "fdir_fault") not in BLANKS]
    fev = [e for e in M.events if e.get("kind") in ("fault.raised", "fault.cleared", "exception")]
    if not tr and not faults and not fev:
        P("  no abort / no FDIR fault / no fault event in this recording")
        if has_col(st, "abort_mode") and M.kind == "blackbox":
            P("  (abort_mode and fdir_* are the idle seams' constants - recording the constant IS the proof")
            P("   the seam was idle, §2.5; they become live one controller at a time per §B12.5)")
        return
    if faults:
        seen = set()
        for r, f in faults:
            key = (f, sval(r, "fdir_recovery"), sval(r, "fdir_abort"))
            if key in seen:
                continue
            seen.add(key)
            P("  FDIR  met %7.1f  fault=%-16s recovery=%-14s abort=%s  (alt %.1fkm q %.0f M %.1f g %.2f)" %
              (g(r, "met_s") or 0, f, sval(r, "fdir_recovery") or "-", sval(r, "fdir_abort") or "-",
               (g(r, "alt_m") or 0) / 1000.0, g(r, "q_pa") or 0, g(r, "mach") or 0, g(r, "accel_g") or 0))
            ALERTS.append("FDIR fault '%s' at met %.1f" % (f, g(r, "met_s") or 0))
    for e in fev:
        P("  met %8.2f  %-14s %s" % (e.get("met_s") or 0, e.get("kind"),
                                     json.dumps(e.get("p") or {}, sort_keys=True)[:70]))
        if e.get("kind") in ("fault.raised", "exception"):
            ALERTS.append("%s at met %.1f: %s" %
                          (e.get("kind"), e.get("met_s") or 0, json.dumps(e.get("p") or {})[:60]))
    for r, m in tr:
        P("  ABORT met %7.1f  mode=%-14s at alt %.1f km  q %.0f Pa  M %.1f  vspd %.1f" %
          (g(r, "met_s") or 0, m, (g(r, "alt_m") or 0) / 1000.0, g(r, "q_pa") or 0,
           g(r, "mach") or 0, g(r, "vspeed_mps") or 0))
        ALERTS.append("ABORT mode '%s' at met %.1f" % (m, g(r, "met_s") or 0))
    ch = transitions(rows, "chute_phase") if has_col(st, "chute_phase") else []
    if ch:
        P("  abort recovery chutes: " + " -> ".join(p for _, p in ch))
    tail = rows[-1]
    P("  outcome: final alt %.2f km  srf_speed %.1f m/s  (splash target ~5-8 m/s)" %
      ((g(tail, "alt_m") or 0) / 1000.0, g(tail, "srf_speed_mps") or 0))


# =============================================================================================
#  8. CONTROL AUTHORITY  --  EXTENDED (§4.10 #8): from the R0 ACCUMULATORS, not from snapshots
# =============================================================================================
def control(M, st):
    sec("8. CONTROL AUTHORITY (per active phase)  [%s]" % st.label)
    rows = st.rows

    def seg(r):
        for col, pre in (("ascent_phase", "ASC"), ("abort_mode", "ABORT"), ("deorbit_phase", "DEORB"),
                         ("entry_phase", "ENT"), ("rv_phase", "RV"), ("dock_phase", "DOCK"),
                         ("boost_phase", "BOOST")):
            v = sval(r, col)
            if v not in BLANKS:
                return pre + "/" + v
        v = sval(r, "mission_phase") or sval(r, "phase_classified")
        return "MISSION/" + (v or "?")

    from collections import OrderedDict
    # ⭐ P0.0 (I1): exclude on-rails warp rows - their control columns are frozen/blank stale reads.
    nwarp = sum(1 for r in rows if is_warp(r))
    rt = [r for r in rows if not is_warp(r)]
    if nwarp:
        P("  (excluding %d on-rails warp rows - control columns there are stale/blank)" % nwarp)
    if not rt:
        P("  no realtime rows to assess")
        return
    segs = OrderedDict()
    for r in rt:
        segs.setdefault(seg(r), []).append(r)

    # ---- ⛔ §3.2's RETRACTION is why the accumulators exist: the act_* per-tick SNAPSHOTS produced a
    # ---- "68-82 % duty" figure that had to be WITHDRAWN, because a ~0.06 s RCS pulse aliases against a
    # ---- 0.1-0.25 s row. Anything that pulses is ACCUMULATED, never sampled. So where the R0 block is
    # ---- present it is the basis, and the snapshot columns are reported as a cross-check only.
    have_acc = has_col(st, "acc_int_s")
    have_sat = has_col(st, "act_sat_s")
    if have_acc:
        P("  basis: R0 ACCUMULATORS (physics-rate, un-aliased). Snapshot columns are the cross-check.")
        if not have_sat:
            P("  (act_sat_s is absent from this schema - saturation reads n/a, which is NOT zero)")
        P("  segment              rows  int_s | att%   trans%  both%   none%  | sat%   | appAtt/s appTrn/s")
        for s, rs in segs.items():
            if len(rs) < 5:
                continue
            tot = sum(g(r, "acc_int_s") or 0 for r in rs)
            if tot <= 0:
                continue
            def frac(c):
                return 100.0 * sum(g(r, c) or 0 for r in rs) / tot
            sat = frac("act_sat_s") if have_sat else None
            P("  %-20s %5d %6.1f | %5.1f %5.1f %5.1f %5.1f | %5s | %7.2f %7.2f" %
              (s[:20], len(rs), tot, frac("acc_att_s"), frac("acc_trans_s"), frac("acc_both_s"),
               frac("acc_none_s"), ("%5.1f" % sat) if sat is not None else "  n/a",
               sum(g(r, "acc_app_att") or 0 for r in rs), sum(g(r, "acc_app_trans") or 0 for r in rs)))
            if sat is not None and sat > 25.0:
                ALERTS.append("control saturated %.0f%% of %s - out of authority there (act_sat_s, R0)" % (sat, s))
        # the ledger must close: att+trans+both+none == int
        resid = []
        for r in rt:
            tot = g(r, "acc_int_s")
            if not tot:
                continue
            parts = sum(g(r, c) or 0 for c in ("acc_att_s", "acc_trans_s", "acc_both_s", "acc_none_s"))
            resid.append(abs(parts - tot) / tot)
        if resid:
            resid.sort()
            P("  accumulator ledger closes to %.4f (p95) of the interval - att+trans+both+none vs int_s" %
              resid[int(len(resid) * .95)])
            if resid[int(len(resid) * .95)] > 0.02:
                alert("the R0 accumulator ledger does not close (p95 residual %.3f) - the duty figures "
                      "below rest on it" % resid[int(len(resid) * .95)])
    else:
        P("  no R0 accumulator block in this schema - falling back to per-tick SNAPSHOTS, which ALIAS the")
        P("  ~0.06 s RCS pulse dwell (§3.2's withdrawn 68-82%% duty figure came from exactly this).")

    P("")
    P("  cross-check (snapshots)  rows | ptErr p50/p95/max deg | act_sat_duty | maxRate dps p/r/y")
    for s, rs in segs.items():
        if len(rs) < 5:
            continue
        pe = sorted(abs(g(r, "att_point_deg")) for r in rs if g(r, "att_point_deg") is not None)

        def pct(a, p):
            return a[min(len(a) - 1, int(len(a) * p))] if a else 0
        sat = sum(1 for r in rs if max(abs(g(r, "act_pitch") or 0), abs(g(r, "act_yaw") or 0),
                                       abs(g(r, "act_roll") or 0)) > 0.95)
        rp = max(abs(g(r, "rate_pitch_dps") or 0) for r in rs)
        rr = max(abs(g(r, "rate_roll_dps") or 0) for r in rs)
        ry = max(abs(g(r, "rate_yaw_dps") or 0) for r in rs)
        broke = pct(pe, .95) > 5 or sat / len(rs) > 0.25
        P("  %-20s %5d | %5.1f /%5.1f /%5.1f     | %11.3f%s | %5.1f %5.1f %5.1f" %
          (s[:20], len(rs), pct(pe, .50), pct(pe, .95), max(pe) if pe else 0, sat / len(rs),
           "  BROKEN" if broke else "", rp, rr, ry))
        if broke and not have_acc:
            ALERTS.append("control BROKEN in %s (p95 pointing error %.1f deg, sat duty %.2f)" %
                          (s, pct(pe, .95), sat / len(rs)))
    if not any(g(r, "att_point_deg") is not None for r in rt):
        P("  (pointing error is blank throughout: att_err_deg is UNFITTED until T17 - §0 says so, and a")
        P("   blank there is the honest report of an unfitted loop, not a missing measurement)")


# =============================================================================================
#  9. CREW & SCREENS  --  NEW (§4.10 #9): the CVR pass
# =============================================================================================
def crew_screens(M):
    sec("9. CREW & SCREENS  (the CVR pass)")
    st = M.primary
    if not has_col(st, "page_l") and not any(e.get("kind") == "crew.page_change" for e in M.events):
        P("  this schema records no screen state at all - the CVR channel does not exist in this file.")
        P("  (§0's three misdiagnoses are exactly what this channel was specified for; the legacy")
        P("   recorders never recorded a single crew interaction or which page was displayed.)")
        return

    sub("page timeline, per screen (a page SELECTION is a state, §2.7)")
    P("  (the recording stores the page INT; the names come from `pure/FigmaUI.cs`'s UiPage enum, read")
    P("   from the tree - the manifest does not carry them. Without the tree they read as ?(n).)")
    changes = [e for e in M.events if e.get("kind") == "crew.page_change"]
    if changes:
        names = {0: "LEFT", 1: "CENTRE", 2: "RIGHT"}
        for e in changes:
            p = e.get("p") or {}
            P("  met %8.2f  %-6s  %s -> %s" %
              (e.get("met_s") or 0, names.get(p.get("screen"), "?"), page(p.get("from")), page(p.get("to"))))
    else:
        P("  no crew.page_change events - falling back to the recorded page columns:")
    for col, nm in (("page_l", "LEFT"), ("page_c", "CENTRE"), ("page_r", "RIGHT")):
        if not has_col(st, col):
            continue
        tr = transitions(st.rows, col)
        if tr:
            P("  %-6s %s" % (nm, " -> ".join("%s@%.0f" % (page(v), g(r, "met_s") or 0) for r, v in tr[:14])))
        else:
            vals = set(sval(r, col) for r in st.rows) - set(BLANKS)
            P("  %-6s %s" % (nm, ("held on " + page(vals.pop())) if len(vals) == 1
                             else "blank throughout (screens not running, or this stream never held focus)"))
    if has_col(st, "cam_view"):
        cv = transitions(st.rows, "cam_view")
        P("  CAMERA %s" % (" -> ".join("%s@%.0f" % (v, g(r, "met_s") or 0) for r, v in cv[:14]) or "unchanged"))

    sub("crew gates + acknowledgements (§2.6)")
    for col, lbl in (("gate_id", "gate"), ("gate_phase", "gate phase"), ("crew_action", "action needed"),
                     ("step_ack_mask", "step acks")):
        if not has_col(st, col):
            continue
        tr = transitions(st.rows, col)
        P("  %-14s %s" % (lbl, " -> ".join("%s@%.0f" % (v, g(r, "met_s") or 0) for r, v in tr[:12])
                          or "unchanged / blank"))

    sub("every interaction with its dispatch verdict (S85)")
    presses = [e for e in M.events if e.get("kind") in ("crew.press", "crew.touch")]
    drops = [e for e in M.events if e.get("kind") == "crew.press_dropped"]
    if not presses:
        P("  no `crew.press` / `crew.touch` events in this recording. Two very different reasons, and the")
        P("  report cannot tell them apart from here: either nobody touched a screen or a console button")
        P("  for the whole flight, or this file predates S85 and HAS no press channel (check the")
        P("  manifest's recorder_version). Either way, 'no press did nothing' must NOT be inferred from")
        P("  silence in this section.")
        ALERTS.append("CVR press channel silent: no crew.press events (pre-S85 recording, or no presses)")
    else:
        did = sum(1 for e in presses if (e.get("p") or {}).get("acted"))
        P("  %d interaction(s): %d acted, %d did nothing. THE SECOND NUMBER IS THE POINT OF THIS CHANNEL"
          % (len(presses), did, len(presses) - did))
        P("  - a press that changes no state (refused, inert, a re-selection of the page already shown, a")
        P("  §14.4(a) no-op) leaves no trace in any state column, so it is recorded here or nowhere.")
        P("      met_s  screen  control_id                 acted  kind/lamp         alarm")
        for e in presses[:40]:
            q = e.get("p") or {}
            scr = {0: "LEFT", 1: "CENTRE", 2: "RIGHT"}.get(q.get("screen"),
                                                           "plate" if q.get("screen") == -1 else "?")
            kl = "/".join(x for x in (q.get("press_kind"), q.get("lamp")) if x) or "-"
            am = q.get("alarm_mask")
            P("  %9.2f  %-6s  %-26s %-5s  %-16s %s" %
              (e.get("met_s") or 0.0, scr, (q.get("control_id") or "?")[:26],
               "yes" if q.get("acted") else "NO", kl[:16],
               ("mask %s / %s" % (am, q.get("sev_system") or "?")) if (am is not None and am >= 0) else "-"))
        if len(presses) > 40:
            P("  ... %d more (§10's timeline carries the full ordered narrative)" % (len(presses) - 40))

        # WHICH controls never did anything. On a screens-only build most of these are the honest
        # §14.4(a) no-op and the EXPECTED answer; once Part B is flying, the same line is a finding.
        tally = {}
        for e in presses:
            q = e.get("p") or {}
            cid = q.get("control_id") or "?"
            h, t = tally.get(cid, (0, 0))
            tally[cid] = (h + (1 if q.get("acted") else 0), t + 1)
        dead = sorted(c for c, (h, t) in tally.items() if h == 0 and c != "none")
        if dead:
            P("  pressed and NEVER acted: " + ", ".join(dead[:14]) + ("" if len(dead) <= 14 else " ..."))
        misses = sum(1 for e in presses if e.get("kind") == "crew.touch")
        if misses:
            P("  %d touch(es) hit no control at all - a mis-aimed press, which is a different finding from"
              % misses)
            P("  a press that was refused, and identical to it in every state trace.")

    if drops:
        worst = max((d.get("p") or {}).get("dropped_total") or 0 for d in drops)
        P("  ⚠ %d PRESS(ES) DROPPED - the screens' publish buffer overflowed, so this section is short"
          % worst)
        P("  by that many interactions. It is in the RECORDING rather than a log line because a recorder")
        P("  that loses data quietly is the S76 failure this whole channel was built against.")
        ALERTS.append("CVR press buffer dropped %d interaction(s) - the press log is incomplete" % worst)


# =============================================================================================
#  10. EVENT TIMELINE  --  NEW (§4.10 #10): the whole log as one ordered narrative (§1.4(d))
# =============================================================================================
def event_timeline(M):
    sec("10. EVENT TIMELINE  (the whole events.jsonl, in order, with the state at each instant)")
    if not M.events:
        if M.kind == "legacy":
            P("  this schema has no event log. Recorder A had free-text per-row notes no tool ever parsed;")
            P("  Recorder B had state columns only. The EVR half of §1.3 did not exist before the BlackBox.")
        else:
            P("  the event log is empty - not even rec.open was written")
            ALERTS.append("no events at all: the event stream never wrote")
        return
    P("  %d event(s) across %d vessel(s). One shared log per mission, so both vessels are already ONE"
      % (len(M.events), len(set(e.get('vessel') for e in M.events))))
    P("  narrative - which is what makes the booster hand-off readable at all.")
    P("")
    P("      met_s  vessel            kind                    payload / state")
    for e in M.events:
        p = e.get("p") or {}
        pay = ", ".join("%s=%s" % (k, ("%.3f" % v).rstrip("0").rstrip(".") if isinstance(v, float) else v)
                        for k, v in sorted(p.items()))
        P("  %9.2f  %-16s  %-22s  %s" %
          (e.get("met_s") or 0.0, (e.get("vessel") or "?")[:16], e.get("kind"), pay[:60]))
        st = M.stream_for(e.get("vessel"))
        r = _near(st, ut=e.get("ut"))
        if r is not None and e.get("kind") not in ("rec.open", "rec.close", "rec.stream_end"):
            P("             %s" % _ctx(r))
    kinds = {}
    for e in M.events:
        kinds[e.get("kind")] = kinds.get(e.get("kind"), 0) + 1
    P("")
    P("  by kind: " + ", ".join("%s x%d" % (k, n) for k, n in sorted(kinds.items())))
    for bad in ("rec.write_error", "rec.width_mismatch", "rec.self_disable", "rec.column_never_written",
                "rec.column_unexpected_writer", "exception"):
        if kinds.get(bad):
            alert("%d x %s in the event log" % (kinds[bad], bad))


# =============================================================================================
#  11. EXCEEDANCES (FOQA)  --  NEW (§4.10 #11): every §B11 target + every CabinLimits threshold,
#      each hit printed WITH ITS PER-PHASE CONTEXT, because a rule-based exceedance ignores the
#      correlations between parameters and a bare flag invites the wrong conclusion.
# =============================================================================================
def _phase_of(r):
    for col in ("mission_phase", "phase_classified", "ascent_phase", "entry_phase", "boost_phase"):
        v = sval(r, col)
        if v not in BLANKS:
            return v
    return "?"


def _band(lo, hi):
    """How a rule's band READS. A one-sided rule has a sentinel on the other side, never a bound."""
    if lo <= -1e8:
        return "<= %.3f" % hi
    if hi >= 1e8:
        return ">= %.3f" % lo
    return "%.3f..%.3f" % (lo, hi)


def _exceed(st, col, lo, hi, tag, why, label):
    """Every row outside [lo,hi], reported as EPISODES with their phase and neighbours - not as N hits."""
    rows = [r for r in st.rows if g(r, col) is not None]
    if not rows:
        return None
    eps, cur = [], None
    for r in rows:
        v = g(r, col)
        out = v < lo or v > hi
        if out and cur is None:
            cur = [r, r, v]
        elif out:
            cur[1] = r
            if abs(v - (lo if v < lo else hi)) > abs(cur[2] - (lo if cur[2] < lo else hi)):
                cur[2] = v
        elif cur is not None:
            eps.append(cur)
            cur = None
    if cur is not None:
        eps.append(cur)
    worst = max((abs(g(r, col)) for r in rows), default=0)
    if not eps:
        P("  OK    %-16s %-22s worst %10.3f   band %s %s" % (col, label, worst, _band(lo, hi), tag))
        return None
    P("  HIT   %-16s %-22s %d episode(s)  %s" % (col, label, len(eps), tag))
    for a, b, ext in eps[:6]:
        ph = _phase_of(a)
        P("        met %8.1f..%-8.1f  extreme %10.3f (band %s)  phase %s" %
          (g(a, "met_s") or 0, g(b, "met_s") or 0, ext, _band(lo, hi), ph))
        P("        context at onset: %s" % _ctx(a))
        P("        correlated:  warp %s  thrust %s N  throttle %s  rcs %s  sev_sys %s  alarm_mask %s" %
          (sval(a, "warp_rate") or "-", sval(a, "thrust_n") or "-", sval(a, "throttle") or "-",
           sval(a, "rcs_on") or "-", sval(a, "sev_system") or "-", sval(a, "alarm_mask") or "-"))
    if len(eps) > 6:
        P("        ... %d further episode(s)" % (len(eps) - 6))
    ALERTS.append("EXCEEDANCE %s (%s): %d episode(s), extreme %.3f outside %s %s" %
                  (col, label, len(eps), max(abs(e[2]) for e in eps), _band(lo, hi), tag))
    return eps


def exceedances(M, st):
    sec("11. EXCEEDANCES (FOQA)  [%s]" % st.label)
    P("  Every rule below was CHECKED. A rule whose column is absent from this schema says so; a rule")
    P("  whose column is present and never breached prints OK with the worst value it saw. Each hit")
    P("  carries its phase and its neighbouring parameters, because rule-based exceedance detection")
    P("  ignores the correlations between parameters - the flag alone invites the wrong conclusion.")

    sub("§B11 flight-data targets (docs/BUILD_PLAN.md §B11)")
    rules = [
        ("accel_g", 0.0, B11["crew_g"][1], B11["crew_g"][2], "crew g limit"),
        ("accel_g_peak", 0.0, B11["crew_g"][1], B11["crew_g"][2], "crew g limit (R0 peak)"),
        ("q_pa", 0.0, B11["maxq_pa"][1] * 1.25, "[DOC]", "dynamic pressure vs max-Q band +25%"),
        ("q_pa_peak", 0.0, B11["maxq_pa"][1] * 1.25, "[DOC]", "dynamic pressure (R0 peak)"),
        ("skin_temp_frac", 0.0, B11["skin_temp_frac"][1], B11["skin_temp_frac"][2], "hull skin temp fraction"),
    ]
    for col, lo, hi, tag, label in rules:
        if not has_col(st, col):
            P("  n/a   %-16s %-22s column absent from this schema" % (col, label))
            continue
        _exceed(st, col, lo, hi, tag, label, label)
    # the phasing floor is a rule about a leg, not about every row
    ph = [r for r in st.rows if sval(r, "mission_phase") in ("PHASING", "APPROACH")
          or sval(r, "rv_phase") not in BLANKS]
    if ph:
        pes = [g(r, "pe_km") for r in ph if g(r, "pe_km") is not None]
        if pes and min(pes) < PE_FLOOR_KM:
            ALERTS.append("phasing periapsis floor breached: %.1f km < %.0f km" % (min(pes), PE_FLOOR_KM))
            P("  HIT   %-16s %-22s min %.1f km (floor %.0f) [DOC]" %
              ("pe_km", "phasing pe floor", min(pes), PE_FLOOR_KM))
        elif pes:
            P("  OK    %-16s %-22s min %.1f km (floor %.0f) [DOC]" %
              ("pe_km", "phasing pe floor", min(pes), PE_FLOOR_KM))

    sub("CabinLimits thresholds (%s)" % CABIN_LIMITS_SRC)
    for col, (caution, alarmv, sense, label) in sorted(CABIN_LIMITS.items()):
        if not has_col(st, col):
            P("  n/a   %-16s %-22s column absent from this schema" % (col, label))
            continue
        if not any(g(r, col) is not None for r in st.rows):
            P("  n/a   %-16s %-22s declared but blank throughout (Conditional - see §0)" % (col, label))
            continue
        if sense > 0:
            _exceed(st, col, -1e9, caution, "[CAUTION]", label, label + " caution")
            _exceed(st, col, -1e9, alarmv, "[ALARM]", label, label + " ALARM")
        else:
            _exceed(st, col, caution, 1e9, "[CAUTION]", label, label + " caution")
            _exceed(st, col, alarmv, 1e9, "[ALARM]", label, label + " ALARM")

    sub("the recorder's own severity + alarm channel")
    sevcols = [c for c in ("sev_system", "sev_vehicle", "sev_ls", "sev_thermal", "alarm_mask")
               if has_col(st, c)]
    if not sevcols:
        P("  none of sev_system / sev_vehicle / sev_ls / sev_thermal / alarm_mask is in this schema -")
        P("  the alarm channel does not exist in this file, so no alarm can be ruled out from it.")
    for col in sevcols:
        vals = [v for v in (sval(r, col) for r in st.rows) if v not in BLANKS]
        if not vals:
            P("  %-12s blank throughout (Conditional on the screens running - see §0)" % col)
            continue
        seen = sorted(set(vals))
        P("  %-12s values seen: %s" % (col, ", ".join(seen[:12])))
        if col != "alarm_mask" and any(v not in ("Nominal", "0") for v in seen):
            ALERTS.append("%s was non-nominal at some point (values: %s)" % (col, ", ".join(seen)))


# =============================================================================================
#  12. VERDICT  --  EXTENDED (§4.10 #12)
# =============================================================================================
def verdict(M):
    sec("12. VERDICT")
    P("  mission %s (%s), %d stream(s), %d event(s)" %
      (M.id, M.kind, len(M.streams), len(M.events)))
    for s in M.streams:
        per = s.row_period_s()
        P("     %-28s %-9s %6d rows  %s" %
          (s.label, s.role, len(s.rows), ("%.3f s/row" % per) if per else "row period indeterminate"))
    if not ALERTS:
        P("")
        P("  NO FINDINGS. Every section above ran and every rule was checked.")
    else:
        P("")
        P("  %d FINDING(S), most recent last:" % len(ALERTS))
        for i, a in enumerate(ALERTS, 1):
            P("   %3d. %s" % (i, a))
    P("")
    P("  ⛔ A recording is EVIDENCE about a flight, never a build source (C7/§5). To act on anything")
    P("     above, quote it INTO a register line or a doc with the mission id, the MET, and the columns")
    P("     it rests on - and remember that a SIMULATED column (§0) is evidence about our model only.")


# =============================================================================================
#  THE PASS
# =============================================================================================
def assess_mission(M):
    del ALERTS[:]
    P("FLIGHT ASSESSMENT  " + M.id + "   [%s]" % M.kind)
    provenance(M)
    for s in M.streams:
        recorder_health(M, s)
    for s in M.streams:
        physics(M, s)
    ascent(M, M.primary)
    bstreams = [s for s in M.streams if has_col(s, "boost_phase") and col_active(s.rows, "boost_phase")]
    for s in (bstreams or [M.primary]):
        booster(M, s)
    rendezvous(M, M.primary)
    return_entry(M, M.primary)
    abort(M, M.primary)
    for s in M.streams:
        control(M, s)
    crew_screens(M)
    event_timeline(M)
    for s in M.streams:
        exceedances(M, s)
    verdict(M)
    P("\n" + "=" * 78 + "\n  END. Everything above was CHECKED, not skipped.\n" + "=" * 78)


def assess(path_or_mission):
    """Entry point for one recording: a Mission, or a legacy CSV path (kept for callers that pass one)."""
    if isinstance(path_or_mission, Mission):
        return assess_mission(path_or_mission)
    M = legacy_mission(path_or_mission)
    if not M.primary.rows:
        P("empty: " + path_or_mission)
        return
    assess_mission(M)


# =============================================================================================
#  SELFTEST - synthesise a BB1/BB2 recording at the CURRENT schema and assess it end to end
# =============================================================================================
def _synth(dirpath):
    """Write a two-stream mission: capsule ascent + a tracked booster, one shared event log.

    ⛔ THIS IS A FIXTURE, NOT FLIGHT DATA. It exists so the reader can be exercised without the capsule
    (the install/glass gate is the owner's, per session) and it is written to a TEMPORARY directory,
    never to `docs/flights/` - the corpus there is named evidence attached to findings, and a
    synthesised file sitting in it would be indistinguishable from a flown one six months from now.
    Every column comes from `BlackBoxSchema.cs` itself, so the fixture tracks the schema instead of
    freezing a copy of it.
    """
    cols = schema_from_source()
    if len(cols) < 100:
        raise AssertionError("schema parse produced %d columns - BlackBoxSchema.cs no longer matches the "
                             "parser, so the fixture would test a stale shape" % len(cols))
    names = [c["name"] for c in cols]
    idx = dict((n, i) for i, n in enumerate(names))
    mid = "Crew-2_20260904_101500"

    def blank():
        return [""] * len(names)

    def put(row, n, v):
        if n in idx:
            row[idx[n]] = v if isinstance(v, str) else ("%.6f" % v).rstrip("0").rstrip(".")

    events = []

    def ev(vessel, ut, met, seq, kind, p):
        events.append({"mission_id": mid, "vessel": vessel, "ut": ut, "met_s": met, "seq": seq,
                       "kind": kind, "p": p})

    UT0 = 1000000.0

    def _alt(t):
        t = max(0.0, t)
        return 0.5 * 9.0 * t * t if t < 200 else 180000.0 + (t - 200) * 90.0

    # ---- capsule stream: 0..540 s of ascent at 10 Hz, thinned to 1 Hz so the fixture stays small ----
    cap, seq = [], 0
    for k in range(0, 541):
        met = float(k)
        ut = UT0 + met
        seq += 1
        if k == 300:
            seq += 3                      # an injected seq GAP - §1 must report 3 dropped rows
        r = blank()
        put(r, "mission_id", mid)
        put(r, "seq", "%d" % seq)
        put(r, "ut", ut)
        put(r, "met_s", met)
        put(r, "wall_s", met)
        put(r, "warp_rate", 1.0)
        put(r, "warp_rails", "0")
        put(r, "vessel", "Crew-2")
        put(r, "focus", "Crew-2")
        put(r, "rec_build_us", 120.0)
        put(r, "body", "Earth")
        # The fixture has to AGREE WITH ITSELF or §2's self-checks fire on the fixture instead of on a
        # recording: altitude is the integral of vertical speed, and accel_g is thrust/mass, by construction.
        alt = _alt(met)
        spd = min(7800.0, 22.0 * met)
        put(r, "alt_m", alt)
        put(r, "alt_radar_m", alt)
        put(r, "speed_mps", spd)
        put(r, "srf_speed_mps", spd)
        put(r, "vspeed_mps", alt - _alt(met - 1.0) if met >= 1 else 0.0)
        put(r, "lat_deg", 28.6084 + met * 1e-4)
        put(r, "lon_deg", -80.6043 + met * 3e-4)
        put(r, "atm_density", max(0.0, 1.2 * math.exp(-alt / 8000.0)))
        put(r, "mass_kg", 550000.0 - 600.0 * met)
        put(r, "mach", spd / 300.0)
        q = 33000.0 * math.exp(-((met - 72.0) ** 2) / 900.0)
        put(r, "q_pa", q)
        put(r, "q_pa_peak", q)
        thr = 7600000.0 if met < 137 else (900000.0 if met < 512 else 0.0)
        mkg = 550000.0 - 600.0 * met
        put(r, "accel_g", thr / mkg / 9.80665)
        put(r, "accel_g_peak", thr / mkg / 9.80665)
        put(r, "throttle", 1.0 if met < 512 else 0.0)
        put(r, "thrust_n", thr)
        put(r, "eng_ignited", "9" if met < 137 else ("1" if met < 512 else "0"))
        put(r, "eng_flameout", "0")
        put(r, "stage", "2" if met < 137 else "1")
        put(r, "mmh_frac", max(0.0, 1.0 - met / 900.0))
        put(r, "nto_frac", max(0.0, 1.0 - met / 900.0))
        put(r, "ec_frac", 0.92)
        ap = min(210.0, 0.4 * met)
        pe = -2000.0 + 4.1 * met if met < 512 else 197.0
        put(r, "ap_km", ap)
        put(r, "pe_km", pe)
        put(r, "inc_deg", 51.63)
        put(r, "raan_deg", 122.0)
        put(r, "ecc", 0.001)
        put(r, "sma_m", 6571000.0)
        put(r, "pitch_deg", max(10.0, 90.0 - met * 0.15))
        put(r, "heading_deg", 45.0)
        put(r, "roll_deg", 0.0)
        put(r, "rate_pitch_dps", 0.4)
        put(r, "rate_roll_dps", 0.2)
        put(r, "rate_yaw_dps", 0.3)
        put(r, "rate_peak_dps", 0.5)
        put(r, "att_rate_meas", 0.5)
        put(r, "acc_int_s", 1.0)
        put(r, "acc_att_s", 0.6)
        put(r, "acc_trans_s", 0.1)
        put(r, "acc_both_s", 0.05)
        put(r, "acc_none_s", 0.25)
        put(r, "acc_app_att", 0.4)
        put(r, "acc_app_trans", 0.05)
        put(r, "act_sat_s", 0.02)
        put(r, "app_pitch", 0.2)
        put(r, "app_yaw", 0.1)
        put(r, "app_roll", 0.0)
        put(r, "ctrl_tq_pitch", 40.0)
        put(r, "ctrl_tq_yaw", 40.0)
        put(r, "ctrl_tq_roll", 12.0)
        put(r, "rcs_thrust_n", 400.0)
        put(r, "rcs_on", "1")
        put(r, "moi_pitch", 900.0)
        put(r, "moi_roll", 300.0)
        put(r, "moi_yaw", 900.0)
        put(r, "skin_temp_frac", 0.35)
        put(r, "hull_temp_c", 210.0)
        put(r, "mission_phase", "PRELAUNCH" if met < 2 else "ASCENT")
        put(r, "phase_classified", "PRELAUNCH" if met < 2 else "ASCENT")
        put(r, "gnc_engaged", "0")
        put(r, "mode_index", "Idle")
        put(r, "gate_id", "None")
        put(r, "gate_phase", "Holding")
        put(r, "crew_action", "0")
        put(r, "is_return", "0")
        put(r, "bus1_on", "1")
        put(r, "bus2_on", "1")
        for s in ("str_a1", "str_b1", "str_c1", "str_a2", "str_b2", "str_c2"):
            put(r, s, "Nominal")
        put(r, "fire_intensity", 0.0)
        put(r, "suppressant", 1.0)
        put(r, "leak_rate", 0.0)
        put(r, "isolating", "0")
        put(r, "o2_store", 0.98)
        put(r, "n2_store", 0.97)
        put(r, "canister_used", 0.02)
        put(r, "cabin_psia", 14.7)
        # an injected CO2 exceedance so §11 is exercised against a real breach, not only against OKs
        put(r, "co2_mmhg", 6.4 if 300 <= met <= 340 else 2.1)
        put(r, "ppo2_psia", 3.0)
        put(r, "cabin_temp_c", 22.0)
        put(r, "loop_a_c", 18.0)
        put(r, "loop_b_c", 19.0)
        put(r, "sev_system", "Nominal")
        put(r, "sev_vehicle", "Nominal")
        put(r, "sev_ls", "Caution" if 300 <= met <= 340 else "Nominal")
        put(r, "sev_thermal", "Nominal")
        put(r, "alarm_mask", "4" if 300 <= met <= 340 else "0")
        put(r, "ls_present", "0")
        put(r, "comm_linked", "1")
        put(r, "comm_signal", 0.95)
        put(r, "ker_avail", "0")
        put(r, "page_l", "4" if met < 250 else "15")
        put(r, "page_c", "1")
        put(r, "page_r", "0" if met < 120 else "3")
        put(r, "cam_view", "1")
        put(r, "prop_frac", 0.88)
        put(r, "step_ack_mask", "0")
        put(r, "fdir_fault", "None")
        put(r, "fdir_recovery", "None")
        put(r, "aborting", "0")
        put(r, "abort_mode", "None")
        cap.append(r)

    ev("Crew-2", UT0, 0.0, 1, "rec.open", {"params_file": mid + ".params.csv", "role": "focused"})
    ev("Crew-2", UT0 + 1.4, 1.4, 2, "flight.liftoff", {"ut": UT0 + 1.4, "met_s": 1.4, "mass_kg": 549000.0})
    ev("Crew-2", UT0 + 74.2, 74.2, 74, "flight.maxq",
       {"peak_q_pa": 33000.0, "alt_m": 12100.0, "mach": 1.9, "met_s": 74.2})
    ev("Crew-2", UT0 + 137.0, 137.0, 137, "stage.engine_shutdown",
       {"from": 9, "to": 0, "thrust_n": 0.0, "stage": 2})
    ev("Crew-2", UT0 + 141.0, 141.0, 141, "stage.staged",
       {"from": 2, "to": 1, "alt_m": 82000.0, "mass_kg": 120000.0})
    ev("Crew-2", UT0 + 148.0, 148.0, 148, "stage.engine_ignite",
       {"from": 0, "to": 1, "thrust_n": 900000.0, "stage": 1})
    ev("Crew-2", UT0 + 250.0, 250.0, 250, "crew.page_change", {"screen": 0, "from": 4, "to": 15})
    # ---- S85: the CVR press channel. Four interactions chosen to exercise every branch of §9's press
    # ---- pass: an acted plate press, a §14.4(a) no-op that acted:false, a nav press, and a touch that
    # ---- hit nothing - plus a dropped-press report, so the overflow alert has a true positive.
    ev("Crew-2", UT0 + 249.6, 249.6, 249, "crew.press",
       {"control_id": "nav.goto.Docking", "surface": "Nav", "enum_v": 1, "screen": 0, "page": 4,
        "px": 640.0, "py": 1980.0, "acted": True, "cmd": -1, "cmd_name": None, "press_kind": None,
        "lamp": None, "alarm_mask": 0, "sev_system": "Nominal"})
    ev("Crew-2", UT0 + 300.2, 300.2, 300, "crew.press",
       {"control_id": "dock.TransFwd", "surface": "Dock", "enum_v": 8, "screen": 1, "page": 27,
        "px": 1710.0, "py": 1180.0, "acted": False, "cmd": -1, "cmd_name": None, "press_kind": None,
        "lamp": None, "alarm_mask": 0, "sev_system": "Nominal"})
    ev("Crew-2", UT0 + 301.0, 301.0, 301, "crew.touch",
       {"control_id": "none", "surface": "None", "enum_v": -1, "screen": 1, "page": 27,
        "px": 40.0, "py": 40.0, "acted": False, "cmd": -1, "cmd_name": None, "press_kind": None,
        "lamp": None, "alarm_mask": 0, "sev_system": "Nominal"})
    ev("Crew-2", UT0 + 505.5, 505.5, 505, "crew.press",
       {"control_id": "panel.FirePyro", "surface": "Panel", "enum_v": 26, "screen": -1, "page": -1,
        "px": None, "py": None, "acted": True, "cmd": 26, "cmd_name": "FirePyro",
        "press_kind": "Momentary", "lamp": "Lit", "alarm_mask": 4, "sev_system": "Caution"})
    ev("Crew-2", UT0 + 506.0, 506.0, 506, "crew.press_dropped",
       {"dropped_total": 2, "dropped_since": 2, "capacity": 64})
    ev("Crew-2", UT0 + 512.0, 512.0, 512, "stage.engine_shutdown",
       {"from": 1, "to": 0, "thrust_n": 0.0, "stage": 1})
    ev("Crew-2", UT0 + 540.0, 540.0, 541, "rec.stream_end", {"rows": len(cap)})

    # ---- booster stream: tracked, unfocused, five-phase descent to just off the deck ----
    boo, seq = [], 0
    phases = [(140, "Flip"), (170, "Boostback"), (230, "Coast"), (300, "EntryBurn"),
              (400, "AeroDescent"), (470, "LandingBurn"), (480, "Landed")]
    for k in range(140, 481):
        met = float(k)
        ut = UT0 + met
        seq += 1
        r = blank()
        ph = next(nm for lim, nm in phases if met <= lim)
        f = (met - 140.0) / 340.0

        def _balt(t):
            ff = min(1.0, max(0.0, (t - 140.0) / 340.0))
            return max(0.0, 82000.0 * (1.0 - ff) ** 1.6)
        put(r, "mission_id", mid)
        put(r, "seq", "%d" % seq)
        put(r, "ut", ut)
        put(r, "met_s", met)
        put(r, "wall_s", met)
        put(r, "warp_rate", 1.0)
        put(r, "warp_rails", "0")
        put(r, "vessel", "Falcon9_S1")
        put(r, "focus", "Crew-2")
        put(r, "rec_build_us", 90.0)
        put(r, "body", "Earth")
        put(r, "alt_m", _balt(met))
        put(r, "alt_radar_m", _balt(met))
        put(r, "speed_mps", max(2.0, 2100.0 * (1.0 - f)))
        put(r, "srf_speed_mps", max(2.0, 2100.0 * (1.0 - f)))
        put(r, "vspeed_mps", _balt(met) - _balt(met - 1.0))
        put(r, "lat_deg", 32.78760)
        put(r, "lon_deg", -76.64455)
        put(r, "atm_density", 0.4 * f)
        put(r, "mass_kg", 40000.0 - 9000.0 * f)
        put(r, "mach", max(0.1, 7.0 * (1.0 - f)))
        put(r, "q_pa", 22000.0 * math.sin(math.pi * f))
        put(r, "q_pa_peak", 22000.0 * math.sin(math.pi * f))
        put(r, "accel_g", 1.0 + 2.0 * f)
        put(r, "accel_g_peak", 1.0 + 2.0 * f)
        put(r, "ap_km", 120.0 * (1.0 - f))
        put(r, "pe_km", -300.0)
        put(r, "inc_deg", 51.63)
        put(r, "throttle", 0.7 if ph in ("Boostback", "EntryBurn", "LandingBurn") else 0.0)
        put(r, "thrust_n", 2400000.0 if ph in ("Boostback", "EntryBurn", "LandingBurn") else 0.0)
        put(r, "eng_ignited", "3" if ph in ("Boostback", "EntryBurn") else ("1" if ph == "LandingBurn" else "0"))
        put(r, "eng_flameout", "0")
        put(r, "stage", "1")
        put(r, "pitch_deg", 20.0)
        put(r, "heading_deg", 250.0)
        put(r, "roll_deg", 0.0)
        put(r, "aoa_deg", 4.0)
        put(r, "aos_deg", 0.5)
        put(r, "rate_pitch_dps", 1.2)
        put(r, "rate_roll_dps", 0.6)
        put(r, "rate_yaw_dps", 0.9)
        put(r, "rate_peak_dps", 1.6)
        put(r, "att_rate_meas", 1.6)
        put(r, "acc_int_s", 1.0)
        put(r, "acc_att_s", 0.8)
        put(r, "acc_trans_s", 0.0)
        put(r, "acc_both_s", 0.0)
        put(r, "acc_none_s", 0.2)
        put(r, "acc_app_att", 0.7)
        put(r, "acc_app_trans", 0.0)
        put(r, "act_sat_s", 0.3 if ph == "LandingBurn" else 0.05)
        put(r, "app_pitch", 0.5)
        put(r, "app_yaw", 0.2)
        put(r, "app_roll", 0.1)
        put(r, "ctrl_tq_pitch", 900.0)
        put(r, "ctrl_tq_yaw", 900.0)
        put(r, "ctrl_tq_roll", 90.0)
        put(r, "rcs_thrust_n", 4000.0)
        put(r, "rcs_on", "1")
        put(r, "moi_pitch", 26000.0)
        put(r, "moi_roll", 900.0)
        put(r, "moi_yaw", 26000.0)
        put(r, "skin_temp_frac", 0.5)
        put(r, "hull_temp_c", 640.0)
        put(r, "phase_classified", "ENTRY")
        put(r, "boost_phase", ph)
        put(r, "boost_steer_pitch", 0.5)
        put(r, "boost_steer_yaw", 0.2)
        put(r, "boost_steer_roll", 0.1)
        put(r, "boost_throttle", 0.7 if ph in ("Boostback", "EntryBurn", "LandingBurn") else 0.0)
        put(r, "boost_db_pitch", "0")
        put(r, "boost_db_yaw", "0")
        put(r, "boost_db_roll", "0")
        put(r, "boost_db_deg", 0.0)
        put(r, "boost_uncommanded", "0")
        put(r, "comm_linked", "1")
        put(r, "comm_signal", 0.7)
        put(r, "ls_present", "0")
        boo.append(r)

    ev("Falcon9_S1", UT0 + 140.0, 140.0, 1, "rec.open",
       {"params_file": mid + ".Falcon9_S1.params.csv", "role": "tracked"})
    ev("Falcon9_S1", UT0 + 480.0, 480.0, 341, "flight.touchdown",
       {"lat_deg": 32.78760, "lon_deg": -76.64455, "vspeed_mps": -1.5})
    ev("Falcon9_S1", UT0 + 480.5, 480.5, 341, "rec.stream_end", {"rows": len(boo)})

    def fill_live(rows, tracked):
        """Every Live column of the right SCOPE gets a value, so the ghost-column check has a true negative.

        A fixture that leaves a Live column blank makes §1 report ghosts that are the FIXTURE's fault, and
        then a real ghost would be indistinguishable from the noise. Scope is respected exactly as the
        recorder respects it: a tracked, never-focused stream writes NO capsule column - that withholding
        is the property BB2 exists for and the one §1 must see.
        """
        for c in cols:
            if c["fit"] != "Live":
                continue
            if tracked and c["scope"] == "capsule":
                continue
            i = idx[c["name"]]
            if any(r[i] for r in rows):
                continue
            u = c["units"]
            for k, r in enumerate(rows):
                if u == "string":
                    r[i] = "selftest"
                elif u == "enum":
                    r[i] = "Nominal"
                elif u in ("0/1", "bits"):
                    r[i] = "0"
                elif u in ("int", "count"):
                    r[i] = "1"
                else:
                    r[i] = "%.4f" % (0.5 + 0.001 * k)

    def write_csv(path, rows):
        with open(path, "w", encoding="utf-8", newline="") as fh:
            w = csv.writer(fh, lineterminator="\n")
            w.writerow(names)
            for r in rows:
                w.writerow(r)

    def manifest(stem, vessel, role, focused, rows, params):
        return {
            "schema_version": 1, "recorder_version": "BB2.0",
            "dragonscreen_asm_version": "0.0.0-selftest",
            "dragonscreen_dll_sha256": "0" * 64, "ksp_version": "1.12.5",
            "mod_versions": ["DragonScreen 0.0.0-selftest"],
            "mission_id": mid, "vessel": vessel, "vessel_persistent_id": 1,
            "crew": ["Selftest Kerman"], "body": "Earth", "target_name": "",
            "stream_role": role, "ever_focused": focused,
            "params_file": params, "events_file": mid + ".events.jsonl",
            "stream_join_on": ["mission_id", "ut"],
            "launch_lat_deg": PAD[0], "launch_lon_deg": PAD[1],
            "launch_ut": UT0, "ut_at_open": UT0, "wall_at_open": 0.0,
            "real_world_utc_at_open": "2026-09-04T10:15:00Z",
            "row_rate_mode": "adaptive", "row_rate_dynamic_hz": 1.0,
            "row_rate_quiescent_hz": 1.0, "warp_wall_floor_s": 1.0,
            "dynamic_phase_rule": "selftest fixture: 1 Hz throughout",
            "mechjeb_cfg_sha": "selftest", "tunables": ["BlackBoxRecorder.FixedRowRateHz = 10"],
            "columns": [{"i": i, "name": c["name"], "units": c["units"], "tier": c["tier"],
                         "period_s": None, "provenance": c["provenance"],
                         "source": c.get("source") or "selftest",
                         "fit": c["fit"], "scope": c["scope"], "note": c.get("note")}
                        for i, c in enumerate(cols)],
            "closed": True, "closed_ut": UT0 + 541.0, "closed_reason": "selftest",
            "rows_written": len(rows), "events_written": len(events), "write_errors": 0,
            "max_rec_build_us": 210.0, "coverage": [],
        }

    fill_live(cap, False)
    fill_live(boo, True)

    p_cap = os.path.join(dirpath, mid + ".params.csv")
    p_boo = os.path.join(dirpath, mid + ".Falcon9_S1.params.csv")
    write_csv(p_cap, cap)
    write_csv(p_boo, boo)
    json.dump(manifest(mid, "Crew-2", "focused", True, cap, os.path.basename(p_cap)),
              open(os.path.join(dirpath, mid + ".manifest.json"), "w", encoding="utf-8"), indent=1)
    json.dump(manifest(mid + ".Falcon9_S1", "Falcon9_S1", "tracked", False, boo, os.path.basename(p_boo)),
              open(os.path.join(dirpath, mid + ".Falcon9_S1.manifest.json"), "w", encoding="utf-8"), indent=1)
    with open(os.path.join(dirpath, mid + ".events.jsonl"), "w", encoding="utf-8", newline="\n") as fh:
        for e in events:
            fh.write(json.dumps(e) + "\n")
    return mid


def selftest(verbose=False):
    """Synthesise a BB1/BB2 recording, assess it, and assert the twelve sections did their job.

    This is the headless check BB3's register line asks for: it needs no KSP, no install and no glass
    time (both of which are separate owner gates), and it proves the reader can decode the CURRENT
    schema, both streams, the shared event log and the manifests - from the three raw file kinds alone.
    """
    import tempfile, shutil, io
    tmp = tempfile.mkdtemp(prefix="dsbb3_")
    try:
        mid = _synth(tmp)
        global REPO_FLIGHTS
        keep, REPO_FLIGHTS = REPO_FLIGHTS, tmp
        try:
            ms = missions(False)
            assert len(ms) == 1, "expected 1 mission, found %d" % len(ms)
            M = ms[0]
            assert M.id == mid, "mission id %r" % M.id
            assert len(M.streams) == 2, "expected 2 streams, found %d" % len(M.streams)
            assert M.primary.vessel == "Crew-2", "primary stream is %r" % M.primary.vessel
            assert M.streams[1].tracked and not M.streams[1].ever_focused
            assert len(M.events) == 17, "expected 17 events, found %d" % len(M.events)
            # The report goes to the BUFFER the assertions read; printing 400 lines into every
            # `build.py test` would bury the C# suites' own output. `--selftest --verbose` shows it.
            buf = io.StringIO()
            _SINK.append(buf)
            _QUIET[0] = not verbose
            try:
                assess_mission(M)
            finally:
                _QUIET[0] = False
                _SINK.remove(buf)
            out = buf.getvalue()
        finally:
            REPO_FLIGHTS = keep

        need = ["0. PROVENANCE", "1. RECORDER HEALTH", "2. PHYSICS SELF-CHECK", "3. ASCENT",
                "4. BOOSTER", "5. RENDEZVOUS", "6. DEORBIT", "7. ABORT", "8. CONTROL AUTHORITY",
                "9. CREW & SCREENS", "10. EVENT TIMELINE", "11. EXCEEDANCES", "12. VERDICT"]
        missing = [s for s in need if s not in out]
        assert not missing, "sections missing from the report: %s" % missing
        checks = [
            ("SIMULATED", "§0 did not mark the SIMULATED columns"),
            ("UNFITTED", "§0 did not list the unfitted columns"),
            ("seq GAP", "§1 did not catch the injected seq gap"),
            ("3 row(s) dropped", "§1 mis-counted the injected gap"),
            ("WITHHELD", "§1 did not recognise the booster's withheld capsule columns"),
            ("no ghost columns", "§1 reported ghosts on a fixture that has none on the capsule stream"),
            ("thrust/mass vs accel_g", "§2 skipped the new thrust/mass check"),
            ("flight.maxq", "§3 did not read the event log"),
            ("maxq_pa", "§3 did not check max-Q against §B11"),
            ("MISS vs DECK CENTRE", "§4 did not port the deck-miss geometry"),
            ("PROVISIONAL AIM POINT", "§4 omitted the provisional-coordinates caveat"),
            ("R0 ACCUMULATORS", "§8 did not use the accumulators"),
            ("page timeline", "§9 did not print the page timeline"),
            ("panel.FirePyro", "§9 did not print the S85 press table"),
            ("2 acted, 2 did nothing", "§9 mis-counted the acted-vs-not split"),
            ("pressed and NEVER acted", "§9 did not name the controls that never did anything"),
            ("hit no control at all", "§9 did not separate a mis-aimed touch from a refused press"),
            ("2 PRESS(ES) DROPPED", "§9 did not report the injected press-buffer overflow"),
            ("by kind:", "§10 did not summarise the event kinds"),
            ("co2_mmhg", "§11 did not check the CO2 rule"),
            ("HIT", "§11 found no exceedance on a fixture that injects one"),
            ("FINDING(S)", "§12 printed no findings on a fixture that injects two"),
        ]
        bad = [msg for token, msg in checks if token not in out]
        assert not bad, "selftest failures:\n   - " + "\n   - ".join(bad)
        # the booster is 'Landed' at a point ~6 m off the deck centre, so the miss must be small but real
        m = re.search(r"MISS vs DECK CENTRE = ([\d.]+) m", out)
        assert m and 0.1 < float(m.group(1)) < 200.0, "deck miss reads %s" % (m and m.group(1))

        # ---- BB5: a blank Tier.R2 cell must refuse a §B11 verdict, not fabricate one ----------------
        # A hand-built, minimal fixture (not `_synth`'s big one) so the injected gap is exact: the MECO
        # row's alt_m is blank and the SECO/insertion row's pe_km is blank - reproducing, verbatim, the
        # overseer-relayed false failures this register line exists for (meco_alt_km = 0.00,
        # insertion pe -9999.0). Before the fix, `(g(mrow,"alt_m") or 0) / 1000.0` and
        # `pe if pe is not None else -9999` turned each blank into exactly those numbers.
        bb5_rows = [
            {"vessel": "Crew-2", "ut": "0", "met_s": "0", "thrust_n": "7000000", "mach": "0.5",
             "alt_m": "5000", "pe_km": "", "ap_km": "", "inc_deg": "", "raan_deg": "",
             "q_pa": "20000", "q_pa_peak": "20000", "accel_g": "1.5", "accel_g_peak": "1.5"},
            {"vessel": "Crew-2", "ut": "137", "met_s": "137", "thrust_n": "0", "mach": "10.0",
             "alt_m": "",  # <-- the injected gap: MECO row, blank alt_m (R2 tier not yet reached)
             "pe_km": "", "ap_km": "", "inc_deg": "", "raan_deg": "",
             "q_pa": "500", "q_pa_peak": "500", "accel_g": "0.1", "accel_g_peak": "0.1"},
            {"vessel": "Crew-2", "ut": "512", "met_s": "512", "thrust_n": "0", "mach": "9.0",
             "alt_m": "200000",
             "pe_km": "",  # <-- the injected gap: insertion row, blank pe_km
             "ap_km": "205", "inc_deg": "51.6", "raan_deg": "120",
             "q_pa": "0", "q_pa_peak": "0", "accel_g": "0.0", "accel_g_peak": "0.0"},
        ]
        bb5_events = [{"mission_id": "selftest-bb5", "vessel": "Crew-2", "ut": 137.0, "met_s": 137.0,
                       "seq": 2, "kind": "stage.engine_shutdown", "p": {"from": 9, "to": 0, "thrust_n": 0.0}}]
        bb5_stream = Stream("selftest-bb5.params.csv", bb5_rows, manifest={}, label="bb5-fixture")
        bb5_mission = Mission("selftest-bb5", [bb5_stream], events=bb5_events)
        saved_alerts = list(ALERTS)
        ALERTS[:] = []
        buf2 = io.StringIO()
        _SINK.append(buf2)
        _QUIET[0] = True
        try:
            ascent(bb5_mission, bb5_stream)
        finally:
            _QUIET[0] = False
            _SINK.remove(buf2)
            bb5_out, bb5_alerts = buf2.getvalue(), list(ALERTS)
            ALERTS[:] = saved_alerts
        assert "meco_alt_km" in bb5_out and "not recorded" in bb5_out, \
            "BB5: meco_alt_km line missing, or a blank cell no longer reads 'not recorded':\n" + bb5_out
        assert not any("meco_alt_km" in a for a in bb5_alerts), \
            "BB5: a blank alt_m on the MECO row still raised a fabricated §B11 alert: %r" % bb5_alerts
        assert "ORBIT STATUS NOT DETERMINED" in bb5_out, \
            "BB5: a blank pe_km on the insertion row did not refuse the orbit verdict:\n" + bb5_out
        assert not any("SUBORBITAL" in a for a in bb5_alerts), \
            "BB5: a blank pe_km still raised a fabricated SUBORBITAL alert: %r" % bb5_alerts

        # ---- BB10: ascent()'s row-selection must not starve on mission_phase/phase_classified's -----
        # ---- OWN Tier.R2 decimation - a LONG gap spanning the real thrust death, reproducing the ----
        # ---- mechanism behind the overseer-relayed false `seco_s = 157.54`. "ASCENT" is tagged only
        # at met=0 and met=50, then BLANK for 90 rows through the real thrust death at met=140 (thrust
        # drops to 0 there) - long enough that the OLD raw-equality selection's lastIdx=50 forward-scan
        # (+80 rows, only as far as met=130) can never reach met=140, and reports the stale met=50 row
        # as if it were SECO. The fix's forward-fill carries "ASCENT" through the gap (per §4.6's
        # documented contract) up to met=141, the next classified row (tagged "COAST" - the genuine
        # transition), landing lastIdx there instead and letting the forward-scan find the real thrust
        # death within a few rows.
        bb10_rows = []
        for met in range(0, 146):
            phase = "ASCENT" if met in (0, 50) else ("COAST" if met == 141 else "")
            bb10_rows.append({
                "vessel": "Crew-2", "ut": str(1000000.0 + met), "met_s": str(met),
                "thrust_n": str(7000000.0 if met < 140 else 0.0), "mach": "1.0",
                "alt_m": str(1000.0 * met), "pe_km": "150", "ap_km": "210",
                "inc_deg": "51.6", "raan_deg": "120",
                "q_pa": "0", "q_pa_peak": "0", "accel_g": "0.5", "accel_g_peak": "0.5",
                "mission_phase": phase, "phase_classified": phase,
            })
        bb10_events = [{"mission_id": "selftest-bb10", "vessel": "Crew-2", "ut": 1000000.0, "met_s": 0.0,
                        "seq": 1, "kind": "flight.liftoff", "p": {}}]
        bb10_stream = Stream("selftest-bb10.params.csv", bb10_rows, manifest={}, label="bb10-fixture")
        bb10_mission = Mission("selftest-bb10", [bb10_stream], events=bb10_events)
        saved_alerts = list(ALERTS)
        ALERTS[:] = []
        buf3 = io.StringIO()
        _SINK.append(buf3)
        _QUIET[0] = True
        try:
            ascent(bb10_mission, bb10_stream)
        finally:
            _QUIET[0] = False
            _SINK.remove(buf3)
            bb10_out = buf3.getvalue()
            ALERTS[:] = saved_alerts
        m10 = re.search(r"seco_s\s+([\d.]+)\s*s", bb10_out)
        assert m10, "BB10: seco_s line not found in ascent() output:\n" + bb10_out
        bb10_seco = float(m10.group(1))
        # The stale-early value the OLD raw-equality selection reports on this exact fixture is 50.00
        # (the last row it can literally see as "ASCENT" before the 90-row gap) - proven by hand, then
        # reverted, per this line's register entry. > 130 requires the fix to have bridged the gap.
        assert bb10_seco > 130.0, (
            "BB10: ascent()'s row-selection still starves on the mission_phase/phase_classified R2 gap - "
            "seco_s read %.2f s (the OLD code reads the stale met=50 row's value, 50.00, on this exact "
            "fixture); forward-fill should have carried ASCENT through the gap to the real thrust death "
            "near met=140" % bb10_seco)

        print("SELFTEST OK - %d sections, %d report lines, deck miss %.1f m, %d finding(s), "
              "BB5 refusal proven, BB10 seco_s %.2f s (decimation-bridged)" %
              (len(need), out.count(chr(10)), float(m.group(1)), len(ALERTS), bb10_seco))
        return 0
    except AssertionError as e:
        print("SELFTEST FAILED: %s" % e)
        return 1
    finally:
        shutil.rmtree(tmp, ignore_errors=True)


# =============================================================================================
def main():
    global PAGES
    PAGES = page_names()
    args = sys.argv[1:]
    external = "--external" in args
    args = [a for a in args if a != "--external"]
    out = None
    if "--out" in args:
        i = args.index("--out")
        out = args[i + 1] if i + 1 < len(args) else None
        args = args[:i] + args[i + 2:]
    verbose = "--verbose" in args
    args = [a for a in args if a != "--verbose"]
    if args and args[0] == "--selftest":
        return selftest(verbose)
    fh = open(out, "w", encoding="utf-8") if out else None
    if fh:
        _SINK.append(fh)
    try:
        if args and args[0] in ("--list", "-l"):
            for m in reversed(missions(external)):
                P("  [BB] %-34s %d stream(s), %d event(s)" % (m.id, len(m.streams), len(m.events)))
                for s in m.streams:
                    P("         %-40s %8.0f KB" % (os.path.basename(s.path),
                                                   os.path.getsize(s.path) / 1024.0))
            for p in reversed(captures(external)):
                P("  [legacy] %s  %.0f KB" % (os.path.basename(p), os.path.getsize(p) / 1024.0))
            return 0
        if args and args[0] in ("--all", "-a"):
            # The whole corpus in one pass. A file that fails to read is REPORTED, not swallowed - the
            # point of a corpus sweep is to find out which recordings are readable at all.
            bad, n = 0, 0
            for M in missions(external):
                n += 1
                try:
                    assess_mission(M)
                except Exception as e:
                    bad += 1
                    P("\n*** FAILED to assess %s: %s: %s ***" % (M.id, type(e).__name__, e))
            for p in captures(external):
                n += 1
                try:
                    assess(p)
                except Exception as e:
                    bad += 1
                    P("\n*** FAILED to assess %s: %s: %s ***" % (os.path.basename(p), type(e).__name__, e))
            P("\ncorpus: %d recording(s), %d failed" % (n, bad))
            return 1 if bad else 0
        if args:
            M = resolve(args[0], external)
            if M is None:
                P("not found (tried BlackBox missions then legacy CSVs): " + args[0])
                return 2
            assess_mission(M)
            return 0
        ms = missions(external)
        if ms:
            assess_mission(ms[-1])
            return 0
        legacy = captures(external)
        if not legacy:
            P("no flight capture found")
            return 2
        assess(legacy[-1])
        return 0
    finally:
        if fh:
            _SINK.remove(fh)
            fh.close()
            print("--- report also written to %s" % out)


if __name__ == "__main__":
    sys.exit(main())
