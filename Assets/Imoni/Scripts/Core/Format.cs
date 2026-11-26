using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Imoni.Core
{
    public static class Format
    {
        public static string Big(BigInteger value, GameConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            string sign = value.Sign < 0 ? "-" : string.Empty;
            value = BigInteger.Abs(value);
            BigInteger divisor = BigInteger.One;
            int unit = 0;
            while (unit + 1 < config.FormatUnits.Count && value >= divisor * config.FormatUnitBase)
            { divisor *= config.FormatUnitBase; unit++; }
            if (unit == 0) return sign + value.ToString("N0", CultureInfo.InvariantCulture);
            var text = new StringBuilder(sign);
            // Once a unit is used, omit values below the first named unit (万 by default).
            int lastUnit = Math.Max(1, unit - config.FormatMaxDisplayGroups + 1);
            for (; unit >= lastUnit; unit--)
            {
                BigInteger group = BigInteger.DivRem(value, divisor, out value);
                if (!group.IsZero) text.Append(group.ToString("N0", CultureInfo.InvariantCulture)).Append(config.FormatUnits[unit]);
                divisor /= config.FormatUnitBase;
            }
            return text.ToString();
        }
    }
}
