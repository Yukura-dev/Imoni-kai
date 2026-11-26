using System;
using System.Numerics;

namespace Imoni.Core
{
    public sealed class StreakCalc
    {
        private readonly GameConfig config;
        public StreakCalc(GameConfig config) { this.config = config ?? throw new ArgumentNullException(nameof(config)); }

        public BigInteger MultiplierRaw(int streak)
        {
            if (streak < 0) throw new ArgumentOutOfRangeException(nameof(streak));
            // floor(scale * base^(streak/divisor)), including fractional exponents, using integers only.
            BigInteger powered = BigInteger.Pow(config.StreakBase, streak) * BigInteger.Pow(config.FixedScale, config.StreakDivisor);
            return GameMath.IntegerRoot(powered, config.StreakDivisor);
        }

        public BigInteger GatherQuantity(BigInteger baseYield, int amountLevel, int streak)
        {
            if (baseYield <= 0 || amountLevel < 0) throw new ArgumentOutOfRangeException(nameof(baseYield));
            BigInteger amountRaw = new UpgradeRules(config).MultiplierRaw("amount", amountLevel);
            return baseYield * amountRaw * MultiplierRaw(streak) / (config.FixedScale * config.FixedScale);
        }
    }
}
