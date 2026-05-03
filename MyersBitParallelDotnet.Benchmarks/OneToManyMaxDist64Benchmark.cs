using BenchmarkDotNet.Attributes;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Compare a single ASCII query against many ASCII candidates with the
/// fuzzy-search threshold tuned via <c>maxDist</c>. The Myers engine
/// prepares its pattern once and reuses it across every candidate; this
/// is the access pattern that benefits most from <c>maxDist</c>-driven
/// early exit and from the optional <c>requiredCharMask</c> filter.
/// </summary>
/// <remarks>
/// Three Myers variants are measured:
/// <list type="bullet">
///   <item><description>No threshold — runs the bit-parallel kernel to completion.</description></item>
///   <item><description><c>maxDist</c> only — adds the length-difference gate, the char-mask popcount prune, and the in-loop score cutoff.</description></item>
///   <item><description><c>maxDist</c> + <c>requiredCharMask</c> — also rejects any candidate that omits a query symbol.</description></item>
/// </list>
/// All three reference algorithms (Naive Levenshtein, Wagner-Fischer,
/// Ukkonen) are also given the same <c>maxDist</c> so the comparison is
/// apples-to-apples. Naive and Wagner-Fischer can only short-circuit on
/// the length-difference gate; Ukkonen actually bands its DP to width
/// <c>maxDist</c> and is the algorithmic peer of the bit-parallel kernel
/// for fuzzy-search workloads.
/// </remarks>
[MemoryDiagnoser]
public class OneToManyMaxDist64Benchmark
{
    [Params(1, 2, 4)]
    public int MaxDist;

    [Params(1000)]
    public int CandidateCount;

    private string _query = null!;
    private string[] _candidates = null!;
    private ulong _requiredCharMask;
    private ulong _partialRequiredCharMask;

    private static readonly MyersBitParallel64 Engine = MyersBitParallel64.AsciiCaseInsensitive;

    [GlobalSetup]
    public void Setup()
    {
        _query = BenchmarkData.AsciiCities[0];
        _candidates = BenchmarkData.BuildNoisyCandidates(BenchmarkData.AsciiCities, CandidateCount);
        _requiredCharMask = Engine.BuildCharMask(_query);
        // lets say we know 1/2 the characters that should exist
        if (_query.Length > 1)
        {
            var start = Random.Shared.Next() % (_query.Length / 2);
            _partialRequiredCharMask = Engine.BuildCharMask(new string(_query.Skip(start).ToArray()));
        }
    }

    [Benchmark(Baseline = true)]
    public long Myers_PreparedOnce_WithMaxDist()
    {
        long sum = 0;
        using MyersPattern64 pat = Engine.Prepare(_query);
        int maxDist = MaxDist;
        for (int i = 0; i < _candidates.Length; i++)
        {
            int d = Engine.Distance(in pat, _candidates[i], maxDist);
            // Treat over-threshold candidates as zero so the loop body is
            // branch-free and we don't accidentally add int.MaxValue to the
            // accumulator.
            if (d != int.MaxValue) sum += d;
        }
        return sum;
    }

    [Benchmark]
    public long Myers_PreparedOnce_NoMaxDist()
    {
        long sum = 0;
        using MyersPattern64 pat = Engine.Prepare(_query);
        for (int i = 0; i < _candidates.Length; i++)
            sum += Engine.Distance(in pat, _candidates[i]);
        return sum;
    }

    [Benchmark]
    public long NaiveLevenshteinReference_NoMaxDist()
    {
        long sum = 0;
        int maxDist = MaxDist;
        for (int i = 0; i < _candidates.Length; i++)
        {
            int d = NaiveLevenshtein.CaseInsensitive(_query, _candidates[i]);
            if (d != int.MaxValue) sum += d;
        }
        return sum;
    }

    [Benchmark]
    public long NaiveLevenshteinReference_WithMaxDist()
    {
        long sum = 0;
        int maxDist = MaxDist;
        for (int i = 0; i < _candidates.Length; i++)
        {
            int d = NaiveLevenshtein.CaseInsensitive(_query, _candidates[i], maxDist);
            if (d != int.MaxValue) sum += d;
        }
        return sum;
    }

    [Benchmark]
    public long WagnerFischerReference_WithMaxDist()
    {
        long sum = 0;
        int maxDist = MaxDist;
        for (int i = 0; i < _candidates.Length; i++)
        {
            int d = WagnerFischer.CaseInsensitive(_query, _candidates[i], maxDist);
            if (d != int.MaxValue) sum += d;
        }
        return sum;
    }

    [Benchmark]
    public long UkkonenReference_WithMaxDist()
    {
        long sum = 0;
        int maxDist = MaxDist;
        for (int i = 0; i < _candidates.Length; i++)
        {
            int d = Ukkonen.CaseInsensitive(_query, _candidates[i], maxDist);
            if (d != int.MaxValue) sum += d;
        }
        return sum;
    }
}
