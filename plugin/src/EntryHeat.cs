/*
 * DragonScreen - EntryHeat
 *
 * GLUE. Reads the heat-shield's thermal + ablator state during entry, so the re-entry is FULLY
 * instrumented: how hot the shield is, how close to its limit, how much PICA-X ablator is left, and the
 * heat flux it is taking right now. This is the data the autopilot needs to learn how far it can push
 * the re-entry steering (a hotter attitude buys more cross-range but eats ablator and skin margin).
 *
 * Detect-by-capability: the heat shield is whatever part carries a ModuleAblator (RO's PICA-X on the
 * Dragon: Ablator 400, ablationTempThresh 1250 K, chars into CharredAblator). No part-name test.
 */
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public struct HeatSample
    {
        public bool Present;
        /// <summary>Ablator (PICA-X) remaining, fraction 0..1. 0 = the shield is spent.</summary>
        public double AblatorFrac;
        /// <summary>Charred fraction 0..1 - how much of the shield has pyrolysed.</summary>
        public double CharFrac;
        /// <summary>Heat-shield skin temperature, K, and its limit.</summary>
        public double ShieldK, ShieldMaxK;
        /// <summary>Heat-shield skin temperature as a fraction of its max (1.0 = at the limit).</summary>
        public double ShieldTempFrac;
        /// <summary>Total heating flux into the shield right now, kW (convection + radiation).</summary>
        public double FluxKw;
        /// <summary>The ModuleAblator's own reported ablation flux (what it is burning off).</summary>
        public double AblationFlux;
        /// <summary>Hottest skin temperature anywhere on the vessel, K - the airframe limit watch.</summary>
        public double PeakSkinK;
    }

    public static class EntryHeat
    {
        /// <summary>
        /// Sample the entry thermal state of <paramref name="v"/>. Walks the parts once; finds the
        /// ablator-bearing heat shield and reads its ablator, char, skin temperature and heat flux, plus
        /// the hottest skin on the whole vessel. Returns Present=false if there is no ablator part.
        /// </summary>
        public static HeatSample Sample(Vessel v)
        {
            HeatSample h = new HeatSample();
            if (v == null || v.parts == null) return h;

            double peakSkin = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p == null) continue;
                if (p.skinTemperature > peakSkin) peakSkin = p.skinTemperature;

                List<ModuleAblator> abs = p.Modules.GetModules<ModuleAblator>();
                if (abs.Count == 0) continue;

                // The heat shield. Read its ablator + char amounts and its own thermal state.
                h.Present = true;
                string res = ReadStr(abs[0], "ablativeResource");
                if (string.IsNullOrEmpty(res)) res = "Ablator";
                string charRes = ReadStr(abs[0], "outputResource");
                if (string.IsNullOrEmpty(charRes)) charRes = "CharredAblator";

                double abAmt = 0.0, abMax = 0.0, chAmt = 0.0, chMax = 0.0;
                for (int k = 0; k < p.Resources.Count; k++)
                {
                    PartResource r = p.Resources[k];
                    if (r.resourceName == res) { abAmt = r.amount; abMax = r.maxAmount; }
                    else if (r.resourceName == charRes) { chAmt = r.amount; chMax = r.maxAmount; }
                }
                h.AblatorFrac = (abMax > 0.0) ? abAmt / abMax : 0.0;
                h.CharFrac = (chMax > 0.0) ? chAmt / chMax : 0.0;

                h.ShieldK = p.skinTemperature;
                h.ShieldMaxK = p.skinMaxTemp;
                h.ShieldTempFrac = (p.skinMaxTemp > 0.0) ? p.skinTemperature / p.skinMaxTemp : 0.0;
                // Heating INTO the part this frame: convection + radiation (kW; conduction moves it around).
                h.FluxKw = p.thermalConvectionFlux + p.thermalRadiationFlux;
                h.AblationFlux = ReadDouble(abs[0], "flux");
            }
            h.PeakSkinK = peakSkin;
            return h;
        }

        private static string ReadStr(PartModule pm, string field)
        {
            try { BaseField f = pm.Fields[field]; if (f == null) return null;
                  object o = f.GetValue(pm); return o == null ? null : o.ToString(); }
            catch (System.Exception) { return null; }
        }

        private static double ReadDouble(PartModule pm, string field)
        {
            try { BaseField f = pm.Fields[field]; if (f == null) return 0.0;
                  object o = f.GetValue(pm); return o == null ? 0.0 : System.Convert.ToDouble(o); }
            catch (System.Exception) { return 0.0; }
        }
    }
}
