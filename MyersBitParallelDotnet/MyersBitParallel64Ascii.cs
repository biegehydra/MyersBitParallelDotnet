using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MyersBitParallel.Internal;

namespace MyersBitParallel;

/// <summary>
/// Single-word Myers bit-parallel Levenshtein engine optimized for ASCII
/// patterns up to <see cref="MaxPatternLength"/> characters. The hot loop
/// reads from a constructor-built <c>byte[256]</c> mapping table and
/// never invokes a user-provided delegate.
/// </summary>
public sealed class MyersBitParallel64Ascii
{
    public static readonly MyersBitParallel64Ascii CaseSensitive = new MyersBitParallel64Ascii(AsciiMappers.CaseSensitive);
    public static readonly MyersBitParallel64Ascii CaseInsensitive = new MyersBitParallel64Ascii(AsciiMappers.CaseInsensitive);


    /// <summary>
    /// Maximum number of pattern characters that can fit into the single
    /// <see cref="ulong"/> bit-vector used by this engine.
    /// </summary>
    public const int MaxPatternLength = 64;

    private readonly byte[] _map;


    /// <summary>
    /// Construct an engine that builds its 256-entry lookup table by
    /// invoking <paramref name="charMapper"/> once per byte value.
    /// </summary>
    public MyersBitParallel64Ascii(Func<char, byte> charMapper)
    {
        if (charMapper == null) throw new ArgumentNullException(nameof(charMapper));
        _map = new byte[256];
        for (int i = 0; i < 256; i++)
            _map[i] = charMapper((char)i);
    }

    /// <summary>
    /// Build a reusable <see cref="MyersPattern64Ascii"/> handle for
    /// <paramref name="pattern"/>. The caller owns the returned struct and
    /// must <see cref="MyersPattern64Ascii.Dispose"/> it when finished.
    /// </summary>
    public MyersPattern64Ascii Prepare(string pattern)
    {
        int m = pattern.Length;
        if (m > MaxPatternLength)
            throw new ArgumentException(
                $"Pattern length {m} exceeds the {MaxPatternLength}-symbol limit of {nameof(MyersBitParallel64Ascii)}.",
                nameof(pattern));

        if (m == 0)
            return new MyersPattern64Ascii(null, 0, 0UL, 0UL, 0UL, 0);

        // 256 slots so any byte value the user-supplied mapper might emit
        // can be indexed directly without further bookkeeping.
        ulong[] masks = ArrayPool<ulong>.Shared.Rent(256);
        Array.Clear(masks, 0, 256);

        ulong charMask = 0UL;
        ref byte mapRef = ref ArrayHelpers.GetArrayDataReference(_map);
        for (int i = 0; i < m; i++)
        {
            byte idx = Unsafe.Add(ref mapRef, (byte)pattern[i]);
            masks[idx] |= 1UL << i;
            // Collapse the 8-bit mapped value to a 6-bit char-mask bit.
            // Different mapped values can collide here; the result is still
            // a correct (though possibly less-selective) pruning filter.
            charMask |= 1UL << (idx & 63);
        }

        // Avoid (1UL << 64) which is undefined in C#; saturate at MaxValue.
        ulong maskAll = m == 64 ? ulong.MaxValue : (1UL << m) - 1UL;
        ulong lastBitMask = 1UL << (m - 1);

        return new MyersPattern64Ascii(masks, m, lastBitMask, maskAll, charMask, Bits.PopCount(charMask));
    }

    /// <summary>
    /// Build the 64-bit character occurrence mask for <paramref name="s"/>
    /// using this engine's mapper. Suitable for passing to
    /// <see cref="Distance(in MyersPattern64Ascii, string, int, ulong)"/>
    /// as the <c>requiredCharMask</c> argument when filtering candidates
    /// that must contain every symbol in a reference string.
    /// </summary>
    /// <remarks>
    /// Mapped byte values are folded into a 6-bit slot via <c>value &amp; 63</c>,
    /// so distinct mapped bytes that share their low 6 bits collide. The
    /// resulting filter is conservative (it never falsely rejects a valid
    /// match) but may be less selective for mappers that emit values above 63.
    /// </remarks>
    public ulong BuildCharMask(string s)
    {
        if (s is null) throw new ArgumentNullException(nameof(s));
        return BuildCharMaskCore(s);
    }

    /// <summary>
    /// Compute the Levenshtein distance between <paramref name="a"/> and
    /// <paramref name="b"/>, preparing and disposing a transient pattern
    /// for <paramref name="a"/>. Returns <see cref="int.MaxValue"/> when
    /// the distance is known to exceed <paramref name="maxDist"/>.
    /// </summary>
    public int Distance(string a, string b, int maxDist = int.MaxValue, ulong requiredCharMask = 0UL)
    {
        using MyersPattern64Ascii pat = Prepare(a);
        return Distance(in pat, b, maxDist, requiredCharMask);
    }

