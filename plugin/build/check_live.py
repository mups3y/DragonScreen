#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
check_live.py - is that F9I function actually FLOWN, or is it dead code?

    python plugin/build/check_live.py Flip1 Flip2 Reentry1 AtmGNC
    python plugin/build/check_live.py --audit          # every citation in our source

WHY THIS EXISTS
---------------
Three flights in a row were lost to one mistake: porting `Flip2` and `Reentry1`, which have NO
CALLERS, while the live path was `Flip1` and `AtmGNC`. Two of those "fixes" made the booster roll
measurably worse. CLAUDE.md warns about exactly this in prose, and the prose did not stop it
happening three times.

    F9I contains paths its own author DISABLED after they lost vehicles.
    Read what the source does with a function, not just what it contains.

So: before a function name goes into a doc comment as a citation, it goes through here.
"""
import os, re, sys, glob

# ---- THE LIVE KSP TREE, NOT THE RELEASE. ----
# This pointed at `Desktop\Falcon 9 Interface\Ships\Script`, which is the PACKAGED RELEASE, and the
# release is COMMENT-STRIPPED. Every line number this tool ever printed was a line number in a file
# that does not exist on disk in that form: it reported `Flip1` defined at BOOSTER.ks:146 and called
# at :108 and :113, when the live file has the definition at :295 and the calls at :226 and :231.
# The LIVE/DEAD verdicts were still right - the call graph survives comment stripping - but the whole
# point of this tool is to produce a CITATION, and a citation with a wrong line number is exactly
# what RULE 1 exists to prevent.
#
# Two further reasons the release is the wrong source, both worse than the line numbers:
#   · it is a SNAPSHOT (v1.1.0) and lags the live tree. `COMMON/TIME.ks` was deleted from the live
#     tree on 2026-08-04 and the tagged release still carries it - so the tool would report its
#     functions as present in code that no longer flies.
#   · "verified live" has to mean the code that actually flew, and that is the KSP install.
F9I_ROOTS = [
    r"C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\Ships\Script",
]
OUR_SRC = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src")


def ks_files():
    out = []
    for root in F9I_ROOTS:
        for dirpath, _, names in os.walk(root):
            out += [os.path.join(dirpath, n) for n in names if n.lower().endswith(".ks")]
    return out


def load():
    """Every .ks file's text, keyed by path."""
    text = {}
    for f in ks_files():
        try:
            text[f] = open(f, encoding="utf-8", errors="replace").read()
        except IOError:
            pass
    return text


def analyse(name, text):
    """Where is it defined, and who calls it?"""
    defs, calls = [], []
    dpat = re.compile(r"^\s*function\s+" + re.escape(name) + r"\s*\{", re.M)
    cpat = re.compile(r"(?<![A-Za-z0-9_])" + re.escape(name) + r"\s*\(")
    for path, body in text.items():
        lines = body.split("\n")
        for m in dpat.finditer(body):
            defs.append((path, body[:m.start()].count("\n") + 1))
        for m in cpat.finditer(body):
            ln = body[:m.start()].count("\n") + 1
            src = lines[ln - 1]
            # the definition line is not a call, and neither is a commented-out one
            if re.match(r"^\s*function\s", src):
                continue
            if src.lstrip().startswith("//"):
                continue
            calls.append((path, ln, src.strip()[:70]))
    return defs, calls


def report(name, text):
    defs, calls = analyse(name, text)
    if not defs:
        print("  %-24s NOT FOUND - no `function %s` anywhere in the F9I tree" % (name, name))
        return "missing"
    where = ", ".join("%s:%d" % (os.path.relpath(p, F9I_ROOTS[0]), l) for p, l in defs)
    if not calls:
        print("  %-24s *** DEAD *** defined at %s, CALLED BY NOTHING" % (name, where))
        print("  %-24s     do not port it. find what the live path uses instead." % "")
        return "dead"
    print("  %-24s LIVE  (%s) - %d call site(s):" % (name, where, len(calls)))
    for p, l, src in calls[:4]:
        print("  %-24s     %s:%d  %s" % ("", os.path.relpath(p, F9I_ROOTS[0]), l, src))
    return "live"


def audit(text):
    """Scan OUR source for anything that looks like an F9I citation and check each one."""
    names = {}
    pat_fn = re.compile(r"\b([A-Z][A-Za-z0-9_]{3,})\s*(?::\d+|\(\))")
    known = set()
    for body in text.values():
        for m in re.finditer(r"^\s*function\s+([A-Za-z0-9_]+)", body, re.M):
            known.add(m.group(1))

    for dirpath, _, files in os.walk(OUR_SRC):
        for fn in files:
            if not fn.endswith(".cs"):
                continue
            path = os.path.join(dirpath, fn)
            body = open(path, encoding="utf-8", errors="replace").read()
            for ln, line in enumerate(body.split("\n"), 1):
                if "//" not in line and "///" not in line:
                    continue
                for m in pat_fn.finditer(line):
                    nm = m.group(1)
                    if nm in known:
                        names.setdefault(nm, []).append("%s:%d" % (fn, ln))
    if not names:
        print("no F9I function citations found in our source")
        return 0
    print("Citations found in our source, checked against the F9I tree:\n")
    bad = 0
    for nm in sorted(names):
        state = report(nm, text)
        if state != "live":
            bad += 1
            print("  %-24s     cited at: %s" % ("", ", ".join(names[nm][:5])))
        print("")
    print("%d cited function(s); %d NOT LIVE." % (len(names), bad))
    return 1 if bad else 0


def main():
    args = sys.argv[1:]
    text = load()
    if not text:
        print("no .ks files found - check F9I_ROOTS"); return 2
    print("scanned %d F9I script files\n" % len(text))
    if not args or args[0] == "--audit":
        return audit(text)
    bad = 0
    for name in args:
        if report(name, text) != "live":
            bad += 1
        print("")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
