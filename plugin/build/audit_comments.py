#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
audit_comments.py - are the COMMENTS still true?

    python plugin/build/audit_comments.py            # everything, ranked
    python plugin/build/audit_comments.py --brief    # counts only
    python plugin/build/audit_comments.py Landing.cs # one file

WHY THIS EXISTS
---------------
This project's comments carry the reasoning - which flight a constant came from, which F9I function
a mechanism was ported out of, what a number used to be. That is the most valuable thing in the
repo and it ROTS SILENTLY, because nothing compiles a comment.

Every check below is a defect that has actually shipped here:

  · `BOOSTER.ks:156` when the real line is 308  - EVERY F9I citation in the tree was a line number
    in the COMMENT-STRIPPED RELEASE, because check_live.py was pointed at the wrong copy.
  · "`Boostback:207` resets it"                 - Boostback spans 394-517. The line is not in it.
  · `bb_dragon_CrewDragon_072`                  - cited as the source of the entry AoA schedule.
    That recording does not exist in the corpus.
  · "the default 5", "`MaxLaps` is 3"           - true when written, false after the value changed.
  · "NOTHING CALLS IT", "read by nothing"       - true when written, false once something did.
  · `RollStoppingScale`                         - named in comments after it was deleted.

WHAT IT DELIBERATELY DOES NOT DO
--------------------------------
It does not read English. It cannot tell you a comment's ARGUMENT is wrong - only that a fact it
states is checkable and does not check out.

⛔ AND IT MUST NOT CRY WOLF. The first version reported 192 "ghost identifiers" and almost all of
them were correct comments citing kOS globals (`stPeriFloor`), KSP API types
(`ModuleAnimateGeneric`), enum members (`BoostbackKill`) and local variables. A checker that is
mostly noise gets ignored, which is this project's own most expensive lesson. The vocabulary below
therefore spans every world a comment may legitimately name.
"""
import io, os, re, sys, glob

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..'))          # plugin/
PROJ = os.path.normpath(os.path.join(ROOT, '..'))          # DragonScreen/

# The LIVE KSP tree, never the packaged release - the release is comment-stripped, so its line
# numbers are not the ones a citation means. This is the same trap check_live.py fell into.
F9I = r"C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\Ships\Script"

REFS = [
    r"C:\Users\User\Desktop\mechjeb_src",
    os.path.join(PROJ, 'assets', 'reference', 'AvionicsSystems-master'),
]
CORPUS = r"C:\Users\User\Desktop\quarantine\blackbox_flightdata"
FLIGHTS = r"C:\Users\User\Desktop\quarantine\dragonscreen_flightdata"

IDENT = r'[A-Za-z_][A-Za-z0-9_]*'
WORD = re.compile(IDENT)
SEV = {'WRONG': 0, 'STALE': 1, 'CHECK': 2}

findings = []


def flag(sev, path, line, what, detail):
    findings.append((SEV[sev], sev, os.path.relpath(path, PROJ), line, what, detail))


def read(p):
    return io.open(p, encoding='utf-8', errors='replace').read()


def cs_files(sub):
    out = []
    for root, _, names in os.walk(os.path.join(ROOT, sub)):
        out += [os.path.join(root, n) for n in names if n.endswith('.cs')]
    return sorted(out)


def md_files():
    """
    The prose that documents the code: README, CLAUDE.md and everything under docs/.

    ⛔ THESE ROT FASTEST OF ALL. A stale line in CLAUDE.md is read at the start of every session and
    believed - the build section claimed C# 5 and `csc.exe` for a day after the switch to Roslyn,
    and the state header claimed 145 recorder columns and "almost none of it has flown" long after
    both were false. Prose gets no compiler either, and it has a much bigger audience.
    """
    out = []
    for name in ('README.md', 'CLAUDE.md'):
        p = os.path.join(PROJ, name)
        if os.path.isfile(p):
            out.append(p)
    d = os.path.join(PROJ, 'docs')
    if os.path.isdir(d):
        for root, _, names in os.walk(d):
            out += [os.path.join(root, n) for n in names if n.endswith('.md')]
    return sorted(out)


def prose_lines(text):
    """Markdown has no comment syntax - every line is the claim. Fenced code is skipped."""
    out, fenced = [], False
    for i, ln in enumerate(text.split('\n'), 1):
        if ln.lstrip().startswith('```'):
            fenced = not fenced
            continue
        if not fenced:
            out.append((i, ln.strip()))
    return out


def comment_lines(text):
    """(lineno, comment text) for // and /// lines and for /* */ block lines."""
    out, inblock = [], False
    for i, ln in enumerate(text.split('\n'), 1):
        st = ln.strip()
        if inblock:
            out.append((i, st.lstrip('*').strip()))
            if '*/' in st:
                inblock = False
            continue
        if st.startswith('/*'):
            inblock = '*/' not in st
            out.append((i, st.strip('/*').strip()))
            continue
        k = ln.find('//')
        if k >= 0 and ln.count('"', 0, k) % 2 == 0:
            out.append((i, ln[k:].lstrip('/').strip()))
    return out


