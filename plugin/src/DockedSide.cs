/*
 * DragonScreen - DockedSide
 *
 * GLUE. Which parts are OURS when we are docked to something.
 *
 * ---- ⛔ WHEN DOCKED, KSP MERGES BOTH CRAFT INTO ONE `Vessel`. ----
 * There is no "our vessel" and "their vessel" any more - `v.parts` is the capsule AND the station,
 * one flat list, and every resource total taken from it is the pair added together. Anything that
 * asks "how much monopropellant do we have" while berthed gets the station's answer.
 *
 * ---- THIS IS A LIVE BUG IN F9I AND IT MUST NOT BE PORTED ----
 * Reported from the Falcon 9 Interface, 2026-08-11: the station refuel tops the Dragon's tank right
 * up, and the undock then refuses with "the fuel tanks are not full". **The Dragon's tank IS full.**
 * `StMono()` is reading the merged vessel, so it reports the station's 6 237-unit farm, which of
 * course is not full and never will be. The capsule's tank is the one that matters and it is the one
 * nobody was looking at.
 *
 * ⚠ AND IT BREAKS THE REFUEL ITSELF, NOT JUST THE MESSAGE. A transfer from the station into the
 * capsule moves propellant WITHIN the merged vessel, so the merged total does not change at all. Our
 * top-up waits for the total to stop rising before it lets go - against the merged total that test can
 * never see a single unit of progress, so it would always fall through on its flat-line timeout and
 * undock without noticing whether anything had been taken on.
 *
 * ---- HOW THE SPLIT IS MADE ----
 * Walk out from the part we are CONTROLLING FROM and refuse to cross a docking joint. Everything
 * reached that way is what will still be attached to us after the undock, by construction - which is
 * the same definition `UndockOps` uses to decide which port to release, and for the same reason:
 * with two Dragons berthed on one station, "the first one found" is somebody else's capsule.
 *
 * Not docked? Then nothing is excluded and this returns the whole vessel, which is the right answer.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class DockedSide
    {
        /// <summary>
        /// The parts on OUR side of any docking joint. Never null; the whole vessel when undocked.
        /// </summary>
        public static List<Part> Ours(Vessel v)
        {
            List<Part> ours = new List<Part>();
            if (v == null) return ours;

            Part start = Control(v);
            if (start == null) { ours.AddRange(v.parts); return ours; }

            // Breadth-first over the part tree, never traversing a docking link.
            HashSet<Part> seen = new HashSet<Part>();
            Queue<Part> queue = new Queue<Part>();
            queue.Enqueue(start);
            seen.Add(start);

            while (queue.Count > 0)
            {
                Part p = queue.Dequeue();
                ours.Add(p);

                if (p.parent != null && !seen.Contains(p.parent) && !Joint(p, p.parent))
                { seen.Add(p.parent); queue.Enqueue(p.parent); }

                for (int i = 0; i < p.children.Count; i++)
                {
                    Part c = p.children[i];
                    if (c == null || seen.Contains(c) || Joint(p, c)) continue;
                    seen.Add(c); queue.Enqueue(c);
                }
            }
            return ours;
        }

        /// <summary>True when we are part of a merged vessel - something is docked to us.</summary>
        public static bool Docked(Vessel v)
        {
            if (v == null) return false;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleDockingNode> ns = v.parts[i].Modules.GetModules<ModuleDockingNode>();
                for (int m = 0; m < ns.Count; m++)
                    if (ns[m].otherNode != null) return true;
            }
            return false;
        }

        /// <summary>
        /// How much of a resource is on OUR side. This is the number every budget wants.
        /// </summary>
        public static double Resource(Vessel v, string resourceName)
        {
            double t = 0.0;
            List<Part> ours = Ours(v);
            for (int i = 0; i < ours.Count; i++)
                for (int k = 0; k < ours[i].Resources.Count; k++)
                    if (ours[i].Resources[k].resourceName == resourceName)
                        t += ours[i].Resources[k].amount;
            return t;
        }

        /// <summary>Capacity of a resource on OUR side - the denominator for "is it full?".</summary>
        public static double Capacity(Vessel v, string resourceName)
        {
            double t = 0.0;
            List<Part> ours = Ours(v);
            for (int i = 0; i < ours.Count; i++)
                for (int k = 0; k < ours[i].Resources.Count; k++)
                    if (ours[i].Resources[k].resourceName == resourceName)
                        t += ours[i].Resources[k].maxAmount;
            return t;
        }

        /// <summary>Monopropellant on our side, units. The one the return budget is built on.</summary>
        public static double Mono(Vessel v) { return Resource(v, "MonoPropellant"); }
        public static double MonoCapacity(Vessel v) { return Capacity(v, "MonoPropellant"); }

        /// <summary>
        /// The part we are flying from.
        ///
        /// ⚠ NOT `v.rootPart`. On a merged vessel the root can perfectly well be a station truss, and
        /// starting the walk there returns the station as "ours" and the capsule as "theirs" - the
        /// original bug, inverted. The reference transform is whatever the crew is controlling from,
        /// which on a berthed Dragon is the Dragon.
        /// </summary>
        private static Part Control(Vessel v)
        {
            Part p = v.GetReferenceTransformPart();
            if (p != null) return p;

            // No control part - fall back to the capsule by capability, the way the rest of the
            // plugin identifies vehicles (`falcon-detect-by-capability`).
            for (int i = 0; i < v.parts.Count; i++)
                if (VehicleParts.IsPod(v.parts[i].name)) return v.parts[i];
            return v.rootPart;
        }

        /// <summary>Is the link between these two parts a DOCKING joint rather than structure?</summary>
        private static bool Joint(Part a, Part b)
        {
            return LinksTo(a, b) || LinksTo(b, a);
        }

        private static bool LinksTo(Part a, Part b)
        {
            List<ModuleDockingNode> ns = a.Modules.GetModules<ModuleDockingNode>();
            for (int m = 0; m < ns.Count; m++)
            {
                ModuleDockingNode n = ns[m];
                if (n.otherNode != null && n.otherNode.part == b) return true;
            }
            return false;
        }
    }
}
