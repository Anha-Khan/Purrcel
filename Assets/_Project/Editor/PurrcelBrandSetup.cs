using System.IO;
using UnityEditor;
using UnityEngine;

namespace CatCourier.Editor
{
    /// <summary>
    /// Wires the Purrcel brand into Player Settings.
    ///
    /// This was a gap, not a feature: every Android icon slot in ProjectSettings.asset was
    /// empty, so the store listing was showing Unity's default icon. HubCourierSquare.png is
    /// a full-bleed painted street scene — atmospheric, but the cat is small and off to one
    /// side, and at 48px it is a brown blur. A store icon has to survive being shrunk.
    ///
    /// API note, learned the hard way twice. This project builds with 2022.3.62f3
    /// (Tools/Common.ps1 pins the CLI), where the icon API is
    /// SetIconsForTargetGroup(BuildTargetGroup, Texture2D[]) — one kind-agnostic call.
    /// The three-argument form takes a single IconKind, not an array, and the
    /// NamedBuildTarget overload only arrived in Unity 6. The two-argument call is the only
    /// form that compiles on the editor this project actually uses, and it assigns every
    /// kind at once, which is what a single-mark launcher icon wants anyway.
    ///
    /// Adaptive foreground/background layers are exported to disk alongside the icon. Drag
    /// them into Player Settings by hand if a Play listing needs them; there is no
    /// IconKind member for them, so they cannot be assigned from script.
    /// </summary>
    public static class PurrcelBrandSetup
    {
        private const string BrandPath = "Assets/_Project/Brand";

        [MenuItem("Purrcel/Setup/Apply Brand Icons", priority = 1)]
        public static void ApplyBrandIcons()
        {
            var icon = Load($"{BrandPath}/icon-512.png");
            if (icon == null)
            {
                Debug.LogWarning(
                    $"[PurrcelBrand] No brand icon at {BrandPath}/icon-512.png. " +
                    "Run Tools/brand/make-brand-assets.py first, or the app keeps Unity's default icon.");
                return;
            }

            var icons = new[] { icon };
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, icons);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Standalone, icons);

            AssetDatabase.SaveAssets();
            Debug.Log("[PurrcelBrand] Applied the Purrcel mark as the Android and Standalone icon. " +
                      $"Adaptive layers: {BrandPath}/icon-foreground-512.png and icon-background-512.png.");
        }

        private static Texture2D Load(string assetPath)
        {
            if (!File.Exists(assetPath))
            {
                return null;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                // Launcher icons must not be mipmapped, or the ears soften at small sizes.
                // The project sets this globally already; doing it per-asset as well means
                // the icons stay correct if that policy is ever relaxed.
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }
    }
}
