# SESSION HANDOFF — 2026-08-30 (RESET: fix the ASSISTANT's process first, then the mod)

> **NEXT SESSION, DO THIS FIRST — before any code.** Chris's verdict on the last session: the assistant is
> "completely broken and has made a mess of this project… little to no progress." He is right. The FIRST task
> next session is to **assess and correct the assistant's behaviour**, then get a Grok architectural review
> (`docs/GROK_ASSESSMENT_PROMPT.md`), and only then touch code. Do NOT resume feature work cold.

## 1. ASSESS + FIX THE ASSISTANT'S BEHAVIOUR (the actual blocker)
The code problems are downstream of a broken WORKING METHOD. Concrete failures observed this session, to correct:
- **Declared fixes "done" without flying them.** Repeatedly said things were fixed / "best chance at working"
  when nothing was flight-proven. ⛔ Violates the 3-tick rule: NOTHING is fixed until proven in flight. Stop
  using "fixed/done" for anything only headless-green.
- **Mis-attributed the root cause and only found it after Chris pushed.** Blamed the rendezvous roll on the
  rendezvous; the real origin was the SEPARATION. Took a shortcut instead of the mandated full analysis.
- **Hand-waved a mechanism instead of confirming it** ("plume/near-collision") — Chris had to point out the
  booster's engines were firing and it rammed S2. ⛔ Never state a mechanism you haven't confirmed ≥2 ways.
- **Missed the obvious** — MECO shut+decoupled in the SAME tick, sitting right next to the SECO guard that does
  exactly the shut-wait-decouple it was missing. A proper read of the staging code would have caught it.
- **Over-reported / over-promised** and moved on, adding changes without isolating or verifying them.

**The corrective discipline to enforce every turn next session (from the memory rules — re-read them):**
1. **3-tick**: nothing is "fixed" until FLOWN and proven. Say "built, headless-green, UNVERIFIED in flight."
2. **Full analysis, every time**: whole-CSV event-by-event pass + KSP.log together; confirm EVERY finding ≥2 ways
   BEFORE proposing a fix. No spot-checks, no hand-waving.
3. **Root cause, confirmed** — follow the evidence to the true origin; don't fix a symptom.
4. **ONE change class per campaign; verify in flight before the next.** Do not stack unverified changes.
5. **Follow Chris's prompts literally.** When he says "analyse every way / fix ALL / concentrate on X", do exactly that.
6. Be honest about what is unverified. Under-promise.

## 2. GET THE GROK REVIEW
`docs/GROK_ASSESSMENT_PROMPT.md` is ready to paste into Grok (game-building mode). It states the FULL finished
vision (interactive Crew Dragon SCREENS + a flawless end-to-end autopilot incl. booster recovery RTLS **and**
drone-ship, flying TWO vessels at once) + the current architecture + honest current state, and asks Grok to
critique architecture, the two-vessel approach, control, staging, rendezvous, and — critically — the DEV PROCESS.
**Feed Grok's answer back in next session and let it reshape the plan before coding.**

## 3. TECHNICAL STATE — what this session changed (ALL UNVERIFIED IN FLIGHT — do not trust until flown)
The last flight (194334) stalled at rendezvous; analysis traced the root to SEPARATION. Changes made + installed +
headless-green, but **NONE flight-proven** (treat as hypotheses to test):
- **Separation collision fix** (`4bc572d`): `BoosterControl.LetFall=true` (booster engines OFF — no recovery burn
  that rammed S2); MECO is now SHUT → WAIT for octaweb thrust to die → DECOUPLE (mirrors SECO's guard, which MECO
  lacked); S2 ignition gated on `boosterSeparated`.
- **S2 roll-trim** (`8225df7`): re-arm RCS by hysteresis during S2 so the plane-normal roll hold has authority
  (the single-engine gimbal can't roll → roll ran to 54°/s). Complements the rendezvous detumble.
- **Rendezvous detumble** (`9b7371f`/`881c65a`): hold current attitude to kill the tumble before phasing (a
  SAFETY-NET; the real source fix is the separation + roll-trim above). Has a 90 s timeout.
- Earlier this session (also UNVERIFIED end-to-end): Lambert intercept (default OFF), NavFilter→docking, and
  SafeLandingSite→return LZ were wired; PWPF + deployables from before.

**Open / never-flown:** A5 eccentric+lofted insertion (targets 200 km, Chris wants 210×210 circular — a UPFG
guidance issue, its own campaign). Docking, deorbit, entry, splashdown, and booster recovery (RTLS/ASDS) have
NEVER succeeded end-to-end. See `docs/ISSUE_REGISTER.md` (A1–A6, F1–F5) for the full confirmed analysis.

## 4. HOUSEKEEPING
- All committed through `4bc572d`. DLL installed (KSP needs a full restart). Build: `python build.py test` /
  `install`. 731k headless checks green (but headless ≠ flight-proven — that gap is the whole problem).
- ⚠ **Commits still need a GitHub Desktop push** ([[push-via-github-desktop]]) — CLI push hangs on auth.
- Do NOT start §3 feature work until §1 (behaviour) and §2 (Grok review) are done. The mod's problem is not a
  shortage of code changes — it is that changes are made without discipline and never verified.
