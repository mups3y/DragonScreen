// DragonScreen — Depletion  (PURE: time-to-depletion, the MARGIN column's one currency)
//
// ⭐ THE OWNER ANSWERED S79-Q1 ON 2026-09-06 (option selected, via the overseer). The option, as
// presented:
//
//     1. Time-to-depletion, one currency for the whole column — hours or days remaining at the
//        current modelled rate.
//
// ⛔ Recorded as a SELECTION FROM PRESENTED OPTIONS, not a verbatim quote (C1.12's evidentiary
// standard — `LZ1` invented one and it took `S89` to unwind). ONE currency for the whole column: not
// split by row family, not a surplus quantity, not dashed.
//
// ---- WHY THIS IS ITS OWN FILE ----------------------------------------------------------------
// The Vehicle Overview is not the only table in this build with a quantity and a rate beside it, and
// "how long until this runs out" should have exactly one answer wherever it is asked. Putting the
// arithmetic and the FORMAT here means a second caller cannot disagree with the first about what a
// margin is — the C7.1 rule applied to a computation rather than to a document.
//
// ---- ⛔ THE DASH IS COMPUTED. THAT IS THE WHOLE POINT OF THIS FILE ----------------------------
// The defect S79 exists to remove is `R(Dash, 3360, y, 25, Dim)` — the same literal on all eight rows,
// on a live feed and a dead one alike, under a header that says MARGIN. Replacing it with a different
// literal in more places would be the same defect in a better font.
//
// So `Text` NEVER prints a dash unconditionally. It prints one when, and only when, the inputs say no
// depletion time EXISTS:
//   · the rate is zero or negative  — nothing is being consumed, so nothing runs out. ⚠ This is the
//     two deorbit propellant rows for all of a nominal mission: they read a real countdown DURING a
//     burn and dash the rest of the time. That is an ACCEPTED CONSEQUENCE of the one-currency ruling,
//     recorded on S79, not a hole — and it is why the dash has to be computed, because a row that
//     dashes for hours and then must produce a number cannot be a literal.
//   · the quantity is absent (negative) — there is no source, which is the older §14.4(e) dash.
//   · either input is not a number.
// A quantity of exactly ZERO with a positive rate is NOT a dash: it is "0.0 h", which is the truth and
// is the one reading a crew most needs to be able to tell apart from "no data".
//
// ---- AND THE UNIT SWITCHES, BECAUSE ONE CURRENCY IS NOT ONE UNIT -----------------------------
// The ruling says "hours or days". A power bus at 60 W off a small pack is hours; a propellant load
// nobody is burning is not a number at all; a lightly-loaded bus is days. Printing 172.4 h is a number
// a crew has to divide in their head, and printing 0.07 d is worse. The switch is at 48 h, and the
// unit is always printed, so the two forms can never be confused for one another.
namespace DragonScreen
{
    public static class Depletion
    {
        /// <summary>Above this many hours the readout switches to days. 48 h rather than 24 so a
        /// two-day figure reads as "45.0 h" instead of "1.9 d" — the shorter number is the one a crew
        /// can act on, and days only start being easier to read once there are several of them.</summary>
        public const double DaysAboveHours = 48.0;

        /// <summary>Seconds in an hour, named so the arithmetic below reads as what it is.</summary>
        public const double SecondsPerHour = 3600.0;

        /// <summary>
        /// Watts one EC per second represents. ⭐ THE ANSWER DOES NOT DEPEND ON THIS NUMBER, and that
        /// is worth knowing before anyone argues about it: a bus's stored energy is `ec × this` and its
        /// draw is `PowerFlow × this × busShare`, so the constant CANCELS and the depletion time is
        /// `ec / (PowerFlow × busShare)` however the scale is chosen. `BusText` still takes it as a
        /// parameter so the cancellation is visible rather than assumed, and `Depletion`'s own suite
        /// pins the invariance by asking the same question at two different scales.
        ///
        /// ⚠ IT IS A THIRD COPY OF A NUMBER THAT ALREADY EXISTS TWICE — `VesselData.EcWatts` (private)
        /// and a bare `120.0` inside `CabinEnvironment.Compute`. Unifying them is not this line's
        /// declared output (C1.11) and is logged separately; naming it here at least stops a fourth.
        /// </summary>
        public const double WattsPerEcPerSecond = 120.0;

