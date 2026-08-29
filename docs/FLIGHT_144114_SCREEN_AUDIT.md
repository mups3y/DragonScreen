# Flight `Crew-2_20260829_144114` — full screen/UI audit (Chris's screenshot tour)

> Chris deliberately captured **every DragonScreen page + button** during the C2 Step-2 Tick-3 flight (Steam
> screenshots, 14:42–14:57 wall-clock). This is the UI-audit pass over that tour. **Every claim below was
> cross-checked against the source** — several first-glance "bugs" turned out to be intentional, and those are
> listed under RULED OUT so they are not re-raised. Confidence is stated per item.

## ✅ CONFIRMED issues (traced to source or unambiguous)

| # | Issue | Evidence (screenshot + source) | Severity |
|---|---|---|---|
| **U1** | **Phase classifier reads `PHASING` while the vehicle is still SUB-ORBITAL on the S2 insertion burn.** From ~T+5:02 to ~T+8:12 the FLIGHT/OVERVIEW/NAV screens show `ACTIVE PHASE PHASING` while pe is **−4,600 → −2,000 km** (SECO not until ~T+8:53) — and the AUTO mode label simultaneously says **"Ascent to orbit"**. | 144637/144721/144858/144921/144947. **Root:** `VesselData.cs:77` `Mission.Classify(mi)` keys on **situation (Regime) + target presence**, with NO orbit-closed check → "in space + has ISS target" ⇒ Phasing, even mid-ascent. Should stay ASCENT/INSERTION until orbit is actually closed (pe above atmosphere / SECO). | **Med** — misleading crew phase readout; internally inconsistent with the AUTO label + the SECO-pending checklist. |
| **U2** | **`STATE → CAUTION` during nominal late ascent, from a PROPELLANT low-alarm.** The propellant gauge (by design, U-RO-4) shows the **near-spent S2** near SECO (~16%), which trips `Alarms.Low(Propellant01)` → the OVERVIEW status row flags **PROPELLANT CAUTION** → whole-vehicle **STATE CAUTION** (~T+7:23 on). The Dragon's actual return propellant (MMH/NTO) is full at this point. | 144921 (centre gauge 16% + left "PROPELLANT CAUTION"); STATE CAUTION 144858→. `Pages.cs:1082` `Dot(..., Alarms.Low(s.Propellant01))`. | **Low-Med** — nuisance/false CAUTION while the ascent stage empties normally; consider suppressing the low-prop alarm while the LIT stage is an ascent stage, or alarm on the RETURN budget instead. |
| **U3** | **`NET PWR 1` and `NET PWR 2` both read exactly `0 W`** on VEHICLE OVERVIEW. The dials are simulated (`CabinEnvironment`) and the comment (`Pages.cs:974`) expects them **negative on battery** (e.g. −59 W). Exactly 0 on both buses looks unpopulated. | 144921 (both dials "0 W"). **NEEDS-VERIFY** against `CabinEnvironment` — is the sim producing a value in this state? | **Low** — verify. |
| **F1** | **"Overheat!" part warning during max-Q ascent (T+1:56).** A part runs into its thermal limit on the climb (aero heating). | 144330 (red "Overheat!" flag). Part not yet identified — check the interstage / grid-fin / fairing thermal in FAR. | **Med** — a thermal margin issue on ascent; identify the part + confirm it's not near failure. |

## 🔁 RECONFIRMED (already tracked — the screens show it playing out)

- **Rendezvous/phasing self-deorbits + drains the return propellant + never docks** (= C2a / register H2·L2). The
  ~23 h rendezvous made the orbit eccentric (**ap 200→420 km, pe 197→149.9 km — below the 150 floor**), drained
  **MMH 655→0 / NTO 509→0**, opened the nose cone for docking (145507) and closed to ~72 m/s, but **never
  captured** → ended in FDIR `ResourceCritical → SafeMode` (14:57). Screens: 145315/145402/145507/145624/145700/
  145733 (resource panel MMH/NTO → 0; pe 149,968). **PARKED** behind the ranked campaigns — not chased now.
- **Booster ballistic (eng never lit) → LOST** (= register H1b, ullage). Log-confirmed earlier.

## ⛔ RULED OUT — checked the source, NOT bugs (do not re-raise)

| Candidate | Why it is correct-by-design |
|---|---|
| ~~"SURPRESS FIRE" typo on the emergency button~~ | **Intentional.** `PanelMap.cs:19`: *"`SURPRESS FIRE` is spelled that way IN THE MODEL. Not corrected here — matching the installed art."* The button label matches the 3D IVA texture on purpose. |
| ~~MECH "NEGATIVE 0.02 deg/s" (wrong unit)~~ | **My misread of a tilted screen.** `MechPage.cs:56` renders NEGATIVE with unit **"g"**; the "deg/s" belongs to the ANGULAR row below it. |
| ~~POWER STRINGS "Ax Bx Cx" = placeholder~~ | **Intended status format.** `MechPage.cs:82` `StringWord(Systems, bus)` — the A/B/C string up/down indicators the ten string-isolation buttons control. |
| ~~PROPELLANT gauge 16% at insertion is wrong~~ | **By design.** `PropellantReadout.cs` header: *"THE GAUGE READ 100% ALL THE WAY TO ORBIT… WHAT IT SHOWS NOW: WHAT THE LIT ENGINES ARE ACTUALLY DRINKING."* 16% = the S2's near-spent propellant near SECO; the caption names it ("PROPELLANT COOLEDRP/COOLED LOX"). (The *CAUTION escalation* off this is U2.) |
| ~~PERIGEE renders blank "−"~~ | **Intentional.** `VesselData.cs:87` `OrbitReadout.PerigeeMeaningful(regime, PeA, atmo)` hides a sub-atmosphere/suborbital pe. |
| ~~Inclination reads ~53.6° vs 51.64° final~~ | Osculating inclination of the *suborbital* arc; it settles toward 51.6° as SECO closes the orbit (52.88° at 144921). Not a display error. |

## ❓ LOW-CONFIDENCE — noted, verify if seen again

- **Unidentified grey featureless sphere sits just below the trunk in close views** (145721/145733). Could be the
  distant target, a background body, or a stray/placeholder part — verify it is not a mis-rendered part.

## Pages/buttons covered by the tour (for completeness)
FLIGHT (144921 centre), VEHICLE▸OVERVIEW (144637/144921), VEHICLE▸MECH (144704), NAV▸3D-PLANET (144858), NAV▸GROUND-
TRACK (144637 right), DOCKING (144721/144722), SETTINGS▸VIDEO S2-cam (144745); the full button banks — VEHICLE/CABIN
EMERGENCIES, POWER 1/2, STRING 1A–2C, RESET 1/2, ENABLE BACKUP PYROS, JETTISON NOSE CONE, MAINS ONLY, DROGUES & MAINS,
ENABLE ENTRY REBOOT, CUT MAINS, FIRE PYRO, the EJECT twist-handle, ENABLE BACKUP/NORMAL ENTRY (144809/144921).
