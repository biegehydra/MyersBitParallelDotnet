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

    /// <summary>
    /// Threshold-aware case-sensitive variant. Adds a length-difference
    /// gate and a final result check; the DP itself still runs to
    /// completion (Wagner-Fischer doesn't have a safe in-loop cutoff
    /// without becoming Ukkonen — see <see cref="Ukkonen"/> for that).
    /// Returns <see cref="int.MaxValue"/> when the distance exceeds
    /// <paramref name="maxDist"/>.
    /// </summary>
    public static int CaseSensitive(string a, string b, int maxDist)
        => CalculateBounded(a, b, maxDist);

    /// <summary>
    /// Threshold-aware case-insensitive variant. See
    /// <see cref="CaseSensitive(string, string, int)"/>.
    /// </summary>
    public static int CaseInsensitive(string a, string b, int maxDist)
        => CalculateBounded(a.ToLowerInvariant(), b.ToLowerInvariant(), maxDist);

    private static int Calculate(string a, string b)
    {
        // Iterate rows over the shorter string so the rolling buffers stay small.
        if (a.Length > b.Length) (a, b) = (b, a);

        int m = a.Length;
        int n = b.Length;
        if (m == 0) return n;

        return CalculateCore(a, b, m, n);
    }

    private static int CalculateBounded(string a, string b, int maxDist)
    {
        if (a.Length > b.Length) (a, b) = (b, a);

        int m = a.Length;
        int n = b.Length;

        if (n - m > maxDist) return int.MaxValue;
        if (m == 0) return n <= maxDist ? n : int.MaxValue;

        int d = CalculateCore(a, b, m, n);
        return d <= maxDist ? d : int.MaxValue;
    }

    private static int CalculateCore(string a, string b, int m, int n)
    {
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
