using System;
using System.IO;
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
