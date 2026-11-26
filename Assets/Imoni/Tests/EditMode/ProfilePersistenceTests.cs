using System;
using System.IO;
using Imoni.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class ProfilePersistenceTests
    {
        private static JObject Config()
        {
#if UNITY_EDITOR
            return JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Imoni/Config/GameConfig.json")));
#else
            return JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json")));
#endif
        }
        [TestCase(null)]
        [TestCase("")]
        [TestCase("{broken")]
        public void MissingOrBrokenSaveUsesConfiguredDefaults(string json)
        {
            JObject config = Config();
            ProfileData profile = ProfilePersistence.Load(config, json);
            Assert.That(profile.Name, Is.EqualTo((string)config["outGame"]["profile"]["defaultName"]));
            Assert.That(profile.FirstTitle, Is.EqualTo((string)config["titles"]["default"]["first"]));
            Assert.That(profile.SoundEnabled, Is.True);
        }
        [Test]
        public void SerializedProfileRetainsAllEditableFieldsAcrossReload()
        {
            JObject config = Config(); var fixture = config["outGame"]["profile"];
            var profile = new ProfileData { Name = "テスト会員", FirstTitle = (string)config["titles"]["firstParts"][1], SecondTitle = (string)config["titles"]["secondParts"][1],
                OwnedFirstTitles = new[] { (string)config["titles"]["firstParts"][1] }, OwnedSecondTitles = new[] { (string)config["titles"]["secondParts"][1] },
                BandColor = (string)fixture["bandColors"][1], OutfitColor = (string)fixture["outfitColors"][1], SoundEnabled = false, TutorialCompleted = true };
            ProfileData restored = ProfilePersistence.Load(config, ProfilePersistence.Save(config, profile));
            Assert.That(restored.Name, Is.EqualTo(profile.Name)); Assert.That(restored.FirstTitle, Is.EqualTo(profile.FirstTitle));
            Assert.That(restored.SecondTitle, Is.EqualTo(profile.SecondTitle)); Assert.That(restored.BandColor, Is.EqualTo(profile.BandColor));
            Assert.That(restored.OutfitColor, Is.EqualTo(profile.OutfitColor)); Assert.That(restored.SoundEnabled, Is.False); Assert.That(restored.TutorialCompleted, Is.True);
        }
        [Test]
        public void NameLimitUsesConfigAndDoesNotSplitUnicodeCharacters()
        {
            JObject config = Config(); config["ui"]["profileNameMaxLength"] = 3;
            ProfileData profile = ProfilePersistence.Normalize(config, new ProfileData { Name = "  山🍲芋煮  " });
            Assert.That(profile.Name, Is.EqualTo("山🍲芋"));
        }
        [Test]
        public void LockedTitlesAndInvalidColorsCannotBecomeSavedSelection()
        {
            JObject config = Config();
            ProfileData profile = ProfilePersistence.Normalize(config, new ProfileData { FirstTitle = "unowned", SecondTitle = "unowned", BandColor = "#000000", OutfitColor = "invalid" });
            Assert.That(profile.FirstTitle, Is.EqualTo((string)config["titles"]["default"]["first"]));
            Assert.That(profile.SecondTitle, Is.EqualTo((string)config["titles"]["default"]["second"]));
            Assert.That(profile.BandColor, Is.EqualTo((string)config["outGame"]["profile"]["defaultBandColor"]));
            Assert.That(profile.OutfitColor, Is.EqualTo((string)config["outGame"]["profile"]["defaultOutfitColor"]));
        }
        [Test]
        public void BlankNameBecomesConfiguredDefault()
        { JObject config = Config(); Assert.That(ProfilePersistence.Normalize(config, new ProfileData { Name = "  " }).Name, Is.EqualTo((string)config["outGame"]["profile"]["defaultName"])); }
    }
}
