namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Two-row rolling Wagner-Fischer. Same algorithm as
/// <see cref="NaiveLevenshtein"/> but with O(min(m, n)) memory instead of
/// O(m*n). Represents the "first reasonable optimization" everyone reaches
/// for after profiling the naive matrix.
/// </summary>
public static class WagnerFischer
{
    public static int CaseSensitive(string a, string b) => Calculate(a, b);

    public static int CaseInsensitive(string a, string b)
        => Calculate(a.ToLowerInvariant(), b.ToLowerInvariant());

    private static int Calculate(string a, string b)
    {
        // Iterate rows over the shorter string so the rolling buffers stay small.
        if (a.Length > b.Length) (a, b) = (b, a);

        int m = a.Length;
        int n = b.Length;
        if (m == 0) return n;

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
