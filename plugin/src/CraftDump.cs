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
 * ⭐⭐ RECOVERED VERBATIM FROM `0d6423d` BY S254 (C1.16's 2026-09-06 extension). This heading and the
 * next one had stood over EMPTY BODIES since `158eb2a` stripped them — the exact defect the rule's
 * extension was written about, in the exact shape described there: "a heading left over an empty body
 * is worse than either — it advertises that something was there and gives no way to find out what."
 * Restored, unedited, because it is still true and because git still had it. Nothing guaranteed that.
 *
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
 *
 * ---- ⭐⭐ S254: EVERYTHING THE PART TREE CAN TELL US ----
 * 🟢 OWNER 2026-09-09: *"add literally everything the part dump could possibly capture for us. Look up
 * every single thing we could possibly capture with it and make sure we get it"*.
 *
 * ⛔ THE MODULE SIDE WAS ALREADY GENERIC and is unchanged in kind: it walks `pm.Fields` / `pm.Events`
 * / `pm.Actions`, so it picks up every `KSPField` any mod defines without knowing the mod exists.
 * ⛔⛔ THE PART ROW WAS NOT. It was four hand-written values against a type that exposes 308 public
 * instance members. It is now walked the same way the modules are, plus nine STRUCTURED rows for the
 * things reflection cannot flatten usefully (the two temperatures against their two limits, the
 * assembly graph, the drag cubes, the crew).
 *
 * ⛔⛔ EVERY DECISION IN THIS FILE LIVES IN `pure/CraftDumpRows.cs`, and that is not tidiness: this
 * file is glue and `build.py test` compiles `src/pure` + `test` only, so a rule written HERE is a rule
 * no test can reach. This file reads live fields; that file decides what the row says.
 *
 * ⛔⛔ STILL CAPTURE-ONLY. S254 adds no assignment to any field, resource, event or action. In
 * particular `res.flowState` is READ and never written — see `CraftDumpRows` §6 for why that is the
 * one the owner is looking at and why there is no `KSPEvent` for it.
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

            StringBuilder sb = new StringBuilder(1 << 20);
            sb.Append(CraftDumpRows.Header).Append('\n');

            List<Part> parts = v.parts;

            // ⭐ THE INDEX MAP, BUILT ONCE. The TREE rows name a parent and children by INDEX, and a
            // linear search per edge would be O(n²) on a 20-part vessel for no reason.
            Dictionary<Part, int> index = new Dictionary<Part, int>();
            for (int i = 0; i < parts.Count; i++)
                if (parts[i] != null && !index.ContainsKey(parts[i])) index[parts[i]] = i;

            try { DumpVessel(sb, v, index); }
            catch (Exception e) { Debug.LogWarning(Tag + "craft dump: vessel row failed: " + e.Message); }

            for (int i = 0; i < parts.Count; i++)
            {
                Part p = parts[i];
                if (p == null) continue;
                try { DumpPart(sb, i, p, index); }
                catch (Exception e) { Debug.LogWarning(Tag + "craft dump: part " + i + " failed: " + e.Message); }
            }

            string text = sb.ToString();
            File.WriteAllText(path, text);

            // ⭐ §2's SIZE RULE, REPORTED BY THE DUMP ITSELF rather than discovered by the owner: the
            // row count and the byte count go in the log line beside the path.
            int rows = 0;
            for (int i = 0; i < text.Length; i++) if (text[i] == '\n') rows++;
            Debug.Log(Tag + "CRAFT DUMP (" + why + ") -> " + path + "  (" + parts.Count
                      + " parts, " + v.vesselName + ", " + (rows - 1) + " rows, "
                      + text.Length + " bytes)");
        }

        // ---------------------------------------------------------------- the vessel header row
        private static void DumpVessel(StringBuilder sb, Vessel v, Dictionary<Part, int> index)
        {
            // ⚠ `Vessel.launchID` and `Vessel.missionID` DO NOT EXIST — checked against
            // Assembly-CSharp, not assumed. They are PART fields, so they come off the ROOT PART and
            // are named `rootLaunchID` / `rootMissionID` so nobody reads them as vessel-level facts.
            int rootIdx = -1;
            uint rootLaunch = 0, rootMission = 0;
            Part root = v.rootPart;
            if (root != null)
            {
                if (index.ContainsKey(root)) rootIdx = index[root];
                rootLaunch = root.launchID;
                rootMission = root.missionID;
            }

            int crew = 0;
            try { crew = v.GetCrewCount(); } catch { }
            int stageCount = -1;
            try { stageCount = KSP.UI.Screens.StageManager.StageCount; } catch { }

            Row(sb, -1, v.vesselName, "-", v.persistentId.ToString(), v.currentStage.ToString(), "-",
                CraftDumpRows.KindVessel, v.vesselName, "-",
                CraftDumpRows.VesselValue(v.vesselName, v.vesselType.ToString(),
                                          v.situation.ToString(), v.parts.Count, v.totalMass, crew),
                "-",
                CraftDumpRows.VesselExtra(v.persistentId, rootLaunch, rootMission, rootIdx,
                                          v.currentStage, stageCount));
        }

        // ---------------------------------------------------------------- one part
        private static void DumpPart(StringBuilder sb, int idx, Part p, Dictionary<Part, int> index)
        {
            // OCT2: the one expression, from `PartNames.Of`. ⛔ THIS DUMP IS THE CONTRACT — the
            // pure layer's tests are written against the CSV this writes, so the live glue and
            // this file classifying by different strings is exactly the OCT1 outage. They can no
            // longer differ. `Of` also never returns null, where this line could; every non-null
            // name is byte-identical to what it produced before.
            string pname = PartNames.Of(p);
            string ptitle = p.partInfo != null ? p.partInfo.title : "-";
            string pid = p.persistentId.ToString();
            string stage = p.inverseStage.ToString();

            Row(sb, idx, pname, ptitle, pid, stage, "-", CraftDumpRows.KindPart, pname, ptitle,
                "mass=" + p.mass.ToString("G4") + " wet=" + p.GetResourceMass().ToString("G4"),
                "", "activates@stage=" + p.inverseStage + " symmetry=" + p.symmetryCounterparts.Count);

            DumpPartMembers(sb, idx, pname, ptitle, pid, stage, p);
            DumpStructured(sb, idx, pname, ptitle, pid, stage, p, index);

            for (int r = 0; r < p.Resources.Count; r++)
            {
                PartResource res = p.Resources[r];
                if (res == null) continue;
                try { DumpResource(sb, idx, pname, ptitle, pid, stage, res); }
                catch (Exception e)
                { Debug.LogWarning(Tag + "craft dump: resource on " + pname + " failed: " + e.Message); }
            }

            for (int m = 0; m < p.Modules.Count; m++)
            {
                PartModule pm = p.Modules[m];
                if (pm == null) continue;
                try { DumpModule(sb, idx, pname, ptitle, pid, stage, pm, m); }
                catch (Exception e)
                { Debug.LogWarning(Tag + "craft dump: module on " + pname + " failed: " + e.Message); }
            }
        }

        /// <summary>⭐⭐ THE GENERIC PART WALK — the same treatment `pm.Fields` has always had.
        /// ⛔ One throwing property costs ONE ROW; `ReflectPublic` guards every read itself.</summary>
        private static void DumpPartMembers(StringBuilder sb, int idx, string pname, string ptitle,
                                            string pid, string stage, Part p)
        {
            List<ReflectedMember> members;
            try { members = CraftDumpRows.ReflectPublic(p); }
            catch (Exception e)
            {
                Debug.LogWarning(Tag + "craft dump: part walk on " + pname + " failed: " + e.Message);
                return;
            }
            for (int i = 0; i < members.Count; i++)
            {
                ReflectedMember m = members[i];
                Row(sb, idx, pname, ptitle, pid, stage, "-", CraftDumpRows.KindProp, m.Name, m.Kind,
                    m.Value, m.TypeName, m.Threw ? "READ THREW" : "");
            }
        }

        /// <summary>The nine rows reflection cannot flatten usefully. ⛔ Each is wrapped on its own,
        /// so a throw in the drag cubes does not cost the thermal row.</summary>
        private static void DumpStructured(StringBuilder sb, int idx, string pname, string ptitle,
                                           string pid, string stage, Part p, Dictionary<Part, int> index)
        {
            // ---- THERMAL: ⛔⛔ the ascent-heating question. MechJeb's overheat limiter reads
            // `temperature` ONLY, never `skinTemperature` — so this row is how we find out which
            // parts are close to failing, and on WHICH of the two.
            Try(sb, pname, "THERMAL", delegate
            {
                Row(sb, idx, pname, ptitle, pid, stage, "-", CraftDumpRows.KindThermal, "thermal", "-",
                    CraftDumpRows.ThermalValue(p.temperature, p.maxTemp, p.skinTemperature, p.skinMaxTemp),
                    "-",
                    CraftDumpRows.ThermalExtra(p.thermalMass, p.skinThermalMass, p.emissiveConstant,
                                               p.heatConductivity, p.skinInternalConductionMult,
                                               p.radiatorHeadroom));
            });

            Try(sb, pname, "STRUCT", delegate
            {
                Row(sb, idx, pname, ptitle, pid, stage, "-", CraftDumpRows.KindStruct, "struct", "-",
                    CraftDumpRows.StructValue(p.crashTolerance, p.breakingForce, p.breakingTorque,
                                              p.maxPressure),
                    "-", "");
            });

            Try(sb, pname, "AERO", delegate
            {
                bool haveCubes = false;
                int cubes = 0; float area = 0f, areaDrag = 0f, xArea = 0f, exposed = 0f;
                float dragCoeff = 0f, depth = 0f, taper = 0f;
                try
                {
                    DragCubeList dcl = p.DragCubes;
                    if (dcl != null)
                    {
                        haveCubes = true;
                        cubes = dcl.Cubes != null ? dcl.Cubes.Count : 0;
                        area = dcl.Area; areaDrag = dcl.AreaDrag;
                        xArea = dcl.CrossSectionalArea; exposed = dcl.ExposedArea;
                        dragCoeff = dcl.DragCoeff; depth = dcl.Depth; taper = dcl.TaperDot;
                    }
                }
                catch { haveCubes = false; }

                Row(sb, idx, pname, ptitle, pid, stage, "-", CraftDumpRows.KindAero, "aero", "-",
                    CraftDumpRows.AeroValue(p.dragScalar, p.bodyLiftScalar, p.maximum_drag,
                                            p.minimum_drag, p.angularDrag, p.buoyancy,
                                            p.ShieldedFromAirstream),
                    "-",
                    CraftDumpRows.DragCubeExtra(haveCubes, cubes, area, areaDrag, xArea, exposed,
                                                dragCoeff, depth, taper));
            });

            Try(sb, pname, "MASS", delegate
            {
                Row(sb, idx, pname, ptitle, pid, stage, "-", CraftDumpRows.KindMass, "mass", "-",
                    CraftDumpRows.MassValue(p.mass, p.prefabMass, p.resourceMass,
                                            p.physicalSignificance.ToString(),
                                            p.rescaleFactor, p.scaleFactor),
                    "-",
                    CraftDumpRows.MassExtra(p.CoMOffset.ToString("F4"), p.CoPOffset.ToString("F4"),
                                            p.CoLOffset.ToString("F4")));
            });

            // ⛔⛔ `Part.stagingEnabled` DOES NOT EXIST — checked against Assembly-CSharp. These are
            // `Part`'s real staging members; `stagingEnabled` is a `PartModule` field and the MODULE
            // row carries it.
            Try(sb, pname, "STAGING", delegate
            {
                Row(sb, idx, pname, ptitle, pid, stage, "-", CraftDumpRows.KindStaging, "staging", "-",
                    CraftDumpRows.StagingValue(p.inverseStage, p.defaultInverseStage, p.separationIndex,
                                               p.manualStageOffset, p.stagingOn, p.stageOffset,
                                               p.childStageOffset, p.originalStage, p.inStageIndex,
                                               p.inverseStageCarryover),
                    "-", "");
            });

            // ---- TREE: ⭐ the assembly graph. Nothing in the dump has ever said what is bolted to what.
            Try(sb, pname, "TREE", delegate
            {
                int parentIdx = -1; string parentName = "-";
                if (p.parent != null)
                {
                    parentName = PartNames.Of(p.parent);
                    if (index.ContainsKey(p.parent)) parentIdx = index[p.parent];
                }
                int[] kids = new int[p.children != null ? p.children.Count : 0];
                for (int c = 0; c < kids.Length; c++)
                {
                    Part kid = p.children[c];
                    kids[c] = (kid != null && index.ContainsKey(kid)) ? index[kid] : -1;
                }

                List<string> nodes = new List<string>();
                if (p.attachNodes != null)
                    for (int n = 0; n < p.attachNodes.Count; n++)
                    {
                        AttachNode an = p.attachNodes[n];
                        if (an == null) continue;
                        nodes.Add(NodeText(an, index));
                    }
                string srf = p.srfAttachNode != null ? NodeText(p.srfAttachNode, index) : null;

                Row(sb, idx, pname, ptitle, pid, stage, "-", CraftDumpRows.KindTree, "tree", "-",
                    CraftDumpRows.TreeValue(parentIdx, parentName, kids, p.attachMode.ToString()),
                    "-", CraftDumpRows.TreeExtra(nodes.ToArray(), srf));
            });

            Try(sb, pname, "IDS", delegate
            {
                Row(sb, idx, pname, ptitle, pid, stage, "-", CraftDumpRows.KindIds, "ids", "-",
                    CraftDumpRows.IdsValue(p.flightID, p.craftID, p.launchID, p.missionID,
                                           p.persistentId, p.isControlSource.ToString()),
                    "-", "");
            });

            // ⛔ `Part.crewCapacity` does not exist; the field is `CrewCapacity`.
            Try(sb, pname, "CREW", delegate
            {
                List<string> names = new List<string>();
                if (p.protoModuleCrew != null)
                    for (int c = 0; c < p.protoModuleCrew.Count; c++)
                        if (p.protoModuleCrew[c] != null) names.Add(p.protoModuleCrew[c].name);

                Row(sb, idx, pname, ptitle, pid, stage, "-", CraftDumpRows.KindCrew, "crew", "-",
                    CraftDumpRows.CrewValue(p.CrewCapacity, names.ToArray()), "-", "");
            });

            // ⛔ `AvailablePart.techRequired` does not exist; the field is `TechRequired`.
            Try(sb, pname, "INFO", delegate
            {
                AvailablePart ap = p.partInfo;
                if (ap == null) return;
                Row(sb, idx, pname, ptitle, pid, stage, "-", CraftDumpRows.KindInfo, ap.name, ap.title,
                    CraftDumpRows.InfoValue(ap.category.ToString(), ap.manufacturer, ap.cost,
                                            ap.entryCost, ap.TechRequired, ap.bulkheadProfiles),
                    "-", ap.description);
            });
        }

        private static string NodeText(AttachNode an, Dictionary<Part, int> index)
        {
            int at = -1; string atName = null;
            if (an.attachedPart != null)
            {
                atName = PartNames.Of(an.attachedPart);
                if (index.ContainsKey(an.attachedPart)) at = index[an.attachedPart];
            }
            return CraftDumpRows.AttachNodeText(an.id, an.size, at, atName,
                                                an.nodeType.ToString(), an.ResourceXFeed);
        }

        // ---------------------------------------------------------------- one resource
        private static void DumpResource(StringBuilder sb, int idx, string pname, string ptitle,
                                         string pid, string stage, PartResource res)
        {
            // ⛔⛔ READ ONLY. `res.flowState` is the field that would turn a tank off, and S254 does
            // NOT assign it — see `CraftDumpRows` §6: there is no `KSPEvent` for it, control would be
            // a bare assignment, and that is the owner's call to make, not a dump's.
            PartResourceDefinition info = res.info;
            string extra = info == null
                ? CraftDumpRows.ResourceExtra(res.flowMode.ToString(), res.isTweakable, res.hideFlow,
                                              res.isVisible, 0, 0.0, 0.0, null, null, null)
                : CraftDumpRows.ResourceExtra(res.flowMode.ToString(), res.isTweakable, res.hideFlow,
                                              res.isVisible, info.id, info.density, info.unitCost,
                                              info.displayName, info.abbreviation,
                                              info.resourceTransferMode.ToString());

            Row(sb, idx, pname, ptitle, pid, stage, "-", CraftDumpRows.KindResource,
                res.resourceName, res.resourceName,
                res.amount.ToString("G6") + "/" + res.maxAmount.ToString("G6"),
                CraftDumpRows.FlowWord(res.flowState), extra);
        }

        // ---------------------------------------------------------------- one module
        private static void DumpModule(StringBuilder sb, int idx, string pname, string ptitle,
                                       string pid, string stage, PartModule pm, int moduleIndex)
        {
            string cls = pm.GetType().Name;
            string disp = "-";
            try { disp = pm.GetModuleDisplayName(); } catch { }
            if (string.IsNullOrEmpty(disp)) disp = pm.moduleName;

            string[] upgrades = new string[0];
            try
            {
                if (pm.upgradesApplied != null) upgrades = pm.upgradesApplied.ToArray();
            }
            catch { }

            bool staging = false;
            try { staging = pm.stagingEnabled; } catch { }

            Row(sb, idx, pname, ptitle, pid, stage, cls, CraftDumpRows.KindModule, pm.moduleName, disp,
                CraftDumpRows.ModuleValue(moduleIndex, pm.enabled, pm.isEnabled, pm.moduleIsEnabled,
                                          staging),
                "-",
                CraftDumpRows.ModuleExtra(FieldStr(pm, "engineID"), upgrades));

            // ⭐ `GetInfo()` GETS ITS OWN ROW rather than riding in the MODULE row's `extra`: it is the
            // VAB tooltip, not a property of the module, and at ~2 KB it would bury the module's own
            // flags in the one cell a reader scans for them. See `CraftDumpRows` §2.
            try
            {
                string info = pm.GetInfo();
                if (!string.IsNullOrEmpty(info))
                    Row(sb, idx, pname, ptitle, pid, stage, cls, CraftDumpRows.KindModInfo,
                        pm.moduleName, "GetInfo()", "-", "-", info);
            }
            catch (Exception e)
            {
                Row(sb, idx, pname, ptitle, pid, stage, cls, CraftDumpRows.KindModInfo,
                    pm.moduleName, "GetInfo()", CraftDumpRows.ErrMarker(e.Message), "-", "");
            }

            if (pm.Events != null)
            {
                foreach (BaseEvent ev in pm.Events)
                {
                    if (ev == null) continue;
                    Row(sb, idx, pname, ptitle, pid, stage, cls, CraftDumpRows.KindEvent,
                        ev.name, ev.guiName, ev.active ? "active" : "inactive", "-",
                        CraftDumpRows.EventExtra(ev.guiActive, ev.guiActiveUnfocused,
                                                 ev.guiActiveUncommand, ev.guiActiveEditor,
                                                 ev.externalToEVAOnly, ev.requireFullControl,
                                                 ev.group != null ? ev.group.name : null));
                }
            }

            if (pm.Fields != null)
            {
                foreach (BaseField f in pm.Fields)
                {
                    if (f == null) continue;
                    string val = "-";
                    try { object o = f.GetValue(pm); val = o == null ? "null" : o.ToString(); }
                    catch (Exception e) { val = CraftDumpRows.ErrMarker(e.Message); }

                    string flightType = null, editorType = null, detail = "";
                    try
                    {
                        if (f.uiControlFlight != null)
                        {
                            flightType = f.uiControlFlight.GetType().Name;
                            detail = CraftDumpRows.ControlDetail("flightCtrl", flightType,
                                                                 Detail(f.uiControlFlight));
                        }
                        if (f.uiControlEditor != null)
                        {
                            editorType = f.uiControlEditor.GetType().Name;
                            string d = CraftDumpRows.ControlDetail("editorCtrl", editorType,
                                                                   Detail(f.uiControlEditor));
                            detail = string.IsNullOrEmpty(detail) ? d : detail + " " + d;
                        }
                    }
                    catch { }

                    string ftype = "?";
                    try { if (f.FieldInfo != null) ftype = f.FieldInfo.FieldType.Name; } catch { }

                    Row(sb, idx, pname, ptitle, pid, stage, cls, CraftDumpRows.KindField,
                        f.name, f.guiName, val,
                        CraftDumpRows.ControlCell(flightType, editorType),
                        CraftDumpRows.FieldExtra(ftype, f.isPersistant, f.guiUnits, f.guiFormat,
                                                 f.group != null ? f.group.name : null,
                                                 f.guiActive, f.guiActiveEditor, detail));
                }
            }

            if (pm.Actions != null)
            {
                foreach (BaseAction a in pm.Actions)
                {
                    if (a == null) continue;
                    Row(sb, idx, pname, ptitle, pid, stage, cls, CraftDumpRows.KindAction,
                        a.name, a.guiName, a.active ? "active" : "inactive", "-",
                        CraftDumpRows.ActionExtra(a.actionGroup.ToString(),
                                                  a.defaultActionGroup.ToString(),
                                                  a.activeEditor, a.requireFullControl));
                }
            }
        }

        /// <summary>
        /// Whatever this control type carries, extracted. ⛔ RETURNING "" IS NOT A FAILURE — the type
        /// NAME is emitted by `ControlCell` / `ControlDetail` regardless, so an unhandled control still
        /// says what it is. That is the fix for the old `DescribeControl`, which printed `-` for
        /// everything it did not recognise and made "unknown control" indistinguishable from "no
        /// control".
        /// </summary>
        private static string Detail(UI_Control uic)
        {
            UI_FloatRange fr = uic as UI_FloatRange;
            if (fr != null) return "min=" + fr.minValue + " max=" + fr.maxValue + " step=" + fr.stepIncrement;

            UI_ChooseOption co = uic as UI_ChooseOption;
            if (co != null && co.options != null) return "options=" + string.Join("|", co.options);

            UI_Toggle tg = uic as UI_Toggle;
            if (tg != null) return "on=" + tg.enabledText + " off=" + tg.disabledText
                                 + " invert=" + tg.invertButton;

            UI_MinMaxRange mm = uic as UI_MinMaxRange;
            if (mm != null) return "minX=" + mm.minValueX + " maxX=" + mm.maxValueX
                                 + " minY=" + mm.minValueY + " maxY=" + mm.maxValueY
                                 + " step=" + mm.stepIncrement;

            UI_Cycle cy = uic as UI_Cycle;
            if (cy != null && cy.stateNames != null) return "states=" + string.Join("|", cy.stateNames);

            UI_ScaleEdit se = uic as UI_ScaleEdit;
            if (se != null) return "intervals=" + (se.intervals != null ? se.intervals.Length : 0)
                                 + " useSI=" + se.useSI + " unit=" + se.unit + " sigFigs=" + se.sigFigs;

            UI_FloatEdit fe = uic as UI_FloatEdit;
            if (fe != null) return "min=" + fe.minValue + " max=" + fe.maxValue
                                 + " incLarge=" + fe.incrementLarge + " incSmall=" + fe.incrementSmall
                                 + " unit=" + fe.unit;

            UI_ProgressBar pb = uic as UI_ProgressBar;
            if (pb != null) return "min=" + pb.minValue + " max=" + pb.maxValue;

            // UI_Label carries nothing at all, and every other control type is reported by NAME only.
            return "";
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

        /// <summary>One structured row, wrapped. ⛔ A throw here costs THAT ROW and nothing else —
        /// the same rule the generic walk follows, applied to the nine hand-built rows.</summary>
        private static void Try(StringBuilder sb, string pname, string what, Action body)
        {
            try { body(); }
            catch (Exception e)
            { Debug.LogWarning(Tag + "craft dump: " + what + " on " + pname + " failed: " + e.Message); }
        }

        private static readonly string[] cells = new string[CraftDumpRows.Columns];

        private static void Row(StringBuilder sb, int idx, string pname, string ptitle, string pid,
                                string stage, string module, string kind, string name, string gui,
                                string value, string ui, string extra)
        {
            cells[0] = idx.ToString(); cells[1] = pname;  cells[2] = ptitle; cells[3] = pid;
            cells[4] = stage;          cells[5] = module; cells[6] = kind;   cells[7] = name;
            cells[8] = gui;            cells[9] = value;  cells[10] = ui;    cells[11] = extra;
            sb.Append(CraftDumpRows.Line(cells));
        }
    }
}
