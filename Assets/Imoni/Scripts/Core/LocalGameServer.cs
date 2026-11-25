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

    /// <summary>Authoritative local commands. A future network server can implement the same boundary.</summary>
    public interface IGameServer
    {
        GameSnapshot Snapshot();
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
    /// M1 authority for inventory, funds, quality and round time. Gather represents an accepted
    /// gather completion; M3 will validate world access, movement and resource work before calling it.
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
            RoundObjectives objectives = null)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            string[] ids = (actorIds ?? Enumerable.Range(1, config.TeamSize)
                .Select(index => "player" + index)).ToArray();
            if (ids.Length != config.TeamSize || ids.Any(string.IsNullOrWhiteSpace) ||
                ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
                throw new ArgumentException("Supply one unique, nonempty actor ID for every configured team member.", nameof(actorIds));
            this.objectives = ValidateObjectives(objectives);
            players = ids.ToDictionary(id => id, id => new PlayerState(id, config), StringComparer.Ordinal);
            inventory = new Inventory(config);
            qualityRoller = new QualityRoller(config);
            streakCalc = new StreakCalc(config);
            upgradeRules = new UpgradeRules(config);
            ResetChallenge(seed);
            BeginRound(this.objectives);
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
                    players.Values.Select(player => new PlayerSnapshot(player.Id, player.Streak, player.Upgrades)),
                    lots, protection, history, events, lastResult);
            }
        }

        public GatherResult Gather(string actorId, string materialId)
        {
            lock (gate)
            {
                PlayerState player = RequireActorDuringCollection(actorId);
                MaterialDefinition material = config.Material(materialId);
                // Validate every command identifier before advancing the deterministic RNG.
                QualityDefinition quality = qualityRoller.Roll(rng, roundNumber, player.Upgrades["luck"]);
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
                // Compare the remaining duration first so even decimal.MaxValue cannot overflow.
                decimal target = seconds >= config.DurationSeconds - elapsedSeconds
                    ? config.DurationSeconds : elapsedSeconds + seconds;
                if (!bonusRevealed && target >= config.BonusRevealSeconds)
                {
                    bonusRevealed = true;
                    Emit(RoundEventKind.BonusRevealed, config.BonusRevealSeconds);
                }
                elapsedSeconds = target;
                if (elapsedSeconds >= config.DurationSeconds) FinishRound();
            }
        }

        public void StartNextRound(RoundObjectives objectives = null)
        {
            lock (gate)
            {
                if (phase != RoundPhase.Succeeded)
                    throw new InvalidOperationException("The next round is available only after a nonfinal success.");
                RoundObjectives next = ValidateObjectives(objectives);
                roundNumber = checked(roundNumber + 1);
                BeginRound(next);
            }
        }

        public void Retry(int seed, RoundObjectives objectives = null)
        {
            lock (gate)
            {
                if (phase != RoundPhase.Failed)
                    throw new InvalidOperationException("Retry is available only after a failed challenge.");
                RoundObjectives first = ValidateObjectives(objectives);
                ResetChallenge(seed);
                history.Clear();
                events.Clear();
                operationSequence = 0;
                eventSequence = 0;
                lastResult = null;
                BeginRound(first);
            }
        }

        private void FinishRound()
        {
            // Transition before publishing the result; there is no command window after time up.
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
                objectives = config.DefaultObjectives;
            }
        }

        private void ResetChallenge(int worldSeed)
        {
            seed = worldSeed;
            rng = new Rng(seed);
            roundNumber = 1;
            elapsedSeconds = 0;
            bonusRevealed = false;
            funds = config.StartFunds;
            cumulativeScore = BigInteger.Zero;
            inventory.Clear();
            foreach (PlayerState player in players.Values)
            {
                player.Streak = 0;
                foreach (string key in player.Upgrades.Keys.ToArray()) player.Upgrades[key] = 0;
            }
        }

        private void BeginRound(RoundObjectives next)
        {
            objectives = next;
            elapsedSeconds = 0;
            bonusRevealed = false;
            phase = RoundPhase.Collecting;
            foreach (PlayerState player in players.Values) player.Streak = 0;
            Emit(RoundEventKind.Started, 0);
            if (config.BonusRevealSeconds == 0)
            {
                bonusRevealed = true;
                Emit(RoundEventKind.BonusRevealed, 0);
            }
        }

        private RoundObjectives ValidateObjectives(RoundObjectives candidate)
        {
            RoundObjectives next = candidate ?? config.DefaultObjectives;
            next.Validate(config);
            return next;
        }

        private PlayerState RequireActorDuringCollection(string actorId)
        {
            if (phase != RoundPhase.Collecting) throw new InvalidOperationException("The round is not collecting.");
            if (actorId == null || !players.TryGetValue(actorId, out PlayerState actor))
                throw new ArgumentException("Unknown actor.", nameof(actorId));
            return actor;
        }

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
        internal PlayerSnapshot(string id, int streak, IDictionary<string, int> upgrades)
        { Id = id; Streak = streak; Upgrades = new ReadOnlyDictionary<string, int>(new Dictionary<string, int>(upgrades)); }
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

        internal GameSnapshot(RoundPhase phase, int round, int seed, decimal elapsed, bool bonusRevealed,
            BigInteger funds, BigInteger cumulative, IEnumerable<Objective> required, Objective bonus,
            IEnumerable<PlayerSnapshot> players, IEnumerable<MaterialLot> lots,
            IEnumerable<ProtectionSnapshot> protection, IEnumerable<OperationRecord> history,
            IEnumerable<RoundEvent> events, RoundResult lastResult)
        {
            Phase = phase; RoundNumber = round; Seed = seed; ElapsedSeconds = elapsed; BonusRevealed = bonusRevealed;
            Funds = funds; CumulativeScore = cumulative; RequiredObjectives = Array.AsReadOnly(required.ToArray());
            BonusObjective = bonus; Players = Array.AsReadOnly(players.ToArray()); Lots = Array.AsReadOnly(lots.ToArray());
            Protection = Array.AsReadOnly(protection.ToArray()); History = Array.AsReadOnly(history.ToArray());
            Events = Array.AsReadOnly(events.ToArray()); LastResult = lastResult;
        }
    }
}
