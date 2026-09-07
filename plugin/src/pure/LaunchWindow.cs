// DragonScreen — LaunchWindow  (PURE: WHEN the pad passes under the target's plane, and whether to fly it)
// ============================================================================================
// Register S215. The owner's directive, 2026-09-07, verbatim:
//     "Read the mechjeb research and ensure everything is set up correctly to launch tto rendezvous
//      including auto warp to launch window"
//
// ---- ⛔ THE GAP THIS FILE CLOSES IS **ABSENCE**, NOT BREAKAGE ----
// Before S215 the conductor's `Configure()` wrote four things: `Autostage = false`, `AscentType = PSG`,
// `LimitQaEnabled = true`, and the apsides + inclination from `AscentTargets.For(...)`. **No phase angle,
// no LAN targeting, no countdown, no warp.** Nothing was wrong with any of it — a flawless ascent would
// simply have arrived in the RIGHT-SHAPED orbit in the WRONG PLANE, and `docs/MECHJEB_MASTER_MAP.md` §7.5
// says exactly why that is fatal to the mission rather than merely untidy:
//     "This is why 'set inclination by hand + engage' fails rendezvous: right inclination, wrong RAAN,
//      and the rendezvous autopilot then needs an unaffordable plane change."
// `docs/MECHJEB_MISSION_TUNING.md:216` had already parked the same fact as a forward note — *"Not used —
// we are not launch-window-phasing with PVG. ⚠ If Part B ever wants a real ISS launch window it lives
// here"* — and `docs/REAL_CREW_DRAGON_MISSION.md` §2 flagged it as a MISSING capability outright:
// *"THE LAUNCH WINDOW BECOMES A PLANE PROBLEM AGAIN … at 51.6 deg it is no longer degenerate: there are
// two windows per day and the launch azimuth is set by the target plane. … We need it back, in C#."*
//
// ---- ⭐ THE MATHS IS **MIRRORED FROM THE VENDORED TREE**, NOT DERIVED ----
// `plugin/mech/MechJebLib/Functions/Astro.cs:453-505` already contains `MinimumTimeToPlane` /
// `TimeToPlane`, and `plugin/mech/MechJebKos/AscentBindingBase.cs:112-133` shows how MechJeb's own kOS
// binding calls them. S215's brief is explicit: **"Build against it; do not re-derive the maths."** So
// every line of `TimeToPlaneS` below corresponds to a named line of `Astro.TimeToPlane`, and the
// correspondence is stated in the code. This is the same discipline `pure/PvgPreflight.cs` §4 uses for
// the PSG phase table, and it exists for the same reason: **the pure layer cannot reference MechJeb**
// (`pure/AscentSequence.cs`'s header: "PURE: no Unity, no KSP, no MechJeb reference"), and a quantity
// that cannot be computed headlessly cannot be TESTED headlessly.
//
// ⛔ AND A MIRROR CAN DRIFT — SO THIS ONE IS **SELF-POLICING**, WHICH IS THE WHOLE POINT.
// The glue does NOT fly this file's number. `src/MechConductor.cs` calls the **vendored**
// `Astro.MinimumTimeToPlane` for the value it actually warps to, calls this mirror alongside it, and
// **holds the launch if the two disagree** (`MirrorAgrees`). So:
//   • the flown number is always the vendored one — one source of truth in flight;
//   • the mirror is what the headless suite mutation-proves against a known target and epoch;
//   • a re-pin of `plugin/mech/` that changed the maths surfaces as a HOLD on the pad with both numbers
//     printed, instead of as a silent plane error discovered in orbit.
// A mirror nobody checks is a second implementation waiting to be wrong. This one is checked every
// launch, by the only party that can see both.
//
// ⛔⛔ **Q1 BELOW IS SUPERSEDED IN PLACE — 2026-09-07, register S219 JOB 2.** Read the paragraph and
// then read this. (C1.16 / G12: reasoning is marked superseded where it stands and is never deleted —
// this one especially, because it is CORRECT about the hazard and only wrong about the remedy.)
//
// **WHAT Q1 CLAIMED.** That there is no `StartCountdown` call in this build and that this file is why one
// is not needed, because arming MechJeb's countdown hands T-0 to `StageManager.ActivateNextStage()`.
//
// **WHAT THE OWNER RULED**, 2026-09-07, verbatim:
//     *"this is all the research I ordered to be completed, I should not have to explain step by step how
//      to use mechjeb if the research has been done."*
//     *"The overseer put this file in the T18 prompt's READ-FIRST list and then ruled against
//      `StartCountdown` without opening §7.5. ⭐ THE MAP IS THE SPECIFICATION. Follow it section by
//      section. Do not re-derive it, and do not invent a parallel mechanism again."*
// `docs/MECHJEB_MASTER_MAP.md` §7.5 documents the plane launch as ONE sequence ending in
// `StartCountdown`, and `MechJebModuleAscentMenu.cs:245-258` is that sequence in source. S219 now
// reproduces it exactly.
//
// ⭐ **AND Q1's OWN OBJECTION IS ANSWERED RATHER THAN OVERRULED.** Its sharpest sentence is still true:
//     *"a safety property that holds only because we win a race is not a safety property."*
// Exactly so — and the answer it did not find is that the race can be DELETED. `TimedLaunch` is a
// public field, MechJeb's own ascent window clears it from its Abort button
// (`MechJebModuleAscentMenu.cs:305`), and `OnFixedUpdate:122` runs the entire staging block only
// `if (TimedLaunch)`. `MechConductor.TickTerminalCount` clears it at T-10 s, so from then on MechJeb has
// no T-0 of any kind and `StageManager.ActivateNextStage()` is unreachable — including in the one case
// Q1's race would have lost, where `IgnitionGate` safes the pad and leaves `ThrustAvailable` at zero.
//
// **WHAT ELSE STOOD DOWN WITH IT.** `MechConductor.TickLaunchWarp` + `WarpLeadSeconds` (this file's
// warp half) are superseded in place there; MechJeb's countdown warps now, and the composed 20 s + 12 s
// lead moved into its own `WarpCountDown` box as `AscentProfile.WarpCountDownS`.
//
// ⚠ **WHAT DID *NOT* STAND DOWN, AND MUST NOT.** Everything else in this file. §7.5's own call IS
// `Astro.MinimumTimeToPlane`, which `SolveWindow` already makes; the MIRROR below is what checks the
// vendored answer and HOLDS THE LAUNCH if a re-pin ever changed the maths; and Q2/Q3/Q4 are untouched.
// The parallel mechanism the owner objected to was the countdown and the warp, not the arithmetic.
//
// ---- THE FOUR DECISIONS THIS FILE APPLIES (all settled BEFORE S215 began; see REGISTER.md S215) ----
// **Q1 — there is no `StartCountdown` call anywhere in this build, and this file is why one is not
// needed.** `MechJebModuleAscentBaseAutopilot.cs:127` fires `StageManager.ActivateNextStage()` at T-0,
// gated only on `Enabled && VesselState.ThrustAvailable < 10E-4` — **not** on `AscentSettings.Autostage`
// (that governs only `Core.Staging.Users`, `:82`). §B8 and §B12.7 forbid MechJeb actuating an ignition,
// and `IgnitionGate` owns T-0. Arming MechJeb's countdown would hand T-0 to `StageManager`. ⚠ The guard
// happens to align with our design — our octaweb is lit ~3 s before T-0, so `ThrustAvailable` is not
// small and the branch would not fire — **but a safety property that holds only because we win a race is
// not a safety property.** We compute the UT here (read-only maths) and warp to it ourselves.
// **Q2 — the COMPUTED inclination wins at launch, and a material disagreement is an ERROR.** See
// `InclinationAgrees` and `MaxDisagreementDeg` below.
// **Q3 — the crew GO comes FIRST, then the warp.** Not this file's mechanism (it is `CrewProcedureOps`'s
// and `AscentSequence`'s), but it is why `Solve` is a function of a target and a clock and knows nothing
// about gates: by the time anything asks this file for a window, the crew have already committed.
// **Q4 — no target in the same SoI ⇒ NO WINDOW EXISTS.** `Verdict.NoTarget`, and the caller must hold.
//
// PURE: no Unity, no KSP, no MechJeb reference. Every function is a function of its arguments.
// ============================================================================================
using System;

