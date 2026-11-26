using System;
using System.Numerics;

namespace Imoni.Core
{
    /// <summary>Exact integer arithmetic. Constants here describe numeric representations, not game balance.</summary>
    public static class GameMath
    {
        public static void DecimalRatio(decimal value, out BigInteger numerator, out BigInteger denominator)
        {
            int[] bits = decimal.GetBits(value);
            numerator = (BigInteger)(uint)bits[0] | ((BigInteger)(uint)bits[1] << 32) | ((BigInteger)(uint)bits[2] << 64);
            if ((bits[3] & int.MinValue) != 0) numerator = -numerator;
            denominator = BigInteger.Pow(10, (bits[3] >> 16) & 255);
            BigInteger divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            numerator /= divisor;
            denominator /= divisor;
        }

        public static BigInteger DivideFloor(BigInteger numerator, BigInteger denominator)
        {
            if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
            BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
            return numerator.Sign < 0 && !remainder.IsZero ? quotient - 1 : quotient;
        }

        public static BigInteger MultiplyFloor(BigInteger amount, decimal multiplier)
        {
            DecimalRatio(multiplier, out BigInteger numerator, out BigInteger denominator);
            return DivideFloor(amount * numerator, denominator);
        }

        public static BigInteger ToScaled(decimal value, BigInteger scale)
        {
            if (scale <= 0) throw new ArgumentOutOfRangeException(nameof(scale));
            return MultiplyFloor(scale, value);
        }

        public static BigInteger DivideRoundHalfUp(BigInteger numerator, BigInteger denominator)
        {
            if (numerator < 0 || denominator <= 0) throw new ArgumentOutOfRangeException(nameof(numerator));
            BigInteger quotient = BigInteger.DivRem(numerator, denominator, out BigInteger remainder);
            return quotient + (remainder * 2 >= denominator ? BigInteger.One : BigInteger.Zero);
        }

        public static BigInteger IntegerRoot(BigInteger value, int degree)
        {
            if (value < 0 || degree < 1) throw new ArgumentOutOfRangeException(nameof(value));
            if (value <= 1 || degree == 1) return value;
            // A power of two above the root bounds Newton's iteration without a double conversion.
            byte[] bytes = value.ToByteArray();
            int length = bytes.Length;
            while (length > 1 && bytes[length - 1] == 0) length--;
            int bits = (length - 1) * 8;
            for (int top = bytes[length - 1]; top > 0; top >>= 1) bits++;
            BigInteger current = BigInteger.One << ((bits - 1) / degree + 1);
            while (true)
            {
                BigInteger next = ((degree - 1) * current + value / BigInteger.Pow(current, degree - 1)) / degree;
                if (next >= current) return current;
                current = next;
            }
        }
    }
}
