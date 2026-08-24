/*
 * DragonScreen - ImpactPredictor
 *
 * GLUE. Measures each vehicle's drag in flight and integrates its trajectory to a ground impact.
 * Law in `pure/Trajectory.cs`.
 *
 * ---- WHAT THIS REPLACES ----
 * A vacuum ballistic solve, which is fine for a boostback that deliberately overshoots by 2.7 km and
 * useless for a de-orbit burn with a 50 m stop tolerance. Drag only ever shortens a trajectory, so
 * the vacuum answer is always LONG - by tens of kilometres on an entry.
 *
 * ---- ⛔ THE DRAG IS MEASURED FROM THE VEHICLE, NOT MODELLED ----
 * a_drag = a_total − a_gravity − a_thrust, taken from the vessel's own velocity change over a
 * physics tick. Everything KSP's aerodynamics does - drag cubes, occlusion, orientation, deployed
 * fins, a jettisoned trunk - arrives in that one number without anyone writing a coefficient down.
 *
 * `falcon-detect-by-capability` is the same principle: ask the vehicle what it is, do not describe
 * it. A hard-coded Cd·A would be wrong the first time a booster put its grid fins out.
 *
 * ---- ⚠ AND IT IS ONLY VALID WHERE IT WAS MEASURED ----
 * A coefficient measured at Mach 5 in thin air does not describe the same vehicle subsonic at sea
 * level, and KSP's drag really does vary with Mach. The estimate is continuously re-measured and
 * time-filtered, so it tracks; it is NOT a constant fitted once. Where no measurement is possible -
 * vacuum, or barely moving - it reports zero and the integrator falls back to a vacuum solve and
 * says which it did. A prediction that quietly changes meaning is worse than one that admits it.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    /// <summary>A predicted impact, in the terms the guidance actually wants.</summary>
    public struct Impact
    {
        public bool Valid;
        public double LatDeg, LonDeg;
        public double TimeToImpactS, SpeedMps;
        /// <summary>True when the integration actually modelled drag.</summary>
        public bool DragModelled;
        public string Note;
    }

    /// <summary>
    /// The STOCK aerodynamic drag of a vessel, tabulated over Mach - the port of Trajectories'
    /// StockAeroUtil.SimAeroForce that makes our predicted impact agree with the add-on.
    ///
    /// ---- ⛔ WHY A TABLE, AND WHY MACH ----
    /// KSP's drag is per-part drag cubes whose area varies with Mach; a single ballistic coefficient
    /// (what we used before) is that area frozen at ONE Mach, so it over-drags badly at the high Mach
    /// of an entry and lands the prediction tens of km short (flight_0820_092128: ours said 7.5 km,
    /// the truth was 29 km). This sums each part's <c>DragCubes.AddSurfaceDragDirection(...).areaDrag</c>
    /// at a grid of Mach numbers, ONCE, for the SHIELD-FORWARD attitude, then the integrator
    /// interpolates - the same "cache it, don't recompute per step" the add-on does.
    ///
    /// The drag acceleration the integrator applies is <c>0.5·rho·v²·DragFactor(mach, rho·v)</c>,
    /// matching SimAeroForce: <c>force = dyn_pressure · areaDrag · DragCubeMultiplier ·
    /// pseudoReynolds · DragMultiplier</c> with <c>dyn_pressure = 0.0005·rho·v²</c>.
    /// </summary>
    public sealed class AeroTable
    {
        public const double MachMax = 25.0;      // SimAeroForce caps Mach at 25
        public const int Bins = 51;              // 0..25 in 0.5 steps

        public double Mass;                      // TONNES - KSP force is kN, so kN/tonne = m/s²
        public double[] AreaDrag;                // Σ part areaDrag at each Mach bin, shield-forward
        public double DragCubeMult, DragMult;    // PhysicsGlobals multipliers
        private readonly FloatCurve pseudoRe;    // PhysicsGlobals.DragCurvePseudoReynolds

        public AeroTable(FloatCurve pseudoReynolds) { pseudoRe = pseudoReynolds; }

        /// <summary>1/BC-equivalent: drag accel = 0.5·rho·v²·this.</summary>
        public double DragFactor(double mach, double pseudoReynolds)
        {
            if (Mass <= 0.0 || AreaDrag == null) return 0.0;
            double pr = (pseudoRe != null) ? pseudoRe.Evaluate((float)pseudoReynolds) : 1.0;
            // 0.0005/0.5 = 0.001 (dyn_pressure uses 0.0005; the integrator's 0.5·rho·v² supplies the rest)
            return 0.001 * DragMult * DragCubeMult * pr * InterpArea(mach) / Mass;
        }

        private double InterpArea(double mach)
        {
            if (mach <= 0.0) return AreaDrag[0];
            if (mach >= MachMax) return AreaDrag[Bins - 1];
            double f = mach / MachMax * (Bins - 1);
            int i = (int)f;
            if (i >= Bins - 1) return AreaDrag[Bins - 1];
            double frac = f - i;
            return AreaDrag[i] * (1.0 - frac) + AreaDrag[i + 1] * frac;
        }
    }

    public static class ImpactPredictor
    {
        private const string Tag = "[DragonScreen] ";

        // ---- ⛔ FAR/RSS-CLEAN DRAG SAMPLING (2026-08-23) ----
        // The live bc is the ONLY FAR-consistent drag we have (it measures the vessel's real
        // deceleration, so FAR's forces arrive in the number without a coefficient - the stock
        // DragCube AeroTable path does NOT match FAR and is capsule-entry only). But it was sampled
        // in two regimes where it means nothing, and both poisoned the booster's descent prediction
        // (flight_0823_100646: bc swung 16 -> 2151, miss prediction good in coast, garbage in the burn):

        /// <summary>Skip drag sampling while thrust exceeds this (kN). The thrust subtraction assumes
        /// thrust along the nose (+ReferenceTransform.up), but a descending booster burns ENGINES-
        /// RETROGRADE - the opposite direction - so a sample taken under thrust is sign-flipped. Hold
        /// the last clean estimate through the entry/landing burns instead. See Sample().</summary>
        public const double ThrustCleanMaxKn = 1.0;

        /// <summary>Skip drag sampling below this dynamic pressure (Pa). In RSS the air above ~80 km is
        /// effectively vacuum (q &lt; 0.001 kPa) yet still inside <c>atmosphereDepth</c> (~140 km), so
        /// the bare depth gate let near-vacuum noise drive bc to 16-120. 100 Pa (0.1 kPa) is where FAR
        /// drag is actually measurable. See Sample().</summary>
        public const double QSampleFloorPa = 100.0;

        /// <summary>Ballistic coefficient estimate per vessel, kg/m². Zero means not yet known.</summary>
        private static readonly System.Collections.Generic.Dictionary<uint, double> bc =
            new System.Collections.Generic.Dictionary<uint, double>();

        private static readonly System.Collections.Generic.Dictionary<uint, Vector3d> lastVel =
            new System.Collections.Generic.Dictionary<uint, Vector3d>();

        /// <summary>
        /// When each vessel was last sampled.
        ///
        /// ---- ⛔ THIS WAS ONE SHARED FIELD, AND IT MEANT ONLY ONE VEHICLE WAS EVER MEASURED ----
        /// `FlightDriver` samples the ascent vehicle and then the booster in the same frame. With a
        /// single timestamp the first call stamped it to now, so the second computed dt = 0, hit the
        /// `dt &lt;= 0` guard and returned - every frame, for the whole flight. The booster's ballistic
        /// coefficient stayed at zero, which silently downgrades its impact prediction to a VACUUM
        /// solve: always long, by kilometres, on the one vehicle whose whole descent is a drag problem.
        /// It fails invisibly because `Predict` still returns an answer and only `DragModelled` says
        /// which kind. Per-vessel, like everything else in here.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<uint, double> lastSampleUt =
            new System.Collections.Generic.Dictionary<uint, double>();

        public static void Reset()
        {
            bc.Clear(); lastVel.Clear(); lastSampleUt.Clear();
            MapValid = false; if (MapPath != null) MapPath.Clear();
        }

        /// <summary>The current estimate for a vessel, for the recorder. Zero = unknown.</summary>
        public static double BallisticCoefficient(Vessel v)
        {
            if (v == null) return 0.0;
            double b;
            return bc.TryGetValue(v.persistentId, out b) ? b : 0.0;
        }

        /// <summary>
        /// Sample a vessel's drag. Call every tick for every vehicle whose trajectory matters.
        ///
        /// The measurement is only taken when it can mean something: in air, moving, and with a
        /// decelerating component that is not just thrust. Anything else leaves the estimate alone
        /// rather than dragging it toward a meaningless number.
        /// </summary>
        public static void Sample(Vessel v)
        {
            if (v == null || v.mainBody == null || v.packed) return;
            double now = Planetarium.GetUniversalTime();
            uint id = v.persistentId;

            Vector3d vel = v.obt_velocity;
            Vector3d prev;
            bool havePrev = lastVel.TryGetValue(id, out prev);
            lastVel[id] = vel;

            double was;
            bool haveWas = lastSampleUt.TryGetValue(id, out was);
            lastSampleUt[id] = now;
            double dt = haveWas ? now - was : 0.0;
            if (!havePrev || dt <= 0.0 || dt > 1.0) return;      // a warp jump is not a measurement

            CelestialBody b = v.mainBody;
            double alt = v.altitude;
            if (alt >= b.atmosphereDepth) return;                // nothing to measure in vacuum

            // a_total from the vessel's own velocity change.
            Vector3d aTotal = (vel - prev) / dt;

            // Subtract gravity.
            Vector3d r = v.CoM - b.position;
            double rm = r.magnitude;
            if (rm < 1.0) return;
            Vector3d aGrav = -r.normalized * (b.gravParameter / (rm * rm));

            // ⛔ FAR-CLEAN: engines OFF only. The thrust subtraction below assumes thrust along the
            // nose, but a descending booster burns engines-retrograde, so any sample under thrust is
            // sign-flipped garbage (bc hit 1800 during the entry burn). Hold the last clean bc instead.
            double thrust = LiveThrust(v);
            if (thrust > ThrustCleanMaxKn) return;

            // Subtract thrust, along the nose. (Near zero here by the gate above; kept for correctness.)
            double mass = v.GetTotalMass();
            if (mass <= 0.0) return;
            Vector3d aThrust = (Vector3d)v.ReferenceTransform.up * (thrust / mass);

            Vector3d aDrag = aTotal - aGrav - aThrust;

            // Only the component OPPOSING the surface velocity is drag. Lift and noise are not, and
            // taking the whole magnitude would inflate the estimate on a lifting entry.
            Vector3d srf = v.srf_velocity;
            if (srf.magnitude < 10.0) return;
            double along = Vector3d.Dot(aDrag, -srf.normalized);
            if (along <= 0.0) return;                            // accelerating; not a drag sample

            double rho = b.GetDensity(b.GetPressure(alt), b.GetTemperature(alt));

            // ⛔ FAR-CLEAN: only where there is real drag to measure. RSS air above ~80 km is
            // effectively vacuum yet still below atmosphereDepth, so the bare depth gate let
            // near-vacuum noise set bc to 16-120. Require measurable dynamic pressure.
            double q = 0.5 * rho * srf.magnitude * srf.magnitude;
            if (q < QSampleFloorPa) return;

            double sample = Trajectory.BallisticCoefficientFrom(rho, srf.magnitude, along);
            if (sample <= 0.0) return;

            double old;
            if (!bc.TryGetValue(id, out old)) old = 0.0;
            bc[id] = Trajectory.SmoothBc(old, sample, dt, Trajectory.BcFilterTauS);
        }

        /// <summary>
        /// Integrate this vessel's trajectory to a ground impact.
        ///
        /// The impact comes out as a LAT/LON, with the body's rotation during the flight already
        /// removed - a 600 km body turning once per six hours moves 175 m/s at the equator, which is
        /// kilometres of error over a long entry.
        /// </summary>
        public static Impact Predict(Vessel v) { return Predict(v, 0.0); }

        /// <summary>
        /// As <see cref="Predict(Vessel)"/> but with an EXPLICIT ballistic coefficient instead of the
        /// live-measured one. This is what lets the de-orbit predict a drag-aware landing BEFORE entry,
        /// in vacuum, where nothing has measured drag yet: hand it the capsule's KNOWN bc (measured
        /// ~440 kg/m2 over flights 0817_214211 and 0817_232723) and Trajectory.Solve integrates the
        /// real entry instead of a vacuum parabola. bcOverride &lt;= 0 falls back to the live estimate.
        /// </summary>
        public static Impact Predict(Vessel v, double bcOverride)
        {
            if (v == null || v.mainBody == null) { Impact im = new Impact(); im.Note = "no vessel"; return im; }
            CelestialBody b = v.mainBody;
            // ---- ⛔ STOCK DRAG-CUBE TABLE IS FOR STOCK KSP ONLY. FAR REPLACES IT. ----
            // BuildAeroTable ports Trajectories' StockAeroUtil - it describes KSP's OWN drag cubes. On
            // Earth with FAR installed the acting aerodynamics are FAR's, NOT the drag cubes, so the
            // table predicts the wrong forces (RO_MODS_MECHANICS: capsule-entry FAR mismatch). Under FAR
            // the ONLY consistent drag we have is the one MEASURED from the vehicle's own deceleration -
            // the same source the booster already uses - so we drop the table and fly the scalar bc:
            // the LIVE-measured value once in atmosphere (FAR reality), the known override in vacuum
            // before anything has been measured. Stock keeps the Mach-tabulated cubes.
            bool far = (b.Radius > 1.0e6);                       // Earth/RSS with FAR (Kerbin 600 km)
            double measured = BallisticCoefficient(v);
            AeroTable table = (bcOverride > 0.0 && !far) ? BuildAeroTable(v) : null;
            double useBc = far ? (measured > 0.0 ? measured : bcOverride)
                               : (bcOverride > 0.0 ? bcOverride : measured);
            return PredictFromState(b, v.CoM - b.position, v.obt_velocity, useBc, table);
        }

        /// <summary>
        /// Integrate to impact from an EXPLICIT state rather than a live vessel: a position relative to
        /// the body centre and an inertial (world-frame) velocity. This is what lets the de-orbit
        /// ignition search ask "if the burn happened HERE, where would the capsule come down?" for a
        /// hypothetical post-burn state the vessel is not actually in yet. The frame handling is exactly
        /// <see cref="Predict(Vessel,double)"/>'s - extracted, not re-derived, so the two cannot drift.
        /// </summary>
        public static Impact PredictFromState(CelestialBody b, Vector3d posRelBody, Vector3d velWorld,
                                              double bc)
        {
            return PredictFromState(b, posRelBody, velWorld, bc, null);
        }

        /// <summary>
        /// As above, with an optional STOCK drag model. When <paramref name="table"/> is non-null the
        /// integration uses the Mach-tabulated drag cubes (matching Trajectories) and ignores
        /// <paramref name="bc"/>; when null it falls back to the scalar bc.
        /// </summary>
        public static Impact PredictFromState(CelestialBody b, Vector3d posRelBody, Vector3d velWorld,
                                              double bc, AeroTable table)
        {
            Impact im = new Impact();
            if (b == null) { im.Note = "no body"; return im; }

            TrajectoryInputs s = new TrajectoryInputs();
            Vector3d r = posRelBody;
            Vector3d vel = velWorld;
            s.Px = r.x; s.Py = r.y; s.Pz = r.z;
            s.Vx = vel.x; s.Vy = vel.y; s.Vz = vel.z;
            s.Mu = b.gravParameter;
            s.BodyRadiusM = b.Radius;
            s.AtmosphereDepthM = b.atmosphereDepth;
            s.BallisticCoefficient = bc;
            s.ImpactAltitudeM = 0.0;
            if (table != null)
            {
                s.DragFactor = table.DragFactor;
                CelestialBody body = b;
                s.SoundSpeed = delegate(double alt) { return StockSoundSpeed(body, alt); };
            }

            // ⚠ THE ROTATION AXIS IS THE BODY'S, NOT THE FRAME'S +Z. Integrating about the wrong
            // axis puts the ground track sideways, which reads as a cross-range error nobody can
            // account for. KSP gives the body's angular velocity directly; use it.
            s.BodyOmega = b.angularVelocity.magnitude;

            // The integrator works in a frame whose +Z is the rotation axis, so hand it a state
            // already expressed there rather than rotating the answer back afterwards.
            Vector3d axis = b.angularVelocity.normalized;
            if (axis.sqrMagnitude < 0.5) axis = b.transform.up;
            QuaternionD toAxis = (QuaternionD)Quaternion.FromToRotation((Vector3)axis, Vector3.forward);
            Vector3d rp = toAxis * r;
            Vector3d vp = toAxis * vel;
            s.Px = rp.x; s.Py = rp.y; s.Pz = rp.z;
            s.Vx = vp.x; s.Vy = vp.y; s.Vz = vp.z;

            CelestialBody db = b;
            TrajectoryResult t = Trajectory.Solve(s, delegate(double alt)
            {
                if (alt < 0.0 || alt >= db.atmosphereDepth) return 0.0;
                return StockDensity(db, alt);
            });

            im.Note = t.Note;
            im.DragModelled = t.DragModelled;
            if (!t.Ok) return im;

            // Back out of the rotated frame, then take the rotation the body did during the flight
            // off the longitude - the ground moved under us.
            Vector3d ip = new Vector3d(t.Ix, t.Iy, t.Iz);
            Vector3d world = (QuaternionD)Quaternion.Inverse(
                Quaternion.FromToRotation((Vector3)axis, Vector3.forward)) * ip;

            im.LatDeg = b.GetLatitude(b.position + world);
            im.LonDeg = b.GetLongitude(b.position + world)
                      - t.BodyRotationRad * 180.0 / Math.PI;
            while (im.LonDeg < -180.0) im.LonDeg += 360.0;
            while (im.LonDeg > 180.0) im.LonDeg -= 360.0;

            im.TimeToImpactS = t.TimeToImpactS;
            im.SpeedMps = t.ImpactSpeedMps;
            im.Valid = true;
            return im;
        }

        // ------------------------------------------------------------------ map-view trajectory

        /// <summary>The last predicted path for the map overlay: BODY-FIXED positions relative to the
        /// body centre (add body.position for world). Ends at <see cref="MapImpact"/>. Null/empty = none.</summary>
        public static List<Vector3d> MapPath = new List<Vector3d>();
        /// <summary>Predicted ground impact, body-relative world (body-fixed). Valid only with MapValid.</summary>
        public static Vector3d MapImpact;
        /// <summary>The aim point (LZ), body-relative world. Valid only with MapValid.</summary>
        public static Vector3d MapTarget;
        public static CelestialBody MapBody;
        public static bool MapValid;
        /// <summary>UT the map path was last refreshed, for staleness.</summary>
        public static double MapStampUt;

        /// <summary>
        /// Recompute the map-view trajectory for a vessel and cache it in the Map* fields for
        /// <see cref="MapTrajectory"/> to draw. Same integration as Predict, but it collects the flown
        /// path and rotates every point into a BODY-FIXED frame (the ground as it is oriented now), so
        /// the line ends exactly on the impact crosshair rather than tens of km away at the inertial
        /// impact the body has since rotated out from under.
        /// </summary>
        public static void UpdateMapTrajectory(Vessel v, double bcOverride, double tgtLatDeg, double tgtLonDeg)
        {
            if (v == null || v.mainBody == null) { MapValid = false; return; }
            CelestialBody b = v.mainBody;

            // FAR-consistent, matching Predict(): no stock drag-cube table on Earth, fly the measured bc.
            bool far = (b.Radius > 1.0e6);
            double measured = BallisticCoefficient(v);
            AeroTable table = (bcOverride > 0.0 && !far) ? BuildAeroTable(v) : null;
            double useBc = far ? (measured > 0.0 ? measured : bcOverride)
                               : (bcOverride > 0.0 ? bcOverride : measured);

            Vector3d r = v.CoM - b.position;
            Vector3d vel = v.obt_velocity;

            TrajectoryInputs s = new TrajectoryInputs();
            s.Mu = b.gravParameter;
            s.BodyRadiusM = b.Radius;
            s.AtmosphereDepthM = b.atmosphereDepth;
            s.BallisticCoefficient = useBc;
            s.ImpactAltitudeM = 0.0;
            s.BodyOmega = b.angularVelocity.magnitude;
            if (table != null)
            {
                s.DragFactor = table.DragFactor;
                CelestialBody body2 = b;
                s.SoundSpeed = delegate(double alt) { return StockSoundSpeed(body2, alt); };
            }

            // Same rotated (+Z = spin axis) frame as PredictFromState.
            Vector3d axis = b.angularVelocity.normalized;
            if (axis.sqrMagnitude < 0.5) axis = b.transform.up;
            QuaternionD toAxis = (QuaternionD)Quaternion.FromToRotation((Vector3)axis, Vector3.forward);
            QuaternionD fromAxis = (QuaternionD)Quaternion.Inverse(
                Quaternion.FromToRotation((Vector3)axis, Vector3.forward));
            Vector3d rp = toAxis * r, vp = toAxis * vel;
            s.Px = rp.x; s.Py = rp.y; s.Pz = rp.z;
            s.Vx = vp.x; s.Vy = vp.y; s.Vz = vp.z;

            List<PathSample> raw = new List<PathSample>();
            s.Path = raw;

            CelestialBody db = b;
            TrajectoryResult t = Trajectory.Solve(s, delegate(double alt)
            {
                if (alt < 0.0 || alt >= db.atmosphereDepth) return 0.0;
                return StockDensity(db, alt);
            });

            if (!t.Ok || raw.Count < 2) { MapValid = false; return; }

            // Rotate each integrator-frame point back by the body rotation to that instant (body-fixed),
            // then out of the spin-axis frame. RotZ(-rot) about the frame's +Z is the body spin.
            MapPath.Clear();
            for (int i = 0; i < raw.Count; i++)
            {
                PathSample ps = raw[i];
                double c = Math.Cos(-ps.Rot), sn = Math.Sin(-ps.Rot);
                Vector3d deRot = new Vector3d(ps.X * c - ps.Y * sn, ps.X * sn + ps.Y * c, ps.Z);
                MapPath.Add(fromAxis * deRot);
            }
            MapImpact = MapPath[MapPath.Count - 1];
            MapTarget = (Vector3d)b.GetWorldSurfacePosition(tgtLatDeg, tgtLonDeg, 0.0) - b.position;
            MapBody = b;
            MapValid = true;
            MapStampUt = Planetarium.GetUniversalTime();
        }

        /// <summary>
        /// Ground distance from a predicted impact to a target, metres. Negative when the predictor
        /// has no answer - a distinct case from a zero miss, and the de-orbit law relies on that.
        /// </summary>
        public static double MissTo(Vessel v, double latDeg, double lonDeg)
        {
            Impact im = Predict(v);
            if (!im.Valid || v.mainBody == null) return -1.0;
            return BoosterRecovery.GroundRange(v.mainBody, im.LatDeg, im.LonDeg, latDeg, lonDeg);
        }

        private static double LiveThrust(Vessel v)
        {
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (es[m].EngineIgnited && !es[m].flameout) t += es[m].finalThrust;
            }
            return t;
        }

        // ------------------------------------------------------------------ stock aero (Trajectories)

        /// <summary>
        /// Build the Mach-tabulated stock drag for the vessel's ENTRY configuration, shield-forward.
        ///
        /// ---- ⛔ SHIELD-FORWARD, AND THE TRUNK IS NOT COUNTED ----
        /// The airflow direction is taken as −ReferenceTransform.up (the heat-shield direction: the
        /// controller points that at retrograde during entry), transformed into each part's local frame
        /// - which is INVARIANT to how the vessel is pointed right now, so it is correct even while the
        /// de-orbit burn is vectored off retrograde. Trunk and second-stage parts are excluded because
        /// they are jettisoned before entry; the prediction is for the capsule that actually comes down.
        ///
        /// Returns null when nothing can be modelled (no drag-cube parts, or zero mass), and the caller
        /// falls back to the scalar bc.
        /// </summary>
        public static AeroTable BuildAeroTable(Vessel v)
        {
            if (v == null || v.ReferenceTransform == null) return null;
            Vector3 shieldFwdWorld = -v.ReferenceTransform.up;

            List<Part> parts = DockedSide.Ours(v);
            List<Part> cubeParts = new List<Part>();
            List<Vector3> localDirs = new List<Vector3>();
            double mass = 0.0;
            for (int i = 0; i < parts.Count; i++)
            {
                Part p = parts[i];
                if (VehicleParts.IsTrunk(p.name) || VehicleParts.IsSecondStage(p.name)) continue;
                if (p.physicalSignificance != Part.PhysicalSignificance.NONE)
                    mass += p.mass + p.GetResourceMass() + p.GetPhysicslessChildMass();
                if (p.ShieldedFromAirstream || p.Rigidbody == null) continue;
                if (p.dragModel != Part.DragModel.DEFAULT && p.dragModel != Part.DragModel.CUBE) continue;
                DragCubeList cubes = p.DragCubes;
                if (cubes == null || cubes.None) continue;
                cubeParts.Add(p);
                localDirs.Add(p.transform.InverseTransformDirection(shieldFwdWorld));
            }
            if (mass <= 0.0 || cubeParts.Count == 0) return null;

            AeroTable table = new AeroTable(PhysicsGlobals.DragCurvePseudoReynolds);
            // ⚠ TONNES, not kg. SimAeroForce's force is in kN and KSP gets acceleration as kN/tonne
            // (= m/s²), so the mass that divides it here is in tonnes - part.mass already is.
            table.Mass = mass;
            table.DragCubeMult = PhysicsGlobals.DragCubeMultiplier;
            table.DragMult = PhysicsGlobals.DragMultiplier;
            table.AreaDrag = new double[AeroTable.Bins];
            for (int bIdx = 0; bIdx < AeroTable.Bins; bIdx++)
            {
                double mach = AeroTable.MachMax * bIdx / (AeroTable.Bins - 1);
                double sum = 0.0;
                for (int k = 0; k < cubeParts.Count; k++)
                {
                    DragCubeList.CubeData data = new DragCubeList.CubeData();
                    try { cubeParts[k].DragCubes.AddSurfaceDragDirection(localDirs[k], (float)mach, ref data); }
                    catch { continue; }
                    sum += data.areaDrag;
                }
                table.AreaDrag[bIdx] = sum;
            }
            return table;
        }

        /// <summary>
        /// Air density at an altitude, ported from Trajectories.StockAeroUtil.GetDensity - the average
        /// day/night equatorial temperature, so the density (and the Mach number built on it) match the
        /// add-on rather than the bare <c>GetTemperature</c> we used before.
        /// </summary>
        public static double StockDensity(CelestialBody body, double altitude)
        {
            if (body == null || !body.atmosphere || altitude > body.atmosphereDepth || altitude < 0.0)
                return 0.0;
            double pressure = body.GetPressure(altitude);
            const double sunDot = 0.5;
            const float sunAxialDot = 0f;
            double tempOffset = body.latitudeTemperatureBiasCurve.Evaluate(0f)
                              + body.latitudeTemperatureSunMultCurve.Evaluate(0f) * sunDot
                              + body.axialTemperatureSunMultCurve.Evaluate(sunAxialDot);
            double temperature = body.GetTemperature(altitude)
                               + body.atmosphereTemperatureSunMultCurve.Evaluate((float)altitude) * tempOffset;
            return body.GetDensity(pressure, temperature);
        }

        /// <summary>Speed of sound at an altitude, using the same density as SimAeroForce.</summary>
        public static double StockSoundSpeed(CelestialBody body, double altitude)
        {
            if (body == null || !body.atmosphere) return 0.0;
            double pressure = body.GetPressure(altitude);
            double rho = StockDensity(body, altitude);
            if (rho <= 0.0) return 0.0;
            return body.GetSpeedOfSound(pressure, rho);
        }
    }
}
