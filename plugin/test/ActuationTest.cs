/*
 * Tests for the direct-control role classifier (pure/Actuation.cs) — the capability→actuation mapping
 * the Actuator glue acts on. Pinned against the REAL Crew-2 stack part names from data/craftdump.csv, so
 * a wrong role (lighting the wrong engine set, dropping the trunk instead of S2) is caught headless, not
 * discovered by a lost flight.
 *
 * REGRESSION carried up from VehiclePartsTest (flight_0822_201219): liftoff must light ONLY the octaweb
 * all-engines mode. Here that is stated at the role level: EngineLightsFor(octaweb, "AllEngines", OctawebAll)
 * true, and the Three/Centre modes AND the pod/MVac all false for the liftoff command.
 */
using DragonScreen;
using System;

public static class ActuationTest
{
    static int checks = 0, failures = 0;
    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL  " + what + "   " + detail); }
    }

    // ---- the real part names (data/craftdump.csv, col 2) ----
    const string OCTAWEB   = "TE.19.F9.S1.Engine";
    const string MVAC      = "TE.19.F9.S2.Engine";
    const string POD       = "TE.18.DRAGONV2.POD";
    const string INTERSTAGE= "TE.19.F9.S1.Interstage";
    const string DRAGONDEC = "TE.19.C.Dragon.Decoupler";
    const string TRUNK     = "TE.18.DRAGONV2.TRUNK";
    const string ERECTOR   = "TE.Ghidorah.Erector";
    const string LEGS      = "KRE-FalconLegMk2-M";
    const string FINS      = "Grid Fin M Titanium";
    const string DROGUES   = "TE.CD2.POD.DROGUES";
    const string MAINS     = "TE.CD2.POD.MAINS";

    // real octaweb engineIDs (RO_TE_Falcon_9.cfg)
    const string ALL = "AllEngines", THREE = "ThreeLanding", CENTRE = "CenterOnly";

    public static int Run()
    {
        Console.WriteLine("DragonScreen direct-control role classifier tests");

        // ---- octaweb: engineID picks the mode ----
        Check("octaweb AllEngines  -> OctawebAll",    Actuation.EngineRoleOf(OCTAWEB, ALL)    == EngineRole.OctawebAll, "");
        Check("octaweb ThreeLanding-> OctawebThree",  Actuation.EngineRoleOf(OCTAWEB, THREE)  == EngineRole.OctawebThree, "");
        Check("octaweb CenterOnly  -> OctawebCentre", Actuation.EngineRoleOf(OCTAWEB, CENTRE) == EngineRole.OctawebCentre, "");

        // ---- MVac + pod SuperDraco ----
        Check("MVac (any id)   -> SecondStage", Actuation.EngineRoleOf(MVAC, "Engine") == EngineRole.SecondStage, "");
        Check("MVac (blank id) -> SecondStage", Actuation.EngineRoleOf(MVAC, "")       == EngineRole.SecondStage, "");
        Check("pod engine      -> PodAbort (SuperDraco)", Actuation.EngineRoleOf(POD, "Engine") == EngineRole.PodAbort, "");

        // ---- THE LIFTOFF GATE: only the all-engines mode lights (flight_0822 tank-cook regression) ----
        Check("liftoff lights octaweb AllEngines",
              Actuation.EngineLightsFor(OCTAWEB, ALL, EngineRole.OctawebAll), "");
        Check("liftoff does NOT light ThreeLanding  [the bug]",
              !Actuation.EngineLightsFor(OCTAWEB, THREE, EngineRole.OctawebAll), "");
        Check("liftoff does NOT light CenterOnly    [the bug]",
              !Actuation.EngineLightsFor(OCTAWEB, CENTRE, EngineRole.OctawebAll), "");
        Check("liftoff does NOT light the MVac",
              !Actuation.EngineLightsFor(MVAC, "", EngineRole.OctawebAll), "");
        Check("liftoff does NOT light the SuperDraco",
              !Actuation.EngineLightsFor(POD, "Engine", EngineRole.OctawebAll), "");

        // ---- SES-1: only the MVac lights ----
        Check("SES-1 lights the MVac",           Actuation.EngineLightsFor(MVAC, "", EngineRole.SecondStage), "");
        Check("SES-1 does NOT light the octaweb", !Actuation.EngineLightsFor(OCTAWEB, ALL, EngineRole.SecondStage), "");
        Check("SES-1 does NOT light the SuperDraco", !Actuation.EngineLightsFor(POD, "Engine", EngineRole.SecondStage), "");

        // ---- abort: only the pod SuperDraco lights ----
        Check("abort lights the pod SuperDraco",  Actuation.EngineLightsFor(POD, "Engine", EngineRole.PodAbort), "");
        Check("abort does NOT light the octaweb", !Actuation.EngineLightsFor(OCTAWEB, ALL, EngineRole.PodAbort), "");
        Check("abort does NOT light the MVac",    !Actuation.EngineLightsFor(MVAC, "", EngineRole.PodAbort), "");

        // ---- non-engine parts carry no engine role ----
        Check("legs   -> no engine role", Actuation.EngineRoleOf(LEGS, "")    == EngineRole.None, "");
        Check("fins   -> no engine role", Actuation.EngineRoleOf(FINS, "")    == EngineRole.None, "");
        Check("drogues-> no engine role", Actuation.EngineRoleOf(DROGUES, "") == EngineRole.None, "");

        // ---- decouplers: each real part maps to exactly one role ----
        Check("interstage -> StageSep",       Actuation.DecouplerRoleOf(INTERSTAGE) == DecouplerRole.StageSep, "");
        Check("dragon dec -> DragonSep",      Actuation.DecouplerRoleOf(DRAGONDEC)  == DecouplerRole.DragonSep, "");
        Check("trunk      -> TrunkJettison",  Actuation.DecouplerRoleOf(TRUNK)      == DecouplerRole.TrunkJettison, "");
        Check("erector    -> Erector",        Actuation.DecouplerRoleOf(ERECTOR)    == DecouplerRole.Erector, "");

        // ---- ⛔ the dangerous confusions SeparateDragon / trunk-jettison depend on being distinct ----
        Check("SECO drops S2, NOT the trunk (dragon dec != TrunkJettison)",
              Actuation.DecouplerRoleOf(DRAGONDEC) != DecouplerRole.TrunkJettison, "");
        Check("trunk jettison is NOT the Dragon decoupler (trunk != DragonSep)",
              Actuation.DecouplerRoleOf(TRUNK) != DecouplerRole.DragonSep, "");
        Check("erector is NOT a stage-sep decoupler (erector != StageSep)",
              Actuation.DecouplerRoleOf(ERECTOR) != DecouplerRole.StageSep, "");

        // ---- non-decoupler parts carry no decoupler role ----
        Check("pod     -> no decoupler role", Actuation.DecouplerRoleOf(POD)     == DecouplerRole.None, "");
        Check("octaweb -> no decoupler role", Actuation.DecouplerRoleOf(OCTAWEB) == DecouplerRole.None, "");
        Check("legs    -> no decoupler role", Actuation.DecouplerRoleOf(LEGS)    == DecouplerRole.None, "");
        Check("mains   -> no decoupler role", Actuation.DecouplerRoleOf(MAINS)   == DecouplerRole.None, "");

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }
}
