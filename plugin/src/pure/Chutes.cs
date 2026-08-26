// DragonScreen — Chutes  (autopilot rebuild L3 return: drogue / main / splashdown sequence)
// ============================================================================================
// The parachute sequence is STATE-BASED, not clock-based — a crew-safety backstop that fires on the
// MEASURED altitude + descent rate regardless of what the rest of the sequence thinks (PHASE_6_DEORBIT_
// ENTRY_SPLASHDOWN_RESEARCH §4/§5b):
//   • 2 DROGUES at ~18 000 ft (5 486 m) / ~156 m/s — stabilise + slow through the transonic region.
//   • 4 MAINS at ~6 000 ft (1 830 m) / ~53 m/s — slow to ~5–5.5 m/s (safe on 3 of 4).
//   • SPLASHDOWN at ~5–8 m/s in the ocean.
// The trigger is altitude + a positive descent rate; mains only after drogues (sequence guard) but each
// is independently gated on measured state so a missed upstream step still deploys them.
// ============================================================================================
using System;

namespace DragonScreen
{
    public enum ChutePhase : byte { Idle, Drogue, Main, Splashed }

    public struct ChuteInputs
    {
        public bool Valid;
        public double AltitudeM;        // above the sea surface
        public double DescentRateMps;   // positive = descending
        public double DrogueAltM;       // 5 486
        public double MainAltM;         // 1 830
        public double SeaAltM;          // ~0 — splashdown reference
    }

    public struct ChuteCommand
    {
        public ChutePhase Phase;
        public bool DeployDrogues;      // command the 2 drogues this tick
        public bool DeployMains;        // command the 4 mains this tick
        public bool Splashed;
        public double TouchdownSpeedMps; // reported at splashdown
    }

    public static class Chutes
    {
        public const double DrogueSpeedMps = 156.0;   // reference deploy speed (informational)
        public const double MainSpeedMps = 53.0;
        public const double TouchdownMaxMps = 8.0;    // nominal splashdown 5–8 m/s
        const double MinDescentMps = 0.5;             // must actually be coming down to deploy

        public static bool DrogueDeploy(double altM, double descentRateMps, double drogueAltM)
        {
            return altM <= drogueAltM && descentRateMps > MinDescentMps;
        }
        public static bool MainDeploy(double altM, double descentRateMps, double mainAltM)
        {
            return altM <= mainAltM && descentRateMps > MinDescentMps;
        }

        public static ChuteCommand Sequence(ChuteInputs s, ChutePhase phase)
        {
            ChuteCommand c = new ChuteCommand();
            c.Phase = phase;
            if (!s.Valid) { c.Phase = ChutePhase.Idle; return c; }

            if (phase == ChutePhase.Idle) phase = ChutePhase.Drogue;

            switch (phase)
            {
                case ChutePhase.Drogue:
                    c.Phase = ChutePhase.Drogue;
                    if (DrogueDeploy(s.AltitudeM, s.DescentRateMps, s.DrogueAltM)) c.DeployDrogues = true;
                    // advance to mains only once we are through the main-deploy gate (drogues already out).
                    if (MainDeploy(s.AltitudeM, s.DescentRateMps, s.MainAltM)) c.Phase = ChutePhase.Main;
                    break;

                case ChutePhase.Main:
                    c.Phase = ChutePhase.Main;
                    if (MainDeploy(s.AltitudeM, s.DescentRateMps, s.MainAltM)) c.DeployMains = true;
                    if (s.AltitudeM <= s.SeaAltM)
                    { c.Phase = ChutePhase.Splashed; c.Splashed = true; c.TouchdownSpeedMps = s.DescentRateMps; }
                    break;

                case ChutePhase.Splashed:
                    c.Phase = ChutePhase.Splashed; c.Splashed = true;
                    c.TouchdownSpeedMps = s.DescentRateMps;
                    break;
            }
            return c;
        }
    }
}