namespace DragonScreen
{
    /// <summary>
    /// Why the launch window is, or is not, flyable.
    /// ⛔ **`NoTarget` IS ZERO ON PURPOSE, AND `Armed` IS NOT.** A `default(WindowPlan)` — a field not yet
    /// assigned, a struct cleared by a reset — must not read as "cleared to launch". The default of an
    /// enum is whichever member is 0, so the safe answer is put there and the permissive one is put out
    /// of reach of an accident.
    /// </summary>
    public enum WindowVerdict : byte
    {
        /// <summary>⛔ No target orbit in the same sphere of influence — there is no plane to launch into.</summary>
        NoTarget = 0,
        /// <summary>⛔ The plane's inclination and the mission's disagree materially. Something is wrong.</summary>
        InclinationDisagrees,
        /// <summary>⛔ The geometry produced a non-finite time or angle. Refuse rather than warp to NaN.</summary>
        NotComputable,
        /// <summary>A window exists and the plane agrees with the mission. Fly it.</summary>
        Armed
    }

    /// <summary>The measured state one launch-window solution is computed from.</summary>
    public struct WindowInputs
    {
        /// <summary>Is there a target, and is its orbit around the body we are sitting on?</summary>
        public bool TargetInSameSoi;
        /// <summary>Central-body rotation period, seconds. Negative for a retrograde-rotating body.</summary>
        public double BodyRotationPeriodS;
        /// <summary>Launch-site latitude, degrees.</summary>
        public double LatitudeDeg;
        /// <summary>Launch-site celestial longitude RIGHT NOW, degrees.</summary>
        public double CelestialLongitudeDeg;
        /// <summary>Target orbit's longitude of the ascending node, degrees.</summary>
        public double TargetLanDeg;
        /// <summary>Target orbit's inclination, degrees.</summary>
        public double TargetInclinationDeg;
        /// <summary>
        /// LAN offset applied to the timing, degrees. MechJeb's `AscentSettings.LaunchLANDifference`;
        /// `docs/MECHJEB_MASTER_MAP.md` §7.5: "`LaunchLANDifference` = 0 for the exact plane."
        /// </summary>
        public double LanDifferenceDeg;
        /// <summary>Universal time now, seconds.</summary>
        public double NowUT;
        /// <summary>
        /// The §B5 MISSION FACT inclination, from `AscentTargets.For(...)`. Q2 checks the computed
        /// plane against this; it is NOT what gets flown.
        /// </summary>
        public double MissionInclinationDeg;
    }

