/*
 * DragonScreen - FlightCommands
 *
 * GLUE. What each console button actually does to the vessel.
 *
 * ---- EVERY ACTION HERE IS REAL KSP STATE, OR IT REFUSES ----
 * The rule the whole project runs on applies hardest to a control: a button that reports success
 * without doing anything is worse than one that lights red. So each command finds the module it needs
 * and returns FALSE if it is not there - the dash goes red, and the log says which module was missing.
 * Nothing here pretends.
 *
 * ---- WHAT WAS CHECKED IN THE PART CONFIGS, NOT ASSUMED ----
 * `TundraExploration/Parts/RodanV2/TE_CD2_POD.cfg`:
 *
 *      ModuleAnimateGeneric  animationName = TE_23_CD2_NOSECONE_ANI
 *      ModuleCargoBay        DeployModuleIndex = 0
 *
 * So the nose cone is HINGED AND ANIMATED, NOT JETTISONED. That settles the open question in
 * CLAUDE.md: `JETTISON NOSE CONE` toggles the animation. It does not decouple anything, and wiring it
 * to a decoupler would have destroyed the cone on a vehicle that is supposed to close it again before
 * entry.
 *
 * Drogues and mains are SEPARATE PARTS - `TE_CD2_POD_DROGUES.cfg` and `TE_CD2_POD_MAINS.cfg`, on
 * `node_drogue` and `node_mains` - each carrying its own ModuleParachute. That is what makes
 * MAINS ONLY / DROGUES & MAINS / CUT MAINS distinguishable rather than three names for one action.
 *
 * ---- DEORBIT NOW IS NO LONGER THE CRUDE ONE ----
 * It used to point retrograde and burn until a periapsis number was met, with no idea where the
 * capsule would land. It now hands off to `DeorbitOps`, which flies the AIM MISS - how far the
 * predicted impact is from the landing zone - against an integrated trajectory that measures the
 * capsule's own drag, with the periapsis target demoted to a depth limit. It refuses if the second
 * stage is attached or there is no orbit to leave, and reports the monopropellant budget before it
 * commits.
 *
 * WATER DEORBIT and BREAKOUT are still the crude form and still say so on ignition.
 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class FlightCommands
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>Latched modes the panel owns. Real state, just ours rather than KSP's.</summary>
        public static bool BackupPyros, EntryReboot, BackupEntry;

        /// <summary>
        /// Launch escape system armed. A real countdown milestone (T-38:00 on Crew-10) and a real
        /// crew-adjacent state, so FLIGHT's sequence reads it and the abort mode depends on it.
        /// Armed by default because the vehicle spawns with its abort staging live.
        /// </summary>
        public static bool EscapeArmed = true;

        /// <summary>
        /// The simulated systems the console drives - power strings, consumables, fire and leak.
        /// One instance for the vehicle, so both emergency plates and all three screens agree.
        /// </summary>
        public static SystemsState State = SystemsState.Fresh();

        /// <summary>Charge fraction, refreshed by VesselData. RESET needs it to judge bus health.</summary>
        public static double Charge01;

        private static bool Reset(int bus)
        {
            if (!Systems.ResetBus(ref State, bus, Charge01))
            {
                Log("RESET " + bus + " refused - charge " + (Charge01 * 100.0).ToString("F0")
                    + "%, need " + (Systems.ResetCharge * 100.0).ToString("F0")
                    + "%, or nothing was tripped");
                return false;
            }
            Log("RESET " + bus + " - strings restored");
            return true;
        }

        /// <summary>
        /// A STRING row only responds once its POWER bus is on - the crew powers the flight-computer
        /// group before its computers will engage, the way the real STRING buttons need their bus. A
        /// refused press returns false (a red flash) and says which POWER button to press. The engage
        /// LAMPS are not gated this way: a phase started from the touchscreen still lights its string.
        /// </summary>
        private static bool Powered(int bus)
        {
            bool on = (bus == 1) ? State.Bus1On : State.Bus2On;
            if (!on) Log("STRING row " + bus + " unpowered - press POWER " + bus + " first");
            return on;
        }

        /// <summary>
        /// STRING 1B: one button for the whole middle of the mission. Off -> `MissionOps.AutoDock`
        /// (far = rendezvous, close = dock). On -> disengage BOTH, because the lamp is lit for either
        /// phase and a mode button the crew cannot switch off is worse than no lamp. AutoDock's own
        /// toggle only cancels the docking servo, so the rendezvous leg is disengaged here.
        /// </summary>
        private static bool ToggleRendezvousDock()
        {
            if (StationApproach.Engaged || DockingOps.Engaged)
            {
                if (DockingOps.Engaged) DockingOps.Reset();
                if (StationApproach.Engaged) StationApproach.Disengage("crew");
                Log("RENDEZVOUS/DOCK disengaged (STRING 1B)");
                return true;
            }
            MissionOps.AutoDock();
            return true;
        }

        // ---- STRING re-init (row 2 of the grid): force-restart a phase's flight computer ----
        // "Reinitialise the backup flight computer" for the real STRING buttons; here it force-restarts
        // the phase autopilot - disengage if running, then engage fresh - so a faulted phase can be
        // recovered without waiting for it to give up. Each returns true (the press was accepted); the
        // underlying engage logs its own outcome and posts a refusal to the screen if it will not run.
        private static bool ReinitAscent()
        {
            if (AutoPilot.Engaged) AutoPilot.Disengage("string re-init");
            AutoPilot.Engage();
            Log("ASCENT computer re-initialised (STRING 2A)");
            return true;
        }

        private static bool ReinitRendezvous()
        {
            if (DockingOps.Engaged) DockingOps.Reset();
            if (StationApproach.Engaged) StationApproach.Disengage("string re-init");
            MissionOps.AutoDock();
            Log("RENDEZVOUS/DOCK computer re-initialised (STRING 2B)");
            return true;
        }

        private static bool ReinitDeorbit()
        {
            if (DeorbitOps.Engaged) DeorbitOps.Disengage("string re-init");
            DeorbitOps.Engage();
            Log("DEORBIT/LAND computer re-initialised (STRING 2C)");
            return true;
        }

        /// <summary>Periapsis a deorbit burn aims for, metres. Low enough to guarantee capture.</summary>
        private const double DeorbitTargetPe = 25000.0;

        /// <summary>WATER DEORBIT aims shallower - a longer, gentler arc onto an ocean track.</summary>
        private const double WaterDeorbitTargetPe = 40000.0;

        // ------------------------------------------------------------------ dispatch

        /// <summary>
        /// Carry out a command. Returns false when the vehicle cannot do it, which lights the dash
        /// red - the caller never has to decide what "worked" means.
        /// </summary>
        public static bool Run(PanelCommand c)
        {
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null) return false;

            try
            {
                switch (c)
                {
                    case PanelCommand.JettisonNoseCone: return ToggleNoseCone(v);
                    case PanelCommand.MainsOnly:        return Deploy(v, mains: true, drogues: false);
                    case PanelCommand.DroguesAndMains:  return Deploy(v, mains: true, drogues: true);
                    case PanelCommand.CutMains:         return CutChutes(v);
                    case PanelCommand.FirePyro:         return FirePyros(v);

                    case PanelCommand.EnableBackupPyros:
                        BackupPyros = !BackupPyros;
                        Log("backup pyros " + (BackupPyros ? "ENABLED" : "disabled"));
                        return true;

                    case PanelCommand.EnableEntryReboot:
                        EntryReboot = !EntryReboot;
                        Log("entry reboot " + (EntryReboot ? "ENABLED" : "disabled"));
                        return true;

                    case PanelCommand.EnableBackupEntry:
                        BackupEntry = true;  Log("entry mode BACKUP"); return true;
                    case PanelCommand.EnableNormalEntry:
                        BackupEntry = false; Log("entry mode NORMAL"); return true;

                    // ---- POWER STRINGS ----
                    // These used to log "no KSP system behind it" and do nothing. Stock KSP has no
                    // redundant string architecture - so we SIMULATE one, driven by the electric
                    // charge that is real. See VehicleSystems, and the rule it cites.
                    case PanelCommand.Power1: Systems.ToggleBus(ref State, 1);
                        Log("bus 1 " + (State.Bus1On ? "ON" : "OFF")); return true;
                    case PanelCommand.Power2: Systems.ToggleBus(ref State, 2);
                        Log("bus 2 " + (State.Bus2On ? "ON" : "OFF")); return true;

                    case PanelCommand.Reset1: return Reset(1);
                    case PanelCommand.Reset2: return Reset(2);

                    // ---- FLIGHT COMPUTER STRINGS (repurposed 2026-08-21) ----
                    // The real Crew Dragon STRING buttons reinitialise the redundant flight computers
                    // when a system faults. OURS are flight computers - the phase autopilots - so the
                    // grid is columns = phase, rows = job (user's mapping, 2026-08-21):
                    //   col A = ASCENT   col B = RENDEZVOUS/DOCK   col C = DEORBIT + LAND
                    //   row 1 (1A/1B/1C) ENGAGE the phase (toggle - a second press disengages)
                    //   row 2 (2A/2B/2C) RE-INITIALISE it (force disengage + re-engage - recover a
                    //                    computer that faulted mid-phase)
                    // The old power-string SIMULATION these used to drive is retired from these
                    // buttons; POWER 1/2 and RESET 1/2 still run the bus model (auto-trip + recover).
                    case PanelCommand.String1A: if (!Powered(1)) return false;
                        AutoPilot.Toggle();  return true;                          // ASCENT
                    case PanelCommand.String1B: if (!Powered(1)) return false;
                        return ToggleRendezvousDock();                            // RENDEZVOUS/DOCK
                    case PanelCommand.String1C: if (!Powered(1)) return false;
                        DeorbitOps.Toggle(); return true;                         // DEORBIT + LAND
                    case PanelCommand.String2A: if (!Powered(2)) return false; return ReinitAscent();
                    case PanelCommand.String2B: if (!Powered(2)) return false; return ReinitRendezvous();
                    case PanelCommand.String2C: if (!Powered(2)) return false; return ReinitDeorbit();

                    // ---- CABIN EMERGENCIES ----
                    // Also previously inert. A fire needs a part genuinely near its temperature
                    // limit and a leak needs the structure genuinely overstressed, so these refuse
                    // when there is nothing to fight - which is the honest behaviour, and it means a
                    // red dash on a quiet cabin means "no emergency", not "broken button".
                    case PanelCommand.DepressResponse:
                        if (!Systems.DepressResponse(ref State)) { Log("no leak to isolate"); return false; }
                        Log("DEPRESS RESPONSE - cabin isolating"); return true;

                    case PanelCommand.SuppressFire:
                        if (!Systems.SuppressFire(ref State))
                        { Log("no fire, or suppressant spent"); return false; }
                        Log("SUPPRESS FIRE - bottle discharged, "
                            + (State.Suppressant * 100.0).ToString("F0") + "% left"); return true;

                    case PanelCommand.FireResponse:
                        if (!Systems.FireResponse(ref State)) { Log("no fire to respond to"); return false; }
                        Log("FIRE RESPONSE - bus 2 shed, suppressant discharged"); return true;

                    case PanelCommand.SwapString1:
                    case PanelCommand.SwapString2:
                    case PanelCommand.SwapString3:
                        Log("entry string swap - undecided function, see REAL_DRAGON_SCREENS.md");
                        return true;

                    // ---- ⛔ THE REAL DE-ORBIT, NOT THE OLD RETROGRADE BURN ----
                    // StartDeorbit drove periapsis to a number and had no idea where the capsule
                    // would land. DeorbitOps flies the AIM MISS - how far the predicted impact is
                    // from the landing zone - with the periapsis target demoted to a depth limit.
                    // It also refuses if the S2 is attached or there is no orbit to leave, and
                    // reports the monopropellant budget before it commits.
                    // ⛔ TOGGLE, NOT ENGAGE. On 2026-08-11 the crew pressed this to escape a
                    // runaway rendezvous and got "Press ignored" - the idempotency guard added that
                    // morning had removed the only abort the cockpit has. A second press cancels.
                    case PanelCommand.DeorbitNow:   DeorbitOps.Toggle(); return true;
                    case PanelCommand.WaterDeorbit: return StartDeorbit(v, WaterDeorbitTargetPe, "WATER DEORBIT");
                    case PanelCommand.Breakout:     return Breakout(v);
                    case PanelCommand.Abort:        return Abort(v);
                }
            }
            catch (Exception e)
            {
                // A throw out of a button handler must not take the IVA with it. Red dash, logged.
                Debug.LogError(Tag + "command " + c + " threw: " + e);
                return false;
            }

            return false;
        }

        // ------------------------------------------------------------------ mechanisms

        /// <summary>
        /// Toggle the hinged nose cone. Found by ANIMATION NAME, not by module index: the pod carries
        /// several ModuleAnimateGeneric-shaped things and B9PartSwitch can reorder modules, so an
        /// index would be a silent mis-fire waiting for a part update.
        /// </summary>
        private static bool ToggleNoseCone(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleAnimateGeneric> mods = v.parts[i].Modules.GetModules<ModuleAnimateGeneric>();
                for (int m = 0; m < mods.Count; m++)
                {
                    if (mods[m].animationName != "TE_23_CD2_NOSECONE_ANI") continue;
                    mods[m].Toggle();
                    Log("nose cone toggled -> " + (mods[m].Progress > 0.5f ? "OPEN" : "CLOSED"));
                    return true;
                }
            }
            Log("no nose cone animation on this vessel - JETTISON NOSE CONE refused");
            return false;
        }

        /// <summary>
        /// Deploy chutes. Drogues and mains are separate parts, told apart by which of the pod's
        /// nodes they hang on - `minAirPressure` distinguishes them in stock terms, so the DROGUE is
        /// the one that will deploy higher.
        /// </summary>
        private static bool Deploy(Vessel v, bool mains, bool drogues)
        {
            int fired = 0;
            List<ModuleParachute> all = Parachutes(v);
            for (int i = 0; i < all.Count; i++)
            {
                ModuleParachute p = all[i];
                bool isDrogue = p.part != null && p.part.name.IndexOf("DROGUE",
                                    StringComparison.OrdinalIgnoreCase) >= 0;
                if (isDrogue && !drogues) continue;
                if (!isDrogue && !mains) continue;
                if (p.deploymentState == ModuleParachute.deploymentStates.DEPLOYED
                    || p.deploymentState == ModuleParachute.deploymentStates.SEMIDEPLOYED) continue;

                p.Deploy();
                fired++;
            }
            Log("chutes deployed: " + fired + " (mains=" + mains + " drogues=" + drogues + ")");
            return fired > 0;
        }

        /// <summary>
        /// CUT MAINS. The model itself carries the warning `USE ONLY AFTER LANDING`, so this cuts
        /// only what is actually out - cutting nothing is a refusal, not a silent success.
        /// </summary>
        private static bool CutChutes(Vessel v)
        {
            int cut = 0;
            List<ModuleParachute> all = Parachutes(v);
            List<string> states = new List<string>();
            for (int i = 0; i < all.Count; i++)
            {
                ModuleParachute p = all[i];
                states.Add(p.deploymentState.ToString());
                if (p.deploymentState != ModuleParachute.deploymentStates.DEPLOYED
                    && p.deploymentState != ModuleParachute.deploymentStates.SEMIDEPLOYED) continue;
                p.CutParachute();
                cut++;
            }
            // ---- SAY WHY, NOT JUST NO ----
            // The first flight logged a bare "chutes cut: 0" after MAINS ONLY had just reported
            // success, which reads like a contradiction. It is not: arming a chute leaves it ACTIVE,
            // and an ACTIVE canopy has nothing to cut. Printing the states makes that self-evident
            // instead of something to work out from two log lines a minute apart.
            Log("chutes cut: " + cut + " of " + all.Count
                + " [" + string.Join(", ", states.ToArray()) + "]");
            return cut > 0;
        }

        private static List<ModuleParachute> Parachutes(Vessel v)
        {
            List<ModuleParachute> found = new List<ModuleParachute>();
            for (int i = 0; i < v.parts.Count; i++)
                found.AddRange(v.parts[i].Modules.GetModules<ModuleParachute>());
            return found;
        }

        /// <summary>
        /// FIRE PYRD - the trunk separation pyro. Fires the decoupler that actually drops the trunk.
        ///
        /// ⚠ THE DRAGON HAS TWO DECOUPLERS AND THEY DO DIFFERENT THINGS. The S2 decoupler drops the
        /// upper stage and LEAVES THE TRUNK ATTACHED; only the trunk decoupler takes the trunk. This
        /// bit an earlier project and the wrong one here would strand a capsule with its trunk on
        /// through entry, so match on the part rather than taking the first decoupler found.
        /// </summary>
        private static bool FirePyros(Vessel v)
        {
            for (int i = 0; i < v.parts.Count; i++)
            {
                Part p = v.parts[i];
                if (!VehicleParts.IsTrunk(p.name)) continue;

                List<ModuleDecouple> ds = p.Modules.GetModules<ModuleDecouple>();
                for (int m = 0; m < ds.Count; m++)
                {
                    if (ds[m].isDecoupled) continue;
                    ds[m].Decouple();
                    Log("trunk pyro fired on '" + p.name + "'");
                    return true;
                }
            }
            Log("no undecoupled trunk decoupler found - FIRE PYRD refused");
            return false;
        }

        /// <summary>
        /// BREAKOUT - get away from the stack now. This is the abort action group, which is what the
        /// craft's own abort staging is built to do, plus full throttle on whatever that leaves lit.
        /// </summary>
        private static bool Breakout(Vessel v)
        {
            v.ActionGroups.SetGroup(KSPActionGroup.Abort, true);
            FlightInputHandler.state.mainThrottle = 1f;
            Log("BREAKOUT - abort action group fired, throttle full");
            return true;
        }

        /// <summary>The EJECT handle. Pull and twist on the real capsule; one click here.</summary>
        private static bool Abort(Vessel v)
        {
            v.ActionGroups.SetGroup(KSPActionGroup.Abort, true);
            Log("ABORT handle - abort action group fired");
            return true;
        }

        // ------------------------------------------------------------------ deorbit

        /// <summary>Target periapsis of the burn now running, or 0 when idle.</summary>
        public static double BurnTargetPe { get; private set; }

        /// <summary>True while a deorbit burn is being flown.</summary>
        public static bool BurnActive { get { return BurnTargetPe > 0.0; } }

        private static bool StartDeorbit(Vessel v, double targetPe, string what)
        {
            if (v.situation == Vessel.Situations.LANDED
                || v.situation == Vessel.Situations.SPLASHED
                || v.situation == Vessel.Situations.PRELAUNCH)
            {
                Log(what + " refused - on the ground");
                return false;
            }
            if (v.orbit == null || v.orbit.PeA <= targetPe)
            {
                Log(what + " refused - periapsis is already at or below "
                    + (targetPe / 1000.0).ToString("F0") + " km");
                return false;
            }

            // ---- ⛔ NOT WITH THE SECOND STAGE STILL ON. ----
            // The 21:01 flight reached orbit as a 20.5 t stack because nothing ever dropped the S2,
            // and de-orbiting that does not work at any burn size: the burn is sized for a capsule,
            // and entry needs the heat shield forward, which a capsule with a spent tank bolted to
            // its nose will not hold. Refuse and say which decoupler is needed rather than firing a
            // burn that cannot end well.
            for (int i = 0; i < v.parts.Count; i++)
            {
                if (!VehicleParts.IsSecondStage(v.parts[i].name)) continue;
                Log(what + " REFUSED - the second stage is still attached. It should have gone at "
                    + "the 40 km periapsis gate during ascent (see AutoPilot.SeparateSecondStage). "
                    + "Drop it on the '" + VehicleParts.DragonDecouplerMarker + "' first.");
                return false;
            }

            // ---- THE TRUNK GOES BEFORE THE BURN, WHICH IS THE REAL PROFILE ----
            // Crew Dragon jettisons the trunk shortly before the de-orbit burn - it carries the
            // solar arrays and radiators and is not built to survive entry. Doing it here rather
            // than leaving it to the crew means the burn is sized against the mass that will
            // actually fly it, and it is announced rather than silent.
            if (FirePyros(v)) Log(what + " - trunk jettisoned before the burn");
            else Log(what + " - no trunk to jettison (already gone)");

            BurnTargetPe = targetPe;
            // ---- ONE CONTROLLER AT A TIME ----
            // The panel deorbit uses stock SAS RETROGRADE mode, which SAS is genuinely good at -
            // it is holding a fixed marker, not tracking a moving guidance vector. But the ascent
            // controller switches SAS OFF and drives the axes itself, so if it is still attached
            // the two fight over pitch/yaw/roll. Hand the axes back before asking SAS for them.
            // Whichever controller has this vessel - normally Ascent, but a deorbit could in
            // principle be commanded on a stage the recovery is flying, and releasing the wrong one
            // would leave the real one still driving the axes.
            AttitudeController held = AttitudeController.For(v);
            if (held != null) held.Release(v);
            v.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
            Log(what + " ignition, target Pe " + (targetPe / 1000.0).ToString("F0")
                + " km. ⚠ INTERIM GUIDANCE: retrograde burn to periapsis, not the entry solution in "
                + "docs/FLIGHT_SOFTWARE_PLAN.md.");
            return true;
        }

        /// <summary>
        /// Fly the burn. Called every fixed update by the monitor - the ONE place that ticks, so
        /// three screens cannot each fly their own copy of it.
        /// </summary>
        private static int lastTickFrame = -1;

        public static void Tick()
        {
            // Called once per screen; must run once per frame. Same guard, and same reason, as
            // NavBallRenderer.Orient and VesselData - three screens must agree, and here they would
            // also fight over the throttle.
            if (Time.frameCount == lastTickFrame) return;
            lastTickFrame = Time.frameCount;

            if (!BurnActive) return;

            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.orbit == null) { StopBurn("no vessel"); return; }

            if (v.orbit.PeA <= BurnTargetPe) { StopBurn("target periapsis reached"); return; }

            // Retrograde by SAS rather than by writing FlightCtrlState. Our own attitude controller
            // is step 4 of the flight-software plan and does not exist yet; borrowing the stock
            // autopilot until it does is honest, and it means this burn does not have to be unpicked
            // when the real controller lands - only its steering source changes.
            v.Autopilot.SetMode(VesselAutopilot.AutopilotMode.Retrograde);

            // ---- VARIABLE THRUST, NOT BANG-BANG ----
            // This was `align > 0.95 ? 1 : 0` - full throttle or nothing, which overshoots the
            // target periapsis between ticks and cannot be taken back, because the engines only
            // push one way. F9I throttles on the ERROR instead (`dragon_deorbit.ks:1710`):
            // periapsis error over a 30 km span, clamped to 0.02 .. 0.70. The ceiling is what keeps
            // the burn shortenable and the floor is what keeps it closing.
            //
            // Alignment now SCALES the throttle rather than gating it, so the capsule eases in as it
            // finishes slewing instead of slamming on at the moment it crosses a threshold.
            Vector3d retro = -v.obt_velocity.normalized;
            double align = Vector3d.Dot(v.ReferenceTransform.up.normalized, retro);
            if (align <= 0.7) { FlightInputHandler.state.mainThrottle = 0f; return; }

            double peErr = v.orbit.PeA - BurnTargetPe;
            double th = Deorbit.BurnThrottle(peErr, 0.0);
            // Ease over the last 30 degrees of the slew: (align-0.7)/0.3.
            double ease = (align - 0.7) / 0.3;
            if (ease > 1.0) ease = 1.0;
            FlightInputHandler.state.mainThrottle = (float)(th * ease);
        }

        public static void StopBurn(string why)
        {
            if (!BurnActive) return;
            BurnTargetPe = 0.0;
            FlightInputHandler.state.mainThrottle = 0f;
            Log("burn ended - " + why);
        }

        private static void Log(string s) { Debug.Log(Tag + "cmd: " + s); }
    }
}
