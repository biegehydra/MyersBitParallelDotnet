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
    /// ~100 international city names mixing Latin-1 supplement, Latin
    /// extended, CJK BMP, and a handful of surrogate-pair samples.
    /// </summary>
    public static readonly string[] UnicodeCities =
    [
        "São Paulo", "Rio de Janeiro", "Brasília", "Salvador", "Belém",
        "México", "Mérida", "Cancún", "León", "Querétaro",
        "Bogotá", "Medellín", "Cúcuta", "Cartagena", "Barranquilla",
        "Buenos Aires", "Córdoba", "Mendoza", "Tucumán", "Resistencia",
        "Santiago", "Concepción", "Valparaíso", "Antofagasta", "Iquique",
        "Madrid", "Barcelona", "Sevilla", "Málaga", "Zaragoza",
        "Valencia", "Bilbao", "Granada", "A Coruña", "Logroño",
        "Paris", "Marseille", "Bordeaux", "Toulouse", "Nantes",
        "Lyon", "Strasbourg", "Montpellier", "Nice", "Rennes",
        "München", "Köln", "Düsseldorf", "Nürnberg", "Stuttgart",
        "Frankfurt am Main", "Würzburg", "Lübeck", "Saarbrücken", "Osnabrück",
        "Wien", "Salzburg", "Innsbruck", "Linz", "Graz",
        "Zürich", "Genève", "Bern", "Lausanne", "Basel",
        "København", "Århus", "Odense", "Ålborg", "Esbjerg",
        "Stockholm", "Göteborg", "Malmö", "Helsingborg", "Västerås",
        "Helsinki", "Tampere", "Jyväskylä", "Lahti", "Pori",
        "Warszawa", "Kraków", "Łódź", "Wrocław", "Poznań",
        "Praha", "Brno", "Plzeň", "Liberec", "České Budějovice",
        "東京", "大阪", "京都", "横浜", "札幌",
        "北京", "上海", "广州", "深圳", "成都",
        "首爾", "釜山", "仁川", "🌴 Hawaii", "🗼 Tokyo",
    ];

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
    /// Build <paramref name="pairCount"/> independent (query, candidate)
    /// pairs by selecting strings round-robin from <paramref name="originals"/>
    /// and applying a single random edit to each candidate.
    /// </summary>
    public static (string Query, string Candidate)[] BuildOneToOnePairs(
        string[] originals,
        int pairCount,
        int seed = 1337)
    {
        var rnd = new Random(seed);
        var pairs = new (string, string)[pairCount];
        for (int i = 0; i < pairCount; i++)
        {
            string query = originals[i % originals.Length];
            string candidate = Permute(query, rnd);
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
