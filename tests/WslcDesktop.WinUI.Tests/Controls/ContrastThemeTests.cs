using WslcDesktop.Controls;

namespace WslcDesktop.Tests.Controls;

public sealed class ContrastThemeTests
{
    [Fact]
    public void Changed_CanBeWatchedWithoutACoreWindow()
    {
        Action handler = () => { };

        ContrastTheme.Changed += handler;
        ContrastTheme.Changed -= handler;
    }
}
