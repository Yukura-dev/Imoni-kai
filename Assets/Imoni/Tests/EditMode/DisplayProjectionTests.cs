using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Imoni.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class DisplayProjectionTests
    {
        private static GameConfig Config(Action<JObject> edit = null)
        {
#if UNITY_EDITOR
            string path = Path.Combine(UnityEngine.Application.dataPath, "Imoni", "Config", "GameConfig.json");
#else
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            var json = JObject.Parse(File.ReadAllText(path));
            edit?.Invoke(json);
            return GameConfig.FromJson(json.ToString());
        }

        [Test]
        public void GoalHandlesHugeQuantitiesAndUsesConfiguredPrecisionAndFormatting()
        {
            GameConfig config = Config(json => json["numeric"]["fixedScale"] = 1000);
            BigInteger huge = BigInteger.Pow(10, 1000);
            MaterialGoalView goal = DisplayProjection.Goal(config, "wood", huge, huge * 3);
            Assert.That(goal.Progress, Is.EqualTo(.333m));
            Assert.That(goal.Achieved, Is.False);
            Assert.That(goal.Name, Is.EqualTo(config.Material("wood").Name));
            Assert.That(goal.CurrentText, Is.EqualTo(Format.Big(huge, config)));
        }

        [TestCase(0, 10, 0, false)]
        [TestCase(5, 10, .5, false)]
        [TestCase(10, 10, 1, true)]
        [TestCase(15, 10, 1, true)]
        public void GoalClampsProgressButRetainsAchievement(int current, int required, double progress, bool achieved)
        {
            MaterialGoalView goal = DisplayProjection.Goal(Config(), "wood", current, required);
            Assert.That(goal.Progress, Is.EqualTo((decimal)progress));
            Assert.That(goal.Achieved, Is.EqualTo(achieved));
        }

        [Test]
        public void BonusTiersUseExactBoundaryAndConfiguredThresholdAndMultiplier()
        {
            GameConfig config = Config(json => { json["bonusTiers"][1]["minPercent"] = 175; json["bonusTiers"][1]["multiplier"] = 3; });
            BigInteger required = BigInteger.Pow(10, 1000);
            var before = DisplayProjection.BonusTiers(config, required * 175 / 100 - 1, required);
            var exact = DisplayProjection.BonusTiers(config, required * 175 / 100, required);
            Assert.That(before[1].Reached, Is.False);
            Assert.That(before[0].Current, Is.True);
            Assert.That(exact[0].Reached, Is.True);
            Assert.That(exact[0].Current, Is.False);
            Assert.That(exact[1].Current, Is.True);
            Assert.That(exact[1].ThresholdText, Is.EqualTo("175%"));
            Assert.That(exact[1].MultiplierText, Is.EqualTo("×3"));
            Assert.That(exact.Count(tier => tier.Current), Is.EqualTo(1));
            Assert.That(DisplayProjection.BonusTiers(config, 0, required).Any(tier => tier.Reached || tier.Current), Is.False);
        }

        [Test]
        public void InterpolationRetainsHugeIntegerEndpointsAndSupportsDecreasingValues()
        {
            BigInteger huge = BigInteger.Pow(10, 1000);
            Assert.That(DisplayProjection.Interpolate(0, huge, .5m), Is.EqualTo(huge / 2));
            Assert.That(DisplayProjection.Interpolate(huge, 0, .5m), Is.EqualTo(huge / 2));
            Assert.That(DisplayProjection.Interpolate(huge, huge + 1, 0), Is.EqualTo(huge));
            Assert.That(DisplayProjection.Interpolate(huge, huge + 1, 1), Is.EqualTo(huge + 1));
        }

        [Test]
        public void InvalidGoalAmountsAndInterpolationFractionsAreRejected()
        {
            GameConfig config = Config();
            Assert.Throws<ArgumentOutOfRangeException>(() => DisplayProjection.Goal(config, "wood", -1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => DisplayProjection.Goal(config, "wood", 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => DisplayProjection.BonusTiers(config, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => DisplayProjection.Interpolate(0, 1, 1.1m));
        }
    }
}
