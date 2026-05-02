using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MyersBitParallel.Internal;

internal static class ArrayHelpers
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T GetArrayDataReference<T>(T[] array)
    {
#if NET5_0_OR_GREATER
        return ref MemoryMarshal.GetArrayDataReference(array);
#else
        return ref MemoryMarshal.GetReference(array.AsSpan());
#endif
    }
}
