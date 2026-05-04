using System;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Tests;

[TestClass]
public sealed class MyersSubstringBitParallel64Tests
{
    private static readonly MyersSubstringBitParallel64 CaseInsensitive = MyersSubstringBitParallel64.CaseInsensitive;
    private static readonly MyersSubstringBitParallel64 CaseSensitive = MyersSubstringBitParallel64.CaseSensitive;

    [TestMethod]
    [DynamicData(nameof(TestCases.SubstringAscii), typeof(TestCases))]
    public void BestMatchDistance_StringString_Matches_Expected(string pattern, string text, int expected)
    {
        Assert.AreEqual(expected, CaseInsensitive.BestMatchDistance(pattern, text));
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.SubstringAscii), typeof(TestCases))]
    public void BestMatchDistance_PreparedPattern_Matches_Expected(string pattern, string text, int expected)
    {
        using MyersSubstringPattern64 pat = CaseInsensitive.Prepare(pattern);
        Assert.AreEqual(expected, CaseInsensitive.BestMatchDistance(in pat, text));
    }

    /// <summary>
    /// Compares the engine against an O(n^3) brute-force substring reference
    /// across a large, deterministic grid of random short inputs. The tiny
    /// alphabet guarantees frequent near-matches that exercise insertion,
    /// deletion and substitution paths simultaneously. Any regression to
    /// prefix-only or global semantics is caught here, even if the curated
    /// theory above happens to miss the offending case.
    /// </summary>
    [TestMethod]
    public void BestMatchDistance_Matches_BruteForce_Reference_On_Randomized_Inputs()
    {
        var rng = new Random(0xC0FFEE);
        const string alphabet = "ABC_";

        for (int trial = 0; trial < 2000; trial++)
        {
            int m = rng.Next(0, 13);
            int n = rng.Next(0, 25);
            string pattern = RandomString(rng, alphabet, m);
            string text = RandomString(rng, alphabet, n);

            int expected = BruteForceSubstringDistance(pattern, text);
            int actual = CaseSensitive.BestMatchDistance(pattern, text);

            Assert.AreEqual(expected, actual, $"pattern='{pattern}', text='{text}'");
        }
    }

    [TestMethod]
    public void BestMatchDistance_Honors_Case_Sensitivity_Of_Configured_Engine()
    {
        // CaseInsensitive folds letter case; CaseSensitive treats each case
        // as a distinct symbol. The "do nothing" rows are just as important:
        // CaseSensitive must NOT fold, and CaseInsensitive must NOT overcount.

        // Exact matches — both engines agree on distance 0.
        Assert.AreEqual(0, CaseSensitive.BestMatchDistance("hello", "say hello world"));
        Assert.AreEqual(0, CaseInsensitive.BestMatchDistance("hello", "say hello world"));
        Assert.AreEqual(0, CaseInsensitive.BestMatchDistance("Hello", "SAY HELLO WORLD"));

        // Case-only differences — CaseSensitive penalizes every letter;
        // CaseInsensitive folds them away.
        Assert.AreEqual(5, CaseSensitive.BestMatchDistance("hello", "SAY HELLO WORLD"));
        Assert.AreEqual(0, CaseInsensitive.BestMatchDistance("hello", "SAY HELLO WORLD"));

        // Near-match with one genuine edit plus case differences. Total
        // edits seen by each engine:
        //   CaseSensitive: 4 letter-case subs + 1 real sub = 5
        //   CaseInsensitive: 1 real sub = 1
        Assert.AreEqual(5, CaseSensitive.BestMatchDistance("hello", "SAY HELLA WORLD"));
        Assert.AreEqual(1, CaseInsensitive.BestMatchDistance("hello", "SAY HELLA WORLD"));
    }

    [TestMethod]
    public void Prepare_Throws_When_Pattern_Exceeds_64_Chars()
    {
        string pattern = new('A', 65);
        Assert.ThrowsExactly<ArgumentException>(() => CaseInsensitive.Prepare(pattern));
    }

    [TestMethod]
    public void Distance_Throws_When_Pattern_Exceeds_64_Chars()
    {
        string pattern = new('A', 65);
        Assert.ThrowsExactly<ArgumentException>(() => CaseInsensitive.BestMatchDistance(pattern, "HAYSTACK"));
    }

    [TestMethod]
    public void Custom_Mapper_Is_Invoked_Only_Once_Per_Byte_Value()
    {
        int invocationCount = 0;
        var engine = new MyersSubstringBitParallel64(c =>
        {
            invocationCount++;
            return AsciiMappers.CaseInsensitive(c);
        });

        Assert.AreEqual(256, invocationCount);

        // Lots of subsequent work; the mapper must never be called again.
        for (int i = 0; i < 1000; i++)
        {
            engine.BestMatchDistance("hello", "say hello world");
        }

        Assert.AreEqual(256, invocationCount);
    }

    private static string RandomString(Random rng, string alphabet, int length)
    {
        if (length == 0) return string.Empty;
        Span<char> buf = stackalloc char[length];
        for (int i = 0; i < length; i++)
        {
            buf[i] = alphabet[rng.Next(alphabet.Length)];
        }
        return new string(buf);
    }

    private static int BruteForceSubstringDistance(string pattern, string text)
    {
        if (pattern.Length == 0) return 0;

        int best = int.MaxValue;
        for (int start = 0; start <= text.Length; start++)
        {
            for (int end = start; end <= text.Length; end++)
            {
                int d = Levenshtein(pattern, text.AsSpan(start, end - start));
                if (d < best) best = d;
            }
        }
        return best;
    }

    private static int Levenshtein(string a, ReadOnlySpan<char> b)
    {
        int m = a.Length;
        int n = b.Length;
        int[,] dp = new int[m + 1, n + 1];
        for (int i = 0; i <= m; i++) dp[i, 0] = i;
        for (int j = 0; j <= n; j++) dp[0, j] = j;
        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                int sub = dp[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                int del = dp[i - 1, j] + 1;
                int ins = dp[i, j - 1] + 1;
                int best = sub;
                if (del < best) best = del;
                if (ins < best) best = ins;
                dp[i, j] = best;
            }
        }
        return dp[m, n];
    }
}
