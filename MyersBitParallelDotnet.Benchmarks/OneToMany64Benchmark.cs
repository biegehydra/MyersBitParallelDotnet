using BenchmarkDotNet.Attributes;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Several distinct ASCII queries (deterministic random pick) each compared
/// against many ASCII candidates. Myers can prepare each pattern once and
/// reuse it across that query's candidates.
/// </summary>
[MemoryDiagnoser]
public class OneToMany64Benchmark
{
    [Params(10, 100, 1000)]
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
    public int MyersBitParallel_PreparedOnce()
    {
        int sum = 0;
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
    public int MyersBitParallel_PerCallPrepare()
    {
        int sum = 0;
        for (int q = 0; q < _queries.Length; q++)
        {
            string query = _queries[q];
            string[] candidates = _candidatesByQuery[q];
            for (int i = 0; i < candidates.Length; i++)
                sum += Engine.Distance(query, candidates[i]);
        }
        return sum;
    }

    [Benchmark]
    public int NaiveLevenshteinReference()
    {
        int sum = 0;
        for (int q = 0; q < _queries.Length; q++)
        {
            string query = _queries[q];
            string[] candidates = _candidatesByQuery[q];
            for (int i = 0; i < candidates.Length; i++)
                sum += NaiveLevenshtein.CaseInsensitive(query, candidates[i]);
        }
        return sum;
    }

    [Benchmark]
    public int WagnerFischerReference()
    {
        int sum = 0;
        for (int q = 0; q < _queries.Length; q++)
        {
            string query = _queries[q];
            string[] candidates = _candidatesByQuery[q];
            for (int i = 0; i < candidates.Length; i++)
                sum += WagnerFischer.CaseInsensitive(query, candidates[i]);
        }
        return sum;
    }

    [Benchmark]
    public int UkkonenReference()
    {
        int sum = 0;
        for (int q = 0; q < _queries.Length; q++)
        {
            string query = _queries[q];
            string[] candidates = _candidatesByQuery[q];
            for (int i = 0; i < candidates.Length; i++)
                sum += Ukkonen.CaseInsensitive(query, candidates[i]);
        }
        return sum;
    }
}
