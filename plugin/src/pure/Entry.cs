// DragonScreen — Entry  (autopilot rebuild L3 return: the lifting bank-angle entry guidance)
// ============================================================================================
// Crew Dragon is a BLUNT LIFTING BODY. An OFFSET (radially displaced) centre of mass gives it a natural
// trim angle of attack (~12°) and lift-to-drag L/D ≈ 0.18–0.27; it then steers by BANK-ANGLE MODULATION,
// the Apollo/Orion/Shuttle method (PHASE_6_DEORBIT_ENTRY_SPLASHDOWN_RESEARCH §3/§5b):
//   • |σ| (bank magnitude) NULLS the predicted DOWNRANGE error — the vertical lift L·cos σ sets descent
//     rate/energy and hence range. Predicted LONG → more bank (tip lift sideways) → shorter; predicted
//     SHORT → less bank (lift up) → longer.
//   • sign(σ) is REVERSED whenever |crossrange error| exceeds a velocity-dependent deadband → a series of
//     S-turns that keep crossrange bounded while |σ| flies the range. Horizontal lift L·sin σ → crossrange.
// The predicted downrange/crossrange errors come from the L1 lift-aware predictor (Trajectory.Solve with
// the MEASURED L/D + current bank) run by the glue — exactly as GridFin takes a predicted-impact error.
//
// ⛔ USE THE CoM SHIFTER CORRECTLY (user, explicit). The offset CoM is the AdjustableCoMShifter part
// (craftdump: EVENT "ToggleMode"/"Turn Descent Mode On", ACTION "Toggle", DescentModeCoM=(0,0,0.2),
// offsetPercent 0..1 sets the magnitude → the L/D). The correct use:
//   1. Engage DESCENT MODE ONCE, before Entry Interface (heat-shield-forward). It is a MODE, not a
//      steering actuator — turned ON for entry and left on. It is OFF for launch/orbit/rendezvous/
//      docking so the CoM stays centred/symmetric.
//   2. offsetPercent sets the trim AoA / L/D — pick it for the target L/D (~1.0 = full offset ≈ L/D 0.2).
//   3. NEVER toggle it to steer. Bank REVERSALS are an RCS ROLL of the whole vehicle about the velocity
//      vector; the CoM shifter only establishes the aerodynamic trim the vehicle holds by itself. The
//      capsule keeps its trim AoA aerodynamically — RCS does not fly AoA, only the roll (bank σ).
//
// ⛔ FULL CONTROL: Guide() ALWAYS returns a definite unit AimForward (heat-shield into the flow) plus the
// commanded BankRad; the glue holds shield-forward and rolls to σ. Never floating.
// ============================================================================================
using System;

namespace DragonScreen
{
    public enum EntryPhase : byte { Idle, PreEntry, Entry, Descent }

    public struct EntryInputs
    {
        public bool Valid;
        public Vec3 Velocity;          // surface-relative velocity, world frame (heat shield points into it)
        public Vec3 Up;                // local radial-up, world frame
        public double AltitudeM;

        public double EntryInterfaceAltM;  // ~120 000 — start active bank guidance below here
        public double DrogueAltM;          // ~5 486 — hand to the chute sequence at/below this

        // predicted footprint errors from the L1 lift-aware predictor (glue runs Trajectory.Solve):
        public double DownrangeErrM;   // predicted − target along the ground track (+ = LONG / overshoot)
        public double CrossrangeErrM;  // perpendicular to the ground track (+ = to the +cross side)
        public double SpeedMps;        // current speed (velocity-dependent crossrange deadband)
        public int PrevBankSign;       // last commanded sign (+1/−1) for reversal hysteresis

        public double TargetLoverD;    // desired L/D (~0.2) → offsetPercent for the CoM shifter
    }

    public struct EntryCommand
    {
        public EntryPhase Phase;
        public Vec3 AimForward;        // ALWAYS unit — heat-shield normal, held into the oncoming flow
        public double BankRad;         // commanded bank σ (signed): the RCS ROLL about the velocity vector
        public int BankSign;           // +1 / −1 (exposed so the glue/next tick tracks reversals)
        public bool EngageDescentMode; // true from PreEntry on: CoM shifter Descent Mode ON (engage ONCE)
        public double OffsetPercent;   // 0..1 CoM offset magnitude → the trim AoA / L/D
        public bool HandToChutes;      // reached the drogue altitude → Chutes.cs takes over
    }

    public static class Entry
    {
        // Bank-magnitude corrector around a reference bank. Reference σ leaves authority to lengthen (→0°,
        // more vertical lift) or shorten (→σ_max, lift tipped sideways/down) the range.
        [Tunable] public static double RefBankDeg = 60.0;      // nominal entry bank (cos60 = 0.5 vertical lift)
        [Tunable] public static double MinBankDeg = 15.0;      // never fully lift-up (keeps roll authority)
        [Tunable] public static double MaxBankDeg = 105.0;     // past 90° = lift-down (max range reduction)
        [Tunable] public static double BankGainDegPerKm = 4.0; // |σ| change per km of predicted downrange error

