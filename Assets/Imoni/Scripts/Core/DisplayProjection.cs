using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace Imoni.Core
{
    /// <summary>Immutable presentation data; UI never compares inventory against an objective.</summary>
    public sealed class MaterialGoalView
    {
        public string MaterialId { get; }
        public string Name { get; }
        public string CurrentText { get; }
        public string RequiredText { get; }
        public decimal Progress { get; }
        public bool Achieved { get; }
        internal MaterialGoalView(string id, string name, string current, string required, decimal progress, bool achieved)
        { MaterialId = id; Name = name; CurrentText = current; RequiredText = required; Progress = progress; Achieved = achieved; }
    }

    public sealed class BonusTierView
    {
        public string ThresholdText { get; }
        public string MultiplierText { get; }
        public bool Reached { get; }
        public bool Current { get; }
        internal BonusTierView(string threshold, string multiplier, bool reached, bool current)
        { ThresholdText = threshold; MultiplierText = multiplier; Reached = reached; Current = current; }
    }

    public static class DisplayProjection
    {
        public static MaterialGoalView Goal(GameConfig config, string materialId, BigInteger current, BigInteger required)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            ValidateAmounts(current, required);
            // Clamp while still in integer space; even 10^1000 quantities never become floating point.
            BigInteger precision = BigInteger.Min(config.FixedScale, new BigInteger(decimal.MaxValue));
            decimal progress = (decimal)(BigInteger.Min(current, required) * precision / required) / (decimal)precision;
            return new MaterialGoalView(materialId, config.Material(materialId).Name,
                Format.Big(current, config), Format.Big(required, config), progress, current >= required);
        }

        public static IReadOnlyList<BonusTierView> BonusTiers(GameConfig config, BigInteger current, BigInteger required)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            ValidateAmounts(current, required);
            // 100 is the definition of percent, not a balance/tuning value.
            BonusTier highest = config.BonusTiers.LastOrDefault(tier => current * 100 >= required * tier.MinPercent);
            return config.BonusTiers.Select(tier => new BonusTierView(
                Achievement(config, tier.MinPercent, 100), "×" + Format.Big(tier.Multiplier, config),
                current * 100 >= required * tier.MinPercent, ReferenceEquals(tier, highest))).ToArray();
        }

        /// <summary>Large achievement ratios use an integer multiple, without float overflow or decimals.</summary>
        public static string Achievement(GameConfig config, BigInteger current, BigInteger required, string percentPrefix = "")
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            ValidateAmounts(current, required);
            return current * 100 >= required * config.AchievementMultipleThresholdPercent
                ? "目標の" + Format.Big(current / required, config) + "倍"
                : percentPrefix + Format.Big(current * 100 / required, config) + "%";
        }

        /// <summary>Integer-safe visual interpolation only; never mutates game state.</summary>
        public static BigInteger Interpolate(BigInteger from, BigInteger to, decimal progress)
        {
            if (progress < 0 || progress > 1) throw new ArgumentOutOfRangeException(nameof(progress));
            return from + GameMath.MultiplyFloor(to - from, progress);
        }

        private static void ValidateAmounts(BigInteger current, BigInteger required)
        {
            if (current < 0) throw new ArgumentOutOfRangeException(nameof(current));
            if (required <= 0) throw new ArgumentOutOfRangeException(nameof(required));
        }
    }
}
