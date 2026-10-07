#nullable enable
using UnityEditor;

namespace SanGuo.Editor
{
    /// <summary>Resources/UiSkin 下的貼圖（取自 TS6Client 的 UISprite）：不縮放成 2 的次方、不產生 mipmap，9 宮格才不會變形。</summary>
    public sealed class UiSkinImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/UiSkin/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
