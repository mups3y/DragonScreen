// DragonScreen — MechAscentReadback  (GLUE: the value MechJeb is ACTUALLY holding, field by field)
// ============================================================================================
// Register S223 JOB 1. The other half of `pure/AscentReadback.cs`: that file says what each box
// SHOULD hold and judges the answer; this file is the only thing in the mod that ASKS.
//
// ---- ⛔⛔ IT READS. IT DOES NOT WRITE. ----
// Owner, 2026-09-08, verbatim: *"let native mechjeb do it AND THEN WE TUNE FROM TRUSTED CAPTURED
// VALUES!!!"* — an instrument is not a tune. **There is not one assignment to a MechJeb field in this
// file**, and `AscentReadbackTest` asserts that by parsing the source: any `a.<Field> =` or
// `.Val =` here fails the build. That check is not paranoia, it is the whole licence this task had to
// run before the reference flight.
//
// ---- WHY A LIVE READ AT ALL, WHEN THE AUDIT ALREADY SAYS WHAT WE FLY ----
// Because the audit was computed from our own intent and had never once been compared with the
// vehicle. The mod's own `ApplyTune()` logged success three times against a cfg carrying
// `PitchRate 0.75` for `MechJebModuleAscentSettings`, and the vehicle flew ~5 deg/s — RO's
// `PITCH_RATE_DEFAULT`. Both statements were in the same log file and nothing could reconcile them,
// because "what the module holds" was never asked. ⛔ **AND S223 DOES NOT ASSERT WHY.** The ordering
// between `MechJebCore.OnStart`'s module pass (which runs `ApplyRODefaults`) and
// `DragonMechJebCore.OnStart`'s `ApplyTune()` is READABLE in the source and is still not evidence
// about what a field ends up holding — a `readonly EditableDouble` behind a `Pass.TYPE` load has more
// than one way to not move. So this reads the values and prints them, at two points, with the delta.
// Either answer is a result; a guess is not.
//
// ---- THE TWO POINTS, AND WHY TWO ----
//   (a) immediately after `Configure` finishes — what the conductor left behind;
//   (b) at the terminal count (T-10 s) — the last moment before anything is lit.
// ⭐ THE DELTA IS THE EXPERIMENT. Empty means nothing re-seeded the module after us, which is a fact
// worth having; non-empty NAMES the box and both values, which is a better one.
// ============================================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using MuMech = DragonScreen.Mech.MuMech;

namespace DragonScreen
{
    /// <summary>
    /// Reads every box `AscentProfile` decides about out of the LIVE `MechJebModuleAscentSettings`
    /// (plus `Core.Thrust`, `Core.Node`, `Core.Warp`), and hands the readings to the pure comparator.
    /// </summary>
    public static class MechAscentReadback
    {
        /// <summary>The reading taken immediately after `Configure`. Null until one is taken.</summary>
        public static AscentObserved[] AtConfigure { get; private set; }

        /// <summary>The reading taken at the terminal count. Null until one is taken.</summary>
        public static AscentObserved[] AtTerminalCount { get; private set; }

        /// <summary>The most recent reading of either kind — what the manifest records if the recorder
        /// opens after the conductor has configured.</summary>
        public static AscentObserved[] Latest { get; private set; }

        /// <summary>Cleared with the conductor, so a reverted flight cannot inherit the last one's
        /// numbers and present them as this one's (the same rule `MechConductor.Reset` applies to
        /// every other latch).</summary>
        public static void Reset()
        {
            AtConfigure = null; AtTerminalCount = null; Latest = null;
        }

        // =========================================================================================
        //  THE READ
        // =========================================================================================

