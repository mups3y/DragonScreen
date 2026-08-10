/*
 * DragonScreen - VehicleSystems
 *
 * PURE. The vehicle systems stock KSP does not model: the redundant power strings, the consumable
 * tanks, and the two cabin emergencies. Simulated from real vessel state, then wired to the console
 * buttons that were previously logging "no KSP system behind this control".
 *
 * ---- THIS IS A SIMULATOR, AND SIMULATING IS THE POINT ---- (user's call, 2026-08-06)
 * The distinction that matters is unchanged and is the only one:
 *
 *      FAKE       a constant, or a random number. Never - indistinguishable from a dead sensor.
 *      SIMULATED  derived from real state by a stated model. Moves because the vessel moved.
 *
 * Every event below has a REAL TRIGGER:
 *
 *      a string trips        because electric charge actually collapsed
 *      a fire starts         because a part is actually near its maximum temperature
 *      a leak starts         because the hull is actually being overstressed
 *      consumables fall      because four crew are actually aboard, for a real elapsed time
 *
 * Nothing here rolls a die. Two vehicles in the same state get the same answer, which is also why
 * three screens can render it without disagreeing.
 *
 * ---- WHY TWO BUSES OF THREE STRINGS ----
 * The console's own labels: `POWER 1 / STRING 1A / 1B / 1C / RESET 1` and the same for 2. That is a
 * dual-bus, triple-string architecture read straight off the panel art, not a design invented here.
 * The real capsule's strings are flight computers; ours are power paths, because power is the part
 * KSP gives us something real to hang it on.
 */
namespace DragonScreen
{
    public enum StringState : byte
    {
        /// <summary>Carrying load.</summary>
        Online = 0,
        /// <summary>Crew took it off line deliberately.</summary>
        Isolated,
        /// <summary>Dropped out on undervoltage. RESET recovers it once the bus is healthy.</summary>
        Tripped
    }

    /// <summary>Persistent systems state. Owned by the glue, advanced by Systems.Update.</summary>
    public struct SystemsState
    {
        public bool Bus1On, Bus2On;
        public StringState A1, B1, C1;
        public StringState A2, B2, C2;

        /// <summary>0..1. Grows while burning, falls under suppressant.</summary>
        public double FireIntensity;
        /// <summary>Suppressant bottle remaining, 0..1. One-shot; there is no recharge in flight.</summary>
        public double Suppressant;

        /// <summary>psia per minute currently escaping. Zero when the cabin is tight.</summary>
        public double LeakRate;
        /// <summary>DEPRESS RESPONSE latched - the cabin is isolated and the leak is closing.</summary>
        public bool Isolating;

        /// <summary>Consumables remaining, 0..1.</summary>
        public double Oxygen, Nitrogen;
        /// <summary>CO2 canister saturation, 0..1. 1 means spent.</summary>
        public double CanisterUsed;

        public bool Fire { get { return FireIntensity > 0.02; } }
        public bool Leaking { get { return LeakRate > 0.001; } }

        public static SystemsState Fresh()
        {
            SystemsState s = new SystemsState();
            s.Bus1On = true; s.Bus2On = true;
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
        /// <summary>Electric charge remaining, 0..1. The real bus health.</summary>
        public double Charge01;
        /// <summary>
        /// Hottest part on the vessel as a fraction of its own maximum temperature. KSP tracks this
        /// genuinely, and it is the most honest fire trigger available - a part at 0.9 of max really
        /// is in trouble.
        /// </summary>
        public double HottestPart01;
        /// <summary>Structural stress proxy: measured g. A real number, really felt.</summary>
        public double GForce;
    }

    public static class Systems
    {
        // ---- TRIGGERS. Chosen so nothing fires in normal flight, then stated. ----
        /// <summary>Below this the bus cannot hold its strings up and they drop out one at a time.</summary>
        public const double TripCharge = 0.15;
        /// <summary>RESET only takes once the bus has genuinely recovered.</summary>
        public const double ResetCharge = 0.30;
        /// <summary>A part this close to its own limit is the fire trigger.</summary>
        public const double FirePart01 = 0.90;
        /// <summary>Beyond this the structure is being overstressed and the cabin can spring a leak.</summary>
        public const double LeakG = 9.0;

