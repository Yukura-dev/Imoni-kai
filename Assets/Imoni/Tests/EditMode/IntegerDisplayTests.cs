using System;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Imoni.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Tests
{
    public sealed class IntegerDisplayTests
    {
        private static GameConfig Config(Action<JObject> edit = null)
        {
#if UNITY_EDITOR
            string path = Path.Combine(UnityEngine.Application.dataPath, "Imoni/Config/GameConfig.json");
#else
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            JObject data = JObject.Parse(File.ReadAllText(path)); edit?.Invoke(data);
            return GameConfig.FromJson(data.ToString());
        }

        [TestCase("1234", "1,234")]
        [TestCase("88700000", "8,870万")]
        [TestCase("12345", "1万")]
        [TestCase("88701234", "8,870万")]
        [TestCase("1234560000", "12億3,456万")]
        [TestCase("1234567890", "12億3,456万")]
        [TestCase("3200000000000", "3兆2,000億")]
        [TestCase("3200012345678", "3兆2,000億")]
        [TestCase("100000001", "1億")]
        [TestCase("99999999", "9,999万")]
        [TestCase("-3200000000000", "-3兆2,000億")]
        [TestCase("1234569999", "12億3,456万")]
        public void UsesIntegerGroupsWithoutDecimals(string number, string expected)
        { Assert.That(Format.Big(BigInteger.Parse(number), Config()), Is.EqualTo(expected)); }

        [Test]
        public void DisplayGroupCountIsConfiguredAndNeverChangesTheIntegerValue()
        {
            BigInteger value = 123456789;
            Assert.That(Format.Big(value, Config(data => data["format"]["maxDisplayGroups"] = 1)), Is.EqualTo("1億"));
            Assert.That(Format.Big(BigInteger.Parse("1234567890123"), Config(data => data["format"]["maxDisplayGroups"] = 3)),
                Is.EqualTo("1兆2,345億6,789万"));
            Assert.That(value, Is.EqualTo(new BigInteger(123456789)));
        }

        [Test]
        public void EndlessRemainderUsesTheNextLowerUnitRatherThanRepeatingTheHighest()
        {
            GameConfig config = Config();
            BigInteger value = 7804 * BigInteger.Pow(config.FormatUnitBase, 7)
                + 8528 * BigInteger.Pow(config.FormatUnitBase, 6);
            Assert.That(Format.Big(value, config), Is.EqualTo("7,804穣8,528秭"));
            Assert.That(Format.Big(-value, config), Is.EqualTo("-7,804穣8,528秭"));
            Assert.That(Format.Compact(value, config), Is.EqualTo("7,804穣"));
        }

        [Test]
        public void EveryAdjacentNamedUnitUsesItsOwnMagnitudeIncludingTheFinalConfiguredUnit()
        {
            GameConfig config = Config();
            for (int unit = 2; unit < config.FormatUnits.Count; unit++)
            {
                BigInteger power = BigInteger.Pow(config.FormatUnitBase, unit);
                BigInteger value = 7804 * power + 8528 * (power / config.FormatUnitBase);
                Assert.That(Format.Big(value, config), Is.EqualTo("7,804" + config.FormatUnits[unit]
                    + "8,528" + config.FormatUnits[unit - 1]), "unit " + unit);
            }
        }

        [Test]
        public void ExtremeNumbersKeepTheHighestUnitAndItsDistinctLowerRemainderWithoutOverflow()
        {
            GameConfig config = Config();
            int lastUnit = config.FormatUnits.Count - 1;
            BigInteger power = BigInteger.Pow(config.FormatUnitBase, lastUnit);
            BigInteger coefficient = BigInteger.Pow(10, 1000);
            BigInteger value = coefficient * power + 8528 * (power / config.FormatUnitBase);
            string upper = coefficient.ToString("N0", CultureInfo.InvariantCulture) + config.FormatUnits[lastUnit];
            Assert.That(Format.Big(value, config), Is.EqualTo(upper + "8,528" + config.FormatUnits[lastUnit - 1]));
            Assert.That(Format.Compact(value, config), Is.EqualTo(upper));
            Assert.That(Format.Exact(value), Is.EqualTo(value.ToString("N0", CultureInfo.InvariantCulture)));
        }

        [TestCase("0", "0")]
        [TestCase("9999", "9,999")]
        [TestCase("10000", "1万")]
        [TestCase("99999999", "9,999万")]
        [TestCase("100000000", "1億")]
        [TestCase("28199999999", "281億")]
        [TestCase("-28199999999", "-281億")]
        [TestCase("3200012345678", "3兆")]
        public void CompactUsesOnlyTheHighestIntegerGroupWithoutRoundingUp(string value, string expected)
        { Assert.That(Format.Compact(BigInteger.Parse(value), Config()), Is.EqualTo(expected)); }

        [Test]
        public void CompactUsesConfiguredUnitsAndBaseRegardlessOfDetailedGroupCount()
        {
            GameConfig config = Config(data =>
            {
                data["format"]["unitBase"] = 1000;
                data["format"]["units"] = new JArray("", "k", "M");
                data["format"]["maxDisplayGroups"] = 3;
            });
            Assert.That(Format.Compact(281999999, config), Is.EqualTo("281M"));
            Assert.That(Format.Big(281999999, config), Is.EqualTo("281M999k"));
        }

        [TestCase("")]
        [TestCase(" ")]
        [TestCase("万")]
        public void ConfigRejectsEmptyOrRepeatedNamedUnits(string unit)
        {
            Assert.Throws<ArgumentException>(() => Config(data => data["format"]["units"] = new JArray("", "万", unit)));
        }

        [Test]
        public void GoalProvidesCompactAndDetailedCountsButKeepsExactAchievementAndTooltip()
        {
            GameConfig config = Config();
            BigInteger current = BigInteger.Parse("28199999998"), required = current + 1;
            MaterialGoalView view = DisplayProjection.Goal(config, "wood", current, required);
            Assert.That(view.CurrentCompactText, Is.EqualTo("281億"));
            Assert.That(view.RequiredCompactText, Is.EqualTo("281億"));
            Assert.That(view.CurrentText, Is.EqualTo("281億9,999万"));
            Assert.That(view.ExactCountsText, Is.EqualTo("28,199,999,998/28,199,999,999"));
            Assert.That(view.Achieved, Is.False, "Matching abbreviated text must not satisfy an objective.");
            Assert.That(DisplayProjection.Goal(config, "wood", required, required).Achieved, Is.True);
        }

        [TestCase(999, 100, "999%")]
        [TestCase(1000, 100, "目標の10倍")]
        [TestCase(1099, 100, "目標の10倍")]
        [TestCase(1199, 100, "目標の11倍")]
        [TestCase(9999, 100, "目標の99倍")]
        public void AchievementSwitchesAtOneThousandPercentAndTruncatesFractions(int current, int required, string expected)
        { Assert.That(DisplayProjection.Achievement(Config(), current, required), Is.EqualTo(expected)); }

        [Test]
        public void AchievementUsesConfiguredBoundaryAndHandlesHugeIntegers()
        {
            GameConfig config = Config(data => data["format"]["achievementMultipleThresholdPercent"] = 2000);
            Assert.That(DisplayProjection.Achievement(config, 19999, 1000), Is.EqualTo("1,999%"));
            Assert.That(DisplayProjection.Achievement(config, 20000, 1000), Is.EqualTo("目標の20倍"));
            BigInteger huge = BigInteger.Pow(10, 1000);
            Assert.That(DisplayProjection.Achievement(config, huge * 30, huge), Is.EqualTo("目標の30倍"));
        }

        [Test]
        public void ResultClearGoalsAndBonusShareTheIntegerFormatter()
        {
            GameConfig config = Config(); var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 88700000, config.FixedScale);
            inventory.Add("mushroom", "normal", 1234560000, config.FixedScale);
            var goals = new RoundObjectives(new[] { new Objective("wood", 10000) }, new Objective("mushroom", 10000));
            RoundEvaluation evaluation = Scoring.Evaluate(config, inventory, goals);
            var result = new RoundResult(config.RoundsToClear, 7, config.DurationSeconds, evaluation,
                BigInteger.Parse("3200000000000"), Array.Empty<OperationRecord>());
            RoundResultView view = RoundResultProjection.Present(config, result);
            Assert.That(view.IsClear, Is.True);
            Assert.That(view.CumulativeScoreText, Is.EqualTo("3兆2,000億"));
            Assert.That(view.Required.Single().CurrentText, Is.EqualTo("8,870万"));
            Assert.That(view.Required.Single().RequiredText, Is.EqualTo("1万"));
            Assert.That(view.BonusText, Is.EqualTo("キノコ 12億3,456万/1万"));
            Assert.That(view.BonusStatusText, Is.EqualTo("目標の12万倍 報酬×8"));
            foreach (string text in new[] { view.RequiredPointsText, view.RequiredWeightedText,
                view.BonusPointsText, view.BonusWeightedText, view.ScoreText, view.FundsRewardText })
                Assert.That(text, Does.Not.Contain("."));
            Assert.That(view.ScoreText, Is.EqualTo(Format.Big(evaluation.Score, config)));
        }
    }
}
