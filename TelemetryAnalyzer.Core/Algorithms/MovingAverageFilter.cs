namespace TelemetryAnalyzer.Core.Algorithms;

public static class MovingAverageFilter
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

        for (int i = 0; i < values.Length; i++)
        {
            int start = Math.Max(0, i - windowSize + 1);

            double sum = 0;
            int count = 0;

            for (int j = start; j <= i; j++)
            {
                sum += values[j];
                count++;
            }

            result[i] = sum / count;
        }

        return result;
    }
}
