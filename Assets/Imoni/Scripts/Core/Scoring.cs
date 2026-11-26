using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Imoni.Core
{
    public sealed class RoundEvaluation
    {
        public bool Success { get; }
        public IReadOnlyList<string> MissingMaterialIds { get; }
        public IReadOnlyList<GoalProgress> RequiredGoals { get; }
        public GoalProgress BonusGoal { get; }
        public BigInteger RequiredPoints { get; }
        public BigInteger BonusPoints { get; }
        public BigInteger BonusMultiplier { get; }
        public BigInteger Score { get; }
        public BigInteger FundsReward { get; }
        public BigInteger RequiredPointsRaw { get; }
        public BigInteger BonusPointsRaw { get; }
        public BigInteger PointScale { get; }

        internal RoundEvaluation(GoalProgress[] required, GoalProgress bonus, BigInteger requiredPoints,
            BigInteger bonusPoints, BigInteger multiplier, BigInteger score, BigInteger fundsReward,
            BigInteger requiredPointsRaw, BigInteger bonusPointsRaw, BigInteger pointScale)
        {
            RequiredGoals = Array.AsReadOnly(required);
            BonusGoal = bonus;
            MissingMaterialIds = Array.AsReadOnly(required.Where(goal => !goal.IsMet).Select(goal => goal.MaterialId).ToArray());
            Success = MissingMaterialIds.Count == 0;
            RequiredPoints = requiredPoints;
            BonusPoints = bonusPoints;
            BonusMultiplier = multiplier;
            Score = score;
            FundsReward = Success ? fundsReward : BigInteger.Zero;
            RequiredPointsRaw = requiredPointsRaw; BonusPointsRaw = bonusPointsRaw; PointScale = pointScale;
        }
    }

    public static class Scoring
    {
        public static RoundEvaluation Evaluate(GameConfig config, Inventory inventory, RoundObjectives objectives)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (objectives == null) throw new ArgumentNullException(nameof(objectives));
            objectives.Validate(config);

            GoalProgress[] required = objectives.Required.Select(goal =>
                new GoalProgress(goal, inventory.QualityEquivalent(goal.MaterialId))).ToArray();
            var bonus = new GoalProgress(objectives.Bonus, inventory.QualityEquivalent(objectives.Bonus.MaterialId));
            BigInteger tier = BigInteger.Zero;
            // Compare rational achievement rates without rounding percentages near a tier boundary.
            foreach (BonusTier candidate in config.BonusTiers.OrderBy(candidate => candidate.MinPercent))
                if (bonus.Current * 100 >= bonus.Required * candidate.MinPercent) tier = candidate.Multiplier;

            BigInteger requiredRaw = objectives.Required.Aggregate(BigInteger.Zero,
                (sum, goal) => sum + inventory.PointsRaw(goal.MaterialId));
            BigInteger bonusRaw = inventory.PointsRaw(objectives.Bonus.MaterialId);
            BigInteger scale = config.FixedScale;
            // Config weights remain exact decimal ratios even when the lot precision is coarse.
            GameMath.DecimalRatio(config.RequiredWeight, out BigInteger requiredWeight, out BigInteger requiredDenominator);
            GameMath.DecimalRatio(config.BonusWeight, out BigInteger bonusWeight, out BigInteger bonusDenominator);
            BigInteger score = (requiredRaw * requiredWeight * bonusDenominator +
                bonusRaw * bonusWeight * tier * requiredDenominator) / (scale * requiredDenominator * bonusDenominator);
            GameMath.DecimalRatio(config.BonusFundsPerPoint, out BigInteger rewardWeight, out BigInteger rewardDenominator);
            BigInteger reward = bonusRaw * tier * rewardWeight / (scale * rewardDenominator);
            return new RoundEvaluation(required, bonus, requiredRaw / scale, bonusRaw / scale, tier, score, reward, requiredRaw, bonusRaw, scale);
        }
    }
}
