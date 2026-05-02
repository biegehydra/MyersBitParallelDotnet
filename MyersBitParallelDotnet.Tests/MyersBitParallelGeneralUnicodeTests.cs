using System;
using System.Text;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Tests;

[TestClass]
public sealed class MyersBitParallelGeneralUnicodeTests
{
    private static readonly MyersBitParallelGeneralUnicode Engine = MyersBitParallelGeneralUnicode.CaseInsensitive;

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndUnicodeAll), typeof(TestCases))]
    public void Distance_StringString_Matches_Expected(string a, string b, int expected)
    {
        Assert.AreEqual(expected, Engine.Distance(a, b));
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndUnicodeAll), typeof(TestCases))]
    public void Distance_PreparedPattern_Matches_Expected(string a, string b, int expected)
    {
        using MyersPatternGeneralUnicode pat = Engine.Prepare(a);
        Assert.AreEqual(expected, Engine.Distance(in pat, b));
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndUnicodeAll), typeof(TestCases))]
    public void SimilarityRatio_StringString_Matches_Expected(string a, string b, int expected)
    {
        SimilarityRatio result = Engine.SimilarityRatio(a, b);
        int maxLen = Math.Max(TestCases.ScalarCount(a), TestCases.ScalarCount(b));
        double expectedRatio = maxLen == 0 ? 1.0 : 1.0 - ((double)expected / maxLen);
        Assert.AreEqual(expected, result.Distance);
        Assert.AreEqual(expectedRatio, result.Ratio, 1e-12);
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndUnicodeAll), typeof(TestCases))]
    public void SimilarityRatio_PreparedPattern_Matches_Expected(string a, string b, int expected)
    {
        using MyersPatternGeneralUnicode pat = Engine.Prepare(a);
        SimilarityRatio result = Engine.SimilarityRatio(in pat, b);
        int maxLen = Math.Max(TestCases.ScalarCount(a), TestCases.ScalarCount(b));
        double expectedRatio = maxLen == 0 ? 1.0 : 1.0 - ((double)expected / maxLen);
        Assert.AreEqual(expected, result.Distance);
        Assert.AreEqual(expectedRatio, result.Ratio, 1e-12);
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndUnicodeAll), typeof(TestCases))]
    public void Distance_Is_Symmetric(string a, string b, int expected)
    {
        Assert.AreEqual(expected, Engine.Distance(a, b));
        Assert.AreEqual(expected, Engine.Distance(b, a));
    }

    [TestMethod]
    public void Pattern_Length_Reflects_Scalar_Count_Not_Char_Count()
    {
        using MyersPatternGeneralUnicode pat = Engine.Prepare("😀😁😂");
        Assert.AreEqual(3, pat.Length);
    }

    [TestMethod]
    public void Prepare_Allows_Empty_Pattern()
    {
        using MyersPatternGeneralUnicode pat = Engine.Prepare("");
        Assert.AreEqual(0, pat.Length);
        Assert.AreEqual(2, Engine.Distance(in pat, "你好"));
        Assert.AreEqual(0, Engine.Distance(in pat, ""));
    }

    [TestMethod]
    public void Prepare_Allows_Pattern_Of_65_Bmp_Scalars_Without_Throwing()
    {
        string pattern = Repeat("你", 65);
        using MyersPatternGeneralUnicode pat = Engine.Prepare(pattern);
        Assert.AreEqual(65, pat.Length);
        Assert.AreEqual(0, Engine.Distance(in pat, pattern));
        Assert.AreEqual(1, Engine.Distance(in pat, Repeat("你", 64)));
    }

    [TestMethod]
    public void Prepare_Allows_Long_Astral_Pattern_Without_Throwing()
    {
        // 200 surrogate pairs == 200 scalars == 400 chars.
        string pattern = Repeat("😀", 200);
        using MyersPatternGeneralUnicode pat = Engine.Prepare(pattern);
        Assert.AreEqual(200, pat.Length);
        Assert.AreEqual(400, pattern.Length);
        Assert.AreEqual(0, Engine.Distance(in pat, pattern));
        Assert.AreEqual(1, Engine.Distance(in pat, Repeat("😀", 199)));
        Assert.AreEqual(200, Engine.Distance(in pat, Repeat("😁", 200)));
    }

    [TestMethod]
    public void CaseInsensitive_Folds_Ascii_Letters()
    {
        Assert.AreEqual(0, MyersBitParallelGeneralUnicode.CaseInsensitive.Distance("Hello", "HELLO"));
        Assert.AreEqual(1, MyersBitParallelGeneralUnicode.CaseInsensitive.Distance("Hello", "hella"));
    }

    [TestMethod]
    public void CaseSensitive_Distinguishes_Letter_Case()
    {
        Assert.AreEqual(1, MyersBitParallelGeneralUnicode.CaseSensitive.Distance("Hello", "hello"));
        Assert.AreEqual(0, MyersBitParallelGeneralUnicode.CaseSensitive.Distance("HELLO", "HELLO"));
    }

    [TestMethod]
    public void Surrogate_Pair_Counts_As_One_Edit_Not_Two()
    {
        Assert.AreEqual(1, Engine.Distance("😀", ""));
        Assert.AreEqual(1, Engine.Distance("", "😀"));
        Assert.AreEqual(1, Engine.Distance("A😀B", "AB"));
        Assert.AreEqual(1, Engine.Distance("AB", "A😀B"));
    }

    [TestMethod]
    public void Pattern_Can_Be_Reused_Across_Many_Candidates()
    {
        using MyersPatternGeneralUnicode pat = Engine.Prepare("你好世界");
        Assert.AreEqual(0, Engine.Distance(in pat, "你好世界"));
        Assert.AreEqual(1, Engine.Distance(in pat, "你好世間"));
        Assert.AreEqual(2, Engine.Distance(in pat, "你好"));
        Assert.AreEqual(1, Engine.Distance(in pat, "你好世界!"));
        Assert.AreEqual(4, Engine.Distance(in pat, ""));
    }

    [TestMethod]
    public void MaxDist_Returns_MaxValue_When_LengthDifference_Exceeds_Threshold()
    {
        // 100 vs 50 BMP scalars: lenDiff = 50, so maxDist=10 must reject.
        string a = Repeat("你", 100);
        string b = Repeat("你", 50);
        Assert.AreEqual(int.MaxValue, Engine.Distance(a, b, maxDist: 10));
        Assert.AreEqual(int.MaxValue, Engine.Distance(b, a, maxDist: 10));
        Assert.AreEqual(50, Engine.Distance(a, b, maxDist: 50));
    }

    [TestMethod]
    public void MaxDist_Returns_MaxValue_When_Distance_Exceeds_Threshold()
    {
        // résumé -> resume is 2 edits in scalar terms.
        Assert.AreEqual(int.MaxValue, Engine.Distance("résumé", "resume", maxDist: 1));
        Assert.AreEqual(2, Engine.Distance("résumé", "resume", maxDist: 2));
    }

    [TestMethod]
    public void MaxDist_Returns_True_Distance_When_Threshold_Is_Loose()
    {
        Assert.AreEqual(0, Engine.Distance("你好世界", "你好世界", maxDist: 5));
        Assert.AreEqual(1, Engine.Distance("café", "cafe", maxDist: 5));
        Assert.AreEqual(1, Engine.Distance("AB", "A😀B", maxDist: 5));
    }

    [TestMethod]
    public void MaxDist_Works_With_Prepared_Pattern_Reuse()
    {
        using MyersPatternGeneralUnicode pat = Engine.Prepare(Repeat("😀", 100));
        Assert.AreEqual(0, Engine.Distance(in pat, Repeat("😀", 100), maxDist: 1));
        Assert.AreEqual(1, Engine.Distance(in pat, Repeat("😀", 99), maxDist: 1));
        Assert.AreEqual(int.MaxValue, Engine.Distance(in pat, Repeat("😁", 100), maxDist: 99));
        Assert.AreEqual(100, Engine.Distance(in pat, Repeat("😁", 100), maxDist: 100));
    }

    private static string Repeat(string s, int times)
    {
        var sb = new StringBuilder(s.Length * times);
        for (int i = 0; i < times; i++)
            sb.Append(s);
        return sb.ToString();
    }
}
