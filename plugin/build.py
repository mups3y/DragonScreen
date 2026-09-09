#!/usr/bin/env python
"""
DragonScreen build script.

RECIPE INHERITED FROM F9_LOP, which shipped from this exact rig - do not re-derive it.
No IDE, no MSBuild, no NuGet: csc.exe straight against KSP's managed assemblies.

    python build.py          # build the plugin DLL
    python build.py test     # build and run the headless tests (no KSP needed) - the C# suites,
                            #   then the python tool selftests (BB3's report generator)
    python build.py preview  # render the pages to build/preview/*.png (no KSP needed)
    python build.py install  # build, then copy GameData/DragonScreen into the KSP install
    python build.py mechwarn  # MJ1: rewrite plugin/mech/WARNINGS.txt, the vendored MechJeb's warning
                              #   baseline, by recompiling it at -warn:4. Diagnostic only: it builds
                              #   nothing that ships. Diff this file at a re-pin.
    python build.py harnesscheck        # S167: prove a THROWING suite still leaves a complete report
    python build.py previewdiff [ref]   # S168: render `ref` (default HEAD) and the working tree, and
                              #   report which pages changed, by hash. THE instrument behind every
                              #   "N pages changed" a register line claims - see S168 for why it is
                              #   code and not a habit.

THE ONE THING THAT WILL BITE YOU: a DLL change needs a full game restart, and so does a cfg change -
ModuleManager applies patches at load. There is no in-flight reload worth trusting. KSP must be
closed to overwrite the DLL, AND SO MUST CKAN, which keeps the GameData tree open.

That is why `preview` exists: restarts are the scarce resource, so anything that can be judged
outside the game - layout, proportion, palette, legibility - is judged from a PNG, and a restart is
spent only on what needs the capsule.
"""
import io, os, re, subprocess, sys, shutil, hashlib, tempfile, time

NL = chr(10)          # response-file line separator, spelled out so no edit can eat the escape

HERE = os.path.dirname(os.path.abspath(__file__))
# The REPO root, one above plugin/. S168's previewdiff runs git here, never in plugin/.
ROOT = os.path.dirname(HERE)
KSP  = r'C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program'
MAN  = os.path.join(KSP, 'KSP_x64_Data', 'Managed')
# ---------------------------------------------------------------- the compiler
# ⛔ THE LANGUAGE VERSION AND THE RUNTIME TARGET ARE TWO KNOBS, AND WE HAD THEM WELDED TOGETHER.
#
# This used the csc that ships inside Windows itself - the 2012 .NET Framework compiler, which caps
# at C# 5. That was a deliberate "no IDE, no MSBuild, no NuGet" decision and it was a good one, but
# it was recorded as if C# 5 were a KSP requirement. It is not. Most modern C# - `$"..."`, `?.`,
# `=>` members, pattern matching - is COMPILE-TIME SUGAR that lowers to ordinary IL, and KSP's
# Unity 2019.4 Mono runs it happily. It is exactly how MechJeb ships modern C# into this same game.
#
# So: prefer Roslyn when it is present, targeting KSP's OWN mscorlib via -nostdlib, and fall back to
# the Framework compiler when it is not. The fallback keeps the project buildable on a bare machine;
# it just cannot compile modern syntax, and it says so rather than emitting a wall of parse errors.
#
# What this bought, concretely: porting MechJebLib's FuelFlowSimulation needed ~95 hand edits to
# downgrade its syntax. Every hand edit to flight-proven code is a chance to introduce a bug, and
# hand edits are where most of this project's regressions came from. That count is now zero.
#
# Roslyn also has -deterministic, which the legacy compiler does not (see `_same`): identical
# sources now produce a byte-identical DLL, so an unchanged build can be recognised as unchanged.
CSC_LEGACY = r'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
DOTNET_SDK = r'C:\Program Files\dotnet\sdk'


def _find_roslyn():
    """Newest Roslyn csc in the installed .NET SDKs, or None."""
    if not os.path.isdir(DOTNET_SDK):
        return None
    best = None
    for v in os.listdir(DOTNET_SDK):
        cand = os.path.join(DOTNET_SDK, v, 'Roslyn', 'bincore', 'csc.exe')
        if os.path.isfile(cand):
            try:
                key = tuple(int(x) for x in v.split('-')[0].split('.'))
            except ValueError:
                key = (0,)
            if best is None or key > best[0]:
                best = (key, cand)
    return best[1] if best else None


ROSLYN  = _find_roslyn()
CSC     = ROSLYN or CSC_LEGACY
MODERN  = ROSLYN is not None

# ⚠ WITH -nostdlib WE MUST NAME THE CORE ASSEMBLIES OURSELVES, and they must be KSP's, not the
# SDK's. Referencing .NET 10 reference assemblies would compile and then fail to load in the game.
CORE_REFS = ['mscorlib.dll', 'System.dll', 'System.Core.dll']

# Assembly-CSharp = KSP itself. UnityEngine is SPLIT INTO MODULES in 2019.4, and the split is not
# intuitive - the compiler will tell you which one it wants, in a CS0012 error naming the assembly.
#   CoreModule          MonoBehaviour, Texture2D, Rect, Color, Debug
#   IMGUIModule         GUI, GUILayout, GUIStyle, GUISkin        (the window itself)
#   TextRenderingModule TextAnchor, Font                          (GUIStyle.alignment lives here)
#   ImageConversionModule Texture2D.LoadImage / EncodeToPNG        (extension methods, not on the type)
#   UI                  uGUI, not used yet but will be for sprites
#   PhysicsModule       Collider, Raycast     -- NOT in CoreModule, which is the surprise. Needed the
#                       moment the buttons get click handling, and already needed just to ASK whether
#                       a transform has a collider.
#   InputLegacyModule   Input, KeyCode        -- the old Input class, split out in 2019.4 when the
#                       new Input System arrived. KSP still uses the legacy one throughout.
REFS = ['Assembly-CSharp.dll', 'UnityEngine.dll', 'UnityEngine.CoreModule.dll',
        'UnityEngine.IMGUIModule.dll', 'UnityEngine.TextRenderingModule.dll',
        'UnityEngine.ImageConversionModule.dll', 'UnityEngine.UI.dll',
        'UnityEngine.PhysicsModule.dll', 'UnityEngine.InputLegacyModule.dll',
        'UnityEngine.AudioModule.dll']   # AudioSource / AudioClip for the abort klaxon

# References to OTHER GameData mods, not to KSP. Empty on purpose right now.
#
# The F9I-era build referenced 000_ClickThroughBlocker for ClickThruBlocker.GUIWindow, which stops
# clicks on a floating IMGUI panel reaching the game world behind it. Nothing in the current sources
# draws an IMGUI window - the content goes into a RenderTexture on the IVA prop - so referencing it
# would declare a hard dependency on a mod this plugin does not use. It comes back on the day the
# floating DEVELOPMENT view lands, and not before; the old code is in reference_f9i/.
EXTRA_REFS = []

MOD     = 'DragonScreen'
OUT_DLL = os.path.join(HERE, 'GameData', MOD, MOD + '.dll')

# ---------------------------------------------------------------- S262: WHICH BRANCH IS THIS?
# ⛔⛔ THE BRANCH `install` IS ALLOWED TO SHIP WITHOUT BEING ASKED TWICE.
#
# ⚠ CHANGE THIS ONE CONSTANT WHEN THE REBUILD LANDS ON ANOTHER BRANCH. It is deliberately a single
# named string in one place: a branch name hardcoded in three places is a branch name that will be
# wrong in two of them. `python build.py install --branch <name>` is the escape for a deliberate
# one-off, and it must NAME the branch, so an accident cannot become a silent bypass.
#
# ⛔ WHY THIS EXISTS. 2026-09-10: the owner merged a pull request in GitHub Desktop, which checked the
# repo out to `master`. Three minutes later `install` compiled and shipped MASTER's build into the
# game - a state 22 commits behind, predating S240 - and overwrote `DragonScreen.cfg` with master's.
# It printed `ALL SUITES PASSED` and `--- ok`, and BOTH WERE TRUE OF THE WRONG CODE. Nothing in the
# output named a branch, so there was nothing to notice.
# ⭐ The accident was ordinary; a person clicked a button in a GUI. THE TOOL'S SILENCE IS THE DEFECT.
EXPECT_BRANCH = 'rebuild/base-screens'

# ---------------------------------------------------------------- the embedded MechJeb (T15a)
# `plugin/mech/` is a VENDORED copy of MuMech/MechJeb2 at a pinned commit - see
# `plugin/mech/VENDOR.md` for the pin, the exclusions and the two licence checks. It is
# deliberately NOT under `src/`, because `sources('src')` sweeps a whole tree and this one has
# parts that must not be swept: MechJebKos needs kOS.dll (an external mod, and C7 forbids
# reading the KSP install for it) and MechJebLibTest needs xunit. Both are vendored whole and
# neither compiles here, so the compile set has to be a NAMED list, not a directory walk.
#
# §B3/§B12.1: it builds as its own PRIVATE ASSEMBLY, `DragonScreen.Mech.dll`, with every
# vendored file wrapped in `namespace DragonScreen.Mech`. Both halves matter - a user running
# their own MechJeb2.dll must never see a type of ours collide with one of theirs.
MECH          = os.path.join(HERE, 'mech')
MECH_DLL      = os.path.join(HERE, 'GameData', MOD, MOD + '.Mech.dll')
MECH_PROJECTS = ['_dragonscreen', 'alglib', 'MechJebLib', 'MechJebLibBindings', 'MechJeb2']

# MechJeb reaches further into Unity than the screens do. These four are ON TOP of REFS and
# come straight from `mech/MechJeb2/MechJeb2.csproj` + `MechJebLibBindings.csproj`, which are
# vendored beside the source precisely so this list can be checked against upstream's own.
MECH_REFS = REFS + ['Assembly-CSharp-firstpass.dll', 'UnityEngine.AnimationModule.dll',
                    'UnityEngine.AssetBundleModule.dll', 'UnityEngine.VehiclesModule.dll']

# Upstream's own AssemblyInfo per project (five separate assemblies there, one here - keeping
# them would mean five AssemblyTitles in one DLL) and its ReSharper suppression files (
# assembly-level attributes, which cannot sit inside the private namespace and mean nothing
# without the analyser). Vendored, not compiled; VENDOR.md says so out loud.
MECH_SKIP = ('properties/assemblyinfo.cs', 'globalsuppressions.cs')

