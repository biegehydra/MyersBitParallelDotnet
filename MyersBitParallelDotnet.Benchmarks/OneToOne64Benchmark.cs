using BenchmarkDotNet.Attributes;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Compare a stream of independent (query, candidate) ASCII pairs. There is
/// no opportunity to amortize pattern preparation across multiple candidates,
/// so this measures the cost of a single distance call end-to-end (including
/// Myers' Prepare overhead).
/// </summary>
[MemoryDiagnoser]
public class OneToOne64Benchmark
{
    [Params(50, 500)]
    public int PairCount;

    private (string Query, string Candidate)[] _pairs = null!;
    private static readonly MyersBitParallel64 Engine = MyersBitParallel64.AsciiCaseInsensitive;

    [GlobalSetup]
    public void Setup()
    {
        _pairs = BenchmarkData.BuildOneToOnePairs(BenchmarkData.AsciiCities, PairCount);
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
