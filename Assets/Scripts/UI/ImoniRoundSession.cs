using System;
using UnityEngine;

namespace Imoni.UI
{
    /// <summary>One local challenge. Result presentation never grants rewards a second time.</summary>
    public static class ImoniRoundSession
    {
        public const int InitialFunds = 1000;
        public const int MaximumRounds = 5;
        public const int BonusRequirement = 30;

        // Provisional economy: the mock supplies the score formula, but not R/B conversion,
        // reward base, or rank cutoffs. Reuse current sale values; 160% gives 600 * 2 = 1200G.
        public const decimal MaterialScoreWeight = .5m;
        public const decimal BonusScoreWeight = .5m;
        public const int BaseSuccessReward = 600;
        private static readonly int[] MaterialPrices = { 2, 10, 6, 30 };
        private static readonly int[] QualityValues = { 1, 2, 5 };
        private static readonly int[] Requirements = { 60, 20, 15 };

        public sealed class RoundStartState
        {
            private readonly int[] upgradeLevels;
            public int RoundNumber { get; }
            public int Funds { get; }
            public int[] UpgradeLevels => (int[])upgradeLevels.Clone();
            internal RoundStartState(int roundNumber, int funds, int[] levels)
            {
                RoundNumber = roundNumber;
                Funds = funds;
                upgradeLevels = CopyLevels(levels);
            }
        }

        public sealed class RoundResultSnapshot
        {
            private readonly int[] convertedAmounts, upgradeLevels;
            internal readonly RoundStartState StartState;
            public int RoundNumber { get; }
            public bool Success { get; }
            public bool IsFinalRound => RoundNumber >= MaximumRounds;
            public long MaterialPoints { get; }
            public long BonusPoints { get; }
            public int BonusPercent { get; }
            public int BonusMultiplier { get; }
            public long RoundScore { get; }
            public long TotalScore { get; }
            public int RewardFunds { get; }
            public int FundsAfterReward { get; }
            public int[] ConvertedAmounts => (int[])convertedAmounts.Clone();
            public int[] RequiredAmounts => (int[])Requirements.Clone();
            public int[] UpgradeLevels => (int[])upgradeLevels.Clone();
            // Provisional rank tiers follow the bonus tiers shown in the mock (160% = A).
            public string Rank => !Success ? "—" : BonusMultiplier >= 8 ? "SSS" :
                BonusMultiplier >= 4 ? "S" : BonusMultiplier >= 2 ? "A" : BonusMultiplier >= 1 ? "B" : "C";

            internal RoundResultSnapshot(RoundStartState start, int[] quantities, int funds, int[] levels, long previousScore)
            {
                StartState = start;
                RoundNumber = start.RoundNumber;
                upgradeLevels = CopyLevels(levels);
                convertedAmounts = new int[4];
                long materialPoints = 0, bonusPoints = 0;
                for (int material = 0; material < 4; material++)
                {
                    long converted = 0;
                    for (int quality = 0; quality < 3; quality++)
                    {
                        int index = material * 3 + quality;
                        long quantity = quantities != null && index < quantities.Length ? Math.Max(0, quantities[index]) : 0;
                        long value = quantity * QualityValues[quality];
                        converted += value;
                        materialPoints += value * MaterialPrices[material];
                        if (material == 3) bonusPoints += value * MaterialPrices[material];
                    }
                    convertedAmounts[material] = (int)Math.Min(int.MaxValue, converted);
                }
                Success = convertedAmounts[0] >= Requirements[0] && convertedAmounts[1] >= Requirements[1] && convertedAmounts[2] >= Requirements[2];
                MaterialPoints = materialPoints;
                BonusPoints = bonusPoints;
                BonusPercent = (int)Math.Min(int.MaxValue, Math.Floor(convertedAmounts[3] * 100d / BonusRequirement + .5d));
                BonusMultiplier = GetBonusMultiplier(BonusPercent);
                RoundScore = (long)Math.Round(MaterialPoints * MaterialScoreWeight + BonusPoints * BonusMultiplier * BonusScoreWeight, MidpointRounding.AwayFromZero);
                TotalScore = previousScore + RoundScore;
                RewardFunds = Success ? BaseSuccessReward * BonusMultiplier : 0;
                FundsAfterReward = (int)Math.Min(int.MaxValue, Math.Max(0L, funds) + RewardFunds);
            }
        }

        private static RoundStartState pendingRound, activeRound;
        private static RoundResultSnapshot lastResult;
        private static long totalScore;

        public static RoundResultSnapshot LastResult => lastResult;
        public static long TotalScore => totalScore;
        public static int PendingRoundNumber => pendingRound?.RoundNumber ?? 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            pendingRound = activeRound = null;
            lastResult = null;
            totalScore = 0;
        }

        public static void BeginNewChallenge()
        {
            Reset();
            pendingRound = new RoundStartState(1, InitialFunds, new int[4]);
        }

        public static RoundStartState ConsumeRoundStart()
        {
            // Opening Game directly must not silently reuse an old round/result.
            if (pendingRound == null) BeginNewChallenge();
            activeRound = pendingRound;
            pendingRound = null;
            lastResult = null;
            return activeRound;
        }

        public static RoundResultSnapshot CompleteRound(RoundStartState start, int[] quantities, int funds, int[] upgradeLevels)
        {
            if (lastResult != null && ReferenceEquals(lastResult.StartState, start)) return lastResult;
            if (start == null || !ReferenceEquals(activeRound, start))
                throw new InvalidOperationException("This round is no longer the active challenge round.");
            lastResult = new RoundResultSnapshot(start, quantities, funds, upgradeLevels, totalScore);
            totalScore = lastResult.TotalScore;
            return lastResult;
        }

        public static bool PrepareNextRound()
        {
            if (pendingRound != null || lastResult == null || !lastResult.Success || lastResult.IsFinalRound) return false;
            pendingRound = new RoundStartState(lastResult.RoundNumber + 1, lastResult.FundsAfterReward, lastResult.UpgradeLevels);
            activeRound = null;
            return true;
        }

        public static void PrepareRetry() => BeginNewChallenge();

        private static int[] CopyLevels(int[] levels)
        {
            var copy = new int[4];
            if (levels != null)
                for (int i = 0; i < Math.Min(copy.Length, levels.Length); i++) copy[i] = Math.Max(0, levels[i]);
            return copy;
        }

        private static int GetBonusMultiplier(int percent) => percent >= 300 ? 8 : percent >= 200 ? 4 : percent >= 150 ? 2 : percent >= 100 ? 1 : 0;
    }
}
