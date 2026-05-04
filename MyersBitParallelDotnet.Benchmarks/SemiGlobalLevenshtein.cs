namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Reference implementations of the "best substring match" problem that
/// <see cref="MyersBitParallel.MyersSubstringBitParallel64"/> solves:
/// the minimum Levenshtein distance between <c>pattern</c> and any
/// contiguous substring of <c>text</c>.
/// </summary>
/// <remarks>
/// All three implementations produce identical results on any input; they
/// differ only in asymptotic cost and memory footprint:
/// <list type="bullet">
///   <item>
///     <description><see cref="FullMatrix"/> — textbook semi-global DP
///     with an <c>(m+1) × (n+1)</c> matrix. Memory-heavy but easy to
///     read; represents the "obvious" semi-global implementation.</description>
///   </item>
///   <item>
///     <description><see cref="TwoRow"/> — the same recurrence with two
///     rolling <c>(n+1)</c> rows. Same running time, <c>O(n)</c> memory,
///     no per-call allocation beyond the two rows.</description>
///   </item>
///   <item>
///     <description><see cref="NaiveAllSubstrings"/> — brute force:
///     enumerate every <c>(i, j)</c> substring of the text and run
///     full Wagner-Fischer against it. <c>O(n^2 · m · n)</c>; included
///     to represent the "first thing a developer writes" before learning
///     about semi-global DP.</description>
///   </item>
/// </list>
/// The semi-global trick: initialize row 0 to zero (the pattern may start
/// anywhere in the text for free) and take the minimum of the last row
/// (the pattern may end anywhere in the text for free).
/// </remarks>
public static class SemiGlobalLevenshtein
{
    public static int FullMatrixCaseInsensitive(string pattern, string text)
        => FullMatrix(pattern.ToLowerInvariant(), text.ToLowerInvariant());

    public static int TwoRowCaseInsensitive(string pattern, string text)
        => TwoRow(pattern.ToLowerInvariant(), text.ToLowerInvariant());

    public static int NaiveAllSubstringsCaseInsensitive(string pattern, string text)
        => NaiveAllSubstrings(pattern.ToLowerInvariant(), text.ToLowerInvariant());

    public static int FullMatrix(string pattern, string text)
    {
        int m = pattern.Length;
        int n = text.Length;

        if (m == 0) return 0;
        if (n == 0) return m;

        var dp = new int[m + 1, n + 1];
        for (int i = 0; i <= m; i++) dp[i, 0] = i;
        // Row 0 is zero along the text axis — pattern start is free.
        for (int j = 0; j <= n; j++) dp[0, j] = 0;

        for (int i = 1; i <= m; i++)
        {
            char pi = pattern[i - 1];
            for (int j = 1; j <= n; j++)
            {
                int cost = pi == text[j - 1] ? 0 : 1;
                int del = dp[i - 1, j] + 1;
                int ins = dp[i, j - 1] + 1;
                int sub = dp[i - 1, j - 1] + cost;
                int min = del < ins ? del : ins;
                if (sub < min) min = sub;
                dp[i, j] = min;
            }
        }

        int best = dp[m, 0];
        for (int j = 1; j <= n; j++)
        {
            if (dp[m, j] < best) best = dp[m, j];
        }
        return best;
    }

    public static int TwoRow(string pattern, string text)
    {
        int m = pattern.Length;
        int n = text.Length;

        if (m == 0) return 0;
        if (n == 0) return m;

        int[] prev = new int[n + 1];
        int[] curr = new int[n + 1];
        // prev = row 0 = zeros (semi-global init).

        for (int i = 1; i <= m; i++)
        {
            curr[0] = i;
            char pi = pattern[i - 1];
            for (int j = 1; j <= n; j++)
            {
                int cost = pi == text[j - 1] ? 0 : 1;
                int del = prev[j] + 1;
                int ins = curr[j - 1] + 1;
                int sub = prev[j - 1] + cost;
                int min = del < ins ? del : ins;
                if (sub < min) min = sub;
                curr[j] = min;
            }
            (prev, curr) = (curr, prev);
        }

        int best = prev[0];
        for (int j = 1; j <= n; j++)
        {
            if (prev[j] < best) best = prev[j];
        }
        return best;
    }

    /// <summary>
    /// Brute force: enumerate every contiguous substring of <paramref name="text"/>
    /// and compute a full Levenshtein distance against <paramref name="pattern"/>,
    /// returning the minimum. Exists only to stand in for the "obvious first
    /// attempt" a developer would write before discovering the semi-global
    /// trick; do not use in production (O(n^3 · m) in the worst case).
    /// </summary>
    public static int NaiveAllSubstrings(string pattern, string text)
    {
        int m = pattern.Length;
        int n = text.Length;

        if (m == 0) return 0;
        if (n == 0) return m;

        int best = int.MaxValue;
        for (int start = 0; start <= n; start++)
        {
            for (int end = start; end <= n; end++)
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
        if (m == 0) return n;
        if (n == 0) return m;

        int[] prev = new int[n + 1];
        int[] curr = new int[n + 1];
        for (int j = 0; j <= n; j++) prev[j] = j;

        for (int i = 1; i <= m; i++)
        {
            curr[0] = i;
            char ai = a[i - 1];
            for (int j = 1; j <= n; j++)
            {
                int cost = ai == b[j - 1] ? 0 : 1;
                int del = prev[j] + 1;
                int ins = curr[j - 1] + 1;
                int sub = prev[j - 1] + cost;
                int min = del < ins ? del : ins;
                if (sub < min) min = sub;
                curr[j] = min;
            }
            (prev, curr) = (curr, prev);
        }
        return prev[n];
    }
}