# ---------------------------------------------------------------- the three [KSPAddon]s (T15b)
# ⛔ THESE THREE ARE A SAFETY EXCLUSION, NOT TIDINESS, AND THEY MUST NOT COME BACK.
#
# KSP instantiates [KSPAddon] classes by SCANNING EVERY ASSEMBLY IN GameData. Nobody has to
# attach anything: no part cfg, no MechJebCore, no user action. Vendoring MechJeb's whole tree
# (§B12.1a, and correct) therefore ships three self-starting MonoBehaviours that would run
# inside DragonScreen's own assembly and speak with MechJeb's voice:
#
#   MechJeb2/CompatibilityChecker.cs:52   Startup.Instantly - version check, can raise a POPUP
#   MechJeb2/InstallChecker.cs:20         Startup.MainMenu  - MechJeb's "you installed it wrong"
#                                         POPUP. It looks for GameData/MechJeb2, and our layout
#                                         deliberately has none, so it is LIKELY TO FIRE.
#   MechJeb2/MechjebBundlesManager.cs:14  Startup.MainMenu  - loads Bundles/shaders.bundle from
#                                         a path we do not ship; for a user who runs the real
#                                         MechJeb2 it is a path ALREADY LOADED BY THEM, and
#                                         Unity refuses the second load.
#
# §B12.1a: the ported GUI must be "vendored but never registered/shown" - ported != enabled. An
# addon that registers ITSELF is precisely what that forbids, so the answer is to keep all three
# in the tree (the pin stays complete) and take them out of the COMPILE, which is the same
# distinction T15a already drew for MechJebKos/ and MechJebLibTest/.
#
# ⚠ THE CLASS NAMES ARE NOT ALL THE FILE NAMES. MechjebBundlesManager.cs declares
# `MechJebBundlesManager` (different capital J), which is why this list is BY PATH.
#
# Only the third has compiled dependents - GuiUtils.cs:905-906 and MechJebModuleDebugArrows.cs
# read its three statics - so it, and only it, has a substitute: _dragonscreen/_BundlesManager.cs.
# The other two are referenced by nothing outside their own file (verified by grep over the whole
# compiled set, T15b) and are simply gone.
MECH_ADDONS_EXCLUDED = ('mechjeb2/compatibilitychecker.cs',
                        'mechjeb2/installchecker.cs',
                        'mechjeb2/mechjebbundlesmanager.cs')


def mech_sources():
    """The .cs that go into DragonScreen.Mech.dll - named projects only, see MECH_PROJECTS."""
    out = []
    seen_addons = set()
    for proj in MECH_PROJECTS:
        p = os.path.join(MECH, proj)
        if not os.path.isdir(p):
            continue
        for root, _, files in os.walk(p):
            for f in files:
                if not f.endswith('.cs'):
                    continue
                full = os.path.join(root, f)
                rel  = os.path.relpath(full, MECH).replace('\\', '/').lower()
                if any(rel.endswith(s) for s in MECH_SKIP):
                    continue
                if rel in MECH_ADDONS_EXCLUDED:
                    seen_addons.add(rel)
                    continue
                out.append(full)
    # A re-pin that renames or moves one of the three [KSPAddon] files would silently put a
    # self-registering MonoBehaviour back into the shipped assembly - the one thing T15b exists
    # to prevent. Fail the build instead, loudly, rather than discover it in the capsule.
    if out:
        missing = [a for a in MECH_ADDONS_EXCLUDED if a not in seen_addons]
        if missing:
            sys.exit('vendored tree has moved or renamed a [KSPAddon] file that MUST stay out of\n'
                     '    the compile: %s\n'
                     '    Find it, update MECH_ADDONS_EXCLUDED, and re-check plugin/mech/VENDOR.md '
                     '§3.1.' % ', '.join(missing))
    return sorted(out)


def sources(*dirs):
    out = []
    for d in dirs:
        p = os.path.join(HERE, d)
        if not os.path.isdir(p):
            continue
        for root, _, files in os.walk(p):
            out += [os.path.join(root, f) for f in files if f.endswith('.cs')]
    return sorted(out)


def preflight():
    bad = False
    if not os.path.isfile(CSC):
        print('MISSING compiler: %s' % CSC); bad = True
    if MODERN:
        for r in CORE_REFS:
            if not os.path.isfile(os.path.join(MAN, r)):
                print('MISSING core reference: %s' % os.path.join(MAN, r)); bad = True
    for r in sorted(set(REFS) | set(MECH_REFS)):
        if not os.path.isfile(os.path.join(MAN, r)):
            print('MISSING reference: %s' % os.path.join(MAN, r)); bad = True
    for r in EXTRA_REFS:
        if not os.path.isfile(r):
            print('MISSING mod reference: %s' % r); bad = True
    if bad:
        sys.exit('preflight failed - nothing built')


def compile_cs(out, src, refs=(), exe=False, extra=(), warn='4', langversion='latest'):
    """
    One invocation, either compiler, via a RESPONSE FILE.

    The response file is not decoration: the reference and source lists run to thousands of
    characters and every KSP path contains spaces. Passing them on the command line is how you get
    `Source file 'C:/Program Files' could not be found`.
    """
    args = ['-nologo', '-warn:' + warn,
            '-target:' + ('exe' if exe else 'library'),
            '-out:' + out]
    if MODERN:
        # KSP's core assemblies, never the SDK's. See CORE_REFS.
        args += ['-langversion:' + langversion, '-deterministic', '-nostdlib']
        args += ['-reference:' + os.path.join(MAN, r) for r in CORE_REFS]
    args += ['-reference:' + r for r in refs]
    args += list(extra)
    args += list(src)

    rsp = os.path.join(HERE, 'build', 'csc.rsp')
    os.makedirs(os.path.dirname(rsp), exist_ok=True)
    with io.open(rsp, 'w', encoding='utf-8') as f:
        for a in args:
            # QUOTE THE VALUE, NOT THE FLAG. A reference path starts with a dash and contains a
            # space; quoting the whole token makes csc read the flag as part of a filename, and
            # leaving it bare splits the path at the space. Split on the first colon.
            if ' ' not in a:
                f.write(a + NL)
            elif a.startswith('-') and ':' in a:
                flag, _, val = a.partition(':')
                f.write(flag + ':"' + val + '"' + NL)
            else:
                f.write('"' + a + '"' + NL)
    return [CSC, '@' + rsp]


def run(args, label):
    print('--- %s' % label)
    p = subprocess.run(args, capture_output=True, text=True)
    out = (p.stdout or '') + (p.stderr or '')
    # csc is chatty about its banner; only surface warnings and errors.
    for line in out.splitlines():
        if 'warning' in line.lower() or 'error' in line.lower():
            print('   ' + line)
    if p.returncode != 0:
        sys.exit('%s FAILED (exit %d)' % (label, p.returncode))
    return out


def build_mech():
    """
    The embedded MechJeb (T15a), as its own assembly. Returns [] if the tree is not vendored.

    WHY A SEPARATE ASSEMBLY AND NOT JUST A NAMESPACE. §B12.1 asks for "a private namespace +
    assembly", and the two do different jobs. The namespace stops a TYPE clashing with a user's
    own MechJeb2.dll; the separate assembly is what lets this compile at all, because vendored
    MechJeb needs a different compiler contract from our code: LangVersion 8 (upstream's own
    setting - so nothing here silently starts depending on a newer C# than upstream compiles
    with), nullable ANNOTATIONS without the warnings, its own wider Unity reference set, and
    the UNITY_2017_1 define its csproj sets. Welding all that onto the screens' build would
    push third-party constraints onto our code for no gain.

    ⛔ WARNINGS ARE OFF (-warn:0) FOR THIS ASSEMBLY, AND THAT IS DELIBERATE, NOT LAZINESS.
    §B12.1's rule is "source kept intact (not rewritten) - rename shell only": we are not
    permitted to fix MechJeb's warnings, so printing a few hundred of them on every single
    build would train the eye to scroll past exactly the region where a REAL error appears.
    Errors are unaffected - csc still reports them and run() still fails the build on them.
    """
    src = mech_sources()
    if not src:
        return []
    os.makedirs(os.path.dirname(MECH_DLL), exist_ok=True)
    if not MODERN:
        sys.exit('the embedded MechJeb needs Roslyn (C#8); the C#5 fallback compiler cannot '
                 'build plugin/mech - install a .NET SDK')
    run(compile_cs(MECH_DLL, src,
                   refs=[os.path.join(MAN, r) for r in MECH_REFS],
                   extra=['-optimize+', '-define:UNITY_2017_1', '-nullable:annotations'],
                   warn='0', langversion='8'),
        'embedded MechJeb (%d source files)' % len(src))
    print('    -> %s  (%.1f KB)' % (MECH_DLL, os.path.getsize(MECH_DLL) / 1024.0))
    return [MECH_DLL]


def build_plugin():
    preflight()
    mech = build_mech()
    src = sources('src')
    if not src:
        sys.exit('no sources in src/ - nothing to build')
    os.makedirs(os.path.dirname(OUT_DLL), exist_ok=True)
    run(compile_cs(OUT_DLL, src,
                   refs=[os.path.join(MAN, r) for r in REFS] + list(EXTRA_REFS) + mech,
                   extra=['-optimize+']),
        'plugin (%d source files)%s' % (len(src), '' if MODERN else '  [C#5 fallback]'))
    print('    -> %s  (%.1f KB)' % (OUT_DLL, os.path.getsize(OUT_DLL) / 1024.0))


def build_tests():
    """
    Headless tests build against src/pure ONLY - no KSP, no Unity. That is the whole point of the
    pure/glue split: F9_LOP's maths was validated 37/37 headless while its KSP glue, which had no
    tests, is where every failed flight came from. Keep the testable half genuinely dependency-free.
    """
    src = sources('src/pure', 'test')
    if not src:
        print('--- no tests yet (src/pure + test are empty)'); return
    exe = os.path.join(HERE, 'build', 'DragonScreenTest.exe')
    os.makedirs(os.path.dirname(exe), exist_ok=True)
    # ⚠ THE TESTS COMPILE src/pure TOO, so they must use the SAME compiler as the plugin - or
    # `src/pure` is pinned to whatever the older of the two accepts, which defeats the switch
    # entirely. This is where the MechJebLib port will land.
    run(compile_cs(exe, src, exe=True), 'tests (%d source files)' % len(src))
    print('--- running tests')
    p = subprocess.run([exe], capture_output=True, text=True)
    out = (p.stdout or '') + (p.stderr or '')
    print(out)
    if p.returncode != 0:
        sys.exit('TESTS FAILED (exit %d)' % p.returncode)
    harness_fault_check(exe, out)
    tool_tests()


