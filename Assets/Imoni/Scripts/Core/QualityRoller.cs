using System;
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
    }
}
