// RendezvousOpsTest — register T19. §B11's approach geometry, §B10.2's op parameters, and §B12.4's
// numbers — plus the half that can only be proved against the PINNED MECHJEB TREE ITSELF.
//
// ---- THE TWO HALVES, AND WHY THE SECOND ONE MATTERS MORE THAN IT LOOKS ----
// (1) The geometry and the thresholds: the waypoint ladder, when a leg has arrived, εd, tol, drift.
// (2) ⭐ THE VENDORED-SOURCE CHECKS. Two claims in `MechOps` are claims about somebody else's code:
//     that each `ConductorOp` names a class that EXISTS, and that each operation's default
//     `TimeReference` — the first entry of its own `_timeReferences` array, since `_currentTimeRef`
//     starts at 0 — is the one §B9/§B10.2 asks for. The conductor cannot SET a TimeSelector (every
//     Operation holds it in a `private static readonly` field), so it relies on those defaults. A
//     re-pin that reorders one array would silently reschedule a rendezvous burn to a different node
//     and nothing else in the tree would notice. These checks read the pinned source and notice.
//     Same idiom as `MechHostTest`, and for the same reason.
//
// ⛔ WHAT THIS SUITE CANNOT DO. It does not build a node, does not run an Operation, and does not
// touch a vessel. `MakeNodes` needs an `Orbit` and a live target; that is `src/MechConductor.cs` and
// it is the sim's to prove.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DragonScreen;

public static class RendezvousOpsTest
{
    static int checks = 0, failures = 0;
    static void Check(bool ok, string what)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL: " + what); } }

