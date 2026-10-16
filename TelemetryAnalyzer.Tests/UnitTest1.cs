using TelemetryAnalyzer.Core.Algorithms;

namespace TelemetryAnalyzer.Tests;

public class MovingAverageFilterTests
{
    [Fact]
    public void Apply_WithWindowSizeThree_ReturnsExpectedValues()
    {
        double[] values = [1, 2, 3, 4, 5];

        double[] result = MovingAverageFilter.Apply(values, 3);

        double[] expected = [1, 1.5, 2, 3, 4];

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Apply_WithWindowSizeOne_ReturnsOriginalValues()
    {
        double[] values = [4, 8, 12];

        double[] result = MovingAverageFilter.Apply(values, 1);

        Assert.Equal(values, result);
    }

    [Fact]
    public void Apply_WithEmptyArray_ReturnsEmptyArray()
    {
        double[] values = [];

        double[] result = MovingAverageFilter.Apply(values, 1);

        Assert.Empty(result);
    }

    [Fact]
    public void Apply_WithZeroWindowSize_ThrowsException()
    {
        double[] values = [1, 2, 3];

        Assert.Throws<ArgumentOutOfRangeException>(
            () => MovingAverageFilter.Apply(values, 0));
    }

    [Fact]
    public void Apply_WithWindowLargerThanArray_ThrowsException()
    {
        double[] values = [1, 2, 3];

        Assert.Throws<ArgumentOutOfRangeException>(
            () => MovingAverageFilter.Apply(values, 4));
    }

    [Fact]
    public void Apply_WithNullArray_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(
            () => MovingAverageFilter.Apply(null!, 1));
    }

    
[Fact]
public void OptimizedFilter_WithWindowSizeThree_ReturnsExpectedValues()
{
    double[] values = [1, 2, 3, 4, 5];

    double[] result = OptimizedMovingAverageFilter.Apply(values, 3);

    double[] expected = [1, 1.5, 2, 3, 4];

    Assert.Equal(expected, result);
}

[Fact]
public void OptimizedFilter_MatchesNaiveFilter()
{
    double[] values = [10, 20, 15, 30, 25, 40, 35];

    double[] expected = MovingAverageFilter.Apply(values, 3);
    double[] actual = OptimizedMovingAverageFilter.Apply(values, 3);

    Assert.Equal(expected.Length, actual.Length);

    for (int i = 0; i < expected.Length; i++)
    {
        Assert.Equal(expected[i], actual[i], 10);
    }
}



    [Theory]
    [InlineData(1, 1)]
    [InlineData(10, 1)]
    [InlineData(10, 3)]
    [InlineData(100, 10)]
    [InlineData(1000, 50)]
    [InlineData(10000, 100)]
    public void OptimizedFilter_MatchesNaiveFilter_ForDifferentSizes(
        int dataSize,
        int windowSize)
    {
        var random = new Random(42);
        double[] values = new double[dataSize];

        for (int i = 0; i < values.Length; i++)
        {
            values[i] = random.NextDouble() * 300;
        }

        double[] expected =
            MovingAverageFilter.Apply(values, windowSize);

        double[] actual =
            OptimizedMovingAverageFilter.Apply(values, windowSize);

        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.InRange(
                Math.Abs(expected[i] - actual[i]),
                0,
                1e-8);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void OptimizedFilter_WithWindowEqualToArrayLength_ReturnsSameAsNaive(
        int size)
    {
        double[] values = [3, 8, 2, 10, 7];

        double[] expected =
            MovingAverageFilter.Apply(values, size);

        double[] actual =
            OptimizedMovingAverageFilter.Apply(values, size);

        Assert.Equal(expected.Length, actual.Length);

        for (int i = 0; i < expected.Length; i++)
        {
            Assert.InRange(
                Math.Abs(expected[i] - actual[i]),
                0,
                1e-8);
        }
    }

}
