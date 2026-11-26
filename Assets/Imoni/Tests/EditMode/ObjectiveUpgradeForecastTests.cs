using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Imoni.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class ObjectiveUpgradeForecastTests
    {
        private static GameConfig Config(Action<JObject> change = null)
        {
#if UNITY_EDITOR
            string path = Path.Combine(UnityEngine.Application.dataPath, "Imoni/Config/GameConfig.json");
#else
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            JObject data = JObject.Parse(File.ReadAllText(path)); change?.Invoke(data);
            return GameConfig.FromJson(data.ToString());
        }
        private static PlayerSnapshot[] Team(GameConfig config, WorldMap world, int level = 0) =>
            Enumerable.Range(1, config.TeamSize).Select(index => new PlayerSnapshot("player" + index, 0,
                config.Upgrades.ToDictionary(item => item.Id, _ => index == 1 ? level : 0), world.Start, isBot: index > 1)).ToArray();
        private static Objective[] Goals(ObjectiveGenerationPlan plan) => plan.Objectives.Required.Concat(new[] { plan.Objectives.Bonus }).ToArray();
        private static string[] GoalValues(IEnumerable<Objective> goals) => goals.Select(goal => goal.MaterialId + ":" + goal.Amount).ToArray();

        [Test]
        public void ForecastPaysCheapestActualPricesWithinOneSharedBudgetAndLeavesInputsUntouched()
        {
            GameConfig config = Config(data => data["upgrades"]["items"][0]["basePrice"] = 17);
            WorldMap world = WorldGen.Generate(config, 202647); PlayerSnapshot[] team = Team(config, world);
            var result = ObjectiveUpgradeForecast.Create(config, team, 1000);
            var levels = team.ToDictionary(player => player.Id, player => player.Upgrades.ToDictionary(item => item.Key, item => item.Value));
            var rules = new UpgradeRules(config); BigInteger total = 0;
            foreach (AnticipatedUpgradePurchase purchase in result.Purchases)
            {
                Assert.That(purchase.ActorId, Is.EqualTo("player1"));
                BigInteger cheapest = config.Upgrades.Min(upgrade => rules.Price(upgrade.Id, levels[purchase.ActorId][upgrade.Id]));
                Assert.That(purchase.Price, Is.EqualTo(cheapest));
                Assert.That(purchase.Price, Is.EqualTo(rules.Price(purchase.UpgradeId, levels[purchase.ActorId][purchase.UpgradeId])));
                Assert.That(purchase.Level, Is.EqualTo(++levels[purchase.ActorId][purchase.UpgradeId]));
                total += purchase.Price; Assert.That(total, Is.LessThanOrEqualTo(result.Budget));
            }
            Assert.That(result.Budget, Is.EqualTo(GameMath.MultiplyFloor(1000, config.ObjectiveGeneration.AnticipatedUpgradeBudgetFraction)));
            Assert.That(result.Spent, Is.EqualTo(total));
            Assert.That(config.Upgrades.All(upgrade => rules.Price(upgrade.Id, levels["player1"][upgrade.Id]) > result.Budget - total), Is.True);
            Assert.That(team.All(player => player.Upgrades.Values.All(level => level == 0)), Is.True);
            Assert.That(result.Players.Where(player => player.IsBot).All(player => player.Upgrades.Values.All(level => level == 0)), Is.True);
            Assert.That(result.Purchases[0].UpgradeId, Is.EqualTo("move"), "Price takes precedence over the configured tie priority.");
        }

        [Test]
        public void ForecastSharesBudgetAcrossHumansWithStableTiesAndAcceptsHugeIntegerFunds()
        {
            GameConfig config = Config(); WorldMap world = WorldGen.Generate(config, 482);
            var humans = Team(config, world).Select(player => new PlayerSnapshot(player.Id, 0,
                player.Upgrades.ToDictionary(item => item.Key, item => item.Value), world.Start)).ToArray();
            BigInteger funds = BigInteger.Pow(10, 50) + 123;
            var first = ObjectiveUpgradeForecast.Create(config, humans, funds);
            var second = ObjectiveUpgradeForecast.Create(config, humans, funds);
            Assert.That(first.Spent, Is.LessThanOrEqualTo(GameMath.MultiplyFloor(funds, config.ObjectiveGeneration.AnticipatedUpgradeBudgetFraction)));
            Assert.That(first.Purchases.Select(item => item.ActorId + "/" + item.UpgradeId + "/" + item.Level),
                Is.EqualTo(second.Purchases.Select(item => item.ActorId + "/" + item.UpgradeId + "/" + item.Level)));
            Assert.That(first.Purchases.Take(config.TeamSize).Select(item => item.ActorId), Is.EqualTo(humans.Select(player => player.Id)));
            Assert.That(first.Players.All(player => player.Upgrades.Values.Min() > 0), Is.True);
        }

        [Test]
        public void ForecastIsConfigDrivenAndRejectsInvalidBudgetsOrPriorities()
        {
            GameConfig config = Config(data => data["objectiveGeneration"]["anticipatedUpgradeBudgetFraction"] = 0);
            WorldMap world = WorldGen.Generate(config, 482);
            Assert.That(ObjectiveUpgradeForecast.Create(config, Team(config, world), 1000000).Purchases, Is.Empty);
            Assert.Throws<ArgumentOutOfRangeException>(() => ObjectiveUpgradeForecast.Create(config, Team(config, world), -1));
            Assert.Throws<ArgumentException>(() => Config(data => data["objectiveGeneration"]["anticipatedUpgradeBudgetFraction"] = 1.01m));
            Assert.Throws<ArgumentException>(() => Config(data => data["objectiveGeneration"]["anticipatedUpgradeBudgetFraction"] = -0.1m));
            Assert.Throws<ArgumentException>(() => Config(data => data["objectiveGeneration"]["anticipatedUpgradePriority"] = new JArray("move", "move", "speed", "amount")));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void ForecastLevelsDriveBothTargetsAndExecutableTravelGatherSupply(int round)
        {
            GameConfig config = Config(); WorldMap world = WorldGen.Generate(config, 202647);
            PlayerSnapshot[] team = Team(config, world, 8); var generator = new ObjectiveGenerator(config);
            var plan = generator.GeneratePlan(world, round, new Rng(97), team, 100000000);
            var explicitPlan = generator.GeneratePlan(world, round, new Rng(97), plan.UpgradeForecast.Players);
            Assert.That(GoalValues(Goals(plan)), Is.EqualTo(GoalValues(Goals(explicitPlan))));
            Assert.That(plan.SimulatedCapacities, Is.EqualTo(explicitPlan.SimulatedCapacities));
            var expectedActors = plan.UpgradeForecast.Players;
            foreach (var actorVisits in plan.Visits.GroupBy(visit => visit.ActorIndex))
            {
                decimal free = 0;
                foreach (ObjectiveGatherVisit visit in actorVisits.OrderBy(visit => visit.DepartSeconds))
                {
                    ResourceNode node = world.Resources.Single(item => item.Id == visit.ResourceId);
                    PathResult path = Pathfinding.FindPathToResource(world, world.Start, node, round);
                    decimal travel = path.DistanceTiles * GatheringTiming.MoveStepSeconds(config, expectedActors[visit.ActorIndex].Upgrades["move"]);
                    Assert.That(visit.DepartSeconds, Is.GreaterThanOrEqualTo(free));
                    Assert.That(visit.GatherStartSeconds - visit.DepartSeconds, Is.EqualTo(travel));
                    Assert.That(visit.GatherSeconds, Is.EqualTo(GatheringTiming.CollectSeconds(config, expectedActors[visit.ActorIndex].Upgrades["speed"], node)));
                    Assert.That(visit.ReturnSeconds, Is.EqualTo(visit.GatherStartSeconds + visit.Count * visit.GatherSeconds + travel));
                    Assert.That(visit.ReturnSeconds, Is.LessThan(config.DurationSeconds));
                    free = visit.ReturnSeconds;
                }
            }
            var cloned = world.Resources.ToDictionary(node => node.Id, node => new ResourceNode(config, node.Id, node.MaterialId,
                node.BiomeId, node.Position, node.QualityModifier, node.EfficiencyModifier));
            var events = plan.Visits.SelectMany(visit => Enumerable.Range(1, visit.Count).Select(index =>
                new { visit.ResourceId, At = visit.GatherStartSeconds + index * visit.GatherSeconds })).OrderBy(item => item.At);
            decimal now = 0;
            foreach (var gather in events)
            {
                foreach (ResourceNode node in cloned.Values) node.Tick(gather.At - now);
                Assert.That(cloned[gather.ResourceId].TryGather(), Is.True, "Shared resource was overscheduled."); now = gather.At;
            }
            foreach (Objective goal in Goals(plan)) Assert.That(goal.Amount, Is.LessThanOrEqualTo(
                GameMath.MultiplyFloor(plan.SimulatedCapacities[goal.MaterialId], config.ObjectiveGeneration.MaximumCapacityFraction)));
            Assert.That(team[0].Upgrades.Values.All(level => level == 8), Is.True);
        }

        [TestCase(482)]
        [TestCase(777)]
        [TestCase(202647)]
        public void ForecastPreservesFinalRoundGrowthAndMoreFundsNeverLowerFixedStateTargets(int seed)
        {
            GameConfig config = Config(); WorldMap world = WorldGen.Generate(config, seed);
            PlayerSnapshot[] team = Team(config, world); var generator = new ObjectiveGenerator(config);
            var previousRound = new Dictionary<string, BigInteger>();
            for (int round = 1; round <= config.RoundsToClear; round++)
            {
                var plan = generator.GeneratePlan(world, round, new Rng(seed + round), team, config.StartFunds);
                foreach (Objective goal in Goals(plan))
                {
                    if (previousRound.TryGetValue(goal.MaterialId, out BigInteger before)) Assert.That(goal.Amount, Is.GreaterThan(before));
                    previousRound[goal.MaterialId] = goal.Amount;
                }
                var moreFunds = generator.GeneratePlan(world, round, new Rng(seed + round), team, 100000000);
                foreach (Objective goal in Goals(moreFunds)) Assert.That(goal.Amount,
                    Is.GreaterThanOrEqualTo(Goals(plan).Single(item => item.MaterialId == goal.MaterialId).Amount));
            }
        }

        [Test]
        public void ServerUsesOpeningFundsWithoutBuyingOrChangingObjectivesAfterActualUpgrade()
        {
            GameConfig config = Config(); var server = new LocalGameServer(config, 202647); GameSnapshot before = server.Snapshot();
            var expected = new ObjectiveGenerator(config).GeneratePlan(server.World, 1, new Rng(202648), before.Players, before.Funds);
            Assert.That(GoalValues(before.RequiredObjectives), Is.EqualTo(GoalValues(expected.Objectives.Required)));
            Assert.That(before.Funds, Is.EqualTo(config.StartFunds)); Assert.That(before.History, Is.Empty);
            Assert.That(before.Players.All(player => player.Upgrades.Values.All(level => level == 0)), Is.True);
            server.Upgrade("player1", "amount"); GameSnapshot after = server.Snapshot();
            Assert.That(GoalValues(after.RequiredObjectives), Is.EqualTo(GoalValues(before.RequiredObjectives)));
            Assert.That(after.History.Count, Is.EqualTo(1));
            Assert.That(after.Funds, Is.EqualTo(before.Funds - new UpgradeRules(config).Price("amount", 0)));
        }

        [Test]
        public void NextRoundForecastUsesSettledFundsAndCurrentLevelsWithoutSpendingOrResettingThem()
        {
            GameConfig config = Config(); var server = new LocalGameServer(config, 202647, objectives:
                new RoundObjectives(new[] { new Objective("wood", 1) }, new Objective("mushroom", 1)), enableBots: false);
            server.Upgrade("player1", "amount"); server.Buy("player1", "wood", 1); server.Buy("player1", "mushroom", 1);
            server.Tick(config.DurationSeconds); GameSnapshot settled = server.Snapshot();
            Assert.That(settled.Phase, Is.EqualTo(RoundPhase.Succeeded));
            var expected = new ObjectiveGenerator(config).GeneratePlan(server.World, 2, new Rng(202649), settled.Players, settled.Funds);
            server.StartNextRound(); GameSnapshot next = server.Snapshot();
            Assert.That(GoalValues(next.RequiredObjectives), Is.EqualTo(GoalValues(expected.Objectives.Required)));
            Assert.That(next.Funds, Is.EqualTo(settled.Funds));
            Assert.That(next.History.Count, Is.EqualTo(settled.History.Count));
            Assert.That(next.Players[0].Upgrades["amount"], Is.EqualTo(settled.Players[0].Upgrades["amount"]));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RetryUsesFreshFundsLevelsAndActualHumanBotRoles(bool bots)
        {
            GameConfig config = Config(); var server = new LocalGameServer(config, 482, objectives:
                new RoundObjectives(new[] { new Objective("wood", BigInteger.Pow(10, 20)) }, new Objective("mushroom", 1)), enableBots: bots);
            server.Upgrade("player1", "luck"); server.Tick(config.DurationSeconds); server.Retry(777);
            var fresh = new LocalGameServer(config, 777, enableBots: bots).Snapshot(); GameSnapshot retry = server.Snapshot();
            Assert.That(GoalValues(retry.RequiredObjectives), Is.EqualTo(GoalValues(fresh.RequiredObjectives)));
            Assert.That(retry.Funds, Is.EqualTo(config.StartFunds));
            Assert.That(retry.Players.Select(player => player.IsBot), Is.EqualTo(fresh.Players.Select(player => player.IsBot)));
            Assert.That(retry.Players.All(player => player.Upgrades.Values.All(level => level == 0)), Is.True);
        }
    }
}
