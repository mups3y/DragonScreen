/*
 * DragonScreen - LaunchWindowOps
 *
 * GLUE. Holds the countdown until the pad has rotated to the right PHASE ANGLE behind the station.
 * Law in `pure/LaunchWindow.cs`. Ported from `F9I/station_ops.ks:311 StLaunchPhaseWait`.
 *
 * ---- ⛔ THE LAW WAS PORTED AND NOTHING EVER CALLED IT. ----
 * `pure/LaunchWindow.cs` has been in the tree, tested and documented as PORTED, with **not one
 * reference from any glue file**. Section 1 of the mission was written and unreachable. That is the
 * same defect as the refuel: a row in the port map saying DONE, and nothing on the other end of it.
 *
 * ---- WHAT A BAD WINDOW COSTS ----
 * F9I: on the first ferry, arriving at the wrong phase meant the rendezvous *"spent 7.3 HOURS phasing
 * (met 370 -> 26695) for only 39 LF"*. The propellant is not the problem - the time is, and so is
 * every orbit of drift the docking then has to absorb.
 *
 * ---- ⚠ TWO OF THE FOUR INPUTS ARE MEASUREMENTS, NOT SETTINGS ----
 * `AscentTimeS` and `PhaseBiasDeg` describe how OUR ascent actually flies, and F9I is emphatic that
 * reading them from a constant is what makes the window drift: *"the ascent changed twice and this
 * number did not follow it."* So this measures the ascent it just flew and says what the next launch
 * should use. The seeds below are F9I's, which is the right starting point and the wrong finishing
 * one - fly it, read the log line, and the numbers become ours.
 */
using System;
using UnityEngine;

namespace DragonScreen
{
    public static class LaunchWindowOps
    {
        private const string Tag = "[DragonScreen] ";