# ---------------------------------------------------------------- vocabulary
def tokens_under(base, exts):
    out = set()
    if not os.path.isdir(base):
        return out
    for root, _, names in os.walk(base):
        for n in names:
            if n.lower().endswith(exts):
                try:
                    out.update(WORD.findall(read(os.path.join(root, n))))
                except Exception:
                    pass
    return out


def build_index(ours):
    """
    What counts as a name that EXISTS, and what every numeric constant is actually set to.

    The vocabulary spans our C# (in ANY position - types, members, locals, parameters), F9I's kOS
    so `stPeriFloor` and `dgAoA` resolve, and the reference trees so KSP/Unity/MechJeb/MAS API
    names resolve. See the header for why this breadth is required rather than generous.
    """
    consts, body = {}, []
    cpat = re.compile(r'\b(?:const|readonly)\s+[A-Za-z0-9_<>\[\]\.]+\s+(' + IDENT +
                      r')\s*=\s*(-?[0-9][0-9_]*\.?[0-9]*)')
    for p in ours:
        t = read(p)
        body.append(t)
        for m in cpat.finditer(t):
            consts[m.group(1)] = m.group(2).replace('_', '')
    joined = '\n'.join(body)

    known = set(WORD.findall(joined))
    known |= tokens_under(F9I, ('.ks',))
    for r in REFS:
        known |= tokens_under(r, ('.cs',))
    # The build scripts name the Unity assemblies (`PhysicsModule`, `InputLegacyModule`) and the
    # optional mod bindings, and the quarantined F9I-era code is legitimate history to cite.
    known |= tokens_under(os.path.join(ROOT, 'build'), ('.py',))
    known |= set(WORD.findall(read(os.path.join(ROOT, 'build.py'))
                              if os.path.isfile(os.path.join(ROOT, 'build.py')) else ''))
    known |= tokens_under(os.path.join(ROOT, 'reference_f9i'), ('.cs',))
    # .NET and third-party types a comment may name that appear in no tree we hold.
    known |= set(['PrivateFontCollection', 'ClickThroughFix', 'ClickThruBlocker', 'FlyControls',
                  'ModuleFreeIva', 'LandingZone2'])
    return known, consts, joined


def f9i_functions():
    """name -> (path, first line, last line), the span running to the next function or EOF."""
    fns = {}
    if not os.path.isdir(F9I):
        return fns
    fpat = re.compile(r'\s*function\s+(' + IDENT + r')')
    for root, _, names in os.walk(F9I):
        for n in names:
            if not n.lower().endswith('.ks'):
                continue
            p = os.path.join(root, n)
            lines = read(p).split('\n')
            marks = []
            for i, ln in enumerate(lines):
                m = fpat.match(ln)
                if m:
                    marks.append((i + 1, m.group(1)))
            for k, (ln, name) in enumerate(marks):
                end = marks[k + 1][0] - 1 if k + 1 < len(marks) else len(lines)
                fns[name] = (p, ln, end)
    return fns


_resolved = {}


def resolve_file(name):
    if name in _resolved:
        return _resolved[name]
    hit = None
    # Case-INSENSITIVE: these are Windows paths and a comment writes `booster.ks` for BOOSTER.ks.
    want = name.lower()
    for base in [F9I] + REFS + [os.path.join(ROOT, 'src')]:
        if not os.path.isdir(base):
            continue
        for root, _, names in os.walk(base):
            for n in names:
                if n.lower() == want:
                    hit = os.path.join(root, n)
                    break
            if hit:
                break
        if hit:
            break
    _resolved[name] = hit
    return hit


