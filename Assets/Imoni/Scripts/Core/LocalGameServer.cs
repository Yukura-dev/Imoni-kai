using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace Imoni.Core
{
    public enum RoundPhase { Collecting, Succeeded, Failed, Cleared }
    public enum RoundEventKind { Started, BonusRevealed, TimeUp, Succeeded, Failed, Cleared }
    public enum OperationKind { Gather, Buy, Sell, SetProtection, Upgrade }
    public enum PlayerAction { Idle, Moving, Gathering }

    /// <summary>Authoritative local commands. A future network server can implement the same boundary.</summary>
    public interface IGameServer
    {
        GameSnapshot Snapshot();
        WorldMap World { get; }
        bool CanSelectResource(string actorId, string resourceId);
        bool TrySelectResource(string actorId, string resourceId);
        void CancelAction(string actorId);
        void SetConnected(string actorId, bool connected);
        GatherResult Gather(string actorId, string materialId);
        BigInteger Buy(string actorId, string materialId, BigInteger quantity);
        BigInteger Sell(string actorId, string materialId, string qualityId, BigInteger quantity);
        void SetProtection(string actorId, string materialId, string qualityId, BigInteger quantity);
        BigInteger Upgrade(string actorId, string upgradeId);
        void Tick(decimal seconds);
        void StartNextRound(RoundObjectives objectives = null);
        void Retry(int seed, RoundObjectives objectives = null);
    }

    /// <summary>
    /// Authority for world movement, timed gathering, inventory, funds, quality and round time.
    /// Gather can commit only the work completion currently being processed by Tick.
    /// No UI owns or mutates the inventory. Every published value is an immutable snapshot.
    /// </summary>
    public sealed class LocalGameServer : IGameServer
    {
        private readonly object gate = new object();
        private readonly GameConfig config;
        private readonly Inventory inventory;
        private readonly QualityRoller qualityRoller;
        private readonly StreakCalc streakCalc;
        private readonly UpgradeRules upgradeRules;
        private readonly Dictionary<string, PlayerState> players;
        private readonly string[] actorOrder;
        private readonly bool enableBots;
        private readonly ObjectiveGenerator objectiveGenerator;
        private readonly Bots bots;
        private decimal nextBotThink;
        private string authorizedGatherActor;
        private string authorizedGatherNode;
        public WorldMap World { get; private set; }
        private readonly List<OperationRecord> history = new List<OperationRecord>();
        private readonly List<RoundEvent> events = new List<RoundEvent>();
        private Rng rng;
        private int seed;
        private int roundNumber;
        private decimal elapsedSeconds;
        private bool bonusRevealed;
        private BigInteger funds;
        private BigInteger cumulativeScore;
        private RoundObjectives objectives;
        private RoundPhase phase;
        private RoundResult lastResult;
        private long operationSequence;
        private long eventSequence;

        public LocalGameServer(GameConfig config, int seed, IEnumerable<string> actorIds = null,
            RoundObjectives objectives = null, bool enableBots = true)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            string[] ids = (actorIds ?? Enumerable.Range(1, config.TeamSize)
                .Select(index => "player" + index)).ToArray();
            if (ids.Length != config.TeamSize || ids.Any(string.IsNullOrWhiteSpace) ||
                ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new ArgumentException("Supply one unique, nonempty actor ID for every configured team member.", nameof(actorIds));
            objectives?.Validate(config);
            this.enableBots = enableBots;
            actorOrder = ids;
            objectiveGenerator = new ObjectiveGenerator(config);
            bots = new Bots();
            players = ids.ToDictionary(id => id, id => new PlayerState(id, config), StringComparer.Ordinal);
            inventory = new Inventory(config);
            qualityRoller = new QualityRoller(config);
            streakCalc = new StreakCalc(config);
            upgradeRules = new UpgradeRules(config);
            ResetChallenge(seed);
            BeginRound(ValidateObjectives(objectives));
        }

        public GameSnapshot Snapshot()
        {
            lock (gate)
            {
                var lots = inventory.Lots.ToArray();
                var protection = lots.Select(lot => (lot.MaterialId, lot.QualityId)).Distinct()
                    .Select(key => new ProtectionSnapshot(key.MaterialId, key.QualityId,
                        inventory.ProtectedQuantity(key.MaterialId, key.QualityId)))
                    .Where(item => item.Quantity > BigInteger.Zero).ToArray();
                return new GameSnapshot(phase, roundNumber, seed, elapsedSeconds, bonusRevealed, funds,
                    cumulativeScore, objectives.Required, bonusRevealed ? objectives.Bonus : null,
                    actorOrder.Select(id => PlayerView(players[id])),
                    lots, protection, history, events, lastResult, World.Resources.Select(node => node.Snapshot()));
            }
        }

        public bool CanSelectResource(string actorId, string resourceId)
        {
            lock (gate) return TryGetSelection(actorId, resourceId, out _, out _, out _);
        }

        public bool TrySelectResource(string actorId, string resourceId)
        {
            lock (gate)
            {
                if (!TryGetSelection(actorId, resourceId, out PlayerState player, out ResourceNode node, out PathResult path))
                    return false;
                StopAction(player);
                player.TargetResourceId = node.Id;
                player.Path = path.Steps.ToArray();
                player.PathIndex = 0;
                if (player.Path.Length > 1) BeginMoveStep(player);
                else BeginGather(player, node);
                return true;
            }
        }

        public void CancelAction(string actorId)
        {
            lock (gate) StopAction(RequireActor(actorId));
        }

        public void SetConnected(string actorId, bool connected)
        {
            lock (gate)
            {
                PlayerState player = RequireActor(actorId);
                player.Connected = connected;
                if (!connected) StopAction(player);
            }
        }

        public GatherResult Gather(string actorId, string materialId)
        {
            lock (gate)
            {
                PlayerState player = RequireActorDuringCollection(actorId);
                MaterialDefinition material = config.Material(materialId);
                ResourceNode node = World.Resources.FirstOrDefault(item => item.Id == authorizedGatherNode);
                if (authorizedGatherActor != actorId || node == null || node.MaterialId != materialId ||
                    player.Action != PlayerAction.Gathering || player.ActionRemainingSeconds > 0 ||
                    !node.IsReady || !player.Connected)
                    throw new InvalidOperationException("Gather requires a completed, reachable resource action from Tick.");
                // Consume authorization before advancing randomness so a completion is single use.
                authorizedGatherActor = null;
                authorizedGatherNode = null;
                if (!node.TryGather()) throw new InvalidOperationException("The node is depleted.");
                QualityDefinition quality = qualityRoller.Roll(rng, roundNumber, player.Upgrades["luck"],
                    node.BiomeId, node.MaterialId, node.QualityModifier);
                int streak = qualityRoller.IsHighest(quality, roundNumber) ? checked(player.Streak + 1) : 0;
                BigInteger multiplier = streakCalc.MultiplierRaw(streak);
                BigInteger quantity = streakCalc.GatherQuantity(material.BaseYield, player.Upgrades["amount"], streak);
                inventory.Add(materialId, quality.Id, quantity, multiplier);
                player.Streak = streak;
                Record(actorId, OperationKind.Gather, materialId, quality.Id, quantity, BigInteger.Zero);
                return new GatherResult(materialId, quality.Id, quantity, multiplier, streak);
            }
        }

        public BigInteger Buy(string actorId, string materialId, BigInteger quantity)
        {
            lock (gate)
            {
                RequireActorDuringCollection(actorId);
                RequirePositiveQuantity(quantity);
                MaterialDefinition material = config.Material(materialId);
                QualityDefinition quality = config.Quality(config.PurchasedQualityId);
                BigInteger cost = GameMath.MultiplyFloor(quantity * material.BasePrice * quality.Multiplier,
                    config.BuyPriceMultiplier);
                if (funds < cost) throw new InvalidOperationException("Insufficient shared funds.");
                inventory.Add(materialId, quality.Id, quantity, config.FixedScale);
                funds -= cost;
                Record(actorId, OperationKind.Buy, materialId, quality.Id, quantity, -cost);
                return cost;
            }
        }

        public BigInteger Sell(string actorId, string materialId, string qualityId, BigInteger quantity)
        {
            lock (gate)
            {
                RequireActorDuringCollection(actorId);
                RequirePositiveQuantity(quantity);
                BigInteger proceeds = inventory.Sell(materialId, qualityId, quantity);
                funds += proceeds;
                Record(actorId, OperationKind.Sell, materialId, qualityId, quantity, proceeds);
                return proceeds;
            }
        }

        public void SetProtection(string actorId, string materialId, string qualityId, BigInteger quantity)
        {
            lock (gate)
            {
                RequireActorDuringCollection(actorId);
                inventory.SetProtectedQuantity(materialId, qualityId, quantity);
                Record(actorId, OperationKind.SetProtection, materialId, qualityId, quantity, BigInteger.Zero);
            }
        }

        public BigInteger Upgrade(string actorId, string upgradeId)
        {
            lock (gate)
            {
                PlayerState player = RequireActorDuringCollection(actorId);
                config.Upgrade(upgradeId);
                int level = player.Upgrades[upgradeId];
                int nextLevel = checked(level + 1);
                BigInteger cost = upgradeRules.Price(upgradeId, level);
                if (funds < cost) throw new InvalidOperationException("Insufficient shared funds.");
                funds -= cost;
                player.Upgrades[upgradeId] = nextLevel;
                Record(actorId, OperationKind.Upgrade, null, null, BigInteger.One, -cost, upgradeId);
                return cost;
            }
        }

        public void Tick(decimal seconds)
        {
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            lock (gate)
            {
                if (phase != RoundPhase.Collecting) return;
                decimal target = seconds >= config.DurationSeconds - elapsedSeconds
                    ? config.DurationSeconds : elapsedSeconds + seconds;
                // Advance to exact simulation events rather than using render-frame-sized steps.
                while (elapsedSeconds < target)
                {
                    CancelUnavailableTargets();
                    decimal step = target - elapsedSeconds;
                    if (!bonusRevealed) step = Math.Min(step, config.BonusRevealSeconds - elapsedSeconds);
                    if (enableBots) step = Math.Min(step, nextBotThink - elapsedSeconds);
                    foreach (string id in actorOrder)
                    {
                        PlayerState player = players[id];
                        if (player.Action != PlayerAction.Idle) step = Math.Min(step, player.ActionRemainingSeconds);
                    }
                    foreach (ResourceNode node in World.Resources) node.Tick(step);
                    foreach (string id in actorOrder)
                        if (players[id].Action != PlayerAction.Idle) players[id].ActionRemainingSeconds -= step;
                    elapsedSeconds += step;
                    // The deadline wins ties with gather completion. No work can commit at 5:00.
                    if (elapsedSeconds >= config.DurationSeconds) { FinishRound(); break; }
                    if (!bonusRevealed && elapsedSeconds >= config.BonusRevealSeconds)
                    {
                        bonusRevealed = true;
                        Emit(RoundEventKind.BonusRevealed, config.BonusRevealSeconds);
                    }
                    ProcessCompletedActions();
                    if (enableBots && elapsedSeconds >= nextBotThink)
                    {
                        nextBotThink += config.BotIntervalSeconds;
                        foreach (string id in actorOrder)
                            if (players[id].IsBot) bots.Think(this, id);
                    }
                }
            }
        }

        public void StartNextRound(RoundObjectives objectives = null)
        {
            lock (gate)
            {
                if (phase != RoundPhase.Succeeded)
                    throw new InvalidOperationException("The next round is available only after a nonfinal success.");
                int nextRound = checked(roundNumber + 1);
                RoundObjectives next = ValidateObjectives(objectives, nextRound);
                roundNumber = nextRound;
                BeginRound(next);
            }
        }

        public void Retry(int seed, RoundObjectives objectives = null)
        {
            lock (gate)
            {
                if (phase != RoundPhase.Failed)
                    throw new InvalidOperationException("Retry is available only after a failed challenge.");
                objectives?.Validate(config);
                WorldMap nextWorld = WorldGen.Generate(config, seed);
                RoundObjectives next = objectives == null
                    ? objectiveGenerator.Generate(nextWorld, 1, new Rng(unchecked(seed + 1)))
                    : ValidateObjectives(objectives, 1, nextWorld);
                ResetChallenge(seed, nextWorld);
                history.Clear();
                events.Clear();
                operationSequence = 0;
                eventSequence = 0;
                lastResult = null;
                BeginRound(next);
            }
        }

        private void FinishRound()
        {
            // Transition before publishing the result; there is no command window after time up.
            foreach (PlayerState player in players.Values) StopAction(player);
            Emit(RoundEventKind.TimeUp, config.DurationSeconds);
            RoundEvaluation evaluation = Scoring.Evaluate(config, inventory, objectives);
            if (evaluation.Success)
            {
                cumulativeScore += evaluation.Score;
                funds += evaluation.FundsReward;
                phase = roundNumber >= config.RoundsToClear ? RoundPhase.Cleared : RoundPhase.Succeeded;
                lastResult = new RoundResult(roundNumber, seed, config.DurationSeconds, evaluation,
                    cumulativeScore, history);
                inventory.Clear();
                foreach (PlayerState player in players.Values) player.Streak = 0;
                Emit(phase == RoundPhase.Cleared ? RoundEventKind.Cleared : RoundEventKind.Succeeded,
                    config.DurationSeconds);
            }
            else
            {
                lastResult = new RoundResult(roundNumber, seed, config.DurationSeconds, evaluation,
                    cumulativeScore, history);
                Emit(RoundEventKind.Failed, config.DurationSeconds);
                // Preserve the detached result/event log for GameOver; reset every active run value.
                ResetChallenge(seed);
                phase = RoundPhase.Failed;
                history.Clear();
                operationSequence = 0;
                // Failed snapshots retain the evaluated objectives; fresh goals are generated on Retry.
            }
        }

        private void ResetChallenge(int worldSeed, WorldMap preparedWorld = null)
        {
            seed = worldSeed;
            rng = new Rng(seed);
            World = preparedWorld ?? WorldGen.Generate(config, seed);
            roundNumber = 1;
            elapsedSeconds = 0;
            bonusRevealed = false;
            funds = config.StartFunds;
            cumulativeScore = BigInteger.Zero;
            inventory.Clear();
            foreach (PlayerState player in players.Values)
            {
                player.Streak = 0;
                player.Position = World.Start;
                player.Connected = true;
                player.IsBot = enableBots && player.Id != actorOrder[0];
                StopAction(player);
                foreach (string key in player.Upgrades.Keys.ToArray()) player.Upgrades[key] = 0;
            }
        }

        private void BeginRound(RoundObjectives next)
        {
            objectives = next;
            elapsedSeconds = 0;
            bonusRevealed = false;
            phase = RoundPhase.Collecting;
            foreach (PlayerState player in players.Values) { player.Streak = 0; StopAction(player); }
            nextBotThink = config.BotIntervalSeconds;
            Emit(RoundEventKind.Started, 0);
            if (config.BonusRevealSeconds == 0)
            {
                bonusRevealed = true;
                Emit(RoundEventKind.BonusRevealed, 0);
            }
        }

        private RoundObjectives ValidateObjectives(RoundObjectives candidate, int? targetRound = null, WorldMap targetWorld = null)
        {
            // Independent goal RNG keeps map/goal generation from shifting the gather RNG stream.
            RoundObjectives next = candidate ?? objectiveGenerator.Generate(World, targetRound ?? roundNumber,
                new Rng(unchecked(seed + (targetRound ?? roundNumber))), actorOrder.Select(id => PlayerView(players[id])));
            next.Validate(config);
            if (candidate != null)
            {
                var available = new HashSet<string>((targetWorld ?? World).ResourcesAvailable(targetRound ?? roundNumber)
                    .Select(node => node.MaterialId), StringComparer.Ordinal);
                if (next.Required.Any(goal => !available.Contains(goal.MaterialId)) || !available.Contains(next.Bonus.MaterialId))
                    throw new ArgumentException("Every objective must be available in the unlocked world.", nameof(candidate));
            }
            return next;
        }

        private PlayerState RequireActorDuringCollection(string actorId)
        {
            if (phase != RoundPhase.Collecting) throw new InvalidOperationException("The round is not collecting.");
            return RequireActor(actorId);
        }

        private PlayerState RequireActor(string actorId)
        {
            if (actorId == null || !players.TryGetValue(actorId, out PlayerState actor))
                throw new ArgumentException("Unknown actor.", nameof(actorId));
            return actor;
        }

        private bool TryGetSelection(string actorId, string resourceId, out PlayerState player,
            out ResourceNode node, out PathResult path)
        {
            player = null; node = null; path = null;
            if (phase != RoundPhase.Collecting || actorId == null || !players.TryGetValue(actorId, out player) ||
                !player.Connected) return false;
            node = World.Resources.FirstOrDefault(item => item.Id == resourceId);
            if (node == null || !node.IsReady) return false;
            path = Pathfinding.FindPathToResource(World, player.Position, node, roundNumber);
            return path.Reachable;
        }

        private void BeginMoveStep(PlayerState player)
        {
            player.Action = PlayerAction.Moving;
            SetDuration(player, GatheringTiming.MoveStepSeconds(config, player.Upgrades["move"]));
        }

        private void BeginGather(PlayerState player, ResourceNode node)
        {
            if (!node.IsReady || !player.Connected) { StopAction(player); return; }
            player.Action = PlayerAction.Gathering;
            SetDuration(player, GatheringTiming.CollectSeconds(config, player.Upgrades["speed"], node));
        }

        private static void SetDuration(PlayerState player, decimal duration)
        {
            player.ActionDurationSeconds = duration;
            player.ActionRemainingSeconds = duration;
        }

        private void ProcessCompletedActions()
        {
            foreach (string id in actorOrder)
            {
                PlayerState player = players[id];
                if (player.Action == PlayerAction.Idle || player.ActionRemainingSeconds > 0) continue;
                ResourceNode node = World.Resources.FirstOrDefault(item => item.Id == player.TargetResourceId);
                if (node == null || !node.IsReady || !player.Connected) { StopAction(player); continue; }
                if (player.Action == PlayerAction.Moving)
                {
                    player.Position = player.Path[++player.PathIndex];
                    if (player.PathIndex + 1 < player.Path.Length) BeginMoveStep(player);
                    else BeginGather(player, node);
                }
                else
                {
                    authorizedGatherActor = player.Id;
                    authorizedGatherNode = node.Id;
                    try { ((IGameServer)this).Gather(player.Id, node.MaterialId); }
                    finally { authorizedGatherActor = null; authorizedGatherNode = null; }
                    if (node.IsReady) BeginGather(player, node);
                    else StopAction(player);
                }
            }
            CancelUnavailableTargets();
        }

        private void CancelUnavailableTargets()
        {
            foreach (PlayerState player in players.Values)
            {
                if (player.Action == PlayerAction.Idle) continue;
                ResourceNode node = World.Resources.FirstOrDefault(item => item.Id == player.TargetResourceId);
                if (!player.Connected || node == null || !node.IsReady) StopAction(player);
            }
        }

        private static void StopAction(PlayerState player)
        {
            player.Action = PlayerAction.Idle;
            player.TargetResourceId = null;
            player.Path = Array.Empty<GridPosition>();
            player.PathIndex = 0;
            player.ActionRemainingSeconds = player.ActionDurationSeconds = 0;
        }

        private static PlayerSnapshot PlayerView(PlayerState player) =>
            new PlayerSnapshot(player.Id, player.Streak, player.Upgrades, player.Position,
                player.Action == PlayerAction.Moving ? player.Path[player.PathIndex + 1] : player.Position,
                player.Action, player.TargetResourceId, player.ActionRemainingSeconds, player.ActionDurationSeconds,
                player.Connected, player.IsBot);

        private static void RequirePositiveQuantity(BigInteger quantity)
        {
            if (quantity <= BigInteger.Zero) throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        private void Record(string actor, OperationKind kind, string material, string quality,
            BigInteger quantity, BigInteger fundsDelta, string upgradeId = null)
        {
            history.Add(new OperationRecord(++operationSequence, actor, kind, material, quality,
                quantity, fundsDelta, roundNumber, elapsedSeconds, upgradeId));
        }

        private void Emit(RoundEventKind kind, decimal atSeconds)
        {
            events.Add(new RoundEvent(++eventSequence, kind, roundNumber, atSeconds));
        }

        private sealed class PlayerState
        {
            public string Id { get; }
            public int Streak { get; set; }
            public Dictionary<string, int> Upgrades { get; }
            public GridPosition Position { get; set; }
            public GridPosition[] Path { get; set; } = Array.Empty<GridPosition>();
            public int PathIndex { get; set; }
            public PlayerAction Action { get; set; }
            public string TargetResourceId { get; set; }
            public decimal ActionRemainingSeconds { get; set; }
            public decimal ActionDurationSeconds { get; set; }
            public bool Connected { get; set; } = true;
            public bool IsBot { get; set; }
            public PlayerState(string id, GameConfig settings)
            {
                Id = id;
                Upgrades = settings.Upgrades.ToDictionary(upgrade => upgrade.Id, upgrade => 0, StringComparer.Ordinal);
            }
        }
    }

    public sealed class GatherResult
    {
        public string MaterialId { get; }
        public string QualityId { get; }
        public BigInteger Quantity { get; }
        public BigInteger PointMultiplierRaw { get; }
        public int Streak { get; }
        internal GatherResult(string materialId, string qualityId, BigInteger quantity, BigInteger multiplier, int streak)
        { MaterialId = materialId; QualityId = qualityId; Quantity = quantity; PointMultiplierRaw = multiplier; Streak = streak; }
    }

    public sealed class RoundEvent
    {
        public long Sequence { get; }
        public RoundEventKind Kind { get; }
        public int RoundNumber { get; }
        public decimal ElapsedSeconds { get; }
        internal RoundEvent(long sequence, RoundEventKind kind, int round, decimal elapsed)
        { Sequence = sequence; Kind = kind; RoundNumber = round; ElapsedSeconds = elapsed; }
    }

    public sealed class OperationRecord
    {
        public long Sequence { get; }
        public string ActorId { get; }
        public OperationKind Kind { get; }
        public string MaterialId { get; }
        public string QualityId { get; }
        public BigInteger Quantity { get; }
        public BigInteger FundsDelta { get; }
        public int RoundNumber { get; }
        public decimal ElapsedSeconds { get; }
        public string UpgradeId { get; }
        internal OperationRecord(long sequence, string actorId, OperationKind kind, string materialId,
            string qualityId, BigInteger quantity, BigInteger fundsDelta, int roundNumber, decimal elapsed, string upgradeId)
        {
            Sequence = sequence; ActorId = actorId; Kind = kind; MaterialId = materialId; QualityId = qualityId;
            Quantity = quantity; FundsDelta = fundsDelta; RoundNumber = roundNumber; ElapsedSeconds = elapsed; UpgradeId = upgradeId;
        }
    }

    public sealed class PlayerSnapshot
    {
        public string Id { get; }
        public int Streak { get; }
        public IReadOnlyDictionary<string, int> Upgrades { get; }
        public GridPosition Position { get; }
        public GridPosition NextPosition { get; }
        public PlayerAction Action { get; }
        public string TargetResourceId { get; }
        public decimal ActionRemainingSeconds { get; }
        public decimal ActionDurationSeconds { get; }
        public bool Connected { get; }
        public bool IsBot { get; }
        internal PlayerSnapshot(string id, int streak, IDictionary<string, int> upgrades,
            GridPosition position = default, GridPosition nextPosition = default, PlayerAction action = PlayerAction.Idle,
            string targetResourceId = null, decimal remaining = 0, decimal duration = 0,
            bool connected = true, bool isBot = false)
        {
            Id = id; Streak = streak; Upgrades = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(upgrades));
            Position = position; NextPosition = nextPosition; Action = action; TargetResourceId = targetResourceId;
            ActionRemainingSeconds = remaining; ActionDurationSeconds = duration; Connected = connected; IsBot = isBot;
        }
    }

    public sealed class ProtectionSnapshot
    {
        public string MaterialId { get; }
        public string QualityId { get; }
        public BigInteger Quantity { get; }
        internal ProtectionSnapshot(string material, string quality, BigInteger quantity)
        { MaterialId = material; QualityId = quality; Quantity = quantity; }
    }

    public sealed class RoundResult
    {
        public int RoundNumber { get; }
        public int Seed { get; }
        public decimal ElapsedSeconds { get; }
        public RoundEvaluation Evaluation { get; }
        public BigInteger CumulativeScore { get; }
        public IReadOnlyList<OperationRecord> History { get; }
        internal RoundResult(int round, int seed, decimal elapsed, RoundEvaluation evaluation,
            BigInteger cumulative, IEnumerable<OperationRecord> history)
        {
            RoundNumber = round; Seed = seed; ElapsedSeconds = elapsed; Evaluation = evaluation;
            CumulativeScore = cumulative; History = Array.AsReadOnly(history.ToArray());
        }
    }

    public sealed class GameSnapshot
    {
        public RoundPhase Phase { get; }
        public int RoundNumber { get; }
        public int Seed { get; }
        public decimal ElapsedSeconds { get; }
        public bool BonusRevealed { get; }
        public BigInteger Funds { get; }
        public BigInteger CumulativeScore { get; }
        public IReadOnlyList<Objective> RequiredObjectives { get; }
        public Objective BonusObjective { get; }
        public IReadOnlyList<PlayerSnapshot> Players { get; }
        public IReadOnlyList<MaterialLot> Lots { get; }
        public IReadOnlyList<ProtectionSnapshot> Protection { get; }
        public IReadOnlyList<OperationRecord> History { get; }
        public IReadOnlyList<RoundEvent> Events { get; }
        public RoundResult LastResult { get; }
        public IReadOnlyList<ResourceSnapshot> Resources { get; }

        internal GameSnapshot(RoundPhase phase, int round, int seed, decimal elapsed, bool bonusRevealed,
            BigInteger funds, BigInteger cumulative, IEnumerable<Objective> required, Objective bonus,
            IEnumerable<PlayerSnapshot> players, IEnumerable<MaterialLot> lots,
            IEnumerable<ProtectionSnapshot> protection, IEnumerable<OperationRecord> history,
            IEnumerable<RoundEvent> events, RoundResult lastResult, IEnumerable<ResourceSnapshot> resources)
        {
            Phase = phase; RoundNumber = round; Seed = seed; ElapsedSeconds = elapsed; BonusRevealed = bonusRevealed;
            Funds = funds; CumulativeScore = cumulative; RequiredObjectives = Array.AsReadOnly(required.ToArray());
            BonusObjective = bonus; Players = Array.AsReadOnly(players.ToArray()); Lots = Array.AsReadOnly(lots.ToArray());
            Protection = Array.AsReadOnly(protection.ToArray()); History = Array.AsReadOnly(history.ToArray());
            Events = Array.AsReadOnly(events.ToArray()); LastResult = lastResult;
            Resources = Array.AsReadOnly(resources.ToArray());
        }
    }
}
