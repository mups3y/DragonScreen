/*
 * MechHostTest - register T15b. The headless half of "host one MechJebCore, and suppress the GUI".
 *
 * WHAT CAN AND CANNOT BE PROVED WITHOUT THE GAME. Nothing here loads a core: that needs KSP, which
 * is `install` + glass time, and both are separate owner gates. What CAN be proved headlessly is
 * every claim that is really a claim about TEXT - the blacklist's substring behaviour against the
 * actual pinned tree, the shipped cfg agreeing with the code, the three [KSPAddon]s being out of
 * the compile, and the tune shipping intact with node names that still resolve. Those are exactly
 * the claims that would otherwise rot silently across a re-pin, so they are the ones worth pinning.
 *
 * The rest - a core actually loading, no second UI appearing, the tune landing on live modules -
 * is the glass checklist on T15b's register line, and this file does not pretend to cover it.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using DragonScreen.Pure;

public static class MechHostTest
{
    static int checks, failures;

    static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (!ok) { failures++; Console.WriteLine("  FAIL " + what + (detail == "" ? "" : "  [" + detail + "]")); }
    }

    // plugin/build/DragonScreenTest.exe -> ".." = plugin/, "../.." = the repo root. Same
    // locate-by-assembly idiom ActuationTest/LayoutTest already use.
    static string Repo(params string[] parts)
    {
        var bits = new List<string> {
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
            "..", ".." };
        bits.AddRange(parts);
        return Path.GetFullPath(Path.Combine(bits.ToArray()));
    }

    public static int Run()
    {
        Console.WriteLine("DragonScreen T15b MechJeb host tests (blacklist substring rule, addon exclusion, shipped tune)");
        checks = failures = 0;

        // ---- (0) the substring rule itself, on cases that do not need the tree ----------
        // This is what MechJebCore.cs:753 does: `blacklist.Contains(t.Name)`.
        Check("substring rule: an exact entry is refused",
              MechProfile.CaughtBy("Alpha,Beta", new[] { "Alpha" }).Count == 1, "");
        Check("substring rule: an unrelated name is not",
              MechProfile.CaughtBy("Alpha,Beta", new[] { "Gamma" }).Count == 0, "");
        Check("substring rule: A NAME THAT IS A SUBSTRING OF AN ENTRY IS ALSO REFUSED - the trap",
              MechProfile.CaughtBy("AlphaWindow", new[] { "Alpha" }).Count == 1, "");
        Check("substring rule: an empty blacklist refuses nothing",
              MechProfile.CaughtBy("", new[] { "Alpha" }).Count == 0, "");
        Check("substring rule: a null name set is not a crash",
              MechProfile.CaughtBy("Alpha", null).Count == 0, "");

        // ---- (1) the blacklist, against the ACTUAL pinned tree --------------------------
        string mech = Repo("plugin", "mech");
        if (!Directory.Exists(mech))
        {
            Console.WriteLine("  (plugin/mech not vendored here - tree-dependent checks skipped)");
        }
        else
        {
            List<string> modules = ComputerModuleNames(Path.Combine(mech, "MechJeb2"));
            Check("scan found the pinned tree's ComputerModules", modules.Count > 40,
                  "found " + modules.Count);

            List<string> caught = MechProfile.CaughtBy(MechProfile.Blacklist, modules);
            var intended = new List<string>(MechProfile.Refused);
            intended.Sort(StringComparer.Ordinal);

            Check("blacklist refuses EXACTLY the modules it names - no collateral",
                  string.Join(",", caught.ToArray()) == string.Join(",", intended.ToArray()),
                  "refused=[" + string.Join(",", caught.ToArray()) + "]");

            foreach (string n in MechProfile.Refused)
                Check("blacklist entry names a module that exists in the pin: " + n,
                      modules.Contains(n), "");

            // The half that actually bites: a careless entry silently removing a module the
            // core assigns to one of its own fields, or one that live code dereferences.
            foreach (string n in MechProfile.MustSurvive)
            {
                Check("MUST SURVIVE and does exist in the pin: " + n, modules.Contains(n), "");
                Check("MUST SURVIVE and is not refused: " + n,
                      MechProfile.CaughtBy(MechProfile.Blacklist, new[] { n }).Count == 0, "");
            }

            // ---- (2) the three [KSPAddon]s are out of the compile -----------------------
            // Vendored (the pin stays complete) but excluded in build.py. Both halves checked:
            // still present in the tree, and still named in the exclusion list.
            string[] addons = { "CompatibilityChecker.cs", "InstallChecker.cs", "MechjebBundlesManager.cs" };
            string buildpy = File.Exists(Repo("plugin", "build.py"))
                           ? File.ReadAllText(Repo("plugin", "build.py")) : "";
            foreach (string a in addons)
            {
                Check("[KSPAddon] file is still VENDORED (the pin is complete): " + a,
                      File.Exists(Path.Combine(Path.Combine(mech, "MechJeb2"), a)), "");
                Check("[KSPAddon] file is EXCLUDED from the compile: " + a,
                      buildpy.ToLowerInvariant().Contains("mechjeb2/" + a.ToLowerInvariant()), "");
            }
            Check("the bundles substitution ships in our own _dragonscreen/",
                  File.Exists(Path.Combine(mech, Path.Combine("_dragonscreen", "_BundlesManager.cs"))), "");
        }

        // ---- (3) the shipped cfg and the code agree ------------------------------------
        string partCfg = Repo("plugin", "GameData", "DragonScreen", "DragonScreen.cfg");
        Check("the part cfg exists", File.Exists(partCfg), partCfg);
        if (File.Exists(partCfg))
        {
            string txt = File.ReadAllText(partCfg);
            Match m = Regex.Match(txt, @"^\s*blacklist\s*=\s*(.+?)\s*$", RegexOptions.Multiline);
            Check("the part cfg carries a blacklist line", m.Success, "");
            if (m.Success)
                Check("the SHIPPED blacklist is byte-identical to MechProfile.Blacklist",
                      m.Groups[1].Value == MechProfile.Blacklist,
                      "cfg=[" + m.Groups[1].Value + "]");

            Check("the core is hosted under a name that cannot collide with a user's MechJeb2",
                  txt.Contains("name = DragonMechJebCore"), "");
            Check("NO node names the ambiguous `MechJebCore`",
                  !Regex.IsMatch(txt, @"^\s*name\s*=\s*MechJebCore\s*$", RegexOptions.Multiline), "");
            // MechJeb's own vendored patch adds a core to EVERY command pod. Ours must not.
            Check("the core is patched onto the Dragon parts only, never all command pods",
                  !txt.Contains("@PART[*]") && txt.Contains("TE_18_DRAGONV2_POD"), "");
        }

        // ---- (4) the tune ships inside the mod, intact ----------------------------------
        string shipped = Repo("plugin", "GameData", "DragonScreen", "PluginData", MechProfile.TuneFileName);
        string source = Repo("docs", "reference", MechProfile.TuneFileName);
        Check("the tune ships in the mod's PluginData (§B12.1: from the mod, never the user)",
              File.Exists(shipped), shipped);
        if (File.Exists(shipped) && File.Exists(source))
            Check("the shipped tune is identical to the repo reference copy (C7.1)",
                  File.ReadAllText(shipped) == File.ReadAllText(source), "");

        if (File.Exists(shipped) && Directory.Exists(mech))
        {
            List<string> modules = ComputerModuleNames(Path.Combine(mech, "MechJeb2"));
            var orphans = new List<string>(MechProfile.KnownOrphanTuneNodes);
            foreach (string node in TuneNodesWithValues(shipped))
            {
                // A node carrying VALUES whose module no longer exists is a silent tuning loss -
                // exactly what a re-pin's rename would cause. Empty orphans are recorded and fine.
                Check("tune node with values still resolves to a module in the pin: " + node,
                      modules.Contains(node), "orphaned - values would be dropped");
                Check("a node carrying values is not on the known-EMPTY orphan list: " + node,
                      !orphans.Contains(node), "");
            }
        }

        // ---- (5) T15d: the two tune COUNTS, derived rather than remembered --------------
        // T15b's glass row 3 predicted the log would read "11 module(s)"; the glass read 51. The
        // checklist was wrong, not the loader - see MechProfile's note. Both numbers are re-derived
        // here from the cfg and the pinned tree so a re-pin cannot silently move what the capsule
        // session is told to expect.
        if (File.Exists(shipped) && Directory.Exists(mech))
        {
            Check("the tune's value-carrying node count is what MechProfile pins",
                  TuneNodesWithValues(shipped).Count == MechProfile.TuneNodesCarryingValues,
                  "derived=" + TuneNodesWithValues(shipped).Count);

            List<string> modules = ComputerModuleNames(Path.Combine(mech, "MechJeb2"));
            int autoConstructed = 0;
            foreach (string node in TuneNodes(shipped))
            {
                if (!modules.Contains(node)) continue;                       // orphan - no module
                if (MechProfile.Blacklist.IndexOf(node, StringComparison.Ordinal) >= 0) continue;
                // MechJebCore.cs:751-753 hard-excludes this one from auto-construction; it exists
                // only as the windows AddDefaultWindows makes, counted separately below.
                if (node == "MechJebModuleCustomInfoWindow") continue;
                autoConstructed++;
            }

            int windows = DefaultWindowCount(Path.Combine(mech, "MechJeb2"));
            Check("AddDefaultWindows still makes the number of windows MechProfile pins",
                  windows == MechProfile.DefaultCustomWindows, "derived=" + windows);
            Check("the expected 'N module(s) matched a node' is what MechProfile pins",
                  autoConstructed + windows == MechProfile.ExpectedTuneModulesApplied,
                  "derived=" + (autoConstructed + windows) + " (" + autoConstructed + " types + "
                  + windows + " default windows)");
        }

        Console.WriteLine("  " + checks + " checks, " + failures + " failed");
        return failures > 0 ? 1 : 0;
    }

    /// <summary>
    /// Every non-abstract class in the pinned tree that descends from ComputerModule - i.e. every
    /// type MechJebCore.LoadComputerModules would try to construct. Read from the SOURCE so it
    /// cannot drift from what actually ships.
    /// </summary>
    static List<string> ComputerModuleNames(string dir)
    {
        var baseOf = new Dictionary<string, string>();
        var isAbstract = new Dictionary<string, bool>();
        var rx = new Regex(@"^[ \t]*(?:(?:public|internal|private|protected|sealed|abstract|static|partial)\s+)*class\s+(\w+)\s*:\s*([\w\.]+)",
                           RegexOptions.Multiline);
        if (Directory.Exists(dir))
        {
            foreach (string f in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                string txt = File.ReadAllText(f);
                foreach (Match m in rx.Matches(txt))
                {
                    baseOf[m.Groups[1].Value] = m.Groups[2].Value;
                    isAbstract[m.Groups[1].Value] = m.Value.Contains("abstract");
                }
            }
        }
        var outNames = new List<string>();
        foreach (var kv in baseOf)
        {
            if (isAbstract[kv.Key]) continue;
            string cur = kv.Value;
            for (int hops = 0; hops < 16 && cur != null; hops++)
            {
                if (cur == "ComputerModule") { outNames.Add(kv.Key); break; }
                string next;
                cur = baseOf.TryGetValue(cur, out next) ? next : null;
            }
        }
        outNames.Sort(StringComparer.Ordinal);
        return outNames;
    }

    /// <summary>
    /// EVERY distinct top-level node name in a MechJeb settings cfg, empty nodes included -
    /// which is the set MechHost.ApplyTune tests each constructed module against.
    /// </summary>
    static List<string> TuneNodes(string path)
    {
        var outNames = new List<string>();
        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            if (!Regex.IsMatch(lines[i], @"^[A-Za-z]")) continue;
            string name = lines[i].Trim();
            if (i + 1 >= lines.Length || lines[i + 1].Trim() != "{") continue;
            if (!outNames.Contains(name)) outNames.Add(name);
        }
        return outNames;
    }

    /// <summary>
    /// How many MechJebModuleCustomInfoWindow instances AddDefaultWindows creates, read out of the
    /// vendored source - one per CreateWindowFromSharingString call in its body. Read rather than
    /// remembered so a re-pin that adds or drops a preset moves the expectation with it.
    /// </summary>
    static int DefaultWindowCount(string dir)
    {
        string f = Path.Combine(dir, "MechJebModuleCustomInfoWindow.cs");
        if (!File.Exists(f)) return -1;
        string txt = File.ReadAllText(f);
        int at = txt.IndexOf("public void AddDefaultWindows()", StringComparison.Ordinal);
        if (at < 0) return -1;
        int end = txt.IndexOf("\n        }", at, StringComparison.Ordinal);
        if (end < 0) return -1;
        return Regex.Matches(txt.Substring(at, end - at), @"CreateWindowFromSharingString\(").Count;
    }

    /// <summary>Top-level node names in a MechJeb settings cfg that actually carry key/value lines.</summary>
    static List<string> TuneNodesWithValues(string path)
    {
        var outNames = new List<string>();
        string[] lines = File.ReadAllLines(path);
        for (int i = 0; i < lines.Length; i++)
        {
            if (!Regex.IsMatch(lines[i], @"^[A-Za-z]")) continue;
            string name = lines[i].Trim();
            int j = i + 1;
            if (j >= lines.Length || lines[j].Trim() != "{") continue;
            int depth = 1; bool hasValue = false;
            for (j++; j < lines.Length && depth > 0; j++)
            {
                string s = lines[j].Trim();
                if (s == "{") depth++;
                else if (s == "}") depth--;
                else if (depth > 0 && s.Length > 0) hasValue = true;
            }
            if (hasValue && !outNames.Contains(name)) outNames.Add(name);
            i = j - 1;
        }
        return outNames;
    }
}
