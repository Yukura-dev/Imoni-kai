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
    public sealed class WorldObjectiveTests
    {
        [TestCase(482)]
        [TestCase(777)]
        [TestCase(202647)]
        public void FirstRoundRequiredGoalsUseTheConfiguredBeginnerMaterials(int seed)
        {
            GameConfig config = Config();
            WorldMap world = WorldGen.Generate(config, seed);
            RoundObjectives goals = new ObjectiveGenerator(config).Generate(world, 1, new Rng(seed + 1));
            Assert.That(goals.Required.Select(goal => goal.MaterialId),
                Is.EquivalentTo(config.ObjectiveGeneration.FirstRoundRequiredMaterials));
            Assert.That(goals.Required.Select(goal => goal.MaterialId), Has.No.Member(goals.Bonus.MaterialId));
        }

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

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void GoalsUseOnlyReachableUnlockedMaterialsWithoutDuplicates(int round)
        {
            GameConfig config = Config();
            WorldMap world = WorldGen.Generate(config, 1249);
            var generator = new ObjectiveGenerator(config);
            ObjectiveGenerationPlan plan = generator.GeneratePlan(world, round, new Rng(round));
            Objective[] goals = All(plan.Objectives);
            Assert.That(goals.Length, Is.EqualTo(config.ObjectiveGeneration.RequiredCount + 1));
            Assert.That(goals.Select(goal => goal.MaterialId).Distinct().Count(), Is.EqualTo(goals.Length));
            var available = world.ResourcesAvailable(round).Select(node => node.MaterialId).ToHashSet();
            foreach (Objective goal in goals)
            {
                Assert.That(available.Contains(goal.MaterialId), Is.True);
                BigInteger cap = plan.SimulatedCapacities[goal.MaterialId];
                Assert.That(goal.Amount * 10 >= cap * 4, Is.True, "At least forty percent of attainable capacity.");
                Assert.That(goal.Amount * 10 <= cap * 7, Is.True, "At most seventy percent of attainable capacity.");
            }
        }

        [Test]
        public void SameWorldSeedAndGoalSeedProduceIdenticalGoalsAndCapacities()
        {
            GameConfig config = Config();
            var generator = new ObjectiveGenerator(config);
            var a = generator.GeneratePlan(WorldGen.Generate(config, -27), 3, new Rng(918));
            var b = generator.GeneratePlan(WorldGen.Generate(config, -27), 3, new Rng(918));
            Assert.That(All(a.Objectives).Select(Describe), Is.EqualTo(All(b.Objectives).Select(Describe)));
            Assert.That(a.SimulatedCapacities, Is.EquivalentTo(b.SimulatedCapacities));
        }

        [Test]
        public void SimulatedGoalsShareOneTeamRoundBudget()
        {
            GameConfig config = Config(json =>
            {
                foreach (JObject biome in json["gathering"]["biomeModifiers"]) biome["efficiencyMultiplier"] = 1;
            });
            WorldMap world = WorldGen.Generate(config, 53);
            var plan = new ObjectiveGenerator(config).GeneratePlan(world, 1, new Rng(34));
            decimal gatherWork = plan.SimulatedCapacities.Sum(item =>
                (decimal)(item.Value / config.Material(item.Key).BaseYield) * config.BaseCollectSeconds);
            Assert.That(gatherWork, Is.LessThan(config.TeamSize * config.DurationSeconds));
        }

        [Test]
        public void GenerationIncludesRegrowingNodesWithoutMutatingTheirStockOrGrowth()
        {
            GameConfig config = Config();
            WorldMap world = WorldGen.Generate(config, 71);
            foreach (ResourceNode node in world.Resources)
            {
                while (node.TryGather()) { }
                node.Tick(config.ResourceRegrowSeconds / 2);
            }
            var plan = new ObjectiveGenerator(config).GeneratePlan(world, 1, new Rng(81));
            Assert.That(plan.Objectives.Required.Count, Is.EqualTo(config.ObjectiveGeneration.RequiredCount));
            Assert.That(world.Resources.All(node => node.RemainingStock == 0 &&
                node.RegrowthSeconds == config.ResourceRegrowSeconds / 2), Is.True);
        }

        [Test]
        public void MoreWorkersAndAmountUpgradesIncreaseSimulatedSupply()
        {
            GameConfig single = Config(json => json["team"]["size"] = 1);
            GameConfig team = Config();
            WorldMap world = WorldGen.Generate(team, 44);
            var one = new ObjectiveGenerator(single).GeneratePlan(world, 1, new Rng(25));
            var three = new ObjectiveGenerator(team).GeneratePlan(world, 1, new Rng(25));
            Assert.That(three.SimulatedCapacities.Values.Aggregate(BigInteger.Zero, (a, b) => a + b),
                Is.GreaterThan(one.SimulatedCapacities.Values.Aggregate(BigInteger.Zero, (a, b) => a + b)));
            PlayerSnapshot[] upgraded = Enumerable.Range(0, team.TeamSize).Select(index => new PlayerSnapshot("p" + index, 0,
                team.Upgrades.ToDictionary(upgrade => upgrade.Id, upgrade => upgrade.Id == "amount" ? 4 : 0), world.Start)).ToArray();
            var better = new ObjectiveGenerator(team).GeneratePlan(world, 1, new Rng(25), upgraded);
            foreach (string material in three.SimulatedCapacities.Keys)
                Assert.That(better.SimulatedCapacities[material], Is.EqualTo(three.SimulatedCapacities[material] * 3));
        }

        [Test]
        public void FractionsComeFromConfigAndInvalidGenerationSettingsAreRejected()
        {
            GameConfig config = Config(json =>
            {
                json["objectiveGeneration"]["minimumFraction"] = 0.5m;
                json["objectiveGeneration"]["maximumFraction"] = 0.6m;
            });
            var plan = new ObjectiveGenerator(config).GeneratePlan(WorldGen.Generate(config, 22), 1, new Rng(26));
            foreach (Objective goal in All(plan.Objectives))
            {
                BigInteger cap = plan.SimulatedCapacities[goal.MaterialId];
                Assert.That(goal.Amount * 10 >= cap * 5 && goal.Amount * 10 <= cap * 6, Is.True);
            }
            Assert.Throws<ArgumentException>(() => Config(json => json["objectiveGeneration"]["minimumFraction"] = 0.8m));
            Assert.Throws<ArgumentException>(() => Config(json => json["objectiveGeneration"]["requiredCount"] = 4));
            Assert.Throws<ArgumentException>(() => Config(json => json["objectiveGeneration"]["requiredCount"] = 2.5m));
            Assert.Throws<ArgumentException>(() => Config(json => json["gathering"]["growthStageFractions"][0] = "0.25"));
        }

        [Test]
        public void CurrentStockLimitsSupplyAndImpossibleCooldownsRejectGoals()
        {
            GameConfig config = Config(json =>
            {
                json["round"]["durationSec"] = 60;
                json["round"]["bonusRevealSec"] = 10;
                json["gathering"]["regrowSec"] = 120;
                json["world"]["resourceChance"] = 0.0001m;
            });
            WorldMap world = WorldGen.Generate(config, 144);
            var generator = new ObjectiveGenerator(config);
            var full = generator.GeneratePlan(world, 1, new Rng(12));
            foreach (ResourceNode node in world.Resources)
                while (node.RemainingStock > 1) node.TryGather();
            var partial = generator.GeneratePlan(world, 1, new Rng(12));
            Assert.That(partial.SimulatedCapacities.Values.Aggregate(BigInteger.Zero, (a, b) => a + b),
                Is.LessThan(full.SimulatedCapacities.Values.Aggregate(BigInteger.Zero, (a, b) => a + b)));
            foreach (ResourceNode node in world.Resources) node.TryGather();
            Assert.Throws<InvalidOperationException>(() => generator.GeneratePlan(world, 1, new Rng(12)));
        }

        private static Objective[] All(RoundObjectives goals) => goals.Required.Concat(new[] { goals.Bonus }).ToArray();
        private static string Describe(Objective goal) => goal.MaterialId + ":" + goal.Amount;
    }
}
