# `plugin/mech/` — the vendored MechJeb2

**This file is DragonScreen's. Everything beside it is MuMech's.** It is the record required by
`docs/BUILD_PLAN.md` §B12.1 ("pin+record the exact upstream commit") and §B12.1a ("record the commit
— hash + date + branch — in this section and in the shipped source header"), written by register
task **T15a, 2026-09-05**.

Its job is that a reader can tell, without guessing, **what was taken, what was left behind and
why, what was changed, and under what licence any of it may be shipped.** A pin that does not say
what it excluded is not a pin.

---

## 1. THE PIN

| | |
|---|---|
| Repository | **`MuMech/MechJeb2`** (upstream — §B12.1a's RESOLVED G5a-Q1: no current or endorsed RO fork exists) |
| Branch | **`dev`** |
| Commit | **`c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa`** |
| Commit date | **2026-08-08** |
| Vendored | **2026-09-05** (register T15a) |
| Supplied as | `C:\Users\User\Desktop\MechJeb2-dev.zip`, **placed by the owner 2026-09-05** after the overseer flagged that C7 bars a build chat from fetching it |

The commit is stamped by GitHub into the zip's archive comment and is the first line of
`unzip -l MechJeb2-dev.zip`. It was **verified by the overseer from the archive itself** and
re-read from the archive here; it was not derived from anything else.

**`dev` is MuMech's DEVELOPMENT branch**, and that is deliberate: it is what "the most up-to-date
GitHub source" (§B12.1a directive 1) resolves to for this project. §B12.1a settles the apparent
tension in one line — *"'most up to date' governs what you fetch, 'pinned' governs what happens
after."* **There is no obligation to track upstream from here.** A re-pin is its own task, and it
is the only thing that may move this hash.

The pin also ships **inside the assembly**, as compile-time constants in
[`_dragonscreen/_Pin.cs`](_dragonscreen/_Pin.cs), and as a five-line banner at the top of every
vendored file that this build compiles — so a file read in isolation still says where it came from.

---

## 2. WHAT IS HERE — and the arithmetic

Upstream ships **339 `.cs`**. **All 339 are here.** §B12.1a is explicit that the port is
*"everything, dead code included"*, and it is: nothing was pruned for being unused by the
conductor. What varies is not what is *vendored* but what is *compiled*.

| | `.cs` | files | in `DragonScreen.Mech.dll`? |
|---|---:|---:|---|
| `alglib/` | 15 | 16 | ✅ compiled |
| `MechJebLib/` | 99 | 100 | ✅ compiled |
| `MechJebLibBindings/` | 8 | 9 | ✅ compiled |
| `MechJeb2/` | 128 | 140 | ✅ compiled |
| `MechJebKos/` | 23 | 25 | ❌ vendored only — §3.2 |
| `MechJebLibTest/` | 64 | 65 | ❌ vendored only — §3.2 |
| `MechJeb2-Unity/` | 1 | 27 | ❌ vendored only — §3.2 |
| `Icons/` `IconsPSD/` `Parts/` `Bundles/` `Localization/` | — | 58 | ❌ not C# |
| root: `LICENSE.md` `README.md` `CONTRIBUTING.md` `Plans.txt` `*.sln` `Directory.Build.*` `Makefile` `flake.*` `.editorconfig` `.envrc` `*.cfg` `GlobalSuppressions.cs` | 1 | 16 | ❌ — `GlobalSuppressions.cs` see §3.3 |
| `_dragonscreen/` | 2 | 2 | ✅ **ours, not MuMech's** — §4.3 |
| **on disk** | **341** | **458** | **247 compiled** |

The `.csproj`/`.sln`/`Directory.Build.*` files are kept even though this build never runs MSBuild.
They are the upstream record of **which references each project needs**, and `build.py`'s
`MECH_REFS` is checked against them — see §4.2. Deleting them would have made that list
unverifiable.

---

## 3. WHAT WAS EXCLUDED, AND WHY

### 3.1 Not vendored at all (5 paths, 12 files)

These are the only things in the archive that are **not** in this tree. Each is upstream's
*development apparatus*, not upstream's *port*.

| Excluded | Why |
|---|---|
| `bin/` (1 file, `bin/test`) | A bash script that drives MSBuild against the local KSP install. It is build/test apparatus, not source — and it reads the KSP install, which **C7 makes off-limits as a build source** regardless. |
| `.github/` (7 files) | GitHub Actions workflows + issue templates. MuMech's CI, meaningless here, and a live workflow file inside our repo is a hazard, not a record. |
| `.idea/` (4 files) | JetBrains Rider project config. IDE state. |
| `.gitignore` | ⚠ **Excluded for a specific reason, not tidiness.** A `.gitignore` in a vendored subtree **takes effect on our repository**: it ignores `bin/`, `[Oo]bj/`, `build/`, `*.meta`, `*.pdb` and more *inside `plugin/mech/`*, so a future upstream file matching any of those would silently fail to be committed and the pin would quietly stop being true. |
| `.gitattributes` | Same class of hazard: it sets `text=auto`, per-extension `text`/`binary` and `eol=lf` rules that would apply to our checkout. |

Verified after vendoring: **458 files on disk, 458 untracked, 0 ignored** by any `.gitignore` —
nothing was lost silently.

### 3.2 Vendored whole, but NOT compiled — whole projects

The task that vendored this asked for an explicit decision on the test project rather than a silent
drop. Here it is, with the other two that share its shape:

- **`MechJebLibTest/` (64 `.cs`) — VENDORED, NOT COMPILED.** It is an xunit + FluentAssertions test
  project. Neither framework is in this repo, and this build is `csc.exe` straight against KSP's
  managed assemblies — *"no IDE, no MSBuild, no NuGet"* (`build.py`'s opening comment). It cannot
  compile here, and nothing about the conductor needs it to. It is kept **complete and byte-exact**
  because it is upstream's own statement of what MechJebLib's maths is supposed to do — the most
  valuable thing in the tree for anyone later tuning §B7–§B11 — and because dropping it would break
  the pin. Compiling it is a separate proposal, not a silent omission.
- **`MechJebKos/` (23 `.cs`) — VENDORED, NOT COMPILED.** It needs `kOS.dll` and `kOS.Safe.dll`
  (`Directory.Build.props`: `$(KspDir)/GameData/kOS/Plugins/kOS.dll`). kOS is a separate mod; the
  only copy on this machine is inside the KSP install, and **C7 forbids reading the install as a
  build source**. There is no other copy in the repo, so per C7 this stops rather than reaching for
  it. It is also the kOS *scripting bridge* — nothing the conductor uses.
- **`MechJeb2-Unity/` (1 `.cs`, 27 files) — VENDORED, NOT COMPILED.** A Unity **editor** project
  that builds `Bundles/shaders.bundle`. Not a KSP assembly.

Because they are not compiled, they also did **not** receive the rename shell (§4.1): they sit here
exactly as upstream wrote them. Anything that later wants to compile one must rename it first.

### 3.3 Vendored, but not compiled — individual files

- **`*/Properties/AssemblyInfo.cs`** — upstream is five separate assemblies; this is one. Keeping
  five `AssemblyTitle`/`AssemblyVersion` sets in a single DLL is not possible, and picking one of
  them would be a lie about the other four. `DragonScreen.Mech`'s identity comes from its own
  `-out:` name instead (§4.2).
- **`GlobalSuppressions.cs`** (repo root + `MechJeb2/`) — ReSharper/analyser suppressions. They are
  **assembly-level attributes**, which cannot sit inside the private namespace, and they carry no
  runtime meaning without the analyser that reads them.

Both are enforced in one place — `build.py`'s `MECH_SKIP` — so the exclusion is code, not a comment
that can drift.

### 3.4 The three `[KSPAddon]`s — vendored, NOT compiled (added by **T15b**, 2026-09-05)

**This is a SAFETY exclusion, and it is the one on this page that must never be reverted casually.**
§7's warning block below found the problem; T15b closed it. **KSP instantiates `[KSPAddon]` classes
by scanning every assembly in `GameData` — nobody has to attach anything**, so vendoring MechJeb's
whole tree (§B12.1a, and correct) ships three self-starting `MonoBehaviour`s that would run inside
DragonScreen's assembly and speak with MechJeb's voice. §B12.1a's rule — *"vendored but never
registered/shown … ported ≠ enabled"* — is exactly what an addon that registers itself defeats.

| Excluded file | Addon | Why it had to go |
|---|---|---|
| `MechJeb2/CompatibilityChecker.cs` | `Startup.Instantly, true` | Version check that **can raise a popup dialog**. Referenced by nothing else in the compiled set (verified by grep over all 245 files). |
| `MechJeb2/InstallChecker.cs` | `Startup.MainMenu, true` | MechJeb's *"you installed it wrong"* **popup**. It looks for `GameData/MechJeb2`, which this layout deliberately does not have, so it is **likely to fire**. Referenced by nothing else. |
| `MechJeb2/MechjebBundlesManager.cs` | `Startup.MainMenu, false` | Loads `GameData/MechJeb2/Bundles/shaders.bundle`. For a user with no MechJeb that path does not exist; **for a user who has one it is a bundle already loaded by them**, and Unity refuses a second load of the same files — we would be printing errors into someone else's mod. |

⚠ **The class name is not always the file name.** `MechjebBundlesManager.cs` declares
`MechJebBundlesManager` (different capital J), which is why `build.py`'s `MECH_ADDONS_EXCLUDED`
lists them **by path**, and why a grep for the class name alone would have missed the file.

**One of the three has compiled dependents, so it has a SUBSTITUTE.** `GuiUtils.cs:905-906` and
`MechJebModuleDebugArrows.cs:319-320,614` read `MechJebBundlesManager`'s three statics, so dropping
the file alone is `CS0103`. `_dragonscreen/_BundlesManager.cs` supplies the three fields and nothing
else — the same shape as §4.3's `JetBrains.Annotations` substitution, and in the same DragonScreen-
owned directory. **`null` is not a degradation:** those fields are non-null only if the bundle
loaded, and in this layout upstream's own file would have hit its `if (assetBundle == null) yield
break` and left all three null anyway. Both consumers are GUI/debug paths this build never runs.

**⛔ THIS DOES NOT WEAKEN THE PIN, and the distinction is T15a's own.** All three files are still
**vendored, byte-exact** — §B12.1a's full-tree rule governs what is *vendored*, not what is
*compiled*, which is precisely the ground on which `MechJebKos/`, `MechJebLibTest/` and
`MechJeb2-Unity/` sit in §3.2. `mech_sources()` **fails the build** if a re-pin renames or moves one
of the three, rather than silently letting a self-registering addon back into the assembly.

---

## 4. WHAT WAS CHANGED — the rename shell, and nothing else

§B12.1 is a hard limit: **"Source kept intact (not rewritten) — rename shell only."** Three changes
were made to vendored files. There is no fourth. No warning was fixed, no method was touched, no
module was improved.

### 4.1 The namespace wrap (244 files)

Every compiled vendored `.cs` has its **entire body** wrapped:

```csharp
namespace DragonScreen.Mech
{
    …the file, unchanged, not even re-indented…
}
```

Nothing inside is edited. `namespace MuMech` becomes `DragonScreen.Mech.MuMech` by nesting, and
every internal reference — `using MuMech;`, `MechJebLib.Primitives.V3`, `alglib.minlp` — keeps
resolving, because C# resolves a namespace name by walking **outward through the enclosing
namespaces** before it looks globally. That is why this works as a pure wrap and needs no
search-and-replace through the bodies.

**One compiled file is deliberately NOT wrapped:** `MechJebLib/Utils/NullableAttributes.cs`, which
declares `namespace System.Diagnostics.CodeAnalysis`. Wrapping it would create
`DragonScreen.Mech.System`, and then every `using System;` in every wrapped file would bind to
*that* and fail to find `String`. Its attributes are `internal`, and .NET Framework 4.8 has no
`AllowNull`/`NotNullWhen` of its own, so there is nothing for them to collide with.

**The wrap is the §B3 requirement, and it was verified, not assumed.** Two probe files were
compiled against the finished `DragonScreen.Mech.dll`:

- `typeof(DragonScreen.Mech.MuMech.MechJebCore)` and four siblings → **compiles**.
- `typeof(MuMech.MechJebCore)`, `MechJebLib.Primitives.V3`, `alglib`, `MechJebLibBindings.…` →
  **CS0246, all four**: `MuMech`, `MechJebLib`, `alglib` and `MechJebLibBindings` do not exist at
  global scope. A user's own `MechJeb2.dll` has nothing of ours to collide with.

### 4.2 A private assembly, not just a private namespace

§B12.1 asks for *"a private namespace **+ assembly**"*, and the two do different jobs. The namespace
stops a **type** clashing; the separate assembly is what makes the tree buildable at all, because
vendored MechJeb needs a different compiler contract from ours:

| | `DragonScreen.dll` (ours) | `DragonScreen.Mech.dll` (vendored) |
|---|---|---|
| `-langversion` | `latest` | **`8`** — upstream's own `<LangVersion>`, so nothing here can start depending on a newer C# than upstream compiles with |
| `-nullable` | (off) | **`annotations`** — `MechJebLib` is written `<Nullable>enable</Nullable>`, so its `T?` must parse; warnings stay off because we may not act on them |
| `-warn` | `4` | **`0`** — see below |
| `-define` | — | **`UNITY_2017_1`**, per `MechJeb2.csproj` |
| references | `REFS` | `MECH_REFS` = `REFS` + `Assembly-CSharp-firstpass`, `UnityEngine.AnimationModule`, `.AssetBundleModule`, `.VehiclesModule` — **taken from the vendored `MechJeb2.csproj`/`MechJebLibBindings.csproj`, which is why they were kept** |

⛔ **Warnings are OFF for this assembly, and that is a consequence of the rename-shell rule, not
laziness.** We are not permitted to fix MechJeb's warnings, so printing several hundred unfixable
ones on every build would train the eye to scroll past exactly the region where a real **error**
appears. Errors are unaffected: `csc` still reports them and `build.py`'s `run()` still fails the
build on a non-zero exit.

The assembly's identity is its `-out:` name, `DragonScreen.Mech` — distinct from `MechJeb2`,
`MechJebLib`, `MechJebLibBindings` and `alglib`, so it cannot collide with an installed MechJeb at
the assembly level either.

### 4.3 `JetBrains.Annotations` — a build-dependency substitution (46 files touched)

Upstream pulls `JetBrains.Annotations` 2023.3.0 from NuGet, and 46 files open with
`extern alias JetBrainsAnnotations;`. This build has no NuGet, and an `extern alias` needs a real
second assembly to alias — which a single-assembly build cannot provide. So:

- the line `extern alias JetBrainsAnnotations;` is **removed**, and
- `using JetBrainsAnnotations::JetBrains.Annotations;` is folded to `using JetBrains.Annotations;`;
- [`_dragonscreen/_JetBrainsAnnotations.cs`](_dragonscreen/_JetBrainsAnnotations.cs) supplies that
  namespace **inside `DragonScreen.Mech`**, so the wrap's outward lookup binds to it and no global
  `JetBrains` type is published from our assembly.

Only the three members MechJeb actually uses are defined — `UsedImplicitly` (208 uses),
`MeansImplicitUse` (1), and the two enums `UsedImplicitly`'s constructors take. They are inert
markers, read by ReSharper and never at runtime. Defining more would be inventing API the tree does
not ask for; if a re-pin needs another, the compiler names it.

`_dragonscreen/` holds **only** this and `_Pin.cs`. Both are ours, both are marked as such in their
own headers, and they are the only files in `plugin/mech/` that MuMech did not write.

---

## 5. LICENCE — two separate checks

DragonScreen's own `LICENSE` at the repo root is **already GPL-3.0**, and `NOTICE` carries the
per-work third-party record. Both checks below are recorded there in summary and here in full.

### 5.1 MechJeb2 itself — GPLv3. Obligation accepted.

`plugin/mech/LICENSE.md`, verbatim first line:

> This software is released under the GNU GPL version 3, 29 July 2007.

…with the full GPLv3 text following (637 lines) and the summary `MechJeb2 Copyright (C) 2013`.
This confirms §B2's finding against the vendored copy rather than against the installed one.

**The obligation, per §B2/§B12.1:** distribution is public, so DragonScreen is a **GPLv3 combined
work** and must ship under GPLv3 **with the embedded MechJeb source**. Both halves are satisfied
already: the repo licence is GPLv3, and `plugin/mech/` *is* the source, in the repository, complete,
at a recorded commit. `LICENSE.md` travels with it, unmodified.

### 5.2 `alglib/` — dual-licensed upstream. **This copy is the GPL edition.** Compatible.

ALGLIB is distributed by its author in two editions — a **free GPL** edition and a **paid
commercial** edition — so which one MuMech vendored is a question that has to be answered from the
files, not assumed. It was.

**All 15 `alglib/*.cs` carry one identical licence header, once each** (verified file by file):

> ALGLIB 4.08.0 (source code generated 2026-06-08)
> Copyright (c) Sergey Bochkanov (ALGLIB project).
> `>>> SOURCE LICENSE >>>` This program is free software; you can redistribute it and/or modify it
> under the terms of the GNU General Public License as published by the Free Software Foundation
> (www.fsf.org); **either version 2 of the License, or (at your option) any later version.**

**Verdict: GPL-2.0-or-later → GPLv3-compatible, and shippable in this GPLv3 work.** The
"or (at your option) any later version" clause is what does it: it permits use under GPLv3, which is
precisely the compatibility question §B2 asks. There is no ambiguity here to escalate.

Two things that look like counter-evidence and are not, checked rather than waved past:

- Seven files (`dataanalysis`, `interpolation`, `linalg`, `minlp`, `optimization`, `solvers`,
  `statistics`) contain blocks headed `! COMMERCIAL EDITION OF ALGLIB:`. These are **documentation
  comments advertising the paid edition's extra speed** — sales copy inside the free edition. They
  grant nothing and restrict nothing.
- `ap.cs` has three `#if _ALGLIB_COMMERCIAL` regions. **`_ALGLIB_COMMERCIAL` is never defined by
  this build** — `build.py` defines exactly one symbol for this assembly, `UNITY_2017_1` — so no
  commercial-edition code is compiled into `DragonScreen.Mech.dll`.

There is no separate `alglib/LICENSE` file in the tree; the per-file headers **are** the licence
grant, and they ship with the source.

### 5.3 The other third-party code inside MechJeb — all compatible

Checked at the same standard, since MechJeb is itself a combined work:

| Component | Licence as stated in the file | GPLv3-compatible |
|---|---|---|
| `MechJebLib/` — 98 files | SPDX: `LicenseRef-PD-hp OR Unlicense OR CC0-1.0 OR 0BSD OR MIT-0 OR MIT OR LGPL-2.1+` (Lamont Granquist, Sebastien Gaggini, MechJeb contributors) | ✅ (any of them) |
| `MechJeb2/MechJebModuleGuidanceController.cs`, `MechJebModulePSGGlueBall.cs`, `MechJebModuleSpinupController.cs` | Lamont Granquist — "Dual licensed under the MIT license and GPLv2 or any later version" | ✅ |
| `MechJeb2/CompatibilityChecker.cs` | Majiir, 2014 — BSD 2-clause | ✅ notice retained in-file |
| `MechJeb2/ToolbarWrapper.cs` | Maik Schreiber, 2013–2016 — BSD 2-clause | ✅ notice retained in-file |
| `MechJebLib/Utils/NullableAttributes.cs` | Microsoft / .NET Foundation — MIT | ✅ notice retained in-file |
| `MechJeb2/UnityToolbag/` (`Dispatcher`, `Future`) | ⚠ **no licence notice in the vendored copy** — the two `README.md` are documentation only | Redistributed exactly as MuMech distributes it, unmodified, under MechJeb2's GPLv3. Recorded, not fixed: this is an upstream gap and altering it would exceed the rename shell. |

Every BSD/MIT notice above sits inside a file that ships in this repo, which is what those licences
require.

---

## 6. HOW TO REPRODUCE THIS TREE

1. Take `MuMech/MechJeb2` at `c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa`.
2. Copy everything into `plugin/mech/` **except** `bin/`, `.github/`, `.idea/`, `.gitignore`,
   `.gitattributes` (§3.1).
3. For every `.cs` under `alglib/`, `MechJebLib/`, `MechJebLibBindings/`, `MechJeb2/`, **except**
   `*/Properties/AssemblyInfo.cs`, `GlobalSuppressions.cs` and
   `MechJebLib/Utils/NullableAttributes.cs`: drop any `extern alias JetBrainsAnnotations;` line,
   rewrite `using JetBrainsAnnotations::JetBrains.Annotations;` to `using JetBrains.Annotations;`,
   prepend the five-line provenance banner, and wrap the whole body in `namespace DragonScreen.Mech
   { … }`. Preserve each file's byte-order mark.
4. Add `_dragonscreen/_Pin.cs` and `_dragonscreen/_JetBrainsAnnotations.cs`.

Step 3 touches 244 files; steps 2–4 leave 341 `.cs` on disk of which `build.py` compiles 247.

---

## 7. WHAT COMPILES IT, AND WHAT DOES **NOT** RUN IT

Stated plainly, because it is easy to read `test` as more than it is:

- **Every `build.py` verb compiles this tree.** `__main__` calls `build_plugin()` **unconditionally**
  before it dispatches on the command, and `build_plugin()` now calls `build_mech()` first. So
  `build.py`, `build.py test`, `build.py preview` and `build.py install` all build
  `DragonScreen.Mech.dll` — a compile error here fails all four.
- **Nothing executes a line of it.** The headless C# suites link `src/pure` + `test`; the preview
  renderer links `src/pure` + `preview` (`build_tests`, `build_preview`). **Neither references
  `DragonScreen.Mech.dll`**, and no test calls into MechJeb. `build.py test` being green proves the
  vendored tree **compiles**; it proves nothing about its behaviour.
- **Behaviour needs the game.** Loading a `MechJebCore` requires KSP — which is `install` + glass
  time, and those are **separate owner gates, per session** (`CLAUDE.md` §0 banner, C1.12). T15a
  neither needed nor used them.
- The screens still fly nothing. §14.4(a) holds: the command buttons remain an honest no-op. This
  task shipped an assembly, not an autopilot — **and the GUI in it is not yet suppressed**, which is
  T15b's first job (§B12.1a: *ported ≠ enabled*).

### ✅ CLOSED BY T15b, 2026-09-05 — the three addons are out of the compiled assembly

**The block below is T15a's finding and stands as written; this note records what answered it.**
All three `[KSPAddon]` files are still vendored and are no longer compiled — see **§3.4** for the
mechanism, the substitute the third one needed, and the build-time guard that stops a re-pin from
quietly undoing it. The compiled set went **247 → 245** files (−3 addons, +1 substitute).

⚠ **`install` and glass time are still separate owner gates and T15b did not open either**, so this
DLL has still never been near the game. What changed is that the reason for the ban is gone: the
count of self-registering `[KSPAddon]`s in `DragonScreen.Mech.dll` is now **zero**, verified from
`mech_sources()` and re-checked on every `build.py test` (`test/MechHostTest.cs`).

### ⛔ DO NOT `install` THIS BUILD UNTIL T15b LANDS — and the reason is sharper than "no core yet"

Found while vendoring, and it widens T15b rather than confirming it. It is **not** true that a
vendored-but-unhosted MechJeb is inert until someone attaches a `MechJebCore`. **KSP instantiates
`[KSPAddon]` classes by scanning every assembly in `GameData` — nobody has to attach anything.**
This tree ships three of them:

| Class | Trigger | What it would do, in a build with no MechJeb host |
|---|---|---|
| `MechJeb2/CompatibilityChecker.cs:52` | `KSPAddon.Startup.Instantly, true` | version check; can raise a **popup dialog** |
| `MechJeb2/InstallChecker.cs:20` | `KSPAddon.Startup.MainMenu, true` | MechJeb's *"you installed it wrong"* **popup** — and it looks for `GameData/MechJeb2`, which our layout deliberately does not have, so it is likely to fire |
| `MechJeb2/MechjebBundlesManager.cs:14` | `KSPAddon.Startup.MainMenu, false` | loads `Bundles/shaders.bundle` from a path our layout does not provide |

`MechJebCore` is a `PartModule`, so *that* still needs a part cfg to attach and none is shipped —
the vendored `Parts/` never reaches `GameData`. The `[KSPAddon]`s need nothing.

**Nothing is at risk today**: `install` and glass time are separate owner gates and neither was
opened for T15a, so this DLL has never been near the game. But the next task to open that gate must
handle these three first. **T15b's GUI suppression therefore covers `[KSPAddon]` auto-registration,
not only window drawing.**

Cost, measured: a full `python plugin/build.py test` takes **~8 s end to end** with the vendored
tree in it, suites and tool selftest included. Not a problem, and recorded so a later re-pin can
tell a regression from the baseline.

---

## Open questions for the owner

Neither of these blocked T15a, and neither was decided by this chat (C1.14). Both are consequences
of §3.2 — projects that are vendored whole and deliberately not compiled.

### Q1 — should upstream's own maths test suite be made to run?

**Situation.** `MechJebLibTest/` (64 files) is vendored complete but not compiled: it needs xunit +
FluentAssertions, and this build has no NuGet. It is upstream's own statement of what MechJebLib's
maths — Lambert, PSG/PVG, the two-body solvers, the fuel-flow simulation — is supposed to do. That
is unusually valuable *here*, because this repo's whole method is the pure/headless split, and
`build.py test` already runs 5,000-odd headless checks against `src/pure`. Running MechJeb's own
suite would give the same kind of evidence for the code that will actually fly the vehicle, and
would turn a future re-pin from "it still compiles" into "it still computes the same answers".

1. **Leave it vendored and uncompiled.** Zero cost, zero risk. The suite stays available as
   reference to read; nothing verifies the vendored maths before it flies.
2. **Write a minimal in-repo runner** (a few hundred lines of `[Fact]`/assertion shims) so the suite
   compiles and runs inside `build.py test`, with no NuGet. Real work, and some upstream tests will
   need per-test exclusion where they lean on FluentAssertions idioms.
3. **Vendor xunit + FluentAssertions as well.** Faithful, but it puts two more third-party trees in
   the repo, each needing its own licence check, to run tests for code we do not modify.

**Recommendation: (1) now, and raise (2) as its own task only if T18–T22's in-sim tuning starts
producing results that are hard to explain.** The suite tests code we are forbidden to change
(rename shell only), so a failure would not be ours to fix — it would be a re-pin decision. That
makes it diagnostic rather than protective, and diagnostics are worth building when there is
something to diagnose.

### Q2 — `MechJebKos/` needs a file C7 will not let a build chat take

**Situation.** `MechJebKos/` (23 files, MechJeb's kOS scripting bridge) references `kOS.dll` and
`kOS.Safe.dll`. `Directory.Build.props` resolves them from `$(KspDir)/GameData/kOS/Plugins/`, and
**C7 makes the KSP install off-limits as a build source**, so T15a stopped rather than reaching for
them. There is no other copy in the repo. Nothing in the conductor (§B4, §B12.3) touches kOS.

1. **Leave it vendored and uncompiled, permanently.** The pin stays complete; the bridge stays dead.
2. **Owner supplies the two kOS assemblies into the repo** — the same move that unblocked T15a's
   own source — and a later task compiles the project.
3. **Drop it at the next re-pin.** Would make the pin incomplete, so only worth it if kOS is ruled
   permanently out of scope.

**Recommendation: (1).** It costs nothing, keeps the pin honest, and the conductor's design has no
place for a kOS bridge. (2) is only worth the owner's time if they later want to *script* MechJeb
from kOS, which is a different project from the conductor.