def harness_fault_check(exe=None, clean_out=None):
    """
    S167: PROVE THE TEST HARNESS STILL REPORTS EVERYTHING WHEN ONE SUITE THROWS.

    A suite returning a failure count is the designed path. A suite THROWING was not caught anywhere,
    so it reached the CLR, killed the process, and every suite registered below it never ran - and the
    report gave no counts for them at all, so a reader could not tell "green" from "never asked". S164
    found this by mutation: its kill came from a suite that was not the one under test.

    MEASURED before the fix (2026-09-06): a NullReferenceException at the top of AudioScopeTest - suite
    25 of 55 - left 25 suites with counts, one with a banner and no count, and 29 that never ran.

    ⚠ WHY THIS IS A PROCESS-LEVEL CHECK AND NOT A C# ONE. `test/HarnessTest.cs` proves the in-process
    half (a throw becomes a named failure of that suite and control returns). It cannot prove the other
    half, because "every suite BELOW the crash still ran" is a claim about a whole process: a check
    running inside that process could only ever show that IT had survived. So this re-runs the built
    exe with the fault seam armed and compares the two reports LINE FOR LINE.

    ⭐ THE COMPARISON IS THE POINT, and it is why this cannot rot: it does not look for a hand-listed
    set of suite names that would go stale the next time one is registered. It asserts that the faulted
    run's output CONTAINS EVERY LINE the clean run produced - so a suite added, renamed or reordered in
    TestMain.cs is covered on the day it lands, with no edit here.
    """
    exe = exe or os.path.join(HERE, 'build', 'DragonScreenTest.exe')
    if not os.path.exists(exe):
        sys.exit('HARNESS CHECK: no test exe at %s - run `build.py test` first' % exe)
    if clean_out is None:
        c = subprocess.run([exe], capture_output=True, text=True)
        clean_out = (c.stdout or '') + (c.stderr or '')
        if c.returncode != 0:
            sys.exit('HARNESS CHECK: the CLEAN run is already failing - fix that first')
    print('--- harness fault check (S167: a throwing suite must not hide the ones below it)')

    env = dict(os.environ)
    env['DRAGONSCREEN_HARNESS_FAULT'] = '1'
    f = subprocess.run([exe], capture_output=True, text=True, env=env)
    faulted = (f.stdout or '') + (f.stderr or '')

    # The closing summary DIFFERS by design (one says ALL SUITES PASSED, the other counts the crash),
    # so it is the one line excluded from the "every clean line survives" comparison.
    def body(text):
        return [l for l in text.splitlines()
                if l.strip() and 'SUITES PASSED' not in l and 'SUITE(S) FAILED' not in l]

    clean_lines, faulted_set = body(clean_out), set(body(faulted))
    missing = [l for l in clean_lines if l not in faulted_set]

    problems = []
    if f.returncode == 0:
        problems.append('the faulted run exited 0 - a crashing suite MUST fail the build')
    if 'SUITE CRASHED' not in faulted or 'DeliberateFaultInjection' not in faulted:
        problems.append('the crash was not reported as a NAMED suite failure')
    if 'BY THROWING' not in faulted:
        problems.append('the summary does not distinguish a throw from a failed check')
    if missing:
        problems.append('%d line(s) the clean run printed are MISSING after the crash - '
                        'suites below it were hidden. First: %r' % (len(missing), missing[0]))
    if problems:
        for x in problems:
            print('    FAIL  ' + x)
        sys.exit('HARNESS CHECK FAILED (S167): the test report is not trustworthy, so no result '
                 'from this run is either.')
    print('    ok: fault named, exit %d, all %d clean report lines still present'
          % (f.returncode, len(clean_lines)))


def tool_tests():
    """
    The PYTHON-side headless checks (BB3). `plugin/tools/assess_flight.py --selftest` synthesises a
    BB1/BB2 recording at the CURRENT schema - parsed out of `BlackBoxSchema.cs`, so it cannot rot - and
    asserts the report generator reads all twelve §4.10 sections back out of it. It needs no KSP, no
    install and no glass time (both of which are separate owner gates), which is exactly why it belongs
    in `test` rather than waiting on a flight.

    A missing tool is skipped, not failed: the C# suites are this command's contract and a tool that has
    not been written yet must not break the build. A tool that IS there and fails, fails the build.
    """
    event_vocabulary_check()
    part_name_source_check()
    column_writer_check()
    preview_diff_selftest()   # S168: the before/after harness's own classifier + render gate
    tri_raster_check()        # S241 (BOB-13): the Tri primitive's only pixel-level evidence
    base_page_check()         # S245: the NON-ICON base page's device-space table (spec section 11)
    overview_check()          # S248: the VEHICLE OVERVIEW's ink, its label/value collision
                              #       property, its four control states and the base page's pixels

    tool = os.path.join(HERE, 'tools', 'assess_flight.py')
    if not os.path.exists(tool):
        return
    print('--- running tool selftests')
    p = subprocess.run([sys.executable, tool, '--selftest'], capture_output=True, text=True)
    print((p.stdout or '') + (p.stderr or ''))
    if p.returncode != 0:
        sys.exit('TOOL SELFTEST FAILED (exit %d)' % p.returncode)


# ======================================================================================================
# S168: THE BEFORE/AFTER PREVIEW HARNESS, IN CODE
#
# Every register line in this project reports "N pages changed" from a preview render, and C1.3 makes
# that report part of the DONE gate. Until this, the report came from a habit: revert the changed files
# by hand, re-render, compare, put them back. `S130` reported "0 existing pages changed"; the true
# figure was 74 (`8d6880f`). The revert had put the MODIFIED files back but left the NEW file on disk,
# the before-render therefore failed to COMPILE, the old PNGs were still sitting in the output folder,
# and the harness read them as "nothing moved".
#
# A SILENT FALSE GREEN PRODUCED BY THE TOOL MEANT TO PREVENT ONE. It was caught only because `S132` ran
# the same habit over a change that visibly redrew two elements and got 0 again.
#
# `8d6880f` diagnosed it exactly and fixed it in PROSE - `git show --stat 8d6880f` touches REGISTER.md
# and nothing else. That is the third verification instrument in this project to fail (S75's tints,
# H-01's 2x width, this) and the first two were fixed in code, where they cannot rot. This is the third.
#
# ---- WHAT MAKES THE FALSE GREEN STRUCTURALLY IMPOSSIBLE HERE ----------------------------------------
# The instruction was to move ADDED files aside as well as reverting modified ones. This does something
# strictly stronger and for the same reason: the baseline is rendered in a SEPARATE GIT WORKTREE, a
# clean checkout of the baseline commit. There is no "put it back" step to get wrong, because the live
# tree is never touched; there is nothing to move aside, because a fresh checkout has no added files in
# it at all; and the two renders write to two different output folders, so neither can be read as the
# other. The class of bug is removed rather than the instance patched.
#
# Then, because a structural argument is still only an argument, three explicit gates:
#   (1) EITHER RENDER FAILING IS FATAL. Non-zero exit, no output folder, or zero PNGs - any of the three
#       ends the run with a non-zero exit and a named reason. S130's render failed and was read as a
#       result; here a render that did not happen can never be read as "nothing moved".
#   (2) A VACUOUS COMPARISON IS REFUSED, not reported. If no render input differs between the baseline
#       and the working tree, "0 pages changed" is arithmetic, not evidence - and it is the exact
#       sentence a register line would quote as proof. It is refused, loudly, with the reason.
#   (3) THE CLASSIFIER IS SELF-TESTED on every `build.py test` (`previewdiff --selftest`, wired into
#       tool_tests), including a case where the before-render produced nothing - the S130 shape.
# ======================================================================================================

# The preview renders from these, and only these: the pure display layer, the renderer itself, the
# shipped cfg it derives its size from (QC H-01 / S100), and the art it draws.
PREVIEW_INPUTS = ('plugin/src/pure/', 'plugin/preview/', 'plugin/GameData/DragonScreen/')


def _sha256(path):
    h = hashlib.sha256()
    with open(path, 'rb') as fh:
        for chunk in iter(lambda: fh.read(1 << 16), b''):
            h.update(chunk)
    return h.hexdigest()


def _pngs(folder):
    """{filename: sha256} for every PNG in a render folder. Missing folder -> None, NOT {}."""
    if not os.path.isdir(folder):
        return None
    out = {}
    for n in sorted(os.listdir(folder)):
        if n.lower().endswith('.png'):
            f = os.path.join(folder, n)
            if os.path.isfile(f):
                out[n] = _sha256(f)
    return out


def render_problems(label, returncode, folder):
    """
    S168 gate (1). What must be true before a render's output may be COMPARED to anything.

    ⛔ THE DISTINCTION THIS FUNCTION EXISTS FOR: `None` (no folder) and `{}` (a folder with no PNGs) are
    both "the render did not produce pages", and neither may be silently treated as an empty set of
    pages that simply happened to match. That conflation IS S130.
    """
    bad = []
    if returncode != 0:
        bad.append('the %s render exited %d - it did not complete, so its PNGs mean nothing'
                   % (label, returncode))
    pngs = _pngs(folder)
    if pngs is None:
        bad.append('the %s render produced no output folder at %s' % (label, folder))
    elif not pngs:
        bad.append('the %s render produced ZERO pages - a folder with no PNGs is a render that did '
                   'not happen, not a render in which nothing moved' % label)
    return bad


def diff_renders(before, after):
    """
    S168: classify two {name: sha256} maps. Pure, so `--selftest` can prove it without rendering.

    Returns (changed, added, removed, same) - four sorted lists of filenames.
    """
    b, a = set(before), set(after)
    changed = sorted(n for n in (b & a) if before[n] != after[n])
    same = sorted(n for n in (b & a) if before[n] == after[n])
    return changed, sorted(a - b), sorted(b - a), same


# ⛔ IGNORED DIRECTORIES THE RENDER READS, AND WHY THIS IS NOT A DETAIL --------------------------
# A `git worktree` checkout contains TRACKED files only. `assets/reference/` is gitignored
# (`.gitignore:12`) and the preview's Earth stand-in lives in it - so the first working version of
# previewdiff rendered the baseline with a BARE GLOBE and reported 35 pages changed for a change that
# touched 3. THIRTY-TWO FALSE POSITIVES, from an instrument built to stop a false negative.
#
# ⭐ AND THE RENDER HAD SAID SO. The baseline log carried `(no body-map stand-in at ... - globe
# previews bare)` five times and the comparison did not read it - the same shape as S130, where the
# render failed and its output was compared anyway.
#
# So: ignored DIRECTORIES are junctioned into the worktree, and the two renders' own missing-input
# warnings are compared. Ignored files are not versioned, so they are identical for both sides by
# construction; junctioning them makes the tracked source the ONLY difference between the renders.
#
# ⚠ IGNORED, NOT UNTRACKED. An untracked-but-not-ignored file is a NEW SOURCE FILE - part of the
# change under test - and must NOT reach the baseline. That distinction is the whole reason S130's
# habit broke: it left a new file in a reverted tree.
MIRROR_SKIP = ('plugin/build/', 'plugin/__pycache__/', 'plugin/build')


