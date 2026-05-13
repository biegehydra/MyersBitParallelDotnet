using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MyersBitParallel.Internal;

namespace MyersBitParallel;

/// <summary>
/// Single-word Myers bit-parallel Levenshtein engine for patterns up to
/// <see cref="MaxPatternLength"/> symbols. Operates on byte values
/// produced by a user-supplied <see cref="char"/>-to-<see cref="byte"/>
/// mapper that is invoked exactly 256 times at construction to populate
/// a lookup table; the hot loop never invokes the delegate again.
/// </summary>
/// <remarks>
/// The engine itself is alphabet-agnostic: any mapping that fits each
/// <see cref="char"/> into a single <see cref="byte"/> bucket is valid.
/// The <see cref="AsciiCaseSensitive"/> and <see cref="AsciiCaseInsensitive"/>
/// statics are convenience instances wired with <see cref="AsciiMappers"/>;
/// for non-ASCII workloads, supply your own mapper via the constructor.
/// </remarks>
public sealed class MyersBitParallel64
{
    /// <summary>
    /// Pre-built engine using <see cref="AsciiMappers.CaseSensitive"/>:
    /// every byte value preserved verbatim, so case differences count.
    /// </summary>
    public static readonly MyersBitParallel64 AsciiCaseSensitive = new MyersBitParallel64(AsciiMappers.CaseSensitive);

    /// <summary>
    /// Pre-built engine using <see cref="AsciiMappers.CaseInsensitive"/>:
    /// ASCII <c>A</c>–<c>Z</c> folded to <c>a</c>–<c>z</c>; everything else
    /// preserved verbatim.
    /// </summary>
    public static readonly MyersBitParallel64 AsciiCaseInsensitive = new MyersBitParallel64(AsciiMappers.CaseInsensitive);


    /// <summary>
    /// Maximum number of pattern characters that can fit into the single
    /// <see cref="ulong"/> bit-vector used by this engine.
    /// </summary>
    public const int MaxPatternLength = 64;

    private readonly byte[] _map;


