using System;
using System.Text;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Tests;

[TestClass]
public sealed class MyersBitParallel64UnicodeTests
{
    private static readonly MyersBitParallel64Unicode Engine = MyersBitParallel64Unicode.CaseInsensitive;

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndUnicode), typeof(TestCases))]
    public void Distance_StringString_Matches_Expected(string a, string b, int expected)
    {
        Assert.AreEqual(expected, Engine.Distance(a, b));
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndUnicode), typeof(TestCases))]
    public void Distance_PreparedPattern_Matches_Expected(string a, string b, int expected)
    {
        using MyersPattern64Unicode pat = Engine.Prepare(a);
        Assert.AreEqual(expected, Engine.Distance(in pat, b));
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndUnicode), typeof(TestCases))]
    public void SimilarityRatio_StringString_Matches_Expected(string a, string b, int expected)
    {
        SimilarityRatio result = Engine.SimilarityRatio(a, b);
        int maxLen = Math.Max(TestCases.ScalarCount(a), TestCases.ScalarCount(b));
        double expectedRatio = maxLen == 0 ? 1.0 : 1.0 - ((double)expected / maxLen);
        Assert.AreEqual(expected, result.Distance);
        Assert.AreEqual(expectedRatio, result.Ratio, 1e-12);
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndUnicode), typeof(TestCases))]
    public void SimilarityRatio_PreparedPattern_Matches_Expected(string a, string b, int expected)
    {
        using MyersPattern64Unicode pat = Engine.Prepare(a);
        SimilarityRatio result = Engine.SimilarityRatio(in pat, b);
        int maxLen = Math.Max(TestCases.ScalarCount(a), TestCases.ScalarCount(b));
        double expectedRatio = maxLen == 0 ? 1.0 : 1.0 - ((double)expected / maxLen);
        Assert.AreEqual(expected, result.Distance);
        Assert.AreEqual(expectedRatio, result.Ratio, 1e-12);
    }

    [TestMethod]
    [DynamicData(nameof(TestCases.AsciiAndUnicode), typeof(TestCases))]
    public void Distance_Is_Symmetric(string a, string b, int expected)
    {
        Assert.AreEqual(expected, Engine.Distance(a, b));
        Assert.AreEqual(expected, Engine.Distance(b, a));
    }

    [TestMethod]
    public void Pattern_Length_Reflects_Scalar_Count_Not_Char_Count()
    {
        // Three astral scalars rendered as six UTF-16 chars.
        using MyersPattern64Unicode pat = Engine.Prepare("😀😁😂");
        Assert.AreEqual(3, pat.Length);
    }

    [TestMethod]
    public void Prepare_Allows_Empty_Pattern()
    {
        using MyersPattern64Unicode pat = Engine.Prepare("");
        Assert.AreEqual(0, pat.Length);
        Assert.AreEqual(2, Engine.Distance(in pat, "你好"));
        Assert.AreEqual(0, Engine.Distance(in pat, ""));
    }

    [TestMethod]
    public void Prepare_Allows_Pattern_Of_Exactly_64_Bmp_Scalars()
    {
        string pattern = Repeat("你", 64);
        using MyersPattern64Unicode pat = Engine.Prepare(pattern);
        Assert.AreEqual(64, pat.Length);
        Assert.AreEqual(0, Engine.Distance(in pat, pattern));
        Assert.AreEqual(1, Engine.Distance(in pat, Repeat("你", 63)));
    }

    [TestMethod]
    public void Prepare_Allows_Pattern_Of_Exactly_64_Astral_Scalars()
    {
        string pattern = Repeat("😀", 64);
        using MyersPattern64Unicode pat = Engine.Prepare(pattern);
        Assert.AreEqual(64, pat.Length);
        Assert.AreEqual(128, pattern.Length);
        Assert.AreEqual(0, Engine.Distance(in pat, pattern));
    }

    [TestMethod]
    public void Prepare_Throws_When_Pattern_Exceeds_64_Bmp_Scalars()
    {
        string pattern = Repeat("你", 65);
        Assert.ThrowsExactly<ArgumentException>(() => Engine.Prepare(pattern));
    }

    [TestMethod]
    public void Prepare_Throws_When_Pattern_Exceeds_64_Astral_Scalars()
    {
        // 65 surrogate pairs == 65 scalars == 130 chars.
        string pattern = Repeat("😀", 65);
        Assert.ThrowsExactly<ArgumentException>(() => Engine.Prepare(pattern));
    }

    [TestMethod]
    public void Distance_Throws_When_Pattern_Exceeds_64_Scalars()
    {
        string pattern = Repeat("A", 65);
        Assert.ThrowsExactly<ArgumentException>(() => Engine.Distance(pattern, "A"));
    }

    [TestMethod]
    public void Pattern_With_64_BmpChars_Counts_As_64_Scalars_Not_64_Bytes()
    {
        // 64 ASCII chars == 64 scalars: at the limit, not over.
        string pattern = new('A', 64);
        using MyersPattern64Unicode pat = Engine.Prepare(pattern);
        Assert.AreEqual(64, pat.Length);
    }

    [TestMethod]
    public void CaseInsensitive_Folds_Ascii_Letters()
    {
        Assert.AreEqual(0, MyersBitParallel64Unicode.CaseInsensitive.Distance("Hello", "HELLO"));
        Assert.AreEqual(0, MyersBitParallel64Unicode.CaseInsensitive.Distance("KITTEN", "kitten"));
    }

    [TestMethod]
    public void CaseSensitive_Distinguishes_Letter_Case()
    {
        Assert.AreEqual(1, MyersBitParallel64Unicode.CaseSensitive.Distance("Hello", "hello"));
        Assert.AreEqual(0, MyersBitParallel64Unicode.CaseSensitive.Distance("HELLO", "HELLO"));
    }

    [TestMethod]
    public void Surrogate_Pair_Counts_As_One_Edit_Not_Two()
    {
        // The astral emoji is a single Unicode scalar value, so adding or
        // removing it costs exactly one edit.
        Assert.AreEqual(1, Engine.Distance("😀", ""));
        Assert.AreEqual(1, Engine.Distance("", "😀"));
        Assert.AreEqual(1, Engine.Distance("A😀B", "AB"));
        Assert.AreEqual(1, Engine.Distance("AB", "A😀B"));
    }

    [TestMethod]
    public void Pattern_Can_Be_Reused_Across_Many_Candidates()
    {
        using MyersPattern64Unicode pat = Engine.Prepare("café");
        Assert.AreEqual(0, Engine.Distance(in pat, "café"));
        Assert.AreEqual(0, Engine.Distance(in pat, "Café"));
        Assert.AreEqual(1, Engine.Distance(in pat, "cafe"));
        Assert.AreEqual(1, Engine.Distance(in pat, "café!"));
        Assert.AreEqual(4, Engine.Distance(in pat, ""));
    }

    [TestMethod]
    public void Lone_Surrogate_Is_Treated_As_Standalone_Code_Unit()
    {
        // Unpaired high surrogate followed by a non-surrogate char: the
        // engine must not crash and must treat the lone surrogate as a single
        // scalar slot rather than swallowing the next character.
        string pattern = "\uD83DA"; // lone high surrogate + 'A'
        using MyersPattern64Unicode pat = Engine.Prepare(pattern);
        Assert.AreEqual(2, pat.Length);
    }

    private static string Repeat(string s, int times)
    {
        var sb = new StringBuilder(s.Length * times);
        for (int i = 0; i < times; i++)
            sb.Append(s);
        return sb.ToString();
    }
}