def _mirror_ignored_inputs(tree, made):
    """
    Junction every ignored directory (bar build output) into the baseline worktree.

    ⛔ `made` IS AN OUT-PARAMETER AND IT IS A SAFETY DEVICE, NOT BOOKKEEPING. Read
    `_unmirror_ignored_inputs` before touching anything here: a junction is a door into the REAL
    directory, and a recursive delete of a tree containing one deletes what is on the other side.
    That happened - see S168 - and it cost ~298 MB of gitignored reference material.
    """
    r = subprocess.run(['git', 'ls-files', '--others', '--ignored', '--exclude-standard',
                        '--directory'], capture_output=True, text=True, cwd=ROOT)
    failed = []
    for rel in sorted(set((r.stdout or '').splitlines())):
        rel = rel.strip()
        if not rel.endswith('/') or rel.startswith(MIRROR_SKIP):
            continue                      # files are not mirrored; build output never is
        src = os.path.join(ROOT, rel.rstrip('/').replace('/', os.sep))
        dst = os.path.join(tree, rel.rstrip('/').replace('/', os.sep))
        if not os.path.isdir(src) or os.path.exists(dst):
            continue
        parent = os.path.dirname(dst)
        if not os.path.isdir(parent):
            os.makedirs(parent)
        j = subprocess.run(['cmd', '/c', 'mklink', '/J', dst, src], capture_output=True, text=True)
        if j.returncode == 0:
            made.append((dst, src))       # recorded BEFORE anything else can fail
        else:
            failed.append(rel)
    if made:
        print('    mirrored %d ignored input dir(s) into the baseline: %s'
              % (len(made), ', '.join(os.path.basename(d) for d, _ in made)))
    if failed:
        # LOUD. A baseline that cannot see an input the working tree can see will report a difference
        # that is the environment, not the change - which is worse than no measurement at all.
        sys.exit('PREVIEWDIFF FAILED: could not mirror ignored input(s) into the baseline worktree: '
                 '%s. The baseline would render without them and every page that draws them would '
                 'read as changed.' % ', '.join(failed))



def _unmirror_ignored_inputs(made):
    """
    ⛔⛔ THE MOST DANGEROUS TEN LINES IN THIS FILE. READ THE INCIDENT BEFORE EDITING THEM.

    WHAT HAPPENED (S168, 2026-09-06). The first working previewdiff junctioned the ignored input
    directories into the baseline worktree and then tore the worktree down with
    `git worktree remove --force` + `shutil.rmtree`. BOTH FOLLOW A JUNCTION. `assets/` went from
    299 MB to 972 KB: `assets/figma/`, `assets/kenney_ui_scifi/` and `assets/reference/` were emptied
    THROUGH THE LINKS, in the real repository, and none of it was in git - that is what "gitignored"
    means. A recursive delete does not know it is standing in a doorway.

    THE RULE THAT FALLS OUT OF IT. A junction is removed with `os.rmdir`, which unlinks the reparse
    point and does NOT touch what is on the other side. Every junction is removed, and VERIFIED
    removed, BEFORE any recursive delete goes anywhere near the tree that held them. If even one
    cannot be removed, the worktree is LEFT ON DISK and the run says so - a stale temp directory
    costs disk space, and the alternative cost 298 MB of material that had to be re-downloaded.

    ⚠ AND THE VERIFICATION IS NOT OPTIONAL. `os.rmdir` succeeding is not the same as the link being
    gone, and the source surviving is the thing actually being protected - so both are checked, and
    a source directory that has lost its contents is reported as loudly as this file can report it.
    """
    survivors = []
    for dst, src in made:
        try:
            if os.path.isdir(dst):
                os.rmdir(dst)             # UNLINK ONLY. Never rmtree, never `del /s`, never git.
        except OSError as e:
            print('    !! could not unlink the junction at %s: %s' % (dst, e))
        if os.path.exists(dst):
            survivors.append(dst)
        # the thing the whole guard is for: is the REAL directory still populated?
        try:
            if os.path.isdir(src) and not os.listdir(src):
                print('    !! WARNING: %s is now EMPTY - check it against your backups' % src)
        except OSError:
            pass
    return survivors

def render_warnings(text):
    """
    The renderer's own vocabulary for "an input I wanted was not there". S168 compares these across
    the two runs: if one render saw an input the other did not, the pages that draw it differ for a
    reason that has nothing to do with the change, and no count from that run means anything.
    """
    keys = ('stand-in', 'MISSING art', 'MISSING cover asset')
    return sorted(l.strip() for l in text.splitlines() if any(k in l for k in keys))


def _changed_inputs(baseref):
    """Render inputs that differ between `baseref` and the WORKING TREE, untracked files included."""
    def git(*args):
        r = subprocess.run(['git'] + list(args), capture_output=True, text=True, cwd=ROOT)
        return (r.stdout or '').splitlines() if r.returncode == 0 else []
    names = set(git('diff', '--name-only', baseref, '--'))
    names |= set(git('ls-files', '--others', '--exclude-standard'))
    return sorted(n for n in names if n and n.startswith(PREVIEW_INPUTS))


def preview_diff(baseref='HEAD'):
    """
    S168: render `baseref` and the working tree, and report the per-page difference by hash.

    Usage:  python plugin/build.py previewdiff [<baseline-ref>]

    The baseline defaults to HEAD - "what my uncommitted work changed". Pass HEAD~1 (or any ref) to
    measure a change that is already committed, which is how most register lines phrase it.
    """
    work = tempfile.mkdtemp(prefix='dsprevdiff-')
    tree = os.path.join(work, 'baseline')
    mirrored = []            # (junction, real target) - see _unmirror_ignored_inputs before editing
    print('--- previewdiff: baseline %s  vs  the working tree' % baseref)

    moved = _changed_inputs(baseref)
    if not moved:
        # ---- GATE (2): REFUSE, DO NOT REPORT ----
        # "0 pages changed" here would be arithmetic dressed as evidence, and it is the exact sentence
        # a register line would quote as proof that a change drew nothing.
        shutil.rmtree(work, ignore_errors=True)
        sys.exit('PREVIEWDIFF REFUSED: no render input differs between %s and the working tree, so '
                 '"0 pages changed" would be arithmetic, not a measurement. Nothing was rendered. '
                 'Pass the ref you actually mean, e.g. `previewdiff HEAD~1`.' % baseref)
    print('    %d changed render input(s): %s'
          % (len(moved), ', '.join(moved[:6]) + (' ...' if len(moved) > 6 else '')))

    try:
        # ---- THE BASELINE, IN ITS OWN WORKTREE ----
        # A detached checkout of `baseref`. The live tree is never touched, so there is no restore step
        # to get wrong, and a fresh checkout cannot be carrying an added file that does not belong to
        # the baseline - which is precisely what broke S130's measurement.
        r = subprocess.run(['git', 'worktree', 'add', '--detach', tree, baseref],
                           capture_output=True, text=True, cwd=ROOT)
        if r.returncode != 0:
            sys.exit('PREVIEWDIFF FAILED: could not check out %s\n%s'
                     % (baseref, (r.stdout or '') + (r.stderr or '')))
        _mirror_ignored_inputs(tree, mirrored)

        before_dir = os.path.join(tree, 'plugin', 'build', 'preview')
        after_dir = os.path.join(HERE, 'build', 'preview')

        print('--- previewdiff: rendering the BASELINE (%s)' % baseref)
        rb = subprocess.run([sys.executable, os.path.join(tree, 'plugin', 'build.py'), 'preview'],
                            capture_output=True, text=True, cwd=tree)
        print('--- previewdiff: rendering the WORKING TREE')
        ra = subprocess.run([sys.executable, os.path.join(HERE, 'build.py'), 'preview'],
                            capture_output=True, text=True, cwd=ROOT)

        # ---- GATE (1): a render that did not happen is never a render in which nothing moved ----
        bad = render_problems('BASELINE', rb.returncode, before_dir)
        bad += render_problems('WORKING TREE', ra.returncode, after_dir)
        if bad:
            for x in bad:
                print('    FAIL  ' + x)
            tail = ((rb.stdout or '') + (rb.stderr or '')) if bad and rb.returncode != 0 \
                else ((ra.stdout or '') + (ra.stderr or ''))
            print(chr(10).join(tail.splitlines()[-25:]))
            sys.exit('PREVIEWDIFF FAILED: a render did not report ok, so NO comparison from this run '
                     'is trustworthy. This is exactly the S130 shape - do not read the PNGs.')

        # ---- GATE (1b): the two renders must have seen the SAME INPUTS ----
        # Caught in the act while this was being written: without the junctions above, the baseline
        # rendered a bare globe and 32 pages read as changed. The renders said so in their own logs
        # and nothing was reading them. Now something is.
        wb = render_warnings((rb.stdout or '') + (rb.stderr or ''))
        wa = render_warnings((ra.stdout or '') + (ra.stderr or ''))
        if wb != wa:
            print('    FAIL  the two renders did not see the same inputs:')
            for l in [x for x in wb if x not in wa]:
                print('        BASELINE only:      ' + l)
            for l in [x for x in wa if x not in wb]:
                print('        WORKING TREE only:  ' + l)
            sys.exit('PREVIEWDIFF FAILED: one render was missing an input the other had, so any page '
                     'that draws it would read as changed for a reason that is not the change.')

        before, after = _pngs(before_dir), _pngs(after_dir)
        changed, added, removed, same = diff_renders(before, after)

        print('--- previewdiff RESULT   baseline %s (%d pages)  ->  working tree (%d pages)'
              % (baseref, len(before), len(after)))
        for label, names in (('CHANGED', changed), ('NEW', added), ('REMOVED', removed)):
            if names:
                print('    %d %s:' % (len(names), label))
                for n in names:
                    print('        ' + n)
        print('    %d unchanged' % len(same))
        # The one-line form a register entry quotes.
        print('--- previewdiff: %d existing page(s) changed, %d new, %d removed  (of %d compared)'
              % (len(changed), len(added), len(removed), len(before)))
    finally:
        # ⛔ ORDER IS LOAD-BEARING: unlink every junction FIRST, and refuse to recurse if one is left.
        left = _unmirror_ignored_inputs(mirrored)
        if left:
            print('    !! LEAVING the baseline worktree at %s - %d junction(s) could not be removed, '
                  'and deleting a tree that still contains one deletes what is on the other side.'
                  % (tree, len(left)))
            subprocess.run(['git', 'worktree', 'prune'], capture_output=True, text=True, cwd=ROOT)
        else:
            subprocess.run(['git', 'worktree', 'remove', '--force', tree],
                           capture_output=True, text=True, cwd=ROOT)
            shutil.rmtree(work, ignore_errors=True)


