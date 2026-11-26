using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace Imoni.Core
{
    /// <summary>
    /// Inflates configured material baselines while checking a constructive shared-time schedule.
    /// Quality-equivalent supply includes the actual quality weights and finite streak expectations.
    /// This guarantees a feasible expected-yield plan, not success for every random quality sequence.
    /// </summary>
    public sealed class ObjectiveGenerator
    {
        private readonly GameConfig config;
        private readonly StreakCalc quantities;

        public ObjectiveGenerator(GameConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            quantities = new StreakCalc(config);
        }

        public RoundObjectives Generate(WorldMap world, int round, Rng rng,
            IEnumerable<PlayerSnapshot> players = null) => GeneratePlan(world, round, rng, players).Objectives;

        public ObjectiveGenerationPlan GeneratePlan(WorldMap world, int round, Rng rng,
            IEnumerable<PlayerSnapshot> players = null)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            ResourceNode[] resources = world.ResourcesAvailable(round).OrderBy(node => node.Id, StringComparer.Ordinal).ToArray();
            var candidates = resources.Select(node => node.MaterialId).Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal).ToList();
            // Fisher-Yates, with a stable initial order, consumes only the caller's seeded RNG.
            for (int index = candidates.Count - 1; index > 0; index--)
            {
                int other = (int)rng.NextBelow(index + 1);
                string temporary = candidates[index]; candidates[index] = candidates[other]; candidates[other] = temporary;
            }
            PlayerSnapshot[] team = players?.ToArray();
            Worker[] workers = CreateWorkers(world, round, team);
            // Same starting positions and resources isolate the production increase from upgrades.
            PlayerSnapshot[] untrained = team?.Select(player => new PlayerSnapshot(player.Id, 0,
                config.Upgrades.ToDictionary(upgrade => upgrade.Id, _ => 0), player.Position)).ToArray();
            Worker[] baselineWorkers = CreateWorkers(world, round, untrained);
            int count = config.ObjectiveGeneration.RequiredCount + 1;
            while (candidates.Count >= count)
            {
                string[] chosen;
                if (round == 1)
                {
                    string[] required = candidates.Where(id => config.ObjectiveGeneration.FirstRoundRequiredMaterials.Contains(id))
                        .Take(count - 1).ToArray();
                    string bonus = candidates.FirstOrDefault(id => !required.Contains(id));
                    if (required.Length != count - 1 || bonus == null) break;
                    chosen = required.Concat(new[] { bonus }).ToArray();
                }
                else chosen = candidates.Take(count).ToArray();
                var capacities = new Dictionary<string, BigInteger>(StringComparer.Ordinal);
                var rawCapacities = new Dictionary<string, BigInteger>(StringComparer.Ordinal);
                var baselineCapacities = new Dictionary<string, BigInteger>(StringComparer.Ordinal);
                var gatherCounts = new Dictionary<string, int>(StringComparer.Ordinal);
                var visits = new List<ObjectiveGatherVisit>();
                string unsuitable = null;
                for (int index = 0; index < chosen.Length; index++)
                {
                    decimal start = config.DurationSeconds * index / count;
                    decimal end = config.DurationSeconds * (index + 1) / count;
                    // The bonus is not actionable until it is publicly revealed.
                    if (index == chosen.Length - 1) start = Math.Max(start, config.BonusRevealSeconds);
                    Supply supply = SimulateMaterial(world, round, resources.Where(node => node.MaterialId == chosen[index]),
                        workers, start, end);
                    capacities.Add(chosen[index], supply.Expected);
                    Supply baselineSupply = team == null || team.All(player => player.Upgrades.Values.All(level => level == 0))
                        ? supply : SimulateMaterial(world, round, resources.Where(node => node.MaterialId == chosen[index]), baselineWorkers, start, end);
                    baselineCapacities.Add(chosen[index], baselineSupply.Expected);
                    rawCapacities.Add(chosen[index], supply.Raw);
                    gatherCounts.Add(chosen[index], supply.Gathers);
                    visits.AddRange(supply.Visits);
                    if (MaximumAmount(supply.Expected) < 1) { unsuitable = chosen[index]; break; }
                }
                if (unsuitable != null)
                {
                    // Every failed attempt removes a material, so malformed/tiny worlds cannot loop forever.
                    candidates.Remove(unsuitable);
                    continue;
                }
                var goals = new List<Objective>();
                var baselines = new Dictionary<string, BigInteger>(StringComparer.Ordinal);
                var requested = new Dictionary<string, BigInteger>(StringComparer.Ordinal);
                foreach (string materialId in chosen)
                {
                    BigInteger basis = config.ObjectiveGeneration.BaseAmounts[materialId];
                    BigInteger low = MinimumAmount(basis);
                    BigInteger high = GameMath.MultiplyFloor(basis, config.ObjectiveGeneration.MaximumFraction);
                    BigInteger baseline = low + rng.NextBelow(high - low + 1);
                    BigInteger target = TargetAmount(materialId, round, team, baseline, capacities[materialId], baselineCapacities[materialId]);
                    baselines.Add(materialId, baseline); requested.Add(materialId, target);
                    goals.Add(new Objective(materialId, BigInteger.Min(target, MaximumAmount(capacities[materialId]))));
                }
                return new ObjectiveGenerationPlan(new RoundObjectives(goals.Take(goals.Count - 1), goals[goals.Count - 1]),
                    capacities, baselineCapacities, rawCapacities, gatherCounts, baselines, requested, visits);
            }
            throw new InvalidOperationException("The unlocked world cannot supply the configured number of distinct required and bonus objectives within the round budget.");
        }

        /// <summary>Uncapped, quality-equivalent target. Sampling supplies the baseline; the caller
        /// then clamps to the checked expected supply, retaining both values for balance inspection.</summary>
        public BigInteger TargetAmount(string materialId, int round, IEnumerable<PlayerSnapshot> players = null, BigInteger? baseline = null,
            BigInteger? expectedSupply = null, BigInteger? untrainedSupply = null)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            config.Material(materialId);
            BigInteger amount = baseline ?? config.ObjectiveGeneration.BaseAmounts[materialId];
            if (amount < 1) throw new ArgumentOutOfRangeException(nameof(baseline));
            PlayerSnapshot[] team = players?.ToArray();
            if (team != null && team.Length != config.TeamSize) throw new ArgumentException("Supply every team member.", nameof(players));
            var quality = new QualityRoller(config);
            BigInteger firstTop = quality.Highest(1).Multiplier, top = quality.Highest(round).Multiplier;
            GameMath.DecimalRatio(config.ObjectiveGeneration.HighestQualityWeight, out BigInteger qn, out BigInteger qd);
            BigInteger qualityNumerator = firstTop * qd + (top - firstTop) * qn;
            BigInteger teamRaw = config.FixedScale;
            foreach (var weight in config.ObjectiveGeneration.UpgradeWeights)
            {
                BigInteger levels = team == null ? BigInteger.Zero : team.Aggregate(BigInteger.Zero,
                    (sum, player) => sum + player.Upgrades[weight.Key]);
                teamRaw += GameMath.MultiplyFloor(levels * config.FixedScale, weight.Value) / config.TeamSize;
            }
            GameMath.DecimalRatio(config.ObjectiveGeneration.RoundGrowth, out BigInteger gn, out BigInteger gd);
            GameMath.DecimalRatio(config.ObjectiveGeneration.UpgradeSupplyWeight, out BigInteger sn, out BigInteger sd);
            BigInteger untrained = BigInteger.Max(BigInteger.One, untrainedSupply ?? BigInteger.One);
            BigInteger supplyNumerator = untrained * sd + BigInteger.Max(0, (expectedSupply ?? untrained) - untrained) * sn;
            return BigInteger.Max(BigInteger.One, amount * BigInteger.Pow(gn, round - 1) * qualityNumerator * teamRaw * supplyNumerator
                / (BigInteger.Pow(gd, round - 1) * firstTop * qd * config.FixedScale * untrained * sd));
        }

        private Worker[] CreateWorkers(WorldMap world, int round, IEnumerable<PlayerSnapshot> snapshots)
        {
            PlayerSnapshot[] players = snapshots?.ToArray();
            if (players != null && players.Length != config.TeamSize)
                throw new ArgumentException("Objective simulation needs every configured team member.", nameof(snapshots));
            var workers = new Worker[config.TeamSize];
            for (int index = 0; index < workers.Length; index++)
            {
                PlayerSnapshot player = players == null ? null : players[index];
                int move = player == null ? 0 : player.Upgrades["move"];
                int speed = player == null ? 0 : player.Upgrades["speed"];
                int amount = player == null ? 0 : player.Upgrades["amount"];
                int luck = player == null ? 0 : player.Upgrades["luck"];
                decimal tileSeconds = GatheringTiming.MoveStepSeconds(config, move);
                PathResult initialPath = Pathfinding.FindPath(world, player == null ? world.Start : player.Position, world.Start, round);
                if (!initialPath.Reachable) throw new InvalidOperationException("A team member cannot return to the base.");
                workers[index] = new Worker(tileSeconds, speed, amount, luck, (initialPath.Steps.Count - 1) * tileSeconds);
            }
            return workers;
        }

        private Supply SimulateMaterial(WorldMap world, int round, IEnumerable<ResourceNode> resources,
            Worker[] workers, decimal stageStart, decimal stageEnd)
        {
            var nodes = resources.Select(node => new SimulatedNode(node,
                Pathfinding.FindPathToResource(world, world.Start, node, round).Steps.Count - 1)).ToArray();
            decimal[] freeAt = workers.Select(worker => Math.Max(stageStart, worker.InitialReturnSeconds)).ToArray();
            BigInteger quantity = BigInteger.Zero;
            GatherYieldExpectation[] expectations = workers.Select(worker => new GatherYieldExpectation(config, round,
                worker.AmountLevel, worker.LuckLevel)).ToArray();
            var visits = new List<ObjectiveGatherVisit>();
            decimal now = stageStart;
            while (true)
            {
                int nextWorker = -1;
                SimulatedNode nextNode = null;
                decimal nextCompletion = decimal.MaxValue;
                decimal nextReturn = 0;
                int nextGathers = 0;
                decimal nextStart = 0, nextTravel = 0, nextGatherSeconds = 0;
                for (int actor = 0; actor < workers.Length; actor++)
                {
                    Worker worker = workers[actor];
                    foreach (SimulatedNode node in nodes)
                    {
                        decimal travel = worker.TileSeconds * node.PathLength;
                        decimal gatherStart = Math.Max(freeAt[actor] + travel, node.Stock > 0 ? node.AvailableFrom : node.ReadyAt);
                        decimal gatherSeconds = GatheringTiming.CollectSeconds(config, worker.SpeedLevel, node.Source);
                        int available = node.Stock > 0 ? node.Stock : config.ResourceStock;
                        int count = Math.Min(available, config.ObjectiveGeneration.SimulationGathersPerVisit);
                        // A visit can end early near the stage boundary. Each actual gather still
                        // consumes its full configured duration, and the return journey is paid once.
                        while (count > 0 && gatherStart + count * gatherSeconds + travel >= stageEnd) count--;
                        if (count == 0) continue;
                        decimal complete = gatherStart + count * gatherSeconds;
                        decimal backAt = complete + travel;
                        if (backAt >= stageEnd || complete >= nextCompletion) continue;
                        nextCompletion = complete; nextWorker = actor; nextNode = node; nextReturn = backAt;
                        nextGathers = count;
                        nextStart = gatherStart; nextTravel = travel; nextGatherSeconds = gatherSeconds;
                    }
                }
                if (nextWorker < 0) return new Supply(quantity,
                    expectations.Aggregate(BigInteger.Zero, (sum, expected) => sum + expected.TotalRaw) / config.FixedScale,
                    expectations.Sum(expected => expected.GatherCount), visits);
                now = nextCompletion;
                foreach (SimulatedNode node in nodes)
                {
                    if (node.Stock == 0 && node.ReadyAt <= now)
                    { node.Stock = config.ResourceStock; node.AvailableFrom = node.ReadyAt; }
                }
                nextNode.Stock -= nextGathers;
                nextNode.AvailableFrom = now;
                if (nextNode.Stock == 0) nextNode.ReadyAt = now + config.ResourceRegrowSeconds;
                freeAt[nextWorker] = nextReturn;
                visits.Add(new ObjectiveGatherVisit(nextWorker, nextNode.Source.Id, nextStart - nextTravel,
                    nextStart, nextGatherSeconds, nextGathers, nextReturn));
                for (int gather = 0; gather < nextGathers; gather++)
                {
                    quantity += quantities.GatherQuantity(config.Material(nextNode.Source.MaterialId).BaseYield,
                        workers[nextWorker].AmountLevel, 0);
                    expectations[nextWorker].Add(nextNode.Source);
                }
            }
        }

        private BigInteger MinimumAmount(BigInteger capacity)
        {
            GameMath.DecimalRatio(config.ObjectiveGeneration.MinimumFraction, out BigInteger numerator, out BigInteger denominator);
            return BigInteger.Max(BigInteger.One, (capacity * numerator + denominator - 1) / denominator);
        }
        private BigInteger MaximumAmount(BigInteger capacity) => GameMath.MultiplyFloor(capacity, config.ObjectiveGeneration.MaximumCapacityFraction);

        private sealed class Supply
        {
            public BigInteger Raw { get; }
            public BigInteger Expected { get; }
            public int Gathers { get; }
            public IReadOnlyList<ObjectiveGatherVisit> Visits { get; }
            public Supply(BigInteger raw, BigInteger expected, int gathers, IList<ObjectiveGatherVisit> visits)
            { Raw = raw; Expected = expected; Gathers = gathers; Visits = new ReadOnlyCollection<ObjectiveGatherVisit>(visits); }
        }

        private sealed class Worker
        {
            public decimal TileSeconds { get; }
            public int SpeedLevel { get; }
            public int AmountLevel { get; }
            public int LuckLevel { get; }
            public decimal InitialReturnSeconds { get; }
            public Worker(decimal tileSeconds, int speed, int amount, int luck, decimal initialReturn)
            { TileSeconds = tileSeconds; SpeedLevel = speed; AmountLevel = amount; LuckLevel = luck; InitialReturnSeconds = initialReturn; }
        }
        private sealed class SimulatedNode
        {
            public ResourceNode Source { get; }
            public int PathLength { get; }
            public int Stock { get; set; }
            public decimal ReadyAt { get; set; }
            public decimal AvailableFrom { get; set; }
            public SimulatedNode(ResourceNode source, int pathLength)
            { Source = source; PathLength = pathLength; Stock = source.RemainingStock; ReadyAt = source.SecondsUntilReady; }
        }
    }

    public sealed class ObjectiveGenerationPlan
    {
        public RoundObjectives Objectives { get; }
        public IReadOnlyDictionary<string, BigInteger> SimulatedCapacities { get; }
        public IReadOnlyDictionary<string, BigInteger> UntrainedSimulatedCapacities { get; }
        public IReadOnlyDictionary<string, BigInteger> RawSimulatedCapacities { get; }
        public IReadOnlyDictionary<string, int> SimulatedGatherCounts { get; }
        public IReadOnlyDictionary<string, BigInteger> BaseAmounts { get; }
        public IReadOnlyDictionary<string, BigInteger> RequestedAmounts { get; }
        public IReadOnlyList<ObjectiveGatherVisit> Visits { get; }
        internal ObjectiveGenerationPlan(RoundObjectives objectives, IDictionary<string, BigInteger> capacities, IDictionary<string, BigInteger> untrainedCapacities,
            IDictionary<string, BigInteger> rawCapacities, IDictionary<string, int> gatherCounts,
            IDictionary<string, BigInteger> baselines, IDictionary<string, BigInteger> requested, IList<ObjectiveGatherVisit> visits)
        {
            Objectives = objectives;
            SimulatedCapacities = new ReadOnlyDictionary<string, BigInteger>(new Dictionary<string, BigInteger>(capacities));
            UntrainedSimulatedCapacities = new ReadOnlyDictionary<string, BigInteger>(new Dictionary<string, BigInteger>(untrainedCapacities));
            RawSimulatedCapacities = new ReadOnlyDictionary<string, BigInteger>(new Dictionary<string, BigInteger>(rawCapacities));
            SimulatedGatherCounts = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(gatherCounts));
            BaseAmounts = new ReadOnlyDictionary<string, BigInteger>(new Dictionary<string, BigInteger>(baselines));
            RequestedAmounts = new ReadOnlyDictionary<string, BigInteger>(new Dictionary<string, BigInteger>(requested));
            Visits = new ReadOnlyCollection<ObjectiveGatherVisit>(visits.ToArray());
        }
    }

    /// <summary>A constructive feasibility witness: leave base, walk, gather a finite batch, return.</summary>
    public sealed class ObjectiveGatherVisit
    {
        public int ActorIndex { get; }
        public string ResourceId { get; }
        public decimal DepartSeconds { get; }
        public decimal GatherStartSeconds { get; }
        public decimal GatherSeconds { get; }
        public int Count { get; }
        public decimal ReturnSeconds { get; }
        internal ObjectiveGatherVisit(int actor, string resource, decimal depart, decimal start, decimal seconds, int count, decimal back)
        { ActorIndex = actor; ResourceId = resource; DepartSeconds = depart; GatherStartSeconds = start;
            GatherSeconds = seconds; Count = count; ReturnSeconds = back; }
    }
}
