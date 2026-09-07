# Falcon User's Guide — structured digest (FALCON EXPERT reference)

**Source document:** `falcon-users-guide-2025-05-09.pdf` · SpaceX · **128 PDF pages** · Version **8, March
2025** (change log, PDF p.11) · file dated **2025-05-09**.
**Status:** research reference. Created by `S222`, 2026-09-08, at owner direction. **C1.16: research is never
deleted** — a later chat may supersede a claim IN PLACE, never remove it.

🟢 **OWNER, 2026-09-08, verbatim:**
> *"We are using the most up to date versions of those engines. The users guide may be using old engine
> version statistics. Lets task a chat to systematically learn the manual front to back including reading it
> structured as it is intended to be not converted to pure text. This chat will be FALCON EXPERT and we will
> refer any falcon related questions to it."*

---

## 0. How to read this file, and how it was built

### 0.1 Page-number convention — READ THIS FIRST

The guide has **two page numbers on every page** and they differ by **11**:

| | |
|---|---|
| **PDF page** | the sheet index, 1…128. What `pypdfium2` indexes (`d[19]` = PDF p.20). |
| **printed page** | the number in the footer. Roman i–x for front matter, then arabic 1…117. |

**PDF page = printed page + 11** throughout the body (verified: PDF p.12 = printed 1; PDF p.20 = printed 9;
PDF p.96 = printed 85).

⛔ **Every citation below is written `PDF p.N / printed M`.** The owner's task brief cited PDF pages
("p.20", "p.96"), so PDF page leads. **A claim without a page cite does not appear in this file** — if you
find one, treat it as a defect.

### 0.2 Method — the pages were RENDERED and READ, not text-dumped

Per the owner's central instruction. Every page was rendered at scale 2 (1224 × 1584 RGB) and read visually:

```python
import pypdfium2 as p
d = p.PdfDocument(r'<path>/falcon-users-guide-2025-05-09.pdf')   # 128 pages
img = d[19].render(scale=2).to_pil()                             # PDF p.20 -> 1224 x 1584
```

Text extraction (`d[i].get_textpage().get_text_range()`) was run **as an independent cross-check only**.
Tables and figures where a number lives were additionally re-cropped at **scale 3** and re-read before being
written down (Table 2-1 propulsion rows, Table 5-3, §3.4/3.5, Tables 10-3/10-4, the change log).

**⭐ THE OWNER'S WARNING WAS CORRECT, AND HERE IS THE PROOF.**

1. **PDF pages 99–115 extract as COMPLETELY EMPTY** — nothing but the copyright footer. That is **17 pages**:
   the whole of Appendix A (PAF drawings), Appendix B (clampband drawings), Appendix C (constellation
   interfaces and keep-in volumes). They are landscape CAD drawings with vector text a flat dump does not
   see. **A text-only reading of this guide silently loses 17 pages and gives no sign that it did.**
   PDF p.116 (Appendix D, the cryo-propellant interface) is the same.
2. **The pre-supplied extraction of PDF p.96 omitted SECO-2 entirely** and collapsed two separate mission
   timelines into ranges. See §0.4 and §10.7 — this is a substantive loss, not cosmetic.

**Where render and text disagreed:** on the two pages the owner named — PDF p.20 and PDF p.96 — my own render
and my own text extraction **AGREE exactly**. Both numbers below are therefore confirmed twice, independently.
No render-vs-text conflict was found anywhere in the document. **The conflicts that do exist are inside the
guide itself** (§0.3) and between the guide and our craft (§0.3).

### 0.3 ⛔ THE VINTAGE CAVEAT — the owner raised it, and the evidence supports him

The owner's concern was that *"the users guide may be using old engine version statistics."* Three
independent pieces of evidence bear on it. **None of them is resolved here; all three are recorded.**

---

**EVIDENCE 1 — the change log does NOT list Section 2 among the March 2025 updates.**
*(PDF p.11 / printed x, read off the render.)*

| Version | Date | Update |
|---|---|---|
| 1 | October 2015 | Original Release |
| 2 | May 2016 | Updated Release |
| 3 | January 2019 | Updated Release |
| 4 | April 2020 | Updated Release |
| 5 | August 2020 | Updated Release |
| 6 | August 2021 | Updated Release |
| 7 | September 2021 | Updated Release |
| **8** | **March 2025** | **Updated Release. Major updates listed as follows:** §3.3 Launch Windows · §3.6 Multiple Payloads & Constellation · **§4** Interfaces · **§5** Environments · **§6** Payload Design Requirements · **§7** Verification · Appendices **A, B, C, D, E, F, G, H, I** |

**What is NOT in that list: Section 1, Section 2 (VEHICLES), §3.1/3.2/3.4/3.5, Section 8 (Facilities),
Section 9, Section 10 (OPERATIONS), Section 11.**

⛔ **State this precisely.** The change log says those sections were *not among the major updates* of March
2025. It does **not** prove they were untouched, and it does not date their content. But **Table 2-1 (the
vehicle specification, PDF p.20) and Tables 10-3/10-4 (the flight timelines, PDF p.96) both sit in sections
that the guide itself did not flag as revised in 2025** — the most recent release that could have revised
them without saying so is Version 7, **September 2021**. That is the documentary basis for the owner's
concern, and it is a real one.

---

**EVIDENCE 2 — ⭐ THE GUIDE CONTRADICTS ITSELF ON FIRST-STAGE THRUST, ONE PAGE APART.**

| Where | What it says |
|---|---|
| **§2.3, PDF p.19 / printed 8** | *"Nine SpaceX M1D engines power the Falcon 9 first stage with up to 845 kN (190,000 lbf) thrust per engine at sea level, for a total thrust of **7,605 kN (1,710,000 lbf)** at liftoff"* |
| **Table 2-1, PDF p.20 / printed 9** | Thrust (stage total), First Stage: **7,686 kN (sea level) (1,710,000 lbf)** |

**7,605 kN and 7,686 kN, both attributed to the same stage, on facing pages.** Both cite the same
1,710,000 lbf.

**Which one is self-consistent — arithmetic, shown so you can check it:**
- 1,710,000 lbf × 4.4482216152605 N/lbf = 7,606,459 N = **7,606 kN**. The body text's 7,605 kN matches.
  Table 2-1's 7,686 kN does **not** — it is ~80 kN high against the guide's own lbf figure.
- 9 engines × 845 kN/engine = 7,605 kN. Matches the body text.
- Cross-check that the conversion convention is otherwise sound: Falcon Heavy, §2.3 PDF p.18 / printed 7 —
  5,130,000 lbf × 4.4482216 = **22,819 kN**, and the guide says 22,819 kN. ✅ Consistent.
  Second stage: 220,500 lbf × 4.4482216 = **981 kN**, and Table 2-1 says 981 kN. ✅ Consistent.

**So Table 2-1's 7,686 kN is the single outlier in the document.** ⛔ I am **not** correcting it and **not**
declaring it a typo — §1.4 forbids an unverifiable reconciliation. Both numbers are recorded, both are cited,
and the arithmetic is shown. **Marked UNRESOLVED — see §14 Q1.**

⚠ **This changes the premise of the comparison in the task brief**, which used 7,686 kN. See EVIDENCE 3.

---

**EVIDENCE 3 — the guide vs our craft. RECORDED, NOT RECONCILED.**

| Source | First-stage sea-level thrust |
|---|---|
| Guide, Table 2-1, PDF p.20 / printed 9 | **7,686 kN** |
| Guide, §2.3 body text, PDF p.19 / printed 8 | **7,605 kN** |
| Our craft's octaweb, 2026-09-07 flight log | **8,227 kN** |

Difference vs Table 2-1: **+7.0 %**. Difference vs the guide's own self-consistent body figure: **+8.2 %**.

⛔ **This is NOT recorded as a defect in our craft.** The owner's reading — that the mod may model a later
uprate while the guide quotes a conservative or earlier published figure — is at least as likely as any
alternative, and EVIDENCE 1 gives it documentary support. ⛔ **Never silently reconcile these.**
**Marked UNRESOLVED — see §14 Q1.**

---

**WHAT THE GUIDE DOES AND DOES NOT SAY ABOUT BLOCK / VARIANT:**
- **It DOES say the vehicle is Block 5.** §2.1, PDF p.17 / printed 6: *"The current version of Falcon 9,
  Falcon 9 Full Thrust Block 5, first flew in the spring of 2018 … Engine performance on both stages was
  improved, releasing additional thrust capability."*
- ⛔ **Table 2-1 itself carries NO block or variant designation.** It says only "M1D" and "MVac". It does not
  state whether its thrust figure is the Block 5 figure, a pre-Block-5 figure, or a conservative published
  value. **The page does not say.** Do not infer one.
- The engine names are not used consistently: **Table 2-1 (PDF p.20) says "MVac"**; the launch-control
  console chart **Figure 10-8 (PDF p.92 / printed 81) says "MVacD"**. The guide never defines the D suffix.

### 0.4 Discrepancy register (everything unresolved, in one place)

| # | Subject | The conflict | Status |
|---|---|---|---|
| **D1** | S1 sea-level thrust | Guide internal: **7,605 kN** (§2.3, p.19) vs **7,686 kN** (Table 2-1, p.20). 7,605 is arithmetically self-consistent. | **UNRESOLVED** — §14 Q1 |
| **D2** | S1 sea-level thrust | Guide (7,605 / 7,686 kN) vs our octaweb **8,227 kN** (+8.2 % / +7.0 %) | **UNRESOLVED** — §14 Q1 |
| **D3** | Vintage of Table 2-1 and Tables 10-3/10-4 | Sections 2 and 10 are **not listed** among the March 2025 major updates (change log, p.11) | **RECORDED** — bounds, does not date |
| **D4** | Engine designation | "MVac" (Table 2-1, p.20) vs "MVacD" (Figure 10-8, p.92) | **RECORDED** — guide never defines the suffix |
| **D5** | Pre-supplied p.96 extraction | Omitted **SECO-2** (T+1696 GTO / T+3090 LEO) and merged two tables into ranges | **CORRECTED here** — §10.7 |
| **D6** | MECO time vs §B11 | Guide LEO **T+145 s** / GTO **T+147 s**; §B11 has **~T+2:17 = 137 s** | **RECORDED** — §13.2, both mission-specific |

