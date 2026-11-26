using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Imoni.Core.Effects
{
    public sealed class ImoniEffectItem
    {
        public int Id { get; internal set; }
        public int Kind { get; internal set; }
        public double SpawnMs { get; internal set; }
        public double FallMs { get; internal set; }
        public double JitterX { get; internal set; }
        public double Rotation { get; internal set; }
        public double Spin { get; internal set; }
        public BigInteger Points { get; internal set; }
        public double LandingMs => SpawnMs + FallMs;
    }

    public sealed class ImoniEffectBurst
    {
        public int Id { get; internal set; }
        public double AtMs { get; internal set; }
        public double X { get; internal set; }
        public bool Big { get; internal set; }
        public double Seed { get; internal set; }
    }

    public sealed class ImoniEffectEvent
    {
        public string Kind { get; internal set; }
        public double AtMs { get; internal set; }
        public int Level { get; internal set; }
        public int Hits { get; internal set; }
        public BigInteger Points { get; internal set; }
    }

    /// <summary>Absolute-time event simulation. Rendering cadence never changes score, spawn or level-up times.</summary>
    public sealed class ImoniEffectTimeline
    {
        private readonly List<ImoniEffectItem> items = new List<ImoniEffectItem>();
        private readonly List<ImoniEffectBurst> bursts = new List<ImoniEffectBurst>();
        private readonly List<ImoniEffectEvent> events = new List<ImoniEffectEvent>();
        private readonly ImoniEffectRandom random;
        private BigInteger committed;
        private double nextSpawn;
        private double nextBeat;
        private bool droneStarted;
        private int nextItemId, nextBurstId, hits;
        public ImoniEffectConfig Config { get; }
        public BigInteger FinalScore { get; }
        public BigInteger SuccessReferenceScore { get; }
        public bool Failed { get; }
        public BigInteger[] Thresholds { get; }
        public double NowMs { get; private set; }
        public int Level { get; private set; }
        public BigInteger Score { get; private set; }
        public double LastHitMs { get; private set; } = double.NegativeInfinity;
        public double LevelAtMs { get; private set; } = double.NegativeInfinity;
        public double? FinaleAtMs { get; private set; }
        public double? ResultAtMs => FinaleAtMs + Config.Number(Failed ? "failResultButtonDelayMs" : "resultButtonDelayAfterFinaleMs");
        public bool ResultVisible => ResultAtMs.HasValue && NowMs >= ResultAtMs.Value;
        public IReadOnlyList<ImoniEffectItem> Items => items;
        public IReadOnlyList<ImoniEffectBurst> Bursts => bursts;
        public IReadOnlyList<ImoniEffectEvent> Events => events;
        public IEnumerable<ImoniEffectEvent> LevelEvents => events.Where(e => e.Kind == "level");
        public IEnumerable<ImoniEffectEvent> HitEvents => events.Where(e => e.Kind == "hit");

        public ImoniEffectTimeline(ImoniEffectConfig config, BigInteger finalScore, BigInteger successReferenceScore, bool failed, int seed = 7)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            if (finalScore < 0 || successReferenceScore < 0) throw new ArgumentOutOfRangeException(nameof(finalScore));
            FinalScore = finalScore; SuccessReferenceScore = successReferenceScore; Failed = failed;
            random = new ImoniEffectRandom(seed);
            Thresholds = config.ThresholdRatios.Select(r => RoundRatio(successReferenceScore, r)).ToArray();
            nextSpawn = config.SpawnStartMs; nextBeat = config.V("droneStartMs");
        }

        public void AdvanceTo(double elapsedMs)
        {
            if (double.IsNaN(elapsedMs) || double.IsInfinity(elapsedMs) || elapsedMs < NowMs)
                throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Elapsed time must be finite and monotonic. Construct a new timeline to replay.");
            while (!FinaleAtMs.HasValue)
            {
                var landing = items.Count == 0 ? double.PositiveInfinity : items.Min(i => i.LandingMs);
                var spawn = committed < FinalScore ? nextSpawn : double.PositiveInfinity;
                var zeroFinish = FinalScore.IsZero ? Config.SpawnStartMs : double.PositiveInfinity;
                var at = Math.Min(Math.Min(landing, spawn), Math.Min(Math.Min(nextBeat, zeroFinish), Config.HardCapMs));
                if (at > elapsedMs) break;
                if (at >= Config.HardCapMs)
                {
                    var missing = FinalScore - Score;
                    if (missing > 0) events.Add(new ImoniEffectEvent { Kind = "score", AtMs = at, Points = missing, Level = Level });
                    Score = FinalScore; committed = FinalScore; items.Clear();
                    SetLevel(at);
                    Complete(at);
                    break;
                }
                if (at >= nextBeat)
                {
                    if (!droneStarted) { droneStarted = true; AddEvent("drone", at); }
                    AddEvent("beat", at); nextBeat = at + Config.BeatIntervals[Level];
                }
                if (at >= spawn)
                {
                    Spawn(at);
                    if (Level >= Config.V("doubleSpawnMinLevel") && random.Next() < Config.V("doubleSpawnChance") && committed < FinalScore) Spawn(at);
                    nextSpawn = at + Config.SpawnIntervals[Level] * random.Range(Config.V("spawnJitterMin"), Config.V("spawnJitterMax"));
                }
                foreach (var item in items.Where(i => i.LandingMs <= at).ToArray())
                {
                    items.Remove(item); Score += item.Points; hits++; LastHitMs = at;
                    AddBurst(at, false, Config.V("centerX") + random.Range(-Config.V("hitJitterX"), Config.V("hitJitterX")), random.Range(0, 360));
                    AddEvent("hit", at, item.Points); SetLevel(at);
                }
                if (Score >= FinalScore && items.Count == 0 && (FinalScore > 0 || at >= Config.SpawnStartMs)) Complete(at);
            }
            NowMs = elapsedMs;
            bursts.RemoveAll(b => elapsedMs - b.AtMs >= Config.V(b.Big ? "bigBurstMs" : "smallBurstMs"));
        }

        public BigInteger DisplayScore
        {
            get
            {
                // A huge integer can retain a visible exponential tail for seconds. Once the
                // result is actionable, show the exact settled total rather than a rounded tail.
                if (ResultVisible) return FinalScore;
                // Analytic sum of score steps solves disp += (score-disp)*(1-pow(.78,dt*60)).
                // It avoids float conversion of the score and gives the same display at any render cadence.
                var pending = BigInteger.Zero;
                foreach (var e in events)
                {
                    if (e.Points.IsZero) continue;
                    var decay = Math.Pow(Config.V("displayDecay"), Math.Max(0, NowMs - e.AtMs) / 1000 * Config.V("displayReferenceFps"));
                    pending += MultiplyFraction(e.Points, decay);
                }
                return BigInteger.Max(BigInteger.Zero, Score - pending);
            }
        }

        public ImoniEffectFrame Evaluate() => ImoniEffectMath.Evaluate(this);

        private void Spawn(double at)
        {
            var fraction = Math.Max(0, Math.Min(1, (at - Config.SpawnStartMs) / (Config.SpawnEndMs - Config.SpawnStartMs)));
            var cumulative = at >= Config.SpawnEndMs ? FinalScore : MultiplyFraction(FinalScore, fraction * fraction, true);
            var points = BigInteger.Min(FinalScore - committed, BigInteger.Max(BigInteger.One, cumulative - committed));
            if (points <= 0) return;
            committed += points;
            items.Add(new ImoniEffectItem
            {
                Id = nextItemId++, Kind = (int)(random.Next() * Config.V("ingredientKinds")), SpawnMs = at, FallMs = Config.Falls[Level],
                JitterX = random.Range(-Config.V("ingredientJitterX"), Config.V("ingredientJitterX")), Rotation = random.Range(0, 360),
                Spin = random.Range(-Config.V("ingredientSpin"), Config.V("ingredientSpin")), Points = points
            });
        }

        private void SetLevel(double at)
        {
            var level = 0;
            if (Score > 0) while (level < Thresholds.Length && Score >= Thresholds[level]) level++;
            if (Failed) level = Math.Min(level, Config.FailMaxLevel);
            if (level <= Level) return;
            Level = level; hits = 0; LevelAtMs = at;
            AddBurst(at, true, Config.V("centerX"), 0); AddEvent("level", at);
        }

        private void Complete(double at)
        {
            FinaleAtMs = at;
            if (!Failed) AddBurst(at, true, Config.V("centerX"), 0);
            AddEvent("finale", at);
        }

        private void AddEvent(string kind, double at, BigInteger points = default) => events.Add(new ImoniEffectEvent { Kind = kind, AtMs = at, Level = Level, Hits = hits, Points = points });
        private void AddBurst(double at, bool big, double x, double seed) => bursts.Add(new ImoniEffectBurst { Id = nextBurstId++, AtMs = at, Big = big, X = x, Seed = seed });
        private static BigInteger RoundRatio(BigInteger value, decimal ratio)
        {
            const long denominator = 1000000000000000000L;
            return (value * new BigInteger(decimal.Round(ratio * denominator)) + denominator / 2) / denominator;
        }
        private static BigInteger MultiplyFraction(BigInteger value, double fraction, bool round = false)
        {
            const long denominator = 1000000000000000000L;
            var numerator = new BigInteger(decimal.Round((decimal)Math.Max(0, Math.Min(1, fraction)) * denominator));
            return (value * numerator + (round ? denominator / 2 : 0)) / denominator;
        }
    }
}
