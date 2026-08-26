// DragonScreen - DockApproach
// ---- ⛔ WHY THIS FILE EXISTS: THE TESTED LAYER WAS NOT THE BROKEN LAYER ----
// ---- ⛔ SIMPLIFIED 2026-08-17: THE LVLH "L" IS GONE. gate -> standoff -> axial. ----
// ---- WHERE THE RULES COME FROM ----
// ---- AND THE STAGE MACHINE IS MONOTONE ----
namespace DragonScreen
{
    // ---- THE STAGE. A plain enum with no KSP in it, so the layer that DECIDES it is testable. ----
    public enum DockStage : byte
    {
        Idle = 0,
        NoPort,
        AwaitingTarget,
        ToGate,
        Rounding,
        Corridor,
        Axial,
        Docked
    }

    public enum DockWaypoint : byte
    {
        None = 0,
        Skirt,
        Gate,
        Standoff,
        Port
    }

    public struct DockApproachInputs
    {
        public bool Valid;
        public double AxialM;
        public double LateralM;
        public double ToStandoffM;
        public double ToGateM;
        public bool PathClear;
        public double AcquireM;
        public double SafeM;
    }

    public struct DockApproachResult
    {
        public DockWaypoint Waypoint;
        public DockStage Stage;
        public bool Captured;
        public string Note;
    }

    public static class DockApproach
    {
        [Tunable] public static double CorridorRadiusM = 1.0;

        [Tunable] public static double CorridorAbortM = 2.0;

        public static double StandoffM { get { return DockGeometry.StandoffM; } }

        public static int Rank(DockStage s)
        {
            switch (s)
            {
                case DockStage.ToGate:
                case DockStage.Rounding: return 1;
                case DockStage.Corridor: return 2;
                case DockStage.Axial:    return 3;
                case DockStage.Docked:   return 4;
                default: return 0;
            }
        }

        public static DockApproachResult Select(DockApproachInputs s, DockStage reached)
        {
            DockApproachResult r = new DockApproachResult();
            r.Stage = reached;

            if (!s.Valid)
            {
                r.Waypoint = DockWaypoint.None;
                r.Note = "no geometry";
                return r;
            }

            // ---- CAPTURED ----
            if (s.AxialM <= s.AcquireM && s.AxialM > -s.AcquireM
                && s.LateralM <= s.AcquireM)
            {
                r.Captured = true;
                r.Stage = DockStage.Docked;
                r.Waypoint = DockWaypoint.Port;
                r.Note = "CAPTURE";
                return r;
            }

            // ---- WRONG SIDE falls through to the gate logic below, NOT a special branch. ----

            // ================= THE NOMINAL PROFILE: gate -> standoff -> axial =================

            // ---- AXIAL: lined up, run straight in. ----
            if (Rank(reached) >= Rank(DockStage.Axial)
                || (s.AxialM > 0.0 && s.AxialM <= StandoffM && s.LateralM <= CorridorRadiusM))
            {
                r.Stage = DockStage.Axial;
                if (s.LateralM > CorridorAbortM)
                {
                    r.Waypoint = DockWaypoint.Standoff;
                    r.Note = "OFF THE AXIS - re-lining up, lateral " + s.LateralM.ToString("F1") + " m";
                    return r;
                }
                r.Waypoint = DockWaypoint.Port;
                r.Note = "AXIAL - " + s.AxialM.ToString("F1") + " m";
                return r;
            }

            // ---- CORRIDOR: at the gate, run in along the port axis to the standoff. ----
            if (Rank(reached) >= Rank(DockStage.Corridor) || DockGeometry.AtGate(s.ToGateM))
            {
                r.Waypoint = DockWaypoint.Standoff;
                r.Stage = DockStage.Corridor;
                r.Note = "CORRIDOR - " + s.ToStandoffM.ToString("F0") + " m to the standoff";
                return r;
            }

            // ---- TO THE GATE: straight there if the path is clear, else slide round the hull. ----
            if (s.PathClear)
            {
                r.Waypoint = DockWaypoint.Gate;
                r.Stage = DockStage.ToGate;
                r.Note = "TO GATE - " + s.ToGateM.ToString("F0") + " m";
                return r;
            }
            r.Waypoint = DockWaypoint.Skirt;
            r.Stage = DockStage.Rounding;
            r.Note = "ROUNDING HULL";
            return r;
        }
    }
}
