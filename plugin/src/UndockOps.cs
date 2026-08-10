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
        private static double stageStartedAt, lastProgressAt, lastMono, d0;
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
            double mono = Mono(ship);
            if (mono > lastMono + 0.01) { lastMono = mono; lastProgressAt = now; }

            double flat = now - lastProgressAt;
            Note = "TOPPING UP - " + mono.ToString("F1") + " units, "
                 + (flat > 1.0 ? "flat for " + flat.ToString("F0") + " s" : "filling");

            if (inStage > TopUpMaxS || flat > TopUpFlatS)
            {
                Debug.Log(Tag + "top-up finished at " + mono.ToString("F1") + " units after "
                          + inStage.ToString("F0") + " s");
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

        private static double Mono(Vessel v)
        {
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
                for (int k = 0; k < v.parts[i].Resources.Count; k++)
                    if (v.parts[i].Resources[k].resourceName == "MonoPropellant")
                        t += v.parts[i].Resources[k].amount;
            return t;
        }
    }
}