        /// <summary>
        /// Every audited box, as the core holds it RIGHT NOW. ⛔ A field that throws comes back
        /// `Unreadable` with the reason, never as a zero — "could not be read" and "holds 0" are
        /// different facts and a flight recorder that conflates them is worse than one with a hole.
        /// </summary>
        public static AscentObserved[] Read(DragonMechJebCore core)
        {
            var obs = new List<AscentObserved>(AscentReadback.Expected.Length);
            if (core == null)
            {
                for (int i = 0; i < AscentReadback.Expected.Length; i++)
                    obs.Add(AscentObserved.Unreadable(AscentReadback.Expected[i].Name, "no core is bound"));
                return obs.ToArray();
            }

            MuMech.MechJebModuleAscentSettings a = null;
            try { a = core.AscentSettings; } catch { }
            if (a == null)
            {
                for (int i = 0; i < AscentReadback.Expected.Length; i++)
                    obs.Add(AscentObserved.Unreadable(AscentReadback.Expected[i].Name,
                                                      "the core has no AscentSettings module"));
                return obs.ToArray();
            }

            // ---- the eight we write ------------------------------------------------------------
            Word(obs, "AscentType", delegate { return a.AscentType.ToString(); });
            Flag(obs, "Autostage", delegate { return a.Autostage; });
            Num (obs, "WarpCountDown", delegate { return (double)a.WarpCountDown.Val; });
            Flag(obs, "SkipCircularization", delegate { return a.SkipCircularization; });
            Flag(obs, "AutoDeploySolarPanels", delegate { return a.AutoDeploySolarPanels; });
            Flag(obs, "AutoDeployAntennas", delegate { return a.AutoDeployAntennas; });
            Flag(obs, "Core.Node.Autowarp", delegate { return core.Node.Autowarp; });
            Flag(obs, "Core.Warp.activateSASOnWarp", delegate { return core.Warp.activateSASOnWarp; });

            // ---- the destination ----------------------------------------------------------------
            Num (obs, "DesiredOrbitAltitude", delegate { return a.DesiredOrbitAltitude.Val; });
            Num (obs, "DesiredApoapsis", delegate { return a.DesiredApoapsis.Val; });
            Num (obs, "DesiredInclination", delegate { return a.DesiredInclination.Val; });
            Flag(obs, "LaunchingToPlane", delegate { return a.LaunchingToPlane; });

            // ---- UI-derived ----------------------------------------------------------------------
            Flag(obs, "LimitQaEnabled", delegate { return a.LimitQaEnabled; });
            Flag(obs, "OptimizeStageFlag", delegate { return a.OptimizeStageFlag; });

            // ---- ⛔ the two awaiting the owner ---------------------------------------------------
            Num (obs, "PitchRate", delegate { return a.PitchRate.Val; });
            Flag(obs, "Core.Thrust.LimitDynamicPressure", delegate { return core.Thrust.LimitDynamicPressure; });

            // ---- the attach altitude --------------------------------------------------------------
            Num (obs, "DesiredAttachAltFixed", delegate { return a.DesiredAttachAltFixed.Val; });
            Num (obs, "DesiredAttachAlt", delegate { return a.DesiredAttachAlt.Val; });
            Flag(obs, "AttachAltFlag", delegate { return a.AttachAltFlag; });
            Num (obs, "DesiredFPA", delegate { return a.DesiredFPA.Val; });
            Flag(obs, "DesiredArgPFlag", delegate { return a.DesiredArgPFlag; });
            Num (obs, "DesiredArgP", delegate { return a.DesiredArgP.Val; });

            // ---- the plane launch's flags -----------------------------------------------------------
            Num (obs, "LaunchLANDifference", delegate { return a.LaunchLANDifference.Val; });
            Flag(obs, "LaunchingToMatchLan", delegate { return a.LaunchingToMatchLan; });
            Flag(obs, "LaunchingToLan", delegate { return a.LaunchingToLan; });
            Num (obs, "DesiredLan", delegate { return a.DesiredLan.Val; });
            Flag(obs, "RelativeLAN", delegate { return a.RelativeLAN; });
            Flag(obs, "OverrideWarpToPlane", delegate { return a.OverrideWarpToPlane; });

            // ---- the pitch program -------------------------------------------------------------------
            Num (obs, "PitchStartHeight", delegate { return a.PitchStartHeight.Val; });
            Flag(obs, "CorrectiveSteering", delegate { return a.CorrectiveSteering; });
            Num (obs, "CorrectiveSteeringGain", delegate { return a.CorrectiveSteeringGain.Val; });

            // ---- roll ----------------------------------------------------------------------------------
            Flag(obs, "ForceRoll", delegate { return a.ForceRoll; });
            Num (obs, "VerticalRoll", delegate { return a.VerticalRoll.Val; });
            Num (obs, "TurnRoll", delegate { return a.TurnRoll.Val; });
            Num (obs, "RollAltitude", delegate { return a.RollAltitude.Val; });

            // ---- the AoA / q-alpha limiters ---------------------------------------------------------
            Num (obs, "LimitQa", delegate { return a.LimitQa.Val; });
            Flag(obs, "LimitAoA", delegate { return a.LimitAoA; });
            Num (obs, "MaxAoA", delegate { return a.MaxAoA.Val; });
            Num (obs, "AOALimitFadeoutPressure", delegate { return a.AOALimitFadeoutPressure.Val; });
            Flag(obs, "LimitingAoA", delegate { return a.LimitingAoA; });
            // ⭐⭐ S235 JOB 4. ⚠ NOT on `a` (AscentSettings) — `AutostageLimit` lives on the STAGING
            // CONTROLLER, which is why it was missed: every other box in this table comes off one
            // module and nobody looked at a second one. Guarded independently: a null Staging module
            // must cost this ONE row its reading, not the other 66 theirs.
            Num (obs, "AutostageLimit", delegate {
                return core.Staging == null ? double.NaN : (double)core.Staging.AutostageLimit.Val; });

            // ---- the PSG stage model ------------------------------------------------------------------
            Num (obs, "MinDeltaV", delegate { return a.MinDeltaV.Val; });
            Num (obs, "LastStage", delegate { return (double)a.LastStage.Val; });
            Num (obs, "MaxCoast", delegate { return a.MaxCoast.Val; });
            Num (obs, "MinCoast", delegate { return a.MinCoast.Val; });
            Flag(obs, "CoastStageFlag", delegate { return a.CoastStageFlag; });
            Num (obs, "CoastStageInternal", delegate { return (double)a.CoastStageInternal.Val; });
            Num (obs, "CoastLocation", delegate { return (double)a.CoastLocation; });
            Flag(obs, "SpinupStageFlag", delegate { return a.SpinupStageFlag; });
            Num (obs, "SpinupStageInternal", delegate { return (double)a.SpinupStageInternal.Val; });
            Num (obs, "SpinupLeadTime", delegate { return a.SpinupLeadTime.Val; });
            Num (obs, "SpinupAngularVelocity", delegate { return a.SpinupAngularVelocity.Val; });
            Flag(obs, "UnguidedStagesFlag", delegate { return a.UnguidedStagesFlag; });
            // ⚠ THE LISTS ARE READ AS THEIR ENTRY COUNT, and the expectation table says so. A list
            // rendered as its contents would make an empty list and an unreadable one look alike in a
            // log, and 0 is a number the comparator can judge.
            Num (obs, "UnguidedStagesInternal", delegate { return (double)a.UnguidedStagesInternal.Val.Count; });
            Num (obs, "UnguidedStages", delegate { return (double)a.UnguidedStages.Count; });
            Flag(obs, "FixedStagesFlag", delegate { return a.FixedStagesFlag; });
            Num (obs, "FixedStagesInternal", delegate { return (double)a.FixedStagesInternal.Val.Count; });
            Num (obs, "FixedStages", delegate { return (double)a.FixedStages.Count; });
            Num (obs, "PreStageTime", delegate { return a.PreStageTime.Val; });
            Num (obs, "OptimizerPauseTime", delegate { return a.OptimizerPauseTime.Val; });
            Num (obs, "Cd", delegate { return a.Cd.Val; });
            Num (obs, "Aref", delegate { return a.Aref.Val; });

            // ---- the thrust controller -------------------------------------------------------------
            Num (obs, "Core.Thrust.MaxDynamicPressure", delegate { return core.Thrust.MaxDynamicPressure.Val; });
            Num (obs, "Core.Thrust.MinThrottle", delegate { return core.Thrust.MinThrottle.Val; });
            Flag(obs, "Core.Thrust.LimiterMinThrottle", delegate { return core.Thrust.LimiterMinThrottle; });
            Flag(obs, "Core.Thrust.LimitToPreventUnstableIgnition",
                 delegate { return core.Thrust.LimitToPreventUnstableIgnition; });
            Flag(obs, "Core.Thrust.AutoRCSUllaging", delegate { return core.Thrust.AutoRCSUllaging; });
            Flag(obs, "Core.Thrust.LimitThrottle", delegate { return core.Thrust.LimitThrottle; });
            Flag(obs, "Core.Thrust.LimitAcceleration", delegate { return core.Thrust.LimitAcceleration; });
            Flag(obs, "Core.Thrust.LimitToPreventOverheats", delegate { return core.Thrust.LimitToPreventOverheats; });

            // ---- the CLASSIC path: unread by the PSG solver, and read back anyway -------------------
            Num (obs, "TurnStartAltitude", delegate { return a.TurnStartAltitude.Val; });
            Num (obs, "TurnStartVelocity", delegate { return a.TurnStartVelocity.Val; });
            Num (obs, "TurnEndAltitude", delegate { return a.TurnEndAltitude.Val; });
            Num (obs, "TurnEndAngle", delegate { return a.TurnEndAngle.Val; });
            Num (obs, "TurnShapeExponent", delegate { return a.TurnShapeExponent.Val; });
            Flag(obs, "AutoPath", delegate { return a.AutoPath; });
            Num (obs, "AutoTurnPerc", delegate { return (double)a.AutoTurnPerc; });
            Num (obs, "AutoTurnSpdFactor", delegate { return (double)a.AutoTurnSpdFactor; });

            return obs.ToArray();
        }