        // ---- F9I's SEEDS. station_ops.ks:72-73, 237, 277. Replace with our own measurements. ----
        /// <summary>Liftoff to insertion, seconds. `stAscentTime`.</summary>
        // ⛔ MEASURED, NOT SEEDED. The calibration line asked for these to be updated on FOUR
        // consecutive flights and nobody did it - 2026-08-11 16:03 flew 281.8 s / 16.29 deg,
        // 2026-08-12 09:32 flew 281.5 s / 16.24. The seeds said 315.0 / 22.80, which is 12% long in
        // time and 40% wrong in longitude, and the window is computed FROM these - so every hold was
        // released at the wrong moment. F9I's warning is exactly this case: "the ascent changed twice
        // and this number did not follow it".
        //
        // Set from the mean of the two clean flights. Re-measure whenever the ascent changes.
        // Re-measured 2026-08-12 after the S2 took over the insertion: the ascent now runs
        // ~3 s longer and 0.5 deg further east, because the circularisation happens on the
        // MVac before separation instead of on the Dracos after it.
        // ⛔ 284.7 -> 519.5 ON 2026-08-17. The 284.7 s seed was measured against an OLD profile. The
        // S2 now performs the insertion and separates AFTER circularising, so the real liftoff-to-
        // co-orbital time is 519.5 s - MEASURED, flight_0817_114554: liftoff met 848.0, circular at
        // station altitude met 1367.5. The 235 s stale seed timed the launch that much too early and
        // the capsule inserted 16 km behind the station instead of the 1 km target, forcing a phasing
        // lap. Tunable, and MeasureAtInsertion re-fits it live each ascent (once the liftoff clock is
        // stamped once, not re-stamped on the recovery handback - see AutoPilot.Engage).
        // ⛔ 519.5 -> 528.5 ON 2026-08-22. The 519.5 was the MechJeb ascent. UPFG (the real ascent now)
        // flew 528.5 s liftoff-to-insertion, MEASURED flight_0822_105240 ("this ascent flew 528.5 s").
        [Tunable] public static double AscentTimeS = 528.5;
        /// <summary>
        /// Longitude the ascent gains, degrees. `stAscentLng`.
        ///
        /// ⛔ 16.76 -> 56.61 ON 2026-08-17. Paired with the AscentTimeS fix and just as stale: the
        /// 16.76 seed was the old 285 s ascent. The 519.5 s ascent sweeps 56.61 deg of longitude
        /// (MEASURED, flight_0817_173..: "gained 56.61 deg"), and the launch window subtracts this
        /// from the station's own travel to place the insertion - so a 40 deg error here put the
        /// capsule 486 km AHEAD of the station instead of 1 km behind, and no rendezvous starts well
        /// from there. Tunable, and MeasureAtInsertion re-fits it live.
        ///
        /// ⛔ 56.61 -> 12.98 ON 2026-08-22. THE BIG ONE. 56.61 was the MechJeb ascent, which flew a long
        /// shallow trajectory halfway round the planet. UPFG (the real ascent now, ullage-Init bug fixed)
        /// flies a far more DIRECT insertion and sweeps only 12.98 deg of longitude - MEASURED
        /// flight_0822_105240 ("gained 12.98 deg"). The 43.6 deg seed error placed the launch so the
        /// capsule inserted 65.8 deg / 7798 km BEHIND the station (delivered-minus-commanded +58.53 deg).
        /// This is the single biggest cause of "we do not launch to rendezvous." Re-measure PhaseBiasDeg
        /// from the INSERTION TRAIL residual on the NEXT flight (it will read far smaller now, ~15 deg).
        /// </summary>
        [Tunable] public static double AscentLonDeg = 12.98;
        /// <summary>
        /// Correction the last arrival measured, degrees. `stPhaseBias`.
        ///
        /// ⛔ 4.418 -> 0.0 ON 2026-08-18. This is the reason the parking orbit was NOT within 1 km.
        /// `RequiredLead = TrailDistM_angle + PhaseBiasDeg`, and 4.418 deg is ~47 km - it SWAMPED the
        /// 1 km TrailDistM (0.08 deg) and made the window aim ~47 km behind the station. An 0818
        /// parking-orbit test measured it: commanded 4.50 deg lead, inserted 45.6 km behind - the
        /// window HITS what it is
        /// told (~1.4 km scatter), so the 4.418 seed was simply telling it to trail 47 km. The gap then
        /// forced a phasing lap before the direct-approach gate (10 km) could even engage. Zeroed so
        /// the trail is TrailDistM alone. It is F9I's seed and was never our measurement - unlike
        /// AscentTimeS/AscentLonDeg, MeasureAtInsertion does NOT re-fit it, so it sat stale. If a future
        /// arrival is consistently off in the SAME direction, that residual is what this should hold.
        ///
        /// ⛔ 0.0 -> 7.25 ON 2026-08-21. Two flights on the current profile (flight_0821_013601 and the
        /// following calibration flight) BOTH inserted ~89 km AHEAD of the station when the window
        /// commanded a 2 km trail - the new `INSERTION TRAIL` line measured delivered-minus-commanded =
        /// -7.25 deg, and the ascent seeds were accurate (524 s / 56.41 deg), so this is a repeatable
        /// release-phase slip, not the ascent. +7.25 is the negative of that residual: it tells the
        /// window to aim 7.25 deg further BEHIND, so after the slip we arrive on target. Re-fit from the
        /// `INSERTION TRAIL` residual whenever it stops reading near zero.
        ///
        /// ⛔ 7.25 -> 1.84 ON 2026-08-22. The 7.25 was the MechJeb-ascent slip. With the UPFG ascent AND
        /// the corrected AscentLonDeg (12.98), flight_0822_112918 delivered 5.42 deg behind against a
        /// commanded 7.26 lead: residual -1.84 deg. PhaseBias is the NEGATIVE of the intrinsic residual
        /// (which is invariant to PhaseBias - it only shifts both command and delivery together), so
        /// -(-1.84) = 1.84 lands the capsule on TrailDistM. NOTE: one flight - confirm the residual reads
        /// ~0 next launch; if not, PhaseBias = old - newResidual.
        /// </summary>
        [Tunable] public static double PhaseBiasDeg = 1.84;
        /// <summary>
        /// How far behind the station to settle, metres. `stTrailDist`. Tunable - lower it for a closer
        /// arrival, but stay inside the direct-approach gate (10 km) so no phasing lap is needed.
        ///
        /// ⛔ 1000 -> 2000 ON 2026-08-18. The user wants the parking orbit within ~1 km. With
        /// PhaseBiasDeg zeroed the arrival is TrailDistM +/- the measured ~1.4 km scatter, so a literal
        /// 1000 could land the capsule slightly AHEAD of the station - awkward, near the 200 m keep-out.
        /// 2000 keeps it safely BEHIND (0.6-3.4 km on the measured scatter), still far inside the 10 km
        /// gate so the direct approach engages immediately with NO phasing lap.
        ///
        /// ⛔ 2000 -> 1000 ON 2026-08-21, paired with the +7.25 PhaseBiasDeg fix. Now that the 89 km
        /// systematic is cancelled, 1000 is the crew's stated 1 km target. If the `INSERTION TRAIL`
        /// residual still carries scatter that risks arriving AHEAD of the station (near the 200 m
        /// keep-out), raise this back toward 2000 - the direct approach handles an arrival either side,
        /// but behind is tidier. Tunable live in PluginData/tuning.cfg.
        /// </summary>
        [Tunable] public static double TrailDistM = 1000.0;

