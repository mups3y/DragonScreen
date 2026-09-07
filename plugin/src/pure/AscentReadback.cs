// DragonScreen — AscentReadback  (PURE: what MechJeb SHOULD be holding, and the verdict on what it IS)
// ============================================================================================
// Register S223 JOB 1. The defect, stated by the owner's brief of 2026-09-08:
//
//     `MechConductor.Configure` logs `AscentProfile.Render()` — "ASCENT SETTINGS AUDIT — 77 boxes:
//     8 written, ... 53 left at the RO default ...". **That sentence is computed from our own
//     intent. Nothing in it was ever read out of MechJeb.**
//
// ⛔ AND THE FLIGHT SAYS THE CLAIM IS NOT SAFE TO TRUST. The mod's own `ApplyTune()` reported success
// three times against `mechjeb_settings_type_Crew-Dragon.cfg`, which carries `PitchRate 0.75` for
// `MechJebModuleAscentSettings` — and the vehicle flew a mean 4.04 deg/s, i.e. RO's `PITCH_RATE_DEFAULT`
// of 5.0. A claim and a measurement disagreed and NOTHING IN THE SYSTEM COULD SAY SO, because there was
// no measurement. This file is the measurement's other half.
//
// ---- ⭐ WHAT THIS FILE IS, IN ONE SENTENCE ----
// `AscentProfile` is the CLAIM (one row per box: what we decided and why). This is the EXPECTATION
// (one row per box: the value that decision should produce, as a NUMBER, with the line of vendored
// source it comes from) plus the VERDICT machinery that compares it to a live reading the glue takes
// out of the running core. Three files, three jobs: decide · expect · read.
//
// ---- ⛔⛔ THIS FILE WRITES NOTHING TO MECHJEB, EVER, AND MAY NOT BE READ AS A TUNE ----
// Owner, 2026-09-08, verbatim: *"let native mechjeb do it AND THEN WE TUNE FROM TRUSTED CAPTURED
// VALUES!!!"*. **An instrument is not a tune.** Nothing here is ever assigned to a MechJeb field —
// there is no MechJeb reference in this assembly to assign to. Changing a number in `Expected` changes
// exactly one thing: whether the instrument calls a row a DISAGREEMENT. It cannot change a flown value,
// and a task that edits one of these numbers to silence a red line has broken the instrument rather
// than fixed the vehicle.
//
// ---- ⚠ WHY IT IS PURE, WHEN S223's OWN BRIEF SAYS "IT DOES NOT GO IN plugin/src/pure/" ----
// That instruction, in full: *"That tree is pure and must stay pure; it cannot see a MechJeb core. The
// read-back belongs in `MechConductor` (or a new non-pure helper)."* The READ — the part that needs a
// `MechJebModuleAscentSettings` — is exactly there: `src/MechAscentReadback.cs`, glue, MechJeb-facing.
// What is here is the half that touches no MechJeb type at all: a table of names and numbers, and the
// comparison over them. It is here because S223's own DONE-WHEN requires *"a headless assertion that
// the read-back reports a disagreement when one exists"*, and `plugin/build.py test` builds
// `src/pure` + `test` ONLY (see `build_tests`). A comparator in the glue could not be asserted at all,
// which is the S130 failure the brief names three times: a green that cannot go red.
//
// ---- THE THREE THINGS AN EXPECTATION ROW HAS TO CARRY ----
//  • the NUMBER (or word) the row should hold, in the unit the LIVE FIELD holds it in — not the unit
//    the audit's prose quotes. `DesiredAttachAltFixed` reads 110000 and the audit says "110 km"; a
//    comparator that parsed the prose would have called that a disagreement on every flight.
//  • WHERE that number comes from, as a citation into the vendored source, so the expectation is
//    checkable by a reader without running anything.
//  • WHETHER it is checkable at all. Three classes are deliberately NOT checked and say so: a
//    MISSION FACT (the destination — known only from the profile in flight), a MENU-DERIVED box
//    (recomputed from the live stage table every tick), and a STATUS FLAG (the autopilot writes it).
//    An unchecked row is still READ and still PRINTED — "unchecked" is not "unrecorded".
// ============================================================================================
using System;
using System.Collections.Generic;

