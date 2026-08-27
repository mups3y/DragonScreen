// DragonScreen — Phasing  (autopilot rebuild L3 rendezvous: the FAR-FIELD co-elliptic chase + the safety floor)
// ============================================================================================
// The terminal rendezvous is Clohessy-Wiltshire (pure/Cw.cs) — but CW is a LINEARISATION about the target and
// is only valid within tens of km. Right after insertion the chaser is THOUSANDS of km away in along-track
// phase (e.g. 13,000 km, flight 214827), where CW is meaningless: its state-transition inverse demanded a
// ~28 km/s "burn" and the glue fired the Dracos retrograde continuously → the capsule SELF-DEORBITED (pe
// +178 → −143 km) until the prop ran dry. THIS module is the far-field law that replaces CW out there.
//
// THE FAR-FIELD TECHNIQUE (the real co-elliptic rendezvous, robust + monotonic + crew-safe):
//   • Raise the chaser to a CO-ELLIPTIC orbit a set height BELOW the station (~10 km) by burning PROGRADE
//     only (prograde raises the orbit — it can NEVER lower periapsis, so it cannot deorbit).
//   • Once co-elliptic, COAST: the lower/faster chaser catches the station in phase; the range closes.
//   • When the range is inside CW's valid regime, hand to the CW terminal legs (glue does this).
// Every decision here is prograde-or-coast — deorbiting is not in the vocabulary — and a HARD PERIAPSIS
// FLOOR (PeSafe) is the independent backstop the executor consults before EVERY burn. Pure + headless-tested.
// ============================================================================================
namespace DragonScreen
{
    public static class Phasing
    {
        // The co-elliptic parking altitude = a set height BELOW the target's (mean) altitude.
        public static double CoEllipticTargetAltM(double targetMeanAltM, double belowM)
        {
            return targetMeanAltM - belowM;
        }

        // Should the chaser still be RAISING (burn prograde), or has it reached the co-elliptic altitude?
        // Raise while EITHER apsis is below the co-elliptic target (minus a tolerance) — continuous prograde
        // thrust walks both apses up until the orbit sits at the co-elliptic altitude, then we coast.
        // ⛔ Returns raise ONLY when below target: it never asks to lower, so the far field cannot deorbit.
        public static bool ShouldRaise(double apAltM, double peAltM, double coEllipticTargetAltM, double tolM)
        {
            return apAltM < coEllipticTargetAltM - tolM || peAltM < coEllipticTargetAltM - tolM;
        }

        // ⛔ THE HARD SAFETY FLOOR (the crew-safety guarantee, independent of any guidance solve). No
        // rendezvous / docking / departure translation burn may fire while periapsis is at or below the floor —
        // so even a garbage guidance command can never walk the orbit down into re-entry. (The intentional
        // ABORT deorbit does NOT go through this path; it commits deliberately.)
        public static bool PeSafe(double peAltM, double floorM)
        {
            return peAltM > floorM;
        }

        // Is the chaser far enough that CW is invalid and the far-field law must fly it instead?
        public static bool FarField(double rangeM, double cwHandoffRangeM)
        {
            return rangeM > cwHandoffRangeM;
        }
    }
}
