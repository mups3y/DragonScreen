// LaunchWindowTest — register S215. The launch-window maths, against a KNOWN TARGET AND EPOCH.
//
// ---- ⛔ THE S167 RULE IS THE WHOLE DESIGN OF THIS SUITE ----
// S215's brief: *"mutation-prove the window maths against a known target and epoch — a launch-time
// calculation that cannot fail on a wrong LAN is not evidence."* A suite that fed one plausible orbit
// in and asserted the answer was "about right" would pass just as happily against a function that
// ignored the LAN entirely, which is the single most likely way for this to be wrong: the LAN is the
// ONLY input that distinguishes "the right inclination" from "the right ORBIT", and getting it wrong
// produces a launch that looks perfect and cannot rendezvous (`docs/MECHJEB_MASTER_MAP.md` §7.5).
// So the cases below are chosen so that each one FAILS under a specific, named way of being wrong:
//   • `LanIsLoadBearing` — a wrong LAN moves the answer by a stated, computed amount;
//   • `KnownGeometry` — three closed-form geometries whose answer can be derived by hand;
//   • `NorthOrSouth` — the two ground tracks, and that the SOONER one is returned;
//   • `Q2Disagreement` / `Q4NoTarget` / `NotComputable` — every refusal, asserted as a refusal;
//   • `MutationProof` — the assertions that kill a constant-returning implementation, by name.
//
// ---- ⚠ THE LITERAL PINS ----
// `MaxDisagreementDeg` (1.0) and `IgnitionLeadSeconds` (3.0) are written out by hand below, once each,
// so a suite derived from the constant cannot fail to notice the constant changing.
using System;
using DragonScreen;

public static class LaunchWindowTest
{
    static int checks = 0, failures = 0;
    static void Check(bool ok, string what)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL: " + what); } }

    // ---- THE KNOWN TARGET AND EPOCH ----------------------------------------------------------
    // Earth-like, because that is what RSS gives us and what every number in this project is stated
    // against. The values are the ones this repo already carries, not new ones:
    //   • rotation period 86164.0905 s — the sidereal day. `Astro.TimeToPlane`'s last line divides the
    //     angle by TAU and multiplies by exactly this, so it is the natural unit for every answer here.
    //   • latitude 28.6083° — LC-39A, and `docs/BUILD_PLAN.md` §B8 states the constraint it creates:
    //     "can't be < launch-site latitude 28.6°".
    //   • inclination 51.6316° — the ISS, the shipped `mechjeb_settings_type_Crew-Dragon.cfg` value.
    const double DayS = 86164.0905;
    const double PadLatDeg = 28.6083;
    const double IssIncDeg = 51.6316;
    const double Epoch = 1.0e6;          // an arbitrary but FIXED UT; every answer below is relative.

    /// <summary>Degrees of body rotation, as seconds. The unit every expectation is written in.</summary>
    static double Deg(double d) { return d / 360.0 * DayS; }

    public static int Run()
    {
        Console.WriteLine("LaunchWindowTest (S215) — the launch window, against a known target and epoch");
        checks = 0; failures = 0;

        KnownGeometry();
        LanIsLoadBearing();
        NorthOrSouth();
        RetrogradeBody();
        Degenerate();
        Q4NoTarget();
        Q2Disagreement();
        NotComputable();
        MirrorGuard();
        MutationProof();
        IgnitionLead();

        Console.WriteLine("  " + checks + " checks, " + failures + " failures");
        return failures;
    }

    static bool Near(double a, double b, double tol) { double d = a - b; return (d < 0 ? -d : d) <= tol; }

