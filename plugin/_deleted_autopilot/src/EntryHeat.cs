// DragonScreen - EntryHeat
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public struct HeatSample
    {
        public bool Present;
        public double AblatorFrac;
        public double CharFrac;
        public double ShieldK, ShieldMaxK;
        public double ShieldTempFrac;
        public double FluxKw;
        public double AblationFlux;
        public double PeakSkinK;
    }

    public static class EntryHeat
    {
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
