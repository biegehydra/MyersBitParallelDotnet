using BenchmarkDotNet.Attributes;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Compare a single Unicode query against many Unicode candidates. The
/// 64-symbol Unicode engine prepares its dictionary of bit masks once and
/// looks up each candidate scalar in the hot loop.
/// </summary>
[MemoryDiagnoser]
public class OneToMany64UnicodeBenchmark
{
    [Params(10, 100, 1000)]
    public int CandidateCount;

    private string _query = null!;
    private string[] _candidates = null!;
    private static readonly MyersBitParallel64Unicode Engine = MyersBitParallel64Unicode.CaseInsensitive;

    [GlobalSetup]
    public void Setup()
    {
        _query = BenchmarkData.UnicodeCities[0];
        _candidates = BenchmarkData.BuildNoisyCandidates(BenchmarkData.UnicodeCities, CandidateCount);
    }

    [Benchmark(Baseline = true)]
    public int MyersBitParallel_PreparedOnce()
    {
        int sum = 0;
        using MyersPattern64Unicode pat = Engine.Prepare(_query);
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