    // ── 1. THREE CLOSED-FORM GEOMETRIES ─────────────────────────────────────────────────────────
    // Each is derivable by hand from `Astro.TimeToPlane`'s own algebra, so the expected value is not
    // "whatever the code printed the first time" — which is the other way this suite could have been
    // written and would have proven nothing.
    static void KnownGeometry()
    {
        // The pad's own ascending node RIGHT NOW, for a northgoing launch:
        //   angleEastOfAN = asin(tan(lat)/tan(inc)),  lanNow = celestialLongitude − angleEastOfAN.
        double aean = Math.Asin(Math.Tan(PadLatDeg * Math.PI / 180.0)
                              / Math.Tan(IssIncDeg * Math.PI / 180.0)) * 180.0 / Math.PI;

        // (a) THE PAD IS ALREADY UNDER THE PLANE. With celestialLongitude = 0, lanNow = −aean; ask for
        //     exactly that LAN and the wait is ZERO — the window is now.
        double t = LaunchWindow.TimeToPlaneS(DayS, PadLatDeg, 0.0, -aean, IssIncDeg);
        Check(Near(t, 0.0, 1e-6), "the pad already under the plane ⇒ zero wait (got " + t + " s)");

        // (b) A QUARTER TURN AWAY is a quarter of a sidereal day, exactly. Nothing about the vehicle
        //     enters this: the site is carried to the plane by the PLANET, so the answer is an angle
        //     divided by a rotation rate.
        t = LaunchWindow.TimeToPlaneS(DayS, PadLatDeg, 0.0, -aean + 90.0, IssIncDeg);
        Check(Near(t, Deg(90.0), 1e-6), "90° short of the plane ⇒ a quarter sidereal day (got " + t + " s)");

        // (c) ONE DEGREE PAST IT WRAPS THE LONG WAY ROUND — 359°, not −1°. `Clamp2Pi` is what makes the
        //     answer a WAIT rather than a signed offset, and a missing wrap is a sign error that would
        //     hand `WarpToUT` a UT in the past.
        t = LaunchWindow.TimeToPlaneS(DayS, PadLatDeg, 0.0, -aean - 1.0, IssIncDeg);
        Check(Near(t, Deg(359.0), 1e-6), "1° past the plane ⇒ wait 359°, never a negative time (got " + t + " s)");
    }

    // ── 2. ⛔ THE LAN IS LOAD-BEARING ────────────────────────────────────────────────────────────
    // THE CENTRAL CASE OF THIS SUITE. If `TimeToPlaneS` ignored its `lan` argument — the exact defect
    // that produces "right inclination, wrong RAAN" — every one of these would collapse to one value.
    static void LanIsLoadBearing()
    {
        double prev = -1.0;
        for (int k = 0; k < 8; k++)
        {
            double lan = k * 45.0;
            double t = LaunchWindow.TimeToPlaneS(DayS, PadLatDeg, 10.0, lan, IssIncDeg);

            // Each 45° step of LAN moves the window by exactly 45° of rotation, modulo a full turn.
            if (k > 0)
            {
                double step = t - prev; if (step < 0.0) step += DayS;
                Check(Near(step, Deg(45.0), 1e-6),
                      "a 45° LAN step moves the window by 45° of rotation (k=" + k + ", got " + step + " s)");
            }
            prev = t;
        }

        // ...and stated once more as the blunt property, so the reason the loop exists cannot be lost:
        double a = LaunchWindow.TimeToPlaneS(DayS, PadLatDeg, 10.0,  30.0, IssIncDeg);
        double b = LaunchWindow.TimeToPlaneS(DayS, PadLatDeg, 10.0, 210.0, IssIncDeg);
        Check(!Near(a, b, 1.0), "⛔ two LANs 180° apart give DIFFERENT windows — the LAN is read, not ignored");
        Check(Near(b - a < 0 ? b - a + DayS : b - a, Deg(180.0), 1e-6),
              "...and they are exactly half a sidereal day apart");
    }

    // ── 3. NORTHGOING vs SOUTHGOING, AND WHICH ONE COMES FIRST ──────────────────────────────────
    static void NorthOrSouth()
    {
        // `MinimumTimeToPlane` must return the SOONER of the two tracks, and the inclination SIGN that
        // flies it. ⭐ This is what settles T18's Q1: the sign is a RESULT, not an inherited cfg value.
        for (int k = 0; k < 12; k++)
        {
            double lan = k * 30.0;
            double north = LaunchWindow.TimeToPlaneS(DayS, PadLatDeg, 0.0, lan,  IssIncDeg);
            double south = LaunchWindow.TimeToPlaneS(DayS, PadLatDeg, 0.0, lan, -IssIncDeg);
            double t, inc;
            LaunchWindow.MinimumTimeToPlane(DayS, PadLatDeg, 0.0, lan, IssIncDeg, out t, out inc);

            double best = north < south ? north : south;
            Check(Near(t, best, 1e-9), "the SOONER track is chosen (lan=" + lan + ")");
            Check(Near(Math.Abs(inc), IssIncDeg, 1e-9), "the magnitude is the target's (lan=" + lan + ")");
            Check((inc > 0.0) == (north < south),
                  "the SIGN says which track: + northgoing, − southgoing (lan=" + lan + ")");
        }

        // ⭐ A NEGATIVE INPUT INCLINATION CANNOT CHANGE THE ANSWER. The vendored `MinimumTimeToPlane`
        // takes `Abs(inc)` of its argument before evaluating either branch (`Astro.cs:456-457`), so the
        // cfg's `-51.6316` and the catalog's `+51.6` are the same question. S214 proved the sign was
        // innocent for the SOLVER; this proves it is innocent for the WINDOW too.
        double t1, i1, t2, i2;
        LaunchWindow.MinimumTimeToPlane(DayS, PadLatDeg, 33.0, 77.0,  IssIncDeg, out t1, out i1);
        LaunchWindow.MinimumTimeToPlane(DayS, PadLatDeg, 33.0, 77.0, -IssIncDeg, out t2, out i2);
        Check(Near(t1, t2, 1e-12) && Near(i1, i2, 1e-12),
              "⛔ a negative TARGET inclination gives an identical window — the input sign is stripped");
    }

