using System.Collections.Generic;

namespace MyersBitParallelDotnet.Tests;

/// <summary>
/// Shared <see cref="DynamicDataAttribute"/> sources for the engine test
/// classes. Each source returns rows of <c>{ string a, string b, int expectedDistance }</c>.
/// </summary>
public static class TestCases
{
    /// <summary>
    /// Pure-ASCII cases that fit inside the 64-symbol pattern limit.
    /// </summary>
    public static IEnumerable<object[]> Ascii()
    {
        // Exact matches.
        yield return new object[] { "HELLO WORLD", "HELLO WORLD", 0 };
        yield return new object[] { "TEST", "TEST", 0 };
        yield return new object[] { "A", "A", 0 };
        yield return new object[] { "", "", 0 };
        yield return new object[] { "ABCDEFGHIJKLMNOPQRSTUVWXYZ", "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 0 };

        // Single substitutions.
        yield return new object[] { "WHITE PINU", "WHITE PINE", 1 };
        yield return new object[] { "KITTEN", "SITTEN", 1 };
        yield return new object[] { "KITTEN", "KITTIN", 1 };
        yield return new object[] { "KITTEN", "KITTES", 1 };
        yield return new object[] { "JAVA", "LAVA", 1 };
        yield return new object[] { "CSHARP", "ASHARP", 1 };

        // Single deletions.
        yield return new object[] { "TESTING", "TESTIN", 1 };
        yield return new object[] { "TESTING", "ESTING", 1 };
        yield return new object[] { "TESTING", "TESING", 1 };
        yield return new object[] { "BANANA", "BAANA", 1 };
        yield return new object[] { "AAA", "AA", 1 };

        // Single insertions.
        yield return new object[] { "TESTIN", "TESTING", 1 };
        yield return new object[] { "ESTING", "TESTING", 1 };
        yield return new object[] { "TESING", "TESTING", 1 };
        yield return new object[] { "HELLO", "HELLLO", 1 };
        yield return new object[] { "AA", "AAA", 1 };

        // Mixed distance-2.
        yield return new object[] { "BOOK", "BACK", 2 };
        yield return new object[] { "HELLO", "HEL", 2 };
        yield return new object[] { "ABC", "ABCDE", 2 };
        yield return new object[] { "LEMON", "MELON", 2 };
        yield return new object[] { "FAST", "CATS", 3 };
        yield return new object[] { "SUNDAY", "SATURDAY", 3 };

        // Specific distance-2 cases.
        yield return new object[] { "PAPER", "PAGES", 2 };
        yield return new object[] { "GHOST", "GOATS", 3 };
        yield return new object[] { "GLOW", "SLOW", 1 };
        yield return new object[] { "MISSISSIPPI", "MISSISSIPPI", 0 };
        yield return new object[] { "MISSISSIPPI", "MISSISIPPI", 1 };

        // Distance-2 / 3 cases involving inserts.
        yield return new object[] { "KITTEN", "KITCHEN", 2 };
        yield return new object[] { "STOP", "TOPS", 2 };
        yield return new object[] { "ROSETTA", "RESET", 3 };

        // Short / edge.
        yield return new object[] { "A", "B", 1 };
        yield return new object[] { "A", "", 1 };
        yield return new object[] { "", "A", 1 };
        yield return new object[] { "AB", "AC", 1 };
        yield return new object[] { "AB", "BA", 2 };
        yield return new object[] { "ABC", "", 3 };

        // Long, but within the 64-symbol limit.
        yield return new object[] { new string('A', 60), new string('A', 60), 0 };
        yield return new object[] { new string('A', 60), 'B' + new string('A', 59), 1 };
        yield return new object[] { new string('A', 64), new string('A', 64), 0 };
        yield return new object[] { new string('A', 64), 'B' + new string('A', 63), 1 };
        yield return new object[] { new string('A', 64), new string('A', 63), 1 };
        yield return new object[] { new string('A', 64), new string('B', 64), 64 };

        // Completely different.
        yield return new object[] { "ABC", "XYZ", 3 };
        yield return new object[] { "AAAA", "BBBB", 4 };

        // Trailing / leading whitespace.
        yield return new object[] { " WORD", "WORD", 1 };
        yield return new object[] { "WORD ", "WORD", 1 };
        yield return new object[] { " WORD ", "WORD", 2 };

        // ASCII punctuation.
        yield return new object[] { "USER-NAME", "USERNAME", 1 };
        yield return new object[] { "USER_NAME", "USER-NAME", 1 };
        yield return new object[] { "12345", "123456", 1 };
        yield return new object[] { "EMAIL@DOMAIN", "EMAIL.DOMAIN", 1 };

        // Repeats / DNA-style.
        yield return new object[] { "ABABAB", "ABAB", 2 };
        yield return new object[] { "XYXYXY", "YXYXYX", 2 };
        yield return new object[] { "INTENTION", "EXECUTION", 5 };
        yield return new object[] { "AGGCTATGC", "AGGCGTATGC", 1 };
        yield return new object[] { "ORANGE", "APPLE", 5 };

        // Larger gaps.
        yield return new object[] { "SHORT", "LONGERSTRING", 10 };
        yield return new object[] { "ABCDEF", "GHIJKL", 6 };
    }
}