### 0.5 ⛔ Repo-hygiene flag (C7)

**The PDF is NOT in the repo.** It was read from `C:\Users\User\Downloads\falcon-users-guide-2025-05-09.pdf`
— **outside `DragonScreen`**, which C7 names as the only source of truth. Per the task brief this is `S221`'s
job and this task did **not** copy it. Until `S221` lands it in `docs/reference/`, **this digest is the only
in-repo record of the guide's contents**, and a later chat cannot re-verify a page cite without the file.
Flagged, not fixed. See §14 Q3.

---

## 1. Introduction *(PDF pp.12–16 / printed 1–5)*

- **Applicability**, §1.1, PDF p.12 / printed 1: the guide covers *"Falcon vehicle configurations with a
  5.2 m (17-ft) diameter fairing."* Missions using a **Dragon spacecraft** are pointed to SpaceX directly —
  see §2.1 below for the one sentence that does bridge the two.
- §1.3 Falcon Program Overview, PDF p.14 / printed 3, Figure 1-3 "Falcon 9 Architecture" callouts: **4
  landing legs**; **composite interstage**; **second stage with in-space start capacity**; **9 Merlin engines
  with engine-out reliability**; **1,710,000 lbf thrust at sea level**; **4 grid fins**.
- §1.4 Falcon Launch Vehicle Safety and §1.5 Falcon Reliability, PDF pp.15–16 / printed 4–5.
- §1.5 **Pricing**, PDF p.16 / printed 5.
- Front matter: contents PDF pp.2–3 · **acronym list PDF pp.4–6 / printed iii–v** · list of figures
  PDF p.7 · list of tables PDF pp.8–10 · **change log PDF p.11 / printed x** (see §0.3).

---

## 2. Vehicles *(PDF pp.17–21 / printed 6–10)* — ⭐ THE SPECIFICATION SECTION

### 2.1 Falcon 9 overview *(PDF p.17 / printed 6)*

- **Two-stage, LOX / RP-1, partially reusable.**
- **Block designation, verbatim:** *"The current version of Falcon 9, Falcon 9 Full Thrust Block 5, first
  flew in the spring of 2018 … Engine performance on both stages was improved, releasing additional thrust
  capability."*
- ⭐ **Directly relevant to this project, verbatim:** *"Falcon 9 can be flown with a fairing or with a SpaceX
  Dragon spacecraft. **All first and second stage vehicle systems are the same in the two configurations;
  only the payload interface to the second stage changes.**"*
  → **The guide's Section 2 vehicle numbers apply to the Dragon configuration by the guide's own statement.**
  ⛔ But §3 performance data (mass to orbit, injection orbits) is fairing-configuration; the guide directs
  Dragon launch capability questions to SpaceX.
- Figure 2-1, same page: Falcon 9 overview illustration.

### 2.2 Falcon Heavy overview *(PDF pp.18–19 / printed 7–8)*

- Three Falcon 9 first-stage cores; **27 first-stage engines**.
- ⭐ **PDF p.18 / printed 7, verbatim: *"each of the 27 first stage engines produces 845 kN (190,000 lbf) of
  thrust at sea level"*, total *"22,819 kN (5,130,000 lbf)"*.** This is the sentence that pins the
  **per-engine** figure at **845 kN / 190,000 lbf**, and it is what makes Table 2-1's throttle row readable
  (§2.5).
- Figure 2-2: a Falcon Heavy launch from KSC, **April 11, 2019**. Figure 2-3, PDF p.19 / printed 8: **Falcon
  Heavy first stage engine layout**.

### 2.3 Structure and propulsion *(PDF pp.19–20 / printed 8–9)*

**First stage:**
- ⭐ *"Nine SpaceX M1D engines power the Falcon 9 first stage with up to **845 kN (190,000 lbf) thrust per
  engine at sea level**, for a total thrust of **7,605 kN (1,710,000 lbf)** at liftoff"* — see §0.3
  EVIDENCE 2 for the conflict with Table 2-1.
- **Engine cycle:** liquid, **gas generator**; **pintle injector**; **milled copper alloy regeneratively
  cooled** thrust chamber liner; **single-shaft turbopump**.
- **Layout:** *"configured in a circular pattern, with eight engines surrounding a center engine"* (the
  octaweb). Figure 2-3's numbering, read off the render: **8 and 1 top, 7 and 2, 9 at centre, 6 and 3, 5 and
  4** — i.e. **engine 9 is the centre engine**.
- ⭐ **Hold-down, verbatim:** *"After engine start, Falcon vehicles are **held down until all vehicle systems
  are verified as functioning normally before release for liftoff**."* → see §10.5.5 for the full chain, and
  §13.1 for what it means for `S219`.
- **Tanks:** aluminium-lithium skin, aluminium domes; **friction stir welded**; a **common dome** separates
  LOX and RP-1; a **double-wall transfer tube** carries LOX through the centre of the RP-1 tank.
- **Recovery hardware:** **four grid fins** near the top of the first stage; **four deployable landing legs**
  at the base, *"nominally flown to support recovery operations."*

**Second stage:**
- **A single Merlin Vacuum (MVac)** with a **fixed 165:1 expansion nozzle**.
- ⭐ *"For added reliability of restart, the engine contains **dual redundant triethylaluminum-triethylborane
  (TEA-TEB) pyrophoric igniters**."*
- ⭐ **Cold nitrogen gas (GN2) attitude control system** for pointing and roll: *"more reliable and produces
  less contamination than a propellant-based reaction control system."*
- The second-stage tank is a shorter version of the first-stage tank, same materials, tooling and technique.
- The stage separation system sits at the **forward end of the interstage**.

### 2.4 Retention, release, and separation systems *(PDF p.20 / printed 9)*

- **Stage separation:** the stages are mated by **mechanical latches at three points** between the top of the
  interstage and the base of the second-stage fuel tank. **After the first stage engines shut down**, a
  **high-pressure helium circuit releases the latches via redundant actuators**. The same helium system
  preloads **four pneumatic pushers**, **plus a redundant centre pusher** to further reduce re-contact
  probability.
- **Fairing:** two halves fastened by **mechanical latches along the vertical seam**; a **high-pressure helium
  circuit releases the latches** and **four pneumatic pushers** deploy the halves.
- **All-pneumatic separation** — chosen for a benign shock environment, testability of the actual flight
  hardware, and minimal debris.

### 2.5 ⭐ TABLE 2-1: FALCON 9 SPECIFICATION *(PDF p.20 / printed 9)*

**Read off the rendered page, then re-read at scale 3, then cross-checked against text extraction — all
three agree.** This is the table the owner flagged as coming out as a run-on. Its column structure:

| Characteristic | **First Stage** | **Second Stage** |
|---|---|---|
| **STRUCTURE** | | |
| Height | **70 m (229.6 ft)** including both stages, interstage, and standard fairing; **75.2 m (246.7 ft)** with extended fairing | *(single spanning cell)* |
| Diameter | **3.66 m (12 ft)** | **3.66 m (12 ft)** |
| Type | LOX tank – monocoque; Fuel tank – skin and stringer | LOX tank – monocoque; Fuel tanks – skin and stringer |
| Material | Aluminum lithium skin; aluminum domes | *(single spanning cell)* |
| **PROPULSION** | | |
| Engine type | Liquid, gas generator | Liquid, gas generator |
| **Engine designation** | **M1D** | **MVac** |
| Engine designer | SpaceX | SpaceX |
| Engine manufacturer | SpaceX | SpaceX |
| **Number of engines** | **9** | **1** |
| Propellant | Liquid oxygen / kerosene (RP-1) | Liquid oxygen / kerosene (RP-1) |
| **Thrust (stage total)** | **7,686 kN (sea level) (1,710,000 lbf)** ⚠ see §0.4 D1 | **981 kN (Vacuum) (220,500 lbf)** |
| Propellant feed system | Turbopump | Turbopump |
| **Throttle capability** | **Yes (190,000 lbf to 108,300 lbf sea level)** | **Yes (220,500 lbf to 140,679 lbf)** |
| **Restart capability** | **Yes** | **Yes** |
| Tank pressurization | **Heated helium** | **Heated helium** |
| **ASCENT ATTITUDE CONTROL** | | |
| Pitch, yaw | **Gimbaled engines** | **Gimbaled engine / nitrogen gas thrusters** |
| Roll | **Gimbaled engines** | **Nitrogen gas thrusters** |
| **Coast attitude control** | **Nitrogen gas thrusters (recovery only)** | **Nitrogen gas thrusters** |
| **OPERATIONS** | | |
| Shutdown process | Commanded shutdown | Commanded shutdown |
| Stage separation system | Pneumatically actuated separation mechanism | N/A |

#### ⭐ READING THE THROTTLE ROW — the one place this table needs interpretation

**What the table literally says** for the first stage: *"Yes (190,000 lbf to 108,300 lbf sea level)"*.
**The table does NOT label this per-engine or per-stage.** But:

- 1,710,000 lbf ÷ 9 = **190,000 lbf exactly** — the upper bound of the throttle row.
- §2.2 (PDF p.18) independently states **845 kN (190,000 lbf) per engine at sea level**.

→ **The first-stage throttle row is therefore PER ENGINE**, and the implied floor is
**108,300 / 190,000 = 57.0 %**. *(Marked as an inference drawn from two cited pages, not as a quoted figure.
The guide does not print "57 %" anywhere.)*

- **Second stage:** 220,500 lbf is the whole stage (one engine), so **140,679 / 220,500 = 63.8 %** throttle
  floor. Again, the percentage is computed here; the guide prints only the two lbf figures.

⚠ **Do not read either floor as a deep-throttle limit for a landing burn.** The guide gives one throttle row
per stage and does not discuss landing-burn throttling anywhere.

### 2.6 Avionics and GNC *(PDF p.21 / printed 10)*

Section 2.5 of the guide. Covered on PDF p.21; consult the page directly if a question turns on the
flight-computer redundancy description.

