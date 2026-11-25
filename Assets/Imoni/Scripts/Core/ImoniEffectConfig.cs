using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Imoni.Core.Effects
{
    /// <summary>GameConfig.imoniEffect is the sole source of animation tuning.</summary>
    public sealed class ImoniEffectConfig
    {
        public JObject Data { get; }
        public JObject Visual { get; }
        public double SpawnStartMs => Number("spawnStartMs");
        public double SpawnEndMs => Number("spawnEndMs");
        public double HardCapMs => Number("hardCapMs");
        public int[] SpawnIntervals { get; }
        public int[] Falls { get; }
        public int[] StreakCounts { get; }
        public int[] SparkleCounts { get; }
        public int[] BeatIntervals { get; }
        public decimal[] ThresholdRatios { get; }
        public string[] LevelNames { get; }
        public int FailMaxLevel => (int)Number("failMaxLevel");
        public double Number(string key) => RequiredNumber(Data, key);
        public double V(string key) => RequiredNumber(Visual, key);
        public double Get(string key) => Visual[key] != null ? V(key) : Number(key);
        public double[] Array(string key) => Values(key);
        public double[] Values(string key) => Visual[key]?.Values<double>().ToArray() ?? throw new ArgumentException("Missing visual array: " + key);

        private ImoniEffectConfig(JObject data)
        {
            Data = (JObject)data.DeepClone();
            Visual = (JObject)Data["visual"] ?? throw new ArgumentException("GameConfig.imoniEffect.visual is required.");
            SpawnIntervals = ReadInts("spawnIntervalMs"); Falls = ReadInts("fallMs");
            StreakCounts = ReadInts("streakCount"); SparkleCounts = ReadInts("sparkleCount");
            BeatIntervals = ReadInts("beatIntervalMs");
            ThresholdRatios = Data["levelThresholdRatios"]?.Values<decimal>().ToArray() ?? throw new ArgumentException("Missing level thresholds.");
            LevelNames = Data["levelNames"]?.Values<string>().ToArray() ?? throw new ArgumentException("Missing level names.");
            if (ThresholdRatios.Length != 5 || LevelNames.Length != 6 || SpawnIntervals.Length != 6 || Falls.Length != 6 ||
                StreakCounts.Length != 6 || SparkleCounts.Length != 6 || BeatIntervals.Length != 6)
                throw new ArgumentException("Effect level settings must contain levels 0 through 5.");
            if (SpawnStartMs < 0 || SpawnEndMs <= SpawnStartMs || HardCapMs < SpawnEndMs ||
                SpawnIntervals.Any(x => x <= 0) || Falls.Any(x => x <= 0) || BeatIntervals.Any(x => x <= 0))
                throw new ArgumentException("Effect durations must be positive and ordered.");
            if (FailMaxLevel < 0 || FailMaxLevel > 5 || ThresholdRatios.Any(x => x < 0 || x > 1) || !ThresholdRatios.SequenceEqual(ThresholdRatios.OrderBy(x => x)))
                throw new ArgumentException("Invalid effect level thresholds.");
            if (Number("resultButtonDelayAfterFinaleMs") < 0 || Number("failResultButtonDelayMs") < 0 ||
                HardCapMs + Math.Max(Number("resultButtonDelayAfterFinaleMs"), Number("failResultButtonDelayMs")) > Number("maxTotalSec") * 1000)
                throw new ArgumentException("Effect result button must fit the configured total duration.");
        }

        public static ImoniEffectConfig FromJson(string gameConfigJson)
        {
            var root = JObject.Parse(gameConfigJson);
            return new ImoniEffectConfig((JObject)(root["imoniEffect"] ?? root));
        }

        private int[] ReadInts(string key) => Data[key]?.Values<int>().ToArray() ?? throw new ArgumentException("Missing effect array: " + key);
        private static double RequiredNumber(JObject source, string key)
        {
            var token = source[key] ?? throw new ArgumentException("Missing effect number: " + key);
            var value = token.Value<double>();
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentException("Effect number must be finite: " + key);
            return value;
        }
    }

    /// <summary>Park–Miller generator; independent of System.Random implementation and Unity.</summary>
    public sealed class ImoniEffectRandom
    {
        private long state;
        public ImoniEffectRandom(int seed) { state = ((seed - 1L) % 2147483646L + 2147483646L) % 2147483646L + 1; }
        public double Next() { state = state * 16807L % 2147483647L; return (state - 1.0) / 2147483646.0; }
        public double Range(double min, double max) => min + Next() * (max - min);
    }
}
