// DragonScreen - Refuel
// ---- WHY THIS IS THE LINCHPIN OF THE WHOLE MISSION ----
// ---- ⛔ WHICH TANK IS OURS IS THE ENTIRE PROBLEM. SEE `DockedSide`. ----
// ---- ⚠ AND IT YIELDS TO ANY OTHER AUTHORITY ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class Refuel
    {
        private const string Tag = "[DragonScreen] ";

        public const double RateUnitsPerS = 8.0;
        public const double DeadbandUnits = 0.1;
        public const double FullFraction = 0.999;

        public static double TakenOnUnits { get; private set; }

        public static void Begin() { TakenOnUnits = 0.0; }

        private static readonly string[] TopUp = { "MMH", "NTO" };

        public static bool Full(Vessel v)
        {
            return DockedSide.ReturnFraction(v) >= FullFraction;
        }

        public static double Fraction(Vessel v)
        {
            return DockedSide.ReturnFraction(v);
        }

        public static double Tick(Vessel v, double dt)
        {
            if (v == null || dt <= 0.0) return 0.0;
            if (!DockedSide.Docked(v)) return 0.0;

            List<Part> ours = DockedSide.Ours(v);
            HashSet<Part> mine = new HashSet<Part>(ours);

            double moved = 0.0;
            for (int t = 0; t < TopUp.Length; t++)
                moved += MoveOne(v, ours, mine, TopUp[t], dt);
            return moved;
        }

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
            if (dst == null) return 0.0;

            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (mine.Contains(p)) continue;
                for (int k = 0; k < p.Resources.Count; k++)
                {
                    PartResource r = p.Resources[k];
                    if (r.resourceName != resourceName || !r.flowState) continue;
                    if (r.amount > have) { have = r.amount; src = r; }
                }
            }
            if (src == null) return 0.0;

            double move = RateUnitsPerS * dt;
            if (move > gap) move = gap;
            if (move > src.amount) move = src.amount;
            if (move <= 0.0) return 0.0;

            src.amount -= move;
            dst.amount += move;
            TakenOnUnits += move;
            return move;
        }

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
