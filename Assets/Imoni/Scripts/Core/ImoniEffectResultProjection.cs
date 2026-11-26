using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Imoni.Core.Effects
{
    public sealed class ImoniEffectResult
    {
        public BigInteger Score { get; internal set; }
        public BigInteger SuccessReferenceScore { get; internal set; }
        public bool Failed { get; internal set; }
        public IReadOnlyList<string> MissingMaterialNames { get; internal set; }
        public string MissingMaterials => string.Join("・", MissingMaterialNames);
        public string ResultRoute { get; internal set; }
        public int Seed { get; internal set; }
    }

    /// <summary>Projects only a settled result. The reference score never changes the awarded score.</summary>
    public static class ImoniEffectResultProjection
    {
        public static ImoniEffectResult Present(GameConfig config, RoundResult result)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (result == null) throw new ArgumentNullException(nameof(result));
            RoundEvaluation evaluation = result.Evaluation;
            BigInteger requiredRaw = evaluation.RequiredPointsRaw;
            // A failed game's success reference fills only missing required equivalents at normal
            // quality with no streak/upgrade bonus. Existing acquired and bonus points stay intact.
            foreach (GoalProgress goal in evaluation.RequiredGoals)
                requiredRaw += BigInteger.Max(0, goal.Required - goal.Current)
                    * config.Material(goal.MaterialId).BasePoint * evaluation.PointScale;
            GameMath.DecimalRatio(config.RequiredWeight, out BigInteger rw, out BigInteger rd);
            GameMath.DecimalRatio(config.BonusWeight, out BigInteger bw, out BigInteger bd);
            BigInteger reference = (requiredRaw * rw * bd + evaluation.BonusPointsRaw * bw * evaluation.BonusMultiplier * rd)
                / (evaluation.PointScale * rd * bd);
            return new ImoniEffectResult
            {
                Score = evaluation.Score, Failed = !evaluation.Success,
                SuccessReferenceScore = BigInteger.Max(BigInteger.One, BigInteger.Max(evaluation.Score, reference)),
                MissingMaterialNames = Array.AsReadOnly(evaluation.MissingMaterialIds.Select(id => config.Material(id).Name).ToArray()),
                ResultRoute = !evaluation.Success ? "GameOver" : result.RoundNumber >= config.RoundsToClear ? "Clear" : "Result",
                Seed = result.Seed
            };
        }
    }
}