def tri_raster_check():
    """
    S241 (BOB-13): run the `Tri` primitive's RASTER proof as part of `test`.

    ⭐ WHY IT IS A GATE. `build.py test` compiles `src/pure` + `test` only, so NEITHER rasteriser is
    reachable from the headless suite - `DisplayListTriTest` can prove the packing and the degeneracy
    rule, and pin the two renderers against each other by SOURCE, but it cannot put down a pixel.
    `--tricheck` is the only pixel-level evidence the primitive has, and BOB-13's answer was that a
    check nobody runs rots.

    ⛔ IT BUILDS THE PREVIEW FIRST. The check lives inside the preview binary because that is the only
    place a rasteriser exists headlessly; running a stale exe would report on code that is no longer
    there, which is the S130 shape (a green that was true once).

    ⚠ It does NOT render any page - it draws triangles into throwaway bitmaps at 1920x1054 and at the
    shipped 2560x1405. Cheap enough to sit in `test`, which is the whole point.
    """
    print("--- Tri raster check (S241: the primitive's only pixel-level evidence)")
    exe = compile_preview()
    if not exe or not os.path.exists(exe):
        sys.exit('TRI RASTER CHECK: the preview binary was not built')
    p = subprocess.run([exe, '--tricheck'], capture_output=True, text=True)
    out = (p.stdout or '') + (p.stderr or '')
    for line in out.splitlines():
        if line.strip():
            print('    ' + line.strip() if not line.startswith('    ') else line)
    if p.returncode != 0:
        sys.exit('TRI RASTER CHECK FAILED (exit %d)' % p.returncode)


def base_page_check():
    """
    S245: run the NON-ICON base page's DEVICE-SPACE proof as part of `test`.

    WHY IT IS A GATE, and it is the same argument as `tri_raster_check` above. `build.py test`
    compiles `src/pure` + `test` only, so no rasteriser is reachable from the headless suite:
    `BasePageNoIconTest` can read every command the page emitted and prove the DESIGN-space column of
    the spec's section 11 exactly, but it cannot put down a pixel. Section 11's second table is
    pixels - which ROW the border's white band starts on at 2560x1405, where #070810 begins, where
    the window line lands - and this is the only thing that can read them.

    IT ALSO CATCHES WHAT NO DESIGN-SPACE TEST CAN SEE. Two antialiased fills of the same colour that
    merely abut leave the row where they meet at about 78 % coverage, and the 22 % that shows through
    reads as a hairline the full width of the page. This build carried one until `BaseBar.Seam` was
    added. The display list was correct throughout; only the render was wrong.

    IT BUILDS THE PREVIEW FIRST, for the same reason the Tri check does: a stale exe reports on code
    that is no longer there, which is the S130 shape.

    It renders the page twice, at both shipped sizes, and probes; it does NOT write any PNG.
    """
    print("--- base page device check (S245: spec section 11's device-space table)")
    exe = compile_preview()
    if not exe or not os.path.exists(exe):
        sys.exit('BASE PAGE CHECK: the preview binary was not built')
    p = subprocess.run([exe, '--basecheck'], capture_output=True, text=True)
    out = (p.stdout or '') + (p.stderr or '')
    for line in out.splitlines():
        if line.strip():
            print('    ' + line.strip() if not line.startswith('    ') else line)
    if p.returncode != 0:
        sys.exit('BASE PAGE CHECK FAILED (exit %d)' % p.returncode)


def overview_check():
    """
    S248: run the VEHICLE OVERVIEW's DEVICE-SPACE proof as part of `test`.

    SAME ARGUMENT AS `base_page_check` ABOVE, and it holds three things the headless suite cannot.
    `build.py test` compiles `src/pure` + `test` only, so no rasteriser is reachable from it:
    `VehicleOverviewContentTest` can prove the arithmetic that turns an INK HEIGHT into a pixel size,
    but whether the glyphs then ink that tall is a question about a rasteriser.

    THE THREE THINGS:
      1. INK HEIGHTS, which the spec states everywhere and says to assert instead of font sizes.
      2. SECTION 8.3's REAL PROPERTY. The spec wants CONNECTIONS' block width computed from the live
         font, which src/pure cannot do; what the rule is FOR is that no label ever reaches its
         value, and that is measurable here, in ink, on all four rows.
      3. THE BASE PAGE UNDERNEATH. An image cannot say whether the shipped base page is wrong or
         merely older than the #070810 ruling. This drives BasePageIcon and reads the pixels.

    IT BUILDS THE PREVIEW FIRST, for the same reason the other two do: a stale exe reports on code
    that is no longer there, which is the S130 shape. It renders at both shipped sizes, runs all four
    control states, and ends by running every probe over a page with its CONTENT REMOVED - which must
    report faults, or the probes are decorative.
    """
    print("--- vehicle overview device check (S248: ink, the collision property, four states)")
    exe = compile_preview()
    if not exe or not os.path.exists(exe):
        sys.exit('OVERVIEW CHECK: the preview binary was not built')
    p = subprocess.run([exe, '--overviewcheck'], capture_output=True, text=True)
    out = (p.stdout or '') + (p.stderr or '')
    for line in out.splitlines():
        if line.strip():
            print('    ' + line.strip() if not line.startswith('    ') else line)
    if p.returncode != 0:
        sys.exit('OVERVIEW CHECK FAILED (exit %d)' % p.returncode)


def preview_diff_selftest():
    """
    S168 gate (3): the classifier and the render gate, proved on every `build.py test`.

    ⚠ NO RENDER RUNS HERE - it is deliberately cheap enough to sit in `test`, because a check that is
    too slow to run is a check nobody runs. The end-to-end path is `previewdiff` itself.
    """
    bad = []
    ran = [0]

    def check(what, ok):
        # ⚠ COUNTED, not written down. A hardcoded total in the "ok" line below would be the same
        # class of defect this whole function exists to prevent: a number that stops matching what
        # was actually done, and no way to tell from the output.
        ran[0] += 1
        if not ok:
            bad.append(what)

    A = {'p.png': 'aa', 'q.png': 'bb'}
    check('identical renders report no change', diff_renders(A, dict(A))[0] == [])
    check('identical renders report every page as same', diff_renders(A, dict(A))[3] == ['p.png', 'q.png'])

    B = {'p.png': 'aa', 'q.png': 'ZZ'}
    ch, ad, rm, sm = diff_renders(A, B)
    check('one differing page is reported as changed, by name', ch == ['q.png'])
    check('...and the identical one is not', sm == ['p.png'])
    check('a change is not miscounted as new or removed', ad == [] and rm == [])

    ch, ad, rm, sm = diff_renders(A, {'p.png': 'aa', 'q.png': 'bb', 'r.png': 'cc'})
    check('a page only in the AFTER render is NEW', ad == ['r.png'] and ch == [])
    ch, ad, rm, sm = diff_renders(A, {'p.png': 'aa'})
    check('a page only in the BEFORE render is REMOVED', rm == ['q.png'] and ad == [])

    # ---- THE S130 SHAPE, WHICH IS THE WHOLE REASON THIS FILE HAS A previewdiff --------------------
    # A render that failed, or produced nothing, must be a FAILURE - never an empty set of pages that
    # trivially matches. Each of the three is checked alone, because in S130 only one of them was true.
    tmp = tempfile.mkdtemp(prefix='dsprevself-')
    try:
        empty = os.path.join(tmp, 'empty')
        os.makedirs(empty)
        check('a render that exited non-zero is a FAILURE even with pages on disk',
              render_problems('x', 1, tmp) != [])
        check('a render with NO OUTPUT FOLDER is a failure, not an empty comparison',
              render_problems('x', 0, os.path.join(tmp, 'nope')) != [])
        check('⭐ a render that produced ZERO PNGs is a failure, not "nothing moved"',
              render_problems('x', 0, empty) != [])
        with open(os.path.join(empty, 'p.png'), 'wb') as fh:
            fh.write(b'x')
        check('a render that exited 0 with pages is accepted', render_problems('x', 0, empty) == [])
        # and the hash really reads the bytes, or every comparison above is vacuous
        with open(os.path.join(empty, 'q.png'), 'wb') as fh:
            fh.write(b'y')
        m = _pngs(empty)
        check('two different files hash differently', m['p.png'] != m['q.png'])
        check('a missing folder is None, distinguishable from an empty one',
              _pngs(os.path.join(tmp, 'nope')) is None and _pngs(empty) is not None)
    finally:
        shutil.rmtree(tmp, ignore_errors=True)

    # ---- THE MISSING-INPUT GUARD, which is what a bare-globe baseline looked like -------------
    bare = '  (no body-map stand-in at C:/x/earth.jpg - globe previews bare)'
    check('a missing stand-in is picked up as a render warning', render_warnings(bare) != [])
    check('MISSING art is picked up too', render_warnings('  MISSING art C:/x/y.png') != [])
    check('MISSING cover asset is picked up too',
          render_warnings('  MISSING cover asset nose_04') != [])
    check('an ordinary render line is NOT a warning',
          render_warnings('  C:/out/cover.png   2560x1406   201 commands') == [])
    check('⭐ the two runs disagreeing about inputs is detectable',
          render_warnings(bare) != render_warnings(''))
    check('...and two identical logs agree', render_warnings(bare) == render_warnings(bare))

    if bad:
        for x in bad:
            print('    FAIL  ' + x)
        sys.exit('PREVIEWDIFF SELFTEST FAILED (S168): %d of %d check(s). The before/after harness is '
                 'the instrument every "N pages changed" claim comes from.' % (len(bad), ran[0]))
    print('--- previewdiff selftest (S168: a render that did not happen is never "nothing moved")')
    print('    ok: %d checks' % ran[0])


