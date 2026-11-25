using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class GatheringSimulationTests
    {
        private static GameConfig Config(Action<JObject> change = null)
        {
#if UNITY_EDITOR
            string path = Path.Combine(UnityEngine.Application.dataPath, "Imoni", "Config", "GameConfig.json");
#else
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            JObject data = JObject.Parse(File.ReadAllText(path));
            change?.Invoke(data);
            return GameConfig.FromJson(data.ToString());
        }

        private static PlayerSnapshot Player(LocalGameServer server, string actor = "player1") =>
            server.Snapshot().Players.Single(player => player.Id == actor);

        private static ResourceNode Nearest(LocalGameServer server, string actor = "player1", string material = null) =>
            server.World.Resources.Where(node => (material == null || node.MaterialId == material) &&
                server.CanSelectResource(actor, node.Id))
            .OrderBy(node => Pathfinding.FindPathToResource(server.World, Player(server, actor).Position,
                node, server.Snapshot().RoundNumber).Steps.Count).ThenBy(node => node.Id, StringComparer.Ordinal).First();

        private static decimal Travel(LocalGameServer server, GameConfig config, ResourceNode node, string actor = "player1") =>
            (Pathfinding.FindPathToResource(server.World, Player(server, actor).Position, node,
                server.Snapshot().RoundNumber).Steps.Count - 1) *
            GatheringTiming.MoveStepSeconds(config, Player(server, actor).Upgrades["move"]);

        private static ResourceNode Node(GameConfig config) =>
            new ResourceNode(config, "test-wood", "wood", "forest", new GridPosition(1, 1));

        [Test]
        public void StockDepletesAfterThreeGathersThenPassesEveryGrowthStageAtConfiguredTimes()
        {
            GameConfig config = Config();
            ResourceNode node = Node(config);
            Assert.That(node.RemainingStock, Is.EqualTo(3));
            for (int index = 0; index < config.ResourceStock; index++) Assert.That(node.TryGather(), Is.True);
            Assert.That(node.TryGather(), Is.False);
            Assert.That(node.GrowthStage, Is.EqualTo(ResourceGrowthStage.Stump));
            ResourceSnapshot frozen = node.Snapshot();
            node.Tick(10);
            Assert.That(node.GrowthStage, Is.EqualTo(ResourceGrowthStage.Sapling));
            node.Tick(10);
            Assert.That(node.GrowthStage, Is.EqualTo(ResourceGrowthStage.Young));
            node.Tick(10);
            Assert.That(node.GrowthStage, Is.EqualTo(ResourceGrowthStage.Mature));
            node.Tick(9.999m);
            Assert.That(node.IsReady, Is.False);
            node.Tick(0.001m);
            Assert.That(node.IsReady, Is.True);
            Assert.That(node.GrowthStage, Is.EqualTo(ResourceGrowthStage.Ready));
            Assert.That(node.RemainingStock, Is.EqualTo(config.ResourceStock));
            Assert.That(frozen.GrowthStage, Is.EqualTo(ResourceGrowthStage.Stump));
            Assert.That(frozen.RemainingStock, Is.Zero);
        }

        [Test]
        public void PartiallyHarvestedNodeDoesNotRegrowAndNegativeTimeCannotMutateIt()
        {
            ResourceNode node = Node(Config());
            node.TryGather();
            node.Tick(100);
            Assert.That(node.RemainingStock, Is.EqualTo(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => node.Tick(-1));
            Assert.That(node.RemainingStock, Is.EqualTo(2));
        }

        [Test]
        public void StockAndRegrowthUseChangedConfiguration()
        {
            GameConfig config = Config(data =>
            {
                data["gathering"]["nodeStockPerCycle"] = 1;
                data["gathering"]["regrowSec"] = 8;
            });
            ResourceNode node = Node(config);
            node.TryGather();
            node.Tick(7.999m);
            Assert.That(node.IsReady, Is.False);
            node.Tick(0.001m);
            Assert.That(node.RemainingStock, Is.EqualTo(1));
        }

        [Test]
        public void SelectionMovesAlongPathThenGathersOnlyAfterArrivalAndWork()
        {
            GameConfig config = Config();
            var server = new LocalGameServer(config, 72, enableBots: false);
            ResourceNode node = Nearest(server);
            decimal travel = Travel(server, config, node);
            decimal collect = GatheringTiming.CollectSeconds(config, 0, node);
            Assert.That(server.TrySelectResource("player1", node.Id), Is.True);
            Assert.Throws<InvalidOperationException>(() => server.Gather("player1", node.MaterialId));
            server.Tick(travel);
            Assert.That(Player(server).Action, Is.EqualTo(PlayerAction.Gathering));
            Assert.That(server.Snapshot().Lots, Is.Empty);
            Assert.That(Math.Abs(Player(server).Position.X - node.Position.X) +
                Math.Abs(Player(server).Position.Y - node.Position.Y), Is.EqualTo(1));
            server.Tick(collect - 0.000001m);
            Assert.That(server.Snapshot().Lots, Is.Empty);
            server.Tick(0.000001m);
            Assert.That(server.Snapshot().History.Count(item => item.Kind == OperationKind.Gather), Is.EqualTo(1));
            Assert.That(node.RemainingStock, Is.EqualTo(config.ResourceStock - 1));
            Assert.That(server.Snapshot().Lots.Single().Quantity, Is.GreaterThan(BigInteger.Zero));
        }

        [Test]
        public void MovementAndGatherSpeedUpgradesShortenTheActualActionDurations()
        {
            GameConfig config = Config();
            var server = new LocalGameServer(config, 72, enableBots: false);
            ResourceNode node = Nearest(server);
            decimal originalTravel = Travel(server, config, node);
            decimal originalCollect = GatheringTiming.CollectSeconds(config, 0, node);
            server.Upgrade("player1", "move");
            server.Upgrade("player1", "speed");
            decimal travel = Travel(server, config, node);
            decimal collect = GatheringTiming.CollectSeconds(config, 1, node);
            Assert.That(travel, Is.LessThan(originalTravel));
            Assert.That(collect, Is.LessThan(originalCollect));
            server.TrySelectResource("player1", node.Id);
            server.Tick(travel + collect);
            Assert.That(server.Snapshot().History.Last().Kind, Is.EqualTo(OperationKind.Gather));
            Assert.That(server.Snapshot().History.Last().ElapsedSeconds, Is.EqualTo(travel + collect));
        }

        [Test]
        public void GatherAutomaticallyRepeatsUntilDepletionAndStopsAllOtherCollectors()
        {
            GameConfig config = Config();
            var server = new LocalGameServer(config, 72, enableBots: false);
            ResourceNode node = Nearest(server);
            decimal travel = Travel(server, config, node);
            foreach (PlayerSnapshot player in server.Snapshot().Players)
                Assert.That(server.TrySelectResource(player.Id, node.Id), Is.True);
            server.Tick(travel + GatheringTiming.CollectSeconds(config, 0, node));
            Assert.That(node.IsReady, Is.False);
            Assert.That(server.Snapshot().History.Count(item => item.Kind == OperationKind.Gather), Is.EqualTo(3));
            Assert.That(server.Snapshot().Players.All(player => player.Action == PlayerAction.Idle), Is.True);
            Assert.That(server.CanSelectResource("player1", node.Id), Is.False);
            server.Tick(config.ResourceRegrowSeconds);
            Assert.That(node.IsReady, Is.True);
            Assert.That(server.CanSelectResource("player1", node.Id), Is.True);
        }

        [Test]
        public void CancelAndDisconnectDiscardIncompleteWork()
        {
            GameConfig config = Config();
            var server = new LocalGameServer(config, 72, enableBots: false);
            ResourceNode node = Nearest(server);
            server.TrySelectResource("player1", node.Id);
            server.Tick(Travel(server, config, node));
            server.Tick(GatheringTiming.CollectSeconds(config, 0, node) / 2);
            server.CancelAction("player1");
            server.Tick(10);
            Assert.That(server.Snapshot().History, Is.Empty);
            server.TrySelectResource("player1", node.Id);
            server.SetConnected("player1", false);
            server.Tick(10);
            Assert.That(server.Snapshot().History, Is.Empty);
            Assert.That(server.TrySelectResource("player1", node.Id), Is.False);
            server.SetConnected("player1", true);
            Assert.That(server.TrySelectResource("player1", node.Id), Is.True);
        }

        [Test]
        public void TwoBotsGatherThroughServerWhileDisconnectedBotWaitsThenResumes()
        {
            GameConfig config = Config();
            IGameServer server = new LocalGameServer(config, 72);
            Assert.That(server.Snapshot().Players.Count(player => player.IsBot), Is.EqualTo(2));
            server.SetConnected("player3", false);
            server.Tick(30);
            Assert.That(server.Snapshot().History.Any(item => item.ActorId == "player2" && item.Kind == OperationKind.Gather), Is.True);
            Assert.That(server.Snapshot().History.Any(item => item.ActorId == "player3"), Is.False);
            server.SetConnected("player3", true);
            server.Tick(30);
            Assert.That(server.Snapshot().History.Any(item => item.ActorId == "player3" && item.Kind == OperationKind.Gather), Is.True);
            Assert.That(server.Snapshot().History.Any(item => item.ActorId == "player1"), Is.False);
        }

        [Test]
        public void SameSeedAndOperationsReproduceWorldPlayersStockQualityAndInventory()
        {
            GameConfig config = Config();
            var first = new LocalGameServer(config, 127);
            var second = new LocalGameServer(config, 127);
            string target = Nearest(first).Id;
            foreach (LocalGameServer server in new[] { first, second })
            {
                server.Upgrade("player1", "amount");
                server.TrySelectResource("player1", target);
                server.Tick(21.75m);
                server.SetConnected("player3", false);
                server.Tick(7.25m);
                server.SetConnected("player3", true);
                server.Tick(31);
            }
            AssertEquivalent(first.Snapshot(), second.Snapshot());
        }

        [Test]
        public void LargeTickMatchesManySmallTicksIncludingRegrowthAndBotDecisions()
        {
            GameConfig config = Config();
            var first = new LocalGameServer(config, 127);
            var second = new LocalGameServer(config, 127);
            first.Tick(60);
            for (int index = 0; index < 600; index++) second.Tick(0.1m);
            AssertEquivalent(first.Snapshot(), second.Snapshot());
        }

        [Test]
        public void TimeUpWinsTieWithGatherCompletion()
        {
            GameConfig config = Config();
            var server = new LocalGameServer(config, 72, enableBots: false);
            ResourceNode node = Nearest(server);
            decimal duration = Travel(server, config, node) + GatheringTiming.CollectSeconds(config, 0, node);
            server.Tick(config.DurationSeconds - duration);
            server.TrySelectResource("player1", node.Id);
            server.Tick(duration);
            Assert.That(server.Snapshot().LastResult.History.Any(item => item.Kind == OperationKind.Gather), Is.False);
            Assert.That(server.Snapshot().Phase, Is.EqualTo(RoundPhase.Failed));
            Assert.Throws<InvalidOperationException>(() => server.Gather("player1", node.MaterialId));
        }

        [Test]
        public void SuccessfulRoundKeepsSameWorldAndPartialRegrowthIntoNextRound()
        {
            GameConfig config = Config();
            var objectives = new RoundObjectives(new[] { new Objective("wood", 1) }, new Objective("mushroom", 1));
            var server = new LocalGameServer(config, 72, objectives: objectives, enableBots: false);
            ResourceNode node = Nearest(server, material: "wood");
            WorldMap world = server.World;
            decimal work = Travel(server, config, node) + config.ResourceStock * GatheringTiming.CollectSeconds(config, 0, node);
            server.Buy("player1", "wood", 1);
            server.Tick(config.DurationSeconds - work - 5);
            server.TrySelectResource("player1", node.Id);
            server.Tick(work + 5);
            Assert.That(server.Snapshot().Phase, Is.EqualTo(RoundPhase.Succeeded));
            Assert.That(node.RegrowthSeconds, Is.EqualTo(5));
            server.StartNextRound(objectives);
            Assert.That(server.World, Is.SameAs(world));
            Assert.That(node.RemainingStock, Is.Zero);
            Assert.That(node.RegrowthSeconds, Is.EqualTo(5));
            server.Tick(config.ResourceRegrowSeconds - 5);
            Assert.That(node.IsReady, Is.True);
        }

        [Test]
        public void FirstAndLaterRoundUseWorldGeneratedObjectivesByDefault()
        {
            GameConfig config = Config();
            const int seed = 72;
            var server = new LocalGameServer(config, seed, enableBots: false);
            var generator = new ObjectiveGenerator(config);
            RoundObjectives first = generator.Generate(server.World, 1, new Rng(seed + 1), server.Snapshot().Players);
            Assert.That(server.Snapshot().RequiredObjectives.Select(goal => (goal.MaterialId, goal.Amount)),
                Is.EqualTo(first.Required.Select(goal => (goal.MaterialId, goal.Amount))));

            var fixture = new RoundObjectives(new[] { new Objective("wood", 1) }, new Objective("mushroom", 1));
            server = new LocalGameServer(config, seed, objectives: fixture, enableBots: false);
            server.Buy("player1", "wood", 1);
            server.Tick(config.DurationSeconds);
            WorldMap retained = server.World;
            server.StartNextRound();
            RoundObjectives second = generator.Generate(server.World, 2, new Rng(seed + 2), server.Snapshot().Players);
            Assert.That(server.Snapshot().RoundNumber, Is.EqualTo(2));
            Assert.That(server.World, Is.SameAs(retained));
            Assert.That(server.Snapshot().RequiredObjectives.Select(goal => (goal.MaterialId, goal.Amount)),
                Is.EqualTo(second.Required.Select(goal => (goal.MaterialId, goal.Amount))));
            Assert.That(server.Snapshot().RequiredObjectives.Select(goal => (goal.MaterialId, goal.Amount)),
                Is.Not.EqualTo(config.DefaultObjectives.Required.Select(goal => (goal.MaterialId, goal.Amount))));
            string[] available = server.World.ResourcesAvailable(2).Select(node => node.MaterialId).ToArray();
            Assert.That(server.Snapshot().RequiredObjectives.All(goal => available.Contains(goal.MaterialId)), Is.True);
            server.Tick(config.BonusRevealSeconds);
            Assert.That(server.Snapshot().BonusObjective.MaterialId, Is.EqualTo(second.Bonus.MaterialId));
            Assert.That(server.Snapshot().BonusObjective.Amount, Is.EqualTo(second.Bonus.Amount));
        }

        [Test]
        public void LockedOrMissingResourceIsUnselectableAndDoesNotCancelCurrentAction()
        {
            var server = new LocalGameServer(Config(), 72, enableBots: false);
            ResourceNode target = Nearest(server);
            ResourceNode locked = server.World.Resources.First(node => !server.World.IsUnlocked(node.Position, 1));
            server.TrySelectResource("player1", target.Id);
            PlayerSnapshot before = Player(server);
            Assert.That(server.CanSelectResource("player1", locked.Id), Is.False);
            Assert.That(server.TrySelectResource("player1", locked.Id), Is.False);
            Assert.That(server.TrySelectResource("player1", "missing"), Is.False);
            Assert.That(Player(server).TargetResourceId, Is.EqualTo(before.TargetResourceId));
            Assert.That(Player(server).ActionRemainingSeconds, Is.EqualTo(before.ActionRemainingSeconds));
        }

        [Test]
        public void UnavailableInjectedGoalIsRejectedWithoutAdvancingSuccessfulRound()
        {
            GameConfig config = Config(data => data["world"]["resourceChance"] = 0.000001m);
            var fixture = new RoundObjectives(new[] { new Objective("wood", 1) }, new Objective("mushroom", 1));
            LocalGameServer server = null;
            string unavailable = null;
            for (int seed = 0; seed < 16 && unavailable == null; seed++)
            {
                server = new LocalGameServer(config, seed, objectives: fixture, enableBots: false);
                string[] available = server.World.ResourcesAvailable(2).Select(node => node.MaterialId).ToArray();
                unavailable = config.Materials.Select(material => material.Id).FirstOrDefault(id => !available.Contains(id));
            }
            Assert.That(unavailable, Is.Not.Null, "The sparse fixture must have a material outside R2.");
            server.Buy("player1", "wood", 1);
            server.Tick(config.DurationSeconds);
            WorldMap retained = server.World;
            var invalid = new RoundObjectives(new[] { new Objective(unavailable, 1) }, new Objective("wood", 1));
            Assert.Throws<ArgumentException>(() => server.StartNextRound(invalid));
            Assert.That(server.Snapshot().RoundNumber, Is.EqualTo(1));
            Assert.That(server.Snapshot().Phase, Is.EqualTo(RoundPhase.Succeeded));
            Assert.That(server.World, Is.SameAs(retained));
        }

        [Test]
        public void RetryWithImpossibleGeneratedGoalsLeavesFailedStateUntouched()
        {
            GameConfig config = Config(data =>
            {
                data["round"]["durationSec"] = 1;
                data["round"]["bonusRevealSec"] = 0.5m;
            });
            var fixture = new RoundObjectives(new[] { new Objective("wood", 1) }, new Objective("mushroom", 1));
            var server = new LocalGameServer(config, 72, objectives: fixture, enableBots: false);
            server.Tick(config.DurationSeconds);
            WorldMap world = server.World;
            GameSnapshot before = server.Snapshot();
            Assert.Throws<InvalidOperationException>(() => server.Retry(99));
            GameSnapshot after = server.Snapshot();
            Assert.That(after.Phase, Is.EqualTo(RoundPhase.Failed));
            Assert.That(after.Seed, Is.EqualTo(before.Seed));
            Assert.That(after.LastResult, Is.SameAs(before.LastResult));
            Assert.That(server.World, Is.SameAs(world));
            Assert.That(after.Events, Is.EqualTo(before.Events));
        }

        private static void AssertEquivalent(GameSnapshot first, GameSnapshot second)
        {
            Assert.That(second.History.Select(item => (item.ActorId, item.Kind, item.MaterialId, item.QualityId, item.Quantity, item.ElapsedSeconds)),
                Is.EqualTo(first.History.Select(item => (item.ActorId, item.Kind, item.MaterialId, item.QualityId, item.Quantity, item.ElapsedSeconds))));
            Assert.That(second.Resources.Select(node => (node.Id, node.RemainingStock, node.GrowthStage, node.RegrowthSeconds)),
                Is.EqualTo(first.Resources.Select(node => (node.Id, node.RemainingStock, node.GrowthStage, node.RegrowthSeconds))));
            Assert.That(second.Players.Select(player => (player.Id, player.Position, player.Action, player.ActionRemainingSeconds, player.Streak)),
                Is.EqualTo(first.Players.Select(player => (player.Id, player.Position, player.Action, player.ActionRemainingSeconds, player.Streak))));
            Assert.That(second.Lots.Select(lot => (lot.MaterialId, lot.QualityId, lot.Quantity, lot.PointMultiplierRaw)),
                Is.EqualTo(first.Lots.Select(lot => (lot.MaterialId, lot.QualityId, lot.Quantity, lot.PointMultiplierRaw))));
        }
    }
}
