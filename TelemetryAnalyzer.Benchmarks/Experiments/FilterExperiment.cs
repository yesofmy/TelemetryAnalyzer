using System.Diagnostics;
using System.Globalization;
using TelemetryAnalyzer.Core.Algorithms;

namespace TelemetryAnalyzer.Benchmarks.Experiments;

public static class FilterExperiment
{
    public static void Run()
    {
        int[] dataSizes = [1_000, 10_000, 100_000];
        int[] windowSizes = [10, 50, 100];

        string filePath = Path.Combine(
            Environment.CurrentDirectory,
            "filter-experiment.csv");

        using var writer = new StreamWriter(filePath);

        writer.WriteLine(
            "DataSize,WindowSize,NaiveMs,OptimizedMs," +
            "Speedup,MaxAbsoluteError");

        foreach (int dataSize in dataSizes)
        {
            double[] values = GenerateData(dataSize);

            foreach (int windowSize in windowSizes)
            {
                double[] naiveResult =
                    MovingAverageFilter.Apply(values, windowSize);

                double[] optimizedResult =
                    OptimizedMovingAverageFilter.Apply(values, windowSize);

                // Warm-up before timing.
                MovingAverageFilter.Apply(values, windowSize);
                OptimizedMovingAverageFilter.Apply(values, windowSize);

                var stopwatch = Stopwatch.StartNew();

                for (int repeat = 0; repeat < 5; repeat++)
                {
                    MovingAverageFilter.Apply(values, windowSize);
                }

                stopwatch.Stop();
                double naiveMs = stopwatch.Elapsed.TotalMilliseconds / 5;

                stopwatch.Restart();

                for (int repeat = 0; repeat < 5; repeat++)
                {
                    OptimizedMovingAverageFilter.Apply(values, windowSize);
                }

                stopwatch.Stop();
                double optimizedMs =
                    stopwatch.Elapsed.TotalMilliseconds / 5;

                double maxError = 0;

                for (int i = 0; i < dataSize; i++)
                {
                    double error = Math.Abs(
                        naiveResult[i] - optimizedResult[i]);

                    if (error > maxError)
                    {
                        maxError = error;
                    }
                }

                double speedup = optimizedMs > 0
                    ? naiveMs / optimizedMs
                    : 0;

                writer.WriteLine(string.Join(",",
                    dataSize.ToString(CultureInfo.InvariantCulture),
                    windowSize.ToString(CultureInfo.InvariantCulture),
                    naiveMs.ToString("F6", CultureInfo.InvariantCulture),
                    optimizedMs.ToString("F6", CultureInfo.InvariantCulture),
                    speedup.ToString("F3", CultureInfo.InvariantCulture),
                    maxError.ToString("G17", CultureInfo.InvariantCulture)));

                Console.WriteLine(
                    $"N={dataSize}, W={windowSize}: " +
                    $"naive={naiveMs:F3} ms, " +
                    $"optimized={optimizedMs:F3} ms, " +
                    $"speedup={speedup:F2}x, " +
                    $"max error={maxError:G6}");
            }
        }

        Console.WriteLine($"\nResults saved to: {filePath}");
    }

    private static double[] GenerateData(int size)
    {
        var random = new Random(42);
        double[] values = new double[size];

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = random.NextDouble() * 300;
        }

        return values;
    }
}
