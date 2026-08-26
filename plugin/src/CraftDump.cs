// DragonScreen - CraftDump
// ---- WHY THIS EXISTS ----
// ---- IT RUNS ITSELF, ONCE, ON THE PAD ----
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

        private static bool dumped;

        public static void Reset() { dumped = false; }

        public static void Auto()
        {
            if (dumped) return;
            Vessel v = FlightGlobals.ActiveVessel;
            if (v == null || v.parts == null || v.parts.Count == 0) return;
            if (v.situation != Vessel.Situations.PRELAUNCH) return;

            dumped = true;
            try { DumpToFile(v, "pad"); }
            catch (Exception e) { Debug.LogWarning(Tag + "craft dump failed: " + e.Message); }
        }

        public static void DumpToFile(Vessel v, string why)
        {
            if (v == null) return;

            string dir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "DragonScreen_capture");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "craftdump.csv");

            StringBuilder sb = new StringBuilder(1 << 16);
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

            Row(sb, idx, pname, ptitle, pid, stage, "-", "PART", pname, ptitle,
                "mass=" + p.mass.ToString("G4") + " wet=" + p.GetResourceMass().ToString("G4"),
                "", "activates@stage=" + p.inverseStage + " symmetry=" + p.symmetryCounterparts.Count);

            for (int r = 0; r < p.Resources.Count; r++)
            {
                PartResource res = p.Resources[r];
                Row(sb, idx, pname, ptitle, pid, stage, "-", "RESOURCE", res.resourceName, res.resourceName,
                    res.amount.ToString("G6") + "/" + res.maxAmount.ToString("G6"),
                    res.flowState ? "flowing" : "locked", "");
            }

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

            Row(sb, idx, pname, ptitle, pid, stage, cls, "MODULE", pm.moduleName, disp,
                "enabled=" + pm.enabled + " isEnabled=" + pm.isEnabled, "",
                "engineID=" + FieldStr(pm, "engineID"));

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
            sb.Length -= 1;
            sb.Append('\n');
        }

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
