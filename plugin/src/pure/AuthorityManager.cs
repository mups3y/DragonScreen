// DragonScreen — AuthorityManager  (Phase 2: the single control-authority arbitration layer)
// ============================================================================================
// ONE place decides WHO controls WHAT, per vehicle. Every control source — Autopilot, Manual (crew),
// Recovery (deorbit-rescue), Abort — claims the axis-groups it wants; the manager grants each group to
// the highest-priority active claimant. This is the layer the flight loop and the crew screens both
// read, so they can never disagree about who is flying.
//
//   PRIORITY (low → high):  None < Autopilot < Manual < Recovery < Abort
//   ABORT pre-empts everything (a triggered abort owns the vehicle until it is cleared).
//   CAMERA IS NOT A SOURCE — there is deliberately no camera input here. Which vessel the player is
//   looking at must never decide who controls a spacecraft (the dual-vessel rule). The mission owns
//   authority; the camera is presentation only.
//
// PURE + TESTABLE: no KSP/Unity types. The glue (FlightDriver / BoosterControl) publishes claims into
// this model each tick; the screens read Mode/Granted from it. Phase 2 is a BEHAVIOUR-PRESERVING
// extraction — it reproduces today's autopilot/abort authority as a readable model and adds the
// Manual/Recovery claim API that the Docking page will command through in Phase 7. It does NOT re-gate
// the proven OnFlyByWire actuation path; that seam is added with Manual, under its own regression.
// ============================================================================================
using System;

namespace DragonScreen
{
    // The two independently-controlled vehicles (dual-vessel: Dragon + its Falcon booster on recovery).
    public enum AuthVehicle : byte { Dragon = 0, Booster = 1 }

    // The controllable axis-groups (match FlightDriver's per-axis ownership latches).
    public enum AuthAxis : byte { Throttle = 0, Translation = 1, Attitude = 2, Roll = 3 }

    // Control sources, ORDERED BY PRIORITY (numeric value = priority; higher wins). Do not reorder.
    public enum AuthSource : byte { None = 0, Autopilot = 1, Manual = 2, Recovery = 3, Abort = 4 }

    // Top-level per-vehicle mode the crew screens show (derived from the granted sources).
    public enum ControlMode : byte { Idle = 0, Auto = 1, Manual = 2, Recovery = 3, Abort = 4 }

    // One vehicle's authority: for each axis-group, the set of sources currently claiming it (a bitmask
    // over AuthSource). Granted() returns the highest-priority claimant. Value-free of KSP → unit-tested.
    public sealed class VehicleAuthority
    {
        readonly int[] claims = new int[4];   // indexed by AuthAxis; bit s set = AuthSource s is claiming

        static int Bit(AuthSource s) { return 1 << (int)s; }

        public void Claim(AuthAxis axis, AuthSource src)   { claims[(int)axis] |=  Bit(src); }
        public void Release(AuthAxis axis, AuthSource src) { claims[(int)axis] &= ~Bit(src); }

        // Claim / release a source across every axis (Abort and Recovery take the whole vehicle).
        public void ClaimAll(AuthSource src)   { for (int a = 0; a < 4; a++) claims[a] |=  Bit(src); }
        public void ReleaseSource(AuthSource src) { for (int a = 0; a < 4; a++) claims[a] &= ~Bit(src); }
        public void Clear() { for (int a = 0; a < 4; a++) claims[a] = 0; }

        // Whether a source is claiming an axis at all (regardless of who currently holds it).
        public bool Claims(AuthAxis axis, AuthSource src) { return (claims[(int)axis] & Bit(src)) != 0; }

        // The source that currently HOLDS an axis = the highest-priority active claim (None if unclaimed).
        public AuthSource Granted(AuthAxis axis)
        {
            int m = claims[(int)axis];
            for (int s = (int)AuthSource.Abort; s >= (int)AuthSource.Autopilot; s--)
                if ((m & (1 << s)) != 0) return (AuthSource)s;
            return AuthSource.None;
        }

        // Does this source actually hold the axis right now? (This is the gate a controller checks before
        // it writes an actuator command — Phase 7 uses it so the autopilot yields to Manual/Abort.)
        public bool Holds(AuthAxis axis, AuthSource src) { return Granted(axis) == src; }

        // Top-level mode = the highest-priority source across ALL axes, mapped to the crew-facing mode.
        public ControlMode Mode
        {
            get
            {
                AuthSource top = AuthSource.None;
                for (int a = 0; a < 4; a++)
                {
                    AuthSource g = Granted((AuthAxis)a);
                    if ((int)g > (int)top) top = g;
                }
                switch (top)
                {
                    case AuthSource.Abort:     return ControlMode.Abort;
                    case AuthSource.Recovery:  return ControlMode.Recovery;
                    case AuthSource.Manual:    return ControlMode.Manual;
                    case AuthSource.Autopilot: return ControlMode.Auto;
                    default:                   return ControlMode.Idle;
                }
            }
        }

        // ---- convenience setters the glue uses each tick (clear-then-set; pure, so unit-tested) ----
        // Autopilot owns exactly the axes whose per-axis latch is engaged this frame.
        public void SetAutopilot(bool throttle, bool translation, bool attitude, bool roll)
        {
            Clear();
            if (throttle)    Claim(AuthAxis.Throttle,    AuthSource.Autopilot);
            if (translation) Claim(AuthAxis.Translation, AuthSource.Autopilot);
            if (attitude)    Claim(AuthAxis.Attitude,    AuthSource.Autopilot);
            if (roll)        Claim(AuthAxis.Roll,        AuthSource.Autopilot);
        }

        // The whole vehicle taken by one source (Abort / Recovery latch, or a full Idle clear).
        public void SetWhole(AuthSource src)
        {
            Clear();
            if (src != AuthSource.None) ClaimAll(src);
        }
    }

    // The process-wide authority model: one VehicleAuthority per vehicle. The glue publishes into it every
    // physics tick; the screens read from it. Reset on every fresh flight scene (like the controllers).
    public static class AuthorityManager
    {
        public static readonly VehicleAuthority Dragon  = new VehicleAuthority();
        public static readonly VehicleAuthority Booster = new VehicleAuthority();

        public static VehicleAuthority Of(AuthVehicle v)
        {
            return v == AuthVehicle.Booster ? Booster : Dragon;
        }

        public static void Reset() { Dragon.Clear(); Booster.Clear(); }

        // Map a source → the crew mode word (for a single-source readout / the abort banner).
        public static ControlMode ModeOf(AuthSource src)
        {
            switch (src)
            {
                case AuthSource.Abort:     return ControlMode.Abort;
                case AuthSource.Recovery:  return ControlMode.Recovery;
                case AuthSource.Manual:    return ControlMode.Manual;
                case AuthSource.Autopilot: return ControlMode.Auto;
                default:                   return ControlMode.Idle;
            }
        }

        public static string Name(ControlMode m)
        {
            switch (m)
            {
                case ControlMode.Auto:     return "AUTO";
                case ControlMode.Manual:   return "MANUAL";
                case ControlMode.Recovery: return "RECOVERY";
                case ControlMode.Abort:    return "ABORT";
                default:                   return "IDLE";
            }
        }
    }
}
