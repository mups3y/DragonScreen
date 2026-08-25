/*
 * DragonScreen - CrewGates (PURE)
 *
 * The real Crew Dragon crew procedure, as DATA: the gates the crew clears, in order, with the real
 * checklist items at each. Built from a MissionProfile so the mission's shape drives which gates exist -
 * a free-flyer with no rendezvous never presents the approach or undock gates.
 *
 * ---- TWO LEGS, MATCHING THE CONDUCTOR ----
 * The mission splits at the station: an OUTBOUND leg (countdown -> ascent -> rendezvous -> dock) and,
 * after the crew's stay, a RETURN leg (undock -> deorbit -> entry). CrewProcedureOps runs those as two
 * presses (ReturnArmed latches between them), so the gates come in two lists to match. Ascent and entry
 * are FLOWN between gates, not gated - the crew monitors them, with ABORT always available.
 *
 * ---- THE ITEMS ARE THE REAL CREW TASKS ----
 * Each item is either something the crew DOES (CrewAck - ingress, arm the launch escape system, "Dragon
 * crew - GO", GO for docking, GO for the deorbit burn) or something the system CONFIRMS from real state
 * (Auto - stable orbit, hard capture, cabin nominal, consumables sufficient). Sources for the sequence and
 * the zones: docs/REAL_CREW_DRAGON_MISSION.md and docs/CREW_PROCEDURES.md. Whatever the real Crew Dragon
 * crew does is what belongs here - see crew2-full-fidelity-no-deviation.
 */
using System.Collections.Generic;

namespace DragonScreen
{
    public static class CrewGates
    {
        /// <summary>The outbound leg's gates: countdown, then (if the mission docks) the approach + docking.</summary>
        public static Gate[] Outbound(MissionProfile p)
        {
            List<Gate> g = new List<Gate>();

            // ---- COUNTDOWN ----
            g.Add(G(GateId.Ingress, "CREW INGRESS",
                Ack("Crew ingress & strap-in"),
                Ack("Comm check with control")));

            if (p.Crewed)
                g.Add(G(GateId.SuitLeakCheck, "SUIT LEAK CHECK",
                    Ack("Pressurize suits"),
                    Ack("Suit leak check - PASS")));

            g.Add(G(GateId.HatchClose, "HATCH & CABIN",
                Ack("Close & lock hatch"),
                Auto("Cabin pressure nominal", AutoCheck.CabinNominal)));

            g.Add(G(GateId.GoForPropLoad, "GO FOR PROP LOAD",
                Ack("Launch Director: GO for propellant load")));

            g.Add(G(GateId.ArmLaunchEscape, "ARM LAUNCH ESCAPE",
                Ack("Arm Launch Escape System")));

            g.Add(G(GateId.InternalPower, "DRAGON CONFIGURED",
                Auto("On internal power", AutoCheck.OnInternalPower),
                Auto("Cabin environment nominal", AutoCheck.CabinNominal),
                Auto("Consumables sufficient", AutoCheck.ConsumablesOk)));

            g.Add(G(GateId.GoForLaunch, "GO / NO-GO FOR LAUNCH",
                Ack("GO/NO-GO poll complete"),
                Ack("Dragon crew - GO"),
                Ack("SpaceX - GO for launch")));

            // ---- ASCENT flies here (monitored, abort armed) ----

            // ---- RENDEZVOUS / PROXIMITY OPS (only a mission that docks) ----
            if (p.HasRendezvous)
            {
                g.Add(G(GateId.ApproachInitiation, "APPROACH INITIATION",
                    Auto("Stable orbit", AutoCheck.StableOrbit),
                    Ack("GO for Approach Initiation")));

                g.Add(G(GateId.HoldWp0, "HOLD - WP0 (400 m BELOW)",
                    Auto("Station-keeping at WP0", AutoCheck.AtWp0),
                    Ack("GO to enter Keep-Out Sphere")));

                g.Add(G(GateId.HoldWp1, "HOLD - WP1 (220 m)",
                    Auto("Station-keeping at WP1", AutoCheck.AtWp1),
                    Ack("GO to continue to 20 m")));

                g.Add(G(GateId.HoldWp2, "HOLD - WP2 (20 m)",
                    Auto("Station-keeping at WP2", AutoCheck.AtWp2),
                    Ack("GO for docking")));

                g.Add(G(GateId.DockingComplete, "DOCKING COMPLETE",
                    Auto("Hard capture", AutoCheck.Docked),
                    Ack("Vestibule leak check - PASS"),
                    Ack("Open hatch")));
            }

            return g.ToArray();
        }

        /// <summary>The return leg's gates: undock (if it docked), then GO for the deorbit burn.</summary>
        public static Gate[] Return(MissionProfile p)
        {
            List<Gate> g = new List<Gate>();

            if (p.HasRendezvous)
            {
                g.Add(G(GateId.GoForUndock, "GO FOR UNDOCK",
                    Ack("Suit up & ingress"),
                    Ack("Hatch closed & leak check - PASS"),
                    Auto("Cabin pressure nominal", AutoCheck.CabinNominal),
                    Ack("GO for undock")));
            }

            g.Add(G(GateId.GoForDeorbit, "GO FOR DEORBIT",
                Ack("Departure burns complete"),
                Auto("Consumables for return", AutoCheck.ConsumablesOk),
                Ack("GO for deorbit burn")));

            // ---- DEORBIT -> ENTRY -> chutes -> splashdown fly here (monitored) ----
            return g.ToArray();
        }

        // ---- builders ----
        private static Gate G(GateId id, string title, params ChecklistItem[] items)
        {
            Gate g = new Gate();
            g.Id = id;
            g.Title = title;
            g.Items = items;
            return g;
        }

        private static ChecklistItem Ack(string label)
        {
            ChecklistItem it = new ChecklistItem();
            it.Label = label; it.Kind = ItemKind.CrewAck; it.Auto = AutoCheck.None;
            return it;
        }

        private static ChecklistItem Auto(string label, AutoCheck a)
        {
            ChecklistItem it = new ChecklistItem();
            it.Label = label; it.Kind = ItemKind.Auto; it.Auto = a;
            return it;
        }
    }
}
