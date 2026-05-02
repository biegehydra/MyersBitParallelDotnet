using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using MyersBitParallelDotnet.Benchmarks;

// Pick exactly one benchmark class to run, then `dotnet run -c Release`.
// Swap the type argument below to focus on a different engine or access pattern.
//
// Available benchmarks:
//   OneToOne64AsciiBenchmark            - 1:1 ASCII pairs through MyersBitParallel64Ascii
//   OneToMany64AsciiBenchmark           - 1:N ASCII through MyersBitParallel64Ascii
//   OneToManyMaxDist64AsciiBenchmark    - 1:N ASCII with maxDist + requiredCharMask pruning

var config = DefaultConfig.Instance
    .AddJob(Job.ShortRun);

BenchmarkRunner.Run<OneToManyMaxDist64AsciiBenchmark>(config);
