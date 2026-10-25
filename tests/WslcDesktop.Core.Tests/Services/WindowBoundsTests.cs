using WslcDesktop.Services;

namespace WslcDesktop.Tests.Services;

public sealed class WindowBoundsTests
{
    private static readonly ScreenRect WorkArea = new(0, 0, 1920, 1040);

    private static readonly Dictionary<string, (ScreenRect Bounds, ScreenRect WorkArea, ScreenRect Expected)> Fits = new()
    {
        ["bounds on screen kept"] = (new ScreenRect(100, 50, 1200, 800), WorkArea, new ScreenRect(100, 50, 1200, 800)),
        ["title bar above the work area pulled down"] = (new ScreenRect(100, -300, 1200, 800), WorkArea, new ScreenRect(100, 0, 1200, 800)),
        ["window off the right edge pulled back in"] = (new ScreenRect(1500, 50, 1200, 800), WorkArea, new ScreenRect(720, 50, 1200, 800)),
        ["bounds larger than the work area shrunk"] = (new ScreenRect(-50, 10, 2560, 1400), WorkArea, new ScreenRect(0, 0, 1920, 1040)),
        ["display left of the primary"] =
            (new ScreenRect(-1800, 20, 1200, 800), new ScreenRect(-2560, 0, 2560, 1400), new ScreenRect(-1800, 20, 1200, 800))
    };

    public static TheoryData<string> FitNames { get; } = new(Fits.Keys);

    [Theory]
    [MemberData(nameof(FitNames))]
    public void Fit_PlacesTheBoundsOnTheWorkArea(string caseName)
    {
        (ScreenRect bounds, ScreenRect workArea, ScreenRect expected) = Fits[caseName];

        Assert.Equal(expected, WindowBounds.Fit(bounds, workArea, 100));
    }

    [Theory]
    [InlineData(100, 50, 0, 0)]
    [InlineData(100, 50, -50, 800)]
    public void Fit_GivesUpOnBoundsWithoutASize(int x, int y, int width, int height) =>
        Assert.Null(WindowBounds.Fit(new ScreenRect(x, y, width, height), WorkArea, 100));

    [Theory]
    [InlineData(-2500, 50)]
    [InlineData(1860, 50)]
    [InlineData(100, 1000)]
    public void Fit_GivesUpOnBoundsBarelyOnTheDisplay(int x, int y) =>
        Assert.Null(WindowBounds.Fit(new ScreenRect(x, y, 1200, 800), WorkArea, 100));
}
