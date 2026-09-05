# BACKLOG RECONCILIATION — one owner for every hole and every finding

**Register line:** `S125` · **Written** 2026-09-06 by the continuous build chat ·
**Sources, read-only:** `docs/SCREEN_LIVENESS_AUDIT.md` (S49) · `docs/QC_FINDINGS.md` (QC's file, **not
edited** by this task) · `REGISTER.md`

---

## 0. WHY THIS DOCUMENT EXISTS

Two independent audits walked the same screens and **neither knows about the other.**

| | |
|---|---|
| `docs/SCREEN_LIVENESS_AUDIT.md` (S49, 2026-09-03) | **45 holes, H1–H45**, each classed (A) buildable now / (B) Part-B's / (C) correctly static. It proposed register lines for a **subset only** — S50–S57. |
| `docs/QC_FINDINGS.md` (QC, swept independently) | **72 findings**. Its own 2026-09-06 verification pass counts **38 remaining**: 30 never actioned, 4 part-closed, 1 not closed, 2 verified still open, 1 unjudgeable. |

⛔ **The same defect is currently reachable from up to three places** — a hole, a QC finding, and a register
line — with nothing anywhere saying they are the same thing. A build chat picking work off any one of the
three has no way to know the other two exist. **That is what this document fixes. It fixes no defect.**

⛔ **This document does not decide anything an owner owns.** Where an item needs a gate, an `OVERRIDE`, or
the owner's taste (C1.14), it is marked and left.

---

## 1. THE HEADLINE: WHAT THE RECONCILIATION ACTUALLY FOUND

1. **Twelve duplicate pairs.** Twelve QC findings and twelve liveness holes are the *same defect described
   twice*. Named in §2. Three were already known (the brief that opened this task named them: `C-01`≡`H1`,
   `S-01`⊃`S35`, `F-02`≡`H13`); **nine were not.**
2. **`S35` is CLOSED by a fix that landed under a different line.** QC's `S-01` is the same defect, `S104`
   fixed it on 2026-09-05 under an owner directive, and the code was verified. **See §4 — this is the one
   item this document takes off the board rather than merely assigning.**
3. **Two QC findings closed by THIS session** and not yet reflected in QC's own count: `C-05` (by `S123` +
   `S116`) and `R-02` (by job 2 of the 2026-09-06 batch). **The live remaining count is 36, not 38.**
4. **Twenty-five liveness holes had no owner at all** — the audit only ever proposed S50–S57, which covers
   17 holes. The rest have sat unowned since 2026-09-03.
5. **`R-01` is the largest unowned item in the project** and had **no register line** despite 18 mentions.
   It is one owner decision that gates a large fraction of everything else. Now `S153`, and **HELD**.

---

## 2. THE DUPLICATE PAIRS — same defect, two names

⭐ **These are the reason this document exists.** Fixing either member fixes both; closing one without the
other leaves a false open item.

| QC | liveness hole | the shared defect | owner after this reconciliation |
|---|---|---|---|
| `C-01` | **H1** | The Cover's top telemetry strip is baked art from someone else's flight | **[[S50]]** — QC records `C-01` CONFIRMED CLOSED; H1's other half (the HUD, H10) is still S50's |
| `C-08` | **H6** | `ENTRY ENABLED` shows a baked verdict; what the row *means* is undecided | **[[S129]]** — HELD, owner answered Q3 but the answer is not yet actionable |
| `C-14` | **H3** | Both TARGET readouts are baked pictures of the same wrong value | **[[S126]]** |
| `H-02` | **H10** | Every docking-HUD readout is a pixel; 8 of 12 contradict live state | **[[S50]]** |
| `F-02` | **H13** | `UiPage.Procedure` and `UiPage.Cabin` are flat PNGs with no state | **[[S136]]** |
| `S-01` | **H14/H15 → and `S35`** | Gauge ring colours were constants asserting a verdict | ✅ **CLOSED** by `S104` — see §4 |
| `S-02` | **H16** | The ALERTS view is one word, and the FDIR bar is a fake three-position gauge | **[[S137]]** |
| `S-03` | **H14** (remainder) | 23 of 36 subsystem state words are still literals after `S51` | **[[S138]]** |
| `S-04` | **H17** | The ~27 honest dashes — a §14.4(f) policy surface, not a page defect | **[[S139]]** — HELD, policy question |
| `V-04` | **H18 + H39** | `LifeSupport.Margins` is computed every frame and shown on no screen | **[[S140]]** |
| `DK-03` | **H25** | There is no camera behind the docking rings | **[[S141]]** |
| `RZ-01` | **H27 + H28** | Hold-Capture card reads NOT ENGAGED forever; arrows and rail inert | **[[S143]]** |
| `NO-02` | **H36** | The standalone orbit plot cannot use the zoom/pan `S43` built | **[[S57]]** (SPLIT) + **[[S62]]** — already owned, relationship recorded |
| `SC-01` | **H19** | The suit procedure is drawn complete before it starts | **[[S55]]** |
| `MC-02` | **H22** | Six live altitude gates and nothing says which is next | **[[S55]]** |
| `AS-01` | **H34** | Eleven ascent events, none tracked, the step machine runs unread | **[[S55]]** |
| `VT-01` | **H21** | The VRIO page takes no state and has no touch (tints done, tracking not) | **[[S55]]** |