    /// <summary>One launch-window solution: when to launch, into what plane, and whether to.</summary>
    public struct WindowPlan
    {
        public WindowVerdict Verdict;
        /// <summary>The UT the pad passes under the target's plane. Meaningless unless <see cref="Armed"/>.</summary>
        public double LaunchUT;
        /// <summary>Seconds from `NowUT` to <see cref="LaunchUT"/>. Meaningless unless <see cref="Armed"/>.</summary>
        public double TimeToWindowS;
        /// <summary>
        /// The inclination that REACHES that plane, sign and all — the northgoing or southgoing
        /// solution, whichever comes sooner. ⭐ This settles T18's Q1 (the inclination SIGN): the sign
        /// is no longer inherited from whatever cfg happened to be loaded, it is a RESULT.
        /// </summary>
        public double InclinationDeg;
        /// <summary>WHY, in words. The same discipline `AscentDecision.Reason` carries.</summary>
        public string Reason;

        public bool Armed { get { return Verdict == WindowVerdict.Armed; } }
    }

    /// <summary>The plane-crossing geometry, and the decision to fly it or hold.</summary>
    public static class LaunchWindow
    {
        const double PI = Math.PI;
        const double TAU = 2.0 * PI;
        /// <summary>`MechJebLib/Utils/Statics.cs:36` — the vendored epsilon, mirrored by value.</summary>
        const double EPS = 2.2204460492503131e-16;

