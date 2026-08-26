// DragonScreen - NodeExecutor
// ---- WHY THIS IS ITS OWN FILE, AND WHY IT WAS BUILT FIRST ----
// ---- ⛔ THE PERIAPSIS FLOOR IS CHECKED HERE, BEFORE IGNITION, ALWAYS ----
// ---- AND IT GOVERNS THE THROTTLE ITSELF ----
using System;
using UnityEngine;

namespace DragonScreen
{
    public enum BurnPhase : byte
    {
        Idle = 0,
        Aligning,
        Holding,
        Burning,
        Done,
        Failed
    }

    public static class NodeExecutor
    {
        private const string Tag = "[DragonScreen] ";

        public static BurnPhase Phase { get; private set; }
        public static string Note = "-";

        public static double RemainingDvMps, InitialDvMps, ThrottleCmd, PointingErrorDeg;

        public static double DeliveredDvMps { get { return InitialDvMps - RemainingDvMps; } }
        public static bool RcsBurn { get { return rcsBurn; } }
        public static double TimeToIgnitionS
        {
            get { return Active ? ignitionUt - Planetarium.GetUniversalTime() : 0.0; }
        }

        private static Vessel ship;
        private static Vector3d dvWorld;
        private static Vector3d dvRemaining;
        private static double nodeUt, ignitionUt, startedBurnAt;
        private static bool rcsWasOn, boughtRcs, warpRequested, warpRefused, orientWarpRequested;

        private static bool rcsBurn;
        private static Vector3d prevObtVel;
        private static double lastProgressDv;

        // ---- FDIR: thrust-delivery monitor (Layer 3 - docs/LAYER3_AUTONOMY_PLAN.md). OBSERVE-ONLY. ----
        private static MonitorState deliveryMon;
        public static HealthVerdict DeliveryVerdict { get { return deliveryMon.Verdict; } }
        public static HealthVerdict DeliveryRaw { get { return deliveryMon.Raw; } }
        public static double DeliveryResidual { get { return deliveryMon.Residual; } }
        public static FaultKind DeliveryFault { get; private set; }

        // ---- ⛔ SELF-CORRECTING RCS TRANSLATION SIGN (flight_0825_163535 diagnosis). ----
        private static double rcsTransSign;
        private static bool rcsSignChecked;
        private static double rcsTransElapsedS;

        public const double RcsSignCheckS = 0.6;
        public const double RcsSignFlipDvMps = -0.10;

        public const double RcsTranslateGateDeg = 25.0;

        public const double WarpArriveLeadS = 6.0;
        public const double WarpWorthwhileS = 12.0;
        public const double OrientLeadS = 600.0;

        public const double SpoolLeadS = 1.5;

        public static bool Active
        {
            get { return Phase == BurnPhase.Aligning || Phase == BurnPhase.Holding
                      || Phase == BurnPhase.Burning; }
        }

        public static bool Begin(Vessel v, Vector3d dv, double atUt, string label, bool useRcs = false)
        {
            if (v == null || dv.sqrMagnitude < 1e-8)
            {
                Note = label + " - nothing to burn";
                Phase = BurnPhase.Failed;
                return false;
            }
            rcsBurn = useRcs;

            string why;
            if (!PeriapsisSafe(v, dv, out why))
            {
                Note = "REFUSED " + label + " - " + why;
                Phase = BurnPhase.Failed;
                Debug.LogWarning(Tag + Note);
                return false;
            }

            ship = v;
            dvWorld = dv;
            dvRemaining = dv;
            InitialDvMps = dv.magnitude;
            nodeUt = atUt;

            double spool = rcsBurn ? 0.0 : SpoolLeadS;
            double half = BurnExec.HalfBurnS(InitialDvMps, v.GetTotalMass(), Thrust(v), spool);
            ignitionUt = atUt - half;

            rcsWasOn = v.ActionGroups[KSPActionGroup.RCS];
            boughtRcs = false;
            warpRequested = false;
            warpRefused = false;
            orientWarpRequested = false;
            rcsTransSign = 1.0; rcsSignChecked = false; rcsTransElapsedS = 0.0;
            deliveryMon = HealthMonitor.Fresh(); DeliveryFault = FaultKind.None;
            Phase = BurnPhase.Aligning;
            Note = label;

            if (TimeWarp.CurrentRateIndex > 0) TimeWarp.SetRate(0, true);

            Debug.Log(Tag + "burn planned: " + label + " - " + InitialDvMps.ToString("F2")
                      + " m/s, ignition in "
                      + (ignitionUt - Planetarium.GetUniversalTime()).ToString("F0")
                      + " s (half-burn lead " + half.ToString("F1") + " s)");
            return true;
        }

