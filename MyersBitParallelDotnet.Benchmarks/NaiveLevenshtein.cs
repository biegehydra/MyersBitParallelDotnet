namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Textbook full-matrix Wagner-Fischer Levenshtein. Allocates an
/// <c>(m+1)*(n+1)</c> matrix every call, mirroring the kind of "obvious"
/// implementation a developer would write before reaching for any
/// optimization.
/// </summary>
public static class NaiveLevenshtein
{
    public static int CaseSensitive(string a, string b) => Calculate(a, b);

    public static int CaseInsensitive(string a, string b)
        => Calculate(a.ToLowerInvariant(), b.ToLowerInvariant());

    private static int Calculate(string a, string b)
    {
        int m = a.Length;
        int n = b.Length;
        if (m == 0) return n;
        if (n == 0) return m;

        var matrix = new int[m + 1, n + 1];
        for (int i = 0; i <= m; i++) matrix[i, 0] = i;
        for (int j = 0; j <= n; j++) matrix[0, j] = j;

        for (int i = 1; i <= m; i++)
        {
            char ai = a[i - 1];
            for (int j = 1; j <= n; j++)
            {
                int cost = ai == b[j - 1] ? 0 : 1;
                int del = matrix[i - 1, j] + 1;
                int ins = matrix[i, j - 1] + 1;
                int sub = matrix[i - 1, j - 1] + cost;
                int min = del < ins ? del : ins;
                if (sub < min) min = sub;
                matrix[i, j] = min;
            }
        }
        return matrix[m, n];
    }
}
