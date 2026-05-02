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
            return new MyersPattern64Ascii(null, 0, 0UL, 0UL);

        // 256 slots so any byte value the user-supplied mapper might emit
        // can be indexed directly without further bookkeeping.
        ulong[] masks = ArrayPool<ulong>.Shared.Rent(256);
        Array.Clear(masks, 0, 256);

        ref byte mapRef = ref ArrayHelpers.GetArrayDataReference(_map);
        for (int i = 0; i < m; i++)
        {
            byte idx = Unsafe.Add(ref mapRef, (byte)pattern[i]);
            masks[idx] |= 1UL << i;
        }

        // Avoid (1UL << 64) which is undefined in C#; saturate at MaxValue.
        ulong maskAll = m == 64 ? ulong.MaxValue : (1UL << m) - 1UL;
        ulong lastBitMask = 1UL << (m - 1);

        return new MyersPattern64Ascii(masks, m, lastBitMask, maskAll);
    }

    /// <summary>
    /// Compute the Levenshtein distance between <paramref name="a"/> and
    /// <paramref name="b"/>, preparing and disposing a transient pattern
    /// for <paramref name="a"/>.
    /// </summary>
    public int Distance(string a, string b)
    {
        using MyersPattern64Ascii pat = Prepare(a);
        return Distance(in pat, b);
    }

    /// <summary>
    /// Compute the Levenshtein distance between an already-prepared
    /// <paramref name="pattern"/> and <paramref name="candidate"/>.
    /// </summary>
    public int Distance(in MyersPattern64Ascii pattern, string candidate)
    {
        int m = pattern.Length;
        int n = candidate.Length;
        if (m == 0) return n;
        if (n == 0) return m;

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
        }

        return score;
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

    internal MyersPattern64Ascii(ulong[]? masks, int length, ulong lastBitMask, ulong maskAll)
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
