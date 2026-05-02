using System;
using System.Buffers;
using System.Runtime.CompilerServices;

#if NET5_0_OR_GREATER
using System.Text;
#endif

namespace MyersBitParallel
{
    /// <summary>
    /// Unicode Levenshtein engine without the 64-character pattern length
    /// limit. Operates on Unicode scalar values (code points) and uses a
    /// dynamic-programming kernel; future versions may upgrade to a blocked
    /// Myers bit-parallel kernel without changing the public API.
    /// </summary>
    public sealed class MyersBitParallelGeneralUnicode : IMyersBitParallel
    {
#if NET5_0_OR_GREATER
        private readonly Func<Rune, Rune> _runeMapper;

        /// <summary>
        /// Construct an engine using
        /// <see cref="UnicodeMappers.SimpleInvariantCaseFold(Rune)"/>.
        /// </summary>
        public MyersBitParallelGeneralUnicode() : this(UnicodeMappers.SimpleInvariantCaseFold) { }

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
        /// Construct an engine using
        /// <see cref="UnicodeMappers.SimpleInvariantCaseFold(int)"/>.
        /// </summary>
        public MyersBitParallelGeneralUnicode() : this(UnicodeMappers.SimpleInvariantCaseFold) { }

        /// <summary>
        /// Construct an engine that normalizes every code point through
        /// <paramref name="codePointMapper"/> before comparing.
        /// </summary>
        public MyersBitParallelGeneralUnicode(Func<int, int> codePointMapper)
        {
            _codePointMapper = codePointMapper ?? throw new ArgumentNullException(nameof(codePointMapper));
        }
#endif

        /// <inheritdoc />
        public MyersPattern Prepare(string pattern)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));

            int[]? codes = ToMappedCodePoints(pattern, out int count);
            return new GeneralUnicodePattern(this, codes, count);
        }

        /// <inheritdoc />
        public int Distance(string a, string b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            using MyersPattern pat = Prepare(a);
            return DistanceCore((GeneralUnicodePattern)pat, b);
        }

        /// <inheritdoc />
        public int Distance(MyersPattern pattern, string candidate)
        {
            GeneralUnicodePattern p = ValidatePattern(pattern);
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            return DistanceCore(p, candidate);
        }

        /// <inheritdoc />
        public SimilarityRatio SimilarityRatio(string a, string b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            using MyersPattern pat = Prepare(a);
            return RatioFromCandidate((GeneralUnicodePattern)pat, b);
        }

        /// <inheritdoc />
        public SimilarityRatio SimilarityRatio(MyersPattern pattern, string candidate)
        {
            GeneralUnicodePattern p = ValidatePattern(pattern);
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            return RatioFromCandidate(p, candidate);
        }

        private SimilarityRatio RatioFromCandidate(GeneralUnicodePattern p, string candidate)
        {
            int[] bCodes = ToMappedCodePoints(candidate, out int n);
            try
            {
                int distance = DistanceCore(p, bCodes, n);
                int maxLen = p.Length >= n ? p.Length : n;
                double ratio = maxLen == 0 ? 1.0 : 1.0 - ((double)distance / maxLen);
                return new SimilarityRatio(distance, ratio);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(bCodes, clearArray: false);
            }
        }

        private GeneralUnicodePattern ValidatePattern(MyersPattern pattern)
        {
            if (pattern == null)
                throw new ArgumentNullException(nameof(pattern));
            if (pattern is not GeneralUnicodePattern p || !ReferenceEquals(p.Owner, this))
                throw new ArgumentException(
                    "Pattern was not created by this engine instance.",
                    nameof(pattern));
            if (p.IsDisposed)
                throw new ObjectDisposedException(nameof(MyersPattern));
            return p;
        }

        private int DistanceCore(GeneralUnicodePattern p, string candidate)
        {
            int[] bCodes = ToMappedCodePoints(candidate, out int n);
            try
            {
                return DistanceCore(p, bCodes, n);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(bCodes, clearArray: false);
            }
        }

        private static int DistanceCore(GeneralUnicodePattern p, int[] bCodes, int n)
        {
            int m = p.Length;
            if (m == 0) return n;
            if (n == 0) return m;

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
            // Code-point count is at most s.Length; rent at least one slot so
            // the caller can safely return an empty buffer to the pool.
            int[] buffer = ArrayPool<int>.Shared.Rent(s.Length == 0 ? 1 : s.Length);
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

        private sealed class GeneralUnicodePattern : MyersPattern
        {
            internal readonly MyersBitParallelGeneralUnicode Owner;
            internal int[]? Codes;

            internal bool IsDisposed { get; private set; }

            internal GeneralUnicodePattern(MyersBitParallelGeneralUnicode owner, int[]? codes, int length)
                : base(length)
            {
                Owner = owner;
                Codes = codes;
            }

            public override void Dispose()
            {
                if (IsDisposed) return;
                IsDisposed = true;
                if (Codes != null)
                {
                    ArrayPool<int>.Shared.Return(Codes, clearArray: false);
                    Codes = null;
                }
            }
        }
    }
}
