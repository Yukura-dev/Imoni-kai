using System;
using System.Numerics;

namespace Imoni.Core
{
    public sealed class UpgradeRules
    {
        private readonly GameConfig config;
        public UpgradeRules(GameConfig config) { this.config = config ?? throw new ArgumentNullException(nameof(config)); }

        public BigInteger Price(string upgradeId, int currentLevel)
        {
            if (currentLevel < 0) throw new ArgumentOutOfRangeException(nameof(currentLevel));
            UpgradeDefinition upgrade = config.Upgrade(upgradeId);
            GameMath.DecimalRatio(config.UpgradePriceGrowth, out BigInteger numerator, out BigInteger denominator);
            return GameMath.DivideRoundHalfUp(upgrade.BasePrice * BigInteger.Pow(numerator, currentLevel),
                BigInteger.Pow(denominator, currentLevel));
        }

        public BigInteger MultiplierRaw(string upgradeId, int level)
        {
            if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
            GameMath.DecimalRatio(config.Upgrade(upgradeId).EffectPerLevel, out BigInteger numerator, out BigInteger denominator);
            return config.FixedScale + config.FixedScale * numerator * level / denominator;
        }

        public BigInteger MoveSpeedRaw(int level) =>
            GameMath.MultiplyFloor(MultiplierRaw("move", level), config.BaseMoveSpeed);

        // An arbitrarily high speed still needs a positive representable duration.
        public BigInteger CollectDurationRaw(int level) => BigInteger.Max(BigInteger.One,
            GameMath.ToScaled(config.BaseCollectSeconds, config.FixedScale) * config.FixedScale / MultiplierRaw("speed", level));
    }
}
