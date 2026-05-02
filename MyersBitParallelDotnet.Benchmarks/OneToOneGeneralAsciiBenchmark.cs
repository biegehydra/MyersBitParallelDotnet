using BenchmarkDotNet.Attributes;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Compare a stream of independent (query, candidate) ASCII pairs through
/// the General ASCII engine, which uses dynamic-programming Levenshtein
/// instead of the bit-parallel kernel. This benchmark exists primarily to
/// quantify the per-call overhead of the general path against the reference
/// algorithms it most closely resembles.
/// </summary>
[MemoryDiagnoser]
public class OneToOneGeneralAsciiBenchmark
{
    [Params(50, 500)]
    public int PairCount;

    private (string Query, string Candidate)[] _pairs = null!;
    private static readonly MyersBitParallelGeneralAscii Engine = MyersBitParallelGeneralAscii.CaseInsensitive;

    [GlobalSetup]
    public void Setup()
    {
        _pairs = BenchmarkData.BuildOneToOnePairs(BenchmarkData.AsciiCities, PairCount);
    }

    [Benchmark(Baseline = true)]
    public int MyersBitParallelGeneral()
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