⚠ **`S55` alone absorbs four QC findings** (`SC-01`, `MC-02`, `AS-01`, `VT-01`) plus five holes. Anyone
picking up S55 is picking up more than its own text says, and its line now records that.

---

## 3. EVERY LIVENESS HOLE, WITH ITS OWNER

**(A)** buildable now · **(B)** Part-B's, no Part-A build proposed · **(C)** recorded, correctly static.

| hole | class | owner | state |
|---|---|---|---|
| H1 | A | [[S50]] | TODO (QC `C-01` closed the Cover half) |
| H2 | A | [[S50]] | TODO |
| H3 | A | **[[S126]]** *(new)* | TODO |
| H4 | A | **[[S127]]** *(new)* | TODO — slots 3/4 owner-declined (S27), slots 0/1/2 buildable |
| H5 | A | **[[S128]]** *(new)* | TODO — prerequisite [[S54]] is DONE |
| H6 | A/B | **[[S129]]** *(new)* | **HELD** — owner answered Q3, not yet actionable |
| H7 | A | **[[S130]]** *(new)* | TODO |
| H8 | A | [[S54]] | ✅ DONE |
| H9 | C | — | recorded, no build owed |
| H10 | A | [[S50]] | TODO |
| H11 | mixed | **[[S132]]** *(new)* | TODO (A parts); `FAR FIELD POSITIONING` is (B) |
| H12 | A | **[[S134]]** *(new)* | TODO |
| H13 | A | **[[S136]]** *(new)* | TODO |
| H14 | A | [[S51]] + **[[S138]]** | S51 DONE (the guard); S138 owns the remaining 23 literals |
| H15 | A | [[S51]] | ✅ DONE |
| H16 | A | **[[S137]]** *(new)* | TODO |
| H17 | A | **[[S139]]** *(new)* | **HELD** — policy question |
| H18 | A | **[[S140]]** *(new)* | TODO |
| H19 | A | [[S55]] | TODO |
| H20 | A | [[S52]] | TODO |
| H21 | A | [[S55]] | TODO |
| H22 | A | [[S55]] | TODO |
| H23 | B | — | §14.4(a); Part B wires it (§B12.5) |
| H24 | B | — | **decided-(a)**, S28, owner 2026-09-02 |
| H25 | A | **[[S141]]** *(new)* | TODO |
| H26 | C/A | **[[S142]]** *(new)* | TODO — the disambiguation IS the research |
| H27 | A | **[[S143]]** *(new)* | TODO |
| H28 | A | **[[S143]]** *(new)* | TODO |
| H29 | B | — | §14.4(e)(3); audit's Q4 flags the (f) tension and does not decide it |
| H30 | A | **[[S144]]** *(new)* | TODO — evaluation is (A), the wording is (C) and must not be edited |
| H31 | A | [[S55]] | TODO |
| H32 | A | [[S56]] | ✅ DONE |
| H33 | A | [[S56]] | ✅ DONE |
| H34 | A | [[S55]] | TODO |
| H35 | A | **[[S145]]** *(new)* | TODO |
| H36 | A | [[S57]] (SPLIT) + [[S62]] | see §2; QC `NO-02` is the same work |
| H37 | A | [[S52]] | TODO |
| H38 | A | **[[S146]]** *(new)* | TODO |
| H39 | A | [[S57]] (SPLIT) + **[[S140]]** | `Margins` half → S140; `Orbital`/`Hohmann` half → S57 |
| H40 | A | **[[S147]]** *(new)* | TODO |
| H41 | A | [[S53]] | ✅ DONE |
| H42 | A | [[S53]] | ✅ DONE |
| H43 | C | — | recorded; S10b/S37/S42 own the scaled-space camera and are HELD |
| H44 | C | [[S57]] (SPLIT) | hygiene |
| H45 | A | **[[S148]]** *(new)* | TODO |