namespace DragonScreen
{
    /// <summary>Where an expected value comes from — and therefore what a disagreement MEANS.</summary>
    public enum ExpectSource : byte
    {
        /// <summary>`MechJebModuleAscentSettings.ApplyRODefaults()` seeds it. A disagreement means either
        /// RO's seed never ran, or something overwrote it after it did.</summary>
        RoDefault,
        /// <summary>MechJeb's own field initialiser; `ApplyRODefaults` does not touch it. A disagreement
        /// means a persisted cfg or a tune reached the field.</summary>
        FieldDefault,
        /// <summary>`MechConductor.Configure` writes it. A disagreement means OUR write did not land —
        /// which is the failure mode with no other symptom at all.</summary>
        OurWrite,
        /// <summary>⛔ NOT CHECKED. MechJeb's own menu recomputes it from the live stage table every
        /// frame (`AscentProfile.OptimizeStageFlagFor`), so there is no fixed value to expect.</summary>
        MenuDerived,
        /// <summary>⛔ NOT CHECKED. A destination, read from the mission profile at runtime. Recorded
        /// per row in the black box instead (`tgt_ap_km` / `tgt_pe_km` / `tgt_inc_deg`).</summary>
        MissionFact,
        /// <summary>⛔ NOT CHECKED. The autopilot writes it every `Drive`; it is ours to read.</summary>
        StatusFlag
    }

    /// <summary>One row of the expectation table.</summary>
    public struct AscentExpect
    {
        public string Name;
        public ExpectSource Source;
        /// <summary>False for the three unchecked classes — the row is read and printed, not judged.</summary>
        public bool Checkable;
        /// <summary>True when <see cref="Number"/> carries the expectation; false when <see cref="Text"/> does.</summary>
        public bool IsNumber;
        public double Number;
        public string Text;
        /// <summary>The vendored line the expectation comes from. A reader checks it without running anything.</summary>
        public string Cite;
    }

    /// <summary>
    /// One live reading, taken by the glue out of the running core. ⛔ `Read == false` is a FIRST-CLASS
    /// answer and never a zero: "the field could not be read" and "the field holds 0" are different
    /// facts, and §4.6's rule for the black box is this file's rule too.
    /// </summary>
    public struct AscentObserved
    {
        public string Name;
        public bool Read;
        public bool IsNumber;
        public double Number;
        public string Text;
        /// <summary>When `Read` is false: why not.</summary>
        public string Why;

        public static AscentObserved Num(string name, double v)
        {
            AscentObserved o = new AscentObserved();
            o.Name = name; o.Read = true; o.IsNumber = true; o.Number = v; o.Text = null; o.Why = null;
            return o;
        }

        public static AscentObserved Word(string name, string v)
        {
            AscentObserved o = new AscentObserved();
            o.Name = name; o.Read = true; o.IsNumber = false; o.Number = 0.0; o.Text = v; o.Why = null;
            return o;
        }

        public static AscentObserved Flag(string name, bool v) { return Word(name, v ? "true" : "false"); }

        public static AscentObserved Unreadable(string name, string why)
        {
            AscentObserved o = new AscentObserved();
            o.Name = name; o.Read = false; o.IsNumber = false; o.Number = 0.0; o.Text = null; o.Why = why;
            return o;
        }

        /// <summary>The value as it is printed and as it is recorded. "?" when it could not be read.</summary>
        public string Rendered()
        {
            if (!Read) return "?";
            return IsNumber ? AscentReadback.Fmt(Number) : (Text ?? "");
        }
    }

    /// <summary>One row's verdict: what MechJeb holds, what it should hold, and whether that is a fault.</summary>
    public struct ReadbackVerdict
    {
        public string Name;
        public ExpectSource Source;
        public string Live;
        public string Expected;
        public string Cite;
        /// <summary>The field could not be read at all.</summary>
        public bool Unread;
        /// <summary>Read, checkable, and NOT what its declared source produces. ⛔ The red line.</summary>
        public bool Disagrees;
        /// <summary>Read, but this class of row has no fixed expectation (see <see cref="ExpectSource"/>).</summary>
        public bool NotChecked;
    }

    public static class AscentReadback
    {
        // =========================================================================================
        // 1. THE EXPECTATION TABLE — one row per `AscentProfile.Audit` row, in the same order
        // =========================================================================================
        //
        // ⛔ EVERY NUMBER IS IN THE UNIT THE LIVE FIELD HOLDS, which is metres/pascals/seconds and NOT
        // the kilometres the audit's prose and MechJeb's own text boxes show. `EditableDoubleMult`'s
        // multiplier is a TEXT-BOX convenience (`new EditableDoubleMult(60000, 1000)` displays "60"
        // and holds 60000) — `.Val` is the held value, and `.Val` is what the glue reads.
        //
        // ⚠ WHERE `ApplyRODefaults()` ASSIGNS A FIELD TWICE, THE EXPECTATION IS THE **LAST** ASSIGNMENT,
        // because that is the value RO leaves behind. There are two, and the audit's own prose already
        // caught one of them (`Core.Thrust.MaxDynamicPressure`: 20000 then 50000). It did NOT catch the
        // other — see `DesiredAttachAlt` below, which is logged as a stray by S223 rather than edited
        // here, because the audit rows are the claim and a task does not quietly edit the claim it is
        // measuring (C1.1).

        static AscentExpect N(string name, ExpectSource src, double v, string cite)
        {
            AscentExpect e = new AscentExpect();
            e.Name = name; e.Source = src; e.Checkable = true; e.IsNumber = true; e.Number = v;
            e.Text = null; e.Cite = cite;
            return e;
        }

