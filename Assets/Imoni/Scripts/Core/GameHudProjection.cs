using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace Imoni.Core
{
    /// <summary>Read-only HUD values. Quality conversion and bonus thresholds stay out of Unity views.</summary>
    public sealed class GameHudView
    {
        public string RoundText { get; internal set; }
        public string TimerText { get; internal set; }
        public bool TimerUrgent { get; internal set; }
        public IReadOnlyList<MaterialGoalView> Required { get; internal set; }
        public bool BonusRevealed { get; internal set; }
        public string BonusText { get; internal set; }
        public string BonusStatusText { get; internal set; }
        public string StreakText { get; internal set; }
        public string StreakMultiplierText { get; internal set; }
        public BigInteger InventoryQuantity { get; internal set; }
    }

    public static class GameHudProjection
    {
        public static GameHudView Present(GameConfig config, GameSnapshot snapshot, string actorId, decimal urgentSeconds)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (urgentSeconds < 0) throw new ArgumentOutOfRangeException(nameof(urgentSeconds));
            PlayerSnapshot actor = snapshot.Players.FirstOrDefault(player => player.Id == actorId)
                ?? throw new ArgumentException("Unknown actor.", nameof(actorId));
            bool ended = snapshot.Phase != RoundPhase.Collecting;
            RoundResult result = ended ? snapshot.LastResult : null;
            decimal remaining = ended ? 0 : Math.Max(0, decimal.Ceiling(config.DurationSeconds - snapshot.ElapsedSeconds));
            var view = new GameHudView
            {
                RoundText = "ラウンド " + (result?.RoundNumber ?? snapshot.RoundNumber) + "/" + config.RoundsToClear,
                TimerText = Clock(remaining), TimerUrgent = remaining <= urgentSeconds,
                Required = result == null ? Array.AsReadOnly(snapshot.RequiredObjectives.Select(goal =>
                    DisplayProjection.Goal(config, goal.MaterialId, Equivalent(config, snapshot.Lots, goal.MaterialId), goal.Amount)).ToArray())
                    : Array.AsReadOnly(result.Evaluation.RequiredGoals.Select(goal =>
                        DisplayProjection.Goal(config, goal.MaterialId, goal.Current, goal.Required)).ToArray()),
                BonusRevealed = result != null || (snapshot.BonusRevealed && snapshot.BonusObjective != null),
                BonusText = "あと " + Clock(decimal.Ceiling(config.BonusRevealSeconds - snapshot.ElapsedSeconds)) + " で発表!",
                BonusStatusText = string.Empty,
                StreakText = actor.Streak.ToString(CultureInfo.InvariantCulture),
                StreakMultiplierText = "×" + FixedMultiplier(new StreakCalc(config).MultiplierRaw(actor.Streak), config),
                InventoryQuantity = snapshot.Lots.Aggregate(BigInteger.Zero, (sum, lot) => sum + lot.Quantity)
            };
            if (view.BonusRevealed)
            {
                GoalProgress finalBonus = result?.Evaluation.BonusGoal;
                Objective bonus = finalBonus == null ? snapshot.BonusObjective : new Objective(finalBonus.MaterialId, finalBonus.Required);
                BigInteger current = finalBonus?.Current ?? Equivalent(config, snapshot.Lots, bonus.MaterialId);
                BonusTier highest = config.BonusTiers.LastOrDefault(tier => current * 100 >= bonus.Amount * tier.MinPercent);
                view.BonusText = config.Material(bonus.MaterialId).Name + " " + Format.Big(current, config) + "/" + Format.Big(bonus.Amount, config);
                view.BonusStatusText = DisplayProjection.Achievement(config, current, bonus.Amount, "達成率 ")
                    + " 報酬×" + Format.Big(highest?.Multiplier ?? BigInteger.Zero, config);
            }
            return view;
        }

        public static BigInteger Equivalent(GameConfig config, IEnumerable<MaterialLot> lots, string materialId) =>
            lots.Where(lot => lot.MaterialId == materialId)
                .Aggregate(BigInteger.Zero, (sum, lot) => sum + lot.Quantity * config.Quality(lot.QualityId).Multiplier);

        public static string GatherHistory(GameConfig config, OperationRecord operation, string actorName)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (operation.Kind != OperationKind.Gather) throw new ArgumentException("A gather operation is required.", nameof(operation));
            return "[" + Clock(operation.ElapsedSeconds) + "] " + actorName + "が" + config.Material(operation.MaterialId).Name
                + "×" + Format.Big(operation.Quantity, config) + "(" + config.Quality(operation.QualityId).Name + ")を採集";
        }

        public static string Clock(decimal seconds)
        {
            BigInteger whole = new BigInteger(Math.Max(0, decimal.Floor(seconds)));
            return (whole / 60).ToString(CultureInfo.InvariantCulture) + ":" + ((int)(whole % 60)).ToString("00", CultureInfo.InvariantCulture);
        }

        private static string FixedMultiplier(BigInteger raw, GameConfig config)
        {
            // Decimal notation constants, with precision shared with the configured number formatter.
            int places = config.FormatDecimalPlaces;
            BigInteger factor = BigInteger.Pow(10, places);
            BigInteger rounded = (raw * factor + config.FixedScale / 2) / config.FixedScale;
            return (rounded / factor).ToString(CultureInfo.InvariantCulture) + (places == 0 ? string.Empty
                : "." + (rounded % factor).ToString(CultureInfo.InvariantCulture).PadLeft(places, '0'));
        }
    }
}
