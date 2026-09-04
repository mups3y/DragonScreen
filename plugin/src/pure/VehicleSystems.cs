// DragonScreen - VehicleSystems
// ---- THIS IS A SIMULATOR, AND SIMULATING IS THE POINT ---- (user's call, 2026-08-06)
// ---- WHY TWO BUSES OF THREE STRINGS ----
namespace DragonScreen
{
    public enum StringState : byte
    {
        Online = 0,
        Isolated,
        Tripped
    }

    public struct SystemsState
    {
        public bool Bus1On, Bus2On;
        public StringState A1, B1, C1;
        public StringState A2, B2, C2;

        public double FireIntensity;
        public double Suppressant;

        public double LeakRate;
        public bool Isolating;

        public double Oxygen, Nitrogen;
        public double CanisterUsed;

        public bool Fire { get { return FireIntensity > 0.02; } }
        public bool Leaking { get { return LeakRate > 0.001; } }

        // ---- THE ECLSS TENANTS THE POWER SYSTEM ALREADY DECIDES (S56 / audit H33) ----
        // The P&ID drew CABIN FAN and PUMP A/B as the literal word "RUNNING" — a machine that ran
        // whatever the crew did to its bus. These are NOT a new simulation and needed none: a pump and a
        // fan are electrical loads, and this model already knows, per bus, whether the crew has powered
        // it and whether any string behind it is online. So the answer is DERIVED, not stored — there is
        // no second state to drift out of step with the buses, and no way to be "running" on a dead bus.
        //
        // The assignment (stated, per §1.4 — the real vehicle's loop-to-bus wiring is not something this
        // build has a source for, so it is OURS and it is coherent rather than transcribed): the two
        // coolant loops are the redundant pair, so loop A rides bus 1 and loop B rides bus 2 — losing one
        // bus costs one loop, which is the whole point of having two. The cabin fan is life-critical and
        // therefore CROSS-STRAPPED: it runs on either bus, and only both buses down stop it.
        public bool PumpAOn { get { return Systems.OnlineCount(this, 1) > 0; } }
        public bool PumpBOn { get { return Systems.OnlineCount(this, 2) > 0; } }
        public bool FanOn   { get { return PumpAOn || PumpBOn; } }

        public static SystemsState Fresh()
        {
            SystemsState s = new SystemsState();
            // ---- BUSES START OFF: THE CREW POWERS EACH ROW BEFORE ITS STRINGS RESPOND ----
            s.Bus1On = false; s.Bus2On = false;
            s.Suppressant = 1.0;
            s.Oxygen = 1.0; s.Nitrogen = 1.0;
            return s;
        }
    }

    public struct SystemsInputs
    {
        public bool Valid;
        public double Dt;
        public int Crew;
        public double Charge01;
        public double HottestPart01;
        public double GForce;
    }

    public static class Systems
    {
        // ---- TRIGGERS. Chosen so nothing fires in normal flight, then stated. ----
        public const double TripCharge = 0.15;
        public const double ResetCharge = 0.30;
        public const double FirePart01 = 0.90;
        public const double LeakG = 9.0;

        private const double OxygenSeconds = 4.0 * 6.0 * 3600.0;
        private const double NitrogenSeconds = 8.0 * 6.0 * 3600.0;
        private const double CanisterSeconds = 3.0 * 6.0 * 3600.0;

