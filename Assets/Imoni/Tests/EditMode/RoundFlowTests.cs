using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class RoundFlowTests
    {
        private static JObject Data()
        {
#if UNITY_EDITOR
            return JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Imoni/Config/GameConfig.json")));
#else
            return JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json")));
#endif
        }
        private static GameConfig Config(Action<JObject> change = null)
        { JObject data = Data(); change?.Invoke(data); return GameConfig.FromJson(data.ToString()); }
        private static RoundObjectives SmallGoals() => new RoundObjectives(new[] { new Objective("wood", 1) }, new Objective("mushroom", 1));
        private static LocalGameServer Server(GameConfig config, int seed = 202647) => new LocalGameServer(config, seed, objectives: SmallGoals(), enableBots: false);
        private static RoundResult Complete(LocalGameServer server, GameConfig config)
        {
            server.Buy("player1", "wood", 1); server.Buy("player1", "mushroom", 1); server.Tick(config.DurationSeconds);
            return server.Snapshot().LastResult;
        }
        private static RoundResult Evaluate(GameConfig config, BigInteger bonusQuantity, BigInteger bonusRequired, int round = 1,
            IEnumerable<OperationRecord> history = null)
        {
            var inventory = new Inventory(config); inventory.Add("wood", "normal", 1, config.FixedScale);
            if (bonusQuantity > 0) inventory.Add("mushroom", "normal", bonusQuantity, config.FixedScale);
            var goals = new RoundObjectives(new[] { new Objective("wood", 1) }, new Objective("mushroom", bonusRequired));
            RoundEvaluation evaluation = Scoring.Evaluate(config, inventory, goals);
            return new RoundResult(round, 202647, config.DurationSeconds, evaluation, evaluation.Score, history ?? Array.Empty<OperationRecord>());
        }

        [Test]
        public void ReplayingOrReadingSettledResultNeverPaysScoreOrFundsAgain()
        {
            GameConfig config = Config(); var server = Server(config); RoundResult result = Complete(server, config);
            GameSnapshot settled = server.Snapshot();
            for (int index = 0; index < 4; index++)
            {
                RoundResultView view = RoundResultProjection.Present(config, result);
                Assert.That(view.ScoreText, Is.EqualTo(Format.Big(result.Evaluation.Score, config)));
                server.Tick(config.DurationSeconds);
            }
            GameSnapshot repeated = server.Snapshot();
            Assert.That(repeated.LastResult, Is.SameAs(result));
            Assert.That(repeated.Funds, Is.EqualTo(settled.Funds));
            Assert.That(result.FundsAfterSettlement, Is.EqualTo(settled.Funds));
            Assert.That(repeated.CumulativeScore, Is.EqualTo(settled.CumulativeScore));
            Assert.That(repeated.Events.Count(item => item.Kind == RoundEventKind.TimeUp), Is.EqualTo(1));
        }

        [Test]
        public void DeadlineSnapshotConsumesAllLotsAndExcludesSoldStockFromRAndB()
        {
            GameConfig config = Config(); var server = Server(config);
            server.Buy("player1", "wood", 12); server.Buy("player2", "mushroom", 2); server.Buy("player2", "negi", 3);
            server.SetProtection("player1", "wood", "normal", 5); server.Sell("player1", "wood", "normal", 7);
            server.Tick(config.DurationSeconds); RoundResult result = server.Snapshot().LastResult;
            Assert.That(result.Evaluation.RequiredGoals.Single().Current, Is.EqualTo(new BigInteger(5)));
            Assert.That(result.Evaluation.RequiredPoints, Is.EqualTo(5 * config.Material("wood").BasePoint));
            Assert.That(result.Evaluation.BonusPoints, Is.EqualTo(2 * config.Material("mushroom").BasePoint));
            Assert.That(server.Snapshot().Lots, Is.Empty); Assert.That(server.Snapshot().Protection, Is.Empty);
            Assert.That(RoundResultProjection.Present(config, result).Required.Single().CurrentText, Is.EqualTo("5"));
        }

        [Test]
        public void NextRoundPreservesWorldGrowthMoneyUpgradesAndExpandsAreaWhileClearingRunStock()
        {
            GameConfig config = Config(); var server = Server(config); WorldMap originalWorld = server.World;
            server.Upgrade("player1", "move"); server.Upgrade("player2", "luck");
            server.Buy("player1", "wood", 1); server.SetProtection("player1", "wood", "normal", 1);
            ResourceNode node = server.World.Resources.Where(resource => resource.MaterialId == "wood" && server.CanSelectResource("player1", resource.Id))
                .OrderBy(resource => Pathfinding.FindPathToResource(server.World, server.World.Start, resource, 1).Steps.Count).First();
            decimal distance = Pathfinding.FindPathToResource(server.World, server.World.Start, node, 1).DistanceTiles;
            decimal work = distance * GatheringTiming.MoveStepSeconds(config, 1) + config.ResourceStock * GatheringTiming.CollectSeconds(config, 0, node);
            server.Tick(config.DurationSeconds - work - 5); server.TrySelectResource("player1", node.Id); server.Tick(work + 5);
            GameSnapshot settled = server.Snapshot();
            decimal regrowth = node.RegrowthSeconds;
            Assert.That(node.RemainingStock, Is.Zero); Assert.That(regrowth, Is.GreaterThan(0));
            server.Tick(200); Assert.That(node.RegrowthSeconds, Is.EqualTo(regrowth));
            var newlyUnlocked = new GridPosition(checked(originalWorld.Start.X + (int)config.World.UnlockRadius(1) + 1), originalWorld.Start.Y);
            Assert.That(originalWorld.IsUnlocked(newlyUnlocked, 1), Is.False);
            server.StartNextRound(); GameSnapshot next = server.Snapshot();
            Assert.That(next.RoundNumber, Is.EqualTo(2)); Assert.That(next.ElapsedSeconds, Is.Zero);
            Assert.That(server.World, Is.SameAs(originalWorld)); Assert.That(server.World.IsUnlocked(newlyUnlocked, 2), Is.True);
            Assert.That(next.Funds, Is.EqualTo(settled.Funds)); Assert.That(next.Players[0].Upgrades["move"], Is.EqualTo(1));
            Assert.That(next.Players[1].Upgrades["luck"], Is.EqualTo(1));
            Assert.That(next.Lots, Is.Empty); Assert.That(next.Protection, Is.Empty); Assert.That(next.Players.All(player => player.Streak == 0), Is.True);
            Assert.That(node.RegrowthSeconds, Is.EqualTo(regrowth));
            server.Tick(config.ResourceRegrowSeconds - regrowth); Assert.That(node.IsReady, Is.True);
            Assert.Throws<InvalidOperationException>(() => server.StartNextRound());
        }

        [Test]
        public void FailureProjectionKeepsHistoricalRoundAndCumulativeEvenAfterActiveResetAndRetry()
        {
            GameConfig config = Config(); var server = Server(config); RoundResult first = Complete(server, config);
            server.StartNextRound(SmallGoals()); server.Upgrade("player1", "speed"); server.Buy("player1", "mushroom", 2);
            server.SetProtection("player1", "mushroom", "normal", 2); server.Tick(config.DurationSeconds);
            RoundResult failure = server.Snapshot().LastResult; RoundResultView view = RoundResultProjection.Present(config, failure);
            Assert.That(server.Snapshot().RoundNumber, Is.EqualTo(1)); Assert.That(server.Snapshot().CumulativeScore, Is.EqualTo(BigInteger.Zero));
            Assert.That(view.RoundNumber, Is.EqualTo(2)); Assert.That(view.RoundText, Is.EqualTo("ラウンド 2/5 結果"));
            Assert.That(view.CumulativeScoreText, Is.EqualTo(Format.Big(first.Evaluation.Score, config)));
            Assert.That(view.MissingMaterialText, Is.EqualTo("必須素材「薪」が足りなかった"));
            Assert.That(view.FundsRewardText, Is.EqualTo("+0G")); Assert.That(view.CanRetry, Is.True); Assert.That(view.CanNextRound, Is.False);
            Assert.That(view.NewlyEligibleTitles, Is.Empty); Assert.That(failure.FundsAfterSettlement, Is.EqualTo(config.StartFunds));
            WorldMap resetWorld = server.World; server.Retry(777);
            Assert.That(server.World, Is.Not.SameAs(resetWorld)); Assert.That(server.World.Seed, Is.EqualTo(777));
            Assert.That(server.Snapshot().Phase, Is.EqualTo(RoundPhase.Collecting)); Assert.That(server.Snapshot().LastResult, Is.Null);
            Assert.That(server.Snapshot().Funds, Is.EqualTo(config.StartFunds)); Assert.That(server.Snapshot().Protection, Is.Empty);
            Assert.That(server.Snapshot().Players.All(player => player.Streak == 0 && player.Upgrades.Values.All(level => level == 0)), Is.True);
            RoundResultView retained = RoundResultProjection.Present(config, failure);
            Assert.That(retained.MissingMaterialText, Is.EqualTo(view.MissingMaterialText)); Assert.That(retained.CumulativeScoreText, Is.EqualTo(view.CumulativeScoreText));
        }

        [Test]
        public void FiveSuccessesExposeClearWithoutR6AndOnlyCompletedRoundScoresAccumulate()
        {
            GameConfig config = Config(); var server = Server(config); BigInteger score = BigInteger.Zero;
            for (int round = 1; round <= config.RoundsToClear; round++)
            {
                RoundResult result = Complete(server, config); score += result.Evaluation.Score;
                var view = RoundResultProjection.Present(config, result);
                Assert.That(result.CumulativeScore, Is.EqualTo(score));
                Assert.That(view.CanNextRound, Is.EqualTo(round < config.RoundsToClear));
                if (round < config.RoundsToClear) server.StartNextRound(SmallGoals());
            }
            RoundResultView clear = RoundResultProjection.Present(config, server.Snapshot().LastResult);
            Assert.That(clear.IsClear, Is.True); Assert.That(clear.NextButtonText, Is.EqualTo("ホームへ戻る"));
            Assert.That(clear.NewlyEligibleTitles.Any(title => title.Value == "伝説の"), Is.True);
            Assert.That(server.Snapshot().Phase, Is.EqualTo(RoundPhase.Cleared));
            Assert.Throws<InvalidOperationException>(() => server.StartNextRound()); Assert.Throws<InvalidOperationException>(() => server.Retry(777));
        }

        [TestCase(99, "C")]
        [TestCase(100, "B")]
        [TestCase(149, "B")]
        [TestCase(150, "A")]
        [TestCase(299, "A")]
        [TestCase(300, "S")]
        public void ResultRankUsesConfiguredExactBonusThreshold(int bonusQuantity, string rank)
        {
            GameConfig config = Config(); RoundResult result = Evaluate(config, bonusQuantity, 100);
            Assert.That(RoundResultProjection.Present(config, result).Rank, Is.EqualTo(rank));
        }

        [Test]
        public void ResultRankDoesNotRoundUpBelowThresholdAndReadsChangedGradeConfig()
        {
            GameConfig config = Config(data => data["rankGrades"][1]["minBonusPercent"] = 160);
            Assert.That(RoundResultProjection.Present(config, Evaluate(config, 15999, 10000)).Rank, Is.EqualTo("B"));
            Assert.That(RoundResultProjection.Present(config, Evaluate(config, 16000, 10000)).Rank, Is.EqualTo("A"));
        }

        [Test]
        public void FractionalLotPointsRemainInternalWhileDisplayedBreakdownUsesIntegers()
        {
            GameConfig config = Config(); var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 1, config.FixedScale * 3 / 2);
            inventory.Add("mushroom", "normal", 1, config.FixedScale * 3 / 2);
            RoundEvaluation evaluation = Scoring.Evaluate(config, inventory, SmallGoals());
            var result = new RoundResult(1, 202647, config.DurationSeconds, evaluation, evaluation.Score, Array.Empty<OperationRecord>());
            RoundResultView view = RoundResultProjection.Present(config, result);
            Assert.That(view.RequiredPointsText, Is.EqualTo("1")); Assert.That(view.RequiredWeightedText, Is.EqualTo("0"));
            Assert.That(view.BonusPointsText, Is.EqualTo("22")); Assert.That(view.BonusWeightedText, Is.EqualTo("11"));
            Assert.That(view.ScoreText, Is.EqualTo("12"));
        }

        [Test]
        public void HugeResultProjectionDoesNotConvertScoresOrBonusRatesThroughFloatingPoint()
        {
            GameConfig config = Config(); BigInteger huge = BigInteger.Pow(10, 100);
            RoundResult result = Evaluate(config, huge, huge / 2);
            RoundResultView view = RoundResultProjection.Present(config, result);
            Assert.That(view.BonusStatusText, Is.EqualTo("200% 報酬×4"));
            Assert.That(view.ScoreText, Is.EqualTo(Format.Big(result.Evaluation.Score, config)));
            Assert.That(view.BonusWeightedText, Is.EqualTo(Format.Big(huge * config.Material("mushroom").BasePoint * 2, config)));
        }

        [Test]
        public void SuccessfulTitleRewardIsPersistedIdempotentlyWithoutRemovingExistingOwnership()
        {
            JObject data = Data(); GameConfig config = GameConfig.FromJson(data.ToString());
            ProfileData original = ProfilePersistence.Load(data, null); RoundResult result = Complete(Server(config), config);
            var before = RoundResultProjection.Present(config, result, "player1", original.OwnedFirstTitles, original.OwnedSecondTitles);
            Assert.That(before.TitleRewardText, Does.Contain("鍋奉行"));
            ProfileData granted = ProfilePersistence.ApplyResultRewards(data, original, result, "player1");
            granted.SecondTitle = "鍋奉行"; ProfileData twice = ProfilePersistence.ApplyResultRewards(data, granted, result, "player1");
            ProfileData loaded = ProfilePersistence.Load(data, ProfilePersistence.Save(data, twice));
            Assert.That(loaded.OwnedFirstTitles, Is.EquivalentTo(original.OwnedFirstTitles));
            Assert.That(loaded.OwnedSecondTitles, Does.Contain("鍋奉行")); Assert.That(loaded.OwnedSecondTitles.Count(title => title == "鍋奉行"), Is.EqualTo(1));
            Assert.That(loaded.SecondTitle, Is.EqualTo("鍋奉行"));
            Assert.That(RoundResultProjection.Present(config, result, "player1", loaded.OwnedFirstTitles, loaded.OwnedSecondTitles).TitleRewardText, Is.EqualTo("獲得済み"));
        }

        [Test]
        public void InvalidOrDuplicateSavedTitlePartsAreRemovedAndOldSavesKeepInitialOwnership()
        {
            JObject data = Data();
            var value = new ProfileData { OwnedFirstTitles = new[] { "伝説の", "invalid", "伝説の" }, OwnedSecondTitles = null, FirstTitle = "伝説の" };
            ProfileData normalized = ProfilePersistence.Normalize(data, value);
            Assert.That(normalized.OwnedFirstTitles, Does.Not.Contain("invalid"));
            Assert.That(normalized.OwnedFirstTitles.Count(title => title == "伝説の"), Is.EqualTo(1));
            Assert.That(normalized.FirstTitle, Is.EqualTo("伝説の"));
            ProfileData old = ProfilePersistence.Load(data, "{\"Name\":\"旧セーブ\"}");
            Assert.That(old.OwnedFirstTitles, Is.EquivalentTo(data["outGame"]["profile"]["initialOwnedFirst"].Values<string>()));
            Assert.That(old.OwnedSecondTitles, Is.EquivalentTo(data["outGame"]["profile"]["initialOwnedSecond"].Values<string>()));
        }

        [Test]
        public void TitleStreakEligibilityUsesTheSelectedActorsCurrentRoundAndRejectsFailure()
        {
            GameConfig config = Config(data => data["titles"]["unlockRules"] = new JArray(new JObject {
                ["part"] = "first", ["value"] = "神話の", ["minimumRound"] = 2, ["minimumStreak"] = 3, ["requiresClear"] = false }));
            var history = new[] { new OperationRecord(1, "player1", OperationKind.Gather, "wood", "rare", 3, 0, 1, 10, null, 10),
                new OperationRecord(2, "player2", OperationKind.Gather, "wood", "rare", 3, 0, 2, 10, null, 4),
                new OperationRecord(3, "player1", OperationKind.Gather, "wood", "rare", 3, 0, 2, 11, null, 2) };
            RoundResult result = Evaluate(config, 1, 1, 2, history);
            Assert.That(RoundResultProjection.EligibleTitles(config, result, "player1"), Is.Empty);
            Assert.That(RoundResultProjection.EligibleTitles(config, result, "player2").Single().Value, Is.EqualTo("神話の"));
            var failedInventory = new Inventory(config); RoundEvaluation failure = Scoring.Evaluate(config, failedInventory, SmallGoals());
            var failed = new RoundResult(2, 202647, config.DurationSeconds, failure, 0, history);
            Assert.That(RoundResultProjection.EligibleTitles(config, failed, "player2"), Is.Empty);
        }
    }
}
