using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace Imoni.Core
{
    public sealed class EarnedTitlePart
    {
        public string Part { get; }
        public string Value { get; }
        internal EarnedTitlePart(string part, string value) { Part = part; Value = value; }
    }

    public sealed class RoundResultView
    {
        public bool Success { get; internal set; }
        public bool IsClear { get; internal set; }
        public bool CanNextRound { get; internal set; }
        public bool CanRetry { get; internal set; }
        public int RoundNumber { get; internal set; }
        public string RoundText { get; internal set; }
        public string Rank { get; internal set; }
        public IReadOnlyList<MaterialGoalView> Required { get; internal set; }
        public MaterialGoalView BonusGoal { get; internal set; }
        public IReadOnlyList<BonusTierView> BonusTiers { get; internal set; }
        public string BonusText { get; internal set; }
        public string BonusStatusText { get; internal set; }
        public string ScoreText { get; internal set; }
        public string FundsRewardText { get; internal set; }
        public string CumulativeScoreText { get; internal set; }
        public string RequiredPointsText { get; internal set; }
        public string RequiredWeightedText { get; internal set; }
        public string BonusPointsText { get; internal set; }
        public string BonusWeightedText { get; internal set; }
        public string BonusMultiplierText { get; internal set; }
        public string BreakdownTitle { get; internal set; }
        public IReadOnlyList<string> MissingMaterialNames { get; internal set; }
        public string MissingMaterialText { get; internal set; }
        public string NextButtonText { get; internal set; }
        public IReadOnlyList<EarnedTitlePart> NewlyEligibleTitles { get; internal set; }
        public string NewTitleText { get; internal set; }
        public string TitleRewardText { get; internal set; }
    }

    /// <summary>Uses the detached deadline result, never a cleared inventory or reset failed-run snapshot.</summary>
    public static class RoundResultProjection
    {
        public static RoundResultView Present(GameConfig config, RoundResult result, string actorId = "player1",
            IEnumerable<string> ownedFirst = null, IEnumerable<string> ownedSecond = null, IReadOnlyList<EarnedTitlePart> progressionTitles = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (result == null) throw new ArgumentNullException(nameof(result));
            RoundEvaluation evaluation = result.Evaluation;
            bool clear = evaluation.Success && !result.IsEndless && result.RoundNumber >= config.RoundsToClear;
            GoalProgress bonus = evaluation.BonusGoal;
            GameMath.DecimalRatio(config.RequiredWeight, out BigInteger requiredWeight, out BigInteger requiredDenominator);
            GameMath.DecimalRatio(config.BonusWeight, out BigInteger bonusWeight, out BigInteger bonusDenominator);
            var missing = Array.AsReadOnly(evaluation.MissingMaterialIds.Select(id => config.Material(id).Name).ToArray());
            var titles = progressionTitles ?? EligibleTitles(config, result, actorId, ownedFirst, ownedSecond);
            string multiplier = "×" + Format.Big(evaluation.BonusMultiplier, config);
            return new RoundResultView
            {
                Success = evaluation.Success, IsClear = clear, CanNextRound = evaluation.Success && !clear, CanRetry = !evaluation.Success,
                RoundNumber = result.RoundNumber, RoundText = result.IsEndless ? "エンドレス R" + result.RoundNumber + " 結果" : "ラウンド " + result.RoundNumber + "/" + config.RoundsToClear + " 結果",
                Rank = evaluation.Success ? config.ResultRules.Grades.First(grade => bonus.Current * 100 >= bonus.Required * grade.MinimumBonusPercent).Grade : string.Empty,
                Required = Array.AsReadOnly(evaluation.RequiredGoals.Select(goal => DisplayProjection.Goal(config, goal.MaterialId, goal.Current, goal.Required)).ToArray()),
                BonusGoal = DisplayProjection.Goal(config, bonus.MaterialId, bonus.Current, bonus.Required),
                BonusTiers = DisplayProjection.BonusTiers(config, bonus.Current, bonus.Required),
                BonusText = config.Material(bonus.MaterialId).Name + " " + Format.Big(bonus.Current, config) + "/" + Format.Big(bonus.Required, config),
                BonusStatusText = DisplayProjection.Achievement(config, bonus.Current, bonus.Required) + " 報酬" + multiplier,
                ScoreText = Format.Big(evaluation.Score, config), FundsRewardText = "+" + Format.Big(evaluation.FundsReward, config) + config.CurrencyUnit,
                CumulativeScoreText = Format.Big(result.CumulativeScore, config),
                RequiredPointsText = RatioText(evaluation.RequiredPointsRaw, evaluation.PointScale, config),
                RequiredWeightedText = RatioText(evaluation.RequiredPointsRaw * requiredWeight, evaluation.PointScale * requiredDenominator, config),
                BonusPointsText = RatioText(evaluation.BonusPointsRaw, evaluation.PointScale, config),
                BonusWeightedText = RatioText(evaluation.BonusPointsRaw * bonusWeight * evaluation.BonusMultiplier, evaluation.PointScale * bonusDenominator, config),
                BonusMultiplierText = multiplier,
                BreakdownTitle = "スコア内訳(" + config.RequiredWeight.ToString(CultureInfo.InvariantCulture) + "×R + " + config.BonusWeight.ToString(CultureInfo.InvariantCulture) + "×B×T)",
                MissingMaterialNames = missing,
                MissingMaterialText = missing.Count == 0 ? string.Empty : "必須素材「" + string.Join("・", missing) + "」が足りなかった",
                NextButtonText = !evaluation.Success ? "R1から再挑戦" : clear ? "ホームへ戻る" : "ラウンド" + (result.RoundNumber + 1) + "へ",
                NewlyEligibleTitles = titles,
                NewTitleText = titles.Count == 0 ? string.Empty : string.Join("・", titles.Select(title => "「" + title.Value + "」GET!")),
                TitleRewardText = titles.Count > 0 ? "「" + titles[0].Value + "」GET!" + (titles.Count > 1 ? "\nほか" + Format.Big(titles.Count - 1, config) + "件" : string.Empty)
                    : EligibleTitles(config, result, actorId).Count > 0 ? "獲得済み" : "なし"
            };
        }

        public static IReadOnlyList<EarnedTitlePart> EligibleTitles(GameConfig config, RoundResult result, string actorId,
            IEnumerable<string> ownedFirst = null, IEnumerable<string> ownedSecond = null)
        {
            if (!result.Evaluation.Success) return Array.Empty<EarnedTitlePart>();
            var first = new HashSet<string>(ownedFirst ?? Array.Empty<string>(), StringComparer.Ordinal);
            var second = new HashSet<string>(ownedSecond ?? Array.Empty<string>(), StringComparer.Ordinal);
            int peak = result.History.Where(record => record.RoundNumber == result.RoundNumber && record.ActorId == actorId && record.Kind == OperationKind.Gather)
                .Select(record => record.GatherStreak).DefaultIfEmpty(0).Max();
            return Array.AsReadOnly(config.ResultRules.TitleUnlocks.Where(rule => result.RoundNumber >= rule.MinimumRound && peak >= rule.MinimumStreak
                && (!rule.RequiresClear || !result.IsEndless && result.RoundNumber >= config.RoundsToClear) && !(rule.Part == "first" ? first : second).Contains(rule.Value))
                .Select(rule => new EarnedTitlePart(rule.Part, rule.Value)).ToArray());
        }

        private static string RatioText(BigInteger numerator, BigInteger denominator, GameConfig config)
        {
            // The authoritative score retains raw lot precision. Only the displayed breakdown
            // is truncated, using exactly the same integer formatter as funds and quantities.
            return Format.Big(numerator / denominator, config);
        }
    }
}