        public static void Update(ref SystemsState s, SystemsInputs i)
        {
            if (!i.Valid || i.Dt <= 0.0) return;
            double dt = i.Dt;

            // ---- POWER STRINGS ----
            if (i.Charge01 < TripCharge)
            {
                Trip(ref s.C1); Trip(ref s.C2);
                if (i.Charge01 < TripCharge * 0.6) { Trip(ref s.B1); Trip(ref s.B2); }
                if (i.Charge01 < TripCharge * 0.3) { Trip(ref s.A1); Trip(ref s.A2); }
            }

            // ---- FIRE ----
            bool hot = i.HottestPart01 > FirePart01;
            double o2Available = s.Oxygen * (s.Leaking ? 0.4 : 1.0);
            if (hot && o2Available > 0.05)
                s.FireIntensity += dt * 0.05 * (i.HottestPart01 - FirePart01) / (1.0 - FirePart01);
            else
                s.FireIntensity -= dt * 0.02;

            if (s.FireIntensity < 0.0) s.FireIntensity = 0.0;
            if (s.FireIntensity > 1.0) s.FireIntensity = 1.0;

            if (s.Fire)
            {
                s.Oxygen -= dt * 0.004 * s.FireIntensity;
                s.CanisterUsed += dt * 0.002 * s.FireIntensity;
            }

            // ---- LEAK ----
            if (i.GForce > LeakG && !s.Isolating)
            {
                double over = (i.GForce - LeakG) / LeakG;
                if (over > s.LeakRate) s.LeakRate = over;
            }
            if (s.Isolating)
            {
                s.LeakRate -= dt / 60.0;
                if (s.LeakRate <= 0.0) { s.LeakRate = 0.0; s.Isolating = false; }
            }
            if (s.Leaking) s.Nitrogen -= dt * s.LeakRate * 0.0008;

            // ---- CONSUMABLES ----
            double crewFrac = (i.Crew > 0) ? i.Crew / 4.0 : 0.0;
            s.Oxygen -= dt * crewFrac / OxygenSeconds;
            s.Nitrogen -= dt * crewFrac / NitrogenSeconds;
            s.CanisterUsed += dt * crewFrac / CanisterSeconds;

            Clamp(ref s.Oxygen); Clamp(ref s.Nitrogen); Clamp(ref s.CanisterUsed);
            Clamp(ref s.Suppressant);
        }

        private static void Trip(ref StringState st)
        {
            if (st == StringState.Online) st = StringState.Tripped;
        }

        private static void Clamp(ref double v)
        {
            if (v < 0.0) v = 0.0; else if (v > 1.0) v = 1.0;
        }

        // ------------------------------------------------------------------ crew actions

        public static bool ToggleString(ref SystemsState s, int bus, int index)
        {
            StringState cur = Get(s, bus, index);
            if (cur == StringState.Tripped) return false;
            Set(ref s, bus, index, cur == StringState.Online ? StringState.Isolated
                                                             : StringState.Online);
            return true;
        }

        public static bool ResetBus(ref SystemsState s, int bus, double charge01)
        {
            if (charge01 < ResetCharge) return false;
            bool any = false;
            for (int i = 0; i < 3; i++)
                if (Get(s, bus, i) == StringState.Tripped)
                {
                    Set(ref s, bus, i, StringState.Online);
                    any = true;
                }
            return any;
        }

        public static void ToggleBus(ref SystemsState s, int bus)
        {
            if (bus == 1) s.Bus1On = !s.Bus1On; else s.Bus2On = !s.Bus2On;
        }

        public static bool DepressResponse(ref SystemsState s)
        {
            if (!s.Leaking) return false;
            s.Isolating = true;
            return true;
        }

        public static bool SuppressFire(ref SystemsState s)
        {
            if (s.Suppressant <= 0.01) return false;
            if (!s.Fire) return false;
            s.Suppressant -= 0.5;
            s.FireIntensity -= 0.7;
            if (s.FireIntensity < 0.0) s.FireIntensity = 0.0;
            if (s.Suppressant < 0.0) s.Suppressant = 0.0;
            return true;
        }

        public static bool FireResponse(ref SystemsState s)
        {
            if (!s.Fire) return false;
            s.Bus2On = false;
            SuppressFire(ref s);
            return true;
        }

        // ------------------------------------------------------------------ accessors

        public static StringState Get(SystemsState s, int bus, int index)
        {
            if (bus == 1) return index == 0 ? s.A1 : index == 1 ? s.B1 : s.C1;
            return index == 0 ? s.A2 : index == 1 ? s.B2 : s.C2;
        }

        public static void Set(ref SystemsState s, int bus, int index, StringState v)
        {
            if (bus == 1) { if (index == 0) s.A1 = v; else if (index == 1) s.B1 = v; else s.C1 = v; }
            else { if (index == 0) s.A2 = v; else if (index == 1) s.B2 = v; else s.C2 = v; }
        }

        public static int OnlineCount(SystemsState s, int bus)
        {
            int n = 0;
            for (int i = 0; i < 3; i++) if (Get(s, bus, i) == StringState.Online) n++;
            if (bus == 1 && !s.Bus1On) return 0;
            if (bus == 2 && !s.Bus2On) return 0;
            return n;
        }

        public static string StateWord(StringState v)
        {
            return v == StringState.Online ? "ON" : v == StringState.Isolated ? "ISOL" : "TRIP";
        }
    }
}
