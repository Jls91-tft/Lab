using UnityEditor;
using UnityEngine;

namespace CepaCero.EditorTools
{
    /// <summary>
    /// Pixel-art import settings for the generated atlas (D-018, tools/artgen): no
    /// filtering, no compression and no mipmaps, so every pixel keeps its palette colour.
    /// </summary>
    public sealed class ArtImportSettings : AssetPostprocessor
    {
        private const string ArtFolder = "Assets/Resources/Cepa/Art/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtFolder))
            {
                return;
            }
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
        }
    }
}
