// VENDORED - MechJeb2, upstream MuMech/MechJeb2, branch dev, commit
// c5a6d8fed6bf458f85c9aafc49c7e282cd4e2ffa (2026-08-08).  Pinned by DragonScreen T15a; see plugin/mech/VENDOR.md.
// GPLv3 (plugin/mech/LICENSE.md).  UNMODIFIED except the rename shell: this file's whole
// body is wrapped in `namespace DragonScreen.Mech` (B3 private namespace) and any
// `extern alias JetBrainsAnnotations` is folded to a plain `using`.  No other edit.

namespace DragonScreen.Mech
{
using System.Collections;
using UnityEngine;

namespace MuMech
{
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class MechJebBundlesManager : MonoBehaviour
    {
        private const string shaderBundle = "shaders.bundle";
        private string shaderPath;

        private const string diffuseAmbientName = "Assets/Shaders/MJ_DiffuseAmbiant.shader";
        private const string diffuseAmbientIgnoreZName = "Assets/Shaders/MJ_DiffuseAmbiantIgnoreZ.shader";

        public static Shader diffuseAmbient;
        public static Shader diffuseAmbientIgnoreZ;
        public static Texture2D comboBoxBackground;

        private void Awake()
        {
            string gameDataPath = KSPUtil.ApplicationRootPath + "/GameData/MechJeb2/Bundles/";
            shaderPath = gameDataPath + shaderBundle;
        }

        private IEnumerator Start()
        {
            // We do this in MainMenu because something is going on in that scene that kills anything loaded with a bundle
            if (diffuseAmbient)
                MechJebCore.Print("Shaders already loaded");

            MechJebCore.Print("Loading Shaders Bundles");

            // Load the font asset bundle
            AssetBundleCreateRequest bundleLoadRequest = AssetBundle.LoadFromFileAsync(shaderPath);
            yield return bundleLoadRequest;

            AssetBundle assetBundle = bundleLoadRequest.assetBundle;
            if (assetBundle == null)
            {
                MechJebCore.Print("Failed to load AssetBundle " + shaderPath);
                yield break;
            }

            AssetBundleRequest assetLoadRequest = assetBundle.LoadAssetAsync<Shader>(diffuseAmbientName);
            yield return assetLoadRequest;
            diffuseAmbient = assetLoadRequest.asset as Shader;

            assetLoadRequest = assetBundle.LoadAssetAsync<Shader>(diffuseAmbientIgnoreZName);
            yield return assetLoadRequest;
            diffuseAmbientIgnoreZ = assetLoadRequest.asset as Shader;

            assetBundle.Unload(false);
            MechJebCore.Print("Loaded Shaders Bundles");

            comboBoxBackground = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            comboBoxBackground.wrapMode = TextureWrapMode.Clamp;

            for (int x = 0; x < comboBoxBackground.width; x++)
            for (int y = 0; y < comboBoxBackground.height; y++)
            {
                if (x == 0 || x == comboBoxBackground.width - 1 || y == 0 || y == comboBoxBackground.height - 1)
                    comboBoxBackground.SetPixel(x, y, new Color(0, 0, 0, 1));
                else
                    comboBoxBackground.SetPixel(x, y, new Color(0.05f, 0.05f, 0.05f, 0.95f));
            }

            comboBoxBackground.Apply();
        }
    }
}

}
