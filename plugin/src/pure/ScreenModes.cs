// DragonScreen — ScreenModes  (PURE: the display-facing status enums the screens colour + label)
// ============================================================================================
// The screens show WHO owns the vehicle (control-authority mode) and WHETHER a fault is tripped
// (the FDIR spine), as colour + text. Those two status feeds used to be produced by the autopilot
// (AuthorityManager for the mode, Fdir for the fault). The autopilot was DELETED 2026-09-01 (owner:
// keep only the Dragon screens / UI), so these enums live here now, purely for the display: with no
// autopilot flying, the screens simply read the nominal values (Idle / None / Continue) forever.
// The definitions are kept byte-identical to the originals so the existing switch/colour logic in
// Pages / StatusIndicator / Alarms is unchanged.
// ============================================================================================
namespace DragonScreen
{
    // Control-authority mode — who owns the vehicle (StatusIndicator colours this; Pages carries it).
    public enum ControlMode : byte { Idle = 0, Auto = 1, Manual = 2, Recovery = 3, Abort = 4 }

    // The FDIR fault spine the crew alert channel shows. With no autopilot, always None.
    public enum FaultKind : byte
    {
        None, KeepOutBreach, ThrustShortfall, NoControlSolution, ResourceCritical,
        TrajectoryDivergence, ConvergenceStall
    }

    // The recovery rung applied to a tripped fault (Alarms folds this into crew severity).
    public enum Recovery : byte { Continue, Retry, Reconfigure, Replan, Downmode, Abort, SafeMode }

    // Display-string for the control-authority mode (DockingPage / DockingPageCentral header, rule C6).
    // Kept from the deleted AuthorityManager so the docking pages' GNC lamp label is unchanged.
    public static class AuthorityManager
    {
        public static string Name(ControlMode m)
        {
            switch (m)
            {
                case ControlMode.Auto:     return "AUTO";
                case ControlMode.Manual:   return "MANUAL";
                case ControlMode.Recovery: return "RECOVERY";
                case ControlMode.Abort:    return "ABORT";
                default:                   return "IDLE";
            }
        }
    }
}
