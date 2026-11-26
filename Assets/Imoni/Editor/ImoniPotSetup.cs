using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Imoni.Editor
{
    /// <summary>Derives reusable body/steam pieces from the user-selected prototype painting.</summary>
    public static class ImoniPotSetup
    {
        private const string ConfigPath = "Assets/Imoni/Config/GameConfig.json";

        public static void EnsureAssets()
        {
            JObject data = JObject.Parse(File.ReadAllText(ConfigPath));
            JObject settings = data["setup"]?["potArtwork"] as JObject
                ?? throw new InvalidOperationException("setup.potArtwork is missing.");
            string sourcePath = settings.Value<string>("source");
            string folder = settings.Value<string>("outputFolder");
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath) ||
                string.IsNullOrWhiteSpace(folder) || !folder.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("setup.potArtwork requires an existing source and an Assets output folder.");
            EnsureFolder(folder);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!ImageConversion.LoadImage(source, File.ReadAllBytes(sourcePath)))
                    throw new InvalidOperationException("Unable to decode pot artwork: " + sourcePath);
                WriteCrop(source, (JArray)settings["campfireCrop"], folder + "/home_pot_campfire.png");
                WriteCrop(source, (JArray)settings["plainCrop"], folder + "/home_pot_plain.png");
                var steam = (JArray)settings["steamCrops"];
                if (steam == null || steam.Count == 0) throw new InvalidOperationException("setup.potArtwork.steamCrops is missing.");
                for (int index = 0; index < steam.Count; index++)
                    WriteCrop(source, (JArray)steam[index], folder + "/home_pot_steam_" + index + ".png");
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        private static void WriteCrop(Texture2D source, JArray rectangle, string path)
        {
            if (rectangle == null || rectangle.Count != 4)
                throw new InvalidOperationException("A pot crop must be [left, top, width, height]: " + path);
            int[] r = rectangle.Values<int>().ToArray();
            if (r[0] < 0 || r[1] < 0 || r[2] <= 0 || r[3] <= 0 || r[0] + r[2] > source.width || r[1] + r[3] > source.height)
                throw new InvalidOperationException("A pot crop extends outside the supplied image: " + path);
            var crop = new Texture2D(r[2], r[3], TextureFormat.RGBA32, false);
            try
            {
                // Configuration uses image/GUI top-left coordinates; Texture2D uses bottom-left.
                crop.SetPixels(source.GetPixels(r[0], source.height - r[1] - r[3], r[2], r[3]));
                crop.Apply(false, false);
                byte[] png = crop.EncodeToPNG();
                if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(png)) File.WriteAllBytes(path, png);
            }
            finally { UnityEngine.Object.DestroyImmediate(crop); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool changed = importer.textureType != TextureImporterType.Default || importer.mipmapEnabled ||
                !importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Bilinear || importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.npotScale != TextureImporterNPOTScale.None;
            if (!changed) return;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent)) throw new InvalidOperationException("Invalid generated pot folder: " + path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