        static AscentExpect W(string name, ExpectSource src, string v, string cite)
        {
            AscentExpect e = new AscentExpect();
            e.Name = name; e.Source = src; e.Checkable = true; e.IsNumber = false; e.Number = 0.0;
            e.Text = v; e.Cite = cite;
            return e;
        }

        static AscentExpect B(string name, ExpectSource src, bool v, string cite)
        {
            return W(name, src, v ? "true" : "false", cite);
        }

        /// <summary>A row that is READ and PRINTED but has no fixed value to be judged against.</summary>
        static AscentExpect U(string name, ExpectSource src, string cite)
        {
            AscentExpect e = new AscentExpect();
            e.Name = name; e.Source = src; e.Checkable = false; e.IsNumber = false; e.Number = 0.0;
            e.Text = null; e.Cite = cite;
            return e;
        }

        public static readonly AscentExpect[] Expected =
        {
            // ---- the eight the conductor WRITES (AscentProfile's `Write` rows) ---------------------
            W("AscentType",            ExpectSource.OurWrite, "PSG",
              "MechConductor.Configure writes AscentType = PSG; ApplyRODefaults ends with the same value"),
            B("Autostage",             ExpectSource.OurWrite, false,
              "the owner's one sanctioned deviation — Configure writes the PROPERTY false; RO seeds true"),
            N("WarpCountDown",         ExpectSource.OurWrite, 32.0,
              "AscentProfile.WarpCountDownS = 20 (PSG cold start) + 12 (WarpPlan.BurnLeadS); MechJeb's own box default is 11"),
            B("SkipCircularization",   ExpectSource.OurWrite, true,
              "Configure writes true; MechJebModuleAscentSettings.cs field default is false"),
            B("AutoDeploySolarPanels", ExpectSource.OurWrite, false,
              "Configure writes false (§B12.7); MechJebModuleAscentSettings.cs field default is true"),
            B("AutoDeployAntennas",    ExpectSource.OurWrite, false,
              "Configure writes false (§B12.7); MechJebModuleAscentSettings.cs field default is true"),
            B("Core.Node.Autowarp",    ExpectSource.OurWrite, true,
              "Configure writes true; MechJebModuleNodeExecutor.cs:24 field default is also true"),
            B("Core.Warp.activateSASOnWarp", ExpectSource.OurWrite, false,
              "Configure writes false; MechJebModuleWarpController.cs:35 field default is true"),

            // ---- the destination: read and printed, never judged (the profile decides it) -----------
            U("DesiredOrbitAltitude", ExpectSource.MissionFact, "AscentTargets.For(profile).PeriapsisM, at runtime — recorded as tgt_pe_km"),
            U("DesiredApoapsis",      ExpectSource.MissionFact, "AscentTargets.For(profile).ApoapsisM, at runtime — recorded as tgt_ap_km"),
            U("DesiredInclination",   ExpectSource.MissionFact, "§7.5's plane launch on a rendezvous, else the mission fact — recorded as tgt_inc_deg"),
            U("LaunchingToPlane",     ExpectSource.MissionFact, "true from the crew's GO to T-0 only (MechJebModuleAscentMenu.cs:245-258)"),

            // ---- UI-derived: one has a determinate answer while AscentType is PSG, one does not -----
            B("LimitQaEnabled",   ExpectSource.MenuDerived, true,
              "MechJebModuleAscentMenu.cs:374 — `LimitQaEnabled = AscentType == PSG`, and AscentType is checked above, so TRUE is determinate"),
            U("OptimizeStageFlag", ExpectSource.MenuDerived,
              "MechJebModuleAscentPSGSettingsMenu.cs:63,83 recomputes it from the live stage table every frame"),

            // ---- ⛔ the two AWAITING THE OWNER. Both fly RO's default and the read-back proves it. ---
            // ⭐⭐ `PitchRate` IS THE ROW THIS WHOLE FILE EXISTS FOR. The shipped tune carries 0.75 for
            // this exact field and the vehicle flew ~5. The expectation here is RO's 5.0 because RO's
            // default is what the audit CLAIMS this row flies at — so if the tune ever does land, this
            // line goes RED and says so, instead of the disagreement being invisible for a second flight.
            N("PitchRate", ExpectSource.RoDefault, 5.0,
              "ApplyRODefaults: PitchRate.Val = PITCH_RATE_DEFAULT = 5.0 (MechJebModuleAscentSettings.cs)"),
            B("Core.Thrust.LimitDynamicPressure", ExpectSource.RoDefault, false,
              "ApplyRODefaults sets it false TWICE (the second at the end of the method)"),

            // ---- the attach altitude ---------------------------------------------------------------
            N("DesiredAttachAltFixed", ExpectSource.RoDefault, 110000.0,
              "ApplyRODefaults: DesiredAttachAltFixed.Val = DESIRED_ATTACH_ALT_DEFAULT = 110000"),
            // ⚠⚠ 145000, NOT THE 110000 THE AUDIT ROW's PROSE SAYS — and this is not a typo here.
            // `ApplyRODefaults` assigns this field TWICE: `DesiredAttachAlt.Val = DESIRED_ATTACH_ALT_DEFAULT`
            // near the top and `DesiredAttachAlt.Val = 145000` alongside `DesiredOrbitAltitude.Val = 145000`
            // further down. The LAST one is what RO leaves. `AscentProfile`'s row says "110 km (RO)" and is
            // wrong about RO's own seed; S223 logs that as a stray rather than editing the claim it is
            // measuring. ⛔ The value is UNREAD on our path either way (AttachAltFlag is RO's false), so
            // nothing about the flight changes — what changes is that the instrument does not fire a false
            // red on a row that is behaving exactly as RO left it.
            N("DesiredAttachAlt",      ExpectSource.RoDefault, 145000.0,
              "ApplyRODefaults assigns it twice; the LAST is `DesiredAttachAlt.Val = 145000`, beside DesiredOrbitAltitude"),
            B("AttachAltFlag",         ExpectSource.RoDefault, false, "ApplyRODefaults: ATTACH_ALT_FLAG_DEFAULT = false"),
            N("DesiredFPA",            ExpectSource.RoDefault, 0.0,   "ApplyRODefaults: DESIRED_FPA_DEFAULT = 0"),
            B("DesiredArgPFlag",       ExpectSource.RoDefault, false, "ApplyRODefaults: DESIRED_ARGP_FLAG_DEFAULT = false"),
            N("DesiredArgP",           ExpectSource.RoDefault, 0.0,   "ApplyRODefaults: DESIRED_ARGP_DEFAULT = 0"),

            // ---- §7.5's plane launch, and the non-persisted block around it -------------------------
            N("LaunchLANDifference",   ExpectSource.RoDefault, 0.0,   "ApplyRODefaults: LAUNCH_LAN_DIFFERENCE = 0"),
            B("LaunchingToMatchLan",   ExpectSource.FieldDefault, false, "MechJebModuleAscentSettings.cs 'some non-persisted values' — false at every scene load"),
            B("LaunchingToLan",        ExpectSource.FieldDefault, false, "same non-persisted block"),
            N("DesiredLan",            ExpectSource.FieldDefault, 0.0,   "field initialiser `new EditableDouble(0.0)`; RO does not seed it"),
            B("RelativeLAN",           ExpectSource.FieldDefault, false, "field initialiser `= false`; RO does not seed it"),
            B("OverrideWarpToPlane",   ExpectSource.FieldDefault, false, "same non-persisted block; only its own toggle sets it"),

            // ---- the pitch program -----------------------------------------------------------------
            N("PitchStartHeight",      ExpectSource.RoDefault, 100.0, "ApplyRODefaults: PITCH_START_HEIGHT_DEFAULT = 100"),
            B("CorrectiveSteering",    ExpectSource.FieldDefault, false, "field initialiser `= false`; RO does not seed it"),
            N("CorrectiveSteeringGain", ExpectSource.FieldDefault, 3.0, "field initialiser `new EditableDouble(3.0)`"),

            // ---- roll --------------------------------------------------------------------------------
            B("ForceRoll",   ExpectSource.FieldDefault, true, "field initialiser `= true`; RO does not seed it"),
            N("VerticalRoll", ExpectSource.FieldDefault, 0.0,  "field initialiser `new EditableDouble(0)`"),
            N("TurnRoll",     ExpectSource.FieldDefault, 0.0,  "field initialiser `new EditableDouble(0)`"),
            N("RollAltitude", ExpectSource.FieldDefault, 50.0, "field initialiser `new EditableDouble(50)`"),

            // ---- the AoA / q-alpha limiters -----------------------------------------------------------
            N("LimitQa",   ExpectSource.RoDefault,    2000.0, "ApplyRODefaults: LIMIT_QA_DEFAULT = 2000"),
            B("LimitAoA",  ExpectSource.FieldDefault, true,   "field initialiser `= true` (the classic limiter)"),
            N("MaxAoA",    ExpectSource.FieldDefault, 5.0,    "field initialiser `= 5`"),
            N("AOALimitFadeoutPressure", ExpectSource.FieldDefault, 2500.0, "field initialiser `new EditableDoubleMult(2500)`"),
            U("LimitingAoA", ExpectSource.StatusFlag, "written by the autopilot every Drive — a reading, not a setting"),

            // ---- the PSG stage model -------------------------------------------------------------------
            N("MinDeltaV",  ExpectSource.RoDefault,    40.0,  "ApplyRODefaults: MIN_DELTAV_DEFAULT = 40"),
            N("LastStage",  ExpectSource.FieldDefault, -1.0,  "field initialiser `= -1`; the PSG menu would clamp it to 0 when it draws, and we suppress the menu"),
            N("MaxCoast",   ExpectSource.RoDefault,    450.0, "ApplyRODefaults: MAX_COAST_DEFAULT = 450"),
            N("MinCoast",   ExpectSource.RoDefault,    0.0,   "ApplyRODefaults: MIN_COAST_DEFAULT = 0"),
            B("CoastStageFlag",     ExpectSource.RoDefault, false, "ApplyRODefaults: CoastStageFlag = false"),
            N("CoastStageInternal", ExpectSource.RoDefault, -1.0,  "ApplyRODefaults: CoastStageInternal.Val = -1"),
            N("CoastLocation",      ExpectSource.FieldDefault, -1.0, "field initialiser `= -1`; RO does not seed it"),
            B("SpinupStageFlag",    ExpectSource.RoDefault, false, "ApplyRODefaults: SpinupStageFlag = false (MechJeb's FIELD default is true)"),
            N("SpinupStageInternal",ExpectSource.RoDefault, -1.0,  "ApplyRODefaults: SpinupStageInternal.Val = -1"),
            N("SpinupLeadTime",     ExpectSource.FieldDefault, 50.0, "field initialiser `= 50`"),
            N("SpinupAngularVelocity", ExpectSource.FieldDefault, 1.0471975511965976,
              "field initialiser `new EditableDoubleMult(TAU / 6.0, TAU / 60.0)` — .Val is TAU/6 rad/s"),
            B("UnguidedStagesFlag",    ExpectSource.RoDefault,    false, "ApplyRODefaults: UnguidedStagesFlag = false"),
            N("UnguidedStagesInternal", ExpectSource.FieldDefault, 0.0, "field initialiser `new EditableIntList()` — read as its ENTRY COUNT, so 0 is empty"),
            N("UnguidedStages",        ExpectSource.FieldDefault, 0.0, "the derived projection: empty while UnguidedStagesFlag is false — read as its entry count"),
            B("FixedStagesFlag",       ExpectSource.RoDefault,    false, "ApplyRODefaults: FixedStagesFlag = false"),
            N("FixedStagesInternal",   ExpectSource.FieldDefault, 0.0, "field initialiser `new EditableIntList()` — read as its entry count"),
            N("FixedStages",           ExpectSource.FieldDefault, 0.0, "the derived projection: empty while FixedStagesFlag is false — read as its entry count"),
            N("PreStageTime",       ExpectSource.RoDefault, 10.0, "ApplyRODefaults: PRE_STAGE_TIME_DEFAULT = 10"),
            N("OptimizerPauseTime", ExpectSource.RoDefault, 5.0,  "ApplyRODefaults: OPTIMIZER_PAUSE_TIME_DEFAULT = 5"),
            N("Cd",   ExpectSource.FieldDefault, 0.5, "field initialiser `new EditableDouble(0.5)`"),
            N("Aref", ExpectSource.FieldDefault, 0.0, "field initialiser `new EditableDouble(0.0)` — 0 means 'derive it'"),

            // ---- the thrust controller: RO seeds every one of these ------------------------------------
            N("Core.Thrust.MaxDynamicPressure", ExpectSource.RoDefault, 50000.0,
              "ApplyRODefaults sets it TWICE — 20000 early, 50000 at the end; the LAST is RO's real seed"),
            N("Core.Thrust.MinThrottle",        ExpectSource.RoDefault, 0.05,  "ApplyRODefaults: Core.Thrust.MinThrottle.Val = 0.05"),
            B("Core.Thrust.LimiterMinThrottle", ExpectSource.RoDefault, true,  "ApplyRODefaults: LimiterMinThrottle = true"),
            B("Core.Thrust.LimitToPreventUnstableIgnition", ExpectSource.RoDefault, false, "ApplyRODefaults: LimitToPreventUnstableIgnition = false"),
            B("Core.Thrust.AutoRCSUllaging",    ExpectSource.RoDefault, true,  "ApplyRODefaults: AutoRCSUllaging = true"),
            B("Core.Thrust.LimitThrottle",      ExpectSource.RoDefault, false, "ApplyRODefaults: LimitThrottle = false"),
            B("Core.Thrust.LimitAcceleration",  ExpectSource.RoDefault, false, "ApplyRODefaults: LimitAcceleration = false"),
            B("Core.Thrust.LimitToPreventOverheats", ExpectSource.RoDefault, false, "ApplyRODefaults: LimitToPreventOverheats = false"),

            // ---- CLASSIC-path only: unread while AscentType is PSG, but still READ BACK, because
            // ---- "unread by the solver" is a claim and this is how it stops being one. ----------------
            N("TurnStartAltitude", ExpectSource.FieldDefault, 500.0,   "field initialiser `new EditableDoubleMult(500)`"),
            N("TurnStartVelocity", ExpectSource.FieldDefault, 50.0,    "field initialiser `new EditableDoubleMult(50)`"),
            N("TurnEndAltitude",   ExpectSource.FieldDefault, 60000.0, "field initialiser `new EditableDoubleMult(60000, 1000)` — .Val is 60000 m"),
            N("TurnEndAngle",      ExpectSource.FieldDefault, 0.0,     "field initialiser `= 0`"),
            N("TurnShapeExponent", ExpectSource.FieldDefault, 0.4,     "field initialiser `new EditableDoubleMult(0.4, 0.01)` — .Val is 0.4"),
            B("AutoPath",          ExpectSource.FieldDefault, true,    "field initialiser `= true`"),
            N("AutoTurnPerc",      ExpectSource.FieldDefault, 0.05,    "field initialiser `= 0.05f` — a float, so compared at float precision"),
            N("AutoTurnSpdFactor", ExpectSource.FieldDefault, 18.5,    "field initialiser `= 18.5f`"),
        };

