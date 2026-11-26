using System;
using System.Globalization;
using System.Numerics;

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
            BigInteger whole = BigInteger.DivRem(value, divisor, out BigInteger remainder);
            string fraction = string.Empty;
            if (config.FormatDecimalPlaces > 0)
            {
                BigInteger digits = remainder * BigInteger.Pow(10, config.FormatDecimalPlaces) / divisor;
                string text = digits.ToString(CultureInfo.InvariantCulture).PadLeft(config.FormatDecimalPlaces, '0').TrimEnd('0');
                if (text.Length > 0) fraction = "." + text;
            }
            return sign + whole.ToString(CultureInfo.InvariantCulture) + fraction + config.FormatUnits[unit];
        }
    }
}
