/*
 * DragonScreen - LifeSupportBridge
 *
 * GLUE. Reads the Crew Dragon's TAC Life Support state off the vessel so the cabin displays run on the
 * REAL simulation instead of the old sine-wander model.
 *
 * ---- WHAT TAC GIVES US, AND WHAT IT DOES NOT ----
 * The install runs TAC Life Support v0.18 (GameData/ThunderAerospace/TacLifeSupport). The Crew Dragon
 * pods carry TAC's LifeSupportModule (TundraExploration/Patches/Extra_TAC.cfg), so TAC really consumes
 * Oxygen/Food/Water and fills CarbonDioxide/Waste/WasteWater on the vessel. Those are ordinary
 * PartResources, so we read them with KSP's own totals - NO hard dependency on TacLifeSupport.dll, and a
 * graceful "Present == false" when TAC (or the resource) is absent, exactly like the kOS/MechJeb-absent
 * rule.
 *
 * ⚠ TAC's Oxygen/CarbonDioxide are STORED CONSUMABLES - the breathing-O2 SUPPLY and the CAPTURED CO2 -
 * NOT the cabin air. TAC has no cabin-atmosphere or pressure model. So this returns the real SUPPLY and
 * ACCUMULATOR fractions, and CabinEnvironment turns those into the ppO2 / CO2 gauge readings by a stated
 * model (a gauge that holds nominal while the supply is healthy and degrades as it runs down). The number
 * is real; the mapping to a cabin partial pressure is a model, and is labelled one.
 *
 * ---- DOCKED: READ THE DRAGON'S SIDE, NOT THE STATION'S ----
 * Berthed, KSP merges the station into v.parts, and the station's HabTech2 LS farm dwarfs the capsule's.
 * DockedSide.Resource/Capacity walk out from the control part and refuse to cross the docking joint, so
 * these are the Dragon's own tanks - the same isolation the propellant refuel uses (see DockedSide).
 */
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    /// <summary>The Dragon's TAC life-support state. Fractions are 0..1 of capacity.</summary>
    public struct LsState
    {
        /// <summary>TAC is active on this vessel - it has an Oxygen tank. False = fall back to the model.</summary>
        public bool Present;
        /// <summary>Breathing-O2 SUPPLY remaining, 0..1. Real (TAC consumes it).</summary>
        public double Oxygen01;
        /// <summary>Captured-CO2 ACCUMULATOR fill, 0..1 - the scrubber-saturation proxy. Real (TAC fills it).</summary>
        public double Co201;
    }

    public static class LifeSupportBridge
    {
        // TAC resource names, from GameData/ThunderAerospace/TacLifeSupport/LifeSupport.cfg.
        private const string Oxygen = "Oxygen";
        private const string CarbonDioxide = "CarbonDioxide";
        private const string Food = "Food";
        private const string Water = "Water";

        /// <summary>Read the Dragon's TAC state. Never throws; Present == false when TAC is not modelling it.</summary>
        public static LsState Read(Vessel v)
        {
            LsState s = new LsState();
            if (v == null) return s;

            double o2Cap = DockedSide.Capacity(v, Oxygen);
            if (o2Cap <= 0.0) return s;              // no TAC Oxygen tank -> not present, use the model
            s.Present = true;
            s.Oxygen01 = Clamp01(DockedSide.Resource(v, Oxygen) / o2Cap);

            double co2Cap = DockedSide.Capacity(v, CarbonDioxide);
            s.Co201 = (co2Cap > 0.0) ? Clamp01(DockedSide.Resource(v, CarbonDioxide) / co2Cap) : 0.0;
            return s;
        }

        /// <summary>
        /// Consumable margins for the Dragon's crew - days of Food/Water/O2 remaining and the O2
        /// time-to-crew-loss - from the real TAC amounts aboard (our side of the joint). This is the
        /// genuine data behind the launch and de-orbit commit gates and the ECLSS readout. Present ==
        /// false when TAC is not modelling this vessel (no Oxygen tank), so the gate must not block on it.
        /// </summary>
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

        /// <summary>Fractions AND margins in ONE walk of the Dragon's side - for the per-row recorder, so
        /// the flight logs O2/CO2 and days-remaining without six BFS traversals per sample.</summary>
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

    /// <summary>One-walk life-support sample for the recorder: cabin fractions + consumable margins.</summary>
    public struct LsSample
    {
        public bool Present;
        public double Oxygen01, Co201;
        public LsMargins Margins;
    }
}
