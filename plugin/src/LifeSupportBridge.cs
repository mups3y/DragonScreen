// DragonScreen - LifeSupportBridge
// ---- WHAT TAC GIVES US, AND WHAT IT DOES NOT ----
// ---- DOCKED: READ THE DRAGON'S SIDE, NOT THE STATION'S ----
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public struct LsState
    {
        public bool Present;
        public double Oxygen01;
        public double Co201;
    }

    public static class LifeSupportBridge
    {
        private const string Oxygen = "Oxygen";
        private const string CarbonDioxide = "CarbonDioxide";
        private const string Food = "Food";
        private const string Water = "Water";

        public static LsState Read(Vessel v)
        {
            LsState s = new LsState();
            if (v == null) return s;

            double o2Cap = DockedSide.Capacity(v, Oxygen);
            if (o2Cap <= 0.0) return s;
            s.Present = true;
            s.Oxygen01 = Clamp01(DockedSide.Resource(v, Oxygen) / o2Cap);

            double co2Cap = DockedSide.Capacity(v, CarbonDioxide);
            s.Co201 = (co2Cap > 0.0) ? Clamp01(DockedSide.Resource(v, CarbonDioxide) / co2Cap) : 0.0;
            return s;
        }

        public static LsMargins Margins(Vessel v)
        {
            if (v == null) return LifeSupport.Margins(false, 0, 0.0, 0.0, 0.0);
            bool present = DockedSide.Capacity(v, Oxygen) > 0.0;
            int crew = v.GetCrewCount();
            double food = DockedSide.Resource(v, Food);
            double water = DockedSide.Resource(v, Water);
            double oxygen = DockedSide.Resource(v, Oxygen);
            return LifeSupport.Margins(present, crew, food, water, oxygen);
        }

        public static LsSample Sample(Vessel v)
        {
            LsSample s = new LsSample();
            if (v == null) { s.Margins = LifeSupport.Margins(false, 0, 0.0, 0.0, 0.0); return s; }

            double o2 = 0, o2cap = 0, co2 = 0, co2cap = 0, food = 0, water = 0;
            List<Part> ours = DockedSide.Ours(v);
            for (int i = 0; i < ours.Count; i++)
                for (int k = 0; k < ours[i].Resources.Count; k++)
                {
                    PartResource res = ours[i].Resources[k];
                    switch (res.resourceName)
                    {
                        case Oxygen:        o2 += res.amount;  o2cap += res.maxAmount;  break;
                        case CarbonDioxide: co2 += res.amount; co2cap += res.maxAmount; break;
                        case Food:          food += res.amount;  break;
                        case Water:         water += res.amount; break;
                    }
                }

            s.Present = o2cap > 0.0;
            s.Oxygen01 = (o2cap > 0.0) ? Clamp01(o2 / o2cap) : 0.0;
            s.Co201 = (co2cap > 0.0) ? Clamp01(co2 / co2cap) : 0.0;
            s.Margins = LifeSupport.Margins(s.Present, v.GetCrewCount(), food, water, o2);
            return s;
        }

        private static double Clamp01(double x) { return (x < 0.0) ? 0.0 : (x > 1.0) ? 1.0 : x; }
    }

    public struct LsSample
    {
        public bool Present;
        public double Oxygen01, Co201;
        public LsMargins Margins;
    }
}
