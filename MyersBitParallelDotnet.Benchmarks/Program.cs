using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using MyersBitParallelDotnet.Benchmarks;

// Pick exactly one benchmark class to run, then `dotnet run -c Release`.
// Swap the type argument below to focus on a different access pattern.
//
// Available benchmarks:
//   OneToOne64Benchmark            - 1:1 ASCII pairs through MyersBitParallel64
//   OneToMany64Benchmark           - 1:N ASCII through MyersBitParallel64
//   OneToManyMaxDist64Benchmark    - 1:N ASCII with maxDist + requiredCharMask pruning
//   OneToManyBestMatch64Benchmark  - 1:N ASCII substring search via MyersSubstringBitParallel64

var config = DefaultConfig.Instance
    .AddJob(Job.ShortRun);

BenchmarkRunner.Run<OneToManyBestMatch64Benchmark>(config);