    // ── 4. A BACKWARDS-ROTATING BODY ────────────────────────────────────────────────────────────
    static void RetrogradeBody()
    {
        // `Astro.TimeToPlane` negates `lanDiff` when the period is negative, and takes `Abs` of the
        // period for the divide. Both matter: the answer must stay a positive WAIT.
        for (int k = 0; k < 8; k++)
        {
            double lan = k * 45.0;
            double t = LaunchWindow.TimeToPlaneS(-DayS, PadLatDeg, 20.0, lan, IssIncDeg);
            Check(t >= 0.0 && t <= DayS, "a retrograde body still gives a wait in [0, one day] (lan=" + lan + ")");
        }
        double fwd = LaunchWindow.TimeToPlaneS( DayS, PadLatDeg, 20.0, 100.0, IssIncDeg);
        double rev = LaunchWindow.TimeToPlaneS(-DayS, PadLatDeg, 20.0, 100.0, IssIncDeg);
        Check(!Near(fwd, rev, 1.0), "⛔ rotation DIRECTION changes the window — the sign of the period is read");
        Check(Near(fwd + rev, DayS, 1e-6), "...and the two waits sum to one full rotation");
    }

    // ── 5. THE DEGENERATE GEOMETRIES THE VENDORED SOURCE NAMES ──────────────────────────────────
    static void Degenerate()
    {
        // Both come straight from `Astro.TimeToPlane`'s own guard clauses. Mirroring the guards without
        // testing them would leave two branches that have never once been executed.
        Check(Near(LaunchWindow.TimeToPlaneS(DayS, 90.0, 40.0, 100.0, IssIncDeg), 0.0, 1e-12),
              "at the pole, tan(lat) is infinite — the vendored guard returns 0");
        Check(Near(LaunchWindow.TimeToPlaneS(DayS, PadLatDeg, 40.0, 100.0, 0.0), 0.0, 1e-12),
              "an equatorial plane has no node to wait for — the vendored guard returns 0");
        Check(Near(LaunchWindow.TimeToPlaneS(DayS, PadLatDeg, 40.0, 100.0, 180.0), 0.0, 1e-12),
              "...and so does a 180° (retrograde equatorial) plane");

        // ⚠ THE CLAMPED ASIN. `SafeAsin` clamps to [-1, 1], which is what lets an inclination BELOW the
        // launch latitude return an answer at all rather than a NaN. §B8: "can't be < launch-site
        // latitude 28.6°" — the geometry is unreachable, and the vendored code degrades rather than
        // throwing. This asserts we degrade the SAME way, because a NaN here would reach `WarpToUT`.
        double t = LaunchWindow.TimeToPlaneS(DayS, PadLatDeg, 0.0, 45.0, 10.0);
        Check(!double.IsNaN(t), "an inclination below the launch latitude clamps rather than NaN-ing");
    }

