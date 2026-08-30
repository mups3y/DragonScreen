// DragonScreen — RcsPulse  (pure: PWPF / delta-sigma pulse modulation for an ON/OFF RCS axis)
// ============================================================================================
// Turns a CONTINUOUS command c ∈ [−1,1] into a PULSED output ∈ {−1,0,+1} whose TIME-AVERAGE tracks c:
//   • DEADBAND — below it, command nothing. This kills the hard two-sided limit cycle that thrashes the
//     Dracos and wastes MMH/NTO (the Campaign-6 failure Chris watched: 51% actuator saturation, the
//     attitude loop sign-flipping every tick with ~0 net torque while burning max propellant).
//   • MIN-ON / MIN-OFF dwell — a pulse lasts at least this long, so there is no sub-tick buzzing.
//   • FULL threshold — a near-full command passes straight through as CONTINUOUS thrust, so a sustained
//     deorbit / translation burn is never chopped (average Δv preserved; only trim commands are pulsed).
//   • Anti-windup — the impulse debt is clamped to one max dwell.
//
// This is PWPF (pulse-width/pulse-frequency) modulation, the technique real spacecraft use so on/off
// thrusters behave near-linearly instead of bang-bang. Adapted from MechJebLib's
// DeltaSigmaThrottleModulator — which MechJeb wires ONLY to the hoverslam engine throttle, NEVER to RCS;
// applying it to the Draco attitude+translation path is our improvement over the reference.
// (docs/MECHJEB_MASTER_MAP.md §3.3, docs/CAPABILITY_BUILD_BACKLOG.md Tier-2.)
// ============================================================================================
namespace DragonScreen
{
    // Per-axis modulator state (one per RCS axis). Struct = allocation-free on the hot path.
    public struct RcsPulseState
    {
        public double Accum;   // signed impulse debt (command·seconds owed)
        public int    Output;  // last emitted pulse: −1, 0, +1
        public double Timer;   // seconds held in the current Output state
        public static RcsPulseState Fresh { get { return new RcsPulseState { Accum = 0.0, Output = 0, Timer = 0.0 }; } }
    }

    public static class RcsPulse
    {
        // One control tick. cmd ∈ [−1,1] continuous demand → pulsed {−1,0,+1}. Deterministic, alloc-free.
        //   deadband : |cmd| below this commands nothing (limit-cycle kill)
        //   minOn/minOff : minimum seconds a pulse (or gap) must last
        //   full : |cmd| at/above this passes through as continuous thrust (sustained burn)
        public static int Step(ref RcsPulseState st, double cmd, double dt,
                               double deadband, double minOn, double minOff, double full)
        {
            if (dt <= 0.0) return st.Output;
            if (cmd >  1.0) cmd =  1.0; else if (cmd < -1.0) cmd = -1.0;
            double mag = cmd < 0.0 ? -cmd : cmd;
            int sign = cmd >= 0.0 ? 1 : -1;
            st.Timer += dt;

            // Sign flip while firing → coast to OFF first (never fire opposing thrusters in one dwell).
            if (st.Output != 0 && st.Output != sign)
            {
                if (st.Timer >= minOn) { st.Output = 0; st.Timer = 0.0; st.Accum = 0.0; }
                return st.Output;
            }

            // DEADBAND: command nothing (but respect min-on so a live pulse is not clipped).
            if (mag < deadband)
            {
                if (st.Output != 0 && st.Timer >= minOn) { st.Output = 0; st.Timer = 0.0; }
                st.Accum = 0.0;
                return st.Output;
            }

            // Near-full → continuous thrust (a sustained burn is not chopped).
            if (mag >= full) { st.Output = sign; st.Accum = 0.0; return sign; }

            // DELTA-SIGMA between 0 and sign(cmd): integrate the debt and toggle to track the average.
            double delivered = st.Output == sign ? 1.0 : 0.0;
            st.Accum += (mag - delivered) * dt;
            double clamp = minOn > minOff ? minOn : minOff;   // anti-windup: ≤ one max dwell of debt
            if (st.Accum >  clamp) st.Accum =  clamp;
            if (st.Accum < -clamp) st.Accum = -clamp;

            if (st.Output == sign) { if (st.Accum < 0.0 && st.Timer >= minOn)  { st.Output = 0;    st.Timer = 0.0; } }
            else                   { if (st.Accum > 0.0 && st.Timer >= minOff) { st.Output = sign; st.Timer = 0.0; } }
            return st.Output;
        }
    }
}
