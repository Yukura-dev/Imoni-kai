using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Imoni.Core
{
    public sealed class QualityRoller
    {
        private readonly GameConfig config;
        public QualityRoller(GameConfig config) { this.config = config ?? throw new ArgumentNullException(nameof(config)); }

        public QualityDefinition Highest(int round)
        {
            if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
            return config.Qualities.Where(quality => quality.UnlockRound <= round)
                .OrderByDescending(quality => quality.Multiplier).First();
        }

        public bool IsHighest(QualityDefinition quality, int round) => quality != null && quality.Id == Highest(round).Id;

        public BigInteger TopChanceRaw(int luckLevel)
        {
            if (luckLevel < 0) throw new ArgumentOutOfRangeException(nameof(luckLevel));
            // Clamp only at mathematical 100%; there is no gameplay cap below it.
            GameMath.DecimalRatio(config.TopBaseChance, out BigInteger chanceNumerator, out BigInteger chanceDenominator);
            GameMath.DecimalRatio(config.LuckPerLevel, out BigInteger luckNumerator, out BigInteger luckDenominator);
            BigInteger raw = config.FixedScale * chanceNumerator * (luckDenominator + luckNumerator * luckLevel)
                / (chanceDenominator * luckDenominator);
            return BigInteger.Min(config.FixedScale, raw);
        }

        public QualityDefinition Roll(Rng rng, int round, int luckLevel)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            QualityDefinition top = Highest(round);
            BigInteger chance = TopChanceRaw(luckLevel);
            QualityDefinition[] lower = config.Qualities.Where(quality => quality.UnlockRound <= round && quality.Id != top.Id).ToArray();
            if (lower.Length == 0 || rng.NextBelow(config.FixedScale) < chance) return top;
            BigInteger[] weights = lower.Select(quality => GameMath.ToScaled(quality.Weight, config.FixedScale)).ToArray();
            BigInteger draw = rng.NextBelow(weights.Aggregate(BigInteger.Zero, (total, weight) => total + weight));
            for (int index = 0; index < lower.Length; index++)
            {
                if (draw < weights[index]) return lower[index];
                draw -= weights[index];
            }
            throw new InvalidOperationException("Quality weights did not cover their sampling range.");
        }

        /// <summary>
        /// The configured pTop/lower-weight distribution is the neutral starting point. A positive
        /// environment factor multiplies each quality's mass by factor^unlockedRank, then all masses are
        /// normalized by their sum. This favors higher qualities without introducing a probability cap.
        /// Node modifiers are additional to the material and biome tuning, never replacements for it.
        /// </summary>
        public QualityDefinition Roll(Rng rng, int round, int luckLevel, string biomeId, string materialId,
            decimal nodeQualityModifier = 1)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            QualityDistribution distribution = Distribution(round, luckLevel, biomeId, materialId, nodeQualityModifier);
            BigInteger draw = rng.NextBelow(distribution.TotalWeight);
            for (int index = 0; index < distribution.Qualities.Count; index++)
            {
                if (draw < distribution.Weights[index]) return distribution.Qualities[index];
                draw -= distribution.Weights[index];
            }
            throw new InvalidOperationException("Quality weights did not cover their sampling range.");
        }

        public QualityDistribution Distribution(int round, int luckLevel, string biomeId, string materialId,
            decimal nodeQualityModifier = 1)
        {
            if (nodeQualityModifier <= 0) throw new ArgumentOutOfRangeException(nameof(nodeQualityModifier));
            QualityDefinition top = Highest(round);
            BigInteger chance = TopChanceRaw(luckLevel);
            var qualities = config.Qualities.Where(quality => quality.UnlockRound <= round)
                .OrderBy(quality => quality.Multiplier).ToArray();
            BigInteger[] lower = qualities.Select(quality => quality.Id == top.Id ? BigInteger.Zero :
                GameMath.ToScaled(quality.Weight, config.FixedScale)).ToArray();
            BigInteger lowerTotal = lower.Aggregate(BigInteger.Zero, (a, b) => a + b);
            GameMath.DecimalRatio(config.Biome(biomeId).QualityMultiplier, out BigInteger numerator, out BigInteger denominator);
            GameMath.DecimalRatio(config.Material(materialId).QualityMultiplier, out BigInteger materialNumerator, out BigInteger materialDenominator);
            GameMath.DecimalRatio(nodeQualityModifier, out BigInteger nodeNumerator, out BigInteger nodeDenominator);
            numerator *= materialNumerator * nodeNumerator;
            denominator *= materialDenominator * nodeDenominator;
            var weights = new BigInteger[qualities.Length];
            for (int rank = 0; rank < qualities.Length; rank++)
            {
                BigInteger mass = qualities.Length == 1 ? BigInteger.One : qualities[rank].Id == top.Id
                    ? chance * lowerTotal : (config.FixedScale - chance) * lower[rank];
                weights[rank] = mass * BigInteger.Pow(numerator, rank) * BigInteger.Pow(denominator, qualities.Length - rank - 1);
            }
            return new QualityDistribution(qualities, weights);
        }
    }

    public sealed class QualityDistribution
    {
        public IReadOnlyList<QualityDefinition> Qualities { get; }
        public IReadOnlyList<BigInteger> Weights { get; }
        public BigInteger TotalWeight { get; }
        internal QualityDistribution(QualityDefinition[] qualities, BigInteger[] weights)
        {
            Qualities = Array.AsReadOnly(qualities);
            Weights = Array.AsReadOnly(weights);
            TotalWeight = weights.Aggregate(BigInteger.Zero, (a, b) => a + b);
        }
    }
}
