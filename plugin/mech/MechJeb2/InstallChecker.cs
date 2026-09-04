// VENDORED - MechJeb2, upstream MuMech/MechJeb2, branch dev, commit
// c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa (2026-08-08).  Pinned by DragonScreen T15a; see plugin/mech/VENDOR.md.
// GPLv3 (plugin/mech/LICENSE.md).  UNMODIFIED except the rename shell: this file's whole
// body is wrapped in `namespace DragonScreen.Mech` (B3 private namespace) and any
// `extern alias JetBrainsAnnotations` is folded to a plain `using`.  No other edit.

namespace DragonScreen.Mech
{
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using KSP.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace MuMech
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    internal class InstallChecker : MonoBehaviour
    {
        protected void Start()
        {
            var assemblies = AssemblyLoader.loadedAssemblies
               .Where(a => a.assembly.GetName().Name == Assembly.GetExecutingAssembly().GetName().Name)
               .Where(a => a.url != "MechJeb2/Plugins")
               .ToList();
            if (assemblies.Any())
            {
                IEnumerable<string> badPaths = assemblies.Select(a => a.path).Select(p =>
                    Uri.UnescapeDataString(new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath)).MakeRelativeUri(new Uri(p)).ToString()
                       .Replace('/', Path.DirectorySeparatorChar)));
                PopupDialog.SpawnPopupDialog(
                    new MultiOptionDialog("InstallCheckerA",
                        null, Localizer.Format("#MechJeb_InstallCheckA_title"), //"Incorrect MechJeb2 Installation"
                        HighLogic.UISkin,
                        new Rect(0.5f, 0.5f, 100f, 100f),
                        new DialogGUIContentSizer(ContentSizeFitter.FitMode.PreferredSize, ContentSizeFitter.FitMode.MinSize),
                        new DialogGUILabel(Localizer.Format("#MechJeb_InstallCheckA_msg") +
                            string.Join("\n",
                                badPaths
                                   .ToArray())), //"MechJeb2 has been installed incorrectly and will not function properly.\nAll MechJeb2 files should be located in KSP like this \n<KSP>\n\tGameData\n\t\tMechJeb2\n\t\t\tParts\n\t\t\tPlugins\n\nDo not move any files from inside the MechJeb2 folder.\n\nIncorrect path(s):\n"
                        new DialogGUIButton("OK", () => { }, true)
                    ), false, HighLogic.UISkin);
            }

            assemblies = AssemblyLoader.loadedAssemblies
               .Where(a => a.assembly.GetName().Name == "MechJebMenuToolbar")
               .ToList();
            if (assemblies.Any())
            {
                IEnumerable<string> badPaths = assemblies.Select(a => a.path).Select(p =>
                    Uri.UnescapeDataString(new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath)).MakeRelativeUri(new Uri(p)).ToString()
                       .Replace('/', Path.DirectorySeparatorChar)));
                PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "InstallCheckerB",
                    Localizer.Format("#MechJeb_InstallCheckB_title"),
                    Localizer.Format("#MechJeb_InstallCheckB_msg") + string.Join("\n", badPaths.ToArray()), "OK", false,
                    HighLogic.UISkin); //"Redundant MechJebMenuToolbar Installation""MechJebMenuToolbar is installed but this version of MechJeb2 already includes support for Blizzy78 Toolbar Plugin.\nPlease delete this dll:\n"
            }
        }
    }
}

}
