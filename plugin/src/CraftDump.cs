/*
 * DragonScreen - CraftDump
 *
 * GLUE. Dumps EVERYTHING KSP exposes about the loaded vessel to a flat CSV: every part, every
 * PartModule, and for each module every EVENT (a button/right-click action), every FIELD (an
 * adjustable or readable setting, with its UI control + range/options), every ACTION (an
 * action-group-bindable), and every resource. One row per thing, with a `kind` column, so it can be
 * grepped and read at a glance.
 *
 * ---- WHY THIS EXISTS ----
 * The goal is to give the autopilot DIRECT control of the vehicle - firing the exact BaseEvent /
 * setting the exact BaseField the part actually has - instead of going through action groups and
 * staging, which are coarse, order-dependent, and blind to which part they hit. To drive a part
 * directly you must know the EXACT names it exposes (the engine mode switch's event name, the gimbal
 * field, the decoupler's event, the fin deploy), and those are not in any doc - they are whatever the
 * part's modules declare at runtime. This dumps them, so control is written against the real handles
 * (the project rule: detect by capability - the module/event/field - not by part name).
 *
 * ---- IT RUNS ITSELF, ONCE, ON THE PAD ----
 * FlightDriver calls Auto() every frame; the first time the active vessel is sitting on the pad
 * (PRELAUNCH) it writes one dump and latches. A fresh flight scene rebuilds FlightDriver, which clears
 * the latch, so every new craft rolled out to the pad is dumped once, automatically, with no button.
 * Written to the same DragonScreen_capture folder as the flight recorder, under a fixed name so the
 * latest pad craft is always at the same path.
 *
 * It only READS the vessel (and GetValue on fields, which is a read) - it fires no events and sets no
 * fields, so dumping can never perturb the craft.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DragonScreen
{
    public static class CraftDump
    {
        private const string Tag = "[DragonScreen] ";

        /// <summary>Latched once a pad dump has been written this flight-scene. FlightDriver is recreated
        /// per scene entry, so this resets for every new craft rolled out.</summary>
        private static bool dumped;

        /// <summary>Reset the once-per-scene latch. Called from FlightDriver.OnDestroy.</summary>
        public static void Reset() { dumped = false; }

        /// <summary>
        /// Called every frame by FlightDriver. Dumps ONCE, the first frame the active vessel is on the
        /// pad. Cheap until it fires (a situation check), then latched.
        /// </summary>
        public static void Auto()
        {
            if (dumped) return;
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.parts == null || v.parts.Count == 0) return;
            if (v.situation != Vessel.Situations.PRELAUNCH) return;   // "on the pad"

            dumped = true;                         // latch BEFORE writing, so a throw cannot loop the dump
            try { DumpToFile(v, "pad"); }
            catch (Exception e) { Debug.LogWarning(Tag + "craft dump failed: " + e.Message); }
        }

        /// <summary>
        /// Write the full dump for <paramref name="v"/> to DragonScreen_capture/craftdump.csv (fixed
        /// name, overwritten each time), and log the path. Public so it can also be fired on demand.
        /// </summary>
        public static void DumpToFile(Vessel v, string why)
        {
            if (v == null) return;

            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "DragonScreen_capture");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "craftdump.csv");

            StringBuilder sb = new StringBuilder(1 << 16);
            // One flat schema for every kind of thing, so a reader never has to reconcile columns.
            sb.Append("part_idx,part_name,part_title,persistent_id,stage,module,kind,name,gui_name,"
                    + "value,ui_control,extra\n");

            List<Part> parts = v.parts;
            for (int i = 0; i < parts.Count; i++)
            {
                Part p = parts[i];
                if (p == null) continue;
                try { DumpPart(sb, i, p); }
                catch (Exception e) { Debug.LogWarning(Tag + "craft dump: part " + i + " failed: " + e.Message); }
            }

            File.WriteAllText(path, sb.ToString());
            Debug.Log(Tag + "CRAFT DUMP (" + why + ") -> " + path + "  (" + parts.Count
                      + " parts, " + v.vesselName + ")");
        }

        private static void DumpPart(StringBuilder sb, int idx, Part p)
        {
            string pname = p.partInfo != null ? p.partInfo.name : p.name;
            string ptitle = p.partInfo != null ? p.partInfo.title : "-";
            string pid = p.persistentId.ToString();
            string stage = p.inverseStage.ToString();

            // The part itself.
            Row(sb, idx, pname, ptitle, pid, stage, "-", "PART", pname, ptitle,
                "mass=" + p.mass.ToString("G4") + " wet=" + p.GetResourceMass().ToString("G4"),
                "", "activates@stage=" + p.inverseStage + " symmetry=" + p.symmetryCounterparts.Count);

            // Resources on the part.
            for (int r = 0; r < p.Resources.Count; r++)
            {
                PartResource res = p.Resources[r];
                Row(sb, idx, pname, ptitle, pid, stage, "-", "RESOURCE", res.resourceName, res.resourceName,
                    res.amount.ToString("G6") + "/" + res.maxAmount.ToString("G6"),
                    res.flowState ? "flowing" : "locked", "");
            }

            // Every module, and everything it exposes.
            for (int m = 0; m < p.Modules.Count; m++)
            {
                PartModule pm = p.Modules[m];
                if (pm == null) continue;
                try { DumpModule(sb, idx, pname, ptitle, pid, stage, pm); }
                catch (Exception e)
                { Debug.LogWarning(Tag + "craft dump: module on " + pname + " failed: " + e.Message); }
            }
        }

        private static void DumpModule(StringBuilder sb, int idx, string pname, string ptitle,
                                       string pid, string stage, PartModule pm)
        {
            string cls = pm.GetType().Name;
            string disp = "-";
            try { disp = pm.GetModuleDisplayName(); } catch { }
            if (string.IsNullOrEmpty(disp)) disp = pm.moduleName;

            // The module row - class name is the handle the code matches on.
            Row(sb, idx, pname, ptitle, pid, stage, cls, "MODULE", pm.moduleName, disp,
                "enabled=" + pm.enabled + " isEnabled=" + pm.isEnabled, "",
                "engineID=" + FieldStr(pm, "engineID"));

            // EVENTS - the right-click / PAW buttons the autopilot can Invoke() directly.
            if (pm.Events != null)
            {
                foreach (BaseEvent ev in pm.Events)
                {
                    if (ev == null) continue;
                    Row(sb, idx, pname, ptitle, pid, stage, cls, "EVENT", ev.name, ev.guiName,
                        ev.active ? "active" : "inactive",
                        "",
                        "guiActive=" + ev.guiActive + " unfocused=" + ev.guiActiveUnfocused
                        + " uncommand=" + ev.guiActiveUncommand);
                }
            }

            // FIELDS - the adjustable / readable settings, with their UI control + range/options.
            if (pm.Fields != null)
            {
                foreach (BaseField f in pm.Fields)
                {
                    if (f == null) continue;
                    string val = "-";
                    try { object o = f.GetValue(pm); val = o == null ? "null" : o.ToString(); }
                    catch (Exception e) { val = "<err:" + e.Message + ">"; }

                    string ctrl, ctrlExtra;
                    DescribeControl(f, out ctrl, out ctrlExtra);

                    Row(sb, idx, pname, ptitle, pid, stage, cls, "FIELD", f.name, f.guiName,
                        val, ctrl,
                        ctrlExtra + " guiActive=" + f.guiActive + " editor=" + f.guiActiveEditor);
                }
            }

            // ACTIONS - the action-group-bindable actions (also invokable directly with a KSPActionParam).
            if (pm.Actions != null)
            {
                foreach (BaseAction a in pm.Actions)
                {
                    if (a == null) continue;
                    Row(sb, idx, pname, ptitle, pid, stage, cls, "ACTION", a.name, a.guiName,
                        a.active ? "active" : "inactive", "", "");
                }
            }
        }

        /// <summary>Describe a field's flight UI control - the type, and its range or option list, so the
        /// dump shows what values are legal to set.</summary>
        private static void DescribeControl(BaseField f, out string ctrl, out string extra)
        {
            ctrl = "-"; extra = "";
            UI_Control uic = f.uiControlFlight != null ? f.uiControlFlight : f.uiControlEditor;
            if (uic == null) { return; }
            ctrl = uic.GetType().Name;

            UI_FloatRange fr = uic as UI_FloatRange;
            if (fr != null) { extra = "min=" + fr.minValue + " max=" + fr.maxValue + " step=" + fr.stepIncrement; return; }

            UI_ChooseOption co = uic as UI_ChooseOption;
            if (co != null && co.options != null) { extra = "options=" + string.Join("|", co.options); return; }

            UI_Toggle tg = uic as UI_Toggle;
            if (tg != null) { extra = "toggle(on=" + tg.enabledText + ";off=" + tg.disabledText + ")"; return; }
        }

        /// <summary>Read a named KSPField off a module as a string, or "-" if it has none. Used to surface
        /// the engineID on the MODULE row (the octaweb's three modes are told apart by it).</summary>
        private static string FieldStr(PartModule pm, string name)
        {
            try
            {
                BaseField bf = pm.Fields[name];
                if (bf == null) return "-";
                object o = bf.GetValue(pm);
                return o == null ? "-" : o.ToString();
            }
            catch { return "-"; }
        }

        private static void Row(StringBuilder sb, int idx, string pname, string ptitle, string pid,
                                string stage, string module, string kind, string name, string gui,
                                string value, string ui, string extra)
        {
            C(sb, idx.ToString()); C(sb, pname); C(sb, ptitle); C(sb, pid); C(sb, stage);
            C(sb, module); C(sb, kind); C(sb, name); C(sb, gui); C(sb, value); C(sb, ui);
            C(sb, extra);
            sb.Length -= 1;           // trailing comma
            sb.Append('\n');
        }

        /// <summary>One CSV cell: commas and newlines would shift columns, so they are neutralised (the
        /// same rule as FlightRecorder.S), and long values are capped so one array field cannot bloat the file.</summary>
        private static void C(StringBuilder sb, string s)
        {
            if (string.IsNullOrEmpty(s)) s = "-";
            if (s.Length > 160) s = s.Substring(0, 157) + "...";
            s = s.Replace(',', ';').Replace('\n', ' ').Replace('\r', ' ');
            sb.Append(s);
            sb.Append(',');
        }
    }
}
