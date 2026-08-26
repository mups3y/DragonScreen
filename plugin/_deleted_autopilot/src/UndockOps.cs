// DragonScreen - UndockOps
// ---- ⛔ THREE TRAPS, ALL OF THEM FLIGHTS ----
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public enum UndockStage : byte
    {
        Idle = 0,
        ToppingUp,
        Releasing,
        CalibratingSign,
        Burst,
        Coasting,
        Clear,
        Failed
    }

    public static class UndockOps
    {
        private const string Tag = "[DragonScreen] ";

        // ---- F9I's CONSTANTS. station_ops.ks:81-87, 705-706. ----
        public const double SafeDistM = 150.0;
        public const double BackRateMps = 1.5;
        public const double BurstMaxS = 6.0;
        public const double BackMaxS = 180.0;
        public const double SignTestS = 2.0;
        public const double TopUpMaxS = 120.0;
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
            if (s == UndockStage.Clear || s == UndockStage.Failed)
                DockingOps.SetTarget(null, "undock complete - clear of the station");

            if (s == UndockStage.Clear)
                DockShroud.Close(ship);

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

        private static void TopUp(double now, double inStage)
        {
            // ---- ⛔ AND SOMETHING HAS TO ACTUALLY MOVE THE PROPELLANT. ----
            Refuel.Tick(ship, now - lastTopUpAt);
            lastTopUpAt = now;

            double mono = Mono(ship);
            if (mono > lastMono + 0.01) { lastMono = mono; lastProgressAt = now; }

            double flat = now - lastProgressAt;
            bool full = Refuel.Full(ship);
            Note = "TOPPING UP - " + mono.ToString("F1") + " units ("
                 + (Refuel.Fraction(ship) * 100.0).ToString("F0") + "%), "
                 + (full ? "full" : (flat > 1.0 ? "flat for " + flat.ToString("F0") + " s" : "filling"));

            if (full || inStage > TopUpMaxS || flat > TopUpFlatS)
            {
                Debug.Log(Tag + "refuel finished after " + inStage.ToString("F0") + " s - "
                          + Refuel.Report(ship));
                if (!full)
                    Debug.LogWarning(Tag + "⚠ UNDOCKING WITHOUT A FULL TANK - the return budget is "
                                     + "sized on leaving the berth full. Check the de-orbit margin "
                                     + "before committing.");
                Go(UndockStage.Releasing);
            }
        }

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
                    if (ns[m].otherNode == null) continue;
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

        private static void Calibrate(double inStage)
        {
            if (station == null) { Go(UndockStage.Burst); return; }
            CapsuleRcs.Set(ship, CapsuleRcs.UndockPct);
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

        /// ---- ⛔ THIS IS THE BUG F9I HAS IN THE GAME RIGHT NOW. DO NOT REINTRODUCE IT. ----
        private static double Mono(Vessel v)
        {
            return DockedSide.Mono(v);
        }
    }
}