**Counts.** 45 holes: **8 DONE** · **20 newly owned here** · **9 already owned and still TODO** ·
**5 (B)/decided** · **3 (C) with nothing owed** (H9, H43, and H44's hygiene half).

---

## 4. ⭐ THE ONE ITEM THIS DOCUMENT TAKES OFF THE BOARD: `S35`

`S35` has sat **TODO — owner call** since it was logged. It should not still be open.

**The defect it describes is verifiably gone.** S35's finding: *"the gauge arcs never consult severity at
all — they are fixed identity colours"*, with `#d12c30` (byte-identical to `DragonPalette.Alarm`) on CABIN
TEMP, seen on glass 2026-09-03. QC filed the same defect independently as **`V-01` + `S-01`**, measured its
real scope at **32 gauges across 7 pages**, and `S104` fixed all 32 on 2026-09-05.

**Verified in the current source, not taken on trust:**
- `VehicleOverviewPage.cs:129-154` — all eight rings now `GC(Alarms.Band(…, CabinLimits.…))`.
- `VehicleSubsystemPage.cs:347/387/442/484/519/569` — all six descriptors now
  `Alarms.GaugeColour(Alarms.Band(…) | Alarms.Low(…), valid)`, with `Accent` retained where the model has
  **no threshold**.
- QC's own verification pass lists `S-01` under **CONFIRMED CLOSED**.

**And the fix had authority.** `S104` is 🟢 OWNER-DIRECTED, verbatim: *"continue with all the screen fixes
one at a time until all pages are complete. You must confirm your findings before fixing."*

⚠ **What that does and does not settle, stated precisely.** S35 asked an owner to choose between keeping the
reference's identity colours and letting severity drive them. **S104 implemented severity-drives-colour**
under a general directive, not as an answer to S35's specific question — so the *defect* is closed by
evidence, while the *aesthetic question* was never put to the owner in those words. ⭐ **One residual is
therefore genuinely open and is written into S35's closing note rather than buried:** for the twelve gauges
with **no** threshold in the model, S104 chose `Accent` — the honest "this is a reading, not a verdict"
colour — which is neither the reference's identity colour nor an invented band. **If the owner wants the
reference's identity palette back on those twelve, that is an `OVERRIDE` (C1.8) and a fresh line.** No
build chat may decide it, and this document does not.

---

## 5. EVERY REMAINING QC FINDING, WITH ITS OWNER

QC's roll-up says **38 remain**. ⭐ **Two of those have since been closed and QC's count predates it:**
`C-05` (by [[S123]] + [[S116]], this session) and `R-02` (by job 2 of the 2026-09-06 batch). **The live
figure is 36.** QC's file is not edited to say so (it is QC's); this is recorded here and in the register.

