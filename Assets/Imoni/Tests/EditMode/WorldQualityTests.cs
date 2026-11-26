using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Imoni.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class WorldQualityTests
    {
        private static GameConfig Config(Action<JObject> change = null)
        {
#if UNITY_EDITOR
            string path = Path.Combine(UnityEngine.Application.dataPath, "Imoni", "Config", "GameConfig.json");
#else
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            var json = JObject.Parse(File.ReadAllText(path));
            change?.Invoke(json);
            return GameConfig.FromJson(json.ToString());
        }

        [Test]
        public void NeutralEnvironmentPreservesConfiguredProbabilityMass()
        {
            GameConfig config = Config();
            var roller = new QualityRoller(config);
            var distribution = roller.Distribution(1, 0, "plain", "negi");
            BigInteger top = distribution.Weights.Last();
            Assert.That(top * config.FixedScale, Is.EqualTo(distribution.TotalWeight * roller.TopChanceRaw(0)));
            Assert.That(distribution.Weights[0] * 30, Is.EqualTo(distribution.Weights[1] * 62));
        }

        [Test]
        public void BiomeMaterialAndIndividualNodeModifiersEachFavorHigherQualities()
        {
            GameConfig config = Config();
            var roller = new QualityRoller(config);
            var neutral = roller.Distribution(3, 0, "plain", "negi");
            var biome = roller.Distribution(3, 0, "forest", "wood");
            var materialConfig = Config(json => json["materials"][1]["qualityMultiplier"] = 1.3m);
            var material = new QualityRoller(materialConfig).Distribution(3, 0, "plain", "negi");
            var node = roller.Distribution(3, 0, "plain", "negi", 1.3m);
            foreach (var modified in new[] { biome, material, node })
                Assert.That(modified.Weights.Last() * neutral.TotalWeight,
                    Is.GreaterThan(neutral.Weights.Last() * modified.TotalWeight));
            Assert.That(material.Weights.Last() * node.TotalWeight, Is.EqualTo(node.Weights.Last() * material.TotalWeight));
        }

        [Test]
        public void ModifiedQualityRollIsDeterministicAndNeverUnlocksFutureQualities()
        {
            var roller = new QualityRoller(Config());
            var a = new Rng(444); var b = new Rng(444);
            for (int index = 0; index < 200; index++)
            {
                QualityDefinition value = roller.Roll(a, 2, 3, "mountain", "sansai", 1.2m);
                Assert.That(value.Id, Is.EqualTo(roller.Roll(b, 2, 3, "mountain", "sansai", 1.2m).Id));
                Assert.That(value.UnlockRound, Is.LessThanOrEqualTo(2));
            }
        }

        [Test]
        public void LuckCoefficientHasOneSourceAndNoArtificialTopProbabilityCap()
        {
            GameConfig config = Config(json => json["upgrades"]["items"][3]["effectPerLevel"] = 0.25m);
            Assert.That(config.LuckPerLevel, Is.EqualTo(config.Upgrade("luck").EffectPerLevel));
            var roller = new QualityRoller(config);
            Assert.That(roller.TopChanceRaw(4), Is.EqualTo(config.FixedScale * 16 / 100));
            var rng = new Rng(49);
            for (int i = 0; i < 30; i++)
                Assert.That(roller.Roll(rng, 5, int.MaxValue, "mountain", "sansai", 0.01m).Id, Is.EqualTo("ultimate"));
        }

        [Test]
        public void InvalidQualityAndBiomeModifiersFailBeforeGathering()
        {
            Assert.Throws<ArgumentException>(() => Config(json => json["gathering"]["biomeModifiers"][0]["qualityMultiplier"] = 0));
            Assert.Throws<ArgumentException>(() => Config(json => json["materials"][0]["biomes"][0] = "missing"));
            Assert.Throws<ArgumentOutOfRangeException>(() => new QualityRoller(Config()).Distribution(1, 0, "plain", "negi", 0));
        }
    }
}