---

## 3. Performance *(PDF pp.22–26 / printed 11–15)*

### 3.1 Available injection orbits *(PDF p.22 / printed 11)*
- ⭐ **Two launch-service profiles:** a **two-burn** profile *optimizes vehicle performance*; a
  **direct-inject** profile *reduces mission duration and requires only a single start of the second stage
  engine*. → This is the guide's own framing of the second-stage burn plan, and it is why Tables 10-3/10-4
  both show SES-2/SECO-2.
- Figure 3-1, PDF p.22: ground tracks / orbits of previous Falcon 9 and Falcon Heavy launches.
- Launch azimuths available by pad: **SLC-40** supports low-to-mid-inclination LEO, high-inclination LEO
  including polar, SSO, GTO, and Earth-escape (PDF p.69). **LC-39A** supports low-to-mid-inclination LEO,
  high-inclination LEO, GTO, and Earth-escape (PDF p.70). **SLC-4E** supports high-inclination LEO including
  polar and SSO (PDF p.73).

### 3.2 Mass-to-orbit capability *(PDF p.23 / printed 12)*
⛔ **THE GUIDE DOES NOT PUBLISH PAYLOAD MASS TO ORBIT.** §3.2 states the data is available upon request.
If asked "what can a Falcon 9 lift to X", the correct answer from this document is **"the guide does not
state this."**

### 3.3 Launch windows *(PDF pp.23–24 / printed 12–13)* — *revised in v8, March 2025*
- **Table 3-2** (PDF p.24 / printed 13) gives **historical weather-scrub probability by month × hour (UTC)**
  for both coasts. Read off the render:
  - **Cape (Eastern Range): overall average 8.0 %.** Monthly averages: Jan 7.2 · Feb 7.0 · Mar 5.7 ·
    Apr 6.1 · May 6.8 · **Jun 13.0** · **Jul 12.4** · Aug 10.9 · Sep 10.9 · Oct 5.7 · **Nov 4.8** · Dec 5.9.
  - **SLC-4 (Western Range): overall average 5.3 %.** Monthly averages: Jan 7.6 · **Feb 8.8** · Mar 5.2 ·
    Apr 3.8 · May 1.8 · **Jun 0.6** · Jul 1.1 · Aug 0.7 · Sep 1.0 · Oct 1.8 · Nov 3.6 · Dec 6.7.
  - The table is 24 hourly columns × 12 monthly rows per site; per-hour cells are on the page if needed.

### 3.4 Flight attitude *(PDF p.24 / printed 13)*
⭐ **Verbatim:** *"If requested, the Falcon second stage will point the X-axis of the launch vehicle to a
customer-specified attitude and perform a **passive thermal control roll of up to ± 1.5 deg/sec** around the
launch vehicle X-axis, held to a **local vertical/local horizontal (LVLH) roll attitude accuracy of ± 5
deg**."*

### 3.5 Separation attitude and accuracy *(PDF pp.24–25 / printed 13–14)*
- **3-axis attitude control OR spin-stabilized separation**, both standard service.
- **Inertial separation:** point the second stage and payload to the desired LVLH attitude and **minimize
  attitude rates**.
- **Spin-stabilized:** point to the desired LVLH attitude, then **spin about the launch vehicle X-axis at a
  customer-specified rate dependent on payload mass properties**.
- ⭐ **Verbatim, and worth knowing:** *"SpaceX does not implement multiple trajectories for various
  dates/times and **does not provide sun-referenced or inertially referenced attitudes during ascent or for
  payload separation**."*
- ⛔ **THE GUIDE DOES NOT PUBLISH SEPARATION ATTITUDE OR RATE ACCURACY NUMBERS**: *"Standard pre-separation
  attitude and rate accuracies are developed as a mission-specific standard service. More information …
  is available from SpaceX upon request."*

### 3.6 Multiple payloads and constellations *(PDF pp.25–26 / printed 14–15)* — *revised in v8*
- Multiple satellites per mission; **restart capability lets each satellite go to a different orbit,
  performance allowing**. Smallsat Rideshare Program referenced.
- §3.6.1 Secondary payloads: SpaceX reserves the right to manifest secondaries on a **non-interference
  basis**.
- Figure 3-2 (PDF p.25): example of CG shift between two payloads deployed at different orbits.

---

## 4. Interfaces *(PDF pp.26–41 / printed 15–30)* — *revised in v8, March 2025*

### 4.1.2 ⭐ Launch vehicle coordinate frame *(PDF p.26 / printed 15, Figure 4-1)*
- **Right-handed X–Y–Z.**
- ⭐ **Origin: 440.69 cm (173.5 in) aft of the first-stage radial engine gimbal.**
- **+X_LV** along the vehicle long axis (out the nose). **+Z_LV** opposite the transporter-erector
  strongback.
- **X = roll, Y = pitch, Z = yaw.**
- Figure 4-2 (same page): side-mounted payload coordinate frame.

### 4.1.3 Fairing *(PDF p.27 / printed 16, Figure 4-3)*
**Standard and extended fairing**, both **5.2 m (17 ft)** outer diameter (§1.1). The vehicle heights that go
with them are in Table 2-1: **70 m** standard, **75.2 m** extended.

### 4.1.5 / 4.1.7 Payload attach fittings and separation systems *(PDF pp.29–31 / printed 18–20)*
- PAFs illustrated: **1,575-mm** (Figure 4-4) · **2,624-mm** (Figure 4-5) · **3,117-mm** (Figure 4-6) ·
  **3,117-mm strut PAF** (Figure 4-7) · **square PAF** (Figure 4-8).
- Standard-service clampband adapters (§9.3, PDF p.82 / printed 71): **937 mm, 1,194 mm or 1,666 mm
  (36.89 in / 47.01 in / 65.59 in)** with a low-shock clampband separation system.
- Standard-service bolted interfaces (§9.3): **1,575 mm** (compatible with the 62.01-in EELV medium-class
  interface) or **2,624 mm**.
- ⭐ **A fuller list of interface diameters appears in Appendix F** (PDF p.118 / printed 107) as the
  boundary-grid table: **937 · 1194 · 1575 · 1666 · 2624 · 2795 · 3117 mm**. The 2,795 mm and 3,117 mm
  entries do not appear in the §9.3 standard-service list.

### 4.1.6 Constellation arrangements *(PDF pp.31–32 / printed 20–21)*
Figures 4-9 (cube and octagon configurations), 4-10 (cube keep-in volumes), 4-11 (octagon keep-in volumes).
Dimensioned drawings are in **Appendix C**.

### 4.2 Mass – CG limitations *(PDF pp.33–35 / printed 22–24)*
- Figure 4-12: allowable mass and CG height above the separation interface plane (SIP) for the **1,575-mm,
  2,624-mm and square PAFs** — the curves are drawn for **1.5 g lateral** and **2.0 g lateral** design cases.
- Figure 4-13: same for the **3,117-mm strut PAF**. Figure 4-14: constellation payloads. Figure 4-15:
  4-point interfaces.

### 4.3 Payload fluid interfaces *(PDF p.35 / printed 24)* · 4.4 Electrical *(PDF pp.36–41 / printed 25–30)*
- Electrical: **up to two sets of 37- or 61-pin** satellite-to-launch-vehicle in-flight disconnect connectors
  as a standard service (§9.3, PDF p.82).
- §4.4.5 covers pad ground cabling (referenced from §10.5.4).
- §4.5 Interface compatibility verification requirements, PDF p.41 / printed 30.

---

## 5. Environments *(PDF pp.42–53 / printed 31–42)* — ⭐ LOADS AND ENVIRONMENTS · *revised in v8*

### 5.1 Transportation *(PDF p.42)* · 5.2 Temperature, humidity, cleanliness *(PDF p.42)*
Table 5-1 (transportation environments) and **Table 5-2 (PPF facility HVAC)** — Table 5-2 is the reference
the PPF table in §10.3 points to for temperature and cleanliness.

### 5.3 Flight environments *(PDF pp.43–53 / printed 32–42)*

**§5.3.2 Quasi-static / steady-state loads — ⭐ THE ACCELERATION LIMITS**
*(Figure 5-1 and **Table 5-3**, PDF p.44 / printed 33 — re-read at scale 3.)*

⭐ **Verbatim and directly relevant to ascent guidance:** *"Both the first and second stage engines may be
throttled to help maintain launch vehicle and payload steady state acceleration limits."*

**Table 5-3: Falcon 9 and Falcon Heavy Flight Limit Load Factors** — the envelope, by payload mass class:

| Payload mass | Max axial [g] | at lateral [g] | Further envelope corners (axial, lateral) |
|---|---|---|---|
| **> 1,800 kg (F9 / FH)** | **6.0** | 0.5 | (4.0, 0.5) · (3.5, 2.0) · (−1.5, 2.0) · (−2.0, 0.5) |
| **1,000 – 1,800 kg (F9)** | **8.5** | 2.0 | (4.0, 2.0) · (4.0, 3.0) · (−1.5, 3.0) · (−4.0, 2.0) |
| **< 1,000 kg (F9)** | **11.0** | 4.0 | (5.0, 4.0) · (5.0, 7.5) · (−2.0, 7.5) |

⚠ These are **payload interface limit loads**, not a stated vehicle acceleration limit — but combined with
the throttling sentence above they are what the ascent profile is throttled to respect. **For any payload
over 1,800 kg the axial limit is 6.0 g.** See §13.2 for how this bears on §B11's `~4 g [EST]` row.

**§5.3.3 Sine vibration** *(PDF p.45)* · **§5.3.4 Acoustic** *(PDF pp.45–46 / printed 34–35)*
- Acoustic maximum predicted environment **with blankets**, overall sound pressure level (OASPL):
  **131.4 dB West Coast / 131.3 dB East Coast** (1/3-octave), **131.6 / 131.4 dB** (full octave).
  Per-band levels are in the table on PDF p.46.

**§5.3.5 Shock — ⭐ THE FLIGHT SHOCK EVENT LIST** *(PDF p.49 / printed 38)*
The guide names **five shock events during flight**, in order:
1. ⭐ **Release of the hold-down at liftoff**
2. Side booster separation *(Falcon Heavy only)*
3. **Stage separation**
4. **Fairing separation**
5. **Spacecraft separation**

