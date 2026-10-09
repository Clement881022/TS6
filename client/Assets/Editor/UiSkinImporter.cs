#nullable enable
using UnityEditor;

namespace SanGuo.Editor
{
    public sealed class UiSkinImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            bool skin = assetPath.Contains("/Resources/UiSkin/") || assetPath.Contains("/Resources/StrategySkin/") || assetPath.Contains("/Resources/ChibiSkin/");
            if (!skin && !assetPath.Contains("/Resources/HeroArt/") && !assetPath.Contains("/Resources/UiBg/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 4096;
            importer.textureCompression = skin ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
        }
    }
}