    // ── 6. Q4 — NO TARGET, NO WINDOW ────────────────────────────────────────────────────────────
    static void Q4NoTarget()
    {
        WindowInputs s = Iss();
        s.TargetInSameSoi = false;
        WindowPlan p = LaunchWindow.Solve(s);
        Check(p.Verdict == WindowVerdict.NoTarget, "⛔ no target in the SoI ⇒ NoTarget");
        Check(!p.Armed, "...and it is NOT armed");
        Check(p.LaunchUT == 0.0 && p.TimeToWindowS == 0.0,
              "...and it returns no time at all — a refusal must not also hand back a plausible number");

        // ⛔ THE ORDER OF THE REFUSALS. With no target, the plane fields are stale garbage. The verdict
        // must still be NoTarget — not a disagreement computed FROM the garbage, which would send a
        // reader chasing the wrong fault.
        s.TargetLanDeg = 12345.0; s.TargetInclinationDeg = -999.0;
        Check(LaunchWindow.Solve(s).Verdict == WindowVerdict.NoTarget,
              "...and NoTarget is decided BEFORE the plane fields are believed");
    }

    // ── 7. Q2 — A MATERIAL DISAGREEMENT IS AN ERROR ─────────────────────────────────────────────
    static void Q2Disagreement()
    {
        // THE LITERAL PIN — 1.0 by hand, not read off the constant.
        Check(LaunchWindow.MaxDisagreementDeg == 1.0, "the disagreement tolerance is 1.0°");

        // The two ISS values this repo actually carries must BOTH pass — that is the tolerance's floor,
        // and a tolerance that failed here would hold a perfectly correct launch.
        Check(LaunchWindow.InclinationAgrees(-51.6316, 51.6, LaunchWindow.MaxDisagreementDeg),
              "the cfg's −51.6316 and the catalog's +51.6 AGREE (the repo's own two ISS values)");

        // ⛔ THE CASE IT EXISTS TO CATCH, and it is a real one: `REAL_CREW_DRAGON_MISSION.md` §3 records
        // a station at 0.133° against a mission fact of 51.6°.
        Check(!LaunchWindow.InclinationAgrees(0.133, 51.6, LaunchWindow.MaxDisagreementDeg),
              "⛔ a 0.133° station against a 51.6° mission DISAGREES");

        WindowInputs s = Iss();
        s.TargetInclinationDeg = 0.133;
        WindowPlan p = LaunchWindow.Solve(s);
        Check(p.Verdict == WindowVerdict.InclinationDisagrees, "⛔ ...and Solve refuses it");
        Check(!p.Armed, "...and does not arm");
        Check(p.Reason != null && p.Reason.Length > 0, "...and says why (it must SURFACE, not just hold)");

        // Right at the edge, both sides. A tolerance that is not actually applied passes one of these.
        Check(LaunchWindow.InclinationAgrees(52.6, 51.6, 1.0),  "exactly at the tolerance AGREES");
        Check(!LaunchWindow.InclinationAgrees(52.7, 51.6, 1.0), "just past the tolerance DISAGREES");

        // ⛔ NON-FINITE DISAGREES. It must not slip through as "close enough" — a NaN comparison is
        // false in both directions, which is exactly how one gets read as in-domain.
        Check(!LaunchWindow.InclinationAgrees(double.NaN, 51.6, 1.0), "NaN DISAGREES");
        Check(!LaunchWindow.InclinationAgrees(double.PositiveInfinity, 51.6, 1.0), "+∞ DISAGREES");
        Check(!LaunchWindow.InclinationAgrees(51.6, double.NaN, 1.0), "a NaN mission fact DISAGREES");
    }

    // ── 8. THE INPUTS THAT ARE NOT NUMBERS ──────────────────────────────────────────────────────
    static void NotComputable()
    {
        WindowInputs s = Iss(); s.BodyRotationPeriodS = 0.0;
        Check(LaunchWindow.Solve(s).Verdict == WindowVerdict.NotComputable,
              "⛔ a non-rotating body has no plane crossing — refuse, do not divide by it");

        s = Iss(); s.TargetLanDeg = double.NaN;
        Check(LaunchWindow.Solve(s).Verdict == WindowVerdict.NotComputable, "⛔ a NaN LAN is refused");
        s = Iss(); s.NowUT = double.PositiveInfinity;
        Check(LaunchWindow.Solve(s).Verdict == WindowVerdict.NotComputable, "⛔ an infinite UT is refused");
        s = Iss(); s.LatitudeDeg = double.NaN;
        Check(LaunchWindow.Solve(s).Verdict == WindowVerdict.NotComputable, "⛔ a NaN latitude is refused");

        // And the happy path still arms, or the four above would prove only that Solve can say no.
        WindowPlan ok = LaunchWindow.Solve(Iss());
        Check(ok.Armed, "⭐ ...and a good ISS window DOES arm");
        Check(ok.LaunchUT > Epoch && ok.LaunchUT <= Epoch + DayS,
              "...with a T-0 inside the next sidereal day");
        Check(Near(ok.LaunchUT, Epoch + ok.TimeToWindowS, 1e-9), "...and LaunchUT = now + timeToWindow");
    }

