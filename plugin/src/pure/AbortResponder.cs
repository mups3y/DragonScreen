// DragonScreen — AbortResponder  (autopilot rebuild L5 FDIR: the SELF-AWARE, regime-correct abort action)
// ============================================================================================
// When the crew / FDIR commands ABORT, the RIGHT response depends on WHERE IN THE MISSION — and, crucially,
// on the vehicle's actual PHYSICAL STATE, not just the phase label (docs/ABORT_PROCEDURES_RESEARCH.md, the
// real Crew Dragon pad + 7 in-flight modes). This pure layer is the BRAIN: it reads the live state and
// picks the mode. The glue (AbortControl) runs the matching real sequence.
//
//   • PAD / ASCENT, sub-orbital, LES armed  → LAUNCH ESCAPE:
//        SuperDraco escape → separate from the stack → TRUNK JETTISON → Draco reorient HEAT-SHIELD-FORWARD
//        → controlled coast/entry → drogues+mains → splash. (The tumble that killed the crew on flight
//        030705 was skipping the trunk jettison + reorient — both are mandatory here.)
//   • ASCENT, already near ORBITAL energy (very late)  → ABORT-TO-ORBIT:
//        do NOT escape-and-splash — use the remaining stage / the Dracos to reach a safe (if lower) orbit.
//   • ON-ORBIT free flight (crew hits ABORT in orbit)  → DEORBIT RETURN:
//        trunk jettison → retrograde deorbit burn → shield-forward controlled entry → chutes, targeting the
//        NEAREST SAFE (ocean) splashdown — never a mountainside. (The glue picks the water site.)
//   • PROX-OPS near the station (phasing/approach)  → KOS RETREAT: back out to a safe standoff. An orbital
//        prox-ops abort is a RETREAT, not a launch escape.
//   • DOCKED  → EMERGENCY UNDOCK: undock, then deorbit-return home (Dragon is the ISS lifeboat).
//   • ENTRY / UNDER CHUTES  → RIDE IT DOWN: past escape; hold shield-forward + ensure the chutes.
//   • no armed escape on the pad/ascent → SAFE-HOLD (nothing to escape with).
//
// ⛔ FULL CONTROL: Respond() always yields a definite command; HoldAttitude keeps L2 holding the last safe
// attitude so nothing ever floats. PURE decision; the glue fires the motors / decouplers / chutes.
// ============================================================================================
namespace DragonScreen
{
    public enum AbortMode : byte
    {
        None, LaunchEscape, AbortToOrbit, DeorbitReturn, KosRetreat, EmergencyUndock, RideItDown, SafeHold
    }

    public struct AbortInputs
    {
        public bool Triggered;          // an abort has been commanded
        public MissionPhase Phase;
        public bool LesArmed;           // launch-escape system armed (crew gate G5)

        // ---- live physical state (this is what makes the responder "self-aware") ----
        public double AltitudeM;        // ASL altitude
        public double AtmTopM;          // atmosphere top for this body (above = vacuum)
        public double SurfaceSpeedMps;  // surface-relative speed
        public double OrbitalSpeedMps;  // local circular-orbit speed (√(µ/r)) — the "near orbital" yardstick
        public double PeriapsisAltM;    // orbit periapsis ASL (> AtmTopM ⇒ a sustained orbit)
        public double ApoapsisAltM;     // orbit apoapsis ASL
        public bool NearStation;        // within prox-ops range of the rendezvous target
        public bool Docked;             // physically docked
    }

    public struct AbortCommand
    {
        public AbortMode Mode;
        public bool FireSuperDracos;    // launch escape: the SuperDraco push away from the stack
        public bool Separate;           // cut the capsule free of the failing stack (escape)
        public bool JettisonTrunk;      // shed the trunk — clears the heat shield (escape + deorbit)
        public bool Undock;             // docked emergency: release the docking hooks first
        public bool DeorbitBurn;        // retrograde deorbit burn to come home (on-orbit)
        public bool RaiseToOrbit;       // abort-to-orbit: thrust prograde to a safe orbit
        public bool Retreat;            // prox-ops: back out of the corridor
        public bool HoldShieldForward;  // hold the heat shield into the flow (controlled entry attitude)
        public bool DeployChutes;       // arm the chute sequence (escape / entry / ride-it-down)
        public bool HoldSafe;           // station-keep / safe attitude hold
        public bool HoldAttitude;       // L2 holds the last safe attitude (never float)
        public string Note;
    }

