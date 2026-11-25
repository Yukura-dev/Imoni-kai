using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Imoni.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class RulesMathTests
    {
        private static string ConfigJson()
        {
#if UNITY_EDITOR
            return File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Imoni", "Config", "GameConfig.json"));
#else
            return File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json"));
#endif
        }

        private static GameConfig Config(Action<JObject> edit = null)
        {
            var json = JObject.Parse(ConfigJson());
            edit?.Invoke(json);
            return GameConfig.FromJson(json.ToString());
        }

        [Test]
        public void ConfigLoadsBigIntegerAmountsWithoutDoublePrecisionLoss()
        {
            string huge = "123456789012345678901234567890123456789012345678901";
            GameConfig config = Config(json => json["economy"]["startFunds"] = huge);
            Assert.That(config.StartFunds, Is.EqualTo(BigInteger.Parse(huge)));
            Assert.That(config.DefaultObjectives.Required.Count, Is.EqualTo(3));
        }

        [TestCase("economy.startFunds", -1)]
        [TestCase("round.durationSec", 0)]
        [TestCase("round.bonusRevealSec", 300)]
        [TestCase("numeric.fixedScale", 0)]
        [TestCase("qualityRoll.topBaseChance", 2)]
        [TestCase("upgrades.items[3].effectPerLevel", -1)]
        [TestCase("streak.divisor", 0)]
        [TestCase("format.unitBase", 1)]
        [TestCase("format.decimalPlaces", -1)]
        public void InvalidConfigFailsBeforeStartingGame(string path, int value)
        {
            Assert.Throws<ArgumentException>(() => Config(json => json.SelectToken(path).Replace(value)));
        }

        [Test]
        public void MissingNumericValueDoesNotSilentlyUseADefault()
        {
            Assert.Throws<ArgumentException>(() => Config(json => ((JObject)json["round"]).Remove("durationSec")));
        }

        [Test]
        public void DuplicateIdsAndUnknownPurchasedQualityAreRejected()
        {
            Assert.Throws<ArgumentException>(() => Config(json => json["materials"][1]["id"] = "wood"));
            Assert.Throws<ArgumentException>(() => Config(json => json["economy"]["purchasedQualityId"] = "missing"));
        }

        [Test]
        public void RandomSequenceHasStableGoldenValues()
        {
            var rng = new Rng(0);
            Assert.That(rng.NextUInt64(), Is.EqualTo(0xE220A8397B1DCDAFUL));
            Assert.That(rng.NextUInt64(), Is.EqualTo(0x6E789E6AA1B965F4UL));
            Assert.That(rng.NextUInt64(), Is.EqualTo(0x06C45D188009454FUL));
        }

        [Test]
        public void RandomSamplingHandlesNegativeSeedsAndBigIntegerBounds()
        {
            var first = new Rng(-42);
            var second = new Rng(-42);
            BigInteger bound = BigInteger.Pow(10, 60) + 37;
            for (int index = 0; index < 100; index++)
            {
                BigInteger value = first.NextBelow(bound);
                Assert.That(value, Is.EqualTo(second.NextBelow(bound)));
                Assert.That(value >= 0 && value < bound, Is.True);
            }
            Assert.That(first.NextBelow(1), Is.EqualTo(BigInteger.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => first.NextBelow(0));
        }

        [TestCase(1, "rare")]
        [TestCase(2, "legend")]
        [TestCase(3, "myth")]
        [TestCase(4, "transcend")]
        [TestCase(5, "ultimate")]
        public void MaxLuckAlwaysChoosesHighestUnlockedQuality(int round, string id)
        {
            var roller = new QualityRoller(Config());
            var rng = new Rng(1234);
            for (int index = 0; index < 100; index++)
                Assert.That(roller.Roll(rng, round, int.MaxValue).Id, Is.EqualTo(id));
        }

        [Test]
        public void SameSeedAndInputsRepeatTheWholeQualitySequence()
        {
            var roller = new QualityRoller(Config());
            var first = new Rng(12);
            var second = new Rng(12);
            for (int index = 0; index < 500; index++)
            {
                int round = index % 5 + 1;
                int luck = index % 12;
                QualityDefinition quality = roller.Roll(first, round, luck);
                Assert.That(quality.Id, Is.EqualTo(roller.Roll(second, round, luck).Id));
                Assert.That(quality.UnlockRound, Is.LessThanOrEqualTo(round));
            }
        }

        [Test]
        public void QualityProbabilityUsesConfigAndLowerWeights()
        {
            var config = Config(json =>
            {
                json["qualityRoll"]["topBaseChance"] = 0;
                json["qualities"][0]["weight"] = 1;
                json["qualities"][1]["weight"] = 3;
            });
            var roller = new QualityRoller(config);
            var rng = new Rng(91);
            int good = 0;
            for (int index = 0; index < 10000; index++)
            {
                string quality = roller.Roll(rng, 1, 0).Id;
                Assert.That(quality, Is.Not.EqualTo("rare"));
                if (quality == "good") good++;
            }
            Assert.That(good, Is.InRange(7300, 7700));
            var changed = new QualityRoller(Config(json =>
            {
                json["qualityRoll"]["topBaseChance"] = 0.2m;
                json["upgrades"]["items"][3]["effectPerLevel"] = 0.25m;
            }));
            Assert.That(changed.TopChanceRaw(4), Is.EqualTo(config.FixedScale * 2 / 5));
        }

        [TestCase(0, "1000000")]
        [TestCase(1, "1071773")]
        [TestCase(10, "2000000")]
        [TestCase(20, "4000000")]
        public void StreakUsesConfiguredFractionalExponent(int streak, string expectedRaw)
        {
            Assert.That(new StreakCalc(Config()).MultiplierRaw(streak), Is.EqualTo(BigInteger.Parse(expectedRaw)));
        }

        [Test]
        public void LargeStreakAndYieldNeverPassThroughFloatingPoint()
        {
            GameConfig config = Config();
            var calc = new StreakCalc(config);
            BigInteger power = BigInteger.Pow(2, 100);
            Assert.That(calc.MultiplierRaw(1000), Is.EqualTo(power * config.FixedScale));
            Assert.That(calc.GatherQuantity(BigInteger.Pow(10, 50), 2, 1000),
                Is.EqualTo(BigInteger.Pow(10, 50) * 2 * power));
        }

        [Test]
        public void FractionalStreakIsFloorOfTheExactIntegerRoot()
        {
            GameConfig config = Config(json => { json["streak"]["base"] = 3; json["streak"]["divisor"] = 7; });
            var calc = new StreakCalc(config);
            BigInteger raw = calc.MultiplierRaw(123);
            BigInteger target = BigInteger.Pow(3, 123) * BigInteger.Pow(config.FixedScale, 7);
            Assert.That(BigInteger.Pow(raw, 7) <= target, Is.True);
            Assert.That(BigInteger.Pow(raw + 1, 7) > target, Is.True);
        }

        [Test]
        public void GatherQuantityFloorsOnlyAfterBothMultipliers()
        {
            var config = Config(json => json["upgrades"]["items"][2]["effectPerLevel"] = 0.75m);
            Assert.That(new StreakCalc(config).GatherQuantity(3, 1, 1), Is.EqualTo(new BigInteger(5)));
        }

        [TestCase(0, "150")]
        [TestCase(1, "225")]
        [TestCase(2, "338")]
        [TestCase(3, "506")]
        public void UpgradePriceMatchesMockHalfUpRounding(int level, string expected)
        {
            Assert.That(new UpgradeRules(Config()).Price("move", level), Is.EqualTo(BigInteger.Parse(expected)));
        }

        [Test]
        public void UpgradePriceAndEffectsFollowChangedConfigAtHugeLevels()
        {
            var config = Config(json =>
            {
                json["upgrades"]["priceGrowth"] = 2;
                json["upgrades"]["items"][0]["basePrice"] = 17;
                json["upgrades"]["items"][0]["effectPerLevel"] = 0.5m;
                json["gathering"]["baseMoveSpeedPxPerSec"] = 80;
                json["gathering"]["baseCollectSec"] = 3;
            });
            var upgrades = new UpgradeRules(config);
            Assert.That(upgrades.Price("move", 256), Is.EqualTo(17 * BigInteger.Pow(2, 256)));
            Assert.That(upgrades.MoveSpeedRaw(2), Is.EqualTo(160 * config.FixedScale));
            Assert.That(upgrades.CollectDurationRaw(4), Is.EqualTo(3 * config.FixedScale / 2));
        }

        [Test]
        public void NegativeLevelsAndStreaksAreRejected()
        {
            var config = Config();
            Assert.Throws<ArgumentOutOfRangeException>(() => new QualityRoller(config).TopChanceRaw(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreakCalc(config).MultiplierRaw(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UpgradeRules(config).Price("move", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new UpgradeRules(config).MultiplierRaw("amount", -1));
        }

        [Test]
        public void UnlimitedSpeedRetainsAPositiveRepresentableDuration()
        {
            var upgrades = new UpgradeRules(Config());
            Assert.That(upgrades.CollectDurationRaw(int.MaxValue), Is.EqualTo(BigInteger.One));
        }

        [TestCase("0", "0")]
        [TestCase("9999", "9,999")]
        [TestCase("10000", "1万")]
        [TestCase("123456", "12.34万")]
        [TestCase("100000000", "1億")]
        [TestCase("1000000000000", "1兆")]
        [TestCase("-1234567890123", "-1.23兆")]
        public void BigNumberFormattingUsesJapaneseUnits(string amount, string expected)
        {
            Assert.That(Format.Big(BigInteger.Parse(amount, CultureInfo.InvariantCulture), Config()), Is.EqualTo(expected));
        }

        [Test]
        public void FormatterUsesConfigAndKeepsVeryLargeIntegers()
        {
            GameConfig config = Config(json =>
            {
                json["format"]["unitBase"] = 1000;
                json["format"]["decimalPlaces"] = 1;
                json["format"]["units"] = new JArray("", "k", "M");
            });
            Assert.That(Format.Big(1234567, config), Is.EqualTo("1.2M"));
            Assert.That(Format.Big(BigInteger.Pow(10, 100), config), Is.EqualTo("1" + new string('0', 94) + "M"));
        }
    }
}
