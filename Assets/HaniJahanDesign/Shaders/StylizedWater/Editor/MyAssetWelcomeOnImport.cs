using System;
using System.Linq;
using UnityEditor;

namespace HaniJahanDesign.StylizedWaterShaderPack
{
    public class MyAssetWelcomeOnImport : AssetPostprocessor
    {
        private const string ShownKey = "HJD.StylizedWaterShaderPack.WelcomeShown";
        private const string AssetRoot = "Assets/HaniJahanDesign/StylizedWaterShaderPack";

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (EditorUserSettings.GetConfigValue(ShownKey) == "1")
            {
                return;
            }

            if (!ContainsAssetRoot(imported) && !ContainsAssetRoot(moved))
            {
                return;
            }

            EditorUserSettings.SetConfigValue(ShownKey, "1");
            EditorApplication.delayCall -= MyAssetWelcome.ShowWindow;
            EditorApplication.delayCall += MyAssetWelcome.ShowWindow;
        }

        private static bool ContainsAssetRoot(string[] paths)
        {
            return paths != null && paths.Any(IsInAssetRoot);
        }

        private static bool IsInAssetRoot(string path)
        {
            return !string.IsNullOrEmpty(path)
                && (string.Equals(path, AssetRoot, StringComparison.Ordinal)
                    || path.StartsWith(AssetRoot + "/", StringComparison.Ordinal));
        }
    }
}