**§5.3.6 Random vibration** *(PDF p.50)* · **§5.3.7 Electromagnetic** *(PDF pp.50–52)*

**§5.3.8 Ventable volumes / fairing pressure decay** *(PDF p.52 / printed 41)*
- ⭐ Fairing internal pressure decay: **≤ 2.8 kPa/s (0.40 psi/s)**, briefly **≤ 4.5 kPa/s (0.65 psi/s)** for
  **≤ 5 s**. This is an ascent-profile-derived number — it is the venting rate the atmosphere-relative climb
  produces.

**§5.3.9 Thermal** · **§5.3.10 ⭐ Free molecular heating and fairing deployment**
*(PDF p.53 / printed 42, Figure 5-9)*
- ⭐ **Nominal fairing deployment is at free molecular heating flux below 1,135 W/m².**
- ⭐ **Figure 5-9 gives the maximum fairing-deploy time: ≈ 230 s for Falcon 9** (≈ 290 s for Falcon Heavy).
- **Cross-check against the flight timelines (§10.7):** fairing separation at **T+195 s (LEO)** and
  **T+222 s (GTO)** — both inside the ≈230 s bound. ✅ The two sections are consistent.

---

## 6. Payload design requirements *(PDF pp.54–58 / printed 43–47)* — *revised in v8*

§6.1 Design factors · §6.2 Fasteners and cable ties · §6.3 Isolators · §6.4 Pressure vessels and systems ·
§6.5 Solid propulsion systems. Payload-side requirements; nothing here bears on vehicle performance or
guidance. §6.4 carries the standards and test factors that §7.5 verification points back to.

---

## 7. Verification *(PDF pp.59–68 / printed 48–57)* — *revised in v8*

- §7.1 Verification test approach *(PDF p.59)* · §7.2 **Payload test levels, Table 7-1** *(PDF pp.60–62)* —
  the levels and durations every §7.4 subsection refers back to.
- §7.3 Constellation testing / lot acceptance, incl. **Table 7-2 retest triggers** *(PDF pp.63–64)*.
- §7.4 Environmental verification requirements *(PDF pp.64–67 / printed 53–56)*: 7.4.1 quasi-static loading ·
  7.4.2 sine vibration (modes < 100 Hz) · 7.4.3 shock · 7.4.4 acoustic · 7.4.5 random vibration
  (+ 7.4.5.1 component-by-component, 7.4.5.2 band splitting — **workmanship floor 0.004 g²/Hz, 20–2,000 Hz**)
  · **7.4.6 activation inhibits** · 7.4.7 electromagnetic (MIL-STD-461) · 7.4.8 ventable volumes ·
  7.4.9 thermal.
- §7.5 Verification for pressure vessels and systems *(PDF p.68 / printed 57)*, incl. HTP-specific
  requirements (60-day decay analysis, **32 °C max storage temp** assumption).

---

## 8. Facilities *(PDF pp.69–80 / printed 58–69)* — ⭐ INCLUDES DOCUMENTED PAD COORDINATES

### ⭐ 8.1–8.2 Launch pad coordinates — TIER-1 DOCUMENTED, verbatim from the guide

| Pad | Latitude | Longitude | Page | Azimuths supported |
|---|---|---|---|---|
| **SLC-40**, CCSFS, Florida | **28° 33.72′ (28.5620°) N** | **80° 34.630′ (80.5772°) W** | PDF p.69 / printed 58 | low-to-mid LEO, high-inclination incl. polar, SSO, GTO, Earth-escape |
| **LC-39A**, KSC, Florida | **28.6082° N** | **80.6041° W** | PDF p.70 / printed 59 | low-to-mid LEO, high-inclination LEO, GTO, Earth-escape |
| **SLC-4E**, VSFB, California | **34° 37.92′ (34.6320°) N** | **120° 36.64′ (120.6107°) W** | PDF p.73 / printed 62 | high-inclination LEO incl. polar, SSO |

⭐ **These are documented coordinates from a primary source.** Given the `LZ1` incident recorded in
`CLAUDE.md` C1.12 — where two tier-3 coordinates were invented and later unwound by `S89` — this table is
worth knowing exists before anyone estimates a pad position again.

⛔ **The guide gives NO coordinate for any landing zone.** **Landing Zone SLC-4W** is *labelled on the
Vandenberg site map* (Figure 8-5, PDF p.73 / printed 62) adjacent to SLC-4E, and **Figure 8-4** (PDF p.72)
labels the Vandenberg "Launch and Landing Center", but **no latitude/longitude is printed for either, and
LZ-1 / LZ-2 at the Cape are not mentioned anywhere in this document.** If asked for LZ coordinates, the
answer from this guide is **"the guide does not state this."**

### 8.1 East coast *(PDF pp.69–72 / printed 58–61)*
- **SLC-40**: vehicle integration hangar, hangar annex, launch pad, pad customer room, propellant and
  pressurant storage, lightning towers (Figure 8-1). Previously used by the U.S. Air Force for Titan III and
  Titan IV.
- **LC-39A**: NASA-built early 1960s, Apollo and Shuttle; **20-year SpaceX lease signed April 2014**;
  upgraded 2016; **first SpaceX launch 19 February 2017 (CRS-10)**; *"more than 100 Falcon 9 and Falcon Heavy
  missions"* since. Hangar **8 miles from the main KSC gate**, **55,000 sq ft floor / 34,000 sq ft high bay**,
  **90-ton, 50-ton and 30-ton bridge cranes**.
- ⭐ **Transport incline:** *"The maximum incline that the integrated launch vehicle experiences during
  transportation from the hangar to the pad is **2.9 degrees** and occurs as it is moved up the ramp."*
- §8.1.3 personnel: access/badging, transportation/lodging, available facilities. **SpaceX Launch and Landing
  Control is at Hangar X.**

### 8.2 Vandenberg *(PDF pp.72–78 / printed 61–67)*
- VSFB / Space Launch Delta 30, the Western Range. **SLC-4** carries SpaceX Runway Operations, the Customer
  Support Building and the Pad EGSE Room; **SLC-6** carries Falcon maintenance, fairing maintenance and the
  PPF. **Launch and Landing Control is on North Base, ~7 miles from SLC-4E.**
- **Building 398** (SLC-6): built mid-1980s for Shuttle SRB assembly, later Atlas/Delta SRBs, now Falcon 9 and
  fairing maintenance (west bay) and payload processing (east bay); **3.5 miles south-southwest of SLC-4**.
  **No solid or hypergolic fuels** at B398 (permitting).
- Pad customer room: Launch Support Building **715**, a reinforced concrete bunker adjacent to the SLC-4E
  launch mount.

### 8.3 Hawthorne *(PDF p.79 / printed 68)* · 8.4 McGregor *(PDF p.79)* · 8.5 Washington DC *(PDF p.80)*
- Hawthorne, CA: Dragon, Falcon 9, Falcon Heavy and Merlin engine production; **nine stations for final
  assembly of the Merlin engine**.
- ⭐ **McGregor, Texas:** *"every Falcon 9 and Falcon Heavy first and second stage and every Merlin engine
  undergoes acceptance testing before first flight."* Merlin refurbishment also happens at the three launch
  sites and at McGregor.

---

## 9. Mission integration and services *(PDF pp.81–85 / printed 70–74)*

### 9.4 Table 9-1: Standard launch integration schedule *(PDF p.83 / printed 72)*

| Estimated schedule | Title |
|---|---|
| **L−24 months** | Contract signature |
| **L−18 months** | Mission integration kickoff |
| **L−9 months** | Completion of mission integration analyses |
| **L−2 months** | Launch campaign readiness review |
| **L−1 day** | Launch readiness review |
| **Separation + TBD minutes** | Orbit injection report |
| **Launch + 8 weeks** | Flight report |

### 9.3 Standard services worth knowing *(PDF pp.82–83 / printed 71–72)*
- Adapters and separation systems (see §4 above).
- **48 cumulative hours** of remote on-call support for payload sine-vibration testing.
- **ISO Class 8 (Class 100,000)** cleanroom integration space.
- **3-axis attitude control or spin-stabilized spacecraft separation.**
- **A collision avoidance manoeuvre (as required).**
- Orbit injection report; final post-flight report.

### 9.5 Customer deliverables *(PDF pp.83–85 / printed 72–74)*
Tables 9-2 / 9-3. Also: registration of deployed objects with the **18th SPCS**; ephemeris publication to
Space-Track and SpaceX Space Traffic Coordination, with an **L+3 hours benchmark** at which the launch COLA
analysis expires; **NASA ISS conjunction deconfliction** contact for any orbit crossing ISS altitude.

---

## 10. Operations *(PDF pp.86–96 / printed 75–85)* — ⭐ COUNTDOWN, FLIGHT, AND THE TIMELINES

### 10.1 Overview and schedule *(PDF p.86 / printed 75, Figure 10-1)*
- Spacecraft standalone operations typically **15 days**, complete by **L−7 days**; adapter mate and fairing
  encapsulation at the PPF; transport to the integration hangar; **encapsulated assembly mated to the launch
  vehicle at approximately L−2 days**, then end-to-end system checkouts.
- ⭐ *"Falcon 9 and Falcon Heavy systems are designed for **rollout and launch on the same day**, but SpaceX
  can perform an earlier rollout and conduct a longer countdown if required."*
- Figure 10-1 campaign flow spans **L−22 to L**, with **Booster Refurbishment** as a long bar ending around
  L−7.

### 10.2–10.4 Delivery, processing, joint operations *(PDF pp.86–91 / printed 75–80)*
- Payloads delivered to the launch site **four weeks prior to launch** (standard service).
- PPF available from **four weeks** before launch, **16 hours/day** standard access.
- **Table 10-1** (PDF pp.89–90 / printed 78–79) gives the PPF service specifications — cleanroom dimensions,
  crane capacities and hook heights, electrical, **GN2 supply (28,613 kPa / 4,150 psi at CCSFS; 34,473 kPa /
  5,000 psi at VSFB, both 1,699.2 Nm³/hr / 1,000 scfm)**, **helium supply (39,300 kPa / 5,700 psi CCSFS;
  41,368 kPa / 6,000 psi VSFB)**, compressed air, comms, security.
