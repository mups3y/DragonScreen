// DragonScreen - MissionProfile (PURE)
// ---- WHAT THE PROFILE ACTUALLY DECIDES ----
// ---- SCOPE: REAL MISSIONS, REAL EARTH ----
namespace DragonScreen
{
    public struct MissionProfile
    {
        public string Name;

        public bool Crewed;
        public bool HasRendezvous;
        public bool RecoverBooster;

        // ---- ASCENT ----
        public double TargetInclinationDeg;
        public double InsertionAltitudeM;

        // ---- RENDEZVOUS TARGET (only meaningful when HasRendezvous) ----
        public string StationVesselName;
        public double Wp0BelowM;
        public double Wp1AheadM;
        public double Wp2RangeM;
        public double KeepOutSphereM;

        // ---- RECOVERY / RETURN ----
        public double DroneshipLatDeg, DroneshipLonDeg;
        public double SplashdownLatDeg, SplashdownLonDeg;

        // ---- CONSUMABLES BUDGET (drives the LS commit gates against real TAC margins) ----
        public double MissionDurationDays;
        public double ConsumablesReserveDays;

        public bool Valid()
        {
            if (string.IsNullOrEmpty(Name)) return false;
            if (TargetInclinationDeg < 0.0 || TargetInclinationDeg > 180.0) return false;
            if (InsertionAltitudeM <= 0.0) return false;
            if (HasRendezvous)
            {
                if (string.IsNullOrEmpty(StationVesselName)) return false;
                if (!(Wp0BelowM > Wp1AheadM && Wp1AheadM > Wp2RangeM && Wp2RangeM > 0.0)) return false;
                if (KeepOutSphereM <= 0.0) return false;
            }
            if (MissionDurationDays < 0.0 || ConsumablesReserveDays < 0.0) return false;
            return true;
        }
    }

    public static class Missions
    {
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
            p.SplashdownLatDeg = 30.0;
            p.SplashdownLonDeg = -80.0;

            p.MissionDurationDays = 1.5;
            p.ConsumablesReserveDays = 0.5;
            return p;
        }

        public static MissionProfile Active = Crew2();
    }
}
