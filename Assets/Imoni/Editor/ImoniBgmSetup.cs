using System;
using System.Linq;
using Imoni.Core.Audio;
using Imoni.Game.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Imoni.Editor
{
    /// <summary>One config catalog owns all production BGM references and import settings.</summary>
    internal static class ImoniBgmSetup
    {
        internal static void EnsureAssets(UIRuntimeAssets assets, JObject config)
        {
            JObject audio = (JObject)config["audio"];
            float quality = audio["bgmImportQuality"].Value<float>();
            assets.backgroundMusicTracks = ((JArray)audio["bgmCatalog"]).Select(item => new NamedBgmTrack
            {
                key = item["key"].Value<string>(),
                intro = Import(item["intro"]?.Value<string>(), quality),
                loop = Import(item["loop"].Value<string>(), quality),
                gain = item["gain"].Value<float>()
            }).ToArray();
            if (assets.backgroundMusicTracks.Select(track => track.key).Distinct().Count() != assets.backgroundMusicTracks.Length)
                throw new InvalidOperationException("BGM catalog keys must be unique.");
            var plan = new BgmLoopPlan(config);
            NamedBgmTrack main = assets.backgroundMusicTracks.Single(track => track.key == "bgm_game_main");
            if (main.intro.samples != plan.IntroSampleCount || main.loop.samples != plan.LoopSampleCount ||
                main.intro.frequency != plan.SampleRate || main.loop.frequency != plan.SampleRate)
                throw new InvalidOperationException("Game BGM WAV sample boundaries differ from GameConfig.audio.gameMain.");
        }

        private static AudioClip Import(string path, float quality)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var importer = AssetImporter.GetAtPath(path) as AudioImporter
                ?? throw new InvalidOperationException("Missing BGM clip: " + path);
            AudioImporterSampleSettings sample = importer.defaultSampleSettings;
            // Vorbis on disk, decoded before use: sample-accurate scheduling and gapless loop playback.
            bool changed = sample.loadType != AudioClipLoadType.DecompressOnLoad ||
                sample.compressionFormat != AudioCompressionFormat.Vorbis ||
                sample.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate ||
                !Mathf.Approximately(sample.quality, quality) || !sample.preloadAudioData || importer.loadInBackground;
            if (changed)
            {
                sample.loadType = AudioClipLoadType.DecompressOnLoad;
                sample.compressionFormat = AudioCompressionFormat.Vorbis;
                sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                sample.quality = quality;
                sample.preloadAudioData = true;
                importer.defaultSampleSettings = sample;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path)
                ?? throw new InvalidOperationException("BGM import failed: " + path);
        }
    }
}
