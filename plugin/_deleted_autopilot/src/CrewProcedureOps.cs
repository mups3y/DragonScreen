// DragonScreen - CrewProcedureOps
// ---- WHY THIS REPLACED THE FLOW-THROUGH CONDUCTOR ----
// ---- ONE BUTTON, TWO LEGS ----
// ---- THE HELM STAYS WITH THE TESTED CONTROLLERS ----
using UnityEngine;

namespace DragonScreen
{
    public static class CrewProcedureOps
    {
        private const string Tag = "[DragonScreen] ";

        public static bool Engaged { get; private set; }
        public static bool ReturnArmed { get; private set; }

        // ---- the live gate machine ----
        private static Gate[] gates = new Gate[0];
        private static ProcState proc;
        private static bool onReturnLeg;

        public static WpPhase ReleasedHold = WpPhase.Idle;

        public static Gate CurrentGate() { return CrewProcedureCore.Current(gates, proc); }
        public static ProcState Proc { get { return proc; } }

        public static string PhaseName
        {
            get
            {
                if (!Engaged) return "";
                Gate g = CurrentGate();
                if (g.Id == GateId.None) return onReturnLeg ? "RETURN" : "OUTBOUND";
                return CrewActionNeeded() ? g.Title : FlyingName(g);
            }
        }

        public static bool CrewActionNeeded()
        {
            if (!Engaged) return false;
            Gate g = CurrentGate();
            if (g.Id == GateId.None) return false;
            if (proc.Phase == GatePhase.GoReady || proc.Phase == GatePhase.NoGo) return true;
            if (proc.Phase != GatePhase.Holding) return false;
            return AllAutoSatisfied(g) && AnyCrewUnchecked(g);
        }

        private static bool AllAutoSatisfied(Gate g)
        {
            if (g.Items == null || proc.Satisfied == null) return true;
            for (int i = 0; i < g.Items.Length; i++)
                if (g.Items[i].Kind == ItemKind.Auto && !proc.Satisfied[i]) return false;
            return true;
        }

        private static bool AnyCrewUnchecked(Gate g)
        {
            if (g.Items == null || proc.Satisfied == null) return false;
            for (int i = 0; i < g.Items.Length; i++)
                if (g.Items[i].Kind == ItemKind.CrewAck && !proc.Satisfied[i]) return true;
            return false;
        }

        // ---- crew inputs, from the UI ----
        public static void ToggleItem(int i)
        {
            if (!Engaged) return;
            Gate g = CurrentGate();
            if (g.Items == null || i < 0 || i >= g.Items.Length) return;
            if (g.Items[i].Kind != ItemKind.CrewAck) return;
            bool now = (proc.Satisfied != null && i < proc.Satisfied.Length) && proc.Satisfied[i];
            CrewProcedureCore.SetItem(g, ref proc, i, !now);

            if (g.Id == GateId.ArmLaunchEscape && !now) AbortResponder.Arm();
        }

        public static void PressGo()
        {
            if (!Engaged) return;
            Gate g = CurrentGate();
            if (!CrewProcedureCore.Go(g, ref proc)) return;
            ExecuteGateAction(g.Id);
            CrewProcedureCore.Advance(gates, ref proc);
            Debug.Log(Tag + "GATE CLEARED - " + g.Title);
        }

        public static void PressNoGo()
        {
            if (!Engaged) return;
            CrewProcedureCore.NoGo(ref proc);
            Debug.Log(Tag + "NO-GO - holding at " + CurrentGate().Title);
        }

        public static void PressAbort()
        {
            if (!Engaged) { AbortResponder.Trigger("crew abort"); return; }
            CrewProcedureCore.Abort(ref proc);
            AbortResponder.Trigger("crew abort at " + CurrentGate().Title);
        }

        // ---- engage / disengage ----
        public static void Toggle()
        {
            if (Engaged) { Disengage("crew"); FlightCommands.CancelAllSequences(); return; }
            Engage();
        }

        public static void Engage()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return;

            onReturnLeg = ReturnArmed && InStableOrbit(v) && !DockedSide.Docked(v);
            gates = onReturnLeg ? CrewGates.Return(Missions.Active) : CrewGates.Outbound(Missions.Active);
            proc = CrewProcedureCore.Begin(gates);
            SkipClearedGates(v);
            Engaged = true;
            ReleasedHold = WpPhase.Idle;
            Debug.Log(Tag + "AUTO SEQUENCE engaged - " + (onReturnLeg ? "RETURN" : "OUTBOUND")
                      + " leg, " + CurrentGate().Title);
        }

        public static void Disengage(string why)
        {
            if (!Engaged) return;
            Engaged = false;
            ReleasedHold = WpPhase.Idle;
            Debug.Log(Tag + "AUTO SEQUENCE disengaged - " + why);
        }

        // ---- the per-frame authority ----
        private static int lastTickFrame = -1;