        /// <summary>
        /// Longest hold worth taking, seconds. `stWindowCap`.
        ///
        /// ⛔ 1800 -> 7200 ON 2026-08-13. THIRTY MINUTES WAS NOT A CAP, IT WAS THE FAILURE MODE.
        /// The station's period at 120 km is about 34 minutes, so a cap of 30 could not even cover
        /// ONE lap: any window more than half an orbit out was abandoned and the flight launched
        /// into whatever phase it happened to have. Measured on 2026-08-13 - "window 34.1 min away,
        /// past the 30 min cap - launching now and phasing in orbit" - and the phasing that was
        /// supposed to recover it then could not, because the gap was too large to close.
        ///
        /// Waiting is FREE. Phasing is not: it costs propellant, laps and the risk of not closing
        /// at all. Two hours covers roughly three and a half laps, so a window is essentially
        /// always taken rather than traded for a phase correction the vehicle may not be able to
        /// afford. If the wait is genuinely longer than this, the phase does not close from this
        /// pad on this orbit and launching is the honest answer.
        /// </summary>
        public const double WindowCapS = 7200.0;

        /// <summary>Above this station inclination the PLANE window is used (RAAN match) instead of the
        /// phase window. Stock Kerbin's ~0.13 deg station stays on phase; the ISS at 51.6 deg uses plane.</summary>
        public const double PlaneMatchMinIncDeg = 5.0;

        /// <summary>Cap for the PLANE window, seconds. Raised to ~4.5 days so the min-lead window (below,
        /// held ~2 days out to give the crew time to warp) is never abandoned as "too far".</summary>
        [Tunable] public static double PlaneWindowCapS = 400000.0;

        /// <summary>
        /// Minimum lead to the launch window, seconds. User 2026-08-23: "give us two days to warp to launch
        /// so we do not miss the window." The north-going plane crossing recurs once per sidereal day, so
        /// the soonest is under 24 h - too soon to set the mission up and easy to miss. The window is pushed
        /// forward whole sidereal days until it is at least this far ahead: identical on-plane geometry,
        /// with a comfortable ~2-day warp to reach it. Set 0 to launch on the next crossing.
        /// </summary>
        [Tunable] public static double PlaneWindowMinLeadS = 172800.0;   // 2 days

        /// <summary>
        /// Degrees SUBTRACTED from the target LAN before solving the plane window (MechJeb's
        /// LaunchLANDifference). The orbit's LAN drifts a little between liftoff and insertion as the
        /// ascent flies its gravity turn, so aiming a touch early lands the INSERTION LAN on the target.
        /// Start at 0 and tune from the `INSERTION PLANE` rel-inc line MeasureAtInsertion logs: if every
        /// flight inserts consistently a few degrees to one side, set this to that residual.
        /// </summary>
        [Tunable] public static double LaunchLanBiasDeg = 0.0;