        public static void Abort(string why)
        {
            if (!Active) return;
            if (warpRequested && TimeWarp.CurrentRateIndex > 0) TimeWarp.SetRate(0, true);
            Stop();
            Phase = BurnPhase.Failed;
            Note = "ABORTED - " + why;
            Debug.LogWarning(Tag + "burn " + Note);
        }

        public static void Reset()
        {
            Phase = BurnPhase.Idle; ship = null; Note = "-";
            RemainingDvMps = 0.0; InitialDvMps = 0.0; ThrottleCmd = 0.0; PointingErrorDeg = 0.0;
            deliveryMon = HealthMonitor.Fresh(); DeliveryFault = FaultKind.None;
        }

        // ------------------------------------------------------------------ the loop

        public static void Tick()
        {
            if (!Active) return;
            if (ship == null || ship.state == Vessel.State.DEAD) { Abort("vessel lost"); return; }

            double now = Planetarium.GetUniversalTime();

            RemainingDvMps = dvRemaining.magnitude;
            Vector3d aim = dvRemaining.sqrMagnitude > 1e-8 ? dvRemaining : dvWorld;

            // ---- ⛔ WARP TO WITHIN OrientLeadS OF THE NODE BEFORE POINTING AT IT (user, 2026-08-19). ----
            if (Phase == BurnPhase.Aligning && (ignitionUt - now) > OrientLeadS)
            {
                AttitudeController.Ascent.Throttle = 0.0;
                AttitudeController.Ascent.Release(ship);
                PointingErrorDeg = Vector3d.Angle(ship.ReferenceTransform.up, aim.normalized);
                WarpToOrient(now);
                return;
            }

            if (Phase == BurnPhase.Aligning && TimeWarp.CurrentRateIndex > 0) TimeWarp.SetRate(0, true);

            AttitudeController.Ascent.SteerTo(ship, aim.normalized, Vector3d.zero);
            // ---- ⛔ FLY THE CAPSULE AT ITS REAL AGILITY, NOT THE ASCENT RATE CAP (flight_0826_014654). ----
            if (rcsBurn) AttitudeController.Ascent.MaxRateDps = Attitude.CapsuleMaxRateDps;
            PointingErrorDeg = Vector3d.Angle(ship.ReferenceTransform.up, aim.normalized);

            switch (Phase)
            {
                case BurnPhase.Aligning: Align(now); break;
                case BurnPhase.Holding: Hold(now); break;
                case BurnPhase.Burning: Burn(now); break;
            }
        }

        private static void Align(double now)
        {
            AttitudeController.Ascent.Throttle = 0.0;
            double toIgnition = ignitionUt - now;
            bool haveWheels = HaveWheelAuthority(ship);

            if (!boughtRcs && BurnExec.NeedRcsToAlign(toIgnition, PointingErrorDeg, haveWheels))
            {
                boughtRcs = true;
                ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
                Debug.Log(Tag + toIgnition.ToString("F0") + " s to ignition and still "
                          + PointingErrorDeg.ToString("F1") + " deg off - RCS "
                          + (haveWheels ? "assisting the turn" : "turning it (no reaction wheels)"));
            }

            if (BurnExec.Aligned(PointingErrorDeg)
                || (toIgnition <= BurnExec.AlignPadS && PointingErrorDeg < BurnExec.LooseAlignDeg))
            {
                if (boughtRcs && !rcsWasOn && haveWheels) ship.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
                Phase = BurnPhase.Holding;
            }
        }

