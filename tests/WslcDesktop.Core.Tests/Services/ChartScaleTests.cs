using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class ChartScaleTests
{
    [Theory]
    [InlineData(new double[0], 100)]
    [InlineData(new[] { 0d, 0d }, 5)]
    [InlineData(new[] { 2.1, 3.7, 3.2 }, 5)]
    [InlineData(new[] { 4.6 }, 10)]
    [InlineData(new[] { 11.8, 11.9 }, 25)]
    [InlineData(new[] { 30d, 45d }, 50)]
    [InlineData(new[] { 60d }, 100)]
    [InlineData(new[] { 99.5 }, 100)]
    [InlineData(new[] { 140d }, 100)]
    public void PercentCeiling_LeavesHeadroomAboveTheHighestValue(double[] values, double expected) =>
        Assert.Equal(expected, ChartScale.PercentCeiling(values));
}
