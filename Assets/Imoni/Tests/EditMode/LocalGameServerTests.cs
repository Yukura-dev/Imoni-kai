using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class LocalGameServerTests
    {
        private static GameConfig Config(Action<JObject> change = null)
        {
#if UNITY_EDITOR
            string path = Path.Combine(UnityEngine.Application.dataPath, "Imoni", "Config", "GameConfig.json");
#else
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            JObject json = JObject.Parse(File.ReadAllText(path));
            change?.Invoke(json);
            return GameConfig.FromJson(json.ToString());
        }

        private static RoundObjectives SmallObjectives() =>
            new RoundObjectives(new[] { new Objective("wood", BigInteger.One) }, new Objective("mushroom", BigInteger.One));

        private static PlayerSnapshot Player(GameSnapshot state, string id = "player1") =>
            state.Players.Single(player => player.Id == id);

        private static BigInteger Quantity(GameSnapshot state, string material, string quality) =>
            state.Lots.Where(lot => lot.MaterialId == material && lot.QualityId == quality)
                .Aggregate(BigInteger.Zero, (sum, lot) => sum + lot.Quantity);

        private static void BuyForSuccess(LocalGameServer server)
        {
            server.Buy("player1", "wood", BigInteger.One);
            server.Buy("player2", "mushroom", BigInteger.One);
        }

        [Test]
        public void NewChallengeUsesConfigAndPublishesRequiredGoalsAtZero()
        {
            GameConfig config = Config(json => { json["team"]["size"] = 2; json["economy"]["startFunds"] = 12345; });
            IGameServer server = new LocalGameServer(config, 42, new[] { "alice", "bob" });
            GameSnapshot state = server.Snapshot();
            Assert.That(state.Phase, Is.EqualTo(RoundPhase.Collecting));
            Assert.That(state.RoundNumber, Is.EqualTo(1));
            Assert.That(state.Seed, Is.EqualTo(42));
            Assert.That(state.ElapsedSeconds, Is.Zero);
            Assert.That(state.Funds, Is.EqualTo(new BigInteger(12345)));
            Assert.That(state.Players.Select(player => player.Id), Is.EqualTo(new[] { "alice", "bob" }));
            Assert.That(state.RequiredObjectives.Select(goal => goal.Amount),
                Is.EqualTo(config.DefaultObjectives.Required.Select(goal => goal.Amount)));
            Assert.That(state.BonusObjective, Is.Null);
            Assert.That(state.Events.Select(item => item.Kind), Is.EqualTo(new[] { RoundEventKind.Started }));
            Assert.That(state.Events[0].ElapsedSeconds, Is.Zero);
        }

        [Test]
        public void BonusIsHiddenUntilExactlyTwoMinutesAndRevealedOnlyOnce()
        {
            GameConfig config = Config();
            var server = new LocalGameServer(config, 7);
            server.Tick(119.999m);
            Assert.That(server.Snapshot().BonusObjective, Is.Null);
            server.Tick(0.001m);
            GameSnapshot state = server.Snapshot();
            Assert.That(state.BonusRevealed, Is.True);
            Assert.That(state.BonusObjective.MaterialId, Is.EqualTo(config.DefaultObjectives.Bonus.MaterialId));
            server.Tick(0m);
            server.Tick(1m);
            RoundEvent[] reveals = server.Snapshot().Events.Where(item => item.Kind == RoundEventKind.BonusRevealed).ToArray();
            Assert.That(reveals.Length, Is.EqualTo(1));
            Assert.That(reveals[0].ElapsedSeconds, Is.EqualTo(120m));
        }

        [Test]
        public void TimerUsesChangedConfigAndIgnoresDevelopmentTimeScale()
        {
            GameConfig config = Config(json =>
            {
                json["round"]["durationSec"] = 11;
                json["round"]["bonusRevealSec"] = 4;
                json["round"]["mockTimeScale"] = 100;
            });
            var server = new LocalGameServer(config, 7);
            server.Tick(1m);
            Assert.That(server.Snapshot().ElapsedSeconds, Is.EqualTo(1m));
            server.Tick(3m);
            Assert.That(server.Snapshot().BonusRevealed, Is.True);
            server.Tick(7m);
            Assert.That(server.Snapshot().LastResult.ElapsedSeconds, Is.EqualTo(11m));
        }

        [Test]
        public void LargeTickEmitsOrderedBoundaryEventsAtTheirExactTimes()
        {
            var server = new LocalGameServer(Config(), 7);
            server.Tick(decimal.MaxValue);
            GameSnapshot state = server.Snapshot();
            Assert.That(state.Events.Select(item => item.Kind), Is.EqualTo(new[]
            {
                RoundEventKind.Started, RoundEventKind.BonusRevealed, RoundEventKind.TimeUp, RoundEventKind.Failed
            }));
            Assert.That(state.Events.Select(item => item.ElapsedSeconds), Is.EqualTo(new[] { 0m, 120m, 300m, 300m }));
            server.Tick(decimal.MaxValue);
            Assert.That(server.Snapshot().Events.Count, Is.EqualTo(4));
        }

        [Test]
        public void NegativeTickIsRejectedWithoutChangingTime()
        {
            var server = new LocalGameServer(Config(), 7);
            Assert.Throws<ArgumentOutOfRangeException>(() => server.Tick(-1m));
            Assert.That(server.Snapshot().ElapsedSeconds, Is.Zero);
            Assert.That(server.Snapshot().Events.Count, Is.EqualTo(1));
        }

        [Test]
        public void FiveMinutesStopsGatheringAndEvaluatesTheInventory()
        {
            GameConfig config = Config();
            var server = new LocalGameServer(config, 7, objectives: SmallObjectives());
            server.Tick(299.999m);
            GatherResult gathered = server.Gather("player1", "wood");
            Assert.That(gathered.Quantity, Is.GreaterThan(BigInteger.Zero));
            server.Tick(0.001m);
            GameSnapshot state = server.Snapshot();
            Assert.That(state.Phase, Is.EqualTo(RoundPhase.Succeeded));
            Assert.That(state.LastResult.Evaluation.Success, Is.True);
            Assert.That(state.LastResult.ElapsedSeconds, Is.EqualTo(300m));
            Assert.That(state.Lots, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => server.Gather("player1", "wood"));
        }

        [TestCase(OperationKind.Gather)]
        [TestCase(OperationKind.Buy)]
        [TestCase(OperationKind.Sell)]
        [TestCase(OperationKind.SetProtection)]
        [TestCase(OperationKind.Upgrade)]
        public void EveryInventoryOrFundsCommandIsRejectedAfterTimeUp(OperationKind operation)
        {
            var server = new LocalGameServer(Config(), 7);
            server.Tick(300m);
            Assert.Throws<InvalidOperationException>(() =>
            {
                switch (operation)
                {
                    case OperationKind.Gather: server.Gather("player1", "wood"); break;
                    case OperationKind.Buy: server.Buy("player1", "wood", 1); break;
                    case OperationKind.Sell: server.Sell("player1", "wood", "normal", 1); break;
                    case OperationKind.SetProtection: server.SetProtection("player1", "wood", "normal", 0); break;
                    case OperationKind.Upgrade: server.Upgrade("player1", "move"); break;
                }
            });
            Assert.That(server.Snapshot().History, Is.Empty);
        }

        [Test]
        public void SuccessConsumesLockedAndUnrelatedItemsButKeepsFundsUpgradesAndSeed()
        {
            GameConfig config = Config();
            var server = new LocalGameServer(config, 29, objectives: SmallObjectives());
            server.Upgrade("player1", "move");
            BuyForSuccess(server);
            server.Buy("player2", "negi", 1);
            server.SetProtection("player3", "wood", "normal", 1);
            BigInteger before = server.Snapshot().Funds;
            server.Tick(config.DurationSeconds);
            GameSnapshot succeeded = server.Snapshot();
            Assert.That(succeeded.Phase, Is.EqualTo(RoundPhase.Succeeded));
            Assert.That(succeeded.Funds, Is.EqualTo(before + succeeded.LastResult.Evaluation.FundsReward));
            Assert.That(succeeded.Lots, Is.Empty);
            Assert.That(succeeded.Protection, Is.Empty);
            Assert.That(Player(succeeded).Upgrades["move"], Is.EqualTo(1));
            Assert.That(succeeded.CumulativeScore, Is.EqualTo(succeeded.LastResult.Evaluation.Score));
            server.StartNextRound(SmallObjectives());
            GameSnapshot next = server.Snapshot();
            Assert.That(next.RoundNumber, Is.EqualTo(2));
            Assert.That(next.Seed, Is.EqualTo(29));
            Assert.That(next.ElapsedSeconds, Is.Zero);
            Assert.That(next.Funds, Is.EqualTo(succeeded.Funds));
            Assert.That(Player(next).Upgrades["move"], Is.EqualTo(1));
            Assert.That(next.BonusObjective, Is.Null);
        }

        [Test]
        public void FailedSecondRoundResetsRunAndKeepsDetachedFailureResult()
        {
            GameConfig config = Config(json => json["qualityRoll"]["topBaseChance"] = 1);
            var server = new LocalGameServer(config, 29, objectives: SmallObjectives());
            BuyForSuccess(server);
            server.Tick(config.DurationSeconds);
            server.StartNextRound(SmallObjectives());
            server.Upgrade("player1", "amount");
            server.Gather("player1", "negi");
            server.Buy("player2", "mushroom", 1);
            server.SetProtection("player3", "mushroom", "normal", 1);
            Assert.That(server.Snapshot().CumulativeScore, Is.GreaterThan(BigInteger.Zero));
            Assert.That(Player(server.Snapshot()).Streak, Is.EqualTo(1));
            server.Tick(config.DurationSeconds);
            GameSnapshot state = server.Snapshot();
            Assert.That(state.Phase, Is.EqualTo(RoundPhase.Failed));
            Assert.That(state.RoundNumber, Is.EqualTo(1));
            Assert.That(state.ElapsedSeconds, Is.Zero);
            Assert.That(state.Funds, Is.EqualTo(config.StartFunds));
            Assert.That(state.CumulativeScore, Is.EqualTo(BigInteger.Zero));
            Assert.That(state.Lots, Is.Empty);
            Assert.That(state.Protection, Is.Empty);
            Assert.That(state.History, Is.Empty);
            Assert.That(state.Players.All(player => player.Streak == 0 && player.Upgrades.Values.All(level => level == 0)), Is.True);
            Assert.That(state.LastResult.RoundNumber, Is.EqualTo(2));
            Assert.That(state.LastResult.ElapsedSeconds, Is.EqualTo(config.DurationSeconds));
            Assert.That(state.LastResult.Evaluation.MissingMaterialIds, Is.EqualTo(new[] { "wood" }));
            Assert.That(state.LastResult.History, Is.Not.Empty);
            Assert.That(state.Events.Last().RoundNumber, Is.EqualTo(2));
            Assert.That(state.Events.Last().ElapsedSeconds, Is.EqualTo(config.DurationSeconds));
            RoundResult retained = state.LastResult;
            server.Retry(51, SmallObjectives());
            Assert.That(server.Snapshot().Seed, Is.EqualTo(51));
            Assert.That(server.Snapshot().LastResult, Is.Null);
            Assert.That(server.Snapshot().Phase, Is.EqualTo(RoundPhase.Collecting));
            Assert.That(server.Snapshot().Events.Count, Is.EqualTo(1));
            Assert.That(retained.RoundNumber, Is.EqualTo(2));
        }

        [Test]
        public void FiveConsecutiveSuccessesClearAndSumOnlyCompletedRoundScores()
        {
            GameConfig config = Config();
            var server = new LocalGameServer(config, 29, objectives: SmallObjectives());
            BigInteger total = BigInteger.Zero;
            for (int round = 1; round <= config.RoundsToClear; round++)
            {
                BuyForSuccess(server);
                server.Tick(config.DurationSeconds);
                total += server.Snapshot().LastResult.Evaluation.Score;
                Assert.That(server.Snapshot().CumulativeScore, Is.EqualTo(total));
                if (round < config.RoundsToClear)
                {
                    Assert.That(server.Snapshot().Phase, Is.EqualTo(RoundPhase.Succeeded));
                    server.StartNextRound(SmallObjectives());
                }
            }
            Assert.That(server.Snapshot().RoundNumber, Is.EqualTo(5));
            Assert.That(server.Snapshot().Phase, Is.EqualTo(RoundPhase.Cleared));
            Assert.That(server.Snapshot().Events.Count(item => item.Kind == RoundEventKind.Cleared), Is.EqualTo(1));
            Assert.Throws<InvalidOperationException>(() => server.StartNextRound());
            Assert.Throws<InvalidOperationException>(() => server.Retry(51));
        }

        [Test]
        public void ClearCountAndRoundObjectivesCanBeSuppliedFromUpdatedRules()
        {
            GameConfig config = Config(json => json["round"]["roundsToClear"] = 1);
            var goals = new RoundObjectives(new[] { new Objective("negi", 1) }, new Objective("fish", 1));
            var server = new LocalGameServer(config, 29, objectives: goals);
            server.Buy("player1", "negi", 1);
            server.Tick(config.DurationSeconds);
            Assert.That(server.Snapshot().Phase, Is.EqualTo(RoundPhase.Cleared));
            Assert.That(server.Snapshot().LastResult.Evaluation.RequiredGoals[0].MaterialId, Is.EqualTo("negi"));
        }

        [Test]
        public void SameSeedAndOperationSequenceReproduceQualityStreakQuantityAndPoints()
        {
            GameConfig config = Config();
            var first = new LocalGameServer(config, 7813);
            var second = new LocalGameServer(config, 7813);
            first.Upgrade("player1", "luck");
            second.Upgrade("player1", "luck");
            string[] materials = { "wood", "taro", "negi", "mushroom" };
            string[] actors = { "player1", "player2", "player3" };
            for (int index = 0; index < 200; index++)
            {
                GatherResult a = first.Gather(actors[index % actors.Length], materials[index % materials.Length]);
                GatherResult b = second.Gather(actors[index % actors.Length], materials[index % materials.Length]);
                Assert.That((a.QualityId, a.Streak, a.Quantity, a.PointMultiplierRaw),
                    Is.EqualTo((b.QualityId, b.Streak, b.Quantity, b.PointMultiplierRaw)));
                first.Tick(0.25m); second.Tick(0.25m);
            }
            Assert.That(first.Snapshot().History.Select(entry => (entry.ActorId, entry.Kind, entry.Quantity, entry.ElapsedSeconds)),
                Is.EqualTo(second.Snapshot().History.Select(entry => (entry.ActorId, entry.Kind, entry.Quantity, entry.ElapsedSeconds))));
        }

        [Test]
        public void RejectedCommandsDoNotConsumeRandomnessOrChangeFunds()
        {
            GameConfig config = Config();
            var first = new LocalGameServer(config, 72);
            var control = new LocalGameServer(config, 72);
            Assert.Throws<ArgumentException>(() => first.Gather("unknown", "wood"));
            Assert.Throws<ArgumentException>(() => first.Gather("player1", "unknown"));
            Assert.Throws<ArgumentException>(() => first.Buy("unknown", "wood", 1));
            Assert.Throws<ArgumentException>(() => first.Sell("unknown", "wood", "normal", 1));
            Assert.Throws<ArgumentException>(() => first.SetProtection("unknown", "wood", "normal", 0));
            Assert.Throws<ArgumentException>(() => first.Upgrade("unknown", "move"));
            Assert.Throws<ArgumentException>(() => first.Upgrade("player1", "unknown"));
            Assert.Throws<InvalidOperationException>(() => first.Buy("player1", "wood", BigInteger.Pow(10, 40)));
            Assert.That(first.Snapshot().Funds, Is.EqualTo(config.StartFunds));
            Assert.That(first.Snapshot().History, Is.Empty);
            for (int index = 0; index < 20; index++)
                Assert.That(first.Gather("player1", "wood").QualityId, Is.EqualTo(control.Gather("player1", "wood").QualityId));
        }

        [Test]
        public void StreakIsPerPlayerSurvivesMaterialChangesAndDoesNotAffectPurchases()
        {
            GameConfig config = Config(json => json["qualityRoll"]["topBaseChance"] = 1);
            var server = new LocalGameServer(config, 72);
            GatherResult first = server.Gather("player1", "wood");
            GatherResult second = server.Gather("player1", "negi");
            Assert.That(first.Streak, Is.EqualTo(1));
            Assert.That(second.Streak, Is.EqualTo(2));
            Assert.That(Player(server.Snapshot(), "player2").Streak, Is.Zero);
            server.Buy("player1", "taro", 1);
            Assert.That(Player(server.Snapshot()).Streak, Is.EqualTo(2));
            MaterialLot bought = server.Snapshot().Lots.Last();
            Assert.That(bought.QualityId, Is.EqualTo(config.PurchasedQualityId));
            Assert.That(bought.PointMultiplierRaw, Is.EqualTo(config.FixedScale));
            Assert.That(server.Snapshot().Lots[0].PointMultiplierRaw, Is.EqualTo(first.PointMultiplierRaw));
        }

        [Test]
        public void NonHighestGatherResetsOnlyThatPlayersStreak()
        {
            var server = new LocalGameServer(Config(), 801);
            bool sawTop = false, sawReset = false;
            int previous = 0;
            for (int index = 0; index < 500; index++)
            {
                GatherResult result = server.Gather("player1", index % 2 == 0 ? "wood" : "negi");
                if (result.Streak > 0) sawTop = true;
                if (previous > 0 && result.Streak == 0) { sawReset = true; break; }
                previous = result.Streak;
            }
            Assert.That(sawTop && sawReset, Is.True);
            Assert.That(Player(server.Snapshot(), "player2").Streak, Is.Zero);
        }

        [Test]
        public void SaleBelowProtectionRejectsAtomicallyAcrossSharedActors()
        {
            GameConfig config = Config();
            var server = new LocalGameServer(config, 72);
            server.Buy("player1", "wood", 5);
            server.SetProtection("player2", "wood", "normal", 4);
            GameSnapshot before = server.Snapshot();
            Assert.Throws<InvalidOperationException>(() => server.Sell("player3", "wood", "normal", 2));
            GameSnapshot after = server.Snapshot();
            Assert.That(after.Funds, Is.EqualTo(before.Funds));
            Assert.That(after.History.Count, Is.EqualTo(before.History.Count));
            Assert.That(Quantity(after, "wood", "normal"), Is.EqualTo(new BigInteger(5)));
            server.Sell("player3", "wood", "normal", 1);
            Assert.That(Quantity(server.Snapshot(), "wood", "normal"), Is.EqualTo(new BigInteger(4)));
        }

        [Test]
        public void ConcurrentSalesCannotSpendTheSameLotTwice()
        {
            GameConfig config = Config();
            var server = new LocalGameServer(config, 72);
            server.Buy("player1", "wood", 1);
            int successes = 0, rejections = 0;
            Parallel.ForEach(new[] { "player2", "player3" }, actor =>
            {
                try { server.Sell(actor, "wood", "normal", 1); System.Threading.Interlocked.Increment(ref successes); }
                catch (InvalidOperationException) { System.Threading.Interlocked.Increment(ref rejections); }
            });
            Assert.That(successes, Is.EqualTo(1));
            Assert.That(rejections, Is.EqualTo(1));
            Assert.That(server.Snapshot().Lots, Is.Empty);
            Assert.That(server.Snapshot().History.Count(record => record.Kind == OperationKind.Sell), Is.EqualTo(1));
        }

        [Test]
        public void PurchaseAndUpgradeInsufficientFundsLeaveEveryValueUntouched()
        {
            GameConfig config = Config(json => json["economy"]["startFunds"] = 1);
            var server = new LocalGameServer(config, 72);
            Assert.Throws<InvalidOperationException>(() => server.Buy("player1", "wood", 1));
            Assert.Throws<InvalidOperationException>(() => server.Upgrade("player1", "move"));
            GameSnapshot state = server.Snapshot();
            Assert.That(state.Funds, Is.EqualTo(BigInteger.One));
            Assert.That(state.Lots, Is.Empty);
            Assert.That(state.History, Is.Empty);
            Assert.That(Player(state).Upgrades["move"], Is.Zero);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidPurchaseOrSaleQuantityCannotChangeTheRun(int quantity)
        {
            var server = new LocalGameServer(Config(), 72);
            Assert.Throws<ArgumentOutOfRangeException>(() => server.Buy("player1", "wood", quantity));
            Assert.Throws<ArgumentOutOfRangeException>(() => server.Sell("player1", "wood", "normal", quantity));
            Assert.That(server.Snapshot().History, Is.Empty);
        }

        [Test]
        public void OperationsRecordActorKindQuantityFundsAndSimulationTime()
        {
            var server = new LocalGameServer(Config(), 72);
            server.Tick(23.5m);
            BigInteger cost = server.Buy("player2", "wood", 2);
            server.SetProtection("player3", "wood", "normal", 1);
            BigInteger proceeds = server.Sell("player1", "wood", "normal", 1);
            OperationRecord bought = server.Snapshot().History[0];
            OperationRecord sold = server.Snapshot().History[2];
            Assert.That(bought.ActorId, Is.EqualTo("player2"));
            Assert.That(bought.Kind, Is.EqualTo(OperationKind.Buy));
            Assert.That(bought.MaterialId, Is.EqualTo("wood"));
            Assert.That(bought.QualityId, Is.EqualTo("normal"));
            Assert.That(bought.Quantity, Is.EqualTo(new BigInteger(2)));
            Assert.That(bought.FundsDelta, Is.EqualTo(-cost));
            Assert.That(bought.ElapsedSeconds, Is.EqualTo(23.5m));
            Assert.That(bought.RoundNumber, Is.EqualTo(1));
            Assert.That(sold.ActorId, Is.EqualTo("player1"));
            Assert.That(sold.Kind, Is.EqualTo(OperationKind.Sell));
            Assert.That(sold.FundsDelta, Is.EqualTo(proceeds));
            Assert.That(sold.Sequence, Is.EqualTo(3));
        }

        [Test]
        public void ClientSnapshotsCannotMutateServerOrChangeAfterLaterCommands()
        {
            var server = new LocalGameServer(Config(), 72);
            server.Buy("player1", "wood", 2);
            GameSnapshot before = server.Snapshot();
            Assert.Throws<NotSupportedException>(() => ((IList<MaterialLot>)before.Lots).Clear());
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, int>)Player(before).Upgrades)["move"] = 99);
            server.Sell("player2", "wood", "normal", 1);
            server.Upgrade("player1", "move");
            Assert.That(Quantity(before, "wood", "normal"), Is.EqualTo(new BigInteger(2)));
            Assert.That(Player(before).Upgrades["move"], Is.Zero);
            Assert.That(before.History.Count, Is.EqualTo(1));
            Assert.That(Quantity(server.Snapshot(), "wood", "normal"), Is.EqualTo(BigInteger.One));
        }

        [Test]
        public void SoldBonusMaterialCannotReturnInTheFinalScore()
        {
            var server = new LocalGameServer(Config(), 72, objectives: SmallObjectives());
            BuyForSuccess(server);
            server.Sell("player3", "mushroom", "normal", 1);
            server.Tick(300m);
            RoundEvaluation result = server.Snapshot().LastResult.Evaluation;
            Assert.That(result.Success, Is.True);
            Assert.That(result.BonusPoints, Is.EqualTo(BigInteger.Zero));
            Assert.That(result.BonusMultiplier, Is.EqualTo(BigInteger.Zero));
            Assert.That(result.Score, Is.EqualTo(BigInteger.Zero)); // floor(0.5 * one ordinary wood point).
        }

        [Test]
        public void LifecycleAndMemberValidationRejectInvalidTransitions()
        {
            GameConfig config = Config();
            Assert.Throws<ArgumentException>(() => new LocalGameServer(config, 7, new[] { "one" }));
            Assert.Throws<ArgumentException>(() => new LocalGameServer(config, 7, new[] { "same", "same", "other" }));
            var server = new LocalGameServer(config, 7);
            Assert.Throws<InvalidOperationException>(() => server.StartNextRound());
            Assert.Throws<InvalidOperationException>(() => server.Retry(8));
            Assert.That(server.Snapshot().Events.Count, Is.EqualTo(1));
        }
    }
}