        // ---- the three readers. Each one catches, because a single throwing field must not cost the
        // ---- other seventy-six their reading. ----------------------------------------------------
        delegate double NumF();
        delegate bool BoolF();
        delegate string WordF();

        static void Num(List<AscentObserved> obs, string name, NumF f)
        {
            try { obs.Add(AscentObserved.Num(name, f())); }
            catch (Exception e) { obs.Add(AscentObserved.Unreadable(name, e.GetType().Name + ": " + e.Message)); }
        }

        static void Flag(List<AscentObserved> obs, string name, BoolF f)
        {
            try { obs.Add(AscentObserved.Flag(name, f())); }
            catch (Exception e) { obs.Add(AscentObserved.Unreadable(name, e.GetType().Name + ": " + e.Message)); }
        }

        static void Word(List<AscentObserved> obs, string name, WordF f)
        {
            try { obs.Add(AscentObserved.Word(name, f())); }
            catch (Exception e) { obs.Add(AscentObserved.Unreadable(name, e.GetType().Name + ": " + e.Message)); }
        }

        // =========================================================================================
        //  THE TWO CALL SITES
        // =========================================================================================

        /// <summary>
        /// Reading (a): what the conductor left behind. Taken and STASHED here, logged by the caller,
        /// because `Configure`'s own "PVG configured" line carries this reading's headline — the CLAIM
        /// and the MEASUREMENT are never more than one line apart, which needs the reading in hand
        /// before that line is built.
        /// </summary>
        public static AscentObserved[] TakeAtConfigure(DragonMechJebCore core)
        {
            AtConfigure = Read(core);
            Latest = AtConfigure;
            return AtConfigure;
        }