def mech_warning_baseline():
    """
    MJ1: WRITE THE VENDORED MECHJEB'S WARNING LIST, so a re-pin has something to diff against.

    `build_mech()` compiles with `-warn:0`, and that is correct for daily use: §B12.1 forbids fixing
    MechJeb's warnings ("source kept intact - rename shell only"), so printing several hundred
    unfixable ones on every build would train the eye to scroll straight past the region where a real
    ERROR appears. ⛔ THE DEFAULT BUILD'S `-warn:0` IS NOT CHANGED BY THIS, and must not be.

    But it also means nobody has ever SEEN the list. And at a re-pin the useful signal was never the
    absolute count - it is the DIFF: a warning that appears at the new commit and did not exist at the
    pinned one is a change in upstream's code, and that is exactly what a re-pin review should read.

    So this is a separate verb (`python plugin/build.py mechwarn`) that recompiles the same sources at
    `-warn:4` and writes the sorted, de-duplicated list to `plugin/mech/WARNINGS.txt`.

    ⛔ IT WRITES TO A THROWAWAY DLL PATH, not to `MECH_DLL`. A `-warn:4` build is otherwise identical,
    but shipping whatever a diagnostic verb happened to leave behind is how a build gets subtly
    different from the one that was tested. The real DLL is only ever written by `build_mech()`.

    ⚠ Paths are made RELATIVE and the list is SORTED, so the file is stable across machines and the
    diff at a re-pin shows real changes rather than a different checkout directory.
    """
    src = mech_sources()
    if not src:
        print('--- no plugin/mech tree vendored; nothing to baseline')
        return
    if not MODERN:
        sys.exit('the warning baseline needs Roslyn (C#8) - install a .NET SDK')
    scratch = os.path.join(HERE, 'build', 'mechwarn', 'DragonScreen.Mech.warn.dll')
    os.makedirs(os.path.dirname(scratch), exist_ok=True)
    print('--- recompiling the vendored MechJeb at -warn:4 (%d source files)' % len(src))
    args = compile_cs(scratch, src,
                      refs=[os.path.join(MAN, r) for r in MECH_REFS],
                      extra=['-optimize+', '-define:UNITY_2017_1', '-nullable:annotations'],
                      warn='4', langversion='8')
    proc = subprocess.run(args, capture_output=True, text=True)
    out = (proc.stdout or '') + (proc.stderr or '')

    warns = set()
    for line in out.splitlines():
        if ': warning ' not in line:
            continue
        line = line.strip().replace('\\', '/')
        here = HERE.replace('\\', '/')
        if here in line:
            line = line.replace(here + '/', '').replace(here, '')
        warns.add(line)

    dest = os.path.join(HERE, 'mech', 'WARNINGS.txt')
    body = [
        '# Vendored MechJeb — compiler warning BASELINE  (register MJ1)',
        '#',
        '# ⛔ THIS FILE IS EVIDENCE, NOT A TASK LIST. §B12.1 forbids fixing any of these:',
        '#    "source kept intact (not rewritten) - rename shell only". Do not touch plugin/mech to',
        '#    make a line here go away.',
        '#',
        '# WHAT IT IS FOR: the DIFF at a re-pin. A warning that appears at a new upstream commit and',
        '# is not in this list is a CHANGE IN UPSTREAM CODE, and is the thing a re-pin review reads.',
        '# The absolute count is not the signal and never was.',
        '#',
        '# ⭐ AND AT THE CURRENT PIN THE LIST IS EMPTY — MEASURED, NOT ASSUMED. MJ1 was logged',
        '#   expecting "several hundred" unfixable warnings. There are none. Verified twice: through',
        '#   this verb, and by running Roslyn directly on the same response file (exit 0, no output).',
        '#',
        '#   ⛔ THAT IS A PROPERTY OF THE COMPILER CONTRACT, NOT OF MECHJEB ALONE, and a re-pin must',
        '#   know it. Measured by removing one flag at a time from the same response file:',
        '#',
        '#       as built  (-warn:4, -nullable:annotations)  ->    0 warnings, exit 0',
        '#       WITHOUT   -nullable:annotations             ->   79 warnings (all CS8632), exit 1',
        '#       at        -warn:0 (the shipped setting)     ->    0 warnings',
        '#',
        '#   So `-nullable:annotations` is what makes this tree clean: it enables the `?` annotations',
        '#   upstream writes WITHOUT the CS8632 "annotation used outside a #nullable context" warning.',
        '#   `build_mech()`s docstring already says that is why the flag is there; this measures it.',
        '#',
        '#   ⭐ AN EMPTY BASELINE IS THE STRONGEST KIND. Any warning at a re-pin is 100% signal — there',
        '#   is no noise floor to read past, which is exactly what MJ1 wanted and better than it hoped.',
        '#   ⚠ If a future re-pin makes this file non-empty, that is the finding. Do not fix it (§B12.1);',
        '#   read it, and record what upstream changed.',
        '#',
        '# REGENERATE:  python plugin/build.py mechwarn',
        '# Pinned commit: see plugin/mech/VENDOR.md. Sources: %d files.' % len(src),
        '# Distinct warning lines: %d' % len(warns),
        '',
    ]
    if not warns:
        body.append('(no warnings at this pin — see the note above)')
    body.extend(sorted(warns))
    io.open(dest, 'w', encoding='utf-8', newline='\n').write('\n'.join(body) + '\n')
    print('    %d distinct warning lines -> %s' % (len(warns), dest))
    if proc.returncode != 0:
        sys.exit('the -warn:4 compile FAILED (exit %d) - that is an ERROR, not a warning'
                 % proc.returncode)


def event_vocabulary_check():
    """
    S90: EVERY DECLARED EVENT KIND MUST HAVE AN EMITTER.

    `BlackBoxEvents.cs` names each `kind` as a constant so that a typo is a compile error rather than a
    lost channel - which is true, and is NOT the same property as "every declared kind can actually
    fire". Three could not: `rec.close`, `rec.scene_change` and `sys.string_state` were declared and
    never emitted, each name appearing exactly once in the whole tree, at its own declaration.

    That is BB1's ghost-column defect (S76) one level up, in the EVENT namespace. A reader filtering
    `events.jsonl` for `sys.string_state` found nothing and concluded no string ever changed state -
    the same wrong inference `torque_cmd` invited by being present and always blank.

    S90 wired one and retired two. This is the standing guard that stops a fourth appearing: it is
    STATIC on purpose, because a kind with an emitter that simply did not fire on a given flight is
    legitimate, while a kind with NO emitter anywhere is always a defect and is knowable without
    flying. BB1's runtime `BlackBoxCoverage` is the same idea for columns, checked at close.
    """
    events = os.path.join(HERE, 'src', 'pure', 'blackbox', 'BlackBoxEvents.cs')
    if not os.path.exists(events):
        return
    print('--- event vocabulary (S90: every declared kind has an emitter)')
    # ⛔ COMMENT LINES ARE SKIPPED, and that is not a detail. A retired kind is kept VERBATIM in a
    # comment (C1.16/G12 - the reasoning for retiring it is worth more than the line it describes), so
    # a naive scan of the file re-reports every retirement as a fresh defect. The first version of
    # this check did exactly that and flagged RecClose and RecSceneChange, which S90 had just removed.
    decl = re.compile(r'public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"\s*;')
    src = NL.join(l for l in io.open(events, encoding='utf-8').read().splitlines()
                  if not l.strip().startswith('//'))
    names = decl.findall(src)

    # Every .cs in the plugin, minus the declaration file itself.
    bodies = []
    for root, _dirs, files in os.walk(os.path.join(HERE, 'src')):
        for f in files:
            if not f.endswith('.cs'):
                continue
            full = os.path.join(root, f)
            if os.path.abspath(full) == os.path.abspath(events):
                continue
            bodies.append(io.open(full, encoding='utf-8', errors='replace').read())
    blob = NL.join(bodies)

    # ---- KNOWN DEAD, EACH OWNED BY A REGISTER LINE ----------------------------------------------
    # ⛔ An entry here is a DEFECT ON RECORD, not a pardon. The guard stays live - a kind that is not
    # on this list and has no emitter still fails the build - and this list is the register's to
    # SHRINK. Nothing may be added to it without a register line that owns the fix.
    #
    # All three below are one finding, logged as S161 by S90 (C1.1: S90's declared scope was the three
    # kinds it named, and these are not them). They are the S76 ghost-column defect applied to the
    # ghost-column DETECTOR: `BlackBoxCoverage.Findings()` has no caller anywhere in plugin/src - the
    # only mention outside its own file is a doc comment in BlackBoxManifest - so the coverage check
    # never runs, these three events can never fire, and `tools/assess_flight.py` alerts on exactly
    # these three kinds and will therefore report "no column defects" on every flight forever.
    # ✅ EMPTY SINCE S161 (2026-09-06). All three entries were cleared by that line: the two coverage
    # kinds gained an emitter, and `Exception` was retired. The guard is back to enforcing the whole
    # vocabulary with no exceptions, which is where it should stay.
    KNOWN_DEAD = {}

    dead = [(sym, kind) for sym, kind in names
            if ('BlackBoxEvents.' + sym) not in blob and sym not in KNOWN_DEAD]
    for sym, kind in dead:
        print('    DEAD KIND  %-24s "%s"  - declared, never emitted' % (sym, kind))
    if dead:
        sys.exit('EVENT VOCABULARY FAILED: %d declared kind(s) have no emitter (S90). '
                 'Either emit it, or retire it and say why - a named channel that cannot '
                 'fire tells a reader the thing never happened.' % len(dead))
    for sym in sorted(KNOWN_DEAD):
        if any(s2 == sym for s2, _k in names):
            print('    known dead  %-23s owned by register %s' % (sym, KNOWN_DEAD[sym]))
    print('    %d kinds, %d emitted, %d known dead and owned'
          % (len(names), len(names) - len(KNOWN_DEAD), len(KNOWN_DEAD)))


