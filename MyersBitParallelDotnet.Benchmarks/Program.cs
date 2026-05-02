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
//   OneToOne64UnicodeBenchmark          - 1:1 Unicode pairs through MyersBitParallel64Unicode
//   OneToMany64UnicodeBenchmark         - 1:N Unicode through MyersBitParallel64Unicode
//   OneToOneGeneralAsciiBenchmark       - 1:1 ASCII pairs through MyersBitParallelGeneralAscii
//   OneToManyGeneralAsciiBenchmark      - 1:N ASCII through MyersBitParallelGeneralAscii
//   OneToOneGeneralUnicodeBenchmark     - 1:1 Unicode pairs through MyersBitParallelGeneralUnicode
//   OneToManyGeneralUnicodeBenchmark    - 1:N Unicode through MyersBitParallelGeneralUnicode

var config = DefaultConfig.Instance
    .AddJob(Job.ShortRun);

BenchmarkRunner.Run<OneToManyMaxDist64AsciiBenchmark>(config);