        private static bool HaveWheelAuthority(Vessel v)
        {
            if (v == null) return false;
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleReactionWheel> ws =
                    v.parts[i].Modules.GetModules<ModuleReactionWheel>();
                for (int m = 0; m < ws.Count; m++)
                {
                    ModuleReactionWheel w = ws[m];
                    if (w.wheelState != ModuleReactionWheel.WheelState.Active) continue;
                    t += (w.PitchTorque + w.YawTorque + w.RollTorque) * (w.authorityLimiter / 100f);
                    if (t >= WheelAuthorityFloorKNm) return true;
                }
            }
            return false;
        }

        private const double WheelAuthorityFloorKNm = 1.0;

        private static void WarpToOrient(double now)
        {
            if (orientWarpRequested) return;
            double target = ignitionUt - OrientLeadS;
            double wait = target - now;
            if (wait < WarpWorthwhileS) return;

            orientWarpRequested = true;
            Debug.Log(Tag + "warping " + wait.ToString("F0") + " s to within "
                      + (OrientLeadS / 60.0).ToString("F0") + " min of '" + Note
                      + "' - controls released, will orient there");
            TimeWarp.fetch.WarpTo(target);
        }

        /// ---- ⛔ WITHOUT THIS THE RETURN IS UNFLYABLE, AND THAT IS NOT AN EXAGGERATION. ----
        private static void WarpToIgnition(double now)
        {
            if (warpRequested) return;
            double lead = ignitionUt - WarpArriveLeadS;
            double wait = lead - now;
            if (wait < WarpWorthwhileS) return;

            // ---- ⛔ A BURN MORE THAN ONE ORBIT AWAY MEANS THE PLAN IS WRONG. ----
            double period = (ship.orbit != null) ? ship.orbit.period : 0.0;
            if (period > 0.0 && wait > period)
            {
                if (!warpRefused)
                {
                    warpRefused = true;
                    Debug.LogWarning(Tag + "NOT warping " + (wait / 60.0).ToString("F1")
                                     + " min to '" + Note + "' - that is more than one orbit ("
                                     + (period / 60.0).ToString("F1") + " min) away. A burn that far "
                                     + "out is a planning error, not a wait. Holding.");
                }
                return;
            }

            warpRequested = true;
            Debug.Log(Tag + "warping " + wait.ToString("F0") + " s to ignition for '"
                      + Note + "'");
            TimeWarp.fetch.WarpTo(lead);
        }

        private static void Hold(double now)
        {
            AttitudeController.Ascent.Throttle = 0.0;

            if (now < ignitionUt - WarpArriveLeadS) { WarpToIgnition(now); return; }

            if (TimeWarp.CurrentRateIndex > 0) TimeWarp.SetRate(0, true);
            if (now < ignitionUt) return;

            startedBurnAt = now;
            prevObtVel = ship.obt_velocity;
            lastProgressDv = RemainingDvMps;
            Phase = BurnPhase.Burning;
            Debug.Log(Tag + "ignition - " + Note + ", " + InitialDvMps.ToString("F2") + " m/s"
                      + (rcsBurn ? " (RCS translation)" : ""));
        }

