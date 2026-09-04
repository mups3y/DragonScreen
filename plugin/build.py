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

THE ONE THING THAT WILL BITE YOU: a DLL change needs a full game restart, and so does a cfg change -
ModuleManager applies patches at load. There is no in-flight reload worth trusting. KSP must be
closed to overwrite the DLL, AND SO MUST CKAN, which keeps the GameData tree open.

That is why `preview` exists: restarts are the scarce resource, so anything that can be judged
outside the game - layout, proportion, palette, legibility - is judged from a PNG, and a restart is
spent only on what needs the capsule.
"""
import io, os, subprocess, sys, shutil, hashlib

NL = chr(10)          # response-file line separator, spelled out so no edit can eat the escape

HERE = os.path.dirname(os.path.abspath(__file__))
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
    print((p.stdout or '') + (p.stderr or ''))
    if p.returncode != 0:
        sys.exit('TESTS FAILED (exit %d)' % p.returncode)
    tool_tests()


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
    tool = os.path.join(HERE, 'tools', 'assess_flight.py')
    if not os.path.exists(tool):
        return
    print('--- running tool selftests')
    p = subprocess.run([sys.executable, tool, '--selftest'], capture_output=True, text=True)
    print((p.stdout or '') + (p.stderr or ''))
    if p.returncode != 0:
        sys.exit('TOOL SELFTEST FAILED (exit %d)' % p.returncode)


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
    # System.Drawing is a .NET Framework assembly and is NOT in KSP's Managed folder, so under
    # -nostdlib it has to be named by full path from the framework directory. Nothing here ships.
    drawing = 'System.Drawing.dll'
    if MODERN:
        drawing = os.path.join(os.path.dirname(CSC_LEGACY), 'System.Drawing.dll')
    run(compile_cs(exe, src, refs=[drawing], exe=True),
        'preview renderer (%d source files)' % len(src))
    print('--- rendering pages')
    p = subprocess.run([exe, out], capture_output=True, text=True)
    print((p.stdout or '') + (p.stderr or ''))
    if p.returncode != 0:
        sys.exit('PREVIEW FAILED (exit %d)' % p.returncode)


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


def install():
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
    print('    KSP needs a FULL RESTART to pick up a DLL change.')


if __name__ == '__main__':
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'build'
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
