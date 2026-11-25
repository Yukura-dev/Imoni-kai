using System;
using System.Numerics;

namespace Imoni.Core
{
    /// <summary>SplitMix64 with rejection sampling; stable across .NET and Unity runtimes.</summary>
    public sealed class Rng
    {
        private ulong state;
        public Rng(int seed) { state = unchecked((ulong)(long)seed); }

        public ulong NextUInt64()
        {
            unchecked
            {
                ulong value = (state += 0x9E3779B97F4A7C15UL);
                value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
                value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
                return value ^ (value >> 31);
            }
        }

        public BigInteger NextBelow(BigInteger exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            if (exclusiveMaximum == 1) return BigInteger.Zero;
            byte[] bound = (exclusiveMaximum - 1).ToByteArray();
            int length = bound.Length;
            while (length > 1 && bound[length - 1] == 0) length--;
            byte mask = bound[length - 1];
            mask |= (byte)(mask >> 1);
            mask |= (byte)(mask >> 2);
            mask |= (byte)(mask >> 4);
            var bytes = new byte[length + 1]; // extra zero byte makes BigInteger positive
            while (true)
            {
                ulong word = 0;
                for (int index = 0; index < length; index++)
                {
                    if (index % 8 == 0) word = NextUInt64();
                    bytes[index] = (byte)word;
                    word >>= 8;
                }
                bytes[length - 1] &= mask;
                BigInteger result = new BigInteger(bytes);
                if (result < exclusiveMaximum) return result;
            }
        }
    }
}