        private static void Burn(double now)
        {
            BurnState s = new BurnState();
            s.RemainingDvMps = RemainingDvMps;
            s.InitialDvMps = InitialDvMps;
            s.MassT = ship.GetTotalMass();
            s.AvailableThrustKn = Thrust(ship);
            s.PointingErrorDeg = PointingErrorDeg;
            s.ElapsedS = now - startedBurnAt;
            s.Overshot = Vector3d.Dot(dvWorld, dvRemaining) < 0.0;

            if (BurnExec.Complete(s))
            {
                string why = BurnExec.CompletionNote(s);
                Stop();
                Phase = why.Contains("ABORTED") ? BurnPhase.Failed : BurnPhase.Done;
                Debug.Log(Tag + "burn complete - " + Note + ": "
                          + InitialDvMps.ToString("F2") + " m/s commanded, "
                          + RemainingDvMps.ToString("F2") + " m/s residual (" + why + ")");
                return;
            }

            // ---- ⛔ DO NOT THROTTLE UP UNTIL THE NOSE IS ON THE Δv. ----
            bool onAxis = BurnExec.Aligned(PointingErrorDeg);
            double dt = TimeWarp.fixedDeltaTime;

            bool onAxisRcs = PointingErrorDeg < RcsTranslateGateDeg;

            if (rcsBurn)
            {
                // ---- CREW DRAGON: BURN ON DRACO RCS, NOT THE MAIN-ENGINE THROTTLE. ----
                if (!boughtRcs) { ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true); boughtRcs = true; }
                AttitudeController.Ascent.Throttle = 0.0;
                AttitudeController.Ascent.UllageFore = onAxisRcs ? rcsTransSign : 0.0;
                ThrottleCmd = onAxisRcs ? 1.0 : 0.0;
                if (onAxisRcs)
                {
                    AccountByVelocity(dt);
                    if (!rcsSignChecked)
                    {
                        rcsTransElapsedS += dt;
                        if (rcsTransElapsedS >= RcsSignCheckS)
                        {
                            rcsSignChecked = true;
                            double alongDv = Vector3d.Dot(dvWorld.normalized, dvWorld - dvRemaining);
                            if (alongDv < RcsSignFlipDvMps)
                            {
                                rcsTransSign = -rcsTransSign;
                                dvRemaining = dvWorld;
                                prevObtVel = ship.obt_velocity;
                                lastProgressDv = InitialDvMps;
                                Debug.LogWarning(Tag + "RCS translation was INVERTED (delivered "
                                    + alongDv.ToString("F2") + " m/s wrong-way) - flipped the sign, restarting");
                            }
                        }
                    }
                }
            }
            else
            {
                // ---- RealFuels relight: SETTLE ULLAGE UNTIL THE ENGINE LIGHTS. ----
                bool lit = LiveThrust(ship) > 1.0;
                if (onAxis && !lit)
                {
                    if (!boughtRcs) { ship.ActionGroups.SetGroup(KSPActionGroup.RCS, true); boughtRcs = true; }
                    AttitudeController.Ascent.UllageFore = 1.0;
                }
                else AttitudeController.Ascent.UllageFore = 0.0;
                ThrottleCmd = onAxis ? BurnExec.Throttle(s) : 0.0;
                AttitudeController.Ascent.Throttle = ThrottleCmd;
                if (onAxis) AccountForDelivered(dt);
            }

            // ---- PROGRESS-BASED BACKSTOP. The runaway timer counts time since the residual LAST FELL,
            bool effectiveOnAxis = rcsBurn ? onAxisRcs : onAxis;
            bool progressed = RemainingDvMps < lastProgressDv - 0.01;
            if (progressed) lastProgressDv = RemainingDvMps;
            if (progressed || !effectiveOnAxis) startedBurnAt = now;