        /// <summary>Seconds still to wait, or 0 when the window is open. Negative = no station.</summary>
        public static double WaitS = -1.0;
        public static string Note = "-";

        /// <summary>The lead the window last COMMANDED, degrees - logged against the trail actually
        /// delivered at insertion so the two can be compared on one line. flight_0821 aimed 0.16 deg
        /// (2 km) and delivered 89 km, and the seeds were accurate, so the slip is in the release phase,
        /// not the ascent - this is how we see it directly.</summary>
        public static double LastRequiredLeadDeg;

        /// <summary>
        /// How long to hold. Zero means go now - including when there is no station to go to, which
        /// is a perfectly ordinary launch and must not be turned into a refusal.
        /// </summary>
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
            // Stock Kerbin's station sits at 0.133 deg, where the plane is degenerate and the along-track
            // phase is the whole story - that is what the phase window below solves. The ISS is at 51.6
            // deg: you only fly INTO its plane at the instant the rotating pad crosses it, and matching
            // only the inclination (the ascent's job) leaves the RAAN off by tens of degrees, which no
            // phasing can fix. So above a real inclination, WAIT FOR THE PLANE CROSSING; the rendezvous
            // then closes the phase by phasing up from the low insertion orbit. See pure/PlaneWindow.cs.
            if (station.orbit.inclination > PlaneMatchMinIncDeg)
            {
                double planeWait = SecondsToPlaneCrossing(v, station, b);
                if (planeWait < 0.0)
                {
                    Note = "plane window unresolved (degenerate geometry) - launching now";
                    return 0.0;
                }
                // ---- HOLD ~2 DAYS OUT SO THE CREW CAN WARP TO IT (see PlaneWindowMinLeadS). ----
                // The soonest north-going crossing is < 1 sidereal day; push forward whole days until the
                // window is at least the min lead ahead. Same on-plane geometry, a comfortable warp to it.
                double sidereal = System.Math.Abs(b.rotationPeriod);
                if (sidereal > 1.0 && PlaneWindowMinLeadS > 0.0)
                    while (planeWait < PlaneWindowMinLeadS) planeWait += sidereal;
                if (planeWait > PlaneWindowCapS)
                {
                    Note = "plane crossing " + (planeWait / 3600.0).ToString("F1") + " h away, past the "
                         + (PlaneWindowCapS / 3600.0).ToString("F0") + " h cap - launching now, plane will be off";
                    Debug.LogWarning(Tag + "LAUNCH WINDOW: " + Note);
                    return 0.0;
                }
                WaitS = planeWait;
                Note = "plane crossing (RAAN match) in " + planeWait.ToString("F0") + " s - station inc "
                     + station.orbit.inclination.ToString("F2") + " deg";
                Debug.Log(Tag + "LAUNCH WINDOW: " + Note + ". Phase closed by rendezvous phasing.");
                return planeWait;
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

            // ⚠ WHERE THE STATION WILL BE AT *OUR INSERTION*, not where it is now. Our insertion
            // longitude is fixed the moment we light - the pad never moves and the ascent always gains
            // the same measured arc - so the whole question is where the station has got to by then.
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

        /// <summary>
        /// Seconds until the pad rotates into the target's orbital PLANE, launching NORTH-going - the
        /// RAAN-matching launch window. Uses MechJeb's proven Astro.TimeToPlane (pure/PlaneWindow.cs),
        /// which works in SCALARS in KSP's celestial frame - the pad's celestial longitude and the
        /// target's own orbit.LAN / orbit.inclination - so there is nothing to mis-swizzle.
        ///
        /// ⛔ THIS REPLACED a vector-normal crossing search that tracked the station's POSITION, not its
        /// plane: the ISS plane is LAN 0 but our launches came out LAN ~107 = the station's argument of
        /// latitude at engage (the user's "X"). The stock 0.13 deg station hid it; a 51.6 deg orbit did
        /// not. We launch to the PLANE regardless of where the station is; the rendezvous closes phase.
        /// </summary>
        private static double SecondsToPlaneCrossing(Vessel v, Vessel station, CelestialBody b)
        {
            if (b == null || station.orbit == null || b.rotationPeriod == 0.0) return -1.0;

            // Pad's celestial longitude, exactly as MechJeb's VesselState computes it:
            //   Planetarium.right.AngleInPlane(-Planetarium.up, orbitalPosition)
            // It lives in the SAME celestial frame as orbit.LAN / orbit.inclination below.
            double celLon = CelestialLongitudeDeg(v.CoM - b.position);

            // +inclination = NORTH-GOING. Our ascent always flies the ascending (north-east) azimuth, so
            // we always want the north-going crossing - never MinimumTimeToPlane's south-going option.
            double lan = station.orbit.LAN - LaunchLanBiasDeg;
            double t = PlaneWindow.TimeToPlane(b.rotationPeriod, v.latitude, celLon,
                                               lan, station.orbit.inclination);

            Debug.Log(Tag + "plane window (MechJeb TimeToPlane): pad celLon " + celLon.ToString("F1")
                + " deg, target LAN " + station.orbit.LAN.ToString("F1") + " (bias " + LaunchLanBiasDeg.ToString("F1")
                + ") inc " + station.orbit.inclination.ToString("F2") + " -> hold " + t.ToString("F0")
                + " s for the north-going plane crossing.");
            return t;
        }

        /// <summary>
        /// The pad's celestial (inertial) longitude in degrees, in KSP's celestial reference frame - the
        /// frame `orbit.LAN` is measured in. Verbatim from MechJeb VesselState.CelestialLongitude: the
        /// angle of the pad position, projected into the equatorial plane, from Planetarium.right.
        /// </summary>
        private static double CelestialLongitudeDeg(Vector3d orbitalPosition)
        {
            return AngleInPlaneDeg(Planetarium.right, -Planetarium.up, orbitalPosition);
        }

        /// <summary>
        /// Signed angle (degrees) from <paramref name="vector"/> to <paramref name="other"/>, both
        /// projected onto the plane whose normal is <paramref name="planeNormal"/>. Port of MechJeb's
        /// Vector3d.AngleInPlane (MathExtensions.cs). Sign follows the plane normal.
        /// </summary>
        private static double AngleInPlaneDeg(Vector3d vector, Vector3d planeNormal, Vector3d other)
        {
            Vector3d v1 = Vector3d.Exclude(planeNormal, vector);
            Vector3d v2 = Vector3d.Exclude(planeNormal, other);
            if (v1.magnitude == 0.0 || v2.magnitude == 0.0) return 0.0;
            double angle = Vector3d.Angle(v1, v2);                       // 0..180 deg
            if (Vector3d.Dot(Vector3d.Cross(v1, v2), planeNormal) < 0.0) angle = -angle;
            return angle;
        }

        /// <summary>
        /// Measure what this ascent actually did, and say what the next one should use.
        ///
        /// ⛔ THIS IS THE HALF THAT KEEPS THE WINDOW HONEST. F9I reads both numbers back from disk
        /// every launch for exactly this reason. We do not have a settings file yet, so this reports
        /// them rather than storing them - which is one manual step, and infinitely better than a
        /// constant that silently describes an ascent we no longer fly.
        /// </summary>
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
            // The seeds above are accurate (flight_0821 flew 525 s / 56.7 deg against 519.5 / 56.61),
            // so a big trail error is NOT the ascent - it is the release phase. This makes that visible:
            // what lead did the window command, and what trail did we actually get? A repeatable
            // delivered-minus-commanded residual is exactly what PhaseBiasDeg exists to cancel.
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
                // Relative inclination from the two orbits' own inc/LAN (KSP scalars): 0 = coplanar (no X),
                // tens of degrees = the X the user kept seeing. If this stays non-zero in the SAME
                // direction, LAN drifts during ascent - feed it back into LaunchLanBiasDeg.
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
                }
            }
        }
    }
}
