using System;
using System.Buffers;
using System.Runtime.CompilerServices;

#if NET5_0_OR_GREATER
using System.Text;
#endif

namespace MyersBitParallel;

/// <summary>
/// Unicode Levenshtein engine without the 64-character pattern length
/// limit. Operates on Unicode scalar values (code points) and uses a
/// dynamic-programming kernel; future versions may upgrade to a blocked
/// Myers bit-parallel kernel without changing the public API.
/// </summary>
public sealed class MyersBitParallelGeneralUnicode
{
    public static readonly MyersBitParallelGeneralUnicode CaseSensitive = new MyersBitParallelGeneralUnicode(UnicodeMappers.Identity);
    public static readonly MyersBitParallelGeneralUnicode CaseInsensitive = new MyersBitParallelGeneralUnicode(UnicodeMappers.SimpleInvariantCaseFold);

#if NET5_0_OR_GREATER
    private readonly Func<Rune, Rune> _runeMapper;

    /// <summary>
    /// Construct an engine that normalizes every rune through
    /// <paramref name="runeMapper"/> before comparing.
    /// </summary>
    public MyersBitParallelGeneralUnicode(Func<Rune, Rune> runeMapper)
    {
        _runeMapper = runeMapper ?? throw new ArgumentNullException(nameof(runeMapper));
    }
#else
    private readonly Func<int, int> _codePointMapper;

    /// <summary>
    /// Construct an engine that normalizes every code point through
    /// <paramref name="codePointMapper"/> before comparing.
    /// </summary>
    public MyersBitParallelGeneralUnicode(Func<int, int> codePointMapper)
    {
        _codePointMapper = codePointMapper ?? throw new ArgumentNullException(nameof(codePointMapper));
    }
#endif

    /// <summary>
    /// Build a reusable <see cref="MyersPatternGeneralUnicode"/> handle
    /// for <paramref name="pattern"/>.
    /// </summary>
    public MyersPatternGeneralUnicode Prepare(string pattern)
    {
        if (pattern.Length == 0)
            return new MyersPatternGeneralUnicode(null, 0);

        int[] codes = ToMappedCodePoints(pattern, out int count);
        return new MyersPatternGeneralUnicode(codes, count);
    }

    /// <summary>
    /// Compute the Levenshtein distance between <paramref name="a"/> and
    /// <paramref name="b"/>, preparing and disposing a transient pattern
    /// for <paramref name="a"/>.
    /// </summary>
    public int Distance(string a, string b)
    {
        using MyersPatternGeneralUnicode pat = Prepare(a);
        return Distance(in pat, b);
    }

    /// <summary>
    /// Compute the Levenshtein distance between an already-prepared
    /// <paramref name="pattern"/> and <paramref name="candidate"/>.
    /// </summary>
    public int Distance(in MyersPatternGeneralUnicode pattern, string candidate)
    {
        if (candidate.Length == 0)
            return pattern.Length;

        int[] bCodes = ToMappedCodePoints(candidate, out int n);
        try
        {
            return DistanceCore(in pattern, bCodes, n);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(bCodes, clearArray: false);
        }
    }

    /// <summary>
    /// Compute distance and similarity ratio between <paramref name="a"/>
    /// and <paramref name="b"/>, preparing and disposing a transient
    /// pattern for <paramref name="a"/>.
    /// </summary>
    public SimilarityRatio SimilarityRatio(string a, string b)
    {
        using MyersPatternGeneralUnicode pat = Prepare(a);
        return SimilarityRatio(in pat, b);
    }

    /// <summary>
    /// Compute distance and similarity ratio between an already-prepared
    /// <paramref name="pattern"/> and <paramref name="candidate"/>.
    /// </summary>
    public SimilarityRatio SimilarityRatio(in MyersPatternGeneralUnicode pattern, string candidate)
    {
        if (candidate.Length == 0)
        {
            int distance = pattern.Length;
            int maxLen = pattern.Length;
            double ratio = maxLen == 0 ? 1.0 : 0.0;
            return new SimilarityRatio(distance, ratio);
        }

        int[] bCodes = ToMappedCodePoints(candidate, out int n);
        try
        {
            int distance = DistanceCore(in pattern, bCodes, n);
            int maxLen = pattern.Length >= n ? pattern.Length : n;
            double ratio = maxLen == 0 ? 1.0 : 1.0 - ((double)distance / maxLen);
            return new SimilarityRatio(distance, ratio);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(bCodes, clearArray: false);
        }
    }

    private static int DistanceCore(in MyersPatternGeneralUnicode p, int[] bCodes, int n)
    {
        int m = p.Length;
        if (m == 0) return n;

        int[] aCodes = p.Codes!;

        int[] prev = ArrayPool<int>.Shared.Rent(n + 1);
        int[] curr = ArrayPool<int>.Shared.Rent(n + 1);
        try
        {
            for (int j = 0; j <= n; j++)
                prev[j] = j;

            for (int i = 1; i <= m; i++)
            {
                curr[0] = i;
                int ai = aCodes[i - 1];
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
            ArrayPool<int>.Shared.Return(prev, clearArray: false);
            ArrayPool<int>.Shared.Return(curr, clearArray: false);
        }
    }

    private int[] ToMappedCodePoints(string s, out int count)
    {
        int[] buffer = ArrayPool<int>.Shared.Rent(s.Length);
        int n = 0;

#if NET5_0_OR_GREATER
        foreach (Rune r in s.EnumerateRunes())
        {
            buffer[n++] = _runeMapper(r).Value;
        }
#else
        int i = 0;
        while (i < s.Length)
        {
            int cp = ReadCodePoint(s, ref i);
            buffer[n++] = _codePointMapper(cp);
        }
#endif

        count = n;
        return buffer;
    }

#if !NET5_0_OR_GREATER
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadCodePoint(string s, ref int index)
    {
        char c = s[index];
        if (char.IsHighSurrogate(c) && index + 1 < s.Length)
        {
            char c2 = s[index + 1];
            if (char.IsLowSurrogate(c2))
            {
                index += 2;
                return char.ConvertToUtf32(c, c2);
            }
        }
        index++;
        return c;
    }
#endif
}

/// <summary>
/// Reusable, prepared pattern handle produced by
/// <see cref="MyersBitParallelGeneralUnicode.Prepare(string)"/>. Holds an
/// <see cref="ArrayPool{T}"/>-rented buffer of mapped code points.
/// </summary>
public readonly struct MyersPatternGeneralUnicode : IDisposable
{
    /// <summary>
    /// Mapped pattern code points in source order: <c>Codes[i]</c> is the
    /// encoded scalar value at pattern position <c>i</c>. Used directly by
    /// the DP kernel.
    /// </summary>
    internal readonly int[]? Codes;

    /// <summary>
    /// Length of the pattern in Unicode scalar values.
    /// </summary>
    public readonly int Length;

    internal MyersPatternGeneralUnicode(int[]? codes, int length)
    {
        Codes = codes;
        Length = length;
    }

    /// <summary>
    /// Return the rented code-point buffer to the shared pool. Calling
    /// this more than once on the same value returns the buffer twice and
    /// is the caller's responsibility to avoid.
    /// </summary>
    public void Dispose()
    {
        if (Codes != null)
        {
            ArrayPool<int>.Shared.Return(Codes, clearArray: false);
        }
    }
}