        /// <summary>
        /// ⭐ **Q2's TOLERANCE — a stated MARGIN, with both of its bounds sourced in this repo.**
        ///
        /// The computed inclination reaches the target's ACTUAL plane; `AscentTargets.For()`'s is the
        /// §B5 MISSION FACT. For a correct window the two agree to within the drift between a published
        /// figure and a live orbit. The tolerance has to sit above that and below any real error:
        ///   • **the floor.** This repo carries TWO values for the same ISS plane, both correct —
        ///     `pure/MissionProfile.cs:64` `IncDeg = 51.6`, and the shipped
        ///     `mechjeb_settings_type_Crew-Dragon.cfg` `DesiredInclination = -51.6316`. That is a
        ///     **0.0316°** spread between two agreed-good numbers, so a tolerance at or under it would
        ///     hold a perfectly correct launch. 1.0° is ~31× that spread.
        ///   • **the ceiling.** The error this must CATCH is "the wrong target is selected" or "the
        ///     station is not where the mission says" — `docs/REAL_CREW_DRAGON_MISSION.md` §3 records
        ///     the real instance, a station at **0.133°** against a mission fact of **51.6°**. Every
        ///     plane error worth refusing a launch over is degrees, not hundredths.
        /// ⚠ **IT IS A MARGIN, NOT PHYSICS, AND IT IS UN-CONVERGED** — the same class as `WarpPlan`'s
        /// four `[Tunable]`s and stated the same way. Safe by construction in one direction only:
        /// smaller = more launches held, larger = more wrong planes flown.
        /// </summary>
        public const double MaxDisagreementDeg = 1.0;

        /// <summary>
        /// Q2. Do the computed plane and the mission fact agree? **MAGNITUDES ONLY** — deliberately.
        /// The SIGN is not a disagreement, it is the northgoing/southgoing CHOICE that
        /// <see cref="MinimumTimeToPlane"/> just made on timing grounds, and `Astro.MinimumTimeToPlane`
        /// (`Astro.cs:456-457`) evaluates both branches from `Abs(inc)` for exactly that reason.
        /// ⛔ A non-finite input DISAGREES. It must not slip through as "close enough".
        /// </summary>
        public static bool InclinationAgrees(double computedDeg, double missionDeg, double tolDeg)
        {
            if (!Finite(computedDeg) || !Finite(missionDeg) || !Finite(tolDeg)) return false;
            double a = computedDeg < 0.0 ? -computedDeg : computedDeg;
            double b = missionDeg   < 0.0 ? -missionDeg   : missionDeg;
            double d = a - b; if (d < 0.0) d = -d;
            return d <= tolDeg;
        }

        /// <summary>
        /// The mirror's guard against its own drift (see the header). Do this file's answer and the
        /// vendored `Astro`'s agree to within <paramref name="tolS"/> seconds?
        /// ⛔ Non-finite on either side is a DISAGREEMENT.
        /// </summary>
        public static bool MirrorAgrees(double mineS, double vendoredS, double tolS)
        {
            if (!Finite(mineS) || !Finite(vendoredS) || !Finite(tolS)) return false;
            double d = mineS - vendoredS; if (d < 0.0) d = -d;
            return d <= tolS;
        }

        /// <summary>
        /// ⚠ **NOT A TUNING VALUE.** How far apart this mirror and the vendored `Astro` may be before
        /// the launch holds. One millisecond on a quantity that is hours long: an agreement epsilon for
        /// "the same double arithmetic ran twice", stated as one rather than hidden.
        /// </summary>
        public const double MirrorToleranceS = 1e-3;

        // ============================ THE MIRROR ============================

