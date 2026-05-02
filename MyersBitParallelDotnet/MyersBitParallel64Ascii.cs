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
public sealed class MyersBitParallel64Ascii : IMyersBitParallel
{
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
        {
            _map[i] = charMapper((char)i);
        }
    }

    /// <inheritdoc />
    public MyersPattern Prepare(string pattern)
    {
        if (pattern == null) throw new ArgumentNullException(nameof(pattern));
        int m = pattern.Length;
        if (m > MaxPatternLength)
        {
            throw new ArgumentException(
                $"Pattern length {m} exceeds the {MaxPatternLength}-symbol limit of {nameof(MyersBitParallel64Ascii)}.",
                nameof(pattern));
        }

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
        ulong maskAll = m == 0
            ? 0UL
            : (m == 64 ? ulong.MaxValue : (1UL << m) - 1UL);
        ulong lastBitMask = m == 0 ? 0UL : 1UL << (m - 1);

        return new Ascii64Pattern(this, masks, m, lastBitMask, maskAll);
    }

    /// <inheritdoc />
    public int Distance(string a, string b)
    {
        if (a == null) throw new ArgumentNullException(nameof(a));
        if (b == null) throw new ArgumentNullException(nameof(b));
        using MyersPattern pat = Prepare(a);
        return DistanceCore((Ascii64Pattern)pat, b);
    }

    /// <inheritdoc />
    public int Distance(MyersPattern pattern, string candidate)
    {
        Ascii64Pattern p = ValidatePattern(pattern);
        if (candidate == null) throw new ArgumentNullException(nameof(candidate));
        return DistanceCore(p, candidate);
    }

    /// <inheritdoc />
    public SimilarityRatio SimilarityRatio(string a, string b)
    {
        if (a == null) throw new ArgumentNullException(nameof(a));
        if (b == null) throw new ArgumentNullException(nameof(b));
        using MyersPattern pat = Prepare(a);
        int distance = DistanceCore((Ascii64Pattern)pat, b);
        return BuildRatio(distance, a.Length, b.Length);
    }

    /// <inheritdoc />
    public SimilarityRatio SimilarityRatio(MyersPattern pattern, string candidate)
    {
        Ascii64Pattern p = ValidatePattern(pattern);
        if (candidate == null) throw new ArgumentNullException(nameof(candidate));
        int distance = DistanceCore(p, candidate);
        return BuildRatio(distance, p.Length, candidate.Length);
    }

    private static SimilarityRatio BuildRatio(int distance, int aLen, int bLen)
    {
        int maxLen = aLen >= bLen ? aLen : bLen;
        double ratio = maxLen == 0 ? 1.0 : 1.0 - ((double)distance / maxLen);
        return new SimilarityRatio(distance, ratio);
    }

    private Ascii64Pattern ValidatePattern(MyersPattern pattern)
    {
        if (pattern == null)
            throw new ArgumentNullException(nameof(pattern));
        if (pattern is not Ascii64Pattern p || !ReferenceEquals(p.Owner, this))
            throw new ArgumentException(
                "Pattern was not created by this engine instance.",
                nameof(pattern));
        if (p.PatternMasks is null)
            throw new ObjectDisposedException(nameof(MyersPattern));
        return p;
    }

    private int DistanceCore(Ascii64Pattern p, string candidate)
    {
        int m = p.Length;
        int n = candidate.Length;
        if (m == 0) return n;
        if (n == 0) return m;

        ulong maskAll = p.MaskAll;
        ulong lastBitMask = p.LastBitMask;
        ulong VP = maskAll;
        ulong VN = 0UL;
        int score = m;

        ulong[] masks = p.PatternMasks!;
        ref ulong patternRef = ref ArrayHelpers.GetArrayDataReference(masks);
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

    private sealed class Ascii64Pattern : MyersPattern
    {
        internal readonly MyersBitParallel64Ascii Owner;
        internal ulong[]? PatternMasks;
        internal readonly ulong MaskAll;
        internal readonly ulong LastBitMask;

        internal Ascii64Pattern(
            MyersBitParallel64Ascii owner,
            ulong[] patternMasks,
            int length,
            ulong lastBitMask,
            ulong maskAll)
            : base(length)
        {
            Owner = owner;
            PatternMasks = patternMasks;
            LastBitMask = lastBitMask;
            MaskAll = maskAll;
        }

        public override void Dispose()
        {
            ulong[]? toReturn = PatternMasks;
            if (toReturn != null)
            {
                PatternMasks = null;
                ArrayPool<ulong>.Shared.Return(toReturn, clearArray: false);
            }
        }
    }
}