        // =========================================================================================
        // 2. LOOKUP AND SELF-CHECK
        // =========================================================================================

        /// <summary>The expectation for a setting, or a row whose `Name` is null when there is none.</summary>
        public static AscentExpect Expect(string name)
        {
            for (int i = 0; i < Expected.Length; i++)
                if (string.Equals(Expected[i].Name, name, StringComparison.Ordinal)) return Expected[i];
            return default(AscentExpect);
        }

        /// <summary>The first `AscentProfile.Audit` row with no expectation, or null. ⛔ A box the audit
        /// decides about and the instrument cannot measure is a box back in the state S219 ended.</summary>
        public static string FirstUnmeasured()
        {
            for (int i = 0; i < AscentProfile.Audit.Length; i++)
            {
                string n = AscentProfile.Audit[i].Name;
                if (Expect(n).Name == null) return n;
            }
            return null;
        }

        /// <summary>The first expectation with no audit row, or null. The other direction: an
        /// expectation for a box nobody decided about is an orphan.</summary>
        public static string FirstOrphan()
        {
            for (int i = 0; i < Expected.Length; i++)
                if (!AscentProfile.Accounted(Expected[i].Name)) return Expected[i].Name;
            return null;
        }

        /// <summary>
        /// ⭐ THE COUPLING CHECK. An expectation's SOURCE has to be consistent with the audit's own
        /// DISPOSITION for the same box, or the two tables have drifted and the instrument is measuring
        /// against a decision that was changed underneath it. Returns the first inconsistent name, or null.
        /// </summary>
        public static string FirstSourceMismatch()
        {
            for (int i = 0; i < Expected.Length; i++)
            {
                AscentSetting row = AscentProfile.Row(Expected[i].Name);
                if (row.Name == null) continue;                  // FirstOrphan reports this case
                if (!Agrees(row.How, Expected[i].Source)) return Expected[i].Name;
            }
            return null;
        }

