/*
 * DragonScreen - Refuel
 *
 * GLUE. Moves monopropellant from the station into the capsule while berthed. Ported from
 * `falcon9.ks:9058 F9RefuelTick`.
 *
 * ---- WHY THIS IS THE LINCHPIN OF THE WHOLE MISSION ----
 * The Dragon launches light. The 2026-08-11 flight reported "have 73.1, need 151.1 -> margin -78.0"
 * and I called that a craft-file problem; it is not. **The capsule is meant to leave the berth with a
 * full tank.** The station has thousands of units and is right there, so the ascent, the rendezvous
 * and the docking may spend whatever they need - the return budget is not what survives them, it is
 * what the station hands over at the end. Without this the return budget can never close and every
 * de-orbit is flown short.
 *
 * ---- ⛔ WHICH TANK IS OURS IS THE ENTIRE PROBLEM. SEE `DockedSide`. ----
 * Docking merges both craft into one `Vessel`, so a naive walk feeds and measures the pair together.
 * F9I paid for this twice over: the undock reports "the fuel tanks are not full" while the Dragon's
 * tank is brimming - it is reading the station's farm - and with two Dragons berthed the transfer
 * "fed the wrong one. A capsule was released with zero monopropellant and stranded as a result."
 * Every source and destination below is chosen through `DockedSide`, never from `v.parts`.
 *
 * ---- ⚠ AND IT YIELDS TO ANY OTHER AUTHORITY ----
 * F9I moved this job to the station's own CPU (`SXSTATION.ks` `SxRefuelTick`) and kept its capsule-side
 * version only as a backup, with the warning: *"there are TWO authorities moving the same resource...
 * one owner is better than two."* So this only ever moves propellant that is STILL MISSING after
 * anyone else has had their turn: if the station is feeding us, the deficit closes on its own, this
 * finds nothing to do, and it stays out of the way.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class Refuel
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>Units per second. Fast enough to fill in a held undock, slow enough to watch.</summary>
        public const double RateUnitsPerS = 8.0;
        /// <summary>Deficits below this are rounding, not a refuel.</summary>
        public const double DeadbandUnits = 0.1;
        /// <summary>Our tank counts as full at this fraction. See `Full`.</summary>
        public const double FullFraction = 0.999;

        /// <summary>Units moved into the capsule since the last <see cref="Begin"/>.</summary>
        public static double TakenOnUnits { get; private set; }

        public static void Begin() { TakenOnUnits = 0.0; }

        /// <summary>
        /// The capsule's REAL return propellant - MMH + NTO (the Draco/SuperDraco bipropellant the
        /// de-orbit and landing burn on). NOT MonoPropellant: the 300 u MonoProp tank is vestigial and
        /// topping it off the station's farm would just waste a transfer. Real Crew-2 does not refuel at
        /// the ISS at all (it flies the whole mission on its launch load), so in practice nothing has
        /// MMH/NTO to give and this never moves anything - it is kept only so a station that DID carry
        /// MMH/NTO could top the return tank. See dragon-return-propellant-mmh-nto.
        /// </summary>
        private static readonly string[] TopUp = { "MMH", "NTO" };

        /// <summary>
        /// Is the CAPSULE'S return tank full? "Full tank for de-orbit and land" (user 2026-08-24) is the
        /// REAL propellant - MMH+NTO - not the vestigial MonoProp, so this asks DockedSide.ReturnFraction
        /// (the limiting one of the bipropellant, MonoProp fallback for an old capsule).
        ///
        /// A capacity of zero means there is no tank to fill, which counts as full - otherwise a
        /// vehicle with no return propellant at all would hold the undock open forever.
        /// </summary>
        public static bool Full(Vessel v)
        {
            return DockedSide.ReturnFraction(v) >= FullFraction;
        }

        /// <summary>Fill fraction of OUR return tank, 0..1, for the readout - the limiting propellant.</summary>
        public static double Fraction(Vessel v)
        {
            return DockedSide.ReturnFraction(v);
        }

        /// <summary>
        /// Move one tick's worth. Returns the units actually transferred, which is zero when there is
        /// nothing to do - because we are full, because we are not docked, or because the station has
        /// nothing left to give. All three are legitimate ends to a refuel and the caller says which.
        ///
        /// ⚠ THE FULLEST SOURCE AND THE EMPTIEST DESTINATION, one pair per tick. That is F9I's shape,
        /// and it is deliberate: a source that runs dry mid-transfer simply loses to a different tank
        /// next tick instead of stalling the refuel.
        /// </summary>
        public static double Tick(Vessel v, double dt)
        {
            if (v == null || dt <= 0.0) return 0.0;
            if (!DockedSide.Docked(v)) return 0.0;

            // Ours by construction - the walk stops at the docking joint.
            List<Part> ours = DockedSide.Ours(v);
            HashSet<Part> mine = new HashSet<Part>(ours);

            // Move EACH return propellant (MMH+NTO, and MonoProp for an old capsule). A bipropellant
            // needs both topped, so this fills every one that has a station source, one pair per tick.
            double moved = 0.0;
            for (int t = 0; t < TopUp.Length; t++)
                moved += MoveOne(v, ours, mine, TopUp[t], dt);
            return moved;
        }

        /// <summary>
        /// One resource, one tick: the emptiest of OUR tanks pulled from the fullest of the STATION's.
        /// Zero when our tank is full, when the station has none of this resource, or when there is no
        /// such tank at all - all legitimate, and the caller sums them.
        /// </summary>
        private static double MoveOne(Vessel v, List<Part> ours, HashSet<Part> mine,
                                      string resourceName, double dt)
        {
            PartResource dst = null, src = null;
            double gap = DeadbandUnits, have = DeadbandUnits;

            for (int i = 0; i < ours.Count; i++)
                for (int k = 0; k < ours[i].Resources.Count; k++)
                {
                    PartResource r = ours[i].Resources[k];
                    if (r.resourceName != resourceName || !r.flowState) continue;
                    if (r.maxAmount - r.amount > gap) { gap = r.maxAmount - r.amount; dst = r; }
                }
            if (dst == null) return 0.0;                    // our tank is full (or we carry none)

            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (mine.Contains(p)) continue;             // ⛔ never drain ourselves into ourselves
                for (int k = 0; k < p.Resources.Count; k++)
                {
                    PartResource r = p.Resources[k];
                    if (r.resourceName != resourceName || !r.flowState) continue;
                    if (r.amount > have) { have = r.amount; src = r; }
                }
            }
            if (src == null) return 0.0;                    // the station has none of this to give

            double move = RateUnitsPerS * dt;
            if (move > gap) move = gap;
            if (move > src.amount) move = src.amount;
            if (move <= 0.0) return 0.0;

            src.amount -= move;
            dst.amount += move;
            TakenOnUnits += move;
            return move;
        }

        /// <summary>One line for the log when the refuel ends, saying which of the three ends it was.</summary>
        public static string Report(Vessel v)
        {
            double cap = DockedSide.MonoCapacity(v);
            string state = Full(v) ? "FULL"
                         : (Fraction(v) * 100.0).ToString("F0") + "% - the station had no more to give";
            return "capsule monopropellant " + DockedSide.Mono(v).ToString("F1")
                 + " / " + cap.ToString("F1") + " units (" + state + "), took on "
                 + TakenOnUnits.ToString("F1");
        }
    }
}
