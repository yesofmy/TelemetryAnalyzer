namespace TelemetryAnalyzer.Core.Algorithms;

public static class OptimizedMovingAverageFilter
{
    public static double[] Apply(double[] values, int windowSize)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (windowSize < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowSize),
                "Window size must be greater than zero.");
        }

        if (windowSize > values.Length && values.Length > 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(windowSize),
                "Window size cannot exceed the number of values.");
        }

        double[] result = new double[values.Length];
        double sum = 0;

        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i];

            if (i >= windowSize)
            {
                sum -= values[i - windowSize];
            }

            int count = Math.Min(i + 1, windowSize);
            result[i] = sum / count;
        }

        return result;
    }
}