        /// <summary>Which expectation sources a given audit disposition admits.</summary>
        public static bool Agrees(AscentDisposition how, ExpectSource src)
        {
            switch (how)
            {
                case AscentDisposition.Write:          return src == ExpectSource.OurWrite;
                case AscentDisposition.RuntimeMission: return src == ExpectSource.MissionFact;
                case AscentDisposition.UiDerived:      return src == ExpectSource.MenuDerived;
                // ⚠ Both fly RO's default TODAY — that is exactly what makes them measurable, and
                // measuring them is how "we did not quietly write it after all" stops being a promise.
                case AscentDisposition.OwnerQuestion:  return src == ExpectSource.RoDefault;
                // A left-alone row is RO's seed, MechJeb's own field default, or a flag the autopilot
                // writes. The audit's prose says which; the expectation says which in a checkable way.
                case AscentDisposition.RoDefault:
                case AscentDisposition.ClassicOnly:
                    return src == ExpectSource.RoDefault || src == ExpectSource.FieldDefault
                        || src == ExpectSource.StatusFlag;
            }
            return false;
        }

        // =========================================================================================
        // 3. THE VERDICT
        // =========================================================================================

        /// <summary>
        /// ⚠ A RELATIVE TOLERANCE, BECAUSE TWO OF THESE FIELDS ARE `float`. `AutoTurnPerc = 0.05f`
        /// widens to 0.05000000074505806 as a double, which is not 0.05 and must not be reported as a
        /// disagreement. 1e-6 relative is far tighter than any real setting difference (the smallest
        /// gap between a default and a tuned value in the shipped cfg is 0.75 vs 5.0) and far looser
        /// than float widening.
        /// </summary>
        public static bool SameNumber(double a, double b)
        {
            double scale = Math.Abs(b); if (scale < 1.0) scale = 1.0;
            return Math.Abs(a - b) <= 1e-6 * scale;
        }

