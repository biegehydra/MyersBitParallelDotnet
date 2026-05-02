using System;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Tests;

[TestClass]
public sealed class MyersBitParallelGeneralAsciiTests
{
    private static readonly MyersBitParallelGeneralAscii Engine = MyersBitParallelGeneralAscii.CaseInsensitive;

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndAsciiLong), typeof(TestCases))]
    public void Distance_StringString_Matches_Expected(string a, string b, int expected)
    {
        Assert.AreEqual(expected, Engine.Distance(a, b));
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndAsciiLong), typeof(TestCases))]
    public void Distance_PreparedPattern_Matches_Expected(string a, string b, int expected)
    {
        using MyersPatternGeneralAscii pat = Engine.Prepare(a);
        Assert.AreEqual(expected, Engine.Distance(in pat, b));
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndAsciiLong), typeof(TestCases))]
    public void SimilarityRatio_StringString_Matches_Expected(string a, string b, int expected)
    {
        SimilarityRatio result = Engine.SimilarityRatio(a, b);
        int maxLen = Math.Max(a.Length, b.Length);
        double expectedRatio = maxLen == 0 ? 1.0 : 1.0 - ((double)expected / maxLen);
        Assert.AreEqual(expected, result.Distance);
        Assert.AreEqual(expectedRatio, result.Ratio, 1e-12);
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndAsciiLong), typeof(TestCases))]
    public void SimilarityRatio_PreparedPattern_Matches_Expected(string a, string b, int expected)
    {
        using MyersPatternGeneralAscii pat = Engine.Prepare(a);
        SimilarityRatio result = Engine.SimilarityRatio(in pat, b);
        int maxLen = Math.Max(a.Length, b.Length);
        double expectedRatio = maxLen == 0 ? 1.0 : 1.0 - ((double)expected / maxLen);
        Assert.AreEqual(expected, result.Distance);
        Assert.AreEqual(expectedRatio, result.Ratio, 1e-12);
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndAsciiLong), typeof(TestCases))]
    public void Distance_Is_Symmetric(string a, string b, int expected)
    {
        Assert.AreEqual(expected, Engine.Distance(a, b));
        Assert.AreEqual(expected, Engine.Distance(b, a));
    }

    [TestMethod]
    public void Prepare_Allows_Empty_Pattern()
    {
        using MyersPatternGeneralAscii pat = Engine.Prepare("");
        Assert.AreEqual(0, pat.Length);
        Assert.AreEqual(5, Engine.Distance(in pat, "HELLO"));
        Assert.AreEqual(0, Engine.Distance(in pat, ""));
    }

    [TestMethod]
    public void Prepare_Allows_Pattern_Of_65_Chars_Without_Throwing()
    {
        // The whole point of the General engine: no 64-symbol cap.
        string pattern = new('A', 65);
        using MyersPatternGeneralAscii pat = Engine.Prepare(pattern);
        Assert.AreEqual(65, pat.Length);
        Assert.AreEqual(0, Engine.Distance(in pat, pattern));
    }

    [TestMethod]
    public void Prepare_Allows_Very_Long_Pattern()
    {
        string pattern = new('A', 5000);
        using MyersPatternGeneralAscii pat = Engine.Prepare(pattern);
        Assert.AreEqual(5000, pat.Length);
        Assert.AreEqual(0, Engine.Distance(in pat, pattern));
        Assert.AreEqual(1, Engine.Distance(in pat, new string('A', 4999)));
        Assert.AreEqual(2, Engine.Distance(in pat, new string('A', 4998)));
        Assert.AreEqual(5000, Engine.Distance(in pat, new string('B', 5000)));
    }

    [TestMethod]
    public void CaseInsensitive_Default_Folds_Ascii_Letters()
    {
        Assert.AreEqual(0, MyersBitParallelGeneralAscii.CaseInsensitive.Distance("Hello", "HELLO"));
        Assert.AreEqual(1, MyersBitParallelGeneralAscii.CaseInsensitive.Distance("Hello", "hella"));
    }

    [TestMethod]
    public void CaseSensitive_Distinguishes_Letter_Case()
    {
        Assert.AreEqual(1, MyersBitParallelGeneralAscii.CaseSensitive.Distance("Hello", "hello"));
        Assert.AreEqual(5, MyersBitParallelGeneralAscii.CaseSensitive.Distance("HELLO", "hello"));
    }

    [TestMethod]
    public void Pattern_Can_Be_Reused_Across_Many_Candidates()
    {
        using MyersPatternGeneralAscii pat = Engine.Prepare(new string('A', 100));
        Assert.AreEqual(0, Engine.Distance(in pat, new string('A', 100)));
        Assert.AreEqual(1, Engine.Distance(in pat, new string('A', 99)));
        Assert.AreEqual(1, Engine.Distance(in pat, new string('A', 101)));
        Assert.AreEqual(100, Engine.Distance(in pat, new string('B', 100)));
        Assert.AreEqual(100, Engine.Distance(in pat, ""));
    }

    [TestMethod]
    public void Custom_Mapper_Is_Invoked_Only_Once_Per_Byte_Value()
    {
        int invocationCount = 0;
        var engine = new MyersBitParallelGeneralAscii(c =>
        {
            invocationCount++;
            return AsciiMappers.CaseInsensitive(c);
        });

        Assert.AreEqual(256, invocationCount);

        for (int i = 0; i < 1000; i++)
            engine.Distance("hello world", "hello world");

        Assert.AreEqual(256, invocationCount);
    }

    [TestMethod]
    public void Long_Pattern_Vs_Empty_Candidate_Equals_Pattern_Length()
    {
        string pattern = new('A', 1000);
        Assert.AreEqual(1000, Engine.Distance(pattern, ""));
        Assert.AreEqual(1000, Engine.Distance("", pattern));
    }
}