        /// <summary>
        /// Time until the launch site passes under a plane defined by (LAN, inc).
        ///
        /// ⭐ **A LINE-FOR-LINE MIRROR of `MechJebLib/Functions/Astro.cs:470-504` `TimeToPlane`.** The
        /// vendored comments are kept where they explain a branch, because the branch is not obvious and
        /// re-deriving why it is there is exactly the cost C1.16 exists to avoid paying twice.
        /// ⛔ Do not "simplify" a branch here. Its job is to equal the vendored function, and
        /// `MirrorAgrees` will catch it on the pad if it stops doing so.
        /// </summary>
        public static double TimeToPlaneS(double rotationPeriodS, double latitudeDeg,
                                          double celestialLongitudeDeg, double lanDeg, double incDeg)
        {
            double latitude = Deg2Rad(latitudeDeg);
            double celestialLongitude = Deg2Rad(celestialLongitudeDeg);
            double lan = Deg2Rad(lanDeg);
            double inc = Deg2Rad(incDeg);

            // handle singularities at the poles where tan(lat) is infinite
            if (Abs(Abs(latitude) - PI / 2.0) < EPS) return 0.0;

            // handle equatorial orbits where longitude doesn't matter
            if (Abs(inc) < EPS || Abs(Abs(inc) - PI) < EPS) return 0.0;

            // Napier's rules for spherical trig
            // the clamped Asin produces correct results for abs(inc) < abs(lat)
            double angleEastOfAN = SafeAsin(Math.Tan(latitude) / Math.Tan(Abs(inc)));

            // handle south going trajectories (and the other two quadrants that Asin doesn't cover).
            // if you are launching to the north your AN is always going to be [-90,90] relative to
            // the zero of the launch site.  or facing the launch site your AN is always going to be
            // in "front" of the planet.  but launching south the AN is [90,270] and the AN is always
            // "behind" the planet.
            if (inc < 0.0) angleEastOfAN = PI - angleEastOfAN;

            double lanNow = celestialLongitude - angleEastOfAN;
            double lanDiff = lan - lanNow;

            // handle planets that rotate backwards
            if (rotationPeriodS < 0.0) lanDiff = -lanDiff;

            return Clamp2Pi(lanDiff) / TAU * Abs(rotationPeriodS);
        }

        /// <summary>
        /// The soonest of the northgoing and southgoing ground tracks, and the inclination that flies it.
        /// ⭐ **A MIRROR of `Astro.cs:453-458` `MinimumTimeToPlane`**, including the tie-break: the
        /// vendored `north &lt; south ? north : south` prefers SOUTH on an exact tie, and so does this.
        /// </summary>
        public static void MinimumTimeToPlane(double rotationPeriodS, double latitudeDeg,
                                              double celestialLongitudeDeg, double lanDeg, double incDeg,
                                              out double timeS, out double inclinationDeg)
        {
            double mag = Abs(incDeg);
            double north = TimeToPlaneS(rotationPeriodS, latitudeDeg, celestialLongitudeDeg, lanDeg,  mag);
            double south = TimeToPlaneS(rotationPeriodS, latitudeDeg, celestialLongitudeDeg, lanDeg, -mag);
            if (north < south) { timeS = north; inclinationDeg =  mag; }
            else               { timeS = south; inclinationDeg = -mag; }
        }

        // ============================ THE DECISION ============================

