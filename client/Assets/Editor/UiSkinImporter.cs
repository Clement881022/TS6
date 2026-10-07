#nullable enable
using UnityEditor;

namespace SanGuo.Editor
{
    /// <summary>Resources/UiSkin、HeroArt、UiBg 下的貼圖（取自 TS6Client）：不縮放成 2 的次方、不產生 mipmap，9 宮格才不會變形。</summary>
    public sealed class UiSkinImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            bool skin = assetPath.Contains("/Resources/UiSkin/");
            if (!skin && !assetPath.Contains("/Resources/HeroArt/") && !assetPath.Contains("/Resources/UiBg/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = skin ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
        }
    }
}
