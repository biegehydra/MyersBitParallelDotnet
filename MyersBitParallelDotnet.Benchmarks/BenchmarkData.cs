namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Shared, deterministic test data and noise generators used by every
/// benchmark class.
/// </summary>
public static class BenchmarkData
{
    /// <summary>
    /// ~100 ASCII city names, mostly &lt; 30 chars. None exceed the 64-symbol
    /// limit so they can be used as patterns by every engine.
    /// </summary>
    public static readonly string[] AsciiCities =
    [
        "Miami", "Orlando", "Tampa", "Jacksonville", "Tallahassee",
        "St Petersburg", "Fort Lauderdale", "Hialeah", "Pembroke Pines", "Hollywood",
        "Gainesville", "Coral Springs", "Miramar", "Pompano Beach", "West Palm Beach",
        "Lakeland", "Davie", "Sunrise", "Boca Raton", "Plantation",
        "Deltona", "Palm Bay", "Largo", "Deerfield Beach", "Boynton Beach",
        "Lauderhill", "Daytona Beach", "Tamarac", "Weston", "Delray Beach",
        "Kissimmee", "Jupiter", "Wellington", "Sanford", "Ocoee",
        "Apopka", "Doral", "Coconut Creek", "North Lauderdale", "Bradenton",
        "Pinellas Park", "Margate", "Sarasota", "North Port", "Coral Gables",
        "Cape Coral", "North Miami", "Riverview", "Brandon", "Fort Myers",
        "Wesley Chapel", "Spring Hill", "Lehigh Acres", "Palm Coast", "Plant City",
        "Royal Palm Beach", "Casselberry", "Greenacres", "Cutler Bay", "Palmetto Bay",
        "South Miami", "Aventura", "North Miami Beach", "Cooper City", "Miami Lakes",
        "Pinecrest", "Winter Garden", "Winter Park", "Winter Springs", "Oviedo",
        "Lake Mary", "Maitland", "Altamonte Springs", "Eustis", "DeBary",
        "DeLand", "Edgewater", "Ormond Beach", "Port Orange", "Holly Hill",
        "New Smyrna Beach", "Bunnell", "Flagler Beach", "Crystal River", "Inverness",
        "Brooksville", "Dade City", "Zephyrhills", "New Port Richey", "Hudson",
        "Land O Lakes", "Lutz", "Trinity", "Odessa", "Citrus Park",
        "Westchase", "Carrollwood", "Egypt Lake Leto", "Lealman", "Safety Harbor",
        "Dunedin", "Oldsmar", "Tarpon Springs", "Palm Harbor", "Clearwater",
    ];

    /// <summary>
    /// Distinct queries used by one-to-many benchmarks (Fisher–Yates shuffle
    /// of <see cref="AsciiCities"/> indices with seed 9001).
    /// </summary>
    public const int OneToManyQueryCount = 8;

    /// <summary>
    /// Build <paramref name="targetCount"/> noisy candidates by repeatedly
    /// permuting strings drawn round-robin from <paramref name="originals"/>.
    /// Deterministic for a given <paramref name="seed"/>.
    /// </summary>
    public static string[] BuildNoisyCandidates(string[] originals, int targetCount, int seed = 42)
    {
        if (targetCount <= 0) return Array.Empty<string>();

        var rnd = new Random(seed);
        var result = new string[targetCount];
        for (int i = 0; i < targetCount; i++)
        {
            string source = originals[i % originals.Length];
            result[i] = Permute(source, rnd);
        }
        return result;
    }