    /// <summary>
    /// Construct an engine with a prebuilt 256-entry lookup table
    /// </summary>
    public MyersBitParallel64(byte[] map)
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
    public MyersBitParallel64(Func<char, byte> charMapper)
    {
        if (charMapper == null) throw new ArgumentNullException(nameof(charMapper));
        _map = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            _map[i] = charMapper((char)i);
        }
    }

    /// <summary>
    /// Build a reusable <see cref="MyersPattern64"/> handle for
    /// <paramref name="pattern"/>. The caller owns the returned struct and
    /// must <see cref="MyersPattern64.Dispose"/> it when finished.
    /// </summary>
    public MyersPattern64 Prepare(string pattern)
    {
        int m = pattern.Length;
        if (m > MaxPatternLength)
        {
            throw new ArgumentException(
                $"Pattern length {m} exceeds the {MaxPatternLength}-symbol limit of {nameof(MyersBitParallel64)}.",
                nameof(pattern));
        }

        if (m == 0)
        {
            return new MyersPattern64(null, 0, 0UL, 0UL, 0UL, 0);
        }

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

        return new MyersPattern64(masks, m, lastBitMask, maskAll, charMask, Bits.PopCount(charMask));
    }

    /// <summary>
    /// Build the 64-bit character occurrence mask for <paramref name="s"/>
    /// using this engine's mapper. Suitable for passing to
    /// <see cref="Distance(in MyersPattern64, string, int, ulong)"/>
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
        using MyersPattern64 pat = Prepare(a);
        return Distance(in pat, b, maxDist, requiredCharMask);
    }

    /// <summary>
    /// Compute the exact Levenshtein distance between <paramref name="a"/>
    /// and <paramref name="b"/>, preparing and disposing a transient
    /// pattern for <paramref name="a"/>. Unlike the
    /// <see cref="Distance(string, string, int, ulong)"/> overload, this
    /// path runs the unbounded kernel: no per-iteration prune branch and
    /// no candidate char-mask prepass, so it is the fastest option when
    /// callers do not need maxDist or required-character filtering.
    /// </summary>
    public int Distance(string a, string b)
    {
        using MyersPattern64 pat = Prepare(a);
        return Distance(in pat, b);
    }

    /// <summary>
    /// Compute the Levenshtein distance between an already-prepared
    /// <paramref name="pattern"/> and <paramref name="candidate"/>.
    /// Returns <see cref="int.MaxValue"/> when the distance is known to
    /// exceed <paramref name="maxDist"/>, optionally short-circuiting via
    /// the <paramref name="requiredCharMask"/> filter.
    /// </summary>
    public int Distance(
        in MyersPattern64 pattern,
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
    /// Compute the exact Levenshtein distance between an already-prepared
    /// <paramref name="pattern"/> and <paramref name="candidate"/>. Unlike
    /// the <see cref="Distance(in MyersPattern64, string, int, ulong)"/>
    /// overload, this path runs the unbounded kernel: no per-iteration
    /// prune branch and no candidate char-mask prepass, so it is the
    /// fastest option when callers do not need maxDist or
    /// required-character filtering.
    /// </summary>
    public int Distance(in MyersPattern64 pattern, string candidate)
    {
        int m = pattern.Length;
        int n = candidate.Length;

        if (m == 0) return n;
        if (n == 0) return m;

        return UnboundedKernel(in pattern, candidate);
    }

    /// <summary>
    /// Compute distance and similarity ratio between <paramref name="a"/>
    /// and <paramref name="b"/>, preparing and disposing a transient
    /// pattern for <paramref name="a"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a finite Levenshtein distance <c>d</c>, the returned
    /// <see cref="SimilarityRatio.Ratio"/> is <c>1 - d / max(|a|, |b|)</c>, or <c>1</c> when both
    /// strings are empty.
    /// </para>
    /// <para>
    /// When <see cref="Distance(string, string, int, ulong)"/> would return
    /// <see cref="int.MaxValue"/> (over <paramref name="maxDist"/>, failed
    /// <paramref name="requiredCharMask"/> / alphabet overlap pruning, etc.), the returned
    /// <see cref="SimilarityRatio.Distance"/> is <see cref="int.MaxValue"/> and
    /// <see cref="SimilarityRatio.Ratio"/> is <c>0</c>. That ratio is a convention for “no
    /// comparable similarity” in <c>[0, 1]</c>, not <c>1 - int.MaxValue / maxLen</c>.
    /// </para>
    /// </remarks>
    public SimilarityRatio SimilarityRatio(string a, string b, int maxDist = int.MaxValue, ulong requiredCharMask = 0UL)
    {
        using MyersPattern64 pat = Prepare(a);
        int distance = Distance(in pat, b, maxDist, requiredCharMask);
        return BuildRatio(distance, a.Length, b.Length);
    }

    /// <summary>
    /// Compute distance and similarity ratio between <paramref name="a"/>
    /// and <paramref name="b"/>, preparing and disposing a transient
    /// pattern for <paramref name="a"/>. Runs the unbounded kernel for
    /// callers that do not need maxDist or required-character filtering.
    /// </summary>
    public SimilarityRatio SimilarityRatio(string a, string b)
    {
        using MyersPattern64 pat = Prepare(a);
        int distance = Distance(in pat, b);
        return BuildRatio(distance, a.Length, b.Length);
    }

    /// <summary>
    /// Compute distance and similarity ratio between an already-prepared
    /// <paramref name="pattern"/> and <paramref name="candidate"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For a finite Levenshtein distance <c>d</c>, the returned
    /// <see cref="SimilarityRatio.Ratio"/> is <c>1 - d / max(|pattern|, |candidate|)</c>, or <c>1</c>
    /// when both lengths are zero.
    /// </para>
    /// <para>
    /// When <see cref="Distance(in MyersPattern64, string, int, ulong)"/> would return
    /// <see cref="int.MaxValue"/> (over <paramref name="maxDist"/>, failed
    /// <paramref name="requiredCharMask"/> / alphabet overlap pruning, etc.), the returned
    /// <see cref="SimilarityRatio.Distance"/> is <see cref="int.MaxValue"/> and
    /// <see cref="SimilarityRatio.Ratio"/> is <c>0</c>. That ratio is a convention for “no
    /// comparable similarity” in <c>[0, 1]</c>, not <c>1 - int.MaxValue / maxLen</c>.
    /// </para>
    /// </remarks>
    public SimilarityRatio SimilarityRatio(in MyersPattern64 pattern, string candidate, int maxDist = int.MaxValue, ulong requiredCharMask = 0UL)
    {
        int distance = Distance(in pattern, candidate, maxDist, requiredCharMask);
        return BuildRatio(distance, pattern.Length, candidate.Length);
    }

    /// <summary>
    /// Compute distance and similarity ratio between an already-prepared
    /// <paramref name="pattern"/> and <paramref name="candidate"/>. Runs
    /// the unbounded kernel for callers that do not need maxDist or
    /// required-character filtering.
    /// </summary>
    public SimilarityRatio SimilarityRatio(in MyersPattern64 pattern, string candidate)
    {
        int distance = Distance(in pattern, candidate);
        return BuildRatio(distance, pattern.Length, candidate.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SimilarityRatio BuildRatio(int distance, int aLen, int bLen)
    {
        if (distance == int.MaxValue)
        {
            return new SimilarityRatio(int.MaxValue, 0);
        }
        int maxLen = aLen >= bLen ? aLen : bLen;
        double ratio = maxLen == 0 ? 1.0 : 1.0 - ((double)distance / maxLen);
        return new SimilarityRatio(distance, ratio);
    }

    private ulong BuildCharMaskCore(string s)
    {
        int n = s.Length;
        if (n == 0)
        {
            return 0UL;
        }

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

    private int BoundedKernel(in MyersPattern64 pattern, string candidate, int maxDist)
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
            {
                score++;
            }
            else if ((HN & lastBitMask) != 0)
            {
                score--;
            }

            // Even if every remaining char decreases the score by 1, can
            // we still finish at <= maxDist? If not, the answer is "too far".
            int remaining = n - i - 1;
            if (score - remaining > maxDist)
            {
                return int.MaxValue;
            }
        }

        return score <= maxDist ? score : int.MaxValue;
    }

    private int UnboundedKernel(in MyersPattern64 pattern, string candidate)
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
            {
                score++;
            }
            else if ((HN & lastBitMask) != 0)
            {
                score--;
            }

            // slightly faster than bounded kernel
            // because we lose one check every iteration
            // here
        }

        return score;
    }
}

/// <summary>
/// Reusable, prepared pattern handle produced by
/// <see cref="MyersBitParallel64.Prepare(string)"/>. Holds an
/// <see cref="ArrayPool{T}"/>-rented bit-mask table that callers must
/// release via <see cref="Dispose"/>.
/// </summary>
public readonly struct MyersPattern64 : IDisposable
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
    /// <c>[0, <see cref="MyersBitParallel64.MaxPatternLength"/>]</c>.
    /// </summary>
    public readonly int Length;

    /// <summary>
    /// 64-bit "alphabet" mask of every mapped symbol that occurs in the
    /// pattern. Each pattern symbol contributes a bit at position
    /// <c>(mapped &amp; 63)</c>. Useful as a hint to
    /// <see cref="MyersBitParallel64.Distance(in MyersPattern64, string, int, ulong)"/>'s
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

    internal MyersPattern64(
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