            // ---- FDIR SAMPLE (observe-only). Expected vs delivered along-axis accel this tick. ----
            double massNow = ship.GetTotalMass();
            ThrustSample sample = new ThrustSample();
            sample.DeliveredAccel = (dt > 0.0) ? (RemainingDvMps - dvRemaining.magnitude) / dt : 0.0;
            if (rcsBurn)
            {
                sample.Commanding = onAxisRcs;
                sample.ExpectedAccel = (onAxisRcs && massNow > 0.0) ? RcsTranslationThrust(ship) / massNow : 0.0;
            }
            else
            {
                sample.Commanding = onAxis && ThrottleCmd > 0.0;
                sample.ExpectedAccel = (sample.Commanding && massNow > 0.0) ? ThrottleCmd * Thrust(ship) / massNow : 0.0;
            }
            deliveryMon = ThrustDeliveryMonitor.Step(deliveryMon, sample, dt);
            DeliveryFault = ThrustDeliveryMonitor.Kind(sample, deliveryMon.Verdict);
        }

        private static void Stop()
        {
            ThrottleCmd = 0.0;
            if (ship != null)
            {
                AttitudeController.Ascent.Throttle = 0.0;
                AttitudeController.Ascent.UllageFore = 0.0;
                AttitudeController.Ascent.Release(ship);
                if (boughtRcs && !rcsWasOn) ship.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
            }
            rcsBurn = false;
        }

        private static void AccountForDelivered(double dt)
        {
            if (dt <= 0.0 || ship == null) return;
            double thrust = LiveThrust(ship);
            if (thrust <= 0.0) return;

            double mass = ship.GetTotalMass();
            if (mass <= 0.0) return;

            double dv = thrust / mass * dt;
            dvRemaining -= (Vector3d)ship.ReferenceTransform.up * dv;
        }

        private static void AccountByVelocity(double dt)
        {
            if (dt <= 0.0 || ship == null || ship.mainBody == null) return;
            Vector3d nowVel = ship.obt_velocity;
            Vector3d r = ship.CoM - ship.mainBody.position;
            double rm = r.magnitude;
            if (rm > 1.0)
            {
                Vector3d grav = -ship.mainBody.gravParameter / (rm * rm * rm) * r;
                Vector3d dvThrust = (nowVel - prevObtVel) - grav * dt;
                dvRemaining -= dvThrust;
            }
            prevObtVel = nowVel;
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

        private static double Thrust(Vessel v)
        {
            if (rcsBurn) return RcsTranslationThrust(v);

            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleEngines> es =
                    v.parts[i].Modules.GetModules<ModuleEngines>();
                for (int m = 0; m < es.Count; m++)
                    if (!es[m].flameout && es[m].isEnabled) t += es[m].MaxThrustOutputVac(true);
            }
            return t;
        }

        private static double RcsTranslationThrust(Vessel v)
        {
            double t = 0.0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                System.Collections.Generic.List<ModuleRCS> rs =
                    v.parts[i].Modules.GetModules<ModuleRCS>();
                for (int m = 0; m < rs.Count; m++)
                {
                    ModuleRCS rcs = rs[m];
                    if (!rcs.isEnabled || rcs.isJustForShow || rcs.flameout) continue;
                    int nz = (rcs.thrusterTransforms != null && rcs.thrusterTransforms.Count > 0)
                             ? rcs.thrusterTransforms.Count : 1;
                    t += rcs.thrusterPower * (rcs.thrustPercentage * 0.01) * nz;
                }
            }
            return t * RcsAlignFraction;
        }

        public const double RcsAlignFraction = 0.4;

        public static bool PeriapsisSafe(Vessel v, Vector3d dv, out string why)
        {
            why = "";
            CelestialBody b = v.mainBody;
            if (b == null) { why = "no body"; return false; }

            Vector3d r = v.CoM - b.position;
            Vector3d vel = v.obt_velocity + dv;
            double rm = r.magnitude;
            double mu = b.gravParameter;
            if (rm <= 0.0 || mu <= 0.0) { why = "degenerate state"; return false; }

            double energy = vel.sqrMagnitude / 2.0 - mu / rm;
            if (energy >= 0.0) { why = "it is an escape trajectory"; return false; }

            double sma = -mu / (2.0 * energy);
            Vector3d h = Vector3d.Cross(r, vel);
            double ecc = Math.Sqrt(Math.Max(0.0, 1.0 - h.sqrMagnitude / (sma * mu)));
            double peri = sma * (1.0 - ecc) - b.Radius;

            if (peri < b.atmosphereDepth)
            {
                why = "it would leave periapsis at " + (peri / 1000.0).ToString("F1")
                    + " km, below the " + (b.atmosphereDepth / 1000.0).ToString("F1") + " km floor";
                return false;
            }
            return true;
        }
    }
}
