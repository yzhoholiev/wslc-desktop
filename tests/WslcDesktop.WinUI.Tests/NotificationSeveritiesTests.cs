using Microsoft.UI.Xaml.Controls;
using WslcDesktop.Services;

namespace WslcDesktop.Tests;

public sealed class NotificationSeveritiesTests
{
    [Theory]
    [InlineData(NotificationSeverity.Success, InfoBarSeverity.Success)]
    [InlineData(NotificationSeverity.Error, InfoBarSeverity.Error)]
    public void ToInfoBar_MapsEachSeverity(NotificationSeverity severity, InfoBarSeverity expected) =>
        Assert.Equal(expected, NotificationSeverities.ToInfoBar(severity));
}
