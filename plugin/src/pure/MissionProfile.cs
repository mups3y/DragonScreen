/*
 * DragonScreen - MissionProfile (PURE)
 *
 * One Crew Dragon mission, as DATA. The autopilot is mission-agnostic: Crew-2, a crew rotation to a
 * different port, a private ISS flight, or a free-flyer are all the SAME flight software driven by a
 * different profile - never bespoke code. This is that profile, and Crew2Profile() is the reference one.
 *
 * ---- WHAT THE PROFILE ACTUALLY DECIDES ----
 * The low-level guidance (UPFG ascent, hoverslam recovery, named-burn rendezvous, bank-angle entry) is
 * already tuned to the real Crew-2 numbers. So the profile does NOT re-tune it; Crew2Profile carries those
 * same values, which is why adding "any mission" regresses nothing. What the profile decides is the shape
 * of the MISSION:
 *   - HasRendezvous == false makes a FREE-FLYER (Inspiration4/Polaris-style): the conductor and the crew
 *     gates skip rendezvous / dock / undock entirely - launch, orbit, return.
 *   - The rendezvous target, approach geometry (WP0/WP1/WP2, keep-out sphere) and the recovery/splashdown
 *     sites are the per-mission facts the gates, the timeline and the displays read.
 *   - The consumables budget (mission days + reserve) is what the launch and de-orbit LS commit gates
 *     check against real TAC margins (see LifeSupport).
 *
 * ---- SCOPE: REAL MISSIONS, REAL EARTH ----
 * "Any Crew Dragon mission" means any REAL one in RSS/RO on Earth. It does not re-open the stock Kerbin /
 * tourist paths (removed 2026-08-25); the stock station-ferry lives in the separate DragonScreen-Stock
 * build. See dragonscreen-two-builds-split.
 */
namespace DragonScreen
{
    public struct MissionProfile
    {
        /// <summary>Human name for the mission - shown on the screen. e.g. "CREW-2".</summary>
        public string Name;

        /// <summary>A crewed Dragon (the LS gates and the crew poll apply). Cargo Dragon would be false.</summary>
        public bool Crewed;
        /// <summary>This mission rendezvous and docks. False = a free-flyer: no rendezvous/dock/undock.</summary>
        public bool HasRendezvous;
        /// <summary>The booster returns to a droneship. False = expendable (no recovery gates).</summary>
        public bool RecoverBooster;

        // ---- ASCENT ----
        /// <summary>Target orbital-plane inclination, degrees. ISS = 51.6.</summary>
        public double TargetInclinationDeg;
        /// <summary>Target insertion altitude, metres (real Earth ISS ~ 4.2e5).</summary>
        public double InsertionAltitudeM;

        // ---- RENDEZVOUS TARGET (only meaningful when HasRendezvous) ----
        /// <summary>The vessel to find and dock with. StationApproach falls back to vesselType==Station.</summary>
        public string StationVesselName;
        /// <summary>WP0: metres directly BELOW the station on the +R-bar (a hold).</summary>
        public double Wp0BelowM;
        /// <summary>WP1: metres in FRONT of the station on the docking axis (a hold).</summary>
        public double Wp1AheadM;
        /// <summary>WP2: metres from the docking port (a hold, then contact).</summary>
        public double Wp2RangeM;
        /// <summary>Keep-Out Sphere radius, metres. ISS = 200.</summary>
        public double KeepOutSphereM;

        // ---- RECOVERY / RETURN ----
        public double DroneshipLatDeg, DroneshipLonDeg;
        /// <summary>Splashdown aim point, degrees. For display + return targeting.</summary>
        public double SplashdownLatDeg, SplashdownLonDeg;

        // ---- CONSUMABLES BUDGET (drives the LS commit gates against real TAC margins) ----
        /// <summary>Planned mission length, days. The LS gate needs this many days of consumables...</summary>
        public double MissionDurationDays;
        /// <summary>...plus this reserve, before it will GO.</summary>
        public double ConsumablesReserveDays;

        /// <summary>The profile is self-consistent enough to fly. Cheap sanity, used by the test + on load.</summary>
        public bool Valid()
        {
            if (string.IsNullOrEmpty(Name)) return false;
            if (TargetInclinationDeg < 0.0 || TargetInclinationDeg > 180.0) return false;
            if (InsertionAltitudeM <= 0.0) return false;
            if (HasRendezvous)
            {
                if (string.IsNullOrEmpty(StationVesselName)) return false;
                // The L-approach only makes sense outside-in: WP0 (below) is the furthest, WP2 the closest.
                if (!(Wp0BelowM > Wp1AheadM && Wp1AheadM > Wp2RangeM && Wp2RangeM > 0.0)) return false;
                if (KeepOutSphereM <= 0.0) return false;
            }
            if (MissionDurationDays < 0.0 || ConsumablesReserveDays < 0.0) return false;
            return true;
        }
    }

    public static class Missions
    {
        /// <summary>
        /// The reference mission - SpaceX Crew-2 to the ISS. Numbers match the values the guidance is
        /// already tuned to (inclination 51.6, the "ISS USOS Real Size" target, WP0/WP1/WP2 = 400/220/20 m,
        /// the 200 m KOS, the droneship deck at 32.79 / -76.64), so selecting it changes no guidance.
        /// Crew-2 stayed ~6 months docked; the LS budget below is the CAPSULE's own free-flight endurance
        /// (Dragon carries a few days of consumables for the transit + return, the station keeps the crew),
        /// which is what the capsule-side commit gate should check.
        /// </summary>
        public static MissionProfile Crew2()
        {
            MissionProfile p = new MissionProfile();
            p.Name = "CREW-2";
            p.Crewed = true;
            p.HasRendezvous = true;
            p.RecoverBooster = true;

            p.TargetInclinationDeg = 51.6;
            p.InsertionAltitudeM = 420000.0;

            p.StationVesselName = "ISS USOS Real Size";
            p.Wp0BelowM = 400.0;
            p.Wp1AheadM = 220.0;
            p.Wp2RangeM = 20.0;
            p.KeepOutSphereM = 200.0;

            p.DroneshipLatDeg = 32.787551;
            p.DroneshipLonDeg = -76.644507;
            p.SplashdownLatDeg = 30.0;      // off the Florida coast; return targeting owns the precise point
            p.SplashdownLonDeg = -80.0;

            // The commit gate checks the CAPSULE's own endurance for the transit + return, NOT the months
            // docked (the station keeps the crew). Kept conservative (1.5 + 0.5 = 2 days) so a normally
            // provisioned Dragon always passes and only a genuinely near-empty capsule is a NO-GO - a
            // too-high bar would false-block the pad, since TAC's auto-loaded amounts are the vehicle's.
            p.MissionDurationDays = 1.5;
            p.ConsumablesReserveDays = 0.5;
            return p;
        }

        /// <summary>The active mission the conductor, gates and displays read. Defaults to Crew-2.</summary>
        public static MissionProfile Active = Crew2();
    }
}
