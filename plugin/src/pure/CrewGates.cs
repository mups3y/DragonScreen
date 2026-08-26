// DragonScreen — CrewGates  (autopilot rebuild L4: the real Crew Dragon gate catalog, mission-as-data)
// ============================================================================================
// The concrete gates G1..G15, built FROM a MissionProfile so the sequence is data, not code. Grounded in
// the real crew launch + return timeline (data/crew_missions.json countdown_gates + rendezvous_docking +
// return; the gate labels/MET are the transcribed NASA/SpaceX callouts). Each gate carries its checklist
// — CREW items the user taps (the real crew action) and AUTO items the system confirms from vessel state.
//
// Countdown  G1 ingress+comm · G2 suit leak · G3 hatch+cabin leak · G4 GO for prop load · G5 LES ARM ·
//            G6 internal power/config · G7 GO for launch ("Dragon crew — GO")
// Prox-ops   G9 GO for AI burn (7.5 km) · G10 hold WP0 (400 m below) GO · G11 hold WP1 (~220 m) GO ·
//            G12 WP2 (20 m) GO for docking · G13 docking complete (vestibule leak + hatch)
// Return     G14 GO for undock (suit up, hatch close, leak) · G15 GO for deorbit
// (Ascent G8 and Entry G16 are MONITORED phases with abort armed, not hold gates.)
//
// A FREE-FLYER (HasRendezvous == false) omits G9..G14 — no rendezvous, dock, or undock — going countdown
// → ascent → free-flight → G15 deorbit → return. PURE data; no vessel access here.
// ============================================================================================
namespace DragonScreen
{
    public enum GateId : byte
    {
        None = 0,
        IngressCommG1, SuitLeakG2, HatchCloseG3, PropLoadGoG4, LesArmG5, InternalPowerG6, LaunchGoG7,
        ApproachInitGoG9, WP0HoldG10, WP1HoldG11, WP2DockGoG12, DockingCompleteG13,
        UndockGoG14, DeorbitGoG15
    }

    public static class CrewGates
    {
        static ChecklistItem C(string s) { return ChecklistItem.Crew(s); }
        static ChecklistItem A(string s) { return ChecklistItem.Sys(s); }
        static Gate G(GateId id, string title, params ChecklistItem[] items)
        { Gate g; g.Id = id; g.Title = title; g.Items = items; return g; }

        // The seven countdown gates (shared by every crewed mission).
        public static Gate[] Countdown()
        {
            return new Gate[]
            {
                G(GateId.IngressCommG1, "CREW INGRESS & COMM CHECK",
                    C("Crew ingress, seated & strapped in"), C("Comm check with control")),
                G(GateId.SuitLeakG2, "SUIT LEAK CHECK",
                    C("Suits pressurised"), A("No suit leak — pressure holds")),
                G(GateId.HatchCloseG3, "HATCH CLOSE & CABIN LEAK CHECK",
                    C("Hatch closed & locked"), A("Cabin seal holds (ΔP nominal)")),
                G(GateId.PropLoadGoG4, "GO FOR PROPELLANT LOAD",
                    A("Cabin environment nominal"), C("Crew acknowledge LD poll — GO for prop load")),
                G(GateId.LesArmG5, "LAUNCH ESCAPE SYSTEM — ARM",
                    C("ARM the launch escape system"), A("Abort system armed")),
                G(GateId.InternalPowerG6, "DRAGON TO INTERNAL POWER",
                    A("On internal power"), C("Flight configuration confirmed")),
                G(GateId.LaunchGoG7, "GO/NO-GO FOR LAUNCH",
                    A("Consumables margin ≥ mission + reserve"), C("Dragon crew — GO"), C("GO for launch")),
            };
        }

        // The prox-ops holds (ISS crew only). WP distances from the DB waypoints block.
        public static Gate[] Approach()
        {
            return new Gate[]
            {
                G(GateId.ApproachInitGoG9, "GO FOR APPROACH INITIATION (7.5 km)",
                    A("On the approach corridor"), C("Mission control GO — crew concur")),
                G(GateId.WP0HoldG10, "HOLD — WP0 (400 m below) — GO TO ENTER KOS",
                    A("Station-keeping at WP0"), C("GO to proceed into the keep-out sphere")),
                G(GateId.WP1HoldG11, "HOLD — WP1 (~220 m on the V-bar) — GO",
                    A("Station-keeping at WP1"), C("GO to continue (manual takeover available)")),
                G(GateId.WP2DockGoG12, "HOLD — WP2 (20 m) — GO FOR DOCKING",
                    A("Docking ring aligned"), C("GO for docking (manual takeover available)")),
                G(GateId.DockingCompleteG13, "DOCKING COMPLETE — VESTIBULE",
                    A("Hard capture — 12 hooks closed"), C("Vestibule leak check"), C("Open hatch")),
            };
        }

        // The return gates.
        public static Gate[] Return(bool hasRendezvous)
        {
            if (hasRendezvous)
            {
                return new Gate[]
                {
                    G(GateId.UndockGoG14, "GO FOR UNDOCK",
                        C("Suit up, ingress, hatch closed"), A("Cabin leak check — seal holds"),
                        C("GO for undock")),
                    G(GateId.DeorbitGoG15, "GO FOR DEORBIT BURN",
                        A("Departure burns complete — stable orbit below the station"),
                        A("Consumables margin for return + reserve"), C("Mission control GO for deorbit")),
                };
            }
            // free-flyer: no undock, just the deorbit GO.
            return new Gate[]
            {
                G(GateId.DeorbitGoG15, "GO FOR DEORBIT BURN",
                    A("Free-flight complete"), A("Consumables margin for return + reserve"),
                    C("Mission control GO for deorbit")),
            };
        }

        // Look a gate up by id (for the conductor). Returns Items == null for None/not-found.
        public static Gate ById(MissionProfile m, GateId id)
        {
            foreach (Gate g in Countdown()) if (g.Id == id) return g;
            if (m.HasRendezvous) foreach (Gate g in Approach()) if (g.Id == id) return g;
            foreach (Gate g in Return(m.HasRendezvous)) if (g.Id == id) return g;
            Gate none; none.Id = GateId.None; none.Title = ""; none.Items = null; return none;
        }
    }
}