    // plugin/build/DragonScreenTest.exe -> "../.." = the repo root. The MechHostTest idiom.
    static string Repo(params string[] parts)
    {
        var bits = new List<string> {
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), "..", ".." };
        bits.AddRange(parts);
        return Path.GetFullPath(Path.Combine(bits.ToArray()));
    }

    public static int Run()
    {
        Console.WriteLine("RendezvousOpsTest (T19: §B11 approach geometry, §B10.2 op params, §B12.4 numbers)");
        checks = 0; failures = 0;

        PublishedGeometry();
        Legs();
        TheLadder();
        Arrival();
        HandOff();
        ReplanNumbers();
        ChainEnds();
        VendoredOperationClasses();
        VendoredTimeReferences();

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures;
    }

    // ---- 1. §B11's PUBLISHED GEOMETRY, WRITTEN OUT BY HAND ---------------------------------------
    // ⛔ The literal pin the batch asked for: these are NOT read from the constants they check.
    static void PublishedGeometry()
    {
        Check(RendezvousOps.ApproachEllipsoidM == 4000.0,
              "the Approach Ellipsoid is 4 km — §B11 'Approach Ellipsoid = 4 x 2 km egg zone'");
        Check(RendezvousOps.GoNoGoHoldM == 1000.0,
              "the Go/No-Go hold is 1 km — §B11 'Dragon halts at ~1 km for the Go/No-Go'");
        Check(RendezvousOps.KeepOutSphereM == 200.0,
              "the Keep-Out Sphere is 200 m — §B11 'Keep-Out Sphere = 200 m'");
        Check(RendezvousOps.Waypoint0M == 400.0, "WP0 is 400 m — §B11 'WP0 = 400 m below'");
        Check(RendezvousOps.Waypoint1M == 220.0, "WP1 is 220 m — §B11 'WP1 (docking axis) = 220 m ahead'");
        Check(RendezvousOps.Waypoint2M == 20.0,  "WP2 is 20 m — §B11 'WP2 = 20 m from the port'");
        Check(RendezvousOps.NodeResidualToleranceMps == 0.1,
              "the node residual tolerance is 0.1 m/s — §B10.1 'MechJeb stock default 0.1'");
        Check(RendezvousOps.FineNodeResidualToleranceMps == 0.05,
              "...tightened to 0.05 for the fine corrections — §B10.1's own words");
        Check(RendezvousOps.NulledRelativeSpeedMps == 0.2,
              "the matched-velocity rate is 0.2 m/s — §B11's 'rate must stay < 0.2 m/s inside 5 m', "
              + "borrowed and marked as borrowed");

        // ⭐ AND THE GEOMETRY AGREES WITH THE GATE CATALOG, which is a SECOND in-repo source for the
        // same three numbers. If either drifts, this fails.
        MissionProfile m = Missions.Resolve("Crew-2");
        Check(CrewGates.ById(m, GateId.WP0HoldG10).Title.Contains("400 m"),
              "gate G10's own title still says 400 m");
        Check(CrewGates.ById(m, GateId.WP1HoldG11).Title.Contains("220 m"),
              "gate G11's own title still says 220 m");
        Check(CrewGates.ById(m, GateId.WP2DockGoG12).Title.Contains("20 m"),
              "gate G12's own title still says 20 m");
    }

    // ---- 2. WHICH LEG, FROM THE GATE IT WALKS TO -------------------------------------------------
    static void Legs()
    {
        Check(RendezvousOps.LegFor(GateId.ApproachInitGoG9) == RendezvousLeg.Phasing, "G9 -> the phasing leg");
        Check(RendezvousOps.LegFor(GateId.WP0HoldG10) == RendezvousLeg.ToWaypoint0, "G10 -> the WP0 leg");
        Check(RendezvousOps.LegFor(GateId.WP1HoldG11) == RendezvousLeg.ToWaypoint1, "G11 -> the WP1 leg");
        Check(RendezvousOps.LegFor(GateId.WP2DockGoG12) == RendezvousLeg.ToWaypoint2, "G12 -> the WP2 leg");

        // ⛔ EVERY OTHER GATE IS `None`, AND A `None` LEG FLIES NOTHING. The conductor must never
        // guess which waypoint it is aiming at — swept over the whole enum so a new gate cannot
        // silently acquire a waypoint.
        GateId[] all = (GateId[])Enum.GetValues(typeof(GateId));
        int named = 0;
        for (int i = 0; i < all.Length; i++)
            if (RendezvousOps.LegFor(all[i]) != RendezvousLeg.None) named++;
        Check(named == 4, "exactly four gates name a leg (got " + named + " of " + all.Length + ")");
        Check(RendezvousOps.LegFor(GateId.DockingCompleteG13) == RendezvousLeg.None,
              "⛔ G13 (docking complete) names NO approach leg — inside the KOS is T20's");
        Check(RendezvousOps.LegFor(GateId.DeorbitGoG15) == RendezvousLeg.None, "...nor does the deorbit gate");

        Check(RendezvousOps.TargetRangeM(RendezvousLeg.ToWaypoint0) == 400.0, "the WP0 leg targets 400 m");
        Check(RendezvousOps.TargetRangeM(RendezvousLeg.ToWaypoint2) == 20.0,  "the WP2 leg targets 20 m");
        Check(RendezvousOps.TargetRangeM(RendezvousLeg.Phasing) == 0.0,
              "⛔ the phasing leg has NO range target — it ends on its orbit, not on a distance");
    }

    // ---- 3. THE INTERCEPT LADDER — §B10.2 "walk down ON LATER PASSES" -----------------------------
    // ⛔ THE RUNG IS CHOSEN BY THE PASS, NOT BY THE RANGE. A range-driven ladder stalls at its own rung
    // boundary — aim at 4 km, land at 4.3 km, pick 4 km again, forever — and every burn looks fine
    // while the approach never descends. `MissionWalkTest` found that; this is the corrected rule.
    static void TheLadder()
    {
        // Pass 0 aims at the Approach Ellipsoid, wherever the vehicle is.
        Check(RendezvousOps.InterceptDistanceM(RendezvousLeg.ToWaypoint0, 250000.0, 0) == 4000.0,
              "pass 0 aims at the 4 km ellipsoid from 250 km out");
        Check(RendezvousOps.InterceptDistanceM(RendezvousLeg.ToWaypoint0, 4300.0, 0) == 4000.0,
              "...and still at 4 km from 4.3 km out");
        // ⭐ AND THIS IS THE ONE THAT WOULD HAVE STALLED: same range, next pass, LOWER rung.
        Check(RendezvousOps.InterceptDistanceM(RendezvousLeg.ToWaypoint0, 4300.0, 1) == 1000.0,
              "⭐ pass 1 at the SAME 4.3 km range drops to the 1 km hold — the range-driven version "
              + "picked 4 km again here and the approach never descended");
        Check(RendezvousOps.InterceptDistanceM(RendezvousLeg.ToWaypoint0, 4300.0, 2) == 400.0,
              "pass 2 drops to the leg's own waypoint");
        Check(RendezvousOps.InterceptDistanceM(RendezvousLeg.ToWaypoint0, 4300.0, 9) == 400.0,
              "...and stays there — further passes never aim inside the waypoint");

        // ⛔ GUARD 1: never aim FURTHER OUT than the vehicle already is.
        Check(RendezvousOps.InterceptDistanceM(RendezvousLeg.ToWaypoint2, 150.0, 0) == 150.0,
              "⛔ a leg that starts at 150 m is NOT told to fly back out to 4 km");
        Check(RendezvousOps.InterceptDistanceM(RendezvousLeg.ToWaypoint1, 900.0, 0) == 900.0,
              "...nor is one at 900 m");

        // ⛔ GUARD 2: never aim INSIDE the leg's own waypoint — that is the next leg's job.
        Check(RendezvousOps.InterceptDistanceM(RendezvousLeg.ToWaypoint0, 50.0, 5) == 400.0,
              "⛔ the WP0 leg never aims inside 400 m, even from 50 m and five passes in");
        Check(RendezvousOps.InterceptDistanceM(RendezvousLeg.ToWaypoint2, 5.0, 5) == 20.0,
              "...and the WP2 leg never aims inside 20 m");

        // ⭐ MONOTONE IN THE PASS, which is what makes the approach converge.
        double last = double.MaxValue; int rises = 0;
        for (int pass = 0; pass < 12; pass++)
        {
            double d = RendezvousOps.InterceptDistanceM(RendezvousLeg.ToWaypoint0, 250000.0, pass);
            if (d > last) rises++;
            last = d;
        }
        Check(rises == 0, "⛔ the ladder is monotone in the pass count — it never aims further out on a "
              + "later pass (" + rises + " rise(s))");
        Check(last == 400.0, "...and it bottoms out AT the waypoint, so the leg can actually arrive");
    }

    // ---- 4. ARRIVING AT A WAYPOINT ---------------------------------------------------------------
    // ⛔ BOTH conditions, not either. And the range band is εd — the SAME number §B12.4 judges a
    // missed burn by — because two independent numbers there is what stalls the approach.
    static void Arrival()
    {
        Check(RendezvousOps.LegComplete(RendezvousLeg.ToWaypoint0, 380.0, 0.05, 0.2),
              "inside 400 m with the velocity matched: the WP0 leg is done");
        Check(!RendezvousOps.LegComplete(RendezvousLeg.ToWaypoint0, 380.0, 3.0, 0.2),
              "⛔ inside 400 m still closing at 3 m/s is NOT an arrival, it is a collision in 90 s");
        Check(!RendezvousOps.LegComplete(RendezvousLeg.ToWaypoint0, 2000.0, 0.01, 0.2),
              "⛔ a matched velocity two kilometres out is NOT an arrival either");
        Check(!RendezvousOps.LegComplete(RendezvousLeg.Phasing, 10.0, 0.0, 0.2),
              "⛔ the phasing leg can never 'arrive' on a range — it has no range target");
        Check(!RendezvousOps.LegComplete(RendezvousLeg.ToWaypoint0, 0.0, 0.0, 0.2),
              "a zero range (no target acquired) is not an arrival");

        // ⭐⭐ THE COUPLING, AND THE 429 m CASE THAT FOUND IT. A burn aiming at the 400 m hold that
        // lands at 429 m is inside εd, so §B12.4 does not re-plan it; it must therefore COUNT as an
        // arrival, or the leg aims at 400 m forever and lands at 429 m forever.
        Check(RendezvousOps.LegComplete(RendezvousLeg.ToWaypoint0, 429.0, 0.05, 0.2),
              "⭐ 429 m at the 400 m hold IS an arrival — a miss too small to re-plan is an arrival");
        Check(RendezvousOps.ClosestApproachToleranceM(RendezvousLeg.ToWaypoint0) == 400.0
              && RendezvousOps.LegComplete(RendezvousLeg.ToWaypoint0, 800.0, 0.05, 0.2)
              && !RendezvousOps.LegComplete(RendezvousLeg.ToWaypoint0, 801.0, 0.05, 0.2),
              "⭐ the arrival band is EXACTLY target + εd (800 m at WP0) — one number, used twice");
        Check(RendezvousOps.LegComplete(RendezvousLeg.ToWaypoint1, 440.0, 0.05, 0.2)
              && !RendezvousOps.LegComplete(RendezvousLeg.ToWaypoint1, 441.0, 0.05, 0.2),
              "...and 440 m at WP1, which is the next leg tightening it by itself");

        // A caller may tighten the matched-velocity rate; zero means "use the published one".
        Check(!RendezvousOps.LegComplete(RendezvousLeg.ToWaypoint2, 15.0, 0.15, 0.05),
              "a caller-tightened rate refuses an arrival the published rate would have allowed");
        Check(RendezvousOps.LegComplete(RendezvousLeg.ToWaypoint2, 15.0, 0.15, 0.0),
              "...and 0 falls back to §B11's published 0.2 m/s");
    }

    // ---- 5. THE KEEP-OUT SPHERE HAND-OFF ---------------------------------------------------------
    static void HandOff()
    {
        Check(!RendezvousOps.InsideKeepOutSphere(201.0), "201 m is outside the Keep-Out Sphere");
        Check(RendezvousOps.InsideKeepOutSphere(200.0), "200 m is inside it");
        Check(RendezvousOps.InsideKeepOutSphere(20.0), "so is WP2");
        Check(!RendezvousOps.InsideKeepOutSphere(0.0),
              "⛔ a zero range is NOT 'inside' — no target acquired must never hand off to the docking AP");
        Check(!RendezvousOps.InsideKeepOutSphere(-5.0), "nor is a negative one");
    }

    // ---- 6. §B12.4's NUMBERS ---------------------------------------------------------------------
    static void ReplanNumbers()
    {
        // εd is DERIVED from the leg, so it scales by construction.
        Check(RendezvousOps.ClosestApproachToleranceM(RendezvousLeg.ToWaypoint0) == 400.0,
              "εd at WP0 is the leg's own 400 m");
        Check(RendezvousOps.ClosestApproachToleranceM(RendezvousLeg.ToWaypoint2) == 20.0,
              "εd at WP2 is 20 m — twenty times tighter, with no second constant to keep in step");
        Check(RendezvousOps.ClosestApproachToleranceM(RendezvousLeg.Phasing) == 0.0,
              "⛔ the phasing leg's εd is 0, which Conductor reads as 'this disjunct is off'");

        // ⭐ AND THAT ZERO REALLY DOES DISABLE IT — asserted against the real Conductor, not described.
        ConductorInputs s = new ConductorInputs();
        s.Phase = MissionPhase.Approach; s.ApproachStepsDone = 1;
        s.ClosestApproachErrM = 9.0e6;
        s.ClosestApproachTolM = RendezvousOps.ClosestApproachToleranceM(RendezvousLeg.Phasing);
        Check(Conductor.Decide(s).Verb != ConductorVerb.Replan,
              "a 9000 km closest-approach error with the phasing leg's (unset) tolerance does not re-plan");
        s.ClosestApproachTolM = RendezvousOps.ClosestApproachToleranceM(RendezvousLeg.ToWaypoint2);
        Check(Conductor.Decide(s).Verb == ConductorVerb.Replan,
              "...and the WP2 leg's 20 m tolerance re-plans it");

        // tol: 0.1 for the big burns, 0.05 from WP0 inward (§B10.1).
        Check(RendezvousOps.NodeResidualToleranceFor(RendezvousLeg.Phasing) == 0.1,
              "the phasing burns use §B10.1's 0.1 m/s");
        Check(RendezvousOps.NodeResidualToleranceFor(RendezvousLeg.ToWaypoint0) == 0.05,
              "the WP0 leg is a 'fine rendezvous correction' and uses 0.05");
        Check(RendezvousOps.NodeResidualToleranceFor(RendezvousLeg.ToWaypoint2) == 0.05,
              "...as does WP2");

        // drift: opening, faster than the matched rate, with range still to close.
        Check(RendezvousOps.Drifting(0.5, 800.0, 400.0, 0.2),
              "opening at 0.5 m/s with 800 m still to close is drift");
        Check(!RendezvousOps.Drifting(-0.5, 800.0, 400.0, 0.2),
              "closing at 0.5 m/s is not drift");
        Check(!RendezvousOps.Drifting(0.05, 800.0, 400.0, 0.2),
              "⛔ opening at 0.05 m/s is inside the matched rate and is NOT drift — orbital motion "
              + "would otherwise re-plan the whole approach on noise");
        Check(!RendezvousOps.Drifting(5.0, 300.0, 400.0, 0.2),
              "⛔ opening INSIDE the waypoint is not drift — the leg has arrived, backing off is the hold");
        Check(!RendezvousOps.Drifting(5.0, 800.0, 0.0, 0.2),
              "a leg with no range target has no drift to detect");
    }

    // ---- 6b. WHAT A LEG DOES WHEN ITS CHAIN RUNS OUT ---------------------------------------------
    // ⛔ Both answers strand a flight if swapped: a phasing leg that loops never raises G9, and an
    // approach leg that ends on its first pass raises the WP0 hold from four kilometres out.
    static void ChainEnds()
    {
        Check(RendezvousOps.OnChainComplete(RendezvousLeg.Phasing) == RendezvousOps.ChainEnd.CompleteLeg,
              "the phasing leg ends on its ORBIT — its chain IS the leg, so G9 comes up");
        Check(RendezvousOps.OnChainComplete(RendezvousLeg.ToWaypoint0) == RendezvousOps.ChainEnd.AnotherPass,
              "the WP0 leg runs the chain again — §B10.2's 'Run 1-2x to walk the closest approach down'");
        Check(RendezvousOps.OnChainComplete(RendezvousLeg.ToWaypoint1) == RendezvousOps.ChainEnd.AnotherPass,
              "...as does WP1");
        Check(RendezvousOps.OnChainComplete(RendezvousLeg.ToWaypoint2) == RendezvousOps.ChainEnd.AnotherPass,
              "...and WP2");

        // ⭐ AND ANOTHER PASS REALLY IS TIGHTER. If it were not, the second pass would aim where the
        // first one already did and the approach could never converge.
        double first  = RendezvousOps.InterceptDistanceM(RendezvousLeg.ToWaypoint0, 30000.0, 0);
        double second = RendezvousOps.InterceptDistanceM(RendezvousLeg.ToWaypoint0, 30000.0, 1);
        double third  = RendezvousOps.InterceptDistanceM(RendezvousLeg.ToWaypoint0, 30000.0, 2);
        Check(first > second && second > third,
              "three passes aim 4000 -> 1000 -> 400 m (got " + first + ", " + second + ", " + third + ")");
        Check(third == RendezvousOps.TargetRangeM(RendezvousLeg.ToWaypoint0),
              "...and the last rung IS the waypoint, so the leg can actually arrive");
    }

    // ---- 7. THE CLASS NAMES, AGAINST THE PINNED TREE ---------------------------------------------
    // ⛔ THE FINDING THIS CHECK EXISTS FOR: the file is `OperationTransfer.cs` and the class inside it
    // is `OperationGeneric`. §B10.2 and `pure/ConductorAction.cs` both say `OperationTransfer`.
    static void VendoredOperationClasses()
    {
        string dir = Repo("plugin", "mech", "MechJeb2", "Maneuver");
        if (!Directory.Exists(dir))
        {
            Console.WriteLine("  (plugin/mech not vendored here - tree-dependent checks skipped)");
            return;
        }

        // Every `public class Operation*` actually declared in the pinned tree.
        var declared = new List<string>();
        foreach (string f in Directory.GetFiles(dir, "*.cs"))
            foreach (Match mm in Regex.Matches(File.ReadAllText(f), @"public\s+class\s+(Operation\w+)\s*:\s*Operation\b"))
                declared.Add(mm.Groups[1].Value);
        Check(declared.Count > 10, "the pinned Maneuver directory declares " + declared.Count + " operations");

        Check(!declared.Contains("OperationTransfer"),
              "⛔ NO class called `OperationTransfer` exists in the pinned tree — §B10.2's own ⚠ "
              + "('verify exact C# class names vs the pinned MechJeb source') was right");
        Check(declared.Contains("OperationGeneric"),
              "⭐ the transfer is `OperationGeneric`, declared in the file named OperationTransfer.cs");

        for (int i = 0; i < MechOps.PlannerOps.Length; i++)
        {
            ConductorOp op = MechOps.PlannerOps[i];
            string cls = MechOps.ClassFor(op);
            Check(cls != null, op + " names a class");
            Check(declared.Contains(cls),
                  "the class `" + cls + "` behind ConductorOp." + op + " EXISTS in the pinned tree");
            string file = Path.Combine(dir, MechOps.FileFor(op));
            Check(File.Exists(file), "...and its file " + MechOps.FileFor(op) + " is where MechOps says");
            Check(File.ReadAllText(file).Contains("public class " + cls + " : Operation"),
                  "...and that file is where the class is actually declared");
        }

        // ⛔ The one the plan bans, and it must stay unnamed. §B1/§B12.4: MechJeb's rendezvous
        // AUTOPILOT is unreliable in RSS/RO, which is why the conductor composes planner ops itself.
        for (int i = 0; i < MechOps.PlannerOps.Length; i++)
            Check(MechOps.ClassFor(MechOps.PlannerOps[i]) != "MechJebModuleRendezvousAutopilot",
                  "no op maps to the rendezvous autopilot");
    }

    // ---- 8. THE DEFAULT TimeReference, AGAINST THE PINNED TREE -----------------------------------
    // ⭐ THE CONDUCTOR CANNOT SET ONE — every Operation holds its TimeSelector in a `private static
    // readonly` field — so it relies on the default, which is `_timeReferences[0]` because
    // `TimeSelector._currentTimeRef` starts at 0. A re-pin that reorders one of those arrays would
    // silently move a rendezvous burn to a different node. This is the check that notices.
    static void VendoredTimeReferences()
    {
        string dir = Repo("plugin", "mech", "MechJeb2", "Maneuver");
        if (!Directory.Exists(dir)) return;

        // Confirm the premise first: `_currentTimeRef` really does start at zero.
        string sel = Path.Combine(dir, "TimeSelector.cs");
        Check(File.Exists(sel), "TimeSelector.cs is in the pinned tree");
        if (File.Exists(sel))
        {
            string src = File.ReadAllText(sel);
            Check(Regex.IsMatch(src, @"public\s+int\s+_currentTimeRef\s*;"),
                  "⭐ `_currentTimeRef` is declared with NO initialiser, so it defaults to 0 — which is "
                  + "the whole premise of pinning the FIRST entry of each array");
            Check(src.Contains("public TimeReference TimeReference => _allowedTimeRef[_currentTimeRef];"),
                  "...and TimeReference is _allowedTimeRef[_currentTimeRef]");
        }

        for (int i = 0; i < MechOps.PlannerOps.Length; i++)
        {
            ConductorOp op = MechOps.PlannerOps[i];
            string want = MechOps.TimeReferenceFor(op);
            string file = Path.Combine(dir, MechOps.FileFor(op));
            if (!File.Exists(file)) { Check(false, "missing " + MechOps.FileFor(op)); continue; }

            Match arr = Regex.Match(File.ReadAllText(file),
                                    @"_timeReferences\s*=\s*\{\s*TimeReference\.(\w+)");
            Check(arr.Success, op + "'s file declares a _timeReferences array");
            if (!arr.Success) continue;
            Check(arr.Groups[1].Value == want,
                  "⭐ " + op + " defaults to TimeReference." + want + " — the one §B9/§B10.2 names "
                  + "(the pinned tree's first entry is " + arr.Groups[1].Value + ")");
        }
    }
}