        public static void Tick()
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v != null && v.situation == Vessel.Situations.PRELAUNCH && ReturnArmed && !Engaged)
                ReturnArmed = false;

            if (!Engaged) return;
            if (Time.frameCount == lastTickFrame) return;
            lastTickFrame = Time.frameCount;
            if (v == null) return;

            if (proc.Phase == GatePhase.Abort) return;

            Gate g = CurrentGate();

            if (CrewProcedureCore.Complete(proc))
            {
                OnLegComplete(v);
                return;
            }

            // ---- refresh the Auto items from real vessel / life-support state ----
            if (g.Items != null && proc.Satisfied != null)
                for (int i = 0; i < g.Items.Length; i++)
                    if (g.Items[i].Kind == ItemKind.Auto)
                        CrewProcedureCore.SetItem(g, ref proc, i, EvalAuto(g.Items[i].Auto, v));
        }

        // ---- gate actions: engage the controller the cleared gate opens ----
        private static void ExecuteGateAction(GateId id)
        {
            switch (id)
            {
                case GateId.GoForLaunch:
                    if (!AutoPilot.Engaged) AutoPilot.Engage();
                    break;

                case GateId.ApproachInitiation:
                    if (AutoPilot.Engaged) AutoPilot.Disengage("crew-ops: insertion complete");
                    if (!StationApproach.Engaged && !DockingOps.Engaged) MissionOps.Rendezvous();
                    break;

                case GateId.HoldWp0: ReleasedHold = WpPhase.Hold0; break;
                case GateId.HoldWp1: ReleasedHold = WpPhase.Hold1; break;
                case GateId.HoldWp2: ReleasedHold = WpPhase.Hold2; break;

                case GateId.GoForUndock:
                    MissionOps.UndockAndLand();
                    break;

                case GateId.GoForDeorbit:
                    if (!DeorbitOps.Engaged && !EntryOps.Engaged) DeorbitOps.Engage();
                    break;
            }
        }

        // ---- auto-item evaluation: the system confirming a real fact ----
        private static bool EvalAuto(AutoCheck a, Vessel v)
        {
            switch (a)
            {
                case AutoCheck.StableOrbit:    return InStableOrbit(v);
                case AutoCheck.Docked:         return DockedSide.Docked(v);
                case AutoCheck.OnInternalPower: return Powered(v);
                case AutoCheck.CabinNominal:   return CabinNominal(v);
                case AutoCheck.AtWp0:          return WaypointApproachOps.Phase == WpPhase.Hold0;
                case AutoCheck.AtWp1:          return WaypointApproachOps.Phase == WpPhase.Hold1;
                case AutoCheck.AtWp2:          return WaypointApproachOps.Phase == WpPhase.Hold2;
                case AutoCheck.ConsumablesOk:
                    return LifeSupport.SufficientFor(LifeSupportBridge.Margins(v),
                                                     Missions.Active.MissionDurationDays,
                                                     Missions.Active.ConsumablesReserveDays);
                default:                        return true;
            }
        }

        private static void OnLegComplete(Vessel v)
        {
            if (!onReturnLeg)
            {
                bool reached = Missions.Active.HasRendezvous ? DockedSide.Docked(v) : InStableOrbit(v);
                if (reached)
                {
                    ReturnArmed = true;
                    Disengage(Missions.Active.HasRendezvous
                              ? "outbound complete - transfer crew, then UNDOCK"
                              : "in orbit - free flight; press AUTO SEQUENCE to return");
                }
            }
            else
            {
                Disengage("return underway - deorbit -> entry -> splashdown");
            }
        }

        // ---- a mid-mission engage: mark gates whose goal is already met as cleared ----
        private static void SkipClearedGates(Vessel v)
        {
            int guard = 0;
            while (!CrewProcedureCore.Complete(proc) && guard++ < 64)
            {
                Gate g = CrewProcedureCore.Current(gates, proc);
                if (!AlreadyPast(g.Id, v)) break;
                for (int i = 0; i < proc.Satisfied.Length; i++)
                    CrewProcedureCore.SetItem(g, ref proc, i, true);
                CrewProcedureCore.Go(g, ref proc);
                CrewProcedureCore.Advance(gates, ref proc);
            }
        }

        private static bool AlreadyPast(GateId id, Vessel v)
        {
            bool airborne = v.situation != Vessel.Situations.PRELAUNCH;
            switch (id)
            {
                case GateId.Ingress:
                case GateId.SuitLeakCheck:
                case GateId.HatchClose:
                case GateId.GoForPropLoad:
                case GateId.ArmLaunchEscape:
                case GateId.InternalPower:
                case GateId.GoForLaunch:
                    return airborne;
                case GateId.ApproachInitiation:
                    return DockedSide.Docked(v);
                default:
                    return false;
            }
        }

        // ---- helpers ----
        private static bool InStableOrbit(Vessel v)
        {
            return v.orbit != null && v.mainBody != null && v.orbit.PeA >= v.mainBody.atmosphereDepth;
        }

        private static bool Powered(Vessel v)
        {
            double amt, max;
            v.GetConnectedResourceTotals(PartResourceLibrary.ElectricityHashcode, out amt, out max);
            return max > 0.0 && amt / max > 0.01;
        }

        private static bool CabinNominal(Vessel v)
        {
            if (!Powered(v)) return false;
            LsState ls = LifeSupportBridge.Read(v);
            if (!ls.Present) return true;
            return ls.Oxygen01 > 0.05 && ls.Co201 < 0.95;
        }

        private static string FlyingName(Gate g)
        {
            switch (g.Id)
            {
                case GateId.ApproachInitiation: return "ASCENT";
                case GateId.HoldWp0:            return "RENDEZVOUS";
                case GateId.HoldWp1:            return "L-APPROACH";
                case GateId.HoldWp2:            return "L-APPROACH";
                case GateId.DockingComplete:    return "DOCKING";
                case GateId.GoForDeorbit:       return "DEPARTURE";
                default:                        return g.Title;
            }
        }
    }
}