| QC | owner | note |
|---|---|---|
| `C-02` | **[[S131]]** *(new)* | stray arrow; owner answered Q1 "Drop it" — **HELD**, answer not yet actionable |
| `C-05` | [[S123]] + [[S116]] | ✅ **CLOSED this session** |
| `C-07` | — | claimed fixed, headless check green, not judgeable from a render |
| `C-08` | **[[S129]]** *(new)* | **HELD** — ≡ H6 |
| `C-09` | — | **glass-gated**; owner: *"Check it during the 2560 install"* |
| `C-11` | **[[S152]]** *(new)* | preview/game tint-rect mismatch — a code-structure claim, unjudgeable from a render |
| `C-14` | **[[S126]]** *(new)* | ≡ H3 |
| `H-02` | [[S50]] | ≡ H10 |
| `H-05` | **[[S133]]** *(new)* | the docking HUD's 822 px empty ALERT ACTIVITY panel |
| `A-01` `A-03` `A-04` | **[[S134]]** *(new)* | settings/audio structural + layout |
| `A-02` | **[[S135]]** *(new)* | **HELD** — owner answered Q6, not yet actionable |
| `A-05` `A-06` | **[[S135]]** *(new)* | ⚠ both wait on the community Figma export, which is **not in the repo (C7)** |
| `F-02` | **[[S136]]** *(new)* | ≡ H13 |
| `F-03` `F-04` | **[[S134]]** *(new)* | F-04 is a real coordinate-system defect, not cosmetic |
| `V-04` | **[[S140]]** *(new)* | ≡ H18 + H39 |
| `S-02` | **[[S137]]** *(new)* | ≡ H16 |
| `S-03` | **[[S138]]** *(new)* | the remainder after S51 |
| `S-04` | **[[S139]]** *(new)* | **HELD** — ≡ H17, policy |
| `MP-01` | **[[S149]]** *(new)* | part-closed: colour agrees, the words still differ |
| `MP-02` | **[[S149]]** *(new)* | one value, three names, three colours |
| `MP-03` | **[[S139]]** *(new)* | the dash surface again |
| `SC-01` | [[S55]] | ≡ H19 |
| `VV-02` | **[[S134]]** *(new)* | part-closed: fixture renders, the writer is still stranded |
| `VT-01` | [[S55]] | part-closed: ≡ H21 |
| `MC-02` | [[S55]] | ≡ H22 |
| `DK-03` | **[[S141]]** *(new)* | ≡ H25 |
| `RZ-01` | **[[S143]]** *(new)* | part-closed: ≡ H27 + H28 |
| `DB-01` `DB-02` `DB-03` | **[[S150]]** *(new)* | corner layout · triplicate content · no touch |
| `NO-02` | [[S57]] + [[S62]] | ≡ H36 |
| `AS-01` | [[S55]] | ≡ H34 |
| `AS-02` | **[[S151]]** *(new)* | the Ascent page uses the left 40% |
| `R-01` | **[[S153]]** *(new)* | ⭐ **HELD — the largest unowned item in the project.** One owner decision |
| `R-02` | job 2, 2026-09-06 | ✅ **CLOSED** |

---

## 6. WHAT IS HELD, AND ON WHAT

⛔ **A build chat may not start any of these.** Each is C1.14's (1), (2) or (3) — a gate, an `OVERRIDE`, or
the owner's taste.

| line | held on |
|---|---|
| **[[S153]]** (`R-01`) | **The one that matters.** Every sampled text element on every Figma-era page is below the measured legibility floor at the shipped width. Fixing it is a global type-scale decision — the owner's taste, and it gates a large fraction of the fix lines below it |
| **[[S139]]** (`H17`/`S-04`/`MP-03`) | §14.4(f) policy: whether ~27 real-but-unmodelled quantities get marked micro-sims, and how far |
| **[[S129]]** (`H6`/`C-08`) | Owner answered Q3 in full, verbatim — but under his own condition: *"I will answer them and then ask the overseer to assess before acting on them."* **Not yet actionable** |
| **[[S131]]** (`C-02`) | Q1 "Drop it" — same condition |
| **[[S135]]** (`A-02`) | Q6 answered — same condition; and `A-05`/`A-06` additionally need the Figma export, which C7 puts outside the repo |
| `C-09` | Glass. Owner: *"Check it during the 2560 install"* |
| `S10b` `S18` `S37` `S42` `S47` `BB8` `S98` `T18`–`T22` | Unchanged: every one has an in-sim DONE-when |

⚠ **The Q1–Q9 answers are a real bottleneck and it is worth stating plainly.** Four of the lines above are
blocked not because the owner has not decided but because **the overseer assessment he asked for has not
happened**. That is one action, and it unblocks four lines.

---

## 7. WHAT THIS RECONCILIATION DID NOT DO

- **It did not re-audit anything.** No hole was re-walked, no finding re-derived. Where this document states
  a defect is gone (`S35`/`S-01` in §4) it says which file and which lines were read.
- **It did not edit `docs/QC_FINDINGS.md`.** That is QC's file. The two findings this session closed are
  recorded here and in the register instead.
- **It did not renumber or restate any existing register line's content** — only added the cross-references
  that were missing.
- **It did not decide anything in §6.**
- **It did not judge severity or order.** The work order is the owner's and the overseer's; this document
  says only *who owns what*.
