// DragonScreen - CrewProcedure (PURE)
// ---- WHY PURE ----
// ---- A GATE ----
namespace DragonScreen
{
    public enum GateId : byte
    {
        None = 0,
        // ---- COUNTDOWN ----
        Ingress,
        SuitLeakCheck,
        HatchClose,
        GoForPropLoad,
        ArmLaunchEscape,
        InternalPower,
        GoForLaunch,
        // ---- ON ORBIT / APPROACH ----
        ApproachInitiation,
        HoldWp0,
        HoldWp1,
        HoldWp2,
        DockingComplete,
        // ---- RETURN ----
        GoForUndock,
        GoForDeorbit,
        EntryMonitor
    }

    public enum ItemKind : byte
    {
        CrewAck,
        Auto
    }

    public enum AutoCheck : byte
    {
        None = 0,
        CabinNominal,
        ConsumablesOk,
        OnInternalPower,
        StableOrbit,
        Docked,
        AtWp0,
        AtWp1,
        AtWp2
    }

    public struct ChecklistItem
    {
        public string Label;
        public ItemKind Kind;
        public AutoCheck Auto;
    }

    public struct Gate
    {
        public GateId Id;
        public string Title;
        public ChecklistItem[] Items;
    }

    public enum GatePhase : byte
    {
        Holding,
        GoReady,
        Go,
        NoGo,
        Abort
    }

    public struct ProcState
    {
        public int GateIndex;
        public GatePhase Phase;
        public bool[] Satisfied;
    }

    public static class CrewProcedureCore
    {
        public static ProcState Begin(Gate[] gates)
        {
            ProcState st = new ProcState();
            if (gates == null || gates.Length == 0) { st.GateIndex = -1; st.Phase = GatePhase.Go; st.Satisfied = new bool[0]; return st; }
            st.GateIndex = 0;
            st.Phase = GatePhase.Holding;
            st.Satisfied = Fresh(gates[0]);
            return st;
        }

        public static bool Complete(ProcState st) { return st.GateIndex < 0; }

        public static Gate Current(Gate[] gates, ProcState st)
        {
            if (gates == null || st.GateIndex < 0 || st.GateIndex >= gates.Length)
            { Gate g = new Gate(); g.Id = GateId.None; g.Title = ""; g.Items = new ChecklistItem[0]; return g; }
            return gates[st.GateIndex];
        }

        public static bool AllSatisfied(Gate g, ProcState st)
        {
            if (g.Items == null) return true;
            if (st.Satisfied == null || st.Satisfied.Length != g.Items.Length) return false;
            for (int i = 0; i < st.Satisfied.Length; i++)
                if (!st.Satisfied[i]) return false;
            return true;
        }

        public static void SetItem(Gate g, ref ProcState st, int i, bool value)
        {
            if (st.Satisfied == null || i < 0 || i >= st.Satisfied.Length) return;
            if (st.Phase == GatePhase.Go || st.Phase == GatePhase.Abort) return;
            st.Satisfied[i] = value;
            Refresh(g, ref st);
        }

        public static void Refresh(Gate g, ref ProcState st)
        {
            if (st.Phase == GatePhase.Go || st.Phase == GatePhase.Abort) return;
            st.Phase = AllSatisfied(g, st) ? GatePhase.GoReady : GatePhase.Holding;
        }

        public static bool Go(Gate g, ref ProcState st)
        {
            if (AllSatisfied(g, st)) { st.Phase = GatePhase.Go; return true; }
            return false;
        }

        public static void NoGo(ref ProcState st)
        {
            if (st.Phase == GatePhase.Go || st.Phase == GatePhase.Abort) return;
            st.Phase = GatePhase.NoGo;
        }

        public static void Abort(ref ProcState st) { st.Phase = GatePhase.Abort; }

        public static void Advance(Gate[] gates, ref ProcState st)
        {
            if (st.Phase != GatePhase.Go || gates == null) return;
            int next = st.GateIndex + 1;
            if (next >= gates.Length) { st.GateIndex = -1; st.Phase = GatePhase.Go; st.Satisfied = new bool[0]; return; }
            st.GateIndex = next;
            st.Phase = GatePhase.Holding;
            st.Satisfied = Fresh(gates[next]);
        }

        private static bool[] Fresh(Gate g)
        {
            int n = (g.Items == null) ? 0 : g.Items.Length;
            return new bool[n];
        }
    }
}
