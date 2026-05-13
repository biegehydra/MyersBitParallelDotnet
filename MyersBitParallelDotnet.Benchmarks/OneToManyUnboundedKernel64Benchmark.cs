using BenchmarkDotNet.Attributes;
using MyersBitParallel;

namespace MyersBitParallelDotnet.Benchmarks;

/// <summary>
/// Compares the unbounded <c>Distance</c> overload (no per-iteration prune
/// branch, no candidate char-mask prepass) against the bounded overload
/// invoked with <c>maxDist == int.MaxValue</c> and
/// <c>requiredCharMask == 0UL</c>. Isolates the cost of the bounded path's
/// extra checks when the caller has no use for them.
/// </summary>
/// <remarks>
/// Patterns are prepared once in <see cref="Setup"/> so the timed region
/// only measures <c>Distance</c> calls.
/// </remarks>
[MemoryDiagnoser]
public class OneToManyUnboundedKernel64Benchmark
{
    [Params(10, 100, 1000)]
    public int CandidateCount;

    private string[] _queries = null!;
    private string[][] _candidatesByQuery = null!;
    private MyersPattern64[] _patterns = null!;
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

        _patterns = new MyersPattern64[_queries.Length];
        for (int q = 0; q < _queries.Length; q++)
            _patterns[q] = Engine.Prepare(_queries[q]);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_patterns is null) return;
        for (int q = 0; q < _patterns.Length; q++)
            _patterns[q].Dispose();
    }

    [Benchmark(Baseline = true)]
    public int Bounded_MaxValueAndZeroMask()
    {
        int sum = 0;
        for (int q = 0; q < _patterns.Length; q++)
        {
            ref readonly MyersPattern64 pat = ref _patterns[q];
            string[] candidates = _candidatesByQuery[q];
            for (int i = 0; i < candidates.Length; i++)
                sum += Engine.Distance(in pat, candidates[i], int.MaxValue, 0UL);
        }
        return sum;
    }

    [Benchmark]
    public int Unbounded()
    {
        int sum = 0;
        for (int q = 0; q < _patterns.Length; q++)
        {
            ref readonly MyersPattern64 pat = ref _patterns[q];
            string[] candidates = _candidatesByQuery[q];
            for (int i = 0; i < candidates.Length; i++)
                sum += Engine.Distance(in pat, candidates[i]);
        }
        return sum;
    }
}
