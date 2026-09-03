# LZ / droneship recovery table (LZ1)

**Register:** LZ1 [S] — *"LZ/droneship sourcing — the per-mission table + the two missing statics."*
**Reads:** `BUILD_PLAN.md` §B16.9 in full (the two-file KK placement schema).
**Source-of-truth tier (§1.4):** verified-real, sourced per row below. Two group-centre coordinates could
not be sourced to that tier — see **Open questions for the owner**.

## 1. The per-mission table

Starting point: the 16 owner-supplied `docs/reference/<mission>.craft` mission descriptions (§B16.9,
G5c/W4). Eight already name a specific droneship or RTLS; eight say only generic **"droneship"**. All 16
were then verified/extended against public flight records (Wikipedia mission/launch-list infoboxes,
NASASpaceflight, Spaceflight Now — see Sources). **Two corrections came out of that pass**, both flagged
below and logged as a stray for a separate task (§3).

| Mission | Date | Booster | Recovery (REAL, verified) | Target | vs `.craft` file | vs `MissionProfile.cs` |
|---|---|---|---|---|---|---|
| DM-2 | 2020-05-30 | B1058‑1 | Droneship | **OCISLY** | matches | matches |
| Crew-1 | 2020-11-16 | B1061‑1 | Droneship | **JRTI** | matches | matches |
| Crew-2 | 2021-04-23 | B1061‑2 | Droneship | **OCISLY** | matches | matches |
| Crew-3 | 2021-11-11 | B1067‑2 | Droneship | **ASOG** | matches | matches |
| Ax-1 | 2022-04-08 | B1062‑4 | Droneship | **ASOG** | matches | matches |
| Crew-4 | 2022-04-27 | B1067‑4 | Droneship | **ASOG** | matches | matches |
| Crew-5 | 2022-10-05 | B1077‑1 | Droneship | **JRTI** | resolved (was generic) | matches |
| Crew-6 | 2023-03-02 | B1078‑1 | Droneship | **JRTI** | resolved (was generic) | matches |
| Ax-2 | 2023-05-21 | B1080‑1 | **RTLS** | **LZ‑1** | matches | matches |
| Crew-7 | 2023-08-26 | B1081‑1 | **RTLS** | **LZ‑1** | resolved (was generic "droneship") | ⚠ **disagrees** (says Droneship) |
| Ax-3 | 2024-01-18 | B1080‑5 | **RTLS** | **LZ‑1** | matches | matches |
| Crew-8 | 2024-03-04 | B1083‑1 | **RTLS** | **LZ‑1** | resolved (was generic "droneship") | ⚠ **disagrees** (says Droneship) |
| Crew-9 | 2024-09-28 | B1085‑2 | **RTLS** | **LZ‑1** | resolved (was generic "droneship") | ⚠ **disagrees** (says Droneship) |
| Crew-10 | 2025-03-14 | B1090‑2 | **RTLS** | **LZ‑1** | resolved (was generic "droneship") | ⚠ **disagrees** (says Droneship) |
| Ax-4 | 2025-06-25 | B1094‑2 | **RTLS** | **LZ‑1** | ⚠ **corrects** `.craft` ("droneship") | ⚠ **disagrees** (says Droneship) |
| Crew-11 | 2025-07-31 | B1094‑3 | **RTLS** | **LZ‑1** (the **final** LZ‑1 landing before the pad's lease ended, Aug 2025) | resolved (was generic "droneship") | ⚠ **disagrees** (says Droneship) |

**Reading the last two columns.** "matches" = the real, sourced value agrees with what's already in that
file. "resolved" = the file only said generic "droneship" (no ship named / `RecoveryMode.Droneship` is
coarse by design, §B16.9); the real value fills the gap without contradicting it. **"⚠ disagrees"** = the
file's value is factually wrong against the sourced real-world record — flagged in §3, not fixed here
(out of LZ1's declared scope: LZ1 produces this table, not `MissionProfile.cs` edits).

**Summary: 8 real droneship recoveries, 8 real RTLS recoveries.** Droneship split: OCISLY (DM-2, Crew-2),
JRTI (Crew-1, Crew-5, Crew-6), ASOG (Crew-3, Crew-4, Ax-1). All 8 RTLS missions used **LZ‑1** — no other
Fossil Industries pad (`Fossil_LZ2`, `Fossil_LZ4`, `Fossil_StarbasePad`) is needed for this 16-mission roster.

## 2. KK placement — the two missing droneships (JRTI, ASOG)

Per §B16.9: the droneship NAME is the KK Group name, the MODEL is the `SpaceXbarge2` static already used
for OCISLY. Two files per droneship (`KerbalKonstructs/NewInstances/KK_GroupCenter_Earth_<Group>.cfg` +
`<static>-instances.cfg`), same schema as the existing OCISLY placement (`RefLatitude`/`RefLongitude` is
what guidance targets — the KK **group centre**, not a vessel position).

**⚠ NOT WRITTEN — blocked on a real coordinate (§1.4 tier 3, owner discussion required).** Unlike LZ‑1 (a
fixed ground pad with a published surveyed position, §3 below) or the existing OCISLY placement (already an
established in-code aim point, `assess_flight.py:405`), a droneship's recovery position is **mission-variable
by design** — SpaceX moves it along each flight's downrange track, not to a fixed dock coordinate. A
search across Wikipedia, NASASpaceflight, space-offshore.com (a site specializing in droneship tracking)
and SpaceX-RSS-RO community KK packs on GitHub (`pmborg/SpaceX-RO-Falcons` — a kOS-scripted vessel
droneship, not a KK static; no config with real coordinates found) turned up only a **range**, never a
single citable point: ASOG operates "300–650 km downrange from KSC/CCSFS" (space-offshore.com); JRTI's
first Atlantic test was "~320 km NE of Cape Canaveral, ~266 km SE of Charleston SC" (Wikipedia/Planetary
Society, a 2015 CRS-6-era position, not necessarily representative of its later 2022–23 Crew-5/6 slot).
Picking one point within those ranges is **invention**, which CLAUDE.md §1.4 reserves for **joint owner
discussion**, not a build chat's unilateral call. See the open question below — once the owner picks a
candidate (or supplies a sourced one), the two-file pairs are a mechanical fill-in of the schema already
proven by OCISLY.

## 3. RTLS target — Fossil_LZ1

**Static:** `Fossil_LZ1` (Fossil Industries "SpaceX Landing Pads" — confirmed installed 2026-09-03,
§B16.9). No re-verification against the live install was done or needed (C7 — that confirmation is already
recorded evidence; this task does not go looking in `GameData` for it).

**Real coordinate — LZ‑1, Cape Canaveral Space Force Station:** `28.48583, -80.54444`
(28°29′09″N 80°32′40″W; Wikipedia "Landing Zones 1 and 2" / Wikidata Q22078213). Unlike the droneships,
this is a fixed, surveyed ground pad — a single real coordinate genuinely exists, no owner call needed.
This is the **same pad** all 8 RTLS missions in §1 targeted (Ax-2, Ax-3, Ax-4, Crew-7, Crew-8, Crew-9,
Crew-10, Crew-11).

**⚠ LZ‑1 is real-world RETIRED as of Crew-11 (2025-08-01)** — SpaceX's lease ended and the pad is being
converted to Space Launch Complex 13 for another operator. This roster's most recent mission (Ax-4, then
Crew-11) still predates/coincides with that retirement, so it does not affect this table. It would matter
for any **future** mission added to the roster after Aug 2025 — noted here so a later LZ1-adjacent task
does not have to re-derive it.

**Placement (proposed, not yet written into a live install — C7 preview-only gate):**
```
STATIC {
    pointername = Fossil_LZ1
    Instances {
        UUID = <assign at placement time>
        RelativePosition = 0,0,0
        Orientation = 0,0,0
        Group = LZ-1
        RefLatitude = 28.48583
        RefLongitude = -80.54444
        LaunchSite {
            LaunchSiteName = LZ-1_Fossil_LZ1_0
        }
    }
}
```
This is a single-static placement (LZ‑1 is a uniquely-modelled pad, not a shared generic model needing the
Group-disambiguation the droneships need) — proposed for the owner to apply at the next authorized
`install` + glass-time session, and confirm placement then (this session cannot test it in-game; `install`
and glass time are a separate owner gate, per CLAUDE.md's preview-only build-go).

## Open questions for the owner (C1.14)

**Q1 — JRTI and ASOG have no citable single real coordinate (only a documented downrange range). Which
group-centre position should the two-file KK placement use?**

*Situation.* §2 above. LZ‑1 (fixed pad) and the existing OCISLY placement (an established in-code aim
point) both have a defensible single coordinate; JRTI and ASOG do not — droneship position is genuinely
mission-variable, and no public source (including community RSS/RO KK packs) gives a single citable point.
CLAUDE.md §1.4 reserves invention for joint owner discussion, so this session stops short of writing one in.

*Options.*
1. **Pick a representative point within the documented real operational envelope, offset from OCISLY's
   existing point so the three groups don't overlap** — e.g. ASOG at a point in the same 300–650 km
   downrange corridor OCISLY already uses (OCISLY's own point, `32.7875, -76.6445`, sits inside that same
   range, since ASOG took over OCISLY's East-coast route); JRTI at its documented ~320 km NE Cape Canaveral
   test position (roughly `30.7, -78.3`, computed from the two published bearing/range figures — this
   session did not do that trigonometry to avoid compounding one estimate with another). **Mark both
   COHERENT/representative, not historical-fact**, same honesty standard as §14.4(e).
2. **Leave JRTI/ASOG unplaced for now**, ship only the LZ‑1 pad from this task (8 of 16 missions), and
   open a follow-up register line once a better source turns up (a specific NOTMAR/hazard-zone coordinate,
   or the owner's own reference).
3. **The owner supplies a specific coordinate** (from SpaceX's own materials, a personal source, or a
   decision to just treat one of the droneships as "close enough" to OCISLY's own point to reuse it
   directly, single-group).

*Recommendation:* **(1)**, clearly marked as representative/coherent rather than historical-fact (consistent
with how OCISLY's own point already functions in this codebase) — it unblocks placement now instead of
leaving 2 of 3 droneships absent, and is honest about what tier of evidence it is. **(2)** is the
conservative fallback if the owner would rather wait for a tighter source. Turning **(1)** into a table
entry needs the owner's go (or amendment) since it is level-3 (§1.4) by nature — this build chat cannot
make that call unilaterally.

## Sources

- [SpaceX Crew-1](https://en.wikipedia.org/wiki/SpaceX_Crew-1), [Space.com — Crew-1 leaning booster](https://www.space.com/spacex-falcon-9-leaning-booster-photos) — JRTI.
- [SpaceX Crew-2](https://en.wikipedia.org/wiki/SpaceX_Crew-2), [Space.com — Crew-2 launch](https://www.space.com/spacex-crew-2-astronaut-launch-rocket-landing-success) — OCISLY (via `.craft` + reconciled, no contradicting source found).
- [SpaceX Crew-3](https://en.wikipedia.org/wiki/SpaceX_Crew-3) — ASOG (via `.craft` + reconciled, no contradicting source found).
- [Space.com — Crew-4 launch](https://www.space.com/spacex-crew4-astronaut-launch-success), [spaceOFFSHORE — B1067-4 ASOG landing](https://x.com/SpaceOffshore/status/1519225629870379008) — ASOG.
- [Axiom Mission 1](https://en.wikipedia.org/wiki/Axiom_Mission_1) — ASOG.
- [Fox News — Crew-5 JRTI landing](https://www.foxnews.com/science/spacex-falcon-9-booster-successfully-lands-just-read-instructions-drone-ship), [NASASpaceFlight — Crew-5](https://www.nasaspaceflight.com/2022/10/spacex-crew-5-launch/) — JRTI.
- [Space.com — Crew-6 launch](https://www.space.com/spacex-crew-6-mission-launches-to-space-station), [Spaceflight Now (X) — B1078 droneship](https://twitter.com/SpaceflightNow/status/1631175160660205570) — JRTI.
- [Axiom Mission 2](https://en.wikipedia.org/wiki/Axiom_Mission_2) — RTLS LZ‑1 (first crewed-Dragon RTLS).
- [List of Falcon 9 and Falcon Heavy launches (2023), via Wikipedia table](https://en.wikipedia.org/wiki/List_of_Falcon_9_and_Falcon_Heavy_launches_(2023)) — Crew-7 RTLS LZ‑1.
- [NASASpaceFlight — Axiom-3](https://www.nasaspaceflight.com/2024/01/axiom-3-multinational-crew/) — Ax-3 RTLS LZ‑1.
- [List of Falcon 9 and Falcon Heavy launches (2024), via Wikipedia table](https://en.wikipedia.org/wiki/List_of_Falcon_9_and_Falcon_Heavy_launches_(2024)) — Crew-8 RTLS LZ‑1.
- [SpaceX Crew-9](https://en.wikipedia.org/wiki/SpaceX_Crew-9) (article body, quoted directly) — RTLS LZ‑1.
- [NASA Commercial Crew blog — Falcon 9 booster lands successfully](https://www.nasa.gov/blogs/commercialcrew/2025/03/14/falcon-9-booster-lands-successfully/) — Crew-10 RTLS LZ‑1.
- [NASASpaceFlight — Axiom Mission 4 launch](https://nasaspaceflight.com/2025/06/ax-4-launch/) — Ax-4 RTLS LZ‑1 (corrects the `.craft` file's generic "droneship").
- [Spaceflight Now — Crew-11 live coverage](https://spaceflightnow.com/2025/07/31/live-coverage-nasa-spacex-to-launch-crew-11-mission-to-the-international-space-station-on-a-falcon-9-rocket-from-the-kennedy-space-center/), [DeepNewz — LZ-1 retired after Crew-11](https://deepnewz.com/space/spacex-retires-landing-zone-1-after-final-falcon-9-booster-touchdown-4826bb98) — Crew-11 RTLS LZ‑1, the final LZ‑1 landing.
- [Landing Zones 1 and 2 (Wikipedia)](https://en.wikipedia.org/wiki/Landing_Zones_1_and_2), [Wikidata Q22078213](https://www.wikidata.org/wiki/Q22078213) — LZ‑1 surveyed coordinate.
- [space-offshore.com — A Shortfall of Gravitas](https://space-offshore.com/a-shortfall-of-gravitas/) — ASOG's documented 300–650 km downrange operating envelope.
- [Planetary Society — "To Recover First Stage, Just Read the Instructions"](https://www.planetary.org/articles/20150410-just-read-instructions) — JRTI's ~320 km NE Cape Canaveral first-test position.
