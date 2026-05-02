using System;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Tests;

[TestClass]
public sealed class MyersBitParallel64AsciiTests
{
    private static readonly MyersBitParallel64Ascii Engine = MyersBitParallel64Ascii.CaseInsensitive;

    [TestMethod]
    [DynamicData(nameof(TestCases.Ascii), typeof(TestCases))]
    public void Distance_StringString_Matches_Expected(string a, string b, int expected)
    {
        Assert.AreEqual(expected, Engine.Distance(a, b));
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.Ascii), typeof(TestCases))]
    public void Distance_PreparedPattern_Matches_Expected(string a, string b, int expected)
    {
        using MyersPattern64Ascii pat = Engine.Prepare(a);
        Assert.AreEqual(expected, Engine.Distance(in pat, b));
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.Ascii), typeof(TestCases))]
    public void SimilarityRatio_StringString_Matches_Expected(string a, string b, int expected)
    {
        SimilarityRatio result = Engine.SimilarityRatio(a, b);
        int maxLen = Math.Max(a.Length, b.Length);
        double expectedRatio = maxLen == 0 ? 1.0 : 1.0 - ((double)expected / maxLen);
        Assert.AreEqual(expected, result.Distance);
        Assert.AreEqual(expectedRatio, result.Ratio, 1e-12);
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.Ascii), typeof(TestCases))]
    public void SimilarityRatio_PreparedPattern_Matches_Expected(string a, string b, int expected)
    {
        using MyersPattern64Ascii pat = Engine.Prepare(a);
        SimilarityRatio result = Engine.SimilarityRatio(in pat, b);
        int maxLen = Math.Max(a.Length, b.Length);
        double expectedRatio = maxLen == 0 ? 1.0 : 1.0 - ((double)expected / maxLen);
        Assert.AreEqual(expected, result.Distance);
        Assert.AreEqual(expectedRatio, result.Ratio, 1e-12);
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.Ascii), typeof(TestCases))]
    public void Distance_Is_Symmetric(string a, string b, int expected)
    {
        Assert.AreEqual(expected, Engine.Distance(a, b));
        Assert.AreEqual(expected, Engine.Distance(b, a));
    }

    [TestMethod]
    public void Prepare_Allows_Empty_Pattern()
    {
        using MyersPattern64Ascii pat = Engine.Prepare("");
        Assert.AreEqual(0, pat.Length);
        Assert.AreEqual(5, Engine.Distance(in pat, "HELLO"));
        Assert.AreEqual(0, Engine.Distance(in pat, ""));
    }

    [TestMethod]
    public void Prepare_Allows_Pattern_Of_Exactly_64_Chars()
    {
        string pattern = new('A', 64);
        using MyersPattern64Ascii pat = Engine.Prepare(pattern);
        Assert.AreEqual(64, pat.Length);
        Assert.AreEqual(0, Engine.Distance(in pat, pattern));
        Assert.AreEqual(1, Engine.Distance(in pat, new string('A', 63)));
        Assert.AreEqual(64, Engine.Distance(in pat, new string('B', 64)));
    }

    [TestMethod]
    public void Prepare_Throws_When_Pattern_Exceeds_64_Chars()
    {
        string pattern = new('A', 65);
        Assert.ThrowsExactly<ArgumentException>(() => Engine.Prepare(pattern));
    }

    [TestMethod]
    public void Distance_Throws_When_Pattern_Exceeds_64_Chars()
    {
        string pattern = new('A', 65);
        Assert.ThrowsExactly<ArgumentException>(() => Engine.Distance(pattern, "A"));
    }

    [TestMethod]
    public void Distance_Allows_Candidate_Longer_Than_64_Chars()
    {
        // Pattern length is bounded; candidate length is not.
        string pattern = "HELLO";
        string candidate = new('X', 200);
        // 195 insertions to grow HELLO to length 200, plus 5 substitutions.
        Assert.AreEqual(200, Engine.Distance(pattern, candidate));
    }

    [TestMethod]
    public void Distance_Allows_Very_Long_Candidate()
    {
        string pattern = new('A', 32);
        string candidate = new('A', 5000);
        // The first 32 A's match the pattern; the remaining 4968 A's are
        // pure insertions.
        Assert.AreEqual(4968, Engine.Distance(pattern, candidate));
    }

    [TestMethod]
    public void CaseInsensitive_Default_Folds_Ascii_Letters()
    {
        Assert.AreEqual(0, MyersBitParallel64Ascii.CaseInsensitive.Distance("Hello", "HELLO"));
        Assert.AreEqual(0, MyersBitParallel64Ascii.CaseInsensitive.Distance("KITTEN", "kitten"));
        Assert.AreEqual(1, MyersBitParallel64Ascii.CaseInsensitive.Distance("Hello", "hella"));
    }

    [TestMethod]
    public void CaseSensitive_Distinguishes_Letter_Case()
    {
        Assert.AreEqual(1, MyersBitParallel64Ascii.CaseSensitive.Distance("Hello", "hello"));
        Assert.AreEqual(5, MyersBitParallel64Ascii.CaseSensitive.Distance("HELLO", "hello"));
        Assert.AreEqual(0, MyersBitParallel64Ascii.CaseSensitive.Distance("HELLO", "HELLO"));
    }

    [TestMethod]
    public void Pattern_Can_Be_Reused_Across_Many_Candidates()
    {
        using MyersPattern64Ascii pat = Engine.Prepare("HELLO");
        Assert.AreEqual(0, Engine.Distance(in pat, "HELLO"));
        Assert.AreEqual(1, Engine.Distance(in pat, "HELLO!"));
        Assert.AreEqual(1, Engine.Distance(in pat, "HALLO"));
        Assert.AreEqual(1, Engine.Distance(in pat, "HELO"));
        Assert.AreEqual(3, Engine.Distance(in pat, "HEY"));
        Assert.AreEqual(5, Engine.Distance(in pat, ""));
    }

    [TestMethod]
    public void Custom_Mapper_Is_Invoked_Only_Once_Per_Byte_Value()
    {
        int invocationCount = 0;
        var engine = new MyersBitParallel64Ascii(c =>
        {
            invocationCount++;
            return AsciiMappers.CaseInsensitive(c);
        });

        Assert.AreEqual(256, invocationCount);

        // Lots of subsequent work; the mapper must never be called again.
        for (int i = 0; i < 1000; i++)
            engine.Distance("hello world", "hello world");

        Assert.AreEqual(256, invocationCount);
    }

    [TestMethod]
    public void MaxDist_Returns_MaxValue_When_LengthDifference_Exceeds_Threshold()
    {
        // |HELLO| - |HI| = 3, so maxDist=2 must short-circuit.
        Assert.AreEqual(int.MaxValue, Engine.Distance("HELLO", "HI", maxDist: 2));
        Assert.AreEqual(int.MaxValue, Engine.Distance("HI", "HELLO", maxDist: 2));
    }

    [TestMethod]
    public void MaxDist_Returns_MaxValue_When_Distance_Exceeds_Threshold()
    {
        // Real distance is 3 (HELLO vs HEY); reject every threshold below.
        Assert.AreEqual(int.MaxValue, Engine.Distance("HELLO", "HEY", maxDist: 0));
        Assert.AreEqual(int.MaxValue, Engine.Distance("HELLO", "HEY", maxDist: 1));
        Assert.AreEqual(int.MaxValue, Engine.Distance("HELLO", "HEY", maxDist: 2));
        Assert.AreEqual(3, Engine.Distance("HELLO", "HEY", maxDist: 3));
        Assert.AreEqual(3, Engine.Distance("HELLO", "HEY", maxDist: 100));
    }

    [TestMethod]
    public void MaxDist_Returns_True_Distance_When_Threshold_Is_Loose()
    {
        // A loose threshold must never alter the reported distance.
        Assert.AreEqual(0, Engine.Distance("HELLO", "HELLO", maxDist: 5));
        Assert.AreEqual(1, Engine.Distance("HELLO", "HALLO", maxDist: 5));
        Assert.AreEqual(2, Engine.Distance("KITTEN", "KITCHEN", maxDist: 5));
    }

    [TestMethod]
    public void MaxDist_Works_With_Prepared_Pattern_Reuse()
    {
        using MyersPattern64Ascii pat = Engine.Prepare("APPLE");
        Assert.AreEqual(0, Engine.Distance(in pat, "APPLE", maxDist: 1));
        Assert.AreEqual(1, Engine.Distance(in pat, "APPLES", maxDist: 1));
        Assert.AreEqual(int.MaxValue, Engine.Distance(in pat, "BANANA", maxDist: 1));
        Assert.AreEqual(5, Engine.Distance(in pat, "BANANA", maxDist: 5));
    }

    [TestMethod]
    public void Pattern_CharMask_Reflects_Distinct_Pattern_Symbols()
    {
        using MyersPattern64Ascii pat = Engine.Prepare("ABCDE");
        // 5 distinct ASCII letters → 5 set bits.
        Assert.AreEqual(5, pat.UniqueCharCount);

        using MyersPattern64Ascii pat2 = Engine.Prepare("AAAAA");
        // 1 distinct symbol regardless of repetition.
        Assert.AreEqual(1, pat2.UniqueCharCount);

        using MyersPattern64Ascii empty = Engine.Prepare("");
        Assert.AreEqual(0UL, empty.CharMask);
        Assert.AreEqual(0, empty.UniqueCharCount);
    }

    [TestMethod]
    public void BuildCharMask_Matches_Prepared_Pattern_CharMask()
    {
        // The two paths (Prepare vs BuildCharMask) walk the same mapper
        // table and the same collapse rule, so for any string they must
        // produce identical masks.
        string sample = "HELLO WORLD";
        using MyersPattern64Ascii pat = Engine.Prepare(sample);
        Assert.AreEqual(pat.CharMask, Engine.BuildCharMask(sample));
    }

    [TestMethod]
    public void BuildCharMask_Empty_String_Returns_Zero()
    {
        Assert.AreEqual(0UL, Engine.BuildCharMask(""));
    }

    [TestMethod]
    public void RequiredCharMask_Rejects_Candidate_Missing_Required_Symbol()
    {
        // Require every symbol of the query "QUEEN" to appear in candidate.
        ulong required = Engine.BuildCharMask("QUEEN");
        using MyersPattern64Ascii pat = Engine.Prepare("QUEEN");

        // Candidate missing 'Q' must short-circuit.
        Assert.AreEqual(int.MaxValue, Engine.Distance(in pat, "UEENS", maxDist: 5, requiredCharMask: required));
        // Candidate containing every required symbol passes the filter.
        Assert.AreEqual(0, Engine.Distance(in pat, "QUEEN", maxDist: 5, requiredCharMask: required));
        Assert.AreEqual(1, Engine.Distance(in pat, "QUEENS", maxDist: 5, requiredCharMask: required));
    }

    [TestMethod]
    public void RequiredCharMask_Default_Zero_Is_A_Noop()
    {
        // requiredCharMask = 0 means "no required symbols", so behavior must
        // be identical to the no-arg overload.
        using MyersPattern64Ascii pat = Engine.Prepare("HELLO");
        Assert.AreEqual(Engine.Distance(in pat, "HEY"),
                        Engine.Distance(in pat, "HEY", maxDist: int.MaxValue, requiredCharMask: 0UL));
    }

    [TestMethod]
    public void RequiredCharMask_Combined_With_MaxDist_Still_Reports_True_Distance_When_Both_Pass()
    {
        ulong required = Engine.BuildCharMask("HELLO");
        using MyersPattern64Ascii pat = Engine.Prepare("HELLO");
        // "HELLLO" has every required symbol and is within distance 1.
        Assert.AreEqual(1, Engine.Distance(in pat, "HELLLO", maxDist: 2, requiredCharMask: required));
    }
}
