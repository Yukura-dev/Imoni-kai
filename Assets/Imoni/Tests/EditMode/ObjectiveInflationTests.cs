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
    public sealed class ObjectiveInflationTests
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
        private static PlayerSnapshot[] Team(GameConfig config, WorldMap world, string upgrade = null, int level = 0) =>
            Enumerable.Range(0, config.TeamSize).Select(index => new PlayerSnapshot("p" + index, 0,
                config.Upgrades.ToDictionary(item => item.Id, item => upgrade == "all" || item.Id == upgrade ? level : 0), world.Start)).ToArray();
        private static Objective[] Goals(ObjectiveGenerationPlan plan) => plan.Objectives.Required.Concat(new[] { plan.Objectives.Bonus }).ToArray();

        [TestCase("wood")]
        [TestCase("taro")]
        [TestCase("negi")]
        [TestCase("mushroom")]
        [TestCase("fish")]
        [TestCase("sansai")]
        public void EveryMaterialFormulaGrowsThroughTheFiveNormalRounds(string material)
        {
            GameConfig config = Config(); var generator = new ObjectiveGenerator(config);
            BigInteger previous = 0;
            for (int round = 1; round <= config.RoundsToClear; round++)
            {
                BigInteger current = generator.TargetAmount(material, round);
                Assert.That(current, Is.GreaterThan(previous), material + " R" + round);
                previous = current;
            }
        }

        [TestCase(482)]
        [TestCase(777)]
        [TestCase(202647)]
        public void FinalGeneratedTargetsAlsoIncreaseForEachRepeatedMaterialAfterCapacityClamp(int seed)
        {
            GameConfig config = Config(); WorldMap world = WorldGen.Generate(config, seed);
            var generator = new ObjectiveGenerator(config);
            var previous = new Dictionary<string, BigInteger>();
            int comparisons = 0;
            for (int round = 1; round <= config.RoundsToClear; round++)
            {
                // Same source world, fresh stock and base position isolate progression from live play.
                ObjectiveGenerationPlan plan = generator.GeneratePlan(world, round, new Rng(unchecked(seed + round)));
                foreach (Objective goal in Goals(plan))
                {
                    if (previous.TryGetValue(goal.MaterialId, out BigInteger before))
                    { Assert.That(goal.Amount, Is.GreaterThan(before), goal.MaterialId + " final R" + round); comparisons++; }
                    previous[goal.MaterialId] = goal.Amount;
                    Assert.That(goal.Amount, Is.LessThanOrEqualTo(plan.SimulatedCapacities[goal.MaterialId]));
                }
            }
            Assert.That(comparisons, Is.GreaterThanOrEqualTo(config.ObjectiveGeneration.RequiredCount * (config.RoundsToClear - 1)));
        }

        [TestCase("move")]
        [TestCase("speed")]
        [TestCase("amount")]
        [TestCase("luck")]
        [TestCase("all")]
        public void RaisingUpgradesNeverReducesFinalTargetsAndReadsActualSupply(string upgrade)
        {
            GameConfig config = Config(); WorldMap world = WorldGen.Generate(config, 202647);
            var generator = new ObjectiveGenerator(config);
            int strictIncreases = 0;
            for (int round = 1; round <= config.RoundsToClear; round++)
            {
                var basic = generator.GeneratePlan(world, round, new Rng(97), Team(config, world));
                var raised = generator.GeneratePlan(world, round, new Rng(97), Team(config, world, upgrade, 3));
                foreach (Objective goal in Goals(raised))
                {
                    BigInteger before = Goals(basic).Single(item => item.MaterialId == goal.MaterialId).Amount;
                    Assert.That(goal.Amount, Is.GreaterThanOrEqualTo(before), upgrade + "/" + goal.MaterialId + " R" + round);
                    if (goal.Amount > before) strictIncreases++;
                    if (upgrade == "all") Assert.That(goal.Amount, Is.GreaterThan(before), "All production upgrades must raise every sampled target.");
                    Assert.That(raised.SimulatedCapacities[goal.MaterialId], Is.GreaterThanOrEqualTo(basic.SimulatedCapacities[goal.MaterialId]));
                    Assert.That(raised.UntrainedSimulatedCapacities[goal.MaterialId], Is.EqualTo(basic.SimulatedCapacities[goal.MaterialId]));
                }
            }
            Assert.That(strictIncreases, Is.GreaterThan(0), "A single travel/speed upgrade may hit a finite-stock plateau, but must help productive goals.");
        }

        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        [TestCase(5, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, true)]
        [TestCase(5, true)]
        public void EveryRoundHasAnExecutableSharedStockTravelAndGatherSchedule(int round, bool growing)
        {
            GameConfig config = Config(); WorldMap world = WorldGen.Generate(config, 777);
            if (growing) foreach (ResourceNode node in world.Resources)
            { while (node.TryGather()) { } node.Tick(config.ResourceRegrowSeconds / 2); }
            PlayerSnapshot[] team = Team(config, world, "all", 2);
            var plan = new ObjectiveGenerator(config).GeneratePlan(world, round, new Rng(37), team);
            foreach (var actorVisits in plan.Visits.GroupBy(visit => visit.ActorIndex))
            {
                decimal free = 0;
                foreach (ObjectiveGatherVisit visit in actorVisits.OrderBy(visit => visit.DepartSeconds))
                {
                    ResourceNode node = world.Resources.Single(item => item.Id == visit.ResourceId);
                    PathResult path = Pathfinding.FindPathToResource(world, world.Start, node, round);
                    decimal travel = path.DistanceTiles * GatheringTiming.MoveStepSeconds(config, team[visit.ActorIndex].Upgrades["move"]);
                    Assert.That(path.Reachable, Is.True);
                    Assert.That(visit.DepartSeconds, Is.GreaterThanOrEqualTo(free));
                    Assert.That(visit.GatherStartSeconds - visit.DepartSeconds, Is.EqualTo(travel));
                    Assert.That(visit.GatherSeconds, Is.EqualTo(GatheringTiming.CollectSeconds(config, team[visit.ActorIndex].Upgrades["speed"], node)));
                    Assert.That(visit.ReturnSeconds, Is.EqualTo(visit.GatherStartSeconds + visit.Count * visit.GatherSeconds + travel));
                    Assert.That(visit.ReturnSeconds, Is.LessThan(config.DurationSeconds));
                    if (node.MaterialId == plan.Objectives.Bonus.MaterialId)
                        Assert.That(visit.DepartSeconds, Is.GreaterThanOrEqualTo(config.BonusRevealSeconds));
                    free = visit.ReturnSeconds;
                }
            }
            var cloned = world.Resources.ToDictionary(node => node.Id, node => new ResourceNode(config, node.Id, node.MaterialId,
                node.BiomeId, node.Position, node.QualityModifier, node.EfficiencyModifier));
            if (growing) foreach (ResourceNode node in cloned.Values)
            { while (node.TryGather()) { } node.Tick(config.ResourceRegrowSeconds / 2); }
            var events = plan.Visits.SelectMany(visit => Enumerable.Range(1, visit.Count).Select(index =>
                new { visit.ResourceId, At = visit.GatherStartSeconds + index * visit.GatherSeconds })).OrderBy(item => item.At).ToArray();
            decimal now = 0;
            foreach (var gather in events)
            {
                foreach (ResourceNode node in cloned.Values) node.Tick(gather.At - now);
                Assert.That(cloned[gather.ResourceId].TryGather(), Is.True, "Shared resource was overscheduled: " + gather.ResourceId);
                now = gather.At;
            }
            Assert.That(events.Length, Is.EqualTo(plan.SimulatedGatherCounts.Values.Sum()));
            foreach (Objective goal in Goals(plan)) Assert.That(goal.Amount,
                Is.LessThanOrEqualTo(GameMath.MultiplyFloor(plan.SimulatedCapacities[goal.MaterialId], config.ObjectiveGeneration.MaximumCapacityFraction)));
        }

        [Test]
        public void CertainTopQualityUsesEveryFiniteStreakAndTheActualGatherFloor()
        {
            GameConfig config = Config(data => data["qualityRoll"]["topBaseChance"] = 1);
            var node = new ResourceNode(config, "test", "wood", "forest", default);
            var expectation = new GatherYieldExpectation(config, 5, 2, 0);
            var streak = new StreakCalc(config); BigInteger expected = 0;
            for (int count = 1; count <= 40; count++)
            {
                expected += streak.GatherQuantity(config.Material("wood").BaseYield, 2, count) * new QualityRoller(config).Highest(5).Multiplier;
                expectation.Add(node);
                Assert.That(expectation.TotalRaw, Is.EqualTo(expected * config.FixedScale));
            }
            Assert.That(expectation.GatherCount, Is.EqualTo(40));
        }

        [Test]
        public void TwoGatherExpectationMatchesEnumeratedQualitySequencesWithinFixedPrecision()
        {
            GameConfig config = Config(data =>
            {
                data["qualityRoll"]["topBaseChance"] = 0.5m;
                foreach (JObject biome in data["gathering"]["biomeModifiers"]) biome["qualityMultiplier"] = 1;
                ((JObject)data["materials"].First(material => material["id"].Value<string>() == "wood"))["baseYield"] = 100;
            });
            var node = new ResourceNode(config, "test", "wood", "forest", default);
            var distribution = new QualityRoller(config).Distribution(1, 0, node.BiomeId, node.MaterialId);
            int top = distribution.Qualities.Count - 1;
            var streak = new StreakCalc(config); BigInteger numerator = 0;
            for (int first = 0; first <= top; first++) for (int second = 0; second <= top; second++)
            {
                int firstStreak = first == top ? 1 : 0;
                int nextStreak = second == top ? firstStreak + 1 : 0;
                BigInteger yield = streak.GatherQuantity(100, 0, firstStreak) * distribution.Qualities[first].Multiplier
                    + streak.GatherQuantity(100, 0, nextStreak) * distribution.Qualities[second].Multiplier;
                numerator += yield * distribution.Weights[first] * distribution.Weights[second];
            }
            BigInteger exactRaw = numerator * config.FixedScale / BigInteger.Pow(distribution.TotalWeight, 2);
            var expectation = new GatherYieldExpectation(config, 1, 0, 0); expectation.Add(node); expectation.Add(node);
            Assert.That(expectation.TotalRaw, Is.LessThanOrEqualTo(exactRaw));
            Assert.That(exactRaw - expectation.TotalRaw, Is.LessThan(streak.GatherQuantity(100, 0, 2) * distribution.Qualities[top].Multiplier));
        }

        [Test]
        public void InflationTuningAndHugeBaselinesUseConfigWithoutFloatOverflow()
        {
            BigInteger huge = BigInteger.Pow(10, 100);
            GameConfig config = Config(data =>
            {
                data["objectiveGeneration"]["baseAmounts"]["wood"] = huge.ToString();
                data["objectiveGeneration"]["roundGrowth"] = 2;
                data["objectiveGeneration"]["highestQualityWeight"] = 0;
            });
            Assert.That(new ObjectiveGenerator(config).TargetAmount("wood", 5), Is.EqualTo(huge * 16));
            Assert.Throws<ArgumentException>(() => Config(data => data["objectiveGeneration"]["roundGrowth"] = 1));
            Assert.Throws<ArgumentException>(() => Config(data => data["objectiveGeneration"]["maximumCapacityFraction"] = 1));
            Assert.Throws<ArgumentException>(() => Config(data => ((JObject)data["objectiveGeneration"]["baseAmounts"]).Remove("wood")));
        }
    }
}