    /// <summary>
    /// Pick <paramref name="count"/> distinct strings from <paramref name="originals"/>
    /// using Fisher–Yates on indices with <paramref name="seed"/> (reproducible).
    /// If <paramref name="count"/> exceeds <paramref name="originals"/>.Length, returns all entries in shuffled order.
    /// </summary>
    public static string[] PickDistinctQueries(string[] originals, int count, int seed = 9001)
    {
        if (originals.Length == 0 || count <= 0)
            return Array.Empty<string>();

        int take = count < originals.Length ? count : originals.Length;
        var indices = new int[originals.Length];
        for (int i = 0; i < indices.Length; i++)
            indices[i] = i;

        var rnd = new Random(seed);
        for (int i = indices.Length - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        var result = new string[take];
        for (int i = 0; i < take; i++)
            result[i] = originals[indices[i]];
        return result;
    }

    /// <summary>
    /// Build <paramref name="targetCount"/> "haystacks" — longer strings in
    /// which a (possibly-noisy) copy of one of <paramref name="patterns"/>
    /// is embedded at a random offset surrounded by filler tokens drawn from
    /// <paramref name="filler"/>. Deterministic for a given <paramref name="seed"/>.
    /// </summary>
    /// <remarks>
    /// Designed for substring / best-match benchmarking: the pattern-sized
    /// window is always present, but it rarely starts at index zero and the
    /// haystack is typically 5–10× longer than the pattern itself.
    /// </remarks>
    public static string[] BuildHaystacks(
        string[] patterns,
        string[] filler,
        int targetCount,
        int minFillerWords = 6,
        int maxFillerWords = 12,
        int seed = 42)
    {
        if (targetCount <= 0) return Array.Empty<string>();

        var rnd = new Random(seed);
        var result = new string[targetCount];
        for (int i = 0; i < targetCount; i++)
        {
            string pattern = patterns[i % patterns.Length];
            string noisy = Permute(pattern, rnd);

            int leftWords = rnd.Next(minFillerWords, maxFillerWords + 1);
            int rightWords = rnd.Next(minFillerWords, maxFillerWords + 1);

            var sb = new System.Text.StringBuilder(noisy.Length + (leftWords + rightWords) * 8);
            for (int w = 0; w < leftWords; w++)
            {
                sb.Append(filler[rnd.Next(filler.Length)]);
                sb.Append(' ');
            }
            sb.Append(noisy);
            for (int w = 0; w < rightWords; w++)
            {
                sb.Append(' ');
                sb.Append(filler[rnd.Next(filler.Length)]);
            }
            result[i] = sb.ToString();
        }
        return result;
    }

    /// <summary>
    /// One haystack list per query slot — deterministic, disjoint RNG
    /// streams, each haystack embeds a noisy copy of its query alongside
    /// filler drawn from <paramref name="filler"/>.
    /// </summary>
    public static string[][] BuildHaystacksPerQuery(
        string[] queries,
        string[] filler,
        int haystackCount,
        int baseSeed = 42)
    {
        var rows = new string[queries.Length][];
        for (int q = 0; q < queries.Length; q++)
        {
            // Each query gets its own pattern array so its own name is the
            // guaranteed embedded substring.
            rows[q] = BuildHaystacks(
                new[] { queries[q] },
                filler,
                haystackCount,
                seed: baseSeed + q * 97_169);
        }
        return rows;
    }

    /// <summary>
    /// Plain-English filler tokens used to pad haystacks around an embedded
    /// (noisy) pattern. All ASCII so every engine can consume them.
    /// </summary>
    public static readonly string[] AsciiFillerWords =
    [
        "THE", "WEATHER", "IN", "REPORT", "NEWS", "UPDATE", "BREAKING",
        "LOCAL", "OFFICIAL", "ANNOUNCED", "STATEMENT", "FROM", "ABOUT",
        "TRAFFIC", "ON", "AT", "WITH", "FOR", "HAS", "WAS", "WILL",
        "TODAY", "YESTERDAY", "TOMORROW", "MORNING", "EVENING",
        "NEAR", "OVER", "UNDER", "BETWEEN", "AFTER", "BEFORE",
        "DRIVER", "PASSENGER", "PHONE", "USER", "RECORD", "FILE",
        "AGENCY", "OFFICER", "POLICE", "COUNCIL", "MAYOR", "GOVERNOR",
        "CONTRACT", "PROJECT", "DISTRICT", "MARKET", "COMPANY",
        "JOHN", "DOE", "JANE", "SMITH", "MARIA", "DAVID", "KEVIN",
    ];

    /// <summary>
    /// One noisy candidate list per query slot (independent RNG streams via <paramref name="baseSeed"/> + offset).
    /// </summary>
    public static string[][] BuildNoisyCandidatesPerQuery(
        string[] originals,
        int queryCount,
        int candidateCount,
        int baseSeed = 42)
    {
        var rows = new string[queryCount][];
        for (int q = 0; q < queryCount; q++)
            rows[q] = BuildNoisyCandidates(originals, candidateCount, baseSeed + q * 97_169);
        return rows;
    }

    /// <summary>
    /// Build <paramref name="pairCount"/> independent (query, candidate) pairs.
    /// A fraction of pairs (<paramref name="nearMatchFraction"/>, default 0.5)
    /// are near-matches: the candidate is a single-edit perturbation of the
    /// query. The rest are unrelated: the candidate is a single-edit
    /// perturbation of a *different* string from <paramref name="originals"/>,
    /// so prefix/suffix-trimming engines (e.g. Quickenshtein) cannot
    /// short-circuit them.
    /// </summary>
    public static (string Query, string Candidate)[] BuildOneToOnePairs(
        string[] originals,
        int pairCount,
        int seed = 1337,
        double nearMatchFraction = 0.5)
    {
        if (originals.Length == 0 || pairCount <= 0)
            return Array.Empty<(string, string)>();

        if (nearMatchFraction < 0) nearMatchFraction = 0;
        else if (nearMatchFraction > 1) nearMatchFraction = 1;

        var rnd = new Random(seed);
        var pairs = new (string, string)[pairCount];
        for (int i = 0; i < pairCount; i++)
        {
            string query = originals[i % originals.Length];
            string source;
            if (originals.Length == 1 || rnd.NextDouble() < nearMatchFraction)
            {
                source = query;
            }
            else
            {
                int otherIdx = rnd.Next(originals.Length - 1);
                if (otherIdx >= i % originals.Length) otherIdx++;
                source = originals[otherIdx];
            }

            string candidate = Permute(source, rnd);
            pairs[i] = (query, candidate);
        }
        return pairs;
    }

    /// <summary>
    /// Apply a single random insertion, deletion, or substitution. Operates
    /// on UTF-16 code units, which is sufficient for the ASCII data and good
    /// enough for the BMP-heavy Unicode data used here.
    /// </summary>
    private static string Permute(string input, Random rnd)
    {
        if (input.Length == 0) return input;

        int action = rnd.Next(3);
        switch (action)
        {
            case 0 when input.Length > 1:
            {
                int del = rnd.Next(input.Length);
                return input.Remove(del, 1);
            }
            case 1:
            {
                int pos = rnd.Next(input.Length + 1);
                char insert = input[rnd.Next(input.Length)];
                return input.Insert(pos, insert.ToString());
            }
            case 2:
            {
                int idx = rnd.Next(input.Length);
                char[] chars = input.ToCharArray();
                chars[idx] = chars[idx] == 'A' ? 'E' : (char)(chars[idx] + 1);
                return new string(chars);
            }
            default:
                return input;
        }
    }
}