        /// <summary>
        /// Solve the window and say whether to fly it. The whole of S215's Q2 and Q4, in one function
        /// of its inputs.
        ///
        /// ⛔ **THE ORDER OF THE REFUSALS IS LOAD-BEARING.** No target is checked FIRST, because with no
        /// target every other field is meaningless — `TargetLanDeg` and `TargetInclinationDeg` would be
        /// whatever the caller last held, and a plausible-looking window computed from a stale plane is
        /// the precise failure §14.4(a) calls worse than a dead control.
        /// </summary>
        public static WindowPlan Solve(WindowInputs s)
        {
            WindowPlan p = new WindowPlan();
            p.LaunchUT = 0.0; p.TimeToWindowS = 0.0; p.InclinationDeg = 0.0;

            // ---- Q4: no target in the same SoI ⇒ there is no plane, so there is no window. ----
            if (!s.TargetInSameSoi)
            {
                p.Verdict = WindowVerdict.NoTarget;
                p.Reason = "no target in this SoI — there is no plane to launch into";
                return p;
            }

            // A body that does not rotate has no plane crossing to wait for, and dividing the geometry
            // by its period would be meaningless. Refuse rather than return a number.
            if (!Finite(s.BodyRotationPeriodS) || s.BodyRotationPeriodS == 0.0
                || !Finite(s.LatitudeDeg) || !Finite(s.CelestialLongitudeDeg)
                || !Finite(s.TargetLanDeg) || !Finite(s.TargetInclinationDeg)
                || !Finite(s.LanDifferenceDeg) || !Finite(s.NowUT))
            {
                p.Verdict = WindowVerdict.NotComputable;
                p.Reason = "the launch geometry is not a finite number";
                return p;
            }

            double timeS, incDeg;
            MinimumTimeToPlane(s.BodyRotationPeriodS, s.LatitudeDeg, s.CelestialLongitudeDeg,
                               s.TargetLanDeg - s.LanDifferenceDeg, s.TargetInclinationDeg,
                               out timeS, out incDeg);

            if (!Finite(timeS) || !Finite(incDeg))
            {
                p.Verdict = WindowVerdict.NotComputable;
                p.Reason = "the plane-crossing solution is not a finite number";
                return p;
            }

            p.InclinationDeg = incDeg;
            p.TimeToWindowS = timeS;
            p.LaunchUT = s.NowUT + timeS;

            // ---- Q2: the computed plane must agree with the §B5 mission fact, or something is wrong. ----
            // ⛔ SURFACE IT AND HOLD. Flying the computed plane silently would hide the fact that the
            // target, the window or the mission fact is wrong — and all three are worth knowing about
            // BEFORE the clamps release.
            if (!InclinationAgrees(incDeg, s.MissionInclinationDeg, MaxDisagreementDeg))
            {
                p.Verdict = WindowVerdict.InclinationDisagrees;
                p.Reason = "target plane " + Fmt(incDeg) + "° disagrees with the mission's "
                         + Fmt(s.MissionInclinationDeg) + "° by more than " + Fmt(MaxDisagreementDeg)
                         + "° — the target, the window or the mission fact is wrong";
                return p;
            }

            p.Verdict = WindowVerdict.Armed;
            p.Reason = "window in " + Fmt(timeS) + " s, plane " + Fmt(incDeg) + "°";
            return p;
        }

        // ============================ mirrored helpers ============================
        // `MechJebLib/Utils/Statics.cs`, by value. The pure layer cannot reference the vendored tree.

        /// <summary>`Statics.cs:132` — `!IsFinite(x) ? NaN : Asin(Clamp(x, -1, 1))`.</summary>
        static double SafeAsin(double x)
        {
            if (!Finite(x)) return double.NaN;
            if (x < -1.0) x = -1.0; else if (x > 1.0) x = 1.0;
            return Math.Asin(x);
        }

        /// <summary>`Statics.cs:267-272` — the equivalent value in [0, TAU).</summary>
        static double Clamp2Pi(double x)
        {
            x %= TAU;
            x = x < 0.0 ? x + TAU : x;
            return x >= TAU ? 0.0 : x;
        }

        /// <summary>`Statics.cs:98` — degrees to radians.</summary>
        static double Deg2Rad(double deg) { return deg * (PI / 180.0); }

        static double Abs(double x) { return x < 0.0 ? -x : x; }

        /// <summary>
        /// Finite = neither NaN nor an infinity. `Statics.IsFinite` by value; spelled out rather than
        /// written as a comparison, because a comparison against a NaN is false in BOTH directions and
        /// that is how an out-of-domain value gets read as in-domain.
        /// </summary>
        static bool Finite(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }

        static string Fmt(double v) { return v.ToString("F4"); }
    }
}