- ⭐ **All spacecraft processing must be complete by L−7 days.** **Joint operations begin seven days before
  launch.** Fairing encapsulation and transport are performed **vertically**; at the hangar the encapsulated
  assembly is **rotated to horizontal** and mated to the launch vehicle on the transporter-erector.

### 10.5 Launch operations *(PDF pp.91–94 / printed 80–83)*

**Table 10-2: Launch control organization** *(PDF p.92 / printed 81)*

| Position | Abbrev. | Organization |
|---|---|---|
| Chief Engineer | CE | SpaceX |
| Mission Manager | MM | SpaceX |
| Launch Director | LD | SpaceX |
| Range Operation Commander | ROC | Launch Range |
| Operations Safety Manager | OSM | Launch Range |

⭐ **Figure 10-8 (same page) shows the full console tree** under the Launch Director: **Falcon Recovery** ·
**Pilot → Co-Pilot** · **Chief Engineer** → Avionics/Flight Software, **GNC**, Stage 1 → **M1D**, Ground
Stations, Stage 2 → **MVacD** · Mission Manager → Customer Launch Director · Operations Safety Manager,
Range Operation Commander, **Launch Weather Officer**.
*(This is where the "MVacD" designation appears — see §0.4 D4.)*

**§10.5.4 Rollout, erection, pad operations** *(PDF p.93 / printed 82)*
Rolled out on the transporter-erector; payload air conditioning reconnected at the pad and maintained
**through liftoff**; electrical connectivity via ground cables. *"The vehicle will typically be erected only
once, although the capability exists to easily return it to a horizontal orientation if necessary."*
**No payload access while the vehicle is vertical.**

### ⭐⭐ 10.5.5 COUNTDOWN — THE IGNITION AND HOLD-DOWN CHAIN *(PDF p.94 / printed 83)*

**Quoted in full, because this is the paragraph that matters most to this project:**

> *"Early in the countdown, the vehicle performs LOX, RP-1, and pressurant loading, and it executes a series
> of vehicle and Range checkouts. The transporter-erector strongback is retracted just prior to launch.
> **Automated software sequencers control all critical Falcon vehicle functions during terminal countdown.**
> Final launch activities include verifying flight termination system status, transferring to internal power,
> and activating the transmitters. **Engine ignition occurs shortly before liftoff, while the vehicle is held
> down at the base via hydraulic clamps. The flight computer evaluates engine ignition and full-power
> performance during the prelaunch hold-down, and if nominal criteria are satisfied, the hydraulic release
> system is activated at T-0. A safe shutdown is executed should any off-nominal condition be detected.**"*

**The chain, stated plainly:**
1. Terminal countdown is run by **automated software sequencers**, not by an operator.
2. **Ignition happens BEFORE T-0**, with the vehicle **held down by hydraulic clamps**.
3. The **flight computer** — not the ground — **evaluates ignition and full-power performance during the
   hold-down**.
4. **Only if nominal criteria are satisfied is the hydraulic release activated, at T-0.**
5. **Any off-nominal condition → safe shutdown**, on the pad, still clamped.

**Corroborating cites elsewhere in the guide:** §2.3, PDF p.19 — *"held down until all vehicle systems are
verified as functioning normally before release for liftoff"*; §5.3.5, PDF p.49 — flight shock event **#1 is
"release of the hold-down at liftoff"**; and **Tables 10-3/10-4 put engine start at T−3 s** (§10.7).

**§10.5.6 Recycle and scrub** *(PDF p.94)* — on a scrub the TE and vehicle **typically stay vertical**,
preserving payload-to-EGSE connectivity through the T-0 umbilical; long postponements return the vehicle to
the hangar.

### 10.6 Flight operations *(PDF p.94 / printed 83)*

- **§10.6.1 Liftoff and ascent, verbatim:** *"During first stage powered flight, Falcon's flight computers
  will command **shutdown of the nine first stage engines based on achieving the target velocity or on
  remaining propellant levels**. The second stage burns an **additional five to six minutes to reach initial
  orbit**, with **deployment of the fairing typically taking place early in second stage flight**. Subsequent
  operations are unique to each mission but may include **multiple coast-and-restart phases** as well as
  multiple spacecraft separation events."*
  → **MECO is a velocity-or-propellant cutoff, not a fixed clock.** That is why Tables 10-3/10-4 are labelled
  "sample".
- **§10.6.2 Spacecraft separation:** the vehicle issues **redundant separation commands**; separation
  indication typically via second-stage telemetry.
- **§10.6.3 CCAM:** the second stage can perform a contamination and collision avoidance manoeuvre after
  payload deploy; standard service for individual primary payloads.
- **§10.6.4 Post-launch reports:** quick-look orbit injection report shortly after separation (best-estimate
  separation state vector); detailed post-flight report **within eight weeks**.
- **§10.6.5 Disposal:** passivation and responsible disposal; customer-specific disposal requirements *"may
  impose modest reductions to the performance specifications."*

### ⭐ 10.7 SAMPLE MISSION PROFILE *(PDF pp.95–96 / printed 84–85)*

