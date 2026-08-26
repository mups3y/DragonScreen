/*
 * DragonScreen headless tests - the ported MechJebLib FuelFlowSimulation (docs/MECHJEBLIB_PORT.md).
 *
 * ⛔ THIS IS THE TEST THAT PROVES THE PORT, not just that it compiled. It hand-builds a SimVessel (no
 * KSP - the whole point of the pure/glue split) and asks the simulation for the staged dv, then checks
 * it against the closed-form rocket equation dv = Isp * g0 * ln(m0/m1). If the H1 curve evaluation, the
 * V3 thrust rollup, the resource drain, or the segment accounting is wrong, this number is wrong.
 *
 * Two scenarios differ only in ambient pressure, so they also prove the AtmosphereCurve (H1) is being
 * evaluated by pressure: the same vehicle gets its vacuum Isp at p=0 and its sea-level Isp at p=1.
 */
using System;
using MechJebLib.FuelFlowSimulation;
using MechJebLib.FuelFlowSimulation.PartModules;
using MechJebLib.Primitives;

public static class FuelFlowTest
{
    static int checks = 0, failures = 0;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    const int FuelId = 1;

    // A single-stage rocket: one root part that is engine + tank. No decouplers. Every field the sim
    // reads is set explicitly, because a default of 0 on MultFlow / MultIsp / ThrottleLimiter / G /
    // FlowMultCap silently produces zero thrust (each of those was verified against the source).
    static SimVessel BuildSingleStage(double dry, double fuelUnits, double density,
                                      double ispVac, double ispSL, double maxFuelFlow)
    {
        SimVessel v = SimVessel.Borrow();
        v.SetCurrentStage(0);

        SimPart p = SimPart.Borrow(v, "rocket");
        p.IsRoot = true;
        p.IsEngine = true;
        p.InverseStage = 0;
        p.DecoupledInStage = int.MinValue;   // REQUIRED: DecouplingAnalyzer treats this as "unvisited"
        p.StagingOn = true;
        p.DryMass = dry;
        p.ResourceRequestRemainingThreshold = 0.0;

        var res = new SimResource { Id = FuelId, Free = false, Density = density, MaxAmount = fuelUnits };
        res.Amount = fuelUnits;
        p.Resources[FuelId] = res;

        v.Parts.Add(p);

        SimModuleEngines e = SimModuleEngines.Borrow(p);
        e.IsEnabled = true;
        e.G = 9.80665f;
        e.MaxFuelFlow = (float)maxFuelFlow;
        e.MinFuelFlow = 0f;
        e.ThrottleLimiter = 100f;            // 0 would zero the flow
        e.MultIsp = 1f;                      // 0 would zero the thrust
        e.MultFlow = 1.0;                    // 0 would zero the thrust
        e.FlowMultCap = 1e9f;                // low cap collapses the flow multiplier to ~0
        e.FlowMultCapSharpness = 1f;
        e.AtmosphereCurve.Add(0.0, ispVac);  // Isp at vacuum
        e.AtmosphereCurve.Add(1.0, ispSL);   // Isp at 1 atm
        e.ThrustDirectionVectors.Add(new V3(0.0, 1.0, 0.0));
        e.ThrustTransformMultipliers.Add(1.0);
        e.Propellants.Add(new SimPropellant(FuelId, false, 1.0, SimFlowMode.STAGE_PRIORITY_FLOW, density));
        p.Modules.Add(e);

        v.EnginesActivatedInStage[0].Add(e);
        v.EnginesDroppedInStage[-1].Add(e);

        DecouplingAnalyzer.Analyze(v);
        return v;
    }

    static double TotalDv(System.Collections.Generic.List<FuelStats> segs)
    {
        double dv = 0;
        for (int i = 0; i < segs.Count; i++) dv += segs[i].DeltaV;
        return dv;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen FuelFlowSimulation tests");

        const double dry = 1.0, fuel = 9.0, density = 1.0;   // m0 = 10 t, m1 = 1 t, ln(m0/m1) = ln 10
        const double g0 = 9.80665;
        double ln10 = Math.Log(10.0);

        // ---- VACUUM: dv must be the rocket equation with the VACUUM Isp ----
        SimVessel vac = BuildSingleStage(dry, fuel, density, 300.0, 250.0, 0.1);
        vac.SetConditions(0.0, 0.0, 0.0);
        var simVac = new FuelFlowSimulation();
        simVac.Run(vac);

        Check("vacuum burn is a single stage", simVac.Segments.Count == 1,
              "segments=" + simVac.Segments.Count);

        double expVac = 300.0 * g0 * ln10;
        double gotVac = TotalDv(simVac.Segments);
        Check("vacuum dv matches Isp*g0*ln(m0/m1)",
              Math.Abs(gotVac - expVac) / expVac < 1e-4,
              gotVac.ToString("F3") + " vs " + expVac.ToString("F3"));

        if (simVac.Segments.Count == 1)
        {
            FuelStats s = simVac.Segments[0];
            Check("start mass is the wet mass", Math.Abs(s.StartMass - 10.0) < 1e-6,
                  s.StartMass.ToString("F6"));
            Check("end mass is the dry mass", Math.Abs(s.EndMass - 1.0) < 1e-6,
                  s.EndMass.ToString("F6"));
            // thrust = massflow * Isp * g0 = 0.1 * 300 * 9.80665
            Check("stage thrust is massflow*Isp*g0",
                  Math.Abs(s.Thrust - 0.1 * 300.0 * g0) < 1e-3, s.Thrust.ToString("F4"));
            Check("Isp of the segment is the vacuum Isp",
                  Math.Abs(s.Isp - 300.0) < 1e-2, s.Isp.ToString("F4"));
        }

        // ---- SEA LEVEL: same vehicle, p=1, must use the LOWER (sea-level) Isp ----
        SimVessel sl = BuildSingleStage(dry, fuel, density, 300.0, 250.0, 0.1);
        sl.SetConditions(0.0, 1.0, 0.0);
        var simSl = new FuelFlowSimulation();
        simSl.Run(sl);

        double expSl = 250.0 * g0 * ln10;
        double gotSl = TotalDv(simSl.Segments);
        Check("sea-level dv matches the LOWER Isp (AtmosphereCurve evaluated by pressure)",
              Math.Abs(gotSl - expSl) / expSl < 1e-4,
              gotSl.ToString("F3") + " vs " + expSl.ToString("F3"));
        Check("sea-level dv is strictly less than vacuum dv", gotSl < gotVac,
              gotSl.ToString("F1") + " < " + gotVac.ToString("F1"));

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
