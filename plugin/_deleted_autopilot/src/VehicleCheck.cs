// DragonScreen - VehicleCheck
// ---- WHY: THIS CLASS OF BUG HAS COST THREE FLIGHTS AND IT IS ALWAYS THE SAME SHAPE ----
// ---- ⛔ IT REPORTS. IT DOES NOT REFUSE. ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    internal static class VehicleCheck
    {
        private const string Tag = "[DragonScreen] VEHICLE: ";
        private static bool done;

        internal static void Reset() { done = false; }

        internal static void Report(Vessel v)
        {
            if (done || v == null || v.parts == null) return;
            done = true;
            try { Inspect(v); }
            catch (Exception e) { Debug.LogWarning(Tag + "check failed: " + e.Message); }
        }

        private static void Inspect(Vessel v)
        {
            int engineParts = 0, engineModules = 0, multiModeParts = 0;
            int rcsModules = 0, wheels = 0, decouplers = 0, chutes = 0;
            double rcsThrustKn = 0.0, wheelTorque = 0.0;
            List<string> notes = new List<string>();

            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (p == null) continue;

                List<ModuleEngines> es = p.Modules.GetModules<ModuleEngines>();
                if (es.Count > 0) engineParts++;
                engineModules += es.Count;

                // ---- THE ONE THAT FLEW A BOOSTER INTO THE PAD ----
                if (es.Count > 1)
                {
                    multiModeParts++;
                    double sum = 0.0, best = 0.0;
                    System.Text.StringBuilder ids = new System.Text.StringBuilder();
                    for (int m = 0; m < es.Count; m++)
                    {
                        sum += es[m].maxThrust;
                        if (es[m].maxThrust > best) best = es[m].maxThrust;
                        if (ids.Length > 0) ids.Append(", ");
                        ids.Append(string.IsNullOrEmpty(es[m].engineID) ? "?" : es[m].engineID);
                        ids.Append(" ").Append(es[m].maxThrust.ToString("F0")).Append(" kN");
                        ids.Append(es[m].isEnabled ? "" : " [disabled]");
                    }
                    notes.Add("'" + p.name + "' has " + es.Count + " engine modules on ONE part - "
                              + ids + ". Summing them gives " + sum.ToString("F0")
                              + " kN; the real figure is at most " + best.ToString("F0")
                              + " kN. Anything reading total thrust must ask KSP for "
                              + "availableThrust, not add maxThrust up.");
                }

                rcsModules += p.Modules.GetModules<ModuleRCS>().Count;
                List<ModuleRCS> rs = p.Modules.GetModules<ModuleRCS>();
                for (int m = 0; m < rs.Count; m++) rcsThrustKn += rs[m].thrusterPower;

                List<ModuleReactionWheel> ws = p.Modules.GetModules<ModuleReactionWheel>();
                wheels += ws.Count;
                for (int m = 0; m < ws.Count; m++)
                    wheelTorque += ws[m].PitchTorque;

                decouplers += p.Modules.GetModules<ModuleDecouple>().Count
                            + p.Modules.GetModules<ModuleAnchoredDecoupler>().Count;
                chutes += p.Modules.GetModules<ModuleParachute>().Count;
            }

            Vector3d moi = v.MOI;
            Debug.Log(Tag + v.vesselName + " - " + v.parts.Count + " parts, "
                      + v.GetTotalMass().ToString("F2") + " t"
                      + " | engines " + engineParts + " part(s)/" + engineModules + " module(s)"
                      + " | RCS " + rcsModules + " (" + rcsThrustKn.ToString("F1") + " kN total)"
                      + " | wheels " + wheels + " (" + wheelTorque.ToString("F1") + " kN.m pitch)"
                      + " | decouplers " + decouplers + " | chutes " + chutes
                      + " | MoI " + moi.x.ToString("F0") + "/" + moi.y.ToString("F0")
                      + "/" + moi.z.ToString("F0"));

            // ---- THE TORQUE-vs-INERTIA CHECK THAT WOULD HAVE CAUGHT THE 0.05 deg/s ASCENT ----
            if (moi.x > 1.0)
            {
                double wheelsOnly = Attitude.ArrestableRate(45.0 * Math.PI / 180.0,
                                                            wheelTorque, moi.x) * 180.0 / Math.PI;
                if (wheelsOnly < 0.45)
                    notes.Add("reaction wheels alone give " + wheelsOnly.ToString("F2")
                              + " deg/s of pitch rate against a MoI of " + moi.x.ToString("F0")
                              + " - the gravity turn needs about 0.45. This vehicle CANNOT pitch on "
                              + "wheels; gimbal and RCS must be available and counted.");
            }

            if (multiModeParts == 0 && engineModules > engineParts)
                notes.Add("more engine modules than engine parts, but none on a single part - "
                          + "check how thrust is being totalled.");

            if (notes.Count == 0)
            {
                Debug.Log(Tag + "nothing the flight software would be surprised by.");
                return;
            }
            for (int i = 0; i < notes.Count; i++)
                Debug.LogWarning(Tag + notes[i]);
        }
    }
}
