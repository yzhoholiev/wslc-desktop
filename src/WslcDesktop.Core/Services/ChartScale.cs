namespace WslcDesktop.Services;

/// <summary>
///     The value axis of a usage chart, which starts at zero and ends at a round step above the samples.
/// </summary>
public static class ChartScale
{
    /// <summary>
    ///     Room above the highest sample, so a line near the ceiling does not run along the chart's top edge.
    /// </summary>
    private const double Headroom = 1.1;

    private static readonly double[] PercentSteps = [5, 10, 25, 50, 100];

    public static double PercentCeiling(IReadOnlyCollection<double> values)
    {
        if (values.Count == 0)
        {
            return 100;
        }

        double needed = values.Max() * Headroom;
        return PercentSteps.FirstOrDefault(step => step >= needed, 100);
    }
}
