using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MyersBitParallel.Internal;

namespace MyersBitParallel;

/// <summary>
/// Single-word Myers bit-parallel engine for the minimum Levenshtein
/// distance between a short pattern (up to <see cref="MaxPatternLength"/>
/// symbols) and any contiguous substring of a longer text.
/// Uses the same 256-entry <see cref="char"/>-to-<see cref="byte"/> lookup
/// table as <see cref="MyersBitParallel64"/>; the mapper is applied at
/// construction only.
/// </summary>
/// <remarks>
/// Full-string <see cref="MyersBitParallel64"/> aligns pattern and text
/// end-to-end. This type scans the text once; use
/// <see cref="BestMatchDistance(string, string)"/> (or the overload that takes
/// a prepared pattern) to obtain the lowest edit distance to any substring
/// of the haystack. The <see cref="CaseSensitive"/> and
/// <see cref="CaseInsensitive"/> statics mirror
/// <see cref="MyersBitParallel64.AsciiCaseSensitive"/> and
/// <see cref="MyersBitParallel64.AsciiCaseInsensitive"/>.
/// </remarks>
public sealed class MyersSubstringBitParallel64
{
    /// <summary>
    /// Pre-built engine using <see cref="AsciiMappers.CaseInsensitive"/>:
    /// ASCII <c>A</c>–<c>Z</c> folded to <c>a</c>–<c>z</c>; everything else
    /// preserved verbatim.
    /// </summary>
    public static MyersSubstringBitParallel64 CaseInsensitive = new MyersSubstringBitParallel64(AsciiMappers.CaseInsensitive);

    /// <summary>
    /// Pre-built engine using <see cref="AsciiMappers.CaseSensitive"/>:
    /// every byte value preserved verbatim, so case differences count.
    /// </summary>
    public static MyersSubstringBitParallel64 CaseSensitive = new MyersSubstringBitParallel64(AsciiMappers.CaseSensitive);

    /// <summary>
    /// Maximum number of pattern characters that can fit into the single
    /// <see cref="ulong"/> bit-vector used by this engine (same limit as
    /// <see cref="MyersBitParallel64.MaxPatternLength"/>).
    /// </summary>
    public const int MaxPatternLength = MyersBitParallel64.MaxPatternLength;

    private readonly byte[] _map;

    /// <summary>
    /// Construct an engine with a prebuilt 256-entry lookup table.
    /// </summary>
    public MyersSubstringBitParallel64(byte[] map)
    {
        if (map.Length != 256)
        {
            throw new ArgumentException("Map must contain 256 entries", nameof(map));
        }

        _map = map;
    }

    /// <summary>
    /// Construct an engine that builds its 256-entry lookup table by
    /// invoking <paramref name="charMapper"/> once per byte value.
    /// </summary>
    public MyersSubstringBitParallel64(Func<char, byte> charMapper)
    {
        if (charMapper == null) throw new ArgumentNullException(nameof(charMapper));

        _map = new byte[256];
        for (int i = 0; i < 256; i++)
            _map[i] = charMapper((char)i);
    }

    /// <summary>
    /// Build a reusable <see cref="MyersSubstringPattern64"/> handle for
    /// <paramref name="query"/>. The caller owns the returned struct and
    /// must <see cref="MyersSubstringPattern64.Dispose"/> it when finished.
    /// </summary>
    public MyersSubstringPattern64 Prepare(string query)
    {
        int m = query.Length;
        if (m > MaxPatternLength)
        {
            throw new ArgumentException(
                $"Query length {m} exceeds the {MaxPatternLength}-symbol limit of {nameof(MyersSubstringBitParallel64)}.",
                nameof(query));
        }

        if (m == 0)
        {
            return new MyersSubstringPattern64(null, 0, 0UL, 0UL);
        }

        // 256 slots so any byte value the user-supplied mapper might emit
        // can be indexed directly without further bookkeeping.
        ulong[] masks = ArrayPool<ulong>.Shared.Rent(256);
        Array.Clear(masks, 0, 256);

        ref byte mapRef = ref ArrayHelpers.GetArrayDataReference(_map);
        for (int i = 0; i < m; i++)
        {
            byte idx = Unsafe.Add(ref mapRef, (byte)query[i]);
            masks[idx] |= 1UL << i;
        }

        // Avoid (1UL << 64) which is undefined in C#; saturate at MaxValue.
        ulong maskAll = m == 64 ? ulong.MaxValue : (1UL << m) - 1UL;
        ulong lastBitMask = 1UL << (m - 1);

        return new MyersSubstringPattern64(masks, m, lastBitMask, maskAll);
    }

