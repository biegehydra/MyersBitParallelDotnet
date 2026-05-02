using System.Runtime.CompilerServices;

#if NET5_0_OR_GREATER
using System.Numerics;
#endif

namespace MyersBitParallel.Internal
{
    internal static class Bits
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(ulong value)
        {
#if NET5_0_OR_GREATER
            return BitOperations.PopCount(value);
#else
            // SWAR popcount fallback for targets without BitOperations.
            const ulong c1 = 0x5555555555555555UL;
            const ulong c2 = 0x3333333333333333UL;
            const ulong c4 = 0x0F0F0F0F0F0F0F0FUL;
            const ulong cM = 0x0101010101010101UL;

            value -= (value >> 1) & c1;
            value = (value & c2) + ((value >> 2) & c2);
            value = (value + (value >> 4)) & c4;
            return (int)((value * cM) >> 56);
#endif
        }
    }
}
