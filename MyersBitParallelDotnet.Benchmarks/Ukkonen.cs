namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Ukkonen's banded edit-distance algorithm. Computes only cells inside a
/// diagonal band of width <c>2k + 1 + (n - m)</c> around the main diagonal,
/// retrying with <c>k = 1, 2, 4, 8, ...</c> until the answer fits inside the
/// band. This gives O(n*d) instead of O(n*m) when the strings are similar
/// (small <c>d</c>) — a common case for fuzzy matching.
/// </summary>
public static class Ukkonen
{
    public static int CaseSensitive(string a, string b) => Calculate(a, b);

    public static int CaseInsensitive(string a, string b)
        => Calculate(a.ToLowerInvariant(), b.ToLowerInvariant());

    /// <summary>
    /// Threshold-aware case-sensitive variant. Bounds the band at
    /// <paramref name="maxDist"/> instead of doubling, so the algorithm
    /// runs in O(n * maxDist) and returns <see cref="int.MaxValue"/> when
    /// the true distance exceeds <paramref name="maxDist"/>.
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
        // Force m <= n so the band offset is non-negative.
        if (a.Length > b.Length) (a, b) = (b, a);

        int m = a.Length;
        int n = b.Length;
        if (m == 0) return n;

        // The minimum possible distance is the length difference; start there
        // and double until we find a band wide enough to hold the answer.
        int k = Math.Max(1, n - m);
        while (true)
        {
            int d = ComputeBanded(a, b, k);
            if (d <= k) return d;
            k *= 2;
        }
    }

    private static int CalculateBounded(string a, string b, int maxDist)
    {
        if (a.Length > b.Length) (a, b) = (b, a);

        int m = a.Length;
        int n = b.Length;

        // Length difference is a hard lower bound; if it already exceeds
        // the threshold no banding is required.
        if (n - m > maxDist) return int.MaxValue;

        if (m == 0) return n <= maxDist ? n : int.MaxValue;

        int d = ComputeBanded(a, b, maxDist);
        return d <= maxDist ? d : int.MaxValue;
    }

    private static int ComputeBanded(string a, string b, int k)
    {
        int m = a.Length;
        int n = b.Length;
        int lenDiff = n - m;
        int sentinel = k + 1;

        int[] prev = new int[n + 1];
        int[] curr = new int[n + 1];

        // Row 0: only the first k+1 cells are reachable within the band.
        for (int j = 0; j <= n; j++)
            prev[j] = j <= k ? j : sentinel;

        for (int i = 1; i <= m; i++)
        {
            // Reset curr to "out of band" everywhere by default.
            Array.Fill(curr, sentinel);

            // Column 0 is reachable only when i fits inside the vertical band.
            if (i <= k) curr[0] = i;

            int jMin = Math.Max(1, i - k);
            int jMax = Math.Min(n, i + lenDiff + k);

            char ai = a[i - 1];
            int rowMin = curr[0];
            for (int j = jMin; j <= jMax; j++)
            {
                int cost = ai == b[j - 1] ? 0 : 1;
                int del = prev[j] + 1;
                int ins = curr[j - 1] + 1;
                int sub = prev[j - 1] + cost;
                int min = del < ins ? del : ins;
                if (sub < min) min = sub;
                if (min > k) min = sentinel;
                curr[j] = min;
                if (min < rowMin) rowMin = min;
            }

            // Whole row exceeded threshold — distance > k, retry with bigger k.
            if (rowMin > k) return sentinel;

            (prev, curr) = (curr, prev);
        }

        return prev[n];
    }
}
