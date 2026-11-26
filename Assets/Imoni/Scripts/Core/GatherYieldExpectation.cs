using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Imoni.Core
{
    /// <summary>Finite-horizon quality-equivalent yield. Probabilities use the same quality weights
    /// and gather floors as play; rounding transfers tiny streak tails to zero, never inventing a jackpot.</summary>
    public sealed class GatherYieldExpectation
    {
        private readonly GameConfig config;
        private readonly QualityRoller roller;
        private readonly StreakCalc quantities;
        private readonly int round, amountLevel, luckLevel;
        private List<BigInteger> streakProbabilities;
        public BigInteger TotalRaw { get; private set; }
        public int GatherCount { get; private set; }

        public GatherYieldExpectation(GameConfig config, int round, int amountLevel, int luckLevel)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            if (round < 1 || amountLevel < 0 || luckLevel < 0) throw new ArgumentOutOfRangeException(nameof(round));
            this.round = round; this.amountLevel = amountLevel; this.luckLevel = luckLevel;
            roller = new QualityRoller(config); quantities = new StreakCalc(config);
            streakProbabilities = new List<BigInteger> { config.FixedScale };
        }

        public BigInteger Add(ResourceNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            QualityDistribution distribution = roller.Distribution(round, luckLevel, node.BiomeId, node.MaterialId, node.QualityModifier);
            int top = distribution.Qualities.Count - 1;
            BigInteger topWeight = distribution.Weights[top], topMultiplier = distribution.Qualities[top].Multiplier;
            BigInteger baseYield = config.Material(node.MaterialId).BaseYield;
            BigInteger lowerWeighted = Enumerable.Range(0, top).Aggregate(BigInteger.Zero,
                (sum, index) => sum + distribution.Weights[index] * distribution.Qualities[index].Multiplier);
            BigInteger gained = quantities.GatherQuantity(baseYield, amountLevel, 0) * lowerWeighted * config.FixedScale / distribution.TotalWeight;
            var next = new List<BigInteger> { BigInteger.Zero };
            BigInteger topProbability = 0;
            for (int streak = 0; streak < streakProbabilities.Count; streak++)
            {
                BigInteger probability = streakProbabilities[streak] * topWeight / distribution.TotalWeight;
                next.Add(probability); topProbability += probability;
                if (probability > 0) gained += probability * quantities.GatherQuantity(baseYield, amountLevel, streak + 1) * topMultiplier;
            }
            next[0] = config.FixedScale - topProbability;
            while (next.Count > 1 && next[next.Count - 1].IsZero) next.RemoveAt(next.Count - 1);
            streakProbabilities = next; TotalRaw += gained; GatherCount++;
            return gained;
        }
    }
}
