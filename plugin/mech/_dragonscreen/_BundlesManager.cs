// ─────────────────────────────────────────────────────────────────────────────────────────
//  DragonScreen's, not MechJeb's.  A BUILD SUBSTITUTION, exactly like _JetBrainsAnnotations.cs,
//  and recorded in VENDOR.md §3.1.  Written by register task T15b, 2026-09-05.
//
//  ⛔ NOTHING VENDORED WAS EDITED TO MAKE THIS WORK.  §B12.1's "rename shell only" holds:
//  `MechJeb2/MechjebBundlesManager.cs` is still in the tree byte-for-byte as upstream wrote
//  it (plus T15a's namespace wrap).  It is simply not in the COMPILE SET any more — the same
//  distinction T15a drew for MechJebKos/ and MechJebLibTest/, which are vendored whole and
//  not compiled.  §B12.1a's full-tree rule governs what is VENDORED, not what is compiled.
//
//  ---- WHY THE ORIGINAL CANNOT SHIP ----
//  Upstream's file is `[KSPAddon(KSPAddon.Startup.MainMenu, false)]`.  KSP instantiates
//  [KSPAddon] classes by SCANNING EVERY ASSEMBLY IN GameData — nobody has to attach anything,
//  and a MechJebCore is not required.  On every visit to the main menu it would run
//
//      AssetBundle.LoadFromFileAsync(<KSP>/GameData/MechJeb2/Bundles/shaders.bundle)
//
//  which is a path THIS MOD DOES NOT SHIP and, for a user who also runs the real MechJeb2, a
//  path that is ALREADY LOADED BY THEM.  Both outcomes are bad and the second is the worse:
//  Unity refuses a second load of the same AssetBundle files, so we would be printing errors
//  into someone else's mod.  §B12.1a is unambiguous that the ported GUI must be "vendored but
//  never registered/shown" — ported ≠ enabled — and an addon that registers itself is exactly
//  what that forbids.  It is one of three; the other two (CompatibilityChecker, InstallChecker)
//  are pure popups with no compiled dependents and are simply dropped.
//
//  ---- WHY A SUBSTITUTE AND NOT JUST A DELETION ----
//  Two compiled files read its statics: `GuiUtils.cs:905-906` (the combo-box background) and
//  `MechJebModuleDebugArrows.cs:319-320,614` (the see-through arrow shaders).  Dropping the
//  file alone is CS0103.  So the three fields stay and the loader goes.
//
//  ---- AND null IS NOT A DEGRADATION ----
//  These three fields are only ever non-null if the bundle loaded.  In DragonScreen's layout
//  there is no `GameData/MechJeb2/Bundles/` to load from, so upstream's own file would have hit
//  its `if (assetBundle == null) yield break` and left all three null anyway.  This substitute
//  reproduces that end state without the file access, the log noise or the collision.  Both
//  consumers are GUI/debug paths that this build never runs: the GUI is suppressed at the
//  enable level (src/MechHost.cs) and MechJebModuleDebugArrows is refused at construction
//  (pure/MechProfile.cs).
// ─────────────────────────────────────────────────────────────────────────────────────────
using UnityEngine;

namespace DragonScreen.Mech
{
    namespace MuMech
    {
        /// <summary>
        /// Stands in for upstream's <c>MechJebBundlesManager</c> so the two files that read its
        /// statics still compile, WITHOUT bringing its <c>[KSPAddon]</c> — and therefore its
        /// asset-bundle load out of a MechJeb2 install this mod does not own — into the build.
        /// The fields keep the values upstream's own code would have left them at in this
        /// layout: null, because there is no bundle here to load.
        /// </summary>
        public static class MechJebBundlesManager
        {
            /// <summary>MJ_DiffuseAmbiant. Null: no shader bundle is shipped or loaded.</summary>
            public static Shader diffuseAmbient;

            /// <summary>MJ_DiffuseAmbiantIgnoreZ. Null, same reason.</summary>
            public static Shader diffuseAmbientIgnoreZ;

            /// <summary>Combo-box background texture. Null: built only by the bundle loader.</summary>
            public static Texture2D comboBoxBackground;
        }
    }
}
