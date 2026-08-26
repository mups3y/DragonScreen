// DragonScreen - FaultResponse
// ---- THE RECOVERY LADDER IS REAL FDIR, LEAST-INTERVENTION FIRST ----
// ---- THE MAPPING IS NOT INVENTED. IT IS WHAT THE CODE ALREADY DOES, MADE EXPLICIT. ----
// ---- WHY A SEPARATE DOMAIN ENUM, NOT MissionPhase ----
namespace DragonScreen
{
    public enum Recovery : byte
    {
        Continue = 0,
        Retry = 1,
        Reconfigure = 2,
        Replan = 3,
        Downmode = 4,
        Abort = 5
    }

    public enum FaultKind : byte
    {
        None = 0,
        ThrustShortfall,
        ThrustReversed,
        TrajectoryDiverging,
        ConvergenceStalled,
        NoControlSolution,
        KeepOutBreach,
        ResourceCritical,
        SensorInvalid
    }

    public enum FaultDomain : byte
    {
        Ascent = 0,
        BoosterRecovery,
        OrbitCoast,
        Rendezvous,
        Docked,
        Deorbit,
        Entry
    }

    public static class FaultResponse
    {
        public static Recovery Decide(FaultKind kind, HealthVerdict verdict, FaultDomain domain)
        {
            if (kind == FaultKind.None || verdict == HealthVerdict.Nominal) return Recovery.Continue;
            bool failed = verdict == HealthVerdict.Failed;

            switch (kind)
            {
                case FaultKind.ThrustShortfall:
                    if (domain == FaultDomain.BoosterRecovery)
                        return failed ? Recovery.Replan : Recovery.Reconfigure;
                    return failed ? Recovery.Replan : Recovery.Retry;

                case FaultKind.ThrustReversed:
                    return failed ? Recovery.Replan : Recovery.Reconfigure;

                case FaultKind.TrajectoryDiverging:
                    if (domain == FaultDomain.Ascent) return failed ? Recovery.Abort : Recovery.Replan;
                    if (domain == FaultDomain.Entry)  return failed ? Recovery.Downmode : Recovery.Replan;
                    return Recovery.Replan;

                case FaultKind.ConvergenceStalled:
                    if (domain == FaultDomain.Rendezvous)
                        return failed ? Recovery.Downmode : Recovery.Replan;
                    return failed ? Recovery.Replan : Recovery.Retry;

                case FaultKind.NoControlSolution:
                    if (domain == FaultDomain.BoosterRecovery || domain == FaultDomain.Entry)
                        return failed ? Recovery.Downmode : Recovery.Reconfigure;
                    return Recovery.Replan;

                case FaultKind.KeepOutBreach:
                    return failed ? Recovery.Abort : Recovery.Downmode;

                case FaultKind.ResourceCritical:
                    return failed ? Recovery.Abort : Recovery.Downmode;

                case FaultKind.SensorInvalid:
                    return failed ? Recovery.Abort : Recovery.Retry;
            }
            return Recovery.Continue;
        }

        public static Recovery Worst(Recovery a, Recovery b)
        {
            return (byte)a >= (byte)b ? a : b;
        }

        public static string Name(Recovery r)
        {
            switch (r)
            {
                case Recovery.Retry:       return "RETRY";
                case Recovery.Reconfigure: return "RECONFIGURE";
                case Recovery.Replan:      return "REPLAN";
                case Recovery.Downmode:    return "DOWNMODE";
                case Recovery.Abort:       return "ABORT";
                default:                   return "CONTINUE";
            }
        }
    }
}