def column_writer_check():
    """
    S137c: EVERY DECLARED BLACK-BOX COLUMN MUST HAVE A WRITER.

    This is S90's event-vocabulary guard applied one namespace over, and for the same reason. S76 found
    `torque_cmd` DECLARED and never populated across a whole mission, so a reader filtering for it saw
    nothing and concluded no torque was ever commanded - a GHOST COLUMN, and the same wrong inference
    a dead event kind invites.

    `BlackBoxCoverage` already catches the runtime half at close. ⛔ IT CANNOT CATCH THE HALF THIS
    GUARD IS FOR, and that is why this exists rather than leaning on it: a CONDITIONAL column left
    blank is reported as a NOTE and deliberately NOT a defect - "no target was ever selected" is a fact
    about the flight, not a bug - so a conditional column with NO WRITER AT ALL is indistinguishable at
    runtime from one whose condition never came true. S137c added `sev_events`, a Conditional column;
    deleting its writer breaks nothing that any test could see. This closes that.

    STATIC on purpose, exactly as the event check is: a column whose writer never fired on a given
    flight is legitimate, while a column with NO writer anywhere is always a defect and is knowable
    without flying.
    """
    cols = os.path.join(HERE, 'src', 'pure', 'blackbox', 'BlackBoxCols.cs')
    schema = os.path.join(HERE, 'src', 'pure', 'blackbox', 'BlackBoxSchema.cs')
    if not os.path.exists(cols) or not os.path.exists(schema):
        return
    print('--- black-box columns (S137c: every FITTED column has a writer)')

    # ---- UNFITTED COLUMNS ARE EXEMPT, AND THAT IS THE SCHEMA'S OWN DESIGN, NOT A LOOPHOLE --------
    # `Unfit(...)` declares a column for a system this build DOES NOT HAVE - the Part B conductor's
    # dv/PVG/node/step block. The schema's own header calls that "the honest state, and also how a
    # real recorder reports an unfitted system", and `BlackBoxCoverage` treats WRITING one as the
    # defect, which is the exact opposite of this check. So the guard would have reported 23 of them
    # on its first run, all correct, and all noise. Measured, then exempted - not assumed.
    sch = io.open(schema, encoding='utf-8', errors='replace').read()
    unfitted = set(re.findall(r'(?<![\w])Unfit\(\s*"([a-z0-9_]+)"', sch))
    decl = re.compile(r'public\s+static\s+readonly\s+int\s+(\w+)\s*=\s*BlackBoxSchema\.Index')
    src = NL.join(l for l in io.open(cols, encoding='utf-8').read().splitlines()
                  if not l.strip().startswith('//'))
    names = decl.findall(src)

    bodies = []
    for root, _dirs, files in os.walk(os.path.join(HERE, 'src')):
        for f in files:
            if not f.endswith('.cs'):
                continue
            full = os.path.join(root, f)
            if os.path.abspath(full) == os.path.abspath(cols):
                continue
            bodies.append(io.open(full, encoding='utf-8', errors='replace').read())
    blob = NL.join(bodies)

    # ---- KNOWN WRITERLESS, EACH OWNED BY A REGISTER LINE -----------------------------------------
    # ⛔ An entry here is a DEFECT ON RECORD, not a pardon - the same standing as KNOWN_DEAD above.
    # The guard stays live and this list is the register's to SHRINK.
    KNOWN_WRITERLESS = {}

    # Cols declares `Name = BlackBoxSchema.Index("name")`; pair the two so an Unfitted column can be
    # recognised by the NAME it indexes rather than by guessing from the C# identifier.
    pairs = dict(re.findall(
        r'public\s+static\s+readonly\s+int\s+(\w+)\s*=\s*BlackBoxSchema\.Index\("([a-z0-9_]+)"\)', src))
    dead = [n for n in names
            if ('BlackBoxCols.' + n) not in blob
            and pairs.get(n, '') not in unfitted
            and n not in KNOWN_WRITERLESS]
    for n in dead:
        print('    NO WRITER  %s' % n)
    if dead:
        sys.exit('BLACK-BOX COLUMN FAILED: %d declared column(s) have no writer (S76/S137c). '
                 'A column nothing ever writes tells a reader the thing never happened. Either '
                 'write it, or remove the column and its Index.' % len(dead))
    print('    %d indexed columns, %d unfitted (declared for a system this build has not got), '
          '%d known writerless and owned'
          % (len(names), sum(1 for n in names if pairs.get(n, '') in unfitted),
             len(KNOWN_WRITERLESS)))


def part_name_source_check():
    """
    OCT2: THE GLUE READS ONE PART-NAME SOURCE, AND THIS IS WHAT KEEPS IT THAT WAY.

    `Part.name` and `partInfo.name` are DIFFERENT STRINGS on a live vessel. OCT1 (2026-09-05) found
    that out the expensive way: `BoosterHost.Describe` asked `IsBooster` for a `.S1.` SUBSTRING, which
    survived the extra characters `Part.name` carried, while the octaweb binder asked for whole-name
    EQUALITY, which did not. "Found the booster" and "octaweb not found" about the same part, 264
    times, and every booster engine command silently dropped for a whole descent.

    OCT1 fixed the two classifiers it was scoped to and left twenty more bare reads standing, each one
    working by the same luck. OCT2 routed all of them - and the four remaining copies of the expression
    - through `PartNames.Of`. This guard is the part that survives the task: a NEW bare read added
    later would restore the divergence silently, and nothing about it looks wrong on the page.

    ⛔ WHY STATIC AND WHY A BUILD STEP rather than a comment: the DONE-when asked for "a test or a
    comment", and a comment is what OCT1 already had - two of them, in two files, each telling the
    reader to remember the other. That is the thing that failed. This cannot be forgotten.

    ⚠ TWO READS ARE LEGITIMATE and carry `OCT2-ALLOW-RAW-NAME` on their own line: the drift detector
    in `OctawebEngines` exists precisely to compare the two strings and print both. An allow marker is
    a claim that the line MEANS to read the raw name, not a way to silence the guard.
    """
    glue = os.path.join(HERE, 'src')
    if not os.path.isdir(glue):
        return
    print('--- part-name source (OCT2: the glue classifies on PartNames.Of)')
    # `p.name` / `part.name` as whole words, but never `partInfo.name` - which is the CORRECT read
    # substring of nothing else here. Comment lines are skipped for the same reason the event check
    # skips them: this file's own reasoning quotes the bad expression by name (C1.16 / G12).
    bad = re.compile(r'(?<!partInfo)(?<![\w.])(?:p|part)\.name(?![\w])')
    hits = []
    for root, _dirs, files in os.walk(glue):
        if os.path.basename(root) in ('pure', 'blackbox', 'mech'):
            _dirs[:] = []
            continue
        for f in sorted(files):
            if not f.endswith('.cs'):
                continue
            full = os.path.join(root, f)
            for n, line in enumerate(io.open(full, encoding='utf-8', errors='replace')
                                     .read().splitlines(), 1):
                t = line.strip()
                if t.startswith('//') or t.startswith('///') or t.startswith('*'):
                    continue
                if 'OCT2-ALLOW-RAW-NAME' in line:
                    continue
                if bad.search(line):
                    hits.append((os.path.relpath(full, HERE), n, t[:100]))
    for rel, n, t in hits:
        print('    BARE Part.name  %s:%d  %s' % (rel, n, t))
    if hits:
        sys.exit('PART-NAME SOURCE FAILED: %d glue line(s) read a bare Part.name (OCT1/OCT2). '
                 'Use PartNames.Of(p) - partInfo.name is the identity the pure layer is tested '
                 'against, Part.name is a live Unity object name and is NOT the contract. If the '
                 'line genuinely means to read the raw name, mark it OCT2-ALLOW-RAW-NAME and say '
                 'why.' % len(hits))
    print('    0 bare reads; PartNames.Of is the one source')


def compile_preview():
    """
    Compile the preview binary WITHOUT rendering anything, and return its path.

    ⭐ EXTRACTED FROM `build_preview` BY S241 so `test` can run `--tricheck` without also rendering 130
    pages. ⛔ ONE compile invocation, called by both - a second copy of this command is a second thing
    to keep in step, and the flags below (System.Drawing by full path under -nostdlib) are exactly the
    kind of detail that drifts when duplicated.
    """
    src = sources('src/pure', 'preview')
    if not src:
        print('--- no preview sources'); return None
    exe = os.path.join(HERE, 'build', 'DragonScreenPreview.exe')
    os.makedirs(os.path.dirname(exe), exist_ok=True)
    # System.Drawing is a .NET Framework assembly and is NOT in KSP's Managed folder, so under
    # -nostdlib it has to be named by full path from the framework directory. Nothing here ships.
    drawing = 'System.Drawing.dll'
    if MODERN:
        drawing = os.path.join(os.path.dirname(CSC_LEGACY), 'System.Drawing.dll')
    run(compile_cs(exe, src, refs=[drawing], exe=True),
        'preview renderer (%d source files)' % len(src))
    return exe


def build_preview():
    """
    Render the pages to PNG with the game closed.

    THE POINT IS RESTARTS. A DLL change costs a full KSP restart and so does a cfg change, while page
    design is where the iteration count explodes. This links src/pure ONLY - same rule as the tests -
    and walks the same DisplayList the in-game painter walks, so the look can be judged in seconds.

    System.Drawing is a .NET Framework assembly, not a KSP one, so csc resolves it by name from the
    framework directory. Nothing here ships to the game.
    """
    src = sources('src/pure', 'preview')
    if not src:
        print('--- no preview sources'); return
    exe = os.path.join(HERE, 'build', 'DragonScreenPreview.exe')
    out = os.path.join(HERE, 'build', 'preview')
    os.makedirs(os.path.dirname(exe), exist_ok=True)

    # ---- THE OUTPUT FOLDER IS EMPTIED FIRST (S100, from QC finding F-05) ----
    # It used to accumulate. 118 PNGs sat here, nineteen of them older than the 2026-09-04 tint fix
    # and therefore drawn by a renderer that ignored asset tint entirely - and one of those,
    # `ui_cover_phase4.png`, was a full-size Cover render from a render block that no longer exists,
    # named exactly like the current `ui_cover_phase5.png`. Nothing about the file said so. A QC pass
    # came within one step of writing a finding against a two-week-old tint-blind render.
    #
    # ⚠ EMPTYING IT EVERY RUN IS THE POINT, and clearing it once by hand is NOT this fix: a stale
    # render has to be IMPOSSIBLE, not merely absent today. After this, every file in the folder was
    # produced by the run that is printed above it, and the manifest below says which run that was.
    #
    # Nothing is lost: the folder is gitignored (`.gitignore:19`) and documented as "output, not
    # input". Checked before writing this (C1.16): no file under `docs/` cites any of these PNGs as
    # evidence - `docs/QC_FINDINGS.md` names them only as the stale files to be got rid of. Research
    # is never deleted; build output is not research.
    if os.path.isdir(out):
        removed = 0
        for name in sorted(os.listdir(out)):
            f = os.path.join(out, name)
            if os.path.isfile(f):
                os.remove(f); removed += 1
        print('--- cleared %d stale file(s) from build/preview' % removed)
    os.makedirs(out, exist_ok=True)
    # System.Drawing is a .NET Framework assembly and is NOT in KSP's Managed folder, so under
    # -nostdlib it has to be named by full path from the framework directory. Nothing here ships.
    compile_preview()
    print('--- rendering pages')
    p = subprocess.run([exe, out], capture_output=True, text=True)
    print((p.stdout or '') + (p.stderr or ''))
    if p.returncode != 0:
        sys.exit('PREVIEW FAILED (exit %d)' % p.returncode)

    # ---- THE MANIFEST (S100 / F-05) ----
    # The folder is now exactly this run's output, so a listing of it IS the manifest, and writing it
    # down means a reader of a PNG can tell what produced it without re-running anything. The width
    # is recorded beside every file because H-01 was a render size nobody could see from the output.
    listing = sorted(f for f in os.listdir(out) if f.lower().endswith('.png'))
    manifest = os.path.join(out, 'MANIFEST.txt')
    lines = [
        '# build/preview - written by `python build.py preview`, %s'
        % time.strftime('%Y-%m-%d %H:%M:%S'),
        '# The folder is emptied at the start of every run (S100 / QC F-05), so every file below',
        '# was produced by THIS run. A PNG here with no line below it is impossible.',
        '# Rendered at the width in plugin/GameData/DragonScreen/DragonScreen.cfg - the preview',
        '# DERIVES its size from the cfg and cannot render at any other (QC H-01).',
    ]
    for f in listing:
        lines.append(f + chr(9) + _png_size(os.path.join(out, f)))
    with open(manifest, 'w') as mf:
        mf.write(chr(10).join(lines) + chr(10))
    print('--- %d page(s) rendered; manifest at %s' % (len(listing), manifest))


