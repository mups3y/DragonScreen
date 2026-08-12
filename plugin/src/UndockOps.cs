/*
 * DragonScreen - UndockOps
 *
 * GLUE. Top up from the station, close the shroud, let go of OUR port, and back away far enough to
 * burn. Ported from `F9I/station_ops.ks` - `StTopUpBeforeUndock:2666`, `StCloseDockingShroud:2291`,
 * `StUndock:2303`, `StBackAway:2338`.
 *
 * ---- ⛔ THREE TRAPS, ALL OF THEM FLIGHTS ----
 *
 * 1. UNDOCK OUR OWN PORT, NOT THE FIRST ONE FOUND. When docked, KSP merges everything into ONE
 *    vessel, so a walk over `vessel.parts` is the whole station - INCLUDING any other Dragon berthed
 *    on it. First-match would happily release somebody else's capsule, with crew in it, and the part
 *    order is not ours to predict. Ours is the docked node NEAREST our own control part: it is bolted
 *    to us, another Dragon's is tens of metres away across the station.
 *
 * 2. WHICH WAY IS "AWAY"? MEASURE IT. `fore` follows the CONTROL PART's facing, and the Dragon's
 *    control-point orientation is exactly the kind of thing that reads plausible and flies backwards
 *    on this project - the Starlink control-point=Down fix is the same bug. Push briefly, see whether
 *    the range opened or closed, keep the sign that opened it. F9I: "Getting this wrong drives the
 *    capsule INTO the station, so it is calibrated, never assumed."
 *
 * 3. BURST, THEN COAST. Holding the thruster on for the whole separation cost flight 031 "19 seconds
 *    of continuous RCS for 14.8 UNITS of monopropellant, to move 150 m". In orbit nothing slows you
 *    down - once you are moving away you keep moving away for free, so all the burn has to buy is the
 *    RATE. 14.8 units matters: the whole return budget is about 140, and 031's entry ran to zero.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public enum UndockStage : byte
    {
        Idle = 0,
        /// <summary>Holding the release until the station's transfers stop making progress.</summary>
        ToppingUp,
        Releasing,
        /// <summary>Two seconds of push to find out which way `fore` actually goes.</summary>
        CalibratingSign,
        /// <summary>Buying the separation RATE. Short.</summary>
        Burst,
        /// <summary>Free. No thrust at all from here.</summary>
        Coasting,
        Clear,
        Failed
    }

    public static class UndockOps
    {
        private const string Tag = "[DragonScreen] ";

        // ---- F9I's CONSTANTS. station_ops.ks:81-87, 705-706. ----
        /// <summary>Back away this far before any burn, metres. `stSafeDist`.</summary>
        public const double SafeDistM = 150.0;
        /// <summary>Separation rate the burst buys, m/s. `stBackRate`.</summary>
        public const double BackRateMps = 1.5;
        /// <summary>Longest the burst may run, seconds. `stBackBurstMax`.</summary>
        public const double BurstMaxS = 6.0;
        /// <summary>Give up backing away after this long, seconds. `stBackMax`.</summary>
        public const double BackMaxS = 180.0;
        /// <summary>Seconds of push used to calibrate the sign.</summary>
        public const double SignTestS = 2.0;
        /// <summary>Never hold the undock longer than this, seconds. `stTopUpMax`.</summary>
        public const double TopUpMaxS = 120.0;
        /// <summary>...or this long with the gauge not moving. `stTopUpFlat`.</summary>
        public const double TopUpFlatS = 6.0;

        public static UndockStage Stage { get; private set; }
        public static string Note = "-";
        public static double SeparationM, OpeningMps;

        private static Vessel ship, station;
        private static double stageStartedAt, lastProgressAt, lastMono, lastTopUpAt, d0;
        private static double foreSign = -1.0;

        public static bool Engaged { get; private set; }

        public static void Engage(Vessel v, Vessel target)
        {
            if (v == null) return;
            ship = v; station = target;
            Engaged = true;
            Stage = UndockStage.ToppingUp;
            stageStartedAt = Planetarium.GetUniversalTime();
            lastProgressAt = stageStartedAt;
            lastMono = Mono(v);
            lastTopUpAt = stageStartedAt;
            Refuel.Begin();
            foreSign = -1.0;
            Debug.Log(Tag + "undock sequence started - topping up first");
        }

        public static void Reset()
        {
            Engaged = false; Stage = UndockStage.Idle; Note = "-";
            ship = null; station = null; SeparationM = 0.0; OpeningMps = 0.0;
        }

        private static void Go(UndockStage s)
        {
            // ---- ⛔ RELEASE THE TARGET WHEN THE STATION STOPS BEING ONE. ----
            // Docking targets the PORT so the DOCKING page reads port-relative state. Once the
            // capsule is clear that target is worse than none: every target-relative readout on
            // every page - range, closing rate, the navball marker - keeps pointing at a station
            // the vehicle is leaving, and the de-orbit that follows is flown with the approach's
            // furniture still on the glass. The trip is over; the crew should see their own orbit.
            //
            // Here rather than at the three call sites that reach `Clear`, so a fourth cannot
            // forget. Refuel completed in `ToppingUp`, long before this.
            if (s == UndockStage.Clear || s == UndockStage.Failed)
                DockingOps.SetTarget(null, "undock complete - clear of the station");

            Stage = s;
            stageStartedAt = Planetarium.GetUniversalTime();
        }

        // ------------------------------------------------------------------ the loop

        public static void Tick()
        {
            if (!Engaged || ship == null || ship.state == Vessel.State.DEAD) return;
            double now = Planetarium.GetUniversalTime();
            double inStage = now - stageStartedAt;

            switch (Stage)
            {
                case UndockStage.ToppingUp: TopUp(now, inStage); break;
                case UndockStage.Releasing: Release(); break;
                case UndockStage.CalibratingSign: Calibrate(inStage); break;
                case UndockStage.Burst: Burst(inStage); break;
                case UndockStage.Coasting: Coast(inStage); break;
            }
        }

        /// <summary>
        /// Hold the release while the station is still transferring propellant into us.
        ///
        /// "Progress" rather than "full": the source is whatever the station has, and waiting for a
        /// full tank on a station that cannot fill it is a hold that never ends. Stop when the gauge
        /// stops moving.
        /// </summary>
        private static void TopUp(double now, double inStage)
        {
            // ---- ⛔ AND SOMETHING HAS TO ACTUALLY MOVE THE PROPELLANT. ----
            // This used only to WATCH a number. Nothing in the plugin transferred anything, so the
            // refuel - the entire reason for the station trip - had never once happened, and the
            // return budget could never close. `Refuel.Tick` yields to any other authority, so if the
            // station is feeding us this finds no deficit and does nothing.
            Refuel.Tick(ship, now - lastTopUpAt);
            lastTopUpAt = now;

            // ⚠ THE CAPSULE'S TANK, NOT THE MERGED VESSEL'S. See DockedSide - reading the pair is
            // what has F9I announcing "the fuel tanks are not full" over a brimming Dragon, and it is
            // also why a progress test against the merged total can never see progress at all.
            double mono = Mono(ship);
            if (mono > lastMono + 0.01) { lastMono = mono; lastProgressAt = now; }

            double flat = now - lastProgressAt;
            bool full = Refuel.Full(ship);
            Note = "TOPPING UP - " + mono.ToString("F1") + " units ("
                 + (Refuel.Fraction(ship) * 100.0).ToString("F0") + "%), "
                 + (full ? "full" : (flat > 1.0 ? "flat for " + flat.ToString("F0") + " s" : "filling"));

            // Full is a reason to stop; so is the gauge going quiet, because a station that cannot
            // fill us is not a station to keep waiting on.
            if (full || inStage > TopUpMaxS || flat > TopUpFlatS)
            {
                Debug.Log(Tag + "refuel finished after " + inStage.ToString("F0") + " s - "
                          + Refuel.Report(ship));
                if (!full)
                    Debug.LogWarning(Tag + "⚠ UNDOCKING WITHOUT A FULL TANK - the return budget is "
                                     + "sized on leaving the berth full. Check the de-orbit margin "
                                     + "before committing.");
                CloseShroud();
                Go(UndockStage.Releasing);
            }
        }

        /// <summary>Close the docking shroud before re-entry - it is not built to fly through air.</summary>
        private static void CloseShroud()
        {
            int n = 0;
            for (int i = 0; i < ship.parts.Count; i++)
            {
                Part p = ship.parts[i];
                for (int m = 0; m < p.Modules.Count; m++)
                {
                    PartModule pm = p.Modules[m];
                    for (int e = 0; e < pm.Events.Count; e++)
                    {
                        BaseEvent ev = pm.Events[e];
                        if (ev == null || ev.guiName == null) continue;
                        string g = ev.guiName.ToLowerInvariant();
                        if (g.Contains("close shroud") || g.Contains("close docking hatch"))
                        {
                            ev.Invoke(); n++;
                        }
                    }
                }
            }
            if (n > 0) Debug.Log(Tag + "docking shroud closed for re-entry");
            else Debug.LogWarning(Tag + "no 'close shroud' event found - it may still be open");
        }

        /// <summary>
        /// Release OUR port. See trap 1 - the nearest docked node to our own control part is ours by
        /// construction, because it is bolted to us.
        /// </summary>
        private static void Release()
        {
            Vector3d me = ship.ReferenceTransform.position;
            ModuleDockingNode best = null;
            double bestD = double.MaxValue;

            for (int i = 0; i < ship.parts.Count; i++)
            {
                List<ModuleDockingNode> ns = ship.parts[i].Modules.GetModules<ModuleDockingNode>();
                for (int m = 0; m < ns.Count; m++)
                {
                    if (ns[m].otherNode == null) continue;              // not a docked node
                    double d = Vector3d.Distance(ns[m].part.transform.position, me);
                    if (d < bestD) { bestD = d; best = ns[m]; }
                }
            }

            if (best == null)
            {
                Note = "no docked port found to release";
                Stage = UndockStage.Failed;
                Debug.LogError(Tag + "UNDOCK FAILED - " + Note);
                return;
            }

            Debug.Log(Tag + "undocking '" + best.part.partInfo.title + "', "
                      + bestD.ToString("F1") + " m from the control point");
            best.Undock();
            d0 = -1.0;
            Go(UndockStage.CalibratingSign);
        }

        /// <summary>
        /// Trap 2: push for two seconds and keep whichever sign opened the range.
        ///
        /// Not a style choice. `fore` follows the control part's facing, and getting it wrong drives
        /// the capsule into the station.
        /// </summary>
        private static void Calibrate(double inStage)
        {
            if (station == null) { Go(UndockStage.Burst); return; }
            double d = Vector3d.Distance(ship.CoM, station.CoM);
            SeparationM = d;

            if (d0 < 0.0) { d0 = d; foreSign = -1.0; }
            Push(foreSign);
            Note = "CHECKING WHICH WAY IS OUT - " + d.ToString("F1") + " m";

            if (inStage >= SignTestS)
            {
                if (d < d0)
                {
                    foreSign = 1.0;
                    Debug.LogWarning(Tag + "back-away sign FLIPPED - fore=-1 was CLOSING ("
                                     + d0.ToString("F1") + " -> " + d.ToString("F1") + " m)");
                }
                else
                {
                    Debug.Log(Tag + "back-away sign confirmed at fore=-1 ("
                              + d0.ToString("F1") + " -> " + d.ToString("F1") + " m)");
                }
                Go(UndockStage.Burst);
            }
        }

        /// <summary>Trap 3: buy the RATE and stop. The coast is free and it is most of the distance.</summary>
        private static void Burst(double inStage)
        {
            if (station == null) { Push(0.0); Go(UndockStage.Clear); return; }
            SeparationM = Vector3d.Distance(ship.CoM, station.CoM);
            Vector3d away = (ship.CoM - station.CoM).normalized;
            OpeningMps = Vector3d.Dot(ship.obt_velocity - station.obt_velocity, away);

            Note = "BACKING AWAY - " + SeparationM.ToString("F0") + " of " + SafeDistM
                 + " m, opening at " + OpeningMps.ToString("F2") + " m/s";

            if (SeparationM > SafeDistM || OpeningMps >= BackRateMps || inStage > BurstMaxS)
            {
                Push(0.0);
                Debug.Log(Tag + "back-away burst done - " + SeparationM.ToString("F0")
                          + " m, opening at " + OpeningMps.ToString("F2")
                          + " m/s, mono " + Mono(ship).ToString("F1") + ". Coasting the rest.");
                Go(UndockStage.Coasting);
                return;
            }
            Push(foreSign);
        }

        /// <summary>No thrust at all from here. Nothing in orbit slows us down.</summary>
        private static void Coast(double inStage)
        {
            Push(0.0);
            if (station == null) { Go(UndockStage.Clear); return; }
            SeparationM = Vector3d.Distance(ship.CoM, station.CoM);
            Note = "COASTING OUT - " + SeparationM.ToString("F0") + " of " + SafeDistM + " m";

            if (SeparationM >= SafeDistM)
            {
                Debug.Log(Tag + "clear of the station at " + SeparationM.ToString("F0") + " m");
                Go(UndockStage.Clear);
                Engaged = false;
            }
            else if (inStage > BackMaxS)
            {
                Debug.LogWarning(Tag + "back-away timed out at " + SeparationM.ToString("F0")
                                 + " m - not clear, but no longer pushing");
                Stage = UndockStage.Failed;
                Engaged = false;
            }
        }

        private static void Push(double sign)
        {
            AttitudeController.Ascent.UllageFore = sign;
        }

        /// <summary>
        /// Monopropellant in the CAPSULE'S tank - not the station's, and not the two added together.
        ///
        /// ---- ⛔ THIS IS THE BUG F9I HAS IN THE GAME RIGHT NOW. DO NOT REINTRODUCE IT. ----
        /// Reported 2026-08-11: the station refuel fills the Dragon, and the undock then announces
        /// that the tanks are not full. They are. The reading is coming off the MERGED vessel, so it
        /// is the station's 6 237-unit farm being measured, and that is never full.
        ///
        /// ⚠ IT ALSO DISABLED THE TOP-UP ENTIRELY, WHICH IS WORSE THAN THE MESSAGE. A transfer from
        /// the station into the capsule moves propellant WITHIN one merged vessel - the merged total
        /// does not move by a single unit. `TopUp` waits for that total to stop rising before letting
        /// go, so against the merged number it could never see progress: every undock would fall
        /// through on the six-second flat-line timeout and report "no propellant moved", however much
        /// had actually been taken on.
        /// </summary>
        private static double Mono(Vessel v)
        {
            return DockedSide.Mono(v);
        }
    }
}
