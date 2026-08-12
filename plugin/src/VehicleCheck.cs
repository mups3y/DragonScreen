/*
 * DragonScreen - VehicleCheck
 *
 * READ THE VEHICLE AND SAY WHETHER IT IS THE ONE THE FLIGHT SOFTWARE WAS WRITTEN FOR.
 *
 * ---- WHY: THIS CLASS OF BUG HAS COST THREE FLIGHTS AND IT IS ALWAYS THE SAME SHAPE ----
 * From CLAUDE.md's own record, all found in flight, none catchable headlessly:
 *
 *   · `AvailableTorque` counted reaction wheels only while its comment claimed RCS was included -
 *     9.5 kN.m against a 21 949 t.m^2 pitch MoI, a rate limit of 0.05 deg/s where the gravity turn
 *     needs 0.45. The vehicle would have rolled fine and simply refused to pitch over.
 *   · The Tundra first stage is ONE part with THREE mutually exclusive `ModuleEnginesFX`. Summing
 *     them gave 5030 kN against a real 2560, and made the one-engine landing case believe it had
 *     1676 kN against a real 764. Ignition too low, into the pad.
 *   · `"K1"` matched from a PAW title instead of `part.name`, so the FIRST stage flew the SECOND
 *     stage's pitch law for an entire ascent.
 *
 * Every one of those is a disagreement between what the code assumed and what the part actually is,
 * and every one was discovered by losing a vehicle. This asks the question on the pad instead.
 *
 * ---- ⛔ IT REPORTS. IT DOES NOT REFUSE. ----
 * An outside review suggested failing the build or refusing to engage. Refusing is wrong here: this
 * is a simulator, the crew may deliberately fly an odd craft, and a hard refusal on a check that is
 * itself only as good as its expectations would ground flights over its own bugs. It logs loudly,
 * once, at engage - which is the moment someone is looking - and the recorder carries the result.
 */
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

        /// <summary>
        /// Read the vessel and report anything the flight software would be surprised by. Once per
        /// vessel, at the moment a controller takes it.
        /// </summary>
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
                // Several ModuleEngines on ONE part are mutually exclusive modes, not more engines.
                // Anything that sums them is reading a thrust the vehicle does not have.
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
            // Wheels alone against pitch inertia. If that ratio cannot produce the rate the ascent
            // needs, the vehicle will hold heading beautifully and refuse to pitch over.
            if (moi.x > 1.0)
            {
                double wheelsOnly = wheelTorque * AttitudeCascade.DefaultMaxStoppingTime / moi.x
                                    * 180.0 / Math.PI;
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
