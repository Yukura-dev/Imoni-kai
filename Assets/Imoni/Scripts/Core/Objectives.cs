using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Imoni.Core
{
    public sealed class Objective
    {
        public string MaterialId { get; }
        public BigInteger Amount { get; }

        public Objective(string materialId, BigInteger amount)
        {
            if (string.IsNullOrWhiteSpace(materialId)) throw new ArgumentException("A material ID is required.", nameof(materialId));
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "An objective must require a positive quantity.");
            MaterialId = materialId;
            Amount = amount;
        }
    }

    /// <summary>A round's immutable goals. Generation from the reachable world is connected in M3.</summary>
    public sealed class RoundObjectives
    {
        public IReadOnlyList<Objective> Required { get; }
        public Objective Bonus { get; }

        public RoundObjectives(IEnumerable<Objective> required, Objective bonus)
        {
            if (required == null) throw new ArgumentNullException(nameof(required));
            Objective[] goals = required.ToArray();
            if (goals.Length == 0 || goals.Any(goal => goal == null))
                throw new ArgumentException("At least one non-null required objective is needed.", nameof(required));
            if (goals.Select(goal => goal.MaterialId).Distinct(StringComparer.Ordinal).Count() != goals.Length)
                throw new ArgumentException("Required objectives must have distinct materials.", nameof(required));
            Bonus = bonus ?? throw new ArgumentNullException(nameof(bonus));
            if (goals.Any(goal => goal.MaterialId == bonus.MaterialId))
                throw new ArgumentException("A material cannot be both required and bonus.", nameof(bonus));
            Required = Array.AsReadOnly(goals);
        }

        public void Validate(GameConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            foreach (Objective objective in Required) config.Material(objective.MaterialId);
            config.Material(Bonus.MaterialId);
        }
    }

    public sealed class GoalProgress
    {
        public string MaterialId { get; }
        public BigInteger Required { get; }
        public BigInteger Current { get; }
        public bool IsMet => Current >= Required;

        internal GoalProgress(Objective objective, BigInteger current)
        {
            MaterialId = objective.MaterialId;
            Required = objective.Amount;
            Current = current;
        }
    }
}
