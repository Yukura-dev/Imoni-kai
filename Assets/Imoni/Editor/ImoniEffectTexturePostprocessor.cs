using UnityEditor;
using UnityEngine;

namespace Imoni.Editor
{
    public sealed class ImoniEffectTexturePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Imoni/Art/Sprites/Effect/") &&
                assetPath != "Assets/Imoni/Art/Sprites/Game/pot_effect.png" &&
                assetPath != "Assets/Imoni/Art/Sprites/Game/fx_sparkle.png") return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
