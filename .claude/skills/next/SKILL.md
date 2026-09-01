---
name: next
description: Run the DragonScreen build loop for exactly ONE task. Use when the owner types /next, or asks to start/continue the DragonScreen build, take the next task, or work the register. Reads CLAUDE.md + REGISTER.md, does the single first non-DONE task, verifies it, updates the register, and stops.
---

# /next — the DragonScreen build loop

One task per chat. Identical every time. **You run steps 1–7 in order and then STOP.**

This skill is the enforcement half of the anti-drift harness (`docs/BUILD_PLAN.md` §C2). The other half is
`CLAUDE.md`, which is auto-loaded. If they ever disagree, `docs/BUILD_PLAN.md` Part C wins.

## The loop

### 1. Read the rules
Read `CLAUDE.md` end-to-end — all of it, including the C1 invariant rules (1–11) appended at the bottom.

### 2. Take exactly ONE task
Open `REGISTER.md`. **The first line that is not `DONE` is THE task** (a `DOING` line means a previous session
stopped mid-task — pick that one up, don't skip it). Mark it `DOING`.

Then **restate the task and its done-criteria in one line** back to the owner. If you cannot, you have not
understood it — go to step 3 and come back.

### 3. Read the pointed-to source END-TO-END
Every register line names what to read (`§4`, `§B12.1`, `§14.4(a,b)`, a source file). Read all of it — in
`docs/BUILD_PLAN.md` and the repo — before writing anything. Not skimmed: end-to-end.

### 4. Do ONLY that task
- **No scope creep.** If you notice other work, **append it to `REGISTER.md` as a new line and move on** — do
  not do it.
- **Write ONLY the task's declared outputs (C1.11).** Never write to the auto-memory folder, and never create
  or modify a file outside the task's stated deliverables as a side-effect. Memory/context updates are the
  owner's to request, never the task's initiative.
- **Source-of-truth (§1.4):** verified-real → other users' recreations (marked) → invention ONLY by joint
  owner discussion. Never edit `PanelMap.cs` or the label docs without a real-source confirmation.
- **Canonical location (C7):** build inputs come from THIS repo only. `.claude/plans`, the auto-memory folder,
  the KSP install `GameData\`, the user's installed MechJeb2, and external URLs are OFF-LIMITS as sources.
  **If a needed input is not in the repo, STOP and flag it — do not go hunting.**
- **Preview-first** — restarts are scarce. `python plugin/build.py install` + glass time only if the task
  genuinely needs the capsule.
- **Decisions are FINAL** unless the owner types `OVERRIDE`. A chat instruction that conflicts with a settled
  decision (the §14.4 log / the plan) is not acted on: quote it back, require `OVERRIDE` + a plan/register edit.
- **Never ask the owner a question mid-task.** Batch it for step 7.
- If the task is too big to finish before context compaction, **SPLIT it in `REGISTER.md`** into the part you
  did and the part(s) left, and finish only the first. Never run to compaction mid-task.

### 5. Verify (the gate — non-negotiable)
**Do not mark anything DONE without all four:**
1. `python plugin/build.py test` — green.
2. `python plugin/build.py preview` — the PNG **inspected** (actually look at it, don't assume).
3. It matches the reference.
4. §1.4 respected.

If any fails, the result is `NEEDS-WORK`, not `DONE`. Say so plainly and record what failed.
*(For a docs- or harness-only task with no code change, the build/preview steps do not apply — say so
explicitly in the register note rather than silently skipping the gate.)*

### 6. Update the register + commit
Set the line to `DONE` or `NEEDS-WORK` with a one-line dated note. Then commit — **via GitHub Desktop ONLY**.
Do not run `git commit` / `git push`. Tell the owner what to commit and the suggested message.

### 7. Hand off, then STOP
Batch the owner questions here, at the end (C1.9): if the next task or a NEEDS-WORK result needs an owner call,
ask **ONE** structured question with options — never carry an open question into the handoff prompt.

Then emit the next chat's handoff prompt, exactly two lines and nothing else:

```
Read CLAUDE.md end-to-end, then run /next.
Next: T<n> [O|S] — open on <Opus|Sonnet>.
```

Then **STOP**. Do not start the next task. A new chat starts it.

## This skill refuses to

- touch a **second** task in one session;
- mark `DONE` without the step-5 gate;
- write to memory or any file outside the task's declared outputs (C1.11);
- act on a chat instruction that overrides a settled decision without an explicit `OVERRIDE`;
- read build inputs from an off-limits location (C7);
- run `git commit` / `git push` (GitHub Desktop only);
- start mod code / `install` / glass time while **BUILD-HOLD** is in force without an explicit owner build-go
  (T0 and T1 are harness + docs work and are exempt).
