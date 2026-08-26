/*
 * DragonScreen - VehicleControl
 *
 * GLUE. The one home for DIRECT-HANDLE actuation - driving parts by the exact BaseEvent / BaseField the
 * craft dump (CraftDump -> craftdump.csv) shows the vehicle really exposes, instead of the coarse
 * action-group / staging path. "Perfect control over the vehicle" means the autopilot reaches for the
 * same handle a human would in the right-click menu, by capability, never by part name.
 *
 * ---- WHAT IS ALREADY DIRECT (this file UNIFIES the primitives; it does not replace working code) ----
 *   Engines        BoosterRecovery drives the octaweb's three ModuleEnginesRF by engineID (Activate /
 *                  Shutdown); PodEngines lights the SuperDracos by their own action. Direct.
 *   Capsule RCS    CapsuleRcs dials ModuleRCS.thrustPercentage per task (docking gentle, burns full).
 *                  Direct - this file now provides the primitive it uses (SetRcsThrust).
 *   Decouplers     AutoPilot / EntryOps fire the specific decoupler's Decouple() - not staging. Direct.
 *
 * ---- WHAT THIS ADDS (new levers the dump revealed, not previously controlled) ----
 *   Gimbal         ModuleGimbal.gimbalLimiter (0..100 "Gimbal Limit") and gimbalLock - tune engine
 *                  steering authority per phase, or lock it.
 *   Fin authority  the grid fins' control-surface authorityLimiter (0..100) - tune aero authority.
 *   Decouple       a by-capability Decouple(part) primitive (fire the part's own "Decouple" event),
 *                  for callers that want one home for it.
 *
 * The handle NAMES are the dump's, verified against this exact craft:
 *   ModuleRCS.thrustPercentage   FloatRange 0..100  ("Thrust Limiter")
 *   ModuleGimbal.gimbalLimiter   FloatRange 0..100  ("Gimbal Limit")
 *   ModuleGimbal.gimbalLock      Toggle             ("Gimbal")
 *   authorityLimiter             FloatRange 0..100  ("Authority Limiter", on the control-surface module)
 *   <decoupler>.Events["Decouple"]                  ("Decouple")
 *
 * Everything here is by-capability (find the module that HAS the field/event), so it survives a part
 * rename or a second vehicle variant - the project's detect-by-capability rule.
 */
using System.Collections.Generic;
using UnityEngine;

namespace DragonScreen
{
    public static class VehicleControl
    {
        private const string Tag = "[DragonScreen] ";

        // ------------------------------------------------------------------ RCS thrust limit

