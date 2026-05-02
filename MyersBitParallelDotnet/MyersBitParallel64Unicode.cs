using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#if NET5_0_OR_GREATER
using System.Text;
#endif

namespace MyersBitParallel;

/// <summary>
/// Single-word Myers bit-parallel Levenshtein engine that operates on
/// Unicode scalar values (code points) and supports patterns up to
/// <see cref="MaxPatternLength"/> scalars. On modern runtimes the engine
/// uses <see cref="System.Text.Rune"/> for iteration; on
/// <c>netstandard2.1</c> it performs manual UTF-16 decoding.
/// </summary>
public sealed class MyersBitParallel64Unicode
{
    public static readonly MyersBitParallel64Unicode CaseSensitive = new MyersBitParallel64Unicode(UnicodeMappers.Identity);
    public static readonly MyersBitParallel64Unicode CaseInsensitive = new MyersBitParallel64Unicode(UnicodeMappers.SimpleInvariantCaseFold);

    /// <summary>
    /// Maximum number of pattern Unicode scalar values that can fit into
    /// the single <see cref="ulong"/> bit-vector used by this engine.
    /// </summary>
    public const int MaxPatternLength = 64;

#if NET5_0_OR_GREATER
    private readonly Func<Rune, Rune> _runeMapper;

    /// <summary>
    /// Construct an engine that normalizes every rune through
    /// <paramref name="runeMapper"/> before bit-vector lookup.
    /// </summary>
    public MyersBitParallel64Unicode(Func<Rune, Rune> runeMapper)
    {
        _runeMapper = runeMapper ?? throw new ArgumentNullException(nameof(runeMapper));
    }
#else
    private readonly Func<int, int> _codePointMapper;

    /// <summary>
    /// Construct an engine that normalizes every code point through
    /// <paramref name="codePointMapper"/> before bit-vector lookup.
    /// </summary>
    public MyersBitParallel64Unicode(Func<int, int> codePointMapper)
    {
        _codePointMapper = codePointMapper ?? throw new ArgumentNullException(nameof(codePointMapper));
    }
#endif

    /// <summary>
    /// Build a reusable <see cref="MyersPattern64Unicode"/> handle for
    /// <paramref name="pattern"/>.
    /// </summary>
    public MyersPattern64Unicode Prepare(string pattern)
    {
        var masks = new Dictionary<int, ulong>();
        int m = 0;

#if NET5_0_OR_GREATER
        foreach (Rune r in pattern.EnumerateRunes())
        {
            if (m >= MaxPatternLength)
                throw new ArgumentException(
                    $"Pattern exceeds the {MaxPatternLength}-symbol limit of {nameof(MyersBitParallel64Unicode)}.",
                    nameof(pattern));
            int cp = _runeMapper(r).Value;
            masks.TryGetValue(cp, out ulong existing);
            masks[cp] = existing | (1UL << m);
            m++;
        }
#else
        int i = 0;
        while (i < pattern.Length)
        {
            if (m >= MaxPatternLength)
                throw new ArgumentException(
                    $"Pattern exceeds the {MaxPatternLength}-symbol limit of {nameof(MyersBitParallel64Unicode)}.",
                    nameof(pattern));
            int cp = ReadCodePoint(pattern, ref i);
            cp = _codePointMapper(cp);
            masks.TryGetValue(cp, out ulong existing);
            masks[cp] = existing | (1UL << m);
            m++;
        }
#endif

        ulong maskAll = m == 0
            ? 0UL
            : (m == 64 ? ulong.MaxValue : (1UL << m) - 1UL);
        ulong lastBitMask = m == 0 ? 0UL : 1UL << (m - 1);

        return new MyersPattern64Unicode(masks, m, lastBitMask, maskAll);
    }

    /// <summary>
    /// Compute the Levenshtein distance between <paramref name="a"/> and
    /// <paramref name="b"/>, preparing and disposing a transient pattern
    /// for <paramref name="a"/>.
    /// </summary>
    public int Distance(string a, string b)
    {
        using MyersPattern64Unicode pat = Prepare(a);
        return DistanceCore(in pat, b, out _);
    }

    /// <summary>
    /// Compute the Levenshtein distance between an already-prepared
    /// <paramref name="pattern"/> and <paramref name="candidate"/>.
    /// </summary>
    public int Distance(in MyersPattern64Unicode pattern, string candidate)
    {
        return DistanceCore(in pattern, candidate, out _);
    }