# ---------------------------------------------------------------- checks
CITE = re.compile(r'`?(' + IDENT + r'(?:[./][A-Za-z0-9_./]+)??)(\.ks|\.cs)?:(\d+)(?:-(\d+))?`?')


# A line that QUOTES a bad citation in order to correct it is the record of a fix, not a new
# defect. `docs/FLIGHT_SYSTEMS.md` names the Starship citation it used to carry so the next reader
# knows what was wrong with it; re-flagging that would delete the only reason the fix is legible.
CORRECTING = re.compile(r'\bCORRECTED\b|\bwas wrong\b|\bit read\b|\bused to (?:say|read|cite)\b|'
                        r'\bwas cited as\b|\bsuperseded\b|\bthis said\b', re.I)


def contexts(comments, span=3):
    """
    Each comment line paired with its NEIGHBOURS.

    ⛔ MARKERS AND THE CLAIMS THEY EXCUSE LAND ON DIFFERENT LINES. A wrapped comment puts
    "CORRECTED 2026-08-13" two lines above the citation it is correcting, and "EXCEPT `FlyPhasing`,
    WHICH IS LIVE" one line below the deadness claim. Testing a marker against its own line only
    re-flags the record of every fix, which is the fastest way to make a report worth ignoring.
    """
    out = []
    for i, (lineno, c) in enumerate(comments):
        lo, hi = max(0, i - span), min(len(comments), i + span + 1)
        out.append((lineno, c, ' '.join(x[1] for x in comments[lo:hi])))
    return out


def check_citations(path, comments, fns):
    """`File.ks:NNN` must exist; `FunctionName:NNN` must land INSIDE that function."""
    for lineno, c, ctx in contexts(comments):
        # ⛔ DOWNGRADE, NEVER SUPPRESS. A correction QUOTES the citation it is replacing, so a
        # window carrying "CORRECTED" cannot be treated as clean - but neither can it be treated as
        # a defect, or every fixed bug re-reports for ever. It is reported at CHECK.
        #
        # This rule is here because suppression HID A REAL ONE. `Approach.cs` corrected a citation
        # from the dead `StTerminal` to "`StDirectApproach` at `station_ops.ks:695`" - a function
        # that spans 1365-1596, and a line that reads `global stMonoIsp is 184`. The correction was
        # wrong twice over and the marker made this tool silent about it. The true source is
        # `StDirectDv:1361`.
        soft = CORRECTING.search(ctx) is not None
        for m in CITE.finditer(c):
            stem, ext, a, b = m.group(1), m.group(2) or '', int(m.group(3)), m.group(4)
            cite = m.group(0).strip('`')
            base = stem.split('/')[-1].split('.')[0]

            if not ext:
                if base in fns:
                    fp, lo, hi = fns[base]
                    if not (lo <= a <= hi):
                        flag('CHECK' if soft else 'WRONG', path, lineno,
                             'citation outside the function it names',
                             '%s - %s spans %d-%d in %s'
                             % (cite, base, lo, hi, os.path.basename(fp)))
                continue

            target = resolve_file(os.path.basename(stem) + ext)
            if target is None:
                flag('CHECK', path, lineno, 'cited file not found', cite)
                continue
            n = len(read(target).split('\n'))
            hi = int(b) if b else a
            if hi > n:
                flag('CHECK' if soft else 'WRONG', path, lineno,
                     'citation past the end of the file',
                     '%s - %s has %d lines' % (cite, os.path.basename(target), n))


DEAD = re.compile(r'(nothing calls|no callers|called by nothing|read by nothing|never called|'
                  r'is dead|are dead|not wired|wired to nothing|uncalled)', re.I)


