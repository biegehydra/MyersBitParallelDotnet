using BenchmarkDotNet.Attributes;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Several distinct ASCII queries (deterministic random pick) each compared
/// against many longer "haystack" strings. Each haystack contains a single
/// noisy copy of its query embedded at a random offset, so the best-match
/// distance is small but the full-string Levenshtein distance is not — the
/// pattern must be located inside the text.
/// </summary>
/// <remarks>
/// Apples-to-apples with
/// <see cref="MyersBitParallel.MyersSubstringBitParallel64.BestMatchDistance(in MyersSubstringPattern64, string)"/>:
/// every reference algorithm in <see cref="SemiGlobalLevenshtein"/>
/// computes the same minimum-over-substrings quantity.
/// </remarks>
[MemoryDiagnoser]
public class OneToManyBestMatch64Benchmark
{
    [Params(100, 1000)]
    public int HaystackCount;

    private string[] _queries = null!;
    private string[][] _haystacksByQuery = null!;

    private static readonly MyersSubstringBitParallel64 Engine = MyersSubstringBitParallel64.CaseInsensitive;

    [GlobalSetup]
    public void Setup()
    {
        _queries = BenchmarkData.PickDistinctQueries(
            BenchmarkData.AsciiCities,
            BenchmarkData.OneToManyQueryCount);
        _haystacksByQuery = BenchmarkData.BuildHaystacksPerQuery(
            _queries,
            BenchmarkData.AsciiFillerWords,
            HaystackCount);
    }

    [Benchmark(Baseline = true)]
    public long Myers_PreparedOnce()
    {
        long sum = 0;
        for (int q = 0; q < _queries.Length; q++)
        {
            string query = _queries[q];
            string[] haystacks = _haystacksByQuery[q];
            using MyersSubstringPattern64 pat = Engine.Prepare(query);
            for (int i = 0; i < haystacks.Length; i++)
            {
                sum += Engine.BestMatchDistance(in pat, haystacks[i]);
            }
        }
        return sum;
    }

    [Benchmark]
    public long Myers_PerCallPrepare()
    {
        long sum = 0;
        for (int q = 0; q < _queries.Length; q++)
        {
            string query = _queries[q];
            string[] haystacks = _haystacksByQuery[q];
            for (int i = 0; i < haystacks.Length; i++)
            {
                sum += Engine.BestMatchDistance(query, haystacks[i]);
            }
        }
        return sum;
    }

    [Benchmark]
    public long SemiGlobal_TwoRow()
    {
        long sum = 0;
        for (int q = 0; q < _queries.Length; q++)
        {
            string query = _queries[q];
            string[] haystacks = _haystacksByQuery[q];
            for (int i = 0; i < haystacks.Length; i++)
            {
                sum += SemiGlobalLevenshtein.TwoRowCaseInsensitive(query, haystacks[i]);
            }
        }
        return sum;
    }

    [Benchmark]
    public long SemiGlobal_FullMatrix()
    {
        long sum = 0;
        for (int q = 0; q < _queries.Length; q++)
        {
            string query = _queries[q];
            string[] haystacks = _haystacksByQuery[q];
            for (int i = 0; i < haystacks.Length; i++)
            {
                sum += SemiGlobalLevenshtein.FullMatrixCaseInsensitive(query, haystacks[i]);
            }
        }
        return sum;
    }
}
