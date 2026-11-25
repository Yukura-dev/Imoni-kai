using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace Imoni.Core
{
    /// <summary>
    /// Generates goals from a conservative, constructive team schedule. Each material receives one
    /// disjoint part of the round. Workers return to the base after every gather, so neither time nor
    /// resource stock is counted twice and travel between material stages is already paid for.
    /// Quality and streak bonuses are deliberately excluded from this attainable lower bound.
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
            Worker[] workers = CreateWorkers(world, round, players);
            int count = config.ObjectiveGeneration.RequiredCount + 1;
            while (candidates.Count >= count)
            {
                string[] chosen = candidates.Take(count).ToArray();
                var capacities = new Dictionary<string, BigInteger>(StringComparer.Ordinal);
                string unsuitable = null;
                for (int index = 0; index < chosen.Length; index++)
                {
                    decimal start = config.DurationSeconds * index / count;
                    decimal end = config.DurationSeconds * (index + 1) / count;
                    // The bonus is not actionable until it is publicly revealed.
                    if (index == chosen.Length - 1) start = Math.Max(start, config.BonusRevealSeconds);
                    BigInteger cap = SimulateMaterial(world, round, resources.Where(node => node.MaterialId == chosen[index]),
                        workers, start, end);
                    capacities.Add(chosen[index], cap);
                    if (MinimumAmount(cap) > MaximumAmount(cap)) { unsuitable = chosen[index]; break; }
                }
                if (unsuitable != null)
                {
                    // Every failed attempt removes a material, so malformed/tiny worlds cannot loop forever.
                    candidates.Remove(unsuitable);
                    continue;
                }
                var goals = new List<Objective>();
                foreach (string materialId in chosen)
                {
                    BigInteger low = MinimumAmount(capacities[materialId]);
                    BigInteger high = MaximumAmount(capacities[materialId]);
                    goals.Add(new Objective(materialId, low + rng.NextBelow(high - low + 1)));
                }
                return new ObjectiveGenerationPlan(new RoundObjectives(goals.Take(goals.Count - 1), goals[goals.Count - 1]), capacities);
            }
            throw new InvalidOperationException("The unlocked world cannot supply the configured number of distinct required and bonus objectives within the round budget.");
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
                decimal tileSeconds = GatheringTiming.MoveStepSeconds(config, move);
                PathResult initialPath = Pathfinding.FindPath(world, player == null ? world.Start : player.Position, world.Start, round);
                if (!initialPath.Reachable) throw new InvalidOperationException("A team member cannot return to the base.");
                workers[index] = new Worker(tileSeconds, speed, amount, (initialPath.Steps.Count - 1) * tileSeconds);
            }
            return workers;
        }

        private BigInteger SimulateMaterial(WorldMap world, int round, IEnumerable<ResourceNode> resources,
            Worker[] workers, decimal stageStart, decimal stageEnd)
        {
            var nodes = resources.Select(node => new SimulatedNode(node,
                Pathfinding.FindPathToResource(world, world.Start, node, round).Steps.Count - 1)).ToArray();
            decimal[] freeAt = workers.Select(worker => Math.Max(stageStart, worker.InitialReturnSeconds)).ToArray();
            BigInteger quantity = BigInteger.Zero;
            decimal now = stageStart;
            while (true)
            {
                int nextWorker = -1;
                SimulatedNode nextNode = null;
                decimal nextCompletion = decimal.MaxValue;
                decimal nextReturn = 0;
                for (int actor = 0; actor < workers.Length; actor++)
                {
                    Worker worker = workers[actor];
                    foreach (SimulatedNode node in nodes)
                    {
                        decimal travel = worker.TileSeconds * node.PathLength;
                        decimal gatherStart = Math.Max(freeAt[actor] + travel, node.Stock > 0 ? node.AvailableFrom : node.ReadyAt);
                        decimal gatherSeconds = GatheringTiming.CollectSeconds(config, worker.SpeedLevel, node.Source);
                        decimal complete = gatherStart + gatherSeconds;
                        decimal backAt = complete + travel;
                        if (backAt >= stageEnd || complete >= nextCompletion) continue;
                        nextCompletion = complete; nextWorker = actor; nextNode = node; nextReturn = backAt;
                    }
                }
                if (nextWorker < 0) return quantity;
                now = nextCompletion;
                foreach (SimulatedNode node in nodes)
                {
                    if (node.Stock == 0 && node.ReadyAt <= now)
                    { node.Stock = config.ResourceStock; node.AvailableFrom = node.ReadyAt; }
                }
                nextNode.Stock--;
                if (nextNode.Stock == 0) nextNode.ReadyAt = now + config.ResourceRegrowSeconds;
                freeAt[nextWorker] = nextReturn;
                quantity += quantities.GatherQuantity(config.Material(nextNode.Source.MaterialId).BaseYield,
                    workers[nextWorker].AmountLevel, 0);
            }
        }

        private BigInteger MinimumAmount(BigInteger capacity)
        {
            GameMath.DecimalRatio(config.ObjectiveGeneration.MinimumFraction, out BigInteger numerator, out BigInteger denominator);
            return BigInteger.Max(BigInteger.One, (capacity * numerator + denominator - 1) / denominator);
        }
        private BigInteger MaximumAmount(BigInteger capacity) => GameMath.MultiplyFloor(capacity, config.ObjectiveGeneration.MaximumFraction);

        private sealed class Worker
        {
            public decimal TileSeconds { get; }
            public int SpeedLevel { get; }
            public int AmountLevel { get; }
            public decimal InitialReturnSeconds { get; }
            public Worker(decimal tileSeconds, int speed, int amount, decimal initialReturn)
            { TileSeconds = tileSeconds; SpeedLevel = speed; AmountLevel = amount; InitialReturnSeconds = initialReturn; }
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
        internal ObjectiveGenerationPlan(RoundObjectives objectives, IDictionary<string, BigInteger> capacities)
        {
            Objectives = objectives;
            SimulatedCapacities = new ReadOnlyDictionary<string, BigInteger>(new Dictionary<string, BigInteger>(capacities));
        }
    }
}
