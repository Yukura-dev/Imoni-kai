using System;
using System.Numerics;

namespace Imoni.Core
{
    /// <summary>Shared fixed-precision durations for goal planning and the authoritative simulation.</summary>
    public static class GatheringTiming
    {
        public static decimal MoveStepSeconds(GameConfig config, int level)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            BigInteger distanceRaw = GameMath.ToScaled(config.World.TileSizePx, config.FixedScale);
            BigInteger speedRaw = new UpgradeRules(config).MoveSpeedRaw(level);
            BigInteger raw = (distanceRaw * config.FixedScale + speedRaw - BigInteger.One) / speedRaw;
            return (decimal)BigInteger.Max(BigInteger.One, raw) / (decimal)config.FixedScale;
        }

        public static decimal CollectSeconds(GameConfig config, int level, ResourceNode node)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (node == null) throw new ArgumentNullException(nameof(node));
            decimal efficiency = config.Biome(node.BiomeId).EfficiencyMultiplier *
                config.Material(node.MaterialId).EfficiencyMultiplier * node.EfficiencyModifier;
            BigInteger efficiencyRaw = BigInteger.Max(BigInteger.One, GameMath.ToScaled(efficiency, config.FixedScale));
            BigInteger raw = new UpgradeRules(config).CollectDurationRaw(level) * config.FixedScale / efficiencyRaw;
            return (decimal)BigInteger.Max(BigInteger.One, raw) / (decimal)config.FixedScale;
        }
    }
}