def _png_size(path):
    """WxH out of a PNG's IHDR - big-endian 32-bit at offsets 16 and 20."""
    try:
        with open(path, 'rb') as fh:
            h = fh.read(24)
        if len(h) < 24:
            return '?'
        w = (h[16] << 24) | (h[17] << 16) | (h[18] << 8) | h[19]
        ht = (h[20] << 24) | (h[21] << 16) | (h[22] << 8) | h[23]
        return '%dx%d' % (w, ht)
    except OSError:
        return '?'


def _same(a, b):
    """
    Byte-identical? Hashed, not compared by size+mtime, because every build rewrites the file.

    THIS CANNOT HELP THE DLL, AND THAT IS NOT FIXABLE HERE. The csc at v4.0.30319 is the LEGACY
    .NET Framework compiler, not Roslyn: it has no /deterministic (checked, 2026-08-05), so it stamps
    a fresh MVID into every build and the output differs even when nothing in the source did. So the
    skip only ever applies to the cfg and any assets.

    Which is fine, because installing over a running game achieves nothing anyway - a DLL change AND
    a cfg change both need a full restart before they do anything. The guard refusing is correct
    behaviour, not an obstacle to route around.
    """
    def h(p):
        with open(p, 'rb') as fh:
            return hashlib.sha256(fh.read()).hexdigest()
    try:
        return h(a) == h(b)
    except OSError:
        return False


def _git_out(*args):
    """One-shot git, stripped. '' when git cannot answer - which the caller must treat as UNKNOWN,
    never as 'fine'. That distinction is the whole point of S262."""
    try:
        r = subprocess.run(['git'] + list(args), capture_output=True, text=True, cwd=ROOT)
    except OSError:
        return ''
    return (r.stdout or '').strip() if r.returncode == 0 else ''


def branch_guard():
    """
    ⛔⛔ S262 - SAY WHICH BRANCH IS BEING SHIPPED, AND REFUSE A SURPRISE.

    ⭐ THE PRINT IS THE HALF THAT WOULD HAVE PREVENTED 2026-09-10 ON ITS OWN. The owner reads this
    output; had it said `master` he would have stopped. The refusal is the belt to that brace.

    ⛔ IT LIVES IN `install()`'s CALL PATH AND NOT IN `__main__`, for the same reason `build_tests()`
    does: the file's own comment there says putting it here 'makes that impossible however install is
    invoked'. A guard that only fires on one spelling of the command is not a guard.

    ⚠ 'git cannot answer' is REFUSED, not waved through, and `--branch` cannot override it: if the
    branch is unknown then no declared name can be checked against it, and shipping a build this
    script cannot identify is precisely the failure S262 exists to stop.
    """
    branch = _git_out('rev-parse', '--abbrev-ref', 'HEAD')
    sha = _git_out('rev-parse', '--short', 'HEAD')

    # `--branch <name>` DECLARES the branch you mean to ship. It is checked AGAINST reality rather
    # than replacing it, so `--branch rebuild/base-screens` while sitting on master still refuses.
    want, explicit = EXPECT_BRANCH, False
    if '--branch' in sys.argv:
        i = sys.argv.index('--branch')
        if i + 1 >= len(sys.argv):
            sys.exit('⛔ --branch needs a branch name, e.g. --branch master')
        want, explicit = sys.argv[i + 1], True

    if not branch or not sha:
        sys.exit('⛔ INSTALL REFUSED - git could not name the checked-out branch.\n'
                 '   install will not ship a build it cannot identify (S262).\n'
                 '   Nothing was copied.')

    if branch != want:
        # ⭐ MEASURED, NOT ASSERTED: how far the intended branch is ahead of this one. A hardcoded
        # sentence ("master does not contain S240-S261") would be stale within a week; this is
        # computed every run and is true whenever it prints.
        ahead = _git_out('rev-list', '--count', branch + '..' + want)
        gap = ('\n   %s has %s commit(s) that %s does not.' % (want, ahead, branch)) if ahead else ''
        sys.exit('⛔⛔ INSTALL REFUSED - WRONG BRANCH.\n'
                 '   on branch : %s @ %s\n'
                 '   expected  : %s%s\n'
                 '   Nothing was copied. Check out %s, or pass --branch %s if you truly mean it.'
                 % (branch, sha, want, gap, want, branch))

    dirty = [l for l in _git_out('status', '--porcelain').splitlines() if l.strip()]
    print('install: branch %s @ %s%s' % (branch, sha, '   [--branch declared]' if explicit else ''))
    if dirty:
        # ⚠ WARN, NEVER REFUSE. Installing uncommitted work is legitimate and routine while
        # iterating in the capsule; being told you are doing it is the whole requirement.
        print('    ⚠ %d uncommitted change(s) in the tree - this ships WORKING-TREE state, not %s'
              % (len(dirty), sha))
        for l in dirty[:5]:
            print('        %s' % l)
        if len(dirty) > 5:
            print('        ... and %d more' % (len(dirty) - 5))


def report_orphans(src, dst):
    """
    ⛔ S262 - NAME WHAT IS IN THE GAME FOLDER THAT THIS BRANCH DID NOT PUT THERE, AND REMOVE NOTHING.

    ⚠ THIS IS THE CHECK THAT WOULD HAVE CAUGHT `SeatSwap.cfg` - a master-only file the wrong-branch
    install added, which a correct reinstall then LEFT BEHIND, because `install` copies in and has
    never pruned.
    ⛔ REPORTS ONLY. Deleting from a live game folder is a far larger decision than this script may
    take on its own: an orphan may be another mod's, a patch, or something the owner put there. The
    owner decides; the tool's job is that he cannot fail to know.
    """
    have = set()
    for root, _, names in os.walk(src):
        rel = os.path.relpath(root, src)
        for n in names:
            have.add(n if rel == '.' else os.path.join(rel, n))
    orphans = []
    for root, _, names in os.walk(dst):
        rel = os.path.relpath(root, dst)
        for n in names:
            f = n if rel == '.' else os.path.join(rel, n)
            if f not in have:
                orphans.append(f)
    if not orphans:
        return
    print('    ⚠ %d file(s) in the destination are NOT in this branch\'s build, and were NOT removed'
          % len(orphans))
    print('      (install copies in; it never prunes - delete by hand if unwanted)')
    for f in sorted(orphans):
        print('        %s' % f)


def install():
    branch_guard()
    dst = os.path.join(KSP, 'GameData', MOD)
    src = os.path.join(HERE, 'GameData', MOD)
    if not os.path.isfile(OUT_DLL):
        sys.exit('build first - no DLL at %s' % OUT_DLL)
    # ---- THE DLL GOES FIRST, AND THAT ORDER IS THE WHOLE GUARD ----
    # There is no reliable way to pre-test a Windows file lock: the previous version opened the
    # destination DLL in append mode and treated success as "unlocked", which does NOT fail on a
    # memory-mapped DLL. It sailed through and then died on the copy - AFTER the cfg had already been
    # written, leaving a NEW cfg against an OLD DLL. A mismatched install is worse than a failed one,
    # because it looks installed.
    #
    # So: copy the file that can fail FIRST. If it throws, nothing else has been touched and the
    # install is still whatever it was before - self-consistent, just old.
    #
    # AND IT IS NOT ALWAYS KSP. Caught 2026-08-05 with the game closed: CKAN (ckan-windows) keeps the
    # GameData tree open too. Anything that scans GameData will do it.
    os.makedirs(dst, exist_ok=True)

    # Walk SUBDIRECTORIES too - art/ lives under GameData/DragonScreen and the flat listdir tried to
    # copy the folder itself as a file, then reported it as a lock. An error that names the wrong
    # cause is worse than none; that lesson was already learned once here.
    files = []
    for root, _, names in os.walk(src):
        rel = os.path.relpath(root, src)
        for n in names:
            files.append(n if rel == '.' else os.path.join(rel, n))
    files.sort(key=lambda f: (not f.lower().endswith('.dll'), f))

    for f in files:
        s, d = os.path.join(src, f), os.path.join(dst, f)
        os.makedirs(os.path.dirname(d), exist_ok=True)
        # SKIP FILES THAT ARE ALREADY IDENTICAL. Rebuilding produces a fresh DLL every time even
        # when nothing in it changed, so a cfg-only edit was failing with "close KSP" over a file
        # that did not need writing at all. An error that names the wrong problem is worse than no
        # error - it teaches you to close the game for no reason.
        if os.path.isfile(d) and _same(s, d):
            print('    unchanged %s' % f)
            continue
        try:
            shutil.copy2(s, d)
        except OSError as e:
            sys.exit('could not write %s: %s\n'
                     '    Something has it open. CLOSE KSP **and CKAN**, then install again.\n'
                     '    Nothing was changed.' % (f, e))
        print('    installed %s' % f)
    print('--- installed to %s' % dst)
    report_orphans(src, dst)
    print('    KSP needs a FULL RESTART to pick up a DLL change.')


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'build'
    # MJ1: a DIAGNOSTIC verb. It does not build the plugin, does not run the tests and does not write
    # the shipped DLL - it only re-reads the vendored tree and writes the warning baseline.
    if cmd == 'mechwarn':
        mech_warning_baseline()
        print('--- ok')
        sys.exit(0)
    # S167: a DIAGNOSTIC verb over the ALREADY-BUILT test exe. `test` runs this check itself on every
    # run - this verb exists so the harness can be interrogated on its own, without a rebuild, when
    # what is in doubt is the instrument rather than the code.
    if cmd == 'harnesscheck':
        harness_fault_check()
        print('--- ok')
        sys.exit(0)
    # S168: the before/after preview harness. Renders a baseline in its own git worktree and the
    # working tree here, and reports the per-page difference by hash. It builds both trees itself, so
    # it does NOT fall through to build_plugin() below.
    if cmd == 'previewdiff':
        preview_diff(sys.argv[2] if len(sys.argv) > 2 else 'HEAD')
        print('--- ok')
        sys.exit(0)
    build_plugin()
    if cmd == 'test':
        build_tests()
    elif cmd == 'preview':
        build_tests()
        build_preview()
    elif cmd == 'install':
        # TESTS BEFORE SHIPPING. install used to copy whatever had just compiled, so a build with
        # failing tests could reach the game - and did, on 2026-08-05, because the shell chained on
        # a grep's exit code rather than this script's. build_tests() exits non-zero on failure, so
        # putting it here makes that impossible however install is invoked.
        build_tests()
        install()
    print('--- ok')
