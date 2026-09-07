// DragonScreen — PART LOSS WATCH  (register S227)
// ============================================================================================
// GLUE. The only place in this build that subscribes to `GameEvents`, and the reason it did not
// exist until 2026-09-08 is that nobody had asked the recorder the owner's question:
//
//   *"we need to know exactly if or when a part failed and which failed first … if a separator
//    suddenly disappears and was not commanded to do anything it is probably because it exploded or
//    failed. This would lead to a cascade of failures that could be confusing without analysing the
//    very first failure point that lead to a instant RUD."*
//
// KSP announces every one of these things. Before S227, `grep -rn "GameEvents\." plugin/src/`
// returned NOTHING: we were not listening to any of it.
//
// ---- ⛔ THE API WAS ENUMERATED, NOT ASSUMED (S227 hazard A) ----
// Every member used here was read out of `Assembly-CSharp.dll` (KSP 1.12.5) by reflection before a
// line was written — `GameEvents` has 399 public static fields and the register records which were
// taken and which rejected. Two of the rejections are load-bearing and are repeated here because the
// next reader will reach for them:
//
//   • `onPartExplode` is `EventData<GameEvents.ExplosionReaction>`, and `ExplosionReaction`'s ONLY
//     fields are `distance` and `magnitude`. ⛔ **IT DOES NOT NAME A PART.** A handler on it could
//     report that an explosion happened and never say what exploded — useless for "which failed
//     first", which is the entire question.
//   • `onPartDie` was passed over in favour of `onPartWillDie`. Both are `EventData<Part>`, but
//     `onPartWillDie` fires BEFORE destruction, so `p.vessel`, `p.parent`, `p.inverseStage` and
//     `p.partInfo` are still readable. Reading them after death is how you get an event whose every
//     identifying field is null.
//
// ---- HAZARD (C): EVERY `.Add` HAS ITS `.Remove`, AND THE TEST PROVES IT ----
// A `GameEvents` handler that outlives its scene fires on dead objects forever and produces an
// exception storm; this project lost a day to `GetPotentialTorque` doing exactly that. `Subscribe`
// and `Unsubscribe` are mirror images, `subscribed` makes both idempotent, and
// `PartLossTest.SymmetryHolds` reads THIS FILE and fails the build if a handler is added without
// being removed — including a proof that the check itself rejects an unbalanced source.
//
// ---- HAZARD (D): GameEvents ARE GLOBAL, THE RECORDER IS PER-STREAM ----
// Routing is `BlackBoxRecorder.EmitForVessel`, which finds the stream for the part's own vessel and
// falls back to the mission log rather than dropping the line. ⛔ Every handler body is wrapped: an
// exception thrown inside a KSP callback propagates into KSP's own dispatch and can take the flight
// scene with it, and this code runs at the exact moment the vehicle is coming apart.
//
// ---- HAZARD (E): AT RUD, ORDER IS THE ENTIRE VALUE ----
// Events are written SYNCHRONOUSLY, in the order the callbacks arrive. ⛔ There is deliberately no
// queue: a queue drained in `FixedUpdate` would lose the tail of a cascade that ends the scene in the
// same frame, and would let a later sort or dedupe destroy the one thing worth having — the FIRST
// line. The flood guard is `PartLossBudget`, which keeps the HEAD of the sequence and announces the
// cut with `part.loss_truncated` exactly once (see `pure/blackbox/PartLoss.cs`).
// ============================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen.BlackBox
{
    /// <summary>
    /// Subscribes the part-loss channel for one flight scene. Static because `GameEvents` is static
    /// and because the recorder it feeds is; instance state would only invite two of them.
    /// </summary>
    internal static class PartLossWatch
    {
        const string Tag = "[DragonScreen/BlackBox] ";

        /// <summary>
        /// ⛔ The flood cap (hazard E). Comfortably above any plausible part count for this vehicle, so
        /// a REAL total break-up is recorded whole and nothing is truncated in the case that matters;
        /// present so a pathological loop cannot fill a disk. `[Tunable]` — `Tuning` hot-reloads it
        /// from `PluginData/tuning.cfg` at 1 Hz, so a flight that hits it can be re-flown with a
        /// bigger budget and no rebuild.
        /// </summary>
        [Tunable] public static int MaxPartLossEvents = 200;

        static bool subscribed;

        // ---- the discriminator's raw material (hazard B) ----------------------------------------
        // ⛔ These exist so `PartLoss.Classify` can be PURE. The judgement — "was that part supposed to
        // go?" — is the one thing in this channel worth testing, and a judgement that can only be
        // exercised by crashing a rocket is one nobody ever checks. So the glue collects facts and the
        // pure layer rules on them.
        static double lastStageCommandUt = double.NaN;
        static readonly Dictionary<uint, double> decoupledAt = new Dictionary<uint, double>();
        static readonly HashSet<uint> failedParts = new HashSet<uint>();
        static readonly HashSet<uint> jointBrokenParts = new HashSet<uint>();
        static readonly HashSet<uint> tearingDown = new HashSet<uint>();

        static PartLossBudget budget = new PartLossBudget(200);

        // ============================== lifecycle ==============================

        /// <summary>
        /// Called from `BlackBoxAddon.Start()`. Idempotent: a second call while subscribed does
        /// nothing, so a duplicated addon cannot double-register a handler and double every event.
        /// </summary>
        public static void Subscribe()
        {
            if (subscribed) return;
            try
            {
                GameEvents.onPartWillDie.Add(OnPartWillDie);
                GameEvents.onPartFailure.Add(OnPartFailure);
                GameEvents.onPartJointBreak.Add(OnPartJointBreak);
                GameEvents.onStageActivate.Add(OnStageActivate);
                GameEvents.onPartDeCouple.Add(OnPartDeCouple);
                GameEvents.onPartUndock.Add(OnPartUndock);
                GameEvents.onVesselWillDestroy.Add(OnVesselWillDestroy);
                GameEvents.onVesselUnloaded.Add(OnVesselUnloaded);
                subscribed = true;
            }
            catch (Exception e)
            {
                // ⛔ A PARTIAL SUBSCRIPTION IS THE WORST OUTCOME, so it is undone rather than left.
                // `subscribed` is still false here, and `Unsubscribe` removes unconditionally —
                // removing a handler that was never added is a no-op in `EventData`, so this is safe
                // and leaves nothing dangling.
                Debug.LogWarning(Tag + "part-loss watch could not subscribe: " + e.Message);
                Unsubscribe();
            }
            Reset();
        }

        /// <summary>
        /// Called from `BlackBoxAddon.OnDestroy()`. ⛔ EVERY `.Add` ABOVE HAS ITS `.Remove` HERE, in the
        /// same order, and `PartLossTest` fails the build if they ever stop matching.
        /// </summary>
        public static void Unsubscribe()
        {
            try
            {
                GameEvents.onPartWillDie.Remove(OnPartWillDie);
                GameEvents.onPartFailure.Remove(OnPartFailure);
                GameEvents.onPartJointBreak.Remove(OnPartJointBreak);
                GameEvents.onStageActivate.Remove(OnStageActivate);
                GameEvents.onPartDeCouple.Remove(OnPartDeCouple);
                GameEvents.onPartUndock.Remove(OnPartUndock);
                GameEvents.onVesselWillDestroy.Remove(OnVesselWillDestroy);
                GameEvents.onVesselUnloaded.Remove(OnVesselUnloaded);
            }
            catch (Exception e) { Debug.LogWarning(Tag + "part-loss watch unsubscribe: " + e.Message); }
            subscribed = false;
            Reset();
        }

        /// <summary>Drop every scene-scoped fact. A new scene is a new vehicle and a new budget.</summary>
        static void Reset()
        {
            lastStageCommandUt = double.NaN;
            decoupledAt.Clear();
            failedParts.Clear();
            jointBrokenParts.Clear();
            tearingDown.Clear();
            budget = new PartLossBudget(MaxPartLossEvents);
        }

        // ============================== the commanded side ==============================
        // ⛔⛔ S235 / NTSB-2026-003 F-308 — THIS BLOCK'S HEADER USED TO READ, VERBATIM:
        //     // Nothing below writes an event. These handlers exist ONLY to record that the vehicle was
        //     // TOLD to do something, so that a death a moment later can be told apart from one nobody
        //     // ordered.
        // SUPERSEDED IN PLACE (C1.16 / G12), and kept because the sentence IS the defect. S227 stated
        // "nothing below writes an event" as though it were a virtue of the design. It was not: it meant
        // every one of these observations existed only to serve a LATER `part.lost`, and `part.lost`
        // fires only from `onPartWillDie`.
        //
        // ⛔ ON FLIGHT 003, AT MET 139.28, THE SIXTEEN LAUNCH-VEHICLE PARTS DID NOT DIE — THEY BECAME A
        // NEW VESSEL. No part died, so no `part.lost` was written, so this whole block's work was
        // collected and thrown away at the exact moment it mattered. The root cause of NTSB-2026-003 is
        // still unresolved, every software agent was eliminated with citations, and the owner eliminated
        // the last branch himself (*"i did not press space"*) — and the recorder could not say whether
        // `StageManager.ActivateNextStage()` had run, **because it had been handed the stage number and
        // kept only a timestamp.**
        //
        // ⭐ SO THEY STILL DO THEIR CLASSIFICATION JOB — that is unchanged and `part.lost` still reads
        // exactly the same discriminator — and they now ALSO WRITE WHAT THEY WERE HANDED. The two are
        // independent: the classification serves a death that may never come, the record serves the
        // reader either way.

        /// <summary>
        /// ⭐⭐ THE ONE `int` THE INVESTIGATION TURNED ON. Emitting it distinguishes "something called
        /// `ActivateNextStage()`" from "the parts left without a stage command", which is the whole
        /// question of NTSB-2026-003.
        /// ⛔ It is emitted through `EmitMission`, NOT through `Emit`: a stage command is a fact about the
        /// VEHICLE, not about a part, and it must never be charged to the part-loss budget — the budget
        /// exists so a cascade keeps the head of its own sequence, and spending it on stage commands
        /// would truncate the very evidence it protects.
        /// </summary>
        static void OnStageActivate(int stage)
        {
            try
            {
                lastStageCommandUt = Now();   // ⭐ UNCHANGED — the classifier still needs this.
                if (!BlackBoxRecorder.EventLogOpen) return;
                BlackBoxRecorder.EmitMission(BlackBoxEvents.StageActivateCalled, lastStageCommandUt,
                    new[] { Kv.Int("stage", stage) });
            }
            catch (Exception e) { Warn("onStageActivate", e); }
        }

        static void OnPartDeCouple(Part p) { NoteReleased(p, BlackBoxEvents.PartDecoupled); }
        static void OnPartUndock(Part p)   { NoteReleased(p, BlackBoxEvents.PartUndocked); }

        /// <summary>
        /// ⭐ THE ONE PIECE OF EVIDENCE STRONG ENOUGH FOR `commanded`: KSP saying THIS PART was released
        /// on purpose. Keyed on `persistentId` and not on the object, because the object is about to be
        /// destroyed and a dictionary holding it would keep a dead `Part` alive.
        /// </summary>
        static void NoteReleased(Part p, string kind)
        {
            try
            {
                if (p == null) return;
                decoupledAt[p.persistentId] = Now();   // ⭐ UNCHANGED — the classifier still needs this.
                // ⭐⭐ S235 / F-308. And now it is RECORDED as well as remembered. Before this line, a
                // release that killed nothing left no trace whatever: the timestamp was kept so a later
                // `part.lost` could be classified `commanded`, and if no part ever died — which is
                // exactly what happened on flight 003 — the observation was discarded unseen.
                // ⛔ Through `Emit`, so it carries the full part identity under `craftdump.csv`'s own key
                // names and is charged to the part budget like every other part-scoped event.
                if (!BlackBoxRecorder.EventLogOpen) return;
                Emit(kind, p, null);
            }
            catch (Exception e) { Warn("onPartDeCouple/onPartUndock", e); }
        }

        static void OnVesselWillDestroy(Vessel v) { NoteTeardown(v); }
        static void OnVesselUnloaded(Vessel v)    { NoteTeardown(v); }

        /// <summary>
        /// The whole vessel is going away. Every part on it is about to die and NONE of those deaths is
        /// a failure — without this, one scene change would emit a vessel's worth of `uncommanded`
        /// verdicts and manufacture exactly the false cascade this channel exists to see through.
        /// </summary>
        static void NoteTeardown(Vessel v)
        {
            try { if (v != null) tearingDown.Add(v.persistentId); }
            catch { }
        }

        // ============================== the loss side ==============================

        static void OnPartFailure(Part p)
        {
            try
            {
                if (p == null || !BlackBoxRecorder.EventLogOpen) return;
                // Remembered so the `onPartWillDie` that follows classifies as `ksp_failure` rather
                // than re-deriving it — the game already told us, and an inference that contradicts a
                // statement is worse than no inference.
                failedParts.Add(p.persistentId);
                Emit(BlackBoxEvents.PartFailure, p, null);
            }
            catch (Exception e) { Warn("onPartFailure", e); }
        }

        /// <summary>
        /// ⭐ The "unzip" the owner described — the joint holding two parts together let go, carrying the
        /// force it broke at. ⛔ NOT a duplicate of `part.lost`: a joint break need not kill either part,
        /// so it is frequently the FIRST line of a sequence and the one a death-only channel misses.
        /// </summary>
        static void OnPartJointBreak(PartJoint j, float breakForce)
        {
            try
            {
                if (j == null || !BlackBoxRecorder.EventLogOpen) return;
                Part child = j.Child, parent = j.Parent;
                if (child != null) jointBrokenParts.Add(child.persistentId);
                Emit(BlackBoxEvents.PartJointBreak, child, new List<Kv> {
                    Kv.Num("break_force", breakForce),
                    Kv.Str("joint_parent", parent == null ? "" : PartNames.Of(parent)),
                    Kv.Num("joint_parent_id", parent == null ? double.NaN : parent.persistentId),
                });
            }
            catch (Exception e) { Warn("onPartJointBreak", e); }
        }

        /// <summary>
        /// The main channel. `onPartWillDie` rather than `onPartDie` so the part's own identity is still
        /// readable — see the file header.
        /// </summary>
        static void OnPartWillDie(Part p)
        {
            try
            {
                if (p == null || !BlackBoxRecorder.EventLogOpen) return;

                PartLossFacts f;
                f.Ut = Now();
                f.LastStageCommandUt = lastStageCommandUt;
                f.LastDecoupleUt = Lookup(decoupledAt, p.persistentId);
                f.VesselTeardown = p.vessel != null && tearingDown.Contains(p.vessel.persistentId);
                f.ArrivedAsFailure = failedParts.Contains(p.persistentId);
                f.ArrivedAsJointBreak = jointBrokenParts.Contains(p.persistentId);

                PartLossVerdict v = PartLoss.Classify(f);
                Emit(BlackBoxEvents.PartLost, p, new List<Kv> {
                    Kv.Str("loss_class", PartLoss.Name(v.Class)),
                    Kv.Str("why", v.Why),
                    Kv.Num("since_stage_s", v.SinceStageS),
                    Kv.Num("since_decouple_s", v.SinceDecoupleS),
                    Kv.Bit("teardown", f.VesselTeardown),
                });
            }
            catch (Exception e) { Warn("onPartWillDie", e); }
        }

        // ============================== emit ==============================

        /// <summary>
        /// One event, budgeted and routed. ⛔ The budget is checked HERE and not per kind, so a cascade
        /// cannot exhaust the file through one channel while another keeps writing; and the truncation
        /// marker is emitted through `EmitMission` (not `Emit`) so it can never itself be budgeted away.
        /// </summary>
        static void Emit(string kind, Part p, List<Kv> extra)
        {
            double ut = Now();
            if (!budget.Admit())
            {
                if (budget.TakeMarker())
                    BlackBoxRecorder.EmitMission(BlackBoxEvents.PartLossTruncated, ut, new[] {
                        Kv.Int("truncated_after", budget.Cap),
                        Kv.Int("seen", budget.Seen),
                    });
                return;
            }

            List<Kv> kv = new List<Kv>(10);
            // ⭐ `craftdump.csv`'s OWN key names (`src/CraftDump.cs`) so an event joins to a named part
            // with no lookup table and no translation layer.
            kv.Add(Kv.Str("part_name", PartNames.Of(p)));
            kv.Add(Kv.Str("part_title", p != null && p.partInfo != null ? p.partInfo.title : ""));
            kv.Add(Kv.Num("persistent_id", p == null ? double.NaN : p.persistentId));
            // ⚠ `part_idx` IS THE LIVE INDEX AT THE MOMENT OF LOSS, AND THAT IS NOT `craftdump.csv`'s
            // PAD INDEX. The dump is written once on the pad; `Vessel.parts` compacts every time a part
            // leaves, so by the third staging the two have diverged. ⛔ **`persistent_id` is the join
            // key.** `part_idx` is recorded because it is what an analyst sees in the game's own part
            // list at that instant, and it is labelled here so nobody joins on it by accident.
            kv.Add(Kv.Int("part_idx", IndexOf(p)));
            kv.Add(Kv.Int("stage", p == null ? -1 : p.inverseStage));
            kv.Add(Kv.Str("parent", p != null && p.parent != null ? PartNames.Of(p.parent) : ""));
            kv.Add(Kv.Num("parent_id", p != null && p.parent != null ? p.parent.persistentId : double.NaN));
            if (extra != null) kv.AddRange(extra);

            BlackBoxRecorder.EmitForVessel(p == null ? null : p.vessel, kind, ut, kv.ToArray());
        }

        // ============================== small helpers ==============================

        static int IndexOf(Part p)
        {
            try
            {
                if (p == null || p.vessel == null || p.vessel.parts == null) return -1;
                return p.vessel.parts.IndexOf(p);
            }
            catch { return -1; }
        }

        static double Lookup(Dictionary<uint, double> d, uint key)
        {
            double v;
            return d.TryGetValue(key, out v) ? v : double.NaN;
        }

        static double Now()
        {
            try { return Planetarium.GetUniversalTime(); }
            catch { return double.NaN; }
        }

        static void Warn(string where, Exception e)
        {
            // ⛔ A LOG LINE, NEVER A RETHROW. This runs inside KSP's own event dispatch at the moment
            // the vehicle is coming apart; an exception escaping here can take the flight scene with
            // it, and losing the scene would lose the recording this channel exists to make.
            Debug.LogWarning(Tag + "part-loss watch: " + where + " failed: " + e.Message);
        }
    }
}
