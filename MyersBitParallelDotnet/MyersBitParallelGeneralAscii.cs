using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using MyersBitParallel.Internal;

namespace MyersBitParallel
{
    /// <summary>
    /// ASCII Levenshtein engine without the 64-character pattern length
    /// limit. The current implementation uses a straightforward
    /// dynamic-programming kernel; future versions may upgrade to a blocked
    /// Myers bit-parallel kernel without changing the public API.
    /// </summary>
    public sealed class MyersBitParallelGeneralAscii : IMyersBitParallel
    {
        private readonly byte[] _map;

        /// <summary>
        /// Construct an engine that uses
        /// <see cref="AsciiMappers.CaseInsensitive"/> to fill the lookup
        /// table.
        /// </summary>
        public MyersBitParallelGeneralAscii() : this(AsciiMappers.CaseInsensitive) { }

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

        /// <inheritdoc />
        public MyersPattern Prepare(string pattern)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));
            int m = pattern.Length;

            byte[]? codes = null;
            if (m > 0)
            {
                codes = ArrayPool<byte>.Shared.Rent(m);
                ref byte mapRef = ref ArrayHelpers.GetArrayDataReference(_map);
                for (int i = 0; i < m; i++)
                    codes[i] = Unsafe.Add(ref mapRef, (byte)pattern[i]);
            }

            return new GeneralAsciiPattern(this, codes, m);
        }

        /// <inheritdoc />
        public int Distance(string a, string b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            using MyersPattern pat = Prepare(a);
            return DistanceCore((GeneralAsciiPattern)pat, b);
        }

        /// <inheritdoc />
        public int Distance(MyersPattern pattern, string candidate)
        {
            GeneralAsciiPattern p = ValidatePattern(pattern);
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            return DistanceCore(p, candidate);
        }

        /// <inheritdoc />
        public SimilarityRatio SimilarityRatio(string a, string b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            using MyersPattern pat = Prepare(a);
            int distance = DistanceCore((GeneralAsciiPattern)pat, b);
            return BuildRatio(distance, a.Length, b.Length);
        }

        /// <inheritdoc />
        public SimilarityRatio SimilarityRatio(MyersPattern pattern, string candidate)
        {
            GeneralAsciiPattern p = ValidatePattern(pattern);
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

        private GeneralAsciiPattern ValidatePattern(MyersPattern pattern)
        {
            if (pattern == null)
                throw new ArgumentNullException(nameof(pattern));
            if (pattern is not GeneralAsciiPattern p || !ReferenceEquals(p.Owner, this))
                throw new ArgumentException(
                    "Pattern was not created by this engine instance.",
                    nameof(pattern));
            if (p.IsDisposed)
                throw new ObjectDisposedException(nameof(MyersPattern));
            return p;
        }

        private int DistanceCore(GeneralAsciiPattern p, string candidate)
        {
            int m = p.Length;
            int n = candidate.Length;
            if (m == 0) return n;
            if (n == 0) return m;

            byte[] aCodes = p.Codes!;
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

        private sealed class GeneralAsciiPattern : MyersPattern
        {
            internal readonly MyersBitParallelGeneralAscii Owner;
            internal byte[]? Codes;

            internal bool IsDisposed { get; private set; }

            internal GeneralAsciiPattern(MyersBitParallelGeneralAscii owner, byte[]? codes, int length)
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
                    ArrayPool<byte>.Shared.Return(Codes, clearArray: false);
                    Codes = null;
                }
            }
        }
    }
}
