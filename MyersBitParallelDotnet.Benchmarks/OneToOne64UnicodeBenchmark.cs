using BenchmarkDotNet.Attributes;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Compare a stream of independent (query, candidate) Unicode pairs through
/// the 64-symbol Unicode engine. Strings are short international city names,
/// well within the 64 scalar-value limit.
/// </summary>
[MemoryDiagnoser]
public class OneToOne64UnicodeBenchmark
{
    [Params(50, 500)]
    public int PairCount;

    private (string Query, string Candidate)[] _pairs = null!;
    private static readonly MyersBitParallel64Unicode Engine = MyersBitParallel64Unicode.CaseInsensitive;

    [GlobalSetup]
    public void Setup()
    {
        _pairs = BenchmarkData.BuildOneToOnePairs(BenchmarkData.UnicodeCities, PairCount);
    }

    [Benchmark(Baseline = true)]
    public int MyersBitParallel()
    {
        int sum = 0;
        for (int i = 0; i < _pairs.Length; i++)
            sum += Engine.Distance(_pairs[i].Query, _pairs[i].Candidate);
        return sum;
    }

    [Benchmark]
    public int NaiveLevenshteinReference()
    {
        int sum = 0;
        for (int i = 0; i < _pairs.Length; i++)
            sum += NaiveLevenshtein.CaseInsensitive(_pairs[i].Query, _pairs[i].Candidate);
        return sum;
    }

    [Benchmark]
    public int WagnerFischerReference()
    {
        int sum = 0;
        for (int i = 0; i < _pairs.Length; i++)
            sum += WagnerFischer.CaseInsensitive(_pairs[i].Query, _pairs[i].Candidate);
        return sum;
    }

    [Benchmark]
    public int UkkonenReference()
    {
        int sum = 0;
        for (int i = 0; i < _pairs.Length; i++)
            sum += Ukkonen.CaseInsensitive(_pairs[i].Query, _pairs[i].Candidate);
        return sum;
    }
}