        /// <summary>Invariant formatting, so a European locale cannot turn 0.75 into "0,75" in a log.</summary>
        public static string Fmt(double v)
        {
            return v.ToString("0.##########", System.Globalization.CultureInfo.InvariantCulture);
        }

        static AscentObserved Find(AscentObserved[] obs, string name)
        {
            if (obs != null)
                for (int i = 0; i < obs.Length; i++)
                    if (string.Equals(obs[i].Name, name, StringComparison.Ordinal)) return obs[i];
            return AscentObserved.Unreadable(name, "no reading was taken");
        }

        /// <summary>One verdict per expectation row, in table order. Rows with no reading come back
        /// `Unread` — never silently absent, because a missing row is exactly the hole this instrument
        /// exists to close.</summary>
        public static ReadbackVerdict[] Verdicts(AscentObserved[] obs)
        {
            var outv = new ReadbackVerdict[Expected.Length];
            for (int i = 0; i < Expected.Length; i++)
            {
                AscentExpect e = Expected[i];
                AscentObserved o = Find(obs, e.Name);

                ReadbackVerdict v = new ReadbackVerdict();
                v.Name = e.Name; v.Source = e.Source; v.Cite = e.Cite;
                v.Live = o.Read ? o.Rendered() : ("? (" + (o.Why ?? "not read") + ")");
                v.Expected = !e.Checkable ? "(not checked — " + SourceWord(e.Source) + ")"
                           : e.IsNumber ? Fmt(e.Number) : e.Text;
                v.Unread = !o.Read;
                v.NotChecked = !e.Checkable;

                if (o.Read && e.Checkable)
                {
                    v.Disagrees = e.IsNumber
                        ? !(o.IsNumber && SameNumber(o.Number, e.Number))
                        : !string.Equals(o.Rendered(), e.Text, StringComparison.OrdinalIgnoreCase);
                }
                outv[i] = v;
            }
            return outv;
        }

