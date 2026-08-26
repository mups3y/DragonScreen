// DragonScreen - LaunchWindowOps
// ---- ⛔ THE LAW WAS PORTED AND NOTHING EVER CALLED IT. ----
// ---- WHAT A BAD WINDOW COSTS ----
// ---- ⚠ TWO OF THE FOUR INPUTS ARE MEASUREMENTS, NOT SETTINGS ----
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class LaunchWindowOps
    {
        private const string Tag = "[DragonScreen] ";

        // ---- F9I's SEEDS. station_ops.ks:72-73, 237, 277. Replace with our own measurements. ----
        [Tunable] public static double AscentTimeS = 528.5;
        [Tunable] public static double AscentLonDeg = 12.98;
        [Tunable] public static double PhaseBiasDeg = 1.84;
        [Tunable] public static double TrailDistM = 1000.0;

        public const double WindowCapS = 7200.0;

        public const double PlaneMatchMinIncDeg = 5.0;

        [Tunable] public static double PlaneWindowCapS = 400000.0;

        [Tunable] public static double PlaneWindowMinLeadS = 172800.0;

        [Tunable] public static double LaunchLanBiasDeg = 0.0;

        // ==================================================================================
        // ==================================================================================

        [Tunable] public static double DesiredPhaseLeadDeg = 25.0;

        [Tunable] public static double AscentArcDeg = 18.0;

        [Tunable] public static double AcceptLeadMinDeg = 20.0;
        [Tunable] public static double AcceptLeadMaxDeg = 160.0;

        [Tunable] public static double PhaseSearchMaxS = 604800.0;

        public static double WaitS = -1.0;
        public static string Note = "-";

        public static double LastRequiredLeadDeg;

        public static double SecondsToWait(Vessel v)
        {
            Note = "-";
            WaitS = 0.0;
            if (v == null) return 0.0;

            Vessel station = StationApproach.Find();
            if (station == null || station.orbit == null)
            {
                Note = "no station - launching on the standard profile";
                return 0.0;
            }

            CelestialBody b = v.mainBody;

            // ---- ⛔ RSS: LAUNCH INTO THE STATION'S PLANE (RAAN), NOT JUST ITS PHASE. ----
            if (station.orbit.inclination > PlaneMatchMinIncDeg)
            {
                double firstCrossing = SecondsToPlaneCrossing(v, station, b);
                if (firstCrossing < 0.0)
                {
                    Note = "plane window unresolved (degenerate geometry) - launching now";
                    return 0.0;
                }
                double sidereal = System.Math.Abs(b.rotationPeriod);

                // ---- PLANE ∩ PHASE: pick the crossing DAY whose target phase is also a small catch-up
                double phase0, step;
                PhaseAtCrossing(v, station, b, firstCrossing, out phase0, out step);
                double chosenWait; int chosenK; double predLead;
                PlaneWindow.PickPhasedCrossing(firstCrossing, sidereal, phase0, step,
                    AcceptLeadMinDeg, AcceptLeadMaxDeg, DesiredPhaseLeadDeg,
                    PlaneWindowMinLeadS, PhaseSearchMaxS,
                    out chosenWait, out chosenK, out predLead);

                LastRequiredLeadDeg = predLead;

                if (chosenWait > PhaseSearchMaxS + sidereal)
                {
                    Note = "no well-phased plane crossing within " + (PhaseSearchMaxS / 86400.0).ToString("F1")
                         + " days - launching now, phase will be large";
                    Debug.LogWarning(Tag + "LAUNCH WINDOW: " + Note);
                    return 0.0;
                }

                WaitS = chosenWait;
                Note = "plane+phase crossing #" + chosenK + " in " + chosenWait.ToString("F0")
                     + " s (" + (chosenWait / 86400.0).ToString("F2") + " d) - target lead "
                     + predLead.ToString("F1") + " deg (want " + DesiredPhaseLeadDeg.ToString("F0")
                     + "), station inc " + station.orbit.inclination.ToString("F2") + " deg";
                Debug.Log(Tag + "LAUNCH WINDOW: " + Note
                     + ". Plane matched (RAAN); the " + predLead.ToString("F1")
                     + " deg lead is closed by rendezvous phasing.");
                return chosenWait;
            }

            WindowInputs w = new WindowInputs();
            w.PadLonDeg = v.longitude;
            w.StationSmaM = station.orbit.semiMajorAxis;
            w.StationPeriodS = station.orbit.period;
            w.AscentTimeS = AscentTimeS;
            w.AscentLonDeg = AscentLonDeg;
            w.PhaseBiasDeg = PhaseBiasDeg;
            w.TrailDistM = TrailDistM;
            w.ParkingPeriodS = 0.0;

            double insertUt = Planetarium.GetUniversalTime() + AscentTimeS;
            Vector3d p = station.orbit.getPositionAtUT(insertUt);
            w.StationLonAtInsertionDeg = b.GetLongitude(p);
            LastRequiredLeadDeg = LaunchWindow.RequiredLead(w);

            double wait = LaunchWindow.SecondsToWindow(w, b.rotationPeriod);
            if (wait < 0.0)
            {
                Note = "the phase never closes from this pad - launching now";
                return 0.0;
            }

            if (wait > WindowCapS)
            {
                Note = "window " + (wait / 60.0).ToString("F1") + " min away, past the "
                     + (WindowCapS / 60.0).ToString("F0") + " min cap - launching now and phasing "
                     + "in orbit";
                Debug.LogWarning(Tag + "LAUNCH WINDOW: " + Note);
                return 0.0;
            }

            WaitS = wait;
            Note = "phase now " + LaunchWindow.PhaseAtLaunch(w).ToString("F2")
                 + " deg, need " + LaunchWindow.RequiredLead(w).ToString("F2")
                 + " deg -> hold " + wait.ToString("F0") + " s";
            return wait;
        }

        private static double SecondsToPlaneCrossing(Vessel v, Vessel station, CelestialBody b)
        {
            if (b == null || station.orbit == null || b.rotationPeriod == 0.0) return -1.0;

            double celLon = CelestialLongitudeDeg(v.CoM - b.position);

            double lan = station.orbit.LAN - LaunchLanBiasDeg;
            double t = PlaneWindow.TimeToPlane(b.rotationPeriod, v.latitude, celLon,
                                               lan, station.orbit.inclination);

            Debug.Log(Tag + "plane window (MechJeb TimeToPlane): pad celLon " + celLon.ToString("F1")
                + " deg, target LAN " + station.orbit.LAN.ToString("F1") + " (bias " + LaunchLanBiasDeg.ToString("F1")
                + ") inc " + station.orbit.inclination.ToString("F2") + " -> hold " + t.ToString("F0")
                + " s for the north-going plane crossing.");
            return t;
        }

        private static void PhaseAtCrossing(Vessel v, Vessel station, CelestialBody b, double firstCrossing,
                                            out double phase0Deg, out double stepDeg)
        {
            const double D2R = System.Math.PI / 180.0;
            const double R2D = 180.0 / System.Math.PI;

            double utIns0 = Planetarium.GetUniversalTime() + firstCrossing + AscentTimeS;

            double nuDeg = station.orbit.TrueAnomalyAtUT(utIns0) * R2D;
            double uStn = PlaneWindow.Norm360(station.orbit.argumentOfPeriapsis + nuDeg);

            double lat = v.latitude * D2R;
            double sinInc = System.Math.Sin(System.Math.Abs(station.orbit.inclination * D2R));
            double ratio = (sinInc > 1e-6) ? System.Math.Sin(lat) / sinInc : 0.0;
            if (ratio > 1.0) ratio = 1.0; else if (ratio < -1.0) ratio = -1.0;
            double uPad = System.Math.Asin(ratio) * R2D;
            double uIns = PlaneWindow.Norm360(uPad + AscentArcDeg);

            phase0Deg = PlaneWindow.Norm360(uStn - uIns);

            double tStn = station.orbit.period;
            stepDeg = (tStn > 1.0)
                    ? PlaneWindow.Norm360(360.0 * System.Math.Abs(b.rotationPeriod) / tStn)
                    : 0.0;

            Debug.Log(Tag + "plane+phase: pad on-plane u " + uPad.ToString("F1") + " + ascent arc "
                + AscentArcDeg.ToString("F1") + " = insertion u " + uIns.ToString("F1")
                + "; target u " + uStn.ToString("F1") + " -> phase0 " + phase0Deg.ToString("F1")
                + " deg, step " + stepDeg.ToString("F1") + " deg/crossing.");
        }

        private static double CelestialLongitudeDeg(Vector3d orbitalPosition)
        {
            return AngleInPlaneDeg(Planetarium.right, -Planetarium.up, orbitalPosition);
        }

        private static double AngleInPlaneDeg(Vector3d vector, Vector3d planeNormal, Vector3d other)
        {
            Vector3d v1 = Vector3d.Exclude(planeNormal, vector);
            Vector3d v2 = Vector3d.Exclude(planeNormal, other);
            if (v1.magnitude == 0.0 || v2.magnitude == 0.0) return 0.0;
            double angle = Vector3d.Angle(v1, v2);
            if (Vector3d.Dot(Vector3d.Cross(v1, v2), planeNormal) < 0.0) angle = -angle;
            return angle;
        }

        public static void MeasureAtInsertion(Vessel v, double liftoffUt, double liftoffLonDeg)
        {
            if (v == null || liftoffUt <= 0.0) return;
            double flown = Planetarium.GetUniversalTime() - liftoffUt;
            double gained = v.longitude - liftoffLonDeg;
            while (gained < -180.0) gained += 360.0;
            while (gained > 180.0) gained -= 360.0;

            Debug.Log(Tag + "LAUNCH WINDOW CALIBRATION - this ascent flew " + flown.ToString("F1")
                      + " s and gained " + gained.ToString("F2") + " deg of longitude. Seeds in use: "
                      + AscentTimeS.ToString("F1") + " s / " + AscentLonDeg.ToString("F2")
                      + " deg. If these differ by more than a few percent, update "
                      + "LaunchWindowOps.AscentTimeS and .AscentLonDeg - F9I: \"the ascent changed "
                      + "twice and this number did not follow it\", and that is what makes the "
                      + "window drift.");

            AscentTimeS = flown;
            AscentLonDeg = gained;

            // ---- COMMANDED LEAD vs. TRAIL ACTUALLY DELIVERED, on one line. ----
            Vessel station = StationApproach.Find();
            if (station != null && station.orbit != null)
            {
                double trailDeg = station.longitude - v.longitude;
                while (trailDeg < -180.0) trailDeg += 360.0;
                while (trailDeg > 180.0) trailDeg -= 360.0;
                double trailKm = trailDeg * System.Math.PI / 180.0 * station.orbit.semiMajorAxis / 1000.0;
                Debug.Log(Tag + "INSERTION TRAIL: station is " + trailDeg.ToString("F2") + " deg / "
                          + trailKm.ToString("F1") + " km ahead of us (positive = we are BEHIND it). "
                          + "Window commanded lead " + LastRequiredLeadDeg.ToString("F2")
                          + " deg; delivered-minus-commanded = "
                          + (trailDeg - LastRequiredLeadDeg).ToString("F2") + " deg. If this residual "
                          + "repeats across flights, set PhaseBiasDeg to its NEGATIVE to cancel it.");

                // ---- GROUND-TRUTH PLANE CHECK. This is the number that says whether the launch made an X.
                if (v.orbit != null)
                {
                    double i1 = v.orbit.inclination * System.Math.PI / 180.0;
                    double i2 = station.orbit.inclination * System.Math.PI / 180.0;
                    double dLan = (v.orbit.LAN - station.orbit.LAN) * System.Math.PI / 180.0;
                    double cosRel = System.Math.Cos(i1) * System.Math.Cos(i2)
                                  + System.Math.Sin(i1) * System.Math.Sin(i2) * System.Math.Cos(dLan);
                    cosRel = System.Math.Max(-1.0, System.Math.Min(1.0, cosRel));
                    double relIncDeg = System.Math.Acos(cosRel) * 57.29578;
                    double dLanDeg = v.orbit.LAN - station.orbit.LAN;
                    while (dLanDeg < -180.0) dLanDeg += 360.0;
                    while (dLanDeg > 180.0) dLanDeg -= 360.0;
                    Debug.Log(Tag + "INSERTION PLANE: rel-inc to station " + relIncDeg.ToString("F2")
                              + " deg (want ~0 - this IS the X check). Our LAN " + v.orbit.LAN.ToString("F1")
                              + " vs station " + station.orbit.LAN.ToString("F1") + " (dLAN " + dLanDeg.ToString("F1")
                              + "), inc " + v.orbit.inclination.ToString("F2") + " vs "
                              + station.orbit.inclination.ToString("F2") + ". If dLAN repeats, add it to LaunchLanBiasDeg.");
                    if (relIncDeg > 3.0)
                        Debug.LogWarning(Tag + "INSERTION PLANE: " + relIncDeg.ToString("F1")
                            + " deg off the station plane - a rendezvous from here needs an expensive plane change.");

                    // ---- INSERTION PHASE (the along-track half of the launch window). ----
                    double r2d = 180.0 / System.Math.PI;
                    double uStnNow = PlaneWindow.Norm360(
                        station.orbit.argumentOfPeriapsis + station.orbit.trueAnomaly * r2d);
                    double uChaserNow = PlaneWindow.Norm360(
                        v.orbit.argumentOfPeriapsis + v.orbit.trueAnomaly * r2d);
                    double deliveredLead = PlaneWindow.Norm360(uStnNow - uChaserNow);
                    double residDeg = PlaneWindow.Wrap180(deliveredLead - LastRequiredLeadDeg);
                    Debug.Log(Tag + "INSERTION PHASE: target lead (arg-of-lat) " + deliveredLead.ToString("F1")
                        + " deg; window predicted " + LastRequiredLeadDeg.ToString("F1") + " deg; residual "
                        + residDeg.ToString("F1") + " deg (want the delivered lead near "
                        + DesiredPhaseLeadDeg.ToString("F0") + "). If this residual repeats, subtract it "
                        + "from LaunchWindowOps.AscentArcDeg.");
                }
            }
        }
    }
}
