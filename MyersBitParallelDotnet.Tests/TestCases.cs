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

    /// <summary>
    /// Substring-match cases: <c>expected</c> is the minimum Levenshtein
    /// distance between <c>pattern</c> and any contiguous substring of
    /// <c>text</c>. Evaluated with the case-insensitive ASCII engine, so
    /// letter case never contributes to the returned distance.
    /// </summary>
    public static IEnumerable<object[]> SubstringAscii()
    {
        // Exact substring hits at different positions of the text.
        yield return new object[] { "CAT", "THE CAT SAT", 0 };
        yield return new object[] { "CAT", "CATNIP", 0 };
        yield return new object[] { "WORLD", "HELLO WORLD", 0 };
        yield return new object[] { "LO WOR", "HELLO WORLD", 0 };
        yield return new object[] { "HELLO", "SAY HELLO TO THEM", 0 };

        // The motivating regression case: "BOAT" in "THE LONG MOAT OF THE
        // CASTLE" with the best window "MOAT" is exactly one substitution.
        yield return new object[] { "BOAT", "THE LONG MOAT OF THE CASTLE", 1 };

        // Pattern equals text.
        yield return new object[] { "HELLO WORLD", "HELLO WORLD", 0 };
        yield return new object[] { "TEST", "TEST", 0 };
        yield return new object[] { "A", "A", 0 };
        yield return new object[] { "", "", 0 };

        // Distance-1 approximate substring matches (one sub / ins / del).
        yield return new object[] { "KITTEN", "I HAVE A KITTIN HERE", 1 };   // sub
        yield return new object[] { "HELLO", "SAY HELO THERE", 1 };          // del
        yield return new object[] { "HELLO", "HELLLO WORLD", 1 };            // ins
        yield return new object[] { "BANANA", "HE ATE A BAANA", 1 };         // del
        yield return new object[] { "WORLD", "HELLO WRLD", 1 };              // suffix, del
        yield return new object[] { "CAT", "MY CXT IS HERE", 1 };            // middle, sub

        // Distance-2 / distance-3 approximate substring matches.
        yield return new object[] { "KITTEN", "THAT KITCHEN SMELLS", 2 };
        // SATURDAY → SUNDAY is the classic distance-3 pair, but the best
        // substring here is "TURDAY" (2 subs against "SUNDAY").
        yield return new object[] { "SUNDAY", "MEETING ON SATURDAY", 2 };
        // EXECUTION → INTENTION is distance 5, but a 7-char window of the
        // text beats the full 9-char one, so the best is 4.
        yield return new object[] { "EXECUTION", "AFTER THE INTENTION", 4 };

        // Case-insensitive folding (CaseInsensitive is the engine under test).
        yield return new object[] { "Hello", "SAY HELLO WORLD", 0 };
        yield return new object[] { "kitten", "THE KITTEN SAT", 0 };
        yield return new object[] { "Hello", "HELLA WORLD", 1 };

        // Empty pattern → distance 0 (the empty substring always exists).
        yield return new object[] { "", "ANYTHING", 0 };
        yield return new object[] { "", "HELLO WORLD", 0 };

        // Empty text → distance = |pattern| (only substring available is "").
        yield return new object[] { "HELLO", "", 5 };
        yield return new object[] { "A", "", 1 };
        yield return new object[] { "ABC", "", 3 };

        // Pattern longer than text: the min is over substrings of text, so
        // the best we can do is compare pattern to the whole text (or any
        // shorter window).
        yield return new object[] { "CAT", "C", 2 };                         // DP(CAT, C) = 2
        yield return new object[] { "HELLO", "LL", 3 };                      // DP(HELLO, LL) = 3
        yield return new object[] { "HELLO WORLD", "WORLD", 6 };             // 6-char trim

        // Pattern completely disjoint from text → distance = |pattern|.
        yield return new object[] { "ABC", "XYZXYZ", 3 };
        yield return new object[] { "AAA", "BBBBB", 3 };
        yield return new object[] { "QZX", "HELLO WORLD", 3 };

        // Repeats — best alignment is still 0 because an exact copy is in.
        yield return new object[] { "AAAA", "AAAAAAAAAA", 0 };
        yield return new object[] { "ABCABC", "XYZABCABCXYZ", 0 };
        // No 4-char window of "AB AB AB AB" equals "ABAB" — the spaces
        // force at least one edit (best window "AB AB" loses 1 space).
        yield return new object[] { "ABAB", "AB AB AB AB", 1 };

        // Cases that would fail a prefix-only or global implementation:
        // pattern appears near the END of text (not at the start).
        yield return new object[] { "AB", "XAB", 0 };
        yield return new object[] { "AB", "XYAB", 0 };
        yield return new object[] { "AB", "ZZZABZZZ", 0 };
        yield return new object[] { "CAT", "DOGCATFISH", 0 };
        yield return new object[] { "HELLO", "SAY HELLO WORLD", 0 };
        yield return new object[] { "ZZZ", "AAAZZZ", 0 };

        // DNA-style windowed searches.
        yield return new object[] { "AGGC", "TTAAGGCTTT", 0 };
        yield return new object[] { "AGGC", "TTACCCTTT", 2 };                // best window ACCC: 2 subs
        yield return new object[] { "GATTACA", "TTGATTACATT", 0 };

        // Real-world identifier / PII fuzzy lookup.
        yield return new object[] { "jsmith", "CONTACTS: JOHN JSMITH, JANE DOE", 0 };
        yield return new object[] { "jsmith", "USER_ID=JSMTH42", 1 };        // del
        yield return new object[] { "jsmith", "ALIAS JASMITH HERE", 1 };     // ins

        // Punctuation / whitespace variance.
        yield return new object[] { "USER-NAME", "THE USER_NAME FIELD", 1 };
        yield return new object[] { "12345", "PIN IS 12345 TODAY", 0 };
        yield return new object[] { "12345", "PIN IS 1234 TODAY", 1 };
        yield return new object[] { "EMAIL@DOMAIN", "EMAIL.DOMAIN", 1 };

        // Pattern at exactly 64 chars (the single-word engine limit).
        yield return new object[] { new string('A', 64), new string('A', 64), 0 };
        yield return new object[] { new string('A', 64), new string('B', 50) + new string('A', 64) + new string('B', 50), 0 };
        yield return new object[] { new string('A', 64), new string('A', 63), 1 };
        yield return new object[] { new string('A', 64), new string('B', 64), 64 };

        // Very long text (much larger than 64).
        yield return new object[] { "HELLO", new string('X', 500) + "HELLO" + new string('X', 500), 0 };
        yield return new object[] { "HELLO", new string('X', 500) + "HELL" + new string('X', 500), 1 };
        yield return new object[] { "HELLO", new string('X', 500), 5 };

        // Single-char pattern.
        yield return new object[] { "A", "BANANA", 0 };
        yield return new object[] { "A", "BCD", 1 };
        yield return new object[] { "Z", "HELLO WORLD", 1 };
    }
}