    public static class AbortResponder
    {
        // A late-ascent abort is worth doing as ABORT-TO-ORBIT (not escape-and-splash) once the vehicle is
        // this close to orbital energy — reaching even a lower orbit beats a ballistic escape.
        public const double NearOrbitalFrac = 0.95;

        public static AbortCommand Respond(AbortInputs s)
        {
            AbortCommand c = new AbortCommand();
            c.HoldAttitude = true;                     // full control: never float, whatever the mode
            if (!s.Triggered) { c.Mode = AbortMode.None; return c; }

            bool orbital = s.PeriapsisAltM > s.AtmTopM;                                  // sustained orbit
            bool nearOrbital = s.OrbitalSpeedMps > 1.0
                               && s.SurfaceSpeedMps >= NearOrbitalFrac * s.OrbitalSpeedMps;

            switch (s.Phase)
            {
                case MissionPhase.Prelaunch:
                case MissionPhase.Ascent:
                    if (!s.LesArmed)
                    {
                        c.Mode = AbortMode.SafeHold; c.HoldSafe = true;
                        c.Note = "ABORT — LES not armed; safing";
                    }
                    else if (nearOrbital || orbital)
                    {
                        c.Mode = AbortMode.AbortToOrbit; c.RaiseToOrbit = true;
                        c.Note = "ABORT-TO-ORBIT — near orbital energy; fly to a safe orbit, do not splash";
                    }
                    else
                    {
                        c.Mode = AbortMode.LaunchEscape;
                        c.FireSuperDracos = true; c.Separate = true; c.JettisonTrunk = true;
                        c.HoldShieldForward = true; c.DeployChutes = true;
                        c.Note = "LAUNCH ESCAPE — SuperDracos, separate, trunk jettison, reorient, chutes";
                    }
                    break;

                case MissionPhase.Coast:
                case MissionPhase.Phasing:
                case MissionPhase.Approach:
                    if (s.NearStation)
                    {
                        c.Mode = AbortMode.KosRetreat;
                        c.Retreat = true; c.HoldSafe = true;
                        c.Note = "PROX-OPS ABORT — retreat out of the corridor to a safe standoff";
                    }
                    else
                    {
                        c.Mode = AbortMode.DeorbitReturn;
                        c.JettisonTrunk = true; c.DeorbitBurn = true;
                        c.HoldShieldForward = true; c.DeployChutes = true;
                        c.Note = "DEORBIT RETURN — deorbit to the nearest SAFE splashdown, controlled entry, chutes";
                    }
                    break;

                case MissionPhase.Docked:
                    c.Mode = AbortMode.EmergencyUndock;
                    c.Undock = true; c.JettisonTrunk = true; c.DeorbitBurn = true;
                    c.HoldShieldForward = true; c.DeployChutes = true;
                    c.Note = "EMERGENCY UNDOCK — release, then deorbit home to the nearest safe splashdown";
                    break;

                case MissionPhase.Entry:
                case MissionPhase.Drogues:
                case MissionPhase.Mains:
                case MissionPhase.Splashdown:
                    c.Mode = AbortMode.RideItDown;
                    c.HoldShieldForward = true; c.DeployChutes = true;
                    c.Note = "RIDE IT DOWN — past the escape point; shield-forward, chutes armed";
                    break;

                default:
                    if (orbital)
                    {
                        c.Mode = AbortMode.DeorbitReturn;
                        c.JettisonTrunk = true; c.DeorbitBurn = true;
                        c.HoldShieldForward = true; c.DeployChutes = true;
                        c.Note = "DEORBIT RETURN (default, orbital)";
                    }
                    else { c.Mode = AbortMode.SafeHold; c.HoldSafe = true; c.Note = "SAFE-HOLD"; }
                    break;
            }
            return c;
        }
    }
}
