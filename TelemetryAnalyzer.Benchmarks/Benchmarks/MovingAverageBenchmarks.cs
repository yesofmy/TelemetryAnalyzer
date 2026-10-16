using BenchmarkDotNet.Attributes;
using TelemetryAnalyzer.Core.Algorithms;

namespace TelemetryAnalyzer.Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class MovingAverageBenchmarks
{
    [Params(1_000, 10_000, 100_000)]
    public int DataSize { get; set; }

    [Params(10, 50)]
    public int WindowSize { get; set; }

    private double[] _values = [];

    [GlobalSetup]
    public void Setup()
    {
        var random = new Random(42);
        _values = new double[DataSize];

        for (int i = 0; i < _values.Length; i++)
        {
            _values[i] = random.NextDouble() * 300;
        }
    }

    [Benchmark(Baseline = true)]
    public double[] NaiveMovingAverage()
    {
        return MovingAverageFilter.Apply(_values, WindowSize);
    }

    [Benchmark]
    public double[] OptimizedMovingAverage()
    {
        return OptimizedMovingAverageFilter.Apply(_values, WindowSize);
    }
}