        /// <summary>Full O2 lasts this long with a full crew, seconds. Four days.</summary>
        private const double OxygenSeconds = 4.0 * 6.0 * 3600.0;
        private const double NitrogenSeconds = 8.0 * 6.0 * 3600.0;
        private const double CanisterSeconds = 3.0 * 6.0 * 3600.0;

        public static void Update(ref SystemsState s, SystemsInputs i)
        {
            if (!i.Valid || i.Dt <= 0.0) return;
            double dt = i.Dt;

            // ---- POWER STRINGS ----
            // Undervoltage protection: strings drop in order C, B, A so the vehicle sheds redundancy
            // before it sheds its primary path. Isolated strings are the CREW's choice and are never
            // touched here - a model that quietly re-closed a switch the crew opened would be worse
            // than one that does nothing.
            if (i.Charge01 < TripCharge)
            {
                Trip(ref s.C1); Trip(ref s.C2);
                if (i.Charge01 < TripCharge * 0.6) { Trip(ref s.B1); Trip(ref s.B2); }
                if (i.Charge01 < TripCharge * 0.3) { Trip(ref s.A1); Trip(ref s.A2); }
            }

            // ---- FIRE ----
            // Needs an ignition source AND oxygen. Grows while the part stays hot; suppressant and a
            // depressurised cabin both starve it, which is exactly why DEPRESS RESPONSE is a fire
            // procedure on the real panel as well as a leak one.
            bool hot = i.HottestPart01 > FirePart01;
            double o2Available = s.Oxygen * (s.Leaking ? 0.4 : 1.0);
            if (hot && o2Available > 0.05)
                s.FireIntensity += dt * 0.05 * (i.HottestPart01 - FirePart01) / (1.0 - FirePart01);
            else
                s.FireIntensity -= dt * 0.02;

            if (s.FireIntensity < 0.0) s.FireIntensity = 0.0;
            if (s.FireIntensity > 1.0) s.FireIntensity = 1.0;

            // A fire eats oxygen and makes CO2 - that is what makes it dangerous rather than merely
            // alarming, and it is why the consumables below are worth modelling at all.
            if (s.Fire)
            {
                s.Oxygen -= dt * 0.004 * s.FireIntensity;
                s.CanisterUsed += dt * 0.002 * s.FireIntensity;
            }

            // ---- LEAK ----
            // Overstress opens it; isolating closes it over about a minute, not instantly.
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
            // Drawn down by the people actually aboard, over the time that actually passed. Scaled so
            // a full load is days rather than minutes - the point is that the numbers MOVE and mean
            // something, not that a station-ferry runs them out.
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

        /// <summary>Take a string off line, or put it back. Returns false if it is tripped.</summary>
        public static bool ToggleString(ref SystemsState s, int bus, int index)
        {
            StringState cur = Get(s, bus, index);
            if (cur == StringState.Tripped) return false;      // RESET is the only way back
            Set(ref s, bus, index, cur == StringState.Online ? StringState.Isolated
                                                             : StringState.Online);
            return true;
        }

        /// <summary>Restore every tripped string on a bus. Refuses while the bus is still sick.</summary>
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

        /// <summary>DEPRESS RESPONSE - isolate the cabin and start closing the leak.</summary>
        public static bool DepressResponse(ref SystemsState s)
        {
            if (!s.Leaking) return false;                       // nothing to isolate
            s.Isolating = true;
            return true;
        }

        /// <summary>SUPPRESS FIRE - discharge the bottle. One shot, and it refuses when empty.</summary>
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

        /// <summary>
        /// FIRE RESPONSE - the procedure, not a single valve: shed the secondary bus to remove
        /// ignition sources, then discharge. On the real vehicle this is the checklist the other two
        /// buttons are steps of, which is why it does both.
        /// </summary>
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

        /// <summary>How many strings are actually carrying load. Drives the bus readouts.</summary>
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