    // ── 9. THE MIRROR'S OWN GUARD ───────────────────────────────────────────────────────────────
    static void MirrorGuard()
    {
        Check(LaunchWindow.MirrorToleranceS == 1e-3, "the mirror-agreement epsilon is 1 ms");
        Check(LaunchWindow.MirrorAgrees(1234.5, 1234.5, 1e-3), "identical answers agree");
        Check(LaunchWindow.MirrorAgrees(1234.5, 1234.5004, 1e-3), "a sub-millisecond difference agrees");
        Check(!LaunchWindow.MirrorAgrees(1234.5, 1234.6, 1e-3), "⛔ a 100 ms difference DISAGREES");
        Check(!LaunchWindow.MirrorAgrees(double.NaN, 1234.5, 1e-3), "⛔ a NaN mirror DISAGREES");
        Check(!LaunchWindow.MirrorAgrees(1234.5, double.NaN, 1e-3), "⛔ a NaN vendored answer DISAGREES");
    }

    // ── 10. ⛔ MUTATION PROOF — the assertions that kill a faked implementation, by name ─────────
    static void MutationProof()
    {
        // (M) `InclinationAgrees => true`  — killed by every `!Agrees` case in Q2Disagreement, and
        //     stated here as one blunt executable claim so the kill cannot be lost in a refactor.
        Check(!LaunchWindow.InclinationAgrees(0.0, 90.0, 1.0),
              "MUTANT `InclinationAgrees => true` is killed: 0° vs 90° must disagree");
        // (M) `InclinationAgrees => false` — killed by the repo's own two ISS values agreeing.
        Check(LaunchWindow.InclinationAgrees(51.6, 51.6, 1.0),
              "MUTANT `InclinationAgrees => false` is killed: a value must agree with itself");

        // (M) `TimeToPlaneS => 0` (or any constant) — killed by two inputs giving different answers,
        //     which is the property `LanIsLoadBearing` proves in detail and this restates as a claim.
        double a = LaunchWindow.TimeToPlaneS(DayS, PadLatDeg, 0.0,  10.0, IssIncDeg);
        double b = LaunchWindow.TimeToPlaneS(DayS, PadLatDeg, 0.0, 190.0, IssIncDeg);
        Check(!Near(a, b, 1.0), "MUTANT `TimeToPlaneS => constant` is killed: two LANs, two answers");

        // (M) `MinimumTimeToPlane` always returns the NORTH branch — killed by finding a LAN where
        //     south is sooner. If no such LAN existed the check below would fail and say so, which is
        //     itself the evidence: a suite that cannot find a southgoing case has not tested one.
        bool sawSouth = false, sawNorth = false;
        for (int k = 0; k < 36; k++)
        {
            double t, inc;
            LaunchWindow.MinimumTimeToPlane(DayS, PadLatDeg, 0.0, k * 10.0, IssIncDeg, out t, out inc);
            if (inc < 0.0) sawSouth = true; else sawNorth = true;
        }
        Check(sawSouth, "MUTANT `always northgoing` is killed: a southgoing window exists and is chosen");
        Check(sawNorth, "MUTANT `always southgoing` is killed: a northgoing window exists and is chosen");

        // (M) `Solve => Armed` — killed by each refusal above; restated as the blunt claim.
        WindowInputs s = Iss(); s.TargetInSameSoi = false;
        Check(!LaunchWindow.Solve(s).Armed, "MUTANT `Solve => always Armed` is killed by the no-target case");
        // (M) `Solve => never Armed` — the mirror image, and the one that would silently ground us.
        Check(LaunchWindow.Solve(Iss()).Armed, "MUTANT `Solve => never Armed` is killed by the good case");
    }