        /// <summary>
        /// Reading (b): the last moment before anything is lit, and the DELTA against (a). ⭐ The delta
        /// is the experiment — see this file's header.
        /// </summary>
        public static void TakeAtTerminalCount(DragonMechJebCore core)
        {
            AtTerminalCount = Read(core);
            Latest = AtTerminalCount;
            LogTable("at the TERMINAL COUNT, the last moment before ignition", AtTerminalCount);
            if (AtConfigure != null)
                Debug.Log("[DragonScreen] conductor: " + AscentReadback.Delta(AtConfigure, AtTerminalCount)
                          + " (S223)");
        }

        /// <summary>The whole table, in `KSP.log`, one line per box.</summary>
        public static void LogTable(string when, AscentObserved[] obs)
        {
            try
            {
                Debug.Log("[DragonScreen] conductor: "
                          + AscentReadback.Render(when, AscentReadback.Verdicts(obs)) + "\n    (S223)");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DragonScreen] conductor: the ascent read-back could not be rendered: "
                                 + e.Message);
            }
        }

        /// <summary>
        /// The one-line verdict appended to `AscentProfile.Render()`'s headline, so the audit's claim
        /// cannot appear in the log without the measurement beside it. ⛔ It can go RED — that is the
        /// point, and `AscentReadbackTest` proves it by seeding a divergence.
        /// </summary>
        public static string HeadlineOf(AscentObserved[] obs)
        {
            try { return AscentReadback.Headline(AscentReadback.Verdicts(obs)); }
            catch (Exception e) { return "⛔ READ-BACK FAILED: " + e.Message; }
        }

        /// <summary>
        /// R-04: the audited ascent settings for the black-box manifest, READ LIVE — the same read as
        /// JOB 1's, taken once more at recorder-open rather than duplicated. Falls back to the most
        /// recent flight reading when no core is bound yet, and says so per row when there is neither.
        /// </summary>
        public static List<string> ManifestLines(DragonMechJebCore core)
        {
            if (core != null) return AscentReadback.ManifestLines(Read(core));
            if (Latest != null) return AscentReadback.ManifestLines(Latest);
            return AscentReadback.ManifestLines(null);
        }
    }
}
