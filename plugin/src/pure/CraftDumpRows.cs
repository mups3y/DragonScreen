// DragonScreen - CraftDumpRows  (S254)
// =============================================================================================
// ⭐⭐ WHY THIS FILE EXISTS AT ALL, WHEN THE DUMP ALREADY WORKED.
//
// 🟢 OWNER 2026-09-09, verbatim: *"add literally everything the part dump could possibly capture for
// us. Look up every single thing we could possibly capture with it and make sure we get it"*,
// following *"next flight we want a new craft dump to run so we can capture this event/setting and
// control it directly"*.
//
// ⛔⛔ `src/CraftDump.cs` IS GLUE AND CANNOT BE COMPILED HEADLESSLY. `build.py test` compiles
// `src/pure` + `test` only, so every rule that lived in the glue was a rule no test could reach. The
// prompt asks for "a test per new row kind, driven from a fixture" and for proof that "a throwing
// property does not lose the part" — neither is possible against a `Part`. So the DECISIONS move
// here and the glue becomes an adapter that reads live fields and hands them over.
//
// ⛔⛔ CAPTURE ONLY. NOTHING IN THIS FILE WRITES ANYTHING TO ANYTHING. It formats strings. The dump
// sets no field, fires no event and toggles no flow — its whole purpose is to tell the owner what
// COULD be controlled, so that the control is his decision and not a side effect of measuring.
//
// ⚠ THE PROMPT'S MEMBER LIST WAS CHECKED AGAINST `Assembly-CSharp.dll` RATHER THAN TRUSTED, and
// three of its names do not survive that check. They are recorded here because a future reader will
// otherwise re-derive them:
//   ⛔ `Part.stagingEnabled`  DOES NOT EXIST. `stagingEnabled` is on `PartModule`, not on `Part`.
//      The staging row carries `Part`'s real ones instead — `stagingOn`, `stageOffset`,
//      `childStageOffset`, `originalStage`, `inStageIndex`, `inverseStageCarryover`.
//   ⛔ `Part.crewCapacity`    IS SPELLED `CrewCapacity` — capital C. The lower-case name compiles to
//      nothing and would have been a silent hole in the CREW row.
//   ⛔ `AvailablePart.techRequired` IS SPELLED `TechRequired` — capital T. Same failure mode.
//   ⭐ `temperatureMultiplier` — the prompt already says it does not exist, and it does not.
// (`Vessel.launchID` / `Vessel.missionID` do not exist either; those two are PART fields, so the
// vessel header row takes them from the ROOT PART and says so.)
// =============================================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace DragonScreen
{
    /// <summary>One member read off an object by <see cref="CraftDumpRows.ReflectPublic"/>.</summary>
    public struct ReflectedMember
    {
        /// <summary>The member's own name.</summary>
        public string Name;
        /// <summary>"field" or "property" — which of the two it was.</summary>
        public string Kind;
        /// <summary>The DECLARED type's full name. ⭐ Without this you cannot write the value back,
        /// which is the same reason `FieldInfo.FieldType` is now on the FIELD rows.</summary>
        public string TypeName;
        /// <summary>The value, already stringified. `&lt;ref:...&gt;` for a skipped reference,
        /// `&lt;err:...&gt;` when the read threw.</summary>
        public string Value;
        /// <summary>⛔ True when the read THREW. The row is still emitted — one throw costs one row,
        /// never the whole part.</summary>
        public bool Threw;
    }

    public static class CraftDumpRows
    {
        // =========================================================================================
        // 1. THE CSV SHAPE — twelve columns, one place
        // =========================================================================================
        // ⭐ THE COLUMN LIST DOES NOT CHANGE, and that is deliberate: the owner already has tooling
        // pointed at this shape and S254 adds ROWS, not columns. Everything new lands in `kind` +
        // `value` + `extra`.

        public const string Header = "part_idx,part_name,part_title,persistent_id,stage,module,kind,"
                                   + "name,gui_name,value,ui_control,extra";

        /// <summary>The number of columns every row must have.</summary>
        public const int Columns = 12;

        // =========================================================================================
        // 2. ⛔⛔ THE SIZE RULE — SOLVED DELIBERATELY, NOT DISCOVERED
        // =========================================================================================
        //
        // ⚠ THE LAST DUMP WAS 1,092,773 BYTES OVER 7,135 LINES for 20 parts
        // (`docs/reference/craftdump.csv`, measured — not remembered). S254 roughly doubles the row
        // count, because `Part` exposes 308 public instance members and every one of them now gets a
        // row. ⛔ So the truncation rule has to be a DECISION, made here, rather than something the
        // owner discovers when the file will not open.
        //
        // ⭐⭐ THE DECISION, AND IT IS BOTH HALVES OF WHAT THE PROMPT OFFERED, for two different
        // reasons:
        //  (a) `extra` gets a WIDER cap than the other eleven columns. ⛔ Not for `GetInfo()`'s sake —
        //      for the rows that are USELESS truncated at 160: `TREE`'s attach-node list, `AERO`'s
        //      drag cubes, `INFO`'s part description, and a `UI_ChooseOption` with thirty options.
        //      Those are the reason the cap moves; a 160-char attach-node list answers nothing.
        //  (b) `PartModule.GetInfo()` STILL gets its own row kind (`MODINFO`) rather than riding in
        //      the MODULE row's `extra`. ⛔ It is a different THING — the VAB tooltip, not a property
        //      of the module — and at ~2 KB it would bury the module's own flags behind it in the one
        //      cell a reader scans for them. A separate row is also droppable with one filter if the
        //      file ever does get too big, which a buried cell is not.
        //
        // ⚠ THE WIDE CAP IS STILL A CAP. Some mods' `GetInfo()` runs to tens of thousands of
        // characters. 2000 is chosen so that 265 MODULE rows cost at most ~0.5 MB even in the worst
        // case, against a measured typical of a few hundred.

        /// <summary>The cap on every column except <c>extra</c>. Unchanged from the original dump.</summary>
        public const int NarrowCap = 160;

        /// <summary>⭐ The cap on <c>extra</c> only. See the block above for why it is not 160.</summary>
        public const int WideCap = 2000;

        /// <summary>
        /// One CSV cell: never empty, never containing a comma or a newline, never longer than
        /// <paramref name="cap"/>. ⛔ TRUNCATION IS MARKED — a silently shortened value that still
        /// looks well-formed is worse than one that says it was cut.
        /// </summary>
        public static string Cell(string s, int cap)
        {
            if (string.IsNullOrEmpty(s)) return "-";
            if (cap < 8) cap = 8;
            if (s.Length > cap) s = s.Substring(0, cap - 3) + "...";
            return s.Replace(',', ';').Replace('\n', ' ').Replace('\r', ' ');
        }

        /// <summary>The cap that applies to column <paramref name="column"/> (0-based).
        /// ⭐ Only `extra` (the last) is wide; asking here rather than at each call site is what stops
        /// the two caps drifting apart.</summary>
        public static int CapFor(int column)
        {
            return column == Columns - 1 ? WideCap : NarrowCap;
        }

        /// <summary>
        /// One complete CSV line, newline included. ⛔ The per-column cap is applied HERE, so no
        /// caller can forget it, and a row with the wrong number of cells is a caught error rather
        /// than a shifted column.
        /// </summary>
        public static string Line(string[] cells)
        {
            if (cells == null || cells.Length != Columns)
                throw new ArgumentException("a craft-dump row has exactly " + Columns + " cells, got "
                                            + (cells == null ? 0 : cells.Length));
            var sb = new StringBuilder(256);
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Cell(cells[i], CapFor(i)));
            }
            sb.Append('\n');
            return sb.ToString();
        }

        // =========================================================================================
        // 3. THE ROW KINDS — named once, so "is every kind emitted" is a question a test can ask
        // =========================================================================================

        public const string KindVessel   = "VESSEL";
        public const string KindPart     = "PART";
        public const string KindProp     = "PROP";      // the generic reflected `Part` member
        public const string KindThermal  = "THERMAL";
        public const string KindStruct   = "STRUCT";
        public const string KindAero     = "AERO";
        public const string KindMass     = "MASS";
        public const string KindStaging  = "STAGING";
        public const string KindTree     = "TREE";
        public const string KindIds      = "IDS";
        public const string KindCrew     = "CREW";
        public const string KindInfo     = "INFO";      // `part.partInfo` — the catalogue entry
        public const string KindResource = "RESOURCE";
        public const string KindModule   = "MODULE";
        public const string KindModInfo  = "MODINFO";   // `PartModule.GetInfo()` — the VAB tooltip
        public const string KindField    = "FIELD";
        public const string KindEvent    = "EVENT";
        public const string KindAction   = "ACTION";

        /// <summary>Every kind the dump can emit. ⛔ A kind the glue writes and this array does not
        /// name is a row nobody decided about — the same failure `AscentProfile.Audit` exists to end.</summary>
        public static readonly string[] Kinds =
        {
            KindVessel, KindPart, KindProp, KindThermal, KindStruct, KindAero, KindMass, KindStaging,
            KindTree, KindIds, KindCrew, KindInfo, KindResource, KindModule, KindModInfo, KindField,
            KindEvent, KindAction,
        };

        /// <summary>The seventeen kinds S254 added or kept, minus the six the dump already had.
        /// ⭐ Exists so a test can assert the ADDITION, not merely the total.</summary>
        public static readonly string[] KindsBeforeS254 =
        { KindPart, KindResource, KindModule, KindField, KindEvent, KindAction };

        public static bool IsKnownKind(string kind)
        {
            for (int i = 0; i < Kinds.Length; i++)
                if (string.Equals(Kinds[i], kind, StringComparison.Ordinal)) return true;
            return false;
        }

        // =========================================================================================
        // 4. ⭐⭐ THE GENERIC `Part` WALK — the gap this task closes
        // =========================================================================================
        //
        // ⛔ THE PART ROW USED TO BE FOUR HAND-WRITTEN VALUES against a type that exposes 308 public
        // instance members (262 fields + 46 properties, counted off `Assembly-CSharp.dll`). The
        // MODULE side has always been generic — it walks `pm.Fields` and so picks up every `KSPField`
        // any mod defines without knowing the mod exists. This makes the PART side generic the same way.
        //
        // ⚠ EVERY READ IS GUARDED INDIVIDUALLY. Some `Part` properties throw off the active vessel;
        // one throw must cost ONE ROW, never the part. That is asserted by a fixture with a property
        // that always throws — not observed on a live vessel that happens not to throw.

        /// <summary>Unity value types we DO want. ⭐ `CoMOffset` / `CoPOffset` / `CoLOffset` are
        /// `UnityEngine.Vector3`, so a blanket "skip everything under UnityEngine" would throw away
        /// three of the MASS row's own members.</summary>
        static readonly string[] UnityValueTypes =
        {
            "UnityEngine.Vector2", "UnityEngine.Vector3", "UnityEngine.Vector4",
            "UnityEngine.Quaternion", "UnityEngine.Color", "UnityEngine.Color32",
            "UnityEngine.Rect", "UnityEngine.Bounds", "UnityEngine.Matrix4x4",
            "UnityEngine.HideFlags", "UnityEngine.Space", "UnityEngine.LayerMask",
        };

        /// <summary>
        /// ⛔ Is this member's DECLARED type Unity plumbing we must not walk into? The prompt's rule:
        /// *"emit the name of such a reference, never walk into it"* — a `GameObject` has its own
        /// children, components and transforms, and following one turns a part dump into a scene dump.
        /// ⭐ The test is on the NAME, so this stays pure: everything under `UnityEngine.` is plumbing
        /// EXCEPT the handful of value types above.
        /// </summary>
        public static bool SkipMemberType(string typeFullName)
        {
            if (string.IsNullOrEmpty(typeFullName)) return false;
            if (!typeFullName.StartsWith("UnityEngine.", StringComparison.Ordinal)) return false;
            for (int i = 0; i < UnityValueTypes.Length; i++)
                if (typeFullName == UnityValueTypes[i]) return false;
            return true;
        }

        /// <summary>The placeholder a skipped reference gets instead of its value.</summary>
        public static string RefMarker(string typeFullName)
        {
            return "<ref:" + (string.IsNullOrEmpty(typeFullName) ? "?" : typeFullName) + ">";
        }

        /// <summary>The marker a THROWING read gets. ⭐ It carries the message, so the owner can see
        /// WHICH property is unreadable on the pad rather than only that one was.</summary>
        public static string ErrMarker(string message)
        {
            return "<err:" + (string.IsNullOrEmpty(message) ? "?" : message) + ">";
        }

        /// <summary>
        /// Stringify one read value. ⭐ A LIST/ARRAY BECOMES ITS COUNT, not its contents: the members
        /// worth expanding (`attachNodes`, `children`, `protoModuleCrew`) each have their OWN row
        /// kind, and expanding them here as well would say the same thing twice at length.
        /// </summary>
        public static string Describe(object value)
        {
            if (value == null) return "null";
            var list = value as System.Collections.ICollection;
            if (list != null) return "count=" + list.Count;
            string s = value.ToString();
            return string.IsNullOrEmpty(s) ? "-" : s;
        }

        /// <summary>
        /// ⭐⭐ Every public instance FIELD and PROPERTY of <paramref name="target"/>, read one at a
        /// time, each guarded. ⛔ NEVER THROWS: a member that throws comes back as a row with
        /// <see cref="ReflectedMember.Threw"/> set, so the caller keeps the other 307.
        /// ⚠ Indexed properties are skipped — `this[int]` has no value to read without an argument.
        /// </summary>
        public static List<ReflectedMember> ReflectPublic(object target)
        {
            var rows = new List<ReflectedMember>();
            if (target == null) return rows;

            Type t;
            try { t = target.GetType(); }
            catch { return rows; }

            const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance;

            FieldInfo[] fields;
            try { fields = t.GetFields(Flags); } catch { fields = new FieldInfo[0]; }
            for (int i = 0; i < fields.Length; i++)
                rows.Add(ReadOne(target, fields[i].Name, "field",
                                 SafeTypeName(fields[i].FieldType), fields[i], null));

            PropertyInfo[] props;
            try { props = t.GetProperties(Flags); } catch { props = new PropertyInfo[0]; }
            for (int i = 0; i < props.Length; i++)
            {
                PropertyInfo p = props[i];
                bool indexed;
                try { indexed = p.GetIndexParameters().Length > 0; } catch { indexed = true; }
                if (indexed) continue;
                bool readable;
                try { readable = p.CanRead; } catch { readable = false; }
                if (!readable) continue;
                rows.Add(ReadOne(target, p.Name, "property", SafeTypeName(p.PropertyType), null, p));
            }

            return rows;
        }

        static string SafeTypeName(Type t)
        {
            try { return t == null ? "?" : (t.FullName ?? t.Name); }
            catch { return "?"; }
        }

        static ReflectedMember ReadOne(object target, string name, string kind, string typeName,
                                       FieldInfo f, PropertyInfo p)
        {
            ReflectedMember r;
            r.Name = name; r.Kind = kind; r.TypeName = typeName; r.Threw = false;

            // ⛔ THE SKIP IS DECIDED BEFORE THE READ, not after. Calling a `GameObject` getter is
            // harmless; deciding afterwards is what leads to walking into one by accident later.
            if (SkipMemberType(typeName)) { r.Value = RefMarker(typeName); return r; }

            try
            {
                object v = f != null ? f.GetValue(target) : p.GetValue(target, null);
                r.Value = Describe(v);
            }
            catch (Exception e)
            {
                // ⚠ A reflected getter's exception arrives wrapped; the INNER one is the useful text.
                Exception inner = e is TargetInvocationException && e.InnerException != null
                                ? e.InnerException : e;
                r.Value = ErrMarker(inner.Message);
                r.Threw = true;
            }
            return r;
        }

        // =========================================================================================
        // 5. THE STRUCTURED ROWS — one function per kind, so a fixture can drive each one
        // =========================================================================================

        /// <summary>
        /// ⭐⭐ THE ASCENT-HEATING ROW, AND THE ONE COMPUTED VERDICT IN THE WHOLE DUMP.
        /// ⛔⛔ NTSB found the trunk's SKIN crossing its limit while its INTERNAL temperature was
        /// fine, and MechJeb's overheat limiter reads `part.temperature` ONLY — never
        /// `skinTemperature` (`MechJebModuleThrustController`, and S250's own audit row says so). So
        /// the question the owner needs answered is not "how hot" but "how close, and on WHICH of the
        /// two". This computes both fractions and names the worse one.
        /// ⚠ A non-positive limit yields -1 for that fraction and is reported as `n/a`, not as 0% —
        /// a part with no limit is not a part with infinite margin.
        /// </summary>
        public static double UsedFraction(double value, double limit)
        {
            if (limit <= 0.0 || double.IsNaN(limit) || double.IsInfinity(limit)) return -1.0;
            if (double.IsNaN(value) || double.IsInfinity(value)) return -1.0;
            return value / limit;
        }

        /// <summary>"SKIN", "INTERNAL", or "-" when neither fraction is computable. ⛔ Ties go to
        /// SKIN, because skin is the one nothing throttles on.</summary>
        public static string WorseOf(double internalFrac, double skinFrac)
        {
            bool haveI = internalFrac >= 0.0, haveS = skinFrac >= 0.0;
            if (!haveI && !haveS) return "-";
            if (!haveS) return "INTERNAL";
            if (!haveI) return "SKIN";
            return skinFrac >= internalFrac ? "SKIN" : "INTERNAL";
        }

        static string Pct(double frac)
        {
            return frac < 0.0 ? "n/a" : (frac * 100.0).ToString("F1") + "%";
        }

        static string N(double d) { return d.ToString("G6"); }
        static string N(float f) { return f.ToString("G6"); }

        /// <summary>THERMAL's `value` cell: the two temperatures against their two limits.</summary>
        public static string ThermalValue(double temperature, double maxTemp,
                                          double skinTemperature, double skinMaxTemp)
        {
            double fi = UsedFraction(temperature, maxTemp);
            double fs = UsedFraction(skinTemperature, skinMaxTemp);
            return "T=" + N(temperature) + "/" + N(maxTemp) + " (" + Pct(fi) + ")"
                 + " skinT=" + N(skinTemperature) + "/" + N(skinMaxTemp) + " (" + Pct(fs) + ")"
                 + " worst=" + WorseOf(fi, fs);
        }

        /// <summary>THERMAL's `extra` cell: what the heat has to move through.</summary>
        public static string ThermalExtra(double thermalMass, double skinThermalMass,
                                          double emissiveConstant, double heatConductivity,
                                          double skinInternalConductionMult, double radiatorHeadroom)
        {
            return "thermalMass=" + N(thermalMass) + " skinThermalMass=" + N(skinThermalMass)
                 + " emissiveConstant=" + N(emissiveConstant)
                 + " heatConductivity=" + N(heatConductivity)
                 + " skinInternalConductionMult=" + N(skinInternalConductionMult)
                 + " radiatorHeadroom=" + N(radiatorHeadroom);
        }

        /// <summary>STRUCT: what actually breaks, and at what load.</summary>
        public static string StructValue(float crashTolerance, float breakingForce,
                                         float breakingTorque, double maxPressure)
        {
            return "crashTolerance=" + N(crashTolerance) + " breakingForce=" + N(breakingForce)
                 + " breakingTorque=" + N(breakingTorque) + " maxPressure=" + N(maxPressure);
        }

        /// <summary>AERO: the drag model the ascent flies through.</summary>
        public static string AeroValue(float dragScalar, float bodyLiftScalar, float maximumDrag,
                                       float minimumDrag, float angularDrag, float buoyancy,
                                       bool shieldedFromAirstream)
        {
            return "dragScalar=" + N(dragScalar) + " bodyLiftScalar=" + N(bodyLiftScalar)
                 + " maximum_drag=" + N(maximumDrag) + " minimum_drag=" + N(minimumDrag)
                 + " angularDrag=" + N(angularDrag) + " buoyancy=" + N(buoyancy)
                 + " ShieldedFromAirstream=" + shieldedFromAirstream;
        }

        /// <summary>AERO's `extra`: the drag cubes' own areas and coefficient.
        /// ⚠ `have` is false when `part.DragCubes` is null or threw — reported, not silently zeroed.</summary>
        public static string DragCubeExtra(bool have, int cubeCount, float area, float areaDrag,
                                           float crossSectionalArea, float exposedArea,
                                           float dragCoeff, float depth, float taperDot)
        {
            if (!have) return "DragCubes=<unreadable>";
            return "cubes=" + cubeCount + " Area=" + N(area) + " AreaDrag=" + N(areaDrag)
                 + " CrossSectionalArea=" + N(crossSectionalArea) + " ExposedArea=" + N(exposedArea)
                 + " DragCoeff=" + N(dragCoeff) + " Depth=" + N(depth) + " TaperDot=" + N(taperDot);
        }

        /// <summary>MASS: where the mass actually is.</summary>
        public static string MassValue(float mass, float prefabMass, float resourceMass,
                                       string physicalSignificance, float rescaleFactor, float scaleFactor)
        {
            return "mass=" + N(mass) + " prefabMass=" + N(prefabMass) + " resourceMass=" + N(resourceMass)
                 + " total=" + N(mass + resourceMass)
                 + " physicalSignificance=" + (physicalSignificance ?? "-")
                 + " rescaleFactor=" + N(rescaleFactor) + " scaleFactor=" + N(scaleFactor);
        }

        /// <summary>MASS's `extra`: the three offsets, already stringified by the glue (they are
        /// `UnityEngine.Vector3`, which this assembly does not reference).</summary>
        public static string MassExtra(string coM, string coP, string coL)
        {
            return "CoMOffset=" + (coM ?? "-") + " CoPOffset=" + (coP ?? "-")
                 + " CoLOffset=" + (coL ?? "-");
        }

        /// <summary>
        /// STAGING — ⭐ feeds `StagingFloor.For` and the S250 floor question directly.
        /// ⛔⛔ `Part.stagingEnabled` DOES NOT EXIST and is not silently dropped: the parameters below
        /// are `Part`'s REAL staging members, and `stagingEnabled` is a `PartModule` field which the
        /// MODULE row now carries. See this file's header.
        /// </summary>
        public static string StagingValue(int inverseStage, int defaultInverseStage, int separationIndex,
                                          int manualStageOffset, bool stagingOn, int stageOffset,
                                          int childStageOffset, int originalStage, int inStageIndex,
                                          bool inverseStageCarryover)
        {
            return "inverseStage=" + inverseStage + " defaultInverseStage=" + defaultInverseStage
                 + " separationIndex=" + separationIndex + " manualStageOffset=" + manualStageOffset
                 + " stagingOn=" + stagingOn + " stageOffset=" + stageOffset
                 + " childStageOffset=" + childStageOffset + " originalStage=" + originalStage
                 + " inStageIndex=" + inStageIndex + " inverseStageCarryover=" + inverseStageCarryover;
        }

        /// <summary>
        /// ⭐⭐ TREE — THE ASSEMBLY GRAPH. Nothing in the dump has ever said what is bolted to what;
        /// the staging analysis has been INFERRING it from stage numbers, which is exactly how S250's
        /// floor ended up anchored on the wrong decoupler once already.
        /// ⚠ `parentIndex` is -1 for the root; a child index is -1 when that child is not in the
        /// vessel's own part list, which is reported rather than dropped.
        /// </summary>
        public static string TreeValue(int parentIndex, string parentName, int[] childIndices,
                                       string attachMode)
        {
            var sb = new StringBuilder(96);
            sb.Append("parent=");
            if (parentIndex < 0) sb.Append("ROOT");
            else sb.Append('#').Append(parentIndex).Append(':').Append(parentName ?? "?");
            sb.Append(" children=");
            if (childIndices == null || childIndices.Length == 0) sb.Append("none");
            else
                for (int i = 0; i < childIndices.Length; i++)
                {
                    if (i > 0) sb.Append('|');
                    sb.Append('#').Append(childIndices[i]);
                }
            sb.Append(" attachMode=").Append(attachMode ?? "-");
            return sb.ToString();
        }

        /// <summary>One attach node, for TREE's `extra`. ⭐ `attachedIndex` &lt; 0 means the node is
        /// EMPTY — which is the interesting case, because an empty node is where something could be
        /// attached and is not.</summary>
        public static string AttachNodeText(string id, int size, int attachedIndex,
                                            string attachedName, string nodeType, bool resourceXFeed)
        {
            return (id ?? "?") + "(size=" + size + " type=" + (nodeType ?? "-")
                 + " xfeed=" + resourceXFeed + ")->"
                 + (attachedIndex < 0 ? "EMPTY" : "#" + attachedIndex + ":" + (attachedName ?? "?"));
        }

        /// <summary>TREE's `extra`: every attach node, plus the surface-attach node.</summary>
        public static string TreeExtra(string[] nodes, string srfNode)
        {
            string joined = nodes == null || nodes.Length == 0 ? "none" : string.Join(" ", nodes);
            return "attachNodes=" + joined + " srfAttachNode=" + (srfNode ?? "none");
        }

        /// <summary>
        /// IDS — ⚠ `launchID` was the `S147b` counter ruling and has never been observed in a dump.
        /// It is here so that it is observed once and the ruling can be checked against a real value
        /// rather than against a memory of one.
        /// </summary>
        public static string IdsValue(uint flightID, uint craftID, uint launchID, uint missionID,
                                      uint persistentId, string isControlSource)
        {
            return "flightID=" + flightID + " craftID=" + craftID + " launchID=" + launchID
                 + " missionID=" + missionID + " persistentId=" + persistentId
                 + " isControlSource=" + (isControlSource ?? "-");
        }

        /// <summary>
        /// CREW — per-part crew, which TAC-LS rates are per-kerbal against.
        /// ⛔ `Part.crewCapacity` does not exist; the field is `CrewCapacity`. See the header.
        /// </summary>
        public static string CrewValue(int crewCapacity, string[] crewNames)
        {
            int n = crewNames == null ? 0 : crewNames.Length;
            return "CrewCapacity=" + crewCapacity + " aboard=" + n
                 + " crew=" + (n == 0 ? "none" : string.Join("|", crewNames));
        }

        /// <summary>
        /// INFO — the catalogue entry. ⚠ `description` is long, so this is the row §2's WIDE cap was
        /// raised for. ⛔ `AvailablePart.techRequired` does not exist; it is `TechRequired`.
        /// </summary>
        public static string InfoValue(string category, string manufacturer, double cost,
                                       int entryCost, string techRequired, string bulkheadProfiles)
        {
            return "category=" + (category ?? "-") + " manufacturer=" + (manufacturer ?? "-")
                 + " cost=" + N(cost) + " entryCost=" + entryCost
                 + " TechRequired=" + (techRequired ?? "-")
                 + " bulkheadProfiles=" + (bulkheadProfiles ?? "-");
        }

        // =========================================================================================
        // 6. ⭐⭐ RESOURCE — THE OWNER'S LIVE QUESTION, AND THE ONE ROW WITH A WARNING ON IT
        // =========================================================================================
        //
        // ⛔⛔ `MechJebLibBindings/FuelFlowSimulation/SimVesselUpdater.cs:91` SKIPS ANY RESOURCE WITH
        // `!flowState` and counts it as `DisabledResourcesMass` — **mass carried, ZERO ΔV**. That is
        // the only state in which propellant is aboard and invisible to the ascent solver, which is
        // why the owner wants to see it before he touches it.
        //
        // ⛔⛔ AND THERE IS NO `KSPEvent` FOR THIS — DO NOT GO LOOKING FOR ONE. Checked against the
        // vendored source and against the last dump: `ModuleFuelTanks` exposes `ChooseTankDefinition`,
        // `Empty`, `ToggleStaging` and `OnResourceMaxChanged`, none of them flow-related, and no
        // `EVENT` row in `docs/reference/craftdump.csv` matches flow or lock. The PAW builds the
        // padlock from `UIPartActionResourceEditor` against the stock `PartResource`.
        // ⭐ CONTROL WOULD BE AN ASSIGNMENT — `res.flowState = false` — and S254 DOES NOT MAKE IT.
        //
        // ⭐ `flowMode` is the field that decides whether locking ONE tank actually stops the engines
        // drawing, so it is the one that says whether the idea works at all.

        /// <summary>The `ui_control` cell for a resource. ⭐ Already emitted by the original dump and
        /// deliberately unchanged — the owner's tooling reads this exact word.</summary>
        public static string FlowWord(bool flowState) { return flowState ? "flowing" : "locked"; }

        /// <summary>
        /// RESOURCE's `extra`: the whole flow-control picture in one cell.
        /// ⚠ `flowState` is NOT repeated here — it is the `ui_control` column already, and §3.2 says
        /// so explicitly.
        /// </summary>
        public static string ResourceExtra(string flowMode, bool isTweakable, bool hideFlow,
                                           bool isVisible, int infoId, double density, double unitCost,
                                           string displayName, string abbreviation, string transferMode)
        {
            return "flowMode=" + (flowMode ?? "-") + " isTweakable=" + isTweakable
                 + " hideFlow=" + hideFlow + " isVisible=" + isVisible
                 + " info.id=" + infoId + " density=" + N(density) + " unitCost=" + N(unitCost)
                 + " displayName=" + (displayName ?? "-") + " abbreviation=" + (abbreviation ?? "-")
                 + " transferMode=" + (transferMode ?? "-");
        }

        // =========================================================================================
        // 7. MODULE / FIELD / EVENT / ACTION — what makes each one addressable
        // =========================================================================================

        /// <summary>
        /// ⭐⭐ THE MODULE INDEX, which is the single thing that makes a module addressable at all:
        /// a ModuleManager `MODULE { }` node is addressed by its INDEX within `part.Modules`, and the
        /// dump has never emitted it. Every patch we ever write needs it.
        /// </summary>
        public static string ModuleValue(int moduleIndex, bool enabled, bool isEnabled,
                                         bool moduleIsEnabled, bool stagingEnabled)
        {
            return "index=" + moduleIndex + " enabled=" + enabled + " isEnabled=" + isEnabled
                 + " moduleIsEnabled=" + moduleIsEnabled + " stagingEnabled=" + stagingEnabled;
        }

        public static string ModuleExtra(string engineId, string[] upgradesApplied)
        {
            int n = upgradesApplied == null ? 0 : upgradesApplied.Length;
            return "engineID=" + (string.IsNullOrEmpty(engineId) ? "-" : engineId)
                 + " upgradesApplied=" + n
                 + (n == 0 ? "" : "(" + string.Join("|", upgradesApplied) + ")");
        }

        /// <summary>
        /// ⭐⭐ `f.FieldInfo.FieldType` — ⛔ WITHOUT THE TYPE YOU CANNOT WRITE THE VALUE BACK, which
        /// makes this the single most useful addition on the FIELD row.
        /// ⚠ `isPersistant` is spelled with an 'a' in KSP's own API; that is not a typo here.
        /// </summary>
        public static string FieldExtra(string fieldType, bool isPersistant, string guiUnits,
                                        string guiFormat, string group, bool guiActive,
                                        bool guiActiveEditor, string controlDetail)
        {
            return "type=" + (string.IsNullOrEmpty(fieldType) ? "?" : fieldType)
                 + " isPersistant=" + isPersistant
                 + " guiUnits=" + (string.IsNullOrEmpty(guiUnits) ? "-" : guiUnits)
                 + " guiFormat=" + (string.IsNullOrEmpty(guiFormat) ? "-" : guiFormat)
                 + " group=" + (string.IsNullOrEmpty(group) ? "-" : group)
                 + " guiActive=" + guiActive + " editor=" + guiActiveEditor
                 + (string.IsNullOrEmpty(controlDetail) ? "" : " " + controlDetail);
        }

        /// <summary>
        /// ⛔⛔ BOTH CONTROLS, NEVER ONE. `DescribeControl` used to collapse `uiControlFlight` and
        /// `uiControlEditor` to whichever was non-null FIRST — so a field editable in the VAB and
        /// locked in flight looked identical to one that was neither. Both are named here.
        /// ⭐ AND AN UNHANDLED CONTROL TYPE EMITS ITS TYPE NAME, never `-`: "there is a control here
        /// and I do not know its shape" and "there is no control here" are different answers, and the
        /// old code gave the same one to both.
        /// </summary>
        public static string ControlCell(string flightTypeName, string editorTypeName)
        {
            bool hasF = !string.IsNullOrEmpty(flightTypeName);
            bool hasE = !string.IsNullOrEmpty(editorTypeName);
            if (!hasF && !hasE) return "-";
            return "flight=" + (hasF ? flightTypeName : "none")
                 + " editor=" + (hasE ? editorTypeName : "none");
        }

        /// <summary>
        /// The detail for one `UI_Control`, given its type name and whatever the glue could extract.
        /// ⛔ WHEN NOTHING WAS EXTRACTED THE TYPE NAME IS STILL EMITTED — that is the whole point:
        /// `UI_ScaleEdit` with no detail still tells the owner the field is a scale editor.
        /// </summary>
        public static string ControlDetail(string which, string typeName, string detail)
        {
            if (string.IsNullOrEmpty(typeName)) return "";
            return which + "=" + typeName
                 + (string.IsNullOrEmpty(detail) ? "" : "[" + detail + "]");
        }

        /// <summary>EVENT's `extra` — ⭐ `guiActiveEditor` and `externalToEVAOnly` say WHERE the
        /// button appears, which is what decides whether we could ever press it.</summary>
        public static string EventExtra(bool guiActive, bool guiActiveUnfocused, bool guiActiveUncommand,
                                        bool guiActiveEditor, bool externalToEVAOnly,
                                        bool requireFullControl, string group)
        {
            return "guiActive=" + guiActive + " unfocused=" + guiActiveUnfocused
                 + " uncommand=" + guiActiveUncommand + " editor=" + guiActiveEditor
                 + " externalToEVAOnly=" + externalToEVAOnly
                 + " requireFullControl=" + requireFullControl
                 + " group=" + (string.IsNullOrEmpty(group) ? "-" : group);
        }

        /// <summary>ACTION's `extra` — ⭐ WHICH ACTION GROUP FIRES IT, currently invisible. The
        /// conductor presses action groups; without this nobody can say what a press would do.</summary>
        public static string ActionExtra(string actionGroup, string defaultActionGroup,
                                         bool activeEditor, bool requireFullControl)
        {
            return "actionGroup=" + (actionGroup ?? "-")
                 + " defaultActionGroup=" + (defaultActionGroup ?? "-")
                 + " activeEditor=" + activeEditor + " requireFullControl=" + requireFullControl;
        }

        // =========================================================================================
        // 8. ⭐ VESSEL — one header row, which did not exist at all
        // =========================================================================================
        //
        // ⚠ `Vessel.launchID` AND `Vessel.missionID` DO NOT EXIST — they are `Part` fields. The header
        // row therefore takes them from the ROOT PART, and says `rootLaunchID` / `rootMissionID` so
        // that nobody reads them as vessel-level facts they are not.

        public static string VesselValue(string vesselName, string vesselType, string situation,
                                         int partCount, double totalMass, int crewCount)
        {
            return "name=" + (vesselName ?? "-") + " type=" + (vesselType ?? "-")
                 + " situation=" + (situation ?? "-") + " parts=" + partCount
                 + " totalMass=" + N(totalMass) + " crew=" + crewCount;
        }

        public static string VesselExtra(uint persistentId, uint rootLaunchId, uint rootMissionId,
                                         int rootPartIndex, int currentStage, int stageCount)
        {
            return "persistentId=" + persistentId + " rootLaunchID=" + rootLaunchId
                 + " rootMissionID=" + rootMissionId + " rootPartIndex=" + rootPartIndex
                 + " currentStage=" + currentStage + " StageManager.StageCount=" + stageCount;
        }
    }
}