    /// <summary>
    /// Compute the Levenshtein distance between an already-prepared
    /// <paramref name="pattern"/> and <paramref name="candidate"/>.
    /// Returns <see cref="int.MaxValue"/> when the distance is known to
    /// exceed <paramref name="maxDist"/>, optionally short-circuiting via
    /// the <paramref name="requiredCharMask"/> filter.
    /// </summary>
    public int Distance(
        in MyersPattern64Ascii pattern,
        string candidate,
        int maxDist = int.MaxValue,
        ulong requiredCharMask = 0UL)
    {
        int m = pattern.Length;
        int n = candidate.Length;

        // Length difference is a hard lower bound on edit distance.
        int lenDiff = m > n ? m - n : n - m;
        if (lenDiff > maxDist) return int.MaxValue;

        if (m == 0) return n;
        if (n == 0) return m;

        // Optional pre-pass: build the candidate's char mask to prune
        // candidates that either omit a required symbol or overlap the
        // pattern's alphabet by too few bits to recover within maxDist.
        if (requiredCharMask != 0UL || maxDist != int.MaxValue)
        {
            ulong candMask = BuildCharMaskCore(candidate);

            if ((candMask & requiredCharMask) != requiredCharMask)
                return int.MaxValue;

            int overlap = Bits.PopCount(candMask & pattern.CharMask);
            if (pattern.UniqueCharCount - overlap > maxDist)
                return int.MaxValue;
        }

        return BoundedKernel(in pattern, candidate, maxDist);
    }

    /// <summary>
    /// Compute distance and similarity ratio between <paramref name="a"/>
    /// and <paramref name="b"/>, preparing and disposing a transient
    /// pattern for <paramref name="a"/>.
    /// </summary>
    public SimilarityRatio SimilarityRatio(string a, string b)
    {
        using MyersPattern64Ascii pat = Prepare(a);
        int distance = Distance(in pat, b);
        return BuildRatio(distance, a.Length, b.Length);
    }

    /// <summary>
    /// Compute distance and similarity ratio between an already-prepared
    /// <paramref name="pattern"/> and <paramref name="candidate"/>.
    /// </summary>
    public SimilarityRatio SimilarityRatio(in MyersPattern64Ascii pattern, string candidate)
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

    private ulong BuildCharMaskCore(string s)
    {
        int n = s.Length;
        if (n == 0) return 0UL;

        ulong mask = 0UL;
        ref byte mapRef = ref ArrayHelpers.GetArrayDataReference(_map);
        ref char sRef = ref MemoryMarshal.GetReference(s.AsSpan());
        for (int i = 0; i < n; i++)
        {
            byte idx = Unsafe.Add(ref mapRef, (byte)Unsafe.Add(ref sRef, i));
            mask |= 1UL << (idx & 63);
        }
        return mask;
    }

    private int BoundedKernel(in MyersPattern64Ascii pattern, string candidate, int maxDist)
    {
        int m = pattern.Length;
        int n = candidate.Length;

        ulong maskAll = pattern.MaskAll;
        ulong lastBitMask = pattern.LastBitMask;
        ulong VP = maskAll;
        ulong VN = 0UL;
        int score = m;

        ref ulong patternRef = ref ArrayHelpers.GetArrayDataReference(pattern.Masks!);
        ref byte mapRef = ref ArrayHelpers.GetArrayDataReference(_map);
        ref char candRef = ref MemoryMarshal.GetReference(candidate.AsSpan());

        for (int i = 0; i < n; i++)
        {
            char c = Unsafe.Add(ref candRef, i);
            byte idx = Unsafe.Add(ref mapRef, (byte)c);
            ulong PM = Unsafe.Add(ref patternRef, idx);

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

            // Even if every remaining char decreases the score by 1, can
            // we still finish at <= maxDist? If not, the answer is "too far".
            int remaining = n - i - 1;
            if (score - remaining > maxDist)
                return int.MaxValue;
        }

        return score <= maxDist ? score : int.MaxValue;
    }
}

/// <summary>
/// Reusable, prepared pattern handle produced by
/// <see cref="MyersBitParallel64Ascii.Prepare(string)"/>. Holds an
/// <see cref="ArrayPool{T}"/>-rented bit-mask table that callers must
/// release via <see cref="Dispose"/>.
/// </summary>
public readonly struct MyersPattern64Ascii : IDisposable
{
    /// <summary>
    /// Bit-mask table indexed by mapped symbol value: <c>Masks[s]</c> is the
    /// bitmap of pattern positions at which symbol <c>s</c> appears. Sized
    /// to 256 entries so any byte from the engine's lookup table indexes it
    /// directly.
    /// </summary>
    internal readonly ulong[]? Masks;
    internal readonly ulong LastBitMask;
    internal readonly ulong MaskAll;

    /// <summary>
    /// Length of the pattern in <see cref="char"/> units, in the range
    /// <c>[0, <see cref="MyersBitParallel64Ascii.MaxPatternLength"/>]</c>.
    /// </summary>
    public readonly int Length;

    /// <summary>
    /// 64-bit "alphabet" mask of every mapped symbol that occurs in the
    /// pattern. Each pattern symbol contributes a bit at position
    /// <c>(mapped &amp; 63)</c>. Useful as a hint to
    /// <see cref="MyersBitParallel64Ascii.Distance(in MyersPattern64Ascii, string, int, ulong)"/>'s
    /// <c>requiredCharMask</c> parameter for callers that want to
    /// short-circuit candidates missing pattern symbols.
    /// </summary>
    public readonly ulong CharMask;

    /// <summary>
    /// Number of distinct bits set in <see cref="CharMask"/>. Used by the
    /// engine to bound the minimum number of edits implied by the
    /// candidate's missing pattern symbols.
    /// </summary>
    public readonly int UniqueCharCount;

    internal MyersPattern64Ascii(
        ulong[]? masks,
        int length,
        ulong lastBitMask,
        ulong maskAll,
        ulong charMask,
        int uniqueCharCount)
    {
        Masks = masks;
        Length = length;
        LastBitMask = lastBitMask;
        MaskAll = maskAll;
        CharMask = charMask;
        UniqueCharCount = uniqueCharCount;
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