        public static string SourceWord(ExpectSource s)
        {
            switch (s)
            {
                case ExpectSource.RoDefault:    return "RO default";
                case ExpectSource.FieldDefault: return "MechJeb field default";
                case ExpectSource.OurWrite:     return "OUR write";
                case ExpectSource.MenuDerived:  return "MechJeb's own menu derives it";
                case ExpectSource.MissionFact:  return "a mission fact, read at runtime";
                case ExpectSource.StatusFlag:   return "a status flag the autopilot writes";
            }
            return "?";
        }

        public static int CountDisagreements(ReadbackVerdict[] v)
        {
            int n = 0;
            if (v != null) for (int i = 0; i < v.Length; i++) if (v[i].Disagrees) n++;
            return n;
        }

        public static int CountUnread(ReadbackVerdict[] v)
        {
            int n = 0;
            if (v != null) for (int i = 0; i < v.Length; i++) if (v[i].Unread) n++;
            return n;
        }

        // =========================================================================================
        // 4. THE RENDER — the claim's headline, the table, and the delta
        // =========================================================================================

        /// <summary>
        /// ⭐⭐ THE SENTENCE THAT MAKES `AscentProfile.Render()` HONEST. It is appended to the audit's
        /// own headline at every configure, so the CLAIM ("8 written, 53 at the RO default") can never
        /// appear in `KSP.log` without the MEASUREMENT beside it.
        /// ⛔ AND IT CAN GO RED. A green that cannot go red is the S130 failure; `AscentReadbackTest`
        /// seeds a divergence and asserts this string reports it.
        /// </summary>
        public static string Headline(ReadbackVerdict[] v)
        {
            if (v == null || v.Length == 0)
                return "⛔ READ-BACK NOT TAKEN — the audit above is a CLAIM with nothing behind it.";

            int dis = CountDisagreements(v), unread = CountUnread(v), notChecked = 0;
            for (int i = 0; i < v.Length; i++) if (v[i].NotChecked) notChecked++;
            int checkedRows = v.Length - unread - notChecked;

            var sb = new System.Text.StringBuilder();
            sb.Append("READ BACK FROM THE LIVE CORE: ").Append(v.Length - unread).Append('/').Append(v.Length)
              .Append(" row(s) read, ").Append(checkedRows).Append(" checked against their declared source, ")
              .Append(notChecked).Append(" not checkable (mission fact / menu-derived / status flag)");
            if (unread > 0) sb.Append(", ⚠ ").Append(unread).Append(" COULD NOT BE READ");
            sb.Append(dis > 0 ? ", ⛔ " + dis + " row(s) DISAGREE WITH THEIR DECLARED SOURCE"
                              : ", 0 disagree");
            return sb.ToString();
        }

        /// <summary>The whole table, one line per box: what MechJeb holds, and the verdict on it.</summary>
        public static string Render(string when, ReadbackVerdict[] v)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("ASCENT READ-BACK (").Append(when ?? "?").Append(") — ").Append(Headline(v));
            if (v == null) return sb.ToString();

