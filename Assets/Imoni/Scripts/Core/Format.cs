using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Imoni.Core
{
    public static class Format
    {
        public static string Big(BigInteger value, GameConfig config)
            => Groups(value, config, config?.FormatMaxDisplayGroups ?? 0);

        /// <summary>One highest unit for compact HUD values. The authoritative integer is unchanged.</summary>
        public static string Compact(BigInteger value, GameConfig config) => Groups(value, config, 1);

        /// <summary>Unabbreviated integer for details and tooltips.</summary>
        public static string Exact(BigInteger value) => value.ToString("N0", CultureInfo.InvariantCulture);

        private static string Groups(BigInteger value, GameConfig config, int displayGroups)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            string sign = value.Sign < 0 ? "-" : string.Empty;
            value = BigInteger.Abs(value);
            BigInteger divisor = BigInteger.One;
            int highestUnit = 0;
            while (highestUnit + 1 < config.FormatUnits.Count && value >= divisor * config.FormatUnitBase)
            { divisor *= config.FormatUnitBase; highestUnit++; }
            if (highestUnit == 0) return sign + Exact(value);
            var text = new StringBuilder(sign);
            // Once a unit is used, omit values below the first named unit (万 by default).
            // At the final configured unit, allow an arbitrarily large integer coefficient:
            // never repeat that unit for the remainder or wrap back to a smaller magnitude.
            int lastUnit = Math.Max(1, highestUnit - displayGroups + 1);
            BigInteger remainder = value;
            for (int unit = highestUnit; unit >= lastUnit; unit--)
            {
                BigInteger group = BigInteger.DivRem(remainder, divisor, out remainder);
                if (!group.IsZero) text.Append(Exact(group)).Append(config.FormatUnits[unit]);
                divisor /= config.FormatUnitBase;
            }
            return text.ToString();
        }
    }
}
