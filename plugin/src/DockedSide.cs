// DragonScreen - DockedSide
// ---- ⛔ WHEN DOCKED, KSP MERGES BOTH CRAFT INTO ONE `Vessel`. ----
// ---- THIS IS A LIVE BUG IN F9I AND IT MUST NOT BE PORTED ----
// ---- HOW THE SPLIT IS MADE ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class DockedSide
    {
        public static List<Part> Ours(Vessel v)
        {
            List<Part> ours = new List<Part>();
            if (v == null) return ours;

            Part start = Control(v);
            if (start == null) { ours.AddRange(v.parts); return ours; }

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

        public static double Mono(Vessel v) { return Resource(v, "MonoPropellant"); }
        public static double MonoCapacity(Vessel v) { return Capacity(v, "MonoPropellant"); }

        public static readonly string[] ReturnProps = { "MMH", "NTO" };

        public static double ReturnFraction(Vessel v)
        {
            double worst = 1.0; bool any = false;
            for (int i = 0; i < ReturnProps.Length; i++)
            {
                double cap = Capacity(v, ReturnProps[i]);
                if (cap <= 0.0) continue;
                any = true;
                double f = Resource(v, ReturnProps[i]) / cap;
                if (f < worst) worst = f;
            }
            if (any) return worst;
            double mc = MonoCapacity(v);
            return (mc > 0.0) ? Mono(v) / mc : 1.0;
        }

        private static Part Control(Vessel v)
        {
            Part p = v.GetReferenceTransformPart();
            if (p != null) return p;

            for (int i = 0; i < v.parts.Count; i++)
                if (VehicleParts.IsPod(v.parts[i].name)) return v.parts[i];
            return v.rootPart;
        }

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