    /// <summary>
    /// Returns the Levenshtein distance of the best match: the minimum edit
    /// distance between <paramref name="pattern"/> and any contiguous
    /// substring of <paramref name="text"/>, preparing and disposing a
    /// transient prepared query.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <paramref name="text"/> is empty the result is
    /// <c>pattern.Length</c>; when <paramref name="pattern"/> is empty the
    /// result is <c>0</c> (the empty substring always matches).
    /// </para>
    /// <para>
    /// When <paramref name="pattern"/> is longer than <paramref name="text"/>,
    /// the method does not throw: the minimum is still taken over every
    /// substring of the text (including the full text), so the result is
    /// bounded below by <c>pattern.Length - text.Length</c>. This is useful
    /// for ranking short candidates against a longer query.
    /// </para>
    /// </remarks>
    public int BestMatchDistance(string pattern, string text)
    {
        using MyersSubstringPattern64 pat = Prepare(pattern);
        return BestMatchDistance(in pat, text);
    }

    /// <summary>
    /// Returns the Levenshtein distance of the best match: the minimum edit
    /// distance between an already-prepared <paramref name="pattern"/> and
    /// any contiguous substring of <paramref name="text"/>.
    /// </summary>
    /// <remarks>
    /// See <see cref="BestMatchDistance(string, string)"/> for the semantics
    /// when either input is empty, or when the prepared pattern is longer
    /// than <paramref name="text"/>.
    /// </remarks>
    public int BestMatchDistance(in MyersSubstringPattern64 pattern, string text)
    {
        int m = pattern.Length;
        int n = text.Length;

        if (m == 0) return 0;
        if (n == 0) return m;

        return Kernel(in pattern, text);
    }

    private int Kernel(in MyersSubstringPattern64 pattern, string text)
    {
        int m = pattern.Length;

        ulong maskAll = pattern.MaskAll;
        ulong lastBitMask = pattern.LastBitMask;

        ulong VP = maskAll;
        ulong VN = 0UL;

        int score = m;
        int best = m;

        ref ulong patternRef = ref ArrayHelpers.GetArrayDataReference(pattern.Masks!);
        ref byte mapRef = ref ArrayHelpers.GetArrayDataReference(_map);
        ref char textRef = ref MemoryMarshal.GetReference(text.AsSpan());

        int n = text.Length;

        for (int i = 0; i < n; i++)
        {
            char c = Unsafe.Add(ref textRef, i);
            byte idx = Unsafe.Add(ref mapRef, (byte)c);
            ulong PM = Unsafe.Add(ref patternRef, idx);

            ulong X = PM | VN;
            ulong D0 = (((X & VP) + VP) ^ VP) | X;
            ulong HN = VP & D0;
            ulong HP = VN | (~(VP | D0) & maskAll);

            // Substring mode: DP[0][j] = 0 for every text position, so the
            // bit shifted into the bottom (row-0 horizontal delta) is 0.
            // Global-alignment kernels OR in 1 here to encode DP[0][j] = j.
            X = HP << 1;
            VN = X & D0;
            VP = (HN << 1) | (~(X | D0) & maskAll);

            if ((HP & lastBitMask) != 0)
            {
                score++;
            }
            else if ((HN & lastBitMask) != 0)
            {
                score--;
            }

            // track best anywhere
            if (score < best)
            {
                best = score;
            }
        }

        return best;
    }
}

/// <summary>
/// Reusable, prepared query handle produced by
/// <see cref="MyersSubstringBitParallel64.Prepare(string)"/>. Holds an
/// <see cref="ArrayPool{T}"/>-rented bit-mask table that callers must
/// release via <see cref="Dispose"/>.
/// </summary>
public readonly struct MyersSubstringPattern64 : IDisposable
{
    internal readonly ulong[]? Masks;
    internal readonly ulong LastBitMask;
    internal readonly ulong MaskAll;

    /// <summary>
    /// Length of the pattern in <see cref="char"/> units, in the range
    /// <c>[0, <see cref="MyersBitParallel64.MaxPatternLength"/>]</c>.
    /// </summary>
    public readonly int Length;

    internal MyersSubstringPattern64(
        ulong[]? masks,
        int length,
        ulong lastBitMask,
        ulong maskAll)
    {
        Masks = masks;
        Length = length;
        LastBitMask = lastBitMask;
        MaskAll = maskAll;
    }

    /// <summary>
    /// Return the rented bit-mask buffer to the shared pool. Calling this
    /// more than once on the same value returns the buffer twice and is
    /// the caller's responsibility to avoid.
    /// </summary>
    public void Dispose()
    {
        if (Masks != null)
        {
            ArrayPool<ulong>.Shared.Return(Masks, clearArray: false);
        }
    }
}