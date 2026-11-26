using System;
using System.Linq;

namespace Imoni.Core.Effects
{
    /// <summary>Conservative simultaneous-object bounds derived from the configured spawn cadence.</summary>
    public sealed class ImoniEffectPoolCapacity
    {
        public int Ingredients { get; }
        public int Bursts { get; }
        public int Rings { get; }
        public int StarsPerBurst { get; }

        private ImoniEffectPoolCapacity(int ingredients, int bursts, int rings, int stars)
        { Ingredients = ingredients; Bursts = bursts; Rings = rings; StarsPerBurst = stars; }

        public static ImoniEffectPoolCapacity From(ImoniEffectConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            double minimumInterval = config.SpawnIntervals.Min() * config.V("spawnJitterMin");
            if (minimumInterval <= 0) throw new ArgumentException("A positive minimum spawn interval is required.", nameof(config));
            int batch = config.V("doubleSpawnChance") > 0 ? 2 : 1;
            int ingredients = batch * ((int)Math.Ceiling(config.Falls.Max() / minimumInterval) + 1);
            // Different fall speeds can bunch together hits from a wider spawn window.
            double hitWindow = config.V("smallBurstMs") + config.Falls.Max() - config.Falls.Min();
            int smallBursts = batch * ((int)Math.Ceiling(hitWindow / minimumInterval) + 1);
            int rings = config.ThresholdRatios.Length + 1; // All level changes plus the success finale.
            int stars = Math.Max((int)config.V("bigStarCount"),
                ImoniEffectMath.SmallStarCount(config.ThresholdRatios.Length, config));
            return new ImoniEffectPoolCapacity(ingredients, smallBursts + rings, rings, stars);
        }
    }
}