# ⛔ A COMMENT MAY NAME A THING IN ORDER TO SAY IT IS *NOT* DEAD, AND USUALLY THAT IS THE MOST
# VALUABLE COMMENT ON THE PAGE. Two real examples this tool flagged on its first run, both correct:
#   "`FlipDeg`, which was documented NOT WIRED *while being called*"
#   "EVERYTHING BELOW IS DEAD BY DECISION - EXCEPT `FlyPhasing`, WHICH IS LIVE"
# Reading the deadness claim without its exception turns the record of a fixed bug into a new one.
ALIVE = re.compile(r'\bexcept\b|\bwhich is live\b|\bis live\b|\bare live\b|while being called|'
                   r'\bnot dead\b|\bstill live\b|should not have been|\bLIVE since\b|\bnow live\b',
                   re.I)


def check_dead_claims(path, comments, joined, fns):
    for lineno, c, ctx in contexts(comments):
        if not DEAD.search(c) or ALIVE.search(ctx):
            continue
        for m in re.finditer('`(' + IDENT + ')`', c):
            name = m.group(1)
            if name in fns:
                fp, lo, hi = fns[name]
                uses = []
                for i, ln in enumerate(read(fp).split('\n'), 1):
                    if ln.strip().startswith('//') or re.match(r'\s*function\s', ln):
                        continue
                    if re.search(r'(?<![A-Za-z0-9_])' + name + r'\s*\(', ln):
                        uses.append(i)
                if uses:
                    flag('WRONG', path, lineno, 'says dead, but it HAS callers',
                         '%s called at %s' % (name, ', '.join(str(u) for u in uses[:3])))
                continue
            hits = len(re.findall(r'(?<![A-Za-z0-9_])' + name + r'(?![A-Za-z0-9_])', joined))
            if hits > 1:
                flag('CHECK', path, lineno, 'says dead/unwired but the name is still referenced',
                     '%s appears %d times in src/' % (name, hits))


def check_ghost_identifiers(path, comments, known, fns):
    # A correction NAMES the thing it is correcting - `SteeringCorrections` appears only so the
    # next reader knows which Starship function the doc used to quote. Same rule as the citations.
    for lineno, c, ctx in contexts(comments):
        if CORRECTING.search(ctx):
            continue
        for m in re.finditer('`(' + IDENT + ')`', c):
            n = m.group(1)
            if n in known or n in fns or len(n) < 5:
                continue
            if not re.match(r'^[a-z]+[A-Z]|^[A-Z][a-z]+[A-Z]', n):
                continue
            flag('STALE', path, lineno, 'names something that exists nowhere', '`%s`' % n)


VAL_FORMS = [r'`?{0}`?\s*(?:=|is|to|of|at)\s*`?(-?[0-9]+(?:\.[0-9]+)?)`?',
             r'`?{0}`?\s*\(\s*(-?[0-9]+(?:\.[0-9]+)?)\s*\)']


def check_const_values(path, comments, consts):
    for lineno, c in comments:
        for name, real in consts.items():
            # ⛔ WORD BOUNDARIES AND A LENGTH FLOOR, OR THIS CHECK IS ALL NOISE. A plain substring
            # test made `Radius` match inside `dockingcorridorRadius`, `ToleranceM` inside
            # `StandoffToleranceM`, and one-letter constants like `G` match any capital G in prose.
            if len(name) < 5 or not re.search(r'\b' + re.escape(name) + r'\b', c):
                continue
            for f in VAL_FORMS:
                m = re.search(r'\b' + f.format(re.escape(name)), c)
                if not m:
                    continue
                # A DERIVATION is not a statement of a value. "StandoffM - StandoffToleranceM =
                # 25 - 12 = 13 m" states the arithmetic of a subtraction, and the 25 belongs to the
                # OTHER constant. Two or more '=' on one line is a chain of working, not a claim.
                if c.count('=') >= 2:
                    break
                # An expression, not a statement of the value: "`9200 / AimGain` = 13 700". The
                # operator can sit either side of the name, so look at the span AND its lead-in.
                span = c[max(0, m.start() - 26):m.end()]
                if any(op in span for op in ('/', '*', '+', '^')):
                    break
                try:
                    sv, rv = float(m.group(1)), float(real)
                except ValueError:
                    break
                if abs(sv - rv) < 1e-9:
                    break
                # A comment may legitimately state a metre constant in km, a fraction as a
                # percentage, or a duration in minutes. Only a real disagreement is a finding.
                if any(abs(sv * k - rv) < 1e-6 * max(1.0, abs(rv))
                       for k in (1000.0, 0.001, 100.0, 0.01, 60.0, 1.0 / 60.0)):
                    break
                flag('STALE', path, lineno, 'states a value the constant no longer has',
                     '%s: comment says %s, code says %s' % (name, m.group(1), real))
                break


