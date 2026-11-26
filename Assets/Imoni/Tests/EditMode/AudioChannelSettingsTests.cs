using System;
using System.IO;
using Imoni.Core.Effects;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class AudioChannelSettingsTests
    {
        private static JObject Config()
        {
#if UNITY_EDITOR
            return JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Imoni/Config/GameConfig.json")));
#else
            return JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json")));
#endif
        }

        [TestCase(0)] [TestCase(0.3)] [TestCase(1)]
        public void LegacySoundVolumeMigratesToBothChannelsWithoutChangingMute(double volume)
        {
            JObject config = Config();
            string json = new JObject { ["SoundEnabled"] = false, ["SoundVolume"] = volume }.ToString();
            ProfileData profile = ProfilePersistence.Load(config, json);
            Assert.That(profile.BgmVolume, Is.EqualTo(volume));
            Assert.That(profile.EffectsVolume, Is.EqualTo(volume));
            Assert.That(profile.SoundEnabled, Is.False);
        }

        [Test]
        public void ChannelVolumesPersistIndependentlyAcrossRestart()
        {
            JObject config = Config();
            var profile = new ProfileData { BgmVolume = 0.2, EffectsVolume = 0.8, SoundVolume = 0.5 };
            ProfileData restored = ProfilePersistence.Load(config, ProfilePersistence.Save(config, profile));
            Assert.That(restored.BgmVolume, Is.EqualTo(0.2));
            Assert.That(restored.EffectsVolume, Is.EqualTo(0.8));
            Assert.That(restored.SoundVolume, Is.EqualTo(0.5));
        }

        [Test]
        public void PartiallyMigratedSaveOnlyInheritsVolumeForMissingChannel()
        {
            ProfileData profile = ProfilePersistence.Load(Config(), "{\"SoundVolume\":0.3,\"BgmVolume\":0.7}");
            Assert.That(profile.BgmVolume, Is.EqualTo(0.7));
            Assert.That(profile.EffectsVolume, Is.EqualTo(0.3));
        }

        [Test]
        public void MissingAllVolumeFieldsUsesConfiguredDefaultForBothChannels()
        {
            JObject config = Config(); config["audio"]["defaultVolume"] = 0.4;
            ProfileData profile = ProfilePersistence.Load(config, "{}");
            Assert.That(profile.BgmVolume, Is.EqualTo(0.4));
            Assert.That(profile.EffectsVolume, Is.EqualTo(0.4));
        }

        [Test]
        public void ChannelBoundsAndInvalidValuesUseGameConfig()
        {
            JObject config = Config();
            config["audio"]["minVolume"] = 0.2; config["audio"]["maxVolume"] = 0.8; config["audio"]["defaultVolume"] = 0.4;
            ProfileData clamped = ProfilePersistence.Normalize(config, new ProfileData { BgmVolume = -5, EffectsVolume = 5 });
            Assert.That(clamped.BgmVolume, Is.EqualTo(0.2)); Assert.That(clamped.EffectsVolume, Is.EqualTo(0.8));
            ProfileData invalid = ProfilePersistence.Normalize(config, new ProfileData { BgmVolume = double.PositiveInfinity, EffectsVolume = double.NegativeInfinity, SoundVolume = 0.6 });
            Assert.That(invalid.BgmVolume, Is.EqualTo(0.4)); Assert.That(invalid.EffectsVolume, Is.EqualTo(0.4));
        }

        [Test]
        public void GlobalMuteRetainsBothSavedChannelLevels()
        {
            JObject config = Config();
            var profile = new ProfileData { BgmVolume = 0, EffectsVolume = 0.7, SoundEnabled = false };
            ProfileData restored = ProfilePersistence.Load(config, ProfilePersistence.Save(config, profile));
            restored.SoundEnabled = true;
            restored = ProfilePersistence.Load(config, ProfilePersistence.Save(config, restored));
            Assert.That(restored.SoundEnabled, Is.True);
            Assert.That(restored.BgmVolume, Is.Zero); Assert.That(restored.EffectsVolume, Is.EqualTo(0.7));
        }

        [TestCase(true)] [TestCase(false)]
        public void SteppingOneChannelDoesNotChangeOtherSavedChannel(bool adjustBgm)
        {
            JObject config = Config(); config["audio"]["volumeStep"] = 0.2;
            var profile = new ProfileData { BgmVolume = 0.3, EffectsVolume = 0.7 };
            if (adjustBgm) profile.BgmVolume = ImoniAudioPlan.AdjustVolume(config, profile.BgmVolume, 1);
            else profile.EffectsVolume = ImoniAudioPlan.AdjustVolume(config, profile.EffectsVolume, -1);
            ProfileData restored = ProfilePersistence.Load(config, ProfilePersistence.Save(config, profile));
            Assert.That(restored.BgmVolume, Is.EqualTo(adjustBgm ? 0.5 : 0.3));
            Assert.That(restored.EffectsVolume, Is.EqualTo(adjustBgm ? 0.7 : 0.5));
        }
    }
}