        // Crossrange deadband: wide at high speed, tightening as the capsule slows (velocity-dependent).
        [Tunable] public static double CrossDeadbandBaseM = 5000.0;   // floor of the deadband
        [Tunable] public static double CrossDeadbandPerMps = 3.0;     // grows with speed

        const double Deg = Math.PI / 180.0;

        public static double CrossDeadbandM(double speedMps)
        {
            double d = CrossDeadbandBaseM + CrossDeadbandPerMps * Math.Max(0.0, speedMps);
            return d;
        }

        // |σ|: predicted LONG (err>0) → bank UP toward MaxBank (shorten); predicted SHORT (err<0) → bank
        // DOWN toward MinBank (lengthen). Proportional around the reference bank, clamped.
        public static double BankMagnitudeRad(double downrangeErrM)
        {
            double sigmaDeg = RefBankDeg + BankGainDegPerKm * (downrangeErrM / 1000.0);
            if (sigmaDeg < MinBankDeg) sigmaDeg = MinBankDeg;
            if (sigmaDeg > MaxBankDeg) sigmaDeg = MaxBankDeg;
            return sigmaDeg * Deg;
        }

        // sign(σ): steer the horizontal lift to OPPOSE the crossrange error. Hold the current sign inside
        // the deadband (hysteresis, no chatter); when the error breaches the deadband on one side, command
        // the sign that pushes back the other way → the S-turn bank-reversal. The glue defines its
        // crossrange-error sign and BankRad roll sign consistently so that sign = −1 really steers toward
        // −cross (the pure law only states "oppose the error").
        public static int BankSignFor(double crossrangeErrM, double speedMps, int prevSign)
        {
            int s = (prevSign == 1 || prevSign == -1) ? prevSign : 1;
            double db = CrossDeadbandM(speedMps);
            if (crossrangeErrM > db) s = -1;        // predicted too far +cross → roll lift to −cross
            else if (crossrangeErrM < -db) s = +1;  // predicted too far −cross → roll lift to +cross
            return s;
        }

        static Vec3 ShieldForward(Vec3 v, Vec3 up)
        {
            return v.Magnitude > 1.0 ? v.Normalized : up.Normalized;   // never undefined
        }

        // offsetPercent for the CoM shifter from the desired L/D. DescentModeCoM gives ~L/D 0.2 at full
        // offset (percent 1.0); scale linearly and clamp to [0,1]. Full offset is the nominal.
        public static double OffsetPercentFor(double targetLoverD)
        {
            const double fullLoverD = 0.20;                  // L/D at offsetPercent = 1.0 (DescentModeCoM)
            if (targetLoverD <= 0.0) return 1.0;             // default: full offset
            double p = targetLoverD / fullLoverD;
            if (p < 0.0) p = 0.0; if (p > 1.0) p = 1.0;
            return p;
        }

        public static EntryCommand Guide(EntryInputs s, EntryPhase phase)
        {
            EntryCommand c = new EntryCommand();
            c.Phase = phase;
            c.AimForward = s.Valid ? ShieldForward(s.Velocity, s.Up) : new Vec3(0, 0, 1);
            c.BankSign = (s.PrevBankSign == 1 || s.PrevBankSign == -1) ? s.PrevBankSign : 1;
            c.OffsetPercent = OffsetPercentFor(s.TargetLoverD);

            if (!s.Valid) { c.Phase = EntryPhase.Idle; return c; }

            if (phase == EntryPhase.Idle) phase = EntryPhase.PreEntry;

            switch (phase)
            {
                case EntryPhase.PreEntry:
                    // above EI: heat-shield-forward, ENGAGE the CoM shifter Descent Mode (once), CoM offset
                    // set for the target L/D. No bank command yet (no measurable aero). Hold σ=0 sign.
                    c.Phase = EntryPhase.PreEntry;
                    c.EngageDescentMode = true;
                    c.BankRad = 0.0;
                    if (s.AltitudeM <= s.EntryInterfaceAltM) return Guide(s, EntryPhase.Entry);
                    break;

                case EntryPhase.Entry:
                {
                    // active bank-angle guidance. |σ| flies downrange; sign reverses on the crossrange deadband.
                    if (s.AltitudeM <= s.DrogueAltM) return Guide(s, EntryPhase.Descent);
                    c.Phase = EntryPhase.Entry;
                    c.EngageDescentMode = true;                  // stays engaged — never toggled to steer
                    int sign = BankSignFor(s.CrossrangeErrM, s.SpeedMps, s.PrevBankSign);
                    double mag = BankMagnitudeRad(s.DownrangeErrM);
                    c.BankSign = sign;
                    c.BankRad = sign * mag;                      // the RCS ROLL — CoM shifter is NOT touched here
                    break;
                }

                case EntryPhase.Descent:
                    // below the drogue altitude: chutes own it. Hold shield-forward; stop banking.
                    c.Phase = EntryPhase.Descent;
                    c.EngageDescentMode = true;
                    c.BankRad = 0.0;
                    c.HandToChutes = true;
                    break;
            }
            return c;
        }
    }
}
