using BenchmarkDotNet.Attributes;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Compare a single ASCII query against many ASCII candidates. The Myers
/// engine prepares its pattern once and reuses it across every candidate;
/// this is the access pattern that the bit-parallel kernel is designed for.
/// </summary>
[MemoryDiagnoser]
public class OneToMany64AsciiBenchmark
{
    [Params(10, 100, 1000)]
    public int CandidateCount;

    private string _query = null!;
    private string[] _candidates = null!;
    private static readonly MyersBitParallel64Ascii Engine = MyersBitParallel64Ascii.CaseInsensitive;

    [GlobalSetup]
    public void Setup()
    {
        _query = BenchmarkData.AsciiCities[0];
        _candidates = BenchmarkData.BuildNoisyCandidates(BenchmarkData.AsciiCities, CandidateCount);
    }

    [Benchmark(Baseline = true)]
    public int MyersBitParallel_PreparedOnce()
    {
        int sum = 0;
        using MyersPattern64Ascii pat = Engine.Prepare(_query);
        for (int i = 0; i < _candidates.Length; i++)
            sum += Engine.Distance(in pat, _candidates[i]);
        return sum;
    }

    [Benchmark]
    public int MyersBitParallel_PerCallPrepare()
    {
        int sum = 0;
        for (int i = 0; i < _candidates.Length; i++)
            sum += Engine.Distance(_query, _candidates[i]);
        return sum;
    }

    [Benchmark]
    public int NaiveLevenshteinReference()
    {
        int sum = 0;
        for (int i = 0; i < _candidates.Length; i++)
            sum += NaiveLevenshtein.CaseInsensitive(_query, _candidates[i]);
        return sum;
    }

    [Benchmark]
    public int WagnerFischerReference()
    {
        int sum = 0;
        for (int i = 0; i < _candidates.Length; i++)
            sum += WagnerFischer.CaseInsensitive(_query, _candidates[i]);
        return sum;
    }

    [Benchmark]
    public int UkkonenReference()
    {
        int sum = 0;
        for (int i = 0; i < _candidates.Length; i++)
            sum += Ukkonen.CaseInsensitive(_query, _candidates[i]);
        return sum;
    }
}
