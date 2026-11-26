using System;
using System.Globalization;

namespace Imoni.Core
{
    /// <summary>Formats a committed gather using its own round and streak, never the actor's later state.</summary>
    public static class GatherPopupProjection
    {
        public static string Text(GameConfig config, OperationRecord operation)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (operation.Kind != OperationKind.Gather) throw new ArgumentException("A gather operation is required.", nameof(operation));
            QualityDefinition quality = config.Quality(operation.QualityId);
            string text = "+" + Format.Big(operation.Quantity, config) + " " + config.Material(operation.MaterialId).Name + "(" + quality.Name + ")";
            if (operation.GatherStreak > 0 && new QualityRoller(config).IsHighest(quality, operation.RoundNumber))
                text += " 連続" + operation.GatherStreak.ToString(CultureInfo.InvariantCulture);
            return text;
        }
    }
}
