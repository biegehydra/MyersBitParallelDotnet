using BenchmarkDotNet.Attributes;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Several distinct ASCII queries (deterministic random pick), each compared
/// against many ASCII candidates with fuzzy-search threshold <c>maxDist</c>.
/// </summary>
/// <remarks>
/// Myers variants: with <c>maxDist</c> (bounded kernel) vs without. Reference
/// algorithms use the same <c>maxDist</c> where supported.
/// </remarks>
[MemoryDiagnoser]
public class OneToManyMaxDist64Benchmark
{
    [Params(1, 2, 4)]
    public int MaxDist;

    [Params(1000)]
    public int CandidateCount;

    private string[] _queries = null!;
    private string[][] _candidatesByQuery = null!;

    private static readonly MyersBitParallel64 Engine = MyersBitParallel64.AsciiCaseInsensitive;

    [GlobalSetup]
    public void Setup()
    {
        _queries = BenchmarkData.PickDistinctQueries(
            BenchmarkData.AsciiCities,
            BenchmarkData.OneToManyQueryCount);
        _candidatesByQuery = BenchmarkData.BuildNoisyCandidatesPerQuery(
            BenchmarkData.AsciiCities,
            _queries.Length,
            CandidateCount);
    }

    [Benchmark(Baseline = true)]
    public long Myers_PreparedOnce_WithMaxDist()
    {
        long sum = 0;
        int maxDist = MaxDist;
        for (int q = 0; q < _queries.Length; q++)
        {
            string query = _queries[q];
            string[] candidates = _candidatesByQuery[q];
            using MyersPattern64 pat = Engine.Prepare(query);
            for (int i = 0; i < candidates.Length; i++)
            {
                int d = Engine.Distance(in pat, candidates[i], maxDist);
                if (d != int.MaxValue) sum += d;
            }
        }
        return sum;
    }

    [Benchmark]
    public long Myers_PreparedOnce_NoMaxDist()
    {
        long sum = 0;
        for (int q = 0; q < _queries.Length; q++)
        {
            string query = _queries[q];
            string[] candidates = _candidatesByQuery[q];
            using MyersPattern64 pat = Engine.Prepare(query);
            for (int i = 0; i < candidates.Length; i++)
                sum += Engine.Distance(in pat, candidates[i]);
        }
        return sum;
    }

    [Benchmark]
    public long NaiveLevenshteinReference_NoMaxDist()
    {
        long sum = 0;
        for (int q = 0; q < _queries.Length; q++)
        {
            string query = _queries[q];
            string[] candidates = _candidatesByQuery[q];
            for (int i = 0; i < candidates.Length; i++)
            {
                int d = NaiveLevenshtein.CaseInsensitive(query, candidates[i]);
                if (d != int.MaxValue) sum += d;
            }
        }
        return sum;
    }

    [Benchmark]
    public long NaiveLevenshteinReference_WithMaxDist()
    {
        long sum = 0;
        int maxDist = MaxDist;
        for (int q = 0; q < _queries.Length; q++)
        {
            string query = _queries[q];
            string[] candidates = _candidatesByQuery[q];
            for (int i = 0; i < candidates.Length; i++)
            {
                int d = NaiveLevenshtein.CaseInsensitive(query, candidates[i], maxDist);
                if (d != int.MaxValue) sum += d;
            }
        }
        return sum;
    }

    [Benchmark]
    public long WagnerFischerReference_WithMaxDist()
    {
        long sum = 0;
        int maxDist = MaxDist;
        for (int q = 0; q < _queries.Length; q++)
        {
            string query = _queries[q];
            string[] candidates = _candidatesByQuery[q];
            for (int i = 0; i < candidates.Length; i++)
            {
                int d = WagnerFischer.CaseInsensitive(query, candidates[i], maxDist);
                if (d != int.MaxValue) sum += d;
            }
        }
        return sum;
    }

    [Benchmark]
    public long UkkonenReference_WithMaxDist()
    {
        long sum = 0;
        int maxDist = MaxDist;
        for (int q = 0; q < _queries.Length; q++)
        {
            string query = _queries[q];
            string[] candidates = _candidatesByQuery[q];
            for (int i = 0; i < candidates.Length; i++)
            {
                int d = Ukkonen.CaseInsensitive(query, candidates[i], maxDist);
                if (d != int.MaxValue) sum += d;
            }
        }
        return sum;
    }
}