**Figure 10-11: Falcon 9 sample mission profile** *(PDF p.95 / printed 84)* — the labelled event sequence,
read off the render. This is the **booster recovery sequence** (§B16's subject):

> **LAUNCH → ASCENT → STAGE SEPARATION** *("First stage has left Earth's atmosphere")* **→ FLIP MANEUVER**
> *("Cold gas thrusters flip first stage")* **→ GRID FINS DEPLOY → ENTRY BURN** *("Engines light again to
> slow down first stage")* **→ AERODYNAMIC GUIDANCE** *("Grid fins steer lift produced by first stage")*
> **→ VERTICAL LANDING** *("Engines light one final time bringing first stage to soft landing")*
> **→ AUTONOMOUS DRONESHIP LANDING**
>
> *(on the upper trajectory, in parallel:* **FAIRING SEPARATION → PAYLOAD SEPARATION** *)*

⭐ **Note what is NOT in the Falcon 9 profile: there is no BOOSTBACK BURN.** The F9 figure shows a droneship
recovery with entry burn + landing burn only. **Figure 10-12 (Falcon Heavy, same page)** *does* show
**BOOSTBACK BURN** — *"Engines light to reverse velocity of stage"* — for the side boosters returning to
**LANDING ZONES**, alongside BOOSTER SEPARATION, GRID FINS DEPLOY, ENTRY BURN, AERODYNAMIC GUIDANCE and
VERTICAL LANDING. **The guide's only depicted boostback is the Falcon Heavy RTLS case.**

---

### ⭐⭐ THE REFERENCE MISSION TIMELINES *(PDF p.96 / printed 85)*

**VERIFIED AGAINST THE RENDERED PAGE, then re-read, then cross-checked against independent text
extraction. All three agree.** These are **two separate tables**, not one — keep them apart.

#### Table 10-3: Falcon 9 Sample Flight Timeline — **GTO Mission**

| Mission elapsed time | Event |
|---|---|
| **T − 3 s** | **Engine start sequence** |
| **T + 0** | **Liftoff** |
| T + 74 s | Maximum dynamic pressure (max Q) |
| T + 147 s | Main engine cutoff (MECO) |
| T + 151 s | Stage separation |
| T + 158 s | Second engine start-1 (SES-1) |
| T + 222 s | Fairing separation |
| T + 484 s | Second engine cutoff 1 (SECO-1) |
| T + 1636 s | Second engine start-2 (SES-2) |
| **T + 1696 s** | **Second engine cutoff-2 (SECO-2)** |
| T + 1996 s | Spacecraft separation |

#### Table 10-4: Falcon 9 Sample Flight Timeline — **LEO Mission**

| Mission elapsed time | Event |
|---|---|
| **T − 3 s** | **Engine start sequence** |
| **T + 0** | **Liftoff** |
| T + 67 s | Maximum dynamic pressure (max Q) |
| T + 145 s | Main engine cutoff (MECO) |
| T + 148 s | Stage separation |
| T + 156 s | Second engine start-1 (SES-1) |
| T + 195 s | Fairing separation |
| T + 514 s | Second engine cutoff-1 (SECO-1) |
| T + 3086 s | Second engine start-2 (SES-2) |
| **T + 3090 s** | **Second engine cutoff-2 (SECO-2)** |
| T + 3390 s | Spacecraft separation |

⛔ **CAVEAT THE GUIDE PUTS ON THEM ITSELF** *(§10.7, PDF p.95)*: *"Note: each flight profile is unique and
will differ from these examples."* And §10.6.1: MECO is commanded on **target velocity or remaining
propellant**, so these times are outcomes, not settings.

#### ⭐ Derived intervals (arithmetic on the table above, shown so it can be checked)

| Interval | GTO | LEO |
|---|---|---|
| Ignition → liftoff (hold-down) | **3 s** | **3 s** |
| MECO → stage sep | 4 s | 3 s |
| Stage sep → SES-1 | 7 s | 8 s |
| MECO → SES-1 (total S1/S2 gap) | 11 s | 11 s |
| SES-1 → fairing sep | 64 s | 39 s |
| **S2 burn 1 (SES-1 → SECO-1)** | **326 s (5 m 26 s)** | **358 s (5 m 58 s)** |
| Coast (SECO-1 → SES-2) | 1,152 s (19 m 12 s) | 2,572 s (42 m 52 s) |
| **⭐ S2 burn 2 (SES-2 → SECO-2)** | **60 s** | **⭐ 4 s** |
| SECO-2 → spacecraft sep | 300 s (5 m) | 300 s (5 m) |

⭐ **Three things worth pulling out of that:**
1. **The S2 first burn is 5 m 26 s / 5 m 58 s — exactly the "five to six minutes" §10.6.1 states.** The two
   sections corroborate each other.
2. ⭐ **The LEO second burn is FOUR SECONDS.** A 4-second relight against the GTO profile's 60 s. That is a
   circularisation nudge, not a transfer burn — and it is precisely the number the pre-supplied extraction
   dropped. **Anyone planning a second-stage restart against this guide needs that distinction.**
3. **Spacecraft separation is +300 s after SECO-2 in both profiles** — a settling/attitude interval, not a
   mission-dependent one.

#### ⚠ What the pre-supplied extraction got wrong

The extraction quoted in the task brief read:
`T-3 s engine start · T+0 liftoff · T+67-74 max Q · T+145-147 MECO · T+148-151 stage sep · T+156-158 SES-1 ·
T+195-222 fairing sep · T+484-514 SECO-1 · T+1636/3086 SES-2 · T+1996/3390 sc sep`

- ⛔ **SECO-2 is missing entirely** — both T+1696 (GTO) and T+3090 (LEO). With it goes the whole second-burn
  duration, including the 4-second LEO relight.
- ⚠ **The ranges merge two different missions.** "T+67-74 max Q" is not a range: it is **67 s on the LEO
  profile and 74 s on the GTO profile**. Reading it as a range loses which mission a number belongs to.
- Everything else in the extraction **is correct** and matches the rendered page.

---

## 11. Safety *(PDF p.97 / printed 86)*
- §11.1: customers must meet **AFSPCMAN 91-710** (Range User's Manual) and **FAA 14 CFR Part 400**. SpaceX is
  the safety liaison to the Range.
- §11.2: hazardous systems include ordnance, **pressurized systems operating below a 4-to-1 safety factor**,
  lifting, toxic/hazardous materials, high-power RF and lasers.
- §11.3: waivers are **a last resort**, not standard practice.

## 12. Contact information *(PDF p.98 / printed 87)*
SpaceX Sales, 1 Rocket Rd., Hawthorne, CA 90250 · sales@spacex.com.

---

## Appendices *(PDF pp.99–128 / printed 88–117)* — ⭐ ALL REVISED IN v8; A–D ARE TEXT-INVISIBLE

⛔ **Appendices A–D are landscape CAD drawings that extract as blank text.** Everything below was read off
the rendered pages. Dimensions are in **millimetres** unless the drawing says otherwise; several sheets are
dual-dimensioned with inches leading and `[mm]` in brackets.

### Appendix A: PAF mechanical interfaces *(PDF pp.99–101 / printed 88–90)*
| Figure | PDF page | Key dimensions read off the drawing |
|---|---|---|
| **A-1** 1,575-mm PAF | p.99 | Bolt circle **Ø62.010 in [1575.0540 mm]** · **120 holes**, Ø.265–.271 in · outer Ø(62.985) [1599.8190] · flange **.400 ± .010 in [10.16 ± 0.254 mm]** · drawing `1575_PAF_INTERFACE_CONTROL_DOCUMENT` rev 01 · note: fasteners installed from the PLA side |
| **A-2** 2,624-mm PAF | p.100 | Bolt circle **Ø103.307 in [2624.0000 mm]** · **244 holes** at **1.475° spacing**, Ø.327–.333 in · outer Ø(105.000) [2667.0000] · flange .400 ± .010 in · note: spacer may be required to clear the PAF closeout |
| **A-3** Square PAF | p.101 | **2× (54.116) in [1374.54]** across · **4× (46.192) in [1173.27]** · **96 holes** Ø.344 +.006/−.001 in [8.73] · hole pitch (2.008) in [51.00] · plate **.500 ± .010 in [12.70 ± 0.25]** · *"contact SpaceX for hole tolerance requirements if using the square PAF interface"* |

### Appendix B: Payload mechanical, electrical and purge standard interfaces *(PDF pp.102–110 / printed 91–99)*
Three clampband families × three sheets each (structural / electrical / purge):

| Clampband | Structural | Electrical | Purge |
|---|---|---|---|
| **937 mm** | B-1, p.102 | B-2, p.103 | B-3, p.104 |
| **1194 mm** | B-4, p.105 | B-5, p.106 | B-6, p.107 |
| **1666 mm** | B-7, p.108 | B-8, p.109 | B-9, p.110 |

**Structural sheets — S/C frame properties for the section less than 25.4 mm above the interface:**

| Clampband | Area (mm²) | I_xx (mm⁴) | I_zz (mm⁴) | Interface Ø (mm) |
|---|---|---|---|---|
| 937 | **345 ± 115** | 40,350 ± 28,650 | 10,065 ± 2,585 | Ø940.73 +0.13/−0 |
| 1194 | **390 ± 160** | 38,500 ± 26,500 | 16,250 ± 8,750 | Ø1209.55 +0.12/−0 |
| 1666 | **460 ± 69** | 53,000 ± 7,950 | 15,000 ± 2,250 | Ø1660.52 +0.12/−0 |

Material: **any aluminium alloy**; finish on all interfacing features **chemical conversion coating per
MIL-DTL-5541 Class 1A or equivalent**. Each ring carries an **engraved mark 180° from the opening device**.

**⭐ Electrical / configuration sheets — the separation-spring data:**
- **937 mm** (B-2): **up to 12 springs at 12 × 30°**, spring centreline **Ø887.5**; **60 J and 20 J energies
  standard**; **2 electrical connectors standard, 180° apart**, 3 paired locations, ±2.0 mm static
  adjustability per location; connector centreline **Ø1219.2**.
- **1194 mm** (B-5): **up to 12 springs at 12 × 30°**, spring centreline **Ø1161**; same 60 J / 20 J;
  connector centreline **Ø1578.25**.
- **1666 mm** (B-8): **up to 8 springs at 8 × 45°**, spring centreline **Ø1600**; same 60 J / 20 J;
  connector centreline **Ø2010**.
- All three: spacecraft connector aft face **85.0 mm to separation plane** with DBAS 70-61 pin or D8179
  equivalent; SpaceX-side **DBAS 79-61 pin** at **72.35 ± 0.38**, shimmable to within ±0.5 mm; connector
  keyway radial out.

**Purge sheets (B-3 / B-6 / B-9, nonstandard service):** **6 possible purge positions**, interchangeable with
the electrical connectors; **150 N max force**, **3.45 barg (50 psig) max pressure**, **175 N/mm
(1000 lbf/in) min stiffness**; spacecraft purge interface **85 ± 0.25 mm** to the separation plane;
**22.9 mm min gasket contact area**, inlet 6.35–6.6 mm.

### Appendix C: Constellation payload mechanical interfaces and keep-in volumes *(PDF pp.111–115 / printed 100–104)*
| Figure | PDF page | Content |
|---|---|---|
| **C-1** | p.111 | **15-in diameter mechanical interface** — bolt circle **Ø15.000 in [381.000 mm]**, **24 holes** Ø.271–.278, **23 × 15.0°** with a 7.5° clock, outer Ø(16.000) [406.400] |
| **C-2** | p.112 | **24-in diameter mechanical interface** — bolt circle **Ø24.000 in [609.600 mm]**, **36 holes** Ø.271–.278, **35 × 10.0°** with a 5.0° clock, outer Ø(25.000) [635.000] |
| **C-3** | p.113 | **Cube arrangement keep-in volume** (no acoustic blankets) — **46.00 in [1168.40]** wide · **34.82 in [884.42]** · **(58.50) in [1485.90]** · **49.69 in [1262.03]** · **119.33 in [3030.86]** tall · **R 87.00 in [2209.80]** · 90.0° sector. **One payload per volume, 4 volumes per tier; the payload adapter must also fit inside the volume.** |
| **C-4** | p.114 | Cube arrangement **with intrusion** — as C-3 plus a **Ø21.00 in [533.40]** intrusion at **7.00 in [177.80]** |
| **C-5** | p.115 | **Octagon arrangement keep-in volume** (2 of 8 shown) — **2× 46.000 in [1168.40]** · **2× 34.506 in [876.44]** · **2× 58.094 in [1475.59]** · **2× 21.903 in [556.35]** · **2× 60.455 in [1535.54]** · **R 87.000 in [2209.80]** · **2 × 45.000°**. **One payload per volume, 8 volumes per tier.** |

### Appendix D: Cryogenic propellant interface *(PDF p.116 / printed 105)*
**Figure D-1: Cryo-propellant mechanical interface (1,575 mm clampband).** Bolt circle centreline
**Ø1575.05**; **cryo QD centreline Ø1399.99**; **4 QD positions at 5.000°**. ⭐ Four fluid lines, labelled:
**LOX VENT · LOX FILL · LCH4 VENT · LCH4 FILL** — i.e. the interface supports **methane** payloads as well as
LOX. Launch-vehicle side **120 × Ø6.70 +0.15/−0.00 THRU**. Fittings: customer **AS4325-12** conical fitting
and **AS4326 K 12 L** nut mating to launch-vehicle **AS1098 E 12** male fitting with **AS1097-12** seal;
SpaceX fitting **PN 01443488-026**. Interface plane to the top of the nut feature **22.866 ± 8.890 mm**.
Notes: the customer sizes the flex hose; **contact SpaceX for the full interface definition**.

### Appendix E: Payload CAD model requirements *(PDF p.117 / printed 106)*
**NX Parasolid (.x_t) preferred**, else **STEP 214 or lower**. **File size ≤ 100 MB.** Simplified to outer
mould line and interface fidelity. Must include: the LV mechanical interface, electrical connectors and
brackets per **AV2052 (Electrical ICD)**, pusher pads, clearance-critical external components, anything within
**< 20 cm** of those, anything protruding below the separation plane, access points, and simple bus
structure. Must NOT include internal components or spurious detail. Mass properties must match the delivered
CAD coordinate system exactly.

### Appendix F: Payload dynamic model requirements *(PDF pp.118–121 / printed 107–110)*
**Craig-Bampton reduced model**, **≤ 500 MB**, **frequency content up to 150 Hz**, multipoint interface,
6 DOF at each interface node, slosh effects included and identified.

⭐ **Boundary grid counts by interface diameter** *(PDF p.118 / printed 107)* — **the fullest list of Falcon
payload interface diameters in the document:**

| Interface diameter (mm) | Number of boundary grids |
|---|---|
| **937** | 120 |
| **1194** | 240 |
| **1575** | 120 |
| **1666** | 360 |
| **2624** | 244 |
| **2795** | 180 |
| **3117** | 180 |

Boundary DOF in a **cylindrical output coordinate system**, origin at the interface centre, nodes numbered
sequentially **counterclockwise about LV +X** (right-handed). Matrix requirements (M, K, OTM/DRM1/DRM2),
**recoveries limited to 5,000 rows**, **SRS rows limited to 500**, damping as percent of critical, SpaceX
applies a model uncertainty factor reflecting launch vehicle maturity.

### Appendix G: Payload thermal model requirements *(PDF pp.122–125 / printed 111–114)*
**Thermal Desktop (.dwg)**, units **Joules, seconds, inches, lb, °C**. **< 5,000 nodes**, **max four
independent LV interfaces**; ≤ 800 submodels, ≤ 2,000 symbols, ≤ 15 radiation analysis groups, ≤ 1,000 domain
tag-sets. Must run **faster than real time** (SINDA run time < 1 h clock per 1 h model time). SpaceX performs
an integrated launch thermal analysis **from hangar rollout to payload deploy**.

⭐ **The canonical flight-phase names, from the symbol table** *(PDF p.124 / printed 113)*:

| Analysis phase | PL_OnPad | Heaters | AvPower |
|---|---|---|---|
| **In Hangar** (steady) | 1 | 0 | 0 |
| **Rollout** (transient) | 1 | 0 | 0 |
| **On Pad** | 1 | 1 | 1 |
| **Liftoff to SES1** | 0 | 1 | 1 |
| **SES1 to Fairing Deploy** | 0 | 1 | 1 |
| **Fairing Deploy to SECO1** | 0 | 1 | 1 |
| **SECO1 to Payload Deploy** | 0 | 1 | 1 |

*(`PL_OnPad` is 1 before liftoff and 0 after — the guide's own on-pad/in-flight discriminator.)*

### ⭐ Appendix H: Delivery format of separation state vector *(PDF p.126 / printed 115)*
**The SpaceX OPM (orbit parameter message) format:**

- All orbital elements are **osculating at the instant of the printed state**.
- ⭐ Elements are computed **in an inertial frame realized by inertially freezing the WGS84 ECEF frame at the
  time of the current state**.
- ⭐ *"This OPM is provided based on flight telemetry from the second stage, and therefore represents the
  state of the second stage and not the state of any other body."*

| Field | Notes |
|---|---|
| UTC time at liftoff | DOY:HH:MM:SS.SS |
| UTC time of current state | DOY:HH:MM:SS.SS |
| Mission elapsed time (s) | |
| **ECEF (X,Y,Z) position (m)** | |
| **ECEF (X,Y,Z) velocity (m/s)** | ⭐ **Earth-relative** |
| **LVLH → BODY quaternion (S,X,Y,Z)** | |
| **Inertial body rates (X,Y,Z) (deg/s)** | |
| **Apogee altitude (km)** | ⭐ assumes a **spherical Earth, radius 6378.137 km** |
| **Perigee altitude (km)** | same assumption |
| **Inclination (deg)** | |
| **Argument of perigee (deg)** | |
| **Longitude of the ascending node (deg)** | ⭐ **LAN defined as the angle between the Greenwich meridian (Earth longitude 0) and the ascending node** |
| **True anomaly (deg)** | |

⚠ **That LAN definition is Greenwich-referenced, not the inertial RAAN convention.** Anyone comparing a
Falcon OPM against an inertial RAAN must account for the difference.

### Appendix I: Test schedule for constellations *(PDF pp.127–128 / printed 116–117)*
**Table I-1** — example schedule for **20 identical payloads** following protoqualification: the **first five
serial numbers, then every fifth** (SN10, SN15, SN20) are fully tested at integrated level. Rows: quasi-static
load, sine vibration, shock, random vibration or acoustics, power inhibits, EMI/EMC, thermal cycling/vacuum,
leak test. Legend: **R = Required · A = Advised · PT = Protoqualification test · AT = Acceptance test.**
**Table I-2** — the same schedule with a **Type 2 retest trigger at SN12**: SN12 and SN13 are fully tested to
re-establish the baseline, then lot sampling resumes. Footnote: power inhibit, EMI/EMC and leak tests may be
waived if the changes triggering the retest are structural in nature.

---

## 13. ⭐ WHAT THIS LANDS ON — open DragonScreen work

⛔ **This section reports. It changes nothing.** `docs/BUILD_PLAN.md` is a guarded file (C1.12 guarded-file
standard, G10) and this chat does not edit it. Everything below is carried in `S222`'s register line for a
later owner-authorised `G`-line to act on, or not.

### 13.1 ⭐ `S219`'s ignition timing — the guide independently confirms it

**`S219` established the ignition chain and its T−3 s timing as an engineering estimate. The guide supplies a
documented source for both halves:**

| Claim | Guide source | Grade |
|---|---|---|
| **Engine start at T−3 s** | **Tables 10-3 AND 10-4, PDF p.96 / printed 85** — *"T − 3 s : Engine start sequence"*, identically in both the GTO and the LEO profile | **[DOC]** |
| Ignition happens with the vehicle **held down**, on **hydraulic clamps** | §10.5.5, PDF p.94 / printed 83 | **[DOC]** |
| The **flight computer evaluates ignition and full-power performance** during hold-down | §10.5.5, PDF p.94 | **[DOC]** |
| **Release is activated at T-0**, only if nominal criteria are satisfied | §10.5.5, PDF p.94 | **[DOC]** |
| **Off-nominal → safe shutdown** on the pad | §10.5.5, PDF p.94 | **[DOC]** |
| Hold-down until all systems verified normal | §2.3, PDF p.19 / printed 8 | **[DOC]** |
| Hold-down release is a **flight shock event** | §5.3.5, PDF p.49 / printed 38 | **[DOC]** |

⛔ **Be precise about what this promotes.** **§B11 has no ignition or T-0 row at all** — its Ascent block runs
Max-Q → MECO → insertion orbit → peak axial accel. So this is **not** the promotion of an existing §B11
`[EST]`; it is (a) **independent documentary confirmation of `S219`'s own estimate**, and (b) **a new [DOC]
row that §B11's Ascent block does not currently carry**. Recording it as "promotes an existing [EST]" would
overstate it.

### 13.2 §B11 Ascent block — how each existing row compares

Read-only comparison. **⛔ Nothing here is a correction to §B11**: the guide's tables are explicitly
mission-specific samples (§10.7) and §B11's numbers come from Crew-Dragon sources, which is a different
mission profile again.

| §B11 row | §B11 value | Guide (LEO, Table 10-4) | Guide (GTO, Table 10-3) | Read |
|---|---|---|---|---|
| Max-Q **[DOC]** | ~T+1:12 = **72 s** | **T+67 s** | **T+74 s** | ✅ §B11 sits between the two sample profiles |
| MECO **[DOC]** | ~T+2:17 = **137 s** | **T+145 s** | **T+147 s** | ⚠ guide is **8–10 s later** — **D6**, recorded not reconciled |
| Stage sep | ~2:21 = **141 s** | **T+148 s** | **T+151 s** | ⚠ same offset |
| S2 ignition | ~2:28 = **148 s** | **T+156 s** | **T+158 s** | ⚠ same offset |
| SECO-1 **[DOC]** | ~8:33 = **513 s** | **T+514 s** | T+484 s | ⭐ **LEO matches to 1 second** |
| "in orbit ~9 min" | ~540 s | SECO-1 at 514 s | — | ✅ consistent |
| Insertion orbit **[DOC/cfg]** | ~190–210 km × 51.63° | — | — | ⛔ **the guide does not state insertion altitudes** (§3.2 withholds performance) |
| Peak axial accel **[EST]** | **~4 g** near MECO and SECO | — | — | ⚠ the guide gives a **payload limit of 6.0 g axial** for >1,800 kg (Table 5-3, PDF p.44) and says engines *"may be throttled to help maintain … acceleration limits"*. **That bounds the [EST] from above; it does not confirm 4 g.** Still **[EST]**. |

⭐ **The MECO offset (D6) has a plausible mechanical reason worth flagging without asserting it:** the guide's
own §10.6.1 says MECO is commanded on **target velocity or remaining propellant**, and §B11's number comes
from a **Crew-Dragon** ascent while Tables 10-3/10-4 are **fairing** profiles. Different payload mass,
different MECO time. Both may be right for their own mission. **Left as recorded, not reconciled.**

### 13.3 §B16 booster recovery — what the guide supplies

- ⭐ **The event sequence** (Figure 10-11, PDF p.95): stage sep → **flip on cold gas thrusters** → grid fins
  deploy → **entry burn** → **aerodynamic guidance on grid fins** → **landing burn / vertical landing** →
  droneship. **No boostback in the F9 profile shown** — boostback appears only in the Falcon Heavy figure
  (Figure 10-12) for side boosters returning to landing zones.
- **Hardware:** **4 grid fins** near the top of the first stage; **4 deployable legs** at the base, *"nominally
  flown to support recovery operations"* (§2.3, PDF p.19).
- ⭐ **Coast attitude control on the first stage is "Nitrogen gas thrusters (recovery only)"** (Table 2-1,
  PDF p.20) — the cold-gas system exists on the booster **solely** for the recovery flight.
- **Throttle authority for the burns:** the only throttle figures the guide gives are Table 2-1's
  **190,000 → 108,300 lbf per engine (≈57 %)**. **The guide does not discuss landing-burn throttling, engine
  count for the entry or landing burn, or any deep-throttle capability.**
- ⛔ **No landing-zone coordinates anywhere.** Landing Zone **SLC-4W** is labelled on Figure 8-5 (PDF p.73)
  without a coordinate; LZ-1 / LZ-2 are not mentioned. See §8 above.

---

## 14. Open questions for the owner

### Q1 — ⭐ The first-stage thrust discrepancy: which number does DragonScreen treat as the guide's figure?

**Situation.** The guide states first-stage sea-level thrust **twice, differently, one page apart**:
**7,605 kN** in the §2.3 body text (PDF p.19 / printed 8) and **7,686 kN** in Table 2-1 (PDF p.20 /
printed 9), both against the same 1,710,000 lbf. Arithmetic makes **7,605 kN self-consistent** with the
guide's own lbf figure and its own per-engine figure (9 × 845 kN), and makes 7,686 kN the outlier; the same
conversion checks out cleanly for Falcon Heavy and for the second stage. Separately, our craft's octaweb
reported **8,227 kN** on 2026-09-07 — **+7.0 %** against Table 2-1, **+8.2 %** against the body text. The
change log (PDF p.11) shows **Section 2 was not among the March 2025 major updates**, so the table's content
may date from Version 7 (September 2021) or earlier.

**Options.**
1. **Record both, cite both, reconcile neither — treat "the guide's figure" as a range 7,605–7,686 kN and
   always quote the page.** *(Recommended.)* This is what §1.4 and §14.4(e) require of an unverifiable
   reconciliation, it costs nothing, and it keeps the octaweb comparison honest — the mod may well model a
   later uprate, which is the owner's own reading and the one the change-log evidence supports.
2. **Adopt 7,605 kN as the guide's figure** on the arithmetic, noting Table 2-1 as a probable transcription
   error. Cleaner for any future comparison, but it is a build chat deciding that a primary source is wrong —
   which §1.4 does not permit without owner authority.
3. **Adopt 7,686 kN** because it is the specification table. Keeps the brief's original 7 % figure, but
   knowingly carries the number that fails the guide's own arithmetic.
4. **Ask SpaceX / seek a second primary source.** Outward-facing; needs an owner gate.

**Recommendation: option 1.** ⛔ Options 2 and 3 both decide a §1.4 source question and need the owner;
option 4 is outward-facing and needs a gate (C1.12). **This chat proceeds with none of them** — the digest
records both numbers and marks them UNRESOLVED, which is option 1's behaviour and the only one available
without a ruling.

### Q2 — Should the guide's T−3 s and hold-down chain be carried into §B11 as new [DOC] rows?

**Situation.** §B11's Ascent block has **no ignition or T-0 row**. The guide supplies seven documented
statements covering ignition timing, hydraulic hold-down, flight-computer thrust evaluation, release at T-0,
and safe shutdown (§13.1) — independently confirming what `S219` built on an estimate. `docs/BUILD_PLAN.md`
is a guarded file; **this chat cannot add them** (C1.12 guarded-file standard, G10).

**Options.**
1. **A later owner-authorised `G`-line adds an "Ignition / T-0 **[DOC]**" row to §B11's Ascent block**,
   citing PDF p.96 and p.94. *(Recommended — it is exactly the route C1.12 prescribes for plan-grade
   material found by a non-governance chat.)*
2. Leave §B11 alone; the confirmation lives only in this digest and in `S222`'s register line. Cheaper, but
   the next ascent chat reads §B11, not this file.
3. Fold it into a broader §B11 refresh alongside the MECO-offset comparison in §13.2.

**Recommendation: option 1**, and **option 3 only if the owner also wants D6 addressed** — D6 is a comparison
between two different mission profiles and may deserve to stay unreconciled rather than be written into the
plan. ⛔ **Needs an owner-authorised `G`-line either way. This chat edits nothing.**

### Q3 — The PDF is outside the repo (C7). Confirm `S221` still owns landing it?

**Situation.** The guide was read from `Downloads` (§0.5). Until it is in `docs/reference/`, no later chat can
re-verify a page cite in this digest, and C7 says a build input that is not in the repo is a stop-and-flag.
The task brief assigns the copy to **`S221`** — but **there is no `S221` line in `REGISTER.md`** (checked
2026-09-08; the register's last line before this task is `S220`).

**Options.**
1. **Open `S221` as a real register line** — copy the PDF into `docs/reference/` and add it to
   `docs/INDEX.md`. *(Recommended: one small task, and it closes the C7 gap this digest currently carries.)*
2. Fold the copy into a later `G`-line.
3. Leave the PDF in `Downloads` and accept that the digest is the only in-repo record.

**Recommendation: option 1.** ⛔ Creating that line is outside this task's declared outputs (C1.11 — this
task writes only `docs/reference/FALCON_USERS_GUIDE.md` and its own register line), so **this chat has not
created it**. Flagged for the owner.

---

## 15. Quick answers — the things likely to be asked

| Question | Answer | Cite |
|---|---|---|
| First-stage sea-level thrust? | **7,605 kN** (body text) **or 7,686 kN** (Table 2-1) — the guide says both; see D1. 1,710,000 lbf in both places. | PDF p.19, p.20 |
| Per-engine sea-level thrust? | **845 kN (190,000 lbf)** | PDF p.18 |
| Second-stage vacuum thrust? | **981 kN (220,500 lbf)**, one MVac | PDF p.20 |
| Throttle floor? | S1 **108,300 lbf per engine** (≈57 % — computed); S2 **140,679 lbf** (≈63.8 % — computed) | PDF p.20 |
| Engine count? | **9** first stage, **1** second stage; **27** on Falcon Heavy | PDF p.20, p.18 |
| Engine designations? | **M1D** and **MVac** (Table 2-1); **"MVacD"** on the console chart | PDF p.20, p.92 |
| Which block? | **Falcon 9 Full Thrust Block 5**, first flew spring 2018 — **but Table 2-1 carries no block label** | PDF p.17, p.20 |
| Vehicle height / diameter? | **70 m** (75.2 m extended fairing) × **3.66 m** | PDF p.20 |
| Fairing diameter? | **5.2 m (17 ft)** | PDF p.12 |
| Second-stage nozzle? | **Fixed 165:1 expansion** | PDF p.20 |
| Second-stage igniters? | **Dual redundant TEA-TEB pyrophoric** | PDF p.20 |
| Restart capability? | **Yes, both stages** | PDF p.20 |
| Tank pressurant? | **Heated helium**, both stages | PDF p.20 |
| Roll control? | S1 **gimbaled engines**; S2 **nitrogen gas thrusters** | PDF p.20 |
| Coast attitude control? | S1 **nitrogen gas thrusters — recovery only**; S2 nitrogen gas thrusters | PDF p.20 |
| When does the engine light? | **T−3 s**, both sample profiles | PDF p.96 |
| What holds it down? | **Hydraulic clamps**; flight computer evaluates thrust; **release at T-0**; off-nominal → safe shutdown | PDF p.94 |
| Max Q? | **T+67 s (LEO) / T+74 s (GTO)** | PDF p.96 |
| MECO? | **T+145 s (LEO) / T+147 s (GTO)**; commanded on **target velocity or remaining propellant** | PDF p.96, p.94 |
| Stage sep? | **T+148 s (LEO) / T+151 s (GTO)** | PDF p.96 |
| SES-1? | **T+156 s (LEO) / T+158 s (GTO)** | PDF p.96 |
| Fairing sep? | **T+195 s (LEO) / T+222 s (GTO)**; max deploy time **≈230 s (F9)**, flux **< 1,135 W/m²** | PDF p.96, p.53 |
| SECO-1 / S2 burn 1? | **T+514 s (LEO) / T+484 s (GTO)** — a **5 m 58 s / 5 m 26 s** burn; §10.6.1 says "five to six minutes" | PDF p.96, p.94 |
| SES-2 / SECO-2 / S2 burn 2? | **T+3086 → T+3090 s (LEO) = 4 s**; **T+1636 → T+1696 s (GTO) = 60 s** | PDF p.96 |
| Spacecraft separation? | **T+3390 s (LEO) / T+1996 s (GTO)** — **+300 s after SECO-2** in both | PDF p.96 |
| How does the booster come back? | flip on cold gas → grid fins → **entry burn** → aero guidance → **landing burn** → droneship. **No boostback shown for F9** | PDF p.95 |
| Max payload acceleration? | **6.0 g axial** for payloads > 1,800 kg (8.5 g for 1,000–1,800 kg; 11.0 g under 1,000 kg) | PDF p.44 |
| Acoustic level? | **OASPL ≈ 131.3–131.6 dB** with blankets | PDF p.46 |
| Flight shock events? | **hold-down release · (FH booster sep) · stage sep · fairing sep · spacecraft sep** | PDF p.49 |
| Passive thermal roll? | **up to ±1.5 deg/s** about X, **±5 deg** LVLH roll accuracy | PDF p.24 |
| LV coordinate frame? | right-handed, origin **440.69 cm (173.5 in) aft of the first-stage radial engine gimbal**, +X along the long axis, +Z opposite the strongback; X=roll, Y=pitch, Z=yaw | PDF p.26 |
| Pad coordinates? | **SLC-40 28.5620 N / 80.5772 W · LC-39A 28.6082 N / 80.6041 W · SLC-4E 34.6320 N / 120.6107 W** | PDF p.69, p.70, p.73 |
| Separation state vector format? | ECEF position + **Earth-relative** velocity, **LVLH→BODY quaternion**, inertial body rates, apogee/perigee on a **6378.137 km spherical Earth**, **LAN measured from Greenwich** | PDF p.126 |
| **Payload mass to orbit?** | ⛔ **The guide does not state this** — available on request | PDF p.23 |
| **Separation attitude / rate accuracy?** | ⛔ **The guide does not state this** — mission-specific, available on request | PDF p.25 |
| **Landing zone coordinates?** | ⛔ **The guide does not state this** — SLC-4W is labelled on a map only; LZ-1/LZ-2 are never mentioned | PDF p.73 |
| **Dragon launch performance?** | ⛔ **The guide does not state this** — but it does say **all first and second stage systems are the same** in the fairing and Dragon configurations | PDF p.17 |
| **Landing-burn throttle / engine count?** | ⛔ **The guide does not state this** | — |
| **Specific impulse, propellant masses, burn-time budgets?** | ⛔ **The guide does not state any of these anywhere** | — |

---

*Built by `S222`, 2026-09-08, from the rendered pages of `falcon-users-guide-2025-05-09.pdf`. Every page 1–128
was opened; pages carrying tables, figures or engineering drawings were read visually at scale 2 and the
number-bearing ones re-read at scale 3. C1.16: supersede in place, never delete.*
