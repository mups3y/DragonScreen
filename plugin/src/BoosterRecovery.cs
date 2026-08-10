/*
 * DragonScreen - BoosterRecovery
 *
 * GLUE. Flies `pure/Landing.cs` on the BOOSTER after separation, hands focus over to it, and hands
 * focus back to the upper stage when it is down.
 *
 * ---- THE HANDOVER IS COPIED FROM OUR OWN kOS INTERFACE, WHICH ALREADY SOLVED THIS ----
 * `Ships/Script/falcon9.ks:4723` FalconExtendRange and FalconFocusBooster:
 *
 *      SetLoadDistances(ship, 1500000)              raise physics range to 1500 km
 *      wait until a vessel named "Booster" is loaded
 *      SetLoadDistances(vessel("Booster"), 1500000)  raise it on the booster too
 *      kuniverse:forceactive(vessel("Booster"))      switch focus
 *
 * The C# equivalents were confirmed present in the game's own assembly rather than recalled:
 * `Vessel.vesselRanges` (Assembly-CSharp even carries the hint "Use Vessel.vesselRanges instead")
 * and `FlightGlobals.ForceSetActiveVessel`, which MechJeb also uses at
 * MechJebModuleStagingController.cs:402.
 *
 * ---- ⚠ AND F9I'S OWN WARNING APPLIES TO US IDENTICALLY ----
 * Its HUD text says it plainly: "KSP will clamp this without PhysicsRangeExtender, so expect the
 * upper stage to unload near 300 km." `falcon-physics-range-clamp` MEASURED that on four flights -
 * 1500 km is requested, 297-341 km is what you get, because PhysicsRangeExtender is NOT installed.
 *
 * So we CANNOT fly both vehicles at once for a whole recovery. What we do instead is honest and
 * works: the upper stage's apoapsis is already set by the ascent, so it COASTS - on rails once it
 * unloads, which preserves its orbit exactly - while the booster flies its recovery. When the
 * booster is down we take focus back and circularise. If the coast has already carried the upper
 * stage past apoapsis, circularisation happens at the NEXT one; nothing is lost but time.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class BoosterRecovery
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>What F9I asks for. KSP clamps it; asking is still correct.</summary>
        public const float RangeMetres = 1500000f;

        public static bool Active { get; private set; }
        public static LandingPhase Phase { get; private set; }
        public static LandingCommand Command;

        private static Vessel booster, upperStage;
        private static double startedAt;

        /// <summary>The pad we came from. Captured at liftoff - it is the RTLS target.</summary>
        public static double PadLat, PadLon;
        public static bool HavePad;

        public static void RememberPad(Vessel v)
        {
            if (v == null || HavePad) return;
            PadLat = v.latitude; PadLon = v.longitude; HavePad = true;
            Debug.Log(Tag + "landing zone remembered: " + PadLat.ToString("F4")
                      + ", " + PadLon.ToString("F4"));
        }

        public static void Reset()
        {
            Active = false; Phase = LandingPhase.Idle;
            booster = null; upperStage = null; HavePad = false;
        }

        // ------------------------------------------------------------------ handover

        /// <summary>
        /// Look for a separated booster. Called while the ascent is running; returns true once the
        /// handover has happened and the caller should stop flying the upper stage.
        /// </summary>
        public static bool TryHandover(Vessel active)
        {
            if (Active || active == null) return false;

            Vessel b = FindBooster(active);
            if (b == null) return false;

            upperStage = active;
            booster = b;
            Active = true;
            Phase = LandingPhase.Boostback;
            startedAt = Planetarium.GetUniversalTime();

            Extend(upperStage);
            Extend(booster);
            FlightGlobals.ForceSetActiveVessel(booster);

            Debug.Log(Tag + "booster recovery: focus -> '" + booster.vesselName
                      + "', upper stage '" + upperStage.vesselName + "' coasts to apoapsis. "
                      + "Physics range asked " + (RangeMetres / 1000f).ToString("F0")
                      + " km; KSP clamps near 300 km without PhysicsRangeExtender.");
            return true;
        }

        /// <summary>
        /// The separated first stage: a loaded vessel that is not us, is not on the ground, and
        /// carries a booster tank. Matched on part name the same way FlightCommands finds the trunk.
        /// </summary>
        private static Vessel FindBooster(Vessel active)
        {
            List<Vessel> all = FlightGlobals.VesselsLoaded;
            for (int i = 0; i < all.Count; i++)
            {
                Vessel v = all[i];
                if (v == null || v == active || v.packed) continue;
                if (v.situation == Vessel.Situations.LANDED
                    || v.situation == Vessel.Situations.SPLASHED
                    || v.situation == Vessel.Situations.PRELAUNCH) continue;
                if (v.GetCrewCount() > 0) continue;            // never take a crewed vehicle

                // See VehicleParts: this matched "K1" from a PAW title and therefore never fired,
                // so the recovery could not run even once the handover gate was right.
                for (int p = 0; p < v.parts.Count; p++)
                    if (VehicleParts.IsBooster(v.parts[p].name)) return v;
            }
            return null;
        }

        private static void Extend(Vessel v)
        {
            if (v == null) return;
            try
            {
                VesselRanges r = v.vesselRanges;
                if (r == null) return;
                Set(r.flying); Set(r.subOrbital); Set(r.orbit); Set(r.escaping);
                v.vesselRanges = r;
            }
            catch (Exception e) { Debug.LogWarning(Tag + "could not extend range: " + e.Message); }
        }

        private static void Set(VesselRanges.Situation s)
        {
            if (s == null) return;
            s.load = RangeMetres;
            s.unload = RangeMetres * 1.1f;
            s.pack = RangeMetres * 0.9f;
            s.unpack = 200f;
        }

        // ------------------------------------------------------------------ flying it

        public static void Tick()
        {
            if (!Active) return;

            if (booster == null || booster.state == Vessel.State.DEAD)
            {
                Finish("booster lost");
                return;
            }

            LandingInputs s = Read(booster);
            LandingCommand c = Landing.Guide(s, Phase);

            if (c.Phase != Phase)
                Debug.Log(Tag + "booster -> " + Landing.Name(c.Phase)
                          + "  alt " + (s.AltitudeRadar / 1000.0).ToString("F1")
                          + " km, v " + s.SurfaceSpeed.ToString("F0")
                          + " m/s, downrange " + (s.DownrangeM / 1000.0).ToString("F1")
                          + " km, ignition at " + (c.IgnitionAltitude / 1000.0).ToString("F2")
                          + " km on " + c.Engines + " engine(s)");
            Phase = c.Phase;
            Command = c;

            if (Phase == LandingPhase.Touchdown) { Finish("booster down"); return; }

            // Only fly it while it is the ACTIVE vessel - an unloaded vessel is on rails and writing
            // a throttle to it does nothing but look like it worked.
            if (FlightGlobals.ActiveVessel != booster) return;

            SetEngines(booster, c.Engines);
            Aim(booster, c, s);
            FlightInputHandler.state.mainThrottle = (float)c.Throttle;

            // ⛔ ISSUE 6. This was a second, hard-coded copy of the legs rule, so the DeployLegs
            // the guidance computes was ignored and the two could disagree. One source.
            if (c.DeployLegs) booster.ActionGroups.SetGroup(KSPActionGroup.Gear, true);
        }

        private static void Finish(string why)
        {
            Debug.Log(Tag + "booster recovery complete - " + why
                      + " after " + (Planetarium.GetUniversalTime() - startedAt).ToString("F0") + " s");
            FlightInputHandler.state.mainThrottle = 0f;
            Active = false;

            // Hand focus back so the upper stage can finish the job it was left in the middle of.
            if (upperStage != null && upperStage.state != Vessel.State.DEAD)
            {
                FlightGlobals.ForceSetActiveVessel(upperStage);
                Debug.Log(Tag + "focus -> '" + upperStage.vesselName + "' to circularise");
            }
        }

        private static LandingInputs Read(Vessel v)
        {
            LandingInputs s = new LandingInputs();
            s.Valid = true;
            s.AltitudeRadar = v.radarAltitude;
            // ⛔ ISSUE 1. This was NEVER SET, so it defaulted to 0 - and `InEntryBand` tests
            // `AltitudeAsl < 32500`, which 0 always satisfies. The entry-burn gate was therefore
            // permanently OPEN, and the burn would have lit the moment vertical speed passed
            // -300 m/s at ANY altitude, including straight after separation at 60 km.
            s.AltitudeAsl = v.altitude;
            s.VerticalSpeed = v.verticalSpeed;
            s.SurfaceSpeed = v.srfSpeed;
            s.HorizontalSpeed = Math.Sqrt(Math.Max(0.0,
                v.srfSpeed * v.srfSpeed - v.verticalSpeed * v.verticalSpeed));
            s.Gravity = (v.mainBody != null)
                ? v.mainBody.gMagnitudeAtCenter / Math.Pow(v.mainBody.Radius + v.altitude, 2.0) : 9.81;
            s.AtmosphereDepthM = (v.mainBody != null) ? v.mainBody.atmosphereDepth : 70000.0;
            s.DynamicPressureKpa = v.dynamicPressurekPa;
            s.Landed = (v.situation == Vessel.Situations.LANDED
                     || v.situation == Vessel.Situations.SPLASHED);

            // Ambient pressure in ATMOSPHERES - atmosphereCurve is keyed in atm, GetPressure is kPa.
            double pressureAtm = (v.mainBody != null)
                ? v.mainBody.GetPressure(v.altitude) / 101.325 : 0.0;

            double thrust = 0.0; int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleEngines> es = v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                {
                    if (es[m].flameout) continue;
                    // ⛔ ISSUE 2. This was MaxThrustOutputVac - VACUUM thrust - for a booster
                    // landing THROUGH AN ATMOSPHERE. A Merlin makes materially less at sea level,
                    // so the solve believed in thrust the stage did not have, put the hoverslam
                    // ignition altitude too LOW, and would have flown it into the pad at speed.
                    // Atmospheric output at the CURRENT pressure is the honest number.
                    // Thrust scales with Isp at fixed mass flow, and `atmosphereCurve` IS the
                    // engine's Isp-against-pressure curve - so the sea-level/vacuum Isp ratio is
                    // the thrust ratio. Built from `maxThrust` and `atmosphereCurve` because both
                    // are plain ModuleEngines fields; the first attempt at this called
                    // `MaxThrustOutputAtm(..., v.staticAmbientTemperature, ...)` and the compiler
                    // rejected it, which is trigger #3 doing its job.
                    float isp0 = es[m].atmosphereCurve.Evaluate(0f);
                    float ispNow = es[m].atmosphereCurve.Evaluate((float)pressureAtm);
                    double scale = (isp0 > 0.01f) ? ispNow / isp0 : 1.0;
                    thrust += es[m].maxThrust * scale;
                    n++;
                }
            }
            s.EngineCount = n;
            double mass = v.GetTotalMass();
            s.MaxThrustAccel = (mass > 0.0) ? thrust / mass : 0.0;

            s.DownrangeM = HavePad && v.mainBody != null
                ? GroundRange(v.mainBody, v.latitude, v.longitude, PadLat, PadLon) : 0.0;
            return s;
        }

        /// <summary>
        /// Great-circle ground distance, metres.
        ///
        /// ⚠ NOT degrees times 111 320. `kerbin-degree-to-metres`: a Kerbin degree is 10 472 m, and
        /// hard-coding Earth's figure has cost this project real miss distances before. Using the
        /// body's OWN radius makes it right on any body without a table.
        /// </summary>
        public static double GroundRange(CelestialBody b, double lat1, double lon1,
                                         double lat2, double lon2)
        {
            double p1 = lat1 * Math.PI / 180.0, p2 = lat2 * Math.PI / 180.0;
            double dp = (lat2 - lat1) * Math.PI / 180.0;
            double dl = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dp / 2) * Math.Sin(dp / 2)
                     + Math.Cos(p1) * Math.Cos(p2) * Math.Sin(dl / 2) * Math.Sin(dl / 2);
            return 2.0 * b.Radius * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        }

        /// <summary>
        /// Light exactly <paramref name="want"/> engines, choosing the ones NEAREST THE CENTRELINE.
        ///
        /// Falcon 9's single-engine landing burn is the CENTRE engine, and it has to be: an outboard
        /// engine alone would put the thrust vector off the centre of mass. Sorting by distance from
        /// the axis finds it without needing to know the part's name.
        /// </summary>
        private static void SetEngines(Vessel v, int want)
        {
            List<ModuleEngines> all = new List<ModuleEngines>();
            for (int i = 0; i < v.parts.Count; i++)
                all.AddRange(v.parts[i].Modules.GetModules<ModuleEngines>());
            if (all.Count == 0) return;

            if (want <= 0)
            {
                for (int i = 0; i < all.Count; i++) if (all[i].EngineIgnited) all[i].Shutdown();
                return;
            }

            Vector3 axis = v.ReferenceTransform.up;
            Vector3 com = v.CoM;
            all.Sort(delegate (ModuleEngines a, ModuleEngines b)
            {
                return Off(a, com, axis).CompareTo(Off(b, com, axis));
            });

            for (int i = 0; i < all.Count; i++)
            {
                bool on = i < want && !all[i].flameout;
                if (on && !all[i].EngineIgnited) all[i].Activate();
                else if (!on && all[i].EngineIgnited) all[i].Shutdown();
            }
        }

        private static float Off(ModuleEngines e, Vector3 com, Vector3 axis)
        {
            if (e == null || e.part == null) return float.MaxValue;
            return Vector3.ProjectOnPlane(e.part.transform.position - com, axis).sqrMagnitude;
        }

        private static void Aim(Vessel v, LandingCommand c, LandingInputs s)
        {
            Vector3d up = (v.CoM - v.mainBody.position).normalized;
            Vector3d dir = up;

            if (c.Aim == LandingAim.SurfaceRetrograde)
            {
                Vector3d srf = v.srf_velocity;
                if (srf.sqrMagnitude > 1.0) dir = -srf.normalized;
            }
            else if (c.Aim == LandingAim.TowardTarget && HavePad)
            {
                // Boostback: pitch up off the horizon so the burn both kills downrange velocity and
                // holds altitude while it does it, which is what the real flip-and-burn looks like.
                Vector3d toPad = (v.mainBody.GetWorldSurfacePosition(PadLat, PadLon, v.altitude)
                                  - v.CoM);
                Vector3d horiz = Vector3d.Exclude(up, toPad);
                if (horiz.sqrMagnitude > 1.0)
                    dir = (horiz.normalized * Math.Cos(20.0 * Math.PI / 180.0)
                         + up * Math.Sin(20.0 * Math.PI / 180.0)).normalized;
            }

            // ---- ⛔ ISSUE 5. LandingZoneGuidance WAS PORTED AND NEVER CONNECTED. ----
            // `Landing.LeanFraction` and `Landing.GuidanceAoaDeg` were written last session, tested,
            // documented as DONE in the port map - and referenced from nowhere. The descent flew
            // plain retrograde, so the booster had NO steering toward the pad at all and the whole
            // landing-accuracy port was doing nothing.
            //
            // The lean is retrograde plus tan(AoA) x errScale toward the miss, exactly as
            // BOOSTER.ks:571. The AoA SIGN FLIPS under thrust - positive works aerodynamically,
            // negative is needed once the force is along the nose - which is why it is asked for
            // per-tick with the engine state rather than held as a constant.
            if (HavePad && c.Aim == LandingAim.SurfaceRetrograde && s.DownrangeM > 0.0)
            {
                Vector3d toPad = v.mainBody.GetWorldSurfacePosition(PadLat, PadLon, v.altitude)
                                 - v.CoM;
                Vector3d errHoriz = Vector3d.Exclude(up, -toPad);   // from the pad toward us = miss
                if (errHoriz.sqrMagnitude > 1.0)
                {
                    double aoa = Landing.GuidanceAoaDeg(s.AltitudeRadar, c.Throttle > 0.01);
                    double lean = Landing.LeanFraction(s.DownrangeM, aoa);
                    dir = (dir.normalized + lean * errHoriz.normalized).normalized;
                }
            }

            // Same controller the ascent uses. A landing booster wants a TIGHT stopping time -
            // BOOSTER.ks sets maxstoppingtime 0.05 for the landing burn against 1 for the coast,
            // because the two want opposite behaviour and one setting cannot serve both.
            AttitudeController.MaxStoppingTime =
                (c.Phase == LandingPhase.LandingBurn) ? 0.05 : 1.0;
            AttitudeController.SteerTo(v, dir, up);
        }
    }
}
