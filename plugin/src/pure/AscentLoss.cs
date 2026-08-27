// DragonScreen — AscentLoss  (autopilot rebuild B9: the ascent Δv-loss decomposition = the tuner objective)
// ============================================================================================
// The three velocity losses that separate a perfect ascent from a wasteful one, integrated live along the
// climb (MechJeb FlightRecorder §L / GravityTurn's TotalLoss = DragLoss + GravityLoss + VectorLoss). Their
// SUM is the objective the B9 LaunchTuner minimizes over flights, and each is a diagnostic in its own right:
//   • GravityLoss  = ∫ g·sin(γ) dt — velocity spent holding the vehicle up against gravity (worst when
//                    vertical, γ = 90°; zero once horizontal). Too steep / too slow a turn inflates it.
//   • DragLoss     = ∫ (drag/m) dt — velocity burned pushing through the air. A turn that goes fast low /
//                    lingers in the dense air inflates it.
//   • SteeringLoss = ∫ thrustAccel·(1 − cos α) dt — thrust wasted because it is not aligned with the
//                    velocity vector (α = angle of attack). Our zero-AoA gravity turn should keep this ~0;
//                    a nonzero, growing SteeringLoss is the direct readout that the nose is off prograde.
// Pure + allocation-free (a value struct accumulated per integration step); headless-tested against the
// closed-form value of each term. The glue Steps it each tick from live telemetry and records the totals.
// ============================================================================================
using System;

namespace DragonScreen
{
    public struct AscentLoss
    {
        public double GravityLoss;    // m/s
        public double DragLoss;       // m/s
        public double SteeringLoss;   // m/s
        public double Total { get { return GravityLoss + DragLoss + SteeringLoss; } }

        // Accumulate one integration step.
        //   dt              — step length (s)
        //   gRadial         — local gravitational acceleration magnitude (m/s², positive)
        //   flightPathAngleRad — velocity angle above the local horizon (π/2 = straight up, 0 = horizontal)
        //   dragAccelMps2   — |aerodynamic drag| / mass (m/s²)
        //   thrustAccelMps2 — |thrust| / mass (m/s²)
        //   aoaRad          — angle between the thrust axis and the velocity vector (0 = aligned)
        public void Step(double dt, double gRadial, double flightPathAngleRad,
                         double dragAccelMps2, double thrustAccelMps2, double aoaRad)
        {
            if (dt <= 0.0) return;
            GravityLoss  += gRadial * Math.Sin(flightPathAngleRad) * dt;
            if (dragAccelMps2 > 0.0)   DragLoss     += dragAccelMps2 * dt;
            if (thrustAccelMps2 > 0.0) SteeringLoss += thrustAccelMps2 * (1.0 - Math.Cos(aoaRad)) * dt;
        }

        public void Reset() { GravityLoss = DragLoss = SteeringLoss = 0.0; }
    }
}