    // ── 11. THE IGNITION LEAD, AND THE PAD HOLD IT PRODUCES ─────────────────────────────────────
    static void IgnitionLead()
    {
        // THE LITERAL PIN — 3.0 by hand. `CREW_MISSION_TELEMETRY.md`: "−0:00:03 | Engine controller
        // commands ignition sequence start".
        Check(AscentInputs.IgnitionLeadSeconds == 3.0, "the ignition lead is the documented 3 s");

        // ⭐ THE COUNTDOWN HOLD, END TO END. A GO with guidance ready and a window still minutes away
        // must NOT light the stage.
        AscentInputs s = AscentInputs.Nominal();
        s.LaunchCommanded = true; s.GuidanceReady = true;
        s.WindowRequired = true; s.WindowArmed = true; s.SecondsToWindowS = 600.0;
        AscentDecision d = AscentSequence.Step(s, AscentStep.Idle);
        Check(d.Next == AscentStep.Idle && d.Act == AscentAct.None,
              "⛔ 10 minutes from the window, a launch GO does NOT light the octaweb");

        // ...and at the lead it does.
        s.SecondsToWindowS = 3.0;
        d = AscentSequence.Step(s, AscentStep.Idle);
        Check(d.Next == AscentStep.Ignition && d.Act == AscentAct.IgniteStageOne,
              "⭐ at T-3 s exactly, the octaweb is commanded alight");

        // One tick either side of the lead, so the comparison itself is proven and not merely present.
        s.SecondsToWindowS = 3.001;
        Check(AscentSequence.Step(s, AscentStep.Idle).Next == AscentStep.Idle,
              "just before the lead, it still holds");

        // ⚠ A PASSED WINDOW IS NOT A DEADLOCK. Negative time-to-window falls through to ignition rather
        // than holding the pad forever waiting for a moment in the past.
        s.SecondsToWindowS = -5.0;
        Check(AscentSequence.Step(s, AscentStep.Idle).Act == AscentAct.IgniteStageOne,
              "a window already passed ignites rather than hanging the countdown");

        // ⛔ REQUIRED BUT NOT ARMED IS A HOLD — the case that stops a rendezvous mission launching blind.
        s = AscentInputs.Nominal();
        s.LaunchCommanded = true; s.GuidanceReady = true;
        s.WindowRequired = true; s.WindowArmed = false;
        d = AscentSequence.Step(s, AscentStep.Idle);
        Check(d.Next == AscentStep.Idle && d.Act == AscentAct.None,
              "⛔ a rendezvous mission with NO window solved HOLDS on the pad");

        // ...and a free-flyer, which has no plane to wait for, still launches on the GO exactly as it
        // did before S215. ⭐ THE REGRESSION THIS FEATURE COULD MOST EASILY HAVE CAUSED.
        s.WindowRequired = false;
        d = AscentSequence.Step(s, AscentStep.Idle);
        Check(d.Next == AscentStep.Ignition && d.Act == AscentAct.IgniteStageOne,
              "⭐ a free-flyer (no rendezvous) launches on the GO, unchanged by S215");

        // ⛔ AND S214's HOLD STILL WINS. The window must never be able to light a stage that nobody
        // will throttle — the two holds compose, they do not replace each other.
        s = AscentInputs.Nominal();
        s.LaunchCommanded = true; s.GuidanceReady = false;
        s.WindowRequired = true; s.WindowArmed = true; s.SecondsToWindowS = 0.0;
        Check(AscentSequence.Step(s, AscentStep.Idle).Act == AscentAct.None,
              "⛔ at T-0 with no guidance solution, S214's hold still refuses the light");

        // ⭐ AND `Nominal()` STILL MEANS "LAUNCH ON THE GO". If this ever flipped, every pre-S215 test
        // in AscentSequenceTest would be quietly asserting a countdown instead of an ignition.
        AscentInputs n = AscentInputs.Nominal();
        Check(!n.WindowArmed && !n.WindowRequired,
              "Nominal() arms no window and requires none — the honest pre-S215 default");
    }

    // The known-good ISS window inputs every Solve case starts from.
    static WindowInputs Iss()
    {
        WindowInputs s = new WindowInputs();
        s.TargetInSameSoi = true;
        s.BodyRotationPeriodS = DayS;
        s.LatitudeDeg = PadLatDeg;
        s.CelestialLongitudeDeg = 137.0;    // arbitrary but FIXED — the epoch's other half
        s.TargetLanDeg = 42.0;              // ditto: a known LAN, so every answer above is reproducible
        s.TargetInclinationDeg = IssIncDeg;
        s.LanDifferenceDeg = 0.0;           // MASTER_MAP §7.5: "0 for the exact plane"
        s.NowUT = Epoch;
        s.MissionInclinationDeg = 51.6;     // the catalog's ISS value
        return s;
    }
}