        /// <summary>
        /// Hours until <paramref name="remaining"/> reaches zero at <paramref name="ratePerSecond"/>,
        /// or a NEGATIVE number when no depletion time exists.
        ///
        /// ⛔ NEGATIVE, not zero, for "no answer" — zero is a real and important reading (empty, or
        /// about to be), and a caller that cannot tell those apart will print "0.0 h" for a tank
        /// nobody is draining. Every caller must test the sign before formatting.
        /// </summary>
        /// <param name="remaining">The quantity left, in any unit. Negative means "no source".</param>
        /// <param name="ratePerSecond">How much of that unit is consumed per second. Zero or negative
        /// means nothing is being consumed — see the file header on why that is a dash and not a very
        /// large number.</param>
        public static double Hours(double remaining, double ratePerSecond)
        {
            if (double.IsNaN(remaining) || double.IsInfinity(remaining)) return -1.0;
            if (double.IsNaN(ratePerSecond) || double.IsInfinity(ratePerSecond)) return -1.0;
            if (remaining < 0.0) return -1.0;          // no source
            if (ratePerSecond <= 0.0) return -1.0;     // nothing is running out
            return remaining / ratePerSecond / SecondsPerHour;
        }

        /// <summary>
        /// The MARGIN cell's text: "12.4 h", "3.2 d", or the no-value dash.
        ///
        /// ⚠ THE DASH COMES OUT OF `Hours`, WHICH COMES OUT OF THE INPUTS. There is no branch here that
        /// prints a dash for a reason of its own, which is what makes a fixture-A-vs-fixture-B test able
        /// to prove this column is not a constant: change the rate and the same row prints a number.
        /// </summary>
        public static string Text(double remaining, double ratePerSecond)
        {
            double h = Hours(remaining, ratePerSecond);
            if (h < 0.0) return Dashes.None;
            if (h > DaysAboveHours) return (h / 24.0).ToString("F1") + " d";
            return h.ToString("F1") + " h";
        }

        /// <summary>
        /// Watts a stored ElectricCharge pool represents. KSP's EC is a quantity, and this build's one
        /// stated conversion is <c>VesselData.EcWatts = 120</c> — "1 EC/s is 120 W" — so 1 EC is 120
        /// joules. ⚠ NAMED HERE so the MARGIN column and the POWER tab's kW readout cannot end up using
        /// two different currencies for the same pool; the glue passes the same constant to both.
        /// </summary>
        public static double JoulesOfEc(double ec, double wattsPerEcPerSecond)
        {
            if (ec < 0.0 || wattsPerEcPerSecond <= 0.0) return -1.0;
            return ec * wattsPerEcPerSecond;
        }

        /// <summary>
        /// A power bus's MARGIN cell: stored charge over what the bus is actually drawing.
        ///
        /// ⛔ `netWatts` IS SIGNED AND THE SIGN MATTERS. `CabinEnvironment` publishes NET power, so a
        /// bus that is CHARGING reads negative-drain — and a charging bus has no depletion time, which
        /// falls out of `Hours`'s rate test rather than needing a branch here. Passing the magnitude
        /// would print a countdown for a pack that is filling up.
        /// </summary>
        public static string BusText(double ec, double wattsPerEcPerSecond, double drawWatts)
        {
            double joules = JoulesOfEc(ec, wattsPerEcPerSecond);
            if (joules < 0.0) return Dashes.None;
            return Text(joules, drawWatts);
        }
    }
}