REC = re.compile(r'`?(bb_[A-Za-z0-9_]+|flight_[0-9_]+|(?:Cargo|Crew)Dragon_[0-9]+|'
                 r'booster_[0-9]+|upper_[0-9]+)(?:\.csv)?`?')


ACKED = re.compile(r'not in (?:our |the )archive|does not exist|NOT IN OUR ARCHIVE', re.I)


def check_paths(path, comments):
    for lineno, c, ctx in contexts(comments):
        # ⛔ AN ACKNOWLEDGED ABSENCE IS NOT A DEFECT. A comment that cites a recording AND says the
        # recording is missing is the most honest form the claim can take - `bb_dragon_CrewDragon_072`
        # is the stated source of the entry AoA schedule and genuinely is not in the corpus. Flagging
        # it for ever would train us to skim the report, which is how a checker dies.
        # The acknowledgement lives next to the claim rather than in an ignore-file, so it cannot
        # drift away from what it is excusing.
        acked = ACKED.search(ctx) is not None

        for m in re.finditer(r'(?<![/>\w])`?((?:docs|assets|plugin)/[A-Za-z0-9_./-]+\.[a-z]{2,4})`?', c):
            if not os.path.exists(os.path.join(PROJ, m.group(1))) and not acked:
                flag('WRONG', path, lineno, 'referenced file does not exist', m.group(1))
        for m in REC.finditer(c):
            stem = m.group(1)
            if stem.endswith('_') or acked:
                continue
            found = False
            for d in (CORPUS, FLIGHTS):
                if os.path.isdir(d) and glob.glob(os.path.join(d, '*' + stem + '*')):
                    found = True
                    break
            if not found:
                flag('WRONG', path, lineno, 'cited recording is not in the archive', stem)


# ---------------------------------------------------------------- report
def main():
    args = [a for a in sys.argv[1:] if not a.startswith('-')]
    brief = '--brief' in sys.argv

    ours = cs_files('src') + cs_files('test')
    docs = md_files()
    everything = ours + docs
    target = [p for p in everything if any(os.path.basename(p) == os.path.basename(a) or a in p
                                           for a in args)] if args else everything
    if args and not target:
        print('nothing matched %s' % ', '.join(args))
        return 2

    known, consts, joined = build_index(ours)
    fns = f9i_functions()
    if not fns:
        print('WARNING: no F9I scripts under %s - citation checks are DISABLED\n' % F9I)

    ncomments = 0
    for p in target:
        cs = prose_lines(read(p)) if p.endswith('.md') else comment_lines(read(p))
        ncomments += len(cs)
        check_citations(p, cs, fns)
        check_dead_claims(p, cs, joined, fns)
        check_ghost_identifiers(p, cs, known, fns)
        check_const_values(p, cs, consts)
        check_paths(p, cs)

    findings.sort()
    counts = {}
    for f in findings:
        counts[f[1]] = counts.get(f[1], 0) + 1

    print('audited %d comment lines in %d files against %d F9I functions, %d constants, '
          '%d known names\n' % (ncomments, len(target), len(fns), len(consts), len(known)))

    if not brief:
        cur = None
        for _, sev, rel, line, what, detail in findings:
            if rel != cur:
                cur = rel
                print('  %s' % cur)
            print('    %-6s %5d  %-44s %s' % (sev, line, what, detail))
        if findings:
            print('')

    print('WRONG %d   STALE %d   CHECK %d'
          % (counts.get('WRONG', 0), counts.get('STALE', 0), counts.get('CHECK', 0)))
    print('  WRONG - a stated fact that is verifiably false. Fix or delete the comment.')
    print('  STALE - almost certainly left behind by a rename or a retune.')
    print('  CHECK - unresolvable; look once, then fix it or make it resolvable.')
    return 1 if counts.get('WRONG') else 0


if __name__ == '__main__':
    sys.exit(main())
