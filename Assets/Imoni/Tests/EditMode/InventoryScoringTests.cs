using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Imoni.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Tests
{
    public sealed class InventoryScoringTests
    {
        private static JObject Json()
        {
#if UNITY_EDITOR
            string path = Path.Combine(UnityEngine.Application.dataPath, "Imoni", "Config", "GameConfig.json");
#else
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            return JObject.Parse(File.ReadAllText(path));
        }

        private static GameConfig Config() => GameConfig.FromJson(Json().ToString());
        private static RoundObjectives Goals(BigInteger required, BigInteger bonus) =>
            new RoundObjectives(new[] { new Objective("wood", required) }, new Objective("mushroom", bonus));

        [Test]
        public void SaleBelowProtectionIsRejectedWithoutChangingAnyLot()
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 4, config.FixedScale);
            inventory.Add("wood", "normal", 6, config.FixedScale * 2);
            inventory.SetProtectedQuantity("wood", "normal", 7);
            IReadOnlyList<MaterialLot> before = inventory.Lots;

            Assert.Throws<InvalidOperationException>(() => inventory.Sell("wood", "normal", 4));
            Assert.That(inventory.Lots.Select(lot => (lot.Sequence, lot.Quantity, lot.PointMultiplierRaw)),
                Is.EqualTo(before.Select(lot => (lot.Sequence, lot.Quantity, lot.PointMultiplierRaw))));
            Assert.That(inventory.ProtectedQuantity("wood", "normal"), Is.EqualTo(new BigInteger(7)));
            Assert.That(inventory.SellableQuantity("wood", "normal"), Is.EqualTo(new BigInteger(3)));
        }

        [Test]
        public void SaleConsumesOldestMatchingLotsAndKeepsHistoricalPointMultipliers()
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 4, config.FixedScale);
            inventory.Add("taro", "normal", 2, config.FixedScale);
            MaterialLot second = inventory.Add("wood", "normal", 6, config.FixedScale * 3);
            inventory.SetProtectedQuantity("wood", "normal", 3);

            BigInteger proceeds = inventory.Sell("wood", "normal", 5);

            Assert.That(proceeds, Is.EqualTo(5 * config.Material("wood").BasePrice * config.Quality("normal").Multiplier));
            MaterialLot remaining = inventory.Lots.Single(lot => lot.MaterialId == "wood");
            Assert.That(remaining.Sequence, Is.EqualTo(second.Sequence));
            Assert.That(remaining.Quantity, Is.EqualTo(new BigInteger(5)));
            Assert.That(remaining.PointMultiplierRaw, Is.EqualTo(config.FixedScale * 3));
            Assert.That(inventory.Quantity("taro", "normal"), Is.EqualTo(new BigInteger(2)));
            Assert.That(inventory.ProtectedQuantity("wood", "normal"), Is.EqualTo(new BigInteger(3)));
        }

        [Test]
        public void SellingDifferentQualityDoesNotConsumeProtectedOrOtherQualityLots()
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            inventory.Add("wood", "rare", 2, config.FixedScale);
            inventory.Add("wood", "normal", 3, config.FixedScale);
            inventory.SetProtectedQuantity("wood", "rare", 2);
            inventory.Sell("wood", "normal", 3);
            Assert.That(inventory.Lots.Count, Is.EqualTo(1));
            Assert.That(inventory.Lots[0].QualityId, Is.EqualTo("rare"));
            Assert.That(inventory.ProtectedQuantity("wood", "rare"), Is.EqualTo(new BigInteger(2)));
        }

        [Test]
        public void InvalidProtectionLeavesPreviousProtectionUnchanged()
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 3, config.FixedScale);
            inventory.SetProtectedQuantity("wood", "normal", 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.SetProtectedQuantity("wood", "normal", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.SetProtectedQuantity("wood", "normal", 4));
            Assert.That(inventory.ProtectedQuantity("wood", "normal"), Is.EqualTo(new BigInteger(2)));
            inventory.SetProtectedQuantity("wood", "normal", 0);
            Assert.That(inventory.SellableQuantity("wood", "normal"), Is.EqualTo(new BigInteger(3)));
        }

        [Test]
        public void InvalidAcquisitionOrSaleDoesNotMutateInventory()
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Add("wood", "normal", 0, config.FixedScale));
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Add("wood", "normal", -1, config.FixedScale));
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Add("wood", "normal", 1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Add("wood", "normal", 1, -1));
            Assert.Catch<ArgumentException>(() => inventory.Add("missing", "normal", 1, config.FixedScale));
            Assert.Catch<ArgumentException>(() => inventory.Add("wood", "missing", 1, config.FixedScale));
            Assert.That(inventory.Lots, Is.Empty);
            inventory.Add("wood", "normal", 1, config.FixedScale);
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Sell("wood", "normal", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Sell("wood", "normal", -1));
            Assert.Throws<InvalidOperationException>(() => inventory.Sell("wood", "normal", 2));
            Assert.That(inventory.Quantity("wood", "normal"), Is.EqualTo(BigInteger.One));
        }

        [Test]
        public void InventorySnapshotsCannotBeChangedAndDoNotTrackLaterSales()
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 5, config.FixedScale);
            IReadOnlyList<MaterialLot> snapshot = inventory.Lots;
            Assert.Throws<NotSupportedException>(() => ((IList<MaterialLot>)snapshot).Clear());
            inventory.Sell("wood", "normal", 2);
            Assert.That(snapshot[0].Quantity, Is.EqualTo(new BigInteger(5)));
            Assert.That(inventory.Lots[0].Quantity, Is.EqualTo(new BigInteger(3)));
        }

        [Test]
        public void HugeQuantitiesAndProceedsKeepExactIntegerPrecision()
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            BigInteger huge = BigInteger.Pow(10, 100) + 7;
            inventory.Add("wood", "rare", huge, config.FixedScale * huge);
            inventory.SetProtectedQuantity("wood", "rare", huge - 1);
            Assert.That(inventory.Sell("wood", "rare", 1),
                Is.EqualTo(config.Material("wood").BasePrice * config.Quality("rare").Multiplier));
            Assert.That(inventory.Quantity("wood", "rare"), Is.EqualTo(huge - 1));
            Assert.That(inventory.PointsRaw("wood"),
                Is.EqualTo((huge - 1) * config.Material("wood").BasePoint * config.Quality("rare").Multiplier * config.FixedScale * huge));
        }

        [Test]
        public void QualityEquivalentIgnoresPointMultiplierAndIncludesAllQualities()
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 3, config.FixedScale * 10);
            inventory.Add("wood", "good", 2, config.FixedScale);
            inventory.Add("taro", "rare", 9, config.FixedScale);
            Assert.That(inventory.QualityEquivalent("wood"),
                Is.EqualTo(3 * config.Quality("normal").Multiplier + 2 * config.Quality("good").Multiplier));
        }

        [Test]
        public void SoldMaterialsAreExcludedFromRequiredAndBonusScore()
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 12, config.FixedScale * 2);
            inventory.Add("mushroom", "normal", 8, config.FixedScale);
            RoundEvaluation before = Scoring.Evaluate(config, inventory, Goals(1, 1));
            inventory.Sell("wood", "normal", 12);
            inventory.Sell("mushroom", "normal", 8);
            RoundEvaluation after = Scoring.Evaluate(config, inventory, Goals(1, 1));
            Assert.That(before.Score, Is.GreaterThan(BigInteger.Zero));
            Assert.That(after.RequiredPoints, Is.EqualTo(BigInteger.Zero));
            Assert.That(after.BonusPoints, Is.EqualTo(BigInteger.Zero));
            Assert.That(after.Score, Is.EqualTo(BigInteger.Zero));
            Assert.That(after.Success, Is.False);
        }

        [Test]
        public void ProtectedMaterialsStillMeetObjectivesAndScoreAndAreClearedForThePot()
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            inventory.Add("wood", "good", 2, config.FixedScale);
            inventory.Add("mushroom", "normal", 3, config.FixedScale);
            RoundEvaluation beforeLock = Scoring.Evaluate(config, inventory, Goals(4, 3));
            inventory.SetProtectedQuantity("wood", "good", 2);
            inventory.SetProtectedQuantity("mushroom", "normal", 3);
            RoundEvaluation afterLock = Scoring.Evaluate(config, inventory, Goals(4, 3));
            Assert.That(afterLock.Success, Is.True);
            Assert.That(afterLock.Score, Is.EqualTo(beforeLock.Score));
            inventory.Clear();
            Assert.That(inventory.Lots, Is.Empty);
            Assert.That(inventory.ProtectedQuantity("wood", "good"), Is.EqualTo(BigInteger.Zero));
            Assert.That(afterLock.RequiredGoals[0].Current, Is.EqualTo(2 * config.Quality("good").Multiplier));
        }

        [TestCase(0, 0)]
        [TestCase(99, 0)]
        [TestCase(100, 1)]
        [TestCase(149, 1)]
        [TestCase(150, 2)]
        [TestCase(199, 2)]
        [TestCase(200, 4)]
        [TestCase(299, 4)]
        [TestCase(300, 8)]
        [TestCase(1000, 8)]
        public void BonusTiersUseExactInclusiveThresholds(int quantity, int expectedMultiplier)
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 1, config.FixedScale);
            if (quantity > 0) inventory.Add("mushroom", "normal", quantity, config.FixedScale);
            Assert.That(Scoring.Evaluate(config, inventory, Goals(1, 100)).BonusMultiplier,
                Is.EqualTo(new BigInteger(expectedMultiplier)));
        }

        [Test]
        public void BonusThresholdDoesNotRoundUpLargeFractionalPercentages()
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            BigInteger target = BigInteger.Pow(10, 80);
            inventory.Add("wood", "normal", 1, config.FixedScale);
            inventory.Add("mushroom", "normal", target * 3 / 2 - 1, config.FixedScale);
            RoundEvaluation result = Scoring.Evaluate(config, inventory, Goals(1, target));
            Assert.That(result.BonusMultiplier, Is.EqualTo(BigInteger.One));
            inventory.Add("mushroom", "normal", 1, config.FixedScale);
            Assert.That(Scoring.Evaluate(config, inventory, Goals(1, target)).BonusMultiplier, Is.EqualTo(new BigInteger(2)));
        }

        [Test]
        public void FractionalLotPointsAreCombinedBeforeScoreIsRounded()
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            for (int i = 0; i < 3; i++) inventory.Add("wood", "normal", 1, config.FixedScale * 19 / 10);
            RoundEvaluation result = Scoring.Evaluate(config, inventory, Goals(1, 1));
            // Three lots of 1.9 points give floor(5.7 * 0.5) = 2, not floor(3 * 0.5) = 1.
            Assert.That(result.RequiredPoints, Is.EqualTo(new BigInteger(5)));
            Assert.That(result.Score, Is.EqualTo(new BigInteger(2)));
        }

        [Test]
        public void BothScoreTermsAreAddedBeforeRounding()
        {
            JObject json = Json();
            json["materials"].Single(material => (string)material["id"] == "mushroom")["basePoint"] = 1;
            GameConfig config = GameConfig.FromJson(json.ToString());
            var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 1, config.FixedScale);
            inventory.Add("mushroom", "normal", 1, config.FixedScale);
            Assert.That(Scoring.Evaluate(config, inventory, Goals(1, 1)).Score, Is.EqualTo(BigInteger.One));
        }

        [Test]
        public void ScoringAndSalePricesFollowChangedConfiguration()
        {
            JObject json = Json();
            JToken wood = json["materials"].Single(material => (string)material["id"] == "wood");
            wood["basePoint"] = 7;
            wood["basePrice"] = 11;
            json["materials"].Single(material => (string)material["id"] == "mushroom")["basePoint"] = 13;
            json["score"]["requiredWeight"] = 0.25m;
            json["score"]["bonusWeight"] = 0.75m;
            json["economy"]["bonusFundsPerPoint"] = 0.25m;
            GameConfig config = GameConfig.FromJson(json.ToString());
            var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 2, config.FixedScale * 3 / 2);
            inventory.Add("mushroom", "normal", 4, config.FixedScale * 2);
            RoundEvaluation result = Scoring.Evaluate(config, inventory, Goals(2, 4));
            Assert.That(result.RequiredPoints, Is.EqualTo(new BigInteger(21)));
            Assert.That(result.BonusPoints, Is.EqualTo(new BigInteger(104)));
            Assert.That(result.Score, Is.EqualTo(new BigInteger(83)));
            Assert.That(result.FundsReward, Is.EqualTo(new BigInteger(26)));
            Assert.That(inventory.Sell("wood", "normal", 2), Is.EqualTo(new BigInteger(22)));
        }

        [Test]
        public void DecimalScoreWeightsKeepFullPrecisionWithHugeQuantitiesAndCoarseLotScale()
        {
            JObject json = Json();
            json["numeric"]["fixedScale"] = 10;
            json["score"]["requiredWeight"] = 0.123456789m;
            json["score"]["bonusWeight"] = 0m;
            GameConfig config = GameConfig.FromJson(json.ToString());
            BigInteger quantity = BigInteger.Pow(10, 60) + 17;
            var inventory = new Inventory(config);
            inventory.Add("wood", "normal", quantity, config.FixedScale);
            Assert.That(Scoring.Evaluate(config, inventory, Goals(1, 1)).Score,
                Is.EqualTo(quantity * 123456789 / 1000000000));
        }

        [Test]
        public void BonusTiersFollowChangedConfiguration()
        {
            JObject json = Json();
            json["bonusTiers"] = new JArray(new JObject { ["minPercent"] = 125, ["multiplier"] = 3 });
            GameConfig config = GameConfig.FromJson(json.ToString());
            var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 1, config.FixedScale);
            inventory.Add("mushroom", "normal", 124, config.FixedScale);
            Assert.That(Scoring.Evaluate(config, inventory, Goals(1, 100)).BonusMultiplier, Is.EqualTo(BigInteger.Zero));
            inventory.Add("mushroom", "normal", 1, config.FixedScale);
            Assert.That(Scoring.Evaluate(config, inventory, Goals(1, 100)).BonusMultiplier, Is.EqualTo(new BigInteger(3)));
        }

        [Test]
        public void QualityConversionFollowsChangedConfiguration()
        {
            JObject json = Json();
            json["qualities"].Single(quality => (string)quality["id"] == "good")["multiplier"] = 3;
            GameConfig config = GameConfig.FromJson(json.ToString());
            var inventory = new Inventory(config);
            inventory.Add("wood", "good", 2, config.FixedScale);
            Assert.That(inventory.QualityEquivalent("wood"), Is.EqualTo(new BigInteger(6)));
            Assert.That(Scoring.Evaluate(config, inventory, Goals(6, 1)).Success, Is.True);
        }

        [Test]
        public void FailureReportsAllMissingMaterialsAndPaysNoReward()
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            inventory.Add("mushroom", "normal", 300, config.FixedScale);
            var goals = new RoundObjectives(new[] { new Objective("wood", 1), new Objective("taro", 1) },
                new Objective("mushroom", 100));
            RoundEvaluation result = Scoring.Evaluate(config, inventory, goals);
            Assert.That(result.Success, Is.False);
            Assert.That(result.MissingMaterialIds, Is.EqualTo(new[] { "wood", "taro" }));
            Assert.That(result.FundsReward, Is.EqualTo(BigInteger.Zero));
            Assert.That(result.BonusMultiplier, Is.EqualTo(new BigInteger(8)));
        }

        [Test]
        public void IrrelevantMaterialsDoNotContributeToScore()
        {
            GameConfig config = Config();
            var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 1, config.FixedScale);
            RoundEvaluation before = Scoring.Evaluate(config, inventory, Goals(1, 1));
            inventory.Add("fish", "ultimate", BigInteger.Pow(10, 80), config.FixedScale * 100);
            RoundEvaluation after = Scoring.Evaluate(config, inventory, Goals(1, 1));
            Assert.That(after.Score, Is.EqualTo(before.Score));
            Assert.That(after.RequiredPoints, Is.EqualTo(before.RequiredPoints));
            Assert.That(after.BonusPoints, Is.EqualTo(before.BonusPoints));
        }

        [Test]
        public void ObjectivesRejectInvalidQuantitiesDuplicatesAndOverlap()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Objective("wood", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Objective("wood", -1));
            Assert.Throws<ArgumentException>(() => new Objective("", 1));
            Assert.Throws<ArgumentException>(() => new RoundObjectives(Array.Empty<Objective>(), new Objective("mushroom", 1)));
            Assert.Throws<ArgumentException>(() => new RoundObjectives(new[] { new Objective("wood", 1), new Objective("wood", 2) }, new Objective("mushroom", 1)));
            Assert.Throws<ArgumentException>(() => new RoundObjectives(new[] { new Objective("wood", 1) }, new Objective("wood", 1)));
            Assert.Catch<ArgumentException>(() => new RoundObjectives(new[] { new Objective("missing", 1) }, new Objective("mushroom", 1)).Validate(Config()));
        }
    }
}
