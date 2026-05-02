using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using MyersBitParallel.Internal;

namespace MyersBitParallel;

/// <summary>
/// ASCII Levenshtein engine without the 64-character pattern length
/// limit. The current implementation uses a straightforward
/// dynamic-programming kernel; future versions may upgrade to a blocked
/// Myers bit-parallel kernel without changing the public API.
/// </summary>
public sealed class MyersBitParallelGeneralAscii
{
    public static readonly MyersBitParallelGeneralAscii CaseSensitive = new MyersBitParallelGeneralAscii(AsciiMappers.CaseSensitive);
    public static readonly MyersBitParallelGeneralAscii CaseInsensitive = new MyersBitParallelGeneralAscii(AsciiMappers.CaseInsensitive);

    private readonly byte[] _map;

    /// <summary>
    /// Construct an engine that builds its 256-entry lookup table by
    /// invoking <paramref name="charMapper"/> once per byte value.
    /// </summary>
    public MyersBitParallelGeneralAscii(Func<char, byte> charMapper)
    {
        if (charMapper == null) throw new ArgumentNullException(nameof(charMapper));
        _map = new byte[256];
        for (int i = 0; i < 256; i++)
            _map[i] = charMapper((char)i);
    }

    /// <summary>
    /// Build a reusable <see cref="MyersPatternGeneralAscii"/> handle for
    /// <paramref name="pattern"/>.
    /// </summary>
    public MyersPatternGeneralAscii Prepare(string pattern)
    {
        int m = pattern.Length;

        if (m == 0)
            return new MyersPatternGeneralAscii(null, 0);

        byte[] codes = ArrayPool<byte>.Shared.Rent(m);
        ref byte mapRef = ref ArrayHelpers.GetArrayDataReference(_map);
        for (int i = 0; i < m; i++)
            codes[i] = Unsafe.Add(ref mapRef, (byte)pattern[i]);

        return new MyersPatternGeneralAscii(codes, m);
    }

    /// <summary>
    /// Compute the Levenshtein distance between <paramref name="a"/> and
    /// <paramref name="b"/>, preparing and disposing a transient pattern
    /// for <paramref name="a"/>.
    /// </summary>
    public int Distance(string a, string b)
    {
        using MyersPatternGeneralAscii pat = Prepare(a);
        return Distance(in pat, b);
    }

    /// <summary>
    /// Compute the Levenshtein distance between an already-prepared
    /// <paramref name="pattern"/> and <paramref name="candidate"/>.
    /// </summary>
    public int Distance(in MyersPatternGeneralAscii pattern, string candidate)
    {
        int m = pattern.Length;
        int n = candidate.Length;
        if (m == 0) return n;
        if (n == 0) return m;

        byte[] aCodes = pattern.Codes!;
        byte[] bCodes = ArrayPool<byte>.Shared.Rent(n);
        int[] prev = ArrayPool<int>.Shared.Rent(n + 1);
        int[] curr = ArrayPool<int>.Shared.Rent(n + 1);
        try
        {
            ref byte mapRef = ref ArrayHelpers.GetArrayDataReference(_map);
            for (int j = 0; j < n; j++)
                bCodes[j] = Unsafe.Add(ref mapRef, (byte)candidate[j]);

            for (int j = 0; j <= n; j++)
                prev[j] = j;

            for (int i = 1; i <= m; i++)
            {
                curr[0] = i;
                byte ai = aCodes[i - 1];
                for (int j = 1; j <= n; j++)
                {
                    int cost = ai == bCodes[j - 1] ? 0 : 1;
                    int del = prev[j] + 1;
                    int ins = curr[j - 1] + 1;
                    int sub = prev[j - 1] + cost;
                    int min = del < ins ? del : ins;
                    if (sub < min) min = sub;
                    curr[j] = min;
                }
                int[] tmp = prev; prev = curr; curr = tmp;
            }

            return prev[n];
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(bCodes, clearArray: false);
            ArrayPool<int>.Shared.Return(prev, clearArray: false);
            ArrayPool<int>.Shared.Return(curr, clearArray: false);
        }
    }

    /// <summary>
    /// Compute distance and similarity ratio between <paramref name="a"/>
    /// and <paramref name="b"/>, preparing and disposing a transient
    /// pattern for <paramref name="a"/>.
    /// </summary>
    public SimilarityRatio SimilarityRatio(string a, string b)
    {
        using MyersPatternGeneralAscii pat = Prepare(a);
        int distance = Distance(in pat, b);
        return BuildRatio(distance, a.Length, b.Length);
    }

    /// <summary>
    /// Compute distance and similarity ratio between an already-prepared
    /// <paramref name="pattern"/> and <paramref name="candidate"/>.
    /// </summary>
    public SimilarityRatio SimilarityRatio(in MyersPatternGeneralAscii pattern, string candidate)
    {
        int distance = Distance(in pattern, candidate);
        return BuildRatio(distance, pattern.Length, candidate.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SimilarityRatio BuildRatio(int distance, int aLen, int bLen)
    {
        int maxLen = aLen >= bLen ? aLen : bLen;
        double ratio = maxLen == 0 ? 1.0 : 1.0 - ((double)distance / maxLen);
        return new SimilarityRatio(distance, ratio);
    }
}

/// <summary>
/// Reusable, prepared pattern handle produced by
/// <see cref="MyersBitParallelGeneralAscii.Prepare(string)"/>. Holds a
/// <see cref="ArrayPool{T}"/>-rented byte buffer of mapped code points.
/// </summary>
public readonly struct MyersPatternGeneralAscii : IDisposable
{
    /// <summary>
    /// Mapped pattern symbols in source order: <c>Codes[i]</c> is the encoded
    /// symbol at pattern position <c>i</c>. Used directly by the DP kernel.
    /// </summary>
    internal readonly byte[]? Codes;

    /// <summary>
    /// Length of the pattern in <see cref="char"/> units.
    /// </summary>
    public readonly int Length;

    internal MyersPatternGeneralAscii(byte[]? codes, int length)
    {
        Codes = codes;
        Length = length;
    }

    /// <summary>
    /// Return the rented code buffer to the shared pool. Calling this
    /// more than once on the same value returns the buffer twice and is
    /// the caller's responsibility to avoid.
    /// </summary>
    public void Dispose()
    {
        if (Codes != null)
        {
            ArrayPool<byte>.Shared.Return(Codes, clearArray: false);
        }
    }
}
