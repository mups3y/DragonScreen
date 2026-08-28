// DragonScreen — DockCapture  (autopilot rebuild L3 docking: the IDSS soft-capture envelope gate)
// ============================================================================================
// Real Crew Dragon delivers the vehicle into the IDSS soft-capture CAPTURE ENVELOPE at first contact, and the
// mechanism (ring + 3 petals + latches) absorbs the residual. The envelope is the primary standard — IDSS IDD
// Rev E, Table 3.3.1.1-2 (SEQUENCE_MAP §1A): closing (axial) rate 0.05–0.10 m/s, lateral (radial) rate
// ≤0.04 m/s, lateral misalignment ≤0.10 m, pitch/yaw ≤4.0° (vector sum) + roll ≤4.0°, pitch/yaw + roll rate
// ≤0.20°/s. This pure predicate is the "are we inside the capture box?" gate; the glue (DockingControl) feeds
// the measured relative state and only declares soft capture once the geometry is inside it (KSP's own docking
// magnetism remains the authoritative capture signal — this gate just stops a fast/skewed fly-through at the
// contact tolerance from counting as a clean capture). PURE + headless-tested.
// ============================================================================================
namespace DragonScreen
{
    public struct CaptureLimits
    {
        public double MaxClosingMps;      // axial closing rate (IDSS: 0.10)
        public double MaxLateralRateMps;  // radial rate (IDSS: 0.04)
        public double MaxLateralOffsetM;  // radial misalignment (IDSS: 0.10)
        public double MaxAngleDeg;        // pitch/yaw + roll misalignment (IDSS: 4.0)
        public double MaxAngRateDegS;     // pitch/yaw + roll rate (IDSS: 0.20)
    }

    public static class DockCapture
    {
        // The IDSS IDD Rev E, Table 3.3.1.1-2 initial contact conditions.
        public static CaptureLimits Idss()
        {
            CaptureLimits l;
            l.MaxClosingMps = 0.10; l.MaxLateralRateMps = 0.04; l.MaxLateralOffsetM = 0.10;
            l.MaxAngleDeg = 4.0; l.MaxAngRateDegS = 0.20;
            return l;
        }

        // Inside the capture box? closingMps is the axial approach rate (+ = closing); the others are magnitudes.
        // A slightly-receding capsule (closingMps < 0) still passes the closing bound (it is not too FAST); the
        // range/contact test in the glue decides whether it is actually at the port.
        public static bool WithinEnvelope(double closingMps, double lateralRateMps, double lateralOffsetM,
                                          double angleDeg, double angRateDegS, CaptureLimits lim)
        {
            return closingMps <= lim.MaxClosingMps
                && lateralRateMps <= lim.MaxLateralRateMps
                && lateralOffsetM <= lim.MaxLateralOffsetM
                && angleDeg <= lim.MaxAngleDeg
                && angRateDegS <= lim.MaxAngRateDegS;
        }
    }
}
