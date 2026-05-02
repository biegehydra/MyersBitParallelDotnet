using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

#if NET5_0_OR_GREATER
using System.Text;
#endif

namespace MyersBitParallel
{
    /// <summary>
    /// Single-word Myers bit-parallel Levenshtein engine that operates on
    /// Unicode scalar values (code points) and supports patterns up to
    /// <see cref="MaxPatternLength"/> scalars. On modern runtimes the engine
    /// uses <see cref="System.Text.Rune"/> for iteration; on
    /// <c>netstandard2.1</c> it performs manual UTF-16 decoding.
    /// </summary>
    public sealed class MyersBitParallel64Unicode : IMyersBitParallel
    {
        /// <summary>
        /// Maximum number of pattern Unicode scalar values that can fit into
        /// the single <see cref="ulong"/> bit-vector used by this engine.
        /// </summary>
        public const int MaxPatternLength = 64;

#if NET5_0_OR_GREATER
        private readonly Func<Rune, Rune> _runeMapper;

        /// <summary>
        /// Construct an engine using
        /// <see cref="UnicodeMappers.SimpleInvariantCaseFold(Rune)"/>.
        /// </summary>
        public MyersBitParallel64Unicode() : this(UnicodeMappers.SimpleInvariantCaseFold) { }

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
        /// Construct an engine using
        /// <see cref="UnicodeMappers.SimpleInvariantCaseFold(int)"/>.
        /// </summary>
        public MyersBitParallel64Unicode() : this(UnicodeMappers.SimpleInvariantCaseFold) { }

        /// <summary>
        /// Construct an engine that normalizes every code point through
        /// <paramref name="codePointMapper"/> before bit-vector lookup.
        /// </summary>
        public MyersBitParallel64Unicode(Func<int, int> codePointMapper)
        {
            _codePointMapper = codePointMapper ?? throw new ArgumentNullException(nameof(codePointMapper));
        }
#endif

        /// <inheritdoc />
        public MyersPattern Prepare(string pattern)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));

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

            return new Unicode64Pattern(this, masks, m, lastBitMask, maskAll);
        }

        /// <inheritdoc />
        public int Distance(string a, string b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            using MyersPattern pat = Prepare(a);
            return DistanceCore((Unicode64Pattern)pat, b, out _);
        }

        /// <inheritdoc />
        public int Distance(MyersPattern pattern, string candidate)
        {
            Unicode64Pattern p = ValidatePattern(pattern);
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            return DistanceCore(p, candidate, out _);
        }

        /// <inheritdoc />
        public SimilarityRatio SimilarityRatio(string a, string b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            using MyersPattern pat = Prepare(a);
            int distance = DistanceCore((Unicode64Pattern)pat, b, out int candidateScalarCount);
            return BuildRatio(distance, pat.Length, candidateScalarCount);
        }

        /// <inheritdoc />
        public SimilarityRatio SimilarityRatio(MyersPattern pattern, string candidate)
        {
            Unicode64Pattern p = ValidatePattern(pattern);
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            int distance = DistanceCore(p, candidate, out int candidateScalarCount);
            return BuildRatio(distance, p.Length, candidateScalarCount);
        }

        private static SimilarityRatio BuildRatio(int distance, int aLen, int bLen)
        {
            int maxLen = aLen >= bLen ? aLen : bLen;
            double ratio = maxLen == 0 ? 1.0 : 1.0 - ((double)distance / maxLen);
            return new SimilarityRatio(distance, ratio);
        }

        private Unicode64Pattern ValidatePattern(MyersPattern pattern)
        {
            if (pattern == null)
                throw new ArgumentNullException(nameof(pattern));
            if (pattern is not Unicode64Pattern p || !ReferenceEquals(p.Owner, this))
                throw new ArgumentException(
                    "Pattern was not created by this engine instance.",
                    nameof(pattern));
            if (p.IsDisposed)
                throw new ObjectDisposedException(nameof(MyersPattern));
            return p;
        }

        private int DistanceCore(Unicode64Pattern p, string candidate, out int candidateScalarCount)
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

        private sealed class Unicode64Pattern : MyersPattern
        {
            internal readonly MyersBitParallel64Unicode Owner;
            internal Dictionary<int, ulong>? Masks;
            internal readonly ulong MaskAll;
            internal readonly ulong LastBitMask;

            internal bool IsDisposed => Masks is null;

            internal Unicode64Pattern(
                MyersBitParallel64Unicode owner,
                Dictionary<int, ulong> masks,
                int length,
                ulong lastBitMask,
                ulong maskAll)
                : base(length)
            {
                Owner = owner;
                Masks = masks;
                LastBitMask = lastBitMask;
                MaskAll = maskAll;
            }

            public override void Dispose()
            {
                Masks = null;
            }
        }
    }
}
