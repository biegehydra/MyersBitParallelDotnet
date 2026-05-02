using BenchmarkDotNet.Attributes;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Compare a single Unicode query against many Unicode candidates through
/// the General Unicode engine.
/// </summary>
[MemoryDiagnoser]
public class OneToManyGeneralUnicodeBenchmark
{
    [Params(10, 100, 1000)]
    public int CandidateCount;

    private string _query = null!;
    private string[] _candidates = null!;
    private static readonly MyersBitParallelGeneralUnicode Engine = MyersBitParallelGeneralUnicode.CaseInsensitive;

    [GlobalSetup]
    public void Setup()
    {
        _query = BenchmarkData.UnicodeCities[0];
        _candidates = BenchmarkData.BuildNoisyCandidates(BenchmarkData.UnicodeCities, CandidateCount);
    }

    [Benchmark(Baseline = true)]
    public int MyersBitParallelGeneral_PreparedOnce()
    {
        int sum = 0;
        using MyersPatternGeneralUnicode pat = Engine.Prepare(_query);
        for (int i = 0; i < _candidates.Length; i++)
            sum += Engine.Distance(in pat, _candidates[i]);
        return sum;
    }

    [Benchmark]
    public int MyersBitParallelGeneral_PerCallPrepare()
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
