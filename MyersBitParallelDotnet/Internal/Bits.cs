using System.Runtime.CompilerServices;

#if NET5_0_OR_GREATER
using System.Numerics;
#endif

namespace MyersBitParallel.Internal;

/// <summary>
/// Bit-twiddling helpers that prefer hardware-accelerated intrinsics on
/// modern runtimes and fall back to a portable software implementation
/// on <c>netstandard2.1</c>.
/// </summary>
internal static class Bits
{
    /// <summary>
    /// Number of set bits in <paramref name="value"/>. Uses
    /// <see cref="System.Numerics.BitOperations.PopCount(ulong)"/> on
    /// runtimes that expose it; otherwise computes via the standard SWAR
    /// reduction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int PopCount(ulong value)
    {
#if NET5_0_OR_GREATER
        return BitOperations.PopCount(value);
#else
        // Hacker's Delight popcount64 (no intrinsics required).
        value -= (value >> 1) & 0x5555555555555555UL;
        value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
        value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
        return (int)((value * 0x0101010101010101UL) >> 56);
#endif
    }
}