    /// <summary>
    /// Compute distance and similarity ratio between <paramref name="a"/>
    /// and <paramref name="b"/>, preparing and disposing a transient
    /// pattern for <paramref name="a"/>.
    /// </summary>
    public SimilarityRatio SimilarityRatio(string a, string b)
    {
        using MyersPattern64Unicode pat = Prepare(a);
        int distance = DistanceCore(in pat, b, out int candidateScalarCount);
        return BuildRatio(distance, pat.Length, candidateScalarCount);
    }

    /// <summary>
    /// Compute distance and similarity ratio between an already-prepared
    /// <paramref name="pattern"/> and <paramref name="candidate"/>.
    /// </summary>
    public SimilarityRatio SimilarityRatio(in MyersPattern64Unicode pattern, string candidate)
    {
        int distance = DistanceCore(in pattern, candidate, out int candidateScalarCount);
        return BuildRatio(distance, pattern.Length, candidateScalarCount);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SimilarityRatio BuildRatio(int distance, int aLen, int bLen)
    {
        int maxLen = aLen >= bLen ? aLen : bLen;
        double ratio = maxLen == 0 ? 1.0 : 1.0 - ((double)distance / maxLen);
        return new SimilarityRatio(distance, ratio);
    }

    private int DistanceCore(in MyersPattern64Unicode p, string candidate, out int candidateScalarCount)
    {
        int m = p.Length;
        ulong maskAll = p.MaskAll;
        ulong lastBitMask = p.LastBitMask;
        Dictionary<int, ulong> masks = p.Masks!;

        ulong VP = maskAll;
        ulong VN = 0UL;
        int score = m;
        int n = 0;

#if NET5_0_OR_GREATER
        foreach (Rune r in candidate.EnumerateRunes())
        {
            int cp = _runeMapper(r).Value;
            masks.TryGetValue(cp, out ulong PM);

            ulong X = PM | VN;
            ulong D0 = (((X & VP) + VP) ^ VP) | X;
            ulong HN = VP & D0;
            ulong HP = VN | (~(VP | D0) & maskAll);

            X = (HP << 1) | 1UL;
            VN = X & D0;
            VP = (HN << 1) | (~(X | D0) & maskAll);

            if ((HP & lastBitMask) != 0)
                score++;
            else if ((HN & lastBitMask) != 0)
                score--;

            n++;
        }
#else
        int i = 0;
        while (i < candidate.Length)
        {
            int cp = ReadCodePoint(candidate, ref i);
            cp = _codePointMapper(cp);
            masks.TryGetValue(cp, out ulong PM);

            ulong X = PM | VN;
            ulong D0 = (((X & VP) + VP) ^ VP) | X;
            ulong HN = VP & D0;
            ulong HP = VN | (~(VP | D0) & maskAll);

            X = (HP << 1) | 1UL;
            VN = X & D0;
            VP = (HN << 1) | (~(X | D0) & maskAll);

            if ((HP & lastBitMask) != 0)
                score++;
            else if ((HN & lastBitMask) != 0)
                score--;

            n++;
        }
#endif

        candidateScalarCount = n;
        if (m == 0) return n;
        return score;
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
/// <see cref="MyersBitParallel64Unicode.Prepare(string)"/>. Holds a
/// dictionary of bit masks keyed by mapped code point.
/// </summary>
public readonly struct MyersPattern64Unicode : IDisposable
{
    /// <summary>
    /// Bit-mask table keyed by mapped Unicode scalar value: <c>Masks[s]</c>
    /// is the bitmap of pattern positions at which scalar <c>s</c> appears.
    /// Backed by a dictionary because the code-point alphabet is too large
    /// to store as a flat array.
    /// </summary>
    internal readonly Dictionary<int, ulong>? Masks;
    internal readonly ulong LastBitMask;
    internal readonly ulong MaskAll;

    /// <summary>
    /// Length of the pattern in Unicode scalar values, in the range
    /// <c>[0, <see cref="MyersBitParallel64Unicode.MaxPatternLength"/>]</c>.
    /// </summary>
    public readonly int Length;

    internal MyersPattern64Unicode(Dictionary<int, ulong> masks, int length, ulong lastBitMask, ulong maskAll)
    {
        Masks = masks;
        Length = length;
        LastBitMask = lastBitMask;
        MaskAll = maskAll;
    }

    /// <summary>
    /// No-op for this engine; the underlying mask dictionary is reclaimed
    /// by the GC. Provided so this struct fits the standard
    /// <c>using</c>-disposable pattern.
    /// </summary>
    public void Dispose()
    {
    }
}
