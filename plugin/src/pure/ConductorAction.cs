// DragonScreen — ConductorAction  (PURE: what the conductor has decided to do this tick)
// ============================================================================================
// Register T16, §B12.2. This is the OUTPUT TYPE of the pure conductor core, and nothing else.
//
// ---- ⛔ THIS FILE COMMANDS NOTHING, AND THAT IS THE WHOLE POINT OF IT EXISTING ----
// §B12.2 splits the conductor in two "to honor the pure/glue split": the pure core decides, the glue
// executes. A `ConductorAction` is a VALUE describing a decision — "engage the node executor",
// "hold at this gate", "re-plan the transfer". It has no reference to KSP, to Unity or to MechJeb,
// it cannot be executed, and constructing one changes nothing about any vessel.
//
// Executing one is the glue's job and arrives with T18 onward (§B12.2's corrected entry: W10's
// `FlightDriver` recovery delivers the READ-ONLY host; "the MechJeb-FACING half … is NEW work
// arriving with T18 onward"). Until then this type is decided, tested, and never acted on — which is
// exactly §14.4(a)'s position for the screens, held here for the conductor.
//
// ---- WHERE THE VOCABULARY COMES FROM (§1.4: no invented names) ----
// Every module and operation named below is either §B12.3's own text or a class that exists in the
// vendored tree; both were checked, not assumed:
//   §B12.3 names, per phase: "arm PVG" · "PVG ascent" · "Maneuver-Planner circularize/apsis ops +
//   Node Executor" · "Plane→Transfer→CourseCorrection→KillRelVel + Node Executor" · "hand off at the
//   Keep-Out Sphere to the Docking AP — the DEFAULT" · "idle/KILL-ROT" · "deorbit via
//   OperationPeriapsis … then SmartASS heat-shield-forward" · "chute triggering" · "release control".
//   In `plugin/mech/`: MechJebModuleAscentPSGAutopilot, MechJebModuleManeuverPlanner,
//   MechJebModuleNodeExecutor, MechJebModuleDockingAutopilot, MechJebModuleSmartASS, and
//   Operation{Circularize,Periapsis,Apoapsis,Plane,Transfer,CourseCorrection,KillRelVel}.
//
// ⛔ `MechJebModuleRendezvousAutopilot` EXISTS IN THE TREE AND IS DELIBERATELY NOT IN THIS ENUM.
// §B1/§B12.4: MechJeb's rendezvous *autopilot* is unreliable in RSS/RO, so the conductor composes
// planner operations itself and re-plans. Naming it here would invite a later chat to engage it.
//
// ---- NO THRESHOLDS LIVE IN THIS FILE, ON PURPOSE ----
// §B12.4's re-plan rule is `closestApproachErr > εd OR residual > tol OR drift`. The RULE is the pure
// core's; the NUMBERS are not. They are §B7–B11 "locked params" and T22's empirical tune, and a build
// chat inventing them is exactly what §1.4 forbids. So every threshold arrives on ConductorInputs
// from the caller, and this file has no magic number in it at all.
// ============================================================================================
namespace DragonScreen
{
    /// <summary>Which embedded MechJeb module a decision is about. `None` = the decision does not
    /// involve a module (a hold, an abort, releasing control).</summary>
    public enum ConductorModule : byte
    {
        None = 0,
        /// <summary>`MechJebModuleAscentPSGAutopilot` — §B8's PVG ascent.</summary>
        AscentPvg,
        /// <summary>`MechJebModuleManeuverPlanner` — builds a node from an Operation.</summary>
        ManeuverPlanner,
        /// <summary>`MechJebModuleNodeExecutor` — burns the node the planner built.</summary>
        NodeExecutor,
        /// <summary>`MechJebModuleDockingAutopilot` — §B10.3 / O6, the DEFAULT inside the KOS.</summary>
        DockingAutopilot,
        /// <summary>`MechJebModuleSmartASS` — attitude hold (KILL-ROT, heat-shield-forward).</summary>
        SmartAss
    }

    /// <summary>Which Operation the ManeuverPlanner should build, or which attitude SmartASS should
    /// hold. `None` = not an operation-bearing decision.</summary>
    public enum ConductorOp : byte
    {
        None = 0,
        Circularize,        // OperationCircularize
        Periapsis,          // OperationPeriapsis  — also the deorbit burn (§B12.3 "deorbit via OperationPeriapsis")
        Apoapsis,           // OperationApoapsis
        MatchPlane,         // OperationPlane
        Transfer,           // OperationTransfer
        CourseCorrection,   // OperationCourseCorrection
        KillRelVel,         // OperationKillRelVel
        KillRot,            // SmartASS: §B12.3's "idle/KILL-ROT" while docked
        HeatShieldForward   // SmartASS: §B10.5's entry attitude
    }

    /// <summary>What the conductor has decided to DO. One verb per tick.</summary>
    public enum ConductorVerb : byte
    {
        /// <summary>Nothing to do — no plan, or a phase this conductor does not fly.</summary>
        Idle = 0,
        /// <summary>Engage `Module` (with `Op`, where the module takes one).</summary>
        Engage,
        /// <summary>Hold: a crew gate is up and has not been cleared. NOT an error.</summary>
        Hold,
        /// <summary>The current step is finished; advance the plan.</summary>
        Advance,
        /// <summary>§B12.4: the executed node did not achieve the intent — rebuild `Op` and re-burn.</summary>
        Replan,
        /// <summary>Abort was commanded. The glue hands off to the abort path, not to MechJeb.</summary>
        Abort,
        /// <summary>§B12.3's "Splashdown → release control". Give the vessel back to the crew.</summary>
        Release
    }

    /// <summary>One decision. A value; executing it is the glue's job (T18+).</summary>
    public struct ConductorAction
    {
        public ConductorVerb Verb;
        public ConductorModule Module;
        public ConductorOp Op;
        /// <summary>WHY, in words. ⭐ Not decoration: this is what a log line and the GNC lamp print,
        /// and §0's three misdiagnoses were all "the vehicle did something and nothing said why".
        /// A decision that cannot explain itself is the defect this field exists to prevent.</summary>
        public string Reason;

        public static ConductorAction Of(ConductorVerb v, ConductorModule m, ConductorOp o, string why)
        {
            ConductorAction a;
            a.Verb = v; a.Module = m; a.Op = o; a.Reason = why;
            return a;
        }

        public static ConductorAction Idle(string why)
        { return Of(ConductorVerb.Idle, ConductorModule.None, ConductorOp.None, why); }

        /// <summary>A stable one-line form, for a log and for a test's failure message.</summary>
        public override string ToString()
        {
            return Verb.ToString()
                 + (Module != ConductorModule.None ? " " + Module : "")
                 + (Op != ConductorOp.None ? "/" + Op : "")
                 + (string.IsNullOrEmpty(Reason) ? "" : " — " + Reason);
        }
    }
}
