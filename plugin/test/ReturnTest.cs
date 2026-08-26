// Tests for L3 return: the departure FSM (pure/Departure.cs), deorbit targeting + burn
// (pure/DeorbitGuidance.cs), the lifting bank-angle entry with the CoM-shifter contract (pure/Entry.cs),
// and the drogue/main/splashdown sequence (pure/Chutes.cs). Full-control contract: every attitude
// command is a definite unit vector.
using System;
using DragonScreen;

public static class ReturnTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    { checks++; if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); } }
    static void Near(string what, double got, double want, double tol)
    { Check(what, Math.Abs(got - want) <= tol, got.ToString("F4") + " vs " + want.ToString("F4")); }

    // ~200 km circular ISS orbit
    const double Mu = 3.986004418e14;
    const double Re = 6.371e6;
    static double OrbitR = Re + 200000.0;
    static double N { get { return Math.Sqrt(Mu / (OrbitR * OrbitR * OrbitR)); } }

    static DepartureInputs Dep(double rx, double ry, double rz)
    {
        DepartureInputs s = new DepartureInputs();
        s.Valid = true; s.N = N; s.AttitudeReady = true; s.AllNominal = true; s.KosRadiusM = 200;
        s.CoEllipticBelowM = 10000; s.CoEllipticBehindM = 20000;
        s.OrbitRadiusM = OrbitR; s.PhasingLowerM = 10000; s.Mu = Mu;
        s.Rel = new LvlhState { Rx = rx, Ry = ry, Rz = rz };
        return s;
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen L3 return tests");

        // ============================ DEPARTURE (Phase 5) ============================
        // full-control: a unit aim in every phase incl. invalid
        foreach (DepPhase ph in new[] { DepPhase.Idle, DepPhase.Undock, DepPhase.Depart0, DepPhase.Depart1,
                                        DepPhase.Depart2, DepPhase.Depart3, DepPhase.Phasing, DepPhase.Departed })
        {
            DepartureCommand cc = Departure.Guide(Dep(0, 6, 0), ph);
            Check("Departure AimLvlh is UNIT in " + ph, Math.Abs(cc.AimLvlh.Magnitude - 1.0) < 1e-6,
                  cc.AimLvlh.Magnitude.ToString("F6"));
        }
        Check("invalid departure still gets a unit aim",
              Math.Abs(Departure.Guide(new DepartureInputs { Valid = false }, DepPhase.Undock).AimLvlh.Magnitude - 1.0) < 1e-6, "");

        // undock: at the port, the sep burn fires and pushes AWAY from the station (+relative position)
        DepartureCommand un = Departure.Guide(Dep(0, 6, 0), DepPhase.Undock);
        Check("undock fires a separation burn", un.Burn && un.BurnDvMps > 0, un.BurnDvMps.ToString("F3"));
        Check("sep push is AWAY from the station (+along)", un.AimLvlh.Y > 0.99, un.AimLvlh.Y.ToString("F3"));
        Check("still undocking inside the sep standoff", un.Phase == DepPhase.Undock, un.Phase.ToString());
        // past the standoff → first departure hop
        Check("clear of the standoff → Depart0",
              Departure.Guide(Dep(0, 60, 0), DepPhase.Undock).Phase == DepPhase.Depart0, "");

        // a departure hop commands a CW burn (attitude points along it)
        DepartureCommand d1 = Departure.Guide(Dep(-1500, -3000, 0), DepPhase.Depart1);
        Check("Depart1 commands a CW burn", d1.Burn && d1.BurnDvMps > 0, d1.BurnDvMps.ToString("F4"));
        Check("Depart1 aim points along the burn",
              Math.Abs(Vec3.Dot(d1.AimLvlh, d1.BurnLvlh.Normalized) - 1.0) < 1e-6, "");

        // arriving at the co-elliptic point (Depart3 aim ≈ −10 km below, −20 km behind) → Phasing
        DepartureCommand arr = Departure.Guide(Dep(-10000, -20000, 0), DepPhase.Depart3);
        Check("reaching the co-elliptic point → Phasing (or beyond)",
              arr.Phase == DepPhase.Phasing || arr.Phase == DepPhase.Departed, arr.Phase.ToString());

        // phasing: a retrograde apsis-lower burn (−along), positive Δv
        DepartureCommand ph2 = Departure.Guide(Dep(-10000, -20000, 0), DepPhase.Phasing);
        Check("phasing burn is retrograde (−along-track)", ph2.BurnLvlh.Y < 0, ph2.BurnLvlh.Y.ToString("F4"));
        Check("phasing burn has positive Δv", ph2.BurnDvMps > 0, ph2.BurnDvMps.ToString("F4"));
        // no lowering requested → departed (coast to deorbit)
        DepartureInputs noLow = Dep(-10000, -20000, 0); noLow.PhasingLowerM = 0;
        Check("no phasing needed → Departed", Departure.Guide(noLow, DepPhase.Phasing).Departed, "");

        // ============================ DEORBIT ============================
        double eiR = Re + 120000.0;
        double dv = DeorbitGuidance.DeorbitDvMps(OrbitR, eiR, Mu);
        Check("deorbit Δv to lower Pe from 200→120 km is positive & sane", dv > 5 && dv < 100, dv.ToString("F2"));
        Check("deorbit Δv is 0 when the target is not below the orbit",
              DeorbitGuidance.DeorbitDvMps(OrbitR, OrbitR + 1000, Mu) == 0.0, "");

        DeorbitInputs di = new DeorbitInputs();
        di.Valid = true; di.Velocity = new Vec3(7784, 0, 0); di.Up = new Vec3(0, 0, 1);
        di.PeriapsisAltM = 200000; di.EntryInterfaceAltM = 50000; di.TrunkAttached = true;
        di.AttitudeReady = true; di.AllNominal = true; di.SettleS = 5; di.SettleElapsedS = 0;

        // full-control unit aim in every deorbit phase
        foreach (DeorbitPhase ph in new[] { DeorbitPhase.Idle, DeorbitPhase.TrunkJettison, DeorbitPhase.Settle,
                                            DeorbitPhase.Burn, DeorbitPhase.Complete, DeorbitPhase.OrientEntry })
            Check("Deorbit AimForward is UNIT in " + ph,
                  Math.Abs(DeorbitGuidance.Guide(di, ph).AimForward.Magnitude - 1.0) < 1e-6, "");

        // trunk goes FIRST, before the burn
        DeorbitCommand tj = DeorbitGuidance.Guide(di, DeorbitPhase.TrunkJettison);
        Check("trunk is jettisoned while attached", tj.JettisonTrunk, "");
        DeorbitInputs det = di; det.TrunkAttached = false;
        Check("trunk gone → Settle", DeorbitGuidance.Guide(det, DeorbitPhase.TrunkJettison).Phase == DeorbitPhase.Settle, "");
        DeorbitInputs settled = det; settled.SettleElapsedS = 6;
        Check("settle dwell elapsed → Burn", DeorbitGuidance.Guide(settled, DeorbitPhase.Settle).Phase == DeorbitPhase.Burn, "");

        // burning: retrograde (−velocity), throttle open
        DeorbitCommand bn = DeorbitGuidance.Guide(settled, DeorbitPhase.Burn);
        Check("deorbit burn points RETROGRADE", bn.AimForward.X < -0.99, bn.AimForward.X.ToString("F3"));
        Check("deorbit burn throttles up", bn.Throttle > 0.99 && bn.Burning, "");
        // periapsis reached → complete, orient heat-shield-forward (into the flow, +velocity)
        DeorbitInputs done = settled; done.PeriapsisAltM = 45000;
        DeorbitCommand dc = DeorbitGuidance.Guide(done, DeorbitPhase.Burn);
        Check("Pe on the corridor → burn complete", dc.Complete && dc.Throttle == 0.0, "");
        Check("post-burn aim is heat-shield-forward (+velocity)",
              DeorbitGuidance.Guide(done, DeorbitPhase.OrientEntry).AimForward.X > 0.99, "");

        // ============================ ENTRY (lifting bank-angle) ============================
        // bank magnitude: 0 error → reference bank; long → more bank (shorter); short → less bank (longer)
        Near("nominal bank at zero error = reference", Entry.BankMagnitudeRad(0) * 180 / Math.PI, Entry.RefBankDeg, 1e-6);
        Check("predicted LONG → more bank than nominal", Entry.BankMagnitudeRad(10000) > Entry.BankMagnitudeRad(0), "");
        Check("predicted SHORT → less bank than nominal", Entry.BankMagnitudeRad(-10000) < Entry.BankMagnitudeRad(0), "");
        Check("bank magnitude clamps at the max", Entry.BankMagnitudeRad(1e6) * 180 / Math.PI <= Entry.MaxBankDeg + 1e-6, "");
        Check("bank magnitude clamps at the min", Entry.BankMagnitudeRad(-1e6) * 180 / Math.PI >= Entry.MinBankDeg - 1e-6, "");

        // bank REVERSAL opposes the crossrange error, with deadband hysteresis (the S-turn)
        double v = 7000, db = Entry.CrossDeadbandM(v);
        Check("far +cross error → roll to −cross (sign −1)", Entry.BankSignFor(db + 1000, v, +1) == -1, "");
        Check("far −cross error → roll to +cross (sign +1)", Entry.BankSignFor(-db - 1000, v, -1) == +1, "");
        Check("inside the deadband → hold the sign (hysteresis)", Entry.BankSignFor(100, v, -1) == -1, "");
        Check("crossrange deadband widens with speed", Entry.CrossDeadbandM(7000) > Entry.CrossDeadbandM(1000), "");

        // CoM-shifter offsetPercent from the target L/D
        Near("full L/D 0.2 → offsetPercent 1.0", Entry.OffsetPercentFor(0.20), 1.0, 1e-9);
        Near("half L/D 0.1 → offsetPercent 0.5", Entry.OffsetPercentFor(0.10), 0.5, 1e-9);
        Near("L/D 0 → default full offset", Entry.OffsetPercentFor(0.0), 1.0, 1e-9);
        Check("over-full L/D clamps to 1.0", Entry.OffsetPercentFor(0.5) == 1.0, "");

        EntryInputs ei = new EntryInputs();
        ei.Valid = true; ei.Velocity = new Vec3(7500, 0, 0); ei.Up = new Vec3(0, 0, 1);
        ei.AltitudeM = 130000; ei.EntryInterfaceAltM = 120000; ei.DrogueAltM = 5486;
        ei.SpeedMps = 7500; ei.PrevBankSign = 1; ei.TargetLoverD = 0.2;

        // full-control unit aim in every entry phase
        foreach (EntryPhase ph in new[] { EntryPhase.Idle, EntryPhase.PreEntry, EntryPhase.Entry, EntryPhase.Descent })
            Check("Entry AimForward is UNIT in " + ph,
                  Math.Abs(Entry.Guide(ei, ph).AimForward.Magnitude - 1.0) < 1e-6, "");

        // ⛔ CoM SHIFTER CONTRACT: engaged ONCE before EI, held on; never used to steer
        EntryCommand pre = Entry.Guide(ei, EntryPhase.PreEntry);
        Check("PreEntry engages the CoM shifter Descent Mode", pre.EngageDescentMode, "");
        Check("PreEntry commands NO bank yet (no aero)", pre.BankRad == 0.0, "");
        Near("PreEntry sets the CoM offset for the target L/D", pre.OffsetPercent, 1.0, 1e-9);
        Check("above EI stays PreEntry", pre.Phase == EntryPhase.PreEntry, "");

        EntryInputs below = ei; below.AltitudeM = 110000; below.DownrangeErrM = 8000; below.CrossrangeErrM = 40000;
        EntryCommand en = Entry.Guide(below, EntryPhase.PreEntry);
        Check("below EI → active Entry", en.Phase == EntryPhase.Entry, en.Phase.ToString());
        Check("CoM shifter STAYS engaged in entry (never toggled to steer)", en.EngageDescentMode, "");
        Check("entry banks to fly the trajectory (nonzero σ)", Math.Abs(en.BankRad) > 1e-6, en.BankRad.ToString("F4"));
        Check("entry steering is a ROLL, heat shield stays into the flow (+velocity)", en.AimForward.X > 0.99, "");

        // reaching the drogue altitude hands off to the chutes
        EntryInputs lowE = ei; lowE.AltitudeM = 5000;
        Check("at drogue altitude → hand to chutes", Entry.Guide(lowE, EntryPhase.Entry).HandToChutes, "");

        // ============================ CHUTES ============================
        Check("drogues deploy below 5.5 km while descending", Chutes.DrogueDeploy(5000, 150, 5486), "");
        Check("no drogue above 5.5 km", !Chutes.DrogueDeploy(6000, 150, 5486), "");
        Check("no drogue if not descending", !Chutes.DrogueDeploy(5000, 0, 5486), "");
        Check("mains deploy below 1.8 km while descending", Chutes.MainDeploy(1500, 50, 1830), "");
        Check("no mains above 1.8 km", !Chutes.MainDeploy(3000, 50, 1830), "");

        ChuteInputs ci = new ChuteInputs();
        ci.Valid = true; ci.DrogueAltM = 5486; ci.MainAltM = 1830; ci.SeaAltM = 0;

        ci.AltitudeM = 5000; ci.DescentRateMps = 150;
        ChuteCommand cd = Chutes.Sequence(ci, ChutePhase.Drogue);
        Check("drogue phase deploys drogues", cd.DeployDrogues && cd.Phase == ChutePhase.Drogue, "");

        ci.AltitudeM = 1500; ci.DescentRateMps = 50;
        ChuteCommand cm = Chutes.Sequence(ci, ChutePhase.Drogue);
        Check("through the main gate → Main phase", cm.Phase == ChutePhase.Main, "");
        Check("main phase deploys mains", Chutes.Sequence(ci, ChutePhase.Main).DeployMains, "");

        ci.AltitudeM = 0; ci.DescentRateMps = 6;
        ChuteCommand cs = Chutes.Sequence(ci, ChutePhase.Main);
        Check("at the sea surface → splashed", cs.Splashed && cs.Phase == ChutePhase.Splashed, "");
        Near("touchdown speed reported (nominal 5–8 m/s)", cs.TouchdownSpeedMps, 6.0, 1e-9);

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
