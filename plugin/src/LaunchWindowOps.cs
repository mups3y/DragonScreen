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
        [Tunable] public static double AscentTimeS = 519.5;
        /// <summary>
        /// Longitude the ascent gains, degrees. `stAscentLng`.
        ///
        /// ⛔ 16.76 -> 56.61 ON 2026-08-17. Paired with the AscentTimeS fix and just as stale: the
        /// 16.76 seed was the old 285 s ascent. The 519.5 s ascent sweeps 56.61 deg of longitude
        /// (MEASURED, flight_0817_173..: "gained 56.61 deg"), and the launch window subtracts this
        /// from the station's own travel to place the insertion - so a 40 deg error here put the
        /// capsule 486 km AHEAD of the station instead of 1 km behind, and no rendezvous starts well
        /// from there. Tunable, and MeasureAtInsertion re-fits it live.
        /// </summary>
        [Tunable] public static double AscentLonDeg = 56.61;
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
        /// </summary>
        [Tunable] public static double PhaseBiasDeg = 0.0;
        /// <summary>
        /// How far behind the station to settle, metres. `stTrailDist`. Tunable - lower it for a closer
        /// arrival, but stay inside the direct-approach gate (10 km) so no phasing lap is needed.
        ///
        /// ⛔ 1000 -> 2000 ON 2026-08-18. The user wants the parking orbit within ~1 km. With
        /// PhaseBiasDeg zeroed the arrival is TrailDistM +/- the measured ~1.4 km scatter, so a literal
        /// 1000 could land the capsule slightly AHEAD of the station - awkward, near the 200 m keep-out.
        /// 2000 keeps it safely BEHIND (0.6-3.4 km on the measured scatter), still far inside the 10 km
        /// gate so the direct approach engages immediately with NO phasing lap. Lower toward 1000 once a
        /// couple of arrivals confirm the scatter.
        /// </summary>
        [Tunable] public static double TrailDistM = 2000.0;

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

        /// <summary>Seconds still to wait, or 0 when the window is open. Negative = no station.</summary>
        public static double WaitS = -1.0;
        public static string Note = "-";

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
        }
    }
}