            // ⛔ THE DISAGREEMENTS FIRST AND SEPARATELY. 77 rows scroll; three red ones inside them do
            // not get seen, and "it was in the log" is not the same as "it was reported".
            for (int i = 0; i < v.Length; i++)
                if (v[i].Disagrees)
                    sb.Append("\n    ⛔ ").Append(v[i].Name).Append(" = ").Append(v[i].Live)
                      .Append("  — expected ").Append(v[i].Expected).Append("  [").Append(v[i].Cite).Append("]");
            for (int i = 0; i < v.Length; i++)
                if (v[i].Unread)
                    sb.Append("\n    ⚠ ").Append(v[i].Name).Append(" ").Append(v[i].Live);

            sb.Append("\n    ---- every box, as the core holds it ----");
            for (int i = 0; i < v.Length; i++)
                sb.Append("\n    ").Append(v[i].Disagrees ? "⛔ " : "   ").Append(v[i].Name)
                  .Append(" = ").Append(v[i].Live)
                  .Append("   (expected ").Append(v[i].Expected).Append(", ")
                  .Append(SourceWord(v[i].Source)).Append(')');
            return sb.ToString();
        }

        /// <summary>
        /// ⭐⭐ THE EXPERIMENT. Two readings, and what moved between them. If nothing re-seeds the
        /// module after `Configure`, this is empty at the terminal count and that is a RESULT; if
        /// something does, this names the box and both values, which is the other result. Neither is a
        /// guess, which is the whole point of taking two readings instead of one.
        /// </summary>
        /// <summary>
        /// ⭐⭐ S228 (NTSB-2026-002). **HOW MANY BOXES MOVED — the same question `Delta` asks, answered as
        /// a NUMBER a guard can act on.**
        ///
        /// ⛔ THE WHOLE REASON THIS EXISTS: on 2026-09-08 the conductor printed *"⛔ 17 box(es) CHANGED …
        /// Something re-seeded the module after the conductor configured it"* at the terminal count **and
        /// lit the engines 69 seconds later**. It had the evidence and no authority to act on it. A
        /// prose sentence is not something code can refuse to fly on; this is.
        ///
        /// ⛔ AND IT SHARES ITS COMPARISON WITH `Delta` RATHER THAN RESTATING IT — one loop, one rule. Two
        /// copies of "did this box move" is exactly how a count and its own explanation drift apart, and
        /// then the log says 17 while the guard says 0.
        /// </summary>
        public static int CountMoved(AscentObserved[] before, AscentObserved[] after)
        {
            return Moved(before, after, null);
        }

        /// <summary>The one comparison. `sb` null = count only; non-null = also render each moved box.</summary>
        static int Moved(AscentObserved[] before, AscentObserved[] after, System.Text.StringBuilder sb)
        {
            int moved = 0;
            for (int i = 0; i < Expected.Length; i++)
            {
                string n = Expected[i].Name;
                AscentObserved a = Find(before, n), b = Find(after, n);
                if (!a.Read && !b.Read) continue;
                string sa = a.Read ? a.Rendered() : "?", sbv = b.Read ? b.Rendered() : "?";
                bool same = a.Read && b.Read && a.IsNumber && b.IsNumber
                            ? SameNumber(a.Number, b.Number)
                            : string.Equals(sa, sbv, StringComparison.Ordinal);
                if (same) continue;
                moved++;
                if (sb != null)
                    sb.Append("\n    ⚠ ").Append(n).Append(": ").Append(sa).Append("  ->  ").Append(sbv);
            }
            return moved;
        }

        public static string Delta(AscentObserved[] before, AscentObserved[] after)
        {
            var sb = new System.Text.StringBuilder();
            int moved = Moved(before, after, sb);
            return moved == 0
                ? "ASCENT READ-BACK DELTA — NOTHING MOVED between the two readings. Every box the "
                  + "conductor left is the box that flew: no later pass re-seeded the module."
                : "ASCENT READ-BACK DELTA — ⛔ " + moved + " box(es) CHANGED between the two readings. "
                  + "Something re-seeded the module after the conductor configured it:" + sb;
        }

        /// <summary>
        /// R-04's half of the manifest: "Name = value" per box, read live, for
        /// `BlackBoxManifest.MechJebAscent`. ⛔ Kept in its OWN collection, never merged with
        /// DragonScreen's `[Tunable]`s — a reader must never have to work out whose setting a line is.
        /// </summary>
        public static List<string> ManifestLines(AscentObserved[] obs)
        {
            var lines = new List<string>();
            for (int i = 0; i < Expected.Length; i++)
            {
                AscentObserved o = Find(obs, Expected[i].Name);
                lines.Add("MechJeb." + Expected[i].Name + " = " + (o.Read ? o.Rendered() : "?"));
            }
            return lines;
        }
    }
}