        /// <summary>
        /// Set ModuleRCS.thrustPercentage (0..100 - the dump's "Thrust Limiter") on every RCS module in
        /// <paramref name="parts"/>. The dynamic per-task RCS strength: gentle for docking, full for a
        /// burn. Clamped to the field's 0..100 range. Returns the number of thrusters set. Callers pass
        /// the part set they own (e.g. DockedSide.Ours for the capsule) so this never touches the
        /// station's or another vessel's RCS.
        /// </summary>
        public static int SetRcsThrust(List<Part> parts, double pct)
        {
            if (parts == null) return 0;
            float p = Clamp01to100(pct);
            int n = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == null) continue;
                List<ModuleRCS> rcss = parts[i].Modules.GetModules<ModuleRCS>();
                for (int k = 0; k < rcss.Count; k++) { rcss[k].thrustPercentage = p; n++; }
            }
            return n;
        }

        // ------------------------------------------------------------------ gimbal

        /// <summary>
        /// Set ModuleGimbal.gimbalLimiter (0..100 - the dump's "Gimbal Limit") on every gimbal of a
        /// vessel: how much of the engine's physical gimbal range the controller may command. 100 = full
        /// authority (the default and the right value for a precision landing burn); lower eases the
        /// steering (e.g. to not fight the airflow at max Q). Returns the number of gimbals set.
        /// </summary>
        public static int SetGimbalLimit(Vessel v, double pct)
        {
            if (v == null) return 0;
            float p = Clamp01to100(pct);
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleGimbal> gs = v.parts[i].Modules.GetModules<ModuleGimbal>();
                for (int k = 0; k < gs.Count; k++) { gs[k].gimbalLimiter = p; n++; }
            }
            return n;
        }

        /// <summary>Lock or free every gimbal on the vessel (ModuleGimbal.gimbalLock). Returns the count.</summary>
        public static int SetGimbalLock(Vessel v, bool locked)
        {
            if (v == null) return 0;
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleGimbal> gs = v.parts[i].Modules.GetModules<ModuleGimbal>();
                for (int k = 0; k < gs.Count; k++) { gs[k].gimbalLock = locked; n++; }
            }
            return n;
        }

        // ------------------------------------------------------------------ control-surface (grid fin) authority

        /// <summary>
        /// Set the control-surface authority limiter (0..100 - the dump's "Authority Limiter") on every
        /// control surface of a vessel - the grid fins. By field name, because the module class varies
        /// (stock ModuleControlSurface, FAR's wrapper, Tundra's SyncModuleControlSurface all expose the
        /// same-named field). Returns the number set.
        /// </summary>
        public static int SetFinAuthority(Vessel v, double pct)
        {
            if (v == null) return 0;
            float p = Clamp01to100(pct);
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                for (int m = 0; m < v.parts[i].Modules.Count; m++)
                    if (SetField(v.parts[i].Modules[m], "authorityLimiter", p)) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ radiators

        /// <summary>
        /// Activate or shut down every ModuleActiveRadiator on the vessel (the trunk radiator - the dump's
        /// "Activate Radiator" / "Shutdown Radiator" events). Real Crew Dragon runs its radiators in orbit
        /// for thermal control; the autopilot asserts that state so the part is not left to a default.
        /// Returns the count actuated.
        /// </summary>
        public static int SetRadiators(Vessel v, bool on)
        {
            if (v == null) return 0;
            string ev = on ? "Activate" : "Shutdown";
            int n = 0;
            for (int i = 0; i < v.parts.Count; i++)
            {
                List<ModuleActiveRadiator> rs = v.parts[i].Modules.GetModules<ModuleActiveRadiator>();
                for (int k = 0; k < rs.Count; k++)
                    if (InvokeEvent(rs[k], ev)) n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ CoM shifter (lifting-entry trim)

        /// <summary>
        /// The Dragon's AdjustableCoMShifter "Descent Mode" - the CoM offset (dump: DescentModeCoM
        /// (0,0,0.2)) that PASSIVELY trims the capsule to an entry angle of attack, the real Crew Dragon
        /// lifting-entry technique. Toggled through the part's own "ToggleMode" event, and only when the
        /// current state (IsDescentMode field) differs from what is wanted, so it is idempotent.
        ///
        /// ⚠ NOT wired into the entry yet: the current EntryGuidance already flies a lifting entry by
        /// ACTIVELY steering the AoA, so enabling this passive trim on top would fight it. This is the
        /// handle for the real passive-trim + bank upgrade; enabling it means reworking the entry
        /// controller to roll the passively-trimmed lift rather than command the AoA. Returns true if it
        /// toggled (or was already in the wanted state on a shifter it found).
        /// </summary>
        public static bool SetDescentMode(Vessel v, bool on)
        {
            if (v == null) return false;
            bool found = false;
            for (int i = 0; i < v.parts.Count; i++)
                for (int m = 0; m < v.parts[i].Modules.Count; m++)
                {
                    PartModule pm = v.parts[i].Modules[m];
                    if (pm.GetType().Name != "AdjustableCoMShifter") continue;
                    found = true;
                    bool isOn = ReadBool(pm, "IsDescentMode");
                    if (isOn != on) InvokeEvent(pm, "ToggleMode");
                }
            return found;
        }

        /// <summary>
        /// Set the AdjustableCoMShifter's offsetPercent (0..1 - the dump's "CoM Offset Limit"), which
        /// SCALES the Descent-Mode CoM shift and therefore the PASSIVE trim angle of attack / lift the
        /// capsule flies. The lifting-entry guidance uses this to modulate how much aero lift it wants
        /// (0 = pure retrograde / no lift, 1 = full trim). Returns true if a shifter was found.
        /// </summary>
        public static bool SetComOffset(Vessel v, double fraction)
        {
            if (v == null) return false;
            float f = fraction < 0.0 ? 0f : (fraction > 1.0 ? 1f : (float)fraction);
            bool found = false;
            for (int i = 0; i < v.parts.Count; i++)
                for (int m = 0; m < v.parts[i].Modules.Count; m++)
                {
                    PartModule pm = v.parts[i].Modules[m];
                    if (pm.GetType().Name != "AdjustableCoMShifter") continue;
                    found = true;
                    SetField(pm, "offsetPercent", f);
                }
            return found;
        }

        /// <summary>Read a module's named bool KSPField, or false if it has none.</summary>
        public static bool ReadBool(PartModule pm, string fieldName)
        {
            try
            {
                BaseField bf = pm.Fields[fieldName];
                if (bf == null) return false;
                object o = bf.GetValue(pm);
                return o is bool && (bool)o;
            }
            catch (System.Exception) { return false; }
        }

        // ------------------------------------------------------------------ decouple

        /// <summary>
        /// Fire a specific part's OWN "Decouple" event directly - not staging. Works for any decoupler
        /// (ModuleDecouple, ModuleTundraDecoupler, ModuleAnchoredDecoupler all expose the event by this
        /// name). Returns true if a decouple event was found and invoked.
        /// </summary>
        public static bool Decouple(Part p)
        {
            if (p == null) return false;
            for (int m = 0; m < p.Modules.Count; m++)
                if (InvokeEvent(p.Modules[m], "Decouple")) return true;
            return false;
        }

        // ------------------------------------------------------------------ primitives

        /// <summary>Invoke a module's event by its internal <c>name</c> (case-insensitive), if it is
        /// active. The by-name handle is the dump's `name` column, stable across localisation (the
        /// guiName is not). Returns true if it fired.</summary>
        public static bool InvokeEvent(PartModule pm, string eventName)
        {
            if (pm == null || pm.Events == null) return false;
            foreach (BaseEvent ev in pm.Events)
            {
                if (ev == null || !ev.active) continue;
                if (!string.Equals(ev.name, eventName, System.StringComparison.OrdinalIgnoreCase)) continue;
                try { ev.Invoke(); return true; }
                catch (System.Exception e)
                { Debug.LogWarning(Tag + "event '" + eventName + "' threw: " + e.Message); return false; }
            }
            return false;
        }

        /// <summary>
        /// Fire a part's capability by its GUI NAME - the EVENT if it is active, otherwise the matching
        /// ACTION. Matched on the human label rather than a module type, so it works across modules that
        /// answer to the same command (RealChuteModule "Deploy Chute" / "Cut main chute", stock parachutes,
        /// the decouplers' "Decouple") - the project's detect-by-capability rule, and the same event-or-
        /// action fallback EntryOps.DoEvent proved (an inactive event, e.g. an already-deployed chute, is
        /// skipped). Returns true if something took the command.
        /// </summary>
        public static bool FireByGuiName(Part p, string guiName)
        {
            if (p == null) return false;
            for (int m = 0; m < p.Modules.Count; m++)
            {
                PartModule pm = p.Modules[m];
                if (pm == null) continue;

                if (pm.Events != null)
                    foreach (BaseEvent ev in pm.Events)
                        if (ev != null && ev.active
                            && string.Equals(ev.guiName, guiName, System.StringComparison.OrdinalIgnoreCase))
                        { try { ev.Invoke(); return true; } catch (System.Exception) { } }

                if (pm.Actions != null)
                    for (int a = 0; a < pm.Actions.Count; a++)
                    {
                        BaseAction ba = pm.Actions[a];
                        if (ba == null || !string.Equals(ba.guiName, guiName, System.StringComparison.OrdinalIgnoreCase))
                            continue;
                        try { ba.Invoke(new KSPActionParam(KSPActionGroup.None, KSPActionType.Activate)); return true; }
                        catch (System.Exception) { }
                    }
            }
            return false;
        }

        /// <summary>Set a module's named float KSPField if it has one. Returns true on success. Used for
        /// fields whose module class varies (see SetFinAuthority).</summary>
        public static bool SetField(PartModule pm, string fieldName, float value)
        {
            if (pm == null || pm.Fields == null) return false;
            try
            {
                BaseField bf = pm.Fields[fieldName];
                if (bf == null) return false;
                bf.SetValue(value, pm);
                return true;
            }
            catch (System.Exception) { return false; }
        }

        private static float Clamp01to100(double pct)
        {
            if (pct < 0.0) pct = 0.0;
            if (pct > 100.0) pct = 100.0;
            return (float)pct;
        }
    }
}
